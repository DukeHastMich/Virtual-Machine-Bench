Imports System

' Generic installed-machine read seam for source-backed Atmel/Microchip AT27 OTP EPROM identities.
' Programming voltage/algorithm and product-ID modes remain programmer/physical-domain truth.
Public NotInheritable Class VmbAt27Otp
    Private ReadOnly _data() As UShort
    Private ReadOnly _mask As UShort
    Public Sub New(words As Integer, widthBits As Integer, Optional contents As UShort() = Nothing)
        If words<=0 Then Throw New ArgumentOutOfRangeException(NameOf(words))
        If widthBits<>8 AndAlso widthBits<>16 Then Throw New ArgumentOutOfRangeException(NameOf(widthBits))
        _mask=If(widthBits=8,CUShort(&HFF),CUShort(&HFFFF))
        ReDim _data(words-1)
        If contents Is Nothing Then
            For i=0 To _data.Length-1 : _data(i)=_mask : Next
        Else
            If contents.Length<>words Then Throw New ArgumentException("OTP image length mismatch",NameOf(contents))
            For i=0 To contents.Length-1 : _data(i)=CUShort(contents(i) And _mask) : Next
        End If
    End Sub
    Public Function EvaluateRead(address As Integer, ceN As Boolean, oeN As Boolean) As Nullable(Of UShort)
        If ceN OrElse oeN Then Return Nothing
        If address<0 OrElse address>=_data.Length Then Throw New ArgumentOutOfRangeException(NameOf(address))
        Return _data(address)
    End Function
End Class
