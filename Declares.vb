Imports System.IO

Module Declares
    'Hardware
    Public VrMem(15, 65535) As Byte 'System Ram
    Public SystemBus As New HardwareBus
    ' CROMWELL PCB REFIT PHASE 2 BRICK 8C - shared motherboard bridge.
    Public MotherboardMemory As New NeatMemoryController286 With {
        .LegacyMirror = VrMem
    }
    Public MotherboardBridge As New NeatMotherboardBridge286(SystemBus, MotherboardMemory)
    Public CpuBus As New CpuLocalBus286(MotherboardBridge)
    Public CPU0 As New Processor286 With {
        .PortBus = CpuBus,
        .MemoryController = MotherboardMemory
    }
    Public MachineClock As New MachineClock286
Public FrontPanel As New FrontPanelState
    Public MasterPic As New Pic8259
    Public SlavePic As New Pic8259(&HA0US, &HA1US, &H70)
    Public DmaController As New Dma8237(
        Function(channelInBed As Integer, addressInBed As UInteger) As Byte
            Dim masterInBed As AtBusMaster286 =
                If(channelInBed >= 5, AtBusMaster286.Dma16, AtBusMaster286.Dma8)
            Return MotherboardBridge.ReadMemoryByte(addressInBed, masterInBed)
        End Function,
        Sub(channelInBed As Integer, addressInBed As UInteger, valueInBed As Byte)
            Dim masterInBed As AtBusMaster286 =
                If(channelInBed >= 5, AtBusMaster286.Dma16, AtBusMaster286.Dma8)
            MotherboardBridge.WriteMemoryByte(addressInBed, valueInBed, masterInBed)
        End Sub,
        Function(channelInBed As Integer, addressInBed As UInteger) As UInt16
            Dim masterInBed As AtBusMaster286 =
                If(channelInBed >= 5, AtBusMaster286.Dma16, AtBusMaster286.Dma8)
            Return MotherboardBridge.ReadMemoryWord(addressInBed, masterInBed)
        End Function,
        Sub(channelInBed As Integer, addressInBed As UInteger, valueInBed As UInt16)
            Dim masterInBed As AtBusMaster286 =
                If(channelInBed >= 5, AtBusMaster286.Dma16, AtBusMaster286.Dma8)
            MotherboardBridge.WriteMemoryWord(addressInBed, valueInBed, masterInBed)
        End Sub)
    Public SystemTimer As New Pit8253(MasterPic)
    Public AtKeyboard As New AtKeyboard101
    Public KeyboardController As New KeyboardController8042(MasterPic, AtKeyboard)
    Public RealTimeClock As New CmosRtc(SlavePic, Path.Combine(AppContext.BaseDirectory, "Firmware", "cmos.nvram"))
    Public SystemControl As New AtSystemControlPorts(SystemTimer)
    ' CROMWELL PC SPEAKER BRICK 1 - downstream of port 61h and PIT channel 2
    Public PcSpeaker As New PcSpeakerDevice(SystemTimer, SystemControl)
    Public NmiGate As New AtNmiGate
    Public Chipset As New NeatCs8221Chipset(Function() MachineClock.CpuClockHz)
    Public VideoCard As New DiamondStealthPro928
    Public FloppyController As New FloppyController765(MasterPic, DmaController)
    Public IdeController As New IdeController(SlavePic)
    Public Com1 As New Uart16550A(&H3F8US, 4, MasterPic)
    Public Com2 As New Uart16550A(&H2F8US, 3, MasterPic)
    Public Lpt1 As New ParallelPortSpp(&H378US, 7, MasterPic)
    Public Lpt2 As New ParallelPortSpp(&H278US, 5, MasterPic)
    ' Physical peripherals beyond the Centronics connectors.  The PC-side LPT
    ' adapters above remain ordinary SPP hardware; these printers only see cable
    ' bytes/control lines and render their marked paper into host files.
    Public Lpt1Printer As New EpsonFxVirtualPrinter("LPT1", Path.Combine(AppContext.BaseDirectory, "Printouts"))
    Public Lpt2Printer As New EpsonFxVirtualPrinter("LPT2", Path.Combine(AppContext.BaseDirectory, "Printouts"))

    ' CROMWELL ISA EXPANSION BRICK - period expansion cards on the same AT bus.
    ' IRQ10/11 are slave-8259 inputs 2/3, keeping the existing master IRQ lines
    ' available to COM/LPT/FDC.  Sound DMA stays on the real 8237 path.
    Public SoundBlaster16 As New SoundBlaster16Isa(&H220US, 10, 1, 5, MasterPic, SlavePic, DmaController)
    Public Ne2000 As New Ne2000Isa(&H300US, 11, MasterPic, SlavePic)
    Public Com1DiagnosticPeripheral As New DiagnosticSerialPeripheral()
    Public Com2DiagnosticPeripheral As New DiagnosticSerialPeripheral()
    Public SerialMouse As New MicrosoftSerialMouse()
    Public IsaMemoryHole As New AtIsaMemoryHole
    Private HardwareInitialized As Boolean
    Private KeyboardA20Enabled As Boolean
    Private ChipsetForcesA20Low As Boolean

    Public Sub InitializeHardware()
        If HardwareInitialized Then Return

        ' Registration order is physical decode priority.  The NEAT memory
        ' controller gets first chance at enabled shadow segments; real ISA
        ' devices follow; the open-bus terminator is deliberately last.
        SystemBus.Register(MasterPic)
        SystemBus.Register(SlavePic)
        SystemBus.Register(DmaController)
        SystemBus.Register(SoundBlaster16)
        SystemBus.Register(Ne2000)
        ' Speaker samples the beginning-of-interval PIT state, so it MUST
        ' precede SystemTimer in the physical-time clock list.
        SystemBus.Register(PcSpeaker)
        SystemBus.Register(SystemTimer)
        SystemBus.Register(SystemControl)
        SystemBus.Register(KeyboardController)
        SystemBus.Register(RealTimeClock)
        SystemBus.Register(Chipset)
        SystemBus.Register(VideoCard)
        SystemBus.Register(FloppyController)
        SystemBus.Register(IdeController)
        SystemBus.Register(Com1)
        SystemBus.Register(Com2)
        SystemBus.Register(SerialMouse)
        SystemBus.Register(Lpt1)
        SystemBus.Register(Lpt2)
        SystemBus.Register(IsaMemoryHole)
        SystemBus.Register(NmiGate)

        CPU0.PortBus = CpuBus
        CPU0.MemoryController = MotherboardMemory
        CPU0.InterruptAcknowledge = AddressOf AcknowledgeHardwareInterrupt

        ' CROMWELL PCB REFIT PHASE 2 BRICK 8D - real motherboard bus ownership.
        MotherboardBridge.AttachCpuHoldInterface(
            Sub(assertedInBed As Boolean) CPU0.SetBusHoldRequest(assertedInBed),
            Function() CPU0.HoldAcknowledgeAsserted)
        MotherboardBridge.AttachCpuResetPulseInterface(AddressOf ResetProcessorAfterShutdown)
        ' CROMWELL PCB REFIT PHASE 2 BRICK 8F - clock-qualified READY policy.
        MotherboardBridge.AttachReadyInterface(
            Sub(waitTStatesInBed As Integer, cpuReadyCycleInBed As Boolean)
                CPU0.RegisterReadyWaitStates(waitTStatesInBed, cpuReadyCycleInBed)
            End Sub,
            Function(cycleInBed As AtReadyCycle286) As Integer
                Return Chipset.GetReadyWaitTStates(cycleInBed)
            End Function,
            Function() Chipset.TimingDiagnosticText())
        AddHandler DmaController.HoldRequestChanged,
            Sub(masterInBed As AtBusMaster286, assertedInBed As Boolean)
                MotherboardBridge.SetDmaHoldRequest(masterInBed, assertedInBed)
            End Sub
        MotherboardBridge.ResetArbitration()

        ' The slave 8259 INT pin is physically wired to master IRQ2.  Keep that
        ' electrical line live instead of synthesizing a cascade only during INTA.
        AddHandler SlavePic.InterruptOutputChanged,
            Sub(asserted As Boolean) MasterPic.SetIrqLine(2, asserted)

        ' 82C206 timer channel 1 drives the 82C211 REFREQ pin directly.
        ' Brick 8D now arbitrates that refresh request against CPU/DMA ownership;
        ' it remains independent of all programmable 8237 channels.
        AddHandler SystemTimer.RefreshRequest,
            Sub() MotherboardBridge.PerformRefreshCycle(AddressOf Chipset.RequestRefresh)

        AddHandler KeyboardController.A20Changed, AddressOf SetKeyboardA20Gate
        AddHandler KeyboardController.ResetRequested, AddressOf ResetProcessorOnly

        ' Host-only front-panel telemetry.  These event taps observe real device
        ' activity but never feed presentation state back into guest hardware.
        AddHandler KeyboardController.KeyboardTransmitActivity,
            Sub() FrontPanel.PulseKeyboardTransmit()
        AddHandler KeyboardController.KeyboardReceiveActivity,
            Sub() FrontPanel.PulseKeyboardReceive()
        AddHandler FloppyController.DriveActivity,
            Sub(driveInBed As Integer) FrontPanel.PulseFloppy(driveInBed)
        AddHandler IdeController.Activity,
            Sub() FrontPanel.PulseHardDisk(0)
        AddHandler Com1.TransmitActivity,
            Sub() FrontPanel.PulseSerialTransmit()
        AddHandler Com2.TransmitActivity,
            Sub() FrontPanel.PulseSerialTransmit()
        AddHandler Com1.ReceiveActivity,
            Sub() FrontPanel.PulseSerialReceive()
        AddHandler Com2.ReceiveActivity,
            Sub() FrontPanel.PulseSerialReceive()
        ' COM1 carries a real Microsoft-protocol serial mouse.  The diagnostic
        ' endpoint is a passive line monitor and therefore does not displace it.
        Com1.Monitor = Com1DiagnosticPeripheral
        Com1.Peripheral = SerialMouse
        Com2.Peripheral = Com2DiagnosticPeripheral
        ' The built-in diagnostic DCE endpoints present the same ready carrier
        ' lines as a powered modem.  Guest software still reaches them solely
        ' through the UART pins/registers; no BIOS or CPU host shortcut exists.
        ' A Microsoft serial mouse uses TXD/RTS/DTR for power and RXD for data.
        ' Its cable does not loop RTS->CTS or DTR->DSR, and it does not assert
        ' carrier detect.  Leaving those modem inputs high misidentifies the DCE
        ' wiring to software which probes COM1 before accepting the M response.
        Com1.SetExternalModemInputs(cts:=False, dsr:=False, ringIndicator:=False, carrierDetect:=False)
        Com2.SetExternalModemInputs(cts:=True, dsr:=True, ringIndicator:=False, carrierDetect:=True)
        ' Attach actual printers, not diagnostic byte sinks.  All guest traffic
        ' still traverses DATA/STROBE/BUSY/ACK on ParallelPortSpp first.
        Lpt1.Peripheral = Lpt1Printer
        Lpt2.Peripheral = Lpt2Printer
        AddHandler MachineClock.CpuStateSampled,
            Sub(stateByteInBed As Byte)
                FrontPanel.SetCpuStateByte(stateByteInBed)
                If (stateByteInBed And CByte(ProcessorStateByte.Shutdown)) = 0 Then
                    MotherboardBridge.ObserveProcessorRunning()
                    Return
                End If

                Dim warmOffsetInBed As UInt16 = CPU0.ReadWord(&H467UI)
                Dim warmSegmentInBed As UInt16 = CPU0.ReadWord(&H469UI)
                Dim stackPhysicalInBed As UInteger =
                    ((CUInt(warmSegmentInBed) << 4) + CUInt(warmOffsetInBed)) And &HFFFFFFUI
                Dim frameInBed As New System.Text.StringBuilder()
                For wordIndexInBed As Integer = 0 To 19
                    If wordIndexInBed > 0 Then frameInBed.Append(" ")
                    frameInBed.Append(CPU0.ReadWord((stackPhysicalInBed + CUInt(wordIndexInBed * 2)) And &HFFFFFFUI).ToString("X4"))
                Next

                MotherboardBridge.ObserveProcessorShutdown(
                    RealTimeClock.PeekCmosByteForDiagnostics(&HF),
                    warmOffsetInBed,
                    warmSegmentInBed,
                    CPU0.ReadWord(&H472UI),
                    frameInBed.ToString())
            End Sub
        AddHandler MachineClock.SpeedModeChanged,
            Sub(turboInBed As Boolean) FrontPanel.SetTurbo(turboInBed)
        FrontPanel.SetPower(True)
        FrontPanel.SetTurbo(MachineClock.TurboEnabled)
        AddHandler Chipset.A20ForceLowChanged, AddressOf SetChipsetA20Override
        AddHandler Chipset.CpuResetRequested, AddressOf ResetProcessorOnly

        AddHandler SystemControl.NmiLineChanged,
            Sub(asserted As Boolean) NmiGate.SetSource(asserted)
        AddHandler SystemControl.NumericCoprocessorReset,
            Sub() CPU0.ResetNumericCoprocessor()
        ' Port F0h clears the motherboard NPX busy/error latch, not the 80287's
        ' architectural status word.  Keep NumericCoprocessorBusyReset as the
        ' physical board signal until the asynchronous ERROR/BUSY/IRQ13 path is
        ' modeled; do not fake it by mutating FpuStatusWord here.
        AddHandler RealTimeClock.NmiMaskChanged,
            Sub(disabled As Boolean) NmiGate.SetMasked(disabled)
        AddHandler Chipset.ReadyTimeoutNmiRequested,
            Sub() NmiGate.PulseSource()
        AddHandler NmiGate.NmiEdge,
            Sub() CPU0.RequestNmi()

        NmiGate.SetMasked(RealTimeClock.NmiDisabled)
        HardwareInitialized = True
    End Sub

    Private Sub SetKeyboardA20Gate(enabled As Boolean)
        KeyboardA20Enabled = enabled
        RecomputeA20()
    End Sub

    Private Sub SetChipsetA20Override(forceLow As Boolean)
        ChipsetForcesA20Low = forceLow
        RecomputeA20()
    End Sub

    Private Sub RecomputeA20()
        CPU0.A20Enabled = KeyboardA20Enabled AndAlso Not ChipsetForcesA20Low
    End Sub

    Private Sub ResetProcessorOnly()
        CPU0.Reset()
        ' A processor-only RESET does not reset the motherboard arbiter. If an
        ' external master still owns HOLD, re-present that physical line to the
        ' reset CPU before execution resumes.
        MotherboardBridge.ResynchronizeCpuHoldInterface()
        RecomputeA20()
        ' RESET# resets the processor, not the oscillator or motherboard timeline.
        ' In particular, this callback may execute synchronously inside RunSlice;
        ' clearing MachineClock here would corrupt the active time-accounting frame.
    End Sub

    Private Sub ResetProcessorAfterShutdown()
        ' An AT shutdown reset is part of the 80286 protected/real-mode
        ' transition.  Keep the already-bounded CPU forensic stream alive so it
        ' records firmware dispatch through CMOS 0Fh and the saved resume frame.
        CPU0.Reset(preserveForensicTraceInBed:=True)
        MotherboardBridge.ResynchronizeCpuHoldInterface()
        RecomputeA20()

        ' RESET# has now completed and the processor SHUTDOWN output is no
        ' longer asserted.  Re-arm the motherboard detector at that electrical
        ' boundary.  Waiting for an end-of-slice "running" sample is racy: a
        ' sufficiently large slice can contain the complete BIOS resume and
        ' reach the next intentional 286 shutdown before telemetry samples the
        ' intervening running state, leaving the previous shutdown latched and
        ' suppressing the next RESET# pulse.
        MotherboardBridge.ObserveProcessorRunning()
    End Sub

    Public Sub ResetHardwareMachine()
        SystemBus.ResetDevices()
        MotherboardBridge.ResetArbitration()
        KeyboardA20Enabled = False
        ChipsetForcesA20Low = Chipset.ForceA20Low
        CPU0.Reset()
        RecomputeA20()
        MachineClock.Reset()
    End Sub

    Public Sub PowerCycleHardwareMachine()
        SystemBus.PowerCycleDevices()
        MotherboardBridge.ResetArbitration()
        KeyboardA20Enabled = False
        ChipsetForcesA20Low = Chipset.ForceA20Low
        CPU0.Reset()
        RecomputeA20()
        MachineClock.Reset()
    End Sub

    Private Function AcknowledgeHardwareInterrupt() As Integer
        If Not MasterPic.HasPendingInterrupt Then Return -1
        Dim masterVector As Integer = MasterPic.Acknowledge()
        Dim masterIrq As Integer = masterVector And 7
        If Not MasterPic.IsProgrammedCascadeInput(masterIrq) Then Return masterVector

        ' The second INTA is answered by the slave even if its request vanished
        ' after the master accepted IRQ2.  Pic8259.Acknowledge then returns the
        ' slave's spurious IRQ7 vector (IRQ15 on an AT), matching cascaded 8259A
        ' electrical behavior instead of inventing a master IRQ2 interrupt.
        Return SlavePic.Acknowledge()
    End Function

    Public Sub MountFloppyImage(drive As Integer, path As String, Optional writeProtected As Boolean = False)
        FloppyController.AttachMediaSource(drive, New ImageFloppyMediaSource(New FloppyImage(path, writeProtected)))
    End Sub

    Public Sub MountPhysicalFloppyDrive(drive As Integer, hostDriveRoot As String)
        FloppyController.AttachMediaSource(drive, New PhysicalFloppyMediaSource(hostDriveRoot))
    End Sub

    Public Sub MountHardDiskImage(path As String, Optional readOnlyMedia As Boolean = False)
        IdeController.MountHardDisk(New HardDiskImage(path, readOnlyMedia))
    End Sub

    Public Sub MountIsoImage(path As String)
        IdeController.MountCdRom(New IsoImage(path))
    End Sub

End Module
