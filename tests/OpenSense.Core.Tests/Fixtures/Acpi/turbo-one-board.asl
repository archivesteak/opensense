/*
 * A gaming WMI method shaped like the AN515-58's BIOS 2.19 code (IH51A board): the mode write picks a power policy in a
 * branch per board, and only one of the two branches acts on Turbo (5). Compiled with iasl: iasl turbo-one-board.asl
 */
DefinitionBlock ("", "SSDT", 2, "OPNSNS", "MODES1", 1)
{
    Name (BORD, 0x16)
    Name (GPUC, 0x10)
    Name (MODE, Zero)
    Name (PROF, Zero)
    Name (ANSR, Buffer (0x04) {})

    Method (WMBH, 3, Serialized)
    {
        If ((Arg1 == 0x16))
        {
            ANSR = Arg2
            Local0 = DerefOf (ANSR [Zero])
            Local1 = DerefOf (ANSR [One])
            ANSR [Zero] = 0x03
            If ((Local0 == 0x02))
            {
                ANSR [Zero] = One
            }
            ElseIf ((Local0 == 0x0B))
            {
                MODE = Local1
                If ((BORD == 0x04))
                {
                    If ((Local1 == Zero))
                    {
                        PROF = 0x03
                    }
                    ElseIf ((Local1 == One))
                    {
                        PROF = 0x04
                    }
                    ElseIf ((Local1 == 0x04))
                    {
                        PROF = 0x05
                    }
                    ElseIf ((Local1 == 0x05))
                    {
                        PROF = 0x07
                    }
                }

                If ((BORD == 0x16))
                {
                    If ((Local1 == Zero))
                    {
                        PROF = 0x12
                    }
                    ElseIf ((Local1 == One))
                    {
                        PROF = 0x13
                    }
                    ElseIf ((Local1 == 0x04))
                    {
                        If ((GPUC == 0x10))
                        {
                            PROF = 0x14
                        }
                        Else
                        {
                            PROF = 0x0E
                        }
                    }
                }

                ANSR [Zero] = Zero
            }

            Return (ANSR)
        }

        Return (ANSR)
    }
}
