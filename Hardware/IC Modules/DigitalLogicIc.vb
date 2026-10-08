Imports System
Imports System.Collections.Generic

' Reusable digital-resolution IC primitives used by VMBS-manufactured boards.
' This layer models computer-visible logic behavior, not analog TTL thresholds/delay.
' Nullable Boolean = tri-state: Nothing means the output is high impedance.
Public Interface IVmbDigitalIc
    ReadOnly Property ModuleId As String
    Sub Reset()
    Function Evaluate(inputs As IReadOnlyDictionary(Of String, Nullable(Of Boolean))) As IReadOnlyDictionary(Of String, Nullable(Of Boolean))
End Interface

Public MustInherit Class VmbDigitalIcBase
    Implements IVmbDigitalIc
    Public MustOverride ReadOnly Property ModuleId As String Implements IVmbDigitalIc.ModuleId
    Public Overridable Sub Reset() Implements IVmbDigitalIc.Reset
    End Sub
    Public MustOverride Function Evaluate(inputs As IReadOnlyDictionary(Of String, Nullable(Of Boolean))) As IReadOnlyDictionary(Of String, Nullable(Of Boolean)) Implements IVmbDigitalIc.Evaluate
    Protected Shared Function Pin(i As IReadOnlyDictionary(Of String, Nullable(Of Boolean)), name As String) As Nullable(Of Boolean)
        Dim v As Nullable(Of Boolean)=Nothing
        If i IsNot Nothing AndAlso i.TryGetValue(name,v) Then Return v
        Return Nothing
    End Function
    Protected Shared Function Z() As Nullable(Of Boolean)
        Return Nothing
    End Function
End Class

Public NotInheritable Class Vmb74xxGateIc
    Inherits VmbDigitalIcBase
    Private ReadOnly _id As String
    Private ReadOnly _gates As Integer
    Private ReadOnly _kind As String
    Public Sub New(moduleId As String, gateCount As Integer, kind As String)
        _id=moduleId : _gates=gateCount : _kind=kind
    End Sub
    Public Overrides ReadOnly Property ModuleId As String
        Get
            Return _id
        End Get
    End Property
    Public Overrides Function Evaluate(i As IReadOnlyDictionary(Of String, Nullable(Of Boolean))) As IReadOnlyDictionary(Of String, Nullable(Of Boolean))
        Dim o As New Dictionary(Of String, Nullable(Of Boolean))(StringComparer.OrdinalIgnoreCase)
        For n=1 To _gates
            Dim a=Pin(i,n.ToString() & "A")
            If _kind="NOT" Then
                o(n.ToString() & "Y")=If(a.HasValue,Not a.Value,CType(Nothing,Nullable(Of Boolean)))
            Else
                Dim b=Pin(i,n.ToString() & "B")
                If Not a.HasValue OrElse Not b.HasValue Then
                    o(n.ToString() & "Y")=Z()
                Else
                    Dim y As Boolean
                    Select Case _kind
                        Case "NAND" : y=Not (a.Value AndAlso b.Value)
                        Case "NOR" : y=Not (a.Value OrElse b.Value)
                        Case "AND" : y=a.Value AndAlso b.Value
                        Case "OR" : y=a.Value OrElse b.Value
                        Case "XOR" : y=a.Value Xor b.Value
                        Case Else : Throw New InvalidOperationException("Unknown gate kind " & _kind)
                    End Select
                    o(n.ToString() & "Y")=y
                End If
            End If
        Next
        Return o
    End Function
End Class


Public NotInheritable Class Vmb74xx157
    Inherits VmbDigitalIcBase
    Public Overrides ReadOnly Property ModuleId As String
        Get
            Return "VMB.LOGIC.74XX157"
        End Get
    End Property
    Public Overrides Function Evaluate(i As IReadOnlyDictionary(Of String, Nullable(Of Boolean))) As IReadOnlyDictionary(Of String, Nullable(Of Boolean))
        Dim o As New Dictionary(Of String, Nullable(Of Boolean))(StringComparer.OrdinalIgnoreCase)
        Dim enabled=Not Pin(i,"E_N").GetValueOrDefault(True)
        Dim sel=Pin(i,"S")
        For n=1 To 4
            If Not enabled Then
                o(n.ToString() & "Y")=False
            ElseIf Not sel.HasValue Then
                o(n.ToString() & "Y")=Z()
            Else
                o(n.ToString() & "Y")=Pin(i,n.ToString() & If(sel.Value,"B","A"))
            End If
        Next
        Return o
    End Function
End Class

Public NotInheritable Class Vmb74xx688
    Inherits VmbDigitalIcBase
    Public Overrides ReadOnly Property ModuleId As String
        Get
            Return "VMB.LOGIC.74XX688"
        End Get
    End Property
    Public Overrides Function Evaluate(i As IReadOnlyDictionary(Of String, Nullable(Of Boolean))) As IReadOnlyDictionary(Of String, Nullable(Of Boolean))
        Dim enabled=Not Pin(i,"G_N").GetValueOrDefault(True)
        Dim equal As Boolean=True
        For n=0 To 7
            Dim p=Pin(i,"P" & n.ToString()), q=Pin(i,"Q" & n.ToString())
            If Not p.HasValue OrElse Not q.HasValue OrElse p.Value<>q.Value Then equal=False
        Next
        Return New Dictionary(Of String, Nullable(Of Boolean))(StringComparer.OrdinalIgnoreCase) From {{"P_EQ_Q_N", Not (enabled AndAlso equal)}}
    End Function
End Class

Public NotInheritable Class Vmb74xx138
    Inherits VmbDigitalIcBase
    Public Overrides ReadOnly Property ModuleId As String
        Get
            Return "VMB.LOGIC.74XX138"
        End Get
    End Property
    Public Overrides Function Evaluate(i As IReadOnlyDictionary(Of String, Nullable(Of Boolean))) As IReadOnlyDictionary(Of String, Nullable(Of Boolean))
        Dim enabled=Pin(i,"G1").GetValueOrDefault(False) AndAlso Not Pin(i,"G2A_N").GetValueOrDefault(True) AndAlso Not Pin(i,"G2B_N").GetValueOrDefault(True)
        Dim sel As Integer=0
        If Pin(i,"A").GetValueOrDefault(False) Then sel=sel Or 1
        If Pin(i,"B").GetValueOrDefault(False) Then sel=sel Or 2
        If Pin(i,"C").GetValueOrDefault(False) Then sel=sel Or 4
        Dim o As New Dictionary(Of String, Nullable(Of Boolean))(StringComparer.OrdinalIgnoreCase)
        For n=0 To 7 : o("Y" & n.ToString() & "_N")=Not (enabled AndAlso n=sel) : Next
        Return o
    End Function
End Class

Public NotInheritable Class Vmb74xx244
    Inherits VmbDigitalIcBase
    Public Overrides ReadOnly Property ModuleId As String
        Get
            Return "VMB.LOGIC.74XX244"
        End Get
    End Property
    Public Overrides Function Evaluate(i As IReadOnlyDictionary(Of String, Nullable(Of Boolean))) As IReadOnlyDictionary(Of String, Nullable(Of Boolean))
        Dim o As New Dictionary(Of String, Nullable(Of Boolean))(StringComparer.OrdinalIgnoreCase)
        For bank=1 To 2
            Dim enabled=Not Pin(i,"OE" & bank.ToString() & "_N").GetValueOrDefault(True)
            For n=1 To 4 : o(bank.ToString() & "Y" & n.ToString())=If(enabled,Pin(i,bank.ToString() & "A" & n.ToString()),Z()) : Next
        Next
        Return o
    End Function
End Class

Public NotInheritable Class Vmb74xx245
    Inherits VmbDigitalIcBase
    Public Overrides ReadOnly Property ModuleId As String
        Get
            Return "VMB.LOGIC.74XX245"
        End Get
    End Property
    Public Overrides Function Evaluate(i As IReadOnlyDictionary(Of String, Nullable(Of Boolean))) As IReadOnlyDictionary(Of String, Nullable(Of Boolean))
        Dim o As New Dictionary(Of String, Nullable(Of Boolean))(StringComparer.OrdinalIgnoreCase)
        Dim enabled=Not Pin(i,"OE_N").GetValueOrDefault(True)
        Dim dir=Pin(i,"DIR")
        For n=1 To 8
            Dim a="A" & n.ToString(), b="B" & n.ToString()
            o(a)=Z() : o(b)=Z()
            If enabled AndAlso dir.HasValue Then
                If dir.Value Then o(b)=Pin(i,a) Else o(a)=Pin(i,b)
            End If
        Next
        Return o
    End Function
End Class


Public NotInheritable Class Vmb74xx74
    Inherits VmbDigitalIcBase
    Private ReadOnly _q() As Boolean={False,False}
    Private ReadOnly _lastClock() As Boolean={False,False}
    Public Overrides ReadOnly Property ModuleId As String
        Get
            Return "VMB.LOGIC.74XX74"
        End Get
    End Property
    Public Overrides Sub Reset()
        _q(0)=False:_q(1)=False:_lastClock(0)=False:_lastClock(1)=False
    End Sub
    Public Overrides Function Evaluate(i As IReadOnlyDictionary(Of String, Nullable(Of Boolean))) As IReadOnlyDictionary(Of String, Nullable(Of Boolean))
        Dim o As New Dictionary(Of String, Nullable(Of Boolean))(StringComparer.OrdinalIgnoreCase)
        For n=1 To 2
            Dim pre=Pin(i,n.ToString() & "PRE_N"), clr=Pin(i,n.ToString() & "CLR_N"), clk=Pin(i,n.ToString() & "CLK").GetValueOrDefault(False)
            If pre.HasValue AndAlso Not pre.Value AndAlso (Not clr.HasValue OrElse clr.Value) Then
                _q(n-1)=True
            ElseIf clr.HasValue AndAlso Not clr.Value AndAlso (Not pre.HasValue OrElse pre.Value) Then
                _q(n-1)=False
            ElseIf pre.HasValue AndAlso clr.HasValue AndAlso Not pre.Value AndAlso Not clr.Value Then
                ' Datasheet-forbidden PRE#=CLR#=LOW state: both physical outputs are HIGH.
                ' _q is retained only as the next valid internal state; outputs are overridden below.
                _q(n-1)=True
            ElseIf Not _lastClock(n-1) AndAlso clk Then
                Dim d=Pin(i,n.ToString() & "D") : If d.HasValue Then _q(n-1)=d.Value
            End If
            _lastClock(n-1)=clk
            If pre.HasValue AndAlso clr.HasValue AndAlso Not pre.Value AndAlso Not clr.Value Then
                o(n.ToString() & "Q")=True:o(n.ToString() & "Q_N")=True
            Else
                o(n.ToString() & "Q")=_q(n-1):o(n.ToString() & "Q_N")=Not _q(n-1)
            End If
        Next
        Return o
    End Function
End Class

Public NotInheritable Class Vmb74xx273
    Inherits VmbDigitalIcBase
    Private ReadOnly _q(7) As Boolean
    Private _lastClock As Boolean
    Private _counterValue As Integer
    Public Overrides ReadOnly Property ModuleId As String
        Get
            Return "VMB.LOGIC.74XX273"
        End Get
    End Property
    Public Overrides Sub Reset()
        Array.Clear(_q,0,_q.Length):_lastClock=False
    End Sub
    Public Overrides Function Evaluate(i As IReadOnlyDictionary(Of String, Nullable(Of Boolean))) As IReadOnlyDictionary(Of String, Nullable(Of Boolean))
        Dim clk=Pin(i,"CLK").GetValueOrDefault(False)
        If Not Pin(i,"CLR_N").GetValueOrDefault(True) Then
            Array.Clear(_q,0,_q.Length)
        ElseIf Not _lastClock AndAlso clk Then
            For n=1 To 8 : Dim d=Pin(i,"D" & n.ToString()) : If d.HasValue Then _q(n-1)=d.Value
            Next
        End If
        _lastClock=clk
        Dim o As New Dictionary(Of String, Nullable(Of Boolean))(StringComparer.OrdinalIgnoreCase)
        For n=1 To 8:o("Q" & n.ToString())=_q(n-1):Next
        Return o
    End Function
End Class

Public NotInheritable Class Vmb74xx16x
    Inherits VmbDigitalIcBase
    Private ReadOnly _id As String
    Private ReadOnly _synchronousClear As Boolean
    Private _value As Integer
    Private _lastClock As Boolean
    Private _counterValue As Integer
    Public Sub New(moduleId As String,synchronousClear As Boolean)
        _id=moduleId:_synchronousClear=synchronousClear
    End Sub
    Public Overrides ReadOnly Property ModuleId As String
        Get
            Return _id
        End Get
    End Property
    Public Overrides Sub Reset()
        _value=0:_lastClock=False
    End Sub
    Public Overrides Function Evaluate(i As IReadOnlyDictionary(Of String, Nullable(Of Boolean))) As IReadOnlyDictionary(Of String, Nullable(Of Boolean))
        Dim clk=Pin(i,"CLK").GetValueOrDefault(False), clr=Pin(i,"CLR_N").GetValueOrDefault(True)
        If Not _synchronousClear AndAlso Not clr Then
            _value=0
        ElseIf Not _lastClock AndAlso clk Then
            If _synchronousClear AndAlso Not clr Then
                _value=0
            ElseIf Not Pin(i,"LOAD_N").GetValueOrDefault(True) Then
                _value=0
                Dim names() As String={"A","B","C","D"}
                For n=0 To 3:If Pin(i,names(n)).GetValueOrDefault(False) Then _value=_value Or (1<<n)
                Next
            ElseIf Pin(i,"ENP").GetValueOrDefault(False) AndAlso Pin(i,"ENT").GetValueOrDefault(False) Then
                _value=(_value+1) And 15
            End If
        End If
        _lastClock=clk
        Dim o As New Dictionary(Of String, Nullable(Of Boolean))(StringComparer.OrdinalIgnoreCase)
        Dim qn() As String={"QA","QB","QC","QD"}
        For n=0 To 3:o(qn(n))=((_value And (1<<n))<>0):Next
        o("RCO")=(_value=15 AndAlso Pin(i,"ENT").GetValueOrDefault(False))
        Return o
    End Function
End Class

Public NotInheritable Class Vmb74xx393
    Inherits VmbDigitalIcBase
    Private ReadOnly _value() As Integer={0,0}
    Private ReadOnly _lastClock() As Boolean={False,False}
    Public Overrides ReadOnly Property ModuleId As String
        Get
            Return "VMB.LOGIC.74XX393"
        End Get
    End Property
    Public Overrides Sub Reset()
        _value(0)=0:_value(1)=0:_lastClock(0)=False:_lastClock(1)=False
    End Sub
    Public Overrides Function Evaluate(i As IReadOnlyDictionary(Of String, Nullable(Of Boolean))) As IReadOnlyDictionary(Of String, Nullable(Of Boolean))
        Dim o As New Dictionary(Of String, Nullable(Of Boolean))(StringComparer.OrdinalIgnoreCase)
        For n=1 To 2
            Dim clk=Pin(i,n.ToString() & "CLK").GetValueOrDefault(False)
            If Pin(i,n.ToString() & "CLR").GetValueOrDefault(False) Then
                _value(n-1)=0
            ElseIf _lastClock(n-1) AndAlso Not clk Then
                _value(n-1)=(_value(n-1)+1) And 15
            End If
            _lastClock(n-1)=clk
            Dim names() As String={"A","B","C","D"}
            For b=0 To 3:o(n.ToString() & "Q" & names(b))=((_value(n-1) And (1<<b))<>0):Next
        Next
        Return o
    End Function
End Class

Public NotInheritable Class Vmb74xx373
    Inherits VmbDigitalIcBase
    Private ReadOnly _q(7) As Boolean
    Public Overrides ReadOnly Property ModuleId As String
        Get
            Return "VMB.LOGIC.74XX373"
        End Get
    End Property
    Public Overrides Sub Reset()
        Array.Clear(_q,0,_q.Length)
    End Sub
    Public Overrides Function Evaluate(i As IReadOnlyDictionary(Of String, Nullable(Of Boolean))) As IReadOnlyDictionary(Of String, Nullable(Of Boolean))
        If Pin(i,"LE").GetValueOrDefault(False) Then
            For n=1 To 8 : Dim v=Pin(i,"D" & n.ToString()) : If v.HasValue Then _q(n-1)=v.Value
            Next
        End If
        Dim enabled=Not Pin(i,"OE_N").GetValueOrDefault(True)
        Dim o As New Dictionary(Of String, Nullable(Of Boolean))(StringComparer.OrdinalIgnoreCase)
        For n=1 To 8 : o("Q" & n.ToString())=If(enabled,CType(_q(n-1),Nullable(Of Boolean)),Z()) : Next
        Return o
    End Function
End Class

Public NotInheritable Class Vmb74xx374
    Inherits VmbDigitalIcBase
    Private ReadOnly _q(7) As Boolean
    Private _lastClock As Boolean
    Private _counterValue As Integer
    Public Overrides ReadOnly Property ModuleId As String
        Get
            Return "VMB.LOGIC.74XX374"
        End Get
    End Property
    Public Overrides Sub Reset()
        Array.Clear(_q,0,_q.Length) : _lastClock=False
    End Sub
    Public Overrides Function Evaluate(i As IReadOnlyDictionary(Of String, Nullable(Of Boolean))) As IReadOnlyDictionary(Of String, Nullable(Of Boolean))
        Dim clk=Pin(i,"CLK").GetValueOrDefault(False)
        If Not _lastClock AndAlso clk Then
            For n=1 To 8 : Dim v=Pin(i,"D" & n.ToString()) : If v.HasValue Then _q(n-1)=v.Value
            Next
        End If
        _lastClock=clk
        Dim enabled=Not Pin(i,"OE_N").GetValueOrDefault(True)
        Dim o As New Dictionary(Of String, Nullable(Of Boolean))(StringComparer.OrdinalIgnoreCase)
        For n=1 To 8 : o("Q" & n.ToString())=If(enabled,CType(_q(n-1),Nullable(Of Boolean)),Z()) : Next
        Return o
    End Function
End Class

Public NotInheritable Class VmbAliasedDigitalIc
    Inherits VmbDigitalIcBase
    Private ReadOnly _aliasId As String
    Private ReadOnly _inner As IVmbDigitalIc
    Public Sub New(aliasId As String, inner As IVmbDigitalIc)
        _aliasId=aliasId : _inner=inner
    End Sub
    Public Overrides ReadOnly Property ModuleId As String
        Get
            Return _aliasId
        End Get
    End Property
    Public Overrides Sub Reset()
        _inner.Reset()
    End Sub
    Public Overrides Function Evaluate(i As IReadOnlyDictionary(Of String, Nullable(Of Boolean))) As IReadOnlyDictionary(Of String, Nullable(Of Boolean))
        Return _inner.Evaluate(i)
    End Function
End Class

Public NotInheritable Class VmbDigitalIcRegistry
    Private Sub New()
    End Sub
    Public Shared Function Supports(moduleId As String) As Boolean
        Return Create(moduleId,throwIfUnknown:=False) IsNot Nothing
    End Function
    Public Shared Function Create(moduleId As String, Optional throwIfUnknown As Boolean=True) As IVmbDigitalIc
        Dim x As IVmbDigitalIc=Nothing
        Select Case moduleId
            Case "VMB.LOGIC.TI_SN74HC00" : x=New VmbAliasedDigitalIc(moduleId,Create("VMB.LOGIC.74XX00"))
            Case "VMB.LOGIC.TI_SN74HC02" : x=New VmbAliasedDigitalIc(moduleId,Create("VMB.LOGIC.74XX02"))
            Case "VMB.LOGIC.TI_SN74HC04" : x=New VmbAliasedDigitalIc(moduleId,Create("VMB.LOGIC.74XX04"))
            Case "VMB.LOGIC.TI_SN74HC08" : x=New VmbAliasedDigitalIc(moduleId,Create("VMB.LOGIC.74XX08"))
            Case "VMB.LOGIC.TI_SN74HC10" : x=New VmbAliasedDigitalIc(moduleId,Create("VMB.LOGIC.74XX10"))
            Case "VMB.LOGIC.TI_SN74HC11" : x=New VmbAliasedDigitalIc(moduleId,Create("VMB.LOGIC.74XX11"))
            Case "VMB.LOGIC.TI_SN74HC14" : x=New VmbAliasedDigitalIc(moduleId,Create("VMB.LOGIC.74XX14"))
            Case "VMB.LOGIC.TI_SN74HC20" : x=New VmbAliasedDigitalIc(moduleId,Create("VMB.LOGIC.74XX20"))
            Case "VMB.LOGIC.TI_SN74HC21" : x=New VmbAliasedDigitalIc(moduleId,Create("VMB.LOGIC.74XX21"))
            Case "VMB.LOGIC.TI_SN74HC27" : x=New VmbAliasedDigitalIc(moduleId,Create("VMB.LOGIC.74XX27"))
            Case "VMB.LOGIC.TI_SN74HC30" : x=New VmbAliasedDigitalIc(moduleId,Create("VMB.LOGIC.74XX30"))
            Case "VMB.LOGIC.TI_SN74HC32" : x=New VmbAliasedDigitalIc(moduleId,Create("VMB.LOGIC.74XX32"))
            Case "VMB.LOGIC.TI_SN74HC42" : x=New VmbAliasedDigitalIc(moduleId,Create("VMB.LOGIC.74XX42"))
            Case "VMB.LOGIC.TI_SN74HC85" : x=New VmbAliasedDigitalIc(moduleId,Create("VMB.LOGIC.74XX85"))
            Case "VMB.LOGIC.TI_SN74HC86" : x=New VmbAliasedDigitalIc(moduleId,Create("VMB.LOGIC.74XX86"))
            Case "VMB.LOGIC.TI_SN74HC125" : x=New VmbAliasedDigitalIc(moduleId,Create("VMB.LOGIC.74XX125"))
            Case "VMB.LOGIC.TI_SN74HC126" : x=New VmbAliasedDigitalIc(moduleId,Create("VMB.LOGIC.74XX126"))
            Case "VMB.LOGIC.TI_SN74HC138" : x=New VmbAliasedDigitalIc(moduleId,Create("VMB.LOGIC.74XX138"))
            Case "VMB.LOGIC.TI_SN74HC139" : x=New VmbAliasedDigitalIc(moduleId,Create("VMB.LOGIC.74XX139"))
            Case "VMB.LOGIC.TI_SN74HC148" : x=New VmbAliasedDigitalIc(moduleId,Create("VMB.LOGIC.74XX148"))
            Case "VMB.LOGIC.TI_SN74HC151" : x=New VmbAliasedDigitalIc(moduleId,Create("VMB.LOGIC.74XX151"))
            Case "VMB.LOGIC.TI_SN74HC153" : x=New VmbAliasedDigitalIc(moduleId,Create("VMB.LOGIC.74XX153"))
            Case "VMB.LOGIC.TI_SN74HC157" : x=New VmbAliasedDigitalIc(moduleId,Create("VMB.LOGIC.74XX157"))
            Case "VMB.LOGIC.TI_SN74HC244" : x=New VmbAliasedDigitalIc(moduleId,Create("VMB.LOGIC.74XX244"))
            Case "VMB.LOGIC.TI_SN74HC245" : x=New VmbAliasedDigitalIc(moduleId,Create("VMB.LOGIC.74XX245"))
            Case "VMB.LOGIC.74XX00" : x=New Vmb74xxGateIc(moduleId,4,"NAND")
            Case "VMB.LOGIC.74XX02" : x=New Vmb74xxGateIc(moduleId,4,"NOR")
            Case "VMB.LOGIC.74XX04" : x=New Vmb74xxGateIc(moduleId,6,"NOT")
            Case "VMB.LOGIC.74XX08" : x=New Vmb74xxGateIc(moduleId,4,"AND")
            Case "VMB.LOGIC.74XX32" : x=New Vmb74xxGateIc(moduleId,4,"OR")
            Case "VMB.LOGIC.74XX86" : x=New Vmb74xxGateIc(moduleId,4,"XOR")
            Case "VMB.LOGIC.74XX138" : x=New Vmb74xx138()
            Case "VMB.LOGIC.74XX74" : x=New Vmb74xx74()
            Case "VMB.LOGIC.74XX273" : x=New Vmb74xx273()
            Case "VMB.LOGIC.74XX161" : x=New Vmb74xx16x(moduleId,False)
            Case "VMB.LOGIC.74XX163" : x=New Vmb74xx16x(moduleId,True)
            Case "VMB.LOGIC.74XX393" : x=New Vmb74xx393()
            Case "VMB.LOGIC.74XX157" : x=New Vmb74xx157()
            Case "VMB.LOGIC.74XX688" : x=New Vmb74xx688()
            Case "VMB.LOGIC.74XX244" : x=New Vmb74xx244()
            Case "VMB.LOGIC.74XX245" : x=New Vmb74xx245()
            Case "VMB.LOGIC.74XX373" : x=New Vmb74xx373()
            Case "VMB.LOGIC.74XX374" : x=New Vmb74xx374()
            Case "VMB.LOGIC.74XX125", "VMB.LOGIC.74XX126", "VMB.LOGIC.74XX51", "VMB.LOGIC.74XX139", "VMB.LOGIC.74XX148", "VMB.LOGIC.74XX151", "VMB.LOGIC.74XX153", "VMB.LOGIC.74XX166", "VMB.LOGIC.74XX174", "VMB.LOGIC.74XX42", "VMB.LOGIC.74XX85", "VMB.LOGIC.74XX175", "VMB.LOGIC.74XX190", "VMB.LOGIC.74XX191", "VMB.LOGIC.74XX192", "VMB.LOGIC.74XX193", "VMB.LOGIC.74XX194", "VMB.LOGIC.74XX280", "VMB.LOGIC.74XX283", "VMB.LOGIC.74XX73", "VMB.LOGIC.74XX75", "VMB.LOGIC.74XX76", "VMB.LOGIC.74XX90", "VMB.LOGIC.74XX92", "VMB.LOGIC.74XX93", "VMB.LOGIC.74XX83A", "VMB.LOGIC.74XX95B", "VMB.LOGIC.74XX03", "VMB.LOGIC.74XX05", "VMB.LOGIC.74XX06", "VMB.LOGIC.74XX10", "VMB.LOGIC.74XX11", "VMB.LOGIC.74XX14", "VMB.LOGIC.74XX20", "VMB.LOGIC.74XX21", "VMB.LOGIC.74XX27", "VMB.LOGIC.74XX30", "VMB.LOGIC.74XX01", "VMB.LOGIC.74XX132", "VMB.LOGIC.74XX133", "VMB.LOGIC.74XX136", "VMB.LOGIC.74XX251" : x=New Vmb74xxSimpleMsi(moduleId)
            ' CP156-180 TI HC identity aliases
            Case "VMB.LOGIC.TI_SN74HC51" : x=New VmbAliasedDigitalIc(moduleId,Create("VMB.LOGIC.74XX51"))
            Case "VMB.LOGIC.TI_SN74HC73" : x=New VmbAliasedDigitalIc(moduleId,Create("VMB.LOGIC.74XX73"))
            Case "VMB.LOGIC.TI_SN74HC74" : x=New VmbAliasedDigitalIc(moduleId,Create("VMB.LOGIC.74XX74"))
            Case "VMB.LOGIC.TI_SN74HC75" : x=New VmbAliasedDigitalIc(moduleId,Create("VMB.LOGIC.74XX75"))
            Case "VMB.LOGIC.TI_SN74HC76" : x=New VmbAliasedDigitalIc(moduleId,Create("VMB.LOGIC.74XX76"))
            Case "VMB.LOGIC.TI_SN74HC90" : x=New VmbAliasedDigitalIc(moduleId,Create("VMB.LOGIC.74XX90"))
            Case "VMB.LOGIC.TI_SN74HC92" : x=New VmbAliasedDigitalIc(moduleId,Create("VMB.LOGIC.74XX92"))
            Case "VMB.LOGIC.TI_SN74HC93" : x=New VmbAliasedDigitalIc(moduleId,Create("VMB.LOGIC.74XX93"))
            Case "VMB.LOGIC.TI_SN74HC132" : x=New VmbAliasedDigitalIc(moduleId,Create("VMB.LOGIC.74XX132"))
            Case "VMB.LOGIC.TI_SN74HC133" : x=New VmbAliasedDigitalIc(moduleId,Create("VMB.LOGIC.74XX133"))
            Case "VMB.LOGIC.TI_SN74HC136" : x=New VmbAliasedDigitalIc(moduleId,Create("VMB.LOGIC.74XX136"))
            Case "VMB.LOGIC.TI_SN74HC161" : x=New VmbAliasedDigitalIc(moduleId,Create("VMB.LOGIC.74XX161"))
            Case "VMB.LOGIC.TI_SN74HC163" : x=New VmbAliasedDigitalIc(moduleId,Create("VMB.LOGIC.74XX163"))
            Case "VMB.LOGIC.TI_SN74HC166" : x=New VmbAliasedDigitalIc(moduleId,Create("VMB.LOGIC.74XX166"))
            Case "VMB.LOGIC.TI_SN74HC174" : x=New VmbAliasedDigitalIc(moduleId,Create("VMB.LOGIC.74XX174"))
            Case "VMB.LOGIC.TI_SN74HC175" : x=New VmbAliasedDigitalIc(moduleId,Create("VMB.LOGIC.74XX175"))
            Case "VMB.LOGIC.TI_SN74HC190" : x=New VmbAliasedDigitalIc(moduleId,Create("VMB.LOGIC.74XX190"))
            Case "VMB.LOGIC.TI_SN74HC191" : x=New VmbAliasedDigitalIc(moduleId,Create("VMB.LOGIC.74XX191"))
            Case "VMB.LOGIC.TI_SN74HC192" : x=New VmbAliasedDigitalIc(moduleId,Create("VMB.LOGIC.74XX192"))
            Case "VMB.LOGIC.TI_SN74HC193" : x=New VmbAliasedDigitalIc(moduleId,Create("VMB.LOGIC.74XX193"))
            Case "VMB.LOGIC.TI_SN74HC194" : x=New VmbAliasedDigitalIc(moduleId,Create("VMB.LOGIC.74XX194"))
            Case "VMB.LOGIC.TI_SN74HC251" : x=New VmbAliasedDigitalIc(moduleId,Create("VMB.LOGIC.74XX251"))
            Case "VMB.LOGIC.TI_SN74HC273" : x=New VmbAliasedDigitalIc(moduleId,Create("VMB.LOGIC.74XX273"))
            Case "VMB.LOGIC.TI_SN74HC03" : x=New VmbAliasedDigitalIc(moduleId,Create("VMB.LOGIC.74XX03"))
            Case "VMB.LOGIC.TI_SN74HC05" : x=New VmbAliasedDigitalIc(moduleId,Create("VMB.LOGIC.74XX05"))
            Case "VMB.LOGIC.TI_SN74HC373" : x=New VmbAliasedDigitalIc(moduleId,Create("VMB.LOGIC.74XX373"))
            Case "VMB.LOGIC.TI_SN74HC374" : x=New VmbAliasedDigitalIc(moduleId,Create("VMB.LOGIC.74XX374"))
            Case "VMB.LOGIC.TI_SN74HC393" : x=New VmbAliasedDigitalIc(moduleId,Create("VMB.LOGIC.74XX393"))
            Case "VMB.LOGIC.TI_SN74HC688" : x=New VmbAliasedDigitalIc(moduleId,Create("VMB.LOGIC.74XX688"))
            Case "VMB.LOGIC.TI_CD74HC280" : x=New VmbAliasedDigitalIc(moduleId,Create("VMB.LOGIC.74XX280"))
            Case "VMB.LOGIC.TI_CD74HC283" : x=New VmbAliasedDigitalIc(moduleId,Create("VMB.LOGIC.74XX283"))
        End Select
        If x Is Nothing AndAlso throwIfUnknown Then Throw New NotSupportedException("Unknown digital IC module '" & moduleId & "'.")
        Return x
    End Function
End Class

' CP52 manual-backed simple MSI logic.
Public NotInheritable Class Vmb74xxSimpleMsi
    Inherits VmbDigitalIcBase
    Private ReadOnly _id As String
    Private ReadOnly _q(7) As Boolean
    Private _lastClock As Boolean
    Private _counterValue As Integer
    Private _lastUp As Boolean = True
    Private _lastDown As Boolean = True
    Private ReadOnly _lastClk2(1) As Boolean
    Private _qa As Boolean
    Private _section As Integer
    Public Sub New(moduleId As String)
        _id=moduleId
    End Sub
    Public Overrides ReadOnly Property ModuleId As String
        Get
            Return _id
        End Get
    End Property
    Public Overrides Sub Reset()
        Array.Clear(_q,0,_q.Length) : _lastClock=False : _counterValue=0
    End Sub
    Public Overrides Function Evaluate(i As IReadOnlyDictionary(Of String, Nullable(Of Boolean))) As IReadOnlyDictionary(Of String, Nullable(Of Boolean))
        Dim o As New Dictionary(Of String, Nullable(Of Boolean))(StringComparer.OrdinalIgnoreCase)
        Select Case _id
            Case "VMB.LOGIC.74XX73", "VMB.LOGIC.74XX76"
                Dim hasPreset=(_id="VMB.LOGIC.74XX76")
                For ch=0 To 1
                    Dim n=(ch+1).ToString(), clk=Pin(i,n & "CLK").GetValueOrDefault(False)
                    Dim clr=Pin(i,n & "CLR_N").GetValueOrDefault(True), pre=If(hasPreset,Pin(i,n & "PRE_N").GetValueOrDefault(True),True)
                    If hasPreset AndAlso Not pre AndAlso Not clr Then o(n & "Q")=True:o(n & "Q_N")=True:_lastClk2(ch)=clk:Continue For
                    If Not clr Then
                        _q(ch)=False
                    ElseIf hasPreset AndAlso Not pre Then
                        _q(ch)=True
                    ElseIf _lastClk2(ch) AndAlso Not clk Then
                        Dim j=Pin(i,n & "J").GetValueOrDefault(False), k=Pin(i,n & "K").GetValueOrDefault(False)
                        If j AndAlso Not k Then
                            _q(ch)=True
                        ElseIf k AndAlso Not j Then
                            _q(ch)=False
                        ElseIf j AndAlso k Then
                            _q(ch)=Not _q(ch)
                        End If
                    End If
                    _lastClk2(ch)=clk:o(n & "Q")=_q(ch):o(n & "Q_N")=Not _q(ch)
                Next
            Case "VMB.LOGIC.74XX75"
                For ch=0 To 3
                    Dim en=Pin(i,If(ch<2,"C1","C2")).GetValueOrDefault(False)
                    If en Then _q(ch)=Pin(i,"D" & ch.ToString()).GetValueOrDefault(False)
                    o("Q" & ch.ToString())=_q(ch):o("Q" & ch.ToString() & "_N")=Not _q(ch)
                Next
            Case "VMB.LOGIC.74XX90", "VMB.LOGIC.74XX92", "VMB.LOGIC.74XX93"
                Dim kind=If(_id.EndsWith("90"),90,If(_id.EndsWith("92"),92,93))
                Dim held=False
                If Pin(i,"R0_1").GetValueOrDefault(False) AndAlso Pin(i,"R0_2").GetValueOrDefault(False) Then _qa=False:_section=0:held=True
                If kind=90 AndAlso Pin(i,"R9_1").GetValueOrDefault(False) AndAlso Pin(i,"R9_2").GetValueOrDefault(False) Then _qa=True:_section=4:held=True
                Dim ca=Pin(i,"CKA").GetValueOrDefault(False), cb=Pin(i,"CKB").GetValueOrDefault(False)
                If Not held AndAlso _lastClk2(0) AndAlso Not ca Then _qa=Not _qa
                If Not held AndAlso _lastClk2(1) AndAlso Not cb Then _section=(_section+1) Mod If(kind=90,5,If(kind=92,6,8))
                _lastClk2(0)=ca:_lastClk2(1)=cb:o("QA")=_qa:o("QB")=((_section And 1)<>0):o("QC")=((_section And 2)<>0):o("QD")=((_section And 4)<>0)
            Case "VMB.LOGIC.74XX83A"
                Dim av=0,bv=0:For n=0 To 3:If Pin(i,"A" & n.ToString()).GetValueOrDefault(False) Then av=av Or (1<<n)
                    If Pin(i,"B" & n.ToString()).GetValueOrDefault(False) Then bv=bv Or (1<<n)
                Next:Dim total=av+bv+If(Pin(i,"C0").GetValueOrDefault(False),1,0):For n=0 To 3:o("S" & n.ToString())=((total And (1<<n))<>0):Next:o("C4")=((total And 16)<>0)
            Case "VMB.LOGIC.74XX95B"
                Dim c1=Pin(i,"CLK1").GetValueOrDefault(False), c2=Pin(i,"CLK2").GetValueOrDefault(False), mode=Pin(i,"MODE").GetValueOrDefault(False)
                If mode AndAlso _lastClk2(1) AndAlso Not c2 Then For n=0 To 3:_q(n)=Pin(i,Chr(Asc("A"c)+n)).GetValueOrDefault(False):Next
                If Not mode AndAlso _lastClk2(0) AndAlso Not c1 Then For n=3 To 1 Step -1:_q(n)=_q(n-1):Next:_q(0)=Pin(i,"SER").GetValueOrDefault(False)
                _lastClk2(0)=c1:_lastClk2(1)=c2:For n=0 To 3:o("Q" & Chr(Asc("A"c)+n))=_q(n):Next
            Case "VMB.LOGIC.74XX42"
                Dim v=If(Pin(i,"A").GetValueOrDefault(False),1,0) Or If(Pin(i,"B").GetValueOrDefault(False),2,0) Or If(Pin(i,"C").GetValueOrDefault(False),4,0) Or If(Pin(i,"D").GetValueOrDefault(False),8,0)
                For n=0 To 9:o("Y" & n.ToString() & "_N")=Not(v<10 AndAlso n=v):Next
            Case "VMB.LOGIC.74XX85"
                Dim av=0,bv=0
                For n=0 To 3:If Pin(i,"A" & n.ToString()).GetValueOrDefault(False) Then av=av Or (1<<n)
                    If Pin(i,"B" & n.ToString()).GetValueOrDefault(False) Then bv=bv Or (1<<n)
                Next
                If av>bv Then:o("A_GT_B")=True:o("A_EQ_B")=False:o("A_LT_B")=False
                ElseIf av<bv Then:o("A_GT_B")=False:o("A_EQ_B")=False:o("A_LT_B")=True
                Else:o("A_GT_B")=Pin(i,"I_GT").GetValueOrDefault(False):o("A_EQ_B")=Pin(i,"I_EQ").GetValueOrDefault(False):o("A_LT_B")=Pin(i,"I_LT").GetValueOrDefault(False):End If
            Case "VMB.LOGIC.74XX175"
                Dim clk=Pin(i,"CLK").GetValueOrDefault(False)
                If Not Pin(i,"CLR_N").GetValueOrDefault(True) Then
                    Array.Clear(_q,0,_q.Length)
                ElseIf Not _lastClock AndAlso clk Then
                    For n=0 To 3
                    _q(n)=Pin(i,"D" & n.ToString()).GetValueOrDefault(False)
                    Next
                End If
                _lastClock=clk:For n=0 To 3:o("Q" & n.ToString())=_q(n):o("Q" & n.ToString() & "_N")=Not _q(n):Next
            Case "VMB.LOGIC.74XX190", "VMB.LOGIC.74XX191"
                Dim isBcd=(_id="VMB.LOGIC.74XX190"), modulus=If(isBcd,10,16), clk=Pin(i,"CLK").GetValueOrDefault(False)
                If Not Pin(i,"LOAD_N").GetValueOrDefault(True) Then
                    _counterValue=0:For n=0 To 3:If Pin(i,Chr(Asc("A"c)+n)).GetValueOrDefault(False) Then _counterValue=_counterValue Or (1<<n)
                    Next:_counterValue=_counterValue Mod modulus
                ElseIf Not _lastClock AndAlso clk AndAlso Not Pin(i,"CTEN_N").GetValueOrDefault(True) Then
                    If Pin(i,"D_U").GetValueOrDefault(False) Then _counterValue=(_counterValue-1+modulus) Mod modulus Else _counterValue=(_counterValue+1) Mod modulus
                End If
                _lastClock=clk:For n=0 To 3:o("Q" & Chr(Asc("A"c)+n))=((_counterValue And (1<<n))<>0):Next
                Dim terminal=If(Pin(i,"D_U").GetValueOrDefault(False),_counterValue=0,_counterValue=modulus-1)
                o("MAX_MIN")=terminal:o("RCO_N")=Not(terminal AndAlso Not clk AndAlso Not Pin(i,"CTEN_N").GetValueOrDefault(True))
            Case "VMB.LOGIC.74XX192", "VMB.LOGIC.74XX193"
                Dim isBcd=(_id="VMB.LOGIC.74XX192"), modulus=If(isBcd,10,16)
                Dim up=Pin(i,"UP").GetValueOrDefault(True), down=Pin(i,"DOWN").GetValueOrDefault(True)
                If Pin(i,"CLR").GetValueOrDefault(False) Then
                    _counterValue=0
                ElseIf Not Pin(i,"LOAD_N").GetValueOrDefault(True) Then
                    _counterValue=0:For n=0 To 3:If Pin(i,Chr(Asc("A"c)+n)).GetValueOrDefault(False) Then _counterValue=_counterValue Or (1<<n)
                    Next:_counterValue=_counterValue Mod modulus
                Else
                    If Not _lastUp AndAlso up AndAlso down Then _counterValue=(_counterValue+1) Mod modulus
                    If Not _lastDown AndAlso down AndAlso up Then _counterValue=(_counterValue-1+modulus) Mod modulus
                End If
                _lastUp=up:_lastDown=down
                For n=0 To 3:o("Q" & Chr(Asc("A"c)+n))=((_counterValue And (1<<n))<>0):Next
                o("CARRY_N")=Not(_counterValue=modulus-1 AndAlso Not up):o("BORROW_N")=Not(_counterValue=0 AndAlso Not down)
            Case "VMB.LOGIC.74XX194"
                Dim clk=Pin(i,"CLK").GetValueOrDefault(False)
                If Not Pin(i,"CLR_N").GetValueOrDefault(True) Then
                    For n=0 To 3:_q(n)=False:Next
                ElseIf Not _lastClock AndAlso clk Then
                    Dim mode=If(Pin(i,"S0").GetValueOrDefault(False),1,0) Or If(Pin(i,"S1").GetValueOrDefault(False),2,0)
                    If mode=1 Then
                        For n=3 To 1 Step -1:_q(n)=_q(n-1):Next:_q(0)=Pin(i,"SR").GetValueOrDefault(False)
                    ElseIf mode=2 Then
                        For n=0 To 2:_q(n)=_q(n+1):Next:_q(3)=Pin(i,"SL").GetValueOrDefault(False)
                    ElseIf mode=3 Then
                        For n=0 To 3:_q(n)=Pin(i,Chr(Asc("A"c)+n)).GetValueOrDefault(False):Next
                    End If
                End If
                _lastClock=clk:For n=0 To 3:o("Q" & Chr(Asc("A"c)+n))=_q(n):Next
            Case "VMB.LOGIC.74XX280"
                Dim parity As Boolean=False:For n=0 To 8:parity=parity Xor Pin(i,"I" & n.ToString()).GetValueOrDefault(False):Next:o("EVEN")=parity:o("ODD")=Not parity
            Case "VMB.LOGIC.74XX283"
                Dim av=0,bv=0:For n=0 To 3:If Pin(i,"A" & n.ToString()).GetValueOrDefault(False) Then av=av Or (1<<n)
                    If Pin(i,"B" & n.ToString()).GetValueOrDefault(False) Then bv=bv Or (1<<n)
                Next:Dim total=av+bv+If(Pin(i,"C0").GetValueOrDefault(False),1,0):For n=0 To 3:o("S" & n.ToString())=((total And (1<<n))<>0):Next:o("C4")=((total And 16)<>0)
            Case "VMB.LOGIC.74XX125", "VMB.LOGIC.74XX126"
                Dim lowEnable=(_id="VMB.LOGIC.74XX125")
                For n=1 To 4
                    Dim g=Pin(i,n.ToString() & If(lowEnable,"G_N","G")).GetValueOrDefault(False)
                    Dim en=If(lowEnable,Not g,g)
                    o(n.ToString() & "Y")=If(en,Pin(i,n.ToString() & "A"),Z())
                Next
            Case "VMB.LOGIC.74XX51"
                Dim a=Pin(i,"1A").GetValueOrDefault(False) AndAlso Pin(i,"1B").GetValueOrDefault(False) AndAlso Pin(i,"1C").GetValueOrDefault(False)
                Dim b=Pin(i,"1D").GetValueOrDefault(False) AndAlso Pin(i,"1E").GetValueOrDefault(False) AndAlso Pin(i,"1F").GetValueOrDefault(False):o("1Y")=Not(a OrElse b)
                a=Pin(i,"2A").GetValueOrDefault(False) AndAlso Pin(i,"2B").GetValueOrDefault(False)
                b=Pin(i,"2C").GetValueOrDefault(False) AndAlso Pin(i,"2D").GetValueOrDefault(False):o("2Y")=Not(a OrElse b)
            Case "VMB.LOGIC.74XX139"
                For s=1 To 2
                    Dim en=Not Pin(i,s.ToString() & "G_N").GetValueOrDefault(True)
                    Dim sel=(If(Pin(i,s.ToString() & "A").GetValueOrDefault(False),1,0)) Or (If(Pin(i,s.ToString() & "B").GetValueOrDefault(False),2,0))
                    For n=0 To 3:o(s.ToString() & "Y" & n.ToString() & "_N")=Not(en AndAlso n=sel):Next
                Next
            Case "VMB.LOGIC.74XX151"
                Dim en=Not Pin(i,"G_N").GetValueOrDefault(True)
                Dim sel=If(Pin(i,"A").GetValueOrDefault(False),1,0) Or If(Pin(i,"B").GetValueOrDefault(False),2,0) Or If(Pin(i,"C").GetValueOrDefault(False),4,0)
                Dim y As Boolean=If(en,Pin(i,"D" & sel.ToString()).GetValueOrDefault(False),False)
                o("Y")=y:o("W")=Not y
            Case "VMB.LOGIC.74XX153"
                Dim sel=If(Pin(i,"A").GetValueOrDefault(False),1,0) Or If(Pin(i,"B").GetValueOrDefault(False),2,0)
                For s=1 To 2
                    Dim en=Not Pin(i,s.ToString() & "G_N").GetValueOrDefault(True)
                    o(s.ToString() & "Y")=If(en,Pin(i,s.ToString() & "C" & sel.ToString()).GetValueOrDefault(False),False)
                Next
            Case "VMB.LOGIC.74XX148"
                Dim en=Not Pin(i,"EI_N").GetValueOrDefault(True)
                Dim winner As Integer=-1
                If en Then
                    For n=0 To 7
                        If Not Pin(i,"I" & n.ToString() & "_N").GetValueOrDefault(True) Then winner=n
                    Next
                End If
                If winner<0 Then
                    o("A0_N")=True:o("A1_N")=True:o("A2_N")=True:o("GS_N")=True:o("EO_N")=Not en
                Else
                    o("A0_N")=(winner And 1)=0:o("A1_N")=(winner And 2)=0:o("A2_N")=(winner And 4)=0:o("GS_N")=False:o("EO_N")=True
                End If
            Case "VMB.LOGIC.74XX174"
                Dim clk=Pin(i,"CLK").GetValueOrDefault(False)
                If Not Pin(i,"CLR_N").GetValueOrDefault(True) Then
                    Array.Clear(_q,0,_q.Length)
                ElseIf Not _lastClock AndAlso clk Then
                    For n=0 To 5
                    _q(n)=Pin(i,"D" & n.ToString()).GetValueOrDefault(False)
                    Next
                End If
                _lastClock=clk:For n=0 To 5:o("Q" & n.ToString())=_q(n):Next
            Case "VMB.LOGIC.74XX03"
                For n=1 To 4
                    Dim a=Pin(i,n.ToString() & "A"), b=Pin(i,n.ToString() & "B")
                    o(n.ToString() & "Y")=If(a.HasValue AndAlso b.HasValue AndAlso a.Value AndAlso b.Value,CType(False,Nullable(Of Boolean)),Z())
                Next
            Case "VMB.LOGIC.74XX05", "VMB.LOGIC.74XX06"
                For n=1 To 6
                    Dim a=Pin(i,n.ToString() & "A")
                    o(n.ToString() & "Y")=If(a.HasValue AndAlso a.Value,CType(False,Nullable(Of Boolean)),Z())
                Next
            Case "VMB.LOGIC.74XX14"
                For n=1 To 6
                    Dim a=Pin(i,n.ToString() & "A")
                    o(n.ToString() & "Y")=If(a.HasValue,CType(Not a.Value,Nullable(Of Boolean)),Z())
                Next
            Case "VMB.LOGIC.74XX10", "VMB.LOGIC.74XX11", "VMB.LOGIC.74XX20", "VMB.LOGIC.74XX21", "VMB.LOGIC.74XX27", "VMB.LOGIC.74XX30"
                Dim gates=If(_id.EndsWith("30"),1,If(_id.EndsWith("20") OrElse _id.EndsWith("21"),2,3))
                Dim width=If(_id.EndsWith("30"),8,If(_id.EndsWith("20") OrElse _id.EndsWith("21"),4,3))
                Dim invert=(_id.EndsWith("10") OrElse _id.EndsWith("20") OrElse _id.EndsWith("27") OrElse _id.EndsWith("30"))
                Dim isNor=_id.EndsWith("27")
                For g=1 To gates
                    Dim known=True, v=If(isNor,False,True)
                    For n=1 To width
                        Dim x=Pin(i,g.ToString() & Chr(Asc("A"c)+n-1))
                        If Not x.HasValue Then
                            known=False
                        ElseIf isNor Then
                            v=v OrElse x.Value
                        Else
                            v=v AndAlso x.Value
                        End If
                    Next
                    o(g.ToString() & "Y")=If(known,CType(If(invert,Not v,v),Nullable(Of Boolean)),Z())
                Next
            Case "VMB.LOGIC.74XX01"
                For n=1 To 4
                    Dim a=Pin(i,n.ToString() & "A"), b=Pin(i,n.ToString() & "B")
                    o(n.ToString() & "Y")=If(a.HasValue AndAlso b.HasValue AndAlso a.Value AndAlso b.Value,CType(False,Nullable(Of Boolean)),Z())
                Next
            Case "VMB.LOGIC.74XX132"
                For n=1 To 4
                    Dim a=Pin(i,n.ToString() & "A"), b=Pin(i,n.ToString() & "B")
                    o(n.ToString() & "Y")=If(a.HasValue AndAlso b.HasValue,CType(Not(a.Value AndAlso b.Value),Nullable(Of Boolean)),Z())
                Next
            Case "VMB.LOGIC.74XX133"
                Dim known=True, allHigh=True
                For n=0 To 12
                    Dim x=Pin(i,Chr(Asc("A"c)+n)):If Not x.HasValue Then known=False Else allHigh=allHigh AndAlso x.Value
                Next:o("Y")=If(known,CType(Not allHigh,Nullable(Of Boolean)),Z())
            Case "VMB.LOGIC.74XX136"
                For n=1 To 4
                    Dim a=Pin(i,n.ToString() & "A"), b=Pin(i,n.ToString() & "B")
                    o(n.ToString() & "Y")=If(a.HasValue AndAlso b.HasValue AndAlso Not(a.Value Xor b.Value),CType(False,Nullable(Of Boolean)),Z())
                Next
            Case "VMB.LOGIC.74XX251"
                If Pin(i,"G_N").GetValueOrDefault(True) Then
                    o("Y")=Z():o("W")=Z()
                Else
                    Dim sel=If(Pin(i,"A").GetValueOrDefault(False),1,0) Or If(Pin(i,"B").GetValueOrDefault(False),2,0) Or If(Pin(i,"C").GetValueOrDefault(False),4,0)
                    Dim y=Pin(i,"D" & sel.ToString()):o("Y")=y:o("W")=If(y.HasValue,CType(Not y.Value,Nullable(Of Boolean)),Z())
                End If
            Case "VMB.LOGIC.74XX166"
                Dim clk=Pin(i,"CLK").GetValueOrDefault(False), inh=Pin(i,"CLK_INH").GetValueOrDefault(False)
                If Not Pin(i,"CLR_N").GetValueOrDefault(True) Then
                    Array.Clear(_q,0,_q.Length)
                ElseIf Not _lastClock AndAlso clk AndAlso Not inh Then
                    If Not Pin(i,"SH_LD_N").GetValueOrDefault(True) Then
                        For n=0 To 7
                        _q(n)=Pin(i,Chr(Asc("A"c)+n)).GetValueOrDefault(False)
                        Next
                    Else
                        For n=7 To 1 Step -1
                        _q(n)=_q(n-1)
                        Next
                        _q(0)=Pin(i,"SER").GetValueOrDefault(False)
                    End If
                End If
                _lastClock=clk:o("QH")=_q(7)
        End Select
        Return o
    End Function
End Class

' CP60 timing-aware digital contract. Absolute nanoseconds are supplied by the board/machine scheduler.
' Rext/Cext -> pulse width belongs to the electrical timing layer; this class consumes the resolved interval.
Public Interface IVmbTimedDigitalIc
    ReadOnly Property ModuleId As String
    Sub Reset()
    Function EvaluateAt(nowNanoseconds As Long, inputs As IReadOnlyDictionary(Of String, Nullable(Of Boolean))) As IReadOnlyDictionary(Of String, Nullable(Of Boolean))
End Interface

Public NotInheritable Class Vmb74xxMonostable
    Implements IVmbTimedDigitalIc
    Private ReadOnly _id As String
    Private ReadOnly _retriggerable As Boolean
    Private ReadOnly _pulseWidthNs As Long
    Private ReadOnly _activeUntil(1) As Long
    Private ReadOnly _lastA(1) As Boolean
    Private ReadOnly _lastB(1) As Boolean
    Public Sub New(moduleId As String, pulseWidthNanoseconds As Long)
        If pulseWidthNanoseconds <= 0 Then Throw New ArgumentOutOfRangeException(NameOf(pulseWidthNanoseconds))
        _id=moduleId : _retriggerable=moduleId.EndsWith("123",StringComparison.OrdinalIgnoreCase) : _pulseWidthNs=pulseWidthNanoseconds
        Reset()
    End Sub
    Public ReadOnly Property ModuleId As String Implements IVmbTimedDigitalIc.ModuleId
        Get
            Return _id
        End Get
    End Property
    Public Sub Reset() Implements IVmbTimedDigitalIc.Reset
        For n=0 To 1 : _activeUntil(n)=0 : _lastA(n)=True : _lastB(n)=False : Next
    End Sub
    Public Function EvaluateAt(nowNs As Long, i As IReadOnlyDictionary(Of String, Nullable(Of Boolean))) As IReadOnlyDictionary(Of String, Nullable(Of Boolean)) Implements IVmbTimedDigitalIc.EvaluateAt
        Dim o As New Dictionary(Of String, Nullable(Of Boolean))(StringComparer.OrdinalIgnoreCase)
        For ch=0 To 1
            Dim n=(ch+1).ToString(), a=True, b=False, clr=True, v As Nullable(Of Boolean)=Nothing
            If i IsNot Nothing AndAlso i.TryGetValue(n & "A",v) AndAlso v.HasValue Then a=v.Value
            If i IsNot Nothing AndAlso i.TryGetValue(n & "B",v) AndAlso v.HasValue Then b=v.Value
            If i IsNot Nothing AndAlso i.TryGetValue(n & "CLR_N",v) AndAlso v.HasValue Then clr=v.Value
            If Not clr Then
                _activeUntil(ch)=nowNs
            Else
                Dim trigger=(_lastA(ch) AndAlso Not a AndAlso b) OrElse (Not _lastB(ch) AndAlso b AndAlso Not a)
                If trigger AndAlso (_retriggerable OrElse nowNs>=_activeUntil(ch)) Then _activeUntil(ch)=nowNs+_pulseWidthNs
            End If
            _lastA(ch)=a : _lastB(ch)=b
            Dim q=nowNs<_activeUntil(ch) : o(n & "Q")=q : o(n & "Q_N")=Not q
        Next
        Return o
    End Function
End Class

Public NotInheritable Class VmbTimedDigitalIcRegistry
    Private Sub New()
    End Sub
    Public Shared Function Create(moduleId As String, pulseWidthNanoseconds As Long) As IVmbTimedDigitalIc
        Select Case moduleId
            Case "VMB.LOGIC.74XX123", "VMB.LOGIC.74XX221" : Return New Vmb74xxMonostable(moduleId,pulseWidthNanoseconds)

            Case "VMB.LOGIC.TI_SN74HC123", "VMB.LOGIC.TI_SN74HC221"
                Return New Vmb74xxMonostable(moduleId,pulseWidthNanoseconds)
            Case Else : Throw New NotSupportedException("Unknown timed digital IC module '" & moduleId & "'.")
        End Select
    End Function
End Class
