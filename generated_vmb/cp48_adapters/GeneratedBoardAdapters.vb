' AUTO-GENERATED EXECUTABLE ADAPTER LAYER. Board Script is canonical; do not hand-edit.
Option Strict On
Option Explicit On
Imports System

Public NotInheritable Class GeneratedVCBAdapters
    Private ReadOnly U1 As Rtl8019As
    Private ReadOnly U2 As Sst39Sf040
    Private ReadOnly U3 As Microwire93Lc46c

    Public Sub New()
        U1 = New Rtl8019As()
        U2 = New Sst39Sf040()
        U3 = New Microwire93Lc46c()
        AddHandler U1.MicrowirePinsChanged, AddressOf ProtocolLink_0
        U1.SetEepromDataOut(U3.SerialDataOut)
    End Sub

    Private Sub ProtocolLink_0(chipSelect As Boolean, clock As Boolean, dataIn As Boolean)
        U3.SetSerialPins(chipSelect, clock, dataIn)
        U1.SetEepromDataOut(U3.SerialDataOut)
    End Sub

    Public Shared ReadOnly Property ExecutableProtocolLinkCount As Integer = 1
    Public Shared ReadOnly Property CoveredProtocolNetCount As Integer = 4
    Public Shared ReadOnly Property PendingSemanticConnectionCount As Integer = 52
End Class
