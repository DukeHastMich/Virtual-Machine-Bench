Option Strict On
Namespace VirtualComputer
Public NotInheritable Class Intel2101A
    Private ReadOnly _memory(255) As Byte
    ' Pin-compatible board-facing control projection: CE1 low, CE2 high selects; R/W high reads; OD high tri-states outputs.
    Public Function Cycle(address As Integer, ce1N As Boolean, ce2 As Boolean, rw As Boolean, Optional od As Boolean=False, Optional dataIn As Byte=0) As Byte?
        If address < 0 OrElse address >= 256 Then Throw New ArgumentOutOfRangeException(NameOf(address))
        If ce1N OrElse Not ce2 Then Return Nothing
        If Not rw Then _memory(address)=CByte(dataIn And &HF) : Return Nothing
        If od Then Return Nothing
        Return CByte(_memory(address) And &HF)
    End Function
End Class
End Namespace
