/*
 * A gaming WMI method shaped like the AN515-46's BIOS 1.18 code: Switch statements, where Turbo's case (5) only answers an
 * error status. Compiled with iasl: iasl turbo-refused.asl
 */
DefinitionBlock ("", "SSDT", 2, "OPNSNS", "MODES3", 1)
{
    Name (MODE, Zero)
    Name (PROF, Zero)
    Name (ANSR, Buffer (0x04) {})

    Method (WMBH, 3, Serialized)
    {
        Switch (ToInteger (Arg1))
        {
            Case (0x16)
            {
                ANSR = Arg2
                Local0 = DerefOf (ANSR [Zero])
                Local1 = DerefOf (ANSR [One])
                Switch (ToInteger (Local0))
                {
                    Case (0x0B)
                    {
                        Switch (ToInteger (Local1))
                        {
                            Case (Zero)
                            {
                                MODE = Zero
                                PROF = 0x23
                            }
                            Case (One)
                            {
                                MODE = One
                                PROF = 0x36
                            }
                            Case (0x04)
                            {
                                MODE = 0x04
                                PROF = 0x48
                            }
                            Case (0x05)
                            {
                                ANSR [Zero] = One
                            }
                            Default
                            {
                                PROF = 0x36
                            }
                        }
                    }
                }

                Return (ANSR)
            }
        }

        Return (ANSR)
    }
}
