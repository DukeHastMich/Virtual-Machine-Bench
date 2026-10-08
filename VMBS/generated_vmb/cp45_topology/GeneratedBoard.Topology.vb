' AUTO-GENERATED TOPOLOGY LAYER. Derived from Board Script; do not hand-edit.
Option Strict On
Option Explicit On
Imports System
Imports System.Collections.Generic

Public NotInheritable Class GeneratedVCBTopology
    Public NotInheritable Class Endpoint
        Public ReadOnly Kind As String
        Public ReadOnly InstanceId As String
        Public ReadOnly InterfaceName As String
        Public ReadOnly Signal As String
        Public Sub New(k As String, i As String, f As String, s As String)
            Kind=k : InstanceId=i : InterfaceName=f : Signal=s
        End Sub
    End Class
    Public NotInheritable Class Net
        Public ReadOnly Name As String
        Public ReadOnly Operation As String
        Public ReadOnly Endpoints As Endpoint()
        Public Sub New(n As String, o As String, e As Endpoint())
            Name=n : Operation=o : Endpoints=e
        End Sub
    End Class

    Public Shared Function BuildTopology() As Net()
        Return New Net() {
            New Net("/A0", "platform_net", New Endpoint() {New Endpoint("MODULE_PIN", "U1", "ISA8", "A0"), New Endpoint("MODULE_PIN", "U2", "PARALLEL_X8_MEMORY", "A0"), New Endpoint("PLATFORM_PIN", "BUS1", "ISA8", "A0")}),
            New Net("/A1", "platform_net", New Endpoint() {New Endpoint("MODULE_PIN", "U1", "ISA8", "A1"), New Endpoint("MODULE_PIN", "U2", "PARALLEL_X8_MEMORY", "A1"), New Endpoint("PLATFORM_PIN", "BUS1", "ISA8", "A1")}),
            New Net("/A10", "platform_net", New Endpoint() {New Endpoint("MODULE_PIN", "U1", "ISA8", "A10"), New Endpoint("MODULE_PIN", "U2", "PARALLEL_X8_MEMORY", "A10"), New Endpoint("PLATFORM_PIN", "BUS1", "ISA8", "A10")}),
            New Net("/A11", "platform_net", New Endpoint() {New Endpoint("MODULE_PIN", "U1", "ISA8", "A11"), New Endpoint("MODULE_PIN", "U2", "PARALLEL_X8_MEMORY", "A11"), New Endpoint("PLATFORM_PIN", "BUS1", "ISA8", "A11")}),
            New Net("/A12", "platform_net", New Endpoint() {New Endpoint("MODULE_PIN", "U1", "ISA8", "A12"), New Endpoint("MODULE_PIN", "U2", "PARALLEL_X8_MEMORY", "A12"), New Endpoint("PLATFORM_PIN", "BUS1", "ISA8", "A12")}),
            New Net("/A13", "platform_net", New Endpoint() {New Endpoint("MODULE_PIN", "U1", "ISA8", "A13"), New Endpoint("MODULE_PIN", "U2", "PARALLEL_X8_MEMORY", "A13"), New Endpoint("PLATFORM_PIN", "BUS1", "ISA8", "A13")}),
            New Net("/A14", "platform_net", New Endpoint() {New Endpoint("MODULE_PIN", "U1", "ISA8", "A14"), New Endpoint("PLATFORM_PIN", "BUS1", "ISA8", "A14")}),
            New Net("/A15", "platform_net", New Endpoint() {New Endpoint("MODULE_PIN", "U1", "ISA8", "A15"), New Endpoint("PLATFORM_PIN", "BUS1", "ISA8", "A15")}),
            New Net("/A16", "platform_net", New Endpoint() {New Endpoint("MODULE_PIN", "U1", "ISA8", "A16"), New Endpoint("PLATFORM_PIN", "BUS1", "ISA8", "A16")}),
            New Net("/A17", "platform_net", New Endpoint() {New Endpoint("MODULE_PIN", "U1", "ISA8", "A17"), New Endpoint("PLATFORM_PIN", "BUS1", "ISA8", "A17")}),
            New Net("/A18", "platform_net", New Endpoint() {New Endpoint("MODULE_PIN", "U1", "ISA8", "A18"), New Endpoint("PLATFORM_PIN", "BUS1", "ISA8", "A18")}),
            New Net("/A19", "platform_net", New Endpoint() {New Endpoint("MODULE_PIN", "U1", "ISA8", "A19"), New Endpoint("PLATFORM_PIN", "BUS1", "ISA8", "A19")}),
            New Net("/A2", "platform_net", New Endpoint() {New Endpoint("MODULE_PIN", "U1", "ISA8", "A2"), New Endpoint("MODULE_PIN", "U2", "PARALLEL_X8_MEMORY", "A2"), New Endpoint("PLATFORM_PIN", "BUS1", "ISA8", "A2")}),
            New Net("/A3", "platform_net", New Endpoint() {New Endpoint("MODULE_PIN", "U1", "ISA8", "A3"), New Endpoint("MODULE_PIN", "U2", "PARALLEL_X8_MEMORY", "A3"), New Endpoint("PLATFORM_PIN", "BUS1", "ISA8", "A3")}),
            New Net("/A4", "platform_net", New Endpoint() {New Endpoint("MODULE_PIN", "U1", "ISA8", "A4"), New Endpoint("MODULE_PIN", "U2", "PARALLEL_X8_MEMORY", "A4"), New Endpoint("PLATFORM_PIN", "BUS1", "ISA8", "A4")}),
            New Net("/A5", "platform_net", New Endpoint() {New Endpoint("MODULE_PIN", "U1", "ISA8", "A5"), New Endpoint("MODULE_PIN", "U2", "PARALLEL_X8_MEMORY", "A5"), New Endpoint("PLATFORM_PIN", "BUS1", "ISA8", "A5")}),
            New Net("/A6", "platform_net", New Endpoint() {New Endpoint("MODULE_PIN", "U1", "ISA8", "A6"), New Endpoint("MODULE_PIN", "U2", "PARALLEL_X8_MEMORY", "A6"), New Endpoint("PLATFORM_PIN", "BUS1", "ISA8", "A6")}),
            New Net("/A7", "platform_net", New Endpoint() {New Endpoint("MODULE_PIN", "U1", "ISA8", "A7"), New Endpoint("MODULE_PIN", "U2", "PARALLEL_X8_MEMORY", "A7"), New Endpoint("PLATFORM_PIN", "BUS1", "ISA8", "A7")}),
            New Net("/A8", "platform_net", New Endpoint() {New Endpoint("MODULE_PIN", "U1", "ISA8", "A8"), New Endpoint("MODULE_PIN", "U2", "PARALLEL_X8_MEMORY", "A8"), New Endpoint("PLATFORM_PIN", "BUS1", "ISA8", "A8")}),
            New Net("/A9", "platform_net", New Endpoint() {New Endpoint("MODULE_PIN", "U1", "ISA8", "A9"), New Endpoint("MODULE_PIN", "U2", "PARALLEL_X8_MEMORY", "A9"), New Endpoint("PLATFORM_PIN", "BUS1", "ISA8", "A9")}),
            New Net("/AEN", "platform_net", New Endpoint() {New Endpoint("MODULE_PIN", "U1", "ISA8", "AEN"), New Endpoint("PLATFORM_PIN", "BUS1", "ISA8", "AEN")}),
            New Net("/D0", "platform_net", New Endpoint() {New Endpoint("MODULE_PIN", "U1", "ISA8", "D0"), New Endpoint("PLATFORM_PIN", "BUS1", "ISA8", "D0")}),
            New Net("/D1", "platform_net", New Endpoint() {New Endpoint("MODULE_PIN", "U1", "ISA8", "D1"), New Endpoint("PLATFORM_PIN", "BUS1", "ISA8", "D1")}),
            New Net("/D2", "platform_net", New Endpoint() {New Endpoint("MODULE_PIN", "U1", "ISA8", "D2"), New Endpoint("PLATFORM_PIN", "BUS1", "ISA8", "D2")}),
            New Net("/D3", "platform_net", New Endpoint() {New Endpoint("MODULE_PIN", "U1", "ISA8", "D3"), New Endpoint("PLATFORM_PIN", "BUS1", "ISA8", "D3")}),
            New Net("/D4", "platform_net", New Endpoint() {New Endpoint("MODULE_PIN", "U1", "ISA8", "D4"), New Endpoint("PLATFORM_PIN", "BUS1", "ISA8", "D4")}),
            New Net("/D5", "platform_net", New Endpoint() {New Endpoint("MODULE_PIN", "U1", "ISA8", "D5"), New Endpoint("PLATFORM_PIN", "BUS1", "ISA8", "D5")}),
            New Net("/D6", "platform_net", New Endpoint() {New Endpoint("MODULE_PIN", "U1", "ISA8", "D6"), New Endpoint("PLATFORM_PIN", "BUS1", "ISA8", "D6")}),
            New Net("/D7", "platform_net", New Endpoint() {New Endpoint("MODULE_PIN", "U1", "ISA8", "D7"), New Endpoint("PLATFORM_PIN", "BUS1", "ISA8", "D7")}),
            New Net("/~IOR", "platform_net", New Endpoint() {New Endpoint("MODULE_PIN", "U1", "ISA8", "IOR#"), New Endpoint("PLATFORM_PIN", "BUS1", "ISA8", "IOR#")}),
            New Net("/~IOW", "platform_net", New Endpoint() {New Endpoint("MODULE_PIN", "U1", "ISA8", "IOW#"), New Endpoint("PLATFORM_PIN", "BUS1", "ISA8", "IOW#")}),
            New Net("GND", "platform_net", New Endpoint() {New Endpoint("MODULE_PIN", "U2", "PARALLEL_X8_MEMORY", "VSS"), New Endpoint("PLATFORM_PIN", "BUS1", "ISA8", "GND"), New Endpoint("PLATFORM_PIN", "BUS1", "ISA8", "GND"), New Endpoint("PLATFORM_PIN", "BUS1", "ISA8", "GND")}),
            New Net("VCC", "platform_net", New Endpoint() {New Endpoint("MODULE_PIN", "U2", "PARALLEL_X8_MEMORY", "VDD"), New Endpoint("PLATFORM_PIN", "BUS1", "ISA8", "+5V"), New Endpoint("PLATFORM_PIN", "BUS1", "ISA8", "+5V")}),
            New Net("/RSTDRV", "platform_net", New Endpoint() {New Endpoint("MODULE_PIN", "U1", "ISA8", "RESETDRV"), New Endpoint("PLATFORM_PIN", "BUS1", "ISA8", "RESETDRV")}),
            New Net("/IRQ9", "platform_net", New Endpoint() {New Endpoint("MODULE_PIN", "U1", "ISA8", "IRQ9"), New Endpoint("PLATFORM_PIN", "BUS1", "ISA8", "IRQ9")}),
            New Net("/~SMEMW", "platform_net", New Endpoint() {New Endpoint("MODULE_PIN", "U1", "ISA8", "SMEMW#"), New Endpoint("MODULE_PIN", "U2", "PARALLEL_X8_MEMORY", "WE#"), New Endpoint("PLATFORM_PIN", "BUS1", "ISA8", "SMEMW#")}),
            New Net("/~SMEMR", "platform_net", New Endpoint() {New Endpoint("MODULE_PIN", "U1", "ISA8", "SMEMR#"), New Endpoint("MODULE_PIN", "U2", "PARALLEL_X8_MEMORY", "OE#"), New Endpoint("PLATFORM_PIN", "BUS1", "ISA8", "SMEMR#")}),
            New Net("/IRQ5", "platform_net", New Endpoint() {New Endpoint("MODULE_PIN", "U1", "ISA8", "IRQ5"), New Endpoint("PLATFORM_PIN", "BUS1", "ISA8", "IRQ5")}),
            New Net("/IRQ4", "platform_net", New Endpoint() {New Endpoint("MODULE_PIN", "U1", "ISA8", "IRQ4"), New Endpoint("PLATFORM_PIN", "BUS1", "ISA8", "IRQ4")}),
            New Net("/IRQ3", "platform_net", New Endpoint() {New Endpoint("MODULE_PIN", "U1", "ISA8", "IRQ3"), New Endpoint("PLATFORM_PIN", "BUS1", "ISA8", "IRQ3")}),
            New Net("/IOCHRDY", "platform_net", New Endpoint() {New Endpoint("MODULE_PIN", "U1", "ISA8", "IOCHRDY"), New Endpoint("PLATFORM_PIN", "BUS1", "ISA8", "IOCHRDY")}),
            New Net("/BA18", "internal_net", New Endpoint() {New Endpoint("MODULE_PIN", "U1", "BOOT_ROM", "A18"), New Endpoint("MODULE_PIN", "U2", "PARALLEL_X8_MEMORY", "A18")}),
            New Net("/BA17", "internal_net", New Endpoint() {New Endpoint("MODULE_PIN", "U1", "BOOT_ROM", "A17"), New Endpoint("MODULE_PIN", "U2", "PARALLEL_X8_MEMORY", "A17")}),
            New Net("/BA16", "internal_net", New Endpoint() {New Endpoint("MODULE_PIN", "U1", "BOOT_ROM", "A16"), New Endpoint("MODULE_PIN", "U2", "PARALLEL_X8_MEMORY", "A16")}),
            New Net("/BD6", "internal_net", New Endpoint() {New Endpoint("MODULE_PIN", "U1", "BOOT_ROM", "D6"), New Endpoint("MODULE_PIN", "U2", "PARALLEL_X8_MEMORY", "D6"), New Endpoint("MODULE_PIN", "U3", "MICROWIRE_3WIRE", "DI"), New Endpoint("MODULE_PIN", "U1", "MICROWIRE_3WIRE", "DI")}),
            New Net("/BD5", "internal_net", New Endpoint() {New Endpoint("MODULE_PIN", "U1", "BOOT_ROM", "D5"), New Endpoint("MODULE_PIN", "U2", "PARALLEL_X8_MEMORY", "D5"), New Endpoint("MODULE_PIN", "U3", "MICROWIRE_3WIRE", "CLK"), New Endpoint("MODULE_PIN", "U1", "MICROWIRE_3WIRE", "CLK")}),
            New Net("/BD4", "internal_net", New Endpoint() {New Endpoint("MODULE_PIN", "U1", "BOOT_ROM", "D4"), New Endpoint("MODULE_PIN", "U2", "PARALLEL_X8_MEMORY", "D4")}),
            New Net("/BD3", "internal_net", New Endpoint() {New Endpoint("MODULE_PIN", "U2", "PARALLEL_X8_MEMORY", "D3"), New Endpoint("MODULE_PIN", "U1", "BOOT_ROM", "D3")}),
            New Net("/BD2", "internal_net", New Endpoint() {New Endpoint("MODULE_PIN", "U2", "PARALLEL_X8_MEMORY", "D2"), New Endpoint("MODULE_PIN", "U1", "BOOT_ROM", "D2")}),
            New Net("/BD1", "internal_net", New Endpoint() {New Endpoint("MODULE_PIN", "U2", "PARALLEL_X8_MEMORY", "D1"), New Endpoint("MODULE_PIN", "U1", "BOOT_ROM", "D1")}),
            New Net("/BD0", "internal_net", New Endpoint() {New Endpoint("MODULE_PIN", "U2", "PARALLEL_X8_MEMORY", "D0"), New Endpoint("MODULE_PIN", "U1", "BOOT_ROM", "D0")}),
            New Net("/BA15", "internal_net", New Endpoint() {New Endpoint("MODULE_PIN", "U1", "BOOT_ROM", "A15"), New Endpoint("MODULE_PIN", "U2", "PARALLEL_X8_MEMORY", "A15")}),
            New Net("/BA14", "internal_net", New Endpoint() {New Endpoint("MODULE_PIN", "U1", "BOOT_ROM", "A14"), New Endpoint("MODULE_PIN", "U2", "PARALLEL_X8_MEMORY", "A14")}),
            New Net("/~BCS", "internal_net", New Endpoint() {New Endpoint("MODULE_PIN", "U2", "PARALLEL_X8_MEMORY", "CE#"), New Endpoint("MODULE_PIN", "U1", "BOOT_ROM", "CE#")}),
            New Net("/EECS", "internal_net", New Endpoint() {New Endpoint("MODULE_PIN", "U3", "MICROWIRE_3WIRE", "CS"), New Endpoint("MODULE_PIN", "U1", "MICROWIRE_3WIRE", "CS")}),
            New Net("/BD7", "internal_net", New Endpoint() {New Endpoint("MODULE_PIN", "U1", "BOOT_ROM", "D7"), New Endpoint("MODULE_PIN", "U2", "PARALLEL_X8_MEMORY", "D7"), New Endpoint("MODULE_PIN", "U1", "MICROWIRE_3WIRE", "DO"), New Endpoint("MODULE_PIN", "U3", "MICROWIRE_3WIRE", "DO")})
        }
    End Function

    Public Shared ReadOnly Property NetCount As Integer = 56
End Class
