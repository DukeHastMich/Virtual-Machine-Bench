Imports System
Imports System.Collections.Generic

' Microchip/Atmel AT28C64B 8K x 8 parallel EEPROM.
' Models board-visible read/write storage and write-cycle polling. High-voltage chip erase and
' analog power/noise thresholds remain physical/electrical truth, not fabricated digital behavior.
Public NotInheritable Class VmbAt28c64b
    Private ReadOnly _data(8191) As Byte
    Private _busyUntilNs As Long
    Private _lastWritten As Byte
    Private _toggle As Boolean
    Public Sub New(Optional contents As Byte() = Nothing)
        If contents Is Nothing Then
            For i=0 To _data.Length-1 : _data(i)=&HFF : Next
        Else
            If contents.Length<>8192 Then Throw New ArgumentException("AT28C64B image must be exactly 8192 bytes.", NameOf(contents))
            Array.Copy(contents,_data,8192)
        End If
    End Sub
    Public Function IsBusy(nowNs As Long) As Boolean : Return nowNs < _busyUntilNs : End Function
    Public Function ReadByte(address As Integer, Optional nowNs As Long=0) As Byte
        If address<0 OrElse address>=8192 Then Throw New ArgumentOutOfRangeException(NameOf(address))
        Dim v=_data(address)
        If IsBusy(nowNs) Then
            _toggle=Not _toggle
            v=CByte((v And &H3F) Or ((Not _lastWritten) And &H80) Or If(_toggle,&H40,0))
        End If
        Return v
    End Function
    Public Function EvaluateRead(address As Integer, ceN As Boolean, oeN As Boolean, weN As Boolean, Optional nowNs As Long=0) As Nullable(Of Byte)
        If ceN OrElse oeN OrElse Not weN Then Return Nothing
        Return ReadByte(address,nowNs)
    End Function
    Public Sub WriteByte(address As Integer, value As Byte, Optional nowNs As Long=0, Optional writeCycleNs As Long=10000000)
        If address<0 OrElse address>=8192 Then Throw New ArgumentOutOfRangeException(NameOf(address))
        If IsBusy(nowNs) Then Return
        _data(address)=value : _lastWritten=value : _busyUntilNs=nowNs+writeCycleNs
    End Sub
End Class
