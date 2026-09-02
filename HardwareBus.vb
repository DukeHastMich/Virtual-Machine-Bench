Imports System
Imports System.Collections.Generic

Public Interface IPortDevice
    Function HandlesPort(port As UInt16) As Boolean
    Function ReadPort(port As UInt16) As Byte
    Sub WritePort(port As UInt16, value As Byte)
End Interface

Public Interface IClockedDevice
    ' Advance by physical emulated time, not by an assumed processor speed.
    ' This keeps PIT/RTC/audio/video clocks stable while turbo changes the CPU.
    Sub AdvanceTime(elapsedPicoseconds As Long)
End Interface

' CROMWELL EVENT-AWARE CLOCK BATCHING BRICK 1
' A wake source reports the next spontaneous guest-visible event which can
' change processor execution without a CPU I/O access first (IRQ, refresh, etc.).
' Poll-only state does not need to be reported: HardwareBus synchronizes pending
' motherboard time before guest I/O/MMIO touches the device.
Public Interface IClockWakeSource
    Function PicosecondsUntilNextWakeEvent() As Long
End Interface

' Marker for clocked devices which have no spontaneous CPU wake signal in their
' current model. They may be advanced in batches because guest accesses force a
' synchronization first.
Public Interface IClockBatchSafeDevice
End Interface

' CROMWELL CLOCK BATCH DIAGNOSTICS BRICK 2.3
Public Enum ClockBatchFlushReason As Byte
    Explicit = 0
    PortAccess = 1
    MemoryAccess = 2
    WakeDeadline = 3
    BatchCeiling = 4
    EndOfSlice = 5
    Reset = 6
End Enum

' CROMWELL LEAN PCB DECODE BRICK 2
' These interfaces describe host-side representations of physical decode wiring.
' They are not guest-visible enumeration services.
Public Interface IPortDecodeCandidateProvider
    ' True means this module may physically decode the port in some legal
    ' register/configuration state. HardwareBus still calls HandlesPort at the
    ' actual cycle so programmable chip-select behavior remains authentic.
    Function PotentiallyHandlesPort(port As UInt16) As Boolean
End Interface

' CROMWELL PCB REFIT PHASE 2 BRICK 9B - targeted memory-decode invalidation.
' Only hardware whose programmable registers can actually rewire physical
' memory selection raises this event. Ordinary PIC/PIT/DMA/ATA/FDC writes no
' longer destroy the motherboard's compiled 4 KiB memory-route cache.
Public Interface IMemoryDecodeChangeSource
    Event MemoryDecodeChanged()
End Interface

' A page-coherent decoder guarantees that HandlesMemory is identical for every
' byte in one 4 KiB page until a guest I/O write or motherboard reset changes
' decode state. Current 286 motherboard/card windows are all >= 16 KiB.
Public Interface IPageCoherentMemoryDecode
End Interface

' Memory contents on these devices do not depend on elapsed motherboard time.
' VRAM/shadow RAM/open-bus memory cycles therefore do not need to flush pending
' device clocks before every byte access.
Public Interface IMemoryClockIndependentDevice
End Interface

Public Interface IMotherboardLocalPortDevice
End Interface

' Brick 8E timing classification hook.  A mapped-memory device may report that
' a successful cycle terminates on local DRAM/open bus instead of the AT bus.
Public Interface IMemoryCycleTimingTargetProvider
    Function GetMemoryCycleTimingTarget(address As UInteger,
                                        isWrite As Boolean) As AtMemoryCycleTarget286
End Interface

Public Interface IWordPortDevice
    Function ReadPortWord(port As UInt16) As UInt16
    Sub WritePortWord(port As UInt16, value As UInt16)
End Interface

' Guest physical-memory devices (video apertures, option ROMs, etc.) live on
' the motherboard bus just like I/O-port devices.  CPU and DMA traffic must
' therefore reach the device rather than a duplicate host-side RAM mirror.
Public Interface IMemoryMappedDevice
    Function HandlesMemory(address As UInteger) As Boolean
    Function ReadMemoryByte(address As UInteger) As Byte
    Sub WriteMemoryByte(address As UInteger, value As Byte)
End Interface

' Memory devices which may decode only one direction of a bus cycle use this
' interface.  This is required for chipset shadow-RAM and bus-routing logic:
' an address can be decoded for writes while reads still continue to an ISA ROM.
Public Interface IConditionalMemoryMappedDevice
    Inherits IMemoryMappedDevice
    Function TryReadMemoryByte(address As UInteger, ByRef value As Byte) As Boolean
    Function TryWriteMemoryByte(address As UInteger, value As Byte) As Boolean
End Interface

' Motherboard devices are reset by the physical reset line independently of
' CPU architectural reset.  Keeping this boundary explicit prevents a guest
' reset from silently reconstructing devices through host-side shortcuts.
Public Interface IResettableDevice
    Sub ResetDevice()
End Interface

' Cold power loss is a wider physical domain than the motherboard RESET line.
' Devices with volatile storage implement this interface so chassis off/on can
' discard that state while ordinary guest/front-panel reset preserves it.
Public Interface IPowerCycleDevice
    Inherits IResettableDevice
    Sub PowerCycleDevice()
End Interface

' CROMWELL PCB REFIT PHASE 2 BRICK 8A - CPU LOCAL BUS / MOTHERBOARD BRIDGE
' The CPU no longer plugs directly into the device fabric.  This class is the
' explicit 80286 local-bus-to-motherboard boundary.  For this milestone it
' forwards established electrical transactions to HardwareBus unchanged; later
' Phase-2 bricks attach READY/wait-state generation, HOLD/HLDA arbitration,
' refresh ownership, DMA masters, and the memory controller to this boundary.
'
' CROMWELL PCB REFIT PHASE 2 BRICK 8B - NEAT PHYSICAL MEMORY OWNERSHIP
' Physical DRAM/ROM backing and the motherboard A20 state belong to the memory
' controller, not to the Harris CPU core.  Brick 8B moves ownership only; the
' existing Processor286 Read/Write fa�ade remains for compatibility until 8C
' moves bus-cycle routing and DMA masters onto CpuLocalBus286.
Public NotInheritable Class NeatMemoryController286
    Friend ReadOnly LowMemoryInBed(1024 * 1024 - 1) As Byte
    Friend ReadOnly ExtendedMemoryInBed(15 * 1024 * 1024 - 1) As Byte

    Private _installedMemoryBytesInBed As UInteger = &H1000000UI
    Private _romStartInBed As UInteger = &H100000UI
    Private _a20EnabledInBed As Boolean

    Public ReadOnly Property Identity As String
        Get
            Return "C&T NEAT physical memory controller"
        End Get
    End Property

    Public Property A20Enabled As Boolean
        Get
            Return _a20EnabledInBed
        End Get
        Set(value As Boolean)
            _a20EnabledInBed = value
        End Set
    End Property

    Public ReadOnly Property InstalledMemoryBytes As UInteger
        Get
            Return _installedMemoryBytesInBed
        End Get
    End Property

    Public ReadOnly Property InstalledMemoryMegabytes As Integer
        Get
            Return CInt(_installedMemoryBytesInBed \ &H100000UI)
        End Get
    End Property

    Friend ReadOnly Property RomStart As UInteger
        Get
            Return _romStartInBed
        End Get
    End Property

    Public Sub ConfigureInstalledMemoryMegabytes(megabytes As Integer,
                                                  Optional clearRam As Boolean = False)
        If megabytes < 1 OrElse megabytes > 16 Then
            Throw New ArgumentOutOfRangeException(NameOf(megabytes))
        End If

        _installedMemoryBytesInBed = CUInt(megabytes) * &H100000UI
        If clearRam Then
            ' Preserve the historical VGA/option-ROM aperture while clearing
            ' writable conventional DRAM, exactly as Processor286 did before 8B.
            Array.Clear(LowMemoryInBed, 0, Math.Min(&HA0000, LowMemoryInBed.Length))
            Array.Clear(ExtendedMemoryInBed, 0, ExtendedMemoryInBed.Length)
        End If
    End Sub

    <Runtime.CompilerServices.MethodImpl(Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)>
    Public Function NormalizePhysicalAddress(address As UInteger) As UInteger
        address = address And &HFFFFFFUI

        ' CROMWELL 80286 CORE REFIT BRICK 01 - physical reset ROM alias.
        ' A 286 begins at FFFFF0h, not at the legacy FFFF0h address.  AT-class
        ' motherboard decode therefore exposes the system ROM again at the top
        ' of the 24-bit physical space.  Resolve that board decode before the
        ' compatibility A20 gate so a reset fetch cannot be folded into DRAM.
        Dim romBytesInBed As UInteger = &H100000UI - _romStartInBed
        If romBytesInBed <> 0UI Then
            Dim highRomStartInBed As UInteger = &H1000000UI - romBytesInBed
            If address >= highRomStartInBed Then
                Return _romStartInBed + (address - highRomStartInBed)
            End If
        End If

        If Not _a20EnabledInBed Then address = address And &HEFFFFFUI
        Return address
    End Function

    Public Sub LoadSystemRom(data As Byte())
        If data Is Nothing OrElse data.Length = 0 OrElse data.Length > &H10000 Then
            Throw New ArgumentException("System ROM must be between 1 and 65536 bytes.", NameOf(data))
        End If
        _romStartInBed = CUInt(&H100000 - data.Length)
        Array.Copy(data, 0, LowMemoryInBed, CInt(_romStartInBed), data.Length)
    End Sub

    Public Sub ImportLegacyLowMemory(sourceInBed As Byte(,))
        If sourceInBed Is Nothing Then Throw New ArgumentNullException(NameOf(sourceInBed))
        Dim bytesInBed As Integer = Math.Min(Buffer.ByteLength(sourceInBed), LowMemoryInBed.Length)
        Buffer.BlockCopy(sourceInBed, 0, LowMemoryInBed, 0, bytesInBed)
    End Sub

    Public Function DiagnosticText() As String
        Return "NEAT memory controller     : " & Identity & Environment.NewLine &
               "  installed DRAM           : " & InstalledMemoryMegabytes.ToString() & " MiB" & Environment.NewLine &
               "  system ROM base          : " & _romStartInBed.ToString("X6") & "h" & Environment.NewLine &
               "  A20 physical gate        : " & If(_a20EnabledInBed, "HIGH", "LOW")
    End Function

    ' CROMWELL PCB REFIT PHASE 2 BRICK 8C - local memory-side cycles
    ' These methods are entered only after the motherboard bridge has resolved
    ' programmable MMIO/shadow/ISA decode.  Addresses are already A20-normalized.
    Public Property LegacyMirror As Byte(,)
    ' Legacy mirror support remains available for archaeology/debug fallback,
    ' but the physical motherboard RAM is now authoritative by default.
    Public Property MirrorLegacyMemory As Boolean = False
    Public Property MirrorLegacyTextCells As Boolean

    Friend Function ClassifyLocalTargetNormalized(address As UInteger) As AtMemoryCycleTarget286
        If address < &H100000UI Then
            If address >= _romStartInBed Then Return AtMemoryCycleTarget286.SystemRom
            Return AtMemoryCycleTarget286.LocalDram
        End If
        If address < _installedMemoryBytesInBed Then Return AtMemoryCycleTarget286.LocalDram
        Return AtMemoryCycleTarget286.OpenBus
    End Function

    <Runtime.CompilerServices.MethodImpl(Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)>
    Friend Function ReadLocalByteNormalized(address As UInteger) As Byte
        Select Case ClassifyLocalTargetNormalized(address)
            Case AtMemoryCycleTarget286.LocalDram, AtMemoryCycleTarget286.SystemRom
                If address < &H100000UI Then
                    Return LowMemoryInBed(CInt(address))
                End If
                Return ExtendedMemoryInBed(CInt(address - &H100000UI))
            Case Else
                Return &HFF
        End Select
    End Function

    <Runtime.CompilerServices.MethodImpl(Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)>
    Friend Sub WriteLocalByteNormalized(address As UInteger, value As Byte)
        If ClassifyLocalTargetNormalized(address) <> AtMemoryCycleTarget286.LocalDram Then Return

        If address < &H100000UI Then
            LowMemoryInBed(CInt(address)) = value
            MirrorLegacyWriteInBed(address, value)
        Else
            ExtendedMemoryInBed(CInt(address - &H100000UI)) = value
        End If
    End Sub

    <Runtime.CompilerServices.MethodImpl(Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)>
    Friend Function ReadLocalWordNormalized(firstAddress As UInteger) As UInt16
        Dim secondAddress As UInteger = firstAddress + 1UI
        Return CUShort(CUInt(ReadLocalByteNormalized(firstAddress)) Or
                       (CUInt(ReadLocalByteNormalized(secondAddress)) << 8))
    End Function

    <Runtime.CompilerServices.MethodImpl(Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)>
    Friend Sub WriteLocalWordNormalized(firstAddress As UInteger, value As UInt16)
        WriteLocalByteNormalized(firstAddress, CByte(value And &HFFUS))
        WriteLocalByteNormalized(firstAddress + 1UI, CByte(value >> 8))
    End Sub

    Private Sub MirrorLegacyWriteInBed(address As UInteger, value As Byte)
        If Not MirrorLegacyMemory OrElse LegacyMirror Is Nothing Then Return
        If address >= &H100000UI Then Return

        Dim mirrorAddressInBed As UInteger = address
        If MirrorLegacyTextCells AndAlso
           address >= &HB8000UI AndAlso address < &HBC000UI Then
            mirrorAddressInBed = address Xor 1UI
        End If

        LegacyMirror(CInt(mirrorAddressInBed >> 16),
                     CInt(mirrorAddressInBed And &HFFFFUI)) = value
    End Sub
End Class

Public Enum AtBusMaster286 As Byte
    Cpu = 0
    Dma8 = 1
    Dma16 = 2
    Refresh = 3
End Enum

' CROMWELL PCB REFIT PHASE 2 BRICK 8D - physical bus ownership state.
' HeldNoMaster is the interval after the 80286 has acknowledged HOLD but before
' a requesting motherboard master begins its first transaction.
Public Enum AtBusOwner286 As Byte
    Cpu = 0
    HeldNoMaster = 1
    Dma8 = 2
    Dma16 = 3
    Refresh = 4
End Enum

Public Enum AtMemoryCycleTarget286 As Byte
    LocalDram = 0
    SystemRom = 1
    MappedDevice = 2
    OpenBus = 3
End Enum

Public Enum AtBusCycleKind286 As Byte
    IoRead8 = 0
    IoRead16 = 1
    IoWrite8 = 2
    IoWrite16 = 3
    MappedMemoryRead8 = 4
    MappedMemoryWrite8 = 5
    InterruptAcknowledge = 6
    PhysicalMemoryRead8 = 7
    PhysicalMemoryRead16 = 8
    PhysicalMemoryWrite8 = 9
    PhysicalMemoryWrite16 = 10
End Enum

' CROMWELL PCB REFIT PHASE 2 BRICK 8E - READY timing classes.
' These describe the electrical termination of a completed bus transaction.
Public Enum AtReadyCycleClass286 As Byte
    LocalDram = 0
    SystemRom = 1
    MotherboardIo = 2
    AtBus8 = 3
    AtBus16 = 4
End Enum

' CROMWELL PCB REFIT PHASE 2 BRICK 8F - clock-qualified READY transaction.
' The timing policy needs the bus owner, electrical target, transfer width, and
' decoded address/port; timing is still returned to the CPU ledger as integer
' 80286 state clocks, never as a host-side delay.
Public Structure AtReadyCycle286
    Public Master As AtBusMaster286
    Public Kind As AtBusCycleKind286
    Public ReadyClass As AtReadyCycleClass286
    Public AddressOrPort As UInteger
    Public WidthBytes As Integer

    Public Sub New(masterInBed As AtBusMaster286,
                   kindInBed As AtBusCycleKind286,
                   readyClassInBed As AtReadyCycleClass286,
                   addressOrPortInBed As UInteger,
                   widthBytesInBed As Integer)
        Master = masterInBed
        Kind = kindInBed
        ReadyClass = readyClassInBed
        AddressOrPort = addressOrPortInBed
        WidthBytes = widthBytesInBed
    End Sub
End Structure

Public NotInheritable Class NeatMotherboardBridge286
    Private ReadOnly _fabricInBed As HardwareBus
    Private ReadOnly _memoryInBed As NeatMemoryController286

    ' CROMWELL PCB REFIT PHASE 2 BRICK 8D - HOLD/HLDA + motherboard arbitration.
    ' Every external memory master must own the local bus before it can issue a
    ' transaction.  The 80286 HOLD/HLDA handshake is logical in this brick;
    ' pin-phase latency and READY/wait-state duration are attached in Brick 8E.
    Private _cpuHoldLineWriterInBed As Action(Of Boolean)
    Private _cpuHldaReaderInBed As Func(Of Boolean)
    Private _cpuResetPulseSinkInBed As Action
    Private _shutdownResetLatchedInBed As Boolean
    Private _shutdownResetCountInBed As ULong
    Private _lastShutdownStatusInBed As Byte
    Private _lastShutdownWarmOffsetInBed As UInt16
    Private _lastShutdownWarmSegmentInBed As UInt16
    Private _lastShutdownWarmBootFlagInBed As UInt16
    Private _lastShutdownFrameWordsInBed As String = "<not captured>"
    Private _holdLineAssertedInBed As Boolean
    Private _ownerInBed As AtBusOwner286 = AtBusOwner286.Cpu
    Private _ownerDepthInBed As Integer
    Private ReadOnly _externalRequestInBed(3) As Boolean
    Private ReadOnly _externalGrantCountInBed(3) As ULong
    Private ReadOnly _externalRequestCountInBed(3) As ULong
    Private _holdAssertionCountInBed As ULong
    Private _holdReleaseCountInBed As ULong
    Private _hldaObservationCountInBed As ULong
    Private _ownerTransitionCountInBed As ULong
    Private _arbitrationFaultCountInBed As ULong
    Private _cpuCyclesBlockedInBed As ULong
    Private _pendingRefreshCyclesInBed As ULong
    Private _pendingRefreshActionInBed As Action

    ' CROMWELL PCB REFIT PHASE 2 BRICK 8E - READY/wait-state completion.
    Private _readyWaitSinkInBed As Action(Of Integer, Boolean)
    Private _readyPolicyInBed As Func(Of AtReadyCycle286, Integer)
    Private _readyPolicyDescriptionInBed As Func(Of String)
    Private ReadOnly _readyCycleCountInBed(3) As ULong
    Private ReadOnly _readyWaitCycleCountInBed(3) As ULong
    Private ReadOnly _readyWaitTStatesInBed(3) As ULong
    Private ReadOnly _readyMaxWaitTStatesInBed(3) As Integer

    Private _ioRead8InBed As ULong
    Private _ioRead16InBed As ULong
    Private _ioWrite8InBed As ULong
    Private _ioWrite16InBed As ULong

    Private ReadOnly _memoryRead8InBed(3) As ULong
    Private ReadOnly _memoryRead16InBed(3) As ULong
    Private ReadOnly _memoryWrite8InBed(3) As ULong
    Private ReadOnly _memoryWrite16InBed(3) As ULong

    Private _mappedRead8InBed As ULong
    Private _mappedWrite8InBed As ULong
    Private _pageGateQueriesInBed As ULong
    Private _pageGatePositiveInBed As ULong

    Public Sub New(fabricInBed As HardwareBus, memoryInBed As NeatMemoryController286)
        If fabricInBed Is Nothing Then Throw New ArgumentNullException(NameOf(fabricInBed))
        If memoryInBed Is Nothing Then Throw New ArgumentNullException(NameOf(memoryInBed))
        _fabricInBed = fabricInBed
        _memoryInBed = memoryInBed
    End Sub

    Public ReadOnly Property Identity As String
        Get
            Return "C&T NEAT CPU/DMA/refresh motherboard arbiter"
        End Get
    End Property

    Public ReadOnly Property Fabric As HardwareBus
        Get
            Return _fabricInBed
        End Get
    End Property

    Public ReadOnly Property MemoryController As NeatMemoryController286
        Get
            Return _memoryInBed
        End Get
    End Property

    Public ReadOnly Property CurrentOwner As AtBusOwner286
        Get
            Return _ownerInBed
        End Get
    End Property

    Public ReadOnly Property HoldAsserted As Boolean
        Get
            Return _holdLineAssertedInBed
        End Get
    End Property

    Public ReadOnly Property HoldAcknowledgeAsserted As Boolean
        Get
            Return CpuHldaAssertedInBed()
        End Get
    End Property

    Public Sub AttachCpuHoldInterface(holdLineWriterInBed As Action(Of Boolean),
                                      hldaReaderInBed As Func(Of Boolean))
        If holdLineWriterInBed Is Nothing Then Throw New ArgumentNullException(NameOf(holdLineWriterInBed))
        If hldaReaderInBed Is Nothing Then Throw New ArgumentNullException(NameOf(hldaReaderInBed))
        _cpuHoldLineWriterInBed = holdLineWriterInBed
        _cpuHldaReaderInBed = hldaReaderInBed
        _cpuHoldLineWriterInBed.Invoke(_holdLineAssertedInBed)
    End Sub

    ' The 80286 has no architectural instruction for clearing PE.  When an
    ' exception cannot be dispatched (the classic zero-length-IDT sequence),
    ' it asserts its SHUTDOWN state.  The NEAT/AT motherboard detects that
    ' state and pulses CPU RESET#, while DRAM, CMOS, ISA devices and the board
    ' clock remain powered.  Firmware then interprets CMOS 0Fh and 0040:0067.
    Public Sub AttachCpuResetPulseInterface(resetPulseSinkInBed As Action)
        If resetPulseSinkInBed Is Nothing Then Throw New ArgumentNullException(NameOf(resetPulseSinkInBed))
        _cpuResetPulseSinkInBed = resetPulseSinkInBed
    End Sub

    Public Sub ObserveProcessorRunning()
        _shutdownResetLatchedInBed = False
    End Sub

    Public Sub ObserveProcessorShutdown(shutdownStatusInBed As Byte,
                                        warmOffsetInBed As UInt16,
                                        warmSegmentInBed As UInt16,
                                        warmBootFlagInBed As UInt16,
                                        frameWordsInBed As String)
        If _shutdownResetLatchedInBed Then Return

        _shutdownResetLatchedInBed = True
        _shutdownResetCountInBed += 1UL
        _lastShutdownStatusInBed = shutdownStatusInBed
        _lastShutdownWarmOffsetInBed = warmOffsetInBed
        _lastShutdownWarmSegmentInBed = warmSegmentInBed
        _lastShutdownWarmBootFlagInBed = warmBootFlagInBed
        _lastShutdownFrameWordsInBed = If(frameWordsInBed, "<not captured>")
        If _cpuResetPulseSinkInBed IsNot Nothing Then _cpuResetPulseSinkInBed.Invoke()
    End Sub

    Public Sub AttachReadyInterface(waitSinkInBed As Action(Of Integer, Boolean),
                                    readyPolicyInBed As Func(Of AtReadyCycle286, Integer),
                                    Optional readyPolicyDescriptionInBed As Func(Of String) = Nothing)
        If waitSinkInBed Is Nothing Then Throw New ArgumentNullException(NameOf(waitSinkInBed))
        If readyPolicyInBed Is Nothing Then Throw New ArgumentNullException(NameOf(readyPolicyInBed))
        _readyWaitSinkInBed = waitSinkInBed
        _readyPolicyInBed = readyPolicyInBed
        _readyPolicyDescriptionInBed = readyPolicyDescriptionInBed
    End Sub
    Private Sub ApplyReadyWaitStatesInBed(masterInBed As AtBusMaster286,
                                          kindInBed As AtBusCycleKind286,
                                          readyClassInBed As AtReadyCycleClass286,
                                          addressOrPortInBed As UInteger,
                                          widthBytesInBed As Integer)
        Dim indexInBed As Integer = MasterIndexInBed(masterInBed)
        _readyCycleCountInBed(indexInBed) += 1UL
        If _readyPolicyInBed Is Nothing Then Return

        Dim cycleInBed As New AtReadyCycle286(masterInBed,
                                              kindInBed,
                                              readyClassInBed,
                                              addressOrPortInBed,
                                              widthBytesInBed)
        Dim waitTStatesInBed As Integer = _readyPolicyInBed.Invoke(cycleInBed)
        If waitTStatesInBed < 0 Then
            _arbitrationFaultCountInBed += 1UL
            Throw New InvalidOperationException("NEAT READY policy returned a negative wait-state duration.")
        End If
        If waitTStatesInBed = 0 Then Return

        _readyWaitCycleCountInBed(indexInBed) += 1UL
        _readyWaitTStatesInBed(indexInBed) += CULng(waitTStatesInBed)
        If waitTStatesInBed > _readyMaxWaitTStatesInBed(indexInBed) Then
            _readyMaxWaitTStatesInBed(indexInBed) = waitTStatesInBed
        End If

        ' The sink adds physical stall T-states to the bounded CPU time ledger.
        ' For DMA masters those T-states represent time during which the CPU owns
        ' no bus; only CPU-originated READY stretching lights the B annunciator.
        If _readyWaitSinkInBed IsNot Nothing Then
            _readyWaitSinkInBed.Invoke(waitTStatesInBed, masterInBed = AtBusMaster286.Cpu)
        End If
    End Sub
    Private Shared Function ReadyClassForMemoryTargetInBed(targetInBed As AtMemoryCycleTarget286,
                                                           widthInBed As Integer) As AtReadyCycleClass286
        Select Case targetInBed
            Case AtMemoryCycleTarget286.LocalDram
                Return AtReadyCycleClass286.LocalDram
            Case AtMemoryCycleTarget286.SystemRom
                Return AtReadyCycleClass286.SystemRom
            Case AtMemoryCycleTarget286.MappedDevice, AtMemoryCycleTarget286.OpenBus
                Return If(widthInBed >= 2, AtReadyCycleClass286.AtBus16, AtReadyCycleClass286.AtBus8)
            Case Else
                Throw New ArgumentOutOfRangeException(NameOf(targetInBed))
        End Select
    End Function
    Private Function ReadyClassForIoInBed(portInBed As UInt16,
                                          widthInBed As Integer) As AtReadyCycleClass286
        If Not _fabricInBed.PortUsesAtBusTimingInBed(portInBed) Then
            Return AtReadyCycleClass286.MotherboardIo
        End If
        Return If(widthInBed >= 2, AtReadyCycleClass286.AtBus16, AtReadyCycleClass286.AtBus8)
    End Function

    ' Used after a processor-only RESET.  Motherboard HOLD state survives that
    ' reset, so the newly-reset CPU must immediately see the actual board line.
    Public Sub ResynchronizeCpuHoldInterface()
        If _cpuHoldLineWriterInBed IsNot Nothing Then
            _cpuHoldLineWriterInBed.Invoke(_holdLineAssertedInBed)
        End If
    End Sub

    ' Full motherboard reset clears arbitration state but intentionally leaves
    ' lifetime diagnostic counters intact, matching the other bridge counters.
    Public Sub ResetArbitration()
        For indexInBed As Integer = 1 To 3
            _externalRequestInBed(indexInBed) = False
        Next
        _pendingRefreshCyclesInBed = 0UL
        _pendingRefreshActionInBed = Nothing
        _ownerDepthInBed = 0
        If _holdLineAssertedInBed Then
            _holdLineAssertedInBed = False
            _holdReleaseCountInBed += 1UL
            If _cpuHoldLineWriterInBed IsNot Nothing Then _cpuHoldLineWriterInBed.Invoke(False)
        ElseIf _cpuHoldLineWriterInBed IsNot Nothing Then
            _cpuHoldLineWriterInBed.Invoke(False)
        End If
        SetOwnerInBed(AtBusOwner286.Cpu)
    End Sub

    Public Sub SetDmaHoldRequest(masterInBed As AtBusMaster286, assertedInBed As Boolean)
        If masterInBed <> AtBusMaster286.Dma8 AndAlso masterInBed <> AtBusMaster286.Dma16 Then
            Throw New ArgumentOutOfRangeException(NameOf(masterInBed), "Only DMA8/DMA16 may drive the DMA HRQ inputs.")
        End If

        Dim indexInBed As Integer = MasterIndexInBed(masterInBed)
        If _externalRequestInBed(indexInBed) = assertedInBed Then Return
        _externalRequestInBed(indexInBed) = assertedInBed
        If assertedInBed Then _externalRequestCountInBed(indexInBed) += 1UL
        ReconcileHoldLineInBed()
    End Sub

    ' Timer-1 REFREQ is a motherboard request, not an 8237 channel.  The NEAT
    ' controller therefore arbitrates a real refresh bus ownership interval and
    ' only then lets the 82C212 advance its RAS refresh address/cycle state.
    Public Sub PerformRefreshCycle(refreshCycleInBed As Action)
        If refreshCycleInBed Is Nothing Then Throw New ArgumentNullException(NameOf(refreshCycleInBed))
        Dim indexInBed As Integer = MasterIndexInBed(AtBusMaster286.Refresh)
        If _pendingRefreshCyclesInBed = ULong.MaxValue Then
            _arbitrationFaultCountInBed += 1UL
            Throw New InvalidOperationException("NEAT refresh request latch overflowed.")
        End If
        _pendingRefreshCyclesInBed += 1UL
        _pendingRefreshActionInBed = refreshCycleInBed
        _externalRequestCountInBed(indexInBed) += 1UL
        If Not _externalRequestInBed(indexInBed) Then
            _externalRequestInBed(indexInBed) = True
            ReconcileHoldLineInBed()
        End If

        ' REFREQ is latched.  If LOCK prevents HLDA, the request remains asserted
        ' until the next CPU bus-cycle boundary; a real NEAT board does not let
        ' refresh steal a locked cycle and does not treat the wait as a fault.
        ServicePendingRefreshCyclesInBed()
    End Sub

    Private Sub ServicePendingRefreshCyclesInBed()
        If _pendingRefreshCyclesInBed = 0UL OrElse
           _pendingRefreshActionInBed Is Nothing OrElse
           Not CpuHldaAssertedInBed() Then Return

        Dim indexInBed As Integer = MasterIndexInBed(AtBusMaster286.Refresh)
        BeginExternalCycleInBed(AtBusMaster286.Refresh)
        Try
            While _pendingRefreshCyclesInBed > 0UL
                _pendingRefreshCyclesInBed -= 1UL
                _pendingRefreshActionInBed.Invoke()
            End While
        Finally
            EndExternalCycleInBed(AtBusMaster286.Refresh)
            If _pendingRefreshCyclesInBed = 0UL Then
                _pendingRefreshActionInBed = Nothing
                _externalRequestInBed(indexInBed) = False
                ReconcileHoldLineInBed()
            End If
        End Try
    End Sub

    Private Shared Function MasterIndexInBed(masterInBed As AtBusMaster286) As Integer
        Dim indexInBed As Integer = CInt(masterInBed)
        If indexInBed < 0 OrElse indexInBed > 3 Then
            Throw New ArgumentOutOfRangeException(NameOf(masterInBed))
        End If
        Return indexInBed
    End Function

    Private Shared Function OwnerForMasterInBed(masterInBed As AtBusMaster286) As AtBusOwner286
        Select Case masterInBed
            Case AtBusMaster286.Cpu
                Return AtBusOwner286.Cpu
            Case AtBusMaster286.Dma8
                Return AtBusOwner286.Dma8
            Case AtBusMaster286.Dma16
                Return AtBusOwner286.Dma16
            Case AtBusMaster286.Refresh
                Return AtBusOwner286.Refresh
            Case Else
                Throw New ArgumentOutOfRangeException(NameOf(masterInBed))
        End Select
    End Function

    Private Function AnyExternalRequestInBed() As Boolean
        Return _externalRequestInBed(1) OrElse
               _externalRequestInBed(2) OrElse
               _externalRequestInBed(3)
    End Function

    Private Function CpuHldaAssertedInBed() As Boolean
        Return _cpuHldaReaderInBed IsNot Nothing AndAlso _cpuHldaReaderInBed.Invoke()
    End Function

    Private Sub SetOwnerInBed(ownerInBed As AtBusOwner286)
        If _ownerInBed = ownerInBed Then Return
        _ownerInBed = ownerInBed
        _ownerTransitionCountInBed += 1UL
    End Sub

    Private Sub SetHoldLineInBed(assertedInBed As Boolean)
        If _holdLineAssertedInBed = assertedInBed Then Return
        _holdLineAssertedInBed = assertedInBed
        If assertedInBed Then
            _holdAssertionCountInBed += 1UL
        Else
            _holdReleaseCountInBed += 1UL
        End If

        If _cpuHoldLineWriterInBed IsNot Nothing Then
            _cpuHoldLineWriterInBed.Invoke(assertedInBed)
        End If

        If assertedInBed Then
            If CpuHldaAssertedInBed() Then
                _hldaObservationCountInBed += 1UL
                If _ownerInBed = AtBusOwner286.Cpu AndAlso _ownerDepthInBed = 0 Then
                    SetOwnerInBed(AtBusOwner286.HeldNoMaster)
                End If
            End If
        ElseIf _ownerDepthInBed = 0 Then
            SetOwnerInBed(AtBusOwner286.Cpu)
        End If
    End Sub

    Private Sub ReconcileHoldLineInBed()
        If AnyExternalRequestInBed() Then
            If Not _holdLineAssertedInBed Then SetHoldLineInBed(True)
            If CpuHldaAssertedInBed() AndAlso
               _ownerInBed = AtBusOwner286.Cpu AndAlso
               _ownerDepthInBed = 0 Then
                SetOwnerInBed(AtBusOwner286.HeldNoMaster)
            End If
            Return
        End If

        If _ownerDepthInBed = 0 Then SetHoldLineInBed(False)
    End Sub

    Private Sub EnsureCpuReleasedInBed()
        If Not _holdLineAssertedInBed Then SetHoldLineInBed(True)
        If Not CpuHldaAssertedInBed() Then
            _arbitrationFaultCountInBed += 1UL
            Throw New InvalidOperationException("External motherboard master attempted a bus cycle before the 80286 asserted HLDA.")
        End If
        If _ownerInBed = AtBusOwner286.Cpu AndAlso _ownerDepthInBed = 0 Then
            SetOwnerInBed(AtBusOwner286.HeldNoMaster)
        End If
    End Sub

    Private Sub AssertCpuMayUseBusInBed()
        ' A refresh request may have arrived while LOCK suppressed HLDA.  Once
        ' the CPU releases LOCK, grant the latched refresh before its next bus
        ' transaction, exactly at the newly available cycle boundary.
        ServicePendingRefreshCyclesInBed()
        If _ownerInBed = AtBusOwner286.Cpu Then Return
        _cpuCyclesBlockedInBed += 1UL
        _arbitrationFaultCountInBed += 1UL
        Throw New InvalidOperationException("80286 attempted a motherboard bus cycle while HLDA had granted the bus to another master.")
    End Sub

    Private Sub BeginExternalCycleInBed(masterInBed As AtBusMaster286)
        If masterInBed = AtBusMaster286.Cpu Then Throw New ArgumentOutOfRangeException(NameOf(masterInBed))
        Dim indexInBed As Integer = MasterIndexInBed(masterInBed)
        If Not _externalRequestInBed(indexInBed) Then
            _arbitrationFaultCountInBed += 1UL
            Throw New InvalidOperationException(masterInBed.ToString() & " attempted a bus cycle without an active motherboard request/HRQ.")
        End If

        EnsureCpuReleasedInBed()
        Dim desiredOwnerInBed As AtBusOwner286 = OwnerForMasterInBed(masterInBed)
        If _ownerInBed = AtBusOwner286.HeldNoMaster Then
            SetOwnerInBed(desiredOwnerInBed)
        ElseIf _ownerInBed <> desiredOwnerInBed Then
            _arbitrationFaultCountInBed += 1UL
            Throw New InvalidOperationException("Motherboard bus-master collision: " &
                                                _ownerInBed.ToString() & " already owns the bus while " &
                                                masterInBed.ToString() & " requested a cycle.")
        End If

        _ownerDepthInBed += 1
        _externalGrantCountInBed(indexInBed) += 1UL
    End Sub

    Private Sub EndExternalCycleInBed(masterInBed As AtBusMaster286)
        Dim desiredOwnerInBed As AtBusOwner286 = OwnerForMasterInBed(masterInBed)
        If _ownerInBed <> desiredOwnerInBed OrElse _ownerDepthInBed <= 0 Then
            _arbitrationFaultCountInBed += 1UL
            Throw New InvalidOperationException("Motherboard bus ownership stack became inconsistent.")
        End If

        _ownerDepthInBed -= 1
        If _ownerDepthInBed <> 0 Then Return

        Dim indexInBed As Integer = MasterIndexInBed(masterInBed)
        If Not _externalRequestInBed(indexInBed) Then
            If AnyExternalRequestInBed() Then
                SetOwnerInBed(AtBusOwner286.HeldNoMaster)
            Else
                SetHoldLineInBed(False)
            End If
        End If
    End Sub

    <Runtime.CompilerServices.MethodImpl(Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)>
    Public Function ReadIoByte(port As UInt16) As Byte
        AssertCpuMayUseBusInBed()
        ApplyReadyWaitStatesInBed(AtBusMaster286.Cpu,
                                  AtBusCycleKind286.IoRead8,
                                  ReadyClassForIoInBed(port, 1),
                                  CUInt(port),
                                  1)
        _ioRead8InBed += 1UL
        Return _fabricInBed.ReadByte(port)
    End Function

    <Runtime.CompilerServices.MethodImpl(Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)>
    Public Function ReadIoWord(port As UInt16) As UInt16
        AssertCpuMayUseBusInBed()
        ApplyReadyWaitStatesInBed(AtBusMaster286.Cpu,
                                  AtBusCycleKind286.IoRead16,
                                  ReadyClassForIoInBed(port, 2),
                                  CUInt(port),
                                  2)
        _ioRead16InBed += 1UL
        Return _fabricInBed.ReadWord(port)
    End Function

    <Runtime.CompilerServices.MethodImpl(Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)>
    Public Sub WriteIoByte(port As UInt16, value As Byte)
        ' READY completes the CPU transaction before device-side semantics are
        ' allowed to raise a new DREQ/HOLD request from inside the I/O handler.
        AssertCpuMayUseBusInBed()
        ApplyReadyWaitStatesInBed(AtBusMaster286.Cpu,
                                  AtBusCycleKind286.IoWrite8,
                                  ReadyClassForIoInBed(port, 1),
                                  CUInt(port),
                                  1)
        _ioWrite8InBed += 1UL
        _fabricInBed.WriteByte(port, value)
    End Sub

    <Runtime.CompilerServices.MethodImpl(Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)>
    Public Sub WriteIoWord(port As UInt16, value As UInt16)
        AssertCpuMayUseBusInBed()
        ApplyReadyWaitStatesInBed(AtBusMaster286.Cpu,
                                  AtBusCycleKind286.IoWrite16,
                                  ReadyClassForIoInBed(port, 2),
                                  CUInt(port),
                                  2)
        _ioWrite16InBed += 1UL
        _fabricInBed.WriteWord(port, value)
    End Sub

    <Runtime.CompilerServices.MethodImpl(Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)>
    Public Function MemoryPageMayRouteToDeviceInBed(address As UInteger) As Boolean
        Dim normalizedInBed As UInteger = _memoryInBed.NormalizePhysicalAddress(address)
        _pageGateQueriesInBed += 1UL
        Dim selectedInBed As Boolean = _fabricInBed.MemoryPageMayRouteToDeviceInBed(normalizedInBed)
        If selectedInBed Then _pageGatePositiveInBed += 1UL
        Return selectedInBed
    End Function

    Public Function TryReadMappedMemoryByte(address As UInteger, ByRef value As Byte) As Boolean
        AssertCpuMayUseBusInBed()
        Dim normalizedInBed As UInteger = _memoryInBed.NormalizePhysicalAddress(address)
        _mappedRead8InBed += 1UL
        Return _fabricInBed.TryReadMemoryByte(normalizedInBed, value)
    End Function

    Public Function TryWriteMappedMemoryByte(address As UInteger, value As Byte) As Boolean
        AssertCpuMayUseBusInBed()
        Dim normalizedInBed As UInteger = _memoryInBed.NormalizePhysicalAddress(address)
        _mappedWrite8InBed += 1UL
        Return _fabricInBed.TryWriteMemoryByte(normalizedInBed, value)
    End Function

    Public Function ReadMemoryByte(address As UInteger,
                                   masterInBed As AtBusMaster286) As Byte
        Dim targetInBed As AtMemoryCycleTarget286
        Return ReadMemoryByte(address, masterInBed, targetInBed)
    End Function

    <Runtime.CompilerServices.MethodImpl(Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)>
    Public Function ReadMemoryByte(address As UInteger,
                                   masterInBed As AtBusMaster286,
                                   ByRef targetInBed As AtMemoryCycleTarget286) As Byte
        Dim externalInBed As Boolean = masterInBed <> AtBusMaster286.Cpu
        If externalInBed Then
            BeginExternalCycleInBed(masterInBed)
        Else
            AssertCpuMayUseBusInBed()
        End If

        Try
            _memoryRead8InBed(MasterIndexInBed(masterInBed)) += 1UL
            Dim normalizedInBed As UInteger = _memoryInBed.NormalizePhysicalAddress(address)

            _pageGateQueriesInBed += 1UL
            Dim pageSelectedInBed As Boolean =
                _fabricInBed.MemoryPageMayRouteToDeviceInBed(normalizedInBed)
            If pageSelectedInBed Then
                _pageGatePositiveInBed += 1UL
                Dim mappedValueInBed As Byte
                Dim mappedTimingTargetInBed As AtMemoryCycleTarget286
                _mappedRead8InBed += 1UL
                If _fabricInBed.TryReadMemoryByte(normalizedInBed,
                                                  mappedValueInBed,
                                                  mappedTimingTargetInBed) Then
                    targetInBed = mappedTimingTargetInBed
                    ApplyReadyWaitStatesInBed(masterInBed,
                                              AtBusCycleKind286.MappedMemoryRead8,
                                              ReadyClassForMemoryTargetInBed(targetInBed, 1),
                                              normalizedInBed,
                                              1)
                    Return mappedValueInBed
                End If
            End If

            targetInBed = _memoryInBed.ClassifyLocalTargetNormalized(normalizedInBed)
            ApplyReadyWaitStatesInBed(masterInBed,
                                      AtBusCycleKind286.PhysicalMemoryRead8,
                                      ReadyClassForMemoryTargetInBed(targetInBed, 1),
                                      normalizedInBed,
                                      1)
            Return _memoryInBed.ReadLocalByteNormalized(normalizedInBed)
        Finally
            If externalInBed Then EndExternalCycleInBed(masterInBed)
        End Try
    End Function

    Public Sub WriteMemoryByte(address As UInteger,
                               value As Byte,
                               masterInBed As AtBusMaster286)
        Dim targetInBed As AtMemoryCycleTarget286
        WriteMemoryByte(address, value, masterInBed, targetInBed)
    End Sub

    <Runtime.CompilerServices.MethodImpl(Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)>
    Public Sub WriteMemoryByte(address As UInteger,
                               value As Byte,
                               masterInBed As AtBusMaster286,
                               ByRef targetInBed As AtMemoryCycleTarget286)
        Dim externalInBed As Boolean = masterInBed <> AtBusMaster286.Cpu
        If externalInBed Then
            BeginExternalCycleInBed(masterInBed)
        Else
            AssertCpuMayUseBusInBed()
        End If

        Try
            _memoryWrite8InBed(MasterIndexInBed(masterInBed)) += 1UL
            Dim normalizedInBed As UInteger = _memoryInBed.NormalizePhysicalAddress(address)

            _pageGateQueriesInBed += 1UL
            Dim pageSelectedInBed As Boolean =
                _fabricInBed.MemoryPageMayRouteToDeviceInBed(normalizedInBed)
            If pageSelectedInBed Then
                _pageGatePositiveInBed += 1UL
                Dim mappedTimingTargetInBed As AtMemoryCycleTarget286
                _mappedWrite8InBed += 1UL
                If _fabricInBed.TryWriteMemoryByte(normalizedInBed,
                                                   value,
                                                   mappedTimingTargetInBed) Then
                    targetInBed = mappedTimingTargetInBed
                    ApplyReadyWaitStatesInBed(masterInBed,
                                              AtBusCycleKind286.MappedMemoryWrite8,
                                              ReadyClassForMemoryTargetInBed(targetInBed, 1),
                                              normalizedInBed,
                                              1)
                    Return
                End If
            End If

            targetInBed = _memoryInBed.ClassifyLocalTargetNormalized(normalizedInBed)
            ApplyReadyWaitStatesInBed(masterInBed,
                                      AtBusCycleKind286.PhysicalMemoryWrite8,
                                      ReadyClassForMemoryTargetInBed(targetInBed, 1),
                                      normalizedInBed,
                                      1)
            _memoryInBed.WriteLocalByteNormalized(normalizedInBed, value)
        Finally
            If externalInBed Then EndExternalCycleInBed(masterInBed)
        End Try
    End Sub

    Public Function ReadMemoryWord(address As UInteger,
                                   masterInBed As AtBusMaster286) As UInt16
        Dim firstTargetInBed As AtMemoryCycleTarget286
        Dim secondTargetInBed As AtMemoryCycleTarget286
        Dim directWordInBed As Boolean
        Return ReadMemoryWord(address, masterInBed,
                              firstTargetInBed, secondTargetInBed, directWordInBed)
    End Function

    Public Function ReadMemoryWord(address As UInteger,
                                   masterInBed As AtBusMaster286,
                                   ByRef firstTargetInBed As AtMemoryCycleTarget286,
                                   ByRef secondTargetInBed As AtMemoryCycleTarget286,
                                   ByRef directWordInBed As Boolean) As UInt16
        If masterInBed = AtBusMaster286.Cpu Then AssertCpuMayUseBusInBed()

        Dim firstInBed As UInteger = _memoryInBed.NormalizePhysicalAddress(address)
        Dim secondInBed As UInteger = _memoryInBed.NormalizePhysicalAddress(address + 1UI)

        If (firstInBed And 1UI) = 0UI AndAlso
           secondInBed = firstInBed + 1UI Then

            Dim firstMappedCandidateInBed As Boolean =
                _fabricInBed.MemoryPageMayRouteToDeviceInBed(firstInBed)
            _pageGateQueriesInBed += 1UL
            If firstMappedCandidateInBed Then _pageGatePositiveInBed += 1UL

            Dim secondMappedCandidateInBed As Boolean = firstMappedCandidateInBed
            If (firstInBed >> 12) <> (secondInBed >> 12) Then
                secondMappedCandidateInBed =
                    _fabricInBed.MemoryPageMayRouteToDeviceInBed(secondInBed)
                _pageGateQueriesInBed += 1UL
                If secondMappedCandidateInBed Then _pageGatePositiveInBed += 1UL
            End If

            If Not firstMappedCandidateInBed AndAlso Not secondMappedCandidateInBed Then
                firstTargetInBed = _memoryInBed.ClassifyLocalTargetNormalized(firstInBed)
                secondTargetInBed = _memoryInBed.ClassifyLocalTargetNormalized(secondInBed)
                If firstTargetInBed = secondTargetInBed AndAlso
                   firstTargetInBed <> AtMemoryCycleTarget286.OpenBus Then
                    Dim externalInBed As Boolean = masterInBed <> AtBusMaster286.Cpu
                    If externalInBed Then BeginExternalCycleInBed(masterInBed)
                    Try
                        _memoryRead16InBed(MasterIndexInBed(masterInBed)) += 1UL
                        directWordInBed = True
                        ApplyReadyWaitStatesInBed(masterInBed,
                                                  AtBusCycleKind286.PhysicalMemoryRead16,
                                                  ReadyClassForMemoryTargetInBed(firstTargetInBed, 2),
                                                  firstInBed,
                                                  2)
                        Return _memoryInBed.ReadLocalWordNormalized(firstInBed)
                    Finally
                        If externalInBed Then EndExternalCycleInBed(masterInBed)
                    End Try
                End If
            End If
        End If

        ' Split word cycles are two physical bus transactions.  Do not hold an
        ' artificial outer ownership lock across them: single-mode 8237 service
        ' is allowed to return the local bus to the CPU between units.
        directWordInBed = False
        Dim lowInBed As Byte = ReadMemoryByte(address, masterInBed, firstTargetInBed)
        Dim highInBed As Byte = ReadMemoryByte(address + 1UI, masterInBed, secondTargetInBed)
        Return CUShort(CUInt(lowInBed) Or (CUInt(highInBed) << 8))
    End Function

    Public Sub WriteMemoryWord(address As UInteger,
                               value As UInt16,
                               masterInBed As AtBusMaster286)
        Dim firstTargetInBed As AtMemoryCycleTarget286
        Dim secondTargetInBed As AtMemoryCycleTarget286
        Dim directWordInBed As Boolean
        WriteMemoryWord(address, value, masterInBed,
                        firstTargetInBed, secondTargetInBed, directWordInBed)
    End Sub

    Public Sub WriteMemoryWord(address As UInteger,
                               value As UInt16,
                               masterInBed As AtBusMaster286,
                               ByRef firstTargetInBed As AtMemoryCycleTarget286,
                               ByRef secondTargetInBed As AtMemoryCycleTarget286,
                               ByRef directWordInBed As Boolean)
        If masterInBed = AtBusMaster286.Cpu Then AssertCpuMayUseBusInBed()

        Dim firstInBed As UInteger = _memoryInBed.NormalizePhysicalAddress(address)
        Dim secondInBed As UInteger = _memoryInBed.NormalizePhysicalAddress(address + 1UI)

        If (firstInBed And 1UI) = 0UI AndAlso
           secondInBed = firstInBed + 1UI Then

            Dim firstMappedCandidateInBed As Boolean =
                _fabricInBed.MemoryPageMayRouteToDeviceInBed(firstInBed)
            _pageGateQueriesInBed += 1UL
            If firstMappedCandidateInBed Then _pageGatePositiveInBed += 1UL

            Dim secondMappedCandidateInBed As Boolean = firstMappedCandidateInBed
            If (firstInBed >> 12) <> (secondInBed >> 12) Then
                secondMappedCandidateInBed =
                    _fabricInBed.MemoryPageMayRouteToDeviceInBed(secondInBed)
                _pageGateQueriesInBed += 1UL
                If secondMappedCandidateInBed Then _pageGatePositiveInBed += 1UL
            End If

            If Not firstMappedCandidateInBed AndAlso Not secondMappedCandidateInBed Then
                firstTargetInBed = _memoryInBed.ClassifyLocalTargetNormalized(firstInBed)
                secondTargetInBed = _memoryInBed.ClassifyLocalTargetNormalized(secondInBed)
                If firstTargetInBed = secondTargetInBed Then
                    Dim externalInBed As Boolean = masterInBed <> AtBusMaster286.Cpu
                    If externalInBed Then BeginExternalCycleInBed(masterInBed)
                    Try
                        _memoryWrite16InBed(MasterIndexInBed(masterInBed)) += 1UL
                        directWordInBed = True
                        ApplyReadyWaitStatesInBed(masterInBed,
                                                  AtBusCycleKind286.PhysicalMemoryWrite16,
                                                  ReadyClassForMemoryTargetInBed(firstTargetInBed, 2),
                                                  firstInBed,
                                                  2)
                        _memoryInBed.WriteLocalWordNormalized(firstInBed, value)
                        Return
                    Finally
                        If externalInBed Then EndExternalCycleInBed(masterInBed)
                    End Try
                End If
            End If
        End If

        directWordInBed = False
        WriteMemoryByte(address, CByte(value And &HFFUS),
                        masterInBed, firstTargetInBed)
        WriteMemoryByte(address + 1UI, CByte(value >> 8),
                        masterInBed, secondTargetInBed)
    End Sub

    Public Function DiagnosticText() As String
        Dim hldaInBed As Boolean = CpuHldaAssertedInBed()
        Return "NEAT motherboard bridge    : " & Identity & Environment.NewLine &
               "  arbiter owner             : " & _ownerInBed.ToString() & Environment.NewLine &
               "  HOLD / HLDA               : " & If(_holdLineAssertedInBed, "1", "0") & " / " & If(hldaInBed, "1", "0") & Environment.NewLine &
               "  HRQ DMA8/DMA16 / REFREQ   : " & If(_externalRequestInBed(1), "1", "0") & " / " & If(_externalRequestInBed(2), "1", "0") & " / " & If(_externalRequestInBed(3), "1", "0") & Environment.NewLine &
               "  HOLD assert/release       : " & _holdAssertionCountInBed.ToString("N0") & " / " & _holdReleaseCountInBed.ToString("N0") & Environment.NewLine &
               "  HLDA observations         : " & _hldaObservationCountInBed.ToString("N0") & Environment.NewLine &
               "  80286 shutdown resets    : " & _shutdownResetCountInBed.ToString("N0") & Environment.NewLine &
               "  last shutdown CMOS/vector : " & _lastShutdownStatusInBed.ToString("X2") & " / " &
               _lastShutdownWarmSegmentInBed.ToString("X4") & ":" & _lastShutdownWarmOffsetInBed.ToString("X4") &
               "  BDA 472=" & _lastShutdownWarmBootFlagInBed.ToString("X4") & Environment.NewLine &
               "  last shutdown stack words : " & _lastShutdownFrameWordsInBed & Environment.NewLine &
               "  owner transitions         : " & _ownerTransitionCountInBed.ToString("N0") & Environment.NewLine &
               "  grants DMA8/DMA16/REFRESH : " & _externalGrantCountInBed(1).ToString("N0") & " / " & _externalGrantCountInBed(2).ToString("N0") & " / " & _externalGrantCountInBed(3).ToString("N0") & Environment.NewLine &
               "  requests DMA8/DMA16/REF   : " & _externalRequestCountInBed(1).ToString("N0") & " / " & _externalRequestCountInBed(2).ToString("N0") & " / " & _externalRequestCountInBed(3).ToString("N0") & Environment.NewLine &
               "  arbitration faults        : " & _arbitrationFaultCountInBed.ToString("N0") & "   CPU blocked cycles: " & _cpuCyclesBlockedInBed.ToString("N0") & Environment.NewLine &
               ReadyDiagnosticInBed("CPU", AtBusMaster286.Cpu) & Environment.NewLine &
               ReadyDiagnosticInBed("DMA8", AtBusMaster286.Dma8) & Environment.NewLine &
               ReadyDiagnosticInBed("DMA16", AtBusMaster286.Dma16) & Environment.NewLine &
               "  CPU I/O R8/R16           : " & _ioRead8InBed.ToString("N0") & " / " & _ioRead16InBed.ToString("N0") & Environment.NewLine &
               "  CPU I/O W8/W16           : " & _ioWrite8InBed.ToString("N0") & " / " & _ioWrite16InBed.ToString("N0") & Environment.NewLine &
               MasterMemoryDiagnosticInBed("CPU", AtBusMaster286.Cpu) & Environment.NewLine &
               MasterMemoryDiagnosticInBed("DMA8", AtBusMaster286.Dma8) & Environment.NewLine &
               MasterMemoryDiagnosticInBed("DMA16", AtBusMaster286.Dma16) & Environment.NewLine &
               MasterMemoryDiagnosticInBed("REFRESH", AtBusMaster286.Refresh) & Environment.NewLine &
               "  mapped R8/W8             : " & _mappedRead8InBed.ToString("N0") & " / " & _mappedWrite8InBed.ToString("N0") & Environment.NewLine &
               "  PCB page-gate yes/total  : " & _pageGatePositiveInBed.ToString("N0") & " / " & _pageGateQueriesInBed.ToString("N0") &
               If(_readyPolicyDescriptionInBed Is Nothing,
                  String.Empty,
                  Environment.NewLine & _readyPolicyDescriptionInBed.Invoke())
    End Function

    Private Function ReadyDiagnosticInBed(labelInBed As String,
                                          masterInBed As AtBusMaster286) As String
        Dim iInBed As Integer = MasterIndexInBed(masterInBed)
        Return "  " & labelInBed.PadRight(7) & " READY cycles/wait/T/max : " &
               _readyCycleCountInBed(iInBed).ToString("N0") & " / " &
               _readyWaitCycleCountInBed(iInBed).ToString("N0") & " / " &
               _readyWaitTStatesInBed(iInBed).ToString("N0") & " / " &
               _readyMaxWaitTStatesInBed(iInBed).ToString()
    End Function

    Private Function MasterMemoryDiagnosticInBed(labelInBed As String,
                                                 masterInBed As AtBusMaster286) As String
        Dim iInBed As Integer = MasterIndexInBed(masterInBed)
        Return "  " & labelInBed.PadRight(7) & " mem R8/R16 W8/W16 : " &
               _memoryRead8InBed(iInBed).ToString("N0") & " / " &
               _memoryRead16InBed(iInBed).ToString("N0") & "   " &
               _memoryWrite8InBed(iInBed).ToString("N0") & " / " &
               _memoryWrite16InBed(iInBed).ToString("N0")
    End Function
End Class

Public NotInheritable Class CpuLocalBus286
    Private ReadOnly _bridgeInBed As NeatMotherboardBridge286

    Public Sub New(bridgeInBed As NeatMotherboardBridge286)
        If bridgeInBed Is Nothing Then Throw New ArgumentNullException(NameOf(bridgeInBed))
        _bridgeInBed = bridgeInBed
    End Sub

    Public ReadOnly Property Identity As String
        Get
            Return "Harris CS80C286 local bus -> C&T NEAT motherboard bridge"
        End Get
    End Property

    Public ReadOnly Property Bridge As NeatMotherboardBridge286
        Get
            Return _bridgeInBed
        End Get
    End Property

    Public ReadOnly Property Fabric As HardwareBus
        Get
            Return _bridgeInBed.Fabric
        End Get
    End Property

    Public ReadOnly Property MemoryController As NeatMemoryController286
        Get
            Return _bridgeInBed.MemoryController
        End Get
    End Property

    <Runtime.CompilerServices.MethodImpl(Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)>
    Public Function ReadByte(port As UInt16) As Byte
        Return _bridgeInBed.ReadIoByte(port)
    End Function

    <Runtime.CompilerServices.MethodImpl(Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)>
    Public Function ReadWord(port As UInt16) As UInt16
        Return _bridgeInBed.ReadIoWord(port)
    End Function

    <Runtime.CompilerServices.MethodImpl(Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)>
    Public Sub WriteByte(port As UInt16, value As Byte)
        _bridgeInBed.WriteIoByte(port, value)
    End Sub

    <Runtime.CompilerServices.MethodImpl(Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)>
    Public Sub WriteWord(port As UInt16, value As UInt16)
        _bridgeInBed.WriteIoWord(port, value)
    End Sub

    Public Function ReadMemoryByte(address As UInteger) As Byte
        Return _bridgeInBed.ReadMemoryByte(address, AtBusMaster286.Cpu)
    End Function

    Public Function ReadMemoryByte(address As UInteger,
                                   ByRef targetInBed As AtMemoryCycleTarget286) As Byte
        Return _bridgeInBed.ReadMemoryByte(address, AtBusMaster286.Cpu, targetInBed)
    End Function

    Public Sub WriteMemoryByte(address As UInteger, value As Byte)
        _bridgeInBed.WriteMemoryByte(address, value, AtBusMaster286.Cpu)
    End Sub

    Public Sub WriteMemoryByte(address As UInteger,
                               value As Byte,
                               ByRef targetInBed As AtMemoryCycleTarget286)
        _bridgeInBed.WriteMemoryByte(address, value, AtBusMaster286.Cpu, targetInBed)
    End Sub

    Public Function ReadMemoryWord(address As UInteger) As UInt16
        Return _bridgeInBed.ReadMemoryWord(address, AtBusMaster286.Cpu)
    End Function

    Public Function ReadMemoryWord(address As UInteger,
                                   ByRef firstTargetInBed As AtMemoryCycleTarget286,
                                   ByRef secondTargetInBed As AtMemoryCycleTarget286,
                                   ByRef directWordInBed As Boolean) As UInt16
        Return _bridgeInBed.ReadMemoryWord(address, AtBusMaster286.Cpu,
                                           firstTargetInBed, secondTargetInBed,
                                           directWordInBed)
    End Function

    Public Sub WriteMemoryWord(address As UInteger, value As UInt16)
        _bridgeInBed.WriteMemoryWord(address, value, AtBusMaster286.Cpu)
    End Sub

    Public Sub WriteMemoryWord(address As UInteger,
                               value As UInt16,
                               ByRef firstTargetInBed As AtMemoryCycleTarget286,
                               ByRef secondTargetInBed As AtMemoryCycleTarget286,
                               ByRef directWordInBed As Boolean)
        _bridgeInBed.WriteMemoryWord(address, value, AtBusMaster286.Cpu,
                                     firstTargetInBed, secondTargetInBed,
                                     directWordInBed)
    End Sub

    ' Compatibility entry points retained while remaining motherboard devices are
    ' migrated. They no longer imply that the CPU owns the physical memory arrays.
    Public Function TryReadMemoryByte(address As UInteger, ByRef value As Byte) As Boolean
        Return _bridgeInBed.TryReadMappedMemoryByte(address, value)
    End Function

    Public Function TryWriteMemoryByte(address As UInteger, value As Byte) As Boolean
        Return _bridgeInBed.TryWriteMappedMemoryByte(address, value)
    End Function

    Public Function MemoryPageMayRouteToDeviceInBed(address As UInteger) As Boolean
        Return _bridgeInBed.MemoryPageMayRouteToDeviceInBed(address)
    End Function

    Public Function DiagnosticText() As String
        Return "CPU/local-bus adapter     : " & Identity
    End Function
End Class

Public Class HardwareBus
    Private Const PhysicalAddressBits As Integer = 24
    Private Const MemoryPageShift As Integer = 12
    Private Const MemoryPageCount As Integer = 1 << (PhysicalAddressBits - MemoryPageShift)

    Private NotInheritable Class PortRoute
        Public First As IPortDevice
        Public Additional As List(Of IPortDevice)

        Public Sub Add(device As IPortDevice)
            If First Is Nothing Then
                First = device
                Return
            End If
            If Additional Is Nothing Then Additional = New List(Of IPortDevice)()
            Additional.Add(device)
        End Sub
    End Class

    Private NotInheritable Class MemoryPageRoute
        Public ReadOnly Devices As IMemoryMappedDevice()

        Public Sub New(devicesIn As IMemoryMappedDevice())
            Devices = devicesIn
        End Sub
    End Class

    Private Shared ReadOnly EmptyMemoryDevices As IMemoryMappedDevice() =
        Array.Empty(Of IMemoryMappedDevice)()

    ' Descriptive module collections are retained for reset, diagnostics and
    ' event scheduling. Hot bus cycles use the compiled decode structures below.
    Private ReadOnly _devices As New List(Of IPortDevice)()
    Private ReadOnly _clockedDevices As New List(Of IClockedDevice)()
    Private ReadOnly _wakeSources As New List(Of IClockWakeSource)()
    Private ReadOnly _memoryDevices As New List(Of IMemoryMappedDevice)()
    Private ReadOnly _pageCoherentMemoryDevices As New List(Of IMemoryMappedDevice)()
    Private ReadOnly _uncachedMemoryDevices As New List(Of IMemoryMappedDevice)()
    Private ReadOnly _resettableDevices As New List(Of IResettableDevice)()

    ' The port table represents PCB chip-select wiring. Each entry contains only
    ' devices which can ever decode that port. Programmable decoders are still
    ' asked HandlesPort at the actual cycle.
    Private ReadOnly _portRoutes(65535) As PortRoute

    ' Memory is resolved lazily per 4 KiB page. Ordinary RAM pages very quickly
    ' cache an empty route, making instruction fetch/data RAM bypass the device
    ' list entirely. Guest I/O writes invalidate the cache because chipset/VGA
    ' registers may have changed physical memory decode.
    Private ReadOnly _memoryPageRoutes(MemoryPageCount - 1) As MemoryPageRoute

    Private _unclassifiedClockedDevices As Integer
    Private ReadOnly _unclassifiedClockedDeviceNamesInBed As New List(Of String)()

    ' Installed only while MachineClock286 is executing a CPU batch. CPU I/O
    ' forces elapsed motherboard time visible before the transaction. Memory
    ' devices explicitly marked clock-independent do not force that flush.
    Private _timeSynchronizer As Action(Of ClockBatchFlushReason)
    Private _timingTopologyChanged As Action
    Private _synchronizingTime As Boolean

    Public Sub Register(device As Object)
        If device Is Nothing Then Throw New ArgumentNullException(NameOf(device))

        Dim portDevice As IPortDevice = TryCast(device, IPortDevice)
        If portDevice IsNot Nothing Then
            _devices.Add(portDevice)
            CompilePortDecode(portDevice)
        End If

        Dim clocked As IClockedDevice = TryCast(device, IClockedDevice)
        If clocked IsNot Nothing Then
            _clockedDevices.Add(clocked)
            Dim wakeSource As IClockWakeSource = TryCast(device, IClockWakeSource)
            If wakeSource IsNot Nothing Then
                _wakeSources.Add(wakeSource)
            ElseIf Not TypeOf device Is IClockBatchSafeDevice Then
                ' Future/legacy clocked devices remain conservative until they
                ' explicitly declare either a wake deadline or batch-safe status.
                _unclassifiedClockedDevices += 1
                Dim typeNameInBed As String = device.GetType().FullName
                If String.IsNullOrWhiteSpace(typeNameInBed) Then typeNameInBed = device.GetType().Name
                _unclassifiedClockedDeviceNamesInBed.Add(typeNameInBed)
            End If
        End If

        Dim memoryDevice As IMemoryMappedDevice = TryCast(device, IMemoryMappedDevice)
        If memoryDevice IsNot Nothing Then
            _memoryDevices.Add(memoryDevice)
            If TypeOf memoryDevice Is IPageCoherentMemoryDecode Then
                _pageCoherentMemoryDevices.Add(memoryDevice)
            Else
                _uncachedMemoryDevices.Add(memoryDevice)
            End If
            InvalidateMemoryDecodeCache()
        End If

        Dim decodeSourceInBed As IMemoryDecodeChangeSource =
            TryCast(device, IMemoryDecodeChangeSource)
        If decodeSourceInBed IsNot Nothing Then
            AddHandler decodeSourceInBed.MemoryDecodeChanged,
                AddressOf InvalidateMemoryDecodeCache
        End If

        Dim resettable As IResettableDevice = TryCast(device, IResettableDevice)
        If resettable IsNot Nothing Then _resettableDevices.Add(resettable)
    End Sub

    ' Compile the physical port chip-select candidates once when the virtual PCB
    ' is assembled. This is substrate wiring, not BIOS/device discovery.
    Private Sub CompilePortDecode(device As IPortDevice)
        Dim dynamicDecode As IPortDecodeCandidateProvider =
            TryCast(device, IPortDecodeCandidateProvider)

        For portNumber As Integer = 0 To 65535
            Dim port As UInt16 = CUShort(portNumber)
            Dim mayDecode As Boolean
            If dynamicDecode IsNot Nothing Then
                mayDecode = dynamicDecode.PotentiallyHandlesPort(port)
            Else
                mayDecode = device.HandlesPort(port)
            End If

            If mayDecode Then
                Dim route As PortRoute = _portRoutes(portNumber)
                If route Is Nothing Then
                    route = New PortRoute()
                    _portRoutes(portNumber) = route
                End If
                route.Add(device)
            End If
        Next
    End Sub

    Private Function ResolvePortDevice(port As UInt16) As IPortDevice
        Dim route As PortRoute = _portRoutes(CInt(port))
        If route Is Nothing OrElse route.First Is Nothing Then Return Nothing

        If route.First.HandlesPort(port) Then Return route.First
        If route.Additional IsNot Nothing Then
            For Each device As IPortDevice In route.Additional
                If device.HandlesPort(port) Then Return device
            Next
        End If
        Return Nothing
    End Function

    Friend Function PortUsesAtBusTimingInBed(port As UInt16) As Boolean
        Dim deviceInBed As IPortDevice = ResolvePortDevice(port)
        If deviceInBed Is Nothing Then Return True
        Return Not TypeOf deviceInBed Is IMotherboardLocalPortDevice
    End Function

    Friend Sub InstallTimeBatchSynchronizer(synchronize As Action(Of ClockBatchFlushReason),
                                             timingTopologyChanged As Action)
        _timeSynchronizer = synchronize
        _timingTopologyChanged = timingTopologyChanged
    End Sub

    Friend Sub ClearTimeBatchSynchronizer()
        _timeSynchronizer = Nothing
        _timingTopologyChanged = Nothing
        _synchronizingTime = False
    End Sub

    Private Sub SynchronizePendingTime(Optional reason As ClockBatchFlushReason = ClockBatchFlushReason.Explicit)
        If _timeSynchronizer Is Nothing OrElse _synchronizingTime Then Return
        _synchronizingTime = True
        Try
            _timeSynchronizer.Invoke(reason)
        Finally
            _synchronizingTime = False
        End Try
    End Sub

    Private Sub NotifyTimingTopologyChanged()
        If _timingTopologyChanged IsNot Nothing Then _timingTopologyChanged.Invoke()
    End Sub

    Public ReadOnly Property HasUnclassifiedClockedDevices As Boolean
        Get
            Return _unclassifiedClockedDevices <> 0
        End Get
    End Property

    ' Host-only diagnostic: identify the clocked devices which force the
    ' conservative 64-T-state batching fallback.  This does not alter timing.
    Public ReadOnly Property UnclassifiedClockedDeviceDiagnosticText As String
        Get
            If _unclassifiedClockedDeviceNamesInBed.Count = 0 Then Return "0 (none)"
            Return _unclassifiedClockedDeviceNamesInBed.Count.ToString() & " — " &
                   String.Join(", ", _unclassifiedClockedDeviceNamesInBed)
        End Get
    End Property

    Public Function PicosecondsUntilNextWakeEvent() As Long
        Dim earliest As Long = Long.MaxValue
        For Each source As IClockWakeSource In _wakeSources
            Dim candidate As Long = source.PicosecondsUntilNextWakeEvent()
            If candidate <= 0 Then candidate = 1
            If candidate < earliest Then earliest = candidate
        Next
        Return earliest
    End Function

    Public Sub InvalidateMemoryDecodeCache()
        Array.Clear(_memoryPageRoutes, 0, _memoryPageRoutes.Length)
    End Sub

    Private Function MemoryPageCandidates(address As UInteger) As IMemoryMappedDevice()
        If address >= (1UI << PhysicalAddressBits) Then Return EmptyMemoryDevices

        Dim pageIndex As Integer = CInt(address >> MemoryPageShift)
        Dim route As MemoryPageRoute = _memoryPageRoutes(pageIndex)
        If route IsNot Nothing Then Return route.Devices

        Dim pageBase As UInteger = CUInt(pageIndex << MemoryPageShift)
        Dim candidates As List(Of IMemoryMappedDevice) = Nothing

        For Each device As IMemoryMappedDevice In _pageCoherentMemoryDevices
            If device.HandlesMemory(pageBase) Then
                If candidates Is Nothing Then candidates = New List(Of IMemoryMappedDevice)()
                candidates.Add(device)
            End If
        Next

        Dim devices As IMemoryMappedDevice()
        If candidates Is Nothing Then
            devices = EmptyMemoryDevices
        Else
            devices = candidates.ToArray()
        End If

        route = New MemoryPageRoute(devices)
        _memoryPageRoutes(pageIndex) = route
        Return devices
    End Function

    ' CROMWELL COMPILED PHYSICAL MEMORY ROUTE BRICK 7A
    ' This is the CPU-facing equivalent of a physical motherboard chip-select:
    ' answer whether this 4 KiB physical page can select any MMIO/memory device.
    ' The route itself is still resolved by the Brick-2 PCB cache and is invalidated
    ' whenever programmable chipset/VGA I/O rewires physical memory decode.
    <Runtime.CompilerServices.MethodImpl(Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)>
    Friend Function MemoryPageMayRouteToDeviceInBed(address As UInteger) As Boolean
        If address >= (1UI << PhysicalAddressBits) Then Return False
        Dim candidates As IMemoryMappedDevice() = MemoryPageCandidates(address)
        Return candidates.Length <> 0 OrElse _uncachedMemoryDevices.Count <> 0
    End Function

    Private Function MemoryAccessRequiresSynchronization(device As IMemoryMappedDevice) As Boolean
        Return Not TypeOf device Is IMemoryClockIndependentDevice
    End Function

    Private Function TryReadMappedDevice(device As IMemoryMappedDevice,
                                         address As UInteger,
                                         ByRef value As Byte) As Boolean
        If MemoryAccessRequiresSynchronization(device) Then SynchronizePendingTime(ClockBatchFlushReason.MemoryAccess)

        Dim conditional As IConditionalMemoryMappedDevice =
            TryCast(device, IConditionalMemoryMappedDevice)
        If conditional IsNot Nothing Then
            If Not conditional.TryReadMemoryByte(address, value) Then Return False
        Else
            value = device.ReadMemoryByte(address)
        End If

        If MemoryAccessRequiresSynchronization(device) Then NotifyTimingTopologyChanged()
        Return True
    End Function

    Private Function TryWriteMappedDevice(device As IMemoryMappedDevice,
                                          address As UInteger,
                                          value As Byte) As Boolean
        If MemoryAccessRequiresSynchronization(device) Then SynchronizePendingTime(ClockBatchFlushReason.MemoryAccess)

        Dim conditional As IConditionalMemoryMappedDevice =
            TryCast(device, IConditionalMemoryMappedDevice)
        If conditional IsNot Nothing Then
            If Not conditional.TryWriteMemoryByte(address, value) Then Return False
        Else
            device.WriteMemoryByte(address, value)
        End If

        If MemoryAccessRequiresSynchronization(device) Then NotifyTimingTopologyChanged()
        Return True
    End Function

    Private Shared Function MemoryTimingTargetInBed(deviceInBed As IMemoryMappedDevice,
                                                    addressInBed As UInteger,
                                                    isWriteInBed As Boolean) As AtMemoryCycleTarget286
        Dim providerInBed As IMemoryCycleTimingTargetProvider =
            TryCast(deviceInBed, IMemoryCycleTimingTargetProvider)
        If providerInBed Is Nothing Then Return AtMemoryCycleTarget286.MappedDevice
        Return providerInBed.GetMemoryCycleTimingTarget(addressInBed, isWriteInBed)
    End Function
    Public Function TryReadMemoryByte(address As UInteger, ByRef value As Byte) As Boolean
        Dim timingTargetInBed As AtMemoryCycleTarget286
        Return TryReadMemoryByte(address, value, timingTargetInBed)
    End Function
    Public Function TryReadMemoryByte(address As UInteger,
                                      ByRef value As Byte,
                                      ByRef timingTargetInBed As AtMemoryCycleTarget286) As Boolean
        Dim candidates As IMemoryMappedDevice() = MemoryPageCandidates(address)
        If candidates.Length = 0 AndAlso _uncachedMemoryDevices.Count = 0 Then Return False

        For Each device As IMemoryMappedDevice In candidates
            If TryReadMappedDevice(device, address, value) Then
                timingTargetInBed = MemoryTimingTargetInBed(device, address, False)
                Return True
            End If
        Next

        ' Non-page-coherent future devices retain exact per-cycle decode.
        For Each device As IMemoryMappedDevice In _uncachedMemoryDevices
            If device.HandlesMemory(address) AndAlso
               TryReadMappedDevice(device, address, value) Then
                timingTargetInBed = MemoryTimingTargetInBed(device, address, False)
                Return True
            End If
        Next

        Return False
    End Function

    Public Function TryWriteMemoryByte(address As UInteger, value As Byte) As Boolean
        Dim timingTargetInBed As AtMemoryCycleTarget286
        Return TryWriteMemoryByte(address, value, timingTargetInBed)
    End Function
    Public Function TryWriteMemoryByte(address As UInteger,
                                       value As Byte,
                                       ByRef timingTargetInBed As AtMemoryCycleTarget286) As Boolean
        Dim candidates As IMemoryMappedDevice() = MemoryPageCandidates(address)
        If candidates.Length = 0 AndAlso _uncachedMemoryDevices.Count = 0 Then Return False

        For Each device As IMemoryMappedDevice In candidates
            If TryWriteMappedDevice(device, address, value) Then
                timingTargetInBed = MemoryTimingTargetInBed(device, address, True)
                Return True
            End If
        Next

        For Each device As IMemoryMappedDevice In _uncachedMemoryDevices
            If device.HandlesMemory(address) AndAlso
               TryWriteMappedDevice(device, address, value) Then
                timingTargetInBed = MemoryTimingTargetInBed(device, address, True)
                Return True
            End If
        Next

        Return False
    End Function

    Private Function ReadPortByteCore(port As UInt16) As Byte
        Dim device As IPortDevice = ResolvePortDevice(port)
        If device Is Nothing Then Return &HFF
        Return device.ReadPort(port)
    End Function

    Private Sub WritePortByteCore(port As UInt16, value As Byte)
        Dim device As IPortDevice = ResolvePortDevice(port)
        If device IsNot Nothing Then device.WritePort(port, value)
    End Sub

    Public Function ReadByte(port As UInt16) As Byte
        SynchronizePendingTime(ClockBatchFlushReason.PortAccess)
        Dim result As Byte = ReadPortByteCore(port)
        NotifyTimingTopologyChanged()
        Return result
    End Function

    Public Function ReadWord(port As UInt16) As UInt16
        SynchronizePendingTime(ClockBatchFlushReason.PortAccess)

        Dim device As IPortDevice = ResolvePortDevice(port)
        If device IsNot Nothing Then
            Dim wordDevice As IWordPortDevice = TryCast(device, IWordPortDevice)
            If wordDevice IsNot Nothing Then
                Dim result As UInt16 = wordDevice.ReadPortWord(port)
                NotifyTimingTopologyChanged()
                Return result
            End If
        End If

        Dim low As Byte = ReadPortByteCore(port)
        Dim high As Byte = ReadPortByteCore(CUShort((CInt(port) + 1) And &HFFFF))
        NotifyTimingTopologyChanged()
        Return CUShort(CUShort(low) Or (CUShort(high) << 8))
    End Function

    Public Sub WriteByte(port As UInt16, value As Byte)
        SynchronizePendingTime(ClockBatchFlushReason.PortAccess)
        WritePortByteCore(port, value)

        ' Devices which can really rewire MMIO/ROM/RAM decode raise
        ' IMemoryDecodeChangeSource.MemoryDecodeChanged themselves.  Do not
        ' invalidate the whole physical-memory route cache for unrelated OUTs.
        NotifyTimingTopologyChanged()
    End Sub

    Public Sub WriteWord(port As UInt16, value As UInt16)
        SynchronizePendingTime(ClockBatchFlushReason.PortAccess)

        Dim device As IPortDevice = ResolvePortDevice(port)
        If device IsNot Nothing Then
            Dim wordDevice As IWordPortDevice = TryCast(device, IWordPortDevice)
            If wordDevice IsNot Nothing Then
                wordDevice.WritePortWord(port, value)
                NotifyTimingTopologyChanged()
                Return
            End If
        End If

        WritePortByteCore(port, CByte(value And &HFFUS))
        WritePortByteCore(CUShort((CInt(port) + 1) And &HFFFF), CByte(value >> 8))
        NotifyTimingTopologyChanged()
    End Sub

    Public Sub AdvanceTime(elapsedPicoseconds As Long)
        If elapsedPicoseconds < 0 Then Throw New ArgumentOutOfRangeException(NameOf(elapsedPicoseconds))
        For Each device As IClockedDevice In _clockedDevices
            device.AdvanceTime(elapsedPicoseconds)
        Next
    End Sub

    Public Sub ResetDevices()
        SynchronizePendingTime(ClockBatchFlushReason.Reset)
        For Each device As IResettableDevice In _resettableDevices
            device.ResetDevice()
        Next
        InvalidateMemoryDecodeCache()
        NotifyTimingTopologyChanged()
    End Sub

    Public Sub PowerCycleDevices()
        SynchronizePendingTime(ClockBatchFlushReason.Reset)
        For Each device As IResettableDevice In _resettableDevices
            Dim powerCycledInBed As IPowerCycleDevice = TryCast(device, IPowerCycleDevice)
            If powerCycledInBed IsNot Nothing Then
                powerCycledInBed.PowerCycleDevice()
            Else
                device.ResetDevice()
            End If
        Next
        InvalidateMemoryDecodeCache()
        NotifyTimingTopologyChanged()
    End Sub
End Class

' Intel 8259A-compatible single master PIC.  It models initialization,
' masking, priority selection, in-service state, EOI, and vector delivery.
Public Class Pic8259
    Implements IPortDevice, IResettableDevice, IMotherboardLocalPortDevice

    Private ReadOnly _commandPort As UInt16
    Private ReadOnly _dataPort As UInt16
    Private _vectorBase As Byte
    Private _interruptMask As Byte
    Private _interruptRequest As Byte
    Private _inService As Byte
    Private _lineLevels As Byte

    Private _initializationStep As Integer
    Private _expectIcw4 As Boolean
    Private _singleMode As Boolean
    Private _levelTriggered As Boolean
    Private _icw3 As Byte
    Private _icw4 As Byte
    Private _autoEoi As Boolean
    Private _specialFullyNested As Boolean

    Private _readIsr As Boolean
    Private _pollNextRead As Boolean
    Private _specialMaskMode As Boolean
    Private _rotateOnAutoEoi As Boolean
    Private _lowestPriority As Integer = 7
    Private _interruptOutput As Boolean

    Public Event InterruptOutputChanged(asserted As Boolean)

    Public Sub New(Optional commandPort As UInt16 = &H20US,
                   Optional dataPort As UInt16 = &H21US,
                   Optional vectorBase As Byte = &H8)
        _commandPort = commandPort
        _dataPort = dataPort
        _vectorBase = CByte(vectorBase And &HF8)
        ResetDevice()
        _vectorBase = CByte(vectorBase And &HF8)
    End Sub

    Public Sub ResetDevice() Implements IResettableDevice.ResetDevice
        _interruptMask = &HFF
        _interruptRequest = 0
        _inService = 0
        _lineLevels = 0
        _initializationStep = 0
        _expectIcw4 = False
        _singleMode = False
        _levelTriggered = False
        _icw3 = 0
        _icw4 = 0
        _autoEoi = False
        _specialFullyNested = False
        _readIsr = False
        _pollNextRead = False
        _specialMaskMode = False
        _rotateOnAutoEoi = False
        _lowestPriority = 7
        SetInterruptOutput(False)
    End Sub

    Public Function HandlesPort(port As UInt16) As Boolean Implements IPortDevice.HandlesPort
        Return port = _commandPort OrElse port = _dataPort
    End Function

    Public Function ReadPort(port As UInt16) As Byte Implements IPortDevice.ReadPort
        If port = _dataPort Then Return _interruptMask

        If _pollNextRead Then
            _pollNextRead = False
            Dim irq As Integer = FindPendingIrq()
            If irq < 0 Then Return 0
            AcknowledgeIrq(irq)
            Return CByte(&H80 Or irq)
        End If

        Return If(_readIsr, _inService, _interruptRequest)
    End Function

    Public Sub WritePort(port As UInt16, value As Byte) Implements IPortDevice.WritePort
        If port = _commandPort Then
            If (value And &H10) <> 0 Then
                BeginInitialization(value)
                Return
            End If

            If (value And &H18) = &H8 Then
                WriteOcw3(value)
                Return
            End If

            WriteOcw2(value)
            Return
        End If

        If _initializationStep <> 0 Then
            WriteInitializationData(value)
        Else
            _interruptMask = value
            UpdateInterruptOutput()
        End If
    End Sub

    Private Sub BeginInitialization(icw1 As Byte)
        _levelTriggered = (icw1 And &H8) <> 0
        _singleMode = (icw1 And &H2) <> 0
        _expectIcw4 = (icw1 And &H1) <> 0
        _interruptMask = 0
        _interruptRequest = 0
        _inService = 0
        _lowestPriority = 7
        _autoEoi = False
        _specialFullyNested = False
        _specialMaskMode = False
        _rotateOnAutoEoi = False
        _initializationStep = 1 ' ICW2 next
        UpdateInterruptOutput()
    End Sub

    Private Sub WriteInitializationData(value As Byte)
        Select Case _initializationStep
            Case 1
                _vectorBase = CByte(value And &HF8)
                If _singleMode Then
                    _initializationStep = If(_expectIcw4, 3, 0)
                Else
                    _initializationStep = 2
                End If
            Case 2
                _icw3 = value
                _initializationStep = If(_expectIcw4, 3, 0)
            Case 3
                _icw4 = value
                _autoEoi = (value And &H2) <> 0
                _specialFullyNested = (value And &H10) <> 0
                _initializationStep = 0
        End Select
        UpdateInterruptOutput()
    End Sub

    Private Sub WriteOcw3(value As Byte)
        ' ESMM/SMM control.
        If (value And &H40) <> 0 Then
            _specialMaskMode = (value And &H20) <> 0
        End If

        ' Poll affects the next command-port read.
        If (value And &H4) <> 0 Then _pollNextRead = True

        ' RR/RIS select IRR versus ISR for ordinary command-port reads.
        If (value And &H2) <> 0 Then _readIsr = (value And &H1) <> 0
        UpdateInterruptOutput()
    End Sub

    Private Sub WriteOcw2(value As Byte)
        Dim rotate As Boolean = (value And &H80) <> 0
        Dim specific As Boolean = (value And &H40) <> 0
        Dim eoi As Boolean = (value And &H20) <> 0
        Dim level As Integer = value And 7

        If eoi Then
            Dim serviced As Integer
            If specific Then
                serviced = level
            Else
                serviced = HighestPrioritySetBit(_inService, respectMask:=_specialMaskMode)
            End If
            If serviced >= 0 Then
                _inService = CByte(_inService And Not (1 << serviced))
                If rotate Then _lowestPriority = serviced
            End If
        ElseIf rotate AndAlso specific Then
            ' Set Priority: selected level becomes the lowest priority.
            _lowestPriority = level
        ElseIf rotate AndAlso Not specific Then
            ' Rotate in automatic-EOI mode SET.
            _rotateOnAutoEoi = True
        ElseIf Not rotate AndAlso Not specific Then
            ' Rotate in automatic-EOI mode CLEAR / NOP.
            _rotateOnAutoEoi = False
        End If

        RefreshLevelRequests()
        UpdateInterruptOutput()
    End Sub

    Public Sub SetIrqLine(irq As Integer, asserted As Boolean)
        If irq < 0 OrElse irq > 7 Then Throw New ArgumentOutOfRangeException(NameOf(irq))
        Dim bit As Byte = CByte(1 << irq)
        Dim wasAsserted As Boolean = (_lineLevels And bit) <> 0

        If asserted Then
            _lineLevels = CByte(_lineLevels Or bit)
            If _levelTriggered OrElse Not wasAsserted Then
                _interruptRequest = CByte(_interruptRequest Or bit)
            End If
        Else
            _lineLevels = CByte(_lineLevels And Not bit)
            If _levelTriggered Then _interruptRequest = CByte(_interruptRequest And Not bit)
        End If
        UpdateInterruptOutput()
    End Sub

    Public Sub RaiseIrq(irq As Integer)
        SetIrqLine(irq, True)
    End Sub

    Public Sub ClearIrq(irq As Integer)
        SetIrqLine(irq, False)
    End Sub

    Public Sub PulseIrq(irq As Integer)
        SetIrqLine(irq, True)
        SetIrqLine(irq, False)
    End Sub

    Public ReadOnly Property HasPendingInterrupt As Boolean
        Get
            Return FindPendingIrq() >= 0
        End Get
    End Property

    Public ReadOnly Property InterruptOutput As Boolean
        Get
            Return _interruptOutput
        End Get
    End Property

    Public Function IsProgrammedCascadeInput(irq As Integer) As Boolean
        If irq < 0 OrElse irq > 7 Then Throw New ArgumentOutOfRangeException(NameOf(irq))
        Return Not _singleMode AndAlso (_icw3 And (1 << irq)) <> 0
    End Function

    Public Function DiagnosticText() As String
        Dim sbInBed As New System.Text.StringBuilder()
        sbInBed.Append("8259 at ").Append(_commandPort.ToString("X2")).Append("h/").Append(_dataPort.ToString("X2")).Append("h").AppendLine()
        sbInBed.Append("  base / IMR / IRR / ISR : ").Append(_vectorBase.ToString("X2")).Append(" / ").Append(_interruptMask.ToString("X2")).Append(" / ").Append(_interruptRequest.ToString("X2")).Append(" / ").Append(_inService.ToString("X2")).AppendLine()
        sbInBed.Append("  line levels / INTR     : ").Append(_lineLevels.ToString("X2")).Append(" / ").Append(_interruptOutput).AppendLine()
        sbInBed.Append("  init step / ICW3/ICW4  : ").Append(_initializationStep).Append(" / ").Append(_icw3.ToString("X2")).Append(" / ").Append(_icw4.ToString("X2")).AppendLine()
        sbInBed.Append("  pending IRQ / priority : ").Append(FindPendingIrq()).Append(" / lowest ").Append(_lowestPriority).AppendLine()
        sbInBed.Append("  level/autoEOI/SMM/SFNM : ").Append(_levelTriggered).Append(" / ").Append(_autoEoi).Append(" / ").Append(_specialMaskMode).Append(" / ").Append(_specialFullyNested)
        Return sbInBed.ToString()
    End Function

    Public Function Acknowledge() As Integer
        Dim irq As Integer = FindPendingIrq()
        If irq < 0 Then
            ' On a real 8259A a disappearing request between INTR and INTA
            ' produces a spurious IRQ7 vector.
            Return _vectorBase + 7
        End If
        AcknowledgeIrq(irq)
        Return _vectorBase + irq
    End Function

    Private Sub AcknowledgeIrq(irq As Integer)
        Dim bit As Byte = CByte(1 << irq)

        ' INTR is negated during the INTA sequence.  If another eligible request
        ' remains, UpdateInterruptOutput below reasserts it.  This edge matters
        ' on an AT cascade: the slave's renewed INTR must be able to relatch
        ' master IRQ2 after the first slave interrupt is acknowledged.
        SetInterruptOutput(False)

        ' Edge-triggered requests are consumed by acknowledge.  In level mode the
        ' IRR bit follows the external line and is restored below if still high.
        _interruptRequest = CByte(_interruptRequest And Not bit)

        If Not _autoEoi Then
            _inService = CByte(_inService Or bit)
        ElseIf _rotateOnAutoEoi Then
            _lowestPriority = irq
        End If

        RefreshLevelRequests()
        UpdateInterruptOutput()
    End Sub

    Private Sub RefreshLevelRequests()
        If Not _levelTriggered Then Return
        _interruptRequest = CByte((_interruptRequest And Not _lineLevels) Or _lineLevels)
    End Sub

    Private Function FindPendingIrq() As Integer
        Dim eligible As Byte = CByte(_interruptRequest And Not _interruptMask)
        If eligible = 0 Then Return -1

        Dim activeIsr As Byte = _inService
        If _specialMaskMode Then activeIsr = CByte(activeIsr And Not _interruptMask)

        Dim inServicePriority As Integer = HighestPrioritySetBit(activeIsr, respectMask:=False)
        For rank As Integer = 0 To 7
            Dim irq As Integer = PriorityAtRank(rank)
            Dim bit As Integer = 1 << irq
            If (eligible And bit) = 0 Then Continue For

            If inServicePriority >= 0 Then
                Dim requestRank As Integer = RankOf(irq)
                Dim serviceRank As Integer = RankOf(inServicePriority)
                Dim specialNestedCascadeReentryInBed As Boolean =
                    _specialFullyNested AndAlso
                    irq = inServicePriority AndAlso
                    IsProgrammedCascadeInput(irq)
                If requestRank >= serviceRank AndAlso Not specialNestedCascadeReentryInBed Then Continue For
            End If
            Return irq
        Next
        Return -1
    End Function

    Private Function HighestPrioritySetBit(bits As Byte, respectMask As Boolean) As Integer
        Dim source As Byte = If(respectMask, CByte(bits And Not _interruptMask), bits)
        For rank As Integer = 0 To 7
            Dim irq As Integer = PriorityAtRank(rank)
            If (source And (1 << irq)) <> 0 Then Return irq
        Next
        Return -1
    End Function

    Private Function PriorityAtRank(rank As Integer) As Integer
        Return (_lowestPriority + 1 + rank) And 7
    End Function

    Private Function RankOf(irq As Integer) As Integer
        Return (irq - (_lowestPriority + 1) + 8) And 7
    End Function

    Private Sub UpdateInterruptOutput()
        SetInterruptOutput(FindPendingIrq() >= 0)
    End Sub

    Private Sub SetInterruptOutput(asserted As Boolean)
        If asserted = _interruptOutput Then Return
        _interruptOutput = asserted
        RaiseEvent InterruptOutputChanged(asserted)
    End Sub
End Class

Public Class Pit8253
    Implements IPortDevice, IClockedDevice, IClockWakeSource, IResettableDevice, IMotherboardLocalPortDevice

    Private Class Channel
        Public ReloadValue As Integer
        Public Counter As Integer
        Public Mode As Integer = 3
        Public RawModeBits As Integer = 3
        Public AccessMode As Integer = 3
        Public Bcd As Boolean

        Public WriteHighByteNext As Boolean
        Public ReadHighByteNext As Boolean
        Public WriteLowByte As Byte

        Public Output As Boolean = True
        Public Gate As Boolean
        Public NullCount As Boolean = True
        Public Running As Boolean
        Public AwaitingTrigger As Boolean
        Public StrobeLowTicks As Integer

        ' Phase within modes 2/3, measured in PIT input clocks from reload.
        Public Phase As Long

        Public CountLatched As Boolean
        Public LatchedCount As UInt16
        Public LatchReadHighByteNext As Boolean
        Public StatusLatched As Boolean
        Public LatchedStatus As Byte
        Public RefreshToggle As Boolean
    End Class

    Private ReadOnly _channels() As Channel = {
        New Channel() With {.Gate = True},
        New Channel() With {.Gate = True},
        New Channel() With {.Gate = False}
    }
    Private ReadOnly _pic As Pic8259
    Private _timeNumeratorRemainder As Long

    ' OUT1 is wired to the 82C211 REFREQ input in a NEAT/82C206 AT design.
    ' Expose the edge at the electrical boundary instead of faking DMA channel 0.
    Public Event RefreshRequest()

    Public Sub New(pic As Pic8259)
        If pic Is Nothing Then Throw New ArgumentNullException(NameOf(pic))
        _pic = pic
        ResetDevice()
    End Sub

    Public Sub ResetDevice() Implements IResettableDevice.ResetDevice
        _timeNumeratorRemainder = 0
        For channelIndex As Integer = 0 To 2
            Dim channel As Channel = _channels(channelIndex)
            channel.ReloadValue = 0
            channel.Counter = 0
            channel.Mode = 3
            channel.RawModeBits = 3
            channel.AccessMode = 3
            channel.Bcd = False
            channel.WriteHighByteNext = False
            channel.ReadHighByteNext = False
            channel.WriteLowByte = 0
            channel.Output = True
            channel.Gate = channelIndex <> 2
            channel.NullCount = True
            channel.Running = False
            channel.AwaitingTrigger = False
            channel.StrobeLowTicks = 0
            channel.Phase = 0
            channel.CountLatched = False
            channel.LatchedCount = 0
            channel.LatchReadHighByteNext = False
            channel.StatusLatched = False
            channel.LatchedStatus = 0
            channel.RefreshToggle = False
        Next
        _pic.ClearIrq(0)
    End Sub

    Public Function HandlesPort(port As UInt16) As Boolean Implements IPortDevice.HandlesPort
        Return port >= &H40US AndAlso port <= &H43US
    End Function

    Public Function ReadPort(port As UInt16) As Byte Implements IPortDevice.ReadPort
        If port = &H43US Then Return &HFF

        Dim channel As Channel = _channels(CInt(port - &H40US))

        ' In an 8254 read-back sequence the latched status byte precedes the
        ' latched count bytes on the same counter port.
        If channel.StatusLatched Then
            channel.StatusLatched = False
            Return channel.LatchedStatus
        End If

        Dim registerValue As UInt16 =
            If(channel.CountLatched, channel.LatchedCount, CurrentCounterRegister(channel))

        Select Case channel.AccessMode
            Case 1 ' low byte
                If channel.CountLatched Then channel.CountLatched = False
                Return CByte(registerValue And &HFFUS)

            Case 2 ' high byte
                If channel.CountLatched Then channel.CountLatched = False
                Return CByte(registerValue >> 8)

            Case Else ' low then high
                Dim highNext As Boolean =
                    If(channel.CountLatched, channel.LatchReadHighByteNext, channel.ReadHighByteNext)
                Dim result As Byte =
                    If(highNext, CByte(registerValue >> 8), CByte(registerValue And &HFFUS))

                If channel.CountLatched Then
                    channel.LatchReadHighByteNext = Not channel.LatchReadHighByteNext
                    If Not channel.LatchReadHighByteNext Then channel.CountLatched = False
                Else
                    channel.ReadHighByteNext = Not channel.ReadHighByteNext
                End If
                Return result
        End Select
    End Function

    Public Sub WritePort(port As UInt16, value As Byte) Implements IPortDevice.WritePort
        If port = &H43US Then
            WriteControl(value)
            Return
        End If

        Dim channel As Channel = _channels(CInt(port - &H40US))
        Select Case channel.AccessMode
            Case 1
                LoadCounter(channel, value)
            Case 2
                LoadCounter(channel, CInt(value) << 8)
            Case 3
                If Not channel.WriteHighByteNext Then
                    channel.WriteLowByte = value
                    channel.WriteHighByteNext = True
                Else
                    LoadCounter(channel, CInt(channel.WriteLowByte) Or (CInt(value) << 8))
                    channel.WriteHighByteNext = False
                End If
        End Select
    End Sub

    Private Sub WriteControl(value As Byte)
        Dim selected As Integer = (value >> 6) And 3

        If selected = 3 Then
            WriteReadBack(value)
            Return
        End If

        Dim channel As Channel = _channels(selected)
        Dim access As Integer = (value >> 4) And 3

        If access = 0 Then
            LatchCount(selected)
            Return
        End If

        channel.AccessMode = access
        channel.RawModeBits = (value >> 1) And 7
        channel.Mode = channel.RawModeBits
        If channel.Mode > 5 Then channel.Mode -= 4 ' 6=>2, 7=>3 aliases
        channel.Bcd = (value And 1) <> 0

        channel.WriteHighByteNext = False
        channel.ReadHighByteNext = False
        channel.CountLatched = False
        channel.StatusLatched = False
        channel.NullCount = True
        channel.Running = False
        channel.AwaitingTrigger = channel.Mode = 1 OrElse channel.Mode = 5
        channel.StrobeLowTicks = 0
        channel.Phase = 0

        ' Programming a mode establishes the documented idle OUT state.
        Select Case channel.Mode
            Case 0
                channel.Output = False
            Case Else
                channel.Output = True
        End Select
    End Sub

    Private Sub WriteReadBack(value As Byte)
        Dim latchCountRequested As Boolean = (value And &H20) = 0
        Dim latchStatusRequested As Boolean = (value And &H10) = 0

        For channelIndex As Integer = 0 To 2
            Dim selectBit As Integer = 2 << channelIndex ' D1=ctr0,D2=ctr1,D3=ctr2
            If (value And selectBit) <> 0 Then Continue For

            If latchCountRequested Then LatchCount(channelIndex)
            If latchStatusRequested Then LatchStatus(channelIndex)
        Next
    End Sub

    Private Sub LatchCount(channelIndex As Integer)
        Dim channel As Channel = _channels(channelIndex)
        If channel.CountLatched Then Return
        channel.LatchedCount = CurrentCounterRegister(channel)
        channel.CountLatched = True
        channel.LatchReadHighByteNext = False
    End Sub

    Private Sub LatchStatus(channelIndex As Integer)
        Dim channel As Channel = _channels(channelIndex)
        If channel.StatusLatched Then Return

        Dim status As Byte
        If channel.Output Then status = CByte(status Or &H80)
        If channel.NullCount Then status = CByte(status Or &H40)
        status = CByte(status Or ((channel.AccessMode And 3) << 4))
        status = CByte(status Or ((channel.RawModeBits And 7) << 1))
        If channel.Bcd Then status = CByte(status Or 1)

        channel.LatchedStatus = status
        channel.StatusLatched = True
    End Sub

    Private Sub LoadCounter(channel As Channel, rawValue As Integer)
        Dim decoded As Integer
        If channel.Bcd Then
            decoded = DecodeBcdCounter(CUShort(rawValue And &HFFFF))
            If decoded = 0 Then decoded = 10000
        Else
            decoded = rawValue And &HFFFF
            If decoded = 0 Then decoded = 65536
        End If

        channel.ReloadValue = decoded
        channel.Counter = decoded
        channel.Phase = 0
        channel.NullCount = False
        channel.ReadHighByteNext = False
        channel.CountLatched = False
        channel.StatusLatched = False
        channel.StrobeLowTicks = 0

        Select Case channel.Mode
            Case 0
                channel.Output = False
                channel.Running = channel.Gate
                channel.AwaitingTrigger = False

            Case 1
                ' Hardware retriggerable one-shot waits for a rising GATE.
                channel.Output = True
                channel.Running = False
                channel.AwaitingTrigger = True

            Case 2, 3
                channel.Output = True
                channel.Running = channel.Gate
                channel.AwaitingTrigger = False

            Case 4
                channel.Output = True
                channel.Running = channel.Gate
                channel.AwaitingTrigger = False

            Case 5
                channel.Output = True
                channel.Running = False
                channel.AwaitingTrigger = True
        End Select
    End Sub

    Private Shared Function DecodeBcdCounter(value As UInt16) As Integer
        Return CInt(value And &HFUS) +
               CInt((value >> 4) And &HFUS) * 10 +
               CInt((value >> 8) And &HFUS) * 100 +
               CInt((value >> 12) And &HFUS) * 1000
    End Function

    Private Shared Function EncodeBcdCounter(value As Integer) As UInt16
        If value <= 0 OrElse value >= 10000 Then Return 0US
        Dim thousands As Integer = value \ 1000
        value -= thousands * 1000
        Dim hundreds As Integer = value \ 100
        value -= hundreds * 100
        Dim tens As Integer = value \ 10
        Dim ones As Integer = value Mod 10
        Return CUShort((thousands << 12) Or (hundreds << 8) Or (tens << 4) Or ones)
    End Function

    Private Function CurrentCounterRegister(channel As Channel) As UInt16
        Dim count As Integer = CurrentCounterValue(channel)
        If channel.Bcd Then Return EncodeBcdCounter(count)
        If count <= 0 OrElse count >= 65536 Then Return 0US
        Return CUShort(count)
    End Function

    Private Shared Function CurrentCounterValue(channel As Channel) As Integer
        If channel.ReloadValue <= 0 Then Return 0

        Select Case channel.Mode
            Case 2
                If Not channel.Running Then Return channel.Counter
                Dim count As Long = channel.ReloadValue - channel.Phase
                If count <= 0 Then count = channel.ReloadValue
                Return CInt(count)

            Case 3
                If Not channel.Running Then Return channel.Counter
                Dim n As Integer = channel.ReloadValue
                Dim p As Integer = CInt(channel.Phase Mod n)
                Dim highTicks As Integer = (n + 1) \ 2

                If p < highTicks Then
                    Dim value As Integer = n - 2 * p
                    If value <= 0 Then value = 1
                    Return value
                End If

                Dim q As Integer = p - highTicks
                Dim lowStart As Integer = If((n And 1) = 0, n, n - 1)
                Dim lowValue As Integer = lowStart - 2 * q
                If lowValue <= 0 Then lowValue = 2
                Return lowValue

            Case Else
                Return channel.Counter
        End Select
    End Function

    Public Sub SetGate(channelIndex As Integer, enabled As Boolean)
        If channelIndex < 0 OrElse channelIndex > 2 Then
            Throw New ArgumentOutOfRangeException(NameOf(channelIndex))
        End If

        Dim channel As Channel = _channels(channelIndex)
        Dim rising As Boolean = enabled AndAlso Not channel.Gate
        channel.Gate = enabled

        Select Case channel.Mode
            Case 0, 4
                channel.Running = enabled AndAlso Not channel.NullCount AndAlso channel.Counter > 0

            Case 1
                If rising AndAlso Not channel.NullCount Then TriggerOneShot(channel)

            Case 2, 3
                If Not enabled Then
                    channel.Running = False
                    channel.Output = True
                ElseIf rising AndAlso Not channel.NullCount Then
                    RestartPeriodic(channel)
                End If

            Case 5
                If rising AndAlso Not channel.NullCount Then TriggerStrobe(channel)
        End Select
    End Sub

    Private Shared Sub RestartPeriodic(channel As Channel)
        channel.Counter = channel.ReloadValue
        channel.Phase = 0
        channel.Output = True
        channel.Running = True
    End Sub

    Private Shared Sub TriggerOneShot(channel As Channel)
        channel.Counter = channel.ReloadValue
        channel.Phase = 0
        channel.Output = False
        channel.Running = True
        channel.AwaitingTrigger = False
    End Sub

    Private Shared Sub TriggerStrobe(channel As Channel)
        channel.Counter = channel.ReloadValue
        channel.Output = True
        channel.Running = True
        channel.AwaitingTrigger = False
        channel.StrobeLowTicks = 0
    End Sub

    Public Function GetOutput(channelIndex As Integer) As Boolean
        If channelIndex < 0 OrElse channelIndex > 2 Then
            Throw New ArgumentOutOfRangeException(NameOf(channelIndex))
        End If
        Return _channels(channelIndex).Output
    End Function
    ' CROMWELL PC SPEAKER SAMPLE-AT-OFFSET BRICK 1
    ' Returns the logic level that this counter will have after the supplied
    ' physical-time offset WITHOUT mutating PIT state.  PcSpeakerDevice is
    ' clocked immediately before the PIT and uses this to render every PCM
    ' sample inside a batched motherboard time interval.
    Public Function GetOutputAtOffset(channelIndex As Integer, elapsedPicoseconds As Long) As Boolean
        If channelIndex < 0 OrElse channelIndex > 2 Then
            Throw New ArgumentOutOfRangeException(NameOf(channelIndex))
        End If
        If elapsedPicoseconds < 0 Then Throw New ArgumentOutOfRangeException(NameOf(elapsedPicoseconds))

        Dim channel As Channel = _channels(channelIndex)
        If elapsedPicoseconds = 0 OrElse channel.NullCount OrElse channel.ReloadValue <= 0 Then
            Return channel.Output
        End If

        ' Include the PIT's fractional input-clock remainder exactly as the real
        ' AdvanceTime path will.  Decomposing whole seconds keeps the multiply
        ' bounded even if a diagnostic ever asks for an unusually large offset.
        Dim wholeSecondsInBed As Long = elapsedPicoseconds \ MachineProfile286.PicosecondsPerSecond
        Dim remainderPicosecondsInBed As Long = elapsedPicoseconds Mod MachineProfile286.PicosecondsPerSecond
        Dim numeratorInBed As Long =
            _timeNumeratorRemainder +
            remainderPicosecondsInBed * MachineProfile286.PitInputClockHz
        Dim ticksInBed As Long =
            wholeSecondsInBed * MachineProfile286.PitInputClockHz +
            numeratorInBed \ MachineProfile286.PicosecondsPerSecond

        If ticksInBed <= 0 Then Return channel.Output

        Select Case channel.Mode
            Case 0
                If Not channel.Running OrElse Not channel.Gate OrElse channel.Counter <= 0 Then
                    Return channel.Output
                End If
                Return ticksInBed >= channel.Counter

            Case 1
                If Not channel.Running OrElse channel.Counter <= 0 Then Return channel.Output
                If ticksInBed < channel.Counter Then Return False
                Return True

            Case 2
                If Not channel.Running OrElse Not channel.Gate Then Return channel.Output
                Dim periodInBed As Long = channel.ReloadValue
                Dim phaseInBed As Long = (channel.Phase + ticksInBed) Mod periodInBed
                Return phaseInBed <> periodInBed - 1L

            Case 3
                If Not channel.Running OrElse Not channel.Gate Then Return channel.Output
                Dim periodInBed As Long = channel.ReloadValue
                Dim phaseInBed As Long = (channel.Phase + ticksInBed) Mod periodInBed
                Dim highTicksInBed As Long = (periodInBed + 1L) \ 2L
                Return phaseInBed < highTicksInBed

            Case 4, 5
                If channel.StrobeLowTicks > 0 Then
                    Return ticksInBed >= channel.StrobeLowTicks
                End If

                If Not channel.Running OrElse channel.Counter <= 0 Then Return channel.Output
                If channel.Mode = 4 AndAlso Not channel.Gate Then Return channel.Output

                If ticksInBed < channel.Counter Then Return True
                If ticksInBed = channel.Counter Then Return False
                Return True

            Case Else
                Return channel.Output
        End Select
    End Function

    Public ReadOnly Property RefreshDetect As Boolean
        Get
            Return _channels(1).RefreshToggle
        End Get
    End Property

    Public Function DiagnosticText() As String
        Dim sbInBed As New System.Text.StringBuilder()
        sbInBed.Append("8253 PIT state").AppendLine()
        For channelIndexInBed As Integer = 0 To 2
            Dim channelInBed As Channel = _channels(channelIndexInBed)
            sbInBed.Append("  CH").Append(channelIndexInBed).Append(" mode=").Append(channelInBed.Mode)
            sbInBed.Append(" access=").Append(channelInBed.AccessMode)
            sbInBed.Append(" reload=").Append(channelInBed.ReloadValue)
            sbInBed.Append(" count=").Append(CurrentCounterValue(channelInBed))
            sbInBed.Append(" phase=").Append(channelInBed.Phase)
            sbInBed.Append(" gate/out=").Append(channelInBed.Gate).Append("/").Append(channelInBed.Output)
            sbInBed.Append(" running/null=").Append(channelInBed.Running).Append("/").Append(channelInBed.NullCount).AppendLine()
        Next
        sbInBed.Append("  next IRQ0 wake (ps)    : ").Append(PicosecondsUntilNextWakeEvent()).AppendLine()
        sbInBed.Append("  fractional numerator  : ").Append(_timeNumeratorRemainder)
        Return sbInBed.ToString()
    End Function

    Public Function PicosecondsUntilNextWakeEvent() As Long Implements IClockWakeSource.PicosecondsUntilNextWakeEvent
        ' CROMWELL PIT REFRESH DECOUPLE BRICK 3A
        ' Only a PIT event which can asynchronously change processor execution
        ' belongs on the CPU wake-deadline path.  Counter 0 can assert IRQ0.
        '
        ' Counter 1 REFREQ is a motherboard-internal DRAM refresh request, not a
        ' CPU wake source.  It is still advanced by AdvanceTime, which counts every
        ' elapsed timer-1 wrap and raises every corresponding REFREQ when the
        ' current motherboard batch commits.  Guest I/O synchronizes pending time
        ' before observing PIT/refresh state, so deferring those internal pulses
        ' inside an otherwise unobservable batch does not skip guest time or
        ' fabricate hardware state.  The finished bus-arbitration layer will later
        ' place the individual refresh cycles back onto the physical bus timeline.
        '
        ' Counter 2 is likewise observed through port 61h and is synchronized by
        ' the bus before the read.
        Dim ticksUntilWake As Long = TicksUntilWakeEvent(0, _channels(0))
        If ticksUntilWake = Long.MaxValue Then Return Long.MaxValue
        Return PicosecondsForPitTicks(ticksUntilWake)
    End Function

    Private Shared Function TicksUntilWakeEvent(channelIndex As Integer,
                                                channel As Channel) As Long
        If channel.NullCount OrElse channel.ReloadValue <= 0 Then Return Long.MaxValue

        Select Case channel.Mode
            Case 0
                If channelIndex = 0 AndAlso channel.Running AndAlso channel.Gate AndAlso channel.Counter > 0 Then
                    Return channel.Counter
                End If

            Case 1
                If channelIndex = 0 AndAlso channel.Running AndAlso channel.Counter > 0 Then
                    Return channel.Counter
                End If

            Case 2, 3
                If channel.Running AndAlso channel.Gate AndAlso (channelIndex = 0 OrElse channelIndex = 1) Then
                    Dim untilWrap As Long = CLng(channel.ReloadValue) - channel.Phase
                    If untilWrap <= 0 Then untilWrap = channel.ReloadValue
                    Return untilWrap
                End If

            Case 4
                If channelIndex = 0 AndAlso channel.Running AndAlso channel.Gate Then
                    If channel.StrobeLowTicks > 0 Then Return channel.StrobeLowTicks
                    If channel.Counter > 0 Then Return CLng(channel.Counter) + 1L
                End If

            Case 5
                If channelIndex = 0 AndAlso channel.Running Then
                    If channel.StrobeLowTicks > 0 Then Return channel.StrobeLowTicks
                    If channel.Counter > 0 Then Return CLng(channel.Counter) + 1L
                End If
        End Select

        Return Long.MaxValue
    End Function

    Private Function PicosecondsForPitTicks(ticks As Long) As Long
        If ticks <= 0 Then Return 1
        Dim requiredNumerator As Long =
            ticks * MachineProfile286.PicosecondsPerSecond - _timeNumeratorRemainder
        If requiredNumerator <= 0 Then Return 1
        Return Math.Max(1L,
                        (requiredNumerator + MachineProfile286.PitInputClockHz - 1L) \
                        MachineProfile286.PitInputClockHz)
    End Function

    Public Sub AdvanceTime(elapsedPicoseconds As Long) Implements IClockedDevice.AdvanceTime
        If elapsedPicoseconds < 0 Then Throw New ArgumentOutOfRangeException(NameOf(elapsedPicoseconds))

        _timeNumeratorRemainder += elapsedPicoseconds * MachineProfile286.PitInputClockHz
        Dim pitTicks As Long = _timeNumeratorRemainder \ MachineProfile286.PicosecondsPerSecond
        _timeNumeratorRemainder = _timeNumeratorRemainder Mod MachineProfile286.PicosecondsPerSecond
        If pitTicks <= 0 Then Return

        For channelIndex As Integer = 0 To 2
            AdvanceChannel(channelIndex, pitTicks)
        Next
    End Sub

    Private Sub AdvanceChannel(channelIndex As Integer, ticks As Long)
        Dim channel As Channel = _channels(channelIndex)
        If channel.NullCount OrElse channel.ReloadValue <= 0 OrElse ticks <= 0 Then Return

        Select Case channel.Mode
            Case 0
                AdvanceMode0(channelIndex, channel, ticks)
            Case 1
                AdvanceMode1(channelIndex, channel, ticks)
            Case 2
                AdvancePeriodic(channelIndex, channel, ticks, squareWave:=False)
            Case 3
                AdvancePeriodic(channelIndex, channel, ticks, squareWave:=True)
            Case 4
                AdvanceStrobe(channelIndex, channel, ticks, hardwareTriggered:=False)
            Case 5
                AdvanceStrobe(channelIndex, channel, ticks, hardwareTriggered:=True)
        End Select
    End Sub

    Private Sub AdvanceMode0(channelIndex As Integer, channel As Channel, ticks As Long)
        If Not channel.Running OrElse Not channel.Gate OrElse channel.Counter <= 0 Then Return

        If ticks < channel.Counter Then
            channel.Counter -= CInt(ticks)
            Return
        End If

        channel.Counter = 0
        channel.Running = False
        SetOutput(channelIndex, channel, True)
    End Sub

    Private Sub AdvanceMode1(channelIndex As Integer, channel As Channel, ticks As Long)
        If Not channel.Running OrElse channel.Counter <= 0 Then Return

        If ticks < channel.Counter Then
            channel.Counter -= CInt(ticks)
            Return
        End If

        channel.Counter = 0
        channel.Running = False
        channel.AwaitingTrigger = True
        SetOutput(channelIndex, channel, True)
    End Sub

    Private Sub AdvancePeriodic(channelIndex As Integer,
                                channel As Channel,
                                ticks As Long,
                                squareWave As Boolean)
        If Not channel.Running OrElse Not channel.Gate Then Return

        Dim period As Long = channel.ReloadValue
        If period <= 0 Then Return

        Dim oldPhase As Long = channel.Phase
        Dim total As Long = oldPhase + ticks
        Dim wraps As Long = total \ period
        channel.Phase = total Mod period
        channel.Counter = CurrentCounterValue(channel)

        If channelIndex = 0 AndAlso wraps > 0 Then
            ' Each new period begins with OUT high after the preceding low phase,
            ' producing the edge that clocks the master 8259 IRQ0 input.
            For i As Long = 1 To wraps
                _pic.PulseIrq(0)
            Next
        End If

        If channelIndex = 1 AndAlso wraps > 0 Then
            ' Each completed timer-1 period is latched by the 82C211 as one
            ' refresh request.  REFREQ is not a synthetic DMA transfer on NEAT.
            For i As Long = 1 To wraps
                RaiseEvent RefreshRequest()
            Next
            If (wraps And 1L) <> 0 Then channel.RefreshToggle = Not channel.RefreshToggle
        End If

        If squareWave Then
            Dim highTicks As Long = (period + 1L) \ 2L
            channel.Output = channel.Phase < highTicks
        Else
            channel.Output = channel.Phase <> period - 1L
        End If
    End Sub

    Private Sub AdvanceStrobe(channelIndex As Integer,
                              channel As Channel,
                              ticks As Long,
                              hardwareTriggered As Boolean)
        If Not channel.Running OrElse channel.Counter <= 0 Then Return
        If Not hardwareTriggered AndAlso Not channel.Gate Then Return

        Dim remainingTicks As Long = ticks

        If channel.StrobeLowTicks > 0 Then
            If remainingTicks >= channel.StrobeLowTicks Then
                remainingTicks -= channel.StrobeLowTicks
                channel.StrobeLowTicks = 0
                channel.Running = False
                channel.AwaitingTrigger = hardwareTriggered
                SetOutput(channelIndex, channel, True)
            Else
                channel.StrobeLowTicks -= CInt(remainingTicks)
            End If
            Return
        End If

        If remainingTicks < channel.Counter Then
            channel.Counter -= CInt(remainingTicks)
            Return
        End If

        remainingTicks -= channel.Counter
        channel.Counter = 0
        SetOutput(channelIndex, channel, False)
        channel.StrobeLowTicks = 1

        If remainingTicks > 0 Then
            channel.StrobeLowTicks = 0
            channel.Running = False
            channel.AwaitingTrigger = hardwareTriggered
            SetOutput(channelIndex, channel, True)
        End If
    End Sub

    Private Sub SetOutput(channelIndex As Integer, channel As Channel, value As Boolean)
        Dim oldValue As Boolean = channel.Output
        channel.Output = value
        If channelIndex = 0 AndAlso value AndAlso Not oldValue Then _pic.PulseIrq(0)
    End Sub
End Class
