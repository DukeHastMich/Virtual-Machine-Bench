Imports System

' Processor-package boundary used by motherboard runtimes which own the machine clock.
' A package may use host worker threads to prepare pure computation, but only the
' motherboard/runtime thread may call GrantExternalClocks and commit guest-visible state.
Public Interface IProcessorPackageRuntime
    ReadOnly Property PackageId As String
    ReadOnly Property ArchitectureName As String
    ReadOnly Property PackagePresent As Boolean
    ReadOnly Property HasIntegratedFloatingPointUnit As Boolean
    ReadOnly Property ExternalBusClockHz As Long
    ReadOnly Property CoreClockHz As Long

    Sub ColdReset()
    Sub WarmReset()

    ' Physical input pins.  These methods change sampled input state only; they do
    ' not create interrupts or elapsed time outside normal processor boundaries.
    Sub SetIntr(assertedInBed As Boolean)
    Sub SignalNmiRisingEdge()
    Sub SetA20Mask(assertedLowInBed As Boolean)
    Sub SetHold(assertedInBed As Boolean)

    ' The board grants physical external clocks.  The package may consume multiple
    ' internal core clocks for each external clock according to its silicon clocking
    ' scheme (DX2 = 2:1), but may never advance motherboard time on its own.
    Function GrantExternalClocks(externalClocksInBed As Long,
                                 fabricInBed As IProcessorLocalBusFabric) As ProcessorClockGrantResult
End Interface

Public Structure ProcessorClockGrantResult
    Public ExternalClocksGranted As Long
    Public ExternalClocksConsumed As Long
    Public CoreClocksConsumed As Long
    Public InstructionsRetired As Long
    Public Halted As Boolean
    Public WaitingForBus As Boolean
    Public HoldingBus As Boolean

    Public Shared Function Empty() As ProcessorClockGrantResult
        Return New ProcessorClockGrantResult()
    End Function
End Structure

' Package metadata is intentionally separate from the executable package object.
' The configurator can describe an unimplemented physical CPU without implying that
' an execution core exists for it.
Public Interface IProcessorPhysicalPackageProfile
    ReadOnly Property PackageId As String
    ReadOnly Property DisplayName As String
    ReadOnly Property SocketFamily As String
    ReadOnly Property NominalVoltage As Double
    ReadOnly Property ExternalClockHz As Long
    ReadOnly Property CoreClockHz As Long
    ReadOnly Property PinCount As Integer
    ReadOnly Property HasIntegratedFloatingPointUnit As Boolean
End Interface
