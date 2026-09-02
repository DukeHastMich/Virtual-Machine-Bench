Module SystemRoms
    Public Sub CodePage(ByVal Keycode As Integer)
        'mainboard at F000:FA6E (15,64110) in my variable, and the second 
        'half is supplied by the location pointed to by interrupt 1F (0000:007C). 
        Dim HalfLife As Byte = 128
        For MemCount = 0 To 7
            If Keycode < 255 Then KeyByte(MemCount) = VrMem(15, 64108 + (Keycode * 8) + 8 + MemCount)
        Next
        'If Keycode = 65 Then Form1.Label1.Text = Str(KeyByte(0)) + ":" + Str(KeyByte(1)) + ":" + Str(KeyByte(2)) + ":" + Str(KeyByte(3)) + ":" + Str(KeyByte(4)) + ":" + Str(KeyByte(5)) + ":" + Str(KeyByte(6)) + ":" + Str(KeyByte(7))
        'decoder works don fuck wit it
        For y = 0 To 7
            For Bit = 0 To 7
                If KeyByte(y) - HalfLife < 0 Then
                    MyBits(Bit, y) = False
                Else
                    MyBits(Bit, y) = True
                    KeyByte(y) -= HalfLife
                End If
                HalfLife /= 2
            Next
            HalfLife = 128
        Next
    End Sub

End Module
