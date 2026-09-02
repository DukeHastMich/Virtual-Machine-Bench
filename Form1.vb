Imports System.IO 'for loading the ROM files
Imports System.Collections.Generic
Imports System.Math 'For advanced Math Functions
Public Class Form1
    Public Shared Current As Form1
    ' CROMWELL HOST REFIT BRICK 9C - the complete guest machine executes on one
    ' dedicated thread. UI access crosses MachineRuntime286's ownership gate.
    Private ReadOnly _machineRuntime As New MachineRuntime286(CPU0, SystemBus, MachineClock)
    Private ReadOnly _cpuPerfLockInBed As New Object()
    Private _videoPresentation As DiamondStealthPro928PresentationWorker
    Private _videoPresentationGenerationInBed As Long
    Private _displayedPresentationFrameInBed As Bitmap
    Private _closingInBed As Boolean
    Private _machinePoweredInBed As Boolean = True
    Private _powerMenuItemInBed As ToolStripMenuItem
    Private _resetMenuItemInBed As ToolStripMenuItem
    Private _chassisPanel As ChassisPanelForm
    Private _onScreenKeyboardInBed As On_Screen_Keyboard
    Private _serialMouseCapturedInBed As Boolean
    Private _serialMouseHostCursorHiddenInBed As Boolean
    Private _serialMouseLeftInBed As Boolean
    Private _serialMouseRightInBed As Boolean
    Private _serialMouseMenuItemInBed As ToolStripMenuItem
    ' Host pointer messages can arrive much faster than the emulated 1200-baud
    ' Microsoft mouse can report them.  Do not make the UI thread wait for the
    ' machine ownership gate once per WM_MOUSEMOVE.  Coalesce raw host counts
    ' here and transfer them to the physical mouse at the next machine boundary.
    Private _serialMousePendingHostXInBed As Long
    Private _serialMousePendingHostYInBed As Long
    ' A period Microsoft ball mouse produced far fewer counts per inch than a
    ' modern high-resolution host pointer. Preserve fractional host travel so
    ' fine motion and shallow diagonals survive the resolution conversion.
    Private Const SerialMouseHostPixelsPerCountInBed As Integer = 4
    Private _serialMouseHostRemainderXInBed As Integer
    Private _serialMouseHostRemainderYInBed As Integer
    Private _serialMouseRecenteringInBed As Boolean
    Private _serialMouseHostMoveMessagesInBed As Long
    Private _serialMouseBoundaryTransfersInBed As Long
    Private _serialMouseUncapturedTitleInBed As String
    Private floppyAStatus As ToolStripMenuItem
    Private floppyBStatus As ToolStripMenuItem
    Private hardDiskStatus As ToolStripMenuItem
    Private cdRomStatus As ToolStripMenuItem
    Private _ideDriveShelf As IdeDriveShelf
    Private ideDriveShelfMenu As ToolStripMenuItem
    Private _mountedIdeDriveId As Integer = -1
    ' CROMWELL TECHNOLOGIES SNEAKER NET / FLOPPY BOX BRICK 1
    Private _floppyBox As FloppyBox
    Private _sneakerNetForm As SneakerNetForm
    Private _driveBayPanelInBed As Panel
    Private _floppyBayAInBed As PictureBox
    Private _floppyBayBInBed As PictureBox
    Private _driveBayToolTipInBed As ToolTip
    Private _threeAndHalfDriveFaceInBed As Bitmap
    Private _fiveAndQuarterDriveFaceInBed As Bitmap
    Private _emptyDriveFaceInBed As Bitmap
    Private floppyAMountMenu As ToolStripMenuItem
    Private floppyBMountMenu As ToolStripMenuItem
    ' CROMWELL KEYBOARD REALITY BRICK 3 FIELDS
    ' CROMWELL KEYBOARD REALITY BRICK 4 PANEL
    ' CROMWELL QBASIC RAW KEYBOARD TRACE PANEL
    ' CROMWELL BIOS KEYBOARD RING FORENSIC PANEL
    ' CROMWELL VGA MODE TRANSITION TRACE PANEL
    Private _ramBanks As RamBankConfiguration
    Private ramBanksMenu As ToolStripMenuItem
    ' CROMWELL SYSTEM CONFIGURATION DRAWER BRICK 01
    Private _systemConfigurationDrawer As SystemConfigurationDrawer
    Private _pendingMemoryMb As Integer?
    Private _isaCardConfiguration As IsaExpansionCardConfiguration
    Private _pendingSoundBlaster16Jumpers As SoundBlaster16JumperSettings
    Private _pendingNe2000Jumpers As Ne2000JumperSettings

    ' CROMWELL CPU PERFORMANCE DIAGNOSTIC
    Private _cpuPerfAccumulatedTStates As Long
    Private _cpuPerfAccumulatedHostTicks As Long
    Private _cpuPerfEffectiveTStatesPerSecond As Double
    Private _cpuPerfLastSliceMilliseconds As Double
    Private _cpuPerfTargetClockHz As Long
    Private _cpuPerfSamplesReady As Boolean
    ' CROMWELL CALIBRATED DYNO / PCB REFIT PHASE 2 BRICK 8A
    ' Rate windows are aligned end-of-slice to end-of-slice.  This counts both
    ' RunSlice execution and the host/UI gap between slices, matching actual wall
    ' throughput without pairing current work with the previous timer interval.
    Private _cpuPerfAlignedLastRecordTicksInBed As Long
    Private _cpuPerfSliceCostAccumTicksInBed As Long
    Private _cpuPerfSliceCostMinTicksInBed As Long = Long.MaxValue
    Private _cpuPerfSliceCostMaxTicksInBed As Long
    Private _cpuPerfSliceCostCountInBed As Long
    Private _cpuPerfAverageSliceMillisecondsInBed As Double
    Private _cpuPerfMinimumSliceMillisecondsInBed As Double
    Private _cpuPerfMaximumSliceMillisecondsInBed As Double
    Private _cpuPerfLastWindowSliceCountInBed As Long
    Private _cpuPerfForm As System.Windows.Forms.Form
    Private _cpuPerfText As System.Windows.Forms.Label
    Private _cpuPerfUiTimer As System.Windows.Forms.Timer

    Private Const WM_KEYDOWN As Integer = &H100
    Private Const WM_KEYUP As Integer = &H101
    Private Const WM_SYSKEYDOWN As Integer = &H104
    Private Const WM_SYSKEYUP As Integer = &H105

    ' Every host-side keyboard source reports physical key state through this
    ' event.  The Keymaster panel uses it only for key-cap animation; guest input
    ' still travels through AtKeyboard101 and the real emulated 8042 serial link.
    Friend Event KeyboardVisualStateChanged(key As AtPhysicalKey, pressed As Boolean)

    Private Sub WithMachineInBed(actionInBed As Action)
        _machineRuntime.Execute(actionInBed)
    End Sub

    Private Function ReadMachineInBed(Of TResult)(readerInBed As Func(Of TResult)) As TResult
        Return _machineRuntime.Query(readerInBed)
    End Function

    ' The host frontend reports physical key positions only.  Windows' raw
    ' message scan field and E0/extended bit are used to distinguish main Enter
    ' from keypad Enter, dedicated navigation from keypad keys, and left/right
    ' modifiers.  No guest scan code is created in the UI layer.
    Protected Overrides Sub WndProc(ByRef m As Message)
        If _serialMouseCapturedInBed AndAlso
           (m.Msg = WM_KEYDOWN OrElse m.Msg = WM_SYSKEYDOWN) AndAlso
           CType(CInt(m.WParam.ToInt64() And &HFFFFL), Keys) = Keys.M Then
            Dim modifiersInBed As Keys = Control.ModifierKeys
            If (modifiersInBed And Keys.Control) <> 0 AndAlso
               (modifiersInBed And Keys.Alt) <> 0 Then
                ReleaseSerialMouseCaptureInBed()
                Return
            End If
        End If
        Select Case m.Msg
            Case WM_KEYDOWN, WM_SYSKEYDOWN
                If RoutePhysicalKeyboardMessage(m, pressed:=True) Then Return
            Case WM_KEYUP, WM_SYSKEYUP
                If RoutePhysicalKeyboardMessage(m, pressed:=False) Then Return
        End Select
        MyBase.WndProc(m)
    End Sub

    ' KeyPreview causes keyboard messages addressed to a child HWND to visit the
    ' form before that child consumes them.  The display/capture boundary can
    ' leave Win32 keyboard focus on PictureBox1 even though mouse capture and
    ' keyboard focus are electrically unrelated.  Preserve the original raw
    ' LPARAM scan field here and feed exactly the same physical-key path used by
    ' WndProc.  ToolStrip messages remain host UI input and are never sent to the
    ' guest.  Messages addressed to the form itself are handled once by WndProc.
    Protected Overrides Function ProcessKeyPreview(ByRef m As Message) As Boolean
        If m.HWnd <> Handle AndAlso ContainsFocus Then
            Dim targetInBed As Control = Control.FromHandle(m.HWnd)
            If targetInBed Is PictureBox1 Then
                Select Case m.Msg
                    Case WM_KEYDOWN, WM_SYSKEYDOWN
                        If RoutePhysicalKeyboardMessage(m, pressed:=True) Then Return True
                    Case WM_KEYUP, WM_SYSKEYUP
                        If RoutePhysicalKeyboardMessage(m, pressed:=False) Then Return True
                End Select
            End If
        End If
        Return MyBase.ProcessKeyPreview(m)
    End Function

    Friend Function RoutePhysicalKeyboardMessage(ByRef m As Message, pressed As Boolean) As Boolean
        If _closingInBed OrElse Not _machinePoweredInBed Then Return False
        Dim lp As Long = m.LParam.ToInt64()
        Dim scan As Byte = CByte((lp >> 16) And &HFF)
        Dim extended As Boolean = (lp And &H1000000L) <> 0
        Dim virtualKey As Keys = CType(CInt(m.WParam.ToInt64() And &HFFFFL), Keys)

        ' Preserve the existing host chassis reset shortcut.  Ctrl and Alt key
        ' transitions have already reached the physical keyboard; the R itself
        ' remains a host command exactly as the ToolStrip shortcut intended.
        If pressed AndAlso virtualKey = Keys.R Then
            Dim mods As Keys = Control.ModifierKeys
            If (mods And Keys.Control) <> 0 AndAlso (mods And Keys.Alt) <> 0 Then Return False
        End If

        Dim key As AtPhysicalKey = HostPhysicalKey(scan, extended, virtualKey)
        If key = AtPhysicalKey.None Then Return False

        ' Pause has a make sequence with no separate break sequence on an AT
        ' enhanced keyboard.  Windows may not deliver a conventional key-up.
        If key = AtPhysicalKey.Pause AndAlso pressed Then
            WithMachineInBed(
                Sub()
                    AtKeyboard.SetPhysicalKey(key, True)
                    AtKeyboard.SetPhysicalKey(key, False)
                End Sub)
            RaiseEvent KeyboardVisualStateChanged(key, True)
            RaiseEvent KeyboardVisualStateChanged(key, False)
            Return True
        End If

        SetHostPhysicalKey(key, pressed)
        Return True
    End Function

    ' Shared entry point for the real host keyboard, clickable key caps, and the
    ' text-drop sequencer.  Keeping these sources together prevents the panel
    ' from bypassing keyboard firmware, scan-set selection, typematic behavior,
    ' the serial wire, 8042 translation, IRQ1, or the guest's LED commands.
    Friend Sub SetHostPhysicalKey(key As AtPhysicalKey, pressed As Boolean)
        If key = AtPhysicalKey.None OrElse _closingInBed OrElse Not _machinePoweredInBed Then Return
        WithMachineInBed(Sub() AtKeyboard.SetPhysicalKey(key, pressed))
        RaiseEvent KeyboardVisualStateChanged(key, pressed)
    End Sub

    Friend Sub ReleaseAllHostPhysicalKeys()
        If _closingInBed OrElse Not _machinePoweredInBed Then Return
        WithMachineInBed(Sub() AtKeyboard.ReleaseAllPhysicalKeys())
    End Sub

    Private Sub Form1_DeactivateKeyboard(sender As Object, e As EventArgs) Handles Me.Deactivate
        ' If Windows removes input focus while a key is held, synthesize the
        ' physical releases which the emulator can no longer observe. During
        ' FormClosing, however, Deactivate is a host lifecycle event after guest
        ' input has been shut down; never call a disposed MachineRuntime from it.
        If _closingInBed OrElse Not _machinePoweredInBed Then Return
        WithMachineInBed(Sub() AtKeyboard.ReleaseAllPhysicalKeys())
        ReleaseSerialMouseCaptureInBed()
    End Sub

    Private Sub PictureBox1_MouseDown(sender As Object, e As MouseEventArgs) Handles PictureBox1.MouseDown
        If _closingInBed OrElse Not _machinePoweredInBed Then Return
        If Not _serialMouseCapturedInBed Then
            If e.Button = MouseButtons.Left Then CaptureSerialMouseInBed()
            Return
        End If
        If _serialMouseCapturedInBed AndAlso e.Button = MouseButtons.Middle Then
            ReleaseSerialMouseCaptureInBed()
            Return
        End If
        If e.Button = MouseButtons.Left Then _serialMouseLeftInBed = True
        If e.Button = MouseButtons.Right Then _serialMouseRightInBed = True
        RouteSerialMouseButtonsInBed()
    End Sub

    Private Sub PictureBox1_MouseUp(sender As Object, e As MouseEventArgs) Handles PictureBox1.MouseUp
        If Not _serialMouseCapturedInBed Then Return
        If e.Button = MouseButtons.Left Then _serialMouseLeftInBed = False
        If e.Button = MouseButtons.Right Then _serialMouseRightInBed = False
        RouteSerialMouseButtonsInBed()
    End Sub

    Private Sub PictureBox1_MouseMove(sender As Object, e As MouseEventArgs) Handles PictureBox1.MouseMove
        If _closingInBed OrElse Not _machinePoweredInBed Then Return
        If Not _serialMouseCapturedInBed Then Return
        Dim centerInBed As New Point(Math.Max(0, PictureBox1.ClientSize.Width \ 2),
                                     Math.Max(0, PictureBox1.ClientSize.Height \ 2))

        ' Cursor.Position is the current physical host pointer.  MouseEventArgs
        ' can describe a WM_MOUSEMOVE which was already queued before our last
        ' center warp.  Treating those stale coordinates as new motion creates
        ' an artificial reverse delta, another warp, and a self-sustaining
        ' crawl even while the operator is not touching the mouse.
        Dim capturedPointInBed As Point = PictureBox1.PointToClient(Cursor.Position)
        If capturedPointInBed.X = centerInBed.X AndAlso capturedPointInBed.Y = centerInBed.Y Then
            _serialMouseRecenteringInBed = False
            Return
        End If
        _serialMouseRecenteringInBed = False
        Dim deltaXInBed As Integer = capturedPointInBed.X - centerInBed.X
        Dim deltaYInBed As Integer = capturedPointInBed.Y - centerInBed.Y
        If deltaXInBed = 0 AndAlso deltaYInBed = 0 Then Return
        QueueSerialMouseHostMovementInBed(deltaXInBed, deltaYInBed)
        _serialMouseRecenteringInBed = True
        Cursor.Position = PictureBox1.PointToScreen(centerInBed)
    End Sub

    Private Sub PictureBox1_MouseEnter(sender As Object, e As EventArgs) Handles PictureBox1.MouseEnter
        ClearSerialMouseHostMovementInBed()
        SetSerialMouseHostCursorHiddenInBed(_serialMouseCapturedInBed)
    End Sub

    Private Sub PictureBox1_MouseLeave(sender As Object, e As EventArgs) Handles PictureBox1.MouseLeave
        If _serialMouseCapturedInBed Then Return
        ClearSerialMouseHostMovementInBed()
        SetSerialMouseHostCursorHiddenInBed(False)
        If _serialMouseLeftInBed OrElse _serialMouseRightInBed Then
            _serialMouseLeftInBed = False
            _serialMouseRightInBed = False
            RouteSerialMouseButtonsInBed()
        End If
    End Sub

    Private Sub PictureBox1_MouseCaptureChanged(sender As Object, e As EventArgs) Handles PictureBox1.MouseCaptureChanged
        If Not _serialMouseCapturedInBed OrElse PictureBox1.Capture OrElse
           _closingInBed OrElse Not _machinePoweredInBed OrElse Not ContainsFocus Then Return

        ' WinForms may release a control's native mouse capture as part of the
        ' ordinary left/right button-up lifecycle.  That is not an operator
        ' request to disconnect the guest mouse.  Reacquire after the current
        ' message unwinds; explicit release paths clear the logical flag first.
        BeginInvoke(
            New Action(
                Sub()
                    If _serialMouseCapturedInBed AndAlso Not _closingInBed AndAlso
                       _machinePoweredInBed AndAlso ContainsFocus Then
                        PictureBox1.Capture = True
                    End If
                End Sub))
    End Sub

    Private Sub CaptureSerialMouseInBed()
        If _serialMouseCapturedInBed OrElse _closingInBed OrElse Not _machinePoweredInBed Then Return
        ' The display is not a keyboard-bearing child control.  Clear any menu or
        ' button focus before taking Win32 mouse capture so WM_KEY messages keep
        ' arriving at this form's raw AT-keyboard scan-code router.
        ActiveControl = Nothing
        Activate()
        Focus()
        _serialMouseCapturedInBed = True
        ClearSerialMouseHostMovementInBed()
        PictureBox1.Capture = True
        SetSerialMouseHostCursorHiddenInBed(True)
        _serialMouseRecenteringInBed = True
        Cursor.Position = PictureBox1.PointToScreen(New Point(Math.Max(0, PictureBox1.ClientSize.Width \ 2),
                                                               Math.Max(0, PictureBox1.ClientSize.Height \ 2)))
        If String.IsNullOrEmpty(_serialMouseUncapturedTitleInBed) Then _serialMouseUncapturedTitleInBed = Text
        Text = _serialMouseUncapturedTitleInBed & " — Mouse captured (Ctrl+Alt+M or middle-click to release)"
        If _serialMouseMenuItemInBed IsNot Nothing Then
            _serialMouseMenuItemInBed.Checked = True
            _serialMouseMenuItemInBed.Text = "Release COM1 serial mouse"
        End If
    End Sub

    Private Sub ReleaseSerialMouseCaptureInBed()
        If Not _serialMouseCapturedInBed Then
            SetSerialMouseHostCursorHiddenInBed(False)
            Return
        End If
        _serialMouseCapturedInBed = False
        _serialMouseRecenteringInBed = False
        ClearSerialMouseHostMovementInBed()
        _serialMouseLeftInBed = False
        _serialMouseRightInBed = False
        If Not _closingInBed Then
            Try
                WithMachineInBed(Sub() SerialMouse.SetHostButtons(False, False))
            Catch
                ' The machine owner may already be stopping during host teardown.
            End Try
        End If
        PictureBox1.Capture = False
        SetSerialMouseHostCursorHiddenInBed(False)
        If Not String.IsNullOrEmpty(_serialMouseUncapturedTitleInBed) Then Text = _serialMouseUncapturedTitleInBed
        If _serialMouseMenuItemInBed IsNot Nothing Then
            _serialMouseMenuItemInBed.Checked = False
            _serialMouseMenuItemInBed.Text = "Capture COM1 serial mouse"
        End If
        If Not _closingInBed AndAlso _machinePoweredInBed Then
            ActiveControl = Nothing
            Activate()
            Focus()
        End If
    End Sub

    Private Sub SetSerialMouseHostCursorHiddenInBed(hiddenInBed As Boolean)
        If hiddenInBed = _serialMouseHostCursorHiddenInBed Then Return
        _serialMouseHostCursorHiddenInBed = hiddenInBed
        If hiddenInBed Then
            Cursor.Hide()
        Else
            Cursor.Show()
        End If
    End Sub

    Private Sub RouteSerialMouseButtonsInBed()
        If _closingInBed OrElse Not _machinePoweredInBed Then Return
        Dim leftInBed As Boolean = _serialMouseLeftInBed
        Dim rightInBed As Boolean = _serialMouseRightInBed
        WithMachineInBed(Sub() SerialMouse.SetHostButtons(leftInBed, rightInBed))
    End Sub

    Private Sub QueueSerialMouseHostMovementInBed(deltaXInBed As Integer, deltaYInBed As Integer)
        Threading.Interlocked.Increment(_serialMouseHostMoveMessagesInBed)
        Dim accumulatedXInBed As Integer = _serialMouseHostRemainderXInBed + deltaXInBed
        Dim accumulatedYInBed As Integer = _serialMouseHostRemainderYInBed + deltaYInBed
        Dim mouseCountXInBed As Integer = accumulatedXInBed \ SerialMouseHostPixelsPerCountInBed
        Dim mouseCountYInBed As Integer = accumulatedYInBed \ SerialMouseHostPixelsPerCountInBed
        _serialMouseHostRemainderXInBed = accumulatedXInBed Mod SerialMouseHostPixelsPerCountInBed
        _serialMouseHostRemainderYInBed = accumulatedYInBed Mod SerialMouseHostPixelsPerCountInBed
        If mouseCountXInBed <> 0 Then Threading.Interlocked.Add(_serialMousePendingHostXInBed, CLng(mouseCountXInBed))
        If mouseCountYInBed <> 0 Then Threading.Interlocked.Add(_serialMousePendingHostYInBed, CLng(mouseCountYInBed))
    End Sub

    Private Sub ClearSerialMouseHostMovementInBed()
        Threading.Interlocked.Exchange(_serialMousePendingHostXInBed, 0L)
        Threading.Interlocked.Exchange(_serialMousePendingHostYInBed, 0L)
        _serialMouseHostRemainderXInBed = 0
        _serialMouseHostRemainderYInBed = 0
    End Sub

    ' Called only by MachineRuntime286 while it owns the complete guest.  The
    ' host accumulator is transport decoupling, not a virtual-input shortcut:
    ' after this handoff the counts still obey mouse sampling, 1200-baud serial
    ' framing, the 16550 receive path, PIC IRQ4, and the guest mouse driver.
    Private Sub DrainSerialMouseHostMovementAtBoundaryInBed()
        Dim deltaXInBed As Long = Threading.Interlocked.Exchange(_serialMousePendingHostXInBed, 0L)
        Dim deltaYInBed As Long = Threading.Interlocked.Exchange(_serialMousePendingHostYInBed, 0L)
        If deltaXInBed = 0 AndAlso deltaYInBed = 0 Then Return

        Const MaximumTransferInBed As Long = Integer.MaxValue
        Dim boundedXInBed As Integer = CInt(Math.Max(-MaximumTransferInBed, Math.Min(MaximumTransferInBed, deltaXInBed)))
        Dim boundedYInBed As Integer = CInt(Math.Max(-MaximumTransferInBed, Math.Min(MaximumTransferInBed, deltaYInBed)))
        SerialMouse.AddHostMovement(boundedXInBed, boundedYInBed)
        Threading.Interlocked.Increment(_serialMouseBoundaryTransfersInBed)
    End Sub

    Friend Shared Function HostPhysicalKey(scan As Byte, extended As Boolean, virtualKey As Keys) As AtPhysicalKey
        If virtualKey = Keys.Pause Then Return AtPhysicalKey.Pause
        If virtualKey = Keys.Snapshot Then Return AtPhysicalKey.PrintScreen

        Select Case scan
            Case &H1 : Return AtPhysicalKey.Escape
            Case &H2 : Return AtPhysicalKey.D1
            Case &H3 : Return AtPhysicalKey.D2
            Case &H4 : Return AtPhysicalKey.D3
            Case &H5 : Return AtPhysicalKey.D4
            Case &H6 : Return AtPhysicalKey.D5
            Case &H7 : Return AtPhysicalKey.D6
            Case &H8 : Return AtPhysicalKey.D7
            Case &H9 : Return AtPhysicalKey.D8
            Case &HA : Return AtPhysicalKey.D9
            Case &HB : Return AtPhysicalKey.D0
            Case &HC : Return AtPhysicalKey.Minus
            Case &HD : Return AtPhysicalKey.Equals
            Case &HE : Return AtPhysicalKey.Backspace
            Case &HF : Return AtPhysicalKey.Tab
            Case &H10 : Return AtPhysicalKey.Q
            Case &H11 : Return AtPhysicalKey.W
            Case &H12 : Return AtPhysicalKey.E
            Case &H13 : Return AtPhysicalKey.R
            Case &H14 : Return AtPhysicalKey.T
            Case &H15 : Return AtPhysicalKey.Y
            Case &H16 : Return AtPhysicalKey.U
            Case &H17 : Return AtPhysicalKey.I
            Case &H18 : Return AtPhysicalKey.O
            Case &H19 : Return AtPhysicalKey.P
            Case &H1A : Return AtPhysicalKey.LeftBracket
            Case &H1B : Return AtPhysicalKey.RightBracket
            Case &H1C : Return If(extended, AtPhysicalKey.KeypadEnter, AtPhysicalKey.Enter)
            Case &H1D : Return If(extended, AtPhysicalKey.RightControl, AtPhysicalKey.LeftControl)
            Case &H1E : Return AtPhysicalKey.A
            Case &H1F : Return AtPhysicalKey.S
            Case &H20 : Return AtPhysicalKey.D
            Case &H21 : Return AtPhysicalKey.F
            Case &H22 : Return AtPhysicalKey.G
            Case &H23 : Return AtPhysicalKey.H
            Case &H24 : Return AtPhysicalKey.J
            Case &H25 : Return AtPhysicalKey.K
            Case &H26 : Return AtPhysicalKey.L
            Case &H27 : Return AtPhysicalKey.Semicolon
            Case &H28 : Return AtPhysicalKey.Quote
            Case &H29 : Return AtPhysicalKey.Grave
            Case &H2A : Return AtPhysicalKey.LeftShift
            Case &H2B : Return AtPhysicalKey.Backslash
            Case &H2C : Return AtPhysicalKey.Z
            Case &H2D : Return AtPhysicalKey.X
            Case &H2E : Return AtPhysicalKey.C
            Case &H2F : Return AtPhysicalKey.V
            Case &H30 : Return AtPhysicalKey.B
            Case &H31 : Return AtPhysicalKey.N
            Case &H32 : Return AtPhysicalKey.M
            Case &H33 : Return AtPhysicalKey.Comma
            Case &H34 : Return AtPhysicalKey.Period
            Case &H35 : Return If(extended, AtPhysicalKey.KeypadDivide, AtPhysicalKey.Slash)
            Case &H36 : Return AtPhysicalKey.RightShift
            Case &H37 : Return If(extended, AtPhysicalKey.PrintScreen, AtPhysicalKey.KeypadMultiply)
            Case &H38 : Return If(extended, AtPhysicalKey.RightAlt, AtPhysicalKey.LeftAlt)
            Case &H39 : Return AtPhysicalKey.Space
            Case &H3A : Return AtPhysicalKey.CapsLock
            Case &H3B : Return AtPhysicalKey.F1
            Case &H3C : Return AtPhysicalKey.F2
            Case &H3D : Return AtPhysicalKey.F3
            Case &H3E : Return AtPhysicalKey.F4
            Case &H3F : Return AtPhysicalKey.F5
            Case &H40 : Return AtPhysicalKey.F6
            Case &H41 : Return AtPhysicalKey.F7
            Case &H42 : Return AtPhysicalKey.F8
            Case &H43 : Return AtPhysicalKey.F9
            Case &H44 : Return AtPhysicalKey.F10
            Case &H45 : Return If(virtualKey = Keys.Pause, AtPhysicalKey.Pause, AtPhysicalKey.NumLock)
            Case &H46 : Return AtPhysicalKey.ScrollLock
            Case &H47 : Return If(extended, AtPhysicalKey.Home, AtPhysicalKey.Keypad7)
            Case &H48 : Return If(extended, AtPhysicalKey.Up, AtPhysicalKey.Keypad8)
            Case &H49 : Return If(extended, AtPhysicalKey.PageUp, AtPhysicalKey.Keypad9)
            Case &H4A : Return AtPhysicalKey.KeypadSubtract
            Case &H4B : Return If(extended, AtPhysicalKey.Left, AtPhysicalKey.Keypad4)
            Case &H4C : Return AtPhysicalKey.Keypad5
            Case &H4D : Return If(extended, AtPhysicalKey.Right, AtPhysicalKey.Keypad6)
            Case &H4E : Return AtPhysicalKey.KeypadAdd
            Case &H4F : Return If(extended, AtPhysicalKey.EndKey, AtPhysicalKey.Keypad1)
            Case &H50 : Return If(extended, AtPhysicalKey.Down, AtPhysicalKey.Keypad2)
            Case &H51 : Return If(extended, AtPhysicalKey.PageDown, AtPhysicalKey.Keypad3)
            Case &H52 : Return If(extended, AtPhysicalKey.Insert, AtPhysicalKey.Keypad0)
            Case &H53 : Return If(extended, AtPhysicalKey.Delete, AtPhysicalKey.KeypadDecimal)
            Case &H57 : Return AtPhysicalKey.F11
            Case &H58 : Return AtPhysicalKey.F12
            Case Else : Return AtPhysicalKey.None
        End Select
    End Function

    Private Sub ShowOnScreenKeyboardInBed()
        If _onScreenKeyboardInBed Is Nothing OrElse _onScreenKeyboardInBed.IsDisposed Then
            _onScreenKeyboardInBed = New On_Screen_Keyboard()
        End If
        If Not _onScreenKeyboardInBed.Visible Then _onScreenKeyboardInBed.Show(Me)
        _onScreenKeyboardInBed.BringToFront()
    End Sub

    '____________________________________________________________________________________________________________________|
    ' CROMWELL FRONT PANEL OVERLAY: machine telemetry is painted by the owned
    ' borderless ChassisPanelForm; Form1 no longer drives individual lamps.
    Private Sub InitializeChassisPanel()
        If _chassisPanel IsNot Nothing Then Return
        _chassisPanel = New ChassisPanelForm(Me, FrontPanel)
        AddHandler _chassisPanel.ConfigurationToggleRequested,
            Sub()
                If _systemConfigurationDrawer Is Nothing Then InitializeSystemConfigurationInBed()
                _systemConfigurationDrawer.ToggleDrawer()
            End Sub
        AddHandler _chassisPanel.PowerToggleRequested,
            Sub() ToggleMachinePowerInBed()
        _chassisPanel.Show(Me)
    End Sub

    Private Sub SystemConfigurationDrawerMotionChangedInBed(directionInBed As Integer,
                                                             movingInBed As Boolean)
        If _chassisPanel Is Nothing OrElse _chassisPanel.IsDisposed Then Return
        _chassisPanel.SetConfigurationMotionInBed(directionInBed, movingInBed)
    End Sub

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
                        reportInBed.AppendLine("===== ISA EXPANSION CARDS =====")
                        reportInBed.AppendLine(SoundBlaster16.DiagnosticText())
                        reportInBed.AppendLine(Ne2000.DiagnosticText())
                        reportInBed.AppendLine(SerialMouse.DiagnosticText())
                        reportInBed.AppendLine(
                            $"Serial mouse host frontend: captured={_serialMouseCapturedInBed} " &
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

    Private Sub Form1_Load(sender As System.Object, e As System.EventArgs) Handles MyBase.Load
        Current = Me
        SystemLoop.Enabled = False
        ' Preserve the emulated CRT face geometry when the host window is resized.
        PictureBox1.SizeMode = PictureBoxSizeMode.Zoom

        ' Load the physical ISA card straps before the devices are registered on
        ' the motherboard bus.  This is host chassis state, not guest CMOS.
        InitializeIsaExpansionCardConfigurationInBed()

        ' Bring only the real motherboard/device fabric up before the reset vector.
        ' The historical CGA host renderer/resources remain compiled archaeology,
        ' but they are no longer initialized in the normal machine path.
        InitializeHardware()


        InitializeRamBanks()
        Dim romPath As String = Path.Combine(AppContext.BaseDirectory, "Firmware", "atbios.rom")
        CPU0.MirrorLegacyMemory = False
        CPU0.MirrorLegacyTextCells = False
        CPU0.HostFirmwareInterrupts = False
        CPU0.LoadSystemRom(File.ReadAllBytes(romPath))

        Dim videoRomPath As String = Path.Combine(AppContext.BaseDirectory, "Firmware", "stealthpro.rom")
        VideoCard.LoadOptionRom(File.ReadAllBytes(videoRomPath))

        ' JP-DIAG is a host service jumper, not guest-visible VGA hardware.
        ' Normal power-on state is OPEN: no forensic overlay.
        VideoCard.DiagnosticOverlayJumperClosed = False
        InitializeIdeDriveShelf()
        InitializeFloppyBox()
        CreateMediaMenu()
        InitializeDriveBayPanelInBed()
        InitializeSystemConfigurationInBed()
        BeginInvoke(New Action(AddressOf InitializeChassisPanel))

        ' The historical WinForms CGA renderers are now presentation fossils.
        ' Guest software sees only the S3/VGA device and its VRAM/registers.
        Mode2.Enabled = False
        Mode3.Enabled = False
        Mode4.Enabled = False
        GPU.Interval = 16
        GPU.Enabled = False

        CPU0.Reset()
        ApplyFirmwareResetVectorSafetyGuardInBed()
        MachineClock.Reset()
        RestoreHostExecutionRateInBed()

        AddHandler _machineRuntime.SliceCompleted, AddressOf MachineRuntimeSliceCompletedInBed
        AddHandler _machineRuntime.RuntimeFaulted, AddressOf MachineRuntimeFaultedInBed

        ' Presentation requests are serviced by the machine thread at a slice
        ' boundary while it already owns the guest. The raster worker never races
        ' the machine gate and therefore cannot starve for seconds under timing debt.
        _videoPresentation = New DiamondStealthPro928PresentationWorker()
        AddHandler _videoPresentation.PresentationFaulted, AddressOf VideoPresentationFaultedInBed
        _videoPresentation.Start()
        _machineRuntime.SetBoundaryService(
            Sub()
                DrainSerialMouseHostMovementAtBoundaryInBed()
                Dim presentationInBed As DiamondStealthPro928PresentationWorker = _videoPresentation
                If presentationInBed IsNot Nothing Then
                    presentationInBed.ServiceCaptureAtMachineBoundary(VideoCard)
                End If
            End Sub)

        _machineRuntime.Start()
        GPU.Enabled = True
    End Sub

    ' Host reset-vector guard retained intentionally.  If firmware is blank at
    ' FFFF:0000 after any host-initiated reset/power-on, do not let uninitialized
    ' bytes execute as guest code. Architectural HLT behavior remains untouched.
    Private Sub ApplyFirmwareResetVectorSafetyGuardInBed()
        If CPU0.ReadWord(&HFFFF0UI) = 0 AndAlso CPU0.ReadWord(&HFFFF2UI) = 0 Then
            CPU0.Halted = True
        End If
    End Sub

    Private Sub MachineRuntimeSliceCompletedInBed(consumedTStatesInBed As Long,
                                                   elapsedHostTicksInBed As Long,
                                                   executionHostTicksInBed As Long,
                                                   targetClockHzInBed As Long)
        RecordCpuPerformanceInBed(consumedTStatesInBed,
                                  elapsedHostTicksInBed,
                                  executionHostTicksInBed,
                                  targetClockHzInBed)
    End Sub

    Private Sub MachineRuntimeFaultedInBed(faultInBed As Exception)
        If IsDisposed OrElse Disposing Then Return
        Try
            BeginInvoke(
                New Action(
                    Sub()
                        If IsDisposed OrElse Disposing Then Return
                        MessageBox.Show(Me,
                                        faultInBed.ToString(),
                                        "Virtual machine execution stopped",
                                        MessageBoxButtons.OK,
                                        MessageBoxIcon.Error)
                    End Sub))
        Catch
            ' The form may be tearing down while the worker reports the fault.
        End Try
    End Sub


    Private Sub VideoPresentationFaultedInBed(faultInBed As Exception)
        If IsDisposed OrElse Disposing Then Return
        Try
            BeginInvoke(
                New Action(
                    Sub()
                        If IsDisposed OrElse Disposing Then Return
                        GPU.Enabled = False
                        MessageBox.Show(Me,
                                        "Host video presentation stopped, but the guest machine is still running." &
                                        Environment.NewLine & Environment.NewLine & faultInBed.ToString(),
                                        "Host video presentation stopped",
                                        MessageBoxButtons.OK,
                                        MessageBoxIcon.Error)
                    End Sub))
        Catch
            ' The form may be tearing down while the presentation worker reports.
        End Try
    End Sub


    Private Sub Form1_FormClosing(sender As Object, e As FormClosingEventArgs) Handles Me.FormClosing
        ' WinForms can deliver Deactivate and other focus/input callbacks during
        ' teardown. Mark shutdown before disposing any machine-owned object.
        _closingInBed = True
        ReleaseSerialMouseCaptureInBed()
        GPU.Enabled = False

        ' Remove the machine-boundary presentation callback while both objects are
        ' still alive. SetBoundaryService crosses the same ownership gate, so when
        ' it returns no in-flight boundary capture can still reference the worker.
        _machineRuntime.SetBoundaryService(Nothing)

        If _videoPresentation IsNot Nothing Then
            If _displayedPresentationFrameInBed IsNot Nothing Then
                PictureBox1.Image = Nothing
                _videoPresentation.RecycleFrame(_displayedPresentationFrameInBed)
                _displayedPresentationFrameInBed = Nothing
            End If
            _videoPresentation.Dispose()
            _videoPresentation = Nothing
        End If

        _machineRuntime.Stop()
        ' The printers sit outside the motherboard reset domain, but they do own
        ' host spool files.  Closing the emulator therefore ejects and finalizes
        ' any sheet still under the virtual print head before process teardown.
        Lpt1Printer.Dispose()
        Lpt2Printer.Dispose()
        SoundBlaster16.Dispose()
        Ne2000.Dispose()
        RealTimeClock.Save()
        If _systemConfigurationDrawer IsNot Nothing AndAlso Not _systemConfigurationDrawer.IsDisposed Then
            _systemConfigurationDrawer.Close()
        End If
        If _chassisPanel IsNot Nothing Then _chassisPanel.Close()
        If _sneakerNetForm IsNot Nothing AndAlso Not _sneakerNetForm.IsDisposed Then _sneakerNetForm.Close()
        If _onScreenKeyboardInBed IsNot Nothing AndAlso Not _onScreenKeyboardInBed.IsDisposed Then
            '_onScreenKeyboardInBed.AllowPermanentClose()
            _onScreenKeyboardInBed.Close()
        End If
        _machineRuntime.Dispose()
    End Sub

    Private Sub CreateMediaMenu()
        Dim menu As New MenuStrip()
        Dim machine As New ToolStripMenuItem("Machine")
        Dim media As New ToolStripMenuItem("Media")
        Dim printer As ToolStripMenuItem = BuildPrinterMenuInBed()
        Dim cards As ToolStripMenuItem = BuildExpansionCardsMenuInBed()
        AddHandler media.DropDownOpening, Sub() UpdateMediaStatus()
        ideDriveShelfMenu = New ToolStripMenuItem("IDE Drive Shelf")
        AddHandler ideDriveShelfMenu.DropDownOpening, Sub() RebuildIdeDriveShelfMenu()
        floppyAMountMenu = New ToolStripMenuItem("Mount floppy A")
        floppyBMountMenu = New ToolStripMenuItem("Mount floppy B")
        AddHandler floppyAMountMenu.DropDownOpening,
            Sub() RebuildFloppyBoxMountMenu(floppyAMountMenu, 0)
        AddHandler floppyBMountMenu.DropDownOpening,
            Sub() RebuildFloppyBoxMountMenu(floppyBMountMenu, 1)
        _powerMenuItemInBed = New ToolStripMenuItem("Power Off")
        AddHandler _powerMenuItemInBed.Click, Sub() ToggleMachinePowerInBed()

        _resetMenuItemInBed = New ToolStripMenuItem("Reset") With {
            .ShortcutKeys = Keys.Control Or Keys.Alt Or Keys.R,
            .ShowShortcutKeys = True
        }
        AddHandler _resetMenuItemInBed.Click, Sub() ResetThroughFirmware()
        Dim turboItem As New ToolStripMenuItem("Turbo 25 MHz") With {
            .CheckOnClick = True,
            .Checked = ReadMachineInBed(Function() MachineClock.TurboEnabled)
        }
        AddHandler turboItem.CheckedChanged,
            Sub()
                WithMachineInBed(Sub() MachineClock.SetTurbo(turboItem.Checked))
                Text = "Virtual Computer - " & MachineProfile286.ProcessorModel &
                       If(turboItem.Checked, " @ 25 MHz", " @ 20 MHz")
            End Sub
        machine.DropDownItems.Add(_powerMenuItemInBed)
        machine.DropDownItems.Add(_resetMenuItemInBed)
        machine.DropDownItems.Add(turboItem)
        machine.DropDownItems.Add("On-screen keyboard...", Nothing, Sub() ShowOnScreenKeyboardInBed())
        _serialMouseMenuItemInBed = New ToolStripMenuItem("Capture COM1 serial mouse") With {
            .CheckOnClick = False,
            .ShortcutKeys = Keys.Control Or Keys.Alt Or Keys.M,
            .ShowShortcutKeys = True,
            .ToolTipText = "Capture the guest pointer; release with Ctrl+Alt+M, middle-click, or this menu"
        }
        AddHandler _serialMouseMenuItemInBed.Click,
            Sub()
                If _serialMouseCapturedInBed Then
                    ReleaseSerialMouseCaptureInBed()
                Else
                    CaptureSerialMouseInBed()
                End If
            End Sub
        machine.DropDownItems.Add(_serialMouseMenuItemInBed)
        UpdatePowerControlsInBed()

        Dim configurationMenuInBed As New ToolStripMenuItem("System Configuration")
        configurationMenuInBed.DropDownItems.Add("Overview", Nothing, Sub() OpenSystemConfigurationInBed("overview"))
        Dim systemMenuInBed As New ToolStripMenuItem("System")
        systemMenuInBed.DropDownItems.Add("Motherboard", Nothing, Sub() OpenSystemConfigurationInBed("motherboard"))
        systemMenuInBed.DropDownItems.Add("Chipset", Nothing, Sub() OpenSystemConfigurationInBed("chipset"))
        systemMenuInBed.DropDownItems.Add("Processor", Nothing, Sub() OpenSystemConfigurationInBed("processor"))
        systemMenuInBed.DropDownItems.Add("Memory", Nothing, Sub() OpenSystemConfigurationInBed("memory"))
        systemMenuInBed.DropDownItems.Add("Firmware", Nothing, Sub() OpenSystemConfigurationInBed("firmware"))
        configurationMenuInBed.DropDownItems.Add(systemMenuInBed)
        Dim storageMenuInBed As New ToolStripMenuItem("Storage")
        storageMenuInBed.DropDownItems.Add("Floppy Drives", Nothing, Sub() OpenSystemConfigurationInBed("floppy"))
        storageMenuInBed.DropDownItems.Add("Hard Drives", Nothing, Sub() OpenSystemConfigurationInBed("harddisk"))
        storageMenuInBed.DropDownItems.Add("Optical Drives", Nothing, Sub() OpenSystemConfigurationInBed("optical"))
        configurationMenuInBed.DropDownItems.Add(storageMenuInBed)
        Dim expansionMenuInBed As New ToolStripMenuItem("Expansion")
        expansionMenuInBed.DropDownItems.Add("Video", Nothing, Sub() OpenSystemConfigurationInBed("video"))
        expansionMenuInBed.DropDownItems.Add("Audio", Nothing, Sub() OpenSystemConfigurationInBed("audio"))
        expansionMenuInBed.DropDownItems.Add("Network", Nothing, Sub() OpenSystemConfigurationInBed("network"))
        configurationMenuInBed.DropDownItems.Add(expansionMenuInBed)
        Dim ioMenuInBed As New ToolStripMenuItem("I/O && Ports")
        ioMenuInBed.DropDownItems.Add("Overview", Nothing, Sub() OpenSystemConfigurationInBed("io"))
        ioMenuInBed.DropDownItems.Add("Serial Ports", Nothing, Sub() OpenSystemConfigurationInBed("serial"))
        ioMenuInBed.DropDownItems.Add("Parallel Ports", Nothing, Sub() OpenSystemConfigurationInBed("parallel"))
        ioMenuInBed.DropDownItems.Add("Game / MIDI", Nothing, Sub() OpenSystemConfigurationInBed("midi"))
        ioMenuInBed.DropDownItems.Add("Resource Map", Nothing, Sub() OpenSystemConfigurationInBed("resources"))
        configurationMenuInBed.DropDownItems.Add(ioMenuInBed)
        machine.DropDownItems.Add(New ToolStripSeparator())
        machine.DropDownItems.Add(configurationMenuInBed)

        Dim serviceJumpers As New ToolStripMenuItem("Service Jumpers")
        Dim vgaDiagnosticJumper As New ToolStripMenuItem("JP-DIAG: VGA Diagnostic Overlay") With {
            .CheckOnClick = True,
            .Checked = ReadMachineInBed(Function() VideoCard.DiagnosticOverlayJumperClosed),
            .ToolTipText = "Checked = jumper CLOSED/installed; unchecked = OPEN/off"
        }
        AddHandler vgaDiagnosticJumper.CheckedChanged,
            Sub() WithMachineInBed(Sub() VideoCard.DiagnosticOverlayJumperClosed = vgaDiagnosticJumper.Checked)
        serviceJumpers.DropDownItems.Add(vgaDiagnosticJumper)
        machine.DropDownItems.Add(New ToolStripSeparator())
        machine.DropDownItems.Add(serviceJumpers)
        Dim diagnosticsMenu As New ToolStripMenuItem("Diagnostics")
        Dim dumpAllDiagnosticsMenuInBed As New ToolStripMenuItem("Dump all diagnostics")
        AddHandler dumpAllDiagnosticsMenuInBed.Click,
            Sub() DumpAllDiagnosticsInBed(dumpAllDiagnosticsMenuInBed)
        diagnosticsMenu.DropDownItems.Add(dumpAllDiagnosticsMenuInBed)
        diagnosticsMenu.DropDownItems.Add(New ToolStripSeparator())
        diagnosticsMenu.DropDownItems.Add("CPU performance...", Nothing, Sub() ShowCpuPerformanceDiagnosticsInBed())
        diagnosticsMenu.DropDownItems.Add("CPU / PIC / PIT live state...", Nothing, Sub() ShowCpuInterruptLiveStateInBed())
        diagnosticsMenu.DropDownItems.Add(New ToolStripSeparator())
        diagnosticsMenu.DropDownItems.Add("Keyboard live state...", Nothing, Sub() ShowKeyboardLiveState())
        diagnosticsMenu.DropDownItems.Add("Show raw keyboard wire trace...", Nothing, Sub() ShowKeyboardWireTrace())
        diagnosticsMenu.DropDownItems.Add(New ToolStripSeparator())
        diagnosticsMenu.DropDownItems.Add("Begin/clear BIOS keyboard-ring trace", Nothing, Sub() WithMachineInBed(Sub() CPU0.BeginDiagnosticBiosKeyboardTrace()))
        diagnosticsMenu.DropDownItems.Add("Stop BIOS keyboard-ring trace", Nothing, Sub() WithMachineInBed(Sub() CPU0.EndDiagnosticBiosKeyboardTrace()))
        diagnosticsMenu.DropDownItems.Add("Show BIOS keyboard-ring trace...", Nothing, Sub() ShowBiosKeyboardRingTrace())
        diagnosticsMenu.DropDownItems.Add("BIOS keyboard-ring live state...", Nothing, Sub() ShowBiosKeyboardRingState())
        diagnosticsMenu.DropDownItems.Add("Clear raw keyboard wire trace", Nothing, Sub() WithMachineInBed(Sub() KeyboardController.ClearDiagnosticTrace()))
        diagnosticsMenu.DropDownItems.Add("Run keyboard hardware self-test...", Nothing, Sub() RunKeyboardHardwareSelfTest())
        diagnosticsMenu.DropDownItems.Add(New ToolStripSeparator())
        diagnosticsMenu.DropDownItems.Add("Begin/clear VGA mode trace", Nothing, Sub() WithMachineInBed(Sub() VideoCard.BeginDiagnosticVgaTrace()))
        diagnosticsMenu.DropDownItems.Add("Stop VGA mode trace", Nothing, Sub() WithMachineInBed(Sub() VideoCard.EndDiagnosticVgaTrace()))
        diagnosticsMenu.DropDownItems.Add("Show VGA mode trace...", Nothing, Sub() ShowVgaModeTrace())
        diagnosticsMenu.DropDownItems.Add("VGA live state...", Nothing, Sub() ShowVgaStateSnapshot())
        diagnosticsMenu.DropDownItems.Add("80287 flight recorder...", Nothing, Sub() ShowNumericCoprocessorFlightRecorderInBed())
        diagnosticsMenu.DropDownItems.Add(New ToolStripSeparator())
        diagnosticsMenu.DropDownItems.Add("Begin/clear important INTn trace", Nothing, Sub() WithMachineInBed(Sub() CPU0.BeginDiagnosticImportantIntTrace()))
        diagnosticsMenu.DropDownItems.Add("Stop important INTn trace", Nothing, Sub() WithMachineInBed(Sub() CPU0.EndDiagnosticImportantIntTrace()))
        diagnosticsMenu.DropDownItems.Add("Show important INTn trace...", Nothing, Sub() ShowImportantIntTrace())
        diagnosticsMenu.DropDownItems.Add(New ToolStripSeparator())
        diagnosticsMenu.DropDownItems.Add("Show QB EXEC forensic trace...", Nothing, Sub() ShowQbExecForensicTraceInBed())
        diagnosticsMenu.DropDownItems.Add("Clear/disarm QB EXEC forensic trace", Nothing, Sub() WithMachineInBed(Sub() CPU0.ClearDiagnosticQbExecTrace()))
        machine.DropDownItems.Add(diagnosticsMenu)
        ramBanksMenu = BuildRamBanksMenu()
        machine.DropDownItems.Add(ramBanksMenu)
        floppyAStatus = New ToolStripMenuItem("Floppy A mounted") With {.Enabled = False}
        floppyBStatus = New ToolStripMenuItem("Floppy B mounted") With {.Enabled = False}
        hardDiskStatus = New ToolStripMenuItem("Hard disk mounted") With {.Enabled = False}
        cdRomStatus = New ToolStripMenuItem("CD-ROM mounted") With {.Enabled = False}
        media.DropDownItems.AddRange({floppyAStatus, floppyBStatus, hardDiskStatus, cdRomStatus, New ToolStripSeparator()})
        media.DropDownItems.Add("Sneaker Net...", Nothing, Sub() RunSneakerNet())
        media.DropDownItems.Add("Open Disk Box", Nothing, Sub() OpenFloppyBox())
        media.DropDownItems.Add(New ToolStripSeparator())
        media.DropDownItems.Add(floppyAMountMenu)
        media.DropDownItems.Add(floppyBMountMenu)
        media.DropDownItems.Add("Eject floppy A", Nothing, Sub() EjectFloppy(0))
        media.DropDownItems.Add("Eject floppy B", Nothing, Sub() EjectFloppy(1))
        media.DropDownItems.Add("Boot floppy A", Nothing, Sub() BootFloppyA())
        media.DropDownItems.Add(New ToolStripSeparator())
        media.DropDownItems.Add(ideDriveShelfMenu)
        media.DropDownItems.Add("Create 64 MB IDE shelf drive...", Nothing, Sub() CreateHardDisk())
        media.DropDownItems.Add("Eject hard disk", Nothing, Sub() EjectHardDisk())
        media.DropDownItems.Add("Boot hard disk", Nothing, Sub() BootHardDisk())
        media.DropDownItems.Add(New ToolStripSeparator())
        media.DropDownItems.Add("Mount CD-ROM ISO...", Nothing, Sub() ChooseIso())
        media.DropDownItems.Add("Eject CD-ROM", Nothing, Sub() EjectCdRom())
        menu.Items.Add(machine)
        menu.Items.Add(media)
        menu.Items.Add(printer)
        menu.Items.Add(cards)
        MainMenuStrip = menu
        Controls.Add(menu)
        menu.BringToFront()
        UpdateMediaStatus()
    End Sub

    Private Function BuildExpansionCardsMenuInBed() As ToolStripMenuItem
        Dim rootInBed As New ToolStripMenuItem("Cards")

        Dim sbStatusInBed As New ToolStripMenuItem() With {.Enabled = False}
        Dim neStatusInBed As New ToolStripMenuItem() With {.Enabled = False}
        rootInBed.DropDownItems.Add(sbStatusInBed)
        rootInBed.DropDownItems.Add(neStatusInBed)
        rootInBed.DropDownItems.Add(New ToolStripSeparator())

        rootInBed.DropDownItems.Add("Sound Blaster 16 diagnostics", Nothing,
            Sub()
                Dim textInBed As String = ReadMachineInBed(Function() SoundBlaster16.DiagnosticText())
                ShowDumpableDiagnosticInBed("Sound Blaster 16", textInBed, "soundblaster16-diagnostic.txt", 980, 700)
            End Sub)
        rootInBed.DropDownItems.Add("NE2000 diagnostics", Nothing,
            Sub()
                Dim textInBed As String = ReadMachineInBed(Function() Ne2000.DiagnosticText())
                ShowDumpableDiagnosticInBed("NE2000", textInBed, "ne2000-diagnostic.txt", 980, 700)
            End Sub)

        rootInBed.DropDownItems.Add(New ToolStripSeparator())
        rootInBed.DropDownItems.Add("Connect NE2000 UDP cable...", Nothing,
            Sub()
                Try
                    Dim localTextInBed As String = Microsoft.VisualBasic.Interaction.InputBox(
                        "Local UDP port for this virtual Ethernet cable endpoint:",
                        "NE2000 UDP cable", "19860")
                    If String.IsNullOrWhiteSpace(localTextInBed) Then Return
                    Dim peerHostInBed As String = Microsoft.VisualBasic.Interaction.InputBox(
                        "Peer host name or IP address:",
                        "NE2000 UDP cable", "127.0.0.1")
                    If String.IsNullOrWhiteSpace(peerHostInBed) Then Return
                    Dim peerTextInBed As String = Microsoft.VisualBasic.Interaction.InputBox(
                        "Peer UDP port (use the other emulator's local port):",
                        "NE2000 UDP cable", "19861")
                    If String.IsNullOrWhiteSpace(peerTextInBed) Then Return

                    Dim localPortInBed As Integer
                    Dim peerPortInBed As Integer
                    If Not Integer.TryParse(localTextInBed, localPortInBed) OrElse localPortInBed < 1 OrElse localPortInBed > 65535 Then
                        Throw New ArgumentException("Local UDP port must be 1 through 65535.")
                    End If
                    If Not Integer.TryParse(peerTextInBed, peerPortInBed) OrElse peerPortInBed < 1 OrElse peerPortInBed > 65535 Then
                        Throw New ArgumentException("Peer UDP port must be 1 through 65535.")
                    End If
                    WithMachineInBed(Sub() Ne2000.ConfigureUdpTunnel(localPortInBed, peerHostInBed, peerPortInBed))
                Catch ex As Exception
                    MessageBox.Show(Me, ex.Message, "NE2000 cable", MessageBoxButtons.OK, MessageBoxIcon.Error)
                End Try
            End Sub)
        rootInBed.DropDownItems.Add("Disconnect NE2000 cable", Nothing,
            Sub() WithMachineInBed(Sub() Ne2000.DisconnectUdpTunnel()))
        rootInBed.DropDownItems.Add("Start NE2000 PCAP capture...", Nothing,
            Sub()
                Using dialogInBed As New SaveFileDialog()
                    dialogInBed.Title = "Capture NE2000 Ethernet frames"
                    dialogInBed.Filter = "Packet capture (*.pcap)|*.pcap|All files (*.*)|*.*"
                    dialogInBed.DefaultExt = "pcap"
                    dialogInBed.AddExtension = True
                    dialogInBed.FileName = "ne2000-capture.pcap"
                    If dialogInBed.ShowDialog(Me) <> DialogResult.OK Then Return
                    Try
                        WithMachineInBed(Sub() Ne2000.SetPcapCapture(dialogInBed.FileName))
                    Catch ex As Exception
                        MessageBox.Show(Me, ex.Message, "NE2000 capture", MessageBoxButtons.OK, MessageBoxIcon.Error)
                    End Try
                End Using
            End Sub)
        rootInBed.DropDownItems.Add("Stop NE2000 PCAP capture", Nothing,
            Sub() WithMachineInBed(Sub() Ne2000.SetPcapCapture(Nothing)))

        AddHandler rootInBed.DropDownOpening,
            Sub()
                sbStatusInBed.Text = "SB16: " & SoundBlaster16.BasePort.ToString("X3") & "h / IRQ" & SoundBlaster16.Irq.ToString() &
                                     " / DMA" & SoundBlaster16.Dma8Channel.ToString() & "/" & SoundBlaster16.Dma16Channel.ToString()
                neStatusInBed.Text = "NE2000: " & Ne2000.BasePort.ToString("X3") & "h / IRQ" & Ne2000.Irq.ToString() & " / " & Ne2000.MacAddressText
            End Sub

        Return rootInBed
    End Function

    Private Function BuildPrinterMenuInBed() As ToolStripMenuItem
        Dim rootInBed As New ToolStripMenuItem("Printer")
        Dim lpt1StatusInBed As New ToolStripMenuItem() With {.Enabled = False}
        Dim lpt2StatusInBed As New ToolStripMenuItem() With {.Enabled = False}

        Dim lpt1OnlineInBed As New ToolStripMenuItem("LPT1 printer online") With {
            .CheckOnClick = True,
            .Checked = Lpt1Printer.Online
        }
        Dim lpt2OnlineInBed As New ToolStripMenuItem("LPT2 printer online") With {
            .CheckOnClick = True,
            .Checked = Lpt2Printer.Online
        }
        Dim lpt1PaperInBed As New ToolStripMenuItem("LPT1 paper loaded") With {
            .CheckOnClick = True,
            .Checked = Lpt1Printer.PaperLoaded
        }
        Dim lpt2PaperInBed As New ToolStripMenuItem("LPT2 paper loaded") With {
            .CheckOnClick = True,
            .Checked = Lpt2Printer.PaperLoaded
        }

        AddHandler lpt1OnlineInBed.CheckedChanged,
            Sub() Lpt1Printer.Online = lpt1OnlineInBed.Checked
        AddHandler lpt2OnlineInBed.CheckedChanged,
            Sub() Lpt2Printer.Online = lpt2OnlineInBed.Checked
        AddHandler lpt1PaperInBed.CheckedChanged,
            Sub() Lpt1Printer.PaperLoaded = lpt1PaperInBed.Checked
        AddHandler lpt2PaperInBed.CheckedChanged,
            Sub() Lpt2Printer.PaperLoaded = lpt2PaperInBed.Checked

        rootInBed.DropDownItems.Add(lpt1StatusInBed)
        rootInBed.DropDownItems.Add(lpt2StatusInBed)
        rootInBed.DropDownItems.Add(New ToolStripSeparator())
        rootInBed.DropDownItems.Add(lpt1OnlineInBed)
        rootInBed.DropDownItems.Add(lpt1PaperInBed)
        rootInBed.DropDownItems.Add(lpt2OnlineInBed)
        rootInBed.DropDownItems.Add(lpt2PaperInBed)
        rootInBed.DropDownItems.Add(New ToolStripSeparator())

        rootInBed.DropDownItems.Add("Eject LPT1 page", Nothing, Sub() Lpt1Printer.EjectPage())
        rootInBed.DropDownItems.Add("Eject LPT2 page", Nothing, Sub() Lpt2Printer.EjectPage())
        rootInBed.DropDownItems.Add("Flush/save LPT1 job now", Nothing, Sub() FlushPrinterJobInBed(Lpt1Printer))
        rootInBed.DropDownItems.Add("Flush/save LPT2 job now", Nothing, Sub() FlushPrinterJobInBed(Lpt2Printer))
        rootInBed.DropDownItems.Add(New ToolStripSeparator())

        Dim outputModeInBed As New ToolStripMenuItem("Output format")
        Dim pdfAndPngInBed As New ToolStripMenuItem("PDF + PNG pages") With {.CheckOnClick = False}
        Dim pdfOnlyInBed As New ToolStripMenuItem("PDF only") With {.CheckOnClick = False}
        Dim pngOnlyInBed As New ToolStripMenuItem("PNG pages only") With {.CheckOnClick = False}
        outputModeInBed.DropDownItems.AddRange({pdfAndPngInBed, pdfOnlyInBed, pngOnlyInBed})

        Dim applyOutputModeInBed As Action(Of VirtualPrinterOutputMode) =
            Sub(modeInBed As VirtualPrinterOutputMode)
                Lpt1Printer.OutputMode = modeInBed
                Lpt2Printer.OutputMode = modeInBed
                pdfAndPngInBed.Checked = (modeInBed = VirtualPrinterOutputMode.PdfAndPng)
                pdfOnlyInBed.Checked = (modeInBed = VirtualPrinterOutputMode.PdfOnly)
                pngOnlyInBed.Checked = (modeInBed = VirtualPrinterOutputMode.PngOnly)
            End Sub
        AddHandler pdfAndPngInBed.Click, Sub() applyOutputModeInBed(VirtualPrinterOutputMode.PdfAndPng)
        AddHandler pdfOnlyInBed.Click, Sub() applyOutputModeInBed(VirtualPrinterOutputMode.PdfOnly)
        AddHandler pngOnlyInBed.Click, Sub() applyOutputModeInBed(VirtualPrinterOutputMode.PngOnly)
        applyOutputModeInBed(Lpt1Printer.OutputMode)
        rootInBed.DropDownItems.Add(outputModeInBed)

        rootInBed.DropDownItems.Add("Open printer output folder", Nothing, AddressOf OpenPrinterOutputFolderInBed)
        rootInBed.DropDownItems.Add("Open last LPT1 output", Nothing, Sub() OpenLastPrinterOutputInBed(Lpt1Printer))
        rootInBed.DropDownItems.Add("Open last LPT2 output", Nothing, Sub() OpenLastPrinterOutputInBed(Lpt2Printer))
        rootInBed.DropDownItems.Add(New ToolStripSeparator())
        rootInBed.DropDownItems.Add("Cancel LPT1 current job", Nothing, Sub() Lpt1Printer.CancelCurrentJob())
        rootInBed.DropDownItems.Add("Cancel LPT2 current job", Nothing, Sub() Lpt2Printer.CancelCurrentJob())
        rootInBed.DropDownItems.Add("Clear printer backend errors", Nothing,
                                    Sub()
                                        Lpt1Printer.ClearBackendError()
                                        Lpt2Printer.ClearBackendError()
                                    End Sub)

        AddHandler rootInBed.DropDownOpening,
            Sub()
                lpt1StatusInBed.Text = "LPT1: " & Lpt1Printer.DiagnosticText()
                lpt2StatusInBed.Text = "LPT2: " & Lpt2Printer.DiagnosticText()
                lpt1OnlineInBed.Checked = Lpt1Printer.Online
                lpt2OnlineInBed.Checked = Lpt2Printer.Online
                lpt1PaperInBed.Checked = Lpt1Printer.PaperLoaded
                lpt2PaperInBed.Checked = Lpt2Printer.PaperLoaded
            End Sub

        Return rootInBed
    End Function

    Private Sub FlushPrinterJobInBed(printerInBed As EpsonFxVirtualPrinter)
        Try
            Dim outputInBed As String = printerInBed.FlushJob()
            If String.IsNullOrWhiteSpace(outputInBed) Then
                MessageBox.Show(Me, "There is no marked paper waiting in " & printerInBed.LogicalName & ".",
                                "Virtual printer", MessageBoxButtons.OK, MessageBoxIcon.Information)
                Return
            End If
            MessageBox.Show(Me, outputInBed, printerInBed.LogicalName & " print job saved",
                            MessageBoxButtons.OK, MessageBoxIcon.Information)
        Catch ex As Exception
            MessageBox.Show(Me, ex.Message, printerInBed.LogicalName & " print error",
                            MessageBoxButtons.OK, MessageBoxIcon.Error)
        End Try
    End Sub

    Private Sub OpenPrinterOutputFolderInBed(sender As Object, e As EventArgs)
        Dim pathInBed As String = Path.Combine(AppContext.BaseDirectory, "Printouts")
        Directory.CreateDirectory(pathInBed)
        OpenHostPathInBed(pathInBed)
    End Sub

    Private Sub OpenLastPrinterOutputInBed(printerInBed As EpsonFxVirtualPrinter)
        Dim pathInBed As String = printerInBed.LastOutputPath
        If String.IsNullOrWhiteSpace(pathInBed) OrElse (Not File.Exists(pathInBed) AndAlso Not Directory.Exists(pathInBed)) Then
            MessageBox.Show(Me, "No completed output is available for " & printerInBed.LogicalName & ".",
                            "Virtual printer", MessageBoxButtons.OK, MessageBoxIcon.Information)
            Return
        End If
        OpenHostPathInBed(pathInBed)
    End Sub

    Private Shared Sub OpenHostPathInBed(pathInBed As String)
        Dim startInfoInBed As New System.Diagnostics.ProcessStartInfo(pathInBed) With {
            .UseShellExecute = True
        }
        System.Diagnostics.Process.Start(startInfoInBed)
    End Sub

    Private Sub InitializeSystemConfigurationInBed()
        If _systemConfigurationDrawer IsNot Nothing Then Return

        _systemConfigurationDrawer = New SystemConfigurationDrawer(Me)
        AddHandler _systemConfigurationDrawer.DrawerMotionChanged,
            AddressOf SystemConfigurationDrawerMotionChangedInBed
        _systemConfigurationDrawer.Show(Me)
        _systemConfigurationDrawer.SyncToOwnerInBed()
    End Sub

    Private Sub OpenSystemConfigurationInBed(pageKeyInBed As String)
        If _systemConfigurationDrawer Is Nothing Then InitializeSystemConfigurationInBed()
        _systemConfigurationDrawer.OpenPage(pageKeyInBed)
    End Sub

    Friend Function GetSystemConfigurationSnapshotInBed() As SystemConfigurationSnapshot
        Return ReadMachineInBed(AddressOf GetSystemConfigurationSnapshotUnlockedInBed)
    End Function

    Private Function GetSystemConfigurationSnapshotUnlockedInBed() As SystemConfigurationSnapshot
        Dim snapshotInBed As New SystemConfigurationSnapshot()
        snapshotInBed.Chassis = "AT chassis profile not yet modeled"
        snapshotInBed.PowerSupply = "Power-supply profile not yet modeled"
        snapshotInBed.Motherboard = "C&T CS8221 NEAT-class AT motherboard"
        snapshotInBed.Chipset = "Chips & Technologies CS8221 NEAT chipset (" & Chipset.GetType().Name & ")"
        snapshotInBed.ChipsetCpuBus = "82C211 CPU / AT-bus controller"
        snapshotInBed.ChipsetMemory = "82C212 memory / shadow / EMS controller"
        snapshotInBed.ChipsetPeripheral = "82C206 integrated peripheral controller — DMA, PIC, PIT, RTC/CMOS support"
        snapshotInBed.ChipsetBuffer = "82C215 data / address buffer role — represented by motherboard decode and routing"
        snapshotInBed.ChipsetConfigurationPorts = "NEAT indexed configuration interface — 22h index / 23h data"
        snapshotInBed.ChipsetTiming = Chipset.TimingDiagnosticText()
        snapshotInBed.Bios = "Cromwell AT BIOS — Firmware\atbios.rom"
        snapshotInBed.Cpu = MachineProfile286.ProcessorVendor & " " & MachineProfile286.ProcessorModel
        snapshotInBed.CpuClock = (MachineClock.CpuClockHz \ 1000000L).ToString() & " MHz" & If(MachineClock.TurboEnabled, " — Turbo", " — Normal")
        snapshotInBed.HostExecutionRatePercent = MachineClock.HostExecutionRatePercent
        snapshotInBed.HostExecutionRate = If(MachineClock.HostExecutionRatePercent = 0,
                                                "Unlimited — host limited",
                                                (CDbl(MachineClock.HostExecutionRatePercent) / 100.0R).ToString("0.##") & "× real time")
        snapshotInBed.NumericCoprocessor = "Intel 80287"
        snapshotInBed.InstalledMemoryMb = If(_ramBanks Is Nothing, 0, _ramBanks.InstalledMemoryMb)
        snapshotInBed.Memory = snapshotInBed.InstalledMemoryMb.ToString() & " MB"
        snapshotInBed.PendingMemoryMb = _pendingMemoryMb
        snapshotInBed.Video = "Diamond Stealth Pro ISA — S3 86C928 — 2 MiB VRAM"
        snapshotInBed.VideoRom = "Diamond Stealth Pro option ROM — Firmware\stealthpro.rom"
        snapshotInBed.ExpansionAudio = "Creative Sound Blaster 16 — " & SoundBlaster16.BasePort.ToString("X3") & "h — IRQ" & SoundBlaster16.Irq.ToString() &
                                       " — DMA" & SoundBlaster16.Dma8Channel.ToString() & "/DMA" & SoundBlaster16.Dma16Channel.ToString() & " — OPL3"
        snapshotInBed.Speaker = "Motherboard PC speaker — PIT channel 2 / port 61h"
        snapshotInBed.Network = "Novell NE2000-compatible — " & Ne2000.BasePort.ToString("X3") & "h — IRQ" & Ne2000.Irq.ToString() & " — " & Ne2000.MacAddressText
        snapshotInBed.FloppyController = "NEC uPD765A / Intel 8272-compatible FDC"
        snapshotInBed.FloppyDriveA = "AT floppy drive unit A — swappable drive profile"
        snapshotInBed.FloppyDriveB = "AT floppy drive unit B — swappable drive profile"
        snapshotInBed.FloppyMediaA = FloppyController.GetAttachmentStatus(0)
        snapshotInBed.FloppyMediaB = FloppyController.GetAttachmentStatus(1)
        snapshotInBed.FloppyMediaSourceIdA = If(String.IsNullOrWhiteSpace(FloppyController.GetMediaSourceId(0)), "empty", FloppyController.GetMediaSourceId(0))
        snapshotInBed.FloppyMediaSourceIdB = If(String.IsNullOrWhiteSpace(FloppyController.GetMediaSourceId(1)), "empty", FloppyController.GetMediaSourceId(1))
        snapshotInBed.HardDiskController = "Primary ATA/IDE controller — 1F0h-1F7h, 3F6h"
        If _ideDriveShelf Is Nothing OrElse _ideDriveShelf.PrimaryMasterId < 0 Then
            snapshotInBed.HardDisk0 = "Primary master disconnected"
        Else
            Try
                Dim entryInBed As IdeDriveShelfEntry = _ideDriveShelf.FindById(_ideDriveShelf.PrimaryMasterId)
                If entryInBed Is Nothing Then
                    snapshotInBed.HardDisk0 = "Primary master: shelf #" & _ideDriveShelf.PrimaryMasterId.ToString() & " unavailable"
                Else
                    snapshotInBed.HardDisk0 = "Primary master: shelf #" & entryInBed.Id.ToString() & " — " & entryInBed.Label & If(_mountedIdeDriveId = entryInBed.Id, " — attached", " — configured, unavailable")
                End If
            Catch ex As Exception
                snapshotInBed.HardDisk0 = "Primary master status unavailable — " & ex.Message
            End Try
        End If
        snapshotInBed.Optical = If(Declares.IdeController.CdRomMounted,
                                   "IDE/ATAPI CD-ROM — ISO media inserted",
                                   "IDE/ATAPI CD-ROM — no media inserted")
        snapshotInBed.Keyboard = "IBM AT 101-key keyboard + 8042 controller"
        snapshotInBed.Com1 = "NS16550A-compatible UART — 3F8h — IRQ4 — Microsoft serial mouse attached"
        snapshotInBed.Com2 = "NS16550A-compatible UART — 2F8h — IRQ3 — DB-9 connector"
        snapshotInBed.Lpt1 = "IBM-compatible SPP / Centronics — 378h — IRQ7"
        snapshotInBed.Lpt2 = "IBM-compatible SPP / Centronics — 278h — IRQ5"
        snapshotInBed.GamePort = If(SoundBlaster16.GamePortEnabled,
                                        "Sound Blaster 16 game port — 201h — no joystick attached",
                                        "Sound Blaster 16 game port — disabled by card jumper")
        snapshotInBed.Midi = "MPU-401 UART-compatible — " & SoundBlaster16.MpuBasePort.ToString("X3") & "h (Sound Blaster 16)"
        snapshotInBed.PendingHardwareSummary = GetPendingHardwareSummaryInBed()
        snapshotInBed.ResourceSummary =
            "NEAT config: 22h/23h" & Environment.NewLine &
            "DMA: 00h-0Fh, 80h-8Fh, C0h-DEh" & Environment.NewLine &
            "PIC: 20h/21h + A0h/A1h" & Environment.NewLine &
            "PIT / speaker gate: 40h-43h + 61h" & Environment.NewLine &
            "RTC / CMOS: 70h/71h, IRQ8" & Environment.NewLine &
            "Keyboard: 60h/64h, IRQ1" & Environment.NewLine &
            "Floppy: 3F2h-3F7h, IRQ6, DMA2" & Environment.NewLine &
            "Primary IDE: 1F0h-1F7h + 3F6h, IRQ14" & Environment.NewLine &
            "VGA/S3: legacy VGA I/O + A0000h-BFFFFh, option ROM C0000h-C7FFFh" & Environment.NewLine &
            "COM1: 3F8h-3FFh, IRQ4; COM2: 2F8h-2FFh, IRQ3" & Environment.NewLine &
            "LPT1: 378h-37Ah, IRQ7; LPT2: 278h-27Ah, IRQ5" & Environment.NewLine &
            "Sound Blaster 16: " & SoundBlaster16.BasePort.ToString("X3") & "h-" & (CInt(SoundBlaster16.BasePort) + &HF).ToString("X3") & "h + 388h-38Bh, IRQ" & SoundBlaster16.Irq.ToString() & ", DMA" & SoundBlaster16.Dma8Channel.ToString() & "/DMA" & SoundBlaster16.Dma16Channel.ToString() & Environment.NewLine &
            "MPU-401 UART: " & SoundBlaster16.MpuBasePort.ToString("X3") & "h-" & (CInt(SoundBlaster16.MpuBasePort) + 1).ToString("X3") & "h (SB16); game port: " & If(SoundBlaster16.GamePortEnabled, "201h", "disabled") & Environment.NewLine &
            "NE2000: " & Ne2000.BasePort.ToString("X3") & "h-" & (CInt(Ne2000.BasePort) + &H1F).ToString("X3") & "h, IRQ" & Ne2000.Irq.ToString() & ", 16 KiB packet RAM"
        Return snapshotInBed
    End Function

    Friend Sub SetHostExecutionRateInBed(ratePercentInBed As Integer)
        WithMachineInBed(Sub() MachineClock.HostExecutionRatePercent = ratePercentInBed)
        My.Settings.HostExecutionRatePercent = ratePercentInBed
        My.Settings.Save()
    End Sub

    Private Sub RestoreHostExecutionRateInBed()
        Dim savedRateInBed As Integer = My.Settings.HostExecutionRatePercent
        Select Case savedRateInBed
            Case 0, 25, 50, 100, 200, 400, 800, 1600
                ' Current discrete host-rate selections.
            Case Else
                savedRateInBed = 100
                My.Settings.HostExecutionRatePercent = savedRateInBed
                My.Settings.Save()
        End Select
        MachineClock.HostExecutionRatePercent = savedRateInBed
    End Sub

    Friend Function GetFloppyConfigurationSourceChoicesInBed(driveInBed As Integer) As List(Of HardwareProfileChoice)
        Return ReadMachineInBed(Function() GetFloppyConfigurationSourceChoicesUnlockedInBed(driveInBed))
    End Function

    Private Function GetFloppyConfigurationSourceChoicesUnlockedInBed(driveInBed As Integer) As List(Of HardwareProfileChoice)
        If driveInBed < 0 OrElse driveInBed > 3 Then Throw New ArgumentOutOfRangeException(NameOf(driveInBed))

        Dim resultInBed As New List(Of HardwareProfileChoice)()
        Dim seenInBed As New HashSet(Of String)(StringComparer.OrdinalIgnoreCase)

        Dim addChoiceInBed As Action(Of String, String) =
            Sub(idInBed As String, displayInBed As String)
                If String.IsNullOrWhiteSpace(idInBed) OrElse seenInBed.Contains(idInBed) Then Return
                seenInBed.Add(idInBed)
                resultInBed.Add(New HardwareProfileChoice(idInBed, displayInBed))
            End Sub

        addChoiceInBed("empty", "(no media source attached)")

        If _floppyBox Is Nothing Then InitializeFloppyBox()
        Try
            For Each imagePathInBed As String In _floppyBox.GetImages()
                Dim fullPathInBed As String = Path.GetFullPath(imagePathInBed)
                addChoiceInBed("image|" & fullPathInBed,
                               "Disk Box image — " & Path.GetFileName(fullPathInBed))
            Next
        Catch
            ' Keep physical-drive configuration usable even if Disk Box scanning
            ' encounters a host filesystem problem.
        End Try

        Try
            For Each rootInBed As String In PhysicalFloppyMediaSource.EnumerateHostFloppyRoots()
                addChoiceInBed(PhysicalFloppyMediaSource.SourceIdForHostRoot(rootInBed),
                               "Physical drive — " & PhysicalFloppyMediaSource.DescribeHostDrive(rootInBed))
            Next
        Catch
            ' Host drive enumeration is advisory UI discovery; an already attached
            ' physical source remains represented below even if enumeration fails.
        End Try

        Dim currentIdInBed As String = FloppyController.GetMediaSourceId(driveInBed)
        If Not String.IsNullOrWhiteSpace(currentIdInBed) AndAlso Not seenInBed.Contains(currentIdInBed) Then
            addChoiceInBed(currentIdInBed,
                           "Current attachment — " & FloppyController.GetAttachmentStatus(driveInBed))
        End If

        Return resultInBed
    End Function

    Friend Sub SelectFloppyConfigurationSourceInBed(driveInBed As Integer,
                                                     sourceIdInBed As String)
        If driveInBed < 0 OrElse driveInBed > 3 Then Throw New ArgumentOutOfRangeException(NameOf(driveInBed))
        If String.IsNullOrWhiteSpace(sourceIdInBed) Then Return

        Dim currentIdInBed As String = ReadMachineInBed(Function() FloppyController.GetMediaSourceId(driveInBed))
        If String.Equals(currentIdInBed, sourceIdInBed, StringComparison.OrdinalIgnoreCase) Then Return
        If sourceIdInBed.Equals("empty", StringComparison.OrdinalIgnoreCase) AndAlso
           String.IsNullOrWhiteSpace(currentIdInBed) Then Return

        Try
            If sourceIdInBed.Equals("empty", StringComparison.OrdinalIgnoreCase) Then
                WithMachineInBed(Sub() FloppyController.Eject(driveInBed))
            ElseIf sourceIdInBed.StartsWith("physical|", StringComparison.OrdinalIgnoreCase) Then
                Dim rootInBed As String = sourceIdInBed.Substring("physical|".Length)
                If rootInBed.EndsWith(":", StringComparison.Ordinal) Then rootInBed &= "\"
                WithMachineInBed(Sub() MountPhysicalFloppyDrive(driveInBed, rootInBed))
            ElseIf sourceIdInBed.StartsWith("image|", StringComparison.OrdinalIgnoreCase) Then
                Dim imagePathInBed As String = sourceIdInBed.Substring("image|".Length)
                WithMachineInBed(Sub() MountFloppyImage(driveInBed, imagePathInBed))
            Else
                Throw New InvalidOperationException("Unknown floppy media-source profile: " & sourceIdInBed)
            End If
            UpdateMediaStatus()
        Catch ex As Exception
            MessageBox.Show(Me,
                            ex.Message,
                            "Unable to change floppy source",
                            MessageBoxButtons.OK,
                            MessageBoxIcon.Error)
        End Try

        If _systemConfigurationDrawer IsNot Nothing Then _systemConfigurationDrawer.RefreshFromMachine()
    End Sub

    Friend Sub BrowseFloppyImageFromConfigurationInBed(driveInBed As Integer)
        ChooseFloppy(driveInBed)
        If _systemConfigurationDrawer IsNot Nothing Then _systemConfigurationDrawer.RefreshFromMachine()
    End Sub

    Friend Sub EjectFloppyFromConfigurationInBed(driveInBed As Integer)
        EjectFloppy(driveInBed)
        If _systemConfigurationDrawer IsNot Nothing Then _systemConfigurationDrawer.RefreshFromMachine()
    End Sub

    Friend Sub StageMemoryConfigurationInBed(megabytesInBed As Integer)
        If _ramBanks Is Nothing OrElse Not RamBankConfiguration.IsSupported(megabytesInBed) Then Return
        If megabytesInBed = _ramBanks.InstalledMemoryMb Then
            _pendingMemoryMb = Nothing
        Else
            _pendingMemoryMb = megabytesInBed
        End If
        If _systemConfigurationDrawer IsNot Nothing Then _systemConfigurationDrawer.RefreshFromMachine()
    End Sub

    Friend Sub RevertPendingSystemConfigurationInBed()
        _pendingMemoryMb = Nothing
        _pendingSoundBlaster16Jumpers = Nothing
        _pendingNe2000Jumpers = Nothing
        If _systemConfigurationDrawer IsNot Nothing Then _systemConfigurationDrawer.RefreshFromMachine()
    End Sub

    Friend Sub OpenSoundBlasterJumperPanelInBed()
        If _isaCardConfiguration Is Nothing Then Return
        Dim sbInBed As SoundBlaster16JumperSettings = EffectiveSoundBlaster16JumpersInBed()
        Dim neInBed As Ne2000JumperSettings = EffectiveNe2000JumpersInBed()
        Using panelInBed As New IsaCardJumperDialog(IsaJumperCardKind.SoundBlaster16, sbInBed, neInBed)
            If panelInBed.ShowDialog(Me) = DialogResult.OK Then
                StageSoundBlaster16JumpersInBed(panelInBed.SoundBlasterSettings)
            End If
        End Using
    End Sub

    Friend Sub OpenNe2000JumperPanelInBed()
        If _isaCardConfiguration Is Nothing Then Return
        Dim sbInBed As SoundBlaster16JumperSettings = EffectiveSoundBlaster16JumpersInBed()
        Dim neInBed As Ne2000JumperSettings = EffectiveNe2000JumpersInBed()
        Using panelInBed As New IsaCardJumperDialog(IsaJumperCardKind.Ne2000, sbInBed, neInBed)
            If panelInBed.ShowDialog(Me) = DialogResult.OK Then
                StageNe2000JumpersInBed(panelInBed.Ne2000Settings)
            End If
        End Using
    End Sub

    Private Sub StageSoundBlaster16JumpersInBed(settingsInBed As SoundBlaster16JumperSettings)
        If settingsInBed Is Nothing OrElse _isaCardConfiguration Is Nothing Then Return
        Dim conflictsInBed As List(Of String) = IsaResourceConflictDetector.Validate(settingsInBed, EffectiveNe2000JumpersInBed())
        If conflictsInBed.Count > 0 Then
            MessageBox.Show(Me, String.Join(Environment.NewLine, conflictsInBed), "ISA resource conflict", MessageBoxButtons.OK, MessageBoxIcon.Warning)
            Return
        End If
        If SameSoundBlaster16JumpersInBed(settingsInBed, _isaCardConfiguration.SoundBlaster16) Then
            _pendingSoundBlaster16Jumpers = Nothing
        Else
            _pendingSoundBlaster16Jumpers = settingsInBed.CloneSettings()
        End If
        If _systemConfigurationDrawer IsNot Nothing Then _systemConfigurationDrawer.RefreshFromMachine()
    End Sub

    Private Sub StageNe2000JumpersInBed(settingsInBed As Ne2000JumperSettings)
        If settingsInBed Is Nothing OrElse _isaCardConfiguration Is Nothing Then Return
        Dim conflictsInBed As List(Of String) = IsaResourceConflictDetector.Validate(EffectiveSoundBlaster16JumpersInBed(), settingsInBed)
        If conflictsInBed.Count > 0 Then
            MessageBox.Show(Me, String.Join(Environment.NewLine, conflictsInBed), "ISA resource conflict", MessageBoxButtons.OK, MessageBoxIcon.Warning)
            Return
        End If
        If SameNe2000JumpersInBed(settingsInBed, _isaCardConfiguration.Ne2000) Then
            _pendingNe2000Jumpers = Nothing
        Else
            _pendingNe2000Jumpers = settingsInBed.CloneSettings()
        End If
        If _systemConfigurationDrawer IsNot Nothing Then _systemConfigurationDrawer.RefreshFromMachine()
    End Sub

    Private Function EffectiveSoundBlaster16JumpersInBed() As SoundBlaster16JumperSettings
        If _pendingSoundBlaster16Jumpers IsNot Nothing Then Return _pendingSoundBlaster16Jumpers.CloneSettings()
        If _isaCardConfiguration Is Nothing OrElse _isaCardConfiguration.SoundBlaster16 Is Nothing Then Return New SoundBlaster16JumperSettings()
        Return _isaCardConfiguration.SoundBlaster16.CloneSettings()
    End Function

    Private Function EffectiveNe2000JumpersInBed() As Ne2000JumperSettings
        If _pendingNe2000Jumpers IsNot Nothing Then Return _pendingNe2000Jumpers.CloneSettings()
        If _isaCardConfiguration Is Nothing OrElse _isaCardConfiguration.Ne2000 Is Nothing Then Return New Ne2000JumperSettings()
        Return _isaCardConfiguration.Ne2000.CloneSettings()
    End Function

    Private Shared Function SameSoundBlaster16JumpersInBed(aInBed As SoundBlaster16JumperSettings, bInBed As SoundBlaster16JumperSettings) As Boolean
        If aInBed Is Nothing OrElse bInBed Is Nothing Then Return False
        Return aInBed.BasePort = bInBed.BasePort AndAlso aInBed.Irq = bInBed.Irq AndAlso
               aInBed.Dma8 = bInBed.Dma8 AndAlso aInBed.Dma16 = bInBed.Dma16 AndAlso
               aInBed.MpuPort = bInBed.MpuPort AndAlso aInBed.GamePortEnabled = bInBed.GamePortEnabled
    End Function

    Private Shared Function SameNe2000JumpersInBed(aInBed As Ne2000JumperSettings, bInBed As Ne2000JumperSettings) As Boolean
        If aInBed Is Nothing OrElse bInBed Is Nothing Then Return False
        Return aInBed.BasePort = bInBed.BasePort AndAlso aInBed.Irq = bInBed.Irq
    End Function

    Private Function GetPendingHardwareSummaryInBed() As String
        Dim pendingInBed As New List(Of String)()
        If _pendingMemoryMb.HasValue Then pendingInBed.Add("memory " & _pendingMemoryMb.Value.ToString() & " MB")
        If _pendingSoundBlaster16Jumpers IsNot Nothing Then pendingInBed.Add("SB16 " & _pendingSoundBlaster16Jumpers.Summary())
        If _pendingNe2000Jumpers IsNot Nothing Then pendingInBed.Add("NE2000 " & _pendingNe2000Jumpers.Summary())
        Return String.Join("; ", pendingInBed)
    End Function

    Friend Sub ApplyPendingSystemConfigurationAndPowerCycleInBed()
        Dim cardChangesInBed As Boolean = _pendingSoundBlaster16Jumpers IsNot Nothing OrElse _pendingNe2000Jumpers IsNot Nothing
        If Not _pendingMemoryMb.HasValue AndAlso Not cardChangesInBed Then Return

        Dim wasPoweredInBed As Boolean = _machinePoweredInBed
        If wasPoweredInBed Then SetMachinePowerInBed(False)

        If _pendingMemoryMb.HasValue Then
            Dim memoryMbInBed As Integer = _pendingMemoryMb.Value
            _pendingMemoryMb = Nothing
            If _ramBanks IsNot Nothing AndAlso RamBankConfiguration.IsSupported(memoryMbInBed) Then
                _ramBanks.InstalledMemoryMb = memoryMbInBed
                _ramBanks.SaveConfiguration()
            End If
        End If

        If _isaCardConfiguration IsNot Nothing Then
            If _pendingSoundBlaster16Jumpers IsNot Nothing Then
                _isaCardConfiguration.SoundBlaster16 = _pendingSoundBlaster16Jumpers.CloneSettings()
                _pendingSoundBlaster16Jumpers = Nothing
            End If
            If _pendingNe2000Jumpers IsNot Nothing Then
                _isaCardConfiguration.Ne2000 = _pendingNe2000Jumpers.CloneSettings()
                _pendingNe2000Jumpers = Nothing
            End If
            If cardChangesInBed Then
                _isaCardConfiguration.SaveConfiguration()
                ApplyIsaExpansionCardConfigurationToDevicesInBed()
            End If
        End If

        If wasPoweredInBed Then SetMachinePowerInBed(True)

        RebuildRamBanksMenu()
        If _systemConfigurationDrawer IsNot Nothing Then _systemConfigurationDrawer.RefreshFromMachine()
    End Sub

    Private Sub InitializeIsaExpansionCardConfigurationInBed()
        _isaCardConfiguration = New IsaExpansionCardConfiguration(AppContext.BaseDirectory)
        _isaCardConfiguration.LoadConfiguration()
        Dim conflictsInBed As List(Of String) = IsaResourceConflictDetector.Validate(_isaCardConfiguration.SoundBlaster16, _isaCardConfiguration.Ne2000)
        If conflictsInBed.Count > 0 Then
            ' An externally edited INI may describe an impossible build.  Refuse
            ' to wire a conflicted ISA backplane at startup and recover to the
            ' known-clean factory strap set instead.
            _isaCardConfiguration.SoundBlaster16 = New SoundBlaster16JumperSettings()
            _isaCardConfiguration.Ne2000 = New Ne2000JumperSettings()
        End If
        _isaCardConfiguration.SaveConfiguration()
        ApplyIsaExpansionCardConfigurationToDevicesInBed()
    End Sub

    Private Sub ApplyIsaExpansionCardConfigurationToDevicesInBed()
        If _isaCardConfiguration Is Nothing Then Return
        Dim sbInBed As SoundBlaster16JumperSettings = _isaCardConfiguration.SoundBlaster16
        Dim neInBed As Ne2000JumperSettings = _isaCardConfiguration.Ne2000
        SoundBlaster16.ConfigureHardware(sbInBed.BasePort, sbInBed.Irq, sbInBed.Dma8, sbInBed.Dma16, sbInBed.MpuPort, sbInBed.GamePortEnabled)
        Ne2000.ConfigureHardware(neInBed.BasePort, neInBed.Irq)
    End Sub

    Private Sub InitializeRamBanks()
        _ramBanks = New RamBankConfiguration(AppContext.BaseDirectory)
        _ramBanks.LoadConfiguration()
        ' Persist the default too, so the chassis configuration is explicit.
        _ramBanks.SaveConfiguration()
        CPU0.ConfigureInstalledMemoryMegabytes(_ramBanks.InstalledMemoryMb, clearRam:=True)
    End Sub

    Private Function BuildRamBanksMenu() As ToolStripMenuItem
        Dim menu As New ToolStripMenuItem("RAM Banks")
        PopulateRamBanksMenu(menu)
        Return menu
    End Function

    Private Sub PopulateRamBanksMenu(menu As ToolStripMenuItem)
        menu.DropDownItems.Clear()
        If _ramBanks Is Nothing Then
            menu.Text = "RAM Banks"
            menu.DropDownItems.Add(New ToolStripMenuItem("Configuration unavailable") With {.Enabled = False})
            Return
        End If

        menu.Text = "RAM Banks (" & _ramBanks.InstalledMemoryMb.ToString() & " MB installed)"
        For Each memoryMb As Integer In RamBankConfiguration.SupportedMemoryMegabytes
            Dim selectedMb As Integer = memoryMb
            Dim item As New ToolStripMenuItem(memoryMb.ToString() & " MB") With {
                .Checked = (memoryMb = _ramBanks.InstalledMemoryMb),
                .CheckOnClick = False
            }
            AddHandler item.Click, Sub() SelectRamBankConfiguration(selectedMb)
            menu.DropDownItems.Add(item)
        Next
    End Sub

    Private Sub RebuildRamBanksMenu()
        If ramBanksMenu IsNot Nothing Then PopulateRamBanksMenu(ramBanksMenu)
    End Sub

    Private Sub SelectRamBankConfiguration(megabytes As Integer)
        If _ramBanks Is Nothing OrElse Not RamBankConfiguration.IsSupported(megabytes) Then Return
        If _ramBanks.InstalledMemoryMb = megabytes Then Return

        ' Menu and drawer are intentionally duplicate pathways into the same
        ' physical configuration operation.  RAM replacement requires power off.
        StageMemoryConfigurationInBed(megabytes)
        ApplyPendingSystemConfigurationAndPowerCycleInBed()
    End Sub
    Private Sub InitializeIdeDriveShelf()
        _ideDriveShelf = New IdeDriveShelf(AppContext.BaseDirectory)
        _ideDriveShelf.EnsureShelfExists()
        _ideDriveShelf.LoadConfiguration()
        AttachConfiguredPrimaryMaster(silent:=True)
    End Sub

    Private Sub AttachConfiguredPrimaryMaster(silent As Boolean)
        _mountedIdeDriveId = -1
        WithMachineInBed(Sub() Declares.IdeController.EjectHardDisk())
        If _ideDriveShelf Is Nothing OrElse _ideDriveShelf.PrimaryMasterId < 0 Then Return

        Try
            Dim entry As IdeDriveShelfEntry = _ideDriveShelf.FindById(_ideDriveShelf.PrimaryMasterId)
            If entry Is Nothing Then Return
            WithMachineInBed(Sub() MountHardDiskImage(entry.FullPath))
            _mountedIdeDriveId = entry.Id
        Catch ex As Exception
            WithMachineInBed(Sub() Declares.IdeController.EjectHardDisk())
            _mountedIdeDriveId = -1
            If Not silent Then
                MessageBox.Show(Me,
                    "Shelf drive #" & _ideDriveShelf.PrimaryMasterId.ToString() & " could not be attached:" & Environment.NewLine & ex.Message,
                    "IDE primary master",
                    MessageBoxButtons.OK,
                    MessageBoxIcon.Error)
            End If
        End Try
    End Sub

    Private Sub RebuildIdeDriveShelfMenu()
        If ideDriveShelfMenu Is Nothing Then Return
        ideDriveShelfMenu.DropDownItems.Clear()

        If _ideDriveShelf Is Nothing Then
            Dim unavailable As New ToolStripMenuItem("IDE shelf is not initialized") With {.Enabled = False}
            ideDriveShelfMenu.DropDownItems.Add(unavailable)
            Return
        End If

        Try
            Dim entries As List(Of IdeDriveShelfEntry) = _ideDriveShelf.GetEntries()
            Dim configured As Integer = _ideDriveShelf.PrimaryMasterId
            Dim configuredText As String = If(configured < 0,
                "Primary Master: disconnected",
                "Primary Master: shelf #" & configured.ToString() & If(_mountedIdeDriveId = configured, " (attached)", " (not attached)"))
            ideDriveShelfMenu.DropDownItems.Add(New ToolStripMenuItem(configuredText) With {.Enabled = False})
            ideDriveShelfMenu.DropDownItems.Add(New ToolStripSeparator())

            If entries.Count = 0 Then
                ideDriveShelfMenu.DropDownItems.Add(New ToolStripMenuItem("No numbered .hdd/.img files found") With {.Enabled = False})
            Else
                For Each entry As IdeDriveShelfEntry In entries
                    Dim driveId As Integer = entry.Id
                    Dim item As New ToolStripMenuItem(entry.Id.ToString() & " - " & entry.Label) With {
                        .Checked = (entry.Id = _mountedIdeDriveId)
                    }
                    item.ToolTipText = entry.FullPath
                    AddHandler item.Click, Sub() SelectIdeShelfDrive(driveId)
                    ideDriveShelfMenu.DropDownItems.Add(item)
                Next
            End If

            ideDriveShelfMenu.DropDownItems.Add(New ToolStripSeparator())
            ideDriveShelfMenu.DropDownItems.Add("Rescan IDE-Drives", Nothing, Sub() RebuildIdeDriveShelfMenu())
            ideDriveShelfMenu.DropDownItems.Add("Open IDE-Drives folder", Nothing, Sub() OpenIdeDriveShelfFolder())
            ideDriveShelfMenu.DropDownItems.Add("Disconnect Primary Master", Nothing, Sub() EjectHardDisk())
        Catch ex As Exception
            ideDriveShelfMenu.DropDownItems.Clear()
            ideDriveShelfMenu.DropDownItems.Add(New ToolStripMenuItem("Shelf error: " & ex.Message) With {.Enabled = False})
            ideDriveShelfMenu.DropDownItems.Add(New ToolStripSeparator())
            ideDriveShelfMenu.DropDownItems.Add("Open IDE-Drives folder", Nothing, Sub() OpenIdeDriveShelfFolder())
        End Try
    End Sub

    Private Sub SelectIdeShelfDrive(driveId As Integer)
        If _ideDriveShelf Is Nothing Then Return
        _ideDriveShelf.PrimaryMasterId = driveId
        _ideDriveShelf.SaveConfiguration()
        AttachConfiguredPrimaryMaster(silent:=False)
        UpdateMediaStatus()
        RebuildIdeDriveShelfMenu()

        If _mountedIdeDriveId = driveId Then
            ' Treat changing the chassis attachment as a powered-reset event from
            ' the guest's perspective.  The BIOS must rediscover the drive via ATA.
            ResetThroughFirmware()
        End If
    End Sub

    Private Sub OpenIdeDriveShelfFolder()
        If _ideDriveShelf Is Nothing Then Return
        _ideDriveShelf.EnsureShelfExists()
        Try
            System.Diagnostics.Process.Start(New System.Diagnostics.ProcessStartInfo(_ideDriveShelf.RootPath) With {.UseShellExecute = True})
        Catch ex As Exception
            MessageBox.Show(Me, ex.Message, "Unable to open IDE-Drives", MessageBoxButtons.OK, MessageBoxIcon.Error)
        End Try
    End Sub
    Private Sub UpdateMediaStatus()
        Dim floppyAPresentInBed As Boolean
        Dim floppyBPresentInBed As Boolean
        Dim floppyATextInBed As String = String.Empty
        Dim floppyBTextInBed As String = String.Empty
        Dim floppyAGeometryInBed As Integer() = Nothing
        Dim floppyBGeometryInBed As Integer() = Nothing
        Dim hardDiskPresentInBed As Boolean
        Dim cdPresentInBed As Boolean

        WithMachineInBed(
            Sub()
                floppyAPresentInBed = FloppyController.IsMediaPresent(0)
                floppyBPresentInBed = FloppyController.IsMediaPresent(1)
                floppyATextInBed = FloppyController.GetAttachmentStatus(0)
                floppyBTextInBed = FloppyController.GetAttachmentStatus(1)
                floppyAGeometryInBed = FloppyController.GetGeometry(0)
                floppyBGeometryInBed = FloppyController.GetGeometry(1)
                hardDiskPresentInBed = Declares.IdeController.HardDiskSectorCount > 0
                cdPresentInBed = Declares.IdeController.CdRomMounted
            End Sub)

        If floppyAStatus IsNot Nothing Then
            floppyAStatus.Checked = floppyAPresentInBed
            floppyAStatus.Text = floppyATextInBed
        End If
        If floppyBStatus IsNot Nothing Then
            floppyBStatus.Checked = floppyBPresentInBed
            floppyBStatus.Text = floppyBTextInBed
        End If
        If hardDiskStatus IsNot Nothing Then
            hardDiskStatus.Checked = hardDiskPresentInBed
            If _ideDriveShelf IsNot Nothing Then
                If _ideDriveShelf.PrimaryMasterId < 0 Then
                    hardDiskStatus.Text = "Primary master disconnected"
                ElseIf _mountedIdeDriveId = _ideDriveShelf.PrimaryMasterId Then
                    hardDiskStatus.Text = "Primary master: shelf #" & _mountedIdeDriveId.ToString()
                Else
                    hardDiskStatus.Text = "Primary master: shelf #" & _ideDriveShelf.PrimaryMasterId.ToString() & " unavailable"
                End If
            End If
        End If
        If cdRomStatus IsNot Nothing Then cdRomStatus.Checked = cdPresentInBed
        UpdateDriveBayFacesInBed(floppyAGeometryInBed, floppyBGeometryInBed)
    End Sub

    Private Sub EjectFloppy(drive As Integer)
        WithMachineInBed(Sub() FloppyController.Eject(drive))
        UpdateMediaStatus()
    End Sub

    Private Sub EjectHardDisk()
        If _ideDriveShelf IsNot Nothing Then
            _ideDriveShelf.PrimaryMasterId = -1
            _ideDriveShelf.SaveConfiguration()
        End If
        WithMachineInBed(Sub() Declares.IdeController.EjectHardDisk())
        _mountedIdeDriveId = -1
        UpdateMediaStatus()
        If ideDriveShelfMenu IsNot Nothing Then RebuildIdeDriveShelfMenu()
    End Sub

    Private Sub EjectCdRom()
        WithMachineInBed(Sub() Declares.IdeController.EjectCdRom())
        UpdateMediaStatus()
    End Sub

    Private Sub BootFloppyA()
        Try
            If Not ReadMachineInBed(Function() FloppyController.IsMediaPresent(0)) Then Throw New InvalidOperationException("Insert floppy media in drive A first.")
            ResetThroughFirmware()
        Catch ex As Exception
            MessageBox.Show(Me, ex.Message, "Floppy boot failed", MessageBoxButtons.OK, MessageBoxIcon.Error)
        End Try
    End Sub

    Private Sub BootHardDisk()
        Try
            If ReadMachineInBed(Function() Declares.IdeController.HardDiskSectorCount) = 0 Then Throw New InvalidOperationException("Mount a hard-disk image first.")
            ResetThroughFirmware()
        Catch ex As Exception
            MessageBox.Show(Me, ex.Message, "Hard-disk boot failed", MessageBoxButtons.OK, MessageBoxIcon.Error)
        End Try
    End Sub

    Private Sub ResetThroughFirmware()
        If _closingInBed OrElse Not _machinePoweredInBed Then Return

        ' A front-panel reset must also recover the host execution worker if the
        ' guest previously faulted it. Stop/join first, reset the physical AT reset
        ' domain while no slice is active, reset host scheduler adaptation, then
        ' restart the same machine. Mounted media and DRAM contents are preserved.
        _machineRuntime.Stop()
        Dim resetSucceededInBed As Boolean = False
        Try
            _machineRuntime.ExecuteWithHostTimeRebase(
                Sub()
                    CPU0.HostFirmwareInterrupts = False
                    ResetHardwareMachine()
                    ApplyFirmwareResetVectorSafetyGuardInBed()
                    FrontPanel.SetCpuStateByte(0)
                End Sub)
            _machineRuntime.ResetHostSchedulingState()
            resetSucceededInBed = True
        Finally
            If resetSucceededInBed AndAlso
               Not _closingInBed AndAlso
               _machinePoweredInBed Then
                _machineRuntime.Start()
                Dim presentationInBed As DiamondStealthPro928PresentationWorker = _videoPresentation
                If presentationInBed IsNot Nothing Then presentationInBed.RequestFrame()
            End If
        End Try
    End Sub

    Private Sub ToggleMachinePowerInBed()
        If _closingInBed Then Return
        Try
            SetMachinePowerInBed(Not _machinePoweredInBed)
        Catch ex As Exception
            MessageBox.Show(Me,
                            ex.Message,
                            "Chassis power transition failed",
                            MessageBoxButtons.OK,
                            MessageBoxIcon.Error)
        End Try
    End Sub

    Private Sub SetMachinePowerInBed(poweredInBed As Boolean)
        If _closingInBed Then Return
        If poweredInBed = _machinePoweredInBed Then
            UpdatePowerControlsInBed()
            Return
        End If

        If Not poweredInBed Then
            ' AT chassis power-off is a host lifecycle transition, not HLT. Stop
            ' the sole machine owner thread and leave mounted media/CMOS attached.
            ReleaseSerialMouseCaptureInBed()
            _machineRuntime.Stop()
            _machinePoweredInBed = False
            FrontPanel.SetCpuStateByte(0)
            FrontPanel.SetPower(False)

            Dim presentationInBed As DiamondStealthPro928PresentationWorker = _videoPresentation
            If presentationInBed IsNot Nothing Then presentationInBed.RequestPowerOffFrame()

            UpdatePowerControlsInBed()
            Return
        End If

        ' Cold power-on discards volatile DRAM, asserts the motherboard/device
        ' reset tree, starts at the 80286 reset vector, and then starts a fresh
        ' runtime worker. CMOS and mounted removable/fixed media remain present.
        Try
            _machineRuntime.ExecuteWithHostTimeRebase(
                Sub()
                    If _ramBanks IsNot Nothing AndAlso
                       RamBankConfiguration.IsSupported(_ramBanks.InstalledMemoryMb) Then
                        CPU0.ConfigureInstalledMemoryMegabytes(_ramBanks.InstalledMemoryMb,
                                                               clearRam:=True)
                    End If

                    CPU0.HostFirmwareInterrupts = False
                    PowerCycleHardwareMachine()
                    ApplyFirmwareResetVectorSafetyGuardInBed()
                    FrontPanel.SetCpuStateByte(0)
                End Sub)
            _machineRuntime.ResetHostSchedulingState()

            _machinePoweredInBed = True
            FrontPanel.SetPower(True)
            _machineRuntime.Start()

            Dim presentationInBed As DiamondStealthPro928PresentationWorker = _videoPresentation
            If presentationInBed IsNot Nothing Then presentationInBed.RequestFrame()
        Catch
            _machinePoweredInBed = False
            FrontPanel.SetPower(False)
            Throw
        Finally
            UpdatePowerControlsInBed()
        End Try
    End Sub

    Private Sub UpdatePowerControlsInBed()
        If _powerMenuItemInBed IsNot Nothing Then
            _powerMenuItemInBed.Text = If(_machinePoweredInBed, "Power Off", "Power On")
        End If
        If _resetMenuItemInBed IsNot Nothing Then
            _resetMenuItemInBed.Enabled = _machinePoweredInBed
        End If
    End Sub

    Private Sub InitializeFloppyBox()
        _floppyBox = New FloppyBox(AppContext.BaseDirectory)
        _floppyBox.EnsureExists()
    End Sub

    Private Sub InitializeDriveBayPanelInBed()
        If _driveBayPanelInBed IsNot Nothing Then Return

        Dim artworkDirectoryInBed As String =
            Path.Combine(AppContext.BaseDirectory, "Resources", "System Images")
        _threeAndHalfDriveFaceInBed = LoadDriveFaceInBed(
            Path.Combine(artworkDirectoryInBed, "Teac-Floppy.png"))
        _fiveAndQuarterDriveFaceInBed = LoadDriveFaceInBed(
            Path.Combine(artworkDirectoryInBed, "five and a quarter floppy.png"))
        _emptyDriveFaceInBed = LoadDriveFaceInBed(
            Path.Combine(artworkDirectoryInBed, "five and a quarter face plate.png"))

        Const bayRailWidthInBed As Integer = 210
        Const displayGapInBed As Integer = 10
        Dim originalDisplayLeftInBed As Integer = PictureBox1.Left
        _driveBayPanelInBed = New Panel() With {
            .Location = New Point(originalDisplayLeftInBed, PictureBox1.Top),
            .Size = New Size(bayRailWidthInBed, PictureBox1.Height),
            .Anchor = AnchorStyles.Top Or AnchorStyles.Bottom Or AnchorStyles.Left,
            .BackColor = Color.FromArgb(178, 176, 158),
            .BorderStyle = BorderStyle.FixedSingle
        }
        _driveBayToolTipInBed = New ToolTip()
        _floppyBayAInBed = CreateFloppyBayFaceInBed(0, 8)
        _floppyBayBInBed = CreateFloppyBayFaceInBed(1, 76)
        _driveBayPanelInBed.Controls.Add(_floppyBayAInBed)
        _driveBayPanelInBed.Controls.Add(_floppyBayBInBed)
        Controls.Add(_driveBayPanelInBed)
        _driveBayPanelInBed.BringToFront()

        PictureBox1.Left = originalDisplayLeftInBed + bayRailWidthInBed + displayGapInBed
        PictureBox1.Width = Math.Max(1, PictureBox1.Width - bayRailWidthInBed - displayGapInBed)
        UpdateDriveBayFacesInBed(Nothing, Nothing)
    End Sub

    Private Shared Function LoadDriveFaceInBed(pathInBed As String) As Bitmap
        Using streamInBed As New FileStream(pathInBed, FileMode.Open, FileAccess.Read, FileShare.Read)
            Using sourceInBed As New Bitmap(streamInBed)
                Return New Bitmap(sourceInBed)
            End Using
        End Using
    End Function

    Private Function CreateFloppyBayFaceInBed(driveInBed As Integer, topInBed As Integer) As PictureBox
        Dim faceInBed As New PictureBox() With {
            .Location = New Point(7, topInBed),
            .Size = New Size(194, 58),
            .Anchor = AnchorStyles.Top Or AnchorStyles.Left Or AnchorStyles.Right,
            .SizeMode = PictureBoxSizeMode.Zoom,
            .BackColor = Color.FromArgb(178, 176, 158),
            .Cursor = Cursors.Hand,
            .Tag = driveInBed,
            .TabStop = False
        }
        _driveBayToolTipInBed.SetToolTip(
            faceInBed,
            "Floppy " & ChrW(AscW("A"c) + driveInBed) & ": — click for media choices")
        AddHandler faceInBed.Click,
            Sub() ShowFloppyBayMenuInBed(faceInBed, driveInBed)
        Return faceInBed
    End Function

    Private Sub ShowFloppyBayMenuInBed(faceInBed As PictureBox, driveInBed As Integer)
        If faceInBed Is Nothing OrElse faceInBed.IsDisposed Then Return

        ' Build from the same routine used by the Media menu, then transfer the
        ' live items (including their handlers) into a short-lived context menu.
        ' This keeps the chassis shortcut and top-level menu behavior identical.
        Dim sourceInBed As New ToolStripMenuItem()
        RebuildFloppyBoxMountMenu(sourceInBed, driveInBed)
        Dim popupInBed As New ContextMenuStrip()
        While sourceInBed.DropDownItems.Count > 0
            Dim itemInBed As ToolStripItem = sourceInBed.DropDownItems(0)
            sourceInBed.DropDownItems.RemoveAt(0)
            popupInBed.Items.Add(itemInBed)
        End While
        AddHandler popupInBed.Closed,
            Sub(senderInBed As Object, eInBed As ToolStripDropDownClosedEventArgs)
                popupInBed.Dispose()
                sourceInBed.Dispose()
            End Sub
        popupInBed.Show(faceInBed, New Point(0, faceInBed.Height))
    End Sub

    Private Sub UpdateDriveBayFacesInBed(geometryAInBed As Integer(), geometryBInBed As Integer())
        UpdateDriveBayFaceInBed(_floppyBayAInBed, 0, geometryAInBed)
        UpdateDriveBayFaceInBed(_floppyBayBInBed, 1, geometryBInBed)
    End Sub

    Private Sub UpdateDriveBayFaceInBed(faceInBed As PictureBox,
                                        driveInBed As Integer,
                                        geometryInBed As Integer())
        If faceInBed Is Nothing Then Return
        Dim isFiveAndQuarterInBed As Boolean =
            geometryInBed IsNot Nothing AndAlso geometryInBed.Length >= 3 AndAlso
            (geometryInBed(0) <= 40 OrElse geometryInBed(2) = 15)
        faceInBed.Image = If(isFiveAndQuarterInBed,
                             _fiveAndQuarterDriveFaceInBed,
                             _threeAndHalfDriveFaceInBed)
        Dim formatInBed As String = If(isFiveAndQuarterInBed, "5.25-inch", "3.5-inch")
        _driveBayToolTipInBed.SetToolTip(
            faceInBed,
            "Floppy " & ChrW(AscW("A"c) + driveInBed) & ": — " & formatInBed &
            " face; click for Disk Box and drive actions")
    End Sub

    Private Sub RebuildFloppyBoxMountMenu(menuInBed As ToolStripMenuItem,
                                          driveInBed As Integer)
        If menuInBed Is Nothing Then Return
        menuInBed.DropDownItems.Clear()
        Dim currentMediaSourceIdInBed As String =
            ReadMachineInBed(Function() FloppyController.GetMediaSourceId(driveInBed))

        Dim diskBoxInBed As New ToolStripMenuItem("Disk Box")
        If _floppyBox Is Nothing Then
            diskBoxInBed.DropDownItems.Add(
                New ToolStripMenuItem("Disk Box is not initialized") With {.Enabled = False})
        Else
            Try
                Dim imagesInBed As List(Of String) = _floppyBox.GetImages()
                If imagesInBed.Count = 0 Then
                    diskBoxInBed.DropDownItems.Add(
                        New ToolStripMenuItem("Disk Box is empty") With {.Enabled = False})
                Else
                    For Each imagePathInBed As String In imagesInBed
                        Dim selectedPathInBed As String = imagePathInBed
                        Dim itemInBed As New ToolStripMenuItem(
                            Path.GetFileNameWithoutExtension(selectedPathInBed)) With {
                            .ToolTipText = selectedPathInBed,
                            .Checked = String.Equals(
                                currentMediaSourceIdInBed,
                                "image|" & Path.GetFullPath(selectedPathInBed),
                                StringComparison.OrdinalIgnoreCase)
                        }
                        AddHandler itemInBed.Click,
                            Sub() MountFloppyFromBox(driveInBed, selectedPathInBed)
                        diskBoxInBed.DropDownItems.Add(itemInBed)
                    Next
                End If
            Catch ex As Exception
                diskBoxInBed.DropDownItems.Clear()
                diskBoxInBed.DropDownItems.Add(
                    New ToolStripMenuItem("Unable to read Disk Box: " & ex.Message) With {
                        .Enabled = False
                    })
            End Try
        End If
        menuInBed.DropDownItems.Add(diskBoxInBed)

        Dim physicalInBed As New ToolStripMenuItem("Physical Floppy Drive")
        Try
            Dim hostRootsInBed As List(Of String) = PhysicalFloppyMediaSource.EnumerateHostFloppyRoots()
            If hostRootsInBed.Count = 0 Then
                physicalInBed.DropDownItems.Add(
                    New ToolStripMenuItem("No host floppy drives detected") With {.Enabled = False})
            Else
                For Each hostRootInBed As String In hostRootsInBed
                    Dim selectedRootInBed As String = hostRootInBed
                    Dim itemInBed As New ToolStripMenuItem(
                        PhysicalFloppyMediaSource.DescribeHostDrive(selectedRootInBed)) With {
                        .Checked = String.Equals(
                            currentMediaSourceIdInBed,
                            PhysicalFloppyMediaSource.SourceIdForHostRoot(selectedRootInBed),
                            StringComparison.OrdinalIgnoreCase),
                        .ToolTipText = "Attach virtual drive " & ChrW(AscW("A"c) + driveInBed) &
                                       " directly to host floppy drive " & selectedRootInBed.Substring(0, 2)
                    }
                    AddHandler itemInBed.Click,
                        Sub() MountPhysicalFloppyFromHost(driveInBed, selectedRootInBed)
                    physicalInBed.DropDownItems.Add(itemInBed)
                Next
            End If
        Catch ex As Exception
            physicalInBed.DropDownItems.Clear()
            physicalInBed.DropDownItems.Add(
                New ToolStripMenuItem("Unable to enumerate host floppy drives: " & ex.Message) With {.Enabled = False})
        End Try
        menuInBed.DropDownItems.Add(physicalInBed)

        menuInBed.DropDownItems.Add(New ToolStripSeparator())
        menuInBed.DropDownItems.Add(
            "Browse for floppy image...",
            Nothing,
            Sub() ChooseFloppy(driveInBed))
    End Sub

    Private Sub MountFloppyFromBox(driveInBed As Integer,
                                   imagePathInBed As String)
        Try
            WithMachineInBed(Sub() MountFloppyImage(driveInBed, imagePathInBed))
            UpdateMediaStatus()
        Catch ex As Exception
            MessageBox.Show(
                Me,
                ex.Message,
                "Unable to mount floppy",
                MessageBoxButtons.OK,
                MessageBoxIcon.Error)
        End Try
    End Sub

    Private Sub MountPhysicalFloppyFromHost(driveInBed As Integer,
                                                hostDriveRootInBed As String)
        Try
            WithMachineInBed(Sub() MountPhysicalFloppyDrive(driveInBed, hostDriveRootInBed))
            UpdateMediaStatus()
        Catch ex As Exception
            MessageBox.Show(
                Me,
                ex.Message,
                "Unable to attach physical floppy drive",
                MessageBoxButtons.OK,
                MessageBoxIcon.Error)
        End Try
    End Sub

    Private Sub OpenFloppyBox()
        If _floppyBox Is Nothing Then InitializeFloppyBox()
        _floppyBox.EnsureExists()

        Dim startInBed As New ProcessStartInfo() With {
            .FileName = _floppyBox.RootPath,
            .UseShellExecute = True
        }
        Process.Start(startInBed)
    End Sub

    Private Sub RunSneakerNet()
        If _floppyBox Is Nothing Then InitializeFloppyBox()

        If _sneakerNetForm Is Nothing OrElse _sneakerNetForm.IsDisposed Then
            _sneakerNetForm = New SneakerNetForm(
                _floppyBox,
                Sub(driveInBed As Integer, imagePathInBed As String)
                    MountFloppyFromBox(driveInBed, imagePathInBed)
                    UpdateMediaStatus()
                End Sub,
                Sub(driveInBed As Integer)
                    EjectFloppy(driveInBed)
                    UpdateMediaStatus()
                End Sub,
                Sub()
                    ResetThroughFirmware()
                    UpdateMediaStatus()
                End Sub,
                _ideDriveShelf,
                Sub(driveIdInBed As Integer)
                    SelectIdeShelfDrive(driveIdInBed)
                    UpdateMediaStatus()
                End Sub,
                Sub()
                    EjectHardDisk()
                    UpdateMediaStatus()
                End Sub,
                Function() _mountedIdeDriveId,
                Sub(imagePathInBed As String)
                    WithMachineInBed(Sub() MountIsoImage(imagePathInBed))
                    UpdateMediaStatus()
                End Sub,
                Sub()
                    EjectCdRom()
                    UpdateMediaStatus()
                End Sub,
                Function() AcquireMediaQuiesceLeaseInBed())
            AddHandler _sneakerNetForm.FormClosed,
                Sub(senderInBed As Object, eInBed As FormClosedEventArgs)
                    _sneakerNetForm = Nothing
                End Sub
            _sneakerNetForm.Show(Me)
        Else
            If _sneakerNetForm.WindowState = FormWindowState.Minimized Then _sneakerNetForm.WindowState = FormWindowState.Normal
            _sneakerNetForm.BringToFront()
            _sneakerNetForm.Activate()
        End If
    End Sub

    Private Function AcquireMediaQuiesceLeaseInBed() As IDisposable
        Dim resumeInBed As Boolean = _machineRuntime IsNot Nothing AndAlso _machineRuntime.IsRunning
        If resumeInBed Then _machineRuntime.Stop()

        Return New HostActionLease(
            Sub()
                If resumeInBed AndAlso Not _closingInBed AndAlso _machinePoweredInBed Then
                    _machineRuntime.ResetHostSchedulingState()
                    _machineRuntime.Start()
                    Dim presentationInBed As DiamondStealthPro928PresentationWorker = _videoPresentation
                    If presentationInBed IsNot Nothing Then presentationInBed.RequestFrame()
                End If
            End Sub)
    End Function

    Private Sub ChooseFloppy(drive As Integer)
        Using picker As New OpenFileDialog()
            picker.Title = "Mount floppy " & ChrW(AscW("A"c) + drive)
            picker.Filter = "Raw floppy images (*.ima;*.img)|*.ima;*.img|All files (*.*)|*.*"
            If picker.ShowDialog(Me) <> DialogResult.OK Then Return
            Try
                WithMachineInBed(Sub() MountFloppyImage(drive, picker.FileName))
                UpdateMediaStatus()
            Catch ex As Exception
                MessageBox.Show(Me, ex.Message, "Unable to mount floppy", MessageBoxButtons.OK, MessageBoxIcon.Error)
            End Try
        End Using
    End Sub

    Private Sub ChooseHardDisk()
        Using picker As New OpenFileDialog()
            picker.Title = "Mount hard-disk image"
            picker.Filter = "Raw disk images (*.img;*.hdd)|*.img;*.hdd|All files (*.*)|*.*"
            If picker.ShowDialog(Me) <> DialogResult.OK Then Return
            Try
                WithMachineInBed(Sub() MountHardDiskImage(picker.FileName))
                UpdateMediaStatus()
            Catch ex As Exception
                MessageBox.Show(Me, ex.Message, "Unable to mount hard disk", MessageBoxButtons.OK, MessageBoxIcon.Error)
            End Try
        End Using
    End Sub

    Private Sub CreateHardDisk()
        If _ideDriveShelf Is Nothing Then Return
        _ideDriveShelf.EnsureShelfExists()

        Dim label As String = Microsoft.VisualBasic.Interaction.InputBox(
            "Label for the new 64 MB IDE drive:",
            "Create IDE Shelf Drive",
            "New 64 MB Drive")
        If String.IsNullOrWhiteSpace(label) Then Return

        Try
            Dim driveId As Integer = _ideDriveShelf.NextAvailableId()
            Dim safeLabel As String = IdeDriveShelf.SanitizeLabel(label)
            Dim imagePath As String = Path.Combine(_ideDriveShelf.RootPath, driveId.ToString() & " - " & safeLabel & ".hdd")
            HardDiskImage.Create(imagePath, 64L * 1024L * 1024L \ 512L)

            _ideDriveShelf.PrimaryMasterId = driveId
            _ideDriveShelf.SaveConfiguration()
            AttachConfiguredPrimaryMaster(silent:=False)
            UpdateMediaStatus()
            RebuildIdeDriveShelfMenu()
            If _mountedIdeDriveId = driveId Then ResetThroughFirmware()
        Catch ex As Exception
            MessageBox.Show(Me, ex.Message, "Unable to create IDE shelf drive", MessageBoxButtons.OK, MessageBoxIcon.Error)
        End Try
    End Sub

    Private Sub ChooseIso()
        Using picker As New OpenFileDialog()
            picker.Title = "Mount CD-ROM image"
            picker.Filter = "ISO images (*.iso)|*.iso|All files (*.*)|*.*"
            If picker.ShowDialog(Me) <> DialogResult.OK Then Return
            Try
                WithMachineInBed(Sub() MountIsoImage(picker.FileName))
                UpdateMediaStatus()
            Catch ex As Exception
                MessageBox.Show(Me, ex.Message, "Unable to mount CD-ROM", MessageBoxButtons.OK, MessageBoxIcon.Error)
            End Try
        End Using
    End Sub
    Private Sub SystemLoop_Tick(sender As System.Object, e As System.EventArgs) Handles SystemLoop.Tick
        ' Deliberately inert. MachineRuntime286 owns CPU/motherboard execution.
        ' Keeping the designer timer stub avoids destructive form-designer churn.
        SystemLoop.Enabled = False
    End Sub

    Private Sub RecordCpuPerformanceInBed(consumedTStatesInBed As Long,
                                           elapsedHostTicksInBed As Long,
                                           sliceExecutionTicksInBed As Long,
                                           currentTargetInBed As Long)
        SyncLock _cpuPerfLockInBed
            ' CROMWELL CALIBRATED DYNO / PCB REFIT PHASE 2 BRICK 8A
            ' elapsedHostTicksInBed is retained in the signature for source compatibility
            ' but is intentionally not used for the rate numerator/denominator pairing.
            Dim recordNowInBed As Long = Stopwatch.GetTimestamp()

            If _cpuPerfTargetClockHz <> currentTargetInBed Then
                _cpuPerfTargetClockHz = currentTargetInBed
                _cpuPerfAccumulatedTStates = 0
                _cpuPerfAccumulatedHostTicks = 0
                _cpuPerfEffectiveTStatesPerSecond = 0.0
                _cpuPerfSamplesReady = False
                _cpuPerfAlignedLastRecordTicksInBed = recordNowInBed
                _cpuPerfSliceCostAccumTicksInBed = 0
                _cpuPerfSliceCostMinTicksInBed = Long.MaxValue
                _cpuPerfSliceCostMaxTicksInBed = 0
                _cpuPerfSliceCostCountInBed = 0
            Else
                If _cpuPerfAlignedLastRecordTicksInBed <> 0 Then
                    Dim alignedWallTicksInBed As Long = recordNowInBed - _cpuPerfAlignedLastRecordTicksInBed
                    If alignedWallTicksInBed > 0 Then _cpuPerfAccumulatedHostTicks += alignedWallTicksInBed
                End If
                _cpuPerfAlignedLastRecordTicksInBed = recordNowInBed
                If consumedTStatesInBed > 0 Then _cpuPerfAccumulatedTStates += consumedTStatesInBed
            End If

            If sliceExecutionTicksInBed >= 0 Then
                _cpuPerfLastSliceMilliseconds = CDbl(sliceExecutionTicksInBed) * 1000.0 / CDbl(Stopwatch.Frequency)
                _cpuPerfSliceCostAccumTicksInBed += sliceExecutionTicksInBed
                If sliceExecutionTicksInBed < _cpuPerfSliceCostMinTicksInBed Then _cpuPerfSliceCostMinTicksInBed = sliceExecutionTicksInBed
                If sliceExecutionTicksInBed > _cpuPerfSliceCostMaxTicksInBed Then _cpuPerfSliceCostMaxTicksInBed = sliceExecutionTicksInBed
                _cpuPerfSliceCostCountInBed += 1
            End If

            ' Half-second aligned wall windows: guest work performed between successive
            ' completed slices divided by the same end-to-end host wall interval.
            If _cpuPerfAccumulatedHostTicks >= Math.Max(1L, Stopwatch.Frequency \ 2L) Then
                _cpuPerfEffectiveTStatesPerSecond =
                CDbl(_cpuPerfAccumulatedTStates) * CDbl(Stopwatch.Frequency) / CDbl(_cpuPerfAccumulatedHostTicks)

                If _cpuPerfSliceCostCountInBed > 0 Then
                    Dim millisecondsPerTickInBed As Double = 1000.0 / CDbl(Stopwatch.Frequency)
                    _cpuPerfAverageSliceMillisecondsInBed =
                    (CDbl(_cpuPerfSliceCostAccumTicksInBed) / CDbl(_cpuPerfSliceCostCountInBed)) * millisecondsPerTickInBed
                    _cpuPerfMinimumSliceMillisecondsInBed =
                    CDbl(_cpuPerfSliceCostMinTicksInBed) * millisecondsPerTickInBed
                    _cpuPerfMaximumSliceMillisecondsInBed =
                    CDbl(_cpuPerfSliceCostMaxTicksInBed) * millisecondsPerTickInBed
                    _cpuPerfLastWindowSliceCountInBed = _cpuPerfSliceCostCountInBed
                End If

                _cpuPerfAccumulatedTStates = 0
                _cpuPerfAccumulatedHostTicks = 0
                _cpuPerfSliceCostAccumTicksInBed = 0
                _cpuPerfSliceCostMinTicksInBed = Long.MaxValue
                _cpuPerfSliceCostMaxTicksInBed = 0
                _cpuPerfSliceCostCountInBed = 0
                _cpuPerfSamplesReady = True
            End If
        End SyncLock
    End Sub
    Private Sub ShowCpuPerformanceDiagnosticsInBed()
        If _cpuPerfForm IsNot Nothing AndAlso Not _cpuPerfForm.IsDisposed Then
            UpdateCpuPerformanceWindowInBed()
            _cpuPerfForm.BringToFront()
            _cpuPerfForm.Activate()
            Return
        End If

        WithMachineInBed(Sub() CPU0.ResetHotPathProfiler())
        SyncLock _cpuPerfLockInBed
            _cpuPerfAccumulatedTStates = 0
            _cpuPerfAccumulatedHostTicks = 0
            _cpuPerfEffectiveTStatesPerSecond = 0.0
            _cpuPerfSamplesReady = False
            _cpuPerfAlignedLastRecordTicksInBed = Stopwatch.GetTimestamp()
            _cpuPerfSliceCostAccumTicksInBed = 0
            _cpuPerfSliceCostMinTicksInBed = Long.MaxValue
            _cpuPerfSliceCostMaxTicksInBed = 0
            _cpuPerfSliceCostCountInBed = 0
        End SyncLock


        _cpuPerfForm = New System.Windows.Forms.Form() With {
            .Text = "CPU Performance",
            .StartPosition = FormStartPosition.CenterParent,
            .ClientSize = New Size(860, 760),
            .AutoScroll = True,
            .FormBorderStyle = FormBorderStyle.SizableToolWindow,
            .MaximizeBox = False,
            .MinimizeBox = False,
            .ShowInTaskbar = False
        }

        _cpuPerfText = New System.Windows.Forms.Label() With {
            .Dock = DockStyle.Top,
            .Padding = New Padding(14),
            .Font = New Font("Consolas", 10.0F, FontStyle.Regular),
            .AutoSize = True
        }
        _cpuPerfForm.Controls.Add(_cpuPerfText)

        _cpuPerfUiTimer = New System.Windows.Forms.Timer() With {.Interval = 250}
        AddHandler _cpuPerfUiTimer.Tick, Sub() UpdateCpuPerformanceWindowInBed()
        AddHandler _cpuPerfForm.FormClosed,
            Sub()
                If _cpuPerfUiTimer IsNot Nothing Then
                    _cpuPerfUiTimer.Stop()
                    _cpuPerfUiTimer.Dispose()
                End If
                _cpuPerfUiTimer = Nothing
                _cpuPerfText = Nothing
                _cpuPerfForm = Nothing
            End Sub

        UpdateCpuPerformanceWindowInBed()
        _cpuPerfUiTimer.Start()
        _cpuPerfForm.Show(Me)
    End Sub

    Private Sub UpdateCpuPerformanceWindowInBed()
        If _cpuPerfText Is Nothing OrElse _cpuPerfText.IsDisposed Then Return

        WithMachineInBed(
            Sub()
                SyncLock _cpuPerfLockInBed
                    Dim targetHzInBed As Long = MachineClock.CpuClockHz
                    Dim effectiveHzInBed As Double = _cpuPerfEffectiveTStatesPerSecond
                    Dim ratioInBed As Double = If(targetHzInBed > 0, effectiveHzInBed / CDbl(targetHzInBed), 0.0)
                    Dim debtTStatesInBed As Long = MachineClock.PendingTStates
                    Dim debtMillisecondsInBed As Double = If(targetHzInBed > 0,
            CDbl(debtTStatesInBed) * 1000.0 / CDbl(targetHzInBed), 0.0)

                    Dim batchFlushCountInBed As Long = MachineClock.LastClockBatchFlushCount
                    Dim batchAverageInBed As Double = MachineClock.LastClockBatchAverageTStates
                    Dim batchLargestInBed As Long = MachineClock.LastClockBatchLargestFlushTStates
                    Dim batchCeilingInBed As Long = MachineClock.LastClockBatchMaximumTStates
                    Dim batchPortInBed As Long = MachineClock.LastClockBatchPortFlushCount
                    Dim batchMemoryInBed As Long = MachineClock.LastClockBatchMemoryFlushCount
                    Dim batchWakeInBed As Long = MachineClock.LastClockBatchWakeFlushCount
                    Dim batchCeilingFlushInBed As Long = MachineClock.LastClockBatchCeilingFlushCount
                    Dim batchEndInBed As Long = MachineClock.LastClockBatchEndFlushCount
                    Dim batchOtherInBed As Long = MachineClock.LastClockBatchExplicitFlushCount
                    Dim unclassifiedClocksInBed As String = SystemBus.UnclassifiedClockedDeviceDiagnosticText

                    Dim statusInBed As String
                    If Not _cpuPerfSamplesReady Then
                        statusInBed = "MEASURING"
                    ElseIf ratioInBed >= 0.95 AndAlso debtMillisecondsInBed < 100.0 Then
                        statusInBed = "KEEPING UP"
                    Else
                        statusInBed = "FALLING BEHIND"
                    End If

                    Dim effectiveTextInBed As String = If(_cpuPerfSamplesReady,
            (effectiveHzInBed / 1000000.0).ToString("0.000") & " MT-states/s",
            "measuring...")
                    Dim ratioTextInBed As String = If(_cpuPerfSamplesReady,
            (ratioInBed * 100.0).ToString("0.0") & " %",
            "measuring...")

                    _cpuPerfText.Text =
            "Harris CS80C286 performance" & Environment.NewLine & Environment.NewLine &
            "Target clock           : " & (CDbl(targetHzInBed) / 1000000.0).ToString("0.000") & " MHz" & Environment.NewLine &
            "Effective T-state rate : " & effectiveTextInBed & Environment.NewLine &
            "Real-time ratio        : " & ratioTextInBed & Environment.NewLine &
            "Pending scheduler debt : " & debtTStatesInBed.ToString("N0") & " T-states" & Environment.NewLine &
            "Debt as guest time     : " & debtMillisecondsInBed.ToString("0.0") & " ms" & Environment.NewLine &
            "Last RunSlice host cost: " & _cpuPerfLastSliceMilliseconds.ToString("0.0") & " ms" & Environment.NewLine &
            "RunSlice avg/min/max    : " & _cpuPerfAverageSliceMillisecondsInBed.ToString("0.0") & " / " & _cpuPerfMinimumSliceMillisecondsInBed.ToString("0.0") & " / " & _cpuPerfMaximumSliceMillisecondsInBed.ToString("0.0") & " ms" & Environment.NewLine &
            "Slices in rate window  : " & _cpuPerfLastWindowSliceCountInBed.ToString("N0") & Environment.NewLine &
            "Rate measurement basis : aligned end-to-end wall" & Environment.NewLine &
            "Slice ceiling          : " & _machineRuntime.CurrentMaximumTStatesPerSlice.ToString("N0") & " T-states adaptive (hard max " & MachineRuntime286.MaximumTStatesPerSlice.ToString("N0") & ")" & Environment.NewLine &
            "Execution owner        : dedicated single machine thread" & Environment.NewLine &
            "Clock batch ceiling    : " & batchCeilingInBed.ToString("N0") & " T-states" & Environment.NewLine &
            "Unclassified clocks    : " & unclassifiedClocksInBed & Environment.NewLine &
            "Clock batches / slice  : " & batchFlushCountInBed.ToString("N0") & Environment.NewLine &
            "Average T-states/batch : " & batchAverageInBed.ToString("0.0") & Environment.NewLine &
            "Largest T-state batch  : " & batchLargestInBed.ToString("N0") & Environment.NewLine &
            "Flushes port / MMIO    : " & batchPortInBed.ToString("N0") & " / " & batchMemoryInBed.ToString("N0") & Environment.NewLine &
            "Flushes wake / ceiling : " & batchWakeInBed.ToString("N0") & " / " & batchCeilingFlushInBed.ToString("N0") & Environment.NewLine &
            "Flushes end / other    : " & batchEndInBed.ToString("N0") & " / " & batchOtherInBed.ToString("N0") & Environment.NewLine & Environment.NewLine &
            CPU0.HotPathDiagnosticText() & Environment.NewLine & Environment.NewLine &
            CpuBus.DiagnosticText() & Environment.NewLine &
            MotherboardBridge.DiagnosticText() & Environment.NewLine & Environment.NewLine &
            MotherboardMemory.DiagnosticText() & Environment.NewLine & Environment.NewLine &
            "Status                 : " & statusInBed
                End SyncLock
            End Sub)
    End Sub
    Private Sub GPU_Tick(sender As System.Object, e As System.EventArgs) Handles GPU.Tick
        ' S3 rasterization and CRT/bezel composition both occur on the presentation
        ' worker. WinForms only swaps in a completed frame when a new generation exists.
        Dim presentationInBed As DiamondStealthPro928PresentationWorker = _videoPresentation
        If presentationInBed Is Nothing Then Return

        If _machinePoweredInBed Then presentationInBed.RequestFrame()
        Dim nextFrameInBed As Bitmap =
            presentationInBed.TakeLatestFrame(_videoPresentationGenerationInBed)
        If nextFrameInBed Is Nothing Then Return

        Dim previousFrameInBed As Bitmap = _displayedPresentationFrameInBed
        _displayedPresentationFrameInBed = nextFrameInBed
        PictureBox1.Image = nextFrameInBed
        PictureBox1.Invalidate()

        If previousFrameInBed IsNot Nothing Then
            presentationInBed.RecycleFrame(previousFrameInBed)
        End If
    End Sub

    Private Shared Sub ReadFully(stream As Stream, buffer As Byte(), count As Integer)
        Dim offset As Integer
        While offset < count
            Dim bytesRead As Integer = stream.Read(buffer, offset, count - offset)
            If bytesRead = 0 Then Throw New EndOfStreamException("Unexpected end of file.")
            offset += bytesRead
        End While
    End Sub
    Private Sub Mode2_Tick(sender As System.Object, e As System.EventArgs) Handles Mode2.Tick
        Dim KeyCode As Byte, Attributes As Byte, ReadStep As Int32
        Dim ABits(7) As Byte, worker As Single, Halflife As Byte = 128
        Dim ColorState As Byte, BColorState As Byte
        Static loopthru As Byte
        Static Blinker As Int16
        Blinker += 1
        If Blinker > 6 Then Blinker = 0
        OffSet = 32768 + CInt(CgaController.StartAddress) * 2
        For y = 1 To 25
            For x = 1 To 80
                'Read from memory
                Attributes = VrMem(11, OffSet + ReadStep)
                KeyCode = VrMem(11, OffSet + 1 + ReadStep)
                'If KeyCode = 255 Then KeyCode = 0

                ReadStep += 2
                'read from memory
                worker = Attributes
                'Interpret attributes into bit flags
                For StepThroughBit = 7 To 0 Step -1
                    If worker - Halflife < 0 Then
                        ABits(StepThroughBit) = 0
                    Else
                        ABits(StepThroughBit) = 1
                        worker -= Halflife
                    End If
                    Halflife >>= 1
                Next
                Halflife = 128
                'interpret attributes into bit flags

                'assign colors
                If ABits(7) = 1 Then BColorState = 0
                If ABits(6) = 1 Then BColorState = 0
                If ABits(5) = 1 Then BColorState = 0
                If ABits(4) = 1 Then BColorState = 0
                If ABits(3) = 1 Then ColorState += 8
                If ABits(2) = 1 Then ColorState = 7
                If ABits(1) = 1 Then ColorState = 7
                If ABits(0) = 1 Then ColorState = 7
                'assign colors
                If KeyCode > 127 Then KeyCode = 254
                If BColorState > 7 Then
                    If Blinker < 4 Then
                        Buffer_W.DrawImage(BGColors(BColorState - 8), (x - 1) * 8, (y - 1) * 16)
                        Buffer_W.DrawImage(AlphaGen(ColorState, KeyCode), (x - 1) * 8, (y - 1) * 16)
                    Else
                        Buffer_W.DrawImage(BGColors(BColorState - 8), (x - 1) * 8, (y - 1) * 16)
                    End If
                Else
                    Buffer_W.DrawImage(BGColors(BColorState), (x - 1) * 8, (y - 1) * 16)
                    Buffer_W.DrawImage(AlphaGen(ColorState, KeyCode), (x - 1) * 8, (y - 1) * 16)
                End If
                ColorState = 0
                BColorState = 0
                For clean = 0 To 7
                    ABits(clean) = 0
                Next
            Next
        Next
        If loopthru = 1 Then
            loopthru = 0
            For MaskY = 0 To 15
                For MaskX = 0 To 20
                    Buffer_W.DrawImage(ElectronGun(0), MaskX * 32, MaskY * 32) ' draw pixel mask to emulate old pixel sizes
                Next
            Next
        Else
            loopthru = 1
            For MaskY = 0 To 15
                For MaskX = 0 To 20
                    Buffer_W.DrawImage(ElectronGun(1), MaskX * 32, MaskY * 32) ' draw pixel mask to emulate old pixel sizes
                Next
            Next

        End If
        PictureBox1.Image = DispBuff
        'PictureBox1.Refresh()

    End Sub

    Private Sub Mode4_Tick(sender As System.Object, e As System.EventArgs) Handles Mode4.Tick
        Dim cgacolor(3) As Byte, worker As Int32, halflife As Byte = 128, abits(7) As Byte
        Dim x1 As Int16, y1 As Int16
        For a = 32768 To 40767
            worker = VrMem(11, a)
            'Interpret attributes into bit flags
            For StepThroughBit = 7 To 0 Step -1
                If worker - halflife < 0 Then
                    abits(StepThroughBit) = 0
                Else
                    abits(StepThroughBit) = 1
                    worker -= halflife
                End If
                halflife >>= 1
            Next
            halflife = 128
            'interpret attributes into bit flags

            'assign colors
            If abits(7) = 1 Then cgacolor(0) += 2
            If abits(6) = 1 Then cgacolor(0) += 1
            If abits(5) = 1 Then cgacolor(1) += 2
            If abits(4) = 1 Then cgacolor(1) += 1
            If abits(3) = 1 Then cgacolor(2) += 2
            If abits(2) = 1 Then cgacolor(2) += 1
            If abits(1) = 1 Then cgacolor(3) += 2
            If abits(0) = 1 Then cgacolor(3) += 1
            'assign colors

            'interpret colors from byte into real colors from 16 color table
            For Cload = 0 To 3
                If cgacolor(Cload) = 3 Then cgacolor(0) = 7
                If cgacolor(Cload) = 2 Then cgacolor(0) = 4
                If cgacolor(Cload) = 1 Then cgacolor(0) = 2
            Next
            'interpret colors from byte into real colors from 16 color table

            Buffer_W.DrawImage(Pixel(0, cgacolor(0)), x1 * 2, y1)
            Buffer_W.DrawImage(Pixel(0, cgacolor(1)), (x1 + 1) * 2, y1)
            Buffer_W.DrawImage(Pixel(0, cgacolor(2)), (x1 + 2) * 2, y1)
            Buffer_W.DrawImage(Pixel(0, cgacolor(3)), (x1 + 3) * 2, y1)

            x1 += 4
            If x1 > 319 Then
                x1 = 0
                y1 += 4
            End If
            cgacolor(0) = 0
            cgacolor(1) = 0
            cgacolor(2) = 0
            cgacolor(3) = 0
        Next
        x1 = 0
        y1 = 2
        For b = 40768 To 48767
            worker = VrMem(11, b)
            'Interpret attributes into bit flags
            For StepThroughBit = 7 To 0 Step -1
                If worker - halflife < 0 Then
                    abits(StepThroughBit) = 0
                Else
                    abits(StepThroughBit) = 1
                    worker -= halflife
                End If
                halflife >>= 1
            Next
            halflife = 128
            'interpret attributes into bit flags

            'assign colors
            If abits(7) = 1 Then cgacolor(0) += 2
            If abits(6) = 1 Then cgacolor(0) += 1
            If abits(5) = 1 Then cgacolor(1) += 2
            If abits(4) = 1 Then cgacolor(1) += 1
            If abits(3) = 1 Then cgacolor(2) += 2
            If abits(2) = 1 Then cgacolor(2) += 1
            If abits(1) = 1 Then cgacolor(3) += 2
            If abits(0) = 1 Then cgacolor(3) += 1
            'assign colors

            'interpret colors from byte into real colors from 16 color table
            For Cload = 0 To 3
                Select Case cgacolor(Cload)
                    Case Is = 0
                        cgacolor(Cload) = 0
                    Case Is = 1
                        cgacolor(Cload) = 2
                    Case Is = 2
                        cgacolor(Cload) = 4
                    Case Is = 3
                        cgacolor(Cload) = 7
                    Case Else
                        cgacolor(Cload) = 0
                End Select
            Next
            'interpret colors from byte into real colors from 16 color table

            Buffer_W.DrawImage(Pixel(0, cgacolor(0)), x1 * 2, y1)
            Buffer_W.DrawImage(Pixel(0, cgacolor(1)), (x1 + 1) * 2, y1)
            Buffer_W.DrawImage(Pixel(0, cgacolor(2)), (x1 + 2) * 2, y1)
            Buffer_W.DrawImage(Pixel(0, cgacolor(3)), (x1 + 3) * 2, y1)
            x1 += 4
            If x1 > 319 Then
                x1 = 0
                y1 += 4
            End If
            cgacolor(0) = 0
            cgacolor(1) = 0
            cgacolor(2) = 0
            cgacolor(3) = 0

        Next

        ' For y = 1 To 199 pStep 2
        '  For x = 0 To 319
        ' Buffer_W.DrawImage(Pixel(0, 15 * Rnd()), x * 2, y * 2)
        '    Next
        ' Next
        'For y = 0 To 198 pStep 2
        ' For x = 0 To 319
        ' Buffer_W.DrawImage(Pixel(0, 15 * Rnd()), x * 2, y * 2)
        'Next
        'Next
        PictureBox1.Image = DispBuff
    End Sub

    Private Sub Mode3_Tick(sender As System.Object, e As System.EventArgs) Handles Mode3.Tick
        Dim KeyCode As Byte, Attributes As Byte, ReadStep As Int32
        Dim ABits(7) As Byte, worker As Single, Halflife As Byte = 128
        Dim ColorState As Byte, BColorState As Byte
        Static loopthru As Byte
        Static Blinker As Int16
        Blinker += 1
        If Blinker > 6 Then Blinker = 0
        OffSet = 32768 + CInt(CgaController.StartAddress) * 2
        For y = 1 To 25
            For x = 1 To 80
                'Read from memory
                Attributes = VrMem(11, OffSet + ReadStep)
                KeyCode = VrMem(11, OffSet + 1 + ReadStep)
                'If KeyCode = 255 Then KeyCode = 0

                ReadStep += 2
                'read from memory
                worker = Attributes
                'Interpret attributes into bit flags
                For StepThroughBit = 7 To 0 Step -1
                    If worker - Halflife < 0 Then
                        ABits(StepThroughBit) = 0
                    Else
                        ABits(StepThroughBit) = 1
                        worker -= Halflife
                    End If
                    Halflife >>= 1
                Next
                Halflife = 128
                'interpret attributes into bit flags

                'assign colors
                If ABits(7) = 1 Then BColorState += 8
                If ABits(6) = 1 Then BColorState += 4
                If ABits(5) = 1 Then BColorState += 2
                If ABits(4) = 1 Then BColorState += 1
                If ABits(3) = 1 Then ColorState += 8
                If ABits(2) = 1 Then ColorState += 4
                If ABits(1) = 1 Then ColorState += 2
                If ABits(0) = 1 Then ColorState += 1
                'assign colors

                If KeyCode > 127 Then KeyCode = 254
                If BColorState > 7 Then
                    If Blinker < 4 Then
                        Buffer_W.DrawImage(BGColors(BColorState - 8), (x - 1) * 8, (y - 1) * 16)
                        Buffer_W.DrawImage(AlphaGen(ColorState, KeyCode), (x - 1) * 8, (y - 1) * 16)
                    Else
                        Buffer_W.DrawImage(BGColors(BColorState - 8), (x - 1) * 8, (y - 1) * 16)
                    End If
                Else
                    Buffer_W.DrawImage(BGColors(BColorState), (x - 1) * 8, (y - 1) * 16)
                    Buffer_W.DrawImage(AlphaGen(ColorState, KeyCode), (x - 1) * 8, (y - 1) * 16)
                End If
                ColorState = 0
                BColorState = 0
                For clean = 0 To 7
                    ABits(clean) = 0
                Next
            Next
        Next
        If loopthru = 1 Then
            loopthru = 0
            For MaskY = 0 To 15
                For MaskX = 0 To 20
                    Buffer_W.DrawImage(ElectronGun(0), MaskX * 32, MaskY * 32) ' draw pixel mask to emulate old pixel sizes
                Next
            Next
        Else
            loopthru = 1
            For MaskY = 0 To 15
                For MaskX = 0 To 20
                    Buffer_W.DrawImage(ElectronGun(1), MaskX * 32, MaskY * 32) ' draw pixel mask to emulate old pixel sizes
                Next
            Next

        End If
        PictureBox1.Image = DispBuff
        'PictureBox1.Refresh()
    End Sub

    Private Sub Label2_Click(sender As Object, e As EventArgs) Handles Label2.Click

    End Sub
End Class
'keyboard buffer address &h0:0100-&h0:01ff (tentative)
