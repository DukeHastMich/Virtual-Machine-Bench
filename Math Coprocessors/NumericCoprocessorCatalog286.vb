Imports System

' Numeric processor-extension packages for the 80286 processor-extension socket.
' An empty socket leaves BUSY/ERROR/PEREQ undriven by an NPX. The 80286 still
' decodes ESC/WAIT according to its own MSW state; no phantom 80287 is retained.
Public NotInheritable Class NumericCoprocessorCatalog286
    Private Sub New()
    End Sub

    Public Shared ReadOnly Property SupportedIds As String()
        Get
            Return New String() {
                MachineHardwarePopulationConfiguration.NoneId,
                MachineHardwarePopulationConfiguration.Intel80287Id
            }
        End Get
    End Property

    Public Shared Function DisplayName(idInBed As String) As String
        Select Case MachineHardwarePopulationConfiguration.NormalizeNumericCoprocessorId(idInBed)
            Case MachineHardwarePopulationConfiguration.NoneId
                Return "None — empty numeric coprocessor socket"
            Case Else
                Return "Intel 80287"
        End Select
    End Function

    Public Shared Function CreateNumericCoprocessor(idInBed As String) As Intel80287
        Select Case MachineHardwarePopulationConfiguration.NormalizeNumericCoprocessorId(idInBed)
            Case MachineHardwarePopulationConfiguration.NoneId
                Return Nothing
            Case MachineHardwarePopulationConfiguration.Intel80287Id
                Return New Intel80287()
            Case Else
                Throw New InvalidOperationException("Unsupported numeric coprocessor package '" & If(idInBed, String.Empty) & "'.")
        End Select
    End Function
End Class
