Option Strict On
Option Explicit On

Imports System
Imports System.IO
Imports MeltySynth

' Host renderer contract.  Nothing in the guest-visible daughterboard references a
' concrete synth library.  Brick 6 adds PCM rendering here while keeping the
' renderer on the wavetable host worker thread.  MeltySynth is not thread-safe, so
' MIDI messages and RenderStereo calls must remain serialized by WavetableHost.
Public Interface IWavetableRenderer
    ReadOnly Property RendererName As String
    ReadOnly Property IsBankLoaded As Boolean
    ReadOnly Property LoadedBankPath As String
    ReadOnly Property SampleRate As Integer

    ' Query only from the renderer-owning WavetableHost worker.  The host mirrors
    ' this count across the thread boundary; callers must never touch MeltySynth
    ' directly from another thread.
    ReadOnly Property ActiveVoiceCount As Integer

    Sub LoadBank(fullPathInBed As String)
    Sub ProcessMidiMessage(messageInBed As WavetableMidiMessage)
    Sub ResetMidiState()
    Sub RenderStereo(leftInBed As Single(), rightInBed As Single(), frameCountInBed As Integer)
End Interface

' MeltySynth is intentionally a host implementation detail.  It is a pure .NET
' SoundFont synth and does not participate in guest IRQ/DMA/port behavior.
Friend NotInheritable Class MeltySynthWavetableRenderer
    Implements IWavetableRenderer

    Private Const RendererSampleRateInBed As Integer = 48000
    Private _synthesizerInBed As Synthesizer
    Private _loadedBankPathInBed As String = String.Empty

    ' VB cannot safely declare Span(Of Single) locals with this project/compiler
    ' combination (BC30668).  MeltySynth also exposes an array Render overload, so
    ' full work blocks use the reusable host arrays directly.  A short tail block
    ' uses cached exact-length arrays so we do not render/advance synth time beyond
    ' the number of guest-time frames actually requested.
    Private _partialLeftInBed As Single() = Array.Empty(Of Single)()
    Private _partialRightInBed As Single() = Array.Empty(Of Single)()

    Public ReadOnly Property RendererName As String Implements IWavetableRenderer.RendererName
        Get
            Return "MeltySynth"
        End Get
    End Property

    Public ReadOnly Property IsBankLoaded As Boolean Implements IWavetableRenderer.IsBankLoaded
        Get
            Return _synthesizerInBed IsNot Nothing
        End Get
    End Property

    Public ReadOnly Property LoadedBankPath As String Implements IWavetableRenderer.LoadedBankPath
        Get
            Return _loadedBankPathInBed
        End Get
    End Property

    Public ReadOnly Property SampleRate As Integer Implements IWavetableRenderer.SampleRate
        Get
            Return RendererSampleRateInBed
        End Get
    End Property

    Public ReadOnly Property ActiveVoiceCount As Integer Implements IWavetableRenderer.ActiveVoiceCount
        Get
            Dim synthInBed As Synthesizer = _synthesizerInBed
            If synthInBed Is Nothing Then Return 0
            Return Math.Max(0, synthInBed.ActiveVoiceCount)
        End Get
    End Property

    Public Sub LoadBank(fullPathInBed As String) Implements IWavetableRenderer.LoadBank
        If String.IsNullOrWhiteSpace(fullPathInBed) Then Throw New ArgumentNullException(NameOf(fullPathInBed))
        Dim normalizedInBed As String = Path.GetFullPath(fullPathInBed)
        If Not File.Exists(normalizedInBed) Then Throw New FileNotFoundException("SoundFont bank was not found.", normalizedInBed)
        If Not String.Equals(Path.GetExtension(normalizedInBed), ".sf2", StringComparison.OrdinalIgnoreCase) Then
            Throw New InvalidDataException("Only .sf2 SoundFont banks are supported by the wavetable renderer.")
        End If

        ' Construction parses/loads the SoundFont, so WavetableHost calls this only
        ' from its background worker.  Never move this onto the machine owner thread.
        Dim synthInBed As New Synthesizer(normalizedInBed, RendererSampleRateInBed)
        _synthesizerInBed = synthInBed
        _loadedBankPathInBed = normalizedInBed
    End Sub

    Public Sub ProcessMidiMessage(messageInBed As WavetableMidiMessage) Implements IWavetableRenderer.ProcessMidiMessage
        Dim synthInBed As Synthesizer = _synthesizerInBed
        If synthInBed Is Nothing OrElse Not messageInBed.IsChannelMessage Then Return

        synthInBed.ProcessMidiMessage(messageInBed.ChannelZeroBased,
                                      CInt(messageInBed.CommandNibble),
                                      CInt(messageInBed.Data1),
                                      CInt(messageInBed.Data2))
    End Sub

    Public Sub ResetMidiState() Implements IWavetableRenderer.ResetMidiState
        Dim synthInBed As Synthesizer = _synthesizerInBed
        If synthInBed Is Nothing Then Return

        ' MIDI System Reset / physical board reset is represented without re-reading
        ' the SoundFont.  This releases voices, resets controllers, program, and
        ' pitch bend to deterministic receiver defaults.  Vendor SysEx behavior is
        ' intentionally still outside the generic Wave Blaster profile.
        For channelInBed As Integer = 0 To 15
            synthInBed.ProcessMidiMessage(channelInBed, &HB0, 120, 0) ' All Sound Off
            synthInBed.ProcessMidiMessage(channelInBed, &HB0, 121, 0) ' Reset All Controllers
            synthInBed.ProcessMidiMessage(channelInBed, &HC0, 0, 0)   ' Program 0
            synthInBed.ProcessMidiMessage(channelInBed, &HE0, 0, 64)  ' Pitch center
        Next
    End Sub

    Public Sub RenderStereo(leftInBed As Single(),
                            rightInBed As Single(),
                            frameCountInBed As Integer) Implements IWavetableRenderer.RenderStereo
        If leftInBed Is Nothing Then Throw New ArgumentNullException(NameOf(leftInBed))
        If rightInBed Is Nothing Then Throw New ArgumentNullException(NameOf(rightInBed))
        If frameCountInBed < 0 OrElse frameCountInBed > leftInBed.Length OrElse frameCountInBed > rightInBed.Length Then
            Throw New ArgumentOutOfRangeException(NameOf(frameCountInBed))
        End If
        If frameCountInBed = 0 Then Return

        Dim synthInBed As Synthesizer = _synthesizerInBed
        If synthInBed Is Nothing Then
            Array.Clear(leftInBed, 0, frameCountInBed)
            Array.Clear(rightInBed, 0, frameCountInBed)
            Return
        End If

        ' MeltySynth has a Single() Render overload.  Use the caller's arrays directly
        ' for a complete work block.  For a short final block, render into cached
        ' exact-length arrays; rendering the whole 512-frame work buffer would advance
        ' synth time for samples that the emulated machine never requested.
        If frameCountInBed = leftInBed.Length AndAlso frameCountInBed = rightInBed.Length Then
            synthInBed.Render(leftInBed, rightInBed)
            Return
        End If

        If _partialLeftInBed.Length <> frameCountInBed Then
            _partialLeftInBed = New Single(frameCountInBed - 1) {}
            _partialRightInBed = New Single(frameCountInBed - 1) {}
        End If

        synthInBed.Render(_partialLeftInBed, _partialRightInBed)
        Array.Copy(_partialLeftInBed, 0, leftInBed, 0, frameCountInBed)
        Array.Copy(_partialRightInBed, 0, rightInBed, 0, frameCountInBed)
    End Sub
End Class
