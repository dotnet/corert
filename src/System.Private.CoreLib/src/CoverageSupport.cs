// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

#if DEBUG
/* When building for code coverage runs, this creates the classes necessary to 
 * call hit using the correct pinvoke signature for the toolchain. It uses the "/UseManagedProxy"
 * version of instrumentation because that only introduces two IL instructions per hit, and is 
 * thus easier fix up afterwards.
 * 
 * The array in the static constructor will be replaced during fixup with the array that was
 * created to register System.Private.CoreLib.dll
*/
#pragma warning disable 169
#pragma warning disable 3001


using System.Runtime.InteropServices;
using System.Security;

namespace MS.Magellan.Runtime
{
    // This type must not be public otherwise it breaks shared production builds where all public
    // types and methods are treated as roots and therefore this assembly will have a dependency
    // on Coverage.dll causing it to fail to load on machines without the Magellan tools.
    internal unsafe class CoverageBase
    {
        private static byte[] m_BBRegHeader;
        private static CoverageIL2 m_BBRegHeaderField;
        internal unsafe byte* m_Vector;

        internal CoverageBase(byte[] header)
        {
            m_BBRegHeader = header;
            fixed (byte* p = m_BBRegHeader)
            {
                ulong u = CoverageRegisterBinaryWithStruct(p);
                this.m_Vector = (byte*)u;
            }
        }
        [DllImport("Coverage.dll", CallingConvention = CallingConvention.StdCall, CharSet = CharSet.Ansi)]
        private static extern unsafe ulong CoverageRegisterBinaryWithStruct(byte* p);
        internal void Hit(uint index)
        {
            this.m_Vector[index] = 1;
        }
    }

    [System.Runtime.CompilerServices.EagerStaticClassConstructionAttribute]
    internal unsafe static class CoverageIL
    {
        private static readonly byte[] m_BBRegHeader;
        private static byte[] m_BBRegHeaderField;
        private static readonly CoverageBase m_Coverage;

        static CoverageIL()
        {
            m_Coverage = new CoverageBase(m_BBRegHeader = new byte[] {
                   0xc0, 1, 0xc0, 0xde, 0x86, 0, 0, 2, 0x60, 0x48, 1, 0, 0x8b, 0x71, 0x58, 0x48,
        0xb9, 0xd6, 0xeb, 14, 0x23, 0x69, 0x57, 0x58, 0x60, 0, 0, 0, 0x69, 0x3d, 1, 0,
        0x60, 0, 0, 0, 40, 0, 0, 0, 0, 0, 5, 0, 0x58, 0x7c, 0x74, 0x76,
        0x18, 0, 0, 0, 0x48, 0, 0, 0, 0x53, 0, 0, 0, 0xcc, 0x30, 0x11, 0,
        0, 0, 0, 0, 0, 0, 0, 0, 0x63, 0x6f, 0x72, 0x65, 0x66, 120, 0x2e, 100,
        0x6c, 0x6c, 0, 0x7b, 110, 0x2f, 0x61, 0x7d, 0, 0, 0, 0, 0, 0, 0,
            });
        }

        public static void Hit(uint num1)
        {
            m_Coverage.Hit(num1);
        }
    }

    [StructLayout(LayoutKind.Explicit, Size = 0x60, Pack = 1)]
    internal struct CoverageIL2
    {
    }
}
#endif
