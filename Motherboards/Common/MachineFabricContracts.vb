Imports System

' Processor-family-neutral local-bus contract.  It deliberately models electrical
' transaction facts rather than exposing RAM arrays or host-side shortcuts.
Public Enum ProcessorBusCycleKind As Byte
    MemoryRead = 0
    MemoryWrite = 1
    IoRead = 2
    IoWrite = 3
    InterruptAcknowledge = 4
    HaltOrShutdown = 5
    Special = 6
End Enum

Public Enum ProcessorBusDataWidth As Byte
    Width8 = 1
    Width16 = 2
    Width32 = 4
End Enum

Public Enum ProcessorBusReadyKind As Byte
    Ready = 0       ' ordinary RDY-style termination
    BurstReady = 1  ' BRDY-style termination
    Backoff = 2     ' bus cycle was backed off and must be restarted
End Enum

Public Structure ProcessorBusCycleRequest
    ' Byte address.  A 486 package derives A31:A2 and active-low BE3#:BE0# from
    ' this plus Width; the fabric may dynamically narrow the transfer.
    Public Address As UInteger
    Public Port As UInt16
    Public Kind As ProcessorBusCycleKind
    Public Width As ProcessorBusDataWidth
    Public WriteData As UInteger

    ' Active-high logical lane mask.  Bit 0 corresponds to physical BE0# being
    ' asserted LOW, through bit 3 / BE3#.  Zero means derive contiguous lanes from
    ' Address and Width.
    Public ActiveByteLaneMask As Byte

    Public LockAsserted As Boolean
    Public PseudoLockAsserted As Boolean
    Public BurstPermitted As Boolean
    Public CacheFillCandidate As Boolean
    Public PageWriteThrough As Boolean
    Public PageCacheDisable As Boolean

    Public ReadOnly Property IsWrite As Boolean
        Get
            Return Kind = ProcessorBusCycleKind.MemoryWrite OrElse Kind = ProcessorBusCycleKind.IoWrite
        End Get
    End Property

    Public ReadOnly Property IsMemory As Boolean
        Get
            Return Kind = ProcessorBusCycleKind.MemoryRead OrElse Kind = ProcessorBusCycleKind.MemoryWrite
        End Get
    End Property
End Structure

Public Structure ProcessorBusCycleResult
    Public ReadData As UInteger
    Public EffectiveWidth As ProcessorBusDataWidth
    Public ReadyKind As ProcessorBusReadyKind

    ' Physical motherboard clocks consumed from ADS# assertion through the
    ' terminating RDY#/BRDY# sample.  The fabric, not the processor, determines it.
    Public ExternalClocksConsumed As Long

    ' KEN# sampled active for the cycle.  Meaningful only where the processor
    ' package exposes an external cache-enable input.
    Public CacheEnableAsserted As Boolean
    Public ParityError As Boolean

    Public Shared Function Completed(widthInBed As ProcessorBusDataWidth,
                                     clocksInBed As Long,
                                     Optional readDataInBed As UInteger = 0UI) As ProcessorBusCycleResult
        If clocksInBed <= 0 Then Throw New ArgumentOutOfRangeException(NameOf(clocksInBed))
        Return New ProcessorBusCycleResult With {
            .ReadData = readDataInBed,
            .EffectiveWidth = widthInBed,
            .ReadyKind = ProcessorBusReadyKind.Ready,
            .ExternalClocksConsumed = clocksInBed
        }
    End Function
End Structure

Public Interface IProcessorLocalBusFabric
    ReadOnly Property FabricIdentity As String
    ReadOnly Property ExternalClockHz As Long
    ReadOnly Property CurrentExternalClock As ULong

    ' This is a synchronous electrical transaction on the owning motherboard
    ' runtime thread.  Implementations may synchronize devices and wait-state
    ' generators, but must never use host Sleep/Delay as emulated timing.
    Function ExecuteProcessorCycle(requestInBed As ProcessorBusCycleRequest) As ProcessorBusCycleResult

    ' Board-owned clock movement for CPU-internal work which did not require an
    ' external bus transaction.  This keeps PIT/DMA/video/etc. on the same timeline.
    Sub AdvanceExternalClocks(externalClocksInBed As Long)
End Interface

' Read-only capabilities let a processor reject a physically impossible binding
' before power is applied, rather than discovering it through fake open-bus cycles.
Public Interface IProcessorFabricCapabilities
    ReadOnly Property Supports8BitTransfers As Boolean
    ReadOnly Property Supports16BitTransfers As Boolean
    ReadOnly Property Supports32BitTransfers As Boolean
    ReadOnly Property SupportsBurstReady As Boolean
    ReadOnly Property SupportsCacheEnableInput As Boolean
    ReadOnly Property SupportsBusBackoff As Boolean
End Interface

' Compatibility-only adapter over the already-proven NEAT/286 path.  It is not wired
' into MachineRuntime286 and therefore cannot change the green 286 execution path.
' Its sole purpose is to prove the neutral contract can represent the existing board.
Public NotInheritable Class Neat286LocalBusFabricAdapter
    Implements IProcessorLocalBusFabric, IProcessorFabricCapabilities

    Private ReadOnly _busInBed As CpuLocalBus286
    Private _externalClockInBed As ULong
    Private ReadOnly _externalClockHzInBed As Long

    Public Sub New(busInBed As CpuLocalBus286, externalClockHzInBed As Long)
        If busInBed Is Nothing Then Throw New ArgumentNullException(NameOf(busInBed))
        If externalClockHzInBed <= 0 Then Throw New ArgumentOutOfRangeException(NameOf(externalClockHzInBed))
        _busInBed = busInBed
        _externalClockHzInBed = externalClockHzInBed
    End Sub

    Public ReadOnly Property FabricIdentity As String Implements IProcessorLocalBusFabric.FabricIdentity
        Get
            Return "Compatibility adapter: " & _busInBed.Identity
        End Get
    End Property

    Public ReadOnly Property ExternalClockHz As Long Implements IProcessorLocalBusFabric.ExternalClockHz
        Get
            Return _externalClockHzInBed
        End Get
    End Property

    Public ReadOnly Property CurrentExternalClock As ULong Implements IProcessorLocalBusFabric.CurrentExternalClock
        Get
            Return _externalClockInBed
        End Get
    End Property

    Public Function ExecuteProcessorCycle(requestInBed As ProcessorBusCycleRequest) As ProcessorBusCycleResult Implements IProcessorLocalBusFabric.ExecuteProcessorCycle
        Dim resultInBed As ProcessorBusCycleResult
        Dim widthInBed As ProcessorBusDataWidth = requestInBed.Width
        Select Case requestInBed.Kind
            Case ProcessorBusCycleKind.MemoryRead
                Select Case widthInBed
                    Case ProcessorBusDataWidth.Width8
                        resultInBed = ProcessorBusCycleResult.Completed(widthInBed, 1, _busInBed.ReadMemoryByte(requestInBed.Address))
                    Case ProcessorBusDataWidth.Width16
                        resultInBed = ProcessorBusCycleResult.Completed(widthInBed, 1, _busInBed.ReadMemoryWord(requestInBed.Address))
                    Case Else
                        Throw New NotSupportedException("The 80286 compatibility adapter has no native 32-bit memory transfer.")
                End Select
            Case ProcessorBusCycleKind.MemoryWrite
                Select Case widthInBed
                    Case ProcessorBusDataWidth.Width8
                        _busInBed.WriteMemoryByte(requestInBed.Address, CByte(requestInBed.WriteData And &HFFUI))
                    Case ProcessorBusDataWidth.Width16
                        _busInBed.WriteMemoryWord(requestInBed.Address, CUShort(requestInBed.WriteData And &HFFFFUI))
                    Case Else
                        Throw New NotSupportedException("The 80286 compatibility adapter has no native 32-bit memory transfer.")
                End Select
                resultInBed = ProcessorBusCycleResult.Completed(widthInBed, 1)
            Case ProcessorBusCycleKind.IoRead
                Select Case widthInBed
                    Case ProcessorBusDataWidth.Width8
                        resultInBed = ProcessorBusCycleResult.Completed(widthInBed, 1, _busInBed.ReadByte(requestInBed.Port))
                    Case ProcessorBusDataWidth.Width16
                        resultInBed = ProcessorBusCycleResult.Completed(widthInBed, 1, _busInBed.ReadWord(requestInBed.Port))
                    Case Else
                        Throw New NotSupportedException("The 80286 compatibility adapter has no native 32-bit I/O transfer.")
                End Select
            Case ProcessorBusCycleKind.IoWrite
                Select Case widthInBed
                    Case ProcessorBusDataWidth.Width8
                        _busInBed.WriteByte(requestInBed.Port, CByte(requestInBed.WriteData And &HFFUI))
                    Case ProcessorBusDataWidth.Width16
                        _busInBed.WriteWord(requestInBed.Port, CUShort(requestInBed.WriteData And &HFFFFUI))
                    Case Else
                        Throw New NotSupportedException("The 80286 compatibility adapter has no native 32-bit I/O transfer.")
                End Select
                resultInBed = ProcessorBusCycleResult.Completed(widthInBed, 1)
            Case Else
                Throw New NotSupportedException("The compatibility adapter does not synthesize interrupt-acknowledge, halt, or special cycles.")
        End Select

        AdvanceExternalClocks(resultInBed.ExternalClocksConsumed)
        Return resultInBed
    End Function

    Public Sub AdvanceExternalClocks(externalClocksInBed As Long) Implements IProcessorLocalBusFabric.AdvanceExternalClocks
        If externalClocksInBed < 0 Then Throw New ArgumentOutOfRangeException(NameOf(externalClocksInBed))
        _externalClockInBed += CULng(externalClocksInBed)
    End Sub

    Public ReadOnly Property Supports8BitTransfers As Boolean Implements IProcessorFabricCapabilities.Supports8BitTransfers
        Get
            Return True
        End Get
    End Property

    Public ReadOnly Property Supports16BitTransfers As Boolean Implements IProcessorFabricCapabilities.Supports16BitTransfers
        Get
            Return True
        End Get
    End Property

    Public ReadOnly Property Supports32BitTransfers As Boolean Implements IProcessorFabricCapabilities.Supports32BitTransfers
        Get
            Return False
        End Get
    End Property

    Public ReadOnly Property SupportsBurstReady As Boolean Implements IProcessorFabricCapabilities.SupportsBurstReady
        Get
            Return False
        End Get
    End Property

    Public ReadOnly Property SupportsCacheEnableInput As Boolean Implements IProcessorFabricCapabilities.SupportsCacheEnableInput
        Get
            Return False
        End Get
    End Property

    Public ReadOnly Property SupportsBusBackoff As Boolean Implements IProcessorFabricCapabilities.SupportsBusBackoff
        Get
            Return False
        End Get
    End Property
End Class
