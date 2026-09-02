Imports System

' CROMWELL 80287 OBJECT BRICK 1
'
' Architectural state for the Intel 80287-class numeric coprocessor attached
' to the 80286.  The emulator remains synchronous for now, but the object owns
' the NPX stack, tags, control/status words, rounding, masked exception results,
' and the logical ERROR output.  The motherboard may wire ERROR/BUSY/IRQ13 in a
' later electrical brick without moving this architectural state again.
'
' NOTE: arithmetic storage is presently System.Double rather than a bit-exact
' 80-bit temporary-real implementation.  The object nevertheless prevents host
' CLR conversion exceptions from escaping into the machine and implements the
' architectural control/status/stack behavior for the supported instruction set.
Public NotInheritable Class Intel80287
    Private Const ExceptionMask As UShort = &H3FUS
    Private Const Ie As UShort = &H1US
    Private Const De As UShort = &H2US
    Private Const Ze As UShort = &H4US
    Private Const Oe As UShort = &H8US
    Private Const Ue As UShort = &H10US
    Private Const Pe As UShort = &H20US
    Private Const Sf As UShort = &H40US
    Private Const Es As UShort = &H80US
    Private Const C0 As UShort = &H100US
    Private Const C1 As UShort = &H200US
    Private Const C2 As UShort = &H400US
    Private Const TopMask As UShort = &H3800US
    Private Const C3 As UShort = &H4000US

    Private Const RcMask As UShort = &HC00US

    Private Const TagValid As Byte = 0
    Private Const TagZero As Byte = 1
    Private Const TagSpecial As Byte = 2
    Private Const TagEmpty As Byte = 3

    Private ReadOnly _registers(7) As Double
    Private ReadOnly _tags(7) As Byte
    Private _top As Integer
    Private _controlWord As UShort
    Private _statusWord As UShort
    Private _errorAsserted As Boolean
    Private _protectedMode As Boolean
    Private Structure DiagnosticInstructionSampleInBed
        Public Valid As Boolean
        Public CodeSegment As UShort
        Public InstructionOffset As UShort
        Public Opcode As Byte
        Public ModRm As Byte
        Public ControlWord As UShort
        Public StatusWord As UShort
        Public Top As Byte
        Public TagWord As UShort
    End Structure

    Private _diagnosticInstructionValid As Boolean
    Private _diagnosticInstructionCs As UShort
    Private _diagnosticInstructionIp As UShort
    Private _diagnosticInstructionOpcode As Byte
    Private _diagnosticInstructionModRm As Byte
    Private _diagnosticFirstStackFault As String = "<none>"
    Private ReadOnly _diagnosticErrorLogPath As String =
        System.IO.Path.Combine(AppContext.BaseDirectory, "Doutput", "80287-error-trace.txt")
    Private _diagnosticLoggedPendingFlags As UShort
    ' This recorder is always armed, so its hot path must not allocate.  Earlier
    ' versions constructed and queued a String for every ESC instruction; an
    ' x87-heavy QuickBASIC startup loop could therefore spend far more host time
    ' feeding diagnostics than executing the guest.  Keep raw snapshots here and
    ' format them only when the operator dumps the recorder or an exception is
    ' actually reported.
    Private ReadOnly _diagnosticFlightRecorder(63) As DiagnosticInstructionSampleInBed
    Private _diagnosticFlightRecorderNext As Integer
    Private _diagnosticFlightRecorderCount As Integer

    Public Property DiagnosticInstructionContext As String
        Get
            Return CurrentDiagnosticInstructionTextInBed()
        End Get
        Set(value As String)
            ' Retained for source compatibility with diagnostic callers.  CPU
            ' execution uses RecordDiagnosticInstruction so it never parses or
            ' allocates a textual instruction context on the hot path.
            _diagnosticInstructionValid = False
        End Set
    End Property

    Public Sub RecordDiagnosticInstruction(codeSegmentInBed As UShort,
                                           instructionOffsetInBed As UShort,
                                           opcodeInBed As Byte,
                                           modRmInBed As Byte)
        _diagnosticInstructionValid = True
        _diagnosticInstructionCs = codeSegmentInBed
        _diagnosticInstructionIp = instructionOffsetInBed
        _diagnosticInstructionOpcode = opcodeInBed
        _diagnosticInstructionModRm = modRmInBed

        Dim sampleInBed As DiagnosticInstructionSampleInBed
        sampleInBed.Valid = True
        sampleInBed.CodeSegment = codeSegmentInBed
        sampleInBed.InstructionOffset = instructionOffsetInBed
        sampleInBed.Opcode = opcodeInBed
        sampleInBed.ModRm = modRmInBed
        sampleInBed.ControlWord = _controlWord
        sampleInBed.StatusWord = _statusWord
        sampleInBed.Top = CByte(_top And 7)
        sampleInBed.TagWord = TagWord
        _diagnosticFlightRecorder(_diagnosticFlightRecorderNext) = sampleInBed
        _diagnosticFlightRecorderNext =
            (_diagnosticFlightRecorderNext + 1) Mod _diagnosticFlightRecorder.Length
        If _diagnosticFlightRecorderCount < _diagnosticFlightRecorder.Length Then
            _diagnosticFlightRecorderCount += 1
        End If
    End Sub

    Public Sub ExecuteReservedEncoding()
        ' Intel 80286/80287 Programmer's Reference Manual, Appendix A:
        ' encodings labelled reserved are not 80387 instructions on this NPX.
        ' Present them to the physical 80287 exception machinery as an illegal
        ' numeric operation; do not silently execute a later-generation opcode.
        SignalException(Ie)
    End Sub

    Public Sub New()
        HardwareReset()
    End Sub

    Public ReadOnly Property Identity As String
        Get
            Return "Intel 80287-compatible numeric coprocessor"
        End Get
    End Property

    Public Property ControlWord As UShort
        Get
            Return _controlWord
        End Get
        Set(value As UShort)
            _controlWord = value
            UpdateExceptionSummary()
        End Set
    End Property

    Public Property StatusWord As UShort
        Get
            Return _statusWord
        End Get
        Set(value As UShort)
            _statusWord = value
            _top = CInt((_statusWord And TopMask) >> 11)
            UpdateExceptionSummary()
        End Set
    End Property

    Public ReadOnly Property TagWord As UShort
        Get
            Dim resultInBed As UInteger = 0UI
            For physicalInBed As Integer = 0 To 7
                resultInBed = resultInBed Or
                    (CUInt(_tags(physicalInBed) And 3) << (physicalInBed * 2))
            Next
            Return CUShort(resultInBed And &HFFFFUI)
        End Get
    End Property

    Public ReadOnly Property Top As Integer
        Get
            Return _top
        End Get
    End Property

    Public Sub LoadEnvironmentState(controlInBed As UShort,
                                    statusInBed As UShort,
                                    tagWordInBed As UShort)
        _controlWord = controlInBed
        _statusWord = statusInBed
        _top = CInt((_statusWord And TopMask) >> 11)
        For physicalInBed As Integer = 0 To 7
            _tags(physicalInBed) = CByte((tagWordInBed >> (physicalInBed * 2)) And 3)
        Next
        UpdateExceptionSummary()
    End Sub

    Public Sub MaskAllExceptions()
        _controlWord = CUShort(_controlWord Or ExceptionMask)
        UpdateExceptionSummary()
    End Sub

    Public Sub GetLogicalRegisterImage(logicalIndexInBed As Integer,
                                       ByRef valueInBed As Double,
                                       ByRef tagInBed As Byte)
        Dim physicalInBed As Integer = PhysicalIndex(logicalIndexInBed)
        valueInBed = _registers(physicalInBed)
        tagInBed = _tags(physicalInBed)
    End Sub

    Public Sub SetLogicalRegisterImage(logicalIndexInBed As Integer,
                                       valueInBed As Double,
                                       tagInBed As Byte)
        Dim physicalInBed As Integer = PhysicalIndex(logicalIndexInBed)
        _registers(physicalInBed) = valueInBed
        _tags(physicalInBed) = CByte(tagInBed And 3)
    End Sub

    Public ReadOnly Property ErrorAsserted As Boolean
        Get
            Return _errorAsserted
        End Get
    End Property

    Public ReadOnly Property Busy As Boolean
        Get
            ' Execution is synchronous in this generation of the emulator.
            Return False
        End Get
    End Property

    Public ReadOnly Property ProtectedMode As Boolean
        Get
            Return _protectedMode
        End Get
    End Property

    Public Sub SetProtectedMode()
        ' FSETPM is one-way until the physical 80287 RESET input is asserted.
        _protectedMode = True
    End Sub

    Public Sub HardwareReset()
        _protectedMode = False
        Reset()
    End Sub

    Public Sub Reset()
        ' FINIT/FNINIT and FSAVE initialize the numeric environment but do not
        ' return an 80287 from protected mode to real-address mode.
        _controlWord = &H37FUS
        _statusWord = 0US
        _top = 0
        _errorAsserted = False
        _diagnosticInstructionValid = False
        _diagnosticFirstStackFault = "<none>"
        _diagnosticLoggedPendingFlags = 0US
        _diagnosticFlightRecorderNext = 0
        _diagnosticFlightRecorderCount = 0
        Array.Clear(_diagnosticFlightRecorder, 0, _diagnosticFlightRecorder.Length)
        Array.Clear(_registers, 0, _registers.Length)
        For indexInBed As Integer = 0 To 7
            _tags(indexInBed) = TagEmpty
        Next
        UpdateTopBits()
    End Sub

    Public Sub ClearExceptions()
        _statusWord = CUShort(_statusWord And Not &HFFUS)
        _diagnosticLoggedPendingFlags = 0US
        UpdateExceptionSummary()
    End Sub

    Public Function IsEmpty(logicalIndexInBed As Integer) As Boolean
        Return _tags(PhysicalIndex(logicalIndexInBed)) = TagEmpty
    End Function

    Public Function Push(valueInBed As Double) As Boolean
        Dim newTopInBed As Integer = (_top - 1) And 7
        If _tags(newTopInBed) <> TagEmpty Then
            Dim maskedInBed As Boolean = SignalStackFault(overflowInBed:=True)
            If Not maskedInBed Then Return False

            _top = newTopInBed
            _registers(_top) = Double.NaN
            _tags(_top) = TagSpecial
            UpdateTopBits()
            Return True
        End If

        _top = newTopInBed
        _registers(_top) = valueInBed
        _tags(_top) = ClassifyTag(valueInBed)
        UpdateTopBits()
        Return True
    End Function

    Public Function PushSt(logicalIndexInBed As Integer) As Boolean
        Dim valueInBed As Double
        If Not TryReadSt(logicalIndexInBed, valueInBed) Then Return False
        Return Push(valueInBed)
    End Function

    Public Function Pop() As Boolean
        If _tags(_top) = TagEmpty Then
            Dim maskedInBed As Boolean = SignalStackFault(overflowInBed:=False)
            If Not maskedInBed Then Return False
        Else
            _tags(_top) = TagEmpty
        End If

        _top = (_top + 1) And 7
        UpdateTopBits()
        Return True
    End Function

    Public Function TryReadSt(logicalIndexInBed As Integer,
                              ByRef valueInBed As Double) As Boolean
        Dim physicalInBed As Integer = PhysicalIndex(logicalIndexInBed)
        If _tags(physicalInBed) = TagEmpty Then
            Dim maskedInBed As Boolean = SignalStackFault(overflowInBed:=False)
            If Not maskedInBed Then
                valueInBed = 0.0
                Return False
            End If
            valueInBed = Double.NaN
            Return True
        End If

        valueInBed = _registers(physicalInBed)
        Return True
    End Function

    Public Function TryReadSt0(ByRef valueInBed As Double) As Boolean
        Return TryReadSt(0, valueInBed)
    End Function

    Public Sub SetSt(logicalIndexInBed As Integer, valueInBed As Double)
        Dim physicalInBed As Integer = PhysicalIndex(logicalIndexInBed)
        _registers(physicalInBed) = valueInBed
        _tags(physicalInBed) = ClassifyTag(valueInBed)
    End Sub

    Public Sub SetSt0(valueInBed As Double)
        SetSt(0, valueInBed)
    End Sub

    Public Function CopySt0To(logicalIndexInBed As Integer) As Boolean
        Dim valueInBed As Double
        If Not TryReadSt0(valueInBed) Then Return False
        SetSt(logicalIndexInBed, valueInBed)
        Return True
    End Function

    Public Function Exchange(logicalIndexInBed As Integer) As Boolean
        Dim st0InBed As Double
        Dim stiInBed As Double
        If Not TryReadSt0(st0InBed) Then Return False
        If Not TryReadSt(logicalIndexInBed, stiInBed) Then Return False
        SetSt0(stiInBed)
        SetSt(logicalIndexInBed, st0InBed)
        Return True
    End Function

    Public Sub Free(logicalIndexInBed As Integer)
        _tags(PhysicalIndex(logicalIndexInBed)) = TagEmpty
    End Sub

    Public Sub DecrementTop()
        _top = (_top - 1) And 7
        UpdateTopBits()
    End Sub

    Public Sub IncrementTop()
        _top = (_top + 1) And 7
        UpdateTopBits()
    End Sub

    Public Function ExecuteSt0Arithmetic(operationInBed As Integer,
                                         rightInBed As Double) As Boolean
        If operationInBed = 2 OrElse operationInBed = 3 Then
            Dim comparedInBed As Boolean = CompareSt0With(rightInBed)
            If comparedInBed AndAlso operationInBed = 3 Then Pop()
            Return comparedInBed
        End If

        Dim leftInBed As Double
        If Not TryReadSt0(leftInBed) Then Return False

        Dim resultInBed As Double
        Select Case operationInBed
            Case 0
                If Not TryBinary(leftInBed, rightInBed, "+"c, resultInBed) Then Return False
            Case 1
                If Not TryBinary(leftInBed, rightInBed, "*"c, resultInBed) Then Return False
            Case 4
                If Not TryBinary(leftInBed, rightInBed, "-"c, resultInBed) Then Return False
            Case 5
                If Not TryBinary(rightInBed, leftInBed, "-"c, resultInBed) Then Return False
            Case 6
                If Not TryBinary(leftInBed, rightInBed, "/"c, resultInBed) Then Return False
            Case 7
                If Not TryBinary(rightInBed, leftInBed, "/"c, resultInBed) Then Return False
            Case Else
                Return False
        End Select

        SetSt0(resultInBed)
        Return True
    End Function

    Public Function ExecuteSt0ArithmeticWithSt(operationInBed As Integer,
                                               logicalIndexInBed As Integer) As Boolean
        Dim rightInBed As Double
        If Not TryReadSt(logicalIndexInBed, rightInBed) Then Return False
        Return ExecuteSt0Arithmetic(operationInBed, rightInBed)
    End Function

    Public Function ExecuteStiArithmeticWithSt0(operationInBed As Integer,
                                                logicalIndexInBed As Integer,
                                                popAfterInBed As Boolean) As Boolean
        Dim stiInBed As Double
        Dim st0InBed As Double
        If Not TryReadSt(logicalIndexInBed, stiInBed) Then Return False
        If Not TryReadSt0(st0InBed) Then Return False

        Dim resultInBed As Double
        Select Case operationInBed
            Case 0
                If Not TryBinary(stiInBed, st0InBed, "+"c, resultInBed) Then Return False
            Case 1
                If Not TryBinary(stiInBed, st0InBed, "*"c, resultInBed) Then Return False
            Case 4
                ' DC/DE E0+i = FSUBR[P] ST(i),ST(0)
                If Not TryBinary(st0InBed, stiInBed, "-"c, resultInBed) Then Return False
            Case 5
                ' DC/DE E8+i = FSUB[P] ST(i),ST(0)
                If Not TryBinary(stiInBed, st0InBed, "-"c, resultInBed) Then Return False
            Case 6
                ' DC/DE F0+i = FDIVR[P] ST(i),ST(0)
                If Not TryBinary(st0InBed, stiInBed, "/"c, resultInBed) Then Return False
            Case 7
                ' DC/DE F8+i = FDIV[P] ST(i),ST(0)
                If Not TryBinary(stiInBed, st0InBed, "/"c, resultInBed) Then Return False
            Case Else
                Return False
        End Select

        SetSt(logicalIndexInBed, resultInBed)
        If popAfterInBed Then Return Pop()
        Return True
    End Function

    Public Function CompareSt0With(valueInBed As Double) As Boolean
        ClearConditionCodes()

        Dim currentInBed As Double
        If Not TryReadSt0(currentInBed) Then Return False

        If Double.IsNaN(currentInBed) OrElse Double.IsNaN(valueInBed) Then
            Dim maskedInBed As Boolean = SignalException(Ie)
            If maskedInBed Then SetUnorderedCondition()
            Return maskedInBed
        End If

        If currentInBed < valueInBed Then
            _statusWord = CUShort(_statusWord Or C0)
        ElseIf currentInBed = valueInBed Then
            _statusWord = CUShort(_statusWord Or C3)
        End If
        Return True
    End Function

    Public Function CompareSt0WithSt(logicalIndexInBed As Integer,
                                     popCountInBed As Integer) As Boolean
        Dim valueInBed As Double
        If Not TryReadSt(logicalIndexInBed, valueInBed) Then Return False
        If Not CompareSt0With(valueInBed) Then Return False

        For countInBed As Integer = 1 To popCountInBed
            If Not Pop() Then Return False
        Next
        Return True
    End Function

    Public Function ChangeSign() As Boolean
        Dim valueInBed As Double
        If Not TryReadSt0(valueInBed) Then Return False
        SetSt0(-valueInBed)
        Return True
    End Function

    Public Function AbsoluteValue() As Boolean
        Dim valueInBed As Double
        If Not TryReadSt0(valueInBed) Then Return False
        SetSt0(Math.Abs(valueInBed))
        Return True
    End Function

    Public Function TestSt0() As Boolean
        Return CompareSt0With(0.0)
    End Function

    Public Function SquareRoot() As Boolean
        Dim valueInBed As Double
        If Not TryReadSt0(valueInBed) Then Return False
        If valueInBed < 0.0 OrElse Double.IsNaN(valueInBed) Then
            Dim maskedInBed As Boolean = SignalException(Ie)
            If maskedInBed Then SetSt0(Double.NaN)
            Return maskedInBed
        End If
        SetSt0(Math.Sqrt(valueInBed))
        Return True
    End Function

    Public Function RoundSt0ToIntegral() As Boolean
        Dim valueInBed As Double
        If Not TryReadSt0(valueInBed) Then Return False
        If Double.IsNaN(valueInBed) OrElse Double.IsInfinity(valueInBed) Then
            Dim maskedInBed As Boolean = SignalException(Ie)
            If maskedInBed Then SetSt0(Double.NaN)
            Return maskedInBed
        End If

        Dim roundedInBed As Double = RoundAccordingToControl(valueInBed)
        If roundedInBed <> valueInBed Then
            If Not SignalException(Pe) Then Return False
        End If
        SetSt0(roundedInBed)
        Return True
    End Function

    Public Function F2xm1() As Boolean
        Dim valueInBed As Double
        If Not TryReadSt0(valueInBed) Then Return False
        SetSt0(Math.Pow(2.0, valueInBed) - 1.0)
        Return True
    End Function

    Public Function Fptan() As Boolean
        Dim valueInBed As Double
        If Not TryReadSt0(valueInBed) Then Return False
        SetSt0(Math.Tan(valueInBed))
        Return Push(1.0)
    End Function

    Public Function Fpatan() As Boolean
        Dim xInBed As Double
        Dim yInBed As Double
        If Not TryReadSt0(xInBed) Then Return False
        If Not TryReadSt(1, yInBed) Then Return False
        SetSt(1, Math.Atan2(yInBed, xInBed))
        Return Pop()
    End Function

    Public Function Fyl2x(addOneInBed As Boolean) As Boolean
        Dim xInBed As Double
        Dim yInBed As Double
        If Not TryReadSt0(xInBed) Then Return False
        If Not TryReadSt(1, yInBed) Then Return False
        If addOneInBed Then xInBed += 1.0

        If xInBed <= 0.0 OrElse Double.IsNaN(xInBed) Then
            Dim maskedInBed As Boolean = SignalException(Ie)
            If Not maskedInBed Then Return False
            SetSt(1, Double.NaN)
            Return Pop()
        End If

        SetSt(1, yInBed * Math.Log(xInBed, 2.0))
        Return Pop()
    End Function

    Public Function Extract() As Boolean
        Dim valueInBed As Double
        If Not TryReadSt0(valueInBed) Then Return False

        If valueInBed = 0.0 Then
            If Not SignalException(Ze) Then Return False
            SetSt0(Double.NegativeInfinity)
            Return Push(valueInBed)
        End If

        If Double.IsNaN(valueInBed) Then
            Dim maskedInBed As Boolean = SignalException(Ie)
            If Not maskedInBed Then Return False
            SetSt0(Double.NaN)
            Return Push(Double.NaN)
        End If

        Dim exponentInBed As Double =
            Math.Floor(Math.Log(Math.Abs(valueInBed), 2.0))
        Dim significandInBed As Double =
            valueInBed / Math.Pow(2.0, exponentInBed)

        SetSt0(exponentInBed)
        Return Push(significandInBed)
    End Function

    Public Function Prem() As Boolean
        Dim dividendInBed As Double
        Dim divisorInBed As Double
        If Not TryReadSt0(dividendInBed) Then Return False
        If Not TryReadSt(1, divisorInBed) Then Return False

        If divisorInBed = 0.0 OrElse
           Double.IsNaN(dividendInBed) OrElse
           Double.IsNaN(divisorInBed) Then
            Dim maskedInBed As Boolean = SignalException(Ie)
            If maskedInBed Then SetSt0(Double.NaN)
            Return maskedInBed
        End If

        Dim quotientInBed As Double = Math.Truncate(dividendInBed / divisorInBed)
        SetSt0(dividendInBed - (quotientInBed * divisorInBed))

        ' This implementation completes the reduction in one host operation.
        ' Therefore C2=0 (reduction complete). Quotient low bits are exposed in
        ' C0/C3/C1 as documented for a completed FPREM.
        ClearConditionCodes()
        Dim quotientBitsInBed As Integer = CInt(Math.Abs(quotientInBed)) And 7
        If (quotientBitsInBed And 4) <> 0 Then _statusWord = CUShort(_statusWord Or C0)
        If (quotientBitsInBed And 2) <> 0 Then _statusWord = CUShort(_statusWord Or C3)
        If (quotientBitsInBed And 1) <> 0 Then _statusWord = CUShort(_statusWord Or C1)
        Return True
    End Function

    Public Function Scale() As Boolean
        Dim valueInBed As Double
        Dim scaleInBed As Double
        If Not TryReadSt0(valueInBed) Then Return False
        If Not TryReadSt(1, scaleInBed) Then Return False
        Dim exponentInBed As Double = Math.Truncate(scaleInBed)
        SetSt0(valueInBed * Math.Pow(2.0, exponentInBed))
        Return True
    End Function

    Public Function TryConvertSt0ToInt16(ByRef resultInBed As Short) As Boolean
        Dim valueInBed As Double
        If Not TryReadSt0(valueInBed) Then Return False

        Dim roundedInBed As Double
        If Not TryPrepareIntegerConversion(valueInBed,
                                           Short.MinValue,
                                           Short.MaxValue,
                                           roundedInBed) Then
            resultInBed = 0S
            Return False
        End If

        If Double.IsNaN(roundedInBed) Then
            resultInBed = Short.MinValue
        Else
            resultInBed = CShort(roundedInBed)
        End If
        Return True
    End Function

    Public Function TryConvertSt0ToInt32(ByRef resultInBed As Integer) As Boolean
        Dim valueInBed As Double
        If Not TryReadSt0(valueInBed) Then Return False

        Dim roundedInBed As Double
        If Not TryPrepareIntegerConversion(valueInBed,
                                           Integer.MinValue,
                                           Integer.MaxValue,
                                           roundedInBed) Then
            resultInBed = 0
            Return False
        End If

        If Double.IsNaN(roundedInBed) Then
            resultInBed = Integer.MinValue
        Else
            resultInBed = CInt(roundedInBed)
        End If
        Return True
    End Function

    Public Function TryConvertSt0ToInt64(ByRef resultInBed As Long) As Boolean
        Dim valueInBed As Double
        If Not TryReadSt0(valueInBed) Then Return False

        Dim roundedInBed As Double
        If Not TryPrepareIntegerConversion(valueInBed,
                                           -9223372036854775808.0,
                                           9223372036854774784.0,
                                           roundedInBed) Then
            Return False
        End If
        If Double.IsNaN(roundedInBed) Then
            resultInBed = Long.MinValue
        Else
            resultInBed = CLng(roundedInBed)
        End If
        Return True
    End Function

    Public Function TryConvertSt0ToSingle(ByRef resultInBed As Single) As Boolean
        Dim valueInBed As Double
        If Not TryReadSt0(valueInBed) Then Return False

        If Double.IsNaN(valueInBed) Then
            resultInBed = Single.NaN
            Return True
        End If
        If Double.IsPositiveInfinity(valueInBed) Then
            resultInBed = Single.PositiveInfinity
            Return True
        End If
        If Double.IsNegativeInfinity(valueInBed) Then
            resultInBed = Single.NegativeInfinity
            Return True
        End If

        If valueInBed > Single.MaxValue OrElse valueInBed < -Single.MaxValue Then
            Dim maskedInBed As Boolean = SignalException(Oe)
            If Not maskedInBed Then
                resultInBed = 0.0F
                Return False
            End If
            resultInBed = If(valueInBed < 0.0,
                             Single.NegativeInfinity,
                             Single.PositiveInfinity)
            Return True
        End If

        resultInBed = CSng(valueInBed)
        If CDbl(resultInBed) <> valueInBed AndAlso Not SignalException(Pe) Then
            resultInBed = 0.0F
            Return False
        End If
        Return True
    End Function

    Public Sub LoadConstant(registerIndexInBed As Integer)
        Select Case registerIndexInBed
            Case 0
                Push(1.0)
            Case 1
                Push(Math.Log(10.0, 2.0))
            Case 2
                Push(Math.Log(Math.E, 2.0))
            Case 3
                Push(Math.PI)
            Case 4
                Push(Math.Log(2.0, 10.0))
            Case 5
                Push(Math.Log(2.0))
            Case 6
                Push(0.0)
        End Select
    End Sub

    Public Function Examine() As Boolean
        ClearConditionCodes()
        Dim physicalInBed As Integer = _top
        Dim tagInBed As Byte = _tags(physicalInBed)

        If tagInBed = TagEmpty Then
            ' Empty: C3,C0 = 1, C2 = 0.
            _statusWord = CUShort(_statusWord Or C3 Or C0)
            Return True
        End If

        Dim valueInBed As Double = _registers(physicalInBed)
        If BitConverter.DoubleToInt64Bits(valueInBed) < 0 Then
            _statusWord = CUShort(_statusWord Or C1)
        End If

        If Double.IsNaN(valueInBed) Then
            _statusWord = CUShort(_statusWord Or C0)
        ElseIf Double.IsInfinity(valueInBed) Then
            _statusWord = CUShort(_statusWord Or C2 Or C0)
        ElseIf valueInBed = 0.0 Then
            _statusWord = CUShort(_statusWord Or C3)
        ElseIf IsHostDenormal(valueInBed) Then
            _statusWord = CUShort(_statusWord Or C3 Or C2)
        Else
            _statusWord = CUShort(_statusWord Or C2)
        End If
        Return True
    End Function

    Public Function DiagnosticText() As String
        Return "80287 NPX                  : " & Identity & Environment.NewLine &
               "  control/status/tag       : " &
               _controlWord.ToString("X4") & "h / " &
               _statusWord.ToString("X4") & "h / " &
               TagWord.ToString("X4") & "h" & Environment.NewLine &
               "  TOP / ERROR / BUSY       : " &
               _top.ToString() & " / " &
               If(_errorAsserted, "ASSERTED", "clear") & " / " &
               If(Busy, "busy", "idle") & Environment.NewLine &
               "  first stack fault        : " & _diagnosticFirstStackFault
    End Function

    Public Function DiagnosticFlightRecorderText() As String
        Dim reportInBed As New Text.StringBuilder()
        reportInBed.AppendLine("80287 flight recorder (oldest first)")
        reportInBed.AppendLine(DiagnosticText())
        reportInBed.AppendLine()
        If _diagnosticFlightRecorderCount = 0 Then
            reportInBed.AppendLine("(no 80287 instructions recorded)")
        Else
            AppendDiagnosticFlightRecorderInBed(reportInBed)
        End If
        Return reportInBed.ToString()
    End Function

    Private Function CurrentDiagnosticInstructionTextInBed() As String
        If Not _diagnosticInstructionValid Then Return "<none>"
        Return _diagnosticInstructionCs.ToString("X4") & ":" &
               _diagnosticInstructionIp.ToString("X4") & " " &
               _diagnosticInstructionOpcode.ToString("X2") & " " &
               _diagnosticInstructionModRm.ToString("X2")
    End Function

    Private Sub AppendDiagnosticFlightRecorderInBed(targetInBed As Text.StringBuilder)
        Dim firstInBed As Integer =
            (_diagnosticFlightRecorderNext - _diagnosticFlightRecorderCount +
             _diagnosticFlightRecorder.Length) Mod _diagnosticFlightRecorder.Length
        For offsetInBed As Integer = 0 To _diagnosticFlightRecorderCount - 1
            Dim sampleInBed As DiagnosticInstructionSampleInBed =
                _diagnosticFlightRecorder((firstInBed + offsetInBed) Mod
                                          _diagnosticFlightRecorder.Length)
            If Not sampleInBed.Valid Then Continue For
            targetInBed.Append(sampleInBed.CodeSegment.ToString("X4"))
            targetInBed.Append(":"c)
            targetInBed.Append(sampleInBed.InstructionOffset.ToString("X4"))
            targetInBed.Append(" ")
            targetInBed.Append(sampleInBed.Opcode.ToString("X2"))
            targetInBed.Append(" ")
            targetInBed.Append(sampleInBed.ModRm.ToString("X2"))
            targetInBed.Append(" control=")
            targetInBed.Append(sampleInBed.ControlWord.ToString("X4"))
            targetInBed.Append("h status=")
            targetInBed.Append(sampleInBed.StatusWord.ToString("X4"))
            targetInBed.Append("h TOP=")
            targetInBed.Append(sampleInBed.Top.ToString())
            targetInBed.Append(" tag=")
            targetInBed.Append(sampleInBed.TagWord.ToString("X4"))
            targetInBed.AppendLine("h")
        Next
    End Sub

    Private Function TryPrepareIntegerConversion(valueInBed As Double,
                                                 minimumInBed As Double,
                                                 maximumInBed As Double,
                                                 ByRef roundedInBed As Double) As Boolean
        If Double.IsNaN(valueInBed) OrElse Double.IsInfinity(valueInBed) Then
            If Not SignalException(Ie) Then
                roundedInBed = 0.0
                Return False
            End If
            roundedInBed = Double.NaN
            Return True
        End If

        roundedInBed = RoundAccordingToControl(valueInBed)
        If roundedInBed < minimumInBed OrElse roundedInBed > maximumInBed Then
            If Not SignalException(Ie) Then
                roundedInBed = 0.0
                Return False
            End If
            ' NaN is an internal marker meaning "integer indefinite".  The
            ' destination-width method converts it to 8000h / 80000000h.
            roundedInBed = Double.NaN
            Return True
        End If

        If roundedInBed <> valueInBed Then
            If Not SignalException(Pe) Then Return False
        End If
        Return True
    End Function

    Private Function RoundAccordingToControl(valueInBed As Double) As Double
        Select Case _controlWord And RcMask
            Case &H0US
                Return Math.Round(valueInBed, MidpointRounding.ToEven)
            Case &H400US
                Return Math.Floor(valueInBed)
            Case &H800US
                Return Math.Ceiling(valueInBed)
            Case Else
                Return Math.Truncate(valueInBed)
        End Select
    End Function

    Private Function TryBinary(leftInBed As Double,
                               rightInBed As Double,
                               operationInBed As Char,
                               ByRef resultInBed As Double) As Boolean
        If Double.IsNaN(leftInBed) OrElse Double.IsNaN(rightInBed) Then
            Dim maskedInBed As Boolean = SignalException(Ie)
            resultInBed = Double.NaN
            Return maskedInBed
        End If

        If operationInBed = "/"c AndAlso rightInBed = 0.0 Then
            If leftInBed = 0.0 OrElse Double.IsInfinity(leftInBed) Then
                Dim maskedInvalidInBed As Boolean = SignalException(Ie)
                resultInBed = Double.NaN
                Return maskedInvalidInBed
            End If

            Dim maskedZeroInBed As Boolean = SignalException(Ze)
            If Not maskedZeroInBed Then
                resultInBed = 0.0
                Return False
            End If

            Dim negativeInBed As Boolean =
                (BitConverter.DoubleToInt64Bits(leftInBed) < 0) Xor
                (BitConverter.DoubleToInt64Bits(rightInBed) < 0)
            resultInBed = If(negativeInBed,
                             Double.NegativeInfinity,
                             Double.PositiveInfinity)
            Return True
        End If

        Select Case operationInBed
            Case "+"c
                resultInBed = leftInBed + rightInBed
            Case "-"c
                resultInBed = leftInBed - rightInBed
            Case "*"c
                resultInBed = leftInBed * rightInBed
            Case "/"c
                resultInBed = leftInBed / rightInBed
            Case Else
                resultInBed = 0.0
                Return False
        End Select

        If Double.IsNaN(resultInBed) Then
            Dim maskedInBed As Boolean = SignalException(Ie)
            Return maskedInBed
        End If

        If Double.IsInfinity(resultInBed) AndAlso
           Not Double.IsInfinity(leftInBed) AndAlso
           Not Double.IsInfinity(rightInBed) Then
            Return SignalException(Oe)
        End If

        Return True
    End Function

    Private Function SignalStackFault(overflowInBed As Boolean) As Boolean
        If _diagnosticFirstStackFault = "<none>" Then
            _diagnosticFirstStackFault =
                If(overflowInBed, "overflow at ", "underflow at ") &
                CurrentDiagnosticInstructionTextInBed() &
                " TOP=" & _top.ToString() &
                " tag=" & TagWord.ToString("X4") & "h"
        End If
        _statusWord = CUShort(_statusWord Or Ie Or Sf)
        If overflowInBed Then
            _statusWord = CUShort(_statusWord Or C1)
        Else
            _statusWord = CUShort(_statusWord And Not C1)
        End If
        UpdateExceptionSummary()
        Dim maskedInBed As Boolean = IsMasked(Ie)
        AppendDiagnosticError(If(overflowInBed, "STACK_OVERFLOW", "STACK_UNDERFLOW"),
                              Ie Or Sf,
                              maskedInBed)
        Return maskedInBed
    End Function

    Private Function SignalException(flagInBed As UShort) As Boolean
        _statusWord = CUShort(_statusWord Or flagInBed)
        UpdateExceptionSummary()
        Dim maskedInBed As Boolean = IsMasked(flagInBed)
        If Not maskedInBed Then AppendDiagnosticError("EXCEPTION", flagInBed, maskedInBed)
        Return maskedInBed
    End Function

    Private Sub AppendDiagnosticError(kindInBed As String,
                                      flagsInBed As UShort,
                                      maskedInBed As Boolean)
        Dim newlyLoggedFlagsInBed As UShort =
            CUShort(flagsInBed And ExceptionMask And Not _diagnosticLoggedPendingFlags)
        If newlyLoggedFlagsInBed = 0US Then Return
        _diagnosticLoggedPendingFlags =
            CUShort(_diagnosticLoggedPendingFlags Or newlyLoggedFlagsInBed)

        Try
            System.IO.Directory.CreateDirectory(
                System.IO.Path.GetDirectoryName(_diagnosticErrorLogPath))
            Dim lineInBed As String =
                DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss.fff",
                                      Globalization.CultureInfo.InvariantCulture) &
                " " & kindInBed &
                " at " & CurrentDiagnosticInstructionTextInBed() &
                " flags=" & flagsInBed.ToString("X4") & "h" &
                " masked=" & If(maskedInBed, "yes", "no") &
                " control=" & _controlWord.ToString("X4") & "h" &
                " status=" & _statusWord.ToString("X4") & "h" &
                " TOP=" & _top.ToString(Globalization.CultureInfo.InvariantCulture) &
                " tag=" & TagWord.ToString("X4") & "h" & Environment.NewLine
            Dim flightRecorderInBed As New Text.StringBuilder()
            AppendDiagnosticFlightRecorderInBed(flightRecorderInBed)
            System.IO.File.AppendAllText(
                _diagnosticErrorLogPath,
                lineInBed &
                "--- preceding 80287 instructions (oldest first) ---" & Environment.NewLine &
                flightRecorderInBed.ToString() &
                "--- end 80287 flight recorder ---" & Environment.NewLine,
                New Text.UTF8Encoding(encoderShouldEmitUTF8Identifier:=False))
        Catch
            ' Diagnostics must never disturb guest execution.
        End Try
    End Sub

    Private Function IsMasked(flagInBed As UShort) As Boolean
        Return (_controlWord And flagInBed) <> 0US
    End Function

    Private Sub UpdateExceptionSummary()
        Dim pendingInBed As UShort =
            CUShort((_statusWord And ExceptionMask) And
                    (Not _controlWord And ExceptionMask))
        _errorAsserted = pendingInBed <> 0US
        If _errorAsserted Then
            _statusWord = CUShort(_statusWord Or Es)
        Else
            _statusWord = CUShort(_statusWord And Not Es)
        End If
    End Sub

    Private Sub UpdateTopBits()
        _statusWord = CUShort((_statusWord And Not TopMask) Or
                              CUShort((_top And 7) << 11))
    End Sub

    Private Sub ClearConditionCodes()
        _statusWord = CUShort(_statusWord And Not (C0 Or C1 Or C2 Or C3))
    End Sub

    Private Sub SetUnorderedCondition()
        _statusWord = CUShort(_statusWord Or C0 Or C2 Or C3)
    End Sub

    Private Function PhysicalIndex(logicalIndexInBed As Integer) As Integer
        If logicalIndexInBed < 0 OrElse logicalIndexInBed > 7 Then
            Throw New ArgumentOutOfRangeException(NameOf(logicalIndexInBed))
        End If
        Return (_top + logicalIndexInBed) And 7
    End Function

    Private Shared Function ClassifyTag(valueInBed As Double) As Byte
        If valueInBed = 0.0 Then Return TagZero
        If Double.IsNaN(valueInBed) OrElse
           Double.IsInfinity(valueInBed) OrElse
           IsHostDenormal(valueInBed) Then
            Return TagSpecial
        End If
        Return TagValid
    End Function

    Private Shared Function IsHostDenormal(valueInBed As Double) As Boolean
        If valueInBed = 0.0 OrElse Double.IsNaN(valueInBed) OrElse Double.IsInfinity(valueInBed) Then
            Return False
        End If
        Dim bitsInBed As Long = BitConverter.DoubleToInt64Bits(Math.Abs(valueInBed))
        Return (bitsInBed And &H7FF0000000000000L) = 0L
    End Function
End Class
