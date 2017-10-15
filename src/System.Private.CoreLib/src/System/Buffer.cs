// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

#if AMD64 || (BIT32 && !ARM)
#define HAS_CUSTOM_BLOCKS
#endif

using System;
using System.Runtime;
using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Runtime.CompilerServices;

#if BIT64
using nuint = System.UInt64;
#else
using nuint = System.UInt32;
#endif

namespace System
{
    public static class Buffer
    {
        public static unsafe void BlockCopy(Array src, int srcOffset,
                                            Array dst, int dstOffset,
                                            int count)
        {
            nuint uSrcLen;
            nuint uDstLen;

            if (src == null)
                throw new ArgumentNullException(nameof(src));
            if (dst == null)
                throw new ArgumentNullException(nameof(dst));

            // Use optimized path for byte arrays since this is the main scenario for Buffer::BlockCopy
            // We only need an unreliable comparison since the slow path can handle the byte[] case too.
            if (src.EETypePtr.FastEqualsUnreliable(EETypePtr.EETypePtrOf<byte[]>()))
            {
                uSrcLen = (nuint)src.Length;
            }
            else
            {
                RuntimeImports.RhCorElementTypeInfo srcCorElementTypeInfo = src.ElementEEType.CorElementTypeInfo;
                uSrcLen = ((nuint)src.Length) << srcCorElementTypeInfo.Log2OfSize;
                if (!srcCorElementTypeInfo.IsPrimitive)
                    throw new ArgumentException(SR.Arg_MustBePrimArray, nameof(src));
            }

            if (src != dst)
            {
                // Use optimized path for byte arrays since this is the main scenario for Buffer::BlockCopy
                // We only need an unreliable comparison since the slow path can handle the byte[] case too.
                if (dst.EETypePtr.FastEqualsUnreliable(EETypePtr.EETypePtrOf<byte[]>()))
                {
                    uDstLen = (nuint)dst.Length;
                }
                else
                {
                    RuntimeImports.RhCorElementTypeInfo dstCorElementTypeInfo = dst.ElementEEType.CorElementTypeInfo;
                    if (!dstCorElementTypeInfo.IsPrimitive)
                        throw new ArgumentException(SR.Arg_MustBePrimArray, nameof(dst));
                    uDstLen = ((nuint)dst.Length) << dstCorElementTypeInfo.Log2OfSize;
                }
            }
            else
            {
                uDstLen = uSrcLen;
            }

            if (srcOffset < 0)
                throw new ArgumentOutOfRangeException(SR.ArgumentOutOfRange_MustBeNonNegInt32, nameof(srcOffset));
            if (dstOffset < 0)
                throw new ArgumentOutOfRangeException(SR.ArgumentOutOfRange_MustBeNonNegInt32, nameof(dstOffset));
            if (count < 0)
                throw new ArgumentOutOfRangeException(SR.ArgumentOutOfRange_MustBeNonNegInt32, nameof(count));

            nuint uCount = (nuint)count;
            nuint uSrcOffset = (nuint)srcOffset;
            nuint uDstOffset = (nuint)dstOffset;

            if (uSrcLen < uSrcOffset + uCount)
                throw new ArgumentException(SR.Argument_InvalidOffLen);
            if (uDstLen < uDstOffset + uCount)
                throw new ArgumentException(SR.Argument_InvalidOffLen);

            if (uCount != 0)
            {
                fixed (byte* pSrc = &src.GetRawArrayData(), pDst = &dst.GetRawArrayData())
                {
                    Buffer.Memmove(pDst + uDstOffset, pSrc + uSrcOffset, uCount);
                }
            }
        }

        // This is ported from the optimized CRT assembly in memchr.asm. The JIT generates 
        // pretty good code here and this ends up being within a couple % of the CRT asm.
        // It is however cross platform as the CRT hasn't ported their fast version to 64-bit
        // platforms.
        //
        internal static unsafe int IndexOfByte(byte* src, byte value, int index, int count)
        {
            byte* pByte = src + index;

            // Align up the pointer to sizeof(int).
            while (((int)pByte & 3) != 0)
            {
                if (count == 0)
                    return -1;
                else if (*pByte == value)
                    return (int)(pByte - src);

                count--;
                pByte++;
            }

            // Fill comparer with value byte for comparisons
            //
            // comparer = 0/0/value/value
            uint comparer = (((uint)value << 8) + (uint)value);
            // comparer = value/value/value/value
            comparer = (comparer << 16) + comparer;

            // Run through buffer until we hit a 4-byte section which contains
            // the byte we're looking for or until we exhaust the buffer.
            while (count > 3)
            {
                // Test the buffer for presence of value. comparer contains the byte
                // replicated 4 times.
                uint t1 = *(uint*)pByte;
                t1 = t1 ^ comparer;
                uint t2 = 0x7efefeff + t1;
                t1 = t1 ^ 0xffffffff;
                t1 = t1 ^ t2;
                t1 = t1 & 0x81010100;

                // if t1 is zero then these 4-bytes don't contain a match
                if (t1 != 0)
                {
                    // We've found a match for value, figure out which position it's in.
                    int foundIndex = (int)(pByte - src);
                    if (pByte[0] == value)
                        return foundIndex;
                    else if (pByte[1] == value)
                        return foundIndex + 1;
                    else if (pByte[2] == value)
                        return foundIndex + 2;
                    else if (pByte[3] == value)
                        return foundIndex + 3;
                }

                count -= 4;
                pByte += 4;
            }

            // Catch any bytes that might be left at the tail of the buffer
            while (count > 0)
            {
                if (*pByte == value)
                    return (int)(pByte - src);

                count--;
                pByte++;
            }

            // If we don't have a match return -1;
            return -1;
        }

        internal static unsafe void ZeroMemory(byte* src, long len)
        {
            while (len-- > 0)
                *(src + len) = 0;
        }

        public static int ByteLength(Array array)
        {
            // Is the array present?
            if (array == null)
                throw new ArgumentNullException(nameof(array));

            // Is it of primitive types?
            if (!array.ElementEEType.IsPrimitive)
                throw new ArgumentException(SR.Arg_MustBePrimArray, nameof(array));

            return _ByteLength(array);
        }

        private static unsafe int _ByteLength(Array array)
        {
            return checked(array.Length * array.EETypePtr.ComponentSize);
        }

        public static unsafe byte GetByte(Array array, int index)
        {
            // Is the array present?
            if (array == null)
                throw new ArgumentNullException(nameof(array));

            // Is it of primitive types?
            if (!array.ElementEEType.IsPrimitive)
                throw new ArgumentException(SR.Arg_MustBePrimArray, nameof(array));

            // Is the index in valid range of the array?
            if (index < 0 || index >= _ByteLength(array))
                throw new ArgumentOutOfRangeException(nameof(index));

            return Unsafe.Add(ref array.GetRawArrayData(), index);
        }

        public static unsafe void SetByte(Array array, int index, byte value)
        {
            // Is the array present?
            if (array == null)
                throw new ArgumentNullException(nameof(array));

            // Is it of primitive types?
            if (!array.ElementEEType.IsPrimitive)
                throw new ArgumentException(SR.Arg_MustBePrimArray, nameof(array));

            // Is the index in valid range of the array?
            if (index < 0 || index >= _ByteLength(array))
                throw new ArgumentOutOfRangeException(nameof(index));

            Unsafe.Add(ref array.GetRawArrayData(), index) = value;
        }

        // The attributes on this method are chosen for best JIT performance. 
        // Please do not edit unless intentional.
        [MethodImplAttribute(MethodImplOptions.AggressiveInlining)]
        [CLSCompliant(false)]
        public static unsafe void MemoryCopy(void* source, void* destination, long destinationSizeInBytes, long sourceBytesToCopy)
        {
            if (sourceBytesToCopy > destinationSizeInBytes)
            {
                throw new ArgumentOutOfRangeException(nameof(sourceBytesToCopy));
            }

            Memmove((byte*)destination, (byte*)source, checked((nuint)sourceBytesToCopy));
        }

        // The attributes on this method are chosen for best JIT performance. 
        // Please do not edit unless intentional.
        [MethodImplAttribute(MethodImplOptions.AggressiveInlining)]
        [CLSCompliant(false)]
        public static unsafe void MemoryCopy(void* source, void* destination, ulong destinationSizeInBytes, ulong sourceBytesToCopy)
        {
            if (sourceBytesToCopy > destinationSizeInBytes)
            {
                throw new ArgumentOutOfRangeException(nameof(sourceBytesToCopy));
            }

            Memmove((byte*)destination, (byte*)source, checked((nuint)sourceBytesToCopy));
        }

        internal unsafe static void Memcpy(byte* dest, byte* src, int len)
        {
            Debug.Assert(len >= 0, "Negative length in memcpy!");
            Memmove(dest, src, (nuint)len);
        }

        // This method has different signature for x64 and other platforms and is done for performance reasons.
        internal unsafe static void Memmove(byte* dest, byte* src, nuint len)
        {
#if AMD64 || (BIT32 && !ARM)
            const nuint CopyThreshold = 2048;
#else
            const nuint CopyThreshold = 512;
#endif // AMD64 || (BIT32 && !ARM)

            // P/Invoke into the native version when the buffers are overlapping.

            if (((nuint)dest - (nuint)src < len) || ((nuint)src - (nuint)dest < len)) goto PInvoke;

            byte* srcEnd = src + len;
            byte* destEnd = dest + len;

            if (len <= 16) goto MCPY02;
            if (len > 64) goto MCPY05;

            MCPY00:
            // Copy bytes which are multiples of 16 and leave the remainder for MCPY01 to handle.
            Debug.Assert(len > 16 && len <= 64);
#if HAS_CUSTOM_BLOCKS
            *(Block16*)dest = *(Block16*)src;                   // [0,16]
#elif BIT64
            *(long*)dest = *(long*)src;
            *(long*)(dest + 8) = *(long*)(src + 8);             // [0,16]
#else
            *(int*)dest = *(int*)src;
            *(int*)(dest + 4) = *(int*)(src + 4);
            *(int*)(dest + 8) = *(int*)(src + 8);
            *(int*)(dest + 12) = *(int*)(src + 12);             // [0,16]
#endif
            if (len <= 32) goto MCPY01;
#if HAS_CUSTOM_BLOCKS
            *(Block16*)(dest + 16) = *(Block16*)(src + 16);     // [0,32]
#elif BIT64
            *(long*)(dest + 16) = *(long*)(src + 16);
            *(long*)(dest + 24) = *(long*)(src + 24);           // [0,32]
#else
            *(int*)(dest + 16) = *(int*)(src + 16);
            *(int*)(dest + 20) = *(int*)(src + 20);
            *(int*)(dest + 24) = *(int*)(src + 24);
            *(int*)(dest + 28) = *(int*)(src + 28);             // [0,32]
#endif
            if (len <= 48) goto MCPY01;
#if HAS_CUSTOM_BLOCKS
            *(Block16*)(dest + 32) = *(Block16*)(src + 32);     // [0,48]
#elif BIT64
            *(long*)(dest + 32) = *(long*)(src + 32);
            *(long*)(dest + 40) = *(long*)(src + 40);           // [0,48]
#else
            *(int*)(dest + 32) = *(int*)(src + 32);
            *(int*)(dest + 36) = *(int*)(src + 36);
            *(int*)(dest + 40) = *(int*)(src + 40);
            *(int*)(dest + 44) = *(int*)(src + 44);             // [0,48]
#endif

            MCPY01:
            // Unconditionally copy the last 16 bytes using destEnd and srcEnd and return.
            Debug.Assert(len > 16 && len <= 64);
#if HAS_CUSTOM_BLOCKS
            *(Block16*)(destEnd - 16) = *(Block16*)(srcEnd - 16);
#elif BIT64
            *(long*)(destEnd - 16) = *(long*)(srcEnd - 16);
            *(long*)(destEnd - 8) = *(long*)(srcEnd - 8);
#else
            *(int*)(destEnd - 16) = *(int*)(srcEnd - 16);
            *(int*)(destEnd - 12) = *(int*)(srcEnd - 12);
            *(int*)(destEnd - 8) = *(int*)(srcEnd - 8);
            *(int*)(destEnd - 4) = *(int*)(srcEnd - 4);
#endif
            return;

            MCPY02:
            // Copy the first 8 bytes and then unconditionally copy the last 8 bytes and return.
            if ((len & 24) == 0) goto MCPY03;
            Debug.Assert(len >= 8 && len <= 16);
#if BIT64
            *(long*)dest = *(long*)src;
            *(long*)(destEnd - 8) = *(long*)(srcEnd - 8);
#else
            *(int*)dest = *(int*)src;
            *(int*)(dest + 4) = *(int*)(src + 4);
            *(int*)(destEnd - 8) = *(int*)(srcEnd - 8);
            *(int*)(destEnd - 4) = *(int*)(srcEnd - 4);
#endif
            return;

            MCPY03:
            // Copy the first 4 bytes and then unconditionally copy the last 4 bytes and return.
            if ((len & 4) == 0) goto MCPY04;
            Debug.Assert(len >= 4 && len < 8);
            *(int*)dest = *(int*)src;
            *(int*)(destEnd - 4) = *(int*)(srcEnd - 4);
            return;

            MCPY04:
            // Copy the first byte. For pending bytes, do an unconditionally copy of the last 2 bytes and return.
            Debug.Assert(len < 4);
            if (len == 0) return;
            *dest = *src;
            if ((len & 2) == 0) return;
            *(short*)(destEnd - 2) = *(short*)(srcEnd - 2);
            return;

            MCPY05:
            // PInvoke to the native version when the copy length exceeds the threshold.
            if (len > CopyThreshold)
            {
                goto PInvoke;
            }

            // Copy 64-bytes at a time until the remainder is less than 64.
            // If remainder is greater than 16 bytes, then jump to MCPY00. Otherwise, unconditionally copy the last 16 bytes and return.
            Debug.Assert(len > 64 && len <= CopyThreshold);
            nuint n = len >> 6;

            MCPY06:
#if HAS_CUSTOM_BLOCKS
            *(Block64*)dest = *(Block64*)src;
#elif BIT64
            *(long*)dest = *(long*)src;
            *(long*)(dest + 8) = *(long*)(src + 8);
            *(long*)(dest + 16) = *(long*)(src + 16);
            *(long*)(dest + 24) = *(long*)(src + 24);
            *(long*)(dest + 32) = *(long*)(src + 32);
            *(long*)(dest + 40) = *(long*)(src + 40);
            *(long*)(dest + 48) = *(long*)(src + 48);
            *(long*)(dest + 56) = *(long*)(src + 56);
#else
            *(int*)dest = *(int*)src;
            *(int*)(dest + 4) = *(int*)(src + 4);
            *(int*)(dest + 8) = *(int*)(src + 8);
            *(int*)(dest + 12) = *(int*)(src + 12);
            *(int*)(dest + 16) = *(int*)(src + 16);
            *(int*)(dest + 20) = *(int*)(src + 20);
            *(int*)(dest + 24) = *(int*)(src + 24);
            *(int*)(dest + 28) = *(int*)(src + 28);
            *(int*)(dest + 32) = *(int*)(src + 32);
            *(int*)(dest + 36) = *(int*)(src + 36);
            *(int*)(dest + 40) = *(int*)(src + 40);
            *(int*)(dest + 44) = *(int*)(src + 44);
            *(int*)(dest + 48) = *(int*)(src + 48);
            *(int*)(dest + 52) = *(int*)(src + 52);
            *(int*)(dest + 56) = *(int*)(src + 56);
            *(int*)(dest + 60) = *(int*)(src + 60);
#endif
            dest += 64;
            src += 64;
            n--;
            if (n != 0) goto MCPY06;

            len %= 64;
            if (len > 16) goto MCPY00;
#if HAS_CUSTOM_BLOCKS
            *(Block16*)(destEnd - 16) = *(Block16*)(srcEnd - 16);
#elif BIT64
            *(long*)(destEnd - 16) = *(long*)(srcEnd - 16);
            *(long*)(destEnd - 8) = *(long*)(srcEnd - 8);
#else
            *(int*)(destEnd - 16) = *(int*)(srcEnd - 16);
            *(int*)(destEnd - 12) = *(int*)(srcEnd - 12);
            *(int*)(destEnd - 8) = *(int*)(srcEnd - 8);
            *(int*)(destEnd - 4) = *(int*)(srcEnd - 4);
#endif
            return;

            PInvoke:
            _Memmove(dest, src, len);
        }

        // Non-inlinable wrapper around the QCall that avoids poluting the fast path
        // with P/Invoke prolog/epilog.
        [MethodImplAttribute(MethodImplOptions.NoInlining)]
        private static unsafe void _Memmove(byte* dest, byte* src, nuint len)
        {
            RuntimeImports.memmove(dest, src, len);
        }

#if HAS_CUSTOM_BLOCKS        
        [StructLayout(LayoutKind.Explicit, Size = 16)]
        private struct Block16 { }

        [StructLayout(LayoutKind.Explicit, Size = 64)]
        private struct Block64 { } 
#endif // HAS_CUSTOM_BLOCKS 
    }
}
