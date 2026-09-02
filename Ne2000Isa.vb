Imports System
Imports System.Collections.Generic
Imports System.IO
Imports System.Net
Imports System.Net.Sockets

' Novell NE2000-compatible 16-bit ISA Ethernet adapter.
'
' Guest-visible model:
'   * National Semiconductor DP8390-compatible register file at BASE+00h..0Fh
'   * Novell ASIC data port at BASE+10h and reset latch at BASE+1Fh
'   * 32-byte station PROM, including the classic doubled bytes seen by an
'     NE2000 in byte-transfer mode
'   * 16 KiB packet RAM at NIC addresses 4000h..7FFFh
'   * programmed-I/O remote DMA, transmit buffer, receive ring and IRQ logic
'
' The Ethernet transport is intentionally behind the card boundary.  Guest
' drivers only ever see NE2000 hardware.  A small UDP tunnel can be attached on
' the host side when an Ethernet peer is desired; no host networking API leaks
' into guest-visible registers.
Public NotInheritable Class Ne2000Isa
    Implements IPortDevice, IWordPortDevice, IPortDecodeCandidateProvider, IClockedDevice, IClockWakeSource, IResettableDevice, IDisposable

    Private Const NicRamStart As Integer = &H4000
    Private Const NicRamEndExclusive As Integer = &H8000
    Private Const PacketRamBytes As Integer = NicRamEndExclusive - NicRamStart
    Private Const HostPollPicoseconds As Long = 1000000000L ' 1 ms

    Private Const IsrPrx As Byte = &H1
    Private Const IsrPtx As Byte = &H2
    Private Const IsrRxe As Byte = &H4
    Private Const IsrTxe As Byte = &H8
    Private Const IsrOvw As Byte = &H10
    Private Const IsrCnt As Byte = &H20
    Private Const IsrRdc As Byte = &H40
    Private Const IsrRst As Byte = &H80

    Private _basePort As UInt16
    Private ReadOnly _masterPic As Pic8259
    Private ReadOnly _slavePic As Pic8259
    Private _irq As Integer
    Private ReadOnly _stationProm(31) As Byte
    Private ReadOnly _ram(PacketRamBytes - 1) As Byte
    Private ReadOnly _multicast(7) As Byte
    Private ReadOnly _physicalAddress(5) As Byte
    Private ReadOnly _sync As New Object()

    Private _command As Byte
    Private _pageStart As Byte
    Private _pageStop As Byte
    Private _boundary As Byte
    Private _transmitPage As Byte
    Private _transmitCount As UInt16
    Private _remoteAddress As UInt16
    Private _remoteCount As UInt16
    Private _receiveConfig As Byte
    Private _transmitConfig As Byte
    Private _dataConfig As Byte
    Private _interruptStatus As Byte
    Private _interruptMask As Byte
    Private _receiveStatus As Byte
    Private _transmitStatus As Byte
    Private _currentPage As Byte
    Private _remoteNextPacket As Byte
    Private _localNextPacket As Byte
    Private _addressCounter As UInt16
    Private _fifo As Byte
    Private _collisionCount As Byte
    Private _tally0 As Byte
    Private _tally1 As Byte
    Private _tally2 As Byte
    Private _irqAsserted As Boolean
    Private _pollAccumulator As Long
    Private _disposed As Boolean

    Private _udp As UdpClient
    Private _udpPeer As IPEndPoint
    Private _capturePath As String
    Private _captureStream As FileStream
    Private _framesTransmitted As ULong
    Private _framesReceived As ULong
    Private _framesDropped As ULong

    Public Sub New(basePort As UInt16,
                   irq As Integer,
                   masterPic As Pic8259,
                   slavePic As Pic8259,
                   Optional macAddress As Byte() = Nothing)
        If masterPic Is Nothing Then Throw New ArgumentNullException(NameOf(masterPic))
        If slavePic Is Nothing Then Throw New ArgumentNullException(NameOf(slavePic))
        If irq < 3 OrElse irq > 15 OrElse irq = 8 OrElse irq = 13 OrElse irq = 14 Then
            Throw New ArgumentOutOfRangeException(NameOf(irq), "Choose an ordinary ISA NIC IRQ.")
        End If
        _basePort = basePort
        _irq = irq
        _masterPic = masterPic
        _slavePic = slavePic

        Dim mac() As Byte = macAddress
        If mac Is Nothing Then mac = New Byte() {&H2, &H86, &H20, &H0, &H0, &H1}
        If mac.Length <> 6 Then Throw New ArgumentException("MAC address must contain six bytes.", NameOf(macAddress))
        Array.Copy(mac, _physicalAddress, 6)
        BuildStationProm(mac)
        ResetDevice()
    End Sub

    Public ReadOnly Property BasePort As UInt16
        Get
            Return _basePort
        End Get
    End Property

    Public ReadOnly Property Irq As Integer
        Get
            Return _irq
        End Get
    End Property

    ' Host-side jumpers. Call only with the virtual chassis stopped/off.
    Public Sub ConfigureHardware(basePort As UInt16, irq As Integer)
        If Array.IndexOf(IsaExpansionCardConfiguration.Ne2000BasePorts, basePort) < 0 Then Throw New ArgumentOutOfRangeException(NameOf(basePort))
        If Array.IndexOf(IsaExpansionCardConfiguration.Ne2000Irqs, irq) < 0 Then Throw New ArgumentOutOfRangeException(NameOf(irq))
        SyncLock _sync
            If _irqAsserted Then DriveIrq(False)
            _irqAsserted = False
            _basePort = basePort
            _irq = irq
            ResetDeviceLocked()
        End SyncLock
    End Sub

    Public ReadOnly Property MacAddressText As String
        Get
            Return String.Join(":", Array.ConvertAll(_physicalAddress, Function(b) b.ToString("X2")))
        End Get
    End Property

    Public ReadOnly Property UdpConnected As Boolean
        Get
            SyncLock _sync
                Return _udp IsNot Nothing AndAlso _udpPeer IsNot Nothing
            End SyncLock
        End Get
    End Property

    Public ReadOnly Property FramesTransmitted As ULong
        Get
            Return _framesTransmitted
        End Get
    End Property

    Public ReadOnly Property FramesReceived As ULong
        Get
            Return _framesReceived
        End Get
    End Property

    Public ReadOnly Property FramesDropped As ULong
        Get
            Return _framesDropped
        End Get
    End Property

    Private Sub BuildStationProm(mac As Byte())
        Array.Clear(_stationProm, 0, _stationProm.Length)
        ' A word-wide NE2000 exposes a 16-byte logical SAPROM as 32 bytes to an
        ' 8-bit probe: each logical byte appears twice.  Classic drivers collapse
        ' those pairs, then expect 57h in logical bytes 14 and 15.
        Dim logicalProm(15) As Byte
        For i As Integer = 0 To 5
            logicalProm(i) = mac(i)
        Next
        For i As Integer = 6 To 13
            logicalProm(i) = 0
        Next
        logicalProm(14) = &H57
        logicalProm(15) = &H57
        For i As Integer = 0 To 15
            _stationProm(i * 2) = logicalProm(i)
            _stationProm(i * 2 + 1) = logicalProm(i)
        Next
    End Sub

    Public Function HandlesPort(port As UInt16) As Boolean Implements IPortDevice.HandlesPort
        Dim offset As Integer = CInt(port) - CInt(_basePort)
        Return offset >= 0 AndAlso offset < &H20
    End Function

    Public Function PotentiallyHandlesPort(port As UInt16) As Boolean Implements IPortDecodeCandidateProvider.PotentiallyHandlesPort
        For Each baseInBed As UInt16 In IsaExpansionCardConfiguration.Ne2000BasePorts
            Dim offsetInBed As Integer = CInt(port) - CInt(baseInBed)
            If offsetInBed >= 0 AndAlso offsetInBed < &H20 Then Return True
        Next
        Return False
    End Function

    Public Function ReadPort(port As UInt16) As Byte Implements IPortDevice.ReadPort
        SyncLock _sync
            Dim offset As Integer = CInt(port) - CInt(_basePort)
            Select Case offset
                Case 0
                    Return _command
                Case 1 To &HF
                    Return ReadControllerRegister(offset)
                Case &H10
                    Return RemoteDmaReadByte()
                Case &H1F
                    ResetDeviceLocked()
                    Return 0
                Case Else
                    Return &HFF
            End Select
        End SyncLock
    End Function

    Public Sub WritePort(port As UInt16, value As Byte) Implements IPortDevice.WritePort
        SyncLock _sync
            Dim offset As Integer = CInt(port) - CInt(_basePort)
            Select Case offset
                Case 0
                    WriteCommand(value)
                Case 1 To &HF
                    WriteControllerRegister(offset, value)
                Case &H10
                    RemoteDmaWriteByte(value)
                Case &H1F
                    ' NE2000 reset is normally initiated by reading the reset
                    ' latch and completed by writing the returned value back.
                    _interruptStatus = CByte(_interruptStatus Or IsrRst)
                    UpdateIrq()
            End Select
        End SyncLock
    End Sub

    Public Function ReadPortWord(port As UInt16) As UInt16 Implements IWordPortDevice.ReadPortWord
        SyncLock _sync
            If port <> CUShort(CInt(_basePort) + &H10) Then
                Dim lo As Byte = ReadPort(port)
                Dim hi As Byte = ReadPort(CUShort((CInt(port) + 1) And &HFFFF))
                Return CUShort(CUInt(lo) Or (CUInt(hi) << 8))
            End If
            Dim loData As Byte = RemoteDmaReadByte()
            Dim hiData As Byte = RemoteDmaReadByte()
            Return CUShort(CUInt(loData) Or (CUInt(hiData) << 8))
        End SyncLock
    End Function

    Public Sub WritePortWord(port As UInt16, value As UInt16) Implements IWordPortDevice.WritePortWord
        SyncLock _sync
            If port <> CUShort(CInt(_basePort) + &H10) Then
                WritePort(port, CByte(value And &HFFUS))
                WritePort(CUShort((CInt(port) + 1) And &HFFFF), CByte(value >> 8))
                Return
            End If
            RemoteDmaWriteByte(CByte(value And &HFFUS))
            RemoteDmaWriteByte(CByte(value >> 8))
        End SyncLock
    End Sub

    Private Function SelectedPage() As Integer
        Return (_command >> 6) And 3
    End Function

    Private Function ReadControllerRegister(offset As Integer) As Byte
        Select Case SelectedPage()
            Case 0
                Select Case offset
                    Case 1 : Return CByte(_remoteAddress And &HFFUS) ' CLDA0 approximation
                    Case 2 : Return CByte(_remoteAddress >> 8)      ' CLDA1 approximation
                    Case 3 : Return _boundary
                    Case 4 : Return _transmitStatus
                    Case 5 : Return _collisionCount
                    Case 6 : Return _fifo
                    Case 7 : Return _interruptStatus
                    Case 8 : Return CByte(_remoteAddress And &HFFUS)
                    Case 9 : Return CByte(_remoteAddress >> 8)
                    Case &HA : Return &H50 ' RTL8019-compatible harmless signature on read-only aliases
                    Case &HB : Return &H70
                    Case &HC : Return _receiveStatus
                    Case &HD : Return _tally0
                    Case &HE : Return _tally1
                    Case &HF : Return _tally2
                End Select
            Case 1
                Select Case offset
                    Case 1 To 6 : Return _physicalAddress(offset - 1)
                    Case 7 : Return _currentPage
                    Case 8 To &HF : Return _multicast(offset - 8)
                End Select
            Case 2
                Select Case offset
                    Case 1 : Return _pageStart
                    Case 2 : Return _pageStop
                    Case 3 : Return _remoteNextPacket
                    Case 4 : Return _transmitPage
                    Case 5 : Return _localNextPacket
                    Case 6 : Return CByte(_addressCounter And &HFFUS)
                    Case 7 : Return CByte(_addressCounter >> 8)
                    Case &HC : Return _receiveConfig
                    Case &HD : Return _transmitConfig
                    Case &HE : Return _dataConfig
                    Case &HF : Return _interruptMask
                End Select
        End Select
        Return &HFF
    End Function

    Private Sub WriteControllerRegister(offset As Integer, value As Byte)
        Select Case SelectedPage()
            Case 0
                Select Case offset
                    Case 1 : _pageStart = value
                    Case 2 : _pageStop = value
                    Case 3 : _boundary = value
                    Case 4 : _transmitPage = value
                    Case 5 : _transmitCount = CUShort((_transmitCount And &HFF00US) Or value)
                    Case 6 : _transmitCount = CUShort((_transmitCount And &HFFUS) Or (CUShort(value) << 8))
                    Case 7
                        ' ISR is write-one-to-clear.
                        _interruptStatus = CByte(_interruptStatus And Not value)
                        UpdateIrq()
                    Case 8 : _remoteAddress = CUShort((_remoteAddress And &HFF00US) Or value)
                    Case 9 : _remoteAddress = CUShort((_remoteAddress And &HFFUS) Or (CUShort(value) << 8))
                    Case &HA : _remoteCount = CUShort((_remoteCount And &HFF00US) Or value)
                    Case &HB : _remoteCount = CUShort((_remoteCount And &HFFUS) Or (CUShort(value) << 8))
                    Case &HC : _receiveConfig = value
                    Case &HD : _transmitConfig = value
                    Case &HE : _dataConfig = value
                    Case &HF
                        _interruptMask = value
                        UpdateIrq()
                End Select
            Case 1
                Select Case offset
                    Case 1 To 6 : _physicalAddress(offset - 1) = value
                    Case 7 : _currentPage = value
                    Case 8 To &HF : _multicast(offset - 8) = value
                End Select
            Case 2
                ' Most page-2 registers are diagnostic mirrors.  A few clones
                ' allow writes; accepting the useful pointer values improves old
                ' diagnostics without inventing guest-visible functionality.
                Select Case offset
                    Case 3 : _remoteNextPacket = value
                    Case 5 : _localNextPacket = value
                    Case 6 : _addressCounter = CUShort((_addressCounter And &HFF00US) Or value)
                    Case 7 : _addressCounter = CUShort((_addressCounter And &HFFUS) Or (CUShort(value) << 8))
                End Select
        End Select
    End Sub

    Private Sub WriteCommand(value As Byte)
        _command = value
        Dim remoteCommand As Integer = (value >> 3) And 7

        ' Linux and several DOS probes deliberately request a zero-byte remote
        ' read to discover the IRQ.  The 8390 completes it immediately.
        If remoteCommand = 1 AndAlso _remoteCount = 0 Then
            _interruptStatus = CByte(_interruptStatus Or IsrRdc)
            UpdateIrq()
        End If

        If (value And &H4) <> 0 Then
            TransmitPacket()
            _command = CByte(_command And Not &H4)
        End If
    End Sub

    Private Function ReadNicMemory(address As UInt16) As Byte
        Dim a As Integer = CInt(address)
        If a >= 0 AndAlso a < _stationProm.Length Then Return _stationProm(a)
        If a >= NicRamStart AndAlso a < NicRamEndExclusive Then Return _ram(a - NicRamStart)
        Return &HFF
    End Function

    Private Sub WriteNicMemory(address As UInt16, value As Byte)
        Dim a As Integer = CInt(address)
        If a >= NicRamStart AndAlso a < NicRamEndExclusive Then _ram(a - NicRamStart) = value
    End Sub

    Private Function RemoteDmaReadByte() As Byte
        If _remoteCount = 0 Then Return &HFF
        Dim result As Byte = ReadNicMemory(_remoteAddress)
        AdvanceRemoteDmaOneByte()
        Return result
    End Function

    Private Sub RemoteDmaWriteByte(value As Byte)
        If _remoteCount = 0 Then Return
        WriteNicMemory(_remoteAddress, value)
        AdvanceRemoteDmaOneByte()
    End Sub

    Private Sub AdvanceRemoteDmaOneByte()
        _remoteAddress = CUShort((_remoteAddress + 1US) And &HFFFFUS)
        _addressCounter = _remoteAddress

        If _pageStart <> 0 AndAlso _pageStop > _pageStart Then
            Dim stopAddress As UInt16 = CUShort(CUInt(_pageStop) << 8)
            If _remoteAddress = stopAddress Then _remoteAddress = CUShort(CUInt(_pageStart) << 8)
        End If

        _remoteCount = CUShort(_remoteCount - 1US)
        If _remoteCount = 0 Then
            _interruptStatus = CByte(_interruptStatus Or IsrRdc)
            UpdateIrq()
        End If
    End Sub

    Private Sub TransmitPacket()
        Dim length As Integer = CInt(_transmitCount)
        Dim startAddress As Integer = CInt(_transmitPage) << 8
        If length <= 0 OrElse startAddress < NicRamStart OrElse startAddress + length > NicRamEndExclusive Then
            _transmitStatus = &H8 ' aborted transmit
            _interruptStatus = CByte(_interruptStatus Or IsrTxe)
            UpdateIrq()
            Return
        End If

        Dim frame(length - 1) As Byte
        Array.Copy(_ram, startAddress - NicRamStart, frame, 0, length)

        ' TCR loopback mode is useful during diagnostics and should return the
        ' frame through the actual receive PathWay instead of bypassing the ring.
        If (_transmitConfig And &H6) <> 0 Then
            ReceiveFrameLocked(frame)
        Else
            SendHostFrame(frame)
        End If

        _framesTransmitted += 1UL
        _transmitStatus = &H1 ' packet transmitted OK
        _interruptStatus = CByte(_interruptStatus Or IsrPtx)
        UpdateIrq()
    End Sub

    Private Sub SendHostFrame(frame As Byte())
        CaptureFrame(frame)
        If _udp Is Nothing OrElse _udpPeer Is Nothing Then Return
        Try
            _udp.Send(frame, frame.Length, _udpPeer)
        Catch
            _framesDropped += 1UL
        End Try
    End Sub

    Public Sub InjectFrame(frame As Byte())
        If frame Is Nothing OrElse frame.Length < 14 Then Return
        SyncLock _sync
            ReceiveFrameLocked(CType(frame.Clone(), Byte()))
        End SyncLock
    End Sub

    Private Sub ReceiveFrameLocked(frame As Byte())
        If (_command And &H1) <> 0 OrElse (_command And &H2) = 0 Then Return ' stopped
        If (_receiveConfig And &H20) <> 0 Then Return ' monitor mode
        If frame.Length < 14 Then Return
        If Not AcceptFrame(frame) Then Return
        If _pageStart = 0 OrElse _pageStop <= _pageStart Then Return
        If _currentPage < _pageStart OrElse _currentPage >= _pageStop Then _currentPage = _pageStart

        Dim payloadCount As Integer = frame.Length + 4 ' receive byte count includes CRC bytes
        Dim totalStored As Integer = 4 + payloadCount
        Dim pages As Integer = (totalStored + 255) \ 256
        If pages < 1 Then pages = 1

        Dim nextPage As Integer = _currentPage + pages
        While nextPage >= _pageStop
            nextPage = _pageStart + (nextPage - _pageStop)
        End While

        ' BNRY identifies the last page software has released.  Landing on it
        ' means the ring is full and the 8390 reports overwrite instead of
        ' silently corrupting unread packets.
        If nextPage = _boundary Then
            _interruptStatus = CByte(_interruptStatus Or IsrOvw)
            _receiveStatus = &H10
            _framesDropped += 1UL
            UpdateIrq()
            Return
        End If

        Dim writeAddress As Integer = CInt(_currentPage) << 8
        RingWriteByte(writeAddress, &H1) : writeAddress += 1 ' PRX
        RingWriteByte(writeAddress, CByte(nextPage)) : writeAddress += 1
        RingWriteByte(writeAddress, CByte(payloadCount And &HFF)) : writeAddress += 1
        RingWriteByte(writeAddress, CByte((payloadCount >> 8) And &HFF)) : writeAddress += 1
        For Each b As Byte In frame
            RingWriteByte(writeAddress, b)
            writeAddress += 1
        Next
        ' Ethernet FCS is not needed by packet drivers, but the 8390 count
        ' includes it.  Store four deterministic zero bytes in the RAM slots.
        For i As Integer = 0 To 3
            RingWriteByte(writeAddress, 0)
            writeAddress += 1
        Next

        _currentPage = CByte(nextPage)
        _receiveStatus = &H1
        _framesReceived += 1UL
        _interruptStatus = CByte(_interruptStatus Or IsrPrx)
        UpdateIrq()
        CaptureFrame(frame)
    End Sub

    Private Function AcceptFrame(frame As Byte()) As Boolean
        If (_receiveConfig And &H10) <> 0 Then Return True ' promiscuous

        Dim isBroadcast As Boolean = True
        For i As Integer = 0 To 5
            If frame(i) <> &HFF Then isBroadcast = False : Exit For
        Next
        If isBroadcast Then Return (_receiveConfig And &H4) <> 0

        If (frame(0) And 1) <> 0 Then
            Return (_receiveConfig And &H8) <> 0 ' multicast acceptance; hash filtering omitted
        End If

        For i As Integer = 0 To 5
            If frame(i) <> _physicalAddress(i) Then Return False
        Next
        Return True
    End Function

    Private Sub RingWriteByte(address As Integer, value As Byte)
        Dim startAddress As Integer = CInt(_pageStart) << 8
        Dim stopAddress As Integer = CInt(_pageStop) << 8
        If stopAddress <= startAddress Then Return
        While address >= stopAddress
            address = startAddress + (address - stopAddress)
        End While
        If address >= NicRamStart AndAlso address < NicRamEndExclusive Then
            _ram(address - NicRamStart) = value
        End If
    End Sub

    Private Sub UpdateIrq()
        Dim shouldAssert As Boolean = (_interruptStatus And _interruptMask And &H7F) <> 0
        If shouldAssert = _irqAsserted Then Return
        _irqAsserted = shouldAssert
        DriveIrq(shouldAssert)
    End Sub

    Private Sub DriveIrq(asserted As Boolean)
        If _irq <= 7 Then
            _masterPic.SetIrqLine(_irq, asserted)
        Else
            _slavePic.SetIrqLine(_irq - 8, asserted)
        End If
    End Sub

    Public Sub ResetDevice() Implements IResettableDevice.ResetDevice
        SyncLock _sync
            ResetDeviceLocked()
        End SyncLock
    End Sub

    Private Sub ResetDeviceLocked()
        If _irqAsserted Then DriveIrq(False)
        _irqAsserted = False
        _command = &H21 ' STOP + no-DMA
        _pageStart = &H40
        _pageStop = &H80
        _boundary = &H40
        _transmitPage = &H40
        _transmitCount = 0
        _remoteAddress = 0
        _remoteCount = 0
        _receiveConfig = &H20 ' monitor until driver initializes the ring
        _transmitConfig = &H2 ' internal loopback during initialization
        _dataConfig = &H49 ' word-wide, 8086 byte order
        _interruptStatus = IsrRst
        _interruptMask = 0
        _receiveStatus = 0
        _transmitStatus = 0
        _currentPage = &H46
        _remoteNextPacket = 0
        _localNextPacket = 0
        _addressCounter = 0
        _fifo = 0
        _collisionCount = 0
        _tally0 = 0 : _tally1 = 0 : _tally2 = 0
        _pollAccumulator = 0
        Array.Clear(_ram, 0, _ram.Length)
        Array.Clear(_multicast, 0, _multicast.Length)
        UpdateIrq()
    End Sub

    Public Sub ConfigureUdpTunnel(localPort As Integer, peerHost As String, peerPort As Integer)
        If localPort < 1 OrElse localPort > 65535 Then Throw New ArgumentOutOfRangeException(NameOf(localPort))
        If peerPort < 1 OrElse peerPort > 65535 Then Throw New ArgumentOutOfRangeException(NameOf(peerPort))
        If String.IsNullOrWhiteSpace(peerHost) Then Throw New ArgumentException("Peer host is required.", NameOf(peerHost))

        Dim addresses() As IPAddress = Dns.GetHostAddresses(peerHost)
        Dim address As IPAddress = Array.Find(addresses, Function(a) a.AddressFamily = AddressFamily.InterNetwork)
        If address Is Nothing Then Throw New InvalidOperationException("The NE2000 UDP tunnel currently requires an IPv4 peer.")

        SyncLock _sync
            CloseUdpLocked()
            _udp = New UdpClient(New IPEndPoint(IPAddress.Any, localPort))
            _udp.Client.Blocking = False
            _udpPeer = New IPEndPoint(address, peerPort)
        End SyncLock
    End Sub

    Public Sub DisconnectUdpTunnel()
        SyncLock _sync
            CloseUdpLocked()
        End SyncLock
    End Sub

    Private Sub CloseUdpLocked()
        If _udp IsNot Nothing Then
            Try : _udp.Close() : Catch : End Try
        End If
        _udp = Nothing
        _udpPeer = Nothing
    End Sub

    Public Sub SetPcapCapture(PathWay As String)
        SyncLock _sync
            CloseCaptureLocked()
            If String.IsNullOrWhiteSpace(PathWay) Then Return
            Directory.CreateDirectory(System.IO.Path.GetDirectoryName(System.IO.Path.GetFullPath(PathWay)))
            _captureStream = New FileStream(PathWay, FileMode.Create, FileAccess.Write, FileShare.Read)
            _capturePath = PathWay
            WritePcapGlobalHeader(_captureStream)
        End SyncLock
    End Sub

    Private Sub CaptureFrame(frame As Byte())
        If _captureStream Is Nothing Then Return
        Try
            Dim now As DateTimeOffset = DateTimeOffset.UtcNow
            Dim seconds As UInteger = CUInt(now.ToUnixTimeSeconds())
            Dim micros As UInteger = CUInt((now.Ticks Mod TimeSpan.TicksPerSecond) \ 10)
            WriteUInt32Little(_captureStream, seconds)
            WriteUInt32Little(_captureStream, micros)
            WriteUInt32Little(_captureStream, CUInt(frame.Length))
            WriteUInt32Little(_captureStream, CUInt(frame.Length))
            _captureStream.Write(frame, 0, frame.Length)
            _captureStream.Flush()
        Catch
        End Try
    End Sub

    Private Shared Sub WritePcapGlobalHeader(stream As Stream)
        WriteUInt32Little(stream, &HA1B2C3D4UI)
        WriteUInt16Little(stream, 2US)
        WriteUInt16Little(stream, 4US)
        WriteUInt32Little(stream, 0UI)
        WriteUInt32Little(stream, 0UI)
        WriteUInt32Little(stream, 65535UI)
        WriteUInt32Little(stream, 1UI) ' Ethernet
    End Sub

    Private Shared Sub WriteUInt16Little(stream As Stream, value As UShort)
        stream.WriteByte(CByte(value And &HFFUS))
        stream.WriteByte(CByte(value >> 8))
    End Sub

    Private Shared Sub WriteUInt32Little(stream As Stream, value As UInteger)
        stream.WriteByte(CByte(value And &HFFUI))
        stream.WriteByte(CByte((value >> 8) And &HFFUI))
        stream.WriteByte(CByte((value >> 16) And &HFFUI))
        stream.WriteByte(CByte((value >> 24) And &HFFUI))
    End Sub

    Private Sub CloseCaptureLocked()
        If _captureStream IsNot Nothing Then
            Try : _captureStream.Dispose() : Catch : End Try
        End If
        _captureStream = Nothing
        _capturePath = Nothing
    End Sub

    Public Sub AdvanceTime(elapsedPicoseconds As Long) Implements IClockedDevice.AdvanceTime
        If elapsedPicoseconds < 0 Then Throw New ArgumentOutOfRangeException(NameOf(elapsedPicoseconds))
        If elapsedPicoseconds = 0 OrElse _disposed Then Return

        SyncLock _sync
            If _udp Is Nothing Then Return
            _pollAccumulator += elapsedPicoseconds
            If _pollAccumulator < HostPollPicoseconds Then Return
            _pollAccumulator = _pollAccumulator Mod HostPollPicoseconds

            For packetIndex As Integer = 0 To 31
                Try
                    If _udp.Available <= 0 Then Exit For
                    Dim source As IPEndPoint = Nothing
                    Dim frame() As Byte = _udp.Receive(source)
                    ReceiveFrameLocked(frame)
                Catch ex As SocketException
                    If ex.SocketErrorCode = SocketError.WouldBlock Then Exit For
                    _framesDropped += 1UL
                    Exit For
                Catch
                    _framesDropped += 1UL
                    Exit For
                End Try
            Next
        End SyncLock
    End Sub

    Public Function PicosecondsUntilNextWakeEvent() As Long Implements IClockWakeSource.PicosecondsUntilNextWakeEvent
        SyncLock _sync
            If _udp Is Nothing Then Return Long.MaxValue
            Dim remaining As Long = HostPollPicoseconds - _pollAccumulator
            If remaining <= 0 Then Return 1
            Return remaining
        End SyncLock
    End Function

    Public Function DiagnosticText() As String
        SyncLock _sync
            Return "Novell NE2000 / DP8390" & Environment.NewLine &
                   "  I/O / IRQ             : " & _basePort.ToString("X3") & "h / " & _irq.ToString() & Environment.NewLine &
                   "  MAC                   : " & MacAddressText & Environment.NewLine &
                   "  CR / ISR / IMR        : " & _command.ToString("X2") & " / " & _interruptStatus.ToString("X2") & " / " & _interruptMask.ToString("X2") & Environment.NewLine &
                   "  PSTART/PSTOP/BNRY/CURR: " & _pageStart.ToString("X2") & "/" & _pageStop.ToString("X2") & "/" & _boundary.ToString("X2") & "/" & _currentPage.ToString("X2") & Environment.NewLine &
                   "  remote addr/count     : " & _remoteAddress.ToString("X4") & " / " & _remoteCount.ToString() & Environment.NewLine &
                   "  TX / RX / dropped     : " & _framesTransmitted.ToString("N0") & " / " & _framesReceived.ToString("N0") & " / " & _framesDropped.ToString("N0") & Environment.NewLine &
                   "  host cable            : " & If(_udp Is Nothing, "disconnected", "UDP tunnel connected")
        End SyncLock
    End Function

    Public Sub Dispose() Implements IDisposable.Dispose
        If _disposed Then Return
        _disposed = True
        SyncLock _sync
            If _irqAsserted Then DriveIrq(False)
            _irqAsserted = False
            CloseUdpLocked()
            CloseCaptureLocked()
        End SyncLock
        GC.SuppressFinalize(Me)
    End Sub
End Class
