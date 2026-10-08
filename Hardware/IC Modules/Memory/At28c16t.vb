Imports System
Public NotInheritable Class VmbAt28c16t
 Private ReadOnly _data(2047) As Byte : Private _busyUntilNs As Long : Private _lastWritten As Byte
 Public Sub New(Optional contents As Byte()=Nothing)
  If contents Is Nothing Then : For i=0 To 2047 : _data(i)=&HFF : Next : Else : If contents.Length<>2048 Then Throw New ArgumentException("AT28C16-T image must be exactly 2048 bytes.") : Array.Copy(contents,_data,2048) : End If
 End Sub
 Public Function IsBusy(nowNs As Long) As Boolean : Return nowNs<_busyUntilNs : End Function
 Public Function ReadyBusyN(nowNs As Long) As Boolean : Return Not IsBusy(nowNs) : End Function
 Public Function ReadByte(address As Integer,Optional nowNs As Long=0) As Byte
  If address<0 OrElse address>=2048 Then Throw New ArgumentOutOfRangeException(NameOf(address))
  Dim v=_data(address) : If IsBusy(nowNs) Then v=CByte((v And &H7F) Or ((Not _lastWritten) And &H80)) : Return v
 End Function
 Public Function EvaluateRead(address As Integer,ceN As Boolean,oeN As Boolean,weN As Boolean,Optional nowNs As Long=0) As Nullable(Of Byte)
  If ceN OrElse oeN OrElse Not weN Then Return Nothing Else Return ReadByte(address,nowNs)
 End Function
 Public Sub WriteByte(address As Integer,value As Byte,Optional nowNs As Long=0,Optional writeCycleNs As Long=1000000)
  If address<0 OrElse address>=2048 Then Throw New ArgumentOutOfRangeException(NameOf(address)) : If IsBusy(nowNs) Then Return : _data(address)=value : _lastWritten=value : _busyUntilNs=nowNs+writeCycleNs
 End Sub
End Class
