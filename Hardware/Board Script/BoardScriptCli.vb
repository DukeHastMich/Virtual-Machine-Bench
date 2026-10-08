Imports System
Imports System.IO
Imports System.Text

Public Module BoardScriptCli
    Public Function TryRunFromCommandLine(argsInBed As String()) As Boolean
        If argsInBed Is Nothing OrElse argsInBed.Length < 2 Then Return False
        If Not String.Equals(argsInBed(0), "--board-script-check", StringComparison.OrdinalIgnoreCase) Then Return False

        Try
            Dim scriptPathInBed As String = Path.GetFullPath(argsInBed(1))
            Dim sourceRootInBed As String = AppContext.BaseDirectory
            Dim reportPathInBed As String = Path.ChangeExtension(scriptPathInBed, ".check.txt")

            Dim iInBed As Integer = 2
            While iInBed < argsInBed.Length
                Select Case argsInBed(iInBed).ToLowerInvariant()
                    Case "--source-root"
                        If iInBed + 1 >= argsInBed.Length Then Throw New ArgumentException("--source-root requires a path.")
                        sourceRootInBed = Path.GetFullPath(argsInBed(iInBed + 1))
                        iInBed += 2
                    Case "--report"
                        If iInBed + 1 >= argsInBed.Length Then Throw New ArgumentException("--report requires a path.")
                        reportPathInBed = Path.GetFullPath(argsInBed(iInBed + 1))
                        iInBed += 2
                    Case Else
                        Throw New ArgumentException("Unknown Board Script option '" & argsInBed(iInBed) & "'.")
                End Select
            End While

            Dim definitionInBed As BoardScriptBoardDefinition = New BoardScriptParser(File.ReadAllText(scriptPathInBed)).ParseBoard()
            Dim catalogInBed As BoardScriptPartCatalog = BoardScriptPartCatalog.LoadTree(sourceRootInBed)
            Dim compiledInBed As BoardScriptCompiledBoard = New BoardScriptCompiler(catalogInBed, New BoardScriptWorkerRegistry()).Compile(definitionInBed)
            Dim runtimeGraphInBed As BoardScriptMotherboardRuntimeGraph = Nothing
            If Not compiledInBed.HasErrors AndAlso
               String.Equals(definitionInBed.BoardId, FoxII286Rev32Board.BoardId, StringComparison.OrdinalIgnoreCase) Then
                ' This is an actual construction check, not a second Board Script
                ' motherboard model: the adapter reuses the compiled HT12 worker
                ' inside FoxII286Rev32Board/FoxII286Rev32FixedFabric.
                runtimeGraphInBed = BoardScriptRuntimeAdapter.BuildMotherboardGraph(compiledInBed)
            End If

            Dim outputInBed As New StringBuilder()
            outputInBed.AppendLine("Board Script: " & definitionInBed.Title)
            outputInBed.AppendLine("  id                    : " & definitionInBed.BoardId)
            outputInBed.AppendLine("  outline               : " & definitionInBed.Width.ToString("0.###") & " x " & definitionInBed.Height.ToString("0.###") & " " & definitionInBed.Units)
            outputInBed.AppendLine("  components            : " & compiledInBed.Components.Count)
            outputInBed.AppendLine("  nets                  : " & definitionInBed.Nets.Count)
            outputInBed.AppendLine("  manufacturing ready   : " & compiledInBed.ManufacturingReady.ToString())
            For Each componentInBed As BoardScriptCompiledComponent In compiledInBed.Components
                Dim workerTextInBed As String = If(componentInBed.Worker Is Nothing, "<physical-only>", componentInBed.Worker.WorkerId)
                outputInBed.AppendLine("  " & componentInBed.Definition.RefDes & " = " & componentInBed.Definition.PartId & " -> " & workerTextInBed)
            Next
            If runtimeGraphInBed IsNot Nothing Then
                outputInBed.AppendLine("  runtime graph          : " & FoxII286Rev32Board.RuntimeImplementationId)
                outputInBed.AppendLine("  runtime HT12 reuse     : yes")
                outputInBed.AppendLine("  runtime memory         : " & runtimeGraphInBed.FoxBoard.InstalledMemoryMegabytes.ToString() & " MiB")
            End If
            For Each diagnosticInBed As BoardScriptDiagnostic In compiledInBed.Diagnostics
                outputInBed.AppendLine(diagnosticInBed.ToString())
            Next

            Dim reportDirectoryInBed As String = Path.GetDirectoryName(reportPathInBed)
            If Not String.IsNullOrWhiteSpace(reportDirectoryInBed) Then Directory.CreateDirectory(reportDirectoryInBed)
            File.WriteAllText(reportPathInBed, outputInBed.ToString())
            Console.Write(outputInBed.ToString())
            Console.WriteLine("Report: " & reportPathInBed)
            Environment.ExitCode = If(compiledInBed.HasErrors, 2, 0)
        Catch ex As Exception
            Console.Error.WriteLine(ex.Message)
            Environment.ExitCode = 2
        End Try
        Return True
    End Function
End Module
