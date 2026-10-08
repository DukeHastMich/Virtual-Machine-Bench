Imports System

' SST39SF040-compatible 512 KiB flash storage primitive. Board decode/windowing
' remains outside the chip so this module is reusable across physical boards.
Public NotInheritable Class Sst39Sf040
    Private Const CapacityInBed As Integer = 512 * 1024
    Private ReadOnly _dataInBed(CapacityInBed - 1) As Byte
    Private _unlockStageInBed As Integer
    Private _programArmedInBed As Boolean

    Public Sub New(Optional imageInBed As Byte() = Nothing)
        Array.Fill(_dataInBed, CByte(&HFF))
        If imageInBed IsNot Nothing Then
            If imageInBed.Length > CapacityInBed Then Throw New ArgumentException("SST39SF040 image exceeds 512 KiB.", NameOf(imageInBed))
            Array.Copy(imageInBed, _dataInBed, imageInBed.Length)
        End If
    End Sub

    Public ReadOnly Property Capacity As Integer
        Get
            Return CapacityInBed
        End Get
    End Property

    Public Function ReadByte(addressInBed As Integer) As Byte
        If addressInBed < 0 OrElse addressInBed >= CapacityInBed Then Throw New ArgumentOutOfRangeException(NameOf(addressInBed))
        Return _dataInBed(addressInBed)
    End Function

    Public Sub WriteByte(addressInBed As Integer, valueInBed As Byte)
        If addressInBed < 0 OrElse addressInBed >= CapacityInBed Then Throw New ArgumentOutOfRangeException(NameOf(addressInBed))
        If _programArmedInBed Then
            _dataInBed(addressInBed) = CByte(_dataInBed(addressInBed) And valueInBed)
            _programArmedInBed = False : _unlockStageInBed = 0 : Return
        End If
        Select Case _unlockStageInBed
            Case 0
                If addressInBed = &H5555 AndAlso valueInBed = &HAA Then _unlockStageInBed = 1
            Case 1
                If addressInBed = &H2AAA AndAlso valueInBed = &H55 Then _unlockStageInBed = 2 Else _unlockStageInBed = 0
            Case 2
                If addressInBed = &H5555 AndAlso valueInBed = &HA0 Then _programArmedInBed = True
                _unlockStageInBed = 0
        End Select
    End Sub

    Public Function Snapshot() As Byte()
        Return DirectCast(_dataInBed.Clone(), Byte())
    End Function
End Class
