Option Strict On
' CP69: Intel 8255A PPI digital functional model. Analog/timing specs remain Component Store facts.
Public NotInheritable Class VmbIntel8255A
    Public Const ModuleId As String = "VMB.IO.INTEL_8255A"
    Private _control As Byte, _paOut As Byte, _pbOut As Byte, _pcOut As Byte
    Private _paIn As Byte, _pbIn As Byte, _pcIn As Byte
    Private _gaMode As Integer, _gbMode As Integer
    Private _paInput As Boolean, _pbInput As Boolean, _pcUpperInput As Boolean, _pcLowerInput As Boolean
    Private _ibfA As Boolean, _ibfB As Boolean, _obfAN As Boolean, _obfBN As Boolean
    Private _inteAIn As Boolean, _inteAOut As Boolean, _inteB As Boolean
    Public ReadOnly Property InterruptA As Boolean
    Public ReadOnly Property InterruptB As Boolean
    Public Sub New() : Reset() : End Sub
    Public Sub Reset()
        _control=&H9B : _paOut=0 : _pbOut=0 : _pcOut=0 : _paIn=0 : _pbIn=0 : _pcIn=0
        _ibfA=False : _ibfB=False : _obfAN=True : _obfBN=True
        _inteAIn=False : _inteAOut=False : _inteB=False : DecodeControl() : UpdateInterrupts()
    End Sub
    Private Sub DecodeControl()
        If (_control And &H60)=&H40 Then _gaMode=2 Else _gaMode=(_control >> 5) And 1
        _gbMode=(_control >> 2) And 1
        _paInput=(_control And &H10)<>0 : _pcUpperInput=(_control And 8)<>0
        _pbInput=(_control And 2)<>0 : _pcLowerInput=(_control And 1)<>0
    End Sub
    Public Sub SetInputs(a As Byte,b As Byte,c As Byte) : _paIn=a : _pbIn=b : _pcIn=c : End Sub
    Public Sub WriteRegister(address As Integer,value As Byte)
        Select Case address And 3
            Case 3
                If (value And &H80)<>0 Then
                    _control=value : DecodeControl() : _paOut=0 : _pbOut=0 : _pcOut=0 : _ibfA=False : _ibfB=False : _obfAN=True : _obfBN=True
                Else
                    Dim bit As Integer=(value >> 1) And 7, mask As Integer=1 << bit, state As Boolean=(value And 1)<>0
                    If state Then _pcOut=CByte(_pcOut Or mask) Else _pcOut=CByte(_pcOut And (Not mask And &HFF))
                    If bit=4 AndAlso (_gaMode=2 OrElse (_gaMode=1 AndAlso _paInput)) Then _inteAIn=state
                    If bit=6 AndAlso (_gaMode=2 OrElse (_gaMode=1 AndAlso Not _paInput)) Then _inteAOut=state
                    If bit=2 AndAlso _gbMode=1 Then _inteB=state
                End If
            Case 0
                _paOut=value : If _gaMode<>0 AndAlso Not _paInput Then _obfAN=False
            Case 1
                _pbOut=value : If _gbMode=1 AndAlso Not _pbInput Then _obfBN=False
            Case 2 : _pcOut=value
        End Select
        UpdateInterrupts()
    End Sub
    Public Function ReadRegister(address As Integer) As Byte
        Select Case address And 3
            Case 3 : Return _control
            Case 0
                Dim v As Byte=If(_paInput OrElse _gaMode=2,_paIn,_paOut)
                If _gaMode<>0 AndAlso (_paInput OrElse _gaMode=2) Then _ibfA=False : UpdateInterrupts()
                Return v
            Case 1
                Dim v As Byte=If(_pbInput,_pbIn,_pbOut)
                If _gbMode=1 AndAlso _pbInput Then _ibfB=False : UpdateInterrupts()
                Return v
            Case Else
                Dim v As Integer=0
                For bit As Integer=0 To 7
                    Dim inp As Boolean=If(bit>=4,_pcUpperInput,_pcLowerInput)
                    Dim src As Byte=If(inp,_pcIn,_pcOut)
                    If (src And (1 << bit))<>0 Then v=v Or (1 << bit)
                Next
                If _gbMode=1 Then
                    v=v And Not 7
                    If InterruptB Then v=v Or 1
                    If _pbInput Then
                        If _ibfB Then v=v Or 2
                    Else
                        If _obfBN Then v=v Or 2
                    End If
                End If
                If _gaMode=1 Then
                    If _paInput Then
                        v=v And Not &H38 : If InterruptA Then v=v Or 8 : If _ibfA Then v=v Or &H20
                    Else
                        v=v And Not &HC8 : If InterruptA Then v=v Or 8 : If _obfAN Then v=v Or &H80
                    End If
                ElseIf _gaMode=2 Then
                    v=v And Not &HF8 : If InterruptA Then v=v Or 8 : If _ibfA Then v=v Or &H20 : If _obfAN Then v=v Or &H80
                End If
                Return CByte(v And &HFF)
        End Select
    End Function
    Public Sub StrobeA(data As Byte) : If Not (_gaMode=2 OrElse (_gaMode=1 AndAlso _paInput)) Then Throw New InvalidOperationException() : _paIn=data : _ibfA=True : UpdateInterrupts() : End Sub
    Public Sub AcknowledgeA() : If Not (_gaMode=2 OrElse (_gaMode=1 AndAlso Not _paInput)) Then Throw New InvalidOperationException() : _obfAN=True : UpdateInterrupts() : End Sub
    Public Sub StrobeB(data As Byte) : If Not (_gbMode=1 AndAlso _pbInput) Then Throw New InvalidOperationException() : _pbIn=data : _ibfB=True : UpdateInterrupts() : End Sub
    Public Sub AcknowledgeB() : If Not (_gbMode=1 AndAlso Not _pbInput) Then Throw New InvalidOperationException() : _obfBN=True : UpdateInterrupts() : End Sub
    Private Sub UpdateInterrupts()
        If _gaMode=2 Then
            InterruptA=(_inteAIn AndAlso _ibfA) OrElse (_inteAOut AndAlso _obfAN)
        ElseIf _gaMode=1 Then
            InterruptA=If(_paInput,_inteAIn AndAlso _ibfA,_inteAOut AndAlso _obfAN)
        Else : InterruptA=False : End If
        InterruptB=If(_gbMode=1,_inteB AndAlso If(_pbInput,_ibfB,_obfBN),False)
    End Sub
End Class
