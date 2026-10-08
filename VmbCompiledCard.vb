Imports System
Imports System.Collections.Generic
Imports System.IO
Imports System.Text.Json

Public NotInheritable Class VmbCompiledCardControl
    Public Property Id As String
    Public Property Kind As String
    Public Property Elements As Integer
    Public Property ChangeWhilePowered As Boolean
End Class

Public NotInheritable Class VmbCompiledCardProperty
    Public Property Name As String
    Public Property AllowedValues As New List(Of String)()
    Public Property DefaultValue As String
End Class

Public NotInheritable Class VmbCompiledCardHeader
    Public Property CardId As String
    Public Property DisplayName As String
    Public Property Category As String
    Public Property Bus As String
    Public Property ConnectorBits As Integer
    Public Property BoardCodeFormat As String
    Public Property PrimaryModuleId As String
    Public Property Controls As New List(Of VmbCompiledCardControl)()
    Public Property Properties As New List(Of VmbCompiledCardProperty)()
    Public Property RequiredModules As New List(Of String)()
End Class

Public NotInheritable Class VmbCompiledCardPackage
    Public Property FilePath As String
    Public Property Header As VmbCompiledCardHeader
    Public Property BoardCode As JsonElement
End Class

' Read-only catalog for Python-compiled cards. Merely browsing the configurator
' reads INSTALL metadata; no emulated hardware is instantiated until power-off
' materialization asks VmbCompiledCardLoader to install the selected package.
Public NotInheritable Class VmbCompiledCardStore
    Private Shared _cachedCardsInBed As List(Of VmbCompiledCardPackage)
    Private Shared _lastBoardCountInBed As Integer = Integer.MinValue

    Private Sub New()
    End Sub

    Public Shared Function MetaFilePath() As String
        Return Path.Combine(StoreDirectory(), "PCB.meta")
    End Function

    ' This is intentionally the only cheap polling operation.  VMB does not
    ' enumerate the VCB directory looking for changes; the producer owns the
    ' count and changing this integer tells us when a rescan is worthwhile.
    Public Shared Function ReadPublishedBoardCount() As Integer
        Dim rawInBed As String = File.ReadAllText(MetaFilePath()).Trim()
        Dim countInBed As Integer
        If Not Integer.TryParse(rawInBed, countInBed) OrElse countInBed < 0 Then
            Throw New InvalidDataException("PCB.meta must contain one non-negative integer.")
        End If
        Return countInBed
    End Function

    Public Shared Function PollBoardCountChanged() As Boolean
        Return VcbStore.PollBoardCountChanged()
    End Function

    Public Shared Sub ForceCatalogRefresh()
        _cachedCardsInBed = Nothing
        VcbStore.ForceCatalogRefresh()
    End Sub

    Public Shared Function StoreDirectory() As String
        Return VcbStore.StoreDirectory()
    End Function

    Public Shared Function EnumerateCards(Optional categoryInBed As String = Nothing) As List(Of VmbCompiledCardPackage)
        Dim resultInBed As New List(Of VmbCompiledCardPackage)()
        For Each entryInBed As VcbCatalogEntry In VcbStore.EnumerateBoards(categoryInBed)
            Try
                Dim boardInBed As IVirtualCircuitBoard = VcbStore.LoadDefinition(entryInBed.FilePath)
                resultInBed.Add(New VmbCompiledCardPackage With {.FilePath = entryInBed.FilePath, .Header = ParseVcbHeaderInBed(boardInBed.InstallHeaderJson)})
            Catch
            End Try
        Next
        Return resultInBed
    End Function

    Public Shared Function FindById(cardIdInBed As String) As VmbCompiledCardPackage
        If String.IsNullOrWhiteSpace(cardIdInBed) Then Return Nothing
        For Each packageInBed In EnumerateCards()
            If String.Equals(packageInBed.Header.CardId, cardIdInBed, StringComparison.OrdinalIgnoreCase) Then Return packageInBed
        Next
        Return Nothing
    End Function

    Private Shared Function ParseVcbHeaderInBed(jsonInBed As String) As VmbCompiledCardHeader
        Using documentInBed As JsonDocument = JsonDocument.Parse(jsonInBed)
            Dim installInBed As JsonElement = documentInBed.RootElement
            Dim headerInBed As New VmbCompiledCardHeader With {
                .CardId = installInBed.GetProperty("card_id").GetString(), .DisplayName = installInBed.GetProperty("display_name").GetString(),
                .Category = installInBed.GetProperty("category").GetString(), .Bus = installInBed.GetProperty("bus").GetString(),
                .ConnectorBits = installInBed.GetProperty("connector_bits").GetInt32(), .BoardCodeFormat = installInBed.GetProperty("board_code_format").GetString(),
                .PrimaryModuleId = installInBed.GetProperty("primary_module_id").GetString()}
            Dim a As JsonElement
            If installInBed.TryGetProperty("required_modules", a) Then For Each x In a.EnumerateArray() : headerInBed.RequiredModules.Add(x.GetString()) : Next
            If installInBed.TryGetProperty("controls", a) Then
                For Each x In a.EnumerateArray()
                    headerInBed.Controls.Add(New VmbCompiledCardControl With {.Id=x.GetProperty("id").GetString(), .Kind=x.GetProperty("kind").GetString(), .Elements=x.GetProperty("elements").GetInt32(), .ChangeWhilePowered=x.GetProperty("change_while_powered").GetBoolean()})
                Next
            End If
            If installInBed.TryGetProperty("properties", a) Then
                For Each x In a.EnumerateArray()
                    Dim p As New VmbCompiledCardProperty With {.Name=x.GetProperty("name").GetString()}
                    Dim d As JsonElement
                    If x.TryGetProperty("default", d) AndAlso d.ValueKind <> JsonValueKind.Null Then p.DefaultValue=d.ToString()
                    For Each v In x.GetProperty("allowed").EnumerateArray()
                        If v.ValueKind=JsonValueKind.Number OrElse v.ValueKind=JsonValueKind.String Then p.AllowedValues.Add(v.ToString())
                    Next
                    headerInBed.Properties.Add(p)
                Next
            End If
            Return headerInBed
        End Using
    End Function

    Public Shared Function LoadPackage(pathInBed As String, Optional headerOnlyInBed As Boolean = False) As VmbCompiledCardPackage
        Using documentInBed As JsonDocument = JsonDocument.Parse(File.ReadAllText(pathInBed))
            Dim rootInBed As JsonElement = documentInBed.RootElement
            If rootInBed.GetProperty("format").GetString() <> "VMB-CARD-1" Then Throw New InvalidDataException("Unsupported compiled-card format.")
            Dim installInBed As JsonElement = rootInBed.GetProperty("install")
            Dim headerInBed As New VmbCompiledCardHeader With {
                .CardId = installInBed.GetProperty("card_id").GetString(),
                .DisplayName = installInBed.GetProperty("display_name").GetString(),
                .Category = installInBed.GetProperty("category").GetString(),
                .Bus = installInBed.GetProperty("bus").GetString(),
                .ConnectorBits = installInBed.GetProperty("connector_bits").GetInt32(),
                .BoardCodeFormat = installInBed.GetProperty("board_code_format").GetString(),
                .PrimaryModuleId = installInBed.GetProperty("primary_module_id").GetString()
            }
            Dim arrayInBed As JsonElement
            If installInBed.TryGetProperty("required_modules", arrayInBed) Then
                For Each itemInBed As JsonElement In arrayInBed.EnumerateArray() : headerInBed.RequiredModules.Add(itemInBed.GetString()) : Next
            End If
            If installInBed.TryGetProperty("controls", arrayInBed) Then
                For Each itemInBed As JsonElement In arrayInBed.EnumerateArray()
                    headerInBed.Controls.Add(New VmbCompiledCardControl With {
                        .Id = itemInBed.GetProperty("id").GetString(), .Kind = itemInBed.GetProperty("kind").GetString(),
                        .Elements = itemInBed.GetProperty("elements").GetInt32(), .ChangeWhilePowered = itemInBed.GetProperty("change_while_powered").GetBoolean()})
                Next
            End If
            If installInBed.TryGetProperty("properties", arrayInBed) Then
                For Each itemInBed As JsonElement In arrayInBed.EnumerateArray()
                    Dim pInBed As New VmbCompiledCardProperty With {.Name = itemInBed.GetProperty("name").GetString(), .DefaultValue = itemInBed.GetProperty("default").ToString()}
                    For Each vInBed As JsonElement In itemInBed.GetProperty("allowed").EnumerateArray() : pInBed.AllowedValues.Add(vInBed.ToString()) : Next
                    headerInBed.Properties.Add(pInBed)
                Next
            End If
            Dim resultInBed As New VmbCompiledCardPackage With {.FilePath = pathInBed, .Header = headerInBed}
            If Not headerOnlyInBed Then resultInBed.BoardCode = rootInBed.GetProperty("board_code").Clone()
            Return resultInBed
        End Using
    End Function
End Class

' Stable ABI between the Python compiler and VB runtime. Module IDs are the only
' behavioral names a compiled board is allowed to know.
Public NotInheritable Class VmbCompiledCardLoader
    Private Sub New()
    End Sub

    Public Shared Function InstallIsaCard(packageInBed As VmbCompiledCardPackage,
                                          slotIdInBed As String,
                                          basePortOverrideInBed As Nullable(Of UInt16),
                                          irqOverrideInBed As Nullable(Of Integer)) As Object
        If packageInBed Is Nothing OrElse packageInBed.Header Is Nothing Then Throw New ArgumentNullException(NameOf(packageInBed))
        If packageInBed.FilePath.EndsWith(".vcb.dll", StringComparison.OrdinalIgnoreCase) Then
            Dim boardInBed As IVirtualCircuitBoard = VcbStore.LoadDefinition(packageInBed.FilePath)
            Dim runtimeInBed As New VcbIsaMaterializationRuntime(slotIdInBed, boardInBed, basePortOverrideInBed, irqOverrideInBed)
            boardInBed.Build(runtimeInBed)
            Return runtimeInBed.InstalledCard
        End If
        If Not String.Equals(packageInBed.Header.Bus, "ISA", StringComparison.OrdinalIgnoreCase) AndAlso
           Not String.Equals(packageInBed.Header.Bus, "ISA8", StringComparison.OrdinalIgnoreCase) Then Throw New InvalidDataException("Card does not declare ISA compatibility.")
        If packageInBed.Header.BoardCodeFormat <> "VMB-BOARD-CODE-1" Then Throw New InvalidDataException("Unsupported Board Code ABI.")

        Dim ioBaseInBed As Integer = RequireNumericPropertyInBed(packageInBed.Header, "IO_BASE", basePortOverrideInBed.HasValue, If(basePortOverrideInBed.HasValue, CInt(basePortOverrideInBed.Value), 0))
        Dim irqInBed As Integer = RequireNumericPropertyInBed(packageInBed.Header, "IRQ", irqOverrideInBed.HasValue, If(irqOverrideInBed.HasValue, irqOverrideInBed.Value, 0))
        Dim allowedIoInBed As UInt16() = NumericDomainUInt16InBed(packageInBed.Header, "IO_BASE")
        Dim allowedIrqInBed As Integer() = NumericDomainIntInBed(packageInBed.Header, "IRQ")

        For Each moduleIdInBed As String In packageInBed.Header.RequiredModules
            If Not VmbModuleRegistry.Supports(moduleIdInBed) Then Throw New InvalidOperationException("VMB runtime does not provide required module '" & moduleIdInBed & "'.")
        Next

        Dim definitionInBed As New IsaCardDefinition286(packageInBed.Header.CardId,
                                                         packageInBed.Header.DisplayName,
                                                         If(packageInBed.Header.ConnectorBits <= 8, IsaSlotConnectorWidth286.EightBit, IsaSlotConnectorWidth286.SixteenBit),
                                                         Function() VmbModuleRegistry.CreatePrimaryIsaModule(packageInBed.Header.PrimaryModuleId,
                                                                                                             CUShort(ioBaseInBed), irqInBed,
                                                                                                             allowedIoInBed, allowedIrqInBed))
        Return IsaBackplane.InstallCard(slotIdInBed, definitionInBed)
    End Function

    Private Shared Function FindPropertyInBed(headerInBed As VmbCompiledCardHeader, nameInBed As String) As VmbCompiledCardProperty
        For Each pInBed As VmbCompiledCardProperty In headerInBed.Properties
            If String.Equals(pInBed.Name, nameInBed, StringComparison.OrdinalIgnoreCase) Then Return pInBed
        Next
        Return Nothing
    End Function

    Private Shared Function RequireNumericPropertyInBed(headerInBed As VmbCompiledCardHeader, nameInBed As String, hasOverrideInBed As Boolean, overrideInBed As Integer) As Integer
        Dim pInBed As VmbCompiledCardProperty = FindPropertyInBed(headerInBed, nameInBed)
        If pInBed Is Nothing Then Throw New InvalidDataException("Compiled card lacks required property " & nameInBed & ".")
        Dim valueInBed As Integer = If(hasOverrideInBed, overrideInBed, Integer.Parse(pInBed.DefaultValue))
        If Not pInBed.AllowedValues.Contains(valueInBed.ToString()) Then Throw New InvalidDataException(nameInBed & " selection is not physically realizable by this compiled card.")
        Return valueInBed
    End Function

    Private Shared Function NumericDomainUInt16InBed(headerInBed As VmbCompiledCardHeader, nameInBed As String) As UInt16()
        Dim pInBed As VmbCompiledCardProperty = FindPropertyInBed(headerInBed, nameInBed)
        If pInBed Is Nothing Then Return Array.Empty(Of UInt16)()
        Return pInBed.AllowedValues.ConvertAll(Function(v) CUShort(Integer.Parse(v))).ToArray()
    End Function

    Private Shared Function NumericDomainIntInBed(headerInBed As VmbCompiledCardHeader, nameInBed As String) As Integer()
        Dim pInBed As VmbCompiledCardProperty = FindPropertyInBed(headerInBed, nameInBed)
        If pInBed Is Nothing Then Return Array.Empty(Of Integer)()
        Return pInBed.AllowedValues.ConvertAll(Function(v) Integer.Parse(v)).ToArray()
    End Function
End Class

Public NotInheritable Class VmbModuleRegistry
    Private Sub New()
    End Sub

    Public Shared Function Supports(moduleIdInBed As String) As Boolean
        Select Case moduleIdInBed
            Case "VMB.NET.RTL8019AS", "VMB.MEM.SST39SF040", "VMB.MEM.MICROWIRE_93LC46C",
                 "VMB.AT.PIC8259A", "VMB.AT.PIT8254", "VMB.AT.DMA8237A", "VMB.AT.KBC8042", "VMB.AT.RTC146818"
                Return True
            Case Else : Return VmbDigitalIcRegistry.Supports(moduleIdInBed)
        End Select
    End Function

    Public Shared Function CreatePrimaryIsaModule(moduleIdInBed As String, basePortInBed As UInt16, irqInBed As Integer, allowedPortsInBed As UInt16(), allowedIrqsInBed As Integer()) As Object
        Select Case moduleIdInBed
            Case "VMB.NET.RTL8019AS"
                Return New Ne2000Isa(basePortInBed, irqInBed, MasterPic, SlavePic, Nothing, allowedPortsInBed, allowedIrqsInBed)
            Case Else
                Throw New NotSupportedException("Module '" & moduleIdInBed & "' is not an ISA card root module.")
        End Select
    End Function
End Class
