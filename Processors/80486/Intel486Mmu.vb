Imports System

Public Enum Intel486SegmentRegister As Byte
    ES = 0
    CS = 1
    SS = 2
    DS = 3
    FS = 4
    GS = 5
End Enum

Public Enum Intel486MemoryAccessKind As Byte
    Read = 0
    Write = 1
    Execute = 2
End Enum

' Hidden portion of one Intel486 segment register.  CP04 owns the storage and
' fundamental limit/base semantics; CP05 fills the complete descriptor/gate loading
' and privilege-transition machinery around it.
Public Structure Intel486SegmentCacheEntry
    Public Selector As UInt16
    Public BaseAddress As UInteger
    Public Limit As UInteger
    Public AccessRights As UInt16
    Public Present As Boolean
    Public Default32 As Boolean
    Public PageGranularity As Boolean
    Public ExpandDown As Boolean
    Public Readable As Boolean
    Public Writable As Boolean
    Public Executable As Boolean
    Public DescriptorPrivilegeLevel As Byte
End Structure

Public NotInheritable Class Intel486PageFaultException
    Inherits Exception

    Public ReadOnly Property LinearAddress As UInteger
    Public ReadOnly Property ErrorCode As UInt16

    Public Sub New(linearAddressInBed As UInteger, errorCodeInBed As UInt16, messageInBed As String)
        MyBase.New(messageInBed)
        LinearAddress = linearAddressInBed
        ErrorCode = errorCodeInBed
    End Sub
End Class

Public Structure Intel486PhysicalTranslation
    Public PhysicalAddress As UInteger
    Public PageWriteThrough As Boolean
    Public PageCacheDisable As Boolean
    Public TlbHit As Boolean
End Structure

Friend Structure Intel486TlbEntry
    Public Valid As Boolean
    Public Tag17 As UInteger
    Public PhysicalPageBase As UInteger
    Public UserAccessible As Boolean
    Public Writable As Boolean
    Public Dirty As Boolean
    Public PageWriteThrough As Boolean
    Public PageCacheDisable As Boolean
    Public PageTableEntryPhysicalAddress As UInteger
End Structure

' Intel486 segmentation + paging unit for the DX/DX2 family.
' - six visible selectors with hidden descriptor caches;
' - CR0/CR2/CR3 semantics needed by 486 paging/cache control;
' - two-level 4 KiB page translation;
' - 32-entry, four-way, eight-set TLB using Intel's documented three-bit pseudo-LRU;
' - locked Accessed/Dirty updates through the real processor-bus contract;
' - A20M# applied at the physical-address pin boundary.
Public NotInheritable Class Intel486Mmu
    Private Const Cr0Pe As UInteger = &H1UI
    Private Const Cr0Mp As UInteger = &H2UI
    Private Const Cr0Em As UInteger = &H4UI
    Private Const Cr0Ts As UInteger = &H8UI
    Private Const Cr0Et As UInteger = &H10UI
    Private Const Cr0Ne As UInteger = &H20UI
    Private Const Cr0Wp As UInteger = &H10000UI
    Private Const Cr0Am As UInteger = &H40000UI
    Private Const Cr0Nw As UInteger = &H20000000UI
    Private Const Cr0Cd As UInteger = &H40000000UI
    Private Const Cr0Pg As UInteger = &H80000000UI

    Private ReadOnly _segmentsInBed(5) As Intel486SegmentCacheEntry
    Private ReadOnly _tlbInBed(7, 3) As Intel486TlbEntry
    Private ReadOnly _tlbLruInBed(7) As Byte

    Private _cr0InBed As UInteger
    Private _cr2InBed As UInteger
    Private _cr3InBed As UInteger
    Private _a20MaskAssertedInBed As Boolean

    Public Sub New()
        Reset()
    End Sub

    Public ReadOnly Property CR0 As UInteger
        Get
            Return _cr0InBed
        End Get
    End Property

    Public ReadOnly Property CR2 As UInteger
        Get
            Return _cr2InBed
        End Get
    End Property

    Public ReadOnly Property CR3 As UInteger
        Get
            Return _cr3InBed
        End Get
    End Property

    Public ReadOnly Property PagingEnabled As Boolean
        Get
            Return (_cr0InBed And Cr0Pg) <> 0UI
        End Get
    End Property

    Public ReadOnly Property ProtectedModeEnabled As Boolean
        Get
            Return (_cr0InBed And Cr0Pe) <> 0UI
        End Get
    End Property

    Public ReadOnly Property AlignmentMaskEnabled As Boolean
        Get
            Return (_cr0InBed And Cr0Am) <> 0UI
        End Get
    End Property

    Public ReadOnly Property SupervisorWriteProtectEnabled As Boolean
        Get
            Return (_cr0InBed And Cr0Wp) <> 0UI
        End Get
    End Property

    Public ReadOnly Property CacheDisabled As Boolean
        Get
            Return (_cr0InBed And Cr0Cd) <> 0UI
        End Get
    End Property

    Public ReadOnly Property NotWriteThrough As Boolean
        Get
            Return (_cr0InBed And Cr0Nw) <> 0UI
        End Get
    End Property

    Public Sub Reset()
        _cr0InBed = &H60000010UI
        _cr2InBed = 0UI
        _cr3InBed = 0UI
        FlushTlb()

        InstallResetRealModeSegment(Intel486SegmentRegister.CS, &HF000US, &HFFFF0000UI, True)
        InstallResetRealModeSegment(Intel486SegmentRegister.DS, 0US, 0UI, False)
        InstallResetRealModeSegment(Intel486SegmentRegister.SS, 0US, 0UI, False)
        InstallResetRealModeSegment(Intel486SegmentRegister.ES, 0US, 0UI, False)
        InstallResetRealModeSegment(Intel486SegmentRegister.FS, 0US, 0UI, False)
        InstallResetRealModeSegment(Intel486SegmentRegister.GS, 0US, 0UI, False)
    End Sub

    Private Sub InstallResetRealModeSegment(registerInBed As Intel486SegmentRegister,
                                            selectorInBed As UInt16,
                                            baseInBed As UInteger,
                                            executableInBed As Boolean)
        Dim entryInBed As New Intel486SegmentCacheEntry With {
            .Selector = selectorInBed,
            .BaseAddress = baseInBed,
            .Limit = &HFFFFUI,
            .AccessRights = CUShort(If(executableInBed, &H9B, &H93)),
            .Present = True,
            .Default32 = False,
            .PageGranularity = False,
            .ExpandDown = False,
            .Readable = True,
            .Writable = Not executableInBed,
            .Executable = executableInBed,
            .DescriptorPrivilegeLevel = 0
        }
        _segmentsInBed(CInt(registerInBed)) = entryInBed
    End Sub

    Public Function GetSegment(registerInBed As Intel486SegmentRegister) As Intel486SegmentCacheEntry
        Return _segmentsInBed(CInt(registerInBed))
    End Function

    Public Sub LoadRealModeSegment(registerInBed As Intel486SegmentRegister, selectorInBed As UInt16)
        Dim executableInBed As Boolean = registerInBed = Intel486SegmentRegister.CS
        InstallResetRealModeSegment(registerInBed, selectorInBed, CUInt(selectorInBed) << 4, executableInBed)
    End Sub

    Public Sub InstallProtectedModeSegment(registerInBed As Intel486SegmentRegister,
                                           entryInBed As Intel486SegmentCacheEntry)
        _segmentsInBed(CInt(registerInBed)) = entryInBed
    End Sub

    Public Function SegmentToLinear(registerInBed As Intel486SegmentRegister,
                                    offsetInBed As UInteger,
                                    lengthInBed As UInteger,
                                    accessInBed As Intel486MemoryAccessKind) As UInteger
        Dim segmentInBed As Intel486SegmentCacheEntry = _segmentsInBed(CInt(registerInBed))
        If Not segmentInBed.Present Then Throw New InvalidOperationException("Segment is not present.")

        Dim lastOffsetInBed As ULong = CULng(offsetInBed)
        If lengthInBed > 0UI Then lastOffsetInBed += CULng(lengthInBed) - 1UL
        If Not segmentInBed.ExpandDown Then
            If lastOffsetInBed > CULng(segmentInBed.Limit) Then Throw New InvalidOperationException("Segment limit exceeded.")
        Else
            Dim maximumInBed As ULong = If(segmentInBed.Default32, &HFFFFFFFFUL, &HFFFFUL)
            If CULng(offsetInBed) <= CULng(segmentInBed.Limit) OrElse lastOffsetInBed > maximumInBed Then
                Throw New InvalidOperationException("Expand-down segment limit exceeded.")
            End If
        End If

        If accessInBed = Intel486MemoryAccessKind.Execute AndAlso Not segmentInBed.Executable Then
            Throw New InvalidOperationException("Execution attempted through a non-code segment.")
        End If
        If accessInBed = Intel486MemoryAccessKind.Write AndAlso Not segmentInBed.Writable Then
            Throw New InvalidOperationException("Write attempted through a non-writable segment.")
        End If
        If accessInBed = Intel486MemoryAccessKind.Read AndAlso segmentInBed.Executable AndAlso Not segmentInBed.Readable Then
            Throw New InvalidOperationException("Read attempted through an execute-only segment.")
        End If

        Return CUInt((CULng(segmentInBed.BaseAddress) + CULng(offsetInBed)) And &HFFFFFFFFUL)
    End Function

    Public Sub SetA20Mask(assertedLowInBed As Boolean)
        _a20MaskAssertedInBed = assertedLowInBed
    End Sub

    Public Function ApplyA20Mask(physicalAddressInBed As UInteger) As UInteger
        If Not _a20MaskAssertedInBed Then Return physicalAddressInBed
        Return physicalAddressInBed And &HFFEFFFFFUI
    End Function

    Public Sub WriteCR0(valueInBed As UInteger)
        ' Intel486 forbids CD=0,NW=1. Paging also requires protected mode.
        If (valueInBed And Cr0Cd) = 0UI AndAlso (valueInBed And Cr0Nw) <> 0UI Then
            Throw New InvalidOperationException("#GP: CR0.NW=1 while CR0.CD=0 is invalid on Intel486.")
        End If
        If (valueInBed And Cr0Pg) <> 0UI AndAlso (valueInBed And Cr0Pe) = 0UI Then
            Throw New InvalidOperationException("#GP: Intel486 paging cannot be enabled while PE=0.")
        End If
        _cr0InBed = valueInBed Or Cr0Et
    End Sub

    ' MOV CR2,r32 is architecturally permitted; page faults overwrite CR2 with the
    ' faulting linear address.  This setter is kept inside the on-die MMU rather than
    ' exposing backing fields to the execution engine.
    Public Sub SetCR2ForSoftware(valueInBed As UInteger)
        _cr2InBed = valueInBed
    End Sub

    Public Sub WriteCR3(valueInBed As UInteger)
        ' Bits 11:5 and 2:0 are reserved for this generation. PWT/PCD (4/3) are retained.
        _cr3InBed = valueInBed And &HFFFFF018UI
        FlushTlb()
    End Sub

    Public Sub FlushTlb()
        For setInBed As Integer = 0 To 7
            For wayInBed As Integer = 0 To 3
                _tlbInBed(setInBed, wayInBed) = New Intel486TlbEntry()
            Next
            _tlbLruInBed(setInBed) = 0
        Next
    End Sub

    Public Sub InvalidatePage(linearAddressInBed As UInteger)
        Dim pageInBed As UInteger = linearAddressInBed >> 12
        Dim setInBed As Integer = CInt(pageInBed And 7UI)
        Dim tagInBed As UInteger = pageInBed >> 3
        For wayInBed As Integer = 0 To 3
            If _tlbInBed(setInBed, wayInBed).Valid AndAlso _tlbInBed(setInBed, wayInBed).Tag17 = tagInBed Then
                _tlbInBed(setInBed, wayInBed) = New Intel486TlbEntry()
            End If
        Next
    End Sub

    Public Function TranslateLinearAddress(linearAddressInBed As UInteger,
                                           accessInBed As Intel486MemoryAccessKind,
                                           cplInBed As Byte,
                                           fabricInBed As IProcessorLocalBusFabric) As Intel486PhysicalTranslation
        If fabricInBed Is Nothing Then Throw New ArgumentNullException(NameOf(fabricInBed))
        If Not PagingEnabled Then
            Return New Intel486PhysicalTranslation With {
                .PhysicalAddress = ApplyA20Mask(linearAddressInBed),
                .PageWriteThrough = False,
                .PageCacheDisable = False,
                .TlbHit = False
            }
        End If

        Dim userAccessInBed As Boolean = cplInBed = 3
        Dim writeAccessInBed As Boolean = accessInBed = Intel486MemoryAccessKind.Write
        Dim pageInBed As UInteger = linearAddressInBed >> 12
        Dim setInBed As Integer = CInt(pageInBed And 7UI)
        Dim tagInBed As UInteger = pageInBed >> 3

        For wayInBed As Integer = 0 To 3
            Dim entryInBed As Intel486TlbEntry = _tlbInBed(setInBed, wayInBed)
            If entryInBed.Valid AndAlso entryInBed.Tag17 = tagInBed Then
                EnforcePageProtection(linearAddressInBed, userAccessInBed, writeAccessInBed,
                                      entryInBed.UserAccessible, entryInBed.Writable, True)
                If writeAccessInBed AndAlso Not entryInBed.Dirty Then
                    entryInBed = MarkTlbEntryDirty(linearAddressInBed, userAccessInBed, entryInBed, fabricInBed)
                    _tlbInBed(setInBed, wayInBed) = entryInBed
                End If
                TouchTlbWay(setInBed, wayInBed)
                Return New Intel486PhysicalTranslation With {
                    .PhysicalAddress = ApplyA20Mask(entryInBed.PhysicalPageBase Or (linearAddressInBed And &HFFFUI)),
                    .PageWriteThrough = entryInBed.PageWriteThrough,
                    .PageCacheDisable = entryInBed.PageCacheDisable,
                    .TlbHit = True
                }
            End If
        Next

        Return WalkPageTables(linearAddressInBed, accessInBed, userAccessInBed, writeAccessInBed, fabricInBed)
    End Function

    Private Function WalkPageTables(linearAddressInBed As UInteger,
                                    accessInBed As Intel486MemoryAccessKind,
                                    userAccessInBed As Boolean,
                                    writeAccessInBed As Boolean,
                                    fabricInBed As IProcessorLocalBusFabric) As Intel486PhysicalTranslation
        Dim directoryIndexInBed As UInteger = (linearAddressInBed >> 22) And &H3FFUI
        Dim tableIndexInBed As UInteger = (linearAddressInBed >> 12) And &H3FFUI
        Dim cr3BaseInBed As UInteger = _cr3InBed And &HFFFFF000UI
        Dim pdeAddressInBed As UInteger = cr3BaseInBed + directoryIndexInBed * 4UI
        Dim cr3PwtInBed As Boolean = (_cr3InBed And &H8UI) <> 0UI
        Dim cr3PcdInBed As Boolean = (_cr3InBed And &H10UI) <> 0UI

        Dim pdeInBed As UInteger = ReadPagingEntry(pdeAddressInBed, cr3PwtInBed, cr3PcdInBed, fabricInBed)
        If (pdeInBed And 1UI) = 0UI Then ThrowPageFault(linearAddressInBed, userAccessInBed, writeAccessInBed, False, "page directory entry not present")

        Dim pdeUserInBed As Boolean = (pdeInBed And &H4UI) <> 0UI
        Dim pdeWritableInBed As Boolean = (pdeInBed And &H2UI) <> 0UI
        Dim pdePwtInBed As Boolean = (pdeInBed And &H8UI) <> 0UI
        Dim pdePcdInBed As Boolean = (pdeInBed And &H10UI) <> 0UI

        If (pdeInBed And &H20UI) = 0UI Then
            pdeInBed = SetPagingEntryBitsLocked(pdeAddressInBed, &H20UI, cr3PwtInBed, cr3PcdInBed, fabricInBed)
            If (pdeInBed And 1UI) = 0UI Then ThrowPageFault(linearAddressInBed, userAccessInBed, writeAccessInBed, False, "page directory entry became not-present during Accessed update")
            pdeUserInBed = (pdeInBed And &H4UI) <> 0UI
            pdeWritableInBed = (pdeInBed And &H2UI) <> 0UI
            pdePwtInBed = (pdeInBed And &H8UI) <> 0UI
            pdePcdInBed = (pdeInBed And &H10UI) <> 0UI
        End If

        Dim pteAddressInBed As UInteger = (pdeInBed And &HFFFFF000UI) + tableIndexInBed * 4UI
        Dim pteInBed As UInteger = ReadPagingEntry(pteAddressInBed, pdePwtInBed, pdePcdInBed, fabricInBed)
        If (pteInBed And 1UI) = 0UI Then ThrowPageFault(linearAddressInBed, userAccessInBed, writeAccessInBed, False, "page table entry not present")

        Dim effectiveUserInBed As Boolean = pdeUserInBed AndAlso ((pteInBed And &H4UI) <> 0UI)
        Dim effectiveWritableInBed As Boolean = pdeWritableInBed AndAlso ((pteInBed And &H2UI) <> 0UI)
        EnforcePageProtection(linearAddressInBed, userAccessInBed, writeAccessInBed,
                              effectiveUserInBed, effectiveWritableInBed, True)

        Dim requiredBitsInBed As UInteger = &H20UI
        If writeAccessInBed Then requiredBitsInBed = requiredBitsInBed Or &H40UI
        If (pteInBed And requiredBitsInBed) <> requiredBitsInBed Then
            pteInBed = SetPagingEntryBitsLocked(pteAddressInBed, requiredBitsInBed, pdePwtInBed, pdePcdInBed, fabricInBed)
            If (pteInBed And 1UI) = 0UI Then ThrowPageFault(linearAddressInBed, userAccessInBed, writeAccessInBed, False, "page table entry became not-present during Accessed/Dirty update")
            effectiveUserInBed = pdeUserInBed AndAlso ((pteInBed And &H4UI) <> 0UI)
            effectiveWritableInBed = pdeWritableInBed AndAlso ((pteInBed And &H2UI) <> 0UI)
            EnforcePageProtection(linearAddressInBed, userAccessInBed, writeAccessInBed,
                                  effectiveUserInBed, effectiveWritableInBed, True)
        End If

        Dim entryInBed As New Intel486TlbEntry With {
            .Valid = True,
            .Tag17 = (linearAddressInBed >> 15),
            .PhysicalPageBase = pteInBed And &HFFFFF000UI,
            .UserAccessible = effectiveUserInBed,
            .Writable = effectiveWritableInBed,
            .Dirty = (pteInBed And &H40UI) <> 0UI,
            .PageWriteThrough = (pteInBed And &H8UI) <> 0UI,
            .PageCacheDisable = (pteInBed And &H10UI) <> 0UI,
            .PageTableEntryPhysicalAddress = pteAddressInBed
        }
        Dim setInBed As Integer = CInt((linearAddressInBed >> 12) And 7UI)
        Dim wayInBed As Integer = ChooseTlbReplacementWay(setInBed)
        _tlbInBed(setInBed, wayInBed) = entryInBed
        TouchTlbWay(setInBed, wayInBed)

        Return New Intel486PhysicalTranslation With {
            .PhysicalAddress = ApplyA20Mask(entryInBed.PhysicalPageBase Or (linearAddressInBed And &HFFFUI)),
            .PageWriteThrough = entryInBed.PageWriteThrough,
            .PageCacheDisable = entryInBed.PageCacheDisable,
            .TlbHit = False
        }
    End Function

    Private Function ReadPagingEntry(addressInBed As UInteger,
                                     pwtInBed As Boolean,
                                     pcdInBed As Boolean,
                                     fabricInBed As IProcessorLocalBusFabric) As UInteger
        Dim requestInBed As New ProcessorBusCycleRequest With {
            .Address = ApplyA20Mask(addressInBed),
            .Kind = ProcessorBusCycleKind.MemoryRead,
            .Width = ProcessorBusDataWidth.Width32,
            .ActiveByteLaneMask = &HF,
            .PageWriteThrough = pwtInBed,
            .PageCacheDisable = pcdInBed
        }
        Return fabricInBed.ExecuteProcessorCycle(requestInBed).ReadData
    End Function

    Private Function SetPagingEntryBitsLocked(addressInBed As UInteger,
                                              bitsInBed As UInteger,
                                              pwtInBed As Boolean,
                                              pcdInBed As Boolean,
                                              fabricInBed As IProcessorLocalBusFabric) As UInteger
        Dim readRequestInBed As New ProcessorBusCycleRequest With {
            .Address = ApplyA20Mask(addressInBed),
            .Kind = ProcessorBusCycleKind.MemoryRead,
            .Width = ProcessorBusDataWidth.Width32,
            .ActiveByteLaneMask = &HF,
            .LockAsserted = True,
            .PageWriteThrough = pwtInBed,
            .PageCacheDisable = pcdInBed
        }
        Dim latestInBed As UInteger = fabricInBed.ExecuteProcessorCycle(readRequestInBed).ReadData
        Dim updatedInBed As UInteger = latestInBed Or bitsInBed
        If updatedInBed <> latestInBed Then
            Dim writeRequestInBed As New ProcessorBusCycleRequest With {
                .Address = ApplyA20Mask(addressInBed),
                .Kind = ProcessorBusCycleKind.MemoryWrite,
                .Width = ProcessorBusDataWidth.Width32,
                .WriteData = updatedInBed,
                .ActiveByteLaneMask = &HF,
                .LockAsserted = True,
                .PageWriteThrough = pwtInBed,
                .PageCacheDisable = pcdInBed
            }
            fabricInBed.ExecuteProcessorCycle(writeRequestInBed)
        End If
        Return updatedInBed
    End Function

    Private Function MarkTlbEntryDirty(linearAddressInBed As UInteger,
                                       userAccessInBed As Boolean,
                                       entryInBed As Intel486TlbEntry,
                                       fabricInBed As IProcessorLocalBusFabric) As Intel486TlbEntry
        Dim latestInBed As UInteger = SetPagingEntryBitsLocked(entryInBed.PageTableEntryPhysicalAddress,
                                                               &H60UI,
                                                               entryInBed.PageWriteThrough,
                                                               entryInBed.PageCacheDisable,
                                                               fabricInBed)
        If (latestInBed And 1UI) = 0UI Then
            entryInBed.Valid = False
            ThrowPageFault(linearAddressInBed, userAccessInBed, True, False,
                           "page table entry became not-present during Dirty update")
        End If
        entryInBed.Dirty = True
        Return entryInBed
    End Function

    Private Sub EnforcePageProtection(linearAddressInBed As UInteger,
                                      userAccessInBed As Boolean,
                                      writeAccessInBed As Boolean,
                                      pageUserInBed As Boolean,
                                      pageWritableInBed As Boolean,
                                      presentInBed As Boolean)
        Dim violationInBed As Boolean
        If userAccessInBed AndAlso Not pageUserInBed Then violationInBed = True
        If writeAccessInBed AndAlso Not pageWritableInBed Then
            If userAccessInBed OrElse SupervisorWriteProtectEnabled Then violationInBed = True
        End If
        If violationInBed Then ThrowPageFault(linearAddressInBed, userAccessInBed, writeAccessInBed, presentInBed, "page protection violation")
    End Sub

    Private Sub ThrowPageFault(linearAddressInBed As UInteger,
                               userAccessInBed As Boolean,
                               writeAccessInBed As Boolean,
                               presentViolationInBed As Boolean,
                               reasonInBed As String)
        _cr2InBed = linearAddressInBed
        Dim errorInBed As UInt16 = 0US
        If presentViolationInBed Then errorInBed = CUShort(errorInBed Or 1US)
        If writeAccessInBed Then errorInBed = CUShort(errorInBed Or 2US)
        If userAccessInBed Then errorInBed = CUShort(errorInBed Or 4US)
        Throw New Intel486PageFaultException(linearAddressInBed, errorInBed, "#PF: " & reasonInBed)
    End Sub

    Private Function ChooseTlbReplacementWay(setInBed As Integer) As Integer
        For wayInBed As Integer = 0 To 3
            If Not _tlbInBed(setInBed, wayInBed).Valid Then Return wayInBed
        Next

        Dim bitsInBed As Byte = _tlbLruInBed(setInBed)
        Dim b0InBed As Boolean = (bitsInBed And 1) <> 0
        Dim b1InBed As Boolean = (bitsInBed And 2) <> 0
        Dim b2InBed As Boolean = (bitsInBed And 4) <> 0
        If Not b0InBed Then
            ' Intel: B0=0 -> ways 0/1 least recent. B1=0 -> replace way 0.
            Return If(Not b1InBed, 0, 1)
        End If
        ' B0=1 -> ways 2/3 least recent. B2=0 -> replace way 2.
        Return If(Not b2InBed, 2, 3)
    End Function

    Private Sub TouchTlbWay(setInBed As Integer, wayInBed As Integer)
        Dim bitsInBed As Byte = _tlbLruInBed(setInBed)
        Select Case wayInBed
            Case 0
                bitsInBed = CByte(bitsInBed Or 1 Or 2)
            Case 1
                bitsInBed = CByte((bitsInBed Or 1) And Not 2)
            Case 2
                bitsInBed = CByte((bitsInBed And Not 1) Or 4)
            Case 3
                bitsInBed = CByte((bitsInBed And Not 1) And Not 4)
            Case Else
                Throw New ArgumentOutOfRangeException(NameOf(wayInBed))
        End Select
        _tlbLruInBed(setInBed) = CByte(bitsInBed And 7)
    End Sub

    Public Shared Sub ValidateMmuOrThrow()
        Dim fabricInBed As New Intel486MmuSelfTestFabric()
        Dim mmuInBed As New Intel486Mmu()

        ' Intel486 ET is physically hardwired high even if software writes CR0 with bit 4 clear.
        mmuInBed.WriteCR0(0UI)
        If (mmuInBed.CR0 And Cr0Et) = 0UI Then Throw New InvalidOperationException("486 CR0.ET hardwired-one behavior failed.")

        Dim directInBed As Intel486PhysicalTranslation = mmuInBed.TranslateLinearAddress(&H123456UI, Intel486MemoryAccessKind.Read, 0, fabricInBed)
        If directInBed.PhysicalAddress <> &H123456UI Then Throw New InvalidOperationException("486 MMU non-paged translation failed.")
        mmuInBed.SetA20Mask(True)
        directInBed = mmuInBed.TranslateLinearAddress(&H100123UI, Intel486MemoryAccessKind.Read, 0, fabricInBed)
        If directInBed.PhysicalAddress <> &H123UI Then Throw New InvalidOperationException("486 A20M# mask failed.")
        mmuInBed.SetA20Mask(False)

        Const directoryBaseInBed As UInteger = &H1000UI
        Const tableBaseInBed As UInteger = &H2000UI
        Const linearInBed As UInteger = &H403004UI
        Const physicalPageInBed As UInteger = &H345000UI
        fabricInBed.WriteDword(directoryBaseInBed + 4UI, tableBaseInBed Or &H7UI)
        fabricInBed.WriteDword(tableBaseInBed + 3UI * 4UI, physicalPageInBed Or &H7UI)
        mmuInBed.WriteCR3(directoryBaseInBed)
        mmuInBed.WriteCR0(&HE0000011UI)

        Dim firstInBed As Intel486PhysicalTranslation = mmuInBed.TranslateLinearAddress(linearInBed, Intel486MemoryAccessKind.Read, 3, fabricInBed)
        If firstInBed.PhysicalAddress <> physicalPageInBed + 4UI OrElse firstInBed.TlbHit Then Throw New InvalidOperationException("486 page walk failed.")
        If (fabricInBed.ReadDword(directoryBaseInBed + 4UI) And &H20UI) = 0UI Then Throw New InvalidOperationException("486 PDE Accessed update failed.")
        If (fabricInBed.ReadDword(tableBaseInBed + 12UI) And &H20UI) = 0UI Then Throw New InvalidOperationException("486 PTE Accessed update failed.")

        Dim secondInBed As Intel486PhysicalTranslation = mmuInBed.TranslateLinearAddress(linearInBed, Intel486MemoryAccessKind.Read, 3, fabricInBed)
        If Not secondInBed.TlbHit Then Throw New InvalidOperationException("486 TLB hit failed.")
        mmuInBed.TranslateLinearAddress(linearInBed, Intel486MemoryAccessKind.Write, 3, fabricInBed)
        If (fabricInBed.ReadDword(tableBaseInBed + 12UI) And &H40UI) = 0UI Then Throw New InvalidOperationException("486 PTE Dirty update failed.")

        mmuInBed.InvalidatePage(linearInBed)
        If mmuInBed.TranslateLinearAddress(linearInBed, Intel486MemoryAccessKind.Read, 3, fabricInBed).TlbHit Then Throw New InvalidOperationException("486 INVLPG-equivalent invalidation failed.")

        ' User write protection: present page, so error code must P|W|U = 7.
        mmuInBed.FlushTlb()
        fabricInBed.WriteDword(tableBaseInBed + 12UI, physicalPageInBed Or &H5UI)
        Try
            mmuInBed.TranslateLinearAddress(linearInBed, Intel486MemoryAccessKind.Write, 3, fabricInBed)
            Throw New InvalidOperationException("486 user read-only page did not fault.")
        Catch faultInBed As Intel486PageFaultException
            If faultInBed.ErrorCode <> 7US OrElse mmuInBed.CR2 <> linearInBed Then Throw New InvalidOperationException("486 page-fault user/write/present error code failed.")
        End Try

        ' Supervisor writes ignore R/W when WP=0, but fault when WP=1.
        mmuInBed.FlushTlb()
        Dim supervisorInBed As Intel486PhysicalTranslation = mmuInBed.TranslateLinearAddress(linearInBed, Intel486MemoryAccessKind.Write, 0, fabricInBed)
        If supervisorInBed.PhysicalAddress <> physicalPageInBed + 4UI Then Throw New InvalidOperationException("486 supervisor WP=0 compatibility failed.")
        mmuInBed.FlushTlb()
        mmuInBed.WriteCR0(mmuInBed.CR0 Or &H10000UI)
        Try
            mmuInBed.TranslateLinearAddress(linearInBed, Intel486MemoryAccessKind.Write, 0, fabricInBed)
            Throw New InvalidOperationException("486 supervisor WP=1 page did not fault.")
        Catch faultInBed As Intel486PageFaultException
            If faultInBed.ErrorCode <> 3US Then Throw New InvalidOperationException("486 supervisor/write/present page-fault error code failed.")
        End Try
    End Sub

    Private NotInheritable Class Intel486MmuSelfTestFabric
        Implements IProcessorLocalBusFabric, IProcessorFabricCapabilities

        Private ReadOnly _memoryInBed(&H3FFFFF) As Byte
        Private _clockInBed As ULong

        Public ReadOnly Property FabricIdentity As String Implements IProcessorLocalBusFabric.FabricIdentity
            Get
                Return "Intel486 MMU self-test fabric"
            End Get
        End Property
        Public ReadOnly Property ExternalClockHz As Long Implements IProcessorLocalBusFabric.ExternalClockHz
            Get
                Return Intel486Dx2.NominalExternalClockHz
            End Get
        End Property
        Public ReadOnly Property CurrentExternalClock As ULong Implements IProcessorLocalBusFabric.CurrentExternalClock
            Get
                Return _clockInBed
            End Get
        End Property

        Public Function ExecuteProcessorCycle(requestInBed As ProcessorBusCycleRequest) As ProcessorBusCycleResult Implements IProcessorLocalBusFabric.ExecuteProcessorCycle
            If requestInBed.Kind <> ProcessorBusCycleKind.MemoryRead AndAlso requestInBed.Kind <> ProcessorBusCycleKind.MemoryWrite Then
                Throw New NotSupportedException("MMU self-test fabric only implements memory cycles.")
            End If
            If requestInBed.Width <> ProcessorBusDataWidth.Width32 Then Throw New NotSupportedException("MMU self-test page walks are 32-bit cycles.")
            If requestInBed.Address > CUInt(_memoryInBed.Length - 4) Then Throw New IndexOutOfRangeException("MMU self-test physical address outside fixture.")
            Dim dataInBed As UInteger = ReadDword(requestInBed.Address)
            If requestInBed.Kind = ProcessorBusCycleKind.MemoryWrite Then
                WriteDword(requestInBed.Address, requestInBed.WriteData)
                dataInBed = 0UI
            End If
            AdvanceExternalClocks(2)
            Return ProcessorBusCycleResult.Completed(ProcessorBusDataWidth.Width32, 2, dataInBed)
        End Function

        Public Sub AdvanceExternalClocks(externalClocksInBed As Long) Implements IProcessorLocalBusFabric.AdvanceExternalClocks
            _clockInBed += CULng(externalClocksInBed)
        End Sub

        Public Function ReadDword(addressInBed As UInteger) As UInteger
            Dim iInBed As Integer = CInt(addressInBed)
            Return CUInt(_memoryInBed(iInBed)) Or
                   (CUInt(_memoryInBed(iInBed + 1)) << 8) Or
                   (CUInt(_memoryInBed(iInBed + 2)) << 16) Or
                   (CUInt(_memoryInBed(iInBed + 3)) << 24)
        End Function

        Public Sub WriteDword(addressInBed As UInteger, valueInBed As UInteger)
            Dim iInBed As Integer = CInt(addressInBed)
            _memoryInBed(iInBed) = CByte(valueInBed And &HFFUI)
            _memoryInBed(iInBed + 1) = CByte((valueInBed >> 8) And &HFFUI)
            _memoryInBed(iInBed + 2) = CByte((valueInBed >> 16) And &HFFUI)
            _memoryInBed(iInBed + 3) = CByte((valueInBed >> 24) And &HFFUI)
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
                Return True
            End Get
        End Property
        Public ReadOnly Property SupportsBurstReady As Boolean Implements IProcessorFabricCapabilities.SupportsBurstReady
            Get
                Return True
            End Get
        End Property
        Public ReadOnly Property SupportsCacheEnableInput As Boolean Implements IProcessorFabricCapabilities.SupportsCacheEnableInput
            Get
                Return True
            End Get
        End Property
        Public ReadOnly Property SupportsBusBackoff As Boolean Implements IProcessorFabricCapabilities.SupportsBusBackoff
            Get
                Return True
            End Get
        End Property
    End Class
End Class
