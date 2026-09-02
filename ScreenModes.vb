Module ScreenModes
    Public Sub Mode13()
        Select Case Mode
            Case Is = 0
                For y = 0 To 479 Step 2
                    For x = 0 To 639 Step 2
                        ScreenBuffer.SetPixel(x, y, Color.FromArgb(vmem(0, x, y), vmem(1, x, y), vmem(2, x, y), vmem(3, x, y)))
                        ScreenBuffer.SetPixel(x + 1, y, Color.FromArgb(vmem(0, x + 1, y), vmem(1, x + 1, y), vmem(2, x + 1, y), vmem(3, x + 1, y)))
                        ScreenBuffer.SetPixel(x, y + 1, Color.FromArgb(vmem(0, x, y + 1), vmem(1, x, y + 1), vmem(2, x, y + 1), vmem(3, x, y + 1)))
                        ScreenBuffer.SetPixel(x + 1, y + 1, Color.FromArgb(vmem(0, x + 1, y + 1), vmem(1, x + 1, y + 1), vmem(2, x + 1, y + 1), vmem(3, x + 1, y + 1)))
                    Next
                Next
        End Select
        'PictureBox1.Image = ScreenBuffer

    End Sub

End Module
