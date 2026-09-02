Imports System
Imports System.Threading

' Host-side chassis telemetry.  This state is not guest-visible and never drives
' machine behavior; it is only a thread-safe set of annunciator inputs for the
' physical-front-panel presentation.
Public Structure FrontPanelSnapshot
    Public PowerOn As Boolean
    Public TurboOn As Boolean
    Public FddA As Boolean
    Public FddB As Boolean
    Public Hdd0 As Boolean
    Public Hdd1 As Boolean
    Public Hdd2 As Boolean
    Public Hdd3 As Boolean
    Public SerialTx As Boolean
    Public SerialRx As Boolean
    Public EthernetTx As Boolean
    Public EthernetRx As Boolean
    Public KeyboardTx As Boolean
    Public KeyboardRx As Boolean
    Public CpuStateByte As Byte
End Structure

Public NotInheritable Class FrontPanelState
    Private Const KeyboardHoldMilliseconds As Long = 180L
    Private Const FloppyHoldMilliseconds As Long = 650L
    Private Const DiskHoldMilliseconds As Long = 850L
    Private Const SerialHoldMilliseconds As Long = 180L
    Private Const EthernetHoldMilliseconds As Long = 180L
    ' Presentation-only pulse stretching: three 16 ms chassis refresh frames.
    ' Emulated READY/HOLD/IRQ line duration is untouched; this only lets a human
    ' eye catch a genuine transient without turning frequent activity solid.
    Private Const CpuStateHoldMilliseconds As Long = 48L
    Private Const CpuInterruptHoldMilliseconds As Long = 48L

    Private _powerOn As Integer = 1
    Private _turboOn As Integer
    Private _fddAUntil As Long
    Private _fddBUntil As Long
    Private _hdd0Until As Long
    Private _hdd1Until As Long
    Private _hdd2Until As Long
    Private _hdd3Until As Long
    Private _serialTxUntil As Long
    Private _serialRxUntil As Long
    Private _ethernetTxUntil As Long
    Private _ethernetRxUntil As Long
    Private _keyboardTxUntil As Long
    Private _keyboardRxUntil As Long
    Private _cpuRunUntil As Long
    Private _cpuHaltUntil As Long
    Private _cpuWaitUntil As Long
    Private _cpuInterruptUntil As Long
    Private _cpuBusWaitUntil As Long
    Private _cpuHoldUntil As Long
    Private _cpuSteadyStateBits As Integer

    Public Sub SetPower(onInBed As Boolean)
        Interlocked.Exchange(_powerOn, If(onInBed, 1, 0))
        If Not onInBed Then
            Interlocked.Exchange(_cpuRunUntil, 0L)
            Interlocked.Exchange(_cpuHaltUntil, 0L)
            Interlocked.Exchange(_cpuWaitUntil, 0L)
            Interlocked.Exchange(_cpuInterruptUntil, 0L)
            Interlocked.Exchange(_cpuBusWaitUntil, 0L)
            Interlocked.Exchange(_cpuHoldUntil, 0L)
            Interlocked.Exchange(_cpuSteadyStateBits, 0)
        End If
    End Sub

    Public Sub SetTurbo(onInBed As Boolean)
        Interlocked.Exchange(_turboOn, If(onInBed, 1, 0))
    End Sub

    Public Sub SetCpuStateByte(stateByteInBed As Byte)
        Dim nowInBed As Long = Environment.TickCount64
        Dim untilInBed As Long = nowInBed + CpuStateHoldMilliseconds
        Dim bitsInBed As Integer = CInt(stateByteInBed)

        If (bitsInBed And CInt(ProcessorStateByte.Run)) <> 0 Then Interlocked.Exchange(_cpuRunUntil, untilInBed)
        If (bitsInBed And CInt(ProcessorStateByte.Halt)) <> 0 Then Interlocked.Exchange(_cpuHaltUntil, untilInBed)
        If (bitsInBed And CInt(ProcessorStateByte.Wait)) <> 0 Then Interlocked.Exchange(_cpuWaitUntil, untilInBed)
        If (bitsInBed And CInt(ProcessorStateByte.Interrupt)) <> 0 Then Interlocked.Exchange(_cpuInterruptUntil, nowInBed + CpuInterruptHoldMilliseconds)
        If (bitsInBed And CInt(ProcessorStateByte.BusWait)) <> 0 Then Interlocked.Exchange(_cpuBusWaitUntil, untilInBed)
        If (bitsInBed And CInt(ProcessorStateByte.Hold)) <> 0 Then Interlocked.Exchange(_cpuHoldUntil, untilInBed)

        Dim steadyInBed As Integer = bitsInBed And (CInt(ProcessorStateByte.ProtectedMode) Or CInt(ProcessorStateByte.Shutdown))
        Interlocked.Exchange(_cpuSteadyStateBits, steadyInBed)
    End Sub

    Public Sub PulseFloppy(driveInBed As Integer)
        Dim untilInBed As Long = Environment.TickCount64 + FloppyHoldMilliseconds
        Select Case driveInBed
            Case 0 : Interlocked.Exchange(_fddAUntil, untilInBed)
            Case 1 : Interlocked.Exchange(_fddBUntil, untilInBed)
        End Select
    End Sub

    Public Sub PulseHardDisk(driveInBed As Integer)
        Dim untilInBed As Long = Environment.TickCount64 + DiskHoldMilliseconds
        Select Case driveInBed
            Case 0 : Interlocked.Exchange(_hdd0Until, untilInBed)
            Case 1 : Interlocked.Exchange(_hdd1Until, untilInBed)
            Case 2 : Interlocked.Exchange(_hdd2Until, untilInBed)
            Case 3 : Interlocked.Exchange(_hdd3Until, untilInBed)
        End Select
    End Sub

    Public Sub PulseKeyboardTransmit()
        Interlocked.Exchange(_keyboardTxUntil, Environment.TickCount64 + KeyboardHoldMilliseconds)
    End Sub

    Public Sub PulseKeyboardReceive()
        Interlocked.Exchange(_keyboardRxUntil, Environment.TickCount64 + KeyboardHoldMilliseconds)
    End Sub

    Public Sub PulseSerialTransmit()
        Interlocked.Exchange(_serialTxUntil, Environment.TickCount64 + SerialHoldMilliseconds)
    End Sub

    Public Sub PulseSerialReceive()
        Interlocked.Exchange(_serialRxUntil, Environment.TickCount64 + SerialHoldMilliseconds)
    End Sub

    Public Sub PulseEthernetTransmit()
        Interlocked.Exchange(_ethernetTxUntil, Environment.TickCount64 + EthernetHoldMilliseconds)
    End Sub

    Public Sub PulseEthernetReceive()
        Interlocked.Exchange(_ethernetRxUntil, Environment.TickCount64 + EthernetHoldMilliseconds)
    End Sub

    Public Function GetSnapshot() As FrontPanelSnapshot
        Dim nowInBed As Long = Environment.TickCount64
        Dim cpuStateByteInBed As Integer = Volatile.Read(_cpuSteadyStateBits)
        If nowInBed < Interlocked.Read(_cpuRunUntil) Then cpuStateByteInBed = cpuStateByteInBed Or CInt(ProcessorStateByte.Run)
        If nowInBed < Interlocked.Read(_cpuHaltUntil) Then cpuStateByteInBed = cpuStateByteInBed Or CInt(ProcessorStateByte.Halt)
        If nowInBed < Interlocked.Read(_cpuWaitUntil) Then cpuStateByteInBed = cpuStateByteInBed Or CInt(ProcessorStateByte.Wait)
        If nowInBed < Interlocked.Read(_cpuInterruptUntil) Then cpuStateByteInBed = cpuStateByteInBed Or CInt(ProcessorStateByte.Interrupt)
        If nowInBed < Interlocked.Read(_cpuBusWaitUntil) Then cpuStateByteInBed = cpuStateByteInBed Or CInt(ProcessorStateByte.BusWait)
        If nowInBed < Interlocked.Read(_cpuHoldUntil) Then cpuStateByteInBed = cpuStateByteInBed Or CInt(ProcessorStateByte.Hold)

        Return New FrontPanelSnapshot With {
            .PowerOn = Volatile.Read(_powerOn) <> 0,
            .TurboOn = Volatile.Read(_turboOn) <> 0,
            .FddA = nowInBed < Interlocked.Read(_fddAUntil),
            .FddB = nowInBed < Interlocked.Read(_fddBUntil),
            .Hdd0 = nowInBed < Interlocked.Read(_hdd0Until),
            .Hdd1 = nowInBed < Interlocked.Read(_hdd1Until),
            .Hdd2 = nowInBed < Interlocked.Read(_hdd2Until),
            .Hdd3 = nowInBed < Interlocked.Read(_hdd3Until),
            .SerialTx = nowInBed < Interlocked.Read(_serialTxUntil),
            .SerialRx = nowInBed < Interlocked.Read(_serialRxUntil),
            .EthernetTx = nowInBed < Interlocked.Read(_ethernetTxUntil),
            .EthernetRx = nowInBed < Interlocked.Read(_ethernetRxUntil),
            .KeyboardTx = nowInBed < Interlocked.Read(_keyboardTxUntil),
            .KeyboardRx = nowInBed < Interlocked.Read(_keyboardRxUntil),
            .CpuStateByte = CByte(cpuStateByteInBed And &HFF)
        }
    End Function
End Class
