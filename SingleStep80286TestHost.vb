Imports System
Imports System.Collections.Generic
Imports System.Diagnostics
Imports System.IO
Imports System.IO.Compression
Imports System.Linq
Imports System.Text
Imports System.Threading
Imports System.Threading.Tasks
Imports System.Windows.Forms

' Host-only runner for the hardware-generated SingleStepTests/80286 corpus.
' The runner owns a completely isolated Processor286 + empty AT fabric; it never
' mutates the running virtual machine. Test files remain external to VMB because
' the upstream corpus is hundreds of megabytes and should be independently
' updateable.
Public NotInheritable Class SingleStep80286TestHostForm
    Inherits Form

    Private Const UpstreamUrlInBed As String = "https://github.com/SingleStepTests/80286.git"
    Private Const UpstreamWebUrlInBed As String = "https://github.com/SingleStepTests/80286"
    Private Const UpstreamLicenseUrlInBed As String = "https://github.com/SingleStepTests/80286/blob/main/LICENSE"
    Private Const AuthorWebUrlInBed As String = "https://github.com/dbalsom"
    Private ReadOnly _pathInBed As New TextBox()
    Private ReadOnly _opcodeInBed As New ComboBox()
    Private ReadOnly _limitInBed As New NumericUpDown()
    Private ReadOnly _runInBed As New Button()
    Private ReadOnly _runSuiteInBed As New Button()
    Private ReadOnly _stopOnFirstFailureInBed As New CheckBox()
    Private ReadOnly _strictSiliconInBed As New CheckBox()
    Private ReadOnly _cancelInBed As New Button()
    Private ReadOnly _exportInBed As New Button()
    Private ReadOnly _cloneInBed As New Button()
    Private ReadOnly _progressInBed As New ProgressBar()
    Private ReadOnly _statusInBed As New Label()
    Private ReadOnly _summaryHeadlineInBed As New Label()
    Private ReadOnly _summaryListInBed As New ListView()
    Private ReadOnly _outputInBed As New TextBox()
    Private ReadOnly _lastReportsInBed As New List(Of SingleStep80286RunReportInBed)()
    Private _lastRunModeInBed As String = String.Empty
    Private _lastRunStartedAtInBed As DateTime
    Private _lastRunEndedAtInBed As DateTime
    Private _lastRunCancelledInBed As Boolean
    Private _lastRunStoppedEarlyInBed As Boolean
    Private _ctsInBed As CancellationTokenSource

    Public Sub New()
        Text = "80286 Hardware Single-Step Tests"
        Width = 1240
        Height = 720
        StartPosition = FormStartPosition.CenterParent
        MinimumSize = New Drawing.Size(980, 560)

        Dim splitInBed As New SplitContainer With {
            .Dock = DockStyle.Fill,
            .Orientation = Orientation.Vertical,
            .FixedPanel = FixedPanel.Panel1
        }

        Dim summaryRootInBed As New TableLayoutPanel With {
            .Dock = DockStyle.Fill,
            .ColumnCount = 1,
            .RowCount = 3,
            .Padding = New Padding(8)
        }
        summaryRootInBed.RowStyles.Add(New RowStyle(SizeType.AutoSize))
        summaryRootInBed.RowStyles.Add(New RowStyle(SizeType.AutoSize))
        summaryRootInBed.RowStyles.Add(New RowStyle(SizeType.Percent, 100.0F))

        Dim summaryTitleInBed As New Label With {
            .Text = "RUNNING TORTURE SUMMARY",
            .AutoSize = True,
            .Font = New Drawing.Font(Drawing.SystemFonts.MessageBoxFont.FontFamily, 10.0F, Drawing.FontStyle.Bold),
            .Margin = New Padding(0, 0, 0, 6)
        }
        summaryRootInBed.Controls.Add(summaryTitleInBed)

        _summaryHeadlineInBed.AutoSize = True
        _summaryHeadlineInBed.MaximumSize = New Drawing.Size(255, 0)
        _summaryHeadlineInBed.Text = "No run yet."
        _summaryHeadlineInBed.Margin = New Padding(0, 0, 0, 8)
        summaryRootInBed.Controls.Add(_summaryHeadlineInBed)

        _summaryListInBed.Dock = DockStyle.Fill
        _summaryListInBed.View = View.Details
        _summaryListInBed.FullRowSelect = True
        _summaryListInBed.GridLines = True
        _summaryListInBed.HideSelection = False
        _summaryListInBed.MultiSelect = False
        _summaryListInBed.Columns.Add("File", 88, HorizontalAlignment.Left)
        _summaryListInBed.Columns.Add("Pass", 52, HorizontalAlignment.Right)
        _summaryListInBed.Columns.Add("Fail", 46, HorizontalAlignment.Right)
        _summaryListInBed.Columns.Add("Skip", 46, HorizontalAlignment.Right)
        _summaryListInBed.Columns.Add("State", 64, HorizontalAlignment.Left)
        summaryRootInBed.Controls.Add(_summaryListInBed)
        splitInBed.Panel1.Controls.Add(summaryRootInBed)

        Dim rootInBed As New TableLayoutPanel With {
            .Dock = DockStyle.Fill,
            .ColumnCount = 1,
            .RowCount = 5,
            .Padding = New Padding(10)
        }
        rootInBed.RowStyles.Add(New RowStyle(SizeType.AutoSize))
        rootInBed.RowStyles.Add(New RowStyle(SizeType.AutoSize))
        rootInBed.RowStyles.Add(New RowStyle(SizeType.AutoSize))
        rootInBed.RowStyles.Add(New RowStyle(SizeType.AutoSize))
        rootInBed.RowStyles.Add(New RowStyle(SizeType.Percent, 100.0F))

        Dim pathRowInBed As New FlowLayoutPanel With {.Dock = DockStyle.Fill, .AutoSize = True, .WrapContents = False}
        pathRowInBed.Controls.Add(New Label With {.Text = "Suite checkout:", .AutoSize = True, .Margin = New Padding(0, 7, 6, 0)})
        _pathInBed.Width = 610
        _pathInBed.Text = DefaultSuitePathInBed()
        pathRowInBed.Controls.Add(_pathInBed)
        Dim browseButtonInBed As New Button With {.Text = "Browse...", .AutoSize = True}
        AddHandler browseButtonInBed.Click, AddressOf BrowseInBed
        pathRowInBed.Controls.Add(browseButtonInBed)
        rootInBed.Controls.Add(pathRowInBed)

        Dim sourceRowInBed As New FlowLayoutPanel With {.Dock = DockStyle.Fill, .AutoSize = True}
        _cloneInBed.Text = "Clone / Update from GitHub"
        _cloneInBed.AutoSize = True
        AddHandler _cloneInBed.Click, AddressOf CloneOrUpdateInBed
        sourceRowInBed.Controls.Add(_cloneInBed)
        Dim upstreamInBed As New Label With {
            .Text = "SingleStepTests/80286 by Daniel Balsom — hardware-generated Harris N80C286 tests; MIT licensed",
            .AutoSize = True,
            .Margin = New Padding(12, 8, 8, 0)
        }
        sourceRowInBed.Controls.Add(upstreamInBed)
        Dim aboutButtonInBed As New Button With {.Text = "About...", .AutoSize = True}
        AddHandler aboutButtonInBed.Click, AddressOf ShowAboutInBed
        sourceRowInBed.Controls.Add(aboutButtonInBed)
        rootInBed.Controls.Add(sourceRowInBed)

        Dim controlsInBed As New FlowLayoutPanel With {.Dock = DockStyle.Fill, .AutoSize = True}
        controlsInBed.Controls.Add(New Label With {.Text = "Opcode file:", .AutoSize = True, .Margin = New Padding(0, 7, 6, 0)})
        _opcodeInBed.DropDownStyle = ComboBoxStyle.DropDownList
        _opcodeInBed.Width = 150
        controlsInBed.Controls.Add(_opcodeInBed)
        Dim refreshInBed As New Button With {.Text = "Refresh", .AutoSize = True}
        AddHandler refreshInBed.Click, Sub() PopulateOpcodeFilesInBed()
        controlsInBed.Controls.Add(refreshInBed)
        controlsInBed.Controls.Add(New Label With {.Text = "Tests:", .AutoSize = True, .Margin = New Padding(12, 7, 6, 0)})
        _limitInBed.Minimum = 0
        _limitInBed.Maximum = 5000
        _limitInBed.Value = 100
        _limitInBed.Width = 72
        controlsInBed.Controls.Add(_limitInBed)
        controlsInBed.Controls.Add(New Label With {.Text = "(0 = entire selected file)", .AutoSize = True, .Margin = New Padding(2, 7, 12, 0)})
        _runInBed.Text = "Run selected"
        _runInBed.AutoSize = True
        AddHandler _runInBed.Click, AddressOf RunSelectedInBed
        controlsInBed.Controls.Add(_runInBed)

        _runSuiteInBed.Text = "TORTURE ME TILL I DIE — ENTIRE SUITE"
        _runSuiteInBed.AutoSize = True
        _runSuiteInBed.Font = New Drawing.Font(Drawing.SystemFonts.MessageBoxFont, Drawing.FontStyle.Bold)
        AddHandler _runSuiteInBed.Click, AddressOf RunEntireSuiteInBed
        controlsInBed.Controls.Add(_runSuiteInBed)

        _stopOnFirstFailureInBed.Text = "Stop on first failure"
        _stopOnFirstFailureInBed.AutoSize = True
        _stopOnFirstFailureInBed.Margin = New Padding(10, 6, 6, 0)
        controlsInBed.Controls.Add(_stopOnFirstFailureInBed)

        _strictSiliconInBed.Text = "Strict Harris undefined bits"
        _strictSiliconInBed.AutoSize = True
        _strictSiliconInBed.Checked = False
        _strictSiliconInBed.Margin = New Padding(10, 6, 6, 0)
        controlsInBed.Controls.Add(_strictSiliconInBed)

        _cancelInBed.Text = "Cancel"
        _cancelInBed.AutoSize = True
        _cancelInBed.Enabled = False
        AddHandler _cancelInBed.Click, Sub() If _ctsInBed IsNot Nothing Then _ctsInBed.Cancel()
        controlsInBed.Controls.Add(_cancelInBed)

        _exportInBed.Text = "Export report..."
        _exportInBed.AutoSize = True
        _exportInBed.Enabled = False
        AddHandler _exportInBed.Click, AddressOf ExportReportInBed
        controlsInBed.Controls.Add(_exportInBed)
        rootInBed.Controls.Add(controlsInBed)

        Dim statusRowInBed As New TableLayoutPanel With {.Dock = DockStyle.Fill, .AutoSize = True, .ColumnCount = 1, .RowCount = 2}
        _progressInBed.Dock = DockStyle.Top
        _statusInBed.AutoSize = True
        _statusInBed.Text = "Select a local checkout of the test repository."
        statusRowInBed.Controls.Add(_progressInBed)
        statusRowInBed.Controls.Add(_statusInBed)
        rootInBed.Controls.Add(statusRowInBed)

        _outputInBed.Dock = DockStyle.Fill
        _outputInBed.Multiline = True
        _outputInBed.ScrollBars = ScrollBars.Both
        _outputInBed.ReadOnly = True
        _outputInBed.Font = New Drawing.Font("Consolas", 9.0F)
        _outputInBed.WordWrap = False
        rootInBed.Controls.Add(_outputInBed)
        splitInBed.Panel2.Controls.Add(rootInBed)
        Controls.Add(splitInBed)

        AddHandler Shown,
            Sub()
                ' SplitContainer validates SplitterDistance against its *current* width.
                ' During construction that width is still the tiny framework default, so
                ' assigning the final 285px split in the object initializer can throw.
                ' Apply it only after docking/layout has given the control its real width.
                Dim availableWidthInBed As Integer = splitInBed.ClientSize.Width - splitInBed.SplitterWidth
                If availableWidthInBed > 1 Then
                    Dim desiredLeftInBed As Integer = Math.Min(285, Math.Max(200, availableWidthInBed - 500))
                    desiredLeftInBed = Math.Min(desiredLeftInBed, availableWidthInBed - 1)
                    splitInBed.SplitterDistance = Math.Max(1, desiredLeftInBed)
                End If
                PopulateOpcodeFilesInBed()
            End Sub
        AddHandler FormClosed, Sub() If _ctsInBed IsNot Nothing Then _ctsInBed.Cancel()
    End Sub

    Private Sub ShowAboutInBed(sender As Object, e As EventArgs)
        Using aboutFormInBed As New Form With {
            .Text = "About 80286 Hardware Single-Step Tests",
            .Width = 650,
            .Height = 440,
            .StartPosition = FormStartPosition.CenterParent,
            .FormBorderStyle = FormBorderStyle.FixedDialog,
            .MaximizeBox = False,
            .MinimizeBox = False,
            .ShowInTaskbar = False
        }
            Dim aboutLayoutInBed As New TableLayoutPanel With {
                .Dock = DockStyle.Fill,
                .ColumnCount = 1,
                .RowCount = 6,
                .Padding = New Padding(16)
            }
            aboutLayoutInBed.RowStyles.Add(New RowStyle(SizeType.AutoSize))
            aboutLayoutInBed.RowStyles.Add(New RowStyle(SizeType.AutoSize))
            aboutLayoutInBed.RowStyles.Add(New RowStyle(SizeType.AutoSize))
            aboutLayoutInBed.RowStyles.Add(New RowStyle(SizeType.AutoSize))
            aboutLayoutInBed.RowStyles.Add(New RowStyle(SizeType.Percent, 100.0F))
            aboutLayoutInBed.RowStyles.Add(New RowStyle(SizeType.AutoSize))

            Dim titleInBed As New Label With {
                .Text = "SingleStepTests/80286",
                .AutoSize = True,
                .Font = New Drawing.Font(Drawing.SystemFonts.MessageBoxFont.FontFamily, 14.0F, Drawing.FontStyle.Bold),
                .Margin = New Padding(0, 0, 0, 10)
            }
            aboutLayoutInBed.Controls.Add(titleInBed)

            Dim creditInBed As New Label With {
                .AutoSize = True,
                .MaximumSize = New Drawing.Size(590, 0),
                .Text = "80286 test corpus, hardware capture methodology, and upstream tooling by Daniel Balsom / SingleStepTests." & Environment.NewLine &
                        "Tests were generated from real Harris N80C286 hardware using ArduinoX86." & Environment.NewLine &
                        "Copyright © 2025 Daniel Balsom. Licensed under the MIT License." & Environment.NewLine & Environment.NewLine &
                        "Virtual Machine Bench host adapter: ChatGPT by OpenAI." & Environment.NewLine &
                        "The adapter provides the VMB UI, MOO reader, isolated test harness, and result comparison. " &
                        "Credit for the tests themselves belongs to Daniel Balsom and the upstream project.",
                .Margin = New Padding(0, 0, 0, 12)
            }
            aboutLayoutInBed.Controls.Add(creditInBed)

            Dim authorLinkInBed As New LinkLabel With {
                .Text = "Daniel Balsom on GitHub — github.com/dbalsom",
                .AutoSize = True,
                .Margin = New Padding(0, 3, 0, 3)
            }
            AddHandler authorLinkInBed.LinkClicked, Sub() OpenExternalUrlInBed(AuthorWebUrlInBed)
            aboutLayoutInBed.Controls.Add(authorLinkInBed)

            Dim repoLinkInBed As New LinkLabel With {
                .Text = "Upstream repository — github.com/SingleStepTests/80286",
                .AutoSize = True,
                .Margin = New Padding(0, 3, 0, 3)
            }
            AddHandler repoLinkInBed.LinkClicked, Sub() OpenExternalUrlInBed(UpstreamWebUrlInBed)
            aboutLayoutInBed.Controls.Add(repoLinkInBed)

            Dim licenseLinkInBed As New LinkLabel With {
                .Text = "MIT License / upstream copyright notice",
                .AutoSize = True,
                .Margin = New Padding(0, 3, 0, 3)
            }
            AddHandler licenseLinkInBed.LinkClicked, Sub() OpenExternalUrlInBed(UpstreamLicenseUrlInBed)
            aboutLayoutInBed.Controls.Add(licenseLinkInBed)

            Dim closeRowInBed As New FlowLayoutPanel With {
                .Dock = DockStyle.Fill,
                .AutoSize = True,
                .FlowDirection = FlowDirection.RightToLeft
            }
            Dim closeButtonInBed As New Button With {.Text = "Close", .AutoSize = True, .DialogResult = DialogResult.OK}
            closeRowInBed.Controls.Add(closeButtonInBed)
            aboutLayoutInBed.Controls.Add(closeRowInBed)

            aboutFormInBed.AcceptButton = closeButtonInBed
            aboutFormInBed.CancelButton = closeButtonInBed
            aboutFormInBed.Controls.Add(aboutLayoutInBed)
            aboutFormInBed.ShowDialog(Me)
        End Using
    End Sub

    Private Shared Sub OpenExternalUrlInBed(urlInBed As String)
        Try
            Process.Start(New ProcessStartInfo(urlInBed) With {.UseShellExecute = True})
        Catch ex As Exception
            MessageBox.Show("Unable to open the web browser." & Environment.NewLine & urlInBed & Environment.NewLine & Environment.NewLine & ex.Message,
                            "80286 Single-Step Tests", MessageBoxButtons.OK, MessageBoxIcon.Information)
        End Try
    End Sub

    Private Shared Function DefaultSuitePathInBed() As String
        Return Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
                            "VirtualMachineBench", "SingleStepTests", "80286")
    End Function

    Private Sub BrowseInBed(sender As Object, e As EventArgs)
        Using pickerInBed As New FolderBrowserDialog With {.Description = "Select the SingleStepTests/80286 repository root"}
            If Directory.Exists(_pathInBed.Text) Then pickerInBed.SelectedPath = _pathInBed.Text
            If pickerInBed.ShowDialog(Me) = DialogResult.OK Then
                _pathInBed.Text = pickerInBed.SelectedPath
                PopulateOpcodeFilesInBed()
            End If
        End Using
    End Sub

    Private Async Sub CloneOrUpdateInBed(sender As Object, e As EventArgs)
        If _ctsInBed IsNot Nothing Then Return
        Dim pathInBed As String = _pathInBed.Text.Trim()
        If pathInBed = String.Empty Then Return
        Try
            SetBusyInBed(True)
            _statusInBed.Text = "Invoking Git for the public test repository..."
            Dim parentInBed As String = Path.GetDirectoryName(pathInBed.TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar))
            If Not Directory.Exists(parentInBed) Then Directory.CreateDirectory(parentInBed)
            Dim argumentsInBed As String
            Dim workingInBed As String
            If Directory.Exists(Path.Combine(pathInBed, ".git")) Then
                argumentsInBed = "-C """ & pathInBed & """ pull --ff-only"
                workingInBed = pathInBed
            Else
                argumentsInBed = "clone --depth 1 " & UpstreamUrlInBed & " """ & pathInBed & """"
                workingInBed = parentInBed
            End If
            Dim resultInBed As String = Await Task.Run(Function() RunProcessCapturedInBed("git", argumentsInBed, workingInBed))
            _outputInBed.Text = resultInBed
            _statusInBed.Text = "Repository ready."
            PopulateOpcodeFilesInBed()
        Catch ex As Exception
            _statusInBed.Text = "Git operation failed."
            _outputInBed.Text = ex.ToString()
        Finally
            SetBusyInBed(False)
        End Try
    End Sub

    Private Shared Function RunProcessCapturedInBed(fileNameInBed As String, argumentsInBed As String, workingInBed As String) As String
        Dim psiInBed As New ProcessStartInfo(fileNameInBed, argumentsInBed) With {
            .WorkingDirectory = workingInBed,
            .UseShellExecute = False,
            .RedirectStandardOutput = True,
            .RedirectStandardError = True,
            .CreateNoWindow = True
        }
        Using processInBed As Process = Process.Start(psiInBed)
            If processInBed Is Nothing Then Throw New InvalidOperationException("Unable to start " & fileNameInBed & ".")
            Dim stdoutInBed As String = processInBed.StandardOutput.ReadToEnd()
            Dim stderrInBed As String = processInBed.StandardError.ReadToEnd()
            processInBed.WaitForExit()
            If processInBed.ExitCode <> 0 Then Throw New InvalidOperationException(stderrInBed & Environment.NewLine & stdoutInBed)
            Return stdoutInBed & If(stderrInBed = String.Empty, String.Empty, Environment.NewLine & stderrInBed)
        End Using
    End Function

    Private Sub PopulateOpcodeFilesInBed()
        Dim previousInBed As String = If(TryCast(_opcodeInBed.SelectedItem, String), String.Empty)
        _opcodeInBed.Items.Clear()
        Dim realModeInBed As String = Path.Combine(_pathInBed.Text.Trim(), "v1_real_mode")
        If Directory.Exists(realModeInBed) Then
            For Each fileInBed As String In Directory.EnumerateFiles(realModeInBed, "*.MOO.gz").OrderBy(Function(pInBed) Path.GetFileName(pInBed), StringComparer.OrdinalIgnoreCase)
                _opcodeInBed.Items.Add(Path.GetFileName(fileInBed))
            Next
        End If
        If _opcodeInBed.Items.Count > 0 Then
            Dim indexInBed As Integer = If(previousInBed = String.Empty, -1, _opcodeInBed.Items.IndexOf(previousInBed))
            _opcodeInBed.SelectedIndex = If(indexInBed >= 0, indexInBed, 0)
            _statusInBed.Text = _opcodeInBed.Items.Count.ToString() & " opcode-form files found."
        Else
            _statusInBed.Text = "No v1_real_mode/*.MOO.gz files found. Clone the repository or browse to an existing checkout."
        End If
    End Sub

    Private Sub ResetSummaryInBed(fileNamesInBed As IEnumerable(Of String))
        _summaryListInBed.BeginUpdate()
        Try
            _summaryListInBed.Items.Clear()
            For Each fileNameInBed As String In fileNamesInBed
                Dim itemInBed As New ListViewItem(fileNameInBed)
                itemInBed.SubItems.Add("0")
                itemInBed.SubItems.Add("0")
                itemInBed.SubItems.Add("0")
                itemInBed.SubItems.Add("WAIT")
                _summaryListInBed.Items.Add(itemInBed)
            Next
        Finally
            _summaryListInBed.EndUpdate()
        End Try
        _summaryHeadlineInBed.Text = "Ready."
    End Sub

    Private Function FindSummaryItemInBed(fileNameInBed As String) As ListViewItem
        For Each itemInBed As ListViewItem In _summaryListInBed.Items
            If String.Equals(itemInBed.Text, fileNameInBed, StringComparison.OrdinalIgnoreCase) Then Return itemInBed
        Next
        Return Nothing
    End Function

    Private Sub UpdateSummaryFileInBed(fileNameInBed As String, passedInBed As Integer, failedInBed As Integer, revokedInBed As Integer, stateInBed As String)
        Dim itemInBed As ListViewItem = FindSummaryItemInBed(fileNameInBed)
        If itemInBed Is Nothing Then
            itemInBed = New ListViewItem(fileNameInBed)
            itemInBed.SubItems.Add("0")
            itemInBed.SubItems.Add("0")
            itemInBed.SubItems.Add("0")
            itemInBed.SubItems.Add(String.Empty)
            _summaryListInBed.Items.Add(itemInBed)
        End If
        itemInBed.SubItems(1).Text = passedInBed.ToString("N0")
        itemInBed.SubItems(2).Text = failedInBed.ToString("N0")
        itemInBed.SubItems(3).Text = revokedInBed.ToString("N0")
        itemInBed.SubItems(4).Text = stateInBed
        If stateInBed = "RUN" Then itemInBed.EnsureVisible()
    End Sub

    Private Sub UpdateSummaryHeadlineInBed(filesCompletedInBed As Integer,
                                           totalFilesInBed As Integer,
                                           consideredInBed As Long,
                                           passedInBed As Long,
                                           failedInBed As Long,
                                           revokedInBed As Long,
                                           currentFileInBed As String)
        Dim sbInBed As New StringBuilder()
        sbInBed.Append("Files ").Append(filesCompletedInBed.ToString("N0")).Append("/").Append(totalFilesInBed.ToString("N0")).AppendLine()
        sbInBed.Append("Tests ").Append(consideredInBed.ToString("N0")).AppendLine()
        sbInBed.Append("PASS ").Append(passedInBed.ToString("N0"))
        sbInBed.Append("   FAIL ").Append(failedInBed.ToString("N0")).AppendLine()
        sbInBed.Append("SKIP ").Append(revokedInBed.ToString("N0"))
        If currentFileInBed <> String.Empty Then sbInBed.AppendLine().Append("Now: ").Append(currentFileInBed)
        _summaryHeadlineInBed.Text = sbInBed.ToString()
    End Sub

    Private Sub BeginRecordedRunInBed(modeInBed As String)
        _lastReportsInBed.Clear()
        _lastRunModeInBed = modeInBed
        _lastRunStartedAtInBed = DateTime.Now
        _lastRunEndedAtInBed = DateTime.MinValue
        _lastRunCancelledInBed = False
        _lastRunStoppedEarlyInBed = False
        _exportInBed.Enabled = False
    End Sub

    Private Sub MarkRunningSummaryStateInBed(newStateInBed As String)
        For Each itemInBed As ListViewItem In _summaryListInBed.Items
            If itemInBed.SubItems.Count >= 5 AndAlso String.Equals(itemInBed.SubItems(4).Text, "RUN", StringComparison.OrdinalIgnoreCase) Then
                itemInBed.SubItems(4).Text = newStateInBed
            End If
        Next
    End Sub

    Private Shared Function ParseSummaryNumberInBed(valueInBed As String) As Long
        Dim parsedInBed As Long
        If Long.TryParse(valueInBed, System.Globalization.NumberStyles.AllowThousands, System.Globalization.CultureInfo.CurrentCulture, parsedInBed) Then Return parsedInBed
        If Long.TryParse(valueInBed.Replace(",", String.Empty), parsedInBed) Then Return parsedInBed
        Return 0
    End Function

    Private Function BuildExportReportInBed() As String
        ' Derive totals from the visible running-summary rows instead of only from
        ' completed RunReport objects. That preserves useful partial counts when a
        ' long torture run is cancelled partway through a file.
        Dim totalPassedInBed As Long = 0
        Dim totalFailedInBed As Long = 0
        Dim totalRevokedInBed As Long = 0
        Dim totalConsideredInBed As Long = 0
        Dim passedFilesInBed As Integer = 0
        Dim failedFilesInBed As Integer = 0
        Dim pendingFilesInBed As Integer = 0
        Dim cancelledFilesInBed As Integer = 0
        Dim errorFilesInBed As Integer = 0

        For Each itemInBed As ListViewItem In _summaryListInBed.Items
            Dim passInBed As Long = If(itemInBed.SubItems.Count > 1, ParseSummaryNumberInBed(itemInBed.SubItems(1).Text), 0)
            Dim failInBed As Long = If(itemInBed.SubItems.Count > 2, ParseSummaryNumberInBed(itemInBed.SubItems(2).Text), 0)
            Dim skipInBed As Long = If(itemInBed.SubItems.Count > 3, ParseSummaryNumberInBed(itemInBed.SubItems(3).Text), 0)
            totalPassedInBed += passInBed
            totalFailedInBed += failInBed
            totalRevokedInBed += skipInBed
            totalConsideredInBed += passInBed + failInBed + skipInBed

            Dim stateInBed As String = If(itemInBed.SubItems.Count >= 5, itemInBed.SubItems(4).Text, String.Empty).ToUpperInvariant()
            Select Case stateInBed
                Case "PASS" : passedFilesInBed += 1
                Case "FAIL" : failedFilesInBed += 1
                Case "WAIT", "PARTIAL" : pendingFilesInBed += 1
                Case "CANCEL", "CANCELLED" : cancelledFilesInBed += 1
                Case "ERROR" : errorFilesInBed += 1
            End Select
        Next

        Dim completionStateInBed As String
        If _lastRunCancelledInBed Then
            completionStateInBed = "CANCELLED"
        ElseIf _lastRunStoppedEarlyInBed Then
            completionStateInBed = "STOPPED_ON_FAILURE"
        ElseIf errorFilesInBed > 0 Then
            completionStateInBed = "COMPLETE_WITH_RUNNER_ERRORS"
        ElseIf failedFilesInBed > 0 Then
            completionStateInBed = "COMPLETE_WITH_FAILURES"
        ElseIf pendingFilesInBed > 0 Then
            completionStateInBed = "PARTIAL"
        Else
            completionStateInBed = "COMPLETE_PASS"
        End If

        Dim strictSiliconInBed As Boolean = _lastReportsInBed.Count > 0 AndAlso _lastReportsInBed(0).StrictSilicon
        Dim sbInBed As New StringBuilder()
        sbInBed.AppendLine("VIRTUAL MACHINE BENCH — 80286 SINGLESTEP TEST REPORT")
        sbInBed.AppendLine("FORMAT_VERSION: 2")
        sbInBed.AppendLine("MASK_MODE: " & If(strictSiliconInBed, "STRICT_HARRIS_RAW_RMSK_IGNORED", "OFFICIAL_MOO_CONFORMANCE_RMSK_HONORED"))
        sbInBed.AppendLine("REPORT_PURPOSE: Human-readable and AI-cognizable regression TODO list")
        sbInBed.AppendLine("GENERATED_LOCAL: " & DateTime.Now.ToString("yyyy-MM-ddTHH:mm:sszzz"))
        sbInBed.AppendLine("RUN_MODE: " & If(_lastRunModeInBed = String.Empty, "unknown", _lastRunModeInBed))
        sbInBed.AppendLine("RUN_STATE: " & completionStateInBed)
        If _lastRunStartedAtInBed <> DateTime.MinValue Then sbInBed.AppendLine("STARTED_LOCAL: " & _lastRunStartedAtInBed.ToString("yyyy-MM-ddTHH:mm:sszzz"))
        If _lastRunEndedAtInBed <> DateTime.MinValue Then sbInBed.AppendLine("ENDED_LOCAL: " & _lastRunEndedAtInBed.ToString("yyyy-MM-ddTHH:mm:sszzz"))
        sbInBed.AppendLine("SUITE_CHECKOUT: " & _pathInBed.Text.Trim())
        sbInBed.AppendLine("UPSTREAM_PROJECT: SingleStepTests/80286")
        sbInBed.AppendLine("UPSTREAM_AUTHOR: Daniel Balsom")
        sbInBed.AppendLine("UPSTREAM_REPOSITORY: " & UpstreamWebUrlInBed)
        sbInBed.AppendLine("UPSTREAM_LICENSE: MIT")
        sbInBed.AppendLine("VMB_HOST_ADAPTER: ChatGPT by OpenAI")
        sbInBed.AppendLine()

        sbInBed.AppendLine("=== TOTALS ===")
        sbInBed.AppendLine("FILES_TOTAL: " & _summaryListInBed.Items.Count.ToString("N0"))
        sbInBed.AppendLine("FILES_PASS: " & passedFilesInBed.ToString("N0"))
        sbInBed.AppendLine("FILES_FAIL: " & failedFilesInBed.ToString("N0"))
        sbInBed.AppendLine("FILES_PENDING: " & pendingFilesInBed.ToString("N0"))
        sbInBed.AppendLine("FILES_CANCELLED: " & cancelledFilesInBed.ToString("N0"))
        sbInBed.AppendLine("FILES_ERROR: " & errorFilesInBed.ToString("N0"))
        sbInBed.AppendLine("TESTS_CONSIDERED: " & totalConsideredInBed.ToString("N0"))
        sbInBed.AppendLine("TESTS_PASS: " & totalPassedInBed.ToString("N0"))
        sbInBed.AppendLine("TESTS_FAIL: " & totalFailedInBed.ToString("N0"))
        sbInBed.AppendLine("TESTS_REVOKED_OR_SKIPPED: " & totalRevokedInBed.ToString("N0"))
        sbInBed.AppendLine()

        sbInBed.AppendLine("=== TODO CHECKLIST ===")
        sbInBed.AppendLine("Legend: [x]=finished/pass, [ ]=action required or not yet run")
        For Each itemInBed As ListViewItem In _summaryListInBed.Items
            Dim fileNameInBed As String = itemInBed.Text
            Dim passInBed As Long = If(itemInBed.SubItems.Count > 1, ParseSummaryNumberInBed(itemInBed.SubItems(1).Text), 0)
            Dim failInBed As Long = If(itemInBed.SubItems.Count > 2, ParseSummaryNumberInBed(itemInBed.SubItems(2).Text), 0)
            Dim skipInBed As Long = If(itemInBed.SubItems.Count > 3, ParseSummaryNumberInBed(itemInBed.SubItems(3).Text), 0)
            Dim stateInBed As String = If(itemInBed.SubItems.Count > 4, itemInBed.SubItems(4).Text, "UNKNOWN").ToUpperInvariant()
            Dim checkInBed As String = If(stateInBed = "PASS", "[x]", "[ ]")
            Dim actionInBed As String
            Select Case stateInBed
                Case "PASS"
                    actionInBed = "none"
                Case "FAIL"
                    actionInBed = "TODO: reproduce and fix architectural mismatch(es), then rerun this file"
                Case "WAIT"
                    actionInBed = "TODO: not yet run"
                Case "PARTIAL"
                    actionInBed = "TODO: only a subset was tested; rerun this file with Tests=0"
                Case "CANCEL", "CANCELLED"
                    actionInBed = "TODO: run was cancelled; rerun this file"
                Case "ERROR"
                    actionInBed = "TODO: runner error; inspect the detail pane/log and rerun this file"
                Case Else
                    actionInBed = "TODO: review state and rerun if needed"
            End Select
            sbInBed.Append(checkInBed).Append(" FILE ").Append(fileNameInBed)
            sbInBed.Append(" | state=").Append(stateInBed)
            sbInBed.Append(" | passed=").Append(passInBed.ToString())
            sbInBed.Append(" | failed=").Append(failInBed.ToString())
            sbInBed.Append(" | skipped=").Append(skipInBed.ToString())
            sbInBed.Append(" | action=").AppendLine(actionInBed)
        Next
        sbInBed.AppendLine()

        sbInBed.AppendLine("=== FAILURE DETAILS / FIX QUEUE ===")
        Dim anyFailuresInBed As Boolean = False
        For Each reportInBed As SingleStep80286RunReportInBed In _lastReportsInBed
            If reportInBed.Failed <= 0 Then Continue For
            anyFailuresInBed = True
            sbInBed.AppendLine("FILE: " & reportInBed.FileName)
            sbInBed.AppendLine("STATE: FAIL")
            sbInBed.AppendLine("FAILED_TEST_COUNT: " & reportInBed.Failed.ToString())
            sbInBed.AppendLine("TODO: Diagnose this opcode/form file; fix emulator behavior; rerun until PASS.")
            sbInBed.AppendLine("DETAILS_RETAINED: " & reportInBed.Failures.Count.ToString() & " (runner retains up to 100 first failures per file)")
            For Each failureInBed As String In reportInBed.Failures
                sbInBed.AppendLine("- [ ] " & failureInBed)
            Next
            sbInBed.AppendLine()
        Next
        If Not anyFailuresInBed Then sbInBed.AppendLine("No recorded architectural failures in the completed portion of this run.")
        sbInBed.AppendLine()

        sbInBed.AppendLine("=== AI HANDOFF INSTRUCTIONS ===")
        sbInBed.AppendLine("1. Treat each unchecked FILE line with state=FAIL according to MASK_MODE above.")
        sbInBed.AppendLine("   In OFFICIAL_MOO_CONFORMANCE mode, RMSK-declared undefined bits are intentionally ignored.")
        sbInBed.AppendLine("   In STRICT_HARRIS_RAW mode, raw captured undefined bits are exploratory silicon-clone evidence, not upstream conformance failures.")
        sbInBed.AppendLine("2. Use FAILURE DETAILS to identify the first reproducible architectural mismatches.")
        sbInBed.AppendLine("3. Do not treat state=WAIT/PARTIAL/CANCELLED as an architectural failure; those files still need full execution.")
        sbInBed.AppendLine("   Treat state=ERROR as a harness/runtime problem that must be resolved before judging the CPU.")
        sbInBed.AppendLine("4. After a fix, rerun the affected file first, then rerun the entire suite for regression coverage.")
        sbInBed.AppendLine("5. This runner validates architectural register/RAM state; upstream bus-cycle traces are separate evidence.")
        Return sbInBed.ToString()
    End Function

    Private Sub ExportReportInBed(sender As Object, e As EventArgs)
        If _summaryListInBed.Items.Count = 0 Then
            MessageBox.Show("There is no test run summary to export yet.", "80286 Single-Step Tests", MessageBoxButtons.OK, MessageBoxIcon.Information)
            Return
        End If

        Using pickerInBed As New SaveFileDialog With {
            .Title = "Export 80286 SingleStepTests report",
            .Filter = "Text report (*.txt)|*.txt|All files (*.*)|*.*",
            .DefaultExt = "txt",
            .AddExtension = True,
            .FileName = "VMB-80286-SingleStepTests-" & DateTime.Now.ToString("yyyyMMdd-HHmmss") & ".txt"
        }
            If pickerInBed.ShowDialog(Me) <> DialogResult.OK Then Return
            Try
                File.WriteAllText(pickerInBed.FileName, BuildExportReportInBed(), New UTF8Encoding(False))
                _statusInBed.Text = "Exported report: " & pickerInBed.FileName
            Catch ex As Exception
                MessageBox.Show("Unable to export the report." & Environment.NewLine & Environment.NewLine & ex.Message,
                                "80286 Single-Step Tests", MessageBoxButtons.OK, MessageBoxIcon.Error)
            End Try
        End Using
    End Sub

    Private Async Sub RunSelectedInBed(sender As Object, e As EventArgs)
        If _ctsInBed IsNot Nothing OrElse _opcodeInBed.SelectedItem Is Nothing Then Return
        Dim rootInBed As String = _pathInBed.Text.Trim()
        Dim selectedNameInBed As String = CStr(_opcodeInBed.SelectedItem)
        Dim testPathInBed As String = Path.Combine(rootInBed, "v1_real_mode", selectedNameInBed)
        If Not File.Exists(testPathInBed) Then Return
        Dim limitInBed As Integer = CInt(_limitInBed.Value)
        Dim strictSiliconInBed As Boolean = _strictSiliconInBed.Checked
        _ctsInBed = New CancellationTokenSource()
        BeginRecordedRunInBed("selected-file")
        SetBusyInBed(True)
        _outputInBed.Clear()
        ResetSummaryInBed(New String() {selectedNameInBed})
        Try
            Dim progressInBed As New Progress(Of SingleStep80286ProgressInBed)(
                Sub(pInBed)
                    _progressInBed.Maximum = Math.Max(1, pInBed.Total)
                    _progressInBed.Value = Math.Min(_progressInBed.Maximum, Math.Max(0, pInBed.Completed))
                    _statusInBed.Text = pInBed.Status
                    UpdateSummaryFileInBed(pInBed.FileName, pInBed.Passed, pInBed.Failed, pInBed.Revoked, "RUN")
                    UpdateSummaryHeadlineInBed(0, 1, pInBed.Completed, pInBed.Passed, pInBed.Failed, pInBed.Revoked, pInBed.FileName)
                End Sub)
            Dim reportInBed As SingleStep80286RunReportInBed =
                Await Task.Run(Function() SingleStep80286RunnerInBed.RunFile(testPathInBed, rootInBed, limitInBed, False, strictSiliconInBed, progressInBed, _ctsInBed.Token))
            _lastReportsInBed.Add(reportInBed)
            _outputInBed.Text = reportInBed.ToText()
            _statusInBed.Text = "Complete: " & reportInBed.Passed.ToString() & " passed, " & reportInBed.Failed.ToString() & " failed, " & reportInBed.Revoked.ToString() & " revoked/skipped."
            Dim selectedStateInBed As String = If(reportInBed.Failed > 0, "FAIL", If(reportInBed.Considered < reportInBed.TotalAvailable, "PARTIAL", "PASS"))
            UpdateSummaryFileInBed(reportInBed.FileName, reportInBed.Passed, reportInBed.Failed, reportInBed.Revoked, selectedStateInBed)
            UpdateSummaryHeadlineInBed(1, 1, reportInBed.Considered, reportInBed.Passed, reportInBed.Failed, reportInBed.Revoked, String.Empty)
        Catch ex As OperationCanceledException
            _lastRunCancelledInBed = True
            MarkRunningSummaryStateInBed("CANCELLED")
            _statusInBed.Text = "Cancelled."
        Catch ex As Exception
            MarkRunningSummaryStateInBed("ERROR")
            _statusInBed.Text = "Test run failed."
            _outputInBed.Text = ex.ToString()
        Finally
            _lastRunEndedAtInBed = DateTime.Now
            _ctsInBed.Dispose()
            _ctsInBed = Nothing
            SetBusyInBed(False)
            _exportInBed.Enabled = _summaryListInBed.Items.Count > 0
        End Try
    End Sub

    Private Async Sub RunEntireSuiteInBed(sender As Object, e As EventArgs)
        If _ctsInBed IsNot Nothing Then Return
        Dim rootInBed As String = _pathInBed.Text.Trim()
        Dim realModeInBed As String = Path.Combine(rootInBed, "v1_real_mode")
        If Not Directory.Exists(realModeInBed) Then
            PopulateOpcodeFilesInBed()
            Return
        End If

        Dim filesInBed As List(Of String) = Directory.EnumerateFiles(realModeInBed, "*.MOO.gz").
            OrderBy(Function(pInBed) Path.GetFileName(pInBed), StringComparer.OrdinalIgnoreCase).
            ToList()
        If filesInBed.Count = 0 Then
            PopulateOpcodeFilesInBed()
            Return
        End If

        Dim stopOnFailureInBed As Boolean = _stopOnFirstFailureInBed.Checked
        Dim strictSiliconInBed As Boolean = _strictSiliconInBed.Checked
        _ctsInBed = New CancellationTokenSource()
        BeginRecordedRunInBed("entire-suite")
        SetBusyInBed(True)
        _outputInBed.Clear()
        ResetSummaryInBed(filesInBed.Select(Function(pInBed) Path.GetFileName(pInBed)))

        Dim totalConsideredInBed As Long = 0
        Dim totalPassedInBed As Long = 0
        Dim totalFailedInBed As Long = 0
        Dim totalRevokedInBed As Long = 0
        Dim filesCompletedInBed As Integer = 0
        Dim failureDetailsInBed As New List(Of String)()
        Dim stoppedEarlyInBed As Boolean = False

        Try
            _progressInBed.Minimum = 0
            _progressInBed.Maximum = Math.Max(1, filesInBed.Count * 1000)
            _progressInBed.Value = 0

            For fileIndexInBed As Integer = 0 To filesInBed.Count - 1
                _ctsInBed.Token.ThrowIfCancellationRequested()
                Dim currentFileIndexInBed As Integer = fileIndexInBed
                Dim testPathInBed As String = filesInBed(currentFileIndexInBed)
                Dim fileNameInBed As String = Path.GetFileName(testPathInBed)
                Dim committedConsideredInBed As Long = totalConsideredInBed
                Dim committedPassedInBed As Long = totalPassedInBed
                Dim committedFailedInBed As Long = totalFailedInBed
                Dim committedRevokedInBed As Long = totalRevokedInBed

                UpdateSummaryFileInBed(fileNameInBed, 0, 0, 0, "RUN")
                _statusInBed.Text = "Torture suite: file " & (currentFileIndexInBed + 1).ToString("N0") & "/" & filesInBed.Count.ToString("N0") & " — " & fileNameInBed

                Dim progressInBed As New Progress(Of SingleStep80286ProgressInBed)(
                    Sub(pInBed)
                        Dim withinFileInBed As Integer = CInt((CLng(pInBed.Completed) * 1000L) \ Math.Max(1, pInBed.Total))
                        Dim overallInBed As Integer = currentFileIndexInBed * 1000 + withinFileInBed
                        _progressInBed.Value = Math.Min(_progressInBed.Maximum, Math.Max(_progressInBed.Minimum, overallInBed))
                        _statusInBed.Text = "Torture suite " & (currentFileIndexInBed + 1).ToString("N0") & "/" & filesInBed.Count.ToString("N0") & " — " & pInBed.Status
                        UpdateSummaryFileInBed(pInBed.FileName, pInBed.Passed, pInBed.Failed, pInBed.Revoked, "RUN")
                        UpdateSummaryHeadlineInBed(currentFileIndexInBed,
                                                   filesInBed.Count,
                                                   committedConsideredInBed + pInBed.Completed,
                                                   committedPassedInBed + pInBed.Passed,
                                                   committedFailedInBed + pInBed.Failed,
                                                   committedRevokedInBed + pInBed.Revoked,
                                                   pInBed.FileName)
                    End Sub)

                Dim reportInBed As SingleStep80286RunReportInBed =
                    Await Task.Run(Function() SingleStep80286RunnerInBed.RunFile(testPathInBed, rootInBed, 0, stopOnFailureInBed, strictSiliconInBed, progressInBed, _ctsInBed.Token))

                _lastReportsInBed.Add(reportInBed)
                totalConsideredInBed += reportInBed.Considered
                totalPassedInBed += reportInBed.Passed
                totalFailedInBed += reportInBed.Failed
                totalRevokedInBed += reportInBed.Revoked
                filesCompletedInBed = currentFileIndexInBed + 1
                UpdateSummaryFileInBed(fileNameInBed, reportInBed.Passed, reportInBed.Failed, reportInBed.Revoked, If(reportInBed.Failed = 0, "PASS", "FAIL"))
                UpdateSummaryHeadlineInBed(filesCompletedInBed, filesInBed.Count, totalConsideredInBed, totalPassedInBed, totalFailedInBed, totalRevokedInBed, String.Empty)

                If reportInBed.Failed > 0 Then
                    failureDetailsInBed.Add(fileNameInBed & " — " & reportInBed.Failed.ToString("N0") & " failure(s)")
                    For Each detailInBed As String In reportInBed.Failures.Take(10)
                        failureDetailsInBed.Add("  " & detailInBed)
                    Next
                    If stopOnFailureInBed Then
                        stoppedEarlyInBed = True
                        _lastRunStoppedEarlyInBed = True
                        Exit For
                    End If
                End If
            Next

            _progressInBed.Value = Math.Min(_progressInBed.Maximum, filesCompletedInBed * 1000)
            Dim finalTextInBed As New StringBuilder()
            finalTextInBed.AppendLine("SingleStepTests/80286 FULL SUITE torture validation")
            finalTextInBed.AppendLine("Mask mode : " & If(strictSiliconInBed, "STRICT HARRIS RAW (RMSK ignored)", "OFFICIAL MOO CONFORMANCE (RMSK honored)"))
            finalTextInBed.AppendLine("Files     : " & filesCompletedInBed.ToString("N0") & "/" & filesInBed.Count.ToString("N0"))
            finalTextInBed.AppendLine("Considered: " & totalConsideredInBed.ToString("N0"))
            finalTextInBed.AppendLine("Passed    : " & totalPassedInBed.ToString("N0"))
            finalTextInBed.AppendLine("Failed    : " & totalFailedInBed.ToString("N0"))
            finalTextInBed.AppendLine("Revoked   : " & totalRevokedInBed.ToString("N0"))
            finalTextInBed.AppendLine()
            If stoppedEarlyInBed Then
                finalTextInBed.AppendLine("STOPPED ON FIRST FAILING FILE by request.")
            ElseIf totalFailedInBed = 0 Then
                finalTextInBed.AppendLine("No architectural register/RAM mismatches across the completed suite.")
            Else
                finalTextInBed.AppendLine("Failure summary:")
            End If
            For Each lineInBed As String In failureDetailsInBed
                finalTextInBed.AppendLine(lineInBed)
            Next
            finalTextInBed.AppendLine()
            finalTextInBed.AppendLine("NOTE: The full-suite button ignores the per-file Tests limit and runs every")
            finalTextInBed.AppendLine("test in every v1_real_mode/*.MOO.gz file unless cancelled or Stop on first failure is enabled.")
            _outputInBed.Text = finalTextInBed.ToString()

            If stoppedEarlyInBed Then
                _statusInBed.Text = "Torture suite stopped on first failing file: " & totalFailedInBed.ToString("N0") & " failure(s)."
            Else
                _statusInBed.Text = "Torture suite complete: " & totalPassedInBed.ToString("N0") & " passed, " & totalFailedInBed.ToString("N0") & " failed, " & totalRevokedInBed.ToString("N0") & " revoked/skipped."
            End If
            UpdateSummaryHeadlineInBed(filesCompletedInBed, filesInBed.Count, totalConsideredInBed, totalPassedInBed, totalFailedInBed, totalRevokedInBed, String.Empty)
        Catch ex As OperationCanceledException
            _lastRunCancelledInBed = True
            MarkRunningSummaryStateInBed("CANCELLED")
            _statusInBed.Text = "Torture suite cancelled."
            UpdateSummaryHeadlineInBed(filesCompletedInBed, filesInBed.Count, totalConsideredInBed, totalPassedInBed, totalFailedInBed, totalRevokedInBed, "CANCELLED")
        Catch ex As Exception
            MarkRunningSummaryStateInBed("ERROR")
            _statusInBed.Text = "Torture suite failed."
            _outputInBed.Text = ex.ToString()
        Finally
            _lastRunEndedAtInBed = DateTime.Now
            _ctsInBed.Dispose()
            _ctsInBed = Nothing
            SetBusyInBed(False)
            _exportInBed.Enabled = _summaryListInBed.Items.Count > 0
        End Try
    End Sub

    Private Sub SetBusyInBed(busyInBed As Boolean)
        _runInBed.Enabled = Not busyInBed
        _runSuiteInBed.Enabled = Not busyInBed
        _stopOnFirstFailureInBed.Enabled = Not busyInBed
        _strictSiliconInBed.Enabled = Not busyInBed
        _cloneInBed.Enabled = Not busyInBed
        _exportInBed.Enabled = (Not busyInBed) AndAlso _summaryListInBed.Items.Count > 0
        _cancelInBed.Enabled = busyInBed AndAlso _ctsInBed IsNot Nothing
    End Sub
End Class

Friend Structure SingleStep80286ProgressInBed
    Public FileName As String
    Public Completed As Integer
    Public Total As Integer
    Public Passed As Integer
    Public Failed As Integer
    Public Revoked As Integer
    Public Status As String
End Structure

Friend NotInheritable Class SingleStep80286RunReportInBed
    Public Property FileName As String
    Public Property CpuName As String
    Public Property TotalAvailable As Integer
    Public Property Considered As Integer
    Public Property Passed As Integer
    Public Property Failed As Integer
    Public Property Revoked As Integer
    Public Property StrictSilicon As Boolean
    Public ReadOnly Failures As New List(Of String)()

    Public Function ToText() As String
        Dim sbInBed As New StringBuilder()
        sbInBed.AppendLine("SingleStepTests/80286 host validation")
        sbInBed.AppendLine("File      : " & FileName)
        sbInBed.AppendLine("CPU corpus: " & CpuName)
        sbInBed.AppendLine("Mask mode : " & If(StrictSilicon, "STRICT HARRIS RAW (RMSK ignored)", "OFFICIAL MOO CONFORMANCE (RMSK honored)"))
        sbInBed.AppendLine("Available : " & TotalAvailable.ToString("N0"))
        sbInBed.AppendLine("Considered: " & Considered.ToString("N0"))
        sbInBed.AppendLine("Passed    : " & Passed.ToString("N0"))
        sbInBed.AppendLine("Failed    : " & Failed.ToString("N0"))
        sbInBed.AppendLine("Revoked   : " & Revoked.ToString("N0"))
        sbInBed.AppendLine()
        If Failures.Count = 0 Then
            sbInBed.AppendLine("No architectural register/RAM mismatches in the selected run.")
        Else
            sbInBed.AppendLine("First failures:")
            For Each lineInBed As String In Failures
                sbInBed.AppendLine(lineInBed)
            Next
        End If
        sbInBed.AppendLine()
        sbInBed.AppendLine("NOTE: This host runner compares architectural register and RAM state. The upstream")
        sbInBed.AppendLine("corpus also contains pin/bus-cycle traces; cycle-exact comparison is intentionally")
        sbInBed.AppendLine("kept separate so a state failure is not hidden by presentation/timing diagnostics.")
        Return sbInBed.ToString()
    End Function
End Class

Friend NotInheritable Class SingleStep80286RunnerInBed
    Private Sub New()
    End Sub

    Public Shared Function RunFile(testPathInBed As String,
                                   repositoryRootInBed As String,
                                   limitInBed As Integer,
                                   stopOnFirstFailureInBed As Boolean,
                                   strictSiliconInBed As Boolean,
                                   progressInBed As IProgress(Of SingleStep80286ProgressInBed),
                                   cancellationTokenInBed As CancellationToken) As SingleStep80286RunReportInBed
        Dim corpusInBed As Moo286CorpusInBed = Moo286ReaderInBed.Read(testPathInBed)
        Dim revokedInBed As HashSet(Of String) = ReadRevocationsInBed(repositoryRootInBed)
        Dim totalInBed As Integer = If(limitInBed <= 0, corpusInBed.Tests.Count, Math.Min(limitInBed, corpusInBed.Tests.Count))
        Dim reportInBed As New SingleStep80286RunReportInBed With {
            .FileName = Path.GetFileName(testPathInBed),
            .CpuName = corpusInBed.CpuName,
            .TotalAvailable = corpusInBed.Tests.Count,
            .StrictSilicon = strictSiliconInBed
        }
        Dim bedInBed As New Isolated286BedInBed(strictSiliconInBed)
        For indexInBed As Integer = 0 To totalInBed - 1
            cancellationTokenInBed.ThrowIfCancellationRequested()
            Dim testInBed As Moo286TestInBed = corpusInBed.Tests(indexInBed)
            Dim stopNowInBed As Boolean = False
            reportInBed.Considered += 1
            If testInBed.Hash <> String.Empty AndAlso revokedInBed.Contains(testInBed.Hash) Then
                reportInBed.Revoked += 1
            Else
                Dim failureInBed As String = bedInBed.RunOne(testInBed)
                If failureInBed = String.Empty Then
                    reportInBed.Passed += 1
                Else
                    reportInBed.Failed += 1
                    If reportInBed.Failures.Count < 100 Then reportInBed.Failures.Add(failureInBed)
                    stopNowInBed = stopOnFirstFailureInBed
                End If
            End If
            If progressInBed IsNot Nothing AndAlso ((indexInBed And 31) = 0 OrElse indexInBed + 1 = totalInBed) Then
                progressInBed.Report(New SingleStep80286ProgressInBed With {
                    .FileName = Path.GetFileName(testPathInBed),
                    .Completed = indexInBed + 1,
                    .Total = totalInBed,
                    .Passed = reportInBed.Passed,
                    .Failed = reportInBed.Failed,
                    .Revoked = reportInBed.Revoked,
                    .Status = Path.GetFileName(testPathInBed) & " — " & (indexInBed + 1).ToString("N0") & "/" & totalInBed.ToString("N0") &
                              "; pass " & reportInBed.Passed.ToString("N0") & ", fail " & reportInBed.Failed.ToString("N0") & ", skip " & reportInBed.Revoked.ToString("N0")
                })
            End If
            If stopNowInBed Then Exit For
        Next
        Return reportInBed
    End Function

    Private Shared Function ReadRevocationsInBed(rootInBed As String) As HashSet(Of String)
        Dim resultInBed As New HashSet(Of String)(StringComparer.OrdinalIgnoreCase)
        Dim pathInBed As String = Path.Combine(rootInBed, "revocation_list.txt")
        If Not File.Exists(pathInBed) Then Return resultInBed
        For Each lineInBed As String In File.ReadLines(pathInBed)
            Dim valueInBed As String = lineInBed.Trim()
            If valueInBed <> String.Empty AndAlso Not valueInBed.StartsWith("#", StringComparison.Ordinal) Then resultInBed.Add(valueInBed)
        Next
        Return resultInBed
    End Function
End Class

Friend NotInheritable Class SingleStepIoBusDeviceInBed
    Implements IPortDevice, IWordPortDevice, IPortDecodeCandidateProvider

    Private ReadOnly _readsInBed As New Queue(Of Moo286IoReadInBed)()

    Public Sub LoadReads(readsInBed As IEnumerable(Of Moo286IoReadInBed))
        _readsInBed.Clear()
        For Each itemInBed As Moo286IoReadInBed In readsInBed
            _readsInBed.Enqueue(itemInBed)
        Next
    End Sub

    Public Function HandlesPort(port As UShort) As Boolean Implements IPortDevice.HandlesPort
        Return True
    End Function

    Public Function PotentiallyHandlesPort(port As UShort) As Boolean Implements IPortDecodeCandidateProvider.PotentiallyHandlesPort
        Return True
    End Function

    Public Function ReadPort(port As UShort) As Byte Implements IPortDevice.ReadPort
        Dim itemInBed As Moo286IoReadInBed = NextReadInBed(port)
        If (port And 1US) <> 0US Then Return CByte((itemInBed.DataBus >> 8) And &HFFUS)
        Return CByte(itemInBed.DataBus And &HFFUS)
    End Function

    Public Function ReadPortWord(port As UShort) As UShort Implements IWordPortDevice.ReadPortWord
        Return NextReadInBed(port).DataBus
    End Function

    Public Sub WritePort(port As UShort, value As Byte) Implements IPortDevice.WritePort
        ' Output cycles are architecturally consumed by the CPU but the hardware
        ' corpus does not require a peripheral side effect for final CPU state.
    End Sub

    Public Sub WritePortWord(port As UShort, value As UShort) Implements IWordPortDevice.WritePortWord
    End Sub

    Private Function NextReadInBed(portInBed As UInt16) As Moo286IoReadInBed
        While _readsInBed.Count > 0
            Dim itemInBed As Moo286IoReadInBed = _readsInBed.Dequeue()
            If itemInBed.Port = portInBed Then Return itemInBed
        End While
        Return New Moo286IoReadInBed With {.Port = portInBed, .DataBus = &HFFFFUS, .Bhe = 1}
    End Function
End Class

Friend NotInheritable Class Isolated286BedInBed
    Private ReadOnly _fabricInBed As New HardwareBus()
    Private ReadOnly _memoryInBed As New NeatMemoryController286()
    Private ReadOnly _bridgeInBed As NeatMotherboardBridge286
    Private ReadOnly _localBusInBed As CpuLocalBus286
    Private ReadOnly _cpuInBed As Processor286
    Private ReadOnly _ioInBed As New SingleStepIoBusDeviceInBed()
    Private ReadOnly _touchedInBed As New HashSet(Of UInteger)()
    Private ReadOnly _strictSiliconInBed As Boolean
    Private _trackExecutionWritesInBed As Boolean

    Public Sub New(strictSiliconInBed As Boolean)
        _strictSiliconInBed = strictSiliconInBed
        _fabricInBed.Register(_ioInBed)
        _bridgeInBed = New NeatMotherboardBridge286(_fabricInBed, _memoryInBed)
        _localBusInBed = New CpuLocalBus286(_bridgeInBed)
        _cpuInBed = New Processor286 With {.PortBus = _localBusInBed, .MemoryController = _memoryInBed}
        _memoryInBed.ConfigureInstalledMemoryMegabytes(16, clearRam:=True)
        _memoryInBed.A20Enabled = True
        _cpuInBed.A20Enabled = True
        _memoryInBed.SingleStepWriteObserverInBed = AddressOf ObserveMemoryWriteInBed
        VerifyFlatMemoryBedInBed()
    End Sub

    Private Sub ObserveMemoryWriteInBed(addressInBed As UInteger)
        If _trackExecutionWritesInBed Then _touchedInBed.Add(addressInBed And &HFFFFFFUI)
    End Sub

    Private Sub VerifyFlatMemoryBedInBed()
        If _memoryInBed.InstalledMemoryBytes <> &H1000000UI Then
            Throw New InvalidOperationException("80286 SingleStep bed requires exactly 16 MiB of physical RAM.")
        End If
        If Not _memoryInBed.A20Enabled OrElse Not _cpuInBed.A20Enabled Then
            Throw New InvalidOperationException("80286 SingleStep bed requires A20 enabled.")
        End If

        ' The upstream real-mode corpus requires RAM to remain writable through
        ' the conventional VGA/option-ROM/system-ROM apertures.  The isolated
        ' bed intentionally loads no ROM or MMIO devices, but probe the critical
        ' regions so a future motherboard change fails loudly instead of turning
        ' the CPU dashboard red.
        Dim probesInBed As UInteger() = {&HA0000UI, &HC0000UI, &HF0000UI, &H100000UI, &HFFFFFFUI}
        For Each addressInBed As UInteger In probesInBed
            Dim originalInBed As Byte = _cpuInBed.ReadByte(addressInBed)
            _cpuInBed.WriteByte(addressInBed, &H5A)
            If _cpuInBed.ReadByte(addressInBed) <> &H5A Then
                Throw New InvalidOperationException("80286 SingleStep bed is not flat writable RAM at " & addressInBed.ToString("X6") & "h.")
            End If
            _cpuInBed.WriteByte(addressInBed, &HA5)
            If _cpuInBed.ReadByte(addressInBed) <> &HA5 Then
                Throw New InvalidOperationException("80286 SingleStep bed is not flat writable RAM at " & addressInBed.ToString("X6") & "h.")
            End If
            _cpuInBed.WriteByte(addressInBed, originalInBed)
        Next
    End Sub

    Public Function RunOne(testInBed As Moo286TestInBed) As String
        ' Erase only bytes dirtied by the previous case; the corpus explicitly lists
        ' every RAM location whose initial value matters, avoiding a 16 MiB clear for
        ' each of ~1.5 million tests.
        For Each addressInBed As UInteger In _touchedInBed
            _cpuInBed.WriteByte(addressInBed, 0)
        Next
        _touchedInBed.Clear()

        _cpuInBed.Reset()
        _memoryInBed.A20Enabled = True
        _cpuInBed.A20Enabled = True
        _ioInBed.LoadReads(testInBed.IoReads)
        ApplyRegistersInBed(testInBed.Initial.Regs)
        For Each cellInBed As Moo286RamCellInBed In testInBed.Initial.Ram
            _cpuInBed.WriteByte(cellInBed.Address, cellInBed.Value)
            _touchedInBed.Add(cellInBed.Address)
        Next

        Dim startCsInBed As UInt16 = _cpuInBed.CS
        Dim startIpInBed As UInt16 = _cpuInBed.IP
        Dim stepsInBed As Integer = 0
        _trackExecutionWritesInBed = True
        Try
            Do
                ' Processor286.RunCycle() is explicitly an instruction-count API, not
                ' a T-state runner.  One call therefore retires one core instruction
                ' (or one REP iteration in this core's restart model).
                _cpuInBed.RunCycle(1)
                stepsInBed += 1
                If _cpuInBed.Halted Then Exit Do
                ' REP string operations in this core rewind to their instruction start
                ' between iterations. Continue until the instruction finally leaves its
                ' starting CS:IP. Ordinary and flow-control instructions leave in one step.
                If _cpuInBed.CS <> startCsInBed OrElse _cpuInBed.IP <> startIpInBed Then Exit Do
                If stepsInBed >= 65536 Then
                    Return FailurePrefixInBed(testInBed) & "instruction did not leave its starting CS:IP within 65536 core steps"
                End If
            Loop
        Finally
            _trackExecutionWritesInBed = False
        End Try

        ' Upstream records state after a terminating HLT. For a normal instruction the
        ' hardware harness injects/executes that HLT after the instruction. HLT itself
        ' has no architectural side effect other than advancing IP and entering HALT,
        ' so reproduce the recorded post-HLT IP without making the isolated core fetch
        ' random bytes from a branch target. If the opcode under test already halted,
        ' its own fetch has already advanced IP and no synthetic terminator is needed.
        If Not _cpuInBed.Halted Then _cpuInBed.IP = CUShort((CInt(_cpuInBed.IP) + 1) And &HFFFF)

        Dim mismatchInBed As String = CompareRegistersInBed(testInBed)
        If mismatchInBed <> String.Empty Then Return FailurePrefixInBed(testInBed) & mismatchInBed

        ' MOO final state is a delta: unchanged RAM cells are omitted.  Overlay the
        ' final delta onto the initial RAM state so an instruction which corrupts a
        ' supposedly unchanged operand/code byte is caught rather than silently
        ' passing.  Final-only cells (for example stack writes from an exception)
        ' are naturally added by the overlay.
        Dim expectedRamInBed As New Dictionary(Of UInteger, Byte)()
        For Each cellInBed As Moo286RamCellInBed In testInBed.Initial.Ram
            expectedRamInBed(cellInBed.Address) = cellInBed.Value
        Next
        For Each cellInBed As Moo286RamCellInBed In testInBed.Final.Ram
            expectedRamInBed(cellInBed.Address) = cellInBed.Value
        Next
        For Each expectedCellInBed As KeyValuePair(Of UInteger, Byte) In expectedRamInBed
            Dim actualInBed As Byte = _cpuInBed.ReadByte(expectedCellInBed.Key)
            _touchedInBed.Add(expectedCellInBed.Key)

            Dim byteMaskInBed As Byte = &HFF
            If testInBed.HasException Then
                Dim flagsMaskInBed As UInt16 = EffectiveRegisterMaskInBed(testInBed, "flags")
                Dim flagAddressInBed As UInteger = testInBed.ExceptionFlagAddress And &HFFFFFFUI
                If expectedCellInBed.Key = flagAddressInBed Then
                    byteMaskInBed = CByte(flagsMaskInBed And &HFFUS)
                ElseIf expectedCellInBed.Key = ((flagAddressInBed + 1UI) And &HFFFFFFUI) Then
                    byteMaskInBed = CByte((flagsMaskInBed >> 8) And &HFFUS)
                End If
            End If

            If (actualInBed And byteMaskInBed) <> (expectedCellInBed.Value And byteMaskInBed) Then
                Dim maskSuffixInBed As String = If(byteMaskInBed = &HFF, String.Empty, " mask=" & byteMaskInBed.ToString("X2"))
                Return FailurePrefixInBed(testInBed) & "RAM[" & expectedCellInBed.Key.ToString("X6") & "] expected " & expectedCellInBed.Value.ToString("X2") & " got " & actualInBed.ToString("X2") & maskSuffixInBed
            End If
        Next
        Return String.Empty
    End Function

    Private Shared Function FailurePrefixInBed(testInBed As Moo286TestInBed) As String
        Return "#" & testInBed.Index.ToString() & " " & testInBed.Name & ": "
    End Function

    Private Sub ApplyRegistersInBed(regsInBed As Dictionary(Of String, UInt16))
        For Each itemInBed As KeyValuePair(Of String, UInt16) In regsInBed
            Select Case itemInBed.Key
                Case "ax" : _cpuInBed.AX = itemInBed.Value
                Case "bx" : _cpuInBed.BX = itemInBed.Value
                Case "cx" : _cpuInBed.CX = itemInBed.Value
                Case "dx" : _cpuInBed.DX = itemInBed.Value
                Case "sp" : _cpuInBed.SP = itemInBed.Value
                Case "bp" : _cpuInBed.BP = itemInBed.Value
                Case "si" : _cpuInBed.SI = itemInBed.Value
                Case "di" : _cpuInBed.DI = itemInBed.Value
                Case "es" : _cpuInBed.ES = itemInBed.Value
                Case "cs" : _cpuInBed.CS = itemInBed.Value
                Case "ss" : _cpuInBed.SS = itemInBed.Value
                Case "ds" : _cpuInBed.DS = itemInBed.Value
                Case "ip" : _cpuInBed.IP = itemInBed.Value
                Case "flags" : _cpuInBed.Flags = itemInBed.Value
            End Select
        Next
    End Sub

    Private Function CompareRegistersInBed(testInBed As Moo286TestInBed) As String
        ' MOO final registers are also a delta.  Start from the complete initial
        ' register file and overlay the final changes so accidental modification of
        ' a register omitted from FINA is still reported.
        Dim expectedInBed As New Dictionary(Of String, UInt16)(testInBed.Initial.Regs, StringComparer.OrdinalIgnoreCase)
        For Each changedInBed As KeyValuePair(Of String, UInt16) In testInBed.Final.Regs
            expectedInBed(changedInBed.Key) = changedInBed.Value
        Next

        For Each itemInBed As KeyValuePair(Of String, UInt16) In expectedInBed
            Dim actualInBed As UInt16
            Select Case itemInBed.Key
                Case "ax" : actualInBed = _cpuInBed.AX
                Case "bx" : actualInBed = _cpuInBed.BX
                Case "cx" : actualInBed = _cpuInBed.CX
                Case "dx" : actualInBed = _cpuInBed.DX
                Case "sp" : actualInBed = _cpuInBed.SP
                Case "bp" : actualInBed = _cpuInBed.BP
                Case "si" : actualInBed = _cpuInBed.SI
                Case "di" : actualInBed = _cpuInBed.DI
                Case "es" : actualInBed = _cpuInBed.ES
                Case "cs" : actualInBed = _cpuInBed.CS
                Case "ss" : actualInBed = _cpuInBed.SS
                Case "ds" : actualInBed = _cpuInBed.DS
                Case "ip" : actualInBed = _cpuInBed.IP
                Case "flags" : actualInBed = _cpuInBed.Flags
                Case Else : Continue For
            End Select
            Dim maskInBed As UInt16 = EffectiveRegisterMaskInBed(testInBed, itemInBed.Key)
            If (actualInBed And maskInBed) <> (itemInBed.Value And maskInBed) Then
                Dim maskSuffixInBed As String = If(maskInBed = &HFFFFUS, String.Empty, " mask=" & maskInBed.ToString("X4"))
                Return itemInBed.Key.ToUpperInvariant() & " expected " & itemInBed.Value.ToString("X4") & " got " & actualInBed.ToString("X4") & maskSuffixInBed
            End If
        Next
        Return String.Empty
    End Function

    Private Function EffectiveRegisterMaskInBed(testInBed As Moo286TestInBed, registerNameInBed As String) As UInt16
        ' Default conformance mode obeys the MOO file's declared undefined-bit
        ' masks.  Strict mode is deliberately separate for Eric's Harris-silicon
        ' archaeology and compares the raw captured values instead.
        If _strictSiliconInBed Then Return &HFFFFUS
        Dim maskInBed As UInt16 = &HFFFFUS
        Dim fileMaskInBed As UInt16
        If testInBed.GlobalRegisterMasks.TryGetValue(registerNameInBed, fileMaskInBed) Then
            maskInBed = CUShort(maskInBed And fileMaskInBed)
        End If
        Dim testMaskInBed As UInt16
        If testInBed.Final.Masks.TryGetValue(registerNameInBed, testMaskInBed) Then
            maskInBed = CUShort(maskInBed And testMaskInBed)
        End If
        Return maskInBed
    End Function
End Class

Friend NotInheritable Class Moo286CorpusInBed
    Public Property CpuName As String
    Public ReadOnly Tests As New List(Of Moo286TestInBed)()
    Public ReadOnly RegisterMasks As New Dictionary(Of String, UInt16)(StringComparer.OrdinalIgnoreCase)
End Class

Friend NotInheritable Class Moo286TestInBed
    Public Property Index As Integer
    Public Property Name As String = String.Empty
    Public Property Hash As String = String.Empty
    Public Property Initial As New Moo286StateInBed()
    Public Property Final As New Moo286StateInBed()
    Public ReadOnly Bytes As New List(Of Byte)()
    Public ReadOnly IoReads As New List(Of Moo286IoReadInBed)()
    Public ReadOnly GlobalRegisterMasks As New Dictionary(Of String, UInt16)(StringComparer.OrdinalIgnoreCase)
    Public Property HasException As Boolean
    Public Property ExceptionNumber As Byte
    Public Property ExceptionFlagAddress As UInteger
End Class

Friend NotInheritable Class Moo286StateInBed
    Public ReadOnly Regs As New Dictionary(Of String, UInt16)(StringComparer.OrdinalIgnoreCase)
    Public ReadOnly Masks As New Dictionary(Of String, UInt16)(StringComparer.OrdinalIgnoreCase)
    Public ReadOnly Ram As New List(Of Moo286RamCellInBed)()
End Class

Friend Structure Moo286RamCellInBed
    Public Address As UInteger
    Public Value As Byte
End Structure

Friend Structure Moo286IoReadInBed
    Public Port As UInt16
    Public DataBus As UInt16
    Public Bhe As Byte
End Structure

Friend NotInheritable Class Moo286ReaderInBed
    Private Shared ReadOnly RegisterOrderInBed As String() = {"ax", "bx", "cx", "dx", "cs", "ss", "ds", "es", "sp", "bp", "si", "di", "ip", "flags"}

    Private Sub New()
    End Sub

    Public Shared Function Read(pathInBed As String) As Moo286CorpusInBed
        Dim dataInBed As Byte()
        Using fileInBed As FileStream = File.OpenRead(pathInBed)
            If pathInBed.EndsWith(".gz", StringComparison.OrdinalIgnoreCase) Then
                Using gzipInBed As New GZipStream(fileInBed, CompressionMode.Decompress)
                    Using bufferInBed As New MemoryStream()
                        gzipInBed.CopyTo(bufferInBed)
                        dataInBed = bufferInBed.ToArray()
                    End Using
                End Using
            Else
                Using bufferInBed As New MemoryStream()
                    fileInBed.CopyTo(bufferInBed)
                    dataInBed = bufferInBed.ToArray()
                End Using
            End If
        End Using
        Using streamInBed As New MemoryStream(dataInBed, writable:=False)
            Using readerInBed As New BinaryReader(streamInBed, Encoding.UTF8, leaveOpen:=False)
                If Encoding.ASCII.GetString(readerInBed.ReadBytes(4)) <> "MOO " Then Throw New InvalidDataException("Not a MOO file: " & pathInBed)
                Dim headerLengthInBed As UInteger = readerInBed.ReadUInt32()
                Dim headerInBed As Byte() = readerInBed.ReadBytes(CInt(headerLengthInBed))
                If headerInBed.Length < 12 Then Throw New InvalidDataException("MOO header is truncated.")
                Dim corpusInBed As New Moo286CorpusInBed With {.CpuName = Encoding.ASCII.GetString(headerInBed, 8, 4).TrimEnd(ChrW(0), " "c)}
                While streamInBed.Position + 8 <= streamInBed.Length
                    Dim tagInBed As String = Encoding.ASCII.GetString(readerInBed.ReadBytes(4))
                    Dim lengthInBed As UInteger = readerInBed.ReadUInt32()
                    Dim endInBed As Long = streamInBed.Position + CLng(lengthInBed)
                    If endInBed > streamInBed.Length Then Throw New InvalidDataException("MOO chunk exceeds file length.")
                    Select Case tagInBed
                        Case "TEST"
                            corpusInBed.Tests.Add(ReadTestInBed(readerInBed, endInBed))
                        Case "RMSK"
                            ReadRegisterMasksInBed(readerInBed, endInBed, corpusInBed.RegisterMasks)
                    End Select
                    streamInBed.Position = endInBed
                End While

                If corpusInBed.RegisterMasks.Count > 0 Then
                    For Each testInBed As Moo286TestInBed In corpusInBed.Tests
                        For Each maskInBed As KeyValuePair(Of String, UInt16) In corpusInBed.RegisterMasks
                            testInBed.GlobalRegisterMasks(maskInBed.Key) = maskInBed.Value
                        Next
                    Next
                End If
                Return corpusInBed
            End Using
        End Using
    End Function

    Private Shared Function ReadTestInBed(readerInBed As BinaryReader, endInBed As Long) As Moo286TestInBed
        Dim testInBed As New Moo286TestInBed With {.Index = CInt(readerInBed.ReadUInt32())}
        While readerInBed.BaseStream.Position + 8 <= endInBed
            Dim tagInBed As String = Encoding.ASCII.GetString(readerInBed.ReadBytes(4))
            Dim lengthInBed As UInteger = readerInBed.ReadUInt32()
            Dim subEndInBed As Long = readerInBed.BaseStream.Position + CLng(lengthInBed)
            Select Case tagInBed
                Case "NAME"
                    Dim countInBed As UInteger = readerInBed.ReadUInt32()
                    testInBed.Name = Encoding.UTF8.GetString(readerInBed.ReadBytes(CInt(countInBed)))
                Case "BYTS"
                    Dim countInBed As UInteger = readerInBed.ReadUInt32()
                    testInBed.Bytes.Clear()
                    testInBed.Bytes.AddRange(readerInBed.ReadBytes(CInt(countInBed)))
                Case "INIT"
                    testInBed.Initial = ReadStateInBed(readerInBed, subEndInBed)
                Case "FINA"
                    testInBed.Final = ReadStateInBed(readerInBed, subEndInBed)
                Case "CYCL"
                    ReadCyclesInBed(readerInBed, subEndInBed, testInBed)
                Case "EXCP"
                    If lengthInBed >= 5UI Then
                        testInBed.ExceptionNumber = readerInBed.ReadByte()
                        testInBed.ExceptionFlagAddress = readerInBed.ReadUInt32()
                        testInBed.HasException = True
                    End If
                Case "HASH"
                    testInBed.Hash = Convert.ToHexString(readerInBed.ReadBytes(CInt(lengthInBed))).ToLowerInvariant()
            End Select
            readerInBed.BaseStream.Position = subEndInBed
        End While
        Return testInBed
    End Function

    Private Shared Sub ReadCyclesInBed(readerInBed As BinaryReader, endInBed As Long, testInBed As Moo286TestInBed)
        If readerInBed.BaseStream.Position + 4 > endInBed Then Return
        Dim countInBed As UInteger = readerInBed.ReadUInt32()
        Dim latchedIoReadPortInBed As UInt16
        Dim latchedIoReadBheInBed As Byte
        Dim haveLatchedIoReadInBed As Boolean

        For indexInBed As UInteger = 0UI To If(countInBed = 0UI, 0UI, countInBed - 1UI)
            If countInBed = 0UI Then Exit For
            If readerInBed.BaseStream.Position + 15 > endInBed Then Exit For
            Dim pinInBed As Byte = readerInBed.ReadByte()
            Dim addressInBed As UInteger = readerInBed.ReadUInt32()
            Dim segmentInBed As Byte = readerInBed.ReadByte()
            Dim memFlagsInBed As Byte = readerInBed.ReadByte()
            Dim ioFlagsInBed As Byte = readerInBed.ReadByte()
            Dim bheInBed As Byte = readerInBed.ReadByte()
            Dim dataBusInBed As UInt16 = readerInBed.ReadUInt16()
            Dim busStatusInBed As Byte = readerInBed.ReadByte()
            Dim tStateInBed As Byte = readerInBed.ReadByte()
            Dim queueOpInBed As Byte = readerInBed.ReadByte()
            Dim queueReadInBed As Byte = readerInBed.ReadByte()

            ' On the 286 the address bus is pipelined: at Tc it may already contain
            ' the next transaction.  Latch the port at the IOR Ts bus-status cycle,
            ' then take the data bus from the following Tc while the i82288 I/O-read
            ' signal is actually asserted.  This avoids replaying floating Ts data.
            If (busStatusInBed And &HF) = 9 AndAlso (tStateInBed And 7) = 1 Then
                latchedIoReadPortInBed = CUShort(addressInBed And &HFFFFUI)
                latchedIoReadBheInBed = bheInBed
                haveLatchedIoReadInBed = True
            ElseIf haveLatchedIoReadInBed AndAlso (tStateInBed And 7) = 2 AndAlso (ioFlagsInBed And 4) <> 0 Then
                testInBed.IoReads.Add(New Moo286IoReadInBed With {
                    .Port = latchedIoReadPortInBed,
                    .DataBus = dataBusInBed,
                    .Bhe = latchedIoReadBheInBed
                })
                haveLatchedIoReadInBed = False
            End If
        Next
    End Sub

    Private Shared Sub ReadRegisterMasksInBed(readerInBed As BinaryReader,
                                                 endInBed As Long,
                                                 destinationInBed As Dictionary(Of String, UInt16))
        If readerInBed.BaseStream.Position + 2 > endInBed Then Return
        Dim presentInBed As UInt16 = readerInBed.ReadUInt16()
        For indexInBed As Integer = 0 To RegisterOrderInBed.Length - 1
            If (CUInt(presentInBed) And CUInt(1 << indexInBed)) <> 0UI Then
                If readerInBed.BaseStream.Position + 2 > endInBed Then
                    Throw New InvalidDataException("RMSK register mask chunk is truncated.")
                End If
                destinationInBed(RegisterOrderInBed(indexInBed)) = readerInBed.ReadUInt16()
            End If
        Next
    End Sub

    Private Shared Function ReadStateInBed(readerInBed As BinaryReader, endInBed As Long) As Moo286StateInBed
        Dim stateInBed As New Moo286StateInBed()
        While readerInBed.BaseStream.Position + 8 <= endInBed
            Dim tagInBed As String = Encoding.ASCII.GetString(readerInBed.ReadBytes(4))
            Dim lengthInBed As UInteger = readerInBed.ReadUInt32()
            Dim subEndInBed As Long = readerInBed.BaseStream.Position + CLng(lengthInBed)
            Select Case tagInBed
                Case "REGS"
                    Dim maskInBed As UInt16 = readerInBed.ReadUInt16()
                    For indexInBed As Integer = 0 To RegisterOrderInBed.Length - 1
                        If (CUInt(maskInBed) And CUInt(1 << indexInBed)) <> 0UI Then stateInBed.Regs(RegisterOrderInBed(indexInBed)) = readerInBed.ReadUInt16()
                    Next
                Case "RMSK"
                    ReadRegisterMasksInBed(readerInBed, subEndInBed, stateInBed.Masks)
                Case "RAM "
                    Dim countInBed As UInteger = readerInBed.ReadUInt32()
                    If countInBed > 0UI Then
                        For indexInBed As UInteger = 0UI To countInBed - 1UI
                            stateInBed.Ram.Add(New Moo286RamCellInBed With {.Address = readerInBed.ReadUInt32(), .Value = readerInBed.ReadByte()})
                        Next
                    End If
            End Select
            readerInBed.BaseStream.Position = subEndInBed
        End While
        Return stateInBed
    End Function
End Class
