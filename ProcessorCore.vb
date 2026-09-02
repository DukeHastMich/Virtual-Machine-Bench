Imports System
Imports System.Diagnostics

' A deliberately substrate-oriented 80286 real-address-mode core.  Physical
' memory is the machine's existing 16 x 64K RAM array; no private shadow RAM is
' used.  Protected-mode state is represented, but the first milestone is a
' correct real-mode execution substrate capable of running BIOS/DOS code.
<Flags>
Public Enum ProcessorStateByte As Byte
    None = 0
    Run = &H1
    Halt = &H2
    Wait = &H4
    Interrupt = &H8
    BusWait = &H10
    Hold = &H20
    ProtectedMode = &H40
    Shutdown = &H80
End Enum

Partial Public Class Processor286
    ' CROMWELL CPU HOT PATH DYNO BRICK 4
    ' Host-only sampled profiler.  One instruction in 1024 pays Stopwatch cost;
    ' guest architectural state and guest T-state accounting are untouched.
    Private Const HotPathSampleMaskInBed As ULong = 1023UL
    Private _hotPathInstructionCounterInBed As ULong
    Private _hotPathSampleCountInBed As ULong
    Private _hotPathHostHandledSamplesInBed As ULong
    Private _hotPathSampleActiveInBed As Boolean
    Private _hotPathProbeTicksInBed As Long
    Private _hotPathCaptureTicksInBed As Long
    Private _hotPathExecuteTicksInBed As Long
    Private _hotPathTimingTicksInBed As Long
    Private _hotPathLengthScanTicksInBed As Long
    Private _hotPathRunnerTicksInBed As Long
    Private _hotPathRunnerInstructionsInBed As ULong
    Private _hotPathRunnerCallsInBed As ULong
    Private _hotPathProfilerGenerationInBed As ULong
    Private Const DiagnosticExecutionSampleCapacityInBed As Integer = 64
    Private ReadOnly _diagnosticExecutionSampleCsInBed(DiagnosticExecutionSampleCapacityInBed - 1) As UInt16
    Private ReadOnly _diagnosticExecutionSampleIpInBed(DiagnosticExecutionSampleCapacityInBed - 1) As UInt16
    Private _diagnosticExecutionSampleWriteInBed As Integer
    Private _diagnosticExecutionSampleCountInBed As Integer
    ' Bounded, no-bus execution evidence retained across 286 shutdown resets.
    ' The ordinary CS:IP sampler identifies a hot loop; this companion ring
    ' preserves enough architectural context to explain it without keeping the
    ' multi-megabyte forensic stream open for an entire Windows installation.
    Private Const DiagnosticDetailedExecutionCapacityInBed As Integer = 192
    Private ReadOnly _diagnosticDetailedExecutionInBed As New System.Collections.Generic.Queue(Of String)()
    Private _diagnosticDetailedExecutionSequenceInBed As ULong

    ' CROMWELL CPU DATAPATH DYNO BRICK 6B
    ' Detailed execution sampling is intentionally offset from the Brick 4 sample
    ' cadence so its nested probes do not contaminate the main hot-path buckets.
    Private _dataPathSampleActiveInBed As Boolean
    Private _dataPathSampleCountInBed As ULong
    Private _dataPathOpcodeFetchTicksInBed As Long
    Private _dataPathExecuteBodyTicksInBed As Long
    Private _dataPathSegmentTicksInBed As Long
    Private _dataPathMemoryApiTicksInBed As Long
    Private _dataPathBusProbeTicksInBed As Long
    Private _dataPathStackTicksInBed As Long
    Private _dataPathSegmentCallsInBed As ULong
    Private _dataPathReadByteCallsInBed As ULong
    Private _dataPathWriteByteCallsInBed As ULong
    Private _dataPathBusProbeCallsInBed As ULong
    Private _dataPathBusHitsInBed As ULong
    Private _dataPathStackOpsInBed As ULong
    Private _dataPathCodeBytesInBed As ULong
    Private _dataPathOpcodeBytesInBed As ULong
    Private _dataPathWordApiTicksInBed As Long
    Private _dataPathWordReadCallsInBed As ULong
    Private _dataPathWordWriteCallsInBed As ULong
    Private _dataPathWordFastReadsInBed As ULong
    Private _dataPathWordFastWritesInBed As ULong
    Private _dataPathLocalCodeByteFetchesInBed As ULong
    Private _dataPathLocalCodeWordFetchesInBed As ULong
    Private _dataPathCodeFetchFallbacksInBed As ULong

    Public Sub ResetHotPathProfiler()
        ' Host-only diagnostic generation. A physical CPU RESET may occur while
        ' RunForTStates is still on the stack; invalidate that in-flight runner
        ' sample instead of allowing unsigned counter subtraction across reset.
        If _hotPathProfilerGenerationInBed = ULong.MaxValue Then
            _hotPathProfilerGenerationInBed = 0UL
        Else
            _hotPathProfilerGenerationInBed += 1UL
        End If
        _hotPathInstructionCounterInBed = 0UL
        _hotPathSampleCountInBed = 0UL
        _hotPathHostHandledSamplesInBed = 0UL
        _hotPathSampleActiveInBed = False
        _hotPathProbeTicksInBed = 0
        _hotPathCaptureTicksInBed = 0
        _hotPathExecuteTicksInBed = 0
        _hotPathTimingTicksInBed = 0
        _hotPathLengthScanTicksInBed = 0
        _hotPathRunnerTicksInBed = 0
        _hotPathRunnerInstructionsInBed = 0UL
        _hotPathRunnerCallsInBed = 0UL
        _diagnosticExecutionSampleWriteInBed = 0
        _diagnosticExecutionSampleCountInBed = 0
        _dataPathSampleActiveInBed = False
        _dataPathSampleCountInBed = 0UL
        _dataPathOpcodeFetchTicksInBed = 0
        _dataPathExecuteBodyTicksInBed = 0
        _dataPathSegmentTicksInBed = 0
        _dataPathMemoryApiTicksInBed = 0
        _dataPathBusProbeTicksInBed = 0
        _dataPathStackTicksInBed = 0
        _dataPathSegmentCallsInBed = 0UL
        _dataPathReadByteCallsInBed = 0UL
        _dataPathWriteByteCallsInBed = 0UL
        _dataPathBusProbeCallsInBed = 0UL
        _dataPathBusHitsInBed = 0UL
        _dataPathStackOpsInBed = 0UL
        _dataPathCodeBytesInBed = 0UL
        _dataPathOpcodeBytesInBed = 0UL
        _dataPathWordApiTicksInBed = 0
        _dataPathWordReadCallsInBed = 0UL
        _dataPathWordWriteCallsInBed = 0UL
        _dataPathWordFastReadsInBed = 0UL
        _dataPathWordFastWritesInBed = 0UL
        _dataPathLocalCodeByteFetchesInBed = 0UL
        _dataPathLocalCodeWordFetchesInBed = 0UL
        _dataPathCodeFetchFallbacksInBed = 0UL
    End Sub

    Private Shared Function HotPathPercentInBed(partTicksInBed As Long, totalTicksInBed As Long) As Double
        If partTicksInBed <= 0 OrElse totalTicksInBed <= 0 Then Return 0.0
        Return CDbl(partTicksInBed) * 100.0 / CDbl(totalTicksInBed)
    End Function

    Public Function HotPathDiagnosticText() As String
        Dim sampledTicksInBed As Long =
            _hotPathProbeTicksInBed + _hotPathCaptureTicksInBed +
            _hotPathExecuteTicksInBed + _hotPathTimingTicksInBed

        If _hotPathSampleCountInBed = 0UL OrElse sampledTicksInBed <= 0 Then
            Return "CPU hot-path profiler     : warming up (1 sample / 1,024 instructions)"
        End If

        Dim nanosecondsPerTickInBed As Double =
            1000000000.0R / CDbl(System.Diagnostics.Stopwatch.Frequency)
        Dim sampledNsPerInstructionInBed As Double =
            CDbl(sampledTicksInBed) * nanosecondsPerTickInBed / CDbl(_hotPathSampleCountInBed)

        Dim runnerNsPerInstructionInBed As Double = 0.0
        If _hotPathRunnerInstructionsInBed > 0UL AndAlso _hotPathRunnerTicksInBed > 0 Then
            runnerNsPerInstructionInBed =
                CDbl(_hotPathRunnerTicksInBed) * nanosecondsPerTickInBed /
                CDbl(_hotPathRunnerInstructionsInBed)
        End If

        Dim outsidePstepPercentInBed As Double = 0.0
        If runnerNsPerInstructionInBed > 0.0 Then
            outsidePstepPercentInBed =
                Math.Max(0.0, Math.Min(100.0,
                    (runnerNsPerInstructionInBed - sampledNsPerInstructionInBed) * 100.0 /
                    runnerNsPerInstructionInBed))
        End If

        Dim lengthOfTimingPercentInBed As Double =
            HotPathPercentInBed(_hotPathLengthScanTicksInBed, _hotPathTimingTicksInBed)

        Dim sbInBed As New System.Text.StringBuilder()
        sbInBed.Append("CPU hot samples         : ").Append(_hotPathSampleCountInBed.ToString("N0"))
        sbInBed.Append(" / instructions ").Append(_hotPathInstructionCounterInBed.ToString("N0")).AppendLine()
        sbInBed.Append("Host-vector samples     : ").Append(_hotPathHostHandledSamplesInBed.ToString("N0")).AppendLine()
        sbInBed.Append("Host-fw gate / handler  : ").Append(HotPathPercentInBed(_hotPathProbeTicksInBed, sampledTicksInBed).ToString("0.0")).Append(" %").AppendLine()
        sbInBed.Append("Fused timing setup      : ").Append(HotPathPercentInBed(_hotPathCaptureTicksInBed, sampledTicksInBed).ToString("0.0")).Append(" %").AppendLine()
        sbInBed.Append("Execute + guest accesses: ").Append(HotPathPercentInBed(_hotPathExecuteTicksInBed, sampledTicksInBed).ToString("0.0")).Append(" %").AppendLine()
        If _dataPathSampleCountInBed > 0UL Then
            Dim dataPathPhaseTicksInBed As Long =
                _dataPathOpcodeFetchTicksInBed + _dataPathExecuteBodyTicksInBed
            Dim dataPathBytesPerInstructionInBed As Double =
                CDbl(_dataPathCodeBytesInBed) / CDbl(_dataPathSampleCountInBed)
            Dim dataPathOpcodeBytesPerInstructionInBed As Double =
                CDbl(_dataPathOpcodeBytesInBed) / CDbl(_dataPathSampleCountInBed)
            sbInBed.Append("  datapath samples      : ").Append(_dataPathSampleCountInBed.ToString("N0")).Append(" (1 / 4,096, offset)").AppendLine()
            sbInBed.Append("  opcode/prefix fetch   : ").Append(HotPathPercentInBed(_dataPathOpcodeFetchTicksInBed, dataPathPhaseTicksInBed).ToString("0.0")).Append(" % datapath").AppendLine()
            sbInBed.Append("  Execute(op) body      : ").Append(HotPathPercentInBed(_dataPathExecuteBodyTicksInBed, dataPathPhaseTicksInBed).ToString("0.0")).Append(" % datapath").AppendLine()
            sbInBed.Append("  segment translation  : ").Append(HotPathPercentInBed(_dataPathSegmentTicksInBed, dataPathPhaseTicksInBed).ToString("0.0")).Append(" % inclusive / calls ").Append(_dataPathSegmentCallsInBed.ToString("N0")).AppendLine()
            sbInBed.Append("    hidden base route   : cached 286 ES/CS/SS/DS base").AppendLine()
            sbInBed.Append("  byte memory API       : ").Append(HotPathPercentInBed(_dataPathMemoryApiTicksInBed, dataPathPhaseTicksInBed).ToString("0.0")).Append(" % inclusive / R ").Append(_dataPathReadByteCallsInBed.ToString("N0")).Append(" W ").Append(_dataPathWriteByteCallsInBed.ToString("N0")).AppendLine()
            sbInBed.Append("  16-bit memory API     : ").Append(HotPathPercentInBed(_dataPathWordApiTicksInBed, dataPathPhaseTicksInBed).ToString("0.0")).Append(" % inclusive / R ").Append(_dataPathWordReadCallsInBed.ToString("N0")).Append(" W ").Append(_dataPathWordWriteCallsInBed.ToString("N0")).AppendLine()
            sbInBed.Append("    local word cycles   : R ").Append(_dataPathWordFastReadsInBed.ToString("N0")).Append(" W ").Append(_dataPathWordFastWritesInBed.ToString("N0")).AppendLine()
            sbInBed.Append("    full MMIO probe     : ").Append(HotPathPercentInBed(_dataPathBusProbeTicksInBed, dataPathPhaseTicksInBed).ToString("0.0")).Append(" % / calls ").Append(_dataPathBusProbeCallsInBed.ToString("N0")).Append(" hits ").Append(_dataPathBusHitsInBed.ToString("N0")).AppendLine()
            sbInBed.Append("    PCB page gate       : compiled 4 KiB chip-select route").AppendLine()
            sbInBed.Append("  stack Push/Pop        : ").Append(HotPathPercentInBed(_dataPathStackTicksInBed, dataPathPhaseTicksInBed).ToString("0.0")).Append(" % inclusive / ops ").Append(_dataPathStackOpsInBed.ToString("N0")).AppendLine()
            sbInBed.Append("  code bytes/instruction: ").Append(dataPathBytesPerInstructionInBed.ToString("0.00")).Append(" total / ").Append(dataPathOpcodeBytesPerInstructionInBed.ToString("0.00")).Append(" opcode+prefix").AppendLine()
            sbInBed.Append("  local code fetches    : byte ").Append(_dataPathLocalCodeByteFetchesInBed.ToString("N0")).Append(" word ").Append(_dataPathLocalCodeWordFetchesInBed.ToString("N0")).Append(" fallback ").Append(_dataPathCodeFetchFallbacksInBed.ToString("N0")).AppendLine()
        Else
            sbInBed.Append("  datapath profiler     : warming up (1 sample / 4,096 instructions)").AppendLine()
        End If
        sbInBed.Append("Timing commit total     : ").Append(HotPathPercentInBed(_hotPathTimingTicksInBed, sampledTicksInBed).ToString("0.0")).Append(" %").AppendLine()
        sbInBed.Append("  common timing route  : compiled 256-entry ledger").AppendLine()
        sbInBed.Append("  next-length rescan    : ").Append(HotPathPercentInBed(_hotPathLengthScanTicksInBed, sampledTicksInBed).ToString("0.0")).Append(" % total / ")
        sbInBed.Append(lengthOfTimingPercentInBed.ToString("0.0")).Append(" % timing").AppendLine()
        sbInBed.Append("Sampled pStep host cost : ").Append(sampledNsPerInstructionInBed.ToString("0.0")).Append(" ns/instruction").AppendLine()
        sbInBed.Append("Runner host cost / instr: ").Append(runnerNsPerInstructionInBed.ToString("0.0")).Append(" ns/instruction").AppendLine()
        sbInBed.Append("Profiler note           : sampled timings; counts exact, % approximate")
        Return sbInBed.ToString()
    End Function

    Public Function DiagnosticExecutionHistoryText() As String
        Dim sbInBed As New System.Text.StringBuilder()
        sbInBed.Append("Recent sampled execution : 1 location / 1,024 instructions").AppendLine()
        If _diagnosticExecutionSampleCountInBed = 0 Then
            sbInBed.Append("  <warming up>")
            Return sbInBed.ToString()
        End If

        Dim oldestInBed As Integer =
            (_diagnosticExecutionSampleWriteInBed - _diagnosticExecutionSampleCountInBed +
             DiagnosticExecutionSampleCapacityInBed) Mod DiagnosticExecutionSampleCapacityInBed
        For sampleInBed As Integer = 0 To _diagnosticExecutionSampleCountInBed - 1
            Dim indexInBed As Integer =
                (oldestInBed + sampleInBed) Mod DiagnosticExecutionSampleCapacityInBed
            sbInBed.Append("  ").Append(_diagnosticExecutionSampleCsInBed(indexInBed).ToString("X4"))
            sbInBed.Append(":").Append(_diagnosticExecutionSampleIpInBed(indexInBed).ToString("X4"))
            If ((sampleInBed + 1) Mod 8) = 0 Then sbInBed.AppendLine()
        Next
        sbInBed.AppendLine().AppendLine()
        sbInBed.Append(DiagnosticDetailedExecutionHistoryText())
        Return sbInBed.ToString().TrimEnd()
    End Function

    Public Function DiagnosticDetailedExecutionHistoryText() As String
        Dim sbInBed As New System.Text.StringBuilder()
        sbInBed.AppendLine("Bounded detailed execution events (fault/reset triggers; retained across AT shutdown resets)")
        sbInBed.AppendLine("Direct DRAM peeks only: event capture adds no guest bus cycles.")
        If _diagnosticDetailedExecutionInBed.Count = 0 Then
            sbInBed.Append("  <warming up>")
            Return sbInBed.ToString()
        End If
        For Each lineInBed As String In _diagnosticDetailedExecutionInBed
            sbInBed.AppendLine(lineInBed)
        Next
        Return sbInBed.ToString().TrimEnd()
    End Function

    Private Function DiagnosticTryPeekRamByteInBed(addressInBed As UInteger,
                                                     ByRef valueInBed As Byte) As Boolean
        If _memoryControllerInBed Is Nothing Then Return False
        Dim normalizedInBed As UInteger =
            _memoryControllerInBed.NormalizePhysicalAddress(addressInBed)
        If normalizedInBed < &H100000UI Then
            valueInBed = _memoryControllerInBed.LowMemoryInBed(CInt(normalizedInBed))
            Return True
        End If
        Dim extendedOffsetInBed As UInteger = normalizedInBed - &H100000UI
        If extendedOffsetInBed >= CUInt(_memoryControllerInBed.ExtendedMemoryInBed.Length) Then Return False
        valueInBed = _memoryControllerInBed.ExtendedMemoryInBed(CInt(extendedOffsetInBed))
        Return True
    End Function

    Private Function DiagnosticNoBusBytesInBed(baseInBed As UInteger,
                                                 offsetInBed As UInt16,
                                                 limitInBed As UInt16,
                                                 countInBed As Integer) As String
        Dim resultInBed As New System.Text.StringBuilder()
        For indexInBed As Integer = 0 To Math.Max(0, Math.Min(16, countInBed)) - 1
            Dim currentOffsetInBed As UInteger = CUInt(offsetInBed) + CUInt(indexInBed)
            If currentOffsetInBed > CUInt(limitInBed) Then Exit For
            If resultInBed.Length > 0 Then resultInBed.Append(" "c)
            Dim valueInBed As Byte
            If DiagnosticTryPeekRamByteInBed((baseInBed + currentOffsetInBed) And &HFFFFFFUI, valueInBed) Then
                resultInBed.Append(valueInBed.ToString("X2"))
            Else
                resultInBed.Append("??")
            End If
        Next
        Return resultInBed.ToString()
    End Function

    Private Function DiagnosticNoBusStackInBed() As String
        Dim resultInBed As New System.Text.StringBuilder()
        For wordIndexInBed As Integer = 0 To 7
            If wordIndexInBed > 0 Then resultInBed.Append(" "c)
            Dim byteOffsetInBed As UInteger = CUInt(SP) + CUInt(wordIndexInBed * 2)
            If byteOffsetInBed + 1UI > CUInt(_segmentLimits(2)) Then
                resultInBed.Append("????")
                Continue For
            End If
            Dim lowInBed As Byte
            Dim highInBed As Byte
            If DiagnosticTryPeekRamByteInBed((_segmentBases(2) + byteOffsetInBed) And &HFFFFFFUI, lowInBed) AndAlso
               DiagnosticTryPeekRamByteInBed((_segmentBases(2) + byteOffsetInBed + 1UI) And &HFFFFFFUI, highInBed) Then
                resultInBed.Append(CUShort(CUInt(lowInBed) Or (CUInt(highInBed) << 8)).ToString("X4"))
            Else
                resultInBed.Append("????")
            End If
        Next
        Return resultInBed.ToString()
    End Function

    Private Sub CaptureDiagnosticDetailedExecutionInBed(reasonInBed As String)
        _diagnosticDetailedExecutionSequenceInBed += 1UL
        Dim lineInBed As String =
            "#X" & _diagnosticDetailedExecutionSequenceInBed.ToString("000000000") &
            " " & reasonInBed &
            " at=" & CS.ToString("X4") & ":" & IP.ToString("X4") &
            " phys=" & ((_segmentBases(1) + CUInt(IP)) And &HFFFFFFUI).ToString("X6") &
            " bytes=[" & DiagnosticNoBusBytesInBed(_segmentBases(1), IP, _segmentLimits(1), 16) & "]" &
            " AX=" & AX.ToString("X4") & " BX=" & BX.ToString("X4") &
            " CX=" & CX.ToString("X4") & " DX=" & DX.ToString("X4") &
            " SI=" & SI.ToString("X4") & " DI=" & DI.ToString("X4") &
            " BP=" & BP.ToString("X4") &
            " ES=" & ES.ToString("X4") & " SS=" & SS.ToString("X4") &
            " DS=" & DS.ToString("X4") & " SP=" & SP.ToString("X4") &
            " FL=" & Flags.ToString("X4") & " MSW=" & MachineStatusWord.ToString("X4") &
            " CPL=" & CurrentPrivilegeLevelInBed().ToString() &
            " CSBASE=" & _segmentBases(1).ToString("X6") &
            " CSLIM=" & _segmentLimits(1).ToString("X4") &
            " CSACC=" & _segmentAccess(1).ToString("X2") &
            " SSBASE=" & _segmentBases(2).ToString("X6") &
            " DSBASE=" & _segmentBases(3).ToString("X6") &
            " LDTR=" & LocalDescriptorTableRegister.ToString("X4") &
            " TR=" & TaskRegister.ToString("X4") &
            " GDTR=" & GdtrBase.ToString("X6") & ":" & GdtrLimit.ToString("X4") &
            " IDTR=" & IdtrBase.ToString("X6") & ":" & IdtrLimit.ToString("X4") &
            " STACK=[" & DiagnosticNoBusStackInBed() & "]"
        While _diagnosticDetailedExecutionInBed.Count >= DiagnosticDetailedExecutionCapacityInBed
            _diagnosticDetailedExecutionInBed.Dequeue()
        End While
        _diagnosticDetailedExecutionInBed.Enqueue(lineInBed)
    End Sub

    Private Enum ProcessorHaltSourceInBed As Byte
        None = 0
        HltInstruction = 1
        FaultStop = 2
        ExternalStop = 3
    End Enum

    ' CROMWELL BIOS KEYBOARD RING FORENSIC TRACE
    ' Host-only diagnostic instrumentation. No guest-visible state or timing.
    Private Const DiagnosticBiosKeyboardTraceCapacity As Integer = 768
    Private ReadOnly _diagnosticBiosKeyboardTrace As New System.Collections.Generic.Queue(Of String)()
    Private _diagnosticBiosKeyboardTraceSequence As ULong
    Private _diagnosticBiosKeyboardTraceEnabled As Boolean
    ' CROMWELL IMPORTANT INTN FORENSIC TRACE
    ' Host-only software interrupt instrumentation.  Disabled means the hot path
    ' pays only one Boolean test in SoftwareInterrupt; no guest state/timing is
    ' synthesized by this diagnostic.
    ' CROMWELL QB EXEC FORENSICS BRICK 1
    ' Arms automatically on DOS INT 21h/AH=4Bh EXEC, then records only the
    ' loader-facing evidence we care about: INT 13h entry/return plus a rolling
    ' instruction history.  It does not alter guest timing, flags, or hardware.
    ' CROMWELL QB RELOCATION WRITE WATCH BRICK 2
    ' Diagnostic-only watch for the DOS loader loop implicated by the QB.EXE
    ' stall.  No guest-visible reads are issued: old bytes and relocation-table
    ' bytes are peeked directly from NEAT low DRAM so the watch cannot create
    ' extra bus cycles or device side effects.
    Private Const DiagnosticQbWriteWatchCapacityInBed As Integer = 1024
    Private Const DiagnosticQbObservedLoaderCsInBed As UInt16 = &H2C3US
    Private Const DiagnosticQbObservedLoaderIpStartInBed As UInt16 = &H9E40US
    Private Const DiagnosticQbObservedLoaderIpEndInBed As UInt16 = &H9E60US
    Private Const DiagnosticQbObservedLoaderPhysicalStartInBed As UInteger = &HCA70UI
    Private Const DiagnosticQbObservedLoaderPhysicalEndInBed As UInteger = &HCA90UI
    Private ReadOnly _diagnosticQbWriteWatchInBed As New System.Collections.Generic.Queue(Of String)()
    Private _diagnosticQbWriteWatchSequenceInBed As ULong
    Private _diagnosticQbWriteWatchTotalInBed As ULong

    Private Const DiagnosticQbInstructionCapacityInBed As Integer = 256
    Private Const DiagnosticQbEventCapacityInBed As Integer = 512

    Private Structure DiagnosticQbInt13PendingInBed
        Public Sequence As ULong
        Public ReturnCs As UInt16
        Public ReturnIp As UInt16
        Public ReturnSs As UInt16
        Public ReturnSp As UInt16
        Public FunctionAh As Byte
        Public RequestedCount As Byte
        Public Cylinder As Integer
        Public Head As Byte
        Public Sector As Byte
        Public Drive As Byte
        Public BufferEs As UInt16
        Public BufferBx As UInt16
        Public PhysicalStart As ULong
        Public PhysicalEnd As ULong
    End Structure

    Private Structure DiagnosticQbInstructionSampleInBed
        Public Sequence As ULong
        Public Cs As UInt16
        Public Ip As UInt16
        Public Ax As UInt16
        Public Bx As UInt16
        Public Cx As UInt16
        Public Dx As UInt16
        Public Si As UInt16
        Public Di As UInt16
        Public Bp As UInt16
        Public Ds As UInt16
        Public Es As UInt16
        Public Ss As UInt16
        Public Sp As UInt16
        Public Flags As UInt16
        Public WasProtectedMode As Boolean
    End Structure

    Private ReadOnly _diagnosticQbInstructionRingInBed(DiagnosticQbInstructionCapacityInBed - 1) As DiagnosticQbInstructionSampleInBed
    Private _diagnosticQbInstructionWriteIndexInBed As Integer
    Private _diagnosticQbInstructionCountInBed As Integer
    Private ReadOnly _diagnosticQbEventTraceInBed As New System.Collections.Generic.Queue(Of String)()
    Private ReadOnly _diagnosticQbPendingInt13InBed As New System.Collections.Generic.List(Of DiagnosticQbInt13PendingInBed)()
    Private _diagnosticQbExecTraceEnabledInBed As Boolean
    Private _diagnosticQbEventSequenceInBed As ULong
    Private _diagnosticQbInstructionSequenceInBed As ULong
    Private _diagnosticQbInt13SequenceInBed As ULong
    Private _diagnosticQbExecProgramInBed As String = ""
    Private _diagnosticQbTerminalReasonInBed As String = ""
    ' Preserved from the rebased Interrupt Shadow + CS=FFFF Tripwire Brick 3 baseline.
    Private _diagnosticQbFirstCsFFFFTransitionInBed As String = ""

    Private Const DiagnosticImportantIntTraceCapacity As Integer = 1024
    Private Const DiagnosticDpmiTraceCapacity As Integer = 512
    Private Const DiagnosticDosFileTraceCapacityInBed As Integer = 768

    Private Structure DiagnosticDosFileCallPendingInBed
        Public Sequence As ULong
        Public Vector As Byte
        Public FunctionAx As UInt16
        Public ReturnCs As UInt16
        Public ReturnIp As UInt16
        Public FunctionAh As Byte
        Public FunctionAl As Byte
        Public CallerCs As UInt16
        Public CallerIp As UInt16
        Public Bx As UInt16
        Public Cx As UInt16
        Public Dx As UInt16
        Public Ds As UInt16
        Public Es As UInt16
        Public WasProtectedMode As Boolean
        Public EntryDetail As String
    End Structure

    Private ReadOnly _diagnosticImportantIntTrace As New System.Collections.Generic.Queue(Of String)()
    Private ReadOnly _diagnosticDpmiTrace As New System.Collections.Generic.Queue(Of String)()
    ' DOS file calls are retained separately from the general INT ring.  A
    ' Windows 3.1 failure returns to COMMAND.COM, whose INT 16h polling can
    ' overwrite thousands of useful entries before the operator clicks Dump.
    ' Pairing entry with the actual IRET destination also records DOS's CF/AX
    ' result without intercepting or replacing any DOS service.
    Private ReadOnly _diagnosticDosFileTraceInBed As New System.Collections.Generic.Queue(Of String)()
    Private ReadOnly _diagnosticDosFilePendingInBed As New System.Collections.Generic.List(Of DiagnosticDosFileCallPendingInBed)()
    Private ReadOnly _diagnosticImportantIntCounts(255) As ULong
    Private _diagnosticImportantIntTraceSequence As ULong
    Private _diagnosticImportantIntCallCount As ULong
    Private _diagnosticImportantIntTraceEnabled As Boolean
    Private _diagnosticDpmiTraceSequence As ULong
    Private _diagnosticDosFileTraceSequenceInBed As ULong
    Private _diagnosticDpmiExceptionReturnPending As Boolean
    Private _forensicTraceStreamInBed As System.IO.FileStream
    Private _forensicTraceBufferInBed As System.IO.BufferedStream
    Private _forensicTraceWriterInBed As System.IO.BinaryWriter
    Private _forensicTraceInstructionCountInBed As ULong
    Private _forensicTraceEventCountInBed As ULong
    Private _forensicTracePathInBed As String = String.Empty
    Private _forensicTraceTerminalReasonInBed As String = String.Empty
    Private _forensicSiSampleValidInBed As Boolean
    Private _forensicLastSiInBed As UInt16
    Private _forensicLastSiCsInBed As UInt16
    Private _forensicLastSiIpInBed As UInt16
    Private _forensicLastSiBytesInBed As String = String.Empty
    Private _diagnosticRealModeDosObservedInBed As Boolean
    ' LMSW enters protected mode at CPL 0.  The visible real-mode CS value is
    ' not yet a protected-mode selector, so its low two bits must not be
    ' interpreted as CPL until the first protected-mode CS load completes.
    Private _protectedModeCsLoadedInBed As Boolean

    Private Const HostFirmwareSegment As UInt16 = &HF000US
    Private Const HostFirmwareBaseOffset As UInt16 = &H100US
    Public Property AX As UInt16
    Public Property CX As UInt16
    Public Property DX As UInt16
    Public Property BX As UInt16
    Public Property SP As UInt16
    Public Property BP As UInt16
    Public Property SI As UInt16
    Public Property DI As UInt16
    Private ReadOnly _segmentSelectors(3) As UInt16
    Private ReadOnly _segmentBases(3) As UInteger
    Private ReadOnly _segmentLimits(3) As UInt16
    Private ReadOnly _segmentAccess(3) As Byte
    Private ReadOnly _segmentValid(3) As Boolean

    Public Property ES As UInt16
        Get
            Return _segmentSelectors(0)
        End Get
        Set(value As UInt16)
            AssignSegment(0, value)
        End Set
    End Property
    Public Property CS As UInt16
        Get
            Return _segmentSelectors(1)
        End Get
        Set(value As UInt16)
            AssignSegment(1, value)
        End Set
    End Property
    Public Property SS As UInt16
        Get
            Return _segmentSelectors(2)
        End Get
        Set(value As UInt16)
            AssignSegment(2, value)
        End Set
    End Property
    Public Property DS As UInt16
        Get
            Return _segmentSelectors(3)
        End Get
        Set(value As UInt16)
            AssignSegment(3, value)
        End Set
    End Property
    Public Property IP As UInt16
    ' CROMWELL 80286 REAL-ADDRESS FLAGS IDENTITY BRICK 1
    ' Intel 286 real-address FLAGS identity is architectural behavior, not an
    ' application compatibility hack: bits 12..15 read clear in real-address mode.
    Public Property Flags As UInt16 = &H2US
    Private _haltedInBed As Boolean
    Private _haltSourceInBed As ProcessorHaltSourceInBed

    Public Property Halted As Boolean
        Get
            Return _haltedInBed
        End Get
        Set(value As Boolean)
            _haltedInBed = value
            If Not value Then
                _haltSourceInBed = ProcessorHaltSourceInBed.None
            ElseIf _haltSourceInBed = ProcessorHaltSourceInBed.None Then
                _haltSourceInBed = ProcessorHaltSourceInBed.ExternalStop
            End If
        End Set
    End Property

    Public Property LastFault As String = ""
    Private Const DiagnosticCpuFaultCapacityInBed As Integer = 64
    Private Const DiagnosticCpuFirstFaultCapacityInBed As Integer = 32
    Private ReadOnly _diagnosticCpuFaultTraceInBed As New System.Collections.Generic.Queue(Of String)()
    Private ReadOnly _diagnosticCpuFirstFaultTraceInBed As New System.Collections.Generic.List(Of String)()
    Private _diagnosticCpuFaultSequenceInBed As ULong
    Private _diagnosticFaultAccessContextInBed As String = ""
    Private Const DiagnosticSelectorWordAddressInBed As UInteger = &HB68AUI
    Private Const DiagnosticSelectorWriteCapacityInBed As Integer = 128
    Private ReadOnly _diagnosticSelectorWriteTraceInBed As New System.Collections.Generic.Queue(Of String)()
    Private _diagnosticSelectorWriteSequenceInBed As ULong
    Private Const DiagnosticSelectorWriterHistoryCapacityInBed As Integer = 128
    Private Structure DiagnosticSelectorWriterSampleInBed
        Public Cs As UInt16
        Public Ip As UInt16
        Public CsBase As UInteger
        Public CsLimit As UInt16
        Public Ax As UInt16
        Public Bx As UInt16
        Public Cx As UInt16
        Public Dx As UInt16
        Public Si As UInt16
        Public Di As UInt16
        Public Bp As UInt16
        Public Sp As UInt16
        Public Ds As UInt16
        Public Es As UInt16
        Public Ss As UInt16
        Public Flags As UInt16
    End Structure
    Private ReadOnly _diagnosticSelectorWriterRingInBed(DiagnosticSelectorWriterHistoryCapacityInBed - 1) As DiagnosticSelectorWriterSampleInBed
    Private _diagnosticSelectorWriterIndexInBed As Integer
    Private _diagnosticSelectorWriterCountInBed As Integer
    Private ReadOnly _diagnosticSelectorWriterFrozenInBed As New System.Collections.Generic.List(Of String)()
    Private ReadOnly _diagnosticSecondCliEntryFrozenInBed As New System.Collections.Generic.List(Of String)()
    Private Const DiagnosticGpReturnHistoryCapacityInBed As Integer = 256
    Private ReadOnly _diagnosticGpReturnRingInBed(DiagnosticGpReturnHistoryCapacityInBed - 1) As DiagnosticSelectorWriterSampleInBed
    Private _diagnosticGpReturnIndexInBed As Integer
    Private _diagnosticGpReturnCountInBed As Integer
    Private _diagnosticGpReturnObservedAwayInBed As Boolean
    Private ReadOnly _diagnosticGpReturnFrozenInBed As New System.Collections.Generic.List(Of String)()
    Private Const DiagnosticGpHandlerCapacityInBed As Integer = 4096
    Private ReadOnly _diagnosticGpHandlerTraceInBed As New System.Collections.Generic.List(Of String)()
    Private _diagnosticGpHandlerRemainingInBed As Integer
    ' Keep a bounded, allocation-free history of writes into the active LDT.
    ' This is deliberately write-driven rather than instruction-driven: Windows
    ' 3.x can run for a long time before a demand-segment #NP, while the only
    ' evidence that matters is who last built or changed the descriptor.  Intel
    ' 80286 hardware reads descriptors from ordinary system memory; observing
    ' those memory writes does not alter architectural state or guest timing.
    Private Const DiagnosticLdtWriteHistoryCapacityInBed As Integer = 512
    Private Structure DiagnosticLdtWriteSampleInBed
        Public Address As UInteger
        Public LdtBase As UInteger
        Public LdtLimit As UInt16
        Public Value As UInt16
        Public Size As Byte
        Public Cs As UInt16
        Public Ip As UInt16
        Public Ax As UInt16
        Public Bx As UInt16
        Public Cx As UInt16
        Public Dx As UInt16
        Public Ds As UInt16
        Public Es As UInt16
        Public Ss As UInt16
        Public Sp As UInt16
        Public Flags As UInt16
    End Structure
    Private ReadOnly _diagnosticLdtWriteRingInBed(DiagnosticLdtWriteHistoryCapacityInBed - 1) As DiagnosticLdtWriteSampleInBed
    Private _diagnosticLdtWriteIndexInBed As Integer
    Private _diagnosticLdtWriteCountInBed As Integer
    Public Property MachineStatusWord As UInt16
    Public Property GdtrBase As UInteger
    Public Property GdtrLimit As UInt16
    Public Property IdtrBase As UInteger
    Public Property IdtrLimit As UInt16 = &H3FFUS
    Public Property LocalDescriptorTableRegister As UInt16
    Public Property TaskRegister As UInt16
    Private _ldtBaseInBed As UInteger
    Private _ldtLimitInBed As UInt16
    Private _ldtAccessInBed As Byte
    Private _ldtValidInBed As Boolean
    Private _taskBaseInBed As UInteger
    Private _taskLimitInBed As UInt16
    Private _taskAccessInBed As Byte
    Private _taskValidInBed As Boolean
    ' CROMWELL PCB REFIT PHASE 2 BRICK 8A
    Public Property PortBus As CpuLocalBus286
    Public Property InterruptAcknowledge As Func(Of Integer)
    ' CROMWELL PCB REFIT PHASE 2 BRICK 8B
    ' The CPU consumes motherboard memory; it no longer owns the backing store.
    Private _memoryControllerInBed As NeatMemoryController286

    Public Property MemoryController As NeatMemoryController286
        Get
            Return _memoryControllerInBed
        End Get
        Set(value As NeatMemoryController286)
            If value Is Nothing Then Throw New ArgumentNullException(NameOf(value))
            _memoryControllerInBed = value
        End Set
    End Property

    ' CROMWELL PCB REFIT PHASE 2 BRICK 8D - 80286 HOLD/HLDA pins.
    ' The current core executes an architectural instruction atomically, while
    ' motherboard transactions are atomic at CpuLocalBus286.  The bridge raises
    ' HOLD only at such a transaction boundary, so this brick can expose the
    ' correct logical HOLD -> HLDA ownership handshake without inventing a host
    ' delay.  Brick 8E attaches the documented pin-phase latency/wait timing.
    Private _busHoldRequestInBed As Boolean
    Private _busHoldAcknowledgeInBed As Boolean
    Private _holdObservedSinceLastRunInBed As Boolean
    Private _lockPrefixInBed As Boolean
    Private _busLockAssertedInBed As Boolean

    Public Sub SetBusHoldRequest(assertedInBed As Boolean)
        _busHoldRequestInBed = assertedInBed
        If assertedInBed Then _holdObservedSinceLastRunInBed = True
        ReconcileHoldAcknowledgeInBed()
    End Sub

    Private Sub ReconcileHoldAcknowledgeInBed()
        If Not _busHoldRequestInBed Then
            _busHoldAcknowledgeInBed = False
            Return
        End If
        ' LOCK prevents HLDA.  The motherboard bridge only raises HOLD at a
        ' completed CPU bus-cycle boundary (or before the next CPU bus cycle).
        ' The host may still be inside the surrounding architectural instruction;
        ' that must not delay HLDA, because a real 80286 releases the bus between
        ' cycles rather than waiting for the whole instruction to retire.
        _busHoldAcknowledgeInBed = Not _busLockAssertedInBed
    End Sub

    Public ReadOnly Property BusLockAsserted As Boolean
        Get
            Return _busLockAssertedInBed
        End Get
    End Property

    Public ReadOnly Property BusHoldRequestAsserted As Boolean
        Get
            Return _busHoldRequestInBed
        End Get
    End Property

    Public ReadOnly Property HoldAcknowledgeAsserted As Boolean
        Get
            Return _busHoldAcknowledgeInBed
        End Get
    End Property

    ' CROMWELL PCB REFIT PHASE 2 BRICK 8E - motherboard READY sink.
    Private _pendingReadyWaitTStatesInBed As Integer
    Private _readyWaitObservedSinceLastRunInBed As Boolean
    Private _suppressReadyWaitAccountingInBed As Boolean

    Public Sub RegisterReadyWaitStates(waitTStatesInBed As Integer,
                                       cpuReadyCycleInBed As Boolean)
        If waitTStatesInBed < 0 Then Throw New ArgumentOutOfRangeException(NameOf(waitTStatesInBed))
        If waitTStatesInBed = 0 OrElse _suppressReadyWaitAccountingInBed Then Return
        If _pendingReadyWaitTStatesInBed > Integer.MaxValue - waitTStatesInBed Then
            Throw New InvalidOperationException("Motherboard READY wait-state debt overflowed the CPU timing ledger.")
        End If
        _pendingReadyWaitTStatesInBed += waitTStatesInBed
        If cpuReadyCycleInBed Then _readyWaitObservedSinceLastRunInBed = True
    End Sub

    Private Function ConsumeReadyWaitTStatesInBed() As Integer
        Dim resultInBed As Integer = _pendingReadyWaitTStatesInBed
        _pendingReadyWaitTStatesInBed = 0
        Return resultInBed
    End Function

    Public Property A20Enabled As Boolean
        Get
            If _memoryControllerInBed Is Nothing Then Return False
            Return _memoryControllerInBed.A20Enabled
        End Get
        Set(value As Boolean)
            If _memoryControllerInBed Is Nothing Then
                Throw New InvalidOperationException("Processor286 is not attached to a motherboard memory controller.")
            End If
            _memoryControllerInBed.A20Enabled = value
        End Set
    End Property

    Private _lastRunActiveTStates As Long
    Private _lastRunIdleTStates As Long
    Private _lastRunStateByte As Byte
    Private _interruptObservedThisRunInBed As Boolean

    ' Host-only execution telemetry for the most recent bounded machine slice.
    ' The state byte is observational only; it never feeds back into guest state.
    Public ReadOnly Property LastRunStateByte As Byte
        Get
            Return _lastRunStateByte
        End Get
    End Property

    ' Host-only execution-duty telemetry for the most recent bounded machine slice.
    ' These are deliberately per-run counters, not lifetime accumulators: the
    ' front panel needs a bounded duty sample, and no guest-visible behavior
    ' should depend on a counter that can eventually overflow.
    Public ReadOnly Property LastRunActiveTStates As Long
        Get
            Return _lastRunActiveTStates
        End Get
    End Property

    Public ReadOnly Property LastRunIdleTStates As Long
        Get
            Return _lastRunIdleTStates
        End Get
    End Property
    Public ReadOnly Property InstalledMemoryBytes As UInteger
        Get
            If _memoryControllerInBed Is Nothing Then Return 0UI
            Return _memoryControllerInBed.InstalledMemoryBytes
        End Get
    End Property

    Public ReadOnly Property InstalledMemoryMegabytes As Integer
        Get
            If _memoryControllerInBed Is Nothing Then Return 0
            Return _memoryControllerInBed.InstalledMemoryMegabytes
        End Get
    End Property

    Public Sub ConfigureInstalledMemoryMegabytes(megabytes As Integer, Optional clearRam As Boolean = False)
        If _memoryControllerInBed Is Nothing Then
            Throw New InvalidOperationException("Processor286 is not attached to a motherboard memory controller.")
        End If
        _memoryControllerInBed.ConfigureInstalledMemoryMegabytes(megabytes, clearRam)
    End Sub
    Public Property HostFirmwareInterrupts As Boolean
    Public Property HostFirmwareHandler As Action(Of Byte)
    Public Property HaltOnCpuException As Boolean
    Public Property MirrorLegacyMemory As Boolean
        Get
            If _memoryControllerInBed Is Nothing Then Return False
            Return _memoryControllerInBed.MirrorLegacyMemory
        End Get
        Set(value As Boolean)
            If _memoryControllerInBed Is Nothing Then
                Throw New InvalidOperationException("Processor286 is not attached to a motherboard memory controller.")
            End If
            _memoryControllerInBed.MirrorLegacyMemory = value
        End Set
    End Property

    'The original CGA implementation stores each text cell as attribute then
    'character. Guest-visible RAM remains PC-compatible (character, attribute);
    'only the legacy card mirror crosses this adapter boundary.
    Public Property MirrorLegacyTextCells As Boolean
        Get
            If _memoryControllerInBed Is Nothing Then Return False
            Return _memoryControllerInBed.MirrorLegacyTextCells
        End Get
        Set(value As Boolean)
            If _memoryControllerInBed Is Nothing Then
                Throw New InvalidOperationException("Processor286 is not attached to a motherboard memory controller.")
            End If
            _memoryControllerInBed.MirrorLegacyTextCells = value
        End Set
    End Property

    ' CROMWELL 80287 OBJECT BRICK 1 - NPX architectural state is no longer
    ' loose state inside the 286.  The physical coprocessor owns its stack/tags/
    ' control/status state and exposes an ERROR output for later IRQ13 wiring.
    Private ReadOnly _numericCoprocessor As New Intel80287()
    Private _lastFpuInstructionOffsetInBed As UShort
    Private _lastFpuInstructionSelectorInBed As UShort
    Private _lastFpuOperandOffsetInBed As UShort
    Private _lastFpuOperandSelectorInBed As UShort
    Private _lastFpuOpcodeWordInBed As UShort
    Private _lastFpuInstructionPhysicalInBed As UInteger
    Private _lastFpuOperandPhysicalInBed As UInteger
    Private _diagnosticEscAttemptCountInBed As ULong
    Private _diagnosticEscNmTrapCountInBed As ULong
    Private _diagnosticLastEscCsInBed As UShort
    Private _diagnosticLastEscIpInBed As UShort
    Private _diagnosticLastEscOpcodeInBed As Byte
    Private _diagnosticLastEscMswInBed As UShort

    Public ReadOnly Property NumericCoprocessor As Intel80287
        Get
            Return _numericCoprocessor
        End Get
    End Property

    Public Property FpuControlWord As UInt16
        Get
            Return _numericCoprocessor.ControlWord
        End Get
        Set(value As UInt16)
            _numericCoprocessor.ControlWord = value
        End Set
    End Property

    Public Property FpuStatusWord As UInt16
        Get
            Return _numericCoprocessor.StatusWord
        End Get
        Set(value As UInt16)
            _numericCoprocessor.StatusWord = value
        End Set
    End Property

    Public ReadOnly Property NumericCoprocessorErrorAsserted As Boolean
        Get
            Return _numericCoprocessor.ErrorAsserted
        End Get
    End Property
    Public ReadOnly Property ProtectedMode As Boolean
        Get
            Return (MachineStatusWord And 1) <> 0
        End Get
    End Property

    Private Const CF As UInt16 = &H1US
    Private Const PF As UInt16 = &H4US
    Private Const AF As UInt16 = &H10US
    Private Const ZF As UInt16 = &H40US
    Private Const SF As UInt16 = &H80US
    Private Const TF As UInt16 = &H100US
    Private Const InterruptFlag As UInt16 = &H200US
    Private Const DF As UInt16 = &H400US
    Private Const OverflowFlag As UInt16 = &H800US
    Private _segOverride As Integer = -1
    Private _rep As Integer
    Private _instructionStartCs As UInt16
    Private _instructionStartIp As UInt16
    Private _instructionStartCsBaseInBed As UInteger
    Private _instructionStartCsLimitInBed As UInt16
    Private _instructionStartCsAccessInBed As Byte
    Private _instructionStartCsValidInBed As Boolean
    Private _currentInstructionActiveInBed As Boolean
    Private _currentInstructionAbortedInBed As Boolean
    Private _currentInstructionLengthInBed As Integer
    Private ReadOnly _prefetchBytesInBed(5) As Byte
    Private ReadOnly _prefetchIpsInBed(5) As UInt16
    Private ReadOnly _prefetchCsInBed(5) As UInt16
    Private ReadOnly _prefetchBasesInBed(5) As UInteger
    Private _prefetchCountInBed As Integer
    Private _interruptShadowRetirementsInBed As Integer
    Private _nmiShadowRetirementsInBed As Integer
    Private _trapFlagSampleAtInstructionStartInBed As Boolean
    Private _nmiBlockedInBed As Boolean
    Private _nmiNestedInterruptDepthInBed As Integer
    Private _enteringNmiInBed As Boolean
    Private _nmiPending As Boolean

    ' CROMWELL 80286 CORE REFIT BRICK 01 - execution substrate.
    ' Architectural exceptions abort the host-side decoder immediately rather
    ' than letting the remainder of a faulting instruction mutate guest state.
    Private NotInheritable Class InstructionAbortSignalInBed
        Inherits Exception
    End Class

    Private NotInheritable Class InterruptDeliveryFaultSignalInBed
        Inherits Exception

        Public ReadOnly Vector As Integer
        Public ReadOnly HasErrorCode As Boolean
        Public ReadOnly ErrorCode As UInt16

        Public Sub New(vectorInBed As Integer,
                       messageInBed As String,
                       hasErrorCodeInBed As Boolean,
                       errorCodeInBed As UInt16)
            MyBase.New(messageInBed)
            Vector = vectorInBed
            HasErrorCode = hasErrorCodeInBed
            ErrorCode = errorCodeInBed
        End Sub
    End Class

    Private _exceptionDeliveryActiveInBed As Boolean
    Private _exceptionDeliveryVectorInBed As Integer = -1

    Public Property AL As Byte
        Get
            Return CByte(AX And &HFFUS)
        End Get
        Set(value As Byte)
            AX = CUShort((AX And &HFF00US) Or value)
        End Set
    End Property
    Public Property AH As Byte
        Get
            Return CByte(AX >> 8)
        End Get
        Set(value As Byte)
            AX = CUShort((AX And &HFFUS) Or (CUShort(value) << 8))
        End Set
    End Property
    Public Property CL As Byte
        Get
            Return CByte(CX And &HFFUS)
        End Get
        Set(value As Byte)
            CX = CUShort((CX And &HFF00US) Or value)
        End Set
    End Property
    Public Property CH As Byte
        Get
            Return CByte(CX >> 8)
        End Get
        Set(value As Byte)
            CX = CUShort((CX And &HFFUS) Or (CUShort(value) << 8))
        End Set
    End Property
    Public Property DL As Byte
        Get
            Return CByte(DX And &HFFUS)
        End Get
        Set(value As Byte)
            DX = CUShort((DX And &HFF00US) Or value)
        End Set
    End Property
    Public Property DH As Byte
        Get
            Return CByte(DX >> 8)
        End Get
        Set(value As Byte)
            DX = CUShort((DX And &HFFUS) Or (CUShort(value) << 8))
        End Set
    End Property
    Public Property BL As Byte
        Get
            Return CByte(BX And &HFFUS)
        End Get
        Set(value As Byte)
            BX = CUShort((BX And &HFF00US) Or value)
        End Set
    End Property
    Public Property BH As Byte
        Get
            Return CByte(BX >> 8)
        End Get
        Set(value As Byte)
            BX = CUShort((BX And &HFFUS) Or (CUShort(value) << 8))
        End Set
    End Property

    Public Sub Reset(Optional preserveForensicTraceInBed As Boolean = False)
        If preserveForensicTraceInBed Then
            CaptureDiagnosticDetailedExecutionInBed("SHUTDOWN-RESET PRE")
        Else
            _diagnosticDetailedExecutionInBed.Clear()
            _diagnosticDetailedExecutionSequenceInBed = 0UL
        End If
        If _forensicTraceWriterInBed IsNot Nothing AndAlso preserveForensicTraceInBed Then
            WriteForensicEventInBed(
                "PROCESSOR-ONLY RESET: preserving forensic stream across AT shutdown resume")
        ElseIf _forensicTraceWriterInBed IsNot Nothing Then
            EndForensicTraceInBed("CPU RESET while protected-mode forensic trace was active")
        End If
        ' Intel 80286 RESET is not an ordinary real-mode CS load.  The visible
        ' selector is F000h while the hidden CS base is FF0000h, placing the
        ' first fetch at physical FFFFF0h.  MSW reserved-high reset identity is
        ' FFF0h; PE/MP/EM/TS are all clear.
        MachineStatusWord = &HFFF0US
        _diagnosticRealModeDosObservedInBed = False
        _protectedModeCsLoadedInBed = False
        AX = 0 : BX = 0 : CX = 0 : DX = 0 : SP = 0 : BP = 0 : SI = 0 : DI = 0
        _segmentSelectors(0) = 0US
        _segmentSelectors(1) = &HF000US
        _segmentSelectors(2) = 0US
        _segmentSelectors(3) = 0US
        _segmentBases(0) = 0UI
        _segmentBases(1) = &HFF0000UI
        _segmentBases(2) = 0UI
        _segmentBases(3) = 0UI
        For indexInBed As Integer = 0 To 3
            _segmentLimits(indexInBed) = &HFFFFUS
            _segmentValid(indexInBed) = True
        Next
        _segmentAccess(0) = &H93
        _segmentAccess(1) = &H9B
        _segmentAccess(2) = &H93
        _segmentAccess(3) = &H93
        IP = &HFFF0US
        Flags = &H2US : Halted = False : LastFault = "" : A20Enabled = False : _nmiPending = False
        If preserveForensicTraceInBed Then
            CaptureDiagnosticDetailedExecutionInBed("SHUTDOWN-RESET POST")
        End If
        _diagnosticCpuFaultTraceInBed.Clear()
        _diagnosticCpuFirstFaultTraceInBed.Clear()
        _diagnosticCpuFaultSequenceInBed = 0UL
        _diagnosticFaultAccessContextInBed = ""
        _diagnosticSelectorWriteTraceInBed.Clear()
        If Not preserveForensicTraceInBed Then
            _diagnosticEscAttemptCountInBed = 0UL
            _diagnosticEscNmTrapCountInBed = 0UL
            _diagnosticLastEscCsInBed = 0US
            _diagnosticLastEscIpInBed = 0US
            _diagnosticLastEscOpcodeInBed = 0
            _diagnosticLastEscMswInBed = 0US
        End If
        _diagnosticSelectorWriteSequenceInBed = 0UL
        _diagnosticSelectorWriterIndexInBed = 0
        _diagnosticSelectorWriterCountInBed = 0
        _diagnosticSelectorWriterFrozenInBed.Clear()
        _diagnosticSecondCliEntryFrozenInBed.Clear()
        _diagnosticGpReturnIndexInBed = 0
        _diagnosticGpReturnCountInBed = 0
        _diagnosticGpReturnObservedAwayInBed = False
        _diagnosticGpReturnFrozenInBed.Clear()
        _diagnosticGpHandlerTraceInBed.Clear()
        _diagnosticGpHandlerRemainingInBed = 0
        _diagnosticLdtWriteIndexInBed = 0
        _diagnosticLdtWriteCountInBed = 0
        _currentInstructionActiveInBed = False : _currentInstructionAbortedInBed = False : _currentInstructionLengthInBed = 0
        _exceptionDeliveryActiveInBed = False : _exceptionDeliveryVectorInBed = -1
        _prefetchCountInBed = 0
        _interruptShadowRetirementsInBed = 0 : _nmiShadowRetirementsInBed = 0
        _trapFlagSampleAtInstructionStartInBed = False
        _nmiBlockedInBed = False : _nmiNestedInterruptDepthInBed = 0 : _enteringNmiInBed = False
        _busHoldRequestInBed = False : _busHoldAcknowledgeInBed = False : _holdObservedSinceLastRunInBed = False
        _lockPrefixInBed = False : _busLockAssertedInBed = False
        _pendingReadyWaitTStatesInBed = 0 : _readyWaitObservedSinceLastRunInBed = False : _suppressReadyWaitAccountingInBed = False
        _lastRunActiveTStates = 0 : _lastRunIdleTStates = 0 : _lastRunStateByte = 0 : _interruptObservedThisRunInBed = False
        GdtrBase = 0 : GdtrLimit = 0 : IdtrBase = 0 : IdtrLimit = &H3FFUS
        LocalDescriptorTableRegister = 0 : TaskRegister = 0
        _ldtBaseInBed = 0UI : _ldtLimitInBed = 0US : _ldtAccessInBed = 0 : _ldtValidInBed = False
        _taskBaseInBed = 0UI : _taskLimitInBed = 0US : _taskAccessInBed = 0 : _taskValidInBed = False
        ResetNumericCoprocessor()
        ResetTimingState()
        ResetHotPathProfiler()
    End Sub

    ' Physical 80287 RESET input.  This is intentionally separate from the CPU
    ' reset path so motherboard port F1h can reset the coprocessor without
    ' inventing a processor reset or clearing unrelated CPU architectural state.
    Public Sub ResetNumericCoprocessor()
        _numericCoprocessor.HardwareReset()
    End Sub

    Public Overridable Sub RunCycle(maxInstructions As Integer)
        For n As Integer = 1 To Math.Max(0, maxInstructions)
            If _busHoldAcknowledgeInBed Then Exit For
            If ServicePendingNmi() Then
                Halted = False
                CommitSyntheticTiming(23 + Math.Max(1, MeasureInstructionLength(CS, IP)))
            ElseIf ServicePendingHardwareInterrupt() Then
                Halted = False
                CommitSyntheticTiming(23 + Math.Max(1, MeasureInstructionLength(CS, IP)))
            End If
            If Halted Then Exit For
            pStep()
        Next
    End Sub

    Public Overridable Function RunForTStates(maxTStates As Long,
                                              Optional timeAdvanced As Action(Of Long) = Nothing,
                                              Optional haltedStepBudget As Func(Of Long, Long) = Nothing) As Long
        If maxTStates < 0 Then Throw New ArgumentOutOfRangeException(NameOf(maxTStates))
        Dim consumed As Long
        Dim hotRunnerStartInBed As Long = System.Diagnostics.Stopwatch.GetTimestamp()
        Dim hotRunnerInstructionStartInBed As ULong = _hotPathInstructionCounterInBed
        Dim hotRunnerProfilerGenerationStartInBed As ULong = _hotPathProfilerGenerationInBed
        Dim activeTStatesInBed As Long
        Dim idleTStatesInBed As Long
        Dim stateByteInBed As Integer
        _interruptObservedThisRunInBed = False

        While consumed < maxTStates
            If _busHoldAcknowledgeInBed Then
                Dim heldTStatesInBed As Long = Math.Min(4L, maxTStates - consumed)
                consumed += heldTStatesInBed
                _totalTStates += heldTStatesInBed
                idleTStatesInBed += heldTStatesInBed
                stateByteInBed = stateByteInBed Or CInt(ProcessorStateByte.Hold)
                If timeAdvanced IsNot Nothing Then timeAdvanced.Invoke(heldTStatesInBed)
                Continue While
            End If
            If ServicePendingNmi() Then
                Halted = False
                Dim nmiTStates As Integer = 23 + Math.Max(1, MeasureInstructionLength(CS, IP))
                CommitSyntheticTiming(nmiTStates)
                Dim committedNmiTStatesInBed As Integer = _lastInstructionTStates
                consumed += committedNmiTStatesInBed
                activeTStatesInBed += committedNmiTStatesInBed
                If timeAdvanced IsNot Nothing Then timeAdvanced.Invoke(committedNmiTStatesInBed)
                Continue While
            End If

            If ServicePendingHardwareInterrupt() Then
                Halted = False
                Dim interruptTStates As Integer = 23 + Math.Max(1, MeasureInstructionLength(CS, IP))
                CommitSyntheticTiming(interruptTStates)
                Dim committedInterruptTStatesInBed As Integer = _lastInstructionTStates
                consumed += committedInterruptTStatesInBed
                activeTStatesInBed += committedInterruptTStatesInBed
                If timeAdvanced IsNot Nothing Then timeAdvanced.Invoke(committedInterruptTStatesInBed)
                Continue While
            End If

            If Halted Then
                ' HLT remains a real processor state.  The caller may advance
                ' directly to the next motherboard wake boundary, but the CPU
                ' executes no instructions during the skipped clock interval.
                ' Legacy/test callers retain the historical 64-T-state fallback.
                If _haltSourceInBed = ProcessorHaltSourceInBed.FaultStop Then
                    stateByteInBed = stateByteInBed Or CInt(ProcessorStateByte.Shutdown)
                Else
                    stateByteInBed = stateByteInBed Or CInt(ProcessorStateByte.Halt)
                End If
                Dim remainingTStates As Long = maxTStates - consumed
                Dim idleTStates As Long
                If haltedStepBudget Is Nothing Then
                    idleTStates = Math.Min(64L, remainingTStates)
                Else
                    idleTStates = haltedStepBudget.Invoke(remainingTStates)
                    If idleTStates <= 0 Then idleTStates = 1
                    If idleTStates > remainingTStates Then idleTStates = remainingTStates
                End If
                consumed += idleTStates
                _totalTStates += idleTStates
                idleTStatesInBed += idleTStates
                If timeAdvanced IsNot Nothing Then timeAdvanced.Invoke(idleTStates)
                Continue While
            End If

            stateByteInBed = stateByteInBed Or CInt(ProcessorStateByte.Run)
            pStep()
            consumed += _lastInstructionTStates
            activeTStatesInBed += _lastInstructionTStates
            If timeAdvanced IsNot Nothing Then timeAdvanced.Invoke(_lastInstructionTStates)
        End While

        If _interruptObservedThisRunInBed Then stateByteInBed = stateByteInBed Or CInt(ProcessorStateByte.Interrupt)
        If _readyWaitObservedSinceLastRunInBed Then stateByteInBed = stateByteInBed Or CInt(ProcessorStateByte.BusWait)
        _readyWaitObservedSinceLastRunInBed = False
        If _holdObservedSinceLastRunInBed OrElse _busHoldRequestInBed Then
            stateByteInBed = stateByteInBed Or CInt(ProcessorStateByte.Hold)
        End If
        _holdObservedSinceLastRunInBed = False
        If ProtectedMode Then stateByteInBed = stateByteInBed Or CInt(ProcessorStateByte.ProtectedMode)
        _lastRunStateByte = CByte(stateByteInBed And &HFF)
        _lastRunActiveTStates = activeTStatesInBed
        _lastRunIdleTStates = idleTStatesInBed
        ' A guest-visible RESET# can reset the host profiler while this runner
        ' call is still active. Do not combine pre-reset and post-reset samples.
        If _hotPathProfilerGenerationInBed = hotRunnerProfilerGenerationStartInBed Then
            _hotPathRunnerTicksInBed += System.Diagnostics.Stopwatch.GetTimestamp() - hotRunnerStartInBed
            _hotPathRunnerInstructionsInBed += _hotPathInstructionCounterInBed - hotRunnerInstructionStartInBed
            _hotPathRunnerCallsInBed += 1UL
        End If
        Return consumed
    End Function

    Public Sub RequestNmi()
        ' NMI is edge-triggered at the processor boundary.  The board-level NMI
        ' gate is responsible for mask/source line semantics; the CPU retains one
        ' pending edge until the next instruction boundary, even when IF=0.
        _nmiPending = True
    End Sub

    Private Function ServicePendingNmi() As Boolean
        If Not _nmiPending OrElse _nmiBlockedInBed OrElse _nmiShadowRetirementsInBed > 0 Then Return False
        _nmiPending = False
        _enteringNmiInBed = True
        Try
            Return EnterInterrupt(2, False)
        Finally
            _enteringNmiInBed = False
        End Try
    End Function

    Private Function ServicePendingHardwareInterrupt() As Boolean
        If InterruptAcknowledge Is Nothing OrElse Not Flag(InterruptFlag) OrElse _interruptShadowRetirementsInBed > 0 Then Return False
        Dim vector As Integer = InterruptAcknowledge.Invoke()
        If vector < 0 Then Return False
        _interruptObservedThisRunInBed = True
        Return EnterInterrupt(vector, False)
    End Function

    Public Sub pStep()
        ' Keep dormant forensic recorders off the ordinary instruction path.
        ' Their full routines are entered only while the corresponding bounded
        ' trace is armed and has work that can actually complete on this step.
        If _diagnosticImportantIntTraceEnabled AndAlso _diagnosticDosFilePendingInBed.Count <> 0 Then
            TraceDiagnosticDosFileReturnInBed()
        End If
        If _diagnosticQbExecTraceEnabledInBed Then TraceDiagnosticQbStepEntryInBed()
        _hotPathInstructionCounterInBed += 1UL
        Dim hotSampleInBed As Boolean =
            (_hotPathInstructionCounterInBed And HotPathSampleMaskInBed) = 0UL
        Dim hotStampInBed As Long = 0
        If hotSampleInBed Then
            _diagnosticExecutionSampleCsInBed(_diagnosticExecutionSampleWriteInBed) = CS
            _diagnosticExecutionSampleIpInBed(_diagnosticExecutionSampleWriteInBed) = IP
            _diagnosticExecutionSampleWriteInBed =
                (_diagnosticExecutionSampleWriteInBed + 1) Mod DiagnosticExecutionSampleCapacityInBed
            If _diagnosticExecutionSampleCountInBed < DiagnosticExecutionSampleCapacityInBed Then
                _diagnosticExecutionSampleCountInBed += 1
            End If
        End If

        ' CROMWELL HOST-FIRMWARE FAST GATE BRICK 6A
        ' Host firmware interception remains fully functional, but ordinary guest
        ' instructions no longer pay a method call merely to discover that the
        ' facility is disabled or that execution is outside the firmware segment.
        If hotSampleInBed Then hotStampInBed = System.Diagnostics.Stopwatch.GetTimestamp()
        Dim hotHostVectorHandledInBed As Boolean = False
        If HostFirmwareInterrupts AndAlso CS = HostFirmwareSegment Then
            hotHostVectorHandledInBed = TryExecuteHostFirmwareVectorCandidateInBed()
        End If
        If hotSampleInBed Then
            _hotPathProbeTicksInBed += System.Diagnostics.Stopwatch.GetTimestamp() - hotStampInBed
        End If

        If hotHostVectorHandledInBed Then
            If hotSampleInBed Then hotStampInBed = System.Diagnostics.Stopwatch.GetTimestamp()
            CommitSyntheticTiming(17 + Math.Max(1, MeasureInstructionLength(CS, IP)))
            If hotSampleInBed Then
                _hotPathTimingTicksInBed += System.Diagnostics.Stopwatch.GetTimestamp() - hotStampInBed
                _hotPathHostHandledSamplesInBed += 1UL
                _hotPathSampleCountInBed += 1UL
            End If
            Return
        End If

        ' CROMWELL FUSED TIMING DECODE BRICK 5A
        ' Build the 286 timing ledger from the real execution decoder instead of
        ' peeking and decoding the same instruction a second time before execution.
        If hotSampleInBed Then hotStampInBed = System.Diagnostics.Stopwatch.GetTimestamp()
        BeginFusedTimingContextInBed()
        If hotSampleInBed Then
            _hotPathCaptureTicksInBed += System.Diagnostics.Stopwatch.GetTimestamp() - hotStampInBed
            hotStampInBed = System.Diagnostics.Stopwatch.GetTimestamp()
        End If

        CaptureInstructionStartStateInBed()
        ' Windows 3.1 deliberately executes 0F FF here to probe #UD delivery.
        ' Start a fresh, narrowly targeted stream before fetching it so the
        ' exception handler and every write that constructs its RETF frame are
        ' retained independently of the earlier protected-mode-entry trace.
        If ProtectedMode AndAlso
           _instructionStartCs = &H48FUS AndAlso
           _instructionStartIp = &H9F05US Then
            If _forensicTraceWriterInBed IsNot Nothing Then
                EndForensicTraceInBed("superseded by targeted 048F:9F05 exception trace")
            End If
            BeginForensicTraceInBed()
            WriteForensicEventInBed(
                "TARGET TRACE BEGIN: deliberate 0F FF at 048F:9F05")
        End If
        ' The Windows 3.1 DPMI host validates the synthetic callback return in
        ' this short #GP-handler branch.  Preserve the actual instruction bytes
        ' (the ordinary compact record intentionally stores only the opcode) so
        ' a failed validation can be decoded without another broad trace.
        If _forensicTraceWriterInBed IsNot Nothing AndAlso
           ProtectedMode AndAlso
           _instructionStartCs = &H70US AndAlso
           _instructionStartIp >= &H1335US AndAlso
           _instructionStartIp <= &H1385US Then
            WriteForensicEventInBed(
                "DPMI GP VALIDATE at=0070:" & _instructionStartIp.ToString("X4") &
                " bytes=[" & ForensicInstructionBytesInBed(10) & "]" &
                " AX=" & AX.ToString("X4") & " BX=" & BX.ToString("X4") &
                " CX=" & CX.ToString("X4") & " DX=" & DX.ToString("X4") &
                " DS=" & DS.ToString("X4") & " SS:SP=" &
                SS.ToString("X4") & ":" & SP.ToString("X4") &
                " FL=" & Flags.ToString("X4"))
        End If
        ' The Windows 3.1 Standard-mode host reaches this path only after its
        ' movable-selector fault has been handled.  It validates the selector
        ' and builds the controlled LOADALL shutdown frame here.  Record full
        ' bytes only in these small ranges so a return to DOS identifies the
        ' precise failed architectural test without another broad trace.
        If _forensicTraceWriterInBed IsNot Nothing AndAlso ProtectedMode AndAlso
           ((_instructionStartCs = &H53US AndAlso
             ((_instructionStartIp >= &H380US AndAlso _instructionStartIp <= &H4D9US) OrElse
              (_instructionStartIp >= &H1EB9US AndAlso _instructionStartIp <= &H1EF9US))) OrElse
            (_instructionStartCs = &H5BUS AndAlso
             _instructionStartIp >= &HB62US AndAlso _instructionStartIp <= &HC01US) OrElse
            (_instructionStartCs = &H78US AndAlso
             _instructionStartIp >= &HC5EUS AndAlso _instructionStartIp <= &HC63US)) Then
            WriteForensicEventInBed(
                "DPMI SHUTDOWN DECISION at=" & _instructionStartCs.ToString("X4") & ":" &
                _instructionStartIp.ToString("X4") &
                " bytes=[" & ForensicInstructionBytesInBed(10) & "]" &
                " AX=" & AX.ToString("X4") & " BX=" & BX.ToString("X4") &
                " CX=" & CX.ToString("X4") & " DX=" & DX.ToString("X4") &
                " SI=" & SI.ToString("X4") & " DI=" & DI.ToString("X4") &
                " DS=" & DS.ToString("X4") & " ES=" & ES.ToString("X4") &
                " SS:SP=" & SS.ToString("X4") & ":" & SP.ToString("X4") &
                " FL=" & Flags.ToString("X4"))
        End If
        ' The Standard-mode loader reaches this compact cleanup/termination
        ' block after its final real-mode DOS callback.  Capture complete bytes
        ' here to identify the stored status value that selects INT 21h/4C01h;
        ' the ordinary instruction record intentionally retains only an opcode.
        If _forensicTraceWriterInBed IsNot Nothing AndAlso
           _instructionStartCs = &H48FUS AndAlso
           _instructionStartIp >= &H94E0US AndAlso
           _instructionStartIp <= &H9590US Then
            WriteForensicEventInBed(
                "WINDOWS EXIT DECISION at=048F:" & _instructionStartIp.ToString("X4") &
                " bytes=[" & ForensicInstructionBytesInBed(10) & "]" &
                " AX=" & AX.ToString("X4") & " BX=" & BX.ToString("X4") &
                " CX=" & CX.ToString("X4") & " DX=" & DX.ToString("X4") &
                " SI=" & SI.ToString("X4") & " DI=" & DI.ToString("X4") &
                " BP=" & BP.ToString("X4") & " DS=" & DS.ToString("X4") &
                " ES=" & ES.ToString("X4") & " SS:SP=" &
                SS.ToString("X4") & ":" & SP.ToString("X4") &
                " FL=" & Flags.ToString("X4") &
                " PE=" & If(ProtectedMode, "1", "0"))
        End If
        ' Windows' Standard-mode loader dispatches an installed module through
        ' an indirect far call in 0497:14xx.  Capture the complete dispatcher,
        ' the called 0DC7 entry stub, and the live stack arguments in one run.
        ' This is observation only: it neither recognizes nor changes the guest.
        If _forensicTraceWriterInBed IsNot Nothing AndAlso ProtectedMode AndAlso
           ((_instructionStartCs = &H497US AndAlso
             _instructionStartIp >= &H14E0US AndAlso _instructionStartIp <= &H1560US) OrElse
            (_instructionStartCs = &HDC7US AndAlso
             _instructionStartIp >= &H200US AndAlso _instructionStartIp <= &H240US)) Then
            WriteForensicEventInBed(
                "WINDOWS MODULE DISPATCH at=" & _instructionStartCs.ToString("X4") & ":" &
                _instructionStartIp.ToString("X4") &
                " bytes=[" & ForensicInstructionBytesInBed(12) & "]" &
                " AX=" & AX.ToString("X4") & " BX=" & BX.ToString("X4") &
                " CX=" & CX.ToString("X4") & " DX=" & DX.ToString("X4") &
                " SI=" & SI.ToString("X4") & " DI=" & DI.ToString("X4") &
                " BP=" & BP.ToString("X4") & " DS=" & DS.ToString("X4") &
                " ES=" & ES.ToString("X4") & " SS:SP=" &
                SS.ToString("X4") & ":" & SP.ToString("X4") &
                " FL=" & Flags.ToString("X4") &
                " CSBASE=" & _segmentBases(1).ToString("X6") &
                " DSBASE=" & _segmentBases(3).ToString("X6") &
                " SSBASE=" & _segmentBases(2).ToString("X6") &
                " STACK=[" & ForensicStackWordsInBed(16) & "]")
        End If
        ' Demand-loading an absent Windows segment enters these ring-3 loader
        ' routines.  The 2026-08-22 capture proved that the compact helper at
        ' 048F:5520 returns AX=0002 before the later inward RETF.  Windows then
        ' takes its error-cleanup path and handles that RETF #GP normally, so
        ' the protection check must not be weakened.  Preserve both the small
        ' classifier and the epilogue to identify the state which selected 2.
        '
        ' This is diagnostic observation only.  The ranges describe the
        ' Windows 3.1 Standard-mode kernel currently under investigation; they
        ' do not alter instruction execution or fabricate a successful load.
        If _forensicTraceWriterInBed IsNot Nothing AndAlso ProtectedMode AndAlso
           _instructionStartCs = &H48FUS AndAlso
           ((_instructionStartIp >= &H5520US AndAlso _instructionStartIp <= &H5580US) OrElse
            (_instructionStartIp >= &H56E0US AndAlso _instructionStartIp <= &H5830US)) Then
            ' This loader builds its fatal-error text in a temporary stack
            ' buffer. Retain enough words for the module name and message.
            WriteForensicEventInBed(
                "WINDOWS LOAD-SEGMENT RETURN at=048F:" & _instructionStartIp.ToString("X4") &
                " bytes=[" & ForensicInstructionBytesInBed(12) & "]" &
                " AX=" & AX.ToString("X4") & " BX=" & BX.ToString("X4") &
                " CX=" & CX.ToString("X4") & " DX=" & DX.ToString("X4") &
                " SI=" & SI.ToString("X4") & " DI=" & DI.ToString("X4") &
                " BP=" & BP.ToString("X4") & " DS=" & DS.ToString("X4") &
                " ES=" & ES.ToString("X4") & " SS:SP=" &
                SS.ToString("X4") & ":" & SP.ToString("X4") &
                " FL=" & Flags.ToString("X4") &
                " STACK192=[" & ForensicStackWordsInBed(192) & "]")
        End If
        ' A demand-loaded NE segment is relocated in this compact KRNL286
        ' routine.  Capture the link-chain walk, including the live DS cache;
        ' an invalid link must be traced to its producer rather than evading
        ' the 80286 limit check.  This is observational and uses direct DRAM
        ' peeks, so it creates no guest-visible bus cycles.
        If _forensicTraceWriterInBed IsNot Nothing AndAlso ProtectedMode AndAlso
           _instructionStartCs = &H48FUS AndAlso
           _instructionStartIp >= &H6D00US AndAlso
           _instructionStartIp <= &H6D80US Then
            Dim linkAddressInBed As UInteger =
                (_segmentBases(3) + CUInt(DI)) And &HFFFFFFUI
            Dim linkLowInBed As Byte = 0
            Dim linkHighInBed As Byte = 0
            Dim linkReadableInBed As Boolean =
                DiagnosticTryPeekRamByteInBed(linkAddressInBed, linkLowInBed) AndAlso
                DiagnosticTryPeekRamByteInBed(
                    (linkAddressInBed + 1UI) And &HFFFFFFUI, linkHighInBed)
            WriteForensicEventInBed(
                "WINDOWS RELOCATION CHAIN at=048F:" &
                _instructionStartIp.ToString("X4") &
                " bytes=[" & ForensicInstructionBytesInBed(12) & "]" &
                " AX=" & AX.ToString("X4") & " BX=" & BX.ToString("X4") &
                " CX=" & CX.ToString("X4") & " DX=" & DX.ToString("X4") &
                " SI=" & SI.ToString("X4") & " DI=" & DI.ToString("X4") &
                " DS=" & DS.ToString("X4") &
                " DSCACHE=[base=" & _segmentBases(3).ToString("X6") &
                " limit=" & _segmentLimits(3).ToString("X4") &
                " access=" & _segmentAccess(3).ToString("X2") & "]" &
                " LINKPHYS=" & linkAddressInBed.ToString("X6") &
                If(linkReadableInBed,
                   " LINKBYTES=[" & linkLowInBed.ToString("X2") & " " &
                   linkHighInBed.ToString("X2") & "]",
                   " LINKBYTES=[unmapped]"))
        End If
        _trapFlagSampleAtInstructionStartInBed = Flag(TF)
        _segOverride = -1 : _rep = 0 : _lockPrefixInBed = False
        _currentInstructionLengthInBed = 0
        _currentInstructionActiveInBed = True
        _currentInstructionAbortedInBed = False

        ' Detailed datapath sample: one instruction every 4096, deliberately at an
        ' offset that does not coincide with Brick 4's 1/1024 Stopwatch sample.
        Dim dataPathSampleInBed As Boolean =
            (_hotPathInstructionCounterInBed And 4095UL) = 1536UL
        Dim dataPathStampInBed As Long = 0
        If dataPathSampleInBed Then
            _dataPathSampleActiveInBed = True
            dataPathStampInBed = System.Diagnostics.Stopwatch.GetTimestamp()
        End If

        Dim op As Byte = 0
        Try
            Do
                op = FetchByte()
                Select Case op
                    Case &H26 : _segOverride = 0
                    Case &H2E : _segOverride = 1
                    Case &H36 : _segOverride = 2
                    Case &H3E : _segOverride = 3
                    Case &HF0
                        ' LOCK is a bus-control prefix, not an IOPL-privileged instruction.
                        _lockPrefixInBed = True
                    Case &HF1 ' Intel 286 no-function prefix; still counts toward 10-byte maximum.
                    Case &HF2 : _rep = 2
                    Case &HF3 : _rep = 3
                    Case Else : Exit Do
                End Select
            Loop

            WriteForensicInstructionInBed(op)

            If dataPathSampleInBed Then
                _dataPathOpcodeFetchTicksInBed +=
                    System.Diagnostics.Stopwatch.GetTimestamp() - dataPathStampInBed
                _dataPathOpcodeBytesInBed += CULng(_fusedTimingCodeBytesInBed)
                dataPathStampInBed = System.Diagnostics.Stopwatch.GetTimestamp()
            End If

            If _lockPrefixInBed Then
                RequireIoPrivilegeInBed()
                BeginBusLockInBed()
            End If
            RecordFusedPrimaryOpcodeInBed(op, _rep)
            Execute(op)
            TraceDiagnosticQbFirstCsFFFFTransitionInBed()
        Catch abortInBed As InstructionAbortSignalInBed
            _currentInstructionAbortedInBed = True
        Finally
            EndBusLockInBed()
            _currentInstructionActiveInBed = False
            ReconcileHoldAcknowledgeInBed()
        End Try

        If dataPathSampleInBed Then
            _dataPathExecuteBodyTicksInBed +=
                System.Diagnostics.Stopwatch.GetTimestamp() - dataPathStampInBed
            _dataPathCodeBytesInBed += CULng(_fusedTimingCodeBytesInBed)
            _dataPathSampleCountInBed += 1UL
            _dataPathSampleActiveInBed = False
        End If

        Dim timingContext As TimingContext286 = CompleteFusedTimingContextInBed()

        If hotSampleInBed Then
            _hotPathExecuteTicksInBed += System.Diagnostics.Stopwatch.GetTimestamp() - hotStampInBed
            hotStampInBed = System.Diagnostics.Stopwatch.GetTimestamp()
            _hotPathSampleActiveInBed = True
            Try
                CommitInstructionTiming(timingContext)
            Finally
                _hotPathSampleActiveInBed = False
            End Try
            _hotPathTimingTicksInBed += System.Diagnostics.Stopwatch.GetTimestamp() - hotStampInBed
            _hotPathSampleCountInBed += 1UL
        Else
            CommitInstructionTiming(timingContext)
        End If

        If Not _currentInstructionAbortedInBed Then RetireInstructionInBed()
    End Sub

    Public Sub InstallHostFirmwareVectors(Optional firstVector As Byte = &H8, Optional lastVector As Byte = &H1A)
        For vector As Integer = firstVector To lastVector
            Dim address As UInteger = CUInt(vector * 4)
            If ReadWord(address) = 0 AndAlso ReadWord(address + 2UI) = 0 Then
                WriteWord(address, CUShort(HostFirmwareBaseOffset + vector * 4))
                WriteWord(address + 2UI, HostFirmwareSegment)
            End If
        Next
    End Sub

    Private Function TryExecuteHostFirmwareVector() As Boolean
        ' Safe semantic wrapper retained for diagnostics/future callers.
        If Not HostFirmwareInterrupts OrElse CS <> HostFirmwareSegment Then Return False
        Return TryExecuteHostFirmwareVectorCandidateInBed()
    End Function

    Private Function TryExecuteHostFirmwareVectorCandidateInBed() As Boolean
        ' Caller has already established that host interception is enabled and that
        ' CS is the host-firmware segment.  All original vector/range/frame/IRET
        ' semantics remain below unchanged.
        Dim relative As Integer = CInt(IP) - HostFirmwareBaseOffset
        If relative < 0 OrElse (relative And 3) <> 0 Then Return False
        Dim vector As Integer = relative \ 4
        If vector < &H8 OrElse vector > &H1A Then Return False
        If HostFirmwareHandler Is Nothing Then Return False
        HostFirmwareHandler.Invoke(CByte(vector))

        ' INT and the conventional PUSHF/CALL FAR chaining sequence have the
        ' same three-word frame. Reflect BIOS result flags into that saved frame
        ' before IRET, as a physical ROM handler would.
        Dim savedFlagsOffset As UInt16 = CUShort((CInt(SP) + 4) And &HFFFF)
        Dim savedFlagsAddress As UInteger = SegmentAddress(2, savedFlagsOffset, 2)
        Const resultMask As UInt16 = CF Or PF Or AF Or ZF Or SF Or OverflowFlag
        Dim savedFlags As UInt16 = ReadWord(savedFlagsAddress)
        WriteWord(savedFlagsAddress, CUShort((savedFlags And Not resultMask) Or (Flags And resultMask)))
        ExecuteIret()
        Return True
    End Function

    Private Sub Execute(op As Byte)
        Select Case op
            Case &H0 To &H3, &H8 To &HB, &H10 To &H13, &H18 To &H1B,
                 &H20 To &H23, &H28 To &H2B, &H30 To &H33, &H38 To &H3B
                ExecuteAluRM(op)
            Case &H4, &HC, &H14, &H1C, &H24, &H2C, &H34, &H3C
                Dim kind As Integer = (op >> 3) And 7
                Dim b As Byte = FetchByte() : Dim r As Byte = Alu8(kind, AL, b)
                If kind <> 7 Then AL = r
            Case &H5, &HD, &H15, &H1D, &H25, &H2D, &H35, &H3D
                Dim kind As Integer = (op >> 3) And 7
                Dim w As UInt16 = FetchWord() : Dim r As UInt16 = Alu16(kind, AX, w)
                If kind <> 7 Then AX = r
            Case &H6 : Push(ES)
            Case &H7 : ExecutePopSegmentInBed(0)
            Case &HE : Push(CS)
            Case &H16 : Push(SS)
            Case &H17
                ExecutePopSegmentInBed(2)
                ArmSsInterruptShadowInBed()
            Case &H1E : Push(DS)
            Case &H1F : ExecutePopSegmentInBed(3)
            Case &H27, &H2F, &H37, &H3F : ExecuteDecimalAdjust(op)
            Case &H40 To &H47
                Dim i = op And 7 : Dim oldCf = Flag(CF) : SetReg16(i, Add16(GetReg16(i), 1)) : SetFlag(CF, oldCf)
            Case &H48 To &H4F
                Dim i = op And 7 : Dim oldCf = Flag(CF) : SetReg16(i, Sub16(GetReg16(i), 1)) : SetFlag(CF, oldCf)
            Case &H50 To &H57 : Push(GetReg16(op And 7))
            Case &H58 To &H5F : SetReg16(op And 7, PopWord())
            Case &H60
                Dim originalSp = SP : Push(AX) : Push(CX) : Push(DX) : Push(BX) : Push(originalSp) : Push(BP) : Push(SI) : Push(DI)
            Case &H61
                DI = PopWord() : SI = PopWord() : BP = PopWord() : PopWord() : BX = PopWord() : DX = PopWord() : CX = PopWord() : AX = PopWord()
            Case &H62 : ExecuteBound()
            Case &H63 : ExecuteArpl()
            Case &H68 : Push(FetchWord())
            Case &H69, &H6B : ExecuteImulImmediate(op)
            Case &H6A : Push(CUShort(Signed8(FetchByte()) And &HFFFF))
            Case &H6C To &H6F : ExecuteStringIo(op)
            Case &H70 To &H7F
                Dim d = Signed8(FetchByte()) : If Condition(op And &HF) Then IP = CUShort((CInt(IP) + d) And &HFFFF)
            Case &H80 To &H83 : ExecuteGroup1(op)
            Case &H84, &H85 : ExecuteTest(op)
            Case &H86, &H87 : ExecuteXchg(op)
            Case &H88 To &H8B : ExecuteMovRM(op)
            Case &H8C, &H8E : ExecuteMovSegment(op)
            Case &H8D : ExecuteLea()
            Case &H8F : ExecutePopRM()
            Case &H90 ' NOP
            Case &H91 To &H97
                Dim i = op And 7 : Dim t = AX : AX = GetReg16(i) : SetReg16(i, t)
            Case &H98 : AX = CUShort(Signed8(AL) And &HFFFF)
            Case &H99 : DX = If((AX And &H8000US) <> 0, &HFFFFUS, 0US)
            Case &H9A
                Dim newIpInBed As UInt16 = FetchWord()
                Dim newCsInBed As UInt16 = FetchWord()
                ExecuteFarControlTransferInBed(newCsInBed, newIpInBed, True)
            Case &H9B : ExecuteWaitInBed()
            Case &H9C : Push(NormalizeFlags(Flags))
            Case &H9D : ExecutePopfInBed()
            Case &H9E : Flags = CUShort((Flags And &HFF00US) Or (AH And &HD5) Or &H2)
            Case &H9F : AH = CByte(Flags And &HD7US)
            Case &HA0 To &HA3 : ExecuteMoffs(op)
            Case &HA4 To &HA7, &HAA To &HAF : ExecuteString(op)
            Case &HA8
                LogicFlags8(CByte(AL And FetchByte()))
            Case &HA9
                LogicFlags16(CUShort(AX And FetchWord()))
            Case &HB0 To &HB7 : SetReg8(op And 7, FetchByte())
            Case &HB8 To &HBF : SetReg16(op And 7, FetchWord())
            Case &HC0, &HC1, &HD0 To &HD3 : ExecuteShift(op)
            Case &HC2
                Dim cleanup As UInt16 = FetchWord()
                IP = PopWord() : SP = CUShort((CInt(SP) + cleanup) And &HFFFF)
            Case &HC3 : IP = PopWord()
            Case &HC4, &HC5 : ExecuteLoadFarPointer(op)
            Case &HC6, &HC7 : ExecuteMovImmediateRM(op)
            Case &HC8
                Dim size = FetchWord(), level = FetchByte() And &H1F : RecordFusedEnterLevelInBed(level) : Push(BP) : Dim frame = SP
                For i = 1 To level - 1 : BP = CUShort((CInt(BP) - 2) And &HFFFF) : Push(ReadWord(SegmentAddress(2, BP))) : Next
                If level > 0 Then Push(frame)
                BP = frame : SP = CUShort((CInt(SP) - size) And &HFFFF)
            Case &HC9 : SP = BP : BP = PopWord()
            Case &HCA
                Dim cleanup As UInt16 = FetchWord()
                ExecuteFarReturnInBed(cleanup)
            Case &HCB : ExecuteFarReturnInBed(0US)
            Case &HCC : SoftwareInterrupt(3)
            Case &HCD : SoftwareInterrupt(FetchByte())
            Case &HCE : If Flag(OverflowFlag) Then SoftwareInterrupt(4)
            Case &HCF : ExecuteIret()
            Case &HD4 : ExecuteAam()
            Case &HD5 : ExecuteAad()
            Case &HD6 ' Intel 286 proprietary single-byte operation; documented emulation is NOP.
            Case &HD7 : AL = ReadByte(CurrentDataAddress(False, CUShort((CInt(BX) + AL) And &HFFFF)))
            Case &HD8 To &HDF : ExecuteEsc(op)
            Case &HE0 To &HE3 : ExecuteLoop(op)
            Case &HE4 To &HE7, &HEC To &HEF : ExecutePortIo(op)
            Case &HE8 : Dim d = Signed16(FetchWord()) : Push(IP) : IP = CUShort((CInt(IP) + d) And &HFFFF)
            Case &HE9
                Dim displacement As Integer = Signed16(FetchWord())
                IP = CUShort((CInt(IP) + displacement) And &HFFFF)
            Case &HEA
                Dim niInBed As UInt16 = FetchWord(), ncInBed As UInt16 = FetchWord()
                ExecuteFarControlTransferInBed(ncInBed, niInBed, False)
            Case &HEB
                Dim displacement As Integer = Signed8(FetchByte())
                IP = CUShort((CInt(IP) + displacement) And &HFFFF)
            Case &HF4 : ExecuteHltInBed()
            Case &HF5 : SetFlag(CF, Not Flag(CF))
            Case &HF6, &HF7 : ExecuteGroup3(op)
            Case &HF8 : SetFlag(CF, False)
            Case &HF9 : SetFlag(CF, True)
            Case &HFA : ExecuteCliInBed()
            Case &HFB : ExecuteStiInBed()
            Case &HFC : SetFlag(DF, False)
            Case &HFD : SetFlag(DF, True)
            Case &HFE, &HFF : ExecuteGroup45(op)
            Case &HF : ExecuteSystemOpcode()
            Case Else : RaiseCpuException(6, "Invalid or unsupported opcode " & op.ToString("X2"))
        End Select
    End Sub

    Private Sub ExecuteAluRM(op As Byte)
        Dim m = DecodeModRM() : Dim kind = (op >> 3) And 7 : Dim word = (op And 1) <> 0 : Dim reverse = (op And 2) <> 0
        If word Then
            Dim a = If(reverse, GetReg16(m.Reg), ReadRM16(m)), b = If(reverse, ReadRM16(m), GetReg16(m.Reg))
            Dim r = Alu16(kind, a, b)
            If kind <> 7 Then
                If reverse Then SetReg16(m.Reg, r) Else WriteRM16(m, r)
            End If
        Else
            Dim a = If(reverse, GetReg8(m.Reg), ReadRM8(m)), b = If(reverse, ReadRM8(m), GetReg8(m.Reg))
            Dim r = Alu8(kind, a, b)
            If kind <> 7 Then
                If reverse Then SetReg8(m.Reg, r) Else WriteRM8(m, r)
            End If
        End If
    End Sub

    Private Sub ExecuteGroup1(op As Byte)
        Dim m = DecodeModRM(), word = op <> &H80 And op <> &H82
        If word Then
            Dim imm As UInt16 = If(op = &H83, CUShort(Signed8(FetchByte()) And &HFFFF), FetchWord())
            Dim r = Alu16(m.Reg, ReadRM16(m), imm) : If m.Reg <> 7 Then WriteRM16(m, r)
        Else
            Dim r = Alu8(m.Reg, ReadRM8(m), FetchByte()) : If m.Reg <> 7 Then WriteRM8(m, r)
        End If
    End Sub

    Private Sub ExecuteMovRM(op As Byte)
        Dim m = DecodeModRM(), word = (op And 1) <> 0, reverse = (op And 2) <> 0
        If word Then
            If reverse Then SetReg16(m.Reg, ReadRM16(m)) Else WriteRM16(m, GetReg16(m.Reg))
        Else
            If reverse Then SetReg8(m.Reg, ReadRM8(m)) Else WriteRM8(m, GetReg8(m.Reg))
        End If
    End Sub

    Private Sub ExecuteTest(op As Byte)
        Dim m = DecodeModRM()
        If (op And 1) = 0 Then LogicFlags8(CByte(ReadRM8(m) And GetReg8(m.Reg))) Else LogicFlags16(CUShort(ReadRM16(m) And GetReg16(m.Reg)))
    End Sub

    Private Sub ExecuteXchg(op As Byte)
        Dim m = DecodeModRM()
        Dim implicitLockInBed As Boolean = m.ModValue <> 3 AndAlso Not _busLockAssertedInBed
        If implicitLockInBed Then BeginBusLockInBed()
        Try
            If (op And 1) = 0 Then
                Dim a = ReadRM8(m), b = GetReg8(m.Reg) : WriteRM8(m, b) : SetReg8(m.Reg, a)
            Else
                Dim a = ReadRM16(m), b = GetReg16(m.Reg) : WriteRM16(m, b) : SetReg16(m.Reg, a)
            End If
        Finally
            If implicitLockInBed Then EndBusLockInBed()
        End Try
    End Sub

    Private Sub ExecuteMovSegment(op As Byte)
        Dim m = DecodeModRM()
        If m.Reg > 3 Then RaiseCpuException(6, "Invalid segment register encoding") : Return
        If op = &H8C Then
            WriteRM16(m, GetSeg(m.Reg))
            Return
        End If
        If m.Reg = 1 Then RaiseCpuException(6, "MOV to CS is invalid") : Return
        Dim selectorInBed As UInt16 = ReadRM16(m)
        Dim stagedInBed As SegmentLoadStageInBed = StageSegmentLoadInBed(m.Reg, selectorInBed)
        CommitSegmentLoadInBed(stagedInBed)
        If m.Reg = 2 Then ArmSsInterruptShadowInBed()
    End Sub

    Private Sub ExecuteLea()
        ' LEA decodes only the effective offset.  It performs no memory access
        ' and therefore must not translate or limit-check the selected segment.
        Dim m = DecodeModRM(resolveAddressInBed:=False)
        If m.ModValue = 3 Then RaiseCpuException(6, "LEA requires memory operand") Else SetReg16(m.Reg, m.Offset)
    End Sub

    Private Sub ExecutePopRM()
        Dim m = DecodeModRM() : If m.Reg <> 0 Then RaiseCpuException(6, "Invalid POP group") Else WriteRM16(m, PopWord())
    End Sub

    Private Sub ExecuteMovImmediateRM(op As Byte)
        Dim m = DecodeModRM() : If m.Reg <> 0 Then RaiseCpuException(6, "Invalid MOV immediate group") : Return
        If op = &HC6 Then WriteRM8(m, FetchByte()) Else WriteRM16(m, FetchWord())
    End Sub

    Private Sub ExecuteMoffs(op As Byte)
        Dim off = FetchWord()
        Dim writeOperationInBed As Boolean = op = &HA2 OrElse op = &HA3
        Dim addr = CurrentDataAddress(False,
                                      off,
                                      If((op And 1) <> 0, 2, 1),
                                      writeOperationInBed)
        Select Case op
            Case &HA0 : AL = ReadByte(addr)
            Case &HA1 : AX = ReadWord(addr)
            Case &HA2 : WriteByte(addr, AL)
            Case &HA3 : WriteWord(addr, AX)
        End Select
    End Sub

    Private Sub ExecuteString(op As Byte)
        If _rep <> 0 AndAlso CX = 0US Then Return

        Dim stepSize As Integer = If((op And 1) = 0, 1, 2)
        If Flag(DF) Then stepSize = -stepSize

        ' One REP element per architectural retirement boundary.  Rewinding IP
        ' to the prefix byte makes INTR/NMI/TF restart the same string instruction
        ' with already-updated SI/DI/CX, matching the 286 restart model.
        Select Case op
            Case &HA4
                Dim destinationInBed As UInteger = SegmentAddress(0, DI, 1, True)
                Dim sourceInBed As UInteger = CurrentDataAddress(False, SI)
                WriteByte(destinationInBed, ReadByte(sourceInBed))
            Case &HA5
                Dim destinationInBed As UInteger = SegmentAddress(0, DI, 2, True)
                Dim sourceInBed As UInteger = CurrentDataAddress(False, SI, 2)
                WriteWord(destinationInBed, ReadWord(sourceInBed))
            Case &HA6
                Sub8(ReadByte(CurrentDataAddress(False, SI)), ReadByte(SegmentAddress(0, DI)))
            Case &HA7
                Sub16(ReadWord(CurrentDataAddress(False, SI, 2)), ReadWord(SegmentAddress(0, DI, 2)))
            Case &HAA
                WriteByte(SegmentAddress(0, DI, 1, True), AL)
            Case &HAB
                WriteWord(SegmentAddress(0, DI, 2, True), AX)
            Case &HAC
                AL = ReadByte(CurrentDataAddress(False, SI))
            Case &HAD
                AX = ReadWord(CurrentDataAddress(False, SI, 2))
            Case &HAE
                Sub8(AL, ReadByte(SegmentAddress(0, DI)))
            Case &HAF
                Sub16(AX, ReadWord(SegmentAddress(0, DI, 2)))
        End Select

        If op <= &HA7 OrElse (op >= &HAC AndAlso op <= &HAD) Then
            SI = CUShort((CInt(SI) + stepSize) And &HFFFF)
        End If
        If (op >= &HA4 AndAlso op <= &HAB) OrElse op >= &HAE Then
            DI = CUShort((CInt(DI) + stepSize) And &HFFFF)
        End If

        If _rep = 0 Then Return
        CX = CUShort((CInt(CX) - 1) And &HFFFF)
        Dim repeatInBed As Boolean = CX <> 0US
        If repeatInBed AndAlso (op = &HA6 OrElse op = &HA7 OrElse op = &HAE OrElse op = &HAF) Then
            If _rep = 3 Then repeatInBed = Flag(ZF) Else repeatInBed = Not Flag(ZF)
        End If
        If repeatInBed Then RewindRepInstructionInBed()
    End Sub

    Private Sub ExecuteShift(op As Byte)
        Dim m = DecodeModRM(), word = (op And 1) <> 0
        Dim count As Integer = If(op = &HD0 Or op = &HD1, 1, If(op = &HD2 Or op = &HD3, CL, FetchByte())) And &H1F
        RecordFusedShiftCountInBed(count)
        If count = 0 Then Return
        If word Then WriteRM16(m, Shift16(m.Reg, ReadRM16(m), count)) Else WriteRM8(m, Shift8(m.Reg, ReadRM8(m), count))
    End Sub

    Private Sub ExecuteGroup3(op As Byte)
        Dim m = DecodeModRM(), word = op = &HF7
        If word Then
            Dim v = ReadRM16(m)
            Select Case m.Reg
                Case 0, 1 : LogicFlags16(CUShort(v And FetchWord()))
                Case 2 : WriteRM16(m, Not v)
                Case 3 : WriteRM16(m, Sub16(0, v))
                Case 4 : Dim p As UInt32 = CUInt(AX) * v : AX = CUShort(p And &HFFFFUI) : DX = CUShort(p >> 16) : SetFlag(CF, DX <> 0) : SetFlag(OverflowFlag, DX <> 0)
                Case 5 : Dim p As Integer = Signed16(AX) * Signed16(v) : AX = CUShort(p And &HFFFF) : DX = CUShort((p >> 16) And &HFFFF) : Dim fit = p >= Short.MinValue And p <= Short.MaxValue : SetFlag(CF, Not fit) : SetFlag(OverflowFlag, Not fit)
                Case 6 : If v = 0 Then RaiseCpuException(0, "Divide by zero") Else Dim q As UInt32 = (CUInt(DX) << 16 Or AX) \ v : If q > &HFFFFUI Then RaiseCpuException(0, "Divide overflow") Else Dim n As UInt32 = (CUInt(DX) << 16 Or AX) : AX = CUShort(q) : DX = CUShort(n Mod v)
                Case 7
                    Dim divisor As Integer = Signed16(v)
                    If divisor = 0 Then
                        RaiseCpuException(0, "Divide by zero")
                    Else
                        Dim dividend As Long = (CLng(Signed16(DX)) << 16) Or AX
                        Dim quotient As Long = dividend \ divisor
                        If quotient < Short.MinValue OrElse quotient > Short.MaxValue Then
                            RaiseCpuException(0, "Divide overflow")
                        Else
                            AX = CUShort(CInt(quotient) And &HFFFF)
                            DX = CUShort(CInt(dividend Mod divisor) And &HFFFF)
                        End If
                    End If
            End Select
        Else
            Dim v = ReadRM8(m)
            Select Case m.Reg
                Case 0, 1 : LogicFlags8(CByte(v And FetchByte()))
                Case 2 : WriteRM8(m, CByte((Not CInt(v)) And &HFF))
                Case 3 : WriteRM8(m, Sub8(0, v))
                Case 4 : AX = CUShort(CInt(AL) * CInt(v)) : SetFlag(CF, AH <> 0) : SetFlag(OverflowFlag, AH <> 0)
                Case 5 : Dim p As Short = CShort(Signed8(AL) * Signed8(v)) : AX = CUShort(CInt(p) And &HFFFF) : Dim fit = p >= SByte.MinValue And p <= SByte.MaxValue : SetFlag(CF, Not fit) : SetFlag(OverflowFlag, Not fit)
                Case 6
                    If v = 0 Then
                        RaiseCpuException(0, "Divide by zero")
                    Else
                        Dim dividend As UInt16 = AX
                        Dim q As Integer = dividend \ v
                        If q > &HFF Then
                            RaiseCpuException(0, "Divide overflow")
                        Else
                            Dim remainder As Integer = dividend Mod v
                            AL = CByte(q) : AH = CByte(remainder)
                        End If
                    End If
                Case 7 : If v = 0 Then RaiseCpuException(0, "Divide by zero") Else Dim n = Signed16(AX), divisor = Signed8(v), q = n \ divisor : If q < SByte.MinValue Or q > SByte.MaxValue Then RaiseCpuException(0, "Divide overflow") Else AL = CByte(q And &HFF) : AH = CByte((n Mod divisor) And &HFF)
            End Select
        End If
    End Sub

    Private Sub ExecuteGroup45(op As Byte)
        Dim m = DecodeModRM(), word = op = &HFF
        If Not word Then
            Dim old = Flag(CF), v = ReadRM8(m)
            If m.Reg = 0 Then
                WriteRM8(m, Add8(v, 1))
            ElseIf m.Reg = 1 Then
                WriteRM8(m, Sub8(v, 1))
            Else
                RaiseCpuException(6, "Invalid FE group")
            End If
            SetFlag(CF, old)
            Return
        End If
        Select Case m.Reg
            Case 0
                Dim old = Flag(CF) : WriteRM16(m, Add16(ReadRM16(m), 1)) : SetFlag(CF, old)
            Case 1
                Dim old = Flag(CF) : WriteRM16(m, Sub16(ReadRM16(m), 1)) : SetFlag(CF, old)
            Case 2
                Dim target = ReadRM16(m) : Push(IP) : IP = target
            Case 3
                If m.ModValue = 3 Then RaiseCpuException(6, "Far CALL requires memory")
                Dim farCallAddressInBed As UInteger = ResolveModRmMemoryAddressInBed(m, 4)
                Dim niInBed As UInt16 = ReadWord(farCallAddressInBed)
                Dim ncInBed As UInt16 = ReadWord(farCallAddressInBed + 2UI)
                ExecuteFarControlTransferInBed(ncInBed, niInBed, True)
            Case 4
                IP = ReadRM16(m)
            Case 5
                If m.ModValue = 3 Then RaiseCpuException(6, "Far JMP requires memory")
                Dim farJumpAddressInBed As UInteger = ResolveModRmMemoryAddressInBed(m, 4)
                Dim niInBed As UInt16 = ReadWord(farJumpAddressInBed)
                Dim ncInBed As UInt16 = ReadWord(farJumpAddressInBed + 2UI)
                ExecuteFarControlTransferInBed(ncInBed, niInBed, False)
            Case 6
                Push(ReadRM16(m))
            Case Else
                RaiseCpuException(6, "Invalid FF group")
        End Select
    End Sub

    Private Structure ModRM
        Public ModValue As Integer, Reg As Integer, RM As Integer
        Public Offset As UInt16, Segment As UInt16, SegmentIndex As Integer, Address As UInteger
    End Structure

    Private Function DecodeModRM(Optional resolveAddressInBed As Boolean = True) As ModRM
        Dim b = FetchByte(), m As New ModRM With {.ModValue = b >> 6, .Reg = (b >> 3) And 7, .RM = b And 7}
        RecordFusedModRmInBed(m.ModValue, m.Reg, m.RM)
        If m.ModValue = 3 Then Return m
        Dim base As Integer, usesBP As Boolean
        Select Case m.RM
            Case 0 : base = CInt(BX) + SI
            Case 1 : base = CInt(BX) + DI
            Case 2 : base = CInt(BP) + SI : usesBP = True
            Case 3 : base = CInt(BP) + DI : usesBP = True
            Case 4 : base = SI
            Case 5 : base = DI
            Case 6 : If m.ModValue = 0 Then base = FetchWord() Else base = BP : usesBP = True
            Case 7 : base = BX
        End Select
        If m.ModValue = 1 Then base += Signed8(FetchByte()) Else If m.ModValue = 2 Then base += Signed16(FetchWord())
        m.Offset = CUShort(base And &HFFFF)
        m.SegmentIndex = If(_segOverride >= 0, _segOverride, If(usesBP, 2, 3))
        m.Segment = GetSeg(m.SegmentIndex)
        If resolveAddressInBed Then m.Address = CurrentDataAddress(usesBP, m.Offset)
        Return m
    End Function

    Private Function ResolveModRmMemoryAddressInBed(operandInBed As ModRM,
                                                     lengthInBed As Integer,
                                                     Optional writeAccessInBed As Boolean = False) As UInteger
        If operandInBed.ModValue = 3 Then Throw New InvalidOperationException("Register ModR/M operand has no memory address")
        Return SegmentAddress(operandInBed.SegmentIndex,
                              operandInBed.Offset,
                              lengthInBed,
                              writeAccessInBed)
    End Function

    Private Shared Function Signed16(value As UInt16) As Integer
        Return If(value < &H8000US, CInt(value), CInt(value) - &H10000)
    End Function

    Private Shared Function Signed8(value As Byte) As Integer
        Return If(value < &H80, CInt(value), CInt(value) - &H100)
    End Function

    Private Function CurrentDataSegment(usesBP As Boolean) As UInt16
        If _segOverride >= 0 Then Return GetSeg(_segOverride)
        Return If(usesBP, SS, DS)
    End Function
    Private Function PhysicalRaw(seg As UInt16, offset As UInt16) As UInteger
        ' 80286 real-address generation is 16*segment + offset.  Do NOT
        ' truncate it to 20 bits here: the external A20 gate is applied later
        ' by NormalizePhysicalAddress(), just as on the physical motherboard.
        Return (CUInt(seg) << 4) + CUInt(offset)
    End Function

    Private Function CurrentDataAddress(usesBP As Boolean,
                                        offset As UInt16,
                                        Optional length As Integer = 1,
                                        Optional writeAccessInBed As Boolean = False) As UInteger
        Dim segmentIndex As Integer
        If _segOverride >= 0 Then segmentIndex = _segOverride Else segmentIndex = If(usesBP, 2, 3)
        Return SegmentAddress(segmentIndex, offset, length, writeAccessInBed)
    End Function

    ' CROMWELL 286 HIDDEN SEGMENT BASE CACHE BRICK 7B
    ' _segmentBases is already maintained whenever ES/CS/SS/DS is loaded:
    ' real mode caches selector<<4, protected mode caches the descriptor base.
    ' Use that hidden state here instead of re-deriving the real-mode base from
    ' the visible selector for every memory reference.
    <Runtime.CompilerServices.MethodImpl(Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)>
    Private Function SegmentAddress(segmentIndex As Integer,
                                    offset As UInt16,
                                    Optional length As Integer = 1,
                                    Optional writeAccessInBed As Boolean = False) As UInteger
        Dim dataPathSampleInBed As Boolean = _dataPathSampleActiveInBed
        Dim dataPathStampInBed As Long = 0
        If dataPathSampleInBed Then
            dataPathStampInBed = System.Diagnostics.Stopwatch.GetTimestamp()
            _dataPathSegmentCallsInBed += 1UL
        End If
        Try
            If ProtectedMode Then
                If Not _segmentValid(segmentIndex) Then
                    If segmentIndex = 2 Then
                        RaiseCpuException(12, "Use of an invalid stack segment", True, 0US)
                    Else
                        RaiseCpuException(13, "Use of an invalid segment register", True, 0US)
                    End If
                End If
                Dim firstOffsetInBed As UInteger = CUInt(offset)
                Dim lastOffsetInBed As UInteger =
                    firstOffsetInBed + CUInt(Math.Max(1, length)) - 1UI
                Dim accessInBed As Byte = _segmentAccess(segmentIndex)
                If writeAccessInBed Then
                    Dim writableDataInBed As Boolean =
                        (accessInBed And &H18) = &H10 AndAlso
                        (accessInBed And 2) <> 0
                    If Not writableDataInBed Then
                        If segmentIndex = 2 Then
                            RaiseCpuException(12, "Write through non-writable stack segment", True, 0US)
                        Else
                            RaiseCpuException(13, "Write through non-writable segment", True, 0US)
                        End If
                    End If
                End If
                Dim dataSegmentInBed As Boolean = (accessInBed And &H18) = &H10
                Dim expandDownInBed As Boolean =
                    dataSegmentInBed AndAlso (accessInBed And &H4) <> 0
                Dim outsideLimitInBed As Boolean
                If expandDownInBed Then
                    ' 286 expand-down data/stack segments invert the usable
                    ' range: limit+1 through FFFFh is valid.  Windows protected
                    ' mode uses these for downward-growing stacks.
                    outsideLimitInBed =
                        firstOffsetInBed <= _segmentLimits(segmentIndex) OrElse
                        lastOffsetInBed > &HFFFFUI
                Else
                    outsideLimitInBed = lastOffsetInBed > _segmentLimits(segmentIndex)
                End If
                If outsideLimitInBed Then
                    Dim segmentNameInBed As String = {"ES", "CS", "SS", "DS"}(segmentIndex)
                    _diagnosticFaultAccessContextInBed =
                        segmentNameInBed & " sel=" & _segmentSelectors(segmentIndex).ToString("X4") &
                        " base=" & _segmentBases(segmentIndex).ToString("X6") &
                        " off=" & offset.ToString("X4") &
                        " len=" & Math.Max(1, length).ToString() &
                        " last=" & lastOffsetInBed.ToString("X5") &
                        " limit=" & _segmentLimits(segmentIndex).ToString("X4") &
                        " access=" & accessInBed.ToString("X2") &
                        " expandDown=" & expandDownInBed.ToString()
                    If segmentIndex = 2 Then
                        RaiseCpuException(12, "Stack-segment limit exceeded", True, 0US)
                    Else
                        RaiseCpuException(13, "Segment limit exceeded", True, 0US)
                    End If
                End If
            End If

            ' In real mode this is cached selector<<4. In protected mode this is
            ' the cached descriptor base after the validity/limit checks above.
            Return (_segmentBases(segmentIndex) + CUInt(offset)) And &HFFFFFFUI
        Finally
            If dataPathSampleInBed Then
                _dataPathSegmentTicksInBed +=
                    System.Diagnostics.Stopwatch.GetTimestamp() - dataPathStampInBed
            End If
        End Try
    End Function

    Private Sub ResetDiagnosticQbWriteWatchInBed()
        _diagnosticQbWriteWatchInBed.Clear()
        _diagnosticQbWriteWatchSequenceInBed = 0UL
        _diagnosticQbWriteWatchTotalInBed = 0UL
    End Sub

    Private Function DiagnosticQbTryPeekLowRamByteInBed(addressInBed As UInteger,
                                                         ByRef valueInBed As Byte) As Boolean
        If _memoryControllerInBed Is Nothing Then Return False
        Dim normalizedInBed As UInteger = _memoryControllerInBed.NormalizePhysicalAddress(addressInBed)
        If normalizedInBed >= &H100000UI Then Return False
        valueInBed = _memoryControllerInBed.LowMemoryInBed(CInt(normalizedInBed))
        Return True
    End Function

    Private Function DiagnosticQbTryPeekLowRamWordInBed(addressInBed As UInteger,
                                                         ByRef valueInBed As UInt16) As Boolean
        Dim lowInBed As Byte
        Dim highInBed As Byte
        If Not DiagnosticQbTryPeekLowRamByteInBed(addressInBed, lowInBed) Then Return False
        If Not DiagnosticQbTryPeekLowRamByteInBed(addressInBed + 1UI, highInBed) Then Return False
        valueInBed = CUShort(CInt(lowInBed) Or (CInt(highInBed) << 8))
        Return True
    End Function

    Private Function DiagnosticQbRelocationEntryTextInBed() As String
        Dim entryPhysicalInBed As UInteger = PhysicalRaw(ES, DI)
        Dim b0InBed As Byte
        Dim b1InBed As Byte
        Dim b2InBed As Byte
        Dim b3InBed As Byte
        If Not DiagnosticQbTryPeekLowRamByteInBed(entryPhysicalInBed, b0InBed) OrElse
           Not DiagnosticQbTryPeekLowRamByteInBed(entryPhysicalInBed + 1UI, b1InBed) OrElse
           Not DiagnosticQbTryPeekLowRamByteInBed(entryPhysicalInBed + 2UI, b2InBed) OrElse
           Not DiagnosticQbTryPeekLowRamByteInBed(entryPhysicalInBed + 3UI, b3InBed) Then
            Return "reloc ES:DI=" & ES.ToString("X4") & ":" & DI.ToString("X4") &
                   " phys=" & entryPhysicalInBed.ToString("X5") & " bytes=<not-low-DRAM>"
        End If

        Dim relocationOffsetInBed As UInt16 =
            CUShort(CInt(b0InBed) Or (CInt(b1InBed) << 8))
        Dim relocationSegmentInBed As UInt16 =
            CUShort(CInt(b2InBed) Or (CInt(b3InBed) << 8))
        Return "reloc ES:DI=" & ES.ToString("X4") & ":" & DI.ToString("X4") &
               " phys=" & entryPhysicalInBed.ToString("X5") &
               " bytes=" & b0InBed.ToString("X2") & " " & b1InBed.ToString("X2") & " " &
                            b2InBed.ToString("X2") & " " & b3InBed.ToString("X2") &
               " entry=" & relocationSegmentInBed.ToString("X4") & ":" &
                           relocationOffsetInBed.ToString("X4")
    End Function

    Private Sub AppendDiagnosticQbWriteWatchInBed(textInBed As String)
        _diagnosticQbWriteWatchSequenceInBed += 1UL
        _diagnosticQbWriteWatchTotalInBed += 1UL
        _diagnosticQbWriteWatchInBed.Enqueue(
            "#" & _diagnosticQbWriteWatchSequenceInBed.ToString("D6") & " " & textInBed)
        While _diagnosticQbWriteWatchInBed.Count > DiagnosticQbWriteWatchCapacityInBed
            _diagnosticQbWriteWatchInBed.Dequeue()
        End While
    End Sub

    Private Function DiagnosticQbWriteWatchReasonInBed(firstPhysicalInBed As UInteger,
                                                        lastPhysicalInBed As UInteger) As String
        Dim writerMatchInBed As Boolean =
            _instructionStartCs = DiagnosticQbObservedLoaderCsInBed AndAlso
            _instructionStartIp >= DiagnosticQbObservedLoaderIpStartInBed AndAlso
            _instructionStartIp <= DiagnosticQbObservedLoaderIpEndInBed

        Dim targetMatchInBed As Boolean =
            lastPhysicalInBed >= DiagnosticQbObservedLoaderPhysicalStartInBed AndAlso
            firstPhysicalInBed <= DiagnosticQbObservedLoaderPhysicalEndInBed

        If writerMatchInBed AndAlso targetMatchInBed Then Return "WRITER+TARGET"
        If targetMatchInBed Then Return "TARGET-WINDOW"
        If writerMatchInBed Then Return "WRITER-LOOP"
        Return ""
    End Function

    Private Sub TraceDiagnosticQbWriteByteInBed(addressInBed As UInteger, valueInBed As Byte)
        If Not _diagnosticQbExecTraceEnabledInBed Then Return
        If _memoryControllerInBed Is Nothing Then Return

        Dim physicalInBed As UInteger =
            _memoryControllerInBed.NormalizePhysicalAddress(addressInBed)
        Dim reasonInBed As String =
            DiagnosticQbWriteWatchReasonInBed(physicalInBed, physicalInBed)
        If reasonInBed.Length = 0 Then Return

        Dim oldInBed As Byte
        Dim oldTextInBed As String =
            If(DiagnosticQbTryPeekLowRamByteInBed(physicalInBed, oldInBed),
               oldInBed.ToString("X2"),
               "??")

        Dim dsBxPhysicalInBed As UInteger = PhysicalRaw(DS, BX)
        AppendDiagnosticQbWriteWatchInBed(
            reasonInBed & " W8" &
            " writer=" & _instructionStartCs.ToString("X4") & ":" &
                           _instructionStartIp.ToString("X4") &
            " cpu-now=" & CS.ToString("X4") & ":" & IP.ToString("X4") &
            " target=" & physicalInBed.ToString("X5") &
            " old=" & oldTextInBed & " new=" & valueInBed.ToString("X2") &
            " DS:BX=" & DS.ToString("X4") & ":" & BX.ToString("X4") &
            " phys=" & dsBxPhysicalInBed.ToString("X5") &
            " SI=" & SI.ToString("X4") &
            " " & DiagnosticQbRelocationEntryTextInBed())
    End Sub

    Private Sub TraceDiagnosticQbWriteWordInBed(addressInBed As UInteger, valueInBed As UInt16)
        If Not _diagnosticQbExecTraceEnabledInBed Then Return
        If _memoryControllerInBed Is Nothing Then Return

        Dim firstPhysicalInBed As UInteger =
            _memoryControllerInBed.NormalizePhysicalAddress(addressInBed)
        Dim secondPhysicalInBed As UInteger =
            _memoryControllerInBed.NormalizePhysicalAddress(addressInBed + 1UI)
        Dim lowPhysicalInBed As UInteger = Math.Min(firstPhysicalInBed, secondPhysicalInBed)
        Dim highPhysicalInBed As UInteger = Math.Max(firstPhysicalInBed, secondPhysicalInBed)
        Dim reasonInBed As String =
            DiagnosticQbWriteWatchReasonInBed(lowPhysicalInBed, highPhysicalInBed)
        If reasonInBed.Length = 0 Then Return

        Dim oldInBed As UInt16
        Dim oldTextInBed As String =
            If(DiagnosticQbTryPeekLowRamWordInBed(addressInBed, oldInBed),
               oldInBed.ToString("X4"),
               "????")

        Dim dsBxPhysicalInBed As UInteger = PhysicalRaw(DS, BX)
        AppendDiagnosticQbWriteWatchInBed(
            reasonInBed & " W16" &
            " writer=" & _instructionStartCs.ToString("X4") & ":" &
                           _instructionStartIp.ToString("X4") &
            " cpu-now=" & CS.ToString("X4") & ":" & IP.ToString("X4") &
            " target=" & firstPhysicalInBed.ToString("X5") &
            " old=" & oldTextInBed & " new=" & valueInBed.ToString("X4") &
            " DS:BX=" & DS.ToString("X4") & ":" & BX.ToString("X4") &
            " phys=" & dsBxPhysicalInBed.ToString("X5") &
            " SI=" & SI.ToString("X4") &
            " " & DiagnosticQbRelocationEntryTextInBed())
    End Sub

    Private Sub AppendDiagnosticQbWriteWatchReportInBed(reportInBed As System.Text.StringBuilder)
        reportInBed.AppendLine("--- QB relocation write watch ---")
        reportInBed.AppendLine(
            "Observed DOS loader writer window: " &
            DiagnosticQbObservedLoaderCsInBed.ToString("X4") & ":" &
            DiagnosticQbObservedLoaderIpStartInBed.ToString("X4") & "-" &
            DiagnosticQbObservedLoaderIpEndInBed.ToString("X4"))
        reportInBed.AppendLine(
            "Observed loader code physical window: " &
            DiagnosticQbObservedLoaderPhysicalStartInBed.ToString("X5") & "-" &
            DiagnosticQbObservedLoaderPhysicalEndInBed.ToString("X5"))
        reportInBed.AppendLine(
            "Matching CPU writes seen: " & _diagnosticQbWriteWatchTotalInBed.ToString("N0") &
            " (rolling capacity " & DiagnosticQbWriteWatchCapacityInBed.ToString("N0") & ")")
        reportInBed.AppendLine(
            "WRITER-LOOP = a write issued by 02C3:9E40-9E60; TARGET-WINDOW = a write landing in 0CA70-0CA90.")
        reportInBed.AppendLine(
            "Old values and relocation bytes are direct NEAT-DRAM peeks; this diagnostic adds no guest bus reads.")
        If _diagnosticQbWriteWatchInBed.Count = 0 Then
            reportInBed.AppendLine("<none>")
        Else
            For Each lineInBed As String In _diagnosticQbWriteWatchInBed
                reportInBed.AppendLine(lineInBed)
            Next
        End If
        reportInBed.AppendLine()
    End Sub

    Private Function DiagnosticQbNoBusCodeBytesInBed(codeCsInBed As UInt16,
                                                        codeIpInBed As UInt16,
                                                        protectedModeInBed As Boolean) As String
        If protectedModeInBed Then Return "<protected-mode bytes suppressed>"
        Dim bytesInBed As New System.Text.StringBuilder()
        For indexInBed As Integer = 0 To 7
            If indexInBed <> 0 Then bytesInBed.Append(" "c)
            Dim currentIpInBed As UInt16 = CUShort((CInt(codeIpInBed) + indexInBed) And &HFFFF)
            Dim valueInBed As Byte
            If DiagnosticQbTryPeekLowRamByteInBed(PhysicalRaw(codeCsInBed, currentIpInBed), valueInBed) Then
                bytesInBed.Append(valueInBed.ToString("X2"))
            Else
                bytesInBed.Append("??")
            End If
        Next
        Return bytesInBed.ToString()
    End Function

    Private Function DiagnosticQbNoBusStackWordsInBed() As String
        If ProtectedMode Then Return "<protected-mode stack suppressed>"
        Dim wordsInBed As New System.Text.StringBuilder()
        For byteOffsetInBed As Integer = 0 To 6 Step 2
            If byteOffsetInBed <> 0 Then wordsInBed.Append(" "c)
            Dim stackOffsetInBed As UInt16 = CUShort((CInt(SP) + byteOffsetInBed) And &HFFFF)
            Dim stackWordInBed As UInt16
            If DiagnosticQbTryPeekLowRamWordInBed(PhysicalRaw(SS, stackOffsetInBed), stackWordInBed) Then
                wordsInBed.Append(stackWordInBed.ToString("X4"))
            Else
                wordsInBed.Append("????")
            End If
        Next
        Return wordsInBed.ToString()
    End Function

    Private Sub TraceDiagnosticQbFirstCsFFFFTransitionInBed()
        If Not _diagnosticQbExecTraceEnabledInBed Then Return
        If _diagnosticQbFirstCsFFFFTransitionInBed.Length <> 0 Then Return
        If _instructionStartCs = &HFFFFUS OrElse CS <> &HFFFFUS Then Return

        Dim transitionInBed As String =
            "from=" & _instructionStartCs.ToString("X4") & ":" & _instructionStartIp.ToString("X4") &
            " bytes=" & DiagnosticQbNoBusCodeBytesInBed(_instructionStartCs, _instructionStartIp, ProtectedMode) &
            " -> " & CS.ToString("X4") & ":" & IP.ToString("X4") &
            " AX=" & AX.ToString("X4") &
            " BX=" & BX.ToString("X4") &
            " CX=" & CX.ToString("X4") &
            " DX=" & DX.ToString("X4") &
            " SI=" & SI.ToString("X4") &
            " DI=" & DI.ToString("X4") &
            " BP=" & BP.ToString("X4") &
            " DS=" & DS.ToString("X4") &
            " ES=" & ES.ToString("X4") &
            " SS:SP=" & SS.ToString("X4") & ":" & SP.ToString("X4") &
            " FL=" & Flags.ToString("X4") &
            " IRQ-shadow=" & _interruptShadowRetirementsInBed.ToString() &
            " NMI-shadow=" & _nmiShadowRetirementsInBed.ToString() &
            " stack[SP..+6]=" & DiagnosticQbNoBusStackWordsInBed()

        _diagnosticQbFirstCsFFFFTransitionInBed = transitionInBed
        AppendDiagnosticQbEventInBed("FIRST CS=FFFF TRANSITION " & transitionInBed)
    End Sub

    Private Sub BeginDiagnosticQbExecTraceInBed(programNameInBed As String)
        ResetDiagnosticQbWriteWatchInBed()
        _diagnosticQbFirstCsFFFFTransitionInBed = ""
        _diagnosticQbInstructionWriteIndexInBed = 0
        _diagnosticQbInstructionCountInBed = 0
        _diagnosticQbEventTraceInBed.Clear()
        _diagnosticQbPendingInt13InBed.Clear()
        _diagnosticQbEventSequenceInBed = 0UL
        _diagnosticQbInstructionSequenceInBed = 0UL
        _diagnosticQbInt13SequenceInBed = 0UL
        _diagnosticQbExecProgramInBed = If(programNameInBed, "")
        _diagnosticQbTerminalReasonInBed = ""
        _diagnosticQbExecTraceEnabledInBed = True
        AppendDiagnosticQbEventInBed("TRACE ARM on DOS EXEC program=""" & _diagnosticQbExecProgramInBed & """ next=" &
                                     CS.ToString("X4") & ":" & IP.ToString("X4") &
                                     " SS:SP=" & SS.ToString("X4") & ":" & SP.ToString("X4"))
    End Sub

    Public Sub ClearDiagnosticQbExecTrace()
        ResetDiagnosticQbWriteWatchInBed()
        _diagnosticQbFirstCsFFFFTransitionInBed = ""
        _diagnosticQbExecTraceEnabledInBed = False
        _diagnosticQbInstructionWriteIndexInBed = 0
        _diagnosticQbInstructionCountInBed = 0
        _diagnosticQbEventTraceInBed.Clear()
        _diagnosticQbPendingInt13InBed.Clear()
        _diagnosticQbEventSequenceInBed = 0UL
        _diagnosticQbInstructionSequenceInBed = 0UL
        _diagnosticQbInt13SequenceInBed = 0UL
        _diagnosticQbExecProgramInBed = ""
        _diagnosticQbTerminalReasonInBed = ""
    End Sub

    Public Sub EndDiagnosticQbExecTrace()
        If _diagnosticQbExecTraceEnabledInBed Then
            AppendDiagnosticQbEventInBed("TRACE MANUALLY DISARMED")
        End If
        _diagnosticQbExecTraceEnabledInBed = False
    End Sub

    Public ReadOnly Property DiagnosticQbExecTraceEnabled As Boolean
        Get
            Return _diagnosticQbExecTraceEnabledInBed
        End Get
    End Property

    Private Sub AppendDiagnosticQbEventInBed(textInBed As String)
        _diagnosticQbEventSequenceInBed += 1UL
        _diagnosticQbEventTraceInBed.Enqueue("#" & _diagnosticQbEventSequenceInBed.ToString("D6") & " " & textInBed)
        While _diagnosticQbEventTraceInBed.Count > DiagnosticQbEventCapacityInBed
            _diagnosticQbEventTraceInBed.Dequeue()
        End While
    End Sub

    Private Function ReadDiagnosticGuestAsciizInBed(segmentInBed As UInt16, offsetInBed As UInt16) As String
        If ProtectedMode Then Return "<protected-mode EXEC pathname not sampled>"
        Dim textInBed As New System.Text.StringBuilder()
        For indexInBed As Integer = 0 To 127
            Dim currentOffsetInBed As UInt16 = CUShort((CInt(offsetInBed) + indexInBed) And &HFFFF)
            Dim valueInBed As Byte = ReadByte(PhysicalRaw(segmentInBed, currentOffsetInBed))
            If valueInBed = 0 Then Exit For
            If valueInBed >= &H20 AndAlso valueInBed <= &H7E Then
                textInBed.Append(ChrW(valueInBed))
            Else
                textInBed.Append("."c)
            End If
        Next
        Return textInBed.ToString()
    End Function

    Private Sub TraceDiagnosticQbExecSoftwareInterruptInBed(vectorInBed As Byte)
        If vectorInBed = &H21 AndAlso AH = &H4B Then
            Dim programNameInBed As String = ReadDiagnosticGuestAsciizInBed(DS, DX)
            If IsDiagnosticQbExecTargetInBed(programNameInBed) Then
                BeginDiagnosticQbExecTraceInBed(programNameInBed)
            ElseIf _diagnosticQbExecTraceEnabledInBed Then
                ' The QB loader forensic ring is intentionally expensive: it
                ' captures every retired instruction.  Never carry it into an
                ' unrelated child program merely because DOS used AH=4Bh.
                EndDiagnosticQbExecTrace()
            End If
            Return
        End If

        If Not _diagnosticQbExecTraceEnabledInBed Then Return
        If vectorInBed = &H13 Then TraceDiagnosticQbInt13EntryInBed()
    End Sub

    Private Shared Function IsDiagnosticQbExecTargetInBed(programPathInBed As String) As Boolean
        If String.IsNullOrWhiteSpace(programPathInBed) Then Return False

        Dim normalizedInBed As String = programPathInBed.Replace("/"c, "\"c)
        Dim separatorInBed As Integer = normalizedInBed.LastIndexOf("\"c)
        Dim fileNameInBed As String =
            If(separatorInBed >= 0,
               normalizedInBed.Substring(separatorInBed + 1),
               normalizedInBed)
        fileNameInBed = fileNameInBed.Trim().ToUpperInvariant()

        Return fileNameInBed = "QB.EXE" OrElse
               fileNameInBed = "QBASIC.EXE" OrElse
               fileNameInBed = "QUICKBASIC.EXE"
    End Function

    Private Sub TraceDiagnosticQbInt13EntryInBed()
        _diagnosticQbInt13SequenceInBed += 1UL

        Dim pendingInBed As New DiagnosticQbInt13PendingInBed With {
            .Sequence = _diagnosticQbInt13SequenceInBed,
            .ReturnCs = CS,
            .ReturnIp = IP,
            .ReturnSs = SS,
            .ReturnSp = SP,
            .FunctionAh = AH,
            .RequestedCount = AL,
            .Cylinder = CInt(CH) Or ((CInt(CL) And &HC0) << 2),
            .Head = DH,
            .Sector = CByte(CL And &H3F),
            .Drive = DL,
            .BufferEs = ES,
            .BufferBx = BX
        }

        pendingInBed.PhysicalStart = CULng(PhysicalRaw(ES, BX))
        Dim byteCountInBed As ULong = CULng(AL) * 512UL
        pendingInBed.PhysicalEnd = If(byteCountInBed = 0UL,
                                     pendingInBed.PhysicalStart,
                                     pendingInBed.PhysicalStart + byteCountInBed - 1UL)
        _diagnosticQbPendingInt13InBed.Add(pendingInBed)

        AppendDiagnosticQbEventInBed(
            "INT13 #" & pendingInBed.Sequence.ToString("D4") &
            " ENTRY AH=" & pendingInBed.FunctionAh.ToString("X2") &
            " AL=" & pendingInBed.RequestedCount.ToString("X2") &
            " req=" & CInt(pendingInBed.RequestedCount).ToString() & " sector(s)" &
            " CHS=" & pendingInBed.Cylinder.ToString() & "/" & pendingInBed.Head.ToString() & "/" & pendingInBed.Sector.ToString() &
            " DL=" & pendingInBed.Drive.ToString("X2") &
            " ES:BX=" & pendingInBed.BufferEs.ToString("X4") & ":" & pendingInBed.BufferBx.ToString("X4") &
            " phys=" & pendingInBed.PhysicalStart.ToString("X5") & "-" & pendingInBed.PhysicalEnd.ToString("X5") &
            " return=" & pendingInBed.ReturnCs.ToString("X4") & ":" & pendingInBed.ReturnIp.ToString("X4") &
            " SS:SP=" & pendingInBed.ReturnSs.ToString("X4") & ":" & pendingInBed.ReturnSp.ToString("X4"))
    End Sub

    Private Sub TraceDiagnosticQbInt13ReturnIfAnyInBed()
        If Not _diagnosticQbExecTraceEnabledInBed OrElse _diagnosticQbPendingInt13InBed.Count = 0 Then Return

        For indexInBed As Integer = _diagnosticQbPendingInt13InBed.Count - 1 To 0 Step -1
            Dim pendingInBed As DiagnosticQbInt13PendingInBed = _diagnosticQbPendingInt13InBed(indexInBed)
            If CS <> pendingInBed.ReturnCs OrElse IP <> pendingInBed.ReturnIp OrElse
               SS <> pendingInBed.ReturnSs OrElse SP <> pendingInBed.ReturnSp Then
                Continue For
            End If

            AppendDiagnosticQbEventInBed(
                "INT13 #" & pendingInBed.Sequence.ToString("D4") &
                " RETURN CF=" & If(Flag(CF), "1", "0") &
                " AH=" & AH.ToString("X2") &
                " AL=" & AL.ToString("X2") &
                " requested=" & CInt(pendingInBed.RequestedCount).ToString() &
                " next=" & CS.ToString("X4") & ":" & IP.ToString("X4") &
                " FL=" & Flags.ToString("X4"))
            _diagnosticQbPendingInt13InBed.RemoveAt(indexInBed)
            Exit For
        Next
    End Sub

    Private Function DiagnosticQbCodeBytesInBed(codeCsInBed As UInt16, codeIpInBed As UInt16, protectedModeInBed As Boolean) As String
        If protectedModeInBed Then Return "<protected-mode bytes suppressed>"
        Dim bytesInBed As New System.Text.StringBuilder()
        For indexInBed As Integer = 0 To 7
            If indexInBed <> 0 Then bytesInBed.Append(" "c)
            Dim currentIpInBed As UInt16 = CUShort((CInt(codeIpInBed) + indexInBed) And &HFFFF)
            bytesInBed.Append(ReadByte(PhysicalRaw(codeCsInBed, currentIpInBed)).ToString("X2"))
        Next
        Return bytesInBed.ToString()
    End Function

    Private Sub TraceDiagnosticQbInstructionInBed()
        If Not _diagnosticQbExecTraceEnabledInBed Then Return
        _diagnosticQbInstructionSequenceInBed += 1UL

        Dim sampleInBed As New DiagnosticQbInstructionSampleInBed With {
            .Sequence = _diagnosticQbInstructionSequenceInBed,
            .Cs = CS,
            .Ip = IP,
            .Ax = AX,
            .Bx = BX,
            .Cx = CX,
            .Dx = DX,
            .Si = SI,
            .Di = DI,
            .Bp = BP,
            .Ds = DS,
            .Es = ES,
            .Ss = SS,
            .Sp = SP,
            .Flags = Flags,
            .WasProtectedMode = ProtectedMode
        }
        _diagnosticQbInstructionRingInBed(_diagnosticQbInstructionWriteIndexInBed) = sampleInBed
        _diagnosticQbInstructionWriteIndexInBed = (_diagnosticQbInstructionWriteIndexInBed + 1) Mod DiagnosticQbInstructionCapacityInBed
        If _diagnosticQbInstructionCountInBed < DiagnosticQbInstructionCapacityInBed Then
            _diagnosticQbInstructionCountInBed += 1
        End If
    End Sub

    Private Sub TraceDiagnosticQbStepEntryInBed()
        If Not _diagnosticQbExecTraceEnabledInBed Then Return
        TraceDiagnosticQbInt13ReturnIfAnyInBed()
        TraceDiagnosticQbInstructionInBed()
    End Sub

    Private Sub FreezeDiagnosticQbExecTraceInBed(reasonInBed As String)
        If Not _diagnosticQbExecTraceEnabledInBed Then Return
        _diagnosticQbTerminalReasonInBed = reasonInBed
        AppendDiagnosticQbEventInBed("TRACE FREEZE: " & reasonInBed &
                                     " at " & CS.ToString("X4") & ":" & IP.ToString("X4"))
        _diagnosticQbExecTraceEnabledInBed = False
        Debug.Print(GetDiagnosticQbExecTrace())
    End Sub

    Public Function GetDiagnosticQbExecTrace() As String
        Dim reportInBed As New System.Text.StringBuilder()
        reportInBed.AppendLine("Cromwell Technologies QB EXEC forensic trace")
        reportInBed.AppendLine("Auto-arm source: DOS INT 21h / AH=4Bh EXEC")
        reportInBed.AppendLine("Trace enabled: " & If(_diagnosticQbExecTraceEnabledInBed, "yes", "no"))
        reportInBed.AppendLine("EXEC program: " & If(String.IsNullOrEmpty(_diagnosticQbExecProgramInBed), "<none captured>", _diagnosticQbExecProgramInBed))
        reportInBed.AppendLine("Terminal reason: " & If(String.IsNullOrEmpty(_diagnosticQbTerminalReasonInBed), "<none>", _diagnosticQbTerminalReasonInBed))
        reportInBed.AppendLine("INT13 calls started: " & _diagnosticQbInt13SequenceInBed.ToString("N0"))
        reportInBed.AppendLine("INT13 calls still pending: " & _diagnosticQbPendingInt13InBed.Count.ToString("N0"))
        reportInBed.AppendLine("Instruction samples executed since EXEC: " & _diagnosticQbInstructionSequenceInBed.ToString("N0"))
        reportInBed.AppendLine("Instruction ring capacity: " & DiagnosticQbInstructionCapacityInBed.ToString("N0"))
        reportInBed.AppendLine()
        reportInBed.AppendLine("--- QB loader / INT13 events ---")
        If _diagnosticQbEventTraceInBed.Count = 0 Then
            reportInBed.AppendLine("<none>")
        Else
            For Each lineInBed As String In _diagnosticQbEventTraceInBed
                reportInBed.AppendLine(lineInBed)
            Next
        End If

        If _diagnosticQbPendingInt13InBed.Count <> 0 Then
            reportInBed.AppendLine()
            reportInBed.AppendLine("--- pending INT13 calls which never returned ---")
            For Each pendingInBed As DiagnosticQbInt13PendingInBed In _diagnosticQbPendingInt13InBed
                reportInBed.AppendLine(
                    "INT13 #" & pendingInBed.Sequence.ToString("D4") &
                    " AH=" & pendingInBed.FunctionAh.ToString("X2") &
                    " req=" & CInt(pendingInBed.RequestedCount).ToString() &
                    " CHS=" & pendingInBed.Cylinder.ToString() & "/" & pendingInBed.Head.ToString() & "/" & pendingInBed.Sector.ToString() &
                    " ES:BX=" & pendingInBed.BufferEs.ToString("X4") & ":" & pendingInBed.BufferBx.ToString("X4") &
                    " expected-return=" & pendingInBed.ReturnCs.ToString("X4") & ":" & pendingInBed.ReturnIp.ToString("X4"))
            Next
        End If

        reportInBed.AppendLine()
        AppendDiagnosticQbWriteWatchReportInBed(reportInBed)
        reportInBed.AppendLine("--- first CS=FFFF transition ---")
        If _diagnosticQbFirstCsFFFFTransitionInBed.Length = 0 Then
            reportInBed.AppendLine("<none observed>")
        Else
            reportInBed.AppendLine(_diagnosticQbFirstCsFFFFTransitionInBed)
        End If
        reportInBed.AppendLine()
        reportInBed.AppendLine("--- last 256 CPU instruction boundaries ---")
        If _diagnosticQbInstructionCountInBed = 0 Then
            reportInBed.AppendLine("<none>")
        Else
            Dim oldestIndexInBed As Integer = If(_diagnosticQbInstructionCountInBed = DiagnosticQbInstructionCapacityInBed,
                                                  _diagnosticQbInstructionWriteIndexInBed,
                                                  0)
            For offsetInBed As Integer = 0 To _diagnosticQbInstructionCountInBed - 1
                Dim sampleIndexInBed As Integer = (oldestIndexInBed + offsetInBed) Mod DiagnosticQbInstructionCapacityInBed
                Dim sampleInBed As DiagnosticQbInstructionSampleInBed = _diagnosticQbInstructionRingInBed(sampleIndexInBed)
                reportInBed.AppendLine(
                    "#" & sampleInBed.Sequence.ToString("D8") &
                    " " & sampleInBed.Cs.ToString("X4") & ":" & sampleInBed.Ip.ToString("X4") &
                    "  " & DiagnosticQbCodeBytesInBed(sampleInBed.Cs, sampleInBed.Ip, sampleInBed.WasProtectedMode) &
                    "  AX=" & sampleInBed.Ax.ToString("X4") &
                    " BX=" & sampleInBed.Bx.ToString("X4") &
                    " CX=" & sampleInBed.Cx.ToString("X4") &
                    " DX=" & sampleInBed.Dx.ToString("X4") &
                    " SI=" & sampleInBed.Si.ToString("X4") &
                    " DI=" & sampleInBed.Di.ToString("X4") &
                    " BP=" & sampleInBed.Bp.ToString("X4") &
                    " DS=" & sampleInBed.Ds.ToString("X4") &
                    " ES=" & sampleInBed.Es.ToString("X4") &
                    " SS:SP=" & sampleInBed.Ss.ToString("X4") & ":" & sampleInBed.Sp.ToString("X4") &
                    " FL=" & sampleInBed.Flags.ToString("X4"))
            Next
        End If

        reportInBed.AppendLine()
        reportInBed.AppendLine("--- current CPU snapshot ---")
        reportInBed.AppendLine(
            "CS:IP=" & CS.ToString("X4") & ":" & IP.ToString("X4") &
            " AX=" & AX.ToString("X4") & " BX=" & BX.ToString("X4") &
            " CX=" & CX.ToString("X4") & " DX=" & DX.ToString("X4") &
            " SI=" & SI.ToString("X4") & " DI=" & DI.ToString("X4") &
            " BP=" & BP.ToString("X4") &
            " DS=" & DS.ToString("X4") & " ES=" & ES.ToString("X4") &
            " SS:SP=" & SS.ToString("X4") & ":" & SP.ToString("X4") &
            " FL=" & Flags.ToString("X4") &
            " halted=" & If(Halted, "yes", "no"))
        If Not ProtectedMode Then
            reportInBed.AppendLine("next bytes: " & DiagnosticQbCodeBytesInBed(CS, IP, ProtectedMode))
        End If
        Return reportInBed.ToString()
    End Function

    Public Sub BeginDiagnosticImportantIntTrace(Optional includeFullForensicInBed As Boolean = True)
        _diagnosticImportantIntTrace.Clear()
        _diagnosticDpmiTrace.Clear()
        _diagnosticDosFileTraceInBed.Clear()
        _diagnosticDosFilePendingInBed.Clear()
        Array.Clear(_diagnosticImportantIntCounts, 0, _diagnosticImportantIntCounts.Length)
        _diagnosticImportantIntTraceSequence = 0UL
        _diagnosticImportantIntCallCount = 0UL
        _diagnosticDpmiTraceSequence = 0UL
        _diagnosticDosFileTraceSequenceInBed = 0UL
        _diagnosticDpmiExceptionReturnPending = False
        _diagnosticImportantIntTraceEnabled = True
        If includeFullForensicInBed Then BeginForensicTraceInBed()
        TraceDiagnosticImportantInt("TRACE BEGIN")
    End Sub

    Public Sub EndDiagnosticImportantIntTrace()
        If _diagnosticImportantIntTraceEnabled Then
            TraceDiagnosticImportantInt("TRACE END")
        End If
        _diagnosticImportantIntTraceEnabled = False
        EndForensicTraceInBed("trace stopped")
    End Sub

    Public ReadOnly Property DiagnosticImportantIntTraceEnabled As Boolean
        Get
            Return _diagnosticImportantIntTraceEnabled
        End Get
    End Property

    Public Function GetDiagnosticImportantIntTrace() As String
        Dim reportInBed As New System.Text.StringBuilder()
        reportInBed.AppendLine("Cromwell Technologies Important INTn forensic trace")
        reportInBed.AppendLine("Trace enabled: " & If(_diagnosticImportantIntTraceEnabled, "yes", "no"))
        reportInBed.AppendLine("Binary forensic trace: " &
                               If(_forensicTracePathInBed.Length = 0, "not started", _forensicTracePathInBed))
        reportInBed.AppendLine("Forensic instructions/events: " &
                               _forensicTraceInstructionCountInBed.ToString("N0") & " / " &
                               _forensicTraceEventCountInBed.ToString("N0"))
        If _forensicTraceTerminalReasonInBed.Length > 0 Then
            reportInBed.AppendLine("Forensic terminal reason: " & _forensicTraceTerminalReasonInBed)
        End If
        reportInBed.AppendLine("Captured software INT calls: " & _diagnosticImportantIntCallCount.ToString("N0"))
        reportInBed.AppendLine("Capacity: " & DiagnosticImportantIntTraceCapacity.ToString("N0") &
                               " rolling entries")
        reportInBed.AppendLine()
        reportInBed.AppendLine("--- captured vector totals ---")

        Dim anyInBed As Boolean
        For vectorInBed As Integer = 0 To 255
            Dim countInBed As ULong = _diagnosticImportantIntCounts(vectorInBed)
            If countInBed = 0UL Then Continue For
            anyInBed = True
            reportInBed.AppendLine(
                "INT " & vectorInBed.ToString("X2") & "h  " &
                DiagnosticImportantInterruptName(CByte(vectorInBed)).PadRight(22) &
                " " & countInBed.ToString("N0"))
        Next
        If Not anyInBed Then reportInBed.AppendLine("(none)")

        reportInBed.AppendLine()
        reportInBed.AppendLine("--- chronological entry trace ---")
        If _diagnosticImportantIntTrace.Count = 0 Then
            reportInBed.AppendLine("(trace empty)")
        Else
            For Each lineInBed As String In _diagnosticImportantIntTrace
                reportInBed.AppendLine(lineInBed)
            Next
        End If
        reportInBed.AppendLine()
        reportInBed.AppendLine("--- preserved DPMI control-transfer trace ---")
        reportInBed.AppendLine("Capacity: " & DiagnosticDpmiTraceCapacity.ToString("N0") &
                               " entries; unaffected by later DOS/BIOS polling")
        If _diagnosticDpmiTrace.Count = 0 Then
            reportInBed.AppendLine("(trace empty)")
        Else
            For Each lineInBed As String In _diagnosticDpmiTrace
                reportInBed.AppendLine(lineInBed)
            Next
        End If
        reportInBed.AppendLine()
        reportInBed.AppendLine("--- preserved DOS file-service entry/return trace ---")
        reportInBed.AppendLine("Capacity: " & DiagnosticDosFileTraceCapacityInBed.ToString("N0") &
                               " entries; unaffected by later INT 16h polling")
        reportInBed.AppendLine("ENTRY records the request; RETURN records DOS's actual CF/AX result.")
        If _diagnosticDosFileTraceInBed.Count = 0 Then
            reportInBed.AppendLine("(trace empty)")
        Else
            For Each lineInBed As String In _diagnosticDosFileTraceInBed
                reportInBed.AppendLine(lineInBed)
            Next
        End If
        Return reportInBed.ToString()
    End Function

    Private Shared Function IsDiagnosticDosFileServiceInBed(functionAhInBed As Byte) As Boolean
        Select Case functionAhInBed
            Case &H3C, &H3D, &H3E, &H3F, &H40, &H41, &H42, &H43,
                 &H4B, &H4C, &H4E, &H4F, &H56, &H57, &H59
                Return True
            Case Else
                Return False
        End Select
    End Function

    Private Sub AppendDiagnosticDosFileTraceInBed(messageInBed As String)
        _diagnosticDosFileTraceSequenceInBed += 1UL
        Dim lineInBed As String =
            "#F" & _diagnosticDosFileTraceSequenceInBed.ToString("D6") & " " & messageInBed
        _diagnosticDosFileTraceInBed.Enqueue(lineInBed)
        While _diagnosticDosFileTraceInBed.Count > DiagnosticDosFileTraceCapacityInBed
            _diagnosticDosFileTraceInBed.Dequeue()
        End While
        If _forensicTraceWriterInBed IsNot Nothing Then
            WriteForensicEventInBed("DOS FILE " & lineInBed)
        End If
    End Sub

    Private Function ReadDiagnosticGuestAsciizNoBusInBed(segmentInBed As UInt16,
                                                           offsetInBed As UInt16) As String
        Dim baseInBed As UInteger
        Dim limitInBed As UInt16
        If ProtectedMode Then
            ' The pathname passed to DOS is normally DS:DX.  Use the already
            ' validated hidden segment cache instead of issuing diagnostic bus
            ' reads or re-reading an LDT descriptor.  This observes exactly the
            ' linear bytes the caller selected without changing READY timing.
            If segmentInBed = DS Then
                baseInBed = _segmentBases(3)
                limitInBed = _segmentLimits(3)
            ElseIf segmentInBed = ES Then
                baseInBed = _segmentBases(0)
                limitInBed = _segmentLimits(0)
            ElseIf segmentInBed = SS Then
                baseInBed = _segmentBases(2)
                limitInBed = _segmentLimits(2)
            ElseIf segmentInBed = CS Then
                baseInBed = _segmentBases(1)
                limitInBed = _segmentLimits(1)
            Else
                Return "<uncached protected selector " & segmentInBed.ToString("X4") & ">"
            End If
        Else
            baseInBed = CUInt(segmentInBed) << 4
            limitInBed = &HFFFFUS
        End If

        Dim textInBed As New System.Text.StringBuilder()
        For indexInBed As Integer = 0 To 127
            Dim currentOffsetInBed As UInt16 =
                CUShort((CInt(offsetInBed) + indexInBed) And &HFFFF)
            If ProtectedMode AndAlso currentOffsetInBed > limitInBed Then Exit For
            Dim valueInBed As Byte
            If Not DiagnosticQbTryPeekLowRamByteInBed(
                    (baseInBed + CUInt(currentOffsetInBed)) And &HFFFFFFUI, valueInBed) Then
                If textInBed.Length = 0 Then Return "<not low DRAM>"
                Exit For
            End If
            If valueInBed = 0 Then Exit For
            If valueInBed >= &H20 AndAlso valueInBed <= &H7E Then
                textInBed.Append(ChrW(valueInBed))
            Else
                textInBed.Append("."c)
            End If
        Next
        Return textInBed.ToString()
    End Function

    Private Sub TraceDiagnosticDosFileServiceEntryInBed(vectorInBed As Byte)
        If Not _diagnosticImportantIntTraceEnabled Then Return
        Dim redirectorInBed As Boolean =
            vectorInBed = &H2F AndAlso (AX And &HFF00US) = &H1100US
        If vectorInBed <> &H21 AndAlso Not redirectorInBed Then Return
        Dim functionAhInBed As Byte = AH
        If vectorInBed = &H21 AndAlso Not IsDiagnosticDosFileServiceInBed(functionAhInBed) Then Return

        Dim detailInBed As String
        If redirectorInBed Then
            detailInBed = "INT 2F redirector AX=" & AX.ToString("X4") &
                          " " & DiagnosticRedirectorServiceNameInBed(AL)
            detailInBed &= " DS:DX?=""" & ReadDiagnosticGuestAsciizNoBusInBed(DS, DX) & """"
            detailInBed &= " DS:SI?=""" & ReadDiagnosticGuestAsciizNoBusInBed(DS, SI) & """"
            detailInBed &= " ES:DI?=""" & ReadDiagnosticGuestAsciizNoBusInBed(ES, DI) & """"
        Else
            detailInBed = DiagnosticImportantInterruptService(&H21)
            Select Case functionAhInBed
                Case &H3C, &H3D, &H41, &H43, &H4B, &H4E, &H56
                    detailInBed &= " path=""" & ReadDiagnosticGuestAsciizNoBusInBed(DS, DX) & """"
            End Select
        End If

        Dim pendingInBed As New DiagnosticDosFileCallPendingInBed With {
            .Sequence = _diagnosticDosFileTraceSequenceInBed + 1UL,
            .Vector = vectorInBed,
            .FunctionAx = AX,
            .ReturnCs = CS,
            .ReturnIp = IP,
            .FunctionAh = functionAhInBed,
            .FunctionAl = AL,
            .CallerCs = _instructionStartCs,
            .CallerIp = _instructionStartIp,
            .Bx = BX,
            .Cx = CX,
            .Dx = DX,
            .Ds = DS,
            .Es = ES,
            .WasProtectedMode = ProtectedMode,
            .EntryDetail = detailInBed
        }
        AppendDiagnosticDosFileTraceInBed(
            "ENTRY " & detailInBed &
            " caller=" & pendingInBed.CallerCs.ToString("X4") & ":" & pendingInBed.CallerIp.ToString("X4") &
            " return=" & pendingInBed.ReturnCs.ToString("X4") & ":" & pendingInBed.ReturnIp.ToString("X4") &
            " AX=" & AX.ToString("X4") & " BX=" & BX.ToString("X4") &
            " CX=" & CX.ToString("X4") & " DX=" & DX.ToString("X4") &
            " DS=" & DS.ToString("X4") & " ES=" & ES.ToString("X4"))

        ' AH=4Ch terminates the process and has no architectural return site.
        If vectorInBed <> &H21 OrElse functionAhInBed <> &H4C Then
            _diagnosticDosFilePendingInBed.Add(pendingInBed)
        End If
    End Sub

    Private Shared Function DiagnosticRedirectorServiceNameInBed(functionAlInBed As Byte) As String
        Select Case functionAlInBed
            Case &H1 : Return "remove directory"
            Case &H3 : Return "make directory"
            Case &H5 : Return "change directory"
            Case &H6 : Return "close"
            Case &H7 : Return "commit"
            Case &H8 : Return "read"
            Case &H9 : Return "write"
            Case &HA : Return "lock"
            Case &HB : Return "unlock"
            Case &HC : Return "disk information"
            Case &HE : Return "set attributes"
            Case &HF : Return "get attributes"
            Case &H11 : Return "rename"
            Case &H13 : Return "delete"
            Case &H16 : Return "open"
            Case &H17 : Return "create/truncate"
            Case &H18 : Return "create new"
            Case &H19 : Return "find first"
            Case &H1B : Return "find next"
            Case &H21 : Return "seek"
            Case &H23 : Return "qualify pathname"
            Case &H2E : Return "extended open/create"
            Case Else : Return "service"
        End Select
    End Function

    Private Sub TraceDiagnosticDosFileReturnInBed()
        If Not _diagnosticImportantIntTraceEnabled OrElse
           _diagnosticDosFilePendingInBed.Count = 0 Then Return

        For indexInBed As Integer = _diagnosticDosFilePendingInBed.Count - 1 To 0 Step -1
            Dim pendingInBed As DiagnosticDosFileCallPendingInBed =
                _diagnosticDosFilePendingInBed(indexInBed)
            If pendingInBed.WasProtectedMode <> ProtectedMode OrElse
               pendingInBed.ReturnCs <> CS OrElse pendingInBed.ReturnIp <> IP Then Continue For

            Dim resultInBed As String
            If Flag(CF) Then
                resultInBed = "ERROR CF=1 AX=" & AX.ToString("X4")
            Else
                Select Case pendingInBed.FunctionAh
                    Case &H3C, &H3D
                        resultInBed = "OK CF=0 handle=" & AX.ToString("X4")
                    Case &H3F, &H40
                        resultInBed = "OK CF=0 count=" & AX.ToString("X4")
                    Case &H42
                        resultInBed = "OK CF=0 position=" & DX.ToString("X4") & ":" & AX.ToString("X4")
                    Case Else
                        resultInBed = "OK CF=0 AX=" & AX.ToString("X4")
                End Select
            End If
            AppendDiagnosticDosFileTraceInBed(
                "RETURN " & pendingInBed.EntryDetail &
                " to=" & CS.ToString("X4") & ":" & IP.ToString("X4") &
                " " & resultInBed & " BX=" & BX.ToString("X4") &
                " CX=" & CX.ToString("X4") & " DX=" & DX.ToString("X4") &
                " FL=" & Flags.ToString("X4"))
            _diagnosticDosFilePendingInBed.RemoveAt(indexInBed)
            Exit For
        Next
    End Sub

    Private Sub TraceDiagnosticImportantSoftwareInterrupt(vectorInBed As Byte)
        ' Windows 3.x Standard Mode uses INT 31h as its DPMI boundary.  Arm the
        ' bounded trace automatically at the first protected-mode call so a
        ' normal one-click CPU dump captures the failure without a separate
        ' diagnostic-menu procedure.
        If ProtectedMode AndAlso vectorInBed = &H31 AndAlso Not _diagnosticImportantIntTraceEnabled Then
            BeginDiagnosticImportantIntTrace(False)
        End If
        If Not _diagnosticImportantIntTraceEnabled Then Return
        If Not IsDiagnosticImportantInterrupt(vectorInBed) Then Return
        ' Windows 3.x uses AX=1689h as an extremely hot idle/yield boundary.
        ' It carries no file-operation evidence and can occur millions of times
        ' during one desktop session.  Do not format or churn the bounded trace
        ' for this known poll; other INT 2Fh services remain visible.
        If vectorInBed = &H2F AndAlso AX = &H1689US Then Return

        _diagnosticImportantIntCallCount += 1UL
        _diagnosticImportantIntCounts(CInt(vectorInBed)) += 1UL

        Dim targetInBed As String
        If ProtectedMode Then
            targetInBed = "IDT[" & vectorInBed.ToString("X2") & "h]"
        Else
            Dim vectorAddressInBed As UInteger = CUInt(vectorInBed) * 4UI
            Dim targetOffsetInBed As UInt16 = ReadWord(vectorAddressInBed)
            Dim targetSegmentInBed As UInt16 = ReadWord(vectorAddressInBed + 2UI)
            targetInBed = targetSegmentInBed.ToString("X4") & ":" &
                          targetOffsetInBed.ToString("X4")
        End If

        Dim callerCsInBed As UInt16 = _instructionStartCs
        Dim callerIpInBed As UInt16 = _instructionStartIp
        Dim serviceInBed As String = DiagnosticImportantInterruptService(vectorInBed)

        Dim entryInBed As String =
            "INT " & vectorInBed.ToString("X2") & "h " &
            DiagnosticImportantInterruptName(vectorInBed) &
            If(serviceInBed.Length > 0, " / " & serviceInBed, String.Empty) &
            "  caller=" & callerCsInBed.ToString("X4") & ":" & callerIpInBed.ToString("X4") &
            " next=" & CS.ToString("X4") & ":" & IP.ToString("X4") &
            " -> " & targetInBed &
            "  AX=" & AX.ToString("X4") &
            " BX=" & BX.ToString("X4") &
            " CX=" & CX.ToString("X4") &
            " DX=" & DX.ToString("X4") &
            " SI=" & SI.ToString("X4") &
            " DI=" & DI.ToString("X4") &
            " BP=" & BP.ToString("X4") &
            " DS=" & DS.ToString("X4") &
            " ES=" & ES.ToString("X4") &
            " SS:SP=" & SS.ToString("X4") & ":" & SP.ToString("X4") &
            " FL=" & Flags.ToString("X4")
        TraceDiagnosticImportantInt(entryInBed)
        If ProtectedMode AndAlso vectorInBed = &H31 Then
            TraceDiagnosticDpmi(entryInBed)
        End If
    End Sub

    Private Sub TraceDiagnosticImportantInt(messageInBed As String)
        If Not _diagnosticImportantIntTraceEnabled Then Return
        _diagnosticImportantIntTraceSequence += 1UL
        While _diagnosticImportantIntTrace.Count >= DiagnosticImportantIntTraceCapacity
            _diagnosticImportantIntTrace.Dequeue()
        End While
        _diagnosticImportantIntTrace.Enqueue(
            "#" & _diagnosticImportantIntTraceSequence.ToString("000000") &
            " " & messageInBed)
    End Sub

    Private Shared Function IsDiagnosticImportantInterrupt(vectorInBed As Byte) As Boolean
        Select Case vectorInBed
            Case &H10, &H11, &H12, &H13, &H14, &H15, &H16, &H17, &H1A,
                 &H20, &H21, &H25, &H26, &H27, &H2F, &H31, &H33, &H67
                Return True
            Case Else
                Return False
        End Select
    End Function

    Private Shared Function DiagnosticImportantInterruptName(vectorInBed As Byte) As String
        Select Case vectorInBed
            Case &H10 : Return "Video BIOS"
            Case &H11 : Return "Equipment list"
            Case &H12 : Return "Conventional memory"
            Case &H13 : Return "Disk BIOS"
            Case &H14 : Return "Serial BIOS"
            Case &H15 : Return "System BIOS"
            Case &H16 : Return "Keyboard BIOS"
            Case &H17 : Return "Printer BIOS"
            Case &H1A : Return "Time/date BIOS"
            Case &H20 : Return "DOS terminate"
            Case &H21 : Return "DOS services"
            Case &H25 : Return "DOS absolute read"
            Case &H26 : Return "DOS absolute write"
            Case &H27 : Return "DOS TSR"
            Case &H2F : Return "Multiplex"
            Case &H31 : Return "DPMI"
            Case &H33 : Return "Mouse"
            Case &H67 : Return "EMS"
            Case Else : Return "software interrupt"
        End Select
    End Function

    Private Function DiagnosticImportantInterruptService(vectorInBed As Byte) As String
        Select Case vectorInBed
            Case &H10
                Select Case AH
                    Case &H0 : Return "AH=00 set video mode AL=" & AL.ToString("X2")
                    Case &H1 : Return "AH=01 set cursor shape"
                    Case &H2 : Return "AH=02 set cursor position"
                    Case &H3 : Return "AH=03 read cursor position"
                    Case &H5 : Return "AH=05 select display page"
                    Case &H6 : Return "AH=06 scroll window up"
                    Case &H7 : Return "AH=07 scroll window down"
                    Case &H8 : Return "AH=08 read character/attribute"
                    Case &H9 : Return "AH=09 write character/attribute"
                    Case &HA : Return "AH=0A write character"
                    Case &HC : Return "AH=0C write pixel"
                    Case &HD : Return "AH=0D read pixel"
                    Case &HE : Return "AH=0E teletype"
                    Case &HF : Return "AH=0F get video mode"
                    Case &H10 : Return "AH=10 palette/DAC AL=" & AL.ToString("X2")
                    Case &H11 : Return "AH=11 character generator AL=" & AL.ToString("X2")
                    Case &H12 : Return "AH=12 alternate select BL=" & BL.ToString("X2")
                    Case &H1A : Return "AH=1A display combination AL=" & AL.ToString("X2")
                    Case &H1B : Return "AH=1B functionality/state"
                    Case &H1C : Return "AH=1C video save/restore AL=" & AL.ToString("X2")
                    Case Else : Return "AH=" & AH.ToString("X2")
                End Select

            Case &H13
                Select Case AH
                    Case &H0 : Return "AH=00 reset disk system"
                    Case &H1 : Return "AH=01 get last status"
                    Case &H2 : Return "AH=02 read sectors"
                    Case &H3 : Return "AH=03 write sectors"
                    Case &H4 : Return "AH=04 verify sectors"
                    Case &H8 : Return "AH=08 get drive parameters"
                    Case &HC : Return "AH=0C seek"
                    Case &H10 : Return "AH=10 test ready"
                    Case &H15 : Return "AH=15 get drive type"
                    Case Else : Return "AH=" & AH.ToString("X2")
                End Select

            Case &H15
                Return "AH=" & AH.ToString("X2") & " AL=" & AL.ToString("X2")

            Case &H16
                Select Case AH
                    Case &H0 : Return "AH=00 read keystroke"
                    Case &H1 : Return "AH=01 check keystroke"
                    Case &H2 : Return "AH=02 get shift flags"
                    Case &H10 : Return "AH=10 extended read keystroke"
                    Case &H11 : Return "AH=11 extended check keystroke"
                    Case &H12 : Return "AH=12 extended shift flags"
                    Case Else : Return "AH=" & AH.ToString("X2")
                End Select

            Case &H1A
                Select Case AH
                    Case &H0 : Return "AH=00 get clock ticks"
                    Case &H1 : Return "AH=01 set clock ticks"
                    Case &H2 : Return "AH=02 get RTC time"
                    Case &H3 : Return "AH=03 set RTC time"
                    Case &H4 : Return "AH=04 get RTC date"
                    Case &H5 : Return "AH=05 set RTC date"
                    Case Else : Return "AH=" & AH.ToString("X2")
                End Select

            Case &H21
                Select Case AH
                    Case &H0 : Return "AH=00 terminate"
                    Case &H1 : Return "AH=01 console input/echo"
                    Case &H2 : Return "AH=02 console output"
                    Case &H6 : Return "AH=06 direct console I/O"
                    Case &H7 : Return "AH=07 console input"
                    Case &H8 : Return "AH=08 console input"
                    Case &H9 : Return "AH=09 display $ string"
                    Case &HA : Return "AH=0A buffered console input"
                    Case &HB : Return "AH=0B keyboard status"
                    Case &HC : Return "AH=0C flush/input"
                    Case &HD : Return "AH=0D disk reset"
                    Case &HE : Return "AH=0E select drive"
                    Case &H19 : Return "AH=19 get current drive"
                    Case &H1A : Return "AH=1A set DTA"
                    Case &H25 : Return "AH=25 set interrupt vector"
                    Case &H2A : Return "AH=2A get date"
                    Case &H2C : Return "AH=2C get time"
                    Case &H2F : Return "AH=2F get DTA"
                    Case &H30 : Return "AH=30 get DOS version"
                    Case &H31 : Return "AH=31 TSR"
                    Case &H35 : Return "AH=35 get interrupt vector"
                    Case &H36 : Return "AH=36 get free disk space"
                    Case &H3C : Return "AH=3C create file"
                    Case &H3D : Return "AH=3D open file"
                    Case &H3E : Return "AH=3E close file"
                    Case &H3F : Return "AH=3F read file/device"
                    Case &H40 : Return "AH=40 write file/device"
                    Case &H41 : Return "AH=41 delete file"
                    Case &H42 : Return "AH=42 seek"
                    Case &H43 : Return "AH=43 file attributes"
                    Case &H47 : Return "AH=47 get current directory"
                    Case &H48 : Return "AH=48 allocate memory"
                    Case &H49 : Return "AH=49 free memory"
                    Case &H4A : Return "AH=4A resize memory block"
                    Case &H4B : Return "AH=4B EXEC AL=" & AL.ToString("X2")
                    Case &H4C : Return "AH=4C terminate with code"
                    Case &H4E : Return "AH=4E find first"
                    Case &H4F : Return "AH=4F find next"
                    Case &H56 : Return "AH=56 rename"
                    Case &H57 : Return "AH=57 file date/time"
                    Case &H59 : Return "AH=59 extended error"
                    Case Else : Return "AH=" & AH.ToString("X2")
                End Select

            Case &H2F
                Return "AX=" & AX.ToString("X4") & " multiplex"

            Case &H33
                Return "AX=" & AX.ToString("X4") & " mouse"

            Case &H67
                Return "AH=" & AH.ToString("X2") & " EMS"

            Case Else
                Return "AX=" & AX.ToString("X4")
        End Select
    End Function

    Public Sub BeginDiagnosticBiosKeyboardTrace()
        _diagnosticBiosKeyboardTrace.Clear()
        _diagnosticBiosKeyboardTraceSequence = 0UL
        _diagnosticBiosKeyboardTraceEnabled = True
        TraceDiagnosticBiosKeyboard("TRACE BEGIN")
        TraceDiagnosticBiosKeyboard("STATE " & DiagnosticBiosKeyboardOneLine())
    End Sub

    Public Sub EndDiagnosticBiosKeyboardTrace()
        If _diagnosticBiosKeyboardTraceEnabled Then
            TraceDiagnosticBiosKeyboard("STATE " & DiagnosticBiosKeyboardOneLine())
            TraceDiagnosticBiosKeyboard("TRACE END")
        End If
        _diagnosticBiosKeyboardTraceEnabled = False
    End Sub

    Public ReadOnly Property DiagnosticBiosKeyboardTraceEnabled As Boolean
        Get
            Return _diagnosticBiosKeyboardTraceEnabled
        End Get
    End Property

    Public Function GetDiagnosticBiosKeyboardTrace() As String
        Dim reportInBed As New System.Text.StringBuilder()
        reportInBed.AppendLine("IBM AT BIOS keyboard ring forensic trace")
        reportInBed.AppendLine("Trace enabled: " & If(_diagnosticBiosKeyboardTraceEnabled, "yes", "no"))
        reportInBed.AppendLine()
        reportInBed.AppendLine(GetDiagnosticBiosKeyboardState())
        reportInBed.AppendLine()
        reportInBed.AppendLine("--- BDA/IVT write trace ---")
        If _diagnosticBiosKeyboardTrace.Count = 0 Then
            reportInBed.AppendLine("(trace empty)")
        Else
            For Each lineInBed As String In _diagnosticBiosKeyboardTrace
                reportInBed.AppendLine(lineInBed)
            Next
        End If
        Return reportInBed.ToString()
    End Function

    Public Function GetDiagnosticBiosKeyboardState() As String
        Dim reportInBed As New System.Text.StringBuilder()
        Dim int09OffsetInBed As UInt16 = DiagnosticPeekLowWord(&H24UI)
        Dim int09SegmentInBed As UInt16 = DiagnosticPeekLowWord(&H26UI)
        Dim int16OffsetInBed As UInt16 = DiagnosticPeekLowWord(&H58UI)
        Dim int16SegmentInBed As UInt16 = DiagnosticPeekLowWord(&H5AUI)
        Dim headInBed As UInt16 = DiagnosticPeekLowWord(&H41AUI)
        Dim tailInBed As UInt16 = DiagnosticPeekLowWord(&H41CUI)

        reportInBed.AppendLine("INT 09h vector: " & int09SegmentInBed.ToString("X4") & ":" & int09OffsetInBed.ToString("X4"))
        reportInBed.AppendLine("INT 16h vector: " & int16SegmentInBed.ToString("X4") & ":" & int16OffsetInBed.ToString("X4"))
        reportInBed.AppendLine("BDA shift flags 40:17 = " & _memoryControllerInBed.LowMemoryInBed(&H417).ToString("X2") & "h")
        reportInBed.AppendLine("BDA shift flags 40:18 = " & _memoryControllerInBed.LowMemoryInBed(&H418).ToString("X2") & "h")
        reportInBed.AppendLine("BDA keyboard flags 40:96 = " & _memoryControllerInBed.LowMemoryInBed(&H496).ToString("X2") & "h")
        reportInBed.AppendLine("Ring head = " & headInBed.ToString("X4") & "h   tail = " & tailInBed.ToString("X4") & "h")
        reportInBed.AppendLine("Active queue: " & DiagnosticFormatActiveKeyboardQueue(headInBed, tailInBed))
        reportInBed.AppendLine()
        reportInBed.AppendLine("Raw 16-slot BIOS ring (0040:001E-003D):")

        For slotIndexInBed As Integer = 0 To 15
            Dim offsetInBed As UInt16 = CUShort(&H1E + slotIndexInBed * 2)
            Dim physicalInBed As UInteger = &H400UI + offsetInBed
            Dim wordInBed As UInt16 = DiagnosticPeekLowWord(physicalInBed)
            Dim markerInBed As String = "  "
            If offsetInBed = headInBed AndAlso offsetInBed = tailInBed Then
                markerInBed = "HT"
            ElseIf offsetInBed = headInBed Then
                markerInBed = "H "
            ElseIf offsetInBed = tailInBed Then
                markerInBed = " T"
            End If
            reportInBed.AppendLine(markerInBed & " " & offsetInBed.ToString("X4") &
                                   ": AX=" & wordInBed.ToString("X4") &
                                   "  scan=" & CByte(wordInBed >> 8).ToString("X2") &
                                   " ascii=" & CByte(wordInBed And &HFFUS).ToString("X2") &
                                   " '" & DiagnosticPrintableAscii(CByte(wordInBed And &HFFUS)) & "'")
        Next

        Return reportInBed.ToString()
    End Function

    Private Function DiagnosticPeekLowWord(addressInBed As UInteger) As UInt16
        If addressInBed + 1UI >= CUInt(_memoryControllerInBed.LowMemoryInBed.Length) Then Return 0US
        Return CUShort(_memoryControllerInBed.LowMemoryInBed(CInt(addressInBed)) Or
                       (CUShort(_memoryControllerInBed.LowMemoryInBed(CInt(addressInBed + 1UI))) << 8))
    End Function

    Private Function DiagnosticPrintableAscii(valueInBed As Byte) As String
        If valueInBed >= &H20 AndAlso valueInBed <= &H7E Then Return ChrW(valueInBed).ToString()
        Select Case valueInBed
            Case &H0 : Return "NUL"
            Case &H8 : Return "BS"
            Case &H9 : Return "TAB"
            Case &HD : Return "CR"
            Case &H1B : Return "ESC"
            Case Else : Return "."
        End Select
    End Function

    Private Function DiagnosticFormatActiveKeyboardQueue(headInBed As UInt16, tailInBed As UInt16) As String
        If headInBed = tailInBed Then Return "(empty)"
        If headInBed < &H1EUS OrElse headInBed >= &H3EUS OrElse
           tailInBed < &H1EUS OrElse tailInBed >= &H3EUS OrElse
           ((headInBed - &H1EUS) And 1US) <> 0US OrElse
           ((tailInBed - &H1EUS) And 1US) <> 0US Then
            Return "(invalid ring pointers)"
        End If

        Dim queueInBed As New System.Text.StringBuilder()
        Dim cursorInBed As UInt16 = headInBed
        Dim safetyInBed As Integer = 0
        While cursorInBed <> tailInBed AndAlso safetyInBed < 16
            Dim wordInBed As UInt16 = DiagnosticPeekLowWord(&H400UI + cursorInBed)
            If queueInBed.Length > 0 Then queueInBed.Append(" | ")
            queueInBed.Append("AX=" & wordInBed.ToString("X4") &
                              " scan=" & CByte(wordInBed >> 8).ToString("X2") &
                              " ascii=" & CByte(wordInBed And &HFFUS).ToString("X2") &
                              " '" & DiagnosticPrintableAscii(CByte(wordInBed And &HFFUS)) & "'")
            cursorInBed = CUShort(cursorInBed + 2US)
            If cursorInBed >= &H3EUS Then cursorInBed = &H1EUS
            safetyInBed += 1
        End While
        Return queueInBed.ToString()
    End Function

    Private Function DiagnosticBiosKeyboardOneLine() As String
        Dim headInBed As UInt16 = DiagnosticPeekLowWord(&H41AUI)
        Dim tailInBed As UInt16 = DiagnosticPeekLowWord(&H41CUI)
        Return "INT09=" & DiagnosticPeekLowWord(&H26UI).ToString("X4") & ":" &
                          DiagnosticPeekLowWord(&H24UI).ToString("X4") &
               " INT16=" & DiagnosticPeekLowWord(&H5AUI).ToString("X4") & ":" &
                          DiagnosticPeekLowWord(&H58UI).ToString("X4") &
               " head=" & headInBed.ToString("X4") &
               " tail=" & tailInBed.ToString("X4") &
               " flags17=" & _memoryControllerInBed.LowMemoryInBed(&H417).ToString("X2") &
               " flags96=" & _memoryControllerInBed.LowMemoryInBed(&H496).ToString("X2")
    End Function

    Private Sub TraceDiagnosticBiosKeyboard(messageInBed As String)
        If Not _diagnosticBiosKeyboardTraceEnabled Then Return
        _diagnosticBiosKeyboardTraceSequence += 1UL
        While _diagnosticBiosKeyboardTrace.Count >= DiagnosticBiosKeyboardTraceCapacity
            _diagnosticBiosKeyboardTrace.Dequeue()
        End While
        _diagnosticBiosKeyboardTrace.Enqueue("#" & _diagnosticBiosKeyboardTraceSequence.ToString("000000") &
                                             " " & messageInBed)
    End Sub

    Private Sub TraceDiagnosticBiosKeyboardWrite(addressInBed As UInteger, valueInBed As Byte)
        If Not _diagnosticBiosKeyboardTraceEnabled Then Return

        Dim interestingInBed As Boolean =
            addressInBed = &H417UI OrElse
            addressInBed = &H418UI OrElse
            (addressInBed >= &H41AUI AndAlso addressInBed <= &H43DUI) OrElse
            addressInBed = &H496UI OrElse
            (addressInBed >= &H24UI AndAlso addressInBed <= &H27UI) OrElse
            (addressInBed >= &H58UI AndAlso addressInBed <= &H5BUI)

        If Not interestingInBed Then Return

        TraceDiagnosticBiosKeyboard("MEM[" & addressInBed.ToString("X5") & "] <- " &
                                    valueInBed.ToString("X2") & "   " &
                                    DiagnosticBiosKeyboardOneLine())
    End Sub

    Public Function ReadByte(address As UInteger) As Byte
        Dim dataPathSampleInBed As Boolean = _dataPathSampleActiveInBed
        Dim dataPathStampInBed As Long = 0
        Dim bridgeStampInBed As Long = 0
        If dataPathSampleInBed Then
            dataPathStampInBed = System.Diagnostics.Stopwatch.GetTimestamp()
            bridgeStampInBed = dataPathStampInBed
            _dataPathReadByteCallsInBed += 1UL
        End If

        Try
            If PortBus Is Nothing Then
                Throw New InvalidOperationException("Processor286 is not attached to its CPU local bus.")
            End If

            Dim targetInBed As AtMemoryCycleTarget286
            Dim valueInBed As Byte = PortBus.ReadMemoryByte(address, targetInBed)

            If dataPathSampleInBed AndAlso targetInBed = AtMemoryCycleTarget286.MappedDevice Then
                _dataPathBusProbeTicksInBed +=
                    System.Diagnostics.Stopwatch.GetTimestamp() - bridgeStampInBed
                _dataPathBusProbeCallsInBed += 1UL
                _dataPathBusHitsInBed += 1UL
            End If
            Return valueInBed
        Finally
            If dataPathSampleInBed Then
                _dataPathMemoryApiTicksInBed +=
                    System.Diagnostics.Stopwatch.GetTimestamp() - dataPathStampInBed
            End If
        End Try
    End Function

    Public Sub WriteByte(address As UInteger, value As Byte)
        Dim dataPathSampleInBed As Boolean = _dataPathSampleActiveInBed
        Dim dataPathStampInBed As Long = 0
        Dim bridgeStampInBed As Long = 0
        If dataPathSampleInBed Then
            dataPathStampInBed = System.Diagnostics.Stopwatch.GetTimestamp()
            bridgeStampInBed = dataPathStampInBed
            _dataPathWriteByteCallsInBed += 1UL
        End If

        Try
            If PortBus Is Nothing Then
                Throw New InvalidOperationException("Processor286 is not attached to its CPU local bus.")
            End If

            Dim targetInBed As AtMemoryCycleTarget286
            WriteForensicMemoryWriteInBed(address, 1, value)
            TraceDiagnosticQbWriteByteInBed(address, value)
            TraceDiagnosticSelectorWriteInBed(address, value, 1)
            TraceDiagnosticLdtWriteInBed(address, value, 1)
            PortBus.WriteMemoryByte(address, value, targetInBed)

            If dataPathSampleInBed AndAlso targetInBed = AtMemoryCycleTarget286.MappedDevice Then
                _dataPathBusProbeTicksInBed +=
                    System.Diagnostics.Stopwatch.GetTimestamp() - bridgeStampInBed
                _dataPathBusProbeCallsInBed += 1UL
                _dataPathBusHitsInBed += 1UL
            End If

            If targetInBed = AtMemoryCycleTarget286.LocalDram Then
                Dim normalizedInBed As UInteger =
                    _memoryControllerInBed.NormalizePhysicalAddress(address)
                If normalizedInBed < &H100000UI Then
                    TraceDiagnosticBiosKeyboardWrite(normalizedInBed, value)
                End If
            End If
        Finally
            If dataPathSampleInBed Then
                _dataPathMemoryApiTicksInBed +=
                    System.Diagnostics.Stopwatch.GetTimestamp() - dataPathStampInBed
            End If
        End Try
    End Sub

    Public Sub LoadSystemRom(data As Byte())
        If _memoryControllerInBed Is Nothing Then
            Throw New InvalidOperationException("Processor286 is not attached to a motherboard memory controller.")
        End If
        _memoryControllerInBed.LoadSystemRom(data)
        If MirrorLegacyMemory Then
            Buffer.BlockCopy(data, 0, VrMem, CInt(_memoryControllerInBed.RomStart), data.Length)
        End If
    End Sub

    Public Sub ImportLowMemory()
        If _memoryControllerInBed Is Nothing Then
            Throw New InvalidOperationException("Processor286 is not attached to a motherboard memory controller.")
        End If
        _memoryControllerInBed.ImportLegacyLowMemory(VrMem)
    End Sub

    <Runtime.CompilerServices.MethodImpl(Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)>
    Private Function NormalizePhysicalAddress(address As UInteger) As UInteger
        If _memoryControllerInBed Is Nothing Then
            Throw New InvalidOperationException("Processor286 is not attached to a motherboard memory controller.")
        End If
        Return _memoryControllerInBed.NormalizePhysicalAddress(address)
    End Function

    Public Function ReadWord(address As UInteger) As UInt16
        Dim dataPathSampleInBed As Boolean = _dataPathSampleActiveInBed
        Dim dataPathStampInBed As Long = 0
        If dataPathSampleInBed Then
            dataPathStampInBed = System.Diagnostics.Stopwatch.GetTimestamp()
            _dataPathWordReadCallsInBed += 1UL
        End If

        Try
            If PortBus Is Nothing Then
                Throw New InvalidOperationException("Processor286 is not attached to its CPU local bus.")
            End If

            Dim firstTargetInBed As AtMemoryCycleTarget286
            Dim secondTargetInBed As AtMemoryCycleTarget286
            Dim directWordInBed As Boolean
            Dim valueInBed As UInt16 =
                PortBus.ReadMemoryWord(address,
                                       firstTargetInBed,
                                       secondTargetInBed,
                                       directWordInBed)

            If dataPathSampleInBed AndAlso directWordInBed AndAlso
               (firstTargetInBed = AtMemoryCycleTarget286.LocalDram OrElse
                firstTargetInBed = AtMemoryCycleTarget286.SystemRom) Then
                _dataPathWordFastReadsInBed += 1UL
            End If

            Return valueInBed
        Finally
            If dataPathSampleInBed Then
                _dataPathWordApiTicksInBed +=
                    System.Diagnostics.Stopwatch.GetTimestamp() - dataPathStampInBed
            End If
        End Try
    End Function

    Public Sub WriteWord(address As UInteger, value As UInt16)
        Dim dataPathSampleInBed As Boolean = _dataPathSampleActiveInBed
        Dim dataPathStampInBed As Long = 0
        If dataPathSampleInBed Then
            dataPathStampInBed = System.Diagnostics.Stopwatch.GetTimestamp()
            _dataPathWordWriteCallsInBed += 1UL
        End If

        Try
            If PortBus Is Nothing Then
                Throw New InvalidOperationException("Processor286 is not attached to its CPU local bus.")
            End If

            Dim firstTargetInBed As AtMemoryCycleTarget286
            Dim secondTargetInBed As AtMemoryCycleTarget286
            Dim directWordInBed As Boolean
            WriteForensicMemoryWriteInBed(address, 2, value)
            TraceDiagnosticQbWriteWordInBed(address, value)
            TraceDiagnosticSelectorWriteInBed(address, value, 2)
            TraceDiagnosticLdtWriteInBed(address, value, 2)
            PortBus.WriteMemoryWord(address, value,
                                    firstTargetInBed,
                                    secondTargetInBed,
                                    directWordInBed)

            Dim firstInBed As UInteger =
                _memoryControllerInBed.NormalizePhysicalAddress(address)
            Dim secondInBed As UInteger =
                _memoryControllerInBed.NormalizePhysicalAddress(address + 1UI)

            If firstTargetInBed = AtMemoryCycleTarget286.LocalDram AndAlso
               firstInBed < &H100000UI Then
                TraceDiagnosticBiosKeyboardWrite(firstInBed, CByte(value And &HFFUS))
            End If
            If secondTargetInBed = AtMemoryCycleTarget286.LocalDram AndAlso
               secondInBed < &H100000UI Then
                TraceDiagnosticBiosKeyboardWrite(secondInBed, CByte(value >> 8))
            End If

            If dataPathSampleInBed AndAlso directWordInBed AndAlso
               firstTargetInBed = AtMemoryCycleTarget286.LocalDram AndAlso
               secondTargetInBed = AtMemoryCycleTarget286.LocalDram Then
                _dataPathWordFastWritesInBed += 1UL
            End If
        Finally
            If dataPathSampleInBed Then
                _dataPathWordApiTicksInBed +=
                    System.Diagnostics.Stopwatch.GetTimestamp() - dataPathStampInBed
            End If
        End Try
    End Sub

    Public Sub LoadBytes(segment As UInt16, offset As UInt16, data As Byte())
        For i As Integer = 0 To data.Length - 1
            WriteByte(PhysicalRaw(segment, CUShort((CInt(offset) + i) And &HFFFF)), data(i))
        Next
    End Sub

    ' CROMWELL LOCAL INSTRUCTION FETCH FAST LANE BRICK 7D
    ' Real-address instruction fetch uses the 286 hidden CS base already cached by
    ' AssignSegment/Brick 7B, then asks the Brick-7A PCB page chip-select whether
    ' any mapped device owns the physical page. Ordinary RAM/ROM code bytes can
    ' therefore be fetched directly without re-entering the full generic memory
    ' transaction machinery. Protected-mode limit/validity checks and all mapped
    ' pages retain the original path.
    <Runtime.CompilerServices.MethodImpl(Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)>
    Private Function FetchByte() As Byte
        If PortBus Is Nothing Then Throw New InvalidOperationException("Processor286 is not attached to its CPU local bus.")
        If _currentInstructionActiveInBed AndAlso _currentInstructionLengthInBed >= 10 Then
            RaiseCpuException(13, "80286 instruction exceeds ten-byte maximum", True, 0US)
        End If

        EnsurePrefetchHeadMatchesExecutionInBed()
        FillPrefetchQueueInBed()
        If _prefetchCountInBed = 0 Then
            RaiseCpuException(13, "Instruction fetch exceeds CS limit", True, 0US)
            Return 0
        End If

        Dim valueInBed As Byte = _prefetchBytesInBed(0)
        For indexInBed As Integer = 1 To _prefetchCountInBed - 1
            _prefetchBytesInBed(indexInBed - 1) = _prefetchBytesInBed(indexInBed)
            _prefetchIpsInBed(indexInBed - 1) = _prefetchIpsInBed(indexInBed)
            _prefetchCsInBed(indexInBed - 1) = _prefetchCsInBed(indexInBed)
            _prefetchBasesInBed(indexInBed - 1) = _prefetchBasesInBed(indexInBed)
        Next
        _prefetchCountInBed -= 1
        IP = CUShort((CInt(IP) + 1) And &HFFFF)
        If _currentInstructionActiveInBed Then _currentInstructionLengthInBed += 1
        RecordFusedCodeBytesInBed(1)
        Return valueInBed
    End Function

    <Runtime.CompilerServices.MethodImpl(Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)>
    Private Function FetchWord() As UInt16
        Dim lowInBed As Byte = FetchByte()
        Dim highInBed As Byte = FetchByte()
        Return CUShort(CInt(lowInBed) Or (CInt(highInBed) << 8))
    End Function

    Private Sub EnsurePrefetchHeadMatchesExecutionInBed()
        If _prefetchCountInBed = 0 Then Return
        If _prefetchIpsInBed(0) <> IP OrElse _prefetchCsInBed(0) <> CS OrElse _prefetchBasesInBed(0) <> _segmentBases(1) Then
            _prefetchCountInBed = 0
        End If
    End Sub

    Private Sub FillPrefetchQueueInBed()
        If _prefetchCountInBed >= 6 Then Return
        Dim nextIpInBed As UInt16
        If _prefetchCountInBed = 0 Then
            nextIpInBed = IP
        Else
            nextIpInBed = CUShort((CInt(_prefetchIpsInBed(_prefetchCountInBed - 1)) + 1) And &HFFFF)
        End If

        While _prefetchCountInBed < 6
            If ProtectedMode Then
                If Not _segmentValid(1) Then
                    If _prefetchCountInBed = 0 Then RaiseCpuException(13, "Invalid CS cache during instruction fetch", True, 0US)
                    Exit While
                End If
                If CUInt(nextIpInBed) > _segmentLimits(1) Then Exit While
            End If
            Dim physicalInBed As UInteger = (_segmentBases(1) + CUInt(nextIpInBed)) And &HFFFFFFUI
            Dim targetInBed As AtMemoryCycleTarget286
            Dim byteInBed As Byte = PortBus.ReadMemoryByte(physicalInBed, targetInBed)
            _prefetchBytesInBed(_prefetchCountInBed) = byteInBed
            _prefetchIpsInBed(_prefetchCountInBed) = nextIpInBed
            _prefetchCsInBed(_prefetchCountInBed) = CS
            _prefetchBasesInBed(_prefetchCountInBed) = _segmentBases(1)
            _prefetchCountInBed += 1
            nextIpInBed = CUShort((CInt(nextIpInBed) + 1) And &HFFFF)
        End While
    End Sub

    Private Sub Push(value As UInt16)
        Dim dataPathSampleInBed As Boolean = _dataPathSampleActiveInBed
        Dim dataPathStampInBed As Long = 0
        If dataPathSampleInBed Then
            dataPathStampInBed = System.Diagnostics.Stopwatch.GetTimestamp()
            _dataPathStackOpsInBed += 1UL
        End If
        Try
            Dim newSpInBed As UInt16 = CUShort((CInt(SP) - 2) And &HFFFF)
            Dim addressInBed As UInteger = SegmentAddress(2, newSpInBed, 2, True)
            WriteWord(addressInBed, value)
            SP = newSpInBed
        Finally
            If dataPathSampleInBed Then
                _dataPathStackTicksInBed +=
                    System.Diagnostics.Stopwatch.GetTimestamp() - dataPathStampInBed
            End If
        End Try
    End Sub

    Private Function PopWord() As UInt16
        Dim dataPathSampleInBed As Boolean = _dataPathSampleActiveInBed
        Dim dataPathStampInBed As Long = 0
        If dataPathSampleInBed Then
            dataPathStampInBed = System.Diagnostics.Stopwatch.GetTimestamp()
            _dataPathStackOpsInBed += 1UL
        End If
        Try
            Dim value As UInt16 = ReadWord(SegmentAddress(2, SP, 2))
            SP = CUShort((CInt(SP) + 2) And &HFFFF)
            Return value
        Finally
            If dataPathSampleInBed Then
                _dataPathStackTicksInBed +=
                    System.Diagnostics.Stopwatch.GetTimestamp() - dataPathStampInBed
            End If
        End Try
    End Function

    Private Function ReadRM8(m As ModRM) As Byte
        If m.ModValue = 3 Then Return GetReg8(m.RM)
        Return ReadByte(ResolveModRmMemoryAddressInBed(m, 1))
    End Function

    Private Function ReadRM16(m As ModRM) As UInt16
        If m.ModValue = 3 Then Return GetReg16(m.RM)
        Return ReadWord(ResolveModRmMemoryAddressInBed(m, 2))
    End Function

    Private Sub WriteRM8(m As ModRM, value As Byte)
        If m.ModValue = 3 Then SetReg8(m.RM, value) Else WriteByte(ResolveModRmMemoryAddressInBed(m, 1, True), value)
    End Sub

    Private Sub TraceDiagnosticDpmi(messageInBed As String)
        If Not _diagnosticImportantIntTraceEnabled Then Return
        _diagnosticDpmiTraceSequence += 1UL
        While _diagnosticDpmiTrace.Count >= DiagnosticDpmiTraceCapacity
            _diagnosticDpmiTrace.Dequeue()
        End While
        _diagnosticDpmiTrace.Enqueue(
            "#D" & _diagnosticDpmiTraceSequence.ToString("000000") &
            " " & messageInBed)
        WriteForensicEventInBed(messageInBed)
    End Sub

    Private Sub BeginForensicTraceInBed()
        If _forensicTraceWriterInBed IsNot Nothing Then Return
        Try
            Dim outputDirectoryInBed As String =
                System.IO.Path.Combine(AppContext.BaseDirectory, "Doutput")
            System.IO.Directory.CreateDirectory(outputDirectoryInBed)
            _forensicTracePathInBed =
                System.IO.Path.Combine(outputDirectoryInBed, "cpu-protected-forensic.bin")
            _forensicTraceStreamInBed =
                New System.IO.FileStream(_forensicTracePathInBed,
                                         System.IO.FileMode.Create,
                                         System.IO.FileAccess.Write,
                                         System.IO.FileShare.Read,
                                         1048576,
                                         System.IO.FileOptions.SequentialScan)
            _forensicTraceBufferInBed = New System.IO.BufferedStream(_forensicTraceStreamInBed, 1048576)
            _forensicTraceWriterInBed = New System.IO.BinaryWriter(
                _forensicTraceBufferInBed, New System.Text.UTF8Encoding(False), True)
            _forensicTraceInstructionCountInBed = 0UL
            _forensicTraceEventCountInBed = 0UL
            _forensicTraceTerminalReasonInBed = String.Empty
            _forensicSiSampleValidInBed = False
            _forensicLastSiInBed = 0US
            _forensicLastSiCsInBed = 0US
            _forensicLastSiIpInBed = 0US
            _forensicLastSiBytesInBed = String.Empty
            _forensicTraceWriterInBed.Write(&H56434654UI) ' "TFCV" little-endian marker.
            _forensicTraceWriterInBed.Write(CUInt(2))
            _forensicTraceWriterInBed.Write(DateTime.UtcNow.Ticks)
            _forensicTraceWriterInBed.Write("Cromwell 80286 protected-mode forensic stream")
        Catch ex As Exception
            _forensicTraceTerminalReasonInBed = "start failed: " & ex.Message
            CloseForensicTraceHandlesInBed()
        End Try
    End Sub

    Private Sub WriteForensicInstructionInBed(opcodeInBed As Byte)
        If _forensicTraceWriterInBed Is Nothing Then Return
        If _forensicTraceInstructionCountInBed >= 65536UL Then
            EndForensicTraceInBed("bounded forensic instruction limit reached")
            Return
        End If
        Try
            ' SI is the retained Standard-mode loader result eventually tested
            ' against 0043h before WIN.COM returns to DOS.  Record only entry
            ' into and departure from error value 0001h, together with the
            ' instruction which preceded the change.  This observes normal CPU
            ' execution and does not special-case or alter the guest result.
            If _forensicSiSampleValidInBed AndAlso SI <> _forensicLastSiInBed AndAlso
               (SI = 1US OrElse _forensicLastSiInBed = 1US) Then
                WriteForensicEventInBed(
                    "SI TRANSITION " & _forensicLastSiInBed.ToString("X4") & "->" &
                    SI.ToString("X4") & " caused-by=" &
                    _forensicLastSiCsInBed.ToString("X4") & ":" &
                    _forensicLastSiIpInBed.ToString("X4") & " bytes=[" &
                    _forensicLastSiBytesInBed & "] next=" &
                    _instructionStartCs.ToString("X4") & ":" &
                    _instructionStartIp.ToString("X4") &
                    " AX=" & AX.ToString("X4") & " BX=" & BX.ToString("X4") &
                    " CX=" & CX.ToString("X4") & " DX=" & DX.ToString("X4") &
                    " DI=" & DI.ToString("X4") & " BP=" & BP.ToString("X4") &
                    " DS=" & DS.ToString("X4") & " ES=" & ES.ToString("X4") &
                    " SS:SP=" & SS.ToString("X4") & ":" & SP.ToString("X4") &
                    " FL=" & Flags.ToString("X4") &
                    " PE=" & If(ProtectedMode, "1", "0"))
            End If
            _forensicSiSampleValidInBed = True
            _forensicLastSiInBed = SI
            _forensicLastSiCsInBed = _instructionStartCs
            _forensicLastSiIpInBed = _instructionStartIp
            _forensicLastSiBytesInBed = ForensicInstructionBytesInBed(10)

            Dim writerInBed As System.IO.BinaryWriter = _forensicTraceWriterInBed
            writerInBed.Write(CByte(1))
            writerInBed.Write(_forensicTraceInstructionCountInBed)
            writerInBed.Write(CS) : writerInBed.Write(IP)
            writerInBed.Write(SS) : writerInBed.Write(SP)
            writerInBed.Write(Flags) : writerInBed.Write(MachineStatusWord)
            writerInBed.Write(AX) : writerInBed.Write(BX)
            writerInBed.Write(CX) : writerInBed.Write(DX)
            writerInBed.Write(SI) : writerInBed.Write(DI) : writerInBed.Write(BP)
            writerInBed.Write(DS) : writerInBed.Write(ES)
            writerInBed.Write(CByte(CurrentPrivilegeLevelInBed()))
            writerInBed.Write(CByte(If(ProtectedMode, 1, 0)))
            writerInBed.Write(opcodeInBed)
            writerInBed.Write(CByte(_rep And &HFF))
            writerInBed.Write(CSByte(_segOverride))
            writerInBed.Write(_segmentBases(1))
            writerInBed.Write(_segmentLimits(1))
            _forensicTraceInstructionCountInBed += 1UL
        Catch ex As Exception
            EndForensicTraceInBed("instruction write failed: " & ex.Message)
        End Try
    End Sub

    Private Function ForensicInstructionBytesInBed(countInBed As Integer) As String
        Dim bytesInBed As New System.Text.StringBuilder()
        Dim boundedCountInBed As Integer = Math.Max(0, Math.Min(countInBed, 16))
        For byteIndexInBed As Integer = 0 To boundedCountInBed - 1
            Dim offsetInBed As UInteger = CUInt(_instructionStartIp) + CUInt(byteIndexInBed)
            If offsetInBed > _instructionStartCsLimitInBed Then Exit For
            If bytesInBed.Length > 0 Then bytesInBed.Append(" "c)
            bytesInBed.Append(
                ReadByte((_instructionStartCsBaseInBed + offsetInBed) And &HFFFFFFUI).
                    ToString("X2"))
        Next
        Return bytesInBed.ToString()
    End Function

    Private Function ForensicStackWordsInBed(byteCountInBed As Integer) As String
        Dim wordsInBed As New System.Text.StringBuilder()
        ' Individual callers choose their diagnostic window. Most request only
        ' 16-32 bytes; the Windows loader error-buffer trace requires 192.
        Dim boundedBytesInBed As Integer = Math.Max(0, Math.Min(byteCountInBed, 192))
        boundedBytesInBed = boundedBytesInBed And Not 1
        For byteOffsetInBed As Integer = 0 To boundedBytesInBed - 1 Step 2
            If wordsInBed.Length > 0 Then wordsInBed.Append(" "c)
            Dim stackOffsetInBed As UShort = CUShort((CUInt(SP) + CUInt(byteOffsetInBed)) And &HFFFFUI)
            Dim physicalInBed As UInteger =
                (_segmentBases(2) + CUInt(stackOffsetInBed)) And &HFFFFFFUI
            Dim lowInBed As Byte = ReadByte(physicalInBed)
            Dim highInBed As Byte = ReadByte((physicalInBed + 1UI) And &HFFFFFFUI)
            wordsInBed.Append(CUShort(CUInt(lowInBed) Or (CUInt(highInBed) << 8)).ToString("X4"))
        Next
        Return wordsInBed.ToString()
    End Function

    Private Sub WriteForensicEventInBed(messageInBed As String)
        If _forensicTraceWriterInBed Is Nothing Then Return
        Try
            _forensicTraceWriterInBed.Write(CByte(2))
            _forensicTraceWriterInBed.Write(_forensicTraceInstructionCountInBed)
            _forensicTraceWriterInBed.Write(messageInBed)
            _forensicTraceEventCountInBed += 1UL
        Catch ex As Exception
            EndForensicTraceInBed("event write failed: " & ex.Message)
        End Try
    End Sub

    Private Sub WriteForensicMemoryWriteInBed(addressInBed As UInteger,
                                               sizeInBed As Byte,
                                               valueInBed As UInt16)
        If _forensicTraceWriterInBed Is Nothing Then Return
        Try
            _forensicTraceWriterInBed.Write(CByte(4))
            _forensicTraceWriterInBed.Write(_forensicTraceInstructionCountInBed)
            _forensicTraceWriterInBed.Write(addressInBed And &HFFFFFFUI)
            _forensicTraceWriterInBed.Write(sizeInBed)
            _forensicTraceWriterInBed.Write(valueInBed)
        Catch ex As Exception
            EndForensicTraceInBed("memory-write record failed: " & ex.Message)
        End Try
    End Sub

    Private Sub TraceDiagnosticLdtWriteInBed(addressInBed As UInteger,
                                             valueInBed As UInt16,
                                             sizeInBed As Byte)
        If Not ProtectedMode OrElse Not _ldtValidInBed Then Return

        Dim firstInBed As UInteger = addressInBed And &HFFFFFFUI
        Dim lastInBed As UInteger = (firstInBed + CUInt(sizeInBed) - 1UI) And &HFFFFFFUI
        Dim tableFirstInBed As UInteger = _ldtBaseInBed And &HFFFFFFUI
        Dim tableLastInBed As UInteger = tableFirstInBed + CUInt(_ldtLimitInBed)
        If lastInBed < tableFirstInBed OrElse firstInBed > tableLastInBed Then Return

        Dim sampleInBed As New DiagnosticLdtWriteSampleInBed With {
            .Address = firstInBed,
            .LdtBase = tableFirstInBed,
            .LdtLimit = _ldtLimitInBed,
            .Value = valueInBed,
            .Size = sizeInBed,
            .Cs = _instructionStartCs,
            .Ip = _instructionStartIp,
            .Ax = AX,
            .Bx = BX,
            .Cx = CX,
            .Dx = DX,
            .Ds = DS,
            .Es = ES,
            .Ss = SS,
            .Sp = SP,
            .Flags = Flags
        }
        _diagnosticLdtWriteRingInBed(_diagnosticLdtWriteIndexInBed) = sampleInBed
        _diagnosticLdtWriteIndexInBed =
            (_diagnosticLdtWriteIndexInBed + 1) Mod DiagnosticLdtWriteHistoryCapacityInBed
        If _diagnosticLdtWriteCountInBed < DiagnosticLdtWriteHistoryCapacityInBed Then
            _diagnosticLdtWriteCountInBed += 1
        End If
    End Sub

    Private Sub WriteDiagnosticLdtHistoryToForensicTraceInBed()
        If _forensicTraceWriterInBed Is Nothing Then Return
        WriteForensicEventInBed(
            "LDT WRITE HISTORY BEGIN count=" & _diagnosticLdtWriteCountInBed.ToString())
        Dim firstIndexInBed As Integer =
            (_diagnosticLdtWriteIndexInBed - _diagnosticLdtWriteCountInBed +
             DiagnosticLdtWriteHistoryCapacityInBed) Mod DiagnosticLdtWriteHistoryCapacityInBed
        For ordinalInBed As Integer = 0 To _diagnosticLdtWriteCountInBed - 1
            Dim sampleInBed As DiagnosticLdtWriteSampleInBed =
                _diagnosticLdtWriteRingInBed(
                    (firstIndexInBed + ordinalInBed) Mod DiagnosticLdtWriteHistoryCapacityInBed)
            Dim descriptorOffsetInBed As UInteger = sampleInBed.Address - sampleInBed.LdtBase
            Dim selectorInBed As UInt16 =
                CUShort(((descriptorOffsetInBed \ 8UI) * 8UI) Or 4UI)
            WriteForensicEventInBed(
                "LDTW #" & (ordinalInBed + 1).ToString("000") &
                " at=" & sampleInBed.Cs.ToString("X4") & ":" & sampleInBed.Ip.ToString("X4") &
                " addr=" & sampleInBed.Address.ToString("X6") &
                " sel=" & selectorInBed.ToString("X4") &
                " byte=" & (descriptorOffsetInBed And 7UI).ToString() &
                " size=" & sampleInBed.Size.ToString() &
                " value=" & If(sampleInBed.Size = 1,
                                  (sampleInBed.Value And &HFFUS).ToString("X2"),
                                  sampleInBed.Value.ToString("X4")) &
                " AX=" & sampleInBed.Ax.ToString("X4") &
                " BX=" & sampleInBed.Bx.ToString("X4") &
                " CX=" & sampleInBed.Cx.ToString("X4") &
                " DX=" & sampleInBed.Dx.ToString("X4") &
                " DS=" & sampleInBed.Ds.ToString("X4") &
                " ES=" & sampleInBed.Es.ToString("X4") &
                " SS:SP=" & sampleInBed.Ss.ToString("X4") & ":" & sampleInBed.Sp.ToString("X4") &
                " FL=" & sampleInBed.Flags.ToString("X4"))
        Next
        WriteForensicEventInBed("LDT WRITE HISTORY END")
    End Sub

    Private Sub EndForensicTraceInBed(reasonInBed As String)
        If _forensicTraceWriterInBed Is Nothing Then Return
        _forensicTraceTerminalReasonInBed = reasonInBed
        Try
            _forensicTraceWriterInBed.Write(CByte(3))
            _forensicTraceWriterInBed.Write(_forensicTraceInstructionCountInBed)
            _forensicTraceWriterInBed.Write(reasonInBed)
            _forensicTraceWriterInBed.Flush()
            _forensicTraceBufferInBed.Flush()
            _forensicTraceStreamInBed.Flush(True)
        Catch
        Finally
            CloseForensicTraceHandlesInBed()
        End Try
    End Sub

    Private Sub CloseForensicTraceHandlesInBed()
        If _forensicTraceWriterInBed IsNot Nothing Then _forensicTraceWriterInBed.Dispose()
        If _forensicTraceBufferInBed IsNot Nothing Then _forensicTraceBufferInBed.Dispose()
        If _forensicTraceStreamInBed IsNot Nothing Then _forensicTraceStreamInBed.Dispose()
        _forensicTraceWriterInBed = Nothing
        _forensicTraceBufferInBed = Nothing
        _forensicTraceStreamInBed = Nothing
    End Sub

    Private Sub WriteRM16(m As ModRM, value As UInt16)
        If m.ModValue = 3 Then SetReg16(m.RM, value) Else WriteWord(ResolveModRmMemoryAddressInBed(m, 2, True), value)
    End Sub

    Private Function GetReg16(index As Integer) As UInt16
        Select Case index And 7
            Case 0 : Return AX
            Case 1 : Return CX
            Case 2 : Return DX
            Case 3 : Return BX
            Case 4 : Return SP
            Case 5 : Return BP
            Case 6 : Return SI
            Case Else : Return DI
        End Select
    End Function

    Private Sub SetReg16(index As Integer, value As UInt16)
        Select Case index And 7
            Case 0 : AX = value
            Case 1 : CX = value
            Case 2 : DX = value
            Case 3 : BX = value
            Case 4 : SP = value
            Case 5 : BP = value
            Case 6 : SI = value
            Case 7 : DI = value
        End Select
    End Sub

    Private Function GetReg8(index As Integer) As Byte
        Select Case index And 7
            Case 0 : Return AL
            Case 1 : Return CL
            Case 2 : Return DL
            Case 3 : Return BL
            Case 4 : Return AH
            Case 5 : Return CH
            Case 6 : Return DH
            Case Else : Return BH
        End Select
    End Function

    Private Sub SetReg8(index As Integer, value As Byte)
        Select Case index And 7
            Case 0 : AL = value
            Case 1 : CL = value
            Case 2 : DL = value
            Case 3 : BL = value
            Case 4 : AH = value
            Case 5 : CH = value
            Case 6 : DH = value
            Case 7 : BH = value
        End Select
    End Sub

    Private Function GetSeg(index As Integer) As UInt16
        Select Case index
            Case 0 : Return ES
            Case 1 : Return CS
            Case 2 : Return SS
            Case Else : Return DS
        End Select
    End Function

    Private Sub SetSeg(index As Integer, value As UInt16)
        Select Case index
            Case 0 : ES = value
            Case 1 : RaiseCpuException(6, "MOV to CS is invalid")
            Case 2 : SS = value
            Case 3 : DS = value
        End Select
    End Sub

    Private Function Flag(mask As UInt16) As Boolean
        Return (Flags And mask) <> 0
    End Function

    Private Sub SetFlag(mask As UInt16, value As Boolean)
        If value Then Flags = Flags Or mask Else Flags = Flags And Not mask
        Flags = Flags Or &H2US
    End Sub

    Private Function NormalizeFlags(value As UInt16) As UInt16
        ' Intel 286 architectural FLAGS image:
        '   bit 1 reads as one
        '   bits 3, 5, and 15 are reserved/clear
        '   in real-address mode bits 12..15 are always clear
        ' Protected-mode IOPL/NT privilege semantics are audited separately;
        ' this helper preserves their representable bits there.
        Dim allowedMask As UInt16 = If(ProtectedMode, &H7FD5US, &H0FD5US)
        Return CUShort((value And allowedMask) Or &H2US)
    End Function

    Private Function Parity(value As Integer) As Boolean
        value = value And &HFF
        value = value Xor (value >> 4)
        value = value And &HF
        Return ((&H9669 >> value) And 1) <> 0
    End Function

    Private Function Add8(a As Byte, b As Byte, Optional carry As Integer = 0) As Byte
        Dim total As Integer = CInt(a) + CInt(b) + carry
        Dim result As Byte = CByte(total And &HFF)
        SetFlag(CF, total > &HFF) : SetFlag(AF, ((a Xor b Xor result) And &H10) <> 0)
        SetFlag(OverflowFlag, ((Not (a Xor b) And (a Xor result)) And &H80) <> 0) : Szp8(result)
        Return result
    End Function

    Private Function Add16(a As UInt16, b As UInt16, Optional carry As Integer = 0) As UInt16
        Dim total As UInteger = CUInt(a) + b + CUInt(carry)
        Dim result As UInt16 = CUShort(total And &HFFFFUI)
        SetFlag(CF, total > &HFFFFUI) : SetFlag(AF, ((a Xor b Xor result) And &H10) <> 0)
        SetFlag(OverflowFlag, ((Not (a Xor b) And (a Xor result)) And &H8000) <> 0) : Szp16(result)
        Return result
    End Function

    Private Function Sub8(a As Byte, b As Byte, Optional borrow As Integer = 0) As Byte
        Dim total As Integer = CInt(a) - CInt(b) - borrow
        Dim result As Byte = CByte(total And &HFF)
        SetFlag(CF, total < 0) : SetFlag(AF, ((a Xor b Xor result) And &H10) <> 0)
        SetFlag(OverflowFlag, (((a Xor b) And (a Xor result)) And &H80) <> 0) : Szp8(result)
        Return result
    End Function

    Private Function Sub16(a As UInt16, b As UInt16, Optional borrow As Integer = 0) As UInt16
        Dim total As Integer = CInt(a) - CInt(b) - borrow
        Dim result As UInt16 = CUShort(total And &HFFFF)
        SetFlag(CF, total < 0) : SetFlag(AF, ((a Xor b Xor result) And &H10) <> 0)
        SetFlag(OverflowFlag, (((a Xor b) And (a Xor result)) And &H8000) <> 0) : Szp16(result)
        Return result
    End Function

    Private Sub Szp8(value As Byte)
        SetFlag(ZF, value = 0) : SetFlag(SF, (value And &H80) <> 0) : SetFlag(PF, Parity(value))
    End Sub
    Private Sub Szp16(value As UInt16)
        SetFlag(ZF, value = 0) : SetFlag(SF, (value And &H8000) <> 0) : SetFlag(PF, Parity(value))
    End Sub
    Private Sub LogicFlags8(value As Byte)
        SetFlag(CF, False) : SetFlag(OverflowFlag, False) : SetFlag(AF, False) : Szp8(value)
    End Sub
    Private Sub LogicFlags16(value As UInt16)
        SetFlag(CF, False) : SetFlag(OverflowFlag, False) : SetFlag(AF, False) : Szp16(value)
    End Sub

    Private Function Alu8(kind As Integer, a As Byte, b As Byte) As Byte
        Select Case kind
            Case 0 : Return Add8(a, b)
            Case 1 : Dim r As Byte = CByte(a Or b) : LogicFlags8(r) : Return r
            Case 2 : Return Add8(a, b, If(Flag(CF), 1, 0))
            Case 3 : Return Sub8(a, b, If(Flag(CF), 1, 0))
            Case 4 : Dim r As Byte = CByte(a And b) : LogicFlags8(r) : Return r
            Case 5 : Return Sub8(a, b)
            Case 6 : Dim r As Byte = CByte(a Xor b) : LogicFlags8(r) : Return r
            Case Else : Sub8(a, b) : Return a
        End Select
    End Function

    Private Function Alu16(kind As Integer, a As UInt16, b As UInt16) As UInt16
        Select Case kind
            Case 0 : Return Add16(a, b)
            Case 1 : Dim r As UInt16 = CUShort(a Or b) : LogicFlags16(r) : Return r
            Case 2 : Return Add16(a, b, If(Flag(CF), 1, 0))
            Case 3 : Return Sub16(a, b, If(Flag(CF), 1, 0))
            Case 4 : Dim r As UInt16 = CUShort(a And b) : LogicFlags16(r) : Return r
            Case 5 : Return Sub16(a, b)
            Case 6 : Dim r As UInt16 = CUShort(a Xor b) : LogicFlags16(r) : Return r
            Case Else : Sub16(a, b) : Return a
        End Select
    End Function

    Private Function Condition(c As Integer) As Boolean
        Select Case c
            Case 0 : Return Flag(OverflowFlag)
            Case 1 : Return Not Flag(OverflowFlag)
            Case 2 : Return Flag(CF)
            Case 3 : Return Not Flag(CF)
            Case 4 : Return Flag(ZF)
            Case 5 : Return Not Flag(ZF)
            Case 6 : Return Flag(CF) Or Flag(ZF)
            Case 7 : Return Not Flag(CF) And Not Flag(ZF)
            Case 8 : Return Flag(SF)
            Case 9 : Return Not Flag(SF)
            Case 10 : Return Flag(PF)
            Case 11 : Return Not Flag(PF)
            Case 12 : Return Flag(SF) <> Flag(OverflowFlag)
            Case 13 : Return Flag(SF) = Flag(OverflowFlag)
            Case 14 : Return Flag(ZF) Or (Flag(SF) <> Flag(OverflowFlag))
            Case Else : Return Not Flag(ZF) And (Flag(SF) = Flag(OverflowFlag))
        End Select
    End Function

    Private Function Shift8(kind As Integer, value As Byte, count As Integer) As Byte
        Dim work As Integer = value
        Dim original As Integer = work
        For index As Integer = 1 To count
            Select Case kind
                Case 0
                    Dim carry As Boolean = (work And &H80) <> 0 : work = ((work << 1) Or If(carry, 1, 0)) And &HFF : SetFlag(CF, carry)
                Case 1
                    Dim carry As Boolean = (work And 1) <> 0 : work = (work >> 1) Or If(carry, &H80, 0) : SetFlag(CF, carry)
                Case 2
                    Dim oldCarry As Boolean = Flag(CF) : SetFlag(CF, (work And &H80) <> 0) : work = ((work << 1) Or If(oldCarry, 1, 0)) And &HFF
                Case 3
                    Dim oldCarry As Boolean = Flag(CF) : SetFlag(CF, (work And 1) <> 0) : work = (work >> 1) Or If(oldCarry, &H80, 0)
                Case 4, 6
                    SetFlag(CF, (work And &H80) <> 0) : work = (work << 1) And &HFF
                Case 5
                    SetFlag(CF, (work And 1) <> 0) : work >>= 1
                Case 7
                    SetFlag(CF, (work And 1) <> 0) : work = (work >> 1) Or (work And &H80)
            End Select
        Next
        Dim result As Byte = CByte(work)
        If kind <= 3 Then
            If count = 1 Then
                If kind = 0 Then SetFlag(OverflowFlag, ((result And &H80) <> 0) Xor Flag(CF))
                If kind = 1 Then SetFlag(OverflowFlag, ((result And &H80) <> 0) Xor ((result And &H40) <> 0))
                If kind = 2 Then SetFlag(OverflowFlag, ((result And &H80) <> 0) Xor Flag(CF))
                If kind = 3 Then SetFlag(OverflowFlag, ((result And &H80) <> 0) Xor ((result And &H40) <> 0))
            End If
        Else
            Szp8(result)
            If count = 1 Then
                If kind = 4 OrElse kind = 6 Then SetFlag(OverflowFlag, ((result And &H80) <> 0) Xor Flag(CF))
                If kind = 5 Then SetFlag(OverflowFlag, (original And &H80) <> 0)
                If kind = 7 Then SetFlag(OverflowFlag, False)
            End If
        End If
        Return result
    End Function

    Private Function Shift16(kind As Integer, value As UInt16, count As Integer) As UInt16
        Dim work As UInteger = value
        Dim original As UInteger = work
        For index As Integer = 1 To count
            Select Case kind
                Case 0
                    Dim carry As Boolean = (work And &H8000UI) <> 0 : work = ((work << 1) Or If(carry, 1UI, 0UI)) And &HFFFFUI : SetFlag(CF, carry)
                Case 1
                    Dim carry As Boolean = (work And 1UI) <> 0 : work = (work >> 1) Or If(carry, &H8000UI, 0UI) : SetFlag(CF, carry)
                Case 2
                    Dim oldCarry As Boolean = Flag(CF) : SetFlag(CF, (work And &H8000UI) <> 0) : work = ((work << 1) Or If(oldCarry, 1UI, 0UI)) And &HFFFFUI
                Case 3
                    Dim oldCarry As Boolean = Flag(CF) : SetFlag(CF, (work And 1UI) <> 0) : work = (work >> 1) Or If(oldCarry, &H8000UI, 0UI)
                Case 4, 6
                    SetFlag(CF, (work And &H8000UI) <> 0) : work = (work << 1) And &HFFFFUI
                Case 5
                    SetFlag(CF, (work And 1UI) <> 0) : work >>= 1
                Case 7
                    SetFlag(CF, (work And 1UI) <> 0) : work = (work >> 1) Or (work And &H8000UI)
            End Select
        Next
        Dim result As UInt16 = CUShort(work)
        If kind <= 3 Then
            If count = 1 Then
                If kind = 0 Then SetFlag(OverflowFlag, ((result And &H8000US) <> 0) Xor Flag(CF))
                If kind = 1 Then SetFlag(OverflowFlag, ((result And &H8000US) <> 0) Xor ((result And &H4000US) <> 0))
                If kind = 2 Then SetFlag(OverflowFlag, ((result And &H8000US) <> 0) Xor Flag(CF))
                If kind = 3 Then SetFlag(OverflowFlag, ((result And &H8000US) <> 0) Xor ((result And &H4000US) <> 0))
            End If
        Else
            Szp16(result)
            If count = 1 Then
                If kind = 4 OrElse kind = 6 Then SetFlag(OverflowFlag, ((result And &H8000US) <> 0) Xor Flag(CF))
                If kind = 5 Then SetFlag(OverflowFlag, (original And &H8000UI) <> 0)
                If kind = 7 Then SetFlag(OverflowFlag, False)
            End If
        End If
        Return result
    End Function

    Private Sub SoftwareInterrupt(vector As Byte)
        If Not ProtectedMode AndAlso vector = &H21 Then
            _diagnosticRealModeDosObservedInBed = True
        End If
        TraceDiagnosticImportantSoftwareInterrupt(vector)
        TraceDiagnosticDosFileServiceEntryInBed(vector)
        TraceDiagnosticQbExecSoftwareInterruptInBed(vector)

        ' Existing high-level firmware services remain an explicit machine boundary.
        Dim vectorAddress As UInteger = CUInt(vector) * 4UI
        Dim guestVectorInstalled As Boolean =
            ReadWord(vectorAddress) <> 0 OrElse
            ReadWord(vectorAddress + 2UI) <> 0

        If HostFirmwareInterrupts AndAlso HostFirmwareHandler IsNot Nothing AndAlso
           vector >= &H10 AndAlso Not guestVectorInstalled Then
            HostFirmwareHandler.Invoke(vector)
            Return
        End If

        EnterInterrupt(vector, True)
    End Sub

    Private Function EnterInterrupt(vector As Integer,
                                    softwareGenerated As Boolean,
                                    Optional hasErrorCodeInBed As Boolean = False,
                                    Optional errorCodeInBed As UInt16 = 0US) As Boolean
        If Not ProtectedMode Then
            Push(NormalizeFlags(Flags)) : Push(CS) : Push(IP)
            SetFlag(InterruptFlag, False) : SetFlag(TF, False)
            IP = ReadWord(CUInt(vector) * 4UI) : CS = ReadWord(CUInt(vector) * 4UI + 2UI)
            NoteInterruptEntryForNmiInBed()
            Return True
        End If

        Dim gateAddress As UInteger = IdtrBase + CUInt(vector * 8)
        Dim idtErrorCodeInBed As UInt16 =
            SelectorErrorCodeInBed(CUShort(vector * 8), idtReferenceInBed:=True)
        If CUInt(vector * 8 + 7) > IdtrLimit Then
            RaiseCpuException(13, "Interrupt vector exceeds IDT limit", True, idtErrorCodeInBed)
            Return False
        End If
        Dim targetOffset As UInt16 = ReadWord(gateAddress)
        Dim targetSelector As UInt16 = ReadWord(gateAddress + 2UI)
        Dim gateAccess As Byte = ReadByte(gateAddress + 5UI)
        Dim gateType As Integer = gateAccess And &H1F
        Dim currentPrivilege As Integer = CurrentPrivilegeLevelInBed()
        If gateType = 5 Then
            Dim gatePrivilegeInBed As Integer = (gateAccess >> 5) And 3
            If softwareGenerated AndAlso currentPrivilege > gatePrivilegeInBed Then
                RaiseCpuException(13, "Task-gate privilege violation", True, idtErrorCodeInBed)
                Return False
            End If
            If (gateAccess And &H80) = 0 Then
                RaiseCpuException(11, "Task gate is not present", True, idtErrorCodeInBed)
                Return False
            End If
            Dim taskSelectorInBed As UInt16 = targetSelector
            PerformTaskSwitchInBed(taskSelectorInBed, TaskSwitchKindInBed.NestedCall)
            If hasErrorCodeInBed Then Push(errorCodeInBed)
            NoteInterruptEntryForNmiInBed()
            Return True
        End If
        If gateType <> 6 AndAlso gateType <> 7 Then
            RaiseCpuException(13, "Unsupported protected-mode gate type", True, idtErrorCodeInBed)
            Return False
        End If
        Dim gatePrivilege As Integer = (gateAccess >> 5) And 3
        If softwareGenerated AndAlso currentPrivilege > gatePrivilege Then
            RaiseCpuException(13, "Interrupt gate privilege violation", True, idtErrorCodeInBed)
            Return False
        End If
        If (gateAccess And &H80) = 0 Then
            RaiseCpuException(11, "Interrupt gate is not present", True, idtErrorCodeInBed)
            Return False
        End If

        Dim codeDescriptor As Descriptor286
        If Not TryReadDescriptor(targetSelector, codeDescriptor) Then
            RaiseCpuException(13, "Interrupt gate target selector is invalid", True, SelectorErrorCodeInBed(targetSelector))
            Return False
        End If
        If (codeDescriptor.Access And &H18) <> &H18 Then
            RaiseCpuException(13, "Interrupt gate target is not a code segment", True, SelectorErrorCodeInBed(targetSelector))
            Return False
        End If
        Dim descriptorPrivilegeInBed As Integer = (codeDescriptor.Access >> 5) And 3
        Dim conformingTargetInBed As Boolean = (codeDescriptor.Access And 4) <> 0
        If descriptorPrivilegeInBed > currentPrivilege Then
            RaiseCpuException(13, "Interrupt target privilege violation", True, SelectorErrorCodeInBed(targetSelector))
            Return False
        End If
        If (codeDescriptor.Access And &H80) = 0 Then
            RaiseCpuException(11, "Interrupt target code segment is not present", True, SelectorErrorCodeInBed(targetSelector))
            Return False
        End If
        ' A conforming target executes at the interrupted CPL.  Its descriptor
        ' DPL controls which outer rings may enter it, but does not become the
        ' new CPL and must not cause a privilege-stack switch.
        Dim targetPrivilege As Integer = If(conformingTargetInBed,
                                               currentPrivilege,
                                               descriptorPrivilegeInBed)

        Dim oldFlags As UInt16 = Flags, oldCs As UInt16 = CS, oldIp As UInt16 = IP
        If targetPrivilege < currentPrivilege Then
            Dim oldSs As UInt16 = SS, oldSp As UInt16 = SP
            Dim tssDescriptor As Descriptor286
            If Not TryReadGdtDescriptor(TaskRegister, tssDescriptor) Then
                RaiseCpuException(10, "Privilege transition requires a valid TSS", True, SelectorErrorCodeInBed(TaskRegister))
                Return False
            End If
            Dim stackOffset As UInteger = CUInt(2 + targetPrivilege * 4)
            Dim newSp As UInt16 = ReadWord(tssDescriptor.BaseAddress + stackOffset)
            Dim newSs As UInt16 = ReadWord(tssDescriptor.BaseAddress + stackOffset + 2UI)
            Dim newStackStageInBed As SegmentLoadStageInBed =
                StageStackSegmentForPrivilegeInBed(newSs, targetPrivilege)
            CommitSegmentLoadInBed(newStackStageInBed)
            SP = newSp
            Push(oldSs) : Push(oldSp) : Push(oldFlags) : Push(oldCs) : Push(oldIp)
        Else
            Push(oldFlags) : Push(oldCs) : Push(oldIp)
        End If
        CacheSegmentDescriptor(1, CUShort((targetSelector And &HFFFCUS) Or targetPrivilege), codeDescriptor)
        IP = targetOffset
        SetFlag(TF, False)
        If gateType = 6 Then SetFlag(InterruptFlag, False)
        If hasErrorCodeInBed Then Push(errorCodeInBed)
        NoteInterruptEntryForNmiInBed()
        Return True
    End Function

    Private Sub CacheSegmentDescriptor(segmentIndex As Integer, selector As UInt16, descriptor As Descriptor286)
        _segmentSelectors(segmentIndex) = selector
        _segmentBases(segmentIndex) = descriptor.BaseAddress
        _segmentLimits(segmentIndex) = descriptor.Limit
        _segmentAccess(segmentIndex) = descriptor.Access
        _segmentValid(segmentIndex) = True
        If segmentIndex = 1 AndAlso ProtectedMode Then _protectedModeCsLoadedInBed = True
    End Sub

    Private Sub ExecuteIret()
        If _diagnosticDpmiExceptionReturnPending Then
            Dim returnIpInBed As UInt16 = PeekStackWordInBed(0)
            Dim returnCsInBed As UInt16 = PeekStackWordInBed(2)
            Dim returnFlagsInBed As UInt16 = PeekStackWordInBed(4)
            TraceDiagnosticDpmi(
                "exception IRET frame -> " & returnCsInBed.ToString("X4") & ":" &
                returnIpInBed.ToString("X4") &
                " from=" & CS.ToString("X4") & ":" & _instructionStartIp.ToString("X4") &
                " SS:SP=" & SS.ToString("X4") & ":" & SP.ToString("X4") &
                " FL=" & returnFlagsInBed.ToString("X4"))
            _diagnosticDpmiExceptionReturnPending = False
        End If
        If Not ProtectedMode Then
            Dim newIpInBed As UInt16 = PeekStackWordInBed(0)
            Dim newCsInBed As UInt16 = PeekStackWordInBed(2)
            Dim newFlagsInBed As UInt16 = PeekStackWordInBed(4)
            Dim finalRealModeSpInBed As UInt16 = CUShort((CInt(SP) + 6) And &HFFFF)
            Dim csStageInBed As SegmentLoadStageInBed = StageSegmentLoadInBed(1, newCsInBed)
            SP = finalRealModeSpInBed
            CommitSegmentLoadInBed(csStageInBed)
            IP = newIpInBed
            Flags = NormalizeFlags(newFlagsInBed)
            NoteIretForNmiInBed()
            Return
        End If

        If (Flags And &H4000US) <> 0US Then
            If Not _taskValidInBed Then RaiseCpuException(10, "IRET with NT requires a valid current TSS", True, SelectorErrorCodeInBed(TaskRegister))
            Dim backlinkInBed As UInt16 = ReadWord(_taskBaseInBed)
            PerformTaskSwitchInBed(backlinkInBed, TaskSwitchKindInBed.Iret)
            NoteIretForNmiInBed()
            Return
        End If

        Dim newIp As UInt16 = PeekStackWordInBed(0)
        Dim newCs As UInt16 = PeekStackWordInBed(2)
        Dim newFlags As UInt16 = PeekStackWordInBed(4)
        If newIp = &H9F31US Then
            If Not _diagnosticImportantIntTraceEnabled Then BeginDiagnosticImportantIntTrace()
            Dim entryInBed As String =
                "IRET -> " & newCs.ToString("X4") & ":" & newIp.ToString("X4") &
                " from=" & CS.ToString("X4") & ":" & _instructionStartIp.ToString("X4") &
                " SS:SP=" & SS.ToString("X4") & ":" & SP.ToString("X4") &
                " FL=" & newFlags.ToString("X4")
            TraceDiagnosticImportantInt(entryInBed)
            TraceDiagnosticDpmi(entryInBed)
        End If
        Dim currentPrivilege As Integer = CurrentPrivilegeLevelInBed()
        Dim returnRplInBed As Integer = newCs And 3

        Dim codeDescriptor As Descriptor286
        If Not TryReadDescriptor(newCs, codeDescriptor) Then
            RaiseCpuException(13, "IRET target selector is invalid", True, SelectorErrorCodeInBed(newCs))
            Return
        End If
        If (codeDescriptor.Access And &H18) <> &H18 Then
            RaiseCpuException(13, "IRET target is not code", True, SelectorErrorCodeInBed(newCs))
            Return
        End If
        Dim returnDescriptorDplInBed As Integer = (codeDescriptor.Access >> 5) And 3
        Dim returnConformingInBed As Boolean = (codeDescriptor.Access And 4) <> 0
        Dim returnPrivilege As Integer
        If returnConformingInBed Then
            If returnDescriptorDplInBed > currentPrivilege Then
                RaiseCpuException(13, "IRET conforming target privilege violation", True, SelectorErrorCodeInBed(newCs))
                Return
            End If
            returnPrivilege = currentPrivilege
        Else
            If returnRplInBed < currentPrivilege Then
                RaiseCpuException(13, "IRET cannot return to a more privileged ring", True, SelectorErrorCodeInBed(newCs))
                Return
            End If
            If returnDescriptorDplInBed <> returnRplInBed Then
                RaiseCpuException(13, "IRET target code privilege violation", True, SelectorErrorCodeInBed(newCs))
                Return
            End If
            returnPrivilege = returnRplInBed
        End If
        If (codeDescriptor.Access And &H80) = 0 Then
            RaiseCpuException(11, "IRET target code is not present", True, SelectorErrorCodeInBed(newCs))
            Return
        End If
        If CUInt(newIp) > codeDescriptor.Limit Then
            RaiseCpuException(13, "IRET target offset exceeds code limit", True, 0US)
            Return
        End If

        Dim finalSpInBed As UInt16 = CUShort((CInt(SP) + 6) And &HFFFF)
        Dim newSsStageInBed As SegmentLoadStageInBed
        Dim newOuterSpInBed As UInt16 = 0US
        If returnPrivilege > currentPrivilege Then
            newOuterSpInBed = PeekStackWordInBed(6)
            Dim newSsInBed As UInt16 = PeekStackWordInBed(8)
            newSsStageInBed = StageStackSegmentForPrivilegeInBed(newSsInBed, returnPrivilege)
        End If

        CacheSegmentDescriptor(1,
                               CUShort((newCs And &HFFFCUS) Or returnPrivilege),
                               codeDescriptor)
        IP = newIp
        If returnPrivilege > currentPrivilege Then
            CommitSegmentLoadInBed(newSsStageInBed)
            SP = newOuterSpInBed
            InvalidateOuterPrivilegeDataSegmentsInBed(returnPrivilege)
        Else
            SP = finalSpInBed
        End If
        ApplyIretFlagsInBed(newFlags, currentPrivilege)
        NoteIretForNmiInBed()
    End Sub
    Private Sub ExecuteLoop(op As Byte)
        Dim displacement As Integer = Signed8(FetchByte()) : Dim take As Boolean
        If op = &HE3 Then
            take = CX = 0
        Else
            CX = CUShort((CInt(CX) - 1) And &HFFFF)
            take = CX <> 0 AndAlso (op = &HE2 OrElse (op = &HE1 And Flag(ZF)) OrElse (op = &HE0 And Not Flag(ZF)))
        End If
        If take Then IP = CUShort((CInt(IP) + displacement) And &HFFFF)
    End Sub

    Private Sub ExecutePortIo(op As Byte)
        RequireIoPrivilegeInBed()
        Dim port As UInt16 = If(op >= &HEC, DX, CUShort(FetchByte()))
        Dim wordOperation As Boolean = (op And 1) <> 0 : Dim inputOperation As Boolean = (op And 2) = 0
        If inputOperation Then
            If wordOperation Then AX = InWord(port) Else AL = InByte(port)
        Else
            If wordOperation Then OutWord(port, AX) Else OutByte(port, AL)
        End If
    End Sub

    Public Overridable Function InByte(port As UInt16) As Byte
        If PortBus IsNot Nothing Then Return PortBus.ReadByte(port)
        Return &HFF
    End Function
    Public Overridable Function InWord(port As UInt16) As UInt16
        If PortBus IsNot Nothing Then Return PortBus.ReadWord(port)
        Return CUShort(InByte(port) Or (CUShort(InByte(CUShort(port + 1))) << 8))
    End Function
    Public Overridable Sub OutByte(port As UInt16, value As Byte)
        If PortBus IsNot Nothing Then PortBus.WriteByte(port, value)
    End Sub
    Public Overridable Sub OutWord(port As UInt16, value As UInt16)
        If PortBus IsNot Nothing Then
            PortBus.WriteWord(port, value)
            Return
        End If
        OutByte(port, CByte(value And &HFFUS)) : OutByte(CUShort(port + 1), CByte(value >> 8))
    End Sub

    Private Sub ExecuteLoadFarPointer(op As Byte)
        Dim m As ModRM = DecodeModRM()
        If m.ModValue = 3 Then RaiseCpuException(6, "LES/LDS requires memory") : Return
        Dim farPointerAddressInBed As UInteger = ResolveModRmMemoryAddressInBed(m, 4)
        Dim newOffsetInBed As UInt16 = ReadWord(farPointerAddressInBed)
        Dim newSelectorInBed As UInt16 = ReadWord(farPointerAddressInBed + 2UI)
        Dim targetSegmentInBed As Integer = If(op = &HC4, 0, 3)
        Dim stagedInBed As SegmentLoadStageInBed = StageSegmentLoadInBed(targetSegmentInBed, newSelectorInBed)
        SetReg16(m.Reg, newOffsetInBed)
        CommitSegmentLoadInBed(stagedInBed)
    End Sub

    Private Sub ExecuteImulImmediate(op As Byte)
        Dim m As ModRM = DecodeModRM()
        Dim immediate As Integer = If(op = &H69, Signed16(FetchWord()), Signed8(FetchByte()))
        Dim product As Integer = Signed16(ReadRM16(m)) * immediate
        SetReg16(m.Reg, CUShort(product And &HFFFF))
        Dim fits As Boolean = product >= Short.MinValue And product <= Short.MaxValue
        SetFlag(CF, Not fits) : SetFlag(OverflowFlag, Not fits)
    End Sub

    Private Sub ExecuteAam()
        Dim baseValue As Byte = FetchByte()
        If baseValue = 0 Then RaiseCpuException(0, "AAM divide by zero") : Return
        AH = CByte(AL \ baseValue) : AL = CByte(AL Mod baseValue) : Szp8(AL)
    End Sub
    Private Sub ExecuteAad()
        Dim baseValue As Byte = FetchByte() : AL = CByte((AH * baseValue + AL) And &HFF) : AH = 0 : Szp8(AL)
    End Sub
    Private Sub ExecuteDecimalAdjust(op As Byte)
        Select Case op
            Case &H27 ' DAA
                Dim oldValue As Byte = AL : Dim oldCarry As Boolean = Flag(CF)
                If (AL And &HF) > 9 OrElse Flag(AF) Then AL = CByte((CInt(AL) + 6) And &HFF) : SetFlag(AF, True) Else SetFlag(AF, False)
                If oldValue > &H99 OrElse oldCarry Then AL = CByte((CInt(AL) + &H60) And &HFF) : SetFlag(CF, True) Else SetFlag(CF, False)
                Szp8(AL)
            Case &H2F ' DAS
                Dim oldValue As Byte = AL : Dim oldCarry As Boolean = Flag(CF)
                If (AL And &HF) > 9 OrElse Flag(AF) Then AL = CByte((CInt(AL) - 6) And &HFF) : SetFlag(AF, True) Else SetFlag(AF, False)
                If oldValue > &H99 OrElse oldCarry Then AL = CByte((CInt(AL) - &H60) And &HFF) : SetFlag(CF, True) Else SetFlag(CF, False)
                Szp8(AL)
            Case &H37 ' AAA
                If (AL And &HF) > 9 OrElse Flag(AF) Then AX = CUShort((CInt(AX) + &H106) And &HFFFF) : SetFlag(AF, True) : SetFlag(CF, True) Else SetFlag(AF, False) : SetFlag(CF, False)
                AL = CByte(AL And &HF)
            Case &H3F ' AAS
                If (AL And &HF) > 9 OrElse Flag(AF) Then AX = CUShort((CInt(AX) - &H106) And &HFFFF) : SetFlag(AF, True) : SetFlag(CF, True) Else SetFlag(AF, False) : SetFlag(CF, False)
                AL = CByte(AL And &HF)
        End Select
    End Sub
    Private Sub ExecuteStringIo(op As Byte)
        If _rep <> 0 AndAlso CX = 0US Then Return
        RequireIoPrivilegeInBed()

        Dim wordOperation As Boolean = (op And 1) <> 0
        Dim increment As Integer = If(wordOperation, 2, 1)
        If Flag(DF) Then increment = -increment

        If op = &H6C OrElse op = &H6D Then
            ' Validate ES destination before the irreversible port read.
            Dim destinationInBed As UInteger = SegmentAddress(0, DI, If(wordOperation, 2, 1), True)
            If wordOperation Then
                Dim valueInBed As UInt16 = InWord(DX)
                WriteWord(destinationInBed, valueInBed)
            Else
                Dim valueInBed As Byte = InByte(DX)
                WriteByte(destinationInBed, valueInBed)
            End If
            DI = CUShort((CInt(DI) + increment) And &HFFFF)
        Else
            ' Validate/read memory before the irreversible port write.
            Dim sourceInBed As UInteger = CurrentDataAddress(False, SI, If(wordOperation, 2, 1))
            If wordOperation Then OutWord(DX, ReadWord(sourceInBed)) Else OutByte(DX, ReadByte(sourceInBed))
            SI = CUShort((CInt(SI) + increment) And &HFFFF)
        End If

        If _rep = 0 Then Return
        CX = CUShort((CInt(CX) - 1) And &HFFFF)
        If CX <> 0US Then RewindRepInstructionInBed()
    End Sub

    Private Sub ExecuteArpl()
        If Not ProtectedMode Then RaiseCpuException(6, "ARPL is invalid in real mode") : Return
        Dim operand As ModRM = DecodeModRM()
        Dim destination As UInt16 = ReadRM16(operand)
        Dim source As UInt16 = GetReg16(operand.Reg)
        If (destination And 3) < (source And 3) Then
            destination = CUShort((destination And &HFFFCUS) Or (source And 3))
            WriteRM16(operand, destination)
            SetFlag(ZF, True)
        Else
            SetFlag(ZF, False)
        End If
    End Sub

    Private Sub ExecuteBound()
        Dim operand As ModRM = DecodeModRM()
        If operand.ModValue = 3 Then RaiseCpuException(6, "BOUND requires a memory operand") : Return
        Dim value As Integer = Signed16(GetReg16(operand.Reg))
        Dim boundsAddressInBed As UInteger = ResolveModRmMemoryAddressInBed(operand, 4)
        Dim lowerBound As Integer = Signed16(ReadWord(boundsAddressInBed))
        Dim upperBound As Integer = Signed16(ReadWord(boundsAddressInBed + 2UI))
        If value < lowerBound OrElse value > upperBound Then RaiseCpuException(5, "BOUND range exceeded")
    End Sub
    Private Sub RequireNumericExtensionForEscInBed()
        ' MSW.EM or MSW.TS makes the first ESC trap through exception 7 so an OS
        ' can emulate or lazily restore the 80287 context.
        If (MachineStatusWord And &HCUS) <> 0US Then
            _diagnosticEscNmTrapCountInBed += 1UL
            RaiseCpuException(7, "Processor extension not available")
        End If
    End Sub

    Private Sub ExecuteWaitInBed()
        ' WAIT traps on TS only when MP is set.  The 80287 object is synchronous
        ' today, so BUSY normally clears immediately.
        If (MachineStatusWord And &HAUS) = &HAUS Then
            RaiseCpuException(7, "WAIT with MP=1 and TS=1")
            Return
        End If
        If _numericCoprocessor.Busy Then
            EnterHaltStateInBed(ProcessorHaltSourceInBed.ExternalStop)
            Return
        End If

        ' An 80286 has no architectural #MF exception.  The 80287's ERROR pin
        ' reaches the AT through the motherboard NPX latch and slave-PIC IRQ13;
        ' vector 10h remains the BIOS video-service interrupt in real mode.
        ' Routing ERROR directly through CPU vector 16 therefore turns FWAIT
        ' into an accidental INT 10h loop (observed in Scorched Earth).  Until
        ' the external IRQ13/latch path is modeled, preserve the coprocessor
        ' status for diagnostics but let WAIT retire normally.
    End Sub

    Private Sub ExecuteEsc(op As Byte)
        ' Record at the 80286 ESC-decode boundary before EM/TS can raise #NM.
        ' This observes no extra guest bus cycles and does not falsely claim
        ' that a trapped instruction reached the physical 80287.
        _diagnosticEscAttemptCountInBed += 1UL
        _diagnosticLastEscCsInBed = _instructionStartCs
        _diagnosticLastEscIpInBed = _instructionStartIp
        _diagnosticLastEscOpcodeInBed = op
        _diagnosticLastEscMswInBed = MachineStatusWord
        RequireNumericExtensionForEscInBed()
        Dim operand As ModRM = DecodeModRM()
        Dim instructionOffsetInBed As UShort = CUShort((CInt(IP) - 2) And &HFFFF)
        Dim modRmByteInBed As Byte =
            CByte(((operand.ModValue And 3) << 6) Or
                  ((operand.Reg And 7) << 3) Or
                  (operand.RM And 7))
        _lastFpuOpcodeWordInBed = CUShort(((op And 7) << 8) Or modRmByteInBed)
        _numericCoprocessor.RecordDiagnosticInstruction(CS,
                                                       instructionOffsetInBed,
                                                       op,
                                                       modRmByteInBed)
        _lastFpuInstructionOffsetInBed = _instructionStartIp
        _lastFpuInstructionSelectorInBed = CS
        _lastFpuInstructionPhysicalInBed =
            (_instructionStartCsBaseInBed + CUInt(_instructionStartIp)) And &HFFFFFUI
        If operand.ModValue <> 3 Then
            _lastFpuOperandOffsetInBed = operand.Offset
            _lastFpuOperandSelectorInBed = operand.Segment
        End If
        If operand.ModValue = 3 Then
            ExecuteFpuRegister(op, operand.Reg, operand.RM)
        Else
            ExecuteFpuMemory(op, operand)
        End If
    End Sub

    Private Sub ExecuteFpuRegister(op As Byte,
                                   operation As Integer,
                                   registerIndex As Integer)
        Select Case op
            Case &HD8
                ' D8 C0-FF: arithmetic/compare with ST(0) destination.
                _numericCoprocessor.ExecuteSt0ArithmeticWithSt(operation, registerIndex)

            Case &HD9
                Select Case operation
                    Case 0
                        ' FLD ST(i)
                        _numericCoprocessor.PushSt(registerIndex)

                    Case 1
                        ' FXCH ST(i)
                        _numericCoprocessor.Exchange(registerIndex)

                    Case 2
                        ' FNOP is D9 D0. Other encodings in this group are reserved.
                        If registerIndex <> 0 Then _numericCoprocessor.ExecuteReservedEncoding()

                    Case 3
                        ' The 80287 decodes D9 D8-DF as the FSTP ST(i)
                        ' compatibility form.
                        If _numericCoprocessor.CopySt0To(registerIndex) Then
                            _numericCoprocessor.Pop()
                        End If

                    Case 4
                        Select Case registerIndex
                            Case 0 ' FCHS
                                _numericCoprocessor.ChangeSign()
                            Case 1 ' FABS
                                _numericCoprocessor.AbsoluteValue()
                            Case 4 ' FTST
                                _numericCoprocessor.TestSt0()
                            Case 5 ' FXAM
                                _numericCoprocessor.Examine()
                            Case Else
                                _numericCoprocessor.ExecuteReservedEncoding()
                        End Select

                    Case 5
                        ' D9 E8-EE: FLD1/FLDL2T/FLDL2E/FLDPI/FLDLG2/FLDLN2/FLDZ.
                        If registerIndex <= 6 Then
                            _numericCoprocessor.LoadConstant(registerIndex)
                        Else
                            _numericCoprocessor.ExecuteReservedEncoding()
                        End If

                    Case 6
                        ' D9 F0-F7.
                        Select Case registerIndex
                            Case 0 ' F2XM1
                                _numericCoprocessor.F2xm1()
                            Case 1 ' FYL2X
                                _numericCoprocessor.Fyl2x(addOneInBed:=False)
                            Case 2 ' FPTAN -- this was incorrectly decoded as FSQRT before this brick.
                                _numericCoprocessor.Fptan()
                            Case 3 ' FPATAN
                                _numericCoprocessor.Fpatan()
                            Case 4 ' FXTRACT
                                _numericCoprocessor.Extract()
                            Case 5 ' Reserved on 80287; FPREM1 is 80387-only.
                                _numericCoprocessor.ExecuteReservedEncoding()
                            Case 6 ' FDECSTP
                                _numericCoprocessor.DecrementTop()
                            Case 7 ' FINCSTP
                                _numericCoprocessor.IncrementTop()
                        End Select

                    Case 7
                        ' D9 F8-FF.  80287-class subset only; 387-only transcendental
                        ' additions are deliberately not invented here.
                        Select Case registerIndex
                            Case 0 ' FPREM
                                _numericCoprocessor.Prem()
                            Case 1 ' FYL2XP1
                                _numericCoprocessor.Fyl2x(addOneInBed:=True)
                            Case 2 ' FSQRT -- actual opcode D9 FA.
                                _numericCoprocessor.SquareRoot()
                            Case 3 ' Reserved on 80287; FSINCOS is 80387-only.
                                _numericCoprocessor.ExecuteReservedEncoding()
                            Case 4 ' FRNDINT
                                _numericCoprocessor.RoundSt0ToIntegral()
                            Case 5 ' FSCALE
                                _numericCoprocessor.Scale()
                            Case 6, 7 ' Reserved on 80287; FSIN/FCOS are 80387-only.
                                _numericCoprocessor.ExecuteReservedEncoding()
                        End Select
                End Select

            Case &HDB
                ' 80287 no-wait control operations.  FINIT is commonly emitted
                ' as FWAIT + FNINIT; silently ignoring DB E3 leaves stale tags
                ' and exceptions in programs which deliberately reinitialize
                ' the coprocessor between phases.
                If operation = 4 Then
                    Select Case registerIndex
                        Case 0, 1
                            ' Intel documents the reserved 8087 FENI/FDISI
                            ' encodings as effective no-ops on the 80287.
                        Case 2 ' FNCLEX (DB E2)
                            _numericCoprocessor.ClearExceptions()
                        Case 3 ' FNINIT (DB E3)
                            _numericCoprocessor.Reset()
                        Case 4
                            ' FSETPM informs a physical 80287 of protected-mode
                            ' addressing. Address formation already belongs to the
                            _numericCoprocessor.SetProtectedMode()
                        Case Else
                            _numericCoprocessor.ExecuteReservedEncoding()
                    End Select
                Else
                    _numericCoprocessor.ExecuteReservedEncoding()
                End If

            Case &HDC
                ' DC C0/CF etc: result destination is ST(i), no pop.
                If operation = 2 OrElse operation = 3 Then
                    ' Appendix A footnotes: DC D0-DF execute as FCOM/FCOMP.
                    _numericCoprocessor.CompareSt0WithSt(
                        registerIndex, If(operation = 3, 1, 0))
                ElseIf operation = 0 OrElse operation = 1 OrElse
                       operation = 4 OrElse operation = 5 OrElse
                       operation = 6 OrElse operation = 7 Then
                    _numericCoprocessor.ExecuteStiArithmeticWithSt0(
                        operation, registerIndex, popAfterInBed:=False)
                End If

            Case &HDD
                Select Case operation
                    Case 0 ' FFREE ST(i)
                        _numericCoprocessor.Free(registerIndex)
                    Case 1 ' Appendix A footnote: undocumented FXCH ST(i).
                        _numericCoprocessor.Exchange(registerIndex)
                    Case 2 ' FST ST(i)
                        _numericCoprocessor.CopySt0To(registerIndex)
                    Case 3 ' FSTP ST(i)
                        If _numericCoprocessor.CopySt0To(registerIndex) Then
                            _numericCoprocessor.Pop()
                        End If
                    Case Else
                        ' DD E0-FF are reserved on 80287. FUCOM/FUCOMP are
                        ' later-coprocessor instructions.
                        _numericCoprocessor.ExecuteReservedEncoding()
                End Select

            Case &HDE
                If operation = 2 Then
                    ' Appendix A footnote: DE D0-D7 execute as FCOMP ST(i).
                    _numericCoprocessor.CompareSt0WithSt(registerIndex, 1)
                ElseIf operation = 3 AndAlso registerIndex = 1 Then
                    ' FCOMPP compares ST(0),ST(1), then pops twice.
                    _numericCoprocessor.CompareSt0WithSt(1, 2)
                ElseIf operation = 0 OrElse operation = 1 OrElse
                       operation = 4 OrElse operation = 5 OrElse
                       operation = 6 OrElse operation = 7 Then
                    _numericCoprocessor.ExecuteStiArithmeticWithSt0(
                        operation, registerIndex, popAfterInBed:=True)
                Else
                    _numericCoprocessor.ExecuteReservedEncoding()
                End If

            Case &HDF
                ' FNSTSW AX = DF E0.
                If operation = 4 AndAlso registerIndex = 0 Then
                    AX = _numericCoprocessor.StatusWord
                ElseIf operation = 0 Then
                    ' Appendix A footnote: FFREE ST(i), then pop stack.
                    _numericCoprocessor.Free(registerIndex)
                    _numericCoprocessor.Pop()
                ElseIf operation = 1 Then
                    ' Appendix A footnote: undocumented FXCH ST(i).
                    _numericCoprocessor.Exchange(registerIndex)
                ElseIf operation = 2 OrElse operation = 3 Then
                    ' 80287 compatibility encodings DF D0-DF are FSTP ST(i).
                    If _numericCoprocessor.CopySt0To(registerIndex) Then
                        _numericCoprocessor.Pop()
                    End If
                Else
                    _numericCoprocessor.ExecuteReservedEncoding()
                End If
        End Select
    End Sub

    Private Sub ExecuteFpuMemory(op As Byte, operand As ModRM)
        Dim operandLengthInBed As Integer
        Dim writeOperandInBed As Boolean
        Select Case op
            Case &HD8, &HDA
                operandLengthInBed = 4
            Case &HDC
                operandLengthInBed = 8
            Case &HDE
                operandLengthInBed = 2
            Case &HD9
                Select Case operand.Reg
                    Case 0, 2, 3 : operandLengthInBed = 4
                    Case 4, 6 : operandLengthInBed = 14
                    Case 5, 7 : operandLengthInBed = 2
                End Select
                writeOperandInBed = operand.Reg = 2 OrElse operand.Reg = 3 OrElse
                                    operand.Reg = 6 OrElse operand.Reg = 7
            Case &HDB
                Select Case operand.Reg
                    Case 0, 2, 3 : operandLengthInBed = 4
                    Case 5, 7 : operandLengthInBed = 10
                End Select
                writeOperandInBed = operand.Reg = 2 OrElse operand.Reg = 3 OrElse operand.Reg = 7
            Case &HDD
                Select Case operand.Reg
                    Case 0, 2, 3 : operandLengthInBed = 8
                    Case 4, 6 : operandLengthInBed = 94
                    Case 7 : operandLengthInBed = 2
                End Select
                writeOperandInBed = operand.Reg = 2 OrElse operand.Reg = 3 OrElse
                                    operand.Reg = 6 OrElse operand.Reg = 7
            Case &HDF
                Select Case operand.Reg
                    Case 0, 2, 3 : operandLengthInBed = 2
                    Case 4, 6 : operandLengthInBed = 10
                    Case 5, 7 : operandLengthInBed = 8
                End Select
                writeOperandInBed = operand.Reg = 2 OrElse operand.Reg = 3 OrElse
                                    operand.Reg = 6 OrElse operand.Reg = 7
        End Select
        If operandLengthInBed = 0 Then
            _numericCoprocessor.ExecuteReservedEncoding()
            Return
        End If
        operand.Address = ResolveModRmMemoryAddressInBed(operand,
                                                         operandLengthInBed,
                                                         writeOperandInBed)
        _lastFpuOperandPhysicalInBed = operand.Address And &HFFFFFUI
        Select Case op
            Case &HD8
                _numericCoprocessor.ExecuteSt0Arithmetic(
                    operand.Reg,
                    CDbl(ReadSingle(operand.Address)))

            Case &HD9
                Select Case operand.Reg
                    Case 0 ' FLD m32real
                        _numericCoprocessor.Push(CDbl(ReadSingle(operand.Address)))

                    Case 2 ' FST m32real
                        StoreFpuSingle(operand.Address, popAfterInBed:=False)

                    Case 3 ' FSTP m32real
                        StoreFpuSingle(operand.Address, popAfterInBed:=True)

                    Case 4 ' FLDENV m14byte (real-address 80287 format)
                        LoadFpuEnvironment14(operand.Address)

                    Case 5 ' FLDCW
                        _numericCoprocessor.ControlWord = ReadWord(operand.Address)

                    Case 6 ' FNSTENV m14byte
                        StoreFpuEnvironment14(operand.Address)
                        _numericCoprocessor.MaskAllExceptions()

                    Case 7 ' FNSTCW
                        WriteWord(operand.Address, _numericCoprocessor.ControlWord)
                End Select

            Case &HDA
                _numericCoprocessor.ExecuteSt0Arithmetic(
                    operand.Reg,
                    CDbl(ReadSignedInteger32(operand.Address)))

            Case &HDB
                Select Case operand.Reg
                    Case 0 ' FILD m32int
                        _numericCoprocessor.Push(CDbl(ReadSignedInteger32(operand.Address)))

                    Case 2 ' FIST m32int
                        StoreFpuInteger32(operand.Address, popAfterInBed:=False)

                    Case 3 ' FISTP m32int
                        StoreFpuInteger32(operand.Address, popAfterInBed:=True)

                    Case 5 ' FLD m80real
                        _numericCoprocessor.Push(ReadExtendedReal80(operand.Address))

                    Case 7 ' FSTP m80real
                        StoreFpuExtendedReal80(operand.Address, popAfterInBed:=True)
                End Select

            Case &HDC
                _numericCoprocessor.ExecuteSt0Arithmetic(
                    operand.Reg,
                    ReadDouble(operand.Address))

            Case &HDD
                Select Case operand.Reg
                    Case 0 ' FLD m64real
                        _numericCoprocessor.Push(ReadDouble(operand.Address))

                    Case 2 ' FST m64real
                        StoreFpuDouble(operand.Address, popAfterInBed:=False)

                    Case 3 ' FSTP m64real
                        StoreFpuDouble(operand.Address, popAfterInBed:=True)

                    Case 4 ' FRSTOR m94byte (real-address 80287 format)
                        RestoreFpuState94(operand.Address)

                    Case 6 ' FNSAVE m94byte
                        SaveFpuState94(operand.Address)
                        _numericCoprocessor.Reset()

                    Case 7 ' FNSTSW m2byte
                        WriteWord(operand.Address, _numericCoprocessor.StatusWord)
                End Select

            Case &HDE
                _numericCoprocessor.ExecuteSt0Arithmetic(
                    operand.Reg,
                    CDbl(ReadSignedInteger16(operand.Address)))

            Case &HDF
                Select Case operand.Reg
                    Case 0 ' FILD m16int
                        _numericCoprocessor.Push(CDbl(ReadSignedInteger16(operand.Address)))

                    Case 2 ' FIST m16int
                        StoreFpuInteger16(operand.Address, popAfterInBed:=False)

                    Case 3 ' FISTP m16int
                        StoreFpuInteger16(operand.Address, popAfterInBed:=True)

                    Case 4 ' FBLD m80bcd
                        _numericCoprocessor.Push(ReadPackedBcd80(operand.Address))

                    Case 5 ' FILD m64int
                        _numericCoprocessor.Push(CDbl(ReadSignedInteger64(operand.Address)))

                    Case 6 ' FBSTP m80bcd
                        StoreFpuPackedBcd80(operand.Address)

                    Case 7 ' FISTP m64int
                        StoreFpuInteger64(operand.Address, popAfterInBed:=True)
                End Select
        End Select
    End Sub

    Private Sub StoreFpuSingle(addressInBed As UInteger,
                               popAfterInBed As Boolean)
        Dim valueInBed As Single
        If Not _numericCoprocessor.TryConvertSt0ToSingle(valueInBed) Then Return
        WriteSingle(addressInBed, valueInBed)
        If popAfterInBed Then _numericCoprocessor.Pop()
    End Sub

    Private Sub StoreFpuDouble(addressInBed As UInteger,
                               popAfterInBed As Boolean)
        Dim valueInBed As Double
        If Not _numericCoprocessor.TryReadSt0(valueInBed) Then Return
        WriteDouble(addressInBed, valueInBed)
        If popAfterInBed Then _numericCoprocessor.Pop()
    End Sub

    Private Sub StoreFpuInteger16(addressInBed As UInteger,
                                  popAfterInBed As Boolean)
        Dim valueInBed As Short
        If Not _numericCoprocessor.TryConvertSt0ToInt16(valueInBed) Then Return

        ' Reinterpret signed bits; never ask VB to range-check a negative Short
        ' into UInt16.
        Dim rawInBed As UShort =
            CUShort(CInt(valueInBed) And &HFFFF)
        WriteWord(addressInBed, rawInBed)
        If popAfterInBed Then _numericCoprocessor.Pop()
    End Sub

    Private Sub StoreFpuInteger32(addressInBed As UInteger,
                                  popAfterInBed As Boolean)
        Dim valueInBed As Integer
        If Not _numericCoprocessor.TryConvertSt0ToInt32(valueInBed) Then Return
        WriteSignedInteger32(addressInBed, valueInBed)
        If popAfterInBed Then _numericCoprocessor.Pop()
    End Sub

    Private Sub StoreFpuInteger64(addressInBed As UInteger,
                                  popAfterInBed As Boolean)
        Dim valueInBed As Long
        If Not _numericCoprocessor.TryConvertSt0ToInt64(valueInBed) Then Return
        Dim bytesInBed As Byte() = BitConverter.GetBytes(valueInBed)
        For indexInBed As Integer = 0 To 7
            WriteByte(addressInBed + CUInt(indexInBed), bytesInBed(indexInBed))
        Next
        If popAfterInBed Then _numericCoprocessor.Pop()
    End Sub

    Private Function ReadSignedInteger64(addressInBed As UInteger) As Long
        Dim bytesInBed(7) As Byte
        For indexInBed As Integer = 0 To 7
            bytesInBed(indexInBed) = ReadByte(addressInBed + CUInt(indexInBed))
        Next
        Return BitConverter.ToInt64(bytesInBed, 0)
    End Function

    Private Function ReadPackedBcd80(addressInBed As UInteger) As Double
        Dim valueInBed As Double = 0.0
        Dim placeInBed As Double = 1.0
        For indexInBed As Integer = 0 To 8
            Dim packedInBed As Byte = ReadByte(addressInBed + CUInt(indexInBed))
            valueInBed += CDbl(packedInBed And &HF) * placeInBed
            placeInBed *= 10.0
            valueInBed += CDbl((packedInBed >> 4) And &HF) * placeInBed
            placeInBed *= 10.0
        Next
        If (ReadByte(addressInBed + 9UI) And &H80) <> 0 Then valueInBed = -valueInBed
        Return valueInBed
    End Function

    Private Sub StoreFpuPackedBcd80(addressInBed As UInteger)
        Dim valueInBed As Double
        If Not _numericCoprocessor.TryReadSt0(valueInBed) Then Return
        Dim roundedInBed As Double = Math.Round(valueInBed, MidpointRounding.ToEven)
        Dim magnitudeInBed As Double = Math.Abs(roundedInBed)
        If Double.IsNaN(magnitudeInBed) OrElse Double.IsInfinity(magnitudeInBed) OrElse
           magnitudeInBed >= 1.0E+18 Then
            ' Packed-BCD integer indefinite.
            For indexInBed As Integer = 0 To 8
                WriteByte(addressInBed + CUInt(indexInBed), 0)
            Next
            WriteByte(addressInBed + 9UI, &H80)
            _numericCoprocessor.Pop()
            Return
        End If

        For indexInBed As Integer = 0 To 8
            Dim lowDigitInBed As Integer = CInt(magnitudeInBed Mod 10.0)
            magnitudeInBed = Math.Floor(magnitudeInBed / 10.0)
            Dim highDigitInBed As Integer = CInt(magnitudeInBed Mod 10.0)
            magnitudeInBed = Math.Floor(magnitudeInBed / 10.0)
            WriteByte(addressInBed + CUInt(indexInBed),
                      CByte(lowDigitInBed Or (highDigitInBed << 4)))
        Next
        WriteByte(addressInBed + 9UI, If(valueInBed < 0.0, CByte(&H80), CByte(0)))
        _numericCoprocessor.Pop()
    End Sub

    Private Function ReadExtendedReal80(addressInBed As UInteger) As Double
        Dim significandBytesInBed(7) As Byte
        For indexInBed As Integer = 0 To 7
            significandBytesInBed(indexInBed) = ReadByte(addressInBed + CUInt(indexInBed))
        Next
        Dim significandInBed As ULong = BitConverter.ToUInt64(significandBytesInBed, 0)
        Dim signExponentInBed As UShort = ReadWord(addressInBed + 8UI)
        Dim exponentInBed As Integer = signExponentInBed And &H7FFF
        Dim negativeInBed As Boolean = (signExponentInBed And &H8000US) <> 0US

        If exponentInBed = 0 AndAlso significandInBed = 0UL Then
            Return If(negativeInBed, -0.0, 0.0)
        End If
        If exponentInBed = &H7FFF Then
            If significandInBed = &H8000000000000000UL Then
                Return If(negativeInBed, Double.NegativeInfinity, Double.PositiveInfinity)
            End If
            Return Double.NaN
        End If

        Dim fractionInBed As Double =
            CDbl(significandInBed And &H7FFFFFFFFFFFFFFFUL) / 9223372036854775808.0 +
            If((significandInBed And &H8000000000000000UL) <> 0UL, 1.0, 0.0)
        Dim valueInBed As Double = fractionInBed * Math.Pow(2.0, exponentInBed - 16383)
        Return If(negativeInBed, -valueInBed, valueInBed)
    End Function

    Private Sub StoreFpuExtendedReal80(addressInBed As UInteger,
                                       popAfterInBed As Boolean)
        Dim valueInBed As Double
        If Not _numericCoprocessor.TryReadSt0(valueInBed) Then Return

        WriteExtendedReal80Value(addressInBed, valueInBed)
        If popAfterInBed Then _numericCoprocessor.Pop()
    End Sub

    Private Sub WriteExtendedReal80Value(addressInBed As UInteger, valueInBed As Double)

        Dim significandInBed As ULong
        Dim signExponentInBed As UShort
        If Double.IsNaN(valueInBed) Then
            significandInBed = &HC000000000000000UL
            signExponentInBed = &H7FFFUS
        ElseIf Double.IsInfinity(valueInBed) Then
            significandInBed = &H8000000000000000UL
            signExponentInBed = CUShort(&H7FFFUS Or If(valueInBed < 0.0, &H8000US, 0US))
        ElseIf valueInBed = 0.0 Then
            significandInBed = 0UL
            signExponentInBed = If(BitConverter.DoubleToInt64Bits(valueInBed) < 0, &H8000US, 0US)
        Else
            Dim absoluteInBed As Double = Math.Abs(valueInBed)
            Dim exponentInBed As Integer = CInt(Math.Floor(Math.Log(absoluteInBed, 2.0)))
            Dim fractionInBed As Double = absoluteInBed / Math.Pow(2.0, exponentInBed)
            significandInBed = &H8000000000000000UL Or
                CULng((fractionInBed - 1.0) * 9223372036854775808.0)
            signExponentInBed = CUShort((exponentInBed + 16383) And &H7FFF)
            If valueInBed < 0.0 Then signExponentInBed = CUShort(signExponentInBed Or &H8000US)
        End If

        Dim bytesInBed As Byte() = BitConverter.GetBytes(significandInBed)
        For indexInBed As Integer = 0 To 7
            WriteByte(addressInBed + CUInt(indexInBed), bytesInBed(indexInBed))
        Next
        WriteWord(addressInBed + 8UI, signExponentInBed)
    End Sub

    Private Sub StoreFpuEnvironment14(addressInBed As UInteger)
        WriteWord(addressInBed, _numericCoprocessor.ControlWord)
        WriteWord(addressInBed + 2UI, _numericCoprocessor.StatusWord)
        WriteWord(addressInBed + 4UI, _numericCoprocessor.TagWord)
        If _numericCoprocessor.ProtectedMode Then
            WriteWord(addressInBed + 6UI, _lastFpuInstructionOffsetInBed)
            WriteWord(addressInBed + 8UI, _lastFpuInstructionSelectorInBed)
            WriteWord(addressInBed + 10UI, _lastFpuOperandOffsetInBed)
            WriteWord(addressInBed + 12UI, _lastFpuOperandSelectorInBed)
        Else
            ' Real-address 80287 image: 20-bit physical pointers.  The high
            ' instruction-pointer nibble shares its word with the 11-bit ESC
            ' opcode image; the data-pointer high nibble occupies bits 15-12.
            WriteWord(addressInBed + 6UI, CUShort(_lastFpuInstructionPhysicalInBed And &HFFFFUI))
            WriteWord(addressInBed + 8UI,
                      CUShort(((_lastFpuInstructionPhysicalInBed >> 4) And &HF000UI) Or
                              (_lastFpuOpcodeWordInBed And &H7FFUS)))
            WriteWord(addressInBed + 10UI, CUShort(_lastFpuOperandPhysicalInBed And &HFFFFUI))
            WriteWord(addressInBed + 12UI,
                      CUShort((_lastFpuOperandPhysicalInBed >> 4) And &HF000UI))
        End If
    End Sub

    Private Sub LoadFpuEnvironment14(addressInBed As UInteger)
        _numericCoprocessor.LoadEnvironmentState(ReadWord(addressInBed),
                                                 ReadWord(addressInBed + 2UI),
                                                 ReadWord(addressInBed + 4UI))
        If _numericCoprocessor.ProtectedMode Then
            _lastFpuInstructionOffsetInBed = ReadWord(addressInBed + 6UI)
            _lastFpuInstructionSelectorInBed = ReadWord(addressInBed + 8UI)
            _lastFpuOperandOffsetInBed = ReadWord(addressInBed + 10UI)
            _lastFpuOperandSelectorInBed = ReadWord(addressInBed + 12UI)
        Else
            Dim instructionHighOpcodeInBed As UInt16 = ReadWord(addressInBed + 8UI)
            _lastFpuInstructionPhysicalInBed =
                CUInt(ReadWord(addressInBed + 6UI)) Or
                (CUInt(instructionHighOpcodeInBed And &HF000US) << 4)
            _lastFpuOpcodeWordInBed = CUShort(instructionHighOpcodeInBed And &H7FFUS)
            _lastFpuOperandPhysicalInBed =
                CUInt(ReadWord(addressInBed + 10UI)) Or
                (CUInt(ReadWord(addressInBed + 12UI) And &HF000US) << 4)
        End If
    End Sub

    Private Sub SaveFpuState94(addressInBed As UInteger)
        StoreFpuEnvironment14(addressInBed)
        For logicalInBed As Integer = 0 To 7
            Dim valueInBed As Double
            Dim tagInBed As Byte
            _numericCoprocessor.GetLogicalRegisterImage(logicalInBed, valueInBed, tagInBed)
            WriteExtendedReal80Value(addressInBed + 14UI + CUInt(logicalInBed * 10), valueInBed)
        Next
    End Sub

    Private Sub RestoreFpuState94(addressInBed As UInteger)
        LoadFpuEnvironment14(addressInBed)
        Dim tagWordInBed As UShort = _numericCoprocessor.TagWord
        For logicalInBed As Integer = 0 To 7
            Dim physicalInBed As Integer = (_numericCoprocessor.Top + logicalInBed) And 7
            Dim tagInBed As Byte = CByte((tagWordInBed >> (physicalInBed * 2)) And 3)
            Dim valueInBed As Double = ReadExtendedReal80(addressInBed + 14UI + CUInt(logicalInBed * 10))
            _numericCoprocessor.SetLogicalRegisterImage(logicalInBed, valueInBed, tagInBed)
        Next
    End Sub

    Private Function ReadSingle(address As UInteger) As Single
        Dim bytesInBed(3) As Byte
        For indexInBed As Integer = 0 To 3
            bytesInBed(indexInBed) = ReadByte(address + CUInt(indexInBed))
        Next
        Return BitConverter.ToSingle(bytesInBed, 0)
    End Function

    Private Sub WriteSingle(address As UInteger, value As Single)
        Dim bytesInBed As Byte() = BitConverter.GetBytes(value)
        For indexInBed As Integer = 0 To 3
            WriteByte(address + CUInt(indexInBed), bytesInBed(indexInBed))
        Next
    End Sub

    Private Function ReadDouble(address As UInteger) As Double
        Dim bytesInBed(7) As Byte
        For indexInBed As Integer = 0 To 7
            bytesInBed(indexInBed) = ReadByte(address + CUInt(indexInBed))
        Next
        Return BitConverter.ToDouble(bytesInBed, 0)
    End Function

    Private Sub WriteDouble(address As UInteger, value As Double)
        Dim bytesInBed As Byte() = BitConverter.GetBytes(value)
        For indexInBed As Integer = 0 To 7
            WriteByte(address + CUInt(indexInBed), bytesInBed(indexInBed))
        Next
    End Sub

    Private Function ReadSignedInteger16(address As UInteger) As Short
        Dim rawInBed As UShort = ReadWord(address)
        Dim bytesInBed As Byte() = BitConverter.GetBytes(rawInBed)
        Return BitConverter.ToInt16(bytesInBed, 0)
    End Function

    Private Function ReadSignedInteger32(address As UInteger) As Integer
        Dim bytesInBed(3) As Byte
        For indexInBed As Integer = 0 To 3
            bytesInBed(indexInBed) = ReadByte(address + CUInt(indexInBed))
        Next
        Return BitConverter.ToInt32(bytesInBed, 0)
    End Function

    Private Sub WriteSignedInteger32(address As UInteger, value As Integer)
        Dim bytesInBed As Byte() = BitConverter.GetBytes(value)
        For indexInBed As Integer = 0 To 3
            WriteByte(address + CUInt(indexInBed), bytesInBed(indexInBed))
        Next
    End Sub

    Private Structure LoadallCacheEntryInBed
        Public BaseAddress As UInteger
        Public Access As Byte
        Public Limit As UInt16
    End Structure

    Private Function ReadLoadallCacheEntryInBed(addressInBed As UInteger) As LoadallCacheEntryInBed
        Return New LoadallCacheEntryInBed With {
            .BaseAddress = CUInt(ReadByte(addressInBed)) Or (CUInt(ReadByte(addressInBed + 1UI)) << 8) Or (CUInt(ReadByte(addressInBed + 2UI)) << 16),
            .Access = ReadByte(addressInBed + 3UI),
            .Limit = ReadWord(addressInBed + 4UI)
        }
    End Function

    Private Sub InstallLoadallSegmentCacheInBed(segmentIndexInBed As Integer,
                                                selectorInBed As UInt16,
                                                cacheInBed As LoadallCacheEntryInBed)
        _segmentSelectors(segmentIndexInBed) = selectorInBed
        _segmentBases(segmentIndexInBed) = cacheInBed.BaseAddress And &HFFFFFFUI
        _segmentLimits(segmentIndexInBed) = cacheInBed.Limit
        _segmentAccess(segmentIndexInBed) = cacheInBed.Access
        _segmentValid(segmentIndexInBed) = (cacheInBed.Access And &H80) <> 0
    End Sub

    Private Sub ExecuteLoadall286InBed()
        Dim wasProtectedInBed As Boolean = ProtectedMode
        If wasProtectedInBed AndAlso CurrentPrivilegeLevelInBed() <> 0 Then
            RaiseCpuException(13, "LOADALL requires CPL 0", True, 0US)
            Return
        End If

        ' Intel test-instruction image: 102 bytes beginning at physical 000800h.
        Dim requestedMswInBed As UInt16 = ReadWord(&H806UI)
        Dim newTaskSelectorInBed As UInt16 = ReadWord(&H816UI)
        Dim newFlagsInBed As UInt16 = ReadWord(&H818UI)
        Dim newIpInBed As UInt16 = ReadWord(&H81AUI)
        Dim newLdtSelectorInBed As UInt16 = ReadWord(&H81CUI)
        Dim newDsInBed As UInt16 = ReadWord(&H81EUI)
        Dim newSsInBed As UInt16 = ReadWord(&H820UI)
        Dim newCsInBed As UInt16 = ReadWord(&H822UI)
        Dim newEsInBed As UInt16 = ReadWord(&H824UI)
        Dim newDiInBed As UInt16 = ReadWord(&H826UI)
        Dim newSiInBed As UInt16 = ReadWord(&H828UI)
        Dim newBpInBed As UInt16 = ReadWord(&H82AUI)
        Dim newSpInBed As UInt16 = ReadWord(&H82CUI)
        Dim newBxInBed As UInt16 = ReadWord(&H82EUI)
        Dim newDxInBed As UInt16 = ReadWord(&H830UI)
        Dim newCxInBed As UInt16 = ReadWord(&H832UI)
        Dim newAxInBed As UInt16 = ReadWord(&H834UI)
        Dim esCacheInBed As LoadallCacheEntryInBed = ReadLoadallCacheEntryInBed(&H836UI)
        Dim csCacheInBed As LoadallCacheEntryInBed = ReadLoadallCacheEntryInBed(&H83CUI)
        Dim ssCacheInBed As LoadallCacheEntryInBed = ReadLoadallCacheEntryInBed(&H842UI)
        Dim dsCacheInBed As LoadallCacheEntryInBed = ReadLoadallCacheEntryInBed(&H848UI)
        Dim gdtCacheInBed As LoadallCacheEntryInBed = ReadLoadallCacheEntryInBed(&H84EUI)
        Dim ldtCacheInBed As LoadallCacheEntryInBed = ReadLoadallCacheEntryInBed(&H854UI)
        Dim idtCacheInBed As LoadallCacheEntryInBed = ReadLoadallCacheEntryInBed(&H85AUI)
        Dim tssCacheInBed As LoadallCacheEntryInBed = ReadLoadallCacheEntryInBed(&H860UI)

        ' Unlike LMSW, the undocumented 80286 LOADALL operation replaces the
        ' complete low MSW nibble and can therefore clear PE.  Protected-mode
        ' hosts use exactly that property to return to real mode without a
        ' motherboard reset.  Do not preserve the pre-LOADALL PE state.
        Dim lowMswInBed As UInt16 = CUShort(requestedMswInBed And &HFUS)
        MachineStatusWord = CUShort(&HFFF0US Or lowMswInBed)
        _protectedModeCsLoadedInBed = (lowMswInBed And 1US) <> 0
        TaskRegister = newTaskSelectorInBed
        LocalDescriptorTableRegister = newLdtSelectorInBed
        IP = newIpInBed
        AX = newAxInBed : BX = newBxInBed : CX = newCxInBed : DX = newDxInBed
        SP = newSpInBed : BP = newBpInBed : SI = newSiInBed : DI = newDiInBed
        InstallLoadallSegmentCacheInBed(0, newEsInBed, esCacheInBed)
        InstallLoadallSegmentCacheInBed(1, newCsInBed, csCacheInBed)
        InstallLoadallSegmentCacheInBed(2, newSsInBed, ssCacheInBed)
        InstallLoadallSegmentCacheInBed(3, newDsInBed, dsCacheInBed)
        GdtrBase = gdtCacheInBed.BaseAddress : GdtrLimit = gdtCacheInBed.Limit
        IdtrBase = idtCacheInBed.BaseAddress : IdtrLimit = idtCacheInBed.Limit
        _ldtBaseInBed = ldtCacheInBed.BaseAddress : _ldtLimitInBed = ldtCacheInBed.Limit : _ldtAccessInBed = ldtCacheInBed.Access : _ldtValidInBed = (ldtCacheInBed.Access And &H80) <> 0
        _taskBaseInBed = tssCacheInBed.BaseAddress : _taskLimitInBed = tssCacheInBed.Limit : _taskAccessInBed = tssCacheInBed.Access : _taskValidInBed = (tssCacheInBed.Access And &H80) <> 0
        Flags = NormalizeFlags(newFlagsInBed)
        FlushPrefetchQueueInBed()
    End Sub

    Private Sub FlushPrefetchQueueInBed()
        _prefetchCountInBed = 0
    End Sub

    Private Sub ExecuteSystemOpcode()
        Dim op2 As Byte = FetchByte()
        RecordFusedSecondOpcodeInBed(op2)
        Select Case op2
            Case &H0
                ExecuteSystemGroup0()
            Case &H1
                ExecuteSystemGroup1()
            Case &H2
                ExecuteLar()
            Case &H3
                ExecuteLsl()
            Case &H5
                RequireCpl0InBed("LOADALL")
                ExecuteLoadall286InBed()
            Case &H6
                RequireCpl0InBed("CLTS")
                MachineStatusWord = CUShort(MachineStatusWord And Not &H8US)
            Case Else : RaiseCpuException(6, "Unsupported 286 extended opcode 0F " & op2.ToString("X2"))
        End Select
    End Sub

    Private Sub ExecuteSystemGroup0()
        Dim operand As ModRM = DecodeModRM()
        Select Case operand.Reg
            Case 0
                WriteRM16(operand, LocalDescriptorTableRegister)
            Case 1
                WriteRM16(operand, TaskRegister)
            Case 2
                RequireCpl0InBed("LLDT")
                LoadLocalDescriptorTable(ReadRM16(operand))
            Case 3
                RequireCpl0InBed("LTR")
                LoadTaskRegister(ReadRM16(operand))
            Case 4
                SetFlag(ZF, SelectorCanRead(ReadRM16(operand), False))
            Case 5
                SetFlag(ZF, SelectorCanRead(ReadRM16(operand), True))
            Case Else
                RaiseCpuException(6, "Invalid 0F 00 system instruction")
        End Select
    End Sub

    Private Sub ExecuteSystemGroup1()
        Dim operand As ModRM = DecodeModRM()
        Select Case operand.Reg
            Case 0
                StoreDescriptorTable(operand, GdtrLimit, GdtrBase)
            Case 1
                StoreDescriptorTable(operand, IdtrLimit, IdtrBase)
            Case 2
                RequireCpl0InBed("LGDT")
                LoadDescriptorTable(operand, True)
            Case 3
                RequireCpl0InBed("LIDT")
                LoadDescriptorTable(operand, False)
            Case 4
                WriteRM16(operand, MachineStatusWord)
            Case 6
                RequireCpl0InBed("LMSW")
                Dim requested As UInt16 = ReadRM16(operand)
                ' PE cannot be cleared by LMSW on a 286; RESET is required.
                Dim enteringProtectedModeInBed As Boolean =
                    Not ProtectedMode AndAlso (requested And 1US) <> 0US
                If enteringProtectedModeInBed Then _protectedModeCsLoadedInBed = False
                If enteringProtectedModeInBed AndAlso
                   _diagnosticRealModeDosObservedInBed AndAlso
                   _forensicTraceWriterInBed Is Nothing Then
                    If Not _diagnosticImportantIntTraceEnabled Then
                        BeginDiagnosticImportantIntTrace(False)
                    Else
                        BeginForensicTraceInBed()
                    End If
                    TraceDiagnosticDpmi(
                        "FORENSIC AUTO-ARM: first protected-mode entry after DOS INT 21h" &
                        " at=" & _instructionStartCs.ToString("X4") & ":" &
                        _instructionStartIp.ToString("X4"))
                End If
                MachineStatusWord = CUShort((MachineStatusWord And &HFFF1US) Or (requested And &HFUS))
            Case Else
                RaiseCpuException(6, "Invalid 0F 01 system instruction")
        End Select
    End Sub

    Private Sub StoreDescriptorTable(operand As ModRM, limit As UInt16, baseAddress As UInteger)
        If operand.ModValue = 3 Then RaiseCpuException(6, "Descriptor table instruction requires memory") : Return
        Dim tableImageAddressInBed As UInteger = ResolveModRmMemoryAddressInBed(operand, 6, True)
        WriteWord(tableImageAddressInBed, limit)
        WriteByte(tableImageAddressInBed + 2, CByte(baseAddress And &HFFUI))
        WriteByte(tableImageAddressInBed + 3, CByte((baseAddress >> 8) And &HFFUI))
        WriteByte(tableImageAddressInBed + 4, CByte((baseAddress >> 16) And &HFFUI))
        WriteByte(tableImageAddressInBed + 5, 0)
    End Sub

    Private Sub LoadDescriptorTable(operand As ModRM, globalTable As Boolean)
        If operand.ModValue = 3 Then RaiseCpuException(6, "Descriptor table instruction requires memory") : Return
        Dim tableImageAddressInBed As UInteger = ResolveModRmMemoryAddressInBed(operand, 6)
        Dim limit As UInt16 = ReadWord(tableImageAddressInBed)
        Dim baseAddress As UInteger = ReadByte(tableImageAddressInBed + 2) Or (CUInt(ReadByte(tableImageAddressInBed + 3)) << 8) Or (CUInt(ReadByte(tableImageAddressInBed + 4)) << 16)
        If globalTable Then
            GdtrLimit = limit : GdtrBase = baseAddress
        Else
            IdtrLimit = limit : IdtrBase = baseAddress
        End If
    End Sub

    Private Sub ExecuteLar()
        Dim operand As ModRM = DecodeModRM()
        Dim selector As UInt16 = ReadRM16(operand)
        Dim descriptor As Descriptor286
        If TryReadDescriptor(selector, descriptor) AndAlso DescriptorVisibleToProbe(selector, descriptor.Access) AndAlso DescriptorTypeAcceptedByLar(descriptor.Access) Then
            ' VB applies the shift using the Byte operand's width, so shifting
            ' a Byte by eight wraps the shift count and leaves it in AL.  The
            ' 80286 LAR result places the descriptor access-rights byte in
            ' bits 15:8 and clears the undefined/reserved low byte here.
            SetReg16(operand.Reg, CUShort(CUInt(descriptor.Access) << 8))
            SetFlag(ZF, True)
        Else
            SetFlag(ZF, False)
        End If
    End Sub

    Private Sub ExecuteLsl()
        Dim operand As ModRM = DecodeModRM()
        Dim selector As UInt16 = ReadRM16(operand)
        Dim descriptor As Descriptor286
        If TryReadDescriptor(selector, descriptor) AndAlso DescriptorVisibleToProbe(selector, descriptor.Access) AndAlso DescriptorTypeAcceptedByLsl(descriptor.Access) Then
            SetReg16(operand.Reg, descriptor.Limit)
            SetFlag(ZF, True)
        Else
            SetFlag(ZF, False)
        End If
    End Sub

    Private Structure Descriptor286
        Public BaseAddress As UInteger
        Public Limit As UInt16
        Public Access As Byte
        Public DescriptorAddress As UInteger
    End Structure

    Private Function TryReadDescriptor(selector As UInt16, ByRef descriptor As Descriptor286) As Boolean
        If (selector And &HFFF8US) = 0 Then Return False
        Dim tableBase As UInteger = GdtrBase
        Dim tableLimit As UInt16 = GdtrLimit
        If (selector And 4) <> 0 Then
            If Not _ldtValidInBed Then Return False
            tableBase = _ldtBaseInBed : tableLimit = _ldtLimitInBed
        End If
        Dim offset As UInteger = CUInt(selector And &HFFF8US)
        If offset + 7UI > tableLimit Then Return False
        descriptor.DescriptorAddress = tableBase + offset
        descriptor.Limit = ReadWord(descriptor.DescriptorAddress)
        descriptor.BaseAddress = ReadWord(descriptor.DescriptorAddress + 2UI) Or (CUInt(ReadByte(descriptor.DescriptorAddress + 4UI)) << 16)
        descriptor.Access = ReadByte(descriptor.DescriptorAddress + 5UI)
        Return True
    End Function

    Private Function TryReadGdtDescriptor(selector As UInt16, ByRef descriptor As Descriptor286) As Boolean
        Dim offset As UInteger = CUInt(selector And &HFFF8US)
        If offset + 7UI > GdtrLimit Then Return False
        descriptor.DescriptorAddress = GdtrBase + offset
        descriptor.Limit = ReadWord(descriptor.DescriptorAddress)
        descriptor.BaseAddress = ReadWord(descriptor.DescriptorAddress + 2UI) Or (CUInt(ReadByte(descriptor.DescriptorAddress + 4UI)) << 16)
        descriptor.Access = ReadByte(descriptor.DescriptorAddress + 5UI)
        Return True
    End Function

    Private Function DescriptorVisible(selector As UInt16, access As Byte) As Boolean
        If (access And &H80) = 0 Then Return False
        Return DescriptorVisibleToProbe(selector, access)
    End Function

    ' LAR, LSL, VERR, and VERW are non-faulting selector probes.  The 80286
    ' checks table bounds, type, and privilege for these instructions, but it
    ' deliberately does not require P=1.  Operating systems use this property
    ' to inspect movable/discarded segments before arranging to make them
    ' present.  Segment-register loads still go through DescriptorVisible and
    ' therefore continue to enforce the Present bit.
    Private Function DescriptorVisibleToProbe(selector As UInt16, access As Byte) As Boolean
        Dim descriptorType As Integer = access And &H1F
        Dim isConformingCode As Boolean = (descriptorType And &H1C) = &H1C
        If isConformingCode Then Return True
        Dim privilege As Integer = Math.Max(selector And 3, CS And 3)
        Return privilege <= ((access >> 5) And 3)
    End Function

    Private Shared Function DescriptorTypeAcceptedByLar(access As Byte) As Boolean
        Dim descriptorType As Integer = access And &H1F
        If (descriptorType And &H10) <> 0 Then Return True ' Code or data segment.
        Select Case descriptorType
            Case 1, 2, 3, 4, 5 ' TSS, LDT, call gate, or task gate.
                Return True
            Case Else          ' Reserved, interrupt gate, or trap gate.
                Return False
        End Select
    End Function

    Private Shared Function DescriptorTypeAcceptedByLsl(access As Byte) As Boolean
        Dim descriptorType As Integer = access And &H1F
        If (descriptorType And &H10) <> 0 Then Return True ' Code or data segment.
        Return descriptorType = 1 OrElse descriptorType = 2 OrElse descriptorType = 3 ' TSS or LDT.
    End Function

    Private Function SelectorCanRead(selector As UInt16, requireWrite As Boolean) As Boolean
        Dim descriptor As Descriptor286
        If Not TryReadDescriptor(selector, descriptor) OrElse Not DescriptorVisibleToProbe(selector, descriptor.Access) Then Return False
        Dim descriptorType As Integer = descriptor.Access And &H1F
        Dim isCode As Boolean = (descriptorType And &H18) = &H18
        If isCode Then Return Not requireWrite AndAlso (descriptorType And 2) <> 0
        Dim isData As Boolean = (descriptorType And &H10) <> 0
        If Not isData Then Return False
        Return Not requireWrite OrElse (descriptorType And 2) <> 0
    End Function

    Private Sub AssignSegment(segmentIndex As Integer, selector As UInt16)
        If Not ProtectedMode Then
            _segmentSelectors(segmentIndex) = selector
            _segmentBases(segmentIndex) = CUInt(selector) << 4
            _segmentLimits(segmentIndex) = &HFFFFUS
            _segmentAccess(segmentIndex) = If(segmentIndex = 1, CByte(&H9B), CByte(&H93))
            _segmentValid(segmentIndex) = True
            Return
        End If

        If (selector And &HFFF8US) = 0 Then
            If segmentIndex = 1 OrElse segmentIndex = 2 Then
                RaiseCpuException(13, "Null selector cannot be loaded into CS or SS", True, 0US)
                Return
            End If
            _segmentSelectors(segmentIndex) = 0US
            _segmentBases(segmentIndex) = 0UI
            _segmentLimits(segmentIndex) = 0US
            _segmentAccess(segmentIndex) = 0
            _segmentValid(segmentIndex) = False
            Return
        End If

        Dim descriptor As Descriptor286
        If Not TryReadDescriptor(selector, descriptor) Then
            RaiseCpuException(13, "Selector lies outside its descriptor table", True, SelectorErrorCodeInBed(selector))
            Return
        End If
        Dim descriptorIsCodeDataInBed As Boolean = (descriptor.Access And &H10) <> 0
        Dim codeSegment As Boolean = (descriptor.Access And &H8) <> 0
        Dim readableWritable As Boolean = (descriptor.Access And 2) <> 0
        Dim dpl As Integer = (descriptor.Access >> 5) And 3
        Dim cpl As Integer = CurrentPrivilegeLevelInBed()
        Dim rpl As Integer = selector And 3
        If Not descriptorIsCodeDataInBed Then
            RaiseCpuException(13, "System descriptor cannot be loaded as a segment", True, SelectorErrorCodeInBed(selector))
            Return
        End If

        Select Case segmentIndex
            Case 1
                If Not codeSegment Then
                    RaiseCpuException(13, "CS requires a code descriptor", True, SelectorErrorCodeInBed(selector))
                    Return
                End If
                Dim conforming As Boolean = (descriptor.Access And 4) <> 0
                If (Not conforming AndAlso (dpl <> cpl OrElse rpl > cpl)) OrElse (conforming AndAlso dpl > cpl) Then
                    RaiseCpuException(13, "Code-segment privilege violation", True, SelectorErrorCodeInBed(selector))
                    Return
                End If
            Case 2
                If codeSegment OrElse Not readableWritable OrElse dpl <> cpl OrElse rpl <> cpl Then
                    RaiseCpuException(13, "SS requires writable data at CPL", True, SelectorErrorCodeInBed(selector))
                    Return
                End If
            Case Else
                If codeSegment AndAlso Not readableWritable Then
                    RaiseCpuException(13, "Unreadable code selector", True, SelectorErrorCodeInBed(selector))
                    Return
                End If
                If Not DescriptorVisible(selector, descriptor.Access) Then
                    RaiseCpuException(13, "Data-segment privilege violation", True, SelectorErrorCodeInBed(selector))
                    Return
                End If
        End Select
        If (descriptor.Access And &H80) = 0 Then
            If segmentIndex = 2 Then
                RaiseCpuException(12, "Stack segment is not present", True, SelectorErrorCodeInBed(selector))
            Else
                RaiseCpuException(11, "Segment is not present", True, SelectorErrorCodeInBed(selector))
            End If
            Return
        End If

        CacheSegmentDescriptor(segmentIndex, selector, descriptor)
    End Sub

    Private Enum TaskSwitchKindInBed
        Jump = 0
        NestedCall = 1
        Iret = 2
    End Enum

    Private Structure TaskImage286InBed
        Public Backlink As UInt16
        Public Sp0 As UInt16
        Public Ss0 As UInt16
        Public Sp1 As UInt16
        Public Ss1 As UInt16
        Public Sp2 As UInt16
        Public Ss2 As UInt16
        Public Ip As UInt16
        Public Flags As UInt16
        Public Ax As UInt16
        Public Cx As UInt16
        Public Dx As UInt16
        Public Bx As UInt16
        Public Sp As UInt16
        Public Bp As UInt16
        Public Si As UInt16
        Public Di As UInt16
        Public Es As UInt16
        Public Cs As UInt16
        Public Ss As UInt16
        Public Ds As UInt16
        Public Ldt As UInt16
    End Structure

    Private Function ReadTaskImageInBed(baseInBed As UInteger) As TaskImage286InBed
        Return New TaskImage286InBed With {
            .Backlink = ReadWord(baseInBed + 0UI),
            .Sp0 = ReadWord(baseInBed + 2UI), .Ss0 = ReadWord(baseInBed + 4UI),
            .Sp1 = ReadWord(baseInBed + 6UI), .Ss1 = ReadWord(baseInBed + 8UI),
            .Sp2 = ReadWord(baseInBed + 10UI), .Ss2 = ReadWord(baseInBed + 12UI),
            .Ip = ReadWord(baseInBed + 14UI), .Flags = ReadWord(baseInBed + 16UI),
            .Ax = ReadWord(baseInBed + 18UI), .Cx = ReadWord(baseInBed + 20UI),
            .Dx = ReadWord(baseInBed + 22UI), .Bx = ReadWord(baseInBed + 24UI),
            .Sp = ReadWord(baseInBed + 26UI), .Bp = ReadWord(baseInBed + 28UI),
            .Si = ReadWord(baseInBed + 30UI), .Di = ReadWord(baseInBed + 32UI),
            .Es = ReadWord(baseInBed + 34UI), .Cs = ReadWord(baseInBed + 36UI),
            .Ss = ReadWord(baseInBed + 38UI), .Ds = ReadWord(baseInBed + 40UI),
            .Ldt = ReadWord(baseInBed + 42UI)
        }
    End Function

    Private Sub SaveCurrentTaskDynamicStateInBed()
        If Not _taskValidInBed OrElse _taskLimitInBed < &H2BUS Then Return
        WriteWord(_taskBaseInBed + 14UI, IP)
        WriteWord(_taskBaseInBed + 16UI, NormalizeFlags(Flags))
        WriteWord(_taskBaseInBed + 18UI, AX) : WriteWord(_taskBaseInBed + 20UI, CX)
        WriteWord(_taskBaseInBed + 22UI, DX) : WriteWord(_taskBaseInBed + 24UI, BX)
        WriteWord(_taskBaseInBed + 26UI, SP) : WriteWord(_taskBaseInBed + 28UI, BP)
        WriteWord(_taskBaseInBed + 30UI, SI) : WriteWord(_taskBaseInBed + 32UI, DI)
        WriteWord(_taskBaseInBed + 34UI, ES) : WriteWord(_taskBaseInBed + 36UI, CS)
        WriteWord(_taskBaseInBed + 38UI, SS) : WriteWord(_taskBaseInBed + 40UI, DS)
        WriteWord(_taskBaseInBed + 42UI, LocalDescriptorTableRegister)
    End Sub

    Private Sub SetTssBusyBitInBed(descriptorInBed As Descriptor286, busyInBed As Boolean)
        Dim accessInBed As Byte = descriptorInBed.Access
        Dim newTypeInBed As Integer = If(busyInBed, 3, 1)
        Dim newAccessInBed As Byte = CByte((accessInBed And &HF0) Or newTypeInBed)
        WriteByte(descriptorInBed.DescriptorAddress + 5UI, newAccessInBed)
    End Sub

    Private Function TryStageTaskLdtInBed(selectorInBed As UInt16,
                                          ByRef descriptorInBed As Descriptor286,
                                          ByRef validInBed As Boolean) As Boolean
        validInBed = False
        If (selectorInBed And &HFFF8US) = 0 Then Return True
        If (selectorInBed And 4US) <> 0US Then Return False
        If Not TryReadGdtDescriptor(selectorInBed, descriptorInBed) Then Return False
        If (descriptorInBed.Access And &H1F) <> 2 OrElse (descriptorInBed.Access And &H80) = 0 Then Return False
        validInBed = True
        Return True
    End Function

    Private Function TryReadDescriptorForTaskInBed(selectorInBed As UInt16,
                                                   ldtDescriptorInBed As Descriptor286,
                                                   ldtValidInBed As Boolean,
                                                   ByRef descriptorInBed As Descriptor286) As Boolean
        If (selectorInBed And &HFFF8US) = 0 Then Return False
        Dim tableBaseInBed As UInteger = GdtrBase
        Dim tableLimitInBed As UInt16 = GdtrLimit
        If (selectorInBed And 4US) <> 0US Then
            If Not ldtValidInBed Then Return False
            tableBaseInBed = ldtDescriptorInBed.BaseAddress
            tableLimitInBed = ldtDescriptorInBed.Limit
        End If
        Dim offsetInBed As UInteger = CUInt(selectorInBed And &HFFF8US)
        If offsetInBed + 7UI > tableLimitInBed Then Return False
        descriptorInBed.DescriptorAddress = tableBaseInBed + offsetInBed
        descriptorInBed.Limit = ReadWord(descriptorInBed.DescriptorAddress)
        descriptorInBed.BaseAddress = ReadWord(descriptorInBed.DescriptorAddress + 2UI) Or (CUInt(ReadByte(descriptorInBed.DescriptorAddress + 4UI)) << 16)
        descriptorInBed.Access = ReadByte(descriptorInBed.DescriptorAddress + 5UI)
        Return True
    End Function

    Private Function ValidateTaskSegmentInBed(selectorInBed As UInt16,
                                              segmentIndexInBed As Integer,
                                              cplInBed As Integer,
                                              ldtDescriptorInBed As Descriptor286,
                                              ldtValidInBed As Boolean,
                                              ByRef descriptorInBed As Descriptor286,
                                              ByRef validInBed As Boolean) As Boolean
        validInBed = True
        If (selectorInBed And &HFFF8US) = 0 Then
            If segmentIndexInBed = 0 OrElse segmentIndexInBed = 3 Then
                validInBed = False
                Return True
            End If
            Return False
        End If
        If Not TryReadDescriptorForTaskInBed(selectorInBed, ldtDescriptorInBed, ldtValidInBed, descriptorInBed) Then Return False
        If (descriptorInBed.Access And &H80) = 0 OrElse (descriptorInBed.Access And &H10) = 0 Then Return False
        Dim codeInBed As Boolean = (descriptorInBed.Access And 8) <> 0
        Dim rwInBed As Boolean = (descriptorInBed.Access And 2) <> 0
        Dim dplInBed As Integer = (descriptorInBed.Access >> 5) And 3
        Dim rplInBed As Integer = selectorInBed And 3
        Select Case segmentIndexInBed
            Case 1
                If Not codeInBed Then Return False
                Dim conformingInBed As Boolean = (descriptorInBed.Access And 4) <> 0
                If Not conformingInBed AndAlso (dplInBed <> cplInBed OrElse rplInBed <> cplInBed) Then Return False
                If conformingInBed AndAlso dplInBed > cplInBed Then Return False
            Case 2
                If codeInBed OrElse Not rwInBed OrElse dplInBed <> cplInBed OrElse rplInBed <> cplInBed Then Return False
            Case Else
                If codeInBed AndAlso Not rwInBed Then Return False
                If Not (codeInBed AndAlso (descriptorInBed.Access And 4) <> 0) AndAlso Math.Max(cplInBed, rplInBed) > dplInBed Then Return False
        End Select
        Return True
    End Function

    Private Sub InstallTaskSegmentCacheInBed(segmentIndexInBed As Integer,
                                             selectorInBed As UInt16,
                                             descriptorInBed As Descriptor286,
                                             validInBed As Boolean)
        _segmentSelectors(segmentIndexInBed) = If(validInBed, selectorInBed, 0US)
        If validInBed Then
            _segmentBases(segmentIndexInBed) = descriptorInBed.BaseAddress
            _segmentLimits(segmentIndexInBed) = descriptorInBed.Limit
            _segmentAccess(segmentIndexInBed) = descriptorInBed.Access
            _segmentValid(segmentIndexInBed) = True
        Else
            _segmentBases(segmentIndexInBed) = 0UI : _segmentLimits(segmentIndexInBed) = 0US
            _segmentAccess(segmentIndexInBed) = 0 : _segmentValid(segmentIndexInBed) = False
        End If
    End Sub

    Private Sub PerformTaskSwitchInBed(targetSelectorInBed As UInt16,
                                       kindInBed As TaskSwitchKindInBed)
        If (targetSelectorInBed And &HFFF8US) = 0 OrElse (targetSelectorInBed And 4US) <> 0US Then
            RaiseCpuException(10, "Task switch requires non-null GDT TSS selector", True, SelectorErrorCodeInBed(targetSelectorInBed))
        End If
        Dim targetDescriptorInBed As Descriptor286
        If Not TryReadGdtDescriptor(targetSelectorInBed, targetDescriptorInBed) Then RaiseCpuException(10, "Target TSS selector invalid", True, SelectorErrorCodeInBed(targetSelectorInBed))
        Dim targetTypeInBed As Integer = targetDescriptorInBed.Access And &HF
        If kindInBed = TaskSwitchKindInBed.Iret Then
            If targetTypeInBed <> 3 Then RaiseCpuException(10, "IRET backlink must reference busy TSS", True, SelectorErrorCodeInBed(targetSelectorInBed))
        Else
            If targetTypeInBed <> 1 Then RaiseCpuException(10, "Task target must be available 286 TSS", True, SelectorErrorCodeInBed(targetSelectorInBed))
        End If
        If (targetDescriptorInBed.Access And &H80) = 0 Then RaiseCpuException(11, "Target TSS not present", True, SelectorErrorCodeInBed(targetSelectorInBed))
        If targetDescriptorInBed.Limit < &H2BUS Then RaiseCpuException(10, "Target TSS too small", True, SelectorErrorCodeInBed(targetSelectorInBed))

        Dim imageInBed As TaskImage286InBed = ReadTaskImageInBed(targetDescriptorInBed.BaseAddress)
        Dim ldtDescriptorInBed As Descriptor286
        Dim ldtValidInBed As Boolean
        If Not TryStageTaskLdtInBed(imageInBed.Ldt, ldtDescriptorInBed, ldtValidInBed) Then RaiseCpuException(10, "Task LDT invalid", True, SelectorErrorCodeInBed(imageInBed.Ldt))

        Dim targetCplInBed As Integer = imageInBed.Cs And 3
        Dim esDescInBed, csDescInBed, ssDescInBed, dsDescInBed As Descriptor286
        Dim esValidInBed, csValidInBed, ssValidInBed, dsValidInBed As Boolean
        If Not ValidateTaskSegmentInBed(imageInBed.Cs, 1, targetCplInBed, ldtDescriptorInBed, ldtValidInBed, csDescInBed, csValidInBed) Then RaiseCpuException(10, "Task CS invalid", True, SelectorErrorCodeInBed(imageInBed.Cs))
        If Not ValidateTaskSegmentInBed(imageInBed.Ss, 2, targetCplInBed, ldtDescriptorInBed, ldtValidInBed, ssDescInBed, ssValidInBed) Then RaiseCpuException(10, "Task SS invalid", True, SelectorErrorCodeInBed(imageInBed.Ss))
        If Not ValidateTaskSegmentInBed(imageInBed.Es, 0, targetCplInBed, ldtDescriptorInBed, ldtValidInBed, esDescInBed, esValidInBed) Then RaiseCpuException(10, "Task ES invalid", True, SelectorErrorCodeInBed(imageInBed.Es))
        If Not ValidateTaskSegmentInBed(imageInBed.Ds, 3, targetCplInBed, ldtDescriptorInBed, ldtValidInBed, dsDescInBed, dsValidInBed) Then RaiseCpuException(10, "Task DS invalid", True, SelectorErrorCodeInBed(imageInBed.Ds))
        If CUInt(imageInBed.Ip) > csDescInBed.Limit Then RaiseCpuException(10, "Task IP exceeds CS limit", True, SelectorErrorCodeInBed(imageInBed.Cs))

        Dim oldTaskSelectorInBed As UInt16 = TaskRegister
        Dim oldTaskDescriptorInBed As Descriptor286
        Dim oldTaskValidInBed As Boolean = _taskValidInBed AndAlso TryReadGdtDescriptor(oldTaskSelectorInBed, oldTaskDescriptorInBed)
        SaveCurrentTaskDynamicStateInBed()

        If kindInBed = TaskSwitchKindInBed.Jump AndAlso oldTaskValidInBed Then SetTssBusyBitInBed(oldTaskDescriptorInBed, False)
        If kindInBed <> TaskSwitchKindInBed.Iret Then SetTssBusyBitInBed(targetDescriptorInBed, True)
        If kindInBed = TaskSwitchKindInBed.Iret AndAlso oldTaskValidInBed Then SetTssBusyBitInBed(oldTaskDescriptorInBed, False)
        If kindInBed = TaskSwitchKindInBed.NestedCall Then WriteWord(targetDescriptorInBed.BaseAddress, oldTaskSelectorInBed)

        AX = imageInBed.Ax : CX = imageInBed.Cx : DX = imageInBed.Dx : BX = imageInBed.Bx
        SP = imageInBed.Sp : BP = imageInBed.Bp : SI = imageInBed.Si : DI = imageInBed.Di
        IP = imageInBed.Ip
        Flags = NormalizeFlags(imageInBed.Flags)
        If kindInBed = TaskSwitchKindInBed.NestedCall Then SetFlag(&H4000US, True)
        If kindInBed = TaskSwitchKindInBed.Jump Then SetFlag(&H4000US, False)

        LocalDescriptorTableRegister = imageInBed.Ldt
        If ldtValidInBed Then
            _ldtBaseInBed = ldtDescriptorInBed.BaseAddress : _ldtLimitInBed = ldtDescriptorInBed.Limit
            _ldtAccessInBed = ldtDescriptorInBed.Access : _ldtValidInBed = True
        Else
            _ldtBaseInBed = 0UI : _ldtLimitInBed = 0US : _ldtAccessInBed = 0 : _ldtValidInBed = False
        End If
        InstallTaskSegmentCacheInBed(0, imageInBed.Es, esDescInBed, esValidInBed)
        InstallTaskSegmentCacheInBed(1, imageInBed.Cs, csDescInBed, csValidInBed)
        InstallTaskSegmentCacheInBed(2, imageInBed.Ss, ssDescInBed, ssValidInBed)
        InstallTaskSegmentCacheInBed(3, imageInBed.Ds, dsDescInBed, dsValidInBed)

        TaskRegister = targetSelectorInBed
        _taskBaseInBed = targetDescriptorInBed.BaseAddress : _taskLimitInBed = targetDescriptorInBed.Limit
        _taskAccessInBed = CByte((targetDescriptorInBed.Access And &HF0) Or 3) : _taskValidInBed = True
        MachineStatusWord = CUShort(MachineStatusWord Or &H8US)
    End Sub

    Private Structure SegmentLoadStageInBed
        Public SegmentIndex As Integer
        Public Selector As UInt16
        Public BaseAddress As UInteger
        Public Limit As UInt16
        Public Access As Byte
        Public Valid As Boolean
    End Structure

    Private Function StageSegmentLoadInBed(segmentIndexInBed As Integer,
                                           selectorInBed As UInt16) As SegmentLoadStageInBed
        Dim stageInBed As New SegmentLoadStageInBed With {
            .SegmentIndex = segmentIndexInBed,
            .Selector = selectorInBed,
            .Valid = True
        }
        If Not ProtectedMode Then
            stageInBed.BaseAddress = (CUInt(selectorInBed) << 4) And &HFFFFFFUI
            stageInBed.Limit = &HFFFFUS
            stageInBed.Access = If(segmentIndexInBed = 1, CByte(&H9B), CByte(&H93))
            Return stageInBed
        End If

        If (selectorInBed And &HFFF8US) = 0 Then
            If segmentIndexInBed = 1 OrElse segmentIndexInBed = 2 Then
                RaiseCpuException(13, "Null selector cannot be loaded into CS or SS", True, 0US)
            End If
            stageInBed.Selector = 0US
            stageInBed.BaseAddress = 0UI
            stageInBed.Limit = 0US
            stageInBed.Access = 0
            stageInBed.Valid = False
            Return stageInBed
        End If

        Dim descriptorInBed As Descriptor286
        If Not TryReadDescriptor(selectorInBed, descriptorInBed) Then
            _diagnosticFaultAccessContextInBed =
                "segment-load target=" & {"ES", "CS", "SS", "DS"}(segmentIndexInBed) &
                " selector=" & selectorInBed.ToString("X4") &
                " GDTR=" & GdtrBase.ToString("X6") & ":" & GdtrLimit.ToString("X4") &
                " LDTR=" & LocalDescriptorTableRegister.ToString("X4") &
                " LDT=" & _ldtBaseInBed.ToString("X6") & ":" & _ldtLimitInBed.ToString("X4")
            RaiseCpuException(13, "Selector lies outside descriptor table", True, SelectorErrorCodeInBed(selectorInBed))
        End If
        Dim isCodeDataInBed As Boolean = (descriptorInBed.Access And &H10) <> 0
        Dim isCodeInBed As Boolean = (descriptorInBed.Access And 8) <> 0
        Dim rwInBed As Boolean = (descriptorInBed.Access And 2) <> 0
        Dim dplInBed As Integer = (descriptorInBed.Access >> 5) And 3
        Dim cplInBed As Integer = CurrentPrivilegeLevelInBed()
        Dim rplInBed As Integer = selectorInBed And 3
        If Not isCodeDataInBed Then RaiseCpuException(13, "System descriptor cannot be loaded as segment", True, SelectorErrorCodeInBed(selectorInBed))

        Select Case segmentIndexInBed
            Case 1
                If Not isCodeInBed Then RaiseCpuException(13, "CS requires code", True, SelectorErrorCodeInBed(selectorInBed))
                Dim conformingInBed As Boolean = (descriptorInBed.Access And 4) <> 0
                If (Not conformingInBed AndAlso (dplInBed <> cplInBed OrElse rplInBed > cplInBed)) OrElse
                   (conformingInBed AndAlso dplInBed > cplInBed) Then
                    RaiseCpuException(13, "Code privilege violation", True, SelectorErrorCodeInBed(selectorInBed))
                End If
            Case 2
                If isCodeInBed OrElse Not rwInBed OrElse dplInBed <> cplInBed OrElse rplInBed <> cplInBed Then
                    RaiseCpuException(13, "Invalid SS descriptor", True, SelectorErrorCodeInBed(selectorInBed))
                End If
            Case Else
                If isCodeInBed AndAlso Not rwInBed Then RaiseCpuException(13, "Unreadable code segment", True, SelectorErrorCodeInBed(selectorInBed))
                If Math.Max(cplInBed, rplInBed) > dplInBed AndAlso Not (isCodeInBed AndAlso (descriptorInBed.Access And 4) <> 0) Then
                    RaiseCpuException(13, "Data segment privilege violation", True, SelectorErrorCodeInBed(selectorInBed))
                End If
        End Select
        If (descriptorInBed.Access And &H80) = 0 Then
            If segmentIndexInBed = 2 Then
                RaiseCpuException(12, "Stack segment is not present", True, SelectorErrorCodeInBed(selectorInBed))
            Else
                RaiseCpuException(11, "Segment is not present", True, SelectorErrorCodeInBed(selectorInBed))
            End If
        End If

        stageInBed.BaseAddress = descriptorInBed.BaseAddress
        stageInBed.Limit = descriptorInBed.Limit
        stageInBed.Access = descriptorInBed.Access
        Return stageInBed
    End Function

    Private Function StageStackSegmentForPrivilegeInBed(selectorInBed As UInt16,
                                                        privilegeInBed As Integer) As SegmentLoadStageInBed
        If (selectorInBed And &HFFF8US) = 0 Then RaiseCpuException(10, "Null privilege stack selector", True, 0US)
        Dim descriptorInBed As Descriptor286
        If Not TryReadDescriptor(selectorInBed, descriptorInBed) Then RaiseCpuException(10, "Invalid privilege stack selector", True, SelectorErrorCodeInBed(selectorInBed))
        Dim validDataInBed As Boolean = (descriptorInBed.Access And &H18) = &H10 AndAlso (descriptorInBed.Access And 2) <> 0
        Dim dplInBed As Integer = (descriptorInBed.Access >> 5) And 3
        If Not validDataInBed OrElse dplInBed <> privilegeInBed OrElse (selectorInBed And 3) <> privilegeInBed Then
            RaiseCpuException(10, "Invalid privilege stack descriptor", True, SelectorErrorCodeInBed(selectorInBed))
        End If
        If (descriptorInBed.Access And &H80) = 0 Then RaiseCpuException(12, "Privilege stack not present", True, SelectorErrorCodeInBed(selectorInBed))
        Return New SegmentLoadStageInBed With {
            .SegmentIndex = 2,
            .Selector = selectorInBed,
            .BaseAddress = descriptorInBed.BaseAddress,
            .Limit = descriptorInBed.Limit,
            .Access = descriptorInBed.Access,
            .Valid = True
        }
    End Function

    Private Sub CommitSegmentLoadInBed(stageInBed As SegmentLoadStageInBed)
        _segmentSelectors(stageInBed.SegmentIndex) = stageInBed.Selector
        _segmentBases(stageInBed.SegmentIndex) = stageInBed.BaseAddress
        _segmentLimits(stageInBed.SegmentIndex) = stageInBed.Limit
        _segmentAccess(stageInBed.SegmentIndex) = stageInBed.Access
        _segmentValid(stageInBed.SegmentIndex) = stageInBed.Valid
        If stageInBed.SegmentIndex = 1 AndAlso ProtectedMode Then _protectedModeCsLoadedInBed = True
    End Sub

    Private Function PeekStackWordInBed(byteDisplacementInBed As Integer) As UInt16
        Dim offsetInBed As UInt16 = CUShort((CInt(SP) + byteDisplacementInBed) And &HFFFF)
        Return ReadWord(SegmentAddress(2, offsetInBed, 2))
    End Function

    Private Sub ExecutePopSegmentInBed(segmentIndexInBed As Integer)
        Dim selectorInBed As UInt16 = PeekStackWordInBed(0)
        Dim stageInBed As SegmentLoadStageInBed = StageSegmentLoadInBed(segmentIndexInBed, selectorInBed)
        SP = CUShort((CInt(SP) + 2) And &HFFFF)
        CommitSegmentLoadInBed(stageInBed)
    End Sub

    Private Sub ApplyIretFlagsInBed(incomingInBed As UInt16, executingPrivilegeInBed As Integer)
        Dim oldFlagsInBed As UInt16 = Flags
        Dim candidateInBed As UInt16 = NormalizeFlags(incomingInBed)
        Dim oldIoplInBed As Integer = (CInt(oldFlagsInBed) >> 12) And 3
        ' ExecuteIret has already installed the return CS by the time flags are
        ' committed, so CurrentPrivilegeLevelInBed() would describe the return
        ' target rather than the code which executed IRET.  Privilege checks for
        ' restoring IOPL and IF must use the latter.
        If executingPrivilegeInBed <> 0 Then
            candidateInBed = CUShort((candidateInBed And Not &H3000US) Or (oldFlagsInBed And &H3000US))
        End If
        ' IF restoration is governed by the CPL of the code executing IRET,
        ' not the privilege level of the return target.  A ring-0 handler must
        ' be able to restore the interrupted ring-3 task's IF even when IOPL=0.
        If executingPrivilegeInBed > oldIoplInBed Then
            candidateInBed = CUShort((candidateInBed And Not InterruptFlag) Or (oldFlagsInBed And InterruptFlag))
        End If
        Flags = candidateInBed
    End Sub

    Private Sub InvalidateOuterPrivilegeDataSegmentsInBed(newCplInBed As Integer)
        For segmentIndexInBed As Integer = 0 To 3 Step 3
            If Not _segmentValid(segmentIndexInBed) Then Continue For
            Dim accessInBed As Byte = _segmentAccess(segmentIndexInBed)
            Dim isCodeInBed As Boolean = (accessInBed And &H18) = &H18
            Dim conformingInBed As Boolean = isCodeInBed AndAlso (accessInBed And 4) <> 0
            Dim dplInBed As Integer = (accessInBed >> 5) And 3
            If Not conformingInBed AndAlso dplInBed < newCplInBed Then
                _segmentSelectors(segmentIndexInBed) = 0US
                _segmentBases(segmentIndexInBed) = 0UI
                _segmentLimits(segmentIndexInBed) = 0US
                _segmentAccess(segmentIndexInBed) = 0
                _segmentValid(segmentIndexInBed) = False
            End If
        Next
    End Sub

    Private Sub ExecuteFarControlTransferInBed(targetSelectorInBed As UInt16,
                                               targetOffsetInBed As UInt16,
                                               isCallInBed As Boolean)
        If Not ProtectedMode Then
            If isCallInBed Then Push(CS) : Push(IP)
            Dim stageInBed As SegmentLoadStageInBed = StageSegmentLoadInBed(1, targetSelectorInBed)
            CommitSegmentLoadInBed(stageInBed)
            IP = targetOffsetInBed
            Return
        End If

        Dim descriptorInBed As Descriptor286
        If Not TryReadDescriptor(targetSelectorInBed, descriptorInBed) Then
            RaiseCpuException(13, "Far transfer selector invalid", True, SelectorErrorCodeInBed(targetSelectorInBed))
        End If
        Dim systemInBed As Boolean = (descriptorInBed.Access And &H10) = 0
        Dim typeInBed As Integer = descriptorInBed.Access And &HF
        Dim legalSystemTargetInBed As Boolean =
            systemInBed AndAlso (typeInBed = 4 OrElse typeInBed = 1 OrElse typeInBed = 3 OrElse typeInBed = 5)
        Dim legalCodeTargetInBed As Boolean = Not systemInBed AndAlso (descriptorInBed.Access And 8) <> 0
        If Not legalSystemTargetInBed AndAlso Not legalCodeTargetInBed Then
            RaiseCpuException(13, "Far transfer target is not code/gate", True, SelectorErrorCodeInBed(targetSelectorInBed))
        End If
        If systemInBed AndAlso typeInBed = 4 Then
            ExecuteCallGateInBed(targetSelectorInBed, descriptorInBed, isCallInBed)
            Return
        End If
        If systemInBed AndAlso (typeInBed = 1 OrElse typeInBed = 3 OrElse typeInBed = 5) Then
            ExecuteTaskTransferInBed(targetSelectorInBed, descriptorInBed, isCallInBed)
            Return
        End If
        Dim cplInBed As Integer = CurrentPrivilegeLevelInBed()
        Dim rplInBed As Integer = targetSelectorInBed And 3
        Dim dplInBed As Integer = (descriptorInBed.Access >> 5) And 3
        Dim conformingInBed As Boolean = (descriptorInBed.Access And 4) <> 0
        If conformingInBed Then
            If dplInBed > cplInBed Then RaiseCpuException(13, "Conforming code privilege violation", True, SelectorErrorCodeInBed(targetSelectorInBed))
        Else
            If dplInBed <> cplInBed OrElse rplInBed > cplInBed Then RaiseCpuException(13, "Code privilege violation", True, SelectorErrorCodeInBed(targetSelectorInBed))
        End If
        If (descriptorInBed.Access And &H80) = 0 Then
            RaiseCpuException(11, "Far transfer target not present", True, SelectorErrorCodeInBed(targetSelectorInBed))
        End If
        If CUInt(targetOffsetInBed) > descriptorInBed.Limit Then RaiseCpuException(13, "Far transfer offset exceeds code limit", True, 0US)

        If isCallInBed Then
            Push(CS)
            Push(IP)
        End If
        CacheSegmentDescriptor(1, CUShort((targetSelectorInBed And &HFFFCUS) Or cplInBed), descriptorInBed)
        IP = targetOffsetInBed
    End Sub

    Private Sub ExecuteCallGateInBed(gateSelectorInBed As UInt16,
                                     gateDescriptorInBed As Descriptor286,
                                     isCallInBed As Boolean)
        Dim cplInBed As Integer = CurrentPrivilegeLevelInBed()
        Dim gateDplInBed As Integer = (gateDescriptorInBed.Access >> 5) And 3
        If Math.Max(cplInBed, gateSelectorInBed And 3) > gateDplInBed Then
            RaiseCpuException(13, "Call-gate privilege violation", True, SelectorErrorCodeInBed(gateSelectorInBed))
        End If
        If (gateDescriptorInBed.Access And &H80) = 0 Then
            RaiseCpuException(11, "Call gate not present", True, SelectorErrorCodeInBed(gateSelectorInBed))
        End If
        If Not isCallInBed Then
            ' JMP through a call gate may not lower privilege; target validation below enforces this.
        End If

        Dim gateAddressInBed As UInteger = gateDescriptorInBed.DescriptorAddress
        Dim targetOffsetInBed As UInt16 = ReadWord(gateAddressInBed)
        Dim targetSelectorInBed As UInt16 = ReadWord(gateAddressInBed + 2UI)
        Dim parameterCountInBed As Integer = ReadByte(gateAddressInBed + 4UI) And &H1F
        Dim targetDescriptorInBed As Descriptor286
        If Not TryReadDescriptor(targetSelectorInBed, targetDescriptorInBed) Then RaiseCpuException(13, "Call-gate target selector invalid", True, SelectorErrorCodeInBed(targetSelectorInBed))
        If (targetDescriptorInBed.Access And &H18) <> &H18 Then RaiseCpuException(13, "Call-gate target is not code", True, SelectorErrorCodeInBed(targetSelectorInBed))
        Dim targetDplInBed As Integer = (targetDescriptorInBed.Access >> 5) And 3
        Dim conformingInBed As Boolean = (targetDescriptorInBed.Access And 4) <> 0
        If conformingInBed Then
            If targetDplInBed > cplInBed Then RaiseCpuException(13, "Call-gate conforming target privilege violation", True, SelectorErrorCodeInBed(targetSelectorInBed))
            If (targetDescriptorInBed.Access And &H80) = 0 Then RaiseCpuException(11, "Call-gate target not present", True, SelectorErrorCodeInBed(targetSelectorInBed))
            If CUInt(targetOffsetInBed) > targetDescriptorInBed.Limit Then RaiseCpuException(13, "Call-gate offset exceeds limit", True, 0US)
            If isCallInBed Then Push(CS) : Push(IP)
            CacheSegmentDescriptor(1, CUShort((targetSelectorInBed And &HFFFCUS) Or cplInBed), targetDescriptorInBed)
            IP = targetOffsetInBed
            Return
        End If
        If targetDplInBed > cplInBed Then RaiseCpuException(13, "Call-gate cannot transfer outward", True, SelectorErrorCodeInBed(targetSelectorInBed))
        If Not isCallInBed AndAlso targetDplInBed <> cplInBed Then RaiseCpuException(13, "JMP gate cannot change CPL", True, SelectorErrorCodeInBed(targetSelectorInBed))
        If (targetDescriptorInBed.Access And &H80) = 0 Then RaiseCpuException(11, "Call-gate target not present", True, SelectorErrorCodeInBed(targetSelectorInBed))
        If CUInt(targetOffsetInBed) > targetDescriptorInBed.Limit Then RaiseCpuException(13, "Call-gate offset exceeds limit", True, 0US)

        If targetDplInBed = cplInBed Then
            If isCallInBed Then Push(CS) : Push(IP)
            CacheSegmentDescriptor(1, CUShort((targetSelectorInBed And &HFFFCUS) Or cplInBed), targetDescriptorInBed)
            IP = targetOffsetInBed
            Return
        End If

        If Not isCallInBed Then RaiseCpuException(13, "Only CALL may use a gate for inward privilege transfer", True, SelectorErrorCodeInBed(gateSelectorInBed))
        Dim tssDescriptorInBed As Descriptor286
        If Not TryReadGdtDescriptor(TaskRegister, tssDescriptorInBed) Then RaiseCpuException(10, "Call gate requires valid current TSS", True, SelectorErrorCodeInBed(TaskRegister))
        Dim stackOffsetInBed As UInteger = CUInt(2 + targetDplInBed * 4)
        Dim newSpInBed As UInt16 = ReadWord(tssDescriptorInBed.BaseAddress + stackOffsetInBed)
        Dim newSsInBed As UInt16 = ReadWord(tssDescriptorInBed.BaseAddress + stackOffsetInBed + 2UI)
        Dim newSsStageInBed As SegmentLoadStageInBed = StageStackSegmentForPrivilegeInBed(newSsInBed, targetDplInBed)

        Dim oldSsInBed As UInt16 = SS
        Dim oldSpInBed As UInt16 = SP
        Dim oldCsInBed As UInt16 = CS
        Dim oldIpInBed As UInt16 = IP
        Dim parametersInBed(Math.Max(0, parameterCountInBed - 1)) As UInt16
        For indexInBed As Integer = 0 To parameterCountInBed - 1
            parametersInBed(indexInBed) = ReadWord(SegmentAddress(2, CUShort((CInt(oldSpInBed) + indexInBed * 2) And &HFFFF), 2))
        Next

        CommitSegmentLoadInBed(newSsStageInBed)
        SP = newSpInBed
        Push(oldSsInBed)
        Push(oldSpInBed)
        For indexInBed As Integer = parameterCountInBed - 1 To 0 Step -1
            Push(parametersInBed(indexInBed))
        Next
        Push(oldCsInBed)
        Push(oldIpInBed)
        CacheSegmentDescriptor(1, CUShort((targetSelectorInBed And &HFFFCUS) Or targetDplInBed), targetDescriptorInBed)
        IP = targetOffsetInBed
    End Sub

    Private Sub ExecuteTaskTransferInBed(selectorInBed As UInt16,
                                         descriptorInBed As Descriptor286,
                                         isCallInBed As Boolean)
        Dim typeInBed As Integer = descriptorInBed.Access And &HF
        If typeInBed = 5 Then
            Dim taskSelectorInBed As UInt16 = ReadWord(descriptorInBed.DescriptorAddress + 2UI)
            PerformTaskSwitchInBed(taskSelectorInBed, If(isCallInBed, TaskSwitchKindInBed.NestedCall, TaskSwitchKindInBed.Jump))
        Else
            PerformTaskSwitchInBed(selectorInBed, If(isCallInBed, TaskSwitchKindInBed.NestedCall, TaskSwitchKindInBed.Jump))
        End If
    End Sub

    Private Sub ExecuteFarReturnInBed(cleanupInBed As UInt16)
        Dim newIpInBed As UInt16 = PeekStackWordInBed(0)
        Dim newCsInBed As UInt16 = PeekStackWordInBed(2)
        Dim diagnosticReturnDescriptorInBed As Descriptor286
        Dim diagnosticReturnDescriptorValidInBed As Boolean = False
        If ProtectedMode Then
            diagnosticReturnDescriptorValidInBed =
                TryReadDescriptor(newCsInBed, diagnosticReturnDescriptorInBed)
        End If
        If ProtectedMode AndAlso
           (newIpInBed = &H9F31US OrElse
            (_instructionStartCs = &H48FUS AndAlso _instructionStartIp = &H9F31US)) Then
            If Not _diagnosticImportantIntTraceEnabled Then BeginDiagnosticImportantIntTrace()
            Dim entryInBed As String =
                "RETF -> " & newCsInBed.ToString("X4") & ":" & newIpInBed.ToString("X4") &
                " from=" & CS.ToString("X4") & ":" & _instructionStartIp.ToString("X4") &
                " SS:SP=" & SS.ToString("X4") & ":" & SP.ToString("X4") &
                " cleanup=" & cleanupInBed.ToString("X4") &
                " CPL=" & CurrentPrivilegeLevelInBed().ToString() &
                " currentCSCache=[base=" & _segmentBases(1).ToString("X6") &
                " limit=" & _segmentLimits(1).ToString("X4") &
                " access=" & _segmentAccess(1).ToString("X2") & "]" &
                If(diagnosticReturnDescriptorValidInBed,
                   " targetDescriptor=[addr=" & diagnosticReturnDescriptorInBed.DescriptorAddress.ToString("X6") &
                   " base=" & diagnosticReturnDescriptorInBed.BaseAddress.ToString("X6") &
                   " limit=" & diagnosticReturnDescriptorInBed.Limit.ToString("X4") &
                   " access=" & diagnosticReturnDescriptorInBed.Access.ToString("X2") &
                   " DPL=" & ((diagnosticReturnDescriptorInBed.Access >> 5) And 3).ToString() &
                   " conforming=" & ((diagnosticReturnDescriptorInBed.Access And 4) <> 0).ToString() & "]",
                   " targetDescriptor=[invalid]") &
                " frame=[" &
                PeekStackWordInBed(0).ToString("X4") & " " &
                PeekStackWordInBed(2).ToString("X4") & " " &
                PeekStackWordInBed(4).ToString("X4") & " " &
                PeekStackWordInBed(6).ToString("X4") & " " &
                PeekStackWordInBed(8).ToString("X4") & " " &
                PeekStackWordInBed(10).ToString("X4") & "]"
            TraceDiagnosticImportantInt(entryInBed)
            TraceDiagnosticDpmi(entryInBed)
        End If
        If Not ProtectedMode Then
            Dim stageInBed As SegmentLoadStageInBed = StageSegmentLoadInBed(1, newCsInBed)
            SP = CUShort((CInt(SP) + 4 + CInt(cleanupInBed)) And &HFFFF)
            CommitSegmentLoadInBed(stageInBed)
            IP = newIpInBed
            Return
        End If

        Dim currentCplInBed As Integer = CurrentPrivilegeLevelInBed()
        Dim returnRplInBed As Integer = newCsInBed And 3
        Dim codeDescriptorInBed As Descriptor286
        If Not TryReadDescriptor(newCsInBed, codeDescriptorInBed) Then RaiseCpuException(13, "RETF code selector invalid", True, SelectorErrorCodeInBed(newCsInBed))
        If (codeDescriptorInBed.Access And &H18) <> &H18 Then RaiseCpuException(13, "RETF target is not code", True, SelectorErrorCodeInBed(newCsInBed))
        Dim returnDescriptorDplInBed As Integer = (codeDescriptorInBed.Access >> 5) And 3
        Dim returnConformingInBed As Boolean = (codeDescriptorInBed.Access And 4) <> 0
        ' Conforming code retains the caller's CPL.  Its selector RPL is not an
        ' inward privilege request and must not be rejected before the descriptor
        ' type is known.  The loaded CS is normalized to the unchanged CPL.
        If returnConformingInBed Then
            If returnDescriptorDplInBed > currentCplInBed Then
                RaiseCpuException(13, "RETF conforming target privilege violation", True, SelectorErrorCodeInBed(newCsInBed))
            End If
            If (codeDescriptorInBed.Access And &H80) = 0 Then RaiseCpuException(11, "RETF code not present", True, SelectorErrorCodeInBed(newCsInBed))
            If CUInt(newIpInBed) > codeDescriptorInBed.Limit Then RaiseCpuException(13, "RETF offset exceeds limit", True, 0US)
            SP = CUShort((CInt(SP) + 4 + CInt(cleanupInBed)) And &HFFFF)
            CacheSegmentDescriptor(1,
                                   CUShort((newCsInBed And &HFFFCUS) Or currentCplInBed),
                                   codeDescriptorInBed)
            IP = newIpInBed
            If _instructionStartCs = &H48FUS AndAlso _instructionStartIp = &H9F31US Then
                EndForensicTraceInBed(
                    "048F:9F31 conforming RETF accepted: destination=" &
                    CS.ToString("X4") & ":" & IP.ToString("X4") &
                    " retainedCPL=" & currentCplInBed.ToString())
            End If
            Return
        End If

        If returnRplInBed < currentCplInBed Then
            If _instructionStartCs = &H48FUS AndAlso _instructionStartIp = &H9F31US Then
                WriteForensicEventInBed(
                    "048F:9F31 deliberate inward RETF raised #GP: destination=" &
                    newCsInBed.ToString("X4") & ":" & newIpInBed.ToString("X4") &
                    " currentCPL=" & currentCplInBed.ToString() &
                    " returnRPL=" & returnRplInBed.ToString())
            End If
            RaiseCpuException(13, "RETF cannot return inward", True, SelectorErrorCodeInBed(newCsInBed))
        End If
        If returnDescriptorDplInBed <> returnRplInBed Then
            RaiseCpuException(13, "RETF target code privilege violation", True, SelectorErrorCodeInBed(newCsInBed))
        End If
        If (codeDescriptorInBed.Access And &H80) = 0 Then RaiseCpuException(11, "RETF code not present", True, SelectorErrorCodeInBed(newCsInBed))
        If CUInt(newIpInBed) > codeDescriptorInBed.Limit Then RaiseCpuException(13, "RETF offset exceeds limit", True, 0US)
        Dim returnCplInBed As Integer = returnRplInBed

        If returnCplInBed = currentCplInBed Then
            SP = CUShort((CInt(SP) + 4 + CInt(cleanupInBed)) And &HFFFF)
            CacheSegmentDescriptor(1, newCsInBed, codeDescriptorInBed)
            IP = newIpInBed
            Return
        End If

        Dim outerSpOffsetInBed As Integer = 4 + CInt(cleanupInBed)
        Dim newSpInBed As UInt16 = PeekStackWordInBed(outerSpOffsetInBed)
        Dim newSsInBed As UInt16 = PeekStackWordInBed(outerSpOffsetInBed + 2)
        Dim ssStageInBed As SegmentLoadStageInBed = StageStackSegmentForPrivilegeInBed(newSsInBed, returnCplInBed)
        CacheSegmentDescriptor(1, newCsInBed, codeDescriptorInBed)
        IP = newIpInBed
        CommitSegmentLoadInBed(ssStageInBed)
        SP = CUShort((CInt(newSpInBed) + CInt(cleanupInBed)) And &HFFFF)
        InvalidateOuterPrivilegeDataSegmentsInBed(returnCplInBed)
    End Sub

    Private Sub LoadLocalDescriptorTable(selector As UInt16)
        If (selector And &HFFF8US) = 0 Then
            LocalDescriptorTableRegister = 0US
            _ldtBaseInBed = 0UI : _ldtLimitInBed = 0US : _ldtAccessInBed = 0 : _ldtValidInBed = False
            Return
        End If
        If (selector And 4US) <> 0US Then RaiseCpuException(13, "LLDT selector must reference GDT", True, SelectorErrorCodeInBed(selector))
        Dim descriptor As Descriptor286
        If Not TryReadGdtDescriptor(selector, descriptor) OrElse (descriptor.Access And &H1F) <> &H2 Then RaiseCpuException(13, "Invalid LDT selector", True, SelectorErrorCodeInBed(selector))
        If (descriptor.Access And &H80) = 0 Then RaiseCpuException(11, "LDT not present", True, SelectorErrorCodeInBed(selector))
        LocalDescriptorTableRegister = selector
        _ldtBaseInBed = descriptor.BaseAddress : _ldtLimitInBed = descriptor.Limit : _ldtAccessInBed = descriptor.Access : _ldtValidInBed = True
    End Sub

    Private Sub LoadTaskRegister(selector As UInt16)
        If (selector And &HFFF8US) = 0 OrElse (selector And 4US) <> 0US Then RaiseCpuException(13, "LTR requires non-null GDT selector", True, SelectorErrorCodeInBed(selector))
        Dim descriptor As Descriptor286
        If Not TryReadGdtDescriptor(selector, descriptor) OrElse (descriptor.Access And &H1F) <> &H1 Then RaiseCpuException(13, "LTR requires available 286 TSS", True, SelectorErrorCodeInBed(selector))
        If (descriptor.Access And &H80) = 0 Then RaiseCpuException(11, "TSS not present", True, SelectorErrorCodeInBed(selector))
        If descriptor.Limit < &H2BUS Then RaiseCpuException(10, "TSS limit is smaller than 44-byte 286 TSS", True, SelectorErrorCodeInBed(selector))
        SetTssBusyBitInBed(descriptor, True)
        TaskRegister = selector
        _taskBaseInBed = descriptor.BaseAddress : _taskLimitInBed = descriptor.Limit : _taskAccessInBed = CByte((descriptor.Access And &HF0) Or 3) : _taskValidInBed = True
    End Sub

    Private Sub BeginBusLockInBed()
        _busLockAssertedInBed = True
        _busHoldAcknowledgeInBed = False
    End Sub

    Private Sub EndBusLockInBed()
        If Not _busLockAssertedInBed Then Return
        _busLockAssertedInBed = False
        ReconcileHoldAcknowledgeInBed()
    End Sub

    Private Sub RewindRepInstructionInBed()
        IP = _instructionStartIp
        _segmentSelectors(1) = _instructionStartCs
        _segmentBases(1) = _instructionStartCsBaseInBed
        _segmentLimits(1) = _instructionStartCsLimitInBed
        _segmentAccess(1) = _instructionStartCsAccessInBed
        _segmentValid(1) = _instructionStartCsValidInBed
    End Sub

    Private Function CurrentPrivilegeLevelInBed() As Integer
        If Not ProtectedMode Then Return 0
        If Not _protectedModeCsLoadedInBed Then Return 0
        Return CS And 3
    End Function

    Private Function CurrentIoplInBed() As Integer
        Return (Flags >> 12) And 3
    End Function

    Private Sub RequireCpl0InBed(operationInBed As String)
        If ProtectedMode AndAlso CurrentPrivilegeLevelInBed() <> 0 Then
            RaiseCpuException(13, operationInBed & " requires CPL 0", True, 0US)
        End If
    End Sub

    Private Sub RequireIoPrivilegeInBed()
        If ProtectedMode AndAlso CurrentPrivilegeLevelInBed() > CurrentIoplInBed() Then
            RaiseCpuException(13, "I/O privilege violation", True, 0US)
        End If
    End Sub

    Private Sub ExecuteCliInBed()
        RequireIoPrivilegeInBed()
        SetFlag(InterruptFlag, False)
    End Sub

    Private Sub ExecuteStiInBed()
        RequireIoPrivilegeInBed()
        SetFlag(InterruptFlag, True)
        ArmStiInterruptShadowInBed()
    End Sub

    Private Sub ExecuteHltInBed()
        If ProtectedMode AndAlso CurrentPrivilegeLevelInBed() <> 0 Then
            RaiseCpuException(13, "HLT requires CPL 0", True, 0US)
            Return
        End If
        EnterHaltStateInBed(ProcessorHaltSourceInBed.HltInstruction)
    End Sub

    Private Sub ExecutePopfInBed()
        Dim incomingInBed As UInt16 = PopWord()
        If Not ProtectedMode Then
            Flags = NormalizeFlags(incomingInBed)
            Return
        End If

        Dim oldFlagsInBed As UInt16 = Flags
        Dim candidateInBed As UInt16 = NormalizeFlags(incomingInBed)
        Dim cplInBed As Integer = CurrentPrivilegeLevelInBed()
        If cplInBed <> 0 Then
            candidateInBed = CUShort((candidateInBed And Not &H3000US) Or (oldFlagsInBed And &H3000US))
        End If
        If cplInBed > CurrentIoplInBed() Then
            candidateInBed = CUShort((candidateInBed And Not InterruptFlag) Or (oldFlagsInBed And InterruptFlag))
        End If
        Flags = candidateInBed
    End Sub

    Private Sub ArmStiInterruptShadowInBed()
        _interruptShadowRetirementsInBed = Math.Max(_interruptShadowRetirementsInBed, 2)
    End Sub

    Private Sub ArmSsInterruptShadowInBed()
        _interruptShadowRetirementsInBed = Math.Max(_interruptShadowRetirementsInBed, 2)
        _nmiShadowRetirementsInBed = Math.Max(_nmiShadowRetirementsInBed, 2)
    End Sub

    Private Sub RetireInstructionInBed()
        If _interruptShadowRetirementsInBed > 0 Then _interruptShadowRetirementsInBed -= 1
        If _nmiShadowRetirementsInBed > 0 Then _nmiShadowRetirementsInBed -= 1

        ' TF is sampled at instruction start.  POPF/IRET which set TF therefore
        ' arm single-step for the following instruction, while an already-set TF
        ' generates #DB after this retirement boundary.
        If _trapFlagSampleAtInstructionStartInBed AndAlso Not Halted Then
            EnterInterrupt(1, False)
        End If
    End Sub

    Private Sub NoteInterruptEntryForNmiInBed()
        If _enteringNmiInBed Then
            _nmiBlockedInBed = True
            _nmiNestedInterruptDepthInBed = 0
        ElseIf _nmiBlockedInBed Then
            _nmiNestedInterruptDepthInBed += 1
        End If
    End Sub

    Private Sub NoteIretForNmiInBed()
        If Not _nmiBlockedInBed Then Return
        If _nmiNestedInterruptDepthInBed > 0 Then
            _nmiNestedInterruptDepthInBed -= 1
        Else
            _nmiBlockedInBed = False
        End If
    End Sub

    Private Sub CaptureInstructionStartStateInBed()
        _instructionStartCs = CS
        _instructionStartIp = IP
        _instructionStartCsBaseInBed = _segmentBases(1)
        _instructionStartCsLimitInBed = _segmentLimits(1)
        _instructionStartCsAccessInBed = _segmentAccess(1)
        _instructionStartCsValidInBed = _segmentValid(1)
    End Sub

    Private Sub RestoreInstructionStartStateInBed()
        IP = _instructionStartIp
        _segmentSelectors(1) = _instructionStartCs
        _segmentBases(1) = _instructionStartCsBaseInBed
        _segmentLimits(1) = _instructionStartCsLimitInBed
        _segmentAccess(1) = _instructionStartCsAccessInBed
        _segmentValid(1) = _instructionStartCsValidInBed
    End Sub

    Public Function CoreRefitDiagnosticText() As String
        Dim sbInBed As New System.Text.StringBuilder()
        sbInBed.Append("80286 core refit         : retirement / protection / task / prefetch substrate").AppendLine()
        sbInBed.Append("CS visible:hidden       : ").Append(CS.ToString("X4")).Append(":").Append(_segmentBases(1).ToString("X6")).AppendLine()
        sbInBed.Append("CS limit/access/valid   : ").Append(_segmentLimits(1).ToString("X4")).Append(" / ").Append(_segmentAccess(1).ToString("X2")).Append(" / ").Append(_segmentValid(1)).AppendLine()
        sbInBed.Append("IP / physical fetch     : ").Append(IP.ToString("X4")).Append(" / ").Append(((_segmentBases(1) + CUInt(IP)) And &HFFFFFFUI).ToString("X6")).AppendLine()
        sbInBed.Append("AX BX CX DX             : ").Append(AX.ToString("X4")).Append(" ").Append(BX.ToString("X4")).Append(" ").Append(CX.ToString("X4")).Append(" ").Append(DX.ToString("X4")).AppendLine()
        sbInBed.Append("SI DI BP SP             : ").Append(SI.ToString("X4")).Append(" ").Append(DI.ToString("X4")).Append(" ").Append(BP.ToString("X4")).Append(" ").Append(SP.ToString("X4")).AppendLine()
        sbInBed.Append("ES SS DS                : ").Append(ES.ToString("X4")).Append(" ").Append(SS.ToString("X4")).Append(" ").Append(DS.ToString("X4")).AppendLine()
        sbInBed.Append("GDTR / IDTR             : ").Append(GdtrBase.ToString("X6")).Append(":").Append(GdtrLimit.ToString("X4")).Append(" / ").Append(IdtrBase.ToString("X6")).Append(":").Append(IdtrLimit.ToString("X4")).AppendLine()
        For segmentIndexInBed As Integer = 0 To 3
            Dim segmentNameInBed As String = {"ES", "CS", "SS", "DS"}(segmentIndexInBed)
            sbInBed.Append(segmentNameInBed).Append(" base/limit/access     : ").Append(_segmentBases(segmentIndexInBed).ToString("X6")).Append(" / ").Append(_segmentLimits(segmentIndexInBed).ToString("X4")).Append(" / ").Append(_segmentAccess(segmentIndexInBed).ToString("X2")).AppendLine()
        Next
        sbInBed.Append("MSW / FLAGS / CPL/IOPL : ").Append(MachineStatusWord.ToString("X4")).Append(" / ").Append(Flags.ToString("X4")).Append(" / ").Append(CurrentPrivilegeLevelInBed()).Append("/").Append(CurrentIoplInBed()).AppendLine()
        sbInBed.Append("ESC attempts / #NM traps : ").Append(_diagnosticEscAttemptCountInBed.ToString("N0")).Append(" / ").Append(_diagnosticEscNmTrapCountInBed.ToString("N0")).AppendLine()
        If _diagnosticEscAttemptCountInBed > 0UL Then
            sbInBed.Append("Last ESC CS:IP/op/MSW    : ").Append(_diagnosticLastEscCsInBed.ToString("X4")).Append(":").Append(_diagnosticLastEscIpInBed.ToString("X4")).Append(" / ").Append(_diagnosticLastEscOpcodeInBed.ToString("X2")).Append(" / ").Append(_diagnosticLastEscMswInBed.ToString("X4")).AppendLine()
        End If
        sbInBed.Append("IRQ/NMI shadows         : ").Append(_interruptShadowRetirementsInBed).Append(" / ").Append(_nmiShadowRetirementsInBed).AppendLine()
        sbInBed.Append("NMI pending/blocked     : ").Append(_nmiPending).Append(" / ").Append(_nmiBlockedInBed).AppendLine()
        sbInBed.Append("HOLD/HLDA/LOCK          : ").Append(_busHoldRequestInBed).Append(" / ").Append(_busHoldAcknowledgeInBed).Append(" / ").Append(_busLockAssertedInBed).AppendLine()
        sbInBed.Append("Prefetch bytes          : ").Append(_prefetchCountInBed).Append(" / 6").AppendLine()
        sbInBed.Append("LDTR cache              : ").Append(LocalDescriptorTableRegister.ToString("X4")).Append(" base ").Append(_ldtBaseInBed.ToString("X6")).Append(" lim ").Append(_ldtLimitInBed.ToString("X4")).Append(" valid ").Append(_ldtValidInBed).AppendLine()
        sbInBed.Append("TR cache                : ").Append(TaskRegister.ToString("X4")).Append(" base ").Append(_taskBaseInBed.ToString("X6")).Append(" lim ").Append(_taskLimitInBed.ToString("X4")).Append(" valid ").Append(_taskValidInBed).AppendLine()
        sbInBed.Append("80287 BUSY/ERROR        : ").Append(_numericCoprocessor.Busy).Append(" / ").Append(_numericCoprocessor.ErrorAsserted).AppendLine()
        sbInBed.Append("Last architectural fault: ").Append(If(String.IsNullOrEmpty(LastFault), "<none>", LastFault))
        Return sbInBed.ToString()
    End Function

    Public Function DiagnosticCpuFaultTraceText() As String
        If _diagnosticCpuFaultTraceInBed.Count = 0 Then Return "CPU exception recorder    : <no exceptions since reset>"
        Dim sbInBed As New System.Text.StringBuilder()
        sbInBed.Append("CPU first exceptions      : preserved first ").Append(_diagnosticCpuFirstFaultTraceInBed.Count).AppendLine()
        For Each entryInBed As String In _diagnosticCpuFirstFaultTraceInBed
            sbInBed.AppendLine(entryInBed)
        Next
        sbInBed.AppendLine()
        sbInBed.Append("CPU exception recorder    : last ").Append(_diagnosticCpuFaultTraceInBed.Count).Append(" detected exceptions").AppendLine()
        For Each entryInBed As String In _diagnosticCpuFaultTraceInBed
            sbInBed.AppendLine(entryInBed)
        Next
        Return sbInBed.ToString().TrimEnd()
    End Function

    Public Function DiagnosticProtectionGateText(vectorInBed As Integer) As String
        Dim sbInBed As New System.Text.StringBuilder()
        sbInBed.Append("Protected gate vector ").Append(vectorInBed).AppendLine()
        If vectorInBed < 0 OrElse CUInt(vectorInBed * 8 + 7) > IdtrLimit Then
            sbInBed.Append("  outside IDTR limit")
            Return sbInBed.ToString()
        End If
        Dim gateAddressInBed As UInteger = IdtrBase + CUInt(vectorInBed * 8)
        Dim rawInBed(7) As Byte
        For byteIndexInBed As Integer = 0 To 7
            rawInBed(byteIndexInBed) = ReadByte(gateAddressInBed + CUInt(byteIndexInBed))
        Next
        Dim targetOffsetInBed As UInt16 = CUShort(rawInBed(0) Or (CUShort(rawInBed(1)) << 8))
        Dim targetSelectorInBed As UInt16 = CUShort(rawInBed(2) Or (CUShort(rawInBed(3)) << 8))
        sbInBed.Append("  IDT address/raw        : ").Append(gateAddressInBed.ToString("X6")).Append(" / ")
        For Each valueInBed As Byte In rawInBed
            sbInBed.Append(valueInBed.ToString("X2")).Append(" ")
        Next
        sbInBed.AppendLine()
        sbInBed.Append("  offset/selector/access : ").Append(targetOffsetInBed.ToString("X4")).Append(" / ").Append(targetSelectorInBed.ToString("X4")).Append(" / ").Append(rawInBed(5).ToString("X2")).AppendLine()
        Dim descriptorInBed As Descriptor286
        If TryReadDescriptor(targetSelectorInBed, descriptorInBed) Then
            sbInBed.Append("  target base/limit/access: ").Append(descriptorInBed.BaseAddress.ToString("X6")).Append(" / ").Append(descriptorInBed.Limit.ToString("X4")).Append(" / ").Append(descriptorInBed.Access.ToString("X2")).AppendLine()
            sbInBed.Append("  target DPL / RPL       : ").Append((descriptorInBed.Access >> 5) And 3).Append(" / ").Append(targetSelectorInBed And 3)
        Else
            sbInBed.Append("  target descriptor invalid")
        End If
        Return sbInBed.ToString()
    End Function

    Public Function DiagnosticSelectorWriteTraceText() As String
        If _diagnosticSelectorWriteTraceInBed.Count = 0 Then Return "Selector-word write trace : <no CPU writes observed>"
        Dim sbInBed As New System.Text.StringBuilder()
        sbInBed.Append("Selector-word write trace : physical B68A-B68B").AppendLine()
        For Each entryInBed As String In _diagnosticSelectorWriteTraceInBed
            sbInBed.AppendLine(entryInBed)
        Next
        Return sbInBed.ToString().TrimEnd()
    End Function

    Public Function DiagnosticSelectorWriterHistoryText() As String
        If _diagnosticSelectorWriterFrozenInBed.Count = 0 Then Return "Selector writer prehistory : <0F82 write not observed>"
        Dim sbInBed As New System.Text.StringBuilder()
        sbInBed.Append("Selector writer prehistory : ").Append(_diagnosticSelectorWriterFrozenInBed.Count).
            AppendLine(" instructions immediately before W16 B68A <- 0F82")
        For Each entryInBed As String In _diagnosticSelectorWriterFrozenInBed
            sbInBed.AppendLine(entryInBed)
        Next
        Return sbInBed.ToString().TrimEnd()
    End Function

    Public Function DiagnosticSecondCliEntryHistoryText() As String
        If _diagnosticSecondCliEntryFrozenInBed.Count = 0 Then Return "Root CLI entry history   : <005B:0B61 not observed>"
        Dim sbInBed As New System.Text.StringBuilder()
        sbInBed.Append("Root CLI entry history   : ").Append(_diagnosticSecondCliEntryFrozenInBed.Count).
            AppendLine(" instructions immediately before 005B:0B61")
        For Each entryInBed As String In _diagnosticSecondCliEntryFrozenInBed
            sbInBed.AppendLine(entryInBed)
        Next
        Return sbInBed.ToString().TrimEnd()
    End Function

    Public Function DiagnosticGpReturnHistoryText() As String
        If _diagnosticGpReturnFrozenInBed.Count = 0 Then Return "GP retry prehistory      : <first return to 0053:1E49 not observed>"
        Dim sbInBed As New System.Text.StringBuilder()
        sbInBed.Append("GP retry prehistory      : ").Append(_diagnosticGpReturnFrozenInBed.Count).
            AppendLine(" instructions immediately before first return to 0053:1E49")
        For Each entryInBed As String In _diagnosticGpReturnFrozenInBed
            sbInBed.AppendLine(entryInBed)
        Next
        Return sbInBed.ToString().TrimEnd()
    End Function

    Private Function CaptureDiagnosticSampleInBed() As DiagnosticSelectorWriterSampleInBed
        Return New DiagnosticSelectorWriterSampleInBed With {
            .Cs = CS, .Ip = IP, .CsBase = _segmentBases(1), .CsLimit = _segmentLimits(1),
            .Ax = AX, .Bx = BX, .Cx = CX, .Dx = DX, .Si = SI, .Di = DI,
            .Bp = BP, .Sp = SP, .Ds = DS, .Es = ES, .Ss = SS, .Flags = Flags
        }
    End Function

    Private Function FormatDiagnosticSampleInBed(sampleInBed As DiagnosticSelectorWriterSampleInBed,
                                                  ordinalInBed As Integer) As String
        Dim bytesInBed As New System.Text.StringBuilder()
        If _memoryControllerInBed IsNot Nothing Then
            For byteIndexInBed As Integer = 0 To 7
                Dim offsetInBed As UInteger = CUInt(sampleInBed.Ip) + CUInt(byteIndexInBed)
                If offsetInBed > sampleInBed.CsLimit Then Exit For
                If bytesInBed.Length > 0 Then bytesInBed.Append(" ")
                bytesInBed.Append(ReadByte((sampleInBed.CsBase + offsetInBed) And &HFFFFFFUI).ToString("X2"))
            Next
        End If
        Return "#" & ordinalInBed.ToString("000") & " " & sampleInBed.Cs.ToString("X4") & ":" & sampleInBed.Ip.ToString("X4") &
            " [" & bytesInBed.ToString() & "] AX=" & sampleInBed.Ax.ToString("X4") & " BX=" & sampleInBed.Bx.ToString("X4") &
            " CX=" & sampleInBed.Cx.ToString("X4") & " DX=" & sampleInBed.Dx.ToString("X4") & " SI=" & sampleInBed.Si.ToString("X4") &
            " DI=" & sampleInBed.Di.ToString("X4") & " BP=" & sampleInBed.Bp.ToString("X4") & " SP=" & sampleInBed.Sp.ToString("X4") &
            " ES=" & sampleInBed.Es.ToString("X4") & " SS=" & sampleInBed.Ss.ToString("X4") & " DS=" & sampleInBed.Ds.ToString("X4") &
            " FL=" & sampleInBed.Flags.ToString("X4")
    End Function

    Private Sub TraceDiagnosticGpReturnStepInBed()
        If _diagnosticCpuFaultSequenceInBed < 3UL OrElse _diagnosticGpReturnFrozenInBed.Count <> 0 Then Return
        If CS <> &H53US OrElse IP <> &H1E49US Then
            _diagnosticGpReturnObservedAwayInBed = True
        ElseIf _diagnosticGpReturnObservedAwayInBed Then
            Dim firstInBed As Integer =
                (_diagnosticGpReturnIndexInBed - _diagnosticGpReturnCountInBed + DiagnosticGpReturnHistoryCapacityInBed) Mod
                DiagnosticGpReturnHistoryCapacityInBed
            For ordinalInBed As Integer = 0 To _diagnosticGpReturnCountInBed - 1
                Dim sampleInBed As DiagnosticSelectorWriterSampleInBed =
                    _diagnosticGpReturnRingInBed((firstInBed + ordinalInBed) Mod DiagnosticGpReturnHistoryCapacityInBed)
                _diagnosticGpReturnFrozenInBed.Add(FormatDiagnosticSampleInBed(sampleInBed, ordinalInBed + 1))
            Next
            Return
        End If
        _diagnosticGpReturnRingInBed(_diagnosticGpReturnIndexInBed) = CaptureDiagnosticSampleInBed()
        _diagnosticGpReturnIndexInBed = (_diagnosticGpReturnIndexInBed + 1) Mod DiagnosticGpReturnHistoryCapacityInBed
        If _diagnosticGpReturnCountInBed < DiagnosticGpReturnHistoryCapacityInBed Then _diagnosticGpReturnCountInBed += 1
    End Sub

    Private Sub TraceDiagnosticSelectorWriterStepInBed()
        If CS = &H5BUS AndAlso IP = &HB61US AndAlso _diagnosticSecondCliEntryFrozenInBed.Count = 0 Then
            Dim firstInBed As Integer =
                (_diagnosticSelectorWriterIndexInBed - _diagnosticSelectorWriterCountInBed + DiagnosticSelectorWriterHistoryCapacityInBed) Mod
                DiagnosticSelectorWriterHistoryCapacityInBed
            For ordinalInBed As Integer = 0 To _diagnosticSelectorWriterCountInBed - 1
                Dim sampleInBed As DiagnosticSelectorWriterSampleInBed =
                    _diagnosticSelectorWriterRingInBed((firstInBed + ordinalInBed) Mod DiagnosticSelectorWriterHistoryCapacityInBed)
                _diagnosticSecondCliEntryFrozenInBed.Add(FormatDiagnosticSampleInBed(sampleInBed, ordinalInBed + 1))
            Next
        End If
        _diagnosticSelectorWriterRingInBed(_diagnosticSelectorWriterIndexInBed) = CaptureDiagnosticSampleInBed()
        _diagnosticSelectorWriterIndexInBed =
            (_diagnosticSelectorWriterIndexInBed + 1) Mod DiagnosticSelectorWriterHistoryCapacityInBed
        If _diagnosticSelectorWriterCountInBed < DiagnosticSelectorWriterHistoryCapacityInBed Then
            _diagnosticSelectorWriterCountInBed += 1
        End If
    End Sub

    Private Sub FreezeDiagnosticSelectorWriterHistoryInBed()
        If _diagnosticSelectorWriterFrozenInBed.Count <> 0 OrElse _memoryControllerInBed Is Nothing Then Return
        Dim firstInBed As Integer =
            (_diagnosticSelectorWriterIndexInBed - _diagnosticSelectorWriterCountInBed + DiagnosticSelectorWriterHistoryCapacityInBed) Mod
            DiagnosticSelectorWriterHistoryCapacityInBed
        For ordinalInBed As Integer = 0 To _diagnosticSelectorWriterCountInBed - 1
            Dim sampleInBed As DiagnosticSelectorWriterSampleInBed =
                _diagnosticSelectorWriterRingInBed((firstInBed + ordinalInBed) Mod DiagnosticSelectorWriterHistoryCapacityInBed)
            _diagnosticSelectorWriterFrozenInBed.Add(FormatDiagnosticSampleInBed(sampleInBed, ordinalInBed + 1))
        Next
    End Sub

    Public Function DiagnosticGpHandlerTraceText() As String
        If _diagnosticGpHandlerTraceInBed.Count = 0 Then Return "Third-fault handler trace : <not armed yet>"
        Dim sbInBed As New System.Text.StringBuilder()
        sbInBed.Append("Second-fault handler trace: first ").Append(_diagnosticGpHandlerTraceInBed.Count).Append(" instructions after exception #2").AppendLine()
        For Each entryInBed As String In _diagnosticGpHandlerTraceInBed
            sbInBed.AppendLine(entryInBed)
        Next
        Return sbInBed.ToString().TrimEnd()
    End Function

    Private Sub TraceDiagnosticGpHandlerStepInBed()
        If _diagnosticGpHandlerRemainingInBed <= 0 Then Return
        Dim bytesInBed As New System.Text.StringBuilder()
        For byteIndexInBed As Integer = 0 To 7
            Dim offsetInBed As UInteger = CUInt(IP) + CUInt(byteIndexInBed)
            If offsetInBed > _segmentLimits(1) Then Exit For
            Dim valueInBed As Byte = ReadByte((_segmentBases(1) + offsetInBed) And &HFFFFFFUI)
            If bytesInBed.Length > 0 Then bytesInBed.Append(" ")
            bytesInBed.Append(valueInBed.ToString("X2"))
        Next
        _diagnosticGpHandlerTraceInBed.Add(
            "#" & (_diagnosticGpHandlerTraceInBed.Count + 1).ToString("000") &
            " " & CS.ToString("X4") & ":" & IP.ToString("X4") &
            " [" & bytesInBed.ToString() & "]" &
            " AX=" & AX.ToString("X4") & " BX=" & BX.ToString("X4") &
            " CX=" & CX.ToString("X4") & " DX=" & DX.ToString("X4") &
            " SI=" & SI.ToString("X4") & " DI=" & DI.ToString("X4") &
            " BP=" & BP.ToString("X4") & " SP=" & SP.ToString("X4") &
            " ES=" & ES.ToString("X4") & " SS=" & SS.ToString("X4") & " DS=" & DS.ToString("X4") &
            " FL=" & Flags.ToString("X4"))
        _diagnosticGpHandlerRemainingInBed -= 1
    End Sub

    Private Sub TraceDiagnosticSelectorWriteInBed(addressInBed As UInteger,
                                                   valueInBed As UInt16,
                                                   widthInBed As Integer)
        If _memoryControllerInBed Is Nothing Then Return
        Dim firstInBed As UInteger = _memoryControllerInBed.NormalizePhysicalAddress(addressInBed)
        Dim lastInBed As UInteger = _memoryControllerInBed.NormalizePhysicalAddress(addressInBed + CUInt(widthInBed - 1))
        If lastInBed < DiagnosticSelectorWordAddressInBed OrElse
           firstInBed > DiagnosticSelectorWordAddressInBed + 1UI Then Return
        If widthInBed = 2 AndAlso firstInBed = DiagnosticSelectorWordAddressInBed AndAlso valueInBed = &HF82US Then
            FreezeDiagnosticSelectorWriterHistoryInBed()
        End If
        _diagnosticSelectorWriteSequenceInBed += 1UL
        Dim oldLowInBed As Byte = _memoryControllerInBed.LowMemoryInBed(CInt(DiagnosticSelectorWordAddressInBed))
        Dim oldHighInBed As Byte = _memoryControllerInBed.LowMemoryInBed(CInt(DiagnosticSelectorWordAddressInBed + 1UI))
        Dim entryInBed As String =
            "#" & _diagnosticSelectorWriteSequenceInBed.ToString("000000") &
            " at=" & _instructionStartCs.ToString("X4") & ":" & _instructionStartIp.ToString("X4") &
            " target=" & firstInBed.ToString("X6") &
            " W" & (widthInBed * 8).ToString() &
            " oldWord=" & CUShort(oldLowInBed Or (CUShort(oldHighInBed) << 8)).ToString("X4") &
            " value=" & If(widthInBed = 1, CByte(valueInBed And &HFFUS).ToString("X2"), valueInBed.ToString("X4")) &
            " AX=" & AX.ToString("X4") & " BX=" & BX.ToString("X4") &
            " CX=" & CX.ToString("X4") & " DX=" & DX.ToString("X4") &
            " ES=" & ES.ToString("X4") & " SS=" & SS.ToString("X4") & " DS=" & DS.ToString("X4")
        While _diagnosticSelectorWriteTraceInBed.Count >= DiagnosticSelectorWriteCapacityInBed
            _diagnosticSelectorWriteTraceInBed.Dequeue()
        End While
        _diagnosticSelectorWriteTraceInBed.Enqueue(entryInBed)
    End Sub

    Private Sub EnterHaltStateInBed(sourceInBed As ProcessorHaltSourceInBed)
        _haltSourceInBed = sourceInBed
        _haltedInBed = True
    End Sub

    Private Sub Fault(message As String)
        LastFault = message
        EnterHaltStateInBed(ProcessorHaltSourceInBed.FaultStop)
        Debug.Print("CPU fault at " & CS.ToString("X4") & ":" & IP.ToString("X4") & " - " & message)
        If _diagnosticQbExecTraceEnabledInBed Then FreezeDiagnosticQbExecTraceInBed("Fault: " & message)
    End Sub

    Private Sub RaiseCpuException(vector As Integer,
                                  message As String,
                                  Optional hasErrorCodeInBed As Boolean = False,
                                  Optional errorCodeInBed As UInt16 = 0US)
        If _exceptionDeliveryActiveInBed Then
            Throw New InterruptDeliveryFaultSignalInBed(vector,
                                                        message,
                                                        hasErrorCodeInBed,
                                                        errorCodeInBed)
        End If
        LastFault = message
        _diagnosticCpuFaultSequenceInBed += 1UL
        If _diagnosticCpuFaultSequenceInBed = 2UL AndAlso _diagnosticGpHandlerTraceInBed.Count = 0 Then
            _diagnosticGpHandlerRemainingInBed = DiagnosticGpHandlerCapacityInBed
        End If
        Dim instructionBytesInBed As New System.Text.StringBuilder()
        For byteIndexInBed As Integer = 0 To 7
            Dim codeOffsetInBed As UInteger = CUInt(_instructionStartIp) + CUInt(byteIndexInBed)
            If codeOffsetInBed > _instructionStartCsLimitInBed Then Exit For
            If instructionBytesInBed.Length > 0 Then instructionBytesInBed.Append(" ")
            instructionBytesInBed.Append(ReadByte((_instructionStartCsBaseInBed + codeOffsetInBed) And &HFFFFFFUI).ToString("X2"))
        Next
        Dim faultEntryInBed As String =
            "#" & _diagnosticCpuFaultSequenceInBed.ToString("000000") &
            " vec=" & vector.ToString("00") &
            " at=" & _instructionStartCs.ToString("X4") & ":" & _instructionStartIp.ToString("X4") &
            " bytes=[" & instructionBytesInBed.ToString() & "]" &
            " AX=" & AX.ToString("X4") & " BX=" & BX.ToString("X4") &
            " CX=" & CX.ToString("X4") & " DX=" & DX.ToString("X4") &
            " SI=" & SI.ToString("X4") & " DI=" & DI.ToString("X4") &
            " BP=" & BP.ToString("X4") & " SP=" & SP.ToString("X4") &
            " ES=" & ES.ToString("X4") & " SS=" & SS.ToString("X4") & " DS=" & DS.ToString("X4") &
            " FL=" & Flags.ToString("X4") &
            " err=" & If(hasErrorCodeInBed, errorCodeInBed.ToString("X4"), "----") &
            " msg=" & message &
            If(String.IsNullOrEmpty(_diagnosticFaultAccessContextInBed), "", " | " & _diagnosticFaultAccessContextInBed)
        ' Windows Standard Mode uses #NP to demand-load movable segments.  A
        ' reset separates this phase from the earlier DPMI callback probe, so
        ' arm a fresh stream at the first post-reset #NP and retain the handler
        ' path that either services or rejects the demand-load request.
        If ProtectedMode AndAlso vector = 11 Then
            ' Keep the most recent demand-load transaction.  Windows can load
            ' many movable segments during Setup; retaining only the first one
            ' lets the bounded stream expire long before a later bad fixup is
            ' executed.  A new architectural #NP therefore supersedes the old
            ' diagnostic file.  This changes observation only, not exception
            ' delivery, descriptor state, or scheduler time.
            If _forensicTraceWriterInBed IsNot Nothing Then
                EndForensicTraceInBed("superseded by later protected-mode #NP demand load")
            End If
            BeginForensicTraceInBed()
            WriteDiagnosticLdtHistoryToForensicTraceInBed()
            Dim faultSelectorInBed As UInt16 =
                CUShort(If(hasErrorCodeInBed, errorCodeInBed And &HFFFCUS, 0US))
            Dim faultDescriptorInBed As Descriptor286
            Dim faultDescriptorTextInBed As String = "unreadable"
            If hasErrorCodeInBed AndAlso
               TryReadDescriptor(faultSelectorInBed, faultDescriptorInBed) Then
                faultDescriptorTextInBed =
                    "addr=" & faultDescriptorInBed.DescriptorAddress.ToString("X6") &
                    " base=" & faultDescriptorInBed.BaseAddress.ToString("X6") &
                    " limit=" & faultDescriptorInBed.Limit.ToString("X4") &
                    " access=" & faultDescriptorInBed.Access.ToString("X2") &
                    " present=" & If((faultDescriptorInBed.Access And &H80) <> 0, "1", "0")
            End If
            WriteForensicEventInBed(
                "TARGET TRACE BEGIN: #NP at " &
                _instructionStartCs.ToString("X4") & ":" &
                _instructionStartIp.ToString("X4") &
                " err=" & If(hasErrorCodeInBed,
                              errorCodeInBed.ToString("X4"), "----") &
                " bytes=[" & instructionBytesInBed.ToString() & "]" &
                " LDTR=" & LocalDescriptorTableRegister.ToString("X4") &
                " LDT=" & _ldtBaseInBed.ToString("X6") & ":" &
                _ldtLimitInBed.ToString("X4") &
                " descriptor=[" & faultDescriptorTextInBed & "]")
        End If
        Dim repetitiveWindowsDispatchFaultInBed As Boolean =
            vector = 13 AndAlso _instructionStartCs = &H78US AndAlso
            _instructionStartIp = &HC62US
        If _diagnosticImportantIntTraceEnabled AndAlso ProtectedMode AndAlso
           Not repetitiveWindowsDispatchFaultInBed Then
            Dim gateAddressInBed As UInteger = IdtrBase + CUInt(vector * 8)
            Dim gateDescriptionInBed As String
            If CUInt(vector * 8 + 7) <= IdtrLimit Then
                gateDescriptionInBed =
                    ReadWord(gateAddressInBed + 2UI).ToString("X4") & ":" &
                    ReadWord(gateAddressInBed).ToString("X4") &
                    " access=" & ReadByte(gateAddressInBed + 5UI).ToString("X2")
            Else
                gateDescriptionInBed = "outside IDTR limit"
            End If
            TraceDiagnosticDpmi(
                "CPU exception vec=" & vector.ToString("00") &
                " at=" & _instructionStartCs.ToString("X4") & ":" & _instructionStartIp.ToString("X4") &
                " bytes=[" & instructionBytesInBed.ToString() & "]" &
                " -> " & gateDescriptionInBed &
                " SS:SP=" & SS.ToString("X4") & ":" & SP.ToString("X4") &
                " FL=" & Flags.ToString("X4") &
                " msg=" & message)
            _diagnosticDpmiExceptionReturnPending = True
        End If
        While _diagnosticCpuFaultTraceInBed.Count >= DiagnosticCpuFaultCapacityInBed
            _diagnosticCpuFaultTraceInBed.Dequeue()
        End While
        If _diagnosticCpuFirstFaultTraceInBed.Count < DiagnosticCpuFirstFaultCapacityInBed Then
            _diagnosticCpuFirstFaultTraceInBed.Add(faultEntryInBed)
        End If
        _diagnosticCpuFaultTraceInBed.Enqueue(faultEntryInBed)
        _diagnosticFaultAccessContextInBed = ""
        RestoreInstructionStartStateInBed()
        If HaltOnCpuException Then
            EnterHaltStateInBed(ProcessorHaltSourceInBed.FaultStop)
            If _currentInstructionActiveInBed Then Throw New InstructionAbortSignalInBed()
            Return
        End If

        DeliverCpuExceptionInBed(vector, hasErrorCodeInBed, errorCodeInBed)
        If _currentInstructionActiveInBed Then Throw New InstructionAbortSignalInBed()
    End Sub

    Private Shared Function IsContributoryExceptionInBed(vectorInBed As Integer) As Boolean
        Return vectorInBed = 0 OrElse
               vectorInBed = 10 OrElse
               vectorInBed = 11 OrElse
               vectorInBed = 12 OrElse
               vectorInBed = 13
    End Function

    Private Sub DeliverCpuExceptionInBed(vectorInBed As Integer,
                                         hasErrorCodeInBed As Boolean,
                                         errorCodeInBed As UInt16)
        Dim deliveryVectorInBed As Integer = vectorInBed
        Dim deliveryHasErrorCodeInBed As Boolean = hasErrorCodeInBed
        Dim deliveryErrorCodeInBed As UInt16 = errorCodeInBed

        Do
            _exceptionDeliveryActiveInBed = True
            _exceptionDeliveryVectorInBed = deliveryVectorInBed
            Try
                If Not EnterInterrupt(deliveryVectorInBed,
                                      False,
                                      deliveryHasErrorCodeInBed,
                                      deliveryErrorCodeInBed) Then
                    EnterHaltStateInBed(ProcessorHaltSourceInBed.FaultStop)
                End If
                Return
            Catch secondFaultInBed As InterruptDeliveryFaultSignalInBed
                If deliveryVectorInBed = 8 Then
                    LastFault = "Protection fault while entering double-fault handler: " & secondFaultInBed.Message
                    EnterHaltStateInBed(ProcessorHaltSourceInBed.FaultStop)
                    Return
                End If
                If IsContributoryExceptionInBed(deliveryVectorInBed) AndAlso
                   IsContributoryExceptionInBed(secondFaultInBed.Vector) Then
                    deliveryVectorInBed = 8
                    deliveryHasErrorCodeInBed = True
                    deliveryErrorCodeInBed = 0US
                Else
                    deliveryVectorInBed = secondFaultInBed.Vector
                    deliveryHasErrorCodeInBed = secondFaultInBed.HasErrorCode
                    deliveryErrorCodeInBed = secondFaultInBed.ErrorCode
                End If
            Finally
                _exceptionDeliveryActiveInBed = False
                _exceptionDeliveryVectorInBed = -1
            End Try
        Loop
    End Sub

    Private Shared Function SelectorErrorCodeInBed(selectorInBed As UInt16,
                                                    Optional idtReferenceInBed As Boolean = False,
                                                    Optional externalInBed As Boolean = False) As UInt16
        ' Selector-originated exception codes retain TI (bit 2) and discard
        ' only the selector's RPL.  IDT-originated codes instead clear the
        ' selector low bits and identify the IDT through bit 1.
        Dim resultInBed As UInt16
        If idtReferenceInBed Then
            resultInBed = CUShort((selectorInBed And &HFFF8US) Or 2US)
        Else
            resultInBed = CUShort(selectorInBed And &HFFFCUS)
        End If
        If externalInBed Then resultInBed = CUShort(resultInBed Or 1US)
        Return resultInBed
    End Function
End Class
