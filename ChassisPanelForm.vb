Imports System
Imports System.Drawing
Imports System.Drawing.Drawing2D
Imports System.Windows.Forms

' Borderless, owned, non-activating annunciator overlay.  It has no guest
' behavior and no hardware knowledge: it only paints FrontPanelState.
Public NotInheritable Class ChassisPanelForm
    Inherits Form

    Private Const WsExToolWindow As Integer = &H80
    Private Const WsExNoActivate As Integer = &H8000000
    Private Const BaseClientWidth As Integer = 1342

    Private Const LeftControlShiftInBed As Integer = 50

    Private ReadOnly _ownerInBed As Form1
    Private ReadOnly _stateInBed As FrontPanelState
    Private ReadOnly _refreshTimerInBed As New Timer()
    Private ReadOnly _configurationGearInBed As New ConfigurationGearButton()
    Private ReadOnly _powerButtonInBed As New ChassisPowerButton()
    Private ReadOnly _configurationToolTipInBed As New ToolTip()

    Public Event ConfigurationToggleRequested()
    Public Event PowerToggleRequested()

    Private Shared ReadOnly CpuStateMasksInBed() As Byte = {
        CByte(ProcessorStateByte.Run),
        CByte(ProcessorStateByte.Halt),
        CByte(ProcessorStateByte.Wait),
        CByte(ProcessorStateByte.Interrupt),
        CByte(ProcessorStateByte.BusWait),
        CByte(ProcessorStateByte.Hold),
        CByte(ProcessorStateByte.ProtectedMode),
        CByte(ProcessorStateByte.Shutdown)
    }
    Private Shared ReadOnly CpuStateLabelsInBed() As String = {"R", "H", "W", "I", "B", "D", "P", "S"}
    Private Shared ReadOnly CpuStateColorsInBed() As Color = {
        Color.LimeGreen, Color.Gold, Color.Cyan, Color.FromArgb(105, 110, 115),
        Color.Orange, Color.MediumPurple, Color.DeepSkyBlue, Color.Red
    }

    Public Sub New(ownerInBed As Form1, stateInBed As FrontPanelState)
        If ownerInBed Is Nothing Then Throw New ArgumentNullException(NameOf(ownerInBed))
        If stateInBed Is Nothing Then Throw New ArgumentNullException(NameOf(stateInBed))
        _ownerInBed = ownerInBed
        _stateInBed = stateInBed

        FormBorderStyle = FormBorderStyle.None
        ShowInTaskbar = False
        ControlBox = False
        StartPosition = FormStartPosition.Manual
        BackColor = ownerInBed.BackColor
        DoubleBuffered = True
        SetStyle(ControlStyles.AllPaintingInWmPaint Or ControlStyles.OptimizedDoubleBuffer Or ControlStyles.UserPaint, True)

        _configurationGearInBed.Location = New Point(3, 1)
        _configurationGearInBed.Anchor = AnchorStyles.Top Or AnchorStyles.Left
        _configurationToolTipInBed.SetToolTip(_configurationGearInBed, "System Configuration")
        AddHandler _configurationGearInBed.Click,
            Sub() RaiseEvent ConfigurationToggleRequested()
        Controls.Add(_configurationGearInBed)
        _configurationGearInBed.BringToFront()

        _powerButtonInBed.Location = New Point(24, 1)
        _powerButtonInBed.Anchor = AnchorStyles.Top Or AnchorStyles.Left
        _configurationToolTipInBed.SetToolTip(_powerButtonInBed, "Chassis Power")
        AddHandler _powerButtonInBed.Click,
            Sub() RaiseEvent PowerToggleRequested()
        Controls.Add(_powerButtonInBed)
        _powerButtonInBed.BringToFront()

        _refreshTimerInBed.Interval = 16
        AddHandler _refreshTimerInBed.Tick, AddressOf RefreshTimerTickInBed
        AddHandler ownerInBed.LocationChanged, AddressOf OwnerGeometryChangedInBed
        AddHandler ownerInBed.SizeChanged, AddressOf OwnerGeometryChangedInBed
        AddHandler ownerInBed.ClientSizeChanged, AddressOf OwnerGeometryChangedInBed
        AddHandler ownerInBed.VisibleChanged, AddressOf OwnerVisibilityChangedInBed
        AddHandler ownerInBed.FormClosed, AddressOf OwnerClosedInBed
    End Sub

    Protected Overrides ReadOnly Property ShowWithoutActivation As Boolean
        Get
            Return True
        End Get
    End Property

    Protected Overrides ReadOnly Property CreateParams As CreateParams
        Get
            Dim paramsInBed As CreateParams = MyBase.CreateParams
            paramsInBed.ExStyle = paramsInBed.ExStyle Or WsExToolWindow Or WsExNoActivate
            Return paramsInBed
        End Get
    End Property

    Protected Overrides Sub OnShown(eInBed As EventArgs)
        MyBase.OnShown(eInBed)
        SyncToOwnerInBed()
        _refreshTimerInBed.Start()
    End Sub

    Protected Overrides Sub OnFormClosed(eInBed As FormClosedEventArgs)
        _refreshTimerInBed.Stop()
        RemoveHandler _ownerInBed.LocationChanged, AddressOf OwnerGeometryChangedInBed
        RemoveHandler _ownerInBed.SizeChanged, AddressOf OwnerGeometryChangedInBed
        RemoveHandler _ownerInBed.ClientSizeChanged, AddressOf OwnerGeometryChangedInBed
        RemoveHandler _ownerInBed.VisibleChanged, AddressOf OwnerVisibilityChangedInBed
        RemoveHandler _ownerInBed.FormClosed, AddressOf OwnerClosedInBed
        _refreshTimerInBed.Dispose()
        _configurationToolTipInBed.Dispose()
        MyBase.OnFormClosed(eInBed)
    End Sub

    Private Sub RefreshTimerTickInBed(senderInBed As Object, eInBed As EventArgs)
        Dim snapshotInBed As FrontPanelSnapshot = _stateInBed.GetSnapshot()
        _powerButtonInBed.SetPoweredInBed(snapshotInBed.PowerOn)
        Invalidate()
    End Sub

    Private Sub OwnerGeometryChangedInBed(senderInBed As Object, eInBed As EventArgs)
        SyncToOwnerInBed()
    End Sub

    Private Sub OwnerVisibilityChangedInBed(senderInBed As Object, eInBed As EventArgs)
        SyncToOwnerInBed()
    End Sub

    Private Sub OwnerClosedInBed(senderInBed As Object, eInBed As FormClosedEventArgs)
        Close()
    End Sub

    Private Sub SyncToOwnerInBed()
        If _ownerInBed.IsDisposed OrElse Not _ownerInBed.Visible OrElse _ownerInBed.WindowState = FormWindowState.Minimized Then
            If Visible Then Hide()
            Return
        End If

        ' Transitional geometry derives from the current front-panel row so DPI
        ' scaling stays aligned while the old designer controls remain underneath.
        Dim panelTopInBed As Integer = Math.Max(0, _ownerInBed.Label4.Top - 2)
        Dim panelBottomInBed As Integer = Math.Max(panelTopInBed + 1, _ownerInBed.PictureBox1.Top)
        Dim screenOriginInBed As Point = _ownerInBed.PointToScreen(New Point(0, panelTopInBed))
        Dim desiredInBed As New Rectangle(screenOriginInBed.X,
                                          screenOriginInBed.Y,
                                          Math.Max(1, _ownerInBed.ClientSize.Width),
                                          panelBottomInBed - panelTopInBed)
        If Bounds <> desiredInBed Then Bounds = desiredInBed
        _configurationGearInBed.Top = Math.Max(0, (ClientSize.Height - _configurationGearInBed.Height) \ 2)
        _configurationGearInBed.Left = 3
        _configurationGearInBed.BringToFront()
        _powerButtonInBed.Top = Math.Max(0, (ClientSize.Height - _powerButtonInBed.Height) \ 2)
        _powerButtonInBed.Left = 24
        _powerButtonInBed.BringToFront()
        If Not Visible Then Show(_ownerInBed)
    End Sub

    Public Sub SetConfigurationMotionInBed(directionInBed As Integer, movingInBed As Boolean)
        _configurationGearInBed.SetMotionInBed(directionInBed, movingInBed)
    End Sub

    Protected Overrides Sub OnPaintBackground(eInBed As PaintEventArgs)
        eInBed.Graphics.Clear(_ownerInBed.BackColor)
    End Sub

    Protected Overrides Sub OnPaint(eInBed As PaintEventArgs)
        MyBase.OnPaint(eInBed)
        Dim snapshotInBed As FrontPanelSnapshot = _stateInBed.GetSnapshot()
        Dim graphicsInBed As Graphics = eInBed.Graphics
        graphicsInBed.SmoothingMode = SmoothingMode.AntiAlias
        graphicsInBed.PixelOffsetMode = PixelOffsetMode.HighQuality

        DrawIndicatorInBed(graphicsInBed, "|Power", 13 + LeftControlShiftInBed, 59 + LeftControlShiftInBed, Color.Green, snapshotInBed.PowerOn)
        DrawIndicatorInBed(graphicsInBed, "|Turbo", 79 + LeftControlShiftInBed, 124 + LeftControlShiftInBed, Color.Gold, snapshotInBed.TurboOn)
        DrawIndicatorInBed(graphicsInBed, "|FDD A", 175 + LeftControlShiftInBed, 221 + LeftControlShiftInBed, Color.LimeGreen, snapshotInBed.FddA)
        DrawIndicatorInBed(graphicsInBed, "|FDD B", 241 + LeftControlShiftInBed, 286 + LeftControlShiftInBed, Color.LimeGreen, snapshotInBed.FddB)
        DrawIndicatorInBed(graphicsInBed, "|HDD 0", 306 + LeftControlShiftInBed, 353 + LeftControlShiftInBed, Color.Red, snapshotInBed.Hdd0)
        DrawIndicatorInBed(graphicsInBed, "|HDD 1", 373 + LeftControlShiftInBed, 420 + LeftControlShiftInBed, Color.Red, snapshotInBed.Hdd1)
        DrawIndicatorInBed(graphicsInBed, "|HDD 2", 440 + LeftControlShiftInBed, 487 + LeftControlShiftInBed, Color.Red, snapshotInBed.Hdd2)
        DrawIndicatorInBed(graphicsInBed, "|HDD 3", 507 + LeftControlShiftInBed, 554 + LeftControlShiftInBed, Color.Red, snapshotInBed.Hdd3)

        DrawLabelInBed(graphicsInBed, "|CPU", 574 + LeftControlShiftInBed, 34)
        DrawCpuStateByteInBed(graphicsInBed, New Point(610 + LeftControlShiftInBed, 2), snapshotInBed.CpuStateByte)

        Dim rightShiftInBed As Integer = ClientSize.Width - BaseClientWidth
        DrawIndicatorInBed(graphicsInBed, "|Ser TX/RX|", 900 + rightShiftInBed, 965 + rightShiftInBed, Color.Gold, snapshotInBed.SerialTx)
        DrawIndicatorInBed(graphicsInBed, " / ", 980 + rightShiftInBed, 990 + rightShiftInBed, Color.Gold, snapshotInBed.SerialRx)
        DrawIndicatorInBed(graphicsInBed, "|Eth TX/RX|", 1050 + rightShiftInBed, 1115 + rightShiftInBed, Color.Red, snapshotInBed.EthernetTx)
        DrawIndicatorInBed(graphicsInBed, " / ", 1130 + rightShiftInBed, 1140 + rightShiftInBed, Color.Red, snapshotInBed.EthernetRx)
        DrawIndicatorInBed(graphicsInBed, "|KB TX/RX|", 1200 + rightShiftInBed, 1265 + rightShiftInBed, Color.Gold, snapshotInBed.KeyboardTx)
        DrawIndicatorInBed(graphicsInBed, " / ", 1280 + rightShiftInBed, 1290 + rightShiftInBed, Color.Red, snapshotInBed.KeyboardRx)
    End Sub

    Private Sub DrawIndicatorInBed(graphicsInBed As Graphics,
                                   textInBed As String,
                                   labelXInBed As Integer,
                                   lampXInBed As Integer,
                                   lampColorInBed As Color,
                                   onInBed As Boolean)
        DrawLabelInBed(graphicsInBed, textInBed, labelXInBed, Math.Max(1, lampXInBed - labelXInBed - 1))
        DrawLedInBed(graphicsInBed, New Rectangle(lampXInBed, 2, 14, 15), lampColorInBed, onInBed)
    End Sub

    Private Sub DrawLabelInBed(graphicsInBed As Graphics, textInBed As String, xInBed As Integer, widthInBed As Integer)
        Dim boundsInBed As New Rectangle(xInBed, 0, Math.Max(1, widthInBed), ClientSize.Height)
        TextRenderer.DrawText(graphicsInBed,
                              textInBed,
                              _ownerInBed.Font,
                              boundsInBed,
                              _ownerInBed.ForeColor,
                              TextFormatFlags.Left Or TextFormatFlags.VerticalCenter Or TextFormatFlags.NoPadding Or TextFormatFlags.NoPrefix)
    End Sub

    Private Shared Sub DrawLedInBed(graphicsInBed As Graphics, boundsInBed As Rectangle, colorInBed As Color, onInBed As Boolean)
        Dim lensInBed As Rectangle = Rectangle.Inflate(boundsInBed, -1, -1)
        Dim outerInBed As Color = If(onInBed, ScaleColorInBed(colorInBed, 0.42), Color.FromArgb(52, 52, 52))
        Dim innerInBed As Color = If(onInBed, colorInBed, ScaleColorInBed(colorInBed, 0.14))

        Using outerBrushInBed As New SolidBrush(outerInBed)
            graphicsInBed.FillEllipse(outerBrushInBed, lensInBed)
        End Using
        Dim innerBoundsInBed As Rectangle = Rectangle.Inflate(lensInBed, -2, -2)
        Using innerBrushInBed As New SolidBrush(innerInBed)
            graphicsInBed.FillEllipse(innerBrushInBed, innerBoundsInBed)
        End Using
        Using rimPenInBed As New Pen(Color.FromArgb(150, 15, 15, 15))
            graphicsInBed.DrawEllipse(rimPenInBed, lensInBed)
        End Using
        If onInBed Then
            Dim glintInBed As New Rectangle(innerBoundsInBed.X + 1,
                                             innerBoundsInBed.Y + 1,
                                             Math.Max(2, innerBoundsInBed.Width \ 3),
                                             Math.Max(2, innerBoundsInBed.Height \ 3))
            Using glintBrushInBed As New SolidBrush(Color.FromArgb(150, Color.White))
                graphicsInBed.FillEllipse(glintBrushInBed, glintInBed)
            End Using
        End If
    End Sub

    Private Sub DrawCpuStateByteInBed(graphicsInBed As Graphics, originInBed As Point, stateByteInBed As Byte)
        Const cellWidthInBed As Integer = 15
        Const cellHeightInBed As Integer = 15
        Const cellGapInBed As Integer = 2

        For indexInBed As Integer = 0 To 7
            Dim maskInBed As Integer = CInt(CpuStateMasksInBed(indexInBed))
            Dim activeInBed As Boolean = (CInt(stateByteInBed) And maskInBed) <> 0
            Dim cellInBed As New Rectangle(originInBed.X + indexInBed * (cellWidthInBed + cellGapInBed),
                                           originInBed.Y,
                                           cellWidthInBed,
                                           cellHeightInBed)
            Dim colorInBed As Color = CpuStateColorsInBed(indexInBed)
            Dim fillInBed As Color = If(activeInBed, colorInBed, ScaleColorInBed(colorInBed, 0.12))
            Dim textColorInBed As Color = If(activeInBed, Color.FromArgb(20, 20, 20), ScaleColorInBed(colorInBed, 0.48))

            Using fillBrushInBed As New SolidBrush(fillInBed)
                graphicsInBed.FillRectangle(fillBrushInBed, cellInBed)
            End Using
            Using borderPenInBed As New Pen(Color.FromArgb(28, 28, 28))
                graphicsInBed.DrawRectangle(borderPenInBed, cellInBed.X, cellInBed.Y, cellInBed.Width - 1, cellInBed.Height - 1)
            End Using
            TextRenderer.DrawText(graphicsInBed,
                                  CpuStateLabelsInBed(indexInBed),
                                  _ownerInBed.Font,
                                  cellInBed,
                                  textColorInBed,
                                  TextFormatFlags.HorizontalCenter Or TextFormatFlags.VerticalCenter Or
                                  TextFormatFlags.NoPadding Or TextFormatFlags.NoPrefix Or TextFormatFlags.SingleLine)
        Next
    End Sub

    Private Shared Function ScaleColorInBed(colorInBed As Color, factorInBed As Double) As Color
        If Double.IsNaN(factorInBed) OrElse Double.IsInfinity(factorInBed) Then factorInBed = 0.0
        factorInBed = Math.Max(0.0, Math.Min(1.0, factorInBed))
        Return Color.FromArgb(SafeRoundedIntInBed(CDbl(colorInBed.R) * factorInBed, 0, 255),
                              SafeRoundedIntInBed(CDbl(colorInBed.G) * factorInBed, 0, 255),
                              SafeRoundedIntInBed(CDbl(colorInBed.B) * factorInBed, 0, 255))
    End Function

    Private Shared Function SafeRoundedIntInBed(valueInBed As Double, minimumInBed As Integer, maximumInBed As Integer) As Integer
        If Double.IsNaN(valueInBed) OrElse Double.IsInfinity(valueInBed) Then Return minimumInBed
        valueInBed = Math.Max(CDbl(minimumInBed), Math.Min(CDbl(maximumInBed), valueInBed))
        Return Convert.ToInt32(Math.Round(valueInBed, MidpointRounding.AwayFromZero))
    End Function
End Class

' Host-only momentary chassis power button. It never touches guest state directly;
' Form1 translates the click into the explicit power lifecycle.
Public NotInheritable Class ChassisPowerButton
    Inherits Control

    Private _poweredInBed As Boolean
    Private _hotInBed As Boolean
    Private _pressedInBed As Boolean

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
        AccessibleName = "Chassis Power"
        AccessibleDescription = "Turn the emulated AT chassis power on or off"
    End Sub

    Public Sub SetPoweredInBed(poweredInBed As Boolean)
        If _poweredInBed = poweredInBed Then Return
        _poweredInBed = poweredInBed
        Invalidate()
    End Sub

    Protected Overrides Sub OnMouseEnter(e As EventArgs)
        _hotInBed = True
        Invalidate()
        MyBase.OnMouseEnter(e)
    End Sub

    Protected Overrides Sub OnMouseLeave(e As EventArgs)
        _hotInBed = False
        _pressedInBed = False
        Invalidate()
        MyBase.OnMouseLeave(e)
    End Sub

    Protected Overrides Sub OnMouseDown(e As MouseEventArgs)
        If e.Button = MouseButtons.Left Then
            _pressedInBed = True
            Invalidate()
        End If
        MyBase.OnMouseDown(e)
    End Sub

    Protected Overrides Sub OnMouseUp(e As MouseEventArgs)
        _pressedInBed = False
        Invalidate()
        MyBase.OnMouseUp(e)
    End Sub

    Protected Overrides Sub OnPaint(e As PaintEventArgs)
        MyBase.OnPaint(e)
        e.Graphics.SmoothingMode = SmoothingMode.AntiAlias

        Dim outerInBed As Rectangle = Rectangle.Inflate(ClientRectangle, -1, -1)
        Dim fillInBed As Color =
            If(_pressedInBed,
               Color.FromArgb(52, 52, 52),
               If(_hotInBed, Color.FromArgb(78, 78, 78), Color.FromArgb(62, 62, 62)))

        Using fillBrushInBed As New SolidBrush(fillInBed)
            e.Graphics.FillEllipse(fillBrushInBed, outerInBed)
        End Using
        Using rimPenInBed As New Pen(Color.FromArgb(26, 26, 26))
            e.Graphics.DrawEllipse(rimPenInBed, outerInBed)
        End Using

        Dim glyphInBed As Color =
            If(_poweredInBed, Color.LimeGreen, Color.FromArgb(150, 150, 150))
        Using glyphPenInBed As New Pen(glyphInBed, 1.7F)
            glyphPenInBed.StartCap = LineCap.Round
            glyphPenInBed.EndCap = LineCap.Round
            e.Graphics.DrawArc(glyphPenInBed, 4, 5, 10, 9, -45.0F, 270.0F)
            e.Graphics.DrawLine(glyphPenInBed, 9, 2, 9, 9)
        End Using
    End Sub
End Class

