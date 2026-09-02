Imports System

Partial Public Class Processor286
    Private Structure TimingContext286
        Public Opcode As Byte
        Public SecondOpcode As Byte
        Public HasSecondOpcode As Boolean
        Public HasModRm As Boolean
        Public ModValue As Integer
        Public RegField As Integer
        Public RmField As Integer
        Public EffectiveAddressThreeTerms As Boolean
        Public PrefixRep As Integer
        Public InitialCx As UInt16
        Public InitialFlags As UInt16
        Public InitialCs As UInt16
        Public InitialIp As UInt16
        Public SequentialCs As UInt16
        Public SequentialIp As UInt16
        Public ShiftCount As Integer
        Public EnterLevel As Integer
        Public OperandIsWord As Boolean
    End Structure

    ' CROMWELL COMPILED 286 TIMING LEDGER BRICK 5C
    ' Zero means "dynamic: use the documented slow timing path".
    ' Low byte = register/no-memory cycles. High byte = memory base cycles;
    ' the existing one-T effective-address complexity adjustment is added when
    ' the decoded instruction actually addresses memory.
    Private Shared ReadOnly _compiledTimingLedgerInBed As Integer() = BuildCompiledTimingLedgerInBed()

    Private Shared Function PackTimingInBed(registerCyclesInBed As Integer,
                                            memoryBaseCyclesInBed As Integer) As Integer
        Return (registerCyclesInBed And &HFF) Or ((memoryBaseCyclesInBed And &HFF) << 8)
    End Function

    Private Shared Sub SetFixedTimingInBed(tableInBed As Integer(),
                                           opcodeInBed As Integer,
                                           cyclesInBed As Integer)
        tableInBed(opcodeInBed And &HFF) = PackTimingInBed(cyclesInBed, cyclesInBed)
    End Sub

    Private Shared Sub SetRmTimingInBed(tableInBed As Integer(),
                                        opcodeInBed As Integer,
                                        registerCyclesInBed As Integer,
                                        memoryBaseCyclesInBed As Integer)
        tableInBed(opcodeInBed And &HFF) =
            PackTimingInBed(registerCyclesInBed, memoryBaseCyclesInBed)
    End Sub

    Private Shared Function BuildCompiledTimingLedgerInBed() As Integer()
        Dim tableInBed(255) As Integer

        ' ALU r/m forms.  Register forms are uniformly 2 T-states.
        For familyBaseInBed As Integer = &H0 To &H38 Step &H8
            For offsetInBed As Integer = 0 To 3
                Dim opcodeInBed As Integer = familyBaseInBed + offsetInBed
                Dim memoryBaseInBed As Integer = 7
                ' CMP r, r/m (3Ah/3Bh) has the documented 6+EA memory form.
                If opcodeInBed >= &H38 AndAlso (opcodeInBed And 2) <> 0 Then
                    memoryBaseInBed = 6
                End If
                SetRmTimingInBed(tableInBed, opcodeInBed, 2, memoryBaseInBed)
            Next
        Next

        ' Immediate accumulator ALU forms.
        For familyBaseInBed As Integer = &H4 To &H3C Step &H8
            SetFixedTimingInBed(tableInBed, familyBaseInBed, 3)
            SetFixedTimingInBed(tableInBed, familyBaseInBed + 1, 3)
        Next

        For Each opcodeInBed As Integer In New Integer() {&H6, &HE, &H16, &H1E}
            SetFixedTimingInBed(tableInBed, opcodeInBed, 3)
        Next
        For Each opcodeInBed As Integer In New Integer() {&H27, &H2F, &H37, &H3F}
            SetFixedTimingInBed(tableInBed, opcodeInBed, 3)
        Next

        For opcodeInBed As Integer = &H40 To &H4F
            SetFixedTimingInBed(tableInBed, opcodeInBed, 2)
        Next
        For opcodeInBed As Integer = &H50 To &H57
            SetFixedTimingInBed(tableInBed, opcodeInBed, 3)
        Next
        For opcodeInBed As Integer = &H58 To &H5F
            SetFixedTimingInBed(tableInBed, opcodeInBed, 5)
        Next

        SetFixedTimingInBed(tableInBed, &H60, 17)
        SetFixedTimingInBed(tableInBed, &H61, 19)
        SetFixedTimingInBed(tableInBed, &H68, 3)
        SetFixedTimingInBed(tableInBed, &H6A, 3)
        SetRmTimingInBed(tableInBed, &H69, 21, 24)
        SetRmTimingInBed(tableInBed, &H6B, 21, 24)

        SetRmTimingInBed(tableInBed, &H84, 2, 6)
        SetRmTimingInBed(tableInBed, &H85, 2, 6)
        SetRmTimingInBed(tableInBed, &H86, 3, 5)
        SetRmTimingInBed(tableInBed, &H87, 3, 5)
        SetRmTimingInBed(tableInBed, &H88, 2, 3)
        SetRmTimingInBed(tableInBed, &H89, 2, 3)
        SetRmTimingInBed(tableInBed, &H8A, 2, 5)
        SetRmTimingInBed(tableInBed, &H8B, 2, 5)
        SetRmTimingInBed(tableInBed, &H8C, 2, 3)

        For opcodeInBed As Integer = &H90 To &H97
            SetFixedTimingInBed(tableInBed, opcodeInBed, 3)
        Next
        SetFixedTimingInBed(tableInBed, &H98, 2)
        SetFixedTimingInBed(tableInBed, &H99, 2)
        SetFixedTimingInBed(tableInBed, &H9B, 3)
        SetFixedTimingInBed(tableInBed, &H9C, 3)
        SetFixedTimingInBed(tableInBed, &H9D, 5)
        SetFixedTimingInBed(tableInBed, &H9E, 2)
        SetFixedTimingInBed(tableInBed, &H9F, 2)

        SetFixedTimingInBed(tableInBed, &HA0, 5)
        SetFixedTimingInBed(tableInBed, &HA1, 5)
        SetFixedTimingInBed(tableInBed, &HA2, 3)
        SetFixedTimingInBed(tableInBed, &HA3, 3)
        SetFixedTimingInBed(tableInBed, &HA8, 3)
        SetFixedTimingInBed(tableInBed, &HA9, 3)
        For opcodeInBed As Integer = &HB0 To &HBF
            SetFixedTimingInBed(tableInBed, opcodeInBed, 2)
        Next

        SetRmTimingInBed(tableInBed, &HC6, 2, 3)
        SetRmTimingInBed(tableInBed, &HC7, 2, 3)
        SetFixedTimingInBed(tableInBed, &HC9, 5)

        SetRmTimingInBed(tableInBed, &HD0, 2, 7)
        SetRmTimingInBed(tableInBed, &HD1, 2, 7)
        SetFixedTimingInBed(tableInBed, &HD4, 16)
        SetFixedTimingInBed(tableInBed, &HD5, 14)
        SetFixedTimingInBed(tableInBed, &HD7, 5)
        For opcodeInBed As Integer = &HD8 To &HDF
            SetRmTimingInBed(tableInBed, opcodeInBed, 9, 12)
        Next

        For Each opcodeInBed As Integer In New Integer() {&HE4, &HE5, &HEC, &HED}
            SetFixedTimingInBed(tableInBed, opcodeInBed, 5)
        Next
        For Each opcodeInBed As Integer In New Integer() {&HE6, &HE7, &HEE, &HEF}
            SetFixedTimingInBed(tableInBed, opcodeInBed, 3)
        Next

        SetFixedTimingInBed(tableInBed, &HF4, 2)
        For Each opcodeInBed As Integer In New Integer() {&HF5, &HF8, &HF9, &HFC, &HFD}
            SetFixedTimingInBed(tableInBed, opcodeInBed, 2)
        Next
        SetFixedTimingInBed(tableInBed, &HFA, 3)
        SetFixedTimingInBed(tableInBed, &HFB, 2)

        Return tableInBed
    End Function

    ' CROMWELL FUSED TIMING DECODE BRICK 5A
    ' The execution decoder owns these fields while one guest instruction is active.
    ' This is timing metadata only; it is not guest-visible processor state.
    Private _fusedTimingContextInBed As TimingContext286
    Private _fusedTimingActiveInBed As Boolean
    Private _fusedTimingCodeBytesInBed As Integer

    Private _lastInstructionTStates As Integer = 1
    Private _totalTStates As Long

    Public ReadOnly Property LastInstructionTStates As Integer
        Get
            Return _lastInstructionTStates
        End Get
    End Property

    Public ReadOnly Property TotalTStates As Long
        Get
            Return _totalTStates
        End Get
    End Property

    Private Sub ResetTimingState()
        _lastInstructionTStates = 1
        _totalTStates = 0
        _pendingReadyWaitTStatesInBed = 0
        _fusedTimingActiveInBed = False
        _fusedTimingCodeBytesInBed = 0
        _fusedTimingContextInBed = New TimingContext286()
    End Sub

    Private Sub BeginFusedTimingContextInBed()
        _fusedTimingContextInBed = New TimingContext286 With {
            .InitialCs = CS,
            .InitialIp = IP,
            .SequentialCs = CS,
            .InitialCx = CX,
            .InitialFlags = Flags
        }
        _fusedTimingCodeBytesInBed = 0
        _fusedTimingActiveInBed = True
    End Sub

    Private Sub RecordFusedPrimaryOpcodeInBed(opcodeInBed As Byte, prefixRepInBed As Integer)
        If Not _fusedTimingActiveInBed Then Return
        _fusedTimingContextInBed.Opcode = opcodeInBed
        _fusedTimingContextInBed.PrefixRep = prefixRepInBed
        _fusedTimingContextInBed.OperandIsWord = TimingOperandIsWord(opcodeInBed)
    End Sub

    Private Sub RecordFusedSecondOpcodeInBed(opcodeInBed As Byte)
        If Not _fusedTimingActiveInBed Then Return
        _fusedTimingContextInBed.HasSecondOpcode = True
        _fusedTimingContextInBed.SecondOpcode = opcodeInBed
    End Sub

    Private Sub RecordFusedModRmInBed(modValueInBed As Integer,
                                      regFieldInBed As Integer,
                                      rmFieldInBed As Integer)
        If Not _fusedTimingActiveInBed Then Return
        _fusedTimingContextInBed.HasModRm = True
        _fusedTimingContextInBed.ModValue = modValueInBed
        _fusedTimingContextInBed.RegField = regFieldInBed
        _fusedTimingContextInBed.RmField = rmFieldInBed
        _fusedTimingContextInBed.EffectiveAddressThreeTerms =
            modValueInBed <> 3 AndAlso modValueInBed <> 0 AndAlso rmFieldInBed <= 3
    End Sub

    Private Sub RecordFusedShiftCountInBed(countInBed As Integer)
        If _fusedTimingActiveInBed Then _fusedTimingContextInBed.ShiftCount = countInBed And &H1F
    End Sub

    Private Sub RecordFusedEnterLevelInBed(levelInBed As Integer)
        If _fusedTimingActiveInBed Then _fusedTimingContextInBed.EnterLevel = levelInBed And &H1F
    End Sub

    Private Sub RecordFusedCodeBytesInBed(byteCountInBed As Integer)
        If _fusedTimingActiveInBed AndAlso byteCountInBed > 0 Then
            _fusedTimingCodeBytesInBed += byteCountInBed
        End If
    End Sub

    Private Function CompleteFusedTimingContextInBed() As TimingContext286
        If Not _fusedTimingActiveInBed Then
            Throw New InvalidOperationException("No fused timing instruction is active.")
        End If

        _fusedTimingContextInBed.SequentialCs = _fusedTimingContextInBed.InitialCs
        _fusedTimingContextInBed.SequentialIp =
            CUShort((CInt(_fusedTimingContextInBed.InitialIp) + _fusedTimingCodeBytesInBed) And &HFFFF)

        _fusedTimingActiveInBed = False
        Return _fusedTimingContextInBed
    End Function

    Private Function CaptureTimingContext() As TimingContext286
        Dim context As New TimingContext286 With {
            .InitialCs = CS,
            .InitialIp = IP,
            .SequentialCs = CS,
            .InitialCx = CX,
            .InitialFlags = Flags
        }

        Dim cursor As Integer = IP
        Dim prefixCount As Integer
        While prefixCount < 15
            Dim prefix As Byte = PeekCodeByte(CS, CUShort(cursor And &HFFFF))
            Select Case prefix
                Case &H26, &H2E, &H36, &H3E, &HF0
                    cursor = (cursor + 1) And &HFFFF
                    prefixCount += 1
                Case &HF2
                    context.PrefixRep = 2
                    cursor = (cursor + 1) And &HFFFF
                    prefixCount += 1
                Case &HF3
                    context.PrefixRep = 3
                    cursor = (cursor + 1) And &HFFFF
                    prefixCount += 1
                Case Else
                    Exit While
            End Select
        End While

        context.Opcode = PeekCodeByte(CS, CUShort(cursor))
        cursor = (cursor + 1) And &HFFFF

        If context.Opcode = &HF Then
            context.HasSecondOpcode = True
            context.SecondOpcode = PeekCodeByte(CS, CUShort(cursor))
            cursor = (cursor + 1) And &HFFFF
        End If

        context.HasModRm = TimingOpcodeHasModRm(context.Opcode, context.SecondOpcode, context.HasSecondOpcode)
        If context.HasModRm Then
            Dim modRm As Byte = PeekCodeByte(CS, CUShort(cursor))
            cursor = (cursor + 1) And &HFFFF
            context.ModValue = modRm >> 6
            context.RegField = (modRm >> 3) And 7
            context.RmField = modRm And 7
            context.EffectiveAddressThreeTerms = context.ModValue <> 3 AndAlso
                                                     context.ModValue <> 0 AndAlso
                                                     context.RmField <= 3

            If context.ModValue = 0 AndAlso context.RmField = 6 Then
                cursor = (cursor + 2) And &HFFFF
            ElseIf context.ModValue = 1 Then
                cursor = (cursor + 1) And &HFFFF
            ElseIf context.ModValue = 2 Then
                cursor = (cursor + 2) And &HFFFF
            End If
        End If

        Dim immediateBytes As Integer = TimingImmediateBytes(context)
        If context.Opcode = &HC0 OrElse context.Opcode = &HC1 Then
            context.ShiftCount = PeekCodeByte(CS, CUShort(cursor)) And &H1F
        ElseIf context.Opcode = &HD2 OrElse context.Opcode = &HD3 Then
            context.ShiftCount = CL And &H1F
        ElseIf context.Opcode = &HD0 OrElse context.Opcode = &HD1 Then
            context.ShiftCount = 1
        End If
        If context.Opcode = &HC8 Then
            context.EnterLevel = PeekCodeByte(CS, CUShort((cursor + 2) And &HFFFF)) And &H1F
        End If

        context.OperandIsWord = TimingOperandIsWord(context.Opcode)
        cursor = (cursor + immediateBytes) And &HFFFF
        context.SequentialIp = CUShort(cursor)
        Return context
    End Function

    Private Sub CommitInstructionTiming(context As TimingContext286)
        Dim cycles As Integer = CalculateNominalTStates(context)
        If cycles < 1 Then cycles = 1

        Dim readyWaitInBed As Integer = ConsumeReadyWaitTStatesInBed()
        If readyWaitInBed > Integer.MaxValue - cycles Then
            Throw New InvalidOperationException("READY wait states overflowed the instruction timing result.")
        End If
        cycles += readyWaitInBed

        _lastInstructionTStates = cycles
        _totalTStates += cycles
    End Sub

    Private Sub CommitSyntheticTiming(tStates As Integer)
        If tStates < 1 Then tStates = 1

        Dim readyWaitInBed As Integer = ConsumeReadyWaitTStatesInBed()
        If readyWaitInBed > Integer.MaxValue - tStates Then
            Throw New InvalidOperationException("READY wait states overflowed the synthetic timing result.")
        End If
        tStates += readyWaitInBed

        _lastInstructionTStates = tStates
        _totalTStates += tStates
    End Sub

    Private Function CalculateNominalTStates(context As TimingContext286) As Integer
        Dim op As Integer = context.Opcode
        Dim memoryOperand As Boolean = context.HasModRm AndAlso context.ModValue <> 3
        Dim ea As Integer = If(context.EffectiveAddressThreeTerms, 1, 0)

        ' CROMWELL COMPILED 286 TIMING LEDGER BRICK 5C
        ' System opcodes and genuinely state-dependent instructions retain the
        ' documented decision tree below.  Common fixed/rm timings are one indexed
        ' lookup and one optional EA adjustment.
        If Not context.HasSecondOpcode Then
            Dim descriptorInBed As Integer = _compiledTimingLedgerInBed(op And &HFF)
            If descriptorInBed <> 0 Then
                If memoryOperand Then
                    Return ((descriptorInBed >> 8) And &HFF) + ea
                End If
                Return descriptorInBed And &HFF
            End If
        End If

        Dim branchTaken As Boolean = CS <> context.SequentialCs OrElse IP <> context.SequentialIp

        ' CROMWELL LAZY CONTROL-M TIMING BRICK 5B
        ' Intel's 286 "m" term is the length of the next instruction, but only a
        ' small control-transfer subset actually consumes it.  Do not decode the
        ' next instruction for MOV/ALU/string/etc. instructions that throw m away.
        Dim controlM As Integer = 1
        If TimingNeedsControlMInBed(context, branchTaken) Then
            Dim nextLength As Integer
            If _hotPathSampleActiveInBed Then
                Dim hotLengthStampInBed As Long = System.Diagnostics.Stopwatch.GetTimestamp()
                nextLength = MeasureInstructionLength(CS, IP)
                _hotPathLengthScanTicksInBed +=
                    System.Diagnostics.Stopwatch.GetTimestamp() - hotLengthStampInBed
            Else
                nextLength = MeasureInstructionLength(CS, IP)
            End If
            controlM = Math.Max(1, nextLength)
        End If

        ' Intel's published counts assume a prefetched instruction, no READY wait
        ' states, no HOLD, and no processor-extension transfer.  This is the
        ' documented nominal execution ledger; the prefetch/bus phase follows it.
        If context.HasSecondOpcode Then
            Return TimingForSystemOpcode(context, memoryOperand, ea)
        End If

        If IsAluRmOpcode(op) Then
            If memoryOperand Then
                If op >= &H38 AndAlso op <= &H3B AndAlso (op And 2) <> 0 Then Return 6 + ea
                Return 7 + ea
            End If
            Return 2
        End If

        If IsImmediateAccumulatorAluOpcode(op) Then Return 3

        Select Case op
            Case &H6, &HE, &H16, &H1E : Return 3
            Case &H7, &H17, &H1F : Return If(ProtectedMode, 20, 5)
            Case &H27, &H2F, &H37, &H3F : Return 3
            Case &H40 To &H4F : Return 2
            Case &H50 To &H57 : Return 3
            Case &H58 To &H5F : Return 5
            Case &H60 : Return 17
            Case &H61 : Return 19
            Case &H62 : Return 13 + ea
            Case &H63 : Return If(memoryOperand, 11 + ea, 10)
            Case &H68, &H6A : Return 3
            Case &H69, &H6B : Return If(memoryOperand, 24 + ea, 21)
            Case &H6C To &H6F
                Dim n As Integer = TimingRepeatCount(context)
                Return If(context.PrefixRep = 0, 5, 5 + 4 * n)
            Case &H70 To &H7F
                Return If(branchTaken, 7 + controlM, 3)
            Case &H80 To &H83
                If memoryOperand Then Return If(context.RegField = 7, 6 + ea, 7 + ea)
                Return 3
            Case &H84, &H85 : Return If(memoryOperand, 6 + ea, 2)
            Case &H86, &H87 : Return If(memoryOperand, 5 + ea, 3)
            Case &H88, &H89 : Return If(memoryOperand, 3 + ea, 2)
            Case &H8A, &H8B : Return If(memoryOperand, 5 + ea, 2)
            Case &H8C : Return If(memoryOperand, 3 + ea, 2)
            Case &H8E
                If ProtectedMode Then Return If(memoryOperand, 19 + ea, 17)
                Return If(memoryOperand, 5 + ea, 2)
            Case &H8D : Return 3 + ea
            Case &H8F : Return 5 + ea
            Case &H90 To &H97 : Return 3
            Case &H98, &H99 : Return 2
            Case &H9A : Return If(ProtectedMode, 26 + controlM, 13 + controlM)
            Case &H9B : Return 3
            Case &H9C : Return 3
            Case &H9D : Return 5
            Case &H9E, &H9F : Return 2
            Case &HA0, &HA1 : Return 5
            Case &HA2, &HA3 : Return 3
            Case &HA4 To &HA7, &HAA To &HAF
                Return TimingForString(context)
            Case &HA8, &HA9 : Return 3
            Case &HB0 To &HBF : Return 2
            Case &HC0, &HC1
                Dim n As Integer = context.ShiftCount
                Return If(memoryOperand, 8 + n + ea, 5 + n)
            Case &HD0, &HD1 : Return If(memoryOperand, 7 + ea, 2)
            Case &HD2, &HD3
                Dim n As Integer = context.ShiftCount
                Return If(memoryOperand, 8 + n + ea, 5 + n)
            Case &HC2, &HC3 : Return 11 + controlM
            Case &HC4, &HC5 : Return If(ProtectedMode, 21 + ea, 7 + ea)
            Case &HC6, &HC7 : Return If(memoryOperand, 3 + ea, 2)
            Case &HC8
                Dim level As Integer = context.EnterLevel
                If level = 0 Then Return 11
                If level = 1 Then Return 15
                Return 16 + 4 * (level - 1)
            Case &HC9 : Return 5
            Case &HCA, &HCB : Return If(ProtectedMode, 25 + controlM, 15 + controlM)
            Case &HCC, &HCD : Return 23 + controlM
            Case &HCE : Return If((context.InitialFlags And OverflowFlag) <> 0, 24 + controlM, 3)
            Case &HCF : Return If(ProtectedMode, 31 + controlM, 17 + controlM)
            Case &HD4 : Return 16
            Case &HD5 : Return 14
            Case &HD7 : Return 5
            Case &HD8 To &HDF : Return If(memoryOperand, 12 + ea, 9)
            Case &HE0 To &HE3 : Return If(branchTaken, 8 + controlM, 4)
            Case &HE4, &HE5, &HEC, &HED : Return 5
            Case &HE6, &HE7, &HEE, &HEF : Return 3
            Case &HE8 : Return 7 + controlM
            Case &HE9, &HEB : Return 7 + controlM
            Case &HEA : Return If(ProtectedMode, 23 + controlM, 11 + controlM)
            Case &HF4 : Return 2
            Case &HF5, &HF8, &HF9, &HFC, &HFD : Return 2
            Case &HFA : Return 3
            Case &HFB : Return 2
            Case &HF6, &HF7 : Return TimingForGroup3(context, memoryOperand, ea)
            Case &HFE, &HFF : Return TimingForGroup45(context, memoryOperand, ea, controlM)
            Case Else : Return 2
        End Select
    End Function

    Private Function TimingNeedsControlMInBed(context As TimingContext286,
                                              branchTakenInBed As Boolean) As Boolean
        If context.HasSecondOpcode Then Return False

        Select Case CInt(context.Opcode)
            ' Conditional transfers only pay the documented m term when taken.
            Case &H70 To &H7F
                Return branchTakenInBed

            ' Far CALL, near/far RET, software interrupt, IRET.
            Case &H9A, &HC2, &HC3, &HCA, &HCB, &HCC, &HCD, &HCF
                Return True

            ' INTO consumes m only when OF caused the interrupt.
            Case &HCE
                Return (context.InitialFlags And OverflowFlag) <> 0

            ' LOOP/JCXZ family only consumes m on the taken path.
            Case &HE0 To &HE3
                Return branchTakenInBed

            ' CALL/JMP immediate forms are unconditional control transfers.
            Case &HE8, &HE9, &HEA, &HEB
                Return True

            ' FF /2,/3,/4,/5 are CALL/JMP forms.  INC/DEC/PUSH do not use m.
            Case &HFF
                Return context.RegField >= 2 AndAlso context.RegField <= 5

            Case Else
                Return False
        End Select
    End Function

    Private Function TimingForString(context As TimingContext286) As Integer
        Dim op As Integer = context.Opcode
        Dim n As Integer = TimingRepeatCount(context)
        If context.PrefixRep = 0 Then
            Select Case op
                Case &HA4, &HA5, &HAC, &HAD : Return 5
                Case &HA6, &HA7 : Return 8
                Case &HAA, &HAB : Return 3
                Case &HAE, &HAF : Return 7
            End Select
        End If

        Select Case op
            Case &HA4, &HA5, &HAC, &HAD : Return 5 + 4 * n
            Case &HA6, &HA7 : Return 5 + 9 * n
            Case &HAA, &HAB : Return 4 + 3 * n
            Case &HAE, &HAF : Return 5 + 8 * n
            Case Else : Return 5 + 4 * n
        End Select
    End Function

    Private Function TimingRepeatCount(context As TimingContext286) As Integer
        If context.PrefixRep = 0 Then Return 1
        Return (CInt(context.InitialCx) - CInt(CX)) And &HFFFF
    End Function

    Private Function TimingForGroup3(context As TimingContext286, memoryOperand As Boolean, ea As Integer) As Integer
        Select Case context.RegField
            Case 0, 1
                Return If(memoryOperand, 6 + ea, 3)
            Case 2, 3
                Return If(memoryOperand, 7 + ea, 2)
            Case 4, 5
                If context.OperandIsWord Then Return If(memoryOperand, 24 + ea, 21)
                Return If(memoryOperand, 16 + ea, 13)
            Case 6
                If context.OperandIsWord Then Return If(memoryOperand, 25 + ea, 22)
                Return If(memoryOperand, 17 + ea, 14)
            Case 7
                If context.OperandIsWord Then Return If(memoryOperand, 28 + ea, 25)
                Return If(memoryOperand, 20 + ea, 17)
            Case Else
                Return 2
        End Select
    End Function

    Private Function TimingForGroup45(context As TimingContext286,
                                      memoryOperand As Boolean,
                                      ea As Integer,
                                      controlM As Integer) As Integer
        If context.Opcode = &HFE Then Return If(memoryOperand, 7 + ea, 2)

        Select Case context.RegField
            Case 0, 1 : Return If(memoryOperand, 7 + ea, 2)
            Case 2 : Return If(memoryOperand, 11 + ea + controlM, 7 + controlM)
            Case 3 : Return 16 + ea + controlM
            Case 4 : Return If(memoryOperand, 11 + ea + controlM, 7 + controlM)
            Case 5 : Return 15 + ea + controlM
            Case 6 : Return If(memoryOperand, 5 + ea, 3)
            Case Else : Return 2
        End Select
    End Function

    Private Function TimingForSystemOpcode(context As TimingContext286,
                                           memoryOperand As Boolean,
                                           ea As Integer) As Integer
        Select Case context.SecondOpcode
            Case &H0
                Select Case context.RegField
                    Case 0, 1 : Return If(memoryOperand, 3 + ea, 2)       ' SLDT/STR
                    Case 2, 3 : Return If(memoryOperand, 19 + ea, 17)    ' LLDT/LTR
                    Case 4, 5 : Return If(memoryOperand, 16 + ea, 14)    ' VERR/VERW
                    Case Else : Return 2
                End Select
            Case &H1
                Select Case context.RegField
                    Case 0, 2 : Return 11 + ea                           ' SGDT/LGDT
                    Case 1, 3 : Return 12 + ea                           ' SIDT/LIDT
                    Case 4 : Return If(memoryOperand, 3 + ea, 2)         ' SMSW
                    Case 6 : Return If(memoryOperand, 6 + ea, 3)         ' LMSW
                    Case Else : Return 2
                End Select
            Case &H2, &H3
                Return If(memoryOperand, 16 + ea, 14)                    ' LAR/LSL
            Case &H6
                Return 2                                                ' CLTS
            Case Else
                Return 2
        End Select
    End Function

    Private Function MeasureInstructionLength(codeSegment As UInt16, offset As UInt16) As Integer
        Dim start As Integer = offset
        Dim cursor As Integer = start
        Dim prefixCount As Integer
        While prefixCount < 15
            Select Case PeekCodeByte(codeSegment, CUShort(cursor And &HFFFF))
                Case &H26, &H2E, &H36, &H3E, &HF0, &HF2, &HF3
                    cursor = (cursor + 1) And &HFFFF
                    prefixCount += 1
                Case Else
                    Exit While
            End Select
        End While

        Dim opcode As Byte = PeekCodeByte(codeSegment, CUShort(cursor))
        cursor = (cursor + 1) And &HFFFF
        Dim second As Byte
        Dim hasSecond As Boolean
        If opcode = &HF Then
            hasSecond = True
            second = PeekCodeByte(codeSegment, CUShort(cursor))
            cursor = (cursor + 1) And &HFFFF
        End If

        Dim context As New TimingContext286 With {
            .Opcode = opcode,
            .SecondOpcode = second,
            .HasSecondOpcode = hasSecond
        }
        context.HasModRm = TimingOpcodeHasModRm(opcode, second, hasSecond)
        If context.HasModRm Then
            Dim modRm As Byte = PeekCodeByte(codeSegment, CUShort(cursor))
            cursor = (cursor + 1) And &HFFFF
            context.ModValue = modRm >> 6
            context.RegField = (modRm >> 3) And 7
            context.RmField = modRm And 7
            If context.ModValue = 0 AndAlso context.RmField = 6 Then
                cursor = (cursor + 2) And &HFFFF
            ElseIf context.ModValue = 1 Then
                cursor = (cursor + 1) And &HFFFF
            ElseIf context.ModValue = 2 Then
                cursor = (cursor + 2) And &HFFFF
            End If
        End If
        cursor = (cursor + TimingImmediateBytes(context)) And &HFFFF
        Dim length As Integer = (cursor - start) And &HFFFF
        If length <= 0 OrElse length > 15 Then Return 1
        Return length
    End Function

    Private Function PeekCodeByte(codeSegment As UInt16, offset As UInt16) As Byte
        Dim address As UInteger
        If ProtectedMode AndAlso _segmentValid(1) Then
            address = _segmentBases(1) + offset
        Else
            address = PhysicalRaw(codeSegment, offset)
        End If

        ' Timing/length peeks are host bookkeeping, not a second physical CPU bus
        ' cycle.  They may still use the existing memory facade, but Brick 8E must
        ' not charge READY debt for a bus transaction the real 80286 never issued.
        Dim previousSuppressInBed As Boolean = _suppressReadyWaitAccountingInBed
        _suppressReadyWaitAccountingInBed = True
        Try
            Return ReadByte(address)
        Finally
            _suppressReadyWaitAccountingInBed = previousSuppressInBed
        End Try
    End Function

    Private Shared Function TimingOpcodeHasModRm(opcode As Byte,
                                                 secondOpcode As Byte,
                                                 hasSecondOpcode As Boolean) As Boolean
        If hasSecondOpcode Then
            Return secondOpcode = &H0 OrElse secondOpcode = &H1 OrElse
                   secondOpcode = &H2 OrElse secondOpcode = &H3
        End If

        If (opcode >= &H0 AndAlso opcode <= &H3) OrElse
           (opcode >= &H8 AndAlso opcode <= &HB) OrElse
           (opcode >= &H10 AndAlso opcode <= &H13) OrElse
           (opcode >= &H18 AndAlso opcode <= &H1B) OrElse
           (opcode >= &H20 AndAlso opcode <= &H23) OrElse
           (opcode >= &H28 AndAlso opcode <= &H2B) OrElse
           (opcode >= &H30 AndAlso opcode <= &H33) OrElse
           (opcode >= &H38 AndAlso opcode <= &H3B) Then Return True

        Select Case opcode
            Case &H62, &H63, &H69, &H6B,
                 &H80 To &H8F,
                 &HC0, &HC1, &HC4 To &HC7,
                 &HD0 To &HD3, &HD8 To &HDF,
                 &HF6, &HF7, &HFE, &HFF
                Return True
            Case Else
                Return False
        End Select
    End Function

    Private Shared Function TimingImmediateBytes(context As TimingContext286) As Integer
        If context.HasSecondOpcode Then Return 0
        Dim opcode As Integer = context.Opcode

        If IsImmediateAccumulatorAluOpcode(opcode) Then Return If((opcode And 1) = 0, 1, 2)
        If opcode >= &H70 AndAlso opcode <= &H7F Then Return 1
        If opcode >= &HB0 AndAlso opcode <= &HB7 Then Return 1
        If opcode >= &HB8 AndAlso opcode <= &HBF Then Return 2

        Select Case opcode
            Case &H68 : Return 2
            Case &H69 : Return 2
            Case &H6A : Return 1
            Case &H6B : Return 1
            Case &H80, &H82, &H83 : Return 1
            Case &H81 : Return 2
            Case &H9A : Return 4
            Case &HA0 To &HA3 : Return 2
            Case &HA8 : Return 1
            Case &HA9 : Return 2
            Case &HC0, &HC1 : Return 1
            Case &HC2 : Return 2
            Case &HC6 : Return 1
            Case &HC7 : Return 2
            Case &HC8 : Return 3
            Case &HCA : Return 2
            Case &HCD : Return 1
            Case &HD4, &HD5 : Return 1
            Case &HE0 To &HE3 : Return 1
            Case &HE4 To &HE7 : Return 1
            Case &HE8, &HE9 : Return 2
            Case &HEA : Return 4
            Case &HEB : Return 1
            Case &HF6
                Return If(context.RegField = 0 OrElse context.RegField = 1, 1, 0)
            Case &HF7
                Return If(context.RegField = 0 OrElse context.RegField = 1, 2, 0)
            Case Else
                Return 0
        End Select
    End Function

    Private Shared Function TimingOperandIsWord(opcode As Byte) As Boolean
        Select Case opcode
            Case &HF7, &HC1, &HD1, &HD3 : Return True
            Case Else : Return (opcode And 1) <> 0
        End Select
    End Function

    Private Shared Function IsAluRmOpcode(opcode As Integer) As Boolean
        Return (opcode >= &H0 AndAlso opcode <= &H3) OrElse
               (opcode >= &H8 AndAlso opcode <= &HB) OrElse
               (opcode >= &H10 AndAlso opcode <= &H13) OrElse
               (opcode >= &H18 AndAlso opcode <= &H1B) OrElse
               (opcode >= &H20 AndAlso opcode <= &H23) OrElse
               (opcode >= &H28 AndAlso opcode <= &H2B) OrElse
               (opcode >= &H30 AndAlso opcode <= &H33) OrElse
               (opcode >= &H38 AndAlso opcode <= &H3B)
    End Function

    Private Shared Function IsImmediateAccumulatorAluOpcode(opcode As Integer) As Boolean
        Return opcode = &H4 OrElse opcode = &H5 OrElse opcode = &HC OrElse opcode = &HD OrElse
               opcode = &H14 OrElse opcode = &H15 OrElse opcode = &H1C OrElse opcode = &H1D OrElse
               opcode = &H24 OrElse opcode = &H25 OrElse opcode = &H2C OrElse opcode = &H2D OrElse
               opcode = &H34 OrElse opcode = &H35 OrElse opcode = &H3C OrElse opcode = &H3D
    End Function
End Class
