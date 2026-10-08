Imports System

' Board-Script-first split of the physical four-chip CS8221 NEAT family.
' This DOES NOT replace the proven live NeatCs8221Chipset class yet.  It gives
' each actual package a worker boundary so Board Script can compose the board
' without pretending the four-package chipset is one physical IC.

Public NotInheritable Class Ct82C211BoardScriptWorker
    Implements IBoardScriptWorker

    Private _reg60 As Byte
    Private _reg61 As Byte
    Private _reg62 As Byte

    Public Sub New(componentInBed As BoardScriptComponentDefinition)
        If componentInBed Is Nothing Then Throw New ArgumentNullException(NameOf(componentInBed))
        If componentInBed.Straps.Count <> 0 Then Throw New InvalidOperationException("82C211 prototype exposes no board straps yet; its indexed registers are firmware state.")
        ResetRegisters()
    End Sub

    Public Sub ResetRegisters()
        _reg60 = 0
        _reg61 = &H45
        _reg62 = &H18
    End Sub

    Public Function ReadIndexedRegister(indexInBed As Byte) As Byte
        Select Case indexInBed
            Case &H60 : Return CByte(_reg60 And &H34)
            Case &H61 : Return _reg61
            Case &H62 : Return _reg62
            Case Else : Return &HFF
        End Select
    End Function

    Public Sub WriteIndexedRegister(indexInBed As Byte, valueInBed As Byte)
        Select Case indexInBed
            Case &H60 : _reg60 = CByte(valueInBed And &H34)
            Case &H61 : _reg61 = valueInBed
            Case &H62 : _reg62 = CByte(valueInBed And &H3F)
        End Select
    End Sub

    Public ReadOnly Property WorkerId As String Implements IBoardScriptWorker.WorkerId
        Get
            Return "ct.neat.82c211"
        End Get
    End Property

    Public ReadOnly Property SiliconIdentity As String Implements IBoardScriptWorker.SiliconIdentity
        Get
            Return "Chips and Technologies 82C211 CPU/Bus Controller"
        End Get
    End Property

    Public Function DiagnosticText() As String Implements IBoardScriptWorker.DiagnosticText
        Return SiliconIdentity & " REG60=" & _reg60.ToString("X2") & " REG61=" & _reg61.ToString("X2") & " REG62=" & _reg62.ToString("X2")
    End Function
End Class

Public NotInheritable Class Ct82C212BoardScriptWorker
    Implements IBoardScriptWorker

    Private ReadOnly _registers(11) As Byte

    Public Sub New(componentInBed As BoardScriptComponentDefinition)
        If componentInBed Is Nothing Then Throw New ArgumentNullException(NameOf(componentInBed))
        If componentInBed.Straps.Count <> 0 Then Throw New InvalidOperationException("82C212 prototype exposes no board straps yet; its indexed registers are firmware state.")
        ResetRegisters()
    End Sub

    Public Sub ResetRegisters()
        Array.Clear(_registers, 0, _registers.Length)
        _registers(0) = 0
        _registers(1) = &HE
        _registers(6) = &H80
        _registers(7) = &H6B
    End Sub

    Public Function ReadIndexedRegister(indexInBed As Byte) As Byte
        If indexInBed < &H64 OrElse indexInBed > &H6F Then Return &HFF
        Return _registers(indexInBed - &H64)
    End Function

    Public Sub WriteIndexedRegister(indexInBed As Byte, valueInBed As Byte)
        If indexInBed < &H65 OrElse indexInBed > &H6F Then Return
        Dim slotInBed As Integer = indexInBed - &H64
        Select Case indexInBed
            Case &H65, &H67, &H68, &H69, &H6B, &H6D, &H6E
                _registers(slotInBed) = valueInBed
            Case &H66
                _registers(slotInBed) = CByte(valueInBed And &H80)
            Case &H6A, &H6C
                _registers(slotInBed) = CByte(valueInBed And &HE0)
            Case &H6F
                _registers(slotInBed) = CByte(valueInBed And &HE6)
        End Select
    End Sub

    Public ReadOnly Property WorkerId As String Implements IBoardScriptWorker.WorkerId
        Get
            Return "ct.neat.82c212"
        End Get
    End Property

    Public ReadOnly Property SiliconIdentity As String Implements IBoardScriptWorker.SiliconIdentity
        Get
            Return "Chips and Technologies 82C212 Page/Interleave and EMS Memory Controller"
        End Get
    End Property

    Public Function DiagnosticText() As String Implements IBoardScriptWorker.DiagnosticText
        Return SiliconIdentity & " REG65=" & _registers(1).ToString("X2") & " REG6A=" & _registers(6).ToString("X2") & " REG6B=" & _registers(7).ToString("X2")
    End Function
End Class

Public NotInheritable Class Ct82C215BoardScriptWorker
    Implements IBoardScriptWorker

    Public Sub New(componentInBed As BoardScriptComponentDefinition)
        If componentInBed Is Nothing Then Throw New ArgumentNullException(NameOf(componentInBed))
        If componentInBed.Straps.Count <> 0 Then Throw New InvalidOperationException("82C215 is a data/address buffer; no guest-visible strap surface is declared in this prototype.")
    End Sub

    Public ReadOnly Property WorkerId As String Implements IBoardScriptWorker.WorkerId
        Get
            Return "ct.neat.82c215"
        End Get
    End Property

    Public ReadOnly Property SiliconIdentity As String Implements IBoardScriptWorker.SiliconIdentity
        Get
            Return "Chips and Technologies 82C215 Data/Address Buffer"
        End Get
    End Property

    Public Function DiagnosticText() As String Implements IBoardScriptWorker.DiagnosticText
        Return SiliconIdentity & " (physical parity/buffer worker boundary; no indexed registers)"
    End Function
End Class

Public NotInheritable Class Ct82C206BoardScriptWorker
    Implements IBoardScriptWorker

    Private _configuration As Byte

    Public Sub New(componentInBed As BoardScriptComponentDefinition)
        If componentInBed Is Nothing Then Throw New ArgumentNullException(NameOf(componentInBed))
        If componentInBed.Straps.Count <> 0 Then Throw New InvalidOperationException("82C206 prototype exposes no board straps yet; configuration register 01h is firmware state.")
        ResetRegisters()
    End Sub

    Public Sub ResetRegisters()
        _configuration = &HC0
    End Sub

    Public Function ReadIndexedRegister(indexInBed As Byte) As Byte
        If indexInBed = &H1 Then Return _configuration
        Return &HFF
    End Function

    Public Sub WriteIndexedRegister(indexInBed As Byte, valueInBed As Byte)
        If indexInBed = &H1 Then _configuration = valueInBed
    End Sub

    Public ReadOnly Property WorkerId As String Implements IBoardScriptWorker.WorkerId
        Get
            Return "ct.neat.82c206"
        End Get
    End Property

    Public ReadOnly Property SiliconIdentity As String Implements IBoardScriptWorker.SiliconIdentity
        Get
            Return "Chips and Technologies 82C206 Integrated Peripherals Controller"
        End Get
    End Property

    Public Function DiagnosticText() As String Implements IBoardScriptWorker.DiagnosticText
        Return SiliconIdentity & " REG01=" & _configuration.ToString("X2")
    End Function
End Class
