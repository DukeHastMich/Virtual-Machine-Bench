
Public Module CGAcard
    Public Sub CHRGen() 'This creates a cache of images for our Text Mode Font
        Dim HalfLife As Byte = 128, mybyte(7) As Single
        Dim ByteVal As Single, y2 As Byte
        'Form1 loads the dedicated 1 KiB character-generator ROM into the first
        'half of CharTable.  The historical System.ROM happened to contain a
        'second copy near F000:FA6E; the new 64 KiB BIOS is intentionally a
        'separate device and must not be treated as font storage.
        For a = 0 To 1023
            CharTable(a + 1024) = CharTable(a)
        Next
        For CColor = 0 To 15
            For CIndex = 0 To 255 'counts from 0 to 127 to increment index of characters
                AlphaGen(CColor, CIndex) = New Bitmap(8, 16)
                For y = 0 To 14 'iterates the y axis of each glyph
                    ByteVal = CharTable(CIndex * 8 + y2 + 7)
                    For x = 0 To 7 ' iterates the x axis of each glyph
                        If ByteVal - HalfLife < 0 Then 'check if the bit position is a 0 or one
                            'IndxdChrTbl(CIndex, x, y) = 0 'if 0 record 0 in Array at index,x,y
                            AlphaGen(CColor, CIndex).SetPixel(x, y, Color.FromArgb(0, 0, 0, 0))
                            AlphaGen(CColor, CIndex).SetPixel(x, y + 1, Color.FromArgb(0, 0, 0, 0))
                        Else
                            'IndxdChrTbl(CIndex, x, y) = 1 'if 1 record 1 in Array at index,x,y
                            AlphaGen(CColor, CIndex).SetPixel(x, y, PaletteColor(CColor))
                            AlphaGen(CColor, CIndex).SetPixel(x, y + 1, PaletteColor(CColor))
                            ByteVal -= HalfLife 'decrement our value to bring to next byte position check
                        End If
                        HalfLife /= 2 'decay by half

                    Next
                    HalfLife = 128 'restore to full for next byte 
                    If y Mod 2 = 1 Then y2 += 1
                Next
                y2 = 0
            Next
        Next
    End Sub

End Module

