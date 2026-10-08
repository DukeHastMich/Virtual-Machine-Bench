Imports System
' CP68: Intel 8288 status decoder only. NOT a complete bus-controller timing model.
Public NotInheritable Class VmbIntel8288StatusDecoder
    Public Const ModuleId As String="VMB.BUS.INTEL_8288"
    Public Shared Function DecodeCommand(s2 As Boolean,s1 As Boolean,s0 As Boolean) As String
        Dim code=(If(s2,4,0) Or If(s1,2,0) Or If(s0,1,0))
        Select Case code
            Case 0 : Return "INTA#"
            Case 1 : Return "IORC#"
            Case 2 : Return "IOWC#"
            Case 4,5 : Return "MRDC#" ' instruction/code access and memory read
            Case 6 : Return "MWTC#"
            Case Else : Return Nothing ' halt/passive issue no command
        End Select
    End Function
End Class
