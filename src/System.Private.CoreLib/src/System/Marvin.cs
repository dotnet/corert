// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System.Diagnostics;
using System.Runtime.CompilerServices;

namespace System
{
    internal static class Marvin
    {
        public static uint ComputeStringHash(ref byte data, uint count, ulong seed)
        {
            uint p0 = (uint)seed;
            uint p1 = (uint)(seed >> 32);

            int byteOffset = 0;  // declared as signed int so we don't have to cast everywhere (it's passed to Unsafe.Add() and used for nothing else.)

            while (count >= 8)
            {
                p0 += Unsafe.As<byte, uint>(ref Unsafe.Add(ref data, byteOffset));
                Block(ref p0, ref p1);

                p0 += Unsafe.As<byte, uint>(ref Unsafe.Add(ref data, byteOffset + 4));
                Block(ref p0, ref p1);

                byteOffset += 8;
                count -= 8;
            }

            switch (count)
            {
                case 4:
                    p0 += Unsafe.As<byte, uint>(ref Unsafe.Add(ref data, byteOffset));
                    Block(ref p0, ref p1);
                    goto case 0;

                case 0:
                    p0 += 0x80u;
                    break;

                case 5:
                    p0 += Unsafe.As<byte, uint>(ref Unsafe.Add(ref data, byteOffset));
                    byteOffset += 4;
                    Block(ref p0, ref p1);
                    goto case 1;

                case 1:
                    p0 += 0x8000u | Unsafe.Add(ref data, byteOffset);
                    break;

                case 6:
                    p0 += Unsafe.As<byte, uint>(ref Unsafe.Add(ref data, byteOffset));
                    byteOffset += 4;
                    Block(ref p0, ref p1);
                    goto case 2;

                case 2:
                    p0 += 0x800000u | Unsafe.As<byte, ushort>(ref Unsafe.Add(ref data, byteOffset));
                    break;

                case 7:
                    p0 += Unsafe.As<byte, uint>(ref Unsafe.Add(ref data, byteOffset));
                    byteOffset += 4;
                    Block(ref p0, ref p1);
                    goto case 3;

                case 3:
                    p0 += 0x80000000u | Unsafe.As<byte, ushort>(ref Unsafe.Add(ref data, byteOffset)) | Unsafe.Add(ref data, byteOffset + 2);
                    break;

                default:
                    Debug.Fail("Should not get here.");
                    throw new InvalidOperationException();
            }

            Block(ref p0, ref p1);
            Block(ref p0, ref p1);

            // At this point, p0 and p1 contains the 8-byte Marvin hash result. If we need a general purpose Marvin implementation in the future,
            // this could be refactored to stop here. For now, String.GetHashCode() is the only user of this function and he wants an 4-byte hash code
            // so this last step is specific to String.GetHashCode().

            return p0 ^ p1;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static void Block(ref uint rp0, ref uint rp1)
        {
            uint p0 = rp0;
            uint p1 = rp1;

            p1 ^= p0;
            p0 = _rotl(p0, 20);

            p0 += p1;
            p1 = _rotl(p1, 9);

            p1 ^= p0;
            p0 = _rotl(p0, 27);

            p0 += p1;
            p1 = _rotl(p1, 19);

            rp0 = p0;
            rp1 = p1;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static uint _rotl(uint value, int shift)
        {
            // This is expected to be optimized into a single rol (or ror with negated shift value) instruction
            return (value << shift) | (value >> (32 - shift));
        }

        public static ulong DefaultSeed => s_defaultSeed;

        private static ulong s_defaultSeed = GenerateSeed();

        private static ulong GenerateSeed()
        {
            ulong seed;
            unsafe
            {
                Interop.GetRandomBytes((byte*)&seed, sizeof(ulong));
            }
            return seed;
        }
    }
}
