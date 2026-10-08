Imports System

' On-die x87 execution block of an Intel486 DX/DX2 package.
' This is NOT a socketed 487 object.  It belongs physically to the DX2 silicon and
' shares the processor's core clock domain.  Checkpoint 07 fills in arithmetic/decode;
' this checkpoint establishes the exact architectural state container and reset rules.
Public NotInheritable Class Intel486IntegratedFpu
    Private ReadOnly _rawStackInBed(7, 9) As Byte
    Private _stateDefinedInBed As Boolean

    Public Property ControlWord As UInt16
    Public Property StatusWord As UInt16
    Public Property TagWord As UInt16
    Public Property InstructionPointerOffset As UInteger
    Public Property DataOperandOffset As UInteger
    Public Property InstructionPointerSelector As UInt16
    Public Property DataOperandSelector As UInt16
    Public Property LastOpcode As UInt16

    Public ReadOnly Property ArchitecturalStateDefined As Boolean
        Get
            Return _stateDefinedInBed
        End Get
    End Property

    Public Function ReadRawRegister80(indexInBed As Integer) As Byte()
        If indexInBed < 0 OrElse indexInBed > 7 Then Throw New ArgumentOutOfRangeException(NameOf(indexInBed))
        Dim resultInBed(9) As Byte
        For byteInBed As Integer = 0 To 9
            resultInBed(byteInBed) = _rawStackInBed(indexInBed, byteInBed)
        Next
        Return resultInBed
    End Function

    Public Sub WriteRawRegister80(indexInBed As Integer, valueInBed As Byte())
        If indexInBed < 0 OrElse indexInBed > 7 Then Throw New ArgumentOutOfRangeException(NameOf(indexInBed))
        If valueInBed Is Nothing OrElse valueInBed.Length <> 10 Then
            Throw New ArgumentException("An x87 register image is exactly 80 bits / 10 bytes.", NameOf(valueInBed))
        End If
        For byteInBed As Integer = 0 To 9
            _rawStackInBed(indexInBed, byteInBed) = valueInBed(byteInBed)
        Next
        _stateDefinedInBed = True
    End Sub

    ' FINIT/FNINIT architectural state and BIST-success FPU state.
    Public Sub InitializeArchitecturalState()
        ControlWord = &H37FUS
        StatusWord = 0US
        TagWord = &HFFFFUS
        InstructionPointerOffset = 0UI
        DataOperandOffset = 0UI
        InstructionPointerSelector = 0US
        DataOperandSelector = 0US
        LastOpcode = 0US
        For registerInBed As Integer = 0 To 7
            For byteInBed As Integer = 0 To 9
                _rawStackInBed(registerInBed, byteInBed) = 0
            Next
        Next
        _stateDefinedInBed = True
    End Sub

    ' Ordinary RESET does not initialize the FPU register set unless BIST ran.
    Public Sub ProcessorReset(bistExecutedInBed As Boolean)
        If bistExecutedInBed Then InitializeArchitecturalState()
    End Sub

    Public ReadOnly Property FloatingErrorAsserted As Nullable(Of Boolean)
        Get
            If Not _stateDefinedInBed Then Return Nothing
            Dim exceptionBitsInBed As UInt16 = CUShort(StatusWord And &H3FUS)
            Dim maskBitsInBed As UInt16 = CUShort(ControlWord And &H3FUS)
            Dim unmaskedInBed As UInteger = CUInt(exceptionBitsInBed) And (CUInt(maskBitsInBed) Xor &H3FUI)
            Return unmaskedInBed <> 0UI
        End Get
    End Property
End Class
