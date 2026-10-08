Option Strict On
Namespace VirtualComputer
Public NotInheritable Class Intel2147H
 Private ReadOnly _memory(4095) As Byte
 Public Property Standby As Boolean=True
 Public Function Cycle(address As Integer, csN As Boolean, weN As Boolean, Optional dataIn As Boolean=False) As Boolean?
  If address<0 OrElse address>=4096 Then Throw New ArgumentOutOfRangeException(NameOf(address))
  Standby=csN
  If csN Then Return Nothing
  If Not weN Then _memory(address)=If(dataIn,CByte(1),CByte(0)):Return Nothing
  Return (_memory(address) And 1)<>0
 End Function
End Class
End Namespace
