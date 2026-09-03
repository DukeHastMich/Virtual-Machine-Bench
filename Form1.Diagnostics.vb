Imports System.IO
Imports System.Text

' Host diagnostic viewers and report composition for Form1. Kept in a partial
' class during the first boundary-cleanup pass so behavior remains unchanged;
' these methods can later consume immutable machine snapshots.
Partial Public Class Form1
    Private Sub ShowKeyboardLiveState()
        Dim text As String =
            ReadMachineInBed(
                Function() As String
                    Dim status As Byte = KeyboardController.StatusRegister
                    Dim leds As Byte = KeyboardController.KeyboardLedState

                    Return _
                        "IBM AT keyboard / 8042 live state" & Environment.NewLine & Environment.NewLine &
                        "8042 status 64h: " & status.ToString("X2") & "h" & Environment.NewLine &
                        "  OBF: " & If((status And &H1) <> 0, "full", "empty") & Environment.NewLine &
                        "  IBF: " & If((status And &H2) <> 0, "full", "empty") & Environment.NewLine &
                        "  System flag: " & If((status And &H4) <> 0, "set", "clear") & Environment.NewLine &
                        "  Command/Data: " & If((status And &H8) <> 0, "command", "data") & Environment.NewLine &
                        "  Keyboard inhibit switch: " & If((status And &H10) <> 0, "not inhibited", "inhibited") & Environment.NewLine &
                        "  TX timeout: " & If((status And &H20) <> 0, "set", "clear") & Environment.NewLine &
                        "  RX timeout: " & If((status And &H40) <> 0, "set", "clear") & Environment.NewLine &
                        "  Parity error: " & If((status And &H80) <> 0, "set", "clear") & Environment.NewLine & Environment.NewLine &
                        "Keyboard interface: " & If(KeyboardController.KeyboardInterfaceEnabled, "enabled", "disabled") & Environment.NewLine &
                        "Keyboard serial link: " & If(KeyboardController.KeyboardLinkBusy, "busy", "idle") & Environment.NewLine &
                        "Keyboard scan set: " & KeyboardController.KeyboardScanCodeSet.ToString() & Environment.NewLine &
                        "8042 translation: " & If(KeyboardController.TranslationEnabled, "enabled", "disabled") & Environment.NewLine &
                        "Keyboard scanning: " & If(AtKeyboard.ScanningEnabled, "enabled", "disabled") & Environment.NewLine &
                        "BAT active: " & If(AtKeyboard.BasicAssuranceTestActive, "yes", "no") & Environment.NewLine &
                        "Pressed physical keys: " & AtKeyboard.PressedKeyCount.ToString() & Environment.NewLine &
                        "Keyboard pending bytes: " & AtKeyboard.PendingTransmitByteCount.ToString() & Environment.NewLine &
                        "Typematic byte: " & KeyboardController.TypematicByte.ToString("X2") & "h" & Environment.NewLine &
                        "LEDs: Caps=" & If((leds And 4) <> 0, "ON", "off") &
                        " Num=" & If((leds And 2) <> 0, "ON", "off") &
                        " Scroll=" & If((leds And 1) <> 0, "ON", "off") & Environment.NewLine &
                        "Frames system->keyboard: " & KeyboardController.KeyboardFramesTransmitted.ToString() & Environment.NewLine &
                        "Frames keyboard->system: " & KeyboardController.KeyboardFramesReceived.ToString() & Environment.NewLine &
                        "Recovered receive parity retries: " & KeyboardController.KeyboardReceiveParityRetries.ToString() & Environment.NewLine &
                        "Final keyboard receive errors: " & KeyboardController.KeyboardReceiveErrors.ToString() & Environment.NewLine &
                        "Keyboard transmit/response errors: " & KeyboardController.KeyboardTransmitErrors.ToString() & Environment.NewLine &
                        "Port 60h reads: " & KeyboardController.Port60ReadCount.ToString() & Environment.NewLine &
                        "Port 64h status reads: " & KeyboardController.Port64ReadCount.ToString() & Environment.NewLine &
                        "IRQ1 assertions: " & KeyboardController.Irq1AssertionCount.ToString()
                End Function)

        MessageBox.Show(Me, text, "AT Keyboard / 8042 State", MessageBoxButtons.OK, MessageBoxIcon.Information)
    End Sub

    Private Sub ShowQbExecForensicTraceInBed()
        Using viewerInBed As New Form()
            viewerInBed.Text = "QB EXEC Forensic Trace"
            viewerInBed.StartPosition = FormStartPosition.CenterParent
            viewerInBed.Width = 1380
            viewerInBed.Height = 820

            Dim traceBoxInBed As New TextBox() With {
                .Multiline = True,
                .ReadOnly = True,
                .ScrollBars = ScrollBars.Both,
                .WordWrap = False,
                .Dock = DockStyle.Fill,
                .Font = New Font(FontFamily.GenericMonospace, 9.0F),
                .Text = ReadMachineInBed(Function() CPU0.GetDiagnosticQbExecTrace())
            }
            viewerInBed.Controls.Add(traceBoxInBed)
            viewerInBed.ShowDialog(Me)
        End Using
    End Sub

    Private Sub ShowImportantIntTrace()
        ShowDumpableDiagnosticInBed(
            "Important INTn Trace",
            ReadMachineInBed(Function() CPU0.GetDiagnosticImportantIntTrace()),
            "important-int-trace.txt",
            1380,
            820)
    End Sub

    Private Sub DumpAllDiagnosticsInBed(statusMenuItemInBed As ToolStripMenuItem)
        Try
            Dim com1CaptureInBed As Byte() = Array.Empty(Of Byte)()
            Dim com2CaptureInBed As Byte() = Array.Empty(Of Byte)()
            Dim com1InputCaptureInBed As Byte() = Array.Empty(Of Byte)()
            Dim com2InputCaptureInBed As Byte() = Array.Empty(Of Byte)()
            Dim lpt1CaptureInBed As Byte() = Array.Empty(Of Byte)()
            Dim lpt2CaptureInBed As Byte() = Array.Empty(Of Byte)()
            Dim diagnosticTextInBed As String =
                ReadMachineInBed(
                    Function() As String
                        Dim reportInBed As New System.Text.StringBuilder()
                        reportInBed.AppendLine("Cromwell Technologies complete machine diagnostic dump")
                        reportInBed.AppendLine(DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss.fff zzz"))
                        reportInBed.AppendLine()
                        reportInBed.AppendLine("===== CPU CORE / PROTECTION =====")
                        reportInBed.AppendLine(CPU0.CoreRefitDiagnosticText())
                        reportInBed.AppendLine(CPU0.HotPathDiagnosticText())
                        reportInBed.AppendLine(CPU0.DiagnosticExecutionHistoryText())
                        reportInBed.AppendLine(CPU0.DiagnosticCpuFaultTraceText())
                        reportInBed.AppendLine(CPU0.DiagnosticProtectionGateText(13))
                        reportInBed.AppendLine(CPU0.DiagnosticSelectorWriteTraceText())
                        reportInBed.AppendLine(CPU0.DiagnosticSelectorWriterHistoryText())
                        reportInBed.AppendLine(CPU0.DiagnosticSecondCliEntryHistoryText())
                        reportInBed.AppendLine(CPU0.DiagnosticGpReturnHistoryText())
                        reportInBed.AppendLine(CPU0.DiagnosticGpHandlerTraceText())
                        reportInBed.AppendLine()
                        reportInBed.AppendLine("===== HOST PERFORMANCE =====")
                        SyncLock _cpuPerfLockInBed
                            Dim targetHzInBed As Long = MachineClock.CpuClockHz
                            Dim ratioInBed As Double = If(targetHzInBed > 0,
                                _cpuPerfEffectiveTStatesPerSecond / CDbl(targetHzInBed), 0.0R)
                            reportInBed.AppendLine("Target CPU clock       : " &
                                (CDbl(targetHzInBed) / 1000000.0R).ToString("0.000") & " MHz")
                            reportInBed.AppendLine("Effective T-state rate : " &
                                (_cpuPerfEffectiveTStatesPerSecond / 1000000.0R).ToString("0.000") & " MT-states/s")
                            reportInBed.AppendLine("Real-time ratio        : " &
                                (ratioInBed * 100.0R).ToString("0.0") & " %")
                            reportInBed.AppendLine("Pending scheduler debt : " &
                                MachineClock.PendingTStates.ToString("N0") & " T-states")
                            reportInBed.AppendLine("RunSlice last/avg/min/max ms: " &
                                _cpuPerfLastSliceMilliseconds.ToString("0.000") & " / " &
                                _cpuPerfAverageSliceMillisecondsInBed.ToString("0.000") & " / " &
                                _cpuPerfMinimumSliceMillisecondsInBed.ToString("0.000") & " / " &
                                _cpuPerfMaximumSliceMillisecondsInBed.ToString("0.000"))
                            reportInBed.AppendLine("Adaptive slice ceiling : " &
                                _machineRuntime.CurrentMaximumTStatesPerSlice.ToString("N0") & " T-states")
                        End SyncLock
                        Dim presentationInBed As DiamondStealthPro928PresentationWorker = _videoPresentation
                        If presentationInBed IsNot Nothing Then reportInBed.AppendLine(presentationInBed.DiagnosticText())
                        reportInBed.AppendLine()
                        reportInBed.AppendLine("===== SOFTWARE INTERRUPTS / DOS FILE SERVICES =====")
                        reportInBed.AppendLine(CPU0.GetDiagnosticImportantIntTrace())
                        reportInBed.AppendLine()
                        reportInBed.AppendLine("===== ATA / ATAPI STORAGE =====")
                        reportInBed.AppendLine(Declares.IdeController.DiagnosticText())
                        reportInBed.AppendLine()
                        reportInBed.AppendLine("===== SERIAL / PARALLEL I/O =====")
                        reportInBed.AppendLine(Com1.DiagnosticText())
                        reportInBed.AppendLine(Com2.DiagnosticText())
                        reportInBed.AppendLine(Lpt1.DiagnosticText())
                        reportInBed.AppendLine(Lpt2.DiagnosticText())
                        reportInBed.AppendLine(Lpt1Printer.DiagnosticText())
                        reportInBed.AppendLine(Lpt2Printer.DiagnosticText())
                        reportInBed.AppendLine()
                        reportInBed.AppendLine("===== MOTHERBOARD AUDIO =====")
                        reportInBed.AppendLine(PcSpeaker.DiagnosticText())
                        reportInBed.AppendLine()
                        reportInBed.AppendLine("===== ISA EXPANSION CARDS =====")
                        reportInBed.AppendLine(SoundBlaster16.DiagnosticText())
                        reportInBed.AppendLine(Ne2000.DiagnosticText())
                        reportInBed.AppendLine(SerialMouse.DiagnosticText())
                        reportInBed.AppendLine(
                            $"Serial mouse host frontend: input=WM_INPUT captured={_serialMouseCapturedInBed} " &
                            $"raw-move-messages={Threading.Interlocked.Read(_serialMouseHostMoveMessagesInBed)} " &
                            $"boundary-transfers={Threading.Interlocked.Read(_serialMouseBoundaryTransfersInBed)} " &
                            $"pending-raw={Threading.Interlocked.Read(_serialMousePendingHostXInBed)}/{Threading.Interlocked.Read(_serialMousePendingHostYInBed)}")
                        reportInBed.AppendLine($"COM1 pins DTR/RTS/OUT1/OUT2/BREAK: {Com1DiagnosticPeripheral.Dtr}/{Com1DiagnosticPeripheral.Rts}/{Com1DiagnosticPeripheral.Out1}/{Com1DiagnosticPeripheral.Out2}/{Com1DiagnosticPeripheral.BreakAsserted}")
                        reportInBed.AppendLine($"COM2 pins DTR/RTS/OUT1/OUT2/BREAK: {Com2DiagnosticPeripheral.Dtr}/{Com2DiagnosticPeripheral.Rts}/{Com2DiagnosticPeripheral.Out1}/{Com2DiagnosticPeripheral.Out2}/{Com2DiagnosticPeripheral.BreakAsserted}")
                        reportInBed.AppendLine($"LPT1 functions SELECTIN/INIT/AUTOFEED: {Lpt1Printer.SelectInAsserted}/{Lpt1Printer.InitializeAsserted}/{Lpt1Printer.AutoFeedAsserted}")
                        reportInBed.AppendLine($"LPT2 functions SELECTIN/INIT/AUTOFEED: {Lpt2Printer.SelectInAsserted}/{Lpt2Printer.InitializeAsserted}/{Lpt2Printer.AutoFeedAsserted}")
                        com1CaptureInBed = Com1DiagnosticPeripheral.ReceivedBytes
                        com2CaptureInBed = Com2DiagnosticPeripheral.ReceivedBytes
                        com1InputCaptureInBed = Com1DiagnosticPeripheral.GuestReceivedBytes
                        com2InputCaptureInBed = Com2DiagnosticPeripheral.GuestReceivedBytes
                        lpt1CaptureInBed = Lpt1Printer.ReceivedBytes
                        lpt2CaptureInBed = Lpt2Printer.ReceivedBytes
                        reportInBed.AppendLine($"Captured bytes COM1/COM2: {com1CaptureInBed.Length}/{com2CaptureInBed.Length} " &
                                               $"(discarded {Com1DiagnosticPeripheral.DroppedBytes}/{Com2DiagnosticPeripheral.DroppedBytes})")
                        reportInBed.AppendLine($"Bytes received by guest COM1/COM2: {com1InputCaptureInBed.Length}/{com2InputCaptureInBed.Length}")
                        reportInBed.AppendLine($"Captured bytes LPT1/LPT2: {lpt1CaptureInBed.Length}/{lpt2CaptureInBed.Length} " &
                                               $"(discarded {Lpt1Printer.DroppedBytes}/{Lpt2Printer.DroppedBytes})")
                        reportInBed.AppendLine()
                        reportInBed.AppendLine("===== 80287 =====")
                        reportInBed.AppendLine(CPU0.NumericCoprocessor.DiagnosticText())
                        reportInBed.AppendLine(CPU0.NumericCoprocessor.DiagnosticFlightRecorderText())
                        reportInBed.AppendLine()
                        reportInBed.AppendLine("===== VGA / S3 86C928 =====")
                        reportInBed.AppendLine("BDA video mode: " & CPU0.ReadByte(&H449UI).ToString("X2") & "h")
                        reportInBed.AppendLine("INT 10h vector: " & CPU0.ReadWord(&H42UI).ToString("X4") & ":" &
                                                CPU0.ReadWord(&H40UI).ToString("X4"))
                        reportInBed.AppendLine(VideoCard.GetDiagnosticVgaStateSnapshot())
                        reportInBed.AppendLine(VideoCard.GetDiagnosticVgaTrace())
                        reportInBed.AppendLine()
                        reportInBed.AppendLine("===== AT KEYBOARD / 8042 / BIOS KEY RING =====")
                        Dim keyboardStatusInBed As Byte = KeyboardController.StatusRegister
                        Dim keyboardLedsInBed As Byte = KeyboardController.KeyboardLedState
                        reportInBed.AppendLine("8042 status 64h          : " & keyboardStatusInBed.ToString("X2") & "h")
                        reportInBed.AppendLine("  OBF / IBF             : " &
                                               If((keyboardStatusInBed And &H1) <> 0, "full", "empty") & " / " &
                                               If((keyboardStatusInBed And &H2) <> 0, "full", "empty"))
                        reportInBed.AppendLine("Interface / serial link : " &
                                               If(KeyboardController.KeyboardInterfaceEnabled, "enabled", "disabled") & " / " &
                                               If(KeyboardController.KeyboardLinkBusy, "busy", "idle"))
                        reportInBed.AppendLine("Scan set / translation  : " &
                                               KeyboardController.KeyboardScanCodeSet.ToString() & " / " &
                                               If(KeyboardController.TranslationEnabled, "enabled", "disabled"))
                        reportInBed.AppendLine("Scanning / BAT           : " &
                                               If(AtKeyboard.ScanningEnabled, "enabled", "disabled") & " / " &
                                               If(AtKeyboard.BasicAssuranceTestActive, "active", "idle"))
                        reportInBed.AppendLine("Pressed / pending bytes  : " &
                                               AtKeyboard.PressedKeyCount.ToString() & " / " &
                                               AtKeyboard.PendingTransmitByteCount.ToString())
                        reportInBed.AppendLine("Typematic byte           : " &
                                               KeyboardController.TypematicByte.ToString("X2") & "h")
                        reportInBed.AppendLine("LEDs Caps/Num/Scroll     : " &
                                               If((keyboardLedsInBed And 4) <> 0, "ON", "off") & " / " &
                                               If((keyboardLedsInBed And 2) <> 0, "ON", "off") & " / " &
                                               If((keyboardLedsInBed And 1) <> 0, "ON", "off"))
                        reportInBed.AppendLine("Frames host/kbd          : " &
                                               KeyboardController.KeyboardFramesTransmitted.ToString() & " / " &
                                               KeyboardController.KeyboardFramesReceived.ToString())
                        reportInBed.AppendLine("Port60 / port64 / IRQ1   : " &
                                               KeyboardController.Port60ReadCount.ToString() & " / " &
                                               KeyboardController.Port64ReadCount.ToString() & " / " &
                                               KeyboardController.Irq1AssertionCount.ToString())
                        reportInBed.AppendLine("Last port 60h value      : " &
                                               KeyboardController.LastPort60Value.ToString("X2") & "h")
                        reportInBed.AppendLine()
                        reportInBed.AppendLine(CPU0.GetDiagnosticBiosKeyboardState())
                        reportInBed.AppendLine(CPU0.GetDiagnosticBiosKeyboardTrace())
                        reportInBed.AppendLine(KeyboardController.GetDiagnosticTrace())
                        reportInBed.AppendLine()
                        reportInBed.AppendLine("===== PIC / PIT / LOCAL BUS =====")
                        reportInBed.AppendLine("MASTER " & MasterPic.DiagnosticText())
                        reportInBed.AppendLine("SLAVE " & SlavePic.DiagnosticText())
                        reportInBed.AppendLine(SystemTimer.DiagnosticText())
                        reportInBed.AppendLine(CpuBus.DiagnosticText())
                        reportInBed.AppendLine(MotherboardBridge.DiagnosticText())
                        Return reportInBed.ToString()
                    End Function)

            Dim outputDirectoryInBed As String = Path.Combine(AppContext.BaseDirectory, "Doutput")
            Directory.CreateDirectory(outputDirectoryInBed)
            Dim outputPathInBed As String = Path.Combine(outputDirectoryInBed, "diagnostic-dump-all.txt")
            File.WriteAllText(outputPathInBed,
                              diagnosticTextInBed,
                              New System.Text.UTF8Encoding(False))
            File.WriteAllBytes(Path.Combine(outputDirectoryInBed, "com1-output.bin"), com1CaptureInBed)
            File.WriteAllBytes(Path.Combine(outputDirectoryInBed, "com2-output.bin"), com2CaptureInBed)
            File.WriteAllBytes(Path.Combine(outputDirectoryInBed, "com1-input.bin"), com1InputCaptureInBed)
            File.WriteAllBytes(Path.Combine(outputDirectoryInBed, "com2-input.bin"), com2InputCaptureInBed)
            File.WriteAllBytes(Path.Combine(outputDirectoryInBed, "lpt1-output.bin"), lpt1CaptureInBed)
            File.WriteAllBytes(Path.Combine(outputDirectoryInBed, "lpt2-output.bin"), lpt2CaptureInBed)
            statusMenuItemInBed.Text = "Dump all diagnostics — written"
            statusMenuItemInBed.ToolTipText = outputPathInBed
        Catch ex As Exception
            statusMenuItemInBed.Text = "Dump all diagnostics — FAILED"
            statusMenuItemInBed.ToolTipText = ex.Message
        End Try
    End Sub

    Private Sub ShowVgaModeTrace()
        Dim traceTextInBed As String =
            ReadMachineInBed(
                Function() As String
                    Return "BDA video mode: " & CPU0.ReadByte(&H449UI).ToString("X2") & "h" & Environment.NewLine &
                           "INT 10h vector: " & CPU0.ReadWord(&H42UI).ToString("X4") & ":" &
                                                CPU0.ReadWord(&H40UI).ToString("X4") & Environment.NewLine &
                           Environment.NewLine &
                           VideoCard.GetDiagnosticVgaTrace()
                End Function)

        ShowDumpableDiagnosticInBed("VGA / S3 Mode Transition Trace",
                                    traceTextInBed,
                                    "vga-mode-trace.txt",
                                    1050,
                                    760)
    End Sub

    Private Sub ShowVgaStateSnapshot()
        Dim stateTextInBed As String =
            ReadMachineInBed(
                Function() As String
                    Return "BDA video mode: " & CPU0.ReadByte(&H449UI).ToString("X2") & "h" & Environment.NewLine &
                           "INT 10h vector: " & CPU0.ReadWord(&H42UI).ToString("X4") & ":" &
                                                CPU0.ReadWord(&H40UI).ToString("X4") & Environment.NewLine &
                           Environment.NewLine &
                           VideoCard.GetDiagnosticVgaStateSnapshot() &
                           Environment.NewLine &
                           CPU0.NumericCoprocessor.DiagnosticText()
                End Function)
        ShowDumpableDiagnosticInBed("VGA / S3 Live State",
                                    stateTextInBed,
                                    "vga-live-state.txt",
                                    900,
                                    650)
    End Sub

    Private Sub ShowNumericCoprocessorFlightRecorderInBed()
        Dim traceTextInBed As String =
            ReadMachineInBed(Function() CPU0.NumericCoprocessor.DiagnosticFlightRecorderText())
        ShowDumpableDiagnosticInBed("80287 Flight Recorder",
                                    traceTextInBed,
                                    "80287-flight-recorder.txt",
                                    950,
                                    700)
    End Sub

    Private Sub ShowCpuInterruptLiveStateInBed()
        Dim stateTextInBed As String =
            ReadMachineInBed(
                Function() As String
                    Return DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss.fff") & Environment.NewLine &
                           CPU0.CoreRefitDiagnosticText() & Environment.NewLine & Environment.NewLine &
                           CPU0.DiagnosticExecutionHistoryText() & Environment.NewLine & Environment.NewLine &
                           CPU0.DiagnosticCpuFaultTraceText() & Environment.NewLine & Environment.NewLine &
                           CPU0.DiagnosticProtectionGateText(13) & Environment.NewLine & Environment.NewLine &
                           CPU0.DiagnosticSelectorWriteTraceText() & Environment.NewLine & Environment.NewLine &
                           CPU0.DiagnosticSelectorWriterHistoryText() & Environment.NewLine & Environment.NewLine &
                           CPU0.DiagnosticSecondCliEntryHistoryText() & Environment.NewLine & Environment.NewLine &
                           CPU0.DiagnosticGpReturnHistoryText() & Environment.NewLine & Environment.NewLine &
                           CPU0.DiagnosticGpHandlerTraceText() & Environment.NewLine & Environment.NewLine &
                           CPU0.GetDiagnosticImportantIntTrace() & Environment.NewLine & Environment.NewLine &
                           "MASTER " & MasterPic.DiagnosticText() & Environment.NewLine & Environment.NewLine &
                           "SLAVE " & SlavePic.DiagnosticText() & Environment.NewLine & Environment.NewLine &
                           SystemTimer.DiagnosticText() & Environment.NewLine & Environment.NewLine &
                           CpuBus.DiagnosticText() & Environment.NewLine &
                           MotherboardBridge.DiagnosticText()
                End Function)
        ShowDumpableDiagnosticInBed("CPU / PIC / PIT Live State",
                                    stateTextInBed,
                                    "cpu-interrupt-live-state.txt",
                                    1000,
                                    780)
    End Sub

    Private Sub ShowDumpableDiagnosticInBed(titleInBed As String,
                                             diagnosticTextInBed As String,
                                             outputFileNameInBed As String,
                                             widthInBed As Integer,
                                             heightInBed As Integer)
        Using viewerInBed As New Form()
            viewerInBed.Text = titleInBed
            viewerInBed.StartPosition = FormStartPosition.CenterParent
            viewerInBed.Width = widthInBed
            viewerInBed.Height = heightInBed

            Dim traceBoxInBed As New TextBox() With {
                .Multiline = True,
                .ReadOnly = True,
                .ScrollBars = ScrollBars.Both,
                .WordWrap = False,
                .Dock = DockStyle.Fill,
                .Font = New Font(FontFamily.GenericMonospace, 9.0F),
                .Text = diagnosticTextInBed
            }

            Dim buttonPanelInBed As New FlowLayoutPanel() With {
                .Dock = DockStyle.Bottom,
                .Height = 42,
                .FlowDirection = FlowDirection.RightToLeft,
                .Padding = New Padding(6)
            }
            Dim closeButtonInBed As New Button() With {.Text = "Close", .AutoSize = True}
            Dim dumpButtonInBed As New Button() With {.Text = "Dump / overwrite", .AutoSize = True}
            Dim statusLabelInBed As New Label() With {
                .AutoSize = True,
                .Padding = New Padding(8, 6, 8, 0)
            }

            AddHandler closeButtonInBed.Click, Sub() viewerInBed.Close()
            AddHandler dumpButtonInBed.Click,
                Sub()
                    Try
                        Dim outputDirectoryInBed As String =
                            Path.Combine(AppContext.BaseDirectory, "Doutput")
                        Directory.CreateDirectory(outputDirectoryInBed)
                        Dim outputPathInBed As String =
                            Path.Combine(outputDirectoryInBed, outputFileNameInBed)
                        File.WriteAllText(outputPathInBed,
                                          traceBoxInBed.Text,
                                          New System.Text.UTF8Encoding(False))
                        statusLabelInBed.Text = "Overwrote " & outputPathInBed
                    Catch ex As Exception
                        statusLabelInBed.Text = "Dump failed: " & ex.Message
                    End Try
                End Sub

            buttonPanelInBed.Controls.Add(closeButtonInBed)
            buttonPanelInBed.Controls.Add(dumpButtonInBed)
            buttonPanelInBed.Controls.Add(statusLabelInBed)
            viewerInBed.Controls.Add(traceBoxInBed)
            viewerInBed.Controls.Add(buttonPanelInBed)
            viewerInBed.AcceptButton = dumpButtonInBed
            viewerInBed.CancelButton = closeButtonInBed
            viewerInBed.ShowDialog(Me)
        End Using
    End Sub

    Private Sub ShowBiosKeyboardRingTrace()
        Using viewerInBed As New Form()
            viewerInBed.Text = "BIOS Keyboard Ring / IVT Forensics"
            viewerInBed.StartPosition = FormStartPosition.CenterParent
            viewerInBed.Width = 1050
            viewerInBed.Height = 760

            Dim traceBoxInBed As New TextBox() With {
                .Multiline = True,
                .ReadOnly = True,
                .ScrollBars = ScrollBars.Both,
                .WordWrap = False,
                .Dock = DockStyle.Fill,
                .Font = New Font(FontFamily.GenericMonospace, 9.0F),
                .Text = ReadMachineInBed(Function() CPU0.GetDiagnosticBiosKeyboardTrace())
            }
            viewerInBed.Controls.Add(traceBoxInBed)
            viewerInBed.ShowDialog(Me)
        End Using
    End Sub

    Private Sub ShowBiosKeyboardRingState()
        MessageBox.Show(Me,
                        ReadMachineInBed(Function() CPU0.GetDiagnosticBiosKeyboardState()),
                        "BIOS Keyboard Ring / IVT Live State",
                        MessageBoxButtons.OK,
                        MessageBoxIcon.Information)
    End Sub

    Private Sub ShowKeyboardWireTrace()
        Dim traceText As String =
            ReadMachineInBed(
                Function() As String
                    Return "Port 60h reads: " & KeyboardController.Port60ReadCount.ToString() & Environment.NewLine &
                           "Port 64h status reads: " & KeyboardController.Port64ReadCount.ToString() & Environment.NewLine &
                           "IRQ1 assertions: " & KeyboardController.Irq1AssertionCount.ToString() & Environment.NewLine &
                           "Last port 60h value: " & KeyboardController.LastPort60Value.ToString("X2") & "h" &
                           Environment.NewLine & Environment.NewLine &
                           KeyboardController.GetDiagnosticTrace()
                End Function)

        Using viewer As New Form()
            viewer.Text = "8042 / IRQ1 Raw Keyboard Trace"
            viewer.StartPosition = FormStartPosition.CenterParent
            viewer.Width = 900
            viewer.Height = 650

            Dim traceBox As New TextBox() With {
                .Multiline = True,
                .ReadOnly = True,
                .ScrollBars = ScrollBars.Both,
                .WordWrap = False,
                .Dock = DockStyle.Fill,
                .Text = traceText
            }
            viewer.Controls.Add(traceBox)
            viewer.ShowDialog(Me)
        End Using
    End Sub
    Private Sub RunKeyboardHardwareSelfTest()
        Dim report As String = KeyboardRealityDiagnostics.RunAll()
        Dim passed As Boolean = report.Contains("RESULT: PASS")
        MessageBox.Show(Me,
                        report,
                        "AT Keyboard Hardware Self-Test",
                        MessageBoxButtons.OK,
                        If(passed, MessageBoxIcon.Information, MessageBoxIcon.Error))
    End Sub
    ' CROMWELL KEYBOARD REALITY BRICK 3 PANEL END

End Class
