Imports System
Public Class VmbIntel8286
    Public Const ModuleId As String="VMB.BUS.INTEL_8286"
    Protected Overridable ReadOnly Property Inverting As Boolean
        Get : Return False : End Get
    End Property
    Public Function DriveA(a As Byte,b As Byte,t As Boolean,oe_n As Boolean) As Nullable(Of Byte)
        If oe_n OrElse t Then Return Nothing
        If Inverting Then Return CByte(b Xor &HFF) Else Return b
    End Function
    Public Function DriveB(a As Byte,b As Byte,t As Boolean,oe_n As Boolean) As Nullable(Of Byte)
        If oe_n OrElse Not t Then Return Nothing
        If Inverting Then Return CByte(a Xor &HFF) Else Return a
    End Function
End Class
Public NotInheritable Class VmbIntel8287
    Inherits VmbIntel8286
    Public Shadows Const ModuleId As String="VMB.BUS.INTEL_8287"
    Protected Overrides ReadOnly Property Inverting As Boolean
        Get : Return True : End Get
    End Property
End Class
