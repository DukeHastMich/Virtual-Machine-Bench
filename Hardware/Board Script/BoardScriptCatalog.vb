Imports System
Imports System.Collections.Generic
Imports System.IO
Imports System.Text.Json

Public NotInheritable Class BoardScriptPartCatalog
    Private ReadOnly _parts As New Dictionary(Of String, BoardScriptPartMetadata)(StringComparer.OrdinalIgnoreCase)

    Public ReadOnly Property Parts As IReadOnlyDictionary(Of String, BoardScriptPartMetadata)
        Get
            Return _parts
        End Get
    End Property

    Public Shared Function LoadTree(rootPathInBed As String) As BoardScriptPartCatalog
        If String.IsNullOrWhiteSpace(rootPathInBed) Then Throw New ArgumentNullException(NameOf(rootPathInBed))
        If Not Directory.Exists(rootPathInBed) Then Throw New DirectoryNotFoundException(rootPathInBed)

        Dim catalogInBed As New BoardScriptPartCatalog()
        For Each pathInBed As String In Directory.EnumerateFiles(rootPathInBed, "PARTS-METADATA.json", SearchOption.AllDirectories)
            catalogInBed.LoadPartsMetadata(pathInBed)
        Next
        For Each pathInBed As String In Directory.EnumerateFiles(rootPathInBed, "WORKER-METADATA.json", SearchOption.AllDirectories)
            catalogInBed.LoadWorkerMetadata(pathInBed)
        Next
        Return catalogInBed
    End Function

    Public Function TryGetPart(partIdInBed As String, ByRef metadataInBed As BoardScriptPartMetadata) As Boolean
        Return _parts.TryGetValue(partIdInBed, metadataInBed)
    End Function

    Private Sub LoadPartsMetadata(pathInBed As String)
        Using documentInBed As JsonDocument = JsonDocument.Parse(File.ReadAllText(pathInBed))
            Dim partsElementInBed As JsonElement
            If Not documentInBed.RootElement.TryGetProperty("parts", partsElementInBed) OrElse partsElementInBed.ValueKind <> JsonValueKind.Array Then Return

            For Each partElementInBed As JsonElement In partsElementInBed.EnumerateArray()
                Dim idInBed As String = ReadString(partElementInBed, "id")
                If String.IsNullOrWhiteSpace(idInBed) Then Continue For
                Dim metadataInBed As BoardScriptPartMetadata = Nothing
                If Not _parts.TryGetValue(idInBed, metadataInBed) Then
                    metadataInBed = New BoardScriptPartMetadata With {.PartId = idInBed}
                    _parts.Add(idInBed, metadataInBed)
                ElseIf Not String.IsNullOrWhiteSpace(metadataInBed.PartsMetadataSource) AndAlso
                       Not String.Equals(metadataInBed.PartsMetadataSource, pathInBed, StringComparison.OrdinalIgnoreCase) Then
                    Throw New InvalidDataException("Duplicate physical part id '" & idInBed & "' appears in both '" & metadataInBed.PartsMetadataSource & "' and '" & pathInBed & "'.")
                End If
                metadataInBed.Manufacturer = ReadString(partElementInBed, "manufacturer")
                metadataInBed.PartNumber = ReadString(partElementInBed, "part_number")
                metadataInBed.Package = ReadString(partElementInBed, "package")
                metadataInBed.Role = ReadString(partElementInBed, "role")
                metadataInBed.PartsMetadataSource = pathInBed
            Next
        End Using
    End Sub

    Private Sub LoadWorkerMetadata(pathInBed As String)
        Using documentInBed As JsonDocument = JsonDocument.Parse(File.ReadAllText(pathInBed))
            Dim bindingsInBed As JsonElement
            If Not documentInBed.RootElement.TryGetProperty("bindings", bindingsInBed) OrElse bindingsInBed.ValueKind <> JsonValueKind.Array Then Return

            For Each bindingInBed As JsonElement In bindingsInBed.EnumerateArray()
                Dim partIdInBed As String = ReadString(bindingInBed, "part_id")
                If String.IsNullOrWhiteSpace(partIdInBed) Then Continue For
                Dim metadataInBed As BoardScriptPartMetadata = Nothing
                If Not _parts.TryGetValue(partIdInBed, metadataInBed) Then
                    Throw New InvalidDataException("Worker metadata '" & pathInBed & "' references unknown part '" & partIdInBed & "'. A behavior binding cannot create a physical part identity.")
                End If
                If Not String.IsNullOrWhiteSpace(metadataInBed.WorkerMetadataSource) AndAlso
                   Not String.Equals(metadataInBed.WorkerMetadataSource, pathInBed, StringComparison.OrdinalIgnoreCase) Then
                    Throw New InvalidDataException("Duplicate worker binding for part '" & partIdInBed & "' appears in both '" & metadataInBed.WorkerMetadataSource & "' and '" & pathInBed & "'.")
                End If
                metadataInBed.WorkerId = ReadString(bindingInBed, "worker_id")
                metadataInBed.PhysicalPinMapComplete = ReadBoolean(bindingInBed, "physical_pin_map_complete")
                metadataInBed.FootprintKnown = ReadBoolean(bindingInBed, "footprint_known")
                metadataInBed.WorkerMetadataSource = pathInBed

                Dim pinsInBed As JsonElement
                If bindingInBed.TryGetProperty("pins", pinsInBed) AndAlso pinsInBed.ValueKind = JsonValueKind.Array Then
                    For Each pinInBed As JsonElement In pinsInBed.EnumerateArray()
                        Dim pinNameInBed As String = ReadString(pinInBed, "name")
                        If String.IsNullOrWhiteSpace(pinNameInBed) Then Continue For
                        Dim pinMetadataInBed As New BoardScriptPinMetadata With {
                            .Name = pinNameInBed,
                            .Direction = ReadString(pinInBed, "direction")
                        }
                        Dim numberInBed As JsonElement
                        If pinInBed.TryGetProperty("number", numberInBed) AndAlso numberInBed.ValueKind = JsonValueKind.Number Then
                            Dim parsedInBed As Integer
                            If numberInBed.TryGetInt32(parsedInBed) Then pinMetadataInBed.Number = parsedInBed
                        End If
                        metadataInBed.Pins(pinNameInBed) = pinMetadataInBed
                    Next
                End If
            Next
        End Using
    End Sub

    Private Shared Function ReadString(elementInBed As JsonElement, propertyNameInBed As String) As String
        Dim valueInBed As JsonElement
        If elementInBed.TryGetProperty(propertyNameInBed, valueInBed) AndAlso valueInBed.ValueKind = JsonValueKind.String Then
            Return If(valueInBed.GetString(), String.Empty)
        End If
        Return String.Empty
    End Function

    Private Shared Function ReadBoolean(elementInBed As JsonElement, propertyNameInBed As String) As Boolean
        Dim valueInBed As JsonElement
        If elementInBed.TryGetProperty(propertyNameInBed, valueInBed) Then
            If valueInBed.ValueKind = JsonValueKind.True Then Return True
            If valueInBed.ValueKind = JsonValueKind.False Then Return False
        End If
        Return False
    End Function
End Class
