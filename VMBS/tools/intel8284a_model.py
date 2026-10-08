"""CP66 Intel 8284A digital timing model.
Manual-backed digital projection: source selection/dividers, CSYNC, RESET and READY synchronizers.
Analog crystal oscillator behavior and metastability are electrical-domain concerns.
"""
class Intel8284AClockCore:
    def __init__(self):
        self.phase=0; self.clk=0; self.pclk=0; self.osc=0; self.reset=0; self.ready=0
        self._pclk_div=0; self._ready_stage=0; self._res=0
        self.rdy1=0; self.rdy2=0; self.aen1_n=1; self.aen2_n=1; self.async_high=1
        self.fc_external=False; self.csync=False
    def set_ready_inputs(self,rdy1,aen1_n,rdy2,aen2_n,async_high):
        self.rdy1=1 if rdy1 else 0; self.rdy2=1 if rdy2 else 0
        self.aen1_n=1 if aen1_n else 0; self.aen2_n=1 if aen2_n else 0
        self.async_high=1 if async_high else 0
    def set_controls(self,fc_external=False,csync=False,res=False):
        self.fc_external=bool(fc_external); self.csync=bool(csync); self._res=1 if res else 0
    def _qualified_ready(self):
        return (self.rdy1 and not self.aen1_n) or (self.rdy2 and not self.aen2_n)
    def _selected_source_tick(self):
        old=self.clk
        if self.csync:
            self.clk=1; self.pclk=1; return
        self.phase=(self.phase+1)%3
        self.clk=1 if self.phase==0 else 0
        rising=(not old and self.clk); falling=(old and not self.clk)
        if rising:
            # ASYNC low selects the documented two-stage path: first stage captures here.
            if not self.async_high:self._ready_stage=1 if self._qualified_ready() else 0
        if falling:
            self._pclk_div ^= 1; self.pclk=self._pclk_div
            self.reset=self._res
            # READY reaches the processor on a CLK falling edge. In two-stage mode use
            # the value captured by the preceding positive edge; in one-stage mode
            # sample the qualified RDY path directly at this edge.
            self.ready=self._ready_stage if not self.async_high else (1 if self._qualified_ready() else 0)
    def crystal_tick(self,level=1):
        # OSC is the buffered crystal oscillator output and exists independently of F/C selection.
        self.osc=1 if level else 0
        if not self.fc_external:self._selected_source_tick()
        return self.outputs()
    def external_tick(self):
        if self.fc_external:self._selected_source_tick()
        return self.outputs()
    def source_tick(self,res):
        # Backward-compatible CP65 helper: one tick of whichever source is selected.
        self._res=1 if res else 0; self._selected_source_tick(); return self.clk,self.pclk,self.reset
    def outputs(self):return self.clk,self.pclk,self.osc,self.reset,self.ready
