Imports System.IO 'for loading the ROM files
Imports System.Collections.Generic
Imports System.Math 'For advanced Math Functions
Imports System.ComponentModel
Imports System.Runtime.InteropServices
Public Class Form1
    Public Shared Current As Form1
    ' CROMWELL HOST REFIT BRICK 9C - the complete guest machine executes on one
    ' dedicated thread. UI access crosses MachineRuntime286's ownership gate.
    Private ReadOnly _machineRuntime As New MachineRuntime286(CPU0, SystemBus, MachineClock)
    Private ReadOnly _cpuPerfLockInBed As New Object()
    Private _videoPresentation As DiamondStealthPro928PresentationWorker
    Private _videoPresentationGenerationInBed As Long
    Private _displayedPresentationFrameInBed As Bitmap
    Private _powerOffDisplayImageInBed As Image
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
    Private _serialMousePreviousClipInBed As Rectangle
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
    Private _mountedPrimarySlaveIdeDriveId As Integer = -1
    Private _mountedSecondaryIdeDriveId As Integer = -1
    Private _mountedSecondarySlaveIdeDriveId As Integer = -1
    Private _sneakerNetIdeTargetInBed As Integer
    Private _sneakerNetIdeTargetSlaveInBed As Boolean
    Private _sneakerNetOpticalTargetInBed As Integer
    Private _sneakerNetOpticalTargetSlaveInBed As Boolean
    ' CROMWELL TECHNOLOGIES SNEAKER NET / FLOPPY BOX BRICK 1
    Private _floppyBox As FloppyBox
    Private _sneakerNetForm As SneakerNetForm
    Private _driveBayPanelInBed As Panel
    Private _floppyBayAInBed As PictureBox
    Private _floppyBayBInBed As PictureBox
    Private _cdRomBayInBed As PictureBox
    Private _cdRomBaySecondaryInBed As PictureBox
    Private _hardDiskBayInBed As PictureBox
    Private _hardDiskBaySecondaryInBed As PictureBox
    Private _driveBayToolTipInBed As ToolTip
    Private _driveBayPopupInBed As ContextMenuStrip
    Private _threeAndHalfDriveFaceInBed As Bitmap
    Private _fiveAndQuarterDriveFaceInBed As Bitmap
    Private _emptyDriveFaceInBed As Bitmap
    Private _cdRomDriveFaceInBed As Bitmap
    Private _mfmHardDiskDriveFaceInBed As Bitmap
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
    Private _pendingMotherboardClockOptionId As String
    Private _pendingMotherboardCacheKb As Integer?
    Private _ideHardwareConfiguration As IdeHardwareConfiguration
    Private _pendingIdeHardwareConfiguration As IdeHardwareConfiguration
    Private _isaCardConfiguration As IsaExpansionCardConfiguration
    Private _hardwarePopulationConfiguration As MachineHardwarePopulationConfiguration
    Private _pendingHardwarePopulationConfiguration As MachineHardwarePopulationConfiguration
    Private ReadOnly _motherboardRuntimeInBed As New MotherboardRuntimeManager()
    Private _installedCpuHardwareId As String = MachineHardwarePopulationConfiguration.NoneId
    Private _installedNumericCoprocessorHardwareId As String = MachineHardwarePopulationConfiguration.NoneId
    Private _installedVideoHardwareId As String = MachineHardwarePopulationConfiguration.NoneId
    Private _installedAudioHardwareId As String = MachineHardwarePopulationConfiguration.NoneId
    Private _installedNetworkHardwareId As String = MachineHardwarePopulationConfiguration.NoneId
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
    Private Const WM_INPUT As Integer = &HFF
    Private Const RID_INPUT As UInteger = &H10000003UI
    Private Const RIM_TYPEMOUSE As UInteger = 0UI
    Private Const MOUSE_MOVE_ABSOLUTE As UShort = &H1US

    <StructLayout(LayoutKind.Sequential)>
    Private Structure RawInputDeviceInBed
        Public UsagePage As UShort
        Public Usage As UShort
        Public Flags As UInteger
        Public Target As IntPtr
    End Structure

    <StructLayout(LayoutKind.Sequential)>
    Private Structure RawInputHeaderInBed
        Public Type As UInteger
        Public Size As UInteger
        Public Device As IntPtr
        Public WParam As IntPtr
    End Structure

    <StructLayout(LayoutKind.Explicit, Size:=24)>
    Private Structure RawMouseInBed
        <FieldOffset(0)> Public Flags As UShort
        <FieldOffset(4)> Public Buttons As UInteger
        <FieldOffset(8)> Public RawButtons As UInteger
        <FieldOffset(12)> Public LastX As Integer
        <FieldOffset(16)> Public LastY As Integer
        <FieldOffset(20)> Public ExtraInformation As UInteger
    End Structure

    <StructLayout(LayoutKind.Sequential)>
    Private Structure RawInputInBed
        Public Header As RawInputHeaderInBed
        Public Mouse As RawMouseInBed
    End Structure

    <DllImport("user32.dll", SetLastError:=True)>
    Private Shared Function RegisterRawInputDevices(
        devicesInBed As RawInputDeviceInBed(),
        deviceCountInBed As UInteger,
        structureSizeInBed As UInteger) As Boolean
    End Function

    <DllImport("user32.dll", SetLastError:=True)>
    Private Shared Function GetRawInputData(
        rawInputHandleInBed As IntPtr,
        commandInBed As UInteger,
        ByRef dataInBed As RawInputInBed,
        ByRef sizeInBed As UInteger,
        headerSizeInBed As UInteger) As UInteger
    End Function

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
        If m.Msg = WM_INPUT Then RouteRawSerialMouseInputInBed(m.LParam)
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

    Private Sub RegisterRawSerialMouseInputInBed()
        Dim devicesInBed As RawInputDeviceInBed() = {
            New RawInputDeviceInBed With {
                .UsagePage = &H1US,
                .Usage = &H2US,
                .Flags = 0UI,
                .Target = Handle
            }
        }
        If Not RegisterRawInputDevices(
            devicesInBed,
            CUInt(devicesInBed.Length),
            CUInt(Marshal.SizeOf(Of RawInputDeviceInBed)())) Then
            Throw New Win32Exception(Marshal.GetLastWin32Error(),
                                     "Unable to register raw host mouse input.")
        End If
    End Sub

    Private Sub RouteRawSerialMouseInputInBed(rawInputHandleInBed As IntPtr)
        If Not _serialMouseCapturedInBed OrElse _closingInBed OrElse Not _machinePoweredInBed Then Return

        Dim rawInBed As New RawInputInBed()
        Dim rawSizeInBed As UInteger = CUInt(Marshal.SizeOf(Of RawInputInBed)())
        Dim headerSizeInBed As UInteger = CUInt(Marshal.SizeOf(Of RawInputHeaderInBed)())
        Dim resultInBed As UInteger =
            GetRawInputData(rawInputHandleInBed,
                            RID_INPUT,
                            rawInBed,
                            rawSizeInBed,
                            headerSizeInBed)
        If resultInBed = UInteger.MaxValue OrElse rawInBed.Header.Type <> RIM_TYPEMOUSE Then Return
        If (rawInBed.Mouse.Flags And MOUSE_MOVE_ABSOLUTE) <> 0US Then Return
        If rawInBed.Mouse.LastX <> 0 OrElse rawInBed.Mouse.LastY <> 0 Then
            QueueSerialMouseHostMovementInBed(rawInBed.Mouse.LastX, rawInBed.Mouse.LastY)
        End If
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
        ' Captured motion is supplied exclusively by WM_INPUT. Absolute
        ' MouseMove coordinates are ignored so cursor confinement and Windows
        ' desktop acceleration cannot create guest motion or message feedback.
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
        Dim centerInBed As New Point(Math.Max(0, PictureBox1.ClientSize.Width \ 2),
                                     Math.Max(0, PictureBox1.ClientSize.Height \ 2))
        Dim centerScreenInBed As Point = PictureBox1.PointToScreen(centerInBed)
        _serialMousePreviousClipInBed = Cursor.Clip
        Cursor.Position = centerScreenInBed
        Cursor.Clip = New Rectangle(centerScreenInBed, New Size(1, 1))
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
        ClearSerialMouseHostMovementInBed()
        Cursor.Clip = _serialMousePreviousClipInBed
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
        If RuntimeDiagnostics.Enabled Then
            Threading.Interlocked.Increment(_serialMouseHostMoveMessagesInBed)
        End If
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
        If RuntimeDiagnostics.Enabled Then
            Threading.Interlocked.Increment(_serialMouseBoundaryTransfersInBed)
        End If
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

    Private Sub Form1_Load(sender As System.Object, e As System.EventArgs) Handles MyBase.Load
        Current = Me
        ' The designer image is the chassis artwork shown with power removed.
        ' Retain it before the first completed S3 frame replaces PictureBox1.Image.
        _powerOffDisplayImageInBed = PictureBox1.Image
        RegisterRawSerialMouseInputInBed()
        SystemLoop.Enabled = False
        ' Preserve the emulated CRT face geometry when the host window is resized.
        PictureBox1.SizeMode = PictureBoxSizeMode.Zoom

        ' Physical population and card straps are chassis state, not guest CMOS.
        InitializeHardwarePopulationConfigurationInBed()
        InitializeIsaExpansionCardConfigurationInBed()

        ' Bring up fixed motherboard fabric through the motherboard runtime
        ' factory, then populate removable sockets/connectors while power is off.
        _motherboardRuntimeInBed.Initialize(_hardwarePopulationConfiguration.MotherboardId)
        ApplyHardwarePopulationToMachineInBed()

        InitializeRamBanks()
        Dim romPath As String = Path.Combine(AppContext.BaseDirectory, "Firmware", "atbios.rom")
        MotherboardMemory.MirrorLegacyMemory = False
        MotherboardMemory.LoadSystemRom(File.ReadAllBytes(romPath))
        If CPU0 IsNot Nothing Then
            CPU0.MirrorLegacyTextCells = False
            CPU0.HostFirmwareInterrupts = False
        End If
        InitializeIdeHardwareConfigurationInBed()
        InitializeIdeDriveShelf()
        InitializeFloppyBox()
        CreateMediaMenu()
        InitializeDriveBayPanelInBed()
        InitializeSystemConfigurationInBed()
        BeginInvoke(New Action(AddressOf InitializeChassisPanel))

        ' Host CRT presentation is deliberately capped at 20 Hz. The S3 worker
        ' currently needs about 23 ms to rasterize a Windows frame; requesting
        ' one every 16 ms wastes host time on frames the UI cannot consume and
        ' starves the substantially more valuable guest CPU. Scan-out state and
        ' all guest-visible VGA timing remain on the emulated card.
        GPU.Interval = 50
        GPU.Enabled = False

        If CPU0 IsNot Nothing Then CPU0.Reset()
        ApplyFirmwareResetVectorSafetyGuardInBed()
        MachineClock.Reset()
        RestoreHostExecutionRateInBed()

        AddHandler _machineRuntime.SliceCompleted, AddressOf MachineRuntimeSliceCompletedInBed
        AddHandler _machineRuntime.RuntimeFaulted, AddressOf MachineRuntimeFaultedInBed

        ' Presentation requests are serviced by the machine thread at a slice
        ' boundary while it already owns the guest. The raster worker never races
        ' the machine gate and therefore cannot starve for seconds under timing debt.
        _machineRuntime.SetBoundaryService(
            Sub()
                DrainSerialMouseHostMovementAtBoundaryInBed()
                Dim presentationInBed As DiamondStealthPro928PresentationWorker = _videoPresentation
                Dim videoInBed As DiamondStealthPro928 = VideoCard
                If presentationInBed IsNot Nothing AndAlso videoInBed IsNot Nothing Then
                    presentationInBed.ServiceCaptureAtMachineBoundary(videoInBed)
                End If
            End Sub)
        ConfigureVideoPresentationForInstalledAdapterInBed()
        IsaBackplane.SetChassisPowered(True)
        _machineRuntime.Start()
        GPU.Enabled = (VideoCard IsNot Nothing)
    End Sub

    ' Host reset-vector guard retained intentionally.  If firmware is blank at
    ' FFFF:0000 after any host-initiated reset/power-on, do not let uninitialized
    ' bytes execute as guest code. Architectural HLT behavior remains untouched.
    Private Sub ApplyFirmwareResetVectorSafetyGuardInBed()
        If CPU0 Is Nothing Then Return
        If CPU0.ReadWord(&HFFFF0UI) = 0 AndAlso CPU0.ReadWord(&HFFFF2UI) = 0 Then
            CPU0.Halted = True
        End If
    End Sub

    Private Sub MachineRuntimeSliceCompletedInBed(consumedTStatesInBed As Long,
                                                   elapsedHostTicksInBed As Long,
                                                   executionHostTicksInBed As Long,
                                                   targetClockHzInBed As Long)
        ' Empty-socket board time is not CPU execution. Do not turn the clock
        ' accounting quantum into bogus MIPS/performance telemetry.
        If CPU0 Is Nothing Then Return
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
        DisposeDriveBayPopupInBed()
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
        IsaBackplane.SetChassisPowered(False)
        RemoveInstalledIsaCardsInBed()
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
        storageMenuInBed.DropDownItems.Add("IDE Controller", Nothing, Sub() OpenSystemConfigurationInBed("ide"))
        storageMenuInBed.DropDownItems.Add("Channel 1 Drive 0", Nothing, Sub() OpenSystemConfigurationInBed("ide-c1d0"))
        storageMenuInBed.DropDownItems.Add("Channel 1 Drive 1", Nothing, Sub() OpenSystemConfigurationInBed("ide-c1d1"))
        storageMenuInBed.DropDownItems.Add("Channel 2 Drive 0", Nothing, Sub() OpenSystemConfigurationInBed("ide-c2d0"))
        storageMenuInBed.DropDownItems.Add("Channel 2 Drive 1", Nothing, Sub() OpenSystemConfigurationInBed("ide-c2d1"))
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
            .Checked = ReadMachineInBed(Function() VideoCard IsNot Nothing AndAlso VideoCard.DiagnosticOverlayJumperClosed),
            .ToolTipText = "Checked = jumper CLOSED/installed; unchecked = OPEN/off"
        }
        AddHandler vgaDiagnosticJumper.CheckedChanged,
            Sub() WithMachineInBed(Sub()
                                        If VideoCard IsNot Nothing Then VideoCard.DiagnosticOverlayJumperClosed = vgaDiagnosticJumper.Checked
                                    End Sub)
        serviceJumpers.DropDownItems.Add(vgaDiagnosticJumper)
        machine.DropDownItems.Add(New ToolStripSeparator())
        machine.DropDownItems.Add(serviceJumpers)
        Dim diagnosticsMenu As New ToolStripMenuItem("Diagnostics")
        Dim dumpAllDiagnosticsMenuInBed As New ToolStripMenuItem("Dump all diagnostics")
        AddHandler dumpAllDiagnosticsMenuInBed.Click,
            Sub() DumpAllDiagnosticsInBed(dumpAllDiagnosticsMenuInBed)
        diagnosticsMenu.DropDownItems.Add(dumpAllDiagnosticsMenuInBed)
        diagnosticsMenu.DropDownItems.Add(New ToolStripSeparator())
        diagnosticsMenu.DropDownItems.Add("80286 hardware single-step tests...", Nothing, Sub() ShowSingleStep80286TestsInBed())
        diagnosticsMenu.DropDownItems.Add("CPU performance...", Nothing, Sub() ShowCpuPerformanceDiagnosticsInBed())
        diagnosticsMenu.DropDownItems.Add("CPU / PIC / PIT live state...", Nothing, Sub() ShowCpuInterruptLiveStateInBed())
        diagnosticsMenu.DropDownItems.Add(New ToolStripSeparator())
        diagnosticsMenu.DropDownItems.Add("Keyboard live state...", Nothing, Sub() ShowKeyboardLiveState())
        diagnosticsMenu.DropDownItems.Add("Show raw keyboard wire trace...", Nothing, Sub() ShowKeyboardWireTrace())
        diagnosticsMenu.DropDownItems.Add(New ToolStripSeparator())
        diagnosticsMenu.DropDownItems.Add("Begin/clear BIOS keyboard-ring trace", Nothing, Sub() WithMachineInBed(Sub()
                                                              If CPU0 IsNot Nothing Then CPU0.BeginDiagnosticBiosKeyboardTrace()
                                                          End Sub))
        diagnosticsMenu.DropDownItems.Add("Stop BIOS keyboard-ring trace", Nothing, Sub() WithMachineInBed(Sub()
                                                             If CPU0 IsNot Nothing Then CPU0.EndDiagnosticBiosKeyboardTrace()
                                                         End Sub))
        diagnosticsMenu.DropDownItems.Add("Show BIOS keyboard-ring trace...", Nothing, Sub() ShowBiosKeyboardRingTrace())
        diagnosticsMenu.DropDownItems.Add("BIOS keyboard-ring live state...", Nothing, Sub() ShowBiosKeyboardRingState())
        diagnosticsMenu.DropDownItems.Add("Clear raw keyboard wire trace", Nothing, Sub() WithMachineInBed(Sub() KeyboardController.ClearDiagnosticTrace()))
        diagnosticsMenu.DropDownItems.Add("Run keyboard hardware self-test...", Nothing, Sub() RunKeyboardHardwareSelfTest())
        diagnosticsMenu.DropDownItems.Add(New ToolStripSeparator())
        diagnosticsMenu.DropDownItems.Add("Begin/clear VGA mode trace", Nothing, Sub() WithMachineInBed(Sub()
                                                        If VideoCard IsNot Nothing Then VideoCard.BeginDiagnosticVgaTrace()
                                                    End Sub))
        diagnosticsMenu.DropDownItems.Add("Stop VGA mode trace", Nothing, Sub() WithMachineInBed(Sub()
                                                       If VideoCard IsNot Nothing Then VideoCard.EndDiagnosticVgaTrace()
                                                   End Sub))
        diagnosticsMenu.DropDownItems.Add("Show VGA mode trace...", Nothing, Sub() ShowVgaModeTrace())
        diagnosticsMenu.DropDownItems.Add("VGA live state...", Nothing, Sub() ShowVgaStateSnapshot())
        diagnosticsMenu.DropDownItems.Add("80287 flight recorder...", Nothing, Sub() ShowNumericCoprocessorFlightRecorderInBed())
        diagnosticsMenu.DropDownItems.Add(New ToolStripSeparator())
        diagnosticsMenu.DropDownItems.Add("Begin/clear important INTn trace", Nothing, Sub() WithMachineInBed(Sub()
                                                           If CPU0 IsNot Nothing Then CPU0.BeginDiagnosticImportantIntTrace()
                                                       End Sub))
        diagnosticsMenu.DropDownItems.Add("Stop important INTn trace", Nothing, Sub() WithMachineInBed(Sub()
                                                          If CPU0 IsNot Nothing Then CPU0.EndDiagnosticImportantIntTrace()
                                                      End Sub))
        diagnosticsMenu.DropDownItems.Add("Show important INTn trace...", Nothing, Sub() ShowImportantIntTrace())
        diagnosticsMenu.DropDownItems.Add(New ToolStripSeparator())
        diagnosticsMenu.DropDownItems.Add("Show QB EXEC forensic trace...", Nothing, Sub() ShowQbExecForensicTraceInBed())
        diagnosticsMenu.DropDownItems.Add("Clear/disarm QB EXEC forensic trace", Nothing, Sub() WithMachineInBed(Sub()
                                                        If CPU0 IsNot Nothing Then CPU0.ClearDiagnosticQbExecTrace()
                                                    End Sub))
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
        media.DropDownItems.Add("Create selected-model IDE shelf drive...", Nothing, Sub() CreateHardDisk())
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
                Dim textInBed As String = ReadMachineInBed(Function() If(SoundBlaster16 Is Nothing, "No Sound Blaster 16 installed.", SoundBlaster16.DiagnosticText()))
                ShowDumpableDiagnosticInBed("Sound Blaster 16", textInBed, "soundblaster16-diagnostic.txt", 980, 700)
            End Sub)
        rootInBed.DropDownItems.Add("NE2000 diagnostics", Nothing,
            Sub()
                Dim textInBed As String = ReadMachineInBed(Function() If(Ne2000 Is Nothing, "No NE2000 installed.", Ne2000.DiagnosticText()))
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
                    WithMachineInBed(Sub()
                                         If Ne2000 Is Nothing Then Throw New InvalidOperationException("No NE2000 is installed.")
                                         Ne2000.ConfigureUdpTunnel(localPortInBed, peerHostInBed, peerPortInBed)
                                     End Sub)
                Catch ex As Exception
                    MessageBox.Show(Me, ex.Message, "NE2000 cable", MessageBoxButtons.OK, MessageBoxIcon.Error)
                End Try
            End Sub)
        rootInBed.DropDownItems.Add("Disconnect NE2000 cable", Nothing,
            Sub() WithMachineInBed(Sub()
                                          If Ne2000 IsNot Nothing Then Ne2000.DisconnectUdpTunnel()
                                      End Sub))
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
                        WithMachineInBed(Sub()
                                             If Ne2000 Is Nothing Then Throw New InvalidOperationException("No NE2000 is installed.")
                                             Ne2000.SetPcapCapture(dialogInBed.FileName)
                                         End Sub)
                    Catch ex As Exception
                        MessageBox.Show(Me, ex.Message, "NE2000 capture", MessageBoxButtons.OK, MessageBoxIcon.Error)
                    End Try
                End Using
            End Sub)
        rootInBed.DropDownItems.Add("Stop NE2000 PCAP capture", Nothing,
            Sub() WithMachineInBed(Sub()
                                          If Ne2000 IsNot Nothing Then Ne2000.SetPcapCapture(Nothing)
                                      End Sub))

        AddHandler rootInBed.DropDownOpening,
            Sub()
                If SoundBlaster16 Is Nothing Then
                    sbStatusInBed.Text = "SB16: None — ISA slot empty"
                Else
                    sbStatusInBed.Text = "SB16: " & SoundBlaster16.BasePort.ToString("X3") & "h / IRQ" & SoundBlaster16.Irq.ToString() &
                                         " / DMA" & SoundBlaster16.Dma8Channel.ToString() & "/" & SoundBlaster16.Dma16Channel.ToString()
                End If
                If Ne2000 Is Nothing Then
                    neStatusInBed.Text = "NE2000: None — ISA slot empty"
                Else
                    neStatusInBed.Text = "NE2000: " & Ne2000.BasePort.ToString("X3") & "h / IRQ" & Ne2000.Irq.ToString() & " / " & Ne2000.MacAddressText
                End If
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

    Private Function DescribeIdeSlotInBed(channelInBed As Integer, slaveInBed As Boolean) As String
        Dim controllerInBed As IdeController = IdeControllerForChannelInBed(channelInBed)
        Dim slotNameInBed As String = IdeSlotDisplayNameInBed(channelInBed, slaveInBed)
        Dim kindInBed As IdeDeviceKind = controllerInBed.GetDeviceKind(slaveInBed)

        Select Case kindInBed
            Case IdeDeviceKind.Empty
                Return slotNameInBed & " — empty IDE socket"

            Case IdeDeviceKind.AtapiCdRom
                Return slotNameInBed & " — ATAPI CD-ROM — " &
                       If(controllerInBed.IsCdRomMounted(slaveInBed), "ISO media inserted", "tray empty")

            Case IdeDeviceKind.AtaHardDisk
                If _ideDriveShelf Is Nothing Then Return slotNameInBed & " — ATA hard disk — IDE shelf unavailable"
                Dim configuredInBed As Integer = _ideDriveShelf.GetConfiguredDriveId(channelInBed, slaveInBed)
                Dim identityInBed As HardDiskIdentity = controllerInBed.GetHardDiskIdentity(slaveInBed)
                Dim modelTextInBed As String = If(identityInBed Is Nothing, String.Empty, " — " & identityInBed.Model)
                If configuredInBed < 0 Then
                    Return slotNameInBed & " — ATA hard disk — no shelf image attached" & modelTextInBed
                End If
                Try
                    Dim entryInBed As IdeDriveShelfEntry = _ideDriveShelf.FindById(configuredInBed)
                    If entryInBed Is Nothing Then
                        Return slotNameInBed & ": shelf #" & configuredInBed.ToString() & " unavailable"
                    End If
                    Return slotNameInBed & ": shelf #" & entryInBed.Id.ToString() & " — " & entryInBed.Label &
                           modelTextInBed &
                           If(MountedIdeDriveIdInBed(channelInBed, slaveInBed) = entryInBed.Id,
                              " — attached",
                              " — configured, unavailable")
                Catch ex As Exception
                    Return slotNameInBed & " status unavailable — " & ex.Message
                End Try

            Case Else
                Return slotNameInBed & " — unknown IDE device"
        End Select
    End Function

    Private Function GetSystemConfigurationSnapshotUnlockedInBed() As SystemConfigurationSnapshot
        Dim snapshotInBed As New SystemConfigurationSnapshot()
        Dim currentPopulationInBed As MachineHardwarePopulationConfiguration =
            If(_hardwarePopulationConfiguration, New MachineHardwarePopulationConfiguration(AppContext.BaseDirectory))
        Dim effectivePopulationInBed As MachineHardwarePopulationConfiguration = EffectiveHardwarePopulationInBed()

        Dim motherboardProfileInBed As MotherboardProfile = MotherboardCatalog.Require(effectivePopulationInBed.MotherboardId)
        snapshotInBed.Chassis = "AT chassis profile not yet modeled"
        snapshotInBed.PowerSupply = "Power-supply profile not yet modeled"
        snapshotInBed.MotherboardHardwareId = motherboardProfileInBed.Id
        snapshotInBed.Motherboard = motherboardProfileInBed.DisplayName
        Dim selectedRuntimeInBed As IMotherboardRuntimeBackplane = MotherboardRuntimeFactory.Create(motherboardProfileInBed.Id)
        snapshotInBed.MotherboardRuntimeStatus = If(selectedRuntimeInBed.FixedFabricAvailable,
                                                    selectedRuntimeInBed.RuntimeDisplayName,
                                                    selectedRuntimeInBed.RuntimeDisplayName & " — activation gated; no substitute chipset will be fabricated")
        snapshotInBed.CpuSocket = motherboardProfileInBed.CpuSocketDisplayName
        snapshotInBed.ClockGenerator = motherboardProfileInBed.ClockGeneratorDisplayName
        snapshotInBed.MemoryTopology = motherboardProfileInBed.MemoryTopologyDisplayName
        snapshotInBed.CacheTopology = motherboardProfileInBed.CacheTopologyDisplayName
        Dim expansionTextInBed As New List(Of String)()
        For Each slotInBed As MotherboardExpansionSlotProfile In motherboardProfileInBed.ExpansionSlots()
            expansionTextInBed.Add(slotInBed.DisplayName & If(slotInBed.BusMasterCapable, " — bus-master capable", String.Empty))
        Next
        snapshotInBed.ExpansionTopology = String.Join(Environment.NewLine, expansionTextInBed)
        Dim boardContextInBed As MotherboardContextConfiguration = LoadMotherboardContextInBed(motherboardProfileInBed.Id)
        Dim effectiveClockIdInBed As String = If(String.IsNullOrWhiteSpace(_pendingMotherboardClockOptionId), boardContextInBed.ClockOptionId, _pendingMotherboardClockOptionId)
        Dim effectiveClockInBed As MotherboardClockOption = motherboardProfileInBed.FindClockOption(effectiveClockIdInBed)
        If effectiveClockInBed Is Nothing Then effectiveClockInBed = motherboardProfileInBed.FindClockOption(motherboardProfileInBed.DefaultClockOptionId)
        snapshotInBed.MotherboardClockOptionId = If(effectiveClockInBed Is Nothing, String.Empty, effectiveClockInBed.Id)
        snapshotInBed.MotherboardClockSetting = If(effectiveClockInBed Is Nothing, "No clock option defined", effectiveClockInBed.DisplayName)
        snapshotInBed.MotherboardCacheKilobytes = If(_pendingMotherboardCacheKb.HasValue, _pendingMotherboardCacheKb.Value, boardContextInBed.CacheKilobytes)
        snapshotInBed.MotherboardCmos = motherboardProfileInBed.CmosStorageBytes.ToString() & " bytes battery-backed CMOS — " & motherboardProfileInBed.CmosNvramRelativePath
        Dim firmwareFullPathInBed As String = Path.Combine(AppContext.BaseDirectory, motherboardProfileInBed.FirmwareImageRelativePath.Replace("\"c, Path.DirectorySeparatorChar))
        snapshotInBed.MotherboardFirmwareImage = motherboardProfileInBed.FirmwareImageRelativePath & If(File.Exists(firmwareFullPathInBed), " — present", " — image required / not present in this checkpoint")
        Dim installedIsa16CountInBed As Integer = CountInstalledIsa16CardsInBed(effectivePopulationInBed)
        Dim isaFitReasonInBed As String = String.Empty
        If motherboardProfileInBed.ValidateIsa16CardCount(installedIsa16CountInBed, isaFitReasonInBed) Then
            snapshotInBed.ExpansionCompatibility = installedIsa16CountInBed.ToString() & " installed 16-bit ISA card(s); " & motherboardProfileInBed.CompatibleIsaSlotCount(16).ToString() & " compatible 16-bit connector(s)"
        Else
            snapshotInBed.ExpansionCompatibility = "CONFLICT — " & isaFitReasonInBed
        End If
        Dim currentMotherboardProfileInBed As MotherboardProfile = MotherboardCatalog.Require(currentPopulationInBed.MotherboardId)
        Dim liveRuntimeInBed As IMotherboardRuntimeBackplane = _motherboardRuntimeInBed.Current
        Dim liveChipsetSuffixInBed As String = If(String.Equals(currentMotherboardProfileInBed.Id, motherboardProfileInBed.Id, StringComparison.OrdinalIgnoreCase),
                                                  " (" & If(liveRuntimeInBed Is Nothing, Chipset.GetType().Name, liveRuntimeInBed.RuntimeDisplayName) & ")",
                                                  " — pending selection; live board remains " & currentMotherboardProfileInBed.DisplayName)
        snapshotInBed.Chipset = motherboardProfileInBed.ChipsetDisplayName & liveChipsetSuffixInBed
        snapshotInBed.ChipsetCpuBus = motherboardProfileInBed.ChipsetCpuBus
        snapshotInBed.ChipsetMemory = motherboardProfileInBed.ChipsetMemory
        snapshotInBed.ChipsetPeripheral = motherboardProfileInBed.ChipsetPeripheral
        snapshotInBed.ChipsetBuffer = motherboardProfileInBed.ChipsetBuffer
        snapshotInBed.ChipsetConfigurationPorts = motherboardProfileInBed.ChipsetConfigurationPorts
        snapshotInBed.ChipsetTiming = If(String.Equals(currentMotherboardProfileInBed.Id, motherboardProfileInBed.Id, StringComparison.OrdinalIgnoreCase),
                                            If(liveRuntimeInBed Is Nothing, Chipset.TimingDiagnosticText(), liveRuntimeInBed.DiagnosticText()),
                                            "Pending motherboard selection — live board remains asserted until the cold replacement is committed" & Environment.NewLine & _motherboardRuntimeInBed.DiagnosticText())
        snapshotInBed.Bios = motherboardProfileInBed.BiosDisplayName

        snapshotInBed.CpuHardwareId = effectivePopulationInBed.CpuId
        snapshotInBed.NumericCoprocessorHardwareId = effectivePopulationInBed.NumericCoprocessorId
        snapshotInBed.VideoHardwareId = effectivePopulationInBed.VideoCardId
        snapshotInBed.AudioHardwareId = effectivePopulationInBed.AudioCardId
        snapshotInBed.NetworkHardwareId = effectivePopulationInBed.NetworkCardId

        snapshotInBed.Cpu = If(CPU0 Is Nothing,
                               ProcessorPackageCatalog.DisplayName(MachineHardwarePopulationConfiguration.NoneId),
                               ProcessorPackageCatalog.DisplayName(currentPopulationInBed.CpuId))
        snapshotInBed.CpuClock = If(CPU0 Is Nothing,
                                    "No CPU clocked — socket empty",
                                    (MachineClock.CpuClockHz \ 1000000L).ToString() & " MHz" & If(MachineClock.TurboEnabled, " — Turbo", " — Normal"))
        snapshotInBed.HostExecutionRatePercent = MachineClock.HostExecutionRatePercent
        snapshotInBed.HostExecutionRate = If(MachineClock.HostExecutionRatePercent = 0,
                                                "Unlimited — host limited",
                                                (CDbl(MachineClock.HostExecutionRatePercent) / 100.0R).ToString("0.##") & "× real time")
        snapshotInBed.NumericCoprocessor = If(NumericCoprocessor0 Is Nothing,
                                              NumericCoprocessorPackageCatalog.DisplayName(MachineHardwarePopulationConfiguration.NoneId),
                                              NumericCoprocessorPackageCatalog.DisplayName(currentPopulationInBed.NumericCoprocessorId))
        snapshotInBed.InstalledMemoryMb = If(_ramBanks Is Nothing, 0, _ramBanks.InstalledMemoryMb)
        snapshotInBed.Memory = snapshotInBed.InstalledMemoryMb.ToString() & " MB"
        snapshotInBed.PendingMemoryMb = _pendingMemoryMb
        snapshotInBed.Video = If(VideoCard Is Nothing,
                                 IsaCardCatalog286.VideoDisplayName(MachineHardwarePopulationConfiguration.NoneId),
                                 IsaCardCatalog286.VideoDisplayName(currentPopulationInBed.VideoCardId))
        snapshotInBed.VideoRom = If(VideoCard Is Nothing,
                                    "None — no video option ROM installed",
                                    "Diamond Stealth Pro option ROM — Firmware\stealthpro.rom")
        If SoundBlaster16 Is Nothing Then
            snapshotInBed.ExpansionAudio = IsaCardCatalog286.AudioDisplayName(MachineHardwarePopulationConfiguration.NoneId)
        Else
            snapshotInBed.ExpansionAudio = "Creative Sound Blaster 16 — " & SoundBlaster16.BasePort.ToString("X3") & "h — IRQ" & SoundBlaster16.Irq.ToString() &
                                           " — DMA" & SoundBlaster16.Dma8Channel.ToString() & "/DMA" & SoundBlaster16.Dma16Channel.ToString() & " — OPL3"
        End If
        snapshotInBed.Speaker = "Motherboard PC speaker — PIT channel 2 / port 61h"
        snapshotInBed.Network = If(Ne2000 Is Nothing,
                                   IsaCardCatalog286.NetworkDisplayName(MachineHardwarePopulationConfiguration.NoneId),
                                   "Novell NE2000-compatible — " & Ne2000.BasePort.ToString("X3") & "h — IRQ" & Ne2000.Irq.ToString() & " — " & Ne2000.MacAddressText)
        snapshotInBed.FloppyController = "NEC uPD765A / Intel 8272-compatible FDC"
        snapshotInBed.FloppyDriveA = "AT floppy drive unit A — swappable drive profile"
        snapshotInBed.FloppyDriveB = "AT floppy drive unit B — swappable drive profile"
        snapshotInBed.FloppyMediaA = FloppyController.GetAttachmentStatus(0)
        snapshotInBed.FloppyMediaB = FloppyController.GetAttachmentStatus(1)
        snapshotInBed.FloppyMediaSourceIdA = If(String.IsNullOrWhiteSpace(FloppyController.GetMediaSourceId(0)), "empty", FloppyController.GetMediaSourceId(0))
        snapshotInBed.FloppyMediaSourceIdB = If(String.IsNullOrWhiteSpace(FloppyController.GetMediaSourceId(1)), "empty", FloppyController.GetMediaSourceId(1))
        snapshotInBed.HardDiskController =
            "Dual-channel ISA ATA/IDE — primary 1F0h-1F7h + 3F6h, IRQ14; secondary 170h-177h + 376h, IRQ15"
        Dim effectiveIdeInBed As IdeHardwareConfiguration = EffectiveIdeHardwareConfigurationInBed()
        snapshotInBed.IdePrimaryMasterHardwareId = IdeHardwareConfiguration.KindId(effectiveIdeInBed.PrimaryMasterKind)
        snapshotInBed.IdePrimarySlaveHardwareId = IdeHardwareConfiguration.KindId(effectiveIdeInBed.PrimarySlaveKind)
        snapshotInBed.IdeSecondaryMasterHardwareId = IdeHardwareConfiguration.KindId(effectiveIdeInBed.SecondaryMasterKind)
        snapshotInBed.IdeSecondarySlaveHardwareId = IdeHardwareConfiguration.KindId(effectiveIdeInBed.SecondarySlaveKind)
        snapshotInBed.IdePrimaryMasterModelId = effectiveIdeInBed.PrimaryMasterModelId
        snapshotInBed.IdePrimarySlaveModelId = effectiveIdeInBed.PrimarySlaveModelId
        snapshotInBed.IdeSecondaryMasterModelId = effectiveIdeInBed.SecondaryMasterModelId
        snapshotInBed.IdeSecondarySlaveModelId = effectiveIdeInBed.SecondarySlaveModelId
        snapshotInBed.HardDisk0 = DescribeIdeSlotInBed(0, False)
        snapshotInBed.HardDisk1 = DescribeIdeSlotInBed(1, False)
        snapshotInBed.Optical = DescribeIdeSlotInBed(0, True)
        snapshotInBed.Optical1 = DescribeIdeSlotInBed(1, True)
        snapshotInBed.Keyboard = "IBM AT 101-key keyboard + 8042 controller"
        snapshotInBed.Com1 = "NS16550A-compatible UART — 3F8h — IRQ4 — Microsoft serial mouse attached"
        snapshotInBed.Com2 = "NS16550A-compatible UART — 2F8h — IRQ3 — DB-9 connector"
        snapshotInBed.Lpt1 = "IBM-compatible SPP / Centronics — 378h — IRQ7"
        snapshotInBed.Lpt2 = "IBM-compatible SPP / Centronics — 278h — IRQ5"
        If SoundBlaster16 Is Nothing Then
            snapshotInBed.GamePort = "None — no expansion game port installed"
            snapshotInBed.Midi = "None — no MPU-401 interface installed"
        Else
            snapshotInBed.GamePort = If(SoundBlaster16.GamePortEnabled,
                                        "Sound Blaster 16 game port — 201h — no joystick attached",
                                        "Sound Blaster 16 game port — disabled by card jumper")
            snapshotInBed.Midi = "MPU-401 UART-compatible — " & SoundBlaster16.MpuBasePort.ToString("X3") & "h (Sound Blaster 16)"
        End If
        snapshotInBed.PendingHardwareSummary = GetPendingHardwareSummaryInBed()

        Dim resourcesInBed As New System.Text.StringBuilder()
        resourcesInBed.AppendLine("NEAT config: 22h/23h")
        resourcesInBed.AppendLine("DMA: 00h-0Fh, 80h-8Fh, C0h-DEh")
        resourcesInBed.AppendLine("PIC: 20h/21h + A0h/A1h")
        resourcesInBed.AppendLine("PIT / speaker gate: 40h-43h + 61h")
        resourcesInBed.AppendLine("RTC / CMOS: 70h/71h, IRQ8")
        resourcesInBed.AppendLine("Keyboard: 60h/64h, IRQ1")
        resourcesInBed.AppendLine("Floppy: 3F2h-3F5h + 3F7h, IRQ6, DMA2")
        resourcesInBed.AppendLine("Primary IDE: 1F0h-1F7h + 3F6h, IRQ14")
        resourcesInBed.AppendLine("Secondary IDE: 170h-177h + 376h, IRQ15")
        If NumericCoprocessor0 IsNot Nothing Then resourcesInBed.AppendLine("80287 numeric processor extension: IRQ13 error path")
        If VideoCard IsNot Nothing Then resourcesInBed.AppendLine("VGA/S3: legacy VGA I/O + A0000h-BFFFFh, option ROM C0000h-C7FFFh")
        resourcesInBed.AppendLine("COM1: 3F8h-3FFh, IRQ4; COM2: 2F8h-2FFh, IRQ3")
        resourcesInBed.AppendLine("LPT1: 378h-37Ah, IRQ7; LPT2: 278h-27Ah, IRQ5")
        If SoundBlaster16 IsNot Nothing Then
            resourcesInBed.AppendLine("Sound Blaster 16: " & SoundBlaster16.BasePort.ToString("X3") & "h-" & (CInt(SoundBlaster16.BasePort) + &HF).ToString("X3") & "h + 388h-38Bh, IRQ" & SoundBlaster16.Irq.ToString() & ", DMA" & SoundBlaster16.Dma8Channel.ToString() & "/DMA" & SoundBlaster16.Dma16Channel.ToString())
            resourcesInBed.AppendLine("MPU-401 UART: " & SoundBlaster16.MpuBasePort.ToString("X3") & "h-" & (CInt(SoundBlaster16.MpuBasePort) + 1).ToString("X3") & "h (SB16); game port: " & If(SoundBlaster16.GamePortEnabled, "201h", "disabled"))
        End If
        If Ne2000 IsNot Nothing Then resourcesInBed.Append("NE2000: " & Ne2000.BasePort.ToString("X3") & "h-" & (CInt(Ne2000.BasePort) + &H1F).ToString("X3") & "h, IRQ" & Ne2000.Irq.ToString() & ", 16 KiB packet RAM")
        snapshotInBed.ResourceSummary = resourcesInBed.ToString().TrimEnd()
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
        If _ramBanks Is Nothing Then Return
        Dim effectivePopulationInBed As MachineHardwarePopulationConfiguration = EffectiveHardwarePopulationInBed()
        Dim profileInBed As MotherboardProfile = MotherboardCatalog.Require(effectivePopulationInBed.MotherboardId)
        If Not profileInBed.SupportsMemoryMegabytes(megabytesInBed) Then Return

        Dim currentBoardIdInBed As String = If(_hardwarePopulationConfiguration Is Nothing,
                                                MotherboardCatalog.CtCs8221NeatAt286Id,
                                                _hardwarePopulationConfiguration.MotherboardId)
        If Not String.Equals(effectivePopulationInBed.MotherboardId, currentBoardIdInBed, StringComparison.OrdinalIgnoreCase) Then
            ' A motherboard on the service bench owns its own SIMMs even if the
            ' selected capacity happens to equal the live board's capacity.
            _pendingMemoryMb = megabytesInBed
        ElseIf megabytesInBed = _ramBanks.InstalledMemoryMb Then
            _pendingMemoryMb = Nothing
        Else
            _pendingMemoryMb = megabytesInBed
        End If
        If _systemConfigurationDrawer IsNot Nothing Then _systemConfigurationDrawer.RefreshFromMachine()
    End Sub

    Friend Sub RevertPendingSystemConfigurationInBed()
        _pendingMemoryMb = Nothing
        _pendingMotherboardClockOptionId = Nothing
        _pendingMotherboardCacheKb = Nothing
        _pendingIdeHardwareConfiguration = Nothing
        _pendingHardwarePopulationConfiguration = Nothing
        _pendingSoundBlaster16Jumpers = Nothing
        _pendingNe2000Jumpers = Nothing
        If _systemConfigurationDrawer IsNot Nothing Then _systemConfigurationDrawer.RefreshFromMachine()
    End Sub

    Friend Sub OpenSoundBlasterJumperPanelInBed()
        If _isaCardConfiguration Is Nothing Then Return
        Dim sbInBed As SoundBlaster16JumperSettings = EffectiveSoundBlaster16JumpersInBed()
        Dim neInBed As Ne2000JumperSettings = EffectiveNe2000JumpersInBed()
        Dim populationInBed As MachineHardwarePopulationConfiguration = EffectiveHardwarePopulationInBed()
        Using panelInBed As New IsaCardJumperDialog(IsaJumperCardKind.SoundBlaster16,
                                                    sbInBed,
                                                    neInBed,
                                                    soundBlasterInstalledInBed:=Not String.Equals(populationInBed.AudioCardId, MachineHardwarePopulationConfiguration.NoneId, StringComparison.OrdinalIgnoreCase),
                                                    ne2000InstalledInBed:=Not String.Equals(populationInBed.NetworkCardId, MachineHardwarePopulationConfiguration.NoneId, StringComparison.OrdinalIgnoreCase))
            If panelInBed.ShowDialog(Me) = DialogResult.OK Then
                StageSoundBlaster16JumpersInBed(panelInBed.SoundBlasterSettings)
            End If
        End Using
    End Sub

    Friend Sub OpenNe2000JumperPanelInBed()
        If _isaCardConfiguration Is Nothing Then Return
        Dim sbInBed As SoundBlaster16JumperSettings = EffectiveSoundBlaster16JumpersInBed()
        Dim neInBed As Ne2000JumperSettings = EffectiveNe2000JumpersInBed()
        Dim populationInBed As MachineHardwarePopulationConfiguration = EffectiveHardwarePopulationInBed()
        Using panelInBed As New IsaCardJumperDialog(IsaJumperCardKind.Ne2000,
                                                    sbInBed,
                                                    neInBed,
                                                    soundBlasterInstalledInBed:=Not String.Equals(populationInBed.AudioCardId, MachineHardwarePopulationConfiguration.NoneId, StringComparison.OrdinalIgnoreCase),
                                                    ne2000InstalledInBed:=Not String.Equals(populationInBed.NetworkCardId, MachineHardwarePopulationConfiguration.NoneId, StringComparison.OrdinalIgnoreCase))
            If panelInBed.ShowDialog(Me) = DialogResult.OK Then
                StageNe2000JumpersInBed(panelInBed.Ne2000Settings)
            End If
        End Using
    End Sub

    Private Sub StageSoundBlaster16JumpersInBed(settingsInBed As SoundBlaster16JumperSettings)
        If settingsInBed Is Nothing OrElse _isaCardConfiguration Is Nothing Then Return
        Dim populationInBed As MachineHardwarePopulationConfiguration = EffectiveHardwarePopulationInBed()
        Dim conflictsInBed As List(Of String) = IsaResourceConflictDetector.Validate(settingsInBed, EffectiveNe2000JumpersInBed(),
                                                                                   Not String.Equals(populationInBed.AudioCardId, MachineHardwarePopulationConfiguration.NoneId, StringComparison.OrdinalIgnoreCase),
                                                                                   Not String.Equals(populationInBed.NetworkCardId, MachineHardwarePopulationConfiguration.NoneId, StringComparison.OrdinalIgnoreCase),
                                                                                   Not String.Equals(populationInBed.NumericCoprocessorId, MachineHardwarePopulationConfiguration.NoneId, StringComparison.OrdinalIgnoreCase))
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
        Dim populationInBed As MachineHardwarePopulationConfiguration = EffectiveHardwarePopulationInBed()
        Dim conflictsInBed As List(Of String) = IsaResourceConflictDetector.Validate(EffectiveSoundBlaster16JumpersInBed(), settingsInBed,
                                                                                   Not String.Equals(populationInBed.AudioCardId, MachineHardwarePopulationConfiguration.NoneId, StringComparison.OrdinalIgnoreCase),
                                                                                   Not String.Equals(populationInBed.NetworkCardId, MachineHardwarePopulationConfiguration.NoneId, StringComparison.OrdinalIgnoreCase),
                                                                                   Not String.Equals(populationInBed.NumericCoprocessorId, MachineHardwarePopulationConfiguration.NoneId, StringComparison.OrdinalIgnoreCase))
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
        Dim aWavetableInBed As WavetableAttachmentSettings = If(aInBed.Wavetable, New WavetableAttachmentSettings())
        Dim bWavetableInBed As WavetableAttachmentSettings = If(bInBed.Wavetable, New WavetableAttachmentSettings())
        Return aInBed.BasePort = bInBed.BasePort AndAlso aInBed.Irq = bInBed.Irq AndAlso
               aInBed.Dma8 = bInBed.Dma8 AndAlso aInBed.Dma16 = bInBed.Dma16 AndAlso
               aInBed.MpuPort = bInBed.MpuPort AndAlso aInBed.GamePortEnabled = bInBed.GamePortEnabled AndAlso
               aWavetableInBed.SameAs(bWavetableInBed)
    End Function

    Private Shared Function SameNe2000JumpersInBed(aInBed As Ne2000JumperSettings, bInBed As Ne2000JumperSettings) As Boolean
        If aInBed Is Nothing OrElse bInBed Is Nothing Then Return False
        Return aInBed.BasePort = bInBed.BasePort AndAlso aInBed.Irq = bInBed.Irq
    End Function

    Private Function GetPendingHardwareSummaryInBed() As String
        Dim pendingInBed As New List(Of String)()
        If _pendingMemoryMb.HasValue Then pendingInBed.Add("memory " & _pendingMemoryMb.Value.ToString() & " MB")
        If Not String.IsNullOrWhiteSpace(_pendingMotherboardClockOptionId) Then pendingInBed.Add("board clock " & _pendingMotherboardClockOptionId)
        If _pendingMotherboardCacheKb.HasValue Then pendingInBed.Add("L2 cache " & _pendingMotherboardCacheKb.Value.ToString() & " KB")
        If _pendingIdeHardwareConfiguration IsNot Nothing Then pendingInBed.Add("IDE " & _pendingIdeHardwareConfiguration.Summary())
        If _pendingHardwarePopulationConfiguration IsNot Nothing Then pendingInBed.Add("population " & _pendingHardwarePopulationConfiguration.Summary())
        If _pendingSoundBlaster16Jumpers IsNot Nothing Then pendingInBed.Add("SB16 " & _pendingSoundBlaster16Jumpers.Summary())
        If _pendingNe2000Jumpers IsNot Nothing Then pendingInBed.Add("NE2000 " & _pendingNe2000Jumpers.Summary())
        Return String.Join("; ", pendingInBed)
    End Function

    Friend Sub ApplyPendingSystemConfigurationAndPowerCycleInBed()
        Dim effectivePopulationForFitInBed As MachineHardwarePopulationConfiguration = EffectiveHardwarePopulationInBed()
        Dim fitProfileInBed As MotherboardProfile = MotherboardCatalog.Require(effectivePopulationForFitInBed.MotherboardId)
        Dim fitReasonInBed As String = String.Empty
        If Not fitProfileInBed.ValidateIsa16CardCount(CountInstalledIsa16CardsInBed(effectivePopulationForFitInBed), fitReasonInBed) Then
            MessageBox.Show(Me, fitReasonInBed & " The running machine has not been altered.", "Motherboard slot conflict", MessageBoxButtons.OK, MessageBoxIcon.Warning)
            Return
        End If
        If _pendingHardwarePopulationConfiguration IsNot Nothing Then
            Dim runtimeReasonInBed As String = String.Empty
            If Not _motherboardRuntimeInBed.CanActivate(_pendingHardwarePopulationConfiguration.MotherboardId,
                                                       _pendingHardwarePopulationConfiguration.CpuId,
                                                       runtimeReasonInBed) Then
                MessageBox.Show(Me,
                                runtimeReasonInBed & " The running machine has not been altered.",
                                "Motherboard runtime pending",
                                MessageBoxButtons.OK,
                                MessageBoxIcon.Information)
                Return
            End If
        End If
        Dim cardChangesInBed As Boolean =
            _pendingSoundBlaster16Jumpers IsNot Nothing OrElse _pendingNe2000Jumpers IsNot Nothing
        Dim ideChangesInBed As Boolean = _pendingIdeHardwareConfiguration IsNot Nothing
        Dim populationChangesInBed As Boolean = _pendingHardwarePopulationConfiguration IsNot Nothing
        Dim motherboardSettingChangesInBed As Boolean = Not String.IsNullOrWhiteSpace(_pendingMotherboardClockOptionId) OrElse _pendingMotherboardCacheKb.HasValue
        If Not _pendingMemoryMb.HasValue AndAlso Not motherboardSettingChangesInBed AndAlso Not cardChangesInBed AndAlso Not ideChangesInBed AndAlso Not populationChangesInBed Then Return

        Dim wasPoweredInBed As Boolean = _machinePoweredInBed
        If wasPoweredInBed Then SetMachinePowerInBed(False)

        If _pendingMemoryMb.HasValue Then
            Dim memoryMbInBed As Integer = _pendingMemoryMb.Value
            _pendingMemoryMb = Nothing
            Dim boardForMemoryInBed As String = EffectiveHardwarePopulationInBed().MotherboardId
            Dim memoryProfileInBed As MotherboardProfile = MotherboardCatalog.Require(boardForMemoryInBed)
            If memoryProfileInBed.SupportsMemoryMegabytes(memoryMbInBed) Then
                Dim boardContextForMemoryInBed As MotherboardContextConfiguration = LoadMotherboardContextInBed(boardForMemoryInBed)
                boardContextForMemoryInBed.MemoryMegabytes = memoryMbInBed
                boardContextForMemoryInBed.SaveConfiguration()
                If String.Equals(boardForMemoryInBed, MotherboardCatalog.CtCs8221NeatAt286Id, StringComparison.OrdinalIgnoreCase) AndAlso _ramBanks IsNot Nothing AndAlso RamBankConfiguration.IsSupported(memoryMbInBed) Then
                    _ramBanks.InstalledMemoryMb = memoryMbInBed
                    _ramBanks.SaveConfiguration()
                End If
            End If
        End If

        If motherboardSettingChangesInBed Then
            Dim boardForSettingsInBed As String = EffectiveHardwarePopulationInBed().MotherboardId
            Dim settingsProfileInBed As MotherboardProfile = MotherboardCatalog.Require(boardForSettingsInBed)
            Dim boardContextForSettingsInBed As MotherboardContextConfiguration = LoadMotherboardContextInBed(boardForSettingsInBed)
            If Not String.IsNullOrWhiteSpace(_pendingMotherboardClockOptionId) AndAlso settingsProfileInBed.FindClockOption(_pendingMotherboardClockOptionId) IsNot Nothing Then
                boardContextForSettingsInBed.ClockOptionId = _pendingMotherboardClockOptionId
            End If
            If _pendingMotherboardCacheKb.HasValue AndAlso settingsProfileInBed.SupportsCacheKilobytes(_pendingMotherboardCacheKb.Value) Then
                boardContextForSettingsInBed.CacheKilobytes = _pendingMotherboardCacheKb.Value
            End If
            boardContextForSettingsInBed.SaveConfiguration()
            _pendingMotherboardClockOptionId = Nothing
            _pendingMotherboardCacheKb = Nothing
        End If

        If ideChangesInBed Then
            Dim previousIdeInBed As IdeHardwareConfiguration =
                If(_ideHardwareConfiguration Is Nothing,
                   New IdeHardwareConfiguration(AppContext.BaseDirectory),
                   _ideHardwareConfiguration.CloneConfiguration())
            Dim nextIdeInBed As IdeHardwareConfiguration =
                _pendingIdeHardwareConfiguration.CloneConfiguration()
            _ideHardwareConfiguration = nextIdeInBed
            _pendingIdeHardwareConfiguration = Nothing
            _ideHardwareConfiguration.SaveConfiguration()
            ApplyIdeHardwareConfigurationToDevicesInBed()

            ' ATA model selection is real physical drive identity, not a cosmetic
            ' label.  When the model changes, rewrite only the adjacent .meta sidecar
            ' if the platter image has exactly the profile's capacity, then reattach
            ' so IDENTIFY DEVICE observes the new hardware.  An incompatible image
            ' is disconnected intact rather than resized or truncated.
            For channelInBed As Integer = 0 To 1
                For Each slaveInBed As Boolean In New Boolean() {False, True}
                    Dim slotKeyInBed As String = IdeSlotKeyInBed(channelInBed, slaveInBed)
                    Dim oldKindInBed As IdeDeviceKind = previousIdeInBed.GetKind(slotKeyInBed)
                    Dim newKindInBed As IdeDeviceKind = nextIdeInBed.GetKind(slotKeyInBed)
                    Dim oldModelInBed As String = previousIdeInBed.GetModelId(slotKeyInBed)
                    Dim newModelInBed As String = nextIdeInBed.GetModelId(slotKeyInBed)
                    Dim modelChangedInBed As Boolean =
                        Not String.Equals(oldModelInBed, newModelInBed, StringComparison.OrdinalIgnoreCase)

                    If newKindInBed <> IdeDeviceKind.AtaHardDisk Then
                        SetMountedIdeDriveIdInBed(channelInBed, -1, slaveInBed)
                        Continue For
                    End If

                    If oldKindInBed = IdeDeviceKind.AtaHardDisk AndAlso Not modelChangedInBed Then Continue For
                    Dim controllerInBed As IdeController = IdeControllerForChannelInBed(channelInBed)
                    WithMachineInBed(Sub() controllerInBed.EjectHardDisk(slaveInBed))
                    SetMountedIdeDriveIdInBed(channelInBed, -1, slaveInBed)
                    If _ideDriveShelf Is Nothing Then Continue For

                    Dim configuredInBed As Integer = _ideDriveShelf.GetConfiguredDriveId(channelInBed, slaveInBed)
                    If configuredInBed < 0 Then Continue For
                    Dim entryInBed As IdeDriveShelfEntry = _ideDriveShelf.FindById(configuredInBed)
                    If entryInBed Is Nothing Then Continue For
                    Dim profileInBed As IdeDriveModelProfile = IdeDriveModelCatalog.FindById(newModelInBed)
                    If profileInBed Is Nothing Then Continue For

                    If entryInBed.Identity Is Nothing OrElse entryInBed.Identity.TotalSectors <> profileInBed.TotalSectors Then
                        _ideDriveShelf.SetConfiguredDriveId(channelInBed, -1, slaveInBed)
                        _ideDriveShelf.SaveConfiguration()
                        Continue For
                    End If

                    Dim updatedIdentityInBed As HardDiskIdentity = HardDiskIdentity.LoadOrCreate(entryInBed.FullPath, entryInBed.Identity.TotalSectors)
                    updatedIdentityInBed.ApplyProfile(profileInBed, preserveSerialInBed:=True)
                    updatedIdentityInBed.Save(entryInBed.FullPath)
                    AttachConfiguredIdeDiskInBed(channelInBed, slaveInBed, silent:=True)
                Next
            Next
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

        If populationChangesInBed Then
            _hardwarePopulationConfiguration = _pendingHardwarePopulationConfiguration.CloneConfiguration()
            _pendingHardwarePopulationConfiguration = Nothing
            _hardwarePopulationConfiguration.SaveConfiguration()
            ApplyHardwarePopulationToMachineInBed()
        End If

        If wasPoweredInBed Then SetMachinePowerInBed(True)

        RebuildRamBanksMenu()
        UpdateMediaStatus()
        If _systemConfigurationDrawer IsNot Nothing Then _systemConfigurationDrawer.RefreshFromMachine()
    End Sub

    Private Sub InitializeHardwarePopulationConfigurationInBed()
        _hardwarePopulationConfiguration = New MachineHardwarePopulationConfiguration(AppContext.BaseDirectory)
        _hardwarePopulationConfiguration.LoadConfiguration()
        _hardwarePopulationConfiguration.SaveConfiguration()
    End Sub

    Private Function EffectiveHardwarePopulationInBed() As MachineHardwarePopulationConfiguration
        If _pendingHardwarePopulationConfiguration IsNot Nothing Then Return _pendingHardwarePopulationConfiguration.CloneConfiguration()
        If _hardwarePopulationConfiguration Is Nothing Then
            Return New MachineHardwarePopulationConfiguration(AppContext.BaseDirectory)
        End If
        Return _hardwarePopulationConfiguration.CloneConfiguration()
    End Function

    Private Shared Function SameHardwarePopulationInBed(aInBed As MachineHardwarePopulationConfiguration,
                                                         bInBed As MachineHardwarePopulationConfiguration) As Boolean
        If aInBed Is Nothing OrElse bInBed Is Nothing Then Return False
        Return String.Equals(aInBed.MotherboardId, bInBed.MotherboardId, StringComparison.OrdinalIgnoreCase) AndAlso
               String.Equals(aInBed.CpuId, bInBed.CpuId, StringComparison.OrdinalIgnoreCase) AndAlso
               String.Equals(aInBed.NumericCoprocessorId, bInBed.NumericCoprocessorId, StringComparison.OrdinalIgnoreCase) AndAlso
               String.Equals(aInBed.VideoCardId, bInBed.VideoCardId, StringComparison.OrdinalIgnoreCase) AndAlso
               String.Equals(aInBed.AudioCardId, bInBed.AudioCardId, StringComparison.OrdinalIgnoreCase) AndAlso
               String.Equals(aInBed.NetworkCardId, bInBed.NetworkCardId, StringComparison.OrdinalIgnoreCase)
    End Function

    Private Sub StageHardwarePopulationInBed(kindInBed As String, idInBed As String)
        If _hardwarePopulationConfiguration Is Nothing Then Return
        Dim nextInBed As MachineHardwarePopulationConfiguration = EffectiveHardwarePopulationInBed()
        Select Case kindInBed
            Case "motherboard"
                nextInBed.MotherboardId = MotherboardCatalog.NormalizeId(idInBed)
                Dim selectedBoardInBed As MotherboardProfile = MotherboardCatalog.Require(nextInBed.MotherboardId)
                If Not selectedBoardInBed.SupportsCpu(nextInBed.CpuId) Then nextInBed.CpuId = selectedBoardInBed.DefaultCpuId
                If Not selectedBoardInBed.SupportsNumericCoprocessor(nextInBed.NumericCoprocessorId) Then nextInBed.NumericCoprocessorId = selectedBoardInBed.DefaultNumericCoprocessorId
                Dim selectedContextInBed As MotherboardContextConfiguration = LoadMotherboardContextInBed(selectedBoardInBed.Id)
                Dim currentBoardIdInBed As String = _hardwarePopulationConfiguration.MotherboardId
                If Not String.Equals(selectedBoardInBed.Id, currentBoardIdInBed, StringComparison.OrdinalIgnoreCase) Then
                    _pendingMemoryMb = selectedContextInBed.MemoryMegabytes
                    _pendingMotherboardClockOptionId = selectedContextInBed.ClockOptionId
                    _pendingMotherboardCacheKb = selectedContextInBed.CacheKilobytes
                Else
                    _pendingMotherboardClockOptionId = Nothing
                    _pendingMotherboardCacheKb = Nothing
                    If _ramBanks IsNot Nothing AndAlso selectedBoardInBed.SupportsMemoryMegabytes(_ramBanks.InstalledMemoryMb) Then
                        _pendingMemoryMb = Nothing
                    End If
                End If
            Case "cpu"
                nextInBed.CpuId = MotherboardCatalog.NormalizeCpuIdForBoard(nextInBed.MotherboardId, idInBed)
            Case "npx"
                nextInBed.NumericCoprocessorId = MotherboardCatalog.NormalizeNumericCoprocessorIdForBoard(nextInBed.MotherboardId, idInBed)
            Case "video"
                nextInBed.VideoCardId = MachineHardwarePopulationConfiguration.NormalizeVideoCardId(idInBed)
            Case "audio"
                nextInBed.AudioCardId = MachineHardwarePopulationConfiguration.NormalizeAudioCardId(idInBed)
            Case "network"
                nextInBed.NetworkCardId = MachineHardwarePopulationConfiguration.NormalizeNetworkCardId(idInBed)
            Case Else
                Return
        End Select

        Dim sbInstalledInBed As Boolean = Not String.Equals(nextInBed.AudioCardId, MachineHardwarePopulationConfiguration.NoneId, StringComparison.OrdinalIgnoreCase)
        Dim neInstalledInBed As Boolean = Not String.Equals(nextInBed.NetworkCardId, MachineHardwarePopulationConfiguration.NoneId, StringComparison.OrdinalIgnoreCase)
        Dim npxInstalledInBed As Boolean = Not String.Equals(nextInBed.NumericCoprocessorId, MachineHardwarePopulationConfiguration.NoneId, StringComparison.OrdinalIgnoreCase)
        Dim conflictsInBed As List(Of String) = IsaResourceConflictDetector.Validate(EffectiveSoundBlaster16JumpersInBed(),
                                                                                   EffectiveNe2000JumpersInBed(),
                                                                                   sbInstalledInBed,
                                                                                   neInstalledInBed,
                                                                                   npxInstalledInBed)
        If conflictsInBed.Count > 0 Then
            MessageBox.Show(Me, String.Join(Environment.NewLine, conflictsInBed), "ISA resource conflict", MessageBoxButtons.OK, MessageBoxIcon.Warning)
            Return
        End If

        If SameHardwarePopulationInBed(nextInBed, _hardwarePopulationConfiguration) Then
            _pendingHardwarePopulationConfiguration = Nothing
        Else
            _pendingHardwarePopulationConfiguration = nextInBed
        End If
        If _systemConfigurationDrawer IsNot Nothing Then _systemConfigurationDrawer.RefreshFromMachine()
    End Sub

    Friend Sub StageMotherboardSelectionInBed(idInBed As String)
        StageHardwarePopulationInBed("motherboard", idInBed)
    End Sub

    Friend Sub StageCpuSelectionInBed(idInBed As String)
        StageHardwarePopulationInBed("cpu", idInBed)
    End Sub

    Friend Sub StageNumericCoprocessorSelectionInBed(idInBed As String)
        StageHardwarePopulationInBed("npx", idInBed)
    End Sub

    Friend Sub StageVideoSelectionInBed(idInBed As String)
        StageHardwarePopulationInBed("video", idInBed)
    End Sub

    Friend Sub StageAudioSelectionInBed(idInBed As String)
        StageHardwarePopulationInBed("audio", idInBed)
    End Sub

    Friend Sub StageNetworkSelectionInBed(idInBed As String)
        StageHardwarePopulationInBed("network", idInBed)
    End Sub

    Friend Sub StageMotherboardClockSelectionInBed(idInBed As String)
        Dim profileInBed As MotherboardProfile = MotherboardCatalog.Require(EffectiveHardwarePopulationInBed().MotherboardId)
        Dim optionInBed As MotherboardClockOption = profileInBed.FindClockOption(idInBed)
        If optionInBed Is Nothing Then Return
        Dim savedContextInBed As MotherboardContextConfiguration = LoadMotherboardContextInBed(profileInBed.Id)
        _pendingMotherboardClockOptionId = If(String.Equals(optionInBed.Id, savedContextInBed.ClockOptionId, StringComparison.OrdinalIgnoreCase), Nothing, optionInBed.Id)
        If _systemConfigurationDrawer IsNot Nothing Then _systemConfigurationDrawer.RefreshFromMachine()
    End Sub

    Friend Sub StageMotherboardCacheSelectionInBed(kilobytesInBed As Integer)
        Dim profileInBed As MotherboardProfile = MotherboardCatalog.Require(EffectiveHardwarePopulationInBed().MotherboardId)
        If Not profileInBed.SupportsCacheKilobytes(kilobytesInBed) Then Return
        Dim savedContextInBed As MotherboardContextConfiguration = LoadMotherboardContextInBed(profileInBed.Id)
        _pendingMotherboardCacheKb = If(kilobytesInBed = savedContextInBed.CacheKilobytes, CType(Nothing, Integer?), CType(kilobytesInBed, Integer?))
        If _systemConfigurationDrawer IsNot Nothing Then _systemConfigurationDrawer.RefreshFromMachine()
    End Sub

    Friend Function GetMotherboardClockChoicesInBed() As List(Of HardwareProfileChoice)
        Dim profileInBed As MotherboardProfile = MotherboardCatalog.Require(EffectiveHardwarePopulationInBed().MotherboardId)
        Dim choicesInBed As New List(Of HardwareProfileChoice)()
        For Each optionInBed As MotherboardClockOption In profileInBed.ClockOptions()
            choicesInBed.Add(New HardwareProfileChoice(optionInBed.Id, optionInBed.DisplayName))
        Next
        Return choicesInBed
    End Function

    Friend Function GetMotherboardCacheChoicesInBed() As List(Of HardwareProfileChoice)
        Dim profileInBed As MotherboardProfile = MotherboardCatalog.Require(EffectiveHardwarePopulationInBed().MotherboardId)
        Dim choicesInBed As New List(Of HardwareProfileChoice)()
        For Each cacheKbInBed As Integer In profileInBed.SupportedCacheKilobytes()
            choicesInBed.Add(New HardwareProfileChoice(cacheKbInBed.ToString(), If(cacheKbInBed = 0, "None / board has no separately populated L2 cache", cacheKbInBed.ToString() & " KB L2 cache")))
        Next
        Return choicesInBed
    End Function

    Private Function LoadMotherboardContextInBed(motherboardIdInBed As String) As MotherboardContextConfiguration
        Dim contextInBed As New MotherboardContextConfiguration(AppContext.BaseDirectory, motherboardIdInBed)
        contextInBed.LoadConfiguration()
        Return contextInBed
    End Function

    Private Shared Function CountInstalledIsa16CardsInBed(populationInBed As MachineHardwarePopulationConfiguration) As Integer
        If populationInBed Is Nothing Then Return 0
        Dim countInBed As Integer = 0
        If Not String.Equals(populationInBed.VideoCardId, MachineHardwarePopulationConfiguration.NoneId, StringComparison.OrdinalIgnoreCase) Then countInBed += 1
        If Not String.Equals(populationInBed.AudioCardId, MachineHardwarePopulationConfiguration.NoneId, StringComparison.OrdinalIgnoreCase) Then countInBed += 1
        If Not String.Equals(populationInBed.NetworkCardId, MachineHardwarePopulationConfiguration.NoneId, StringComparison.OrdinalIgnoreCase) Then countInBed += 1
        Return countInBed
    End Function

    Friend Function GetMotherboardChoicesInBed() As List(Of HardwareProfileChoice)
        Dim choicesInBed As New List(Of HardwareProfileChoice)()
        For Each profileInBed As MotherboardProfile In MotherboardCatalog.Profiles()
            choicesInBed.Add(New HardwareProfileChoice(profileInBed.Id, profileInBed.DisplayName))
        Next
        Return choicesInBed
    End Function

    Friend Function GetProcessorChoicesInBed() As List(Of HardwareProfileChoice)
        Dim populationInBed As MachineHardwarePopulationConfiguration = EffectiveHardwarePopulationInBed()
        Dim profileInBed As MotherboardProfile = MotherboardCatalog.Require(populationInBed.MotherboardId)
        Dim choicesInBed As New List(Of HardwareProfileChoice)()
        For Each cpuIdInBed As String In profileInBed.SupportedCpuIds()
            choicesInBed.Add(New HardwareProfileChoice(cpuIdInBed, ProcessorPackageCatalog.DisplayName(cpuIdInBed)))
        Next
        Return choicesInBed
    End Function

    Friend Function GetNumericCoprocessorChoicesInBed() As List(Of HardwareProfileChoice)
        Dim populationInBed As MachineHardwarePopulationConfiguration = EffectiveHardwarePopulationInBed()
        Dim profileInBed As MotherboardProfile = MotherboardCatalog.Require(populationInBed.MotherboardId)
        Dim choicesInBed As New List(Of HardwareProfileChoice)()
        For Each npxIdInBed As String In profileInBed.SupportedNumericCoprocessorIds()
            choicesInBed.Add(New HardwareProfileChoice(npxIdInBed, NumericCoprocessorPackageCatalog.DisplayName(npxIdInBed)))
        Next
        Return choicesInBed
    End Function

    Friend Function GetMemoryChoicesInBed() As List(Of HardwareProfileChoice)
        Dim populationInBed As MachineHardwarePopulationConfiguration = EffectiveHardwarePopulationInBed()
        Dim profileInBed As MotherboardProfile = MotherboardCatalog.Require(populationInBed.MotherboardId)
        Dim choicesInBed As New List(Of HardwareProfileChoice)()
        For Each megabytesInBed As Integer In profileInBed.SupportedMemoryMegabytes()
            choicesInBed.Add(New HardwareProfileChoice(megabytesInBed.ToString(), megabytesInBed.ToString() & " MB installed"))
        Next
        Return choicesInBed
    End Function

    Friend Function GetVideoCardChoicesInBed() As List(Of HardwareProfileChoice)
        Return New List(Of HardwareProfileChoice) From {
            New HardwareProfileChoice(MachineHardwarePopulationConfiguration.NoneId, IsaCardCatalog286.VideoDisplayName(MachineHardwarePopulationConfiguration.NoneId)),
            New HardwareProfileChoice(MachineHardwarePopulationConfiguration.DiamondStealthPro928Id, IsaCardCatalog286.VideoDisplayName(MachineHardwarePopulationConfiguration.DiamondStealthPro928Id))
        }
    End Function

    Friend Function GetAudioCardChoicesInBed() As List(Of HardwareProfileChoice)
        Return New List(Of HardwareProfileChoice) From {
            New HardwareProfileChoice(MachineHardwarePopulationConfiguration.NoneId, IsaCardCatalog286.AudioDisplayName(MachineHardwarePopulationConfiguration.NoneId)),
            New HardwareProfileChoice(MachineHardwarePopulationConfiguration.SoundBlaster16Id, IsaCardCatalog286.AudioDisplayName(MachineHardwarePopulationConfiguration.SoundBlaster16Id))
        }
    End Function

    Friend Function GetNetworkCardChoicesInBed() As List(Of HardwareProfileChoice)
        Return New List(Of HardwareProfileChoice) From {
            New HardwareProfileChoice(MachineHardwarePopulationConfiguration.NoneId, IsaCardCatalog286.NetworkDisplayName(MachineHardwarePopulationConfiguration.NoneId)),
            New HardwareProfileChoice(MachineHardwarePopulationConfiguration.Ne2000Id, IsaCardCatalog286.NetworkDisplayName(MachineHardwarePopulationConfiguration.Ne2000Id))
        }
    End Function

    Private Sub RemoveInstalledIsaCardsInBed()
        For Each slotIdInBed As String In New String() {VideoIsaSlotId, AudioIsaSlotId, NetworkIsaSlotId}
            Dim slotInBed As IsaBackplaneSlot286 = IsaBackplane.FindSlot(slotIdInBed)
            If slotInBed IsNot Nothing AndAlso Not slotInBed.IsEmpty Then IsaBackplane.RemoveCard(slotIdInBed)
        Next
        VideoCard = Nothing
        SoundBlaster16 = Nothing
        Ne2000 = Nothing
        _installedVideoHardwareId = MachineHardwarePopulationConfiguration.NoneId
        _installedAudioHardwareId = MachineHardwarePopulationConfiguration.NoneId
        _installedNetworkHardwareId = MachineHardwarePopulationConfiguration.NoneId
    End Sub

    Private Sub ApplyHardwarePopulationToMachineInBed()
        If _hardwarePopulationConfiguration Is Nothing Then Return
        If _machineRuntime.IsRunning Then Throw New InvalidOperationException("Hardware population may only change while chassis power is off.")
        IsaBackplane.SetChassisPowered(False)

        Dim targetCpuInBed As String = MachineHardwarePopulationConfiguration.NormalizeCpuId(_hardwarePopulationConfiguration.CpuId)
        If Not String.Equals(_installedCpuHardwareId, targetCpuInBed, StringComparison.OrdinalIgnoreCase) Then
            SetProcessorPackageInBed(Nothing)
            If Not String.Equals(targetCpuInBed, MachineHardwarePopulationConfiguration.NoneId, StringComparison.OrdinalIgnoreCase) Then
                SetProcessorPackageInBed(ProcessorCatalog286.CreateProcessor(targetCpuInBed))
            End If
            _installedCpuHardwareId = targetCpuInBed
        End If

        Dim targetNpxInBed As String = MachineHardwarePopulationConfiguration.NormalizeNumericCoprocessorId(_hardwarePopulationConfiguration.NumericCoprocessorId)
        If Not String.Equals(_installedNumericCoprocessorHardwareId, targetNpxInBed, StringComparison.OrdinalIgnoreCase) Then
            SetNumericCoprocessorPackageInBed(Nothing)
            If Not String.Equals(targetNpxInBed, MachineHardwarePopulationConfiguration.NoneId, StringComparison.OrdinalIgnoreCase) Then
                SetNumericCoprocessorPackageInBed(NumericCoprocessorCatalog286.CreateNumericCoprocessor(targetNpxInBed))
            End If
            _installedNumericCoprocessorHardwareId = targetNpxInBed
        End If
        _machineRuntime.SetProcessor(CPU0)

        If CPU0 IsNot Nothing Then
            CPU0.MirrorLegacyTextCells = False
            CPU0.HostFirmwareInterrupts = False
        End If

        Dim targetVideoInBed As String = MachineHardwarePopulationConfiguration.NormalizeVideoCardId(_hardwarePopulationConfiguration.VideoCardId)
        If Not String.Equals(_installedVideoHardwareId, targetVideoInBed, StringComparison.OrdinalIgnoreCase) Then
            Dim slotInBed As IsaBackplaneSlot286 = IsaBackplane.FindSlot(VideoIsaSlotId)
            If slotInBed IsNot Nothing AndAlso Not slotInBed.IsEmpty Then IsaBackplane.RemoveCard(VideoIsaSlotId)
            VideoCard = Nothing
            If Not String.Equals(targetVideoInBed, MachineHardwarePopulationConfiguration.NoneId, StringComparison.OrdinalIgnoreCase) Then
                Dim definitionInBed As New IsaCardDefinition286(MachineHardwarePopulationConfiguration.DiamondStealthPro928Id,
                                                                IsaCardCatalog286.VideoDisplayName(targetVideoInBed),
                                                                IsaSlotConnectorWidth286.SixteenBit,
                                                                Function() New DiamondStealthPro928())
                VideoCard = DirectCast(IsaBackplane.InstallCard(VideoIsaSlotId, definitionInBed), DiamondStealthPro928)
                Dim videoRomPathInBed As String = Path.Combine(AppContext.BaseDirectory, "Firmware", "stealthpro.rom")
                If File.Exists(videoRomPathInBed) Then VideoCard.LoadOptionRom(File.ReadAllBytes(videoRomPathInBed))
                VideoCard.DiagnosticOverlayJumperClosed = False
            End If
            _installedVideoHardwareId = targetVideoInBed
        End If

        Dim targetAudioInBed As String = MachineHardwarePopulationConfiguration.NormalizeAudioCardId(_hardwarePopulationConfiguration.AudioCardId)
        If Not String.Equals(_installedAudioHardwareId, targetAudioInBed, StringComparison.OrdinalIgnoreCase) Then
            Dim slotInBed As IsaBackplaneSlot286 = IsaBackplane.FindSlot(AudioIsaSlotId)
            If slotInBed IsNot Nothing AndAlso Not slotInBed.IsEmpty Then IsaBackplane.RemoveCard(AudioIsaSlotId)
            SoundBlaster16 = Nothing
            If Not String.Equals(targetAudioInBed, MachineHardwarePopulationConfiguration.NoneId, StringComparison.OrdinalIgnoreCase) Then
                Dim sbSettingsInBed As SoundBlaster16JumperSettings = EffectiveSoundBlaster16JumpersInBed()
                Dim definitionInBed As New IsaCardDefinition286(MachineHardwarePopulationConfiguration.SoundBlaster16Id,
                                                                IsaCardCatalog286.AudioDisplayName(targetAudioInBed),
                                                                IsaSlotConnectorWidth286.SixteenBit,
                                                                Function()
                                                                    Dim cardInBed As New SoundBlaster16Isa(sbSettingsInBed.BasePort, sbSettingsInBed.Irq, sbSettingsInBed.Dma8, sbSettingsInBed.Dma16, MasterPic, SlavePic, DmaController)
                                                                    cardInBed.ConfigureHardware(sbSettingsInBed.BasePort, sbSettingsInBed.Irq, sbSettingsInBed.Dma8, sbSettingsInBed.Dma16, sbSettingsInBed.MpuPort, sbSettingsInBed.GamePortEnabled)
                                                                    cardInBed.ConfigureWavetableHardware(sbSettingsInBed.Wavetable)
                                                                    Return cardInBed
                                                                End Function)
                SoundBlaster16 = DirectCast(IsaBackplane.InstallCard(AudioIsaSlotId, definitionInBed), SoundBlaster16Isa)
            End If
            _installedAudioHardwareId = targetAudioInBed
        End If

        Dim targetNetworkInBed As String = MachineHardwarePopulationConfiguration.NormalizeNetworkCardId(_hardwarePopulationConfiguration.NetworkCardId)
        If Not String.Equals(_installedNetworkHardwareId, targetNetworkInBed, StringComparison.OrdinalIgnoreCase) Then
            Dim slotInBed As IsaBackplaneSlot286 = IsaBackplane.FindSlot(NetworkIsaSlotId)
            If slotInBed IsNot Nothing AndAlso Not slotInBed.IsEmpty Then IsaBackplane.RemoveCard(NetworkIsaSlotId)
            Ne2000 = Nothing
            If Not String.Equals(targetNetworkInBed, MachineHardwarePopulationConfiguration.NoneId, StringComparison.OrdinalIgnoreCase) Then
                Dim neSettingsInBed As Ne2000JumperSettings = EffectiveNe2000JumpersInBed()
                Dim definitionInBed As New IsaCardDefinition286(MachineHardwarePopulationConfiguration.Ne2000Id,
                                                                IsaCardCatalog286.NetworkDisplayName(targetNetworkInBed),
                                                                IsaSlotConnectorWidth286.SixteenBit,
                                                                Function()
                                                                    Dim cardInBed As New Ne2000Isa(neSettingsInBed.BasePort, neSettingsInBed.Irq, MasterPic, SlavePic)
                                                                    cardInBed.ConfigureHardware(neSettingsInBed.BasePort, neSettingsInBed.Irq)
                                                                    Return cardInBed
                                                                End Function)
                Ne2000 = DirectCast(IsaBackplane.InstallCard(NetworkIsaSlotId, definitionInBed), Ne2000Isa)
            End If
            _installedNetworkHardwareId = targetNetworkInBed
        End If

        ConfigureVideoPresentationForInstalledAdapterInBed()
    End Sub

    Private Sub ConfigureVideoPresentationForInstalledAdapterInBed()
        If VideoCard Is Nothing Then
            GPU.Enabled = False
            If _videoPresentation IsNot Nothing Then
                RemoveHandler _videoPresentation.PresentationFaulted, AddressOf VideoPresentationFaultedInBed
                _videoPresentation.Dispose()
                _videoPresentation = Nothing
            End If
            If _machinePoweredInBed Then ShowPowerOffArtworkInBed()
            Return
        End If

        If _videoPresentation Is Nothing Then
            _videoPresentation = New DiamondStealthPro928PresentationWorker()
            AddHandler _videoPresentation.PresentationFaulted, AddressOf VideoPresentationFaultedInBed
            _videoPresentation.Start()
        End If
        GPU.Enabled = _machinePoweredInBed
    End Sub

    Private Sub InitializeIsaExpansionCardConfigurationInBed()
        _isaCardConfiguration = New IsaExpansionCardConfiguration(AppContext.BaseDirectory)
        _isaCardConfiguration.LoadConfiguration()
        Dim populationInBed As MachineHardwarePopulationConfiguration = EffectiveHardwarePopulationInBed()
        Dim conflictsInBed As List(Of String) = IsaResourceConflictDetector.Validate(_isaCardConfiguration.SoundBlaster16,
                                                                                    _isaCardConfiguration.Ne2000,
                                                                                    Not String.Equals(populationInBed.AudioCardId, MachineHardwarePopulationConfiguration.NoneId, StringComparison.OrdinalIgnoreCase),
                                                                                    Not String.Equals(populationInBed.NetworkCardId, MachineHardwarePopulationConfiguration.NoneId, StringComparison.OrdinalIgnoreCase),
                                                                                    Not String.Equals(populationInBed.NumericCoprocessorId, MachineHardwarePopulationConfiguration.NoneId, StringComparison.OrdinalIgnoreCase))
        If conflictsInBed.Count > 0 Then
            ' Preserve the installed-card population but recover impossible strap
            ' values to the clean factory positions before the backplane is wired.
            _isaCardConfiguration.SoundBlaster16 = New SoundBlaster16JumperSettings()
            _isaCardConfiguration.Ne2000 = New Ne2000JumperSettings()
        End If
        _isaCardConfiguration.SaveConfiguration()
    End Sub

    Private Sub ApplyIsaExpansionCardConfigurationToDevicesInBed()
        If _isaCardConfiguration Is Nothing Then Return
        Dim sbInBed As SoundBlaster16JumperSettings = _isaCardConfiguration.SoundBlaster16
        Dim neInBed As Ne2000JumperSettings = _isaCardConfiguration.Ne2000
        If SoundBlaster16 IsNot Nothing Then
            SoundBlaster16.ConfigureHardware(sbInBed.BasePort, sbInBed.Irq, sbInBed.Dma8, sbInBed.Dma16, sbInBed.MpuPort, sbInBed.GamePortEnabled)
            SoundBlaster16.ConfigureWavetableHardware(sbInBed.Wavetable)
        End If
        If Ne2000 IsNot Nothing Then Ne2000.ConfigureHardware(neInBed.BasePort, neInBed.Irq)
    End Sub

    Private Sub InitializeRamBanks()
        _ramBanks = New RamBankConfiguration(AppContext.BaseDirectory)
        _ramBanks.LoadConfiguration()
        ' Persist the default too, so the chassis configuration is explicit.
        _ramBanks.SaveConfiguration()
        MotherboardMemory.ConfigureInstalledMemoryMegabytes(_ramBanks.InstalledMemoryMb, clearRam:=True)
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
    Private Sub InitializeIdeHardwareConfigurationInBed()
        _ideHardwareConfiguration = New IdeHardwareConfiguration(AppContext.BaseDirectory)
        _ideHardwareConfiguration.LoadConfiguration()
        _ideHardwareConfiguration.SaveConfiguration()
        ApplyIdeHardwareConfigurationToDevicesInBed()
    End Sub

    Private Sub ApplyIdeHardwareConfigurationToDevicesInBed()
        If _ideHardwareConfiguration Is Nothing Then Return
        WithMachineInBed(
            Sub()
                Declares.IdeController.ConfigureDeviceKinds(
                    _ideHardwareConfiguration.PrimaryMasterKind,
                    _ideHardwareConfiguration.PrimarySlaveKind)
                Declares.SecondaryIdeController.ConfigureDeviceKinds(
                    _ideHardwareConfiguration.SecondaryMasterKind,
                    _ideHardwareConfiguration.SecondarySlaveKind)
            End Sub)
    End Sub

    Private Function EffectiveIdeHardwareConfigurationInBed() As IdeHardwareConfiguration
        If _pendingIdeHardwareConfiguration IsNot Nothing Then Return _pendingIdeHardwareConfiguration.CloneConfiguration()
        If _ideHardwareConfiguration Is Nothing Then
            Dim fallbackInBed As New IdeHardwareConfiguration(AppContext.BaseDirectory)
            Return fallbackInBed
        End If
        Return _ideHardwareConfiguration.CloneConfiguration()
    End Function

    Friend Sub StageIdeHardwareKindInBed(slotInBed As String, kindIdInBed As String)
        If _ideHardwareConfiguration Is Nothing Then Return

        Dim stagedInBed As IdeHardwareConfiguration = EffectiveIdeHardwareConfigurationInBed()
        Dim currentKindInBed As IdeDeviceKind = stagedInBed.GetKind(slotInBed)
        Dim nextKindInBed As IdeDeviceKind = IdeHardwareConfiguration.ParseKind(kindIdInBed, currentKindInBed)
        stagedInBed.SetKind(slotInBed, nextKindInBed)

        If stagedInBed.SamePopulation(_ideHardwareConfiguration) Then
            _pendingIdeHardwareConfiguration = Nothing
        Else
            _pendingIdeHardwareConfiguration = stagedInBed
        End If

        If _systemConfigurationDrawer IsNot Nothing Then _systemConfigurationDrawer.RefreshFromMachine()
    End Sub

    Friend Sub StageIdeDriveModelInBed(slotInBed As String, modelIdInBed As String)
        If _ideHardwareConfiguration Is Nothing Then Return
        Dim profileInBed As IdeDriveModelProfile = IdeDriveModelCatalog.FindById(modelIdInBed)
        If profileInBed Is Nothing Then Return

        Dim stagedInBed As IdeHardwareConfiguration = EffectiveIdeHardwareConfigurationInBed()
        If stagedInBed.GetKind(slotInBed) <> IdeDeviceKind.AtaHardDisk Then Return

        Dim channelInBed As Integer = If(slotInBed.StartsWith("secondary-", StringComparison.OrdinalIgnoreCase), 1, 0)
        Dim slaveInBed As Boolean = slotInBed.EndsWith("-slave", StringComparison.OrdinalIgnoreCase)
        If _ideDriveShelf IsNot Nothing Then
            Dim configuredInBed As Integer = _ideDriveShelf.GetConfiguredDriveId(channelInBed, slaveInBed)
            If configuredInBed >= 0 Then
                Try
                    Dim entryInBed As IdeDriveShelfEntry = _ideDriveShelf.FindById(configuredInBed)
                    If entryInBed IsNot Nothing AndAlso entryInBed.Identity IsNot Nothing AndAlso
                       entryInBed.Identity.TotalSectors <> profileInBed.TotalSectors Then
                        Dim answerInBed As DialogResult = MessageBox.Show(
                            Me,
                            profileInBed.DisplayName & " requires " & profileInBed.TotalSectors.ToString() &
                            " sectors (" & profileInBed.CapacityDescription & ")." & Environment.NewLine & Environment.NewLine &
                            "The currently configured shelf drive #" & configuredInBed.ToString() & " contains " &
                            entryInBed.Identity.TotalSectors.ToString() & " sectors and cannot physically become that model." &
                            Environment.NewLine & Environment.NewLine &
                            "Stage the model change and disconnect that incompatible drive at the next power cycle? The image will not be resized or deleted.",
                            "IDE drive model capacity mismatch",
                            MessageBoxButtons.YesNo,
                            MessageBoxIcon.Warning)
                        If answerInBed <> DialogResult.Yes Then Return
                    End If
                Catch ex As Exception
                    MessageBox.Show(Me, ex.Message, "Unable to inspect IDE drive identity", MessageBoxButtons.OK, MessageBoxIcon.Error)
                    Return
                End Try
            End If
        End If

        stagedInBed.SetModelId(slotInBed, profileInBed.Id)
        If stagedInBed.SamePopulation(_ideHardwareConfiguration) Then
            _pendingIdeHardwareConfiguration = Nothing
        Else
            _pendingIdeHardwareConfiguration = stagedInBed
        End If
        If _systemConfigurationDrawer IsNot Nothing Then _systemConfigurationDrawer.RefreshFromMachine()
    End Sub

    Friend Function GetIdeDriveModelDetailsInBed(channelInBed As Integer, slaveInBed As Boolean) As String
        Dim slotInBed As String = IdeSlotKeyInBed(channelInBed, slaveInBed)
        Dim effectiveInBed As IdeHardwareConfiguration = EffectiveIdeHardwareConfigurationInBed()
        If effectiveInBed.GetKind(slotInBed) <> IdeDeviceKind.AtaHardDisk Then Return "Not an ATA hard-disk socket."
        Dim profileInBed As IdeDriveModelProfile = IdeDriveModelCatalog.FindById(effectiveInBed.GetModelId(slotInBed))
        Dim textInBed As String = IdeDriveModelCatalog.Describe(profileInBed)

        If _ideDriveShelf Is Nothing Then Return textInBed & Environment.NewLine & "IDE Drive Shelf unavailable."
        Dim configuredInBed As Integer = _ideDriveShelf.GetConfiguredDriveId(channelInBed, slaveInBed)
        If configuredInBed < 0 Then Return textInBed & Environment.NewLine & "Backing drive: none attached."
        Try
            Dim entryInBed As IdeDriveShelfEntry = _ideDriveShelf.FindById(configuredInBed)
            If entryInBed Is Nothing Then Return textInBed & Environment.NewLine & "Backing drive: shelf #" & configuredInBed.ToString() & " is unavailable."
            Dim identityInBed As HardDiskIdentity = entryInBed.Identity
            Dim matchTextInBed As String = String.Empty
            If profileInBed IsNot Nothing AndAlso identityInBed IsNot Nothing Then
                matchTextInBed = If(profileInBed.TotalSectors = identityInBed.TotalSectors,
                                    "capacity compatible",
                                    "CAPACITY MISMATCH — will detach on power cycle")
            End If
            Return textInBed & Environment.NewLine &
                   "Backing drive: #" & entryInBed.Id.ToString() & " — " & entryInBed.Label & Environment.NewLine &
                   "Sidecar identity: " & If(identityInBed Is Nothing, "unavailable", identityInBed.Manufacturer & " " & identityInBed.Model) &
                   If(matchTextInBed.Length = 0, String.Empty, " — " & matchTextInBed)
        Catch ex As Exception
            Return textInBed & Environment.NewLine & "Backing drive status unavailable: " & ex.Message
        End Try
    End Function

    ' Compatibility shim for the first population-only configurator implementation.
    Friend Sub StageIdeHardwarePopulationInBed(slotInBed As String, installedInBed As Boolean)
        Dim fallbackKindInBed As String =
            If(slotInBed IsNot Nothing AndAlso slotInBed.EndsWith("slave", StringComparison.OrdinalIgnoreCase),
               "atapi",
               "ata")
        StageIdeHardwareKindInBed(slotInBed, If(installedInBed, fallbackKindInBed, "empty"))
    End Sub

    Private Function IdeControllerForChannelInBed(channelInBed As Integer) As IdeController
        Select Case channelInBed
            Case 0
                Return Declares.IdeController
            Case 1
                Return Declares.SecondaryIdeController
            Case Else
                Throw New ArgumentOutOfRangeException(NameOf(channelInBed))
        End Select
    End Function

    Private Function IdeChannelDisplayNameInBed(channelInBed As Integer) As String
        Select Case channelInBed
            Case 0
                Return "Primary"
            Case 1
                Return "Secondary"
            Case Else
                Throw New ArgumentOutOfRangeException(NameOf(channelInBed))
        End Select
    End Function

    Private Function IdeSlotKeyInBed(channelInBed As Integer, slaveInBed As Boolean) As String
        If channelInBed <> 0 AndAlso channelInBed <> 1 Then Throw New ArgumentOutOfRangeException(NameOf(channelInBed))
        Return If(channelInBed = 0, "primary-", "secondary-") & If(slaveInBed, "slave", "master")
    End Function

    Private Function IdeSlotDisplayNameInBed(channelInBed As Integer, slaveInBed As Boolean) As String
        Return "Channel " & (channelInBed + 1).ToString() & " Drive " & If(slaveInBed, "1 (Slave)", "0 (Master)")
    End Function

    Private Function MountedIdeDriveIdInBed(channelInBed As Integer,
                                               Optional slaveInBed As Boolean = False) As Integer
        Select Case channelInBed
            Case 0
                Return If(slaveInBed, _mountedPrimarySlaveIdeDriveId, _mountedIdeDriveId)
            Case 1
                Return If(slaveInBed, _mountedSecondarySlaveIdeDriveId, _mountedSecondaryIdeDriveId)
            Case Else
                Throw New ArgumentOutOfRangeException(NameOf(channelInBed))
        End Select
    End Function

    Private Sub SetMountedIdeDriveIdInBed(channelInBed As Integer,
                                          driveIdInBed As Integer,
                                          Optional slaveInBed As Boolean = False)
        Select Case channelInBed
            Case 0
                If slaveInBed Then _mountedPrimarySlaveIdeDriveId = driveIdInBed Else _mountedIdeDriveId = driveIdInBed
            Case 1
                If slaveInBed Then _mountedSecondarySlaveIdeDriveId = driveIdInBed Else _mountedSecondaryIdeDriveId = driveIdInBed
            Case Else
                Throw New ArgumentOutOfRangeException(NameOf(channelInBed))
        End Select
    End Sub

    Private Function FindMountedIdeSlotForDriveInBed(driveIdInBed As Integer,
                                                       excludeChannelInBed As Integer,
                                                       excludeSlaveInBed As Boolean,
                                                       ByRef foundChannelInBed As Integer,
                                                       ByRef foundSlaveInBed As Boolean) As Boolean
        For channelInBed As Integer = 0 To 1
            For Each slaveInBed As Boolean In New Boolean() {False, True}
                If channelInBed = excludeChannelInBed AndAlso slaveInBed = excludeSlaveInBed Then Continue For
                If MountedIdeDriveIdInBed(channelInBed, slaveInBed) = driveIdInBed Then
                    foundChannelInBed = channelInBed
                    foundSlaveInBed = slaveInBed
                    Return True
                End If
            Next
        Next
        Return False
    End Function

    Private Sub InitializeIdeDriveShelf()
        _ideDriveShelf = New IdeDriveShelf(AppContext.BaseDirectory)
        _ideDriveShelf.EnsureShelfExists()
        _ideDriveShelf.LoadConfiguration()
        For channelInBed As Integer = 0 To 1
            AttachConfiguredIdeDiskInBed(channelInBed, False, silent:=True)
            AttachConfiguredIdeDiskInBed(channelInBed, True, silent:=True)
        Next
        SynchronizeIdeModelConfigurationFromAttachedDrivesInBed()
    End Sub

    Private Sub SynchronizeIdeModelConfigurationFromAttachedDrivesInBed()
        If _ideHardwareConfiguration Is Nothing OrElse _ideDriveShelf Is Nothing Then Return
        Dim changedInBed As Boolean = False
        For channelInBed As Integer = 0 To 1
            For Each slaveInBed As Boolean In New Boolean() {False, True}
                Dim slotInBed As String = IdeSlotKeyInBed(channelInBed, slaveInBed)
                If _ideHardwareConfiguration.GetKind(slotInBed) <> IdeDeviceKind.AtaHardDisk Then Continue For
                Dim configuredInBed As Integer = _ideDriveShelf.GetConfiguredDriveId(channelInBed, slaveInBed)
                If configuredInBed < 0 Then Continue For
                Try
                    Dim entryInBed As IdeDriveShelfEntry = _ideDriveShelf.FindById(configuredInBed)
                    If entryInBed Is Nothing OrElse entryInBed.Identity Is Nothing OrElse
                       String.IsNullOrWhiteSpace(entryInBed.Identity.ProfileId) Then Continue For
                    If IdeDriveModelCatalog.FindById(entryInBed.Identity.ProfileId) Is Nothing Then Continue For
                    If Not String.Equals(_ideHardwareConfiguration.GetModelId(slotInBed), entryInBed.Identity.ProfileId, StringComparison.OrdinalIgnoreCase) Then
                        _ideHardwareConfiguration.SetModelId(slotInBed, entryInBed.Identity.ProfileId)
                        changedInBed = True
                    End If
                Catch
                    ' Attachment/status reporting will surface malformed sidecars.
                End Try
            Next
        Next
        If changedInBed Then _ideHardwareConfiguration.SaveConfiguration()
    End Sub

    Private Sub AttachConfiguredIdeDiskInBed(channelInBed As Integer,
                                              slaveInBed As Boolean,
                                              silent As Boolean)
        Dim controllerInBed As IdeController = IdeControllerForChannelInBed(channelInBed)
        SetMountedIdeDriveIdInBed(channelInBed, -1, slaveInBed)
        WithMachineInBed(Sub() controllerInBed.EjectHardDisk(slaveInBed))

        If ReadMachineInBed(Function() controllerInBed.GetDeviceKind(slaveInBed)) <> IdeDeviceKind.AtaHardDisk Then Return
        If _ideDriveShelf Is Nothing Then Return

        Dim configuredInBed As Integer = _ideDriveShelf.GetConfiguredDriveId(channelInBed, slaveInBed)
        If configuredInBed < 0 Then Return

        Dim otherChannelInBed As Integer = -1
        Dim otherSlaveInBed As Boolean = False
        If FindMountedIdeSlotForDriveInBed(configuredInBed, channelInBed, slaveInBed, otherChannelInBed, otherSlaveInBed) Then
            If Not silent Then
                MessageBox.Show(
                    Me,
                    "Shelf drive #" & configuredInBed.ToString() &
                    " is already attached to " &
                    IdeSlotDisplayNameInBed(otherChannelInBed, otherSlaveInBed).ToLowerInvariant() &
                    ". A writable disk image cannot be plugged into two IDE sockets at once.",
                    "IDE drive already attached",
                    MessageBoxButtons.OK,
                    MessageBoxIcon.Warning)
            End If
            Return
        End If

        Try
            Dim entryInBed As IdeDriveShelfEntry = _ideDriveShelf.FindById(configuredInBed)
            If entryInBed Is Nothing Then Return
            WithMachineInBed(Sub() MountHardDiskImage(entryInBed.FullPath, False, channelInBed, slaveInBed))
            SetMountedIdeDriveIdInBed(channelInBed, entryInBed.Id, slaveInBed)
        Catch ex As Exception
            WithMachineInBed(Sub() controllerInBed.EjectHardDisk(slaveInBed))
            SetMountedIdeDriveIdInBed(channelInBed, -1, slaveInBed)
            If Not silent Then
                MessageBox.Show(
                    Me,
                    "Shelf drive #" & configuredInBed.ToString() & " could not be attached:" &
                    Environment.NewLine & ex.Message,
                    IdeSlotDisplayNameInBed(channelInBed, slaveInBed),
                    MessageBoxButtons.OK,
                    MessageBoxIcon.Error)
            End If
        End Try
    End Sub

    Private Sub RebuildIdeDriveShelfMenu()
        If ideDriveShelfMenu Is Nothing Then Return
        ideDriveShelfMenu.DropDownItems.Clear()

        For channelInBed As Integer = 0 To 1
            For Each slaveInBed As Boolean In New Boolean() {False, True}
                Dim slotMenuInBed As New ToolStripMenuItem(IdeSlotDisplayNameInBed(channelInBed, slaveInBed))
                PopulateIdeDriveShelfMenuInBed(slotMenuInBed, channelInBed, slaveInBed)
                ideDriveShelfMenu.DropDownItems.Add(slotMenuInBed)
            Next
        Next
    End Sub

    Private Sub PopulateIdeDriveShelfMenuInBed(menuInBed As ToolStripMenuItem,
                                               Optional channelInBed As Integer = 0,
                                               Optional slaveInBed As Boolean = False)
        If menuInBed Is Nothing Then Return
        menuInBed.DropDownItems.Clear()

        If _ideDriveShelf Is Nothing Then
            menuInBed.DropDownItems.Add(New ToolStripMenuItem("IDE shelf is not initialized") With {.Enabled = False})
            Return
        End If

        Dim kindInBed As IdeDeviceKind =
            ReadMachineInBed(Function() IdeControllerForChannelInBed(channelInBed).GetDeviceKind(slaveInBed))
        If kindInBed <> IdeDeviceKind.AtaHardDisk Then
            menuInBed.DropDownItems.Add(
                New ToolStripMenuItem(
                    IdeSlotDisplayNameInBed(channelInBed, slaveInBed) & " contains " &
                    If(kindInBed = IdeDeviceKind.AtapiCdRom, "an ATAPI CD-ROM", "no device") &
                    " — select ATA hard disk in System Configuration") With {.Enabled = False})
            Return
        End If

        Try
            Dim entriesInBed As List(Of IdeDriveShelfEntry) = _ideDriveShelf.GetEntries()
            Dim configuredInBed As Integer = _ideDriveShelf.GetConfiguredDriveId(channelInBed, slaveInBed)
            Dim mountedInBed As Integer = MountedIdeDriveIdInBed(channelInBed, slaveInBed)
            Dim slotNameInBed As String = IdeSlotDisplayNameInBed(channelInBed, slaveInBed)
            Dim configuredTextInBed As String =
                If(configuredInBed < 0,
                   slotNameInBed & ": disconnected",
                   slotNameInBed & ": shelf #" & configuredInBed.ToString() &
                   If(mountedInBed = configuredInBed, " (attached)", " (not attached)"))
            menuInBed.DropDownItems.Add(New ToolStripMenuItem(configuredTextInBed) With {.Enabled = False})
            menuInBed.DropDownItems.Add(New ToolStripSeparator())

            If entriesInBed.Count = 0 Then
                menuInBed.DropDownItems.Add(New ToolStripMenuItem("No numbered .hdd/.img files found") With {.Enabled = False})
            Else
                For Each entryInBed As IdeDriveShelfEntry In entriesInBed
                    Dim driveIdInBed As Integer = entryInBed.Id
                    Dim itemInBed As New ToolStripMenuItem(
                        entryInBed.Id.ToString() & " - " & entryInBed.Label) With {
                        .Checked = (entryInBed.Id = mountedInBed)
                    }
                    itemInBed.ToolTipText = entryInBed.FullPath
                    AddHandler itemInBed.Click, Sub() SelectIdeShelfDrive(driveIdInBed, channelInBed, slaveInBed)
                    menuInBed.DropDownItems.Add(itemInBed)
                Next
            End If

            menuInBed.DropDownItems.Add(New ToolStripSeparator())
            menuInBed.DropDownItems.Add("Rescan IDE-Drives", Nothing, Sub() PopulateIdeDriveShelfMenuInBed(menuInBed, channelInBed, slaveInBed))
            menuInBed.DropDownItems.Add("Open IDE-Drives folder", Nothing, Sub() OpenIdeDriveShelfFolder())
            menuInBed.DropDownItems.Add("Disconnect " & slotNameInBed, Nothing, Sub() EjectHardDisk(channelInBed, slaveInBed))
        Catch ex As Exception
            menuInBed.DropDownItems.Clear()
            menuInBed.DropDownItems.Add(New ToolStripMenuItem("Shelf error: " & ex.Message) With {.Enabled = False})
            menuInBed.DropDownItems.Add(New ToolStripSeparator())
            menuInBed.DropDownItems.Add("Open IDE-Drives folder", Nothing, Sub() OpenIdeDriveShelfFolder())
        End Try
    End Sub

    Private Sub SelectIdeShelfDrive(driveIdInBed As Integer,
                                    Optional channelInBed As Integer = 0,
                                    Optional slaveInBed As Boolean = False)
        If _ideDriveShelf Is Nothing Then Return
        Dim controllerInBed As IdeController = IdeControllerForChannelInBed(channelInBed)
        If ReadMachineInBed(Function() controllerInBed.GetDeviceKind(slaveInBed)) <> IdeDeviceKind.AtaHardDisk Then
            MessageBox.Show(
                Me,
                IdeSlotDisplayNameInBed(channelInBed, slaveInBed) &
                " is not configured as an ATA hard disk. Select ATA hard disk in System Configuration first.",
                "ATA hard disk not installed",
                MessageBoxButtons.OK,
                MessageBoxIcon.Information)
            Return
        End If

        Dim otherChannelInBed As Integer = -1
        Dim otherSlaveInBed As Boolean = False
        If FindMountedIdeSlotForDriveInBed(driveIdInBed, channelInBed, slaveInBed, otherChannelInBed, otherSlaveInBed) Then
            MessageBox.Show(
                Me,
                "Shelf drive #" & driveIdInBed.ToString() &
                " is already attached to " &
                IdeSlotDisplayNameInBed(otherChannelInBed, otherSlaveInBed).ToLowerInvariant() &
                ". A writable disk image cannot be plugged into two IDE sockets at once.",
                "IDE drive already attached",
                MessageBoxButtons.OK,
                MessageBoxIcon.Warning)
            Return
        End If

        _ideDriveShelf.SetConfiguredDriveId(channelInBed, driveIdInBed, slaveInBed)
        _ideDriveShelf.SaveConfiguration()
        AttachConfiguredIdeDiskInBed(channelInBed, slaveInBed, silent:=False)
        UpdateMediaStatus()
        RebuildIdeDriveShelfMenu()

        If MountedIdeDriveIdInBed(channelInBed, slaveInBed) = driveIdInBed Then
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
        Dim attachedHardDisksInBed As Integer
        Dim installedHardDisksInBed As Integer
        Dim mountedCdMediaInBed As Integer
        Dim installedCdDrivesInBed As Integer

        WithMachineInBed(
            Sub()
                floppyAPresentInBed = FloppyController.IsMediaPresent(0)
                floppyBPresentInBed = FloppyController.IsMediaPresent(1)
                floppyATextInBed = FloppyController.GetAttachmentStatus(0)
                floppyBTextInBed = FloppyController.GetAttachmentStatus(1)
                floppyAGeometryInBed = FloppyController.GetGeometry(0)
                floppyBGeometryInBed = FloppyController.GetGeometry(1)
                For channelInBed As Integer = 0 To 1
                    Dim controllerInBed As IdeController = IdeControllerForChannelInBed(channelInBed)
                    For Each slaveInBed As Boolean In New Boolean() {False, True}
                        Select Case controllerInBed.GetDeviceKind(slaveInBed)
                            Case IdeDeviceKind.AtaHardDisk
                                installedHardDisksInBed += 1
                                If controllerInBed.GetHardDiskSectorCount(slaveInBed) > 0 Then attachedHardDisksInBed += 1
                            Case IdeDeviceKind.AtapiCdRom
                                installedCdDrivesInBed += 1
                                If controllerInBed.IsCdRomMounted(slaveInBed) Then mountedCdMediaInBed += 1
                        End Select
                    Next
                Next
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
            hardDiskStatus.Checked = attachedHardDisksInBed > 0
            hardDiskStatus.Text = "IDE hard disks — " & attachedHardDisksInBed.ToString() &
                                  "/" & installedHardDisksInBed.ToString() & " attached"
        End If
        If cdRomStatus IsNot Nothing Then
            cdRomStatus.Checked = mountedCdMediaInBed > 0
            cdRomStatus.Text = "ATAPI CD-ROMs — " & mountedCdMediaInBed.ToString() &
                               "/" & installedCdDrivesInBed.ToString() & " with media"
        End If
        UpdateDriveBayFacesInBed(floppyAGeometryInBed, floppyBGeometryInBed)
    End Sub

    Private Sub EjectFloppy(drive As Integer)
        WithMachineInBed(Sub() FloppyController.Eject(drive))
        UpdateMediaStatus()
    End Sub

    Private Sub EjectHardDisk(Optional channelInBed As Integer = 0,
                              Optional slaveInBed As Boolean = False)
        If _ideDriveShelf IsNot Nothing Then
            _ideDriveShelf.SetConfiguredDriveId(channelInBed, -1, slaveInBed)
            _ideDriveShelf.SaveConfiguration()
        End If
        Dim controllerInBed As IdeController = IdeControllerForChannelInBed(channelInBed)
        WithMachineInBed(Sub() controllerInBed.EjectHardDisk(slaveInBed))
        SetMountedIdeDriveIdInBed(channelInBed, -1, slaveInBed)
        UpdateMediaStatus()
        If ideDriveShelfMenu IsNot Nothing Then RebuildIdeDriveShelfMenu()
    End Sub

    Private Sub EjectCdRom(Optional channelInBed As Integer = 0,
                           Optional slaveInBed As Boolean = True)
        Dim controllerInBed As IdeController = IdeControllerForChannelInBed(channelInBed)
        WithMachineInBed(Sub() controllerInBed.EjectCdRom(slaveInBed))
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
            Dim primaryKindInBed As IdeDeviceKind = ReadMachineInBed(Function() Declares.IdeController.GetDeviceKind(False))
            If primaryKindInBed <> IdeDeviceKind.AtaHardDisk Then
                Throw New InvalidOperationException("Primary master is not configured as an ATA hard disk.")
            End If
            If ReadMachineInBed(Function() Declares.IdeController.GetHardDiskSectorCount(False)) = 0 Then
                Throw New InvalidOperationException("Mount a hard-disk image on primary master first.")
            End If
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
                    If CPU0 IsNot Nothing Then CPU0.HostFirmwareInterrupts = False
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
            IsaBackplane.SetChassisPowered(False)
            _machinePoweredInBed = False
            FrontPanel.SetCpuStateByte(0)
            FrontPanel.SetPower(False)
            ShowPowerOffArtworkInBed()

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
                        MotherboardMemory.ConfigureInstalledMemoryMegabytes(_ramBanks.InstalledMemoryMb,
                                                                          clearRam:=True)
                    End If

                    If CPU0 IsNot Nothing Then CPU0.HostFirmwareInterrupts = False
                    PowerCycleHardwareMachine()
                    ApplyFirmwareResetVectorSafetyGuardInBed()
                    FrontPanel.SetCpuStateByte(0)
                End Sub)
            _machineRuntime.ResetHostSchedulingState()

            _machinePoweredInBed = True
            FrontPanel.SetPower(True)
            PictureBox1.SizeMode = PictureBoxSizeMode.Zoom
            IsaBackplane.SetChassisPowered(True)
            _machineRuntime.Start()
            GPU.Enabled = (VideoCard IsNot Nothing)

            Dim presentationInBed As DiamondStealthPro928PresentationWorker = _videoPresentation
            If presentationInBed IsNot Nothing Then presentationInBed.RequestFrame()
        Catch
            IsaBackplane.SetChassisPowered(False)
            _machinePoweredInBed = False
            FrontPanel.SetPower(False)
            Throw
        Finally
            UpdatePowerControlsInBed()
        End Try
    End Sub

    Private Sub ShowPowerOffArtworkInBed()
        Dim presentationInBed As DiamondStealthPro928PresentationWorker = _videoPresentation
        Dim previousFrameInBed As Bitmap = _displayedPresentationFrameInBed

        _displayedPresentationFrameInBed = Nothing
        PictureBox1.SizeMode = PictureBoxSizeMode.StretchImage
        PictureBox1.Image = _powerOffDisplayImageInBed
        PictureBox1.Invalidate()

        If presentationInBed IsNot Nothing AndAlso previousFrameInBed IsNot Nothing Then
            presentationInBed.RecycleFrame(previousFrameInBed)
        End If
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

        ' The fixed-disk and optical fronts are intentionally derived from the
        ' supplied blank 5.25-inch plate.  That keeps the whole chassis in one
        ' period-beige visual family without inventing extra guest-visible drives.
        _cdRomDriveFaceInBed = BuildCdRomDriveFaceInBed(_emptyDriveFaceInBed)
        _mfmHardDiskDriveFaceInBed = BuildMfmHardDiskDriveFaceInBed(_emptyDriveFaceInBed)

        Const bayRailWidthInBed As Integer = 210
        Const displayGapInBed As Integer = 10
        Const firstBayTopInBed As Integer = 8
        Const bayHeightInBed As Integer = 58
        Const bayGapInBed As Integer = 4
        Const bayPitchInBed As Integer = bayHeightInBed + bayGapInBed

        Dim originalDisplayLeftInBed As Integer = PictureBox1.Left
        _driveBayPanelInBed = New Panel() With {
            .Location = New Point(originalDisplayLeftInBed, PictureBox1.Top),
            .Size = New Size(bayRailWidthInBed, PictureBox1.Height),
            .Anchor = AnchorStyles.Top Or AnchorStyles.Bottom Or AnchorStyles.Left,
            .BackColor = Color.FromArgb(178, 176, 158),
            .BorderStyle = BorderStyle.FixedSingle
        }
        _driveBayToolTipInBed = New ToolTip()

        ' Physical chassis order.  The two standard IDE channels are now
        ' represented honestly as four independent electrical positions. System
        ' Configuration may fit ATA, ATAPI, or no device in any position; the
        ' runtime facade is refreshed from that hardware choice after power cycle.
        _floppyBayAInBed = CreateFloppyBayFaceInBed(0, firstBayTopInBed)
        _floppyBayBInBed = CreateFloppyBayFaceInBed(1, firstBayTopInBed + bayPitchInBed)
        _cdRomBayInBed = CreateFixedBayFaceInBed(
            _cdRomDriveFaceInBed,
            firstBayTopInBed + (bayPitchInBed * 2),
            Cursors.Hand,
            "Channel 1 Drive 1 (Slave) — click for IDE device/media choices",
            Sub() ShowIdeBayMenuInBed(0, True))
        _cdRomBaySecondaryInBed = CreateFixedBayFaceInBed(
            _cdRomDriveFaceInBed,
            firstBayTopInBed + (bayPitchInBed * 3),
            Cursors.Hand,
            "Channel 2 Drive 1 (Slave) — click for IDE device/media choices",
            Sub() ShowIdeBayMenuInBed(1, True))
        _hardDiskBayInBed = CreateFixedBayFaceInBed(
            _mfmHardDiskDriveFaceInBed,
            firstBayTopInBed + (bayPitchInBed * 4),
            Cursors.Hand,
            "Channel 1 Drive 0 (Master) — click for IDE device/media choices",
            Sub() ShowIdeBayMenuInBed(0, False))
        _hardDiskBaySecondaryInBed = CreateFixedBayFaceInBed(
            _mfmHardDiskDriveFaceInBed,
            firstBayTopInBed + (bayPitchInBed * 5),
            Cursors.Hand,
            "Channel 2 Drive 0 (Master) — click for IDE device/media choices",
            Sub() ShowIdeBayMenuInBed(1, False))

        _driveBayPanelInBed.Controls.Add(_floppyBayAInBed)
        _driveBayPanelInBed.Controls.Add(_floppyBayBInBed)
        _driveBayPanelInBed.Controls.Add(_cdRomBayInBed)
        _driveBayPanelInBed.Controls.Add(_cdRomBaySecondaryInBed)
        _driveBayPanelInBed.Controls.Add(_hardDiskBayInBed)
        _driveBayPanelInBed.Controls.Add(_hardDiskBaySecondaryInBed)
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

    Private Shared Function BuildCdRomDriveFaceInBed(blankFaceInBed As Bitmap) As Bitmap
        Dim resultInBed As New Bitmap(blankFaceInBed)
        Using graphicsInBed As Graphics = Graphics.FromImage(resultInBed)
            graphicsInBed.SmoothingMode = System.Drawing.Drawing2D.SmoothingMode.AntiAlias
            graphicsInBed.TextRenderingHint = System.Drawing.Text.TextRenderingHint.ClearTypeGridFit

            Dim trayInBed As New Rectangle(78, 63, resultInBed.Width - 178, 138)
            Using trayShadowInBed As New Pen(Color.FromArgb(105, 102, 90), 5.0F),
                  trayLineInBed As New Pen(Color.FromArgb(73, 72, 65), 3.0F),
                  trayFillInBed As New SolidBrush(Color.FromArgb(166, 165, 148))
                graphicsInBed.FillRectangle(trayFillInBed, trayInBed)
                graphicsInBed.DrawRectangle(trayShadowInBed, trayInBed)
                graphicsInBed.DrawLine(trayLineInBed,
                                       trayInBed.Left + 14,
                                       trayInBed.Bottom - 22,
                                       trayInBed.Right - 14,
                                       trayInBed.Bottom - 22)
            End Using

            Using darkInBed As New SolidBrush(Color.FromArgb(62, 62, 57))
                graphicsInBed.FillEllipse(darkInBed, resultInBed.Width - 142, 200, 18, 18)
                graphicsInBed.FillRectangle(darkInBed, resultInBed.Width - 101, 198, 48, 21)
            End Using
            Using ledInBed As New SolidBrush(Color.FromArgb(72, 105, 49))
                graphicsInBed.FillRectangle(ledInBed, resultInBed.Width - 187, 201, 23, 15)
            End Using

            Using labelFontInBed As New Font("Arial", 24.0F, FontStyle.Bold, GraphicsUnit.Pixel),
                  detailFontInBed As New Font("Arial", 17.0F, FontStyle.Regular, GraphicsUnit.Pixel),
                  labelBrushInBed As New SolidBrush(Color.FromArgb(112, 111, 99))
                graphicsInBed.DrawString("ATAPI CD-ROM", labelFontInBed, labelBrushInBed, 91.0F, 215.0F)
                graphicsInBed.DrawString("4X", detailFontInBed, labelBrushInBed, 91.0F, 247.0F)
            End Using
        End Using
        Return resultInBed
    End Function

    Private Shared Function BuildMfmHardDiskDriveFaceInBed(blankFaceInBed As Bitmap) As Bitmap
        Dim resultInBed As New Bitmap(blankFaceInBed)
        Using graphicsInBed As Graphics = Graphics.FromImage(resultInBed)
            graphicsInBed.SmoothingMode = System.Drawing.Drawing2D.SmoothingMode.AntiAlias
            graphicsInBed.TextRenderingHint = System.Drawing.Text.TextRenderingHint.ClearTypeGridFit

            ' A deliberately chunky late-80s full-width fixed-disk facade:
            ' vent field, recessed service plate, key/lock detail and red activity LED.
            Dim recessInBed As New Rectangle(70, 58, resultInBed.Width - 140, 156)
            Using recessFillInBed As New SolidBrush(Color.FromArgb(154, 153, 137)),
                  recessEdgeInBed As New Pen(Color.FromArgb(105, 103, 91), 5.0F)
                graphicsInBed.FillRectangle(recessFillInBed, recessInBed)
                graphicsInBed.DrawRectangle(recessEdgeInBed, recessInBed)
            End Using

            Using ventBrushInBed As New SolidBrush(Color.FromArgb(83, 82, 74))
                For ventInBed As Integer = 0 To 10
                    Dim xInBed As Integer = 104 + (ventInBed * 39)
                    graphicsInBed.FillRectangle(ventBrushInBed, xInBed, 90, 19, 86)
                Next
            End Using

            Using plateFillInBed As New SolidBrush(Color.FromArgb(184, 182, 162)),
                  plateEdgeInBed As New Pen(Color.FromArgb(108, 106, 94), 3.0F)
                graphicsInBed.FillRectangle(plateFillInBed, 572, 84, 329, 98)
                graphicsInBed.DrawRectangle(plateEdgeInBed, 572, 84, 329, 98)
            End Using

            Using labelFontInBed As New Font("Arial", 25.0F, FontStyle.Bold, GraphicsUnit.Pixel),
                  detailFontInBed As New Font("Arial", 17.0F, FontStyle.Regular, GraphicsUnit.Pixel),
                  labelBrushInBed As New SolidBrush(Color.FromArgb(89, 87, 76))
                graphicsInBed.DrawString("FIXED DISK", labelFontInBed, labelBrushInBed, 603.0F, 98.0F)
                graphicsInBed.DrawString("MFM / RLL", detailFontInBed, labelBrushInBed, 604.0F, 136.0F)
            End Using

            Using ledBezelInBed As New SolidBrush(Color.FromArgb(70, 62, 58)),
                  ledInBed As New SolidBrush(Color.FromArgb(132, 28, 23))
                graphicsInBed.FillRectangle(ledBezelInBed, 930, 92, 63, 44)
                graphicsInBed.FillRectangle(ledInBed, 941, 103, 41, 22)
            End Using

            Using lockBezelInBed As New SolidBrush(Color.FromArgb(82, 81, 73)),
                  lockFaceInBed As New SolidBrush(Color.FromArgb(197, 195, 174))
                graphicsInBed.FillEllipse(lockBezelInBed, 928, 153, 48, 48)
                graphicsInBed.FillEllipse(lockFaceInBed, 937, 162, 30, 30)
                graphicsInBed.FillRectangle(lockBezelInBed, 950, 168, 4, 18)
            End Using
        End Using
        Return resultInBed
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

    Private Function CreateFixedBayFaceInBed(imageInBed As Image,
                                             topInBed As Integer,
                                             cursorInBed As Cursor,
                                             toolTipInBed As String,
                                             clickInBed As Action) As PictureBox
        Dim faceInBed As New PictureBox() With {
            .Location = New Point(7, topInBed),
            .Size = New Size(194, 58),
            .Anchor = AnchorStyles.Top Or AnchorStyles.Left Or AnchorStyles.Right,
            .SizeMode = PictureBoxSizeMode.Zoom,
            .BackColor = Color.FromArgb(178, 176, 158),
            .Cursor = cursorInBed,
            .Image = imageInBed,
            .TabStop = False
        }
        _driveBayToolTipInBed.SetToolTip(faceInBed, toolTipInBed)
        If clickInBed IsNot Nothing Then
            AddHandler faceInBed.Click, Sub() clickInBed()
        End If
        Return faceInBed
    End Function

    Private Sub DisposeDriveBayPopupInBed()
        If _driveBayPopupInBed Is Nothing Then Return
        _driveBayPopupInBed.Dispose()
        _driveBayPopupInBed = Nothing
    End Sub

    Private Sub ShowFloppyBayMenuInBed(faceInBed As PictureBox, driveInBed As Integer)
        If faceInBed Is Nothing OrElse faceInBed.IsDisposed Then Return
        DisposeDriveBayPopupInBed()

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
        sourceInBed.Dispose()
        _driveBayPopupInBed = popupInBed
        popupInBed.Show(faceInBed, New Point(0, faceInBed.Height))
    End Sub

    Private Function IdeBayForSlotInBed(channelInBed As Integer, slaveInBed As Boolean) As PictureBox
        Select Case channelInBed
            Case 0
                Return If(slaveInBed, _cdRomBayInBed, _hardDiskBayInBed)
            Case 1
                Return If(slaveInBed, _cdRomBaySecondaryInBed, _hardDiskBaySecondaryInBed)
            Case Else
                Throw New ArgumentOutOfRangeException(NameOf(channelInBed))
        End Select
    End Function

    Private Sub ShowIdeBayMenuInBed(channelInBed As Integer, slaveInBed As Boolean)
        Dim kindInBed As IdeDeviceKind =
            ReadMachineInBed(Function() IdeControllerForChannelInBed(channelInBed).GetDeviceKind(slaveInBed))
        Select Case kindInBed
            Case IdeDeviceKind.AtaHardDisk
                ShowHardDiskBayMenuInBed(channelInBed, slaveInBed)
            Case IdeDeviceKind.AtapiCdRom
                ShowCdRomBayMenuInBed(channelInBed, slaveInBed)
            Case Else
                Dim faceInBed As PictureBox = IdeBayForSlotInBed(channelInBed, slaveInBed)
                If faceInBed Is Nothing OrElse faceInBed.IsDisposed Then Return
                DisposeDriveBayPopupInBed()
                Dim popupInBed As New ContextMenuStrip()
                popupInBed.Items.Add(New ToolStripMenuItem(IdeSlotDisplayNameInBed(channelInBed, slaveInBed) & " — empty IDE socket") With {.Enabled = False})
                popupInBed.Items.Add(New ToolStripMenuItem("Install ATA or ATAPI hardware from System Configuration, then power-cycle.") With {.Enabled = False})
                _driveBayPopupInBed = popupInBed
                popupInBed.Show(faceInBed, New Point(0, faceInBed.Height))
        End Select
    End Sub

    Private Sub ShowCdRomBayMenuInBed(channelInBed As Integer, slaveInBed As Boolean)
        Dim faceInBed As PictureBox = IdeBayForSlotInBed(channelInBed, slaveInBed)
        If faceInBed Is Nothing OrElse faceInBed.IsDisposed Then Return
        DisposeDriveBayPopupInBed()

        Dim controllerInBed As IdeController = IdeControllerForChannelInBed(channelInBed)
        Dim kindInBed As IdeDeviceKind
        Dim cdPresentInBed As Boolean
        ReadMachineInBed(
            Function()
                kindInBed = controllerInBed.GetDeviceKind(slaveInBed)
                cdPresentInBed = controllerInBed.IsCdRomMounted(slaveInBed)
                Return True
            End Function)

        Dim popupInBed As New ContextMenuStrip()
        If kindInBed <> IdeDeviceKind.AtapiCdRom Then
            popupInBed.Items.Add(New ToolStripMenuItem(IdeSlotDisplayNameInBed(channelInBed, slaveInBed) & " is not an ATAPI device") With {.Enabled = False})
        Else
            popupInBed.Items.Add(New ToolStripMenuItem(
                IdeSlotDisplayNameInBed(channelInBed, slaveInBed) & " ATAPI CD-ROM — " &
                If(cdPresentInBed, "media inserted", "tray empty")) With {.Enabled = False})
            popupInBed.Items.Add(New ToolStripSeparator())
            popupInBed.Items.Add("Disc Box / Optical Utilities...", Nothing, Sub() RunSneakerNet("optical", channelInBed, slaveInBed))
            popupInBed.Items.Add("Mount CD-ROM ISO...", Nothing, Sub() ChooseIso(channelInBed, slaveInBed))
            Dim ejectInBed As ToolStripItem = popupInBed.Items.Add("Eject CD-ROM", Nothing, Sub() EjectCdRom(channelInBed, slaveInBed))
            ejectInBed.Enabled = cdPresentInBed
        End If

        _driveBayPopupInBed = popupInBed
        popupInBed.Show(faceInBed, New Point(0, faceInBed.Height))
    End Sub

    Private Sub ShowHardDiskBayMenuInBed(channelInBed As Integer, slaveInBed As Boolean)
        Dim faceInBed As PictureBox = IdeBayForSlotInBed(channelInBed, slaveInBed)
        If faceInBed Is Nothing OrElse faceInBed.IsDisposed Then Return
        DisposeDriveBayPopupInBed()

        Dim controllerInBed As IdeController = IdeControllerForChannelInBed(channelInBed)
        Dim kindInBed As IdeDeviceKind
        Dim hardDiskPresentInBed As Boolean
        Dim identityInBed As HardDiskIdentity = Nothing
        ReadMachineInBed(
            Function()
                kindInBed = controllerInBed.GetDeviceKind(slaveInBed)
                hardDiskPresentInBed = controllerInBed.GetHardDiskSectorCount(slaveInBed) > 0
                identityInBed = controllerInBed.GetHardDiskIdentity(slaveInBed)
                Return True
            End Function)

        Dim popupInBed As New ContextMenuStrip()
        If kindInBed <> IdeDeviceKind.AtaHardDisk Then
            popupInBed.Items.Add(New ToolStripMenuItem(IdeSlotDisplayNameInBed(channelInBed, slaveInBed) & " is not an ATA hard disk") With {.Enabled = False})
        Else
            Dim modelTextInBed As String = If(identityInBed Is Nothing, "ATA hard disk", identityInBed.Manufacturer & " " & identityInBed.Model)
            popupInBed.Items.Add(New ToolStripMenuItem(
                IdeSlotDisplayNameInBed(channelInBed, slaveInBed) & " — " & modelTextInBed & " — " &
                If(hardDiskPresentInBed, "attached", "no disk image attached")) With {.Enabled = False})
            popupInBed.Items.Add(New ToolStripSeparator())

            Dim shelfInBed As New ToolStripMenuItem("IDE Drive Shelf")
            PopulateIdeDriveShelfMenuInBed(shelfInBed, channelInBed, slaveInBed)
            popupInBed.Items.Add(shelfInBed)
            popupInBed.Items.Add("Hard Drive Utilities...", Nothing, Sub() RunSneakerNet("harddisk", channelInBed, slaveInBed))
            popupInBed.Items.Add(New ToolStripSeparator())
            popupInBed.Items.Add("Mount hard-disk image...", Nothing, Sub() ChooseHardDisk(channelInBed, slaveInBed))
            popupInBed.Items.Add("Create selected-model IDE shelf drive...", Nothing, Sub() CreateHardDisk(channelInBed, slaveInBed))
            popupInBed.Items.Add("Open IDE-Drives folder", Nothing, Sub() OpenIdeDriveShelfFolder())
            popupInBed.Items.Add(New ToolStripSeparator())

            If channelInBed = 0 AndAlso Not slaveInBed Then
                Dim bootInBed As ToolStripItem = popupInBed.Items.Add("Boot hard disk", Nothing, Sub() BootHardDisk())
                bootInBed.Enabled = hardDiskPresentInBed
            Else
                popupInBed.Items.Add(New ToolStripMenuItem("BIOS boot path — primary master only") With {.Enabled = False})
            End If

            Dim ejectInBed As ToolStripItem = popupInBed.Items.Add("Disconnect hard disk", Nothing, Sub() EjectHardDisk(channelInBed, slaveInBed))
            ejectInBed.Enabled = hardDiskPresentInBed OrElse
                                 (_ideDriveShelf IsNot Nothing AndAlso _ideDriveShelf.GetConfiguredDriveId(channelInBed, slaveInBed) >= 0)
        End If

        _driveBayPopupInBed = popupInBed
        popupInBed.Show(faceInBed, New Point(0, faceInBed.Height))
    End Sub

    Private Sub UpdateDriveBayFacesInBed(geometryAInBed As Integer(), geometryBInBed As Integer())
        UpdateDriveBayFaceInBed(_floppyBayAInBed, 0, geometryAInBed)
        UpdateDriveBayFaceInBed(_floppyBayBInBed, 1, geometryBInBed)

        For channelInBed As Integer = 0 To 1
            Dim controllerInBed As IdeController = IdeControllerForChannelInBed(channelInBed)
            For Each slaveInBed As Boolean In New Boolean() {False, True}
                Dim kindInBed As IdeDeviceKind
                Dim hardDiskPresentInBed As Boolean
                Dim cdPresentInBed As Boolean
                Dim identityInBed As HardDiskIdentity = Nothing
                ReadMachineInBed(
                    Function()
                        kindInBed = controllerInBed.GetDeviceKind(slaveInBed)
                        hardDiskPresentInBed = controllerInBed.GetHardDiskSectorCount(slaveInBed) > 0
                        cdPresentInBed = controllerInBed.IsCdRomMounted(slaveInBed)
                        identityInBed = controllerInBed.GetHardDiskIdentity(slaveInBed)
                        Return True
                    End Function)

                Dim faceInBed As PictureBox = IdeBayForSlotInBed(channelInBed, slaveInBed)
                If faceInBed Is Nothing Then Continue For
                Dim slotNameInBed As String = IdeSlotDisplayNameInBed(channelInBed, slaveInBed)

                Select Case kindInBed
                    Case IdeDeviceKind.AtaHardDisk
                        faceInBed.Image = _mfmHardDiskDriveFaceInBed
                        faceInBed.Cursor = Cursors.Hand
                        Dim modelTextInBed As String = If(identityInBed Is Nothing, "ATA hard disk", identityInBed.Model)
                        _driveBayToolTipInBed.SetToolTip(faceInBed, slotNameInBed & " — " & modelTextInBed & " — " & If(hardDiskPresentInBed, "attached", "no disk image attached") & "; click for drive shelf and utilities")
                    Case IdeDeviceKind.AtapiCdRom
                        faceInBed.Image = _cdRomDriveFaceInBed
                        faceInBed.Cursor = Cursors.Hand
                        _driveBayToolTipInBed.SetToolTip(faceInBed, slotNameInBed & " — ATAPI CD-ROM — " & If(cdPresentInBed, "media inserted", "tray empty") & "; click for optical media choices")
                    Case Else
                        faceInBed.Image = _emptyDriveFaceInBed
                        faceInBed.Cursor = Cursors.Hand
                        _driveBayToolTipInBed.SetToolTip(faceInBed, slotNameInBed & " — empty IDE socket; click for configuration hint")
                End Select
            Next
        Next
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

    Private Sub RunSneakerNet(Optional mediaPageInBed As String = Nothing,
                              Optional ideChannelInBed As Integer = 0,
                              Optional ideSlaveInBed As Boolean = False)
        If ideChannelInBed < 0 OrElse ideChannelInBed > 1 Then Throw New ArgumentOutOfRangeException(NameOf(ideChannelInBed))
        _sneakerNetIdeTargetInBed = ideChannelInBed
        _sneakerNetIdeTargetSlaveInBed = ideSlaveInBed
        _sneakerNetOpticalTargetInBed = ideChannelInBed
        _sneakerNetOpticalTargetSlaveInBed = ideSlaveInBed
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
                    SelectIdeShelfDrive(driveIdInBed, _sneakerNetIdeTargetInBed, _sneakerNetIdeTargetSlaveInBed)
                    UpdateMediaStatus()
                End Sub,
                Sub()
                    EjectHardDisk(_sneakerNetIdeTargetInBed, _sneakerNetIdeTargetSlaveInBed)
                    UpdateMediaStatus()
                End Sub,
                Function() MountedIdeDriveIdInBed(_sneakerNetIdeTargetInBed, _sneakerNetIdeTargetSlaveInBed),
                Sub(imagePathInBed As String)
                    WithMachineInBed(Sub() MountIsoImage(imagePathInBed, _sneakerNetOpticalTargetInBed, _sneakerNetOpticalTargetSlaveInBed))
                    UpdateMediaStatus()
                End Sub,
                Sub()
                    EjectCdRom(_sneakerNetOpticalTargetInBed, _sneakerNetOpticalTargetSlaveInBed)
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

        If Not String.IsNullOrWhiteSpace(mediaPageInBed) Then _sneakerNetForm.ShowMediaPageInBed(mediaPageInBed)
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

    Private Sub ChooseHardDisk(Optional channelInBed As Integer = 0,
                               Optional slaveInBed As Boolean = False)
        Dim controllerInBed As IdeController = IdeControllerForChannelInBed(channelInBed)
        If ReadMachineInBed(Function() controllerInBed.GetDeviceKind(slaveInBed)) <> IdeDeviceKind.AtaHardDisk Then
            MessageBox.Show(Me, IdeSlotDisplayNameInBed(channelInBed, slaveInBed) &
                            " is not configured as an ATA hard disk. Select ATA hard disk in System Configuration first.",
                            "ATA hard disk not installed", MessageBoxButtons.OK, MessageBoxIcon.Information)
            Return
        End If

        Using picker As New OpenFileDialog()
            picker.Title = "Mount hard-disk image on " & IdeSlotDisplayNameInBed(channelInBed, slaveInBed).ToLowerInvariant()
            picker.Filter = "Raw disk images (*.img;*.hdd)|*.img;*.hdd|All files (*.*)|*.*"
            If picker.ShowDialog(Me) <> DialogResult.OK Then Return
            Try
                WithMachineInBed(Sub() MountHardDiskImage(picker.FileName, False, channelInBed, slaveInBed))
                If _ideDriveShelf IsNot Nothing Then
                    _ideDriveShelf.SetConfiguredDriveId(channelInBed, -1, slaveInBed)
                    _ideDriveShelf.SaveConfiguration()
                End If
                SetMountedIdeDriveIdInBed(channelInBed, -1, slaveInBed)
                UpdateMediaStatus()
                RebuildIdeDriveShelfMenu()
            Catch ex As Exception
                MessageBox.Show(Me, ex.Message, "Unable to mount hard disk", MessageBoxButtons.OK, MessageBoxIcon.Error)
            End Try
        End Using
    End Sub

    Private Sub CreateHardDisk(Optional channelInBed As Integer = 0,
                               Optional slaveInBed As Boolean = False)
        If _ideDriveShelf Is Nothing Then Return
        Dim controllerInBed As IdeController = IdeControllerForChannelInBed(channelInBed)
        If ReadMachineInBed(Function() controllerInBed.GetDeviceKind(slaveInBed)) <> IdeDeviceKind.AtaHardDisk Then
            MessageBox.Show(Me, IdeSlotDisplayNameInBed(channelInBed, slaveInBed) &
                            " is not configured as an ATA hard disk. Select ATA hard disk in System Configuration first.",
                            "ATA hard disk not installed", MessageBoxButtons.OK, MessageBoxIcon.Information)
            Return
        End If

        _ideDriveShelf.EnsureShelfExists()
        Dim effectiveIdeInBed As IdeHardwareConfiguration = EffectiveIdeHardwareConfigurationInBed()
        Dim slotKeyInBed As String = IdeSlotKeyInBed(channelInBed, slaveInBed)
        Dim profileInBed As IdeDriveModelProfile = IdeDriveModelCatalog.FindById(effectiveIdeInBed.GetModelId(slotKeyInBed))
        If profileInBed Is Nothing Then profileInBed = IdeDriveModelCatalog.FindById(IdeDriveModelCatalog.Generic64MiBId)
        Dim labelInBed As String = Microsoft.VisualBasic.Interaction.InputBox(
            "Label for the new " & profileInBed.DisplayName & ":",
            "Create IDE Shelf Drive",
            profileInBed.DisplayName)
        If String.IsNullOrWhiteSpace(labelInBed) Then Return

        Try
            Dim driveIdInBed As Integer = _ideDriveShelf.NextAvailableId()
            Dim safeLabelInBed As String = IdeDriveShelf.SanitizeLabel(labelInBed)
            Dim imagePathInBed As String = Path.Combine(_ideDriveShelf.RootPath, driveIdInBed.ToString() & " - " & safeLabelInBed & ".hdd")
            HardDiskImage.Create(imagePathInBed, profileInBed)

            _ideDriveShelf.SetConfiguredDriveId(channelInBed, driveIdInBed, slaveInBed)
            _ideDriveShelf.SaveConfiguration()
            AttachConfiguredIdeDiskInBed(channelInBed, slaveInBed, silent:=False)
            UpdateMediaStatus()
            RebuildIdeDriveShelfMenu()
            If MountedIdeDriveIdInBed(channelInBed, slaveInBed) = driveIdInBed Then ResetThroughFirmware()
        Catch ex As Exception
            MessageBox.Show(Me, ex.Message, "Unable to create IDE shelf drive", MessageBoxButtons.OK, MessageBoxIcon.Error)
        End Try
    End Sub

    Private Sub ChooseIso(Optional channelInBed As Integer = 0,
                          Optional slaveInBed As Boolean = True)
        Dim controllerInBed As IdeController = IdeControllerForChannelInBed(channelInBed)
        If ReadMachineInBed(Function() controllerInBed.GetDeviceKind(slaveInBed)) <> IdeDeviceKind.AtapiCdRom Then
            MessageBox.Show(Me, IdeSlotDisplayNameInBed(channelInBed, slaveInBed) &
                            " is not configured as an ATAPI CD-ROM. Select ATAPI CD-ROM in System Configuration first.",
                            "ATAPI drive not installed", MessageBoxButtons.OK, MessageBoxIcon.Information)
            Return
        End If

        Using picker As New OpenFileDialog()
            picker.Title = "Mount CD-ROM image on " & IdeSlotDisplayNameInBed(channelInBed, slaveInBed).ToLowerInvariant()
            picker.Filter = "ISO images (*.iso)|*.iso|All files (*.*)|*.*"
            If picker.ShowDialog(Me) <> DialogResult.OK Then Return
            Try
                WithMachineInBed(Sub() MountIsoImage(picker.FileName, channelInBed, slaveInBed))
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

        WithMachineInBed(Sub()
                             If CPU0 IsNot Nothing Then CPU0.ResetHotPathProfiler()
                         End Sub)
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

        ' Performance-baseline mode deliberately keeps only the rate meter.
        ' The full diagnostic window used to format bus/CPU/chipset reports AND
        ' assign the WinForms label while owning the machine gate.  That makes
        ' the observer part of the workload we are trying to measure.
        If Not RuntimeDiagnostics.Enabled Then
            Dim targetHzInBed As Long
            Dim debtTStatesInBed As Long
            Dim sliceCeilingInBed As Long
            WithMachineInBed(
                Sub()
                    targetHzInBed = MachineClock.CpuClockHz
                    debtTStatesInBed = MachineClock.PendingTStates
                    sliceCeilingInBed = _machineRuntime.CurrentMaximumTStatesPerSlice
                End Sub)

            Dim effectiveHzInBed As Double
            Dim samplesReadyInBed As Boolean
            Dim lastSliceMsInBed As Double
            Dim averageSliceMsInBed As Double
            Dim minimumSliceMsInBed As Double
            Dim maximumSliceMsInBed As Double
            Dim sliceCountInBed As Long
            SyncLock _cpuPerfLockInBed
                effectiveHzInBed = _cpuPerfEffectiveTStatesPerSecond
                samplesReadyInBed = _cpuPerfSamplesReady
                lastSliceMsInBed = _cpuPerfLastSliceMilliseconds
                averageSliceMsInBed = _cpuPerfAverageSliceMillisecondsInBed
                minimumSliceMsInBed = _cpuPerfMinimumSliceMillisecondsInBed
                maximumSliceMsInBed = _cpuPerfMaximumSliceMillisecondsInBed
                sliceCountInBed = _cpuPerfLastWindowSliceCountInBed
            End SyncLock

            Dim ratioInBed As Double =
                If(targetHzInBed > 0, effectiveHzInBed / CDbl(targetHzInBed), 0.0)
            Dim debtMillisecondsInBed As Double =
                If(targetHzInBed > 0,
                   CDbl(debtTStatesInBed) * 1000.0 / CDbl(targetHzInBed),
                   0.0)
            Dim effectiveTextInBed As String =
                If(samplesReadyInBed,
                   (effectiveHzInBed / 1000000.0).ToString("0.000") & " MT-states/s",
                   "measuring...")
            Dim ratioTextInBed As String =
                If(samplesReadyInBed,
                   (ratioInBed * 100.0).ToString("0.0") & " %",
                   "measuring...")
            Dim statusInBed As String =
                If(Not samplesReadyInBed,
                   "MEASURING",
                   If(ratioInBed >= 0.95 AndAlso debtMillisecondsInBed < 100.0,
                      "KEEPING UP",
                      "FALLING BEHIND"))

            _cpuPerfText.Text =
                "Harris CS80C286 performance — DIAGNOSTICS OFF" & Environment.NewLine & Environment.NewLine &
                "Target clock           : " & (CDbl(targetHzInBed) / 1000000.0).ToString("0.000") & " MHz" & Environment.NewLine &
                "Effective T-state rate : " & effectiveTextInBed & Environment.NewLine &
                "Real-time ratio        : " & ratioTextInBed & Environment.NewLine &
                "Pending scheduler debt : " & debtTStatesInBed.ToString("N0") & " T-states" & Environment.NewLine &
                "Debt as guest time     : " & debtMillisecondsInBed.ToString("0.0") & " ms" & Environment.NewLine &
                "Last RunSlice host cost: " & lastSliceMsInBed.ToString("0.0") & " ms" & Environment.NewLine &
                "RunSlice avg/min/max    : " & averageSliceMsInBed.ToString("0.0") & " / " &
                                                minimumSliceMsInBed.ToString("0.0") & " / " &
                                                maximumSliceMsInBed.ToString("0.0") & " ms" & Environment.NewLine &
                "Slices in rate window  : " & sliceCountInBed.ToString("N0") & Environment.NewLine &
                "Slice ceiling          : " & sliceCeilingInBed.ToString("N0") & " T-states" & Environment.NewLine &
                "Runtime trace collectors: COMPILED OUT" & Environment.NewLine & Environment.NewLine &
                "Status                 : " & statusInBed
            Return
        End If

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
            If(CPU0 Is Nothing, "No CPU installed.", CPU0.HotPathDiagnosticText()) & Environment.NewLine & Environment.NewLine &
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
        If Not _machinePoweredInBed Then Return

        presentationInBed.RequestFrame()
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

    Private Sub ShowSingleStep80286TestsInBed()
        Dim formInBed As New SingleStep80286TestHostForm()
        formInBed.Show(Me)
    End Sub
End Class
'keyboard buffer address &h0:0100-&h0:01ff (tentative)
