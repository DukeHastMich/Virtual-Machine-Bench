Imports System
Imports System.Collections.Generic
Imports System.Collections.Concurrent
Imports System.Runtime.InteropServices
Imports System.Threading

' Creative Sound Blaster 16-class ISA adapter.
'
' The guest-facing boundary is the actual ISA programming model: DSP command
' and response FIFOs, mixer configuration/status registers, 8237 DMA requests,
' 8259 IRQs and Yamaha-compatible FM ports.  Host audio is only a transducer for
' the emulated DAC/FM output and never substitutes for the guest DMA/IRQ path.
Public NotInheritable Class SoundBlaster16Isa
    Implements IPortDevice, IPortDecodeCandidateProvider, IClockedDevice, IClockWakeSource, IResettableDevice, IDisposable

    Private Const PicosecondsPerSecond As Long = 1000000000000L
    Private Const HostSampleRate As Integer = 48000
    ' Cross-thread host rendering is deliberately coalesced to 5 ms.  Audio-affecting
    ' guest writes flush the partial span first, preserving exact register ordering.
    Private Const HostRenderBatchFrames As Long = 240L
    Private Const DspMinimumResetPulsePicoseconds As Long = 3000000L      ' 3 us
    Private Const DspInitializationPicoseconds As Long = 100000000L      ' about 100 us
    Private Const AdpcmNone As Integer = 0
    Private Const Adpcm2Bit As Integer = 2
    Private Const Adpcm3Bit As Integer = 3
    Private Const Adpcm4Bit As Integer = 4

    ' Creative documents the ADPCM framing/commands but not the internal
    ' predictor law.  Keep the decoder expressed algorithmically rather than as
    ' a copied implementation table; its transition behavior is cross-checked
    ' against an independent emulator implementation in the CP08 audit notes.

    Private _basePort As UInt16
    Private _configuredIrq As Integer
    Private _configuredDma8 As Integer
    Private _configuredDma16 As Integer
    Private _mpuBasePort As UInt16 = &H330US
    Private _gamePortEnabled As Boolean = True
    Private ReadOnly _masterPic As Pic8259
    Private ReadOnly _slavePic As Pic8259
    Private ReadOnly _dma As Dma8237
    Private ReadOnly _dspReadQueue As New Queue(Of Byte)()
    Private ReadOnly _mixer(255) As Byte
    Private ReadOnly _commandParams As New List(Of Byte)(4)
    Private ReadOnly _opl As New Opl3FmCore(HostSampleRate)
    ' Machine-thread mirror of the guest-visible YMF262 address latches.  Host
    ' synthesis receives atomic register writes so an overloaded audio worker
    ' can never lose only the address or only the data half of an OPL write.
    Private ReadOnly _hostOplAddress(1) As Byte
    Private ReadOnly _hostAudioInBed As New Sb16HostAudioEngine(HostSampleRate)
    Private ReadOnly _mpuReadQueue As New Queue(Of Byte)()
    Private ReadOnly _waveBlasterConnector As New WaveBlasterConnector()
    Private _wavetableHostInBed As WavetableHost
    Private ReadOnly _processExitHandler As EventHandler

    ' Physical daughterboard selection applied by the chassis configuration path.
    ' The host bank identity remains configuration only; guest MIDI reaches the
    ' device shell solely through the real MPU UART -> Wave Blaster connector path.
    Private _wavetableHardwareSettingsInBed As WavetableAttachmentSettings = New WavetableAttachmentSettings()

    Private _irq As Integer
    Private _dma8 As Integer
    Private _dma16 As Integer
    Private _mixerIndex As Byte
    Private _dspResetAsserted As Boolean
    Private _dspResetAssertedPicoseconds As Long
    Private _dspInitializationPicosecondsRemaining As Long
    Private _pendingCommand As Integer = -1
    Private _pendingParamCount As Integer
    Private _testRegister As Byte
    ' DSP 4.xx keeps the D1h/D3h speaker flag for D8h status, but those
    ' commands no longer gate the actual digitized output signal.
    Private _speakerEnabled As Boolean
    Private _sampleRate As Integer = 22050
    Private _outputSampleRate As Integer = 22050
    Private _inputSampleRate As Integer = 22050
    Private _legacyBlockUnits As Integer = 1
    Private _legacyRequestedSampleRate As Integer = 22050
    Private _silenceUnitsRemaining As Integer
    Private _silenceClockNumerator As Long

    ' Creative ADPCM is a legacy 8-bit DMA playback format retained by DSP 4.xx.
    ' The first byte of a reference block seeds the adaptive decoder and is not
    ' itself an ADPCM code.  Subsequent compressed bytes expand to 4/3/2
    ' decoded DAC samples per compressed byte for 2/3/4-bit ADPCM respectively.
    Private _playbackAdpcmMode As Integer = AdpcmNone
    Private _adpcmReference As Integer = &H80
    Private _adpcmScale As Integer
    Private _adpcmNeedsReference As Boolean
    Private _adpcmBlockPendingCompletion As Boolean
    Private ReadOnly _adpcmDecodedSamples As New Queue(Of Byte)()

    ' Programming a single-cycle transfer while an auto-init transfer is active
    ' takes effect at the current block boundary.  Keep that next transfer
    ' electrically staged instead of replacing the live block mid-flight.
    Private _pendingTransferValid As Boolean
    Private _pendingTransferBits16 As Boolean
    Private _pendingTransferStereo As Boolean
    Private _pendingTransferSigned As Boolean
    Private _pendingTransferInput As Boolean
    Private _pendingTransferUnits As Integer
    Private _pendingTransferAdpcmMode As Integer
    Private _pendingTransferAdpcmReference As Boolean

    Private _playbackActive As Boolean
    Private _playbackPaused As Boolean
    Private _playbackInput As Boolean
    Private _playback16Bit As Boolean
    Private _playbackStereo As Boolean
    Private _playbackSigned As Boolean
    Private _playbackAutoInit As Boolean
    Private _playbackBlockUnits As Integer
    Private _playbackUnitsRemaining As Integer
    Private _exitAutoInitAfterBlock As Boolean
    Private _pcmClockNumerator As Long
    Private _hostClockNumerator As Long
    ' Host audio is deliberately batched.  This counter is machine-thread-owned and
    ' holds elapsed 48-kHz frames that have not yet crossed to the audio workers.
    Private _hostRenderFramesPendingInBed As Long
    Private _hostPcmFramesPendingInBed As Integer
    Private _hostPcmIntervalStartLeftInBed As Short
    Private _hostPcmIntervalStartRightInBed As Short
    Private _currentLeft As Short
    Private _currentRight As Short
    Private _pendingIrqBits As Byte ' bit0 8-bit, bit1 16-bit, bit2 MPU
    Private _irqLineAsserted As Boolean
    Private _mpuUartMode As Boolean

    ' Legacy Sound Blaster MIDI interface implemented by the DSP itself.
    ' This is distinct from the SB16 MPU-401 UART at 300h/330h.  In DSP UART
    ' mode every byte written to the DSP data/command port is MIDI data until
    ' the DSP reset port is pulsed.  With no external MIDI-IN source attached,
    ' polling/input modes remain electrically quiet rather than synthesizing data.
    Private _sbMidiInputPolling As Boolean
    Private _sbMidiInputInterrupt As Boolean
    Private _sbMidiUartMode As Boolean
    Private _sbMidiUartInterrupt As Boolean
    Private _sbMidiTimestamp As Boolean
    Private _sbMidiRestoreAfterReset As Boolean
    Private _sbMidiSavedSpeakerEnabled As Boolean
    Private _sbMidiSavedSampleRate As Integer
    Private _sbMidiSavedOutputSampleRate As Integer
    Private _sbMidiSavedInputSampleRate As Integer
    Private _sbMidiSavedLegacyRequestedSampleRate As Integer
    Private _sbMidiSavedLegacyBlockUnits As Integer
    Private _sbMidiSavedTestRegister As Byte

    Private _disposed As Boolean
    Private _dspUnderruns As ULong
    Private _dmaBytesPlayed As ULong

    Public Sub New(basePort As UInt16,
                   defaultIrq As Integer,
                   dma8Channel As Integer,
                   dma16Channel As Integer,
                   masterPic As Pic8259,
                   slavePic As Pic8259,
                   dma As Dma8237)
        If masterPic Is Nothing Then Throw New ArgumentNullException(NameOf(masterPic))
        If slavePic Is Nothing Then Throw New ArgumentNullException(NameOf(slavePic))
        If dma Is Nothing Then Throw New ArgumentNullException(NameOf(dma))
        _basePort = basePort
        _masterPic = masterPic
        _slavePic = slavePic
        _dma = dma
        Dim effectiveDma16InBed As Integer =
            If(dma16Channel = IsaExpansionCardConfiguration.Sb16Dma16ViaLowChannel, dma8Channel, dma16Channel)
        _configuredIrq = defaultIrq
        _configuredDma8 = dma8Channel
        _configuredDma16 = effectiveDma16InBed
        _irq = defaultIrq
        _dma8 = dma8Channel
        _dma16 = effectiveDma16InBed
        _processExitHandler = AddressOf HandleProcessExit
        AddHandler AppDomain.CurrentDomain.ProcessExit, _processExitHandler
        ResetDevice()
    End Sub

    Public ReadOnly Property BasePort As UInt16
        Get
            Return _basePort
        End Get
    End Property

    Public ReadOnly Property Irq As Integer
        Get
            Return _irq
        End Get
    End Property

    Public ReadOnly Property Dma8Channel As Integer
        Get
            Return _dma8
        End Get
    End Property

    Public ReadOnly Property Dma16Channel As Integer
        Get
            Return _dma16
        End Get
    End Property

    Public ReadOnly Property MpuBasePort As UInt16
        Get
            Return _mpuBasePort
        End Get
    End Property

    Public ReadOnly Property GamePortEnabled As Boolean
        Get
            Return _gamePortEnabled
        End Get
    End Property

    Public ReadOnly Property WavetableHardwareSettings As WavetableAttachmentSettings
        Get
            Return _wavetableHardwareSettingsInBed.CloneSettings()
        End Get
    End Property

    Public ReadOnly Property WavetableInstalled As Boolean
        Get
            Return _wavetableHardwareSettingsInBed.Installed
        End Get
    End Property

    ' Chassis-side physical daughterboard configuration.  The caller applies this
    ' only while the machine is stopped/off as part of the existing staged ISA-card
    ' power-cycle path.  No SoundFont file is opened here.
    Public Sub ConfigureWavetableHardware(settingsInBed As WavetableAttachmentSettings)
        Dim appliedInBed As WavetableAttachmentSettings = If(settingsInBed, New WavetableAttachmentSettings()).CloneSettings()
        Dim profileInBed As WavetableProfile = WavetableProfileCatalog.FindById(appliedInBed.ProfileId)
        appliedInBed.ProfileId = profileInBed.Id
        _wavetableHardwareSettingsInBed = appliedInBed

        Dim oldHostInBed As WavetableHost = _wavetableHostInBed
        _wavetableHostInBed = Nothing
        _waveBlasterConnector.Detach()

        ' The host audio worker owns wavetable PCM consumption.  Swapping this
        ' reference waits only for host audio work; this configuration path runs
        ' while the chassis is stopped/off and never holds the running machine.
        _hostAudioInBed.SetWavetableHost(Nothing)
        If oldHostInBed IsNot Nothing Then oldHostInBed.Dispose()

        If appliedInBed.Installed Then
            ' Host bank parsing/render-state work starts behind a bounded queue.
            ' ConfigureWavetableHardware itself performs no SoundFont file I/O.
            Dim hostInBed As New WavetableHost(appliedInBed)
            _wavetableHostInBed = hostInBed
            _hostAudioInBed.SetWavetableHost(hostInBed)
            _waveBlasterConnector.Attach(profileInBed, hostInBed)
        End If
    End Sub

    ' Host-side jumpers/DIP switches.  This must only be called while the
    ' virtual chassis is stopped/off; guest mixer writes may subsequently
    ' change the active IRQ/DMA values until the next hardware reset.
    Public Sub ConfigureHardware(basePort As UInt16,
                                 irq As Integer,
                                 dma8Channel As Integer,
                                 dma16Channel As Integer,
                                 mpuBasePort As UInt16,
                                 gamePortEnabled As Boolean)
        If Array.IndexOf(IsaExpansionCardConfiguration.Sb16BasePorts, basePort) < 0 Then Throw New ArgumentOutOfRangeException(NameOf(basePort))
        If Array.IndexOf(IsaExpansionCardConfiguration.Sb16Irqs, irq) < 0 Then Throw New ArgumentOutOfRangeException(NameOf(irq))
        If Array.IndexOf(IsaExpansionCardConfiguration.Sb16Dma8Channels, dma8Channel) < 0 Then Throw New ArgumentOutOfRangeException(NameOf(dma8Channel))
        If Array.IndexOf(IsaExpansionCardConfiguration.Sb16Dma16Channels, dma16Channel) < 0 Then Throw New ArgumentOutOfRangeException(NameOf(dma16Channel))
        If Array.IndexOf(IsaExpansionCardConfiguration.Sb16MpuPorts, mpuBasePort) < 0 Then Throw New ArgumentOutOfRangeException(NameOf(mpuBasePort))

        Dim effectiveDma16InBed As Integer =
            If(dma16Channel = IsaExpansionCardConfiguration.Sb16Dma16ViaLowChannel, dma8Channel, dma16Channel)

        If _irqLineAsserted Then DriveIrq(_irq, False)
        _irqLineAsserted = False
        _basePort = basePort
        _configuredIrq = irq
        _configuredDma8 = dma8Channel
        _configuredDma16 = effectiveDma16InBed
        _irq = irq
        _dma8 = dma8Channel
        _dma16 = effectiveDma16InBed
        _mpuBasePort = mpuBasePort
        _gamePortEnabled = gamePortEnabled
    End Sub

    Public Function HandlesPort(port As UInt16) As Boolean Implements IPortDevice.HandlesPort
        Dim offset As Integer = CInt(port) - CInt(_basePort)
        If offset >= 0 AndAlso offset <= &HF Then Return True
        If port >= &H388US AndAlso port <= &H38BUS Then Return True
        If port = _mpuBasePort OrElse port = CUShort(CInt(_mpuBasePort) + 1) Then Return True
        If _gamePortEnabled AndAlso port = &H201US Then Return True ' SB16 joystick/game-port connector
        Return False
    End Function

    Public Function PotentiallyHandlesPort(port As UInt16) As Boolean Implements IPortDecodeCandidateProvider.PotentiallyHandlesPort
        For Each baseInBed As UInt16 In IsaExpansionCardConfiguration.Sb16BasePorts
            Dim offsetInBed As Integer = CInt(port) - CInt(baseInBed)
            If offsetInBed >= 0 AndAlso offsetInBed <= &HF Then Return True
        Next
        If port >= &H388US AndAlso port <= &H38BUS Then Return True
        For Each mpuInBed As UInt16 In IsaExpansionCardConfiguration.Sb16MpuPorts
            If port = mpuInBed OrElse port = CUShort(CInt(mpuInBed) + 1) Then Return True
        Next
        If port = &H201US Then Return True
        Return False
    End Function

    Public Function ReadPort(port As UInt16) As Byte Implements IPortDevice.ReadPort
        If port >= &H388US AndAlso port <= &H38BUS Then Return ReadOplPort(port)
        If port = _mpuBasePort OrElse port = CUShort(CInt(_mpuBasePort) + 1) Then Return ReadMpuPort(port)
        If _gamePortEnabled AndAlso port = &H201US Then Return &HFF ' game port present, no joystick presently attached

        Dim offset As Integer = CInt(port) - CInt(_basePort)
        Select Case offset
            ' SB16 exposes the OPL3 at base+0..3 as well as the legacy
            ' base+8/+9 and AdLib 388h/389h aliases.  Even offsets are status/
            ' address ports on reads; data-port reads are undefined/open-bus.
            Case 0, 2
                Return _opl.ReadStatus()
            Case 1, 3
                Return &HFF
            Case 4
                ' CT1745 mixer index/address port is write-only.
                Return &HFF
            Case 5
                Return ReadMixerData()
            Case 8
                Return _opl.ReadStatus()
            Case 9
                Return &HFF
            Case &HA
                If _dspReadQueue.Count = 0 Then Return &HFF
                Return _dspReadQueue.Dequeue()
            Case &HC
                ' During DSP reset/initialization the command interface is not ready.
                ' Once initialized, this DSP 4.13 model can accept the next command byte.
                Return If(_dspResetAsserted OrElse _dspInitializationPicosecondsRemaining > 0,
                          CByte(&H80),
                          CByte(0))
            Case &HE
                AcknowledgeDspIrq(False)
                Return If(_dspReadQueue.Count > 0, CByte(&H80), CByte(0))
            Case &HF
                AcknowledgeDspIrq(True)
                Return &HFF
            Case Else
                Return &HFF
        End Select
    End Function

    Public Sub WritePort(port As UInt16, value As Byte) Implements IPortDevice.WritePort
        If port >= &H388US AndAlso port <= &H38BUS Then
            WriteOplPort(port, value)
            Return
        End If
        If port = _mpuBasePort OrElse port = CUShort(CInt(_mpuBasePort) + 1) Then
            WriteMpuPort(port, value)
            Return
        End If
        If _gamePortEnabled AndAlso port = &H201US Then
            ' A write charges the game-port RC timing network.  With no joystick
            ' connected all four axis inputs remain open/high, so no host input
            ' state is synthesized here.
            Return
        End If

        Dim offset As Integer = CInt(port) - CInt(_basePort)
        Select Case offset
            Case 0
                _opl.WriteAddress(0, value)
                _hostOplAddress(0) = value
            Case 1
                FlushHostAudioTimelineInBed()
                _opl.WriteData(0, value)
                _hostAudioInBed.PostOplRegister(0, _hostOplAddress(0), value)
                _hostAudioInBed.NotifyPotentialAudioActivity()
            Case 2
                _opl.WriteAddress(1, value)
                _hostOplAddress(1) = value
            Case 3
                FlushHostAudioTimelineInBed()
                _opl.WriteData(1, value)
                _hostAudioInBed.PostOplRegister(1, _hostOplAddress(1), value)
                _hostAudioInBed.NotifyPotentialAudioActivity()
            Case 4
                _mixerIndex = value
            Case 5
                WriteMixerData(value)
            Case 6
                WriteDspReset(value)
            Case 8
                _opl.WriteAddress(0, value)
                _hostOplAddress(0) = value
            Case 9
                FlushHostAudioTimelineInBed()
                _opl.WriteData(0, value)
                _hostAudioInBed.PostOplRegister(0, _hostOplAddress(0), value)
                _hostAudioInBed.NotifyPotentialAudioActivity()
            Case &HC
                WriteDspByte(value)
        End Select
    End Sub

    Private Function ReadOplPort(port As UInt16) As Byte
        Select Case port
            Case &H388US, &H38AUS
                Return _opl.ReadStatus()
            Case Else
                Return &HFF
        End Select
    End Function

    Private Sub WriteOplPort(port As UInt16, value As Byte)
        Select Case port
            Case &H388US
                _opl.WriteAddress(0, value)
                _hostOplAddress(0) = value
            Case &H389US
                FlushHostAudioTimelineInBed()
                _opl.WriteData(0, value)
                _hostAudioInBed.PostOplRegister(0, _hostOplAddress(0), value)
                _hostAudioInBed.NotifyPotentialAudioActivity()
            Case &H38AUS
                _opl.WriteAddress(1, value)
                _hostOplAddress(1) = value
            Case &H38BUS
                FlushHostAudioTimelineInBed()
                _opl.WriteData(1, value)
                _hostAudioInBed.PostOplRegister(1, _hostOplAddress(1), value)
                _hostAudioInBed.NotifyPotentialAudioActivity()
        End Select
    End Sub

    Private Function ReadMpuPort(port As UInt16) As Byte
        If port = CUShort(CInt(_mpuBasePort) + 1) Then
            ' bit 6 = output not ready, bit 7 = input not ready.
            Return If(_mpuReadQueue.Count = 0, CByte(&H80), CByte(0))
        End If

        ' Creative documents the MPU data-port read itself as the interrupt
        ' acknowledge operation.  This is independent of the DSP 8/16-bit ACK
        ' ports and is reported separately in mixer register 82h bit 2.
        Dim result As Byte = If(_mpuReadQueue.Count = 0, CByte(&HFF), _mpuReadQueue.Dequeue())
        AcknowledgeMpuIrq()
        Return result
    End Function

    Private Sub WriteMpuPort(port As UInt16, value As Byte)
        If port = CUShort(CInt(_mpuBasePort) + 1) Then
            If _mpuUartMode Then
                ' Once UART mode is active the only recognized command is Reset.
                If value = &HFF Then
                    _mpuUartMode = False
                    _mpuReadQueue.Clear()
                    AcknowledgeMpuIrq()
                    _mpuReadQueue.Enqueue(&HFE)
                End If
                Return
            End If

            Select Case value
                Case &HFF ' reset
                    _mpuUartMode = False
                    _mpuReadQueue.Clear()
                    AcknowledgeMpuIrq()
                    _mpuReadQueue.Enqueue(&HFE)

                Case &H3F ' enter UART mode
                    _mpuReadQueue.Clear()
                    _mpuUartMode = True
                    _mpuReadQueue.Enqueue(&HFE)
                    ' SB16 generates an IRQ when the UART-mode transition succeeds.
                    RaiseMpuIrq()

                Case Else
                    ' Only Reset and Enter-UART are implemented by the SB16 MPU
                    ' interface.  Unknown command bytes are ignored rather than
                    ' manufacturing a successful acknowledgement.
            End Select
        ElseIf _mpuUartMode Then
            ' The guest byte has reached the emulated MPU transmitter.  Feed the
            ' internal Wave Blaster connector from this exact hardware seam; no
            ' direct host/UI MIDI injection is permitted.
            TransmitGuestMidiByte(value)
        End If
    End Sub

    Private Sub CaptureSbMidiDspParameters()
        _sbMidiSavedSpeakerEnabled = _speakerEnabled
        _sbMidiSavedSampleRate = _sampleRate
        _sbMidiSavedOutputSampleRate = _outputSampleRate
        _sbMidiSavedInputSampleRate = _inputSampleRate
        _sbMidiSavedLegacyRequestedSampleRate = _legacyRequestedSampleRate
        _sbMidiSavedLegacyBlockUnits = _legacyBlockUnits
        _sbMidiSavedTestRegister = _testRegister
    End Sub

    Private Sub RestoreSbMidiDspParameters()
        _speakerEnabled = _sbMidiSavedSpeakerEnabled
        _sampleRate = _sbMidiSavedSampleRate
        _outputSampleRate = _sbMidiSavedOutputSampleRate
        _inputSampleRate = _sbMidiSavedInputSampleRate
        _legacyRequestedSampleRate = Math.Max(1, _sbMidiSavedLegacyRequestedSampleRate)
        _legacyBlockUnits = Math.Max(1, _sbMidiSavedLegacyBlockUnits)
        _testRegister = _sbMidiSavedTestRegister
    End Sub

    Private Sub TransmitGuestMidiByte(value As Byte)
        ' Preserve the same audio-time ordering used by the real connector: render
        ' everything elapsed before this UART byte before the wavetable worker sees it.
        FlushHostAudioTimelineInBed()
        ' Both Creative MIDI programming paths ultimately drive the card's MIDI
        ' transmitter.  Feed the Wave Blaster connector only from this guest-visible
        ' hardware seam; the host synth is never injected directly.
        _waveBlasterConnector.TransmitMidiByte(value)
        If _wavetableHostInBed IsNot Nothing Then
            _hostAudioInBed.NotifyPotentialAudioActivity()
        End If
    End Sub

    Private Sub WriteDspReset(value As Byte)
        Dim asserted As Boolean = (value And 1) <> 0
        If asserted Then
            _sbMidiRestoreAfterReset = _sbMidiUartMode
            _dspResetAsserted = True
            _dspResetAssertedPicoseconds = 0
            _dspInitializationPicosecondsRemaining = 0

            ' The DSP reset pin resets the DSP itself, not the CT1745 mixer or MPU.
            ' Creative documents that reset terminates pending digitized I/O and
            ' leaves the speaker-status latch disabled. Preserve only an unrelated
            ' MPU interrupt which may already be pending on the shared ISA IRQ.
            StopPlayback()
            _adpcmReference = &H80
            _adpcmScale = 0
            _speakerEnabled = False
            _sbMidiInputPolling = False
            _sbMidiInputInterrupt = False
            _sbMidiUartMode = False
            _sbMidiUartInterrupt = False
            _sbMidiTimestamp = False
            _sampleRate = 22050
            _outputSampleRate = 22050
            _inputSampleRate = 22050
            _legacyRequestedSampleRate = 22050
            _legacyBlockUnits = 1
            _silenceUnitsRemaining = 0
            _silenceClockNumerator = 0
            _testRegister = 0
            _currentLeft = 0
            _currentRight = 0
            _pendingIrqBits = CByte(_pendingIrqBits And &H4)
            _mixer(&H82) = _pendingIrqBits
            UpdateIrqLine()
            _hostAudioInBed.NotifyPotentialAudioActivity()

            _dspReadQueue.Clear()
            _pendingCommand = -1
            _pendingParamCount = 0
            _commandParams.Clear()
            Return
        End If

        If _dspResetAsserted Then
            Dim pulseWasLongEnough As Boolean =
                _dspResetAssertedPicoseconds >= DspMinimumResetPulsePicoseconds
            _dspResetAsserted = False
            _dspReadQueue.Clear()
            _pendingCommand = -1
            _pendingParamCount = 0
            _commandParams.Clear()

            ' Creative specifies at least a 3 us reset pulse and roughly 100 us for
            ' DSP initialization.  A too-short pulse does not synthesize a successful
            ' AAh response.
            If pulseWasLongEnough Then
                If _sbMidiRestoreAfterReset Then RestoreSbMidiDspParameters()
                _sbMidiRestoreAfterReset = False
                _dspInitializationPicosecondsRemaining = DspInitializationPicoseconds
            Else
                _sbMidiRestoreAfterReset = False
                _dspInitializationPicosecondsRemaining = 0
            End If
        End If
    End Sub

    Private Sub WriteDspByte(value As Byte)
        ' The DSP write port remains busy while reset/initialization is active.
        ' Software which violates that status must not smuggle commands into the
        ' command parser before the AAh reset-complete response.
        If _dspResetAsserted OrElse _dspInitializationPicosecondsRemaining > 0 Then Return

        ' Creative DSP commands 34h..37h place the legacy SB-MIDI interface in
        ' UART mode.  While active, command decoding is bypassed: every byte
        ' written here is an outbound MIDI byte.  Only a hardware DSP reset
        ' exits this mode.
        If _sbMidiUartMode Then
            TransmitGuestMidiByte(value)
            Return
        End If

        If _pendingCommand >= 0 Then
            _commandParams.Add(value)
            If _commandParams.Count >= _pendingParamCount Then
                Dim command As Byte = CByte(_pendingCommand)
                _pendingCommand = -1
                ExecuteDspCommand(command, _commandParams.ToArray())
                _commandParams.Clear()
                _pendingParamCount = 0
            End If
            Return
        End If

        Dim parameterCount As Integer = DspParameterCount(value)
        If parameterCount = 0 Then
            ExecuteDspCommand(value, Array.Empty(Of Byte)())
        Else
            _pendingCommand = value
            _pendingParamCount = parameterCount
            _commandParams.Clear()
        End If
    End Sub

    Private Shared Function DspParameterCount(command As Byte) As Integer
        If (command And &HF0) = &HB0 OrElse (command And &HF0) = &HC0 Then Return 3
        Select Case command
            ' One-byte parameter commands known to the SB/SB16 DSP family.
            Case &H4, &H8, &HF, &H10, &H38, &H40, &HE0, &HE2, &HE4, &HF9
                Return 1

            ' Two-byte length/register commands, including the single-cycle
            ' Creative ADPCM forms whose length is expressed in DMA bytes minus one.
            Case &H5, &HE, &H14, &H15, &H16, &H17, &H24, &H41, &H42, &H48,
                 &H74, &H75, &H76, &H77, &H80, &HFA
                Return 2

            Case Else
                Return 0
        End Select
    End Function

    Private Sub ExecuteDspCommand(command As Byte, parameters As Byte())
        If (command And &HF0) = &HB0 OrElse (command And &HF0) = &HC0 Then
            ExecuteModernPlayback(command, parameters)
            Return
        End If

        Select Case command
            Case &H8 ' SB16 ASP query; this card has no CT1748 ASP fitted
                If parameters.Length >= 1 AndAlso parameters(0) = &H3 Then
                    ' Real SB16-family cards without an ASP return FFh to the
                    ' get-chip-version subcommand rather than pretending one exists.
                    _dspReadQueue.Enqueue(&HFF)
                End If

            Case &H10 ' direct 8-bit DAC
                If parameters.Length >= 1 Then
                    FlushHostAudioTimelineInBed()
                    Dim sample As Short = CShort((CInt(parameters(0)) - 128) << 8)
                    _currentLeft = sample : _currentRight = sample
                    ' On DSP 4.xx the D1h/D3h speaker flag is status-only.
                    _hostAudioInBed.NotifyPotentialAudioActivity()
                End If
            Case &H14 ' 8-bit single-cycle DMA output
                Dim legacyOutputUnits As Integer = CInt(parameters(0)) Or (CInt(parameters(1)) << 8)
                StartPlayback(bits16:=False, stereo:=False, signedData:=False, autoInit:=False, units:=legacyOutputUnits + 1)
            Case &H16, &H17 ' 8-bit -> 2-bit Creative ADPCM single-cycle output
                Dim adpcm2Units As Integer = CInt(parameters(0)) Or (CInt(parameters(1)) << 8)
                StartAdpcmPlayback(Adpcm2Bit, autoInit:=False, units:=adpcm2Units + 1,
                                   referenceByte:=(command = &H17))
            Case &H1C ' 8-bit auto-init DMA output using programmed block size
                StartPlayback(bits16:=False, stereo:=False, signedData:=False, autoInit:=True, units:=Math.Max(1, _legacyBlockUnits))
            Case &H1F ' 8-bit -> 2-bit Creative ADPCM auto-init, first block has reference
                StartAdpcmPlayback(Adpcm2Bit, autoInit:=True, units:=Math.Max(1, _legacyBlockUnits),
                                   referenceByte:=True)
            Case &H20 ' direct 8-bit ADC input; no external source = unsigned silence
                _dspReadQueue.Enqueue(&H80)
            Case &H24 ' 8-bit single-cycle DMA input
                Dim legacyInputUnits As Integer = CInt(parameters(0)) Or (CInt(parameters(1)) << 8)
                StartPlayback(bits16:=False, stereo:=False, signedData:=False, autoInit:=False,
                              units:=legacyInputUnits + 1, inputTransfer:=True)
            Case &H2C ' 8-bit auto-init DMA input using programmed block size
                StartPlayback(bits16:=False, stereo:=False, signedData:=False, autoInit:=True,
                              units:=Math.Max(1, _legacyBlockUnits), inputTransfer:=True)

            Case &H30 ' SB-MIDI polling input
                _sbMidiInputPolling = True
                _sbMidiInputInterrupt = False
                _sbMidiUartMode = False
                _sbMidiUartInterrupt = False
                _sbMidiTimestamp = False

            Case &H31 ' SB-MIDI interrupt input; a second 31h terminates it
                _sbMidiInputInterrupt = Not _sbMidiInputInterrupt
                _sbMidiInputPolling = False
                _sbMidiUartMode = False
                _sbMidiUartInterrupt = False
                _sbMidiTimestamp = False

            Case &H34, &H35, &H36, &H37 ' SB-MIDI UART I/O modes
                CaptureSbMidiDspParameters()
                _sbMidiInputPolling = False
                _sbMidiInputInterrupt = False
                _sbMidiUartMode = True
                _sbMidiUartInterrupt = (command = &H35 OrElse command = &H37)
                _sbMidiTimestamp = (command = &H36 OrElse command = &H37)

            Case &H38 ' non-UART MIDI output; one data byte follows command
                If parameters.Length >= 1 Then TransmitGuestMidiByte(parameters(0))

            Case &H40 ' legacy transfer time constant applies to digitized I/O
                Dim denominator As Integer = 256 - CInt(parameters(0))
                If denominator <= 0 Then denominator = 1
                SetLegacySampleRate(CInt(Math.Round(1000000.0 / denominator)))
            Case &H41 ' output sample rate, high byte then low byte
                SetOutputSampleRate((CInt(parameters(0)) << 8) Or parameters(1))
            Case &H42 ' input sample rate, high byte then low byte
                SetInputSampleRate((CInt(parameters(0)) << 8) Or parameters(1))
            Case &H48 ' legacy auto-init block size
                _legacyBlockUnits = (CInt(parameters(0)) Or (CInt(parameters(1)) << 8)) + 1
            Case &H74, &H75 ' 8-bit -> 4-bit Creative ADPCM single-cycle output
                Dim adpcm4Units As Integer = CInt(parameters(0)) Or (CInt(parameters(1)) << 8)
                StartAdpcmPlayback(Adpcm4Bit, autoInit:=False, units:=adpcm4Units + 1,
                                   referenceByte:=(command = &H75))
            Case &H76, &H77 ' 8-bit -> 3-bit (2.6-bit packed) Creative ADPCM single-cycle output
                Dim adpcm3Units As Integer = CInt(parameters(0)) Or (CInt(parameters(1)) << 8)
                StartAdpcmPlayback(Adpcm3Bit, autoInit:=False, units:=adpcm3Units + 1,
                                   referenceByte:=(command = &H77))
            Case &H7D ' 8-bit -> 4-bit Creative ADPCM auto-init, first block has reference
                StartAdpcmPlayback(Adpcm4Bit, autoInit:=True, units:=Math.Max(1, _legacyBlockUnits),
                                   referenceByte:=True)
            Case &H7F ' 8-bit -> 3-bit Creative ADPCM auto-init, first block has reference
                StartAdpcmPlayback(Adpcm3Bit, autoInit:=True, units:=Math.Max(1, _legacyBlockUnits),
                                   referenceByte:=True)
            Case &H80 ' pause DAC for a duration, then raise an 8-bit DSP IRQ
                Dim durationMinusOne As Integer = CInt(parameters(0)) Or (CInt(parameters(1)) << 8)
                FlushHostAudioTimelineInBed()
                _silenceUnitsRemaining = durationMinusOne + 1
                _silenceClockNumerator = 0
                _currentLeft = 0 : _currentRight = 0
                _hostAudioInBed.NotifyPotentialAudioActivity()
            Case &HD0
                If Not _playback16Bit Then _playbackPaused = True
            Case &HD1
                ' DSP 4.xx: update only the internal flag returned by D8h.
                _speakerEnabled = True
            Case &HD3
                ' DSP 4.xx: update only the internal flag returned by D8h.
                _speakerEnabled = False
            Case &HD4
                If Not _playback16Bit Then _playbackPaused = False
            Case &HD5
                If _playback16Bit Then _playbackPaused = True
            Case &HD6
                If _playback16Bit Then _playbackPaused = False
            Case &HD8
                _dspReadQueue.Enqueue(If(_speakerEnabled, CByte(&HFF), CByte(0)))
            Case &HD9
                If _playback16Bit Then _exitAutoInitAfterBlock = True
            Case &HDA
                If Not _playback16Bit Then _exitAutoInitAfterBlock = True
            Case &HE0
                _dspReadQueue.Enqueue(CByte(Not parameters(0)))
            Case &HE1
                _dspReadQueue.Enqueue(4)   ' DSP 4.13: a common early SB16 revision
                _dspReadQueue.Enqueue(13)
            Case &HE3
                Dim text() As Byte = System.Text.Encoding.ASCII.GetBytes("COPYRIGHT (C) CREATIVE TECHNOLOGY LTD, 1992." & ChrW(0))
                For Each b As Byte In text : _dspReadQueue.Enqueue(b) : Next
            Case &HE4
                _testRegister = parameters(0)
            Case &HE8
                _dspReadQueue.Enqueue(_testRegister)
            Case &HF2
                RaiseDspIrq(False)
            Case &HF3
                RaiseDspIrq(True)
        End Select
    End Sub

    Private Sub ExecuteModernPlayback(command As Byte, parameters As Byte())
        If parameters.Length < 3 Then Return
        Dim bits16 As Boolean = (command And &HF0) = &HB0
        Dim isInput As Boolean = (command And &H8) <> 0
        Dim autoInit As Boolean = (command And &H4) <> 0
        Dim mode As Byte = parameters(0)
        Dim stereo As Boolean = (mode And &H20) <> 0
        Dim signedData As Boolean = (mode And &H10) <> 0
        Dim units As Integer = (CInt(parameters(1)) Or (CInt(parameters(2)) << 8)) + 1
        StartPlayback(bits16, stereo, signedData, autoInit, units, inputTransfer:=isInput)
    End Sub

    Private Sub StartPlayback(bits16 As Boolean,
                              stereo As Boolean,
                              signedData As Boolean,
                              autoInit As Boolean,
                              units As Integer,
                              Optional inputTransfer As Boolean = False)
        If units < 1 Then units = 1

        If _playbackActive AndAlso _playbackAutoInit AndAlso Not autoInit Then
            StagePendingTransfer(bits16, stereo, signedData, inputTransfer, units,
                                 AdpcmNone, False)
            Return
        End If

        BeginPlaybackNow(bits16, stereo, signedData, autoInit, units, inputTransfer)
    End Sub

    Private Sub BeginPlaybackNow(bits16 As Boolean,
                                 stereo As Boolean,
                                 signedData As Boolean,
                                 autoInit As Boolean,
                                 units As Integer,
                                 inputTransfer As Boolean)
        If units < 1 Then units = 1
        _playbackAdpcmMode = AdpcmNone
        _adpcmDecodedSamples.Clear()
        _adpcmBlockPendingCompletion = False
        _playbackInput = inputTransfer
        _playback16Bit = bits16
        _playbackStereo = stereo
        _playbackSigned = signedData
        _playbackAutoInit = autoInit
        _playbackBlockUnits = units
        _playbackUnitsRemaining = units
        _exitAutoInitAfterBlock = False
        _playbackPaused = False
        _playbackActive = True
        _sampleRate = If(_playbackInput, _inputSampleRate, _outputSampleRate)
        _pcmClockNumerator = 0
    End Sub

    Private Sub StartAdpcmPlayback(adpcmMode As Integer,
                                   autoInit As Boolean,
                                   units As Integer,
                                   referenceByte As Boolean)
        If adpcmMode <> Adpcm2Bit AndAlso adpcmMode <> Adpcm3Bit AndAlso adpcmMode <> Adpcm4Bit Then
            Throw New ArgumentOutOfRangeException(NameOf(adpcmMode))
        End If
        If units < 1 Then units = 1

        If _playbackActive AndAlso _playbackAutoInit AndAlso Not autoInit Then
            StagePendingTransfer(False, False, False, False, units,
                                 adpcmMode, referenceByte)
            Return
        End If

        BeginAdpcmPlaybackNow(adpcmMode, autoInit, units, referenceByte)
    End Sub

    Private Sub BeginAdpcmPlaybackNow(adpcmMode As Integer,
                                      autoInit As Boolean,
                                      units As Integer,
                                      referenceByte As Boolean)
        _playbackInput = False
        _playback16Bit = False
        _playbackStereo = False
        _playbackSigned = False
        _playbackAutoInit = autoInit
        _playbackBlockUnits = Math.Max(1, units)
        _playbackUnitsRemaining = _playbackBlockUnits
        _exitAutoInitAfterBlock = False
        _playbackPaused = False
        _playbackActive = True
        _playbackAdpcmMode = adpcmMode
        _adpcmNeedsReference = referenceByte
        _adpcmBlockPendingCompletion = False
        _adpcmDecodedSamples.Clear()
        If referenceByte Then
            _adpcmReference = &H80
            _adpcmScale = 0
        End If
        _sampleRate = ClampAdpcmSampleRate(_legacyRequestedSampleRate, adpcmMode)
        _pcmClockNumerator = 0
    End Sub

    Private Sub StagePendingTransfer(bits16 As Boolean,
                                     stereo As Boolean,
                                     signedData As Boolean,
                                     inputTransfer As Boolean,
                                     units As Integer,
                                     adpcmMode As Integer,
                                     referenceByte As Boolean)
        _pendingTransferValid = True
        _pendingTransferBits16 = bits16
        _pendingTransferStereo = stereo
        _pendingTransferSigned = signedData
        _pendingTransferInput = inputTransfer
        _pendingTransferUnits = Math.Max(1, units)
        _pendingTransferAdpcmMode = adpcmMode
        _pendingTransferAdpcmReference = referenceByte
    End Sub

    Private Sub ActivatePendingTransfer()
        If Not _pendingTransferValid Then Return

        Dim bits16 As Boolean = _pendingTransferBits16
        Dim stereo As Boolean = _pendingTransferStereo
        Dim signedData As Boolean = _pendingTransferSigned
        Dim inputTransfer As Boolean = _pendingTransferInput
        Dim units As Integer = _pendingTransferUnits
        Dim adpcmMode As Integer = _pendingTransferAdpcmMode
        Dim referenceByte As Boolean = _pendingTransferAdpcmReference

        ClearPendingTransfer()

        If adpcmMode = AdpcmNone Then
            BeginPlaybackNow(bits16, stereo, signedData, False, units, inputTransfer)
        Else
            BeginAdpcmPlaybackNow(adpcmMode, False, units, referenceByte)
        End If
    End Sub

    Private Sub ClearPendingTransfer()
        _pendingTransferValid = False
        _pendingTransferBits16 = False
        _pendingTransferStereo = False
        _pendingTransferSigned = False
        _pendingTransferInput = False
        _pendingTransferUnits = 0
        _pendingTransferAdpcmMode = AdpcmNone
        _pendingTransferAdpcmReference = False
    End Sub

    Private Sub StopPlayback()
        If _dma IsNot Nothing Then
            Try : _dma.SetDreq(_dma8, False) : Catch : End Try
            Try : _dma.SetDreq(_dma16, False) : Catch : End Try
        End If
        _playbackActive = False
        _playbackPaused = False
        _playbackInput = False
        _playbackUnitsRemaining = 0
        _playbackAdpcmMode = AdpcmNone
        _adpcmNeedsReference = False
        _adpcmBlockPendingCompletion = False
        _adpcmDecodedSamples.Clear()
        ClearPendingTransfer()
        _pcmClockNumerator = 0
        _currentLeft = 0
        _currentRight = 0
    End Sub

    Private Shared Function ClampDsp4SampleRate(value As Integer) As Integer
        If value < 5000 Then Return 5000
        If value > 44100 Then Return 44100
        Return value
    End Function

    Private Shared Function ClampAdpcmSampleRate(value As Integer, adpcmMode As Integer) As Integer
        Dim maximum As Integer
        Select Case adpcmMode
            Case Adpcm4Bit : maximum = 12000
            Case Adpcm3Bit : maximum = 13000
            Case Adpcm2Bit : maximum = 11000
            Case Else : maximum = 44100
        End Select
        If value < 4000 Then Return 4000
        If value > maximum Then Return maximum
        Return value
    End Function

    Private Sub SetLegacySampleRate(value As Integer)
        _legacyRequestedSampleRate = Math.Max(1, value)
        value = ClampDsp4SampleRate(value)
        _outputSampleRate = value
        _inputSampleRate = value
        If Not _playbackActive Then _sampleRate = value
    End Sub

    Private Sub SetOutputSampleRate(value As Integer)
        _outputSampleRate = ClampDsp4SampleRate(value)
        If Not _playbackActive OrElse Not _playbackInput Then _sampleRate = _outputSampleRate
    End Sub

    Private Sub SetInputSampleRate(value As Integer)
        _inputSampleRate = ClampDsp4SampleRate(value)
        If Not _playbackActive OrElse _playbackInput Then _sampleRate = _inputSampleRate
    End Sub

    Private Function ConsumePcmFrame() As Boolean
        If Not _playbackActive OrElse _playbackPaused Then Return False
        If _playbackAdpcmMode <> AdpcmNone Then Return ConsumeAdpcmFrame()

        Dim bytesPerSample As Integer = If(_playback16Bit, 2, 1)
        Dim channels As Integer = If(_playbackStereo, 2, 1)
        Dim bytesNeeded As Integer = bytesPerSample * channels
        Dim buffer(bytesNeeded - 1) As Byte

        ' DSP 4.xx can move 16-bit samples over the selected 8-bit DMA channel
        ' when mixer register 81h has no high-DMA bit selected.
        Dim dmaChannel As Integer =
            If(_playback16Bit AndAlso _dma16 >= 5 AndAlso _dma16 <= 7,
               _dma16,
               _dma8)

        If _playbackInput Then
            FillAdcSilenceBuffer(buffer, _playback16Bit, _playbackSigned)
        End If

        _dma.SetDreq(dmaChannel, True)
        Dim moved As Integer
        Try
            If _playbackInput Then
                moved = _dma.TransferToMemory(dmaChannel, buffer, 0, bytesNeeded)
            Else
                moved = _dma.TransferFromMemory(dmaChannel, buffer, 0, bytesNeeded)
            End If
        Finally
            _dma.SetDreq(dmaChannel, False)
        End Try

        If moved < bytesNeeded Then
            _dspUnderruns += 1UL
            Return False
        End If

        _dmaBytesPlayed += CULng(moved)
        Dim dmaUnits As Integer = If(_playback16Bit, moved \ 2, moved)
        _playbackUnitsRemaining -= dmaUnits

        Dim hostAcceptedInBed As Boolean
        If _playbackInput Then
            ' No analog source is attached to this virtual card yet.  The ADC still
            ' performs the real DMA cycles and completion IRQs, writing correctly
            ' biased silence into guest memory.
            hostAcceptedInBed = False
        Else
            If _playback16Bit Then
                _currentLeft = Decode16(buffer, 0, _playbackSigned)
                If _playbackStereo Then
                    _currentRight = Decode16(buffer, 2, _playbackSigned)
                Else
                    _currentRight = _currentLeft
                End If
            Else
                _currentLeft = Decode8(buffer(0), _playbackSigned)
                If _playbackStereo Then
                    _currentRight = Decode8(buffer(1), _playbackSigned)
                Else
                    _currentRight = _currentLeft
                End If
            End If

            ' DSP 4.xx D1h/D3h do not gate digitized output; they only update
            ' the speaker-status latch returned by D8h.
            hostAcceptedInBed =
                _hostAudioInBed.TryPostPcmSourceFrame(_currentLeft, _currentRight)
        End If

        If _playbackUnitsRemaining <= 0 Then
            CompletePlaybackBlock()
        End If
        Return hostAcceptedInBed
    End Function

    Private Function ConsumeAdpcmFrame() As Boolean
        If Not _playbackActive OrElse _playbackPaused OrElse _playbackAdpcmMode = AdpcmNone Then Return False

        ' One emulated DAC sample is produced per playback clock.  Pull a new
        ' compressed DMA byte only when all codes from the previous byte have
        ' been consumed.
        While _adpcmDecodedSamples.Count = 0 AndAlso _playbackActive
            If _adpcmBlockPendingCompletion Then
                _adpcmBlockPendingCompletion = False
                CompletePlaybackBlock()
                If Not _playbackActive OrElse _playbackAdpcmMode = AdpcmNone Then Return False
                Continue While
            End If

            If _playbackUnitsRemaining <= 0 Then
                CompletePlaybackBlock()
                If Not _playbackActive OrElse _playbackAdpcmMode = AdpcmNone Then Return False
                Continue While
            End If

            Dim compressed(0) As Byte
            _dma.SetDreq(_dma8, True)
            Dim moved As Integer
            Try
                moved = _dma.TransferFromMemory(_dma8, compressed, 0, 1)
            Finally
                _dma.SetDreq(_dma8, False)
            End Try

            If moved < 1 Then
                _dspUnderruns += 1UL
                Return False
            End If

            _dmaBytesPlayed += 1UL
            _playbackUnitsRemaining -= 1

            If _adpcmNeedsReference Then
                _adpcmReference = compressed(0)
                _adpcmScale = 0
                _adpcmNeedsReference = False
            Else
                QueueDecodedAdpcmSamples(compressed(0))
            End If

            If _playbackUnitsRemaining <= 0 Then
                If _adpcmDecodedSamples.Count = 0 Then
                    CompletePlaybackBlock()
                Else
                    ' Preserve the final decompressed samples before signaling the
                    ' block boundary / activating a staged next transfer.
                    _adpcmBlockPendingCompletion = True
                End If
            End If
        End While

        If _adpcmDecodedSamples.Count = 0 Then Return False

        Dim sample8 As Byte = _adpcmDecodedSamples.Dequeue()
        Dim sample As Short = Decode8(sample8, False)
        _currentLeft = sample
        _currentRight = sample
        Dim accepted As Boolean = _hostAudioInBed.TryPostPcmSourceFrame(sample, sample)

        If _adpcmDecodedSamples.Count = 0 AndAlso _adpcmBlockPendingCompletion Then
            _adpcmBlockPendingCompletion = False
            CompletePlaybackBlock()
        End If
        Return accepted
    End Function

    Private Sub CompletePlaybackBlock()
        Dim completed16Bit As Boolean = _playback16Bit
        RaiseDspIrq(completed16Bit)

        If _pendingTransferValid Then
            ActivatePendingTransfer()
            Return
        End If

        If _playbackAutoInit AndAlso Not _exitAutoInitAfterBlock Then
            _playbackUnitsRemaining = Math.Max(1, _playbackBlockUnits)
            ' The Creative auto-init ADPCM commands seed the first block only.
            ' Predictor/reference state then carries across subsequent blocks.
            Return
        End If

        _playbackActive = False
        _playbackUnitsRemaining = 0
        _playbackAdpcmMode = AdpcmNone
        _adpcmBlockPendingCompletion = False
        _adpcmDecodedSamples.Clear()
    End Sub

    Private Sub QueueDecodedAdpcmSamples(packed As Byte)
        Select Case _playbackAdpcmMode
            Case Adpcm4Bit
                _adpcmDecodedSamples.Enqueue(DecodeCreativeAdpcm4((packed >> 4) And &HF))
                _adpcmDecodedSamples.Enqueue(DecodeCreativeAdpcm4(packed And &HF))
            Case Adpcm3Bit
                _adpcmDecodedSamples.Enqueue(DecodeCreativeAdpcm3((packed >> 5) And &H7))
                _adpcmDecodedSamples.Enqueue(DecodeCreativeAdpcm3((packed >> 2) And &H7))
                _adpcmDecodedSamples.Enqueue(DecodeCreativeAdpcm3((packed And &H3) << 1))
            Case Adpcm2Bit
                _adpcmDecodedSamples.Enqueue(DecodeCreativeAdpcm2((packed >> 6) And &H3))
                _adpcmDecodedSamples.Enqueue(DecodeCreativeAdpcm2((packed >> 4) And &H3))
                _adpcmDecodedSamples.Enqueue(DecodeCreativeAdpcm2((packed >> 2) And &H3))
                _adpcmDecodedSamples.Enqueue(DecodeCreativeAdpcm2(packed And &H3))
        End Select
    End Sub

    Private Function DecodeCreativeAdpcm4(code As Integer) As Byte
        Dim magnitude As Integer = code And &H7
        Dim delta As Integer
        If _adpcmScale = 0 Then
            delta = magnitude
        Else
            delta = (2 * magnitude + 1) << (_adpcmScale - 1)
        End If
        If (code And &H8) <> 0 Then delta = -delta

        If magnitude = 0 AndAlso _adpcmScale > 0 Then
            _adpcmScale -= 1
        ElseIf magnitude >= 5 AndAlso _adpcmScale < 3 Then
            _adpcmScale += 1
        End If
        Return ApplyCreativeAdpcmDelta(delta)
    End Function

    Private Function DecodeCreativeAdpcm3(code As Integer) As Byte
        Dim magnitude As Integer = code And &H3
        Dim delta As Integer
        If _adpcmScale = 0 Then
            delta = magnitude
        ElseIf _adpcmScale < 4 Then
            delta = (2 * magnitude + 1) << (_adpcmScale - 1)
        Else
            ' The highest 2.6-bit state is the documented-family decoder's
            ' saturated 5/15/25/35 delta tier rather than another power of two.
            delta = 5 * (2 * magnitude + 1)
        End If
        If (code And &H4) <> 0 Then delta = -delta

        If magnitude = 0 AndAlso _adpcmScale > 0 Then
            _adpcmScale -= 1
        ElseIf magnitude = 3 AndAlso _adpcmScale < 4 Then
            _adpcmScale += 1
        End If
        Return ApplyCreativeAdpcmDelta(delta)
    End Function

    Private Function DecodeCreativeAdpcm2(code As Integer) As Byte
        Dim magnitude As Integer = code And &H1
        Dim delta As Integer
        If _adpcmScale = 0 Then
            delta = magnitude
        Else
            delta = (2 * magnitude + 1) << (_adpcmScale - 1)
        End If
        If (code And &H2) <> 0 Then delta = -delta

        If magnitude = 0 AndAlso _adpcmScale > 0 Then
            _adpcmScale -= 1
        ElseIf magnitude = 1 AndAlso _adpcmScale < 5 Then
            _adpcmScale += 1
        End If
        Return ApplyCreativeAdpcmDelta(delta)
    End Function

    Private Function ApplyCreativeAdpcmDelta(delta As Integer) As Byte
        _adpcmReference += delta
        If _adpcmReference < 0 Then _adpcmReference = 0
        If _adpcmReference > 255 Then _adpcmReference = 255
        Return CByte(_adpcmReference)
    End Function

    Private Shared Sub FillAdcSilenceBuffer(buffer As Byte(),
                                           bits16 As Boolean,
                                           signedData As Boolean)
        If buffer Is Nothing Then Throw New ArgumentNullException(NameOf(buffer))
        If bits16 Then
            For offset As Integer = 0 To buffer.Length - 1 Step 2
                If signedData Then
                    buffer(offset) = 0
                    If offset + 1 < buffer.Length Then buffer(offset + 1) = 0
                Else
                    buffer(offset) = 0
                    If offset + 1 < buffer.Length Then buffer(offset + 1) = &H80
                End If
            Next
        Else
            Dim silence As Byte = If(signedData, CByte(0), CByte(&H80))
            For i As Integer = 0 To buffer.Length - 1
                buffer(i) = silence
            Next
        End If
    End Sub

    Private Shared Function Decode8(value As Byte, signedData As Boolean) As Short
        If signedData Then
            Dim signed As Integer = If(value >= &H80, CInt(value) - 256, CInt(value))
            Return CShort(signed << 8)
        End If
        Return CShort((CInt(value) - 128) << 8)
    End Function

    Private Shared Function Decode16(buffer As Byte(), offset As Integer, signedData As Boolean) As Short
        Dim raw As Integer = CInt(buffer(offset)) Or (CInt(buffer(offset + 1)) << 8)
        If signedData Then
            If raw >= &H8000 Then raw -= &H10000
            Return CShort(raw)
        End If
        Return CShort(raw - &H8000)
    End Function

    Private Sub RaiseDspIrq(bits16 As Boolean)
        Dim bit As Byte = If(bits16, CByte(2), CByte(1))
        _pendingIrqBits = CByte(_pendingIrqBits Or bit)
        _mixer(&H82) = _pendingIrqBits
        UpdateIrqLine()
    End Sub

    Private Sub AcknowledgeDspIrq(bits16 As Boolean)
        Dim bit As Byte = If(bits16, CByte(2), CByte(1))
        _pendingIrqBits = CByte(_pendingIrqBits And Not bit)
        _mixer(&H82) = _pendingIrqBits
        UpdateIrqLine()
    End Sub

    Private Sub RaiseMpuIrq()
        _pendingIrqBits = CByte(_pendingIrqBits Or &H4)
        _mixer(&H82) = _pendingIrqBits
        UpdateIrqLine()
    End Sub

    Private Sub AcknowledgeMpuIrq()
        _pendingIrqBits = CByte(_pendingIrqBits And &HFB)
        _mixer(&H82) = _pendingIrqBits
        UpdateIrqLine()
    End Sub

    Private Sub UpdateIrqLine()
        Dim asserted As Boolean = _pendingIrqBits <> 0
        If asserted = _irqLineAsserted Then Return
        _irqLineAsserted = asserted
        DriveIrq(_irq, asserted)
    End Sub

    Private Sub DriveIrq(irq As Integer, asserted As Boolean)
        If irq <= 7 Then
            _masterPic.SetIrqLine(irq, asserted)
        Else
            _slavePic.SetIrqLine(irq - 8, asserted)
        End If
    End Sub

    Private Function ReadMixerData() As Byte
        Select Case _mixerIndex
            Case &H80 : Return EncodeIrqMixer(_irq)
            Case &H81 : Return EncodeDmaMixer(_dma8, _dma16)
            Case &H82 : Return _pendingIrqBits
            Case Else : Return _mixer(_mixerIndex)
        End Select
    End Function

    Private Sub WriteMixerData(value As Byte)
        ' Mixer gain/routing changes occur at this guest I/O instant.  Finish the
        ' already-elapsed host span with the old CT1745 state before applying it.
        FlushHostAudioTimelineInBed()
        If _mixerIndex = 0 Then
            ResetMixer()
            _hostAudioInBed.NotifyPotentialAudioActivity()
            Return
        End If

        Select Case _mixerIndex
            Case &H80
                Dim newIrq As Integer = DecodeIrqMixer(value)
                If newIrq >= 0 Then ChangeIrq(newIrq)
            Case &H81
                DecodeAndSetDmaMixer(value)
            Case &H82
                ' IRQ status is read-only.
            Case Else
                WriteCt1745MixerRegister(_mixerIndex, value)
                ' A mixer write may unmute, reroute, change gain, or alter tone on
                ' an already-running source. Wake the host transducer immediately.
                _hostAudioInBed.NotifyPotentialAudioActivity()
        End Select
    End Sub

    Private Sub WriteCt1745MixerRegister(index As Byte, value As Byte)
        Select Case index
            ' CT1345 compatibility controls are real aliases into the finer CT1745
            ' controls, not independent storage. The +08h mapping selects the odd
            ' 2-dB step corresponding to each legacy 4-dB volume step.
            Case &H4
                _mixer(&H4) = value
                _mixer(&H32) = CByte((value And &HF0) Or &H8)
                _mixer(&H33) = CByte(((CInt(value) And &HF) << 4) Or &H8)
            Case &H22
                _mixer(&H22) = value
                _mixer(&H30) = CByte((value And &HF0) Or &H8)
                _mixer(&H31) = CByte(((CInt(value) And &HF) << 4) Or &H8)
            Case &H26
                _mixer(&H26) = value
                _mixer(&H34) = CByte((value And &HF0) Or &H8)
                _mixer(&H35) = CByte(((CInt(value) And &HF) << 4) Or &H8)
            Case &H28
                _mixer(&H28) = value
                _mixer(&H36) = CByte((value And &HF0) Or &H8)
                _mixer(&H37) = CByte(((CInt(value) And &HF) << 4) Or &H8)
            Case &H2E
                _mixer(&H2E) = value
                _mixer(&H38) = CByte((value And &HF0) Or &H8)
                _mixer(&H39) = CByte(((CInt(value) And &HF) << 4) Or &H8)
            Case &HA
                _mixer(&HA) = CByte(value And &H7)
                _mixer(&H3A) = CByte(((CInt(value) And &H7) << 5) Or &H18)

            ' Native 32-level CT1745 controls. Reserved low bits do not retain data.
            Case &H30 To &H3A
                Dim stored As Byte = CByte(value And &HF8)
                _mixer(index) = stored
                SyncCt1345CompatibilityFromNative(index)

            Case &H3B ' PC speaker: D7:D6 only
                _mixer(index) = CByte(value And &HC0)
            Case &H3C ' output mixer switches: D4:D0
                _mixer(index) = CByte(value And &H1F)
            Case &H3D, &H3E ' input mixer switches: D6:D0
                _mixer(index) = CByte(value And &H7F)
            Case &H3F To &H42 ' input/output gain: D7:D6
                _mixer(index) = CByte(value And &HC0)
            Case &H43 ' microphone AGC/fixed gain bit
                _mixer(index) = CByte(value And &H1)
            Case &H44 To &H47 ' treble/bass: D7:D4
                _mixer(index) = CByte(value And &HF0)
            Case Else
                _mixer(index) = value
        End Select
    End Sub

    Private Sub SyncCt1345CompatibilityFromNative(index As Byte)
        Select Case index
            Case &H30
                _mixer(&H22) = CByte((_mixer(&H22) And &HF) Or (_mixer(&H30) And &HF0))
            Case &H31
                _mixer(&H22) = CByte((_mixer(&H22) And &HF0) Or ((_mixer(&H31) >> 4) And &HF))
            Case &H32
                _mixer(&H4) = CByte((_mixer(&H4) And &HF) Or (_mixer(&H32) And &HF0))
            Case &H33
                _mixer(&H4) = CByte((_mixer(&H4) And &HF0) Or ((_mixer(&H33) >> 4) And &HF))
            Case &H34
                _mixer(&H26) = CByte((_mixer(&H26) And &HF) Or (_mixer(&H34) And &HF0))
            Case &H35
                _mixer(&H26) = CByte((_mixer(&H26) And &HF0) Or ((_mixer(&H35) >> 4) And &HF))
            Case &H36
                _mixer(&H28) = CByte((_mixer(&H28) And &HF) Or (_mixer(&H36) And &HF0))
            Case &H37
                _mixer(&H28) = CByte((_mixer(&H28) And &HF0) Or ((_mixer(&H37) >> 4) And &HF))
            Case &H38
                _mixer(&H2E) = CByte((_mixer(&H2E) And &HF) Or (_mixer(&H38) And &HF0))
            Case &H39
                _mixer(&H2E) = CByte((_mixer(&H2E) And &HF0) Or ((_mixer(&H39) >> 4) And &HF))
            Case &H3A
                _mixer(&HA) = CByte((_mixer(&H3A) >> 5) And &H7)
        End Select
    End Sub

    Private Sub ResetMixer()
        Array.Clear(_mixer, 0, _mixer.Length)

        ' CT1745 documented reset state. Master/Voice/MIDI default to level 24
        ' (-14 dB); CD/Line/Mic default fully attenuated. Compatibility registers
        ' mirror those native controls.
        _mixer(&H30) = &HC0 : _mixer(&H31) = &HC0
        _mixer(&H32) = &HC0 : _mixer(&H33) = &HC0
        _mixer(&H34) = &HC0 : _mixer(&H35) = &HC0
        _mixer(&H36) = 0 : _mixer(&H37) = 0
        _mixer(&H38) = 0 : _mixer(&H39) = 0
        _mixer(&H3A) = 0
        _mixer(&H3B) = 0
        _mixer(&H3C) = &H1F
        _mixer(&H3D) = &H15
        _mixer(&H3E) = &HB
        _mixer(&H3F) = 0 : _mixer(&H40) = 0
        _mixer(&H41) = 0 : _mixer(&H42) = 0
        _mixer(&H43) = 0
        _mixer(&H44) = &H80 : _mixer(&H45) = &H80
        _mixer(&H46) = &H80 : _mixer(&H47) = &H80

        _mixer(&H22) = &HCC
        _mixer(&H4) = &HCC
        _mixer(&H26) = &HCC
        _mixer(&H28) = 0
        _mixer(&H2E) = 0
        _mixer(&HA) = 0

        _mixer(&H80) = EncodeIrqMixer(_irq)
        _mixer(&H81) = EncodeDmaMixer(_dma8, _dma16)
        _mixer(&H82) = _pendingIrqBits
    End Sub

    Private Shared Function EncodeIrqMixer(irq As Integer) As Byte
        Select Case irq
            Case 9 : Return &H1
            Case 5 : Return &H2
            Case 7 : Return &H4
            Case 10 : Return &H8
            Case Else : Return 0
        End Select
    End Function

    Private Shared Function DecodeIrqMixer(value As Byte) As Integer
        If (value And &H8) <> 0 Then Return 10
        If (value And &H4) <> 0 Then Return 7
        If (value And &H2) <> 0 Then Return 5
        If (value And &H1) <> 0 Then Return 9 ' AT IRQ2-compatible selection is physically IRQ9
        Return -1
    End Function

    Private Sub ChangeIrq(newIrq As Integer)
        If newIrq = _irq Then Return
        If _irqLineAsserted Then DriveIrq(_irq, False)
        _irq = newIrq
        _mixer(&H80) = EncodeIrqMixer(_irq)
        If _irqLineAsserted Then DriveIrq(_irq, True)
    End Sub

    Private Shared Function EncodeDmaMixer(dma8 As Integer, dma16 As Integer) As Byte
        Dim value As Integer
        If dma8 = 0 OrElse dma8 = 1 OrElse dma8 = 3 Then value = value Or (1 << dma8)
        ' When dma16 equals the low DMA channel, DSP 4.xx is in its documented
        ' 16-bit-over-8-bit-DMA mode and therefore exposes no high-DMA selection bit.
        If dma16 >= 5 AndAlso dma16 <= 7 Then value = value Or (1 << dma16)
        Return CByte(value And &HFF)
    End Function

    Private Sub DecodeAndSetDmaMixer(value As Byte)
        Dim lowMask As Integer = CInt(value) And &HB ' DMA0, DMA1, DMA3; DMA2 is not valid on SB16
        Dim highMask As Integer = CInt(value) And &HE0

        If lowMask <> 0 AndAlso (lowMask And (lowMask - 1)) = 0 Then
            If (lowMask And &H1) <> 0 Then
                _dma8 = 0
            ElseIf (lowMask And &H2) <> 0 Then
                _dma8 = 1
            ElseIf (lowMask And &H8) <> 0 Then
                _dma8 = 3
            End If
        End If

        If highMask = 0 Then
            ' Documented DSP 4.xx mode: no high DMA selected; 16-bit transfers use
            ' the configured low DMA channel.
            _dma16 = _dma8
        ElseIf (highMask And (highMask - 1)) = 0 Then
            If (highMask And &H20) <> 0 Then
                _dma16 = 5
            ElseIf (highMask And &H40) <> 0 Then
                _dma16 = 6
            ElseIf (highMask And &H80) <> 0 Then
                _dma16 = 7
            End If
        End If

        ' Invalid multi-select writes do not manufacture an arbitrary channel.
        _mixer(&H81) = EncodeDmaMixer(_dma8, _dma16)
    End Sub

    Private Shared Function Ct1745VolumeGain(level As Integer) As Double
        level = Math.Max(0, Math.Min(31, level))
        ' Native CT1745 levels are -62..0 dB in 2 dB steps.
        Dim decibels As Double = CDbl(level - 31) * 2.0
        Return Math.Pow(10.0, decibels / 20.0)
    End Function

    Private Function Ct1745OutputGain(channelLeft As Boolean) As Double
        Dim registerValue As Byte = If(channelLeft, _mixer(&H41), _mixer(&H42))
        Dim stepIndex As Integer = (registerValue >> 6) And &H3
        Return Math.Pow(10.0, (stepIndex * 6.0) / 20.0)
    End Function

    Private Shared Function Ct1745ToneDb(registerValue As Byte) As Double
        Dim level As Integer = (registerValue >> 4) And &HF
        If level <= 7 Then Return (level - 7) * 2.0
        Return (level - 8) * 2.0
    End Function

    Private Function PcmVolume(channelLeft As Boolean) As Double
        Dim master As Integer = If(channelLeft, _mixer(&H30), _mixer(&H31)) >> 3
        Dim voice As Integer = If(channelLeft, _mixer(&H32), _mixer(&H33)) >> 3
        Return Ct1745VolumeGain(master) * Ct1745VolumeGain(voice) * Ct1745OutputGain(channelLeft)
    End Function

    Private Function MidiVolume(channelLeft As Boolean) As Double
        Dim master As Integer = If(channelLeft, _mixer(&H30), _mixer(&H31)) >> 3
        Dim midi As Integer = If(channelLeft, _mixer(&H34), _mixer(&H35)) >> 3
        Return Ct1745VolumeGain(master) * Ct1745VolumeGain(midi) * Ct1745OutputGain(channelLeft)
    End Function

    Private Sub FlushHostAudioTimelineInBed()
        Dim hostFramesInBed As Long = _hostRenderFramesPendingInBed
        If hostFramesInBed <= 0 Then Return

        If _hostAudioInBed.NeedsContinuousRendering Then
            Dim startLeftInBed As Short
            Dim startRightInBed As Short
            If _hostPcmFramesPendingInBed > 0 Then
                startLeftInBed = _hostPcmIntervalStartLeftInBed
                startRightInBed = _hostPcmIntervalStartRightInBed
            Else
                startLeftInBed = _currentLeft
                startRightInBed = _currentRight
            End If

            Dim renderAcceptedInBed As Boolean =
                _hostAudioInBed.PostRenderInterval(hostFramesInBed,
                                                   _hostPcmFramesPendingInBed,
                                                   startLeftInBed,
                                                   startRightInBed,
                                                   True,
                                                   PcmVolume(True),
                                                   PcmVolume(False),
                                                   MidiVolume(True),
                                                   MidiVolume(False),
                                                   Ct1745ToneDb(_mixer(&H46)),
                                                   Ct1745ToneDb(_mixer(&H47)),
                                                   Ct1745ToneDb(_mixer(&H44)),
                                                   Ct1745ToneDb(_mixer(&H45)))
            If renderAcceptedInBed Then
                ' The wavetable request is emitted at the same coalesced boundary as
                ' OPL/mixer work.  This avoids a second instruction-rate command flood.
                Dim wavetableHostInBed As WavetableHost = _wavetableHostInBed
                If wavetableHostInBed IsNot Nothing Then
                    wavetableHostInBed.RequestPcmFrames(hostFramesInBed)
                End If
            ElseIf _hostPcmFramesPendingInBed > 0 Then
                _hostAudioInBed.DiscardPendingPcmSourceFrames()
            End If
        ElseIf _hostPcmFramesPendingInBed > 0 Then
            _hostAudioInBed.DiscardPendingPcmSourceFrames()
        End If

        _hostRenderFramesPendingInBed = 0
        _hostPcmFramesPendingInBed = 0
        _hostPcmIntervalStartLeftInBed = _currentLeft
        _hostPcmIntervalStartRightInBed = _currentRight
    End Sub

    Public Sub AdvanceTime(elapsedPicoseconds As Long) Implements IClockedDevice.AdvanceTime
        If _disposed Then Return
        If elapsedPicoseconds < 0 Then Throw New ArgumentOutOfRangeException(NameOf(elapsedPicoseconds))
        If elapsedPicoseconds = 0 Then Return

        If _dspResetAsserted Then
            If _dspResetAssertedPicoseconds <= Long.MaxValue - elapsedPicoseconds Then
                _dspResetAssertedPicoseconds += elapsedPicoseconds
            Else
                _dspResetAssertedPicoseconds = Long.MaxValue
            End If
        ElseIf _dspInitializationPicosecondsRemaining > 0 Then
            _dspInitializationPicosecondsRemaining -=
                Math.Min(_dspInitializationPicosecondsRemaining, elapsedPicoseconds)
            If _dspInitializationPicosecondsRemaining <= 0 Then
                _dspInitializationPicosecondsRemaining = 0
                _dspReadQueue.Enqueue(&HAA)
            End If
        End If

        If _silenceUnitsRemaining > 0 AndAlso _sampleRate > 0 Then
            Dim silenceTotal As Long =
                _silenceClockNumerator + elapsedPicoseconds * CLng(_sampleRate)
            Dim silenceUnitsElapsed As Long = silenceTotal \ PicosecondsPerSecond
            _silenceClockNumerator = silenceTotal Mod PicosecondsPerSecond
            If silenceUnitsElapsed > 0 Then
                If silenceUnitsElapsed >= _silenceUnitsRemaining Then
                    _silenceUnitsRemaining = 0
                    _silenceClockNumerator = 0
                    RaiseDspIrq(False)
                Else
                    _silenceUnitsRemaining -= CInt(silenceUnitsElapsed)
                End If
            End If
        End If

        ' Guest-visible OPL timers stay on the coordinated machine timeline.  The
        ' expensive waveform generation is mirrored to Sb16HostAudioEngine and no
        ' longer runs 48,000 times per emulated second on the machine owner.
        If _opl.HasRunningTimers Then
            _opl.AdvanceTime(elapsedPicoseconds)
        End If

        If _playbackActive AndAlso Not _playbackPaused Then
            Dim pcmTotal As Long = _pcmClockNumerator + elapsedPicoseconds * CLng(_sampleRate)
            Dim frames As Long = pcmTotal \ PicosecondsPerSecond
            _pcmClockNumerator = pcmTotal Mod PicosecondsPerSecond
            For i As Long = 0 To frames - 1
                If ConsumePcmFrame() Then _hostPcmFramesPendingInBed += 1
                If Not _playbackActive Then Exit For
            Next
        End If

        If _hostPcmFramesPendingInBed > 0 Then
            ' One cheap level write per machine interval, not one Interlocked
            ' activity event per DSP sample.
            _hostAudioInBed.EnsureRenderingActive()
        End If

        ' Advance the card's host transducer clock with one cheap interval.
        ' Host work is parallel: the YMF262 worker synthesizes FM, WavetableHost's
        ' worker synthesizes SoundFont PCM, and the mixer/output worker combines
        ' those streams with DSP PCM and owns waveOut pacing.
        Dim hostTotal As Long = _hostClockNumerator + elapsedPicoseconds * CLng(HostSampleRate)
        Dim hostFrames As Long = hostTotal \ PicosecondsPerSecond
        _hostClockNumerator = hostTotal Mod PicosecondsPerSecond

        If hostFrames > 0 Then
            ' Advance the logical host-audio timeline cheaply on the machine owner.
            ' Most AdvanceTime calls are much smaller than one useful PCM block; queueing
            ' each one independently created tens of thousands of BlockingCollection
            ' transactions per second and let worker backlog become audible corruption.
            _hostRenderFramesPendingInBed += hostFrames

            ' Ordinary continuous audio crosses the worker boundary in ~5-ms batches.
            ' OPL, mixer and MIDI writes call FlushHostAudioTimelineInBed first, so a
            ' partial batch never moves a guest-visible state transition in time.
            If _hostRenderFramesPendingInBed >= HostRenderBatchFrames Then
                FlushHostAudioTimelineInBed()
            End If
        End If
    End Sub

    Public Function PicosecondsUntilNextWakeEvent() As Long Implements IClockWakeSource.PicosecondsUntilNextWakeEvent
        Dim nextWake As Long = Long.MaxValue

        If _dspInitializationPicosecondsRemaining > 0 Then
            nextWake = Math.Min(nextWake, _dspInitializationPicosecondsRemaining)
        End If

        If _silenceUnitsRemaining > 0 AndAlso _sampleRate > 0 Then
            Dim silencePs As Long =
                (CLng(_silenceUnitsRemaining) * PicosecondsPerSecond) \ _sampleRate
            If silencePs <= 0 Then silencePs = 1
            nextWake = Math.Min(nextWake, silencePs)
        End If

        If _playbackActive AndAlso Not _playbackPaused AndAlso
           _sampleRate > 0 AndAlso _playbackUnitsRemaining > 0 Then
            Dim unitsPerFrame As Integer = If(_playbackStereo, 2, 1)
            Dim frames As Long =
                Math.Max(1L, (CLng(_playbackUnitsRemaining) + unitsPerFrame - 1L) \ unitsPerFrame)
            Dim playbackPs As Long = (frames * PicosecondsPerSecond) \ _sampleRate
            If playbackPs <= 0 Then playbackPs = 1
            nextWake = Math.Min(nextWake, playbackPs)
        End If

        Return nextWake
    End Function

    Public Sub ResetDevice() Implements IResettableDevice.ResetDevice
        If _irqLineAsserted Then DriveIrq(_irq, False)
        _irqLineAsserted = False
        _irq = _configuredIrq
        _dma8 = _configuredDma8
        _dma16 = _configuredDma16
        _pendingIrqBits = 0
        _dspReadQueue.Clear()
        _commandParams.Clear()
        _pendingCommand = -1
        _pendingParamCount = 0
        _dspResetAsserted = False
        _dspResetAssertedPicoseconds = 0
        _dspInitializationPicosecondsRemaining = 0
        _speakerEnabled = False
        _sampleRate = 22050
        _outputSampleRate = 22050
        _inputSampleRate = 22050
        _legacyRequestedSampleRate = 22050
        _legacyBlockUnits = 1
        _silenceUnitsRemaining = 0
        _silenceClockNumerator = 0
        _testRegister = 0
        _mpuReadQueue.Clear()
        _mpuUartMode = False
        _sbMidiInputPolling = False
        _sbMidiInputInterrupt = False
        _sbMidiUartMode = False
        _sbMidiUartInterrupt = False
        _sbMidiTimestamp = False
        _sbMidiRestoreAfterReset = False
        If _wavetableHostInBed IsNot Nothing Then _wavetableHostInBed.DiscardBufferedPcm()
        _waveBlasterConnector.HardwareReset()
        StopPlayback()
        _adpcmReference = &H80
        _adpcmScale = 0
        _hostClockNumerator = 0
        _hostRenderFramesPendingInBed = 0
        _hostPcmFramesPendingInBed = 0
        _hostPcmIntervalStartLeftInBed = 0
        _hostPcmIntervalStartRightInBed = 0
        _opl.Reset()
        _hostOplAddress(0) = 0
        _hostOplAddress(1) = 0
        ResetMixer()
        _hostAudioInBed.ResetAudioState()
    End Sub

    Public Function DiagnosticText() As String
        Return "Creative Sound Blaster 16-class ISA" & Environment.NewLine &
               "  I/O / IRQ             : " & _basePort.ToString("X3") & "h / " & _irq.ToString() & Environment.NewLine &
               "  DMA 8 / DMA 16        : " & _dma8.ToString() & " / " & _dma16.ToString() & Environment.NewLine &
               "  physical straps       : IRQ" & _configuredIrq.ToString() & " DMA" & _configuredDma8.ToString() & "/" & _configuredDma16.ToString() & Environment.NewLine &
               "  MPU / game            : " & _mpuBasePort.ToString("X3") & "h / " & If(_gamePortEnabled, "201h enabled", "disabled") & Environment.NewLine &
               "  wavetable hardware    : " & _wavetableHardwareSettingsInBed.Summary() & " / profile " & WavetableAttachmentSettings.NormalizeProfileId(_wavetableHardwareSettingsInBed.ProfileId) & Environment.NewLine &
               "  DSP version           : 4.13" & Environment.NewLine &
               "  sample rate out / in  : " & _outputSampleRate.ToString() & " / " & _inputSampleRate.ToString() & " Hz" & Environment.NewLine &
               "  transfer              : " & If(_playbackActive,
                                                   If(_playbackInput, "ADC ", "DAC ") &
                                                   If(_playback16Bit, "16-bit", "8-bit") &
                                                   If(_playbackStereo, " stereo", " mono"),
                                                   "idle") & Environment.NewLine &
               "  speaker               : " & If(_speakerEnabled, "on", "off") & Environment.NewLine &
               "  pending IRQ bits      : " & _pendingIrqBits.ToString("X2") & Environment.NewLine &
               "  DMA bytes / underruns : " & _dmaBytesPlayed.ToString("N0") & " / " & _dspUnderruns.ToString("N0") & Environment.NewLine &
               "  OPL3                  : " & If(_opl.Opl3Enabled, "enabled", "OPL2-compatible mode") & Environment.NewLine &
               _waveBlasterConnector.DiagnosticText() & Environment.NewLine &
               If(_wavetableHostInBed Is Nothing, "  Wavetable host        : disabled", _wavetableHostInBed.DiagnosticText())
    End Function

    Private Sub HandleProcessExit(sender As Object, e As EventArgs)
        Dispose()
    End Sub

    Public Sub Dispose() Implements IDisposable.Dispose
        If _disposed Then Return
        _disposed = True
        RemoveHandler AppDomain.CurrentDomain.ProcessExit, _processExitHandler
        If _irqLineAsserted Then DriveIrq(_irq, False)
        _irqLineAsserted = False
        StopPlayback()
        _waveBlasterConnector.Detach()
        Dim wavetableHostInBed As WavetableHost = _wavetableHostInBed
        _wavetableHostInBed = Nothing
        _hostAudioInBed.SetWavetableHost(Nothing)
        If wavetableHostInBed IsNot Nothing Then wavetableHostInBed.Dispose()
        _hostAudioInBed.Dispose()
        GC.SuppressFinalize(Me)
    End Sub
End Class


Friend Enum Sb16HostAudioCommandKind As Byte
    RenderInterval = 0
    Reset = 1
End Enum

Friend Structure Sb16HostAudioCommand
    Public Kind As Sb16HostAudioCommandKind
    Public Generation As Integer
    Public StartFrame As Long
    Public HostFrames As Long
    Public PcmSourceFrames As Integer
    Public PcmStartLeft As Short
    Public PcmStartRight As Short
    Public SpeakerEnabled As Boolean
    Public PcmGainLeft As Double
    Public PcmGainRight As Double
    Public MidiGainLeft As Double
    Public MidiGainRight As Double
    Public BassDbLeft As Double
    Public BassDbRight As Double
    Public TrebleDbLeft As Double
    Public TrebleDbRight As Double
End Structure

Friend Enum Sb16OplWorkerCommandKind As Byte
    RegisterWrite = 0
    RenderFrames = 1
    Reset = 2
End Enum

Friend Structure Sb16OplWorkerCommand
    Public Kind As Sb16OplWorkerCommandKind
    Public Generation As Integer
    Public StartFrame As Long
    Public HostFrames As Long
    Public Bank As Integer
    Public Register As Byte
    Public Value As Byte
End Structure

' Parallel host transducer for the SB16.
'
' Guest-visible timers, DMA, DREQ, IRQ and register state remain on the coordinated
' machine owner.  Host-only waveform work is split by silicon responsibility:
'
'   * OPL worker       - owns the host YMF262 synthesis copy and produces FM PCM.
'   * Wavetable worker - owned by WavetableHost / MeltySynth.
'   * Mixer worker     - owns CT1745 host filtering, DSP/FM/wavetable mixing and
'                        WinMM submission.
'
' The VM thread never synthesizes an FM sample and never waits for the Windows
' audio device.  The OPL and wavetable producers can run concurrently while the
' mixer consumes their time-aligned PCM.  Presentation audio may be discarded
' under catastrophic host overload; guest-visible hardware state may not.
Friend NotInheritable Class Sb16HostAudioEngine
    Implements IDisposable

    Private Const MixCommandQueueCapacity As Integer = 8192
    Private Const PcmSourceRingCapacity As Integer = 65536
    Private Const PcmSourceRingMask As Long = PcmSourceRingCapacity - 1L
    Private Const OplPcmRingCapacity As Integer = 65536
    Private Const OplPcmRingMask As Long = OplPcmRingCapacity - 1L
    Private Const RenderBlockFrames As Integer = 512

    ' Wavetable synthesis is independently threaded and may need a short bounded
    ' rendezvous at the final mixer.  YMF262 is different: its PCM is part of the
    ' exact FM waveform, so the mixer must never turn an ordinary worker scheduling
    ' delay into zero-filled holes.  It waits for the requested Yamaha frame watermark
    ' instead; that wait is confined to the mixer/output worker and never blocks the VM.
    Private Const WavetableWaitMilliseconds As Integer = 12

    ' A real OPL/wavetable voice can continue after the CPU stops writing it.
    ' Therefore "no recent command" is NOT an idle test.  The host renderer only
    ' sleeps after it has actually produced sustained near-zero mixed output.
    Private Const SilenceThresholdInBed As Integer = 2
    Private Const SilenceFramesBeforeSleepInBed As Long = 4800L ' 100 ms @ 48 kHz

    Private ReadOnly _sampleRateInBed As Integer

    ' Machine -> mixer: bounded because presentation intervals may be dropped if
    ' Windows audio falls catastrophically behind.
    Private ReadOnly _mixCommandsInBed As New BlockingCollection(Of Sb16HostAudioCommand)(MixCommandQueueCapacity)

    ' Machine -> OPL: unbounded sparse/order-preserving command stream.  OPL
    ' register writes and render-time advancement are part of one ordered Yamaha
    ' timeline, so neither is silently discarded or split into address/data halves.
    ' The producer never waits; if this queue grows, the host is objectively unable
    ' to synthesize the requested card clock in real time and diagnostics can expose
    ' that without corrupting guest state.
    Private ReadOnly _oplCommandsInBed As New BlockingCollection(Of Sb16OplWorkerCommand)()

    Private ReadOnly _mixWorkerInBed As Thread
    Private ReadOnly _oplWorkerInBed As Thread
    Private ReadOnly _oplInBed As Opl3FmCore
    Private ReadOnly _waveOutInBed As WinMmStereoOut16
    Private ReadOnly _oplSamplesAvailableInBed As New AutoResetEvent(False)

    Private ReadOnly _wavetableSyncInBed As New Object()
    Private ReadOnly _bassToneAlphaInBed As Double
    Private ReadOnly _trebleToneAlphaInBed As Double
    Private _bassToneStateLeftInBed As Double
    Private _bassToneStateRightInBed As Double
    Private _trebleLowStateLeftInBed As Double
    Private _trebleLowStateRightInBed As Double

    ' Machine -> mixer decoded DSP PCM ring.  The machine owner is the sole
    ' producer and the mixer worker is the sole consumer.
    Private ReadOnly _pcmSourceLeftInBed(PcmSourceRingCapacity - 1) As Short
    Private ReadOnly _pcmSourceRightInBed(PcmSourceRingCapacity - 1) As Short
    Private _pcmSourceReadPositionInBed As Long
    Private _pcmSourceWritePositionInBed As Long
    Private _pcmSourceDiscardBeforePositionInBed As Long

    ' OPL worker -> mixer worker SPSC ring.  Positions are absolute host-audio
    ' frame numbers, not "number of samples currently buffered".  That lets the
    ' mixer drop an obsolete presentation interval without shifting all later FM
    ' samples in time.
    Private ReadOnly _oplLeftInBed(OplPcmRingCapacity - 1) As Single
    Private ReadOnly _oplRightInBed(OplPcmRingCapacity - 1) As Single
    Private _oplReadPositionInBed As Long
    Private _oplWritePositionInBed As Long
    Private _oplDiscardBeforePositionInBed As Long
    Private _nextRenderFramePositionInBed As Long
    Private _oplHasActiveVoicesInBed As Integer
    Private _oplPendingRegisterWritesInBed As Integer

    Private _wavetableHostInBed As WavetableHost
    Private _generationInBed As Integer
    Private _disposedInBed As Integer

    ' Cross-boundary rendering latch.  Machine-side writes only arm it; only the
    ' mixer worker may disarm it, and only after observing actual sustained silence.
    Private _needsContinuousRenderingInBed As Integer
    Private _activitySerialInBed As Long
    Private _silentFramesInBed As Long
    Private _silenceActivitySerialInBed As Long

    Public Sub New(sampleRateInBed As Integer)
        If sampleRateInBed <= 0 Then Throw New ArgumentOutOfRangeException(NameOf(sampleRateInBed))
        _sampleRateInBed = sampleRateInBed

        ' The programming guide specifies the +/-14 dB control law but not the
        ' CT1745 analog filter topology. Stable first-order shelf boundaries keep
        ' the tone controls electrically meaningful without inventing guest state.
        _bassToneAlphaInBed = 1.0 - Math.Exp((-2.0 * Math.PI * 250.0) / sampleRateInBed)
        _trebleToneAlphaInBed = 1.0 - Math.Exp((-2.0 * Math.PI * 4000.0) / sampleRateInBed)

        _oplInBed = New Opl3FmCore(sampleRateInBed)
        _waveOutInBed = New WinMmStereoOut16(sampleRateInBed)

        _oplWorkerInBed = New Thread(AddressOf OplWorkerLoopInBed) With {
            .IsBackground = True,
            .Name = "Virtual Machine Bench SB16 YMF262 worker",
            .Priority = ThreadPriority.AboveNormal
        }
        _mixWorkerInBed = New Thread(AddressOf MixerWorkerLoopInBed) With {
            .IsBackground = True,
            .Name = "Virtual Machine Bench SB16 mixer/output worker",
            .Priority = ThreadPriority.AboveNormal
        }

        _oplWorkerInBed.Start()
        _mixWorkerInBed.Start()
    End Sub

    Public Sub SetWavetableHost(hostInBed As WavetableHost)
        SyncLock _wavetableSyncInBed
            _wavetableHostInBed = hostInBed
        End SyncLock
        If hostInBed IsNot Nothing Then NotifyPotentialAudioActivity()
    End Sub

    Public ReadOnly Property NeedsContinuousRendering As Boolean
        Get
            Return Volatile.Read(_needsContinuousRenderingInBed) <> 0
        End Get
    End Property

    Public Sub NotifyPotentialAudioActivity()
        If Volatile.Read(_disposedInBed) <> 0 Then Return
        Interlocked.Increment(_activitySerialInBed)
        Volatile.Write(_needsContinuousRenderingInBed, 1)
    End Sub

    ' Continuous DSP PCM does not need to create a new cross-thread activity
    ' serial for every sample.  The machine owner calls this once per clock
    ' interval when at least one source frame reached the host ring.
    Public Sub EnsureRenderingActive()
        If Volatile.Read(_disposedInBed) <> 0 Then Return
        Volatile.Write(_needsContinuousRenderingInBed, 1)
    End Sub

    Public Sub DiscardPendingPcmSourceFrames()
        Interlocked.Exchange(_pcmSourceDiscardBeforePositionInBed,
                             Interlocked.Read(_pcmSourceWritePositionInBed))
    End Sub

    Public Sub PostOplRegister(bankInBed As Integer,
                               registerInBed As Byte,
                               valueInBed As Byte)
        If Volatile.Read(_disposedInBed) <> 0 Then Return

        Dim commandInBed As New Sb16OplWorkerCommand With {
            .Kind = Sb16OplWorkerCommandKind.RegisterWrite,
            .Generation = Volatile.Read(_generationInBed),
            .Bank = bankInBed And 1,
            .Register = registerInBed,
            .Value = valueInBed
        }

        ' Complete Yamaha register transactions are serialized on the OPL worker.
        ' Count the write until the producer has actually applied (or reset-discarded)
        ' it; the mixer uses this as a control-state watermark and may not infer idle
        ' while a guest-visible Yamaha transition is still waiting in the queue.
        Interlocked.Increment(_oplPendingRegisterWritesInBed)
        Try
            _oplCommandsInBed.Add(commandInBed)
        Catch ex As InvalidOperationException
            Interlocked.Decrement(_oplPendingRegisterWritesInBed)
        End Try
    End Sub

    ' SPSC decoded-DSP source ring.  No allocation or wait occurs in the DMA/sample
    ' hot path.
    Public Function TryPostPcmSourceFrame(leftInBed As Short,
                                          rightInBed As Short) As Boolean
        If Volatile.Read(_disposedInBed) <> 0 Then Return False
        Dim writeInBed As Long = Interlocked.Read(_pcmSourceWritePositionInBed)
        Dim readInBed As Long = Interlocked.Read(_pcmSourceReadPositionInBed)
        Dim discardInBed As Long = Interlocked.Read(_pcmSourceDiscardBeforePositionInBed)
        If readInBed < discardInBed Then readInBed = discardInBed
        If writeInBed - readInBed >= PcmSourceRingCapacity Then Return False

        Dim indexInBed As Integer = CInt(writeInBed And PcmSourceRingMask)
        _pcmSourceLeftInBed(indexInBed) = leftInBed
        _pcmSourceRightInBed(indexInBed) = rightInBed
        Interlocked.Exchange(_pcmSourceWritePositionInBed, writeInBed + 1L)
        Return True
    End Function

    Public Function PostRenderInterval(hostFramesInBed As Long,
                                       pcmSourceFramesInBed As Integer,
                                       pcmStartLeftInBed As Short,
                                       pcmStartRightInBed As Short,
                                       speakerEnabledInBed As Boolean,
                                       pcmGainLeftInBed As Double,
                                       pcmGainRightInBed As Double,
                                       midiGainLeftInBed As Double,
                                       midiGainRightInBed As Double,
                                       bassDbLeftInBed As Double,
                                       bassDbRightInBed As Double,
                                       trebleDbLeftInBed As Double,
                                       trebleDbRightInBed As Double) As Boolean
        If hostFramesInBed <= 0 OrElse Volatile.Read(_disposedInBed) <> 0 Then Return False

        Dim generationInBed As Integer = Volatile.Read(_generationInBed)
        Dim startFrameInBed As Long = Interlocked.Read(_nextRenderFramePositionInBed)
        Dim endFrameInBed As Long
        If hostFramesInBed > Long.MaxValue - startFrameInBed Then
            endFrameInBed = Long.MaxValue
        Else
            endFrameInBed = startFrameInBed + hostFramesInBed
        End If
        Interlocked.Exchange(_nextRenderFramePositionInBed, endFrameInBed)

        ' Give the Yamaha worker its time slice first.  It can synthesize in
        ' parallel while the machine owner enqueues the mixer interval and the
        ' Wave Blaster worker renders the same host-time span.
        Dim oplCommandInBed As New Sb16OplWorkerCommand With {
            .Kind = Sb16OplWorkerCommandKind.RenderFrames,
            .Generation = generationInBed,
            .StartFrame = startFrameInBed,
            .HostFrames = hostFramesInBed
        }
        Try
            _oplCommandsInBed.Add(oplCommandInBed)
        Catch ex As InvalidOperationException
            Return False
        End Try

        Dim mixCommandInBed As New Sb16HostAudioCommand With {
            .Kind = Sb16HostAudioCommandKind.RenderInterval,
            .Generation = generationInBed,
            .StartFrame = startFrameInBed,
            .HostFrames = hostFramesInBed,
            .PcmSourceFrames = Math.Max(0, pcmSourceFramesInBed),
            .PcmStartLeft = pcmStartLeftInBed,
            .PcmStartRight = pcmStartRightInBed,
            .SpeakerEnabled = speakerEnabledInBed,
            .PcmGainLeft = pcmGainLeftInBed,
            .PcmGainRight = pcmGainRightInBed,
            .MidiGainLeft = midiGainLeftInBed,
            .MidiGainRight = midiGainRightInBed,
            .BassDbLeft = bassDbLeftInBed,
            .BassDbRight = bassDbRightInBed,
            .TrebleDbLeft = trebleDbLeftInBed,
            .TrebleDbRight = trebleDbRightInBed
        }

        Try
            If _mixCommandsInBed.TryAdd(mixCommandInBed, 0) Then Return True
        Catch ex As InvalidOperationException
        End Try

        ' Host presentation has fallen behind.  Keep the OPL worker advancing its
        ' phase/envelope timeline, but tell its PCM ring and the DSP PCM ring that
        ' this obsolete presentation span must not shift later audio backward.
        PublishOplDiscardBeforeInBed(endFrameInBed)
        Interlocked.Exchange(_pcmSourceDiscardBeforePositionInBed,
                             Interlocked.Read(_pcmSourceWritePositionInBed))
        Return False
    End Function

    Public Sub ResetAudioState()
        If Volatile.Read(_disposedInBed) <> 0 Then Return

        Dim generationInBed As Integer = Interlocked.Increment(_generationInBed)
        Dim resetFrameInBed As Long = Interlocked.Read(_nextRenderFramePositionInBed)

        Interlocked.Exchange(_pcmSourceDiscardBeforePositionInBed,
                             Interlocked.Read(_pcmSourceWritePositionInBed))
        PublishOplDiscardBeforeInBed(resetFrameInBed)
        Interlocked.Exchange(_silentFramesInBed, 0L)

        Dim activityInBed As Long = Interlocked.Increment(_activitySerialInBed)
        Interlocked.Exchange(_silenceActivitySerialInBed, activityInBed)
        Volatile.Write(_needsContinuousRenderingInBed, 1)

        Dim oplCommandInBed As New Sb16OplWorkerCommand With {
            .Kind = Sb16OplWorkerCommandKind.Reset,
            .Generation = generationInBed,
            .StartFrame = resetFrameInBed
        }
        Dim mixCommandInBed As New Sb16HostAudioCommand With {
            .Kind = Sb16HostAudioCommandKind.Reset,
            .Generation = generationInBed,
            .StartFrame = resetFrameInBed
        }

        Try
            _oplCommandsInBed.Add(oplCommandInBed)
        Catch ex As InvalidOperationException
        End Try
        Try
            _mixCommandsInBed.Add(mixCommandInBed)
        Catch ex As InvalidOperationException
        End Try
    End Sub

    Private Sub OplWorkerLoopInBed()
        Try
            For Each commandInBed As Sb16OplWorkerCommand In _oplCommandsInBed.GetConsumingEnumerable()
                If Volatile.Read(_disposedInBed) <> 0 Then Exit For

                Dim isRegisterWriteInBed As Boolean =
                    commandInBed.Kind = Sb16OplWorkerCommandKind.RegisterWrite

                If commandInBed.Generation <> Volatile.Read(_generationInBed) Then
                    If isRegisterWriteInBed Then Interlocked.Decrement(_oplPendingRegisterWritesInBed)
                    Continue For
                End If

                Try
                    Select Case commandInBed.Kind
                        Case Sb16OplWorkerCommandKind.RegisterWrite
                            _oplInBed.WriteAddress(commandInBed.Bank, commandInBed.Register)
                            _oplInBed.WriteData(commandInBed.Bank, commandInBed.Value)
                            Volatile.Write(_oplHasActiveVoicesInBed,
                                           If(_oplInBed.HasActiveVoices, 1, 0))

                            ' The guest-side write armed rendering when it was issued.
                            ' Arm it again after the producer has actually changed Yamaha
                            ' state.  A mixer that rendered preceding silence can therefore
                            ' never strand a newly processed KEY-ON in the sleeping state.
                            Interlocked.Increment(_activitySerialInBed)
                            Volatile.Write(_needsContinuousRenderingInBed, 1)

                        Case Sb16OplWorkerCommandKind.RenderFrames
                            RenderOplFramesInBed(commandInBed)

                        Case Sb16OplWorkerCommandKind.Reset
                            _oplInBed.Reset()
                            Volatile.Write(_oplHasActiveVoicesInBed, 0)
                            PublishOplDiscardBeforeInBed(commandInBed.StartFrame)
                    End Select
                Finally
                    If isRegisterWriteInBed Then Interlocked.Decrement(_oplPendingRegisterWritesInBed)
                    _oplSamplesAvailableInBed.Set()
                End Try
            Next
        Catch ex As InvalidOperationException
        Catch ex As ObjectDisposedException
        Finally
            _oplSamplesAvailableInBed.Set()
        End Try
    End Sub

    Private Sub RenderOplFramesInBed(commandInBed As Sb16OplWorkerCommand)
        Dim framePositionInBed As Long = commandInBed.StartFrame
        Dim remainingInBed As Long = Math.Max(0L, commandInBed.HostFrames)

        While remainingInBed > 0 AndAlso
              Volatile.Read(_disposedInBed) = 0 AndAlso
              commandInBed.Generation = Volatile.Read(_generationInBed)

            Dim blockFramesInBed As Integer =
                CInt(Math.Min(CLng(RenderBlockFrames), remainingInBed))

            For indexInBed As Integer = 0 To blockFramesInBed - 1
                Dim fmLeftInBed As Double
                Dim fmRightInBed As Double
                _oplInBed.GenerateSample(fmLeftInBed, fmRightInBed)
                WriteOplSampleInBed(framePositionInBed,
                                    CSng(fmLeftInBed),
                                    CSng(fmRightInBed))
                framePositionInBed += 1L
            Next

            remainingInBed -= blockFramesInBed
            Volatile.Write(_oplHasActiveVoicesInBed,
                           If(_oplInBed.HasActiveVoices, 1, 0))
            _oplSamplesAvailableInBed.Set()
        End While
    End Sub

    Private Sub WriteOplSampleInBed(framePositionInBed As Long,
                                    leftInBed As Single,
                                    rightInBed As Single)
        Dim readInBed As Long = Interlocked.Read(_oplReadPositionInBed)
        Dim discardInBed As Long = Interlocked.Read(_oplDiscardBeforePositionInBed)
        If readInBed < discardInBed Then readInBed = discardInBed

        ' The OPL worker is allowed to outrun a stalled Windows presenter.  Drop
        ' the oldest host-only PCM rather than ever stalling Yamaha synthesis or
        ' the VM.  Absolute frame positions keep later samples synchronized.
        If framePositionInBed - readInBed >= OplPcmRingCapacity Then
            Dim newDiscardInBed As Long = framePositionInBed - OplPcmRingCapacity + 1L
            PublishOplDiscardBeforeInBed(newDiscardInBed)
        End If

        Dim indexInBed As Integer = CInt(framePositionInBed And OplPcmRingMask)
        _oplLeftInBed(indexInBed) = leftInBed
        _oplRightInBed(indexInBed) = rightInBed

        ' Publish sample data only after both channels have been written.
        Interlocked.Exchange(_oplWritePositionInBed, framePositionInBed + 1L)
    End Sub

    Private Sub MixerWorkerLoopInBed()
        Dim oplLeftInBed(RenderBlockFrames - 1) As Single
        Dim oplRightInBed(RenderBlockFrames - 1) As Single
        Dim wavetableLeftInBed(RenderBlockFrames - 1) As Single
        Dim wavetableRightInBed(RenderBlockFrames - 1) As Single

        Try
            For Each commandInBed As Sb16HostAudioCommand In _mixCommandsInBed.GetConsumingEnumerable()
                If Volatile.Read(_disposedInBed) <> 0 Then Exit For
                If commandInBed.Generation <> Volatile.Read(_generationInBed) Then Continue For

                Select Case commandInBed.Kind
                    Case Sb16HostAudioCommandKind.Reset
                        _waveOutInBed.Reset()
                        _bassToneStateLeftInBed = 0.0
                        _bassToneStateRightInBed = 0.0
                        _trebleLowStateLeftInBed = 0.0
                        _trebleLowStateRightInBed = 0.0
                        _silentFramesInBed = 0
                        _silenceActivitySerialInBed =
                            Interlocked.Read(_activitySerialInBed)
                        PublishOplDiscardBeforeInBed(commandInBed.StartFrame)

                    Case Sb16HostAudioCommandKind.RenderInterval
                        RenderMixIntervalInBed(commandInBed,
                                               oplLeftInBed,
                                               oplRightInBed,
                                               wavetableLeftInBed,
                                               wavetableRightInBed)
                End Select
            Next
        Catch ex As InvalidOperationException
        Catch ex As ObjectDisposedException
        End Try
    End Sub

    Private Sub RenderMixIntervalInBed(commandInBed As Sb16HostAudioCommand,
                                       oplLeftInBed As Single(),
                                       oplRightInBed As Single(),
                                       wavetableLeftInBed As Single(),
                                       wavetableRightInBed As Single())
        Dim totalHostFramesInBed As Long = Math.Max(1L, commandInBed.HostFrames)
        Dim sourceFramesInBed As Integer = Math.Max(0, commandInBed.PcmSourceFrames)
        Dim sourceAccumulatorInBed As Long = 0
        Dim sourceConsumedInBed As Integer = 0
        Dim currentPcmLeftInBed As Short = commandInBed.PcmStartLeft
        Dim currentPcmRightInBed As Short = commandInBed.PcmStartRight
        Dim hostRemainingInBed As Long = commandInBed.HostFrames
        Dim framePositionInBed As Long = commandInBed.StartFrame

        While hostRemainingInBed > 0 AndAlso
              Volatile.Read(_disposedInBed) = 0 AndAlso
              commandInBed.Generation = Volatile.Read(_generationInBed)

            Dim blockFramesInBed As Integer =
                CInt(Math.Min(CLng(RenderBlockFrames), hostRemainingInBed))

            ' Never manufacture holes in an FM waveform because the producer lost a
            ' host scheduling race.  The mixer may wait for YMF262 because it is already
            ' off the VM thread.  If the span became obsolete (reset/overflow), skip the
            ' whole presentation block coherently instead of splicing zeroes into it.
            If Not FillOplBlockInBed(framePositionInBed,
                                     oplLeftInBed,
                                     oplRightInBed,
                                     blockFramesInBed,
                                     commandInBed.Generation) Then
                ' A reset/overflow/fault made this presentation interval obsolete.
                ' Drop the remainder as one coherent span; do not resume halfway
                ' through with DSP/FM source alignment shifted by the lost block.
                Exit While
            End If

            FillWavetableBlockInBed(wavetableLeftInBed,
                                    wavetableRightInBed,
                                    blockFramesInBed)

            Dim blockIsSilentInBed As Boolean = True

            For indexInBed As Integer = 0 To blockFramesInBed - 1
                sourceAccumulatorInBed += sourceFramesInBed
                While sourceConsumedInBed < sourceFramesInBed AndAlso
                      sourceAccumulatorInBed >= totalHostFramesInBed
                    Dim nextLeftInBed As Short
                    Dim nextRightInBed As Short
                    If TryReadPcmSourceFrameInBed(nextLeftInBed, nextRightInBed) Then
                        currentPcmLeftInBed = nextLeftInBed
                        currentPcmRightInBed = nextRightInBed
                    End If
                    sourceConsumedInBed += 1
                    sourceAccumulatorInBed -= totalHostFramesInBed
                End While

                Dim pcmLeftInBed As Double = 0.0
                Dim pcmRightInBed As Double = 0.0
                If commandInBed.SpeakerEnabled Then
                    pcmLeftInBed = CDbl(currentPcmLeftInBed) * commandInBed.PcmGainLeft
                    pcmRightInBed = CDbl(currentPcmRightInBed) * commandInBed.PcmGainRight
                End If

                ' Both synthesis chips have their own producer worker.  The mixer
                ' only scales and combines already-generated PCM for this exact
                ' host-audio frame.
                Dim midiLeftInBed As Double =
                    (CDbl(oplLeftInBed(indexInBed)) * 32767.0 +
                     CDbl(wavetableLeftInBed(indexInBed)) * 9000.0) *
                    commandInBed.MidiGainLeft
                Dim midiRightInBed As Double =
                    (CDbl(oplRightInBed(indexInBed)) * 32767.0 +
                     CDbl(wavetableRightInBed(indexInBed)) * 9000.0) *
                    commandInBed.MidiGainRight

                Dim rawLeftInBed As Double = pcmLeftInBed + midiLeftInBed
                Dim rawRightInBed As Double = pcmRightInBed + midiRightInBed

                Dim tonedLeftInBed As Double =
                    ApplyCt1745ToneInBed(rawLeftInBed,
                                         commandInBed.BassDbLeft,
                                         commandInBed.TrebleDbLeft,
                                         _bassToneStateLeftInBed,
                                         _trebleLowStateLeftInBed)
                Dim tonedRightInBed As Double =
                    ApplyCt1745ToneInBed(rawRightInBed,
                                         commandInBed.BassDbRight,
                                         commandInBed.TrebleDbRight,
                                         _bassToneStateRightInBed,
                                         _trebleLowStateRightInBed)

                Dim mixedLeftInBed As Integer =
                    ClampInt16InBed(CInt(Math.Round(tonedLeftInBed)))
                Dim mixedRightInBed As Integer =
                    ClampInt16InBed(CInt(Math.Round(tonedRightInBed)))
                _waveOutInBed.SubmitSample(CShort(mixedLeftInBed),
                                           CShort(mixedRightInBed))

                If Math.Abs(mixedLeftInBed) > SilenceThresholdInBed OrElse
                   Math.Abs(mixedRightInBed) > SilenceThresholdInBed Then
                    blockIsSilentInBed = False
                End If
            Next

            UpdateRenderedSilenceStateInBed(blockIsSilentInBed, blockFramesInBed)
            hostRemainingInBed -= blockFramesInBed
            framePositionInBed += blockFramesInBed
        End While

        ' A dropped/overloaded presentation frame must not poison the next interval's
        ' decoded-DSP source alignment.  Consume any source frames not reached above.
        While sourceConsumedInBed < sourceFramesInBed
            Dim ignoredLeftInBed As Short
            Dim ignoredRightInBed As Short
            TryReadPcmSourceFrameInBed(ignoredLeftInBed, ignoredRightInBed)
            sourceConsumedInBed += 1
        End While
    End Sub

    Private Function FillOplBlockInBed(startFrameInBed As Long,
                                       leftInBed As Single(),
                                       rightInBed As Single(),
                                       frameCountInBed As Integer,
                                       generationInBed As Integer) As Boolean
        Array.Clear(leftInBed, 0, frameCountInBed)
        Array.Clear(rightInBed, 0, frameCountInBed)
        If frameCountInBed <= 0 Then Return True

        Dim endFrameInBed As Long = startFrameInBed + CLng(frameCountInBed)

        ' The mixer/output worker is deliberately the synchronization point.  It may
        ' wait as long as the Yamaha producer needs; neither the VM owner nor the OPL
        ' producer is blocked by this.  A late worker therefore causes latency under
        ' host overload, not random zero-filled slots inside a note.
        Do
            If generationInBed <> Volatile.Read(_generationInBed) Then Return False
            If Volatile.Read(_disposedInBed) <> 0 Then Return False

            Dim discardInBed As Long = Interlocked.Read(_oplDiscardBeforePositionInBed)
            If discardInBed > startFrameInBed Then
                ' At least part of this block is already obsolete.  Do not make a
                ' half-old/half-new waveform; drop this presentation block coherently.
                PublishOplReadAtLeastInBed(Math.Min(discardInBed, endFrameInBed))
                Return False
            End If

            Dim writeInBed As Long = Interlocked.Read(_oplWritePositionInBed)
            If writeInBed >= endFrameInBed Then Exit Do

            ' Avoid an infinite wait if the producer itself has faulted.  This is not
            ' a normal underrun path: healthy scheduling delay has no timeout.
            If Not _oplWorkerInBed.IsAlive Then Return False
            _oplSamplesAvailableInBed.WaitOne(2)
        Loop

        For indexInBed As Integer = 0 To frameCountInBed - 1
            Dim sampleLeftInBed As Single
            Dim sampleRightInBed As Single
            Dim absoluteFrameInBed As Long = startFrameInBed + indexInBed
            If Not TryReadOplSampleInBed(absoluteFrameInBed,
                                         sampleLeftInBed,
                                         sampleRightInBed) Then
                ' The readiness watermark said this entire block existed.  If a reset
                ' or overflow invalidated it between that check and consumption, reject
                ' the entire block rather than inserting an isolated zero sample.
                Array.Clear(leftInBed, 0, frameCountInBed)
                Array.Clear(rightInBed, 0, frameCountInBed)
                Return False
            End If
            leftInBed(indexInBed) = sampleLeftInBed
            rightInBed(indexInBed) = sampleRightInBed
        Next
        Return True
    End Function

    Private Function TryReadOplSampleInBed(framePositionInBed As Long,
                                           ByRef leftInBed As Single,
                                           ByRef rightInBed As Single) As Boolean
        leftInBed = 0.0F
        rightInBed = 0.0F

        Dim discardInBed As Long = Interlocked.Read(_oplDiscardBeforePositionInBed)
        If framePositionInBed < discardInBed Then
            PublishOplReadAtLeastInBed(discardInBed)
            Return False
        End If

        Dim writeInBed As Long = Interlocked.Read(_oplWritePositionInBed)
        If writeInBed <= framePositionInBed Then Return False

        Dim indexInBed As Integer = CInt(framePositionInBed And OplPcmRingMask)
        leftInBed = _oplLeftInBed(indexInBed)
        rightInBed = _oplRightInBed(indexInBed)
        PublishOplReadAtLeastInBed(framePositionInBed + 1L)
        Return True
    End Function

    Private Sub PublishOplDiscardBeforeInBed(positionInBed As Long)
        AtomicMaxInBed(_oplDiscardBeforePositionInBed, positionInBed)
        PublishOplReadAtLeastInBed(positionInBed)
        _oplSamplesAvailableInBed.Set()
    End Sub

    Private Sub PublishOplReadAtLeastInBed(positionInBed As Long)
        AtomicMaxInBed(_oplReadPositionInBed, positionInBed)
    End Sub

    Private Shared Sub AtomicMaxInBed(ByRef targetInBed As Long,
                                      valueInBed As Long)
        Dim observedInBed As Long = Interlocked.Read(targetInBed)
        While valueInBed > observedInBed
            Dim previousInBed As Long =
                Interlocked.CompareExchange(targetInBed, valueInBed, observedInBed)
            If previousInBed = observedInBed Then Exit While
            observedInBed = previousInBed
        End While
    End Sub

    Private Sub UpdateRenderedSilenceStateInBed(blockIsSilentInBed As Boolean,
                                                  blockFramesInBed As Integer)
        If Not blockIsSilentInBed Then
            _silentFramesInBed = 0
            _silenceActivitySerialInBed = Interlocked.Read(_activitySerialInBed)
            Return
        End If

        If _silentFramesInBed = 0 Then
            _silenceActivitySerialInBed = Interlocked.Read(_activitySerialInBed)
        End If
        _silentFramesInBed += Math.Max(0, blockFramesInBed)

        If _silentFramesInBed < SilenceFramesBeforeSleepInBed Then Return

        ' A queued/applied guest register transition is not silence.  In particular,
        ' KEY-ON may be behind already-requested silent render work on the Yamaha
        ' producer.  Keep the host transducer armed until every register transaction
        ' issued by the guest has crossed the worker boundary.
        If Volatile.Read(_oplPendingRegisterWritesInBed) > 0 Then
            _silentFramesInBed = SilenceFramesBeforeSleepInBed
            Return
        End If

        ' The OPL core is worker-owned.  Only its cheap active-voice mirror crosses
        ' back to the mixer thread.
        If Volatile.Read(_oplHasActiveVoicesInBed) <> 0 Then
            _silentFramesInBed = SilenceFramesBeforeSleepInBed
            Return
        End If

        Dim wavetableMayContinueInBed As Boolean
        SyncLock _wavetableSyncInBed
            Dim hostInBed As WavetableHost = _wavetableHostInBed
            wavetableMayContinueInBed =
                hostInBed IsNot Nothing AndAlso hostInBed.MayStillProduceAudio
        End SyncLock
        If wavetableMayContinueInBed Then
            _silentFramesInBed = SilenceFramesBeforeSleepInBed
            Return
        End If

        Dim observedActivityInBed As Long = Interlocked.Read(_activitySerialInBed)
        If observedActivityInBed <> _silenceActivitySerialInBed Then
            _silentFramesInBed = 0
            _silenceActivitySerialInBed = observedActivityInBed
            Return
        End If

        Volatile.Write(_needsContinuousRenderingInBed, 0)

        ' Close the narrow race where new activity lands between the serial read
        ' above and the sleep write.
        If Interlocked.Read(_activitySerialInBed) <> observedActivityInBed Then
            Volatile.Write(_needsContinuousRenderingInBed, 1)
            _silentFramesInBed = 0
            _silenceActivitySerialInBed = Interlocked.Read(_activitySerialInBed)
        End If
    End Sub

    Private Sub FillWavetableBlockInBed(leftInBed As Single(),
                                        rightInBed As Single(),
                                        frameCountInBed As Integer)
        Array.Clear(leftInBed, 0, frameCountInBed)
        Array.Clear(rightInBed, 0, frameCountInBed)

        SyncLock _wavetableSyncInBed
            Dim hostInBed As WavetableHost = _wavetableHostInBed
            If hostInBed Is Nothing Then Return
            If Not hostInBed.MayStillProduceAudio Then Return

            ' WavetableHost owns a completely separate MeltySynth worker.  Waiting
            ' here synchronizes only the final mixer to that producer; it does not
            ' serialize or pause YMF262 synthesis.
            hostInBed.ReadPcmBlockWait(leftInBed,
                                       rightInBed,
                                       frameCountInBed,
                                       WavetableWaitMilliseconds)
        End SyncLock
    End Sub

    Private Function TryReadPcmSourceFrameInBed(ByRef leftInBed As Short,
                                                ByRef rightInBed As Short) As Boolean
        leftInBed = 0
        rightInBed = 0

        Dim readInBed As Long = Interlocked.Read(_pcmSourceReadPositionInBed)
        Dim discardBeforeInBed As Long =
            Interlocked.Read(_pcmSourceDiscardBeforePositionInBed)

        If readInBed < discardBeforeInBed Then
            readInBed = discardBeforeInBed
            Interlocked.Exchange(_pcmSourceReadPositionInBed, readInBed)
        End If

        Dim writeInBed As Long = Interlocked.Read(_pcmSourceWritePositionInBed)
        If readInBed >= writeInBed Then Return False

        Dim indexInBed As Integer = CInt(readInBed And PcmSourceRingMask)
        leftInBed = _pcmSourceLeftInBed(indexInBed)
        rightInBed = _pcmSourceRightInBed(indexInBed)
        Interlocked.Exchange(_pcmSourceReadPositionInBed, readInBed + 1L)
        Return True
    End Function

    Private Function ApplyCt1745ToneInBed(valueInBed As Double,
                                           bassDbInBed As Double,
                                           trebleDbInBed As Double,
                                           ByRef bassStateInBed As Double,
                                           ByRef trebleLowStateInBed As Double) As Double
        bassStateInBed += _bassToneAlphaInBed * (valueInBed - bassStateInBed)
        trebleLowStateInBed += _trebleToneAlphaInBed * (valueInBed - trebleLowStateInBed)

        Dim lowInBed As Double = bassStateInBed
        Dim highInBed As Double = valueInBed - trebleLowStateInBed
        Dim midInBed As Double = valueInBed - lowInBed - highInBed
        Dim bassGainInBed As Double = Math.Pow(10.0, bassDbInBed / 20.0)
        Dim trebleGainInBed As Double = Math.Pow(10.0, trebleDbInBed / 20.0)
        Return lowInBed * bassGainInBed + midInBed + highInBed * trebleGainInBed
    End Function

    Private Shared Function ClampInt16InBed(valueInBed As Integer) As Integer
        If valueInBed > Short.MaxValue Then Return Short.MaxValue
        If valueInBed < Short.MinValue Then Return Short.MinValue
        Return valueInBed
    End Function

    Public Sub Dispose() Implements IDisposable.Dispose
        If Interlocked.Exchange(_disposedInBed, 1) <> 0 Then Return

        Try
            _oplCommandsInBed.CompleteAdding()
        Catch ex As InvalidOperationException
        End Try
        Try
            _mixCommandsInBed.CompleteAdding()
        Catch ex As InvalidOperationException
        End Try

        _oplSamplesAvailableInBed.Set()

        SyncLock _wavetableSyncInBed
            _wavetableHostInBed = Nothing
        End SyncLock

        If Thread.CurrentThread IsNot _oplWorkerInBed Then _oplWorkerInBed.Join(500)
        If Thread.CurrentThread IsNot _mixWorkerInBed Then _mixWorkerInBed.Join(500)

        _waveOutInBed.Dispose()
        _oplSamplesAvailableInBed.Dispose()
    End Sub
End Class

' YMF262/OPL3 register, timer and host-synthesis core.  Guest-visible timer/status
' state remains on the coordinated card timeline; expensive phase/envelope/FM
' generation lives on the dedicated SB16 YMF262 worker and follows Yamaha's
' documented clock, rate, routing, operator and waveform model.
Friend NotInheritable Class Opl3FmCore
    ' YMF262 host renderer.
    '
    ' The card-facing instance is also used for timer/status compatibility, while the
    ' host-audio copy below is owned only by Sb16HostAudioEngine's YMF262 worker.
    ' Register writes and render intervals share one ordered worker command stream,
    ' preserving guest write order without making the machine owner synthesize FM.
    '
    ' Yamaha's application manual defines a 14.31818 MHz master clock and a
    ' 49.7 kHz (fM / 288) output stream.  Render internally at that cadence and
    ' resample only at the final host boundary.
    Private Const YamahaMasterClockHz As Double = 14318180.0
    Private Const YamahaNativeSampleRate As Double = YamahaMasterClockHz / 288.0
    Private Const EnvelopeSilenceDb As Double = 96.0
    Private Const TwoPi As Double = Math.PI * 2.0

    Private Enum EnvelopePhase As Byte
        Off = 0
        Attack = 1
        Decay = 2
        Sustain = 3
        Release = 4
    End Enum

    Private NotInheritable Class OperatorState
        Public Reg20 As Byte
        Public Reg40 As Byte
        Public Reg60 As Byte
        Public Reg80 As Byte
        Public RegE0 As Byte
        Public Phase As Double
        Public AttenuationDb As Double
        Public EnvPhase As EnvelopePhase
    End Class

    Private NotInheritable Class ChannelState
        Public Fnum As Integer
        Public Block As Integer
        Public KeyOn As Boolean
        Public RegC0 As Byte
        Public Feedback1 As Double
        Public Feedback2 As Double
    End Class

    Private Shared ReadOnly OperatorOffsets() As Integer =
        {0, 1, 2, 3, 4, 5, 8, 9, 10, 11, 12, 13, 16, 17, 18, 19, 20, 21}
    Private Shared ReadOnly ModulatorIndex() As Integer =
        {0, 1, 2, 6, 7, 8, 12, 13, 14}
    Private Shared ReadOnly CarrierIndex() As Integer =
        {3, 4, 5, 9, 10, 11, 15, 16, 17}
    Private Shared ReadOnly MultiplierTable() As Double =
        {0.5, 1.0, 2.0, 3.0, 4.0, 5.0, 6.0, 7.0,
         8.0, 9.0, 10.0, 10.0, 12.0, 12.0, 15.0, 15.0}
    Private Shared ReadOnly FeedbackDepthRadians() As Double =
        {0.0, Math.PI / 16.0, Math.PI / 8.0, Math.PI / 4.0,
         Math.PI / 2.0, Math.PI, Math.PI * 2.0, Math.PI * 4.0}

    Private ReadOnly _hostSampleRate As Integer
    Private ReadOnly _registers(1, 255) As Byte
    Private ReadOnly _address(1) As Byte
    Private ReadOnly _operators(35) As OperatorState
    Private ReadOnly _channels(17) As ChannelState

    Private _opl3Enabled As Boolean
    Private _connectionSelect As Byte
    Private _nts As Boolean
    Private _rhythmControl As Byte

    Private _timer1Value As Byte
    Private _timer2Value As Byte
    Private _timerControl As Byte
    Private _timer1Elapsed As Long
    Private _timer2Elapsed As Long
    Private _status As Byte

    ' Native YMF262 sample clock -> host sample clock bridge.  The expensive native
    ' FM step runs only on the SB16 host-audio worker.
    Private _nativeClockAccumulator As Double
    Private _nativePrimed As Boolean
    Private _nativePreviousLeft As Double
    Private _nativePreviousRight As Double
    Private _nativeCurrentLeft As Double
    Private _nativeCurrentRight As Double

    Private _tremoloPhase As Double
    Private _vibratoPhase As Double
    Private _noiseLfsr As UInteger = 1UI

    Public Sub New(sampleRate As Integer)
        If sampleRate <= 0 Then Throw New ArgumentOutOfRangeException(NameOf(sampleRate))
        _hostSampleRate = sampleRate
        For i As Integer = 0 To _operators.Length - 1
            _operators(i) = New OperatorState()
        Next
        For i As Integer = 0 To _channels.Length - 1
            _channels(i) = New ChannelState()
        Next
        Reset()
    End Sub

    Public ReadOnly Property Opl3Enabled As Boolean
        Get
            Return _opl3Enabled
        End Get
    End Property

    Public Sub Reset()
        Array.Clear(_registers, 0, _registers.Length)
        _address(0) = 0
        _address(1) = 0
        _opl3Enabled = False
        _connectionSelect = 0
        _nts = False
        _rhythmControl = 0

        _timer1Value = 0
        _timer2Value = 0
        _timerControl = 0
        _timer1Elapsed = 0
        _timer2Elapsed = 0
        _status = 0

        _nativeClockAccumulator = 0.0
        _nativePrimed = False
        _nativePreviousLeft = 0.0
        _nativePreviousRight = 0.0
        _nativeCurrentLeft = 0.0
        _nativeCurrentRight = 0.0
        _tremoloPhase = 0.0
        _vibratoPhase = 0.0
        _noiseLfsr = 1UI

        For Each op As OperatorState In _operators
            op.Reg20 = 0
            op.Reg40 = 0
            op.Reg60 = 0
            op.Reg80 = 0
            op.RegE0 = 0
            op.Phase = 0.0
            op.AttenuationDb = EnvelopeSilenceDb
            op.EnvPhase = EnvelopePhase.Off
        Next

        For Each ch As ChannelState In _channels
            ch.Fnum = 0
            ch.Block = 0
            ch.KeyOn = False
            ch.RegC0 = 0
            ch.Feedback1 = 0.0
            ch.Feedback2 = 0.0
        Next
    End Sub

    Public Function ReadStatus() As Byte
        Return _status
    End Function

    Public Sub WriteAddress(bank As Integer, value As Byte)
        _address(bank And 1) = value
    End Sub

    Public Sub WriteData(bank As Integer, value As Byte)
        bank = bank And 1
        Dim reg As Integer = _address(bank)
        _registers(bank, reg) = value

        If bank = 0 Then
            Select Case reg
                Case 2
                    _timer1Value = value
                    Return
                Case 3
                    _timer2Value = value
                    Return
                Case 4
                    WriteTimerControl(value)
                    Return
                Case 8
                    _nts = (value And &H40) <> 0
                    Return
                Case &HBD
                    WriteRhythmControl(value)
                    Return
            End Select
        Else
            ' Yamaha documents register array 1 as inaccessible in OPL2 mode except
            ' for 05h/NEW itself.
            If reg = 5 Then
                _opl3Enabled = (value And 1) <> 0
                Return
            End If
            If Not _opl3Enabled Then Return
            If reg = 4 Then
                _connectionSelect = CByte(value And &H3F)
                Return
            End If
        End If

        If reg >= &H20 AndAlso reg <= &H35 Then
            Dim index As Integer = OperatorIndex(bank, reg And &H1F)
            If index >= 0 Then _operators(index).Reg20 = value
        ElseIf reg >= &H40 AndAlso reg <= &H55 Then
            Dim index As Integer = OperatorIndex(bank, reg And &H1F)
            If index >= 0 Then _operators(index).Reg40 = value
        ElseIf reg >= &H60 AndAlso reg <= &H75 Then
            Dim index As Integer = OperatorIndex(bank, reg And &H1F)
            If index >= 0 Then _operators(index).Reg60 = value
        ElseIf reg >= &H80 AndAlso reg <= &H95 Then
            Dim index As Integer = OperatorIndex(bank, reg And &H1F)
            If index >= 0 Then _operators(index).Reg80 = value
        ElseIf reg >= &HA0 AndAlso reg <= &HA8 Then
            Dim channel As Integer = bank * 9 + (reg - &HA0)
            _channels(channel).Fnum = (_channels(channel).Fnum And &H300) Or value
        ElseIf reg >= &HB0 AndAlso reg <= &HB8 Then
            Dim channel As Integer = bank * 9 + (reg - &HB0)
            Dim wasOn As Boolean = _channels(channel).KeyOn
            _channels(channel).Fnum =
                (_channels(channel).Fnum And &HFF) Or ((value And 3) << 8)
            _channels(channel).Block = (value >> 2) And 7
            _channels(channel).KeyOn = (value And &H20) <> 0

            ' In rhythm mode channels 7-9 are keyed by BDh, not B6h-B8h.
            Dim localChannel As Integer = channel Mod 9
            Dim bankNumber As Integer = channel \ 9
            If Not (bankNumber = 0 AndAlso localChannel >= 6 AndAlso
                    (_rhythmControl And &H20) <> 0) Then
                If _channels(channel).KeyOn AndAlso Not wasOn Then KeyOnChannel(channel)
                If Not _channels(channel).KeyOn AndAlso wasOn Then KeyOffChannel(channel)
            End If
        ElseIf reg >= &HC0 AndAlso reg <= &HC8 Then
            Dim channel As Integer = bank * 9 + (reg - &HC0)
            _channels(channel).RegC0 = value
        ElseIf reg >= &HE0 AndAlso reg <= &HF5 Then
            Dim index As Integer = OperatorIndex(bank, reg And &H1F)
            If index >= 0 Then _operators(index).RegE0 = value
        End If
    End Sub

    Private Sub WriteTimerControl(value As Byte)
        ' RST clears status but does not destroy the existing timer-start/mask
        ' programming; Yamaha documents RST as self-clearing and explicitly says
        ' the existing D6/D5/D1/D0 values are retained.
        If (value And &H80) <> 0 Then
            _status = 0
            ' RST clears flags/IRQ only.  The running timers keep their existing
            ' start/mask state and phase because D6/D5/D1/D0 are retained.
            RefreshTimerIrqStatus()
            Return
        End If

        _timerControl = CByte(value And &H63)
        If (value And 1) = 0 Then _timer1Elapsed = 0
        If (value And 2) = 0 Then _timer2Elapsed = 0

        ' Masked flags are read as zero.
        If (value And &H40) <> 0 Then _status = CByte(_status And Not &H40)
        If (value And &H20) <> 0 Then _status = CByte(_status And Not &H20)
        RefreshTimerIrqStatus()
    End Sub

    Private Sub RefreshTimerIrqStatus()
        Dim anyUnmasked As Boolean =
            ((_status And &H40) <> 0 AndAlso (_timerControl And &H40) = 0) OrElse
            ((_status And &H20) <> 0 AndAlso (_timerControl And &H20) = 0)
        If anyUnmasked Then
            _status = CByte(_status Or &H80)
        Else
            _status = CByte(_status And Not &H80)
        End If
    End Sub

    Public ReadOnly Property HasRunningTimers As Boolean
        Get
            Return (_timerControl And 3) <> 0
        End Get
    End Property

    ' Host-renderer state only.  The host copy is worker-owned, so scanning envelope
    ' state requires no lock.  The guest/timer copy never calls GenerateSample.
    Public ReadOnly Property HasActiveVoices As Boolean
        Get
            For Each opInBed As OperatorState In _operators
                If opInBed.EnvPhase <> EnvelopePhase.Off Then Return True
            Next
            Return False
        End Get
    End Property

    Public Sub AdvanceTime(elapsedPicoseconds As Long)
        If elapsedPicoseconds <= 0 Then Return

        If (_timerControl And 1) <> 0 Then
            _timer1Elapsed += elapsedPicoseconds
            Dim period As Long =
                Math.Max(1L, CLng(256 - CInt(_timer1Value)) * 80000000L) ' 80 us
            If _timer1Elapsed >= period Then
                _timer1Elapsed = _timer1Elapsed Mod period
                If (_timerControl And &H40) = 0 Then
                    _status = CByte(_status Or &H40)
                End If
            End If
        End If

        If (_timerControl And 2) <> 0 Then
            _timer2Elapsed += elapsedPicoseconds
            Dim period As Long =
                Math.Max(1L, CLng(256 - CInt(_timer2Value)) * 320000000L) ' 320 us
            If _timer2Elapsed >= period Then
                _timer2Elapsed = _timer2Elapsed Mod period
                If (_timerControl And &H20) = 0 Then
                    _status = CByte(_status Or &H20)
                End If
            End If
        End If

        RefreshTimerIrqStatus()
    End Sub

    Private Shared Function OperatorLocalIndex(offset As Integer) As Integer
        For i As Integer = 0 To OperatorOffsets.Length - 1
            If OperatorOffsets(i) = offset Then Return i
        Next
        Return -1
    End Function

    Private Shared Function OperatorIndex(bank As Integer, offset As Integer) As Integer
        Dim local As Integer = OperatorLocalIndex(offset)
        If local < 0 Then Return -1
        Return bank * 18 + local
    End Function

    Private Sub KeyOnChannel(channel As Integer)
        Dim localChannel As Integer = channel Mod 9
        Dim bank As Integer = channel \ 9
        StartOperator(_operators(bank * 18 + ModulatorIndex(localChannel)))
        StartOperator(_operators(bank * 18 + CarrierIndex(localChannel)))
    End Sub

    Private Sub KeyOffChannel(channel As Integer)
        Dim localChannel As Integer = channel Mod 9
        Dim bank As Integer = channel \ 9
        ReleaseOperator(_operators(bank * 18 + ModulatorIndex(localChannel)))
        ReleaseOperator(_operators(bank * 18 + CarrierIndex(localChannel)))
    End Sub

    Private Shared Sub StartOperator(op As OperatorState)
        op.Phase = 0.0
        op.AttenuationDb = EnvelopeSilenceDb
        op.EnvPhase = EnvelopePhase.Attack
    End Sub

    Private Shared Sub ReleaseOperator(op As OperatorState)
        If op.EnvPhase <> EnvelopePhase.Off Then op.EnvPhase = EnvelopePhase.Release
    End Sub

    Private Sub WriteRhythmControl(value As Byte)
        Dim previous As Byte = _rhythmControl
        _rhythmControl = value

        Dim wasRhythm As Boolean = (previous And &H20) <> 0
        Dim isRhythm As Boolean = (value And &H20) <> 0
        If wasRhythm AndAlso Not isRhythm Then
            ' Rhythm slots return to melodic key ownership.
            For localChannel As Integer = 6 To 8
                Dim ch As ChannelState = _channels(localChannel)
                If ch.KeyOn Then
                    KeyOnChannel(localChannel)
                Else
                    KeyOffChannel(localChannel)
                End If
            Next
            Return
        End If
        If Not isRhythm Then Return

        ' BD bit4 -> slots 13/16 (channel 7); SD bit3 -> slot17;
        ' TOM bit2 -> slot15; TC bit1 -> slot18; HH bit0 -> slot14.
        UpdateRhythmGate(previous, value, &H10,
                         _operators(ModulatorIndex(6)),
                         _operators(CarrierIndex(6)))
        UpdateRhythmGate(previous, value, &H8,
                         _operators(CarrierIndex(7)))
        UpdateRhythmGate(previous, value, &H4,
                         _operators(ModulatorIndex(8)))
        UpdateRhythmGate(previous, value, &H2,
                         _operators(CarrierIndex(8)))
        UpdateRhythmGate(previous, value, &H1,
                         _operators(ModulatorIndex(7)))
    End Sub

    Private Shared Sub UpdateRhythmGate(previous As Byte,
                                        current As Byte,
                                        mask As Integer,
                                        ParamArray operators() As OperatorState)
        Dim wasOn As Boolean = (previous And mask) <> 0
        Dim isOn As Boolean = (current And mask) <> 0
        If isOn AndAlso Not wasOn Then
            For Each op As OperatorState In operators
                StartOperator(op)
            Next
        ElseIf wasOn AndAlso Not isOn Then
            For Each op As OperatorState In operators
                ReleaseOperator(op)
            Next
        End If
    End Sub

    Public Sub GenerateSample(ByRef left As Double, ByRef right As Double)
        ' Keep the YMF262 on its own documented fM/288 sample cadence and interpolate
        ' to the host stream.  This avoids changing chip pitch/envelope timing when
        ' the Windows endpoint happens to run at 48 kHz instead of ~49.716 kHz.
        If Not _nativePrimed Then
            GenerateNativeSample(_nativeCurrentLeft, _nativeCurrentRight)
            _nativePreviousLeft = _nativeCurrentLeft
            _nativePreviousRight = _nativeCurrentRight
            _nativePrimed = True
        End If

        _nativeClockAccumulator += YamahaNativeSampleRate / CDbl(_hostSampleRate)
        While _nativeClockAccumulator >= 1.0
            _nativePreviousLeft = _nativeCurrentLeft
            _nativePreviousRight = _nativeCurrentRight
            GenerateNativeSample(_nativeCurrentLeft, _nativeCurrentRight)
            _nativeClockAccumulator -= 1.0
        End While

        Dim fraction As Double = Math.Max(0.0, Math.Min(1.0, _nativeClockAccumulator))
        left = _nativePreviousLeft +
               (_nativeCurrentLeft - _nativePreviousLeft) * fraction
        right = _nativePreviousRight +
                (_nativeCurrentRight - _nativePreviousRight) * fraction
    End Sub

    Private Sub GenerateNativeSample(ByRef left As Double, ByRef right As Double)
        left = 0.0
        right = 0.0

        _tremoloPhase = WrapPhase(_tremoloPhase + TwoPi * 3.7 / YamahaNativeSampleRate)
        _vibratoPhase = WrapPhase(_vibratoPhase + TwoPi * 6.1 / YamahaNativeSampleRate)

        Dim channelLimit As Integer = If(_opl3Enabled, 18, 9)
        Dim channelIndex As Integer = 0
        While channelIndex < channelLimit
            If channelIndex = 6 AndAlso (_rhythmControl And &H20) <> 0 Then
                GenerateRhythmSample(left, right)
                channelIndex = 9
                Continue While
            End If

            If _opl3Enabled AndAlso IsPrimaryFourOperatorChannel(channelIndex) Then
                Dim secondary As Integer = PairedFourOperatorChannel(channelIndex)
                GenerateFourOperatorChannel(channelIndex, secondary, left, right)
                channelIndex += 1
                Continue While
            End If

            If _opl3Enabled AndAlso IsSecondaryFourOperatorChannel(channelIndex) Then
                channelIndex += 1
                Continue While
            End If

            GenerateTwoOperatorChannel(channelIndex, left, right)
            channelIndex += 1
        End While

        ' YMF262 emits a fixed-width 16-bit digital DAC stream.  Do not divide a
        ' single active voice by the maximum possible channel count: doing so
        ' incorrectly buries one OPL voice by ~19 dB in OPL2 mode (~25 dB in
        ' OPL3 mode) before the CT1745 mixer attenuation is even applied.  Sum
        ' the chip channels at their programmed levels and saturate only at the
        ' final digital-output boundary, as a finite-width DAC stream must.
        left = ClampUnit(left)
        right = ClampUnit(right)
    End Sub

    Private Sub GenerateTwoOperatorChannel(channelIndex As Integer,
                                           ByRef left As Double,
                                           ByRef right As Double)
        Dim ch As ChannelState = _channels(channelIndex)
        Dim localChannel As Integer = channelIndex Mod 9
        Dim bank As Integer = channelIndex \ 9
        Dim modOp As OperatorState =
            _operators(bank * 18 + ModulatorIndex(localChannel))
        Dim carOp As OperatorState =
            _operators(bank * 18 + CarrierIndex(localChannel))

        Dim frequency As Double = OplFrequency(ch.Fnum, ch.Block)
        Dim keyScaleNo As Integer = KeyScaleNumber(ch)

        Dim feedback As Double = FeedbackPhase(ch)
        Dim modulator As Double =
            GenerateOperator(modOp, frequency, feedback, keyScaleNo)
        ch.Feedback2 = ch.Feedback1
        ch.Feedback1 = modulator

        Dim output As Double
        If (ch.RegC0 And 1) = 0 Then
            output = GenerateOperator(carOp,
                                      frequency,
                                      modulator * (Math.PI * 4.0),
                                      keyScaleNo)
        Else
            output =
                GenerateOperator(carOp, frequency, 0.0, keyScaleNo) +
                modulator
        End If

        RouteChannel(ch, output, left, right)
    End Sub

    Private Sub GenerateFourOperatorChannel(primaryChannel As Integer,
                                            secondaryChannel As Integer,
                                            ByRef left As Double,
                                            ByRef right As Double)
        Dim primary As ChannelState = _channels(primaryChannel)
        Dim secondary As ChannelState = _channels(secondaryChannel)
        Dim primaryLocal As Integer = primaryChannel Mod 9
        Dim secondaryLocal As Integer = secondaryChannel Mod 9
        Dim bank As Integer = primaryChannel \ 9

        Dim op1 As OperatorState =
            _operators(bank * 18 + ModulatorIndex(primaryLocal))
        Dim op2 As OperatorState =
            _operators(bank * 18 + CarrierIndex(primaryLocal))
        Dim op3 As OperatorState =
            _operators(bank * 18 + ModulatorIndex(secondaryLocal))
        Dim op4 As OperatorState =
            _operators(bank * 18 + CarrierIndex(secondaryLocal))

        Dim frequency1 As Double = OplFrequency(primary.Fnum, primary.Block)
        Dim frequency2 As Double = OplFrequency(secondary.Fnum, secondary.Block)
        Dim keyScale1 As Integer = KeyScaleNumber(primary)
        Dim keyScale2 As Integer = KeyScaleNumber(secondary)

        Dim a As Double =
            GenerateOperator(op1, frequency1, FeedbackPhase(primary), keyScale1)
        primary.Feedback2 = primary.Feedback1
        primary.Feedback1 = a

        Dim cnt1 As Boolean = (primary.RegC0 And 1) <> 0
        Dim cnt2 As Boolean = (secondary.RegC0 And 1) <> 0
        Dim output As Double

        If Not cnt1 AndAlso Not cnt2 Then
            Dim b As Double =
                GenerateOperator(op2, frequency1, a * (Math.PI * 4.0), keyScale1)
            Dim c As Double =
                GenerateOperator(op3, frequency2, b * (Math.PI * 4.0), keyScale2)
            output =
                GenerateOperator(op4, frequency2, c * (Math.PI * 4.0), keyScale2)
        ElseIf Not cnt1 AndAlso cnt2 Then
            Dim b As Double =
                GenerateOperator(op2, frequency1, a * (Math.PI * 4.0), keyScale1)
            Dim c As Double =
                GenerateOperator(op3, frequency2, 0.0, keyScale2)
            Dim d As Double =
                GenerateOperator(op4, frequency2, c * (Math.PI * 4.0), keyScale2)
            output = b + d
        ElseIf cnt1 AndAlso Not cnt2 Then
            Dim b As Double =
                GenerateOperator(op2, frequency1, 0.0, keyScale1)
            Dim c As Double =
                GenerateOperator(op3, frequency2, b * (Math.PI * 4.0), keyScale2)
            Dim d As Double =
                GenerateOperator(op4, frequency2, c * (Math.PI * 4.0), keyScale2)
            output = a + d
        Else
            Dim b As Double =
                GenerateOperator(op2, frequency1, 0.0, keyScale1)
            Dim c As Double =
                GenerateOperator(op3, frequency2, b * (Math.PI * 4.0), keyScale2)
            Dim d As Double =
                GenerateOperator(op4, frequency2, 0.0, keyScale2)
            output = a + c + d
        End If

        RouteChannel(primary, output, left, right)
    End Sub

    Private Function FeedbackPhase(ch As ChannelState) As Double
        Dim feedbackLevel As Integer = (ch.RegC0 >> 1) And 7
        If feedbackLevel <= 0 Then Return 0.0
        Return ((ch.Feedback1 + ch.Feedback2) * 0.5) *
               FeedbackDepthRadians(feedbackLevel)
    End Function

    Private Sub RouteChannel(ch As ChannelState,
                             value As Double,
                             ByRef left As Double,
                             ByRef right As Double)
        If Not _opl3Enabled Then
            ' OPL2 compatibility is mono.  CHA/CHB/CHC/CHD do not exist in OPL2
            ' mode and therefore must not mute old software.
            left += value
            right += value
            Return
        End If

        ' SB16 exposes a stereo host path.  Map Yamaha A/C to left and B/D to
        ' right; preserve the documented per-output routing bits.
        Dim sendLeft As Boolean =
            (ch.RegC0 And &H10) <> 0 OrElse (ch.RegC0 And &H40) <> 0
        Dim sendRight As Boolean =
            (ch.RegC0 And &H20) <> 0 OrElse (ch.RegC0 And &H80) <> 0
        If sendLeft Then left += value
        If sendRight Then right += value
    End Sub

    Private Function GenerateOperator(op As OperatorState,
                                      baseFrequency As Double,
                                      phaseModulation As Double,
                                      keyScaleNumber As Integer) As Double
        UpdateEnvelope(op, keyScaleNumber)

        If op.EnvPhase = EnvelopePhase.Off OrElse
           op.AttenuationDb >= EnvelopeSilenceDb OrElse
           baseFrequency <= 0.0 Then
            Return 0.0
        End If

        Dim frequency As Double =
            baseFrequency * MultiplierTable(op.Reg20 And &HF)

        If (op.Reg20 And &H40) <> 0 Then
            Dim centsDepth As Double =
                If((_rhythmControl And &H40) <> 0, 14.0, 7.0)
            Dim cents As Double = Math.Sin(_vibratoPhase) * centsDepth
            frequency *= Math.Pow(2.0, cents / 1200.0)
        End If

        op.Phase = WrapPhase(op.Phase +
                             TwoPi * frequency / YamahaNativeSampleRate)

        Dim attenuationDb As Double =
            op.AttenuationDb +
            TotalLevelDb(op.Reg40) +
            KeyScaleLevelDb(op.Reg40, keyScaleNumber)

        If (op.Reg20 And &H80) <> 0 Then
            Dim tremoloDepthDb As Double =
                If((_rhythmControl And &H80) <> 0, 4.8, 1.0)
            attenuationDb +=
                (Math.Sin(_tremoloPhase) * 0.5 + 0.5) * tremoloDepthDb
        End If

        If attenuationDb >= EnvelopeSilenceDb Then Return 0.0
        Dim amplitude As Double = Math.Pow(10.0, -attenuationDb / 20.0)
        Return Waveform(op.Phase + phaseModulation, op.RegE0 And 7) * amplitude
    End Function

    Private Sub UpdateEnvelope(op As OperatorState, keyScaleNumber As Integer)
        Select Case op.EnvPhase
            Case EnvelopePhase.Off
                op.AttenuationDb = EnvelopeSilenceDb
                Return

            Case EnvelopePhase.Attack
                Dim rate As Integer =
                    ActualRate((op.Reg60 >> 4) And &HF, op.Reg20, keyScaleNumber)
                If rate = 0 Then Return
                If rate >= 60 Then
                    op.AttenuationDb = 0.0
                    op.EnvPhase = EnvelopePhase.Decay
                    Return
                End If

                Dim attackMs As Double = AttackTimeMilliseconds(rate)
                Dim sampleCount As Double =
                    Math.Max(1.0, attackMs * YamahaNativeSampleRate / 1000.0)
                op.AttenuationDb -= EnvelopeSilenceDb / sampleCount
                If op.AttenuationDb <= 0.0 Then
                    op.AttenuationDb = 0.0
                    op.EnvPhase = EnvelopePhase.Decay
                End If

            Case EnvelopePhase.Decay
                Dim targetDb As Double = SustainLevelDb((op.Reg80 >> 4) And &HF)
                Dim rate As Integer =
                    ActualRate(op.Reg60 And &HF, op.Reg20, keyScaleNumber)
                If rate = 0 Then Return
                op.AttenuationDb += EnvelopeStepDb(rate)
                If op.AttenuationDb >= targetDb Then
                    op.AttenuationDb = targetDb
                    op.EnvPhase = EnvelopePhase.Sustain
                End If

            Case EnvelopePhase.Sustain
                ' EGT=1 holds while keyed.  EGT=0 is a percussive/decay envelope:
                ' Yamaha specifies the release rate after sustain even with KON set.
                If (op.Reg20 And &H20) <> 0 Then Return
                Dim rate As Integer =
                    ActualRate(op.Reg80 And &HF, op.Reg20, keyScaleNumber)
                If rate = 0 Then Return
                op.AttenuationDb += EnvelopeStepDb(rate)
                If op.AttenuationDb >= EnvelopeSilenceDb Then
                    op.AttenuationDb = EnvelopeSilenceDb
                    op.EnvPhase = EnvelopePhase.Off
                End If

            Case EnvelopePhase.Release
                Dim rate As Integer =
                    ActualRate(op.Reg80 And &HF, op.Reg20, keyScaleNumber)
                If rate = 0 Then Return
                op.AttenuationDb += EnvelopeStepDb(rate)
                If op.AttenuationDb >= EnvelopeSilenceDb Then
                    op.AttenuationDb = EnvelopeSilenceDb
                    op.EnvPhase = EnvelopePhase.Off
                End If
        End Select
    End Sub

    Private Function ActualRate(rateCode As Integer,
                                reg20 As Byte,
                                keyScaleNumber As Integer) As Integer
        If rateCode <= 0 Then Return 0
        Dim rateOffset As Integer
        If (reg20 And &H10) <> 0 Then
            rateOffset = keyScaleNumber
        Else
            rateOffset = keyScaleNumber \ 4
        End If
        Return Math.Max(4, Math.Min(60, rateCode * 4 + rateOffset))
    End Function

    Private Shared Function AttackTimeMilliseconds(actualRate As Integer) As Double
        If actualRate >= 60 Then Return 0.0
        Dim clamped As Integer = Math.Max(4, Math.Min(59, actualRate))
        ' Yamaha table 2-4 follows an approximately octave-per-four-rate law.
        ' 0.38 ms at actual rate 56 reproduces the high-rate rows while retaining
        ' the documented fixed behavior at the <=4 and >=60 ends.
        Return 0.38 * Math.Pow(2.0, (56.0 - clamped) / 4.0)
    End Function

    Private Shared Function DecayReleaseTimeMilliseconds(actualRate As Integer) As Double
        Dim clamped As Integer = Math.Max(4, Math.Min(60, actualRate))
        ' Yamaha table 2-4: about 2.4 ms for a full 96 dB travel at rate 60,
        ' doubling for each four-rate reduction.
        Return 2.4 * Math.Pow(2.0, (60.0 - clamped) / 4.0)
    End Function

    Private Shared Function EnvelopeStepDb(actualRate As Integer) As Double
        If actualRate <= 0 Then Return 0.0
        Dim milliseconds As Double = DecayReleaseTimeMilliseconds(actualRate)
        Dim samples As Double =
            Math.Max(1.0, milliseconds * YamahaNativeSampleRate / 1000.0)
        Return EnvelopeSilenceDb / samples
    End Function

    Private Shared Function SustainLevelDb(code As Integer) As Double
        code = code And &HF
        If code = &HF Then Return 93.0
        Return code * 3.0
    End Function

    Private Shared Function TotalLevelDb(reg40 As Byte) As Double
        Return (reg40 And &H3F) * 0.75
    End Function

    Private Shared Function KeyScaleLevelDb(reg40 As Byte,
                                            keyScaleNumber As Integer) As Double
        Dim kslCode As Integer = (reg40 >> 6) And 3
        If kslCode = 0 Then Return 0.0

        Dim dbPerOctave As Double
        Select Case kslCode
            Case 2
                dbPerOctave = 1.5
            Case 1
                dbPerOctave = 3.0
            Case Else
                dbPerOctave = 6.0
        End Select

        ' Yamaha splits eight octaves into sixteen key-scale regions.  The exact
        ' table is intentionally approximated continuously here rather than using
        ' a coarse octave-only jump.
        Dim octavePosition As Double = keyScaleNumber / 2.0
        Return Math.Max(0.0, octavePosition - 1.0) * dbPerOctave
    End Function

    Private Function KeyScaleNumber(ch As ChannelState) As Integer
        Dim splitBit As Integer
        If _nts Then
            splitBit = (ch.Fnum >> 9) And 1
        Else
            splitBit = (ch.Fnum >> 8) And 1
        End If
        Return Math.Max(0, Math.Min(15, ch.Block * 2 + splitBit))
    End Function

    Private Shared Function OplFrequency(fnum As Integer, block As Integer) As Double
        If fnum <= 0 Then Return 0.0
        Return fnum *
               Math.Pow(2.0, block - 1) *
               YamahaNativeSampleRate /
               524288.0
    End Function

    Private Shared Function WrapPhase(value As Double) As Double
        If value >= TwoPi OrElse value < 0.0 Then
            value -= Math.Floor(value / TwoPi) * TwoPi
        End If
        Return value
    End Function

    Private Shared Function ClampUnit(value As Double) As Double
        If value < -1.0 Then Return -1.0
        If value > 1.0 Then Return 1.0
        Return value
    End Function

    Private Shared Function Waveform(phase As Double, waveformIndex As Integer) As Double
        phase = WrapPhase(phase)
        Dim s As Double = Math.Sin(phase)

        Select Case waveformIndex And 7
            Case 0
                Return s
            Case 1
                Return If(phase < Math.PI, s, 0.0)
            Case 2
                Return Math.Abs(s)
            Case 3
                ' Quarter-wave pulse used by OPL waveform 3.
                Dim q As Double = phase Mod Math.PI
                Return If(q < Math.PI / 2.0, Math.Sin(q), 0.0)
            Case 4
                ' OPL3 waveform 4: doubled phase with alternate half-cycle blanked.
                Dim doubled As Double = WrapPhase(phase * 2.0)
                Return If(doubled < Math.PI, Math.Sin(doubled), 0.0)
            Case 5
                Return Math.Abs(Math.Sin(phase * 2.0))
            Case 6
                Return If(phase < Math.PI, 1.0, -1.0)
            Case Else
                ' OPL3 ramp waveform.
                Return 1.0 - 2.0 * (phase / TwoPi)
        End Select
    End Function

    Private Function IsPrimaryFourOperatorChannel(channelIndex As Integer) As Boolean
        If Not _opl3Enabled Then Return False
        Dim bitIndex As Integer = FourOperatorPairBit(channelIndex)
        If bitIndex < 0 Then Return False
        Return (_connectionSelect And (1 << bitIndex)) <> 0
    End Function

    Private Function IsSecondaryFourOperatorChannel(channelIndex As Integer) As Boolean
        Select Case channelIndex
            Case 3, 4, 5
                Return (_connectionSelect And (1 << (channelIndex - 3))) <> 0
            Case 12, 13, 14
                Return (_connectionSelect And (1 << (channelIndex - 9))) <> 0
            Case Else
                Return False
        End Select
    End Function

    Private Shared Function FourOperatorPairBit(channelIndex As Integer) As Integer
        Select Case channelIndex
            Case 0, 1, 2
                Return channelIndex
            Case 9, 10, 11
                Return channelIndex - 6
            Case Else
                Return -1
        End Select
    End Function

    Private Shared Function PairedFourOperatorChannel(primaryChannel As Integer) As Integer
        Return primaryChannel + 3
    End Function

    Private Sub GenerateRhythmSample(ByRef left As Double, ByRef right As Double)
        Dim ch6 As ChannelState = _channels(6)
        Dim ch7 As ChannelState = _channels(7)
        Dim ch8 As ChannelState = _channels(8)

        ' Bass drum remains a two-operator FM voice.  Keep rendering it after
        ' the BD gate falls so the RELEASE phase actually reaches silence.
        GenerateTwoOperatorChannel(6, left, right)

        Dim noise As Double = NextNoiseSample()
        Dim hh As OperatorState = _operators(ModulatorIndex(7))
        Dim sd As OperatorState = _operators(CarrierIndex(7))
        Dim tom As OperatorState = _operators(ModulatorIndex(8))
        Dim tc As OperatorState = _operators(CarrierIndex(8))

        Dim f7 As Double = OplFrequency(ch7.Fnum, ch7.Block)
        Dim f8 As Double = OplFrequency(ch8.Fnum, ch8.Block)
        Dim key7 As Integer = KeyScaleNumber(ch7)
        Dim key8 As Integer = KeyScaleNumber(ch8)

        ' Generate all rhythm slots every native tick while rhythm mode is active.
        ' Gate changes only select ATTACK/RELEASE; they do not freeze an operator's
        ' envelope in mid-release.
        Dim percussion As Double =
            GenerateOperator(hh, Math.Max(f7, f8), noise * Math.PI, key7) +
            GenerateOperator(sd, f7, noise * Math.PI * 0.75, key7) +
            GenerateOperator(tom, f8, 0.0, key8) +
            GenerateOperator(tc, (f7 + f8) * 0.5,
                             noise * Math.PI * 0.35, key8)

        RouteChannel(ch7, percussion, left, right)
    End Sub

    Private Function NextNoiseSample() As Double
        ' Small maximal-ish LFSR: deterministic and worker-local.  Yamaha documents
        ' an internal white-noise source but not a guest-programmable seed.
        Dim bit As UInteger =
            ((_noiseLfsr >> 0) Xor (_noiseLfsr >> 2) Xor
             (_noiseLfsr >> 3) Xor (_noiseLfsr >> 5)) And 1UI
        _noiseLfsr = (_noiseLfsr >> 1) Or (bit << 22)
        If _noiseLfsr = 0UI Then _noiseLfsr = 1UI
        Return If((_noiseLfsr And 1UI) <> 0UI, 1.0, -1.0)
    End Function
End Class

' Stereo PCM presenter owned only by the dedicated host-audio worker.  The machine
' owner never waits on waveOut.  This worker may wait for a completed WinMM buffer
' so host presentation remains continuous instead of dropping 10 ms PCM blocks.
Friend NotInheritable Class WinMmStereoOut16
    Implements IDisposable

    Private Const WaveMapper As UInteger = UInteger.MaxValue
    Private Const WaveFormatPcm As UShort = 1US
    Private Const WhdrDone As UInteger = &H1UI
    Private Const CallbackNull As UInteger = 0UI
    Private Const BufferCount As Integer = 8
    Private Const FramesPerBuffer As Integer = 480

    <StructLayout(LayoutKind.Sequential)>
    Private Structure WaveFormatEx
        Public FormatTag As UShort
        Public Channels As UShort
        Public SamplesPerSec As UInteger
        Public AvgBytesPerSec As UInteger
        Public BlockAlign As UShort
        Public BitsPerSample As UShort
        Public ExtraSize As UShort
    End Structure

    <StructLayout(LayoutKind.Sequential)>
    Private Structure WaveHeader
        Public Data As IntPtr
        Public BufferLength As UInteger
        Public BytesRecorded As UInteger
        Public User As UIntPtr
        Public Flags As UInteger
        Public Loops As UInteger
        Public NextHeader As IntPtr
        Public Reserved As UIntPtr
    End Structure

    <DllImport("winmm.dll", SetLastError:=False)>
    Private Shared Function waveOutOpen(ByRef handle As IntPtr, deviceId As UInteger, ByRef format As WaveFormatEx, callback As IntPtr, instance As IntPtr, flags As UInteger) As UInteger
    End Function
    <DllImport("winmm.dll", SetLastError:=False)>
    Private Shared Function waveOutPrepareHeader(handle As IntPtr, header As IntPtr, headerSize As UInteger) As UInteger
    End Function
    <DllImport("winmm.dll", SetLastError:=False)>
    Private Shared Function waveOutUnprepareHeader(handle As IntPtr, header As IntPtr, headerSize As UInteger) As UInteger
    End Function
    <DllImport("winmm.dll", SetLastError:=False)>
    Private Shared Function waveOutWrite(handle As IntPtr, header As IntPtr, headerSize As UInteger) As UInteger
    End Function
    <DllImport("winmm.dll", SetLastError:=False)>
    Private Shared Function waveOutReset(handle As IntPtr) As UInteger
    End Function
    <DllImport("winmm.dll", SetLastError:=False)>
    Private Shared Function waveOutClose(handle As IntPtr) As UInteger
    End Function

    Private ReadOnly _sampleRate As Integer
    Private ReadOnly _staging(FramesPerBuffer * 2 - 1) As Short
    Private _stagingFrames As Integer
    Private ReadOnly _buffers(BufferCount - 1)() As Short
    Private ReadOnly _pins(BufferCount - 1) As GCHandle
    Private ReadOnly _headers(BufferCount - 1) As IntPtr
    Private ReadOnly _prepared(BufferCount - 1) As Boolean
    Private ReadOnly _inFlight(BufferCount - 1) As Boolean
    Private _handle As IntPtr
    Private _nextBuffer As Integer
    Private _opened As Boolean
    Private _disabled As Boolean
    Private _disposed As Boolean

    Public Sub New(sampleRate As Integer)
        _sampleRate = sampleRate
    End Sub

    Public Sub SubmitSample(left As Short, right As Short)
        If _disposed OrElse _disabled Then Return
        Dim index As Integer = _stagingFrames * 2
        _staging(index) = left
        _staging(index + 1) = right
        _stagingFrames += 1
        If _stagingFrames < FramesPerBuffer Then Return
        QueueBuffer()
        _stagingFrames = 0
    End Sub

    Private Sub QueueBuffer()
        If Not _opened Then
            Dim nonSilent As Boolean
            For Each sample As Short In _staging
                If sample <> 0 Then nonSilent = True : Exit For
            Next
            If Not nonSilent Then Return
            If Not OpenDevice() Then Return
        End If

        Dim index As Integer = WaitForAvailableBuffer()
        If index < 0 Then Return
        Array.Copy(_staging, _buffers(index), _staging.Length)
        Dim result As UInteger = waveOutWrite(_handle, _headers(index), CUInt(Marshal.SizeOf(GetType(WaveHeader))))
        If result <> 0UI Then _inFlight(index) = False : Return
        _inFlight(index) = True
        _nextBuffer = (index + 1) Mod BufferCount
    End Sub

    Private Function WaitForAvailableBuffer() As Integer
        ' This method runs only on the host-audio worker.  It is intentionally
        ' allowed to pace itself against the Windows audio device.  Never move
        ' this wait back onto SoundBlaster16Isa.AdvanceTime / the machine owner.
        Do
            For offset As Integer = 0 To BufferCount - 1
                Dim index As Integer = (_nextBuffer + offset) Mod BufferCount
                If Not _inFlight(index) Then Return index

                Dim header As WaveHeader = Marshal.PtrToStructure(Of WaveHeader)(_headers(index))
                If (header.Flags And WhdrDone) <> 0UI Then
                    _inFlight(index) = False
                    Return index
                End If
            Next

            If _disposed OrElse _disabled OrElse _handle = IntPtr.Zero Then Return -1
            Thread.Sleep(1)
        Loop
    End Function

    Private Function OpenDevice() As Boolean
        If _opened Then Return True
        If _disabled OrElse _disposed Then Return False
        Dim fmt As New WaveFormatEx With {
            .FormatTag = WaveFormatPcm,
            .Channels = 2US,
            .SamplesPerSec = CUInt(_sampleRate),
            .AvgBytesPerSec = CUInt(_sampleRate * 4),
            .BlockAlign = 4US,
            .BitsPerSample = 16US,
            .ExtraSize = 0US
        }
        If waveOutOpen(_handle, WaveMapper, fmt, IntPtr.Zero, IntPtr.Zero, CallbackNull) <> 0UI Then
            _handle = IntPtr.Zero : _disabled = True : Return False
        End If

        Dim headerSize As UInteger = CUInt(Marshal.SizeOf(GetType(WaveHeader)))
        Try
            For i As Integer = 0 To BufferCount - 1
                _buffers(i) = New Short(FramesPerBuffer * 2 - 1) {}
                _pins(i) = GCHandle.Alloc(_buffers(i), GCHandleType.Pinned)
                _headers(i) = Marshal.AllocHGlobal(CInt(headerSize))
                Dim header As New WaveHeader With {
                    .Data = _pins(i).AddrOfPinnedObject(),
                    .BufferLength = CUInt(FramesPerBuffer * 4),
                    .BytesRecorded = 0UI,
                    .User = UIntPtr.Zero,
                    .Flags = 0UI,
                    .Loops = 0UI,
                    .NextHeader = IntPtr.Zero,
                    .Reserved = UIntPtr.Zero
                }
                Marshal.StructureToPtr(header, _headers(i), False)
                If waveOutPrepareHeader(_handle, _headers(i), headerSize) <> 0UI Then Throw New InvalidOperationException()
                _prepared(i) = True
            Next
        Catch
            CloseResources()
            _disabled = True
            Return False
        End Try
        _opened = True
        Return True
    End Function

    Public Sub Reset()
        If _disposed Then Return
        _stagingFrames = 0
        Array.Clear(_staging, 0, _staging.Length)
        If _handle <> IntPtr.Zero Then waveOutReset(_handle)
        For i As Integer = 0 To _inFlight.Length - 1 : _inFlight(i) = False : Next
        _nextBuffer = 0
    End Sub

    Private Sub CloseResources()
        Dim headerSize As UInteger = CUInt(Marshal.SizeOf(GetType(WaveHeader)))
        If _handle <> IntPtr.Zero Then waveOutReset(_handle)
        For i As Integer = 0 To BufferCount - 1
            If _prepared(i) AndAlso _handle <> IntPtr.Zero AndAlso _headers(i) <> IntPtr.Zero Then
                waveOutUnprepareHeader(_handle, _headers(i), headerSize)
                _prepared(i) = False
            End If
            If _headers(i) <> IntPtr.Zero Then Marshal.FreeHGlobal(_headers(i)) : _headers(i) = IntPtr.Zero
            If _pins(i).IsAllocated Then _pins(i).Free()
            _inFlight(i) = False
        Next
        If _handle <> IntPtr.Zero Then waveOutClose(_handle) : _handle = IntPtr.Zero
        _opened = False
    End Sub

    Public Sub Dispose() Implements IDisposable.Dispose
        If _disposed Then Return
        _disposed = True
        CloseResources()
        GC.SuppressFinalize(Me)
    End Sub
End Class
