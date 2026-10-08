Option Strict On
Namespace VirtualComputer
Public NotInheritable Class Intel2102A
    Private ReadOnly _memory(1023) As Byte
    Public Sub WriteBit(address As Integer, value As Boolean)
        If address < 0 OrElse address >= 1024 Then Throw New ArgumentOutOfRangeException(NameOf(address))
        _memory(address) = If(value, CByte(1), CByte(0))
    End Sub
    Public Function ReadBit(address As Integer) As Boolean
        If address < 0 OrElse address >= 1024 Then Throw New ArgumentOutOfRangeException(NameOf(address))
        Return (_memory(address) And 1) <> 0
    End Function
    ' CE is active-low. R/W high=read, low=write. Nothing means DOUT is high impedance.
    Public Function Cycle(address As Integer, ce As Boolean, rw As Boolean, Optional dataIn As Boolean=False) As Boolean?
        If ce Then Return Nothing
        If Not rw Then WriteBit(address, dataIn) : Return Nothing
        Return ReadBit(address)
    End Function
End Class
End Namespace
