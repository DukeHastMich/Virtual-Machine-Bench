Imports System

' 27512/27C512-class 64K x 8 EPROM installed-machine read behavior.
' Programming voltage/algorithm remain external programmer operations.
Public NotInheritable Class VmbEprom27512
    Private ReadOnly _data(65535) As Byte
    Public Sub New(contents As Byte())
        If contents Is Nothing OrElse contents.Length <> 65536 Then Throw New ArgumentException("27512 payload must be exactly 65536 bytes.", NameOf(contents))
        Array.Copy(contents, _data, contents.Length)
    End Sub
    Public Function ReadByte(address As Integer) As Byte
        If address < 0 OrElse address >= 65536 Then Throw New ArgumentOutOfRangeException(NameOf(address))
        Return _data(address)
    End Function
    Public Function EvaluateRead(address As Integer, ceN As Boolean, oeN As Boolean) As Nullable(Of Byte)
        If ceN OrElse oeN Then Return Nothing
        Return ReadByte(address)
    End Function
End Class
