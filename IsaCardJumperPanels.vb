Imports System
Imports System.Collections.Generic
Imports System.Drawing
Imports System.IO
Imports System.Windows.Forms

' Host-side physical switch/jumper configuration for ISA expansion cards.
' These settings are deliberately outside guest CMOS.  They represent the
' little pieces of plastic and DIP switches on the card itself and therefore
' become electrically effective only after the machine is power-cycled.
Public NotInheritable Class SoundBlaster16JumperSettings
    Public Property BasePort As UInt16 = &H220US
    Public Property Irq As Integer = 10
    Public Property Dma8 As Integer = 1
    Public Property Dma16 As Integer = 5
    Public Property MpuPort As UInt16 = &H330US
    Public Property GamePortEnabled As Boolean = True
    ' The wavetable attachment is physical card/chassis configuration too.  The
    ' settings object is generic so later wavetable-capable sound cards can reuse
    ' the same host configurator rather than growing SB16-specific SoundFont UI.
    Public Property Wavetable As WavetableAttachmentSettings = New WavetableAttachmentSettings()

    Public Function CloneSettings() As SoundBlaster16JumperSettings
        Return New SoundBlaster16JumperSettings With {
            .BasePort = BasePort,
            .Irq = Irq,
            .Dma8 = Dma8,
            .Dma16 = Dma16,
            .MpuPort = MpuPort,
            .GamePortEnabled = GamePortEnabled,
            .Wavetable = If(Wavetable, New WavetableAttachmentSettings()).CloneSettings()
        }
    End Function

    Public Function Summary() As String
        Dim wavetableInBed As WavetableAttachmentSettings = If(Wavetable, New WavetableAttachmentSettings())
        Dim dma16TextInBed As String =
            If(Dma16 = IsaExpansionCardConfiguration.Sb16Dma16ViaLowChannel,
               "low DMA (16-bit over DMA" & Dma8.ToString() & ")",
               "DMA" & Dma16.ToString())
        Return BasePort.ToString("X3") & "h / IRQ" & Irq.ToString() &
               " / DMA" & Dma8.ToString() & "/" & dma16TextInBed &
               " / MPU " & MpuPort.ToString("X3") & "h" &
               If(GamePortEnabled, " / game 201h", " / game disabled") &
               " / " & wavetableInBed.Summary()
    End Function
End Class

Public NotInheritable Class Ne2000JumperSettings
    Public Property BasePort As UInt16 = &H300US
    Public Property Irq As Integer = 11

    Public Function CloneSettings() As Ne2000JumperSettings
        Return New Ne2000JumperSettings With {.BasePort = BasePort, .Irq = Irq}
    End Function

    Public Function Summary() As String
        Return BasePort.ToString("X3") & "h / IRQ" & Irq.ToString()
    End Function
End Class

Public NotInheritable Class IsaExpansionCardConfiguration
    Private Const MachineConfigurationName As String = "VirtualComputer.machine.ini"
    Private ReadOnly _configurationPath As String

    Public Const Sb16Dma16ViaLowChannel As Integer = -1
    Public Shared ReadOnly Sb16BasePorts As UInt16() = {&H220US, &H240US, &H260US, &H280US}
    Public Shared ReadOnly Sb16Irqs As Integer() = {5, 7, 9, 10}
    Public Shared ReadOnly Sb16Dma8Channels As Integer() = {0, 1, 3}
    Public Shared ReadOnly Sb16Dma16Channels As Integer() = {Sb16Dma16ViaLowChannel, 5, 6, 7}
    Public Shared ReadOnly Sb16MpuPorts As UInt16() = {&H300US, &H330US}
    Public Shared ReadOnly Ne2000BasePorts As UInt16() = {&H280US, &H300US, &H320US, &H340US, &H360US}
    Public Shared ReadOnly Ne2000Irqs As Integer() = {3, 4, 5, 9, 10, 11, 12, 15}

    Public Sub New(baseDirectory As String)
        If String.IsNullOrWhiteSpace(baseDirectory) Then Throw New ArgumentNullException(NameOf(baseDirectory))
        _configurationPath = Path.Combine(Path.GetFullPath(baseDirectory), MachineConfigurationName)
    End Sub

    Public Property SoundBlaster16 As New SoundBlaster16JumperSettings()
    Public Property Ne2000 As New Ne2000JumperSettings()

    Public ReadOnly Property ConfigurationPath As String
        Get
            Return _configurationPath
        End Get
    End Property

    Public Sub LoadConfiguration()
        Dim sb As New SoundBlaster16JumperSettings()
        sb.BasePort = ReadHexChoice("SoundBlaster16", "BasePort", sb.BasePort, Sb16BasePorts)
        sb.Irq = ReadIntChoice("SoundBlaster16", "IRQ", sb.Irq, Sb16Irqs)
        sb.Dma8 = ReadIntChoice("SoundBlaster16", "DMA8", sb.Dma8, Sb16Dma8Channels)
        sb.Dma16 = ReadIntChoice("SoundBlaster16", "DMA16", sb.Dma16, Sb16Dma16Channels)
        sb.MpuPort = ReadHexChoice("SoundBlaster16", "MPUPort", sb.MpuPort, Sb16MpuPorts)
        Dim gameText As String = MachineConfigurationStore.ReadValue(_configurationPath, "SoundBlaster16", "GamePortEnabled", "True")
        Dim gameEnabled As Boolean
        If Boolean.TryParse(gameText, gameEnabled) Then sb.GamePortEnabled = gameEnabled

        Dim wavetableInBed As New WavetableAttachmentSettings()
        Dim wtInstalledTextInBed As String = MachineConfigurationStore.ReadValue(_configurationPath, "SoundBlaster16", "WavetableInstalled", "True")
        Dim wtInstalledInBed As Boolean
        If Boolean.TryParse(wtInstalledTextInBed, wtInstalledInBed) Then wavetableInBed.Installed = wtInstalledInBed
        wavetableInBed.ProfileId = WavetableAttachmentSettings.NormalizeProfileId(
            MachineConfigurationStore.ReadValue(_configurationPath, "SoundBlaster16", "WavetableProfile", WavetableAttachmentSettings.GenericWaveBlasterProfileId))
        Dim wtBankRawInBed As String = MachineConfigurationStore.ReadValue(_configurationPath, "SoundBlaster16", "WavetableBank", WavetableAttachmentSettings.DefaultSoundFontFileName)
        Dim wtPolicyRawInBed As String = MachineConfigurationStore.ReadValue(_configurationPath, "SoundBlaster16", "WavetableBankPolicyVersion", "1")
        Dim wtPolicyInBed As Integer = 1
        Integer.TryParse(wtPolicyRawInBed, wtPolicyInBed)
        Dim normalizedWtBankInBed As String = WavetableAttachmentSettings.NormalizeBankFileName(wtBankRawInBed)
        If wtPolicyInBed < WavetableAttachmentSettings.CurrentBankPolicyVersion AndAlso
           String.Equals(normalizedWtBankInBed, WavetableAttachmentSettings.LegacyDefaultSoundFontFileName, StringComparison.OrdinalIgnoreCase) Then
            ' Bricks 1/2 persisted uo_4mb_2.sf2 as the old factory default.  Migrate
            ' that unversioned default to the new automatic SB16.sf2 policy so an
            ' existing test machine actually receives the new smallest-bank fallback.
            normalizedWtBankInBed = WavetableAttachmentSettings.DefaultSoundFontFileName
        End If
        wavetableInBed.BankFileName = normalizedWtBankInBed
        sb.Wavetable = wavetableInBed

        Dim ne As New Ne2000JumperSettings()
        ne.BasePort = ReadHexChoice("NE2000", "BasePort", ne.BasePort, Ne2000BasePorts)
        ne.Irq = ReadIntChoice("NE2000", "IRQ", ne.Irq, Ne2000Irqs)

        SoundBlaster16 = sb
        Ne2000 = ne
    End Sub

    Public Sub SaveConfiguration()
        Dim sb As SoundBlaster16JumperSettings = If(SoundBlaster16, New SoundBlaster16JumperSettings())
        Dim ne As Ne2000JumperSettings = If(Ne2000, New Ne2000JumperSettings())
        MachineConfigurationStore.WriteValue(_configurationPath, "SoundBlaster16", "BasePort", sb.BasePort.ToString("X3"))
        MachineConfigurationStore.WriteValue(_configurationPath, "SoundBlaster16", "IRQ", sb.Irq.ToString())
        MachineConfigurationStore.WriteValue(_configurationPath, "SoundBlaster16", "DMA8", sb.Dma8.ToString())
        MachineConfigurationStore.WriteValue(_configurationPath, "SoundBlaster16", "DMA16", sb.Dma16.ToString())
        MachineConfigurationStore.WriteValue(_configurationPath, "SoundBlaster16", "MPUPort", sb.MpuPort.ToString("X3"))
        MachineConfigurationStore.WriteValue(_configurationPath, "SoundBlaster16", "GamePortEnabled", sb.GamePortEnabled.ToString())
        Dim wavetableInBed As WavetableAttachmentSettings = If(sb.Wavetable, New WavetableAttachmentSettings())
        MachineConfigurationStore.WriteValue(_configurationPath, "SoundBlaster16", "WavetableInstalled", wavetableInBed.Installed.ToString())
        MachineConfigurationStore.WriteValue(_configurationPath, "SoundBlaster16", "WavetableProfile", WavetableAttachmentSettings.NormalizeProfileId(wavetableInBed.ProfileId))
        MachineConfigurationStore.WriteValue(_configurationPath, "SoundBlaster16", "WavetableBank", WavetableAttachmentSettings.NormalizeBankFileName(wavetableInBed.BankFileName))
        MachineConfigurationStore.WriteValue(_configurationPath, "SoundBlaster16", "WavetableBankPolicyVersion", WavetableAttachmentSettings.CurrentBankPolicyVersion.ToString())
        MachineConfigurationStore.WriteValue(_configurationPath, "NE2000", "BasePort", ne.BasePort.ToString("X3"))
        MachineConfigurationStore.WriteValue(_configurationPath, "NE2000", "IRQ", ne.Irq.ToString())
    End Sub

    Private Function ReadHexChoice(section As String, key As String, fallback As UInt16, allowed As UInt16()) As UInt16
        Dim raw As String = MachineConfigurationStore.ReadValue(_configurationPath, section, key, fallback.ToString("X3")).Trim()
        If raw.EndsWith("h", StringComparison.OrdinalIgnoreCase) Then raw = raw.Substring(0, raw.Length - 1)
        Dim parsed As Integer
        If Integer.TryParse(raw, Globalization.NumberStyles.HexNumber, Globalization.CultureInfo.InvariantCulture, parsed) Then
            Dim value As UInt16 = CUShort(parsed And &HFFFF)
            If Array.IndexOf(allowed, value) >= 0 Then Return value
        End If
        Return fallback
    End Function

    Private Function ReadIntChoice(section As String, key As String, fallback As Integer, allowed As Integer()) As Integer
        Dim raw As String = MachineConfigurationStore.ReadValue(_configurationPath, section, key, fallback.ToString())
        Dim parsed As Integer
        If Integer.TryParse(raw, parsed) AndAlso Array.IndexOf(allowed, parsed) >= 0 Then Return parsed
        Return fallback
    End Function
End Class

Public NotInheritable Class IsaResourceConflictDetector
    Private Sub New()
    End Sub

    Private NotInheritable Class IoClaim
        Public ReadOnly StartPort As Integer
        Public ReadOnly EndPort As Integer
        Public ReadOnly Owner As String
        Public Sub New(startInBed As Integer, endInBed As Integer, ownerInBed As String)
            StartPort = startInBed : EndPort = endInBed : Owner = ownerInBed
        End Sub
    End Class

    Public Shared Function Validate(sb As SoundBlaster16JumperSettings,
                                    ne As Ne2000JumperSettings,
                                    Optional soundBlasterInstalledInBed As Boolean = True,
                                    Optional ne2000InstalledInBed As Boolean = True,
                                    Optional numericCoprocessorInstalledInBed As Boolean = True) As List(Of String)
        Dim conflicts As New List(Of String)()
        If soundBlasterInstalledInBed AndAlso sb Is Nothing Then
            conflicts.Add("The installed Sound Blaster 16 has no jumper configuration.")
            Return conflicts
        End If
        If ne2000InstalledInBed AndAlso ne Is Nothing Then
            conflicts.Add("The installed NE2000 has no jumper configuration.")
            Return conflicts
        End If

        If soundBlasterInstalledInBed Then
            If Array.IndexOf(IsaExpansionCardConfiguration.Sb16BasePorts, sb.BasePort) < 0 Then conflicts.Add("SB16 base I/O selection is not supported.")
            If Array.IndexOf(IsaExpansionCardConfiguration.Sb16Irqs, sb.Irq) < 0 Then conflicts.Add("SB16 IRQ selection is not supported.")
            If Array.IndexOf(IsaExpansionCardConfiguration.Sb16Dma8Channels, sb.Dma8) < 0 Then conflicts.Add("SB16 8-bit DMA selection is not supported.")
            If Array.IndexOf(IsaExpansionCardConfiguration.Sb16Dma16Channels, sb.Dma16) < 0 Then conflicts.Add("SB16 16-bit DMA selection is not supported.")
            If Array.IndexOf(IsaExpansionCardConfiguration.Sb16MpuPorts, sb.MpuPort) < 0 Then conflicts.Add("SB16 MPU-401 I/O selection is not supported.")
        End If
        If ne2000InstalledInBed Then
            If Array.IndexOf(IsaExpansionCardConfiguration.Ne2000BasePorts, ne.BasePort) < 0 Then conflicts.Add("NE2000 base I/O selection is not supported.")
            If Array.IndexOf(IsaExpansionCardConfiguration.Ne2000Irqs, ne.Irq) < 0 Then conflicts.Add("NE2000 IRQ selection is not supported.")
        End If

        Dim fixedClaims As New List(Of IoClaim) From {
            New IoClaim(&H20, &H21, "master PIC"),
            New IoClaim(&H40, &H43, "PIT"),
            New IoClaim(&H60, &H64, "keyboard/controller"),
            New IoClaim(&H70, &H71, "RTC/CMOS"),
            New IoClaim(&H170, &H177, "secondary IDE"),
            New IoClaim(&H1F0, &H1F7, "primary IDE"),
            New IoClaim(&H278, &H27A, "LPT2"),
            New IoClaim(&H2F8, &H2FF, "COM2"),
            New IoClaim(&H376, &H376, "secondary IDE control"),
            New IoClaim(&H378, &H37A, "LPT1"),
            New IoClaim(&H3F2, &H3F5, "floppy controller"),
            New IoClaim(&H3F6, &H3F6, "primary IDE control"),
            New IoClaim(&H3F7, &H3F7, "floppy controller"),
            New IoClaim(&H3F8, &H3FF, "COM1"),
            New IoClaim(&HA0, &HA1, "slave PIC")
        }

        Dim cardClaims As New List(Of IoClaim)()
        If soundBlasterInstalledInBed Then
            cardClaims.Add(New IoClaim(sb.BasePort, sb.BasePort + &HF, "Sound Blaster 16"))
            cardClaims.Add(New IoClaim(sb.MpuPort, sb.MpuPort + 1, "SB16 MPU-401"))
            cardClaims.Add(New IoClaim(&H388, &H38B, "SB16 OPL2/OPL3"))
            If sb.GamePortEnabled Then cardClaims.Add(New IoClaim(&H201, &H201, "SB16 game port"))
        End If
        If ne2000InstalledInBed Then cardClaims.Add(New IoClaim(ne.BasePort, ne.BasePort + &H1F, "NE2000"))

        For Each card As IoClaim In cardClaims
            For Each fixedClaim As IoClaim In fixedClaims
                If Overlaps(card, fixedClaim) Then
                    conflicts.Add(card.Owner & " I/O " & RangeText(card) & " overlaps " & fixedClaim.Owner & " " & RangeText(fixedClaim) & ".")
                End If
            Next
        Next
        For i As Integer = 0 To cardClaims.Count - 2
            For j As Integer = i + 1 To cardClaims.Count - 1
                If Overlaps(cardClaims(i), cardClaims(j)) Then
                    conflicts.Add(cardClaims(i).Owner & " I/O " & RangeText(cardClaims(i)) & " overlaps " & cardClaims(j).Owner & " " & RangeText(cardClaims(j)) & ".")
                End If
            Next
        Next

        Dim fixedIrqs As New Dictionary(Of Integer, String) From {
            {1, "keyboard"}, {3, "COM2"}, {4, "COM1"}, {5, "LPT2"}, {6, "floppy"},
            {7, "LPT1"}, {8, "RTC"}, {14, "primary IDE"}, {15, "secondary IDE"}
        }
        If numericCoprocessorInstalledInBed Then fixedIrqs(13) = "numeric coprocessor"
        Dim owner As String = Nothing
        If soundBlasterInstalledInBed AndAlso fixedIrqs.TryGetValue(sb.Irq, owner) Then conflicts.Add("SB16 IRQ" & sb.Irq.ToString() & " conflicts with " & owner & ".")
        If ne2000InstalledInBed AndAlso fixedIrqs.TryGetValue(ne.Irq, owner) Then conflicts.Add("NE2000 IRQ" & ne.Irq.ToString() & " conflicts with " & owner & ".")
        If soundBlasterInstalledInBed AndAlso ne2000InstalledInBed AndAlso sb.Irq = ne.Irq Then conflicts.Add("SB16 and NE2000 are both strapped to IRQ" & sb.Irq.ToString() & ".")

        Return conflicts
    End Function

    Private Shared Function Overlaps(a As IoClaim, b As IoClaim) As Boolean
        Return a.StartPort <= b.EndPort AndAlso b.StartPort <= a.EndPort
    End Function

    Private Shared Function RangeText(c As IoClaim) As String
        If c.StartPort = c.EndPort Then Return c.StartPort.ToString("X3") & "h"
        Return c.StartPort.ToString("X3") & "h-" & c.EndPort.ToString("X3") & "h"
    End Function
End Class

Friend Enum IsaJumperCardKind
    SoundBlaster16
    Ne2000
End Enum

' One physical-looking setup panel is used for both cards.  The controls are
' deliberately labeled as jumpers/DIP switches rather than as guest software
' preferences; pressing Stage does not touch the live ISA decoder.
Friend NotInheritable Class IsaCardJumperDialog
    Inherits Form

    Private ReadOnly _kind As IsaJumperCardKind
    Private ReadOnly _otherSb As SoundBlaster16JumperSettings
    Private ReadOnly _otherNe As Ne2000JumperSettings
    Private ReadOnly _soundBlasterInstalledInBed As Boolean
    Private ReadOnly _ne2000InstalledInBed As Boolean
    Private ReadOnly _baseCombo As New ComboBox()
    Private ReadOnly _irqCombo As New ComboBox()
    Private ReadOnly _dma8Combo As New ComboBox()
    Private ReadOnly _dma16Combo As New ComboBox()
    Private ReadOnly _mpuCombo As New ComboBox()
    Private ReadOnly _gameCheck As New CheckBox()
    Private ReadOnly _wavetableButton As New Button()
    Private ReadOnly _wavetableValue As New Label()
    Private ReadOnly _conflictLabel As New Label()
    Private ReadOnly _stageButton As New Button()

    Public ReadOnly Property SoundBlasterSettings As SoundBlaster16JumperSettings
    Public ReadOnly Property Ne2000Settings As Ne2000JumperSettings

    Public Sub New(kind As IsaJumperCardKind,
                   sb As SoundBlaster16JumperSettings,
                   ne As Ne2000JumperSettings,
                   Optional soundBlasterInstalledInBed As Boolean = True,
                   Optional ne2000InstalledInBed As Boolean = True)
        _kind = kind
        _soundBlasterInstalledInBed = soundBlasterInstalledInBed
        _ne2000InstalledInBed = ne2000InstalledInBed
        _otherSb = If(sb, New SoundBlaster16JumperSettings()).CloneSettings()
        _otherNe = If(ne, New Ne2000JumperSettings()).CloneSettings()
        SoundBlasterSettings = _otherSb.CloneSettings()
        Ne2000Settings = _otherNe.CloneSettings()

        Text = If(kind = IsaJumperCardKind.SoundBlaster16, "Sound Blaster 16 — card configuration", "NE2000 — jumpers / DIP switches")
        FormBorderStyle = FormBorderStyle.FixedDialog
        MaximizeBox = False
        MinimizeBox = False
        ShowInTaskbar = False
        StartPosition = FormStartPosition.CenterParent
        ClientSize = New Size(474, If(kind = IsaJumperCardKind.SoundBlaster16, 565, 380))
        BackColor = Color.FromArgb(232, 226, 210)

        BuildUi()
        LoadSelections()
        ValidateSelections()
    End Sub

    Private Sub BuildUi()
        Dim cardPanel As New Panel() With {
            .Left = 14, .Top = 14, .Width = ClientSize.Width - 28,
            .Height = If(_kind = IsaJumperCardKind.SoundBlaster16, 390, 205),
            .BackColor = Color.FromArgb(52, 91, 62),
            .BorderStyle = BorderStyle.FixedSingle
        }
        Controls.Add(cardPanel)

        Dim silk As New Label() With {
            .AutoSize = True, .Left = 14, .Top = 10,
            .ForeColor = Color.WhiteSmoke,
            .Font = New Font(SystemFonts.MessageBoxFont, FontStyle.Bold),
            .Text = If(_kind = IsaJumperCardKind.SoundBlaster16,
                       "CT17xx 16-BIT ISA  •  CONFIGURATION BLOCK",
                       "NE2000 / DP8390 16-BIT ISA  •  JUMPER BLOCK")
        }
        cardPanel.Controls.Add(silk)

        AddJumperRow(cardPanel, 48, "I/O BASE", _baseCombo)
        AddJumperRow(cardPanel, 88, "IRQ", _irqCombo)
        If _kind = IsaJumperCardKind.SoundBlaster16 Then
            AddJumperRow(cardPanel, 128, "8-BIT DMA", _dma8Combo)
            AddJumperRow(cardPanel, 168, "16-BIT DMA", _dma16Combo)
            AddJumperRow(cardPanel, 208, "MPU-401", _mpuCombo)
            _gameCheck.Left = 166 : _gameCheck.Top = 252 : _gameCheck.Width = 230
            _gameCheck.ForeColor = Color.WhiteSmoke
            _gameCheck.Text = "JP-GAME  201h enabled"
            _gameCheck.AutoSize = False
            AddHandler _gameCheck.CheckedChanged, AddressOf SelectionChanged
            cardPanel.Controls.Add(_gameCheck)

            Dim wavetableCaptionInBed As New Label() With {
                .Left = 18, .Top = 301, .Width = 132, .Height = 24,
                .ForeColor = Color.WhiteSmoke,
                .Text = "DAUGHTERBOARD",
                .Font = New Font(SystemFonts.MessageBoxFont, FontStyle.Bold)
            }
            cardPanel.Controls.Add(wavetableCaptionInBed)

            _wavetableButton.Left = 166 : _wavetableButton.Top = 292 : _wavetableButton.Width = 245 : _wavetableButton.Height = 29
            _wavetableButton.Text = "Wavetable / ROM bank..."
            AddHandler _wavetableButton.Click, AddressOf ConfigureWavetableInBed
            cardPanel.Controls.Add(_wavetableButton)

            _wavetableValue.Left = 166 : _wavetableValue.Top = 327 : _wavetableValue.Width = 245 : _wavetableValue.Height = 42
            _wavetableValue.ForeColor = Color.Gainsboro
            _wavetableValue.AutoEllipsis = True
            cardPanel.Controls.Add(_wavetableValue)
        End If

        Dim note As New Label() With {
            .Left = 14, .Top = cardPanel.Bottom + 8, .Width = ClientSize.Width - 28, .Height = 34,
            .ForeColor = Color.FromArgb(62, 55, 43),
            .Text = If(_kind = IsaJumperCardKind.SoundBlaster16,
                       "Card straps and wavetable daughterboard/ROM selection are staged here and become active only after a virtual chassis power cycle.",
                       "These are physical card straps. Changes are staged now and become active only after a virtual chassis power cycle.")
        }
        Controls.Add(note)

        _conflictLabel.Left = 14
        _conflictLabel.Top = note.Bottom + 2
        _conflictLabel.Width = ClientSize.Width - 28
        _conflictLabel.Height = 42
        _conflictLabel.ForeColor = Color.DarkRed
        Controls.Add(_conflictLabel)

        Dim cancelButton As New Button() With {.Text = "Cancel", .Width = 90, .Height = 29, .Left = ClientSize.Width - 104, .Top = ClientSize.Height - 43}
        _stageButton.Text = If(_kind = IsaJumperCardKind.SoundBlaster16, "Stage card", "Stage jumpers")
        _stageButton.Width = 112 : _stageButton.Height = 29
        _stageButton.Left = cancelButton.Left - 118 : _stageButton.Top = cancelButton.Top
        _stageButton.DialogResult = DialogResult.OK
        cancelButton.DialogResult = DialogResult.Cancel
        Controls.Add(_stageButton) : Controls.Add(cancelButton)
        AcceptButton = _stageButton : CancelButton = cancelButton
    End Sub

    Private Sub AddJumperRow(parent As Control, top As Integer, caption As String, combo As ComboBox)
        Dim label As New Label() With {
            .Left = 18, .Top = top + 5, .Width = 132, .Height = 24,
            .ForeColor = Color.WhiteSmoke,
            .Text = "JP  " & caption,
            .Font = New Font(SystemFonts.MessageBoxFont, FontStyle.Bold)
        }
        combo.Left = 166 : combo.Top = top : combo.Width = 245 : combo.Height = 26
        combo.DropDownStyle = ComboBoxStyle.DropDownList
        AddHandler combo.SelectedIndexChanged, AddressOf SelectionChanged
        parent.Controls.Add(label) : parent.Controls.Add(combo)
    End Sub

    Private Sub LoadSelections()
        If _kind = IsaJumperCardKind.SoundBlaster16 Then
            FillHex(_baseCombo, IsaExpansionCardConfiguration.Sb16BasePorts, SoundBlasterSettings.BasePort)
            FillInts(_irqCombo, IsaExpansionCardConfiguration.Sb16Irqs, SoundBlasterSettings.Irq, Function(v) If(v = 9, "IRQ 2/9", "IRQ " & v.ToString()))
            FillInts(_dma8Combo, IsaExpansionCardConfiguration.Sb16Dma8Channels, SoundBlasterSettings.Dma8, Function(v) "DMA " & v.ToString())
            FillInts(_dma16Combo, IsaExpansionCardConfiguration.Sb16Dma16Channels, SoundBlasterSettings.Dma16,
                     Function(v) If(v = IsaExpansionCardConfiguration.Sb16Dma16ViaLowChannel,
                                    "Use low DMA for 16-bit",
                                    "DMA " & v.ToString()))
            FillHex(_mpuCombo, IsaExpansionCardConfiguration.Sb16MpuPorts, SoundBlasterSettings.MpuPort)
            _gameCheck.Checked = SoundBlasterSettings.GamePortEnabled
            If SoundBlasterSettings.Wavetable Is Nothing Then SoundBlasterSettings.Wavetable = New WavetableAttachmentSettings()
            UpdateWavetableValueInBed()
        Else
            FillHex(_baseCombo, IsaExpansionCardConfiguration.Ne2000BasePorts, Ne2000Settings.BasePort)
            FillInts(_irqCombo, IsaExpansionCardConfiguration.Ne2000Irqs, Ne2000Settings.Irq, Function(v) If(v = 9, "IRQ 2/9", "IRQ " & v.ToString()))
        End If
    End Sub

    Private Sub ConfigureWavetableInBed(senderInBed As Object, eInBed As EventArgs)
        If _kind <> IsaJumperCardKind.SoundBlaster16 Then Return
        Dim currentInBed As WavetableAttachmentSettings = If(SoundBlasterSettings.Wavetable, New WavetableAttachmentSettings()).CloneSettings()
        Using dialogInBed As New WavetableConfigForm("Sound Blaster 16", currentInBed)
            If dialogInBed.ShowDialog(Me) = DialogResult.OK Then
                SoundBlasterSettings.Wavetable = dialogInBed.WavetableSettings
                UpdateWavetableValueInBed()
                ValidateSelections()
            End If
        End Using
    End Sub

    Private Sub UpdateWavetableValueInBed()
        If _kind <> IsaJumperCardKind.SoundBlaster16 Then Return
        Dim wavetableInBed As WavetableAttachmentSettings = If(SoundBlasterSettings.Wavetable, New WavetableAttachmentSettings())
        _wavetableValue.Text = If(wavetableInBed.Installed,
                                  "Installed — " & WavetableAttachmentSettings.NormalizeBankFileName(wavetableInBed.BankFileName),
                                  "Not installed")
    End Sub

    Private Shared Sub FillHex(combo As ComboBox, choices As UInt16(), selected As UInt16)
        combo.Items.Clear()
        For Each value As UInt16 In choices : combo.Items.Add(value.ToString("X3") & "h") : Next
        Dim at As Integer = Array.IndexOf(choices, selected)
        combo.SelectedIndex = If(at >= 0, at, 0)
    End Sub

    Private Shared Sub FillInts(combo As ComboBox, choices As Integer(), selected As Integer, formatter As Func(Of Integer, String))
        combo.Items.Clear()
        For Each value As Integer In choices : combo.Items.Add(formatter(value)) : Next
        Dim at As Integer = Array.IndexOf(choices, selected)
        combo.SelectedIndex = If(at >= 0, at, 0)
    End Sub

    Private Sub SelectionChanged(sender As Object, e As EventArgs)
        ValidateSelections()
    End Sub

    Private Sub ValidateSelections()
        If _baseCombo.SelectedIndex < 0 OrElse _irqCombo.SelectedIndex < 0 Then Return
        If _kind = IsaJumperCardKind.SoundBlaster16 Then
            If _dma8Combo.SelectedIndex < 0 OrElse _dma16Combo.SelectedIndex < 0 OrElse _mpuCombo.SelectedIndex < 0 Then Return
            SoundBlasterSettings.BasePort = IsaExpansionCardConfiguration.Sb16BasePorts(_baseCombo.SelectedIndex)
            SoundBlasterSettings.Irq = IsaExpansionCardConfiguration.Sb16Irqs(_irqCombo.SelectedIndex)
            SoundBlasterSettings.Dma8 = IsaExpansionCardConfiguration.Sb16Dma8Channels(_dma8Combo.SelectedIndex)
            SoundBlasterSettings.Dma16 = IsaExpansionCardConfiguration.Sb16Dma16Channels(_dma16Combo.SelectedIndex)
            SoundBlasterSettings.MpuPort = IsaExpansionCardConfiguration.Sb16MpuPorts(_mpuCombo.SelectedIndex)
            SoundBlasterSettings.GamePortEnabled = _gameCheck.Checked
        Else
            Ne2000Settings.BasePort = IsaExpansionCardConfiguration.Ne2000BasePorts(_baseCombo.SelectedIndex)
            Ne2000Settings.Irq = IsaExpansionCardConfiguration.Ne2000Irqs(_irqCombo.SelectedIndex)
        End If

        Dim conflicts As List(Of String) = IsaResourceConflictDetector.Validate(SoundBlasterSettings,
                                                                                 Ne2000Settings,
                                                                                 _soundBlasterInstalledInBed,
                                                                                 _ne2000InstalledInBed,
                                                                                 numericCoprocessorInstalledInBed:=False)
        If conflicts.Count = 0 Then
            _conflictLabel.ForeColor = Color.DarkGreen
            _conflictLabel.Text = "Resource map clean — no ISA I/O or IRQ collisions detected."
            _stageButton.Enabled = True
        Else
            _conflictLabel.ForeColor = Color.DarkRed
            _conflictLabel.Text = "Conflict: " & conflicts(0)
            If conflicts.Count > 1 Then _conflictLabel.Text &= "  (+" & (conflicts.Count - 1).ToString() & " more)"
            _stageButton.Enabled = False
        End If
    End Sub
End Class
