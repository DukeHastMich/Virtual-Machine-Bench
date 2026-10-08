Imports System
Imports System.Collections.Generic
Imports System.Numerics

' Native Intel486 integer/system execution front end.
'
' This engine deliberately belongs only to Intel486 silicon.  It does not inherit,
' call through, or mutate ProcessorCore286.  CP05 establishes architectural
' instruction semantics; CP06 interposes the cycle-accurate cache/write-buffer/bus
' engine before the QDI board is allowed to execute it.
Public NotInheritable Class Intel486ExecutionEngine
    Private Const FlagCf As UInteger = &H1UI
    Private Const FlagPf As UInteger = &H4UI
    Private Const FlagAf As UInteger = &H10UI
    Private Const FlagZf As UInteger = &H40UI
    Private Const FlagSf As UInteger = &H80UI
    Private Const FlagTf As UInteger = &H100UI
    Private Const FlagIf As UInteger = &H200UI
    Private Const FlagDf As UInteger = &H400UI
    Private Const FlagOf As UInteger = &H800UI
    Private Const FlagIopl As UInteger = &H3000UI
    Private Const FlagNt As UInteger = &H4000UI
    Private Const FlagRf As UInteger = &H10000UI
    Private Const FlagVm As UInteger = &H20000UI
    Private Const FlagAc As UInteger = &H40000UI
    Private Const FlagId As UInteger = &H200000UI

    Private Const Cr0Pe As UInteger = &H1UI
    Private Const Cr0Mp As UInteger = &H2UI
    Private Const Cr0Em As UInteger = &H4UI
    Private Const Cr0Ts As UInteger = &H8UI
    Private Const Cr0Et As UInteger = &H10UI
    Private Const Cr0Ne As UInteger = &H20UI
    Private Const Cr0Am As UInteger = &H40000UI
    Private Const Cr0Pg As UInteger = &H80000000UI

    Private ReadOnly _cpuInBed As Intel486Dx2
    Private ReadOnly _mmuInBed As Intel486Mmu

    Private _gdtrInBed As Intel486DescriptorTableRegister
    Private _idtrInBed As Intel486DescriptorTableRegister
    Private _ldtrInBed As Intel486SystemSelectorRegister
    Private _taskRegisterInBed As Intel486SystemSelectorRegister
    Private _cplInBed As Byte
    Private _haltedInBed As Boolean
    Private _shutdownInBed As Boolean
    Private _currentInstructionStartInBed As UInteger
    Private _instructionBytesInBed As Integer
    Private _exceptionDepthInBed As Integer
    Private _maskableInterruptShadowInBed As Integer
    Private _nmiInServiceInBed As Boolean
    Private _shutdownRequiresResetInBed As Boolean

    Public Sub New(cpuInBed As Intel486Dx2, mmuInBed As Intel486Mmu)
        If cpuInBed Is Nothing Then Throw New ArgumentNullException(NameOf(cpuInBed))
        If mmuInBed Is Nothing Then Throw New ArgumentNullException(NameOf(mmuInBed))
        _cpuInBed = cpuInBed
        _mmuInBed = mmuInBed
        ResetArchitecturalSystemState()
    End Sub

    Public ReadOnly Property GDTR As Intel486DescriptorTableRegister
        Get
            Return _gdtrInBed
        End Get
    End Property

    Public ReadOnly Property IDTR As Intel486DescriptorTableRegister
        Get
            Return _idtrInBed
        End Get
    End Property

    Public ReadOnly Property LDTR As Intel486SystemSelectorRegister
        Get
            Return _ldtrInBed
        End Get
    End Property

    Public ReadOnly Property TaskRegister As Intel486SystemSelectorRegister
        Get
            Return _taskRegisterInBed
        End Get
    End Property

    Public ReadOnly Property CurrentPrivilegeLevel As Byte
        Get
            Return _cplInBed
        End Get
    End Property

    Public ReadOnly Property Halted As Boolean
        Get
            Return _haltedInBed
        End Get
    End Property

    Public ReadOnly Property Shutdown As Boolean
        Get
            Return _shutdownInBed
        End Get
    End Property

    Public ReadOnly Property Virtual8086Mode As Boolean
        Get
            Return (_cpuInBed.EFLAGS And FlagVm) <> 0UI
        End Get
    End Property

    Public Sub ResetArchitecturalSystemState()
        _gdtrInBed = New Intel486DescriptorTableRegister With {.BaseAddress = 0UI, .Limit = &HFFFFUS}
        _idtrInBed = New Intel486DescriptorTableRegister With {.BaseAddress = 0UI, .Limit = &H3FFUS}
        _ldtrInBed = New Intel486SystemSelectorRegister()
        _taskRegisterInBed = New Intel486SystemSelectorRegister()
        _cplInBed = 0
        _haltedInBed = False
        _shutdownInBed = False
        _currentInstructionStartInBed = _cpuInBed.EIP
        _instructionBytesInBed = 0
        _exceptionDepthInBed = 0
        _maskableInterruptShadowInBed = 0
        _nmiInServiceInBed = False
        _shutdownRequiresResetInBed = False
    End Sub

    ' CP05 semantic entry point.  It intentionally executes one complete instruction
    ' without pretending that host time is processor time.  CP06 wraps this surface in
    ' the external/core clock budget and cache/bus state machine.
    Public Function ExecuteOneInstruction(fabricInBed As IProcessorLocalBusFabric) As Intel486InstructionResult
        If fabricInBed Is Nothing Then Throw New ArgumentNullException(NameOf(fabricInBed))

        Dim resultInBed As New Intel486InstructionResult With {
            .StartEip = _cpuInBed.EIP,
            .EndEip = _cpuInBed.EIP,
            .CoreClocksNominal = 1
        }

        ' Shutdown is a silicon state, not a host exception.  A normal shutdown can be
        ' left by NMI or RESET; if shutdown happened while servicing NMI, Intel specifies
        ' that only RESET may restart the processor.
        If _shutdownInBed Then
            If _cpuInBed.PendingNmi AndAlso Not _shutdownRequiresResetInBed Then
                _cpuInBed.ClearPendingNmi()
                _shutdownInBed = False
                _nmiInServiceInBed = True
                DeliverBoundaryInterrupt(CByte(Intel486ExceptionVector.Nmi), fabricInBed, True)
                resultInBed.EndEip = _cpuInBed.EIP
                resultInBed.Retired = False
                Return resultInBed
            End If
            resultInBed.Halted = True
            Return resultInBed
        End If

        If _haltedInBed Then
            If (_cpuInBed.PendingNmi AndAlso Not _nmiInServiceInBed) OrElse
               (_cpuInBed.IntrAsserted AndAlso FlagIsSet(FlagIf)) Then
                _haltedInBed = False
            Else
                resultInBed.Halted = True
                Return resultInBed
            End If
        End If

        ' External asynchronous events are recognized only at instruction boundaries.
        ' NMI is internally blocked after entry until IRET; one later edge may remain pending.
        If _cpuInBed.PendingNmi AndAlso Not _nmiInServiceInBed Then
            _cpuInBed.ClearPendingNmi()
            _nmiInServiceInBed = True
            DeliverBoundaryInterrupt(CByte(Intel486ExceptionVector.Nmi), fabricInBed, True)
            resultInBed.EndEip = _cpuInBed.EIP
            resultInBed.Retired = False
            Return resultInBed
        End If

        If _cpuInBed.IntrAsserted AndAlso FlagIsSet(FlagIf) AndAlso _maskableInterruptShadowInBed = 0 Then
            Dim vectorInBed As Byte = AcknowledgeExternalInterrupt(fabricInBed)
            DeliverBoundaryInterrupt(vectorInBed, fabricInBed, True)
            resultInBed.EndEip = _cpuInBed.EIP
            resultInBed.Retired = False
            Return resultInBed
        End If

        _currentInstructionStartInBed = _cpuInBed.EIP
        _instructionBytesInBed = 0

        Try
            Dim prefixesInBed As Intel486PrefixState = ScanPrefixes(fabricInBed)
            Dim opcodeInBed As Byte = FetchCodeByte(fabricInBed)
            resultInBed.Opcode = opcodeInBed
            ExecutePrimaryOpcode(opcodeInBed, prefixesInBed, resultInBed, fabricInBed)
            resultInBed.Retired = True
            resultInBed.EndEip = _cpuInBed.EIP
            resultInBed.Halted = _haltedInBed
            If _maskableInterruptShadowInBed > 0 Then _maskableInterruptShadowInBed -= 1

            If FlagIsSet(FlagTf) Then
                Throw New Intel486CpuException(Intel486ExceptionVector.Debug, _cpuInBed.EIP,
                                                "Single-step trap", False, 0US, True)
            End If
            Return resultInBed
        Catch pageInBed As Intel486PageFaultException
            _cpuInBed.MutableEip = _currentInstructionStartInBed
            Dim cpuFaultInBed As New Intel486CpuException(Intel486ExceptionVector.PageFault,
                                                          _currentInstructionStartInBed,
                                                          pageInBed.Message,
                                                          True,
                                                          pageInBed.ErrorCode)
            DeliverArchitecturalException(cpuFaultInBed, fabricInBed)
            resultInBed.EndEip = _cpuInBed.EIP
            resultInBed.Retired = False
            Return resultInBed
        Catch cpuFaultInBed As Intel486CpuException
            If Not cpuFaultInBed.IsTrap Then _cpuInBed.MutableEip = cpuFaultInBed.RestartEip
            DeliverArchitecturalException(cpuFaultInBed, fabricInBed)
            resultInBed.EndEip = _cpuInBed.EIP
            resultInBed.Retired = cpuFaultInBed.IsTrap
            Return resultInBed
        End Try
    End Function

    Private Sub DeliverBoundaryInterrupt(vectorInBed As Byte,
                                         fabricInBed As IProcessorLocalBusFabric,
                                         externalInBed As Boolean)
        Try
            DeliverInterrupt(vectorInBed, False, False, 0US, fabricInBed, externalInBed)
        Catch pageInBed As Intel486PageFaultException
            Dim pageFaultInBed As New Intel486CpuException(Intel486ExceptionVector.PageFault,
                                                           _cpuInBed.MutableEip,
                                                           pageInBed.Message,
                                                           True,
                                                           pageInBed.ErrorCode)
            DeliverArchitecturalException(pageFaultInBed, fabricInBed)
        Catch deliveryFaultInBed As Intel486CpuException
            DeliverArchitecturalException(deliveryFaultInBed, fabricInBed)
        End Try
    End Sub

    Private Function ScanPrefixes(fabricInBed As IProcessorLocalBusFabric) As Intel486PrefixState
        Dim prefixInBed As New Intel486PrefixState()
        Do
            Dim candidateInBed As Byte = PeekCodeByte(fabricInBed)
            Dim recognizedInBed As Boolean = True
            Select Case candidateInBed
                Case &H26
                    prefixInBed.SegmentOverridePresent = True
                    prefixInBed.SegmentOverride = Intel486SegmentRegister.ES
                Case &H2E
                    prefixInBed.SegmentOverridePresent = True
                    prefixInBed.SegmentOverride = Intel486SegmentRegister.CS
                Case &H36
                    prefixInBed.SegmentOverridePresent = True
                    prefixInBed.SegmentOverride = Intel486SegmentRegister.SS
                Case &H3E
                    prefixInBed.SegmentOverridePresent = True
                    prefixInBed.SegmentOverride = Intel486SegmentRegister.DS
                Case &H64
                    prefixInBed.SegmentOverridePresent = True
                    prefixInBed.SegmentOverride = Intel486SegmentRegister.FS
                Case &H65
                    prefixInBed.SegmentOverridePresent = True
                    prefixInBed.SegmentOverride = Intel486SegmentRegister.GS
                Case &H66
                    prefixInBed.OperandSizeOverride = True
                Case &H67
                    prefixInBed.AddressSizeOverride = True
                Case &HF0
                    prefixInBed.LockPresent = True
                Case &HF2
                    prefixInBed.Repeat = Intel486RepeatPrefix.Repne
                Case &HF3
                    prefixInBed.Repeat = Intel486RepeatPrefix.Repe
                Case Else
                    recognizedInBed = False
            End Select
            If Not recognizedInBed Then Exit Do
            FetchCodeByte(fabricInBed)
            prefixInBed.PrefixByteCount += 1
            If _instructionBytesInBed >= 15 Then ThrowFault(Intel486ExceptionVector.GeneralProtection, "Instruction exceeds Intel486 15-byte maximum.")
        Loop
        Return prefixInBed
    End Function

    Private Function CodeDefault32() As Boolean
        If Virtual8086Mode Then Return False
        Return _mmuInBed.GetSegment(Intel486SegmentRegister.CS).Default32
    End Function

    Private Function StackAddress32() As Boolean
        If Not _mmuInBed.ProtectedModeEnabled OrElse Virtual8086Mode Then Return False
        Return _mmuInBed.GetSegment(Intel486SegmentRegister.SS).Default32
    End Function

    Private Function FetchCodeByte(fabricInBed As IProcessorLocalBusFabric) As Byte
        If _instructionBytesInBed >= 15 Then ThrowFault(Intel486ExceptionVector.GeneralProtection, "Instruction exceeds Intel486 15-byte maximum.")
        Dim addressInBed As UInteger = SegmentedPhysicalAddress(Intel486SegmentRegister.CS,
                                                               _cpuInBed.MutableEip,
                                                               1UI,
                                                               Intel486MemoryAccessKind.Execute,
                                                               fabricInBed)
        Dim valueInBed As Byte = ReadPhysicalByte(addressInBed, False, False, fabricInBed)
        _cpuInBed.MutableEip = CUInt((CULng(_cpuInBed.MutableEip) + 1UL) And &HFFFFFFFFUL)
        _instructionBytesInBed += 1
        Return valueInBed
    End Function

    Private Function PeekCodeByte(fabricInBed As IProcessorLocalBusFabric) As Byte
        Dim addressInBed As UInteger = SegmentedPhysicalAddress(Intel486SegmentRegister.CS,
                                                               _cpuInBed.MutableEip,
                                                               1UI,
                                                               Intel486MemoryAccessKind.Execute,
                                                               fabricInBed)
        Return ReadPhysicalByte(addressInBed, False, False, fabricInBed)
    End Function

    Private Function FetchCodeWord(fabricInBed As IProcessorLocalBusFabric) As UInt16
        Dim loInBed As UInteger = FetchCodeByte(fabricInBed)
        Dim hiInBed As UInteger = FetchCodeByte(fabricInBed)
        Return CUShort(loInBed Or (hiInBed << 8))
    End Function

    Private Function FetchCodeDword(fabricInBed As IProcessorLocalBusFabric) As UInteger
        Dim b0InBed As UInteger = FetchCodeByte(fabricInBed)
        Dim b1InBed As UInteger = FetchCodeByte(fabricInBed)
        Dim b2InBed As UInteger = FetchCodeByte(fabricInBed)
        Dim b3InBed As UInteger = FetchCodeByte(fabricInBed)
        Return b0InBed Or (b1InBed << 8) Or (b2InBed << 16) Or (b3InBed << 24)
    End Function

    Private Function FetchSignedByte(fabricInBed As IProcessorLocalBusFabric) As Integer
        Dim valueInBed As Byte = FetchCodeByte(fabricInBed)
        If (valueInBed And &H80) <> 0 Then Return CInt(valueInBed) - &H100
        Return CInt(valueInBed)
    End Function

    Private Function FetchSignedWord(fabricInBed As IProcessorLocalBusFabric) As Integer
        Dim valueInBed As UInt16 = FetchCodeWord(fabricInBed)
        If (valueInBed And &H8000US) <> 0 Then Return CInt(valueInBed) - &H10000
        Return CInt(valueInBed)
    End Function

    Private Function FetchSignedDwordAsLong(fabricInBed As IProcessorLocalBusFabric) As Long
        Return SignedValue(FetchCodeDword(fabricInBed), 32)
    End Function

    Private Function SegmentedPhysicalAddress(segmentInBed As Intel486SegmentRegister,
                                              offsetInBed As UInteger,
                                              lengthInBed As UInteger,
                                              accessInBed As Intel486MemoryAccessKind,
                                              fabricInBed As IProcessorLocalBusFabric) As UInteger
        Dim translationsInBed() As Intel486PhysicalTranslation = TranslateSegmentRange(segmentInBed, offsetInBed, lengthInBed, accessInBed, fabricInBed)
        Return translationsInBed(0).PhysicalAddress
    End Function

    Private Function EffectivePagingCpl() As Byte
        If Virtual8086Mode Then Return 3
        Return _cplInBed
    End Function

    Private Function TranslateSegmentRange(segmentInBed As Intel486SegmentRegister,
                                           offsetInBed As UInteger,
                                           lengthInBed As UInteger,
                                           accessInBed As Intel486MemoryAccessKind,
                                           fabricInBed As IProcessorLocalBusFabric) As Intel486PhysicalTranslation()
        If lengthInBed = 0UI Then Throw New ArgumentOutOfRangeException(NameOf(lengthInBed))
        Dim linearStartInBed As UInteger
        Try
            ' Intel486 checks the complete segmented quantity before page translation.
            linearStartInBed = _mmuInBed.SegmentToLinear(segmentInBed, offsetInBed, lengthInBed, accessInBed)
        Catch exInBed As InvalidOperationException
            Dim vectorInBed As Intel486ExceptionVector = If(segmentInBed = Intel486SegmentRegister.SS,
                                                            Intel486ExceptionVector.StackFault,
                                                            Intel486ExceptionVector.GeneralProtection)
            Throw New Intel486CpuException(vectorInBed, _currentInstructionStartInBed, exInBed.Message, True, 0US)
        End Try
        Return TranslateLinearRange(linearStartInBed, lengthInBed, accessInBed, EffectivePagingCpl(), fabricInBed)
    End Function

    Private Function TranslateLinearRange(linearStartInBed As UInteger,
                                          lengthInBed As UInteger,
                                          accessInBed As Intel486MemoryAccessKind,
                                          cplInBed As Byte,
                                          fabricInBed As IProcessorLocalBusFabric) As Intel486PhysicalTranslation()
        If lengthInBed = 0UI Then Throw New ArgumentOutOfRangeException(NameOf(lengthInBed))
        Dim countInBed As Integer = CInt(lengthInBed)
        Dim translationsInBed(countInBed - 1) As Intel486PhysicalTranslation
        ' Pre-translate every byte before any operand data transfer.  This preserves
        ' restartability for split-page/split-segment operands: a later #PF cannot
        ' leave an earlier byte write visible to the guest.
        For byteInBed As Integer = 0 To countInBed - 1
            Dim linearInBed As UInteger = CUInt((CULng(linearStartInBed) + CULng(byteInBed)) And &HFFFFFFFFUL)
            translationsInBed(byteInBed) = _mmuInBed.TranslateLinearAddress(linearInBed, accessInBed, cplInBed, fabricInBed)
        Next
        Return translationsInBed
    End Function

    Private Function ReadMemoryByte(segmentInBed As Intel486SegmentRegister,
                                    offsetInBed As UInteger,
                                    fabricInBed As IProcessorLocalBusFabric) As Byte
        Dim translationsInBed() As Intel486PhysicalTranslation = TranslateSegmentRange(segmentInBed, offsetInBed, 1UI, Intel486MemoryAccessKind.Read, fabricInBed)
        Dim translatedInBed As Intel486PhysicalTranslation = translationsInBed(0)
        Return ReadPhysicalByte(translatedInBed.PhysicalAddress, translatedInBed.PageWriteThrough, translatedInBed.PageCacheDisable, fabricInBed)
    End Function

    Private Function ReadMemoryWord(segmentInBed As Intel486SegmentRegister,
                                    offsetInBed As UInteger,
                                    fabricInBed As IProcessorLocalBusFabric) As UInt16
        CheckAlignment(offsetInBed, 2UI)
        Dim translationsInBed() As Intel486PhysicalTranslation = TranslateSegmentRange(segmentInBed, offsetInBed, 2UI, Intel486MemoryAccessKind.Read, fabricInBed)
        Dim b0InBed As UInteger = ReadPhysicalByte(translationsInBed(0).PhysicalAddress, translationsInBed(0).PageWriteThrough, translationsInBed(0).PageCacheDisable, fabricInBed)
        Dim b1InBed As UInteger = ReadPhysicalByte(translationsInBed(1).PhysicalAddress, translationsInBed(1).PageWriteThrough, translationsInBed(1).PageCacheDisable, fabricInBed)
        Return CUShort(b0InBed Or (b1InBed << 8))
    End Function

    Private Function ReadMemoryDword(segmentInBed As Intel486SegmentRegister,
                                     offsetInBed As UInteger,
                                     fabricInBed As IProcessorLocalBusFabric) As UInteger
        CheckAlignment(offsetInBed, 4UI)
        Dim translationsInBed() As Intel486PhysicalTranslation = TranslateSegmentRange(segmentInBed, offsetInBed, 4UI, Intel486MemoryAccessKind.Read, fabricInBed)
        Dim valueInBed As UInteger = 0UI
        For byteInBed As Integer = 0 To 3
            Dim translatedInBed As Intel486PhysicalTranslation = translationsInBed(byteInBed)
            valueInBed = valueInBed Or (CUInt(ReadPhysicalByte(translatedInBed.PhysicalAddress, translatedInBed.PageWriteThrough, translatedInBed.PageCacheDisable, fabricInBed)) << (byteInBed * 8))
        Next
        Return valueInBed
    End Function

    Private Sub WriteMemoryByte(segmentInBed As Intel486SegmentRegister,
                                offsetInBed As UInteger,
                                valueInBed As Byte,
                                fabricInBed As IProcessorLocalBusFabric,
                                Optional lockedInBed As Boolean = False)
        Dim translationsInBed() As Intel486PhysicalTranslation = TranslateSegmentRange(segmentInBed, offsetInBed, 1UI, Intel486MemoryAccessKind.Write, fabricInBed)
        Dim translatedInBed As Intel486PhysicalTranslation = translationsInBed(0)
        WritePhysicalByte(translatedInBed.PhysicalAddress, valueInBed, translatedInBed.PageWriteThrough, translatedInBed.PageCacheDisable, fabricInBed, lockedInBed)
    End Sub

    Private Sub WriteMemoryWord(segmentInBed As Intel486SegmentRegister,
                                offsetInBed As UInteger,
                                valueInBed As UInt16,
                                fabricInBed As IProcessorLocalBusFabric,
                                Optional lockedInBed As Boolean = False)
        CheckAlignment(offsetInBed, 2UI)
        Dim translationsInBed() As Intel486PhysicalTranslation = TranslateSegmentRange(segmentInBed, offsetInBed, 2UI, Intel486MemoryAccessKind.Write, fabricInBed)
        For byteInBed As Integer = 0 To 1
            Dim translatedInBed As Intel486PhysicalTranslation = translationsInBed(byteInBed)
            WritePhysicalByte(translatedInBed.PhysicalAddress,
                              CByte((CUInt(valueInBed) >> (byteInBed * 8)) And &HFFUI),
                              translatedInBed.PageWriteThrough, translatedInBed.PageCacheDisable,
                              fabricInBed, lockedInBed)
        Next
    End Sub

    Private Sub WriteMemoryDword(segmentInBed As Intel486SegmentRegister,
                                 offsetInBed As UInteger,
                                 valueInBed As UInteger,
                                 fabricInBed As IProcessorLocalBusFabric,
                                 Optional lockedInBed As Boolean = False)
        CheckAlignment(offsetInBed, 4UI)
        Dim translationsInBed() As Intel486PhysicalTranslation = TranslateSegmentRange(segmentInBed, offsetInBed, 4UI, Intel486MemoryAccessKind.Write, fabricInBed)
        For byteInBed As Integer = 0 To 3
            Dim translatedInBed As Intel486PhysicalTranslation = translationsInBed(byteInBed)
            WritePhysicalByte(translatedInBed.PhysicalAddress,
                              CByte((valueInBed >> (byteInBed * 8)) And &HFFUI),
                              translatedInBed.PageWriteThrough, translatedInBed.PageCacheDisable,
                              fabricInBed, lockedInBed)
        Next
    End Sub

    Private Sub CheckAlignment(offsetInBed As UInteger, lengthInBed As UInteger)
        If lengthInBed <= 1UI Then Return
        If Not _mmuInBed.AlignmentMaskEnabled Then Return
        If Not FlagIsSet(FlagAc) Then Return
        If EffectivePagingCpl() <> 3 Then Return
        If (offsetInBed And (lengthInBed - 1UI)) <> 0UI Then
            ThrowFault(Intel486ExceptionVector.AlignmentCheck, "Unaligned CPL3 Intel486 data reference with CR0.AM and EFLAGS.AC set.", True, 0US)
        End If
    End Sub

    Private Function ReadSystemLinearByte(linearInBed As UInteger, fabricInBed As IProcessorLocalBusFabric) As Byte
        Dim translationsInBed() As Intel486PhysicalTranslation = TranslateLinearRange(linearInBed, 1UI, Intel486MemoryAccessKind.Read, 0, fabricInBed)
        Dim translatedInBed As Intel486PhysicalTranslation = translationsInBed(0)
        Return ReadPhysicalByte(translatedInBed.PhysicalAddress, translatedInBed.PageWriteThrough, translatedInBed.PageCacheDisable, fabricInBed)
    End Function

    Private Function ReadSystemLinearWord(linearInBed As UInteger, fabricInBed As IProcessorLocalBusFabric) As UInt16
        Dim translationsInBed() As Intel486PhysicalTranslation = TranslateLinearRange(linearInBed, 2UI, Intel486MemoryAccessKind.Read, 0, fabricInBed)
        Dim b0InBed As UInteger = ReadPhysicalByte(translationsInBed(0).PhysicalAddress, translationsInBed(0).PageWriteThrough, translationsInBed(0).PageCacheDisable, fabricInBed)
        Dim b1InBed As UInteger = ReadPhysicalByte(translationsInBed(1).PhysicalAddress, translationsInBed(1).PageWriteThrough, translationsInBed(1).PageCacheDisable, fabricInBed)
        Return CUShort(b0InBed Or (b1InBed << 8))
    End Function

    Private Function ReadSystemLinearDword(linearInBed As UInteger, fabricInBed As IProcessorLocalBusFabric) As UInteger
        Dim translationsInBed() As Intel486PhysicalTranslation = TranslateLinearRange(linearInBed, 4UI, Intel486MemoryAccessKind.Read, 0, fabricInBed)
        Dim valueInBed As UInteger = 0UI
        For nInBed As Integer = 0 To 3
            Dim translatedInBed As Intel486PhysicalTranslation = translationsInBed(nInBed)
            valueInBed = valueInBed Or (CUInt(ReadPhysicalByte(translatedInBed.PhysicalAddress, translatedInBed.PageWriteThrough, translatedInBed.PageCacheDisable, fabricInBed)) << (nInBed * 8))
        Next
        Return valueInBed
    End Function

    Private Sub WriteSystemLinearByte(linearInBed As UInteger, valueInBed As Byte, fabricInBed As IProcessorLocalBusFabric, Optional lockedInBed As Boolean = False)
        Dim translationsInBed() As Intel486PhysicalTranslation = TranslateLinearRange(linearInBed, 1UI, Intel486MemoryAccessKind.Write, 0, fabricInBed)
        Dim translatedInBed As Intel486PhysicalTranslation = translationsInBed(0)
        WritePhysicalByte(translatedInBed.PhysicalAddress, valueInBed, translatedInBed.PageWriteThrough, translatedInBed.PageCacheDisable, fabricInBed, lockedInBed)
    End Sub

    Private Sub WriteSystemLinearWord(linearInBed As UInteger, valueInBed As UInt16, fabricInBed As IProcessorLocalBusFabric, Optional lockedInBed As Boolean = False)
        Dim translationsInBed() As Intel486PhysicalTranslation = TranslateLinearRange(linearInBed, 2UI, Intel486MemoryAccessKind.Write, 0, fabricInBed)
        For nInBed As Integer = 0 To 1
            Dim translatedInBed As Intel486PhysicalTranslation = translationsInBed(nInBed)
            WritePhysicalByte(translatedInBed.PhysicalAddress, CByte((CUInt(valueInBed) >> (nInBed * 8)) And &HFFUI),
                              translatedInBed.PageWriteThrough, translatedInBed.PageCacheDisable, fabricInBed, lockedInBed)
        Next
    End Sub

    Private Sub WriteSystemLinearDword(linearInBed As UInteger, valueInBed As UInteger, fabricInBed As IProcessorLocalBusFabric, Optional lockedInBed As Boolean = False)
        Dim translationsInBed() As Intel486PhysicalTranslation = TranslateLinearRange(linearInBed, 4UI, Intel486MemoryAccessKind.Write, 0, fabricInBed)
        For nInBed As Integer = 0 To 3
            Dim translatedInBed As Intel486PhysicalTranslation = translationsInBed(nInBed)
            WritePhysicalByte(translatedInBed.PhysicalAddress, CByte((valueInBed >> (nInBed * 8)) And &HFFUI),
                              translatedInBed.PageWriteThrough, translatedInBed.PageCacheDisable, fabricInBed, lockedInBed)
        Next
    End Sub

    Private Function ReadPhysicalByte(physicalInBed As UInteger,
                                      pageWriteThroughInBed As Boolean,
                                      pageCacheDisableInBed As Boolean,
                                      fabricInBed As IProcessorLocalBusFabric) As Byte
        Dim requestInBed As New ProcessorBusCycleRequest With {
            .Address = physicalInBed,
            .Kind = ProcessorBusCycleKind.MemoryRead,
            .Width = ProcessorBusDataWidth.Width8,
            .PageWriteThrough = pageWriteThroughInBed,
            .PageCacheDisable = pageCacheDisableInBed
        }
        Dim responseInBed As ProcessorBusCycleResult = fabricInBed.ExecuteProcessorCycle(requestInBed)
        If responseInBed.ReadyKind = ProcessorBusReadyKind.Backoff Then
            responseInBed = fabricInBed.ExecuteProcessorCycle(requestInBed)
        End If
        Return CByte(responseInBed.ReadData And &HFFUI)
    End Function

    Private Sub WritePhysicalByte(physicalInBed As UInteger,
                                  valueInBed As Byte,
                                  pageWriteThroughInBed As Boolean,
                                  pageCacheDisableInBed As Boolean,
                                  fabricInBed As IProcessorLocalBusFabric,
                                  Optional lockedInBed As Boolean = False)
        Dim requestInBed As New ProcessorBusCycleRequest With {
            .Address = physicalInBed,
            .Kind = ProcessorBusCycleKind.MemoryWrite,
            .Width = ProcessorBusDataWidth.Width8,
            .WriteData = valueInBed,
            .LockAsserted = lockedInBed,
            .PageWriteThrough = pageWriteThroughInBed,
            .PageCacheDisable = pageCacheDisableInBed
        }
        Dim responseInBed As ProcessorBusCycleResult = fabricInBed.ExecuteProcessorCycle(requestInBed)
        If responseInBed.ReadyKind = ProcessorBusReadyKind.Backoff Then fabricInBed.ExecuteProcessorCycle(requestInBed)
    End Sub

    Private Function ReadIo(portInBed As UInt16, widthInBed As Integer, fabricInBed As IProcessorLocalBusFabric) As UInteger
        EnforceIoPermission(portInBed, widthInBed, fabricInBed)
        Dim busWidthInBed As ProcessorBusDataWidth = WidthToBusWidth(widthInBed)
        Dim requestInBed As New ProcessorBusCycleRequest With {.Port = portInBed, .Kind = ProcessorBusCycleKind.IoRead, .Width = busWidthInBed}
        Return fabricInBed.ExecuteProcessorCycle(requestInBed).ReadData And WidthMask(widthInBed)
    End Function

    Private Sub WriteIo(portInBed As UInt16, widthInBed As Integer, valueInBed As UInteger, fabricInBed As IProcessorLocalBusFabric)
        EnforceIoPermission(portInBed, widthInBed, fabricInBed)
        Dim busWidthInBed As ProcessorBusDataWidth = WidthToBusWidth(widthInBed)
        Dim requestInBed As New ProcessorBusCycleRequest With {.Port = portInBed, .Kind = ProcessorBusCycleKind.IoWrite, .Width = busWidthInBed, .WriteData = valueInBed And WidthMask(widthInBed)}
        fabricInBed.ExecuteProcessorCycle(requestInBed)
    End Sub

    Private Shared Function WidthToBusWidth(widthInBed As Integer) As ProcessorBusDataWidth
        Select Case widthInBed
            Case 8 : Return ProcessorBusDataWidth.Width8
            Case 16 : Return ProcessorBusDataWidth.Width16
            Case 32 : Return ProcessorBusDataWidth.Width32
            Case Else : Throw New ArgumentOutOfRangeException(NameOf(widthInBed))
        End Select
    End Function

    Private Function DecodeModRm(address32InBed As Boolean, fabricInBed As IProcessorLocalBusFabric) As Intel486DecodedModRm
        Dim decodedInBed As New Intel486DecodedModRm()
        decodedInBed.Raw = FetchCodeByte(fabricInBed)
        decodedInBed.ModField = CByte((decodedInBed.Raw >> 6) And 3)
        decodedInBed.RegField = CByte((decodedInBed.Raw >> 3) And 7)
        decodedInBed.RmField = CByte(decodedInBed.Raw And 7)
        decodedInBed.IsRegisterOperand = decodedInBed.ModField = 3
        decodedInBed.DefaultSegment = Intel486SegmentRegister.DS
        If decodedInBed.IsRegisterOperand Then Return decodedInBed

        If address32InBed Then
            DecodeModRm32(decodedInBed, fabricInBed)
        Else
            DecodeModRm16(decodedInBed, fabricInBed)
        End If
        Return decodedInBed
    End Function

    Private Sub DecodeModRm16(ByRef decodedInBed As Intel486DecodedModRm, fabricInBed As IProcessorLocalBusFabric)
        Dim baseInBed As Integer = 0
        Select Case decodedInBed.RmField
            Case 0 : baseInBed = CInt(_cpuInBed.ReadGeneralRegister16(3)) + CInt(_cpuInBed.ReadGeneralRegister16(6)) ' BX+SI
            Case 1 : baseInBed = CInt(_cpuInBed.ReadGeneralRegister16(3)) + CInt(_cpuInBed.ReadGeneralRegister16(7)) ' BX+DI
            Case 2 : baseInBed = CInt(_cpuInBed.ReadGeneralRegister16(5)) + CInt(_cpuInBed.ReadGeneralRegister16(6)) : decodedInBed.DefaultSegment = Intel486SegmentRegister.SS
            Case 3 : baseInBed = CInt(_cpuInBed.ReadGeneralRegister16(5)) + CInt(_cpuInBed.ReadGeneralRegister16(7)) : decodedInBed.DefaultSegment = Intel486SegmentRegister.SS
            Case 4 : baseInBed = _cpuInBed.ReadGeneralRegister16(6)
            Case 5 : baseInBed = _cpuInBed.ReadGeneralRegister16(7)
            Case 6
                If decodedInBed.ModField = 0 Then
                    decodedInBed.Displacement = FetchCodeWord(fabricInBed)
                    decodedInBed.DisplacementWidth = 16
                    decodedInBed.EffectiveOffset = CUInt(decodedInBed.Displacement And &HFFFF)
                    Return
                End If
                baseInBed = _cpuInBed.ReadGeneralRegister16(5)
                decodedInBed.DefaultSegment = Intel486SegmentRegister.SS
            Case 7 : baseInBed = _cpuInBed.ReadGeneralRegister16(3)
        End Select

        Select Case decodedInBed.ModField
            Case 1
                decodedInBed.Displacement = FetchSignedByte(fabricInBed)
                decodedInBed.DisplacementWidth = 8
            Case 2
                decodedInBed.Displacement = FetchSignedWord(fabricInBed)
                decodedInBed.DisplacementWidth = 16
        End Select
        decodedInBed.EffectiveOffset = CUInt((CLng(baseInBed) + decodedInBed.Displacement) And &HFFFFL)
    End Sub

    Private Sub DecodeModRm32(ByRef decodedInBed As Intel486DecodedModRm, fabricInBed As IProcessorLocalBusFabric)
        Dim baseValueInBed As UInteger = 0UI
        Dim indexValueInBed As UInteger = 0UI
        Dim hasBaseInBed As Boolean = True

        If decodedInBed.RmField = 4 Then
            decodedInBed.HasSib = True
            decodedInBed.SibRaw = FetchCodeByte(fabricInBed)
            decodedInBed.SibScale = CByte((decodedInBed.SibRaw >> 6) And 3)
            decodedInBed.SibIndex = CByte((decodedInBed.SibRaw >> 3) And 7)
            decodedInBed.SibBase = CByte(decodedInBed.SibRaw And 7)

            If decodedInBed.SibIndex <> 4 Then
                indexValueInBed = _cpuInBed.ReadGeneralRegister32(decodedInBed.SibIndex) << decodedInBed.SibScale
            End If
            If decodedInBed.ModField = 0 AndAlso decodedInBed.SibBase = 5 Then
                hasBaseInBed = False
            Else
                baseValueInBed = _cpuInBed.ReadGeneralRegister32(decodedInBed.SibBase)
                If decodedInBed.SibBase = 4 OrElse decodedInBed.SibBase = 5 Then decodedInBed.DefaultSegment = Intel486SegmentRegister.SS
            End If
        ElseIf decodedInBed.ModField = 0 AndAlso decodedInBed.RmField = 5 Then
            hasBaseInBed = False
        Else
            baseValueInBed = _cpuInBed.ReadGeneralRegister32(decodedInBed.RmField)
            If decodedInBed.RmField = 4 OrElse decodedInBed.RmField = 5 Then decodedInBed.DefaultSegment = Intel486SegmentRegister.SS
        End If

        Select Case decodedInBed.ModField
            Case 0
                If Not hasBaseInBed Then
                    decodedInBed.Displacement = CInt(SignedValue(FetchCodeDword(fabricInBed), 32))
                    decodedInBed.DisplacementWidth = 32
                End If
            Case 1
                decodedInBed.Displacement = FetchSignedByte(fabricInBed)
                decodedInBed.DisplacementWidth = 8
            Case 2
                decodedInBed.Displacement = CInt(SignedValue(FetchCodeDword(fabricInBed), 32))
                decodedInBed.DisplacementWidth = 32
        End Select

        Dim displacementBitsInBed As UInteger = UnsignedBits(decodedInBed.Displacement, 32)
        decodedInBed.EffectiveOffset = CUInt((CULng(baseValueInBed) + CULng(indexValueInBed) + CULng(displacementBitsInBed)) And &HFFFFFFFFUL)
    End Sub

    Private Function EffectiveOperandSegment(decodedInBed As Intel486DecodedModRm, prefixesInBed As Intel486PrefixState) As Intel486SegmentRegister
        If prefixesInBed.SegmentOverridePresent Then Return prefixesInBed.SegmentOverride
        Return decodedInBed.DefaultSegment
    End Function

    Private Function ReadRm(decodedInBed As Intel486DecodedModRm,
                            widthInBed As Integer,
                            prefixesInBed As Intel486PrefixState,
                            fabricInBed As IProcessorLocalBusFabric) As UInteger
        If decodedInBed.IsRegisterOperand Then Return ReadRegisterByWidth(decodedInBed.RmField, widthInBed)
        Dim segmentInBed As Intel486SegmentRegister = EffectiveOperandSegment(decodedInBed, prefixesInBed)
        Select Case widthInBed
            Case 8 : Return ReadMemoryByte(segmentInBed, decodedInBed.EffectiveOffset, fabricInBed)
            Case 16 : Return ReadMemoryWord(segmentInBed, decodedInBed.EffectiveOffset, fabricInBed)
            Case Else : Return ReadMemoryDword(segmentInBed, decodedInBed.EffectiveOffset, fabricInBed)
        End Select
    End Function

    Private Sub WriteRm(decodedInBed As Intel486DecodedModRm,
                        widthInBed As Integer,
                        valueInBed As UInteger,
                        prefixesInBed As Intel486PrefixState,
                        fabricInBed As IProcessorLocalBusFabric,
                        Optional lockedInBed As Boolean = False)
        valueInBed = valueInBed And WidthMask(widthInBed)
        If decodedInBed.IsRegisterOperand Then
            WriteRegisterByWidth(decodedInBed.RmField, widthInBed, valueInBed)
            Return
        End If
        Dim segmentInBed As Intel486SegmentRegister = EffectiveOperandSegment(decodedInBed, prefixesInBed)
        Select Case widthInBed
            Case 8 : WriteMemoryByte(segmentInBed, decodedInBed.EffectiveOffset, CByte(valueInBed), fabricInBed, lockedInBed)
            Case 16 : WriteMemoryWord(segmentInBed, decodedInBed.EffectiveOffset, CUShort(valueInBed), fabricInBed, lockedInBed)
            Case Else : WriteMemoryDword(segmentInBed, decodedInBed.EffectiveOffset, valueInBed, fabricInBed, lockedInBed)
        End Select
    End Sub

    Private Function ReadRegisterByWidth(indexInBed As Integer, widthInBed As Integer) As UInteger
        Select Case widthInBed
            Case 8 : Return _cpuInBed.ReadGeneralRegister8(indexInBed)
            Case 16 : Return _cpuInBed.ReadGeneralRegister16(indexInBed)
            Case 32 : Return _cpuInBed.ReadGeneralRegister32(indexInBed)
            Case Else : Throw New ArgumentOutOfRangeException(NameOf(widthInBed))
        End Select
    End Function

    Private Sub WriteRegisterByWidth(indexInBed As Integer, widthInBed As Integer, valueInBed As UInteger)
        Select Case widthInBed
            Case 8 : _cpuInBed.WriteGeneralRegister8(indexInBed, CByte(valueInBed And &HFFUI))
            Case 16 : _cpuInBed.WriteGeneralRegister16(indexInBed, CUShort(valueInBed And &HFFFFUI))
            Case 32 : _cpuInBed.WriteGeneralRegister32(indexInBed, valueInBed)
            Case Else : Throw New ArgumentOutOfRangeException(NameOf(widthInBed))
        End Select
    End Sub

    Private Shared Function WidthMask(widthInBed As Integer) As UInteger
        Select Case widthInBed
            Case 8 : Return &HFFUI
            Case 16 : Return &HFFFFUI
            Case 32 : Return &HFFFFFFFFUI
            Case Else : Throw New ArgumentOutOfRangeException(NameOf(widthInBed))
        End Select
    End Function

    Private Shared Function WidthSignBit(widthInBed As Integer) As UInteger
        Select Case widthInBed
            Case 8 : Return &H80UI
            Case 16 : Return &H8000UI
            Case 32 : Return &H80000000UI
            Case Else : Throw New ArgumentOutOfRangeException(NameOf(widthInBed))
        End Select
    End Function

    Private Shared Function WidthModulus(widthInBed As Integer) As ULong
        If widthInBed = 32 Then Return &H100000000UL
        Return 1UL << widthInBed
    End Function

    Private Shared Function SignedValue(valueInBed As UInteger, widthInBed As Integer) As Long
        Dim maskedInBed As UInteger = valueInBed And WidthMask(widthInBed)
        Dim signInBed As UInteger = WidthSignBit(widthInBed)
        If (maskedInBed And signInBed) = 0UI Then Return CLng(maskedInBed)
        Return CLng(maskedInBed) - CLng(WidthModulus(widthInBed))
    End Function

    Private Shared Function UnsignedBits(valueInBed As Long, widthInBed As Integer) As UInteger
        Dim modulusInBed As Long = If(widthInBed = 32, &H100000000L, 1L << widthInBed)
        Dim normalizedInBed As Long = valueInBed Mod modulusInBed
        If normalizedInBed < 0 Then normalizedInBed += modulusInBed
        Return CUInt(normalizedInBed)
    End Function

    Private Function AddValues(leftInBed As UInteger, rightInBed As UInteger, carryInBed As UInteger, widthInBed As Integer) As UInteger
        Dim maskInBed As UInteger = WidthMask(widthInBed)
        leftInBed = leftInBed And maskInBed
        rightInBed = rightInBed And maskInBed
        Dim totalInBed As ULong = CULng(leftInBed) + CULng(rightInBed) + CULng(carryInBed And 1UI)
        Dim resultInBed As UInteger = CUInt(totalInBed And CULng(maskInBed))
        SetFlag(FlagCf, totalInBed >= WidthModulus(widthInBed))
        SetFlag(FlagAf, ((leftInBed Xor rightInBed Xor resultInBed) And &H10UI) <> 0UI)
        SetFlag(FlagOf, ((Not (leftInBed Xor rightInBed) And (leftInBed Xor resultInBed) And WidthSignBit(widthInBed)) <> 0UI))
        SetSzpFlags(resultInBed, widthInBed)
        Return resultInBed
    End Function

    Private Function SubtractValues(leftInBed As UInteger, rightInBed As UInteger, borrowInBed As UInteger, widthInBed As Integer) As UInteger
        Dim maskInBed As UInteger = WidthMask(widthInBed)
        leftInBed = leftInBed And maskInBed
        rightInBed = rightInBed And maskInBed
        Dim subtrahendInBed As ULong = CULng(rightInBed) + CULng(borrowInBed And 1UI)
        Dim modulusInBed As ULong = WidthModulus(widthInBed)
        Dim rawInBed As ULong = (CULng(leftInBed) + modulusInBed - subtrahendInBed) Mod modulusInBed
        Dim resultInBed As UInteger = CUInt(rawInBed And CULng(maskInBed))
        SetFlag(FlagCf, CULng(leftInBed) < subtrahendInBed)
        SetFlag(FlagAf, ((leftInBed Xor rightInBed Xor resultInBed) And &H10UI) <> 0UI)
        SetFlag(FlagOf, (((leftInBed Xor rightInBed) And (leftInBed Xor resultInBed) And WidthSignBit(widthInBed)) <> 0UI))
        SetSzpFlags(resultInBed, widthInBed)
        Return resultInBed
    End Function

    Private Function LogicResult(valueInBed As UInteger, widthInBed As Integer) As UInteger
        Dim resultInBed As UInteger = valueInBed And WidthMask(widthInBed)
        SetFlag(FlagCf, False)
        SetFlag(FlagOf, False)
        SetSzpFlags(resultInBed, widthInBed)
        Return resultInBed
    End Function

    Private Sub SetSzpFlags(valueInBed As UInteger, widthInBed As Integer)
        Dim maskedInBed As UInteger = valueInBed And WidthMask(widthInBed)
        SetFlag(FlagSf, (maskedInBed And WidthSignBit(widthInBed)) <> 0UI)
        SetFlag(FlagZf, maskedInBed = 0UI)
        SetFlag(FlagPf, HasEvenParity(CByte(maskedInBed And &HFFUI)))
    End Sub

    Private Shared Function HasEvenParity(valueInBed As Byte) As Boolean
        Dim valueWorkInBed As Integer = valueInBed
        valueWorkInBed = valueWorkInBed Xor (valueWorkInBed >> 4)
        valueWorkInBed = valueWorkInBed And &HF
        Return ((&H6996 >> valueWorkInBed) And 1) = 0
    End Function

    Private Function FlagIsSet(maskInBed As UInteger) As Boolean
        Return (_cpuInBed.MutableEflags And maskInBed) <> 0UI
    End Function

    Private Sub SetFlag(maskInBed As UInteger, valueInBed As Boolean)
        Dim flagsInBed As UInteger = _cpuInBed.MutableEflags
        If valueInBed Then
            flagsInBed = flagsInBed Or maskInBed
        Else
            flagsInBed = flagsInBed And Not maskInBed
        End If
        _cpuInBed.MutableEflags = flagsInBed
    End Sub

    Private Function ConditionTrue(conditionInBed As Integer) As Boolean
        Dim cfInBed As Boolean = FlagIsSet(FlagCf)
        Dim pfInBed As Boolean = FlagIsSet(FlagPf)
        Dim zfInBed As Boolean = FlagIsSet(FlagZf)
        Dim sfInBed As Boolean = FlagIsSet(FlagSf)
        Dim ofInBed As Boolean = FlagIsSet(FlagOf)
        Select Case conditionInBed And &HF
            Case 0 : Return ofInBed
            Case 1 : Return Not ofInBed
            Case 2 : Return cfInBed
            Case 3 : Return Not cfInBed
            Case 4 : Return zfInBed
            Case 5 : Return Not zfInBed
            Case 6 : Return cfInBed OrElse zfInBed
            Case 7 : Return Not cfInBed AndAlso Not zfInBed
            Case 8 : Return sfInBed
            Case 9 : Return Not sfInBed
            Case 10 : Return pfInBed
            Case 11 : Return Not pfInBed
            Case 12 : Return sfInBed <> ofInBed
            Case 13 : Return sfInBed = ofInBed
            Case 14 : Return zfInBed OrElse (sfInBed <> ofInBed)
            Case Else : Return Not zfInBed AndAlso (sfInBed = ofInBed)
        End Select
    End Function

    Private Sub ThrowFault(vectorInBed As Intel486ExceptionVector,
                           messageInBed As String,
                           Optional hasErrorCodeInBed As Boolean = False,
                           Optional errorCodeInBed As UInt16 = 0US)
        Throw New Intel486CpuException(vectorInBed, _currentInstructionStartInBed, messageInBed, hasErrorCodeInBed, errorCodeInBed)
    End Sub

    Private Function GetStackPointer() As UInteger
        If StackAddress32() Then Return _cpuInBed.ReadGeneralRegister32(4)
        Return _cpuInBed.ReadGeneralRegister16(4)
    End Function

    Private Sub SetStackPointer(valueInBed As UInteger)
        If StackAddress32() Then
            _cpuInBed.WriteGeneralRegister32(4, valueInBed)
        Else
            _cpuInBed.WriteGeneralRegister16(4, CUShort(valueInBed And &HFFFFUI))
        End If
    End Sub

    Private Sub PushValue(valueInBed As UInteger, widthInBed As Integer, fabricInBed As IProcessorLocalBusFabric)
        Dim bytesInBed As UInteger = CUInt(widthInBed \ 8)
        Dim spInBed As UInteger = GetStackPointer()
        If StackAddress32() Then
            spInBed = CUInt((CULng(spInBed) - CULng(bytesInBed)) And &HFFFFFFFFUL)
        Else
            spInBed = CUInt((CLng(spInBed) - CLng(bytesInBed)) And &HFFFFL)
        End If
        SetStackPointer(spInBed)
        If widthInBed = 16 Then
            WriteMemoryWord(Intel486SegmentRegister.SS, spInBed, CUShort(valueInBed And &HFFFFUI), fabricInBed)
        Else
            WriteMemoryDword(Intel486SegmentRegister.SS, spInBed, valueInBed, fabricInBed)
        End If
    End Sub

    Private Function PopValue(widthInBed As Integer, fabricInBed As IProcessorLocalBusFabric) As UInteger
        Dim bytesInBed As UInteger = CUInt(widthInBed \ 8)
        Dim spInBed As UInteger = GetStackPointer()
        Dim resultInBed As UInteger
        If widthInBed = 16 Then
            resultInBed = ReadMemoryWord(Intel486SegmentRegister.SS, spInBed, fabricInBed)
        Else
            resultInBed = ReadMemoryDword(Intel486SegmentRegister.SS, spInBed, fabricInBed)
        End If
        If StackAddress32() Then
            spInBed = CUInt((CULng(spInBed) + CULng(bytesInBed)) And &HFFFFFFFFUL)
        Else
            spInBed = (spInBed + bytesInBed) And &HFFFFUI
        End If
        SetStackPointer(spInBed)
        Return resultInBed
    End Function

    Private Function FetchImmediate(widthInBed As Integer, fabricInBed As IProcessorLocalBusFabric) As UInteger
        Select Case widthInBed
            Case 8 : Return FetchCodeByte(fabricInBed)
            Case 16 : Return FetchCodeWord(fabricInBed)
            Case 32 : Return FetchCodeDword(fabricInBed)
            Case Else : Throw New ArgumentOutOfRangeException(NameOf(widthInBed))
        End Select
    End Function

    Private Sub BranchRelative(displacementInBed As Long, operand32InBed As Boolean)
        Dim targetInBed As Long = CLng(_cpuInBed.MutableEip) + displacementInBed
        If operand32InBed Then
            _cpuInBed.MutableEip = UnsignedBits(targetInBed, 32)
        Else
            _cpuInBed.MutableEip = CUInt(targetInBed And &HFFFFL)
        End If
    End Sub

    Private Function ExecuteAluOperation(operationInBed As Integer,
                                         leftInBed As UInteger,
                                         rightInBed As UInteger,
                                         widthInBed As Integer) As UInteger
        Select Case operationInBed And 7
            Case 0 ' ADD
                Return AddValues(leftInBed, rightInBed, 0UI, widthInBed)
            Case 1 ' OR
                Return LogicResult(leftInBed Or rightInBed, widthInBed)
            Case 2 ' ADC
                Return AddValues(leftInBed, rightInBed, If(FlagIsSet(FlagCf), 1UI, 0UI), widthInBed)
            Case 3 ' SBB
                Return SubtractValues(leftInBed, rightInBed, If(FlagIsSet(FlagCf), 1UI, 0UI), widthInBed)
            Case 4 ' AND
                Return LogicResult(leftInBed And rightInBed, widthInBed)
            Case 5 ' SUB
                Return SubtractValues(leftInBed, rightInBed, 0UI, widthInBed)
            Case 6 ' XOR
                Return LogicResult(leftInBed Xor rightInBed, widthInBed)
            Case Else ' CMP
                Return SubtractValues(leftInBed, rightInBed, 0UI, widthInBed)
        End Select
    End Function

    Private Sub ExecutePrimaryOpcode(opcodeInBed As Byte,
                                     prefixesInBed As Intel486PrefixState,
                                     ByRef resultInBed As Intel486InstructionResult,
                                     fabricInBed As IProcessorLocalBusFabric)
        Dim operand32InBed As Boolean = prefixesInBed.EffectiveOperand32(CodeDefault32())
        Dim address32InBed As Boolean = prefixesInBed.EffectiveAddress32(CodeDefault32())
        Dim operandWidthInBed As Integer = If(operand32InBed, 32, 16)

        ' Arithmetic/logical family 00-3D.  Forms 0-5 repeat every eight opcodes.
        If opcodeInBed <= &H3D AndAlso (opcodeInBed And 7) <= 5 Then
            ExecuteAluFamily(opcodeInBed, operandWidthInBed, address32InBed, prefixesInBed, fabricInBed)
            Return
        End If

        Select Case opcodeInBed
            Case &H6 : RejectLock(prefixesInBed) : PushValue(_mmuInBed.GetSegment(Intel486SegmentRegister.ES).Selector, operandWidthInBed, fabricInBed)
            Case &H7 : RejectLock(prefixesInBed) : LoadSegmentFromStack(Intel486SegmentRegister.ES, operandWidthInBed, fabricInBed)
            Case &HE : RejectLock(prefixesInBed) : PushValue(_mmuInBed.GetSegment(Intel486SegmentRegister.CS).Selector, operandWidthInBed, fabricInBed)
            Case &H16 : RejectLock(prefixesInBed) : PushValue(_mmuInBed.GetSegment(Intel486SegmentRegister.SS).Selector, operandWidthInBed, fabricInBed)
            Case &H17 : RejectLock(prefixesInBed) : LoadSegmentFromStack(Intel486SegmentRegister.SS, operandWidthInBed, fabricInBed)
            Case &H1E : RejectLock(prefixesInBed) : PushValue(_mmuInBed.GetSegment(Intel486SegmentRegister.DS).Selector, operandWidthInBed, fabricInBed)
            Case &H1F : RejectLock(prefixesInBed) : LoadSegmentFromStack(Intel486SegmentRegister.DS, operandWidthInBed, fabricInBed)
            Case &H27 : RejectLock(prefixesInBed) : ExecuteDaa()
            Case &H2F : RejectLock(prefixesInBed) : ExecuteDas()
            Case &H37 : RejectLock(prefixesInBed) : ExecuteAaa()
            Case &H3F : RejectLock(prefixesInBed) : ExecuteAas()

            Case &H40 To &H47
                RejectLock(prefixesInBed)
                Dim regInBed As Integer = opcodeInBed And 7
                Dim oldCfInBed As Boolean = FlagIsSet(FlagCf)
                Dim valueInBed As UInteger = AddValues(ReadRegisterByWidth(regInBed, operandWidthInBed), 1UI, 0UI, operandWidthInBed)
                SetFlag(FlagCf, oldCfInBed)
                WriteRegisterByWidth(regInBed, operandWidthInBed, valueInBed)
            Case &H48 To &H4F
                RejectLock(prefixesInBed)
                Dim regInBed As Integer = opcodeInBed And 7
                Dim oldCfInBed As Boolean = FlagIsSet(FlagCf)
                Dim valueInBed As UInteger = SubtractValues(ReadRegisterByWidth(regInBed, operandWidthInBed), 1UI, 0UI, operandWidthInBed)
                SetFlag(FlagCf, oldCfInBed)
                WriteRegisterByWidth(regInBed, operandWidthInBed, valueInBed)
            Case &H50 To &H57
                RejectLock(prefixesInBed)
                Dim regInBed As Integer = opcodeInBed And 7
                Dim sourceInBed As UInteger = ReadRegisterByWidth(regInBed, operandWidthInBed)
                PushValue(sourceInBed, operandWidthInBed, fabricInBed)
            Case &H58 To &H5F
                RejectLock(prefixesInBed)
                Dim regInBed As Integer = opcodeInBed And 7
                WriteRegisterByWidth(regInBed, operandWidthInBed, PopValue(operandWidthInBed, fabricInBed))
            Case &H60
                RejectLock(prefixesInBed)
                ExecutePushAll(operandWidthInBed, fabricInBed)
            Case &H61
                RejectLock(prefixesInBed)
                ExecutePopAll(operandWidthInBed, fabricInBed)
            Case &H62
                RejectLock(prefixesInBed)
                ExecuteBound(operandWidthInBed, address32InBed, prefixesInBed, fabricInBed)
            Case &H63
                RejectLock(prefixesInBed)
                ExecuteArpl(address32InBed, prefixesInBed, fabricInBed)
            Case &H68
                RejectLock(prefixesInBed)
                PushValue(FetchImmediate(operandWidthInBed, fabricInBed), operandWidthInBed, fabricInBed)
            Case &H69
                RejectLock(prefixesInBed)
                ExecuteImulImmediate(operandWidthInBed, address32InBed, prefixesInBed, False, fabricInBed)
            Case &H6A
                RejectLock(prefixesInBed)
                Dim immediateInBed As UInteger = UnsignedBits(FetchSignedByte(fabricInBed), operandWidthInBed)
                PushValue(immediateInBed, operandWidthInBed, fabricInBed)
            Case &H6B
                RejectLock(prefixesInBed)
                ExecuteImulImmediate(operandWidthInBed, address32InBed, prefixesInBed, True, fabricInBed)
            Case &H6C To &H6F
                RejectLock(prefixesInBed)
                ExecuteStringIo(opcodeInBed, operandWidthInBed, address32InBed, prefixesInBed, fabricInBed)
            Case &H70 To &H7F
                RejectLock(prefixesInBed)
                Dim displacementInBed As Integer = FetchSignedByte(fabricInBed)
                If ConditionTrue(opcodeInBed And &HF) Then BranchRelative(displacementInBed, operand32InBed)
            Case &H80 To &H83
                ExecuteGroup1(opcodeInBed, operandWidthInBed, address32InBed, prefixesInBed, fabricInBed)
            Case &H84, &H85
                RejectLock(prefixesInBed)
                Dim widthInBed As Integer = If(opcodeInBed = &H84, 8, operandWidthInBed)
                Dim modRmInBed As Intel486DecodedModRm = DecodeModRm(address32InBed, fabricInBed)
                LogicResult(ReadRm(modRmInBed, widthInBed, prefixesInBed, fabricInBed) And ReadRegisterByWidth(modRmInBed.RegField, widthInBed), widthInBed)
            Case &H86, &H87
                ExecuteXchgRm(opcodeInBed, operandWidthInBed, address32InBed, prefixesInBed, fabricInBed)
            Case &H88 To &H8B
                RejectLock(prefixesInBed)
                ExecuteMovRmReg(opcodeInBed, operandWidthInBed, address32InBed, prefixesInBed, fabricInBed)
            Case &H8C
                RejectLock(prefixesInBed)
                ExecuteMovFromSegment(address32InBed, prefixesInBed, fabricInBed)
            Case &H8D
                RejectLock(prefixesInBed)
                ExecuteLea(operandWidthInBed, address32InBed, fabricInBed)
            Case &H8E
                RejectLock(prefixesInBed)
                ExecuteMovToSegment(address32InBed, prefixesInBed, fabricInBed)
            Case &H8F
                RejectLock(prefixesInBed)
                ExecutePopRm(operandWidthInBed, address32InBed, prefixesInBed, fabricInBed)
            Case &H90
                RejectLock(prefixesInBed) ' NOP / XCHG EAX,EAX
            Case &H91 To &H97
                RejectLock(prefixesInBed)
                Dim regInBed As Integer = opcodeInBed And 7
                Dim accumulatorInBed As UInteger = ReadRegisterByWidth(0, operandWidthInBed)
                Dim otherInBed As UInteger = ReadRegisterByWidth(regInBed, operandWidthInBed)
                WriteRegisterByWidth(0, operandWidthInBed, otherInBed)
                WriteRegisterByWidth(regInBed, operandWidthInBed, accumulatorInBed)
            Case &H98
                RejectLock(prefixesInBed)
                If operand32InBed Then
                    _cpuInBed.WriteGeneralRegister32(0, UnsignedBits(SignedValue(_cpuInBed.ReadGeneralRegister16(0), 16), 32)) ' CWDE
                Else
                    _cpuInBed.WriteGeneralRegister16(0, CUShort(UnsignedBits(SignedValue(_cpuInBed.ReadGeneralRegister8(0), 8), 16))) ' CBW
                End If
            Case &H99
                RejectLock(prefixesInBed)
                If operand32InBed Then
                    _cpuInBed.WriteGeneralRegister32(2, If(SignedValue(_cpuInBed.ReadGeneralRegister32(0), 32) < 0, &HFFFFFFFFUI, 0UI)) ' CDQ
                Else
                    _cpuInBed.WriteGeneralRegister16(2, CUShort(If(SignedValue(_cpuInBed.ReadGeneralRegister16(0), 16) < 0, &HFFFF, 0))) ' CWD
                End If
            Case &H9A
                RejectLock(prefixesInBed)
                ExecuteFarCallImmediate(operandWidthInBed, fabricInBed)
            Case &H9B
                RejectLock(prefixesInBed)
                ExecuteWait()
            Case &H9C
                RejectLock(prefixesInBed)
                PushFlags(operandWidthInBed, fabricInBed)
            Case &H9D
                RejectLock(prefixesInBed)
                PopFlags(operandWidthInBed, fabricInBed)
            Case &H9E
                RejectLock(prefixesInBed)
                ExecuteSahf()
            Case &H9F
                RejectLock(prefixesInBed)
                ExecuteLahf()
            Case &HA0 To &HA3
                RejectLock(prefixesInBed)
                ExecuteMovMoffs(opcodeInBed, operandWidthInBed, address32InBed, prefixesInBed, fabricInBed)
            Case &HA4 To &HA7, &HAA To &HAF
                RejectLock(prefixesInBed)
                ExecuteStringMemory(opcodeInBed, operandWidthInBed, address32InBed, prefixesInBed, fabricInBed)
            Case &HA8, &HA9
                RejectLock(prefixesInBed)
                Dim widthInBed As Integer = If(opcodeInBed = &HA8, 8, operandWidthInBed)
                LogicResult(ReadRegisterByWidth(0, widthInBed) And FetchImmediate(widthInBed, fabricInBed), widthInBed)
            Case &HB0 To &HB7
                RejectLock(prefixesInBed)
                _cpuInBed.WriteGeneralRegister8(opcodeInBed And 7, FetchCodeByte(fabricInBed))
            Case &HB8 To &HBF
                RejectLock(prefixesInBed)
                WriteRegisterByWidth(opcodeInBed And 7, operandWidthInBed, FetchImmediate(operandWidthInBed, fabricInBed))
            Case &HC0, &HC1
                ExecuteShiftGroup(opcodeInBed, operandWidthInBed, address32InBed, prefixesInBed, FetchCodeByte(fabricInBed), fabricInBed)
            Case &HC2
                RejectLock(prefixesInBed)
                ExecuteNearReturn(operandWidthInBed, FetchCodeWord(fabricInBed), fabricInBed)
            Case &HC3
                RejectLock(prefixesInBed)
                ExecuteNearReturn(operandWidthInBed, 0US, fabricInBed)
            Case &HC4
                RejectLock(prefixesInBed)
                ExecuteLoadFarPointer(Intel486SegmentRegister.ES, operandWidthInBed, address32InBed, prefixesInBed, fabricInBed)
            Case &HC5
                RejectLock(prefixesInBed)
                ExecuteLoadFarPointer(Intel486SegmentRegister.DS, operandWidthInBed, address32InBed, prefixesInBed, fabricInBed)
            Case &HC6, &HC7
                RejectLock(prefixesInBed)
                ExecuteMovImmediateRm(opcodeInBed, operandWidthInBed, address32InBed, prefixesInBed, fabricInBed)
            Case &HC8
                RejectLock(prefixesInBed)
                ExecuteEnter(operandWidthInBed, FetchCodeWord(fabricInBed), FetchCodeByte(fabricInBed), fabricInBed)
            Case &HC9
                RejectLock(prefixesInBed)
                ExecuteLeave(operandWidthInBed, fabricInBed)
            Case &HCA
                RejectLock(prefixesInBed)
                ExecuteFarReturn(operandWidthInBed, FetchCodeWord(fabricInBed), fabricInBed)
            Case &HCB
                RejectLock(prefixesInBed)
                ExecuteFarReturn(operandWidthInBed, 0US, fabricInBed)
            Case &HCC
                RejectLock(prefixesInBed)
                DeliverInterrupt(3, True, False, 0US, fabricInBed, False)
            Case &HCD
                RejectLock(prefixesInBed)
                Dim vectorInBed As Byte = FetchCodeByte(fabricInBed)
                If Virtual8086Mode AndAlso CurrentIopl() < 3 Then
                    ThrowFault(Intel486ExceptionVector.GeneralProtection, "INT n is IOPL-sensitive in virtual-8086 mode.", True, 0US)
                End If
                DeliverInterrupt(vectorInBed, True, False, 0US, fabricInBed, False)
            Case &HCE
                RejectLock(prefixesInBed)
                If FlagIsSet(FlagOf) Then DeliverInterrupt(4, True, False, 0US, fabricInBed, False)
            Case &HCF
                RejectLock(prefixesInBed)
                ExecuteIret(operandWidthInBed, fabricInBed)
                _nmiInServiceInBed = False
            Case &HD0 To &HD3
                Dim countInBed As Byte
                If opcodeInBed = &HD0 OrElse opcodeInBed = &HD1 Then countInBed = 1 Else countInBed = _cpuInBed.ReadGeneralRegister8(1)
                ExecuteShiftGroup(opcodeInBed, operandWidthInBed, address32InBed, prefixesInBed, countInBed, fabricInBed)
            Case &HD4
                RejectLock(prefixesInBed)
                ExecuteAam(FetchCodeByte(fabricInBed))
            Case &HD5
                RejectLock(prefixesInBed)
                ExecuteAad(FetchCodeByte(fabricInBed))
            Case &HD7
                RejectLock(prefixesInBed)
                ExecuteXlat(address32InBed, prefixesInBed, fabricInBed)
            Case &HD8 To &HDF
                RejectLock(prefixesInBed)
                ExecuteFloatingPointEscape(opcodeInBed, address32InBed, prefixesInBed, fabricInBed)
            Case &HE0 To &HE3
                RejectLock(prefixesInBed)
                ExecuteLoopFamily(opcodeInBed, address32InBed, fabricInBed)
            Case &HE4 To &HE7
                RejectLock(prefixesInBed)
                ExecuteImmediateIo(opcodeInBed, operandWidthInBed, fabricInBed)
            Case &HE8
                RejectLock(prefixesInBed)
                Dim returnInBed As UInteger = _cpuInBed.MutableEip
                Dim displacementInBed As Long = If(operand32InBed, FetchSignedDwordAsLong(fabricInBed), FetchSignedWord(fabricInBed))
                returnInBed = _cpuInBed.MutableEip
                PushValue(returnInBed, operandWidthInBed, fabricInBed)
                BranchRelative(displacementInBed, operand32InBed)
            Case &HE9
                RejectLock(prefixesInBed)
                Dim displacementInBed As Long = If(operand32InBed, FetchSignedDwordAsLong(fabricInBed), FetchSignedWord(fabricInBed))
                BranchRelative(displacementInBed, operand32InBed)
            Case &HEA
                RejectLock(prefixesInBed)
                ExecuteFarJumpImmediate(operandWidthInBed, fabricInBed)
            Case &HEB
                RejectLock(prefixesInBed)
                BranchRelative(FetchSignedByte(fabricInBed), operand32InBed)
            Case &HEC To &HEF
                RejectLock(prefixesInBed)
                ExecuteDxIo(opcodeInBed, operandWidthInBed, fabricInBed)
            Case &HF4
                RejectLock(prefixesInBed)
                EnforcePrivilegeZero("HLT")
                _haltedInBed = True
            Case &HF5
                RejectLock(prefixesInBed)
                SetFlag(FlagCf, Not FlagIsSet(FlagCf))
            Case &HF6, &HF7
                ExecuteGroup3(opcodeInBed, operandWidthInBed, address32InBed, prefixesInBed, fabricInBed)
            Case &HF8
                RejectLock(prefixesInBed) : SetFlag(FlagCf, False)
            Case &HF9
                RejectLock(prefixesInBed) : SetFlag(FlagCf, True)
            Case &HFA
                RejectLock(prefixesInBed) : EnforceIoplSensitive("CLI") : SetFlag(FlagIf, False)
            Case &HFB
                RejectLock(prefixesInBed) : EnforceIoplSensitive("STI") : SetFlag(FlagIf, True) : _maskableInterruptShadowInBed = 2
            Case &HFC
                RejectLock(prefixesInBed) : SetFlag(FlagDf, False)
            Case &HFD
                RejectLock(prefixesInBed) : SetFlag(FlagDf, True)
            Case &HFE
                ExecuteGroup4(address32InBed, prefixesInBed, fabricInBed)
            Case &HFF
                ExecuteGroup5(operandWidthInBed, address32InBed, prefixesInBed, fabricInBed)
            Case &HF
                RejectLock(prefixesInBed)
                Dim secondInBed As Byte = FetchCodeByte(fabricInBed)
                resultInBed.Opcode2 = secondInBed
                resultInBed.HasOpcode2 = True
                Execute0F(secondInBed, operandWidthInBed, operand32InBed, address32InBed, prefixesInBed, fabricInBed)
            Case Else
                ThrowFault(Intel486ExceptionVector.InvalidOpcode, "Undefined Intel486 opcode " & opcodeInBed.ToString("X2") & "h.")
        End Select
    End Sub

    Private Sub ExecuteAluFamily(opcodeInBed As Byte,
                                 operandWidthInBed As Integer,
                                 address32InBed As Boolean,
                                 prefixesInBed As Intel486PrefixState,
                                 fabricInBed As IProcessorLocalBusFabric)
        Dim operationInBed As Integer = (opcodeInBed >> 3) And 7
        Dim formInBed As Integer = opcodeInBed And 7
        Dim widthInBed As Integer = If((formInBed And 1) = 0, 8, operandWidthInBed)
        If formInBed >= 4 Then widthInBed = If(formInBed = 4, 8, operandWidthInBed)

        Select Case formInBed
            Case 0, 1
                Dim modRmInBed As Intel486DecodedModRm = DecodeModRm(address32InBed, fabricInBed)
                Dim leftInBed As UInteger = ReadRm(modRmInBed, widthInBed, prefixesInBed, fabricInBed)
                Dim rightInBed As UInteger = ReadRegisterByWidth(modRmInBed.RegField, widthInBed)
                Dim valueInBed As UInteger = ExecuteAluOperation(operationInBed, leftInBed, rightInBed, widthInBed)
                If operationInBed <> 7 Then
                    If prefixesInBed.LockPresent AndAlso modRmInBed.IsRegisterOperand Then ThrowFault(Intel486ExceptionVector.InvalidOpcode, "LOCK requires a memory destination.")
                    WriteRm(modRmInBed, widthInBed, valueInBed, prefixesInBed, fabricInBed, prefixesInBed.LockPresent)
                Else
                    RejectLock(prefixesInBed)
                End If
            Case 2, 3
                RejectLock(prefixesInBed)
                Dim modRmInBed As Intel486DecodedModRm = DecodeModRm(address32InBed, fabricInBed)
                Dim leftInBed As UInteger = ReadRegisterByWidth(modRmInBed.RegField, widthInBed)
                Dim rightInBed As UInteger = ReadRm(modRmInBed, widthInBed, prefixesInBed, fabricInBed)
                Dim valueInBed As UInteger = ExecuteAluOperation(operationInBed, leftInBed, rightInBed, widthInBed)
                If operationInBed <> 7 Then WriteRegisterByWidth(modRmInBed.RegField, widthInBed, valueInBed)
            Case 4, 5
                RejectLock(prefixesInBed)
                Dim rightInBed As UInteger = FetchImmediate(widthInBed, fabricInBed)
                Dim leftInBed As UInteger = ReadRegisterByWidth(0, widthInBed)
                Dim valueInBed As UInteger = ExecuteAluOperation(operationInBed, leftInBed, rightInBed, widthInBed)
                If operationInBed <> 7 Then WriteRegisterByWidth(0, widthInBed, valueInBed)
        End Select
    End Sub

    Private Sub RejectLock(prefixesInBed As Intel486PrefixState)
        If prefixesInBed.LockPresent Then ThrowFault(Intel486ExceptionVector.InvalidOpcode, "LOCK prefix is invalid for this Intel486 instruction form.")
    End Sub

    Private Sub ExecuteDaa()
        Dim oldAlInBed As Byte = _cpuInBed.ReadGeneralRegister8(0)
        Dim oldCfInBed As Boolean = FlagIsSet(FlagCf)
        Dim alInBed As Integer = oldAlInBed
        If (alInBed And &HF) > 9 OrElse FlagIsSet(FlagAf) Then
            alInBed = (alInBed + 6) And &HFF
            SetFlag(FlagAf, True)
        Else
            SetFlag(FlagAf, False)
        End If
        If oldAlInBed > &H99 OrElse oldCfInBed Then
            alInBed = (alInBed + &H60) And &HFF
            SetFlag(FlagCf, True)
        Else
            SetFlag(FlagCf, False)
        End If
        _cpuInBed.WriteGeneralRegister8(0, CByte(alInBed))
        SetSzpFlags(CUInt(alInBed), 8)
    End Sub

    Private Sub ExecuteDas()
        Dim oldAlInBed As Byte = _cpuInBed.ReadGeneralRegister8(0)
        Dim oldCfInBed As Boolean = FlagIsSet(FlagCf)
        Dim alInBed As Integer = oldAlInBed
        If (alInBed And &HF) > 9 OrElse FlagIsSet(FlagAf) Then
            alInBed = (alInBed - 6) And &HFF
            SetFlag(FlagAf, True)
        Else
            SetFlag(FlagAf, False)
        End If
        If oldAlInBed > &H99 OrElse oldCfInBed Then
            alInBed = (alInBed - &H60) And &HFF
            SetFlag(FlagCf, True)
        Else
            SetFlag(FlagCf, False)
        End If
        _cpuInBed.WriteGeneralRegister8(0, CByte(alInBed))
        SetSzpFlags(CUInt(alInBed), 8)
    End Sub

    Private Sub ExecuteAaa()
        Dim alInBed As Integer = _cpuInBed.ReadGeneralRegister8(0)
        Dim ahInBed As Integer = _cpuInBed.ReadGeneralRegister8(4)
        If (alInBed And &HF) > 9 OrElse FlagIsSet(FlagAf) Then
            alInBed = (alInBed + 6) And &HFF
            ahInBed = (ahInBed + 1) And &HFF
            SetFlag(FlagAf, True)
            SetFlag(FlagCf, True)
        Else
            SetFlag(FlagAf, False)
            SetFlag(FlagCf, False)
        End If
        _cpuInBed.WriteGeneralRegister8(0, CByte(alInBed And &HF))
        _cpuInBed.WriteGeneralRegister8(4, CByte(ahInBed))
    End Sub

    Private Sub ExecuteAas()
        Dim alInBed As Integer = _cpuInBed.ReadGeneralRegister8(0)
        Dim ahInBed As Integer = _cpuInBed.ReadGeneralRegister8(4)
        If (alInBed And &HF) > 9 OrElse FlagIsSet(FlagAf) Then
            alInBed = (alInBed - 6) And &HFF
            ahInBed = (ahInBed - 1) And &HFF
            SetFlag(FlagAf, True)
            SetFlag(FlagCf, True)
        Else
            SetFlag(FlagAf, False)
            SetFlag(FlagCf, False)
        End If
        _cpuInBed.WriteGeneralRegister8(0, CByte(alInBed And &HF))
        _cpuInBed.WriteGeneralRegister8(4, CByte(ahInBed))
    End Sub

    Private Sub ExecuteAam(baseInBed As Byte)
        If baseInBed = 0 Then ThrowFault(Intel486ExceptionVector.DivideError, "AAM base is zero.")
        Dim alInBed As Integer = _cpuInBed.ReadGeneralRegister8(0)
        _cpuInBed.WriteGeneralRegister8(4, CByte(alInBed \ baseInBed))
        _cpuInBed.WriteGeneralRegister8(0, CByte(alInBed Mod baseInBed))
        SetSzpFlags(_cpuInBed.ReadGeneralRegister8(0), 8)
    End Sub

    Private Sub ExecuteAad(baseInBed As Byte)
        Dim alInBed As Integer = _cpuInBed.ReadGeneralRegister8(0)
        Dim ahInBed As Integer = _cpuInBed.ReadGeneralRegister8(4)
        Dim resultInBed As Byte = CByte((alInBed + ahInBed * CInt(baseInBed)) And &HFF)
        _cpuInBed.WriteGeneralRegister8(0, resultInBed)
        _cpuInBed.WriteGeneralRegister8(4, 0)
        SetSzpFlags(resultInBed, 8)
    End Sub

    Private Sub ExecutePushAll(widthInBed As Integer, fabricInBed As IProcessorLocalBusFabric)
        Dim originalStackInBed As UInteger = ReadRegisterByWidth(4, widthInBed)
        PushValue(ReadRegisterByWidth(0, widthInBed), widthInBed, fabricInBed)
        PushValue(ReadRegisterByWidth(1, widthInBed), widthInBed, fabricInBed)
        PushValue(ReadRegisterByWidth(2, widthInBed), widthInBed, fabricInBed)
        PushValue(ReadRegisterByWidth(3, widthInBed), widthInBed, fabricInBed)
        PushValue(originalStackInBed, widthInBed, fabricInBed)
        PushValue(ReadRegisterByWidth(5, widthInBed), widthInBed, fabricInBed)
        PushValue(ReadRegisterByWidth(6, widthInBed), widthInBed, fabricInBed)
        PushValue(ReadRegisterByWidth(7, widthInBed), widthInBed, fabricInBed)
    End Sub

    Private Sub ExecutePopAll(widthInBed As Integer, fabricInBed As IProcessorLocalBusFabric)
        WriteRegisterByWidth(7, widthInBed, PopValue(widthInBed, fabricInBed))
        WriteRegisterByWidth(6, widthInBed, PopValue(widthInBed, fabricInBed))
        WriteRegisterByWidth(5, widthInBed, PopValue(widthInBed, fabricInBed))
        PopValue(widthInBed, fabricInBed) ' SP/ESP image is discarded.
        WriteRegisterByWidth(3, widthInBed, PopValue(widthInBed, fabricInBed))
        WriteRegisterByWidth(2, widthInBed, PopValue(widthInBed, fabricInBed))
        WriteRegisterByWidth(1, widthInBed, PopValue(widthInBed, fabricInBed))
        WriteRegisterByWidth(0, widthInBed, PopValue(widthInBed, fabricInBed))
    End Sub

    Private Sub ExecuteBound(widthInBed As Integer,
                             address32InBed As Boolean,
                             prefixesInBed As Intel486PrefixState,
                             fabricInBed As IProcessorLocalBusFabric)
        Dim modRmInBed As Intel486DecodedModRm = DecodeModRm(address32InBed, fabricInBed)
        If modRmInBed.IsRegisterOperand Then ThrowFault(Intel486ExceptionVector.InvalidOpcode, "BOUND requires a memory bounds operand.")
        Dim segmentInBed As Intel486SegmentRegister = EffectiveOperandSegment(modRmInBed, prefixesInBed)
        Dim candidateInBed As Long = SignedValue(ReadRegisterByWidth(modRmInBed.RegField, widthInBed), widthInBed)
        Dim lowerInBed As Long
        Dim upperInBed As Long
        If widthInBed = 16 Then
            lowerInBed = SignedValue(ReadMemoryWord(segmentInBed, modRmInBed.EffectiveOffset, fabricInBed), 16)
            upperInBed = SignedValue(ReadMemoryWord(segmentInBed, modRmInBed.EffectiveOffset + 2UI, fabricInBed), 16)
        Else
            lowerInBed = SignedValue(ReadMemoryDword(segmentInBed, modRmInBed.EffectiveOffset, fabricInBed), 32)
            upperInBed = SignedValue(ReadMemoryDword(segmentInBed, modRmInBed.EffectiveOffset + 4UI, fabricInBed), 32)
        End If
        If candidateInBed < lowerInBed OrElse candidateInBed > upperInBed Then ThrowFault(Intel486ExceptionVector.Bounds, "BOUND range exceeded.")
    End Sub

    Private Sub ExecuteArpl(address32InBed As Boolean,
                            prefixesInBed As Intel486PrefixState,
                            fabricInBed As IProcessorLocalBusFabric)
        If Not _mmuInBed.ProtectedModeEnabled OrElse Virtual8086Mode Then ThrowFault(Intel486ExceptionVector.InvalidOpcode, "ARPL is defined only in protected mode.")
        Dim modRmInBed As Intel486DecodedModRm = DecodeModRm(address32InBed, fabricInBed)
        Dim destinationInBed As UInt16 = CUShort(ReadRm(modRmInBed, 16, prefixesInBed, fabricInBed))
        Dim sourceInBed As UInt16 = _cpuInBed.ReadGeneralRegister16(modRmInBed.RegField)
        If (destinationInBed And 3US) < (sourceInBed And 3US) Then
            destinationInBed = CUShort((destinationInBed And &HFFFCUS) Or (sourceInBed And 3US))
            WriteRm(modRmInBed, 16, destinationInBed, prefixesInBed, fabricInBed)
            SetFlag(FlagZf, True)
        Else
            SetFlag(FlagZf, False)
        End If
    End Sub

    Private Sub ExecuteImulImmediate(widthInBed As Integer,
                                     address32InBed As Boolean,
                                     prefixesInBed As Intel486PrefixState,
                                     immediateIsByteInBed As Boolean,
                                     fabricInBed As IProcessorLocalBusFabric)
        Dim modRmInBed As Intel486DecodedModRm = DecodeModRm(address32InBed, fabricInBed)
        Dim leftInBed As Long = SignedValue(ReadRm(modRmInBed, widthInBed, prefixesInBed, fabricInBed), widthInBed)
        Dim rightInBed As Long
        If immediateIsByteInBed Then
            rightInBed = FetchSignedByte(fabricInBed)
        ElseIf widthInBed = 16 Then
            rightInBed = FetchSignedWord(fabricInBed)
        Else
            rightInBed = FetchSignedDwordAsLong(fabricInBed)
        End If
        Dim productInBed As BigInteger = New BigInteger(leftInBed) * New BigInteger(rightInBed)
        Dim maskInBed As BigInteger = If(widthInBed = 16, New BigInteger(&HFFFF), (BigInteger.One << 32) - BigInteger.One)
        Dim lowInBed As UInteger = CUInt(productInBed And maskInBed)
        WriteRegisterByWidth(modRmInBed.RegField, widthInBed, lowInBed)
        Dim signExtendedLowInBed As BigInteger = New BigInteger(SignedValue(lowInBed, widthInBed))
        Dim overflowInBed As Boolean = productInBed <> signExtendedLowInBed
        SetFlag(FlagCf, overflowInBed)
        SetFlag(FlagOf, overflowInBed)
    End Sub

    Private Sub ExecuteGroup1(opcodeInBed As Byte,
                              operandWidthInBed As Integer,
                              address32InBed As Boolean,
                              prefixesInBed As Intel486PrefixState,
                              fabricInBed As IProcessorLocalBusFabric)
        Dim widthInBed As Integer = If(opcodeInBed = &H80 OrElse opcodeInBed = &H82, 8, operandWidthInBed)
        Dim modRmInBed As Intel486DecodedModRm = DecodeModRm(address32InBed, fabricInBed)
        Dim immediateInBed As UInteger
        If opcodeInBed = &H83 Then
            immediateInBed = UnsignedBits(FetchSignedByte(fabricInBed), widthInBed)
        Else
            immediateInBed = FetchImmediate(widthInBed, fabricInBed)
        End If
        Dim operationInBed As Integer = modRmInBed.RegField
        If prefixesInBed.LockPresent AndAlso (operationInBed = 7 OrElse modRmInBed.IsRegisterOperand) Then
            ThrowFault(Intel486ExceptionVector.InvalidOpcode, "LOCK is invalid for this Group 1 form.")
        End If
        Dim oldInBed As UInteger = ReadRm(modRmInBed, widthInBed, prefixesInBed, fabricInBed)
        Dim newInBed As UInteger = ExecuteAluOperation(operationInBed, oldInBed, immediateInBed, widthInBed)
        If operationInBed <> 7 Then WriteRm(modRmInBed, widthInBed, newInBed, prefixesInBed, fabricInBed, prefixesInBed.LockPresent)
    End Sub

    Private Sub ExecuteXchgRm(opcodeInBed As Byte,
                              operandWidthInBed As Integer,
                              address32InBed As Boolean,
                              prefixesInBed As Intel486PrefixState,
                              fabricInBed As IProcessorLocalBusFabric)
        Dim widthInBed As Integer = If(opcodeInBed = &H86, 8, operandWidthInBed)
        Dim modRmInBed As Intel486DecodedModRm = DecodeModRm(address32InBed, fabricInBed)
        Dim leftInBed As UInteger = ReadRm(modRmInBed, widthInBed, prefixesInBed, fabricInBed)
        Dim rightInBed As UInteger = ReadRegisterByWidth(modRmInBed.RegField, widthInBed)
        WriteRegisterByWidth(modRmInBed.RegField, widthInBed, leftInBed)
        ' XCHG with memory is implicitly locked on the Intel486 even without F0h.
        WriteRm(modRmInBed, widthInBed, rightInBed, prefixesInBed, fabricInBed, Not modRmInBed.IsRegisterOperand)
    End Sub

    Private Sub ExecuteMovRmReg(opcodeInBed As Byte,
                                operandWidthInBed As Integer,
                                address32InBed As Boolean,
                                prefixesInBed As Intel486PrefixState,
                                fabricInBed As IProcessorLocalBusFabric)
        Dim widthInBed As Integer = If((opcodeInBed And 1) = 0, 8, operandWidthInBed)
        Dim modRmInBed As Intel486DecodedModRm = DecodeModRm(address32InBed, fabricInBed)
        If (opcodeInBed And 2) = 0 Then
            WriteRm(modRmInBed, widthInBed, ReadRegisterByWidth(modRmInBed.RegField, widthInBed), prefixesInBed, fabricInBed)
        Else
            WriteRegisterByWidth(modRmInBed.RegField, widthInBed, ReadRm(modRmInBed, widthInBed, prefixesInBed, fabricInBed))
        End If
    End Sub

    Private Function SegmentRegisterFromEncoding(encodingInBed As Integer, allowCsInBed As Boolean) As Intel486SegmentRegister
        Select Case encodingInBed And 7
            Case 0 : Return Intel486SegmentRegister.ES
            Case 1
                If allowCsInBed Then Return Intel486SegmentRegister.CS
                ThrowFault(Intel486ExceptionVector.InvalidOpcode, "MOV cannot load CS.")
            Case 2 : Return Intel486SegmentRegister.SS
            Case 3 : Return Intel486SegmentRegister.DS
            Case 4 : Return Intel486SegmentRegister.FS
            Case 5 : Return Intel486SegmentRegister.GS
            Case Else
                ThrowFault(Intel486ExceptionVector.InvalidOpcode, "Invalid segment register encoding.")
                Return Intel486SegmentRegister.DS
        End Select
    End Function

    Private Sub ExecuteMovFromSegment(address32InBed As Boolean,
                                      prefixesInBed As Intel486PrefixState,
                                      fabricInBed As IProcessorLocalBusFabric)
        Dim modRmInBed As Intel486DecodedModRm = DecodeModRm(address32InBed, fabricInBed)
        Dim segmentInBed As Intel486SegmentRegister = SegmentRegisterFromEncoding(modRmInBed.RegField, True)
        WriteRm(modRmInBed, 16, _mmuInBed.GetSegment(segmentInBed).Selector, prefixesInBed, fabricInBed)
    End Sub

    Private Sub ExecuteLea(widthInBed As Integer, address32InBed As Boolean, fabricInBed As IProcessorLocalBusFabric)
        Dim modRmInBed As Intel486DecodedModRm = DecodeModRm(address32InBed, fabricInBed)
        If modRmInBed.IsRegisterOperand Then ThrowFault(Intel486ExceptionVector.InvalidOpcode, "LEA requires a memory addressing form.")
        WriteRegisterByWidth(modRmInBed.RegField, widthInBed, modRmInBed.EffectiveOffset)
    End Sub

    Private Sub ExecuteMovToSegment(address32InBed As Boolean,
                                    prefixesInBed As Intel486PrefixState,
                                    fabricInBed As IProcessorLocalBusFabric)
        Dim modRmInBed As Intel486DecodedModRm = DecodeModRm(address32InBed, fabricInBed)
        Dim segmentInBed As Intel486SegmentRegister = SegmentRegisterFromEncoding(modRmInBed.RegField, False)
        Dim selectorInBed As UInt16 = CUShort(ReadRm(modRmInBed, 16, prefixesInBed, fabricInBed))
        LoadSegmentSelector(segmentInBed, selectorInBed, fabricInBed)
    End Sub

    Private Sub ExecutePopRm(widthInBed As Integer,
                             address32InBed As Boolean,
                             prefixesInBed As Intel486PrefixState,
                             fabricInBed As IProcessorLocalBusFabric)
        Dim modRmInBed As Intel486DecodedModRm = DecodeModRm(address32InBed, fabricInBed)
        If modRmInBed.RegField <> 0 Then ThrowFault(Intel486ExceptionVector.InvalidOpcode, "8F group opcode requires /0.")
        Dim valueInBed As UInteger = PopValue(widthInBed, fabricInBed)
        WriteRm(modRmInBed, widthInBed, valueInBed, prefixesInBed, fabricInBed)
    End Sub

    Private Sub LoadSegmentFromStack(segmentInBed As Intel486SegmentRegister, widthInBed As Integer, fabricInBed As IProcessorLocalBusFabric)
        Dim selectorInBed As UInt16 = CUShort(PopValue(widthInBed, fabricInBed) And &HFFFFUI)
        LoadSegmentSelector(segmentInBed, selectorInBed, fabricInBed)
    End Sub

    Private Sub ExecuteMovImmediateRm(opcodeInBed As Byte,
                                      operandWidthInBed As Integer,
                                      address32InBed As Boolean,
                                      prefixesInBed As Intel486PrefixState,
                                      fabricInBed As IProcessorLocalBusFabric)
        Dim widthInBed As Integer = If(opcodeInBed = &HC6, 8, operandWidthInBed)
        Dim modRmInBed As Intel486DecodedModRm = DecodeModRm(address32InBed, fabricInBed)
        If modRmInBed.RegField <> 0 Then ThrowFault(Intel486ExceptionVector.InvalidOpcode, "MOV immediate r/m requires /0.")
        WriteRm(modRmInBed, widthInBed, FetchImmediate(widthInBed, fabricInBed), prefixesInBed, fabricInBed)
    End Sub

    Private Sub ExecuteMovMoffs(opcodeInBed As Byte,
                                operandWidthInBed As Integer,
                                address32InBed As Boolean,
                                prefixesInBed As Intel486PrefixState,
                                fabricInBed As IProcessorLocalBusFabric)
        Dim offsetInBed As UInteger = If(address32InBed, FetchCodeDword(fabricInBed), CUInt(FetchCodeWord(fabricInBed)))
        Dim segmentInBed As Intel486SegmentRegister = If(prefixesInBed.SegmentOverridePresent, prefixesInBed.SegmentOverride, Intel486SegmentRegister.DS)
        Dim widthInBed As Integer = If(opcodeInBed = &HA0 OrElse opcodeInBed = &HA2, 8, operandWidthInBed)
        If opcodeInBed = &HA0 OrElse opcodeInBed = &HA1 Then
            Dim valueInBed As UInteger
            If widthInBed = 8 Then
                valueInBed = ReadMemoryByte(segmentInBed, offsetInBed, fabricInBed)
            ElseIf widthInBed = 16 Then
                valueInBed = ReadMemoryWord(segmentInBed, offsetInBed, fabricInBed)
            Else
                valueInBed = ReadMemoryDword(segmentInBed, offsetInBed, fabricInBed)
            End If
            WriteRegisterByWidth(0, widthInBed, valueInBed)
        Else
            Dim valueInBed As UInteger = ReadRegisterByWidth(0, widthInBed)
            If widthInBed = 8 Then
                WriteMemoryByte(segmentInBed, offsetInBed, CByte(valueInBed), fabricInBed)
            ElseIf widthInBed = 16 Then
                WriteMemoryWord(segmentInBed, offsetInBed, CUShort(valueInBed), fabricInBed)
            Else
                WriteMemoryDword(segmentInBed, offsetInBed, valueInBed, fabricInBed)
            End If
        End If
    End Sub

    Private Function ReadIndexRegister(sourceInBed As Boolean, address32InBed As Boolean) As UInteger
        Dim registerInBed As Integer = If(sourceInBed, 6, 7)
        If address32InBed Then Return _cpuInBed.ReadGeneralRegister32(registerInBed)
        Return _cpuInBed.ReadGeneralRegister16(registerInBed)
    End Function

    Private Sub WriteIndexRegister(sourceInBed As Boolean, address32InBed As Boolean, valueInBed As UInteger)
        Dim registerInBed As Integer = If(sourceInBed, 6, 7)
        If address32InBed Then
            _cpuInBed.WriteGeneralRegister32(registerInBed, valueInBed)
        Else
            _cpuInBed.WriteGeneralRegister16(registerInBed, CUShort(valueInBed And &HFFFFUI))
        End If
    End Sub

    Private Function ReadRepeatCount(address32InBed As Boolean) As UInteger
        If address32InBed Then Return _cpuInBed.ReadGeneralRegister32(1)
        Return _cpuInBed.ReadGeneralRegister16(1)
    End Function

    Private Sub WriteRepeatCount(address32InBed As Boolean, valueInBed As UInteger)
        If address32InBed Then
            _cpuInBed.WriteGeneralRegister32(1, valueInBed)
        Else
            _cpuInBed.WriteGeneralRegister16(1, CUShort(valueInBed And &HFFFFUI))
        End If
    End Sub

    Private Function AdvanceStringIndex(indexInBed As UInteger, bytesInBed As Integer, address32InBed As Boolean) As UInteger
        Dim deltaInBed As Long = If(FlagIsSet(FlagDf), -bytesInBed, bytesInBed)
        If address32InBed Then Return UnsignedBits(CLng(indexInBed) + deltaInBed, 32)
        Return CUInt((CLng(indexInBed) + deltaInBed) And &HFFFFL)
    End Function

    Private Sub ExecuteStringMemory(opcodeInBed As Byte,
                                    operandWidthInBed As Integer,
                                    address32InBed As Boolean,
                                    prefixesInBed As Intel486PrefixState,
                                    fabricInBed As IProcessorLocalBusFabric)
        Dim elementWidthInBed As Integer = If((opcodeInBed And 1) = 0, 8, operandWidthInBed)
        Dim elementBytesInBed As Integer = elementWidthInBed \ 8
        Dim countInBed As UInteger = If(prefixesInBed.Repeat = Intel486RepeatPrefix.None, 1UI, ReadRepeatCount(address32InBed))
        If countInBed = 0UI Then Return

        Dim sourceSegmentInBed As Intel486SegmentRegister = If(prefixesInBed.SegmentOverridePresent, prefixesInBed.SegmentOverride, Intel486SegmentRegister.DS)
        Do While countInBed > 0UI
            Dim siInBed As UInteger = ReadIndexRegister(True, address32InBed)
            Dim diInBed As UInteger = ReadIndexRegister(False, address32InBed)
            Dim sourceValueInBed As UInteger = 0UI
            Dim destinationValueInBed As UInteger = 0UI

            Select Case opcodeInBed
                Case &HA4, &HA5 ' MOVS
                    sourceValueInBed = ReadMemoryByWidth(sourceSegmentInBed, siInBed, elementWidthInBed, fabricInBed)
                    WriteMemoryByWidth(Intel486SegmentRegister.ES, diInBed, elementWidthInBed, sourceValueInBed, fabricInBed)
                    WriteIndexRegister(True, address32InBed, AdvanceStringIndex(siInBed, elementBytesInBed, address32InBed))
                    WriteIndexRegister(False, address32InBed, AdvanceStringIndex(diInBed, elementBytesInBed, address32InBed))
                Case &HA6, &HA7 ' CMPS
                    sourceValueInBed = ReadMemoryByWidth(sourceSegmentInBed, siInBed, elementWidthInBed, fabricInBed)
                    destinationValueInBed = ReadMemoryByWidth(Intel486SegmentRegister.ES, diInBed, elementWidthInBed, fabricInBed)
                    SubtractValues(sourceValueInBed, destinationValueInBed, 0UI, elementWidthInBed)
                    WriteIndexRegister(True, address32InBed, AdvanceStringIndex(siInBed, elementBytesInBed, address32InBed))
                    WriteIndexRegister(False, address32InBed, AdvanceStringIndex(diInBed, elementBytesInBed, address32InBed))
                Case &HAA, &HAB ' STOS
                    WriteMemoryByWidth(Intel486SegmentRegister.ES, diInBed, elementWidthInBed, ReadRegisterByWidth(0, elementWidthInBed), fabricInBed)
                    WriteIndexRegister(False, address32InBed, AdvanceStringIndex(diInBed, elementBytesInBed, address32InBed))
                Case &HAC, &HAD ' LODS
                    sourceValueInBed = ReadMemoryByWidth(sourceSegmentInBed, siInBed, elementWidthInBed, fabricInBed)
                    WriteRegisterByWidth(0, elementWidthInBed, sourceValueInBed)
                    WriteIndexRegister(True, address32InBed, AdvanceStringIndex(siInBed, elementBytesInBed, address32InBed))
                Case &HAE, &HAF ' SCAS
                    destinationValueInBed = ReadMemoryByWidth(Intel486SegmentRegister.ES, diInBed, elementWidthInBed, fabricInBed)
                    SubtractValues(ReadRegisterByWidth(0, elementWidthInBed), destinationValueInBed, 0UI, elementWidthInBed)
                    WriteIndexRegister(False, address32InBed, AdvanceStringIndex(diInBed, elementBytesInBed, address32InBed))
            End Select

            If prefixesInBed.Repeat = Intel486RepeatPrefix.None Then Exit Do
            countInBed -= 1UI
            WriteRepeatCount(address32InBed, countInBed)
            If opcodeInBed = &HA6 OrElse opcodeInBed = &HA7 OrElse opcodeInBed = &HAE OrElse opcodeInBed = &HAF Then
                If prefixesInBed.Repeat = Intel486RepeatPrefix.Repe AndAlso Not FlagIsSet(FlagZf) Then Exit Do
                If prefixesInBed.Repeat = Intel486RepeatPrefix.Repne AndAlso FlagIsSet(FlagZf) Then Exit Do
            End If
        Loop
    End Sub

    Private Sub ExecuteStringIo(opcodeInBed As Byte,
                                operandWidthInBed As Integer,
                                address32InBed As Boolean,
                                prefixesInBed As Intel486PrefixState,
                                fabricInBed As IProcessorLocalBusFabric)
        Dim elementWidthInBed As Integer = If((opcodeInBed And 1) = 0, 8, operandWidthInBed)
        Dim elementBytesInBed As Integer = elementWidthInBed \ 8
        Dim countInBed As UInteger = If(prefixesInBed.Repeat = Intel486RepeatPrefix.None, 1UI, ReadRepeatCount(address32InBed))
        If countInBed = 0UI Then Return
        Dim portInBed As UInt16 = _cpuInBed.ReadGeneralRegister16(2)
        Dim sourceSegmentInBed As Intel486SegmentRegister = If(prefixesInBed.SegmentOverridePresent, prefixesInBed.SegmentOverride, Intel486SegmentRegister.DS)
        Do While countInBed > 0UI
            If opcodeInBed = &H6C OrElse opcodeInBed = &H6D Then ' INS
                Dim diInBed As UInteger = ReadIndexRegister(False, address32InBed)
                Dim valueInBed As UInteger = ReadIo(portInBed, elementWidthInBed, fabricInBed)
                WriteMemoryByWidth(Intel486SegmentRegister.ES, diInBed, elementWidthInBed, valueInBed, fabricInBed)
                WriteIndexRegister(False, address32InBed, AdvanceStringIndex(diInBed, elementBytesInBed, address32InBed))
            Else ' OUTS
                Dim siInBed As UInteger = ReadIndexRegister(True, address32InBed)
                Dim valueInBed As UInteger = ReadMemoryByWidth(sourceSegmentInBed, siInBed, elementWidthInBed, fabricInBed)
                WriteIo(portInBed, elementWidthInBed, valueInBed, fabricInBed)
                WriteIndexRegister(True, address32InBed, AdvanceStringIndex(siInBed, elementBytesInBed, address32InBed))
            End If
            If prefixesInBed.Repeat = Intel486RepeatPrefix.None Then Exit Do
            countInBed -= 1UI
            WriteRepeatCount(address32InBed, countInBed)
        Loop
    End Sub

    Private Function ReadMemoryByWidth(segmentInBed As Intel486SegmentRegister,
                                       offsetInBed As UInteger,
                                       widthInBed As Integer,
                                       fabricInBed As IProcessorLocalBusFabric) As UInteger
        Select Case widthInBed
            Case 8 : Return ReadMemoryByte(segmentInBed, offsetInBed, fabricInBed)
            Case 16 : Return ReadMemoryWord(segmentInBed, offsetInBed, fabricInBed)
            Case 32 : Return ReadMemoryDword(segmentInBed, offsetInBed, fabricInBed)
            Case Else : Throw New ArgumentOutOfRangeException(NameOf(widthInBed))
        End Select
    End Function

    Private Sub WriteMemoryByWidth(segmentInBed As Intel486SegmentRegister,
                                   offsetInBed As UInteger,
                                   widthInBed As Integer,
                                   valueInBed As UInteger,
                                   fabricInBed As IProcessorLocalBusFabric)
        Select Case widthInBed
            Case 8 : WriteMemoryByte(segmentInBed, offsetInBed, CByte(valueInBed And &HFFUI), fabricInBed)
            Case 16 : WriteMemoryWord(segmentInBed, offsetInBed, CUShort(valueInBed And &HFFFFUI), fabricInBed)
            Case 32 : WriteMemoryDword(segmentInBed, offsetInBed, valueInBed, fabricInBed)
            Case Else : Throw New ArgumentOutOfRangeException(NameOf(widthInBed))
        End Select
    End Sub

    Private Sub ExecuteSahf()
        Dim ahInBed As UInteger = _cpuInBed.ReadGeneralRegister8(4)
        Dim preservedInBed As UInteger = _cpuInBed.MutableEflags And Not (FlagSf Or FlagZf Or FlagAf Or FlagPf Or FlagCf)
        _cpuInBed.MutableEflags = preservedInBed Or (ahInBed And &HD5UI) Or &H2UI
    End Sub

    Private Sub ExecuteLahf()
        Dim ahInBed As Byte = CByte((_cpuInBed.MutableEflags And &HD5UI) Or &H2UI)
        _cpuInBed.WriteGeneralRegister8(4, ahInBed)
    End Sub

    Private Sub PushFlags(widthInBed As Integer, fabricInBed As IProcessorLocalBusFabric)
        If Virtual8086Mode AndAlso CurrentIopl() < 3 Then
            ThrowFault(Intel486ExceptionVector.GeneralProtection, "PUSHF is IOPL-sensitive in virtual-8086 mode.", True, 0US)
        End If
        Dim flagsInBed As UInteger = _cpuInBed.MutableEflags
        If widthInBed = 16 Then flagsInBed = flagsInBed And &HFFFFUI
        PushValue(flagsInBed, widthInBed, fabricInBed)
    End Sub

    Private Sub PopFlags(widthInBed As Integer, fabricInBed As IProcessorLocalBusFabric)
        If Virtual8086Mode AndAlso CurrentIopl() < 3 Then ThrowFault(Intel486ExceptionVector.GeneralProtection, "POPF is IOPL-sensitive in virtual-8086 mode.", True, 0US)
        Dim incomingInBed As UInteger = PopValue(widthInBed, fabricInBed)
        Dim oldInBed As UInteger = _cpuInBed.MutableEflags
        Dim modifiableInBed As UInteger
        If widthInBed = 16 Then
            modifiableInBed = &H7FD5UI
        Else
            modifiableInBed = &H3F7FD5UI
        End If
        If _mmuInBed.ProtectedModeEnabled AndAlso _cplInBed > CurrentIopl() Then modifiableInBed = modifiableInBed And Not FlagIf
        If _cplInBed <> 0 Then modifiableInBed = modifiableInBed And Not (FlagIopl Or FlagVm)
        _cpuInBed.MutableEflags = (oldInBed And Not modifiableInBed) Or (incomingInBed And modifiableInBed)
    End Sub

    Private Function CurrentIopl() As Byte
        Return CByte((_cpuInBed.MutableEflags >> 12) And 3UI)
    End Function

    Private Sub ExecuteXlat(address32InBed As Boolean,
                            prefixesInBed As Intel486PrefixState,
                            fabricInBed As IProcessorLocalBusFabric)
        Dim baseInBed As UInteger = If(address32InBed, _cpuInBed.ReadGeneralRegister32(3), CUInt(_cpuInBed.ReadGeneralRegister16(3)))
        Dim offsetInBed As UInteger = If(address32InBed,
                                         baseInBed + _cpuInBed.ReadGeneralRegister8(0),
                                         (baseInBed + _cpuInBed.ReadGeneralRegister8(0)) And &HFFFFUI)
        Dim segmentInBed As Intel486SegmentRegister = If(prefixesInBed.SegmentOverridePresent, prefixesInBed.SegmentOverride, Intel486SegmentRegister.DS)
        _cpuInBed.WriteGeneralRegister8(0, ReadMemoryByte(segmentInBed, offsetInBed, fabricInBed))
    End Sub

    Private Sub ExecuteImmediateIo(opcodeInBed As Byte, operandWidthInBed As Integer, fabricInBed As IProcessorLocalBusFabric)
        Dim portInBed As UInt16 = FetchCodeByte(fabricInBed)
        Dim widthInBed As Integer = If((opcodeInBed And 1) = 0, 8, operandWidthInBed)
        If (opcodeInBed And 2) = 0 Then
            WriteRegisterByWidth(0, widthInBed, ReadIo(portInBed, widthInBed, fabricInBed))
        Else
            WriteIo(portInBed, widthInBed, ReadRegisterByWidth(0, widthInBed), fabricInBed)
        End If
    End Sub

    Private Sub ExecuteDxIo(opcodeInBed As Byte, operandWidthInBed As Integer, fabricInBed As IProcessorLocalBusFabric)
        Dim portInBed As UInt16 = _cpuInBed.ReadGeneralRegister16(2)
        Dim widthInBed As Integer = If((opcodeInBed And 1) = 0, 8, operandWidthInBed)
        If (opcodeInBed And 2) = 0 Then
            WriteRegisterByWidth(0, widthInBed, ReadIo(portInBed, widthInBed, fabricInBed))
        Else
            WriteIo(portInBed, widthInBed, ReadRegisterByWidth(0, widthInBed), fabricInBed)
        End If
    End Sub

    Private Sub ExecuteLoopFamily(opcodeInBed As Byte, address32InBed As Boolean, fabricInBed As IProcessorLocalBusFabric)
        Dim displacementInBed As Integer = FetchSignedByte(fabricInBed)
        If opcodeInBed = &HE3 Then
            If ReadRepeatCount(address32InBed) = 0UI Then BranchRelative(displacementInBed, address32InBed)
            Return
        End If
        Dim countInBed As UInteger = ReadRepeatCount(address32InBed)
        countInBed = If(address32InBed, countInBed - 1UI, (countInBed - 1UI) And &HFFFFUI)
        WriteRepeatCount(address32InBed, countInBed)
        Dim takeInBed As Boolean = countInBed <> 0UI
        If opcodeInBed = &HE0 Then takeInBed = takeInBed AndAlso Not FlagIsSet(FlagZf)
        If opcodeInBed = &HE1 Then takeInBed = takeInBed AndAlso FlagIsSet(FlagZf)
        If takeInBed Then BranchRelative(displacementInBed, address32InBed)
    End Sub

    Private Sub ExecuteGroup3(opcodeInBed As Byte,
                              operandWidthInBed As Integer,
                              address32InBed As Boolean,
                              prefixesInBed As Intel486PrefixState,
                              fabricInBed As IProcessorLocalBusFabric)
        Dim widthInBed As Integer = If(opcodeInBed = &HF6, 8, operandWidthInBed)
        Dim modRmInBed As Intel486DecodedModRm = DecodeModRm(address32InBed, fabricInBed)
        Dim operationInBed As Integer = modRmInBed.RegField

        If operationInBed = 0 Then
            RejectLock(prefixesInBed)
            LogicResult(ReadRm(modRmInBed, widthInBed, prefixesInBed, fabricInBed) And FetchImmediate(widthInBed, fabricInBed), widthInBed)
            Return
        End If
        If operationInBed = 1 Then ThrowFault(Intel486ExceptionVector.InvalidOpcode, "Group 3 /1 is undefined on Intel486.")

        If prefixesInBed.LockPresent AndAlso ((operationInBed <> 2 AndAlso operationInBed <> 3) OrElse modRmInBed.IsRegisterOperand) Then
            ThrowFault(Intel486ExceptionVector.InvalidOpcode, "LOCK is valid only for memory NOT/NEG in Group 3.")
        End If

        Dim sourceInBed As UInteger = ReadRm(modRmInBed, widthInBed, prefixesInBed, fabricInBed)
        Select Case operationInBed
            Case 2 ' NOT
                WriteRm(modRmInBed, widthInBed, (Not sourceInBed) And WidthMask(widthInBed), prefixesInBed, fabricInBed, prefixesInBed.LockPresent)
            Case 3 ' NEG
                Dim valueInBed As UInteger = SubtractValues(0UI, sourceInBed, 0UI, widthInBed)
                WriteRm(modRmInBed, widthInBed, valueInBed, prefixesInBed, fabricInBed, prefixesInBed.LockPresent)
            Case 4 ' MUL
                ExecuteUnsignedMultiply(sourceInBed, widthInBed)
            Case 5 ' IMUL one operand
                ExecuteSignedMultiply(sourceInBed, widthInBed)
            Case 6 ' DIV
                ExecuteUnsignedDivide(sourceInBed, widthInBed)
            Case 7 ' IDIV
                ExecuteSignedDivide(sourceInBed, widthInBed)
        End Select
    End Sub

    Private Sub ExecuteUnsignedMultiply(sourceInBed As UInteger, widthInBed As Integer)
        Select Case widthInBed
            Case 8
                Dim productInBed As UInteger = CUInt(_cpuInBed.ReadGeneralRegister8(0)) * (sourceInBed And &HFFUI)
                _cpuInBed.WriteGeneralRegister16(0, CUShort(productInBed And &HFFFFUI))
                Dim overflowInBed As Boolean = (productInBed And &HFF00UI) <> 0UI
                SetFlag(FlagCf, overflowInBed) : SetFlag(FlagOf, overflowInBed)
            Case 16
                Dim productInBed As UInteger = CUInt(_cpuInBed.ReadGeneralRegister16(0)) * (sourceInBed And &HFFFFUI)
                _cpuInBed.WriteGeneralRegister16(0, CUShort(productInBed And &HFFFFUI))
                _cpuInBed.WriteGeneralRegister16(2, CUShort((productInBed >> 16) And &HFFFFUI))
                Dim overflowInBed As Boolean = (productInBed And &HFFFF0000UI) <> 0UI
                SetFlag(FlagCf, overflowInBed) : SetFlag(FlagOf, overflowInBed)
            Case 32
                Dim productInBed As ULong = CULng(_cpuInBed.ReadGeneralRegister32(0)) * CULng(sourceInBed)
                _cpuInBed.WriteGeneralRegister32(0, CUInt(productInBed And &HFFFFFFFFUL))
                _cpuInBed.WriteGeneralRegister32(2, CUInt((productInBed >> 32) And &HFFFFFFFFUL))
                Dim overflowInBed As Boolean = (productInBed >> 32) <> 0UL
                SetFlag(FlagCf, overflowInBed) : SetFlag(FlagOf, overflowInBed)
        End Select
    End Sub

    Private Sub ExecuteSignedMultiply(sourceInBed As UInteger, widthInBed As Integer)
        Dim productInBed As BigInteger = New BigInteger(SignedValue(ReadRegisterByWidth(0, If(widthInBed = 8, 8, widthInBed)), If(widthInBed = 8, 8, widthInBed))) *
                                             New BigInteger(SignedValue(sourceInBed, widthInBed))
        Select Case widthInBed
            Case 8
                Dim bitsInBed As UInteger = CUInt(productInBed And New BigInteger(&HFFFF))
                _cpuInBed.WriteGeneralRegister16(0, CUShort(bitsInBed))
            Case 16
                Dim bitsInBed As UInteger = CUInt(productInBed And New BigInteger(&HFFFFFFFFUI))
                _cpuInBed.WriteGeneralRegister16(0, CUShort(bitsInBed And &HFFFFUI))
                _cpuInBed.WriteGeneralRegister16(2, CUShort((bitsInBed >> 16) And &HFFFFUI))
            Case 32
                Dim bits64InBed As BigInteger = productInBed And ((BigInteger.One << 64) - BigInteger.One)
                _cpuInBed.WriteGeneralRegister32(0, CUInt(bits64InBed And New BigInteger(&HFFFFFFFFUI)))
                _cpuInBed.WriteGeneralRegister32(2, CUInt((bits64InBed >> 32) And New BigInteger(&HFFFFFFFFUI)))
        End Select
        Dim minimumInBed As BigInteger = -(BigInteger.One << (widthInBed - 1))
        Dim maximumInBed As BigInteger = (BigInteger.One << (widthInBed - 1)) - BigInteger.One
        Dim overflowInBed As Boolean = productInBed < minimumInBed OrElse productInBed > maximumInBed
        SetFlag(FlagCf, overflowInBed) : SetFlag(FlagOf, overflowInBed)
    End Sub

    Private Sub ExecuteUnsignedDivide(divisorInBed As UInteger, widthInBed As Integer)
        divisorInBed = divisorInBed And WidthMask(widthInBed)
        If divisorInBed = 0UI Then ThrowFault(Intel486ExceptionVector.DivideError, "Unsigned division by zero.")
        Select Case widthInBed
            Case 8
                Dim dividendInBed As UInteger = _cpuInBed.ReadGeneralRegister16(0)
                Dim quotientInBed As UInteger = dividendInBed \ divisorInBed
                Dim remainderInBed As UInteger = dividendInBed Mod divisorInBed
                If quotientInBed > &HFFUI Then ThrowFault(Intel486ExceptionVector.DivideError, "DIV quotient overflow.")
                _cpuInBed.WriteGeneralRegister8(0, CByte(quotientInBed))
                _cpuInBed.WriteGeneralRegister8(4, CByte(remainderInBed))
            Case 16
                Dim dividendInBed As ULong = (CULng(_cpuInBed.ReadGeneralRegister16(2)) << 16) Or _cpuInBed.ReadGeneralRegister16(0)
                Dim quotientInBed As ULong = dividendInBed \ divisorInBed
                Dim remainderInBed As ULong = dividendInBed Mod divisorInBed
                If quotientInBed > &HFFFFUL Then ThrowFault(Intel486ExceptionVector.DivideError, "DIV quotient overflow.")
                _cpuInBed.WriteGeneralRegister16(0, CUShort(quotientInBed))
                _cpuInBed.WriteGeneralRegister16(2, CUShort(remainderInBed))
            Case 32
                Dim dividendInBed As BigInteger = (New BigInteger(_cpuInBed.ReadGeneralRegister32(2)) << 32) Or New BigInteger(_cpuInBed.ReadGeneralRegister32(0))
                Dim divisorBigInBed As New BigInteger(divisorInBed)
                Dim quotientInBed As BigInteger = dividendInBed / divisorBigInBed
                Dim remainderInBed As BigInteger = dividendInBed Mod divisorBigInBed
                If quotientInBed > New BigInteger(&HFFFFFFFFUI) Then ThrowFault(Intel486ExceptionVector.DivideError, "DIV quotient overflow.")
                _cpuInBed.WriteGeneralRegister32(0, CUInt(quotientInBed))
                _cpuInBed.WriteGeneralRegister32(2, CUInt(remainderInBed))
        End Select
    End Sub

    Private Sub ExecuteSignedDivide(divisorBitsInBed As UInteger, widthInBed As Integer)
        Dim divisorInBed As BigInteger = New BigInteger(SignedValue(divisorBitsInBed, widthInBed))
        If divisorInBed.IsZero Then ThrowFault(Intel486ExceptionVector.DivideError, "Signed division by zero.")
        Dim dividendInBed As BigInteger
        Select Case widthInBed
            Case 8
                dividendInBed = New BigInteger(SignedValue(_cpuInBed.ReadGeneralRegister16(0), 16))
            Case 16
                Dim bitsInBed As UInteger = (CUInt(_cpuInBed.ReadGeneralRegister16(2)) << 16) Or _cpuInBed.ReadGeneralRegister16(0)
                dividendInBed = New BigInteger(SignedValue(bitsInBed, 32))
            Case Else
                dividendInBed = (New BigInteger(SignedValue(_cpuInBed.ReadGeneralRegister32(2), 32)) << 32) + New BigInteger(_cpuInBed.ReadGeneralRegister32(0))
        End Select
        Dim quotientInBed As BigInteger = dividendInBed / divisorInBed
        Dim remainderInBed As BigInteger = dividendInBed Mod divisorInBed
        Dim minimumInBed As BigInteger = -(BigInteger.One << (widthInBed - 1))
        Dim maximumInBed As BigInteger = (BigInteger.One << (widthInBed - 1)) - BigInteger.One
        If quotientInBed < minimumInBed OrElse quotientInBed > maximumInBed Then ThrowFault(Intel486ExceptionVector.DivideError, "IDIV quotient overflow.")
        Select Case widthInBed
            Case 8
                _cpuInBed.WriteGeneralRegister8(0, CByte(CUInt(quotientInBed And New BigInteger(&HFF))))
                _cpuInBed.WriteGeneralRegister8(4, CByte(CUInt(remainderInBed And New BigInteger(&HFF))))
            Case 16
                _cpuInBed.WriteGeneralRegister16(0, CUShort(CUInt(quotientInBed And New BigInteger(&HFFFF))))
                _cpuInBed.WriteGeneralRegister16(2, CUShort(CUInt(remainderInBed And New BigInteger(&HFFFF))))
            Case 32
                _cpuInBed.WriteGeneralRegister32(0, CUInt(quotientInBed And New BigInteger(&HFFFFFFFFUI)))
                _cpuInBed.WriteGeneralRegister32(2, CUInt(remainderInBed And New BigInteger(&HFFFFFFFFUI)))
        End Select
    End Sub

    Private Sub ExecuteGroup4(address32InBed As Boolean, prefixesInBed As Intel486PrefixState, fabricInBed As IProcessorLocalBusFabric)
        Dim modRmInBed As Intel486DecodedModRm = DecodeModRm(address32InBed, fabricInBed)
        If modRmInBed.RegField > 1 Then ThrowFault(Intel486ExceptionVector.InvalidOpcode, "Undefined FE group operation.")
        If prefixesInBed.LockPresent AndAlso modRmInBed.IsRegisterOperand Then ThrowFault(Intel486ExceptionVector.InvalidOpcode, "LOCK requires memory INC/DEC.")
        Dim oldCfInBed As Boolean = FlagIsSet(FlagCf)
        Dim valueInBed As UInteger = ReadRm(modRmInBed, 8, prefixesInBed, fabricInBed)
        If modRmInBed.RegField = 0 Then valueInBed = AddValues(valueInBed, 1UI, 0UI, 8) Else valueInBed = SubtractValues(valueInBed, 1UI, 0UI, 8)
        SetFlag(FlagCf, oldCfInBed)
        WriteRm(modRmInBed, 8, valueInBed, prefixesInBed, fabricInBed, prefixesInBed.LockPresent)
    End Sub

    Private Sub ExecuteGroup5(widthInBed As Integer,
                              address32InBed As Boolean,
                              prefixesInBed As Intel486PrefixState,
                              fabricInBed As IProcessorLocalBusFabric)
        Dim modRmInBed As Intel486DecodedModRm = DecodeModRm(address32InBed, fabricInBed)
        Dim operationInBed As Integer = modRmInBed.RegField
        If prefixesInBed.LockPresent AndAlso ((operationInBed <> 0 AndAlso operationInBed <> 1) OrElse modRmInBed.IsRegisterOperand) Then
            ThrowFault(Intel486ExceptionVector.InvalidOpcode, "LOCK invalid for this FF group operation.")
        End If
        Select Case operationInBed
            Case 0, 1
                Dim oldCfInBed As Boolean = FlagIsSet(FlagCf)
                Dim valueInBed As UInteger = ReadRm(modRmInBed, widthInBed, prefixesInBed, fabricInBed)
                If operationInBed = 0 Then valueInBed = AddValues(valueInBed, 1UI, 0UI, widthInBed) Else valueInBed = SubtractValues(valueInBed, 1UI, 0UI, widthInBed)
                SetFlag(FlagCf, oldCfInBed)
                WriteRm(modRmInBed, widthInBed, valueInBed, prefixesInBed, fabricInBed, prefixesInBed.LockPresent)
            Case 2 ' near call
                RejectLock(prefixesInBed)
                Dim targetInBed As UInteger = ReadRm(modRmInBed, widthInBed, prefixesInBed, fabricInBed)
                PushValue(_cpuInBed.MutableEip, widthInBed, fabricInBed)
                _cpuInBed.MutableEip = targetInBed And WidthMask(widthInBed)
            Case 3 ' far call m16:16/32
                RejectLock(prefixesInBed)
                If modRmInBed.IsRegisterOperand Then ThrowFault(Intel486ExceptionVector.InvalidOpcode, "Far CALL requires memory pointer.")
                ExecuteFarMemoryTransfer(modRmInBed, prefixesInBed, widthInBed, True, fabricInBed)
            Case 4 ' near jmp
                RejectLock(prefixesInBed)
                _cpuInBed.MutableEip = ReadRm(modRmInBed, widthInBed, prefixesInBed, fabricInBed) And WidthMask(widthInBed)
            Case 5 ' far jmp
                RejectLock(prefixesInBed)
                If modRmInBed.IsRegisterOperand Then ThrowFault(Intel486ExceptionVector.InvalidOpcode, "Far JMP requires memory pointer.")
                ExecuteFarMemoryTransfer(modRmInBed, prefixesInBed, widthInBed, False, fabricInBed)
            Case 6 ' push
                RejectLock(prefixesInBed)
                PushValue(ReadRm(modRmInBed, widthInBed, prefixesInBed, fabricInBed), widthInBed, fabricInBed)
            Case Else
                ThrowFault(Intel486ExceptionVector.InvalidOpcode, "Undefined FF /7 operation.")
        End Select
    End Sub

    Private Sub ExecuteShiftGroup(opcodeInBed As Byte,
                                  operandWidthInBed As Integer,
                                  address32InBed As Boolean,
                                  prefixesInBed As Intel486PrefixState,
                                  countByteInBed As Byte,
                                  fabricInBed As IProcessorLocalBusFabric)
        Dim widthInBed As Integer = If((opcodeInBed And 1) = 0, 8, operandWidthInBed)
        Dim modRmInBed As Intel486DecodedModRm = DecodeModRm(address32InBed, fabricInBed)
        Dim operationInBed As Integer = modRmInBed.RegField
        If operationInBed = 6 Then ThrowFault(Intel486ExceptionVector.InvalidOpcode, "Shift group /6 is undefined on Intel486.")
        If prefixesInBed.LockPresent AndAlso modRmInBed.IsRegisterOperand Then ThrowFault(Intel486ExceptionVector.InvalidOpcode, "LOCK requires a memory shift/rotate operand.")
        Dim countInBed As Integer = countByteInBed And &H1F
        If countInBed = 0 Then Return
        If operationInBed = 2 OrElse operationInBed = 3 Then countInBed = countInBed Mod (widthInBed + 1)
        If operationInBed = 0 OrElse operationInBed = 1 Then countInBed = countInBed Mod widthInBed
        If countInBed = 0 Then Return

        Dim maskInBed As UInteger = WidthMask(widthInBed)
        Dim signInBed As UInteger = WidthSignBit(widthInBed)
        Dim valueInBed As UInteger = ReadRm(modRmInBed, widthInBed, prefixesInBed, fabricInBed) And maskInBed
        Dim originalInBed As UInteger = valueInBed
        Dim carryInBed As Boolean = FlagIsSet(FlagCf)

        Select Case operationInBed
            Case 0 ' ROL
                For nInBed As Integer = 1 To countInBed
                    Dim msbInBed As Boolean = (valueInBed And signInBed) <> 0UI
                    valueInBed = ((valueInBed << 1) And maskInBed) Or If(msbInBed, 1UI, 0UI)
                    carryInBed = msbInBed
                Next
                SetFlag(FlagCf, carryInBed)
                If countInBed = 1 Then SetFlag(FlagOf, ((valueInBed And signInBed) <> 0UI) Xor carryInBed)
            Case 1 ' ROR
                For nInBed As Integer = 1 To countInBed
                    Dim lsbInBed As Boolean = (valueInBed And 1UI) <> 0UI
                    valueInBed = (valueInBed >> 1) Or If(lsbInBed, signInBed, 0UI)
                    carryInBed = lsbInBed
                Next
                SetFlag(FlagCf, carryInBed)
                If countInBed = 1 Then SetFlag(FlagOf, ((valueInBed And signInBed) <> 0UI) Xor ((valueInBed And (signInBed >> 1)) <> 0UI))
            Case 2 ' RCL
                For nInBed As Integer = 1 To countInBed
                    Dim newCarryInBed As Boolean = (valueInBed And signInBed) <> 0UI
                    valueInBed = ((valueInBed << 1) And maskInBed) Or If(carryInBed, 1UI, 0UI)
                    carryInBed = newCarryInBed
                Next
                SetFlag(FlagCf, carryInBed)
                If countInBed = 1 Then SetFlag(FlagOf, ((valueInBed And signInBed) <> 0UI) Xor carryInBed)
            Case 3 ' RCR
                For nInBed As Integer = 1 To countInBed
                    Dim newCarryInBed As Boolean = (valueInBed And 1UI) <> 0UI
                    valueInBed = (valueInBed >> 1) Or If(carryInBed, signInBed, 0UI)
                    carryInBed = newCarryInBed
                Next
                SetFlag(FlagCf, carryInBed)
                If countInBed = 1 Then SetFlag(FlagOf, ((valueInBed And signInBed) <> 0UI) Xor ((valueInBed And (signInBed >> 1)) <> 0UI))
            Case 4 ' SHL/SAL
                For nInBed As Integer = 1 To countInBed
                    carryInBed = (valueInBed And signInBed) <> 0UI
                    valueInBed = (valueInBed << 1) And maskInBed
                Next
                SetFlag(FlagCf, carryInBed)
                If countInBed = 1 Then SetFlag(FlagOf, ((valueInBed And signInBed) <> 0UI) Xor carryInBed)
                SetSzpFlags(valueInBed, widthInBed)
            Case 5 ' SHR
                For nInBed As Integer = 1 To countInBed
                    carryInBed = (valueInBed And 1UI) <> 0UI
                    valueInBed >>= 1
                Next
                SetFlag(FlagCf, carryInBed)
                If countInBed = 1 Then SetFlag(FlagOf, (originalInBed And signInBed) <> 0UI)
                SetSzpFlags(valueInBed, widthInBed)
            Case 7 ' SAR
                For nInBed As Integer = 1 To countInBed
                    carryInBed = (valueInBed And 1UI) <> 0UI
                    Dim keepSignInBed As Boolean = (valueInBed And signInBed) <> 0UI
                    valueInBed >>= 1
                    If keepSignInBed Then valueInBed = valueInBed Or signInBed
                Next
                SetFlag(FlagCf, carryInBed)
                If countInBed = 1 Then SetFlag(FlagOf, False)
                SetSzpFlags(valueInBed, widthInBed)
        End Select
        WriteRm(modRmInBed, widthInBed, valueInBed, prefixesInBed, fabricInBed, prefixesInBed.LockPresent)
    End Sub

    Private Sub ExecuteNearReturn(widthInBed As Integer, popBytesInBed As UInt16, fabricInBed As IProcessorLocalBusFabric)
        _cpuInBed.MutableEip = PopValue(widthInBed, fabricInBed) And WidthMask(widthInBed)
        If popBytesInBed <> 0US Then SetStackPointer(GetStackPointer() + popBytesInBed)
    End Sub

    Private Sub ExecuteEnter(widthInBed As Integer, allocationInBed As UInt16, nestingLevelInBed As Byte, fabricInBed As IProcessorLocalBusFabric)
        nestingLevelInBed = CByte(nestingLevelInBed And &H1F)
        Dim framePointerInBed As UInteger = ReadRegisterByWidth(5, widthInBed)
        PushValue(framePointerInBed, widthInBed, fabricInBed)
        Dim frameTempInBed As UInteger = GetStackPointer()
        If nestingLevelInBed > 0 Then
            Dim walkInBed As UInteger = framePointerInBed
            For levelInBed As Integer = 1 To nestingLevelInBed - 1
                walkInBed = UnsignedBits(CLng(walkInBed) - (widthInBed \ 8), If(StackAddress32(), 32, 16))
                Dim ancestorInBed As UInteger = ReadMemoryByWidth(Intel486SegmentRegister.SS, walkInBed, widthInBed, fabricInBed)
                PushValue(ancestorInBed, widthInBed, fabricInBed)
            Next
            PushValue(frameTempInBed, widthInBed, fabricInBed)
        End If
        WriteRegisterByWidth(5, widthInBed, frameTempInBed)
        Dim newSpInBed As UInteger = UnsignedBits(CLng(GetStackPointer()) - CLng(allocationInBed), If(StackAddress32(), 32, 16))
        SetStackPointer(newSpInBed)
    End Sub

    Private Sub ExecuteLeave(widthInBed As Integer, fabricInBed As IProcessorLocalBusFabric)
        If StackAddress32() Then
            SetStackPointer(_cpuInBed.ReadGeneralRegister32(5))
        Else
            SetStackPointer(_cpuInBed.ReadGeneralRegister16(5))
        End If
        WriteRegisterByWidth(5, widthInBed, PopValue(widthInBed, fabricInBed))
    End Sub

    Private Sub ExecuteLoadFarPointer(segmentInBed As Intel486SegmentRegister,
                                      widthInBed As Integer,
                                      address32InBed As Boolean,
                                      prefixesInBed As Intel486PrefixState,
                                      fabricInBed As IProcessorLocalBusFabric)
        Dim modRmInBed As Intel486DecodedModRm = DecodeModRm(address32InBed, fabricInBed)
        If modRmInBed.IsRegisterOperand Then ThrowFault(Intel486ExceptionVector.InvalidOpcode, "Far pointer load requires a memory operand.")
        Dim sourceSegmentInBed As Intel486SegmentRegister = EffectiveOperandSegment(modRmInBed, prefixesInBed)
        Dim offsetInBed As UInteger = ReadMemoryByWidth(sourceSegmentInBed, modRmInBed.EffectiveOffset, widthInBed, fabricInBed)
        Dim selectorOffsetInBed As UInteger = modRmInBed.EffectiveOffset + CUInt(widthInBed \ 8)
        Dim selectorInBed As UInt16 = ReadMemoryWord(sourceSegmentInBed, selectorOffsetInBed, fabricInBed)
        LoadSegmentSelector(segmentInBed, selectorInBed, fabricInBed)
        WriteRegisterByWidth(modRmInBed.RegField, widthInBed, offsetInBed)
    End Sub

    Private Sub ExecuteFarCallImmediate(widthInBed As Integer, fabricInBed As IProcessorLocalBusFabric)
        Dim offsetInBed As UInteger = FetchImmediate(widthInBed, fabricInBed)
        Dim selectorInBed As UInt16 = FetchCodeWord(fabricInBed)
        ExecuteFarTransfer(selectorInBed, offsetInBed, True, widthInBed, fabricInBed)
    End Sub

    Private Sub ExecuteFarJumpImmediate(widthInBed As Integer, fabricInBed As IProcessorLocalBusFabric)
        Dim offsetInBed As UInteger = FetchImmediate(widthInBed, fabricInBed)
        Dim selectorInBed As UInt16 = FetchCodeWord(fabricInBed)
        ExecuteFarTransfer(selectorInBed, offsetInBed, False, widthInBed, fabricInBed)
    End Sub

    Private Sub ExecuteFarMemoryTransfer(modRmInBed As Intel486DecodedModRm,
                                         prefixesInBed As Intel486PrefixState,
                                         widthInBed As Integer,
                                         isCallInBed As Boolean,
                                         fabricInBed As IProcessorLocalBusFabric)
        Dim segmentInBed As Intel486SegmentRegister = EffectiveOperandSegment(modRmInBed, prefixesInBed)
        Dim offsetInBed As UInteger = ReadMemoryByWidth(segmentInBed, modRmInBed.EffectiveOffset, widthInBed, fabricInBed)
        Dim selectorInBed As UInt16 = ReadMemoryWord(segmentInBed, modRmInBed.EffectiveOffset + CUInt(widthInBed \ 8), fabricInBed)
        ExecuteFarTransfer(selectorInBed, offsetInBed, isCallInBed, widthInBed, fabricInBed)
    End Sub

    Private Sub ExecuteFarTransfer(selectorInBed As UInt16,
                                   offsetInBed As UInteger,
                                   isCallInBed As Boolean,
                                   widthInBed As Integer,
                                   fabricInBed As IProcessorLocalBusFabric)
        If Not _mmuInBed.ProtectedModeEnabled OrElse Virtual8086Mode Then
            If isCallInBed Then
                PushValue(_mmuInBed.GetSegment(Intel486SegmentRegister.CS).Selector, widthInBed, fabricInBed)
                PushValue(_cpuInBed.MutableEip, widthInBed, fabricInBed)
            End If
            _mmuInBed.LoadRealModeSegment(Intel486SegmentRegister.CS, selectorInBed)
            _cpuInBed.MutableEip = offsetInBed And WidthMask(widthInBed)
            Return
        End If
        ExecuteProtectedFarTransfer(selectorInBed, offsetInBed, isCallInBed, widthInBed, fabricInBed)
    End Sub

    Private Sub ExecuteFarReturn(widthInBed As Integer, popBytesInBed As UInt16, fabricInBed As IProcessorLocalBusFabric)
        If Not _mmuInBed.ProtectedModeEnabled OrElse Virtual8086Mode Then
            Dim offsetInBed As UInteger = PopValue(widthInBed, fabricInBed)
            Dim selectorInBed As UInt16 = CUShort(PopValue(widthInBed, fabricInBed) And &HFFFFUI)
            If popBytesInBed <> 0US Then SetStackPointer(GetStackPointer() + popBytesInBed)
            _mmuInBed.LoadRealModeSegment(Intel486SegmentRegister.CS, selectorInBed)
            _cpuInBed.MutableEip = offsetInBed And WidthMask(widthInBed)
            Return
        End If
        ExecuteProtectedFarReturn(widthInBed, popBytesInBed, fabricInBed)
    End Sub

    Private Sub ExecuteWait()
        Dim cr0InBed As UInteger = _mmuInBed.CR0
        If (cr0InBed And Cr0Ts) <> 0UI AndAlso (cr0InBed And Cr0Mp) <> 0UI Then ThrowFault(Intel486ExceptionVector.DeviceNotAvailable, "WAIT with CR0.TS=1 and MP=1.")
        Dim pendingInBed As Nullable(Of Boolean) = _cpuInBed.IntegratedFpu.FloatingErrorAsserted
        If pendingInBed.HasValue AndAlso pendingInBed.Value AndAlso (cr0InBed And Cr0Ne) <> 0UI Then ThrowFault(Intel486ExceptionVector.FloatingPoint, "Pending unmasked integrated FPU exception.")
    End Sub

    Private Sub ExecuteFloatingPointEscape(opcodeInBed As Byte,
                                           address32InBed As Boolean,
                                           prefixesInBed As Intel486PrefixState,
                                           fabricInBed As IProcessorLocalBusFabric)
        Dim cr0InBed As UInteger = _mmuInBed.CR0
        If (cr0InBed And (Cr0Em Or Cr0Ts)) <> 0UI Then ThrowFault(Intel486ExceptionVector.DeviceNotAvailable, "Floating-point instruction trapped by CR0.EM/TS.")
        ' Consume ModR/M so the CP05 front end can be validated without falsely
        ' claiming arithmetic that belongs to CP07.
        DecodeModRm(address32InBed, fabricInBed)
        Throw New NotSupportedException("Intel486 integrated x87 arithmetic/decode is activated at CP07; CP05 only owns the integer/system front end.")
    End Sub

    Private Sub EnforcePrivilegeZero(operationInBed As String)
        If _mmuInBed.ProtectedModeEnabled AndAlso (Virtual8086Mode OrElse _cplInBed <> 0) Then
            ThrowFault(Intel486ExceptionVector.GeneralProtection, operationInBed & " requires CPL 0.", True, 0US)
        End If
    End Sub

    Private Sub EnforceIoplSensitive(operationInBed As String)
        If Not _mmuInBed.ProtectedModeEnabled Then Return
        Dim effectiveCplInBed As Byte = If(Virtual8086Mode, CByte(3), _cplInBed)
        If effectiveCplInBed > CurrentIopl() Then ThrowFault(Intel486ExceptionVector.GeneralProtection, operationInBed & " is IOPL-sensitive.", True, 0US)
    End Sub

    Private Sub EnforceIoPermission(portInBed As UInt16, widthInBed As Integer, fabricInBed As IProcessorLocalBusFabric)
        If Not _mmuInBed.ProtectedModeEnabled Then Return
        Dim mustConsultBitmapInBed As Boolean = Virtual8086Mode OrElse _cplInBed > CurrentIopl()
        If Not mustConsultBitmapInBed Then Return
        If Not _taskRegisterInBed.Valid OrElse _taskRegisterInBed.TypeField <> &HB Then
            ThrowFault(Intel486ExceptionVector.GeneralProtection, "I/O permission bitmap unavailable.", True, 0US)
        End If
        If _taskRegisterInBed.Limit < &H67UI Then ThrowFault(Intel486ExceptionVector.GeneralProtection, "Current TSS has no I/O map base.", True, 0US)
        Dim mapBaseInBed As UInt16 = ReadSystemLinearWord(_taskRegisterInBed.BaseAddress + &H66UI, fabricInBed)
        Dim bytesInBed As Integer = widthInBed \ 8
        For offsetInBed As Integer = 0 To bytesInBed - 1
            Dim pInBed As UInteger = CUInt(portInBed) + CUInt(offsetInBed)
            Dim byteOffsetInBed As UInteger = CUInt(mapBaseInBed) + (pInBed >> 3)
            If byteOffsetInBed > _taskRegisterInBed.Limit Then ThrowFault(Intel486ExceptionVector.GeneralProtection, "I/O permission bitmap denies port outside TSS limit.", True, 0US)
            Dim mapInBed As Byte = ReadSystemLinearByte(_taskRegisterInBed.BaseAddress + byteOffsetInBed, fabricInBed)
            If (mapInBed And (1 << CInt(pInBed And 7UI))) <> 0 Then ThrowFault(Intel486ExceptionVector.GeneralProtection, "I/O permission bitmap denies port.", True, 0US)
        Next
    End Sub

    Private Function AcknowledgeExternalInterrupt(fabricInBed As IProcessorLocalBusFabric) As Byte
        Dim firstInBed As New ProcessorBusCycleRequest With {
            .Kind = ProcessorBusCycleKind.InterruptAcknowledge,
            .Width = ProcessorBusDataWidth.Width8,
            .LockAsserted = True,
            .PseudoLockAsserted = True
        }
        fabricInBed.ExecuteProcessorCycle(firstInBed)
        Dim secondInBed As ProcessorBusCycleResult = fabricInBed.ExecuteProcessorCycle(firstInBed)
        Return CByte(secondInBed.ReadData And &HFFUI)
    End Function


    ' -------------------------
    ' CP05 protected/system core
    ' -------------------------

    Private Shared Function SelectorIndex(selectorInBed As UInt16) As UInteger
        Return CUInt(selectorInBed >> 3)
    End Function

    Private Shared Function SelectorRpl(selectorInBed As UInt16) As Byte
        Return CByte(selectorInBed And 3US)
    End Function

    Private Shared Function SelectorIsNull(selectorInBed As UInt16) As Boolean
        Return (selectorInBed And &HFFFCUS) = 0US
    End Function

    Private Shared Function SelectorErrorCode(selectorInBed As UInt16, Optional externalInBed As Boolean = False) As UInt16
        Dim codeInBed As UInt16 = CUShort(selectorInBed And &HFFFCUS)
        If externalInBed Then codeInBed = CUShort(codeInBed Or 1US)
        Return codeInBed
    End Function

    Private Shared Function IdtErrorCode(vectorInBed As Byte, Optional externalInBed As Boolean = False) As UInt16
        Dim codeInBed As UInt16 = CUShort((CUInt(vectorInBed) << 3) Or 2UI)
        If externalInBed Then codeInBed = CUShort(codeInBed Or 1US)
        Return codeInBed
    End Function

    Private Function ParseDescriptor(rawLowInBed As UInteger,
                                     rawHighInBed As UInteger,
                                     descriptorAddressInBed As UInteger) As Intel486Descriptor
        Dim limitInBed As UInteger = (rawLowInBed And &HFFFFUI) Or (((rawHighInBed >> 16) And &HFUI) << 16)
        Dim granularityInBed As Boolean = (rawHighInBed And &H800000UI) <> 0UI
        If granularityInBed Then limitInBed = (limitInBed << 12) Or &HFFFUI

        Dim baseInBed As UInteger = ((rawLowInBed >> 16) And &HFFFFUI) Or
                                    ((rawHighInBed And &HFFUI) << 16) Or
                                    (rawHighInBed And &HFF000000UI)
        Return New Intel486Descriptor With {
            .DescriptorLinearAddress = descriptorAddressInBed,
            .RawLow = rawLowInBed,
            .RawHigh = rawHighInBed,
            .BaseAddress = baseInBed,
            .Limit = limitInBed,
            .TypeField = CByte((rawHighInBed >> 8) And &HFUI),
            .SystemBit = (rawHighInBed And &H1000UI) <> 0UI,
            .DescriptorPrivilegeLevel = CByte((rawHighInBed >> 13) And 3UI),
            .Present = (rawHighInBed And &H8000UI) <> 0UI,
            .Available = (rawHighInBed And &H100000UI) <> 0UI,
            .Default32 = (rawHighInBed And &H400000UI) <> 0UI,
            .Granularity = granularityInBed
        }
    End Function

    Private Function TryReadDescriptor(selectorInBed As UInt16,
                                       fabricInBed As IProcessorLocalBusFabric,
                                       ByRef descriptorOutInBed As Intel486Descriptor) As Boolean
        If SelectorIsNull(selectorInBed) Then Return False

        Dim tableInBed As Intel486DescriptorTableRegister
        If (selectorInBed And 4US) = 0US Then
            tableInBed = _gdtrInBed
        Else
            If Not _ldtrInBed.Valid OrElse Not _ldtrInBed.Present Then Return False
            tableInBed = New Intel486DescriptorTableRegister With {
                .BaseAddress = _ldtrInBed.BaseAddress,
                .Limit = CUShort(Math.Min(_ldtrInBed.Limit, &HFFFFUI))
            }
        End If

        Dim entryOffsetInBed As UInteger = SelectorIndex(selectorInBed) * 8UI
        If entryOffsetInBed + 7UI > tableInBed.Limit Then Return False
        Dim addressInBed As UInteger = CUInt((CULng(tableInBed.BaseAddress) + CULng(entryOffsetInBed)) And &HFFFFFFFFUL)
        descriptorOutInBed = ParseDescriptor(ReadSystemLinearDword(addressInBed, fabricInBed),
                                              ReadSystemLinearDword(addressInBed + 4UI, fabricInBed),
                                              addressInBed)
        Return True
    End Function

    Private Function ReadDescriptor(selectorInBed As UInt16,
                                    fabricInBed As IProcessorLocalBusFabric,
                                    faultVectorInBed As Intel486ExceptionVector,
                                    messageInBed As String,
                                    Optional externalInBed As Boolean = False) As Intel486Descriptor
        Dim descriptorInBed As Intel486Descriptor
        If Not TryReadDescriptor(selectorInBed, fabricInBed, descriptorInBed) Then
            ThrowFault(faultVectorInBed, messageInBed, True, SelectorErrorCode(selectorInBed, externalInBed))
        End If
        Return descriptorInBed
    End Function

    Private Function ReadIdtGate(vectorInBed As Byte,
                                 fabricInBed As IProcessorLocalBusFabric,
                                 externalInBed As Boolean) As Intel486Descriptor
        Dim offsetInBed As UInteger = CUInt(vectorInBed) * 8UI
        If offsetInBed + 7UI > _idtrInBed.Limit Then
            ThrowFault(Intel486ExceptionVector.GeneralProtection,
                       "IDT vector lies beyond IDTR limit.", True, IdtErrorCode(vectorInBed, externalInBed))
        End If
        Dim addressInBed As UInteger = CUInt((CULng(_idtrInBed.BaseAddress) + CULng(offsetInBed)) And &HFFFFFFFFUL)
        Return ParseDescriptor(ReadSystemLinearDword(addressInBed, fabricInBed),
                               ReadSystemLinearDword(addressInBed + 4UI, fabricInBed),
                               addressInBed)
    End Function

    Private Sub MarkDescriptorAccessed(ByRef descriptorInBed As Intel486Descriptor,
                                       fabricInBed As IProcessorLocalBusFabric)
        If Not descriptorInBed.IsCodeOrData OrElse descriptorInBed.Accessed Then Return
        descriptorInBed.RawHigh = descriptorInBed.RawHigh Or &H100UI
        descriptorInBed.TypeField = CByte(descriptorInBed.TypeField Or 1)
        WriteSystemLinearDword(descriptorInBed.DescriptorLinearAddress + 4UI,
                               descriptorInBed.RawHigh, fabricInBed, True)
    End Sub

    Private Sub MarkTssBusy(ByRef descriptorInBed As Intel486Descriptor,
                            busyInBed As Boolean,
                            fabricInBed As IProcessorLocalBusFabric)
        If Not (descriptorInBed.IsAvailable386Tss OrElse descriptorInBed.IsBusy386Tss) Then
            ThrowFault(Intel486ExceptionVector.InvalidTss, "Task descriptor is not a 386/486 TSS.", True, 0US)
        End If
        If busyInBed Then
            descriptorInBed.TypeField = &HB
            descriptorInBed.RawHigh = (descriptorInBed.RawHigh And Not &HF00UI) Or &HB00UI
        Else
            descriptorInBed.TypeField = &H9
            descriptorInBed.RawHigh = (descriptorInBed.RawHigh And Not &HF00UI) Or &H900UI
        End If
        WriteSystemLinearDword(descriptorInBed.DescriptorLinearAddress + 4UI,
                               descriptorInBed.RawHigh, fabricInBed, True)
    End Sub

    Private Function BuildSegmentCache(selectorInBed As UInt16,
                                       descriptorInBed As Intel486Descriptor) As Intel486SegmentCacheEntry
        Return New Intel486SegmentCacheEntry With {
            .Selector = selectorInBed,
            .BaseAddress = descriptorInBed.BaseAddress,
            .Limit = descriptorInBed.Limit,
            .AccessRights = CUShort((CUInt(descriptorInBed.TypeField) And &HFUI) Or
                                    If(descriptorInBed.SystemBit, &H10UI, 0UI) Or
                                    (CUInt(descriptorInBed.DescriptorPrivilegeLevel) << 5) Or
                                    If(descriptorInBed.Present, &H80UI, 0UI)),
            .Present = descriptorInBed.Present,
            .Default32 = descriptorInBed.Default32,
            .PageGranularity = descriptorInBed.Granularity,
            .ExpandDown = descriptorInBed.ExpandDownData,
            .Readable = If(descriptorInBed.Executable, descriptorInBed.ReadableCode, True),
            .Writable = descriptorInBed.WritableData,
            .Executable = descriptorInBed.Executable,
            .DescriptorPrivilegeLevel = descriptorInBed.DescriptorPrivilegeLevel
        }
    End Function

    Private Sub InstallNullDataSegment(segmentInBed As Intel486SegmentRegister)
        _mmuInBed.InstallProtectedModeSegment(segmentInBed, New Intel486SegmentCacheEntry With {
            .Selector = 0US,
            .Present = False,
            .Limit = 0UI,
            .BaseAddress = 0UI
        })
    End Sub

    Private Sub LoadSegmentSelector(segmentInBed As Intel486SegmentRegister,
                                    selectorInBed As UInt16,
                                    fabricInBed As IProcessorLocalBusFabric)
        If Not _mmuInBed.ProtectedModeEnabled OrElse Virtual8086Mode Then
            _mmuInBed.LoadRealModeSegment(segmentInBed, selectorInBed)
            If segmentInBed = Intel486SegmentRegister.SS Then _maskableInterruptShadowInBed = 2
            Return
        End If

        If segmentInBed = Intel486SegmentRegister.CS Then
            ThrowFault(Intel486ExceptionVector.GeneralProtection, "CS may only be loaded by a control transfer.", True, SelectorErrorCode(selectorInBed))
        End If

        If SelectorIsNull(selectorInBed) Then
            If segmentInBed = Intel486SegmentRegister.SS Then
                ThrowFault(Intel486ExceptionVector.GeneralProtection, "SS cannot be loaded with a null selector.", True, 0US)
            End If
            InstallNullDataSegment(segmentInBed)
            Return
        End If

        Dim descriptorInBed As Intel486Descriptor = ReadDescriptor(selectorInBed, fabricInBed,
                                                                   Intel486ExceptionVector.GeneralProtection,
                                                                   "Segment selector is outside its descriptor table.")
        Dim rplInBed As Byte = SelectorRpl(selectorInBed)
        If segmentInBed = Intel486SegmentRegister.SS Then
            If Not descriptorInBed.IsCodeOrData OrElse descriptorInBed.Executable OrElse Not descriptorInBed.WritableData Then
                ThrowFault(Intel486ExceptionVector.GeneralProtection, "SS must select a writable data segment.", True, SelectorErrorCode(selectorInBed))
            End If
            If rplInBed <> _cplInBed OrElse descriptorInBed.DescriptorPrivilegeLevel <> _cplInBed Then
                ThrowFault(Intel486ExceptionVector.GeneralProtection, "SS RPL/DPL must equal CPL.", True, SelectorErrorCode(selectorInBed))
            End If
            If Not descriptorInBed.Present Then
                ThrowFault(Intel486ExceptionVector.StackFault, "Stack segment is not present.", True, SelectorErrorCode(selectorInBed))
            End If
        Else
            If Not descriptorInBed.IsCodeOrData OrElse (descriptorInBed.Executable AndAlso Not descriptorInBed.ReadableCode) Then
                ThrowFault(Intel486ExceptionVector.GeneralProtection, "Data segment register requires data or readable code.", True, SelectorErrorCode(selectorInBed))
            End If
            If descriptorInBed.Executable AndAlso descriptorInBed.ConformingCode Then
                If descriptorInBed.DescriptorPrivilegeLevel > _cplInBed Then
                    ThrowFault(Intel486ExceptionVector.GeneralProtection, "Conforming readable code segment privilege check failed.", True, SelectorErrorCode(selectorInBed))
                End If
            ElseIf Math.Max(CInt(_cplInBed), CInt(rplInBed)) > descriptorInBed.DescriptorPrivilegeLevel Then
                ThrowFault(Intel486ExceptionVector.GeneralProtection, "Data segment privilege check failed.", True, SelectorErrorCode(selectorInBed))
            End If
            If Not descriptorInBed.Present Then
                ThrowFault(Intel486ExceptionVector.SegmentNotPresent, "Data segment is not present.", True, SelectorErrorCode(selectorInBed))
            End If
        End If

        MarkDescriptorAccessed(descriptorInBed, fabricInBed)
        _mmuInBed.InstallProtectedModeSegment(segmentInBed, BuildSegmentCache(selectorInBed, descriptorInBed))
        If segmentInBed = Intel486SegmentRegister.SS Then _maskableInterruptShadowInBed = 2
    End Sub

    Private Sub LoadStackSegmentForPrivilege(selectorInBed As UInt16,
                                             targetCplInBed As Byte,
                                             fabricInBed As IProcessorLocalBusFabric)
        If SelectorIsNull(selectorInBed) Then
            ThrowFault(Intel486ExceptionVector.InvalidTss, "Privilege stack contains a null SS.", True, 0US)
        End If
        Dim descriptorInBed As Intel486Descriptor = ReadDescriptor(selectorInBed, fabricInBed,
                                                                   Intel486ExceptionVector.InvalidTss,
                                                                   "Privilege-stack SS selector is invalid.")
        If Not descriptorInBed.IsCodeOrData OrElse descriptorInBed.Executable OrElse Not descriptorInBed.WritableData OrElse
           SelectorRpl(selectorInBed) <> targetCplInBed OrElse descriptorInBed.DescriptorPrivilegeLevel <> targetCplInBed Then
            ThrowFault(Intel486ExceptionVector.InvalidTss, "Privilege-stack SS descriptor is invalid.", True, SelectorErrorCode(selectorInBed))
        End If
        If Not descriptorInBed.Present Then
            ThrowFault(Intel486ExceptionVector.StackFault, "Privilege-stack SS is not present.", True, SelectorErrorCode(selectorInBed))
        End If
        MarkDescriptorAccessed(descriptorInBed, fabricInBed)
        _mmuInBed.InstallProtectedModeSegment(Intel486SegmentRegister.SS, BuildSegmentCache(selectorInBed, descriptorInBed))
    End Sub

    Private Sub LoadCodeSegment(selectorInBed As UInt16,
                                descriptorInBed As Intel486Descriptor,
                                targetCplInBed As Byte,
                                offsetInBed As UInteger,
                                fabricInBed As IProcessorLocalBusFabric)
        If Not descriptorInBed.Executable Then
            ThrowFault(Intel486ExceptionVector.GeneralProtection, "Control transfer target is not executable.", True, SelectorErrorCode(selectorInBed))
        End If
        If Not descriptorInBed.Present Then
            ThrowFault(Intel486ExceptionVector.SegmentNotPresent, "Control transfer code segment is not present.", True, SelectorErrorCode(selectorInBed))
        End If
        If CULng(offsetInBed) > descriptorInBed.Limit Then
            ThrowFault(Intel486ExceptionVector.GeneralProtection, "Control transfer offset exceeds code segment limit.", True, 0US)
        End If
        MarkDescriptorAccessed(descriptorInBed, fabricInBed)
        Dim visibleSelectorInBed As UInt16 = CUShort((selectorInBed And &HFFFCUS) Or targetCplInBed)
        _mmuInBed.InstallProtectedModeSegment(Intel486SegmentRegister.CS, BuildSegmentCache(visibleSelectorInBed, descriptorInBed))
        _cplInBed = targetCplInBed
        _cpuInBed.MutableEip = offsetInBed
    End Sub

    Private Function ReadPrivilegeStack(targetCplInBed As Byte,
                                        fabricInBed As IProcessorLocalBusFabric,
                                        ByRef ssOutInBed As UInt16,
                                        ByRef espOutInBed As UInteger) As Boolean
        If targetCplInBed > 2 OrElse Not _taskRegisterInBed.Valid OrElse _taskRegisterInBed.Limit < &H67UI Then Return False
        Dim espOffsetInBed As UInteger = 4UI + CUInt(targetCplInBed) * 8UI
        Dim ssOffsetInBed As UInteger = espOffsetInBed + 4UI
        If ssOffsetInBed + 1UI > _taskRegisterInBed.Limit Then Return False
        espOutInBed = ReadSystemLinearDword(_taskRegisterInBed.BaseAddress + espOffsetInBed, fabricInBed)
        ssOutInBed = ReadSystemLinearWord(_taskRegisterInBed.BaseAddress + ssOffsetInBed, fabricInBed)
        Return True
    End Function

    Private Sub ExecuteProtectedFarTransfer(selectorInBed As UInt16,
                                            offsetInBed As UInteger,
                                            isCallInBed As Boolean,
                                            widthInBed As Integer,
                                            fabricInBed As IProcessorLocalBusFabric)
        If SelectorIsNull(selectorInBed) Then
            ThrowFault(Intel486ExceptionVector.GeneralProtection, "Far transfer uses null selector.", True, 0US)
        End If
        Dim descriptorInBed As Intel486Descriptor = ReadDescriptor(selectorInBed, fabricInBed,
                                                                   Intel486ExceptionVector.GeneralProtection,
                                                                   "Far transfer selector invalid.")
        Dim rplInBed As Byte = SelectorRpl(selectorInBed)

        If descriptorInBed.IsCodeOrData Then
            If Not descriptorInBed.Executable Then
                ThrowFault(Intel486ExceptionVector.GeneralProtection, "Far transfer target must be code.", True, SelectorErrorCode(selectorInBed))
            End If
            If descriptorInBed.ConformingCode Then
                If descriptorInBed.DescriptorPrivilegeLevel > _cplInBed Then
                    ThrowFault(Intel486ExceptionVector.GeneralProtection, "Conforming code DPL is less privileged than CPL.", True, SelectorErrorCode(selectorInBed))
                End If
            Else
                If descriptorInBed.DescriptorPrivilegeLevel <> _cplInBed OrElse rplInBed > _cplInBed Then
                    ThrowFault(Intel486ExceptionVector.GeneralProtection, "Direct far transfer cannot change privilege.", True, SelectorErrorCode(selectorInBed))
                End If
            End If
            If isCallInBed Then
                PushValue(_mmuInBed.GetSegment(Intel486SegmentRegister.CS).Selector, widthInBed, fabricInBed)
                PushValue(_cpuInBed.MutableEip, widthInBed, fabricInBed)
            End If
            LoadCodeSegment(selectorInBed, descriptorInBed, _cplInBed, offsetInBed And WidthMask(widthInBed), fabricInBed)
            Return
        End If

        If descriptorInBed.IsTaskGate Then
            If Not isCallInBed Then
                SwitchTask(descriptorInBed.GateSelector, False, False, fabricInBed)
            Else
                SwitchTask(descriptorInBed.GateSelector, True, False, fabricInBed)
            End If
            Return
        End If

        If descriptorInBed.IsAvailable386Tss OrElse descriptorInBed.IsBusy386Tss Then
            SwitchTask(selectorInBed, isCallInBed, False, fabricInBed)
            Return
        End If

        If Not (descriptorInBed.Is286CallGate OrElse descriptorInBed.Is386CallGate) Then
            ThrowFault(Intel486ExceptionVector.GeneralProtection, "Far transfer selector is neither code nor a valid gate/TSS.", True, SelectorErrorCode(selectorInBed))
        End If
        If Math.Max(CInt(_cplInBed), CInt(rplInBed)) > descriptorInBed.DescriptorPrivilegeLevel Then
            ThrowFault(Intel486ExceptionVector.GeneralProtection, "Call-gate DPL check failed.", True, SelectorErrorCode(selectorInBed))
        End If
        If Not descriptorInBed.Present Then
            ThrowFault(Intel486ExceptionVector.SegmentNotPresent, "Call gate is not present.", True, SelectorErrorCode(selectorInBed))
        End If

        Dim targetSelectorInBed As UInt16 = descriptorInBed.GateSelector
        Dim targetInBed As Intel486Descriptor = ReadDescriptor(targetSelectorInBed, fabricInBed,
                                                               Intel486ExceptionVector.GeneralProtection,
                                                               "Call-gate target selector invalid.")
        If Not targetInBed.Executable Then
            ThrowFault(Intel486ExceptionVector.GeneralProtection, "Call-gate target is not code.", True, SelectorErrorCode(targetSelectorInBed))
        End If
        If Not targetInBed.Present Then
            ThrowFault(Intel486ExceptionVector.SegmentNotPresent, "Call-gate target code not present.", True, SelectorErrorCode(targetSelectorInBed))
        End If
        Dim gateWidthInBed As Integer = If(descriptorInBed.Is386CallGate, 32, 16)
        Dim gateOffsetInBed As UInteger = descriptorInBed.GateOffset And WidthMask(gateWidthInBed)

        Dim targetCplInBed As Byte = If(targetInBed.ConformingCode, _cplInBed, targetInBed.DescriptorPrivilegeLevel)
        If targetCplInBed > _cplInBed Then
            ThrowFault(Intel486ExceptionVector.GeneralProtection, "Call gate cannot transfer to a less privileged code segment.", True, SelectorErrorCode(targetSelectorInBed))
        End If
        If Not isCallInBed AndAlso targetCplInBed <> _cplInBed Then
            ThrowFault(Intel486ExceptionVector.GeneralProtection, "Far JMP through a call gate cannot change privilege.", True, SelectorErrorCode(targetSelectorInBed))
        End If

        If targetCplInBed = _cplInBed Then
            If isCallInBed Then
                PushValue(_mmuInBed.GetSegment(Intel486SegmentRegister.CS).Selector, gateWidthInBed, fabricInBed)
                PushValue(_cpuInBed.MutableEip, gateWidthInBed, fabricInBed)
            End If
            LoadCodeSegment(targetSelectorInBed, targetInBed, _cplInBed, gateOffsetInBed, fabricInBed)
            Return
        End If

        ' Inner privilege calls switch stacks through the current 386/486 TSS.
        Dim newSsInBed As UInt16
        Dim newEspInBed As UInteger
        If Not ReadPrivilegeStack(targetCplInBed, fabricInBed, newSsInBed, newEspInBed) Then
            ThrowFault(Intel486ExceptionVector.InvalidTss, "Current TSS does not contain the requested privilege stack.", True, SelectorErrorCode(_taskRegisterInBed.Selector))
        End If

        Dim oldSsInBed As UInt16 = _mmuInBed.GetSegment(Intel486SegmentRegister.SS).Selector
        Dim oldEspInBed As UInteger = GetStackPointer()
        Dim oldCsInBed As UInt16 = _mmuInBed.GetSegment(Intel486SegmentRegister.CS).Selector
        Dim oldEipInBed As UInteger = _cpuInBed.MutableEip
        Dim parametersInBed As New List(Of UInteger)()
        Dim parameterBytesInBed As UInteger = CUInt(gateWidthInBed \ 8)
        For parameterInBed As Integer = 0 To descriptorInBed.GateParameterCount - 1
            Dim sourceOffsetInBed As UInteger = CUInt((CULng(oldEspInBed) + CULng(CUInt(parameterInBed) * parameterBytesInBed)) And If(StackAddress32(), &HFFFFFFFFUL, &HFFFFUL))
            parametersInBed.Add(ReadMemoryByWidth(Intel486SegmentRegister.SS, sourceOffsetInBed, gateWidthInBed, fabricInBed))
        Next

        LoadStackSegmentForPrivilege(newSsInBed, targetCplInBed, fabricInBed)
        _cplInBed = targetCplInBed
        SetStackPointer(newEspInBed)
        PushValue(oldSsInBed, gateWidthInBed, fabricInBed)
        PushValue(oldEspInBed, gateWidthInBed, fabricInBed)
        For parameterInBed As Integer = parametersInBed.Count - 1 To 0 Step -1
            PushValue(parametersInBed(parameterInBed), gateWidthInBed, fabricInBed)
        Next
        PushValue(oldCsInBed, gateWidthInBed, fabricInBed)
        PushValue(oldEipInBed, gateWidthInBed, fabricInBed)
        LoadCodeSegment(targetSelectorInBed, targetInBed, targetCplInBed, gateOffsetInBed, fabricInBed)
    End Sub

    Private Sub ExecuteProtectedFarReturn(widthInBed As Integer,
                                          popBytesInBed As UInt16,
                                          fabricInBed As IProcessorLocalBusFabric)
        Dim oldSpInBed As UInteger = GetStackPointer()
        Dim bytesInBed As UInteger = CUInt(widthInBed \ 8)
        Dim cursorInBed As UInteger = oldSpInBed
        Dim targetOffsetInBed As UInteger = ReadStackValueAt(cursorInBed, widthInBed, fabricInBed)
        cursorInBed = AdvanceStackCursor(cursorInBed, bytesInBed)
        Dim targetSelectorInBed As UInt16 = CUShort(ReadStackValueAt(cursorInBed, widthInBed, fabricInBed) And &HFFFFUI)
        cursorInBed = AdvanceStackCursor(cursorInBed, bytesInBed)
        If SelectorIsNull(targetSelectorInBed) Then ThrowFault(Intel486ExceptionVector.GeneralProtection, "RETF target selector is null.", True, 0US)

        Dim targetInBed As Intel486Descriptor = ReadDescriptor(targetSelectorInBed, fabricInBed,
                                                               Intel486ExceptionVector.GeneralProtection,
                                                               "RETF target selector invalid.")
        If Not targetInBed.Executable Then ThrowFault(Intel486ExceptionVector.GeneralProtection, "RETF target is not code.", True, SelectorErrorCode(targetSelectorInBed))
        Dim targetRplInBed As Byte = SelectorRpl(targetSelectorInBed)
        If targetRplInBed < _cplInBed Then ThrowFault(Intel486ExceptionVector.GeneralProtection, "RETF cannot return to a more privileged level.", True, SelectorErrorCode(targetSelectorInBed))
        If targetInBed.ConformingCode Then
            If targetInBed.DescriptorPrivilegeLevel > targetRplInBed Then ThrowFault(Intel486ExceptionVector.GeneralProtection, "RETF conforming-code privilege check failed.", True, SelectorErrorCode(targetSelectorInBed))
        ElseIf targetInBed.DescriptorPrivilegeLevel <> targetRplInBed Then
            ThrowFault(Intel486ExceptionVector.GeneralProtection, "RETF nonconforming-code DPL must equal target RPL.", True, SelectorErrorCode(targetSelectorInBed))
        End If
        If Not targetInBed.Present Then ThrowFault(Intel486ExceptionVector.SegmentNotPresent, "RETF target code not present.", True, SelectorErrorCode(targetSelectorInBed))
        If targetOffsetInBed > targetInBed.Limit Then ThrowFault(Intel486ExceptionVector.GeneralProtection, "RETF target offset exceeds limit.", True, 0US)

        If targetRplInBed = _cplInBed Then
            SetStackPointer(AdvanceStackCursor(cursorInBed, popBytesInBed))
            LoadCodeSegment(targetSelectorInBed, targetInBed, _cplInBed, targetOffsetInBed And WidthMask(widthInBed), fabricInBed)
            Return
        End If

        cursorInBed = AdvanceStackCursor(cursorInBed, popBytesInBed)
        Dim newEspInBed As UInteger = ReadStackValueAt(cursorInBed, widthInBed, fabricInBed)
        cursorInBed = AdvanceStackCursor(cursorInBed, bytesInBed)
        Dim newSsInBed As UInt16 = CUShort(ReadStackValueAt(cursorInBed, widthInBed, fabricInBed) And &HFFFFUI)
        ValidateOuterStackSegment(newSsInBed, targetRplInBed, fabricInBed)

        LoadCodeSegment(targetSelectorInBed, targetInBed, targetRplInBed, targetOffsetInBed And WidthMask(widthInBed), fabricInBed)
        LoadStackSegmentForPrivilegeReturn(newSsInBed, targetRplInBed, fabricInBed)
        SetStackPointer(CUInt((CULng(newEspInBed) + CULng(popBytesInBed)) And If(StackAddress32(), &HFFFFFFFFUL, &HFFFFUL)))
        ClearDataSegmentsInvalidAtCpl(fabricInBed)
    End Sub

    Private Function ReadStackValueAt(stackPointerInBed As UInteger,
                                      widthInBed As Integer,
                                      fabricInBed As IProcessorLocalBusFabric) As UInteger
        If widthInBed = 16 Then Return ReadMemoryWord(Intel486SegmentRegister.SS, stackPointerInBed, fabricInBed)
        Return ReadMemoryDword(Intel486SegmentRegister.SS, stackPointerInBed, fabricInBed)
    End Function

    Private Function AdvanceStackCursor(cursorInBed As UInteger, amountInBed As UInteger) As UInteger
        Dim maskInBed As ULong = If(StackAddress32(), &HFFFFFFFFUL, &HFFFFUL)
        Return CUInt((CULng(cursorInBed) + CULng(amountInBed)) And maskInBed)
    End Function

    Private Sub ValidateOuterStackSegment(selectorInBed As UInt16,
                                          targetCplInBed As Byte,
                                          fabricInBed As IProcessorLocalBusFabric)
        If SelectorIsNull(selectorInBed) Then ThrowFault(Intel486ExceptionVector.GeneralProtection, "Outer-level SS is null.", True, 0US)
        Dim descriptorInBed As Intel486Descriptor = ReadDescriptor(selectorInBed, fabricInBed,
                                                                   Intel486ExceptionVector.GeneralProtection,
                                                                   "Outer-level SS selector invalid.")
        If Not descriptorInBed.IsCodeOrData OrElse descriptorInBed.Executable OrElse Not descriptorInBed.WritableData OrElse
           SelectorRpl(selectorInBed) <> targetCplInBed OrElse descriptorInBed.DescriptorPrivilegeLevel <> targetCplInBed Then
            ThrowFault(Intel486ExceptionVector.GeneralProtection, "Outer-level SS descriptor privilege/type invalid.", True, SelectorErrorCode(selectorInBed))
        End If
        If Not descriptorInBed.Present Then ThrowFault(Intel486ExceptionVector.StackFault, "Outer-level stack segment not present.", True, SelectorErrorCode(selectorInBed))
    End Sub

    Private Sub LoadStackSegmentForPrivilegeReturn(selectorInBed As UInt16,
                                                   targetCplInBed As Byte,
                                                   fabricInBed As IProcessorLocalBusFabric)
        Dim descriptorInBed As Intel486Descriptor = ReadDescriptor(selectorInBed, fabricInBed,
                                                                   Intel486ExceptionVector.GeneralProtection,
                                                                   "Return SS selector invalid.")
        MarkDescriptorAccessed(descriptorInBed, fabricInBed)
        _mmuInBed.InstallProtectedModeSegment(Intel486SegmentRegister.SS, BuildSegmentCache(selectorInBed, descriptorInBed))
    End Sub

    Private Sub ClearDataSegmentsInvalidAtCpl(fabricInBed As IProcessorLocalBusFabric)
        For Each segmentInBed As Intel486SegmentRegister In New Intel486SegmentRegister() {
            Intel486SegmentRegister.ES, Intel486SegmentRegister.DS, Intel486SegmentRegister.FS, Intel486SegmentRegister.GS}
            Dim cacheInBed As Intel486SegmentCacheEntry = _mmuInBed.GetSegment(segmentInBed)
            If cacheInBed.Selector = 0US Then Continue For
            Dim descriptorInBed As Intel486Descriptor
            If Not TryReadDescriptor(cacheInBed.Selector, fabricInBed, descriptorInBed) Then
                InstallNullDataSegment(segmentInBed)
                Continue For
            End If
            If descriptorInBed.Executable AndAlso descriptorInBed.ConformingCode Then Continue For
            If descriptorInBed.DescriptorPrivilegeLevel < _cplInBed Then InstallNullDataSegment(segmentInBed)
        Next
    End Sub

    Private Sub DeliverArchitecturalException(faultInBed As Intel486CpuException,
                                              fabricInBed As IProcessorLocalBusFabric)
        If _shutdownInBed Then Return
        If _exceptionDepthInBed >= 4 Then
            EnterShutdown()
            Return
        End If

        Dim firstVectorInBed As Intel486ExceptionVector = faultInBed.Vector
        Try
            _exceptionDepthInBed += 1
            DeliverInterrupt(CByte(firstVectorInBed), False, faultInBed.HasErrorCode,
                             faultInBed.ErrorCode, fabricInBed, faultInBed.IsExternal)
        Catch pageInBed As Intel486PageFaultException
            Dim deliveryFaultInBed As New Intel486CpuException(Intel486ExceptionVector.PageFault,
                                                               _cpuInBed.MutableEip,
                                                               pageInBed.Message,
                                                               True,
                                                               pageInBed.ErrorCode)
            HandleFaultDuringExceptionDelivery(firstVectorInBed, deliveryFaultInBed, fabricInBed)
        Catch deliveryFaultInBed As Intel486CpuException
            HandleFaultDuringExceptionDelivery(firstVectorInBed, deliveryFaultInBed, fabricInBed)
        Finally
            _exceptionDepthInBed = Math.Max(0, _exceptionDepthInBed - 1)
        End Try
    End Sub

    Private Sub HandleFaultDuringExceptionDelivery(firstVectorInBed As Intel486ExceptionVector,
                                                   deliveryFaultInBed As Intel486CpuException,
                                                   fabricInBed As IProcessorLocalBusFabric)
        If firstVectorInBed = Intel486ExceptionVector.DoubleFault Then
            EnterShutdown()
            Return
        End If

        If ShouldDoubleFault(firstVectorInBed, deliveryFaultInBed.Vector) Then
            DeliverArchitecturalException(New Intel486CpuException(Intel486ExceptionVector.DoubleFault,
                                                                   _cpuInBed.MutableEip,
                                                                   "Double fault during exception delivery.",
                                                                   True,
                                                                   0US),
                                          fabricInBed)
        Else
            DeliverArchitecturalException(deliveryFaultInBed, fabricInBed)
        End If
    End Sub

    Private Sub EnterShutdown()
        _shutdownInBed = True
        _haltedInBed = False
        _shutdownRequiresResetInBed = _nmiInServiceInBed
    End Sub

    Private Shared Function IsContributoryException(vectorInBed As Intel486ExceptionVector) As Boolean
        Return vectorInBed = Intel486ExceptionVector.DivideError OrElse
               vectorInBed = Intel486ExceptionVector.InvalidTss OrElse
               vectorInBed = Intel486ExceptionVector.SegmentNotPresent OrElse
               vectorInBed = Intel486ExceptionVector.StackFault OrElse
               vectorInBed = Intel486ExceptionVector.GeneralProtection
    End Function

    Private Shared Function ShouldDoubleFault(firstInBed As Intel486ExceptionVector,
                                              secondInBed As Intel486ExceptionVector) As Boolean
        ' Intel486 Programmer's Reference Manual Table 9-4:
        '   contributory -> contributory = #DF
        '   page fault   -> contributory = #DF
        '   page fault   -> page fault   = #DF
        '   contributory -> page fault   = serially handled
        If IsContributoryException(firstInBed) AndAlso IsContributoryException(secondInBed) Then Return True
        If firstInBed = Intel486ExceptionVector.PageFault AndAlso
           (IsContributoryException(secondInBed) OrElse secondInBed = Intel486ExceptionVector.PageFault) Then Return True
        Return False
    End Function

    Private Sub DeliverInterrupt(vectorInBed As Byte,
                                 softwareInBed As Boolean,
                                 hasErrorCodeInBed As Boolean,
                                 errorCodeInBed As UInt16,
                                 fabricInBed As IProcessorLocalBusFabric,
                                 externalInBed As Boolean)
        If Not _mmuInBed.ProtectedModeEnabled Then
            DeliverRealModeInterrupt(vectorInBed, hasErrorCodeInBed, errorCodeInBed, fabricInBed)
            Return
        End If

        Dim gateInBed As Intel486Descriptor = ReadIdtGate(vectorInBed, fabricInBed, externalInBed)
        If Not gateInBed.IsGate OrElse gateInBed.Is286CallGate OrElse gateInBed.Is386CallGate Then
            ThrowFault(Intel486ExceptionVector.GeneralProtection, "IDT entry is not an interrupt/trap/task gate.", True, IdtErrorCode(vectorInBed, externalInBed))
        End If
        Dim callerCplInBed As Byte = If(Virtual8086Mode, CByte(3), _cplInBed)
        If softwareInBed AndAlso callerCplInBed > gateInBed.DescriptorPrivilegeLevel Then
            ThrowFault(Intel486ExceptionVector.GeneralProtection, "Software INT gate DPL check failed.", True, IdtErrorCode(vectorInBed, False))
        End If
        If Not gateInBed.Present Then
            ThrowFault(Intel486ExceptionVector.SegmentNotPresent, "IDT gate is not present.", True, IdtErrorCode(vectorInBed, externalInBed))
        End If
        If gateInBed.IsTaskGate Then
            SwitchTask(gateInBed.GateSelector, True, False, fabricInBed)
            Return
        End If

        Dim gate32InBed As Boolean = gateInBed.Is386InterruptGate OrElse gateInBed.Is386TrapGate
        Dim frameWidthInBed As Integer = If(gate32InBed, 32, 16)
        Dim targetSelectorInBed As UInt16 = gateInBed.GateSelector
        If SelectorIsNull(targetSelectorInBed) Then
            ThrowFault(Intel486ExceptionVector.GeneralProtection, "IDT gate contains null CS selector.", True, 0US)
        End If
        Dim targetInBed As Intel486Descriptor = ReadDescriptor(targetSelectorInBed, fabricInBed,
                                                               Intel486ExceptionVector.GeneralProtection,
                                                               "IDT target CS selector invalid.", externalInBed)
        If Not targetInBed.Executable Then
            ThrowFault(Intel486ExceptionVector.GeneralProtection, "IDT target is not a code segment.", True, SelectorErrorCode(targetSelectorInBed, externalInBed))
        End If
        If Not targetInBed.Present Then
            ThrowFault(Intel486ExceptionVector.SegmentNotPresent, "IDT target code is not present.", True, SelectorErrorCode(targetSelectorInBed, externalInBed))
        End If

        Dim oldCplInBed As Byte = If(Virtual8086Mode, CByte(3), _cplInBed)
        Dim targetCplInBed As Byte = If(targetInBed.ConformingCode, oldCplInBed, targetInBed.DescriptorPrivilegeLevel)
        If targetCplInBed > oldCplInBed Then
            ThrowFault(Intel486ExceptionVector.GeneralProtection, "Interrupt gate cannot transfer outward in privilege.", True, SelectorErrorCode(targetSelectorInBed, externalInBed))
        End If

        Dim oldFlagsInBed As UInteger = _cpuInBed.MutableEflags
        Dim oldCsInBed As UInt16 = _mmuInBed.GetSegment(Intel486SegmentRegister.CS).Selector
        Dim oldEipInBed As UInteger = _cpuInBed.MutableEip
        Dim wasV86InBed As Boolean = Virtual8086Mode

        If targetCplInBed < oldCplInBed Then
            Dim newSsInBed As UInt16
            Dim newEspInBed As UInteger
            If Not ReadPrivilegeStack(targetCplInBed, fabricInBed, newSsInBed, newEspInBed) Then
                ThrowFault(Intel486ExceptionVector.InvalidTss, "No valid privilege stack in current TSS.", True, SelectorErrorCode(_taskRegisterInBed.Selector, externalInBed))
            End If
            Dim oldSsInBed As UInt16 = _mmuInBed.GetSegment(Intel486SegmentRegister.SS).Selector
            Dim oldEspInBed As UInteger = GetStackPointer()
            Dim oldEsInBed As UInt16 = _mmuInBed.GetSegment(Intel486SegmentRegister.ES).Selector
            Dim oldDsInBed As UInt16 = _mmuInBed.GetSegment(Intel486SegmentRegister.DS).Selector
            Dim oldFsInBed As UInt16 = _mmuInBed.GetSegment(Intel486SegmentRegister.FS).Selector
            Dim oldGsInBed As UInt16 = _mmuInBed.GetSegment(Intel486SegmentRegister.GS).Selector

            LoadStackSegmentForPrivilege(newSsInBed, targetCplInBed, fabricInBed)
            _cplInBed = targetCplInBed
            SetStackPointer(newEspInBed)
            If wasV86InBed Then
                PushValue(oldGsInBed, frameWidthInBed, fabricInBed)
                PushValue(oldFsInBed, frameWidthInBed, fabricInBed)
                PushValue(oldDsInBed, frameWidthInBed, fabricInBed)
                PushValue(oldEsInBed, frameWidthInBed, fabricInBed)
            End If
            PushValue(oldSsInBed, frameWidthInBed, fabricInBed)
            PushValue(oldEspInBed, frameWidthInBed, fabricInBed)
        End If

        PushValue(oldFlagsInBed, frameWidthInBed, fabricInBed)
        PushValue(oldCsInBed, frameWidthInBed, fabricInBed)
        PushValue(oldEipInBed, frameWidthInBed, fabricInBed)
        If hasErrorCodeInBed Then PushValue(errorCodeInBed, frameWidthInBed, fabricInBed)

        Dim newFlagsInBed As UInteger = oldFlagsInBed And Not (FlagTf Or FlagNt Or FlagRf Or FlagVm)
        If gateInBed.Is286InterruptGate OrElse gateInBed.Is386InterruptGate Then newFlagsInBed = newFlagsInBed And Not FlagIf
        _cpuInBed.MutableEflags = newFlagsInBed
        If wasV86InBed Then
            InstallNullDataSegment(Intel486SegmentRegister.ES)
            InstallNullDataSegment(Intel486SegmentRegister.DS)
            InstallNullDataSegment(Intel486SegmentRegister.FS)
            InstallNullDataSegment(Intel486SegmentRegister.GS)
        End If
        LoadCodeSegment(targetSelectorInBed, targetInBed, targetCplInBed,
                        gateInBed.GateOffset And WidthMask(frameWidthInBed), fabricInBed)
    End Sub

    Private Sub DeliverRealModeInterrupt(vectorInBed As Byte,
                                         hasErrorCodeInBed As Boolean,
                                         errorCodeInBed As UInt16,
                                         fabricInBed As IProcessorLocalBusFabric)
        Dim entryOffsetInBed As UInteger = CUInt(vectorInBed) * 4UI
        If entryOffsetInBed + 3UI > _idtrInBed.Limit Then
            ThrowFault(Intel486ExceptionVector.GeneralProtection, "Real-mode interrupt vector lies beyond IDTR limit.", True, 0US)
        End If
        Dim addressInBed As UInteger = CUInt((CULng(_idtrInBed.BaseAddress) + CULng(entryOffsetInBed)) And &HFFFFFFFFUL)
        Dim newIpInBed As UInt16 = ReadSystemLinearWord(addressInBed, fabricInBed)
        Dim newCsInBed As UInt16 = ReadSystemLinearWord(addressInBed + 2UI, fabricInBed)
        PushValue(_cpuInBed.MutableEflags And &HFFFFUI, 16, fabricInBed)
        PushValue(_mmuInBed.GetSegment(Intel486SegmentRegister.CS).Selector, 16, fabricInBed)
        PushValue(_cpuInBed.MutableEip And &HFFFFUI, 16, fabricInBed)
        If hasErrorCodeInBed Then PushValue(errorCodeInBed, 16, fabricInBed)
        _cpuInBed.MutableEflags = _cpuInBed.MutableEflags And Not (FlagIf Or FlagTf)
        _mmuInBed.LoadRealModeSegment(Intel486SegmentRegister.CS, newCsInBed)
        _cpuInBed.MutableEip = newIpInBed
    End Sub

    Private Sub ExecuteIret(widthInBed As Integer, fabricInBed As IProcessorLocalBusFabric)
        If Not _mmuInBed.ProtectedModeEnabled Then
            Dim realModeIpInBed As UInteger = PopValue(widthInBed, fabricInBed) And WidthMask(widthInBed)
            Dim realModeCsInBed As UInt16 = CUShort(PopValue(widthInBed, fabricInBed) And &HFFFFUI)
            Dim realModeFlagsInBed As UInteger = PopValue(widthInBed, fabricInBed)
            _mmuInBed.LoadRealModeSegment(Intel486SegmentRegister.CS, realModeCsInBed)
            _cpuInBed.MutableEip = realModeIpInBed
            ApplyIretFlags(realModeFlagsInBed, widthInBed, 0)
            Return
        End If

        If Virtual8086Mode Then
            If CurrentIopl() < 3 Then ThrowFault(Intel486ExceptionVector.GeneralProtection, "IRET is IOPL-sensitive in virtual-8086 mode.", True, 0US)
            Dim v86IpInBed As UInteger = PopValue(widthInBed, fabricInBed) And WidthMask(widthInBed)
            Dim v86CsInBed As UInt16 = CUShort(PopValue(widthInBed, fabricInBed) And &HFFFFUI)
            Dim v86FlagsInBed As UInteger = PopValue(widthInBed, fabricInBed)
            _mmuInBed.LoadRealModeSegment(Intel486SegmentRegister.CS, v86CsInBed)
            _cpuInBed.MutableEip = v86IpInBed
            ApplyIretFlags(v86FlagsInBed, widthInBed, 3)
            Return
        End If

        If FlagIsSet(FlagNt) Then
            If Not _taskRegisterInBed.Valid OrElse _taskRegisterInBed.Limit < 1UI Then
                ThrowFault(Intel486ExceptionVector.InvalidTss, "Nested-task IRET has no valid current TSS.", True, SelectorErrorCode(_taskRegisterInBed.Selector))
            End If
            Dim backlinkInBed As UInt16 = ReadSystemLinearWord(_taskRegisterInBed.BaseAddress, fabricInBed)
            SwitchTask(backlinkInBed, False, True, fabricInBed)
            Return
        End If

        Dim originalSpInBed As UInteger = GetStackPointer()
        Dim bytesInBed As UInteger = CUInt(widthInBed \ 8)
        Dim cursorInBed As UInteger = originalSpInBed
        Dim newEipInBed As UInteger = ReadStackValueAt(cursorInBed, widthInBed, fabricInBed)
        cursorInBed = AdvanceStackCursor(cursorInBed, bytesInBed)
        Dim newCsInBed As UInt16 = CUShort(ReadStackValueAt(cursorInBed, widthInBed, fabricInBed) And &HFFFFUI)
        cursorInBed = AdvanceStackCursor(cursorInBed, bytesInBed)
        Dim newFlagsInBed As UInteger = ReadStackValueAt(cursorInBed, widthInBed, fabricInBed)
        cursorInBed = AdvanceStackCursor(cursorInBed, bytesInBed)

        If widthInBed = 32 AndAlso (newFlagsInBed And FlagVm) <> 0UI Then
            If _cplInBed <> 0 Then ThrowFault(Intel486ExceptionVector.GeneralProtection, "Only CPL0 IRET can enter virtual-8086 mode.", True, 0US)
            Dim newEspInBed As UInteger = ReadStackValueAt(cursorInBed, 32, fabricInBed) : cursorInBed = AdvanceStackCursor(cursorInBed, 4UI)
            Dim newSsInBed As UInt16 = CUShort(ReadStackValueAt(cursorInBed, 32, fabricInBed) And &HFFFFUI) : cursorInBed = AdvanceStackCursor(cursorInBed, 4UI)
            Dim newEsInBed As UInt16 = CUShort(ReadStackValueAt(cursorInBed, 32, fabricInBed) And &HFFFFUI) : cursorInBed = AdvanceStackCursor(cursorInBed, 4UI)
            Dim newDsInBed As UInt16 = CUShort(ReadStackValueAt(cursorInBed, 32, fabricInBed) And &HFFFFUI) : cursorInBed = AdvanceStackCursor(cursorInBed, 4UI)
            Dim newFsInBed As UInt16 = CUShort(ReadStackValueAt(cursorInBed, 32, fabricInBed) And &HFFFFUI) : cursorInBed = AdvanceStackCursor(cursorInBed, 4UI)
            Dim newGsInBed As UInt16 = CUShort(ReadStackValueAt(cursorInBed, 32, fabricInBed) And &HFFFFUI)

            _cplInBed = 3
            _cpuInBed.MutableEflags = newFlagsInBed Or &H2UI
            _mmuInBed.LoadRealModeSegment(Intel486SegmentRegister.CS, newCsInBed)
            _mmuInBed.LoadRealModeSegment(Intel486SegmentRegister.SS, newSsInBed)
            _mmuInBed.LoadRealModeSegment(Intel486SegmentRegister.ES, newEsInBed)
            _mmuInBed.LoadRealModeSegment(Intel486SegmentRegister.DS, newDsInBed)
            _mmuInBed.LoadRealModeSegment(Intel486SegmentRegister.FS, newFsInBed)
            _mmuInBed.LoadRealModeSegment(Intel486SegmentRegister.GS, newGsInBed)
            _cpuInBed.MutableEip = newEipInBed And &HFFFFUI
            _cpuInBed.WriteGeneralRegister32(4, newEspInBed)
            Return
        End If

        If SelectorIsNull(newCsInBed) Then ThrowFault(Intel486ExceptionVector.GeneralProtection, "IRET target CS is null.", True, 0US)
        Dim targetInBed As Intel486Descriptor = ReadDescriptor(newCsInBed, fabricInBed,
                                                               Intel486ExceptionVector.GeneralProtection,
                                                               "IRET target CS invalid.")
        Dim targetCplInBed As Byte = SelectorRpl(newCsInBed)
        If targetCplInBed < _cplInBed Then ThrowFault(Intel486ExceptionVector.GeneralProtection, "IRET cannot return inward in privilege.", True, SelectorErrorCode(newCsInBed))
        If Not targetInBed.Executable Then ThrowFault(Intel486ExceptionVector.GeneralProtection, "IRET target is not code.", True, SelectorErrorCode(newCsInBed))
        If targetInBed.ConformingCode Then
            If targetInBed.DescriptorPrivilegeLevel > targetCplInBed Then ThrowFault(Intel486ExceptionVector.GeneralProtection, "IRET conforming-code privilege check failed.", True, SelectorErrorCode(newCsInBed))
        ElseIf targetInBed.DescriptorPrivilegeLevel <> targetCplInBed Then
            ThrowFault(Intel486ExceptionVector.GeneralProtection, "IRET nonconforming-code DPL must equal RPL.", True, SelectorErrorCode(newCsInBed))
        End If
        If Not targetInBed.Present Then ThrowFault(Intel486ExceptionVector.SegmentNotPresent, "IRET target code not present.", True, SelectorErrorCode(newCsInBed))
        If newEipInBed > targetInBed.Limit Then ThrowFault(Intel486ExceptionVector.GeneralProtection, "IRET target EIP exceeds code limit.", True, 0US)

        If targetCplInBed = _cplInBed Then
            SetStackPointer(cursorInBed)
            LoadCodeSegment(newCsInBed, targetInBed, _cplInBed, newEipInBed And WidthMask(widthInBed), fabricInBed)
            ApplyIretFlags(newFlagsInBed, widthInBed, _cplInBed)
            Return
        End If

        Dim newEspOuterInBed As UInteger = ReadStackValueAt(cursorInBed, widthInBed, fabricInBed) : cursorInBed = AdvanceStackCursor(cursorInBed, bytesInBed)
        Dim newSsOuterInBed As UInt16 = CUShort(ReadStackValueAt(cursorInBed, widthInBed, fabricInBed) And &HFFFFUI)
        ValidateOuterStackSegment(newSsOuterInBed, targetCplInBed, fabricInBed)
        LoadCodeSegment(newCsInBed, targetInBed, targetCplInBed, newEipInBed And WidthMask(widthInBed), fabricInBed)
        LoadStackSegmentForPrivilegeReturn(newSsOuterInBed, targetCplInBed, fabricInBed)
        SetStackPointer(newEspOuterInBed)
        ApplyIretFlags(newFlagsInBed, widthInBed, targetCplInBed)
        ClearDataSegmentsInvalidAtCpl(fabricInBed)
    End Sub

    Private Sub ApplyIretFlags(newFlagsInBed As UInteger, widthInBed As Integer, resultingCplInBed As Byte)
        Dim oldFlagsInBed As UInteger = _cpuInBed.MutableEflags
        Dim maskInBed As UInteger = If(widthInBed = 16, &HFFFFUI, &H3FFFFFUI)
        If resultingCplInBed <> 0 Then maskInBed = maskInBed And Not (FlagIopl Or FlagVm)
        If resultingCplInBed > CByte((oldFlagsInBed >> 12) And 3UI) Then maskInBed = maskInBed And Not FlagIf
        _cpuInBed.MutableEflags = (oldFlagsInBed And Not maskInBed) Or (newFlagsInBed And maskInBed) Or &H2UI
    End Sub

    Private Sub SwitchTask(targetSelectorInBed As UInt16,
                           nestedInBed As Boolean,
                           iretReturnInBed As Boolean,
                           fabricInBed As IProcessorLocalBusFabric)
        If SelectorIsNull(targetSelectorInBed) OrElse (targetSelectorInBed And 4US) <> 0US Then
            ThrowFault(Intel486ExceptionVector.InvalidTss, "Task switch requires a non-null GDT TSS selector.", True, SelectorErrorCode(targetSelectorInBed))
        End If
        Dim targetInBed As Intel486Descriptor = ReadDescriptor(targetSelectorInBed, fabricInBed,
                                                               Intel486ExceptionVector.InvalidTss,
                                                               "Target TSS selector invalid.")
        If iretReturnInBed Then
            If Not targetInBed.IsBusy386Tss Then ThrowFault(Intel486ExceptionVector.InvalidTss, "IRET task target must be busy.", True, SelectorErrorCode(targetSelectorInBed))
        ElseIf Not targetInBed.IsAvailable386Tss Then
            ThrowFault(Intel486ExceptionVector.GeneralProtection, "Task target must be an available 386/486 TSS.", True, SelectorErrorCode(targetSelectorInBed))
        End If
        If Not targetInBed.Present Then ThrowFault(Intel486ExceptionVector.SegmentNotPresent, "Target TSS not present.", True, SelectorErrorCode(targetSelectorInBed))
        If targetInBed.Limit < &H67UI Then ThrowFault(Intel486ExceptionVector.InvalidTss, "386/486 TSS limit is below 67h.", True, SelectorErrorCode(targetSelectorInBed))

        Dim oldTaskInBed As Intel486SystemSelectorRegister = _taskRegisterInBed
        If oldTaskInBed.Valid AndAlso oldTaskInBed.Limit >= &H67UI Then
            SaveCurrentTaskState(oldTaskInBed, fabricInBed)
        End If
        If nestedInBed Then WriteSystemLinearWord(targetInBed.BaseAddress, oldTaskInBed.Selector, fabricInBed)

        If iretReturnInBed AndAlso oldTaskInBed.Valid Then
            Dim oldDescriptorInBed As Intel486Descriptor
            If TryReadDescriptor(oldTaskInBed.Selector, fabricInBed, oldDescriptorInBed) Then MarkTssBusy(oldDescriptorInBed, False, fabricInBed)
        ElseIf Not nestedInBed AndAlso oldTaskInBed.Valid Then
            Dim oldDescriptorInBed As Intel486Descriptor
            If TryReadDescriptor(oldTaskInBed.Selector, fabricInBed, oldDescriptorInBed) AndAlso oldDescriptorInBed.IsBusy386Tss Then MarkTssBusy(oldDescriptorInBed, False, fabricInBed)
        End If
        If Not iretReturnInBed Then MarkTssBusy(targetInBed, True, fabricInBed)

        _taskRegisterInBed = New Intel486SystemSelectorRegister With {
            .Selector = targetSelectorInBed,
            .Valid = True,
            .BaseAddress = targetInBed.BaseAddress,
            .Limit = targetInBed.Limit,
            .TypeField = &HB,
            .DescriptorPrivilegeLevel = targetInBed.DescriptorPrivilegeLevel,
            .Present = True,
            .Default32 = True
        }
        LoadTaskState(targetInBed.BaseAddress, fabricInBed)
        If nestedInBed Then
            SetFlag(FlagNt, True)
        ElseIf iretReturnInBed Then
            SetFlag(FlagNt, False)
        End If
    End Sub

    Private Sub SaveCurrentTaskState(taskInBed As Intel486SystemSelectorRegister,
                                     fabricInBed As IProcessorLocalBusFabric)
        Dim baseInBed As UInteger = taskInBed.BaseAddress
        WriteSystemLinearDword(baseInBed + &H1CUI, _mmuInBed.CR3, fabricInBed)
        WriteSystemLinearDword(baseInBed + &H20UI, _cpuInBed.MutableEip, fabricInBed)
        WriteSystemLinearDword(baseInBed + &H24UI, _cpuInBed.MutableEflags, fabricInBed)
        WriteSystemLinearDword(baseInBed + &H28UI, _cpuInBed.ReadGeneralRegister32(0), fabricInBed)
        WriteSystemLinearDword(baseInBed + &H2CUI, _cpuInBed.ReadGeneralRegister32(1), fabricInBed)
        WriteSystemLinearDword(baseInBed + &H30UI, _cpuInBed.ReadGeneralRegister32(2), fabricInBed)
        WriteSystemLinearDword(baseInBed + &H34UI, _cpuInBed.ReadGeneralRegister32(3), fabricInBed)
        WriteSystemLinearDword(baseInBed + &H38UI, _cpuInBed.ReadGeneralRegister32(4), fabricInBed)
        WriteSystemLinearDword(baseInBed + &H3CUI, _cpuInBed.ReadGeneralRegister32(5), fabricInBed)
        WriteSystemLinearDword(baseInBed + &H40UI, _cpuInBed.ReadGeneralRegister32(6), fabricInBed)
        WriteSystemLinearDword(baseInBed + &H44UI, _cpuInBed.ReadGeneralRegister32(7), fabricInBed)
        WriteSystemLinearWord(baseInBed + &H48UI, _mmuInBed.GetSegment(Intel486SegmentRegister.ES).Selector, fabricInBed)
        WriteSystemLinearWord(baseInBed + &H4CUI, _mmuInBed.GetSegment(Intel486SegmentRegister.CS).Selector, fabricInBed)
        WriteSystemLinearWord(baseInBed + &H50UI, _mmuInBed.GetSegment(Intel486SegmentRegister.SS).Selector, fabricInBed)
        WriteSystemLinearWord(baseInBed + &H54UI, _mmuInBed.GetSegment(Intel486SegmentRegister.DS).Selector, fabricInBed)
        WriteSystemLinearWord(baseInBed + &H58UI, _mmuInBed.GetSegment(Intel486SegmentRegister.FS).Selector, fabricInBed)
        WriteSystemLinearWord(baseInBed + &H5CUI, _mmuInBed.GetSegment(Intel486SegmentRegister.GS).Selector, fabricInBed)
        WriteSystemLinearWord(baseInBed + &H60UI, _ldtrInBed.Selector, fabricInBed)
    End Sub

    Private Sub LoadTaskState(baseInBed As UInteger, fabricInBed As IProcessorLocalBusFabric)
        _mmuInBed.WriteCR3(ReadSystemLinearDword(baseInBed + &H1CUI, fabricInBed))
        _cpuInBed.MutableEip = ReadSystemLinearDword(baseInBed + &H20UI, fabricInBed)
        _cpuInBed.MutableEflags = ReadSystemLinearDword(baseInBed + &H24UI, fabricInBed)
        For registerInBed As Integer = 0 To 7
            _cpuInBed.WriteGeneralRegister32(registerInBed, ReadSystemLinearDword(baseInBed + &H28UI + CUInt(registerInBed * 4), fabricInBed))
        Next
        Dim esInBed As UInt16 = ReadSystemLinearWord(baseInBed + &H48UI, fabricInBed)
        Dim csInBed As UInt16 = ReadSystemLinearWord(baseInBed + &H4CUI, fabricInBed)
        Dim ssInBed As UInt16 = ReadSystemLinearWord(baseInBed + &H50UI, fabricInBed)
        Dim dsInBed As UInt16 = ReadSystemLinearWord(baseInBed + &H54UI, fabricInBed)
        Dim fsInBed As UInt16 = ReadSystemLinearWord(baseInBed + &H58UI, fabricInBed)
        Dim gsInBed As UInt16 = ReadSystemLinearWord(baseInBed + &H5CUI, fabricInBed)
        Dim ldtInBed As UInt16 = ReadSystemLinearWord(baseInBed + &H60UI, fabricInBed)

        LoadLdtr(ldtInBed, fabricInBed)
        Dim csDescriptorInBed As Intel486Descriptor = ReadDescriptor(csInBed, fabricInBed, Intel486ExceptionVector.InvalidTss, "Task CS selector invalid.")
        If Not csDescriptorInBed.Executable Then ThrowFault(Intel486ExceptionVector.InvalidTss, "Task CS is not executable.", True, SelectorErrorCode(csInBed))
        Dim targetCplInBed As Byte = SelectorRpl(csInBed)
        If Not csDescriptorInBed.ConformingCode AndAlso csDescriptorInBed.DescriptorPrivilegeLevel <> targetCplInBed Then ThrowFault(Intel486ExceptionVector.InvalidTss, "Task CS privilege mismatch.", True, SelectorErrorCode(csInBed))
        _cplInBed = targetCplInBed
        LoadStackSegmentForPrivilegeReturn(ssInBed, targetCplInBed, fabricInBed)
        LoadCodeSegment(csInBed, csDescriptorInBed, targetCplInBed, _cpuInBed.MutableEip, fabricInBed)
        LoadSegmentSelector(Intel486SegmentRegister.ES, esInBed, fabricInBed)
        LoadSegmentSelector(Intel486SegmentRegister.DS, dsInBed, fabricInBed)
        LoadSegmentSelector(Intel486SegmentRegister.FS, fsInBed, fabricInBed)
        LoadSegmentSelector(Intel486SegmentRegister.GS, gsInBed, fabricInBed)
        _maskableInterruptShadowInBed = 0
        _haltedInBed = False
    End Sub

    Private Sub LoadLdtr(selectorInBed As UInt16, fabricInBed As IProcessorLocalBusFabric)
        If SelectorIsNull(selectorInBed) Then
            _ldtrInBed = New Intel486SystemSelectorRegister()
            Return
        End If
        If (selectorInBed And 4US) <> 0US Then ThrowFault(Intel486ExceptionVector.GeneralProtection, "LLDT selector must reference GDT.", True, SelectorErrorCode(selectorInBed))
        Dim descriptorInBed As Intel486Descriptor = ReadDescriptor(selectorInBed, fabricInBed, Intel486ExceptionVector.GeneralProtection, "LDT selector invalid.")
        If Not descriptorInBed.IsLdt Then ThrowFault(Intel486ExceptionVector.GeneralProtection, "LLDT selector is not an LDT descriptor.", True, SelectorErrorCode(selectorInBed))
        If Not descriptorInBed.Present Then ThrowFault(Intel486ExceptionVector.SegmentNotPresent, "LDT not present.", True, SelectorErrorCode(selectorInBed))
        _ldtrInBed = New Intel486SystemSelectorRegister With {
            .Selector = selectorInBed,
            .Valid = True,
            .BaseAddress = descriptorInBed.BaseAddress,
            .Limit = descriptorInBed.Limit,
            .TypeField = descriptorInBed.TypeField,
            .DescriptorPrivilegeLevel = descriptorInBed.DescriptorPrivilegeLevel,
            .Present = True,
            .Default32 = False
        }
    End Sub

    Private Sub LoadTaskRegister(selectorInBed As UInt16, fabricInBed As IProcessorLocalBusFabric)
        If SelectorIsNull(selectorInBed) OrElse (selectorInBed And 4US) <> 0US Then ThrowFault(Intel486ExceptionVector.GeneralProtection, "LTR requires non-null GDT selector.", True, SelectorErrorCode(selectorInBed))
        Dim descriptorInBed As Intel486Descriptor = ReadDescriptor(selectorInBed, fabricInBed, Intel486ExceptionVector.GeneralProtection, "LTR selector invalid.")
        If Not descriptorInBed.IsAvailable386Tss Then ThrowFault(Intel486ExceptionVector.GeneralProtection, "LTR requires available 386/486 TSS.", True, SelectorErrorCode(selectorInBed))
        If Not descriptorInBed.Present Then ThrowFault(Intel486ExceptionVector.SegmentNotPresent, "LTR target TSS not present.", True, SelectorErrorCode(selectorInBed))
        If descriptorInBed.Limit < &H67UI Then ThrowFault(Intel486ExceptionVector.InvalidTss, "LTR TSS too small.", True, SelectorErrorCode(selectorInBed))
        MarkTssBusy(descriptorInBed, True, fabricInBed)
        _taskRegisterInBed = New Intel486SystemSelectorRegister With {
            .Selector = selectorInBed,
            .Valid = True,
            .BaseAddress = descriptorInBed.BaseAddress,
            .Limit = descriptorInBed.Limit,
            .TypeField = &HB,
            .DescriptorPrivilegeLevel = descriptorInBed.DescriptorPrivilegeLevel,
            .Present = True,
            .Default32 = True
        }
    End Sub

    ' -------------------------
    ' 0F opcode map (386 + 486 additions)
    ' -------------------------

    Private Sub Execute0F(secondInBed As Byte,
                          operandWidthInBed As Integer,
                          operand32InBed As Boolean,
                          address32InBed As Boolean,
                          prefixesInBed As Intel486PrefixState,
                          fabricInBed As IProcessorLocalBusFabric)
        Select Case secondInBed
            Case &H0
                Execute0FGroup6(address32InBed, prefixesInBed, fabricInBed)
            Case &H1
                Execute0FGroup7(operandWidthInBed, address32InBed, prefixesInBed, fabricInBed)
            Case &H2, &H3
                RejectLock(prefixesInBed)
                ExecuteLarLsl(secondInBed, operandWidthInBed, address32InBed, prefixesInBed, fabricInBed)
            Case &H6
                RejectLock(prefixesInBed)
                EnforcePrivilegeZero("CLTS")
                TryWriteCr0(_mmuInBed.CR0 And Not Cr0Ts)
            Case &H8, &H9
                RejectLock(prefixesInBed)
                EnforcePrivilegeZero(If(secondInBed = &H8, "INVD", "WBINVD"))
                ' CP05 has no cache array yet.  CP06 consumes these architectural
                ' operations in the real 8 KiB cache/write-buffer engine.
            Case &H20, &H21, &H22, &H23
                RejectLock(prefixesInBed)
                ExecuteMovControlDebug(secondInBed, address32InBed, fabricInBed)
            Case &H80 To &H8F
                RejectLock(prefixesInBed)
                Dim displacementInBed As Long = If(operand32InBed, FetchSignedDwordAsLong(fabricInBed), FetchSignedWord(fabricInBed))
                If ConditionTrue(secondInBed And &HF) Then BranchRelative(displacementInBed, operand32InBed)
            Case &H90 To &H9F
                If prefixesInBed.LockPresent Then ThrowFault(Intel486ExceptionVector.InvalidOpcode, "LOCK is invalid with SETcc.")
                Dim modRmInBed As Intel486DecodedModRm = DecodeModRm(address32InBed, fabricInBed)
                WriteRm(modRmInBed, 8, If(ConditionTrue(secondInBed And &HF), 1UI, 0UI), prefixesInBed, fabricInBed)
            Case &HA0
                RejectLock(prefixesInBed) : PushValue(_mmuInBed.GetSegment(Intel486SegmentRegister.FS).Selector, operandWidthInBed, fabricInBed)
            Case &HA1
                RejectLock(prefixesInBed) : LoadSegmentFromStack(Intel486SegmentRegister.FS, operandWidthInBed, fabricInBed)
            Case &HA3, &HAB, &HB3, &HBB
                ExecuteBitRegister(secondInBed, operandWidthInBed, address32InBed, prefixesInBed, fabricInBed)
            Case &HA4, &HA5, &HAC, &HAD
                ExecuteDoubleShift(secondInBed, operandWidthInBed, address32InBed, prefixesInBed, fabricInBed)
            Case &HA8
                RejectLock(prefixesInBed) : PushValue(_mmuInBed.GetSegment(Intel486SegmentRegister.GS).Selector, operandWidthInBed, fabricInBed)
            Case &HA9
                RejectLock(prefixesInBed) : LoadSegmentFromStack(Intel486SegmentRegister.GS, operandWidthInBed, fabricInBed)
            Case &HAF
                RejectLock(prefixesInBed)
                ExecuteTwoOperandImul(operandWidthInBed, address32InBed, prefixesInBed, fabricInBed)
            Case &HB0, &HB1
                ExecuteCmpxchg(secondInBed, operandWidthInBed, address32InBed, prefixesInBed, fabricInBed)
            Case &HB2
                RejectLock(prefixesInBed) : ExecuteLoadFarPointer(Intel486SegmentRegister.SS, operandWidthInBed, address32InBed, prefixesInBed, fabricInBed)
            Case &HB4
                RejectLock(prefixesInBed) : ExecuteLoadFarPointer(Intel486SegmentRegister.FS, operandWidthInBed, address32InBed, prefixesInBed, fabricInBed)
            Case &HB5
                RejectLock(prefixesInBed) : ExecuteLoadFarPointer(Intel486SegmentRegister.GS, operandWidthInBed, address32InBed, prefixesInBed, fabricInBed)
            Case &HB6, &HB7, &HBE, &HBF
                RejectLock(prefixesInBed)
                ExecuteMovExtend(secondInBed, operandWidthInBed, address32InBed, prefixesInBed, fabricInBed)
            Case &HBA
                ExecuteBitImmediateGroup(operandWidthInBed, address32InBed, prefixesInBed, fabricInBed)
            Case &HBC, &HBD
                RejectLock(prefixesInBed)
                ExecuteBitScan(secondInBed, operandWidthInBed, address32InBed, prefixesInBed, fabricInBed)
            Case &HC0, &HC1
                ExecuteXadd(secondInBed, operandWidthInBed, address32InBed, prefixesInBed, fabricInBed)
            Case &HC8 To &HCF
                RejectLock(prefixesInBed)
                If Not operand32InBed Then ThrowFault(Intel486ExceptionVector.InvalidOpcode, "BSWAP has no 16-bit form on Intel486.")
                Dim registerInBed As Integer = secondInBed And 7
                Dim valueInBed As UInteger = _cpuInBed.ReadGeneralRegister32(registerInBed)
                Dim swappedInBed As UInteger = ((valueInBed And &HFFUI) << 24) Or
                                               ((valueInBed And &HFF00UI) << 8) Or
                                               ((valueInBed And &HFF0000UI) >> 8) Or
                                               ((valueInBed And &HFF000000UI) >> 24)
                _cpuInBed.WriteGeneralRegister32(registerInBed, swappedInBed)
            Case &HA2
                ' The selected 1992 stepping-B DX2/66 predates CPUID support.
                ThrowFault(Intel486ExceptionVector.InvalidOpcode, "CPUID is not implemented by the selected 1992 Intel486 DX2 stepping B.")
            Case Else
                ThrowFault(Intel486ExceptionVector.InvalidOpcode, "Undefined/unsupported Intel486 0F opcode " & secondInBed.ToString("X2") & "h.")
        End Select
    End Sub

    Private Sub Execute0FGroup6(address32InBed As Boolean,
                                prefixesInBed As Intel486PrefixState,
                                fabricInBed As IProcessorLocalBusFabric)
        RejectLock(prefixesInBed)
        If Not _mmuInBed.ProtectedModeEnabled OrElse Virtual8086Mode Then
            ThrowFault(Intel486ExceptionVector.InvalidOpcode, "0F 00 protected-system instructions are undefined outside protected mode.")
        End If
        Dim modRmInBed As Intel486DecodedModRm = DecodeModRm(address32InBed, fabricInBed)
        Select Case modRmInBed.RegField
            Case 0 ' SLDT
                WriteRm(modRmInBed, 16, _ldtrInBed.Selector, prefixesInBed, fabricInBed)
            Case 1 ' STR
                WriteRm(modRmInBed, 16, _taskRegisterInBed.Selector, prefixesInBed, fabricInBed)
            Case 2 ' LLDT
                EnforcePrivilegeZero("LLDT")
                LoadLdtr(CUShort(ReadRm(modRmInBed, 16, prefixesInBed, fabricInBed)), fabricInBed)
            Case 3 ' LTR
                EnforcePrivilegeZero("LTR")
                LoadTaskRegister(CUShort(ReadRm(modRmInBed, 16, prefixesInBed, fabricInBed)), fabricInBed)
            Case 4, 5 ' VERR/VERW
                Dim selectorInBed As UInt16 = CUShort(ReadRm(modRmInBed, 16, prefixesInBed, fabricInBed))
                Dim descriptorInBed As Intel486Descriptor
                Dim validInBed As Boolean = TryReadDescriptor(selectorInBed, fabricInBed, descriptorInBed)
                If validInBed Then
                    validInBed = descriptorInBed.IsCodeOrData AndAlso descriptorInBed.Present
                    If modRmInBed.RegField = 4 Then
                        validInBed = validInBed AndAlso (Not descriptorInBed.Executable OrElse descriptorInBed.ReadableCode)
                    Else
                        validInBed = validInBed AndAlso descriptorInBed.WritableData
                    End If
                    If validInBed AndAlso Not (descriptorInBed.Executable AndAlso descriptorInBed.ConformingCode) Then
                        validInBed = Math.Max(CInt(_cplInBed), CInt(SelectorRpl(selectorInBed))) <= descriptorInBed.DescriptorPrivilegeLevel
                    End If
                End If
                SetFlag(FlagZf, validInBed)
            Case Else
                ThrowFault(Intel486ExceptionVector.InvalidOpcode, "Undefined 0F 00 group operation.")
        End Select
    End Sub

    Private Sub Execute0FGroup7(operandWidthInBed As Integer,
                                address32InBed As Boolean,
                                prefixesInBed As Intel486PrefixState,
                                fabricInBed As IProcessorLocalBusFabric)
        RejectLock(prefixesInBed)
        Dim modRmInBed As Intel486DecodedModRm = DecodeModRm(address32InBed, fabricInBed)
        Select Case modRmInBed.RegField
            Case 0, 1 ' SGDT/SIDT
                If modRmInBed.IsRegisterOperand Then ThrowFault(Intel486ExceptionVector.InvalidOpcode, "SGDT/SIDT require memory operand.")
                Dim tableInBed As Intel486DescriptorTableRegister = If(modRmInBed.RegField = 0, _gdtrInBed, _idtrInBed)
                Dim segmentInBed As Intel486SegmentRegister = EffectiveOperandSegment(modRmInBed, prefixesInBed)
                WriteMemoryWord(segmentInBed, modRmInBed.EffectiveOffset, tableInBed.Limit, fabricInBed)
                Dim baseToStoreInBed As UInteger = tableInBed.BaseAddress
                If operandWidthInBed = 16 Then baseToStoreInBed = baseToStoreInBed And &HFFFFFFUI
                WriteMemoryDword(segmentInBed, modRmInBed.EffectiveOffset + 2UI, baseToStoreInBed, fabricInBed)
            Case 2, 3 ' LGDT/LIDT
                EnforcePrivilegeZero(If(modRmInBed.RegField = 2, "LGDT", "LIDT"))
                If modRmInBed.IsRegisterOperand Then ThrowFault(Intel486ExceptionVector.InvalidOpcode, "LGDT/LIDT require memory operand.")
                Dim segmentInBed As Intel486SegmentRegister = EffectiveOperandSegment(modRmInBed, prefixesInBed)
                Dim limitInBed As UInt16 = ReadMemoryWord(segmentInBed, modRmInBed.EffectiveOffset, fabricInBed)
                Dim baseInBed As UInteger = ReadMemoryDword(segmentInBed, modRmInBed.EffectiveOffset + 2UI, fabricInBed)
                If operandWidthInBed = 16 Then baseInBed = baseInBed And &HFFFFFFUI
                If modRmInBed.RegField = 2 Then
                    _gdtrInBed = New Intel486DescriptorTableRegister With {.BaseAddress = baseInBed, .Limit = limitInBed}
                Else
                    _idtrInBed = New Intel486DescriptorTableRegister With {.BaseAddress = baseInBed, .Limit = limitInBed}
                End If
            Case 4 ' SMSW
                WriteRm(modRmInBed, 16, _mmuInBed.CR0 And &HFFFFUI, prefixesInBed, fabricInBed)
            Case 6 ' LMSW
                EnforcePrivilegeZero("LMSW")
                Dim sourceInBed As UInteger = ReadRm(modRmInBed, 16, prefixesInBed, fabricInBed) And &HFUI
                Dim oldInBed As UInteger = _mmuInBed.CR0
                Dim lowInBed As UInteger = sourceInBed
                If (oldInBed And Cr0Pe) <> 0UI Then lowInBed = lowInBed Or Cr0Pe
                TryWriteCr0((oldInBed And Not &HFUI) Or lowInBed)
            Case 7 ' INVLPG
                EnforcePrivilegeZero("INVLPG")
                If modRmInBed.IsRegisterOperand Then ThrowFault(Intel486ExceptionVector.InvalidOpcode, "INVLPG requires a memory effective address.")
                Dim segmentInBed As Intel486SegmentRegister = EffectiveOperandSegment(modRmInBed, prefixesInBed)
                Dim linearInBed As UInteger
                Try
                    linearInBed = _mmuInBed.SegmentToLinear(segmentInBed, modRmInBed.EffectiveOffset, 1UI, Intel486MemoryAccessKind.Read)
                Catch exInBed As InvalidOperationException
                    ThrowFault(Intel486ExceptionVector.GeneralProtection, exInBed.Message, True, 0US)
                    Return
                End Try
                _mmuInBed.InvalidatePage(linearInBed)
            Case Else
                ThrowFault(Intel486ExceptionVector.InvalidOpcode, "Undefined 0F 01 group operation.")
        End Select
    End Sub

    Private Sub TryWriteCr0(valueInBed As UInteger)
        Try
            _mmuInBed.WriteCR0(valueInBed)
        Catch exInBed As InvalidOperationException
            ThrowFault(Intel486ExceptionVector.GeneralProtection, exInBed.Message, True, 0US)
        End Try
    End Sub

    Private Sub ExecuteMovControlDebug(secondInBed As Byte,
                                       address32InBed As Boolean,
                                       fabricInBed As IProcessorLocalBusFabric)
        EnforcePrivilegeZero("MOV control/debug register")
        Dim modRmInBed As Intel486DecodedModRm = DecodeModRm(address32InBed, fabricInBed)
        If Not modRmInBed.IsRegisterOperand Then ThrowFault(Intel486ExceptionVector.InvalidOpcode, "MOV CR/DR requires register operand.")
        Dim specialInBed As Integer = modRmInBed.RegField
        Dim gpInBed As Integer = modRmInBed.RmField
        Select Case secondInBed
            Case &H20 ' MOV r32, CRn
                Dim valueInBed As UInteger
                Select Case specialInBed
                    Case 0 : valueInBed = _mmuInBed.CR0
                    Case 2 : valueInBed = _mmuInBed.CR2
                    Case 3 : valueInBed = _mmuInBed.CR3
                    Case Else : ThrowFault(Intel486ExceptionVector.InvalidOpcode, "Undefined Intel486 control register.") : Return
                End Select
                _cpuInBed.WriteGeneralRegister32(gpInBed, valueInBed)
            Case &H22 ' MOV CRn, r32
                Dim valueInBed As UInteger = _cpuInBed.ReadGeneralRegister32(gpInBed)
                Select Case specialInBed
                    Case 0 : TryWriteCr0(valueInBed)
                    Case 2
                        ' CR2 is architecturally written only by page faults; MOV to CR2 is accepted on 486 hardware.
                        _mmuInBed.SetCR2ForSoftware(valueInBed)
                    Case 3 : _mmuInBed.WriteCR3(valueInBed)
                    Case Else : ThrowFault(Intel486ExceptionVector.InvalidOpcode, "Undefined Intel486 control register.")
                End Select
            Case &H21 ' MOV r32, DRn
                If specialInBed = 4 OrElse specialInBed = 5 Then ThrowFault(Intel486ExceptionVector.InvalidOpcode, "DR4/DR5 are reserved on Intel486.")
                _cpuInBed.WriteGeneralRegister32(gpInBed, _cpuInBed.ReadDebugRegister(specialInBed))
            Case &H23 ' MOV DRn, r32
                If specialInBed = 4 OrElse specialInBed = 5 Then ThrowFault(Intel486ExceptionVector.InvalidOpcode, "DR4/DR5 are reserved on Intel486.")
                _cpuInBed.WriteDebugRegister(specialInBed, _cpuInBed.ReadGeneralRegister32(gpInBed))
        End Select
    End Sub

    Private Sub ExecuteLarLsl(secondInBed As Byte,
                              widthInBed As Integer,
                              address32InBed As Boolean,
                              prefixesInBed As Intel486PrefixState,
                              fabricInBed As IProcessorLocalBusFabric)
        If Not _mmuInBed.ProtectedModeEnabled OrElse Virtual8086Mode Then
            ThrowFault(Intel486ExceptionVector.InvalidOpcode, "LAR/LSL are defined only in protected mode on Intel486.")
        End If
        Dim modRmInBed As Intel486DecodedModRm = DecodeModRm(address32InBed, fabricInBed)
        Dim selectorInBed As UInt16 = CUShort(ReadRm(modRmInBed, 16, prefixesInBed, fabricInBed))
        Dim descriptorInBed As Intel486Descriptor
        Dim validInBed As Boolean = TryReadDescriptor(selectorInBed, fabricInBed, descriptorInBed)
        If validInBed Then
            Dim typePermittedInBed As Boolean = descriptorInBed.IsCodeOrData OrElse
                descriptorInBed.IsAvailable386Tss OrElse descriptorInBed.IsBusy386Tss OrElse descriptorInBed.IsLdt OrElse
                descriptorInBed.Is286CallGate OrElse descriptorInBed.Is386CallGate OrElse
                descriptorInBed.IsTaskGate
            validInBed = typePermittedInBed AndAlso
                         ((descriptorInBed.Executable AndAlso descriptorInBed.ConformingCode) OrElse
                          Math.Max(CInt(_cplInBed), CInt(SelectorRpl(selectorInBed))) <= descriptorInBed.DescriptorPrivilegeLevel)
        End If
        SetFlag(FlagZf, validInBed)
        If Not validInBed Then Return
        If secondInBed = &H2 Then
            ' LAR returns access-rights bits in their architectural positions.
            Dim accessInBed As UInteger = descriptorInBed.RawHigh And &HFF00UI
            accessInBed = accessInBed Or (descriptorInBed.RawHigh And &HF00000UI)
            WriteRegisterByWidth(modRmInBed.RegField, widthInBed, accessInBed)
        Else
            WriteRegisterByWidth(modRmInBed.RegField, widthInBed, descriptorInBed.Limit)
        End If
    End Sub

    Private Sub ExecuteMovExtend(secondInBed As Byte,
                                 widthInBed As Integer,
                                 address32InBed As Boolean,
                                 prefixesInBed As Intel486PrefixState,
                                 fabricInBed As IProcessorLocalBusFabric)
        Dim sourceWidthInBed As Integer = If(secondInBed = &HB6 OrElse secondInBed = &HBE, 8, 16)
        Dim modRmInBed As Intel486DecodedModRm = DecodeModRm(address32InBed, fabricInBed)
        Dim sourceInBed As UInteger = ReadRm(modRmInBed, sourceWidthInBed, prefixesInBed, fabricInBed)
        Dim valueInBed As UInteger
        If secondInBed = &HBE OrElse secondInBed = &HBF Then
            valueInBed = UnsignedBits(SignedValue(sourceInBed, sourceWidthInBed), widthInBed)
        Else
            valueInBed = sourceInBed
        End If
        WriteRegisterByWidth(modRmInBed.RegField, widthInBed, valueInBed)
    End Sub

    Private Sub ExecuteTwoOperandImul(widthInBed As Integer,
                                     address32InBed As Boolean,
                                     prefixesInBed As Intel486PrefixState,
                                     fabricInBed As IProcessorLocalBusFabric)
        Dim modRmInBed As Intel486DecodedModRm = DecodeModRm(address32InBed, fabricInBed)
        Dim leftInBed As New BigInteger(SignedValue(ReadRegisterByWidth(modRmInBed.RegField, widthInBed), widthInBed))
        Dim rightInBed As New BigInteger(SignedValue(ReadRm(modRmInBed, widthInBed, prefixesInBed, fabricInBed), widthInBed))
        Dim productInBed As BigInteger = leftInBed * rightInBed
        Dim minInBed As BigInteger = -(BigInteger.One << (widthInBed - 1))
        Dim maxInBed As BigInteger = (BigInteger.One << (widthInBed - 1)) - BigInteger.One
        Dim overflowInBed As Boolean = productInBed < minInBed OrElse productInBed > maxInBed
        Dim modulusInBed As BigInteger = BigInteger.One << widthInBed
        Dim bitsInBed As BigInteger = productInBed Mod modulusInBed
        If bitsInBed.Sign < 0 Then bitsInBed += modulusInBed
        WriteRegisterByWidth(modRmInBed.RegField, widthInBed, CUInt(bitsInBed))
        SetFlag(FlagCf, overflowInBed) : SetFlag(FlagOf, overflowInBed)
    End Sub

    Private Sub ExecuteCmpxchg(secondInBed As Byte,
                               operandWidthInBed As Integer,
                               address32InBed As Boolean,
                               prefixesInBed As Intel486PrefixState,
                               fabricInBed As IProcessorLocalBusFabric)
        Dim widthInBed As Integer = If(secondInBed = &HB0, 8, operandWidthInBed)
        Dim modRmInBed As Intel486DecodedModRm = DecodeModRm(address32InBed, fabricInBed)
        If prefixesInBed.LockPresent AndAlso modRmInBed.IsRegisterOperand Then ThrowFault(Intel486ExceptionVector.InvalidOpcode, "LOCK CMPXCHG requires memory destination.")
        Dim destinationInBed As UInteger = ReadRm(modRmInBed, widthInBed, prefixesInBed, fabricInBed)
        Dim accumulatorInBed As UInteger = ReadRegisterByWidth(0, widthInBed)
        SubtractValues(accumulatorInBed, destinationInBed, 0UI, widthInBed)
        If accumulatorInBed = destinationInBed Then
            SetFlag(FlagZf, True)
            WriteRm(modRmInBed, widthInBed, ReadRegisterByWidth(modRmInBed.RegField, widthInBed), prefixesInBed, fabricInBed, prefixesInBed.LockPresent)
        Else
            SetFlag(FlagZf, False)
            WriteRegisterByWidth(0, widthInBed, destinationInBed)
        End If
    End Sub

    Private Sub ExecuteXadd(secondInBed As Byte,
                            operandWidthInBed As Integer,
                            address32InBed As Boolean,
                            prefixesInBed As Intel486PrefixState,
                            fabricInBed As IProcessorLocalBusFabric)
        Dim widthInBed As Integer = If(secondInBed = &HC0, 8, operandWidthInBed)
        Dim modRmInBed As Intel486DecodedModRm = DecodeModRm(address32InBed, fabricInBed)
        If prefixesInBed.LockPresent AndAlso modRmInBed.IsRegisterOperand Then ThrowFault(Intel486ExceptionVector.InvalidOpcode, "LOCK XADD requires memory destination.")
        Dim destinationInBed As UInteger = ReadRm(modRmInBed, widthInBed, prefixesInBed, fabricInBed)
        Dim sourceInBed As UInteger = ReadRegisterByWidth(modRmInBed.RegField, widthInBed)
        Dim resultInBed As UInteger = AddValues(destinationInBed, sourceInBed, 0UI, widthInBed)
        WriteRegisterByWidth(modRmInBed.RegField, widthInBed, destinationInBed)
        WriteRm(modRmInBed, widthInBed, resultInBed, prefixesInBed, fabricInBed, prefixesInBed.LockPresent)
    End Sub

    Private Sub ExecuteBitScan(secondInBed As Byte,
                               widthInBed As Integer,
                               address32InBed As Boolean,
                               prefixesInBed As Intel486PrefixState,
                               fabricInBed As IProcessorLocalBusFabric)
        Dim modRmInBed As Intel486DecodedModRm = DecodeModRm(address32InBed, fabricInBed)
        Dim sourceInBed As UInteger = ReadRm(modRmInBed, widthInBed, prefixesInBed, fabricInBed) And WidthMask(widthInBed)
        If sourceInBed = 0UI Then SetFlag(FlagZf, True) : Return
        SetFlag(FlagZf, False)
        Dim bitInBed As Integer
        If secondInBed = &HBC Then
            bitInBed = 0
            While (sourceInBed And (1UI << bitInBed)) = 0UI
                bitInBed += 1
            End While
        Else
            bitInBed = widthInBed - 1
            While (sourceInBed And (1UI << bitInBed)) = 0UI
                bitInBed -= 1
            End While
        End If
        WriteRegisterByWidth(modRmInBed.RegField, widthInBed, CUInt(bitInBed))
    End Sub

    Private Sub ExecuteBitRegister(secondInBed As Byte,
                                   widthInBed As Integer,
                                   address32InBed As Boolean,
                                   prefixesInBed As Intel486PrefixState,
                                   fabricInBed As IProcessorLocalBusFabric)
        Dim modRmInBed As Intel486DecodedModRm = DecodeModRm(address32InBed, fabricInBed)
        Dim operationInBed As Integer
        Select Case secondInBed
            Case &HA3 : operationInBed = 0
            Case &HAB : operationInBed = 1
            Case &HB3 : operationInBed = 2
            Case Else : operationInBed = 3
        End Select
        If prefixesInBed.LockPresent AndAlso (operationInBed = 0 OrElse modRmInBed.IsRegisterOperand) Then ThrowFault(Intel486ExceptionVector.InvalidOpcode, "LOCK invalid for this bit-test form.")
        Dim bitIndexInBed As Long = SignedValue(ReadRegisterByWidth(modRmInBed.RegField, widthInBed), widthInBed)
        ExecuteBitOperation(modRmInBed, widthInBed, bitIndexInBed, operationInBed, prefixesInBed, fabricInBed)
    End Sub

    Private Sub ExecuteBitImmediateGroup(widthInBed As Integer,
                                         address32InBed As Boolean,
                                         prefixesInBed As Intel486PrefixState,
                                         fabricInBed As IProcessorLocalBusFabric)
        Dim modRmInBed As Intel486DecodedModRm = DecodeModRm(address32InBed, fabricInBed)
        If modRmInBed.RegField < 4 OrElse modRmInBed.RegField > 7 Then ThrowFault(Intel486ExceptionVector.InvalidOpcode, "Undefined 0F BA group operation.")
        Dim operationInBed As Integer = modRmInBed.RegField - 4
        If prefixesInBed.LockPresent AndAlso (operationInBed = 0 OrElse modRmInBed.IsRegisterOperand) Then ThrowFault(Intel486ExceptionVector.InvalidOpcode, "LOCK invalid for this bit-test immediate form.")
        Dim bitIndexInBed As Long = FetchCodeByte(fabricInBed) And (widthInBed - 1)
        ExecuteBitOperation(modRmInBed, widthInBed, bitIndexInBed, operationInBed, prefixesInBed, fabricInBed)
    End Sub

    Private Sub ExecuteBitOperation(modRmInBed As Intel486DecodedModRm,
                                    widthInBed As Integer,
                                    bitIndexInBed As Long,
                                    operationInBed As Integer,
                                    prefixesInBed As Intel486PrefixState,
                                    fabricInBed As IProcessorLocalBusFabric)
        Dim targetInBed As Intel486DecodedModRm = modRmInBed
        Dim bitInOperandInBed As Integer
        If targetInBed.IsRegisterOperand Then
            bitInOperandInBed = CInt(bitIndexInBed And (widthInBed - 1))
        Else
            Dim wordIndexInBed As Long = FloorDivide(bitIndexInBed, widthInBed)
            Dim byteStrideInBed As Long = wordIndexInBed * (widthInBed \ 8)
            targetInBed.EffectiveOffset = UnsignedBits(CLng(targetInBed.EffectiveOffset) + byteStrideInBed, 32)
            bitInOperandInBed = CInt(bitIndexInBed - wordIndexInBed * widthInBed)
        End If
        Dim valueInBed As UInteger = ReadRm(targetInBed, widthInBed, prefixesInBed, fabricInBed)
        Dim bitMaskInBed As UInteger = 1UI << bitInOperandInBed
        SetFlag(FlagCf, (valueInBed And bitMaskInBed) <> 0UI)
        Select Case operationInBed
            Case 1 : valueInBed = valueInBed Or bitMaskInBed
            Case 2 : valueInBed = valueInBed And Not bitMaskInBed
            Case 3 : valueInBed = valueInBed Xor bitMaskInBed
        End Select
        If operationInBed <> 0 Then WriteRm(targetInBed, widthInBed, valueInBed, prefixesInBed, fabricInBed, prefixesInBed.LockPresent)
    End Sub

    Private Shared Function FloorDivide(valueInBed As Long, divisorInBed As Integer) As Long
        Dim quotientInBed As Long = valueInBed \ divisorInBed
        Dim remainderInBed As Long = valueInBed Mod divisorInBed
        If remainderInBed <> 0 AndAlso valueInBed < 0 Then quotientInBed -= 1
        Return quotientInBed
    End Function

    Private Sub ExecuteDoubleShift(secondInBed As Byte,
                                   widthInBed As Integer,
                                   address32InBed As Boolean,
                                   prefixesInBed As Intel486PrefixState,
                                   fabricInBed As IProcessorLocalBusFabric)
        Dim modRmInBed As Intel486DecodedModRm = DecodeModRm(address32InBed, fabricInBed)
        If prefixesInBed.LockPresent AndAlso modRmInBed.IsRegisterOperand Then ThrowFault(Intel486ExceptionVector.InvalidOpcode, "LOCK SHLD/SHRD requires memory destination.")
        Dim countInBed As Integer = If(secondInBed = &HA4 OrElse secondInBed = &HAC,
                                       CInt(FetchCodeByte(fabricInBed)),
                                       CInt(_cpuInBed.ReadGeneralRegister8(1))) And &H1F
        If countInBed = 0 Then Return
        If countInBed > widthInBed Then
            ' Intel documents result/flags undefined for counts beyond operand size.
            ' Preserve deterministic silicon state without inventing a fault.
            countInBed = countInBed Mod widthInBed
            If countInBed = 0 Then countInBed = widthInBed
        End If
        Dim destinationInBed As UInteger = ReadRm(modRmInBed, widthInBed, prefixesInBed, fabricInBed) And WidthMask(widthInBed)
        Dim sourceInBed As UInteger = ReadRegisterByWidth(modRmInBed.RegField, widthInBed) And WidthMask(widthInBed)
        Dim resultInBed As UInteger
        If secondInBed = &HA4 OrElse secondInBed = &HA5 Then
            Dim compositeInBed As ULong = (CULng(destinationInBed) << widthInBed) Or CULng(sourceInBed)
            resultInBed = CUInt((compositeInBed << countInBed >> widthInBed) And CULng(WidthMask(widthInBed)))
            SetFlag(FlagCf, ((destinationInBed >> (widthInBed - countInBed)) And 1UI) <> 0UI)
            If countInBed = 1 Then SetFlag(FlagOf, ((resultInBed Xor destinationInBed) And WidthSignBit(widthInBed)) <> 0UI)
        Else
            Dim modulusInBed As BigInteger = BigInteger.One << widthInBed
            Dim compositeInBed As BigInteger = (New BigInteger(sourceInBed) << widthInBed) Or New BigInteger(destinationInBed)
            resultInBed = CUInt((compositeInBed >> countInBed) Mod modulusInBed)
            SetFlag(FlagCf, ((destinationInBed >> (countInBed - 1)) And 1UI) <> 0UI)
            If countInBed = 1 Then SetFlag(FlagOf, ((destinationInBed Xor resultInBed) And WidthSignBit(widthInBed)) <> 0UI)
        End If
        SetSzpFlags(resultInBed, widthInBed)
        WriteRm(modRmInBed, widthInBed, resultInBed, prefixesInBed, fabricInBed, prefixesInBed.LockPresent)
    End Sub

End Class
