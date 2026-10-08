Imports System

Public Enum Intel486ExceptionVector As Byte
    DivideError = 0
    Debug = 1
    Nmi = 2
    Breakpoint = 3
    Overflow = 4
    Bounds = 5
    InvalidOpcode = 6
    DeviceNotAvailable = 7
    DoubleFault = 8
    CoprocessorSegmentOverrun = 9
    InvalidTss = 10
    SegmentNotPresent = 11
    StackFault = 12
    GeneralProtection = 13
    PageFault = 14
    FloatingPoint = 16
    AlignmentCheck = 17
End Enum

' Architectural exception raised by the native Intel486 execution engine.
' RestartEip always points to the instruction start for faults.  Traps may place
' the post-instruction EIP in RestartEip when they are dispatched after retirement.
Public NotInheritable Class Intel486CpuException
    Inherits Exception

    Public ReadOnly Property Vector As Intel486ExceptionVector
    Public ReadOnly Property HasErrorCode As Boolean
    Public ReadOnly Property ErrorCode As UInt16
    Public ReadOnly Property RestartEip As UInteger
    Public ReadOnly Property IsTrap As Boolean
    Public ReadOnly Property IsExternal As Boolean

    Public Sub New(vectorInBed As Intel486ExceptionVector,
                   restartEipInBed As UInteger,
                   messageInBed As String,
                   Optional hasErrorCodeInBed As Boolean = False,
                   Optional errorCodeInBed As UInt16 = 0US,
                   Optional isTrapInBed As Boolean = False,
                   Optional isExternalInBed As Boolean = False)
        MyBase.New(messageInBed)
        Vector = vectorInBed
        RestartEip = restartEipInBed
        HasErrorCode = hasErrorCodeInBed
        ErrorCode = errorCodeInBed
        IsTrap = isTrapInBed
        IsExternal = isExternalInBed
    End Sub
End Class

Public Enum Intel486RepeatPrefix As Byte
    None = 0
    Repne = 1
    Repe = 2
End Enum

Public Structure Intel486PrefixState
    Public SegmentOverridePresent As Boolean
    Public SegmentOverride As Intel486SegmentRegister
    Public Repeat As Intel486RepeatPrefix
    Public LockPresent As Boolean
    Public OperandSizeOverride As Boolean
    Public AddressSizeOverride As Boolean
    Public PrefixByteCount As Integer

    Public Function EffectiveOperand32(default32InBed As Boolean) As Boolean
        Return default32InBed Xor OperandSizeOverride
    End Function

    Public Function EffectiveAddress32(default32InBed As Boolean) As Boolean
        Return default32InBed Xor AddressSizeOverride
    End Function
End Structure

Public Structure Intel486DecodedModRm
    Public Raw As Byte
    Public ModField As Byte
    Public RegField As Byte
    Public RmField As Byte
    Public IsRegisterOperand As Boolean
    Public HasSib As Boolean
    Public SibRaw As Byte
    Public SibScale As Byte
    Public SibIndex As Byte
    Public SibBase As Byte
    Public Displacement As Integer
    Public DisplacementWidth As Byte
    Public EffectiveOffset As UInteger
    Public DefaultSegment As Intel486SegmentRegister
End Structure

Public Structure Intel486DescriptorTableRegister
    Public BaseAddress As UInteger
    Public Limit As UInt16
End Structure

Public Structure Intel486SystemSelectorRegister
    Public Selector As UInt16
    Public Valid As Boolean
    Public BaseAddress As UInteger
    Public Limit As UInteger
    Public TypeField As Byte
    Public DescriptorPrivilegeLevel As Byte
    Public Present As Boolean
    Public Default32 As Boolean
End Structure

Public Structure Intel486Descriptor
    Public DescriptorLinearAddress As UInteger
    Public RawLow As UInteger
    Public RawHigh As UInteger
    Public BaseAddress As UInteger
    Public Limit As UInteger
    Public TypeField As Byte
    Public SystemBit As Boolean
    Public DescriptorPrivilegeLevel As Byte
    Public Present As Boolean
    Public Available As Boolean
    Public Default32 As Boolean
    Public Granularity As Boolean

    Public ReadOnly Property IsCodeOrData As Boolean
        Get
            Return SystemBit
        End Get
    End Property

    Public ReadOnly Property Executable As Boolean
        Get
            Return SystemBit AndAlso (TypeField And &H8) <> 0
        End Get
    End Property

    Public ReadOnly Property ReadableCode As Boolean
        Get
            Return Executable AndAlso (TypeField And &H2) <> 0
        End Get
    End Property

    Public ReadOnly Property WritableData As Boolean
        Get
            Return SystemBit AndAlso Not Executable AndAlso (TypeField And &H2) <> 0
        End Get
    End Property

    Public ReadOnly Property ConformingCode As Boolean
        Get
            Return Executable AndAlso (TypeField And &H4) <> 0
        End Get
    End Property

    Public ReadOnly Property ExpandDownData As Boolean
        Get
            Return SystemBit AndAlso Not Executable AndAlso (TypeField And &H4) <> 0
        End Get
    End Property

    Public ReadOnly Property Accessed As Boolean
        Get
            Return SystemBit AndAlso (TypeField And &H1) <> 0
        End Get
    End Property

    Public ReadOnly Property IsAvailable386Tss As Boolean
        Get
            Return Not SystemBit AndAlso TypeField = &H9
        End Get
    End Property

    Public ReadOnly Property IsBusy386Tss As Boolean
        Get
            Return Not SystemBit AndAlso TypeField = &HB
        End Get
    End Property

    Public ReadOnly Property IsLdt As Boolean
        Get
            Return Not SystemBit AndAlso TypeField = &H2
        End Get
    End Property

    Public ReadOnly Property IsTaskGate As Boolean
        Get
            Return Not SystemBit AndAlso TypeField = &H5
        End Get
    End Property

    Public ReadOnly Property Is386CallGate As Boolean
        Get
            Return Not SystemBit AndAlso TypeField = &HC
        End Get
    End Property

    Public ReadOnly Property Is386InterruptGate As Boolean
        Get
            Return Not SystemBit AndAlso TypeField = &HE
        End Get
    End Property

    Public ReadOnly Property Is386TrapGate As Boolean
        Get
            Return Not SystemBit AndAlso TypeField = &HF
        End Get
    End Property

    Public ReadOnly Property Is286CallGate As Boolean
        Get
            Return Not SystemBit AndAlso TypeField = &H4
        End Get
    End Property

    Public ReadOnly Property Is286InterruptGate As Boolean
        Get
            Return Not SystemBit AndAlso TypeField = &H6
        End Get
    End Property

    Public ReadOnly Property Is286TrapGate As Boolean
        Get
            Return Not SystemBit AndAlso TypeField = &H7
        End Get
    End Property

    Public ReadOnly Property IsGate As Boolean
        Get
            Return Is286CallGate OrElse Is286InterruptGate OrElse Is286TrapGate OrElse
                   Is386CallGate OrElse Is386InterruptGate OrElse Is386TrapGate OrElse IsTaskGate
        End Get
    End Property

    Public ReadOnly Property GateSelector As UInt16
        Get
            Return CUShort((RawLow >> 16) And &HFFFFUI)
        End Get
    End Property

    Public ReadOnly Property GateOffset As UInteger
        Get
            Dim lowInBed As UInteger = RawLow And &HFFFFUI
            If Is386CallGate OrElse Is386InterruptGate OrElse Is386TrapGate Then
                Return lowInBed Or (RawHigh And &HFFFF0000UI)
            End If
            Return lowInBed
        End Get
    End Property

    Public ReadOnly Property GateParameterCount As Byte
        Get
            If Not (Is286CallGate OrElse Is386CallGate) Then Return 0
            Return CByte(RawHigh And &H1FUI)
        End Get
    End Property
End Structure

Public Structure Intel486InstructionResult
    Public StartEip As UInteger
    Public EndEip As UInteger
    Public Opcode As Byte
    Public Opcode2 As Byte
    Public HasOpcode2 As Boolean
    Public Retired As Boolean
    Public Halted As Boolean
    Public CoreClocksNominal As Integer
    Public MemoryOrIoCycles As Integer
End Structure
