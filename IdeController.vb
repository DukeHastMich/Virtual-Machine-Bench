Imports System
Imports System.Text

' =============================================================================
' IDE / ATA / ATAPI CHANNEL
' =============================================================================
' One instance represents one legacy IDE channel. The command block, control
' port and slave-PIC IRQ input are constructor straps so the same electrical
' implementation can be used for the standard primary and secondary channels.
' Each channel exposes two independent sockets. Either master or slave may be
' empty, an ATA hard disk, or an ATAPI CD-ROM according to chassis configuration.
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
Public Enum IdeDeviceKind As Byte
    Empty = 0
    AtaHardDisk = 1
    AtapiCdRom = 2
End Enum

Public Class IdeController
    Implements IPortDevice, IWordPortDevice, IResettableDevice, IDisposable

    ' ATA status register bits used by this implementation.
    Private Const StatusError As Byte = &H1
    Private Const StatusDataRequest As Byte = &H8
    Private Const StatusSeekComplete As Byte = &H10
    Private Const StatusDriveReady As Byte = &H40
    Private Const StatusBusy As Byte = &H80
    Private Const StatusReadyComplete As Byte = StatusDriveReady Or StatusSeekComplete

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

        ' SCSI sense state. UnitAttentionPending is distinct because the event is
        ' consumed when CHECK CONDITION reports it, while the sense tuple remains
        ' available for a later REQUEST SENSE.
        Public SenseKey As Byte
        Public AdditionalSenseCode As Byte
        Public AdditionalSenseQualifier As Byte
        Public UnitAttentionPending As Boolean

        ' PREVENT/ALLOW MEDIUM REMOVAL state (SCSI command 1Eh).
        Public MediumRemovalPrevented As Boolean
    End Class

    Private ReadOnly _pic As Pic8259
    Private ReadOnly _commandBase As UInt16
    Private ReadOnly _controlPort As UInt16
    Private ReadOnly _systemIrq As Integer
    Private ReadOnly _picIrqLine As Integer
    Private ReadOnly _channelName As String
    Private ReadOnly _master As New IdeDeviceState()
    Private ReadOnly _slave As New IdeDeviceState()

    ' Socket population is independent of master/slave position.  A standard
    ' IDE ribbon exposes two electrical positions; either may contain an ATA disk,
    ' an ATAPI packet device, or nothing.  Media remains a separate host attachment.
    Private _masterKind As IdeDeviceKind = IdeDeviceKind.AtaHardDisk
    Private _slaveKind As IdeDeviceKind = IdeDeviceKind.AtapiCdRom
    Private ReadOnly _hardDisks(1) As HardDiskImage
    Private ReadOnly _cdroms(1) As IsoImage

    ' Device/Head and Device Control are channel-visible registers.  The Device
    ' bit in _driveHead selects which per-device task file is exposed at 1F1-1F5
    ' and 1F7.
    Private _driveHead As Byte = &HA0
    Private _deviceControl As Byte

    ' Host-only flight recorder. Keep the high-frequency ATA hard-disk command/
    ' sector strings disabled by default: FAT metadata operations can hit this path
    ' hard enough that formatting/enqueuing trace text becomes guest-visible lag.
    ' Cheap numeric counters remain live, and ATAPI / low-frequency channel events
    ' are still retained for diagnosis. No guest I/O cycles are added by diagnostics.
    Private Const TraceAtaHardDiskHotPathInBed As Boolean = False
    Private Const AtaDiagnosticCapacityInBed As Integer = 128
    Private ReadOnly _ataDiagnosticInBed As New Collections.Generic.Queue(Of String)()
    Private _ataDiagnosticSequenceInBed As ULong
    Private _ataReadSectorPhasesInBed As ULong
    Private _ataWriteSectorPhasesInBed As ULong
    Private _atapiPacketCommandsInBed As ULong
    Private _atapiReadSectorsInBed As ULong
    Private _atapiDataPhasesInBed As ULong

    Public Event Activity(slaveInBed As Boolean, kindInBed As IdeDeviceKind)

    ' pic is the slave 8259 in an AT configuration. Standard PC/AT resources are
    ' primary 1F0h/3F6h/IRQ14 (slave input 6) and secondary 170h/376h/IRQ15
    ' (slave input 7). Alternate cards can still instantiate this channel with
    ' different straps later without changing its task-file behavior.
    Public Sub New(pic As Pic8259,
                   Optional commandBase As UInt16 = &H1F0US,
                   Optional controlPort As UInt16 = &H3F6US,
                   Optional systemIrq As Integer = 14,
                   Optional channelName As String = "Primary")
        If pic Is Nothing Then Throw New ArgumentNullException(NameOf(pic))
        If systemIrq < 8 OrElse systemIrq > 15 Then Throw New ArgumentOutOfRangeException(NameOf(systemIrq))
        If commandBase > &HFFF8US Then Throw New ArgumentOutOfRangeException(NameOf(commandBase))
        _pic = pic
        _commandBase = commandBase
        _controlPort = controlPort
        _systemIrq = systemIrq
        _picIrqLine = systemIrq - 8
        _channelName = If(String.IsNullOrWhiteSpace(channelName), "IDE", channelName.Trim())
        ResetDevice()
    End Sub

    Public ReadOnly Property CommandBase As UInt16
        Get
            Return _commandBase
        End Get
    End Property

    Public ReadOnly Property ControlPort As UInt16
        Get
            Return _controlPort
        End Get
    End Property

    Public ReadOnly Property SystemIrq As Integer
        Get
            Return _systemIrq
        End Get
    End Property

    Public ReadOnly Property ChannelName As String
        Get
            Return _channelName
        End Get
    End Property

    Public ReadOnly Property MasterInstalled As Boolean
        Get
            Return _masterKind <> IdeDeviceKind.Empty
        End Get
    End Property

    Public ReadOnly Property SlaveInstalled As Boolean
        Get
            Return _slaveKind <> IdeDeviceKind.Empty
        End Get
    End Property

    Public ReadOnly Property MasterDeviceKind As IdeDeviceKind
        Get
            Return _masterKind
        End Get
    End Property

    Public ReadOnly Property SlaveDeviceKind As IdeDeviceKind
        Get
            Return _slaveKind
        End Get
    End Property

    Public Function GetDeviceKind(slaveInBed As Boolean) As IdeDeviceKind
        Return If(slaveInBed, _slaveKind, _masterKind)
    End Function

    Public Sub ConfigureDeviceKinds(masterKindInBed As IdeDeviceKind,
                                    slaveKindInBed As IdeDeviceKind)
        ValidateDeviceKindInBed(masterKindInBed)
        ValidateDeviceKindInBed(slaveKindInBed)

        If _masterKind <> masterKindInBed Then DisposeMediaForSlotInBed(False)
        If _slaveKind <> slaveKindInBed Then DisposeMediaForSlotInBed(True)
        _masterKind = masterKindInBed
        _slaveKind = slaveKindInBed
        If RuntimeDiagnostics.Enabled Then
            TraceAtaInBed("POPULATION master=" & DeviceKindTextInBed(_masterKind) &
                          " slave=" & DeviceKindTextInBed(_slaveKind))
        End If
        ResetDevice()
    End Sub

    ' Compatibility wrapper retained for callers from the first dual-channel
    ' brick: an installed master meant ATA and an installed slave meant ATAPI.
    Public Sub ConfigureInstalledDevices(masterInstalled As Boolean, slaveInstalled As Boolean)
        ConfigureDeviceKinds(If(masterInstalled, IdeDeviceKind.AtaHardDisk, IdeDeviceKind.Empty),
                             If(slaveInstalled, IdeDeviceKind.AtapiCdRom, IdeDeviceKind.Empty))
    End Sub

    Private Shared Sub ValidateDeviceKindInBed(kindInBed As IdeDeviceKind)
        Select Case kindInBed
            Case IdeDeviceKind.Empty, IdeDeviceKind.AtaHardDisk, IdeDeviceKind.AtapiCdRom
                Return
            Case Else
                Throw New ArgumentOutOfRangeException(NameOf(kindInBed))
        End Select
    End Sub

    ' -------------------------------------------------------------------------
    ' Physical reset / media attachment
    ' -------------------------------------------------------------------------
    Public Sub ResetDevice() Implements IResettableDevice.ResetDevice
        ' Motherboard RESET resets both devices and shared channel controls, but
        ' does not detach host media from mechanisms that remain installed.
        _driveHead = &HA0
        _deviceControl = 0
        ResetDeviceStateForKindInBed(_master, _masterKind, False)
        ResetDeviceStateForKindInBed(_slave, _slaveKind, False)
        _pic.ClearIrq(_picIrqLine)
    End Sub

    Public Sub MountHardDisk(image As HardDiskImage, Optional slaveInBed As Boolean = False)
        If image Is Nothing Then Throw New ArgumentNullException(NameOf(image))
        If GetDeviceKind(slaveInBed) <> IdeDeviceKind.AtaHardDisk Then
            Throw New InvalidOperationException(SocketDescriptionInBed(slaveInBed) & " does not contain an ATA hard disk.")
        End If
        Dim slotInBed As Integer = SlotIndexInBed(slaveInBed)
        If _hardDisks(slotInBed) IsNot Nothing Then _hardDisks(slotInBed).Dispose()
        _hardDisks(slotInBed) = image
    End Sub

    Public Sub MountCdRom(image As IsoImage, Optional slaveInBed As Boolean = True)
        If image Is Nothing Then Throw New ArgumentNullException(NameOf(image))
        If GetDeviceKind(slaveInBed) <> IdeDeviceKind.AtapiCdRom Then
            Throw New InvalidOperationException(SocketDescriptionInBed(slaveInBed) & " does not contain an ATAPI CD-ROM.")
        End If
        Dim slotInBed As Integer = SlotIndexInBed(slaveInBed)
        If _cdroms(slotInBed) IsNot Nothing Then _cdroms(slotInBed).Dispose()
        _cdroms(slotInBed) = image
        Dim stateInBed As IdeDeviceState = StateForSlotInBed(slaveInBed)
        If RuntimeDiagnostics.Enabled Then
            TraceAtaInBed(SocketDescriptionInBed(slaveInBed) & " ATAPI media mounted sectors=" & image.SectorCount.ToString())
        End If
        SetUnitAttention(stateInBed, &H28, &H0)
    End Sub

    Public Sub EjectHardDisk(Optional slaveInBed As Boolean = False)
        Dim slotInBed As Integer = SlotIndexInBed(slaveInBed)
        If _hardDisks(slotInBed) IsNot Nothing Then _hardDisks(slotInBed).Dispose()
        _hardDisks(slotInBed) = Nothing
    End Sub

    Public Sub EjectCdRom(Optional slaveInBed As Boolean = True)
        Dim slotInBed As Integer = SlotIndexInBed(slaveInBed)
        If _cdroms(slotInBed) IsNot Nothing Then _cdroms(slotInBed).Dispose()
        _cdroms(slotInBed) = Nothing
        If GetDeviceKind(slaveInBed) = IdeDeviceKind.AtapiCdRom Then
            Dim stateInBed As IdeDeviceState = StateForSlotInBed(slaveInBed)
            If RuntimeDiagnostics.Enabled Then
                TraceAtaInBed(SocketDescriptionInBed(slaveInBed) & " ATAPI media ejected")
            End If
            SetUnitAttention(stateInBed, &H28, &H0)
        End If
    End Sub

    Public ReadOnly Property HardDiskSectorCount As Long
        Get
            Return GetHardDiskSectorCount(False)
        End Get
    End Property

    Public ReadOnly Property CdRomMounted As Boolean
        Get
            Return IsCdRomMounted(True)
        End Get
    End Property

    Public Function GetHardDiskSectorCount(slaveInBed As Boolean) As Long
        If GetDeviceKind(slaveInBed) <> IdeDeviceKind.AtaHardDisk Then Return 0
        Dim diskInBed As HardDiskImage = HardDiskForSlotInBed(slaveInBed)
        Return If(diskInBed Is Nothing, 0, diskInBed.SectorCount)
    End Function

    Public Function IsCdRomMounted(slaveInBed As Boolean) As Boolean
        Return GetDeviceKind(slaveInBed) = IdeDeviceKind.AtapiCdRom AndAlso CdRomForSlotInBed(slaveInBed) IsNot Nothing
    End Function

    Public Function GetHardDiskIdentity(slaveInBed As Boolean) As HardDiskIdentity
        Dim diskInBed As HardDiskImage = HardDiskForSlotInBed(slaveInBed)
        Return If(diskInBed Is Nothing, Nothing, diskInBed.Identity)
    End Function

    ' BIOS-facing helpers intentionally target device 0 on this channel.  The
    ' present BIOS boots the primary master; secondary/slave discovery is through
    ' the normal IDE task file until BIOS fixed-disk enumeration is expanded.
    Public Function BiosRead(lba As Long, count As Integer) As Byte()
        Dim diskInBed As HardDiskImage = _hardDisks(0)
        If _masterKind <> IdeDeviceKind.AtaHardDisk OrElse diskInBed Is Nothing OrElse count < 1 OrElse lba < 0 OrElse lba + count > diskInBed.SectorCount Then Return Nothing
        RaiseEvent Activity(False, IdeDeviceKind.AtaHardDisk)
        Try
            Dim result(count * 512 - 1) As Byte
            For index As Integer = 0 To count - 1
                Array.Copy(diskInBed.ReadSector(lba + index), 0, result, index * 512, 512)
            Next
            Return result
        Catch
            Return Nothing
        End Try
    End Function

    Public Function BiosWrite(lba As Long, data As Byte()) As Boolean
        Dim diskInBed As HardDiskImage = _hardDisks(0)
        If _masterKind <> IdeDeviceKind.AtaHardDisk OrElse diskInBed Is Nothing OrElse data Is Nothing OrElse data.Length = 0 OrElse data.Length Mod 512 <> 0 Then Return False
        RaiseEvent Activity(False, IdeDeviceKind.AtaHardDisk)
        Dim count As Integer = data.Length \ 512
        If lba < 0 OrElse lba + count > diskInBed.SectorCount Then Return False
        Try
            For index As Integer = 0 To count - 1
                Dim sectorData(511) As Byte
                Array.Copy(data, index * 512, sectorData, 0, 512)
                diskInBed.WriteSector(lba + index, sectorData)
            Next
            diskInBed.Flush()
            Return True
        Catch
            Return False
        End Try
    End Function

    Private Shared Function SlotIndexInBed(slaveInBed As Boolean) As Integer
        Return If(slaveInBed, 1, 0)
    End Function

    Private Function StateForSlotInBed(slaveInBed As Boolean) As IdeDeviceState
        Return If(slaveInBed, _slave, _master)
    End Function

    Private Function HardDiskForSlotInBed(slaveInBed As Boolean) As HardDiskImage
        Return _hardDisks(SlotIndexInBed(slaveInBed))
    End Function

    Private Function CdRomForSlotInBed(slaveInBed As Boolean) As IsoImage
        Return _cdroms(SlotIndexInBed(slaveInBed))
    End Function

    Private Function HardDiskForStateInBed(stateInBed As IdeDeviceState) As HardDiskImage
        Return If(Object.ReferenceEquals(stateInBed, _slave), _hardDisks(1), _hardDisks(0))
    End Function

    Private Function CdRomForStateInBed(stateInBed As IdeDeviceState) As IsoImage
        Return If(Object.ReferenceEquals(stateInBed, _slave), _cdroms(1), _cdroms(0))
    End Function

    Private Function DeviceKindForStateInBed(stateInBed As IdeDeviceState) As IdeDeviceKind
        Return If(Object.ReferenceEquals(stateInBed, _slave), _slaveKind, _masterKind)
    End Function

    Private Function SocketDescriptionInBed(slaveInBed As Boolean) As String
        Return _channelName & " " & If(slaveInBed, "slave", "master") & " socket"
    End Function

    Private Sub DisposeMediaForSlotInBed(slaveInBed As Boolean)
        Dim indexInBed As Integer = SlotIndexInBed(slaveInBed)
        If _hardDisks(indexInBed) IsNot Nothing Then _hardDisks(indexInBed).Dispose()
        If _cdroms(indexInBed) IsNot Nothing Then _cdroms(indexInBed).Dispose()
        _hardDisks(indexInBed) = Nothing
        _cdroms(indexInBed) = Nothing
    End Sub

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


    Private ReadOnly Property SelectedDeviceKind As IdeDeviceKind
        Get
            Return If(SlaveSelected, _slaveKind, _masterKind)
        End Get
    End Property

    Private ReadOnly Property SelectedDeviceInstalled As Boolean
        Get
            Return SelectedDeviceKind <> IdeDeviceKind.Empty
        End Get
    End Property

    ' -------------------------------------------------------------------------
    ' I/O decode
    ' -------------------------------------------------------------------------
    Public Function HandlesPort(port As UInt16) As Boolean Implements IPortDevice.HandlesPort
        Dim portValueInBed As Integer = CInt(port)
        Dim baseInBed As Integer = CInt(_commandBase)
        Return (portValueInBed >= baseInBed AndAlso portValueInBed <= baseInBed + 7) OrElse port = _controlPort
    End Function

    Public Function ReadPort(port As UInt16) As Byte Implements IPortDevice.ReadPort
        Dim offsetInBed As Integer = CInt(port) - CInt(_commandBase)
        Dim state As IdeDeviceState = SelectedDevice

        If port = _controlPort Then
            If Not SelectedDeviceInstalled Then Return 0
            Return state.Status
        End If
        If offsetInBed < 0 OrElse offsetInBed > 7 Then Return &HFF

        If Not SelectedDeviceInstalled Then
            If offsetInBed = 0 Then Return &HFF
            If offsetInBed = 6 Then Return _driveHead
            Return 0
        End If

        Select Case offsetInBed
            Case 0
                ' Byte accesses to the data port are unusual on an ATAPI device.
                ' Preserve historical behavior by consuming one 16-bit word and
                ' returning its low byte. Normal ATA/ATAPI data transfers use INSW.
                Return CByte(ReadDataWord() And &HFFUS)
            Case 1
                Return state.ErrorRegister
            Case 2
                Return state.SectorCount
            Case 3
                Return state.LbaLow
            Case 4
                Return state.LbaMid
            Case 5
                Return state.LbaHigh
            Case 6
                Return _driveHead
            Case 7
                ' Reading regular Status acknowledges the channel IRQ. Reading
                ' Alternate Status explicitly does NOT acknowledge it.
                _pic.ClearIrq(_picIrqLine)
                Return state.Status
            Case Else
                Return &HFF
        End Select
    End Function

    Public Sub WritePort(port As UInt16, value As Byte) Implements IPortDevice.WritePort
        Dim offsetInBed As Integer = CInt(port) - CInt(_commandBase)

        If port = _controlPort Then
            WriteDeviceControl(value)
            Return
        End If
        If offsetInBed < 0 OrElse offsetInBed > 7 Then Return

        ' Device/Head is electrically shared by the channel and must remain
        ' writable even when the newly selected socket is physically empty.
        If offsetInBed = 6 Then
            Dim oldSlaveInBed As Boolean = SlaveSelected
            _driveHead = value
            Dim newSlaveInBed As Boolean = SlaveSelected
            If oldSlaveInBed <> newSlaveInBed Then
                If RuntimeDiagnostics.Enabled Then
                    TraceAtaInBed("SELECT " & If(newSlaveInBed, "slave", "master") &
                                  " kind=" & DeviceKindTextInBed(SelectedDeviceKind))
                End If
            End If
            Return
        End If

        If Not SelectedDeviceInstalled Then Return
        Dim state As IdeDeviceState = SelectedDevice
        Select Case offsetInBed
            Case 0
                WriteDataWord(value)
            Case 1
                state.Features = value
            Case 2
                state.SectorCount = value
            Case 3
                state.LbaLow = value
            Case 4
                state.LbaMid = value
            Case 5
                state.LbaHigh = value
            Case 7
                ExecuteCommand(value)
        End Select
    End Sub

    Public Function ReadPortWord(port As UInt16) As UInt16 Implements IWordPortDevice.ReadPortWord
        If port = _commandBase Then Return ReadDataWord()
        Return CUShort(ReadPort(port) Or (CUShort(ReadPort(CUShort(port + 1))) << 8))
    End Function

    Public Sub WritePortWord(port As UInt16, value As UInt16) Implements IWordPortDevice.WritePortWord
        If port = _commandBase Then
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
                ExecutePacket(state, packet)

            Case TransferKind.AtaWrite
                CommitAtaWrite(state)
        End Select
    End Sub

    ' -------------------------------------------------------------------------
    ' ATA/ATAPI command register dispatch
    ' -------------------------------------------------------------------------
    Private Sub ExecuteCommand(command As Byte)
        Dim state As IdeDeviceState = SelectedDevice
        If Not SelectedDeviceInstalled Then Return

        Dim mediaAttachedInBed As Boolean =
            (SelectedDeviceKind = IdeDeviceKind.AtaHardDisk AndAlso HardDiskForStateInBed(state) IsNot Nothing) OrElse
            (SelectedDeviceKind = IdeDeviceKind.AtapiCdRom AndAlso CdRomForStateInBed(state) IsNot Nothing)
        If mediaAttachedInBed Then RaiseEvent Activity(SlaveSelected, SelectedDeviceKind)

        ' A new command replaces the selected device's previous transient transfer
        ' state. The other device's task file and transfer bookkeeping are untouched.
        state.Status = StatusBusy
        state.ErrorRegister = 0
        ClearTransfer(state)

        Select Case SelectedDeviceKind
            Case IdeDeviceKind.AtaHardDisk
                ExecuteAtaCommand(state, command)
            Case IdeDeviceKind.AtapiCdRom
                ExecuteAtapiCommand(state, command)
            Case Else
                ' Empty sockets never reach this branch because SelectedDeviceInstalled
                ' is checked above. Keep the electrically quiet behavior explicit.
                Return
        End Select
    End Sub

    Private Sub ExecuteAtaCommand(state As IdeDeviceState, command As Byte)
        Dim hardDiskInBed As HardDiskImage = HardDiskForStateInBed(state)
        If hardDiskInBed Is Nothing Then
            FailCommand(state, &H4)
            Return
        End If

        Select Case command
            Case &H20, &H21                   ' READ SECTORS / READ SECTORS NO RETRY
                If RuntimeDiagnostics.Enabled AndAlso TraceAtaHardDiskHotPathInBed Then
                    If RuntimeDiagnostics.Enabled Then
                        TraceAtaInBed("READ command count=" & EffectiveSectorCount(state).ToString())
                    End If
                End If
                PrepareAtaRead(state)

            Case &H30, &H31                   ' WRITE SECTORS / WRITE SECTORS NO RETRY
                state.PendingLba = CurrentLba(state)
                state.PendingSectors = EffectiveSectorCount(state)
                If RuntimeDiagnostics.Enabled AndAlso TraceAtaHardDiskHotPathInBed Then
                    If RuntimeDiagnostics.Enabled Then
                        TraceAtaInBed("WRITE command lba=" & state.PendingLba.ToString() &
                                      " count=" & state.PendingSectors.ToString())
                    End If
                End If
                If state.PendingLba < 0 OrElse state.PendingLba + state.PendingSectors > hardDiskInBed.SectorCount Then
                    FailCommand(state, &H10)
                    Return
                End If

                StartNextAtaWriteSector(state)

            Case &HE7                         ' FLUSH CACHE
                hardDiskInBed.Flush()
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
        If RuntimeDiagnostics.Enabled Then
            TraceAtaInBed("ATAPI ATA command=" & command.ToString("X2") &
                          " features=" & state.Features.ToString("X2") &
                          " limit=" & state.LbaHigh.ToString("X2") & state.LbaMid.ToString("X2"))
        End If
        Select Case command
            Case &HA0                         ' PACKET
                BeginPacketCommand(state)

            Case &HA1                         ' IDENTIFY PACKET DEVICE
                PrepareIdentify(state, True)

            Case &HEC, &H20, &H21            ' ATA IDENTIFY / READ probes against packet device
                ' Early ATAPI-aware DOS drivers commonly probe an IDE position with
                ' ATA IDENTIFY (or READ) and then look for the packet signature after
                ' the command aborts.  ATAPI requires these commands to return ABRT
                ' while restoring 01h/01h/14h/EBh in the task file.
                AbortAtaProbeAsAtapiInBed(state)

            Case Else
                FailCommand(state, &H4)       ' ABRT unsupported ATA command
        End Select
    End Sub

    Private Sub AbortAtaProbeAsAtapiInBed(state As IdeDeviceState)
        ClearTransfer(state)
        state.ErrorRegister = &H4             ' ABRT
        state.SectorCount = 1
        state.LbaLow = 1
        state.LbaMid = &H14
        state.LbaHigh = &HEB
        ' Preserve the legacy-ready status that the real DOS driver path already
        ' proved it expects.  The pre-refactor controller completed unsupported
        ' ATA probes with DRDY+DSC+ERR (51h) while leaving the ATAPI signature in
        ' 1F2h-1F5h.  VU-CCD/VIDE-CDD-style polling waits for the 50h ready/seek-
        ' complete baseline; returning only ERR (01h) makes the device look stuck.
        state.Status = StatusReadyComplete Or StatusError
        If RuntimeDiagnostics.Enabled Then
            TraceAtaInBed("ATAPI ATA probe aborted; signature=01/01/14/EB ST=" & state.Status.ToString("X2"))
        End If
        RaiseInterrupt()
    End Sub

    ' -------------------------------------------------------------------------
    ' ATA disk transfers
    ' -------------------------------------------------------------------------
    Private Sub PrepareAtaRead(state As IdeDeviceState)
        Dim hardDiskInBed As HardDiskImage = HardDiskForStateInBed(state)
        If hardDiskInBed Is Nothing Then
            FailCommand(state, &H4)
            Return
        End If
        state.PendingLba = CurrentLba(state)
        state.PendingSectors = EffectiveSectorCount(state)

        If state.PendingLba < 0 OrElse state.PendingLba + state.PendingSectors > hardDiskInBed.SectorCount Then
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
        Dim hardDiskInBed As HardDiskImage = HardDiskForStateInBed(state)
        If hardDiskInBed Is Nothing Then
            FailCommand(state, &H4)
            Return
        End If
        Dim block As Byte() = hardDiskInBed.ReadSector(state.PendingLba)
        ReDim state.Data(511)
        Array.Copy(block, 0, state.Data, 0, 512)
        state.DataIndex = 0
        state.Transfer = TransferKind.AtaRead
        state.Status = StatusReadyComplete Or StatusDataRequest
        If RuntimeDiagnostics.Enabled Then _ataReadSectorPhasesInBed += 1UL
        If RuntimeDiagnostics.Enabled AndAlso TraceAtaHardDiskHotPathInBed Then
            If RuntimeDiagnostics.Enabled Then
                TraceAtaInBed("READ DRQ lba=" & state.PendingLba.ToString() &
                              " remaining=" & state.PendingSectors.ToString())
            End If
        End If
        RaiseInterrupt()
    End Sub

    Private Sub StartNextAtaWriteSector(state As IdeDeviceState)
        ReDim state.Data(511)
        state.DataIndex = 0
        state.Transfer = TransferKind.AtaWrite
        state.Status = StatusReadyComplete Or StatusDataRequest
        If RuntimeDiagnostics.Enabled Then _ataWriteSectorPhasesInBed += 1UL
        If RuntimeDiagnostics.Enabled AndAlso TraceAtaHardDiskHotPathInBed Then
            If RuntimeDiagnostics.Enabled Then
                TraceAtaInBed("WRITE DRQ lba=" & state.PendingLba.ToString() &
                              " remaining=" & state.PendingSectors.ToString())
            End If
        End If
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

        Dim hardDiskInBed As HardDiskImage = HardDiskForStateInBed(state)
        If hardDiskInBed Is Nothing Then Return
        Dim identity As HardDiskIdentity = hardDiskInBed.Identity
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
        Dim hardDiskInBed As HardDiskImage = HardDiskForStateInBed(state)
        If hardDiskInBed Is Nothing Then
            FailCommand(state, &H4)
            Return
        End If
        Try
            hardDiskInBed.WriteSector(state.PendingLba, state.Data)
            If CompleteAtaSector(state) Then
                StartNextAtaWriteSector(state)
                RaiseInterrupt()
            Else
                hardDiskInBed.Flush()
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

        Dim hardDiskInBed As HardDiskImage = HardDiskForStateInBed(state)
        If sector = 0 OrElse hardDiskInBed Is Nothing Then Return -1

        Dim identity As HardDiskIdentity = hardDiskInBed.Identity
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

            Dim hardDiskInBed As HardDiskImage = HardDiskForStateInBed(state)
            If hardDiskInBed Is Nothing Then
                FailCommand(state, &H4)
                Return
            End If
            Dim identity As HardDiskIdentity = hardDiskInBed.Identity
            Dim heads As Integer = identity.BiosHeads
            Dim sectorsPerTrack As Integer = identity.BiosSectorsPerTrack
            Dim cylinders As Integer = identity.BiosCylinders
            Dim lbaSectors As UInteger = CUInt(Math.Min(hardDiskInBed.SectorCount, CLng(UInteger.MaxValue)))
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
        state.Status = StatusReadyComplete Or StatusDataRequest
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
        If RuntimeDiagnostics.Enabled Then
            TraceAtaInBed("ATAPI PACKET command-out DRQ limit=" & state.PacketByteLimit.ToString())
        End If

        ReDim state.Data(AtapiPacketBytes - 1)
        state.DataIndex = 0
        state.Transfer = TransferKind.AtapiPacketCommandOut
        state.PacketResponse = Nothing
        state.PacketResponseOffset = 0

        ' Command-out phase: host writes the 12-byte CDB to 1F0h.
        state.SectorCount = AtapiReasonCommandOut
        state.Status = StatusReadyComplete Or StatusDataRequest

        ' No IRQ is required to make progress here. OAKCDROM and many DOS drivers
        ' simply poll BSY/DRQ after writing A0h and then issue OUTSW.
    End Sub

    Private Sub ExecutePacket(state As IdeDeviceState, packet As Byte())
        Dim opcode As Byte = packet(0)
        If RuntimeDiagnostics.Enabled Then _atapiPacketCommandsInBed += 1UL
        If RuntimeDiagnostics.Enabled Then
            TraceAtaInBed("ATAPI CDB " & BitConverter.ToString(packet).Replace("-", " "))
        End If

        ' UNIT ATTENTION is reported on the next media-dependent/ordinary command.
        ' INQUIRY is allowed during discovery, and REQUEST SENSE must always be able
        ' to retrieve the pending condition. This prevents an endless sense loop.
        If state.UnitAttentionPending AndAlso opcode <> &H3 AndAlso opcode <> &H12 Then
            SetSense(state, SenseUnitAttention, state.AdditionalSenseCode, state.AdditionalSenseQualifier)
            state.UnitAttentionPending = False
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
                PutBigEndian32(response, 0, CUInt(CdRomForStateInBed(state).SectorCount - 1))
                PutBigEndian32(response, 4, 2048UI)
                FinishPacket(state, response)

            Case &H28                         ' READ(10)
                ExecuteRead10(state, packet)

            Case &H2B                         ' SEEK(10)
                If Not RequireCdRom(state) Then Return
                Dim lba As UInteger = ReadBigEndian32(packet, 2)
                If CLng(lba) >= CdRomForStateInBed(state).SectorCount Then
                    SetSense(state, SenseIllegalRequest, &H21)
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
                SetSense(state, SenseIllegalRequest, &H20) ' invalid command operation code
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
                SetSense(state, SenseIllegalRequest, &H53, &H2)
                FailPacket(state)
                Return
            End If
            EjectCdRom(Object.ReferenceEquals(state, _slave))
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
        If RuntimeDiagnostics.Enabled Then
            TraceAtaInBed("ATAPI READ lba=" & lba.ToString() & " sectors=" & count.ToString())
        End If
        If count = 0 Then
            FinishPacket(state, Array.Empty(Of Byte)())
            Return
        End If

        If CLng(lba) < 0 OrElse CLng(lba) + count > CdRomForStateInBed(state).SectorCount Then
            SetSense(state, SenseIllegalRequest, &H21) ' logical block address out of range
            FailPacket(state)
            Return
        End If

        Dim byteCount As Long = count * 2048L
        If byteCount > Integer.MaxValue Then
            SetSense(state, SenseIllegalRequest, &H24) ' invalid field in CDB / impractical request
            FailPacket(state)
            Return
        End If

        Dim response(CInt(byteCount) - 1) As Byte
        For index As Long = 0 To count - 1
            Array.Copy(CdRomForStateInBed(state).ReadSector(CLng(lba) + index), 0, response, CInt(index * 2048L), 2048)
        Next
        If RuntimeDiagnostics.Enabled Then _atapiReadSectorsInBed += CULng(count)
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
            SetSense(state, SenseIllegalRequest, &H24)
            FailPacket(state)
            Return
        End If

        Dim includeTrack As Boolean = (startingTrack = 0 OrElse startingTrack <= 1)
        Dim includeLeadOut As Boolean = (startingTrack = 0 OrElse startingTrack <= 1 OrElse startingTrack = &HAA)
        If Not includeTrack AndAlso Not includeLeadOut Then
            SetSense(state, SenseIllegalRequest, &H24)
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
            PutTocDescriptor(response, offset, &HAA, CdRomForStateInBed(state).SectorCount, msf)
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
            SetSense(state, SenseIllegalRequest, &H24)
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
            SetSense(state, SenseIllegalRequest, &H24)
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
            SetSense(state, SenseIllegalRequest, &H24)
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
        state.Status = StatusReadyComplete Or StatusDataRequest
        If RuntimeDiagnostics.Enabled Then _atapiDataPhasesInBed += 1UL
        If RuntimeDiagnostics.Enabled Then
            TraceAtaInBed("ATAPI data-in DRQ bytes=" & phaseLength.ToString() &
                          " response-offset=" & state.PacketResponseOffset.ToString() & "/" &
                          state.PacketResponse.Length.ToString())
        End If
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
        state.Status = StatusReadyComplete
        If RuntimeDiagnostics.Enabled Then
            TraceAtaInBed("ATAPI complete reason=03 ST=" & state.Status.ToString("X2"))
        End If
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
                state.Status = StatusReadyComplete
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
                state.Status = StatusReadyComplete
        End Select
    End Sub

    ' -------------------------------------------------------------------------
    ' Command completion / error / sense helpers
    ' -------------------------------------------------------------------------
    Private Sub FinishAtaCommand(state As IdeDeviceState)
        ClearTransfer(state)
        state.Status = StatusReadyComplete
        RaiseInterrupt()
    End Sub

    Private Sub FailCommand(state As IdeDeviceState, errorCode As Byte)
        ClearTransfer(state)
        state.ErrorRegister = errorCode
        state.Status = StatusReadyComplete Or StatusError
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
        state.Status = StatusReadyComplete Or StatusError
        If RuntimeDiagnostics.Enabled Then
            TraceAtaInBed("ATAPI CHECK CONDITION sense=" & state.SenseKey.ToString("X2") & "/" &
                          state.AdditionalSenseCode.ToString("X2") & "/" &
                          state.AdditionalSenseQualifier.ToString("X2") &
                          " ERR=" & state.ErrorRegister.ToString("X2"))
        End If
        RaiseInterrupt()
    End Sub

    Private Function RequireCdRom(state As IdeDeviceState) As Boolean
        If CdRomForStateInBed(state) IsNot Nothing Then Return True
        SetSense(state, SenseNotReady, &H3A, &H0)      ' medium not present
        FailPacket(state)
        Return False
    End Function

    Private Shared Sub SetSense(state As IdeDeviceState,
                                key As Byte,
                                code As Byte,
                                Optional qualifier As Byte = 0)
        state.SenseKey = key
        state.AdditionalSenseCode = code
        state.AdditionalSenseQualifier = qualifier
    End Sub

    Private Shared Sub SetUnitAttention(state As IdeDeviceState, code As Byte, qualifier As Byte)
        state.SenseKey = SenseUnitAttention
        state.AdditionalSenseCode = code
        state.AdditionalSenseQualifier = qualifier
        state.UnitAttentionPending = True
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
        If (_deviceControl And DeviceControlDisableIrq) = 0 Then _pic.RaiseIrq(_picIrqLine)
    End Sub

    Private Sub TraceAtaInBed(messageInBed As String)
        If Not RuntimeDiagnostics.Enabled Then Return
        _ataDiagnosticSequenceInBed += 1UL
        While _ataDiagnosticInBed.Count >= AtaDiagnosticCapacityInBed
            _ataDiagnosticInBed.Dequeue()
        End While
        _ataDiagnosticInBed.Enqueue("#A" & _ataDiagnosticSequenceInBed.ToString("000000000") &
                                    " " & messageInBed)
    End Sub

    Public Function DiagnosticText() As String
        Dim reportInBed As New StringBuilder()
        reportInBed.AppendLine(_channelName & " ISA ATA/ATAPI channel " &
                               _commandBase.ToString("X3") & "h-" & (CInt(_commandBase) + 7).ToString("X3") &
                               "h, " & _controlPort.ToString("X3") & "h, IRQ" & _systemIrq.ToString())
        reportInBed.AppendLine("Installed devices         : master=" & DeviceKindTextInBed(_masterKind) &
                               ", slave=" & DeviceKindTextInBed(_slaveKind))
        AppendSlotDiagnosticInBed(reportInBed, False, _master)
        AppendSlotDiagnosticInBed(reportInBed, True, _slave)
        reportInBed.AppendLine("ATA sector DRQ phases R/W : " &
                               _ataReadSectorPhasesInBed.ToString("N0") & " / " &
                               _ataWriteSectorPhasesInBed.ToString("N0"))
        reportInBed.AppendLine("ATAPI packets/read sectors: " &
                               _atapiPacketCommandsInBed.ToString("N0") & " / " &
                               _atapiReadSectorsInBed.ToString("N0"))
        reportInBed.AppendLine("ATAPI data-in DRQ phases  : " & _atapiDataPhasesInBed.ToString("N0"))
        reportInBed.AppendLine("ATA HDD hot-path text trace: " & If(TraceAtaHardDiskHotPathInBed, "enabled", "disabled"))
        reportInBed.AppendLine("Recent ATAPI / low-frequency ATA events (oldest first):")
        If _ataDiagnosticInBed.Count = 0 Then
            reportInBed.AppendLine("  <none>")
        Else
            For Each entryInBed As String In _ataDiagnosticInBed
                reportInBed.AppendLine("  " & entryInBed)
            Next
        End If
        Return reportInBed.ToString().TrimEnd()
    End Function

    Private Sub AppendSlotDiagnosticInBed(reportInBed As StringBuilder,
                                          slaveInBed As Boolean,
                                          stateInBed As IdeDeviceState)
        Dim labelInBed As String = If(slaveInBed, "slave", "master")
        Dim kindInBed As IdeDeviceKind = GetDeviceKind(slaveInBed)
        reportInBed.AppendLine(labelInBed.PadRight(7) & " task file SC/LBA      : " &
                               stateInBed.SectorCount.ToString("X2") & " / " &
                               stateInBed.LbaHigh.ToString("X2") & ":" &
                               stateInBed.LbaMid.ToString("X2") & ":" &
                               stateInBed.LbaLow.ToString("X2") &
                               " ST=" & stateInBed.Status.ToString("X2") &
                               " kind=" & DeviceKindTextInBed(kindInBed))
        Select Case kindInBed
            Case IdeDeviceKind.AtaHardDisk
                Dim diskInBed As HardDiskImage = HardDiskForSlotInBed(slaveInBed)
                reportInBed.AppendLine(labelInBed.PadRight(7) & " ATA media/model       : " &
                                       If(diskInBed Is Nothing,
                                          "no image attached",
                                          diskInBed.Identity.Model & " / " & diskInBed.SectorCount.ToString("N0") & " sectors"))
            Case IdeDeviceKind.AtapiCdRom
                Dim cdInBed As IsoImage = CdRomForSlotInBed(slaveInBed)
                reportInBed.AppendLine(labelInBed.PadRight(7) & " ATAPI media/UA/prevent : " &
                                       If(cdInBed Is Nothing, "tray empty", "media present") & " / " &
                                       If(stateInBed.UnitAttentionPending, "pending", "clear") & " / " &
                                       If(stateInBed.MediumRemovalPrevented, "yes", "no"))
                reportInBed.AppendLine(labelInBed.PadRight(7) & " ATAPI sense key/ASC/Q  : " &
                                       stateInBed.SenseKey.ToString("X2") & "/" &
                                       stateInBed.AdditionalSenseCode.ToString("X2") & "/" &
                                       stateInBed.AdditionalSenseQualifier.ToString("X2"))
        End Select
    End Sub

    Private Shared Function DeviceKindTextInBed(kindInBed As IdeDeviceKind) As String
        Select Case kindInBed
            Case IdeDeviceKind.AtaHardDisk : Return "ATA hard disk"
            Case IdeDeviceKind.AtapiCdRom : Return "ATAPI CD-ROM"
            Case Else : Return "empty"
        End Select
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
        If (_deviceControl And DeviceControlDisableIrq) <> 0 Then _pic.ClearIrq(_picIrqLine)

        If newReset Then
            If RuntimeDiagnostics.Enabled AndAlso Not oldReset Then TraceAtaInBed("SRST assert")
            AssertSoftwareReset()
        ElseIf oldReset Then
            If RuntimeDiagnostics.Enabled Then
                TraceAtaInBed("SRST release")
            End If
            ReleaseSoftwareReset()
        End If
    End Sub

    Private Sub AssertSoftwareReset()
        ClearTransfer(_master)
        ClearTransfer(_slave)
        _master.Status = If(_masterKind <> IdeDeviceKind.Empty, StatusBusy, CByte(0))
        _slave.Status = If(_slaveKind <> IdeDeviceKind.Empty, StatusBusy, CByte(0))
        _pic.ClearIrq(_picIrqLine)
    End Sub

    Private Sub ReleaseSoftwareReset()
        ' BOTH devices receive reset signatures simultaneously. Device selection
        ' later decides which signature appears at 1F2-1F5; selection itself never
        ' fabricates or copies a signature.
        ResetDeviceStateForKindInBed(_master, _masterKind, True)
        ResetDeviceStateForKindInBed(_slave, _slaveKind, True)
        _pic.ClearIrq(_picIrqLine)
    End Sub

    Private Sub ResetDeviceStateForKindInBed(stateInBed As IdeDeviceState,
                                                kindInBed As IdeDeviceKind,
                                                softwareResetInBed As Boolean)
        Select Case kindInBed
            Case IdeDeviceKind.AtaHardDisk
                ResetAtaDeviceState(stateInBed)
            Case IdeDeviceKind.AtapiCdRom
                ResetAtapiDeviceState(stateInBed, softwareResetInBed)
            Case Else
                ResetAbsentDeviceState(stateInBed)
        End Select
    End Sub

    Private Shared Sub ResetAbsentDeviceState(state As IdeDeviceState)
        state.Features = 0
        state.ErrorRegister = 0
        state.SectorCount = 0
        state.LbaLow = 0
        state.LbaMid = 0
        state.LbaHigh = 0
        state.Status = 0
        state.PacketByteLimit = 0
        state.MediumRemovalPrevented = False
        state.UnitAttentionPending = False
        ClearSense(state)
        ClearTransfer(state)
    End Sub

    Private Shared Sub ResetAtaDeviceState(state As IdeDeviceState)
        state.Features = 0
        state.ErrorRegister = 1
        state.SectorCount = 1
        state.LbaLow = 1
        state.LbaMid = 0
        state.LbaHigh = 0
        state.Status = StatusReadyComplete
        ClearTransfer(state)
    End Sub

    Private Sub ResetAtapiDeviceState(state As IdeDeviceState, softwareReset As Boolean)
        state.Features = 0
        state.ErrorRegister = 1
        state.SectorCount = 1
        state.LbaLow = 1
        state.LbaMid = &H14
        state.LbaHigh = &HEB

        ' Keep the driver-proven legacy ready/seek-complete baseline.  The old
        ' working ATAPI path exposed 50h here and VIDE-CDD/VU-CCD-class polling
        ' was observed waiting for that exact idle state.
        state.Status = StatusReadyComplete
        state.PacketByteLimit = 2048
        state.MediumRemovalPrevented = False
        ClearTransfer(state)

        ' Mount/eject owns real media-change UNIT ATTENTION (06/28/00).  Software
        ' reset is a transport reset and must not manufacture a new 29/00 event
        ' which can overwrite or prolong the one-shot media-change condition.
        If Not state.UnitAttentionPending Then ClearSense(state)

        If RuntimeDiagnostics.Enabled Then
            TraceAtaInBed("ATAPI " & If(softwareReset, "SRST", "RESET") &
                          " signature SC/LBA=01/01/14/EB ST=" &
                          state.Status.ToString("X2") &
                          " UA=" & If(state.UnitAttentionPending, "pending", "clear"))
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
        DisposeMediaForSlotInBed(False)
        DisposeMediaForSlotInBed(True)
    End Sub
End Class
