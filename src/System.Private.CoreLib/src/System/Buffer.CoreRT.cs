// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System.Diagnostics;
using System.Runtime;
using System.Runtime.CompilerServices;

#if BIT64
using nuint = System.UInt64;
#else
using nuint = System.UInt32;
#endif

namespace System
{
    partial class Buffer
    {
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static bool IsPrimitiveTypeArray(Array array) => array.ElementEEType.IsPrimitive;

        private static int _ByteLength(Array array)
        {
            return checked(array.Length * array.EETypePtr.ComponentSize);
        }


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

        internal static unsafe void Memcpy(byte* dest, byte* src, int len)
        {
            Debug.Assert(len >= 0, "Negative length in memcpy!");
            Memmove(dest, src, (nuint)len);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static unsafe void __Memmove(byte* dest, byte* src, nuint len) =>
            RuntimeImports.memmove(dest, src, len);
    }
}
