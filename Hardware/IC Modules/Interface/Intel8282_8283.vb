Imports System
Public Class VmbIntel8282
    Public Const ModuleId As String="VMB.BUS.INTEL_8282"
    Protected _latched As Byte
    Protected Overridable ReadOnly Property Inverting As Boolean
        Get : Return False : End Get
    End Property
    ' Nothing represents the physical high-impedance output state.
    Public Function StepPins(data As Byte, stb As Boolean, oe_n As Boolean) As Nullable(Of Byte)
        If stb Then _latched=data ' transparent while STB is active high
        If oe_n Then Return Nothing
        If Inverting Then Return CByte(_latched Xor &HFF) Else Return _latched
    End Function
End Class
Public NotInheritable Class VmbIntel8283
    Inherits VmbIntel8282
    Public Shadows Const ModuleId As String="VMB.BUS.INTEL_8283"
    Protected Overrides ReadOnly Property Inverting As Boolean
        Get : Return True : End Get
    End Property
End Class
