Option Strict On
Option Explicit On

Imports System
Imports System.Collections.Generic
Imports System.Text

' Guest-visible shell for a Wave Blaster-compatible MIDI daughterboard.
'
' Brick 4 adds MIDI 1.0 stream parsing and renderer-independent channel/note state.
' Brick 5 adds a narrow non-blocking render-event sink after that device state has
' been updated.  The board still opens NO SoundFont files and produces NO host PCM.
' Guest MIDI enters only through the owning sound card's real MPU/MIDI path.
Public NotInheritable Class WaveBlasterDaughterboard
    Private Const RecentMidiCapacity As Integer = 32

    Private ReadOnly _profileInBed As WavetableProfile
    Private ReadOnly _recentMidiInBed As New Queue(Of Byte)(RecentMidiCapacity)
    Private ReadOnly _midiParserInBed As New WavetableMidiParser()
    Private ReadOnly _midiStateInBed As New WavetableMidiState()
    Private ReadOnly _diagnosticsInBed As New WavetableDiagnostics()
    Private ReadOnly _renderSinkInBed As IWavetableRenderEventSink
    Private _midiBytesReceivedInBed As ULong
    Private _hardwareResetCountInBed As ULong
    Private _hasLastMidiByteInBed As Boolean
    Private _lastMidiByteInBed As Byte
    Private _renderSequenceInBed As ULong
    Private _renderEventsPostedInBed As ULong
    Private _renderEventsDroppedInBed As ULong

    Public Sub New(profileInBed As WavetableProfile,
                   Optional renderSinkInBed As IWavetableRenderEventSink = Nothing)
        If profileInBed Is Nothing Then Throw New ArgumentNullException(NameOf(profileInBed))
        _profileInBed = profileInBed
        _renderSinkInBed = renderSinkInBed
    End Sub

    Public ReadOnly Property Profile As WavetableProfile
        Get
            Return _profileInBed
        End Get
    End Property

    Public ReadOnly Property MidiBytesReceived As ULong
        Get
            Return _midiBytesReceivedInBed
        End Get
    End Property

    Public ReadOnly Property MidiState As WavetableMidiState
        Get
            Return _midiStateInBed
        End Get
    End Property

    ' Guest-data ingress.  The caller is the physical connector attached to the
    ' SB16 MPU transmitter; there is intentionally no host/UI "play note" shortcut.
    Public Sub ReceiveMidiByte(valueInBed As Byte)
        If RuntimeDiagnostics.Enabled Then
            _midiBytesReceivedInBed += 1UL
            _lastMidiByteInBed = valueInBed
            _hasLastMidiByteInBed = True

            If _recentMidiInBed.Count >= RecentMidiCapacity Then _recentMidiInBed.Dequeue()
            _recentMidiInBed.Enqueue(valueInBed)
        End If

        Dim decodedInBed As Nullable(Of WavetableMidiMessage) = _midiParserInBed.FeedByte(valueInBed)
        If Not decodedInBed.HasValue Then Return

        Dim messageInBed As WavetableMidiMessage = decodedInBed.Value
        _midiStateInBed.ApplyMessage(messageInBed)
        If RuntimeDiagnostics.Enabled Then _diagnosticsInBed.RecordMessage(messageInBed)

        _renderSequenceInBed += 1UL
        TryPostRenderEventInBed(New WavetableRenderEvent(WavetableRenderEventKind.MidiMessage,
                                                        _renderSequenceInBed,
                                                        messageInBed))
    End Sub

    ' Hardware/card reset only.  An MPU-401 command-port reset is still not treated
    ' as a daughterboard reset: that electrical connection has not been established
    ' from documentation.  A MIDI FFh byte arriving on the data stream IS handled
    ' separately as MIDI System Reset by WavetableMidiState.
    Public Sub HardwareReset()
        If RuntimeDiagnostics.Enabled Then
            _hardwareResetCountInBed += 1UL
            _recentMidiInBed.Clear()
            _hasLastMidiByteInBed = False
            _diagnosticsInBed.ResetRecent()
        End If
        _midiParserInBed.Reset()
        _midiStateInBed.Reset()

        _renderSequenceInBed += 1UL
        TryPostRenderEventInBed(New WavetableRenderEvent(WavetableRenderEventKind.HardwareReset,
                                                        _renderSequenceInBed,
                                                        New WavetableMidiMessage()))
    End Sub

    Private Sub TryPostRenderEventInBed(eventInBed As WavetableRenderEvent)
        Dim sinkInBed As IWavetableRenderEventSink = _renderSinkInBed
        If sinkInBed Is Nothing Then Return
        Try
            If sinkInBed.TryPostEvent(eventInBed) Then
                If RuntimeDiagnostics.Enabled Then _renderEventsPostedInBed += 1UL
            Else
                If RuntimeDiagnostics.Enabled Then _renderEventsDroppedInBed += 1UL
            End If
        Catch
            ' Host renderer failures must never escape back through the guest-visible
            ' MIDI path and stop the machine thread.  The host diagnostic side owns
            ' the detailed fault; the device only counts the failed boundary handoff.
            If RuntimeDiagnostics.Enabled Then _renderEventsDroppedInBed += 1UL
        End Try
    End Sub

    Public Function DiagnosticText() As String
        Dim recentInBed As String = "(none)"
        If _recentMidiInBed.Count > 0 Then
            Dim builderInBed As New StringBuilder()
            For Each valueInBed As Byte In _recentMidiInBed
                If builderInBed.Length > 0 Then builderInBed.Append(" ")
                builderInBed.Append(valueInBed.ToString("X2"))
            Next
            recentInBed = builderInBed.ToString()
        End If

        Return "    daughterboard        : " & _profileInBed.DisplayName & Environment.NewLine &
               "    MIDI bytes received  : " & _midiBytesReceivedInBed.ToString("N0") & Environment.NewLine &
               "    last MIDI byte       : " & If(_hasLastMidiByteInBed, _lastMidiByteInBed.ToString("X2") & "h", "(none)") & Environment.NewLine &
               "    hardware resets      : " & _hardwareResetCountInBed.ToString("N0") & Environment.NewLine &
               "    renderer events      : " & _renderEventsPostedInBed.ToString("N0") & " posted / " & _renderEventsDroppedInBed.ToString("N0") & " dropped" & Environment.NewLine &
               "    recent MIDI bytes    : " & recentInBed & Environment.NewLine &
               _midiParserInBed.DiagnosticText() & Environment.NewLine &
               _diagnosticsInBed.DiagnosticText() & Environment.NewLine &
               _midiStateInBed.DiagnosticText(_profileInBed)
    End Function
End Class
