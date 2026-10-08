Imports System

' Intel 2148H 1024 x 4 high-speed static RAM.
' Board-facing logic is 2114-pinout compatible; CS deselection also enters automatic low-power standby.
Public NotInheritable Class Intel2148H
    Private ReadOnly _data(1023) As Byte

    Public Function ReadNibble(address As Integer) As Byte
        If address < 0 OrElse address >= 1024 Then Throw New ArgumentOutOfRangeException(NameOf(address))
        Return CByte(_data(address) And &HF)
    End Function

    Public Sub WriteNibble(address As Integer, value As Byte)
        If address < 0 OrElse address >= 1024 Then Throw New ArgumentOutOfRangeException(NameOf(address))
        _data(address) = CByte(value And &HF)
    End Sub

    Public Function Cycle(address As Integer, csN As Boolean, weN As Boolean, Optional dataIn As Byte = 0) As Nullable(Of Byte)
        If csN Then Return Nothing
        If Not weN Then
            WriteNibble(address, dataIn)
            Return Nothing
        End If
        Return ReadNibble(address)
    End Function
End Class
