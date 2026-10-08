Imports System

' 93LC46C persistent EEPROM primitive. The compiled board supplies wiring.
' CP46 adds the x16 Microwire pin protocol so generated board glue can connect
' real CS/CLK/DI/DO nets instead of bypassing the serial interface.
Public NotInheritable Class Microwire93Lc46c
    Private Const WordCountInBed As Integer = 64
    Private ReadOnly _wordsInBed(WordCountInBed - 1) As UInt16
    Private _writeEnabledInBed As Boolean
    Private _csInBed As Boolean
    Private _clkInBed As Boolean
    Private _doInBed As Boolean
    Private _commandInBed As UInteger
    Private _commandBitsInBed As Integer
    Private _outputInBed As UInteger
    Private _outputBitsInBed As Integer
    Private _writeAddressInBed As Integer = -1
    Private _writeDataInBed As UInteger
    Private _writeBitsInBed As Integer

    Public Sub New(Optional imageInBed As Byte() = Nothing)
        For iInBed As Integer = 0 To _wordsInBed.Length - 1 : _wordsInBed(iInBed) = &HFFFFUS : Next
        If imageInBed Is Nothing Then Return
        If imageInBed.Length > WordCountInBed * 2 Then Throw New ArgumentException("93LC46C image exceeds 128 bytes.", NameOf(imageInBed))
        For iInBed As Integer = 0 To (imageInBed.Length \ 2) - 1
            _wordsInBed(iInBed) = CUShort((CUInt(imageInBed(iInBed * 2)) << 8) Or imageInBed(iInBed * 2 + 1))
        Next
    End Sub

    Public Sub SetWriteEnable(enabledInBed As Boolean)
        _writeEnabledInBed = enabledInBed
    End Sub
    Public Function ReadWord(addressInBed As Integer) As UInt16
        If addressInBed < 0 OrElse addressInBed >= WordCountInBed Then Throw New ArgumentOutOfRangeException(NameOf(addressInBed))
        Return _wordsInBed(addressInBed)
    End Function
    Public Sub WriteWord(addressInBed As Integer, valueInBed As UInt16)
        If Not _writeEnabledInBed Then Return
        If addressInBed < 0 OrElse addressInBed >= WordCountInBed Then Throw New ArgumentOutOfRangeException(NameOf(addressInBed))
        _wordsInBed(addressInBed) = valueInBed
    End Sub
    Public Sub EraseWord(addressInBed As Integer)
        WriteWord(addressInBed, &HFFFFUS)
    End Sub

    ' Drive the three Microwire input pins and return the current DO level.
    ' Commands are sampled MSB-first on CLK rising edges while CS is asserted.
    Public Function SetSerialPins(csInBed As Boolean, clkInBed As Boolean, diInBed As Boolean) As Boolean
        If Not csInBed Then
            _csInBed = False : _clkInBed = clkInBed : ResetSerialTransaction() : Return _doInBed
        End If
        If Not _csInBed Then ResetSerialTransaction()
        Dim risingInBed As Boolean = (Not _clkInBed) AndAlso clkInBed
        _csInBed = True
        If risingInBed Then ClockSerialBit(diInBed)
        _clkInBed = clkInBed
        Return _doInBed
    End Function
    Public ReadOnly Property SerialDataOut As Boolean
        Get
            Return _doInBed
        End Get
    End Property

    Private Sub ResetSerialTransaction()
        _commandInBed = 0UI : _commandBitsInBed = 0 : _outputInBed = 0UI : _outputBitsInBed = 0
        _writeAddressInBed = -1 : _writeDataInBed = 0UI : _writeBitsInBed = 0 : _doInBed = False
    End Sub
    Private Sub ClockSerialBit(diInBed As Boolean)
        If _outputBitsInBed > 0 Then
            _doInBed = ((_outputInBed >> (_outputBitsInBed - 1)) And 1UI) <> 0UI
            _outputBitsInBed -= 1 : Return
        End If
        If _writeAddressInBed >= 0 Then
            _writeDataInBed = (_writeDataInBed << 1) Or If(diInBed, 1UI, 0UI) : _writeBitsInBed += 1
            If _writeBitsInBed = 16 Then WriteWord(_writeAddressInBed, CUShort(_writeDataInBed And &HFFFFUI)) : _writeAddressInBed = -1
            Return
        End If
        ' Ignore leading zeroes until the Microwire start bit.
        If _commandBitsInBed = 0 AndAlso Not diInBed Then Return
        _commandInBed = (_commandInBed << 1) Or If(diInBed, 1UI, 0UI) : _commandBitsInBed += 1
        If _commandBitsInBed <> 9 Then Return
        Dim opcodeInBed As Integer = CInt((_commandInBed >> 6) And 3UI)
        Dim addressInBed As Integer = CInt(_commandInBed And &H3FUI)
        Select Case opcodeInBed
            Case 2 ' READ
                _outputInBed = ReadWord(addressInBed) : _outputBitsInBed = 16
            Case 1 ' WRITE
                _writeAddressInBed = addressInBed : _writeDataInBed = 0UI : _writeBitsInBed = 0
            Case 3 ' ERASE
                EraseWord(addressInBed)
            Case 0 ' EWDS/WRAL/ERAL/EWEN family; implement write-enable controls here.
                Select Case (addressInBed >> 4) And 3
                    Case 0 : SetWriteEnable(False) ' EWDS
                    Case 3 : SetWriteEnable(True)  ' EWEN
                End Select
        End Select
    End Sub

    Public Function Snapshot() As Byte()
        Dim resultInBed(WordCountInBed * 2 - 1) As Byte
        For iInBed As Integer = 0 To WordCountInBed - 1
            resultInBed(iInBed * 2) = CByte((_wordsInBed(iInBed) >> 8) And &HFFUS)
            resultInBed(iInBed * 2 + 1) = CByte(_wordsInBed(iInBed) And &HFFUS)
        Next
        Return resultInBed
    End Function
End Class
