Imports System
Imports System.Collections.Generic

' Intel486 DX2/66, 5 V, 168-pin PGA.
'
' External bus: 33 MHz nominal.
' Internal integer/FPU/cache clock domain: 66 MHz nominal.
' Integrated FPU is part of this package; there is no subordinate 487 socketed beside it.
Public NotInheritable Class Intel486Dx2
    Implements IProcessorPackageRuntime, IProcessorPhysicalPackageProfile

    Public Const Intel486Dx266PackageId As String = "intel-80486dx2-66"
    Public Const NominalExternalClockHz As Long = 33000000L
    Public Const NominalCoreClockHz As Long = 66000000L
    Public Const PhysicalPinCount As Integer = 168
    Public Const NominalVcc As Double = 5.0R
    Public Const ComponentId As Byte = &H4
    Public Const RevisionIdSteppingB As Byte = &H33

    Private ReadOnly _workersInBed As DeterministicSiliconWorkerPool
    Private ReadOnly _fpuInBed As Intel486IntegratedFpu
    Private ReadOnly _mmuInBed As Intel486Mmu
    Private ReadOnly _executionInBed As Intel486ExecutionEngine
    Private ReadOnly _debugRegistersInBed(7) As UInteger

    ' Base architecture registers.
    Private _eaxInBed As UInteger
    Private _ebxInBed As UInteger
    Private _ecxInBed As UInteger
    Private _edxInBed As UInteger
    Private _esiInBed As UInteger
    Private _ediInBed As UInteger
    Private _ebpInBed As UInteger
    Private _espInBed As UInteger
    Private _eipInBed As UInteger
    Private _eflagsInBed As UInteger

    ' Segmentation, control registers, paging and TLB live in the on-die MMU.
    ' External pin state. RESET does not magically clear motherboard-driven inputs.
    Private _intrInBed As Boolean
    Private _nmiPendingInBed As Boolean
    Private _a20MaskAssertedInBed As Boolean
    Private _holdInBed As Boolean
    Private _addressHoldInBed As Boolean
    Private _busBackoffInBed As Boolean
    Private _upgradePresentInBed As Boolean
    Private _ignoreNumericErrorInBed As Boolean
    Private _flushAssertedInBed As Boolean

    Private _upgradePowerDownInBed As Boolean
    Private _bistLastResetInBed As Boolean
    Private _totalExternalClocksInBed As ULong
    Private _totalCoreClocksInBed As ULong

    Public Sub New(Optional maximumWorkersInBed As Integer = 0)
        _workersInBed = New DeterministicSiliconWorkerPool(maximumWorkersInBed)
        _fpuInBed = New Intel486IntegratedFpu()
        _mmuInBed = New Intel486Mmu()
        _executionInBed = New Intel486ExecutionEngine(Me, _mmuInBed)
        ApplyReset(False)
    End Sub

    Public ReadOnly Property PackageId As String Implements IProcessorPackageRuntime.PackageId, IProcessorPhysicalPackageProfile.PackageId
        Get
            Return Intel486Dx266PackageId
        End Get
    End Property

    Public ReadOnly Property DisplayName As String Implements IProcessorPhysicalPackageProfile.DisplayName
        Get
            Return "Intel486 DX2-66 (5 V, 168-pin PGA)"
        End Get
    End Property

    Public ReadOnly Property ArchitectureName As String Implements IProcessorPackageRuntime.ArchitectureName
        Get
            Return "Intel486"
        End Get
    End Property

    Public ReadOnly Property SocketFamily As String Implements IProcessorPhysicalPackageProfile.SocketFamily
        Get
            Return "Socket 3 compatible / 168-pin Intel486 DX pinout"
        End Get
    End Property

    Public ReadOnly Property PackagePresent As Boolean Implements IProcessorPackageRuntime.PackagePresent
        Get
            Return True
        End Get
    End Property

    Public ReadOnly Property HasIntegratedFloatingPointUnit As Boolean Implements IProcessorPackageRuntime.HasIntegratedFloatingPointUnit, IProcessorPhysicalPackageProfile.HasIntegratedFloatingPointUnit
        Get
            Return True
        End Get
    End Property

    Public ReadOnly Property NominalVoltage As Double Implements IProcessorPhysicalPackageProfile.NominalVoltage
        Get
            Return NominalVcc
        End Get
    End Property

    Public ReadOnly Property PinCount As Integer Implements IProcessorPhysicalPackageProfile.PinCount
        Get
            Return PhysicalPinCount
        End Get
    End Property

    Public ReadOnly Property ExternalBusClockHz As Long Implements IProcessorPackageRuntime.ExternalBusClockHz
        Get
            Return NominalExternalClockHz
        End Get
    End Property

    Private ReadOnly Property ProfileExternalClockHz As Long Implements IProcessorPhysicalPackageProfile.ExternalClockHz
        Get
            Return NominalExternalClockHz
        End Get
    End Property

    Public ReadOnly Property CoreClockHz As Long Implements IProcessorPackageRuntime.CoreClockHz
        Get
            Return NominalCoreClockHz
        End Get
    End Property

    Private ReadOnly Property ProfileCoreClockHz As Long Implements IProcessorPhysicalPackageProfile.CoreClockHz
        Get
            Return NominalCoreClockHz
        End Get
    End Property

    Public ReadOnly Property IntegratedFpu As Intel486IntegratedFpu
        Get
            Return _fpuInBed
        End Get
    End Property

    Public ReadOnly Property WorkerCount As Integer
        Get
            Return _workersInBed.MaximumWorkers
        End Get
    End Property

    Public ReadOnly Property EAX As UInteger
        Get
            Return _eaxInBed
        End Get
    End Property
    Public ReadOnly Property EBX As UInteger
        Get
            Return _ebxInBed
        End Get
    End Property
    Public ReadOnly Property ECX As UInteger
        Get
            Return _ecxInBed
        End Get
    End Property
    Public ReadOnly Property EDX As UInteger
        Get
            Return _edxInBed
        End Get
    End Property
    Public ReadOnly Property ESI As UInteger
        Get
            Return _esiInBed
        End Get
    End Property
    Public ReadOnly Property EDI As UInteger
        Get
            Return _ediInBed
        End Get
    End Property
    Public ReadOnly Property EBP As UInteger
        Get
            Return _ebpInBed
        End Get
    End Property
    Public ReadOnly Property ESP As UInteger
        Get
            Return _espInBed
        End Get
    End Property
    Public ReadOnly Property EIP As UInteger
        Get
            Return _eipInBed
        End Get
    End Property
    Public ReadOnly Property EFLAGS As UInteger
        Get
            Return _eflagsInBed
        End Get
    End Property
    Public ReadOnly Property CS As UInt16
        Get
            Return _mmuInBed.GetSegment(Intel486SegmentRegister.CS).Selector
        End Get
    End Property
    Public ReadOnly Property CSBase As UInteger
        Get
            Return _mmuInBed.GetSegment(Intel486SegmentRegister.CS).BaseAddress
        End Get
    End Property
    Public ReadOnly Property CSLimit As UInteger
        Get
            Return _mmuInBed.GetSegment(Intel486SegmentRegister.CS).Limit
        End Get
    End Property
    Public ReadOnly Property CR0 As UInteger
        Get
            Return _mmuInBed.CR0
        End Get
    End Property
    Public ReadOnly Property CR2 As UInteger
        Get
            Return _mmuInBed.CR2
        End Get
    End Property
    Public ReadOnly Property CR3 As UInteger
        Get
            Return _mmuInBed.CR3
        End Get
    End Property
    Public ReadOnly Property IDTRBase As UInteger
        Get
            Return _executionInBed.IDTR.BaseAddress
        End Get
    End Property
    Public ReadOnly Property IDTRLimit As UInt16
        Get
            Return _executionInBed.IDTR.Limit
        End Get
    End Property
    Public ReadOnly Property DR6 As UInteger
        Get
            Return _debugRegistersInBed(6)
        End Get
    End Property
    Public ReadOnly Property DR7 As UInteger
        Get
            Return _debugRegistersInBed(7)
        End Get
    End Property

    Public ReadOnly Property Mmu As Intel486Mmu
        Get
            Return _mmuInBed
        End Get
    End Property

    Public ReadOnly Property ExecutionEngine As Intel486ExecutionEngine
        Get
            Return _executionInBed
        End Get
    End Property

    Public ReadOnly Property UpgradePowerDown As Boolean
        Get
            Return _upgradePowerDownInBed
        End Get
    End Property

    Public ReadOnly Property BusOutputsFloated As Boolean
        Get
            Return _upgradePowerDownInBed OrElse _holdInBed OrElse _busBackoffInBed
        End Get
    End Property

    Public ReadOnly Property HoldAcknowledge As Boolean
        Get
            Return _holdInBed AndAlso Not _upgradePowerDownInBed
        End Get
    End Property

    Public ReadOnly Property TotalExternalClocks As ULong
        Get
            Return _totalExternalClocksInBed
        End Get
    End Property

    Public ReadOnly Property TotalCoreClocks As ULong
        Get
            Return _totalCoreClocksInBed
        End Get
    End Property

    Public Sub ColdReset() Implements IProcessorPackageRuntime.ColdReset
        ApplyReset(False)
    End Sub

    Public Sub ColdResetWithBist()
        ApplyReset(True)
    End Sub

    Public Sub WarmReset() Implements IProcessorPackageRuntime.WarmReset
        ApplyReset(False)
    End Sub

    Private Sub ApplyReset(bistInBed As Boolean)
        ' Undefined integer registers are represented deterministically as zero in VMB;
        ' they remain documented as architecturally undefined and no test may depend on
        ' this host-selected value. EDX is the one BIOS-visible component/revision ID.
        _eaxInBed = 0UI
        _ebxInBed = 0UI
        _ecxInBed = 0UI
        _edxInBed = (CUInt(ComponentId) << 8) Or CUInt(RevisionIdSteppingB)
        _esiInBed = 0UI
        _ediInBed = 0UI
        _ebpInBed = 0UI
        _espInBed = 0UI
        _eipInBed = &HFFF0UI
        _eflagsInBed = &H2UI

        _mmuInBed.Reset()
        _mmuInBed.SetA20Mask(_a20MaskAssertedInBed)
        For indexInBed As Integer = 0 To 7
            _debugRegistersInBed(indexInBed) = 0UI
        Next
        _debugRegistersInBed(6) = &HFFFF0FF0UI
        _executionInBed.ResetArchitecturalSystemState()

        _nmiPendingInBed = False
        _bistLastResetInBed = bistInBed
        _upgradePowerDownInBed = _upgradePresentInBed
        _totalExternalClocksInBed = 0UL
        _totalCoreClocksInBed = 0UL

        _fpuInBed.ProcessorReset(bistInBed)
    End Sub

    Public Sub SetIntr(assertedInBed As Boolean) Implements IProcessorPackageRuntime.SetIntr
        _intrInBed = assertedInBed
    End Sub

    Public Sub SignalNmiRisingEdge() Implements IProcessorPackageRuntime.SignalNmiRisingEdge
        _nmiPendingInBed = True
    End Sub

    Public Sub SetA20Mask(assertedLowInBed As Boolean) Implements IProcessorPackageRuntime.SetA20Mask
        _a20MaskAssertedInBed = assertedLowInBed
        _mmuInBed.SetA20Mask(assertedLowInBed)
    End Sub

    Public Sub SetHold(assertedInBed As Boolean) Implements IProcessorPackageRuntime.SetHold
        _holdInBed = assertedInBed
    End Sub

    Public Sub SetAddressHold(assertedInBed As Boolean)
        _addressHoldInBed = assertedInBed
    End Sub

    Public Sub SetBusBackoff(assertedInBed As Boolean)
        _busBackoffInBed = assertedInBed
    End Sub

    Public Sub SetUpgradePresent(assertedLowInBed As Boolean)
        _upgradePresentInBed = assertedLowInBed
        ' Intel specifies UP# as a RESET-edge decision for power-down.  Merely
        ' toggling it during normal execution does not retroactively change mode.
    End Sub

    Public Sub SetIgnoreNumericError(assertedLowInBed As Boolean)
        _ignoreNumericErrorInBed = assertedLowInBed
    End Sub

    Public Sub SetFlush(assertedLowInBed As Boolean)
        _flushAssertedInBed = assertedLowInBed
    End Sub

    Public Function GrantExternalClocks(externalClocksInBed As Long,
                                        fabricInBed As IProcessorLocalBusFabric) As ProcessorClockGrantResult Implements IProcessorPackageRuntime.GrantExternalClocks
        If externalClocksInBed < 0 Then Throw New ArgumentOutOfRangeException(NameOf(externalClocksInBed))
        If fabricInBed Is Nothing Then Throw New ArgumentNullException(NameOf(fabricInBed))
        If fabricInBed.ExternalClockHz <> NominalExternalClockHz Then
            Throw New InvalidOperationException("Intel486 DX2-66 requires the selected board clock to be 33 MHz in this package profile; fabric reports " & fabricInBed.ExternalClockHz.ToString() & " Hz.")
        End If

        Dim resultInBed As ProcessorClockGrantResult = ProcessorClockGrantResult.Empty()
        resultInBed.ExternalClocksGranted = externalClocksInBed
        If externalClocksInBed = 0 Then Return resultInBed

        ' CP03 contains no instruction engine yet.  The grant validates the physical
        ' 2:1 clock domain and board-time ownership without pretending to retire code.
        fabricInBed.AdvanceExternalClocks(externalClocksInBed)
        _totalExternalClocksInBed += CULng(externalClocksInBed)
        resultInBed.ExternalClocksConsumed = externalClocksInBed

        If _upgradePowerDownInBed OrElse _holdInBed OrElse _busBackoffInBed Then
            resultInBed.HoldingBus = _holdInBed
            Return resultInBed
        End If

        Dim coreClocksInBed As Long = checkedMultiplyByTwo(externalClocksInBed)
        _totalCoreClocksInBed += CULng(coreClocksInBed)
        resultInBed.CoreClocksConsumed = coreClocksInBed
        Return resultInBed
    End Function

    Private Shared Function checkedMultiplyByTwo(valueInBed As Long) As Long
        If valueInBed > Long.MaxValue \ 2L Then Throw New OverflowException("DX2 core-clock grant overflow.")
        Return valueInBed * 2L
    End Function

    Friend Function ReadGeneralRegister32(indexInBed As Integer) As UInteger
        Select Case indexInBed And 7
            Case 0 : Return _eaxInBed
            Case 1 : Return _ecxInBed
            Case 2 : Return _edxInBed
            Case 3 : Return _ebxInBed
            Case 4 : Return _espInBed
            Case 5 : Return _ebpInBed
            Case 6 : Return _esiInBed
            Case Else : Return _ediInBed
        End Select
    End Function

    Friend Sub WriteGeneralRegister32(indexInBed As Integer, valueInBed As UInteger)
        Select Case indexInBed And 7
            Case 0 : _eaxInBed = valueInBed
            Case 1 : _ecxInBed = valueInBed
            Case 2 : _edxInBed = valueInBed
            Case 3 : _ebxInBed = valueInBed
            Case 4 : _espInBed = valueInBed
            Case 5 : _ebpInBed = valueInBed
            Case 6 : _esiInBed = valueInBed
            Case 7 : _ediInBed = valueInBed
        End Select
    End Sub

    Friend Function ReadGeneralRegister16(indexInBed As Integer) As UInt16
        Return CUShort(ReadGeneralRegister32(indexInBed) And &HFFFFUI)
    End Function

    Friend Sub WriteGeneralRegister16(indexInBed As Integer, valueInBed As UInt16)
        Dim oldInBed As UInteger = ReadGeneralRegister32(indexInBed)
        WriteGeneralRegister32(indexInBed, (oldInBed And &HFFFF0000UI) Or CUInt(valueInBed))
    End Sub

    Friend Function ReadGeneralRegister8(indexInBed As Integer) As Byte
        If (indexInBed And 7) < 4 Then
            Return CByte(ReadGeneralRegister32(indexInBed And 3) And &HFFUI)
        End If
        Return CByte((ReadGeneralRegister32((indexInBed And 3)) >> 8) And &HFFUI)
    End Function

    Friend Sub WriteGeneralRegister8(indexInBed As Integer, valueInBed As Byte)
        Dim bankInBed As Integer = indexInBed And 3
        Dim oldInBed As UInteger = ReadGeneralRegister32(bankInBed)
        If (indexInBed And 7) < 4 Then
            WriteGeneralRegister32(bankInBed, (oldInBed And &HFFFFFF00UI) Or CUInt(valueInBed))
        Else
            WriteGeneralRegister32(bankInBed, (oldInBed And &HFFFF00FFUI) Or (CUInt(valueInBed) << 8))
        End If
    End Sub

    Friend Property MutableEip As UInteger
        Get
            Return _eipInBed
        End Get
        Set(valueInBed As UInteger)
            _eipInBed = valueInBed
        End Set
    End Property

    Friend Property MutableEflags As UInteger
        Get
            Return _eflagsInBed
        End Get
        Set(valueInBed As UInteger)
            ' Bit 1 is architecturally fixed to one.  Reserved high bits remain masked
            ' by the individual instruction paths rather than by this storage primitive.
            _eflagsInBed = valueInBed Or &H2UI
        End Set
    End Property

    Friend Function ReadDebugRegister(indexInBed As Integer) As UInteger
        If indexInBed < 0 OrElse indexInBed > 7 OrElse indexInBed = 4 OrElse indexInBed = 5 Then
            Throw New ArgumentOutOfRangeException(NameOf(indexInBed))
        End If
        Return _debugRegistersInBed(indexInBed)
    End Function

    Friend Sub WriteDebugRegister(indexInBed As Integer, valueInBed As UInteger)
        If indexInBed < 0 OrElse indexInBed > 7 OrElse indexInBed = 4 OrElse indexInBed = 5 Then
            Throw New ArgumentOutOfRangeException(NameOf(indexInBed))
        End If
        If indexInBed = 6 Then
            _debugRegistersInBed(6) = valueInBed Or &HFFFF0FF0UI
        ElseIf indexInBed = 7 Then
            _debugRegistersInBed(7) = valueInBed And &HFFFF2FFFUI
        Else
            _debugRegistersInBed(indexInBed) = valueInBed
        End If
    End Sub

    Friend Sub ClearPendingNmi()
        _nmiPendingInBed = False
    End Sub

    Friend ReadOnly Property PendingNmi As Boolean
        Get
            Return _nmiPendingInBed
        End Get
    End Property

    Friend ReadOnly Property IntrAsserted As Boolean
        Get
            Return _intrInBed
        End Get
    End Property

    Public Function DiagnosticText() As String
        Dim fpuStateInBed As String = If(_fpuInBed.ArchitecturalStateDefined, "defined", "undefined until BIST/FNINIT")
        Return DisplayName & Environment.NewLine &
               "  package                   : 168-pin PGA, 5 V" & Environment.NewLine &
               "  board/core clocks         : 33 MHz / 66 MHz (2:1)" & Environment.NewLine &
               "  reset EDX                  : " & _edxInBed.ToString("X8") & "h (component 04h, stepping B/33h)" & Environment.NewLine &
               "  reset fetch                : " & (CSBase + _eipInBed).ToString("X8") & "h" & Environment.NewLine &
               "  integrated FPU             : present; state " & fpuStateInBed & Environment.NewLine &
               "  host pure-compute workers  : " & WorkerCount.ToString() & Environment.NewLine &
               "  board-time ownership       : external clock grants only"
    End Function

    Public Shared Sub ValidatePackageShellOrThrow()
        Dim cpuInBed As New Intel486Dx2(1)
        If cpuInBed.EDX <> &H433UI Then Throw New InvalidOperationException("DX2 stepping-B RESET EDX mismatch.")
        If cpuInBed.CR0 <> &H60000010UI Then Throw New InvalidOperationException("DX2 RESET CR0 mismatch.")
        If cpuInBed.EFLAGS <> &H2UI Then Throw New InvalidOperationException("DX2 RESET EFLAGS mismatch.")
        If cpuInBed.CS <> &HF000US OrElse cpuInBed.CSBase <> &HFFFF0000UI OrElse cpuInBed.EIP <> &HFFF0UI Then
            Throw New InvalidOperationException("DX2 RESET fetch state mismatch.")
        End If
        If cpuInBed.CSBase + cpuInBed.EIP <> &HFFFFFFF0UI Then Throw New InvalidOperationException("DX2 RESET physical fetch address mismatch.")
        If cpuInBed.IDTRBase <> 0UI OrElse cpuInBed.IDTRLimit <> &H3FFUS Then Throw New InvalidOperationException("DX2 RESET IDTR mismatch.")
        If cpuInBed.DR6 <> &HFFFF0FF0UI OrElse cpuInBed.DR7 <> 0UI Then Throw New InvalidOperationException("DX2 RESET debug-register mismatch.")
        If cpuInBed.IntegratedFpu.ArchitecturalStateDefined Then Throw New InvalidOperationException("DX2 FPU was incorrectly initialized by ordinary RESET without BIST.")

        cpuInBed.ColdResetWithBist()
        If Not cpuInBed.IntegratedFpu.ArchitecturalStateDefined OrElse cpuInBed.IntegratedFpu.ControlWord <> &H37FUS Then
            Throw New InvalidOperationException("DX2 BIST FPU initialization mismatch.")
        End If

        Intel486Mmu.ValidateMmuOrThrow()
        DeterministicSiliconWorkerPool.ValidateDeterministicOrderingOrThrow()
    End Sub
End Class

' Signal-to-package-coordinate reference for the 168-pin PGA.  Coordinates are
' taken from Intel 241245-002 Figures 1.1/1.2 and the pin cross-reference table.
' This deliberately lists electrically meaningful signals; VCC/VSS/NC positions
' remain board/package-layout metadata rather than executable pseudo-pins.
Public NotInheritable Class Intel486Dx2SignalPinMap
    Private Shared ReadOnly _pinsInBed As Dictionary(Of String, String) = BuildPins()

    Private Sub New()
    End Sub

    Private Shared Function BuildPins() As Dictionary(Of String, String)
        Dim pInBed As New Dictionary(Of String, String)(StringComparer.OrdinalIgnoreCase)
        Dim addressCoordinatesInBed() As String = {"Q14","R15","S16","Q12","S15","Q13","R13","Q11","S13","R12","S7","Q10","S5","R7","Q9","Q3","R5","Q4","Q8","Q5","Q7","S3","Q6","R2","S2","S1","R1","P2","P3","Q1"}
        For nInBed As Integer = 2 To 31
            pInBed("A" & nInBed.ToString()) = addressCoordinatesInBed(nInBed - 2)
        Next
        Dim dataCoordinatesInBed() As String = {"P1","N2","N1","H2","M3","J2","L2","L3","F2","D1","E3","C1","G3","D2","K3","F3","J3","D3","C2","B1","A1","B2","A2","A4","A6","B6","C7","C6","C8","A8","C9","B8"}
        For nInBed As Integer = 0 To 31
            pInBed("D" & nInBed.ToString()) = dataCoordinatesInBed(nInBed)
        Next
        pInBed("DP0") = "N3" : pInBed("DP1") = "F1" : pInBed("DP2") = "H3" : pInBed("DP3") = "A5"
        pInBed("A20M#") = "D15" : pInBed("ADS#") = "S17" : pInBed("AHOLD") = "A17"
        pInBed("BE0#") = "K15" : pInBed("BE1#") = "J16" : pInBed("BE2#") = "J15" : pInBed("BE3#") = "F17"
        pInBed("BLAST#") = "R16" : pInBed("BOFF#") = "D17" : pInBed("BRDY#") = "H15" : pInBed("BREQ") = "Q15"
        pInBed("BS8#") = "D16" : pInBed("BS16#") = "C17" : pInBed("CLK") = "C3" : pInBed("D/C#") = "M15"
        pInBed("EADS#") = "B17" : pInBed("FERR#") = "C14" : pInBed("FLUSH#") = "C15" : pInBed("HLDA") = "P15"
        pInBed("HOLD") = "E15" : pInBed("IGNNE#") = "A15" : pInBed("INTR") = "A16" : pInBed("KEN#") = "F15"
        pInBed("LOCK#") = "N15" : pInBed("M/IO#") = "N16" : pInBed("NMI") = "B15" : pInBed("PCD") = "J17"
        pInBed("PCHK#") = "Q17" : pInBed("PWT") = "L15" : pInBed("PLOCK#") = "Q16" : pInBed("RDY#") = "F16"
        pInBed("RESET") = "C16" : pInBed("UP#") = "C11" : pInBed("W/R#") = "N17"
        pInBed("TCK") = "A3" : pInBed("TDI") = "A14" : pInBed("TDO") = "B16" : pInBed("TMS") = "B14"
        Return pInBed
    End Function

    Public Shared Function Location(signalNameInBed As String) As String
        If String.IsNullOrWhiteSpace(signalNameInBed) Then Throw New ArgumentException("Signal name is required.", NameOf(signalNameInBed))
        Dim locationInBed As String = Nothing
        If Not _pinsInBed.TryGetValue(signalNameInBed, locationInBed) Then
            Throw New KeyNotFoundException("No Intel486 DX2 signal pin named '" & signalNameInBed & "' is present in the executable signal map.")
        End If
        Return locationInBed
    End Function

    Public Shared Function Snapshot() As Dictionary(Of String, String)
        Return New Dictionary(Of String, String)(_pinsInBed, StringComparer.OrdinalIgnoreCase)
    End Function
End Class
