Imports System
Imports System.Collections.Generic

' Bridge from a validated physical Board Script into the same executable
' motherboard graph used by the ordinary runtime.  This is intentionally a
' board-family adapter rather than a second hardware model: the HT12 silicon
' object created by the Board Script worker is reused by FoxII286Rev32Board.
Public NotInheritable Class BoardScriptMotherboardRuntimeGraph
    Public Sub New(compiledBoardInBed As BoardScriptCompiledBoard,
                   foxBoardInBed As FoxII286Rev32Board,
                   fixedFabricInBed As FoxII286Rev32FixedFabric)
        If compiledBoardInBed Is Nothing Then Throw New ArgumentNullException(NameOf(compiledBoardInBed))
        If foxBoardInBed Is Nothing Then Throw New ArgumentNullException(NameOf(foxBoardInBed))
        If fixedFabricInBed Is Nothing Then Throw New ArgumentNullException(NameOf(fixedFabricInBed))
        CompiledBoard = compiledBoardInBed
        FoxBoard = foxBoardInBed
        FixedFabric = fixedFabricInBed
    End Sub

    Public ReadOnly Property CompiledBoard As BoardScriptCompiledBoard
    Public ReadOnly Property FoxBoard As FoxII286Rev32Board
    Public ReadOnly Property FixedFabric As FoxII286Rev32FixedFabric

    Public Function DiagnosticText() As String
        Return "Board Script runtime graph" & Environment.NewLine &
               "  board id                 : " & CompiledBoard.Definition.BoardId & Environment.NewLine &
               "  physical components      : " & CompiledBoard.Components.Count.ToString() & Environment.NewLine &
               "  physical nets            : " & CompiledBoard.Definition.Nets.Count.ToString() & Environment.NewLine &
               "  HT12 worker reused       : yes" & Environment.NewLine &
               "  FOX installed memory     : " & FoxBoard.InstalledMemoryMegabytes.ToString() & " MiB" & Environment.NewLine &
               "  runtime implementation   : " & FoxII286Rev32Board.RuntimeImplementationId
    End Function
End Class

Public NotInheritable Class BoardScriptRuntimeAdapter
    Private Sub New()
    End Sub

    Public Shared Function BuildMotherboardGraph(compiledBoardInBed As BoardScriptCompiledBoard,
                                                 Optional cmosPersistencePathInBed As String = Nothing) As BoardScriptMotherboardRuntimeGraph
        If compiledBoardInBed Is Nothing Then Throw New ArgumentNullException(NameOf(compiledBoardInBed))
        If compiledBoardInBed.Definition Is Nothing Then Throw New InvalidOperationException("Compiled Board Script has no board definition.")
        If compiledBoardInBed.HasErrors Then Throw New InvalidOperationException("Board Script contains compile errors and cannot construct runtime hardware.")

        Select Case compiledBoardInBed.Definition.BoardId.ToLowerInvariant()
            Case FoxII286Rev32Board.BoardId
                Return BuildFoxII286Rev32GraphInBed(compiledBoardInBed, cmosPersistencePathInBed)
            Case Else
                Throw New NotSupportedException("No runtime-graph adapter is registered for Board Script board '" &
                                                compiledBoardInBed.Definition.BoardId & "'.")
        End Select
    End Function

    Private Shared Function BuildFoxII286Rev32GraphInBed(compiledBoardInBed As BoardScriptCompiledBoard,
                                                          cmosPersistencePathInBed As String) As BoardScriptMotherboardRuntimeGraph
        Dim ht12ComponentInBed As BoardScriptCompiledComponent = RequireSinglePartInBed(compiledBoardInBed, "headland-ht12-a")
        RequireSinglePartInBed(compiledBoardInBed, "amd-n80l286-16-s")
        RequireSinglePartInBed(compiledBoardInBed, "hualon-hm6818p")
        RequireSinglePartInBed(compiledBoardInBed, "eprom-27256-even")
        RequireSinglePartInBed(compiledBoardInBed, "eprom-27256-odd")

        Dim ht12WorkerInBed As HeadlandHt12BoardScriptWorker = TryCast(ht12ComponentInBed.Worker, HeadlandHt12BoardScriptWorker)
        If ht12WorkerInBed Is Nothing Then
            Throw New InvalidOperationException("FOX Board Script HT12 component is not bound to the Headland HT12 worker.")
        End If

        ' Reuse the exact silicon object created from the script's strap values.
        ' FoxII286Rev32Board snapshots the straps for board metadata and checks
        ' that REG10 reset state still matches before accepting the worker.
        Dim boardInBed As New FoxII286Rev32Board(ht12WorkerInBed.PowerOnStraps, ht12WorkerInBed.Silicon)
        Dim fixedFabricInBed As New FoxII286Rev32FixedFabric(boardInBed, cmosPersistencePathInBed)

        If Not Object.ReferenceEquals(boardInBed.Chipset, ht12WorkerInBed.Silicon) Then
            Throw New InvalidOperationException("Board Script/runtime convergence failure: FOX board did not retain the compiled HT12 silicon instance.")
        End If

        Return New BoardScriptMotherboardRuntimeGraph(compiledBoardInBed, boardInBed, fixedFabricInBed)
    End Function

    Private Shared Function RequireSinglePartInBed(compiledBoardInBed As BoardScriptCompiledBoard,
                                                    partIdInBed As String) As BoardScriptCompiledComponent
        Dim matchInBed As BoardScriptCompiledComponent = Nothing
        Dim countInBed As Integer = 0
        For Each componentInBed As BoardScriptCompiledComponent In compiledBoardInBed.Components
            If componentInBed Is Nothing OrElse componentInBed.Definition Is Nothing Then Continue For
            If String.Equals(componentInBed.Definition.PartId, partIdInBed, StringComparison.OrdinalIgnoreCase) Then
                countInBed += 1
                matchInBed = componentInBed
            End If
        Next
        If countInBed <> 1 Then
            Throw New InvalidOperationException("FOX Board Script requires exactly one '" & partIdInBed &
                                                "' component; found " & countInBed.ToString() & ".")
        End If
        Return matchInBed
    End Function
End Class
