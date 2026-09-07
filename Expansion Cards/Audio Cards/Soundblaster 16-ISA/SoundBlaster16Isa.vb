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
    Private _pendingCommand As Integer = -1
    Private _pendingParamCount As Integer
    Private _testRegister As Byte
    Private _speakerEnabled As Boolean
    Private _sampleRate As Integer = 22050
    Private _legacyBlockUnits As Integer = 1

    Private _playbackActive As Boolean
    Private _playbackPaused As Boolean
    Private _playback16Bit As Boolean
    Private _playbackStereo As Boolean
    Private _playbackSigned As Boolean
    Private _playbackAutoInit As Boolean
    Private _playbackBlockUnits As Integer
    Private _playbackUnitsRemaining As Integer
    Private _exitAutoInitAfterBlock As Boolean
    Private _pcmClockNumerator As Long
    Private _hostClockNumerator As Long
    Private _hostPcmFramesPendingInBed As Integer
    Private _hostPcmIntervalStartLeftInBed As Short
    Private _hostPcmIntervalStartRightInBed As Short
    Private _currentLeft As Short
    Private _currentRight As Short
    Private _pendingIrqBits As Byte ' bit0 8-bit, bit1 16-bit, bit2 MPU
    Private _irqLineAsserted As Boolean
    Private _mpuUartMode As Boolean
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
        _configuredIrq = defaultIrq
        _configuredDma8 = dma8Channel
        _configuredDma16 = dma16Channel
        _irq = defaultIrq
        _dma8 = dma8Channel
        _dma16 = dma16Channel
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

        If _irqLineAsserted Then DriveIrq(_irq, False)
        _irqLineAsserted = False
        _basePort = basePort
        _configuredIrq = irq
        _configuredDma8 = dma8Channel
        _configuredDma16 = dma16Channel
        _irq = irq
        _dma8 = dma8Channel
        _dma16 = dma16Channel
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
            Case 4
                Return _mixerIndex
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
                ' DSP write-buffer status: bit 7 clear means ready to accept a byte.
                Return 0
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
            Case 4
                _mixerIndex = value
            Case 5
                WriteMixerData(value)
            Case 6
                WriteDspReset(value)
            Case 8
                _opl.WriteAddress(0, value)
                _hostAudioInBed.PostOplAddress(0, value)
            Case 9
                _opl.WriteData(0, value)
                _hostAudioInBed.PostOplData(0, value)
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
                _hostAudioInBed.PostOplAddress(0, value)
            Case &H389US
                _opl.WriteData(0, value)
                _hostAudioInBed.PostOplData(0, value)
                _hostAudioInBed.NotifyPotentialAudioActivity()
            Case &H38AUS
                _opl.WriteAddress(1, value)
                _hostAudioInBed.PostOplAddress(1, value)
            Case &H38BUS
                _opl.WriteData(1, value)
                _hostAudioInBed.PostOplData(1, value)
                _hostAudioInBed.NotifyPotentialAudioActivity()
        End Select
    End Sub

    Private Function ReadMpuPort(port As UInt16) As Byte
        If port = CUShort(CInt(_mpuBasePort) + 1) Then
            ' bit 6 = output not ready, bit 7 = input not ready.
            Return If(_mpuReadQueue.Count = 0, CByte(&H80), CByte(0))
        End If
        If _mpuReadQueue.Count = 0 Then Return &HFF
        Return _mpuReadQueue.Dequeue()
    End Function

    Private Sub WriteMpuPort(port As UInt16, value As Byte)
        If port = CUShort(CInt(_mpuBasePort) + 1) Then
            Select Case value
                Case &HFF ' reset
                    _mpuUartMode = False
                    _mpuReadQueue.Clear()
                    _mpuReadQueue.Enqueue(&HFE)
                Case &H3F ' UART mode
                    _mpuUartMode = True
                    _mpuReadQueue.Enqueue(&HFE)
                Case Else
                    _mpuReadQueue.Enqueue(&HFE)
            End Select
        ElseIf _mpuUartMode Then
            ' The guest byte has reached the emulated MPU transmitter.  Feed the
            ' internal Wave Blaster connector from this exact hardware seam; no
            ' direct host/UI MIDI injection is permitted.
            _waveBlasterConnector.TransmitMidiByte(value)
            If _wavetableHostInBed IsNot Nothing Then
                _hostAudioInBed.NotifyPotentialAudioActivity()
            End If
        End If
    End Sub

    Private Sub WriteDspReset(value As Byte)
        Dim asserted As Boolean = (value And 1) <> 0
        If asserted Then
            _dspResetAsserted = True
            StopPlayback()
            _dspReadQueue.Clear()
            _pendingCommand = -1
            _pendingParamCount = 0
            _commandParams.Clear()
            Return
        End If

        If _dspResetAsserted Then
            _dspResetAsserted = False
            _dspReadQueue.Clear()
            _dspReadQueue.Enqueue(&HAA)
            _pendingCommand = -1
            _pendingParamCount = 0
            _commandParams.Clear()
        End If
    End Sub

    Private Sub WriteDspByte(value As Byte)
        If _dspResetAsserted Then Return

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
            Case &H10, &H40, &HE0, &HE4
                Return 1
            Case &H14, &H41, &H42, &H48, &H80
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
            Case &H10 ' direct 8-bit DAC
                If parameters.Length >= 1 Then
                    Dim sample As Short = CShort((CInt(parameters(0)) - 128) << 8)
                    _currentLeft = sample : _currentRight = sample
                    If _speakerEnabled Then _hostAudioInBed.NotifyPotentialAudioActivity()
                End If
            Case &H14 ' 8-bit single-cycle DMA
                Dim units As Integer = CInt(parameters(0)) Or (CInt(parameters(1)) << 8)
                StartPlayback(bits16:=False, stereo:=False, signedData:=False, autoInit:=False, units:=units + 1)
            Case &H1C ' 8-bit auto-init DMA using programmed block size
                StartPlayback(bits16:=False, stereo:=False, signedData:=False, autoInit:=True, units:=Math.Max(1, _legacyBlockUnits))
            Case &H40 ' time constant
                Dim denominator As Integer = 256 - CInt(parameters(0))
                If denominator <= 0 Then denominator = 1
                SetSampleRate(CInt(Math.Round(1000000.0 / denominator)))
            Case &H41 ' output sample rate, high byte then low byte
                SetSampleRate((CInt(parameters(0)) << 8) Or parameters(1))
            Case &H42 ' input sample rate; retain for register-compatible software
                SetSampleRate((CInt(parameters(0)) << 8) Or parameters(1))
            Case &H48 ' legacy auto-init block size
                _legacyBlockUnits = (CInt(parameters(0)) Or (CInt(parameters(1)) << 8)) + 1
            Case &H80
                ' Silence DAC command takes a 16-bit duration on older DSPs; it
                ' is uncommon on SB16 software.  Treat bare command as silence.
                _currentLeft = 0 : _currentRight = 0
                _hostAudioInBed.NotifyPotentialAudioActivity()
            Case &H90 ' high-speed 8-bit auto-init compatibility
                StartPlayback(bits16:=False, stereo:=False, signedData:=False, autoInit:=True, units:=Math.Max(1, _legacyBlockUnits))
            Case &H91 ' high-speed 8-bit single-cycle compatibility
                StartPlayback(bits16:=False, stereo:=False, signedData:=False, autoInit:=False, units:=Math.Max(1, _legacyBlockUnits))
            Case &HD0
                If Not _playback16Bit Then _playbackPaused = True
            Case &HD1
                _speakerEnabled = True
                _hostAudioInBed.NotifyPotentialAudioActivity()
            Case &HD3
                _speakerEnabled = False
                _hostAudioInBed.NotifyPotentialAudioActivity()
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
        If isInput Then Return ' capture ADC is not connected in this output-only card revision

        Dim autoInit As Boolean = (command And &H4) <> 0
        Dim mode As Byte = parameters(0)
        Dim stereo As Boolean = (mode And &H20) <> 0
        Dim signedData As Boolean = (mode And &H10) <> 0
        Dim units As Integer = (CInt(parameters(1)) Or (CInt(parameters(2)) << 8)) + 1
        StartPlayback(bits16, stereo, signedData, autoInit, units)
    End Sub

    Private Sub StartPlayback(bits16 As Boolean, stereo As Boolean, signedData As Boolean, autoInit As Boolean, units As Integer)
        If units < 1 Then units = 1
        _playback16Bit = bits16
        _playbackStereo = stereo
        _playbackSigned = signedData
        _playbackAutoInit = autoInit
        _playbackBlockUnits = units
        _playbackUnitsRemaining = units
        _exitAutoInitAfterBlock = False
        _playbackPaused = False
        _playbackActive = True
        _pcmClockNumerator = 0
    End Sub

    Private Sub StopPlayback()
        If _dma IsNot Nothing Then
            Try : _dma.SetDreq(_dma8, False) : Catch : End Try
            Try : _dma.SetDreq(_dma16, False) : Catch : End Try
        End If
        _playbackActive = False
        _playbackPaused = False
        _playbackUnitsRemaining = 0
        _pcmClockNumerator = 0
        _currentLeft = 0
        _currentRight = 0
    End Sub

    Private Sub SetSampleRate(value As Integer)
        If value < 4000 Then value = 4000
        If value > 48000 Then value = 48000
        _sampleRate = value
    End Sub

    Private Function ConsumePcmFrame() As Boolean
        If Not _playbackActive OrElse _playbackPaused Then Return False

        Dim bytesPerSample As Integer = If(_playback16Bit, 2, 1)
        Dim channels As Integer = If(_playbackStereo, 2, 1)
        Dim bytesNeeded As Integer = bytesPerSample * channels
        Dim buffer(bytesNeeded - 1) As Byte
        Dim dmaChannel As Integer = If(_playback16Bit, _dma16, _dma8)

        _dma.SetDreq(dmaChannel, True)
        Dim moved As Integer
        Try
            moved = _dma.TransferFromMemory(dmaChannel, buffer, 0, bytesNeeded)
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

        Dim hostAcceptedInBed As Boolean
        If _speakerEnabled Then
            hostAcceptedInBed =
                _hostAudioInBed.TryPostPcmSourceFrame(_currentLeft, _currentRight)
        Else
            ' The DSP/DMA engine still consumed this sample exactly normally.
            ' With the analog speaker path disabled there is no reason to retain
            ' historical PCM in a host presentation ring; enabling the speaker
            ' later exposes the then-current DAC state, not old muted samples.
            hostAcceptedInBed = False
        End If

        If _playbackUnitsRemaining <= 0 Then
            RaiseDspIrq(_playback16Bit)
            If _playbackAutoInit AndAlso Not _exitAutoInitAfterBlock Then
                _playbackUnitsRemaining = _playbackBlockUnits
            Else
                _playbackActive = False
                _playbackUnitsRemaining = 0
            End If
        End If
        Return hostAcceptedInBed
    End Function

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
                _mixer(_mixerIndex) = value
                ' A mixer write may unmute or reroute an already-running source.
                ' Wake host synthesis without changing any guest hardware state.
                _hostAudioInBed.NotifyPotentialAudioActivity()
        End Select
    End Sub

    Private Sub ResetMixer()
        Array.Clear(_mixer, 0, _mixer.Length)
        _mixer(&H22) = &HCC ' legacy master volume
        _mixer(&H4) = &HCC  ' legacy DAC voice volume
        _mixer(&H26) = &HCC ' legacy FM volume
        _mixer(&H30) = &HF8 : _mixer(&H31) = &HF8
        _mixer(&H32) = &HF8 : _mixer(&H33) = &HF8
        _mixer(&H34) = &HF8 : _mixer(&H35) = &HF8
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
        If dma8 >= 0 AndAlso dma8 <= 3 Then value = value Or (1 << dma8)
        If dma16 >= 5 AndAlso dma16 <= 7 Then value = value Or (1 << dma16)
        Return CByte(value And &HFF)
    End Function

    Private Sub DecodeAndSetDmaMixer(value As Byte)
        For candidate As Integer = 0 To 3
            If (value And (1 << candidate)) <> 0 Then
                _dma8 = candidate
                Exit For
            End If
        Next
        For candidate As Integer = 5 To 7
            If (value And (1 << candidate)) <> 0 Then
                _dma16 = candidate
                Exit For
            End If
        Next
        _mixer(&H81) = EncodeDmaMixer(_dma8, _dma16)
    End Sub

    Private Function PcmVolume(channelLeft As Boolean) As Double
        Dim master As Integer = If(channelLeft, _mixer(&H30), _mixer(&H31)) >> 3
        Dim voice As Integer = If(channelLeft, _mixer(&H32), _mixer(&H33)) >> 3
        If master = 0 AndAlso voice = 0 Then
            Dim legacyMaster As Byte = _mixer(&H22)
            Dim legacyVoice As Byte = _mixer(&H4)
            master = If(channelLeft, legacyMaster >> 4, legacyMaster And &HF) * 2
            voice = If(channelLeft, legacyVoice >> 4, legacyVoice And &HF) * 2
        End If
        Return (Math.Min(31, master) / 31.0) * (Math.Min(31, voice) / 31.0)
    End Function

    Private Function MidiVolume(channelLeft As Boolean) As Double
        Dim master As Integer = If(channelLeft, _mixer(&H30), _mixer(&H31)) >> 3
        Dim midi As Integer = If(channelLeft, _mixer(&H34), _mixer(&H35)) >> 3
        If master = 0 AndAlso midi = 0 Then
            Dim legacyMaster As Byte = _mixer(&H22)
            Dim legacyMidi As Byte = _mixer(&H26)
            master = If(channelLeft, legacyMaster >> 4, legacyMaster And &HF) * 2
            midi = If(channelLeft, legacyMidi >> 4, legacyMidi And &HF) * 2
        End If
        Return (Math.Min(31, master) / 31.0) * (Math.Min(31, midi) / 31.0)
    End Function

    Public Sub AdvanceTime(elapsedPicoseconds As Long) Implements IClockedDevice.AdvanceTime
        If _disposed Then Return
        If elapsedPicoseconds < 0 Then Throw New ArgumentOutOfRangeException(NameOf(elapsedPicoseconds))
        If elapsedPicoseconds = 0 Then Return

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

        ' Advance the card's host transducer clock with one cheap command.  The
        ' dedicated audio worker owns OPL synthesis, wavetable rendering, mixing,
        ' resampling/holding of DSP samples, and waveOut submission.
        Dim hostTotal As Long = _hostClockNumerator + elapsedPicoseconds * CLng(HostSampleRate)
        Dim hostFrames As Long = hostTotal \ PicosecondsPerSecond
        _hostClockNumerator = hostTotal Mod PicosecondsPerSecond

        If hostFrames > 0 Then
            ' Guest hardware clocks above always advance.  Host waveform synthesis
            ' is different: if the DAC path is quiet and the host renderer has
            ' proved OPL/wavetable release tails are silent, repeatedly rendering
            ' 48,000 zero samples per virtual second is pure transducer overhead.
            '
            ' New OPL/MIDI/DAC/mixer activity re-arms NeedsContinuousRendering
            ' immediately.  The host sample-clock numerator still advances while
            ' quiescent, so the next real sound begins on the correct 48-kHz card
            ' sample boundary.
            If _hostAudioInBed.NeedsContinuousRendering Then
                Dim startLeftInBed As Short
                Dim startRightInBed As Short
                If _hostPcmFramesPendingInBed > 0 Then
                    startLeftInBed = _hostPcmIntervalStartLeftInBed
                    startRightInBed = _hostPcmIntervalStartRightInBed
                Else
                    ' Direct-DAC/silence changes can occur without a DMA source frame.
                    ' At the next host sample boundary the current DAC value is the
                    ' correct value to present.
                    startLeftInBed = _currentLeft
                    startRightInBed = _currentRight
                End If

                Dim renderAcceptedInBed As Boolean =
                    _hostAudioInBed.PostRenderInterval(hostFrames,
                                                       _hostPcmFramesPendingInBed,
                                                       startLeftInBed,
                                                       startRightInBed,
                                                       _speakerEnabled,
                                                       PcmVolume(True),
                                                       PcmVolume(False),
                                                       MidiVolume(True),
                                                       MidiVolume(False))
                If renderAcceptedInBed Then
                    ' Post the wavetable render request immediately after the matching
                    ' host-mix interval is accepted. Guest MIDI is produced only by
                    ' this same machine owner, so WavetableHost FIFO order is retained.
                    Dim wavetableHostInBed As WavetableHost = _wavetableHostInBed
                    If wavetableHostInBed IsNot Nothing Then
                        wavetableHostInBed.RequestPcmFrames(hostFrames)
                    End If
                End If
            ElseIf _hostPcmFramesPendingInBed > 0 Then
                ' A presentation interval was not needed.  Prevent muted/obsolete
                ' source-frame history from accumulating behind the sleeping host
                ' renderer.
                _hostAudioInBed.DiscardPendingPcmSourceFrames()
            End If

            _hostPcmFramesPendingInBed = 0
            _hostPcmIntervalStartLeftInBed = _currentLeft
            _hostPcmIntervalStartRightInBed = _currentRight
        End If
    End Sub

    Public Function PicosecondsUntilNextWakeEvent() As Long Implements IClockWakeSource.PicosecondsUntilNextWakeEvent
        If Not _playbackActive OrElse _playbackPaused OrElse _sampleRate <= 0 OrElse _playbackUnitsRemaining <= 0 Then
            Return Long.MaxValue
        End If
        Dim unitsPerFrame As Integer = If(_playbackStereo, 2, 1)
        Dim frames As Long = Math.Max(1L, (CLng(_playbackUnitsRemaining) + unitsPerFrame - 1L) \ unitsPerFrame)
        Dim ps As Long = (frames * PicosecondsPerSecond) \ _sampleRate
        If ps <= 0 Then ps = 1
        Return ps
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
        _speakerEnabled = False
        _sampleRate = 22050
        _legacyBlockUnits = 1
        _testRegister = 0
        _mpuReadQueue.Clear()
        _mpuUartMode = False
        If _wavetableHostInBed IsNot Nothing Then _wavetableHostInBed.DiscardBufferedPcm()
        _waveBlasterConnector.HardwareReset()
        StopPlayback()
        _hostClockNumerator = 0
        _hostPcmFramesPendingInBed = 0
        _hostPcmIntervalStartLeftInBed = 0
        _hostPcmIntervalStartRightInBed = 0
        _opl.Reset()
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
               "  sample rate           : " & _sampleRate.ToString() & " Hz" & Environment.NewLine &
               "  playback              : " & If(_playbackActive, If(_playback16Bit, "16-bit", "8-bit") & If(_playbackStereo, " stereo", " mono"), "idle") & Environment.NewLine &
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
    OplAddress = 0
    OplData = 1
    RenderInterval = 2
    Reset = 3
End Enum

Friend Structure Sb16HostAudioCommand
    Public Kind As Sb16HostAudioCommandKind
    Public Generation As Integer
    Public Bank As Integer
    Public Value As Byte
    Public HostFrames As Long
    Public PcmSourceFrames As Integer
    Public PcmStartLeft As Short
    Public PcmStartRight As Short
    Public SpeakerEnabled As Boolean
    Public PcmGainLeft As Double
    Public PcmGainRight As Double
    Public MidiGainLeft As Double
    Public MidiGainRight As Double
End Structure

' Host transducer for the SB16.  Guest-visible timers, DMA, DREQ, IRQ and register
' state remain on the coordinated machine owner; waveform generation and Windows
' audio submission run here on their own 48 kHz card/output clock.
Friend NotInheritable Class Sb16HostAudioEngine
    Implements IDisposable

    Private Const CommandQueueCapacity As Integer = 16384
    Private Const PcmSourceRingCapacity As Integer = 65536
    Private Const PcmSourceRingMask As Long = PcmSourceRingCapacity - 1L
    Private Const RenderBlockFrames As Integer = 512
    Private Const WavetableWaitMilliseconds As Integer = 20

    ' A real OPL/wavetable voice can continue after the CPU stops writing it.
    ' Therefore "no recent command" is NOT an idle test.  The host renderer only
    ' sleeps after it has actually produced sustained near-zero mixed output.
    Private Const SilenceThresholdInBed As Integer = 2
    Private Const SilenceFramesBeforeSleepInBed As Long = 4800L ' 100 ms @ 48 kHz

    Private ReadOnly _sampleRateInBed As Integer
    Private ReadOnly _commandsInBed As New BlockingCollection(Of Sb16HostAudioCommand)(CommandQueueCapacity)
    Private ReadOnly _workerInBed As Thread
    Private ReadOnly _oplInBed As Opl3FmCore
    Private ReadOnly _waveOutInBed As WinMmStereoOut16
    Private ReadOnly _wavetableSyncInBed As New Object()

    Private ReadOnly _pcmSourceLeftInBed(PcmSourceRingCapacity - 1) As Short
    Private ReadOnly _pcmSourceRightInBed(PcmSourceRingCapacity - 1) As Short
    Private _pcmSourceReadPositionInBed As Long
    Private _pcmSourceWritePositionInBed As Long
    Private _pcmSourceDiscardBeforePositionInBed As Long

    Private _wavetableHostInBed As WavetableHost
    Private _generationInBed As Integer
    Private _disposedInBed As Integer

    ' Cross-boundary rendering latch.  Machine-side writes only arm it; only the
    ' host worker may disarm it, and only after observing actual sustained silence.
    Private _needsContinuousRenderingInBed As Integer
    Private _activitySerialInBed As Long
    Private _silentFramesInBed As Long
    Private _silenceActivitySerialInBed As Long

    Public Sub New(sampleRateInBed As Integer)
        If sampleRateInBed <= 0 Then Throw New ArgumentOutOfRangeException(NameOf(sampleRateInBed))
        _sampleRateInBed = sampleRateInBed
        _oplInBed = New Opl3FmCore(sampleRateInBed)
        _waveOutInBed = New WinMmStereoOut16(sampleRateInBed)
        _workerInBed = New Thread(AddressOf WorkerLoopInBed) With {
            .IsBackground = True,
            .Name = "Virtual Machine Bench SB16 host audio"
        }
        _workerInBed.Start()
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

    Public Sub PostOplAddress(bankInBed As Integer, valueInBed As Byte)
        PostSimpleCommandInBed(Sb16HostAudioCommandKind.OplAddress, bankInBed, valueInBed)
    End Sub

    Public Sub PostOplData(bankInBed As Integer, valueInBed As Byte)
        PostSimpleCommandInBed(Sb16HostAudioCommandKind.OplData, bankInBed, valueInBed)
    End Sub

    Private Sub PostSimpleCommandInBed(kindInBed As Sb16HostAudioCommandKind,
                                       bankInBed As Integer,
                                       valueInBed As Byte)
        If Volatile.Read(_disposedInBed) <> 0 Then Return
        Dim commandInBed As New Sb16HostAudioCommand With {
            .Kind = kindInBed,
            .Generation = Volatile.Read(_generationInBed),
            .Bank = bankInBed And 1,
            .Value = valueInBed
        }
        Try
            _commandsInBed.TryAdd(commandInBed, 0)
        Catch ex As InvalidOperationException
        End Try
    End Sub

    ' SPSC decoded-DSP source ring.  The machine owner is the only producer and
    ' this host-audio worker is the only consumer.  No allocation or wait occurs
    ' in the DMA/sample hot path.
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
                                       midiGainRightInBed As Double) As Boolean
        If hostFramesInBed <= 0 OrElse Volatile.Read(_disposedInBed) <> 0 Then Return False
        Dim commandInBed As New Sb16HostAudioCommand With {
            .Kind = Sb16HostAudioCommandKind.RenderInterval,
            .Generation = Volatile.Read(_generationInBed),
            .HostFrames = hostFramesInBed,
            .PcmSourceFrames = Math.Max(0, pcmSourceFramesInBed),
            .PcmStartLeft = pcmStartLeftInBed,
            .PcmStartRight = pcmStartRightInBed,
            .SpeakerEnabled = speakerEnabledInBed,
            .PcmGainLeft = pcmGainLeftInBed,
            .PcmGainRight = pcmGainRightInBed,
            .MidiGainLeft = midiGainLeftInBed,
            .MidiGainRight = midiGainRightInBed
        }
        Try
            If _commandsInBed.TryAdd(commandInBed, 0) Then Return True
        Catch ex As InvalidOperationException
        End Try

        ' Host audio has fallen behind.  Flush decoded source samples up to this
        ' point rather than ever blocking the machine owner on presentation work.
        Interlocked.Exchange(_pcmSourceDiscardBeforePositionInBed,
                             Interlocked.Read(_pcmSourceWritePositionInBed))
        Return False
    End Function

    Public Sub ResetAudioState()
        If Volatile.Read(_disposedInBed) <> 0 Then Return
        Dim generationInBed As Integer = Interlocked.Increment(_generationInBed)
        Interlocked.Exchange(_pcmSourceDiscardBeforePositionInBed,
                             Interlocked.Read(_pcmSourceWritePositionInBed))
        Interlocked.Exchange(_silentFramesInBed, 0L)
        Dim activityInBed As Long = Interlocked.Increment(_activitySerialInBed)
        Interlocked.Exchange(_silenceActivitySerialInBed, activityInBed)
        Volatile.Write(_needsContinuousRenderingInBed, 1)
        Dim commandInBed As New Sb16HostAudioCommand With {
            .Kind = Sb16HostAudioCommandKind.Reset,
            .Generation = generationInBed
        }
        Try
            _commandsInBed.TryAdd(commandInBed, 0)
        Catch ex As InvalidOperationException
        End Try
    End Sub

    Private Sub WorkerLoopInBed()
        Dim wavetableLeftInBed(RenderBlockFrames - 1) As Single
        Dim wavetableRightInBed(RenderBlockFrames - 1) As Single
        Try
            For Each commandInBed As Sb16HostAudioCommand In _commandsInBed.GetConsumingEnumerable()
                If Volatile.Read(_disposedInBed) <> 0 Then Exit For
                If commandInBed.Generation <> Volatile.Read(_generationInBed) Then Continue For

                Select Case commandInBed.Kind
                    Case Sb16HostAudioCommandKind.OplAddress
                        _oplInBed.WriteAddress(commandInBed.Bank, commandInBed.Value)
                    Case Sb16HostAudioCommandKind.OplData
                        _oplInBed.WriteData(commandInBed.Bank, commandInBed.Value)
                    Case Sb16HostAudioCommandKind.Reset
                        _oplInBed.Reset()
                        _waveOutInBed.Reset()
                        _silentFramesInBed = 0
                        _silenceActivitySerialInBed =
                            Interlocked.Read(_activitySerialInBed)
                    Case Sb16HostAudioCommandKind.RenderInterval
                        RenderIntervalInBed(commandInBed, wavetableLeftInBed, wavetableRightInBed)
                End Select
            Next
        Catch ex As InvalidOperationException
        Catch ex As ObjectDisposedException
        End Try
    End Sub

    Private Sub RenderIntervalInBed(commandInBed As Sb16HostAudioCommand,
                                    wavetableLeftInBed As Single(),
                                    wavetableRightInBed As Single())
        Dim totalHostFramesInBed As Long = Math.Max(1L, commandInBed.HostFrames)
        Dim sourceFramesInBed As Integer = Math.Max(0, commandInBed.PcmSourceFrames)
        Dim sourceAccumulatorInBed As Long = 0
        Dim sourceConsumedInBed As Integer = 0
        Dim currentPcmLeftInBed As Short = commandInBed.PcmStartLeft
        Dim currentPcmRightInBed As Short = commandInBed.PcmStartRight
        Dim hostRemainingInBed As Long = commandInBed.HostFrames

        While hostRemainingInBed > 0 AndAlso Volatile.Read(_disposedInBed) = 0
            Dim blockFramesInBed As Integer = CInt(Math.Min(CLng(RenderBlockFrames), hostRemainingInBed))
            FillWavetableBlockInBed(wavetableLeftInBed, wavetableRightInBed, blockFramesInBed)

            Dim blockIsSilentInBed As Boolean = True

            For indexInBed As Integer = 0 To blockFramesInBed - 1
                sourceAccumulatorInBed += sourceFramesInBed
                While sourceConsumedInBed < sourceFramesInBed AndAlso sourceAccumulatorInBed >= totalHostFramesInBed
                    Dim nextLeftInBed As Short
                    Dim nextRightInBed As Short
                    If TryReadPcmSourceFrameInBed(nextLeftInBed, nextRightInBed) Then
                        currentPcmLeftInBed = nextLeftInBed
                        currentPcmRightInBed = nextRightInBed
                    End If
                    sourceConsumedInBed += 1
                    sourceAccumulatorInBed -= totalHostFramesInBed
                End While

                Dim fmLeftInBed As Double
                Dim fmRightInBed As Double
                _oplInBed.GenerateSample(fmLeftInBed, fmRightInBed)

                Dim pcmLeftInBed As Double = 0.0
                Dim pcmRightInBed As Double = 0.0
                If commandInBed.SpeakerEnabled Then
                    pcmLeftInBed = CDbl(currentPcmLeftInBed) * commandInBed.PcmGainLeft
                    pcmRightInBed = CDbl(currentPcmRightInBed) * commandInBed.PcmGainRight
                End If

                Dim midiLeftInBed As Double = (fmLeftInBed + CDbl(wavetableLeftInBed(indexInBed))) * 9000.0 * commandInBed.MidiGainLeft
                Dim midiRightInBed As Double = (fmRightInBed + CDbl(wavetableRightInBed(indexInBed))) * 9000.0 * commandInBed.MidiGainRight

                Dim mixedLeftInBed As Integer = ClampInt16InBed(CInt(Math.Round(pcmLeftInBed + midiLeftInBed)))
                Dim mixedRightInBed As Integer = ClampInt16InBed(CInt(Math.Round(pcmRightInBed + midiRightInBed)))
                _waveOutInBed.SubmitSample(CShort(mixedLeftInBed), CShort(mixedRightInBed))

                If Math.Abs(mixedLeftInBed) > SilenceThresholdInBed OrElse
                   Math.Abs(mixedRightInBed) > SilenceThresholdInBed Then
                    blockIsSilentInBed = False
                End If
            Next

            UpdateRenderedSilenceStateInBed(blockIsSilentInBed, blockFramesInBed)
            hostRemainingInBed -= blockFramesInBed
        End While

        ' A dropped/overloaded presentation frame must not poison the next interval's
        ' source-ring alignment.  Consume any source frames not reached above.
        While sourceConsumedInBed < sourceFramesInBed
            Dim ignoredLeftInBed As Short
            Dim ignoredRightInBed As Short
            TryReadPcmSourceFrameInBed(ignoredLeftInBed, ignoredRightInBed)
            sourceConsumedInBed += 1
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

        ' Silence alone is insufficient.  A muted OPL operator or SoundFont voice
        ' must continue evolving so that a later mixer unmute exposes the phase /
        ' envelope it would have reached on real hardware.
        If _oplInBed.HasActiveVoices Then
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

        ' Do not race a new guest OPL/MIDI/DAC/mixer event. If activity changed
        ' during the silent observation window, restart the proof instead.
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
        Dim discardBeforeInBed As Long = Interlocked.Read(_pcmSourceDiscardBeforePositionInBed)
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

    Private Shared Function ClampInt16InBed(valueInBed As Integer) As Integer
        If valueInBed > Short.MaxValue Then Return Short.MaxValue
        If valueInBed < Short.MinValue Then Return Short.MinValue
        Return valueInBed
    End Function

    Public Sub Dispose() Implements IDisposable.Dispose
        If Interlocked.Exchange(_disposedInBed, 1) <> 0 Then Return
        Try
            _commandsInBed.CompleteAdding()
        Catch ex As InvalidOperationException
        End Try
        SyncLock _wavetableSyncInBed
            _wavetableHostInBed = Nothing
        End SyncLock
        If Thread.CurrentThread IsNot _workerInBed Then _workerInBed.Join(500)
        _waveOutInBed.Dispose()
    End Sub
End Class

' Register-compatible OPL2/OPL3 synthesis core.  The register/timer behavior is
' modeled for AdLib/SB detection and ordinary two-operator music.  FM waveform
' generation intentionally favors stable real-time output over transistor-level
' YMF262 envelope/phase quirks; guest-visible timers and programming registers
' remain at the card boundary.
Friend NotInheritable Class Opl3FmCore
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
        Public Envelope As Double
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

    Private Shared ReadOnly OperatorOffsets() As Integer = {0, 1, 2, 3, 4, 5, 8, 9, 10, 11, 12, 13, 16, 17, 18, 19, 20, 21}
    Private Shared ReadOnly ModulatorIndex() As Integer = {0, 1, 2, 6, 7, 8, 12, 13, 14}
    Private Shared ReadOnly CarrierIndex() As Integer = {3, 4, 5, 9, 10, 11, 15, 16, 17}
    Private Shared ReadOnly MultiplierTable() As Double = {0.5, 1, 2, 3, 4, 5, 6, 7, 8, 9, 10, 10, 12, 12, 15, 15}

    Private ReadOnly _sampleRate As Integer
    Private ReadOnly _registers(1, 255) As Byte
    Private ReadOnly _address(1) As Byte
    Private ReadOnly _operators(35) As OperatorState
    Private ReadOnly _channels(17) As ChannelState

    Private _opl3Enabled As Boolean
    Private _timer1Value As Byte
    Private _timer2Value As Byte
    Private _timerControl As Byte
    Private _timer1Elapsed As Long
    Private _timer2Elapsed As Long
    Private _status As Byte

    Public Sub New(sampleRate As Integer)
        _sampleRate = sampleRate
        For i As Integer = 0 To _operators.Length - 1 : _operators(i) = New OperatorState() : Next
        For i As Integer = 0 To _channels.Length - 1 : _channels(i) = New ChannelState() : Next
        Reset()
    End Sub

    Public ReadOnly Property Opl3Enabled As Boolean
        Get
            Return _opl3Enabled
        End Get
    End Property

    Public Sub Reset()
        Array.Clear(_registers, 0, _registers.Length)
        _address(0) = 0 : _address(1) = 0
        _opl3Enabled = False
        _timer1Value = 0 : _timer2Value = 0 : _timerControl = 0
        _timer1Elapsed = 0 : _timer2Elapsed = 0 : _status = 0
        For Each op As OperatorState In _operators
            op.Reg20 = 0 : op.Reg40 = 0 : op.Reg60 = 0 : op.Reg80 = 0 : op.RegE0 = 0
            op.Phase = 0 : op.Envelope = 0 : op.EnvPhase = EnvelopePhase.Off
        Next
        For Each ch As ChannelState In _channels
            ch.Fnum = 0 : ch.Block = 0 : ch.KeyOn = False : ch.RegC0 = 0
            ch.Feedback1 = 0 : ch.Feedback2 = 0
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
                Case 2 : _timer1Value = value : Return
                Case 3 : _timer2Value = value : Return
                Case 4 : WriteTimerControl(value) : Return
            End Select
        ElseIf reg = 5 Then
            _opl3Enabled = (value And 1) <> 0
            Return
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
            _channels(channel).Fnum = (_channels(channel).Fnum And &HFF) Or ((value And 3) << 8)
            _channels(channel).Block = (value >> 2) And 7
            _channels(channel).KeyOn = (value And &H20) <> 0
            If _channels(channel).KeyOn AndAlso Not wasOn Then KeyOnChannel(channel)
            If Not _channels(channel).KeyOn AndAlso wasOn Then KeyOffChannel(channel)
        ElseIf reg >= &HC0 AndAlso reg <= &HC8 Then
            Dim channel As Integer = bank * 9 + (reg - &HC0)
            _channels(channel).RegC0 = value
        ElseIf reg >= &HE0 AndAlso reg <= &HF5 Then
            Dim index As Integer = OperatorIndex(bank, reg And &H1F)
            If index >= 0 Then _operators(index).RegE0 = value
        End If
    End Sub

    Private Sub WriteTimerControl(value As Byte)
        If (value And &H80) <> 0 Then
            _status = 0
            _timer1Elapsed = 0
            _timer2Elapsed = 0
        End If
        _timerControl = value
        If (value And 1) = 0 Then _timer1Elapsed = 0
        If (value And 2) = 0 Then _timer2Elapsed = 0
    End Sub

    Public ReadOnly Property HasRunningTimers As Boolean
        Get
            Return (_timerControl And 3) <> 0
        End Get
    End Property

    ' Host-renderer state only.  This core lives exclusively on the dedicated
    ' SB16 audio worker, so scanning envelope phases here is race-free.
    Public ReadOnly Property HasActiveVoices As Boolean
        Get
            For Each opInBed As OperatorState In _operators
                If opInBed.EnvPhase <> EnvelopePhase.Off Then Return True
            Next
            Return False
        End Get
    End Property

    Public Sub AdvanceTime(elapsedPicoseconds As Long)
        If (_timerControl And 1) <> 0 Then
            _timer1Elapsed += elapsedPicoseconds
            Dim period As Long = Math.Max(1L, CLng(256 - CInt(_timer1Value)) * 80000000L) ' 80 us
            If _timer1Elapsed >= period Then
                _timer1Elapsed = _timer1Elapsed Mod period
                _status = CByte(_status Or &H40)
                If (_timerControl And &H40) = 0 Then _status = CByte(_status Or &H80)
            End If
        End If
        If (_timerControl And 2) <> 0 Then
            _timer2Elapsed += elapsedPicoseconds
            Dim period As Long = Math.Max(1L, CLng(256 - CInt(_timer2Value)) * 320000000L) ' 320 us
            If _timer2Elapsed >= period Then
                _timer2Elapsed = _timer2Elapsed Mod period
                _status = CByte(_status Or &H20)
                If (_timerControl And &H20) = 0 Then _status = CByte(_status Or &H80)
            End If
        End If
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
        op.EnvPhase = EnvelopePhase.Attack
        If op.Envelope < 0.0001 Then op.Envelope = 0.0001
    End Sub

    Private Shared Sub ReleaseOperator(op As OperatorState)
        If op.EnvPhase <> EnvelopePhase.Off Then op.EnvPhase = EnvelopePhase.Release
    End Sub

    Public Sub GenerateSample(ByRef left As Double, ByRef right As Double)
        left = 0.0 : right = 0.0
        Dim channelLimit As Integer = If(_opl3Enabled, 18, 9)

        For channelIndex As Integer = 0 To channelLimit - 1
            Dim ch As ChannelState = _channels(channelIndex)
            Dim localChannel As Integer = channelIndex Mod 9
            Dim bank As Integer = channelIndex \ 9
            Dim modOp As OperatorState = _operators(bank * 18 + ModulatorIndex(localChannel))
            Dim carOp As OperatorState = _operators(bank * 18 + CarrierIndex(localChannel))

            Dim frequency As Double = OplFrequency(ch.Fnum, ch.Block)
            Dim feedbackLevel As Integer = (ch.RegC0 >> 1) And 7
            Dim feedback As Double = 0.0
            If feedbackLevel > 0 Then
                feedback = (ch.Feedback1 + ch.Feedback2) * (feedbackLevel / 14.0)
            End If

            Dim modulator As Double = GenerateOperator(modOp, frequency, feedback, ch.KeyOn)
            ch.Feedback2 = ch.Feedback1
            ch.Feedback1 = modulator

            Dim carrier As Double
            If (ch.RegC0 And 1) = 0 Then
                carrier = GenerateOperator(carOp, frequency, modulator * 4.0, ch.KeyOn)
            Else
                carrier = GenerateOperator(carOp, frequency, 0.0, ch.KeyOn) + modulator * 0.5
            End If

            Dim sendLeft As Boolean = True
            Dim sendRight As Boolean = True
            If _opl3Enabled Then
                sendLeft = (ch.RegC0 And &H10) <> 0
                sendRight = (ch.RegC0 And &H20) <> 0
                If Not sendLeft AndAlso Not sendRight Then sendLeft = True : sendRight = True
            End If
            If sendLeft Then left += carrier
            If sendRight Then right += carrier
        Next

        left /= 9.0
        right /= 9.0
    End Sub

    Private Shared Function OplFrequency(fnum As Integer, block As Integer) As Double
        If fnum <= 0 Then Return 0.0
        Return fnum * Math.Pow(2.0, block - 1) * 49716.0 / 524288.0
    End Function

    Private Function GenerateOperator(op As OperatorState, baseFrequency As Double, phaseModulation As Double, keyOn As Boolean) As Double
        UpdateEnvelope(op, keyOn)
        If op.Envelope <= 0.0 OrElse baseFrequency <= 0.0 Then Return 0.0

        Dim multiple As Double = MultiplierTable(op.Reg20 And &HF)
        op.Phase += 2.0 * Math.PI * baseFrequency * multiple / _sampleRate
        If op.Phase >= Math.PI * 2.0 Then op.Phase -= Math.Floor(op.Phase / (Math.PI * 2.0)) * Math.PI * 2.0

        Dim totalLevel As Integer = op.Reg40 And &H3F
        Dim level As Double = Math.Pow(10.0, -(totalLevel * 0.75) / 20.0)
        Dim wave As Double = Waveform(op.Phase + phaseModulation, op.RegE0 And 7)
        Return wave * op.Envelope * level
    End Function

    Private Sub UpdateEnvelope(op As OperatorState, keyOn As Boolean)
        If Not keyOn AndAlso op.EnvPhase <> EnvelopePhase.Off Then op.EnvPhase = EnvelopePhase.Release

        Dim attack As Integer = (op.Reg60 >> 4) And &HF
        Dim decay As Integer = op.Reg60 And &HF
        Dim sustainCode As Integer = (op.Reg80 >> 4) And &HF
        Dim release As Integer = op.Reg80 And &HF
        Dim sustainLevel As Double = Math.Max(0.02, 1.0 - sustainCode / 15.0)

        Select Case op.EnvPhase
            Case EnvelopePhase.Attack
                Dim stepValue As Double = Math.Pow(attack + 1.0, 2.0) / (_sampleRate * 4.0)
                op.Envelope += (1.0 - op.Envelope) * Math.Min(1.0, stepValue)
                If op.Envelope >= 0.995 Then op.Envelope = 1.0 : op.EnvPhase = EnvelopePhase.Decay
            Case EnvelopePhase.Decay
                Dim stepValue As Double = Math.Pow(decay + 1.0, 2.0) / (_sampleRate * 18.0)
                op.Envelope -= Math.Min(op.Envelope, stepValue)
                If op.Envelope <= sustainLevel Then op.Envelope = sustainLevel : op.EnvPhase = EnvelopePhase.Sustain
            Case EnvelopePhase.Sustain
                If (op.Reg20 And &H20) = 0 Then
                    Dim stepValue As Double = Math.Pow(release + 1.0, 2.0) / (_sampleRate * 60.0)
                    op.Envelope -= Math.Min(op.Envelope, stepValue)
                    If op.Envelope <= 0.0001 Then op.Envelope = 0 : op.EnvPhase = EnvelopePhase.Off
                End If
            Case EnvelopePhase.Release
                Dim stepValue As Double = Math.Pow(release + 1.0, 2.0) / (_sampleRate * 30.0)
                op.Envelope -= Math.Min(op.Envelope, stepValue)
                If op.Envelope <= 0.0001 Then op.Envelope = 0 : op.EnvPhase = EnvelopePhase.Off
        End Select
    End Sub

    Private Shared Function Waveform(phase As Double, waveformIndex As Integer) As Double
        Dim s As Double = Math.Sin(phase)
        Select Case waveformIndex And 7
            Case 0 : Return s
            Case 1 : Return If(s < 0, 0.0, s)
            Case 2 : Return Math.Abs(s) * 2.0 - 1.0
            Case 3 : Return If(s < 0, 0.0, Math.Abs(s) * 2.0 - 1.0)
            Case 4 : Return Math.Sin(phase * 2.0)
            Case 5 : Return If(Math.Sin(phase * 2.0) < 0, 0.0, Math.Sin(phase * 2.0))
            Case 6 : Return If(s >= 0, 1.0, -1.0)
            Case Else : Return (2.0 / Math.PI) * Math.Asin(Math.Sin(phase))
        End Select
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
