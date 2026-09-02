Imports System
Imports System.Collections.Generic
Imports System.Diagnostics
Imports System.IO
Imports System.IO.Compression
Imports System.Linq
Imports System.Runtime.InteropServices
Imports System.Text
Imports System.Threading
Imports System.Threading.Tasks
Imports System.Windows.Forms

' CROMWELL TECHNOLOGIES SNEAKER NET MEDIA WORKBENCH
'
' Host-side media logistics and imaging. Nothing here invents guest hardware:
' the active chassis still decides which prepared media can actually be attached.
' Floppy Box remains the period-media workbench inside the broader host tool.
Public NotInheritable Class FloppyBox
    Private Const BoxDirectoryName As String = "Floppy-Box"
    Private ReadOnly _rootPath As String

    Public Sub New(baseDirectory As String)
        If String.IsNullOrWhiteSpace(baseDirectory) Then
            Throw New ArgumentNullException(NameOf(baseDirectory))
        End If
        _rootPath = Path.Combine(Path.GetFullPath(baseDirectory), BoxDirectoryName)
    End Sub

    Public ReadOnly Property RootPath As String
        Get
            Return _rootPath
        End Get
    End Property

    Public Sub EnsureExists()
        Directory.CreateDirectory(_rootPath)
    End Sub

    Public Function GetImages() As List(Of String)
        EnsureExists()
        Dim images As New List(Of String)()
        For Each pathInBed As String In Directory.EnumerateFiles(_rootPath, "*", SearchOption.TopDirectoryOnly)
            Dim extensionInBed As String = Path.GetExtension(pathInBed)
            If extensionInBed.Equals(".img", StringComparison.OrdinalIgnoreCase) OrElse
               extensionInBed.Equals(".ima", StringComparison.OrdinalIgnoreCase) Then
                images.Add(pathInBed)
            End If
        Next
        images.Sort(Function(leftInBed, rightInBed)
                        Return StringComparer.CurrentCultureIgnoreCase.Compare(
                            Path.GetFileName(leftInBed),
                            Path.GetFileName(rightInBed))
                    End Function)
        Return images
    End Function

    Public Function CreateUniqueImagePath(labelInBed As String) As String
        EnsureExists()
        Dim safeInBed As String = SanitizeHostLabel(labelInBed)
        Dim candidateInBed As String = Path.Combine(_rootPath, safeInBed & ".img")
        If Not File.Exists(candidateInBed) Then Return candidateInBed

        Dim suffixInBed As Integer = 2
        Do
            candidateInBed = Path.Combine(
                _rootPath,
                safeInBed & " (" & suffixInBed.ToString() & ").img")
            If Not File.Exists(candidateInBed) Then Return candidateInBed
            suffixInBed += 1
        Loop
    End Function

    Public Function CreateUniqueImageSetPaths(labelInBed As String, countInBed As Integer) As List(Of String)
        If countInBed <= 0 Then Throw New ArgumentOutOfRangeException(NameOf(countInBed))
        EnsureExists()

        Dim safeInBed As String = SanitizeHostLabel(labelInBed)
        Dim setBaseInBed As String = safeInBed
        Dim suffixInBed As Integer = 2

        Do
            Dim resultInBed As New List(Of String)()
            Dim collisionInBed As Boolean = False
            For diskInBed As Integer = 1 To countInBed
                Dim candidateInBed As String = Path.Combine(
                    _rootPath,
                    setBaseInBed & "_" & diskInBed.ToString() & ".img")
                If File.Exists(candidateInBed) Then
                    collisionInBed = True
                    Exit For
                End If
                resultInBed.Add(candidateInBed)
            Next
            If Not collisionInBed Then Return resultInBed

            setBaseInBed = safeInBed & " (" & suffixInBed.ToString() & ")"
            suffixInBed += 1
        Loop
    End Function

    Public Shared Function SanitizeHostLabel(labelInBed As String) As String
        Dim cleanedInBed As String = If(labelInBed, String.Empty).Trim()
        If cleanedInBed.Length = 0 Then cleanedInBed = "Sneaker Net Disk"
        For Each badInBed As Char In Path.GetInvalidFileNameChars()
            cleanedInBed = cleanedInBed.Replace(badInBed, "_"c)
        Next
        cleanedInBed = cleanedInBed.Trim(" "c, "."c)
        If cleanedInBed.Length = 0 Then cleanedInBed = "Sneaker Net Disk"
        Return cleanedInBed
    End Function
End Class

Public NotInheritable Class SneakerNetResult
    Public Sub New(pathInBed As String,
                   formatNameInBed As String,
                   fileCountInBed As Integer,
                   payloadBytesInBed As Long)
        ImagePath = pathInBed
        FormatName = formatNameInBed
        FileCount = fileCountInBed
        PayloadBytes = payloadBytesInBed
    End Sub

    Public ReadOnly Property ImagePath As String
    Public ReadOnly Property FormatName As String
    Public ReadOnly Property FileCount As Integer
    Public ReadOnly Property PayloadBytes As Long
End Class

Public NotInheritable Class SneakerNetTool
    Private Sub New()
    End Sub

    Public Shared Function CreateTransferDisk(ownerInBed As IWin32Window,
                                              boxInBed As FloppyBox) As SneakerNetResult
        If boxInBed Is Nothing Then Throw New ArgumentNullException(NameOf(boxInBed))
        boxInBed.EnsureExists()

        Dim selectedInBed As String()
        Using pickerInBed As New OpenFileDialog()
            pickerInBed.Title = "Sneaker Net - choose files for the floppy"
            pickerInBed.Filter = "All files (*.*)|*.*"
            pickerInBed.Multiselect = True
            pickerInBed.CheckFileExists = True
            pickerInBed.CheckPathExists = True
            If pickerInBed.ShowDialog(ownerInBed) <> DialogResult.OK Then Return Nothing
            selectedInBed = pickerInBed.FileNames
        End Using

        If selectedInBed Is Nothing OrElse selectedInBed.Length = 0 Then Return Nothing

        Dim defaultLabelInBed As String =
            Path.GetFileNameWithoutExtension(selectedInBed(0))
        If selectedInBed.Length > 1 Then defaultLabelInBed &= " Transfer"

        Dim labelInBed As String = Microsoft.VisualBasic.Interaction.InputBox(
            "Name for the new disk in the Floppy Box:" & Environment.NewLine &
            "Sneaker Net will choose the smallest standard FAT12 floppy that fits.",
            "Cromwell Technologies Sneaker Net",
            defaultLabelInBed)

        If String.IsNullOrWhiteSpace(labelInBed) Then Return Nothing

        Dim formatInBed As Fat12FloppyFormat =
            Fat12FloppyBuilder.SelectSmallestFormat(selectedInBed)
        If formatInBed Is Nothing Then
            Throw New InvalidOperationException(
                "Those files do not fit on a supported floppy image." &
                Environment.NewLine &
                "Quick Sneaker Net currently tops out at a 2.88 MB FAT12 floppy." &
                Environment.NewLine &
                "The later full workbench can hand oversized payloads to the Disc Box.")
        End If

        Dim imagePathInBed As String = boxInBed.CreateUniqueImagePath(labelInBed)
        Fat12FloppyBuilder.CreateImage(
            selectedInBed,
            imagePathInBed,
            labelInBed,
            formatInBed)

        Dim payloadInBed As Long = 0
        For Each sourceInBed As String In selectedInBed
            payloadInBed += New FileInfo(sourceInBed).Length
        Next

        Return New SneakerNetResult(
            imagePathInBed,
            formatInBed.DisplayName,
            selectedInBed.Length,
            payloadInBed)
    End Function
End Class

Public NotInheritable Class Fat12FloppyFormat
    Public Sub New(displayNameInBed As String,
                   totalSectorsInBed As Integer,
                   sectorsPerClusterInBed As Integer,
                   rootEntriesInBed As Integer,
                   mediaDescriptorInBed As Byte,
                   sectorsPerFatInBed As Integer,
                   sectorsPerTrackInBed As Integer,
                   headsInBed As Integer)
        DisplayName = displayNameInBed
        TotalSectors = totalSectorsInBed
        SectorsPerCluster = sectorsPerClusterInBed
        RootEntries = rootEntriesInBed
        MediaDescriptor = mediaDescriptorInBed
        SectorsPerFat = sectorsPerFatInBed
        SectorsPerTrack = sectorsPerTrackInBed
        Heads = headsInBed
    End Sub

    Public ReadOnly Property DisplayName As String
    Public ReadOnly Property TotalSectors As Integer
    Public ReadOnly Property SectorsPerCluster As Integer
    Public ReadOnly Property RootEntries As Integer
    Public ReadOnly Property MediaDescriptor As Byte
    Public ReadOnly Property SectorsPerFat As Integer
    Public ReadOnly Property SectorsPerTrack As Integer
    Public ReadOnly Property Heads As Integer

    Public ReadOnly Property TotalBytes As Integer
        Get
            Return TotalSectors * 512
        End Get
    End Property

    Public ReadOnly Property RootDirectorySectors As Integer
        Get
            Return ((RootEntries * 32) + 511) \ 512
        End Get
    End Property

    Public ReadOnly Property DataStartSector As Integer
        Get
            Return 1 + (2 * SectorsPerFat) + RootDirectorySectors
        End Get
    End Property

    Public ReadOnly Property AvailableClusters As Integer
        Get
            Return (TotalSectors - DataStartSector) \ SectorsPerCluster
        End Get
    End Property

    Public ReadOnly Property ClusterBytes As Integer
        Get
            Return SectorsPerCluster * 512
        End Get
    End Property
End Class

Public NotInheritable Class Fat12FloppyBuilder
    Private Sub New()
    End Sub

    ' These are exactly the raw geometries accepted by FloppyImage.DetectGeometry.
    ' The BPB values are conventional DOS FAT12 layouts for those geometries.
    Private Shared ReadOnly FormatsInBed As Fat12FloppyFormat() = {
        New Fat12FloppyFormat("160 KB 5.25-inch SS/DD", 320, 1, 64, &HFE, 1, 8, 1),
        New Fat12FloppyFormat("180 KB 5.25-inch SS/DD", 360, 1, 64, &HFC, 2, 9, 1),
        New Fat12FloppyFormat("320 KB 5.25-inch DS/DD", 640, 2, 112, &HFF, 1, 8, 2),
        New Fat12FloppyFormat("360 KB 5.25-inch DS/DD", 720, 2, 112, &HFD, 2, 9, 2),
        New Fat12FloppyFormat("720 KB 3.5-inch DD", 1440, 2, 112, &HF9, 3, 9, 2),
        New Fat12FloppyFormat("1.2 MB 5.25-inch HD", 2400, 1, 224, &HF9, 7, 15, 2),
        New Fat12FloppyFormat("1.44 MB 3.5-inch HD", 2880, 1, 224, &HF0, 9, 18, 2),
        New Fat12FloppyFormat("2.88 MB 3.5-inch ED", 5760, 2, 240, &HF0, 9, 36, 2)
    }

    Public Shared Function GetFormats() As Fat12FloppyFormat()
        Return FormatsInBed.ToArray()
    End Function

    Public Shared Function SelectSmallestFormat(sourceFilesInBed As IEnumerable(Of String)) As Fat12FloppyFormat
        Dim filesInBed As String() = sourceFilesInBed.ToArray()
        For Each formatInBed As Fat12FloppyFormat In FormatsInBed
            If Fits(filesInBed, formatInBed) Then Return formatInBed
        Next
        Return Nothing
    End Function

    Public Shared Function Fits(filesInBed As String(),
                                 formatInBed As Fat12FloppyFormat) As Boolean
        ' One root entry is reserved for the volume label.
        If filesInBed.Length + 1 > formatInBed.RootEntries Then Return False

        Dim clustersNeededInBed As Long = 0
        For Each pathInBed As String In filesInBed
            Dim lengthInBed As Long = New FileInfo(pathInBed).Length
            If lengthInBed < 0 OrElse lengthInBed > UInteger.MaxValue Then Return False
            If lengthInBed > 0 Then
                clustersNeededInBed +=
                    (lengthInBed + formatInBed.ClusterBytes - 1L) \ formatInBed.ClusterBytes
            End If
            If clustersNeededInBed > formatInBed.AvailableClusters Then Return False
        Next
        Return clustersNeededInBed <= formatInBed.AvailableClusters
    End Function

    Public Shared Function FitsHostPaths(pathsInBed As IEnumerable(Of String),
                                         formatInBed As Fat12FloppyFormat) As Boolean
        If formatInBed Is Nothing Then Return False
        Dim pathsArrayInBed As String() = pathsInBed.
            Where(Function(pathInBed) Not String.IsNullOrWhiteSpace(pathInBed) AndAlso
                  (File.Exists(pathInBed) OrElse Directory.Exists(pathInBed))).
            Select(Function(pathInBed) Path.GetFullPath(pathInBed)).
            Distinct(StringComparer.OrdinalIgnoreCase).
            ToArray()
        If pathsArrayInBed.Length = 0 Then Return False

        ' One fixed root entry is reserved for the volume label.  Subdirectories
        ' consume ordinary FAT12 data clusters and carry . / .. entries.
        If pathsArrayInBed.Length + 1 > formatInBed.RootEntries Then Return False

        Try
            Dim clustersNeededInBed As Long = 0
            For Each pathInBed As String In pathsArrayInBed
                clustersNeededInBed += HostPathClusterCost(pathInBed, formatInBed)
                If clustersNeededInBed > formatInBed.AvailableClusters Then Return False
            Next
            Return clustersNeededInBed <= formatInBed.AvailableClusters
        Catch
            Return False
        End Try
    End Function

    Private Shared Function HostPathClusterCost(pathInBed As String,
                                                formatInBed As Fat12FloppyFormat) As Long
        If File.Exists(pathInBed) Then
            Dim lengthInBed As Long = New FileInfo(pathInBed).Length
            If lengthInBed < 0 OrElse lengthInBed > UInteger.MaxValue Then
                Throw New InvalidOperationException("A host file is too large for FAT12.")
            End If
            If lengthInBed = 0 Then Return 0
            Return (lengthInBed + formatInBed.ClusterBytes - 1L) \ formatInBed.ClusterBytes
        End If

        If Not Directory.Exists(pathInBed) Then Return 0
        Dim infoInBed As New DirectoryInfo(pathInBed)
        If (infoInBed.Attributes And FileAttributes.ReparsePoint) <> 0 Then
            Throw New IOException("Reparse-point directories are not imported into FAT12 images.")
        End If

        Dim childDirectoriesInBed As String() = Directory.GetDirectories(pathInBed)
        Dim childFilesInBed As String() = Directory.GetFiles(pathInBed)
        Dim entriesPerClusterInBed As Integer = formatInBed.ClusterBytes \ 32
        Dim directoryEntriesInBed As Integer = 2 + childDirectoriesInBed.Length + childFilesInBed.Length
        Dim directoryClustersInBed As Long =
            Math.Max(1, (directoryEntriesInBed + entriesPerClusterInBed - 1) \ entriesPerClusterInBed)

        Dim totalInBed As Long = directoryClustersInBed
        For Each directoryInBed As String In childDirectoriesInBed
            totalInBed += HostPathClusterCost(directoryInBed, formatInBed)
        Next
        For Each fileInBed As String In childFilesInBed
            totalInBed += HostPathClusterCost(fileInBed, formatInBed)
        Next
        Return totalInBed
    End Function

    Public Shared Sub CreateImage(sourceFilesInBed As IEnumerable(Of String),
                                  destinationPathInBed As String,
                                  volumeLabelInBed As String,
                                  formatInBed As Fat12FloppyFormat)
        If formatInBed Is Nothing Then Throw New ArgumentNullException(NameOf(formatInBed))
        Dim filesInBed As String() = sourceFilesInBed.ToArray()
        If Not Fits(filesInBed, formatInBed) Then
            Throw New InvalidOperationException("Payload does not fit " & formatInBed.DisplayName & ".")
        End If

        Dim imageInBed(formatInBed.TotalBytes - 1) As Byte
        WriteBootSector(imageInBed, formatInBed, volumeLabelInBed)

        Dim fatInBed(formatInBed.SectorsPerFat * 512 - 1) As Byte
        fatInBed(0) = formatInBed.MediaDescriptor
        fatInBed(1) = &HFF
        fatInBed(2) = &HFF

        Dim rootStartInBed As Integer = (1 + (2 * formatInBed.SectorsPerFat)) * 512
        Dim dataStartInBed As Integer = formatInBed.DataStartSector * 512

        WriteVolumeLabelEntry(
            imageInBed,
            rootStartInBed,
            MakeVolumeLabel(volumeLabelInBed))

        Dim usedNamesInBed As New HashSet(Of String)(StringComparer.OrdinalIgnoreCase)
        Dim nextClusterInBed As Integer = 2
        Dim rootEntryInBed As Integer = 1

        For Each sourcePathInBed As String In filesInBed
            Dim infoInBed As New FileInfo(sourcePathInBed)
            Dim shortNameInBed As String = MakeUniqueShortName(infoInBed.Name, usedNamesInBed)
            Dim fileDataInBed As Byte() = File.ReadAllBytes(sourcePathInBed)
            Dim clusterCountInBed As Integer = 0
            Dim firstClusterInBed As Integer = 0

            If fileDataInBed.Length > 0 Then
                clusterCountInBed =
                    (fileDataInBed.Length + formatInBed.ClusterBytes - 1) \ formatInBed.ClusterBytes
                firstClusterInBed = nextClusterInBed

                For clusterOffsetInBed As Integer = 0 To clusterCountInBed - 1
                    Dim clusterInBed As Integer = nextClusterInBed + clusterOffsetInBed
                    Dim nextValueInBed As Integer =
                        If(clusterOffsetInBed = clusterCountInBed - 1,
                           &HFFF,
                           clusterInBed + 1)
                    SetFat12Entry(fatInBed, clusterInBed, nextValueInBed)

                    Dim sourceOffsetInBed As Integer =
                        clusterOffsetInBed * formatInBed.ClusterBytes
                    Dim countInBed As Integer =
                        Math.Min(formatInBed.ClusterBytes,
                                 fileDataInBed.Length - sourceOffsetInBed)
                    Dim destinationOffsetInBed As Integer =
                        dataStartInBed +
                        ((clusterInBed - 2) * formatInBed.ClusterBytes)

                    Buffer.BlockCopy(
                        fileDataInBed,
                        sourceOffsetInBed,
                        imageInBed,
                        destinationOffsetInBed,
                        countInBed)
                Next

                nextClusterInBed += clusterCountInBed
            End If

            WriteFileDirectoryEntry(
                imageInBed,
                rootStartInBed + (rootEntryInBed * 32),
                shortNameInBed,
                firstClusterInBed,
                CUInt(fileDataInBed.Length),
                infoInBed.LastWriteTime)

            rootEntryInBed += 1
        Next

        Dim firstFatOffsetInBed As Integer = 512
        Buffer.BlockCopy(fatInBed, 0, imageInBed, firstFatOffsetInBed, fatInBed.Length)
        Buffer.BlockCopy(
            fatInBed,
            0,
            imageInBed,
            firstFatOffsetInBed + fatInBed.Length,
            fatInBed.Length)

        Directory.CreateDirectory(Path.GetDirectoryName(destinationPathInBed))
        Using streamInBed As New FileStream(
            destinationPathInBed,
            FileMode.CreateNew,
            FileAccess.Write,
            FileShare.None)
            streamInBed.Write(imageInBed, 0, imageInBed.Length)
            streamInBed.Flush()
        End Using
    End Sub

    Private Shared Sub WriteBootSector(imageInBed As Byte(),
                                       formatInBed As Fat12FloppyFormat,
                                       volumeLabelInBed As String)
        imageInBed(0) = &HEB
        imageInBed(1) = &H3C
        imageInBed(2) = &H90
        WriteAscii(imageInBed, 3, "CROMWELL", 8)

        WriteUInt16(imageInBed, 11, 512US)
        imageInBed(13) = CByte(formatInBed.SectorsPerCluster)
        WriteUInt16(imageInBed, 14, 1US)
        imageInBed(16) = 2
        WriteUInt16(imageInBed, 17, CUShort(formatInBed.RootEntries))
        WriteUInt16(imageInBed, 19, CUShort(formatInBed.TotalSectors))
        imageInBed(21) = formatInBed.MediaDescriptor
        WriteUInt16(imageInBed, 22, CUShort(formatInBed.SectorsPerFat))
        WriteUInt16(imageInBed, 24, CUShort(formatInBed.SectorsPerTrack))
        WriteUInt16(imageInBed, 26, CUShort(formatInBed.Heads))
        WriteUInt32(imageInBed, 28, 0UI)
        WriteUInt32(imageInBed, 32, 0UI)

        imageInBed(36) = 0
        imageInBed(37) = 0
        imageInBed(38) = &H29
        Dim serialInBed As UInteger =
            CUInt(DateTime.Now.Ticks And &HFFFFFFFFL)
        WriteUInt32(imageInBed, 39, serialInBed)
        WriteAscii(imageInBed, 43, MakeVolumeLabel(volumeLabelInBed), 11)
        WriteAscii(imageInBed, 54, "FAT12   ", 8)

        ' Non-bootable transfer disk: politely hand control back to the BIOS.
        imageInBed(62) = &HCD
        imageInBed(63) = &H18
        imageInBed(64) = &HEB
        imageInBed(65) = &HFE

        imageInBed(510) = &H55
        imageInBed(511) = &HAA
    End Sub

    Private Shared Sub WriteVolumeLabelEntry(imageInBed As Byte(),
                                             offsetInBed As Integer,
                                             labelInBed As String)
        WriteAscii(imageInBed, offsetInBed, labelInBed, 11)
        imageInBed(offsetInBed + 11) = &H8
        WriteDosDateTime(imageInBed, offsetInBed, DateTime.Now)
    End Sub

    Private Shared Sub WriteFileDirectoryEntry(imageInBed As Byte(),
                                               offsetInBed As Integer,
                                               shortNameInBed As String,
                                               firstClusterInBed As Integer,
                                               fileSizeInBed As UInteger,
                                               modifiedInBed As DateTime)
        Dim piecesInBed As String() = shortNameInBed.Split("."c)
        Dim baseInBed As String = piecesInBed(0)
        Dim extensionInBed As String = If(piecesInBed.Length > 1, piecesInBed(1), String.Empty)

        WriteAscii(imageInBed, offsetInBed, baseInBed.PadRight(8), 8)
        WriteAscii(imageInBed, offsetInBed + 8, extensionInBed.PadRight(3), 3)
        imageInBed(offsetInBed + 11) = &H20
        WriteDosDateTime(imageInBed, offsetInBed, modifiedInBed)
        WriteUInt16(imageInBed, offsetInBed + 26, CUShort(firstClusterInBed))
        WriteUInt32(imageInBed, offsetInBed + 28, fileSizeInBed)
    End Sub

    Private Shared Sub WriteDosDateTime(imageInBed As Byte(),
                                        directoryOffsetInBed As Integer,
                                        valueInBed As DateTime)
        Dim safeInBed As DateTime = valueInBed
        If safeInBed.Year < 1980 Then safeInBed = New DateTime(1980, 1, 1, 0, 0, 0)
        If safeInBed.Year > 2107 Then safeInBed = New DateTime(2107, 12, 31, 23, 59, 58)

        Dim dosTimeInBed As UShort =
            CUShort((safeInBed.Hour << 11) Or
                    (safeInBed.Minute << 5) Or
                    (safeInBed.Second \ 2))
        Dim dosDateInBed As UShort =
            CUShort(((safeInBed.Year - 1980) << 9) Or
                    (safeInBed.Month << 5) Or
                    safeInBed.Day)

        WriteUInt16(imageInBed, directoryOffsetInBed + 22, dosTimeInBed)
        WriteUInt16(imageInBed, directoryOffsetInBed + 24, dosDateInBed)
    End Sub

    Private Shared Sub SetFat12Entry(fatInBed As Byte(),
                                     clusterInBed As Integer,
                                     valueInBed As Integer)
        valueInBed = valueInBed And &HFFF
        Dim offsetInBed As Integer = clusterInBed + (clusterInBed \ 2)

        If (clusterInBed And 1) = 0 Then
            fatInBed(offsetInBed) = CByte(valueInBed And &HFF)
            fatInBed(offsetInBed + 1) =
                CByte((fatInBed(offsetInBed + 1) And &HF0) Or
                      ((valueInBed >> 8) And &HF))
        Else
            fatInBed(offsetInBed) =
                CByte((fatInBed(offsetInBed) And &HF) Or
                      ((valueInBed << 4) And &HF0))
            fatInBed(offsetInBed + 1) =
                CByte((valueInBed >> 4) And &HFF)
        End If
    End Sub

    Private Shared Function MakeUniqueShortName(fileNameInBed As String,
                                                usedInBed As HashSet(Of String)) As String
        Dim baseInBed As String =
            CleanDosPart(Path.GetFileNameWithoutExtension(fileNameInBed))
        Dim extensionInBed As String =
            CleanDosPart(Path.GetExtension(fileNameInBed).TrimStart("."c))

        If baseInBed.Length = 0 Then baseInBed = "FILE"
        If baseInBed.Length > 8 Then baseInBed = baseInBed.Substring(0, 8)
        If extensionInBed.Length > 3 Then extensionInBed = extensionInBed.Substring(0, 3)

        Dim candidateInBed As String =
            If(extensionInBed.Length = 0,
               baseInBed,
               baseInBed & "." & extensionInBed)

        If usedInBed.Add(candidateInBed) Then Return candidateInBed

        For suffixInBed As Integer = 1 To 999999
            Dim suffixTextInBed As String = "~" & suffixInBed.ToString()
            Dim keepInBed As Integer = Math.Max(1, 8 - suffixTextInBed.Length)
            Dim collisionBaseInBed As String =
                baseInBed.Substring(0, Math.Min(baseInBed.Length, keepInBed)) &
                suffixTextInBed
            candidateInBed =
                If(extensionInBed.Length = 0,
                   collisionBaseInBed,
                   collisionBaseInBed & "." & extensionInBed)
            If usedInBed.Add(candidateInBed) Then Return candidateInBed
        Next

        Throw New IOException("Could not generate a unique DOS 8.3 filename for " & fileNameInBed & ".")
    End Function

    Private Shared Function CleanDosPart(valueInBed As String) As String
        Dim builderInBed As New StringBuilder()
        For Each chInBed As Char In If(valueInBed, String.Empty).ToUpperInvariant()
            If (chInBed >= "A"c AndAlso chInBed <= "Z"c) OrElse
               (chInBed >= "0"c AndAlso chInBed <= "9"c) OrElse
               "_-$~!#%&'()@^{}".IndexOf(chInBed) >= 0 Then
                builderInBed.Append(chInBed)
            ElseIf chInBed <> " "c AndAlso chInBed <> "."c Then
                builderInBed.Append("_"c)
            End If
        Next
        Return builderInBed.ToString()
    End Function

    Private Shared Function MakeVolumeLabel(valueInBed As String) As String
        Dim builderInBed As New StringBuilder()
        For Each chInBed As Char In If(valueInBed, String.Empty).ToUpperInvariant()
            If chInBed >= " "c AndAlso chInBed <= "~"c AndAlso
               chInBed <> "."c AndAlso chInBed <> "/"c AndAlso
               chInBed <> "\"c AndAlso chInBed <> ":"c AndAlso
               chInBed <> ";"c AndAlso chInBed <> "*"c AndAlso
               chInBed <> "?"c AndAlso chInBed <> """"c AndAlso
               chInBed <> "<"c AndAlso chInBed <> ">"c AndAlso
               chInBed <> "|"c AndAlso chInBed <> "+"c AndAlso
               chInBed <> "="c AndAlso chInBed <> "["c AndAlso
               chInBed <> "]"c Then
                builderInBed.Append(chInBed)
            End If
            If builderInBed.Length = 11 Then Exit For
        Next
        If builderInBed.Length = 0 Then builderInBed.Append("SNEAKERNET")
        Return builderInBed.ToString().PadRight(11).Substring(0, 11)
    End Function

    Private Shared Sub WriteAscii(targetInBed As Byte(),
                                  offsetInBed As Integer,
                                  valueInBed As String,
                                  fixedLengthInBed As Integer)
        Dim textInBed As String = If(valueInBed, String.Empty)
        If textInBed.Length < fixedLengthInBed Then textInBed = textInBed.PadRight(fixedLengthInBed)
        If textInBed.Length > fixedLengthInBed Then textInBed = textInBed.Substring(0, fixedLengthInBed)
        Dim bytesInBed As Byte() = Encoding.ASCII.GetBytes(textInBed)
        Buffer.BlockCopy(bytesInBed, 0, targetInBed, offsetInBed, fixedLengthInBed)
    End Sub

    Private Shared Sub WriteUInt16(targetInBed As Byte(),
                                   offsetInBed As Integer,
                                   valueInBed As UShort)
        targetInBed(offsetInBed) = CByte(valueInBed And &HFFUS)
        targetInBed(offsetInBed + 1) = CByte((valueInBed >> 8) And &HFFUS)
    End Sub

    Private Shared Sub WriteUInt32(targetInBed As Byte(),
                                   offsetInBed As Integer,
                                   valueInBed As UInteger)
        targetInBed(offsetInBed) = CByte(valueInBed And &HFFUI)
        targetInBed(offsetInBed + 1) = CByte((valueInBed >> 8) And &HFFUI)
        targetInBed(offsetInBed + 2) = CByte((valueInBed >> 16) And &HFFUI)
        targetInBed(offsetInBed + 3) = CByte((valueInBed >> 24) And &HFFUI)
    End Sub
End Class

Public NotInheritable Class SneakerNetIso9660Builder
    Private Const SectorBytes As Integer = 2048

    Private NotInheritable Class IsoFileInBed
        Public Property SourcePath As String
        Public Property Identifier As String
        Public Property Length As UInteger
        Public Property Extent As UInteger
        Public Property Modified As DateTime
    End Class

    Private Sub New()
    End Sub

    Public Shared Sub CreateImage(sourceFilesInBed As IEnumerable(Of String),
                                  destinationPathInBed As String,
                                  volumeLabelInBed As String)
        Dim sourcesInBed As String() = sourceFilesInBed.
            Where(Function(pathInBed) Not String.IsNullOrWhiteSpace(pathInBed) AndAlso File.Exists(pathInBed)).
            Select(Function(pathInBed) Path.GetFullPath(pathInBed)).
            Distinct(StringComparer.OrdinalIgnoreCase).
            ToArray()
        If sourcesInBed.Length = 0 Then Throw New InvalidOperationException("No host files were supplied for the ISO image.")

        Dim usedNamesInBed As New HashSet(Of String)(StringComparer.OrdinalIgnoreCase)
        Dim entriesInBed As New List(Of IsoFileInBed)()
        For Each sourceInBed As String In sourcesInBed
            Dim infoInBed As New FileInfo(sourceInBed)
            If infoInBed.Length < 0 OrElse infoInBed.Length > UInteger.MaxValue Then
                Throw New InvalidOperationException(infoInBed.Name & " is too large for this ISO 9660 Level 1 writer.")
            End If
            entriesInBed.Add(New IsoFileInBed With {
                .SourcePath = sourceInBed,
                .Identifier = MakeUniqueIsoIdentifier(infoInBed.Name, usedNamesInBed),
                .Length = CUInt(infoInBed.Length),
                .Modified = infoInBed.LastWriteTime
            })
        Next

        Const pvdSectorInBed As UInteger = 16UI
        Const terminatorSectorInBed As UInteger = 17UI
        Const littlePathTableSectorInBed As UInteger = 18UI
        Const bigPathTableSectorInBed As UInteger = 19UI
        Const rootSectorInBed As UInteger = 20UI

        Dim rootDirectoryBytesInBed As Integer = ComputeRootDirectorySize(entriesInBed)
        Dim rootDirectorySectorsInBed As UInteger = CUInt((rootDirectoryBytesInBed + SectorBytes - 1) \ SectorBytes)
        Dim nextExtentInBed As UInteger = rootSectorInBed + rootDirectorySectorsInBed
        For Each entryInBed As IsoFileInBed In entriesInBed
            entryInBed.Extent = nextExtentInBed
            Dim sectorsInBed As UInteger = CUInt((CULng(entryInBed.Length) + SectorBytes - 1UL) \ SectorBytes)
            nextExtentInBed += sectorsInBed
        Next
        Dim totalSectorsInBed As UInteger = nextExtentInBed

        Dim rootDataInBed(CInt(rootDirectorySectorsInBed) * SectorBytes - 1) As Byte
        BuildRootDirectory(rootDataInBed, rootSectorInBed, CUInt(rootDataInBed.Length), entriesInBed)

        Dim directoryInBed As String = Path.GetDirectoryName(destinationPathInBed)
        If Not String.IsNullOrWhiteSpace(directoryInBed) Then Directory.CreateDirectory(directoryInBed)
        Using streamInBed As New FileStream(destinationPathInBed, FileMode.CreateNew, FileAccess.Write, FileShare.None)
            streamInBed.SetLength(CLng(totalSectorsInBed) * SectorBytes)

            Dim pvdInBed(SectorBytes - 1) As Byte
            pvdInBed(0) = 1
            WriteAsciiFixed(pvdInBed, 1, "CD001", 5, ChrW(0))
            pvdInBed(6) = 1
            WriteAsciiFixed(pvdInBed, 8, String.Empty, 32, " "c)
            WriteAsciiFixed(pvdInBed, 40, MakeIsoVolumeLabel(volumeLabelInBed), 32, " "c)
            WriteBothEndianUInt32(pvdInBed, 80, totalSectorsInBed)
            WriteBothEndianUInt16(pvdInBed, 120, 1US)
            WriteBothEndianUInt16(pvdInBed, 124, 1US)
            WriteBothEndianUInt16(pvdInBed, 128, CUShort(SectorBytes))
            WriteBothEndianUInt32(pvdInBed, 132, 10UI)
            WriteUInt32LE(pvdInBed, 140, littlePathTableSectorInBed)
            WriteUInt32LE(pvdInBed, 144, 0UI)
            WriteUInt32BE(pvdInBed, 148, bigPathTableSectorInBed)
            WriteUInt32BE(pvdInBed, 152, 0UI)

            Dim rootRecordInBed As Byte() = MakeDirectoryRecord(rootSectorInBed, CUInt(rootDataInBed.Length), DateTime.Now, True, New Byte() {0})
            Buffer.BlockCopy(rootRecordInBed, 0, pvdInBed, 156, rootRecordInBed.Length)
            WriteAsciiFixed(pvdInBed, 190, "CROMWELL TECHNOLOGIES", 128, " "c)
            WriteAsciiFixed(pvdInBed, 318, "CROMWELL TECHNOLOGIES", 128, " "c)
            WriteAsciiFixed(pvdInBed, 446, "SNEAKER NET", 128, " "c)
            WriteAsciiFixed(pvdInBed, 574, "SNEAKER NET", 128, " "c)
            WriteAsciiFixed(pvdInBed, 702, String.Empty, 37, " "c)
            WriteAsciiFixed(pvdInBed, 739, String.Empty, 37, " "c)
            WriteAsciiFixed(pvdInBed, 776, String.Empty, 37, " "c)
            WriteVolumeDateTime(pvdInBed, 813, DateTime.UtcNow)
            WriteVolumeDateTime(pvdInBed, 830, DateTime.UtcNow)
            WriteAsciiFixed(pvdInBed, 847, "0000000000000000", 16, "0"c)
            pvdInBed(863) = 0
            WriteAsciiFixed(pvdInBed, 864, "0000000000000000", 16, "0"c)
            pvdInBed(880) = 0
            pvdInBed(881) = 1
            WriteSector(streamInBed, pvdSectorInBed, pvdInBed)

            Dim terminatorInBed(SectorBytes - 1) As Byte
            terminatorInBed(0) = 255
            WriteAsciiFixed(terminatorInBed, 1, "CD001", 5, ChrW(0))
            terminatorInBed(6) = 1
            WriteSector(streamInBed, terminatorSectorInBed, terminatorInBed)

            Dim littlePathInBed(SectorBytes - 1) As Byte
            littlePathInBed(0) = 1
            littlePathInBed(1) = 0
            WriteUInt32LE(littlePathInBed, 2, rootSectorInBed)
            WriteUInt16LE(littlePathInBed, 6, 1US)
            littlePathInBed(8) = 0
            littlePathInBed(9) = 0
            WriteSector(streamInBed, littlePathTableSectorInBed, littlePathInBed)

            Dim bigPathInBed(SectorBytes - 1) As Byte
            bigPathInBed(0) = 1
            bigPathInBed(1) = 0
            WriteUInt32BE(bigPathInBed, 2, rootSectorInBed)
            WriteUInt16BE(bigPathInBed, 6, 1US)
            bigPathInBed(8) = 0
            bigPathInBed(9) = 0
            WriteSector(streamInBed, bigPathTableSectorInBed, bigPathInBed)

            streamInBed.Position = CLng(rootSectorInBed) * SectorBytes
            streamInBed.Write(rootDataInBed, 0, rootDataInBed.Length)

            Dim bufferInBed(1024 * 1024 - 1) As Byte
            For Each entryInBed As IsoFileInBed In entriesInBed
                streamInBed.Position = CLng(entryInBed.Extent) * SectorBytes
                Using inputInBed As New FileStream(entryInBed.SourcePath, FileMode.Open, FileAccess.Read, FileShare.Read)
                    Dim readInBed As Integer
                    Do
                        readInBed = inputInBed.Read(bufferInBed, 0, bufferInBed.Length)
                        If readInBed > 0 Then streamInBed.Write(bufferInBed, 0, readInBed)
                    Loop While readInBed > 0
                End Using
            Next
            streamInBed.Flush()
        End Using
    End Sub

    Private Shared Function ComputeRootDirectorySize(entriesInBed As List(Of IsoFileInBed)) As Integer
        Dim lengthsInBed As New List(Of Integer) From {
            MakeDirectoryRecord(0UI, 0UI, DateTime.Now, True, New Byte() {0}).Length,
            MakeDirectoryRecord(0UI, 0UI, DateTime.Now, True, New Byte() {1}).Length
        }
        For Each entryInBed As IsoFileInBed In entriesInBed
            lengthsInBed.Add(MakeDirectoryRecord(0UI, entryInBed.Length, entryInBed.Modified, False, Encoding.ASCII.GetBytes(entryInBed.Identifier)).Length)
        Next

        Dim offsetInBed As Integer = 0
        For Each lengthInBed As Integer In lengthsInBed
            Dim sectorOffsetInBed As Integer = offsetInBed Mod SectorBytes
            If sectorOffsetInBed + lengthInBed > SectorBytes Then
                offsetInBed += SectorBytes - sectorOffsetInBed
            End If
            offsetInBed += lengthInBed
        Next
        Return Math.Max(SectorBytes, ((offsetInBed + SectorBytes - 1) \ SectorBytes) * SectorBytes)
    End Function

    Private Shared Sub BuildRootDirectory(targetInBed As Byte(),
                                          rootExtentInBed As UInteger,
                                          rootLengthInBed As UInteger,
                                          entriesInBed As List(Of IsoFileInBed))
        Dim recordsInBed As New List(Of Byte()) From {
            MakeDirectoryRecord(rootExtentInBed, rootLengthInBed, DateTime.Now, True, New Byte() {0}),
            MakeDirectoryRecord(rootExtentInBed, rootLengthInBed, DateTime.Now, True, New Byte() {1})
        }
        For Each entryInBed As IsoFileInBed In entriesInBed
            recordsInBed.Add(MakeDirectoryRecord(entryInBed.Extent, entryInBed.Length, entryInBed.Modified, False, Encoding.ASCII.GetBytes(entryInBed.Identifier)))
        Next

        Dim offsetInBed As Integer = 0
        For Each recordInBed As Byte() In recordsInBed
            Dim sectorOffsetInBed As Integer = offsetInBed Mod SectorBytes
            If sectorOffsetInBed + recordInBed.Length > SectorBytes Then
                offsetInBed += SectorBytes - sectorOffsetInBed
            End If
            Buffer.BlockCopy(recordInBed, 0, targetInBed, offsetInBed, recordInBed.Length)
            offsetInBed += recordInBed.Length
        Next
    End Sub

    Private Shared Function MakeDirectoryRecord(extentInBed As UInteger,
                                                lengthInBed As UInteger,
                                                modifiedInBed As DateTime,
                                                isDirectoryInBed As Boolean,
                                                identifierInBed As Byte()) As Byte()
        Dim idLengthInBed As Integer = identifierInBed.Length
        Dim recordLengthInBed As Integer = 33 + idLengthInBed + If((idLengthInBed And 1) = 0, 1, 0)
        Dim recordInBed(recordLengthInBed - 1) As Byte
        recordInBed(0) = CByte(recordLengthInBed)
        recordInBed(1) = 0
        WriteBothEndianUInt32(recordInBed, 2, extentInBed)
        WriteBothEndianUInt32(recordInBed, 10, lengthInBed)
        WriteDirectoryDateTime(recordInBed, 18, modifiedInBed)
        recordInBed(25) = If(isDirectoryInBed, CByte(2), CByte(0))
        recordInBed(26) = 0
        recordInBed(27) = 0
        WriteBothEndianUInt16(recordInBed, 28, 1US)
        recordInBed(32) = CByte(idLengthInBed)
        Buffer.BlockCopy(identifierInBed, 0, recordInBed, 33, idLengthInBed)
        Return recordInBed
    End Function

    Private Shared Function MakeUniqueIsoIdentifier(fileNameInBed As String,
                                                    usedInBed As HashSet(Of String)) As String
        Dim baseInBed As String = CleanIsoPart(Path.GetFileNameWithoutExtension(fileNameInBed))
        Dim extInBed As String = CleanIsoPart(Path.GetExtension(fileNameInBed).TrimStart("."c))
        If baseInBed.Length = 0 Then baseInBed = "FILE"
        If baseInBed.Length > 8 Then baseInBed = baseInBed.Substring(0, 8)
        If extInBed.Length > 3 Then extInBed = extInBed.Substring(0, 3)

        For suffixInBed As Integer = 0 To 999999
            Dim candidateBaseInBed As String = baseInBed
            If suffixInBed > 0 Then
                Dim tailInBed As String = suffixInBed.ToString()
                candidateBaseInBed = baseInBed.Substring(0, Math.Min(baseInBed.Length, Math.Max(1, 8 - tailInBed.Length))) & tailInBed
            End If
            Dim candidateInBed As String = candidateBaseInBed & If(extInBed.Length > 0, "." & extInBed, String.Empty) & ";1"
            If usedInBed.Add(candidateInBed) Then Return candidateInBed
        Next
        Throw New IOException("Could not generate a unique ISO 9660 filename for " & fileNameInBed & ".")
    End Function

    Private Shared Function CleanIsoPart(valueInBed As String) As String
        Dim builderInBed As New StringBuilder()
        For Each chInBed As Char In If(valueInBed, String.Empty).ToUpperInvariant()
            If (chInBed >= "A"c AndAlso chInBed <= "Z"c) OrElse
               (chInBed >= "0"c AndAlso chInBed <= "9"c) OrElse chInBed = "_"c Then
                builderInBed.Append(chInBed)
            ElseIf chInBed <> " "c AndAlso chInBed <> "."c Then
                builderInBed.Append("_"c)
            End If
        Next
        Return builderInBed.ToString()
    End Function

    Private Shared Function MakeIsoVolumeLabel(valueInBed As String) As String
        Dim cleanedInBed As String = CleanIsoPart(If(valueInBed, "SNEAKERNET"))
        If cleanedInBed.Length = 0 Then cleanedInBed = "SNEAKERNET"
        If cleanedInBed.Length > 32 Then cleanedInBed = cleanedInBed.Substring(0, 32)
        Return cleanedInBed
    End Function

    Private Shared Sub WriteSector(streamInBed As Stream, sectorInBed As UInteger, dataInBed As Byte())
        streamInBed.Position = CLng(sectorInBed) * SectorBytes
        streamInBed.Write(dataInBed, 0, Math.Min(dataInBed.Length, SectorBytes))
    End Sub

    Private Shared Sub WriteDirectoryDateTime(targetInBed As Byte(), offsetInBed As Integer, valueInBed As DateTime)
        Dim safeInBed As DateTime = valueInBed.ToUniversalTime()
        If safeInBed.Year < 1900 Then safeInBed = New DateTime(1900, 1, 1)
        If safeInBed.Year > 2155 Then safeInBed = New DateTime(2155, 12, 31, 23, 59, 59)
        targetInBed(offsetInBed) = CByte(safeInBed.Year - 1900)
        targetInBed(offsetInBed + 1) = CByte(safeInBed.Month)
        targetInBed(offsetInBed + 2) = CByte(safeInBed.Day)
        targetInBed(offsetInBed + 3) = CByte(safeInBed.Hour)
        targetInBed(offsetInBed + 4) = CByte(safeInBed.Minute)
        targetInBed(offsetInBed + 5) = CByte(safeInBed.Second)
        targetInBed(offsetInBed + 6) = 0
    End Sub

    Private Shared Sub WriteVolumeDateTime(targetInBed As Byte(), offsetInBed As Integer, valueInBed As DateTime)
        Dim utcInBed As DateTime = valueInBed.ToUniversalTime()
        Dim textInBed As String = utcInBed.ToString("yyyyMMddHHmmss") & "00"
        Dim bytesInBed As Byte() = Encoding.ASCII.GetBytes(textInBed)
        Buffer.BlockCopy(bytesInBed, 0, targetInBed, offsetInBed, 16)
        targetInBed(offsetInBed + 16) = 0
    End Sub

    Private Shared Sub WriteAsciiFixed(targetInBed As Byte(), offsetInBed As Integer, valueInBed As String, lengthInBed As Integer, padInBed As Char)
        Dim textInBed As String = If(valueInBed, String.Empty)
        If textInBed.Length > lengthInBed Then textInBed = textInBed.Substring(0, lengthInBed)
        textInBed = textInBed.PadRight(lengthInBed, padInBed)
        Dim bytesInBed As Byte() = Encoding.ASCII.GetBytes(textInBed)
        Buffer.BlockCopy(bytesInBed, 0, targetInBed, offsetInBed, lengthInBed)
    End Sub

    Private Shared Sub WriteBothEndianUInt16(targetInBed As Byte(), offsetInBed As Integer, valueInBed As UShort)
        WriteUInt16LE(targetInBed, offsetInBed, valueInBed)
        WriteUInt16BE(targetInBed, offsetInBed + 2, valueInBed)
    End Sub

    Private Shared Sub WriteBothEndianUInt32(targetInBed As Byte(), offsetInBed As Integer, valueInBed As UInteger)
        WriteUInt32LE(targetInBed, offsetInBed, valueInBed)
        WriteUInt32BE(targetInBed, offsetInBed + 4, valueInBed)
    End Sub

    Private Shared Sub WriteUInt16LE(targetInBed As Byte(), offsetInBed As Integer, valueInBed As UShort)
        targetInBed(offsetInBed) = CByte(valueInBed And &HFFUS)
        targetInBed(offsetInBed + 1) = CByte((valueInBed >> 8) And &HFFUS)
    End Sub

    Private Shared Sub WriteUInt16BE(targetInBed As Byte(), offsetInBed As Integer, valueInBed As UShort)
        targetInBed(offsetInBed) = CByte((valueInBed >> 8) And &HFFUS)
        targetInBed(offsetInBed + 1) = CByte(valueInBed And &HFFUS)
    End Sub

    Private Shared Sub WriteUInt32LE(targetInBed As Byte(), offsetInBed As Integer, valueInBed As UInteger)
        targetInBed(offsetInBed) = CByte(valueInBed And &HFFUI)
        targetInBed(offsetInBed + 1) = CByte((valueInBed >> 8) And &HFFUI)
        targetInBed(offsetInBed + 2) = CByte((valueInBed >> 16) And &HFFUI)
        targetInBed(offsetInBed + 3) = CByte((valueInBed >> 24) And &HFFUI)
    End Sub

    Private Shared Sub WriteUInt32BE(targetInBed As Byte(), offsetInBed As Integer, valueInBed As UInteger)
        targetInBed(offsetInBed) = CByte((valueInBed >> 24) And &HFFUI)
        targetInBed(offsetInBed + 1) = CByte((valueInBed >> 16) And &HFFUI)
        targetInBed(offsetInBed + 2) = CByte((valueInBed >> 8) And &HFFUI)
        targetInBed(offsetInBed + 3) = CByte(valueInBed And &HFFUI)
    End Sub
End Class

Public NotInheritable Class PkZip204Spanner
    Private Const LocalHeaderSignature As UInteger = &H4034B50UI
    Private Const CentralHeaderSignature As UInteger = &H2014B50UI
    Private Const EndSignature As UInteger = &H6054B50UI
    Private Const SpanningMarker As UInteger = &H8074B50UI
    Private Const Version20 As UShort = 20US
    Private Const MethodStored As UShort = 0US
    Private Const MethodDeflate As UShort = 8US
    Private Const MaximumPk204Entries As Integer = 16383
    Private Const MaximumPkBackVolumes As Integer = 999

    Private NotInheritable Class PackedEntryInBed
        Public Property SourcePath As String
        Public Property ArchiveName As String
        Public Property Method As UShort
        Public Property Crc32 As UInteger
        Public Property UncompressedSize As UInteger
        Public Property CompressedSize As UInteger
        Public Property PackedPath As String
        Public Property PackedPathIsTemporary As Boolean
        Public Property DosTime As UShort
        Public Property DosDate As UShort
        Public Property LocalDisk As UShort
        Public Property LocalOffset As UInteger
        Public Property CentralDisk As UShort
    End Class

    Private NotInheritable Class SpanWriterInBed
        Implements IDisposable

        Private ReadOnly _directory As String
        Private ReadOnly _capacity As Integer
        Private ReadOnly _paths As New List(Of String)()
        Private _stream As FileStream
        Private _disk As Integer = -1

        Public Sub New(directoryInBed As String, capacityInBed As Integer)
            _directory = directoryInBed
            _capacity = capacityInBed
            Directory.CreateDirectory(_directory)
            NextSegment()
        End Sub

        Public ReadOnly Property Paths As List(Of String)
            Get
                Return _paths
            End Get
        End Property

        Public ReadOnly Property DiskNumber As Integer
            Get
                Return _disk
            End Get
        End Property

        Public ReadOnly Property Offset As Integer
            Get
                Return CInt(_stream.Position)
            End Get
        End Property

        Public ReadOnly Property Remaining As Integer
            Get
                Return _capacity - Offset
            End Get
        End Property

        Public Sub EnsureRecordFits(lengthInBed As Integer)
            If lengthInBed > _capacity Then Throw New InvalidOperationException("A ZIP header record is larger than the selected span size.")
            If Remaining < lengthInBed Then NextSegment()
        End Sub

        Public Sub WriteRecord(dataInBed As Byte())
            EnsureRecordFits(dataInBed.Length)
            _stream.Write(dataInBed, 0, dataInBed.Length)
        End Sub

        Public Sub WritePayload(streamInBed As Stream)
            Dim bufferInBed(1024 * 1024 - 1) As Byte
            If streamInBed.CanSeek Then
                While streamInBed.Position < streamInBed.Length
                    If Remaining = 0 Then NextSegment()
                    Dim wantedInBed As Integer = Math.Min(bufferInBed.Length, Remaining)
                    Dim readInBed As Integer = streamInBed.Read(bufferInBed, 0, wantedInBed)
                    If readInBed <= 0 Then Exit While
                    _stream.Write(bufferInBed, 0, readInBed)
                End While
            Else
                Do
                    If Remaining = 0 Then NextSegment()
                    Dim wantedInBed As Integer = Math.Min(bufferInBed.Length, Remaining)
                    Dim readInBed As Integer = streamInBed.Read(bufferInBed, 0, wantedInBed)
                    If readInBed <= 0 Then Exit Do
                    _stream.Write(bufferInBed, 0, readInBed)
                Loop
            End If
        End Sub

        Public Sub NextSegment()
            If _stream IsNot Nothing Then
                _stream.Flush()
                _stream.Dispose()
            End If
            _disk += 1
            If _disk >= MaximumPkBackVolumes Then Throw New InvalidOperationException("PKZIP 2.04g DOS spanning is limited here to 999 PKBACK volumes.")
            Dim pathInBed As String = Path.Combine(_directory, "segment-" & (_disk + 1).ToString("000") & ".bin")
            _paths.Add(pathInBed)
            _stream = New FileStream(pathInBed, FileMode.CreateNew, FileAccess.Write, FileShare.None)
        End Sub

        Public Sub Dispose() Implements IDisposable.Dispose
            If _stream IsNot Nothing Then
                _stream.Flush()
                _stream.Dispose()
                _stream = Nothing
            End If
        End Sub
    End Class

    Private Shared ReadOnly CrcTableInBed As UInteger() = BuildCrcTable()

    Private Sub New()
    End Sub

    Public Shared Function CreateFloppySet(sourceFilesInBed As IEnumerable(Of String),
                                           boxInBed As FloppyBox,
                                           setLabelInBed As String,
                                           formatInBed As Fat12FloppyFormat) As List(Of String)
        If boxInBed Is Nothing Then Throw New ArgumentNullException(NameOf(boxInBed))
        If formatInBed Is Nothing Then Throw New ArgumentNullException(NameOf(formatInBed))
        Dim sourcesInBed As String() = sourceFilesInBed.
            Where(Function(pathInBed) Not String.IsNullOrWhiteSpace(pathInBed) AndAlso File.Exists(pathInBed)).
            Select(Function(pathInBed) Path.GetFullPath(pathInBed)).
            Distinct(StringComparer.OrdinalIgnoreCase).
            ToArray()
        If sourcesInBed.Length = 0 Then Throw New InvalidOperationException("Choose at least one file to span.")
        If sourcesInBed.Length > MaximumPk204Entries Then Throw New InvalidOperationException("PKZIP 2.04g compatibility is limited to 16,383 archive entries.")

        Dim segmentCapacityInBed As Integer = formatInBed.AvailableClusters * formatInBed.ClusterBytes
        If segmentCapacityInBed < 65536 Then Throw New InvalidOperationException("Selected floppy geometry is too small for ZIP spanning.")

        Dim safeLabelInBed As String = FloppyBox.SanitizeHostLabel(setLabelInBed)
        Dim archiveFileNameInBed As String = MakeDosZipFileName(safeLabelInBed)
        Dim tempRootInBed As String = Path.Combine(Path.GetTempPath(), "SneakerNet-PK204-" & Guid.NewGuid().ToString("N"))
        Dim packedDirectoryInBed As String = Path.Combine(tempRootInBed, "packed")
        Dim spanDirectoryInBed As String = Path.Combine(tempRootInBed, "span")
        Directory.CreateDirectory(packedDirectoryInBed)

        Dim packedEntriesInBed As New List(Of PackedEntryInBed)()
        Try
            Dim usedNamesInBed As New HashSet(Of String)(StringComparer.OrdinalIgnoreCase)
            For indexInBed As Integer = 0 To sourcesInBed.Length - 1
                packedEntriesInBed.Add(PackSource(sourcesInBed(indexInBed), packedDirectoryInBed, indexInBed, usedNamesInBed))
            Next

            Dim centralRecordsInBed As New List(Of Byte())()
            Dim centralSizeInBed As UInteger = 0UI
            Dim centralStartDiskInBed As UShort
            Dim centralStartOffsetInBed As UInteger
            Dim finalDiskEntryCountInBed As UShort

            Using writerInBed As New SpanWriterInBed(spanDirectoryInBed, segmentCapacityInBed)
                writerInBed.WriteRecord(MakeUInt32Bytes(SpanningMarker))

                For Each entryInBed As PackedEntryInBed In packedEntriesInBed
                    Dim nameBytesInBed As Byte() = Encoding.ASCII.GetBytes(entryInBed.ArchiveName)
                    Dim localInBed As Byte() = MakeLocalHeader(entryInBed, nameBytesInBed)
                    writerInBed.EnsureRecordFits(localInBed.Length)
                    entryInBed.LocalDisk = CUShort(writerInBed.DiskNumber)
                    entryInBed.LocalOffset = CUInt(writerInBed.Offset)
                    writerInBed.WriteRecord(localInBed)
                    Using packedInBed As New FileStream(entryInBed.PackedPath, FileMode.Open, FileAccess.Read, FileShare.Read)
                        writerInBed.WritePayload(packedInBed)
                    End Using
                Next

                For Each entryInBed As PackedEntryInBed In packedEntriesInBed
                    Dim recordInBed As Byte() = MakeCentralHeader(entryInBed, Encoding.ASCII.GetBytes(entryInBed.ArchiveName))
                    centralRecordsInBed.Add(recordInBed)
                    centralSizeInBed += CUInt(recordInBed.Length)
                Next

                For indexInBed As Integer = 0 To centralRecordsInBed.Count - 1
                    Dim recordInBed As Byte() = centralRecordsInBed(indexInBed)
                    Dim reserveEndInBed As Integer = If(indexInBed = centralRecordsInBed.Count - 1, 22, 0)
                    If writerInBed.Remaining < recordInBed.Length + reserveEndInBed Then writerInBed.NextSegment()
                    If indexInBed = 0 Then
                        centralStartDiskInBed = CUShort(writerInBed.DiskNumber)
                        centralStartOffsetInBed = CUInt(writerInBed.Offset)
                    End If
                    packedEntriesInBed(indexInBed).CentralDisk = CUShort(writerInBed.DiskNumber)
                    writerInBed.WriteRecord(recordInBed)
                Next

                finalDiskEntryCountInBed = CUShort(packedEntriesInBed.Where(Function(entryInBed) entryInBed.CentralDisk = writerInBed.DiskNumber).Count())
                Dim endInBed As Byte() = MakeEndRecord(
                    CUShort(writerInBed.DiskNumber),
                    centralStartDiskInBed,
                    finalDiskEntryCountInBed,
                    CUShort(packedEntriesInBed.Count),
                    centralSizeInBed,
                    centralStartOffsetInBed)
                writerInBed.WriteRecord(endInBed)
            End Using

            Dim rawSegmentsInBed As String() = Directory.GetFiles(spanDirectoryInBed, "segment-*.bin").OrderBy(Function(pathInBed) pathInBed).ToArray()
            Dim imagePathsInBed As List(Of String) = boxInBed.CreateUniqueImageSetPaths(safeLabelInBed, rawSegmentsInBed.Length)

            For indexInBed As Integer = 0 To rawSegmentsInBed.Length - 1
                Dim segmentFolderInBed As String = Path.Combine(tempRootInBed, "disk-" & (indexInBed + 1).ToString("000"))
                Directory.CreateDirectory(segmentFolderInBed)
                Dim zipOnDiskInBed As String = Path.Combine(segmentFolderInBed, archiveFileNameInBed)
                File.Copy(rawSegmentsInBed(indexInBed), zipOnDiskInBed)
                Dim volumeLabelInBed As String = "PKBACK#" & (indexInBed + 1).ToString("000")
                Fat12FloppyBuilder.CreateImage(New String() {zipOnDiskInBed}, imagePathsInBed(indexInBed), volumeLabelInBed, formatInBed)
            Next

            Return imagePathsInBed
        Finally
            For Each entryInBed As PackedEntryInBed In packedEntriesInBed
                If entryInBed.PackedPathIsTemporary Then
                    Try
                        If File.Exists(entryInBed.PackedPath) Then File.Delete(entryInBed.PackedPath)
                    Catch
                    End Try
                End If
            Next
            Try
                If Directory.Exists(tempRootInBed) Then Directory.Delete(tempRootInBed, True)
            Catch
            End Try
        End Try
    End Function

    Private Shared Function PackSource(sourcePathInBed As String,
                                       packedDirectoryInBed As String,
                                       indexInBed As Integer,
                                       usedNamesInBed As HashSet(Of String)) As PackedEntryInBed
        Dim infoInBed As New FileInfo(sourcePathInBed)
        If infoInBed.Length < 0 OrElse infoInBed.Length > UInteger.MaxValue Then
            Throw New InvalidOperationException(infoInBed.Name & " exceeds the classic ZIP 32-bit file-size limit.")
        End If

        Dim dosTimeInBed As UShort
        Dim dosDateInBed As UShort
        ToDosDateTime(infoInBed.LastWriteTime, dosTimeInBed, dosDateInBed)

        Dim packedPathInBed As String = Path.Combine(packedDirectoryInBed, "packed-" & indexInBed.ToString("00000") & ".deflate")
        Dim crcInBed As UInteger = &HFFFFFFFFUI
        Dim inputBytesInBed As Long = 0
        Dim bufferInBed(1024 * 1024 - 1) As Byte
        Using inputInBed As New FileStream(sourcePathInBed, FileMode.Open, FileAccess.Read, FileShare.Read),
              outputInBed As New FileStream(packedPathInBed, FileMode.CreateNew, FileAccess.Write, FileShare.None),
              deflateInBed As New DeflateStream(outputInBed, CompressionLevel.Optimal, leaveOpen:=True)
            Dim readInBed As Integer
            Do
                readInBed = inputInBed.Read(bufferInBed, 0, bufferInBed.Length)
                If readInBed > 0 Then
                    crcInBed = UpdateCrc(crcInBed, bufferInBed, readInBed)
                    inputBytesInBed += readInBed
                    deflateInBed.Write(bufferInBed, 0, readInBed)
                End If
            Loop While readInBed > 0
        End Using
        crcInBed = crcInBed Xor &HFFFFFFFFUI

        Dim packedLengthInBed As Long = New FileInfo(packedPathInBed).Length
        Dim useStoredInBed As Boolean = packedLengthInBed >= inputBytesInBed
        If useStoredInBed Then
            Try
                File.Delete(packedPathInBed)
            Catch
            End Try
        End If

        Return New PackedEntryInBed With {
            .SourcePath = sourcePathInBed,
            .ArchiveName = MakeUniqueDosArchiveName(infoInBed.Name, usedNamesInBed),
            .Method = If(useStoredInBed, MethodStored, MethodDeflate),
            .Crc32 = crcInBed,
            .UncompressedSize = CUInt(inputBytesInBed),
            .CompressedSize = CUInt(If(useStoredInBed, inputBytesInBed, packedLengthInBed)),
            .PackedPath = If(useStoredInBed, sourcePathInBed, packedPathInBed),
            .PackedPathIsTemporary = Not useStoredInBed,
            .DosTime = dosTimeInBed,
            .DosDate = dosDateInBed
        }
    End Function

    Private Shared Function MakeLocalHeader(entryInBed As PackedEntryInBed, nameBytesInBed As Byte()) As Byte()
        Dim dataInBed(30 + nameBytesInBed.Length - 1) As Byte
        WriteUInt32(dataInBed, 0, LocalHeaderSignature)
        WriteUInt16(dataInBed, 4, Version20)
        WriteUInt16(dataInBed, 6, 0US)
        WriteUInt16(dataInBed, 8, entryInBed.Method)
        WriteUInt16(dataInBed, 10, entryInBed.DosTime)
        WriteUInt16(dataInBed, 12, entryInBed.DosDate)
        WriteUInt32(dataInBed, 14, entryInBed.Crc32)
        WriteUInt32(dataInBed, 18, entryInBed.CompressedSize)
        WriteUInt32(dataInBed, 22, entryInBed.UncompressedSize)
        WriteUInt16(dataInBed, 26, CUShort(nameBytesInBed.Length))
        WriteUInt16(dataInBed, 28, 0US)
        Buffer.BlockCopy(nameBytesInBed, 0, dataInBed, 30, nameBytesInBed.Length)
        Return dataInBed
    End Function

    Private Shared Function MakeCentralHeader(entryInBed As PackedEntryInBed, nameBytesInBed As Byte()) As Byte()
        Dim dataInBed(46 + nameBytesInBed.Length - 1) As Byte
        WriteUInt32(dataInBed, 0, CentralHeaderSignature)
        WriteUInt16(dataInBed, 4, Version20) ' made by PKZIP 2.x / MS-DOS attribute model
        WriteUInt16(dataInBed, 6, Version20)
        WriteUInt16(dataInBed, 8, 0US)
        WriteUInt16(dataInBed, 10, entryInBed.Method)
        WriteUInt16(dataInBed, 12, entryInBed.DosTime)
        WriteUInt16(dataInBed, 14, entryInBed.DosDate)
        WriteUInt32(dataInBed, 16, entryInBed.Crc32)
        WriteUInt32(dataInBed, 20, entryInBed.CompressedSize)
        WriteUInt32(dataInBed, 24, entryInBed.UncompressedSize)
        WriteUInt16(dataInBed, 28, CUShort(nameBytesInBed.Length))
        WriteUInt16(dataInBed, 30, 0US)
        WriteUInt16(dataInBed, 32, 0US)
        WriteUInt16(dataInBed, 34, entryInBed.LocalDisk)
        WriteUInt16(dataInBed, 36, 0US)
        WriteUInt32(dataInBed, 38, &H20UI) ' DOS archive attribute
        WriteUInt32(dataInBed, 42, entryInBed.LocalOffset)
        Buffer.BlockCopy(nameBytesInBed, 0, dataInBed, 46, nameBytesInBed.Length)
        Return dataInBed
    End Function

    Private Shared Function MakeEndRecord(thisDiskInBed As UShort,
                                          centralStartDiskInBed As UShort,
                                          entriesThisDiskInBed As UShort,
                                          totalEntriesInBed As UShort,
                                          centralSizeInBed As UInteger,
                                          centralOffsetInBed As UInteger) As Byte()
        Dim dataInBed(21) As Byte
        WriteUInt32(dataInBed, 0, EndSignature)
        WriteUInt16(dataInBed, 4, thisDiskInBed)
        WriteUInt16(dataInBed, 6, centralStartDiskInBed)
        WriteUInt16(dataInBed, 8, entriesThisDiskInBed)
        WriteUInt16(dataInBed, 10, totalEntriesInBed)
        WriteUInt32(dataInBed, 12, centralSizeInBed)
        WriteUInt32(dataInBed, 16, centralOffsetInBed)
        WriteUInt16(dataInBed, 20, 0US)
        Return dataInBed
    End Function

    Private Shared Function MakeUInt32Bytes(valueInBed As UInteger) As Byte()
        Dim dataInBed(3) As Byte
        WriteUInt32(dataInBed, 0, valueInBed)
        Return dataInBed
    End Function

    Private Shared Function MakeDosZipFileName(valueInBed As String) As String
        Dim baseInBed As String = CleanDosPart(Path.GetFileNameWithoutExtension(valueInBed))
        If baseInBed.Length = 0 Then baseInBed = "SPAN"
        If baseInBed.Length > 8 Then baseInBed = baseInBed.Substring(0, 8)
        Return baseInBed & ".ZIP"
    End Function

    Private Shared Function MakeUniqueDosArchiveName(fileNameInBed As String, usedInBed As HashSet(Of String)) As String
        Dim baseInBed As String = CleanDosPart(Path.GetFileNameWithoutExtension(fileNameInBed))
        Dim extInBed As String = CleanDosPart(Path.GetExtension(fileNameInBed).TrimStart("."c))
        If baseInBed.Length = 0 Then baseInBed = "FILE"
        If baseInBed.Length > 8 Then baseInBed = baseInBed.Substring(0, 8)
        If extInBed.Length > 3 Then extInBed = extInBed.Substring(0, 3)

        For suffixInBed As Integer = 0 To 999999
            Dim candidateBaseInBed As String = baseInBed
            If suffixInBed > 0 Then
                Dim tailInBed As String = "~" & suffixInBed.ToString()
                candidateBaseInBed = baseInBed.Substring(0, Math.Min(baseInBed.Length, Math.Max(1, 8 - tailInBed.Length))) & tailInBed
            End If
            Dim candidateInBed As String = candidateBaseInBed & If(extInBed.Length > 0, "." & extInBed, String.Empty)
            If usedInBed.Add(candidateInBed) Then Return candidateInBed
        Next
        Throw New IOException("Could not create a unique DOS archive name for " & fileNameInBed & ".")
    End Function

    Private Shared Function CleanDosPart(valueInBed As String) As String
        Dim builderInBed As New StringBuilder()
        For Each chInBed As Char In If(valueInBed, String.Empty).ToUpperInvariant()
            If (chInBed >= "A"c AndAlso chInBed <= "Z"c) OrElse
               (chInBed >= "0"c AndAlso chInBed <= "9"c) OrElse
               "_-$~!#%&'()@^{}".IndexOf(chInBed) >= 0 Then
                builderInBed.Append(chInBed)
            ElseIf chInBed <> " "c AndAlso chInBed <> "."c Then
                builderInBed.Append("_"c)
            End If
        Next
        Return builderInBed.ToString()
    End Function

    Private Shared Sub ToDosDateTime(valueInBed As DateTime, ByRef timeInBed As UShort, ByRef dateInBed As UShort)
        Dim safeInBed As DateTime = valueInBed
        If safeInBed.Year < 1980 Then safeInBed = New DateTime(1980, 1, 1)
        If safeInBed.Year > 2107 Then safeInBed = New DateTime(2107, 12, 31, 23, 59, 58)
        timeInBed = CUShort((safeInBed.Hour << 11) Or (safeInBed.Minute << 5) Or (safeInBed.Second \ 2))
        dateInBed = CUShort(((safeInBed.Year - 1980) << 9) Or (safeInBed.Month << 5) Or safeInBed.Day)
    End Sub

    Private Shared Function BuildCrcTable() As UInteger()
        Dim tableInBed(255) As UInteger
        For indexInBed As Integer = 0 To 255
            Dim valueInBed As UInteger = CUInt(indexInBed)
            For bitInBed As Integer = 0 To 7
                If (valueInBed And 1UI) <> 0UI Then
                    valueInBed = (valueInBed >> 1) Xor &HEDB88320UI
                Else
                    valueInBed >>= 1
                End If
            Next
            tableInBed(indexInBed) = valueInBed
        Next
        Return tableInBed
    End Function

    Private Shared Function UpdateCrc(crcInBed As UInteger, bufferInBed As Byte(), countInBed As Integer) As UInteger
        Dim valueInBed As UInteger = crcInBed
        For indexInBed As Integer = 0 To countInBed - 1
            valueInBed = (valueInBed >> 8) Xor CrcTableInBed(CInt((valueInBed Xor bufferInBed(indexInBed)) And &HFFUI))
        Next
        Return valueInBed
    End Function

    Private Shared Sub WriteUInt16(targetInBed As Byte(), offsetInBed As Integer, valueInBed As UShort)
        targetInBed(offsetInBed) = CByte(valueInBed And &HFFUS)
        targetInBed(offsetInBed + 1) = CByte((valueInBed >> 8) And &HFFUS)
    End Sub

    Private Shared Sub WriteUInt32(targetInBed As Byte(), offsetInBed As Integer, valueInBed As UInteger)
        targetInBed(offsetInBed) = CByte(valueInBed And &HFFUI)
        targetInBed(offsetInBed + 1) = CByte((valueInBed >> 8) And &HFFUI)
        targetInBed(offsetInBed + 2) = CByte((valueInBed >> 16) And &HFFUI)
        targetInBed(offsetInBed + 3) = CByte((valueInBed >> 24) And &HFFUI)
    End Sub
End Class

' ============================================================================
' CROMWELL TECHNOLOGIES SNEAKER NET WORKBENCH - UI PROTOTYPE BRICK 1
' Host-side only.  This form never bypasses the emulated floppy controller.
' ============================================================================

Public NotInheritable Class SneakerNetDiskEntry
    Friend Sub New(nameInBed As String,
                   attributesInBed As Byte,
                   sizeInBed As Long,
                   firstClusterInBed As Integer,
                   directoryOffsetInBed As Integer,
                   modifiedInBed As DateTime)
        Name = nameInBed
        Attributes = attributesInBed
        Size = sizeInBed
        FirstCluster = firstClusterInBed
        DirectoryOffset = directoryOffsetInBed
        Modified = modifiedInBed
    End Sub

    Public ReadOnly Property Name As String
    Public ReadOnly Property Attributes As Byte
    Public ReadOnly Property Size As Long
    Public ReadOnly Property FirstCluster As Integer
    Public ReadOnly Property Modified As DateTime
    Friend ReadOnly Property DirectoryOffset As Integer

    Public ReadOnly Property IsDirectory As Boolean
        Get
            Return (Attributes And &H10) <> 0
        End Get
    End Property

    Public ReadOnly Property AttributeText As String
        Get
            Dim textInBed As New StringBuilder()
            If (Attributes And &H1) <> 0 Then textInBed.Append("R")
            If (Attributes And &H2) <> 0 Then textInBed.Append("H")
            If (Attributes And &H4) <> 0 Then textInBed.Append("S")
            If (Attributes And &H10) <> 0 Then textInBed.Append("D")
            If (Attributes And &H20) <> 0 Then textInBed.Append("A")
            Return textInBed.ToString()
        End Get
    End Property
End Class

Public NotInheritable Class SneakerNetVerificationResult
    Public Sub New(okInBed As Boolean, linesInBed As IEnumerable(Of String))
        IsValid = okInBed
        Lines = New List(Of String)(linesInBed)
    End Sub

    Public ReadOnly Property IsValid As Boolean
    Public ReadOnly Property Lines As List(Of String)
End Class

Friend NotInheritable Class SneakerNetPayload
    Public Property Name As String
    Public Property Attributes As Byte
    Public Property Data As Byte()
    Public Property Modified As DateTime
End Class

Public NotInheritable Class Fat12ImageDocument
    Private _path As String
    Private ReadOnly _allowPhysicalSectorClamp As Boolean
    Private _image As Byte()
    Private _bytesPerSector As Integer
    Private _sectorsPerCluster As Integer
    Private _reservedSectors As Integer
    Private _fatCount As Integer
    Private _rootEntries As Integer
    Private _totalSectors As Integer
    Private _sectorsPerFat As Integer
    Private _sectorsPerTrack As Integer
    Private _heads As Integer
    Private _fatStartOffset As Integer
    Private _rootStartOffset As Integer
    Private _rootDirectoryBytes As Integer
    Private _dataStartOffset As Integer
    Private _dataClusterCount As Integer
    Private _volumeLabel As String

    Public Sub New(pathInBed As String)
        Me.New(pathInBed, False)
    End Sub

    Friend Sub New(pathInBed As String, allowPhysicalSectorClampInBed As Boolean)
        If String.IsNullOrWhiteSpace(pathInBed) Then Throw New ArgumentException("An image path is required.", NameOf(pathInBed))
        _path = Path.GetFullPath(pathInBed)
        _allowPhysicalSectorClamp = allowPhysicalSectorClampInBed
        Reload()
    End Sub

    Public ReadOnly Property ImagePath As String
        Get
            Return _path
        End Get
    End Property

    Public Function RenameImageFile(requestedFileNameInBed As String) As String
        Dim requestedInBed As String = If(requestedFileNameInBed, String.Empty).Trim()
        If requestedInBed.Length = 0 Then Throw New InvalidOperationException("Enter an image filename.")

        Dim fileNameInBed As String = Path.GetFileName(requestedInBed)
        If fileNameInBed.Length = 0 OrElse fileNameInBed = "." OrElse fileNameInBed = ".." Then
            Throw New InvalidOperationException("Enter a valid image filename.")
        End If
        For Each badInBed As Char In Path.GetInvalidFileNameChars()
            If fileNameInBed.IndexOf(badInBed) >= 0 Then
                Throw New InvalidOperationException("The image filename contains an invalid character.")
            End If
        Next

        Dim oldExtensionInBed As String = Path.GetExtension(_path)
        Dim extensionInBed As String = Path.GetExtension(fileNameInBed)
        If String.IsNullOrWhiteSpace(extensionInBed) Then
            fileNameInBed &= oldExtensionInBed
            extensionInBed = oldExtensionInBed
        End If
        If Not extensionInBed.Equals(".img", StringComparison.OrdinalIgnoreCase) AndAlso
           Not extensionInBed.Equals(".ima", StringComparison.OrdinalIgnoreCase) Then
            Throw New InvalidOperationException("Sneaker Net image filenames must end in .img or .ima.")
        End If

        Dim directoryInBed As String = Path.GetDirectoryName(_path)
        Dim destinationInBed As String = Path.Combine(directoryInBed, fileNameInBed)
        If destinationInBed.Equals(_path, StringComparison.Ordinal) Then Return _path

        If destinationInBed.Equals(_path, StringComparison.OrdinalIgnoreCase) Then
            ' Windows treats case-only renames as the same path.  Hop through a
            ' temporary name so the requested casing still becomes real on disk.
            Dim temporaryInBed As String = Path.Combine(
                directoryInBed,
                ".sneakernet-rename-" & Guid.NewGuid().ToString("N") & oldExtensionInBed)
            File.Move(_path, temporaryInBed)
            Try
                File.Move(temporaryInBed, destinationInBed)
            Catch
                Try
                    If File.Exists(temporaryInBed) Then File.Move(temporaryInBed, _path)
                Catch
                End Try
                Throw
            End Try
        Else
            If File.Exists(destinationInBed) Then
                Throw New IOException(Path.GetFileName(destinationInBed) & " already exists.")
            End If
            File.Move(_path, destinationInBed)
        End If

        _path = destinationInBed
        Return _path
    End Function

    Public ReadOnly Property VolumeLabel As String
        Get
            Return _volumeLabel
        End Get
    End Property

    Public ReadOnly Property TotalBytes As Long
        Get
            Return _image.LongLength
        End Get
    End Property

    Public ReadOnly Property FreeBytes As Long
        Get
            Dim freeClustersInBed As Integer = 0
            For clusterInBed As Integer = 2 To _dataClusterCount + 1
                If ReadFatEntry(clusterInBed) = 0 Then freeClustersInBed += 1
            Next
            Return CLng(freeClustersInBed) * _sectorsPerCluster * _bytesPerSector
        End Get
    End Property

    Public ReadOnly Property GeometryText As String
        Get
            Return (_totalSectors * _bytesPerSector \ 1024).ToString("N0") & " KB FAT12 • " &
                   _sectorsPerTrack.ToString() & " spt • " & _heads.ToString() & " head(s)"
        End Get
    End Property

    Public ReadOnly Property HasBootSignature As Boolean
        Get
            Return _image.Length >= 512 AndAlso _image(510) = &H55 AndAlso _image(511) = &HAA
        End Get
    End Property

    Public ReadOnly Property BootStatusText As String
        Get
            Dim namesInBed As New HashSet(Of String)(GetEntries().Select(Function(entryInBed) entryInBed.Name), StringComparer.OrdinalIgnoreCase)
            Dim msDosInBed As Boolean = namesInBed.Contains("IO.SYS") AndAlso namesInBed.Contains("MSDOS.SYS") AndAlso namesInBed.Contains("COMMAND.COM")
            Dim pcDosInBed As Boolean = namesInBed.Contains("IBMBIO.COM") AndAlso namesInBed.Contains("IBMDOS.COM") AndAlso namesInBed.Contains("COMMAND.COM")
            Dim transferStubInBed As Boolean = _image.Length > 64 AndAlso _image(62) = &HCD AndAlso _image(63) = &H18
            If HasBootSignature AndAlso (msDosInBed OrElse pcDosInBed) AndAlso Not transferStubInBed Then Return "DOS boot files installed"
            If transferStubInBed Then Return "transfer disk (INT 18h, not bootable)"
            If HasBootSignature Then Return "boot signature present; system files not recognized"
            Return "not marked bootable"
        End Get
    End Property

    Public Sub Reload()
        _image = File.ReadAllBytes(_path)
        ParseLayout()
    End Sub

    Public Sub Save()
        Using streamInBed As New FileStream(_path, FileMode.Open, FileAccess.Write, FileShare.Read)
            streamInBed.Position = 0
            streamInBed.Write(_image, 0, _image.Length)
            streamInBed.SetLength(_image.Length)
            streamInBed.Flush()
        End Using
    End Sub

    Public Function GetEntries(Optional directoryClusterInBed As Integer = 0) As List(Of SneakerNetDiskEntry)
        Dim resultInBed As New List(Of SneakerNetDiskEntry)()
        If directoryClusterInBed = 0 Then _volumeLabel = String.Empty

        If directoryClusterInBed = 0 Then
            For indexInBed As Integer = 0 To _rootEntries - 1
                Dim offsetInBed As Integer = _rootStartOffset + indexInBed * 32
                Dim endReachedInBed As Boolean = False
                Dim entryInBed As SneakerNetDiskEntry =
                    ReadVisibleDirectoryEntry(offsetInBed, True, endReachedInBed)
                If endReachedInBed Then Exit For
                If entryInBed IsNot Nothing Then resultInBed.Add(entryInBed)
            Next
            Return resultInBed
        End If

        ValidateDataCluster(directoryClusterInBed)
        Dim clusterInBed As Integer = directoryClusterInBed
        Dim visitedInBed As New HashSet(Of Integer)()
        Dim entriesPerClusterInBed As Integer = (_sectorsPerCluster * _bytesPerSector) \ 32

        Do
            ValidateDataCluster(clusterInBed)
            If Not visitedInBed.Add(clusterInBed) Then
                Throw New InvalidDataException("FAT12 directory cluster-chain loop detected.")
            End If

            Dim clusterOffsetInBed As Integer = ClusterOffset(clusterInBed)
            For slotInBed As Integer = 0 To entriesPerClusterInBed - 1
                Dim endReachedInBed As Boolean = False
                Dim entryInBed As SneakerNetDiskEntry =
                    ReadVisibleDirectoryEntry(clusterOffsetInBed + slotInBed * 32, False, endReachedInBed)
                If endReachedInBed Then Return resultInBed
                If entryInBed IsNot Nothing Then resultInBed.Add(entryInBed)
            Next

            Dim nextInBed As Integer = ReadFatEntry(clusterInBed)
            If nextInBed >= &HFF8 Then Exit Do
            If nextInBed < 2 Then Throw New InvalidDataException("FAT12 directory cluster chain ends unexpectedly.")
            clusterInBed = nextInBed
        Loop

        Return resultInBed
    End Function

    Private Function ReadVisibleDirectoryEntry(offsetInBed As Integer,
                                               isRootInBed As Boolean,
                                               ByRef endReachedInBed As Boolean) As SneakerNetDiskEntry
        endReachedInBed = False
        Dim firstInBed As Byte = _image(offsetInBed)
        If firstInBed = 0 Then
            endReachedInBed = True
            Return Nothing
        End If
        If firstInBed = &HE5 Then Return Nothing

        Dim attributesInBed As Byte = _image(offsetInBed + 11)
        If attributesInBed = &HF Then Return Nothing ' VFAT long-name fragment.

        Dim rawBaseInBed As String = Encoding.ASCII.GetString(_image, offsetInBed, 8).TrimEnd(" "c)
        Dim rawExtInBed As String = Encoding.ASCII.GetString(_image, offsetInBed + 8, 3).TrimEnd(" "c)
        If rawBaseInBed.Length = 0 Then Return Nothing

        Dim nameInBed As String = rawBaseInBed
        If rawExtInBed.Length > 0 Then nameInBed &= "." & rawExtInBed

        If nameInBed = "." OrElse nameInBed = ".." Then Return Nothing
        If (attributesInBed And &H8) <> 0 Then
            If isRootInBed Then _volumeLabel = (rawBaseInBed & rawExtInBed).Trim()
            Return Nothing
        End If

        Dim firstClusterInBed As Integer = ReadUInt16(_image, offsetInBed + 26)
        Dim sizeInBed As Long = ReadUInt32(_image, offsetInBed + 28)
        Dim modifiedInBed As DateTime = ReadDosDateTime(offsetInBed)
        Return New SneakerNetDiskEntry(
            nameInBed,
            attributesInBed,
            sizeInBed,
            firstClusterInBed,
            offsetInBed,
            modifiedInBed)
    End Function

    Public Function ReadFile(entryInBed As SneakerNetDiskEntry) As Byte()
        If entryInBed Is Nothing Then Throw New ArgumentNullException(NameOf(entryInBed))
        If entryInBed.IsDirectory Then Throw New InvalidOperationException("Directory extraction is not implemented in this prototype yet.")
        If entryInBed.Size = 0 Then Return Array.Empty(Of Byte)()
        If entryInBed.Size > Integer.MaxValue Then Throw New InvalidDataException("FAT12 file is unexpectedly large.")

        Dim resultInBed(CInt(entryInBed.Size) - 1) As Byte
        Dim remainingInBed As Integer = resultInBed.Length
        Dim destinationOffsetInBed As Integer = 0
        Dim clusterInBed As Integer = entryInBed.FirstCluster
        Dim visitedInBed As New HashSet(Of Integer)()
        Dim clusterBytesInBed As Integer = _sectorsPerCluster * _bytesPerSector

        While remainingInBed > 0
            ValidateDataCluster(clusterInBed)
            If Not visitedInBed.Add(clusterInBed) Then Throw New InvalidDataException("FAT12 cluster-chain loop detected in " & entryInBed.Name & ".")

            Dim sourceOffsetInBed As Integer = ClusterOffset(clusterInBed)
            Dim countInBed As Integer = Math.Min(clusterBytesInBed, remainingInBed)
            Buffer.BlockCopy(_image, sourceOffsetInBed, resultInBed, destinationOffsetInBed, countInBed)
            destinationOffsetInBed += countInBed
            remainingInBed -= countInBed
            If remainingInBed <= 0 Then Exit While

            Dim nextInBed As Integer = ReadFatEntry(clusterInBed)
            If nextInBed >= &HFF8 Then Throw New InvalidDataException("FAT12 cluster chain ends before the recorded file size for " & entryInBed.Name & ".")
            clusterInBed = nextInBed
        End While

        Return resultInBed
    End Function

    Public Sub ExtractFile(entryInBed As SneakerNetDiskEntry, destinationDirectoryInBed As String)
        If entryInBed Is Nothing Then Return
        If entryInBed.IsDirectory Then
            ExtractEntry(entryInBed, destinationDirectoryInBed)
            Return
        End If
        Directory.CreateDirectory(destinationDirectoryInBed)
        Dim destinationInBed As String = CreateUniqueHostPath(destinationDirectoryInBed, entryInBed.Name)
        File.WriteAllBytes(destinationInBed, ReadFile(entryInBed))
        Try
            File.SetLastWriteTime(destinationInBed, entryInBed.Modified)
        Catch
        End Try
    End Sub

    Public Sub ExtractEntry(entryInBed As SneakerNetDiskEntry, destinationDirectoryInBed As String)
        If entryInBed Is Nothing Then Return
        Directory.CreateDirectory(destinationDirectoryInBed)

        If Not entryInBed.IsDirectory Then
            ExtractFile(entryInBed, destinationDirectoryInBed)
            Return
        End If

        Dim destinationInBed As String = CreateUniqueHostDirectoryPath(destinationDirectoryInBed, entryInBed.Name)
        Directory.CreateDirectory(destinationInBed)
        For Each childInBed As SneakerNetDiskEntry In GetEntries(entryInBed.FirstCluster)
            ExtractEntry(childInBed, destinationInBed)
        Next
        Try
            Directory.SetLastWriteTime(destinationInBed, entryInBed.Modified)
        Catch
        End Try
    End Sub

    Public Function AddHostFiles(pathsInBed As IEnumerable(Of String)) As List(Of String)
        Return AddHostPaths(pathsInBed, 0)
    End Function

    Public Function AddHostPaths(pathsInBed As IEnumerable(Of String),
                                 Optional directoryClusterInBed As Integer = 0) As List(Of String)
        Dim addedInBed As New List(Of String)()
        Dim normalizedPathsInBed As String() = pathsInBed.
            Where(Function(pathInBed) Not String.IsNullOrWhiteSpace(pathInBed) AndAlso
                  (File.Exists(pathInBed) OrElse Directory.Exists(pathInBed))).
            Select(Function(pathInBed) Path.GetFullPath(pathInBed)).
            Distinct(StringComparer.OrdinalIgnoreCase).
            ToArray()

        For Each sourceInBed As String In normalizedPathsInBed
            AddHostPathToDirectory(sourceInBed, directoryClusterInBed, addedInBed)
        Next
        Return addedInBed
    End Function

    Private Sub AddHostPathToDirectory(sourceInBed As String,
                                       targetDirectoryClusterInBed As Integer,
                                       addedInBed As List(Of String))
        If File.Exists(sourceInBed) Then
            Dim infoInBed As New FileInfo(sourceInBed)
            Dim fileEntriesInBed As List(Of SneakerNetDiskEntry) = GetEntries(targetDirectoryClusterInBed)
            Dim existingNamesInBed As New HashSet(Of String)(
                fileEntriesInBed.Select(Function(entryInBed) entryInBed.Name),
                StringComparer.OrdinalIgnoreCase)
            Dim normalizedInBed As String = NormalizeShortName(infoInBed.Name)
            Dim inputAlreadyShortInBed As Boolean =
                normalizedInBed.Length > 0 AndAlso infoInBed.Name.Equals(normalizedInBed, StringComparison.OrdinalIgnoreCase)
            Dim desiredInBed As String =
                If(inputAlreadyShortInBed,
                   normalizedInBed,
                   MakeUniqueShortName(infoInBed.Name, existingNamesInBed))

            Dim existingInBed As SneakerNetDiskEntry =
                fileEntriesInBed.FirstOrDefault(Function(entryInBed) entryInBed.Name.Equals(desiredInBed, StringComparison.OrdinalIgnoreCase))
            If existingInBed IsNot Nothing AndAlso existingInBed.IsDirectory Then
                desiredInBed = MakeUniqueShortName(infoInBed.Name, existingNamesInBed)
            End If

            AddOrReplaceBytesInDirectory(
                targetDirectoryClusterInBed,
                desiredInBed,
                File.ReadAllBytes(sourceInBed),
                &H20,
                infoInBed.LastWriteTime,
                replaceExisting:=True)
            addedInBed.Add(desiredInBed)
            Return
        End If

        If Not Directory.Exists(sourceInBed) Then Return
        Dim directoryInfoInBed As New DirectoryInfo(sourceInBed)
        If (directoryInfoInBed.Attributes And FileAttributes.ReparsePoint) <> 0 Then
            Throw New IOException("Sneaker Net will not follow reparse-point directory " & directoryInfoInBed.FullName & ".")
        End If

        Dim entriesInBed As List(Of SneakerNetDiskEntry) = GetEntries(targetDirectoryClusterInBed)
        Dim usedNamesInBed As New HashSet(Of String)(
            entriesInBed.Select(Function(entryInBed) entryInBed.Name),
            StringComparer.OrdinalIgnoreCase)
        Dim normalizedDirectoryNameInBed As String = NormalizeShortName(directoryInfoInBed.Name)
        Dim desiredDirectoryNameInBed As String =
            If(normalizedDirectoryNameInBed.Length > 0 AndAlso
               directoryInfoInBed.Name.Equals(normalizedDirectoryNameInBed, StringComparison.OrdinalIgnoreCase),
               normalizedDirectoryNameInBed,
               MakeUniqueShortName(directoryInfoInBed.Name, usedNamesInBed))

        Dim existingDirectoryInBed As SneakerNetDiskEntry =
            entriesInBed.FirstOrDefault(
                Function(entryInBed) entryInBed.Name.Equals(desiredDirectoryNameInBed, StringComparison.OrdinalIgnoreCase))

        Dim childClusterInBed As Integer
        If existingDirectoryInBed IsNot Nothing AndAlso existingDirectoryInBed.IsDirectory Then
            childClusterInBed = existingDirectoryInBed.FirstCluster
        Else
            If existingDirectoryInBed IsNot Nothing Then
                desiredDirectoryNameInBed = MakeUniqueShortName(directoryInfoInBed.Name, usedNamesInBed)
            End If
            childClusterInBed =
                CreateSubdirectory(targetDirectoryClusterInBed, desiredDirectoryNameInBed, directoryInfoInBed.LastWriteTime)
            addedInBed.Add(desiredDirectoryNameInBed & "\")
        End If

        For Each childDirectoryInBed As String In Directory.GetDirectories(sourceInBed)
            AddHostPathToDirectory(childDirectoryInBed, childClusterInBed, addedInBed)
        Next
        For Each childFileInBed As String In Directory.GetFiles(sourceInBed)
            AddHostPathToDirectory(childFileInBed, childClusterInBed, addedInBed)
        Next
    End Sub

    Public Function CanAddHostFiles(pathsInBed As IEnumerable(Of String)) As Boolean
        Return CanAddHostPaths(pathsInBed, 0)
    End Function

    Public Function CanAddHostPaths(pathsInBed As IEnumerable(Of String),
                                    Optional directoryClusterInBed As Integer = 0) As Boolean
        Dim snapshotInBed As Byte() = DirectCast(_image.Clone(), Byte())
        Try
            AddHostPaths(pathsInBed, directoryClusterInBed)
            Return True
        Catch
            Return False
        Finally
            _image = snapshotInBed
            ParseLayout()
        End Try
    End Function

    Public Sub DeleteEntry(entryInBed As SneakerNetDiskEntry,
                           Optional parentDirectoryClusterInBed As Integer = 0)
        If entryInBed Is Nothing Then Return
        If entryInBed.IsDirectory Then
            DeleteDirectoryTree(entryInBed.FirstCluster, New HashSet(Of Integer)())
        Else
            FreeClusterChain(entryInBed.FirstCluster)
        End If
        _image(entryInBed.DirectoryOffset) = &HE5
    End Sub

    Private Sub DeleteDirectoryTree(directoryClusterInBed As Integer,
                                    visitedDirectoriesInBed As HashSet(Of Integer))
        ValidateDataCluster(directoryClusterInBed)
        If Not visitedDirectoriesInBed.Add(directoryClusterInBed) Then
            Throw New InvalidDataException("FAT12 directory loop detected while deleting.")
        End If
        For Each childInBed As SneakerNetDiskEntry In GetEntries(directoryClusterInBed)
            If childInBed.IsDirectory Then
                DeleteDirectoryTree(childInBed.FirstCluster, visitedDirectoriesInBed)
            Else
                FreeClusterChain(childInBed.FirstCluster)
            End If
            _image(childInBed.DirectoryOffset) = &HE5
        Next
        FreeClusterChain(directoryClusterInBed)
    End Sub

    Public Sub RenameEntry(entryInBed As SneakerNetDiskEntry,
                           requestedNameInBed As String,
                           Optional parentDirectoryClusterInBed As Integer = 0)
        If entryInBed Is Nothing Then Return
        Dim normalizedInBed As String = NormalizeShortName(requestedNameInBed)
        If normalizedInBed.Length = 0 Then Throw New InvalidOperationException("A DOS 8.3 name is required.")

        For Each otherInBed As SneakerNetDiskEntry In GetEntries(parentDirectoryClusterInBed)
            If otherInBed.DirectoryOffset <> entryInBed.DirectoryOffset AndAlso
               otherInBed.Name.Equals(normalizedInBed, StringComparison.OrdinalIgnoreCase) Then
                Throw New IOException(normalizedInBed & " already exists in this directory.")
            End If
        Next

        WriteShortName(entryInBed.DirectoryOffset, normalizedInBed)
    End Sub

    Public Function Verify() As SneakerNetVerificationResult
        Dim linesInBed As New List(Of String)()
        Dim okInBed As Boolean = True

        linesInBed.Add("✓ BPB describes a FAT12-sized disk")
        If HasBootSignature Then
            linesInBed.Add("✓ 55 AA boot signature present")
        Else
            linesInBed.Add("• No 55 AA boot signature")
        End If

        If _fatCount >= 2 Then
            Dim fatBytesInBed As Integer = _sectorsPerFat * _bytesPerSector
            For indexInBed As Integer = 0 To fatBytesInBed - 1
                If _image(_fatStartOffset + indexInBed) <> _image(_fatStartOffset + fatBytesInBed + indexInBed) Then
                    okInBed = False
                    linesInBed.Add("✗ FAT copies differ")
                    Exit For
                End If
            Next
            If okInBed Then linesInBed.Add("✓ FAT copies agree")
        End If

        Dim ownerByClusterInBed As New Dictionary(Of Integer, String)()
        For Each entryInBed As SneakerNetDiskEntry In GetEntries()
            If entryInBed.IsDirectory OrElse entryInBed.Size = 0 Then Continue For
            Try
                Dim clusterInBed As Integer = entryInBed.FirstCluster
                Dim remainingInBed As Long = entryInBed.Size
                Dim clusterBytesInBed As Integer = _sectorsPerCluster * _bytesPerSector
                Dim visitedInBed As New HashSet(Of Integer)()
                While remainingInBed > 0
                    ValidateDataCluster(clusterInBed)
                    If Not visitedInBed.Add(clusterInBed) Then Throw New InvalidDataException("cluster loop")
                    If ownerByClusterInBed.ContainsKey(clusterInBed) Then
                        Throw New InvalidDataException("cross-linked cluster " & clusterInBed.ToString() & " with " & ownerByClusterInBed(clusterInBed))
                    End If
                    ownerByClusterInBed(clusterInBed) = entryInBed.Name
                    remainingInBed -= clusterBytesInBed
                    If remainingInBed <= 0 Then Exit While
                    Dim nextInBed As Integer = ReadFatEntry(clusterInBed)
                    If nextInBed >= &HFF8 Then Throw New InvalidDataException("chain ends early")
                    clusterInBed = nextInBed
                End While
            Catch ex As Exception
                okInBed = False
                linesInBed.Add("✗ " & entryInBed.Name & ": " & ex.Message)
            End Try
        Next

        If okInBed Then linesInBed.Add("✓ Root files have sane, non-cross-linked cluster chains")
        linesInBed.Add("• " & FreeBytes.ToString("N0") & " bytes free")
        Return New SneakerNetVerificationResult(okInBed, linesInBed)
    End Function

    Public Sub InstallDosBootFromSourceImage(sourcePathInBed As String)
        ' A donor is read-only source material.  Some legitimate archived floppy
        ' images carry a stale BPB total-sector count even though their raw byte
        ' length is a standard floppy geometry.  Be tolerant only for the donor;
        ' normal editable images remain strict.
        Dim sourceInBed As New Fat12ImageDocument(sourcePathInBed, True)
        If Not sourceInBed.HasBootSignature Then Throw New InvalidDataException("The source image does not contain a 55 AA boot signature.")

        Dim sourceEntriesInBed As List(Of SneakerNetDiskEntry) = sourceInBed.GetEntries()
        Dim sourceByNameInBed As New Dictionary(Of String, SneakerNetDiskEntry)(StringComparer.OrdinalIgnoreCase)
        For Each entryInBed As SneakerNetDiskEntry In sourceEntriesInBed
            sourceByNameInBed(entryInBed.Name) = entryInBed
        Next

        Dim requiredInBed As String()
        If sourceByNameInBed.ContainsKey("IO.SYS") AndAlso sourceByNameInBed.ContainsKey("MSDOS.SYS") Then
            requiredInBed = {"IO.SYS", "MSDOS.SYS", "COMMAND.COM"}
        ElseIf sourceByNameInBed.ContainsKey("IBMBIO.COM") AndAlso sourceByNameInBed.ContainsKey("IBMDOS.COM") Then
            requiredInBed = {"IBMBIO.COM", "IBMDOS.COM", "COMMAND.COM"}
        Else
            Throw New InvalidDataException("Source does not look like an MS-DOS/PC DOS boot floppy. Expected IO.SYS + MSDOS.SYS or IBMBIO.COM + IBMDOS.COM.")
        End If
        For Each nameInBed As String In requiredInBed
            If Not sourceByNameInBed.ContainsKey(nameInBed) Then Throw New InvalidDataException("Source boot image is missing " & nameInBed & ".")
        Next

        Dim currentEntriesInBed As List(Of SneakerNetDiskEntry) = GetEntries()
        Dim originalLabelInBed As String = _volumeLabel
        Dim currentPayloadsInBed As New List(Of SneakerNetPayload)()
        For Each entryInBed As SneakerNetDiskEntry In currentEntriesInBed
            If entryInBed.IsDirectory Then
                Throw New InvalidOperationException("Make Bootable currently rebuilds the root directory and will not touch an image containing subdirectories. Use a fresh disk for this prototype.")
            End If
            If requiredInBed.Contains(entryInBed.Name, StringComparer.OrdinalIgnoreCase) Then Continue For
            currentPayloadsInBed.Add(New SneakerNetPayload With {
                .Name = entryInBed.Name,
                .Attributes = entryInBed.Attributes,
                .Data = ReadFile(entryInBed),
                .Modified = entryInBed.Modified
            })
        Next

        Dim formatInBed As Fat12FloppyFormat = FormatForCurrentImage()
        Dim tempInBed As String = _path & ".sneakernet.blank." & Guid.NewGuid().ToString("N") & ".tmp"
        Try
            Fat12FloppyBuilder.CreateImage(Array.Empty(Of String)(), tempInBed, If(String.IsNullOrWhiteSpace(originalLabelInBed), "SNEAKERNET", originalLabelInBed), formatInBed)
            Dim blankInBed As Byte() = File.ReadAllBytes(tempInBed)

            ' Keep the target geometry/BPB but transplant the source boot loader.
            Buffer.BlockCopy(sourceInBed._image, 0, blankInBed, 0, 11)
            Dim sourceHasExtendedBpbInBed As Boolean = sourceInBed._image.Length > 62 AndAlso (sourceInBed._image(38) = &H28 OrElse sourceInBed._image(38) = &H29)
            Dim codeStartInBed As Integer = If(sourceHasExtendedBpbInBed, 62, 30)
            Buffer.BlockCopy(sourceInBed._image, codeStartInBed, blankInBed, codeStartInBed, 510 - codeStartInBed)
            blankInBed(510) = &H55
            blankInBed(511) = &HAA

            _image = blankInBed
            ParseLayout()
            ClearRootDirectory()

            ' DOS versions which care about physical placement expect the system
            ' files first.  Empty media gives them the first root entries and the
            ' first contiguous data clusters.
            For Each nameInBed As String In requiredInBed
                Dim sourceEntryInBed As SneakerNetDiskEntry = sourceByNameInBed(nameInBed)
                AddOrReplaceBytes(
                    nameInBed,
                    sourceInBed.ReadFile(sourceEntryInBed),
                    sourceEntryInBed.Attributes,
                    sourceEntryInBed.Modified,
                    replaceExisting:=True)
            Next

            For Each payloadInBed As SneakerNetPayload In currentPayloadsInBed
                AddOrReplaceBytes(payloadInBed.Name, payloadInBed.Data, payloadInBed.Attributes, payloadInBed.Modified, replaceExisting:=True)
            Next

            WriteVolumeLabelEntry(If(String.IsNullOrWhiteSpace(originalLabelInBed), "SNEAKERNET", originalLabelInBed))
            Save()
            Reload()
        Finally
            Try
                If File.Exists(tempInBed) Then File.Delete(tempInBed)
            Catch
            End Try
        End Try
    End Sub

    Private Shared Function CreateUniqueHostPath(directoryInBed As String, fileNameInBed As String) As String
        Dim candidateInBed As String = Path.Combine(directoryInBed, fileNameInBed)
        If Not File.Exists(candidateInBed) Then Return candidateInBed
        Dim baseInBed As String = Path.GetFileNameWithoutExtension(fileNameInBed)
        Dim extInBed As String = Path.GetExtension(fileNameInBed)
        For suffixInBed As Integer = 2 To 999999
            candidateInBed = Path.Combine(directoryInBed, baseInBed & " (" & suffixInBed.ToString() & ")" & extInBed)
            If Not File.Exists(candidateInBed) Then Return candidateInBed
        Next
        Throw New IOException("Could not create a unique host filename for " & fileNameInBed & ".")
    End Function

    Private Shared Function CreateUniqueHostDirectoryPath(directoryInBed As String, directoryNameInBed As String) As String
        Dim candidateInBed As String = Path.Combine(directoryInBed, directoryNameInBed)
        If Not Directory.Exists(candidateInBed) AndAlso Not File.Exists(candidateInBed) Then Return candidateInBed
        For suffixInBed As Integer = 2 To 999999
            candidateInBed = Path.Combine(directoryInBed, directoryNameInBed & " (" & suffixInBed.ToString() & ")")
            If Not Directory.Exists(candidateInBed) AndAlso Not File.Exists(candidateInBed) Then Return candidateInBed
        Next
        Throw New IOException("Could not create a unique host directory for " & directoryNameInBed & ".")
    End Function

    Private Sub ParseLayout()
        If _image Is Nothing OrElse _image.Length < 512 Then Throw New InvalidDataException("Image is too small to contain a FAT12 boot sector.")
        _bytesPerSector = ReadUInt16(_image, 11)
        _sectorsPerCluster = _image(13)
        _reservedSectors = ReadUInt16(_image, 14)
        _fatCount = _image(16)
        _rootEntries = ReadUInt16(_image, 17)
        Dim smallTotalInBed As Integer = ReadUInt16(_image, 19)
        _totalSectors = If(smallTotalInBed <> 0, smallTotalInBed, CInt(ReadUInt32(_image, 32)))
        _sectorsPerFat = ReadUInt16(_image, 22)
        _sectorsPerTrack = ReadUInt16(_image, 24)
        _heads = ReadUInt16(_image, 26)

        If _bytesPerSector <> 512 Then Throw New InvalidDataException("Sneaker Net currently supports 512-byte FAT12 sectors only.")
        If _sectorsPerCluster <= 0 OrElse _reservedSectors <= 0 OrElse _fatCount <= 0 OrElse _rootEntries <= 0 OrElse _sectorsPerFat <= 0 Then
            Throw New InvalidDataException("The BPB does not describe a conventional FAT12 floppy.")
        End If
        If _totalSectors <= 0 Then
            Throw New InvalidDataException("The BPB does not contain a valid total-sector count.")
        End If

        Dim claimedBytesInBed As Long = CLng(_totalSectors) * _bytesPerSector
        If claimedBytesInBed > _image.LongLength Then
            Dim rawGeometryInBed As Boolean =
                (_image.LongLength Mod _bytesPerSector) = 0 AndAlso
                Fat12FloppyBuilder.GetFormats().Any(
                    Function(formatInBed) CLng(formatInBed.TotalSectors) * _bytesPerSector = _image.LongLength)

            If _allowPhysicalSectorClamp AndAlso rawGeometryInBed Then
                _totalSectors = CInt(_image.LongLength \ _bytesPerSector)
            Else
                Throw New InvalidDataException(
                    "The BPB claims " & _totalSectors.ToString("N0") & " sectors (" &
                    claimedBytesInBed.ToString("N0") & " bytes), but the image contains only " &
                    _image.LongLength.ToString("N0") & " bytes.")
            End If
        End If

        _fatStartOffset = _reservedSectors * _bytesPerSector
        _rootStartOffset = (_reservedSectors + _fatCount * _sectorsPerFat) * _bytesPerSector
        _rootDirectoryBytes = ((_rootEntries * 32 + _bytesPerSector - 1) \ _bytesPerSector) * _bytesPerSector
        _dataStartOffset = _rootStartOffset + _rootDirectoryBytes
        Dim dataSectorsInBed As Integer = _totalSectors - (_dataStartOffset \ _bytesPerSector)
        _dataClusterCount = dataSectorsInBed \ _sectorsPerCluster
        If _dataClusterCount <= 0 OrElse _dataClusterCount >= 4085 Then Throw New InvalidDataException("The image does not describe a FAT12 data region.")

        GetEntries()
    End Sub

    Private Function FormatForCurrentImage() As Fat12FloppyFormat
        Return New Fat12FloppyFormat(
            GeometryText,
            _totalSectors,
            _sectorsPerCluster,
            _rootEntries,
            _image(21),
            _sectorsPerFat,
            _sectorsPerTrack,
            _heads)
    End Function

    Private Sub ClearRootDirectory()
        Array.Clear(_image, _rootStartOffset, _rootDirectoryBytes)
        _volumeLabel = String.Empty
    End Sub

    Private Sub WriteVolumeLabelEntry(labelInBed As String)
        Dim offsetInBed As Integer = FindFreeRootEntryOffset()
        Dim normalizedInBed As String = MakeVolumeLabel(labelInBed)
        WriteAscii(_image, offsetInBed, normalizedInBed, 11)
        _image(offsetInBed + 11) = &H8
        WriteDosDateTime(offsetInBed, DateTime.Now)
        _volumeLabel = normalizedInBed.Trim()
    End Sub

    Private Sub AddOrReplaceBytes(requestedNameInBed As String,
                                  dataInBed As Byte(),
                                  attributesInBed As Byte,
                                  modifiedInBed As DateTime,
                                  replaceExisting As Boolean)
        AddOrReplaceBytesInDirectory(
            0,
            requestedNameInBed,
            dataInBed,
            attributesInBed,
            modifiedInBed,
            replaceExisting)
    End Sub

    Private Sub AddOrReplaceBytesInDirectory(directoryClusterInBed As Integer,
                                             requestedNameInBed As String,
                                             dataInBed As Byte(),
                                             attributesInBed As Byte,
                                             modifiedInBed As DateTime,
                                             replaceExisting As Boolean)
        If dataInBed Is Nothing Then dataInBed = Array.Empty(Of Byte)()
        Dim entriesInBed As List(Of SneakerNetDiskEntry) = GetEntries(directoryClusterInBed)
        Dim normalizedInBed As String = NormalizeShortName(requestedNameInBed)
        If normalizedInBed.Length = 0 Then
            normalizedInBed = MakeUniqueShortName(requestedNameInBed, entriesInBed.Select(Function(entryInBed) entryInBed.Name))
        End If

        Dim existingInBed As SneakerNetDiskEntry =
            entriesInBed.FirstOrDefault(Function(entryInBed) entryInBed.Name.Equals(normalizedInBed, StringComparison.OrdinalIgnoreCase))
        Dim directoryOffsetInBed As Integer
        If existingInBed IsNot Nothing Then
            If Not replaceExisting Then
                normalizedInBed =
                    MakeUniqueShortName(normalizedInBed, entriesInBed.Select(Function(entryInBed) entryInBed.Name))
                existingInBed = Nothing
            ElseIf existingInBed.IsDirectory Then
                Throw New IOException(normalizedInBed & " is a directory.")
            End If
        End If

        If existingInBed IsNot Nothing Then
            FreeClusterChain(existingInBed.FirstCluster)
            directoryOffsetInBed = existingInBed.DirectoryOffset
            Array.Clear(_image, directoryOffsetInBed, 32)
        Else
            directoryOffsetInBed = FindFreeDirectoryEntryOffset(directoryClusterInBed)
        End If

        Dim clusterBytesInBed As Integer = _sectorsPerCluster * _bytesPerSector
        Dim clusterCountInBed As Integer =
            If(dataInBed.Length = 0, 0, (dataInBed.Length + clusterBytesInBed - 1) \ clusterBytesInBed)
        Dim clustersInBed As List(Of Integer) = FindFreeClusters(clusterCountInBed)
        Dim firstClusterInBed As Integer = If(clustersInBed.Count = 0, 0, clustersInBed(0))

        For indexInBed As Integer = 0 To clustersInBed.Count - 1
            Dim clusterInBed As Integer = clustersInBed(indexInBed)
            Dim offsetInBed As Integer = ClusterOffset(clusterInBed)
            Array.Clear(_image, offsetInBed, clusterBytesInBed)
            Dim sourceOffsetInBed As Integer = indexInBed * clusterBytesInBed
            Dim countInBed As Integer = Math.Min(clusterBytesInBed, dataInBed.Length - sourceOffsetInBed)
            If countInBed > 0 Then Buffer.BlockCopy(dataInBed, sourceOffsetInBed, _image, offsetInBed, countInBed)
            Dim nextValueInBed As Integer =
                If(indexInBed = clustersInBed.Count - 1, &HFFF, clustersInBed(indexInBed + 1))
            WriteFatEntry(clusterInBed, nextValueInBed)
        Next

        WriteShortName(directoryOffsetInBed, normalizedInBed)
        Dim safeAttributesInBed As Byte = CByte(attributesInBed And &H27)
        If safeAttributesInBed = 0 Then safeAttributesInBed = &H20
        _image(directoryOffsetInBed + 11) = safeAttributesInBed
        WriteDosDateTime(directoryOffsetInBed, modifiedInBed)
        WriteUInt16(_image, directoryOffsetInBed + 26, CUShort(firstClusterInBed))
        WriteUInt32(_image, directoryOffsetInBed + 28, CUInt(dataInBed.Length))
    End Sub

    Private Function CreateSubdirectory(parentDirectoryClusterInBed As Integer,
                                        directoryNameInBed As String,
                                        modifiedInBed As DateTime) As Integer
        Dim clusterInBed As Integer = FindFreeClusters(1)(0)
        WriteFatEntry(clusterInBed, &HFFF)
        Dim clusterBytesInBed As Integer = _sectorsPerCluster * _bytesPerSector
        Dim clusterOffsetInBed As Integer = ClusterOffset(clusterInBed)
        Array.Clear(_image, clusterOffsetInBed, clusterBytesInBed)

        Dim entryOffsetInBed As Integer = FindFreeDirectoryEntryOffset(parentDirectoryClusterInBed)
        WriteShortName(entryOffsetInBed, directoryNameInBed)
        _image(entryOffsetInBed + 11) = &H10
        WriteDosDateTime(entryOffsetInBed, modifiedInBed)
        WriteUInt16(_image, entryOffsetInBed + 26, CUShort(clusterInBed))
        WriteUInt32(_image, entryOffsetInBed + 28, 0UI)

        WriteDotDirectoryEntry(clusterOffsetInBed, ".", clusterInBed, modifiedInBed)
        WriteDotDirectoryEntry(clusterOffsetInBed + 32, "..", parentDirectoryClusterInBed, modifiedInBed)
        Return clusterInBed
    End Function

    Private Sub WriteDotDirectoryEntry(offsetInBed As Integer,
                                       dotNameInBed As String,
                                       targetClusterInBed As Integer,
                                       modifiedInBed As DateTime)
        WriteAscii(_image, offsetInBed, dotNameInBed.PadRight(11), 11)
        _image(offsetInBed + 11) = &H10
        WriteDosDateTime(offsetInBed, modifiedInBed)
        WriteUInt16(_image, offsetInBed + 26, CUShort(Math.Max(0, targetClusterInBed)))
        WriteUInt32(_image, offsetInBed + 28, 0UI)
    End Sub

    Private Function FindFreeClusters(countInBed As Integer) As List(Of Integer)
        Dim resultInBed As New List(Of Integer)()
        If countInBed <= 0 Then Return resultInBed
        For clusterInBed As Integer = 2 To _dataClusterCount + 1
            If ReadFatEntry(clusterInBed) = 0 Then
                resultInBed.Add(clusterInBed)
                If resultInBed.Count = countInBed Then Return resultInBed
            End If
        Next
        Throw New IOException("Not enough free space on the floppy image.")
    End Function

    Private Sub FreeClusterChain(firstClusterInBed As Integer)
        If firstClusterInBed < 2 Then Return
        Dim clusterInBed As Integer = firstClusterInBed
        Dim visitedInBed As New HashSet(Of Integer)()
        Do
            ValidateDataCluster(clusterInBed)
            If Not visitedInBed.Add(clusterInBed) Then Exit Do
            Dim nextInBed As Integer = ReadFatEntry(clusterInBed)
            WriteFatEntry(clusterInBed, 0)
            If nextInBed >= &HFF8 OrElse nextInBed < 2 Then Exit Do
            clusterInBed = nextInBed
        Loop
    End Sub

    Private Function FindFreeRootEntryOffset() As Integer
        For indexInBed As Integer = 0 To _rootEntries - 1
            Dim offsetInBed As Integer = _rootStartOffset + indexInBed * 32
            If _image(offsetInBed) = 0 OrElse _image(offsetInBed) = &HE5 Then Return offsetInBed
        Next
        Throw New IOException("The FAT12 root directory is full.")
    End Function

    Private Function FindFreeDirectoryEntryOffset(directoryClusterInBed As Integer) As Integer
        If directoryClusterInBed = 0 Then Return FindFreeRootEntryOffset()

        ValidateDataCluster(directoryClusterInBed)
        Dim clusterInBed As Integer = directoryClusterInBed
        Dim visitedInBed As New HashSet(Of Integer)()
        Dim entriesPerClusterInBed As Integer = (_sectorsPerCluster * _bytesPerSector) \ 32

        Do
            ValidateDataCluster(clusterInBed)
            If Not visitedInBed.Add(clusterInBed) Then
                Throw New InvalidDataException("FAT12 directory cluster-chain loop detected.")
            End If

            Dim clusterOffsetInBed As Integer = ClusterOffset(clusterInBed)
            For slotInBed As Integer = 0 To entriesPerClusterInBed - 1
                Dim offsetInBed As Integer = clusterOffsetInBed + slotInBed * 32
                If _image(offsetInBed) = 0 OrElse _image(offsetInBed) = &HE5 Then Return offsetInBed
            Next

            Dim nextInBed As Integer = ReadFatEntry(clusterInBed)
            If nextInBed >= &HFF8 Then
                Dim newClusterInBed As Integer = FindFreeClusters(1)(0)
                WriteFatEntry(clusterInBed, newClusterInBed)
                WriteFatEntry(newClusterInBed, &HFFF)
                Dim newOffsetInBed As Integer = ClusterOffset(newClusterInBed)
                Array.Clear(_image, newOffsetInBed, _sectorsPerCluster * _bytesPerSector)
                Return newOffsetInBed
            End If
            If nextInBed < 2 Then Throw New InvalidDataException("FAT12 directory chain ends unexpectedly.")
            clusterInBed = nextInBed
        Loop
    End Function

    Private Function ReadFatEntry(clusterInBed As Integer) As Integer
        Dim relativeInBed As Integer = clusterInBed + (clusterInBed \ 2)
        Dim b0InBed As Integer = _image(_fatStartOffset + relativeInBed)
        Dim b1InBed As Integer = _image(_fatStartOffset + relativeInBed + 1)
        If (clusterInBed And 1) = 0 Then Return b0InBed Or ((b1InBed And &HF) << 8)
        Return ((b0InBed >> 4) And &HF) Or (b1InBed << 4)
    End Function

    Private Sub WriteFatEntry(clusterInBed As Integer, valueInBed As Integer)
        valueInBed = valueInBed And &HFFF
        Dim relativeInBed As Integer = clusterInBed + (clusterInBed \ 2)
        Dim fatBytesInBed As Integer = _sectorsPerFat * _bytesPerSector
        For fatIndexInBed As Integer = 0 To _fatCount - 1
            Dim baseInBed As Integer = _fatStartOffset + fatIndexInBed * fatBytesInBed
            If (clusterInBed And 1) = 0 Then
                _image(baseInBed + relativeInBed) = CByte(valueInBed And &HFF)
                _image(baseInBed + relativeInBed + 1) = CByte((_image(baseInBed + relativeInBed + 1) And &HF0) Or ((valueInBed >> 8) And &HF))
            Else
                _image(baseInBed + relativeInBed) = CByte((_image(baseInBed + relativeInBed) And &HF) Or ((valueInBed << 4) And &HF0))
                _image(baseInBed + relativeInBed + 1) = CByte((valueInBed >> 4) And &HFF)
            End If
        Next
    End Sub

    Private Function ClusterOffset(clusterInBed As Integer) As Integer
        Return _dataStartOffset + (clusterInBed - 2) * _sectorsPerCluster * _bytesPerSector
    End Function

    Private Sub ValidateDataCluster(clusterInBed As Integer)
        If clusterInBed < 2 OrElse clusterInBed > _dataClusterCount + 1 Then Throw New InvalidDataException("FAT12 data cluster is outside the image: " & clusterInBed.ToString())
    End Sub

    Private Sub WriteShortName(offsetInBed As Integer, shortNameInBed As String)
        Dim piecesInBed As String() = shortNameInBed.Split("."c)
        Dim baseInBed As String = piecesInBed(0).PadRight(8).Substring(0, 8)
        Dim extInBed As String = If(piecesInBed.Length > 1, piecesInBed(1), String.Empty).PadRight(3).Substring(0, 3)
        WriteAscii(_image, offsetInBed, baseInBed, 8)
        WriteAscii(_image, offsetInBed + 8, extInBed, 3)
    End Sub

    Private Function NormalizeShortName(valueInBed As String) As String
        Dim inputInBed As String = If(valueInBed, String.Empty).Trim()
        If inputInBed.Length = 0 Then Return String.Empty
        Dim baseInBed As String = CleanDosPart(Path.GetFileNameWithoutExtension(inputInBed))
        Dim extInBed As String = CleanDosPart(Path.GetExtension(inputInBed).TrimStart("."c))
        If baseInBed.Length = 0 Then Return String.Empty
        If baseInBed.Length > 8 Then baseInBed = baseInBed.Substring(0, 8)
        If extInBed.Length > 3 Then extInBed = extInBed.Substring(0, 3)
        Return If(extInBed.Length = 0, baseInBed, baseInBed & "." & extInBed)
    End Function

    Private Function MakeUniqueShortName(fileNameInBed As String, usedNamesInBed As IEnumerable(Of String)) As String
        Dim usedInBed As New HashSet(Of String)(StringComparer.OrdinalIgnoreCase)
        If usedNamesInBed IsNot Nothing Then
            For Each nameInBed As String In usedNamesInBed
                usedInBed.Add(nameInBed)
            Next
        Else
            For Each entryInBed As SneakerNetDiskEntry In GetEntries()
                usedInBed.Add(entryInBed.Name)
            Next
        End If

        Dim baseInBed As String = CleanDosPart(Path.GetFileNameWithoutExtension(fileNameInBed))
        Dim extInBed As String = CleanDosPart(Path.GetExtension(fileNameInBed).TrimStart("."c))
        If baseInBed.Length = 0 Then baseInBed = "FILE"
        If baseInBed.Length > 8 Then baseInBed = baseInBed.Substring(0, 8)
        If extInBed.Length > 3 Then extInBed = extInBed.Substring(0, 3)
        Dim candidateInBed As String = If(extInBed.Length = 0, baseInBed, baseInBed & "." & extInBed)
        If usedInBed.Add(candidateInBed) Then Return candidateInBed

        For suffixInBed As Integer = 1 To 999999
            Dim suffixTextInBed As String = "~" & suffixInBed.ToString()
            Dim keepInBed As Integer = Math.Max(1, 8 - suffixTextInBed.Length)
            Dim collisionBaseInBed As String = baseInBed.Substring(0, Math.Min(baseInBed.Length, keepInBed)) & suffixTextInBed
            candidateInBed = If(extInBed.Length = 0, collisionBaseInBed, collisionBaseInBed & "." & extInBed)
            If usedInBed.Add(candidateInBed) Then Return candidateInBed
        Next
        Throw New IOException("Could not create a unique DOS 8.3 name for " & fileNameInBed & ".")
    End Function

    Private Shared Function CleanDosPart(valueInBed As String) As String
        Dim builderInBed As New StringBuilder()
        For Each chInBed As Char In If(valueInBed, String.Empty).ToUpperInvariant()
            If (chInBed >= "A"c AndAlso chInBed <= "Z"c) OrElse
               (chInBed >= "0"c AndAlso chInBed <= "9"c) OrElse
               "_-$~!#%&'()@^{}".IndexOf(chInBed) >= 0 Then
                builderInBed.Append(chInBed)
            ElseIf chInBed <> " "c AndAlso chInBed <> "."c Then
                builderInBed.Append("_"c)
            End If
        Next
        Return builderInBed.ToString()
    End Function

    Private Shared Function MakeVolumeLabel(valueInBed As String) As String
        Dim builderInBed As New StringBuilder()
        For Each chInBed As Char In If(valueInBed, String.Empty).ToUpperInvariant()
            If chInBed >= " "c AndAlso chInBed <= "~"c AndAlso ".\/:;*?""<>|+=[]".IndexOf(chInBed) < 0 Then
                builderInBed.Append(chInBed)
            End If
            If builderInBed.Length = 11 Then Exit For
        Next
        If builderInBed.Length = 0 Then builderInBed.Append("SNEAKERNET")
        Return builderInBed.ToString().PadRight(11).Substring(0, 11)
    End Function

    Private Function ReadDosDateTime(directoryOffsetInBed As Integer) As DateTime
        Try
            Dim timeInBed As Integer = ReadUInt16(_image, directoryOffsetInBed + 22)
            Dim dateInBed As Integer = ReadUInt16(_image, directoryOffsetInBed + 24)
            If dateInBed = 0 Then Return New DateTime(1980, 1, 1)
            Dim yearInBed As Integer = 1980 + ((dateInBed >> 9) And &H7F)
            Dim monthInBed As Integer = (dateInBed >> 5) And &HF
            Dim dayInBed As Integer = dateInBed And &H1F
            Dim hourInBed As Integer = (timeInBed >> 11) And &H1F
            Dim minuteInBed As Integer = (timeInBed >> 5) And &H3F
            Dim secondInBed As Integer = (timeInBed And &H1F) * 2
            Return New DateTime(yearInBed, Math.Max(1, monthInBed), Math.Max(1, dayInBed), hourInBed, minuteInBed, secondInBed)
        Catch
            Return New DateTime(1980, 1, 1)
        End Try
    End Function

    Private Sub WriteDosDateTime(directoryOffsetInBed As Integer, valueInBed As DateTime)
        Dim safeInBed As DateTime = valueInBed
        If safeInBed.Year < 1980 Then safeInBed = New DateTime(1980, 1, 1)
        If safeInBed.Year > 2107 Then safeInBed = New DateTime(2107, 12, 31, 23, 59, 58)
        Dim dosTimeInBed As UShort = CUShort((safeInBed.Hour << 11) Or (safeInBed.Minute << 5) Or (safeInBed.Second \ 2))
        Dim dosDateInBed As UShort = CUShort(((safeInBed.Year - 1980) << 9) Or (safeInBed.Month << 5) Or safeInBed.Day)
        WriteUInt16(_image, directoryOffsetInBed + 22, dosTimeInBed)
        WriteUInt16(_image, directoryOffsetInBed + 24, dosDateInBed)
    End Sub

    Private Shared Function ReadUInt16(dataInBed As Byte(), offsetInBed As Integer) As UShort
        Return CUShort(dataInBed(offsetInBed) Or (CUInt(dataInBed(offsetInBed + 1)) << 8))
    End Function

    Private Shared Function ReadUInt32(dataInBed As Byte(), offsetInBed As Integer) As UInteger
        Return CUInt(dataInBed(offsetInBed)) Or
               (CUInt(dataInBed(offsetInBed + 1)) << 8) Or
               (CUInt(dataInBed(offsetInBed + 2)) << 16) Or
               (CUInt(dataInBed(offsetInBed + 3)) << 24)
    End Function

    Private Shared Sub WriteAscii(targetInBed As Byte(), offsetInBed As Integer, valueInBed As String, fixedLengthInBed As Integer)
        Dim textInBed As String = If(valueInBed, String.Empty)
        If textInBed.Length < fixedLengthInBed Then textInBed = textInBed.PadRight(fixedLengthInBed)
        If textInBed.Length > fixedLengthInBed Then textInBed = textInBed.Substring(0, fixedLengthInBed)
        Dim bytesInBed As Byte() = Encoding.ASCII.GetBytes(textInBed)
        Buffer.BlockCopy(bytesInBed, 0, targetInBed, offsetInBed, fixedLengthInBed)
    End Sub

    Private Shared Sub WriteUInt16(targetInBed As Byte(), offsetInBed As Integer, valueInBed As UShort)
        targetInBed(offsetInBed) = CByte(valueInBed And &HFFUS)
        targetInBed(offsetInBed + 1) = CByte((valueInBed >> 8) And &HFFUS)
    End Sub

    Private Shared Sub WriteUInt32(targetInBed As Byte(), offsetInBed As Integer, valueInBed As UInteger)
        targetInBed(offsetInBed) = CByte(valueInBed And &HFFUI)
        targetInBed(offsetInBed + 1) = CByte((valueInBed >> 8) And &HFFUI)
        targetInBed(offsetInBed + 2) = CByte((valueInBed >> 16) And &HFFUI)
        targetInBed(offsetInBed + 3) = CByte((valueInBed >> 24) And &HFFUI)
    End Sub
End Class

Friend NotInheritable Class SneakerNetImageSession
    Public Property Document As Fat12ImageDocument
    Public Property Page As TabPage
    Public Property List As ListView
    Public Property Watcher As FileSystemWatcher
    Public Property CurrentDirectoryCluster As Integer = 0
    Public Property CurrentDirectoryPath As String = "\"
    Public ReadOnly Property ParentDirectoryClusters As New Stack(Of Integer)()
    Public ReadOnly Property ParentDirectoryPaths As New Stack(Of String)()
End Class

Public NotInheritable Class SneakerNetForm
    Inherits Form

    Private ReadOnly _box As FloppyBox
    Private ReadOnly _mountImage As Action(Of Integer, String)
    Private ReadOnly _ejectDrive As Action(Of Integer)
    Private ReadOnly _resetMachine As Action

    Private ReadOnly _toolStrip As New ToolStrip()
    Private ReadOnly _hostPathBox As New TextBox()
    Private ReadOnly _hostList As New ListView()
    Private ReadOnly _hostImageList As New ImageList()
    Private ReadOnly _driveStrip As New FlowLayoutPanel()
    Private ReadOnly _hostBackButton As New Button()
    Private ReadOnly _hostForwardButton As New Button()
    Private ReadOnly _hostUpButton As New Button()
    Private ReadOnly _hostBrowseButton As New Button()
    Private ReadOnly _hostToolTip As New ToolTip()
    Private ReadOnly _hostHistory As New List(Of String)()
    Private _hostHistoryIndex As Integer = -1
    Private _lastDriveRefreshUtc As DateTime = DateTime.MinValue
    Private ReadOnly _tabs As New TabControl()
    Private ReadOnly _tabRenameBox As New TextBox()
    Private _tabRenameSession As SneakerNetImageSession
    Private _suppressTabRenameCommit As Boolean
    Private ReadOnly _imageWorkspacePanel As New Panel()
    Private ReadOnly _imageActivePanel As New Panel()
    Private ReadOnly _emptyImagePanel As New Panel()
    Private ReadOnly _imageHeader As New TableLayoutPanel()
    Private ReadOnly _imageNameBox As New TextBox()
    Private ReadOnly _imageCloseButton As New Button()
    Private ReadOnly _imageToolTip As New ToolTip()
    Private ReadOnly _outerSplit As New SplitContainer()
    Private ReadOnly _innerSplit As New SplitContainer()
    Private ReadOnly _toolsPanel As New FlowLayoutPanel()
    Private ReadOnly _actionPanel As New Panel()
    Private ReadOnly _statusStrip As New StatusStrip()
    Private ReadOnly _statusLabel As New ToolStripStatusLabel()
    Private ReadOnly _geometryLabel As New ToolStripStatusLabel()
    Private ReadOnly _mountAButton As New ToolStripButton("Mount A:")
    Private ReadOnly _mountBButton As New ToolStripButton("Mount B:")
    Private ReadOnly _addSelectedHostButton As New Button()
    Private ReadOnly _extractButton As New ToolStripButton("Extract")
    Private ReadOnly _backupFloppyButtonInBed As New ToolStripButton("Backup")
    Private ReadOnly _bootButton As New ToolStripButton("Make Bootable")
    Private ReadOnly _testBootButton As New ToolStripButton("Test Boot")
    Private ReadOnly _toolsButton As New ToolStripButton("Tools")
    Private _checkFilesystemButton As Button
    Private _imagePropertiesButton As Button
    Private _bootSectorButton As Button
    Private _openFloppyBoxButton As Button
    Private _openDiscBoxButton As Button
    Private _spanPkzip204Button As Button
    Private _lastCreatedIsoPath As String
    Private ReadOnly _newDiskLabelBox As New TextBox()
    Private ReadOnly _newDiskFormatBox As New ComboBox()
    Private _newDiskCreateButton As Button
    Private _suppressImageNameCommit As Boolean
    Private _hostPath As String
    Private _mountedPathA As String
    Private _mountedPathB As String
    Private _dragSession As SneakerNetImageSession
    Private _dragEntries As List(Of SneakerNetDiskEntry)
    Private _splitLayoutInitialized As Boolean

    ' CROMWELL SNEAKER NET MEDIA WORKBENCH PALLET 11 - outer host-media tabs.
    Private ReadOnly _mediaTabs As New TabControl()
    Private ReadOnly _floppyWorkbenchPage As New TabPage("Floppies")
    Private ReadOnly _hardDrivePage As New TabPage("Hard Drives")
    Private ReadOnly _opticalPage As New TabPage("Optical")
    Private ReadOnly _hostDevicesPage As New TabPage("Host Devices")
    Private ReadOnly _imagingPage As New TabPage("Imaging")
    Private ReadOnly _backupPage As New TabPage("Backup")

    Private ReadOnly _ideShelf As IdeDriveShelf
    Private ReadOnly _attachIdeShelfDrive As Action(Of Integer)
    Private ReadOnly _ejectIdeShelfDrive As Action
    Private ReadOnly _mountedIdeShelfDriveId As Func(Of Integer)
    Private ReadOnly _mountIsoImage As Action(Of String)
    Private ReadOnly _ejectIsoImage As Action
    Private ReadOnly _quiesceMachineInBed As Func(Of IDisposable)

    Private ReadOnly _hddListInBed As New ListView()
    Private ReadOnly _hddDetailsInBed As New TextBox()
    Private ReadOnly _hddLabelInBed As New TextBox()
    Private ReadOnly _hddCapacityMbInBed As New NumericUpDown()

    Private ReadOnly _hostDeviceListInBed As New ListView()
    Private ReadOnly _hostDeviceStatusInBed As New Label()
    Private ReadOnly _hostDeviceProgressInBed As New ProgressBar()
    Private _hostDeviceCancellationInBed As CancellationTokenSource

    Private ReadOnly _imagingSourceInBed As New TextBox()
    Private ReadOnly _imagingDestinationInBed As New TextBox()
    Private ReadOnly _imagingStatusInBed As New TextBox()
    Private _imagingCancellationInBed As CancellationTokenSource

    Private ReadOnly _opticalPathInBed As New TextBox()
    Private ReadOnly _opticalDetailsInBed As New TextBox()

    Private ReadOnly _backupRootInBed As New TextBox()
    Private ReadOnly _backupListInBed As New ListView()
    Private ReadOnly _backupStatusInBed As New Label()

    <StructLayout(LayoutKind.Sequential, CharSet:=CharSet.Unicode)>
    Private Structure SHFILEINFO
        Public hIcon As IntPtr
        Public iIcon As Integer
        Public dwAttributes As UInteger
        <MarshalAs(UnmanagedType.ByValTStr, SizeConst:=260)>
        Public szDisplayName As String
        <MarshalAs(UnmanagedType.ByValTStr, SizeConst:=80)>
        Public szTypeName As String
    End Structure

    <DllImport("shell32.dll", CharSet:=CharSet.Unicode)>
    Private Shared Function SHGetFileInfo(
        pszPath As String,
        dwFileAttributes As UInteger,
        ByRef psfi As SHFILEINFO,
        cbFileInfo As UInteger,
        uFlags As UInteger) As IntPtr
    End Function

    <DllImport("user32.dll", SetLastError:=True)>
    Private Shared Function DestroyIcon(hIcon As IntPtr) As Boolean
    End Function

    Private Const SHGFI_ICON As UInteger = &H100UI
    Private Const SHGFI_SMALLICON As UInteger = &H1UI
    Private Const SHGFI_USEFILEATTRIBUTES As UInteger = &H10UI
    Private Const FILE_ATTRIBUTE_DIRECTORY As UInteger = &H10UI
    Private Const FILE_ATTRIBUTE_NORMAL As UInteger = &H80UI

    Public Sub New(boxInBed As FloppyBox,
                   mountImageInBed As Action(Of Integer, String),
                   ejectDriveInBed As Action(Of Integer),
                   resetMachineInBed As Action,
                   Optional ideShelfInBed As IdeDriveShelf = Nothing,
                   Optional attachIdeShelfDriveInBed As Action(Of Integer) = Nothing,
                   Optional ejectIdeShelfDriveInBed As Action = Nothing,
                   Optional mountedIdeShelfDriveIdInBed As Func(Of Integer) = Nothing,
                   Optional mountIsoImageInBed As Action(Of String) = Nothing,
                   Optional ejectIsoImageInBed As Action = Nothing,
                   Optional quiesceMachineInBed As Func(Of IDisposable) = Nothing)
        If boxInBed Is Nothing Then Throw New ArgumentNullException(NameOf(boxInBed))
        _box = boxInBed
        _mountImage = mountImageInBed
        _ejectDrive = ejectDriveInBed
        _resetMachine = resetMachineInBed
        _ideShelf = ideShelfInBed
        _attachIdeShelfDrive = attachIdeShelfDriveInBed
        _ejectIdeShelfDrive = ejectIdeShelfDriveInBed
        _mountedIdeShelfDriveId = mountedIdeShelfDriveIdInBed
        _mountIsoImage = mountIsoImageInBed
        _ejectIsoImage = ejectIsoImageInBed
        _quiesceMachineInBed = quiesceMachineInBed
        BuildUi()
        _box.EnsureExists()
        Dim documentsInBed As String = Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments)
        If String.IsNullOrWhiteSpace(documentsInBed) OrElse Not Directory.Exists(documentsInBed) Then documentsInBed = _box.RootPath
        RefreshDriveStrip(forceInBed:=True)
        NavigateHost(documentsInBed)
        RefreshActionState()
        RefreshHardDrivesInBed()
        RefreshHostDevicesInBed()
        RefreshBackupBrowserInBed()
    End Sub

    Private Sub BuildUi()
        Text = "Cromwell Technologies Sneaker Net"
        StartPosition = FormStartPosition.CenterParent
        Size = New Size(1180, 720)
        MinimumSize = New Size(850, 520)
        Font = New Font("Segoe UI", 9.0F)
        KeyPreview = True
        AllowDrop = True

        _toolStrip.GripStyle = ToolStripGripStyle.Hidden
        _toolStrip.Dock = DockStyle.Top
        Dim newButtonInBed As New ToolStripButton("+ New Disk")
        Dim openButtonInBed As New ToolStripButton("Open Image")
        _toolStrip.Items.AddRange(New ToolStripItem() {
            newButtonInBed,
            openButtonInBed,
            New ToolStripSeparator(),
            _extractButton,
            _backupFloppyButtonInBed,
            New ToolStripSeparator(),
            _bootButton,
            _mountAButton,
            _mountBButton,
            _testBootButton,
            New ToolStripSeparator(),
            _toolsButton
        })
        AddHandler newButtonInBed.Click, Sub() CreateConfiguredNewDisk()
        AddHandler openButtonInBed.Click, Sub() ChooseAndOpenImage()
        AddHandler _extractButton.Click, Sub() ExtractSelectedToHost()
        AddHandler _backupFloppyButtonInBed.Click, AddressOf BackupActiveFloppyClickedInBed
        AddHandler _bootButton.Click, Sub() ShowMakeBootablePanel()
        AddHandler _mountAButton.Click, Sub() ToggleMount(0)
        AddHandler _mountBButton.Click, Sub() ToggleMount(1)
        AddHandler _testBootButton.Click, Sub() TestBootActiveImage()
        AddHandler _toolsButton.Click, Sub() _innerSplit.Panel2Collapsed = Not _innerSplit.Panel2Collapsed

        _statusLabel.Spring = True
        _statusLabel.TextAlign = ContentAlignment.MiddleLeft
        _geometryLabel.TextAlign = ContentAlignment.MiddleRight
        _statusStrip.Items.Add(_statusLabel)
        _statusStrip.Items.Add(_geometryLabel)
        SetStatus("Ready")

        _actionPanel.Dock = DockStyle.Bottom
        _actionPanel.Height = 0
        _actionPanel.Padding = New Padding(8)
        _actionPanel.AutoScroll = True

        ' Configure only topology here.  Splitter distances/minimums are applied
        ' after the form is shown, when WinForms has assigned real client widths.
        _outerSplit.Dock = DockStyle.Fill
        _outerSplit.Orientation = Orientation.Vertical

        _innerSplit.Dock = DockStyle.Fill
        _innerSplit.Orientation = Orientation.Vertical
        _outerSplit.Panel2.Controls.Add(_innerSplit)

        BuildHostPane()
        BuildImagePane()
        BuildToolsPane()
        WireExternalImageDropSurfaces()

        ' The historical floppy workbench is preserved intact, but it now lives
        ' inside the Floppies page of the chassis-independent media workbench.
        _floppyWorkbenchPage.Controls.Add(_outerSplit)
        _floppyWorkbenchPage.Controls.Add(_actionPanel)
        _floppyWorkbenchPage.Controls.Add(_statusStrip)
        _floppyWorkbenchPage.Controls.Add(_toolStrip)
        _toolStrip.BringToFront()
        _statusStrip.BringToFront()
        _actionPanel.BringToFront()

        _mediaTabs.Dock = DockStyle.Fill
        _mediaTabs.TabPages.Add(_floppyWorkbenchPage)
        _mediaTabs.TabPages.Add(_hardDrivePage)
        _mediaTabs.TabPages.Add(_opticalPage)
        _mediaTabs.TabPages.Add(_hostDevicesPage)
        _mediaTabs.TabPages.Add(_imagingPage)
        _mediaTabs.TabPages.Add(_backupPage)
        BuildHardDrivePageInBed()
        BuildOpticalPageInBed()
        BuildHostDevicesPageInBed()
        BuildImagingPageInBed()
        BuildBackupPageInBed()
        Controls.Add(_mediaTabs)

        AddHandler _tabs.SelectedIndexChanged, Sub() RefreshActionState()
        AddHandler _mediaTabs.SelectedIndexChanged,
            Sub()
                If _mediaTabs.SelectedTab Is _hardDrivePage Then RefreshHardDrivesInBed()
                If _mediaTabs.SelectedTab Is _hostDevicesPage Then RefreshHostDevicesInBed()
                If _mediaTabs.SelectedTab Is _backupPage Then RefreshBackupBrowserInBed()
            End Sub
        AddHandler Me.KeyDown, AddressOf SneakerNetForm_KeyDown
        AddHandler Me.Shown,
            Sub()
                ApplyInitialSplitterLayout()
                RefreshDriveStrip(forceInBed:=True)
            End Sub
        AddHandler Me.Activated, Sub() RefreshDriveStrip(forceInBed:=False)
        AddHandler Me.FormClosing, Sub() DisposeAllImageWatchers()
    End Sub

    Private Sub ApplyInitialSplitterLayout()
        If _splitLayoutInitialized Then Return

        ' SplitContainer validates minimum sizes against its CURRENT laid-out size
        ' and CURRENT splitter position.  Therefore establish a legal splitter
        ' position first, then lock in the minimums.
        Dim outerAvailableInBed As Integer = Math.Max(0, _outerSplit.ClientSize.Width - _outerSplit.SplitterWidth)
        Const outerLeftMinimumInBed As Integer = 250
        Const outerRightMinimumInBed As Integer = 520

        If outerAvailableInBed >= outerLeftMinimumInBed + outerRightMinimumInBed Then
            Dim outerTargetInBed As Integer = Math.Max(outerLeftMinimumInBed,
                Math.Min(360, outerAvailableInBed - outerRightMinimumInBed))
            _outerSplit.SplitterDistance = outerTargetInBed
            _outerSplit.Panel1MinSize = outerLeftMinimumInBed
            _outerSplit.Panel2MinSize = outerRightMinimumInBed
        Else
            ' Very small/DPI-scaled host: stay usable rather than throwing.
            _outerSplit.Panel1MinSize = 0
            _outerSplit.Panel2MinSize = 0
            If outerAvailableInBed > 1 Then
                _outerSplit.SplitterDistance = Math.Max(1, Math.Min(outerAvailableInBed - 1, outerAvailableInBed \ 3))
            End If
        End If

        Dim innerAvailableInBed As Integer = Math.Max(0, _innerSplit.ClientSize.Width - _innerSplit.SplitterWidth)
        Const innerImageMinimumInBed As Integer = 320
        Const innerToolsMinimumInBed As Integer = 180

        If innerAvailableInBed >= innerImageMinimumInBed + innerToolsMinimumInBed Then
            Dim innerTargetInBed As Integer = Math.Max(innerImageMinimumInBed,
                Math.Min(560, innerAvailableInBed - innerToolsMinimumInBed))
            _innerSplit.SplitterDistance = innerTargetInBed
            _innerSplit.Panel1MinSize = innerImageMinimumInBed
            _innerSplit.Panel2MinSize = innerToolsMinimumInBed
        Else
            _innerSplit.Panel1MinSize = 0
            _innerSplit.Panel2MinSize = 0
            If innerAvailableInBed > 1 Then
                _innerSplit.SplitterDistance = Math.Max(1, Math.Min(innerAvailableInBed - 1, CInt(innerAvailableInBed * 0.72)))
            End If
        End If

        _splitLayoutInitialized = True
    End Sub

    Private Sub BuildHostPane()
        Dim hostPanelInBed As New Panel() With {.Dock = DockStyle.Fill}

        Dim hostHeaderInBed As New TableLayoutPanel() With {
            .Dock = DockStyle.Top,
            .Height = 35,
            .ColumnCount = 5,
            .Padding = New Padding(4, 4, 4, 2)
        }
        hostHeaderInBed.ColumnStyles.Add(New ColumnStyle(SizeType.Absolute, 32))
        hostHeaderInBed.ColumnStyles.Add(New ColumnStyle(SizeType.Absolute, 32))
        hostHeaderInBed.ColumnStyles.Add(New ColumnStyle(SizeType.Absolute, 32))
        hostHeaderInBed.ColumnStyles.Add(New ColumnStyle(SizeType.Percent, 100))
        hostHeaderInBed.ColumnStyles.Add(New ColumnStyle(SizeType.Absolute, 70))

        _hostBackButton.Text = "←"
        _hostForwardButton.Text = "→"
        _hostUpButton.Text = "↑"
        _hostBrowseButton.Text = "Browse"
        _hostBackButton.Dock = DockStyle.Fill
        _hostForwardButton.Dock = DockStyle.Fill
        _hostUpButton.Dock = DockStyle.Fill
        _hostBrowseButton.Dock = DockStyle.Fill
        _hostBackButton.Margin = New Padding(0)
        _hostForwardButton.Margin = New Padding(0)
        _hostUpButton.Margin = New Padding(0)
        _hostBrowseButton.Margin = New Padding(3, 0, 0, 0)

        _hostToolTip.SetToolTip(_hostBackButton, "Back (Alt+Left)")
        _hostToolTip.SetToolTip(_hostForwardButton, "Forward (Alt+Right)")
        _hostToolTip.SetToolTip(_hostUpButton, "Up one folder (Alt+Up)")
        _hostToolTip.SetToolTip(_hostBrowseButton, "Browse for a host folder")

        _hostPathBox.Dock = DockStyle.Fill
        _hostPathBox.Margin = New Padding(4, 0, 4, 0)
        _hostToolTip.SetToolTip(_hostPathBox, "Host path (Ctrl+L focuses this box)")

        AddHandler _hostBackButton.Click, Sub() NavigateHostBack()
        AddHandler _hostForwardButton.Click, Sub() NavigateHostForward()
        AddHandler _hostUpButton.Click, Sub() NavigateHostParent()
        AddHandler _hostBrowseButton.Click, Sub() BrowseHostFolder()
        AddHandler _hostPathBox.KeyDown,
            Sub(senderInBed As Object, eInBed As KeyEventArgs)
                If eInBed.KeyCode = Keys.Enter Then
                    NavigateHost(_hostPathBox.Text)
                    eInBed.SuppressKeyPress = True
                ElseIf eInBed.KeyCode = Keys.Escape Then
                    _hostPathBox.Text = If(_hostPath, String.Empty)
                    _hostList.Focus()
                    eInBed.SuppressKeyPress = True
                End If
            End Sub

        hostHeaderInBed.Controls.Add(_hostBackButton, 0, 0)
        hostHeaderInBed.Controls.Add(_hostForwardButton, 1, 0)
        hostHeaderInBed.Controls.Add(_hostUpButton, 2, 0)
        hostHeaderInBed.Controls.Add(_hostPathBox, 3, 0)
        hostHeaderInBed.Controls.Add(_hostBrowseButton, 4, 0)

        _driveStrip.Dock = DockStyle.Top
        _driveStrip.Height = 54
        _driveStrip.FlowDirection = FlowDirection.LeftToRight
        _driveStrip.WrapContents = False
        _driveStrip.AutoScroll = True
        _driveStrip.BorderStyle = BorderStyle.FixedSingle
        _driveStrip.Padding = New Padding(3, 3, 3, 2)
        _driveStrip.Margin = New Padding(0)
        _hostToolTip.SetToolTip(_driveStrip, "Host drives — one click jumps to a drive")

        _hostImageList.ColorDepth = ColorDepth.Depth32Bit
        _hostImageList.ImageSize = New Size(16, 16)
        _hostImageList.TransparentColor = Color.Transparent

        ConfigureFileList(_hostList)
        _hostList.Dock = DockStyle.Fill
        _hostList.Columns.Add("Host", 190)
        _hostList.Columns.Add("Type", 75)
        _hostList.Columns.Add("Size", 85, HorizontalAlignment.Right)
        _hostList.AllowDrop = True
        _hostList.SmallImageList = _hostImageList
        _hostList.ShowItemToolTips = True
        AddHandler _hostList.ItemActivate, AddressOf HostList_ItemActivate
        AddHandler _hostList.ItemDrag, AddressOf HostList_ItemDrag
        AddHandler _hostList.DragEnter, AddressOf HostList_DragEnter
        AddHandler _hostList.DragDrop, AddressOf HostList_DragDrop
        AddHandler _hostList.SelectedIndexChanged, Sub() RefreshActionState()

        Dim transferRailInBed As New Panel() With {
            .Dock = DockStyle.Right,
            .Width = 36,
            .Padding = New Padding(3, 10, 3, 0)
        }
        _addSelectedHostButton.Text = "→"
        _addSelectedHostButton.Dock = DockStyle.Top
        _addSelectedHostButton.Height = 34
        _addSelectedHostButton.Font = New Font(Font.FontFamily, 12.0F, FontStyle.Bold)
        _addSelectedHostButton.TabStop = False
        _hostToolTip.SetToolTip(_addSelectedHostButton, "Add selected files and directories to image")
        AddHandler _addSelectedHostButton.Click, Sub() AddSelectedHostPathsToImage()
        transferRailInBed.Controls.Add(_addSelectedHostButton)

        Dim hostLabelInBed As New Label() With {
            .Text = "HOST",
            .Dock = DockStyle.Top,
            .Height = 24,
            .TextAlign = ContentAlignment.MiddleLeft,
            .Padding = New Padding(6, 0, 0, 0),
            .Font = New Font(Font, FontStyle.Bold)
        }

        Dim hostBodyInBed As New Panel() With {.Dock = DockStyle.Fill}
        hostBodyInBed.Controls.Add(_hostList)
        hostBodyInBed.Controls.Add(transferRailInBed)

        hostPanelInBed.Controls.Add(hostBodyInBed)
        hostPanelInBed.Controls.Add(hostHeaderInBed)
        hostPanelInBed.Controls.Add(_driveStrip)
        hostPanelInBed.Controls.Add(hostLabelInBed)
        _outerSplit.Panel1.Controls.Add(hostPanelInBed)

        UpdateHostNavigationButtons()
    End Sub

    Private Sub BuildImagePane()
        _imageWorkspacePanel.Dock = DockStyle.Fill
        _imageActivePanel.Dock = DockStyle.Fill
        _emptyImagePanel.Dock = DockStyle.Fill
        _emptyImagePanel.BackColor = SystemColors.Window

        ' The active image area has two explicit rows.  Do not let DockStyle.Fill
        ' and DockStyle.Top negotiate for the same client rectangle: at some DPI /
        ' layout combinations the TabControl can cover the filename/[×] header.
        Dim imageActiveLayoutInBed As New TableLayoutPanel() With {
            .Dock = DockStyle.Fill,
            .ColumnCount = 1,
            .RowCount = 2,
            .Margin = New Padding(0),
            .Padding = New Padding(0)
        }
        imageActiveLayoutInBed.ColumnStyles.Add(New ColumnStyle(SizeType.Percent, 100.0F))
        imageActiveLayoutInBed.RowStyles.Add(New RowStyle(SizeType.Absolute, 36.0F))
        imageActiveLayoutInBed.RowStyles.Add(New RowStyle(SizeType.Percent, 100.0F))

        _imageHeader.Dock = DockStyle.Fill
        _imageHeader.Margin = New Padding(0)
        _imageHeader.ColumnCount = 2
        _imageHeader.RowCount = 1
        _imageHeader.Padding = New Padding(5, 4, 5, 4)
        _imageHeader.ColumnStyles.Clear()
        _imageHeader.RowStyles.Clear()
        _imageHeader.ColumnStyles.Add(New ColumnStyle(SizeType.Percent, 100.0F))
        _imageHeader.ColumnStyles.Add(New ColumnStyle(SizeType.Absolute, 36.0F))
        _imageHeader.RowStyles.Add(New RowStyle(SizeType.Percent, 100.0F))

        _imageNameBox.Dock = DockStyle.Fill
        _imageNameBox.BorderStyle = BorderStyle.FixedSingle
        _imageNameBox.Margin = New Padding(0, 0, 5, 0)

        _imageCloseButton.Text = "×"
        _imageCloseButton.Dock = DockStyle.Fill
        _imageCloseButton.Margin = New Padding(0)
        _imageCloseButton.TabStop = False
        _imageCloseButton.Font = New Font(Font.FontFamily, 11.0F, FontStyle.Bold)
        _imageToolTip.SetToolTip(_imageCloseButton, "Close image")

        AddHandler _imageCloseButton.Click, Sub() CloseActiveImage()
        AddHandler _imageNameBox.Enter,
            Sub()
                Dim sessionInBed As SneakerNetImageSession = ActiveSession()
                _imageNameBox.Tag = If(sessionInBed Is Nothing, Nothing, sessionInBed.Document.ImagePath)
            End Sub
        AddHandler _imageNameBox.KeyDown, AddressOf ImageNameBox_KeyDown
        AddHandler _imageNameBox.Validated, Sub() CommitImageNameEdit()

        _imageHeader.Controls.Add(_imageNameBox, 0, 0)
        _imageHeader.Controls.Add(_imageCloseButton, 1, 0)

        _tabs.Dock = DockStyle.Fill
        _tabs.Margin = New Padding(0)
        _tabs.DrawMode = TabDrawMode.OwnerDrawFixed
        _tabs.Padding = New Point(18, 3)
        _tabs.HotTrack = True
        AddHandler _tabs.DrawItem, AddressOf Tabs_DrawItem
        AddHandler _tabs.MouseDown, AddressOf Tabs_MouseDown
        AddHandler _tabs.KeyDown, AddressOf Tabs_KeyDown

        imageActiveLayoutInBed.Controls.Add(_imageHeader, 0, 0)
        imageActiveLayoutInBed.Controls.Add(_tabs, 0, 1)
        _imageActivePanel.Controls.Add(imageActiveLayoutInBed)

        ' TabControl headers cannot host child controls, so the filename editor is
        ' a temporary TextBox overlaid on the selected tab's text rectangle.
        _tabRenameBox.Visible = False
        _tabRenameBox.BorderStyle = BorderStyle.FixedSingle
        AddHandler _tabRenameBox.KeyDown, AddressOf TabRenameBox_KeyDown
        AddHandler _tabRenameBox.Validated, Sub() CommitTabRenameEdit()
        _imageActivePanel.Controls.Add(_tabRenameBox)
        _tabRenameBox.BringToFront()

        BuildEmptyImageState()

        _imageWorkspacePanel.Controls.Add(_imageActivePanel)
        _imageWorkspacePanel.Controls.Add(_emptyImagePanel)
        _innerSplit.Panel1.Controls.Add(_imageWorkspacePanel)
    End Sub

    Private Sub Tabs_DrawItem(senderInBed As Object, eInBed As DrawItemEventArgs)
        If eInBed.Index < 0 OrElse eInBed.Index >= _tabs.TabPages.Count Then Return

        Dim tabRectInBed As Rectangle = _tabs.GetTabRect(eInBed.Index)
        Dim selectedInBed As Boolean = (eInBed.State And DrawItemState.Selected) <> 0
        Dim backColorInBed As Color = If(selectedInBed, SystemColors.Window, SystemColors.Control)
        Using backBrushInBed As New SolidBrush(backColorInBed)
            eInBed.Graphics.FillRectangle(backBrushInBed, tabRectInBed)
        End Using
        eInBed.Graphics.DrawRectangle(SystemPens.ControlDark, tabRectInBed.X, tabRectInBed.Y, Math.Max(0, tabRectInBed.Width - 1), Math.Max(0, tabRectInBed.Height - 1))

        Dim textRectInBed As Rectangle = GetTabTextRectangle(eInBed.Index)
        TextRenderer.DrawText(
            eInBed.Graphics,
            _tabs.TabPages(eInBed.Index).Text,
            _tabs.Font,
            textRectInBed,
            SystemColors.ControlText,
            TextFormatFlags.Left Or TextFormatFlags.VerticalCenter Or TextFormatFlags.EndEllipsis Or TextFormatFlags.NoPrefix)

        Dim closeRectInBed As Rectangle = GetTabCloseRectangle(eInBed.Index)
        Using closeFontInBed As New Font(_tabs.Font.FontFamily, _tabs.Font.Size + 1.0F, FontStyle.Bold)
            TextRenderer.DrawText(
                eInBed.Graphics,
                "×",
                closeFontInBed,
                closeRectInBed,
                SystemColors.ControlText,
                TextFormatFlags.HorizontalCenter Or TextFormatFlags.VerticalCenter Or TextFormatFlags.NoPadding)
        End Using
    End Sub

    Private Function GetTabCloseRectangle(indexInBed As Integer) As Rectangle
        Dim tabRectInBed As Rectangle = _tabs.GetTabRect(indexInBed)
        Const closeWidthInBed As Integer = 18
        Return New Rectangle(
            Math.Max(tabRectInBed.Left, tabRectInBed.Right - closeWidthInBed - 2),
            tabRectInBed.Top + 2,
            closeWidthInBed,
            Math.Max(1, tabRectInBed.Height - 4))
    End Function

    Private Function GetTabTextRectangle(indexInBed As Integer) As Rectangle
        Dim tabRectInBed As Rectangle = _tabs.GetTabRect(indexInBed)
        Dim closeRectInBed As Rectangle = GetTabCloseRectangle(indexInBed)
        Dim leftInBed As Integer = tabRectInBed.Left + 7
        Dim widthInBed As Integer = Math.Max(24, closeRectInBed.Left - leftInBed - 3)
        Return New Rectangle(leftInBed, tabRectInBed.Top + 2, widthInBed, Math.Max(1, tabRectInBed.Height - 4))
    End Function

    Private Function TabIndexAt(pointInBed As Point) As Integer
        For indexInBed As Integer = 0 To _tabs.TabPages.Count - 1
            If _tabs.GetTabRect(indexInBed).Contains(pointInBed) Then Return indexInBed
        Next
        Return -1
    End Function

    Private Sub Tabs_MouseDown(senderInBed As Object, eInBed As MouseEventArgs)
        If eInBed.Button <> MouseButtons.Left Then Return

        Dim indexInBed As Integer = TabIndexAt(eInBed.Location)
        If indexInBed < 0 Then Return

        Dim pageInBed As TabPage = _tabs.TabPages(indexInBed)
        If GetTabCloseRectangle(indexInBed).Contains(eInBed.Location) Then
            CancelTabRenameEdit()
            CloseImagePage(pageInBed)
            Return
        End If

        Dim wasSelectedInBed As Boolean = indexInBed = _tabs.SelectedIndex
        _tabs.SelectedIndex = indexInBed

        ' A first click switches tabs normally.  Clicking the filename on the
        ' already-active tab turns that filename into an inline editor.
        If wasSelectedInBed AndAlso GetTabTextRectangle(indexInBed).Contains(eInBed.Location) Then
            BeginTabRenameEdit(indexInBed)
        End If
    End Sub

    Private Sub Tabs_KeyDown(senderInBed As Object, eInBed As KeyEventArgs)
        If eInBed.KeyCode = Keys.F2 AndAlso _tabs.SelectedIndex >= 0 Then
            BeginTabRenameEdit(_tabs.SelectedIndex)
            eInBed.SuppressKeyPress = True
        End If
    End Sub

    Private Sub BeginTabRenameEdit(indexInBed As Integer)
        If indexInBed < 0 OrElse indexInBed >= _tabs.TabPages.Count Then Return

        Dim pageInBed As TabPage = _tabs.TabPages(indexInBed)
        Dim sessionInBed As SneakerNetImageSession = TryCast(pageInBed.Tag, SneakerNetImageSession)
        If sessionInBed Is Nothing OrElse sessionInBed.Document Is Nothing Then Return

        If IsImagePathMounted(sessionInBed.Document.ImagePath) Then
            SetError("Eject this image before renaming it.")
            Return
        End If

        _tabs.SelectedIndex = indexInBed
        _tabRenameSession = sessionInBed
        _tabRenameBox.Text = Path.GetFileName(sessionInBed.Document.ImagePath)

        Dim textRectInBed As Rectangle = GetTabTextRectangle(indexInBed)
        Dim screenPointInBed As Point = _tabs.PointToScreen(textRectInBed.Location)
        Dim clientPointInBed As Point = _imageActivePanel.PointToClient(screenPointInBed)
        _tabRenameBox.SetBounds(
            clientPointInBed.X,
            clientPointInBed.Y,
            Math.Max(60, textRectInBed.Width),
            Math.Max(_tabRenameBox.PreferredHeight, textRectInBed.Height))
        _tabRenameBox.Visible = True
        _tabRenameBox.BringToFront()
        _tabRenameBox.Focus()
        _tabRenameBox.SelectAll()
    End Sub

    Private Sub TabRenameBox_KeyDown(senderInBed As Object, eInBed As KeyEventArgs)
        If eInBed.KeyCode = Keys.Enter Then
            CommitTabRenameEdit()
            eInBed.SuppressKeyPress = True
        ElseIf eInBed.KeyCode = Keys.Escape Then
            CancelTabRenameEdit()
            _tabs.Focus()
            eInBed.SuppressKeyPress = True
        End If
    End Sub

    Private Sub CommitTabRenameEdit()
        If _suppressTabRenameCommit OrElse Not _tabRenameBox.Visible Then Return

        Dim sessionInBed As SneakerNetImageSession = _tabRenameSession
        If sessionInBed Is Nothing OrElse sessionInBed.Document Is Nothing Then
            CancelTabRenameEdit()
            Return
        End If

        Dim requestedInBed As String = _tabRenameBox.Text.Trim()
        _suppressTabRenameCommit = True
        Try
            ' Reuse the established rename path so watcher suppression, mounted
            ' image protection, extension preservation, and error handling stay
            ' identical whether the header field or tab itself performs the edit.
            _imageNameBox.Tag = sessionInBed.Document.ImagePath
            _imageNameBox.Text = requestedInBed
            CommitImageNameEdit()
            _tabs.Invalidate()
            _tabRenameBox.Visible = False
            _tabRenameSession = Nothing
        Finally
            _suppressTabRenameCommit = False
        End Try
    End Sub

    Private Sub CancelTabRenameEdit()
        If Not _tabRenameBox.Visible AndAlso _tabRenameSession Is Nothing Then Return
        _suppressTabRenameCommit = True
        Try
            _tabRenameBox.Visible = False
            _tabRenameSession = Nothing
        Finally
            _suppressTabRenameCommit = False
        End Try
        _tabs.Invalidate()
    End Sub

    Private Sub BuildEmptyImageState()
        _emptyImagePanel.Controls.Clear()

        Dim centeringInBed As New TableLayoutPanel() With {
            .Dock = DockStyle.Fill,
            .ColumnCount = 3,
            .RowCount = 3
        }
        centeringInBed.ColumnStyles.Add(New ColumnStyle(SizeType.Percent, 50.0F))
        centeringInBed.ColumnStyles.Add(New ColumnStyle(SizeType.AutoSize))
        centeringInBed.ColumnStyles.Add(New ColumnStyle(SizeType.Percent, 50.0F))
        centeringInBed.RowStyles.Add(New RowStyle(SizeType.Percent, 50.0F))
        centeringInBed.RowStyles.Add(New RowStyle(SizeType.AutoSize))
        centeringInBed.RowStyles.Add(New RowStyle(SizeType.Percent, 50.0F))

        Dim contentInBed As New FlowLayoutPanel() With {
            .AutoSize = True,
            .AutoSizeMode = AutoSizeMode.GrowAndShrink,
            .FlowDirection = FlowDirection.TopDown,
            .WrapContents = False,
            .Margin = New Padding(0)
        }

        Dim openIconInBed As New Label() With {
            .Text = "📂",
            .Width = 220,
            .Height = 48,
            .TextAlign = ContentAlignment.MiddleCenter,
            .Font = New Font("Segoe UI Emoji", 27.0F, FontStyle.Regular),
            .Margin = New Padding(0, 0, 0, 0)
        }
        Dim openInBed As New Button() With {
            .Text = "Open Image",
            .Width = 220,
            .Height = 34,
            .Margin = New Padding(0, 0, 0, 14)
        }
        Dim noImageInBed As New Label() With {
            .Text = "No disk image is open",
            .Width = 220,
            .Height = 24,
            .TextAlign = ContentAlignment.MiddleCenter,
            .Font = New Font(Font, FontStyle.Bold),
            .Margin = New Padding(0, 0, 0, 8)
        }
        Dim newInBed As New Button() With {
            .Text = "+  NEW DISK",
            .Width = 220,
            .Height = 58,
            .Font = New Font(Font.FontFamily, 12.0F, FontStyle.Bold),
            .Margin = New Padding(0, 0, 0, 10)
        }
        Dim helpInBed As New Label() With {
            .Text = "Create a floppy image or open an existing .IMG / .IMA file.",
            .Width = 250,
            .Height = 42,
            .TextAlign = ContentAlignment.TopCenter,
            .ForeColor = SystemColors.GrayText,
            .Margin = New Padding(0)
        }

        AddHandler openInBed.Click, Sub() ChooseAndOpenImage()
        AddHandler newInBed.Click, Sub() CreateConfiguredNewDisk()

        contentInBed.Controls.Add(openIconInBed)
        contentInBed.Controls.Add(openInBed)
        contentInBed.Controls.Add(noImageInBed)
        contentInBed.Controls.Add(newInBed)
        contentInBed.Controls.Add(helpInBed)

        centeringInBed.Controls.Add(contentInBed, 1, 1)
        _emptyImagePanel.Controls.Add(centeringInBed)
    End Sub

    Private Sub ImageNameBox_KeyDown(senderInBed As Object, eInBed As KeyEventArgs)
        If eInBed.KeyCode = Keys.Enter Then
            CommitImageNameEdit()
            eInBed.SuppressKeyPress = True
        ElseIf eInBed.KeyCode = Keys.Escape Then
            Dim sessionInBed As SneakerNetImageSession = ActiveSession()
            _suppressImageNameCommit = True
            Try
                _imageNameBox.Text = If(sessionInBed Is Nothing, String.Empty, Path.GetFileName(sessionInBed.Document.ImagePath))
            Finally
                _suppressImageNameCommit = False
            End Try
            _imageNameBox.SelectAll()
            eInBed.SuppressKeyPress = True
        End If
    End Sub

    Private Sub CommitImageNameEdit()
        If _suppressImageNameCommit Then Return

        Dim sessionInBed As SneakerNetImageSession = Nothing
        Dim originalPathInBed As String = TryCast(_imageNameBox.Tag, String)

        If Not String.IsNullOrWhiteSpace(originalPathInBed) Then
            For Each pageInBed As TabPage In _tabs.TabPages
                Dim candidateInBed As SneakerNetImageSession = TryCast(pageInBed.Tag, SneakerNetImageSession)
                If candidateInBed IsNot Nothing AndAlso
                   candidateInBed.Document.ImagePath.Equals(originalPathInBed, StringComparison.OrdinalIgnoreCase) Then
                    sessionInBed = candidateInBed
                    Exit For
                End If
            Next
        End If
        If sessionInBed Is Nothing Then sessionInBed = ActiveSession()
        If sessionInBed Is Nothing Then Return

        Dim oldPathInBed As String = sessionInBed.Document.ImagePath
        Dim oldNameInBed As String = Path.GetFileName(oldPathInBed)
        Dim requestedInBed As String = _imageNameBox.Text.Trim()
        If requestedInBed.Equals(oldNameInBed, StringComparison.Ordinal) Then Return

        If IsImagePathMounted(oldPathInBed) Then
            SetError("Eject this image before renaming it.")
            RestoreImageNameBox(sessionInBed)
            Return
        End If

        ' Renaming our own backing file would otherwise look exactly like an
        ' external move to FileSystemWatcher.  Drop the watcher for the tiny
        ' rename window, then bind it to the new name (or the old name on error).
        DisposeImageWatcher(sessionInBed)
        Try
            Dim newPathInBed As String = sessionInBed.Document.RenameImageFile(requestedInBed)
            sessionInBed.Page.Text = Path.GetFileName(newPathInBed)
            _imageNameBox.Tag = newPathInBed
            RestoreImageNameBox(sessionInBed)
            StartWatchingImageSession(sessionInBed)
            SetStatus("Renamed image to " & Path.GetFileName(newPathInBed))
        Catch ex As Exception
            RestoreImageNameBox(sessionInBed)
            StartWatchingImageSession(sessionInBed)
            SetError(ex.Message)
        End Try
    End Sub

    Private Sub RestoreImageNameBox(sessionInBed As SneakerNetImageSession)
        _suppressImageNameCommit = True
        Try
            _imageNameBox.Text = If(sessionInBed Is Nothing, String.Empty, Path.GetFileName(sessionInBed.Document.ImagePath))
            _imageNameBox.Tag = If(sessionInBed Is Nothing, Nothing, sessionInBed.Document.ImagePath)
        Finally
            _suppressImageNameCommit = False
        End Try
    End Sub

    Private Function IsImagePathMounted(pathInBed As String) As Boolean
        If String.IsNullOrWhiteSpace(pathInBed) Then Return False
        Return (_mountedPathA IsNot Nothing AndAlso _mountedPathA.Equals(pathInBed, StringComparison.OrdinalIgnoreCase)) OrElse
               (_mountedPathB IsNot Nothing AndAlso _mountedPathB.Equals(pathInBed, StringComparison.OrdinalIgnoreCase))
    End Function

    Private Sub BuildToolsPane()
        _toolsPanel.Dock = DockStyle.Fill
        _toolsPanel.FlowDirection = FlowDirection.TopDown
        _toolsPanel.WrapContents = False
        _toolsPanel.AutoScroll = True
        _toolsPanel.Padding = New Padding(8)

        ' Persistent new-disk configuration lives in the tool rail so the common
        ' 1.44 MB case is genuinely one click: set this once, then + New Disk.
        Dim newDiskTitleInBed As New Label() With {
            .Text = "NEW DISK",
            .AutoSize = True,
            .Font = New Font(Font, FontStyle.Bold),
            .Margin = New Padding(3, 3, 3, 8)
        }
        _toolsPanel.Controls.Add(newDiskTitleInBed)

        Dim labelCaptionInBed As New Label() With {
            .Text = "Label",
            .AutoSize = True,
            .Margin = New Padding(3, 0, 3, 2)
        }
        _toolsPanel.Controls.Add(labelCaptionInBed)

        _newDiskLabelBox.Width = 175
        _newDiskLabelBox.Text = "New Disk"
        _newDiskLabelBox.Margin = New Padding(3, 0, 3, 7)
        _toolsPanel.Controls.Add(_newDiskLabelBox)

        Dim formatCaptionInBed As New Label() With {
            .Text = "Format",
            .AutoSize = True,
            .Margin = New Padding(3, 0, 3, 2)
        }
        _toolsPanel.Controls.Add(formatCaptionInBed)

        _newDiskFormatBox.Width = 175
        _newDiskFormatBox.DropDownStyle = ComboBoxStyle.DropDownList
        _newDiskFormatBox.Margin = New Padding(3, 0, 3, 7)
        _newDiskFormatBox.Items.Clear()
        For Each formatInBed As Fat12FloppyFormat In Fat12FloppyBuilder.GetFormats()
            _newDiskFormatBox.Items.Add(formatInBed)
        Next
        _newDiskFormatBox.DisplayMember = "DisplayName"
        If _newDiskFormatBox.Items.Count > 0 Then
            Dim defaultIndexInBed As Integer = 0
            For indexInBed As Integer = 0 To _newDiskFormatBox.Items.Count - 1
                Dim candidateInBed As Fat12FloppyFormat = TryCast(_newDiskFormatBox.Items(indexInBed), Fat12FloppyFormat)
                If candidateInBed IsNot Nothing AndAlso candidateInBed.DisplayName.StartsWith("1.44 MB", StringComparison.OrdinalIgnoreCase) Then
                    defaultIndexInBed = indexInBed
                    Exit For
                End If
            Next
            _newDiskFormatBox.SelectedIndex = defaultIndexInBed
        End If
        _toolsPanel.Controls.Add(_newDiskFormatBox)

        _newDiskCreateButton = New Button() With {
            .Text = "+ Create New Disk",
            .Width = 175,
            .Height = 38,
            .Font = New Font(Font, FontStyle.Bold),
            .TextAlign = ContentAlignment.MiddleCenter,
            .Margin = New Padding(3, 2, 3, 14)
        }
        AddHandler _newDiskCreateButton.Click, Sub() CreateConfiguredNewDisk()
        _toolsPanel.Controls.Add(_newDiskCreateButton)

        Dim titleInBed As New Label() With {
            .Text = "QUICK TOOLS",
            .AutoSize = True,
            .Font = New Font(Font, FontStyle.Bold),
            .Margin = New Padding(3, 3, 3, 10)
        }
        _toolsPanel.Controls.Add(titleInBed)
        _checkFilesystemButton = AddToolButton("Check Filesystem", Sub() VerifyActiveImage())
        _imagePropertiesButton = AddToolButton("Image Properties", Sub() ShowImageProperties())
        _bootSectorButton = AddToolButton("Boot Sector", Sub() ShowBootSector())
        _openFloppyBoxButton = AddToolButton("Open Floppy Box", Sub() OpenFloppyBoxFolder())
        _openDiscBoxButton = AddToolButton("Open Disc Box", Sub() OpenDiscBoxFolder())
        _spanPkzip204Button = AddToolButton("Span ISO -> Floppies" & Environment.NewLine & "(PKZIP 2.04g)", Sub() ChooseIsoToSpanPkZip204())
        _spanPkzip204Button.Height = 48

        Dim noteInBed As New Label() With {
            .Text = "FAT Inspector, raw-sector editing, templates and hard-disk media can land here without bloating the normal workflow.",
            .AutoSize = True,
            .MaximumSize = New Size(185, 0),
            .Margin = New Padding(3, 14, 3, 3)
        }
        _toolsPanel.Controls.Add(noteInBed)
        _innerSplit.Panel2.Controls.Add(_toolsPanel)
    End Sub

    Private Function AddToolButton(textInBed As String, clickInBed As Action) As Button
        Dim buttonInBed As New Button() With {
            .Text = textInBed,
            .Width = 175,
            .Height = 32,
            .TextAlign = ContentAlignment.MiddleLeft,
            .Margin = New Padding(3, 3, 3, 3)
        }
        AddHandler buttonInBed.Click, Sub() clickInBed()
        _toolsPanel.Controls.Add(buttonInBed)
        Return buttonInBed
    End Function

    Private Shared Sub ConfigureFileList(listInBed As ListView)
        listInBed.View = View.Details
        listInBed.FullRowSelect = True
        listInBed.HideSelection = False
        listInBed.MultiSelect = True
        listInBed.LabelEdit = False
        listInBed.GridLines = False
    End Sub

    Private Sub NavigateHost(pathInBed As String)
        NavigateHostCore(pathInBed, addHistoryInBed:=True)
    End Sub

    Private Sub NavigateHostCore(pathInBed As String, addHistoryInBed As Boolean)
        Try
            Dim fullInBed As String = Path.GetFullPath(Environment.ExpandEnvironmentVariables(pathInBed))
            If Not Directory.Exists(fullInBed) Then Throw New DirectoryNotFoundException(fullInBed)

            If addHistoryInBed Then
                If _hostHistoryIndex < 0 OrElse
                   Not _hostHistory(_hostHistoryIndex).Equals(fullInBed, StringComparison.OrdinalIgnoreCase) Then
                    If _hostHistoryIndex < _hostHistory.Count - 1 Then
                        _hostHistory.RemoveRange(_hostHistoryIndex + 1, _hostHistory.Count - _hostHistoryIndex - 1)
                    End If
                    _hostHistory.Add(fullInBed)
                    _hostHistoryIndex = _hostHistory.Count - 1
                End If
            End If

            _hostPath = fullInBed
            _hostPathBox.Text = fullInBed
            RefreshHostList()
            UpdateHostNavigationButtons()
            RefreshDriveSelection()
            SetStatus("Host: " & fullInBed)
        Catch ex As Exception
            SetError(ex.Message)
            UpdateHostNavigationButtons()
        End Try
    End Sub

    Private Sub NavigateHostBack()
        If _hostHistoryIndex <= 0 Then Return
        Dim oldIndexInBed As Integer = _hostHistoryIndex
        _hostHistoryIndex -= 1
        Dim targetInBed As String = _hostHistory(_hostHistoryIndex)
        If Directory.Exists(targetInBed) Then
            NavigateHostCore(targetInBed, addHistoryInBed:=False)
        Else
            _hostHistory.RemoveAt(_hostHistoryIndex)
            _hostHistoryIndex = Math.Min(oldIndexInBed - 1, _hostHistory.Count - 1)
            UpdateHostNavigationButtons()
            SetError("That host folder no longer exists.")
        End If
    End Sub

    Private Sub NavigateHostForward()
        If _hostHistoryIndex < 0 OrElse _hostHistoryIndex >= _hostHistory.Count - 1 Then Return
        _hostHistoryIndex += 1
        Dim targetInBed As String = _hostHistory(_hostHistoryIndex)
        If Directory.Exists(targetInBed) Then
            NavigateHostCore(targetInBed, addHistoryInBed:=False)
        Else
            _hostHistory.RemoveAt(_hostHistoryIndex)
            _hostHistoryIndex = Math.Max(-1, _hostHistoryIndex - 1)
            UpdateHostNavigationButtons()
            SetError("That host folder no longer exists.")
        End If
    End Sub

    Private Sub NavigateHostParent()
        If String.IsNullOrWhiteSpace(_hostPath) Then Return
        Dim parentInBed As DirectoryInfo = Directory.GetParent(_hostPath)
        If parentInBed IsNot Nothing Then NavigateHost(parentInBed.FullName)
    End Sub

    Private Sub UpdateHostNavigationButtons()
        _hostBackButton.Enabled = _hostHistoryIndex > 0
        _hostForwardButton.Enabled = _hostHistoryIndex >= 0 AndAlso _hostHistoryIndex < _hostHistory.Count - 1
        If String.IsNullOrWhiteSpace(_hostPath) Then
            _hostUpButton.Enabled = False
        Else
            Try
                _hostUpButton.Enabled = Directory.GetParent(_hostPath) IsNot Nothing
            Catch
                _hostUpButton.Enabled = False
            End Try
        End If
    End Sub

    Private Sub BrowseHostFolder()
        Using pickerInBed As New FolderBrowserDialog()
            pickerInBed.Description = "Choose the host-side Sneaker Net working folder"
            pickerInBed.SelectedPath = _hostPath
            If pickerInBed.ShowDialog(Me) = DialogResult.OK Then NavigateHost(pickerInBed.SelectedPath)
        End Using
    End Sub

    Private Sub RefreshDriveStrip(forceInBed As Boolean)
        If Not forceInBed AndAlso (DateTime.UtcNow - _lastDriveRefreshUtc).TotalSeconds < 2.0 Then Return
        _lastDriveRefreshUtc = DateTime.UtcNow

        Dim drivesInBed As DriveInfo()
        Try
            drivesInBed = DriveInfo.GetDrives()
        Catch ex As Exception
            SetError("Unable to enumerate host drives: " & ex.Message)
            Return
        End Try

        For Each oldControlInBed As Control In _driveStrip.Controls
            Dim oldButtonInBed As Button = TryCast(oldControlInBed, Button)
            If oldButtonInBed IsNot Nothing AndAlso oldButtonInBed.Image IsNot Nothing Then
                oldButtonInBed.Image.Dispose()
                oldButtonInBed.Image = Nothing
            End If
        Next
        _driveStrip.Controls.Clear()

        For Each driveInBed As DriveInfo In drivesInBed.OrderBy(Function(drive) drive.Name, StringComparer.OrdinalIgnoreCase)
            Dim rootInBed As String = driveInBed.Name
            Dim driveTypeInBed As DriveType
            Try
                driveTypeInBed = driveInBed.DriveType
            Catch
                driveTypeInBed = DriveType.Unknown
            End Try

            Dim driveButtonInBed As New Button() With {
                .Width = 64,
                .Height = 30,
                .Margin = New Padding(2, 1, 2, 1),
                .Padding = New Padding(3, 1, 3, 1),
                .Text = rootInBed.TrimEnd("\"c),
                .Tag = rootInBed,
                .TextImageRelation = TextImageRelation.ImageBeforeText,
                .ImageAlign = ContentAlignment.MiddleLeft,
                .TextAlign = ContentAlignment.MiddleCenter,
                .FlatStyle = FlatStyle.Flat
            }

            driveButtonInBed.FlatAppearance.BorderSize = 1
            driveButtonInBed.Image = GetShellBitmap(rootInBed, FILE_ATTRIBUTE_DIRECTORY, useFileAttributesInBed:=False)

            Dim tipInBed As String = rootInBed.TrimEnd("\"c) & " • " & FriendlyDriveType(driveTypeInBed)
            If driveTypeInBed <> DriveType.Network Then
                Try
                    If driveInBed.IsReady Then
                        Dim labelInBed As String = driveInBed.VolumeLabel
                        If Not String.IsNullOrWhiteSpace(labelInBed) Then tipInBed &= " • " & labelInBed
                        tipInBed &= " • " & FormatBytes(driveInBed.AvailableFreeSpace) & " free"
                    Else
                        tipInBed &= " • not ready"
                    End If
                Catch
                End Try
            End If
            _hostToolTip.SetToolTip(driveButtonInBed, tipInBed)
            AddHandler driveButtonInBed.Click,
                Sub(senderInBed As Object, eInBed As EventArgs)
                    Dim buttonInBed As Button = TryCast(senderInBed, Button)
                    Dim targetInBed As String = If(buttonInBed Is Nothing, Nothing, TryCast(buttonInBed.Tag, String))
                    If Not String.IsNullOrWhiteSpace(targetInBed) Then NavigateHost(targetInBed)
                End Sub
            _driveStrip.Controls.Add(driveButtonInBed)
        Next

        Dim refreshButtonInBed As New Button() With {
            .Text = "↻",
            .Width = 34,
            .Height = 40,
            .Margin = New Padding(4, 1, 2, 1),
            .FlatStyle = FlatStyle.Flat
        }
        refreshButtonInBed.FlatAppearance.BorderSize = 1
        _hostToolTip.SetToolTip(refreshButtonInBed, "Refresh host drives")
        AddHandler refreshButtonInBed.Click, Sub() RefreshDriveStrip(forceInBed:=True)
        _driveStrip.Controls.Add(refreshButtonInBed)

        RefreshDriveSelection()
    End Sub

    Private Sub RefreshDriveSelection()
        Dim currentRootInBed As String = String.Empty
        If Not String.IsNullOrWhiteSpace(_hostPath) Then
            Try
                currentRootInBed = Path.GetPathRoot(_hostPath)
            Catch
            End Try
        End If

        For Each controlInBed As Control In _driveStrip.Controls
            Dim buttonInBed As Button = TryCast(controlInBed, Button)
            If buttonInBed Is Nothing Then Continue For
            Dim rootInBed As String = TryCast(buttonInBed.Tag, String)
            If String.IsNullOrWhiteSpace(rootInBed) Then Continue For
            Dim selectedInBed As Boolean =
                Not String.IsNullOrWhiteSpace(currentRootInBed) AndAlso
                rootInBed.Equals(currentRootInBed, StringComparison.OrdinalIgnoreCase)
            buttonInBed.BackColor = If(selectedInBed, SystemColors.ControlLightLight, SystemColors.Control)
            buttonInBed.FlatAppearance.BorderSize = If(selectedInBed, 2, 1)
        Next
    End Sub

    Private Shared Function FriendlyDriveType(typeInBed As DriveType) As String
        Select Case typeInBed
            Case DriveType.Fixed
                Return "Fixed disk"
            Case DriveType.Removable
                Return "Removable"
            Case DriveType.CDRom
                Return "Optical"
            Case DriveType.Network
                Return "Network"
            Case DriveType.Ram
                Return "RAM disk"
            Case DriveType.NoRootDirectory
                Return "Unavailable"
            Case Else
                Return "Drive"
        End Select
    End Function

    Private Shared Function GetShellBitmap(pathInBed As String,
                                           attributesInBed As UInteger,
                                           useFileAttributesInBed As Boolean) As Bitmap
        Dim infoInBed As New SHFILEINFO()
        Dim flagsInBed As UInteger = SHGFI_ICON Or SHGFI_SMALLICON
        If useFileAttributesInBed Then flagsInBed = flagsInBed Or SHGFI_USEFILEATTRIBUTES

        Try
            Dim resultInBed As IntPtr = SHGetFileInfo(
                pathInBed,
                attributesInBed,
                infoInBed,
                CUInt(Marshal.SizeOf(GetType(SHFILEINFO))),
                flagsInBed)

            If resultInBed <> IntPtr.Zero AndAlso infoInBed.hIcon <> IntPtr.Zero Then
                Dim clonedInBed As Icon = CType(Icon.FromHandle(infoInBed.hIcon).Clone(), Icon)
                DestroyIcon(infoInBed.hIcon)
                Using clonedInBed
                    Return clonedInBed.ToBitmap()
                End Using
            End If
        Catch
            If infoInBed.hIcon <> IntPtr.Zero Then
                Try
                    DestroyIcon(infoInBed.hIcon)
                Catch
                End Try
            End If
        End Try

        Return SystemIcons.Application.ToBitmap()
    End Function

    Private Function EnsureHostIconKey(pathInBed As String, directoryInBed As Boolean) As String
        Dim keyInBed As String
        Dim sampleInBed As String
        Dim attributesInBed As UInteger

        If directoryInBed Then
            keyInBed = "__folder"
            sampleInBed = "folder"
            attributesInBed = FILE_ATTRIBUTE_DIRECTORY
        Else
            Dim extensionInBed As String = Path.GetExtension(pathInBed).ToLowerInvariant()
            keyInBed = If(String.IsNullOrWhiteSpace(extensionInBed), "__file", "__file" & extensionInBed)
            sampleInBed = If(String.IsNullOrWhiteSpace(extensionInBed), "file", "file" & extensionInBed)
            attributesInBed = FILE_ATTRIBUTE_NORMAL
        End If

        If Not _hostImageList.Images.ContainsKey(keyInBed) Then
            Using bitmapInBed As Bitmap = GetShellBitmap(sampleInBed, attributesInBed, useFileAttributesInBed:=True)
                _hostImageList.Images.Add(keyInBed, CType(bitmapInBed.Clone(), Bitmap))
            End Using
        End If

        Return keyInBed
    End Function

    Private Sub RefreshHostList()
        _hostList.BeginUpdate()
        Try
            _hostList.Items.Clear()

            Dim directoriesInBed As IEnumerable(Of String)
            Dim filesInBed As IEnumerable(Of String)
            Try
                directoriesInBed = Directory.EnumerateDirectories(_hostPath).
                    OrderBy(Function(pathInBed) Path.GetFileName(pathInBed), StringComparer.CurrentCultureIgnoreCase).
                    ToArray()
                filesInBed = Directory.EnumerateFiles(_hostPath).
                    OrderBy(Function(pathInBed) Path.GetFileName(pathInBed), StringComparer.CurrentCultureIgnoreCase).
                    ToArray()
            Catch ex As Exception
                SetError(ex.Message)
                Return
            End Try

            For Each directoryInBed As String In directoriesInBed
                Dim itemInBed As New ListViewItem(Path.GetFileName(directoryInBed)) With {
                    .Tag = directoryInBed,
                    .ImageKey = EnsureHostIconKey(directoryInBed, directoryInBed:=True),
                    .ToolTipText = directoryInBed
                }
                itemInBed.SubItems.Add("Folder")
                itemInBed.SubItems.Add(String.Empty)
                _hostList.Items.Add(itemInBed)
            Next

            For Each fileInBed As String In filesInBed
                Dim infoInBed As New FileInfo(fileInBed)
                Dim itemInBed As New ListViewItem(infoInBed.Name) With {
                    .Tag = fileInBed,
                    .ImageKey = EnsureHostIconKey(fileInBed, directoryInBed:=False),
                    .ToolTipText = fileInBed
                }
                itemInBed.SubItems.Add(If(String.IsNullOrWhiteSpace(infoInBed.Extension), "File", infoInBed.Extension.TrimStart("."c).ToUpperInvariant()))
                itemInBed.SubItems.Add(FormatBytes(infoInBed.Length))
                _hostList.Items.Add(itemInBed)
            Next
        Finally
            _hostList.EndUpdate()
        End Try
    End Sub

    Private Sub HostList_ItemActivate(senderInBed As Object, eInBed As EventArgs)
        If _hostList.SelectedItems.Count = 0 Then Return
        Dim pathInBed As String = TryCast(_hostList.SelectedItems(0).Tag, String)
        If Directory.Exists(pathInBed) Then
            NavigateHost(pathInBed)
        ElseIf File.Exists(pathInBed) Then
            Dim extensionInBed As String = Path.GetExtension(pathInBed)
            If extensionInBed.Equals(".img", StringComparison.OrdinalIgnoreCase) OrElse
               extensionInBed.Equals(".ima", StringComparison.OrdinalIgnoreCase) Then
                OpenImage(pathInBed)
            Else
                AddPathsToActiveImage({pathInBed})
            End If
        End If
    End Sub

    Private Sub HostList_ItemDrag(senderInBed As Object, eInBed As ItemDragEventArgs)
        Dim pathsInBed As New List(Of String)()
        For Each itemInBed As ListViewItem In _hostList.SelectedItems
            Dim pathInBed As String = TryCast(itemInBed.Tag, String)
            If File.Exists(pathInBed) OrElse Directory.Exists(pathInBed) Then pathsInBed.Add(pathInBed)
        Next
        If pathsInBed.Count = 0 Then Return
        Dim dataInBed As New DataObject(DataFormats.FileDrop, pathsInBed.ToArray())
        _hostList.DoDragDrop(dataInBed, DragDropEffects.Copy)
    End Sub

    Private Sub HostList_DragEnter(senderInBed As Object, eInBed As DragEventArgs)
        If _dragEntries IsNot Nothing AndAlso _dragEntries.Count > 0 Then
            eInBed.Effect = DragDropEffects.Copy
        Else
            eInBed.Effect = DragDropEffects.None
        End If
    End Sub

    Private Sub HostList_DragDrop(senderInBed As Object, eInBed As DragEventArgs)
        If _dragSession Is Nothing OrElse _dragEntries Is Nothing OrElse _dragEntries.Count = 0 Then Return
        Try
            For Each entryInBed As SneakerNetDiskEntry In _dragEntries
                _dragSession.Document.ExtractEntry(entryInBed, _hostPath)
            Next
            RefreshHostList()
            SetStatus(_dragEntries.Count.ToString() & " item(s) extracted to " & _hostPath)
        Catch ex As Exception
            SetError(ex.Message)
        Finally
            _dragSession = Nothing
            _dragEntries = Nothing
        End Try
    End Sub

    Private Sub ChooseAndOpenImage()
        Using pickerInBed As New OpenFileDialog()
            pickerInBed.Title = "Open floppy image in Sneaker Net"
            pickerInBed.Filter = "Raw floppy images (*.img;*.ima)|*.img;*.ima|All files (*.*)|*.*"
            pickerInBed.Multiselect = True
            If pickerInBed.ShowDialog(Me) <> DialogResult.OK Then Return
            For Each pathInBed As String In pickerInBed.FileNames
                OpenImage(pathInBed)
            Next
        End Using
    End Sub

    Private Sub OpenImage(pathInBed As String)
        Try
            Dim fullInBed As String = Path.GetFullPath(pathInBed)
            For Each pageInBed As TabPage In _tabs.TabPages
                Dim existingInBed As SneakerNetImageSession = TryCast(pageInBed.Tag, SneakerNetImageSession)
                If existingInBed IsNot Nothing AndAlso existingInBed.Document.ImagePath.Equals(fullInBed, StringComparison.OrdinalIgnoreCase) Then
                    _tabs.SelectedTab = pageInBed
                    RefreshSession(existingInBed)
                    Return
                End If
            Next

            Dim sessionInBed As New SneakerNetImageSession()
            sessionInBed.Document = New Fat12ImageDocument(fullInBed)
            sessionInBed.Page = New TabPage(Path.GetFileName(fullInBed))
            sessionInBed.List = New ListView()
            ConfigureFileList(sessionInBed.List)
            sessionInBed.List.Dock = DockStyle.Fill
            sessionInBed.List.Columns.Add("Disk Image", 220)
            sessionInBed.List.Columns.Add("Size", 85, HorizontalAlignment.Right)
            sessionInBed.List.Columns.Add("Attr", 55)
            sessionInBed.List.Columns.Add("Modified", 135)
            sessionInBed.List.AllowDrop = True
            sessionInBed.List.LabelEdit = True
            sessionInBed.Page.Tag = sessionInBed
            sessionInBed.Page.Controls.Add(sessionInBed.List)
            AddHandler sessionInBed.List.DragEnter, AddressOf ImageList_DragEnter
            AddHandler sessionInBed.List.DragDrop, AddressOf ImageList_DragDrop
            AddHandler sessionInBed.List.ItemActivate, AddressOf ImageList_ItemActivate
            AddHandler sessionInBed.List.ItemDrag, AddressOf ImageList_ItemDrag
            AddHandler sessionInBed.List.AfterLabelEdit, AddressOf ImageList_AfterLabelEdit
            AddHandler sessionInBed.List.SelectedIndexChanged, Sub() RefreshActionState()
            AddHandler sessionInBed.List.KeyDown, AddressOf ImageList_KeyDown
            _tabs.TabPages.Add(sessionInBed.Page)
            _tabs.SelectedTab = sessionInBed.Page
            RefreshSession(sessionInBed)
            StartWatchingImageSession(sessionInBed)
            SetStatus("Opened " & fullInBed)
        Catch ex As Exception
            SetError(ex.Message)
        End Try
    End Sub

    Private Async Sub BackupActiveFloppyClickedInBed(senderInBed As Object, eInBed As EventArgs)
        Dim sessionInBed As SneakerNetImageSession = ActiveSession()
        If sessionInBed Is Nothing Then
            SetError("Open a floppy image first.")
            Return
        End If
        Try
            SetStatus("Checking floppy backup history...")
            Dim resultInBed As MediaBackupResult
            Using leaseInBed As IDisposable = AcquireMediaQuiesceInBed()
                resultInBed = Await Task.Run(Function() MediaBackupArchive.BackupIfChanged(sessionInBed.Document.ImagePath, "Floppy-Box"))
            End Using
            SetStatus(resultInBed.Message & "  " & resultInBed.DestinationPath)
            RefreshBackupBrowserInBed()
        Catch ex As Exception
            SetError(ex.Message)
        End Try
    End Sub

    Private Function ActiveSession() As SneakerNetImageSession
        If _tabs.SelectedTab Is Nothing Then Return Nothing
        Return TryCast(_tabs.SelectedTab.Tag, SneakerNetImageSession)
    End Function

    Private Sub RefreshSession(sessionInBed As SneakerNetImageSession)
        If sessionInBed Is Nothing Then Return
        sessionInBed.Document.Reload()
        sessionInBed.List.BeginUpdate()
        Try
            sessionInBed.List.Items.Clear()
            If sessionInBed.CurrentDirectoryCluster <> 0 Then
                Dim parentItemInBed As New ListViewItem("..") With {
                    .Tag = Nothing,
                    .ToolTipText = "Up one directory"
                }
                parentItemInBed.SubItems.Add(String.Empty)
                parentItemInBed.SubItems.Add("D")
                parentItemInBed.SubItems.Add(String.Empty)
                sessionInBed.List.Items.Add(parentItemInBed)
            End If

            For Each entryInBed As SneakerNetDiskEntry In sessionInBed.Document.GetEntries(sessionInBed.CurrentDirectoryCluster)
                Dim itemInBed As New ListViewItem(entryInBed.Name) With {.Tag = entryInBed}
                itemInBed.SubItems.Add(If(entryInBed.IsDirectory, String.Empty, FormatBytes(entryInBed.Size)))
                itemInBed.SubItems.Add(entryInBed.AttributeText)
                itemInBed.SubItems.Add(entryInBed.Modified.ToString("yyyy-MM-dd HH:mm"))
                sessionInBed.List.Items.Add(itemInBed)
            Next
        Finally
            sessionInBed.List.EndUpdate()
        End Try
        If sessionInBed.List.Columns.Count > 0 Then
            sessionInBed.List.Columns(0).Text =
                "Disk Image " & If(String.IsNullOrWhiteSpace(sessionInBed.CurrentDirectoryPath), "\", sessionInBed.CurrentDirectoryPath)
        End If
        sessionInBed.Page.Text = Path.GetFileName(sessionInBed.Document.ImagePath)
        RefreshActionState()
    End Sub

    Private Sub ImageList_ItemActivate(senderInBed As Object, eInBed As EventArgs)
        Dim sessionInBed As SneakerNetImageSession = ActiveSession()
        If sessionInBed Is Nothing OrElse sessionInBed.List.SelectedItems.Count = 0 Then Return

        Dim selectedInBed As ListViewItem = sessionInBed.List.SelectedItems(0)
        Dim entryInBed As SneakerNetDiskEntry = TryCast(selectedInBed.Tag, SneakerNetDiskEntry)

        If entryInBed Is Nothing AndAlso selectedInBed.Text = ".." Then
            If sessionInBed.ParentDirectoryClusters.Count > 0 Then
                sessionInBed.CurrentDirectoryCluster = sessionInBed.ParentDirectoryClusters.Pop()
                sessionInBed.CurrentDirectoryPath = sessionInBed.ParentDirectoryPaths.Pop()
                RefreshSession(sessionInBed)
            End If
            Return
        End If

        If entryInBed Is Nothing OrElse Not entryInBed.IsDirectory Then Return
        sessionInBed.ParentDirectoryClusters.Push(sessionInBed.CurrentDirectoryCluster)
        sessionInBed.ParentDirectoryPaths.Push(sessionInBed.CurrentDirectoryPath)
        sessionInBed.CurrentDirectoryCluster = entryInBed.FirstCluster
        If sessionInBed.CurrentDirectoryPath = "\" Then
            sessionInBed.CurrentDirectoryPath = "\" & entryInBed.Name
        Else
            sessionInBed.CurrentDirectoryPath = sessionInBed.CurrentDirectoryPath.TrimEnd("\"c) & "\" & entryInBed.Name
        End If
        RefreshSession(sessionInBed)
    End Sub

    Private Sub CloseActiveImage()
        CloseImagePage(_tabs.SelectedTab)
    End Sub

    Private Sub CloseImagePage(pageInBed As TabPage)
        If pageInBed Is Nothing Then Return

        Dim sessionInBed As SneakerNetImageSession = TryCast(pageInBed.Tag, SneakerNetImageSession)
        If sessionInBed IsNot Nothing AndAlso Object.ReferenceEquals(_tabRenameSession, sessionInBed) Then
            CancelTabRenameEdit()
        End If
        DisposeImageWatcher(sessionInBed)
        _tabs.TabPages.Remove(pageInBed)
        pageInBed.Dispose()

        If _tabs.TabPages.Count = 0 Then HideActionPanel()
        RefreshActionState()
        _tabs.Invalidate()
    End Sub

    Private Sub StartWatchingImageSession(sessionInBed As SneakerNetImageSession)
        If sessionInBed Is Nothing OrElse sessionInBed.Document Is Nothing Then Return

        DisposeImageWatcher(sessionInBed)

        Dim pathInBed As String = sessionInBed.Document.ImagePath
        Dim directoryInBed As String = Path.GetDirectoryName(pathInBed)
        Dim fileNameInBed As String = Path.GetFileName(pathInBed)
        If String.IsNullOrWhiteSpace(directoryInBed) OrElse
           String.IsNullOrWhiteSpace(fileNameInBed) OrElse
           Not Directory.Exists(directoryInBed) Then Return

        Dim watcherInBed As New FileSystemWatcher(directoryInBed, fileNameInBed) With {
            .IncludeSubdirectories = False,
            .NotifyFilter = NotifyFilters.FileName,
            .EnableRaisingEvents = False
        }

        AddHandler watcherInBed.Deleted,
            Sub(senderInBed As Object, eInBed As FileSystemEventArgs)
                QueueMissingImageSession(sessionInBed, "deleted outside Sneaker Net")
            End Sub
        AddHandler watcherInBed.Renamed,
            Sub(senderInBed As Object, eInBed As RenamedEventArgs)
                QueueMissingImageSession(sessionInBed, "renamed or moved outside Sneaker Net")
            End Sub

        sessionInBed.Watcher = watcherInBed
        watcherInBed.EnableRaisingEvents = True

        ' Close the race between the initial image load and enabling the watcher.
        If Not File.Exists(pathInBed) Then
            QueueMissingImageSession(sessionInBed, "deleted outside Sneaker Net")
        End If
    End Sub

    Private Sub DisposeImageWatcher(sessionInBed As SneakerNetImageSession)
        If sessionInBed Is Nothing Then Return

        Dim watcherInBed As FileSystemWatcher = sessionInBed.Watcher
        sessionInBed.Watcher = Nothing
        If watcherInBed Is Nothing Then Return

        Try
            watcherInBed.EnableRaisingEvents = False
        Catch
        End Try
        watcherInBed.Dispose()
    End Sub

    Private Sub DisposeAllImageWatchers()
        For Each pageInBed As TabPage In _tabs.TabPages
            DisposeImageWatcher(TryCast(pageInBed.Tag, SneakerNetImageSession))
        Next
    End Sub

    Private Sub QueueMissingImageSession(sessionInBed As SneakerNetImageSession,
                                         reasonInBed As String)
        If sessionInBed Is Nothing OrElse IsDisposed OrElse Not IsHandleCreated Then Return

        Try
            BeginInvoke(
                New Action(
                    Sub()
                        HandleMissingImageSession(sessionInBed, reasonInBed)
                    End Sub))
        Catch ex As ObjectDisposedException
            ' The form is already closing; its watcher will be disposed there.
        Catch ex As InvalidOperationException
            ' Same shutdown race, different WinForms exception.
        End Try
    End Sub

    Private Sub HandleMissingImageSession(sessionInBed As SneakerNetImageSession,
                                          reasonInBed As String)
        If sessionInBed Is Nothing OrElse sessionInBed.Page Is Nothing Then Return
        If Not _tabs.TabPages.Contains(sessionInBed.Page) Then Return

        Dim pathInBed As String = sessionInBed.Document.ImagePath
        If File.Exists(pathInBed) Then Return

        Dim ejectedInBed As New List(Of String)()
        If _mountedPathA IsNot Nothing AndAlso
           _mountedPathA.Equals(pathInBed, StringComparison.OrdinalIgnoreCase) Then
            Try
                If _ejectDrive IsNot Nothing Then _ejectDrive(0)
            Catch
            End Try
            _mountedPathA = Nothing
            ejectedInBed.Add("A:")
        End If
        If _mountedPathB IsNot Nothing AndAlso
           _mountedPathB.Equals(pathInBed, StringComparison.OrdinalIgnoreCase) Then
            Try
                If _ejectDrive IsNot Nothing Then _ejectDrive(1)
            Catch
            End Try
            _mountedPathB = Nothing
            ejectedInBed.Add("B:")
        End If

        Dim oldNameInBed As String = Path.GetFileName(pathInBed)
        Dim pageInBed As TabPage = sessionInBed.Page
        If Object.ReferenceEquals(_tabRenameSession, sessionInBed) Then CancelTabRenameEdit()
        DisposeImageWatcher(sessionInBed)
        _tabs.TabPages.Remove(pageInBed)
        pageInBed.Dispose()

        If _tabs.TabPages.Count = 0 Then HideActionPanel()
        RefreshActionState()

        Try
            If Not String.IsNullOrWhiteSpace(_hostPath) AndAlso
               Directory.Exists(_hostPath) AndAlso
               String.Equals(Path.GetFullPath(_hostPath),
                             Path.GetFullPath(Path.GetDirectoryName(pathInBed)),
                             StringComparison.OrdinalIgnoreCase) Then
                RefreshHostList()
            End If
        Catch
        End Try

        Dim messageInBed As String =
            oldNameInBed & " was " & reasonInBed & "; its image tab was closed."
        If ejectedInBed.Count > 0 Then
            messageInBed &= " Ejected " & String.Join(" and ", ejectedInBed) & "."
        End If
        SetError(messageInBed)
    End Sub

    Private Sub WireExternalImageDropSurfaces()
        Dim targetsInBed As Control() = {
            _imageWorkspacePanel,
            _imageActivePanel,
            _emptyImagePanel,
            _imageHeader,
            _tabs
        }
        For Each targetInBed As Control In targetsInBed
            targetInBed.AllowDrop = True
            AddHandler targetInBed.DragEnter, AddressOf ImageList_DragEnter
            AddHandler targetInBed.DragDrop, AddressOf ImageSurface_DragDrop
        Next
        WireDropChildren(_emptyImagePanel)
    End Sub

    Private Sub WireDropChildren(parentInBed As Control)
        For Each childInBed As Control In parentInBed.Controls
            If TypeOf childInBed Is TextBoxBase Then Continue For
            childInBed.AllowDrop = True
            AddHandler childInBed.DragEnter, AddressOf ImageList_DragEnter
            AddHandler childInBed.DragDrop, AddressOf ImageSurface_DragDrop
            If childInBed.HasChildren Then WireDropChildren(childInBed)
        Next
    End Sub

    Private Sub ImageSurface_DragDrop(senderInBed As Object, eInBed As DragEventArgs)
        HandleExternalImageDrop(eInBed)
    End Sub

    Private Sub HandleExternalImageDrop(eInBed As DragEventArgs)
        If eInBed.Data Is Nothing OrElse Not eInBed.Data.GetDataPresent(DataFormats.FileDrop) Then Return
        Dim droppedInBed As String() = TryCast(eInBed.Data.GetData(DataFormats.FileDrop), String())
        If droppedInBed Is Nothing Then Return
        AcceptExternalPayload(droppedInBed)
    End Sub

    Private Sub AcceptExternalPayload(pathsInBed As IEnumerable(Of String))
        Dim payloadPathsInBed As String() = pathsInBed.
            Where(Function(pathInBed) Not String.IsNullOrWhiteSpace(pathInBed) AndAlso
                  (File.Exists(pathInBed) OrElse Directory.Exists(pathInBed))).
            Select(Function(pathInBed) Path.GetFullPath(pathInBed)).
            Distinct(StringComparer.OrdinalIgnoreCase).
            ToArray()
        If payloadPathsInBed.Length = 0 Then Return

        Dim sessionInBed As SneakerNetImageSession = ActiveSession()
        If sessionInBed IsNot Nothing Then
            If IsActiveImageMounted() Then
                SetError("That image is mounted. Eject it before dropping files into its filesystem.")
                Return
            End If
            If sessionInBed.Document.CanAddHostPaths(payloadPathsInBed, sessionInBed.CurrentDirectoryCluster) Then
                AddPathsToActiveImage(payloadPathsInBed)
                Return
            End If
        End If

        Dim selectedFormatInBed As Fat12FloppyFormat = SelectedNewDiskFormat()
        If selectedFormatInBed Is Nothing Then
            SetError("Choose a floppy format in the New Disk panel first.")
            Return
        End If

        Dim routeFormatInBed As Fat12FloppyFormat = selectedFormatInBed
        If payloadPathsInBed.Any(Function(pathInBed) Not Fat12FloppyBuilder.FitsHostPaths(New String() {pathInBed}, routeFormatInBed)) Then
            routeFormatInBed = Fat12FloppyBuilder.GetFormats().FirstOrDefault(
                Function(formatInBed) payloadPathsInBed.All(
                    Function(pathInBed) Fat12FloppyBuilder.FitsHostPaths(New String() {pathInBed}, formatInBed)))
        End If

        If routeFormatInBed Is Nothing Then
            If payloadPathsInBed.Any(Function(pathInBed) Directory.Exists(pathInBed)) Then
                SetError("A selected directory is too large for any supported floppy. Optical directory authoring is pinned for the CD/DVD Shelf, so Sneaker Net did not flatten or discard the folder tree.")
            Else
                AcceptPayloadAsIso(payloadPathsInBed)
            End If
            Return
        End If

        Dim groupsInBed As List(Of List(Of String)) =
            PackSequentialFloppySet(payloadPathsInBed, routeFormatInBed)
        If groupsInBed Is Nothing OrElse groupsInBed.Count = 0 Then
            If payloadPathsInBed.Any(Function(pathInBed) Directory.Exists(pathInBed)) Then
                SetError("The selected directory payload does not fit the available floppy formats as intact top-level folders.")
            Else
                AcceptPayloadAsIso(payloadPathsInBed)
            End If
            Return
        End If

        Dim baseLabelInBed As String = SuggestedPayloadLabel(payloadPathsInBed)
        Try
            Dim createdInBed As List(Of String) =
                CreateFloppyPayloadSet(groupsInBed, baseLabelInBed, routeFormatInBed)
            For Each imagePathInBed As String In createdInBed
                OpenImage(imagePathInBed)
            Next
            Dim promotedInBed As String =
                If(routeFormatInBed Is selectedFormatInBed,
                   String.Empty,
                   " (promoted to " & routeFormatInBed.DisplayName & ")")
            SetStatus(payloadPathsInBed.Length.ToString() & " top-level item(s) accepted as " &
                      createdInBed.Count.ToString() & " floppy image(s)" & promotedInBed & ".")
        Catch ex As Exception
            SetError(ex.Message)
        End Try
    End Sub

    Private Function SelectedNewDiskFormat() As Fat12FloppyFormat
        Return TryCast(_newDiskFormatBox.SelectedItem, Fat12FloppyFormat)
    End Function

    Private Shared Function PackSequentialFloppySet(filesInBed As IEnumerable(Of String), formatInBed As Fat12FloppyFormat) As List(Of List(Of String))
        Dim resultInBed As New List(Of List(Of String))()
        Dim currentInBed As New List(Of String)()

        For Each pathInBed As String In filesInBed
            If Not Fat12FloppyBuilder.FitsHostPaths(New String() {pathInBed}, formatInBed) Then Return Nothing
            Dim candidateInBed As New List(Of String)(currentInBed)
            candidateInBed.Add(pathInBed)
            If Fat12FloppyBuilder.FitsHostPaths(candidateInBed.ToArray(), formatInBed) Then
                currentInBed.Add(pathInBed)
            Else
                If currentInBed.Count > 0 Then resultInBed.Add(currentInBed)
                currentInBed = New List(Of String) From {pathInBed}
            End If
        Next
        If currentInBed.Count > 0 Then resultInBed.Add(currentInBed)
        Return resultInBed
    End Function

    Private Function CreateFloppyPayloadSet(groupsInBed As List(Of List(Of String)),
                                            labelInBed As String,
                                            formatInBed As Fat12FloppyFormat) As List(Of String)
        Dim resultInBed As New List(Of String)()
        If groupsInBed.Count = 1 Then
            Dim singlePathInBed As String = _box.CreateUniqueImagePath(labelInBed)
            CreateFloppyImageFromHostPaths(groupsInBed(0), singlePathInBed, labelInBed, formatInBed)
            resultInBed.Add(singlePathInBed)
            Return resultInBed
        End If

        Dim pathsInBed As List(Of String) = _box.CreateUniqueImageSetPaths(labelInBed, groupsInBed.Count)
        For indexInBed As Integer = 0 To groupsInBed.Count - 1
            Dim diskLabelInBed As String = labelInBed & " " & (indexInBed + 1).ToString()
            CreateFloppyImageFromHostPaths(groupsInBed(indexInBed), pathsInBed(indexInBed), diskLabelInBed, formatInBed)
            resultInBed.Add(pathsInBed(indexInBed))
        Next
        Return resultInBed
    End Function

    Private Shared Sub CreateFloppyImageFromHostPaths(pathsInBed As IEnumerable(Of String),
                                                       destinationPathInBed As String,
                                                       labelInBed As String,
                                                       formatInBed As Fat12FloppyFormat)
        Fat12FloppyBuilder.CreateImage(Array.Empty(Of String)(), destinationPathInBed, labelInBed, formatInBed)
        Try
            Dim documentInBed As New Fat12ImageDocument(destinationPathInBed)
            documentInBed.AddHostPaths(pathsInBed, 0)
            documentInBed.Save()
        Catch
            Try
                If File.Exists(destinationPathInBed) Then File.Delete(destinationPathInBed)
            Catch
            End Try
            Throw
        End Try
    End Sub

    Private Function SuggestedPayloadLabel(filesInBed As String()) As String
        Dim configuredInBed As String = If(_newDiskLabelBox.Text, String.Empty).Trim()
        If configuredInBed.Length > 0 AndAlso Not configuredInBed.Equals("New Disk", StringComparison.OrdinalIgnoreCase) Then
            Return configuredInBed
        End If
        Dim firstInBed As String =
            If(Directory.Exists(filesInBed(0)),
               New DirectoryInfo(filesInBed(0)).Name,
               Path.GetFileNameWithoutExtension(filesInBed(0)))
        If filesInBed.Length = 1 Then Return If(String.IsNullOrWhiteSpace(firstInBed), "Sneaker Net", firstInBed)
        Return If(String.IsNullOrWhiteSpace(firstInBed), "Sneaker Net Set", firstInBed & " Set")
    End Function

    Private Function DiscBoxPath() As String
        Dim baseInBed As String = Path.GetDirectoryName(_box.RootPath)
        If String.IsNullOrWhiteSpace(baseInBed) Then baseInBed = _box.RootPath
        Dim pathInBed As String = Path.Combine(baseInBed, "Disc-Box")
        Directory.CreateDirectory(pathInBed)
        Return pathInBed
    End Function

    Private Function CreateUniqueIsoPath(labelInBed As String) As String
        Dim rootInBed As String = DiscBoxPath()
        Dim safeInBed As String = FloppyBox.SanitizeHostLabel(labelInBed)
        Dim candidateInBed As String = Path.Combine(rootInBed, safeInBed & ".iso")
        If Not File.Exists(candidateInBed) Then Return candidateInBed
        For suffixInBed As Integer = 2 To 999999
            candidateInBed = Path.Combine(rootInBed, safeInBed & " (" & suffixInBed.ToString() & ").iso")
            If Not File.Exists(candidateInBed) Then Return candidateInBed
        Next
        Throw New IOException("Could not create a unique ISO filename.")
    End Function

    Private Sub AcceptPayloadAsIso(filesInBed As String())
        Try
            Dim labelInBed As String = SuggestedPayloadLabel(filesInBed)
            Dim isoPathInBed As String = CreateUniqueIsoPath(labelInBed)
            If filesInBed.Length = 1 AndAlso Path.GetExtension(filesInBed(0)).Equals(".iso", StringComparison.OrdinalIgnoreCase) Then
                File.Copy(filesInBed(0), isoPathInBed)
            Else
                SneakerNetIso9660Builder.CreateImage(filesInBed, isoPathInBed, labelInBed)
            End If
            _lastCreatedIsoPath = isoPathInBed
            NavigateHost(DiscBoxPath())
            SetError("Payload contains a file too large for any supported floppy. ISO required; accepted as " & Path.GetFileName(isoPathInBed) & ".")
        Catch ex As Exception
            SetError(ex.Message)
        End Try
    End Sub

    Private Sub ChooseIsoToSpanPkZip204()
        Dim selectedFormatInBed As Fat12FloppyFormat = SelectedNewDiskFormat()
        If selectedFormatInBed Is Nothing Then
            SetError("Choose a floppy format first.")
            Return
        End If

        Using pickerInBed As New OpenFileDialog()
            pickerInBed.Title = "Span ISO to floppy set - PKZIP 2.04g compatible"
            pickerInBed.Filter = "ISO images (*.iso)|*.iso|All files (*.*)|*.*"
            pickerInBed.Multiselect = False
            pickerInBed.InitialDirectory = DiscBoxPath()
            If Not String.IsNullOrWhiteSpace(_lastCreatedIsoPath) AndAlso File.Exists(_lastCreatedIsoPath) Then
                pickerInBed.FileName = Path.GetFileName(_lastCreatedIsoPath)
            End If
            If pickerInBed.ShowDialog(Me) <> DialogResult.OK Then Return
            SpanFilesPkZip204(New String() {pickerInBed.FileName}, selectedFormatInBed)
        End Using
    End Sub

    Private Sub SpanFilesPkZip204(filesInBed As IEnumerable(Of String), formatInBed As Fat12FloppyFormat)
        Dim sourcesInBed As String() = filesInBed.Where(Function(pathInBed) File.Exists(pathInBed)).ToArray()
        If sourcesInBed.Length = 0 Then Return
        Try
            Dim labelInBed As String = Path.GetFileNameWithoutExtension(sourcesInBed(0))
            Dim imagesInBed As List(Of String) = PkZip204Spanner.CreateFloppySet(sourcesInBed, _box, labelInBed, formatInBed)
            For Each imagePathInBed As String In imagesInBed
                OpenImage(imagePathInBed)
            Next
            SetStatus("PKZIP 2.04g-compatible span set created: " & imagesInBed.Count.ToString() & " disk(s), " & formatInBed.DisplayName & ".")
        Catch ex As Exception
            SetError(ex.Message)
        End Try
    End Sub

    Private Sub OpenDiscBoxFolder()
        Try
            Process.Start(New ProcessStartInfo(DiscBoxPath()) With {.UseShellExecute = True})
        Catch ex As Exception
            SetError(ex.Message)
        End Try
    End Sub

    Private Sub AddSelectedHostPathsToImage()
        Dim sessionInBed As SneakerNetImageSession = ActiveSession()
        If sessionInBed Is Nothing Then
            SetError("Open or create a floppy image first.")
            Return
        End If

        Dim selectedPathsInBed As New List(Of String)()
        For Each itemInBed As ListViewItem In _hostList.SelectedItems
            Dim pathInBed As String = TryCast(itemInBed.Tag, String)
            If File.Exists(pathInBed) OrElse Directory.Exists(pathInBed) Then
                selectedPathsInBed.Add(pathInBed)
            End If
        Next
        If selectedPathsInBed.Count = 0 Then
            SetError("Select one or more host files or directories first.")
            Return
        End If

        AddPathsToActiveImage(selectedPathsInBed)
    End Sub

    Private Sub ChooseFilesToAdd()
        If ActiveSession() Is Nothing Then
            SetError("Open or create a floppy image first.")
            Return
        End If
        Using pickerInBed As New OpenFileDialog()
            pickerInBed.Title = "Add files to the current floppy image"
            pickerInBed.Filter = "All files (*.*)|*.*"
            pickerInBed.Multiselect = True
            If pickerInBed.ShowDialog(Me) <> DialogResult.OK Then Return
            AddPathsToActiveImage(pickerInBed.FileNames)
        End Using
    End Sub

    Private Sub AddPathsToActiveImage(pathsInBed As IEnumerable(Of String))
        Dim sessionInBed As SneakerNetImageSession = ActiveSession()
        If sessionInBed Is Nothing Then
            SetError("Open or create a floppy image first.")
            Return
        End If
        If IsActiveImageMounted() Then
            SetError("That image is mounted. Eject it before changing its filesystem.")
            Return
        End If
        Try
            Dim addedInBed As List(Of String) =
                sessionInBed.Document.AddHostPaths(pathsInBed, sessionInBed.CurrentDirectoryCluster)
            sessionInBed.Document.Save()
            RefreshSession(sessionInBed)
            SetStatus(addedInBed.Count.ToString() & " item(s) added")
        Catch ex As Exception
            Try
                sessionInBed.Document.Reload()
            Catch
            End Try
            SetError(ex.Message)
        End Try
    End Sub

    Private Sub ExtractSelectedToHost()
        Dim sessionInBed As SneakerNetImageSession = ActiveSession()
        If sessionInBed Is Nothing OrElse sessionInBed.List.SelectedItems.Count = 0 Then
            SetError("Select one or more files in the disk image.")
            Return
        End If
        Try
            Dim countInBed As Integer = 0
            For Each itemInBed As ListViewItem In sessionInBed.List.SelectedItems
                Dim entryInBed As SneakerNetDiskEntry = TryCast(itemInBed.Tag, SneakerNetDiskEntry)
                If entryInBed IsNot Nothing Then
                    sessionInBed.Document.ExtractEntry(entryInBed, _hostPath)
                    countInBed += 1
                End If
            Next
            RefreshHostList()
            SetStatus(countInBed.ToString() & " item(s) extracted to " & _hostPath)
        Catch ex As Exception
            SetError(ex.Message)
        End Try
    End Sub

    Private Sub ImageList_DragEnter(senderInBed As Object, eInBed As DragEventArgs)
        If eInBed.Data IsNot Nothing AndAlso eInBed.Data.GetDataPresent(DataFormats.FileDrop) Then
            eInBed.Effect = DragDropEffects.Copy
        Else
            eInBed.Effect = DragDropEffects.None
        End If
    End Sub

    Private Sub ImageList_DragDrop(senderInBed As Object, eInBed As DragEventArgs)
        HandleExternalImageDrop(eInBed)
    End Sub

    Private Sub ImageList_ItemDrag(senderInBed As Object, eInBed As ItemDragEventArgs)
        Dim sessionInBed As SneakerNetImageSession = ActiveSession()
        If sessionInBed Is Nothing Then Return
        Dim entriesInBed As New List(Of SneakerNetDiskEntry)()
        For Each itemInBed As ListViewItem In sessionInBed.List.SelectedItems
            Dim entryInBed As SneakerNetDiskEntry = TryCast(itemInBed.Tag, SneakerNetDiskEntry)
            If entryInBed IsNot Nothing Then entriesInBed.Add(entryInBed)
        Next
        If entriesInBed.Count = 0 Then Return
        _dragSession = sessionInBed
        _dragEntries = entriesInBed
        sessionInBed.List.DoDragDrop("SneakerNetImageEntries", DragDropEffects.Copy)
        _dragSession = Nothing
        _dragEntries = Nothing
    End Sub

    Private Sub ImageList_KeyDown(senderInBed As Object, eInBed As KeyEventArgs)
        Dim listInBed As ListView = TryCast(senderInBed, ListView)
        If listInBed Is Nothing Then Return
        If eInBed.KeyCode = Keys.F2 AndAlso
           listInBed.SelectedItems.Count = 1 AndAlso
           TypeOf listInBed.SelectedItems(0).Tag Is SneakerNetDiskEntry Then
            listInBed.SelectedItems(0).BeginEdit()
            eInBed.Handled = True
        ElseIf eInBed.KeyCode = Keys.Delete Then
            DeleteSelectedImageEntries()
            eInBed.Handled = True
        ElseIf eInBed.Control AndAlso eInBed.KeyCode = Keys.A Then
            For Each itemInBed As ListViewItem In listInBed.Items
                itemInBed.Selected = True
            Next
            eInBed.Handled = True
        End If
    End Sub

    Private Sub ImageList_AfterLabelEdit(senderInBed As Object, eInBed As LabelEditEventArgs)
        If eInBed.Label Is Nothing Then Return
        Dim sessionInBed As SneakerNetImageSession = ActiveSession()
        If sessionInBed Is Nothing Then Return
        If IsActiveImageMounted() Then
            eInBed.CancelEdit = True
            SetError("Eject the image before renaming files.")
            Return
        End If
        Try
            Dim itemInBed As ListViewItem = sessionInBed.List.Items(eInBed.Item)
            Dim entryInBed As SneakerNetDiskEntry = TryCast(itemInBed.Tag, SneakerNetDiskEntry)
            If entryInBed Is Nothing Then
                eInBed.CancelEdit = True
                Return
            End If
            sessionInBed.Document.RenameEntry(
                entryInBed,
                eInBed.Label,
                sessionInBed.CurrentDirectoryCluster)
            sessionInBed.Document.Save()
            BeginInvoke(New Action(Sub() RefreshSession(sessionInBed)))
            SetStatus("Renamed " & entryInBed.Name)
        Catch ex As Exception
            eInBed.CancelEdit = True
            Try
                sessionInBed.Document.Reload()
            Catch
            End Try
            SetError(ex.Message)
        End Try
    End Sub

    Private Sub DeleteSelectedImageEntries()
        Dim sessionInBed As SneakerNetImageSession = ActiveSession()
        If sessionInBed Is Nothing OrElse sessionInBed.List.SelectedItems.Count = 0 Then Return
        If IsActiveImageMounted() Then
            SetError("Eject the image before deleting files.")
            Return
        End If

        Dim entriesInBed As New List(Of SneakerNetDiskEntry)()
        For Each itemInBed As ListViewItem In sessionInBed.List.SelectedItems
            Dim entryInBed As SneakerNetDiskEntry = TryCast(itemInBed.Tag, SneakerNetDiskEntry)
            If entryInBed IsNot Nothing Then entriesInBed.Add(entryInBed)
        Next
        If entriesInBed.Count = 0 Then Return

        If MessageBox.Show(Me,
                           "Delete " & entriesInBed.Count.ToString() & " selected item(s) from the floppy image?" &
                           Environment.NewLine & "Directories are deleted recursively.",
                           "Sneaker Net - delete",
                           MessageBoxButtons.OKCancel,
                           MessageBoxIcon.Warning) <> DialogResult.OK Then Return
        Try
            For Each entryInBed As SneakerNetDiskEntry In entriesInBed
                sessionInBed.Document.DeleteEntry(
                    entryInBed,
                    sessionInBed.CurrentDirectoryCluster)
            Next
            sessionInBed.Document.Save()
            RefreshSession(sessionInBed)
            SetStatus(entriesInBed.Count.ToString() & " item(s) deleted")
        Catch ex As Exception
            Try
                sessionInBed.Document.Reload()
            Catch
            End Try
            SetError(ex.Message)
        End Try
    End Sub

    Private Sub CreateConfiguredNewDisk()
        Try
            Dim formatInBed As Fat12FloppyFormat = TryCast(_newDiskFormatBox.SelectedItem, Fat12FloppyFormat)
            If formatInBed Is Nothing Then
                SetError("Choose a floppy format in the New Disk configuration.")
                Return
            End If

            Dim labelInBed As String = _newDiskLabelBox.Text.Trim()
            If String.IsNullOrWhiteSpace(labelInBed) Then labelInBed = "New Disk"

            Dim pathInBed As String = _box.CreateUniqueImagePath(labelInBed)
            Fat12FloppyBuilder.CreateImage(Array.Empty(Of String)(), pathInBed, labelInBed, formatInBed)
            OpenImage(pathInBed)
            SetStatus(Path.GetFileName(pathInBed) & " created — " & formatInBed.DisplayName)
        Catch ex As Exception
            SetError(ex.Message)
        End Try
    End Sub

    Private Sub ShowMakeBootablePanel()
        Dim sessionInBed As SneakerNetImageSession = ActiveSession()
        If sessionInBed Is Nothing Then
            SetError("Open or create a floppy image first.")
            Return
        End If
        ShowActionPanel("MAKE BOOTABLE", 145)
        Dim layoutInBed As New TableLayoutPanel() With {.Dock = DockStyle.Fill, .ColumnCount = 4, .RowCount = 2}
        layoutInBed.ColumnStyles.Add(New ColumnStyle(SizeType.Absolute, 105))
        layoutInBed.ColumnStyles.Add(New ColumnStyle(SizeType.Percent, 100))
        layoutInBed.ColumnStyles.Add(New ColumnStyle(SizeType.Absolute, 85))
        layoutInBed.ColumnStyles.Add(New ColumnStyle(SizeType.Absolute, 130))
        Dim sourceBoxInBed As New TextBox() With {.Dock = DockStyle.Fill}
        Dim browseInBed As New Button() With {.Text = "Browse...", .Dock = DockStyle.Fill}
        Dim applyInBed As New Button() With {.Text = "Build Boot Disk", .Dock = DockStyle.Fill}
        Dim closeInBed As New Button() With {.Text = "Close", .Width = 70}
        Dim helpInBed As New Label() With {
            .Text = "Choose a bootable MS-DOS/PC DOS floppy image you legally possess. Sneaker Net copies its boot loader and system files, while preserving this disk's geometry and current root files.",
            .Dock = DockStyle.Fill,
            .AutoEllipsis = True
        }
        layoutInBed.Controls.Add(New Label() With {.Text = "Boot source image", .Dock = DockStyle.Fill, .TextAlign = ContentAlignment.MiddleLeft}, 0, 0)
        layoutInBed.Controls.Add(sourceBoxInBed, 1, 0)
        layoutInBed.Controls.Add(browseInBed, 2, 0)
        layoutInBed.Controls.Add(applyInBed, 3, 0)
        layoutInBed.Controls.Add(helpInBed, 0, 1)
        layoutInBed.SetColumnSpan(helpInBed, 3)
        layoutInBed.Controls.Add(closeInBed, 3, 1)
        _actionPanel.Controls.Add(layoutInBed)
        AddHandler closeInBed.Click, Sub() HideActionPanel()
        AddHandler browseInBed.Click,
            Sub()
                Using pickerInBed As New OpenFileDialog()
                    pickerInBed.Title = "Choose a bootable DOS source floppy"
                    pickerInBed.Filter = "Raw floppy images (*.img;*.ima)|*.img;*.ima|All files (*.*)|*.*"
                    If pickerInBed.ShowDialog(Me) = DialogResult.OK Then sourceBoxInBed.Text = pickerInBed.FileName
                End Using
            End Sub
        AddHandler applyInBed.Click,
            Sub()
                If IsActiveImageMounted() Then
                    SetError("Eject the target image before installing boot files.")
                    Return
                End If
                Try
                    If Not File.Exists(sourceBoxInBed.Text) Then Throw New FileNotFoundException("Choose a bootable source floppy image.")
                    sessionInBed.Document.InstallDosBootFromSourceImage(sourceBoxInBed.Text)
                    RefreshSession(sessionInBed)
                    SetStatus("Boot machinery installed from " & Path.GetFileName(sourceBoxInBed.Text))
                Catch ex As Exception
                    Try
                        sessionInBed.Document.Reload()
                    Catch
                    End Try
                    SetError(ex.Message)
                End Try
            End Sub
    End Sub

    Private Sub ShowActionPanel(titleInBed As String, heightInBed As Integer)
        _actionPanel.Controls.Clear()
        _actionPanel.Height = heightInBed
        _actionPanel.BorderStyle = BorderStyle.FixedSingle
        SetStatus(titleInBed)
    End Sub

    Private Sub HideActionPanel()
        _actionPanel.Controls.Clear()
        _actionPanel.Height = 0
    End Sub

    Private Sub VerifyActiveImage()
        Dim sessionInBed As SneakerNetImageSession = ActiveSession()
        If sessionInBed Is Nothing Then
            SetError("Open an image first.")
            Return
        End If
        Try
            Dim resultInBed As SneakerNetVerificationResult = sessionInBed.Document.Verify()
            ShowTextResult("FILESYSTEM CHECK", resultInBed.Lines)
            SetStatus(If(resultInBed.IsValid, "Filesystem check passed", "Filesystem check found a problem"))
        Catch ex As Exception
            SetError(ex.Message)
        End Try
    End Sub

    Private Sub ShowImageProperties()
        Dim sessionInBed As SneakerNetImageSession = ActiveSession()
        If sessionInBed Is Nothing Then
            SetError("Open an image first.")
            Return
        End If
        Dim documentInBed As Fat12ImageDocument = sessionInBed.Document
        ShowTextResult("IMAGE PROPERTIES", {
            "Path: " & documentInBed.ImagePath,
            "Volume: " & If(String.IsNullOrWhiteSpace(documentInBed.VolumeLabel), "(none)", documentInBed.VolumeLabel),
            "Geometry: " & documentInBed.GeometryText,
            "Size: " & FormatBytes(documentInBed.TotalBytes),
            "Free: " & FormatBytes(documentInBed.FreeBytes),
            "Boot: " & documentInBed.BootStatusText
        })
    End Sub

    Private Sub ShowBootSector()
        Dim sessionInBed As SneakerNetImageSession = ActiveSession()
        If sessionInBed Is Nothing Then
            SetError("Open an image first.")
            Return
        End If
        Try
            Dim bytesInBed(511) As Byte
            Using streamInBed As New FileStream(sessionInBed.Document.ImagePath, FileMode.Open, FileAccess.Read, FileShare.ReadWrite)
                Dim readInBed As Integer = streamInBed.Read(bytesInBed, 0, bytesInBed.Length)
                If readInBed < 512 Then Throw New EndOfStreamException("Image is shorter than one sector.")
            End Using
            Dim linesInBed As New List(Of String)()
            For rowInBed As Integer = 0 To 31
                Dim offsetInBed As Integer = rowInBed * 16
                Dim hexInBed As New StringBuilder()
                Dim asciiInBed As New StringBuilder()
                For indexInBed As Integer = 0 To 15
                    Dim valueInBed As Byte = bytesInBed(offsetInBed + indexInBed)
                    hexInBed.Append(valueInBed.ToString("X2")).Append(" ")
                    asciiInBed.Append(If(valueInBed >= 32 AndAlso valueInBed <= 126, ChrW(valueInBed), "."c))
                Next
                linesInBed.Add(offsetInBed.ToString("X4") & "  " & hexInBed.ToString() & " " & asciiInBed.ToString())
            Next
            ShowTextResult("BOOT SECTOR (READ ONLY)", linesInBed, 245)
        Catch ex As Exception
            SetError(ex.Message)
        End Try
    End Sub

    Private Sub ShowTextResult(titleInBed As String, linesInBed As IEnumerable(Of String), Optional heightInBed As Integer = 180)
        ShowActionPanel(titleInBed, heightInBed)
        Dim boxInBed As New TextBox() With {
            .Dock = DockStyle.Fill,
            .Multiline = True,
            .ReadOnly = True,
            .ScrollBars = ScrollBars.Both,
            .WordWrap = False,
            .Font = New Font("Consolas", 9.0F),
            .Text = String.Join(Environment.NewLine, linesInBed)
        }
        Dim closeInBed As New Button() With {.Text = "Close", .Dock = DockStyle.Right, .Width = 70}
        AddHandler closeInBed.Click, Sub() HideActionPanel()
        _actionPanel.Controls.Add(boxInBed)
        _actionPanel.Controls.Add(closeInBed)
        closeInBed.BringToFront()
    End Sub

    Private Sub ToggleMount(driveInBed As Integer)
        Dim sessionInBed As SneakerNetImageSession = ActiveSession()
        If sessionInBed Is Nothing Then
            SetError("Open an image first.")
            Return
        End If
        Dim activePathInBed As String = sessionInBed.Document.ImagePath
        Dim mountedInBed As String = If(driveInBed = 0, _mountedPathA, _mountedPathB)
        Try
            If mountedInBed IsNot Nothing AndAlso mountedInBed.Equals(activePathInBed, StringComparison.OrdinalIgnoreCase) Then
                If _ejectDrive IsNot Nothing Then _ejectDrive(driveInBed)
                If driveInBed = 0 Then _mountedPathA = Nothing Else _mountedPathB = Nothing
                SetStatus("Ejected drive " & ChrW(AscW("A"c) + driveInBed))
            Else
                If _mountImage IsNot Nothing Then _mountImage(driveInBed, activePathInBed)
                If driveInBed = 0 Then _mountedPathA = activePathInBed Else _mountedPathB = activePathInBed
                SetStatus(Path.GetFileName(activePathInBed) & " mounted in " & ChrW(AscW("A"c) + driveInBed) & ":")
            End If
            RefreshActionState()
        Catch ex As Exception
            SetError(ex.Message)
        End Try
    End Sub

    Private Sub TestBootActiveImage()
        Dim sessionInBed As SneakerNetImageSession = ActiveSession()
        If sessionInBed Is Nothing Then
            SetError("Open an image first.")
            Return
        End If
        Try
            If _mountImage IsNot Nothing Then _mountImage(0, sessionInBed.Document.ImagePath)
            _mountedPathA = sessionInBed.Document.ImagePath
            If _resetMachine IsNot Nothing Then _resetMachine()
            SetStatus("Mounted " & Path.GetFileName(sessionInBed.Document.ImagePath) & " in A: and reset the virtual machine")
            RefreshActionState()
        Catch ex As Exception
            SetError(ex.Message)
        End Try
    End Sub

    Private Function IsActiveImageMounted() As Boolean
        Dim sessionInBed As SneakerNetImageSession = ActiveSession()
        If sessionInBed Is Nothing Then Return False
        Dim pathInBed As String = sessionInBed.Document.ImagePath
        Return (_mountedPathA IsNot Nothing AndAlso _mountedPathA.Equals(pathInBed, StringComparison.OrdinalIgnoreCase)) OrElse
               (_mountedPathB IsNot Nothing AndAlso _mountedPathB.Equals(pathInBed, StringComparison.OrdinalIgnoreCase))
    End Function

    Private Sub RefreshActionState()
        Dim sessionInBed As SneakerNetImageSession = ActiveSession()
        Dim hasImageInBed As Boolean = sessionInBed IsNot Nothing

        Dim hasHostSelectionInBed As Boolean = False
        For Each hostItemInBed As ListViewItem In _hostList.SelectedItems
            Dim hostPathInBed As String = TryCast(hostItemInBed.Tag, String)
            If File.Exists(hostPathInBed) OrElse Directory.Exists(hostPathInBed) Then
                hasHostSelectionInBed = True
                Exit For
            End If
        Next
        _addSelectedHostButton.Enabled =
            hasImageInBed AndAlso hasHostSelectionInBed AndAlso Not IsActiveImageMounted()

        Dim hasImageSelectionInBed As Boolean = False
        If hasImageInBed Then
            For Each imageItemInBed As ListViewItem In sessionInBed.List.SelectedItems
                If TypeOf imageItemInBed.Tag Is SneakerNetDiskEntry Then
                    hasImageSelectionInBed = True
                    Exit For
                End If
            Next
        End If
        _extractButton.Enabled = hasImageSelectionInBed
        _backupFloppyButtonInBed.Enabled = hasImageInBed
        _bootButton.Enabled = hasImageInBed
        _mountAButton.Enabled = hasImageInBed
        _mountBButton.Enabled = hasImageInBed
        _testBootButton.Enabled = hasImageInBed

        If _checkFilesystemButton IsNot Nothing Then _checkFilesystemButton.Enabled = hasImageInBed
        If _imagePropertiesButton IsNot Nothing Then _imagePropertiesButton.Enabled = hasImageInBed
        If _bootSectorButton IsNot Nothing Then _bootSectorButton.Enabled = hasImageInBed
        If _openFloppyBoxButton IsNot Nothing Then _openFloppyBoxButton.Enabled = True
        If _openDiscBoxButton IsNot Nothing Then _openDiscBoxButton.Enabled = True
        If _spanPkzip204Button IsNot Nothing Then _spanPkzip204Button.Enabled = True

        _imageActivePanel.Visible = hasImageInBed
        _emptyImagePanel.Visible = Not hasImageInBed
        _imageCloseButton.Enabled = hasImageInBed

        If hasImageInBed Then
            _imageActivePanel.BringToFront()
            Dim pathInBed As String = sessionInBed.Document.ImagePath
            Dim mountedInBed As Boolean = IsImagePathMounted(pathInBed)

            _mountAButton.Text = If(_mountedPathA IsNot Nothing AndAlso _mountedPathA.Equals(pathInBed, StringComparison.OrdinalIgnoreCase), "Eject A:", "Mount A:")
            _mountBButton.Text = If(_mountedPathB IsNot Nothing AndAlso _mountedPathB.Equals(pathInBed, StringComparison.OrdinalIgnoreCase), "Eject B:", "Mount B:")
            _geometryLabel.Text = sessionInBed.Document.GeometryText & " • " & FormatBytes(sessionInBed.Document.FreeBytes) & " free • " & sessionInBed.Document.BootStatusText

            _suppressImageNameCommit = True
            Try
                _imageNameBox.Text = Path.GetFileName(pathInBed)
                _imageNameBox.Tag = pathInBed
            Finally
                _suppressImageNameCommit = False
            End Try
            _imageNameBox.Enabled = Not mountedInBed
            _imageToolTip.SetToolTip(
                _imageNameBox,
                If(mountedInBed, "Eject this image before renaming it.", "Edit the image filename; press Enter or leave the field to rename."))
        Else
            _emptyImagePanel.BringToFront()
            _mountAButton.Text = "Mount A:"
            _mountBButton.Text = "Mount B:"
            _geometryLabel.Text = String.Empty
            _suppressImageNameCommit = True
            Try
                _imageNameBox.Text = String.Empty
                _imageNameBox.Tag = Nothing
            Finally
                _suppressImageNameCommit = False
            End Try
            _imageNameBox.Enabled = False
        End If
    End Sub

    Private Sub SneakerNetForm_KeyDown(senderInBed As Object, eInBed As KeyEventArgs)
        If eInBed.Alt AndAlso eInBed.KeyCode = Keys.Left Then
            NavigateHostBack()
            eInBed.SuppressKeyPress = True
        ElseIf eInBed.Alt AndAlso eInBed.KeyCode = Keys.Right Then
            NavigateHostForward()
            eInBed.SuppressKeyPress = True
        ElseIf eInBed.Alt AndAlso eInBed.KeyCode = Keys.Up Then
            NavigateHostParent()
            eInBed.SuppressKeyPress = True
        ElseIf eInBed.Control AndAlso eInBed.KeyCode = Keys.L Then
            _hostPathBox.Focus()
            _hostPathBox.SelectAll()
            eInBed.SuppressKeyPress = True
        ElseIf eInBed.KeyCode = Keys.F5 Then
            RefreshDriveStrip(forceInBed:=True)
            If Not String.IsNullOrWhiteSpace(_hostPath) AndAlso Directory.Exists(_hostPath) Then RefreshHostList()
            eInBed.SuppressKeyPress = True
        ElseIf eInBed.Control AndAlso eInBed.KeyCode = Keys.O Then
            ChooseAndOpenImage()
            eInBed.SuppressKeyPress = True
        ElseIf eInBed.Control AndAlso eInBed.KeyCode = Keys.N Then
            CreateConfiguredNewDisk()
            eInBed.SuppressKeyPress = True
        ElseIf eInBed.KeyCode = Keys.Escape AndAlso _actionPanel.Height > 0 Then
            HideActionPanel()
            eInBed.SuppressKeyPress = True
        End If
    End Sub

    Private Sub OpenFloppyBoxFolder()
        Try
            _box.EnsureExists()
            Process.Start(New ProcessStartInfo(_box.RootPath) With {.UseShellExecute = True})
        Catch ex As Exception
            SetError(ex.Message)
        End Try
    End Sub

    ' ========================================================================
    ' SNEAKER NET MEDIA WORKBENCH PALLET 11
    ' The pages below are host-side logistics.  They never synthesize guest
    ' controllers: the virtual chassis still decides what media can be attached.
    ' ========================================================================

    Private Function AcquireMediaQuiesceInBed() As IDisposable
        If _quiesceMachineInBed Is Nothing Then Return New HostActionLease(Nothing)
        Return _quiesceMachineInBed()
    End Function

    Private Sub BuildHardDrivePageInBed()
        _hardDrivePage.Padding = New Padding(8)

        Dim toolbarInBed As New FlowLayoutPanel() With {
            .Dock = DockStyle.Top,
            .Height = 68,
            .FlowDirection = FlowDirection.LeftToRight,
            .WrapContents = False,
            .Padding = New Padding(4)
        }

        Dim refreshInBed As New Button() With {.Text = "Refresh", .AutoSize = True}
        Dim attachInBed As New Button() With {.Text = "Attach Primary", .AutoSize = True}
        Dim ejectInBed As New Button() With {.Text = "Disconnect", .AutoSize = True}
        Dim inspectInBed As New Button() With {.Text = "Inspect", .AutoSize = True}
        Dim cloneInBed As New Button() With {.Text = "Clone...", .AutoSize = True}
        Dim backupInBed As New Button() With {.Text = "Backup Now", .AutoSize = True}
        Dim openFolderInBed As New Button() With {.Text = "Open Shelf", .AutoSize = True}

        _hddLabelInBed.Width = 145
        _hddLabelInBed.Text = "New Hard Drive"
        _hddCapacityMbInBed.Minimum = 1D
        _hddCapacityMbInBed.Maximum = 8192D
        _hddCapacityMbInBed.Value = 64D
        _hddCapacityMbInBed.Width = 72
        Dim createInBed As New Button() With {.Text = "Create MB", .AutoSize = True}

        toolbarInBed.Controls.Add(refreshInBed)
        toolbarInBed.Controls.Add(attachInBed)
        toolbarInBed.Controls.Add(ejectInBed)
        toolbarInBed.Controls.Add(inspectInBed)
        toolbarInBed.Controls.Add(cloneInBed)
        toolbarInBed.Controls.Add(backupInBed)
        toolbarInBed.Controls.Add(openFolderInBed)
        toolbarInBed.Controls.Add(New Label() With {.Text = "    Label", .AutoSize = True, .Padding = New Padding(0, 7, 0, 0)})
        toolbarInBed.Controls.Add(_hddLabelInBed)
        toolbarInBed.Controls.Add(_hddCapacityMbInBed)
        toolbarInBed.Controls.Add(createInBed)

        _hddListInBed.Dock = DockStyle.Fill
        _hddListInBed.View = View.Details
        _hddListInBed.FullRowSelect = True
        _hddListInBed.MultiSelect = False
        _hddListInBed.HideSelection = False
        _hddListInBed.Columns.Add("ID", 48)
        _hddListInBed.Columns.Add("Image", 300)
        _hddListInBed.Columns.Add("Size", 105)
        _hddListInBed.Columns.Add("State", 125)

        _hddDetailsInBed.Dock = DockStyle.Fill
        _hddDetailsInBed.Multiline = True
        _hddDetailsInBed.ReadOnly = True
        _hddDetailsInBed.ScrollBars = ScrollBars.Both
        _hddDetailsInBed.Font = New Font(FontFamily.GenericMonospace, 9.0F)
        _hddDetailsInBed.WordWrap = False

        Dim splitInBed As New SplitContainer() With {
            .Dock = DockStyle.Fill,
            .Orientation = Orientation.Vertical,
            .SplitterDistance = 560
        }
        splitInBed.Panel1.Controls.Add(_hddListInBed)
        splitInBed.Panel2.Controls.Add(_hddDetailsInBed)

        _hardDrivePage.Controls.Add(splitInBed)
        _hardDrivePage.Controls.Add(toolbarInBed)

        AddHandler refreshInBed.Click, Sub() RefreshHardDrivesInBed()
        AddHandler attachInBed.Click, Sub() AttachSelectedHardDriveInBed()
        AddHandler ejectInBed.Click, Sub() DisconnectHardDriveInBed()
        AddHandler inspectInBed.Click, Sub() InspectSelectedHardDriveInBed()
        AddHandler cloneInBed.Click, Sub() CloneSelectedHardDriveInBed()
        AddHandler backupInBed.Click, Sub() BackupSelectedHardDriveInBed()
        AddHandler openFolderInBed.Click, Sub() OpenIdeShelfFolderInBed()
        AddHandler createInBed.Click, Sub() CreateHardDriveInBed()
        AddHandler _hddListInBed.DoubleClick, Sub() InspectSelectedHardDriveInBed()
    End Sub

    Private Sub RefreshHardDrivesInBed()
        _hddListInBed.BeginUpdate()
        Try
            _hddListInBed.Items.Clear()
            If _ideShelf Is Nothing Then
                _hddDetailsInBed.Text = "IDE shelf is unavailable to this Sneaker Net session."
                Return
            End If

            _ideShelf.EnsureShelfExists()
            Dim mountedIdInBed As Integer = If(_mountedIdeShelfDriveId Is Nothing, -1, _mountedIdeShelfDriveId())
            For Each entryInBed As IdeDriveShelfEntry In _ideShelf.GetEntries()
                Dim itemInBed As New ListViewItem(entryInBed.Id.ToString()) With {.Tag = entryInBed}
                itemInBed.SubItems.Add(Path.GetFileName(entryInBed.FullPath))
                itemInBed.SubItems.Add(FormatBytes(New FileInfo(entryInBed.FullPath).Length))
                itemInBed.SubItems.Add(If(entryInBed.Id = mountedIdInBed, "Primary Master", "Shelf"))
                _hddListInBed.Items.Add(itemInBed)
            Next
            If _hddListInBed.Items.Count = 0 Then
                _hddDetailsInBed.Text = "No numbered .hdd/.img files are currently in " & _ideShelf.RootPath
            End If
        Catch ex As Exception
            _hddDetailsInBed.Text = ex.Message
        Finally
            _hddListInBed.EndUpdate()
        End Try
    End Sub

    Private Function SelectedHardDriveInBed() As IdeDriveShelfEntry
        If _hddListInBed.SelectedItems.Count = 0 Then Return Nothing
        Return TryCast(_hddListInBed.SelectedItems(0).Tag, IdeDriveShelfEntry)
    End Function

    Private Sub AttachSelectedHardDriveInBed()
        Dim entryInBed As IdeDriveShelfEntry = SelectedHardDriveInBed()
        If entryInBed Is Nothing OrElse _attachIdeShelfDrive Is Nothing Then Return
        Try
            _attachIdeShelfDrive(entryInBed.Id)
            RefreshHardDrivesInBed()
            _hddDetailsInBed.Text = "Attached as Primary Master:" & Environment.NewLine & entryInBed.FullPath
        Catch ex As Exception
            _hddDetailsInBed.Text = ex.Message
        End Try
    End Sub

    Private Sub DisconnectHardDriveInBed()
        If _ejectIdeShelfDrive Is Nothing Then Return
        Try
            _ejectIdeShelfDrive()
            RefreshHardDrivesInBed()
            _hddDetailsInBed.Text = "Primary Master disconnected."
        Catch ex As Exception
            _hddDetailsInBed.Text = ex.Message
        End Try
    End Sub

    Private Sub InspectSelectedHardDriveInBed()
        Dim entryInBed As IdeDriveShelfEntry = SelectedHardDriveInBed()
        If entryInBed Is Nothing Then Return
        Try
            _hddDetailsInBed.Text = DiskImageInspector.BuildReport(entryInBed.FullPath)
        Catch ex As Exception
            _hddDetailsInBed.Text = ex.Message
        End Try
    End Sub

    Private Async Sub CloneSelectedHardDriveInBed()
        Dim entryInBed As IdeDriveShelfEntry = SelectedHardDriveInBed()
        If entryInBed Is Nothing Then Return

        Using pickerInBed As New SaveFileDialog()
            pickerInBed.Title = "Clone hard-disk image - choose a NEW destination"
            pickerInBed.Filter = "Hard-disk images (*.hdd;*.img)|*.hdd;*.img|All files (*.*)|*.*"
            pickerInBed.InitialDirectory = Path.GetDirectoryName(entryInBed.FullPath)
            Dim cloneIdInBed As Integer = If(_ideShelf Is Nothing, entryInBed.Id + 1, _ideShelf.NextAvailableId())
            pickerInBed.FileName = cloneIdInBed.ToString() & " - " & IdeDriveShelf.SanitizeLabel(entryInBed.Label & " Copy") & Path.GetExtension(entryInBed.FullPath)
            pickerInBed.OverwritePrompt = True
            If pickerInBed.ShowDialog(Me) <> DialogResult.OK Then Return
            Try
                _hddDetailsInBed.Text = "Cloning " & entryInBed.FullPath & Environment.NewLine & "to " & pickerInBed.FileName & "..."
                Using leaseInBed As IDisposable = AcquireMediaQuiesceInBed()
                    Using cancellationInBed As New CancellationTokenSource()
                        Await MediaImagingEngine.CloneFileNoOverwriteAsync(entryInBed.FullPath, pickerInBed.FileName, Nothing, cancellationInBed.Token)
                    End Using
                End Using
                _hddDetailsInBed.Text &= Environment.NewLine & "Clone complete. Source was not modified."
                RefreshHardDrivesInBed()
            Catch ex As Exception
                _hddDetailsInBed.Text = ex.Message
            End Try
        End Using
    End Sub

    Private Async Sub BackupSelectedHardDriveInBed()
        Dim entryInBed As IdeDriveShelfEntry = SelectedHardDriveInBed()
        If entryInBed Is Nothing Then Return
        Try
            _hddDetailsInBed.Text = "Checking backup history..."
            Dim resultInBed As MediaBackupResult
            Using leaseInBed As IDisposable = AcquireMediaQuiesceInBed()
                resultInBed = Await Task.Run(Function() MediaBackupArchive.BackupIfChanged(entryInBed.FullPath, "IDE-Drives"))
            End Using
            _hddDetailsInBed.Text = resultInBed.Message & Environment.NewLine & resultInBed.DestinationPath
            RefreshBackupBrowserInBed()
        Catch ex As Exception
            _hddDetailsInBed.Text = ex.Message
        End Try
    End Sub

    Private Sub CreateHardDriveInBed()
        If _ideShelf Is Nothing Then Return
        Try
            _ideShelf.EnsureShelfExists()
            Dim idInBed As Integer = _ideShelf.NextAvailableId()
            Dim labelInBed As String = IdeDriveShelf.SanitizeLabel(_hddLabelInBed.Text)
            Dim pathInBed As String = Path.Combine(_ideShelf.RootPath, idInBed.ToString() & " - " & labelInBed & ".hdd")
            If File.Exists(pathInBed) Then Throw New IOException("Refusing to overwrite existing media: " & pathInBed)
            Dim megabytesInBed As Long = CLng(_hddCapacityMbInBed.Value)
            HardDiskImage.Create(pathInBed, megabytesInBed * 1024L * 1024L \ 512L)
            _hddDetailsInBed.Text = "Created blank " & megabytesInBed.ToString() & " MB image:" & Environment.NewLine & pathInBed
            RefreshHardDrivesInBed()
        Catch ex As Exception
            _hddDetailsInBed.Text = ex.Message
        End Try
    End Sub

    Private Sub OpenIdeShelfFolderInBed()
        If _ideShelf Is Nothing Then Return
        Try
            _ideShelf.EnsureShelfExists()
            Process.Start(New ProcessStartInfo(_ideShelf.RootPath) With {.UseShellExecute = True})
        Catch ex As Exception
            _hddDetailsInBed.Text = ex.Message
        End Try
    End Sub

    Private Sub BuildOpticalPageInBed()
        _opticalPage.Padding = New Padding(10)
        Dim topInBed As New FlowLayoutPanel() With {.Dock = DockStyle.Top, .Height = 42, .WrapContents = False}
        Dim chooseInBed As New Button() With {.Text = "Choose ISO...", .AutoSize = True}
        Dim mountInBed As New Button() With {.Text = "Mount", .AutoSize = True}
        Dim ejectInBed As New Button() With {.Text = "Eject", .AutoSize = True}
        Dim inspectInBed As New Button() With {.Text = "Inspect", .AutoSize = True}
        _opticalPathInBed.Width = 520
        topInBed.Controls.Add(chooseInBed)
        topInBed.Controls.Add(_opticalPathInBed)
        topInBed.Controls.Add(mountInBed)
        topInBed.Controls.Add(ejectInBed)
        topInBed.Controls.Add(inspectInBed)

        _opticalDetailsInBed.Dock = DockStyle.Fill
        _opticalDetailsInBed.Multiline = True
        _opticalDetailsInBed.ReadOnly = True
        _opticalDetailsInBed.ScrollBars = ScrollBars.Both
        _opticalDetailsInBed.Font = New Font(FontFamily.GenericMonospace, 9.0F)
        _opticalPage.Controls.Add(_opticalDetailsInBed)
        _opticalPage.Controls.Add(topInBed)

        AddHandler chooseInBed.Click, Sub() ChooseOpticalImageInBed()
        AddHandler mountInBed.Click, Sub() MountOpticalImageInBed()
        AddHandler ejectInBed.Click,
            Sub()
                If _ejectIsoImage IsNot Nothing Then _ejectIsoImage()
                _opticalDetailsInBed.Text = "Optical media ejected from the current chassis."
            End Sub
        AddHandler inspectInBed.Click, Sub() InspectOpticalImageInBed()
    End Sub

    Private Sub ChooseOpticalImageInBed()
        Using pickerInBed As New OpenFileDialog()
            pickerInBed.Title = "Choose optical image"
            pickerInBed.Filter = "ISO images (*.iso)|*.iso|All files (*.*)|*.*"
            pickerInBed.InitialDirectory = DiscBoxPath()
            If pickerInBed.ShowDialog(Me) <> DialogResult.OK Then Return
            _opticalPathInBed.Text = pickerInBed.FileName
            InspectOpticalImageInBed()
        End Using
    End Sub

    Private Sub MountOpticalImageInBed()
        If _mountIsoImage Is Nothing Then
            Dim messageInBed As String = "This Disc Box is not connected to a chassis optical drive."
            _opticalDetailsInBed.Text = messageInBed
            MessageBox.Show(Me, messageInBed, "Unable to mount CD-ROM", MessageBoxButtons.OK, MessageBoxIcon.Error)
            Return
        End If

        If String.IsNullOrWhiteSpace(_opticalPathInBed.Text) Then
            ChooseOpticalImageInBed()
            If String.IsNullOrWhiteSpace(_opticalPathInBed.Text) Then
                _opticalDetailsInBed.Text = "No ISO image was selected."
                Return
            End If
        End If

        Try
            Dim fullPathInBed As String = Path.GetFullPath(_opticalPathInBed.Text)
            If Not File.Exists(fullPathInBed) Then Throw New FileNotFoundException("The selected ISO image no longer exists.", fullPathInBed)
            _mountIsoImage(fullPathInBed)
            _opticalPathInBed.Text = fullPathInBed
            _opticalDetailsInBed.Text = "Mounted through the current chassis optical path:" & Environment.NewLine & fullPathInBed
        Catch ex As Exception
            _opticalDetailsInBed.Text = ex.Message
            MessageBox.Show(Me, ex.Message, "Unable to mount CD-ROM", MessageBoxButtons.OK, MessageBoxIcon.Error)
        End Try
    End Sub

    Private Sub InspectOpticalImageInBed()
        If String.IsNullOrWhiteSpace(_opticalPathInBed.Text) Then Return
        Try
            _opticalDetailsInBed.Text = DiskImageInspector.BuildReport(_opticalPathInBed.Text)
        Catch ex As Exception
            _opticalDetailsInBed.Text = ex.Message
        End Try
    End Sub

    Private Sub BuildHostDevicesPageInBed()
        _hostDevicesPage.Padding = New Padding(8)
        Dim toolbarInBed As New FlowLayoutPanel() With {.Dock = DockStyle.Top, .Height = 44, .WrapContents = False}
        Dim refreshInBed As New Button() With {.Text = "Refresh", .AutoSize = True}
        Dim browseInBed As New Button() With {.Text = "Browse", .AutoSize = True}
        Dim imageInBed As New Button() With {.Text = "Image Device...", .AutoSize = True}
        Dim writeInBed As New Button() With {.Text = "Write Image...", .AutoSize = True}
        Dim cancelInBed As New Button() With {.Text = "Cancel", .AutoSize = True}
        toolbarInBed.Controls.Add(refreshInBed)
        toolbarInBed.Controls.Add(browseInBed)
        toolbarInBed.Controls.Add(imageInBed)
        toolbarInBed.Controls.Add(writeInBed)
        toolbarInBed.Controls.Add(cancelInBed)

        _hostDeviceListInBed.Dock = DockStyle.Fill
        _hostDeviceListInBed.View = View.Details
        _hostDeviceListInBed.FullRowSelect = True
        _hostDeviceListInBed.MultiSelect = False
        _hostDeviceListInBed.HideSelection = False
        _hostDeviceListInBed.Columns.Add("Drive", 70)
        _hostDeviceListInBed.Columns.Add("Type", 100)
        _hostDeviceListInBed.Columns.Add("Label", 180)
        _hostDeviceListInBed.Columns.Add("Filesystem", 90)
        _hostDeviceListInBed.Columns.Add("Size", 100)
        _hostDeviceListInBed.Columns.Add("Free", 100)
        _hostDeviceListInBed.Columns.Add("Physical", 125)
        _hostDeviceListInBed.Columns.Add("Raw write", 90)

        Dim bottomInBed As New Panel() With {.Dock = DockStyle.Bottom, .Height = 52}
        _hostDeviceStatusInBed.Dock = DockStyle.Fill
        _hostDeviceStatusInBed.TextAlign = ContentAlignment.MiddleLeft
        _hostDeviceStatusInBed.AutoEllipsis = True
        _hostDeviceProgressInBed.Dock = DockStyle.Bottom
        _hostDeviceProgressInBed.Height = 18
        bottomInBed.Controls.Add(_hostDeviceStatusInBed)
        bottomInBed.Controls.Add(_hostDeviceProgressInBed)

        _hostDevicesPage.Controls.Add(_hostDeviceListInBed)
        _hostDevicesPage.Controls.Add(bottomInBed)
        _hostDevicesPage.Controls.Add(toolbarInBed)

        AddHandler refreshInBed.Click, Sub() RefreshHostDevicesInBed()
        AddHandler browseInBed.Click, Sub() BrowseSelectedHostDeviceInBed()
        AddHandler imageInBed.Click, AddressOf ImageSelectedHostDeviceClickedInBed
        AddHandler writeInBed.Click, AddressOf WriteImageToHostDeviceClickedInBed
        AddHandler cancelInBed.Click,
            Sub()
                If _hostDeviceCancellationInBed IsNot Nothing Then _hostDeviceCancellationInBed.Cancel()
            End Sub
        AddHandler _hostDeviceListInBed.DoubleClick, Sub() BrowseSelectedHostDeviceInBed()
    End Sub

    Private Sub RefreshHostDevicesInBed()
        _hostDeviceListInBed.BeginUpdate()
        Try
            _hostDeviceListInBed.Items.Clear()
            For Each deviceInBed As HostStorageDeviceInfo In HostStorageDeviceCatalog.Enumerate()
                Dim itemInBed As New ListViewItem(deviceInBed.RootPath) With {.Tag = deviceInBed}
                itemInBed.SubItems.Add(deviceInBed.DriveType.ToString())
                itemInBed.SubItems.Add(If(deviceInBed.VolumeLabel, String.Empty))
                itemInBed.SubItems.Add(If(deviceInBed.FileSystem, String.Empty))
                itemInBed.SubItems.Add(If(deviceInBed.TotalBytes > 0, FormatBytes(deviceInBed.TotalBytes), ""))
                itemInBed.SubItems.Add(If(deviceInBed.FreeBytes > 0, FormatBytes(deviceInBed.FreeBytes), ""))
                itemInBed.SubItems.Add(If(deviceInBed.PhysicalDriveNumber >= 0, "PhysicalDrive" & deviceInBed.PhysicalDriveNumber.ToString(), "—"))
                itemInBed.SubItems.Add(If(deviceInBed.IsSystemPhysicalDrive, "BLOCKED", If(deviceInBed.CanRawWrite, "available", "n/a")))
                _hostDeviceListInBed.Items.Add(itemInBed)
            Next
            _hostDeviceStatusInBed.Text = "Host devices are logistics sources/destinations only. They do not imply guest USB hardware."
        Catch ex As Exception
            _hostDeviceStatusInBed.Text = ex.Message
        Finally
            _hostDeviceListInBed.EndUpdate()
        End Try
    End Sub

    Private Function SelectedHostDeviceInBed() As HostStorageDeviceInfo
        If _hostDeviceListInBed.SelectedItems.Count = 0 Then Return Nothing
        Return TryCast(_hostDeviceListInBed.SelectedItems(0).Tag, HostStorageDeviceInfo)
    End Function

    Private Sub BrowseSelectedHostDeviceInBed()
        Dim deviceInBed As HostStorageDeviceInfo = SelectedHostDeviceInBed()
        If deviceInBed Is Nothing OrElse String.IsNullOrWhiteSpace(deviceInBed.RootPath) Then Return
        Try
            Process.Start(New ProcessStartInfo(deviceInBed.RootPath) With {.UseShellExecute = True})
        Catch ex As Exception
            _hostDeviceStatusInBed.Text = ex.Message
        End Try
    End Sub

    Private Async Sub ImageSelectedHostDeviceClickedInBed(senderInBed As Object, eInBed As EventArgs)
        Dim deviceInBed As HostStorageDeviceInfo = SelectedHostDeviceInBed()
        If deviceInBed Is Nothing Then Return
        If Not deviceInBed.CanRawImage Then
            _hostDeviceStatusInBed.Text = "Windows did not expose a raw physical-drive mapping for that device."
            Return
        End If

        Using pickerInBed As New SaveFileDialog()
            pickerInBed.Title = "Image physical device - choose a NEW image file"
            pickerInBed.Filter = "Raw disk image (*.img)|*.img|All files (*.*)|*.*"
            Dim labelInBed As String = If(String.IsNullOrWhiteSpace(deviceInBed.VolumeLabel), "PhysicalDrive" & deviceInBed.PhysicalDriveNumber.ToString(), FloppyBox.SanitizeHostLabel(deviceInBed.VolumeLabel))
            pickerInBed.FileName = labelInBed & ".img"
            pickerInBed.OverwritePrompt = True
            If pickerInBed.ShowDialog(Me) <> DialogResult.OK Then Return

            Try
                _hostDeviceCancellationInBed = New CancellationTokenSource()
                _hostDeviceProgressInBed.Style = ProgressBarStyle.Marquee
                Dim progressInBed As New Progress(Of Long)(Sub(bytesInBed) _hostDeviceStatusInBed.Text = "Imaged " & FormatBytes(bytesInBed) & " from " & deviceInBed.PhysicalPath)
                Await MediaImagingEngine.ImagePhysicalDeviceToFileAsync(deviceInBed, pickerInBed.FileName, progressInBed, _hostDeviceCancellationInBed.Token)
                _hostDeviceStatusInBed.Text = "Image complete: " & pickerInBed.FileName
            Catch ex As OperationCanceledException
                _hostDeviceStatusInBed.Text = "Imaging cancelled. A partial destination file may remain and should not be trusted."
            Catch ex As Exception
                _hostDeviceStatusInBed.Text = ex.Message
            Finally
                _hostDeviceProgressInBed.Style = ProgressBarStyle.Blocks
                If _hostDeviceCancellationInBed IsNot Nothing Then _hostDeviceCancellationInBed.Dispose()
                _hostDeviceCancellationInBed = Nothing
            End Try
        End Using
    End Sub

    Private Async Sub WriteImageToHostDeviceClickedInBed(senderInBed As Object, eInBed As EventArgs)
        Dim deviceInBed As HostStorageDeviceInfo = SelectedHostDeviceInBed()
        If deviceInBed Is Nothing Then Return
        If Not deviceInBed.CanRawWrite Then
            _hostDeviceStatusInBed.Text = If(deviceInBed.IsSystemPhysicalDrive,
                "Raw writes to the physical disk containing Windows are blocked.",
                "That device is not available for raw writes.")
            Return
        End If

        Using pickerInBed As New OpenFileDialog()
            pickerInBed.Title = "Choose raw image to write to " & deviceInBed.RootPath
            pickerInBed.Filter = "Disk images (*.img;*.hdd;*.ima)|*.img;*.hdd;*.ima|All files (*.*)|*.*"
            If pickerInBed.ShowDialog(Me) <> DialogResult.OK Then Return

            Dim warningInBed As String =
                "WRITE IMAGE TO PHYSICAL DEVICE" & Environment.NewLine & Environment.NewLine &
                "Target: " & deviceInBed.RootPath & "  " & deviceInBed.PhysicalPath & Environment.NewLine &
                "Label: " & If(deviceInBed.VolumeLabel, String.Empty) & Environment.NewLine &
                "Image: " & pickerInBed.FileName & Environment.NewLine & Environment.NewLine &
                "This overwrites sectors on the selected physical device. Existing partitions/files may be destroyed." & Environment.NewLine &
                "The Windows system physical disk is blocked, but verify the target anyway."
            If MessageBox.Show(Me, warningInBed, "Sneaker Net - destructive physical write", MessageBoxButtons.YesNo, MessageBoxIcon.Warning, MessageBoxDefaultButton.Button2) <> DialogResult.Yes Then Return

            Try
                _hostDeviceCancellationInBed = New CancellationTokenSource()
                _hostDeviceProgressInBed.Style = ProgressBarStyle.Marquee
                Dim progressInBed As New Progress(Of Long)(Sub(bytesInBed) _hostDeviceStatusInBed.Text = "Wrote " & FormatBytes(bytesInBed) & " to " & deviceInBed.PhysicalPath)
                Await MediaImagingEngine.WriteImageToPhysicalDeviceAsync(pickerInBed.FileName, deviceInBed, progressInBed, _hostDeviceCancellationInBed.Token)
                _hostDeviceStatusInBed.Text = "Physical-device write complete. Reinsert/rescan the device before browsing it."
                RefreshHostDevicesInBed()
            Catch ex As OperationCanceledException
                _hostDeviceStatusInBed.Text = "WRITE CANCELLED. The target device may now contain a partial image."
            Catch ex As Exception
                _hostDeviceStatusInBed.Text = ex.Message
            Finally
                _hostDeviceProgressInBed.Style = ProgressBarStyle.Blocks
                If _hostDeviceCancellationInBed IsNot Nothing Then _hostDeviceCancellationInBed.Dispose()
                _hostDeviceCancellationInBed = Nothing
            End Try
        End Using
    End Sub

    Private Sub BuildImagingPageInBed()
        _imagingPage.Padding = New Padding(10)

        Dim topInBed As New TableLayoutPanel() With {
            .Dock = DockStyle.Top,
            .AutoSize = True,
            .ColumnCount = 3,
            .RowCount = 3,
            .Padding = New Padding(0, 0, 0, 8)
        }
        topInBed.ColumnStyles.Add(New ColumnStyle(SizeType.Absolute, 90))
        topInBed.ColumnStyles.Add(New ColumnStyle(SizeType.Percent, 100.0F))
        topInBed.ColumnStyles.Add(New ColumnStyle(SizeType.Absolute, 110))

        Dim browseSourceInBed As New Button() With {.Text = "Browse...", .Dock = DockStyle.Fill}
        Dim browseDestinationInBed As New Button() With {.Text = "Browse...", .Dock = DockStyle.Fill}
        _imagingSourceInBed.Dock = DockStyle.Fill
        _imagingDestinationInBed.Dock = DockStyle.Fill
        topInBed.Controls.Add(New Label() With {.Text = "Source image", .AutoSize = True, .Padding = New Padding(0, 6, 0, 0)}, 0, 0)
        topInBed.Controls.Add(_imagingSourceInBed, 1, 0)
        topInBed.Controls.Add(browseSourceInBed, 2, 0)
        topInBed.Controls.Add(New Label() With {.Text = "Destination", .AutoSize = True, .Padding = New Padding(0, 6, 0, 0)}, 0, 1)
        topInBed.Controls.Add(_imagingDestinationInBed, 1, 1)
        topInBed.Controls.Add(browseDestinationInBed, 2, 1)

        Dim actionsInBed As New FlowLayoutPanel() With {.AutoSize = True, .Dock = DockStyle.Fill, .WrapContents = False}
        Dim cloneInBed As New Button() With {.Text = "Clone Image", .AutoSize = True}
        Dim verifyInBed As New Button() With {.Text = "Verify", .AutoSize = True}
        Dim inspectInBed As New Button() With {.Text = "Inspect Source", .AutoSize = True}
        Dim backupInBed As New Button() With {.Text = "Backup Source", .AutoSize = True}
        Dim cancelInBed As New Button() With {.Text = "Cancel", .AutoSize = True}
        actionsInBed.Controls.Add(cloneInBed)
        actionsInBed.Controls.Add(verifyInBed)
        actionsInBed.Controls.Add(inspectInBed)
        actionsInBed.Controls.Add(backupInBed)
        actionsInBed.Controls.Add(cancelInBed)
        topInBed.Controls.Add(actionsInBed, 1, 2)
        topInBed.SetColumnSpan(actionsInBed, 2)

        _imagingStatusInBed.Dock = DockStyle.Fill
        _imagingStatusInBed.Multiline = True
        _imagingStatusInBed.ReadOnly = True
        _imagingStatusInBed.ScrollBars = ScrollBars.Both
        _imagingStatusInBed.Font = New Font(FontFamily.GenericMonospace, 9.0F)
        _imagingStatusInBed.Text = "Exact image cloning and verification operate on raw bytes. No existing destination is overwritten." & Environment.NewLine &
                                   "Partition-aware resize/expand is intentionally a later layer; the cloning substrate stays boring first."

        _imagingPage.Controls.Add(_imagingStatusInBed)
        _imagingPage.Controls.Add(topInBed)

        AddHandler browseSourceInBed.Click, Sub() BrowseImagingSourceInBed()
        AddHandler browseDestinationInBed.Click, Sub() BrowseImagingDestinationInBed()
        AddHandler cloneInBed.Click, AddressOf CloneImageClickedInBed
        AddHandler verifyInBed.Click, AddressOf VerifyImageClickedInBed
        AddHandler inspectInBed.Click, Sub() InspectImagingSourceInBed()
        AddHandler backupInBed.Click, AddressOf BackupImagingSourceClickedInBed
        AddHandler cancelInBed.Click,
            Sub()
                If _imagingCancellationInBed IsNot Nothing Then _imagingCancellationInBed.Cancel()
            End Sub
    End Sub

    Private Sub BrowseImagingSourceInBed()
        Using pickerInBed As New OpenFileDialog()
            pickerInBed.Title = "Choose source image"
            pickerInBed.Filter = "Media images (*.img;*.hdd;*.ima;*.iso;*.vhd;*.vhdx)|*.img;*.hdd;*.ima;*.iso;*.vhd;*.vhdx|All files (*.*)|*.*"
            If pickerInBed.ShowDialog(Me) = DialogResult.OK Then
                _imagingSourceInBed.Text = pickerInBed.FileName
                InspectImagingSourceInBed()
            End If
        End Using
    End Sub

    Private Sub BrowseImagingDestinationInBed()
        Using pickerInBed As New SaveFileDialog()
            pickerInBed.Title = "Choose a NEW destination image"
            pickerInBed.Filter = "Raw/media image (*.img;*.hdd)|*.img;*.hdd|All files (*.*)|*.*"
            pickerInBed.OverwritePrompt = True
            If pickerInBed.ShowDialog(Me) = DialogResult.OK Then _imagingDestinationInBed.Text = pickerInBed.FileName
        End Using
    End Sub

    Private Async Sub CloneImageClickedInBed(senderInBed As Object, eInBed As EventArgs)
        If String.IsNullOrWhiteSpace(_imagingSourceInBed.Text) OrElse String.IsNullOrWhiteSpace(_imagingDestinationInBed.Text) Then Return
        Try
            _imagingCancellationInBed = New CancellationTokenSource()
            _imagingStatusInBed.Text = "Cloning raw image..."
            Dim progressInBed As New Progress(Of Long)(Sub(bytesInBed) _imagingStatusInBed.Text = "Cloned " & FormatBytes(bytesInBed) & "..." )
            Using leaseInBed As IDisposable = AcquireMediaQuiesceInBed()
                Await MediaImagingEngine.CloneFileNoOverwriteAsync(_imagingSourceInBed.Text, _imagingDestinationInBed.Text, progressInBed, _imagingCancellationInBed.Token)
            End Using
            _imagingStatusInBed.Text = "Clone complete." & Environment.NewLine & _imagingDestinationInBed.Text
        Catch ex As OperationCanceledException
            _imagingStatusInBed.Text = "Clone cancelled. A partial destination may remain and should not be trusted."
        Catch ex As Exception
            _imagingStatusInBed.Text = ex.Message
        Finally
            If _imagingCancellationInBed IsNot Nothing Then _imagingCancellationInBed.Dispose()
            _imagingCancellationInBed = Nothing
        End Try
    End Sub

    Private Async Sub VerifyImageClickedInBed(senderInBed As Object, eInBed As EventArgs)
        If String.IsNullOrWhiteSpace(_imagingSourceInBed.Text) OrElse String.IsNullOrWhiteSpace(_imagingDestinationInBed.Text) Then Return
        Try
            _imagingCancellationInBed = New CancellationTokenSource()
            _imagingStatusInBed.Text = "Computing SHA-256 for both images..."
            Dim equalInBed As Boolean = Await MediaImagingEngine.VerifyFilesAsync(_imagingSourceInBed.Text, _imagingDestinationInBed.Text, _imagingCancellationInBed.Token)
            _imagingStatusInBed.Text = If(equalInBed, "VERIFY PASS - images are byte-identical.", "VERIFY FAIL - images differ.")
        Catch ex As OperationCanceledException
            _imagingStatusInBed.Text = "Verification cancelled."
        Catch ex As Exception
            _imagingStatusInBed.Text = ex.Message
        Finally
            If _imagingCancellationInBed IsNot Nothing Then _imagingCancellationInBed.Dispose()
            _imagingCancellationInBed = Nothing
        End Try
    End Sub

    Private Sub InspectImagingSourceInBed()
        If String.IsNullOrWhiteSpace(_imagingSourceInBed.Text) Then Return
        Try
            _imagingStatusInBed.Text = DiskImageInspector.BuildReport(_imagingSourceInBed.Text)
        Catch ex As Exception
            _imagingStatusInBed.Text = ex.Message
        End Try
    End Sub

    Private Async Sub BackupImagingSourceClickedInBed(senderInBed As Object, eInBed As EventArgs)
        If String.IsNullOrWhiteSpace(_imagingSourceInBed.Text) Then Return
        Try
            _imagingStatusInBed.Text = "Checking backup history..."
            Dim sourceInBed As String = Path.GetFullPath(_imagingSourceInBed.Text)
            Dim resultInBed As MediaBackupResult
            Using leaseInBed As IDisposable = AcquireMediaQuiesceInBed()
                resultInBed = Await Task.Run(Function() MediaBackupArchive.BackupIfChanged(sourceInBed))
            End Using
            _imagingStatusInBed.Text = resultInBed.Message & Environment.NewLine & resultInBed.DestinationPath
            RefreshBackupBrowserInBed()
        Catch ex As Exception
            _imagingStatusInBed.Text = ex.Message
        End Try
    End Sub

    Private Sub BuildBackupPageInBed()
        _backupPage.Padding = New Padding(8)
        Dim toolbarInBed As New FlowLayoutPanel() With {.Dock = DockStyle.Top, .Height = 42, .WrapContents = False}
        Dim openInBed As New Button() With {.Text = "Open Backup Folder", .AutoSize = True}
        Dim refreshInBed As New Button() With {.Text = "Refresh", .AutoSize = True}
        Dim backupFileInBed As New Button() With {.Text = "Backup File...", .AutoSize = True}
        _backupRootInBed.Width = 520
        _backupRootInBed.ReadOnly = True
        toolbarInBed.Controls.Add(openInBed)
        toolbarInBed.Controls.Add(refreshInBed)
        toolbarInBed.Controls.Add(backupFileInBed)
        toolbarInBed.Controls.Add(_backupRootInBed)

        _backupListInBed.Dock = DockStyle.Fill
        _backupListInBed.View = View.Details
        _backupListInBed.FullRowSelect = True
        _backupListInBed.MultiSelect = False
        _backupListInBed.Columns.Add("Category", 140)
        _backupListInBed.Columns.Add("Medium silo", 330)
        _backupListInBed.Columns.Add("Generations", 95)
        _backupListInBed.Columns.Add("Newest", 165)

        _backupStatusInBed.Dock = DockStyle.Bottom
        _backupStatusInBed.Height = 42
        _backupStatusInBed.TextAlign = ContentAlignment.MiddleLeft
        _backupStatusInBed.Text = "Manual append-only protection is active here. Hourly host monitoring is not armed yet."

        _backupPage.Controls.Add(_backupListInBed)
        _backupPage.Controls.Add(_backupStatusInBed)
        _backupPage.Controls.Add(toolbarInBed)

        AddHandler openInBed.Click, Sub() OpenBackupRootInBed()
        AddHandler refreshInBed.Click, Sub() RefreshBackupBrowserInBed()
        AddHandler backupFileInBed.Click, AddressOf BackupArbitraryFileClickedInBed
        AddHandler _backupListInBed.DoubleClick, Sub() OpenSelectedBackupSiloInBed()
    End Sub

    Private Sub RefreshBackupBrowserInBed()
        Try
            Dim rootInBed As String = HostMediaConfiguration.GetBackupRoot()
            _backupRootInBed.Text = rootInBed
            Directory.CreateDirectory(rootInBed)
            _backupListInBed.BeginUpdate()
            Try
                _backupListInBed.Items.Clear()
                For Each categoryPathInBed As String In Directory.EnumerateDirectories(rootInBed)
                    Dim categoryInBed As String = Path.GetFileName(categoryPathInBed)
                    For Each siloPathInBed As String In Directory.EnumerateDirectories(categoryPathInBed)
                        Dim generationFilesInBed As String() = Directory.EnumerateFiles(siloPathInBed).Where(
                            Function(pathInBed)
                                Dim stemInBed As String = Path.GetFileNameWithoutExtension(pathInBed)
                                Dim ignoredInBed As Long
                                Return Long.TryParse(stemInBed, System.Globalization.NumberStyles.AllowHexSpecifier, System.Globalization.CultureInfo.InvariantCulture, ignoredInBed)
                            End Function).ToArray()
                        Dim newestInBed As DateTime = DateTime.MinValue
                        If generationFilesInBed.Length > 0 Then newestInBed = generationFilesInBed.Max(Function(pathInBed) File.GetLastWriteTime(pathInBed))
                        Dim itemInBed As New ListViewItem(categoryInBed) With {.Tag = siloPathInBed}
                        itemInBed.SubItems.Add(Path.GetFileName(siloPathInBed))
                        itemInBed.SubItems.Add(generationFilesInBed.Length.ToString())
                        itemInBed.SubItems.Add(If(newestInBed = DateTime.MinValue, "", newestInBed.ToString("yyyy-MM-dd HH:mm:ss")))
                        _backupListInBed.Items.Add(itemInBed)
                    Next
                Next
            Finally
                _backupListInBed.EndUpdate()
            End Try
        Catch ex As Exception
            _backupStatusInBed.Text = ex.Message
        End Try
    End Sub

    Private Sub OpenBackupRootInBed()
        Try
            HostMediaConfiguration.EnsureBackupRoot()
            Process.Start(New ProcessStartInfo(HostMediaConfiguration.GetBackupRoot()) With {.UseShellExecute = True})
        Catch ex As Exception
            _backupStatusInBed.Text = ex.Message
        End Try
    End Sub

    Private Sub OpenSelectedBackupSiloInBed()
        If _backupListInBed.SelectedItems.Count = 0 Then Return
        Dim pathInBed As String = TryCast(_backupListInBed.SelectedItems(0).Tag, String)
        If String.IsNullOrWhiteSpace(pathInBed) Then Return
        Try
            Process.Start(New ProcessStartInfo(pathInBed) With {.UseShellExecute = True})
        Catch ex As Exception
            _backupStatusInBed.Text = ex.Message
        End Try
    End Sub

    Private Async Sub BackupArbitraryFileClickedInBed(senderInBed As Object, eInBed As EventArgs)
        Using pickerInBed As New OpenFileDialog()
            pickerInBed.Title = "Choose persistent media to archive"
            pickerInBed.Filter = "Media images (*.hdd;*.img;*.ima;*.iso;*.vhd;*.vhdx)|*.hdd;*.img;*.ima;*.iso;*.vhd;*.vhdx|All files (*.*)|*.*"
            If pickerInBed.ShowDialog(Me) <> DialogResult.OK Then Return
            Try
                _backupStatusInBed.Text = "Checking " & pickerInBed.FileName & " against its latest generation..."
                Dim resultInBed As MediaBackupResult
                Using leaseInBed As IDisposable = AcquireMediaQuiesceInBed()
                    resultInBed = Await Task.Run(Function() MediaBackupArchive.BackupIfChanged(pickerInBed.FileName))
                End Using
                _backupStatusInBed.Text = resultInBed.Message & "  " & resultInBed.DestinationPath
                RefreshBackupBrowserInBed()
            Catch ex As Exception
                _backupStatusInBed.Text = ex.Message
            End Try
        End Using
    End Sub

    Private Sub SetStatus(textInBed As String)
        _statusLabel.ForeColor = SystemColors.ControlText
        _statusLabel.Text = textInBed
    End Sub

    Private Sub SetError(textInBed As String)
        _statusLabel.ForeColor = Color.Firebrick
        _statusLabel.Text = textInBed
    End Sub

    Private Shared Function FormatBytes(valueInBed As Long) As String
        If valueInBed >= 1024L * 1024L Then Return (valueInBed / (1024.0 * 1024.0)).ToString("0.00") & " MB"
        If valueInBed >= 1024L Then Return (valueInBed / 1024.0).ToString("0.0") & " KB"
        Return valueInBed.ToString("N0") & " B"
    End Function
End Class
