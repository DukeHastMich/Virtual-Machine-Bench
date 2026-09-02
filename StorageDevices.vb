Imports System
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
End Class

' Persistent identity of the physical ATA drive represented by a raw sector file.
' The .hdd/.img file remains only the platter data.  This adjacent .meta record is
' host hardware configuration; guests can observe it solely through ATA commands.
Public NotInheritable Class HardDiskIdentity
    Private Const CurrentSchemaVersion As Integer = 1

    Public Property SchemaVersion As Integer = CurrentSchemaVersion
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
        Dim heads As Integer = If(sectorCount >= 1008, 16, 1)
        Dim sectorsPerTrack As Integer = CInt(Math.Min(63L, sectorCount))
        Dim cylinders As Integer = CInt(Math.Max(1L, Math.Min(1024L, sectorCount \ CLng(heads * sectorsPerTrack))))
        Dim capacityMiB As Long = Math.Max(1L, sectorCount * 512L \ (1024L * 1024L))
        Dim serial As String = "CT" & sectorCount.ToString("X12")
        Dim identity As New HardDiskIdentity With {
            .Model = "Cromwell CT" & capacityMiB.ToString() & " IDE",
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
