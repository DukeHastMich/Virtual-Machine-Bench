Imports System
Imports System.IO
Imports System.Text

' Dual Intel 8237A-compatible DMA substrate for the AT/82C206 path.  It keeps
' independently programmable address/count/page registers, masks, requests,
' terminal-count state, modes, and each controller's byte flip-flop.  Device
' transfers terminate in motherboard memory callbacks rather than CPU-private
' storage, preserving the physical DMA/memory-device boundary.
Public Class Dma8237
    Implements IPortDevice, IResettableDevice, IMotherboardLocalPortDevice

    Private Class ChannelState
        Public BaseAddress As UInt16
        Public CurrentAddress As UInt16
        Public BaseCount As UInt16
        Public CurrentCount As UInt16
        Public Page As Byte
        Public Mode As Byte
        Public Masked As Boolean = True
        Public SoftwareRequest As Boolean
        Public Dreq As Boolean
        Public TerminalCount As Boolean
    End Class

    Private Class ControllerState
        Public Command As Byte
        Public StatusTerminalCount As Byte
        Public MaskRegister As Byte = &HF
        Public FlipFlopHigh As Boolean
        Public PriorityBase As Integer = 3
    End Class

    Private ReadOnly _channels(7) As ChannelState
    Private ReadOnly _controllers(1) As ControllerState
    ' CROMWELL PCB REFIT PHASE 2 BRICK 8C - DMA is a motherboard bus master.
    Private ReadOnly _readMemoryByte As Func(Of Integer, UInteger, Byte)
    Private ReadOnly _writeMemoryByte As Action(Of Integer, UInteger, Byte)
    Private ReadOnly _readMemoryWord As Func(Of Integer, UInteger, UInt16)
    Private ReadOnly _writeMemoryWord As Action(Of Integer, UInteger, UInt16)
    Private _refreshPage As Byte

    ' CROMWELL PCB REFIT PHASE 2 BRICK 8D - logical 8237 HRQ outputs.
    Private _dma8HrqInBed As Boolean
    Private _dma16HrqInBed As Boolean

    Public Event TerminalCount(channel As Integer)
    Public Event HoldRequestChanged(master As AtBusMaster286, asserted As Boolean)

    Public Sub New(readMemoryByte As Func(Of Integer, UInteger, Byte),
                   writeMemoryByte As Action(Of Integer, UInteger, Byte),
                   readMemoryWord As Func(Of Integer, UInteger, UInt16),
                   writeMemoryWord As Action(Of Integer, UInteger, UInt16))
        If readMemoryByte Is Nothing Then Throw New ArgumentNullException(NameOf(readMemoryByte))
        If writeMemoryByte Is Nothing Then Throw New ArgumentNullException(NameOf(writeMemoryByte))
        If readMemoryWord Is Nothing Then Throw New ArgumentNullException(NameOf(readMemoryWord))
        If writeMemoryWord Is Nothing Then Throw New ArgumentNullException(NameOf(writeMemoryWord))
        _readMemoryByte = readMemoryByte
        _writeMemoryByte = writeMemoryByte
        _readMemoryWord = readMemoryWord
        _writeMemoryWord = writeMemoryWord
        For i As Integer = 0 To 7 : _channels(i) = New ChannelState() : Next
        For i As Integer = 0 To 1 : _controllers(i) = New ControllerState() : Next
        ResetDevice()
    End Sub

    Public Sub ResetDevice() Implements IResettableDevice.ResetDevice
        SetHrqInBed(AtBusMaster286.Dma8, False)
        SetHrqInBed(AtBusMaster286.Dma16, False)
        For controller As Integer = 0 To 1
            _controllers(controller).Command = 0
            _controllers(controller).StatusTerminalCount = 0
            _controllers(controller).MaskRegister = &HF
            _controllers(controller).FlipFlopHigh = False
            _controllers(controller).PriorityBase = 3
        Next
        _refreshPage = 0
        For channel As Integer = 0 To 7
            Dim state As ChannelState = _channels(channel)
            state.BaseAddress = 0 : state.CurrentAddress = 0
            state.BaseCount = 0 : state.CurrentCount = 0
            state.Page = 0 : state.Mode = 0
            state.SoftwareRequest = False : state.Dreq = False : state.TerminalCount = False
            state.Masked = True
        Next
    End Sub

    Public Function HandlesPort(port As UInt16) As Boolean Implements IPortDevice.HandlesPort
        If port <= &HFUS Then Return True
        If IsPageRegisterPort(port) Then Return True
        Return port >= &HC0US AndAlso port <= &HDEUS AndAlso (port And 1US) = 0US
    End Function

    Public Function ReadPort(port As UInt16) As Byte Implements IPortDevice.ReadPort
        If IsPageRegisterPort(port) Then Return ReadPagePort(port)

        Dim controller As Integer
        Dim register As Integer
        If port <= &HFUS Then
            controller = 0 : register = CInt(port)
        Else
            controller = 1 : register = CInt((port - &HC0US) \ 2US)
        End If

        If register <= 7 Then
            Dim localChannel As Integer = register \ 2
            Dim channel As Integer = localChannel + controller * 4
            Dim isCount As Boolean = (register And 1) <> 0
            Dim word As UInt16 = If(isCount, _channels(channel).CurrentCount, _channels(channel).CurrentAddress)
            Return ReadAddressOrCount(controller, word)
        End If

        Select Case register
            Case 8
                Dim result As Byte = CByte((_controllers(controller).StatusTerminalCount And &HF) Or
                                           (CurrentRequestBits(controller) << 4))
                _controllers(controller).StatusTerminalCount = 0
                Return result
            Case &HD
                Return 0 ' temporary register is not used by PC/AT software
            Case &HF
                Return _controllers(controller).MaskRegister
            Case Else
                Return &HFF
        End Select
    End Function

    Public Sub WritePort(port As UInt16, value As Byte) Implements IPortDevice.WritePort
        If IsPageRegisterPort(port) Then
            WritePagePort(port, value)
            Return
        End If

        Dim controller As Integer
        Dim register As Integer
        If port <= &HFUS Then
            controller = 0 : register = CInt(port)
        Else
            controller = 1 : register = CInt((port - &HC0US) \ 2US)
        End If

        If register <= 7 Then
            Dim localChannel As Integer = register \ 2
            Dim channel As Integer = localChannel + controller * 4
            Dim isCount As Boolean = (register And 1) <> 0
            WriteAddressOrCount(controller, channel, isCount, value)
            Return
        End If

        Select Case register
            Case 8 ' command
                _controllers(controller).Command = value
                RecomputeHrqInBed(controller)
            Case 9 ' software request
                Dim localChannel As Integer = value And 3
                _channels(controller * 4 + localChannel).SoftwareRequest = (value And 4) <> 0
                RecomputeHrqInBed(controller)
            Case &HA ' single-channel mask
                Dim localChannel As Integer = value And 3
                SetMask(controller, localChannel, (value And 4) <> 0)
            Case &HB ' mode
                Dim localChannel As Integer = value And 3
                _channels(controller * 4 + localChannel).Mode = value
                RecomputeHrqInBed(controller)
            Case &HC ' clear byte pointer flip-flop
                _controllers(controller).FlipFlopHigh = False
            Case &HD ' master clear
                MasterClear(controller)
            Case &HE ' clear mask register
                For localChannel As Integer = 0 To 3 : SetMask(controller, localChannel, False) : Next
            Case &HF ' write all mask bits
                For localChannel As Integer = 0 To 3
                    SetMask(controller, localChannel, (value And (1 << localChannel)) <> 0)
                Next
        End Select
    End Sub

    Private Function ReadAddressOrCount(controller As Integer, word As UInt16) As Byte
        Dim result As Byte
        If Not _controllers(controller).FlipFlopHigh Then
            result = CByte(word And &HFFUS)
        Else
            result = CByte(word >> 8)
        End If
        _controllers(controller).FlipFlopHigh = Not _controllers(controller).FlipFlopHigh
        Return result
    End Function

    Private Sub WriteAddressOrCount(controller As Integer, channel As Integer, isCount As Boolean, value As Byte)
        Dim state As ChannelState = _channels(channel)
        If Not _controllers(controller).FlipFlopHigh Then
            If isCount Then
                state.BaseCount = CUShort((state.BaseCount And &HFF00US) Or value)
                state.CurrentCount = CUShort((state.CurrentCount And &HFF00US) Or value)
            Else
                state.BaseAddress = CUShort((state.BaseAddress And &HFF00US) Or value)
                state.CurrentAddress = CUShort((state.CurrentAddress And &HFF00US) Or value)
            End If
        Else
            If isCount Then
                state.BaseCount = CUShort((state.BaseCount And &HFFUS) Or (CUShort(value) << 8))
                state.CurrentCount = state.BaseCount
                state.TerminalCount = False
            Else
                state.BaseAddress = CUShort((state.BaseAddress And &HFFUS) Or (CUShort(value) << 8))
                state.CurrentAddress = state.BaseAddress
            End If
        End If
        _controllers(controller).FlipFlopHigh = Not _controllers(controller).FlipFlopHigh
    End Sub

    Private Sub SetMask(controller As Integer, localChannel As Integer, masked As Boolean)
        Dim bit As Byte = CByte(1 << localChannel)
        If masked Then
            _controllers(controller).MaskRegister = CByte(_controllers(controller).MaskRegister Or bit)
        Else
            _controllers(controller).MaskRegister = CByte(_controllers(controller).MaskRegister And Not bit)
        End If
        _channels(controller * 4 + localChannel).Masked = masked
        RecomputeHrqInBed(controller)
    End Sub

    Private Sub MasterClear(controller As Integer)
        SetHrqInBed(If(controller = 0, AtBusMaster286.Dma8, AtBusMaster286.Dma16), False)
        _controllers(controller).Command = 0
        _controllers(controller).StatusTerminalCount = 0
        _controllers(controller).MaskRegister = &HF
        _controllers(controller).FlipFlopHigh = False
        _controllers(controller).PriorityBase = 3
        For localChannel As Integer = 0 To 3
            Dim channel As Integer = controller * 4 + localChannel
            _channels(channel).SoftwareRequest = False
            _channels(channel).Dreq = False
            _channels(channel).Masked = True
        Next
    End Sub

    Private Shared Function BusMasterForChannelInBed(channelInBed As Integer) As AtBusMaster286
        If channelInBed >= 5 Then Return AtBusMaster286.Dma16
        Return AtBusMaster286.Dma8
    End Function

    Private Sub SetHrqInBed(masterInBed As AtBusMaster286, assertedInBed As Boolean)
        Select Case masterInBed
            Case AtBusMaster286.Dma8
                If _dma8HrqInBed = assertedInBed Then Return
                _dma8HrqInBed = assertedInBed
            Case AtBusMaster286.Dma16
                If _dma16HrqInBed = assertedInBed Then Return
                _dma16HrqInBed = assertedInBed
            Case Else
                Throw New ArgumentOutOfRangeException(NameOf(masterInBed))
        End Select
        RaiseEvent HoldRequestChanged(masterInBed, assertedInBed)
    End Sub

    Public ReadOnly Property Dma8HoldRequestAsserted As Boolean
        Get
            Return _dma8HrqInBed
        End Get
    End Property

    Public ReadOnly Property Dma16HoldRequestAsserted As Boolean
        Get
            Return _dma16HrqInBed
        End Get
    End Property

    Public Sub SetDreq(channel As Integer, asserted As Boolean)
        ValidateChannel(channel)
        If channel = 4 Then Return ' channel 4 is the fixed cascade path
        _channels(channel).Dreq = asserted
        RecomputeHrqInBed(If(channel <= 3, 0, 1))
    End Sub

    Public Function IsDreqAsserted(channel As Integer) As Boolean
        ValidateChannel(channel)
        Return _channels(channel).Dreq OrElse _channels(channel).SoftwareRequest
    End Function

    Public Function IsMasked(channel As Integer) As Boolean
        ValidateChannel(channel)
        Return _channels(channel).Masked
    End Function

    Public Function GetPhysicalAddress(channel As Integer) As UInteger
        ValidateChannel(channel)
        Dim state As ChannelState = _channels(channel)
        If channel <= 3 Then
            Return (CUInt(state.Page) << 16) Or state.CurrentAddress
        End If
        If channel = 4 Then Return 0
        ' 16-bit channels present word addresses on XA1-XA16; the page register
        ' supplies A17-A23.  Page bit 0 does not participate in a 16-bit DMA.
        Return (CUInt(state.Page And &HFE) << 16) Or (CUInt(state.CurrentAddress) << 1)
    End Function

    Public Function TransferToMemory(channel As Integer, buffer As Byte(), offset As Integer, length As Integer) As Integer
        ValidateTransfer(channel, buffer, offset, length)
        If channel = 4 OrElse _channels(channel).Masked OrElse ControllerDisabled(channel) Then Return 0
        ' 8237 mode bits 3:2 = 01: peripheral writes into memory.
        If ((_channels(channel).Mode >> 2) And 3) <> 1 Then Return 0
        Return TransferBytes(channel, buffer, offset, length, toMemory:=True)
    End Function

    Public Function TransferFromMemory(channel As Integer, buffer As Byte(), offset As Integer, length As Integer) As Integer
        ValidateTransfer(channel, buffer, offset, length)
        If channel = 4 OrElse _channels(channel).Masked OrElse ControllerDisabled(channel) Then Return 0
        ' 8237 mode bits 3:2 = 10: memory is read toward the peripheral.
        If ((_channels(channel).Mode >> 2) And 3) <> 2 Then Return 0
        Return TransferBytes(channel, buffer, offset, length, toMemory:=False)
    End Function

    Private Function TransferBytes(channel As Integer, buffer As Byte(), offset As Integer, length As Integer, toMemory As Boolean) As Integer
        Dim state As ChannelState = _channels(channel)
        Dim unitBytes As Integer = If(channel >= 5, 2, 1)
        Dim transferred As Integer
        Dim serviceModeInBed As Integer = (state.Mode >> 6) And 3
        Dim masterInBed As AtBusMaster286 = BusMasterForChannelInBed(channel)

        ' A real 8237 does not become a bus master merely because software calls
        ' a helper.  A live peripheral/software request first produces HRQ, then
        ' memory cycles may occur only after the motherboard returns HLDA.
        If Not IsDreqAsserted(channel) Then Return 0
        If serviceModeInBed = 3 Then Return 0 ' cascade channels do not transfer data themselves
        If HighestPriorityRequestedChannelInBed(If(channel <= 3, 0, 1)) <> channel Then Return 0

        Dim continuousHrqInBed As Boolean = (serviceModeInBed = 0 OrElse serviceModeInBed = 2)
        If continuousHrqInBed Then SetHrqInBed(masterInBed, True)

        Try
            While transferred < length
                ' Demand mode relinquishes the bus when DREQ drops.  Block mode
                ' keeps ownership once service begins; single mode emits one HRQ
                ' ownership interval for each programmed DMA unit.
                If serviceModeInBed = 0 AndAlso Not IsDreqAsserted(channel) Then Exit While

                Dim remainingUnits As Integer = CInt(state.CurrentCount) + 1
                If remainingUnits <= 0 Then Exit While
                Dim address As UInteger = GetPhysicalAddress(channel)

                If serviceModeInBed = 1 Then SetHrqInBed(masterInBed, True)
                Try
                    If unitBytes = 2 AndAlso transferred + 1 < length Then
                        ' AT secondary DMA channels are sixteen-bit bus masters.
                        ' One programmed DMA unit therefore owns one physical word cycle.
                        If toMemory Then
                            Dim wordInBed As UInt16 =
                                CUShort(CUInt(buffer(offset + transferred)) Or
                                       (CUInt(buffer(offset + transferred + 1)) << 8))
                            _writeMemoryWord(channel, address, wordInBed)
                        Else
                            Dim wordInBed As UInt16 = _readMemoryWord(channel, address)
                            buffer(offset + transferred) = CByte(wordInBed And &HFFUS)
                            buffer(offset + transferred + 1) = CByte(wordInBed >> 8)
                        End If
                        transferred += 2
                    Else
                        If toMemory Then
                            _writeMemoryByte(channel, address, buffer(offset + transferred))
                        Else
                            buffer(offset + transferred) = _readMemoryByte(channel, address)
                        End If
                        transferred += 1
                    End If
                Finally
                    If serviceModeInBed = 1 Then SetHrqInBed(masterInBed, False)
                End Try

                AdvanceChannel(channel)
                If state.TerminalCount Then Exit While
            End While
        Finally
            RecomputeHrqInBed(If(channel <= 3, 0, 1))
        End Try
        Return transferred
    End Function

    Private Sub AdvanceChannel(channel As Integer)
        Dim state As ChannelState = _channels(channel)
        Dim decrement As Boolean = (state.Mode And &H20) <> 0
        If decrement Then
            state.CurrentAddress = CUShort((CInt(state.CurrentAddress) - 1) And &HFFFF)
        Else
            state.CurrentAddress = CUShort((CInt(state.CurrentAddress) + 1) And &HFFFF)
        End If

        If state.CurrentCount = 0 Then
            Dim controller As Integer = If(channel <= 3, 0, 1)
            Dim localChannel As Integer = channel And 3
            state.CurrentCount = &HFFFFUS
            state.TerminalCount = True
            _controllers(controller).StatusTerminalCount =
                CByte(_controllers(controller).StatusTerminalCount Or (1 << localChannel))
            RaiseEvent TerminalCount(channel)

            If (state.Mode And &H10) <> 0 Then
                state.CurrentAddress = state.BaseAddress
                state.CurrentCount = state.BaseCount
                state.TerminalCount = False
            Else
                SetMask(controller, localChannel, True)
            End If

            If (_controllers(controller).Command And &H10) <> 0 Then
                _controllers(controller).PriorityBase = localChannel
            End If
        Else
            state.CurrentCount = CUShort(state.CurrentCount - 1US)
        End If

        If (_controllers(If(channel <= 3, 0, 1)).Command And &H10) <> 0 Then
            _controllers(If(channel <= 3, 0, 1)).PriorityBase = channel And 3
        End If
        RecomputeHrqInBed(If(channel <= 3, 0, 1))
    End Sub

    Private Function HighestPriorityRequestedChannelInBed(controllerInBed As Integer) As Integer
        If (_controllers(controllerInBed).Command And &H4) <> 0 Then Return -1
        Dim firstRankInBed As Integer = (_controllers(controllerInBed).PriorityBase + 1) And 3
        For rankInBed As Integer = 0 To 3
            Dim localChannelInBed As Integer = (firstRankInBed + rankInBed) And 3
            Dim channelInBed As Integer = controllerInBed * 4 + localChannelInBed
            If channelInBed = 4 Then Continue For
            Dim stateInBed As ChannelState = _channels(channelInBed)
            If Not stateInBed.Masked AndAlso
               (stateInBed.Dreq OrElse stateInBed.SoftwareRequest) AndAlso
               ((stateInBed.Mode >> 6) And 3) <> 3 Then
                Return channelInBed
            End If
        Next
        Return -1
    End Function

    Private Sub RecomputeHrqInBed(controllerInBed As Integer)
        Dim masterInBed As AtBusMaster286 =
            If(controllerInBed = 0, AtBusMaster286.Dma8, AtBusMaster286.Dma16)
        SetHrqInBed(masterInBed, HighestPriorityRequestedChannelInBed(controllerInBed) >= 0)
    End Sub

    Private Function ControllerDisabled(channel As Integer) As Boolean
        Dim controller As Integer = If(channel <= 3, 0, 1)
        Return (_controllers(controller).Command And &H4) <> 0
    End Function

    Private Function CurrentRequestBits(controller As Integer) As Byte
        Dim result As Byte
        For localChannel As Integer = 0 To 3
            Dim state As ChannelState = _channels(controller * 4 + localChannel)
            If state.Dreq OrElse state.SoftwareRequest Then result = CByte(result Or (1 << localChannel))
        Next
        Return result
    End Function

    Private Function ReadPagePort(port As UInt16) As Byte
        If port = &H8FUS Then Return _refreshPage
        Dim channel As Integer = PagePortToChannel(port)
        If channel < 0 Then Return &HFF
        Return _channels(channel).Page
    End Function

    Private Sub WritePagePort(port As UInt16, value As Byte)
        If port = &H8FUS Then
            _refreshPage = value
            Return
        End If
        Dim channel As Integer = PagePortToChannel(port)
        If channel >= 0 Then _channels(channel).Page = value
    End Sub

    Private Shared Function IsPageRegisterPort(port As UInt16) As Boolean
        Select Case port
            Case &H81US, &H82US, &H83US, &H87US, &H89US, &H8AUS, &H8BUS, &H8FUS
                Return True
            Case Else
                Return False
        End Select
    End Function

    Private Shared Function PagePortToChannel(port As UInt16) As Integer
        Select Case port
            Case &H87US : Return 0
            Case &H83US : Return 1
            Case &H81US : Return 2
            Case &H82US : Return 3
            Case &H8BUS : Return 5
            Case &H89US : Return 6
            Case &H8AUS : Return 7
            Case Else : Return -1
        End Select
    End Function

    Private Shared Sub ValidateChannel(channel As Integer)
        If channel < 0 OrElse channel > 7 Then Throw New ArgumentOutOfRangeException(NameOf(channel))
    End Sub

    Private Shared Sub ValidateTransfer(channel As Integer, buffer As Byte(), offset As Integer, length As Integer)
        ValidateChannel(channel)
        If buffer Is Nothing Then Throw New ArgumentNullException(NameOf(buffer))
        If offset < 0 OrElse length < 0 OrElse offset + length > buffer.Length Then Throw New ArgumentOutOfRangeException(NameOf(offset))
    End Sub
End Class

' Motorola 6845-compatible register front end used by the CGA adapter.
' The display renderer consumes the programmed start address, just as the
' physical CRT controller selects which part of B8000h is scanned out.
Public Class CgaCrtc6845
    Implements IPortDevice

    Private ReadOnly _registers(31) As Byte
    Private _index As Byte

    Public ReadOnly Property StartAddress As UInt16
        Get
            Return CUShort(((CUShort(_registers(&HC)) << 8) Or _registers(&HD)) And &H3FFFUS)
        End Get
    End Property

    Public Function HandlesPort(port As UInt16) As Boolean Implements IPortDevice.HandlesPort
        Return port >= &H3D4US AndAlso port <= &H3DAUS
    End Function

    Public Function ReadPort(port As UInt16) As Byte Implements IPortDevice.ReadPort
        Select Case port
            Case &H3D4US : Return _index
            Case &H3D5US : Return _registers(_index And &H1F)
            Case &H3DAUS : Return 1 ' display enabled; deterministic status
            Case Else : Return 0
        End Select
    End Function

    Public Sub WritePort(port As UInt16, value As Byte) Implements IPortDevice.WritePort
        If port = &H3D4US Then
            _index = CByte(value And &H1F)
        ElseIf port = &H3D5US Then
            _registers(_index And &H1F) = value
        End If
    End Sub
End Class

' 8042-compatible host interface plus an AT keyboard serial transport.
' The controller has a one-byte output buffer.  Host key events wait on the
' keyboard side of the serial link and enter the 8042 one frame at a time;
' only a newly-filled keyboard output buffer generates IRQ1.
' IBM PC/AT 8042 keyboard controller.  The controller and keyboard are separate
' devices: the keyboard emits its selected scan set on an 11-bit serial link;
' the 8042 owns the host-facing buffers/status, scan-set-2 translation, IRQ1,
' keyboard inhibit, A20, RESET#, and controller command/RAM semantics.
' IBM PC/AT 8042 keyboard controller.  The controller and keyboard are separate
' devices: the keyboard emits its selected scan set on an 11-bit serial link;
' the 8042 owns the host-facing buffers/status, scan-set-2 translation, IRQ1,
' keyboard inhibit, A20, RESET#, and controller command/RAM semantics.
' CROMWELL KEYBOARD REALITY BRICK 3 CONTROLLER
' IBM PC/AT 8042 keyboard controller.  The controller and keyboard are separate
' devices: the keyboard emits its selected scan set on an 11-bit serial link;
' the 8042 owns the host-facing buffers/status, scan-set-2 translation, IRQ1,
' keyboard inhibit, A20, RESET#, and controller command/RAM semantics.
' CROMWELL KEYBOARD REALITY BRICK 4 CONTROLLER
' IBM PC/AT 8042 keyboard controller.  The controller and keyboard are separate
' devices: the keyboard emits its selected scan set on an 11-bit serial link;
' the 8042 owns the host-facing buffers/status, scan-set-2 translation, IRQ1,
' keyboard inhibit, A20, RESET#, and controller command/RAM semantics.
' CROMWELL KEYBOARD REALITY BRICK 4 CONTROLLER
' CROMWELL QBASIC RAW KEYBOARD TRACE
' IBM PC/AT 8042 keyboard controller.  The controller and keyboard are separate
' devices: the keyboard emits its selected scan set on an 11-bit serial link;
' the 8042 owns the host-facing buffers/status, scan-set-2 translation, IRQ1,
' keyboard inhibit, A20, RESET#, and controller command/RAM semantics.
Public Class KeyboardController8042
    Implements IPortDevice, IClockedDevice, IClockWakeSource, IResettableDevice, IMotherboardLocalPortDevice

    Private Const CommandByteIrq1Enable As Byte = &H1
    Private Const CommandByteSystemFlag As Byte = &H4
    Private Const CommandByteInhibitOverride As Byte = &H8
    Private Const CommandByteKeyboardDisabled As Byte = &H10
    Private Const CommandByteTranslation As Byte = &H40

    Private Const KeyboardSerialClockHz As Long = 12000L
    Private Const KeyboardFrameBits As Integer = 11
    Private Const PicosecondsPerSecond As Long = 1000000000000L
    Private Const ControllerInputDelayPicoseconds As Long = 12000000L ' ~12 us
    Private Const KeyboardResponseTimeoutPicoseconds As Long = 25000000000L ' IBM controller command-response limit: 25 ms
    Private Const KeyboardReceiveFrameTimeoutPicoseconds As Long = 2000000000L ' 2 ms from keyboard start to completion
    Private Const KeyboardTransmitStartTimeoutPicoseconds As Long = 15000000000L ' keyboard must start clocking within 15 ms
    Private Const KeyboardTransmitFrameTimeoutPicoseconds As Long = 2000000000L ' once started, complete within 2 ms

    Private Enum LinkDirection As Byte
        None = 0
        KeyboardToController = 1
        ControllerToKeyboard = 2
    End Enum

    Private Enum DiagnosticLinkFault As Byte
        None = 0
        StallKeyboardReceive = 1
        StallControllerStart = 2
        StallControllerFrame = 3
    End Enum

    Private Shared ReadOnly TranslationTable() As Byte = {
        &HFF,&H43,&H41,&H3F,&H3D,&H3B,&H3C,&H58,&H64,&H44,&H42,&H40,&H3E,&HF,&H29,&H59,
        &H65,&H38,&H2A,&H70,&H1D,&H10,&H2,&H5A,&H66,&H71,&H2C,&H1F,&H1E,&H11,&H3,&H5B,
        &H67,&H2E,&H2D,&H20,&H12,&H5,&H4,&H5C,&H68,&H39,&H2F,&H21,&H14,&H13,&H6,&H5D,
        &H69,&H31,&H30,&H23,&H22,&H15,&H7,&H5E,&H6A,&H72,&H32,&H24,&H16,&H8,&H9,&H5F,
        &H6B,&H33,&H25,&H17,&H18,&HB,&HA,&H60,&H6C,&H34,&H35,&H26,&H27,&H19,&HC,&H61,
        &H6D,&H73,&H28,&H74,&H1A,&HD,&H62,&H6E,&H3A,&H36,&H1C,&H1B,&H75,&H2B,&H63,&H76,
        &H55,&H56,&H77,&H78,&H79,&H7A,&HE,&H7B,&H7C,&H4F,&H7D,&H4B,&H47,&H7E,&H7F,&H6F,
        &H52,&H53,&H50,&H4C,&H4D,&H48,&H1,&H45,&H57,&H4E,&H51,&H4A,&H37,&H49,&H46,&H54
    }

    Private ReadOnly _pic As Pic8259
    Private ReadOnly _keyboard As AtKeyboard101
    Private ReadOnly _controllerRam(31) As Byte
    Private ReadOnly _controllerOutputQueue As New Collections.Generic.Queue(Of Byte)()
    Private ReadOnly _hostToKeyboardQueue As New Collections.Generic.Queue(Of Byte)()

    Private _outputBufferFull As Boolean
    Private _outputBufferValue As Byte
    Private _outputBufferContainsKeyboardData As Boolean
    Private _outputBufferKeyboardCommandResponse As Boolean
    Private _inputBufferFull As Boolean
    Private _pendingInputPort As UInt16
    Private _pendingInputValue As Byte
    Private _inputDelayRemaining As Long
    Private _pendingControllerRamIndex As Integer = -1
    Private _pendingControllerCommand As Integer = -1
    Private _lastWriteWasCommand As Boolean
    Private _transmitTimeout As Boolean
    Private _receiveTimeout As Boolean
    Private _parityError As Boolean
    Private _outputPort As Byte = &H1
    Private _inputPort As Byte = &HA0
    Private _linkDirection As LinkDirection
    Private _linkFrameValue As Byte
    Private _linkFrameWord As UInt16
    Private _linkFrameIsCommandResponse As Boolean
    Private _linkFrameBitsRemaining As Integer
    Private _keyboardClockNumerator As Long
    Private _translationBreakPending As Boolean
    Private _pollInputLow As Boolean
    Private _pollInputHigh As Boolean
    Private _awaitingKeyboardResponse As Boolean
    Private _awaitingResendData As Boolean
    Private _keyboardResponseTimeoutRemaining As Long
    Private _keyboardFramesReceived As ULong
    Private _keyboardFramesTransmitted As ULong
    Private _keyboardReceiveParityRetries As ULong
    Private _keyboardReceiveErrors As ULong
    Private _keyboardTransmitErrors As ULong
    Private _receiveParityRetryActive As Boolean
    Private _linkElapsedPicoseconds As Long
    Private _linkFault As DiagnosticLinkFault
    Private _diagnosticCorruptKeyboardFrames As Integer
    Private _diagnosticCorruptKeyboardResponses As Integer
    Private _diagnosticCorruptControllerFrames As Integer
    Private _diagnosticDropKeyboardResponses As Integer
    Private _diagnosticNextLinkFault As DiagnosticLinkFault

    ' Host-only forensic trace.  This is not guest-visible state and does not
    ' change timing, scan codes, IRQ routing, or controller semantics.
    Private Const DiagnosticTraceCapacity As Integer = 160
    Private ReadOnly _diagnosticTrace As New Collections.Generic.Queue(Of String)()
    Private _diagnosticTraceSequence As ULong
    Private _port60ReadCount As ULong
    Private _port64ReadCount As ULong
    Private _irq1AssertionCount As ULong
    Private _lastPort60Value As Byte
    Private _diagnosticPort64RunValid As Boolean
    Private _diagnosticPort64RunValue As Byte
    Private _diagnosticPort64RunLength As ULong

    Public Event A20Changed(enabled As Boolean)
    Public Event ResetRequested()
    Public Event KeyboardReceiveActivity()
    Public Event KeyboardTransmitActivity()

    Public Sub New(pic As Pic8259, keyboard As AtKeyboard101)
        If pic Is Nothing Then Throw New ArgumentNullException(NameOf(pic))
        If keyboard Is Nothing Then Throw New ArgumentNullException(NameOf(keyboard))
        _pic = pic
        _keyboard = keyboard
        ResetDevice()
    End Sub

    Public Sub ResetDevice() Implements IResettableDevice.ResetDevice
        Array.Clear(_controllerRam, 0, _controllerRam.Length)
        _controllerRam(0) = CommandByteIrq1Enable
        _controllerOutputQueue.Clear()
        _hostToKeyboardQueue.Clear()
        _outputBufferFull = False
        _outputBufferValue = 0
        _outputBufferContainsKeyboardData = False
        _outputBufferKeyboardCommandResponse = False
        _inputBufferFull = False
        _inputDelayRemaining = 0
        _pendingControllerRamIndex = -1
        _pendingControllerCommand = -1
        _lastWriteWasCommand = False
        _transmitTimeout = False
        _receiveTimeout = False
        _parityError = False
        _outputPort = &H1
        _linkDirection = LinkDirection.None
        _linkFrameWord = 0
        _linkFrameIsCommandResponse = False
        _linkFrameBitsRemaining = 0
        _keyboardClockNumerator = 0
        _translationBreakPending = False
        _pollInputLow = False
        _pollInputHigh = False
        _awaitingKeyboardResponse = False
        _awaitingResendData = False
        _keyboardResponseTimeoutRemaining = 0
        _keyboardFramesReceived = 0
        _keyboardFramesTransmitted = 0
        _keyboardReceiveParityRetries = 0
        _keyboardReceiveErrors = 0
        _keyboardTransmitErrors = 0
        _receiveParityRetryActive = False
        _linkElapsedPicoseconds = 0
        _linkFault = DiagnosticLinkFault.None
        _diagnosticCorruptKeyboardFrames = 0
        _diagnosticCorruptKeyboardResponses = 0
        _diagnosticCorruptControllerFrames = 0
        _diagnosticDropKeyboardResponses = 0
        _diagnosticNextLinkFault = DiagnosticLinkFault.None
        _diagnosticTrace.Clear()
        _diagnosticTraceSequence = 0
        _port60ReadCount = 0
        _port64ReadCount = 0
        _irq1AssertionCount = 0
        _lastPort60Value = 0
        _diagnosticPort64RunValid = False
        _diagnosticPort64RunValue = 0
        _diagnosticPort64RunLength = 0UL
        _keyboard.PowerOnReset()
        UpdateKeyboardInhibitState()
        _pic.ClearIrq(1)
        RaiseEvent A20Changed(False)
    End Sub

    Public Property InputPortValue As Byte
        Get
            Return _inputPort
        End Get
        Set(value As Byte)
            _inputPort = value
            UpdateKeyboardInhibitState()
            BeginLinkFrameIfPossible()
        End Set
    End Property

    Public ReadOnly Property KeyboardLedState As Byte
        Get
            Return _keyboard.LedState
        End Get
    End Property

    Public ReadOnly Property TypematicByte As Byte
        Get
            Return _keyboard.TypematicByte
        End Get
    End Property

    Public ReadOnly Property KeyboardScanCodeSet As Byte
        Get
            Return _keyboard.ScanCodeSet
        End Get
    End Property

    Public ReadOnly Property TranslationEnabled As Boolean
        Get
            Return (_controllerRam(0) And CommandByteTranslation) <> 0
        End Get
    End Property

    Public ReadOnly Property StatusRegister As Byte
        Get
            Return ComposeStatus()
        End Get
    End Property

    Public ReadOnly Property OutputBufferFull As Boolean
        Get
            Return _outputBufferFull
        End Get
    End Property

    Public ReadOnly Property InputBufferFull As Boolean
        Get
            Return _inputBufferFull
        End Get
    End Property

    Public ReadOnly Property KeyboardInterfaceEnabled As Boolean
        Get
            Return Not ControllerKeyboardClockDisabled
        End Get
    End Property

    Public ReadOnly Property KeyboardLinkBusy As Boolean
        Get
            Return _linkDirection <> LinkDirection.None
        End Get
    End Property

    Public ReadOnly Property KeyboardFramesReceived As ULong
        Get
            Return _keyboardFramesReceived
        End Get
    End Property

    Public ReadOnly Property KeyboardFramesTransmitted As ULong
        Get
            Return _keyboardFramesTransmitted
        End Get
    End Property

    Public ReadOnly Property KeyboardReceiveParityRetries As ULong
        Get
            Return _keyboardReceiveParityRetries
        End Get
    End Property

    Public ReadOnly Property KeyboardReceiveErrors As ULong
        Get
            Return _keyboardReceiveErrors
        End Get
    End Property

    Public ReadOnly Property KeyboardTransmitErrors As ULong
        Get
            Return _keyboardTransmitErrors
        End Get
    End Property

    Public ReadOnly Property Port60ReadCount As ULong
        Get
            Return _port60ReadCount
        End Get
    End Property

    Public ReadOnly Property Port64ReadCount As ULong
        Get
            Return _port64ReadCount
        End Get
    End Property

    Public ReadOnly Property Irq1AssertionCount As ULong
        Get
            Return _irq1AssertionCount
        End Get
    End Property

    Public ReadOnly Property LastPort60Value As Byte
        Get
            Return _lastPort60Value
        End Get
    End Property

    Public Sub ClearDiagnosticTrace()
        _diagnosticTrace.Clear()
        _diagnosticTraceSequence = 0
        TraceDiagnostic("trace cleared")
    End Sub

    Public Function GetDiagnosticTrace() As String
        Dim lines As String() = _diagnosticTrace.ToArray()
        Dim currentRunInBed As String =
            If(_diagnosticPort64RunValid,
               "CURRENT CPU IN 64h -> " & _diagnosticPort64RunValue.ToString("X2") &
               " repeated " & _diagnosticPort64RunLength.ToString("N0") & " times",
               "CURRENT CPU IN 64h run: none")
        If lines.Length = 0 Then Return "(keyboard wire trace is empty)" & Environment.NewLine & currentRunInBed
        Return String.Join(Environment.NewLine, lines) & Environment.NewLine & currentRunInBed
    End Function

    Private Sub ObserveDiagnosticPort64ReadInBed(valueInBed As Byte)
        If _diagnosticPort64RunValid AndAlso valueInBed = _diagnosticPort64RunValue Then
            _diagnosticPort64RunLength += 1UL
            Return
        End If
        If _diagnosticPort64RunValid Then
            TraceDiagnostic("CPU IN 64h -> " & _diagnosticPort64RunValue.ToString("X2") &
                            " repeated " & _diagnosticPort64RunLength.ToString("N0") & " times")
        End If
        _diagnosticPort64RunValid = True
        _diagnosticPort64RunValue = valueInBed
        _diagnosticPort64RunLength = 1UL
    End Sub

    Private Sub TraceDiagnostic(message As String)
        _diagnosticTraceSequence += 1UL
        Dim line As String = "#" & _diagnosticTraceSequence.ToString("000000") & " " & message
        While _diagnosticTrace.Count >= DiagnosticTraceCapacity
            _diagnosticTrace.Dequeue()
        End While
        _diagnosticTrace.Enqueue(line)
    End Sub

    ' Host-only diagnostic line-fault injectors.  These are internal test hooks,
    ' not I/O ports and not guest-visible compatibility behavior.
    Friend Sub DiagnosticCorruptNextKeyboardFrames(Optional count As Integer = 1)
        _diagnosticCorruptKeyboardFrames = Math.Max(_diagnosticCorruptKeyboardFrames, Math.Max(0, count))
    End Sub

    Friend Sub DiagnosticCorruptNextKeyboardResponses(Optional count As Integer = 1)
        _diagnosticCorruptKeyboardResponses = Math.Max(_diagnosticCorruptKeyboardResponses, Math.Max(0, count))
    End Sub

    Friend Sub DiagnosticCorruptNextControllerFrames(Optional count As Integer = 1)
        _diagnosticCorruptControllerFrames = Math.Max(_diagnosticCorruptControllerFrames, Math.Max(0, count))
    End Sub

    Friend Sub DiagnosticDropNextKeyboardResponses(Optional count As Integer = 1)
        _diagnosticDropKeyboardResponses = Math.Max(_diagnosticDropKeyboardResponses, Math.Max(0, count))
    End Sub

    Friend Sub DiagnosticStallNextKeyboardFrame()
        _diagnosticNextLinkFault = DiagnosticLinkFault.StallKeyboardReceive
    End Sub

    Friend Sub DiagnosticStallNextControllerStart()
        _diagnosticNextLinkFault = DiagnosticLinkFault.StallControllerStart
    End Sub

    Friend Sub DiagnosticStallNextControllerFrame()
        _diagnosticNextLinkFault = DiagnosticLinkFault.StallControllerFrame
    End Sub

    Public Function HandlesPort(port As UInt16) As Boolean Implements IPortDevice.HandlesPort
        Return port = &H60US OrElse port = &H64US
    End Function

    Public Function ReadPort(port As UInt16) As Byte Implements IPortDevice.ReadPort
        If port = &H64US Then
            _port64ReadCount += 1UL
            Dim statusInBed As Byte = ComposeStatus()
            ObserveDiagnosticPort64ReadInBed(statusInBed)
            Return statusInBed
        End If
        _port60ReadCount += 1UL
        If Not _outputBufferFull Then
            ' CROMWELL 8042 DBB RETENTION FIX
            ' UPI-41/8042 host reads clear OBF, not the DBB data latch itself.
            ' With OBF clear there is no *new* byte, but a raw read still sees
            ' the value retained in the controller output data register until
            ' firmware writes a replacement byte.
            _lastPort60Value = _outputBufferValue
            TraceDiagnostic("CPU IN 60h -> " & _outputBufferValue.ToString("X2") &
                            " (OBF empty; retained DBB)")
            Return _outputBufferValue
        End If

        Dim value As Byte = _outputBufferValue
        _lastPort60Value = value
        TraceDiagnostic("CPU IN 60h -> " & value.ToString("X2") &
                        "  cmdresp=" & If(_outputBufferKeyboardCommandResponse, "1", "0") &
                        "  translation=" & If(TranslationEnabled, "1", "0"))
        Dim wasKeyboardData As Boolean = _outputBufferContainsKeyboardData
        Dim wasCommandResponse As Boolean = _outputBufferKeyboardCommandResponse
        _outputBufferFull = False
        _outputBufferContainsKeyboardData = False
        _outputBufferKeyboardCommandResponse = False
        _pic.ClearIrq(1)

        ' Consuming port 60h is the host's acceptance of a keyboard byte.
        ' Release the keyboard clock before telling the keyboard its Reset ACK
        ' was accepted; the keyboard then observes the required 500 us idle
        ' interval before beginning BAT.
        UpdateKeyboardInhibitState()
        If wasKeyboardData Then _keyboard.NotifyControllerAcceptedByte(value, wasCommandResponse)

        TryFeedOutputBuffer()
        BeginLinkFrameIfPossible()
        Return value
    End Function

    Private Function ComposeStatus() As Byte
        Dim status As Byte
        If _outputBufferFull Then status = CByte(status Or &H1)
        If _inputBufferFull Then status = CByte(status Or &H2)
        If (_controllerRam(0) And CommandByteSystemFlag) <> 0 Then status = CByte(status Or &H4)
        If _lastWriteWasCommand Then status = CByte(status Or &H8)
        ' Original PC/AT bit 4 reflects the physical keyboard-inhibit switch,
        ' not command-byte bit 4 and not the inhibit-override setting.
        If (_inputPort And &H80) <> 0 Then status = CByte(status Or &H10)
        If _transmitTimeout Then status = CByte(status Or &H20)
        If _receiveTimeout Then status = CByte(status Or &H40)
        If _parityError Then status = CByte(status Or &H80)

        If _pollInputLow Then
            status = CByte((status And &HF) Or ((_inputPort And &HF) << 4))
        ElseIf _pollInputHigh Then
            status = CByte((status And &HF) Or (_inputPort And &HF0))
        End If
        Return status
    End Function

    Public Sub WritePort(port As UInt16, value As Byte) Implements IPortDevice.WritePort
        TraceDiagnostic("CPU OUT " & port.ToString("X2") & "h <- " & value.ToString("X2"))
        If _inputBufferFull Then
            _transmitTimeout = True
            Return
        End If

        _lastWriteWasCommand = (port = &H64US)
        _inputBufferFull = True
        _pendingInputPort = port
        _pendingInputValue = value
        _inputDelayRemaining = ControllerInputDelayPicoseconds
    End Sub

    Public Function PicosecondsUntilNextWakeEvent() As Long Implements IClockWakeSource.PicosecondsUntilNextWakeEvent
        Dim earliest As Long = Long.MaxValue

        Dim keyboardEvent As Long = _keyboard.PicosecondsUntilNextEvent
        If keyboardEvent > 0 AndAlso keyboardEvent < earliest Then earliest = keyboardEvent

        If _awaitingKeyboardResponse AndAlso
           _keyboardResponseTimeoutRemaining > 0 AndAlso
           _keyboardResponseTimeoutRemaining < earliest Then
            earliest = _keyboardResponseTimeoutRemaining
        End If

        If _inputBufferFull AndAlso _inputDelayRemaining > 0 AndAlso _inputDelayRemaining < earliest Then
            earliest = _inputDelayRemaining
        End If

        If _linkDirection <> LinkDirection.None Then
            Dim linkEvent As Long

            If _linkFault <> DiagnosticLinkFault.None Then
                Dim timeout As Long
                Select Case _linkFault
                    Case DiagnosticLinkFault.StallKeyboardReceive
                        timeout = KeyboardReceiveFrameTimeoutPicoseconds
                    Case DiagnosticLinkFault.StallControllerStart
                        timeout = KeyboardTransmitStartTimeoutPicoseconds
                    Case DiagnosticLinkFault.StallControllerFrame
                        timeout = KeyboardTransmitFrameTimeoutPicoseconds
                End Select
                linkEvent = Math.Max(1L, timeout - _linkElapsedPicoseconds)
            ElseIf _linkFrameBitsRemaining > 0 Then
                Dim numeratorNeeded As Long =
                    CLng(_linkFrameBitsRemaining) * PicosecondsPerSecond - _keyboardClockNumerator
                If numeratorNeeded <= 0 Then
                    linkEvent = 1
                Else
                    linkEvent = Math.Max(1L,
                        (numeratorNeeded + KeyboardSerialClockHz - 1L) \ KeyboardSerialClockHz)
                End If
            End If

            If linkEvent > 0 AndAlso linkEvent < earliest Then earliest = linkEvent
        ElseIf Not ControllerKeyboardClockDisabled AndAlso
               (_hostToKeyboardQueue.Count > 0 OrElse _keyboard.HasByteToTransmit) Then
            ' A queued byte can start clocking immediately at the next motherboard
            ' advancement boundary.
            earliest = 1
        End If

        Return earliest
    End Function

    Public Sub AdvanceTime(elapsedPicoseconds As Long) Implements IClockedDevice.AdvanceTime
        If elapsedPicoseconds < 0 Then Throw New ArgumentOutOfRangeException(NameOf(elapsedPicoseconds))
        If elapsedPicoseconds = 0 Then Return

        ' Advance only serial activity which existed at the beginning of this
        ' interval. Keyboard/BAT/IBF events which mature at the endpoint may start
        ' a new frame, but that new frame must not consume time which elapsed
        ' before it existed.
        BeginLinkFrameIfPossible()
        AdvanceActiveLink(elapsedPicoseconds)

        _keyboard.AdvanceTime(elapsedPicoseconds)

        If _awaitingKeyboardResponse Then
            _keyboardResponseTimeoutRemaining -= elapsedPicoseconds
            If _keyboardResponseTimeoutRemaining <= 0 Then
                _awaitingKeyboardResponse = False
                _awaitingResendData = False
                _keyboardResponseTimeoutRemaining = 0
                _transmitTimeout = True
                _receiveTimeout = True
                _keyboardTransmitErrors += 1UL
                ' IBM AT controller firmware reports FEh when a system-to-keyboard
                ' transfer completed but the keyboard failed to answer in time.
                PresentOutput(&HFE, keyboardData:=True, keyboardCommandResponse:=True)
            End If
        End If

        If _inputBufferFull Then
            _inputDelayRemaining -= elapsedPicoseconds
            If _inputDelayRemaining <= 0 Then
                _inputBufferFull = False
                ProcessSystemWrite(_pendingInputPort, _pendingInputValue)
            End If
        End If

        ' Endpoint events may have queued a new byte. Arm its frame now; its clock
        ' starts with the next motherboard interval, never retroactively.
        BeginLinkFrameIfPossible()
    End Sub

    Private Sub AdvanceActiveLink(elapsedPicoseconds As Long)
        If _linkDirection = LinkDirection.None Then Return

        _linkElapsedPicoseconds += elapsedPicoseconds

        If _linkFault <> DiagnosticLinkFault.None Then
            Select Case _linkFault
                Case DiagnosticLinkFault.StallKeyboardReceive
                    If _linkElapsedPicoseconds >= KeyboardReceiveFrameTimeoutPicoseconds Then
                        FinishKeyboardReceiveTimeout()
                    End If
                Case DiagnosticLinkFault.StallControllerStart
                    If _linkElapsedPicoseconds >= KeyboardTransmitStartTimeoutPicoseconds Then
                        FinishControllerTransmitTimeout()
                    End If
                Case DiagnosticLinkFault.StallControllerFrame
                    If _linkElapsedPicoseconds >= KeyboardTransmitFrameTimeoutPicoseconds Then
                        FinishControllerTransmitTimeout()
                    End If
            End Select
            Return
        End If

        _keyboardClockNumerator += elapsedPicoseconds * KeyboardSerialClockHz
        Dim clocks As Long = _keyboardClockNumerator \ PicosecondsPerSecond
        If clocks <= 0 Then Return
        _keyboardClockNumerator -= clocks * PicosecondsPerSecond

        While clocks > 0 AndAlso _linkDirection <> LinkDirection.None
            If clocks < _linkFrameBitsRemaining Then
                _linkFrameBitsRemaining -= CInt(clocks)
                clocks = 0
            Else
                clocks -= _linkFrameBitsRemaining
                CompleteLinkFrame()
                BeginLinkFrameIfPossible()
            End If
        End While
    End Sub

    Private Sub BeginLinkFrameIfPossible()
        If _linkDirection <> LinkDirection.None Then Return

        ' Command-byte bit 4 physically holds the keyboard clock low and stops
        ' either direction of the serial interface.  The chassis key-lock input
        ' is different: transmissions TO the keyboard remain legal, while scan
        ' codes received FROM it may later be discarded by the 8042 firmware.
        If ControllerKeyboardClockDisabled Then Return

        If _hostToKeyboardQueue.Count > 0 AndAlso Not _awaitingKeyboardResponse Then
            _linkDirection = LinkDirection.ControllerToKeyboard
            _linkFrameValue = _hostToKeyboardQueue.Dequeue()
            _linkFrameWord = EncodeSerialFrame(_linkFrameValue)
            If _diagnosticCorruptControllerFrames > 0 Then
                _diagnosticCorruptControllerFrames -= 1
                _linkFrameWord = CUShort(_linkFrameWord Xor CUShort(1 << 9))
            End If
            _linkFrameIsCommandResponse = False
            _linkFrameBitsRemaining = KeyboardFrameBits
            _linkElapsedPicoseconds = 0
            If _diagnosticNextLinkFault = DiagnosticLinkFault.StallControllerStart OrElse
               _diagnosticNextLinkFault = DiagnosticLinkFault.StallControllerFrame Then
                _linkFault = _diagnosticNextLinkFault
                _diagnosticNextLinkFault = DiagnosticLinkFault.None
            Else
                _linkFault = DiagnosticLinkFault.None
            End If
            TraceDiagnostic("WIRE system->kbd raw " & _linkFrameValue.ToString("X2"))
            RaiseEvent KeyboardTransmitActivity()
            Return
        End If

        If _outputBufferFull OrElse _controllerOutputQueue.Count > 0 Then Return
        Dim value As Byte
        Dim commandResponse As Boolean
        If _keyboard.TryDequeueTransmitByte(value, commandResponse) Then
            If commandResponse AndAlso _diagnosticDropKeyboardResponses > 0 Then
                _diagnosticDropKeyboardResponses -= 1
                Return
            End If

            _linkDirection = LinkDirection.KeyboardToController
            _linkFrameValue = value
            _linkFrameWord = EncodeSerialFrame(value)
            If commandResponse AndAlso _diagnosticCorruptKeyboardResponses > 0 Then
                _diagnosticCorruptKeyboardResponses -= 1
                _linkFrameWord = CUShort(_linkFrameWord Xor CUShort(1 << 9))
            ElseIf _diagnosticCorruptKeyboardFrames > 0 Then
                _diagnosticCorruptKeyboardFrames -= 1
                _linkFrameWord = CUShort(_linkFrameWord Xor CUShort(1 << 9))
            End If
            _linkFrameIsCommandResponse = commandResponse
            _linkFrameBitsRemaining = KeyboardFrameBits
            _linkElapsedPicoseconds = 0
            If _diagnosticNextLinkFault = DiagnosticLinkFault.StallKeyboardReceive Then
                _linkFault = _diagnosticNextLinkFault
                _diagnosticNextLinkFault = DiagnosticLinkFault.None
            Else
                _linkFault = DiagnosticLinkFault.None
            End If
            TraceDiagnostic("WIRE kbd->8042 raw " & _linkFrameValue.ToString("X2") &
                            "  cmdresp=" & If(_linkFrameIsCommandResponse, "1", "0"))
            RaiseEvent KeyboardReceiveActivity()
        End If
    End Sub

    Private Sub CompleteLinkFrame()
        Dim direction As LinkDirection = _linkDirection
        Dim commandResponse As Boolean = _linkFrameIsCommandResponse
        Dim frameWord As UInt16 = _linkFrameWord
        Dim frameValue As Byte = _linkFrameValue

        ClearActiveLink()

        If direction = LinkDirection.ControllerToKeyboard Then
            _keyboardFramesTransmitted += 1UL
            ' The keyboard is the physical receiver in this direction and owns
            ' parity/start/stop validation. A bad host frame causes FEh from the
            ' keyboard rather than being silently "decoded" by the controller.
            _keyboard.ReceiveHostSerialFrame(frameWord)
            _awaitingKeyboardResponse = True
            _awaitingResendData = (frameValue = &HFE)
            _keyboardResponseTimeoutRemaining = KeyboardResponseTimeoutPicoseconds
            Return
        End If

        If direction <> LinkDirection.KeyboardToController Then Return

        ' The keyboard regards the byte as transmitted when its stop bit leaves
        ' the wire.  It cannot know whether the 8042 accepted parity, so FEh
        ' must resend this byte even when controller-side validation fails.
        _keyboard.NotifyByteTransmitted(frameValue, commandResponse)

        Dim value As Byte
        If Not TryDecodeSerialFrame(frameWord, value) Then
            HandleKeyboardParityError(commandResponse)
            Return
        End If

        _keyboardFramesReceived += 1UL
        If _awaitingResendData Then
            _awaitingResendData = False
            _awaitingKeyboardResponse = False
            _keyboardResponseTimeoutRemaining = 0
        End If
        If Not commandResponse Then _receiveParityRetryActive = False
        AcceptKeyboardByte(value, commandResponse)
    End Sub

    Private Sub ClearActiveLink()
        _linkDirection = LinkDirection.None
        _linkFrameWord = 0
        _linkFrameIsCommandResponse = False
        _linkFrameBitsRemaining = 0
        _linkElapsedPicoseconds = 0
        _linkFault = DiagnosticLinkFault.None
    End Sub

    Private Sub HandleKeyboardParityError(commandResponse As Boolean)
        If commandResponse Then
            ' This was the keyboard's answer to a system transmission.  IBM AT
            ' firmware reports FEh and sets transmit-timeout + parity; no retry.
            _awaitingKeyboardResponse = False
            _awaitingResendData = False
            _keyboardResponseTimeoutRemaining = 0
            _transmitTimeout = True
            _parityError = True
            _keyboardTransmitErrors += 1UL
            PresentOutput(&HFE, keyboardData:=True, keyboardCommandResponse:=True)
            Return
        End If

        ' Normal keyboard->controller data receives one automatic Resend attempt.
        ' If the retransmission also has bad parity, return the controller's
        ' receive-error byte and leave the parity status bit set.
        If Not _receiveParityRetryActive Then
            _receiveParityRetryActive = True
            _keyboardReceiveParityRetries += 1UL
            _hostToKeyboardQueue.Enqueue(&HFE)
            BeginLinkFrameIfPossible()
            Return
        End If

        _receiveParityRetryActive = False
        _awaitingResendData = False
        _awaitingKeyboardResponse = False
        _keyboardResponseTimeoutRemaining = 0
        _parityError = True
        _keyboardReceiveErrors += 1UL
        PresentReceiveError()
    End Sub

    Private Sub FinishKeyboardReceiveTimeout()
        If _linkDirection <> LinkDirection.KeyboardToController Then Return
        ClearActiveLink()
        _receiveParityRetryActive = False
        _awaitingResendData = False
        _awaitingKeyboardResponse = False
        _keyboardResponseTimeoutRemaining = 0
        _receiveTimeout = True
        _keyboardReceiveErrors += 1UL
        PresentReceiveError()
    End Sub

    Private Sub FinishControllerTransmitTimeout()
        If _linkDirection <> LinkDirection.ControllerToKeyboard Then Return
        ClearActiveLink()
        _transmitTimeout = True
        _keyboardTransmitErrors += 1UL
        PresentOutput(&HFE, keyboardData:=True, keyboardCommandResponse:=True)
    End Sub

    Private Sub PresentReceiveError()
        PresentOutput(ReceiveErrorByte(), keyboardData:=True, keyboardCommandResponse:=False)
    End Sub

    Private Function ReceiveErrorByte() As Byte
        ' IBM's default controller mode (CCB bits 5, 6 and 7 all zero) reports
        ' receive errors as 00h.  Other modes report FFh.
        Return If((_controllerRam(0) And &HE0) = 0, CByte(&H0), CByte(&HFF))
    End Function

    Private Shared Function EncodeSerialFrame(value As Byte) As UInt16
        ' AT keyboard wire format: start=0, eight data bits LSB first,
        ' odd parity, stop=1.
        Dim frame As UInt16 = 0US
        Dim parityBit As Integer = 1
        For bit As Integer = 0 To 7
            If (value And (1 << bit)) <> 0 Then
                frame = CUShort(frame Or CUShort(1 << (bit + 1)))
                parityBit = parityBit Xor 1
            End If
        Next
        If parityBit <> 0 Then frame = CUShort(frame Or CUShort(1 << 9))
        frame = CUShort(frame Or CUShort(1 << 10))
        Return frame
    End Function

    Private Shared Function TryDecodeSerialFrame(frame As UInt16, ByRef value As Byte) As Boolean
        If (frame And 1US) <> 0US Then Return False
        If (frame And CUShort(1 << 10)) = 0US Then Return False

        Dim decoded As Integer = 0
        Dim oneCount As Integer = 0
        For bit As Integer = 0 To 7
            If (frame And CUShort(1 << (bit + 1))) <> 0US Then
                decoded = decoded Or (1 << bit)
                oneCount += 1
            End If
        Next
        If (frame And CUShort(1 << 9)) <> 0US Then oneCount += 1
        If (oneCount And 1) = 0 Then Return False

        value = CByte(decoded)
        Return True
    End Function

    Private Sub AcceptKeyboardByte(value As Byte, commandResponse As Boolean)
        ' Command-response bytes are protocol data, not scan codes.  They bypass
        ' the Set-2 -> Set-1 translator even while compatibility translation is
        ' enabled.  This matters for values such as the second Enhanced Keyboard
        ' ID byte 83h, which is also a valid Set-2 make code (F7).
        If commandResponse Then
            TraceDiagnostic("8042 protocol byte " & value.ToString("X2") & " bypasses translation")
            _awaitingKeyboardResponse = False
            _awaitingResendData = False
            _keyboardResponseTimeoutRemaining = 0
            _translationBreakPending = False
            PresentOutput(value, keyboardData:=True, keyboardCommandResponse:=True)
            Return
        End If

        ' The front-panel key lock inhibits keystrokes only.  Command responses
        ' still reach the system, and system-to-keyboard traffic remains legal.
        If PhysicalKeyboardInhibited AndAlso Not InhibitOverrideEnabled Then Return

        If TranslationEnabled Then
            If value = &HF0 Then
                TraceDiagnostic("8042 translate F0 break prefix")
                _translationBreakPending = True
                Return
            End If
            If value = &HE0 OrElse value = &HE1 Then
                TraceDiagnostic("8042 pass prefix " & value.ToString("X2"))
                PresentOutput(value, keyboardData:=True, keyboardCommandResponse:=False)
                Return
            End If

            Dim translated As Byte = Translate8042(value)
            Dim translatedBeforeBreak As Byte = translated
            If _translationBreakPending Then
                translated = CByte(translated Or &H80)
                _translationBreakPending = False
            End If
            TraceDiagnostic("8042 translate " & value.ToString("X2") & " -> " &
                            translated.ToString("X2") &
                            If(translated <> translatedBeforeBreak, " (break)", ""))
            PresentOutput(translated, keyboardData:=True, keyboardCommandResponse:=False)
        Else
            _translationBreakPending = False
            PresentOutput(value, keyboardData:=True, keyboardCommandResponse:=False)
        End If
    End Sub

    Private Shared Function Translate8042(value As Byte) As Byte
        If value < &H80 Then Return TranslationTable(CInt(value))
        If value = &H83 Then Return &H41
        If value = &H84 Then Return &H54
        Return value
    End Function

    Private Sub ProcessSystemWrite(port As UInt16, value As Byte)
        _transmitTimeout = False
        _receiveTimeout = False
        _parityError = False
        _pollInputLow = False
        _pollInputHigh = False

        If port = &H64US Then
            HandleControllerCommand(value)
            Return
        End If

        If _pendingControllerRamIndex >= 0 Then
            Dim index As Integer = _pendingControllerRamIndex
            _pendingControllerRamIndex = -1
            _controllerRam(index) = value
            If index = 0 Then ApplyCommandByte(value)
            Return
        End If

        If _pendingControllerCommand = &HD1 Then
            _pendingControllerCommand = -1
            ApplyOutputPort(value)
            Return
        End If

        If _pendingControllerCommand = &HD2 Then
            _pendingControllerCommand = -1
            PresentOutput(value, keyboardData:=True, keyboardCommandResponse:=False)
            Return
        End If

        _hostToKeyboardQueue.Enqueue(value)
        BeginLinkFrameIfPossible()
    End Sub

    Private Sub HandleControllerCommand(value As Byte)
        If value >= &H20 AndAlso value <= &H3F Then
            QueueControllerOutput(_controllerRam(value And &H1F))
            Return
        End If
        If value >= &H60 AndAlso value <= &H7F Then
            _pendingControllerRamIndex = value And &H1F
            Return
        End If

        Select Case value
            Case &HAA ' controller self-test
                _controllerRam(0) = CByte((_controllerRam(0) Or CommandByteSystemFlag Or CommandByteKeyboardDisabled))
                UpdateKeyboardInhibitState()
                QueueControllerOutput(&H55)
            Case &HAB ' keyboard interface test
                QueueControllerOutput(&H0)
            Case &HAC ' diagnostic dump: 16 RAM bytes + input/output ports + PSW
                For i As Integer = 0 To 15 : QueueControllerOutput(_controllerRam(i)) : Next
                QueueControllerOutput(_inputPort)
                QueueControllerOutput(_outputPort)
                QueueControllerOutput(ComposeStatus())
            Case &HAD
                SetKeyboardInterfaceEnabled(False)
            Case &HAE
                SetKeyboardInterfaceEnabled(True)
            Case &HC0
                QueueControllerOutput(_inputPort)
            Case &HC1
                _pollInputLow = True
            Case &HC2
                _pollInputHigh = True
            Case &HD0
                QueueControllerOutput(_outputPort)
            Case &HD1
                _pendingControllerCommand = &HD1
            Case &HD2
                _pendingControllerCommand = &HD2
            Case &HE0
                Dim testInputs As Byte = 0
                If Not ControllerKeyboardClockDisabled Then testInputs = CByte(testInputs Or 3)
                QueueControllerOutput(testInputs)
            Case &HF0 To &HFF
                PulseOutputPort(value)
            Case Else
                ' Undefined 8042 commands have no architectural side effect.
        End Select
    End Sub

    Private Sub ApplyCommandByte(value As Byte)
        Dim oldInhibited As Boolean = ControllerKeyboardClockDisabled
        Dim oldIrqEnabled As Boolean = (_controllerRam(0) And CommandByteIrq1Enable) <> 0
        Dim oldTranslation As Boolean = TranslationEnabled
        _controllerRam(0) = value
        If oldTranslation <> TranslationEnabled Then _translationBreakPending = False
        Dim newInhibited As Boolean = ControllerKeyboardClockDisabled
        UpdateKeyboardInhibitState()

        If oldInhibited AndAlso Not newInhibited Then BeginLinkFrameIfPossible()
        If Not oldIrqEnabled AndAlso
           (_controllerRam(0) And CommandByteIrq1Enable) <> 0 AndAlso
           _outputBufferFull AndAlso _outputBufferContainsKeyboardData Then
            _pic.RaiseIrq(1)
        End If
    End Sub

    Private ReadOnly Property ControllerKeyboardClockDisabled As Boolean
        Get
            Return (_controllerRam(0) And CommandByteKeyboardDisabled) <> 0
        End Get
    End Property

    Private ReadOnly Property PhysicalKeyboardInhibited As Boolean
        Get
            Return (_inputPort And &H80) = 0
        End Get
    End Property

    Private ReadOnly Property InhibitOverrideEnabled As Boolean
        Get
            Return (_controllerRam(0) And CommandByteInhibitOverride) <> 0
        End Get
    End Property

    Private Sub UpdateKeyboardInhibitState()
        ' The 8042 holds the keyboard clock low while its interface is disabled
        ' or while a received byte is waiting in OBF.  This inhibits the serial
        ' wire only; the keyboard continues scanning and places typematic bytes
        ' in its own bounded FIFO.
        _keyboard.SetHostTransmissionInhibited(ControllerKeyboardClockDisabled OrElse _outputBufferFull)
    End Sub

    Private Sub SetKeyboardInterfaceEnabled(enabled As Boolean)
        Dim commandByte As Byte = _controllerRam(0)
        If enabled Then
            commandByte = CByte(commandByte And Not CommandByteKeyboardDisabled)
        Else
            commandByte = CByte(commandByte Or CommandByteKeyboardDisabled)
        End If
        ApplyCommandByte(commandByte)
    End Sub

    Private Sub ApplyOutputPort(value As Byte)
        Dim oldA20 As Boolean = (_outputPort And 2) <> 0
        _outputPort = value
        Dim newA20 As Boolean = (_outputPort And 2) <> 0
        If newA20 <> oldA20 Then RaiseEvent A20Changed(newA20)
        If (value And 1) = 0 Then RaiseEvent ResetRequested()
    End Sub

    Private Sub PulseOutputPort(command As Byte)
        ' F0h..FFh pulse selected output lines low.  RESET# is line 0.  A pulse
        ' is momentary; it does not rewrite the output-port latch.
        If (command And 1) = 0 Then RaiseEvent ResetRequested()
    End Sub

    Private Sub QueueControllerOutput(value As Byte)
        _controllerOutputQueue.Enqueue(value)
        TryFeedOutputBuffer()
    End Sub

    Private Sub TryFeedOutputBuffer()
        If _outputBufferFull Then Return
        If _controllerOutputQueue.Count > 0 Then
            PresentOutput(_controllerOutputQueue.Dequeue(), keyboardData:=False)
            Return
        End If
        BeginLinkFrameIfPossible()
    End Sub

    Private Sub PresentOutput(value As Byte, keyboardData As Boolean, Optional keyboardCommandResponse As Boolean = False)
        If _outputBufferFull Then
            ' Real hardware inhibits the keyboard when OBF is full; controller
            ' responses queue internally in this model and never overwrite OBF.
            If keyboardData Then
                _receiveTimeout = True
                Return
            End If
            _controllerOutputQueue.Enqueue(value)
            Return
        End If

        _outputBufferValue = value
        _outputBufferContainsKeyboardData = keyboardData
        _outputBufferKeyboardCommandResponse = keyboardData AndAlso keyboardCommandResponse
        _outputBufferFull = True
        TraceDiagnostic("OBF <- " & value.ToString("X2") &
                        "  keyboard=" & If(keyboardData, "1", "0") &
                        "  irqEnabled=" & If((_controllerRam(0) And CommandByteIrq1Enable) <> 0, "1", "0"))
        UpdateKeyboardInhibitState()
        If keyboardData AndAlso (_controllerRam(0) And CommandByteIrq1Enable) <> 0 Then
            _irq1AssertionCount += 1UL
            TraceDiagnostic("IRQ1 assert for " & value.ToString("X2"))
            _pic.RaiseIrq(1)
        End If
    End Sub
End Class

' PC/AT system control port B and POST latch.  The CS8221/82C211 exposes
' parity/I/O-channel status, timer 2, refresh detect, and speaker controls at
' port 61h.  Port 80h retains the most recent POST checkpoint.  Ports F0h/F1h
' provide the documented 80287 busy/reset strobes.
Public Class AtSystemControlPorts
    Implements IPortDevice, IResettableDevice, IMotherboardLocalPortDevice

    Private ReadOnly _pit As Pit8253
    Private _portBWritable As Byte
    Private _postCode As Byte
    Private _parityCheckLatched As Boolean
    Private _ioChannelCheckLatched As Boolean

    Public Event NmiRequested()
    Public Event NmiLineChanged(asserted As Boolean)
    Public Event NumericCoprocessorBusyReset()
    Public Event NumericCoprocessorReset()

    Public Sub New(pit As Pit8253)
        If pit Is Nothing Then Throw New ArgumentNullException(NameOf(pit))
        _pit = pit
        ResetDevice()
    End Sub

    Public Sub ResetDevice() Implements IResettableDevice.ResetDevice
        _portBWritable = 0
        _postCode = 0
        _parityCheckLatched = False
        _ioChannelCheckLatched = False
        _pit.SetGate(2, False)
        RaiseEvent NmiLineChanged(False)
    End Sub

    Public Function HandlesPort(port As UInt16) As Boolean Implements IPortDevice.HandlesPort
        Return port = &H61US OrElse port = &H80US OrElse port = &HF0US OrElse port = &HF1US
    End Function

    Public Function ReadPort(port As UInt16) As Byte Implements IPortDevice.ReadPort
        Select Case port
            Case &H61US
                Dim result As Byte = CByte(_portBWritable And &HF)
                If _pit.RefreshDetect Then result = CByte(result Or &H10)
                If _pit.GetOutput(2) Then result = CByte(result Or &H20)
                If _ioChannelCheckLatched Then result = CByte(result Or &H40)
                If _parityCheckLatched Then result = CByte(result Or &H80)
                Return result
            Case &H80US
                Return _postCode
            Case Else
                Return &HFF
        End Select
    End Function

    Public Sub WritePort(port As UInt16, value As Byte) Implements IPortDevice.WritePort
        Select Case port
            Case &H61US
                ' Bits 2 and 3 are active-low enables on the AT.  Writing either
                ' high disables that source and clears its error latch.
                _portBWritable = CByte(value And &HF)
                If (value And &H4) <> 0 Then _parityCheckLatched = False
                If (value And &H8) <> 0 Then _ioChannelCheckLatched = False
                UpdateNmiLine()
                _pit.SetGate(2, (value And 1) <> 0)
            Case &H80US
                _postCode = value
            Case &HF0US
                RaiseEvent NumericCoprocessorBusyReset()
            Case &HF1US
                RaiseEvent NumericCoprocessorReset()
        End Select
    End Sub

    Public Sub LatchParityError()
        If (_portBWritable And &H4) <> 0 Then Return ' disabled
        Dim wasLatched As Boolean = _parityCheckLatched
        _parityCheckLatched = True
        If Not wasLatched Then RaiseEvent NmiRequested()
        UpdateNmiLine()
    End Sub

    Public Sub LatchIoChannelCheck()
        If (_portBWritable And &H8) <> 0 Then Return ' disabled
        Dim wasLatched As Boolean = _ioChannelCheckLatched
        _ioChannelCheckLatched = True
        If Not wasLatched Then RaiseEvent NmiRequested()
        UpdateNmiLine()
    End Sub

    Private Sub UpdateNmiLine()
        RaiseEvent NmiLineChanged(_parityCheckLatched OrElse _ioChannelCheckLatched)
    End Sub

    ' CROMWELL PC SPEAKER PORT-61 OBSERVABILITY BRICK 1
    Public ReadOnly Property SpeakerDataEnabled As Boolean
        Get
            Return (_portBWritable And 2) <> 0
        End Get
    End Property

    Public ReadOnly Property SpeakerTimerGateEnabled As Boolean
        Get
            Return (_portBWritable And 1) <> 0
        End Get
    End Property
    Public ReadOnly Property SpeakerOutput As Boolean
        Get
            Return (_portBWritable And 2) <> 0 AndAlso _pit.GetOutput(2)
        End Get
    End Property

    Public ReadOnly Property PostCode As Byte
        Get
            Return _postCode
        End Get
    End Property
End Class

' Motorola MC146818A/82C206-compatible real-time clock and 128-byte CMOS RAM.
' It models the one-second update cycle, UIP window, binary/BCD and 12/24-hour
' formats, alarm/update/periodic flags, IRQ8, NMI masking through port 70h,
' battery-backed persistence, and the IBM PC/AT configuration record.
Public Class CmosRtc
    Implements IPortDevice, IClockedDevice, IClockWakeSource, IResettableDevice, IMotherboardLocalPortDevice

    Private Const PicosecondsPerSecond As Long = 1000000000000L
    Private Const UpdateWarningPicoseconds As Long = 244000000L
    Private Const UpdateCyclePicoseconds As Long = 2000000000L
    Private Const PersistenceMagic As String = "VCCMOS3"
    Private Const PreviousPersistenceMagic As String = "VCCMOS2"
    Private Const LegacyPersistenceMagic As String = "VCCMOS1"

    Private ReadOnly _cmos(127) As Byte
    Private ReadOnly _pic As Pic8259
    Private ReadOnly _persistencePath As String
    Private _index As Byte
    Private _nmiDisabled As Boolean
    Private _currentTime As DateTime
    Private _subsecondPicoseconds As Long
    Private _periodicPicoseconds As Long
    Private _registerC As Byte
    Private _updateCycleRemaining As Long
    Private _updatePending As Boolean
    Private _dirty As Boolean

    Public Event NmiMaskChanged(disabled As Boolean)

    Public Sub New(pic As Pic8259, Optional persistencePath As String = Nothing)
        If pic Is Nothing Then Throw New ArgumentNullException(NameOf(pic))
        _pic = pic
        _persistencePath = persistencePath
        SeedDefaults()
        LoadPersistentState()
    End Sub

    Public Sub ResetDevice() Implements IResettableDevice.ResetDevice
        ' PSRSTB resets interrupt-enable/flag state but does not erase battery RAM
        ' or the timekeeper.  Register-B's PIE/AIE/UIE bits clear; 24/12, binary,
        ' daylight-saving and SET state are retained.
        _cmos(&HB) = CByte(_cmos(&HB) And &H87)
        _registerC = 0
        _periodicPicoseconds = 0
        _updateCycleRemaining = 0
        _updatePending = False
        _pic.ClearIrq(0)
    End Sub

    Public Function HandlesPort(port As UInt16) As Boolean Implements IPortDevice.HandlesPort
        Return port = &H70US OrElse port = &H71US
    End Function

    Public Function ReadPort(port As UInt16) As Byte Implements IPortDevice.ReadPort
        If port = &H70US Then Return CByte(_index Or If(_nmiDisabled, &H80, 0))

        ' During the actual ~2 ms update cycle, locations 00h-09h are unavailable
        ' to the CPU on the 82C206.  The AT data bus therefore floats high here.
        If _index <= 9 AndAlso _updateCycleRemaining > 0 Then Return &HFF

        Select Case _index
            Case &H0 : Return EncodeNumber(_currentTime.Second)
            Case &H2 : Return EncodeNumber(_currentTime.Minute)
            Case &H4 : Return EncodeHour(_currentTime.Hour)
            Case &H6 : Return _cmos(&H6)
            Case &H7 : Return EncodeNumber(_currentTime.Day)
            Case &H8 : Return EncodeNumber(_currentTime.Month)
            Case &H9 : Return EncodeNumber(_currentTime.Year Mod 100)
            Case &HA
                Return CByte((_cmos(&HA) And &H7F) Or If(UpdateInProgress, &H80, 0))
            Case &HB
                Return _cmos(&HB)
            Case &HC
                Dim result As Byte = _registerC
                _registerC = 0
                _pic.ClearIrq(0)
                Return result
            Case &HD
                Return &H80
            Case Else
                Return _cmos(_index)
        End Select
    End Function

    Public Sub WritePort(port As UInt16, value As Byte) Implements IPortDevice.WritePort
        If port = &H70US Then
            Dim disabled As Boolean = (value And &H80) <> 0
            _index = CByte(value And &H7F)
            If disabled <> _nmiDisabled Then
                _nmiDisabled = disabled
                RaiseEvent NmiMaskChanged(disabled)
            End If
            Return
        End If

        If _index <= 9 AndAlso _updateCycleRemaining > 0 Then Return

        Select Case _index
            Case &H0
                SetSecond(DecodeNumber(value))
            Case &H1, &H3, &H5
                _cmos(_index) = value
            Case &H2
                SetMinute(DecodeNumber(value))
            Case &H4
                SetHour(DecodeHour(value))
            Case &H6
                _cmos(_index) = value
            Case &H7
                SetCalendar(day:=DecodeNumber(value))
            Case &H8
                SetCalendar(month:=DecodeNumber(value))
            Case &H9
                SetCalendar(yearWithinCentury:=DecodeNumber(value))
            Case &H32
                SetCentury(DecodeNumber(value))
            Case &HA
                _cmos(&HA) = CByte(value And &H7F)
                _periodicPicoseconds = 0
            Case &HB
                Dim oldSet As Boolean = (_cmos(&HB) And &H80) <> 0
                _cmos(&HB) = value
                Dim newSet As Boolean = (value And &H80) <> 0
                If newSet AndAlso Not oldSet Then
                    _updateCycleRemaining = 0
                    _updatePending = False
                End If
                UpdateIrqLine()
            Case &HC, &HD
                Return
            Case Else
                _cmos(_index) = value
        End Select

        _dirty = True
        SavePersistentState()
    End Sub

    Public Function PicosecondsUntilNextWakeEvent() As Long Implements IClockWakeSource.PicosecondsUntilNextWakeEvent
        Dim earliest As Long = Long.MaxValue

        ' Periodic interrupt: only PIE can wake the CPU.  PF itself is still
        ' generated on a later synchronization if software merely polls register C.
        If (_cmos(&HB) And &H40) <> 0 Then
            Dim rateHz As Integer = PeriodicRateHz()
            If rateHz > 0 Then
                Dim period As Long = PicosecondsPerSecond \ rateHz
                If period <= 0 Then period = 1
                Dim untilPeriodic As Long = period - _periodicPicoseconds
                If untilPeriodic <= 0 Then untilPeriodic = 1
                If untilPeriodic < earliest Then earliest = untilPeriodic
            End If
        End If

        ' Update-ended and alarm IRQs are asserted when the update cycle
        ' completes, not at the one-second boundary where UIP begins.
        If (_cmos(&HB) And &H30) <> 0 AndAlso
           (_cmos(&HB) And &H80) = 0 AndAlso
           DividerChainRunningInBed() Then

            Dim untilUpdateComplete As Long
            If _updateCycleRemaining > 0 Then
                untilUpdateComplete = _updateCycleRemaining
            Else
                untilUpdateComplete =
                    Math.Max(1L, PicosecondsPerSecond - _subsecondPicoseconds) +
                    UpdateCyclePicoseconds
            End If

            If untilUpdateComplete < earliest Then earliest = untilUpdateComplete
        End If

        Return earliest
    End Function

    Public Sub AdvanceTime(elapsedPicoseconds As Long) Implements IClockedDevice.AdvanceTime
        If elapsedPicoseconds < 0 Then Throw New ArgumentOutOfRangeException(NameOf(elapsedPicoseconds))
        If elapsedPicoseconds = 0 Then Return

        AdvancePeriodic(elapsedPicoseconds)

        If (_cmos(&HB) And &H80) <> 0 Then Return ' SET inhibits/aborts updates
        If Not DividerChainRunningInBed() Then Return

        Dim remaining As Long = elapsedPicoseconds
        While remaining > 0
            If _updateCycleRemaining > 0 Then
                Dim updateStepTime As Long = Math.Min(remaining, _updateCycleRemaining)
                _updateCycleRemaining -= updateStepTime
                _subsecondPicoseconds += updateStepTime
                remaining -= updateStepTime
                If _updateCycleRemaining = 0 AndAlso _updatePending Then
                    CompleteUpdateCycle()
                End If
                Continue While
            End If

            Dim toBoundary As Long = PicosecondsPerSecond - _subsecondPicoseconds
            Dim stepTime As Long = Math.Min(remaining, toBoundary)
            _subsecondPicoseconds += stepTime
            remaining -= stepTime

            If _subsecondPicoseconds >= PicosecondsPerSecond Then
                _subsecondPicoseconds -= PicosecondsPerSecond
                _updateCycleRemaining = UpdateCyclePicoseconds
                _updatePending = True
            End If
        End While
    End Sub

    Private Sub CompleteUpdateCycle()
        _updatePending = False
        _currentTime = _currentTime.AddSeconds(1)
        _cmos(&H6) = EncodeNumber(CInt(_currentTime.DayOfWeek) + 1)
        SetEventFlag(&H10) ' UF
        If AlarmMatches() Then SetEventFlag(&H20) ' AF
        _dirty = True
    End Sub

    Public ReadOnly Property NmiDisabled As Boolean
        Get
            Return _nmiDisabled
        End Get
    End Property

    Public ReadOnly Property BatteryValid As Boolean
        Get
            Return True
        End Get
    End Property

    ' Host diagnostic observation only.  Unlike a port-71h read this does not
    ' alter the selected CMOS address, NMI mask, RTC flags, or guest-visible bus.
    Public Function PeekCmosByteForDiagnostics(indexInBed As Byte) As Byte
        Return _cmos(indexInBed And &H7F)
    End Function

    Public Sub Save()
        SavePersistentState(force:=True)
    End Sub

    Public Sub ResetConfigurationToAtDefaults()
        Dim savedTime As DateTime = _currentTime
        SeedDefaults()
        _currentTime = savedTime
        _cmos(&H6) = EncodeNumber(CInt(_currentTime.DayOfWeek) + 1)
        _dirty = True
        SavePersistentState(force:=True)
    End Sub

    Private ReadOnly Property UpdateInProgress As Boolean
        Get
            If (_cmos(&HB) And &H80) <> 0 Then Return False
            If Not DividerChainRunningInBed() Then Return False
            Return _updateCycleRemaining > 0 OrElse
                   _subsecondPicoseconds >= PicosecondsPerSecond - UpdateWarningPicoseconds
        End Get
    End Property

    Private Sub AdvancePeriodic(elapsedPicoseconds As Long)
        If Not DividerChainRunningInBed() Then Return
        Dim rateHz As Integer = PeriodicRateHz()
        If rateHz <= 0 Then Return
        Dim period As Long = PicosecondsPerSecond \ rateHz
        If period <= 0 Then period = 1
        _periodicPicoseconds += elapsedPicoseconds
        If _periodicPicoseconds < period Then Return
        _periodicPicoseconds = _periodicPicoseconds Mod period
        SetEventFlag(&H40)
    End Sub

    Private Function PeriodicRateHz() As Integer
        Dim selector As Integer = _cmos(&HA) And &HF
        Select Case selector
            Case 1 : Return 256
            Case 2 : Return 128
            Case 3 : Return 8192
            Case 4 : Return 4096
            Case 5 : Return 2048
            Case 6 : Return 1024
            Case 7 : Return 512
            Case 8 : Return 256
            Case 9 : Return 128
            Case 10 : Return 64
            Case 11 : Return 32
            Case 12 : Return 16
            Case 13 : Return 8
            Case 14 : Return 4
            Case 15 : Return 2
            Case Else : Return 0
        End Select
    End Function

    Private Function DividerChainRunningInBed() As Boolean
        ' This board supplies the 82C206 with the normal 32.768-kHz time-base.
        ' DV=010 selects that divider chain; reset/test selections do not
        ' continue producing update or periodic events.
        Return (_cmos(&HA) And &H70) = &H20
    End Function

    Private Sub SetEventFlag(flag As Byte)
        _registerC = CByte(_registerC Or flag)
        UpdateIrqLine()
    End Sub

    Private Sub UpdateIrqLine()
        Dim enabledEvent As Boolean =
            ((_registerC And &H40) <> 0 AndAlso (_cmos(&HB) And &H40) <> 0) OrElse
            ((_registerC And &H20) <> 0 AndAlso (_cmos(&HB) And &H20) <> 0) OrElse
            ((_registerC And &H10) <> 0 AndAlso (_cmos(&HB) And &H10) <> 0)

        If enabledEvent Then
            _registerC = CByte(_registerC Or &H80)
            _pic.RaiseIrq(0)
        Else
            _registerC = CByte(_registerC And &H7F)
            _pic.ClearIrq(0)
        End If
    End Sub

    Private Function AlarmMatches() As Boolean
        Return AlarmFieldMatches(_cmos(&H1), _currentTime.Second, False) AndAlso
               AlarmFieldMatches(_cmos(&H3), _currentTime.Minute, False) AndAlso
               AlarmFieldMatches(_cmos(&H5), _currentTime.Hour, True)
    End Function

    Private Function AlarmFieldMatches(raw As Byte, value As Integer, hourField As Boolean) As Boolean
        If (raw And &HC0) = &HC0 Then Return True
        Dim decoded As Integer = If(hourField, DecodeHour(raw), DecodeNumber(raw))
        Return decoded = value
    End Function

    Private Function EncodeNumber(value As Integer) As Byte
        If (_cmos(&HB) And 4) <> 0 Then Return CByte(value)
        Return CByte(((value \ 10) << 4) Or (value Mod 10))
    End Function

    Private Function DecodeNumber(value As Byte) As Integer
        If (_cmos(&HB) And 4) <> 0 Then Return value
        Return ((value >> 4) And &HF) * 10 + (value And &HF)
    End Function

    Private Function EncodeHour(hour As Integer) As Byte
        If (_cmos(&HB) And 2) <> 0 Then Return EncodeNumber(hour)
        Dim pm As Boolean = hour >= 12
        Dim twelveHour As Integer = hour Mod 12
        If twelveHour = 0 Then twelveHour = 12
        Dim result As Byte = EncodeNumber(twelveHour)
        If pm Then result = CByte(result Or &H80)
        Return result
    End Function

    Private Function DecodeHour(value As Byte) As Integer
        If (_cmos(&HB) And 2) <> 0 Then Return DecodeNumber(CByte(value And &H7F))
        Dim pm As Boolean = (value And &H80) <> 0
        Dim hour As Integer = DecodeNumber(CByte(value And &H7F)) Mod 12
        If pm Then hour += 12
        Return hour
    End Function

    Private Sub SetSecond(value As Integer)
        If value < 0 OrElse value > 59 Then Return
        _currentTime = New DateTime(_currentTime.Year, _currentTime.Month, _currentTime.Day,
                                    _currentTime.Hour, _currentTime.Minute, value,
                                    DateTimeKind.Unspecified)
        _subsecondPicoseconds = 0
    End Sub

    Private Sub SetMinute(value As Integer)
        If value < 0 OrElse value > 59 Then Return
        _currentTime = New DateTime(_currentTime.Year, _currentTime.Month, _currentTime.Day,
                                    _currentTime.Hour, value, _currentTime.Second,
                                    DateTimeKind.Unspecified)
    End Sub

    Private Sub SetHour(value As Integer)
        If value < 0 OrElse value > 23 Then Return
        _currentTime = New DateTime(_currentTime.Year, _currentTime.Month, _currentTime.Day,
                                    value, _currentTime.Minute, _currentTime.Second,
                                    DateTimeKind.Unspecified)
    End Sub

    Private Sub SetCalendar(Optional day As Integer = -1,
                            Optional month As Integer = -1,
                            Optional yearWithinCentury As Integer = -1)
        Dim newMonth As Integer = If(month >= 1 AndAlso month <= 12, month, _currentTime.Month)
        Dim century As Integer = DecodeCentury()
        Dim newYear As Integer = If(yearWithinCentury >= 0 AndAlso yearWithinCentury <= 99,
                                    century * 100 + yearWithinCentury,
                                    _currentTime.Year)
        If newYear < 1 OrElse newYear > 9999 Then newYear = _currentTime.Year
        Dim maximumDay As Integer = DateTime.DaysInMonth(newYear, newMonth)
        Dim newDay As Integer = If(day >= 1 AndAlso day <= maximumDay, day, Math.Min(_currentTime.Day, maximumDay))
        _currentTime = New DateTime(newYear, newMonth, newDay,
                                    _currentTime.Hour, _currentTime.Minute, _currentTime.Second,
                                    DateTimeKind.Unspecified)
        _cmos(&H32) = EncodeNumber(newYear \ 100)
    End Sub

    Private Function DecodeCentury() As Integer
        Dim century As Integer = DecodeNumber(_cmos(&H32))
        If century < 1 OrElse century > 99 Then century = _currentTime.Year \ 100
        Return century
    End Function

    Private Sub SetCentury(value As Integer)
        If value < 1 OrElse value > 99 Then Return
        Dim yearWithinCentury As Integer = _currentTime.Year Mod 100
        Dim newYear As Integer = value * 100 + yearWithinCentury
        Dim maximumDay As Integer = DateTime.DaysInMonth(newYear, _currentTime.Month)
        _currentTime = New DateTime(newYear, _currentTime.Month, Math.Min(_currentTime.Day, maximumDay),
                                    _currentTime.Hour, _currentTime.Minute, _currentTime.Second,
                                    DateTimeKind.Unspecified)
        _cmos(&H32) = EncodeNumber(value)
    End Sub

    Private Sub SeedDefaults()
        Array.Clear(_cmos, 0, _cmos.Length)
        _currentTime = DateTime.SpecifyKind(DateTime.Now, DateTimeKind.Unspecified)
        _cmos(&HA) = &H26             ' 32.768 kHz divider, 1024 Hz periodic rate
        _cmos(&HB) = &H2              ' BCD, 24-hour, interrupts disabled
        _cmos(&H6) = EncodeNumber(CInt(_currentTime.DayOfWeek) + 1)
        _cmos(&HD) = &H80             ' valid battery-backed RAM
        _cmos(&HE) = 0                ' diagnostic status
        _cmos(&HF) = 0                ' shutdown status
        _cmos(&H10) = &H44            ' two 1.44 MB 3.5-inch drives (AMI extension)
        _cmos(&H12) = 0               ' BIOS fills detected fixed-disk type
        _cmos(&H14) = &H63            ' two floppies, 80-column color, 80287 present
        _cmos(&H15) = &H80            ' 640 KiB base memory
        _cmos(&H16) = &H2
        _cmos(&H17) = 0               ' 15360 KiB extended memory
        _cmos(&H18) = &H3C
        _cmos(&H20) = &H1              ' OEM setup flags: boot Num Lock on, floppy first
        _cmos(&H30) = 0
        _cmos(&H31) = &H3C
        _cmos(&H32) = EncodeNumber(_currentTime.Year \ 100)
        UpdateConfigurationChecksum()
        _registerC = 0
        _subsecondPicoseconds = 0
        _periodicPicoseconds = 0
        _updateCycleRemaining = 0
        _updatePending = False
        _dirty = True
    End Sub

    Public Sub UpdateConfigurationChecksum()
        Dim checksum As Integer
        For address As Integer = &H10 To &H20
            checksum = (checksum + _cmos(address)) And &HFFFF
        Next
        _cmos(&H2E) = CByte(checksum >> 8)
        _cmos(&H2F) = CByte(checksum And &HFF)
        _dirty = True
    End Sub

    Private Sub LoadPersistentState()
        If String.IsNullOrWhiteSpace(_persistencePath) OrElse Not File.Exists(_persistencePath) Then Return
        Try
            Using stream As New FileStream(_persistencePath, FileMode.Open, FileAccess.Read, FileShare.Read)
                Using reader As New BinaryReader(stream, Encoding.ASCII, leaveOpen:=False)
                    Dim magicChars As Char() = reader.ReadChars(PersistenceMagic.Length)
                    Dim magic As String = New String(magicChars)
                    Dim bytesToRead As Integer
                    Dim migrateSetupFlags As Boolean
                    If magic = PersistenceMagic Then
                        bytesToRead = _cmos.Length
                    ElseIf magic = PreviousPersistenceMagic Then
                        bytesToRead = _cmos.Length
                        migrateSetupFlags = True
                    ElseIf magic = LegacyPersistenceMagic Then
                        bytesToRead = 64
                        migrateSetupFlags = True
                    Else
                        Return
                    End If

                    Dim bytes As Byte() = reader.ReadBytes(bytesToRead)
                    If bytes.Length <> bytesToRead Then Return
                    Array.Copy(bytes, _cmos, bytes.Length)

                    Dim savedClockTicks As Long = reader.ReadInt64()
                    Dim savedHostUtcTicks As Long = reader.ReadInt64()
                    _currentTime = New DateTime(savedClockTicks, DateTimeKind.Unspecified)
                    If (_cmos(&HB) And &H80) = 0 Then
                        Dim elapsed As TimeSpan = DateTime.UtcNow - New DateTime(savedHostUtcTicks, DateTimeKind.Utc)
                        If elapsed > TimeSpan.Zero AndAlso elapsed < TimeSpan.FromDays(3650) Then
                            _currentTime = _currentTime.Add(elapsed)
                        End If
                    End If
                    _cmos(&HD) = &H80
                    If migrateSetupFlags Then
                        ' VCCMOS1/2 predate the OEM setup-flags byte. Give an
                        ' existing machine the new factory Boot Num Lock default
                        ' exactly once; VCCMOS3 subsequently preserves user choice.
                        _cmos(&H20) = CByte(_cmos(&H20) Or &H1)
                        UpdateConfigurationChecksum()
                        _dirty = True
                    Else
                        _dirty = False
                    End If
                End Using
            End Using
            If _dirty Then SavePersistentState(force:=True)
        Catch
            SeedDefaults()
        End Try
    End Sub

    Private Sub SavePersistentState(Optional force As Boolean = False)
        If String.IsNullOrWhiteSpace(_persistencePath) OrElse (Not force AndAlso Not _dirty) Then Return
        Try
            Dim directoryPath As String = Path.GetDirectoryName(_persistencePath)
            If Not String.IsNullOrEmpty(directoryPath) Then System.IO.Directory.CreateDirectory(directoryPath)
            Dim temporary As String = _persistencePath & ".tmp"
            Using stream As New FileStream(temporary, FileMode.Create, FileAccess.Write, FileShare.None)
                Using writer As New BinaryWriter(stream, Encoding.ASCII, leaveOpen:=False)
                    writer.Write(PersistenceMagic.ToCharArray())
                    writer.Write(_cmos)
                    writer.Write(_currentTime.Ticks)
                    writer.Write(DateTime.UtcNow.Ticks)
                End Using
            End Using
            File.Move(temporary, _persistencePath, True)
            _dirty = False
        Catch
            ' The RTC remains operational even if the host cannot persist the
            ' battery image; the next successful save will retry atomically.
        End Try
    End Sub
End Class

' ---------------------------------------------------------------------------
' C&T CS8221 NEAT motherboard core: 82C211 CPU/bus controller +
' 82C212 memory/shadow controller + the 82C206 indexed configuration byte.
' The 82C215 is electrically a data/address buffer; its guest-visible behavior
' is represented by these decode and routing decisions rather than fake ports.
' ---------------------------------------------------------------------------
Public Class NeatCs8221Chipset
    Implements IPortDevice, IConditionalMemoryMappedDevice, IClockedDevice, IClockBatchSafeDevice, IPageCoherentMemoryDecode, IMemoryClockIndependentDevice, IResettableDevice, IMotherboardLocalPortDevice, IMemoryCycleTimingTargetProvider, IMemoryDecodeChangeSource

    Public Event MemoryDecodeChanged() Implements IMemoryDecodeChangeSource.MemoryDecodeChanged

    ' BRICK 8C: physical memory no longer loops back through Processor286.
    Private ReadOnly _cpuClockHz As Func(Of Long)
    Private ReadOnly _shadowRam(&H5FFFF) As Byte ' A0000h-FFFFFh, 384 KiB

    Private _index As Byte
    Private _indexArmed As Boolean
    Private _ipcConfiguration As Byte

    Private _ra0 As Byte
    Private _ra1 As Byte
    Private _ra2 As Byte

    Private ReadOnly _rb(11) As Byte ' 64h-6Fh
    Private _readyTimeoutLatched As Boolean
    Private _cpuResetPicosecondsRemaining As Long
    Private _refreshAddress As UInt16
    Private _refreshCycles As ULong

    Public Event CpuResetRequested()
    Public Event A20ForceLowChanged(forceLow As Boolean)
    Public Event ReadyTimeoutNmiRequested()

    Public Sub New(Optional cpuClockHz As Func(Of Long) = Nothing)
        _cpuClockHz = cpuClockHz
        ResetDevice()
    End Sub

    Public Sub ResetDevice() Implements IResettableDevice.ResetDevice
        _index = 0
        _indexArmed = False

        ' 82C206 Configuration register reset value from the C&T data sheet.
        _ipcConfiguration = &HC0

        ' 82C211 reset/default values.
        _ra0 = 0
        _ra1 = &H45 ' IO delay=1, 8-bit memory delay=1, quick mode disabled
        _ra2 = &H18 ' normal mode: 8-bit=4 waits, 16-bit=1 wait, BCLK=CLK2IN/2

        Array.Clear(_rb, 0, _rb.Length)
        _rb(0) = 0       ' 64h: initial 82C212 revision
        _rb(1) = &HE     ' 65h: F ROM on; C/D/E local ROM off; shadow writable
        _rb(2) = 0       ' 66h
        _rb(3) = 0       ' 67h
        _rb(4) = 0       ' 68h
        _rb(5) = 0       ' 69h
        _rb(6) = &H80    ' 6Ah: one bank, 256K-bit DRAM type default
        _rb(7) = &H6B    ' 6Bh: 3 ROM waits, 2 EMS waits, 1 RAM wait, relocation
        _rb(8) = 0       ' 6Ch
        _rb(9) = 0       ' 6Dh EMS base
        _rb(10) = 0      ' 6Eh EMS extension
        _rb(11) = 0      ' 6Fh RB11 misc / GA20 passes CPU A20

        _readyTimeoutLatched = False
        _cpuResetPicosecondsRemaining = 0
        _refreshAddress = 0
        _refreshCycles = 0
        RaiseEvent A20ForceLowChanged(False)
    End Sub

    Public Function HandlesPort(port As UInt16) As Boolean Implements IPortDevice.HandlesPort
        Return port = &H22US OrElse port = &H23US
    End Function

    Public Function ReadPort(port As UInt16) As Byte Implements IPortDevice.ReadPort
        If port = &H22US Then Return _index
        If Not _indexArmed Then Return &HFF
        _indexArmed = False

        Select Case _index
            Case &H1
                Return _ipcConfiguration
            Case &H60
                ' 82C211 RA0: b5 CPU RESET, b4 PROCCLK select, b2 READY-timeout
                ' NMI enable are writable; b0 is the READY-timeout status latch.
                ' Revision bits 7:6 read zero for this profile.
                Dim result As Byte = CByte(_ra0 And &H34)
                If _readyTimeoutLatched Then result = CByte(result Or &H1)
                Return result
            Case &H61
                Return _ra1
            Case &H62
                Return _ra2
            Case &H64 To &H6F
                Return _rb(_index - &H64)
            Case Else
                Return &HFF
        End Select
    End Function

    Public Sub WritePort(port As UInt16, value As Byte) Implements IPortDevice.WritePort
        If port = &H22US Then
            _index = value
            _indexArmed = True
            Return
        End If

        If Not _indexArmed Then Return
        _indexArmed = False

        Select Case _index
            Case &H1
                _ipcConfiguration = value
            Case &H60
                WriteRa0(value)
            Case &H61
                _ra1 = value
            Case &H62
                _ra2 = CByte(value And &H3F) ' bits 7:6 reserved
            Case &H64
                ' version register is read-only
            Case &H65 To &H6F
                WriteMemoryControllerRegister(_index, value)
        End Select
    End Sub

    Private Sub WriteRa0(value As Byte)
        Dim oldReset As Boolean = (_ra0 And &H20) <> 0
        ' Writable bits are CPU RESET (5), PROCCLK SELECT (4), and READY
        ' timeout NMI enable (2).  READY timeout status (0) is read-only.
        _ra0 = CByte(value And &H34)
        Dim newReset As Boolean = (_ra0 And &H20) <> 0
        If newReset AndAlso Not oldReset Then
            Dim hz As Long = If(_cpuClockHz Is Nothing,
                                MachineProfile286.TurboCpuClockHz,
                                Math.Max(1L, _cpuClockHz.Invoke()))
            _cpuResetPicosecondsRemaining =
                Math.Max(1L, (16L * MachineProfile286.PicosecondsPerSecond + hz - 1L) \ hz)
            RaiseEvent CpuResetRequested()
        End If
    End Sub

    Private Sub WriteMemoryControllerRegister(index As Byte, value As Byte)
        Dim slot As Integer = index - &H64
        Dim oldValueInBed As Byte = _rb(slot)
        Select Case index
            Case &H65
                _rb(slot) = value
            Case &H66
                ' 82C212 RB2: bits 0-6 reserved; bit 7 selects whether
                ' 80000h-9FFFFh is supplied by local motherboard DRAM (1) or
                ' the AT I/O channel (0).
                _rb(slot) = CByte(value And &H80)
            Case &H67, &H68, &H69
                ' 82C212 RB3/RB4/RB5: eight independent 16 KiB shadow-RAM
                ' enables for A-B, C-D, and E-F respectively.
                _rb(slot) = value
            Case &H6A
                _rb(slot) = CByte(value And &HE0)
            Case &H6B
                _rb(slot) = value
            Case &H6C
                ' 82C212 RB8: bits 0-4 reserved; bit 5 bank-pair enable;
                ' bits 7:6 DRAM type.  Reserved bit 4 must remain zero.
                _rb(slot) = CByte(value And &HE0)
            Case &H6D, &H6E
                _rb(slot) = value
            Case &H6F
                Dim oldForce As Boolean = (_rb(slot) And &H2) <> 0
                _rb(slot) = CByte(value And &HE6)
                Dim newForce As Boolean = (_rb(slot) And &H2) <> 0
                If newForce <> oldForce Then RaiseEvent A20ForceLowChanged(newForce)
        End Select

        ' RB2-RB5 contain motherboard-vs-ISA and shadow-decode controls.  Only
        ' those registers can change the compiled physical page candidate set.
        If index >= &H66 AndAlso index <= &H69 AndAlso _rb(slot) <> oldValueInBed Then
            RaiseEvent MemoryDecodeChanged()
        End If
    End Sub

    Public Sub AdvanceTime(elapsedPicoseconds As Long) Implements IClockedDevice.AdvanceTime
        If elapsedPicoseconds < 0 Then Throw New ArgumentOutOfRangeException(NameOf(elapsedPicoseconds))
        If _cpuResetPicosecondsRemaining <= 0 Then Return
        _cpuResetPicosecondsRemaining -= elapsedPicoseconds
        If _cpuResetPicosecondsRemaining <= 0 Then
            _cpuResetPicosecondsRemaining = 0
            _ra0 = CByte(_ra0 And &HDF)
        End If
    End Sub


    ' REFREQ from 82C206 timer channel 1 is internally latched by the 82C211.
    ' A NEAT refresh cycle supplies a 10-bit refresh address to the 82C212 and
    ' cycles its DRAM RAS lines; it is not a memory copy and does not consume a
    ' programmable 8237 channel.  Host RAM does not decay, so the observable
    ' substrate state is the refresh address/cycle progression.
    Public Sub RequestRefresh()
        _refreshAddress = CUShort((_refreshAddress + 1US) And &H3FFUS)
        _refreshCycles += 1UL
    End Sub

    Public ReadOnly Property RefreshAddress As UInt16
        Get
            Return _refreshAddress
        End Get
    End Property

    Public ReadOnly Property RefreshCycleCount As ULong
        Get
            Return _refreshCycles
        End Get
    End Property

    ' CROMWELL PCB REFIT PHASE 2 BRICK 8F - clock-qualified NEAT timing policy.
    ' The 82C211 AT state machine measures command/data delays in BCLK periods.
    ' The integrated 82C206 measures local-access and DMA waits in SCLK/SYSCLK
    ' periods.  The 82C212 local RAM/ROM settings already express CPU READY Tw.
    ' Convert those physical clock domains into the CPU's bounded T-state ledger;
    ' never delay the host thread.
    Public Function GetReadyWaitTStates(cycleInBed As AtReadyCycle286) As Integer
        Dim waitTStatesInBed As Integer

        Select Case cycleInBed.ReadyClass
            Case AtReadyCycleClass286.LocalDram
                waitTStatesInBed = RamWaitStates

            Case AtReadyCycleClass286.SystemRom
                waitTStatesInBed = RomWaitStates

            Case AtReadyCycleClass286.MotherboardIo
                ' The IPC wait-state register applies when the CPU accesses the
                ' integrated 82C206 blocks.  The discrete 8042 and 82C211-local
                ' ports have their own device timing and do not inherit IPC waits.
                If Is82C206IntegratedPortInBed(CUShort(cycleInBed.AddressOrPort And &HFFFFUI)) Then
                    waitTStatesInBed =
                        ConvertClockTicksToCpuTStatesInBed(CpuAccessWaitStates,
                                                           SystemBusClockHzInBed())
                Else
                    waitTStatesInBed = 0
                End If

            Case AtReadyCycleClass286.AtBus8, AtReadyCycleClass286.AtBus16
                waitTStatesInBed = GetAtBusReadyWaitTStatesInBed(cycleInBed)

            Case Else
                Throw New ArgumentOutOfRangeException(NameOf(cycleInBed))
        End Select

        ' 82C206 DMA wait-state control is part of the DMA service cadence and
        ' therefore adds time while the external master owns the motherboard bus.
        Select Case cycleInBed.Master
            Case AtBusMaster286.Dma8
                waitTStatesInBed +=
                    ConvertClockTicksToCpuTStatesInBed(Dma8WaitStates, DmaClockHzInBed())
            Case AtBusMaster286.Dma16
                waitTStatesInBed +=
                    ConvertClockTicksToCpuTStatesInBed(Dma16WaitStates, DmaClockHzInBed())
        End Select

        Return waitTStatesInBed
    End Function

    Private Function GetAtBusReadyWaitTStatesInBed(cycleInBed As AtReadyCycle286) As Integer
        Dim commandDelayInBed As Integer
        Select Case cycleInBed.Kind
            Case AtBusCycleKind286.IoRead8, AtBusCycleKind286.IoRead16,
                 AtBusCycleKind286.IoWrite8, AtBusCycleKind286.IoWrite16
                commandDelayInBed = AtIoCommandDelay
            Case Else
                commandDelayInBed = If(cycleInBed.WidthBytes >= 2,
                                       At16BitCommandDelay,
                                       At8BitCommandDelay)
        End Select

        Dim dataWaitInBed As Integer =
            If(cycleInBed.ReadyClass = AtReadyCycleClass286.AtBus16,
               At16BitWaitStates,
               At8BitWaitStates)

        ' RA1 bit 7 extends the AT memory address-hold phase; it is not an I/O
        ' command delay and must not be charged to port cycles.
        Dim addressHoldInBed As Integer
        Select Case cycleInBed.Kind
            Case AtBusCycleKind286.IoRead8, AtBusCycleKind286.IoRead16,
                 AtBusCycleKind286.IoWrite8, AtBusCycleKind286.IoWrite16
                addressHoldInBed = 0
            Case Else
                addressHoldInBed = If(ExtraAddressHoldEnabled, 1, 0)
        End Select

        Return ConvertClockTicksToCpuTStatesInBed(commandDelayInBed +
                                                   dataWaitInBed +
                                                   addressHoldInBed,
                                                   BclkHzInBed())
    End Function

    Private Shared Function Is82C206IntegratedPortInBed(portInBed As UInt16) As Boolean
        ' Dual 8237 DMA, dual 8259 PIC, 8254 timer, RTC/CMOS and DMA page logic.
        ' Port 61h and F0h/F1h are 82C211 board logic; 60h/64h are the 8042.
        If portInBed <= &H1FUS Then Return True
        If portInBed >= &H20US AndAlso portInBed <= &H21US Then Return True
        If portInBed >= &H40US AndAlso portInBed <= &H43US Then Return True
        If portInBed >= &H70US AndAlso portInBed <= &H71US Then Return True
        If portInBed >= &H80US AndAlso portInBed <= &H8FUS Then Return True
        If portInBed >= &HA0US AndAlso portInBed <= &HA1US Then Return True
        If portInBed >= &HC0US AndAlso portInBed <= &HDFUS Then Return True
        Return False
    End Function

    Private Function CpuStateClockHzInBed() As Long
        If _cpuClockHz Is Nothing Then Return MachineProfile286.TurboCpuClockHz
        Return Math.Max(1L, _cpuClockHz.Invoke())
    End Function

    Private Function BclkHzInBed() As Long
        ' CpuClockHz is the 80286 state-clock rate.  CLK2IN/PROCCLK are twice
        ' that rate on the 286.  Therefore normal BCLK=CLK2IN/2 equals the CPU
        ' state-clock rate; quick/delayed BCLK=CLK2IN is twice it.
        Dim cpuStateHzInBed As Long = CpuStateClockHzInBed()
        Select Case BclkSourceSelect
            Case 0
                Return cpuStateHzInBed
            Case 1
                If cpuStateHzInBed > Long.MaxValue \ 2L Then Return Long.MaxValue
                Return cpuStateHzInBed * 2L
            Case 2
                ' MachineProfile.IsaClockHz is the guest-visible ISA SYSCLK.
                ' External mode has BCLK=ATCLK and SYSCLK=BCLK/2.
                If MachineProfile286.IsaClockHz > Long.MaxValue \ 2L Then Return Long.MaxValue
                Return MachineProfile286.IsaClockHz * 2L
            Case Else
                ' 11b is reserved.  Keep deterministic electrical timing at the
                ' configured external AT clock while diagnostics expose RESERVED.
                If MachineProfile286.IsaClockHz > Long.MaxValue \ 2L Then Return Long.MaxValue
                Return MachineProfile286.IsaClockHz * 2L
        End Select
    End Function

    Private Function SystemBusClockHzInBed() As Long
        Return Math.Max(1L, BclkHzInBed() \ 2L)
    End Function

    Private Function DmaClockHzInBed() As Long
        Dim sclkInBed As Long = SystemBusClockHzInBed()
        If DmaClockUsesFullSclk Then Return sclkInBed
        Return Math.Max(1L, sclkInBed \ 2L)
    End Function

    Private Function ConvertClockTicksToCpuTStatesInBed(clockTicksInBed As Integer,
                                                        sourceClockHzInBed As Long) As Integer
        If clockTicksInBed <= 0 Then Return 0
        If sourceClockHzInBed <= 0 Then
            Throw New InvalidOperationException("NEAT timing source clock is not running.")
        End If

        Dim cpuStateHzInBed As Long = CpuStateClockHzInBed()
        Dim numeratorInBed As Long = CLng(clockTicksInBed) * cpuStateHzInBed
        Dim resultInBed As Long =
            (numeratorInBed + sourceClockHzInBed - 1L) \ sourceClockHzInBed
        If resultInBed > Integer.MaxValue Then
            Throw New InvalidOperationException("NEAT READY duration exceeds the CPU timing ledger.")
        End If
        Return CInt(resultInBed)
    End Function

    Private Function TimingModeNameInBed() As String
        Select Case BclkSourceSelect
            Case 0
                Return "Normal"
            Case 1
                Return If(QuickModeEnabled, "Quick", "Delayed")
            Case 2
                Return "External"
            Case Else
                Return "Reserved(11)"
        End Select
    End Function

    Public Function TimingDiagnosticText() As String
        Return "  82C211 timing             : " & TimingModeNameInBed() &
               " BCLK=" & (CDbl(BclkHzInBed()) / 1000000.0).ToString("0.###") & "MHz" &
               " SYSCLK=" & (CDbl(SystemBusClockHzInBed()) / 1000000.0).ToString("0.###") & "MHz" &
               " RA1=" & _ra1.ToString("X2") & " RA2=" & _ra2.ToString("X2") & Environment.NewLine &
               "  AT cmd I/O/8/16 wait8/16 : " &
               AtIoCommandDelay.ToString() & "/" &
               At8BitCommandDelay.ToString() & "/" &
               At16BitCommandDelay.ToString() & "  " &
               At8BitWaitStates.ToString() & "/" &
               At16BitWaitStates.ToString() & Environment.NewLine &
               "  82C206 CPU/DMA8/DMA16     : " &
               CpuAccessWaitStates.ToString() & "/" &
               Dma8WaitStates.ToString() & "/" &
               Dma16WaitStates.ToString() &
               " waits  DMAclk=" &
               (CDbl(DmaClockHzInBed()) / 1000000.0).ToString("0.###") & "MHz" & Environment.NewLine &
               "  82C212 RAM/ROM READY Tw    : " &
               RamWaitStates.ToString() & "/" & RomWaitStates.ToString()
    End Function

    Public Function GetMemoryCycleTimingTarget(address As UInteger,
                                               isWrite As Boolean) As AtMemoryCycleTarget286 Implements IMemoryCycleTimingTargetProvider.GetMemoryCycleTimingTarget
        ' This device only wins a conditional memory cycle when its on-board
        ' shadow RAM selected the address/direction, so READY comes from local DRAM.
        Return AtMemoryCycleTarget286.LocalDram
    End Function

    Public Sub LatchReadyTimeout()
        _readyTimeoutLatched = True
        If (_ra0 And &H4) <> 0 Then RaiseEvent ReadyTimeoutNmiRequested()
    End Sub

    Public Sub ClearReadyTimeout()
        _readyTimeoutLatched = False
    End Sub

    Public ReadOnly Property IpcConfigurationRegister As Byte
        Get
            Return _ipcConfiguration
        End Get
    End Property

    Public ReadOnly Property CpuAccessWaitStates As Integer
        Get
            ' 82C206 index 01h encodes one through four access wait states.
            Return 1 + ((_ipcConfiguration >> 6) And 3)
        End Get
    End Property

    Public ReadOnly Property Dma8WaitStates As Integer
        Get
            Return 1 + ((_ipcConfiguration >> 2) And 3)
        End Get
    End Property

    Public ReadOnly Property Dma16WaitStates As Integer
        Get
            Return 1 + ((_ipcConfiguration >> 4) And 3)
        End Get
    End Property

    Public ReadOnly Property DmaClockUsesFullSclk As Boolean
        Get
            Return (_ipcConfiguration And 1) <> 0
        End Get
    End Property

    Public ReadOnly Property ProcessorClockUsesBclk As Boolean
        Get
            Return (_ra0 And &H10) <> 0
        End Get
    End Property

    Public ReadOnly Property ProcessorResetActive As Boolean
        Get
            Return (_ra0 And &H20) <> 0
        End Get
    End Property

    Public ReadOnly Property ReadyTimeoutNmiEnabled As Boolean
        Get
            Return (_ra0 And &H4) <> 0
        End Get
    End Property

    Public ReadOnly Property ReadyTimeoutLatched As Boolean
        Get
            Return _readyTimeoutLatched
        End Get
    End Property

    Public ReadOnly Property AtIoCommandDelay As Integer
        Get
            Return _ra1 And 3
        End Get
    End Property

    Public ReadOnly Property At8BitCommandDelay As Integer
        Get
            Return (_ra1 >> 2) And 3
        End Get
    End Property

    Public ReadOnly Property At16BitCommandDelay As Integer
        Get
            Return (_ra1 >> 4) And 3
        End Get
    End Property

    Public ReadOnly Property QuickModeEnabled As Boolean
        Get
            ' RA1 bit 6 is active-low: zero selects Quick mode.
            Return (_ra1 And &H40) = 0
        End Get
    End Property

    Public ReadOnly Property ExtraAddressHoldEnabled As Boolean
        Get
            Return (_ra1 And &H80) <> 0
        End Get
    End Property

    Public ReadOnly Property BclkSourceSelect As Integer
        Get
            Return _ra2 And 3
        End Get
    End Property

    Public ReadOnly Property At8BitWaitStates As Integer
        Get
            Return 2 + ((_ra2 >> 2) And 3)
        End Get
    End Property

    Public ReadOnly Property At16BitWaitStates As Integer
        Get
            Return (_ra2 >> 4) And 3
        End Get
    End Property

    Public ReadOnly Property RomWaitStates As Integer
        Get
            Return _rb(7) And 3
        End Get
    End Property

    Public ReadOnly Property EmsWaitStates As Integer
        Get
            ' RB7 bits 3:2: 00=0, 01=1, 10=2, 11=reserved.
            Dim encoded As Integer = (_rb(7) >> 2) And 3
            Return If(encoded = 3, -1, encoded)
        End Get
    End Property

    Public ReadOnly Property MemoryRelocationEnabled As Boolean
        Get
            Return (_rb(7) And &H40) <> 0
        End Get
    End Property

    Public ReadOnly Property Bank01PairEnabled As Boolean
        Get
            Return (_rb(6) And &H20) <> 0
        End Get
    End Property

    Public ReadOnly Property Bank01DramTypeCode As Integer
        Get
            Return (_rb(6) >> 6) And 3
        End Get
    End Property

    Public ReadOnly Property Bank23PairEnabled As Boolean
        Get
            Return (_rb(8) And &H20) <> 0
        End Get
    End Property

    Public ReadOnly Property Bank23DramTypeCode As Integer
        Get
            Return (_rb(8) >> 6) And 3
        End Get
    End Property

    Public ReadOnly Property RamWaitStates As Integer
        Get
            Return If((_rb(7) And &H20) <> 0, 1, 0)
        End Get
    End Property

    Public ReadOnly Property EmsEnabled As Boolean
        Get
            Return (_rb(7) And &H10) <> 0
        End Get
    End Property

    Public ReadOnly Property PageInterleavedMemoryEnabled As Boolean
        Get
            Return (_rb(7) And &H80) <> 0
        End Get
    End Property

    Public ReadOnly Property RasTimeoutEnabled As Boolean
        Get
            Return (_rb(11) And &H4) <> 0
        End Get
    End Property

    Public ReadOnly Property EmsPageRegisterIoBase As UInt16
        Get
            Select Case _rb(9) And &HF
                Case &H0 : Return &H208US
                Case &H1 : Return &H218US
                Case &H5 : Return &H258US
                Case &H6 : Return &H268US
                Case &HA : Return &H2A8US
                Case &HB : Return &H2B8US
                Case &HE : Return &H2E8US
                Case Else : Return 0US
            End Select
        End Get
    End Property

    Public ReadOnly Property EmsPageFrameBase As UInteger
        Get
            Dim selector As Integer = (_rb(9) >> 4) And &HF
            If selector < 0 OrElse selector > 8 Then Return 0UI
            Return &HC0000UI + CUInt(selector) * &H4000UI
        End Get
    End Property

    Public ReadOnly Property EmsConfiguredBytes As UInteger
        Get
            ' RB11[7:5] encodes 0.5 MiB through 7 MiB, always beginning at 1 MiB.
            Dim code As Integer = (_rb(11) >> 5) And 7
            If code = 0 Then Return &H80000UI
            Return CUInt(code) * &H100000UI
        End Get
    End Property

    Public ReadOnly Property UpperConventionalRamOnMotherboard As Boolean
        Get
            ' 82C212 RB2/index 66h bit 7: 1 = 80000h-9FFFFh decoded as local
            ' motherboard DRAM; 0 = that 128 KiB window is assigned to the AT
            ' I/O channel.  The property is exposed now, while full topology
            ' enforcement is intentionally deferred until the firmware programs
            ' the register rather than relying on the host's flat-RAM default.
            Return (_rb(2) And &H80) <> 0
        End Get
    End Property

    Public Function EmsAddressExtensionBase(pageFramePage As Integer) As UInteger
        If pageFramePage < 0 OrElse pageFramePage > 3 Then Throw New ArgumentOutOfRangeException(NameOf(pageFramePage))
        ' RB10/index 6Eh stores two high address-extension bits per 16 KiB EMS
        ' page.  Encodings select 1-2, 2-4, 4-6, or 6-8 MiB address blocks;
        ' the original NEAT mapper never uses block 00 as 0-2 MiB.
        Dim shift As Integer = (3 - pageFramePage) * 2
        Dim extension As Integer = (_rb(10) >> shift) And 3
        Select Case extension
            Case 0 : Return &H100000UI
            Case 1 : Return &H200000UI
            Case 2 : Return &H400000UI
            Case Else : Return &H600000UI
        End Select
    End Function

    Public ReadOnly Property ForceA20Low As Boolean
        Get
            Return (_rb(11) And &H2) <> 0
        End Get
    End Property

    Public Function HandlesMemory(address As UInteger) As Boolean Implements IMemoryMappedDevice.HandlesMemory
        If address < &HA0000UI OrElse address > &HFFFFFUI Then Return False
        Return ShadowRamMapped(address)
    End Function

    Public Function TryReadMemoryByte(address As UInteger, ByRef value As Byte) As Boolean Implements IConditionalMemoryMappedDevice.TryReadMemoryByte
        If Not ShadowRamMapped(address) Then Return False

        ' C0000h-FFFFFh has independent ROM and RAM decode controls.  The
        ' documented shadow-copy sequence first enables the RAM mapping while
        ' leaving ROMCS active: reads still come from ROM while writes land in
        ' the hidden RAM.  Only after the copy does software disable ROMCS, at
        ' which point reads are served by the shadow RAM.  A/B shadow chunks do
        ' not have the separate RB1 ROMCS gate and therefore read RAM directly.
        If address >= &HC0000UI AndAlso Not LocalRomDisabled(address) Then Return False

        value = _shadowRam(CInt(address - &HA0000UI))
        Return True
    End Function

    Public Function TryWriteMemoryByte(address As UInteger, value As Byte) As Boolean Implements IConditionalMemoryMappedDevice.TryWriteMemoryByte
        If Not ShadowRamMapped(address) Then Return False
        If Not ShadowWriteProtected(address) Then
            _shadowRam(CInt(address - &HA0000UI)) = value
        End If
        Return True
    End Function

    Public Function ReadMemoryByte(address As UInteger) As Byte Implements IMemoryMappedDevice.ReadMemoryByte
        Dim value As Byte
        If TryReadMemoryByte(address, value) Then Return value
        Return &HFF
    End Function

    Public Sub WriteMemoryByte(address As UInteger, value As Byte) Implements IMemoryMappedDevice.WriteMemoryByte
        TryWriteMemoryByte(address, value)
    End Sub

    Private Function ShadowRamMapped(address As UInteger) As Boolean
        Dim chunk As Integer = CInt((address - &HA0000UI) \ &H4000UI)
        If chunk < 0 OrElse chunk > 23 Then Return False

        ' Official 82C212 map:
        '   RB3/67h -> A0000h-BFFFFh (8 x 16 KiB)
        '   RB4/68h -> C0000h-DFFFFh (8 x 16 KiB)
        '   RB5/69h -> E0000h-FFFFFh (8 x 16 KiB)
        ' RB2/66h is the separate 80000h-9FFFFh local-vs-ISA decode control.
        Dim registerIndex As Integer = 3 + (chunk \ 8) ' rb3, rb4, rb5
        Dim bit As Integer = chunk And 7
        Return (_rb(registerIndex) And (1 << bit)) <> 0
    End Function

    Private Function LocalRomDisabled(address As UInteger) As Boolean
        Dim group As Integer = CInt((address - &HC0000UI) \ &H10000UI) ' C,D,E,F
        If group < 0 OrElse group > 3 Then Return False
        Dim enableBit As Integer = 3 - group ' RB1 b3=C, b2=D, b1=E, b0=F
        Return (_rb(1) And (1 << enableBit)) <> 0
    End Function

    Private Function ShadowWriteProtected(address As UInteger) As Boolean
        If address < &HC0000UI Then Return False
        Dim group As Integer = CInt((address - &HC0000UI) \ &H10000UI) ' C,D,E,F
        Dim protectBit As Integer = 7 - group
        Return (_rb(1) And (1 << protectBit)) <> 0
    End Function
End Class

' ISA option-ROM/upper-memory holes are not ordinary motherboard RAM.  This
' terminator is intentionally registered after real ISA/MMIO devices so those
' devices get first decode; otherwise undriven reads float high and writes vanish.
Public Class AtIsaMemoryHole
    Implements IMemoryMappedDevice, IPageCoherentMemoryDecode, IMemoryClockIndependentDevice, IResettableDevice, IMemoryCycleTimingTargetProvider

    Public Function HandlesMemory(address As UInteger) As Boolean Implements IMemoryMappedDevice.HandlesMemory
        Return address >= &HC0000UI AndAlso address < &HF0000UI
    End Function

    Public Function ReadMemoryByte(address As UInteger) As Byte Implements IMemoryMappedDevice.ReadMemoryByte
        Return &HFF
    End Function

    Public Sub WriteMemoryByte(address As UInteger, value As Byte) Implements IMemoryMappedDevice.WriteMemoryByte
        ' No selected ISA target drives a write response.
    End Sub

    Public Function GetMemoryCycleTimingTarget(address As UInteger,
                                               isWrite As Boolean) As AtMemoryCycleTarget286 Implements IMemoryCycleTimingTargetProvider.GetMemoryCycleTimingTarget
        Return AtMemoryCycleTarget286.OpenBus
    End Function

    Public Sub ResetDevice() Implements IResettableDevice.ResetDevice
    End Sub
End Class

' Board-level NMI gate.  Port 70h bit 7 masks the CPU NMI input without erasing
' the parity/IOCHCK source latch.  Unmasking a still-asserted source therefore
' creates the physical low-to-high NMI edge seen by the processor.
Public Class AtNmiGate
    Implements IResettableDevice

    Private _masked As Boolean
    Private _sourceAsserted As Boolean
    Private _outputAsserted As Boolean

    Public Event NmiEdge()

    Public Sub ResetDevice() Implements IResettableDevice.ResetDevice
        _masked = False
        _sourceAsserted = False
        _outputAsserted = False
    End Sub

    Public Sub SetMasked(masked As Boolean)
        _masked = masked
        Recompute()
    End Sub

    Public Sub SetSource(asserted As Boolean)
        _sourceAsserted = asserted
        Recompute()
    End Sub

    Public Sub PulseSource()
        If Not _masked Then RaiseEvent NmiEdge()
    End Sub

    Private Sub Recompute()
        Dim asserted As Boolean = _sourceAsserted AndAlso Not _masked
        If asserted AndAlso Not _outputAsserted Then RaiseEvent NmiEdge()
        _outputAsserted = asserted
    End Sub
End Class

