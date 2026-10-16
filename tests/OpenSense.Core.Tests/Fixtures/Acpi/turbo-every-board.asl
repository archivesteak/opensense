/*
 * A gaming WMI method shaped like the AN515-57's BIOS 1.17 code: one If per mode, each moving the power policy, Turbo (5)
 * included. Compiled with iasl: iasl turbo-every-board.asl
 */
DefinitionBlock ("", "SSDT", 2, "OPNSNS", "MODES2", 1)
{
    Name (BASE, 0x02)
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
            If ((Local0 == 0x0B))
            {
                MODE = Local1
                If ((Local1 == Zero))
                {
                    PROF -= One
                }

                If ((Local1 == One))
                {
                    PROF = BASE
                }

                If ((Local1 == 0x04))
                {
                    PROF += One
                }

                If ((Local1 == 0x05))
                {
                    PROF += 0x02
                }

                ANSR [Zero] = Zero
            }
            Else
            {
                ANSR [Zero] = 0x03
            }

            Return (ANSR)
        }

        Return (ANSR)
    }
}
