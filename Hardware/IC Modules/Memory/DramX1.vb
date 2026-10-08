Imports System
Imports System.Collections
Imports System.Collections.Generic

' CP64 manual-backed x1 asynchronous DRAM storage/cycle core.
' This models multiplexed row/column addressing and refresh obligations without inventing
' a deterministic post-retention capacitor-decay pattern.
Public Class VmbDramX1
    Private ReadOnly _rowBits As Integer, _colBits As Integer, _refreshBits As Integer
    Private ReadOnly _refreshWindowNs As Long
    Private ReadOnly _cells As BitArray
    Private ReadOnly _lastRefresh() As Long
    Public Sub New(rowBits As Integer, colBits As Integer, refreshBits As Integer, refreshWindowNs As Long)
        _rowBits=rowBits:_colBits=colBits:_refreshBits=refreshBits:_refreshWindowNs=refreshWindowNs
        _cells=New BitArray(1 << (rowBits+colBits))
        ReDim _lastRefresh((1 << refreshBits)-1)
    End Sub
    Private Function Address(row As Integer, col As Integer) As Integer
        If row<0 OrElse row>=(1 << _rowBits) OrElse col<0 OrElse col>=(1 << _colBits) Then Throw New ArgumentOutOfRangeException()
        Return (row << _colBits) Or col
    End Function
    Public Function ReadBit(row As Integer,col As Integer) As Boolean
        Return _cells(Address(row,col))
    End Function
    Public Sub WriteBit(row As Integer,col As Integer,value As Boolean)
        _cells(Address(row,col))=value
    End Sub
    Public Sub Refresh(refreshAddress As Integer, nowNs As Long)
        If refreshAddress<0 OrElse refreshAddress>=_lastRefresh.Length Then Throw New ArgumentOutOfRangeException(NameOf(refreshAddress))
        _lastRefresh(refreshAddress)=nowNs
    End Sub
    Public Function OverdueRefreshRows(nowNs As Long) As IReadOnlyList(Of Integer)
        Dim r As New List(Of Integer)()
        For i=0 To _lastRefresh.Length-1
            If nowNs-_lastRefresh(i)>_refreshWindowNs Then r.Add(i)
        Next
        Return r
    End Function
End Class

Public NotInheritable Class VmbMcm6665A
    Inherits VmbDramX1
    Public Const ModuleId As String="VMB.MEM.DRAM_MCM6665A"
    Public Sub New():MyBase.New(8,8,7,2000000):End Sub
End Class

Public NotInheritable Class VmbTms4256
    Inherits VmbDramX1
    Public Const ModuleId As String="VMB.MEM.DRAM_TMS4256"
    Public Sub New():MyBase.New(9,9,8,4000000):End Sub
End Class
