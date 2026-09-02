Imports System
Imports System.Collections.Generic

' Physical key identities on an IBM Enhanced 101-key AT keyboard.  These are
' positions/functions, not scan codes; scan-code generation belongs to the
' keyboard firmware model below.
Public Enum AtPhysicalKey
    None = 0
    Escape
    F1
    F2
    F3
    F4
    F5
    F6
    F7
    F8
    F9
    F10
    F11
    F12
    Grave
    D1
    D2
    D3
    D4
    D5
    D6
    D7
    D8
    D9
    D0
    Minus
    Equals
    Backspace
    Tab
    Q
    W
    E
    R
    T
    Y
    U
    I
    O
    P
    LeftBracket
    RightBracket
    Backslash
    CapsLock
    A
    S
    D
    F
    G
    H
    J
    K
    L
    Semicolon
    Quote
    Enter
    LeftShift
    Z
    X
    C
    V
    B
    N
    M
    Comma
    Period
    Slash
    RightShift
    LeftControl
    LeftAlt
    Space
    RightAlt
    RightControl
    PrintScreen
    ScrollLock
    Pause
    Insert
    Home
    PageUp
    Delete
    EndKey
    PageDown
    Up
    Left
    Down
    Right
    NumLock
    KeypadDivide
    KeypadMultiply
    KeypadSubtract
    KeypadAdd
    KeypadEnter
    Keypad0
    Keypad1
    Keypad2
    Keypad3
    Keypad4
    Keypad5
    Keypad6
    Keypad7
    Keypad8
    Keypad9
    KeypadDecimal
End Enum

<Flags()>
Public Enum AtSet3KeyBehavior As Byte
    None = 0
    Make = 1
    Break = 2
    Typematic = 4
    MakeBreak = Make Or Break
    TypematicMake = Make Or Typematic
    TypematicMakeBreak = Make Or Break Or Typematic
End Enum

' CROMWELL KEYBOARD REALITY BRICK 4 DEVICE
' IBM Enhanced 101-key AT keyboard.  It owns the key matrix, scan-code-set
' selection, make/break generation, typematic engine, LEDs, BAT/ID, and the
' keyboard-side command state.  The 8042 is deliberately NOT part of this
' class; bytes leave through the serial transmit FIFO and traverse the
' controller link one 11-bit frame at a time.
Public Class AtKeyboard101
    Private Const PicosecondsPerMillisecond As Long = 1000000000L
    Private Const PowerOnBatDelayPicoseconds As Long = 500L * PicosecondsPerMillisecond
    Private Const ResetAckAcceptancePicoseconds As Long = 500000000L ' 500 us of released clock/data
    Private Const MaxTransmitBytes As Integer = 16

    Private ReadOnly _scanTx As New Queue(Of Byte)()
    Private ReadOnly _responseTx As New Queue(Of Byte)()
    Private ReadOnly _pressed As New HashSet(Of AtPhysicalKey)()
    Private ReadOnly _pressOrder As New List(Of AtPhysicalKey)()
    Private ReadOnly _set3Behavior As New Dictionary(Of AtPhysicalKey, AtSet3KeyBehavior)()

    Private _scanSet As Byte = 2
    Private _ledState As Byte
    Private _typematicByte As Byte = &H2B
    Private _scanningEnabled As Boolean = True
    Private _pendingCommand As Integer = -1
    Private _set3ListCommand As Integer = -1
    Private _lastTransmittedByte As Byte = &HAA
    Private _lastNonResendByte As Byte = &HAA
    Private _lastNonResendWasCommandResponse As Boolean = True
    Private _resendPending As Boolean
    Private _resendValue As Byte
    Private _resendCommandResponse As Boolean
    Private _batDelayRemaining As Long
    Private _typematicKey As AtPhysicalKey = AtPhysicalKey.None
    Private _typematicDelayRemaining As Long
    Private _typematicPeriodPicoseconds As Long
    Private _resetAckPending As Boolean
    Private _resetAcceptanceDelayRemaining As Long
    Private _resumeScanningAfterId As Boolean
    Private _resumeScanningAfterParameter As Boolean
    Private _hostTransmissionInhibited As Boolean

    Public Event LedStateChanged(state As Byte)

    Public Sub New()
        ResetDefaults(clearPressed:=True, preserveLeds:=False)
        StartBat()
    End Sub

    Public ReadOnly Property ScanCodeSet As Byte
        Get
            Return _scanSet
        End Get
    End Property

    Public ReadOnly Property LedState As Byte
        Get
            Return _ledState
        End Get
    End Property

    Public ReadOnly Property TypematicByte As Byte
        Get
            Return _typematicByte
        End Get
    End Property

    Public ReadOnly Property ScanningEnabled As Boolean
        Get
            Return _scanningEnabled
        End Get
    End Property

    Public ReadOnly Property HasByteToTransmit As Boolean
        Get
            Return _responseTx.Count <> 0 OrElse _scanTx.Count <> 0
        End Get
    End Property

    Public ReadOnly Property BasicAssuranceTestActive As Boolean
        Get
            Return _batDelayRemaining > 0
        End Get
    End Property

    Public ReadOnly Property PendingTransmitByteCount As Integer
        Get
            Return _responseTx.Count + _scanTx.Count
        End Get
    End Property

    Public ReadOnly Property PressedKeyCount As Integer
        Get
            Return _pressed.Count
        End Get
    End Property

    Public Sub PowerOnReset()
        _scanTx.Clear()
        _responseTx.Clear()
        ResetDefaults(clearPressed:=True, preserveLeds:=False)
        StartBat()
    End Sub

    Private Sub StartBat()
        _scanningEnabled = False
        StopTypematic()
        ' The Enhanced Keyboard illuminates all three indicators during BAT.
        If _ledState <> 7 Then
            _ledState = 7
            RaiseEvent LedStateChanged(_ledState)
        End If
        _batDelayRemaining = PowerOnBatDelayPicoseconds
    End Sub

    Private Sub ResetDefaults(clearPressed As Boolean, preserveLeds As Boolean)
        _scanSet = 2
        If Not preserveLeds AndAlso _ledState <> 0 Then
            _ledState = 0
            RaiseEvent LedStateChanged(_ledState)
        End If
        _typematicByte = &H2B
        _pendingCommand = -1
        _set3ListCommand = -1
        _resendPending = False
        _resendValue = 0
        _resendCommandResponse = False
        _typematicKey = AtPhysicalKey.None
        _typematicDelayRemaining = 0
        _typematicPeriodPicoseconds = TypematicPeriodFromByte(_typematicByte)
        _resetAckPending = False
        _resetAcceptanceDelayRemaining = 0
        _resumeScanningAfterId = False
        _resumeScanningAfterParameter = False
        If clearPressed Then
            _pressed.Clear()
            _pressOrder.Clear()
        End If
        ResetSet3Defaults()
    End Sub

    ' Clock inhibition is a physical signal from the system-side controller.
    ' It stops serial transmission, not the keyboard microcontroller's matrix
    ' scan or typematic clock.  Generated scan bytes accumulate in the real
    ' keyboard's bounded FIFO while the host holds CLOCK low.
    Public Sub SetHostTransmissionInhibited(inhibited As Boolean)
        _hostTransmissionInhibited = inhibited
    End Sub

    ' Event-aware motherboard scheduler hook.  This reports only keyboard-side
    ' transitions which can create a new serial byte without a CPU access.
    Public ReadOnly Property PicosecondsUntilNextEvent As Long
        Get
            Dim earliest As Long = Long.MaxValue

            If _resetAcceptanceDelayRemaining > 0 AndAlso _resetAcceptanceDelayRemaining < earliest Then
                earliest = _resetAcceptanceDelayRemaining
            End If

            If _batDelayRemaining > 0 AndAlso _batDelayRemaining < earliest Then
                earliest = _batDelayRemaining
            End If

            If _typematicKey <> AtPhysicalKey.None AndAlso
               _scanningEnabled AndAlso
               _pressed.Contains(_typematicKey) AndAlso
               IsTypematicEnabled(_typematicKey) AndAlso
               _typematicDelayRemaining > 0 AndAlso
               _typematicDelayRemaining < earliest Then
                earliest = _typematicDelayRemaining
            End If

            Return earliest
        End Get
    End Property

    Public Sub AdvanceTime(elapsedPicoseconds As Long)
        If elapsedPicoseconds <= 0 Then Return

        Dim remaining As Long = elapsedPicoseconds

        ' IBM reset sequencing: after the host has consumed the Reset-command
        ' ACK, clock and data remain released for at least 500 us before BAT
        ' begins.  This is distinct from the approximately 500 ms BAT itself.
        If _resetAcceptanceDelayRemaining > 0 Then
            If remaining < _resetAcceptanceDelayRemaining Then
                _resetAcceptanceDelayRemaining -= remaining
                Return
            End If
            remaining -= _resetAcceptanceDelayRemaining
            _resetAcceptanceDelayRemaining = 0
            StartBat()
        End If

        If _batDelayRemaining > 0 Then
            If remaining < _batDelayRemaining Then
                _batDelayRemaining -= remaining
                Return
            End If
            remaining -= _batDelayRemaining
            _batDelayRemaining = 0
            If _ledState <> 0 Then
                _ledState = 0
                RaiseEvent LedStateChanged(_ledState)
            End If
            QueueResponse(&HAA)
            _scanningEnabled = True
        End If

        If remaining <= 0 Then Return
        If _typematicKey = AtPhysicalKey.None OrElse Not _scanningEnabled Then Return
        If Not _pressed.Contains(_typematicKey) OrElse Not IsTypematicEnabled(_typematicKey) Then
            StopTypematic()
            Return
        End If

        _typematicDelayRemaining -= remaining
        While _typematicDelayRemaining <= 0 AndAlso _typematicKey <> AtPhysicalKey.None
            EmitMake(_typematicKey, isTypematicRepeat:=True)
            _typematicDelayRemaining += Math.Max(1L, _typematicPeriodPicoseconds)
        End While
    End Sub

    Public Sub SetPhysicalKey(key As AtPhysicalKey, pressed As Boolean)
        If key = AtPhysicalKey.None Then Return

        If pressed Then
            If Not _pressed.Add(key) Then Return ' Host autorepeat is not the keyboard's typematic clock.
            _pressOrder.Remove(key)
            _pressOrder.Add(key)
            If _scanningEnabled Then EmitMake(key, isTypematicRepeat:=False)
            If _scanningEnabled AndAlso IsTypematicEnabled(key) Then StartTypematic(key)
        Else
            If Not _pressed.Remove(key) Then Return
            _pressOrder.Remove(key)
            Dim wasTypematic As Boolean = (_typematicKey = key)
            If wasTypematic Then StopTypematic()
            If _scanningEnabled Then EmitBreak(key)
            If wasTypematic AndAlso _scanningEnabled Then RestartTypematicIfHeld()
        End If
    End Sub

    Public Sub ReleaseAllPhysicalKeys()
        If _pressed.Count = 0 Then Return
        Dim keys(_pressed.Count - 1) As AtPhysicalKey
        _pressed.CopyTo(keys)
        For Each key In keys
            SetPhysicalKey(key, False)
        Next
    End Sub

    Public Function TryDequeueTransmitByte(ByRef value As Byte, ByRef commandResponse As Boolean) As Boolean
        If _resendPending Then
            value = _resendValue
            commandResponse = _resendCommandResponse
            _resendPending = False
            Return True
        End If
        If _responseTx.Count <> 0 Then
            value = _responseTx.Dequeue()
            commandResponse = True
            Return True
        End If
        If _scanTx.Count = 0 Then Return False
        value = _scanTx.Dequeue()
        commandResponse = False
        Return True
    End Function

    Public Sub NotifyByteTransmitted(value As Byte, commandResponse As Boolean)
        _lastTransmittedByte = value
        If value <> &HFE Then
            _lastNonResendByte = value
            _lastNonResendWasCommandResponse = commandResponse
        End If

        ' Read-ID suspends scanning until the final ID byte has crossed the wire.
        If _resumeScanningAfterId AndAlso value = &H83 Then
            _resumeScanningAfterId = False
            _scanningEnabled = True
            RestartTypematicIfHeld()
        End If
    End Sub

    Public Sub NotifyControllerAcceptedByte(value As Byte, commandResponse As Boolean)
        ' Reset execution begins only after the host consumes the ACK from
        ' port 60h and the controller releases clock/data.  The 500 us idle-high
        ' acceptance interval is modeled before BAT begins.
        If commandResponse AndAlso _resetAckPending AndAlso value = &HFA Then
            _resetAckPending = False
            _resetAcceptanceDelayRemaining = ResetAckAcceptancePicoseconds
        End If
    End Sub

    ' The keyboard is the receiver for system-to-keyboard frames.  It validates
    ' start, stop and odd parity itself; a malformed host transmission causes
    ' the Enhanced Keyboard to request Resend (FEh) instead of executing the byte.
    Public Sub ReceiveHostSerialFrame(frame As UInt16)
        If _batDelayRemaining > 0 OrElse _resetAcceptanceDelayRemaining > 0 Then Return

        Dim value As Byte
        If Not TryDecodeSerialFrame(frame, value) Then
            QueueResponse(&HFE)
            Return
        End If

        ReceiveHostByte(value)
    End Sub

    Private Shared Function TryDecodeSerialFrame(frame As UInt16, ByRef value As Byte) As Boolean
        If (frame And 1US) <> 0US Then Return False
        If (frame And CUShort(1 << 10)) = 0US Then Return False

        Dim decoded As Integer
        Dim oneCount As Integer
        For bit As Integer = 0 To 7
            If (frame And CUShort(1 << (bit + 1))) <> 0US Then
                decoded = decoded Or (1 << bit)
                oneCount += 1
            End If
        Next
        If (frame And CUShort(1 << 9)) <> 0US Then oneCount += 1
        If (oneCount And 1) = 0 Then Return False

        value = CByte(decoded)
        Return True
    End Function

    Public Sub ReceiveHostByte(value As Byte)
        ' During POR/BAT the enhanced keyboard ignores clock/data activity.
        If _batDelayRemaining > 0 Then Return

        ' A command byte terminates the set-3 key-identification list.
        If _set3ListCommand >= 0 AndAlso IsKeyboardCommand(value) Then
            _set3ListCommand = -1
        ElseIf _set3ListCommand >= 0 Then
            Dim key As AtPhysicalKey
            If TryKeyFromSet3Code(value, key) Then
                ApplySet3Behavior(key, BehaviorForSet3Command(CByte(_set3ListCommand)))
                QueueResponse(&HFA)
                Return
            End If
            QueueResponse(&HFE)
            Return
        End If

        ' ED/F0/F3 are two-byte commands.  A new command in place of their
        ' option byte aborts the pending operation without changing its value.
        If _pendingCommand >= 0 AndAlso IsKeyboardCommand(value) Then
            _pendingCommand = -1
            RestoreParameterScanning()
            ' Continue below and execute the new command.
        ElseIf _pendingCommand >= 0 Then
            Dim pending As Byte = CByte(_pendingCommand)
            _pendingCommand = -1
            Select Case pending
                Case &HED
                    If (value And &HF8) <> 0 Then
                        QueueResponse(&HFE)
                    Else
                        _ledState = CByte(value And 7)
                        QueueResponse(&HFA)
                        RaiseEvent LedStateChanged(_ledState)
                    End If
                    RestoreParameterScanning()
                    Return
                Case &HF0
                    If value = 0 Then
                        QueueResponse(&HFA)
                        QueueResponse(_scanSet)
                    ElseIf value >= 1 AndAlso value <= 3 Then
                        _scanSet = value
                        QueueResponse(&HFA)
                    Else
                        QueueResponse(&HFE)
                    End If
                    RestoreParameterScanning()
                    Return
                Case &HF3
                    If (value And &H80) <> 0 Then
                        QueueResponse(&HFE)
                    Else
                        _typematicByte = value
                        _typematicPeriodPicoseconds = TypematicPeriodFromByte(value)
                        QueueResponse(&HFA)
                    End If
                    RestoreParameterScanning()
                    Return
            End Select
        End If

        Select Case value
            Case &HED ' Set/reset status indicators
                BeginParameterCommand(value, clearScanBuffer:=False)
            Case &HEE ' Echo -- echo itself, not ACK then echo
                QueueResponse(&HEE)
            Case &HEF, &HF1 ' Explicitly invalid commands
                QueueResponse(&HFE)
            Case &HF0 ' Select/query scan-code set
                BeginParameterCommand(value, clearScanBuffer:=True)
            Case &HF2 ' Read ID: enhanced 101/102-key keyboard
                _resumeScanningAfterId = _scanningEnabled
                _scanningEnabled = False
                StopTypematic()
                QueueResponse(&HFA)
                QueueResponse(&HAB)
                QueueResponse(&H83)
            Case &HF3 ' Set typematic rate/delay
                BeginParameterCommand(value, clearScanBuffer:=False)
            Case &HF4 ' Enable scanning
                ClearScanBuffer()
                QueueResponse(&HFA)
                _scanningEnabled = True
                RestartTypematicIfHeld()
            Case &HF5 ' Default-disable
                ClearScanBuffer()
                ResetDefaults(clearPressed:=False, preserveLeds:=True)
                _scanningEnabled = False
                QueueResponse(&HFA)
            Case &HF6 ' Set defaults; preserve the prior scanning enable state
                Dim wasScanning As Boolean = _scanningEnabled
                ClearScanBuffer()
                ResetDefaults(clearPressed:=False, preserveLeds:=True)
                _scanningEnabled = wasScanning
                QueueResponse(&HFA)
                If wasScanning Then RestartTypematicIfHeld()
            Case &HF7, &HF8, &HF9, &HFA
                ClearScanBuffer()
                QueueResponse(&HFA)
                ResetSet3Behavior(BehaviorForSet3Command(value))
            Case &HFB, &HFC, &HFD
                ClearScanBuffer()
                QueueResponse(&HFA)
                _set3ListCommand = value
            Case &HFE ' Resend last non-Resend byte, preserving its original class
                _resendValue = _lastNonResendByte
                _resendCommandResponse = _lastNonResendWasCommandResponse
                _resendPending = True
            Case &HFF ' Reset + BAT
                _scanTx.Clear()
                _responseTx.Clear()
                ResetDefaults(clearPressed:=True, preserveLeds:=False)
                _scanningEnabled = False
                _resetAckPending = True
                QueueResponse(&HFA)
            Case Else
                QueueResponse(&HFE)
        End Select
    End Sub

    Private Shared Function IsKeyboardCommand(value As Byte) As Boolean
        Return value >= &HED
    End Function

    Private Sub BeginParameterCommand(command As Byte, clearScanBuffer As Boolean)
        _resumeScanningAfterParameter = _scanningEnabled
        _scanningEnabled = False
        StopTypematic()
        If clearScanBuffer Then _scanTx.Clear()
        QueueResponse(&HFA)
        _pendingCommand = command
    End Sub

    Private Sub RestoreParameterScanning()
        Dim resumeScanning As Boolean = _resumeScanningAfterParameter
        _resumeScanningAfterParameter = False
        _scanningEnabled = resumeScanning
        If resumeScanning Then RestartTypematicIfHeld()
    End Sub

    Private Sub QueueResponse(value As Byte)
        ' Command responses do not consume positions in the keyboard's 16-byte
        ' keystroke FIFO.  They are serialized ahead of pending scan bytes once
        ' the controller releases the clock line.
        _responseTx.Enqueue(value)
    End Sub

    Private Sub ClearScanBuffer()
        _scanTx.Clear()
        StopTypematic()
    End Sub

    Private Sub QueueScanByte(value As Byte)
        If _scanTx.Count >= MaxTransmitBytes Then
            MarkScanBufferOverrun()
            Return
        End If
        _scanTx.Enqueue(value)
    End Sub

    Private Sub MarkScanBufferOverrun()
        ' IBM Enhanced Keyboard: capacity is 16 normal bytes.  On overflow the
        ' last buffered code is replaced by the set-specific overrun code; the
        ' earlier fifteen bytes retain FIFO order and additional keystrokes are
        ' discarded until space becomes available.
        Dim retained As Byte() = _scanTx.ToArray()
        _scanTx.Clear()
        Dim keep As Integer = Math.Min(15, retained.Length)
        For i As Integer = 0 To keep - 1
            _scanTx.Enqueue(retained(i))
        Next
        _scanTx.Enqueue(If(_scanSet = 1, CByte(&HFF), CByte(&H0)))
    End Sub

    Private Sub StartTypematic(key As AtPhysicalKey)
        If key = AtPhysicalKey.Pause Then Return
        _typematicKey = key
        Dim delayQuarterSeconds As Integer = ((CInt(_typematicByte) >> 5) And 3) + 1
        _typematicDelayRemaining = CLng(delayQuarterSeconds) * 250L * PicosecondsPerMillisecond
        _typematicPeriodPicoseconds = TypematicPeriodFromByte(_typematicByte)
    End Sub

    Private Sub RestartTypematicIfHeld()
        ' The most recently pressed still-held typematic key owns repeat.  If it
        ' is released, the next-most-recent held key resumes after a fresh delay.
        For i As Integer = _pressOrder.Count - 1 To 0 Step -1
            Dim key As AtPhysicalKey = _pressOrder(i)
            If _pressed.Contains(key) AndAlso IsTypematicEnabled(key) Then
                StartTypematic(key)
                Exit For
            End If
        Next
    End Sub

    Private Sub StopTypematic()
        _typematicKey = AtPhysicalKey.None
        _typematicDelayRemaining = 0
    End Sub

    Private Shared Function TypematicPeriodFromByte(value As Byte) As Long
        ' IBM/PS2 formula: cps = 240 / ((8+A) * 2^B), A=bits 0..2, B=bits 3..4.
        Dim a As Integer = value And 7
        Dim b As Integer = (value >> 3) And 3
        Dim denominator As Integer = (8 + a) << b
        Return (1000000000000L * denominator) \ 240L
    End Function

    Private Function IsTypematicEnabled(key As AtPhysicalKey) As Boolean
        ' IBM's enhanced keyboard makes every key except Pause typematic in
        ' scan sets 1 and 2.  BIOS lock/modifier logic suppresses repeated side
        ' effects; the keyboard itself still repeats the make sequence.
        If key = AtPhysicalKey.Pause Then Return False
        If _scanSet <> 3 Then Return True
        Dim behavior As AtSet3KeyBehavior = AtSet3KeyBehavior.TypematicMakeBreak
        If _set3Behavior.TryGetValue(key, behavior) Then Return (behavior And AtSet3KeyBehavior.Typematic) <> 0
        Return True
    End Function

    Private Function ShouldEmitSet3Make(key As AtPhysicalKey) As Boolean
        Dim behavior As AtSet3KeyBehavior = AtSet3KeyBehavior.TypematicMakeBreak
        If _set3Behavior.TryGetValue(key, behavior) Then Return (behavior And AtSet3KeyBehavior.Make) <> 0
        Return True
    End Function

    Private Function ShouldEmitSet3Break(key As AtPhysicalKey) As Boolean
        Dim behavior As AtSet3KeyBehavior = AtSet3KeyBehavior.TypematicMakeBreak
        If _set3Behavior.TryGetValue(key, behavior) Then Return (behavior And AtSet3KeyBehavior.Break) <> 0
        Return True
    End Function

    Private Sub ResetSet3Defaults()
        ' IBM Enhanced 101-key scan-set-3 power-on defaults. "Typematic"
        ' means make + typematic with no break; modifiers use make/break;
        ' function/lock and most editing/keypad keys are make-only.
        ResetSet3Behavior(AtSet3KeyBehavior.TypematicMake)

        For Each key In {AtPhysicalKey.CapsLock, AtPhysicalKey.LeftShift, AtPhysicalKey.RightShift,
                         AtPhysicalKey.LeftControl, AtPhysicalKey.LeftAlt}
            _set3Behavior(key) = AtSet3KeyBehavior.MakeBreak
        Next

        For Each key In {AtPhysicalKey.RightAlt, AtPhysicalKey.RightControl,
                         AtPhysicalKey.Insert, AtPhysicalKey.Home, AtPhysicalKey.PageUp,
                         AtPhysicalKey.EndKey, AtPhysicalKey.PageDown,
                         AtPhysicalKey.NumLock, AtPhysicalKey.KeypadDivide,
                         AtPhysicalKey.KeypadMultiply, AtPhysicalKey.KeypadSubtract,
                         AtPhysicalKey.KeypadEnter, AtPhysicalKey.Keypad0, AtPhysicalKey.Keypad1,
                         AtPhysicalKey.Keypad2, AtPhysicalKey.Keypad3, AtPhysicalKey.Keypad4,
                         AtPhysicalKey.Keypad5, AtPhysicalKey.Keypad6, AtPhysicalKey.Keypad7,
                         AtPhysicalKey.Keypad8, AtPhysicalKey.Keypad9, AtPhysicalKey.KeypadDecimal,
                         AtPhysicalKey.Escape, AtPhysicalKey.F1, AtPhysicalKey.F2, AtPhysicalKey.F3,
                         AtPhysicalKey.F4, AtPhysicalKey.F5, AtPhysicalKey.F6, AtPhysicalKey.F7,
                         AtPhysicalKey.F8, AtPhysicalKey.F9, AtPhysicalKey.F10, AtPhysicalKey.F11,
                         AtPhysicalKey.F12, AtPhysicalKey.PrintScreen, AtPhysicalKey.ScrollLock,
                         AtPhysicalKey.Pause}
            _set3Behavior(key) = AtSet3KeyBehavior.Make
        Next

        ' The editing arrows and Delete are typematic in the IBM default table.
        For Each key In {AtPhysicalKey.Delete, AtPhysicalKey.Left, AtPhysicalKey.Right,
                         AtPhysicalKey.Up, AtPhysicalKey.Down, AtPhysicalKey.KeypadAdd}
            _set3Behavior(key) = AtSet3KeyBehavior.TypematicMake
        Next
    End Sub

    Private Sub ResetSet3Behavior(behavior As AtSet3KeyBehavior)
        _set3Behavior.Clear()
        For Each key As AtPhysicalKey In [Enum].GetValues(GetType(AtPhysicalKey))
            If key <> AtPhysicalKey.None Then _set3Behavior(key) = behavior
        Next
    End Sub

    Private Sub ApplySet3Behavior(key As AtPhysicalKey, behavior As AtSet3KeyBehavior)
        _set3Behavior(key) = behavior
    End Sub

    Private Shared Function BehaviorForSet3Command(command As Byte) As AtSet3KeyBehavior
        Select Case command
            Case &HF7, &HFB : Return AtSet3KeyBehavior.TypematicMake
            Case &HF8, &HFC : Return AtSet3KeyBehavior.MakeBreak
            Case &HF9, &HFD : Return AtSet3KeyBehavior.Make
            Case Else : Return AtSet3KeyBehavior.TypematicMakeBreak
        End Select
    End Function

    Private Sub EmitMake(key As AtPhysicalKey, isTypematicRepeat As Boolean)
        If _scanSet = 3 AndAlso Not ShouldEmitSet3Make(key) Then Return
        Select Case _scanSet
            Case 1 : EmitSet1(key, released:=False)
            Case 2 : EmitSet2(key, released:=False)
            Case 3 : EmitSet3(key, released:=False)
        End Select
    End Sub

    Private Sub EmitBreak(key As AtPhysicalKey)
        ' Plain Pause has no break sequence in sets 1/2.  With Ctrl held the
        ' physical key is Break and does have an E0 make/break pair.
        If key = AtPhysicalKey.Pause AndAlso _scanSet <> 3 AndAlso Not AnyControlDown() Then Return
        If _scanSet = 3 AndAlso Not ShouldEmitSet3Break(key) Then Return
        Select Case _scanSet
            Case 1 : EmitSet1(key, released:=True)
            Case 2 : EmitSet2(key, released:=True)
            Case 3 : EmitSet3(key, released:=True)
        End Select
    End Sub

    Private Function AnyShiftDown() As Boolean
        Return _pressed.Contains(AtPhysicalKey.LeftShift) OrElse _pressed.Contains(AtPhysicalKey.RightShift)
    End Function

    Private Function LeftShiftDown() As Boolean
        Return _pressed.Contains(AtPhysicalKey.LeftShift)
    End Function

    Private Function RightShiftDown() As Boolean
        Return _pressed.Contains(AtPhysicalKey.RightShift)
    End Function

    Private Sub QueueSet1HeldShiftBreaks()
        If LeftShiftDown() Then QueueSequence(&HE0, &HAA)
        If RightShiftDown() Then QueueSequence(&HE0, &HB6)
    End Sub

    Private Sub QueueSet1HeldShiftMakes()
        If LeftShiftDown() Then QueueSequence(&HE0, &H2A)
        If RightShiftDown() Then QueueSequence(&HE0, &H36)
    End Sub

    Private Sub QueueSet2HeldShiftBreaks()
        If LeftShiftDown() Then QueueSequence(&HE0, &HF0, &H12)
        If RightShiftDown() Then QueueSequence(&HE0, &HF0, &H59)
    End Sub

    Private Sub QueueSet2HeldShiftMakes()
        If LeftShiftDown() Then QueueSequence(&HE0, &H12)
        If RightShiftDown() Then QueueSequence(&HE0, &H59)
    End Sub

    Private Function AnyControlDown() As Boolean
        Return _pressed.Contains(AtPhysicalKey.LeftControl) OrElse _pressed.Contains(AtPhysicalKey.RightControl)
    End Function

    Private Function AnyAltDown() As Boolean
        Return _pressed.Contains(AtPhysicalKey.LeftAlt) OrElse _pressed.Contains(AtPhysicalKey.RightAlt)
    End Function

    Private Shared Function IsEnhancedNavigationKey(key As AtPhysicalKey) As Boolean
        Select Case key
            Case AtPhysicalKey.Insert, AtPhysicalKey.Delete, AtPhysicalKey.Home, AtPhysicalKey.EndKey,
                 AtPhysicalKey.PageUp, AtPhysicalKey.PageDown, AtPhysicalKey.Up, AtPhysicalKey.Down,
                 AtPhysicalKey.Left, AtPhysicalKey.Right
                Return True
            Case Else
                Return False
        End Select
    End Function

    Private Sub EmitSet1(key As AtPhysicalKey, released As Boolean)
        If key = AtPhysicalKey.Pause Then
            If AnyControlDown() Then
                If released Then QueueSequence(&HE0, &HC6) Else QueueSequence(&HE0, &H46)
            ElseIf Not released Then
                QueueSequence(&HE1, &H1D, &H45, &HE1, &H9D, &HC5)
            End If
            Return
        End If

        If key = AtPhysicalKey.PrintScreen Then
            If AnyAltDown() Then
                If released Then QueueSequence(&HD4) Else QueueSequence(&H54)
            ElseIf AnyShiftDown() OrElse AnyControlDown() Then
                If released Then QueueSequence(&HE0, &HB7) Else QueueSequence(&HE0, &H37)
            Else
                If released Then QueueSequence(&HE0, &HB7, &HE0, &HAA) Else QueueSequence(&HE0, &H2A, &HE0, &H37)
            End If
            Return
        End If

        Dim code As Byte, extended As Boolean
        If Not TryGetSet1Code(key, code, extended) Then Return

        If extended AndAlso IsEnhancedNavigationKey(key) Then
            Dim shiftHeld As Boolean = AnyShiftDown()
            Dim numOn As Boolean = (_ledState And 2) <> 0
            If Not released Then
                If numOn AndAlso Not shiftHeld Then
                    QueueSequence(&HE0, &H2A)
                ElseIf Not numOn AndAlso shiftHeld Then
                    QueueSet1HeldShiftBreaks()
                End If
                QueueSequence(&HE0, code)
            Else
                QueueSequence(&HE0, CByte(code Or &H80))
                If numOn AndAlso Not shiftHeld Then
                    QueueSequence(&HE0, &HAA)
                ElseIf Not numOn AndAlso shiftHeld Then
                    QueueSet1HeldShiftMakes()
                End If
            End If
            Return
        End If

        If key = AtPhysicalKey.KeypadDivide AndAlso AnyShiftDown() Then
            ' Key 95 has its own artificial-shift form.  Unlike the ordinary
            ' enhanced navigation keys, the slash code itself is unprefixed in
            ' the Shift case; this is what the original translated stream does.
            If Not released Then
                QueueSet1HeldShiftBreaks()
                QueueSequence(&H35)
            Else
                QueueSet1HeldShiftMakes()
                QueueSequence(&HB5)
            End If
            Return
        End If

        If extended Then
            QueueSequence(&HE0, If(released, CByte(code Or &H80), code))
        Else
            QueueSequence(If(released, CByte(code Or &H80), code))
        End If
    End Sub

    Private Sub EmitSet2(key As AtPhysicalKey, released As Boolean)
        If key = AtPhysicalKey.Pause Then
            If AnyControlDown() Then
                If released Then QueueSequence(&HE0, &HF0, &H7E) Else QueueSequence(&HE0, &H7E)
            ElseIf Not released Then
                QueueSequence(&HE1, &H14, &H77, &HE1, &HF0, &H14, &HF0, &H77)
            End If
            Return
        End If

        If key = AtPhysicalKey.PrintScreen Then
            If AnyAltDown() Then
                If released Then QueueSequence(&HF0, &H84) Else QueueSequence(&H84)
            ElseIf AnyShiftDown() OrElse AnyControlDown() Then
                If released Then QueueSequence(&HE0, &HF0, &H7C) Else QueueSequence(&HE0, &H7C)
            Else
                If released Then QueueSequence(&HE0, &HF0, &H7C, &HE0, &HF0, &H12) Else QueueSequence(&HE0, &H12, &HE0, &H7C)
            End If
            Return
        End If

        Dim code As Byte, extended As Boolean
        If Not TryGetSet2Code(key, code, extended) Then Return

        If extended AndAlso IsEnhancedNavigationKey(key) Then
            Dim shiftHeld As Boolean = AnyShiftDown()
            Dim numOn As Boolean = (_ledState And 2) <> 0
            If Not released Then
                If numOn AndAlso Not shiftHeld Then
                    QueueSequence(&HE0, &H12)
                ElseIf Not numOn AndAlso shiftHeld Then
                    QueueSet2HeldShiftBreaks()
                End If
                QueueSequence(&HE0, code)
            Else
                QueueSequence(&HE0, &HF0, code)
                If numOn AndAlso Not shiftHeld Then
                    QueueSequence(&HE0, &HF0, &H12)
                ElseIf Not numOn AndAlso shiftHeld Then
                    QueueSet2HeldShiftMakes()
                End If
            End If
            Return
        End If

        If key = AtPhysicalKey.KeypadDivide AndAlso AnyShiftDown() Then
            ' IBM key 95: shifted make is E0 F0 <shift> 4A and shifted break
            ' is E0 <shift> F0 4A (one pair for each held Shift key).
            If Not released Then
                QueueSet2HeldShiftBreaks()
                QueueSequence(&H4A)
            Else
                QueueSet2HeldShiftMakes()
                QueueSequence(&HF0, &H4A)
            End If
            Return
        End If

        If extended Then
            If released Then QueueSequence(&HE0, &HF0, code) Else QueueSequence(&HE0, code)
        Else
            If released Then QueueSequence(&HF0, code) Else QueueSequence(code)
        End If
    End Sub

    Private Sub EmitSet3(key As AtPhysicalKey, released As Boolean)
        Dim code As Byte
        If Not TryGetSet3Code(key, code) Then Return
        If released Then QueueSequence(&HF0, code) Else QueueSequence(code)
    End Sub

    Private Sub QueueSequence(ParamArray values() As Byte)
        If values Is Nothing OrElse values.Length = 0 Then Return
        ' Multi-byte key sequences are atomic at the keyboard buffer.  If the
        ' complete sequence will not fit, no partial sequence is inserted and
        ' the final buffer position becomes the overrun indication.
        If _scanTx.Count + values.Length > MaxTransmitBytes Then
            MarkScanBufferOverrun()
            Return
        End If
        For Each value In values
            _scanTx.Enqueue(value)
        Next
    End Sub

    Public Shared Function TryGetSet1Code(key As AtPhysicalKey, ByRef code As Byte, ByRef extended As Boolean) As Boolean
        extended = False
        Select Case key
            Case AtPhysicalKey.Escape : code = &H1
            Case AtPhysicalKey.D1 : code = &H2
            Case AtPhysicalKey.D2 : code = &H3
            Case AtPhysicalKey.D3 : code = &H4
            Case AtPhysicalKey.D4 : code = &H5
            Case AtPhysicalKey.D5 : code = &H6
            Case AtPhysicalKey.D6 : code = &H7
            Case AtPhysicalKey.D7 : code = &H8
            Case AtPhysicalKey.D8 : code = &H9
            Case AtPhysicalKey.D9 : code = &HA
            Case AtPhysicalKey.D0 : code = &HB
            Case AtPhysicalKey.Minus : code = &HC
            Case AtPhysicalKey.Equals : code = &HD
            Case AtPhysicalKey.Backspace : code = &HE
            Case AtPhysicalKey.Tab : code = &HF
            Case AtPhysicalKey.Q : code = &H10
            Case AtPhysicalKey.W : code = &H11
            Case AtPhysicalKey.E : code = &H12
            Case AtPhysicalKey.R : code = &H13
            Case AtPhysicalKey.T : code = &H14
            Case AtPhysicalKey.Y : code = &H15
            Case AtPhysicalKey.U : code = &H16
            Case AtPhysicalKey.I : code = &H17
            Case AtPhysicalKey.O : code = &H18
            Case AtPhysicalKey.P : code = &H19
            Case AtPhysicalKey.LeftBracket : code = &H1A
            Case AtPhysicalKey.RightBracket : code = &H1B
            Case AtPhysicalKey.Enter : code = &H1C
            Case AtPhysicalKey.LeftControl : code = &H1D
            Case AtPhysicalKey.A : code = &H1E
            Case AtPhysicalKey.S : code = &H1F
            Case AtPhysicalKey.D : code = &H20
            Case AtPhysicalKey.F : code = &H21
            Case AtPhysicalKey.G : code = &H22
            Case AtPhysicalKey.H : code = &H23
            Case AtPhysicalKey.J : code = &H24
            Case AtPhysicalKey.K : code = &H25
            Case AtPhysicalKey.L : code = &H26
            Case AtPhysicalKey.Semicolon : code = &H27
            Case AtPhysicalKey.Quote : code = &H28
            Case AtPhysicalKey.Grave : code = &H29
            Case AtPhysicalKey.LeftShift : code = &H2A
            Case AtPhysicalKey.Backslash : code = &H2B
            Case AtPhysicalKey.Z : code = &H2C
            Case AtPhysicalKey.X : code = &H2D
            Case AtPhysicalKey.C : code = &H2E
            Case AtPhysicalKey.V : code = &H2F
            Case AtPhysicalKey.B : code = &H30
            Case AtPhysicalKey.N : code = &H31
            Case AtPhysicalKey.M : code = &H32
            Case AtPhysicalKey.Comma : code = &H33
            Case AtPhysicalKey.Period : code = &H34
            Case AtPhysicalKey.Slash : code = &H35
            Case AtPhysicalKey.RightShift : code = &H36
            Case AtPhysicalKey.KeypadMultiply : code = &H37
            Case AtPhysicalKey.LeftAlt : code = &H38
            Case AtPhysicalKey.Space : code = &H39
            Case AtPhysicalKey.CapsLock : code = &H3A
            Case AtPhysicalKey.F1 : code = &H3B
            Case AtPhysicalKey.F2 : code = &H3C
            Case AtPhysicalKey.F3 : code = &H3D
            Case AtPhysicalKey.F4 : code = &H3E
            Case AtPhysicalKey.F5 : code = &H3F
            Case AtPhysicalKey.F6 : code = &H40
            Case AtPhysicalKey.F7 : code = &H41
            Case AtPhysicalKey.F8 : code = &H42
            Case AtPhysicalKey.F9 : code = &H43
            Case AtPhysicalKey.F10 : code = &H44
            Case AtPhysicalKey.NumLock : code = &H45
            Case AtPhysicalKey.ScrollLock : code = &H46
            Case AtPhysicalKey.Keypad7 : code = &H47
            Case AtPhysicalKey.Keypad8 : code = &H48
            Case AtPhysicalKey.Keypad9 : code = &H49
            Case AtPhysicalKey.KeypadSubtract : code = &H4A
            Case AtPhysicalKey.Keypad4 : code = &H4B
            Case AtPhysicalKey.Keypad5 : code = &H4C
            Case AtPhysicalKey.Keypad6 : code = &H4D
            Case AtPhysicalKey.KeypadAdd : code = &H4E
            Case AtPhysicalKey.Keypad1 : code = &H4F
            Case AtPhysicalKey.Keypad2 : code = &H50
            Case AtPhysicalKey.Keypad3 : code = &H51
            Case AtPhysicalKey.Keypad0 : code = &H52
            Case AtPhysicalKey.KeypadDecimal : code = &H53
            Case AtPhysicalKey.F11 : code = &H57
            Case AtPhysicalKey.F12 : code = &H58
            Case AtPhysicalKey.KeypadEnter : code = &H1C : extended = True
            Case AtPhysicalKey.RightControl : code = &H1D : extended = True
            Case AtPhysicalKey.KeypadDivide : code = &H35 : extended = True
            Case AtPhysicalKey.RightAlt : code = &H38 : extended = True
            Case AtPhysicalKey.Home : code = &H47 : extended = True
            Case AtPhysicalKey.Up : code = &H48 : extended = True
            Case AtPhysicalKey.PageUp : code = &H49 : extended = True
            Case AtPhysicalKey.Left : code = &H4B : extended = True
            Case AtPhysicalKey.Right : code = &H4D : extended = True
            Case AtPhysicalKey.EndKey : code = &H4F : extended = True
            Case AtPhysicalKey.Down : code = &H50 : extended = True
            Case AtPhysicalKey.PageDown : code = &H51 : extended = True
            Case AtPhysicalKey.Insert : code = &H52 : extended = True
            Case AtPhysicalKey.Delete : code = &H53 : extended = True
            Case Else : Return False
        End Select
        Return True
    End Function

    Public Shared Function TryGetSet2Code(key As AtPhysicalKey, ByRef code As Byte, ByRef extended As Boolean) As Boolean
        extended = False
        Select Case key
            Case AtPhysicalKey.F9 : code = &H1
            Case AtPhysicalKey.F5 : code = &H3
            Case AtPhysicalKey.F3 : code = &H4
            Case AtPhysicalKey.F1 : code = &H5
            Case AtPhysicalKey.F2 : code = &H6
            Case AtPhysicalKey.F12 : code = &H7
            Case AtPhysicalKey.F10 : code = &H9
            Case AtPhysicalKey.F8 : code = &HA
            Case AtPhysicalKey.F6 : code = &HB
            Case AtPhysicalKey.F4 : code = &HC
            Case AtPhysicalKey.Tab : code = &HD
            Case AtPhysicalKey.Grave : code = &HE
            Case AtPhysicalKey.LeftAlt : code = &H11
            Case AtPhysicalKey.LeftShift : code = &H12
            Case AtPhysicalKey.LeftControl : code = &H14
            Case AtPhysicalKey.Q : code = &H15
            Case AtPhysicalKey.D1 : code = &H16
            Case AtPhysicalKey.Z : code = &H1A
            Case AtPhysicalKey.S : code = &H1B
            Case AtPhysicalKey.A : code = &H1C
            Case AtPhysicalKey.W : code = &H1D
            Case AtPhysicalKey.D2 : code = &H1E
            Case AtPhysicalKey.C : code = &H21
            Case AtPhysicalKey.X : code = &H22
            Case AtPhysicalKey.D : code = &H23
            Case AtPhysicalKey.E : code = &H24
            Case AtPhysicalKey.D4 : code = &H25
            Case AtPhysicalKey.D3 : code = &H26
            Case AtPhysicalKey.Space : code = &H29
            Case AtPhysicalKey.V : code = &H2A
            Case AtPhysicalKey.F : code = &H2B
            Case AtPhysicalKey.T : code = &H2C
            Case AtPhysicalKey.R : code = &H2D
            Case AtPhysicalKey.D5 : code = &H2E
            Case AtPhysicalKey.N : code = &H31
            Case AtPhysicalKey.B : code = &H32
            Case AtPhysicalKey.H : code = &H33
            Case AtPhysicalKey.G : code = &H34
            Case AtPhysicalKey.Y : code = &H35
            Case AtPhysicalKey.D6 : code = &H36
            Case AtPhysicalKey.M : code = &H3A
            Case AtPhysicalKey.J : code = &H3B
            Case AtPhysicalKey.U : code = &H3C
            Case AtPhysicalKey.D7 : code = &H3D
            Case AtPhysicalKey.D8 : code = &H3E
            Case AtPhysicalKey.Comma : code = &H41
            Case AtPhysicalKey.K : code = &H42
            Case AtPhysicalKey.I : code = &H43
            Case AtPhysicalKey.O : code = &H44
            Case AtPhysicalKey.D0 : code = &H45
            Case AtPhysicalKey.D9 : code = &H46
            Case AtPhysicalKey.Period : code = &H49
            Case AtPhysicalKey.Slash : code = &H4A
            Case AtPhysicalKey.L : code = &H4B
            Case AtPhysicalKey.Semicolon : code = &H4C
            Case AtPhysicalKey.P : code = &H4D
            Case AtPhysicalKey.Minus : code = &H4E
            Case AtPhysicalKey.Quote : code = &H52
            Case AtPhysicalKey.LeftBracket : code = &H54
            Case AtPhysicalKey.Equals : code = &H55
            Case AtPhysicalKey.CapsLock : code = &H58
            Case AtPhysicalKey.RightShift : code = &H59
            Case AtPhysicalKey.Enter : code = &H5A
            Case AtPhysicalKey.RightBracket : code = &H5B
            Case AtPhysicalKey.Backslash : code = &H5D
            Case AtPhysicalKey.Backspace : code = &H66
            Case AtPhysicalKey.Keypad1 : code = &H69
            Case AtPhysicalKey.Keypad4 : code = &H6B
            Case AtPhysicalKey.Keypad7 : code = &H6C
            Case AtPhysicalKey.Keypad0 : code = &H70
            Case AtPhysicalKey.KeypadDecimal : code = &H71
            Case AtPhysicalKey.Keypad2 : code = &H72
            Case AtPhysicalKey.Keypad5 : code = &H73
            Case AtPhysicalKey.Keypad6 : code = &H74
            Case AtPhysicalKey.Keypad8 : code = &H75
            Case AtPhysicalKey.Escape : code = &H76
            Case AtPhysicalKey.NumLock : code = &H77
            Case AtPhysicalKey.F11 : code = &H78
            Case AtPhysicalKey.KeypadAdd : code = &H79
            Case AtPhysicalKey.Keypad3 : code = &H7A
            Case AtPhysicalKey.KeypadSubtract : code = &H7B
            Case AtPhysicalKey.KeypadMultiply : code = &H7C
            Case AtPhysicalKey.Keypad9 : code = &H7D
            Case AtPhysicalKey.ScrollLock : code = &H7E
            Case AtPhysicalKey.F7 : code = &H83
            Case AtPhysicalKey.RightAlt : code = &H11 : extended = True
            Case AtPhysicalKey.RightControl : code = &H14 : extended = True
            Case AtPhysicalKey.KeypadDivide : code = &H4A : extended = True
            Case AtPhysicalKey.KeypadEnter : code = &H5A : extended = True
            Case AtPhysicalKey.EndKey : code = &H69 : extended = True
            Case AtPhysicalKey.Left : code = &H6B : extended = True
            Case AtPhysicalKey.Home : code = &H6C : extended = True
            Case AtPhysicalKey.Insert : code = &H70 : extended = True
            Case AtPhysicalKey.Delete : code = &H71 : extended = True
            Case AtPhysicalKey.Down : code = &H72 : extended = True
            Case AtPhysicalKey.Right : code = &H74 : extended = True
            Case AtPhysicalKey.Up : code = &H75 : extended = True
            Case AtPhysicalKey.PageDown : code = &H7A : extended = True
            Case AtPhysicalKey.PageUp : code = &H7D : extended = True
            Case Else : Return False
        End Select
        Return True
    End Function

    Public Shared Function TryGetSet3Code(key As AtPhysicalKey, ByRef code As Byte) As Boolean
        Select Case key
            Case AtPhysicalKey.F1 : code = &H7
            Case AtPhysicalKey.Escape : code = &H8
            Case AtPhysicalKey.Tab : code = &HD
            Case AtPhysicalKey.Grave : code = &HE
            Case AtPhysicalKey.F2 : code = &HF
            Case AtPhysicalKey.LeftControl : code = &H11
            Case AtPhysicalKey.LeftShift : code = &H12
            Case AtPhysicalKey.CapsLock : code = &H14
            Case AtPhysicalKey.Q : code = &H15
            Case AtPhysicalKey.D1 : code = &H16
            Case AtPhysicalKey.F3 : code = &H17
            Case AtPhysicalKey.LeftAlt : code = &H19
            Case AtPhysicalKey.Z : code = &H1A
            Case AtPhysicalKey.S : code = &H1B
            Case AtPhysicalKey.A : code = &H1C
            Case AtPhysicalKey.W : code = &H1D
            Case AtPhysicalKey.D2 : code = &H1E
            Case AtPhysicalKey.F4 : code = &H1F
            Case AtPhysicalKey.C : code = &H21
            Case AtPhysicalKey.X : code = &H22
            Case AtPhysicalKey.D : code = &H23
            Case AtPhysicalKey.E : code = &H24
            Case AtPhysicalKey.D4 : code = &H25
            Case AtPhysicalKey.D3 : code = &H26
            Case AtPhysicalKey.F5 : code = &H27
            Case AtPhysicalKey.Space : code = &H29
            Case AtPhysicalKey.V : code = &H2A
            Case AtPhysicalKey.F : code = &H2B
            Case AtPhysicalKey.T : code = &H2C
            Case AtPhysicalKey.R : code = &H2D
            Case AtPhysicalKey.D5 : code = &H2E
            Case AtPhysicalKey.F6 : code = &H2F
            Case AtPhysicalKey.N : code = &H31
            Case AtPhysicalKey.B : code = &H32
            Case AtPhysicalKey.H : code = &H33
            Case AtPhysicalKey.G : code = &H34
            Case AtPhysicalKey.Y : code = &H35
            Case AtPhysicalKey.D6 : code = &H36
            Case AtPhysicalKey.F7 : code = &H37
            Case AtPhysicalKey.RightAlt : code = &H39
            Case AtPhysicalKey.M : code = &H3A
            Case AtPhysicalKey.J : code = &H3B
            Case AtPhysicalKey.U : code = &H3C
            Case AtPhysicalKey.D7 : code = &H3D
            Case AtPhysicalKey.D8 : code = &H3E
            Case AtPhysicalKey.F8 : code = &H3F
            Case AtPhysicalKey.Comma : code = &H41
            Case AtPhysicalKey.K : code = &H42
            Case AtPhysicalKey.I : code = &H43
            Case AtPhysicalKey.O : code = &H44
            Case AtPhysicalKey.D0 : code = &H45
            Case AtPhysicalKey.D9 : code = &H46
            Case AtPhysicalKey.F9 : code = &H47
            Case AtPhysicalKey.Period : code = &H49
            Case AtPhysicalKey.Slash : code = &H4A
            Case AtPhysicalKey.L : code = &H4B
            Case AtPhysicalKey.Semicolon : code = &H4C
            Case AtPhysicalKey.P : code = &H4D
            Case AtPhysicalKey.Minus : code = &H4E
            Case AtPhysicalKey.F10 : code = &H4F
            Case AtPhysicalKey.Quote : code = &H52
            Case AtPhysicalKey.LeftBracket : code = &H54
            Case AtPhysicalKey.Equals : code = &H55
            Case AtPhysicalKey.F11 : code = &H56
            Case AtPhysicalKey.PrintScreen : code = &H57
            Case AtPhysicalKey.RightControl : code = &H58
            Case AtPhysicalKey.RightShift : code = &H59
            Case AtPhysicalKey.Enter : code = &H5A
            Case AtPhysicalKey.RightBracket : code = &H5B
            Case AtPhysicalKey.Backslash : code = &H5C
            Case AtPhysicalKey.F12 : code = &H5E
            Case AtPhysicalKey.ScrollLock : code = &H5F
            Case AtPhysicalKey.Down : code = &H60
            Case AtPhysicalKey.Left : code = &H61
            Case AtPhysicalKey.Pause : code = &H62
            Case AtPhysicalKey.Up : code = &H63
            Case AtPhysicalKey.Delete : code = &H64
            Case AtPhysicalKey.EndKey : code = &H65
            Case AtPhysicalKey.Backspace : code = &H66
            Case AtPhysicalKey.Insert : code = &H67
            Case AtPhysicalKey.Keypad1 : code = &H69
            Case AtPhysicalKey.Right : code = &H6A
            Case AtPhysicalKey.Keypad4 : code = &H6B
            Case AtPhysicalKey.Keypad7 : code = &H6C
            Case AtPhysicalKey.PageDown : code = &H6D
            Case AtPhysicalKey.Home : code = &H6E
            Case AtPhysicalKey.PageUp : code = &H6F
            Case AtPhysicalKey.Keypad0 : code = &H70
            Case AtPhysicalKey.KeypadDecimal : code = &H71
            Case AtPhysicalKey.Keypad2 : code = &H72
            Case AtPhysicalKey.Keypad5 : code = &H73
            Case AtPhysicalKey.Keypad6 : code = &H74
            Case AtPhysicalKey.Keypad8 : code = &H75
            Case AtPhysicalKey.NumLock : code = &H76
            Case AtPhysicalKey.KeypadDivide : code = &H77
            Case AtPhysicalKey.KeypadEnter : code = &H79
            Case AtPhysicalKey.Keypad3 : code = &H7A
            Case AtPhysicalKey.KeypadAdd : code = &H7C
            Case AtPhysicalKey.Keypad9 : code = &H7D
            Case AtPhysicalKey.KeypadMultiply : code = &H7E
            Case AtPhysicalKey.KeypadSubtract : code = &H84
            Case Else : Return False
        End Select
        Return True
    End Function

    Private Shared Function TryKeyFromSet3Code(code As Byte, ByRef key As AtPhysicalKey) As Boolean
        For Each candidate As AtPhysicalKey In [Enum].GetValues(GetType(AtPhysicalKey))
            Dim candidateCode As Byte
            If candidate <> AtPhysicalKey.None AndAlso TryGetSet3Code(candidate, candidateCode) AndAlso candidateCode = code Then
                key = candidate
                Return True
            End If
        Next
        key = AtPhysicalKey.None
        Return False
    End Function
End Class
