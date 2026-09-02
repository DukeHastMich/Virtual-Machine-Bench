Imports System
Imports System.Collections.Generic
Imports System.Diagnostics
Imports System.Drawing
Imports System.IO
Imports System.Drawing.Drawing2D
Imports System.Runtime.InteropServices
Imports System.Windows.Forms

' Host-side description of the physical machine currently asserted by the emulator.
' This is deliberately not guest-visible configuration state.  It is the source
' consumed by the System Configuration drawer so the UI reports the machine that
' is actually assembled, not a second set of disconnected preference variables.
Public NotInheritable Class SystemConfigurationSnapshot
    Public Property Chassis As String
    Public Property PowerSupply As String
    Public Property Motherboard As String
    Public Property Chipset As String
    Public Property ChipsetCpuBus As String
    Public Property ChipsetMemory As String
    Public Property ChipsetPeripheral As String
    Public Property ChipsetBuffer As String
    Public Property ChipsetConfigurationPorts As String
    Public Property ChipsetTiming As String
    Public Property Bios As String
    Public Property Cpu As String
    Public Property CpuClock As String
    Public Property HostExecutionRatePercent As Integer
    Public Property HostExecutionRate As String
    Public Property NumericCoprocessor As String
    Public Property Memory As String
    Public Property InstalledMemoryMb As Integer
    Public Property PendingMemoryMb As Integer?
    Public Property PendingHardwareSummary As String
    Public Property Video As String
    Public Property VideoRom As String
    Public Property ExpansionAudio As String
    Public Property Speaker As String
    Public Property Network As String
    Public Property FloppyController As String
    Public Property FloppyDriveA As String
    Public Property FloppyDriveB As String
    Public Property FloppyMediaA As String
    Public Property FloppyMediaB As String
    Public Property FloppyMediaSourceIdA As String
    Public Property FloppyMediaSourceIdB As String
    Public Property HardDiskController As String
    Public Property HardDisk0 As String
    Public Property Optical As String
    Public Property Keyboard As String
    Public Property Com1 As String
    Public Property Com2 As String
    Public Property Lpt1 As String
    Public Property Lpt2 As String
    Public Property GamePort As String
    Public Property Midi As String
    Public Property ResourceSummary As String
End Class

Friend NotInheritable Class HardwareProfileChoice
    Public ReadOnly Property Id As String
    Public ReadOnly Property DisplayName As String

    Public Sub New(idInBed As String, displayNameInBed As String)
        Id = If(idInBed, String.Empty)
        DisplayName = If(displayNameInBed, String.Empty)
    End Sub

    Public Overrides Function ToString() As String
        Return DisplayName
    End Function
End Class

' A tiny bronze host-side gear switch for the chassis annunciator strip.
' It is stationary at rest.  While the configuration drawer moves, it rolls in
' the same screen direction as the drawer: left/open = counter-clockwise,
' right/close = clockwise.  Nothing in this control is guest-visible.
Public NotInheritable Class ConfigurationGearButton
    Inherits Control

    Private ReadOnly _spinTimerInBed As New Timer()
    Private _angleInBed As Single
    Private _spinDirectionInBed As Integer
    Private _hotInBed As Boolean

    Public Sub New()
        SetStyle(ControlStyles.AllPaintingInWmPaint Or
                 ControlStyles.OptimizedDoubleBuffer Or
                 ControlStyles.ResizeRedraw Or
                 ControlStyles.UserPaint Or
                 ControlStyles.SupportsTransparentBackColor, True)
        Size = New Size(18, 18)
        BackColor = Color.Transparent
        Cursor = Cursors.Hand
        TabStop = False
        AccessibleName = "System Configuration"
        AccessibleDescription = "Open or close the System Configuration drawer"

        _spinTimerInBed.Interval = 32
        AddHandler _spinTimerInBed.Tick,
            Sub()
                If _spinDirectionInBed = 0 Then
                    _spinTimerInBed.Stop()
                    Return
                End If
                _angleInBed = (_angleInBed + CSng(_spinDirectionInBed * 12)) Mod 360.0F
                Invalidate()
            End Sub
    End Sub

    Public Sub SetMotionInBed(directionInBed As Integer, movingInBed As Boolean)
        If Not movingInBed OrElse directionInBed = 0 Then
            _spinDirectionInBed = 0
            _spinTimerInBed.Stop()
            Invalidate()
            Return
        End If

        _spinDirectionInBed = Math.Sign(directionInBed)
        If Not _spinTimerInBed.Enabled Then _spinTimerInBed.Start()
    End Sub

    Protected Overrides Sub OnMouseEnter(e As EventArgs)
        _hotInBed = True
        Invalidate()
        MyBase.OnMouseEnter(e)
    End Sub

    Protected Overrides Sub OnMouseLeave(e As EventArgs)
        _hotInBed = False
        Invalidate()
        MyBase.OnMouseLeave(e)
    End Sub

    Protected Overrides Sub OnPaint(e As PaintEventArgs)
        MyBase.OnPaint(e)
        e.Graphics.SmoothingMode = SmoothingMode.AntiAlias

        If _hotInBed Then
            Using hotBrushInBed As New SolidBrush(Color.FromArgb(34, 184, 115, 51))
                e.Graphics.FillEllipse(hotBrushInBed, 0, 0, Width - 1, Height - 1)
            End Using
        End If

        Dim cxInBed As Single = Width / 2.0F
        Dim cyInBed As Single = Height / 2.0F
        Dim outerRadiusInBed As Single = Math.Max(5.5F, Math.Min(Width, Height) * 0.43F)
        Dim rootRadiusInBed As Single = outerRadiusInBed * 0.73F
        Dim pointsInBed As New List(Of PointF)()
        Const teethInBed As Integer = 9
        For indexInBed As Integer = 0 To teethInBed * 4 - 1
            Dim phaseInBed As Integer = indexInBed Mod 4
            Dim radiusInBed As Single = If(phaseInBed = 0 OrElse phaseInBed = 1,
                                           outerRadiusInBed,
                                           rootRadiusInBed)
            Dim radiansInBed As Double =
                (indexInBed * Math.PI * 2.0 / (teethInBed * 4.0)) - (Math.PI / 2.0)
            pointsInBed.Add(New PointF(CSng(Math.Cos(radiansInBed) * radiusInBed),
                                       CSng(Math.Sin(radiansInBed) * radiusInBed)))
        Next

        Dim stateInBed As GraphicsState = e.Graphics.Save()
        e.Graphics.TranslateTransform(cxInBed, cyInBed)
        e.Graphics.RotateTransform(_angleInBed)

        Dim bronzeInBed As Color = If(_hotInBed,
                                      Color.FromArgb(205, 139, 68),
                                      Color.FromArgb(176, 108, 48))
        Using gearBrushInBed As New SolidBrush(bronzeInBed),
              outlineInBed As New Pen(Color.FromArgb(89, 50, 22), 1.0F)
            e.Graphics.FillPolygon(gearBrushInBed, pointsInBed.ToArray())
            e.Graphics.DrawPolygon(outlineInBed, pointsInBed.ToArray())
        End Using
        Using hubBrushInBed As New SolidBrush(Color.FromArgb(132, 76, 31))
            e.Graphics.FillEllipse(hubBrushInBed, -3.3F, -3.3F, 6.6F, 6.6F)
        End Using
        Using holeBrushInBed As New SolidBrush(Color.FromArgb(45, 31, 22))
            e.Graphics.FillEllipse(holeBrushInBed, -1.25F, -1.25F, 2.5F, 2.5F)
        End Using
        e.Graphics.Restore(stateInBed)
    End Sub

    Protected Overrides Sub Dispose(disposing As Boolean)
        If disposing Then
            _spinTimerInBed.Stop()
            _spinTimerInBed.Dispose()
        End If
        MyBase.Dispose(disposing)
    End Sub
End Class

' Sliding host-side machine-build/configuration drawer.  It is a separate owned
' sidecar form.  Closed, it is geometrically underneath Form1; opening reveals it
' to the LEFT of the chassis edge.  A reveal region clips every pixel that would
' overlap Form1, so the configurator can never intrude into the CRT surface.
' No guest-visible hardware path depends on this form.
Public NotInheritable Class SystemConfigurationDrawer
    Inherits Form

    Private Const DrawerWidthInBed As Integer = 520
    Private Const NavigationWidthInBed As Integer = 142

    Private ReadOnly _ownerInBed As Form1
    Private ReadOnly _animationTimerInBed As New Timer()
    Private ReadOnly _liveTimerInBed As New Timer()
    Private ReadOnly _treeInBed As New TreeView()
    Private ReadOnly _contentInBed As New Panel()
    Private ReadOnly _pageTitleInBed As New Label()
    Private ReadOnly _pageBodyInBed As New Panel()
    Private ReadOnly _footerInBed As New Panel()
    Private ReadOnly _pendingLabelInBed As New Label()
    Private ReadOnly _revertButtonInBed As New Button()
    Private ReadOnly _powerCycleButtonInBed As New Button()
    Private ReadOnly _valueLabelsInBed As New Dictionary(Of String, Label)(StringComparer.OrdinalIgnoreCase)
    Private ReadOnly _selectorsInBed As New Dictionary(Of String, ComboBox)(StringComparer.OrdinalIgnoreCase)
    Private ReadOnly _toolTipInBed As New ToolTip()
    Private _hostRateSliderInBed As TrackBar
    Private _hostRateValueInBed As Label
    Private ReadOnly _nodeByKeyInBed As New Dictionary(Of String, TreeNode)(StringComparer.OrdinalIgnoreCase)
    Private _targetLeftInBed As Integer
    Private _openInBed As Boolean
    Private _lastMotionDirectionInBed As Integer
    Private _updatingControlsInBed As Boolean
    Private _currentPageInBed As String = "overview"

    Public Sub New(ownerInBed As Form1)
        If ownerInBed Is Nothing Then Throw New ArgumentNullException(NameOf(ownerInBed))
        _ownerInBed = ownerInBed

        FormBorderStyle = FormBorderStyle.None
        ShowInTaskbar = False
        ControlBox = False
        MaximizeBox = False
        MinimizeBox = False
        StartPosition = FormStartPosition.Manual
        Width = DrawerWidthInBed
        BackColor = Color.FromArgb(235, 235, 235)
        DoubleBuffered = True

        BuildChromeInBed()
        BuildTreeInBed()

        _animationTimerInBed.Interval = 15
        AddHandler _animationTimerInBed.Tick, AddressOf AnimationTickInBed
        _liveTimerInBed.Interval = 750
        AddHandler _liveTimerInBed.Tick, AddressOf LiveRefreshTickInBed

        AddHandler _treeInBed.AfterSelect, AddressOf TreeSelectedInBed
        AddHandler _ownerInBed.LocationChanged, AddressOf OwnerGeometryChangedInBed
        AddHandler _ownerInBed.SizeChanged, AddressOf OwnerGeometryChangedInBed
        AddHandler _ownerInBed.ClientSizeChanged, AddressOf OwnerGeometryChangedInBed
        AddHandler _ownerInBed.VisibleChanged, AddressOf OwnerGeometryChangedInBed
        AddHandler _ownerInBed.FormClosed, AddressOf OwnerClosedInBed

        SyncToOwnerInBed()
        SelectPageInBed("overview", False)
        RefreshFromMachine()
    End Sub

    Public ReadOnly Property IsOpen As Boolean
        Get
            Return _openInBed
        End Get
    End Property

    Public Event DrawerMotionChanged(directionInBed As Integer, movingInBed As Boolean)

    Public Sub ToggleDrawer()
        SetDrawerOpenInBed(Not _openInBed)
    End Sub

    Public Sub OpenPage(pageKeyInBed As String)
        SetDrawerOpenInBed(True)
        SelectPageInBed(pageKeyInBed, True)
    End Sub

    Public Sub RefreshFromMachine()
        If IsDisposed Then Return
        Dim snapshotInBed As SystemConfigurationSnapshot = _ownerInBed.GetSystemConfigurationSnapshotInBed()
        If snapshotInBed Is Nothing Then Return

        _updatingControlsInBed = True
        Try
            SetValueInBed("clock", snapshotInBed.CpuClock)
            SetValueInBed("host-rate", snapshotInBed.HostExecutionRate)
            If _hostRateSliderInBed IsNot Nothing AndAlso Not _hostRateSliderInBed.Capture Then
                _hostRateSliderInBed.Value = HostRateSliderPositionInBed(snapshotInBed.HostExecutionRatePercent)
            End If
            SetValueInBed("npx", snapshotInBed.NumericCoprocessor)
            SetValueInBed("chipset", snapshotInBed.Chipset)
            SetValueInBed("chipset-cpubus", snapshotInBed.ChipsetCpuBus)
            SetValueInBed("chipset-memory", snapshotInBed.ChipsetMemory)
            SetValueInBed("chipset-peripheral", snapshotInBed.ChipsetPeripheral)
            SetValueInBed("chipset-buffer", snapshotInBed.ChipsetBuffer)
            SetValueInBed("chipset-ports", snapshotInBed.ChipsetConfigurationPorts)
            SetValueInBed("chipset-timing", snapshotInBed.ChipsetTiming)
            SetValueInBed("bios", snapshotInBed.Bios)
            SetValueInBed("video-rom", snapshotInBed.VideoRom)
            SetValueInBed("floppy-a-media", snapshotInBed.FloppyMediaA)
            SetValueInBed("floppy-b-media", snapshotInBed.FloppyMediaB)
            SetValueInBed("hdd0", snapshotInBed.HardDisk0)
            SetValueInBed("optical", snapshotInBed.Optical)
            SetValueInBed("keyboard", snapshotInBed.Keyboard)
            SetValueInBed("com1", snapshotInBed.Com1)
            SetValueInBed("com2", snapshotInBed.Com2)
            SetValueInBed("lpt1", snapshotInBed.Lpt1)
            SetValueInBed("lpt2", snapshotInBed.Lpt2)
            SetValueInBed("gameport", snapshotInBed.GamePort)
            SetValueInBed("midi", snapshotInBed.Midi)
            SetValueInBed("serial-summary", snapshotInBed.Com1 & " / " & snapshotInBed.Com2)
            SetValueInBed("parallel-summary", snapshotInBed.Lpt1 & " / " & snapshotInBed.Lpt2)
            SetValueInBed("midi-summary", snapshotInBed.GamePort & " / " & snapshotInBed.Midi)
            SetValueInBed("resources", snapshotInBed.ResourceSummary)
            SetValueInBed("chassis", snapshotInBed.Chassis)
            SetValueInBed("psu", snapshotInBed.PowerSupply)

            SelectChoiceInBed("motherboard", snapshotInBed.Motherboard)
            SelectChoiceInBed("cpu", snapshotInBed.Cpu)
            SelectChoiceInBed("video", snapshotInBed.Video)
            SelectChoiceInBed("audio", snapshotInBed.ExpansionAudio)
            SelectChoiceInBed("speaker", snapshotInBed.Speaker)
            SelectChoiceInBed("network", snapshotInBed.Network)
            SelectChoiceInBed("fdc", snapshotInBed.FloppyController)
            SelectChoiceInBed("floppy-a-drive", snapshotInBed.FloppyDriveA)
            SelectChoiceInBed("floppy-b-drive", snapshotInBed.FloppyDriveB)
            SelectChoiceInBed("floppy-a-source", snapshotInBed.FloppyMediaSourceIdA)
            SelectChoiceInBed("floppy-b-source", snapshotInBed.FloppyMediaSourceIdB)
            SelectChoiceInBed("ide", snapshotInBed.HardDiskController)

            Dim memoryChoiceInBed As Integer = If(snapshotInBed.PendingMemoryMb.HasValue,
                                                   snapshotInBed.PendingMemoryMb.Value,
                                                   snapshotInBed.InstalledMemoryMb)
            SelectChoiceInBed("memory", memoryChoiceInBed.ToString())

            If Not String.IsNullOrWhiteSpace(snapshotInBed.PendingHardwareSummary) Then
                _pendingLabelInBed.Text = "Pending: " & snapshotInBed.PendingHardwareSummary & " — requires power cycle"
                _pendingLabelInBed.ForeColor = Color.DarkGoldenrod
                _revertButtonInBed.Enabled = True
                _powerCycleButtonInBed.Enabled = True
            Else
                _pendingLabelInBed.Text = "Running configuration — no pending hardware changes"
                _pendingLabelInBed.ForeColor = Color.DimGray
                _revertButtonInBed.Enabled = False
                _powerCycleButtonInBed.Enabled = False
            End If
        Finally
            _updatingControlsInBed = False
        End Try
    End Sub

    Private Sub BuildChromeInBed()
        _treeInBed.Dock = DockStyle.Left
        _treeInBed.Width = NavigationWidthInBed
        _treeInBed.BorderStyle = BorderStyle.None
        _treeInBed.BackColor = Color.FromArgb(44, 44, 44)
        _treeInBed.ForeColor = Color.Gainsboro
        _treeInBed.HideSelection = False
        _treeInBed.FullRowSelect = True
        _treeInBed.ShowLines = False
        _treeInBed.ShowPlusMinus = True
        _treeInBed.ShowRootLines = False

        _contentInBed.Dock = DockStyle.Fill
        _contentInBed.BackColor = Color.FromArgb(244, 244, 244)

        _pageTitleInBed.Dock = DockStyle.Top
        _pageTitleInBed.Height = 43
        _pageTitleInBed.Padding = New Padding(14, 12, 8, 0)
        _pageTitleInBed.Font = New Font(SystemFonts.MessageBoxFont.FontFamily, 11.0F, FontStyle.Bold)
        _pageTitleInBed.ForeColor = Color.FromArgb(35, 35, 35)
        _pageTitleInBed.Text = "SYSTEM OVERVIEW"

        _footerInBed.Dock = DockStyle.Bottom
        _footerInBed.Height = 72
        _footerInBed.BackColor = Color.FromArgb(226, 226, 226)
        _footerInBed.Padding = New Padding(10, 7, 10, 7)

        _pendingLabelInBed.AutoEllipsis = True
        _pendingLabelInBed.Location = New Point(10, 8)
        _pendingLabelInBed.Size = New Size(338, 20)
        _pendingLabelInBed.Anchor = AnchorStyles.Top Or AnchorStyles.Left Or AnchorStyles.Right

        _revertButtonInBed.Text = "Revert"
        _revertButtonInBed.Size = New Size(84, 28)
        _revertButtonInBed.Location = New Point(164, 34)
        _revertButtonInBed.Anchor = AnchorStyles.Bottom Or AnchorStyles.Right
        AddHandler _revertButtonInBed.Click,
            Sub()
                _ownerInBed.RevertPendingSystemConfigurationInBed()
                RefreshFromMachine()
            End Sub

        _powerCycleButtonInBed.Text = "Power Cycle Now"
        _powerCycleButtonInBed.Size = New Size(120, 28)
        _powerCycleButtonInBed.Location = New Point(254, 34)
        _powerCycleButtonInBed.Anchor = AnchorStyles.Bottom Or AnchorStyles.Right
        AddHandler _powerCycleButtonInBed.Click,
            Sub()
                _ownerInBed.ApplyPendingSystemConfigurationAndPowerCycleInBed()
                RefreshFromMachine()
            End Sub

        _footerInBed.Controls.Add(_pendingLabelInBed)
        _footerInBed.Controls.Add(_revertButtonInBed)
        _footerInBed.Controls.Add(_powerCycleButtonInBed)
        AddHandler _footerInBed.Resize, AddressOf FooterResizedInBed

        _pageBodyInBed.Dock = DockStyle.Fill
        _pageBodyInBed.AutoScroll = True
        _pageBodyInBed.Padding = New Padding(12, 8, 12, 8)

        _contentInBed.Controls.Add(_pageBodyInBed)
        _contentInBed.Controls.Add(_footerInBed)
        _contentInBed.Controls.Add(_pageTitleInBed)
        Controls.Add(_contentInBed)
        Controls.Add(_treeInBed)
    End Sub


    Private Sub FooterResizedInBed(senderInBed As Object, eInBed As EventArgs)
        Dim rightInBed As Integer = Math.Max(10, _footerInBed.ClientSize.Width - 10)
        _powerCycleButtonInBed.Left = Math.Max(10, rightInBed - _powerCycleButtonInBed.Width)
        _revertButtonInBed.Left = Math.Max(10, _powerCycleButtonInBed.Left - 6 - _revertButtonInBed.Width)
        _pendingLabelInBed.Width = Math.Max(40, _footerInBed.ClientSize.Width - 20)
    End Sub

    Private Sub BuildTreeInBed()
        _treeInBed.Nodes.Clear()
        _nodeByKeyInBed.Clear()

        AddNodeInBed(Nothing, "overview", "System Overview")

        Dim hostInBed As TreeNode = AddNodeInBed(Nothing, "host", "Host Configuration")
        AddNodeInBed(hostInBed, "backup", "Backup")

        Dim systemInBed As TreeNode = AddNodeInBed(Nothing, "system", "System")
        AddNodeInBed(systemInBed, "motherboard", "Motherboard")
        AddNodeInBed(systemInBed, "chipset", "Chipset")
        AddNodeInBed(systemInBed, "processor", "Processor")
        AddNodeInBed(systemInBed, "memory", "Memory")
        AddNodeInBed(systemInBed, "firmware", "Firmware")

        Dim storageInBed As TreeNode = AddNodeInBed(Nothing, "storage", "Storage")
        AddNodeInBed(storageInBed, "floppy", "Floppy Drives")
        AddNodeInBed(storageInBed, "harddisk", "Hard Drives")
        AddNodeInBed(storageInBed, "optical", "Optical Drives")

        Dim expansionInBed As TreeNode = AddNodeInBed(Nothing, "expansion", "Expansion")
        AddNodeInBed(expansionInBed, "video", "Video")
        AddNodeInBed(expansionInBed, "audio", "Audio")
        AddNodeInBed(expansionInBed, "network", "Network")

        Dim ioInBed As TreeNode = AddNodeInBed(Nothing, "io", "I/O && Ports")
        AddNodeInBed(ioInBed, "serial", "Serial Ports")
        AddNodeInBed(ioInBed, "parallel", "Parallel Ports")
        AddNodeInBed(ioInBed, "midi", "Game / MIDI")
        AddNodeInBed(ioInBed, "resources", "Resource Map")
        hostInBed.Expand()
        systemInBed.Expand()
        storageInBed.Expand()
        expansionInBed.Expand()
        ioInBed.Expand()
    End Sub

    Private Function AddNodeInBed(parentInBed As TreeNode, keyInBed As String, textInBed As String) As TreeNode
        Dim nodeInBed As New TreeNode(textInBed) With {.Name = keyInBed, .Tag = keyInBed}
        If parentInBed Is Nothing Then
            _treeInBed.Nodes.Add(nodeInBed)
        Else
            parentInBed.Nodes.Add(nodeInBed)
        End If
        _nodeByKeyInBed(keyInBed) = nodeInBed
        Return nodeInBed
    End Function

    Private Sub TreeSelectedInBed(senderInBed As Object, eInBed As TreeViewEventArgs)
        If eInBed.Node Is Nothing OrElse eInBed.Node.Tag Is Nothing Then Return
        Dim keyInBed As String = CStr(eInBed.Node.Tag)
        Select Case keyInBed
            Case "host", "system", "storage", "expansion"
                eInBed.Node.Expand()
                Return
        End Select
        SelectPageInBed(keyInBed, False)
    End Sub

    Private Sub SelectPageInBed(pageKeyInBed As String, selectTreeInBed As Boolean)
        If String.IsNullOrWhiteSpace(pageKeyInBed) Then pageKeyInBed = "overview"
        _currentPageInBed = pageKeyInBed.ToLowerInvariant()
        If selectTreeInBed AndAlso _nodeByKeyInBed.ContainsKey(_currentPageInBed) Then
            _treeInBed.SelectedNode = _nodeByKeyInBed(_currentPageInBed)
        End If
        BuildPageInBed(_currentPageInBed)
        RefreshFromMachine()
    End Sub

    Private Sub BuildPageInBed(pageKeyInBed As String)
        _pageBodyInBed.SuspendLayout()
        Try
            _pageBodyInBed.Controls.Clear()
            _valueLabelsInBed.Clear()
            _selectorsInBed.Clear()
            _hostRateSliderInBed = Nothing
            _hostRateValueInBed = Nothing

            Dim snapshotInBed As SystemConfigurationSnapshot = _ownerInBed.GetSystemConfigurationSnapshotInBed()
            Select Case pageKeyInBed
                Case "backup"
                    _pageTitleInBed.Text = "HOST BACKUP"
                    AddBackupPathEditorInBed()
                    AddNoteInBed("This is host-wide policy, not guest hardware. Sneaker Net uses this directory for append-only media generations. Changing the path only changes where future backups are written; it does not move, merge, delete or rewrite existing backups.")
                    AddNoteInBed("Automatic hourly checking is intentionally not armed in this pallet. Manual Backup Now operations are available in Sneaker Net while the storage tooling proves itself.")

                Case "motherboard"
                    _pageTitleInBed.Text = "MOTHERBOARD"
                    AddSelectorInBed("Motherboard", "motherboard", {New HardwareProfileChoice(snapshotInBed.Motherboard, snapshotInBed.Motherboard)}, Nothing)
                    AddValueInBed("Chipset", "chipset", snapshotInBed.Chipset)
                    AddValueInBed("Chassis", "chassis", snapshotInBed.Chassis)
                    AddValueInBed("Power supply", "psu", snapshotInBed.PowerSupply)
                    AddNoteInBed("Device swaps are designed as physical replacements. Future profiles will validate slots, connectors, power, jumpers and resource conflicts before they can be committed.")

                Case "chipset"
                    _pageTitleInBed.Text = "CHIPSET"
                    AddSelectorInBed("Installed chipset", "chipset-selector", {New HardwareProfileChoice(snapshotInBed.Chipset, snapshotInBed.Chipset)}, Nothing)
                    AddValueInBed("CPU / AT-bus controller", "chipset-cpubus", snapshotInBed.ChipsetCpuBus)
                    AddValueInBed("Memory controller", "chipset-memory", snapshotInBed.ChipsetMemory)
                    AddValueInBed("Integrated peripheral controller", "chipset-peripheral", snapshotInBed.ChipsetPeripheral)
                    AddValueInBed("Data / address buffer", "chipset-buffer", snapshotInBed.ChipsetBuffer)
                    AddValueInBed("Configuration interface", "chipset-ports", snapshotInBed.ChipsetConfigurationPorts)
                    AddValueInBed("Live timing state", "chipset-timing", snapshotInBed.ChipsetTiming, 118)
                    AddNoteInBed("This identifies the chipset pieces the motherboard is actually asserting. The 82C215 buffer role is represented by the board's physical decode and routing rather than invented guest-visible registers.")

                Case "processor"
                    _pageTitleInBed.Text = "PROCESSOR"
                    AddSelectorInBed("CPU", "cpu", {New HardwareProfileChoice(snapshotInBed.Cpu, snapshotInBed.Cpu)}, Nothing)
                    AddValueInBed("Running clock", "clock", snapshotInBed.CpuClock)
                    AddHostExecutionRateControlInBed(snapshotInBed.HostExecutionRatePercent,
                                                     snapshotInBed.HostExecutionRate)
                    AddSelectorInBed("Numeric coprocessor", "npx-selector", {New HardwareProfileChoice(snapshotInBed.NumericCoprocessor, snapshotInBed.NumericCoprocessor)}, Nothing)
                    AddNoteInBed("Execution rate is host pacing only. It accelerates the complete machine timeline without changing the guest-visible CPU clock, PIT, ISA bus, DMA, video or storage timing ratios. CPU and NPX replacement remain powered chassis changes.")

                Case "memory"
                    _pageTitleInBed.Text = "MEMORY"
                    Dim choicesInBed As New List(Of HardwareProfileChoice)()
                    For Each mbInBed As Integer In RamBankConfiguration.SupportedMemoryMegabytes
                        choicesInBed.Add(New HardwareProfileChoice(mbInBed.ToString(), mbInBed.ToString() & " MB installed"))
                    Next
                    AddSelectorInBed("Installed RAM", "memory", choicesInBed, AddressOf MemorySelectionChangedInBed)
                    AddNoteInBed("RAM changes are staged. The running machine is not altered until a power cycle, matching a physical SIMM-bank change rather than a warm reset.")

                Case "firmware"
                    _pageTitleInBed.Text = "FIRMWARE"
                    AddSelectorInBed("System BIOS", "bios-selector", {New HardwareProfileChoice(snapshotInBed.Bios, snapshotInBed.Bios)}, Nothing)
                    AddValueInBed("Video option ROM", "video-rom", snapshotInBed.VideoRom)
                    AddNoteInBed("Firmware selectors describe ROM devices physically installed in the build; guest firmware execution still occurs through the emulated bus and ROM windows.")

                Case "floppy"
                    _pageTitleInBed.Text = "FLOPPY DRIVES"
                    AddSelectorInBed("Controller", "fdc", {New HardwareProfileChoice(snapshotInBed.FloppyController, snapshotInBed.FloppyController)}, Nothing)

                    AddSelectorInBed("Drive A hardware", "floppy-a-drive", {New HardwareProfileChoice(snapshotInBed.FloppyDriveA, snapshotInBed.FloppyDriveA)}, Nothing)
                    AddSelectorInBed("Drive A media source", "floppy-a-source", _ownerInBed.GetFloppyConfigurationSourceChoicesInBed(0), AddressOf FloppySourceSelectionChangedInBed)
                    If _selectorsInBed.ContainsKey("floppy-a-source") Then _selectorsInBed("floppy-a-source").Tag = 0
                    AddValueInBed("Drive A attachment", "floppy-a-media", snapshotInBed.FloppyMediaA)
                    AddFloppyActionsInBed(0)

                    AddSelectorInBed("Drive B hardware", "floppy-b-drive", {New HardwareProfileChoice(snapshotInBed.FloppyDriveB, snapshotInBed.FloppyDriveB)}, Nothing)
                    AddSelectorInBed("Drive B media source", "floppy-b-source", _ownerInBed.GetFloppyConfigurationSourceChoicesInBed(1), AddressOf FloppySourceSelectionChangedInBed)
                    If _selectorsInBed.ContainsKey("floppy-b-source") Then _selectorsInBed("floppy-b-source").Tag = 1
                    AddValueInBed("Drive B attachment", "floppy-b-media", snapshotInBed.FloppyMediaB)
                    AddFloppyActionsInBed(1)

                    AddNoteInBed("The drive mechanism remains the installed hardware. This page can attach that drive either to a host physical floppy drive or to an image source; the FDC and guest path remain identical. Disk-image authoring and physical-disk imaging remain Sneaker Net jobs.")

                Case "harddisk"
                    _pageTitleInBed.Text = "HARD DRIVES"
                    AddSelectorInBed("Controller", "ide", {New HardwareProfileChoice(snapshotInBed.HardDiskController, snapshotInBed.HardDiskController)}, Nothing)
                    AddValueInBed("Primary master", "hdd0", snapshotInBed.HardDisk0)
                    AddNoteInBed("Drive Shelf operations remain available through Media/Sneaker Net. Hardware-model, controller and channel selectors live here as the configuration layer grows.")

                Case "optical"
                    _pageTitleInBed.Text = "OPTICAL DRIVES"
                    AddSelectorInBed("Optical device", "optical-selector", {New HardwareProfileChoice(snapshotInBed.Optical, snapshotInBed.Optical)}, Nothing)
                    AddNoteInBed("Optical image authoring and CD/DVD Shelf work remain separate from installed-drive configuration.")

                Case "video"
                    _pageTitleInBed.Text = "VIDEO"
                    AddSelectorInBed("Installed adapter", "video", {New HardwareProfileChoice(snapshotInBed.Video, snapshotInBed.Video)}, Nothing)
                    AddValueInBed("Option ROM", "video-rom", snapshotInBed.VideoRom)
                    AddNoteInBed("The running adapter is the Diamond Stealth Pro 928 path. Additional video boards will become alternate profiles in this same selector.")

                Case "audio"
                    _pageTitleInBed.Text = "AUDIO"
                    AddSelectorInBed("Expansion audio", "audio", {New HardwareProfileChoice(snapshotInBed.ExpansionAudio, snapshotInBed.ExpansionAudio)}, Nothing, AddressOf AudioJumperButtonClickedInBed)
                    AddSelectorInBed("Chassis speaker", "speaker", {New HardwareProfileChoice(snapshotInBed.Speaker, snapshotInBed.Speaker)}, Nothing)
                    AddNoteInBed("The installed SB16 exposes a DSP 4.x command interface, mixer-controlled ISA IRQ/DMA resources, 8/16-bit PCM DMA playback and OPL-compatible FM ports. Host waveOut is only the final speaker transducer; guest software still drives the emulated card through ISA I/O, DMA and interrupts.")

                Case "network"
                    _pageTitleInBed.Text = "NETWORK"
                    AddSelectorInBed("Installed adapter", "network", {New HardwareProfileChoice(snapshotInBed.Network, snapshotInBed.Network)}, Nothing, AddressOf NetworkJumperButtonClickedInBed)
                    AddNoteInBed("The installed NE2000-compatible adapter exposes the DP8390 register pages, station PROM, remote-DMA data port and 16 KiB receive/transmit packet RAM. Host attachment is optional; an unplugged virtual cable leaves the guest-visible board installed exactly like a physical NIC with no Ethernet lead connected.")

                Case "io"
                    _pageTitleInBed.Text = "I/O && PORTS"
                    AddValueInBed("Keyboard", "keyboard", snapshotInBed.Keyboard)
                    AddValueInBed("COM1", "com1", snapshotInBed.Com1)
                    AddValueInBed("COM2", "com2", snapshotInBed.Com2)
                    AddValueInBed("LPT1", "lpt1", snapshotInBed.Lpt1)
                    AddValueInBed("LPT2", "lpt2", snapshotInBed.Lpt2)
                    AddValueInBed("Game port", "gameport", snapshotInBed.GamePort)
                    AddValueInBed("MIDI / MPU-401", "midi", snapshotInBed.Midi)
                    AddNoteInBed("Only hardware actually implemented and installed is reported as present. Empty COM, LPT, game and MIDI positions stay visible so later I/O-card profiles have an explicit physical home instead of appearing as hidden convenience ports.")

                Case "serial"
                    _pageTitleInBed.Text = "SERIAL PORTS"
                    AddSelectorInBed("COM1 hardware", "com1-selector", {New HardwareProfileChoice(snapshotInBed.Com1, snapshotInBed.Com1)}, Nothing)
                    AddSelectorInBed("COM2 hardware", "com2-selector", {New HardwareProfileChoice(snapshotInBed.Com2, snapshotInBed.Com2)}, Nothing)
                    AddNoteInBed("Both guest-visible UARTs implement the 16550A register file, divisor clock, FIFO, prioritized interrupts, modem-control loopback and physical-time transmission and reception. COM1 carries a Microsoft two-button serial mouse; its passive wire monitor records traffic in both directions without displacing the peripheral. Click the guest display to capture the host mouse and press Ctrl+Alt+M to release it.")

                Case "parallel"
                    _pageTitleInBed.Text = "PARALLEL PORTS"
                    AddSelectorInBed("LPT1 hardware", "lpt1-selector", {New HardwareProfileChoice(snapshotInBed.Lpt1, snapshotInBed.Lpt1)}, Nothing)
                    AddSelectorInBed("LPT2 hardware", "lpt2-selector", {New HardwareProfileChoice(snapshotInBed.Lpt2, snapshotInBed.Lpt2)}, Nothing)
                    AddNoteInBed("Both guest-visible ports implement the IBM standard parallel data/status/control latches, Centronics signal polarity, timed BUSY/ACK handshake and optional ACK interrupt. Each connector now terminates in an Epson FX-class ESC/P virtual paper printer; the guest sees printer status and timing, while marked sheets are rendered host-side to PNG and/or PDF.")

                Case "midi"
                    _pageTitleInBed.Text = "GAME / MIDI"
                    AddSelectorInBed("Game port hardware", "gameport-selector", {New HardwareProfileChoice(snapshotInBed.GamePort, snapshotInBed.GamePort)}, Nothing)
                    AddSelectorInBed("MIDI / MPU-401 hardware", "midi-selector", {New HardwareProfileChoice(snapshotInBed.Midi, snapshotInBed.Midi)}, Nothing)
                    AddNoteInBed("The Sound Blaster 16 supplies the game-port connector at 201h and an MPU-401 UART-compatible interface at 330h/331h. No joystick or external MIDI synthesizer is silently fabricated: an empty physical connector reads as such until a host peripheral is explicitly attached.")

                Case "resources"
                    _pageTitleInBed.Text = "RESOURCE MAP"
                    AddValueInBed("Allocated IRQ / DMA / I/O / ROM / memory windows", "resources", snapshotInBed.ResourceSummary, 220)
                    AddNoteInBed("This is the current asserted resource map. As swappable hardware profiles are added, conflict detection should be generated from these physical allocations rather than from UI assumptions.")

                Case Else
                    _currentPageInBed = "overview"
                    _pageTitleInBed.Text = "SYSTEM OVERVIEW"
                    AddSelectorInBed("Motherboard", "motherboard", {New HardwareProfileChoice(snapshotInBed.Motherboard, snapshotInBed.Motherboard)}, Nothing)
                    AddValueInBed("Chipset", "chipset", snapshotInBed.Chipset)
                    AddSelectorInBed("CPU", "cpu", {New HardwareProfileChoice(snapshotInBed.Cpu, snapshotInBed.Cpu)}, Nothing)
                    Dim memoryChoicesInBed As New List(Of HardwareProfileChoice)()
                    For Each mbInBed As Integer In RamBankConfiguration.SupportedMemoryMegabytes
                        memoryChoicesInBed.Add(New HardwareProfileChoice(mbInBed.ToString(), mbInBed.ToString() & " MB"))
                    Next
                    AddSelectorInBed("Memory", "memory", memoryChoicesInBed, AddressOf MemorySelectionChangedInBed)
                    AddSelectorInBed("Video", "video", {New HardwareProfileChoice(snapshotInBed.Video, snapshotInBed.Video)}, Nothing)
                    AddSelectorInBed("Audio card", "audio", {New HardwareProfileChoice(snapshotInBed.ExpansionAudio, snapshotInBed.ExpansionAudio)}, Nothing, AddressOf AudioJumperButtonClickedInBed)
                    AddSelectorInBed("Network", "network", {New HardwareProfileChoice(snapshotInBed.Network, snapshotInBed.Network)}, Nothing, AddressOf NetworkJumperButtonClickedInBed)
                    AddValueInBed("CPU clock", "clock", snapshotInBed.CpuClock)
                    AddValueInBed("Numeric coprocessor", "npx", snapshotInBed.NumericCoprocessor)
                    AddValueInBed("Floppy A", "floppy-a-media", snapshotInBed.FloppyMediaA)
                    AddValueInBed("Floppy B", "floppy-b-media", snapshotInBed.FloppyMediaB)
                    AddValueInBed("Hard disk", "hdd0", snapshotInBed.HardDisk0)
                    AddValueInBed("Optical", "optical", snapshotInBed.Optical)
                    AddValueInBed("COM1 / COM2", "serial-summary", snapshotInBed.Com1 & " / " & snapshotInBed.Com2)
                    AddValueInBed("LPT1 / LPT2", "parallel-summary", snapshotInBed.Lpt1 & " / " & snapshotInBed.Lpt2)
                    AddValueInBed("Game / MIDI", "midi-summary", snapshotInBed.GamePort & " / " & snapshotInBed.Midi)
            End Select
        Finally
            _pageBodyInBed.ResumeLayout()
        End Try
    End Sub

    Private Sub AddBackupPathEditorInBed()
        Dim panelInBed As New TableLayoutPanel() With {
            .AutoSize = True,
            .AutoSizeMode = AutoSizeMode.GrowAndShrink,
            .ColumnCount = 3,
            .RowCount = 2,
            .Dock = DockStyle.Top,
            .Padding = New Padding(0, 0, 0, 10)
        }
        panelInBed.ColumnStyles.Add(New ColumnStyle(SizeType.Percent, 100.0F))
        panelInBed.ColumnStyles.Add(New ColumnStyle(SizeType.Absolute, 92))
        panelInBed.ColumnStyles.Add(New ColumnStyle(SizeType.Absolute, 92))

        Dim captionInBed As New Label() With {
            .AutoSize = True,
            .Text = "Backup directory",
            .ForeColor = Color.DimGray,
            .Margin = New Padding(0, 0, 0, 3)
        }
        panelInBed.Controls.Add(captionInBed, 0, 0)
        panelInBed.SetColumnSpan(captionInBed, 3)

        Dim pathBoxInBed As New TextBox() With {
            .Dock = DockStyle.Fill,
            .ReadOnly = True,
            .Text = HostMediaConfiguration.GetBackupRoot()
        }
        Dim browseInBed As New Button() With {.Text = "Browse...", .Dock = DockStyle.Fill}
        Dim openInBed As New Button() With {.Text = "Open", .Dock = DockStyle.Fill}
        panelInBed.Controls.Add(pathBoxInBed, 0, 1)
        panelInBed.Controls.Add(browseInBed, 1, 1)
        panelInBed.Controls.Add(openInBed, 2, 1)

        AddHandler browseInBed.Click,
            Sub()
                Using pickerInBed As New FolderBrowserDialog()
                    pickerInBed.Description = "Choose the host backup directory. Existing backups are not moved or deleted."
                    pickerInBed.SelectedPath = HostMediaConfiguration.GetBackupRoot()
                    pickerInBed.ShowNewFolderButton = True
                    If pickerInBed.ShowDialog(Me) <> DialogResult.OK Then Return
                    Try
                        HostMediaConfiguration.SetBackupRoot(pickerInBed.SelectedPath)
                        pathBoxInBed.Text = HostMediaConfiguration.GetBackupRoot()
                    Catch ex As Exception
                        MessageBox.Show(Me, ex.Message, "Backup directory", MessageBoxButtons.OK, MessageBoxIcon.Error)
                    End Try
                End Using
            End Sub

        AddHandler openInBed.Click,
            Sub()
                Try
                    HostMediaConfiguration.EnsureBackupRoot()
                    Process.Start(New ProcessStartInfo(HostMediaConfiguration.GetBackupRoot()) With {.UseShellExecute = True})
                Catch ex As Exception
                    MessageBox.Show(Me, ex.Message, "Backup directory", MessageBoxButtons.OK, MessageBoxIcon.Error)
                End Try
            End Sub

        _pageBodyInBed.Controls.Add(panelInBed)
        panelInBed.SendToBack()
    End Sub

    Private Sub AddSelectorInBed(labelInBed As String,
                                 keyInBed As String,
                                 choicesInBed As IEnumerable(Of HardwareProfileChoice),
                                 changedInBed As EventHandler,
                                 Optional configureInBed As EventHandler = Nothing)
        Dim hasConfigButtonInBed As Boolean = configureInBed IsNot Nothing
        Dim rowInBed As New TableLayoutPanel() With {
            .AutoSize = True,
            .AutoSizeMode = AutoSizeMode.GrowAndShrink,
            .ColumnCount = If(hasConfigButtonInBed, 2, 1),
            .RowCount = 2,
            .Dock = DockStyle.Top,
            .Padding = New Padding(0, 0, 0, 8)
        }
        rowInBed.ColumnStyles.Add(New ColumnStyle(SizeType.Percent, 100.0F))
        If hasConfigButtonInBed Then rowInBed.ColumnStyles.Add(New ColumnStyle(SizeType.Absolute, 38.0F))

        Dim labelControlInBed As New Label() With {
            .AutoSize = True,
            .Text = labelInBed,
            .ForeColor = Color.DimGray,
            .Margin = New Padding(0, 0, 0, 3)
        }
        Dim comboInBed As New ComboBox() With {
            .DropDownStyle = ComboBoxStyle.DropDownList,
            .Dock = DockStyle.Top,
            .IntegralHeight = True,
            .Margin = New Padding(0, 0, If(hasConfigButtonInBed, 4, 0), 0)
        }
        For Each choiceInBed As HardwareProfileChoice In choicesInBed
            comboInBed.Items.Add(choiceInBed)
        Next
        If comboInBed.Items.Count > 0 Then comboInBed.SelectedIndex = 0
        If changedInBed IsNot Nothing Then AddHandler comboInBed.SelectedIndexChanged, changedInBed
        _selectorsInBed(keyInBed) = comboInBed
        rowInBed.Controls.Add(labelControlInBed, 0, 0)
        If hasConfigButtonInBed Then rowInBed.SetColumnSpan(labelControlInBed, 2)
        rowInBed.Controls.Add(comboInBed, 0, 1)

        If hasConfigButtonInBed Then
            Dim jumperButtonInBed As New Button() With {
                .Text = "JP",
                .Dock = DockStyle.Fill,
                .Margin = New Padding(0),
                .TabStop = True,
                .AccessibleName = labelInBed & " jumper and DIP switch configuration"
            }
            _toolTipInBed.SetToolTip(jumperButtonInBed, "Card-level jumper / DIP-switch setup")
            AddHandler jumperButtonInBed.Click, configureInBed
            rowInBed.Controls.Add(jumperButtonInBed, 1, 1)
        End If

        _pageBodyInBed.Controls.Add(rowInBed)
        rowInBed.SendToBack()
    End Sub

    Private Sub AudioJumperButtonClickedInBed(senderInBed As Object, eInBed As EventArgs)
        _ownerInBed.OpenSoundBlasterJumperPanelInBed()
        RefreshFromMachine()
    End Sub

    Private Sub NetworkJumperButtonClickedInBed(senderInBed As Object, eInBed As EventArgs)
        _ownerInBed.OpenNe2000JumperPanelInBed()
        RefreshFromMachine()
    End Sub

    Private Sub AddValueInBed(labelInBed As String,
                              keyInBed As String,
                              valueInBed As String,
                              Optional heightInBed As Integer = 44)
        Dim panelInBed As New Panel() With {
            .Dock = DockStyle.Top,
            .Height = heightInBed,
            .Padding = New Padding(0, 0, 0, 6)
        }
        Dim captionInBed As New Label() With {
            .AutoSize = True,
            .Text = labelInBed,
            .ForeColor = Color.DimGray,
            .Location = New Point(0, 0)
        }
        Dim valueLabelInBed As New Label() With {
            .AutoEllipsis = (heightInBed < 70),
            .Text = If(valueInBed, String.Empty),
            .Location = New Point(0, 19),
            .Size = New Size(Math.Max(40, _pageBodyInBed.ClientSize.Width - 12), Math.Max(20, heightInBed - 22)),
            .Anchor = AnchorStyles.Top Or AnchorStyles.Left Or AnchorStyles.Right,
            .ForeColor = Color.FromArgb(35, 35, 35)
        }
        _valueLabelsInBed(keyInBed) = valueLabelInBed
        panelInBed.Controls.Add(captionInBed)
        panelInBed.Controls.Add(valueLabelInBed)
        _pageBodyInBed.Controls.Add(panelInBed)
        panelInBed.SendToBack()
    End Sub

    Private Shared ReadOnly HostRatePercentagesInBed() As Integer = {25, 50, 100, 200, 400, 800, 1600, 0}

    Private Shared Function HostRateSliderPositionInBed(percentInBed As Integer) As Integer
        For indexInBed As Integer = 0 To HostRatePercentagesInBed.Length - 1
            If HostRatePercentagesInBed(indexInBed) = percentInBed Then Return indexInBed
        Next
        Return 2
    End Function

    Private Shared Function HostRateTextInBed(percentInBed As Integer) As String
        If percentInBed = 0 Then Return "Unlimited"
        Return (CDbl(percentInBed) / 100.0R).ToString("0.##") & "× real time"
    End Function

    Private Sub AddHostExecutionRateControlInBed(percentInBed As Integer, textInBed As String)
        Dim panelInBed As New Panel() With {
            .Dock = DockStyle.Top,
            .Height = 92,
            .Padding = New Padding(0, 0, 0, 8)
        }
        Dim captionInBed As New Label() With {
            .AutoSize = True,
            .Text = "Host execution rate",
            .ForeColor = Color.DimGray,
            .Location = New Point(0, 0)
        }
        _hostRateValueInBed = New Label() With {
            .AutoSize = True,
            .Text = If(textInBed, HostRateTextInBed(percentInBed)),
            .ForeColor = Color.FromArgb(35, 35, 35),
            .Location = New Point(0, 20)
        }
        _valueLabelsInBed("host-rate") = _hostRateValueInBed
        _hostRateSliderInBed = New TrackBar() With {
            .Minimum = 0,
            .Maximum = HostRatePercentagesInBed.Length - 1,
            .TickFrequency = 1,
            .SmallChange = 1,
            .LargeChange = 1,
            .Value = HostRateSliderPositionInBed(percentInBed),
            .Dock = DockStyle.Bottom
        }
        AddHandler _hostRateSliderInBed.ValueChanged,
            Sub()
                If _updatingControlsInBed Then Return
                Dim selectedPercentInBed As Integer = HostRatePercentagesInBed(_hostRateSliderInBed.Value)
                _hostRateValueInBed.Text = HostRateTextInBed(selectedPercentInBed)
                _ownerInBed.SetHostExecutionRateInBed(selectedPercentInBed)
            End Sub
        panelInBed.Controls.Add(captionInBed)
        panelInBed.Controls.Add(_hostRateValueInBed)
        panelInBed.Controls.Add(_hostRateSliderInBed)
        _pageBodyInBed.Controls.Add(panelInBed)
        panelInBed.SendToBack()
    End Sub

    Private Sub AddNoteInBed(textInBed As String)
        Dim noteInBed As New Label() With {
            .Dock = DockStyle.Top,
            .AutoSize = False,
            .Height = 82,
            .Padding = New Padding(8),
            .BackColor = Color.FromArgb(228, 234, 238),
            .ForeColor = Color.FromArgb(55, 55, 55),
            .Text = textInBed
        }
        _pageBodyInBed.Controls.Add(noteInBed)
        noteInBed.SendToBack()
    End Sub

    Private Sub AddFloppyActionsInBed(driveInBed As Integer)
        Dim rowInBed As New FlowLayoutPanel() With {
            .Dock = DockStyle.Top,
            .AutoSize = True,
            .AutoSizeMode = AutoSizeMode.GrowAndShrink,
            .FlowDirection = FlowDirection.LeftToRight,
            .WrapContents = True,
            .Padding = New Padding(0, 0, 0, 9)
        }

        Dim browseInBed As New Button() With {
            .AutoSize = True,
            .Text = "Browse image..."
        }
        AddHandler browseInBed.Click,
            Sub()
                _ownerInBed.BrowseFloppyImageFromConfigurationInBed(driveInBed)
                BuildPageInBed("floppy")
                RefreshFromMachine()
            End Sub

        Dim ejectInBed As New Button() With {
            .AutoSize = True,
            .Text = "Eject"
        }
        AddHandler ejectInBed.Click,
            Sub()
                _ownerInBed.EjectFloppyFromConfigurationInBed(driveInBed)
                BuildPageInBed("floppy")
                RefreshFromMachine()
            End Sub

        Dim refreshInBed As New Button() With {
            .AutoSize = True,
            .Text = "Rescan sources"
        }
        AddHandler refreshInBed.Click,
            Sub()
                BuildPageInBed("floppy")
                RefreshFromMachine()
            End Sub

        rowInBed.Controls.Add(browseInBed)
        rowInBed.Controls.Add(ejectInBed)
        rowInBed.Controls.Add(refreshInBed)
        _pageBodyInBed.Controls.Add(rowInBed)
        rowInBed.SendToBack()
    End Sub

    Private Sub FloppySourceSelectionChangedInBed(senderInBed As Object, eInBed As EventArgs)
        If _updatingControlsInBed Then Return
        Dim comboInBed As ComboBox = TryCast(senderInBed, ComboBox)
        If comboInBed Is Nothing OrElse comboInBed.Tag Is Nothing Then Return
        Dim choiceInBed As HardwareProfileChoice = TryCast(comboInBed.SelectedItem, HardwareProfileChoice)
        If choiceInBed Is Nothing Then Return

        Dim driveInBed As Integer = CInt(comboInBed.Tag)
        _ownerInBed.SelectFloppyConfigurationSourceInBed(driveInBed, choiceInBed.Id)
        BuildPageInBed("floppy")
        RefreshFromMachine()
    End Sub

    Private Sub MemorySelectionChangedInBed(senderInBed As Object, eInBed As EventArgs)
        If _updatingControlsInBed Then Return
        Dim comboInBed As ComboBox = TryCast(senderInBed, ComboBox)
        If comboInBed Is Nothing Then Return
        Dim choiceInBed As HardwareProfileChoice = TryCast(comboInBed.SelectedItem, HardwareProfileChoice)
        If choiceInBed Is Nothing Then Return
        Dim megabytesInBed As Integer
        If Not Integer.TryParse(choiceInBed.Id, megabytesInBed) Then Return
        _ownerInBed.StageMemoryConfigurationInBed(megabytesInBed)
        RefreshFromMachine()
    End Sub

    Private Sub SetValueInBed(keyInBed As String, valueInBed As String)
        Dim labelInBed As Label = Nothing
        If _valueLabelsInBed.TryGetValue(keyInBed, labelInBed) Then labelInBed.Text = If(valueInBed, String.Empty)
    End Sub

    Private Sub SelectChoiceInBed(keyInBed As String, idOrDisplayInBed As String)
        Dim comboInBed As ComboBox = Nothing
        If Not _selectorsInBed.TryGetValue(keyInBed, comboInBed) Then Return
        For indexInBed As Integer = 0 To comboInBed.Items.Count - 1
            Dim choiceInBed As HardwareProfileChoice = TryCast(comboInBed.Items(indexInBed), HardwareProfileChoice)
            If choiceInBed IsNot Nothing AndAlso
               (String.Equals(choiceInBed.Id, idOrDisplayInBed, StringComparison.OrdinalIgnoreCase) OrElse
                String.Equals(choiceInBed.DisplayName, idOrDisplayInBed, StringComparison.OrdinalIgnoreCase)) Then
                comboInBed.SelectedIndex = indexInBed
                Return
            End If
        Next
    End Sub

    Private Sub SetDrawerOpenInBed(openInBed As Boolean)
        _openInBed = openInBed
        Dim edgeXInBed As Integer = OwnerDrawerEdgeXInBed()
        _targetLeftInBed = If(openInBed, edgeXInBed - Width, edgeXInBed)
        Enabled = True
        If Not Visible AndAlso Not _ownerInBed.IsDisposed AndAlso _ownerInBed.Visible Then
            Show(_ownerInBed)
        End If

        Dim directionInBed As Integer = Math.Sign(_targetLeftInBed - Left)
        If directionInBed = 0 Then directionInBed = If(openInBed, -1, 1)
        _lastMotionDirectionInBed = directionInBed
        RaiseEvent DrawerMotionChanged(directionInBed, True)

        _animationTimerInBed.Start()
        If openInBed Then
            _liveTimerInBed.Start()
            RefreshFromMachine()
        Else
            _liveTimerInBed.Stop()
        End If
    End Sub

    Private Sub AnimationTickInBed(senderInBed As Object, eInBed As EventArgs)
        Dim differenceInBed As Integer = _targetLeftInBed - Left
        If Math.Abs(differenceInBed) <= 2 Then
            Left = _targetLeftInBed
            UpdateRevealRegionInBed()
            _animationTimerInBed.Stop()
            _lastMotionDirectionInBed = 0
            RaiseEvent DrawerMotionChanged(0, False)
            If Not _openInBed Then Enabled = False
            Return
        End If

        Dim stepInBed As Integer = Math.Max(14, Math.Abs(differenceInBed) \ 4)
        Left += Math.Sign(differenceInBed) * Math.Min(stepInBed, Math.Abs(differenceInBed))
        UpdateRevealRegionInBed()
    End Sub

    Private Sub LiveRefreshTickInBed(senderInBed As Object, eInBed As EventArgs)
        For Each comboInBed As ComboBox In _selectorsInBed.Values
            If comboInBed.DroppedDown Then Return
        Next
        RefreshFromMachine()
    End Sub

    Private Sub OwnerGeometryChangedInBed(senderInBed As Object, eInBed As EventArgs)
        SyncToOwnerInBed()
    End Sub

    Private Sub OwnerClosedInBed(senderInBed As Object, eInBed As FormClosedEventArgs)
        If Not IsDisposed Then Close()
    End Sub

    Private Function OwnerDrawerEdgeXInBed() As Integer
        If _ownerInBed.IsDisposed Then Return Left + Width
        Return _ownerInBed.PointToScreen(New Point(0, _ownerInBed.PictureBox1.Top)).X
    End Function

    Private Sub UpdateRevealRegionInBed()
        If IsDisposed Then Return

        Dim visibleWidthInBed As Integer =
            Math.Max(0, Math.Min(Width, OwnerDrawerEdgeXInBed() - Left))

        Dim nextRegionInBed As New Region()
        nextRegionInBed.MakeEmpty()
        If visibleWidthInBed > 0 AndAlso Height > 0 Then
            nextRegionInBed.Union(New Rectangle(0, 0, visibleWidthInBed, Height))
        End If

        Dim oldRegionInBed As Region = Region
        Region = nextRegionInBed
        If oldRegionInBed IsNot Nothing Then oldRegionInBed.Dispose()
    End Sub

    Public Sub SyncToOwnerInBed()
        If _ownerInBed.IsDisposed Then Return

        ' This is an owned sidecar form. Windows automatically hides owned forms
        ' with a minimized/hidden owner; do not force a Hide/Show cycle here.
        If Not _ownerInBed.Visible OrElse _ownerInBed.WindowState = FormWindowState.Minimized Then Return

        Dim screenOriginInBed As Point =
            _ownerInBed.PointToScreen(New Point(0, _ownerInBed.PictureBox1.Top))
        Top = screenOriginInBed.Y
        Height = Math.Max(120, _ownerInBed.ClientSize.Height - _ownerInBed.PictureBox1.Top)

        Dim edgeXInBed As Integer = screenOriginInBed.X
        _targetLeftInBed = If(_openInBed, edgeXInBed - Width, edgeXInBed)
        If Not _animationTimerInBed.Enabled Then Left = _targetLeftInBed

        UpdateRevealRegionInBed()
    End Sub

    Protected Overrides Sub Dispose(disposing As Boolean)
        If disposing Then
            _animationTimerInBed.Stop()
            _liveTimerInBed.Stop()
            _animationTimerInBed.Dispose()
            _liveTimerInBed.Dispose()
            RemoveHandler _ownerInBed.LocationChanged, AddressOf OwnerGeometryChangedInBed
            RemoveHandler _ownerInBed.SizeChanged, AddressOf OwnerGeometryChangedInBed
            RemoveHandler _ownerInBed.ClientSizeChanged, AddressOf OwnerGeometryChangedInBed
            RemoveHandler _ownerInBed.VisibleChanged, AddressOf OwnerGeometryChangedInBed
            RemoveHandler _ownerInBed.FormClosed, AddressOf OwnerClosedInBed
            If _lastMotionDirectionInBed <> 0 Then RaiseEvent DrawerMotionChanged(0, False)
        End If
        MyBase.Dispose(disposing)
    End Sub
End Class
