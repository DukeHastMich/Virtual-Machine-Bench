Imports System
Imports System.Collections.Generic

' Physical ISA backplane/chassis population layer.
'
' This deliberately models card installation separately from card configuration.
' An empty slot contains no guest device object on HardwareBus at all.  Installing
' or removing a board is a cold chassis/service action; ISA hot-plug is not being
' invented for an AT which never had it.
Public Enum IsaSlotConnectorWidth286 As Byte
    EightBit = 8
    SixteenBit = 16
End Enum

' Host-side catalog entry for a kind of physical ISA board.  The factory is only
' invoked when a board is actually installed in a slot, so selecting None later
' can mean no instantiated card, no host worker and no guest bus participation.
Public NotInheritable Class IsaCardDefinition286
    Private ReadOnly _factoryInBed As Func(Of Object)

    Public Sub New(idInBed As String,
                   displayNameInBed As String,
                   minimumConnectorWidthInBed As IsaSlotConnectorWidth286,
                   factoryInBed As Func(Of Object))
        If String.IsNullOrWhiteSpace(idInBed) Then Throw New ArgumentNullException(NameOf(idInBed))
        If String.IsNullOrWhiteSpace(displayNameInBed) Then Throw New ArgumentNullException(NameOf(displayNameInBed))
        If factoryInBed Is Nothing Then Throw New ArgumentNullException(NameOf(factoryInBed))
        If minimumConnectorWidthInBed <> IsaSlotConnectorWidth286.EightBit AndAlso
           minimumConnectorWidthInBed <> IsaSlotConnectorWidth286.SixteenBit Then
            Throw New ArgumentOutOfRangeException(NameOf(minimumConnectorWidthInBed))
        End If

        Id = idInBed.Trim()
        DisplayName = displayNameInBed.Trim()
        MinimumConnectorWidth = minimumConnectorWidthInBed
        _factoryInBed = factoryInBed
    End Sub

    Public ReadOnly Property Id As String
    Public ReadOnly Property DisplayName As String
    Public ReadOnly Property MinimumConnectorWidth As IsaSlotConnectorWidth286

    Friend Function CreatePhysicalDeviceInBed() As Object
        Dim deviceInBed As Object = _factoryInBed()
        If deviceInBed Is Nothing Then
            Throw New InvalidOperationException("ISA card factory '" & Id & "' returned no physical device.")
        End If
        Return deviceInBed
    End Function
End Class

' One physical edge connector on the motherboard/backplane.  The slot object is
' configuration/chassis metadata and remains even when electrically empty.
Public NotInheritable Class IsaBackplaneSlot286
    Friend Sub New(idInBed As String,
                   displayNameInBed As String,
                   connectorWidthInBed As IsaSlotConnectorWidth286)
        Id = idInBed
        DisplayName = displayNameInBed
        ConnectorWidth = connectorWidthInBed
    End Sub

    Public ReadOnly Property Id As String
    Public ReadOnly Property DisplayName As String
    Public ReadOnly Property ConnectorWidth As IsaSlotConnectorWidth286

    Friend Property InstalledDefinitionInBed As IsaCardDefinition286
    Friend Property InstalledDeviceInBed As Object

    Public ReadOnly Property IsEmpty As Boolean
        Get
            Return InstalledDeviceInBed Is Nothing
        End Get
    End Property

    Public ReadOnly Property InstalledCardId As String
        Get
            If InstalledDefinitionInBed Is Nothing Then Return String.Empty
            Return InstalledDefinitionInBed.Id
        End Get
    End Property

    Public ReadOnly Property InstalledCardName As String
        Get
            If InstalledDefinitionInBed Is Nothing Then Return "None"
            Return InstalledDefinitionInBed.DisplayName
        End Get
    End Property

    Public ReadOnly Property InstalledDevice As Object
        Get
            Return InstalledDeviceInBed
        End Get
    End Property
End Class

Public NotInheritable Class IsaBackplane286
    Private ReadOnly _busInBed As HardwareBus
    Private ReadOnly _decodeAnchorInBed As Object
    Private ReadOnly _slotsInBed As New Dictionary(Of String, IsaBackplaneSlot286)(StringComparer.OrdinalIgnoreCase)
    Private _chassisPoweredInBed As Boolean

    Public Sub New(busInBed As HardwareBus, Optional decodeAnchorInBed As Object = Nothing)
        If busInBed Is Nothing Then Throw New ArgumentNullException(NameOf(busInBed))
        _busInBed = busInBed
        _decodeAnchorInBed = decodeAnchorInBed
    End Sub

    ' Defines a physical connector.  This does not install a board.
    Public Function DefineSlot(idInBed As String,
                               displayNameInBed As String,
                               connectorWidthInBed As IsaSlotConnectorWidth286) As IsaBackplaneSlot286
        If String.IsNullOrWhiteSpace(idInBed) Then Throw New ArgumentNullException(NameOf(idInBed))
        If String.IsNullOrWhiteSpace(displayNameInBed) Then Throw New ArgumentNullException(NameOf(displayNameInBed))
        If connectorWidthInBed <> IsaSlotConnectorWidth286.EightBit AndAlso
           connectorWidthInBed <> IsaSlotConnectorWidth286.SixteenBit Then
            Throw New ArgumentOutOfRangeException(NameOf(connectorWidthInBed))
        End If

        Dim normalizedIdInBed As String = idInBed.Trim()
        If _slotsInBed.ContainsKey(normalizedIdInBed) Then
            Throw New InvalidOperationException("ISA slot '" & normalizedIdInBed & "' is already defined.")
        End If

        Dim slotInBed As New IsaBackplaneSlot286(normalizedIdInBed,
                                                  displayNameInBed.Trim(),
                                                  connectorWidthInBed)
        _slotsInBed.Add(normalizedIdInBed, slotInBed)
        Return slotInBed
    End Function

    ' The chassis owner calls this at the same lifecycle boundary as the front
    ' panel power switch.  It intentionally does not power/reset HardwareBus by
    ' itself; it only prevents physically impossible live ISA service operations.
    Public Sub SetChassisPowered(poweredInBed As Boolean)
        _chassisPoweredInBed = poweredInBed
    End Sub

    Public ReadOnly Property ChassisPowered As Boolean
        Get
            Return _chassisPoweredInBed
        End Get
    End Property

    Public Function FindSlot(idInBed As String) As IsaBackplaneSlot286
        If String.IsNullOrWhiteSpace(idInBed) Then Return Nothing
        Dim slotInBed As IsaBackplaneSlot286 = Nothing
        If _slotsInBed.TryGetValue(idInBed.Trim(), slotInBed) Then Return slotInBed
        Return Nothing
    End Function

    Public Function Slots() As IsaBackplaneSlot286()
        If _slotsInBed.Count = 0 Then Return Array.Empty(Of IsaBackplaneSlot286)()
        Dim resultInBed(_slotsInBed.Count - 1) As IsaBackplaneSlot286
        _slotsInBed.Values.CopyTo(resultInBed, 0)
        Return resultInBed
    End Function

    ' Install a fresh physical board.  The factory runs only here; an empty slot
    ' therefore has no detached emulated card consuming clock/worker resources.
    Public Function InstallCard(slotIdInBed As String,
                                definitionInBed As IsaCardDefinition286) As Object
        EnsureChassisOpenInBed()
        If definitionInBed Is Nothing Then Throw New ArgumentNullException(NameOf(definitionInBed))

        Dim slotInBed As IsaBackplaneSlot286 = RequireSlotInBed(slotIdInBed)
        If Not slotInBed.IsEmpty Then
            Throw New InvalidOperationException("ISA slot '" & slotInBed.Id & "' is already populated by " &
                                                slotInBed.InstalledCardName & ".")
        End If
        If CInt(slotInBed.ConnectorWidth) < CInt(definitionInBed.MinimumConnectorWidth) Then
            Throw New InvalidOperationException(definitionInBed.DisplayName &
                                                " requires a " & CInt(definitionInBed.MinimumConnectorWidth).ToString() &
                                                "-bit ISA connector; slot '" & slotInBed.Id & "' is only " &
                                                CInt(slotInBed.ConnectorWidth).ToString() & "-bit.")
        End If

        Dim deviceInBed As Object = definitionInBed.CreatePhysicalDeviceInBed()
        Try
            If _decodeAnchorInBed IsNot Nothing AndAlso _busInBed.IsRegistered(_decodeAnchorInBed) Then
                _busInBed.RegisterBefore(deviceInBed, _decodeAnchorInBed)
            Else
                _busInBed.Register(deviceInBed)
            End If
        Catch
            DisposePhysicalDeviceInBed(deviceInBed, swallowErrorsInBed:=True)
            Throw
        End Try

        slotInBed.InstalledDefinitionInBed = definitionInBed
        slotInBed.InstalledDeviceInBed = deviceInBed
        Return deviceInBed
    End Function

    ' Remove the board from the connector.  After this returns successfully the
    ' guest bus has no decode/clock/reset membership for it and the old card model
    ' has been disposed.  Reinstallation creates a new physical instance.
    Public Function RemoveCard(slotIdInBed As String) As Boolean
        EnsureChassisOpenInBed()
        Dim slotInBed As IsaBackplaneSlot286 = RequireSlotInBed(slotIdInBed)
        If slotInBed.IsEmpty Then Return False

        Dim deviceInBed As Object = slotInBed.InstalledDeviceInBed
        slotInBed.InstalledDeviceInBed = Nothing
        slotInBed.InstalledDefinitionInBed = Nothing

        Dim wasRegisteredInBed As Boolean = _busInBed.Unregister(deviceInBed)
        If Not wasRegisteredInBed Then
            DisposePhysicalDeviceInBed(deviceInBed, swallowErrorsInBed:=True)
            Throw New InvalidOperationException("ISA backplane state was inconsistent: the installed board was not registered on HardwareBus.")
        End If

        DisposePhysicalDeviceInBed(deviceInBed, swallowErrorsInBed:=False)
        Return True
    End Function

    Private Function RequireSlotInBed(idInBed As String) As IsaBackplaneSlot286
        Dim slotInBed As IsaBackplaneSlot286 = FindSlot(idInBed)
        If slotInBed Is Nothing Then
            Throw New KeyNotFoundException("Unknown ISA slot '" & If(idInBed, String.Empty) & "'.")
        End If
        Return slotInBed
    End Function

    Private Sub EnsureChassisOpenInBed()
        If _chassisPoweredInBed Then
            Throw New InvalidOperationException("ISA cards may only be installed or removed while chassis power is off.")
        End If
    End Sub

    Private Shared Sub DisposePhysicalDeviceInBed(deviceInBed As Object,
                                                   swallowErrorsInBed As Boolean)
        Dim disposableInBed As IDisposable = TryCast(deviceInBed, IDisposable)
        If disposableInBed Is Nothing Then Return

        If swallowErrorsInBed Then
            Try
                disposableInBed.Dispose()
            Catch
                ' Preserve the original installation failure.
            End Try
            Return
        End If

        disposableInBed.Dispose()
    End Sub
End Class
