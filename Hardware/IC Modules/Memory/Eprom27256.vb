Imports System

' 27256-class 32K x 8 EPROM normal installed-machine behavior.
' Programming algorithm/VPP pulses are programmer-side behavior and are intentionally not fabricated here.
Public NotInheritable Class VmbEprom27256
    Private ReadOnly _data(32767) As Byte
    Public Sub New(contents As Byte())
        If contents Is Nothing OrElse contents.Length<>32768 Then Throw New ArgumentException("27256 payload must be exactly 32768 bytes.",NameOf(contents))
        Array.Copy(contents,_data,contents.Length)
    End Sub
    Public Function ReadByte(address As Integer) As Byte
        If address<0 OrElse address>=32768 Then Throw New ArgumentOutOfRangeException(NameOf(address))
        Return _data(address)
    End Function
    Public Function EvaluateRead(address As Integer, ceN As Boolean, oeN As Boolean) As Nullable(Of Byte)
        If ceN OrElse oeN Then Return Nothing
        Return ReadByte(address)
    End Function
End Class
