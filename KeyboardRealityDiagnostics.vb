Imports System.Text

' CROMWELL KEYBOARD REALITY BRICK 4 DIAGNOSTICS
' Host-only diagnostic exerciser for the physical AT keyboard/8042 model.
' It constructs an isolated keyboard, 8042 and PIC; no guest RAM, BIOS state,
' mounted media, or running machine hardware is changed.
Public NotInheritable Class KeyboardRealityDiagnostics
    Private Const OneMillisecondPicoseconds As Long = 1000000000L

    Private Sub New()
    End Sub

    Public Shared Function RunAll() As String
        Dim report As New StringBuilder()
        report.AppendLine("Cromwell Technologies AT keyboard hardware self-test")
        report.AppendLine("Isolated device test - running guest machine is untouched")
        report.AppendLine()

        Try
            Dim pic As New Pic8259()
            Dim keyboard As New AtKeyboard101()
            Dim controller As New KeyboardController8042(pic, keyboard)

            Advance(controller, 510)
            ExpectByte(controller, &HAA, "power-on keyboard BAT", report)

            WriteCommand(controller, &HAA)
            ExpectByte(controller, &H55, "8042 controller self-test", report)

            WriteCommand(controller, &HAB)
            ExpectByte(controller, &H0, "8042 keyboard-interface test", report)

            ' Configuration mode: system flag set, IRQ1 off, translation off.
            WriteCommand(controller, &H60)
            WriteData(controller, &H4)
            WriteCommand(controller, &HAE)
            Expect(controller.KeyboardInterfaceEnabled, "keyboard interface enabled", report)

            ' Reset ACK is not the reset itself.  BAT begins only after the CPU
            ' consumes ACK and the keyboard observes at least 500 us idle-high.
            WriteData(controller, &HFF)
            ExpectByte(controller, &HFA, "keyboard reset ACK", report)
            Expect(Not keyboard.BasicAssuranceTestActive, "reset waits for ACK acceptance interval", report)
            Advance(controller, 1)
            Expect(keyboard.BasicAssuranceTestActive, "BAT begins after reset acceptance interval", report)
            Advance(controller, 501)
            ExpectByte(controller, &HAA, "keyboard reset BAT completion", report)

            SendKeyboardAck(controller, &HF5, "Default Disable", report)
            Expect(Not keyboard.ScanningEnabled, "F5 stops scanning", report)

            SendKeyboardAck(controller, &HF6, "Set Default while disabled", report)
            Expect(Not keyboard.ScanningEnabled, "F6 preserves disabled scanning state", report)

            SendKeyboardAck(controller, &HF4, "Enable scanning", report)
            Expect(keyboard.ScanningEnabled, "F4 enables scanning", report)

            SendKeyboardAck(controller, &HF2, "Read ID", report)
            ExpectByte(controller, &HAB, "Enhanced Keyboard ID byte 1", report)
            ExpectByte(controller, &H83, "Enhanced Keyboard ID byte 2", report)

            SendKeyboardAck(controller, &HF0, "Select scan set", report)
            SendKeyboardAck(controller, &H2, "Select scan set 2", report)
            Expect(keyboard.ScanCodeSet = 2, "keyboard reports scan set 2", report)

            SendKeyboardAck(controller, &HF0, "Query scan set command", report)
            SendKeyboardAck(controller, &H0, "Query scan set parameter", report)
            ExpectByte(controller, &H2, "scan-set query result", report)

            SendKeyboardAck(controller, &HF3, "Set typematic command", report)
            SendKeyboardAck(controller, &H2B, "Set default typematic byte", report)
            Expect(keyboard.TypematicByte = &H2B, "typematic byte latched", report)

            SendKeyboardAck(controller, &HED, "Set LEDs command", report)
            SendKeyboardAck(controller, &H7, "Set all keyboard LEDs", report)
            Expect(keyboard.LedState = 7, "keyboard LED latch = Scroll+Num+Caps", report)

            SendKeyboardAck(controller, &HED, "Clear LEDs command", report)
            SendKeyboardAck(controller, &H0, "Clear keyboard LEDs", report)
            Expect(keyboard.LedState = 0, "keyboard LED latch cleared", report)

            ' Prove raw Set-2 output before enabling 8042 compatibility translation.
            keyboard.SetPhysicalKey(AtPhysicalKey.Enter, True)
            ExpectByte(controller, &H5A, "raw Set-2 Enter make", report)
            keyboard.SetPhysicalKey(AtPhysicalKey.Enter, False)
            ExpectSequence(controller, New Integer() {&HF0, &H5A}, "raw Set-2 Enter break", report)

            ' Set 3 selection plus programmable key behavior.
            SendKeyboardAck(controller, &HF0, "Select scan set command", report)
            SendKeyboardAck(controller, &H3, "Select scan set 3", report)
            Expect(keyboard.ScanCodeSet = 3, "keyboard reports scan set 3", report)

            SendKeyboardAck(controller, &HF8, "Set all keys make/break", report)
            Dim set3A As Byte
            Expect(AtKeyboard101.TryGetSet3Code(AtPhysicalKey.A, set3A), "Set-3 A code exists", report)
            keyboard.SetPhysicalKey(AtPhysicalKey.A, True)
            ExpectByte(controller, set3A, "Set-3 A make", report)
            keyboard.SetPhysicalKey(AtPhysicalKey.A, False)
            ExpectSequence(controller, New Integer() {&HF0, set3A}, "Set-3 A break", report)

            SendKeyboardAck(controller, &HF6, "Restore keyboard defaults", report)
            Expect(keyboard.ScanCodeSet = 2, "F6 restores scan set 2 default", report)
            Expect(keyboard.ScanningEnabled, "F6 preserves enabled scanning state", report)

            ' Enable AT compatibility translation and IRQ1.
            WriteCommand(controller, &H60)
            WriteData(controller, &H45)
            WriteCommand(controller, &HAE)
            Expect(controller.TranslationEnabled, "8042 Set-2 to Set-1 translation enabled", report)

            ' Command responses must bypass translation even though 83h is itself
            ' a valid Set-2 make code.
            SendKeyboardAck(controller, &HF2, "Read ID with translation enabled", report)
            ExpectByte(controller, &HAB, "translated-mode ID byte 1 bypass", report)
            ExpectByte(controller, &H83, "translated-mode ID byte 2 bypass", report)

            TestTranslatedKey(controller, keyboard, AtPhysicalKey.Escape, &H1, "Escape", report)
            TestTranslatedKey(controller, keyboard, AtPhysicalKey.Enter, &H1C, "main Enter", report)
            TestTranslatedKey(controller, keyboard, AtPhysicalKey.Period, &H34, "period", report)
            TestTranslatedKey(controller, keyboard, AtPhysicalKey.Slash, &H35, "slash", report)

            keyboard.SetPhysicalKey(AtPhysicalKey.RightControl, True)
            ExpectSequence(controller, New Integer() {&HE0, &H1D}, "right Ctrl make", report)
            keyboard.SetPhysicalKey(AtPhysicalKey.RightControl, False)
            ExpectSequence(controller, New Integer() {&HE0, &H9D}, "right Ctrl break", report)

            keyboard.SetPhysicalKey(AtPhysicalKey.KeypadEnter, True)
            ExpectSequence(controller, New Integer() {&HE0, &H1C}, "keypad Enter make", report)
            keyboard.SetPhysicalKey(AtPhysicalKey.KeypadEnter, False)
            ExpectSequence(controller, New Integer() {&HE0, &H9C}, "keypad Enter break", report)

            keyboard.SetPhysicalKey(AtPhysicalKey.PrintScreen, True)
            ExpectSequence(controller, New Integer() {&HE0, &H2A, &HE0, &H37}, "PrintScreen make", report)
            keyboard.SetPhysicalKey(AtPhysicalKey.PrintScreen, False)
            ExpectSequence(controller, New Integer() {&HE0, &HB7, &HE0, &HAA}, "PrintScreen break", report)

            keyboard.SetPhysicalKey(AtPhysicalKey.Pause, True)
            keyboard.SetPhysicalKey(AtPhysicalKey.Pause, False)
            ExpectSequence(controller, New Integer() {&HE1, &H1D, &H45, &HE1, &H9D, &HC5}, "Pause make sequence", report)

            keyboard.SetPhysicalKey(AtPhysicalKey.A, True)
            ExpectByte(controller, &H1E, "typematic A initial make", report)
            Advance(controller, 520)
            ExpectByte(controller, &H1E, "typematic A repeat after programmed delay", report)
            keyboard.SetPhysicalKey(AtPhysicalKey.A, False)
            ExpectByte(controller, &H9E, "typematic A break", report)

            ' OBF back-pressure holds the serial clock low, but it must not stop
            ' the keyboard's own typematic clock.  Leave the initial make in OBF,
            ' advance beyond the delay, then prove a repeat was buffered behind it.
            keyboard.SetPhysicalKey(AtPhysicalKey.B, True)
            Advance(controller, 700)
            ExpectByte(controller, &H30, "back-pressure B initial make", report)
            ExpectByte(controller, &H30, "back-pressure B buffered repeat", report)
            keyboard.SetPhysicalKey(AtPhysicalKey.B, False)
            ExpectEventuallyByte(controller, &HB0, 16, "back-pressure B break after buffered repeats", report)

            ' Commands which temporarily/default-disable scanning must restore a
            ' typematic owner when scanning resumes and the physical key is held.
            keyboard.SetPhysicalKey(AtPhysicalKey.C, True)
            ExpectByte(controller, &H2E, "held C initial make", report)
            SendKeyboardAck(controller, &HF5, "Default Disable while C held", report)
            SendKeyboardAck(controller, &HF4, "Enable scanning while C held", report)
            Advance(controller, 520)
            ExpectByte(controller, &H2E, "held C repeat after Enable", report)
            SendKeyboardAck(controller, &HF6, "Set Defaults while C held", report)
            Advance(controller, 520)
            ExpectByte(controller, &H2E, "held C repeat after Set Defaults", report)
            keyboard.SetPhysicalKey(AtPhysicalKey.C, False)
            ExpectByte(controller, &HAE, "held C break", report)

            WriteCommand(controller, &H20)
            ExpectByte(controller, &H45, "read 8042 command byte", report)

            WriteCommand(controller, &HD1)
            WriteData(controller, &H3)
            WriteCommand(controller, &HD0)
            ExpectByte(controller, &H3, "8042 output-port/A20 latch readback", report)

            ' EE echoes itself; FE repeats the previous keyboard output byte.
            WriteData(controller, &HEE)
            ExpectByte(controller, &HEE, "keyboard diagnostic Echo", report)
            WriteData(controller, &HFE)
            ExpectByte(controller, &HEE, "keyboard Resend", report)

            report.AppendLine()
            report.AppendLine("Electrical/protocol fault paths:")

            ' Receive parity: first failure is automatically retried by sending
            ' FEh to the keyboard.  The keyboard must resend the original scan
            ' byte with its original scan/response classification.
            Dim faultPic As New Pic8259()
            Dim faultKeyboard As New AtKeyboard101()
            Dim faultController As New KeyboardController8042(faultPic, faultKeyboard)
            Advance(faultController, 510)
            ExpectByte(faultController, &HAA, "fault rig power-on BAT", report)
            WriteCommand(faultController, &H60)
            WriteData(faultController, &H45)
            WriteCommand(faultController, &HAE)

            faultController.DiagnosticCorruptNextKeyboardFrames(1)
            faultKeyboard.SetPhysicalKey(AtPhysicalKey.A, True)
            ExpectByte(faultController, &H1E, "keyboard receive parity auto-Resend recovery", report)
            Expect(faultController.KeyboardReceiveParityRetries = 1UL,
                   "receive parity retry counter", report)
            Expect((faultController.StatusRegister And &H80) = 0,
                   "recovered parity error does not become a final status error", report)
            faultKeyboard.SetPhysicalKey(AtPhysicalKey.A, False)
            ExpectByte(faultController, &H9E, "post-retry A break", report)

            ' A second parity failure on the resent scan byte exhausts the retry
            ' and reports FFh in compatibility mode.
            faultController.DiagnosticCorruptNextKeyboardFrames(2)
            faultKeyboard.SetPhysicalKey(AtPhysicalKey.B, True)
            ExpectByte(faultController, &HFF, "persistent receive parity error byte", report)
            Expect((faultController.StatusRegister And &H80) <> 0,
                   "persistent receive parity sets status bit 7", report)

            ' A fresh controller in default mode reports 00h instead of FFh.
            faultPic = New Pic8259()
            faultKeyboard = New AtKeyboard101()
            faultController = New KeyboardController8042(faultPic, faultKeyboard)
            Advance(faultController, 510)
            ExpectByte(faultController, &HAA, "default-mode fault rig BAT", report)
            WriteCommand(faultController, &H60)
            WriteData(faultController, &H4)
            WriteCommand(faultController, &HAE)
            faultController.DiagnosticCorruptNextKeyboardFrames(2)
            faultKeyboard.SetPhysicalKey(AtPhysicalKey.C, True)
            ExpectByte(faultController, &H0, "default-mode receive error byte is 00h", report)

            ' Receive timeout is a 2 ms wire timeout and is not retried.
            faultPic = New Pic8259()
            faultKeyboard = New AtKeyboard101()
            faultController = New KeyboardController8042(faultPic, faultKeyboard)
            Advance(faultController, 510)
            ExpectByte(faultController, &HAA, "receive-timeout rig BAT", report)
            WriteCommand(faultController, &H60)
            WriteData(faultController, &H45)
            WriteCommand(faultController, &HAE)
            faultController.DiagnosticStallNextKeyboardFrame()
            faultKeyboard.SetPhysicalKey(AtPhysicalKey.D, True)
            Advance(faultController, 3)
            ExpectByte(faultController, &HFF, "2 ms keyboard receive timeout byte", report)
            Expect((faultController.StatusRegister And &H40) <> 0,
                   "receive timeout sets status bit 6", report)

            ' If the keyboard response to a host command has bad parity, IBM AT
            ' firmware returns FEh and sets transmit-timeout + parity, with no retry.
            faultPic = New Pic8259()
            faultKeyboard = New AtKeyboard101()
            faultController = New KeyboardController8042(faultPic, faultKeyboard)
            Advance(faultController, 510)
            ExpectByte(faultController, &HAA, "response-parity rig BAT", report)
            WriteCommand(faultController, &H60)
            WriteData(faultController, &H4)
            WriteCommand(faultController, &HAE)
            faultController.DiagnosticCorruptNextKeyboardResponses(1)
            WriteData(faultController, &HED)
            ExpectByte(faultController, &HFE, "command-response parity error returns FEh", report)
            Expect((faultController.StatusRegister And &HA0) = &HA0,
                   "response parity sets transmit-timeout + parity", report)

            ' If no keyboard response arrives within 25 ms, both timeout bits set.
            faultPic = New Pic8259()
            faultKeyboard = New AtKeyboard101()
            faultController = New KeyboardController8042(faultPic, faultKeyboard)
            Advance(faultController, 510)
            ExpectByte(faultController, &HAA, "response-timeout rig BAT", report)
            WriteCommand(faultController, &H60)
            WriteData(faultController, &H4)
            WriteCommand(faultController, &HAE)
            faultController.DiagnosticDropNextKeyboardResponses(1)
            WriteData(faultController, &HED)
            Advance(faultController, 26)
            ExpectByte(faultController, &HFE, "25 ms keyboard response timeout returns FEh", report)
            Expect((faultController.StatusRegister And &H60) = &H60,
                   "response timeout sets transmit + receive timeout bits", report)

            ' A keyboard that never begins clocking a controller transmission
            ' causes the 15 ms transmit-start timeout and FEh.
            faultPic = New Pic8259()
            faultKeyboard = New AtKeyboard101()
            faultController = New KeyboardController8042(faultPic, faultKeyboard)
            Advance(faultController, 510)
            ExpectByte(faultController, &HAA, "transmit-start-timeout rig BAT", report)
            WriteCommand(faultController, &H60)
            WriteData(faultController, &H4)
            WriteCommand(faultController, &HAE)
            faultController.DiagnosticStallNextControllerStart()
            WriteData(faultController, &HED)
            Advance(faultController, 16)
            ExpectByte(faultController, &HFE, "15 ms controller transmit-start timeout", report)
            Expect((faultController.StatusRegister And &H20) <> 0 AndAlso
                   (faultController.StatusRegister And &H40) = 0,
                   "transmit-start timeout sets only status bit 5", report)

            ' Once controller transmission has started, failure to finish
            ' clocking the frame within 2 ms is also a transmit timeout.
            faultPic = New Pic8259()
            faultKeyboard = New AtKeyboard101()
            faultController = New KeyboardController8042(faultPic, faultKeyboard)
            Advance(faultController, 510)
            ExpectByte(faultController, &HAA, "transmit-frame-timeout rig BAT", report)
            WriteCommand(faultController, &H60)
            WriteData(faultController, &H4)
            WriteCommand(faultController, &HAE)
            faultController.DiagnosticStallNextControllerFrame()
            WriteData(faultController, &HED)
            Advance(faultController, 3)
            ExpectByte(faultController, &HFE, "2 ms controller transmit-frame timeout", report)
            Expect((faultController.StatusRegister And &H20) <> 0 AndAlso
                   (faultController.StatusRegister And &H40) = 0,
                   "transmit-frame timeout sets only status bit 5", report)

            ' Corrupting a system-to-keyboard frame is detected by the keyboard,
            ' which requests Resend with FEh.  The 8042 does not magically decode
            ' its own bad transmission.
            faultPic = New Pic8259()
            faultKeyboard = New AtKeyboard101()
            faultController = New KeyboardController8042(faultPic, faultKeyboard)
            Advance(faultController, 510)
            ExpectByte(faultController, &HAA, "keyboard parity-reject rig BAT", report)
            WriteCommand(faultController, &H60)
            WriteData(faultController, &H4)
            WriteCommand(faultController, &HAE)
            faultController.DiagnosticCorruptNextControllerFrames(1)
            WriteData(faultController, &HED)
            ExpectByte(faultController, &HFE, "keyboard rejects bad host parity with Resend", report)

            report.AppendLine()
            report.AppendLine("RESULT: PASS")
            report.AppendLine("No application-specific compatibility path was exercised.")
        Catch ex As Exception
            report.AppendLine()
            report.AppendLine("RESULT: FAIL")
            report.AppendLine(ex.Message)
        End Try

        Return report.ToString()
    End Function

    Private Shared Sub TestTranslatedKey(controller As KeyboardController8042,
                                         keyboard As AtKeyboard101,
                                         key As AtPhysicalKey,
                                         makeCode As Byte,
                                         label As String,
                                         report As StringBuilder)
        keyboard.SetPhysicalKey(key, True)
        ExpectByte(controller, makeCode, label & " make", report)
        keyboard.SetPhysicalKey(key, False)
        ExpectByte(controller, CByte(makeCode Or &H80), label & " break", report)
    End Sub

    Private Shared Sub SendKeyboardAck(controller As KeyboardController8042,
                                       value As Byte,
                                       label As String,
                                       report As StringBuilder)
        WriteData(controller, value)
        ExpectByte(controller, &HFA, label & " ACK", report)
    End Sub

    Private Shared Sub WriteCommand(controller As KeyboardController8042, value As Byte)
        WaitInputClear(controller, 50)
        controller.WritePort(&H64US, value)
        Advance(controller, 1)
        WaitInputClear(controller, 50)
    End Sub

    Private Shared Sub WriteData(controller As KeyboardController8042, value As Byte)
        WaitInputClear(controller, 50)
        controller.WritePort(&H60US, value)
        Advance(controller, 1)
        WaitInputClear(controller, 50)
    End Sub

    Private Shared Sub WaitInputClear(controller As KeyboardController8042, timeoutMilliseconds As Integer)
        For i As Integer = 0 To timeoutMilliseconds
            If (controller.ReadPort(&H64US) And &H2) = 0 Then Return
            Advance(controller, 1)
        Next
        Throw New InvalidOperationException("Timed out waiting for 8042 input buffer to clear.")
    End Sub

    Private Shared Function ReadNext(controller As KeyboardController8042,
                                     timeoutMilliseconds As Integer) As Byte
        For i As Integer = 0 To timeoutMilliseconds
            If (controller.ReadPort(&H64US) And &H1) <> 0 Then Return controller.ReadPort(&H60US)
            Advance(controller, 1)
        Next
        Throw New InvalidOperationException("Timed out waiting for a byte from port 60h.")
    End Function

    Private Shared Sub ExpectByte(controller As KeyboardController8042,
                                  expected As Integer,
                                  label As String,
                                  report As StringBuilder)
        Dim actual As Byte = ReadNext(controller, 1200)
        If actual <> CByte(expected And &HFF) Then
            Throw New InvalidOperationException(label & ": expected " &
                                                (expected And &HFF).ToString("X2") & "h, got " &
                                                actual.ToString("X2") & "h.")
        End If
        report.AppendLine("PASS  " & label)
    End Sub

    Private Shared Sub ExpectSequence(controller As KeyboardController8042,
                                      expected As Integer(),
                                      label As String,
                                      report As StringBuilder)
        For i As Integer = 0 To expected.Length - 1
            Dim actual As Byte = ReadNext(controller, 1200)
            Dim wanted As Byte = CByte(expected(i) And &HFF)
            If actual <> wanted Then
                Throw New InvalidOperationException(label & " byte " & (i + 1).ToString() &
                                                    ": expected " & wanted.ToString("X2") &
                                                    "h, got " & actual.ToString("X2") & "h.")
            End If
        Next
        report.AppendLine("PASS  " & label)
    End Sub

    Private Shared Sub ExpectEventuallyByte(controller As KeyboardController8042,
                                            expected As Integer,
                                            maximumBytes As Integer,
                                            label As String,
                                            report As StringBuilder)
        Dim wanted As Byte = CByte(expected And &HFF)
        For index As Integer = 1 To maximumBytes
            Dim actual As Byte = ReadNext(controller, 1200)
            If actual = wanted Then
                report.AppendLine("PASS  " & label)
                Return
            End If
        Next
        Throw New InvalidOperationException(label & ": expected " & wanted.ToString("X2") &
                                            "h within " & maximumBytes.ToString() & " bytes.")
    End Sub

    Private Shared Sub Expect(condition As Boolean, label As String, report As StringBuilder)
        If Not condition Then Throw New InvalidOperationException(label & ": condition was false.")
        report.AppendLine("PASS  " & label)
    End Sub

    Private Shared Sub Advance(controller As KeyboardController8042, milliseconds As Integer)
        For i As Integer = 1 To milliseconds
            controller.AdvanceTime(OneMillisecondPicoseconds)
        Next
    End Sub
End Class
