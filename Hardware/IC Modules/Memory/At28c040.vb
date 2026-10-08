Imports System
Public NotInheritable Class VmbAt28c040
 Private ReadOnly _data(524287) As Byte
 Private _busyUntilNs As Long
 Public Sub New(Optional contents As Byte()=Nothing)
  If contents Is Nothing Then : For i=0 To _data.Length-1 : _data(i)=&HFF : Next : Else : If contents.Length<>524288 Then Throw New ArgumentException("AT28C040 image must be exactly 524288 bytes") : Array.Copy(contents,_data,524288) : End If
 End Sub
 Public Function EvaluateRead(a As Integer, ceN As Boolean, oeN As Boolean, weN As Boolean) As Nullable(Of Byte)
  If ceN OrElse oeN OrElse Not weN Then Return Nothing
  If a<0 OrElse a>=_data.Length Then Throw New ArgumentOutOfRangeException(NameOf(a))
  Return _data(a)
 End Function
End Class
