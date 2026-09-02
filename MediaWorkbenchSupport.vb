Imports System
Imports System.Collections.Generic
Imports System.ComponentModel
Imports System.Diagnostics
Imports System.Globalization
Imports System.IO
Imports System.Linq
Imports System.Runtime.InteropServices
Imports System.Security.Cryptography
Imports System.Text
Imports System.Threading
Imports System.Threading.Tasks
Imports Microsoft.Win32.SafeHandles

Public NotInheritable Class HostActionLease
    Implements IDisposable

    Private _releaseInBed As Action

    Public Sub New(releaseInBed As Action)
        _releaseInBed = releaseInBed
    End Sub

    Public Sub Dispose() Implements IDisposable.Dispose
        Dim releaseInBed As Action = Interlocked.Exchange(_releaseInBed, Nothing)
        If releaseInBed IsNot Nothing Then releaseInBed()
    End Sub
End Class

' ============================================================================
' CROMWELL TECHNOLOGIES HOST MEDIA WORKBENCH SUBSTRATE
' Host-only.  Nothing in this file creates guest-visible hardware.
' ============================================================================

Public NotInheritable Class HostMediaConfiguration
    Private Const CompanyDirectoryInBed As String = "Cromwell Technologies"
    Private Const ProductDirectoryInBed As String = "Virtual Computer"
    Private Const ConfigurationFileNameInBed As String = "HostConfiguration.ini"

    Private Sub New()
    End Sub

    Public Shared ReadOnly Property ConfigurationPath As String
        Get
            Dim localInBed As String = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData)
            If String.IsNullOrWhiteSpace(localInBed) Then localInBed = AppContext.BaseDirectory
            Dim directoryInBed As String = Path.Combine(localInBed, CompanyDirectoryInBed, ProductDirectoryInBed)
            Directory.CreateDirectory(directoryInBed)
            Return Path.Combine(directoryInBed, ConfigurationFileNameInBed)
        End Get
    End Property

    Public Shared Function DefaultBackupRoot() As String
        Dim documentsInBed As String = Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments)
        If String.IsNullOrWhiteSpace(documentsInBed) Then
            documentsInBed = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData)
        End If
        If String.IsNullOrWhiteSpace(documentsInBed) Then documentsInBed = AppContext.BaseDirectory
        Return Path.Combine(documentsInBed, "Virtual Computer", "Backup")
    End Function

    Public Shared Function GetBackupRoot() As String
        Dim rawInBed As String = MachineConfigurationStore.ReadValue(
            ConfigurationPath,
            "Backup",
            "Directory",
            DefaultBackupRoot())

        If String.IsNullOrWhiteSpace(rawInBed) Then rawInBed = DefaultBackupRoot()
        Try
            rawInBed = Environment.ExpandEnvironmentVariables(rawInBed.Trim())
            Return Path.GetFullPath(rawInBed)
        Catch
            Return Path.GetFullPath(DefaultBackupRoot())
        End Try
    End Function

    Public Shared Sub SetBackupRoot(pathInBed As String)
        If String.IsNullOrWhiteSpace(pathInBed) Then Throw New ArgumentException("A backup directory is required.", NameOf(pathInBed))
        Dim fullInBed As String = Path.GetFullPath(Environment.ExpandEnvironmentVariables(pathInBed.Trim()))
        Directory.CreateDirectory(fullInBed)
        MachineConfigurationStore.WriteValue(ConfigurationPath, "Backup", "Directory", fullInBed)
    End Sub

    Public Shared Sub EnsureBackupRoot()
        Directory.CreateDirectory(GetBackupRoot())
    End Sub
End Class

Public NotInheritable Class MediaBackupResult
    Public Sub New(createdInBed As Boolean,
                   sourcePathInBed As String,
                   destinationPathInBed As String,
                   generationInBed As Long,
                   hashInBed As String,
                   messageInBed As String)
        Created = createdInBed
        SourcePath = sourcePathInBed
        DestinationPath = destinationPathInBed
        Generation = generationInBed
        Sha256 = hashInBed
        Message = messageInBed
    End Sub

    Public ReadOnly Property Created As Boolean
    Public ReadOnly Property SourcePath As String
    Public ReadOnly Property DestinationPath As String
    Public ReadOnly Property Generation As Long
    Public ReadOnly Property Sha256 As String
    Public ReadOnly Property Message As String
End Class

Public NotInheritable Class MediaBackupArchive
    Private Sub New()
    End Sub

    Public Shared Function ClassifyMedia(sourcePathInBed As String) As String
        Dim extensionInBed As String = Path.GetExtension(sourcePathInBed).ToLowerInvariant()
        Select Case extensionInBed
            Case ".hdd", ".vhd", ".vhdx"
                Return "IDE-Drives"
            Case ".iso"
                Return "Optical"
            Case ".ima"
                Return "Floppy-Box"
            Case ".img"
                Dim lengthInBed As Long = New FileInfo(sourcePathInBed).Length
                Select Case lengthInBed
                    Case 163840L, 184320L, 327680L, 368640L, 737280L, 1228800L, 1474560L, 2949120L
                        Return "Floppy-Box"
                    Case Else
                        Return "USB-Images"
                End Select
            Case Else
                Return "Media"
        End Select
    End Function

    Public Shared Function BackupIfChanged(sourcePathInBed As String,
                                           Optional categoryInBed As String = Nothing) As MediaBackupResult
        If String.IsNullOrWhiteSpace(sourcePathInBed) Then Throw New ArgumentException("A media-image path is required.", NameOf(sourcePathInBed))
        Dim sourceFullInBed As String = Path.GetFullPath(sourcePathInBed)
        If Not File.Exists(sourceFullInBed) Then Throw New FileNotFoundException("The media image does not exist.", sourceFullInBed)

        Dim categorySafeInBed As String = If(String.IsNullOrWhiteSpace(categoryInBed), ClassifyMedia(sourceFullInBed), SanitizePathComponentInBed(categoryInBed))
        Dim backupRootInBed As String = HostMediaConfiguration.GetBackupRoot()
        Dim siloInBed As String = Path.Combine(backupRootInBed, categorySafeInBed, Path.GetFileName(sourceFullInBed))
        Directory.CreateDirectory(siloInBed)

        Dim extensionInBed As String = Path.GetExtension(sourceFullInBed)
        If String.IsNullOrEmpty(extensionInBed) Then extensionInBed = ".img"

        Dim latestGenerationInBed As Long = -1L
        Dim latestPathInBed As String = Nothing
        For Each candidateInBed As String In Directory.EnumerateFiles(siloInBed, "*" & extensionInBed, SearchOption.TopDirectoryOnly)
            Dim generationInBed As Long
            If TryParseGenerationInBed(Path.GetFileNameWithoutExtension(candidateInBed), generationInBed) AndAlso generationInBed > latestGenerationInBed Then
                latestGenerationInBed = generationInBed
                latestPathInBed = candidateInBed
            End If
        Next

        Dim sourceHashInBed As String = ComputeSha256InBed(sourceFullInBed)
        If latestPathInBed IsNot Nothing Then
            Dim latestHashInBed As String = ComputeSha256InBed(latestPathInBed)
            If sourceHashInBed.Equals(latestHashInBed, StringComparison.OrdinalIgnoreCase) Then
                Return New MediaBackupResult(False, sourceFullInBed, latestPathInBed, latestGenerationInBed, sourceHashInBed, "Media is unchanged; no new backup generation was created.")
            End If
        End If

        If latestGenerationInBed = Long.MaxValue Then Throw New OverflowException("Backup generation counter exhausted.")
        Dim nextGenerationInBed As Long = latestGenerationInBed + 1L
        Dim widthInBed As Integer = Math.Max(4, nextGenerationInBed.ToString("X", CultureInfo.InvariantCulture).Length)
        Dim generationTextInBed As String = nextGenerationInBed.ToString("X" & widthInBed.ToString(CultureInfo.InvariantCulture), CultureInfo.InvariantCulture)
        Dim destinationInBed As String = Path.Combine(siloInBed, generationTextInBed & extensionInBed)
        If File.Exists(destinationInBed) Then Throw New IOException("Backup generation already exists and will not be overwritten: " & destinationInBed)

        Dim temporaryInBed As String = Path.Combine(siloInBed, "." & generationTextInBed & "." & Guid.NewGuid().ToString("N") & ".tmp")
        Try
            CopyFileDurablyInBed(sourceFullInBed, temporaryInBed)
            Dim temporaryHashInBed As String = ComputeSha256InBed(temporaryInBed)
            If Not sourceHashInBed.Equals(temporaryHashInBed, StringComparison.OrdinalIgnoreCase) Then
                Throw New IOException("Backup verification failed: source and staged copy SHA-256 values differ.")
            End If
            If File.Exists(destinationInBed) Then Throw New IOException("Backup generation appeared while the snapshot was being written; refusing to overwrite it.")
            File.Move(temporaryInBed, destinationInBed)

            Dim metadataPathInBed As String = Path.Combine(siloInBed, generationTextInBed & ".meta.txt")
            Using metadataInBed As New FileStream(metadataPathInBed, FileMode.CreateNew, FileAccess.Write, FileShare.Read)
                Using writerInBed As New StreamWriter(metadataInBed, New UTF8Encoding(False), 4096, True)
                    writerInBed.WriteLine("Generation=" & generationTextInBed)
                    writerInBed.WriteLine("HostTime=" & DateTimeOffset.Now.ToString("O", CultureInfo.InvariantCulture))
                    writerInBed.WriteLine("Source=" & sourceFullInBed)
                    writerInBed.WriteLine("Length=" & New FileInfo(sourceFullInBed).Length.ToString(CultureInfo.InvariantCulture))
                    writerInBed.WriteLine("SHA256=" & sourceHashInBed)
                End Using
                metadataInBed.Flush(True)
            End Using
        Finally
            If File.Exists(temporaryInBed) Then
                Try
                    File.Delete(temporaryInBed)
                Catch
                End Try
            End If
        End Try

        Return New MediaBackupResult(True, sourceFullInBed, destinationInBed, nextGenerationInBed, sourceHashInBed, "Created backup generation " & generationTextInBed & ".")
    End Function

    Public Shared Function GetSiloRootForSource(sourcePathInBed As String,
                                                Optional categoryInBed As String = Nothing) As String
        Dim sourceFullInBed As String = Path.GetFullPath(sourcePathInBed)
        Dim categorySafeInBed As String = If(String.IsNullOrWhiteSpace(categoryInBed), ClassifyMedia(sourceFullInBed), SanitizePathComponentInBed(categoryInBed))
        Return Path.Combine(HostMediaConfiguration.GetBackupRoot(), categorySafeInBed, Path.GetFileName(sourceFullInBed))
    End Function

    Private Shared Function SanitizePathComponentInBed(valueInBed As String) As String
        Dim resultInBed As String = If(valueInBed, String.Empty).Trim()
        If resultInBed.Length = 0 Then resultInBed = "Media"
        For Each badInBed As Char In Path.GetInvalidFileNameChars()
            resultInBed = resultInBed.Replace(badInBed, "_"c)
        Next
        Return resultInBed
    End Function

    Private Shared Function TryParseGenerationInBed(stemInBed As String, ByRef generationInBed As Long) As Boolean
        Return Long.TryParse(stemInBed, NumberStyles.AllowHexSpecifier, CultureInfo.InvariantCulture, generationInBed)
    End Function

    Private Shared Sub CopyFileDurablyInBed(sourceInBed As String, destinationInBed As String)
        Using inputInBed As New FileStream(sourceInBed, FileMode.Open, FileAccess.Read, FileShare.ReadWrite, 1024 * 1024, FileOptions.SequentialScan)
            Using outputInBed As New FileStream(destinationInBed, FileMode.CreateNew, FileAccess.Write, FileShare.Read, 1024 * 1024, FileOptions.SequentialScan)
                inputInBed.CopyTo(outputInBed, 1024 * 1024)
                outputInBed.Flush(True)
            End Using
        End Using
    End Sub

    Private Shared Function ComputeSha256InBed(pathInBed As String) As String
        Using algorithmInBed As SHA256 = SHA256.Create()
            Using streamInBed As New FileStream(pathInBed, FileMode.Open, FileAccess.Read, FileShare.ReadWrite, 1024 * 1024, FileOptions.SequentialScan)
                Return Convert.ToHexString(algorithmInBed.ComputeHash(streamInBed))
            End Using
        End Using
    End Function
End Class

Public NotInheritable Class DiskPartitionInfo
    Public Property Index As Integer
    Public Property Bootable As Boolean
    Public Property PartitionType As Byte
    Public Property StartLba As UInteger
    Public Property SectorCount As UInteger
End Class

Public NotInheritable Class DiskImageInspection
    Public Property Path As String
    Public Property Length As Long
    Public Property SectorAligned As Boolean
    Public Property HasBootSignature As Boolean
    Public Property LooksBootable As Boolean
    Public Property Kind As String
    Public Property OemName As String
    Public ReadOnly Property Partitions As New List(Of DiskPartitionInfo)()

    Public Function ToReport() As String
        Dim sbInBed As New StringBuilder()
        sbInBed.AppendLine("Path: " & Path)
        sbInBed.AppendLine("Size: " & Length.ToString("N0", CultureInfo.CurrentCulture) & " bytes")
        sbInBed.AppendLine("512-byte aligned: " & If(SectorAligned, "yes", "no"))
        sbInBed.AppendLine("Kind: " & If(String.IsNullOrWhiteSpace(Kind), "raw / unknown", Kind))
        sbInBed.AppendLine("55AA boot signature: " & If(HasBootSignature, "yes", "no"))
        sbInBed.AppendLine("Bootable-looking: " & If(LooksBootable, "yes", "no"))
        If Not String.IsNullOrWhiteSpace(OemName) Then sbInBed.AppendLine("OEM/BPB name: " & OemName)
        If Partitions.Count = 0 Then
            sbInBed.AppendLine("MBR partitions: none detected")
        Else
            sbInBed.AppendLine("MBR partitions:")
            For Each partitionInBed As DiskPartitionInfo In Partitions
                sbInBed.Append("  #").Append(partitionInBed.Index.ToString()).Append("  ")
                sbInBed.Append(If(partitionInBed.Bootable, "active  ", "        "))
                sbInBed.Append("type ").Append(partitionInBed.PartitionType.ToString("X2"))
                sbInBed.Append("  start ").Append(partitionInBed.StartLba.ToString("N0", CultureInfo.CurrentCulture))
                sbInBed.Append("  sectors ").Append(partitionInBed.SectorCount.ToString("N0", CultureInfo.CurrentCulture))
                sbInBed.AppendLine()
            Next
        End If
        Return sbInBed.ToString()
    End Function
End Class

Public NotInheritable Class DiskImageInspector
    Private Sub New()
    End Sub

    Public Shared Function Inspect(pathInBed As String) As DiskImageInspection
        Dim fullInBed As String = System.IO.Path.GetFullPath(pathInBed)
        If Not File.Exists(fullInBed) Then Throw New FileNotFoundException("Image not found.", fullInBed)

        Dim resultInBed As New DiskImageInspection() With {
            .Path = fullInBed,
            .Length = New FileInfo(fullInBed).Length
        }
        resultInBed.SectorAligned = (resultInBed.Length Mod 512L) = 0L

        If System.IO.Path.GetExtension(fullInBed).Equals(".iso", StringComparison.OrdinalIgnoreCase) AndAlso resultInBed.Length >= 17L * 2048L Then
            Using isoInBed As New FileStream(fullInBed, FileMode.Open, FileAccess.Read, FileShare.ReadWrite)
                isoInBed.Position = 16L * 2048L + 1L
                Dim signatureInBed(4) As Byte
                If isoInBed.Read(signatureInBed, 0, signatureInBed.Length) = signatureInBed.Length AndAlso
                   Encoding.ASCII.GetString(signatureInBed) = "CD001" Then
                    resultInBed.Kind = "ISO-9660 optical image"
                    Return resultInBed
                End If
            End Using
        End If

        If resultInBed.Length < 512L Then
            resultInBed.Kind = "short raw file"
            Return resultInBed
        End If

        Dim sectorInBed(511) As Byte
        Using streamInBed As New FileStream(fullInBed, FileMode.Open, FileAccess.Read, FileShare.ReadWrite)
            ReadFullyInBed(streamInBed, sectorInBed)
        End Using

        resultInBed.HasBootSignature = sectorInBed(510) = &H55 AndAlso sectorInBed(511) = &HAA
        Dim anyBootCodeInBed As Boolean = False
        For indexInBed As Integer = 0 To 439
            If sectorInBed(indexInBed) <> 0 Then
                anyBootCodeInBed = True
                Exit For
            End If
        Next

        For partitionIndexInBed As Integer = 0 To 3
            Dim offsetInBed As Integer = 446 + partitionIndexInBed * 16
            Dim typeInBed As Byte = sectorInBed(offsetInBed + 4)
            Dim startInBed As UInteger = ReadUInt32InBed(sectorInBed, offsetInBed + 8)
            Dim countInBed As UInteger = ReadUInt32InBed(sectorInBed, offsetInBed + 12)
            If typeInBed <> 0 AndAlso countInBed <> 0UI Then
                resultInBed.Partitions.Add(New DiskPartitionInfo() With {
                    .Index = partitionIndexInBed + 1,
                    .Bootable = sectorInBed(offsetInBed) = &H80,
                    .PartitionType = typeInBed,
                    .StartLba = startInBed,
                    .SectorCount = countInBed
                })
            End If
        Next

        Dim bpbLooksPlausibleInBed As Boolean = sectorInBed(11) = 0 AndAlso sectorInBed(12) = 2
        If bpbLooksPlausibleInBed Then
            resultInBed.OemName = Encoding.ASCII.GetString(sectorInBed, 3, 8).Trim()
        End If

        If resultInBed.Partitions.Count > 0 Then
            resultInBed.Kind = "MBR-partitioned disk image"
        ElseIf bpbLooksPlausibleInBed Then
            resultInBed.Kind = "filesystem / floppy-style raw image"
        Else
            resultInBed.Kind = "raw disk image"
        End If

        resultInBed.LooksBootable = resultInBed.HasBootSignature AndAlso
            (anyBootCodeInBed OrElse resultInBed.Partitions.Any(Function(partitionInBed) partitionInBed.Bootable))
        Return resultInBed
    End Function

    Public Shared Function BuildReport(pathInBed As String) As String
        Return Inspect(pathInBed).ToReport()
    End Function

    Private Shared Sub ReadFullyInBed(streamInBed As Stream, bufferInBed As Byte())
        Dim offsetInBed As Integer = 0
        While offsetInBed < bufferInBed.Length
            Dim countInBed As Integer = streamInBed.Read(bufferInBed, offsetInBed, bufferInBed.Length - offsetInBed)
            If countInBed = 0 Then Throw New EndOfStreamException()
            offsetInBed += countInBed
        End While
    End Sub

    Private Shared Function ReadUInt32InBed(dataInBed As Byte(), offsetInBed As Integer) As UInteger
        Return CUInt(dataInBed(offsetInBed)) Or
               (CUInt(dataInBed(offsetInBed + 1)) << 8) Or
               (CUInt(dataInBed(offsetInBed + 2)) << 16) Or
               (CUInt(dataInBed(offsetInBed + 3)) << 24)
    End Function
End Class

Public NotInheritable Class HostStorageDeviceInfo
    Public Property RootPath As String
    Public Property VolumePath As String
    Public Property DisplayName As String
    Public Property DriveType As DriveType
    Public Property VolumeLabel As String
    Public Property FileSystem As String
    Public Property TotalBytes As Long
    Public Property FreeBytes As Long
    Public Property PhysicalDriveNumber As Integer = -1
    Public Property PhysicalPath As String
    Public Property IsSystemPhysicalDrive As Boolean

    Public ReadOnly Property CanRawImage As Boolean
        Get
            Return PhysicalDriveNumber >= 0 AndAlso Not String.IsNullOrWhiteSpace(PhysicalPath)
        End Get
    End Property

    Public ReadOnly Property CanRawWrite As Boolean
        Get
            Return CanRawImage AndAlso Not IsSystemPhysicalDrive AndAlso
                   (DriveType = System.IO.DriveType.Removable OrElse DriveType = System.IO.DriveType.Fixed)
        End Get
    End Property
End Class

Public NotInheritable Class HostStorageDeviceCatalog
    Private Const IOCTL_STORAGE_GET_DEVICE_NUMBER As UInteger = &H2D1080UI
    Private Const GENERIC_READ As UInteger = &H80000000UI
    Private Const FILE_SHARE_READ As UInteger = &H1UI
    Private Const FILE_SHARE_WRITE As UInteger = &H2UI
    Private Const FILE_SHARE_DELETE As UInteger = &H4UI
    Private Const OPEN_EXISTING As UInteger = 3UI

    <StructLayout(LayoutKind.Sequential)>
    Private Structure STORAGE_DEVICE_NUMBER
        Public DeviceType As UInteger
        Public DeviceNumber As UInteger
        Public PartitionNumber As UInteger
    End Structure

    <DllImport("kernel32.dll", EntryPoint:="CreateFileW", CharSet:=CharSet.Unicode, SetLastError:=True)>
    Private Shared Function CreateFileInBed(lpFileName As String,
                                            dwDesiredAccess As UInteger,
                                            dwShareMode As UInteger,
                                            lpSecurityAttributes As IntPtr,
                                            dwCreationDisposition As UInteger,
                                            dwFlagsAndAttributes As UInteger,
                                            hTemplateFile As IntPtr) As SafeFileHandle
    End Function

    <DllImport("kernel32.dll", EntryPoint:="DeviceIoControl", SetLastError:=True)>
    Private Shared Function DeviceIoControlStorageNumberInBed(hDevice As SafeFileHandle,
                                                              dwIoControlCode As UInteger,
                                                              lpInBuffer As IntPtr,
                                                              nInBufferSize As UInteger,
                                                              ByRef lpOutBuffer As STORAGE_DEVICE_NUMBER,
                                                              nOutBufferSize As UInteger,
                                                              ByRef lpBytesReturned As UInteger,
                                                              lpOverlapped As IntPtr) As Boolean
    End Function

    Private Sub New()
    End Sub

    Public Shared Function Enumerate() As List(Of HostStorageDeviceInfo)
        Dim systemRootInBed As String = Path.GetPathRoot(Environment.SystemDirectory)
        Dim systemPhysicalInBed As Integer = TryGetPhysicalDriveNumberInBed(systemRootInBed)
        Dim resultInBed As New List(Of HostStorageDeviceInfo)()

        For Each driveInBed As DriveInfo In DriveInfo.GetDrives()
            Dim infoInBed As New HostStorageDeviceInfo() With {
                .RootPath = driveInBed.Name,
                .VolumePath = ToVolumePathInBed(driveInBed.Name),
                .DisplayName = driveInBed.Name,
                .DriveType = driveInBed.DriveType
            }
            Try
                If driveInBed.IsReady Then
                    infoInBed.VolumeLabel = driveInBed.VolumeLabel
                    infoInBed.FileSystem = driveInBed.DriveFormat
                    infoInBed.TotalBytes = driveInBed.TotalSize
                    infoInBed.FreeBytes = driveInBed.AvailableFreeSpace
                End If
            Catch
            End Try

            infoInBed.PhysicalDriveNumber = TryGetPhysicalDriveNumberInBed(driveInBed.Name)
            If infoInBed.PhysicalDriveNumber >= 0 Then
                infoInBed.PhysicalPath = "\\.\PhysicalDrive" & infoInBed.PhysicalDriveNumber.ToString(CultureInfo.InvariantCulture)
                Dim isSystemVolumeInBed As Boolean = Not String.IsNullOrWhiteSpace(systemRootInBed) AndAlso
                    driveInBed.Name.Equals(systemRootInBed, StringComparison.OrdinalIgnoreCase)
                infoInBed.IsSystemPhysicalDrive = isSystemVolumeInBed OrElse
                    (infoInBed.PhysicalDriveNumber = systemPhysicalInBed AndAlso systemPhysicalInBed >= 0) OrElse
                    (systemPhysicalInBed < 0 AndAlso driveInBed.DriveType = System.IO.DriveType.Fixed)
            End If
            resultInBed.Add(infoInBed)
        Next

        Return resultInBed.OrderBy(Function(infoInBed) infoInBed.RootPath, StringComparer.CurrentCultureIgnoreCase).ToList()
    End Function

    Friend Shared Function GetVolumePathsForPhysicalDevice(physicalDriveNumberInBed As Integer) As List(Of String)
        Dim resultInBed As New List(Of String)()
        For Each driveInBed As DriveInfo In DriveInfo.GetDrives()
            If TryGetPhysicalDriveNumberInBed(driveInBed.Name) = physicalDriveNumberInBed Then
                resultInBed.Add(ToVolumePathInBed(driveInBed.Name))
            End If
        Next
        Return resultInBed
    End Function

    Private Shared Function TryGetPhysicalDriveNumberInBed(rootPathInBed As String) As Integer
        If String.IsNullOrWhiteSpace(rootPathInBed) Then Return -1
        Dim volumePathInBed As String = ToVolumePathInBed(rootPathInBed)
        If String.IsNullOrWhiteSpace(volumePathInBed) Then Return -1

        Using handleInBed As SafeFileHandle = CreateFileInBed(
            volumePathInBed,
            0UI,
            FILE_SHARE_READ Or FILE_SHARE_WRITE Or FILE_SHARE_DELETE,
            IntPtr.Zero,
            OPEN_EXISTING,
            0UI,
            IntPtr.Zero)

            If handleInBed Is Nothing OrElse handleInBed.IsInvalid Then Return -1
            Dim numberInBed As New STORAGE_DEVICE_NUMBER()
            Dim returnedInBed As UInteger
            If Not DeviceIoControlStorageNumberInBed(
                handleInBed,
                IOCTL_STORAGE_GET_DEVICE_NUMBER,
                IntPtr.Zero,
                0UI,
                numberInBed,
                CUInt(Marshal.SizeOf(GetType(STORAGE_DEVICE_NUMBER))),
                returnedInBed,
                IntPtr.Zero) Then Return -1
            Return CInt(numberInBed.DeviceNumber)
        End Using
    End Function

    Private Shared Function ToVolumePathInBed(rootPathInBed As String) As String
        If String.IsNullOrWhiteSpace(rootPathInBed) Then Return Nothing
        Dim rootInBed As String = Path.GetPathRoot(rootPathInBed)
        If String.IsNullOrWhiteSpace(rootInBed) OrElse rootInBed.Length < 2 OrElse rootInBed(1) <> ":"c Then Return Nothing
        Return "\\.\" & rootInBed.Substring(0, 2)
    End Function
End Class

Public NotInheritable Class MediaImagingEngine
    Private Const GENERIC_READ As UInteger = &H80000000UI
    Private Const GENERIC_WRITE As UInteger = &H40000000UI
    Private Const FILE_SHARE_READ As UInteger = &H1UI
    Private Const FILE_SHARE_WRITE As UInteger = &H2UI
    Private Const OPEN_EXISTING As UInteger = 3UI
    Private Const IOCTL_DISK_GET_LENGTH_INFO As UInteger = &H7405CUI
    Private Const FSCTL_LOCK_VOLUME As UInteger = &H90018UI
    Private Const FSCTL_UNLOCK_VOLUME As UInteger = &H9001CUI
    Private Const FSCTL_DISMOUNT_VOLUME As UInteger = &H90020UI

    <DllImport("kernel32.dll", EntryPoint:="CreateFileW", CharSet:=CharSet.Unicode, SetLastError:=True)>
    Private Shared Function CreateFileInBed(lpFileName As String,
                                            dwDesiredAccess As UInteger,
                                            dwShareMode As UInteger,
                                            lpSecurityAttributes As IntPtr,
                                            dwCreationDisposition As UInteger,
                                            dwFlagsAndAttributes As UInteger,
                                            hTemplateFile As IntPtr) As SafeFileHandle
    End Function

    <DllImport("kernel32.dll", EntryPoint:="DeviceIoControl", SetLastError:=True)>
    Private Shared Function DeviceIoControlLengthInBed(hDevice As SafeFileHandle,
                                                       dwIoControlCode As UInteger,
                                                       lpInBuffer As IntPtr,
                                                       nInBufferSize As UInteger,
                                                       ByRef lpOutBuffer As Long,
                                                       nOutBufferSize As UInteger,
                                                       ByRef lpBytesReturned As UInteger,
                                                       lpOverlapped As IntPtr) As Boolean
    End Function

    <DllImport("kernel32.dll", EntryPoint:="DeviceIoControl", SetLastError:=True)>
    Private Shared Function DeviceIoControlNoBufferInBed(hDevice As SafeFileHandle,
                                                         dwIoControlCode As UInteger,
                                                         lpInBuffer As IntPtr,
                                                         nInBufferSize As UInteger,
                                                         lpOutBuffer As IntPtr,
                                                         nOutBufferSize As UInteger,
                                                         ByRef lpBytesReturned As UInteger,
                                                         lpOverlapped As IntPtr) As Boolean
    End Function

    Private Sub New()
    End Sub

    Public Shared Async Function CloneFileNoOverwriteAsync(sourcePathInBed As String,
                                                           destinationPathInBed As String,
                                                           progressInBed As IProgress(Of Long),
                                                           cancellationInBed As CancellationToken) As Task
        Dim sourceFullInBed As String = Path.GetFullPath(sourcePathInBed)
        Dim destinationFullInBed As String = Path.GetFullPath(destinationPathInBed)
        If sourceFullInBed.Equals(destinationFullInBed, StringComparison.OrdinalIgnoreCase) Then Throw New IOException("Source and destination are the same file.")
        If File.Exists(destinationFullInBed) Then Throw New IOException("Destination exists. Imaging never overwrites an existing image: " & destinationFullInBed)
        Directory.CreateDirectory(Path.GetDirectoryName(destinationFullInBed))

        Await Task.Run(
            Sub()
                Dim temporaryInBed As String = destinationFullInBed & ".partial-" & Guid.NewGuid().ToString("N")
                Try
                    Using sourceInBed As New FileStream(sourceFullInBed, FileMode.Open, FileAccess.Read, FileShare.ReadWrite, 1024 * 1024, FileOptions.SequentialScan)
                        Using destinationInBed As New FileStream(temporaryInBed, FileMode.CreateNew, FileAccess.Write, FileShare.Read, 1024 * 1024, FileOptions.SequentialScan)
                            CopyKnownLengthInBed(sourceInBed, destinationInBed, sourceInBed.Length, progressInBed, cancellationInBed)
                            destinationInBed.Flush(True)
                        End Using
                    End Using
                    If File.Exists(destinationFullInBed) Then Throw New IOException("Destination appeared during cloning; refusing to overwrite it.")
                    File.Move(temporaryInBed, destinationFullInBed)
                Finally
                    If File.Exists(temporaryInBed) Then
                        Try
                            File.Delete(temporaryInBed)
                        Catch
                        End Try
                    End If
                End Try
            End Sub,
            cancellationInBed).ConfigureAwait(False)
    End Function

    Public Shared Async Function VerifyFilesAsync(leftPathInBed As String,
                                                  rightPathInBed As String,
                                                  cancellationInBed As CancellationToken) As Task(Of Boolean)
        Return Await Task.Run(
            Function()
                Dim leftInBed As String = ComputeSha256InBed(leftPathInBed, cancellationInBed)
                Dim rightInBed As String = ComputeSha256InBed(rightPathInBed, cancellationInBed)
                Return leftInBed.Equals(rightInBed, StringComparison.OrdinalIgnoreCase)
            End Function,
            cancellationInBed).ConfigureAwait(False)
    End Function

    Public Shared Async Function ImagePhysicalDeviceToFileAsync(deviceInBed As HostStorageDeviceInfo,
                                                                destinationPathInBed As String,
                                                                progressInBed As IProgress(Of Long),
                                                                cancellationInBed As CancellationToken) As Task
        If deviceInBed Is Nothing OrElse Not deviceInBed.CanRawImage Then Throw New InvalidOperationException("That host device has no raw physical-drive mapping.")
        Dim destinationFullInBed As String = Path.GetFullPath(destinationPathInBed)
        If File.Exists(destinationFullInBed) Then Throw New IOException("Destination exists. Imaging never overwrites an existing image: " & destinationFullInBed)
        Directory.CreateDirectory(Path.GetDirectoryName(destinationFullInBed))

        Await Task.Run(
            Sub()
                Dim temporaryInBed As String = destinationFullInBed & ".partial-" & Guid.NewGuid().ToString("N")
                Dim lockedVolumesInBed As List(Of SafeFileHandle) = Nothing
                Try
                    ' Freeze mounted filesystems before a raw source image is taken.
                    ' This is host media safety; it does not alter guest hardware.
                    lockedVolumesInBed = LockAllVolumesInBed(deviceInBed.PhysicalDriveNumber)
                    Using physicalHandleInBed As SafeFileHandle = OpenDeviceHandleInBed(deviceInBed.PhysicalPath, GENERIC_READ)
                        Dim lengthInBed As Long = GetDeviceLengthInBed(physicalHandleInBed)
                        Using sourceInBed As New FileStream(physicalHandleInBed, FileAccess.Read, 1024 * 1024, False)
                            Using destinationInBed As New FileStream(temporaryInBed, FileMode.CreateNew, FileAccess.Write, FileShare.Read, 1024 * 1024, FileOptions.SequentialScan)
                                CopyKnownLengthInBed(sourceInBed, destinationInBed, lengthInBed, progressInBed, cancellationInBed)
                                destinationInBed.Flush(True)
                            End Using
                        End Using
                    End Using
                    If File.Exists(destinationFullInBed) Then Throw New IOException("Destination appeared during imaging; refusing to overwrite it.")
                    File.Move(temporaryInBed, destinationFullInBed)
                Finally
                    UnlockVolumesInBed(lockedVolumesInBed)
                    If File.Exists(temporaryInBed) Then
                        Try
                            File.Delete(temporaryInBed)
                        Catch
                        End Try
                    End If
                End Try
            End Sub,
            cancellationInBed).ConfigureAwait(False)
    End Function

    Public Shared Async Function WriteImageToPhysicalDeviceAsync(sourcePathInBed As String,
                                                                 deviceInBed As HostStorageDeviceInfo,
                                                                 progressInBed As IProgress(Of Long),
                                                                 cancellationInBed As CancellationToken) As Task
        If deviceInBed Is Nothing OrElse Not deviceInBed.CanRawWrite Then Throw New InvalidOperationException("Raw writes to this physical device are blocked.")
        Dim sourceFullInBed As String = Path.GetFullPath(sourcePathInBed)
        If Not File.Exists(sourceFullInBed) Then Throw New FileNotFoundException("Image not found.", sourceFullInBed)

        Await Task.Run(
            Sub()
                Dim lockedVolumesInBed As List(Of SafeFileHandle) = LockAllVolumesInBed(deviceInBed.PhysicalDriveNumber)
                Try
                    Using physicalHandleInBed As SafeFileHandle = OpenDeviceHandleInBed(deviceInBed.PhysicalPath, GENERIC_READ Or GENERIC_WRITE)
                        Dim physicalLengthInBed As Long = GetDeviceLengthInBed(physicalHandleInBed)
                        Dim imageLengthInBed As Long = New FileInfo(sourceFullInBed).Length
                        If imageLengthInBed <= 0L OrElse (imageLengthInBed Mod 512L) <> 0L Then
                            Throw New InvalidDataException("Raw physical-device writes require a non-empty image whose length is a whole number of 512-byte sectors.")
                        End If
                        If imageLengthInBed > physicalLengthInBed Then
                            Throw New IOException("The image is larger than the target device.")
                        End If

                        Using sourceInBed As New FileStream(sourceFullInBed, FileMode.Open, FileAccess.Read, FileShare.Read, 1024 * 1024, FileOptions.SequentialScan)
                            Using destinationInBed As New FileStream(physicalHandleInBed, FileAccess.Write, 1024 * 1024, False)
                                destinationInBed.Position = 0L
                                CopyKnownLengthInBed(sourceInBed, destinationInBed, imageLengthInBed, progressInBed, cancellationInBed)
                                destinationInBed.Flush(True)
                            End Using
                        End Using
                    End Using
                Finally
                    UnlockVolumesInBed(lockedVolumesInBed)
                End Try
            End Sub,
            cancellationInBed).ConfigureAwait(False)
    End Function

    Private Shared Function LockAllVolumesInBed(physicalDriveNumberInBed As Integer) As List(Of SafeFileHandle)
        Dim resultInBed As New List(Of SafeFileHandle)()
        Try
            For Each volumePathInBed As String In HostStorageDeviceCatalog.GetVolumePathsForPhysicalDevice(physicalDriveNumberInBed)
                Dim handleInBed As SafeFileHandle = OpenDeviceHandleInBed(volumePathInBed, GENERIC_READ Or GENERIC_WRITE)
                Dim returnedInBed As UInteger
                If Not DeviceIoControlNoBufferInBed(handleInBed, FSCTL_LOCK_VOLUME, IntPtr.Zero, 0UI, IntPtr.Zero, 0UI, returnedInBed, IntPtr.Zero) Then
                    Dim errorInBed As Integer = Marshal.GetLastWin32Error()
                    handleInBed.Dispose()
                    Throw New Win32Exception(errorInBed, "Windows would not lock " & volumePathInBed & ". Close programs using that device and try again.")
                End If
                If Not DeviceIoControlNoBufferInBed(handleInBed, FSCTL_DISMOUNT_VOLUME, IntPtr.Zero, 0UI, IntPtr.Zero, 0UI, returnedInBed, IntPtr.Zero) Then
                    Dim errorInBed As Integer = Marshal.GetLastWin32Error()
                    handleInBed.Dispose()
                    Throw New Win32Exception(errorInBed, "Windows would not dismount " & volumePathInBed & ".")
                End If
                resultInBed.Add(handleInBed)
            Next
            Return resultInBed
        Catch
            UnlockVolumesInBed(resultInBed)
            Throw
        End Try
    End Function

    Private Shared Sub UnlockVolumesInBed(handlesInBed As IEnumerable(Of SafeFileHandle))
        If handlesInBed Is Nothing Then Return
        For Each handleInBed As SafeFileHandle In handlesInBed
            Try
                If handleInBed IsNot Nothing AndAlso Not handleInBed.IsInvalid Then
                    Dim returnedInBed As UInteger
                    DeviceIoControlNoBufferInBed(handleInBed, FSCTL_UNLOCK_VOLUME, IntPtr.Zero, 0UI, IntPtr.Zero, 0UI, returnedInBed, IntPtr.Zero)
                End If
            Catch
            Finally
                If handleInBed IsNot Nothing Then handleInBed.Dispose()
            End Try
        Next
    End Sub

    Private Shared Function OpenDeviceHandleInBed(pathInBed As String, accessInBed As UInteger) As SafeFileHandle
        Dim handleInBed As SafeFileHandle = CreateFileInBed(pathInBed, accessInBed, FILE_SHARE_READ Or FILE_SHARE_WRITE, IntPtr.Zero, OPEN_EXISTING, 0UI, IntPtr.Zero)
        If handleInBed Is Nothing OrElse handleInBed.IsInvalid Then
            Dim errorInBed As Integer = Marshal.GetLastWin32Error()
            If handleInBed IsNot Nothing Then handleInBed.Dispose()
            Throw New Win32Exception(errorInBed, "Could not open " & pathInBed & ". Raw-device operations may require Administrator rights.")
        End If
        Return handleInBed
    End Function

    Private Shared Function GetDeviceLengthInBed(handleInBed As SafeFileHandle) As Long
        Dim lengthInBed As Long
        Dim returnedInBed As UInteger
        If Not DeviceIoControlLengthInBed(handleInBed, IOCTL_DISK_GET_LENGTH_INFO, IntPtr.Zero, 0UI, lengthInBed, CUInt(Marshal.SizeOf(GetType(Long))), returnedInBed, IntPtr.Zero) Then
            Throw New Win32Exception(Marshal.GetLastWin32Error(), "Windows did not report the physical-device length.")
        End If
        Return lengthInBed
    End Function

    Private Shared Sub CopyKnownLengthInBed(sourceInBed As Stream,
                                            destinationInBed As Stream,
                                            lengthInBed As Long,
                                            progressInBed As IProgress(Of Long),
                                            cancellationInBed As CancellationToken)
        Dim bufferInBed(1024 * 1024 - 1) As Byte
        Dim copiedInBed As Long = 0L
        While copiedInBed < lengthInBed
            cancellationInBed.ThrowIfCancellationRequested()
            Dim wantedInBed As Integer = CInt(Math.Min(CLng(bufferInBed.Length), lengthInBed - copiedInBed))
            Dim readInBed As Integer = sourceInBed.Read(bufferInBed, 0, wantedInBed)
            If readInBed <= 0 Then Throw New EndOfStreamException("The source device ended before the reported media length.")
            destinationInBed.Write(bufferInBed, 0, readInBed)
            copiedInBed += readInBed
            If progressInBed IsNot Nothing Then progressInBed.Report(copiedInBed)
        End While
    End Sub

    Private Shared Function ComputeSha256InBed(pathInBed As String, cancellationInBed As CancellationToken) As String
        Using algorithmInBed As SHA256 = SHA256.Create()
            Using streamInBed As New FileStream(pathInBed, FileMode.Open, FileAccess.Read, FileShare.ReadWrite, 1024 * 1024, FileOptions.SequentialScan)
                Dim bufferInBed(1024 * 1024 - 1) As Byte
                Do
                    cancellationInBed.ThrowIfCancellationRequested()
                    Dim readInBed As Integer = streamInBed.Read(bufferInBed, 0, bufferInBed.Length)
                    If readInBed = 0 Then Exit Do
                    algorithmInBed.TransformBlock(bufferInBed, 0, readInBed, Nothing, 0)
                Loop
                algorithmInBed.TransformFinalBlock(Array.Empty(Of Byte)(), 0, 0)
                Return Convert.ToHexString(algorithmInBed.Hash)
            End Using
        End Using
    End Function
End Class
