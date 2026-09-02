Imports System
Imports System.Collections.Generic

' CROMWELL KEYBOARD REALITY BRICK 4 FDD ACTIVITY
' NEC uPD765A / Intel 8272-compatible PC floppy controller.  Commands arrive
' through the FIFO at 3F5h and sector data moves through 8237 channel 2.
Public Class FloppyController765
    Implements IPortDevice, IResettableDevice, IDisposable

    Private ReadOnly _pic As Pic8259
    Private ReadOnly _dma As Dma8237
    Private ReadOnly _drives(3) As FloppyDriveUnit
    Private ReadOnly _command As New List(Of Byte)()
    Private ReadOnly _result As New Queue(Of Byte)()
    Private _dor As Byte
    Private _selectedDrive As Integer
    Private _pendingInterrupt As Boolean
    Private _status0 As Byte = &HC0
    Private _dataRate As Byte

    Public Event Activity()
    Public Event DriveActivity(drive As Integer)

    Public Sub New(pic As Pic8259, dma As Dma8237)
        If pic Is Nothing Then Throw New ArgumentNullException("pic")
        If dma Is Nothing Then Throw New ArgumentNullException("dma")
        _pic = pic
        _dma = dma
        For driveInBed As Integer = 0 To 3
            _drives(driveInBed) = New FloppyDriveUnit(driveInBed)
        Next
        ResetDevice()
    End Sub

    Public Sub ResetDevice() Implements IResettableDevice.ResetDevice
        ' Controller RESET/DOR reset clears the controller state but does not
        ' physically step the drive heads back to cylinder zero.  Mounted media
        ' and the remembered mechanical head positions therefore survive.
        _dor = 0
        _selectedDrive = 0
        _dataRate = 0
        For driveInBed As Integer = 0 To 3
            _drives(driveInBed).MotorOn = False
        Next
        _command.Clear()
        _result.Clear()
        _pendingInterrupt = False
        _status0 = &HC0
        _pic.ClearIrq(6)
    End Sub

    Public Sub Mount(drive As Integer, image As FloppyImage)
        If image Is Nothing Then Throw New ArgumentNullException(NameOf(image))
        AttachMediaSource(drive, New ImageFloppyMediaSource(image))
    End Sub

    Public Sub AttachMediaSource(drive As Integer, sourceInBed As IFloppyMediaSource)
        ValidateDrive(drive)
        If sourceInBed Is Nothing Then Throw New ArgumentNullException(NameOf(sourceInBed))
        _drives(drive).InsertMediaSource(sourceInBed)
    End Sub

    Public Sub Eject(drive As Integer)
        ValidateDrive(drive)
        _drives(drive).EjectMediaSource()
    End Sub

    ' Compatibility meaning: a backing source is attached.  A physical source can
    ' remain attached while the human removes the actual diskette from its drive.
    Public Function IsMounted(drive As Integer) As Boolean
        ValidateDrive(drive)
        Return _drives(drive).HasMediaSource
    End Function

    Public Function IsMediaPresent(drive As Integer) As Boolean
        ValidateDrive(drive)
        Return _drives(drive).MediaPresent
    End Function

    Public Function GetMediaSourceId(drive As Integer) As String
        ValidateDrive(drive)
        Return _drives(drive).MediaSourceId
    End Function

    Public Function GetMediaSourceKind(drive As Integer) As FloppyMediaSourceKind?
        ValidateDrive(drive)
        Return _drives(drive).MediaSourceKind
    End Function

    Public Function GetMediaSourceDisplayName(drive As Integer) As String
        ValidateDrive(drive)
        Return _drives(drive).MediaSourceDisplayName
    End Function

    Public Function GetAttachmentStatus(drive As Integer) As String
        ValidateDrive(drive)
        Dim letterInBed As Char = ChrW(AscW("A"c) + drive)
        If Not _drives(drive).HasMediaSource Then Return "Floppy " & letterInBed & ": empty"
        Dim suffixInBed As String = If(_drives(drive).MediaPresent, String.Empty, " — no disk inserted")
        Return "Floppy " & letterInBed & ": " & _drives(drive).MediaSourceDisplayName & suffixInBed
    End Function

    Public Function GetGeometry(drive As Integer) As Integer()
        ValidateDrive(drive)
        Dim geometryInBed As FloppyMediaGeometry = Nothing
        If Not _drives(drive).TryGetGeometry(geometryInBed) Then Return Nothing
        Return New Integer() {geometryInBed.Cylinders, geometryInBed.Heads, geometryInBed.SectorsPerTrack}
    End Function

    ' Firmware-facing access still terminates in the same mounted controller media.
    Public Function BiosRead(drive As Integer, cylinder As Integer, head As Integer, sector As Integer, count As Integer) As Byte()
        ValidateDrive(drive)
        If Not _drives(drive).MediaPresent OrElse count < 1 Then Return Nothing
        RaiseEvent Activity()
        RaiseEvent DriveActivity(drive)
        Try
            Dim geometryInBed As FloppyMediaGeometry = Nothing
            If Not _drives(drive).TryGetGeometry(geometryInBed) Then Return Nothing
            Dim result(count * 512 - 1) As Byte
            For index As Integer = 0 To count - 1
                Dim currentSector As Integer = sector + index
                Dim currentHead As Integer = head
                Dim currentCylinder As Integer = cylinder
                While currentSector > geometryInBed.SectorsPerTrack
                    currentSector -= geometryInBed.SectorsPerTrack
                    currentHead += 1
                    If currentHead >= geometryInBed.Heads Then currentHead = 0 : currentCylinder += 1
                End While
                Dim lba As Long = geometryInBed.ChsToLba(currentCylinder, currentHead, currentSector)
                Array.Copy(_drives(drive).ReadSector(lba), 0, result, index * 512, 512)
            Next
            Return result
        Catch
            Return Nothing
        End Try
    End Function

    Public Function BiosWrite(drive As Integer, cylinder As Integer, head As Integer, sector As Integer, data As Byte()) As Boolean
        ValidateDrive(drive)
        If Not _drives(drive).MediaPresent OrElse data Is Nothing OrElse data.Length = 0 OrElse data.Length Mod 512 <> 0 Then Return False
        If _drives(drive).IsWriteProtected Then Return False
        RaiseEvent Activity()
        RaiseEvent DriveActivity(drive)
        Try
            Dim geometryInBed As FloppyMediaGeometry = Nothing
            If Not _drives(drive).TryGetGeometry(geometryInBed) Then Return False
            Dim count As Integer = data.Length \ 512
            For index As Integer = 0 To count - 1
                Dim currentSector As Integer = sector + index
                Dim currentHead As Integer = head
                Dim currentCylinder As Integer = cylinder
                While currentSector > geometryInBed.SectorsPerTrack
                    currentSector -= geometryInBed.SectorsPerTrack
                    currentHead += 1
                    If currentHead >= geometryInBed.Heads Then currentHead = 0 : currentCylinder += 1
                End While
                Dim sectorData(511) As Byte
                Array.Copy(data, index * 512, sectorData, 0, 512)
                _drives(drive).WriteSector(geometryInBed.ChsToLba(currentCylinder, currentHead, currentSector), sectorData)
            Next
            _drives(drive).Flush()
            Return True
        Catch
            Return False
        End Try
    End Function

    Public Function HandlesPort(port As UInt16) As Boolean Implements IPortDevice.HandlesPort
        Return port >= &H3F2 AndAlso port <= &H3F7
    End Function

    Public Function ReadPort(port As UInt16) As Byte Implements IPortDevice.ReadPort
        Select Case port
            Case &H3F2 : Return _dor
            Case &H3F4
                Dim directionToCpu As Boolean = _result.Count > 0
                Return CByte(&H80 Or If(directionToCpu, &H40, 0) Or If(_command.Count > 0, &H10, 0))
            Case &H3F5
                If _result.Count = 0 Then Return &HFF
                Dim value As Byte = _result.Dequeue()
                If _result.Count = 0 Then _pic.ClearIrq(6)
                Return value
            Case &H3F7 : Return _dataRate
            Case Else : Return &HFF
        End Select
    End Function

    Public Sub WritePort(port As UInt16, value As Byte) Implements IPortDevice.WritePort
        Select Case port
            Case &H3F2
                Dim wasReset As Boolean = (_dor And 4) = 0
                _dor = value
                _selectedDrive = value And 3
                For driveInBed As Integer = 0 To 3
                    _drives(driveInBed).MotorOn = (value And (1 << (4 + driveInBed))) <> 0
                Next
                If (value And 4) = 0 Then
                    ResetController()
                ElseIf wasReset Then
                    _pendingInterrupt = True
                    _status0 = &HC0
                    _pic.RaiseIrq(6)
                End If
            Case &H3F5
                If _result.Count = 0 Then AcceptCommandByte(value)
            Case &H3F7
                _dataRate = CByte(value And 3)
        End Select
    End Sub

    Private Sub AcceptCommandByte(value As Byte)
        _command.Add(value)
        If _command.Count >= ExpectedLength(_command(0)) Then
            ExecuteCommand()
            _command.Clear()
        End If
    End Sub

    Private Shared Function ExpectedLength(command As Byte) As Integer
        Select Case command And &H1F
            Case &H3 : Return 3
            Case &H4 : Return 2
            Case &H5, &H6, &H9, &HC : Return 9
            Case &H7 : Return 2
            Case &H8 : Return 1
            Case &HA : Return 2
            Case &HF : Return 3
            Case &H10 : Return 1
            Case Else : Return 1
        End Select
    End Function

    Private Sub ExecuteCommand()
        Select Case _command(0) And &H1F
            Case &H3 ' SPECIFY
            Case &H4 ' SENSE DRIVE STATUS
                Dim drive As Integer = _command(1) And 3
                Dim head As Integer = (_command(1) >> 2) And 1
                Dim status3 As Byte = CByte(drive Or (head << 2))
                Dim geometryInBed As FloppyMediaGeometry = Nothing
                If _drives(drive).MediaPresent Then status3 = CByte(status3 Or &H20)
                If _drives(drive).CurrentCylinder = 0 Then status3 = CByte(status3 Or &H10)
                If _drives(drive).TryGetGeometry(geometryInBed) AndAlso geometryInBed.Heads > 1 Then status3 = CByte(status3 Or &H8)
                If _drives(drive).IsWriteProtected Then status3 = CByte(status3 Or &H40)
                _result.Enqueue(status3)
            Case &H5, &H9 ' WRITE DATA / WRITE DELETED DATA
                TransferSector(False)
            Case &H6, &HC ' READ DATA / READ DELETED DATA
                TransferSector(True)
            Case &H7 ' RECALIBRATE
                Dim drive As Integer = _command(1) And 3
                _drives(drive).CurrentCylinder = 0
                CompleteSeek(drive, 0)
            Case &H8 ' SENSE INTERRUPT STATUS
                _result.Enqueue(_status0)
                _result.Enqueue(_drives(_selectedDrive).CurrentCylinder)
                _pendingInterrupt = False
                _pic.ClearIrq(6)
            Case &HA ' READ ID
                Dim drive As Integer = _command(1) And 3
                RaiseEvent Activity()
                RaiseEvent DriveActivity(drive)
                If Not _drives(drive).MediaPresent Then
                    QueueTransferResult(drive, 0, 0, 0, 2, &H48, &H4, 0)
                Else
                    QueueTransferResult(drive, 0, _drives(drive).CurrentCylinder, CByte((_command(1) >> 2) And 1), 1, 2, 0, 0)
                End If
            Case &HF ' SEEK
                Dim drive As Integer = _command(1) And 3
                _drives(drive).CurrentCylinder = _command(2)
                CompleteSeek(drive, _command(2))
            Case &H10 ' VERSION
                _result.Enqueue(&H90)
            Case Else
                _result.Enqueue(&H80)
        End Select
    End Sub

    Private Sub CompleteSeek(drive As Integer, cylinder As Byte)
        RaiseEvent Activity()
        RaiseEvent DriveActivity(drive)
        _selectedDrive = drive
        _status0 = CByte(&H20 Or drive)
        _pendingInterrupt = True
        _pic.RaiseIrq(6)
    End Sub

    Private Sub TransferSector(reading As Boolean)
        Dim drive As Integer = _command(1) And 3
        RaiseEvent Activity()
        RaiseEvent DriveActivity(drive)
        Dim head As Integer = (_command(1) >> 2) And 1
        Dim cylinder As Integer = _command(2)
        Dim requestedHead As Integer = _command(3)
        Dim sector As Integer = _command(4)
        Dim sizeCode As Integer = _command(5)
        Dim endOfTrack As Integer = _command(6)
        If Not _drives(drive).MediaPresent Then
            QueueTransferResult(drive, head, cylinder, requestedHead, sector, sizeCode, &H48, &H4)
            Return
        End If
        If sizeCode <> 2 OrElse head <> requestedHead Then
            QueueTransferResult(drive, head, cylinder, requestedHead, sector, sizeCode, &H40, &H20)
            Return
        End If
        If Not reading AndAlso _drives(drive).IsWriteProtected Then
            QueueTransferResult(drive, head, cylinder, requestedHead, sector, sizeCode, &H40, &H2)
            Return
        End If

        Dim geometryInBed As FloppyMediaGeometry = Nothing
        If Not _drives(drive).TryGetGeometry(geometryInBed) Then
            QueueTransferResult(drive, head, cylinder, requestedHead, sector, sizeCode, &H48, &H4)
            Return
        End If

        ' CROMWELL PCB REFIT PHASE 2 BRICK 8D - the floppy controller now drives
        ' a real logical DREQ2 around DMA service.  Dma8237 converts that request
        ' into HRQ according to the programmed demand/single/block mode; the NEAT
        ' bridge performs HOLD/HLDA ownership before each memory bus transaction.
        _dma.SetDreq(2, True)
        Try
            Dim lastSector As Integer = Math.Min(endOfTrack, geometryInBed.SectorsPerTrack)
            Dim transferredSector As Integer = sector
            While transferredSector <= lastSector
                Dim lba As Long = geometryInBed.ChsToLba(cylinder, head, transferredSector)
                If reading Then
                    Dim data As Byte() = _drives(drive).ReadSector(lba)
                    If _dma.TransferToMemory(2, data, 0, data.Length) <> data.Length Then Exit While
                Else
                    Dim data(511) As Byte
                    If _dma.TransferFromMemory(2, data, 0, data.Length) <> data.Length Then Exit While
                    _drives(drive).WriteSector(lba, data)
                End If
                transferredSector += 1
            End While
            If Not reading Then _drives(drive).Flush()
            _drives(drive).CurrentCylinder = CByte(cylinder)
            QueueTransferResult(drive, head, cylinder, requestedHead, Math.Min(transferredSector, lastSector), sizeCode, 0, 0)
        Catch ex As Exception
            QueueTransferResult(drive, head, cylinder, requestedHead, sector, sizeCode, &H40, &H4)
        Finally
            _dma.SetDreq(2, False)
        End Try
    End Sub

    Private Sub QueueTransferResult(drive As Integer, head As Integer, cylinder As Integer, requestedHead As Integer, sector As Integer, sizeCode As Integer, status0Flags As Integer, status1 As Integer)
        _status0 = CByte(status0Flags Or drive Or (head << 2))
        _result.Enqueue(_status0)
        _result.Enqueue(CByte(status1))
        _result.Enqueue(0)
        _result.Enqueue(CByte(cylinder))
        _result.Enqueue(CByte(requestedHead))
        _result.Enqueue(CByte(sector))
        _result.Enqueue(CByte(sizeCode))
        _pendingInterrupt = True
        _pic.RaiseIrq(6)
    End Sub

    Private Sub ResetController()
        _command.Clear()
        _result.Clear()
        _pendingInterrupt = False
        _status0 = &HC0
        _pic.ClearIrq(6)
    End Sub

    Private Shared Sub ValidateDrive(drive As Integer)
        If drive < 0 OrElse drive > 3 Then Throw New ArgumentOutOfRangeException("drive")
    End Sub

    Public Sub Dispose() Implements IDisposable.Dispose
        For drive As Integer = 0 To 3
            _drives(drive).Dispose()
        Next
    End Sub
End Class
