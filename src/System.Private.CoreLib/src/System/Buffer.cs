// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Runtime;
using System.Diagnostics.Contracts;
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
            if (src == null)
                throw new ArgumentNullException("src");
            if (dst == null)
                throw new ArgumentNullException("dst");


            RuntimeImports.RhCorElementTypeInfo srcCorElementTypeInfo = src.ElementEEType.CorElementTypeInfo;

            nuint uSrcLen = ((nuint)src.Length) << srcCorElementTypeInfo.Log2OfSize;
            nuint uDstLen = uSrcLen;

            if (!srcCorElementTypeInfo.IsPrimitive)
                throw new ArgumentException(SR.Arg_MustBePrimArray, "src");

            if (src != dst)
            {
                RuntimeImports.RhCorElementTypeInfo dstCorElementTypeInfo = dst.ElementEEType.CorElementTypeInfo;
                if (!dstCorElementTypeInfo.IsPrimitive)
                    throw new ArgumentException(SR.Arg_MustBePrimArray, "dst");
                uDstLen = ((nuint)dst.Length) << dstCorElementTypeInfo.Log2OfSize;
            }

            if (srcOffset < 0)
                throw new ArgumentOutOfRangeException(SR.ArgumentOutOfRange_MustBeNonNegInt32, "srcOffset");
            if (dstOffset < 0)
                throw new ArgumentOutOfRangeException(SR.ArgumentOutOfRange_MustBeNonNegInt32, "dstOffset");
            if (count < 0)
                throw new ArgumentOutOfRangeException(SR.ArgumentOutOfRange_MustBeNonNegInt32, "count");

            nuint uCount = (nuint)count;
            if (uSrcLen < ((nuint)srcOffset) + uCount)
                throw new ArgumentException(SR.Argument_InvalidOffLen);
            if (uDstLen < ((nuint)dstOffset) + uCount)
                throw new ArgumentException(SR.Argument_InvalidOffLen);

            if (uCount == 0)
                return;

            fixed (IntPtr* pSrcObj = &src.m_pEEType, pDstObj = &dst.m_pEEType)
            {
                byte* pSrc = (byte*)Array.GetAddrOfPinnedArrayFromEETypeField(pSrcObj) + srcOffset;
                byte* pDst = (byte*)Array.GetAddrOfPinnedArrayFromEETypeField(pDstObj) + dstOffset;

                Buffer.Memmove(pDst, pSrc, uCount);
            }
        }

        // This is ported from the optimized CRT assembly in memchr.asm. The JIT generates 
        // pretty good code here and this ends up being within a couple % of the CRT asm.
        // It is however cross platform as the CRT hasn't ported their fast version to 64-bit
        // platforms.
        //
        internal unsafe static int IndexOfByte(byte* src, byte value, int index, int count)
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

        internal unsafe static void ZeroMemory(byte* src, long len)
        {
            while (len-- > 0)
                *(src + len) = 0;
        }

        public static int ByteLength(Array array)
        {
            // Is the array present?
            if (array == null)
                throw new ArgumentNullException("array");

            // Is it of primitive types?
            if (!array.ElementEEType.IsPrimitive)
                throw new ArgumentException(SR.Arg_MustBePrimArray, "array");

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
                throw new ArgumentNullException("array");

            // Is it of primitive types?
            if (!array.ElementEEType.IsPrimitive)
                throw new ArgumentException(SR.Arg_MustBePrimArray, "array");

            // Is the index in valid range of the array?
            if (index < 0 || index >= _ByteLength(array))
                throw new ArgumentOutOfRangeException("index");

            fixed (IntPtr* pObj = &array.m_pEEType)
            {
                byte* pByte = (byte*)Array.GetAddrOfPinnedArrayFromEETypeField(pObj) + index;
                return *pByte;
            }
        }

        public static unsafe void SetByte(Array array, int index, byte value)
        {
            // Is the array present?
            if (array == null)
                throw new ArgumentNullException("array");

            // Is it of primitive types?
            if (!array.ElementEEType.IsPrimitive)
                throw new ArgumentException(SR.Arg_MustBePrimArray, "array");

            // Is the index in valid range of the array?
            if (index < 0 || index >= _ByteLength(array))
                throw new ArgumentOutOfRangeException("index");

            fixed (IntPtr* pObj = &array.m_pEEType)
            {
                byte* pByte = (byte*)Array.GetAddrOfPinnedArrayFromEETypeField(pObj) + index;
                *pByte = value;
            }
        }

        // The attributes on this method are chosen for best JIT performance. 
        // Please do not edit unless intentional.
        [MethodImplAttribute(MethodImplOptions.AggressiveInlining)]
        [CLSCompliant(false)]
        public static unsafe void MemoryCopy(void* source, void* destination, long destinationSizeInBytes, long sourceBytesToCopy)
        {
            if (sourceBytesToCopy > destinationSizeInBytes)
            {
                throw new ArgumentOutOfRangeException("sourceBytesToCopy");
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
                throw new ArgumentOutOfRangeException("sourceBytesToCopy");
            }

            Memmove((byte*)destination, (byte*)source, checked((nuint)sourceBytesToCopy));
        }

        internal unsafe static void Memmove(byte* dest, byte* src, nuint len)
        {
            // P/Invoke into the native version when the buffers are overlapping and the copy needs to be performed backwards
            // This check can produce false positives for lengths greater than Int32.MaxInt. It is fine because we want to use PInvoke path for the large lengths anyway.
            if ((nuint)dest - (nuint)src < len)
            {
                _Memmove(dest, src, len);
                return;
            }

            //
            // This is portable version of memcpy. It mirrors what the hand optimized assembly versions of memcpy typically do.
            //

#if ALIGN_ACCESS
#error Needs porting for ALIGN_ACCESS (https://github.com/dotnet/corert/issues/430)
#else // ALIGN_ACCESS
            switch (len)
            {
                case 0:
                    return;
                case 1:
                    *dest = *src;
                    return;
                case 2:
                    *(short*)dest = *(short*)src;
                    return;
                case 3:
                    *(short*)dest = *(short*)src;
                    *(dest + 2) = *(src + 2);
                    return;
                case 4:
                    *(int*)dest = *(int*)src;
                    return;
                case 5:
                    *(int*)dest = *(int*)src;
                    *(dest + 4) = *(src + 4);
                    return;
                case 6:
                    *(int*)dest = *(int*)src;
                    *(short*)(dest + 4) = *(short*)(src + 4);
                    return;
                case 7:
                    *(int*)dest = *(int*)src;
                    *(short*)(dest + 4) = *(short*)(src + 4);
                    *(dest + 6) = *(src + 6);
                    return;
                case 8:
#if BIT64
                    *(long*)dest = *(long*)src;
#else
                    *(int*)dest = *(int*)src;
                    *(int*)(dest + 4) = *(int*)(src + 4);
#endif
                    return;
                case 9:
#if BIT64
                    *(long*)dest = *(long*)src;
#else
                    *(int*)dest = *(int*)src;
                    *(int*)(dest + 4) = *(int*)(src + 4);
#endif
                    *(dest + 8) = *(src + 8);
                    return;
                case 10:
#if BIT64
                    *(long*)dest = *(long*)src;
#else
                    *(int*)dest = *(int*)src;
                    *(int*)(dest + 4) = *(int*)(src + 4);
#endif
                    *(short*)(dest + 8) = *(short*)(src + 8);
                    return;
                case 11:
#if BIT64
                    *(long*)dest = *(long*)src;
#else
                    *(int*)dest = *(int*)src;
                    *(int*)(dest + 4) = *(int*)(src + 4);
#endif
                    *(short*)(dest + 8) = *(short*)(src + 8);
                    *(dest + 10) = *(src + 10);
                    return;
                case 12:
#if BIT64
                    *(long*)dest = *(long*)src;
#else
                    *(int*)dest = *(int*)src;
                    *(int*)(dest + 4) = *(int*)(src + 4);
#endif
                    *(int*)(dest + 8) = *(int*)(src + 8);
                    return;
                case 13:
#if BIT64
                    *(long*)dest = *(long*)src;
#else
                    *(int*)dest = *(int*)src;
                    *(int*)(dest + 4) = *(int*)(src + 4);
#endif
                    *(int*)(dest + 8) = *(int*)(src + 8);
                    *(dest + 12) = *(src + 12);
                    return;
                case 14:
#if BIT64
                    *(long*)dest = *(long*)src;
#else
                    *(int*)dest = *(int*)src;
                    *(int*)(dest + 4) = *(int*)(src + 4);
#endif
                    *(int*)(dest + 8) = *(int*)(src + 8);
                    *(short*)(dest + 12) = *(short*)(src + 12);
                    return;
                case 15:
#if BIT64
                    *(long*)dest = *(long*)src;
#else
                    *(int*)dest = *(int*)src;
                    *(int*)(dest + 4) = *(int*)(src + 4);
#endif
                    *(int*)(dest + 8) = *(int*)(src + 8);
                    *(short*)(dest + 12) = *(short*)(src + 12);
                    *(dest + 14) = *(src + 14);
                    return;
                case 16:
#if BIT64
                    *(long*)dest = *(long*)src;
                    *(long*)(dest + 8) = *(long*)(src + 8);
#else
                    *(int*)dest = *(int*)src;
                    *(int*)(dest + 4) = *(int*)(src + 4);
                    *(int*)(dest + 8) = *(int*)(src + 8);
                    *(int*)(dest + 12) = *(int*)(src + 12);
#endif
                    return;
                default:
                    break;
            }

            // P/Invoke into the native version for large lengths.
            if (len >= 200)
            {
                _Memmove(dest, src, len);
                return;
            }

            if (((int)dest & 3) != 0)
            {
                if (((int)dest & 1) != 0)
                {
                    *dest = *src;
                    src++;
                    dest++;
                    len--;
                    if (((int)dest & 2) == 0)
                        goto Aligned;
                }
                *(short*)dest = *(short*)src;
                src += 2;
                dest += 2;
                len -= 2;
            Aligned:;
            }

#if BIT64
            if (((int)dest & 4) != 0)
            {
                *(int*)dest = *(int*)src;
                src += 4;
                dest += 4;
                len -= 4;
            }
#endif

            nuint count = len / 16;
            while (count > 0)
            {
#if BIT64
                ((long*)dest)[0] = ((long*)src)[0];
                ((long*)dest)[1] = ((long*)src)[1];
#else
                ((int*)dest)[0] = ((int*)src)[0];
                ((int*)dest)[1] = ((int*)src)[1];
                ((int*)dest)[2] = ((int*)src)[2];
                ((int*)dest)[3] = ((int*)src)[3];
#endif
                dest += 16;
                src += 16;
                count--;
            }

            if ((len & 8) != 0)
            {
#if BIT64
                ((long*)dest)[0] = ((long*)src)[0];
#else
                ((int*)dest)[0] = ((int*)src)[0];
                ((int*)dest)[1] = ((int*)src)[1];
#endif
                dest += 8;
                src += 8;
            }
            if ((len & 4) != 0)
            {
                ((int*)dest)[0] = ((int*)src)[0];
                dest += 4;
                src += 4;
            }
            if ((len & 2) != 0)
            {
                ((short*)dest)[0] = ((short*)src)[0];
                dest += 2;
                src += 2;
            }
            if ((len & 1) != 0)
                *dest = *src;
#endif // ALIGN_ACCESS
        }

        // Non-inlinable wrapper around the QCall that avoids poluting the fast path
        // with P/Invoke prolog/epilog.
        [MethodImplAttribute(MethodImplOptions.NoInlining)]
        private unsafe static void _Memmove(byte* dest, byte* src, nuint len)
        {
            RuntimeImports.memmove(dest, src, len);
        }
    }
}
