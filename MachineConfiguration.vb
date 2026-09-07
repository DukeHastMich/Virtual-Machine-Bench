Imports System
Imports System.Collections.Generic
Imports System.IO

' Host/chassis configuration store.  This file describes what is physically
' installed in the virtual machine; it is not guest-visible CMOS state.
Public NotInheritable Class MachineConfigurationStore
    Private Sub New()
    End Sub

    Public Shared Function ReadValue(path As String,
                                     section As String,
                                     key As String,
                                     defaultValue As String) As String
        If String.IsNullOrWhiteSpace(path) OrElse Not File.Exists(path) Then Return defaultValue

        Dim activeSection As String = String.Empty
        For Each rawLine As String In File.ReadAllLines(path)
            Dim line As String = rawLine.Trim()
            If line.Length = 0 OrElse line.StartsWith("#", StringComparison.Ordinal) OrElse
               line.StartsWith(";", StringComparison.Ordinal) Then Continue For

            If line.StartsWith("[", StringComparison.Ordinal) AndAlso line.EndsWith("]", StringComparison.Ordinal) Then
                activeSection = line.Substring(1, line.Length - 2).Trim()
                Continue For
            End If

            If Not activeSection.Equals(section, StringComparison.OrdinalIgnoreCase) Then Continue For
            Dim equalsAt As Integer = line.IndexOf("="c)
            If equalsAt <= 0 Then Continue For
            Dim candidateKey As String = line.Substring(0, equalsAt).Trim()
            If candidateKey.Equals(key, StringComparison.OrdinalIgnoreCase) Then
                Return line.Substring(equalsAt + 1).Trim()
            End If
        Next

        Return defaultValue
    End Function

    Public Shared Sub WriteValue(path As String,
                                 section As String,
                                 key As String,
                                 value As String)
        If String.IsNullOrWhiteSpace(path) Then Throw New ArgumentNullException(NameOf(path))
        If String.IsNullOrWhiteSpace(section) Then Throw New ArgumentNullException(NameOf(section))
        If String.IsNullOrWhiteSpace(key) Then Throw New ArgumentNullException(NameOf(key))

        Dim lines As New List(Of String)()
        If File.Exists(path) Then lines.AddRange(File.ReadAllLines(path))
        If lines.Count = 0 Then
            lines.Add("# Cromwell Technologies Virtual Computer host chassis configuration")
            lines.Add("# Host-only. This file is not visible through CMOS, ATA, or guest RAM.")
        End If

        Dim sectionStart As Integer = -1
        Dim sectionEnd As Integer = lines.Count
        Dim keyIndex As Integer = -1
        Dim activeSection As String = String.Empty

        For index As Integer = 0 To lines.Count - 1
            Dim line As String = lines(index).Trim()
            If line.StartsWith("[", StringComparison.Ordinal) AndAlso line.EndsWith("]", StringComparison.Ordinal) Then
                Dim foundSection As String = line.Substring(1, line.Length - 2).Trim()
                If sectionStart >= 0 AndAlso sectionEnd = lines.Count Then
                    sectionEnd = index
                    Exit For
                End If
                activeSection = foundSection
                If foundSection.Equals(section, StringComparison.OrdinalIgnoreCase) Then sectionStart = index
                Continue For
            End If

            If sectionStart >= 0 AndAlso activeSection.Equals(section, StringComparison.OrdinalIgnoreCase) Then
                Dim equalsAt As Integer = line.IndexOf("="c)
                If equalsAt > 0 Then
                    Dim candidateKey As String = line.Substring(0, equalsAt).Trim()
                    If candidateKey.Equals(key, StringComparison.OrdinalIgnoreCase) Then
                        keyIndex = index
                        Exit For
                    End If
                End If
            End If
        Next

        Dim assignment As String = key & "=" & If(value, String.Empty)
        If keyIndex >= 0 Then
            lines(keyIndex) = assignment
        ElseIf sectionStart >= 0 Then
            lines.Insert(sectionEnd, assignment)
        Else
            If lines.Count > 0 AndAlso lines(lines.Count - 1).Length <> 0 Then lines.Add(String.Empty)
            lines.Add("[" & section & "]")
            lines.Add(assignment)
        End If

        File.WriteAllLines(path, lines)
    End Sub
End Class

Public NotInheritable Class RamBankConfiguration
    Private Const MachineConfigurationName As String = "VirtualComputer.machine.ini"
    Private ReadOnly _configurationPath As String

    Public Shared ReadOnly SupportedMemoryMegabytes As Integer() = {1, 2, 4, 8, 12, 16}

    Public Sub New(baseDirectory As String)
        If String.IsNullOrWhiteSpace(baseDirectory) Then Throw New ArgumentNullException(NameOf(baseDirectory))
        _configurationPath = Path.Combine(Path.GetFullPath(baseDirectory), MachineConfigurationName)
    End Sub

    Public Property InstalledMemoryMb As Integer = 4

    Public ReadOnly Property ConfigurationPath As String
        Get
            Return _configurationPath
        End Get
    End Property

    Public Sub LoadConfiguration()
        Dim raw As String = MachineConfigurationStore.ReadValue(_configurationPath, "Machine", "MemoryMB", "4")
        Dim parsed As Integer
        If Integer.TryParse(raw, parsed) AndAlso IsSupported(parsed) Then
            InstalledMemoryMb = parsed
        Else
            InstalledMemoryMb = 4
        End If
    End Sub

    Public Sub SaveConfiguration()
        MachineConfigurationStore.WriteValue(_configurationPath, "Machine", "MemoryMB", InstalledMemoryMb.ToString())
    End Sub

    Public Shared Function IsSupported(megabytes As Integer) As Boolean
        For Each candidate As Integer In SupportedMemoryMegabytes
            If candidate = megabytes Then Return True
        Next
        Return False
    End Function
End Class
' Physical population of the two standard ISA IDE channels. Each ribbon has
' master/slave electrical positions and either socket may contain an ATA hard
' disk, an ATAPI CD-ROM, or remain empty. Media attachment is deliberately stored
' separately so changing a disk/ISO is not confused with replacing a mechanism.
Public NotInheritable Class IdeHardwareConfiguration
    Private Const MachineConfigurationName As String = "VirtualComputer.machine.ini"
    Private Const CurrentSchemaVersion As Integer = 3
    Private ReadOnly _configurationPath As String

    Public Sub New(baseDirectory As String)
        If String.IsNullOrWhiteSpace(baseDirectory) Then Throw New ArgumentNullException(NameOf(baseDirectory))
        _configurationPath = Path.Combine(Path.GetFullPath(baseDirectory), MachineConfigurationName)
    End Sub

    Public Property PrimaryMasterKind As IdeDeviceKind = IdeDeviceKind.AtaHardDisk
    Public Property PrimarySlaveKind As IdeDeviceKind = IdeDeviceKind.AtapiCdRom
    ' Adding a second IDE channel adds two electrical sockets; it must not silently
    ' populate those sockets with phantom drives.  New/migrated machines therefore
    ' retain the historical primary HDD + CD-ROM pair and leave secondary empty
    ' until the user installs hardware in System Configuration.
    Public Property SecondaryMasterKind As IdeDeviceKind = IdeDeviceKind.Empty
    Public Property SecondarySlaveKind As IdeDeviceKind = IdeDeviceKind.Empty

    ' ATA model profile is physical hardware identity, distinct from the raw
    ' platter image selected from the IDE shelf.  The profile is committed to
    ' that image's .meta sidecar when the chassis hardware change is applied.
    Public Property PrimaryMasterModelId As String = IdeDriveModelCatalog.Generic64MiBId
    Public Property PrimarySlaveModelId As String = String.Empty
    Public Property SecondaryMasterModelId As String = String.Empty
    Public Property SecondarySlaveModelId As String = String.Empty

    Public ReadOnly Property ConfigurationPath As String
        Get
            Return _configurationPath
        End Get
    End Property

    ' Compatibility properties make older code/config migrations explicit while
    ' preserving the richer three-state socket model.
    Public Property PrimaryMasterInstalled As Boolean
        Get
            Return PrimaryMasterKind <> IdeDeviceKind.Empty
        End Get
        Set(value As Boolean)
            PrimaryMasterKind = If(value, IdeDeviceKind.AtaHardDisk, IdeDeviceKind.Empty)
        End Set
    End Property

    Public Property PrimarySlaveInstalled As Boolean
        Get
            Return PrimarySlaveKind <> IdeDeviceKind.Empty
        End Get
        Set(value As Boolean)
            PrimarySlaveKind = If(value, IdeDeviceKind.AtapiCdRom, IdeDeviceKind.Empty)
        End Set
    End Property

    Public Property SecondaryMasterInstalled As Boolean
        Get
            Return SecondaryMasterKind <> IdeDeviceKind.Empty
        End Get
        Set(value As Boolean)
            SecondaryMasterKind = If(value, IdeDeviceKind.AtaHardDisk, IdeDeviceKind.Empty)
        End Set
    End Property

    Public Property SecondarySlaveInstalled As Boolean
        Get
            Return SecondarySlaveKind <> IdeDeviceKind.Empty
        End Get
        Set(value As Boolean)
            SecondarySlaveKind = If(value, IdeDeviceKind.AtapiCdRom, IdeDeviceKind.Empty)
        End Set
    End Property

    Public Sub LoadConfiguration()
        Dim rawSchemaInBed As String = MachineConfigurationStore.ReadValue(
            _configurationPath, "IDEHardware", "SchemaVersion", "0")
        Dim schemaInBed As Integer
        If Not Integer.TryParse(rawSchemaInBed, schemaInBed) Then schemaInBed = 0

        PrimaryMasterKind = ReadKind("PrimaryMasterKind", "PrimaryMasterInstalled", IdeDeviceKind.AtaHardDisk)
        PrimarySlaveKind = ReadKind("PrimarySlaveKind", "PrimarySlaveInstalled", IdeDeviceKind.AtapiCdRom)

        If schemaInBed >= 2 Then
            SecondaryMasterKind = ReadKind("SecondaryMasterKind", "SecondaryMasterInstalled", IdeDeviceKind.Empty)
            SecondarySlaveKind = ReadKind("SecondarySlaveKind", "SecondarySlaveInstalled", IdeDeviceKind.Empty)
        Else
            ' Schema 0 is the original single-channel machine. Schema 1 was the
            ' first dual-channel patch and accidentally auto-populated both new
            ' secondary sockets (ATA master + ATAPI slave).  Treat those as slots
            ' that were added to the backplane, not hardware the user installed.
            SecondaryMasterKind = IdeDeviceKind.Empty
            SecondarySlaveKind = IdeDeviceKind.Empty
        End If

        If schemaInBed >= 3 Then
            PrimaryMasterModelId = ReadModelId("PrimaryMasterModel", PrimaryMasterKind)
            PrimarySlaveModelId = ReadModelId("PrimarySlaveModel", PrimarySlaveKind)
            SecondaryMasterModelId = ReadModelId("SecondaryMasterModel", SecondaryMasterKind)
            SecondarySlaveModelId = ReadModelId("SecondarySlaveModel", SecondarySlaveKind)
        Else
            PrimaryMasterModelId = DefaultModelIdForKind(PrimaryMasterKind)
            PrimarySlaveModelId = DefaultModelIdForKind(PrimarySlaveKind)
            SecondaryMasterModelId = DefaultModelIdForKind(SecondaryMasterKind)
            SecondarySlaveModelId = DefaultModelIdForKind(SecondarySlaveKind)
        End If
    End Sub

    Public Sub SaveConfiguration()
        MachineConfigurationStore.WriteValue(_configurationPath, "IDEHardware", "SchemaVersion", CurrentSchemaVersion.ToString())
        MachineConfigurationStore.WriteValue(_configurationPath, "IDEHardware", "PrimaryMasterKind", KindId(PrimaryMasterKind))
        MachineConfigurationStore.WriteValue(_configurationPath, "IDEHardware", "PrimarySlaveKind", KindId(PrimarySlaveKind))
        MachineConfigurationStore.WriteValue(_configurationPath, "IDEHardware", "SecondaryMasterKind", KindId(SecondaryMasterKind))
        MachineConfigurationStore.WriteValue(_configurationPath, "IDEHardware", "SecondarySlaveKind", KindId(SecondarySlaveKind))
        MachineConfigurationStore.WriteValue(_configurationPath, "IDEHardware", "PrimaryMasterModel", PrimaryMasterModelId)
        MachineConfigurationStore.WriteValue(_configurationPath, "IDEHardware", "PrimarySlaveModel", PrimarySlaveModelId)
        MachineConfigurationStore.WriteValue(_configurationPath, "IDEHardware", "SecondaryMasterModel", SecondaryMasterModelId)
        MachineConfigurationStore.WriteValue(_configurationPath, "IDEHardware", "SecondarySlaveModel", SecondarySlaveModelId)
    End Sub

    Public Function CloneConfiguration() As IdeHardwareConfiguration
        Dim cloneInBed As New IdeHardwareConfiguration(Path.GetDirectoryName(_configurationPath)) With {
            .PrimaryMasterKind = PrimaryMasterKind,
            .PrimarySlaveKind = PrimarySlaveKind,
            .SecondaryMasterKind = SecondaryMasterKind,
            .SecondarySlaveKind = SecondarySlaveKind,
            .PrimaryMasterModelId = PrimaryMasterModelId,
            .PrimarySlaveModelId = PrimarySlaveModelId,
            .SecondaryMasterModelId = SecondaryMasterModelId,
            .SecondarySlaveModelId = SecondarySlaveModelId
        }
        Return cloneInBed
    End Function

    Public Function SamePopulation(otherInBed As IdeHardwareConfiguration) As Boolean
        If otherInBed Is Nothing Then Return False
        Return PrimaryMasterKind = otherInBed.PrimaryMasterKind AndAlso
               PrimarySlaveKind = otherInBed.PrimarySlaveKind AndAlso
               SecondaryMasterKind = otherInBed.SecondaryMasterKind AndAlso
               SecondarySlaveKind = otherInBed.SecondarySlaveKind AndAlso
               String.Equals(PrimaryMasterModelId, otherInBed.PrimaryMasterModelId, StringComparison.OrdinalIgnoreCase) AndAlso
               String.Equals(PrimarySlaveModelId, otherInBed.PrimarySlaveModelId, StringComparison.OrdinalIgnoreCase) AndAlso
               String.Equals(SecondaryMasterModelId, otherInBed.SecondaryMasterModelId, StringComparison.OrdinalIgnoreCase) AndAlso
               String.Equals(SecondarySlaveModelId, otherInBed.SecondarySlaveModelId, StringComparison.OrdinalIgnoreCase)
    End Function

    Public Function GetKind(slotInBed As String) As IdeDeviceKind
        Select Case If(slotInBed, String.Empty).Trim().ToLowerInvariant()
            Case "primary-master" : Return PrimaryMasterKind
            Case "primary-slave" : Return PrimarySlaveKind
            Case "secondary-master" : Return SecondaryMasterKind
            Case "secondary-slave" : Return SecondarySlaveKind
            Case Else : Throw New ArgumentOutOfRangeException(NameOf(slotInBed))
        End Select
    End Function

    Public Sub SetKind(slotInBed As String, kindInBed As IdeDeviceKind)
        Dim normalizedSlotInBed As String = If(slotInBed, String.Empty).Trim().ToLowerInvariant()
        Select Case normalizedSlotInBed
            Case "primary-master" : PrimaryMasterKind = kindInBed
            Case "primary-slave" : PrimarySlaveKind = kindInBed
            Case "secondary-master" : SecondaryMasterKind = kindInBed
            Case "secondary-slave" : SecondarySlaveKind = kindInBed
            Case Else : Throw New ArgumentOutOfRangeException(NameOf(slotInBed))
        End Select
        If kindInBed <> IdeDeviceKind.AtaHardDisk Then
            SetModelId(normalizedSlotInBed, String.Empty)
        ElseIf String.IsNullOrWhiteSpace(GetModelId(normalizedSlotInBed)) Then
            SetModelId(normalizedSlotInBed, IdeDriveModelCatalog.Generic64MiBId)
        End If
    End Sub

    Public Function GetModelId(slotInBed As String) As String
        Select Case If(slotInBed, String.Empty).Trim().ToLowerInvariant()
            Case "primary-master" : Return PrimaryMasterModelId
            Case "primary-slave" : Return PrimarySlaveModelId
            Case "secondary-master" : Return SecondaryMasterModelId
            Case "secondary-slave" : Return SecondarySlaveModelId
            Case Else : Throw New ArgumentOutOfRangeException(NameOf(slotInBed))
        End Select
    End Function

    Public Sub SetModelId(slotInBed As String, modelIdInBed As String)
        Dim normalizedInBed As String = If(modelIdInBed, String.Empty).Trim()
        If normalizedInBed.Length > 0 AndAlso IdeDriveModelCatalog.FindById(normalizedInBed) Is Nothing Then
            Throw New ArgumentException("Unknown IDE hard-disk model profile '" & normalizedInBed & "'.", NameOf(modelIdInBed))
        End If
        Select Case If(slotInBed, String.Empty).Trim().ToLowerInvariant()
            Case "primary-master" : PrimaryMasterModelId = normalizedInBed
            Case "primary-slave" : PrimarySlaveModelId = normalizedInBed
            Case "secondary-master" : SecondaryMasterModelId = normalizedInBed
            Case "secondary-slave" : SecondarySlaveModelId = normalizedInBed
            Case Else : Throw New ArgumentOutOfRangeException(NameOf(slotInBed))
        End Select
    End Sub

    Public Function Summary() As String
        Return "PM=" & KindId(PrimaryMasterKind) & ModelSummaryInBed(PrimaryMasterKind, PrimaryMasterModelId) &
               ", PS=" & KindId(PrimarySlaveKind) & ModelSummaryInBed(PrimarySlaveKind, PrimarySlaveModelId) &
               ", SM=" & KindId(SecondaryMasterKind) & ModelSummaryInBed(SecondaryMasterKind, SecondaryMasterModelId) &
               ", SS=" & KindId(SecondarySlaveKind) & ModelSummaryInBed(SecondarySlaveKind, SecondarySlaveModelId)
    End Function

    Private Shared Function ModelSummaryInBed(kindInBed As IdeDeviceKind, modelIdInBed As String) As String
        If kindInBed <> IdeDeviceKind.AtaHardDisk OrElse String.IsNullOrWhiteSpace(modelIdInBed) Then Return String.Empty
        Return "/" & modelIdInBed
    End Function

    Public Shared Function KindId(kindInBed As IdeDeviceKind) As String
        Select Case kindInBed
            Case IdeDeviceKind.AtaHardDisk : Return "ata"
            Case IdeDeviceKind.AtapiCdRom : Return "atapi"
            Case Else : Return "empty"
        End Select
    End Function

    Public Shared Function ParseKind(valueInBed As String, fallbackInBed As IdeDeviceKind) As IdeDeviceKind
        Select Case If(valueInBed, String.Empty).Trim().ToLowerInvariant()
            Case "ata", "hdd", "harddisk", "hard-disk" : Return IdeDeviceKind.AtaHardDisk
            Case "atapi", "cd", "cdrom", "cd-rom" : Return IdeDeviceKind.AtapiCdRom
            Case "empty", "none", "disconnected" : Return IdeDeviceKind.Empty
            Case Else : Return fallbackInBed
        End Select
    End Function

    Private Function ReadModelId(keyInBed As String, kindInBed As IdeDeviceKind) As String
        If kindInBed <> IdeDeviceKind.AtaHardDisk Then Return String.Empty
        Dim fallbackInBed As String = DefaultModelIdForKind(kindInBed)
        Dim rawInBed As String = MachineConfigurationStore.ReadValue(_configurationPath,
                                                                      "IDEHardware",
                                                                      keyInBed,
                                                                      fallbackInBed)
        If IdeDriveModelCatalog.FindById(rawInBed) Is Nothing Then Return fallbackInBed
        Return rawInBed
    End Function

    Private Shared Function DefaultModelIdForKind(kindInBed As IdeDeviceKind) As String
        If kindInBed = IdeDeviceKind.AtaHardDisk Then Return IdeDriveModelCatalog.Generic64MiBId
        Return String.Empty
    End Function

    Private Function ReadKind(kindKeyInBed As String,
                              legacyBoolKeyInBed As String,
                              fallbackInBed As IdeDeviceKind) As IdeDeviceKind
        Dim missingMarkerInBed As String = "__missing__"
        Dim rawKindInBed As String = MachineConfigurationStore.ReadValue(_configurationPath,
                                                                          "IDEHardware",
                                                                          kindKeyInBed,
                                                                          missingMarkerInBed)
        If Not rawKindInBed.Equals(missingMarkerInBed, StringComparison.Ordinal) Then
            Return ParseKind(rawKindInBed, fallbackInBed)
        End If

        ' Migrate the first dual-channel brick's Installed=True/False keys. The
        ' legacy defaults encoded master=ATA and slave=ATAPI in the fallback kind.
        Dim rawLegacyInBed As String = MachineConfigurationStore.ReadValue(_configurationPath,
                                                                            "IDEHardware",
                                                                            legacyBoolKeyInBed,
                                                                            "__missing__")
        Dim legacyInstalledInBed As Boolean
        If Boolean.TryParse(rawLegacyInBed, legacyInstalledInBed) Then
            Return If(legacyInstalledInBed, fallbackInBed, IdeDeviceKind.Empty)
        End If
        Return fallbackInBed
    End Function
End Class
