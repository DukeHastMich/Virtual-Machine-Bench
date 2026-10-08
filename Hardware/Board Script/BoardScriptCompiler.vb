Imports System
Imports System.Collections.Generic

Public NotInheritable Class BoardScriptCompiler
    Private ReadOnly _catalog As BoardScriptPartCatalog
    Private ReadOnly _workers As BoardScriptWorkerRegistry

    Public Sub New(catalogInBed As BoardScriptPartCatalog, workersInBed As BoardScriptWorkerRegistry)
        If catalogInBed Is Nothing Then Throw New ArgumentNullException(NameOf(catalogInBed))
        If workersInBed Is Nothing Then Throw New ArgumentNullException(NameOf(workersInBed))
        _catalog = catalogInBed
        _workers = workersInBed
    End Sub

    Public Function Compile(definitionInBed As BoardScriptBoardDefinition) As BoardScriptCompiledBoard
        If definitionInBed Is Nothing Then Throw New ArgumentNullException(NameOf(definitionInBed))
        Dim resultInBed As New BoardScriptCompiledBoard With {.Definition = definitionInBed}

        If Not String.Equals(definitionInBed.Units, "mm", StringComparison.OrdinalIgnoreCase) Then
            AddError(resultInBed, "Prototype accepts only millimetres; got '" & definitionInBed.Units & "'.")
        End If
        If definitionInBed.Width <= 0 OrElse definitionInBed.Height <= 0 Then
            AddError(resultInBed, "Board outline must have positive width and height.")
        End If

        Dim componentsByRefDesInBed As New Dictionary(Of String, BoardScriptCompiledComponent)(StringComparer.OrdinalIgnoreCase)
        For Each componentInBed As BoardScriptComponentDefinition In definitionInBed.Components
            If String.IsNullOrWhiteSpace(componentInBed.RefDes) Then
                AddError(resultInBed, "Component has an empty reference designator.")
                Continue For
            End If
            If componentsByRefDesInBed.ContainsKey(componentInBed.RefDes) Then
                AddError(resultInBed, "Duplicate reference designator '" & componentInBed.RefDes & "'.")
                Continue For
            End If

            Dim metadataInBed As BoardScriptPartMetadata = Nothing
            If Not _catalog.TryGetPart(componentInBed.PartId, metadataInBed) Then
                AddError(resultInBed, "Unknown part '" & componentInBed.PartId & "' at " & componentInBed.RefDes & ". Board Script will not invent a component.")
                Continue For
            End If

            If componentInBed.Placement Is Nothing Then
                AddError(resultInBed, componentInBed.RefDes & " has no placement.")
            Else
                ValidatePlacement(resultInBed, componentInBed)
            End If

            Dim compiledComponentInBed As New BoardScriptCompiledComponent With {
                .Definition = componentInBed,
                .Metadata = metadataInBed
            }

            If componentInBed.Straps.Count > 0 AndAlso String.IsNullOrWhiteSpace(metadataInBed.WorkerId) Then
                AddError(resultInBed, componentInBed.RefDes & " declares straps, but part '" & componentInBed.PartId & "' has no behavioral worker binding to receive them.")
            End If

            If Not String.IsNullOrWhiteSpace(metadataInBed.WorkerId) Then
                If Not _workers.CanCreate(metadataInBed.WorkerId) Then
                    AddError(resultInBed, componentInBed.RefDes & " requests worker '" & metadataInBed.WorkerId & "', but no worker factory exists.")
                Else
                    Try
                        compiledComponentInBed.Worker = _workers.Create(metadataInBed.WorkerId, componentInBed)
                    Catch ex As Exception
                        AddError(resultInBed, componentInBed.RefDes & " worker construction failed: " & ex.Message)
                    End Try
                End If
            End If

            resultInBed.Components.Add(compiledComponentInBed)
            componentsByRefDesInBed.Add(componentInBed.RefDes, compiledComponentInBed)
        Next

        Dim netNamesInBed As New HashSet(Of String)(StringComparer.OrdinalIgnoreCase)
        For Each netInBed As BoardScriptNetDefinition In definitionInBed.Nets
            If Not netNamesInBed.Add(netInBed.Name) Then AddError(resultInBed, "Duplicate net name '" & netInBed.Name & "'.")
            If netInBed.Endpoints.Count < 2 Then AddError(resultInBed, "Net '" & netInBed.Name & "' must have at least two endpoints.")
            For Each endpointInBed As BoardScriptEndpoint In netInBed.Endpoints
                Dim componentInBed As BoardScriptCompiledComponent = Nothing
                If Not componentsByRefDesInBed.TryGetValue(endpointInBed.RefDes, componentInBed) Then
                    AddError(resultInBed, "Net '" & netInBed.Name & "' references unknown component '" & endpointInBed.RefDes & "'.")
                    Continue For
                End If
                If componentInBed.Metadata.Pins.Count = 0 Then
                    AddError(resultInBed, "Net '" & netInBed.Name & "' references " & endpointInBed.RefDes & "." & endpointInBed.PinName & ", but that part has no sourced logical-pin declaration yet.")
                    Continue For
                End If
                If Not componentInBed.Metadata.Pins.ContainsKey(endpointInBed.PinName) Then
                    AddError(resultInBed, "Net '" & netInBed.Name & "' references unknown pin '" & endpointInBed.PinName & "' on " & endpointInBed.RefDes & ".")
                End If
            Next
        Next

        resultInBed.ManufacturingReady = Not resultInBed.HasErrors
        If resultInBed.ManufacturingReady Then
            For Each componentInBed As BoardScriptCompiledComponent In resultInBed.Components
                If Not componentInBed.Metadata.FootprintKnown OrElse
                   (componentInBed.Metadata.Pins.Count > 0 AndAlso Not componentInBed.Metadata.PhysicalPinMapComplete) Then
                    resultInBed.ManufacturingReady = False
                    AddInfo(resultInBed, componentInBed.Definition.RefDes & " is logic-valid but not manufacturing-complete; footprint/pin evidence is incomplete.")
                End If
            Next
        End If

        Return resultInBed
    End Function

    Private Sub ValidatePlacement(resultInBed As BoardScriptCompiledBoard, componentInBed As BoardScriptComponentDefinition)
        Dim placementInBed As BoardScriptPlacement = componentInBed.Placement
        If placementInBed.X < 0 OrElse placementInBed.Y < 0 OrElse
           placementInBed.X > resultInBed.Definition.Width OrElse placementInBed.Y > resultInBed.Definition.Height Then
            AddError(resultInBed, componentInBed.RefDes & " placement lies outside the board outline.")
        End If
        If placementInBed.RotationDegrees Mod 90 <> 0 Then
            AddError(resultInBed, componentInBed.RefDes & " prototype rotation must be a multiple of 90 degrees.")
        End If
        If Not String.Equals(placementInBed.Side, "top", StringComparison.OrdinalIgnoreCase) AndAlso
           Not String.Equals(placementInBed.Side, "bottom", StringComparison.OrdinalIgnoreCase) Then
            AddError(resultInBed, componentInBed.RefDes & " side must be top or bottom.")
        End If
    End Sub

    Private Shared Sub AddError(resultInBed As BoardScriptCompiledBoard, messageInBed As String)
        resultInBed.Diagnostics.Add(New BoardScriptDiagnostic With {.IsError = True, .Message = messageInBed})
    End Sub

    Private Shared Sub AddInfo(resultInBed As BoardScriptCompiledBoard, messageInBed As String)
        resultInBed.Diagnostics.Add(New BoardScriptDiagnostic With {.IsError = False, .Message = messageInBed})
    End Sub
End Class
