Imports System

' 27128A-class 16K x 8 EPROM installed-machine read behavior.
' UV erase and elevated-voltage programming are external programmer operations.
Public NotInheritable Class VmbEprom27128
    Private ReadOnly _data(16383) As Byte
    Public Sub New(contents As Byte())
        If contents Is Nothing OrElse contents.Length <> 16384 Then Throw New ArgumentException("27128 payload must be exactly 16384 bytes.", NameOf(contents))
        Array.Copy(contents, _data, contents.Length)
    End Sub
    Public Function ReadByte(address As Integer) As Byte
        If address < 0 OrElse address >= 16384 Then Throw New ArgumentOutOfRangeException(NameOf(address))
        Return _data(address)
    End Function
    Public Function EvaluateRead(address As Integer, ceN As Boolean, oeN As Boolean, Optional pgmN As Boolean = True) As Nullable(Of Byte)
        ' Intel 27128A normal Read mode requires CE#=L, OE#=L, PGM#=H.
        If ceN OrElse oeN OrElse Not pgmN Then Return Nothing
        Return ReadByte(address)
    End Function
End Class
