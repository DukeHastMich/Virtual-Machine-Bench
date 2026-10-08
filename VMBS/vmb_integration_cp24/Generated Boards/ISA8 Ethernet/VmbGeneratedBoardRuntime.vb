Imports System
Imports System.Collections.Generic
Imports System.Text.Json

' VMBS -> existing VMB integration adapter.
'
' This is deliberately an adapter, not Board Script knowledge in the emulator.
' Generated board source supplies module identities, legal configuration domains,
' platform attachment and wiring.  This adapter maps stable MODULE_IDs onto the
' current VMB implementation classes.
'
' Compatibility note: the current Ne2000Isa class is a board-level/fat NE2000
' implementation.  For DEFAULT_EXECUTION only, VMB.NET.RTL8019AS is mapped onto
' that existing implementation.  The generated U2 flash and U3 EEPROM remain
' represented by VMBS but are not swallowed as a claim of full configuration
' fidelity: the generated board reports full VMB_EXECUTION TITS(up) until those
' component behaviors/payloads are stitched separately.  Default boot ROM is off.
Public NotInheritable Class VmbGeneratedBoardRuntime
    Implements IVmbBoardRuntime

    Private ReadOnly _moduleIdsInBed As New Dictionary(Of String, String)(StringComparer.OrdinalIgnoreCase)
    Private ReadOnly _propertiesInBed As New Dictionary(Of String, JsonElement)(StringComparer.OrdinalIgnoreCase)
    Private ReadOnly _ioBaseOverrideInBed As Nullable(Of UInt16)
    Private ReadOnly _irqOverrideInBed As Nullable(Of Integer)
    Private _isa8AttachedInBed As Boolean
    Private _rtlInstanceInBed As String
    Private _installedNe2000InBed As Ne2000Isa

    Public Sub New(Optional ioBaseOverrideInBed As Nullable(Of UInt16) = Nothing,
                   Optional irqOverrideInBed As Nullable(Of Integer) = Nothing)
        _ioBaseOverrideInBed = ioBaseOverrideInBed
        _irqOverrideInBed = irqOverrideInBed
    End Sub

    Public ReadOnly Property InstalledNe2000 As Ne2000Isa
        Get
            Return _installedNe2000InBed
        End Get
    End Property

    Public Sub AllocateModule(instanceId As String, moduleId As String, partId As String) Implements IVmbBoardRuntime.AllocateModule
        _moduleIdsInBed.Add(instanceId, moduleId)
        If String.Equals(moduleId, "VMB.NET.RTL8019AS", StringComparison.OrdinalIgnoreCase) Then
            If _rtlInstanceInBed IsNot Nothing Then Throw New InvalidOperationException("Generated board contains more than one RTL8019AS instance; current compatibility adapter expects one.")
            _rtlInstanceInBed = instanceId
        End If
    End Sub

    Public Sub AttachPlatform(attachmentId As String, platformId As String, signalsJson As String) Implements IVmbBoardRuntime.AttachPlatform
        If String.Equals(platformId, "ISA8", StringComparison.OrdinalIgnoreCase) Then _isa8AttachedInBed = True
    End Sub

    Public Sub CreateControl(controlId As String, kind As String, elements As Integer, changeWhilePowered As Boolean, storage As String) Implements IVmbBoardRuntime.CreateControl
        ' Controls remain machine-instance configuration.  The current compatibility
        ' adapter consumes the decoded IO/IRQ result rather than emulating DIP contacts.
    End Sub

    Public Sub BindConfigPin(instanceId As String, pin As String, role As String, sampling As String, sourceJson As String) Implements IVmbBoardRuntime.BindConfigPin
        ' Temporal pin semantics are retained in generated source.  Current Ne2000Isa
        ' exposes cold-service ConfigureHardware for the IO/IRQ results it can represent.
    End Sub

    Public Sub DefineProperty(instanceId As String, propertyName As String, status As String, definitionJson As String) Implements IVmbBoardRuntime.DefineProperty
        Using docInBed As JsonDocument = JsonDocument.Parse(definitionJson)
            _propertiesInBed(instanceId & ":" & propertyName) = docInBed.RootElement.Clone()
        End Using
    End Sub

    Public Sub Link(linkId As String, netName As String, endpointsJson As String) Implements IVmbBoardRuntime.Link
        ' The existing fat Ne2000Isa owns its internal DP8390/card wiring.  Link data is
        ' intentionally not discarded by VMBS; this adapter simply cannot consume every
        ' internal component edge until the skinny RTL8019/EEPROM/flash modules exist.
    End Sub

    Public Sub CreateDeviceSlot(slotId As String, instanceId As String, definitionJson As String) Implements IVmbBoardRuntime.CreateDeviceSlot
        ' Machine-local persistent slots are represented by generated VMBS metadata.
        ' Default execution does not require U2 because BOOT_ROM defaults disabled.
    End Sub

    Public Sub FinalizeBoard(hardwareTits As Integer, executionTits As Integer, defaultExecutionTits As Integer) Implements IVmbBoardRuntime.FinalizeBoard
        If hardwareTits <> 0 Then Throw New InvalidOperationException("Generated VMBS board is hardware-incomplete (TITS up).")
        If defaultExecutionTits <> 0 Then Throw New InvalidOperationException("Generated VMBS board cannot execute its default configuration (TITS up).")
        If String.IsNullOrWhiteSpace(_rtlInstanceInBed) Then Throw New InvalidOperationException("Generated board has no VMB.NET.RTL8019AS module.")
        If Not _isa8AttachedInBed Then Throw New InvalidOperationException("Generated RTL8019 board is not attached to an ISA8 platform.")

        Dim ioBaseInBed As UInt16 = CUShort(ReadDefaultIntegerInBed(_rtlInstanceInBed, "IO_BASE"))
        Dim irqInBed As Integer = ReadDefaultIntegerInBed(_rtlInstanceInBed, "IRQ")
        If _ioBaseOverrideInBed.HasValue Then
            RequireAllowedIntegerInBed(_rtlInstanceInBed, "IO_BASE", _ioBaseOverrideInBed.Value)
            ioBaseInBed = _ioBaseOverrideInBed.Value
        End If
        If _irqOverrideInBed.HasValue Then
            RequireAllowedIntegerInBed(_rtlInstanceInBed, "IRQ", _irqOverrideInBed.Value)
            irqInBed = _irqOverrideInBed.Value
        End If

        Dim definitionInBed As New IsaCardDefinition286(
            MachineHardwarePopulationConfiguration.Ne2000Id,
            "VMBS generated RTL8019AS / NE2000-compatible ISA8 board",
            IsaSlotConnectorWidth286.EightBit,
            Function()
                Dim cardInBed As New Ne2000Isa(ioBaseInBed, irqInBed, MasterPic, SlavePic)
                cardInBed.ConfigureHardware(ioBaseInBed, irqInBed)
                Return cardInBed
            End Function)

        _installedNe2000InBed = DirectCast(IsaBackplane.InstallCard(NetworkIsaSlotId, definitionInBed), Ne2000Isa)
    End Sub

    Private Function ReadDefaultIntegerInBed(instanceIdInBed As String, propertyNameInBed As String) As Integer
        Dim definitionInBed As JsonElement
        If Not _propertiesInBed.TryGetValue(instanceIdInBed & ":" & propertyNameInBed, definitionInBed) Then
            Throw New InvalidOperationException("Generated board did not define " & propertyNameInBed & ".")
        End If
        Dim valueInBed As JsonElement
        If Not definitionInBed.TryGetProperty("default_value", valueInBed) OrElse valueInBed.ValueKind <> JsonValueKind.Number Then
            Throw New InvalidOperationException("Generated board has no numeric default for " & propertyNameInBed & ".")
        End If
        Return valueInBed.GetInt32()
    End Function

    Private Sub RequireAllowedIntegerInBed(instanceIdInBed As String, propertyNameInBed As String, valueInBed As Integer)
        Dim definitionInBed As JsonElement
        If Not _propertiesInBed.TryGetValue(instanceIdInBed & ":" & propertyNameInBed, definitionInBed) Then Throw New InvalidOperationException("Generated board property is missing: " & propertyNameInBed)
        Dim allowedInBed As JsonElement
        If Not definitionInBed.TryGetProperty("allowed_values", allowedInBed) OrElse allowedInBed.ValueKind <> JsonValueKind.Array Then Throw New InvalidOperationException("Generated board property has no allowed domain: " & propertyNameInBed)
        For Each candidateInBed As JsonElement In allowedInBed.EnumerateArray()
            If candidateInBed.ValueKind = JsonValueKind.Number AndAlso candidateInBed.GetInt32() = valueInBed Then Return
        Next
        Throw New ArgumentOutOfRangeException(propertyNameInBed, valueInBed, "Requested machine configuration is not physically selectable on this generated board.")
    End Sub
End Class
