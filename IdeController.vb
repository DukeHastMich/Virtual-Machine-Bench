Imports System
Imports System.Text

' =============================================================================
' IDE / ATA / ATAPI PRIMARY CHANNEL
' =============================================================================
' Emulates the legacy primary IDE channel at:
'
'   1F0h  Data             (16-bit PIO data register)
'   1F1h  Error / Features (read / write)
'   1F2h  Sector Count     (ATA) / Interrupt Reason (ATAPI after PACKET)
'   1F3h  Sector Number    (ATA)
'   1F4h  Cylinder Low     (ATA) / ATAPI Byte Count Low
'   1F5h  Cylinder High    (ATA) / ATAPI Byte Count High
'   1F6h  Device / Head
'   1F7h  Status / Command (read / write)
'   3F6h  Alternate Status / Device Control (read / write)
'
' Physical layout represented here:
'
'   primary IDE channel
'       device 0 (master) = ATA hard disk
'       device 1 (slave)  = ATAPI CD-ROM
'       IRQ14             = slave PIC input 6
'
' IMPORTANT MODELING RULES
' ------------------------
' 1. The IDE CHANNEL owns Device/Head, Device Control and the shared IRQ wire.
' 2. EACH DEVICE owns its own task-file register image and command/transfer state.
'    Selecting the slave must therefore expose the slave's 14h/EBh ATAPI reset
'    signature without destroying or borrowing the master's 00h/00h ATA signature.
' 3. ATAPI PACKET is a PIO state machine even when later packet data could, on real
'    hardware, use DMA.  This emulator currently advertises/implements PIO only.
' 4. ATAPI interrupt-reason bits in 1F2h are not an ATA sector count during packet
'    execution:
'
'        C/D I/O   1F2 low bits   Meaning
'         1   0       01h         host must write command packet
'         0   1       02h         device has data for host
'         1   1       03h         command/status phase complete
'
'    A successful zero-data command MUST finish with 03h, not 02h.  Reporting
'    "data-in" while DRQ is clear is contradictory and can leave DOS drivers such
'    as OAKCDROM.SYS waiting forever for a phase which will never arrive.
' 5. For PACKET data-in, the host places its maximum PIO phase size in 1F4h/1F5h
'    before issuing A0h.  A response larger than that limit must be divided into
'    multiple DRQ phases.  The byte-count registers describe THE CURRENT PHASE,
'    not the entire command response.
'
' This controller deliberately keeps device progress independent of IRQ service.
' A DOS driver is allowed to disable IDE interrupts and poll 1F7h/3F6h instead.
' =============================================================================
Public Class IdeController
    Implements IPortDevice, IWordPortDevice, IResettableDevice, IDisposable

    ' ATA status register bits used by this implementation.
    Private Const StatusError As Byte = &H1
    Private Const StatusDataRequest As Byte = &H8
    Private Const StatusDriveReady As Byte = &H40
    Private Const StatusBusy As Byte = &H80

    ' Device Control register bits.
    Private Const DeviceControlDisableIrq As Byte = &H2
    Private Const DeviceControlSoftwareReset As Byte = &H4

    ' ATAPI interrupt-reason low bits (returned through task-file register 1F2h).
    Private Const AtapiReasonCommandOut As Byte = &H1  ' C/D=1, I/O=0
    Private Const AtapiReasonDataIn As Byte = &H2      ' C/D=0, I/O=1
    Private Const AtapiReasonComplete As Byte = &H3    ' C/D=1, I/O=1

    ' Fixed-format SCSI sense keys used by the packet device.
    Private Const SenseNoSense As Byte = &H0
    Private Const SenseNotReady As Byte = &H2
    Private Const SenseIllegalRequest As Byte = &H5
    Private Const SenseUnitAttention As Byte = &H6

    ' A 12-byte command packet is advertised in IDENTIFY PACKET DEVICE word 0.
    Private Const AtapiPacketBytes As Integer = 12

    ' A host packet byte-count limit of 0000h represents a 64-KiB allowance.
    ' We cap individual phases at 65534 bytes so the live phase count is always
    ' representable as a non-zero 16-bit value even for conservative DOS drivers.
    Private Const AtapiMaximumPioPhase As Integer = 65534

    Private Enum TransferKind As Byte
        None = 0
        AtaRead = 1
        AtaWrite = 2
        AtaIdentify = 3
        AtapiIdentify = 4
        AtapiPacketCommandOut = 5
        AtapiPacketDataIn = 6
    End Enum

    ' -------------------------------------------------------------------------
    ' Per-device state
    ' -------------------------------------------------------------------------
    ' The original controller stored one copy of these registers for both master
    ' and slave.  That is not sufficient for IDE probing: after SRST the master
    ' must retain an ATA signature while the slave simultaneously retains the
    ' ATAPI 14h/EBh signature.  It also made packet state vulnerable to an innocent
    ' device-select write.  Each device now owns a complete task-file image.
    Private NotInheritable Class IdeDeviceState
        Public Features As Byte
        Public ErrorRegister As Byte
        Public SectorCount As Byte
        Public LbaLow As Byte
        Public LbaMid As Byte
        Public LbaHigh As Byte
        Public Status As Byte

        Public Data() As Byte
        Public DataIndex As Integer
        Public Transfer As TransferKind

        ' ATA write/read bookkeeping.
        Public PendingLba As Long
        Public PendingSectors As Integer

        ' ATAPI packet transfer bookkeeping.
        ' PacketByteLimit is captured BEFORE A0h changes LbaMid/LbaHigh into the
        ' byte count for the current DRQ phase.
        Public PacketByteLimit As Integer
        Public PacketResponse() As Byte
        Public PacketResponseOffset As Integer

        ' SCSI sense state.  UnitAttentionPending is distinct because the pending
        ' event must survive until a command reports CHECK CONDITION and REQUEST
        ' SENSE consumes it.
        Public SenseKey As Byte
        Public AdditionalSenseCode As Byte
        Public AdditionalSenseQualifier As Byte
        Public UnitAttentionPending As Boolean

        ' PREVENT/ALLOW MEDIUM REMOVAL state (SCSI command 1Eh).
        Public MediumRemovalPrevented As Boolean
    End Class

    Private ReadOnly _pic As Pic8259
    Private ReadOnly _master As New IdeDeviceState()
    Private ReadOnly _slave As New IdeDeviceState()

    Private _hardDisk As HardDiskImage
    Private _cdrom As IsoImage

    ' Device/Head and Device Control are channel-visible registers.  The Device
    ' bit in _driveHead selects which per-device task file is exposed at 1F1-1F5
    ' and 1F7.
    Private _driveHead As Byte = &HA0
    Private _deviceControl As Byte

    ' Host-only flight recorder.  Entries are made at ATA sector boundaries,
    ' never by issuing extra guest I/O cycles, and survive processor-only reset.
    Private Const AtaDiagnosticCapacityInBed As Integer = 128
    Private ReadOnly _ataDiagnosticInBed As New Collections.Generic.Queue(Of String)()
    Private _ataDiagnosticSequenceInBed As ULong
    Private _ataReadSectorPhasesInBed As ULong
    Private _ataWriteSectorPhasesInBed As ULong
    Private _atapiPacketCommandsInBed As ULong
    Private _atapiReadSectorsInBed As ULong
    Private _atapiDataPhasesInBed As ULong

    Public Event Activity()

    ' pic is the slave 8259 in an AT configuration; IDE is its local IRQ6,
    ' which the cascaded pair exposes to the CPU as IRQ14.
    Public Sub New(pic As Pic8259)
        If pic Is Nothing Then Throw New ArgumentNullException(NameOf(pic))
        _pic = pic
        ResetDevice()
    End Sub

    ' -------------------------------------------------------------------------
    ' Physical reset / media attachment
    ' -------------------------------------------------------------------------
    Public Sub ResetDevice() Implements IResettableDevice.ResetDevice
        ' Motherboard RESET resets both devices and the shared channel controls,
        ' but it does not detach the physical media supplied by the host.
        _driveHead = &HA0
        _deviceControl = 0
        ResetAtaDeviceState(_master)
        ResetAtapiDeviceState(_slave, False)
        _pic.ClearIrq(6)
    End Sub

    Public Sub MountHardDisk(image As HardDiskImage)
        If _hardDisk IsNot Nothing Then _hardDisk.Dispose()
        _hardDisk = image
    End Sub

    Public Sub MountCdRom(image As IsoImage)
        If _cdrom IsNot Nothing Then _cdrom.Dispose()
        _cdrom = image
        TraceAtaInBed("ATAPI media mounted sectors=" & image.SectorCount.ToString())

        ' A newly inserted medium is reported once as UNIT ATTENTION / 28h.
        ' The next ordinary packet command therefore returns CHECK CONDITION;
        ' REQUEST SENSE reports and clears the event, after which normal I/O can
        ' proceed.  This is intentionally separate from simple "medium absent".
        SetUnitAttention(&H28, &H0)
    End Sub

    Public Sub EjectHardDisk()
        If _hardDisk IsNot Nothing Then _hardDisk.Dispose()
        _hardDisk = Nothing
    End Sub

    Public Sub EjectCdRom()
        If _cdrom IsNot Nothing Then _cdrom.Dispose()
        _cdrom = Nothing
        TraceAtaInBed("ATAPI media ejected")
        SetUnitAttention(&H28, &H0)
    End Sub

    Public ReadOnly Property HardDiskSectorCount As Long
        Get
            Return If(_hardDisk Is Nothing, 0, _hardDisk.SectorCount)
        End Get
    End Property

    Public ReadOnly Property CdRomMounted As Boolean
        Get
            Return _cdrom IsNot Nothing
        End Get
    End Property

    ' BIOS-facing helpers remain intentionally outside the task-file protocol.
    ' They represent firmware use of the same physical disk, not a second disk.
    Public Function BiosRead(lba As Long, count As Integer) As Byte()
        If _hardDisk Is Nothing OrElse count < 1 OrElse lba < 0 OrElse lba + count > _hardDisk.SectorCount Then Return Nothing
        RaiseEvent Activity()
        Try
            Dim result(count * 512 - 1) As Byte
            For index As Integer = 0 To count - 1
                Array.Copy(_hardDisk.ReadSector(lba + index), 0, result, index * 512, 512)
            Next
            Return result
        Catch
            Return Nothing
        End Try
    End Function

    Public Function BiosWrite(lba As Long, data As Byte()) As Boolean
        If _hardDisk Is Nothing OrElse data Is Nothing OrElse data.Length = 0 OrElse data.Length Mod 512 <> 0 Then Return False
        RaiseEvent Activity()
        Dim count As Integer = data.Length \ 512
        If lba < 0 OrElse lba + count > _hardDisk.SectorCount Then Return False
        Try
            For index As Integer = 0 To count - 1
                Dim sectorData(511) As Byte
                Array.Copy(data, index * 512, sectorData, 0, 512)
                _hardDisk.WriteSector(lba + index, sectorData)
            Next
            _hardDisk.Flush()
            Return True
        Catch
            Return False
        End Try
    End Function

    Private ReadOnly Property SlaveSelected As Boolean
        Get
            Return (_driveHead And &H10) <> 0
        End Get
    End Property

    Private ReadOnly Property SelectedDevice As IdeDeviceState
        Get
            Return If(SlaveSelected, _slave, _master)
        End Get
    End Property

    ' -------------------------------------------------------------------------
    ' I/O decode
    ' -------------------------------------------------------------------------
    Public Function HandlesPort(port As UInt16) As Boolean Implements IPortDevice.HandlesPort
        Return (port >= &H1F0 AndAlso port <= &H1F7) OrElse port = &H3F6
    End Function

    Public Function ReadPort(port As UInt16) As Byte Implements IPortDevice.ReadPort
        Dim state As IdeDeviceState = SelectedDevice

        Select Case port
            Case &H1F0
                ' Byte accesses to the data port are unusual on an ATAPI device.
                ' Preserve historical behavior by consuming one 16-bit word and
                ' returning its low byte. Normal ATA/ATAPI data transfers use INSW.
                Return CByte(ReadDataWord() And &HFFUS)
            Case &H1F1
                Return state.ErrorRegister
            Case &H1F2
                Return state.SectorCount
            Case &H1F3
                Return state.LbaLow
            Case &H1F4
                Return state.LbaMid
            Case &H1F5
                Return state.LbaHigh
            Case &H1F6
                Return _driveHead
            Case &H1F7
                ' Reading regular Status acknowledges the channel IRQ. Reading
                ' Alternate Status at 3F6h explicitly does NOT acknowledge it.
                _pic.ClearIrq(6)
                Return state.Status
            Case &H3F6
                Return state.Status
            Case Else
                Return &HFF
        End Select
    End Function

    Public Sub WritePort(port As UInt16, value As Byte) Implements IPortDevice.WritePort
        Dim state As IdeDeviceState = SelectedDevice

        Select Case port
            Case &H1F0
                WriteDataWord(value)
            Case &H1F1
                state.Features = value
            Case &H1F2
                state.SectorCount = value
            Case &H1F3
                state.LbaLow = value
            Case &H1F4
                state.LbaMid = value
            Case &H1F5
                state.LbaHigh = value
            Case &H1F6
                ' Device selection is a channel operation. No task-file values are
                ' copied here: selecting a device simply exposes THAT DEVICE'S task
                ' file. This is the key to preserving distinct ATA/ATAPI signatures.
                _driveHead = value
            Case &H1F7
                ExecuteCommand(value)
            Case &H3F6
                WriteDeviceControl(value)
        End Select
    End Sub

    Public Function ReadPortWord(port As UInt16) As UInt16 Implements IWordPortDevice.ReadPortWord
        If port = &H1F0 Then Return ReadDataWord()
        Return CUShort(ReadPort(port) Or (CUShort(ReadPort(CUShort(port + 1))) << 8))
    End Function

    Public Sub WritePortWord(port As UInt16, value As UInt16) Implements IWordPortDevice.WritePortWord
        If port = &H1F0 Then
            WriteDataWord(value)
        Else
            WritePort(port, CByte(value And &HFFUS))
            WritePort(CUShort(port + 1), CByte(value >> 8))
        End If
    End Sub

    ' -------------------------------------------------------------------------
    ' PIO data register
    ' -------------------------------------------------------------------------
    Private Function ReadDataWord() As UInt16
        Dim state As IdeDeviceState = SelectedDevice
        If state.Data Is Nothing OrElse state.DataIndex >= state.Data.Length Then Return &HFFFFUS

        Dim value As UInt16 = state.Data(state.DataIndex)
        If state.DataIndex + 1 < state.Data.Length Then
            value = CUShort(value Or (CUShort(state.Data(state.DataIndex + 1)) << 8))
        End If

        state.DataIndex += 2
        If state.DataIndex >= state.Data.Length Then CompleteDataPhase(state)
        Return value
    End Function

    Private Sub WriteDataWord(value As UInt16)
        Dim state As IdeDeviceState = SelectedDevice
        If state.Data Is Nothing OrElse state.DataIndex >= state.Data.Length Then Return

        state.Data(state.DataIndex) = CByte(value And &HFFUS)
        If state.DataIndex + 1 < state.Data.Length Then state.Data(state.DataIndex + 1) = CByte(value >> 8)
        state.DataIndex += 2

        If state.DataIndex < state.Data.Length Then Return

        Select Case state.Transfer
            Case TransferKind.AtapiPacketCommandOut
                ' Copy the completed CDB before ExecutePacket repurposes state.Data
                ' for a response phase.
                Dim packet(AtapiPacketBytes - 1) As Byte
                Array.Copy(state.Data, 0, packet, 0, packet.Length)
                state.Data = Nothing
                state.DataIndex = 0
                state.Transfer = TransferKind.None
                ExecutePacket(packet)

            Case TransferKind.AtaWrite
                CommitAtaWrite(state)
        End Select
    End Sub

    ' -------------------------------------------------------------------------
    ' ATA/ATAPI command register dispatch
    ' -------------------------------------------------------------------------
    Private Sub ExecuteCommand(command As Byte)
        Dim state As IdeDeviceState = SelectedDevice

        If (SlaveSelected AndAlso _cdrom IsNot Nothing) OrElse
           (Not SlaveSelected AndAlso _hardDisk IsNot Nothing) Then
            RaiseEvent Activity()
        End If

        ' A new command replaces the selected device's previous transient transfer
        ' state. The other device's task file and transfer bookkeeping are untouched.
        state.Status = StatusBusy
        state.ErrorRegister = 0
        ClearTransfer(state)

        If SlaveSelected Then
            ExecuteAtapiCommand(state, command)
        Else
            ExecuteAtaCommand(state, command)
        End If
    End Sub

    Private Sub ExecuteAtaCommand(state As IdeDeviceState, command As Byte)
        If _hardDisk Is Nothing Then
            FailCommand(state, &H4)
            Return
        End If

        Select Case command
            Case &H20, &H21                   ' READ SECTORS / READ SECTORS NO RETRY
                TraceAtaInBed("READ command count=" & EffectiveSectorCount(state).ToString())
                PrepareAtaRead(state)

            Case &H30, &H31                   ' WRITE SECTORS / WRITE SECTORS NO RETRY
                state.PendingLba = CurrentLba(state)
                state.PendingSectors = EffectiveSectorCount(state)
                TraceAtaInBed("WRITE command lba=" & state.PendingLba.ToString() &
                              " count=" & state.PendingSectors.ToString())
                If state.PendingLba < 0 OrElse state.PendingLba + state.PendingSectors > _hardDisk.SectorCount Then
                    FailCommand(state, &H10)
                    Return
                End If

                StartNextAtaWriteSector(state)

            Case &HE7                         ' FLUSH CACHE
                _hardDisk.Flush()
                FinishAtaCommand(state)

            Case &HEC                         ' IDENTIFY DEVICE
                PrepareIdentify(state, False)

            Case &H10 To &H1F                 ' RECALIBRATE family (legacy no-op)
                FinishAtaCommand(state)

            Case Else
                FailCommand(state, &H4)       ' ABRT
        End Select
    End Sub

    Private Sub ExecuteAtapiCommand(state As IdeDeviceState, command As Byte)
        TraceAtaInBed("ATAPI ATA command=" & command.ToString("X2") &
                      " features=" & state.Features.ToString("X2") &
                      " limit=" & state.LbaHigh.ToString("X2") & state.LbaMid.ToString("X2"))
        Select Case command
            Case &HA0                         ' PACKET
                BeginPacketCommand(state)

            Case &HA1                         ' IDENTIFY PACKET DEVICE
                PrepareIdentify(state, True)

            Case Else
                FailCommand(state, &H4)       ' ABRT unsupported ATA command
        End Select
    End Sub

    ' -------------------------------------------------------------------------
    ' ATA disk transfers
    ' -------------------------------------------------------------------------
    Private Sub PrepareAtaRead(state As IdeDeviceState)
        state.PendingLba = CurrentLba(state)
        state.PendingSectors = EffectiveSectorCount(state)

        If state.PendingLba < 0 OrElse state.PendingLba + state.PendingSectors > _hardDisk.SectorCount Then
            FailCommand(state, &H10)
            Return
        End If

        StartNextAtaReadSector(state)
    End Sub

    ' READ/WRITE SECTORS is a sequence of 512-byte PIO data phases.  DRQ does
    ' not describe one command-sized byte array: after every sector the device
    ' advances its task file, decrements Sector Count, and presents the next
    ' sector as a new DRQ/IRQ phase.  Keeping that electrical boundary matters
    ' to protected-mode disk code which deliberately waits between sectors.
    Private Sub StartNextAtaReadSector(state As IdeDeviceState)
        Dim block As Byte() = _hardDisk.ReadSector(state.PendingLba)
        ReDim state.Data(511)
        Array.Copy(block, 0, state.Data, 0, 512)
        state.DataIndex = 0
        state.Transfer = TransferKind.AtaRead
        state.Status = StatusDriveReady Or StatusDataRequest
        _ataReadSectorPhasesInBed += 1UL
        TraceAtaInBed("READ DRQ lba=" & state.PendingLba.ToString() &
                      " remaining=" & state.PendingSectors.ToString())
        RaiseInterrupt()
    End Sub

    Private Sub StartNextAtaWriteSector(state As IdeDeviceState)
        ReDim state.Data(511)
        state.DataIndex = 0
        state.Transfer = TransferKind.AtaWrite
        state.Status = StatusDriveReady Or StatusDataRequest
        _ataWriteSectorPhasesInBed += 1UL
        TraceAtaInBed("WRITE DRQ lba=" & state.PendingLba.ToString() &
                      " remaining=" & state.PendingSectors.ToString())
    End Sub

    Private Function CompleteAtaSector(state As IdeDeviceState) As Boolean
        state.PendingSectors -= 1
        state.SectorCount = CByte(state.PendingSectors And &HFF)
        state.PendingLba += 1
        SetTaskFileFromLba(state, state.PendingLba)
        Return state.PendingSectors > 0
    End Function

    Private Sub SetTaskFileFromLba(state As IdeDeviceState, lba As Long)
        If (_driveHead And &H40) <> 0 Then
            state.LbaLow = CByte(lba And &HFFL)
            state.LbaMid = CByte((lba >> 8) And &HFFL)
            state.LbaHigh = CByte((lba >> 16) And &HFFL)
            _driveHead = CByte((_driveHead And &HF0) Or CByte((lba >> 24) And &HFL))
            Return
        End If

        Dim identity As HardDiskIdentity = _hardDisk.Identity
        Dim sectorsPerCylinder As Long = CLng(identity.BiosHeads) * identity.BiosSectorsPerTrack
        Dim cylinder As Long = lba \ sectorsPerCylinder
        Dim withinCylinder As Long = lba Mod sectorsPerCylinder
        Dim head As Long = withinCylinder \ identity.BiosSectorsPerTrack
        Dim sector As Long = (withinCylinder Mod identity.BiosSectorsPerTrack) + 1
        state.LbaLow = CByte(sector)
        state.LbaMid = CByte(cylinder And &HFFL)
        state.LbaHigh = CByte((cylinder >> 8) And &HFFL)
        _driveHead = CByte((_driveHead And &HF0) Or CByte(head And &HFL))
    End Sub

    Private Sub CommitAtaWrite(state As IdeDeviceState)
        Try
            _hardDisk.WriteSector(state.PendingLba, state.Data)
            If CompleteAtaSector(state) Then
                StartNextAtaWriteSector(state)
                RaiseInterrupt()
            Else
                _hardDisk.Flush()
                FinishAtaCommand(state)
            End If
        Catch
            ClearTransfer(state)
            FailCommand(state, &H40)
        End Try
    End Sub

    Private Function CurrentLba(state As IdeDeviceState) As Long
        If (_driveHead And &H40) <> 0 Then
            Return CLng(state.LbaLow) Or
                   (CLng(state.LbaMid) << 8) Or
                   (CLng(state.LbaHigh) << 16) Or
                   (CLng(_driveHead And &HF) << 24)
        End If

        Dim sector As Integer = state.LbaLow And &H3F
        Dim head As Integer = _driveHead And &HF
        Dim cylinder As Integer = state.LbaMid Or (CInt(state.LbaHigh) << 8)

        If sector = 0 OrElse _hardDisk Is Nothing Then Return -1

        Dim identity As HardDiskIdentity = _hardDisk.Identity
        If head >= identity.BiosHeads OrElse
           sector > identity.BiosSectorsPerTrack OrElse
           cylinder >= identity.BiosCylinders Then Return -1

        Return (CLng(cylinder) * identity.BiosHeads + head) * identity.BiosSectorsPerTrack + sector - 1
    End Function

    Private Shared Function EffectiveSectorCount(state As IdeDeviceState) As Integer
        Return If(state.SectorCount = 0, 256, CInt(state.SectorCount))
    End Function

    ' -------------------------------------------------------------------------
    ' IDENTIFY DEVICE / IDENTIFY PACKET DEVICE
    ' -------------------------------------------------------------------------
    Private Sub PrepareIdentify(state As IdeDeviceState, atapi As Boolean)
        ReDim state.Data(511)

        If atapi Then
            ' 8580h:
            '   bit 15 = ATAPI device
            '   device type = 05h (CD-ROM)
            '   removable-media bit set
            '   12-byte command packet / conservative DRQ behavior
            PutWord(state.Data, 0, &H8580US)
            PutAtaString(state.Data, 10, 10, "VC-CDROM-0001")
            PutAtaString(state.Data, 23, 4, "1.00")
            PutAtaString(state.Data, 27, 20, "VIRTUAL COMPUTER ATAPI CD-ROM")

            ' Capability word: LBA supported. DMA capability is deliberately NOT
            ' advertised because this controller currently implements packet PIO.
            PutWord(state.Data, 49, &H200US)
            state.Transfer = TransferKind.AtapiIdentify
        Else
            PutWord(state.Data, 0, &H40US)

            Dim identity As HardDiskIdentity = _hardDisk.Identity
            Dim heads As Integer = identity.BiosHeads
            Dim sectorsPerTrack As Integer = identity.BiosSectorsPerTrack
            Dim cylinders As Integer = identity.BiosCylinders
            Dim lbaSectors As UInteger = CUInt(Math.Min(_hardDisk.SectorCount, CLng(UInteger.MaxValue)))
            Dim chsSectors As UInteger = CUInt(cylinders * heads * sectorsPerTrack)

            ' Legacy translated geometry used by the executable BIOS INT 13h layer.
            PutWord(state.Data, 1, CUShort(cylinders))
            PutWord(state.Data, 3, CUShort(heads))
            PutWord(state.Data, 6, CUShort(sectorsPerTrack))
            PutWord(state.Data, 47, CUShort(If(identity.MaximumMultipleSectors > 0, &H8000 Or identity.MaximumMultipleSectors, 0)))
            PutWord(state.Data, 49, CUShort(If(identity.SupportsLba28, &H200, 0)))
            PutWord(state.Data, 51, CUShort(Math.Min(identity.MaximumPioMode, 2) << 8))
            PutWord(state.Data, 53, 1US)
            PutWord(state.Data, 54, CUShort(cylinders))
            PutWord(state.Data, 55, CUShort(heads))
            PutWord(state.Data, 56, CUShort(sectorsPerTrack))
            PutWord(state.Data, 57, CUShort(chsSectors And &HFFFFUI))
            PutWord(state.Data, 58, CUShort(chsSectors >> 16))

            ' Native 28-bit LBA capacity remains available to software that asks.
            PutWord(state.Data, 60, CUShort(lbaSectors And &HFFFFUI))
            PutWord(state.Data, 61, CUShort(lbaSectors >> 16))
            If identity.MaximumPioMode >= 3 Then
                Dim advancedPioBits As Integer = (1 << Math.Min(2, identity.MaximumPioMode - 2)) - 1
                PutWord(state.Data, 64, CUShort(advancedPioBits))
            End If
            PutWord(state.Data, 80, CUShort(1 << Math.Min(14, Math.Max(1, identity.AtaMajorVersion))))
            PutAtaString(state.Data, 10, 10, identity.SerialNumber)
            PutAtaString(state.Data, 23, 4, identity.FirmwareRevision)
            PutAtaString(state.Data, 27, 20, identity.Model)
            state.Transfer = TransferKind.AtaIdentify
        End If

        state.DataIndex = 0
        state.Status = StatusDriveReady Or StatusDataRequest
        RaiseInterrupt()
    End Sub

    ' -------------------------------------------------------------------------
    ' ATAPI PACKET transport state machine
    ' -------------------------------------------------------------------------
    Private Sub BeginPacketCommand(state As IdeDeviceState)
        ' Features bit 0 requests packet DMA on ATA/ATAPI devices. This emulator
        ' intentionally advertises no DMA support; rejecting the request is safer
        ' and more authentic than silently treating a DMA transaction as PIO.
        If (state.Features And &H1) <> 0 Then
            FailCommand(state, &H4)
            Return
        End If

        ' Capture the host-supplied maximum byte count BEFORE the cylinder/byte
        ' count registers become output registers for individual packet phases.
        Dim requestedLimit As Integer = CInt(state.LbaMid) Or (CInt(state.LbaHigh) << 8)
        If requestedLimit = 0 Then requestedLimit = AtapiMaximumPioPhase

        ' ATAPI data register transfers are 16-bit. Keep intermediate phases even
        ' sized; an odd host limit is conservatively rounded down, never to zero.
        If requestedLimit > 1 AndAlso (requestedLimit And 1) <> 0 Then requestedLimit -= 1
        state.PacketByteLimit = Math.Max(2, Math.Min(AtapiMaximumPioPhase, requestedLimit))
        TraceAtaInBed("ATAPI PACKET command-out DRQ limit=" & state.PacketByteLimit.ToString())

        ReDim state.Data(AtapiPacketBytes - 1)
        state.DataIndex = 0
        state.Transfer = TransferKind.AtapiPacketCommandOut
        state.PacketResponse = Nothing
        state.PacketResponseOffset = 0

        ' Command-out phase: host writes the 12-byte CDB to 1F0h.
        state.SectorCount = AtapiReasonCommandOut
        state.Status = StatusDriveReady Or StatusDataRequest

        ' No IRQ is required to make progress here. OAKCDROM and many DOS drivers
        ' simply poll BSY/DRQ after writing A0h and then issue OUTSW.
    End Sub

    Private Sub ExecutePacket(packet As Byte())
        Dim state As IdeDeviceState = _slave
        Dim opcode As Byte = packet(0)
        _atapiPacketCommandsInBed += 1UL
        TraceAtaInBed("ATAPI CDB " & BitConverter.ToString(packet).Replace("-", " "))

        ' UNIT ATTENTION is reported on the next media-dependent/ordinary command.
        ' INQUIRY is allowed during discovery, and REQUEST SENSE must always be able
        ' to retrieve the pending condition. This prevents an endless sense loop.
        If state.UnitAttentionPending AndAlso opcode <> &H3 AndAlso opcode <> &H12 Then
            SetSense(SenseUnitAttention, state.AdditionalSenseCode, state.AdditionalSenseQualifier)
            FailPacket(state)
            Return
        End If

        Select Case opcode
            Case &H0                          ' TEST UNIT READY
                If Not RequireCdRom(state) Then Return
                FinishPacket(state, Array.Empty(Of Byte)())

            Case &H3                          ' REQUEST SENSE
                Dim allocationLength As Integer = packet(4)
                Dim response(17) As Byte
                response(0) = &H70             ' fixed-format current errors
                response(2) = state.SenseKey
                response(7) = 10
                response(12) = state.AdditionalSenseCode
                response(13) = state.AdditionalSenseQualifier

                Dim wasUnitAttention As Boolean = (state.SenseKey = SenseUnitAttention)
                ClearSense(state)
                If wasUnitAttention Then state.UnitAttentionPending = False
                FinishPacket(state, LimitResponse(response, allocationLength))

            Case &H12                         ' INQUIRY
                Dim allocationLength As Integer = packet(4)
                Dim response(35) As Byte
                response(0) = &H5              ' peripheral device type: CD/DVD
                response(1) = &H80             ' removable medium
                response(2) = &H0              ' ANSI version (legacy-friendly)
                response(3) = &H21             ' response-data format
                response(4) = 31               ' additional length
                PutAscii(response, 8, 8, "VIRTUAL")
                PutAscii(response, 16, 16, "ATAPI CD-ROM")
                PutAscii(response, 32, 4, "1.0")
                FinishPacket(state, LimitResponse(response, allocationLength))

            Case &H1A                         ' MODE SENSE(6)
                ExecuteModeSense6(state, packet)

            Case &H1B                         ' START STOP UNIT
                ExecuteStartStopUnit(state, packet)

            Case &H1E                         ' PREVENT/ALLOW MEDIUM REMOVAL
                state.MediumRemovalPrevented = (packet(4) And 1) <> 0
                FinishPacket(state, Array.Empty(Of Byte)())

            Case &H25                         ' READ CAPACITY(10)
                If Not RequireCdRom(state) Then Return
                Dim response(7) As Byte
                PutBigEndian32(response, 0, CUInt(_cdrom.SectorCount - 1))
                PutBigEndian32(response, 4, 2048UI)
                FinishPacket(state, response)

            Case &H28                         ' READ(10)
                ExecuteRead10(state, packet)

            Case &H2B                         ' SEEK(10)
                If Not RequireCdRom(state) Then Return
                Dim lba As UInteger = ReadBigEndian32(packet, 2)
                If CLng(lba) >= _cdrom.SectorCount Then
                    SetSense(SenseIllegalRequest, &H21)
                    FailPacket(state)
                    Return
                End If
                FinishPacket(state, Array.Empty(Of Byte)())

            Case &H42                         ' READ SUB-CHANNEL (minimal stopped state)
                ExecuteReadSubChannel(state, packet)

            Case &H43                         ' READ TOC/PMA/ATIP
                ExecuteReadToc(state, packet)

            Case &H5A                         ' MODE SENSE(10)
                ExecuteModeSense10(state, packet)

            Case &HA8                         ' READ(12)
                ExecuteRead12(state, packet)

            Case Else
                SetSense(SenseIllegalRequest, &H20) ' invalid command operation code
                FailPacket(state)
        End Select
    End Sub

    ' ----- Individual packet commands ----------------------------------------
    Private Sub ExecuteStartStopUnit(state As IdeDeviceState, packet As Byte())
        Dim loadEject As Boolean = (packet(4) And &H2) <> 0
        Dim start As Boolean = (packet(4) And &H1) <> 0

        If loadEject AndAlso Not start Then
            If state.MediumRemovalPrevented Then
                ' 53/02 = medium removal prevented.
                SetSense(SenseIllegalRequest, &H53, &H2)
                FailPacket(state)
                Return
            End If
            EjectCdRom()
        End If

        ' START without a host-side medium insertion mechanism is a successful
        ' spindle/no-op operation. Medium presence is tested by TUR/read commands.
        FinishPacket(state, Array.Empty(Of Byte)())
    End Sub

    Private Sub ExecuteRead10(state As IdeDeviceState, packet As Byte())
        If Not RequireCdRom(state) Then Return

        Dim lba As UInteger = ReadBigEndian32(packet, 2)
        Dim count As Long = (CLng(packet(7)) << 8) Or packet(8)
        PrepareCdRead(state, lba, count)
    End Sub

    Private Sub ExecuteRead12(state As IdeDeviceState, packet As Byte())
        If Not RequireCdRom(state) Then Return

        Dim lba As UInteger = ReadBigEndian32(packet, 2)
        Dim count As Long = CLng(ReadBigEndian32(packet, 6))
        PrepareCdRead(state, lba, count)
    End Sub

    Private Sub PrepareCdRead(state As IdeDeviceState, lba As UInteger, count As Long)
        TraceAtaInBed("ATAPI READ lba=" & lba.ToString() & " sectors=" & count.ToString())
        If count = 0 Then
            FinishPacket(state, Array.Empty(Of Byte)())
            Return
        End If

        If CLng(lba) < 0 OrElse CLng(lba) + count > _cdrom.SectorCount Then
            SetSense(SenseIllegalRequest, &H21) ' logical block address out of range
            FailPacket(state)
            Return
        End If

        Dim byteCount As Long = count * 2048L
        If byteCount > Integer.MaxValue Then
            SetSense(SenseIllegalRequest, &H24) ' invalid field in CDB / impractical request
            FailPacket(state)
            Return
        End If

        Dim response(CInt(byteCount) - 1) As Byte
        For index As Long = 0 To count - 1
            Array.Copy(_cdrom.ReadSector(CLng(lba) + index), 0, response, CInt(index * 2048L), 2048)
        Next
        _atapiReadSectorsInBed += CULng(count)
        FinishPacket(state, response)
    End Sub

    Private Sub ExecuteReadToc(state As IdeDeviceState, packet As Byte())
        If Not RequireCdRom(state) Then Return

        Dim msf As Boolean = (packet(1) And &H2) <> 0
        Dim format As Integer = packet(2) And &HF
        Dim startingTrack As Integer = packet(6)
        Dim allocationLength As Integer = (CInt(packet(7)) << 8) Or packet(8)

        ' OAKCDROM-era DOS software chiefly needs format 0: normal TOC.
        If format <> 0 Then
            SetSense(SenseIllegalRequest, &H24)
            FailPacket(state)
            Return
        End If

        Dim includeTrack As Boolean = (startingTrack = 0 OrElse startingTrack <= 1)
        Dim includeLeadOut As Boolean = (startingTrack = 0 OrElse startingTrack <= 1 OrElse startingTrack = &HAA)
        If Not includeTrack AndAlso Not includeLeadOut Then
            SetSense(SenseIllegalRequest, &H24)
            FailPacket(state)
            Return
        End If

        Dim descriptorCount As Integer = If(includeTrack, 1, 0) + If(includeLeadOut, 1, 0)
        Dim response(4 + descriptorCount * 8 - 1) As Byte

        response(2) = 1                        ' first track
        response(3) = 1                        ' last track

        Dim offset As Integer = 4
        If includeTrack Then
            PutTocDescriptor(response, offset, 1, 0, msf)
            offset += 8
        End If
        If includeLeadOut Then
            PutTocDescriptor(response, offset, &HAA, _cdrom.SectorCount, msf)
        End If

        ' Data length excludes the two-byte length field itself.
        Dim tocDataLength As Integer = response.Length - 2
        response(0) = CByte((tocDataLength >> 8) And &HFF)
        response(1) = CByte(tocDataLength And &HFF)

        FinishPacket(state, LimitResponse(response, allocationLength))
    End Sub

    Private Shared Sub PutTocDescriptor(buffer As Byte(), offset As Integer, trackNumber As Integer, lba As Long, msf As Boolean)
        buffer(offset) = 0
        buffer(offset + 1) = &H14              ' ADR=1, CONTROL=4 (data track)
        buffer(offset + 2) = CByte(trackNumber And &HFF)
        buffer(offset + 3) = 0

        If msf Then
            ' CD MSF addresses include the conventional 150-frame (2-second)
            ' lead-in offset. Descriptor address is 00:MM:SS:FF.
            Dim totalFrames As Long = lba + 150L
            Dim minutes As Long = totalFrames \ (60L * 75L)
            Dim seconds As Long = (totalFrames \ 75L) Mod 60L
            Dim frames As Long = totalFrames Mod 75L
            buffer(offset + 4) = 0
            buffer(offset + 5) = CByte(minutes And &HFF)
            buffer(offset + 6) = CByte(seconds And &HFF)
            buffer(offset + 7) = CByte(frames And &HFF)
        Else
            PutBigEndian32(buffer, offset + 4, CUInt(Math.Min(lba, CLng(UInteger.MaxValue))))
        End If
    End Sub

    Private Sub ExecuteModeSense6(state As IdeDeviceState, packet As Byte())
        Dim pageCode As Integer = packet(2) And &H3F
        Dim allocationLength As Integer = packet(4)
        Dim page As Byte() = BuildModePage(pageCode)
        If page Is Nothing Then
            SetSense(SenseIllegalRequest, &H24)
            FailPacket(state)
            Return
        End If

        Dim response(4 + page.Length - 1) As Byte
        response(0) = CByte(response.Length - 1) ' mode data length excludes itself
        response(1) = 0                         ' medium type
        response(2) = 0                         ' device-specific parameter
        response(3) = 0                         ' block descriptor length
        Array.Copy(page, 0, response, 4, page.Length)
        FinishPacket(state, LimitResponse(response, allocationLength))
    End Sub

    Private Sub ExecuteModeSense10(state As IdeDeviceState, packet As Byte())
        Dim pageCode As Integer = packet(2) And &H3F
        Dim allocationLength As Integer = (CInt(packet(7)) << 8) Or packet(8)
        Dim page As Byte() = BuildModePage(pageCode)
        If page Is Nothing Then
            SetSense(SenseIllegalRequest, &H24)
            FailPacket(state)
            Return
        End If

        Dim response(8 + page.Length - 1) As Byte
        Dim modeDataLength As Integer = response.Length - 2
        response(0) = CByte((modeDataLength >> 8) And &HFF)
        response(1) = CByte(modeDataLength And &HFF)
        response(2) = 0                        ' medium type
        response(3) = 0                        ' device-specific parameter
        response(6) = 0                        ' block descriptor length MSB
        response(7) = 0                        ' block descriptor length LSB
        Array.Copy(page, 0, response, 8, page.Length)
        FinishPacket(state, LimitResponse(response, allocationLength))
    End Sub

    Private Shared Function BuildModePage(pageCode As Integer) As Byte()
        ' Page 2Ah is the legacy CD-ROM capabilities/mechanical-status page.
        ' Page 3Fh means "return all supported pages"; with one implemented page,
        ' it is equivalent to returning 2Ah.
        If pageCode <> &H2A AndAlso pageCode <> &H3F Then Return Nothing

        Dim page(&H13) As Byte                 ' 2-byte header + 18-byte payload
        page(0) = &H2A
        page(1) = &H12

        ' Conservative read-only data CD-ROM capabilities. We deliberately do not
        ' advertise audio playback, writable media, multisession tricks, or DMA.
        ' Speeds are in kB/s; 176 approximates single-speed CD-ROM throughput.
        PutBigEndian16(page, 8, 176US)          ' maximum read speed
        PutBigEndian16(page, 14, 176US)         ' current read speed
        Return page
    End Function

    Private Sub ExecuteReadSubChannel(state As IdeDeviceState, packet As Byte())
        If Not RequireCdRom(state) Then Return

        Dim allocationLength As Integer = (CInt(packet(7)) << 8) Or packet(8)
        Dim subQ As Boolean = (packet(2) And &H40) <> 0
        Dim dataFormat As Integer = packet(3)

        If Not subQ Then
            ' Header-only response: no sub-Q data requested.
            Dim header(3) As Byte
            header(1) = &H15                   ' audio status: no current audio status
            FinishPacket(state, LimitResponse(header, allocationLength))
            Return
        End If

        If dataFormat <> 1 Then
            SetSense(SenseIllegalRequest, &H24)
            FailPacket(state)
            Return
        End If

        ' Current-position subchannel, stopped at track 1 LBA 0. This is enough for
        ' DOS discovery/control code that only wants a coherent non-playing state.
        Dim response(15) As Byte
        response(1) = &H15                     ' no current audio status
        response(2) = 0
        response(3) = 12                       ' subchannel payload length
        response(4) = 1                        ' data format: current position
        response(5) = &H14                     ' ADR/control: data track
        response(6) = 1                        ' track 1
        response(7) = 1                        ' index 1
        ' Absolute and relative addresses remain LBA 0.
        FinishPacket(state, LimitResponse(response, allocationLength))
    End Sub

    ' ----- Packet response phasing -------------------------------------------
    Private Sub FinishPacket(state As IdeDeviceState, response As Byte())
        If response Is Nothing Then response = Array.Empty(Of Byte)()

        state.PacketResponse = response
        state.PacketResponseOffset = 0
        state.Data = Nothing
        state.DataIndex = 0

        If response.Length = 0 Then
            CompletePacketCommand(state)
        Else
            StartNextPacketDataInPhase(state)
        End If
    End Sub

    Private Sub StartNextPacketDataInPhase(state As IdeDeviceState)
        Dim remaining As Integer = state.PacketResponse.Length - state.PacketResponseOffset
        If remaining <= 0 Then
            CompletePacketCommand(state)
            Return
        End If

        Dim phaseLength As Integer = Math.Min(remaining, state.PacketByteLimit)

        ' Keep all non-final phases word-aligned. The final phase may contain an
        ' odd byte; ReadDataWord supplies that byte in the low half of the word.
        If phaseLength < remaining AndAlso (phaseLength And 1) <> 0 Then phaseLength -= 1
        If phaseLength <= 0 Then phaseLength = Math.Min(remaining, 2)

        ReDim state.Data(phaseLength - 1)
        Array.Copy(state.PacketResponse, state.PacketResponseOffset, state.Data, 0, phaseLength)
        state.PacketResponseOffset += phaseLength
        state.DataIndex = 0
        state.Transfer = TransferKind.AtapiPacketDataIn

        ' Data-in phase. These registers describe THIS DRQ block only.
        state.LbaMid = CByte(phaseLength And &HFF)
        state.LbaHigh = CByte((phaseLength >> 8) And &HFF)
        state.SectorCount = AtapiReasonDataIn
        state.Status = StatusDriveReady Or StatusDataRequest
        _atapiDataPhasesInBed += 1UL
        TraceAtaInBed("ATAPI data-in DRQ bytes=" & phaseLength.ToString() &
                      " response-offset=" & state.PacketResponseOffset.ToString() & "/" &
                      state.PacketResponse.Length.ToString())
        RaiseInterrupt()
    End Sub

    Private Sub CompletePacketCommand(state As IdeDeviceState)
        state.Data = Nothing
        state.DataIndex = 0
        state.PacketResponse = Nothing
        state.PacketResponseOffset = 0
        state.Transfer = TransferKind.None

        ' Status phase. This is the critical 03h completion state: C/D=1, I/O=1,
        ' DRQ clear. It applies equally to a zero-data success (e.g. TUR) and to
        ' completion after the final data-in phase.
        state.SectorCount = AtapiReasonComplete
        state.LbaMid = 0
        state.LbaHigh = 0
        state.Status = StatusDriveReady
        TraceAtaInBed("ATAPI complete reason=03 ST=" & state.Status.ToString("X2"))
        RaiseInterrupt()
    End Sub

    Private Sub CompleteDataPhase(state As IdeDeviceState)
        Select Case state.Transfer
            Case TransferKind.AtapiPacketDataIn
                state.Data = Nothing
                state.DataIndex = 0
                If state.PacketResponse IsNot Nothing AndAlso state.PacketResponseOffset < state.PacketResponse.Length Then
                    StartNextPacketDataInPhase(state)
                Else
                    CompletePacketCommand(state)
                End If

            Case TransferKind.AtapiIdentify
                state.Data = Nothing
                state.DataIndex = 0
                state.Transfer = TransferKind.None
                state.SectorCount = AtapiReasonComplete
                state.Status = StatusDriveReady
                RaiseInterrupt()

            Case TransferKind.AtaRead, TransferKind.AtaIdentify
                If state.Transfer = TransferKind.AtaRead AndAlso CompleteAtaSector(state) Then
                    StartNextAtaReadSector(state)
                Else
                    FinishAtaCommand(state)
                End If

            Case Else
                state.Data = Nothing
                state.DataIndex = 0
                state.Transfer = TransferKind.None
                state.Status = StatusDriveReady
        End Select
    End Sub

    ' -------------------------------------------------------------------------
    ' Command completion / error / sense helpers
    ' -------------------------------------------------------------------------
    Private Sub FinishAtaCommand(state As IdeDeviceState)
        ClearTransfer(state)
        state.Status = StatusDriveReady
        RaiseInterrupt()
    End Sub

    Private Sub FailCommand(state As IdeDeviceState, errorCode As Byte)
        ClearTransfer(state)
        state.ErrorRegister = errorCode
        state.Status = StatusDriveReady Or StatusError
        RaiseInterrupt()
    End Sub

    Private Sub FailPacket(state As IdeDeviceState)
        state.Data = Nothing
        state.DataIndex = 0
        state.PacketResponse = Nothing
        state.PacketResponseOffset = 0
        state.Transfer = TransferKind.None

        ' ATAPI encodes the SCSI sense key in the high nibble of Error. The low
        ' nibble remains clear here; Status.ERR tells the host to issue REQUEST SENSE.
        state.ErrorRegister = CByte((state.SenseKey And &HF) << 4)
        state.SectorCount = AtapiReasonComplete
        state.LbaMid = 0
        state.LbaHigh = 0
        state.Status = StatusDriveReady Or StatusError
        TraceAtaInBed("ATAPI CHECK CONDITION sense=" & state.SenseKey.ToString("X2") & "/" &
                      state.AdditionalSenseCode.ToString("X2") & "/" &
                      state.AdditionalSenseQualifier.ToString("X2") &
                      " ERR=" & state.ErrorRegister.ToString("X2"))
        RaiseInterrupt()
    End Sub

    Private Function RequireCdRom(state As IdeDeviceState) As Boolean
        If _cdrom IsNot Nothing Then Return True
        SetSense(SenseNotReady, &H3A, &H0)      ' medium not present
        FailPacket(state)
        Return False
    End Function

    Private Sub SetSense(key As Byte, code As Byte, Optional qualifier As Byte = 0)
        _slave.SenseKey = key
        _slave.AdditionalSenseCode = code
        _slave.AdditionalSenseQualifier = qualifier
    End Sub

    Private Sub SetUnitAttention(code As Byte, qualifier As Byte)
        _slave.SenseKey = SenseUnitAttention
        _slave.AdditionalSenseCode = code
        _slave.AdditionalSenseQualifier = qualifier
        _slave.UnitAttentionPending = True
    End Sub

    Private Shared Sub ClearSense(state As IdeDeviceState)
        state.SenseKey = SenseNoSense
        state.AdditionalSenseCode = 0
        state.AdditionalSenseQualifier = 0
    End Sub

    Private Shared Sub ClearTransfer(state As IdeDeviceState)
        state.Data = Nothing
        state.DataIndex = 0
        state.Transfer = TransferKind.None
        state.PendingLba = 0
        state.PendingSectors = 0
        state.PacketResponse = Nothing
        state.PacketResponseOffset = 0
    End Sub

    Private Sub RaiseInterrupt()
        If (_deviceControl And DeviceControlDisableIrq) = 0 Then _pic.RaiseIrq(6)
    End Sub

    Private Sub TraceAtaInBed(messageInBed As String)
        _ataDiagnosticSequenceInBed += 1UL
        While _ataDiagnosticInBed.Count >= AtaDiagnosticCapacityInBed
            _ataDiagnosticInBed.Dequeue()
        End While
        _ataDiagnosticInBed.Enqueue("#A" & _ataDiagnosticSequenceInBed.ToString("000000000") &
                                    " " & messageInBed)
    End Sub

    Public Function DiagnosticText() As String
        Dim reportInBed As New StringBuilder()
        reportInBed.AppendLine("Primary ISA ATA/ATAPI channel 1F0h-1F7h, 3F6h, IRQ14")
        reportInBed.AppendLine("ATA sector DRQ phases R/W : " &
                               _ataReadSectorPhasesInBed.ToString("N0") & " / " &
                               _ataWriteSectorPhasesInBed.ToString("N0"))
        reportInBed.AppendLine("ATA task file SC/LBA      : " &
                               _master.SectorCount.ToString("X2") & " / " &
                               _master.LbaHigh.ToString("X2") & ":" &
                               _master.LbaMid.ToString("X2") & ":" &
                               _master.LbaLow.ToString("X2") &
                               " DH=" & _driveHead.ToString("X2") &
                               " ST=" & _master.Status.ToString("X2"))
        reportInBed.AppendLine("ATAPI packets/read sectors: " &
                               _atapiPacketCommandsInBed.ToString("N0") & " / " &
                               _atapiReadSectorsInBed.ToString("N0"))
        reportInBed.AppendLine("ATAPI data-in DRQ phases : " & _atapiDataPhasesInBed.ToString("N0"))
        reportInBed.AppendLine("ATAPI task file reason/BC : " &
                               _slave.SectorCount.ToString("X2") & " / " &
                               _slave.LbaHigh.ToString("X2") & _slave.LbaMid.ToString("X2") &
                               " ERR=" & _slave.ErrorRegister.ToString("X2") &
                               " ST=" & _slave.Status.ToString("X2"))
        reportInBed.AppendLine("ATAPI transfer/index      : " & _slave.Transfer.ToString() & " / " &
                               _slave.DataIndex.ToString() & "/" &
                               If(_slave.Data Is Nothing, "0", _slave.Data.Length.ToString()))
        reportInBed.AppendLine("ATAPI response offset     : " & _slave.PacketResponseOffset.ToString() & "/" &
                               If(_slave.PacketResponse Is Nothing, "0", _slave.PacketResponse.Length.ToString()) &
                               " limit=" & _slave.PacketByteLimit.ToString())
        reportInBed.AppendLine("ATAPI media/UA/prevent    : " &
                               If(_cdrom Is Nothing, "absent", "present") & " / " &
                               If(_slave.UnitAttentionPending, "pending", "clear") & " / " &
                               If(_slave.MediumRemovalPrevented, "yes", "no"))
        reportInBed.AppendLine("ATAPI sense key/ASC/ASCQ  : " &
                               _slave.SenseKey.ToString("X2") & "/" &
                               _slave.AdditionalSenseCode.ToString("X2") & "/" &
                               _slave.AdditionalSenseQualifier.ToString("X2"))
        reportInBed.AppendLine("Recent ATA/ATAPI phases (oldest first):")
        If _ataDiagnosticInBed.Count = 0 Then
            reportInBed.AppendLine("  <none>")
        Else
            For Each entryInBed As String In _ataDiagnosticInBed
                reportInBed.AppendLine("  " & entryInBed)
            Next
        End If
        Return reportInBed.ToString().TrimEnd()
    End Function

    ' -------------------------------------------------------------------------
    ' Software reset and signatures
    ' -------------------------------------------------------------------------
    Private Sub WriteDeviceControl(value As Byte)
        Dim oldReset As Boolean = (_deviceControl And DeviceControlSoftwareReset) <> 0
        Dim newReset As Boolean = (value And DeviceControlSoftwareReset) <> 0
        _deviceControl = value

        ' nIEN masks the physical interrupt output. If software masks IRQs while
        ' one is asserted, remove the channel request immediately; device command
        ' progress itself continues and can still be observed by polling status.
        If (_deviceControl And DeviceControlDisableIrq) <> 0 Then _pic.ClearIrq(6)

        If newReset Then
            AssertSoftwareReset()
        ElseIf oldReset Then
            ReleaseSoftwareReset()
        End If
    End Sub

    Private Sub AssertSoftwareReset()
        ClearTransfer(_master)
        ClearTransfer(_slave)
        _master.Status = StatusBusy
        _slave.Status = StatusBusy
        _pic.ClearIrq(6)
    End Sub

    Private Sub ReleaseSoftwareReset()
        ' BOTH devices receive reset signatures simultaneously. Device selection
        ' later decides which signature appears at 1F2-1F5; selection itself never
        ' fabricates or copies a signature.
        ResetAtaDeviceState(_master)
        ResetAtapiDeviceState(_slave, True)
        _pic.ClearIrq(6)
    End Sub

    Private Shared Sub ResetAtaDeviceState(state As IdeDeviceState)
        state.Features = 0
        state.ErrorRegister = 1
        state.SectorCount = 1
        state.LbaLow = 1
        state.LbaMid = 0
        state.LbaHigh = 0
        state.Status = StatusDriveReady
        ClearTransfer(state)
    End Sub

    Private Sub ResetAtapiDeviceState(state As IdeDeviceState, softwareReset As Boolean)
        state.Features = 0
        state.ErrorRegister = 1
        state.SectorCount = 1
        state.LbaLow = 1
        state.LbaMid = &H14
        state.LbaHigh = &HEB
        state.Status = StatusDriveReady
        state.PacketByteLimit = 2048
        state.MediumRemovalPrevented = False
        ClearTransfer(state)

        ' A packet device may report reset as UNIT ATTENTION. Preserve an already
        ' pending media-change event; otherwise SRST creates the standard reset UA.
        If softwareReset AndAlso Not state.UnitAttentionPending Then
            state.SenseKey = SenseUnitAttention
            state.AdditionalSenseCode = &H29    ' power on, reset, or bus-device reset
            state.AdditionalSenseQualifier = 0
            state.UnitAttentionPending = True
        ElseIf Not state.UnitAttentionPending Then
            ClearSense(state)
        End If
    End Sub

    ' -------------------------------------------------------------------------
    ' Byte/word formatting helpers
    ' -------------------------------------------------------------------------
    Private Shared Function LimitResponse(response As Byte(), allocationLength As Integer) As Byte()
        If response Is Nothing OrElse allocationLength <= 0 Then Return Array.Empty(Of Byte)()
        If allocationLength >= response.Length Then Return response

        Dim limited(allocationLength - 1) As Byte
        Array.Copy(response, 0, limited, 0, allocationLength)
        Return limited
    End Function

    Private Shared Sub PutWord(buffer As Byte(), index As Integer, value As UInt16)
        buffer(index * 2) = CByte(value And &HFFUS)
        buffer(index * 2 + 1) = CByte(value >> 8)
    End Sub

    Private Shared Sub PutAtaString(buffer As Byte(), wordIndex As Integer, wordCount As Integer, value As String)
        Dim padded As String = If(value, String.Empty).PadRight(wordCount * 2)
        If padded.Length > wordCount * 2 Then padded = padded.Substring(0, wordCount * 2)

        For i As Integer = 0 To wordCount - 1
            buffer((wordIndex + i) * 2) = CByte(AscW(padded(i * 2 + 1)))
            buffer((wordIndex + i) * 2 + 1) = CByte(AscW(padded(i * 2)))
        Next
    End Sub

    Private Shared Sub PutAscii(buffer As Byte(), offset As Integer, length As Integer, value As String)
        Dim padded As String = If(value, String.Empty).PadRight(length)
        If padded.Length > length Then padded = padded.Substring(0, length)
        Dim bytes As Byte() = Encoding.ASCII.GetBytes(padded)
        Array.Copy(bytes, 0, buffer, offset, length)
    End Sub

    Private Shared Sub PutBigEndian16(buffer As Byte(), offset As Integer, value As UInt16)
        buffer(offset) = CByte(value >> 8)
        buffer(offset + 1) = CByte(value And &HFFUS)
    End Sub

    Private Shared Sub PutBigEndian32(buffer As Byte(), offset As Integer, value As UInteger)
        buffer(offset) = CByte(value >> 24)
        buffer(offset + 1) = CByte((value >> 16) And &HFFUI)
        buffer(offset + 2) = CByte((value >> 8) And &HFFUI)
        buffer(offset + 3) = CByte(value And &HFFUI)
    End Sub

    Private Shared Function ReadBigEndian32(buffer As Byte(), offset As Integer) As UInteger
        Return (CUInt(buffer(offset)) << 24) Or
               (CUInt(buffer(offset + 1)) << 16) Or
               (CUInt(buffer(offset + 2)) << 8) Or
               CUInt(buffer(offset + 3))
    End Function

    Public Sub Dispose() Implements IDisposable.Dispose
        EjectHardDisk()
        EjectCdRom()
    End Sub
End Class
