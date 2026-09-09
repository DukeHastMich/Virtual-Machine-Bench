Imports System
Imports System.Diagnostics
Imports System.Threading

' Canonical clock identity for the late, fully-loaded 286 profile.
' The guest-visible processor is a Harris CS80C286-25. Instruction semantics
' and undocumented edge cases fall back to the late Intel M80C286 unless a
' Harris-specific source establishes a difference.
Public NotInheritable Class MachineProfile286
    Private Sub New()
    End Sub

    Public Const ProcessorVendor As String = "Harris Semiconductor"
    Public Const ProcessorModel As String = "CS80C286-25"
    Public Const IntelReferenceModel As String = "M80C286"

    Public Const NormalCpuClockHz As Long = 20000000L
    Public Const TurboCpuClockHz As Long = 25000000L
    Public Const IsaClockHz As Long = 8333333L
    Public Const PitInputClockHz As Long = 1193182L
    Public Const RtcCrystalHz As Long = 32768L

    Public Const PicosecondsPerSecond As Long = 1000000000000L
End Class

Public Enum ProcessorSpeedMode
    Normal20MHz = 0
    Turbo25MHz = 1
End Enum

' Converts host elapsed time into physical emulated time, then lets the CPU
' consume that time as whole T-states. Physical-time debt is retained across
' host stalls and turbo changes, so changing CPU frequency cannot alter the
' amount of motherboard time already owed.
Public NotInheritable Class MachineClock286
    Implements ITimeBatchSynchronizer286
    Private _speedMode As ProcessorSpeedMode = ProcessorSpeedMode.Turbo25MHz
    Private _hostPicosecondNumeratorRemainder As Long
    Private _hostRateNumeratorRemainder As Long
    Private _hostExecutionRatePercentInBed As Integer = 100
    Private _pendingPicoseconds As Long
    Private _totalPicoseconds As Long
    Private _lastClockBatchFlushCount As Long
    Private _lastClockBatchMaximumTStates As Long
    Private _lastClockBatchConsumedTStates As Long
    Private _lastClockBatchLargestFlushTStates As Long
    Private _lastClockBatchPortFlushCount As Long
    Private _lastClockBatchMemoryFlushCount As Long
    Private _lastClockBatchWakeFlushCount As Long
    Private _lastClockBatchCeilingFlushCount As Long
    Private _lastClockBatchEndFlushCount As Long
    Private _lastClockBatchExplicitFlushCount As Long

    ' ACTIVE RUNSLICE COORDINATOR
    ' ---------------------------
    ' These fields used to live in compiler-generated closure objects allocated
    ' inside every RunSlice().  MachineClock286 is single-owner on the machine
    ' worker, so one reusable state frame is sufficient and keeps the exact same
    ' motherboard-time algorithm without per-slice closure allocation or
    ' per-instruction Action.Invoke dispatch.
    Private _batchBusInBed As HardwareBus
    Private _batchTStatePicosecondsInBed As Long
    Private _batchMaximumPicosecondsInBed As Long
    Private _batchAdvancedPicosecondsInBed As Long
    Private _batchPendingPicosecondsInBed As Long
    Private _batchNextWakePicosecondsInBed As Long = Long.MaxValue
    Private _batchDeadlineDirtyInBed As Boolean

    Private _batchFlushCountInBed As Long
    Private _batchLargestFlushTStatesInBed As Long
    Private _batchPortFlushCountInBed As Long
    Private _batchMemoryFlushCountInBed As Long
    Private _batchWakeFlushCountInBed As Long
    Private _batchCeilingFlushCountInBed As Long
    Private _batchEndFlushCountInBed As Long
    Private _batchExplicitFlushCountInBed As Long

    ' Host-only dyno telemetry; none of these values are guest-visible.
    Private Const PreferredMaximumBatchTStates As Long = 32768L
    Private Const LegacyMaximumBatchTStates As Long = 64L

    Public Event CpuStateSampled(stateByte As Byte)
    Public Event SpeedModeChanged(turboEnabled As Boolean)
    Public Event HostExecutionRateChanged(ratePercentInBed As Integer)

    Public Property ThrottleToRealTime As Boolean = True

    ' Host-only pacing. Zero means unlimited; otherwise this scales how much
    ' emulated physical time is admitted per unit of host wall time. The CPU,
    ' PIT, DMA, video, storage and every other clocked device still advance from
    ' the same machine timeline and retain their real hardware ratios.
    Public Property HostExecutionRatePercent As Integer
        Get
            Return _hostExecutionRatePercentInBed
        End Get
        Set(value As Integer)
            If value <> 0 AndAlso (value < 25 OrElse value > 1600) Then
                Throw New ArgumentOutOfRangeException(NameOf(value))
            End If
            If _hostExecutionRatePercentInBed = value Then Return
            _hostExecutionRatePercentInBed = value
            ThrottleToRealTime = value <> 0
            _hostRateNumeratorRemainder = 0
            RaiseEvent HostExecutionRateChanged(value)
        End Set
    End Property

    Public Property SpeedMode As ProcessorSpeedMode
        Get
            Return _speedMode
        End Get
        Set(value As ProcessorSpeedMode)
            If value <> ProcessorSpeedMode.Normal20MHz AndAlso value <> ProcessorSpeedMode.Turbo25MHz Then
                Throw New ArgumentOutOfRangeException(NameOf(value))
            End If
            If _speedMode = value Then Return
            _speedMode = value
            RaiseEvent SpeedModeChanged(TurboEnabled)
        End Set
    End Property

    Public ReadOnly Property CpuClockHz As Long
        Get
            Return If(_speedMode = ProcessorSpeedMode.Turbo25MHz,
                      MachineProfile286.TurboCpuClockHz,
                      MachineProfile286.NormalCpuClockHz)
        End Get
    End Property

    Public ReadOnly Property PicosecondsPerTState As Long
        Get
            ' Both supported clocks divide 10^12 exactly: 20 MHz = 50 ns,
            ' 25 MHz = 40 ns.
            Return MachineProfile286.PicosecondsPerSecond \ CpuClockHz
        End Get
    End Property

    Public ReadOnly Property PendingPicoseconds As Long
        Get
            Return _pendingPicoseconds
        End Get
    End Property

    Public ReadOnly Property PendingTStates As Long
        Get
            Return _pendingPicoseconds \ PicosecondsPerTState
        End Get
    End Property

    Public ReadOnly Property TotalPicoseconds As Long
        Get
            Return _totalPicoseconds
        End Get
    End Property

    Public ReadOnly Property TurboEnabled As Boolean
        Get
            Return _speedMode = ProcessorSpeedMode.Turbo25MHz
        End Get
    End Property

    Public ReadOnly Property LastClockBatchFlushCount As Long
        Get
            Return _lastClockBatchFlushCount
        End Get
    End Property

    Public ReadOnly Property LastClockBatchMaximumTStates As Long
        Get
            Return _lastClockBatchMaximumTStates
        End Get
    End Property

    Public ReadOnly Property LastClockBatchAverageTStates As Double
        Get
            If _lastClockBatchFlushCount <= 0 Then Return 0.0
            Return CDbl(_lastClockBatchConsumedTStates) / CDbl(_lastClockBatchFlushCount)
        End Get
    End Property

    Public ReadOnly Property LastClockBatchLargestFlushTStates As Long
        Get
            Return _lastClockBatchLargestFlushTStates
        End Get
    End Property

    Public ReadOnly Property LastClockBatchPortFlushCount As Long
        Get
            Return _lastClockBatchPortFlushCount
        End Get
    End Property

    Public ReadOnly Property LastClockBatchMemoryFlushCount As Long
        Get
            Return _lastClockBatchMemoryFlushCount
        End Get
    End Property

    Public ReadOnly Property LastClockBatchWakeFlushCount As Long
        Get
            Return _lastClockBatchWakeFlushCount
        End Get
    End Property

    Public ReadOnly Property LastClockBatchCeilingFlushCount As Long
        Get
            Return _lastClockBatchCeilingFlushCount
        End Get
    End Property

    Public ReadOnly Property LastClockBatchEndFlushCount As Long
        Get
            Return _lastClockBatchEndFlushCount
        End Get
    End Property

    Public ReadOnly Property LastClockBatchExplicitFlushCount As Long
        Get
            Return _lastClockBatchExplicitFlushCount
        End Get
    End Property

    Public Sub SetTurbo(enabled As Boolean)
        SpeedMode = If(enabled, ProcessorSpeedMode.Turbo25MHz, ProcessorSpeedMode.Normal20MHz)
    End Sub

    Public Sub Reset()
        _hostPicosecondNumeratorRemainder = 0
        _hostRateNumeratorRemainder = 0
        _pendingPicoseconds = 0
        _totalPicoseconds = 0
        _lastClockBatchFlushCount = 0
        _lastClockBatchMaximumTStates = 0
        _lastClockBatchConsumedTStates = 0
        _lastClockBatchLargestFlushTStates = 0
        _lastClockBatchPortFlushCount = 0
        _lastClockBatchMemoryFlushCount = 0
        _lastClockBatchWakeFlushCount = 0
        _lastClockBatchCeilingFlushCount = 0
        _lastClockBatchEndFlushCount = 0
        _lastClockBatchExplicitFlushCount = 0
    End Sub

    ' Runs one bounded host slice. CPU time is accumulated cheaply and the
    ' motherboard is advanced only at a real wake deadline, a guest I/O/MMIO
    ' synchronization point, or a bounded batch boundary. Unserved host time
    ' remains debt exactly as before.
    Public Function RunSlice(cpu As Processor286,
                             bus As HardwareBus,
                             elapsedStopwatchTicks As Long,
                             maxTStates As Long) As Long
        If bus Is Nothing Then Throw New ArgumentNullException(NameOf(bus))
        If elapsedStopwatchTicks < 0 Then Throw New ArgumentOutOfRangeException(NameOf(elapsedStopwatchTicks))
        If maxTStates <= 0 Then Throw New ArgumentOutOfRangeException(NameOf(maxTStates))

        If ThrottleToRealTime Then
            AccumulateHostTime(elapsedStopwatchTicks)
        Else
            _pendingPicoseconds += maxTStates * PicosecondsPerTState
        End If

        Dim availableTStates As Long = _pendingPicoseconds \ PicosecondsPerTState
        If availableTStates <= 0 Then Return 0

        Dim requested As Long = Math.Min(availableTStates, maxTStates)
        Dim tStatePs As Long = PicosecondsPerTState

        ' An empty CPU socket is a valid powered-board configuration. The crystal,
        ' PIT, RTC, DMA and other independently clocked motherboard hardware keep
        ' seeing physical time, but no processor is scheduled and no bus cycles are
        ' invented on its behalf. Use CPU T-state duration only as the existing
        ' bounded host-slice quantum for accounting.
        If cpu Is Nothing Then
            Dim elapsedPicosecondsInBed As Long = requested * tStatePs
            bus.AdvanceTime(elapsedPicosecondsInBed)
            _pendingPicoseconds -= elapsedPicosecondsInBed
            _totalPicoseconds += elapsedPicosecondsInBed
            _lastClockBatchFlushCount = 0
            _lastClockBatchMaximumTStates = 0
            _lastClockBatchConsumedTStates = requested
            _lastClockBatchLargestFlushTStates = 0
            _lastClockBatchPortFlushCount = 0
            _lastClockBatchMemoryFlushCount = 0
            _lastClockBatchWakeFlushCount = 0
            _lastClockBatchCeilingFlushCount = 0
            _lastClockBatchEndFlushCount = 0
            _lastClockBatchExplicitFlushCount = 0
            RaiseEvent CpuStateSampled(0)
            Return requested
        End If

        Dim maximumBatchTStates As Long =
            If(bus.HasUnclassifiedClockedDevices, LegacyMaximumBatchTStates, PreferredMaximumBatchTStates)
        Dim maximumBatchPicoseconds As Long = maximumBatchTStates * tStatePs

        BeginClockBatchInBed(bus,
                             tStatePs,
                             maximumBatchPicoseconds)

        bus.InstallTimeBatchSynchronizer(Me)
        Dim consumed As Long
        Try
            RefreshBatchDeadlineInBed()
            consumed = cpu.RunForTStates(requested, Me)
            FlushPendingBatchInBed(ClockBatchFlushReason.EndOfSlice)
        Finally
            bus.ClearTimeBatchSynchronizer()
        End Try

        If consumed <= 0 Then Return 0

        If RuntimeDiagnostics.Enabled Then
            _lastClockBatchFlushCount = _batchFlushCountInBed
            _lastClockBatchMaximumTStates = maximumBatchTStates
            _lastClockBatchConsumedTStates = consumed
            _lastClockBatchLargestFlushTStates = _batchLargestFlushTStatesInBed
            _lastClockBatchPortFlushCount = _batchPortFlushCountInBed
            _lastClockBatchMemoryFlushCount = _batchMemoryFlushCountInBed
            _lastClockBatchWakeFlushCount = _batchWakeFlushCountInBed
            _lastClockBatchCeilingFlushCount = _batchCeilingFlushCountInBed
            _lastClockBatchEndFlushCount = _batchEndFlushCountInBed
            _lastClockBatchExplicitFlushCount = _batchExplicitFlushCountInBed
        End If

        _pendingPicoseconds -= _batchAdvancedPicosecondsInBed
        _totalPicoseconds += _batchAdvancedPicosecondsInBed
        EndClockBatchInBed()

        ' Preserve the host-only front-panel telemetry from the diagnostics branch.
        RaiseEvent CpuStateSampled(cpu.LastRunStateByte)
        Return consumed
    End Function

    Private Sub BeginClockBatchInBed(busInBed As HardwareBus,
                                     tStatePicosecondsInBed As Long,
                                     maximumBatchPicosecondsInBed As Long)
        _batchBusInBed = busInBed
        _batchTStatePicosecondsInBed = tStatePicosecondsInBed
        _batchMaximumPicosecondsInBed = maximumBatchPicosecondsInBed
        _batchAdvancedPicosecondsInBed = 0
        _batchPendingPicosecondsInBed = 0
        _batchNextWakePicosecondsInBed = Long.MaxValue
        _batchDeadlineDirtyInBed = True

        If RuntimeDiagnostics.Enabled Then
            _batchFlushCountInBed = 0
            _batchLargestFlushTStatesInBed = 0
            _batchPortFlushCountInBed = 0
            _batchMemoryFlushCountInBed = 0
            _batchWakeFlushCountInBed = 0
            _batchCeilingFlushCountInBed = 0
            _batchEndFlushCountInBed = 0
            _batchExplicitFlushCountInBed = 0
        End If
    End Sub

    Private Sub EndClockBatchInBed()
        _batchBusInBed = Nothing
        _batchPendingPicosecondsInBed = 0
        _batchNextWakePicosecondsInBed = Long.MaxValue
        _batchDeadlineDirtyInBed = True
    End Sub

    <Runtime.CompilerServices.MethodImpl(Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)>
    Friend Sub AdvanceCpuTStatesInBed(tStatesInBed As Long)
        If tStatesInBed <= 0 Then Return

        _batchPendingPicosecondsInBed +=
            tStatesInBed * _batchTStatePicosecondsInBed

        If _batchDeadlineDirtyInBed Then
            RefreshBatchDeadlineInBed()
        End If

        If _batchNextWakePicosecondsInBed <> Long.MaxValue AndAlso
           _batchPendingPicosecondsInBed >= _batchNextWakePicosecondsInBed Then
            FlushPendingBatchInBed(ClockBatchFlushReason.WakeDeadline)
        ElseIf _batchPendingPicosecondsInBed >= _batchMaximumPicosecondsInBed Then
            FlushPendingBatchInBed(ClockBatchFlushReason.BatchCeiling)
        End If
    End Sub

    Friend Function HaltedStepBudgetInBed(remainingTStatesInBed As Long) As Long
        If remainingTStatesInBed <= 0 Then Return 0

        Dim busInBed As HardwareBus = _batchBusInBed
        If busInBed Is Nothing Then
            Return Math.Min(LegacyMaximumBatchTStates, remainingTStatesInBed)
        End If

        If busInBed.HasUnclassifiedClockedDevices Then
            Return Math.Min(LegacyMaximumBatchTStates, remainingTStatesInBed)
        End If

        If _batchDeadlineDirtyInBed Then
            RefreshBatchDeadlineInBed()
        End If
        If _batchNextWakePicosecondsInBed = Long.MaxValue Then
            Return remainingTStatesInBed
        End If

        Dim untilWakePicosecondsInBed As Long =
            _batchNextWakePicosecondsInBed - _batchPendingPicosecondsInBed
        If untilWakePicosecondsInBed <= 0 Then
            FlushPendingBatchInBed(ClockBatchFlushReason.WakeDeadline)
            If _batchNextWakePicosecondsInBed = Long.MaxValue Then
                Return remainingTStatesInBed
            End If
            untilWakePicosecondsInBed = _batchNextWakePicosecondsInBed
        End If

        Dim untilWakeTStatesInBed As Long =
            Math.Max(
                1L,
                (untilWakePicosecondsInBed +
                 _batchTStatePicosecondsInBed - 1L) \
                _batchTStatePicosecondsInBed)

        Return Math.Min(remainingTStatesInBed, untilWakeTStatesInBed)
    End Function

    Private Sub RefreshBatchDeadlineInBed()
        Dim busInBed As HardwareBus = _batchBusInBed
        If busInBed Is Nothing Then
            _batchNextWakePicosecondsInBed = Long.MaxValue
        Else
            _batchNextWakePicosecondsInBed =
                busInBed.PicosecondsUntilNextWakeEvent()
        End If
        _batchDeadlineDirtyInBed = False
    End Sub

    Private Sub FlushPendingBatchInBed(reasonInBed As ClockBatchFlushReason)
        Dim busInBed As HardwareBus = _batchBusInBed
        If busInBed Is Nothing Then Return

        If _batchPendingPicosecondsInBed > 0 Then
            Dim elapsedPicosecondsInBed As Long =
                _batchPendingPicosecondsInBed
            Dim flushedTStatesInBed As Long =
                elapsedPicosecondsInBed \ _batchTStatePicosecondsInBed

            _batchPendingPicosecondsInBed = 0
            busInBed.AdvanceTime(elapsedPicosecondsInBed)
            _batchAdvancedPicosecondsInBed += elapsedPicosecondsInBed

            If RuntimeDiagnostics.Enabled Then
                _batchFlushCountInBed += 1
                If flushedTStatesInBed > _batchLargestFlushTStatesInBed Then
                    _batchLargestFlushTStatesInBed = flushedTStatesInBed
                End If

                Select Case reasonInBed
                    Case ClockBatchFlushReason.PortAccess
                        _batchPortFlushCountInBed += 1
                    Case ClockBatchFlushReason.MemoryAccess
                        _batchMemoryFlushCountInBed += 1
                    Case ClockBatchFlushReason.WakeDeadline
                        _batchWakeFlushCountInBed += 1
                    Case ClockBatchFlushReason.BatchCeiling
                        _batchCeilingFlushCountInBed += 1
                    Case ClockBatchFlushReason.EndOfSlice
                        _batchEndFlushCountInBed += 1
                    Case Else
                        _batchExplicitFlushCountInBed += 1
                End Select
            End If
        End If

        RefreshBatchDeadlineInBed()
    End Sub

    Private Sub SynchronizePendingTime(
        reasonInBed As ClockBatchFlushReason) Implements ITimeBatchSynchronizer286.SynchronizePendingTime

        FlushPendingBatchInBed(reasonInBed)
    End Sub

    Private Sub TimingTopologyChanged() Implements ITimeBatchSynchronizer286.TimingTopologyChanged
        _batchDeadlineDirtyInBed = True
    End Sub

    Private Sub AccumulateHostTime(elapsedStopwatchTicks As Long)
        If elapsedStopwatchTicks = 0 Then Return

        Dim frequency As Long = Stopwatch.Frequency
        Dim wholeSeconds As Long = elapsedStopwatchTicks \ frequency
        Dim remainderTicks As Long = elapsedStopwatchTicks Mod frequency
        Dim unscaledPicosecondsInBed As Long = wholeSeconds * MachineProfile286.PicosecondsPerSecond

        ' Decompose 10^12/F before multiplication. This avoids overflowing
        ' Int64 on high-resolution host timers while retaining the exact
        ' fractional remainder between slices.
        Dim wholePicosecondsPerTick As Long = MachineProfile286.PicosecondsPerSecond \ frequency
        Dim fractionalNumeratorPerTick As Long = MachineProfile286.PicosecondsPerSecond Mod frequency
        unscaledPicosecondsInBed += remainderTicks * wholePicosecondsPerTick

        Dim fractionalNumerator As Long =
            remainderTicks * fractionalNumeratorPerTick + _hostPicosecondNumeratorRemainder
        unscaledPicosecondsInBed += fractionalNumerator \ frequency
        _hostPicosecondNumeratorRemainder = fractionalNumerator Mod frequency

        ' Scale by the host pacing percentage without forming
        ' unscaledPicoseconds * ratePercent as one Int64 product.  A single
        ' long host discontinuity (sleep/suspend/debugger pause) can make that
        ' intermediate overflow even though the final /100 result is nowhere
        ' near Int64.MaxValue.  Divide by 100 first, then carry the exact
        ' hundredth-picosecond remainder across calls.
        Dim ratePercentInBed As Long = CLng(_hostExecutionRatePercentInBed)
        Dim wholeHundredsInBed As Long = unscaledPicosecondsInBed \ 100L
        Dim remainderPicosecondsInBed As Long = unscaledPicosecondsInBed Mod 100L
        Dim scaledPicosecondsInBed As Long = wholeHundredsInBed * ratePercentInBed
        Dim scaledRemainderNumeratorInBed As Long =
            remainderPicosecondsInBed * ratePercentInBed + _hostRateNumeratorRemainder
        scaledPicosecondsInBed += scaledRemainderNumeratorInBed \ 100L
        _hostRateNumeratorRemainder = scaledRemainderNumeratorInBed Mod 100L
        _pendingPicoseconds += scaledPicosecondsInBed
    End Sub
End Class

' CROMWELL HOST REFIT BRICK 9C - single-owner machine execution thread.
'
' CPU, motherboard bridge, HardwareBus and guest devices remain one synchronous
' electrical machine.  This class moves that entire timeline off the WinForms
' message pump; it does NOT create an independently concurrent "bus thread".
'
' Host UI code which still needs live machine state crosses one ownership gate.
' That deliberately conservative boundary can later be replaced with command
' queues / immutable snapshots without changing guest hardware semantics.
Public NotInheritable Class MachineRuntime286
    Implements IDisposable

    Public Const MaximumTStatesPerSlice As Long = 25000L
    Private Const MinimumTStatesPerSliceInBed As Long = 256L

    Private _cpuInBed As Processor286
    Private ReadOnly _busInBed As HardwareBus
    Private ReadOnly _clockInBed As MachineClock286
    Private ReadOnly _executionGateInBed As New Object()
    Private ReadOnly _wakeInBed As New AutoResetEvent(False)

    Private _workerInBed As Thread
    Private _stopRequestedInBed As Integer
    Private _runningInBed As Integer
    Private _lastHostTimestampInBed As Long
    Private _adaptiveMaximumTStatesPerSliceInBed As Long = MaximumTStatesPerSlice
    Private _externalGateWaitersInBed As Integer
    Private _boundaryServiceInBed As Action
    Private _disposedInBed As Boolean

    Public Event SliceCompleted(consumedTStatesInBed As Long,
                                elapsedHostTicksInBed As Long,
                                executionHostTicksInBed As Long,
                                targetClockHzInBed As Long)
    Public Event RuntimeFaulted(faultInBed As Exception)
    Public Event BoundaryServiceFaulted(faultInBed As Exception)

    Public Sub New(cpuInBed As Processor286,
                   busInBed As HardwareBus,
                   clockInBed As MachineClock286)
        If busInBed Is Nothing Then Throw New ArgumentNullException(NameOf(busInBed))
        If clockInBed Is Nothing Then Throw New ArgumentNullException(NameOf(clockInBed))
        _cpuInBed = cpuInBed
        _busInBed = busInBed
        _clockInBed = clockInBed
    End Sub

    Public ReadOnly Property IsRunning As Boolean
        Get
            Return Volatile.Read(_runningInBed) <> 0
        End Get
    End Property

    Public ReadOnly Property ProcessorInstalled As Boolean
        Get
            SyncLock _executionGateInBed
                Return _cpuInBed IsNot Nothing
            End SyncLock
        End Get
    End Property

    ' Cold socket operation. Callers stop the runtime for a physical CPU swap.
    ' Nothing is not a halted CPU; it means there is no processor object scheduled.
    Public Sub SetProcessor(cpuInBed As Processor286)
        ThrowIfDisposedInBed()
        EnterExternalGateInBed()
        Try
            If _runningInBed <> 0 Then
                Throw New InvalidOperationException("The CPU package may only be replaced while the machine runtime is stopped.")
            End If
            _cpuInBed = cpuInBed
            _lastHostTimestampInBed = Stopwatch.GetTimestamp()
        Finally
            ExitExternalGateInBed()
        End Try
        _wakeInBed.Set()
    End Sub

    Public ReadOnly Property CurrentMaximumTStatesPerSlice As Long
        Get
            Return Interlocked.Read(_adaptiveMaximumTStatesPerSliceInBed)
        End Get
    End Property

    Public Sub Start()
        If _disposedInBed Then Throw New ObjectDisposedException(NameOf(MachineRuntime286))
        SyncLock _executionGateInBed
            If _runningInBed <> 0 Then Return
            Interlocked.Exchange(_stopRequestedInBed, 0)
            _lastHostTimestampInBed = Stopwatch.GetTimestamp()
            _workerInBed = New Thread(AddressOf WorkerLoopInBed) With {
                .IsBackground = True,
                .Name = "Virtual Computer 80286 machine",
                .Priority = ThreadPriority.Normal
            }
            Interlocked.Exchange(_runningInBed, 1)
            _workerInBed.Start()
        End SyncLock
    End Sub

    Public Sub [Stop]()
        Dim workerInBed As Thread = Nothing
        SyncLock _executionGateInBed
            ' Join a worker object even if it has already marked IsRunning false
            ' after a fault.  This closes the tiny teardown race before wait-handle
            ' disposal and makes Stop idempotent.
            workerInBed = _workerInBed
            If workerInBed Is Nothing Then Return
            Interlocked.Exchange(_stopRequestedInBed, 1)
            _wakeInBed.Set()
        End SyncLock

        If workerInBed IsNot Thread.CurrentThread Then workerInBed.Join()

        SyncLock _executionGateInBed
            If Object.ReferenceEquals(_workerInBed, workerInBed) Then _workerInBed = Nothing
            Interlocked.Exchange(_runningInBed, 0)
        End SyncLock
    End Sub

    Private Sub ThrowIfDisposedInBed()
        If _disposedInBed Then Throw New ObjectDisposedException(NameOf(MachineRuntime286))
    End Sub

    Private Sub EnterExternalGateInBed()
        Interlocked.Increment(_externalGateWaitersInBed)
        Try
            Monitor.Enter(_executionGateInBed)
        Catch
            Interlocked.Decrement(_externalGateWaitersInBed)
            Throw
        End Try
    End Sub

    Private Sub ExitExternalGateInBed()
        Monitor.Exit(_executionGateInBed)
        Interlocked.Decrement(_externalGateWaitersInBed)
    End Sub

    Public Sub Execute(actionInBed As Action)
        If actionInBed Is Nothing Then Throw New ArgumentNullException(NameOf(actionInBed))
        ThrowIfDisposedInBed()
        EnterExternalGateInBed()
        Try
            actionInBed.Invoke()
        Finally
            ExitExternalGateInBed()
        End Try
        _wakeInBed.Set()
    End Sub

    Public Function Query(Of TResult)(queryInBed As Func(Of TResult)) As TResult
        If queryInBed Is Nothing Then Throw New ArgumentNullException(NameOf(queryInBed))
        ThrowIfDisposedInBed()
        EnterExternalGateInBed()
        Try
            Return queryInBed.Invoke()
        Finally
            ExitExternalGateInBed()
        End Try
    End Function

    ' Installs one host-only service that is invoked by the machine thread at the
    ' end of each bounded guest slice while the execution gate is already owned.
    ' This is the safe place to snapshot guest device state without lock gambling.
    Public Sub SetBoundaryService(serviceInBed As Action)
        ThrowIfDisposedInBed()
        EnterExternalGateInBed()
        Try
            _boundaryServiceInBed = serviceInBed
        Finally
            ExitExternalGateInBed()
        End Try
        _wakeInBed.Set()
    End Sub

    ' Use for an intentional host-side discontinuity such as a chassis power
    ' cycle. Time spent rebuilding powered-off hardware is not guest elapsed time.
    Public Sub ExecuteWithHostTimeRebase(actionInBed As Action)
        If actionInBed Is Nothing Then Throw New ArgumentNullException(NameOf(actionInBed))
        ThrowIfDisposedInBed()
        EnterExternalGateInBed()
        Try
            actionInBed.Invoke()
            _lastHostTimestampInBed = Stopwatch.GetTimestamp()
        Finally
            ExitExternalGateInBed()
        End Try
        _wakeInBed.Set()
    End Sub

    Public Sub RebaseHostClock()
        ThrowIfDisposedInBed()
        EnterExternalGateInBed()
        Try
            _lastHostTimestampInBed = Stopwatch.GetTimestamp()
        Finally
            ExitExternalGateInBed()
        End Try
        _wakeInBed.Set()
    End Sub

    ' Host-only lifecycle repair used after a chassis reset or cold power-on.
    ' Guest clock/debt semantics are reset separately by MachineClock286.Reset().
    ' This only forgets the previous workload's adaptive host slice estimate and
    ' rebases the wall-clock timestamp before a new worker run begins.
    Public Sub ResetHostSchedulingState()
        ThrowIfDisposedInBed()
        EnterExternalGateInBed()
        Try
            Interlocked.Exchange(_adaptiveMaximumTStatesPerSliceInBed,
                                 MaximumTStatesPerSlice)
            _lastHostTimestampInBed = Stopwatch.GetTimestamp()
        Finally
            ExitExternalGateInBed()
        End Try
        _wakeInBed.Set()
    End Sub

    Private Sub AdaptHostSliceCeilingInBed(consumedTStatesInBed As Long,
                                               executionTicksInBed As Long)
        If consumedTStatesInBed <= 0 OrElse executionTicksInBed <= 0 Then Return

        Dim targetTicksInBed As Long = Math.Max(1L, Stopwatch.Frequency \ 1000L)
        Dim currentInBed As Long =
            Math.Max(MinimumTStatesPerSliceInBed,
                     Interlocked.Read(_adaptiveMaximumTStatesPerSliceInBed))

        ' Derive cost from what actually executed, not from the requested ceiling.
        Dim desiredInBed As Long =
            Math.Max(1L, (consumedTStatesInBed * targetTicksInBed) \ executionTicksInBed)

        ' Smooth adaptation to avoid oscillating wildly on one pathological slice.
        Dim minimumStepInBed As Long =
            Math.Max(MinimumTStatesPerSliceInBed, currentInBed \ 2L)
        Dim maximumStepInBed As Long =
            Math.Min(MaximumTStatesPerSlice, Math.Max(currentInBed + 1L, currentInBed * 2L))
        desiredInBed = Math.Max(minimumStepInBed, Math.Min(maximumStepInBed, desiredInBed))
        desiredInBed = Math.Max(MinimumTStatesPerSliceInBed,
                                Math.Min(MaximumTStatesPerSlice, desiredInBed))
        Interlocked.Exchange(_adaptiveMaximumTStatesPerSliceInBed, desiredInBed)
    End Sub

    Private Sub WorkerLoopInBed()
        Try
            While Volatile.Read(_stopRequestedInBed) = 0
                Dim consumedInBed As Long
                Dim elapsedTicksInBed As Long
                Dim executionTicksInBed As Long
                Dim targetClockHzInBed As Long
                Dim debtTStatesInBed As Long
                Dim boundaryFaultInBed As Exception = Nothing

                SyncLock _executionGateInBed
                    Dim nowInBed As Long = Stopwatch.GetTimestamp()
                    elapsedTicksInBed = Math.Max(0L, nowInBed - _lastHostTimestampInBed)
                    _lastHostTimestampInBed = nowInBed

                    Dim executionStartInBed As Long = Stopwatch.GetTimestamp()
                    Dim sliceCeilingInBed As Long =
                        Math.Max(MinimumTStatesPerSliceInBed,
                                 Math.Min(MaximumTStatesPerSlice,
                                          Interlocked.Read(_adaptiveMaximumTStatesPerSliceInBed)))
                    consumedInBed = _clockInBed.RunSlice(_cpuInBed,
                                                        _busInBed,
                                                        elapsedTicksInBed,
                                                        sliceCeilingInBed)
                    executionTicksInBed = Stopwatch.GetTimestamp() - executionStartInBed
                    targetClockHzInBed = _clockInBed.CpuClockHz
                    debtTStatesInBed = _clockInBed.PendingTStates

                    Dim boundaryServiceInBed As Action = _boundaryServiceInBed
                    If boundaryServiceInBed IsNot Nothing Then
                        Try
                            boundaryServiceInBed.Invoke()
                        Catch ex As Exception
                            ' A host observer/presenter must never kill guest execution.
                            ' Disable the broken service and report it outside the gate.
                            _boundaryServiceInBed = Nothing
                            boundaryFaultInBed = ex
                        End Try
                    End If
                End SyncLock

                If boundaryFaultInBed IsNot Nothing Then
                    RaiseEvent BoundaryServiceFaulted(boundaryFaultInBed)
                End If

                AdaptHostSliceCeilingInBed(consumedInBed, executionTicksInBed)
                RaiseEvent SliceCompleted(consumedInBed,
                                          elapsedTicksInBed,
                                          executionTicksInBed,
                                          targetClockHzInBed)

                ' When debt exists, immediately spend another bounded slice so a
                ' host stall can actually be recovered.  Once caught up, sleep at
                ' most ~1 ms instead of burning a host core polling for wall time.
                If Volatile.Read(_externalGateWaitersInBed) > 0 Then
                    ' An operator/menu/input request is already waiting. Give it a
                    ' real scheduler quantum before trying to reacquire the gate.
                    Thread.Sleep(1)
                ElseIf debtTStatesInBed > 0 Then
                    Thread.Sleep(0)
                Else
                    _wakeInBed.WaitOne(1)
                End If
            End While
        Catch ex As Exception
            Interlocked.Exchange(_stopRequestedInBed, 1)
            RaiseEvent RuntimeFaulted(ex)
        Finally
            Interlocked.Exchange(_runningInBed, 0)
        End Try
    End Sub

    Public Sub Dispose() Implements IDisposable.Dispose
        If _disposedInBed Then Return
        [Stop]()
        _boundaryServiceInBed = Nothing
        _wakeInBed.Dispose()
        _disposedInBed = True
    End Sub
End Class

