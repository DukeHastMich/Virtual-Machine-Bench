Imports System

' Intel 8284A manual-backed digital timing projection (CP66).
' Models source selection/dividers, CSYNC, RESET and READY synchronization.
' Crystal analog behavior, threshold/metastability and propagation tolerances stay in the electrical model.
Public NotInheritable Class VmbIntel8284AClockCore
    Public Const ModuleId As String = "VMB.CLOCK.INTEL_8284A"
    Private _phase As Integer, _clk As Boolean, _pclk As Boolean, _osc As Boolean
    Private _pclkDiv As Boolean, _reset As Boolean, _ready As Boolean, _readyStage As Boolean
    Private _res As Boolean, _rdy1 As Boolean, _rdy2 As Boolean, _aen1n As Boolean = True, _aen2n As Boolean = True
    Private _asyncHigh As Boolean = True, _fcExternal As Boolean, _csync As Boolean
    Public ReadOnly Property CLK As Boolean : Get : Return _clk : End Get : End Property
    Public ReadOnly Property PCLK As Boolean : Get : Return _pclk : End Get : End Property
    Public ReadOnly Property OSC As Boolean : Get : Return _osc : End Get : End Property
    Public ReadOnly Property RESET As Boolean : Get : Return _reset : End Get : End Property
    Public ReadOnly Property READY As Boolean : Get : Return _ready : End Get : End Property
    Public Sub SetReadyInputs(rdy1 As Boolean, aen1_n As Boolean, rdy2 As Boolean, aen2_n As Boolean, asyncHigh As Boolean)
        _rdy1=rdy1 : _aen1n=aen1_n : _rdy2=rdy2 : _aen2n=aen2_n : _asyncHigh=asyncHigh
    End Sub
    Public Sub SetControls(fcExternal As Boolean, csync As Boolean, resInput As Boolean)
        _fcExternal=fcExternal : _csync=csync : _res=resInput
    End Sub
    Private Function QualifiedReady() As Boolean
        Return (_rdy1 AndAlso Not _aen1n) OrElse (_rdy2 AndAlso Not _aen2n)
    End Function
    Private Sub SelectedSourceTick()
        Dim oldClk=_clk
        If _csync Then _clk=True : _pclk=True : Return
        _phase=(_phase+1) Mod 3 : _clk=(_phase=0)
        Dim rising=(Not oldClk AndAlso _clk), falling=(oldClk AndAlso Not _clk)
        If rising AndAlso Not _asyncHigh Then _readyStage=QualifiedReady()
        If falling Then
            _pclkDiv=Not _pclkDiv : _pclk=_pclkDiv
            _reset=_res
            If _asyncHigh Then _ready=QualifiedReady() Else _ready=_readyStage
        End If
    End Sub
    Public Sub CrystalTick(level As Boolean)
        _osc=level
        If Not _fcExternal Then SelectedSourceTick()
    End Sub
    Public Sub ExternalTick()
        If _fcExternal Then SelectedSourceTick()
    End Sub
    Public Sub SourceTick(resInput As Boolean)
        _res=resInput : SelectedSourceTick()
    End Sub
End Class
