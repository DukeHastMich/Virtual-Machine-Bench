Imports System.Collections.Generic
Imports System.IO

' CROMWELL TECHNOLOGIES KEYMASTER / CLORTO EDITION
' This host panel represents physical keys. It must always feed AtKeyboard101,
' never the BIOS key buffer, so DOS, protected-mode software, raw scan-code
' readers, the emulated serial keyboard link, 8042 translation, and IRQ1 all see
' the same machine-accurate path.
Public Class On_Screen_Keyboard
    ' Embedded copy of Resources\332aeaf1-91df-45ce-9a66-371fd04e8a90.png.
    ' Original PNG: 256x71, 897 bytes.
    ' SHA-256: 4AA406843D09CDE73A5AD4C7931ECC17F7086C960DBFCBE976BFAC2ECFC9B8EA
    ' Keeping the exact board-mark artwork here prevents WinForms designer/resource
    ' re-parenting from silently dropping the image at runtime.
    Private Const EmbeddedLogoPngBase64 As String = "iVBORw0KGgoAAAANSUhEUgAAAQAAAABHCAYAAADoWJkGAAAAAXNSR0IArs4c6QAAAARnQU1BAACxjwv8YQUAAAAJcEhZcwAADsMAAA7DAcdvqGQAAAMWSURBVHhe7dw9UuswFIbh47sEaEmXLAB6Qs+WUmdL9LAANuB00JItiIJ7mOE4iuQ/+ee8z4wL2bKtKJ5PsmOoRCQcj0dRh8NBKFOm7KNciUj4LQFw5Z9dAcAPAgBwjAAAHCMAAMeKPwQM4ed0581ORERuP0+mBoBSmAEAjg0WACGErAXAfAwWAACWp3MAMLIDy9c5AAAsX+sAiI34581OzpudVFV1cQEwP60DAMB69A4AHflvP09Xf9NnJgDMT+8AALBcrd8EtPf/vNEHLBczAMAxAgBwjAAAHCMAJqLvU3zdbeXrbptcb9k3MZvLi4TwYncbTG47S5mqPVOddygEAODYbALAjmBLTdRS7JuWJxE5/Xkj81mq6tnuhoFp/6feg5mr2QQAgPKKB0BshLdvCt581HLzUTfq1yFI3bjfjc8c7PbmUksIdXQ/XZ8q2/3sYuuVFus32y67PVZPxb4nyx6va/3Uflbsc6eOo9vfX9/k/fXtd72tHzuOPU/femMpHgAA5qN3AOgIkKLJpnS/WNLZvy7Ue6yt/Cx2e4rWb5a3/5e29K77Mnu+qemnzG2X7d/YPW7u8VTX+rnfs9X2etEZodZ/eNrLw9O+MRPIlft5bfti/T203gEAYLlaB0AsQe09jF2G03XEbuf6+C7JdmiCl070lLm2azzXv6emy/Xv949yv3+0q5NS/a3rc5+lDK11AABYj84BEJsJ5NJ7HpuIKToy28Qcmo4D9hmHLcfYGVCpRI/Rfku1K9a/tl4pth1d5R4nNiJ3lervvsfvq3MAAFi+1v8PIFcq1fTJaJvRH5iKXs9ru26ZAQCOjTYDUHYmsLYExTrZ61at7fplBgA4NvoMQK31HgpYMmYAgGOjB8DUv3MCiBs9AADMV7FnAADmhxkA4BgBADhGAACOEQCAYwQA4BgBADhGAACOEQCAYwQA4BgBADhGAACOEQCAYwQA4BgBADhGAACOEQCAYwQA4BgBADhGAACOEQCAYwQA4BgBADhGAACOEQCAYwQA4BgBADhGAACOEQCAYwQA4BgBADj2DQhKxTu95WMLAAAAAElFTkSuQmCC"
    Private Const WM_KEYDOWN As Integer = &H100
    Private Const WM_KEYUP As Integer = &H101
    Private Const WM_SYSKEYDOWN As Integer = &H104
    Private Const WM_SYSKEYUP As Integer = &H105
    Private ReadOnly _host As Form1
    Private ReadOnly _keyCaps As New Dictionary(Of AtPhysicalKey, Button)()
    Private ReadOnly _normalKeyCapSizes As New Dictionary(Of AtPhysicalKey, Size)()
    Private ReadOnly _mouseHeld As New HashSet(Of AtPhysicalKey)()
    Private ReadOnly _bufferedKeys As New Queue(Of BufferedStroke)()
    Private ReadOnly _bufferTimer As New System.Windows.Forms.Timer() With {.Interval = 45}
    Private _bufferCurrent As BufferedStroke
    Private _bufferKeyDown As Boolean
    Private _dropText As TextBox
    Private _queueStatus As Label
    Private _permanentClose As Boolean

    Private Structure BufferedStroke
        Public Key As AtPhysicalKey
        Public Shift As Boolean
    End Structure

    Public Sub New()
        InitializeComponent()
        _host = Form1.Current
    End Sub

    Public Sub New(host As Form1)
        InitializeComponent()
        _host = host
    End Sub

    Friend Sub AllowPermanentClose()
        _permanentClose = True
    End Sub

    ' Forward host key positions through the same decoder used by the CRT form.
    ' This is required because Windows delivers key messages to whichever host
    ' window owns focus; focusing Keymaster must not disconnect the real keyboard.
    Protected Overrides Sub WndProc(ByRef m As Message)
        If _host IsNot Nothing Then
            Select Case m.Msg
                Case WM_KEYDOWN, WM_SYSKEYDOWN
                    If _host.RoutePhysicalKeyboardMessage(m, pressed:=True) Then Return
                Case WM_KEYUP, WM_SYSKEYUP
                    If _host.RoutePhysicalKeyboardMessage(m, pressed:=False) Then Return
            End Select
        End If
        MyBase.WndProc(m)
    End Sub

    Private Sub On_Screen_Keyboard_Load(sender As Object, e As EventArgs) Handles MyBase.Load
        BuildKeyboardSurface()
        AddHandler _bufferTimer.Tick, AddressOf BufferedTypingTick
        If _host IsNot Nothing Then
            AddHandler _host.KeyboardVisualStateChanged, AddressOf HostKeyVisualStateChanged
        End If
        AddHandler AtKeyboard.LedStateChanged, AddressOf GuestLedStateChanged
        UpdateLedDisplay(AtKeyboard.LedState)
    End Sub

    Private Sub On_Screen_Keyboard_FormClosing(sender As Object, e As FormClosingEventArgs) Handles Me.FormClosing
        If Not _permanentClose AndAlso e.CloseReason = CloseReason.UserClosing Then
            e.Cancel = True
            Hide()
        End If
    End Sub

    Private Sub On_Screen_Keyboard_FormClosed(sender As Object, e As FormClosedEventArgs) Handles Me.FormClosed
        If _host IsNot Nothing Then
            RemoveHandler _host.KeyboardVisualStateChanged, AddressOf HostKeyVisualStateChanged
        End If
        RemoveHandler AtKeyboard.LedStateChanged, AddressOf GuestLedStateChanged
        RemoveHandler _bufferTimer.Tick, AddressOf BufferedTypingTick
        _bufferTimer.Dispose()
    End Sub

    Private Sub BuildKeyboardSurface()
        SuspendLayout()
        Controls.Clear()
        MinimumSize = New Size(1325, 545)
        ' MaximumSize treats zero as a literal zero-sized dimension rather than
        ' "unbounded" on this WinForms runtime.  Use the Win32 practical window
        ' extent so width stays fixed while height remains freely resizable.
        MaximumSize = New Size(1325, 4096)
        Size = New Size(1325, 545)
        KeyPreview = True
        AllowDrop = True

        Dim root As New TableLayoutPanel() With {
            .Dock = DockStyle.Fill, .BackColor = Color.FromArgb(24, 24, 24),
            .ColumnCount = 1, .RowCount = 3, .Padding = New Padding(10)
        }
        root.RowStyles.Add(New RowStyle(SizeType.Absolute, 82))
        ' Six 38-pixel caps plus their two-pixel margins consume exactly 42
        ' pixels per row.  Keeping this band fixed prevents TableLayoutPanel
        ' from distributing spare height as unrealistic gaps between key rows.
        root.RowStyles.Add(New RowStyle(SizeType.Absolute, 252))
        ' All resize surplus belongs to the text batch area.  The logo, LEDs,
        ' key sizes, row pitch, and keyboard geometry remain physically fixed.
        root.RowStyles.Add(New RowStyle(SizeType.Percent, 100.0F))
        Controls.Add(root)

        Dim header As New Panel() With {.Dock = DockStyle.Fill, .Margin = New Padding(0)}
        ' The form width is fixed. Do not right-anchor this control before its
        ' header parent has a real width: WinForms otherwise records a negative
        ' anchor distance and relocates the logo off-screen.
        Dim logoPanel As New Panel() With {
            .BackgroundImage = DecodeEmbeddedLogo(),
            .BackgroundImageLayout = ImageLayout.Zoom,
            .Size = New Size(198, 55),
            .Location = New Point(1080, 0),
            .Anchor = AnchorStyles.Top Or AnchorStyles.Left,
            .BorderStyle = BorderStyle.None,
            .TabStop = False,
            .BackColor = Color.Transparent
        }
        header.Controls.Add(logoPanel)
        AddLed(header, "Num", 962, _numLed)
        AddLed(header, "Caps", 1032, _capsLed)
        AddLed(header, "Scroll", 1105, _scrollLed)
        logoPanel.BringToFront()
        root.Controls.Add(header, 0, 0)

        Dim rows As New TableLayoutPanel() With {
            .Dock = DockStyle.Fill,
            .RowCount = 6,
            .ColumnCount = 1,
            .Margin = New Padding(0),
            .Padding = New Padding(0)
        }
        For n As Integer = 0 To 5
            ' A key is 38 pixels tall with two-pixel top/bottom margins. Using
            ' percentages rounded occasional rows down to 41 pixels, which made
            ' WinForms manufacture a tiny scrollbar for every affected row.
            rows.RowStyles.Add(New RowStyle(SizeType.Absolute, 42))
        Next
        root.Controls.Add(rows, 0, 1)

        Dim row As FlowLayoutPanel = NewKeyRow()
        AddKey(row, "Esc", AtPhysicalKey.Escape, 54) : AddSpacer(row, 34)
        For n As Integer = 1 To 12
            If n = 5 OrElse n = 9 Then AddSpacer(row, 27)
            AddKey(row, "F" & n, CType(AtPhysicalKey.F1 + n - 1, AtPhysicalKey))
        Next
        AddSpacer(row, 14) : AddKey(row, "PrtSc", AtPhysicalKey.PrintScreen, 54)
        AddKey(row, "Scroll", AtPhysicalKey.ScrollLock, 54) : AddKey(row, "Pause", AtPhysicalKey.Pause, 54)
        rows.Controls.Add(row, 0, 0)

        row = NewKeyRow()
        AddKey(row, "` ~", AtPhysicalKey.Grave)
        For n As Integer = 1 To 9 : AddKey(row, n.ToString(), CType(AtPhysicalKey.D1 + n - 1, AtPhysicalKey)) : Next
        AddKey(row, "0", AtPhysicalKey.D0) : AddKey(row, "- _", AtPhysicalKey.Minus) : AddKey(row, "= +", AtPhysicalKey.Equals)
        AddKey(row, "Backspace", AtPhysicalKey.Backspace, 90) : AddSpacer(row, 14)
        AddKey(row, "Ins", AtPhysicalKey.Insert, 54) : AddKey(row, "Home", AtPhysicalKey.Home, 54) : AddKey(row, "PgUp", AtPhysicalKey.PageUp, 54)
        AddSpacer(row, 14) : AddKey(row, "Num", AtPhysicalKey.NumLock, 54) : AddKey(row, "/", AtPhysicalKey.KeypadDivide, 54)
        AddKey(row, "*", AtPhysicalKey.KeypadMultiply, 54) : AddKey(row, "-", AtPhysicalKey.KeypadSubtract, 54)
        rows.Controls.Add(row, 0, 1)

        row = NewKeyRow() : AddKey(row, "Tab", AtPhysicalKey.Tab, 72)
        AddLetters(row, "QWERTYUIOP")
        AddKey(row, "[ {", AtPhysicalKey.LeftBracket) : AddKey(row, "] }", AtPhysicalKey.RightBracket)
        AddKey(row, "\ |", AtPhysicalKey.Backslash, 66) : AddSpacer(row, 14)
        AddKey(row, "Del", AtPhysicalKey.Delete, 54) : AddKey(row, "End", AtPhysicalKey.EndKey, 54) : AddKey(row, "PgDn", AtPhysicalKey.PageDown, 54)
        AddSpacer(row, 14) : AddKey(row, "7", AtPhysicalKey.Keypad7, 54) : AddKey(row, "8", AtPhysicalKey.Keypad8, 54)
        AddKey(row, "9", AtPhysicalKey.Keypad9, 54) : AddKey(row, "+", AtPhysicalKey.KeypadAdd, 54)
        rows.Controls.Add(row, 0, 2)

        row = NewKeyRow() : AddKey(row, "Caps", AtPhysicalKey.CapsLock, 86) : AddLetters(row, "ASDFGHJKL")
        AddKey(row, "; :", AtPhysicalKey.Semicolon) : AddKey(row, "' """, AtPhysicalKey.Quote)
        AddKey(row, "Enter", AtPhysicalKey.Enter, 104) : AddSpacer(row, 202)
        AddKey(row, "4", AtPhysicalKey.Keypad4, 54) : AddKey(row, "5", AtPhysicalKey.Keypad5, 54) : AddKey(row, "6", AtPhysicalKey.Keypad6, 54)
        AddSpacer(row, 58)
        rows.Controls.Add(row, 0, 3)

        row = NewKeyRow() : AddKey(row, "Shift", AtPhysicalKey.LeftShift, 112) : AddLetters(row, "ZXCVBNM")
        AddKey(row, ", <", AtPhysicalKey.Comma) : AddKey(row, ". >", AtPhysicalKey.Period) : AddKey(row, "/ ?", AtPhysicalKey.Slash)
        AddKey(row, "Shift", AtPhysicalKey.RightShift, 130) : AddSpacer(row, 14)
        AddSpacer(row, 58) : AddKey(row, "Up", AtPhysicalKey.Up, 54) : AddSpacer(row, 58)
        AddSpacer(row, 14) : AddKey(row, "1", AtPhysicalKey.Keypad1, 54) : AddKey(row, "2", AtPhysicalKey.Keypad2, 54)
        AddKey(row, "3", AtPhysicalKey.Keypad3, 54) : AddKey(row, "Enter", AtPhysicalKey.KeypadEnter, 54)
        rows.Controls.Add(row, 0, 4)

        row = NewKeyRow() : AddKey(row, "Ctrl", AtPhysicalKey.LeftControl, 72) : AddKey(row, "Alt", AtPhysicalKey.LeftAlt, 72)
        AddKey(row, "Space", AtPhysicalKey.Space, 420) : AddKey(row, "Alt", AtPhysicalKey.RightAlt, 72)
        AddKey(row, "Ctrl", AtPhysicalKey.RightControl, 114) : AddSpacer(row, 14)
        AddKey(row, "Left", AtPhysicalKey.Left, 54) : AddKey(row, "Down", AtPhysicalKey.Down, 54) : AddKey(row, "Right", AtPhysicalKey.Right, 54)
        AddSpacer(row, 14) : AddKey(row, "0", AtPhysicalKey.Keypad0, 112) : AddKey(row, ".", AtPhysicalKey.KeypadDecimal, 54)
        AddSpacer(row, 58)
        rows.Controls.Add(row, 0, 5)

        Dim input As New TableLayoutPanel() With {
            .Dock = DockStyle.Fill,
            .ColumnCount = 2,
            .RowCount = 1,
            .Margin = New Padding(0),
            .Padding = New Padding(4)
        }
        input.ColumnStyles.Add(New ColumnStyle(SizeType.Percent, 100.0F))
        input.ColumnStyles.Add(New ColumnStyle(SizeType.Absolute, 190))
        _dropText = New TextBox() With {
            .Dock = DockStyle.Fill, .Multiline = True, .AllowDrop = True,
            .BackColor = Color.White, .ForeColor = Color.Black,
            .ScrollBars = ScrollBars.Vertical,
            .Text = "Drop a plain-text file here or paste text to type into the virtual machine."
        }
        AddHandler _dropText.DragEnter, AddressOf DropTextDragEnter
        AddHandler _dropText.DragDrop, AddressOf DropTextDragDrop
        Dim context As New ContextMenuStrip()
        context.Items.Add("Paste and type", Nothing, AddressOf PasteAndQueue)
        _dropText.ContextMenuStrip = context
        input.Controls.Add(_dropText, 0, 0)

        Dim actions As New FlowLayoutPanel() With {
            .Dock = DockStyle.Fill,
            .FlowDirection = FlowDirection.TopDown,
            .WrapContents = False,
            .AutoScroll = False
        }
        Dim queueButton As New Button() With {.Text = "Type text into VM", .Width = 165, .Height = 27}
        AddHandler queueButton.Click, Sub() QueueText(_dropText.Text)
        Dim pasteButton As New Button() With {.Text = "Paste and type", .Width = 165, .Height = 27}
        AddHandler pasteButton.Click, AddressOf PasteAndQueue
        Dim clearButton As New Button() With {.Text = "Cancel typing", .Width = 165, .Height = 27}
        AddHandler clearButton.Click, Sub() ClearBufferedText()
        _queueStatus = New Label() With {
            .AutoSize = False,
            .Size = New Size(175, 20),
            .AutoEllipsis = True,
            .ForeColor = Color.White,
            .Text = "Nothing waiting"
        }
        StyleActionButton(queueButton)
        StyleActionButton(pasteButton)
        StyleActionButton(clearButton)
        actions.Controls.Add(queueButton) : actions.Controls.Add(pasteButton)
        actions.Controls.Add(clearButton) : actions.Controls.Add(_queueStatus)
        input.Controls.Add(actions, 1, 0)
        root.Controls.Add(input, 0, 2)
        ResumeLayout(True)
    End Sub

    Private _numLed As Panel
    Private _capsLed As Panel
    Private _scrollLed As Panel

    Private Shared Function NewKeyRow() As FlowLayoutPanel
        Return New FlowLayoutPanel() With {
            .Dock = DockStyle.Fill,
            .WrapContents = False,
            .AutoScroll = False,
            .Margin = New Padding(0),
            .Padding = New Padding(0)
        }
    End Function

    Private Shared Function DecodeEmbeddedLogo() As Image
        Dim bytes As Byte() = Convert.FromBase64String(EmbeddedLogoPngBase64)
        Using stream As New MemoryStream(bytes, writable:=False)
            Using source As Image = Image.FromStream(stream)
                ' Image.FromStream retains the stream; clone into an independent
                ' bitmap so the temporary decoder stream can be disposed safely.
                Return New Bitmap(source)
            End Using
        End Using
    End Function

    Private Shared Sub StyleActionButton(button As Button)
        button.FlatStyle = FlatStyle.Flat
        button.UseVisualStyleBackColor = False
        button.BackColor = Color.FromArgb(52, 52, 52)
        button.ForeColor = Color.White
        button.FlatAppearance.BorderColor = Color.Gray
    End Sub

    Private Sub AddLetters(row As FlowLayoutPanel, letters As String)
        For Each letter As Char In letters
            Dim key As AtPhysicalKey = CType([Enum].Parse(GetType(AtPhysicalKey), letter.ToString()), AtPhysicalKey)
            AddKey(row, letter.ToString(), key)
        Next
    End Sub

    Private Sub AddKey(row As FlowLayoutPanel, caption As String, key As AtPhysicalKey, Optional width As Integer = 48)
        ' The holder is the immutable mechanical key well. FlowLayoutPanel sees
        ' only this fixed outer footprint, so depressing the cap cannot trigger
        ' a row reflow or make neighboring keys jiggle.
        Dim holder As New Panel() With {
            .Size = New Size(width + 4, 42),
            .Margin = New Padding(0),
            .Padding = New Padding(0),
            .BackColor = Color.Transparent
        }
        Dim cap As New Button() With {
            .Text = caption, .Width = width, .Height = 38,
            .Location = New Point(2, 2), .Margin = New Padding(0),
            .Tag = key, .FlatStyle = FlatStyle.Flat,
            .BackColor = Color.FromArgb(52, 52, 52), .ForeColor = Color.White,
            .UseVisualStyleBackColor = False, .TabStop = False
        }
        cap.FlatAppearance.BorderColor = Color.Gray
        AddHandler cap.MouseDown, AddressOf KeyCapMouseDown
        AddHandler cap.MouseUp, AddressOf KeyCapMouseUp
        AddHandler cap.MouseCaptureChanged, AddressOf KeyCapMouseCaptureChanged
        holder.Controls.Add(cap)
        row.Controls.Add(holder)
        _keyCaps(key) = cap
        _normalKeyCapSizes(key) = cap.Size
    End Sub

    Private Shared Sub AddSpacer(row As FlowLayoutPanel, width As Integer)
        row.Controls.Add(New Panel() With {.Width = width, .Height = 38, .Margin = New Padding(0)})
    End Sub

    Private Shared Sub AddLed(header As Panel, caption As String, x As Integer, ByRef led As Panel)
        header.Controls.Add(New Label() With {.Text = caption, .ForeColor = Color.White, .AutoSize = True, .Location = New Point(x, 57)})
        led = New Panel() With {.Size = New Size(15, 15), .Location = New Point(x + 42, 57), .BackColor = Color.FromArgb(30, 55, 30), .BorderStyle = BorderStyle.FixedSingle}
        header.Controls.Add(led)
    End Sub

    Private Sub KeyCapMouseDown(sender As Object, e As MouseEventArgs)
        If e.Button <> MouseButtons.Left OrElse _host Is Nothing Then Return
        Dim key As AtPhysicalKey = DirectCast(DirectCast(sender, Button).Tag, AtPhysicalKey)
        If key = AtPhysicalKey.Pause Then
            _host.SetHostPhysicalKey(key, True) : _host.SetHostPhysicalKey(key, False)
        ElseIf _mouseHeld.Add(key) Then
            _host.SetHostPhysicalKey(key, True)
        End If
    End Sub

    Private Sub KeyCapMouseUp(sender As Object, e As MouseEventArgs)
        ReleaseMouseKey(DirectCast(DirectCast(sender, Button).Tag, AtPhysicalKey))
    End Sub

    Private Sub KeyCapMouseCaptureChanged(sender As Object, e As EventArgs)
        If Control.MouseButtons = MouseButtons.None Then
            ReleaseMouseKey(DirectCast(DirectCast(sender, Button).Tag, AtPhysicalKey))
        End If
    End Sub

    Private Sub ReleaseMouseKey(key As AtPhysicalKey)
        If _mouseHeld.Remove(key) AndAlso _host IsNot Nothing Then _host.SetHostPhysicalKey(key, False)
    End Sub

    Private Sub HostKeyVisualStateChanged(key As AtPhysicalKey, pressed As Boolean)
        If Not Visible Then Return
        If InvokeRequired Then
            BeginInvoke(New Action(Of AtPhysicalKey, Boolean)(AddressOf HostKeyVisualStateChanged), key, pressed)
            Return
        End If
        Dim cap As Button = Nothing
        If _keyCaps.TryGetValue(key, cap) Then
            Dim normalSize As Size = _normalKeyCapSizes(key)
            cap.Size = If(pressed,
                          New Size(Math.Max(1, normalSize.Width - 2), Math.Max(1, normalSize.Height - 2)),
                          normalSize)
            cap.Location = If(pressed, New Point(3, 3), New Point(2, 2))
            cap.BackColor = Color.FromArgb(52, 52, 52)
            cap.FlatAppearance.BorderColor = If(pressed, Color.FromArgb(36, 36, 36), Color.Gray)
        End If
    End Sub

    Private Sub On_Screen_Keyboard_VisibleChanged(sender As Object, e As EventArgs) Handles Me.VisibleChanged
        If Visible Then
            If _bufferedKeys.Count <> 0 Then _bufferTimer.Start()
            Return
        End If
        ReleaseBufferedStroke()
        _bufferTimer.Stop()
        _mouseHeld.Clear()
        For Each cap As Button In _keyCaps.Values
            Dim key As AtPhysicalKey = DirectCast(cap.Tag, AtPhysicalKey)
            cap.Size = _normalKeyCapSizes(key)
            cap.Location = New Point(2, 2)
            cap.BackColor = Color.FromArgb(52, 52, 52)
            cap.FlatAppearance.BorderColor = Color.Gray
        Next
    End Sub

    Private Sub On_Screen_Keyboard_Deactivate(sender As Object, e As EventArgs) Handles Me.Deactivate
        If _host IsNot Nothing Then _host.ReleaseAllHostPhysicalKeys()
        _mouseHeld.Clear()
    End Sub

    Private Sub GuestLedStateChanged(state As Byte)
        If IsDisposed OrElse Disposing Then Return
        If InvokeRequired Then
            Try
                BeginInvoke(New Action(Of Byte)(AddressOf GuestLedStateChanged), state)
            Catch ex As InvalidOperationException
            End Try
            Return
        End If
        UpdateLedDisplay(state)
    End Sub

    Private Sub UpdateLedDisplay(state As Byte)
        If _numLed Is Nothing Then Return
        _scrollLed.BackColor = If((state And 1) <> 0, Color.Lime, Color.FromArgb(30, 55, 30))
        _numLed.BackColor = If((state And 2) <> 0, Color.Lime, Color.FromArgb(30, 55, 30))
        _capsLed.BackColor = If((state And 4) <> 0, Color.Lime, Color.FromArgb(30, 55, 30))
    End Sub

    Private Sub DropTextDragEnter(sender As Object, e As DragEventArgs)
        e.Effect = If(e.Data.GetDataPresent(DataFormats.FileDrop), DragDropEffects.Copy, DragDropEffects.None)
    End Sub

    Private Sub DropTextDragDrop(sender As Object, e As DragEventArgs)
        Dim paths As String() = TryCast(e.Data.GetData(DataFormats.FileDrop), String())
        If paths Is Nothing OrElse paths.Length = 0 Then Return
        Try
            Dim info As New FileInfo(paths(0))
            If info.Length > 1024 * 1024 Then Throw New InvalidDataException("Text-drop files are limited to 1 MiB.")
            Dim text As String = File.ReadAllText(paths(0))
            _dropText.Text = text
            QueueText(text)
        Catch ex As Exception
            MessageBox.Show(Me, ex.Message, "Text drop rejected", MessageBoxButtons.OK, MessageBoxIcon.Warning)
        End Try
    End Sub

    Private Sub PasteAndQueue(sender As Object, e As EventArgs)
        If Not Clipboard.ContainsText() Then Return
        Dim text As String = Clipboard.GetText(TextDataFormat.UnicodeText)
        _dropText.Text = text
        QueueText(text)
    End Sub

    Private Sub QueueText(text As String)
        If String.IsNullOrEmpty(text) Then Return
        Dim rejected As Integer
        For Each character As Char In text.Replace(vbCrLf, vbLf).Replace(vbCr, vbLf)
            Dim stroke As BufferedStroke
            If TryMapCharacter(character, stroke) Then
                _bufferedKeys.Enqueue(stroke)
            Else
                rejected += 1
            End If
        Next
        UpdateQueueStatus(rejected)
        If Visible AndAlso _bufferedKeys.Count <> 0 Then _bufferTimer.Start()
    End Sub

    Private Sub ClearBufferedText()
        ReleaseBufferedStroke()
        _bufferedKeys.Clear()
        _bufferTimer.Stop()
        UpdateQueueStatus(0)
    End Sub

    Private Sub BufferedTypingTick(sender As Object, e As EventArgs)
        If _host Is Nothing OrElse Not Visible Then Return
        If _bufferKeyDown Then
            ReleaseBufferedStroke()
        ElseIf _bufferedKeys.Count <> 0 Then
            _bufferCurrent = _bufferedKeys.Dequeue()
            If _bufferCurrent.Shift Then _host.SetHostPhysicalKey(AtPhysicalKey.LeftShift, True)
            _host.SetHostPhysicalKey(_bufferCurrent.Key, True)
            _bufferKeyDown = True
        Else
            _bufferTimer.Stop()
        End If
        UpdateQueueStatus(0)
    End Sub

    Private Sub ReleaseBufferedStroke()
        If Not _bufferKeyDown Then Return
        If _host IsNot Nothing Then
            _host.SetHostPhysicalKey(_bufferCurrent.Key, False)
            If _bufferCurrent.Shift Then _host.SetHostPhysicalKey(AtPhysicalKey.LeftShift, False)
        End If
        _bufferKeyDown = False
    End Sub

    Private Sub UpdateQueueStatus(rejected As Integer)
        If _queueStatus Is Nothing Then Return
        Dim count As Integer = _bufferedKeys.Count + If(_bufferKeyDown, 1, 0)
        _queueStatus.Text = If(count = 0, "Nothing waiting", "Characters waiting: " & count.ToString())
        If rejected <> 0 Then _queueStatus.Text &= " (" & rejected.ToString() & " unsupported)"
    End Sub

    ' US 101-key character chords. Caps Lock is included when deciding whether
    ' Shift is needed, so buffered alphabetic text remains correct when the guest
    ' has changed the keyboard LED state.
    Private Shared Function TryMapCharacter(character As Char, ByRef stroke As BufferedStroke) As Boolean
        stroke = New BufferedStroke()
        If Char.IsLetter(character) AndAlso AscW(character) < 128 Then
            stroke.Key = CType([Enum].Parse(GetType(AtPhysicalKey), Char.ToUpperInvariant(character).ToString()), AtPhysicalKey)
            Dim capsOn As Boolean = (AtKeyboard.LedState And 4) <> 0
            stroke.Shift = Char.IsUpper(character) Xor capsOn
            Return True
        End If
        If Char.IsDigit(character) Then
            stroke.Key = CType([Enum].Parse(GetType(AtPhysicalKey), "D" & character), AtPhysicalKey)
            Return True
        End If
        Select Case character
            Case " "c : stroke.Key = AtPhysicalKey.Space
            Case ControlChars.Lf : stroke.Key = AtPhysicalKey.Enter
            Case ControlChars.Tab : stroke.Key = AtPhysicalKey.Tab
            Case ChrW(8) : stroke.Key = AtPhysicalKey.Backspace
            Case "-"c : stroke.Key = AtPhysicalKey.Minus
            Case "_"c : stroke.Key = AtPhysicalKey.Minus : stroke.Shift = True
            Case "="c : stroke.Key = AtPhysicalKey.Equals
            Case "+"c : stroke.Key = AtPhysicalKey.Equals : stroke.Shift = True
            Case "["c : stroke.Key = AtPhysicalKey.LeftBracket
            Case "{"c : stroke.Key = AtPhysicalKey.LeftBracket : stroke.Shift = True
            Case "]"c : stroke.Key = AtPhysicalKey.RightBracket
            Case "}"c : stroke.Key = AtPhysicalKey.RightBracket : stroke.Shift = True
            Case "\"c : stroke.Key = AtPhysicalKey.Backslash
            Case "|"c : stroke.Key = AtPhysicalKey.Backslash : stroke.Shift = True
            Case ";"c : stroke.Key = AtPhysicalKey.Semicolon
            Case ":"c : stroke.Key = AtPhysicalKey.Semicolon : stroke.Shift = True
            Case "'"c : stroke.Key = AtPhysicalKey.Quote
            Case """"c : stroke.Key = AtPhysicalKey.Quote : stroke.Shift = True
            Case ","c : stroke.Key = AtPhysicalKey.Comma
            Case "<"c : stroke.Key = AtPhysicalKey.Comma : stroke.Shift = True
            Case "."c : stroke.Key = AtPhysicalKey.Period
            Case ">"c : stroke.Key = AtPhysicalKey.Period : stroke.Shift = True
            Case "/"c : stroke.Key = AtPhysicalKey.Slash
            Case "?"c : stroke.Key = AtPhysicalKey.Slash : stroke.Shift = True
            Case "`"c : stroke.Key = AtPhysicalKey.Grave
            Case "~"c : stroke.Key = AtPhysicalKey.Grave : stroke.Shift = True
            Case "!"c : stroke.Key = AtPhysicalKey.D1 : stroke.Shift = True
            Case "@"c : stroke.Key = AtPhysicalKey.D2 : stroke.Shift = True
            Case "#"c : stroke.Key = AtPhysicalKey.D3 : stroke.Shift = True
            Case "$"c : stroke.Key = AtPhysicalKey.D4 : stroke.Shift = True
            Case "%"c : stroke.Key = AtPhysicalKey.D5 : stroke.Shift = True
            Case "^"c : stroke.Key = AtPhysicalKey.D6 : stroke.Shift = True
            Case "&"c : stroke.Key = AtPhysicalKey.D7 : stroke.Shift = True
            Case "*"c : stroke.Key = AtPhysicalKey.D8 : stroke.Shift = True
            Case "("c : stroke.Key = AtPhysicalKey.D9 : stroke.Shift = True
            Case ")"c : stroke.Key = AtPhysicalKey.D0 : stroke.Shift = True
            Case Else : Return False
        End Select
        Return True
    End Function
End Class
