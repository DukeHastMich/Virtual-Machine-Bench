Imports System

' HT12 physical DRAM / ROM / shadow-memory worker for 80286-class boards.
'
' This worker owns only memory which the HT12 itself selects.  ISA/VGA/MMIO
' ownership remains on HardwareBus.  That distinction is important during the
' shadow load phase: selected C0000h-FFFFFh blocks accept RAM writes while reads
' still fall through to ROM or the ordinary option-ROM/device decode until the
' global Shadow Enable bit is asserted.
Public NotInheritable Class Ht12MemoryWorker286
    Private Const OneMegabyteInBed As UInteger = &H100000UI
    Private Const ConventionalRamEndInBed As UInteger = &HA0000UI
    Private Const ShadowBaseInBed As UInteger = &HC0000UI
    Private Const ShadowEndExclusiveInBed As UInteger = &H100000UI
    Private Const ShadowWindowSizeInBed As UInteger = &H4000UI
    Private Const SystemRomBaseInBed As UInteger = &HF0000UI
    Private Const SystemRomSizeInBed As Integer = &H10000
    Private Const HighSystemRomAliasBaseInBed As UInteger = &HFF0000UI

    Private ReadOnly _chipsetInBed As HeadlandHt12
    Private ReadOnly _dramInBed() As Byte
    Private ReadOnly _shadowRamInBed(&H3FFFF) As Byte
    Private ReadOnly _systemRomInBed(SystemRomSizeInBed - 1) As Byte
    Private _systemRomLoadedInBed As Boolean
    Private _keyboardA20GateInBed As Boolean
    Private _fastA20GateInBed As Boolean

    Public Sub New(chipsetInBed As HeadlandHt12, installedMemoryMegabytesInBed As Integer)
        If chipsetInBed Is Nothing Then Throw New ArgumentNullException(NameOf(chipsetInBed))
        If installedMemoryMegabytesInBed <> 1 AndAlso
           installedMemoryMegabytesInBed <> 2 AndAlso
           installedMemoryMegabytesInBed <> 4 Then
            Throw New ArgumentOutOfRangeException(NameOf(installedMemoryMegabytesInBed),
                                                  "FOX/HT12 physical population supports 1, 2 or 4 MiB in this worker.")
        End If

        _chipsetInBed = chipsetInBed
        ReDim _dramInBed(installedMemoryMegabytesInBed * 1024 * 1024 - 1)
    End Sub

    Public ReadOnly Property InstalledMemoryBytes As UInteger
        Get
            Return CUInt(_dramInBed.Length)
        End Get
    End Property

    Public ReadOnly Property InstalledMemoryMegabytes As Integer
        Get
            Return _dramInBed.Length \ (1024 * 1024)
        End Get
    End Property

    Public ReadOnly Property A20Enabled As Boolean
        Get
            ' PS/2-compatible alternate Gate A20 and the keyboard-controller A20
            ' source are logically ORed: either source may force A20 to propagate.
            Return _keyboardA20GateInBed OrElse _fastA20GateInBed
        End Get
    End Property

    Public Sub SetKeyboardA20Gate(enabledInBed As Boolean)
        _keyboardA20GateInBed = enabledInBed
    End Sub

    Public Sub SetFastA20Gate(enabledInBed As Boolean)
        _fastA20GateInBed = enabledInBed
    End Sub

    Public Sub LoadSystemRom(dataInBed As Byte())
        If dataInBed Is Nothing OrElse dataInBed.Length <> SystemRomSizeInBed Then
            Throw New ArgumentException("FOX II paired 27256 lanes form exactly one 64 KiB CPU-linear BIOS image.", NameOf(dataInBed))
        End If
        Array.Copy(dataInBed, _systemRomInBed, SystemRomSizeInBed)
        _systemRomLoadedInBed = True
    End Sub

    ' Physical FOX II ROM sockets are even/odd byte lanes.  Keeping lane assembly
    ' here makes the board decoder testable without flattening the hardware model.
    Public Sub LoadSystemRomLanes(evenLaneInBed As Byte(), oddLaneInBed As Byte())
        If evenLaneInBed Is Nothing OrElse oddLaneInBed Is Nothing OrElse
           evenLaneInBed.Length <> &H8000 OrElse oddLaneInBed.Length <> &H8000 Then
            Throw New ArgumentException("Each FOX II 27256 BIOS lane must contain exactly 32768 bytes.")
        End If

        For wordIndexInBed As Integer = 0 To &H7FFF
            _systemRomInBed(wordIndexInBed * 2) = evenLaneInBed(wordIndexInBed)
            _systemRomInBed(wordIndexInBed * 2 + 1) = oddLaneInBed(wordIndexInBed)
        Next
        _systemRomLoadedInBed = True
    End Sub

    Public Function NormalizePhysicalAddress(addressInBed As UInteger) As UInteger
        Dim addressInBedNormalized As UInteger = addressInBed And &HFFFFFFUI

        ' AT-class 286 reset decode aliases the top 64 KiB of the 24-bit space to
        ' the system-ROM window, so the FFFFF0h reset fetch reaches FFFF0h ROM.
        If addressInBedNormalized >= HighSystemRomAliasBaseInBed Then
            Return SystemRomBaseInBed + (addressInBedNormalized - HighSystemRomAliasBaseInBed)
        End If

        If Not A20Enabled Then addressInBedNormalized = addressInBedNormalized And &HEFFFFFUI
        Return addressInBedNormalized
    End Function

    Public Function TryReadProcessorByte(addressInBed As UInteger,
                                         ByRef valueInBed As Byte,
                                         ByRef targetInBed As AtMemoryCycleTarget286) As Boolean
        Dim normalizedInBed As UInteger = NormalizePhysicalAddress(addressInBed)

        Dim emsPhysicalInBed As UInteger
        If _chipsetInBed.TryTranslateEmsAddress(normalizedInBed, emsPhysicalInBed) AndAlso
           emsPhysicalInBed < InstalledMemoryBytes Then
            valueInBed = _dramInBed(CInt(emsPhysicalInBed))
            targetInBed = AtMemoryCycleTarget286.LocalDram
            Return True
        End If

        If IsShadowSelectedInBed(normalizedInBed) AndAlso ShadowGloballyEnabledInBed() Then
            valueInBed = _shadowRamInBed(CInt(normalizedInBed - ShadowBaseInBed))
            targetInBed = AtMemoryCycleTarget286.LocalDram
            Return True
        End If

        If normalizedInBed >= SystemRomBaseInBed AndAlso normalizedInBed < ShadowEndExclusiveInBed Then
            valueInBed = If(_systemRomLoadedInBed,
                            _systemRomInBed(CInt(normalizedInBed - SystemRomBaseInBed)),
                            CByte(&HFF))
            targetInBed = AtMemoryCycleTarget286.SystemRom
            Return True
        End If

        If IsDirectDramAddressInBed(normalizedInBed) Then
            valueInBed = _dramInBed(CInt(normalizedInBed))
            targetInBed = AtMemoryCycleTarget286.LocalDram
            Return True
        End If

        targetInBed = AtMemoryCycleTarget286.MappedDevice
        Return False
    End Function

    Public Function TryWriteProcessorByte(addressInBed As UInteger,
                                          valueInBed As Byte,
                                          ByRef targetInBed As AtMemoryCycleTarget286) As Boolean
        Dim normalizedInBed As UInteger = NormalizePhysicalAddress(addressInBed)

        Dim emsPhysicalInBed As UInteger
        If _chipsetInBed.TryTranslateEmsAddress(normalizedInBed, emsPhysicalInBed) AndAlso
           emsPhysicalInBed < InstalledMemoryBytes Then
            _dramInBed(CInt(emsPhysicalInBed)) = valueInBed
            targetInBed = AtMemoryCycleTarget286.LocalDram
            Return True
        End If

        If IsShadowSelectedInBed(normalizedInBed) Then
            targetInBed = AtMemoryCycleTarget286.LocalDram
            If Not ShadowGloballyEnabledInBed() Then
                ' Datasheet load phase: selected shadow RAM is writable while
                ' ordinary reads continue to the underlying ROM/device decode.
                _shadowRamInBed(CInt(normalizedInBed - ShadowBaseInBed)) = valueInBed
            End If
            ' After global enable the selected shadow block is read-only.
            Return True
        End If

        If normalizedInBed >= SystemRomBaseInBed AndAlso normalizedInBed < ShadowEndExclusiveInBed Then
            targetInBed = AtMemoryCycleTarget286.SystemRom
            Return True ' selected ROM ignores writes
        End If

        If IsDirectDramAddressInBed(normalizedInBed) Then
            _dramInBed(CInt(normalizedInBed)) = valueInBed
            targetInBed = AtMemoryCycleTarget286.LocalDram
            Return True
        End If

        targetInBed = AtMemoryCycleTarget286.MappedDevice
        Return False
    End Function

    Public Function ReadDmaByte(addressInBed As UInteger) As Byte
        ' CPU Gate-A20 and reset-ROM aliasing are CPU-address-decode behavior.
        ' The AT DMA page/address registers present their own 24-bit physical
        ' address and must not be folded through the processor A20 gate.
        Dim normalizedInBed As UInteger = addressInBed And &HFFFFFFUI
        If normalizedInBed < InstalledMemoryBytes AndAlso
           (normalizedInBed < ConventionalRamEndInBed OrElse normalizedInBed >= OneMegabyteInBed) Then
            Return _dramInBed(CInt(normalizedInBed))
        End If
        Return &HFF
    End Function

    Public Sub WriteDmaByte(addressInBed As UInteger, valueInBed As Byte)
        Dim normalizedInBed As UInteger = addressInBed And &HFFFFFFUI
        If normalizedInBed < InstalledMemoryBytes AndAlso
           (normalizedInBed < ConventionalRamEndInBed OrElse normalizedInBed >= OneMegabyteInBed) Then
            _dramInBed(CInt(normalizedInBed)) = valueInBed
        End If
    End Sub

    Public Function ReadDmaWord(addressInBed As UInteger) As UInt16
        Return CUShort(CUShort(ReadDmaByte(addressInBed)) Or
                       (CUShort(ReadDmaByte((addressInBed + 1UI) And &HFFFFFFUI)) << 8))
    End Function

    Public Sub WriteDmaWord(addressInBed As UInteger, valueInBed As UInt16)
        WriteDmaByte(addressInBed, CByte(valueInBed And &HFFUS))
        WriteDmaByte((addressInBed + 1UI) And &HFFFFFFUI, CByte(valueInBed >> 8))
    End Sub

    Public Function DiagnosticText() As String
        Return "HT12 physical memory       : " & InstalledMemoryMegabytes.ToString() & " MiB" & Environment.NewLine &
               "  A20 keyboard / fast / out: " & If(_keyboardA20GateInBed, "1", "0") & " / " &
                                               If(_fastA20GateInBed, "1", "0") & " / " &
                                               If(A20Enabled, "1", "0") & Environment.NewLine &
               "  BIOS ROM loaded          : " & If(_systemRomLoadedInBed, "yes — paired 27256/64 KiB", "no") & Environment.NewLine &
               "  shadow select 12h/13h    : " & _chipsetInBed.Configuration.ReadRegister(&H12).ToString("X2") & " / " &
                                               _chipsetInBed.Configuration.ReadRegister(&H13).ToString("X2") & Environment.NewLine &
               "  shadow global 14h.1      : " & If(ShadowGloballyEnabledInBed(), "1 — read-only", "0 — selected blocks load/write-only")
    End Function

    Private Function IsDirectDramAddressInBed(addressInBed As UInteger) As Boolean
        If addressInBed >= InstalledMemoryBytes Then Return False

        If addressInBed < ConventionalRamEndInBed Then
            ' 14h bit 3 gates 40000h-9FFFFh.  The first 256 KiB remains decoded.
            If addressInBed >= &H40000UI AndAlso
               (_chipsetInBed.Configuration.ReadRegister(&H14) And &H8) = 0 Then Return False
            Return True
        End If

        If addressInBed < OneMegabyteInBed Then Return False

        ' Register 18h bits 5:0 are the last ordinary 64 KiB physical block.
        ' Therefore reset 3Fh means 000000h-3FFFFFh (4 MiB exclusive top), not
        ' 1 MiB + 3Fh blocks.  BIOS may lower this boundary to reserve physical
        ' DRAM above it for the HT12 EMS mapper.
        Dim lastOrdinaryBlockInBed As UInteger = CUInt(_chipsetInBed.Configuration.ReadRegister(&H18) And &H3F)
        Dim topOrdinaryExclusiveInBed As UInteger = (lastOrdinaryBlockInBed + 1UI) * &H10000UI
        Return addressInBed < Math.Min(InstalledMemoryBytes, topOrdinaryExclusiveInBed)
    End Function

    Private Function IsShadowSelectedInBed(addressInBed As UInteger) As Boolean
        If addressInBed < ShadowBaseInBed OrElse addressInBed >= ShadowEndExclusiveInBed Then Return False
        Dim windowInBed As Integer = CInt((addressInBed - ShadowBaseInBed) \ ShadowWindowSizeInBed)
        If windowInBed < 8 Then
            Return (_chipsetInBed.Configuration.ReadRegister(&H12) And (1 << windowInBed)) <> 0
        End If
        Return (_chipsetInBed.Configuration.ReadRegister(&H13) And (1 << (windowInBed - 8))) <> 0
    End Function

    Private Function ShadowGloballyEnabledInBed() As Boolean
        Return (_chipsetInBed.Configuration.ReadRegister(&H14) And &H2) <> 0
    End Function
End Class

' Processor-family-neutral local-bus fabric for HT12 boards.  It is usable for
' construction/validation before the Harris-specialized Processor286 class is
' generalized to consume IProcessorLocalBusFabric directly.
Public NotInheritable Class Ht12ProcessorLocalBusFabric
    Implements IProcessorLocalBusFabric, IProcessorFabricCapabilities

    Private ReadOnly _busInBed As HardwareBus
    Private ReadOnly _memoryInBed As Ht12MemoryWorker286
    Private ReadOnly _externalClockHzInBed As Long
    Private _currentExternalClockInBed As ULong
    Private _timeNumeratorRemainderInBed As Long
    Private _dma8HoldRequestedInBed As Boolean
    Private _dma16HoldRequestedInBed As Boolean
    Private _pendingRefreshRequestsInBed As ULong

    ' These are real motherboard request lines/latches.  The current Processor286
    ' binding cannot consume them yet, so the fabric publishes them rather than
    ' pretending DMA/refresh ownership was granted behind the CPU's back.
    Public Event HoldRequestChanged(assertedInBed As Boolean)
    Public Event RefreshRequestLatched(pendingCountInBed As ULong)

    Public Sub New(busInBed As HardwareBus,
                   memoryInBed As Ht12MemoryWorker286,
                   externalClockHzInBed As Long)
        If busInBed Is Nothing Then Throw New ArgumentNullException(NameOf(busInBed))
        If memoryInBed Is Nothing Then Throw New ArgumentNullException(NameOf(memoryInBed))
        If externalClockHzInBed <= 0 Then Throw New ArgumentOutOfRangeException(NameOf(externalClockHzInBed))
        _busInBed = busInBed
        _memoryInBed = memoryInBed
        _externalClockHzInBed = externalClockHzInBed
    End Sub

    Public ReadOnly Property FabricIdentity As String Implements IProcessorLocalBusFabric.FabricIdentity
        Get
            Return "Headland HT12/A 80286 local bus / AT fabric"
        End Get
    End Property

    Public ReadOnly Property ExternalClockHz As Long Implements IProcessorLocalBusFabric.ExternalClockHz
        Get
            Return _externalClockHzInBed
        End Get
    End Property

    Public ReadOnly Property CurrentExternalClock As ULong Implements IProcessorLocalBusFabric.CurrentExternalClock
        Get
            Return _currentExternalClockInBed
        End Get
    End Property

    Public Function ExecuteProcessorCycle(requestInBed As ProcessorBusCycleRequest) As ProcessorBusCycleResult Implements IProcessorLocalBusFabric.ExecuteProcessorCycle
        Dim readDataInBed As UInteger = 0UI
        Select Case requestInBed.Kind
            Case ProcessorBusCycleKind.MemoryRead
                readDataInBed = ReadMemoryInBed(requestInBed.Address, requestInBed.Width)
            Case ProcessorBusCycleKind.MemoryWrite
                WriteMemoryInBed(requestInBed.Address, requestInBed.Width, requestInBed.WriteData)
            Case ProcessorBusCycleKind.IoRead
                Select Case requestInBed.Width
                    Case ProcessorBusDataWidth.Width8
                        readDataInBed = _busInBed.ReadByte(requestInBed.Port)
                    Case ProcessorBusDataWidth.Width16
                        readDataInBed = _busInBed.ReadWord(requestInBed.Port)
                    Case Else
                        Throw New NotSupportedException("HT12/80286 fabric has no native 32-bit I/O transfer.")
                End Select
            Case ProcessorBusCycleKind.IoWrite
                Select Case requestInBed.Width
                    Case ProcessorBusDataWidth.Width8
                        _busInBed.WriteByte(requestInBed.Port, CByte(requestInBed.WriteData And &HFFUI))
                    Case ProcessorBusDataWidth.Width16
                        _busInBed.WriteWord(requestInBed.Port, CUShort(requestInBed.WriteData And &HFFFFUI))
                    Case Else
                        Throw New NotSupportedException("HT12/80286 fabric has no native 32-bit I/O transfer.")
                End Select
            Case Else
                Throw New NotSupportedException("HT12 processor binding has not yet attached INTA/HOLD/special-cycle ownership.")
        End Select

        ' The exact 80286 READY-state binding remains a processor-integration step.
        ' Construction validation still advances one physical bus clock per
        ' completed transaction so clocked board devices share one timeline.
        Dim resultInBed As ProcessorBusCycleResult =
            ProcessorBusCycleResult.Completed(requestInBed.Width, 1, readDataInBed)
        AdvanceExternalClocks(resultInBed.ExternalClocksConsumed)
        Return resultInBed
    End Function

    Public Sub AdvanceExternalClocks(externalClocksInBed As Long) Implements IProcessorLocalBusFabric.AdvanceExternalClocks
        If externalClocksInBed < 0 Then Throw New ArgumentOutOfRangeException(NameOf(externalClocksInBed))
        If externalClocksInBed = 0 Then Return
        _currentExternalClockInBed += CULng(externalClocksInBed)

        Dim numeratorInBed As Decimal = CDec(externalClocksInBed) * 1000000000000D + CDec(_timeNumeratorRemainderInBed)
        Dim picosecondsInBed As Long = CLng(Math.Truncate(numeratorInBed / CDec(_externalClockHzInBed)))
        _timeNumeratorRemainderInBed = CLng(numeratorInBed Mod CDec(_externalClockHzInBed))
        If picosecondsInBed > 0 Then _busInBed.AdvanceTime(picosecondsInBed)
    End Sub

    Public ReadOnly Property HoldRequestAsserted As Boolean
        Get
            Return _dma8HoldRequestedInBed OrElse _dma16HoldRequestedInBed OrElse _pendingRefreshRequestsInBed <> 0UL
        End Get
    End Property

    Public ReadOnly Property PendingRefreshRequests As ULong
        Get
            Return _pendingRefreshRequestsInBed
        End Get
    End Property

    Public Sub SetDmaHoldRequest(masterInBed As AtBusMaster286, assertedInBed As Boolean)
        Dim oldAggregateInBed As Boolean = HoldRequestAsserted
        Select Case masterInBed
            Case AtBusMaster286.Dma8
                _dma8HoldRequestedInBed = assertedInBed
            Case AtBusMaster286.Dma16
                _dma16HoldRequestedInBed = assertedInBed
            Case Else
                Throw New ArgumentOutOfRangeException(NameOf(masterInBed), "Only the DMA8/DMA16 HRQ sources may use SetDmaHoldRequest.")
        End Select
        Dim newAggregateInBed As Boolean = HoldRequestAsserted
        If newAggregateInBed <> oldAggregateInBed Then RaiseEvent HoldRequestChanged(newAggregateInBed)
    End Sub

    Public Sub LatchRefreshRequest()
        Dim oldAggregateInBed As Boolean = HoldRequestAsserted
        If _pendingRefreshRequestsInBed = ULong.MaxValue Then
            Throw New InvalidOperationException("HT12 refresh-request latch overflowed.")
        End If
        _pendingRefreshRequestsInBed += 1UL
        If HoldRequestAsserted <> oldAggregateInBed Then RaiseEvent HoldRequestChanged(True)
        RaiseEvent RefreshRequestLatched(_pendingRefreshRequestsInBed)
    End Sub

    ' Future 80286 binding calls this only after the chipset has actually won the
    ' bus and emitted the refresh cycle.  Keeping completion explicit prevents a
    ' timer tick from becoming a magical host-side DRAM refresh.
    Public Sub CompleteOneRefreshRequest()
        If _pendingRefreshRequestsInBed = 0UL Then Return
        Dim oldAggregateInBed As Boolean = HoldRequestAsserted
        _pendingRefreshRequestsInBed -= 1UL
        If HoldRequestAsserted <> oldAggregateInBed Then RaiseEvent HoldRequestChanged(False)
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

    Private Function ReadMemoryInBed(addressInBed As UInteger, widthInBed As ProcessorBusDataWidth) As UInteger
        Select Case widthInBed
            Case ProcessorBusDataWidth.Width8
                Return ReadMemoryByteInBed(addressInBed)
            Case ProcessorBusDataWidth.Width16
                Return CUInt(ReadMemoryByteInBed(addressInBed)) Or
                       (CUInt(ReadMemoryByteInBed((addressInBed + 1UI) And &HFFFFFFUI)) << 8)
            Case Else
                Throw New NotSupportedException("HT12/80286 fabric has no native 32-bit memory transfer.")
        End Select
    End Function

    Private Sub WriteMemoryInBed(addressInBed As UInteger,
                                 widthInBed As ProcessorBusDataWidth,
                                 valueInBed As UInteger)
        Select Case widthInBed
            Case ProcessorBusDataWidth.Width8
                WriteMemoryByteInBed(addressInBed, CByte(valueInBed And &HFFUI))
            Case ProcessorBusDataWidth.Width16
                WriteMemoryByteInBed(addressInBed, CByte(valueInBed And &HFFUI))
                WriteMemoryByteInBed((addressInBed + 1UI) And &HFFFFFFUI, CByte((valueInBed >> 8) And &HFFUI))
            Case Else
                Throw New NotSupportedException("HT12/80286 fabric has no native 32-bit memory transfer.")
        End Select
    End Sub

    Private Function ReadMemoryByteInBed(addressInBed As UInteger) As Byte
        Dim valueInBed As Byte
        Dim targetInBed As AtMemoryCycleTarget286
        If _memoryInBed.TryReadProcessorByte(addressInBed, valueInBed, targetInBed) Then Return valueInBed
        If _busInBed.TryReadMemoryByte(_memoryInBed.NormalizePhysicalAddress(addressInBed), valueInBed, targetInBed) Then Return valueInBed
        Return &HFF
    End Function

    Private Sub WriteMemoryByteInBed(addressInBed As UInteger, valueInBed As Byte)
        Dim targetInBed As AtMemoryCycleTarget286
        If _memoryInBed.TryWriteProcessorByte(addressInBed, valueInBed, targetInBed) Then Return
        _busInBed.TryWriteMemoryByte(_memoryInBed.NormalizePhysicalAddress(addressInBed), valueInBed, targetInBed)
    End Sub
End Class
