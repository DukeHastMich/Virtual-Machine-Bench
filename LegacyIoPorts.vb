Imports System
Imports System.Collections.Generic
Imports System.Linq

' Cromwell Technologies legacy I/O board
' ----------------------------------------
' The dual UART follows the NS/TI 16550 register contract and 1.8432 MHz
' reference clock.  The parallel channel is the original IBM-compatible SPP
' data/status/control latch with a Centronics-style ACK handshake.
' UART register reference: Texas Instruments TL16C550C data sheet, Rev. I,
' https://www.ti.com/lit/ds/symlink/tl16c550c.pdf
' Mouse packet/framing reference: Microsoft Mouse Programmer's Reference,
' Microsoft serial-mouse identification and three-byte binary protocol.

Public Interface ISerialPeripheral
    Sub TransmitByte(value As Byte, dataBits As Integer, parityMode As Integer, stopBits As Double)
    Sub ModemOutputsChanged(dtr As Boolean, rts As Boolean, out1 As Boolean, out2 As Boolean)
    Sub BreakStateChanged(asserted As Boolean)
End Interface

' Active DCE peripherals receive a physical connector when installed.  Bytes
' returned through that connector are serialized by the UART receive path; the
' peripheral never writes the guest-visible RBR or PIC directly.
Public Interface IActiveSerialPeripheral
    Sub AttachToUart(uart As Uart16550A)
    Sub DetachFromUart(uart As Uart16550A)
End Interface

' Passive instrumentation observes both directions without occupying the DB-9.
Public Interface ISerialLineMonitor
    Sub ObserveUartTransmit(value As Byte, dataBits As Integer, parityMode As Integer, stopBits As Double)
    Sub ObserveUartReceive(value As Byte, lineErrorBits As Byte)
    Sub ObserveModemOutputs(dtr As Boolean, rts As Boolean, out1 As Boolean, out2 As Boolean)
    Sub ObserveBreakState(asserted As Boolean)
End Interface

Public Interface IParallelPeripheral
    Function AcceptByte(value As Byte) As Boolean
    Sub ControlLinesChanged(selectIn As Boolean, initialize As Boolean, autoFeed As Boolean)
End Interface

' Optional status contract for a real peripheral attached to the Centronics
' connector.  ParallelPortSpp owns the PC status register; the peripheral merely
' supplies the physical PAPER END, SELECTED and /ERROR conditions that appear on
' the cable.  Keeping this separate from IParallelPeripheral preserves the tiny
' byte/control contract used by diagnostic sinks while allowing printers to drive
' guest-visible status without reaching into the port-card implementation.
Public Interface IParallelStatusSource
    ReadOnly Property Busy As Boolean
    ReadOnly Property PaperEnd As Boolean
    ReadOnly Property Selected As Boolean
    ReadOnly Property ErrorOk As Boolean
End Interface

Public NotInheritable Class DiagnosticSerialPeripheral
    Implements ISerialPeripheral, ISerialLineMonitor

    Private Const MaximumCapturedBytes As Integer = 1024 * 1024
    Private ReadOnly _received As New Queue(Of Byte)()
    Private ReadOnly _transmittedToGuest As New Queue(Of Byte)()
    Private _droppedBytes As Long
    Private _breakAsserted As Boolean
    Private _dtr As Boolean
    Private _rts As Boolean
    Private _out1 As Boolean
    Private _out2 As Boolean
    Private _lastDataBits As Integer
    Private _lastParityMode As Integer
    Private _lastStopBits As Double

    Public ReadOnly Property ReceivedBytes As Byte()
        Get
            Return _received.ToArray()
        End Get
    End Property

    Public ReadOnly Property GuestReceivedBytes As Byte()
        Get
            Return _transmittedToGuest.ToArray()
        End Get
    End Property

    Public ReadOnly Property DroppedBytes As Long
        Get
            Return _droppedBytes
        End Get
    End Property

    Public ReadOnly Property BreakAsserted As Boolean
        Get
            Return _breakAsserted
        End Get
    End Property

    Public ReadOnly Property Dtr As Boolean
        Get
            Return _dtr
        End Get
    End Property

    Public ReadOnly Property Rts As Boolean
        Get
            Return _rts
        End Get
    End Property

    Public ReadOnly Property Out1 As Boolean
        Get
            Return _out1
        End Get
    End Property

    Public ReadOnly Property Out2 As Boolean
        Get
            Return _out2
        End Get
    End Property

    Public ReadOnly Property LastDataBits As Integer
        Get
            Return _lastDataBits
        End Get
    End Property

    Public ReadOnly Property LastParityMode As Integer
        Get
            Return _lastParityMode
        End Get
    End Property

    Public ReadOnly Property LastStopBits As Double
        Get
            Return _lastStopBits
        End Get
    End Property

    Public Sub Clear()
        _received.Clear()
        _transmittedToGuest.Clear()
        _droppedBytes = 0
    End Sub

    Public Sub TransmitByte(value As Byte, dataBits As Integer, parityMode As Integer, stopBits As Double) Implements ISerialPeripheral.TransmitByte
        _lastDataBits = dataBits
        _lastParityMode = parityMode
        _lastStopBits = stopBits
        If _received.Count >= MaximumCapturedBytes Then
            _received.Dequeue()
            _droppedBytes += 1
        End If
        _received.Enqueue(value)
    End Sub

    Public Sub ModemOutputsChanged(dtr As Boolean, rts As Boolean, out1 As Boolean, out2 As Boolean) Implements ISerialPeripheral.ModemOutputsChanged
        _dtr = dtr
        _rts = rts
        _out1 = out1
        _out2 = out2
    End Sub

    Public Sub BreakStateChanged(asserted As Boolean) Implements ISerialPeripheral.BreakStateChanged
        _breakAsserted = asserted
    End Sub

    Public Sub ObserveUartTransmit(value As Byte, dataBits As Integer, parityMode As Integer, stopBits As Double) Implements ISerialLineMonitor.ObserveUartTransmit
        TransmitByte(value, dataBits, parityMode, stopBits)
    End Sub

    Public Sub ObserveUartReceive(value As Byte, lineErrorBits As Byte) Implements ISerialLineMonitor.ObserveUartReceive
        If _transmittedToGuest.Count >= MaximumCapturedBytes Then
            _transmittedToGuest.Dequeue()
            _droppedBytes += 1
        End If
        _transmittedToGuest.Enqueue(value)
    End Sub

    Public Sub ObserveModemOutputs(dtr As Boolean, rts As Boolean, out1 As Boolean, out2 As Boolean) Implements ISerialLineMonitor.ObserveModemOutputs
        ModemOutputsChanged(dtr, rts, out1, out2)
    End Sub

    Public Sub ObserveBreakState(asserted As Boolean) Implements ISerialLineMonitor.ObserveBreakState
        BreakStateChanged(asserted)
    End Sub
End Class

Public NotInheritable Class DiagnosticParallelPeripheral
    Implements IParallelPeripheral

    Private Const MaximumCapturedBytes As Integer = 1024 * 1024
    Private ReadOnly _received As New Queue(Of Byte)()
    Private _droppedBytes As Long
    Private _selectInAsserted As Boolean
    Private _initializeAsserted As Boolean
    Private _autoFeedAsserted As Boolean

    Public ReadOnly Property ReceivedBytes As Byte()
        Get
            Return _received.ToArray()
        End Get
    End Property

    Public ReadOnly Property DroppedBytes As Long
        Get
            Return _droppedBytes
        End Get
    End Property

    Public ReadOnly Property SelectInAsserted As Boolean
        Get
            Return _selectInAsserted
        End Get
    End Property

    Public ReadOnly Property InitializeAsserted As Boolean
        Get
            Return _initializeAsserted
        End Get
    End Property

    Public ReadOnly Property AutoFeedAsserted As Boolean
        Get
            Return _autoFeedAsserted
        End Get
    End Property

    Public Sub Clear()
        _received.Clear()
        _droppedBytes = 0
    End Sub

    Public Function AcceptByte(value As Byte) As Boolean Implements IParallelPeripheral.AcceptByte
        If Not _selectInAsserted OrElse _initializeAsserted Then Return False
        If _received.Count >= MaximumCapturedBytes Then
            _received.Dequeue()
            _droppedBytes += 1
        End If
        _received.Enqueue(value)
        Return True
    End Function

    Public Sub ControlLinesChanged(selectIn As Boolean, initialize As Boolean, autoFeed As Boolean) Implements IParallelPeripheral.ControlLinesChanged
        _selectInAsserted = selectIn
        _initializeAsserted = initialize
        _autoFeedAsserted = autoFeed
    End Sub
End Class

Public NotInheritable Class Uart16550A
    Implements IPortDevice, IClockedDevice, IClockWakeSource, IResettableDevice

    Private Const CrystalHz As Long = 1843200L
    Private ReadOnly _basePort As UInt16
    Private ReadOnly _irq As Integer
    Private ReadOnly _pic As Pic8259
    Private NotInheritable Class ReceivedCharacter
        Public ReadOnly Value As Byte
        Public Errors As Byte

        Public Sub New(value As Byte, errors As Byte)
            Me.Value = value
            Me.Errors = CByte(errors And &H1C)
        End Sub
    End Class


    Private NotInheritable Class SerialReceiveFrame
        Public ReadOnly Value As Byte
        Public ReadOnly BaudRate As Integer
        Public ReadOnly DataBits As Integer
        Public ReadOnly ParityMode As Integer
        Public ReadOnly StopBits As Double
        Public ReadOnly SourceErrors As Byte

        Public Sub New(value As Byte,
                       baudRate As Integer,
                       dataBits As Integer,
                       parityMode As Integer,
                       stopBits As Double,
                       sourceErrors As Byte)
            Me.Value = value
            Me.BaudRate = Math.Max(1, baudRate)
            Me.DataBits = Math.Max(5, Math.Min(8, dataBits))
            Me.ParityMode = parityMode And 7
            Me.StopBits = stopBits
            Me.SourceErrors = CByte(sourceErrors And &H1C)
        End Sub
    End Class

    Private ReadOnly _rxFifo As New Queue(Of ReceivedCharacter)()
    Private ReadOnly _txFifo As New Queue(Of Byte)()
    Private ReadOnly _rxWireQueue As New Queue(Of SerialReceiveFrame)()
    Private Const MaximumReceiveWireFrames As Integer = 1024

    Private _ier As Byte
    Private _lcr As Byte
    Private _mcr As Byte
    Private _fcr As Byte
    Private _scr As Byte
    Private _dll As Byte
    Private _dlm As Byte
    Private _overrunError As Boolean
    Private _receiverBufferValue As Byte
    Private _externalModemInputs As Byte
    Private _effectiveModemInputs As Byte
    Private _msrDelta As Byte
    Private _fifoEnabled As Boolean
    Private _threInterruptPending As Boolean
    Private _txShiftActive As Boolean
    Private _txShiftValue As Byte
    Private _txRemainingPicoseconds As Long
    Private _txHoldingTransferPicoseconds As Long
    Private _rxTimeoutRemainingPicoseconds As Long
    Private _rxTimeoutPending As Boolean
    Private _peripheral As ISerialPeripheral
    Private _monitor As ISerialLineMonitor
    Private _rxShiftActive As Boolean
    Private _rxShiftFrame As SerialReceiveFrame
    Private _rxShiftRemainingPicoseconds As Long
    Private _rxWireDroppedFrames As Long
    Private Const MaximumDiagnosticEvents As Integer = 256
    Private ReadOnly _diagnosticEvents As New Queue(Of String)()
    Private _diagnosticSequence As Long
    Private _lastPolledLsr As Integer = -1
    Private _lastPolledLsrCount As Long

    Public Event TransmitActivity()
    Public Event ReceiveActivity()

    Public Sub New(basePort As UInt16, irq As Integer, pic As Pic8259)
        If pic Is Nothing Then Throw New ArgumentNullException(NameOf(pic))
        If irq < 0 OrElse irq > 7 Then Throw New ArgumentOutOfRangeException(NameOf(irq))
        _basePort = basePort
        _irq = irq
        _pic = pic
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

    Public Function DiagnosticText() As String
        Dim divisor As Integer = (CInt(_dlm) << 8) Or CInt(_dll)
        Dim baud As Long = If(divisor = 0, 0L, CrystalHz \ (16L * divisor))
        Dim summary As String = $"UART {_basePort:X4}h IRQ{_irq}: IER={_ier:X2} IIR={InterruptIdentification():X2} LCR={_lcr:X2} MCR={_mcr:X2} " &
               $"LSR={LineStatus():X2} MSR={(CurrentModemInputs() Or _msrDelta):X2} SCR={_scr:X2} divisor={divisor} baud={baud} " &
               $"FIFO={If(_fifoEnabled, "on", "off")} RX={_rxFifo.Count}/16 TX={_txFifo.Count}/16 shift={If(_txShiftActive, "busy", "idle")} " &
               $"THR-transfer={If(_txHoldingTransferPicoseconds > 0, _txHoldingTransferPicoseconds.ToString(), "idle")} " &
               $"rx-wire={If(_rxShiftActive, "busy", "idle")} queued={_rxWireQueue.Count} dropped={_rxWireDroppedFrames} " &
               $"rx-timeout={If(_rxTimeoutPending, "pending", If(_rxTimeoutRemainingPicoseconds > 0, _rxTimeoutRemainingPicoseconds.ToString(), "idle"))}"
        Dim detail As New System.Text.StringBuilder(summary)
        detail.AppendLine()
        detail.AppendLine($"UART {_basePort:X4}h recent bus-visible history (oldest first; retained across warm reset):")
        For Each entry As String In _diagnosticEvents
            detail.AppendLine(entry)
        Next
        If _lastPolledLsrCount > 0 Then
            detail.AppendLine($"  pending: LSR read {_lastPolledLsrCount} time(s), value={_lastPolledLsr:X2}")
        End If
        Return detail.ToString().TrimEnd()
    End Function

    Private Sub FlushPolledLineStatus()
        If _lastPolledLsrCount <= 0 Then Return
        AddDiagnosticEvent($"LSR read {_lastPolledLsrCount} time(s), value={_lastPolledLsr:X2}")
        _lastPolledLsr = -1
        _lastPolledLsrCount = 0
    End Sub

    Private Sub RecordPolledLineStatus(value As Byte)
        If _lastPolledLsr = value Then
            _lastPolledLsrCount += 1
            Return
        End If
        FlushPolledLineStatus()
        _lastPolledLsr = value
        _lastPolledLsrCount = 1
    End Sub

    Private Sub AddDiagnosticEvent(description As String)
        _diagnosticSequence += 1
        While _diagnosticEvents.Count >= MaximumDiagnosticEvents
            _diagnosticEvents.Dequeue()
        End While
        _diagnosticEvents.Enqueue($"  #{_diagnosticSequence}: {description}")
    End Sub

    Private Sub RecordRegisterWrite(offset As Integer, value As Byte)
        FlushPolledLineStatus()
        AddDiagnosticEvent($"OUT {CUShort(_basePort + offset):X4}h <- {value:X2} " &
                           $"(DLAB={If((_lcr And &H80) <> 0, 1, 0)} LCR={_lcr:X2} MCR={_mcr:X2})")
    End Sub

    Public Property Peripheral As ISerialPeripheral
        Get
            Return _peripheral
        End Get
        Set(value As ISerialPeripheral)
            Dim oldActive As IActiveSerialPeripheral = TryCast(_peripheral, IActiveSerialPeripheral)
            If oldActive IsNot Nothing Then oldActive.DetachFromUart(Me)
            _peripheral = value
            Dim newActive As IActiveSerialPeripheral = TryCast(_peripheral, IActiveSerialPeripheral)
            If newActive IsNot Nothing Then newActive.AttachToUart(Me)
            NotifyModemOutputs()
            NotifyBreakState()
        End Set
    End Property

    Public Property Monitor As ISerialLineMonitor
        Get
            Return _monitor
        End Get
        Set(value As ISerialLineMonitor)
            _monitor = value
            NotifyModemOutputs()
            NotifyBreakState()
        End Set
    End Property

    Public ReadOnly Property ReceiveWireFreeFrames As Integer
        Get
            Return Math.Max(0, MaximumReceiveWireFrames - _rxWireQueue.Count - If(_rxShiftActive, 1, 0))
        End Get
    End Property

    ' Queue one complete asynchronous frame at the connector.  The byte does
    ' not become visible in RBR/FIFO until the physical frame time has elapsed.
    Public Function QueueReceivedSerialByte(value As Byte,
                                            baudRate As Integer,
                                            dataBits As Integer,
                                            parityMode As Integer,
                                            stopBits As Double,
                                            Optional sourceErrors As Byte = 0) As Boolean
        If ReceiveWireFreeFrames <= 0 Then
            _rxWireDroppedFrames += 1
            Return False
        End If
        _rxWireQueue.Enqueue(New SerialReceiveFrame(value,
                                                     baudRate,
                                                     dataBits,
                                                     parityMode,
                                                     stopBits,
                                                     sourceErrors))
        BeginReceiveIfIdle()
        Return True
    End Function

    Public Function QueueReceivedSerialBytes(values As IEnumerable(Of Byte),
                                             baudRate As Integer,
                                             dataBits As Integer,
                                             parityMode As Integer,
                                             stopBits As Double) As Boolean
        If values Is Nothing Then Throw New ArgumentNullException(NameOf(values))
        Dim materialized As Byte() = values.ToArray()
        If materialized.Length > ReceiveWireFreeFrames Then
            _rxWireDroppedFrames += materialized.Length
            Return False
        End If
        For Each value As Byte In materialized
            _rxWireQueue.Enqueue(New SerialReceiveFrame(value, baudRate, dataBits, parityMode, stopBits, 0))
        Next
        BeginReceiveIfIdle()
        Return True
    End Function

    Public Sub SetExternalModemInputs(cts As Boolean, dsr As Boolean, ringIndicator As Boolean, carrierDetect As Boolean)
        Dim nextInputs As Byte
        If cts Then nextInputs = CByte(nextInputs Or &H10)
        If dsr Then nextInputs = CByte(nextInputs Or &H20)
        If ringIndicator Then nextInputs = CByte(nextInputs Or &H40)
        If carrierDetect Then nextInputs = CByte(nextInputs Or &H80)
        _externalModemInputs = nextInputs
        If (_mcr And &H10) = 0 Then ApplyModemInputs(nextInputs)
    End Sub

    Public Sub InjectReceivedByte(value As Byte, Optional lineErrorBits As Byte = 0)
        CommitReceivedCharacter(value, lineErrorBits)
    End Sub

    Private Sub CommitReceivedCharacter(value As Byte, lineErrorBits As Byte)
        Dim capacity As Integer = If(_fifoEnabled, 16, 1)
        If _rxFifo.Count >= capacity Then
            _overrunError = True
            If Not _fifoEnabled Then
                ' In 16450-compatible mode the single RBR latch is overwritten
                ' by the newly completed character.  FIFO-full operation instead
                ' preserves all queued characters and discards the new arrival.
                _rxFifo.Dequeue()
                _receiverBufferValue = value
                _rxFifo.Enqueue(New ReceivedCharacter(value, lineErrorBits))
            End If
        Else
            _receiverBufferValue = value
            _rxFifo.Enqueue(New ReceivedCharacter(value, lineErrorBits))
        End If
        ' This is connector activity even when a full FIFO discards the frame.
        RaiseEvent ReceiveActivity()
        If _monitor IsNot Nothing Then _monitor.ObserveUartReceive(value, lineErrorBits)
        RestartReceiverTimeout()
        UpdateIrq()
    End Sub

    Public Function HandlesPort(port As UInt16) As Boolean Implements IPortDevice.HandlesPort
        Return port >= _basePort AndAlso port <= CUShort(_basePort + 7US)
    End Function

    Public Function ReadPort(port As UInt16) As Byte Implements IPortDevice.ReadPort
        Dim offset As Integer = CInt(port - _basePort)
        Select Case offset
            Case 0
                If (_lcr And &H80) <> 0 Then Return _dll
                If _rxFifo.Count > 0 Then _receiverBufferValue = _rxFifo.Dequeue().Value
                RestartReceiverTimeout()
                UpdateIrq()
                Return _receiverBufferValue
            Case 1
                If (_lcr And &H80) <> 0 Then Return _dlm
                Return _ier
            Case 2
                Dim value As Byte = InterruptIdentification()
                If (value And &HE) = &H2 Then
                    _threInterruptPending = False
                    UpdateIrq()
                End If
                Return value
            Case 3 : Return _lcr
            Case 4 : Return _mcr
            Case 5
                Dim value As Byte = LineStatus()
                RecordPolledLineStatus(value)
                _overrunError = False
                If _rxFifo.Count > 0 Then _rxFifo.Peek().Errors = 0
                UpdateIrq()
                Return value
            Case 6
                Dim value As Byte = CByte(CurrentModemInputs() Or _msrDelta)
                _msrDelta = 0
                UpdateIrq()
                Return value
            Case 7 : Return _scr
            Case Else : Return &HFF
        End Select
    End Function

    Public Sub WritePort(port As UInt16, value As Byte) Implements IPortDevice.WritePort
        Dim offset As Integer = CInt(port - _basePort)
        RecordRegisterWrite(offset, value)
        Select Case offset
            Case 0
                If (_lcr And &H80) <> 0 Then
                    _dll = value
                    RestartReceiverTimeout()
                Else
                    WriteTransmitter(value)
                End If
            Case 1
                If (_lcr And &H80) <> 0 Then
                    _dlm = value
                    RestartReceiverTimeout()
                Else
                    Dim oldIer As Byte = _ier
                    _ier = CByte(value And &HF)
                    If (oldIer And &H2) = 0 AndAlso (_ier And &H2) <> 0 AndAlso (LineStatus() And &H20) <> 0 Then
                        _threInterruptPending = True
                    End If
                    UpdateIrq()
                End If
            Case 2 : WriteFifoControl(value)
            Case 3
                Dim oldBreakState As Boolean = (_lcr And &H40) <> 0
                _lcr = value
                If oldBreakState <> ((_lcr And &H40) <> 0) Then NotifyBreakState()
                RestartReceiverTimeout()
            Case 4
                _mcr = CByte(value And &H1F)
                NotifyModemOutputs()
                NotifyBreakState()
                RefreshLoopbackInputs()
                UpdateIrq()
            Case 7 : _scr = value
        End Select
    End Sub

    Private Sub WriteTransmitter(value As Byte)
        Dim capacity As Integer = If(_fifoEnabled, 16, 1)
        If _txFifo.Count < capacity Then
            _txFifo.Enqueue(value)
        ElseIf Not _fifoEnabled Then
            ' A second write before the one-byte THR transfers to the shift
            ' register replaces that holding-latch value; there is no TX overrun.
            _txFifo.Dequeue()
            _txFifo.Enqueue(value)
        End If
        _threInterruptPending = False
        If Not _txShiftActive AndAlso _txFifo.Count > 0 AndAlso _txHoldingTransferPicoseconds <= 0 Then
            ' Loading THR clears THRE immediately.  An idle 16550 does not make
            ' THR empty in the same ISA I/O transaction: the baud generator's
            ' next 16x transmitter clock transfers THR into TSR.  Keeping that
            ' real interval is required by drivers which verify THRE cleared.
            _txHoldingTransferPicoseconds = TransmitterClockPicoseconds()
            FlushPolledLineStatus()
            AddDiagnosticEvent($"THR loaded value={value:X2}, transfer in {_txHoldingTransferPicoseconds} ps")
        End If
        UpdateIrq()
    End Sub

    Private Sub WriteFifoControl(value As Byte)
        Dim enable As Boolean = (value And 1) <> 0
        If enable <> _fifoEnabled Then
            _rxFifo.Clear()
            _txFifo.Clear()
            _txHoldingTransferPicoseconds = 0
            _threInterruptPending = True
        End If
        _fifoEnabled = enable
        ' FCR bit 3 selects the package RXRDY/TXRDY DMA pin behavior.  It is
        ' retained in the register model, but this AT ISA board has no serial
        ' DMA channel wiring; ordinary PC drivers use IRQ or polled service.
        _fcr = If(enable, CByte(value And &HC9), CByte(0))
        If enable AndAlso (value And &H2) <> 0 Then
            _rxFifo.Clear()
            _overrunError = False
        End If
        If enable AndAlso (value And &H4) <> 0 Then
            _txFifo.Clear()
            _txHoldingTransferPicoseconds = 0
            If Not _txShiftActive Then _threInterruptPending = True
        End If
        RestartReceiverTimeout()
        UpdateIrq()
    End Sub

    Private Sub BeginTransmitIfIdle()
        If _txShiftActive OrElse _txFifo.Count = 0 OrElse _txHoldingTransferPicoseconds > 0 Then Return
        _txShiftValue = _txFifo.Dequeue()
        _txShiftActive = True
        _txRemainingPicoseconds = CharacterPicoseconds()
        FlushPolledLineStatus()
        AddDiagnosticEvent($"TX shift start value={_txShiftValue:X2}, frame={_txRemainingPicoseconds} ps, THR={_txFifo.Count}")
        If _txFifo.Count = 0 Then _threInterruptPending = True
    End Sub

    Private Sub BeginReceiveIfIdle()
        If _rxShiftActive OrElse _rxWireQueue.Count = 0 Then Return
        _rxShiftFrame = _rxWireQueue.Dequeue()
        _rxShiftActive = True
        _rxShiftRemainingPicoseconds = SerialFramePicoseconds(_rxShiftFrame.BaudRate,
                                                               _rxShiftFrame.DataBits,
                                                               _rxShiftFrame.ParityMode,
                                                               _rxShiftFrame.StopBits)
    End Sub

    Private Shared Function SerialFramePicoseconds(baudRate As Integer,
                                                   dataBits As Integer,
                                                   parityMode As Integer,
                                                   stopBits As Double) As Long
        Dim parityBits As Integer = If((parityMode And 1) <> 0, 1, 0)
        Dim totalBits As Double = 1.0R + dataBits + parityBits + stopBits
        Return Math.Max(1L,
                        CLng(Math.Ceiling(totalBits * MachineProfile286.PicosecondsPerSecond /
                                          Math.Max(1, baudRate))))
    End Function

    Private Sub CompleteReceiveFrame()
        Dim frame As SerialReceiveFrame = _rxShiftFrame
        _rxShiftFrame = Nothing
        _rxShiftActive = False
        _rxShiftRemainingPicoseconds = 0
        If frame Is Nothing Then Return

        Dim configuredDivisor As Integer = (CInt(_dlm) << 8) Or CInt(_dll)
        If configuredDivisor = 0 Then configuredDivisor = 1
        Dim configuredBaud As Double = CrystalHz / (16.0R * configuredDivisor)
        Dim configuredDataBits As Integer = 5 + (_lcr And 3)
        Dim configuredParityMode As Integer = (_lcr >> 3) And 7
        Dim configuredStopBits As Double = If((_lcr And 4) = 0,
                                              1.0R,
                                              If(configuredDataBits = 5, 1.5R, 2.0R))
        Dim errors As Byte = frame.SourceErrors
        Dim baudError As Double = Math.Abs(configuredBaud - frame.BaudRate) / frame.BaudRate
        If baudError > 0.04R OrElse configuredDataBits <> frame.DataBits OrElse
           Math.Abs(configuredStopBits - frame.StopBits) > 0.25R Then
            errors = CByte(errors Or &H8) ' framing error
        End If
        If configuredParityMode <> frame.ParityMode Then errors = CByte(errors Or &H4)

        Dim dataMask As Integer = (1 << configuredDataBits) - 1
        CommitReceivedCharacter(CByte(CInt(frame.Value) And dataMask), errors)
        BeginReceiveIfIdle()
    End Sub

    Private Function CharacterPicoseconds() As Long
        Dim divisor As Integer = (CInt(_dlm) << 8) Or CInt(_dll)
        If divisor = 0 Then divisor = 1
        Dim dataBits As Integer = 5 + (_lcr And 3)
        Dim parityBits As Integer = If((_lcr And &H8) <> 0, 1, 0)
        Dim stopBits As Double = If((_lcr And &H4) = 0, 1.0R, If(dataBits = 5, 1.5R, 2.0R))
        Dim totalBits As Double = 1.0R + dataBits + parityBits + stopBits
        Return Math.Max(1L, CLng(Math.Ceiling(totalBits * 16.0R * divisor * MachineProfile286.PicosecondsPerSecond / CrystalHz)))
    End Function

    Private Function TransmitterClockPicoseconds() As Long
        Dim divisor As Integer = (CInt(_dlm) << 8) Or CInt(_dll)
        If divisor = 0 Then divisor = 1
        Return Math.Max(1L, CLng(Math.Ceiling(divisor * MachineProfile286.PicosecondsPerSecond / CDbl(CrystalHz))))
    End Function

    Private Function LineStatus() As Byte
        Dim value As Byte
        If _overrunError Then value = CByte(value Or &H2)
        If _rxFifo.Count > 0 Then value = CByte(value Or _rxFifo.Peek().Errors)
        If _rxFifo.Count > 0 Then value = CByte(value Or &H1)
        If _txFifo.Count = 0 Then value = CByte(value Or &H20)
        If _txFifo.Count = 0 AndAlso Not _txShiftActive Then value = CByte(value Or &H40)
        If _fifoEnabled AndAlso ReceiverFifoContainsError() Then value = CByte(value Or &H80)
        Return value
    End Function

    Private Function ReceiverFifoContainsError() As Boolean
        For Each character As ReceivedCharacter In _rxFifo
            If character.Errors <> 0 Then Return True
        Next
        Return False
    End Function

    Private Function ReceiverTriggerLevel() As Integer
        If Not _fifoEnabled Then Return 1
        Select Case (_fcr >> 6) And 3
            Case 0 : Return 1
            Case 1 : Return 4
            Case 2 : Return 8
            Case Else : Return 14
        End Select
    End Function

    Private Function InterruptIdentification() As Byte
        Dim result As Byte
        If (_ier And &H4) <> 0 AndAlso (LineStatus() And &H1E) <> 0 Then
            result = &H6
        ElseIf (_ier And &H1) <> 0 AndAlso _rxFifo.Count >= ReceiverTriggerLevel() Then
            result = &H4
        ElseIf (_ier And &H1) <> 0 AndAlso _fifoEnabled AndAlso _rxTimeoutPending AndAlso _rxFifo.Count > 0 Then
            result = &HC
        ElseIf (_ier And &H2) <> 0 AndAlso _threInterruptPending Then
            result = &H2
        ElseIf (_ier And &H8) <> 0 AndAlso _msrDelta <> 0 Then
            result = &H0
        Else
            result = &H1
        End If
        If _fifoEnabled Then result = CByte(result Or &HC0)
        Return result
    End Function

    Private Function CurrentModemInputs() As Byte
        If (_mcr And &H10) = 0 Then Return _externalModemInputs
        Dim value As Byte
        If (_mcr And &H2) <> 0 Then value = CByte(value Or &H10) ' RTS -> CTS
        If (_mcr And &H1) <> 0 Then value = CByte(value Or &H20) ' DTR -> DSR
        If (_mcr And &H4) <> 0 Then value = CByte(value Or &H40) ' OUT1 -> RI
        If (_mcr And &H8) <> 0 Then value = CByte(value Or &H80) ' OUT2 -> DCD
        Return value
    End Function

    Private Sub RefreshLoopbackInputs()
        ApplyModemInputs(CurrentModemInputs())
    End Sub

    Private Sub ApplyModemInputs(nextInputs As Byte)
        Dim oldInputs As Byte = _effectiveModemInputs
        Dim changed As Byte = CByte(oldInputs Xor nextInputs)
        If (changed And &H10) <> 0 Then _msrDelta = CByte(_msrDelta Or &H1)
        If (changed And &H20) <> 0 Then _msrDelta = CByte(_msrDelta Or &H2)
        If (oldInputs And &H40) <> 0 AndAlso (nextInputs And &H40) = 0 Then _msrDelta = CByte(_msrDelta Or &H4)
        If (changed And &H80) <> 0 Then _msrDelta = CByte(_msrDelta Or &H8)
        _effectiveModemInputs = CByte(nextInputs And &HF0)
        UpdateIrq()
    End Sub

    Private Sub NotifyModemOutputs()
        Dim loopback As Boolean = (_mcr And &H10) <> 0
        Dim dtr As Boolean = Not loopback AndAlso (_mcr And 1) <> 0
        Dim rts As Boolean = Not loopback AndAlso (_mcr And 2) <> 0
        Dim out1 As Boolean = Not loopback AndAlso (_mcr And 4) <> 0
        Dim out2 As Boolean = Not loopback AndAlso (_mcr And 8) <> 0
        If _peripheral IsNot Nothing Then _peripheral.ModemOutputsChanged(dtr, rts, out1, out2)
        If _monitor IsNot Nothing Then _monitor.ObserveModemOutputs(dtr, rts, out1, out2)
    End Sub

    Private Sub NotifyBreakState()
        ' Loopback disconnects the external serial pins; SOUT is held inactive.
        Dim asserted As Boolean = (_mcr And &H10) = 0 AndAlso (_lcr And &H40) <> 0
        If _peripheral IsNot Nothing Then _peripheral.BreakStateChanged(asserted)
        If _monitor IsNot Nothing Then _monitor.ObserveBreakState(asserted)
    End Sub

    Private Sub RestartReceiverTimeout()
        _rxTimeoutPending = False
        If _fifoEnabled AndAlso _rxFifo.Count > 0 Then
            Dim characterTime As Long = CharacterPicoseconds()
            If characterTime > Long.MaxValue \ 4L Then
                _rxTimeoutRemainingPicoseconds = Long.MaxValue
            Else
                _rxTimeoutRemainingPicoseconds = characterTime * 4L
            End If
        Else
            _rxTimeoutRemainingPicoseconds = 0
        End If
    End Sub

    Private Sub UpdateIrq()
        Dim pending As Boolean = (InterruptIdentification() And 1) = 0
        _pic.SetIrqLine(_irq, pending AndAlso (_mcr And &H8) <> 0)
    End Sub

    Public Sub AdvanceTime(elapsedPicoseconds As Long) Implements IClockedDevice.AdvanceTime
        If elapsedPicoseconds < 0 Then Throw New ArgumentOutOfRangeException(NameOf(elapsedPicoseconds))
        Dim remaining As Long = elapsedPicoseconds
        BeginTransmitIfIdle()
        BeginReceiveIfIdle()
        While remaining > 0
            Dim stepPicoseconds As Long = remaining
            If Not _txShiftActive AndAlso _txFifo.Count > 0 AndAlso _txHoldingTransferPicoseconds > 0 Then
                stepPicoseconds = Math.Min(stepPicoseconds, _txHoldingTransferPicoseconds)
            End If
            If _txShiftActive Then stepPicoseconds = Math.Min(stepPicoseconds, _txRemainingPicoseconds)
            If _rxShiftActive Then stepPicoseconds = Math.Min(stepPicoseconds, _rxShiftRemainingPicoseconds)
            Dim timeoutClockRunning As Boolean =
                _fifoEnabled AndAlso _rxFifo.Count > 0 AndAlso Not _rxTimeoutPending AndAlso
                _rxTimeoutRemainingPicoseconds > 0
            If timeoutClockRunning Then
                stepPicoseconds = Math.Min(stepPicoseconds, _rxTimeoutRemainingPicoseconds)
            End If

            remaining -= stepPicoseconds
            If Not _txShiftActive AndAlso _txFifo.Count > 0 AndAlso _txHoldingTransferPicoseconds > 0 Then
                _txHoldingTransferPicoseconds -= stepPicoseconds
            End If
            If _txShiftActive Then _txRemainingPicoseconds -= stepPicoseconds
            If _rxShiftActive Then _rxShiftRemainingPicoseconds -= stepPicoseconds
            If timeoutClockRunning Then _rxTimeoutRemainingPicoseconds -= stepPicoseconds

            If _txShiftActive AndAlso _txRemainingPicoseconds <= 0 Then CompleteTransmitFrame()
            If Not _txShiftActive AndAlso _txFifo.Count > 0 AndAlso _txHoldingTransferPicoseconds <= 0 Then
                _txHoldingTransferPicoseconds = 0
                BeginTransmitIfIdle()
            End If
            If _rxShiftActive AndAlso _rxShiftRemainingPicoseconds <= 0 Then CompleteReceiveFrame()
            If timeoutClockRunning AndAlso _rxTimeoutRemainingPicoseconds <= 0 AndAlso
               _fifoEnabled AndAlso _rxFifo.Count > 0 AndAlso Not _rxTimeoutPending Then
                _rxTimeoutRemainingPicoseconds = 0
                _rxTimeoutPending = True
                UpdateIrq()
            End If
        End While
    End Sub

    Private Sub CompleteTransmitFrame()
        Dim completed As Byte = _txShiftValue
        _txShiftActive = False
        _txRemainingPicoseconds = 0
        FlushPolledLineStatus()
        AddDiagnosticEvent($"TX shift complete value={completed:X2}, THR={_txFifo.Count}")
        RaiseEvent TransmitActivity()
        Dim dataBits As Integer = 5 + (_lcr And 3)
        Dim wireValue As Byte = CByte(CInt(completed) And ((1 << dataBits) - 1))
        If (_mcr And &H10) <> 0 Then
            CommitReceivedCharacter(wireValue, 0)
        ElseIf (_lcr And &H40) = 0 Then
            Dim parityMode As Integer = (_lcr >> 3) And 7
            Dim stopBits As Double = If((_lcr And 4) = 0, 1.0R, If(dataBits = 5, 1.5R, 2.0R))
            If _peripheral IsNot Nothing Then _peripheral.TransmitByte(wireValue, dataBits, parityMode, stopBits)
            If _monitor IsNot Nothing Then _monitor.ObserveUartTransmit(wireValue, dataBits, parityMode, stopBits)
        End If
        BeginTransmitIfIdle()
        UpdateIrq()
    End Sub

    Public Function PicosecondsUntilNextWakeEvent() As Long Implements IClockWakeSource.PicosecondsUntilNextWakeEvent
        Dim nextWake As Long = Long.MaxValue
        If _txShiftActive Then nextWake = Math.Max(1L, _txRemainingPicoseconds)
        If Not _txShiftActive AndAlso _txFifo.Count > 0 AndAlso _txHoldingTransferPicoseconds > 0 Then
            nextWake = Math.Min(nextWake, Math.Max(1L, _txHoldingTransferPicoseconds))
        End If
        If _rxShiftActive Then nextWake = Math.Min(nextWake, Math.Max(1L, _rxShiftRemainingPicoseconds))
        If _fifoEnabled AndAlso _rxFifo.Count > 0 AndAlso Not _rxTimeoutPending AndAlso _rxTimeoutRemainingPicoseconds > 0 Then
            nextWake = Math.Min(nextWake, Math.Max(1L, _rxTimeoutRemainingPicoseconds))
        End If
        Return nextWake
    End Function

    Public Sub ResetDevice() Implements IResettableDevice.ResetDevice
        FlushPolledLineStatus()
        AddDiagnosticEvent("UART master reset")
        _pic.SetIrqLine(_irq, False)
        _rxFifo.Clear()
        _txFifo.Clear()
        _rxWireQueue.Clear()
        _ier = 0
        _lcr = 0
        _mcr = 0
        _fcr = 0
        ' A 16550 master reset does not alter SCR or the divisor latches.
        ' They retain their power-on value at construction and survive warm reset.
        _overrunError = False
        _effectiveModemInputs = _externalModemInputs
        _msrDelta = 0
        _fifoEnabled = False
        _threInterruptPending = True
        _txShiftActive = False
        _txRemainingPicoseconds = 0
        _txHoldingTransferPicoseconds = 0
        _rxShiftActive = False
        _rxShiftFrame = Nothing
        _rxShiftRemainingPicoseconds = 0
        _rxTimeoutRemainingPicoseconds = 0
        _rxTimeoutPending = False
        NotifyModemOutputs()
        NotifyBreakState()
    End Sub
End Class

Public NotInheritable Class MicrosoftSerialMouse
    Implements ISerialPeripheral, IActiveSerialPeripheral, IClockedDevice, IClockWakeSource

    ' Microsoft two-button serial protocol: 1200 baud, 7 data bits, no parity,
    ' one stop bit.  Movement reports are limited to signed eight-bit deltas and
    ' use a synchronization bit in the first of three seven-bit characters.
    Private Const MouseBaud As Integer = 1200
    Private Const MouseDataBits As Integer = 7
    Private Const MouseParityMode As Integer = 0
    Private Const MouseStopBits As Double = 1.0R
    Private Const SampleIntervalPicoseconds As Long = 25000000000L

    Private _uart As Uart16550A
    Private _dtr As Boolean
    Private _rts As Boolean
    Private _powered As Boolean
    Private _identifyArmed As Boolean = True
    Private _identificationPending As Boolean
    Private _breakAsserted As Boolean
    Private _pendingX As Long
    Private _pendingY As Long
    Private _leftButton As Boolean
    Private _rightButton As Boolean
    Private _reportedLeftButton As Boolean
    Private _reportedRightButton As Boolean
    Private _sampleRemainingPicoseconds As Long
    Private _identificationsSent As Long
    Private _packetsSent As Long
    Private _packetsDeferred As Long
    Private _hostMovementEvents As Long

    Public Sub AttachToUart(uart As Uart16550A) Implements IActiveSerialPeripheral.AttachToUart
        _uart = uart
    End Sub

    Public Sub DetachFromUart(uart As Uart16550A) Implements IActiveSerialPeripheral.DetachFromUart
        If Object.ReferenceEquals(_uart, uart) Then _uart = Nothing
    End Sub

    Public Sub TransmitByte(value As Byte,
                            dataBits As Integer,
                            parityMode As Integer,
                            stopBits As Double) Implements ISerialPeripheral.TransmitByte
        ' The original Microsoft mouse has no command channel.  Driver writes
        ' still traverse TXD electrically but do not alter the mouse protocol.
    End Sub

    Public Sub ModemOutputsChanged(dtr As Boolean,
                                   rts As Boolean,
                                   out1 As Boolean,
                                   out2 As Boolean) Implements ISerialPeripheral.ModemOutputsChanged
        _dtr = dtr
        _rts = rts
        ReevaluatePower()
    End Sub

    Private Sub ReevaluatePower()
        Dim wasPowered As Boolean = _powered
        ' The mouse obtains power from the positive DTR/RTS rails.  TXD BREAK is
        ' data-line state and must not turn the peripheral off or retrigger ID.
        _powered = _dtr AndAlso _rts
        If Not _powered Then
            _identifyArmed = True
            _identificationPending = False
            _pendingX = 0
            _pendingY = 0
            _reportedLeftButton = False
            _reportedRightButton = False
            _sampleRemainingPicoseconds = 0
            Return
        End If

        If Not wasPowered AndAlso _identifyArmed Then
            _identifyArmed = False
            _identificationPending = True
            ArmSampleClock(1)
        End If
    End Sub

    Public Sub BreakStateChanged(asserted As Boolean) Implements ISerialPeripheral.BreakStateChanged
        _breakAsserted = asserted
        ReevaluatePower()
    End Sub

    Public Sub AddHostMovement(deltaX As Integer, deltaY As Integer)
        If Not _powered Then Return
        ' The fitted Windows 3.1 serial-mouse driver interprets the device's
        ' quadrature count polarity opposite to WinForms client coordinates on
        ' both axes. Convert the host deltas once here, before packet framing.
        _pendingX = SaturatingAdd(_pendingX, -CLng(deltaX))
        _pendingY = SaturatingAdd(_pendingY, -CLng(deltaY))
        _hostMovementEvents += 1

        ' Motion is itself a report-triggering physical event.  Waiting a full
        ' sample interval before starting an idle serial link makes host motion
        ' wait on 25 ms of guest time; a later button edge then appears to make
        ' the cursor jump because SetHostButtons correctly wakes the mouse at
        ' once.  Start the first pending report immediately.  UART admission,
        ' 1200-baud framing, IRQ4 and subsequent backlog pacing remain on the
        ' real emulated serial path.
        ArmSampleClock(1)
    End Sub

    Public Sub SetHostButtons(leftPressed As Boolean, rightPressed As Boolean)
        _leftButton = leftPressed
        _rightButton = rightPressed
        If _powered AndAlso
           (_leftButton <> _reportedLeftButton OrElse _rightButton <> _reportedRightButton) Then
            ArmSampleClock(1)
        End If
    End Sub

    Private Shared Function SaturatingAdd(current As Long, delta As Long) As Long
        Const Limit As Long = 1048576
        If delta > 0 AndAlso current > Limit - delta Then Return Limit
        If delta < 0 AndAlso current < -Limit - delta Then Return -Limit
        Return Math.Max(-Limit, Math.Min(Limit, current + delta))
    End Function

    Private Sub ArmSampleClock(delayPicoseconds As Long)
        delayPicoseconds = Math.Max(1L, delayPicoseconds)
        If _sampleRemainingPicoseconds <= 0 Then
            _sampleRemainingPicoseconds = delayPicoseconds
        Else
            _sampleRemainingPicoseconds = Math.Min(_sampleRemainingPicoseconds, delayPicoseconds)
        End If
    End Sub

    Private Function HasPendingReport() As Boolean
        Return _pendingX <> 0 OrElse _pendingY <> 0 OrElse
               _leftButton <> _reportedLeftButton OrElse
               _rightButton <> _reportedRightButton
    End Function

    Private Sub TryEmitPendingData()
        If Not _powered OrElse _uart Is Nothing Then Return
        If _identificationPending Then
            If _uart.QueueReceivedSerialByte(&H4D, MouseBaud, MouseDataBits, MouseParityMode, MouseStopBits) Then
                _identificationPending = False
                _identificationsSent += 1
            Else
                _packetsDeferred += 1
                ArmSampleClock(SampleIntervalPicoseconds)
                Return
            End If
        End If

        If Not HasPendingReport() Then Return
        Dim deltaX As Integer = CInt(Math.Max(-128L, Math.Min(127L, _pendingX)))
        Dim deltaY As Integer = CInt(Math.Max(-128L, Math.Min(127L, _pendingY)))
        Dim xByte As Integer = deltaX And &HFF
        Dim yByte As Integer = deltaY And &HFF
        Dim first As Byte = &H40
        If _leftButton Then first = CByte(first Or &H20)
        If _rightButton Then first = CByte(first Or &H10)
        first = CByte(first Or ((yByte And &HC0) >> 4) Or ((xByte And &HC0) >> 6))
        Dim packet As Byte() = {first, CByte(xByte And &H3F), CByte(yByte And &H3F)}
        If Not _uart.QueueReceivedSerialBytes(packet,
                                              MouseBaud,
                                              MouseDataBits,
                                              MouseParityMode,
                                              MouseStopBits) Then
            _packetsDeferred += 1
            ArmSampleClock(SampleIntervalPicoseconds)
            Return
        End If

        _pendingX -= deltaX
        _pendingY -= deltaY
        _reportedLeftButton = _leftButton
        _reportedRightButton = _rightButton
        _packetsSent += 1
        If HasPendingReport() Then ArmSampleClock(SampleIntervalPicoseconds)
    End Sub

    Public Sub AdvanceTime(elapsedPicoseconds As Long) Implements IClockedDevice.AdvanceTime
        If elapsedPicoseconds < 0 Then Throw New ArgumentOutOfRangeException(NameOf(elapsedPicoseconds))
        If _sampleRemainingPicoseconds <= 0 Then Return
        If elapsedPicoseconds < _sampleRemainingPicoseconds Then
            _sampleRemainingPicoseconds -= elapsedPicoseconds
            Return
        End If
        _sampleRemainingPicoseconds = 0
        TryEmitPendingData()
    End Sub

    Public Function PicosecondsUntilNextWakeEvent() As Long Implements IClockWakeSource.PicosecondsUntilNextWakeEvent
        If _sampleRemainingPicoseconds > 0 Then Return _sampleRemainingPicoseconds
        Return Long.MaxValue
    End Function

    Public Function DiagnosticText() As String
        Return $"Microsoft serial mouse: attached={If(_uart IsNot Nothing, "yes", "no")} powered={_powered} " &
               $"identify-pending={_identificationPending} break={_breakAsserted} buttons={If(_leftButton, "L", "-")}{If(_rightButton, "R", "-")} " &
               $"pending-delta={_pendingX}/{_pendingY} identification={_identificationsSent} packets={_packetsSent} " &
               $"deferred={_packetsDeferred} host-moves={_hostMovementEvents}"
    End Function
End Class

Public NotInheritable Class ParallelPortSpp
    Implements IPortDevice, IClockedDevice, IClockWakeSource, IResettableDevice

    Private ReadOnly _basePort As UInt16
    Private ReadOnly _irq As Integer
    Private ReadOnly _pic As Pic8259
    Private _dataLatch As Byte
    Private _controlLatch As Byte
    Private _busy As Boolean
    Private _peripheralBusy As Boolean
    Private _ackLow As Boolean
    Private _paperEnd As Boolean
    Private _selected As Boolean
    Private _errorOk As Boolean
    Private _handshakePhase As Integer
    Private _handshakeRemaining As Long
    Private _peripheral As IParallelPeripheral

    Private Const AckAssertDelayPicoseconds As Long = 5000000L
    Private Const AckPulsePicoseconds As Long = 5000000L

    Public Event TransferActivity()

    Public Sub New(basePort As UInt16, irq As Integer, pic As Pic8259)
        If pic Is Nothing Then Throw New ArgumentNullException(NameOf(pic))
        _basePort = basePort
        _irq = irq
        _pic = pic
        ResetDevice()
    End Sub

    Public Property Peripheral As IParallelPeripheral
        Get
            Return _peripheral
        End Get
        Set(value As IParallelPeripheral)
            _peripheral = value
            NotifyControlLines()
            RefreshPeripheralStatus()
        End Set
    End Property

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

    Public Function DiagnosticText() As String
        Dim status As Byte = ReadPort(CUShort(_basePort + 1US))
        Return $"LPT {_basePort:X4}h IRQ{_irq}: DATA={_dataLatch:X2} STATUS={status:X2} CONTROL={_controlLatch:X2} " &
               $"handshake={_handshakePhase} remaining-ps={_handshakeRemaining} busy={_busy} peripheral-busy={_peripheralBusy} ack-low={_ackLow}"
    End Function

    Public Function HandlesPort(port As UInt16) As Boolean Implements IPortDevice.HandlesPort
        Return port >= _basePort AndAlso port <= CUShort(_basePort + 2US)
    End Function

    Public Function ReadPort(port As UInt16) As Byte Implements IPortDevice.ReadPort
        Select Case CInt(port - _basePort)
            Case 0 : Return _dataLatch
            Case 1
                ' Sample the peripheral pins when the guest samples the adapter.
                ' ACK remains owned by this SPP handshake engine.  BUSY can be
                ' asserted either during the adapter's transfer handshake or by
                ' the peripheral; PAPER END, SELECT and /ERROR likewise come
                ' from the device at the cable end.
                RefreshPeripheralStatus()
                Dim status As Byte
                If Not (_busy OrElse _peripheralBusy) Then status = CByte(status Or &H80)
                If Not _ackLow Then status = CByte(status Or &H40)
                If _paperEnd Then status = CByte(status Or &H20)
                If _selected Then status = CByte(status Or &H10)
                If _errorOk Then status = CByte(status Or &H8)
                Return status
            Case 2 : Return CByte((_controlLatch And &H3F) Or &HC0)
            Case Else : Return &HFF
        End Select
    End Function

    Public Sub WritePort(port As UInt16, value As Byte) Implements IPortDevice.WritePort
        Select Case CInt(port - _basePort)
            Case 0 : _dataLatch = value
            Case 2
                Dim oldControl As Byte = _controlLatch
                _controlLatch = CByte(value And &H3F)
                NotifyControlLines()
                If (oldControl And 1) = 0 AndAlso (_controlLatch And 1) <> 0 Then BeginStrobe()
                If (_controlLatch And 4) = 0 Then
                    _handshakePhase = 0
                    _handshakeRemaining = 0
                    _busy = False
                    _ackLow = False
                End If
        End Select
    End Sub

    Private Sub BeginStrobe()
        RefreshPeripheralStatus()
        If _busy OrElse _peripheralBusy Then Return
        Dim accepted As Boolean = _peripheral Is Nothing OrElse _peripheral.AcceptByte(_dataLatch)
        RefreshPeripheralStatus()
        If Not accepted Then Return
        _busy = True
        _ackLow = False
        _handshakePhase = 1
        _handshakeRemaining = AckAssertDelayPicoseconds
        RaiseEvent TransferActivity()
    End Sub

    Private Sub NotifyControlLines()
        If _peripheral Is Nothing Then Return
        ' PC parallel-control bits 0, 1 and 3 are inverted between the register
        ' and connector.  These Boolean arguments describe asserted Centronics
        ' functions, not raw pin voltage: /SELECTIN and /AUTOFEED assert when
        ' their register bits are one, while /INIT asserts when bit 2 is zero.
        _peripheral.ControlLinesChanged((_controlLatch And 8) <> 0,
                                         (_controlLatch And 4) = 0,
                                         (_controlLatch And 2) <> 0)
        RefreshPeripheralStatus()
    End Sub

    Private Sub RefreshPeripheralStatus()
        Dim statusSource As IParallelStatusSource = TryCast(_peripheral, IParallelStatusSource)
        If statusSource Is Nothing Then Return
        _peripheralBusy = statusSource.Busy
        _paperEnd = statusSource.PaperEnd
        _selected = statusSource.Selected
        _errorOk = statusSource.ErrorOk
    End Sub

    Public Sub SetPeripheralStatus(paperEnd As Boolean, selected As Boolean, errorOk As Boolean)
        _paperEnd = paperEnd
        _selected = selected
        _errorOk = errorOk
    End Sub

    Public Sub AdvanceTime(elapsedPicoseconds As Long) Implements IClockedDevice.AdvanceTime
        If elapsedPicoseconds < 0 Then Throw New ArgumentOutOfRangeException(NameOf(elapsedPicoseconds))
        Dim remaining As Long = elapsedPicoseconds
        While _handshakePhase <> 0 AndAlso remaining >= _handshakeRemaining
            remaining -= _handshakeRemaining
            If _handshakePhase = 1 Then
                _ackLow = True
                _handshakePhase = 2
                _handshakeRemaining = AckPulsePicoseconds
            Else
                _ackLow = False
                _busy = False
                _handshakePhase = 0
                _handshakeRemaining = 0
                If (_controlLatch And &H10) <> 0 Then
                    _pic.SetIrqLine(_irq, True)
                    _pic.SetIrqLine(_irq, False)
                End If
            End If
        End While
        If _handshakePhase <> 0 Then _handshakeRemaining -= remaining
    End Sub

    Public Function PicosecondsUntilNextWakeEvent() As Long Implements IClockWakeSource.PicosecondsUntilNextWakeEvent
        If _handshakePhase <> 0 Then Return Math.Max(1L, _handshakeRemaining)
        Return Long.MaxValue
    End Function

    Public Sub ResetDevice() Implements IResettableDevice.ResetDevice
        _pic.SetIrqLine(_irq, False)
        _dataLatch = 0
        _controlLatch = &HC
        _busy = False
        _peripheralBusy = False
        _ackLow = False
        _paperEnd = False
        _selected = True
        _errorOk = True
        _handshakePhase = 0
        _handshakeRemaining = 0
        NotifyControlLines()
        RefreshPeripheralStatus()
    End Sub
End Class
