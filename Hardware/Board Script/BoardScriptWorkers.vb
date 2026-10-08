Imports System
Imports System.Collections.Generic

Public NotInheritable Class BoardScriptWorkerRegistry
    Private ReadOnly _factories As New Dictionary(Of String, Func(Of BoardScriptComponentDefinition, IBoardScriptWorker))(StringComparer.OrdinalIgnoreCase)

    Public Sub New()
        Register("headland.ht12-a", Function(componentInBed) New HeadlandHt12BoardScriptWorker(componentInBed))
        Register("ct.neat.82c211", Function(componentInBed) New Ct82C211BoardScriptWorker(componentInBed))
        Register("ct.neat.82c212", Function(componentInBed) New Ct82C212BoardScriptWorker(componentInBed))
        Register("ct.neat.82c215", Function(componentInBed) New Ct82C215BoardScriptWorker(componentInBed))
        Register("ct.neat.82c206", Function(componentInBed) New Ct82C206BoardScriptWorker(componentInBed))
    End Sub

    Public Sub Register(workerIdInBed As String, factoryInBed As Func(Of BoardScriptComponentDefinition, IBoardScriptWorker))
        If String.IsNullOrWhiteSpace(workerIdInBed) Then Throw New ArgumentNullException(NameOf(workerIdInBed))
        If factoryInBed Is Nothing Then Throw New ArgumentNullException(NameOf(factoryInBed))
        _factories(workerIdInBed) = factoryInBed
    End Sub

    Public Function CanCreate(workerIdInBed As String) As Boolean
        Return Not String.IsNullOrWhiteSpace(workerIdInBed) AndAlso _factories.ContainsKey(workerIdInBed)
    End Function

    Public Function Create(workerIdInBed As String, componentInBed As BoardScriptComponentDefinition) As IBoardScriptWorker
        Dim factoryInBed As Func(Of BoardScriptComponentDefinition, IBoardScriptWorker) = Nothing
        If Not _factories.TryGetValue(workerIdInBed, factoryInBed) Then
            Throw New InvalidOperationException("No Board Script worker factory is registered for '" & workerIdInBed & "'.")
        End If
        Return factoryInBed(componentInBed)
    End Function
End Class

Public NotInheritable Class HeadlandHt12BoardScriptWorker
    Implements IBoardScriptWorker

    Private ReadOnly _siliconInBed As HeadlandHt12
    Private ReadOnly _strapsInBed As Ht12PowerOnStraps

    Public Sub New(componentInBed As BoardScriptComponentDefinition)
        If componentInBed Is Nothing Then Throw New ArgumentNullException(NameOf(componentInBed))
        Dim strapsInBed As New Ht12PowerOnStraps With {
            .RamConfigurationCode = 0,
            .AtBusLowSpeed = False,
            .ZeroWaitMainMemory = False,
            .ExtraWaitStateMode = 0,
            .TcStrapHigh = False
        }

        For Each entryInBed As KeyValuePair(Of String, BoardScriptValue) In componentInBed.Straps
            Select Case entryInBed.Key.ToLowerInvariant()
                Case "ramconfigurationcode"
                    strapsInBed.RamConfigurationCode = CByte(entryInBed.Value.AsInteger(entryInBed.Key) And &H7)
                Case "atbuslowspeed"
                    strapsInBed.AtBusLowSpeed = entryInBed.Value.AsBoolean(entryInBed.Key)
                Case "zerowaitmainmemory"
                    strapsInBed.ZeroWaitMainMemory = entryInBed.Value.AsBoolean(entryInBed.Key)
                Case "extrawaitstatemode"
                    strapsInBed.ExtraWaitStateMode = CByte(entryInBed.Value.AsInteger(entryInBed.Key) And &H3)
                Case "tcstraphigh"
                    strapsInBed.TcStrapHigh = entryInBed.Value.AsBoolean(entryInBed.Key)
                Case Else
                    Throw New InvalidOperationException("HT12/A has no sourced Board Script strap named '" & entryInBed.Key & "'.")
            End Select
        Next
        _strapsInBed = strapsInBed
        _siliconInBed = New HeadlandHt12(_strapsInBed)
    End Sub

    Public ReadOnly Property PowerOnStraps As Ht12PowerOnStraps
        Get
            Return New Ht12PowerOnStraps With {
                .RamConfigurationCode = _strapsInBed.RamConfigurationCode,
                .AtBusLowSpeed = _strapsInBed.AtBusLowSpeed,
                .ZeroWaitMainMemory = _strapsInBed.ZeroWaitMainMemory,
                .ExtraWaitStateMode = _strapsInBed.ExtraWaitStateMode,
                .TcStrapHigh = _strapsInBed.TcStrapHigh
            }
        End Get
    End Property

    Public ReadOnly Property Silicon As HeadlandHt12
        Get
            Return _siliconInBed
        End Get
    End Property

    Public ReadOnly Property WorkerId As String Implements IBoardScriptWorker.WorkerId
        Get
            Return "headland.ht12-a"
        End Get
    End Property

    Public ReadOnly Property SiliconIdentity As String Implements IBoardScriptWorker.SiliconIdentity
        Get
            Return _siliconInBed.Identity
        End Get
    End Property

    Public Function DiagnosticText() As String Implements IBoardScriptWorker.DiagnosticText
        Return _siliconInBed.DiagnosticText()
    End Function
End Class
