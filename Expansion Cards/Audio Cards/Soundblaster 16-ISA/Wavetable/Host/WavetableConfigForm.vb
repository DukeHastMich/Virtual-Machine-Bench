Imports System
Imports System.Collections.Generic
Imports System.Diagnostics
Imports System.Drawing
Imports System.IO
Imports System.Windows.Forms

' Reusable host-side wavetable librarian/configurator.  Every wavetable-capable
' sound card uses the shared project-root Banks repository.  No Sound Blaster
' object is referenced here, so future cards can use the same dialog unchanged.
Friend NotInheritable Class WavetableConfigForm
    Inherits Form

    Private ReadOnly _cardDisplayNameInBed As String
    Private ReadOnly _bankDirectoryInBed As String
    Private ReadOnly _workingSettingsInBed As WavetableAttachmentSettings
    Private ReadOnly _installedCheckInBed As New CheckBox()
    Private ReadOnly _profileComboInBed As New ComboBox()
    Private ReadOnly _bankComboInBed As New ComboBox()
    Private ReadOnly _bankListInBed As New ListView()
    Private ReadOnly _statusLabelInBed As New Label()
    Private ReadOnly _pathLabelInBed As New Label()
    Private ReadOnly _okButtonInBed As New Button()
    Private ReadOnly _refreshButtonInBed As New Button()
    Private ReadOnly _openFolderButtonInBed As New Button()
    Private _banksInBed As New List(Of SoundFontBankInfo)()
    Private _updatingInBed As Boolean

    Public Sub New(cardDisplayNameInBed As String,
                   settingsInBed As WavetableAttachmentSettings)
        Me.New(cardDisplayNameInBed, settingsInBed, SoundFontCatalog.ResolveSharedBanksDirectory())
    End Sub

    ' Explicit directory overload is kept Friend for focused tests or a future
    ' packaged layout, but ordinary sound-card UI must use the shared-root form.
    Friend Sub New(cardDisplayNameInBed As String,
                   settingsInBed As WavetableAttachmentSettings,
                   bankDirectoryInBed As String)
        _cardDisplayNameInBed = If(String.IsNullOrWhiteSpace(cardDisplayNameInBed), "Sound card", cardDisplayNameInBed.Trim())
        _workingSettingsInBed = If(settingsInBed, New WavetableAttachmentSettings()).CloneSettings()
        _bankDirectoryInBed = Path.GetFullPath(bankDirectoryInBed)

        Text = _cardDisplayNameInBed & " — wavetable daughterboard"
        FormBorderStyle = FormBorderStyle.FixedDialog
        MaximizeBox = False
        MinimizeBox = False
        ShowInTaskbar = False
        StartPosition = FormStartPosition.CenterParent
        ClientSize = New Size(650, 510)
        BackColor = Color.FromArgb(238, 236, 230)

        BuildUiInBed()
        RefreshCatalogInBed()
        LoadSelectionsInBed()
        UpdateEnabledStateInBed()
    End Sub

    Public ReadOnly Property WavetableSettings As WavetableAttachmentSettings
        Get
            Return _workingSettingsInBed.CloneSettings()
        End Get
    End Property

    Private Sub BuildUiInBed()
        Dim headerInBed As New Panel() With {
            .Left = 12, .Top = 12, .Width = ClientSize.Width - 24, .Height = 86,
            .BackColor = Color.FromArgb(52, 91, 62), .BorderStyle = BorderStyle.FixedSingle
        }
        Controls.Add(headerInBed)

        Dim titleInBed As New Label() With {
            .AutoSize = True, .Left = 14, .Top = 12,
            .ForeColor = Color.WhiteSmoke,
            .Font = New Font(SystemFonts.MessageBoxFont, FontStyle.Bold),
            .Text = "WAVETABLE DAUGHTERBOARD / ROM BANK"
        }
        headerInBed.Controls.Add(titleInBed)

        _installedCheckInBed.Left = 16
        _installedCheckInBed.Top = 47
        _installedCheckInBed.Width = 300
        _installedCheckInBed.ForeColor = Color.WhiteSmoke
        _installedCheckInBed.Text = "Daughterboard installed"
        AddHandler _installedCheckInBed.CheckedChanged, AddressOf InstalledChangedInBed
        headerInBed.Controls.Add(_installedCheckInBed)

        Dim profileLabelInBed As New Label() With {.Left = 14, .Top = 112, .Width = 125, .Text = "Board profile:"}
        Controls.Add(profileLabelInBed)
        _profileComboInBed.Left = 145 : _profileComboInBed.Top = 108 : _profileComboInBed.Width = 485
        _profileComboInBed.DropDownStyle = ComboBoxStyle.DropDownList
        For Each profileInBed As WavetableProfile In WavetableProfileCatalog.AllProfiles()
            _profileComboInBed.Items.Add(profileInBed)
        Next
        If _profileComboInBed.Items.Count > 0 Then _profileComboInBed.SelectedIndex = 0
        Controls.Add(_profileComboInBed)

        Dim bankLabelInBed As New Label() With {.Left = 14, .Top = 150, .Width = 125, .Text = "SoundFont bank:"}
        Controls.Add(bankLabelInBed)
        _bankComboInBed.Left = 145 : _bankComboInBed.Top = 146 : _bankComboInBed.Width = 392
        _bankComboInBed.DropDownStyle = ComboBoxStyle.DropDownList
        AddHandler _bankComboInBed.SelectedIndexChanged, AddressOf BankSelectionChangedInBed
        Controls.Add(_bankComboInBed)

        _refreshButtonInBed.Text = "Refresh"
        _refreshButtonInBed.Left = 544 : _refreshButtonInBed.Top = 145 : _refreshButtonInBed.Width = 86 : _refreshButtonInBed.Height = 28
        AddHandler _refreshButtonInBed.Click, Sub() RefreshCatalogAndSelectionInBed()
        Controls.Add(_refreshButtonInBed)

        _pathLabelInBed.Left = 14 : _pathLabelInBed.Top = 181 : _pathLabelInBed.Width = 523 : _pathLabelInBed.Height = 34
        _pathLabelInBed.AutoEllipsis = True
        _pathLabelInBed.ForeColor = Color.DimGray
        _pathLabelInBed.Text = _bankDirectoryInBed
        Controls.Add(_pathLabelInBed)

        _openFolderButtonInBed.Text = "Open Banks"
        _openFolderButtonInBed.Left = 544 : _openFolderButtonInBed.Top = 180 : _openFolderButtonInBed.Width = 86 : _openFolderButtonInBed.Height = 28
        AddHandler _openFolderButtonInBed.Click, AddressOf OpenBankFolderInBed
        Controls.Add(_openFolderButtonInBed)

        _bankListInBed.Left = 14 : _bankListInBed.Top = 220 : _bankListInBed.Width = 616 : _bankListInBed.Height = 190
        _bankListInBed.View = View.Details
        _bankListInBed.FullRowSelect = True
        _bankListInBed.HideSelection = False
        _bankListInBed.MultiSelect = False
        _bankListInBed.Columns.Add("SoundFont", 350)
        _bankListInBed.Columns.Add("Size", 110)
        _bankListInBed.Columns.Add("Modified", 135)
        AddHandler _bankListInBed.SelectedIndexChanged, AddressOf ListSelectionChangedInBed
        Controls.Add(_bankListInBed)

        _statusLabelInBed.Left = 14 : _statusLabelInBed.Top = 417 : _statusLabelInBed.Width = 616 : _statusLabelInBed.Height = 40
        _statusLabelInBed.ForeColor = Color.DimGray
        Controls.Add(_statusLabelInBed)

        Dim cancelButtonInBed As New Button() With {
            .Text = "Cancel", .Width = 90, .Height = 29,
            .Left = ClientSize.Width - 104, .Top = ClientSize.Height - 43,
            .DialogResult = DialogResult.Cancel
        }
        _okButtonInBed.Text = "Use selection"
        _okButtonInBed.Width = 112 : _okButtonInBed.Height = 29
        _okButtonInBed.Left = cancelButtonInBed.Left - 118 : _okButtonInBed.Top = cancelButtonInBed.Top
        _okButtonInBed.DialogResult = DialogResult.OK
        AddHandler _okButtonInBed.Click, AddressOf CommitSelectionsInBed
        Controls.Add(_okButtonInBed)
        Controls.Add(cancelButtonInBed)
        AcceptButton = _okButtonInBed
        CancelButton = cancelButtonInBed
    End Sub

    Private Sub RefreshCatalogAndSelectionInBed()
        Dim keepBankInBed As String = SelectedBankFileNameInBed()
        If String.IsNullOrWhiteSpace(keepBankInBed) Then keepBankInBed = _workingSettingsInBed.BankFileName
        RefreshCatalogInBed()
        PopulateBankControlsInBed(keepBankInBed)
        UpdateEnabledStateInBed()
    End Sub

    Private Sub RefreshCatalogInBed()
        _banksInBed = SoundFontCatalog.EnumerateBanks(_bankDirectoryInBed)
    End Sub

    Private Sub LoadSelectionsInBed()
        _updatingInBed = True
        Try
            _installedCheckInBed.Checked = _workingSettingsInBed.Installed
            SelectProfileInBed(_workingSettingsInBed.ProfileId)
            PopulateBankControlsInBed(_workingSettingsInBed.BankFileName)
        Finally
            _updatingInBed = False
        End Try
        UpdateStatusInBed()
    End Sub

    Private Sub PopulateBankControlsInBed(preferredBankInBed As String)
        Dim preferredInBed As String = WavetableAttachmentSettings.NormalizeBankFileName(preferredBankInBed)
        _updatingInBed = True
        Try
            _bankComboInBed.Items.Clear()
            _bankListInBed.Items.Clear()

            Dim selectedIndexInBed As Integer = -1
            For indexInBed As Integer = 0 To _banksInBed.Count - 1
                Dim bankInBed As SoundFontBankInfo = _banksInBed(indexInBed)
                _bankComboInBed.Items.Add(bankInBed)
                Dim rowInBed As New ListViewItem(bankInBed.FileName)
                rowInBed.SubItems.Add(FormatBankSizeInBed(bankInBed.Length))
                rowInBed.SubItems.Add(bankInBed.LastWriteTimeUtc.ToLocalTime().ToString("yyyy-MM-dd HH:mm"))
                rowInBed.Tag = bankInBed
                _bankListInBed.Items.Add(rowInBed)
                If String.Equals(bankInBed.FileName, preferredInBed, StringComparison.OrdinalIgnoreCase) Then selectedIndexInBed = indexInBed
            Next

            If selectedIndexInBed >= 0 Then
                _bankComboInBed.SelectedIndex = selectedIndexInBed
                _bankListInBed.Items(selectedIndexInBed).Selected = True
                _bankListInBed.EnsureVisible(selectedIndexInBed)
            Else
                Dim resolutionInBed As SoundFontBankResolution = SoundFontCatalog.ResolveBank(_bankDirectoryInBed, preferredInBed)
                If resolutionInBed.IsAutomaticFallback AndAlso resolutionInBed.ResolvedBank IsNot Nothing Then
                    ' Keep SB16.sf2 as the configured automatic request.  The host
                    ' resolver chooses the smallest bank at power cycle without
                    ' turning that fallback filename into a permanent user choice.
                    _bankComboInBed.Items.Insert(0, New MissingSoundFontChoice(preferredInBed, resolutionInBed.ResolvedBank.FileName))
                    _bankComboInBed.SelectedIndex = 0
                    For Each rowInBed As ListViewItem In _bankListInBed.Items
                        Dim candidateInBed As SoundFontBankInfo = TryCast(rowInBed.Tag, SoundFontBankInfo)
                        rowInBed.Selected = candidateInBed IsNot Nothing AndAlso String.Equals(candidateInBed.FileName, resolutionInBed.ResolvedBank.FileName, StringComparison.OrdinalIgnoreCase)
                    Next
                Else
                    ' Explicit missing choices remain missing.  Only the automatic
                    ' SB16.sf2 request is permitted to use the size fallback.
                    _bankComboInBed.Items.Insert(0, New MissingSoundFontChoice(preferredInBed))
                    _bankComboInBed.SelectedIndex = 0
                End If
            End If
        Finally
            _updatingInBed = False
        End Try
        UpdateStatusInBed()
    End Sub

    Private Sub InstalledChangedInBed(senderInBed As Object, eInBed As EventArgs)
        UpdateEnabledStateInBed()
        UpdateStatusInBed()
    End Sub

    Private Sub UpdateEnabledStateInBed()
        Dim enabledInBed As Boolean = _installedCheckInBed.Checked
        _profileComboInBed.Enabled = enabledInBed
        _bankComboInBed.Enabled = enabledInBed
        _bankListInBed.Enabled = enabledInBed
        _refreshButtonInBed.Enabled = enabledInBed
    End Sub

    Private Sub BankSelectionChangedInBed(senderInBed As Object, eInBed As EventArgs)
        If _updatingInBed Then Return
        Dim bankInBed As SoundFontBankInfo = TryCast(_bankComboInBed.SelectedItem, SoundFontBankInfo)
        If bankInBed IsNot Nothing Then
            For Each rowInBed As ListViewItem In _bankListInBed.Items
                Dim candidateInBed As SoundFontBankInfo = TryCast(rowInBed.Tag, SoundFontBankInfo)
                rowInBed.Selected = candidateInBed IsNot Nothing AndAlso String.Equals(candidateInBed.FileName, bankInBed.FileName, StringComparison.OrdinalIgnoreCase)
            Next
        End If
        UpdateStatusInBed()
    End Sub

    Private Sub ListSelectionChangedInBed(senderInBed As Object, eInBed As EventArgs)
        If _updatingInBed OrElse _bankListInBed.SelectedItems.Count = 0 Then Return
        Dim bankInBed As SoundFontBankInfo = TryCast(_bankListInBed.SelectedItems(0).Tag, SoundFontBankInfo)
        If bankInBed Is Nothing Then Return
        For indexInBed As Integer = 0 To _bankComboInBed.Items.Count - 1
            Dim candidateInBed As SoundFontBankInfo = TryCast(_bankComboInBed.Items(indexInBed), SoundFontBankInfo)
            If candidateInBed IsNot Nothing AndAlso String.Equals(candidateInBed.FileName, bankInBed.FileName, StringComparison.OrdinalIgnoreCase) Then
                _updatingInBed = True
                _bankComboInBed.SelectedIndex = indexInBed
                _updatingInBed = False
                Exit For
            End If
        Next
        UpdateStatusInBed()
    End Sub

    Private Sub CommitSelectionsInBed(senderInBed As Object, eInBed As EventArgs)
        _workingSettingsInBed.Installed = _installedCheckInBed.Checked
        Dim profileInBed As WavetableProfile = TryCast(_profileComboInBed.SelectedItem, WavetableProfile)
        _workingSettingsInBed.ProfileId = If(profileInBed Is Nothing,
                                             WavetableAttachmentSettings.GenericWaveBlasterProfileId,
                                             profileInBed.Id)
        _workingSettingsInBed.BankFileName = SelectedBankFileNameInBed()
    End Sub

    Private Sub SelectProfileInBed(profileIdInBed As String)
        Dim normalizedInBed As String = WavetableAttachmentSettings.NormalizeProfileId(profileIdInBed)
        For indexInBed As Integer = 0 To _profileComboInBed.Items.Count - 1
            Dim profileInBed As WavetableProfile = TryCast(_profileComboInBed.Items(indexInBed), WavetableProfile)
            If profileInBed IsNot Nothing AndAlso String.Equals(profileInBed.Id, normalizedInBed, StringComparison.OrdinalIgnoreCase) Then
                _profileComboInBed.SelectedIndex = indexInBed
                Return
            End If
        Next
        If _profileComboInBed.Items.Count > 0 Then _profileComboInBed.SelectedIndex = 0
    End Sub

    Private Function SelectedBankFileNameInBed() As String
        Dim bankInBed As SoundFontBankInfo = TryCast(_bankComboInBed.SelectedItem, SoundFontBankInfo)
        If bankInBed IsNot Nothing Then Return bankInBed.FileName
        Dim missingInBed As MissingSoundFontChoice = TryCast(_bankComboInBed.SelectedItem, MissingSoundFontChoice)
        If missingInBed IsNot Nothing Then Return missingInBed.FileName
        Return WavetableAttachmentSettings.DefaultSoundFontFileName
    End Function

    Private Sub UpdateStatusInBed()
        If Not _installedCheckInBed.Checked Then
            _statusLabelInBed.ForeColor = Color.DimGray
            _statusLabelInBed.Text = "Daughterboard disabled.  MIDI/MPU hardware remains a property of the sound card; no wavetable bank will be attached."
            Return
        End If

        Dim bankInBed As SoundFontBankInfo = TryCast(_bankComboInBed.SelectedItem, SoundFontBankInfo)
        If bankInBed Is Nothing Then
            Dim requestedInBed As String = SelectedBankFileNameInBed()
            Dim resolutionInBed As SoundFontBankResolution = SoundFontCatalog.ResolveBank(_bankDirectoryInBed, requestedInBed)
            If resolutionInBed.IsAutomaticFallback AndAlso resolutionInBed.ResolvedBank IsNot Nothing Then
                _statusLabelInBed.ForeColor = Color.DarkGoldenrod
                _statusLabelInBed.Text = requestedInBed & " is not present. Automatic startup fallback will use the smallest bank: " & resolutionInBed.ResolvedBank.FileName & " (" & SoundFontBankInfo.FormatByteSize(resolutionInBed.ResolvedBank.Length) & ")."
            ElseIf resolutionInBed.Reason = SoundFontResolutionReason.NoBanksAvailable Then
                _statusLabelInBed.ForeColor = Color.DarkRed
                _statusLabelInBed.Text = "No .sf2 banks are available in the shared Banks directory. The daughterboard remains present but the host renderer will be silent."
            Else
                _statusLabelInBed.ForeColor = Color.DarkRed
                _statusLabelInBed.Text = "Explicitly configured bank is missing: " & requestedInBed & ". No automatic substitution will be made."
            End If
            Return
        End If

        _statusLabelInBed.ForeColor = Color.DarkGreen
        _statusLabelInBed.Text = _banksInBed.Count.ToString() & " .sf2 bank(s) found.  Selected: " & bankInBed.FileName & ".  Selection becomes hardware-effective on the next chassis power cycle."
    End Sub

    Private Sub OpenBankFolderInBed(senderInBed As Object, eInBed As EventArgs)
        Try
            If Not Directory.Exists(_bankDirectoryInBed) Then Directory.CreateDirectory(_bankDirectoryInBed)
            Process.Start(New ProcessStartInfo(_bankDirectoryInBed) With {.UseShellExecute = True})
        Catch ex As Exception
            MessageBox.Show(Me, ex.Message, "Wavetable Banks", MessageBoxButtons.OK, MessageBoxIcon.Error)
        End Try
    End Sub

    Private Shared Function FormatBankSizeInBed(bytesInBed As Long) As String
        Const MiB As Double = 1024.0R * 1024.0R
        Const GiB As Double = MiB * 1024.0R
        If bytesInBed >= GiB Then Return (bytesInBed / GiB).ToString("0.00") & " GiB"
        Return (bytesInBed / MiB).ToString("0.0") & " MiB"
    End Function

    Private NotInheritable Class MissingSoundFontChoice
        Private ReadOnly _fallbackFileNameInBed As String

        Public Sub New(fileNameInBed As String, Optional fallbackFileNameInBed As String = Nothing)
            FileName = WavetableAttachmentSettings.NormalizeBankFileName(fileNameInBed)
            _fallbackFileNameInBed = If(fallbackFileNameInBed, String.Empty)
        End Sub

        Public ReadOnly Property FileName As String

        Public Overrides Function ToString() As String
            If _fallbackFileNameInBed.Length > 0 Then Return "[automatic] " & FileName & " -> " & _fallbackFileNameInBed
            Return "[missing] " & FileName
        End Function
    End Class
End Class
