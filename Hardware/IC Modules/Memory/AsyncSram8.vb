Imports System

' Manual-backed asynchronous x8 SRAM core for 6116/6264/62256-class devices.
' Access-time enforcement is a board timing concern; this worker models computer-visible storage semantics.
Public NotInheritable Class VmbAsyncSram8
    Private ReadOnly _data() As Byte
    Public ReadOnly Property CapacityBytes As Integer
    Public Sub New(capacity As Integer)
        If capacity <= 0 OrElse (capacity And (capacity-1))<>0 Then Throw New ArgumentOutOfRangeException(NameOf(capacity))
        CapacityBytes=capacity : ReDim _data(capacity-1)
    End Sub
    Public Function ReadByte(address As Integer) As Byte
        If address<0 OrElse address>=CapacityBytes Then Throw New ArgumentOutOfRangeException(NameOf(address))
        Return _data(address)
    End Function
    Public Sub WriteByte(address As Integer, value As Byte)
        If address<0 OrElse address>=CapacityBytes Then Throw New ArgumentOutOfRangeException(NameOf(address))
        _data(address)=value
    End Sub
    ' CE#/OE#/WE# truth at digital resolution. Nothing means the bidirectional DQ bus is released.
    Public Function EvaluateRead(address As Integer, ceN As Boolean, oeN As Boolean, weN As Boolean) As Nullable(Of Byte)
        If ceN OrElse Not weN OrElse oeN Then Return Nothing
        Return ReadByte(address)
    End Function
    Public Sub ApplyWrite(address As Integer, value As Byte, ceN As Boolean, weN As Boolean)
        If Not ceN AndAlso Not weN Then WriteByte(address,value)
    End Sub
End Class
