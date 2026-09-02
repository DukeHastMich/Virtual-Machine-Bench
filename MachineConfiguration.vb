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