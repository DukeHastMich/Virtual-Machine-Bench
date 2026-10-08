Imports System
Imports System.Collections.Generic
Imports System.IO
Imports System.Reflection
Imports System.Runtime.Loader
Imports System.Text.Json
Imports System.Linq

' Stable ABI between VMBS-manufactured Virtual Circuit Boards and VMB.
Public Interface IVmbBoardRuntime
    Sub AllocateModule(instanceId As String, moduleId As String, partId As String)
    Sub AttachPlatform(attachmentId As String, platformId As String, signalsJson As String)
    Sub CreateControl(controlId As String, kind As String, elements As Integer, changeWhilePowered As Boolean, storage As String)
    Sub BindConfigPin(instanceId As String, pin As String, role As String, sampling As String, sourceJson As String)
    Sub DefineProperty(instanceId As String, propertyName As String, status As String, definitionJson As String)
    Sub Link(linkId As String, netName As String, endpointsJson As String)
    Sub CreateDeviceSlot(slotId As String, instanceId As String, definitionJson As String)
    Sub FinalizeBoard(hardwareTits As Integer, executionTits As Integer, defaultExecutionTits As Integer)
End Interface

Public Interface IVirtualCircuitBoard
    ReadOnly Property BoardId As String
    ReadOnly Property DisplayName As String
    ReadOnly Property Category As String
    ReadOnly Property Bus As String
    ReadOnly Property ConnectorBits As Integer
    ReadOnly Property InstallHeaderJson As String
    Sub Build(runtime As IVmbBoardRuntime)
End Interface

Public NotInheritable Class VcbCatalogEntry
    Public Property FilePath As String
    Public Property BoardId As String
    Public Property DisplayName As String
    Public Property Category As String
    Public Property Bus As String
    Public Property ConnectorBits As Integer
End Class

' VMB polls only PCB.meta.  DLL enumeration happens only when the producer-published
' board count changes or an explicit initial/forced scan is requested.
Public NotInheritable Class VcbStore
    Private Shared _lastPublishedCount As Integer = Integer.MinValue
    Private Shared _cache As List(Of VcbCatalogEntry)
    Private Sub New()
    End Sub

    Public Shared Function StoreDirectory() As String
        ' AppContext is normally VMB\bin\... during development. Walk only upward
        ' inside this fixed VMB tree until the canonical VMB\VMBS\VCB Store exists.
        Dim cursor As New DirectoryInfo(AppContext.BaseDirectory)
        While cursor IsNot Nothing
            Dim candidate = Path.Combine(cursor.FullName, "VMBS", "VCB Store")
            If Directory.Exists(candidate) Then Return candidate
            cursor = cursor.Parent
        End While
        Return Path.Combine(AppContext.BaseDirectory, "VMBS", "VCB Store")
    End Function

    Public Shared Function MetaFilePath() As String
        Return Path.Combine(StoreDirectory(), "PCB.meta")
    End Function

    Public Shared Function ReadPublishedBoardCount() As Integer
        Dim n As Integer
        If Not Integer.TryParse(File.ReadAllText(MetaFilePath()).Trim(), n) OrElse n < 0 Then
            Throw New InvalidDataException("PCB.meta must contain one non-negative integer.")
        End If
        Return n
    End Function

    Public Shared Function PollBoardCountChanged() As Boolean
        Try
            Dim n = ReadPublishedBoardCount()
            If _lastPublishedCount = Integer.MinValue Then
                _lastPublishedCount = n
                Return False
            End If
            If n = _lastPublishedCount Then Return False
            _lastPublishedCount = n
            _cache = Nothing
            Return True
        Catch
            Return False
        End Try
    End Function

    Public Shared Sub ForceCatalogRefresh()
        _cache = Nothing
        Try
            _lastPublishedCount = ReadPublishedBoardCount()
        Catch
            _lastPublishedCount = Integer.MinValue
        End Try
    End Sub

    Public Shared Function EnumerateBoards(Optional category As String = Nothing) As List(Of VcbCatalogEntry)
        If _cache Is Nothing Then
            Dim found As New List(Of VcbCatalogEntry)()
            Directory.CreateDirectory(StoreDirectory())
            For Each dllPath In Directory.GetFiles(StoreDirectory(), "*.vcb.dll", SearchOption.AllDirectories)
                Try
                    Dim board = LoadDefinition(dllPath)
                    found.Add(New VcbCatalogEntry With {
                        .FilePath = dllPath, .BoardId = board.BoardId, .DisplayName = board.DisplayName,
                        .Category = board.Category, .Bus = board.Bus, .ConnectorBits = board.ConnectorBits})
                Catch
                    ' A broken third-party VCB does not take down Configurator.
                End Try
            Next
            found.Sort(Function(a,b) StringComparer.OrdinalIgnoreCase.Compare(a.DisplayName,b.DisplayName))
            _cache = found
            Try : _lastPublishedCount = ReadPublishedBoardCount() : Catch : End Try
        End If
        If String.IsNullOrWhiteSpace(category) Then Return New List(Of VcbCatalogEntry)(_cache)
        Return _cache.FindAll(Function(x) String.Equals(x.Category, category, StringComparison.OrdinalIgnoreCase))
    End Function

    Public Shared Function FindById(boardId As String) As VcbCatalogEntry
        Return EnumerateBoards().Find(Function(x) String.Equals(x.BoardId, boardId, StringComparison.OrdinalIgnoreCase))
    End Function

    Public Shared Function LoadDefinition(path As String) As IVirtualCircuitBoard
        Dim fullPath = System.IO.Path.GetFullPath(path)
        Dim asm = AssemblyLoadContext.Default.LoadFromAssemblyPath(fullPath)
        For Each t In asm.GetExportedTypes()
            If GetType(IVirtualCircuitBoard).IsAssignableFrom(t) AndAlso Not t.IsAbstract AndAlso Not t.IsInterface Then
                Return DirectCast(Activator.CreateInstance(t), IVirtualCircuitBoard)
            End If
        Next
        Throw New InvalidDataException("VCB DLL exports no IVirtualCircuitBoard implementation: " & fullPath)
    End Function
End Class

' Materialization target used by generated VCB code.  CP29 keeps the existing
' RTL8019 FAT contraction: generated wiring/configuration is consumed, while the
' root NIC module owns the invariant surrounding behavior at VMB resolution.
Public NotInheritable Class VcbIsaMaterializationRuntime
    Implements IVmbBoardRuntime
    Private ReadOnly _slotId As String
    Private ReadOnly _board As IVirtualCircuitBoard
    Private ReadOnly _ioOverride As Nullable(Of UInt16)
    Private ReadOnly _irqOverride As Nullable(Of Integer)
    Private ReadOnly _modules As New Dictionary(Of String,String)(StringComparer.OrdinalIgnoreCase)
    Private ReadOnly _properties As New Dictionary(Of String,JsonElement)(StringComparer.OrdinalIgnoreCase)
    Public Property InstalledCard As Object

    Public Sub New(slotId As String, board As IVirtualCircuitBoard, ioOverride As Nullable(Of UInt16), irqOverride As Nullable(Of Integer))
        _slotId=slotId : _board=board : _ioOverride=ioOverride : _irqOverride=irqOverride
    End Sub
    Public Sub AllocateModule(instanceId As String,moduleId As String,partId As String) Implements IVmbBoardRuntime.AllocateModule
        _modules(instanceId)=moduleId
    End Sub
    Public Sub AttachPlatform(attachmentId As String,platformId As String,signalsJson As String) Implements IVmbBoardRuntime.AttachPlatform
        If Not platformId.StartsWith("ISA",StringComparison.OrdinalIgnoreCase) Then Throw New NotSupportedException("CP29 ISA materializer cannot attach platform " & platformId)
    End Sub
    Public Sub CreateControl(controlId As String,kind As String,elements As Integer,changeWhilePowered As Boolean,storage As String) Implements IVmbBoardRuntime.CreateControl
        ' Physical controls are surfaced from InstallHeaderJson by Board Properties.
    End Sub
    Public Sub BindConfigPin(instanceId As String,pin As String,role As String,sampling As String,sourceJson As String) Implements IVmbBoardRuntime.BindConfigPin
    End Sub
    Public Sub DefineProperty(instanceId As String,propertyName As String,status As String,definitionJson As String) Implements IVmbBoardRuntime.DefineProperty
        Using d=JsonDocument.Parse(definitionJson) : _properties(propertyName)=d.RootElement.Clone() : End Using
    End Sub
    Public Sub Link(linkId As String,netName As String,endpointsJson As String) Implements IVmbBoardRuntime.Link
        ' Links remain compiler-emitted truth. The current FAT NIC backend consumes
        ' the resolved ISA/config domains rather than allocating one CLR object per net.
    End Sub
    Public Sub CreateDeviceSlot(slotId As String,instanceId As String,definitionJson As String) Implements IVmbBoardRuntime.CreateDeviceSlot
        ' Persistent slot ownership is machine-instance state; Board Properties handles payload installation.
    End Sub
    Public Sub FinalizeBoard(hardwareTits As Integer,executionTits As Integer,defaultExecutionTits As Integer) Implements IVmbBoardRuntime.FinalizeBoard
        If hardwareTits<>0 OrElse defaultExecutionTits<>0 Then Throw New InvalidDataException("VCB is incomplete for default VMB execution.")
        Dim primary = _modules.Values.FirstOrDefault(Function(x) x.StartsWith("VMB.NET.",StringComparison.OrdinalIgnoreCase))
        If primary Is Nothing Then Throw New InvalidDataException("VCB has no supported ISA root module.")
        Dim ioDomain=NumericDomainU16("IO_BASE") : Dim irqDomain=NumericDomainInt("IRQ")
        Dim io As UInt16=If(_ioOverride.HasValue,_ioOverride.Value,DefaultU16("IO_BASE",ioDomain))
        Dim irq As Integer=If(_irqOverride.HasValue,_irqOverride.Value,DefaultInt("IRQ",irqDomain))
        If Array.IndexOf(ioDomain,io)<0 Then Throw New InvalidDataException("I/O base is not physically realizable by this VCB.")
        If Array.IndexOf(irqDomain,irq)<0 Then Throw New InvalidDataException("IRQ is not physically realizable by this VCB.")
        Dim def As New IsaCardDefinition286(_board.BoardId,_board.DisplayName,If(_board.ConnectorBits<=8,IsaSlotConnectorWidth286.EightBit,IsaSlotConnectorWidth286.SixteenBit),Function() VmbModuleRegistry.CreatePrimaryIsaModule(primary,io,irq,ioDomain,irqDomain))
        InstalledCard=IsaBackplane.InstallCard(_slotId,def)
    End Sub
    Private Function NumericDomainU16(name As String) As UInt16()
        If Not _properties.ContainsKey(name) Then Return Array.Empty(Of UInt16)()
        Dim a As New List(Of UInt16)() : For Each x In _properties(name).GetProperty("allowed_values").EnumerateArray() : If x.ValueKind=JsonValueKind.Number Then a.Add(CUShort(x.GetInt32()))
        Next : Return a.ToArray()
    End Function
    Private Function NumericDomainInt(name As String) As Integer()
        If Not _properties.ContainsKey(name) Then Return Array.Empty(Of Integer)()
        Dim a As New List(Of Integer)() : For Each x In _properties(name).GetProperty("allowed_values").EnumerateArray() : If x.ValueKind=JsonValueKind.Number Then a.Add(x.GetInt32())
        Next : Return a.ToArray()
    End Function
    Private Function DefaultU16(name As String,domain As UInt16()) As UInt16
        Dim e=_properties(name) : Dim d As JsonElement : If e.TryGetProperty("default_value",d) AndAlso d.ValueKind=JsonValueKind.Number Then Return CUShort(d.GetInt32())
        If domain.Length=0 Then Throw New InvalidDataException("No " & name & " domain.") Else Return domain(0)
    End Function
    Private Function DefaultInt(name As String,domain As Integer()) As Integer
        Dim e=_properties(name) : Dim d As JsonElement : If e.TryGetProperty("default_value",d) AndAlso d.ValueKind=JsonValueKind.Number Then Return d.GetInt32()
        If domain.Length=0 Then Throw New InvalidDataException("No " & name & " domain.") Else Return domain(0)
    End Function
End Class
