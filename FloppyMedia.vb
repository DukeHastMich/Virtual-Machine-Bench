Imports System
Imports System.ComponentModel
Imports System.Collections.Generic
Imports System.IO
Imports System.Runtime.InteropServices
Imports Microsoft.Win32.SafeHandles

' CROMWELL FLOPPY MEDIA ATTACHMENT BRICK 01
' The emulated FDD is a permanent mechanical device.  What changes is the
' medium/backing source connected to that drive: a raw image, a host physical
' floppy drive, or nothing.  The FDC never needs to know which source supplies
' the sectors.
Public Enum FloppyMediaSourceKind
    Image = 0
    PhysicalDrive = 1
End Enum

Public NotInheritable Class FloppyMediaGeometry
    Public ReadOnly Property Cylinders As Integer
    Public ReadOnly Property Heads As Integer
    Public ReadOnly Property SectorsPerTrack As Integer
    Public ReadOnly Property BytesPerSector As Integer

    Public Sub New(cylindersInBed As Integer,
                   headsInBed As Integer,
                   sectorsPerTrackInBed As Integer,
                   bytesPerSectorInBed As Integer)
        If cylindersInBed <= 0 Then Throw New ArgumentOutOfRangeException(NameOf(cylindersInBed))
        If headsInBed <= 0 Then Throw New ArgumentOutOfRangeException(NameOf(headsInBed))
        If sectorsPerTrackInBed <= 0 Then Throw New ArgumentOutOfRangeException(NameOf(sectorsPerTrackInBed))
        If bytesPerSectorInBed <= 0 Then Throw New ArgumentOutOfRangeException(NameOf(bytesPerSectorInBed))
        Cylinders = cylindersInBed
        Heads = headsInBed
        SectorsPerTrack = sectorsPerTrackInBed
        BytesPerSector = bytesPerSectorInBed
    End Sub

    Public ReadOnly Property SectorCount As Long
        Get
            Return CLng(Cylinders) * CLng(Heads) * CLng(SectorsPerTrack)
        End Get
    End Property

    Public ReadOnly Property TotalBytes As Long
        Get
            Return SectorCount * CLng(BytesPerSector)
        End Get
    End Property

    Public Function ChsToLba(cylinderInBed As Integer,
                             headInBed As Integer,
                             sectorInBed As Integer) As Long
        If cylinderInBed < 0 OrElse cylinderInBed >= Cylinders Then Throw New ArgumentOutOfRangeException(NameOf(cylinderInBed))
        If headInBed < 0 OrElse headInBed >= Heads Then Throw New ArgumentOutOfRangeException(NameOf(headInBed))
        If sectorInBed < 1 OrElse sectorInBed > SectorsPerTrack Then Throw New ArgumentOutOfRangeException(NameOf(sectorInBed))
        Return (CLng(cylinderInBed) * Heads + headInBed) * SectorsPerTrack + (sectorInBed - 1)
    End Function
End Class

Public Interface IFloppyMediaSource
    Inherits IDisposable

    ReadOnly Property SourceKind As FloppyMediaSourceKind
    ReadOnly Property SourceId As String
    ReadOnly Property DisplayName As String
    ReadOnly Property IsPresent As Boolean
    ReadOnly Property IsWriteProtected As Boolean
    ReadOnly Property Geometry As FloppyMediaGeometry

    Function ReadSector(lbaInBed As Long) As Byte()
    Sub WriteSector(lbaInBed As Long, dataInBed As Byte())
    Sub Flush()
End Interface

Public NotInheritable Class ImageFloppyMediaSource
    Implements IFloppyMediaSource

    Private _imageInBed As FloppyImage
    Private ReadOnly _geometryInBed As FloppyMediaGeometry

    Public Sub New(imageInBed As FloppyImage)
        If imageInBed Is Nothing Then Throw New ArgumentNullException(NameOf(imageInBed))
        _imageInBed = imageInBed
        _geometryInBed = New FloppyMediaGeometry(imageInBed.Cylinders,
                                                 imageInBed.Heads,
                                                 imageInBed.SectorsPerTrack,
                                                 imageInBed.SectorSize)
    End Sub

    Public ReadOnly Property SourceKind As FloppyMediaSourceKind Implements IFloppyMediaSource.SourceKind
        Get
            Return FloppyMediaSourceKind.Image
        End Get
    End Property

    Public ReadOnly Property SourceId As String Implements IFloppyMediaSource.SourceId
        Get
            EnsureNotDisposed()
            Return "image|" & Path.GetFullPath(_imageInBed.ImagePath)
        End Get
    End Property

    Public ReadOnly Property DisplayName As String Implements IFloppyMediaSource.DisplayName
        Get
            EnsureNotDisposed()
            Return Path.GetFileName(_imageInBed.ImagePath)
        End Get
    End Property

    Public ReadOnly Property IsPresent As Boolean Implements IFloppyMediaSource.IsPresent
        Get
            Return _imageInBed IsNot Nothing
        End Get
    End Property

    Public ReadOnly Property IsWriteProtected As Boolean Implements IFloppyMediaSource.IsWriteProtected
        Get
            EnsureNotDisposed()
            Return _imageInBed.IsReadOnly
        End Get
    End Property

    Public ReadOnly Property Geometry As FloppyMediaGeometry Implements IFloppyMediaSource.Geometry
        Get
            EnsureNotDisposed()
            Return _geometryInBed
        End Get
    End Property

    Public Function ReadSector(lbaInBed As Long) As Byte() Implements IFloppyMediaSource.ReadSector
        EnsureNotDisposed()
        Return _imageInBed.ReadSector(lbaInBed)
    End Function

    Public Sub WriteSector(lbaInBed As Long, dataInBed As Byte()) Implements IFloppyMediaSource.WriteSector
        EnsureNotDisposed()
        _imageInBed.WriteSector(lbaInBed, dataInBed)
    End Sub

    Public Sub Flush() Implements IFloppyMediaSource.Flush
        EnsureNotDisposed()
        _imageInBed.Flush()
    End Sub

    Private Sub EnsureNotDisposed()
        If _imageInBed Is Nothing Then Throw New ObjectDisposedException(NameOf(ImageFloppyMediaSource))
    End Sub

    Public Sub Dispose() Implements IDisposable.Dispose
        If _imageInBed Is Nothing Then Return
        _imageInBed.Dispose()
        _imageInBed = Nothing
    End Sub
End Class

Public NotInheritable Class PhysicalFloppyMediaSource
    Implements IFloppyMediaSource

    Private Const GenericReadInBed As UInteger = &H80000000UI
    Private Const GenericWriteInBed As UInteger = &H40000000UI
    Private Const FileShareReadInBed As UInteger = &H1UI
    Private Const FileShareWriteInBed As UInteger = &H2UI
    Private Const OpenExistingInBed As UInteger = 3UI
    Private Const DriveRemovableInBed As UInteger = 2UI
    Private Const IoctlDiskGetDriveGeometryInBed As UInteger = &H70000UI
    Private Const IoctlDiskIsWritableInBed As UInteger = &H70024UI
    Private Const ErrorWriteProtectInBed As Integer = 19
    Private Const ErrorNotReadyInBed As Integer = 21
    Private Const ErrorNoMediaInDriveInBed As Integer = 1112

    <StructLayout(LayoutKind.Sequential)>
    Private Structure NativeDiskGeometryInBed
        Public Cylinders As Long
        Public MediaType As Integer
        Public TracksPerCylinder As UInteger
        Public SectorsPerTrack As UInteger
        Public BytesPerSector As UInteger
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
    Private Shared Function DeviceIoControlGeometryInBed(hDevice As SafeFileHandle,
                                                         dwIoControlCode As UInteger,
                                                         lpInBuffer As IntPtr,
                                                         nInBufferSize As UInteger,
                                                         ByRef lpOutBuffer As NativeDiskGeometryInBed,
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

    <DllImport("kernel32.dll", EntryPoint:="GetDriveTypeW", CharSet:=CharSet.Unicode)>
    Private Shared Function GetDriveTypeInBed(lpRootPathName As String) As UInteger
    End Function

    Private ReadOnly _hostRootInBed As String
    Private ReadOnly _devicePathInBed As String
    Private _disposedInBed As Boolean

    Public Sub New(hostDriveRootInBed As String)
        If String.IsNullOrWhiteSpace(hostDriveRootInBed) Then Throw New ArgumentException("A host floppy drive is required.", NameOf(hostDriveRootInBed))
        Dim fullRootInBed As String = Path.GetPathRoot(hostDriveRootInBed)
        If String.IsNullOrWhiteSpace(fullRootInBed) OrElse fullRootInBed.Length < 2 OrElse fullRootInBed(1) <> ":"c Then
            Throw New ArgumentException("The host floppy source must be a drive-letter root such as A:\", NameOf(hostDriveRootInBed))
        End If
        _hostRootInBed = Char.ToUpperInvariant(fullRootInBed(0)).ToString() & ":\"
        _devicePathInBed = "\\.\" & _hostRootInBed.Substring(0, 2)
    End Sub

    Public ReadOnly Property HostDriveRoot As String
        Get
            Return _hostRootInBed
        End Get
    End Property

    Public ReadOnly Property SourceKind As FloppyMediaSourceKind Implements IFloppyMediaSource.SourceKind
        Get
            Return FloppyMediaSourceKind.PhysicalDrive
        End Get
    End Property

    Public ReadOnly Property SourceId As String Implements IFloppyMediaSource.SourceId
        Get
            Return SourceIdForHostRoot(_hostRootInBed)
        End Get
    End Property

    Public ReadOnly Property DisplayName As String Implements IFloppyMediaSource.DisplayName
        Get
            Return "Host " & _hostRootInBed.Substring(0, 2) & " physical floppy"
        End Get
    End Property

    Public ReadOnly Property IsPresent As Boolean Implements IFloppyMediaSource.IsPresent
        Get
            EnsureNotDisposed()
            Dim nativeInBed As NativeDiskGeometryInBed
            Dim errorInBed As Integer
            Return TryReadNativeGeometryInBed(nativeInBed, errorInBed)
        End Get
    End Property

    Public ReadOnly Property IsWriteProtected As Boolean Implements IFloppyMediaSource.IsWriteProtected
        Get
            EnsureNotDisposed()
            Dim handleInBed As SafeFileHandle = Nothing
            Try
                handleInBed = OpenVolumeHandleInBed(GenericReadInBed, throwOnFailureInBed:=False)
                If handleInBed Is Nothing OrElse handleInBed.IsInvalid Then Return False
                Dim returnedInBed As UInteger
                If DeviceIoControlNoBufferInBed(handleInBed,
                                                IoctlDiskIsWritableInBed,
                                                IntPtr.Zero,
                                                0UI,
                                                IntPtr.Zero,
                                                0UI,
                                                returnedInBed,
                                                IntPtr.Zero) Then
                    Return False
                End If
                Return Marshal.GetLastWin32Error() = ErrorWriteProtectInBed
            Finally
                If handleInBed IsNot Nothing Then handleInBed.Dispose()
            End Try
        End Get
    End Property

    Public ReadOnly Property Geometry As FloppyMediaGeometry Implements IFloppyMediaSource.Geometry
        Get
            EnsureNotDisposed()
            Dim nativeInBed As NativeDiskGeometryInBed
            Dim errorInBed As Integer
            If Not TryReadNativeGeometryInBed(nativeInBed, errorInBed) Then
                Throw CreatePhysicalIoExceptionInBed("Unable to read the physical floppy geometry from " & _hostRootInBed, errorInBed)
            End If
            If nativeInBed.BytesPerSector <> 512UI Then
                Throw New InvalidDataException("The physical floppy reports " & nativeInBed.BytesPerSector.ToString() & " bytes per sector; this FDC path currently requires 512-byte sectors.")
            End If
            If nativeInBed.Cylinders <= 0 OrElse nativeInBed.Cylinders > Integer.MaxValue OrElse
               nativeInBed.TracksPerCylinder = 0UI OrElse nativeInBed.SectorsPerTrack = 0UI Then
                Throw New InvalidDataException("The host returned an invalid physical floppy geometry.")
            End If
            Return New FloppyMediaGeometry(CInt(nativeInBed.Cylinders),
                                           CInt(nativeInBed.TracksPerCylinder),
                                           CInt(nativeInBed.SectorsPerTrack),
                                           CInt(nativeInBed.BytesPerSector))
        End Get
    End Property

    Public Function ReadSector(lbaInBed As Long) As Byte() Implements IFloppyMediaSource.ReadSector
        EnsureNotDisposed()
        Dim geometryInBed As FloppyMediaGeometry = Geometry
        ValidateLbaInBed(lbaInBed, geometryInBed)
        Dim resultInBed(geometryInBed.BytesPerSector - 1) As Byte
        Using handleInBed As SafeFileHandle = OpenVolumeHandleInBed(GenericReadInBed, throwOnFailureInBed:=True)
            Using streamInBed As New FileStream(handleInBed, FileAccess.Read, 4096, False)
                streamInBed.Seek(lbaInBed * CLng(geometryInBed.BytesPerSector), SeekOrigin.Begin)
                Dim offsetInBed As Integer = 0
                While offsetInBed < resultInBed.Length
                    Dim readInBed As Integer = streamInBed.Read(resultInBed, offsetInBed, resultInBed.Length - offsetInBed)
                    If readInBed <= 0 Then Throw New EndOfStreamException("Unexpected end of physical floppy media.")
                    offsetInBed += readInBed
                End While
            End Using
        End Using
        Return resultInBed
    End Function

    Public Sub WriteSector(lbaInBed As Long, dataInBed As Byte()) Implements IFloppyMediaSource.WriteSector
        EnsureNotDisposed()
        Dim geometryInBed As FloppyMediaGeometry = Geometry
        If dataInBed Is Nothing OrElse dataInBed.Length <> geometryInBed.BytesPerSector Then
            Throw New ArgumentException("A physical floppy write must contain exactly one sector.", NameOf(dataInBed))
        End If
        ValidateLbaInBed(lbaInBed, geometryInBed)
        If IsWriteProtected Then Throw New UnauthorizedAccessException("The physical floppy in " & _hostRootInBed & " is write-protected.")

        Using handleInBed As SafeFileHandle = OpenVolumeHandleInBed(GenericReadInBed Or GenericWriteInBed, throwOnFailureInBed:=True)
            Using streamInBed As New FileStream(handleInBed, FileAccess.ReadWrite, 4096, False)
                streamInBed.Seek(lbaInBed * CLng(geometryInBed.BytesPerSector), SeekOrigin.Begin)
                streamInBed.Write(dataInBed, 0, dataInBed.Length)
                streamInBed.Flush(True)
            End Using
        End Using
    End Sub

    Public Sub Flush() Implements IFloppyMediaSource.Flush
        ' Physical writes are flushed before their temporary raw-volume handle is
        ' released, so there is no persistent host buffer to flush here.
    End Sub

    Public Shared Function SourceIdForHostRoot(hostDriveRootInBed As String) As String
        Dim rootInBed As String = Path.GetPathRoot(hostDriveRootInBed)
        If String.IsNullOrWhiteSpace(rootInBed) Then rootInBed = hostDriveRootInBed
        Return "physical|" & rootInBed.TrimEnd("\"c).ToUpperInvariant()
    End Function

    Public Shared Function EnumerateHostFloppyRoots() As List(Of String)
        Dim resultInBed As New List(Of String)()
        For codeInBed As Integer = AscW("A"c) To AscW("Z"c)
            Dim rootInBed As String = ChrW(codeInBed) & ":\"
            If GetDriveTypeInBed(rootInBed) <> DriveRemovableInBed Then Continue For

            If codeInBed = AscW("A"c) OrElse codeInBed = AscW("B"c) Then
                resultInBed.Add(rootInBed)
                Continue For
            End If

            ' Nontraditional drive letters are admitted only when the inserted
            ' medium actually reports floppy-scale geometry.  This keeps ordinary
            ' USB flash drives out of the physical-floppy menu.
            Try
                Using candidateInBed As New PhysicalFloppyMediaSource(rootInBed)
                    If candidateInBed.IsPresent Then
                        Dim geometryInBed As FloppyMediaGeometry = candidateInBed.Geometry
                        If geometryInBed.TotalBytes <= 4L * 1024L * 1024L AndAlso
                           geometryInBed.Heads <= 2 AndAlso
                           geometryInBed.SectorsPerTrack <= 36 Then
                            resultInBed.Add(rootInBed)
                        End If
                    End If
                End Using
            Catch
                ' Ignore removable devices that are not raw floppy media.
            End Try
        Next
        Return resultInBed
    End Function

    Public Shared Function DescribeHostDrive(hostDriveRootInBed As String) As String
        Try
            Using sourceInBed As New PhysicalFloppyMediaSource(hostDriveRootInBed)
                Dim prefixInBed As String = sourceInBed.HostDriveRoot.Substring(0, 2) & " physical floppy"
                If Not sourceInBed.IsPresent Then Return prefixInBed & " — no disk inserted"
                Dim geometryInBed As FloppyMediaGeometry = sourceInBed.Geometry
                Return prefixInBed & " — " & FormatCapacityInBed(geometryInBed.TotalBytes)
            End Using
        Catch
            Dim rootInBed As String = Path.GetPathRoot(hostDriveRootInBed)
            If String.IsNullOrWhiteSpace(rootInBed) Then rootInBed = hostDriveRootInBed
            Return rootInBed.TrimEnd("\"c) & " physical floppy"
        End Try
    End Function

    Private Shared Function FormatCapacityInBed(bytesInBed As Long) As String
        Select Case bytesInBed
            Case 163840L : Return "160 KB"
            Case 184320L : Return "180 KB"
            Case 327680L : Return "320 KB"
            Case 368640L : Return "360 KB"
            Case 737280L : Return "720 KB"
            Case 1228800L : Return "1.2 MB"
            Case 1474560L : Return "1.44 MB"
            Case 2949120L : Return "2.88 MB"
            Case Else : Return Math.Round(bytesInBed / 1024.0R, 0).ToString() & " KB"
        End Select
    End Function

    Private Function TryReadNativeGeometryInBed(ByRef geometryInBed As NativeDiskGeometryInBed,
                                                ByRef errorInBed As Integer) As Boolean
        errorInBed = 0
        Dim handleInBed As SafeFileHandle = CreateFileInBed(_devicePathInBed,
                                                            GenericReadInBed,
                                                            FileShareReadInBed Or FileShareWriteInBed,
                                                            IntPtr.Zero,
                                                            OpenExistingInBed,
                                                            0UI,
                                                            IntPtr.Zero)
        If handleInBed Is Nothing OrElse handleInBed.IsInvalid Then
            errorInBed = Marshal.GetLastWin32Error()
            If handleInBed IsNot Nothing Then handleInBed.Dispose()
            Return False
        End If
        Using handleInBed
            Dim returnedInBed As UInteger
            Dim okInBed As Boolean = DeviceIoControlGeometryInBed(handleInBed,
                                                                  IoctlDiskGetDriveGeometryInBed,
                                                                  IntPtr.Zero,
                                                                  0UI,
                                                                  geometryInBed,
                                                                  CUInt(Marshal.SizeOf(Of NativeDiskGeometryInBed)()),
                                                                  returnedInBed,
                                                                  IntPtr.Zero)
            If Not okInBed Then errorInBed = Marshal.GetLastWin32Error()
            Return okInBed
        End Using
    End Function

    Private Function OpenVolumeHandleInBed(accessInBed As UInteger,
                                           throwOnFailureInBed As Boolean) As SafeFileHandle
        Dim handleInBed As SafeFileHandle = CreateFileInBed(_devicePathInBed,
                                                            accessInBed,
                                                            FileShareReadInBed Or FileShareWriteInBed,
                                                            IntPtr.Zero,
                                                            OpenExistingInBed,
                                                            0UI,
                                                            IntPtr.Zero)
        If handleInBed IsNot Nothing AndAlso Not handleInBed.IsInvalid Then Return handleInBed

        Dim errorInBed As Integer = Marshal.GetLastWin32Error()
        If handleInBed IsNot Nothing Then handleInBed.Dispose()
        If throwOnFailureInBed Then
            Throw CreatePhysicalIoExceptionInBed("Unable to open physical floppy " & _hostRootInBed, errorInBed)
        End If
        Return Nothing
    End Function

    Private Shared Function CreatePhysicalIoExceptionInBed(messageInBed As String,
                                                           errorInBed As Integer) As Exception
        Dim detailInBed As String
        Select Case errorInBed
            Case ErrorNoMediaInDriveInBed, ErrorNotReadyInBed
                detailInBed = "No disk is inserted or the drive is not ready."
            Case ErrorWriteProtectInBed
                detailInBed = "The disk is write-protected."
            Case Else
                detailInBed = New Win32Exception(errorInBed).Message
        End Select
        Return New IOException(messageInBed & ": " & detailInBed & " (Win32 " & errorInBed.ToString() & ").")
    End Function

    Private Shared Sub ValidateLbaInBed(lbaInBed As Long, geometryInBed As FloppyMediaGeometry)
        If lbaInBed < 0 OrElse lbaInBed >= geometryInBed.SectorCount Then Throw New ArgumentOutOfRangeException(NameOf(lbaInBed))
    End Sub

    Private Sub EnsureNotDisposed()
        If _disposedInBed Then Throw New ObjectDisposedException(NameOf(PhysicalFloppyMediaSource))
    End Sub

    Public Sub Dispose() Implements IDisposable.Dispose
        _disposedInBed = True
    End Sub
End Class

Public NotInheritable Class FloppyDriveUnit
    Implements IDisposable

    Private ReadOnly _driveNumberInBed As Integer
    Private _mediaSourceInBed As IFloppyMediaSource
    Private _currentCylinderInBed As Byte
    Private _motorOnInBed As Boolean

    Public Sub New(driveNumberInBed As Integer)
        If driveNumberInBed < 0 OrElse driveNumberInBed > 3 Then Throw New ArgumentOutOfRangeException(NameOf(driveNumberInBed))
        _driveNumberInBed = driveNumberInBed
    End Sub

    Public ReadOnly Property DriveNumber As Integer
        Get
            Return _driveNumberInBed
        End Get
    End Property

    Public Property CurrentCylinder As Byte
        Get
            Return _currentCylinderInBed
        End Get
        Set(value As Byte)
            _currentCylinderInBed = value
        End Set
    End Property

    Public Property MotorOn As Boolean
        Get
            Return _motorOnInBed
        End Get
        Set(value As Boolean)
            _motorOnInBed = value
        End Set
    End Property

    Public ReadOnly Property HasMediaSource As Boolean
        Get
            Return _mediaSourceInBed IsNot Nothing
        End Get
    End Property

    Public ReadOnly Property MediaPresent As Boolean
        Get
            Return _mediaSourceInBed IsNot Nothing AndAlso _mediaSourceInBed.IsPresent
        End Get
    End Property

    Public ReadOnly Property MediaSourceKind As FloppyMediaSourceKind?
        Get
            If _mediaSourceInBed Is Nothing Then Return Nothing
            Return _mediaSourceInBed.SourceKind
        End Get
    End Property

    Public ReadOnly Property MediaSourceId As String
        Get
            If _mediaSourceInBed Is Nothing Then Return String.Empty
            Return _mediaSourceInBed.SourceId
        End Get
    End Property

    Public ReadOnly Property MediaSourceDisplayName As String
        Get
            If _mediaSourceInBed Is Nothing Then Return String.Empty
            Return _mediaSourceInBed.DisplayName
        End Get
    End Property

    Public ReadOnly Property IsWriteProtected As Boolean
        Get
            Return _mediaSourceInBed IsNot Nothing AndAlso _mediaSourceInBed.IsWriteProtected
        End Get
    End Property

    Public Function TryGetGeometry(ByRef geometryInBed As FloppyMediaGeometry) As Boolean
        geometryInBed = Nothing
        If Not MediaPresent Then Return False
        Try
            geometryInBed = _mediaSourceInBed.Geometry
            Return geometryInBed IsNot Nothing
        Catch
            geometryInBed = Nothing
            Return False
        End Try
    End Function

    Public Sub InsertMediaSource(sourceInBed As IFloppyMediaSource)
        If sourceInBed Is Nothing Then Throw New ArgumentNullException(NameOf(sourceInBed))
        If _mediaSourceInBed IsNot Nothing Then _mediaSourceInBed.Dispose()
        _mediaSourceInBed = sourceInBed
        ' Inserting/ejecting a disk does not move the real drive head.  Preserve
        ' CurrentCylinder; BIOS/FDC software may recalibrate it explicitly.
    End Sub

    Public Sub EjectMediaSource()
        If _mediaSourceInBed IsNot Nothing Then _mediaSourceInBed.Dispose()
        _mediaSourceInBed = Nothing
    End Sub

    Public Function ReadSector(lbaInBed As Long) As Byte()
        EnsureMediaPresent()
        Return _mediaSourceInBed.ReadSector(lbaInBed)
    End Function

    Public Sub WriteSector(lbaInBed As Long, dataInBed As Byte())
        EnsureMediaPresent()
        _mediaSourceInBed.WriteSector(lbaInBed, dataInBed)
    End Sub

    Public Sub Flush()
        EnsureMediaPresent()
        _mediaSourceInBed.Flush()
    End Sub

    Private Sub EnsureMediaPresent()
        If _mediaSourceInBed Is Nothing Then Throw New InvalidOperationException("No floppy media source is attached to drive " & _driveNumberInBed.ToString() & ".")
        If Not _mediaSourceInBed.IsPresent Then Throw New IOException("The attached physical floppy drive has no disk inserted.")
    End Sub

    Public Sub Dispose() Implements IDisposable.Dispose
        EjectMediaSource()
    End Sub
End Class
