// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

//
// This is where we group together all the runtime export calls.
//

using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Runtime.CompilerServices;

using Internal.Runtime;

namespace System.Runtime
{
    internal static class RuntimeExports
    {
        //
        // internal calls for allocation
        //
        [RuntimeExport("RhNewObject")]
        public unsafe static object RhNewObject(EETypePtr pEEType)
        {
            EEType* ptrEEType = (EEType*)pEEType.ToPointer();
#if FEATURE_64BIT_ALIGNMENT
            if (ptrEEType->RequiresAlign8)
            {
                if (ptrEEType->IsValueType)
                    return InternalCalls.RhpNewFastMisalign(ptrEEType);
                if (ptrEEType->IsFinalizable)
                    return InternalCalls.RhpNewFinalizableAlign8(ptrEEType);
                return InternalCalls.RhpNewFastAlign8(ptrEEType);
            }
            else
#endif // FEATURE_64BIT_ALIGNMENT
            {
                if (ptrEEType->IsFinalizable)
                    return InternalCalls.RhpNewFinalizable(ptrEEType);
                return InternalCalls.RhpNewFast(ptrEEType);
            }
        }

        [RuntimeExport("RhNewArray")]
        public unsafe static object RhNewArray(EETypePtr pEEType, int length)
        {
            EEType* ptrEEType = (EEType*)pEEType.ToPointer();
#if FEATURE_64BIT_ALIGNMENT
            if (ptrEEType->RequiresAlign8)
            {
                return InternalCalls.RhpNewArrayAlign8(ptrEEType, length);
            }
            else
#endif // FEATURE_64BIT_ALIGNMENT
            {
                return InternalCalls.RhpNewArray(ptrEEType, length);
            }
        }

        [RuntimeExport("RhBox")]
        public unsafe static object RhBox(EETypePtr pEEType, ref byte data)
        {
            EEType* ptrEEType = (EEType*)pEEType.ToPointer();
            int dataOffset = 0;
            object result;

            // If we're boxing a Nullable<T> then either box the underlying T or return null (if the
            // nullable's value is empty).
            if (ptrEEType->IsNullable)
            {
                // The boolean which indicates whether the value is null comes first in the Nullable struct.
                if (data == 0)
                    return null;

                // Switch type we're going to box to the Nullable<T> target type and advance the data pointer
                // to the value embedded within the nullable.
                dataOffset = ptrEEType->NullableValueOffset;
                ptrEEType = ptrEEType->NullableType;
            }

#if FEATURE_64BIT_ALIGNMENT
            if (ptrEEType->RequiresAlign8)
            {
                result = InternalCalls.RhpNewFastMisalign(ptrEEType);
            }
            else
#endif // FEATURE_64BIT_ALIGNMENT
            {
                result = InternalCalls.RhpNewFast(ptrEEType);
            }
            InternalCalls.RhpBox(result, ref Unsafe.Add(ref data, dataOffset));
            return result;
        }

        [RuntimeExport("RhBoxAny")]
        public unsafe static object RhBoxAny(ref byte data, EETypePtr pEEType)
        {
            EEType* ptrEEType = (EEType*)pEEType.ToPointer();
            if (ptrEEType->IsValueType)
            {
                return RhBox(pEEType, ref data);
            }
            else
            {
                return Unsafe.As<byte, Object>(ref data);
            }
        }

        private unsafe static bool UnboxAnyTypeCompare(EEType *pEEType, EEType *ptrUnboxToEEType)
        {
            bool result = false;

            if (pEEType->CorElementType == ptrUnboxToEEType->CorElementType)
            {
                result = TypeCast.AreTypesEquivalentInternal(pEEType, ptrUnboxToEEType);

                if (!result)
                {
                    // Enum's and primitive types should pass the UnboxAny exception cases
                    // if they have an exactly matching cor element type.
                    switch (ptrUnboxToEEType->CorElementType)
                    {
                        case CorElementType.ELEMENT_TYPE_I1:
                        case CorElementType.ELEMENT_TYPE_U1:
                        case CorElementType.ELEMENT_TYPE_I2:
                        case CorElementType.ELEMENT_TYPE_U2:
                        case CorElementType.ELEMENT_TYPE_I4:
                        case CorElementType.ELEMENT_TYPE_U4:
                        case CorElementType.ELEMENT_TYPE_I8:
                        case CorElementType.ELEMENT_TYPE_U8:
                        case CorElementType.ELEMENT_TYPE_I:
                        case CorElementType.ELEMENT_TYPE_U:
                            result = true;
                            break;
                    }
                }
            }

            return result;
        }

        [RuntimeExport("RhUnboxAny")]
        public unsafe static void RhUnboxAny(object o, ref byte data, EETypePtr pUnboxToEEType)
        {
            EEType* ptrUnboxToEEType = (EEType*)pUnboxToEEType.ToPointer();
            if (ptrUnboxToEEType->IsValueType)
            {
                bool isValid = false;

                if (ptrUnboxToEEType->IsNullable)
                {
                    isValid = (o == null) || TypeCast.AreTypesEquivalentInternal(o.EEType, ptrUnboxToEEType->NullableType);
                }
                else
                {
                    isValid = (o != null) && UnboxAnyTypeCompare(o.EEType, ptrUnboxToEEType);
                }

                if (!isValid)
                {
                    // Throw the invalid cast exception defined by the classlib, using the input unbox EEType* 
                    // to find the correct classlib.

                    ExceptionIDs exID = o == null ? ExceptionIDs.NullReference : ExceptionIDs.InvalidCast;

                    throw ptrUnboxToEEType->GetClasslibException(exID);
                }

                InternalCalls.RhUnbox(o, ref data, ptrUnboxToEEType);
            }
            else
            {
                if (o != null && (TypeCast.IsInstanceOf(o, ptrUnboxToEEType) == null))
                {
                    throw ptrUnboxToEEType->GetClasslibException(ExceptionIDs.InvalidCast);
                }

                Unsafe.As<byte, Object>(ref data) = o;
            }
        }

        //
        // Unbox helpers with RyuJIT conventions
        //
        [RuntimeExport("RhUnbox2")]
        static public unsafe ref byte RhUnbox2(EETypePtr pUnboxToEEType, Object obj)
        {
            EEType * ptrUnboxToEEType = (EEType *)pUnboxToEEType.ToPointer();
            if (obj.EEType != ptrUnboxToEEType)
            {
                // We allow enums and their primtive type to be interchangable
                if (obj.EEType->CorElementType != ptrUnboxToEEType->CorElementType)
                {
                    throw ptrUnboxToEEType->GetClasslibException(ExceptionIDs.InvalidCast);
                }
            }
            return ref obj.GetRawData();
        }

        [RuntimeExport("RhUnboxNullable")]
        static public unsafe void RhUnboxNullable(ref byte data, EETypePtr pUnboxToEEType, Object obj)
        {
            EEType* ptrUnboxToEEType = (EEType*)pUnboxToEEType.ToPointer();
            if ((obj != null) && (obj.EEType != ptrUnboxToEEType->NullableType))
            {
                throw ptrUnboxToEEType->GetClasslibException(ExceptionIDs.InvalidCast);
            }
            InternalCalls.RhUnbox(obj, ref data, ptrUnboxToEEType);
        }

        [RuntimeExport("RhArrayStoreCheckAny")]
        static public unsafe void RhArrayStoreCheckAny(object array, ref byte data)
        {
            if (array == null)
            {
                return;
            }

            Debug.Assert(array.EEType->IsArray, "first argument must be an array");

            EEType* arrayElemType = array.EEType->RelatedParameterType;
            if (arrayElemType->IsValueType)
            {
                return;
            }

            TypeCast.CheckArrayStore(array, Unsafe.As<byte, Object>(ref data));
        }

        [RuntimeExport("RhBoxAndNullCheck")]
        static public unsafe bool RhBoxAndNullCheck(ref byte data, EETypePtr pEEType)
        {
            EEType* ptrEEType = (EEType*)pEEType.ToPointer();
            if (ptrEEType->IsValueType)
                return true;
            else
                return Unsafe.As<byte, Object>(ref data) != null;
        }

#pragma warning disable 169 // The field 'System.Runtime.RuntimeExports.Wrapper.o' is never used. 
        private class Wrapper
        {
            private Object _o;
        }
#pragma warning restore 169

        [RuntimeExport("RhAllocLocal")]
        public unsafe static object RhAllocLocal(EETypePtr pEEType)
        {
            EEType* ptrEEType = (EEType*)pEEType.ToPointer();
            if (ptrEEType->IsValueType)
            {
#if FEATURE_64BIT_ALIGNMENT
                if (ptrEEType->RequiresAlign8)
                    return InternalCalls.RhpNewFastMisalign(ptrEEType);
#endif
                return InternalCalls.RhpNewFast(ptrEEType);
            }
            else
                return new Wrapper();
        }

        [RuntimeExport("RhMemberwiseClone")]
        public unsafe static object RhMemberwiseClone(object src)
        {
            object objClone;

            if (src.EEType->IsArray)
                objClone = RhNewArray(new EETypePtr((IntPtr)src.EEType), src.GetArrayLength());
            else
                objClone = RhNewObject(new EETypePtr((IntPtr)src.EEType));

            InternalCalls.RhpCopyObjectContents(objClone, src);

            return objClone;
        }

        [RuntimeExport("RhpReversePInvokeBadTransition")]
        public static void RhpReversePInvokeBadTransition(IntPtr returnAddress)
        {
            EH.FailFastViaClasslib(
                RhFailFastReason.IllegalNativeCallableEntry,
                null,
                returnAddress);
        }

        [RuntimeExport("RhGetCurrentThreadStackTrace")]
        [MethodImpl(MethodImplOptions.NoInlining)] // Ensures that the RhGetCurrentThreadStackTrace frame is always present
        public static unsafe int RhGetCurrentThreadStackTrace(IntPtr[] outputBuffer)
        {
            fixed (IntPtr* pOutputBuffer = outputBuffer)
                return RhpGetCurrentThreadStackTrace(pOutputBuffer, (uint)((outputBuffer != null) ? outputBuffer.Length : 0));
        }

        [DllImport(Redhawk.BaseName, CallingConvention = CallingConvention.Cdecl)]
        private static unsafe extern int RhpGetCurrentThreadStackTrace(IntPtr* pOutputBuffer, uint outputBufferLength);

        // Worker for RhGetCurrentThreadStackTrace.  RhGetCurrentThreadStackTrace just allocates a transition
        // frame that will be used to seed the stack trace and this method does all the real work.
        //
        // Input:           outputBuffer may be null or non-null
        // Return value:    positive: number of entries written to outputBuffer
        //                  negative: number of required entries in outputBuffer in case it's too small (or null)
        // Output:          outputBuffer is filled in with return address IPs, starting with placing the this
        //                  method's return address into index 0
        //
        // NOTE: We don't want to allocate the array on behalf of the caller because we don't know which class
        // library's objects the caller understands (we support multiple class libraries with multiple root
        // System.Object types).
        [NativeCallable(EntryPoint = "RhpCalculateStackTraceWorker", CallingConvention = CallingConvention.Cdecl)]
        private static unsafe int RhpCalculateStackTraceWorker(IntPtr * pOutputBuffer, uint outputBufferLength)
        {
            uint nFrames = 0;
            bool success = true;

            StackFrameIterator frameIter = new StackFrameIterator();

            bool isValid = frameIter.Init(null);
            Debug.Assert(isValid, "Missing RhGetCurrentThreadStackTrace frame");

            // Note that the while loop will skip RhGetCurrentThreadStackTrace frame
            while (frameIter.Next())
            {
                if (nFrames < outputBufferLength)
                    pOutputBuffer[nFrames] = new IntPtr(frameIter.ControlPC);
                else
                    success = false;

                nFrames++;
            }

            return success ? (int)nFrames : -(int)nFrames;
        }

        // The GC conservative reporting descriptor is a special structure of data that the GC
        // parses to determine whether there are specific regions of memory that it should not
        // collect or move around.
        // During garbage collection, the GC will inspect the data in this structure, and verify that:
        //  1) _magic is set to the magic number (also hard coded on the GC side)
        //  2) The reported region is valid (checks alignments, size, within bounds of the thread memory, etc...)
        //  3) The ConservativelyReportedRegionDesc pointer must be reported by a frame which does not make a pinvoke transition.
        //  4) The value of the _hash field is the computed hash of _regionPointerLow with _regionPointerHigh
        //  5) The region must be IntPtr aligned, and have a size which is also IntPtr aligned
        // If all conditions are satisfied, the region of memory starting at _regionPointerLow and ending at
        // _regionPointerHigh will be conservatively reported.
        // This can only be used to report memory regions on the current stack and the structure must itself 
        // be located on the stack.
        public struct ConservativelyReportedRegionDesc
        {
            internal const ulong MagicNumber64 = 0x87DF7A104F09E0A9UL;
            internal const uint MagicNumber32 = 0x4F09E0A9;

            internal UIntPtr _magic;
            internal UIntPtr _regionPointerLow;
            internal UIntPtr _regionPointerHigh;
            internal UIntPtr _hash;
        }

        [RuntimeExport("RhInitializeConservativeReportingRegion")]
        public static unsafe void RhInitializeConservativeReportingRegion(ConservativelyReportedRegionDesc* regionDesc, void* bufferBegin, int cbBuffer)
        {
            Debug.Assert((((int)bufferBegin) & (sizeof(IntPtr) - 1)) == 0, "Buffer not IntPtr aligned");
            Debug.Assert((cbBuffer & (sizeof(IntPtr) - 1)) == 0, "Size of buffer not IntPtr aligned");

            UIntPtr regionPointerLow = (UIntPtr)bufferBegin;
            UIntPtr regionPointerHigh = (UIntPtr)(((byte*)bufferBegin) + cbBuffer);

            // Setup pointers to start and end of region
            regionDesc->_regionPointerLow = regionPointerLow;
            regionDesc->_regionPointerHigh = regionPointerHigh;

            // Activate the region for processing
#if BIT64
            ulong hash = ConservativelyReportedRegionDesc.MagicNumber64;
            hash = ((hash << 13) ^ hash) ^ (ulong)regionPointerLow;
            hash = ((hash << 13) ^ hash) ^ (ulong)regionPointerHigh;

            regionDesc->_hash = new UIntPtr(hash);
            regionDesc->_magic = new UIntPtr(ConservativelyReportedRegionDesc.MagicNumber64);
#else
            uint hash = ConservativelyReportedRegionDesc.MagicNumber32;
            hash = ((hash << 13) ^ hash) ^ (uint)regionPointerLow;
            hash = ((hash << 13) ^ hash) ^ (uint)regionPointerHigh;

            regionDesc->_hash = new UIntPtr(hash);
            regionDesc->_magic = new UIntPtr(ConservativelyReportedRegionDesc.MagicNumber32);
#endif
        }

        // Disable conservative reporting 
        [RuntimeExport("RhDisableConservativeReportingRegion")]
        public static unsafe void RhDisableConservativeReportingRegion(ConservativelyReportedRegionDesc* regionDesc)
        {
            regionDesc->_magic = default(UIntPtr);
        }
    }
}
