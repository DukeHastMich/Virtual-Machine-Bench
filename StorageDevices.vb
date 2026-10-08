Imports System
Imports System.Collections.Generic
Imports System.IO
Imports System.Text.Json

Public Interface IBlockMedia
    ReadOnly Property SectorSize As Integer
    ReadOnly Property SectorCount As Long
    ReadOnly Property IsReadOnly As Boolean
    ReadOnly Property ImagePath As String
    Function ReadSector(lba As Long) As Byte()
    Sub WriteSector(lba As Long, data As Byte())
    Sub Flush()
End Interface

Public MustInherit Class FileBlockMedia
    Implements IBlockMedia, IDisposable

    Private ReadOnly _path As String
    Private ReadOnly _sectorSize As Integer
    Private ReadOnly _readOnly As Boolean
    Private ReadOnly _stream As FileStream
    Private _disposed As Boolean

    Protected Sub New(path As String, sectorSize As Integer, readOnlyMedia As Boolean)
        If String.IsNullOrWhiteSpace(path) Then Throw New ArgumentException("An image path is required.", "path")
        If sectorSize <= 0 Then Throw New ArgumentOutOfRangeException("sectorSize")
        _path = System.IO.Path.GetFullPath(path)
        _sectorSize = sectorSize
        _readOnly = readOnlyMedia
        Dim access As FileAccess = If(readOnlyMedia, FileAccess.Read, FileAccess.ReadWrite)
        Dim share As FileShare = If(readOnlyMedia, FileShare.Read, FileShare.Read)
        _stream = New FileStream(_path, FileMode.Open, access, share, 65536, FileOptions.RandomAccess)
        If _stream.Length Mod sectorSize <> 0 Then
            _stream.Dispose()
            Throw New InvalidDataException("Image length is not a whole number of sectors.")
        End If
    End Sub

    Public ReadOnly Property SectorSize As Integer Implements IBlockMedia.SectorSize
        Get
            Return _sectorSize
        End Get
    End Property

    Public ReadOnly Property SectorCount As Long Implements IBlockMedia.SectorCount
        Get
            Return _stream.Length \ _sectorSize
        End Get
    End Property

    Public ReadOnly Property IsReadOnly As Boolean Implements IBlockMedia.IsReadOnly
        Get
            Return _readOnly
        End Get
    End Property

    Public ReadOnly Property ImagePath As String Implements IBlockMedia.ImagePath
        Get
            Return _path
        End Get
    End Property

    Public Function ReadSector(lba As Long) As Byte() Implements IBlockMedia.ReadSector
        ValidateLba(lba)
        Dim result(_sectorSize - 1) As Byte
        SyncLock _stream
            _stream.Position = lba * CLng(_sectorSize)
            Dim position As Integer = 0
            While position < result.Length
                Dim read As Integer = _stream.Read(result, position, result.Length - position)
                If read = 0 Then Throw New EndOfStreamException("Unexpected end of disk image.")
                position += read
            End While
        End SyncLock
        Return result
    End Function

    Public Sub WriteSector(lba As Long, data As Byte()) Implements IBlockMedia.WriteSector
        If _readOnly Then Throw New UnauthorizedAccessException("The mounted medium is write-protected.")
        If data Is Nothing OrElse data.Length <> _sectorSize Then
            Throw New ArgumentException("A write must contain exactly one sector.", "data")
        End If
        ValidateLba(lba)
        SyncLock _stream
            _stream.Position = lba * CLng(_sectorSize)
            _stream.Write(data, 0, data.Length)
        End SyncLock
    End Sub

    Public Sub Flush() Implements IBlockMedia.Flush
        SyncLock _stream
            _stream.Flush()
        End SyncLock
    End Sub

    Protected Sub ValidateLba(lba As Long)
        If lba < 0 OrElse lba >= SectorCount Then Throw New ArgumentOutOfRangeException("lba")
    End Sub

    Public Sub Dispose() Implements IDisposable.Dispose
        If _disposed Then Return
        _stream.Dispose()
        _disposed = True
    End Sub
End Class

Public Class FloppyImage
    Inherits FileBlockMedia

    Public ReadOnly Property Cylinders As Integer
    Public ReadOnly Property Heads As Integer
    Public ReadOnly Property SectorsPerTrack As Integer

    Public Sub New(path As String, Optional writeProtected As Boolean = False)
        MyBase.New(path, 512, writeProtected)
        Dim geometry As Integer() = DetectGeometry(New FileInfo(path).Length)
        Cylinders = geometry(0)
        Heads = geometry(1)
        SectorsPerTrack = geometry(2)
    End Sub

    Public Function ChsToLba(cylinder As Integer, head As Integer, sector As Integer) As Long
        If cylinder < 0 OrElse cylinder >= Cylinders Then Throw New ArgumentOutOfRangeException("cylinder")
        If head < 0 OrElse head >= Heads Then Throw New ArgumentOutOfRangeException("head")
        If sector < 1 OrElse sector > SectorsPerTrack Then Throw New ArgumentOutOfRangeException("sector")
        Return (CLng(cylinder) * Heads + head) * SectorsPerTrack + (sector - 1)
    End Function

    Private Shared Function DetectGeometry(length As Long) As Integer()
        Select Case length
            Case 163840 : Return New Integer() {40, 1, 8}
            Case 184320 : Return New Integer() {40, 1, 9}
            Case 327680 : Return New Integer() {40, 2, 8}
            Case 368640 : Return New Integer() {40, 2, 9}
            Case 737280 : Return New Integer() {80, 2, 9}
            Case 1228800 : Return New Integer() {80, 2, 15}
            Case 1474560 : Return New Integer() {80, 2, 18}
            Case 2949120 : Return New Integer() {80, 2, 36}
            Case Else
                Throw New InvalidDataException("Unsupported raw floppy image size: " & length.ToString() & " bytes.")
        End Select
    End Function
End Class

Public NotInheritable Class IdeDriveModelProfile
    Public Sub New(idInBed As String,
                   displayNameInBed As String,
                   manufacturerInBed As String,
                   modelInBed As String,
                   firmwareInBed As String,
                   totalSectorsInBed As Long,
                   nativeCylindersInBed As Integer,
                   nativeHeadsInBed As Integer,
                   nativeSectorsPerTrackInBed As Integer,
                   biosCylindersInBed As Integer,
                   biosHeadsInBed As Integer,
                   biosSectorsPerTrackInBed As Integer,
                   ataMajorVersionInBed As Integer,
                   supportsLba28InBed As Boolean,
                   maximumMultipleSectorsInBed As Integer,
                   maximumPioModeInBed As Integer,
                   rotationRpmInBed As Integer,
                   averageSeekMillisecondsInBed As Double,
                   serialPrefixInBed As String)
        Id = idInBed
        DisplayName = displayNameInBed
        Manufacturer = manufacturerInBed
        Model = modelInBed
        FirmwareRevision = firmwareInBed
        TotalSectors = totalSectorsInBed
        NativeCylinders = nativeCylindersInBed
        NativeHeads = nativeHeadsInBed
        NativeSectorsPerTrack = nativeSectorsPerTrackInBed
        BiosCylinders = biosCylindersInBed
        BiosHeads = biosHeadsInBed
        BiosSectorsPerTrack = biosSectorsPerTrackInBed
        AtaMajorVersion = ataMajorVersionInBed
        SupportsLba28 = supportsLba28InBed
        MaximumMultipleSectors = maximumMultipleSectorsInBed
        MaximumPioMode = maximumPioModeInBed
        RotationRpm = rotationRpmInBed
        AverageSeekMilliseconds = averageSeekMillisecondsInBed
        SerialPrefix = serialPrefixInBed
    End Sub

    Public ReadOnly Property Id As String
    Public ReadOnly Property DisplayName As String
    Public ReadOnly Property Manufacturer As String
    Public ReadOnly Property Model As String
    Public ReadOnly Property FirmwareRevision As String
    Public ReadOnly Property TotalSectors As Long
    Public ReadOnly Property NativeCylinders As Integer
    Public ReadOnly Property NativeHeads As Integer
    Public ReadOnly Property NativeSectorsPerTrack As Integer
    Public ReadOnly Property BiosCylinders As Integer
    Public ReadOnly Property BiosHeads As Integer
    Public ReadOnly Property BiosSectorsPerTrack As Integer
    Public ReadOnly Property AtaMajorVersion As Integer
    Public ReadOnly Property SupportsLba28 As Boolean
    Public ReadOnly Property MaximumMultipleSectors As Integer
    Public ReadOnly Property MaximumPioMode As Integer
    Public ReadOnly Property RotationRpm As Integer
    Public ReadOnly Property AverageSeekMilliseconds As Double
    Public ReadOnly Property SerialPrefix As String

    Public ReadOnly Property CapacityBytes As Long
        Get
            Return TotalSectors * 512L
        End Get
    End Property

    Public ReadOnly Property CapacityDescription As String
        Get
            Dim mibInBed As Double = CDbl(CapacityBytes) / (1024.0R * 1024.0R)
            Dim mbInBed As Double = CDbl(CapacityBytes) / 1000000.0R
            Return mibInBed.ToString("0.0") & " MiB / " & mbInBed.ToString("0.0") & " MB"
        End Get
    End Property

    Public Function CreateIdentity(Optional serialInBed As String = Nothing) As HardDiskIdentity
        Dim serialTextInBed As String = serialInBed
        If String.IsNullOrWhiteSpace(serialTextInBed) Then
            serialTextInBed = SerialPrefix & TotalSectors.ToString("X")
        End If
        If serialTextInBed.Length > 20 Then serialTextInBed = serialTextInBed.Substring(0, 20)
        Return New HardDiskIdentity With {
            .ProfileId = Id,
            .Manufacturer = Manufacturer,
            .Model = Model,
            .SerialNumber = serialTextInBed,
            .FirmwareRevision = FirmwareRevision,
            .TotalSectors = TotalSectors,
            .NativeCylinders = NativeCylinders,
            .NativeHeads = NativeHeads,
            .NativeSectorsPerTrack = NativeSectorsPerTrack,
            .BiosCylinders = BiosCylinders,
            .BiosHeads = BiosHeads,
            .BiosSectorsPerTrack = BiosSectorsPerTrack,
            .AtaMajorVersion = AtaMajorVersion,
            .SupportsLba28 = SupportsLba28,
            .MaximumMultipleSectors = MaximumMultipleSectors,
            .MaximumPioMode = MaximumPioMode,
            .RotationRpm = RotationRpm,
            .AverageSeekMilliseconds = AverageSeekMilliseconds,
            .Removable = False
        }
    End Function
End Class

' Catalog of physical ATA drive personalities available to the chassis configurator.
' A profile is not merely a label: capacity, translated BIOS geometry and IDENTIFY
' identity are one package.  The raw .hdd/.img remains platter data and its adjacent
' .meta sidecar records which profile was used for that particular physical drive.
Public NotInheritable Class IdeDriveModelCatalog
    Public Const Generic64MiBId As String = "generic-64mib"
    Public Const Generic500MbId As String = "generic-500mb"
    Public Const ConnerCp2064Id As String = "conner-cp2064"
    Public Const ConnerCp30064HId As String = "conner-cp30064h"

    Private Shared ReadOnly _profiles As IdeDriveModelProfile() = {
        New IdeDriveModelProfile(
            Generic64MiBId,
            "Generic 64 MiB IDE HDD",
            "Generic",
            "GENERIC 64M IDE HDD",
            "1.00",
            131072L,
            130, 16, 63,
            130, 16, 63,
            2, True, 16, 2, 3600, 17.0R,
            "G64-"),
        New IdeDriveModelProfile(
            Generic500MbId,
            "Generic 500 MB IDE HDD",
            "Generic",
            "GENERIC 500MB IDE HDD",
            "1.00",
            1024000L,
            1015, 16, 63,
            1015, 16, 63,
            2, True, 16, 2, 3600, 14.0R,
            "G500-"),
        New IdeDriveModelProfile(
            ConnerCp2064Id,
            "Conner CP-2064 — 64 MB",
            "Conner Peripherals",
            "CONNER CP2064",
            "1.00",
            125096L,
            823, 4, 38,
            823, 4, 38,
            1, False, 1, 0, 3600, 19.0R,
            "CP2064-"),
        New IdeDriveModelProfile(
            ConnerCp30064HId,
            "Conner CP30064H — 60 MB",
            "Conner Peripherals",
            "CONNER CP30064H",
            "1.00",
            118872L,
            762, 4, 39,
            762, 4, 39,
            1, False, 1, 0, 3400, 19.0R,
            "CP30064H-")
    }

    Private Sub New()
    End Sub

    Public Shared Function GetProfiles() As IReadOnlyList(Of IdeDriveModelProfile)
        Return _profiles
    End Function

    Public Shared Function FindById(idInBed As String) As IdeDriveModelProfile
        If String.IsNullOrWhiteSpace(idInBed) Then Return Nothing
        For Each profileInBed As IdeDriveModelProfile In _profiles
            If profileInBed.Id.Equals(idInBed.Trim(), StringComparison.OrdinalIgnoreCase) Then Return profileInBed
        Next
        Return Nothing
    End Function

    Public Shared Function FindByExactSectorCount(sectorCountInBed As Long) As List(Of IdeDriveModelProfile)
        Dim resultInBed As New List(Of IdeDriveModelProfile)()
        For Each profileInBed As IdeDriveModelProfile In _profiles
            If profileInBed.TotalSectors = sectorCountInBed Then resultInBed.Add(profileInBed)
        Next
        Return resultInBed
    End Function

    Public Shared Function DefaultProfileIdForSectorCount(sectorCountInBed As Long) As String
        If sectorCountInBed = 131072L Then Return Generic64MiBId
        If sectorCountInBed = 1024000L Then Return Generic500MbId
        Return String.Empty
    End Function

    Public Shared Function Describe(profileInBed As IdeDriveModelProfile) As String
        If profileInBed Is Nothing Then Return "No drive model selected"
        Return profileInBed.Manufacturer & " " & profileInBed.Model & Environment.NewLine &
               "Capacity: " & profileInBed.CapacityDescription & " (" & profileInBed.TotalSectors.ToString() & " sectors)" & Environment.NewLine &
               "Native geometry: " & profileInBed.NativeCylinders.ToString() & " / " & profileInBed.NativeHeads.ToString() & " / " & profileInBed.NativeSectorsPerTrack.ToString() & Environment.NewLine &
               "BIOS geometry: " & profileInBed.BiosCylinders.ToString() & " / " & profileInBed.BiosHeads.ToString() & " / " & profileInBed.BiosSectorsPerTrack.ToString() & Environment.NewLine &
               "ATA-" & profileInBed.AtaMajorVersion.ToString() & If(profileInBed.SupportsLba28, " / LBA28", " / CHS") &
               " / PIO " & profileInBed.MaximumPioMode.ToString() & Environment.NewLine &
               profileInBed.RotationRpm.ToString() & " RPM / " & profileInBed.AverageSeekMilliseconds.ToString("0.0") & " ms average seek"
    End Function
End Class

Public Class HardDiskImage
    Inherits FileBlockMedia

    Public ReadOnly Property Identity As HardDiskIdentity

    Public Sub New(path As String, Optional readOnlyMedia As Boolean = False)
        MyBase.New(path, 512, readOnlyMedia)
        If SectorCount = 0 Then Throw New InvalidDataException("A hard-disk image cannot be empty.")
        Try
            Identity = HardDiskIdentity.LoadOrCreate(ImagePath, SectorCount)
        Catch
            Dispose()
            Throw
        End Try
    End Sub

    Public Shared Sub Create(path As String, sectorCount As Long)
        If sectorCount <= 0 Then Throw New ArgumentOutOfRangeException("sectorCount")
        Using stream As New FileStream(path, FileMode.CreateNew, FileAccess.Write, FileShare.None)
            stream.SetLength(sectorCount * 512L)
        End Using
        HardDiskIdentity.CreateDefault(path, sectorCount).Save(path)
    End Sub

    Public Shared Sub Create(path As String, profileInBed As IdeDriveModelProfile)
        If profileInBed Is Nothing Then Throw New ArgumentNullException(NameOf(profileInBed))
        Using stream As New FileStream(path, FileMode.CreateNew, FileAccess.Write, FileShare.None)
            stream.SetLength(profileInBed.TotalSectors * 512L)
        End Using
        profileInBed.CreateIdentity().Save(path)
    End Sub
End Class

' Persistent identity of the physical ATA drive represented by a raw sector file.
' The .hdd/.img file remains only the platter data.  This adjacent .meta record is
' host hardware configuration; guests can observe it solely through ATA commands.
Public NotInheritable Class HardDiskIdentity
    Private Const CurrentSchemaVersion As Integer = 1

    Public Property SchemaVersion As Integer = CurrentSchemaVersion
    Public Property ProfileId As String = String.Empty
    Public Property Manufacturer As String = "Cromwell Technologies"
    Public Property Model As String = "Cromwell IDE Hard Disk"
    Public Property SerialNumber As String = ""
    Public Property FirmwareRevision As String = "1.00"
    Public Property TotalSectors As Long
    Public Property NativeCylinders As Integer
    Public Property NativeHeads As Integer
    Public Property NativeSectorsPerTrack As Integer
    Public Property BiosCylinders As Integer
    Public Property BiosHeads As Integer
    Public Property BiosSectorsPerTrack As Integer
    Public Property AtaMajorVersion As Integer = 4
    Public Property SupportsLba28 As Boolean = True
    Public Property MaximumMultipleSectors As Integer = 16
    Public Property MaximumPioMode As Integer = 2
    Public Property RotationRpm As Integer = 3600
    Public Property AverageSeekMilliseconds As Double = 17.0
    Public Property Removable As Boolean

    Public Shared Function MetadataPath(imagePath As String) As String
        Return Path.GetFullPath(imagePath) & ".meta"
    End Function

    Public Shared Function LoadOrCreate(imagePath As String, actualSectorCount As Long) As HardDiskIdentity
        Dim sidecar As String = MetadataPath(imagePath)
        If File.Exists(sidecar) Then
            Try
                Dim parsed As HardDiskIdentity = JsonSerializer.Deserialize(Of HardDiskIdentity)(File.ReadAllText(sidecar))
                If parsed Is Nothing Then Throw New InvalidDataException("The hard-disk metadata file is empty.")
                parsed.Validate(actualSectorCount, sidecar)
                If String.IsNullOrWhiteSpace(parsed.ProfileId) Then
                    Dim inferredProfileInBed As IdeDriveModelProfile = Nothing
                    If parsed.Manufacturer.Equals("Cromwell Technologies", StringComparison.OrdinalIgnoreCase) AndAlso
                       parsed.Model.StartsWith("Cromwell CT", StringComparison.OrdinalIgnoreCase) Then
                        Dim inferredIdInBed As String = IdeDriveModelCatalog.DefaultProfileIdForSectorCount(actualSectorCount)
                        inferredProfileInBed = IdeDriveModelCatalog.FindById(inferredIdInBed)
                        If inferredProfileInBed IsNot Nothing Then
                            Dim preservedSerialInBed As String = parsed.SerialNumber
                            parsed = inferredProfileInBed.CreateIdentity(preservedSerialInBed)
                            parsed.Save(imagePath)
                        End If
                    Else
                        For Each candidateInBed As IdeDriveModelProfile In IdeDriveModelCatalog.FindByExactSectorCount(actualSectorCount)
                            If candidateInBed.Model.Equals(parsed.Model, StringComparison.OrdinalIgnoreCase) Then
                                parsed.ProfileId = candidateInBed.Id
                                parsed.Save(imagePath)
                                Exit For
                            End If
                        Next
                    End If
                End If
                Return parsed
            Catch ex As InvalidDataException
                Throw
            Catch ex As Exception
                Throw New InvalidDataException("Unable to read hard-disk metadata '" & sidecar & "'.", ex)
            End Try
        End If

        Dim legacy As HardDiskIdentity = CreateDefault(imagePath, actualSectorCount)
        Try
            legacy.Save(imagePath)
        Catch ex As UnauthorizedAccessException
            ' Read-only legacy images remain usable.  Their conservative identity
            ' is stable for the current attachment even if the sidecar cannot be made.
        Catch ex As IOException
        End Try
        Return legacy
    End Function

    Public Shared Function CreateDefault(imagePath As String, sectorCount As Long) As HardDiskIdentity
        If sectorCount <= 0 Then Throw New ArgumentOutOfRangeException(NameOf(sectorCount))

        Dim profileIdInBed As String = IdeDriveModelCatalog.DefaultProfileIdForSectorCount(sectorCount)
        Dim profileInBed As IdeDriveModelProfile = IdeDriveModelCatalog.FindById(profileIdInBed)
        If profileInBed IsNot Nothing Then
            Dim identityFromProfileInBed As HardDiskIdentity = profileInBed.CreateIdentity()
            identityFromProfileInBed.Validate(sectorCount, MetadataPath(imagePath))
            Return identityFromProfileInBed
        End If

        Dim heads As Integer = If(sectorCount >= 1008, 16, 1)
        Dim sectorsPerTrack As Integer = CInt(Math.Min(63L, sectorCount))
        Dim cylinders As Integer = CInt(Math.Max(1L, Math.Min(1024L, sectorCount \ CLng(heads * sectorsPerTrack))))
        Dim capacityMiB As Long = Math.Max(1L, sectorCount * 512L \ (1024L * 1024L))
        Dim serial As String = "GEN" & sectorCount.ToString("X12")
        Dim identity As New HardDiskIdentity With {
            .ProfileId = String.Empty,
            .Manufacturer = "Generic",
            .Model = "GENERIC " & capacityMiB.ToString() & "M IDE HDD",
            .SerialNumber = serial,
            .TotalSectors = sectorCount,
            .NativeCylinders = cylinders,
            .NativeHeads = heads,
            .NativeSectorsPerTrack = sectorsPerTrack,
            .BiosCylinders = cylinders,
            .BiosHeads = heads,
            .BiosSectorsPerTrack = sectorsPerTrack
        }
        identity.Validate(sectorCount, MetadataPath(imagePath))
        Return identity
    End Function

    Public Sub ApplyProfile(profileInBed As IdeDriveModelProfile, Optional preserveSerialInBed As Boolean = True)
        If profileInBed Is Nothing Then Throw New ArgumentNullException(NameOf(profileInBed))
        If TotalSectors <> profileInBed.TotalSectors Then
            Throw New InvalidDataException("Drive model '" & profileInBed.DisplayName & "' requires " &
                                           profileInBed.TotalSectors.ToString() & " sectors but this image contains " &
                                           TotalSectors.ToString() & ".")
        End If
        Dim replacementInBed As HardDiskIdentity =
            profileInBed.CreateIdentity(If(preserveSerialInBed, SerialNumber, Nothing))
        ProfileId = replacementInBed.ProfileId
        Manufacturer = replacementInBed.Manufacturer
        Model = replacementInBed.Model
        SerialNumber = replacementInBed.SerialNumber
        FirmwareRevision = replacementInBed.FirmwareRevision
        NativeCylinders = replacementInBed.NativeCylinders
        NativeHeads = replacementInBed.NativeHeads
        NativeSectorsPerTrack = replacementInBed.NativeSectorsPerTrack
        BiosCylinders = replacementInBed.BiosCylinders
        BiosHeads = replacementInBed.BiosHeads
        BiosSectorsPerTrack = replacementInBed.BiosSectorsPerTrack
        AtaMajorVersion = replacementInBed.AtaMajorVersion
        SupportsLba28 = replacementInBed.SupportsLba28
        MaximumMultipleSectors = replacementInBed.MaximumMultipleSectors
        MaximumPioMode = replacementInBed.MaximumPioMode
        RotationRpm = replacementInBed.RotationRpm
        AverageSeekMilliseconds = replacementInBed.AverageSeekMilliseconds
        Removable = replacementInBed.Removable
    End Sub

    Public Sub Save(imagePath As String)
        Validate(TotalSectors, MetadataPath(imagePath))
        Dim options As New JsonSerializerOptions With {.WriteIndented = True}
        Dim sidecar As String = MetadataPath(imagePath)
        Dim temporary As String = sidecar & ".tmp"
        File.WriteAllText(temporary, JsonSerializer.Serialize(Me, options))
        File.Move(temporary, sidecar, True)
    End Sub

    Public Sub Validate(actualSectorCount As Long, sourceDescription As String)
        If SchemaVersion <> CurrentSchemaVersion Then
            Throw New InvalidDataException("Unsupported hard-disk metadata schema " & SchemaVersion.ToString() & " in " & sourceDescription & ".")
        End If
        If TotalSectors <> actualSectorCount Then
            Throw New InvalidDataException("Hard-disk metadata capacity does not match the raw image in " & sourceDescription & ".")
        End If
        If Not String.IsNullOrWhiteSpace(ProfileId) Then ValidateText(ProfileId, 64, NameOf(ProfileId), sourceDescription)
        ValidateText(Manufacturer, 40, NameOf(Manufacturer), sourceDescription)
        ValidateText(Model, 40, NameOf(Model), sourceDescription)
        ValidateText(SerialNumber, 20, NameOf(SerialNumber), sourceDescription)
        ValidateText(FirmwareRevision, 8, NameOf(FirmwareRevision), sourceDescription)
        ValidateGeometry(NativeCylinders, NativeHeads, NativeSectorsPerTrack, "native", actualSectorCount, sourceDescription)
        ValidateGeometry(BiosCylinders, BiosHeads, BiosSectorsPerTrack, "BIOS", actualSectorCount, sourceDescription)
        If AtaMajorVersion < 1 OrElse AtaMajorVersion > 14 Then Throw New InvalidDataException("Invalid ATA version in " & sourceDescription & ".")
        If MaximumMultipleSectors < 0 OrElse MaximumMultipleSectors > 255 Then Throw New InvalidDataException("Invalid multiple-sector count in " & sourceDescription & ".")
        If MaximumPioMode < 0 OrElse MaximumPioMode > 4 Then Throw New InvalidDataException("Invalid PIO mode in " & sourceDescription & ".")
        If RotationRpm < 0 OrElse RotationRpm > 100000 Then Throw New InvalidDataException("Invalid spindle speed in " & sourceDescription & ".")
        If Double.IsNaN(AverageSeekMilliseconds) OrElse Double.IsInfinity(AverageSeekMilliseconds) OrElse AverageSeekMilliseconds < 0 Then
            Throw New InvalidDataException("Invalid seek time in " & sourceDescription & ".")
        End If
    End Sub

    Private Shared Sub ValidateGeometry(cylinders As Integer, heads As Integer, sectorsPerTrack As Integer,
                                        label As String, actualSectorCount As Long, sourceDescription As String)
        If cylinders < 1 OrElse cylinders > 65535 OrElse heads < 1 OrElse heads > 16 OrElse sectorsPerTrack < 1 OrElse sectorsPerTrack > 63 Then
            Throw New InvalidDataException("Invalid " & label & " CHS geometry in " & sourceDescription & ".")
        End If
        Dim addressable As Long = CLng(cylinders) * heads * sectorsPerTrack
        If addressable > actualSectorCount Then
            Throw New InvalidDataException("The " & label & " CHS geometry exceeds the raw image in " & sourceDescription & ".")
        End If
    End Sub

    Private Shared Sub ValidateText(value As String, maximumLength As Integer, fieldName As String, sourceDescription As String)
        If String.IsNullOrWhiteSpace(value) OrElse value.Length > maximumLength Then
            Throw New InvalidDataException("Invalid " & fieldName & " in " & sourceDescription & ".")
        End If
        For Each character As Char In value
            If AscW(character) < &H20 OrElse AscW(character) > &H7E Then
                Throw New InvalidDataException(fieldName & " must contain printable ASCII in " & sourceDescription & ".")
            End If
        Next
    End Sub
End Class

Public Class IsoImage
    Inherits FileBlockMedia

    Public Sub New(path As String)
        MyBase.New(path, 2048, True)
        If SectorCount < 17 Then Throw New InvalidDataException("ISO image is too small to contain a volume descriptor.")
        Dim descriptor As Byte() = ReadSector(16)
        If descriptor(1) <> AscW("C"c) OrElse descriptor(2) <> AscW("D"c) OrElse
           descriptor(3) <> AscW("0"c) OrElse descriptor(4) <> AscW("0"c) OrElse descriptor(5) <> AscW("1"c) Then
            Throw New InvalidDataException("The image does not contain an ISO-9660 primary volume descriptor.")
        End If
    End Sub
End Class
