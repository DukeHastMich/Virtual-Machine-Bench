Imports System
Imports System.Windows.Forms

Module Program
    <STAThread>
    Public Sub Main()
        Application.EnableVisualStyles()
        Application.SetCompatibleTextRenderingDefault(False)
        ' Install this filter before constructing Form1 so it gets first crack at
        ' Alt/F10 system-key traffic before MenuStrip/ToolStrip keyboard navigation.
        Dim keyboardFilterInBed As New GuestAltKeyboardMessageFilter()
        Application.AddMessageFilter(keyboardFilterInBed)
        Dim mainFormInBed As New Form1()
        keyboardFilterInBed.Target = mainFormInBed
        Try
            Application.Run(mainFormInBed)
        Finally
            Application.RemoveMessageFilter(keyboardFilterInBed)
        End Try
    End Sub
End Module

' Host-side arbitration only. This class never creates guest scan codes;
' it forwards the original Windows physical scan field to Form1's existing
' physical-key router before WinForms/MenuStrip can consume Alt/F10.
Public NotInheritable Class GuestAltKeyboardMessageFilter
    Implements IMessageFilter

    Private Const WM_KEYDOWN As Integer = &H100
    Private Const WM_KEYUP As Integer = &H101
    Private Const WM_SYSKEYDOWN As Integer = &H104
    Private Const WM_SYSKEYUP As Integer = &H105
    Private Const WM_SYSCHAR As Integer = &H106
    Private Const WM_SYSDEADCHAR As Integer = &H107

    Public Property Target As Form1

    Public Function PreFilterMessage(ByRef m As Message) As Boolean Implements IMessageFilter.PreFilterMessage
        Dim targetInBed As Form1 = Target
        If targetInBed Is Nothing OrElse targetInBed.IsDisposed Then Return False
        If Form.ActiveForm IsNot targetInBed Then Return False

        Select Case m.Msg
            Case WM_SYSKEYDOWN
                Return targetInBed.RoutePhysicalKeyboardMessage(m, pressed:=True)
            Case WM_SYSKEYUP
                Return targetInBed.RoutePhysicalKeyboardMessage(m, pressed:=False)
            Case WM_KEYDOWN, WM_KEYUP
                ' F10 is another Windows menu-activation key even without Alt.
                Dim virtualKeyInBed As Keys = CType(CInt(m.WParam.ToInt64() And &HFFFFL), Keys)
                If virtualKeyInBed = Keys.F10 Then
                    Return targetInBed.RoutePhysicalKeyboardMessage(m, pressed:=(m.Msg = WM_KEYDOWN))
                End If
            Case WM_SYSCHAR, WM_SYSDEADCHAR
                ' A consumed system-key transition must not leave a translated menu
                ' character behind for WinForms to beep on or activate a host menu.
                Return True
        End Select

        Return False
    End Function
End Class
