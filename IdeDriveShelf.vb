Imports System
Imports System.Collections.Generic
Imports System.IO

' Host-side IDE drive library.  This class is deliberately outside the guest
' hardware model: filenames and Windows paths never enter emulated CMOS/ATA.
Public NotInheritable Class IdeDriveShelfEntry
    Public Sub New(id As Integer, label As String, fullPath As String, identityInBed As HardDiskIdentity)
        Me.Id = id
        Me.Label = label
        Me.FullPath = fullPath
        Me.Identity = identityInBed
    End Sub

    Public ReadOnly Property Id As Integer
    Public ReadOnly Property Label As String
    Public ReadOnly Property FullPath As String
    Public ReadOnly Property Identity As HardDiskIdentity

    Public Overrides Function ToString() As String
        Dim modelInBed As String = If(Identity Is Nothing, String.Empty, " — " & Identity.Model)
        Return Id.ToString() & " - " & Label & modelInBed
    End Function
End Class

Public NotInheritable Class IdeDriveShelf
    Private Const ShelfDirectoryName As String = "IDE-Drives"
    Private Const MachineConfigurationName As String = "VirtualComputer.machine.ini"

    Private ReadOnly _baseDirectory As String
    Private ReadOnly _rootPath As String
    Private ReadOnly _configurationPath As String

    ' Media attachment is tracked per electrical IDE socket.  The default boot
    ' disk remains shelf #0 on primary master; all other sockets start detached.
    ' A configured ID is meaningful only while that socket contains an ATA disk.
    Public Property PrimaryMasterId As Integer = 0
    Public Property PrimarySlaveId As Integer = -1
    Public Property SecondaryMasterId As Integer = -1
    Public Property SecondarySlaveId As Integer = -1

    Public Sub New(baseDirectory As String)
        If String.IsNullOrWhiteSpace(baseDirectory) Then Throw New ArgumentNullException(NameOf(baseDirectory))
        _baseDirectory = Path.GetFullPath(baseDirectory)
        _rootPath = Path.Combine(_baseDirectory, ShelfDirectoryName)
        _configurationPath = Path.Combine(_baseDirectory, MachineConfigurationName)
    End Sub

    Public ReadOnly Property RootPath As String
        Get
            Return _rootPath
        End Get
    End Property

    Public ReadOnly Property ConfigurationPath As String
        Get
            Return _configurationPath
        End Get
    End Property

    Public Sub EnsureShelfExists()
        Directory.CreateDirectory(_rootPath)
    End Sub

    Public Sub LoadConfiguration()
        EnsureShelfExists()
        PrimaryMasterId = ReadConfiguredDriveId("PrimaryMaster", 0)
        PrimarySlaveId = ReadConfiguredDriveId("PrimarySlave", -1)
        SecondaryMasterId = ReadConfiguredDriveId("SecondaryMaster", -1)
        SecondarySlaveId = ReadConfiguredDriveId("SecondarySlave", -1)
    End Sub

    Public Sub SaveConfiguration()
        EnsureShelfExists()
        WriteConfiguredDriveId("PrimaryMaster", PrimaryMasterId)
        WriteConfiguredDriveId("PrimarySlave", PrimarySlaveId)
        WriteConfiguredDriveId("SecondaryMaster", SecondaryMasterId)
        WriteConfiguredDriveId("SecondarySlave", SecondarySlaveId)
    End Sub

    Public Function GetConfiguredDriveId(channelInBed As Integer,
                                         Optional slaveInBed As Boolean = False) As Integer
        Select Case channelInBed
            Case 0
                Return If(slaveInBed, PrimarySlaveId, PrimaryMasterId)
            Case 1
                Return If(slaveInBed, SecondarySlaveId, SecondaryMasterId)
            Case Else
                Throw New ArgumentOutOfRangeException(NameOf(channelInBed))
        End Select
    End Function

    Public Sub SetConfiguredDriveId(channelInBed As Integer,
                                    driveIdInBed As Integer,
                                    Optional slaveInBed As Boolean = False)
        Select Case channelInBed
            Case 0
                If slaveInBed Then PrimarySlaveId = driveIdInBed Else PrimaryMasterId = driveIdInBed
            Case 1
                If slaveInBed Then SecondarySlaveId = driveIdInBed Else SecondaryMasterId = driveIdInBed
            Case Else
                Throw New ArgumentOutOfRangeException(NameOf(channelInBed))
        End Select
    End Sub

    Private Function ReadConfiguredDriveId(keyInBed As String, fallbackInBed As Integer) As Integer
        Dim fallbackTextInBed As String = If(fallbackInBed < 0, "None", fallbackInBed.ToString())
        Dim valueInBed As String = MachineConfigurationStore.ReadValue(_configurationPath, "IDE", keyInBed, fallbackTextInBed)
        If valueInBed.Equals("None", StringComparison.OrdinalIgnoreCase) OrElse
           valueInBed.Equals("Disconnected", StringComparison.OrdinalIgnoreCase) Then Return -1
        Dim parsedInBed As Integer
        If Integer.TryParse(valueInBed, parsedInBed) AndAlso parsedInBed >= 0 Then Return parsedInBed
        Return fallbackInBed
    End Function

    Private Sub WriteConfiguredDriveId(keyInBed As String, driveIdInBed As Integer)
        Dim valueInBed As String = If(driveIdInBed < 0, "None", driveIdInBed.ToString())
        MachineConfigurationStore.WriteValue(_configurationPath, "IDE", keyInBed, valueInBed)
    End Sub

    Public Function GetEntries() As List(Of IdeDriveShelfEntry)
        EnsureShelfExists()
        Dim result As New List(Of IdeDriveShelfEntry)()
        Dim ids As New HashSet(Of Integer)()

        For Each filePath As String In Directory.EnumerateFiles(_rootPath, "*", SearchOption.TopDirectoryOnly)
            Dim extension As String = Path.GetExtension(filePath)
            If Not extension.Equals(".hdd", StringComparison.OrdinalIgnoreCase) AndAlso
               Not extension.Equals(".img", StringComparison.OrdinalIgnoreCase) Then
                Continue For
            End If

            Dim id As Integer
            Dim label As String = Nothing
            If Not TryParseShelfFilename(Path.GetFileNameWithoutExtension(filePath), id, label) Then Continue For

            If Not ids.Add(id) Then
                Throw New InvalidDataException("Duplicate IDE shelf drive ID #" & id.ToString() & ". Each numeric prefix must be unique.")
            End If

            Dim fileLengthInBed As Long = New FileInfo(filePath).Length
            If fileLengthInBed <= 0 OrElse fileLengthInBed Mod 512L <> 0 Then
                Throw New InvalidDataException("IDE shelf drive '" & filePath & "' is not a whole number of 512-byte sectors.")
            End If
            Dim identityInBed As HardDiskIdentity = HardDiskIdentity.LoadOrCreate(filePath, fileLengthInBed \ 512L)
            result.Add(New IdeDriveShelfEntry(id, label, filePath, identityInBed))
        Next

        result.Sort(Function(left, right) left.Id.CompareTo(right.Id))
        Return result
    End Function

    Public Function FindById(id As Integer) As IdeDriveShelfEntry
        If id < 0 Then Return Nothing
        For Each entry As IdeDriveShelfEntry In GetEntries()
            If entry.Id = id Then Return entry
        Next
        Return Nothing
    End Function

    Public Function NextAvailableId() As Integer
        Dim used As New HashSet(Of Integer)()
        For Each entry As IdeDriveShelfEntry In GetEntries()
            used.Add(entry.Id)
        Next
        Dim candidate As Integer = 0
        While used.Contains(candidate)
            candidate += 1
        End While
        Return candidate
    End Function

    Public Shared Function SanitizeLabel(label As String) As String
        If String.IsNullOrWhiteSpace(label) Then Return "Untitled IDE Drive"
        Dim cleaned As String = label.Trim()
        For Each bad As Char In Path.GetInvalidFileNameChars()
            cleaned = cleaned.Replace(bad, "_"c)
        Next
        cleaned = cleaned.Trim(" "c, "."c)
        If cleaned.Length = 0 Then cleaned = "Untitled IDE Drive"
        Return cleaned
    End Function

    Private Shared Function TryParseShelfFilename(stem As String, ByRef id As Integer, ByRef label As String) As Boolean
        id = -1
        label = Nothing
        If String.IsNullOrWhiteSpace(stem) Then Return False

        Dim index As Integer = 0
        While index < stem.Length AndAlso Char.IsDigit(stem(index))
            index += 1
        End While
        If index = 0 Then Return False

        Dim parsed As Integer
        If Not Integer.TryParse(stem.Substring(0, index), parsed) OrElse parsed < 0 Then Return False

        Dim labelStart As Integer = index
        While labelStart < stem.Length
            Dim ch As Char = stem(labelStart)
            If ch = " "c OrElse ch = "-"c OrElse ch = "_"c OrElse ch = "."c Then
                labelStart += 1
            Else
                Exit While
            End If
        End While

        Dim parsedLabel As String = If(labelStart < stem.Length, stem.Substring(labelStart).Trim(), String.Empty)
        If parsedLabel.Length = 0 Then parsedLabel = "Drive " & parsed.ToString()

        id = parsed
        label = parsedLabel
        Return True
    End Function
End Class