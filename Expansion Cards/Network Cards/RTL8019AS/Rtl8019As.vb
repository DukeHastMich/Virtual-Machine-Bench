Imports System

' RTL8019AS computer-visible controller core.
' This is an IC boundary, not an ISA-card wrapper: board decode, IRQ routing,
' boot-ROM wiring, EEPROM wiring and host Ethernet transport remain external.
Public NotInheritable Class Rtl8019As
    Private Const NicRamStart As Integer = &H4000
    Private Const NicRamEndExclusive As Integer = &H8000
    Private Const PacketRamBytes As Integer = NicRamEndExclusive - NicRamStart
    Private Const IsrPtx As Byte = &H2
    Private Const IsrTxe As Byte = &H8
    Private Const IsrRdc As Byte = &H40
    Private Const IsrRst As Byte = &H80

    Private ReadOnly _stationProm(31) As Byte
    Private ReadOnly _ram(PacketRamBytes - 1) As Byte
    Private ReadOnly _multicast(7) As Byte
    Private ReadOnly _physicalAddress(5) As Byte
    Private _command As Byte, _pageStart As Byte, _pageStop As Byte, _boundary As Byte
    Private _transmitPage As Byte, _receiveConfig As Byte, _transmitConfig As Byte, _dataConfig As Byte
    Private _interruptStatus As Byte, _interruptMask As Byte, _receiveStatus As Byte, _transmitStatus As Byte
    Private _currentPage As Byte, _remoteNextPacket As Byte, _localNextPacket As Byte
    Private _fifo As Byte, _collisionCount As Byte, _tally0 As Byte, _tally1 As Byte, _tally2 As Byte
    Private _transmitCount As UInt16, _remoteAddress As UInt16, _remoteCount As UInt16, _addressCounter As UInt16
    ' RTL8019AS page-3 9346 interface state.  The controller drives CS/SK/DI;
    ' the external EEPROM drives DO.  This is intentionally an IC pin seam.
    Private _eepromMode As Byte
    Private _eepromChipSelect As Boolean, _eepromClock As Boolean, _eepromDataIn As Boolean, _eepromDataOut As Boolean

    Public Event InterruptChanged(asserted As Boolean)
    Public Event TransmitRequested(frame As Byte())
    Public Event MicrowirePinsChanged(chipSelect As Boolean, clock As Boolean, dataIn As Boolean)

    Public Sub New(Optional stationAddress As Byte() = Nothing)
        Dim mac=If(stationAddress, New Byte(){&H2,&H86,&H2,&H86,&H2,&H86})
        If mac.Length<>6 Then Throw New ArgumentException("RTL8019AS station address must contain six bytes.",NameOf(stationAddress))
        BuildStationProm(mac)
        Reset()
    End Sub

    Public ReadOnly Property EepromChipSelect As Boolean
        Get : Return _eepromChipSelect : End Get
    End Property
    Public ReadOnly Property EepromClock As Boolean
        Get : Return _eepromClock : End Get
    End Property
    Public ReadOnly Property EepromDataIn As Boolean
        Get : Return _eepromDataIn : End Get
    End Property
    Public Sub SetEepromDataOut(value As Boolean)
        _eepromDataOut=value
    End Sub

    Public ReadOnly Property InterruptAsserted As Boolean
        Get : Return (_interruptStatus And _interruptMask And &H7F)<>0 : End Get
    End Property

    Public Sub Reset()
        _command=&H21 : _pageStart=&H40 : _pageStop=&H80 : _boundary=&H40
        _transmitPage=&H40 : _transmitCount=0 : _remoteAddress=0 : _remoteCount=0
        _receiveConfig=&H20 : _transmitConfig=&H2 : _dataConfig=&H49
        _interruptStatus=IsrRst : _interruptMask=0 : _receiveStatus=0 : _transmitStatus=0
        _currentPage=&H46 : _remoteNextPacket=0 : _localNextPacket=0 : _addressCounter=0
        _fifo=0 : _collisionCount=0 : _tally0=0 : _tally1=0 : _tally2=0
        _eepromMode=0 : _eepromChipSelect=False : _eepromClock=False : _eepromDataIn=False : _eepromDataOut=False
        Array.Clear(_ram,0,_ram.Length) : Array.Clear(_multicast,0,_multicast.Length)
        RaiseEvent InterruptChanged(False)
    End Sub

    Public Function ReadRegister(offset As Integer) As Byte
        If offset<0 OrElse offset>&HF Then Throw New ArgumentOutOfRangeException(NameOf(offset))
        If offset=0 Then Return _command
        Select Case SelectedPage()
            Case 0
                Select Case offset
                    Case 1 : Return CByte(_remoteAddress And &HFFUS)
                    Case 2 : Return CByte(_remoteAddress >> 8)
                    Case 3 : Return _boundary
                    Case 4 : Return _transmitStatus
                    Case 5 : Return _collisionCount
                    Case 6 : Return _fifo
                    Case 7 : Return _interruptStatus
                    Case 8 : Return CByte(_remoteAddress And &HFFUS)
                    Case 9 : Return CByte(_remoteAddress >> 8)
                    Case &HA : Return &H50
                    Case &HB : Return &H70
                    Case &HC : Return _receiveStatus
                    Case &HD : Return _tally0
                    Case &HE : Return _tally1
                    Case &HF : Return _tally2
                End Select
            Case 1
                If offset>=1 AndAlso offset<=6 Then Return _physicalAddress(offset-1)
                If offset=7 Then Return _currentPage
                If offset>=8 Then Return _multicast(offset-8)
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
            Case 3
                If offset=1 Then Return Read9346Control()
        End Select
        Return &HFF
    End Function

    Public Sub WriteRegister(offset As Integer,value As Byte)
        If offset<0 OrElse offset>&HF Then Throw New ArgumentOutOfRangeException(NameOf(offset))
        If offset=0 Then : WriteCommand(value) : Return : End If
        Select Case SelectedPage()
            Case 0
                Select Case offset
                    Case 1 : _pageStart=value
                    Case 2 : _pageStop=value
                    Case 3 : _boundary=value
                    Case 4 : _transmitPage=value
                    Case 5 : _transmitCount=CUShort((_transmitCount And &HFF00US) Or value)
                    Case 6 : _transmitCount=CUShort((_transmitCount And &HFFUS) Or (CUShort(value)<<8))
                    Case 7 : _interruptStatus=CByte(_interruptStatus And Not value) : UpdateIrq()
                    Case 8 : _remoteAddress=CUShort((_remoteAddress And &HFF00US) Or value)
                    Case 9 : _remoteAddress=CUShort((_remoteAddress And &HFFUS) Or (CUShort(value)<<8))
                    Case &HA : _remoteCount=CUShort((_remoteCount And &HFF00US) Or value)
                    Case &HB : _remoteCount=CUShort((_remoteCount And &HFFUS) Or (CUShort(value)<<8))
                    Case &HC : _receiveConfig=value
                    Case &HD : _transmitConfig=value
                    Case &HE : _dataConfig=value
                    Case &HF : _interruptMask=value : UpdateIrq()
                End Select
            Case 1
                If offset>=1 AndAlso offset<=6 Then _physicalAddress(offset-1)=value
                If offset=7 Then _currentPage=value
                If offset>=8 Then _multicast(offset-8)=value
            Case 2
                Select Case offset
                    Case 3 : _remoteNextPacket=value
                    Case 5 : _localNextPacket=value
                    Case 6 : _addressCounter=CUShort((_addressCounter And &HFF00US) Or value)
                    Case 7 : _addressCounter=CUShort((_addressCounter And &HFFUS) Or (CUShort(value)<<8))
                End Select
            Case 3
                If offset=1 Then Write9346Control(value)
        End Select
    End Sub

    Private Function Read9346Control() As Byte
        Dim v As Byte=CByte((_eepromMode And 3)<<6)
        If _eepromChipSelect Then v=CByte(v Or &H8)
        If _eepromClock Then v=CByte(v Or &H4)
        If _eepromDataIn Then v=CByte(v Or &H2)
        If _eepromDataOut Then v=CByte(v Or &H1)
        Return v
    End Function

    Private Sub Write9346Control(value As Byte)
        _eepromMode=CByte((value>>6) And 3)
        ' Datasheet page-3 9346 programming mode exposes EECS/EESK/EEDI directly.
        ' We retain the pin levels in all modes; board glue decides whether a connected
        ' EEPROM is present.  No EEPROM contents are invented here.
        _eepromChipSelect=(value And &H8)<>0
        _eepromClock=(value And &H4)<>0
        _eepromDataIn=(value And &H2)<>0
        RaiseEvent MicrowirePinsChanged(_eepromChipSelect,_eepromClock,_eepromDataIn)
    End Sub

    Public Function RemoteDmaReadByte() As Byte
        If _remoteCount=0 Then Return &HFF
        Dim result=ReadNicMemory(_remoteAddress) : AdvanceRemoteDmaOneByte() : Return result
    End Function
    Public Sub RemoteDmaWriteByte(value As Byte)
        If _remoteCount=0 Then Return
        WriteNicMemory(_remoteAddress,value) : AdvanceRemoteDmaOneByte()
    End Sub

    Private Sub WriteCommand(value As Byte)
        _command=value
        Dim remoteCommand=(value>>3) And 7
        If remoteCommand=1 AndAlso _remoteCount=0 Then _interruptStatus=CByte(_interruptStatus Or IsrRdc) : UpdateIrq()
        If (value And &H4)<>0 Then TransmitPacket() : _command=CByte(_command And Not &H4)
    End Sub
    Private Function SelectedPage() As Integer : Return (_command>>6) And 3 : End Function
    Private Function ReadNicMemory(address As UInt16) As Byte
        Dim a=CInt(address)
        If a>=0 AndAlso a<_stationProm.Length Then Return _stationProm(a)
        If a>=NicRamStart AndAlso a<NicRamEndExclusive Then Return _ram(a-NicRamStart)
        Return &HFF
    End Function
    Private Sub WriteNicMemory(address As UInt16,value As Byte)
        Dim a=CInt(address)
        If a>=NicRamStart AndAlso a<NicRamEndExclusive Then _ram(a-NicRamStart)=value
    End Sub
    Private Sub AdvanceRemoteDmaOneByte()
        _remoteAddress=CUShort((_remoteAddress+1US) And &HFFFFUS) : _addressCounter=_remoteAddress
        If _pageStart<>0 AndAlso _pageStop>_pageStart Then
            Dim stopAddress=CUShort(CUInt(_pageStop)<<8)
            If _remoteAddress=stopAddress Then _remoteAddress=CUShort(CUInt(_pageStart)<<8)
        End If
        _remoteCount=CUShort(_remoteCount-1US)
        If _remoteCount=0 Then _interruptStatus=CByte(_interruptStatus Or IsrRdc) : UpdateIrq()
    End Sub
    Private Sub TransmitPacket()
        Dim length=CInt(_transmitCount), startAddress=CInt(_transmitPage)<<8
        If length<=0 OrElse startAddress<NicRamStart OrElse startAddress+length>NicRamEndExclusive Then
            _transmitStatus=&H8 : _interruptStatus=CByte(_interruptStatus Or IsrTxe) : UpdateIrq() : Return
        End If
        Dim frame(length-1) As Byte : Array.Copy(_ram,startAddress-NicRamStart,frame,0,length)
        RaiseEvent TransmitRequested(frame)
        _transmitStatus=&H1 : _interruptStatus=CByte(_interruptStatus Or IsrPtx) : UpdateIrq()
    End Sub
    Private Sub UpdateIrq() : RaiseEvent InterruptChanged(InterruptAsserted) : End Sub
    Private Sub BuildStationProm(mac As Byte())
        Dim logical(15) As Byte
        For i=0 To 5 : logical(i)=mac(i) : Next
        logical(14)=&H57 : logical(15)=&H57
        For i=0 To 15 : _stationProm(i*2)=logical(i) : _stationProm(i*2+1)=logical(i) : Next
    End Sub
End Class
