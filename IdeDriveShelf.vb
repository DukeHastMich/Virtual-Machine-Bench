Imports System
Imports System.Collections.Generic
Imports System.IO

' Host-side IDE drive library.  This class is deliberately outside the guest
' hardware model: filenames and Windows paths never enter emulated CMOS/ATA.
Public NotInheritable Class IdeDriveShelfEntry
    Public Sub New(id As Integer, label As String, fullPath As String)
        Me.Id = id
        Me.Label = label
        Me.FullPath = fullPath
    End Sub

    Public ReadOnly Property Id As Integer
    Public ReadOnly Property Label As String
    Public ReadOnly Property FullPath As String

    Public Overrides Function ToString() As String
        Return Id.ToString() & " - " & Label
    End Function
End Class

Public NotInheritable Class IdeDriveShelf
    Private Const ShelfDirectoryName As String = "IDE-Drives"
    Private Const MachineConfigurationName As String = "VirtualComputer.machine.ini"

    Private ReadOnly _baseDirectory As String
    Private ReadOnly _rootPath As String
    Private ReadOnly _configurationPath As String

    ' Chassis default: shelf drive #0 is plugged into primary master unless the
    ' machine configuration explicitly says otherwise.  A missing #0 means the
    ' ATA socket is physically empty; it is never synthesized by the host.
    Public Property PrimaryMasterId As Integer = 0

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
        PrimaryMasterId = 0
        Dim value As String = MachineConfigurationStore.ReadValue(_configurationPath, "IDE", "PrimaryMaster", "0")
        If value.Equals("None", StringComparison.OrdinalIgnoreCase) OrElse
           value.Equals("Disconnected", StringComparison.OrdinalIgnoreCase) Then
            PrimaryMasterId = -1
        Else
            Dim parsed As Integer
            If Integer.TryParse(value, parsed) AndAlso parsed >= 0 Then PrimaryMasterId = parsed
        End If
    End Sub

    Public Sub SaveConfiguration()
        EnsureShelfExists()
        Dim value As String = If(PrimaryMasterId < 0, "None", PrimaryMasterId.ToString())
        MachineConfigurationStore.WriteValue(_configurationPath, "IDE", "PrimaryMaster", value)
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

            result.Add(New IdeDriveShelfEntry(id, label, filePath))
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