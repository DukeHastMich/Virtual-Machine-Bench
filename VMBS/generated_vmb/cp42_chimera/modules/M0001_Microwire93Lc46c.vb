Imports System

' 93LC46C persistent EEPROM primitive. The compiled board supplies the wiring
' and organization; this object owns nonvolatile bits and basic word access.
Public NotInheritable Class Microwire93Lc46c
    Private Const WordCountInBed As Integer = 64
    Private ReadOnly _wordsInBed(WordCountInBed - 1) As UInt16
    Private _writeEnabledInBed As Boolean

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

    Public Function Snapshot() As Byte()
        Dim resultInBed(WordCountInBed * 2 - 1) As Byte
        For iInBed As Integer = 0 To WordCountInBed - 1
            resultInBed(iInBed * 2) = CByte((_wordsInBed(iInBed) >> 8) And &HFFUS)
            resultInBed(iInBed * 2 + 1) = CByte(_wordsInBed(iInBed) And &HFFUS)
        Next
        Return resultInBed
    End Function
End Class
