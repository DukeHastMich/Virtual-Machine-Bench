Option Strict On
Namespace VirtualComputer
Public NotInheritable Class Intel2142
 Private ReadOnly _memory(1023) As Byte
 Public Function Cycle(address As Integer, cs1N As Boolean, cs2 As Boolean, weN As Boolean, Optional od As Boolean=False, Optional dataIn As Byte=0) As Byte?
  If address<0 OrElse address>=1024 Then Throw New ArgumentOutOfRangeException(NameOf(address))
  If cs1N OrElse Not cs2 Then Return Nothing
  If Not weN Then _memory(address)=CByte(dataIn And &HF):Return Nothing
  If od Then Return Nothing
  Return CByte(_memory(address) And &HF)
 End Function
End Class
End Namespace
