// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

//
// This is where we group together all the runtime export calls.
//

using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Runtime.CompilerServices;

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
        public unsafe static object RhBox(EETypePtr pEEType, void* pData)
        {
            EEType* ptrEEType = (EEType*)pEEType.ToPointer();
            object result;

            // If we're boxing a Nullable<T> then either box the underlying T or return null (if the
            // nullable's value is empty).
            if (ptrEEType->IsNullable)
            {
                // The boolean which indicates whether the value is null comes first in the Nullable struct.
                if (!*(bool*)pData)
                    return null;

                // Switch type we're going to box to the Nullable<T> target type and advance the data pointer
                // to the value embedded within the nullable.
                pData = (byte*)pData + ptrEEType->GetNullableValueOffset();
                ptrEEType = ptrEEType->GetNullableType();
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
            InternalCalls.RhpBox(result, pData);
            return result;
        }

        // this serves as a kind of union where:
        // - the field o is used if the struct wraps a reference type
        // - the field p is used together with pointer arithmetic if the struct is a valuetype
        public struct Hack_o_p
        {
            internal Object o;
            internal IntPtr p;
        }

        [RuntimeExport("RhBoxAny")]
        public unsafe static object RhBoxAny(ref Hack_o_p data, EETypePtr pEEType)
        {
            EEType* ptrEEType = (EEType*)pEEType.ToPointer();
            if (ptrEEType->IsValueType)
            {
                // HACK: we would really want to take the address of o here,
                // but the rules of the C# language don't let us do that,
                // so we arrive at the same result by taking the address of p
                // and going back one pointer-sized unit
                fixed (IntPtr* pData = &data.p)
                    return RhBox(pEEType, pData - 1);
            }
            else
                return data.o;
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
                        case TypeCast.CorElementType.ELEMENT_TYPE_I1:
                        case TypeCast.CorElementType.ELEMENT_TYPE_U1:
                        case TypeCast.CorElementType.ELEMENT_TYPE_I2:
                        case TypeCast.CorElementType.ELEMENT_TYPE_U2:
                        case TypeCast.CorElementType.ELEMENT_TYPE_I4:
                        case TypeCast.CorElementType.ELEMENT_TYPE_U4:
                        case TypeCast.CorElementType.ELEMENT_TYPE_I8:
                        case TypeCast.CorElementType.ELEMENT_TYPE_U8:
                        case TypeCast.CorElementType.ELEMENT_TYPE_I:
                        case TypeCast.CorElementType.ELEMENT_TYPE_U:
                            result = true;
                            break;
                    }
                }
            }

            return result;
        }

        [RuntimeExport("RhUnboxAny")]
        public unsafe static void RhUnboxAny(object o, ref Hack_o_p data, EETypePtr pUnboxToEEType)
        {
            EEType* ptrUnboxToEEType = (EEType*)pUnboxToEEType.ToPointer();
            if (ptrUnboxToEEType->IsValueType)
            {
                // HACK: we would really want to take the address of o here,
                // but the rules of the C# language don't let us do that,
                // so we arrive at the same result by taking the address of p
                // and going back one pointer-sized unit
                fixed (IntPtr* pData = &data.p)
                {
                    bool isValid = false;

                    if (ptrUnboxToEEType->IsNullable)
                        isValid = (o == null) || TypeCast.AreTypesEquivalentInternal(o.EEType, ptrUnboxToEEType->GetNullableType());
                    else if (o != null)
                    {
                        isValid = UnboxAnyTypeCompare(o.EEType, ptrUnboxToEEType);
                    }

                    if (!isValid)
                    {
                        // Throw the invalid cast exception defined by the classlib, using the input unbox EEType* 
                        // to find the correct classlib.

                        ExceptionIDs exID = o == null ? ExceptionIDs.NullReference : ExceptionIDs.InvalidCast;

                        Exception e = ptrUnboxToEEType->GetClasslibException(exID);

                        BinderIntrinsics.TailCall_RhpThrowEx(e);
                    }
                    InternalCalls.RhUnbox(o, pData - 1, ptrUnboxToEEType);
                }
            }
            else
            {
                if (o == null || (TypeCast.IsInstanceOf(o, ptrUnboxToEEType) != null))
                {
                    data.o = o;
                }
                else
                {
                    Exception e = ptrUnboxToEEType->GetClasslibException(ExceptionIDs.InvalidCast);

                    BinderIntrinsics.TailCall_RhpThrowEx(e);
                }
            }
        }

#if CORERT
        //
        // Unbox helpers with RyuJIT conventions
        //
        [RuntimeExport("RhUnbox2")]
        static public unsafe void* RhUnbox2(EETypePtr pUnboxToEEType, Object obj)
        {
            EEType * ptrUnboxToEEType = (EEType *)pUnboxToEEType.ToPointer();
            if (obj.EEType != ptrUnboxToEEType)
            {
                // We allow enums and their primtive type to be interchangable
                if (obj.EEType->CorElementType != ptrUnboxToEEType->CorElementType)
                {
                    Exception e = ptrUnboxToEEType->GetClasslibException(ExceptionIDs.InvalidCast);

                    BinderIntrinsics.TailCall_RhpThrowEx(e);
                }
            }

            fixed (void* pObject = &obj.m_pEEType)
            {
                // CORERT-TODO: This code has GC hole - the method return type should really be byref.
                // Requires byref returns in C# to fix cleanly (https://github.com/dotnet/roslyn/issues/118)
                return (IntPtr*)pObject + 1;
            }
        }

        [RuntimeExport("RhUnboxNullable")]
        static public unsafe void RhUnboxNullable(ref Hack_o_p data, EETypePtr pUnboxToEEType, Object obj)
        {
            EEType* ptrUnboxToEEType = (EEType*)pUnboxToEEType.ToPointer();

            // HACK: we would really want to take the address of o here,
            // but the rules of the C# language don't let us do that,
            // so we arrive at the same result by taking the address of p
            // and going back one pointer-sized unit
            fixed (IntPtr* pData = &data.p)
            {
                if ((obj != null) && (obj.EEType != ptrUnboxToEEType->GetNullableType()))
                {
                    Exception e = ptrUnboxToEEType->GetClasslibException(ExceptionIDs.InvalidCast);

                    BinderIntrinsics.TailCall_RhpThrowEx(e);
                }
                InternalCalls.RhUnbox(obj, pData - 1, ptrUnboxToEEType);
            }
        }
#endif // CORERT

        [RuntimeExport("RhArrayStoreCheckAny")]
        static public unsafe void RhArrayStoreCheckAny(object array, ref Hack_o_p data)
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

            TypeCast.CheckArrayStore(array, data.o);
        }

        [RuntimeExport("RhBoxAndNullCheck")]
        static public unsafe bool RhBoxAndNullCheck(ref Hack_o_p data, EETypePtr pEEType)
        {
            EEType* ptrEEType = (EEType*)pEEType.ToPointer();
            if (ptrEEType->IsValueType)
                return true;
            else
                return data.o != null;
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

        // EEType interrogation methods.

        [RuntimeExport("RhGetRelatedParameterType")]
        public static unsafe EETypePtr RhGetRelatedParameterType(EETypePtr ptrEEType)
        {
            EEType* pEEType = ptrEEType.ToPointer();
            return new EETypePtr((IntPtr)pEEType->RelatedParameterType);
        }

        [RuntimeExport("RhGetNonArrayBaseType")]
        public static unsafe EETypePtr RhGetNonArrayBaseType(EETypePtr ptrEEType)
        {
            EEType* pEEType = ptrEEType.ToPointer();
            return new EETypePtr((IntPtr)pEEType->NonArrayBaseType);
        }

        [RuntimeExport("RhGetComponentSize")]
        public static unsafe ushort RhGetComponentSize(EETypePtr ptrEEType)
        {
            EEType* pEEType = ptrEEType.ToPointer();
            return pEEType->ComponentSize;
        }

        [RuntimeExport("RhGetBaseSize")]
        public static unsafe uint RhGetBaseSize(EETypePtr ptrEEType)
        {
            EEType* pEEType = ptrEEType.ToPointer();
            return pEEType->BaseSize;
        }

        [RuntimeExport("RhGetNumInterfaces")]
        public static unsafe uint RhGetNumInterfaces(EETypePtr ptrEEType)
        {
            EEType* pEEType = ptrEEType.ToPointer();
            return (uint)pEEType->NumInterfaces;
        }

        [RuntimeExport("RhGetInterface")]
        public static unsafe EETypePtr RhGetInterface(EETypePtr ptrEEType, uint index)
        {
            EEType* pEEType = ptrEEType.ToPointer();

            // The convoluted pointer arithmetic into the interface map below (rather than a simply array
            // dereference) is because C# will generate a 64-bit multiply for the lookup by default. This
            // causes us a problem on x86 because it uses a helper that's mapped directly into the CRT via
            // import magic and that technique doesn't work with the way we link this code into the runtime
            // image. Since we don't need a 64-bit multiply here (the classlib is trusted code) we manually
            // perform the calculation.
            EEInterfaceInfo* pInfo = (EEInterfaceInfo*)((byte*)pEEType->InterfaceMap + (index * (uint)sizeof(EEInterfaceInfo)));

            return new EETypePtr((IntPtr)pInfo->InterfaceType);
        }

        [RuntimeExport("RhSetInterface")]
        public static unsafe void RhSetInterface(EETypePtr ptrEEType, int index, EETypePtr ptrInterfaceEEType)
        {
            EEType* pEEType = ptrEEType.ToPointer();
            EEType* pInterfaceEEType = ptrInterfaceEEType.ToPointer();
            pEEType->InterfaceMap[index].InterfaceType = pInterfaceEEType;
        }

        [RuntimeExport("RhIsDynamicType")]
        public static unsafe bool RhIsDynamicType(EETypePtr ptrEEType)
        {
            EEType* pEEType = ptrEEType.ToPointer();
            return pEEType->IsDynamicType;
        }

        [RuntimeExport("RhHasCctor")]
        public static unsafe bool RhHasCctor(EETypePtr ptrEEType)
        {
            EEType* pEEType = ptrEEType.ToPointer();
            return pEEType->HasCctor;
        }

        [RuntimeExport("RhIsValueType")]
        public static unsafe bool RhIsValueType(EETypePtr ptrEEType)
        {
            EEType* pEEType = ptrEEType.ToPointer();
            return pEEType->IsValueType;
        }

        [RuntimeExport("RhIsInterface")]
        public static unsafe bool RhIsInterface(EETypePtr ptrEEType)
        {
            EEType* pEEType = ptrEEType.ToPointer();
            return pEEType->IsInterface;
        }

        [RuntimeExport("RhIsArray")]
        public static unsafe bool RhIsArray(EETypePtr ptrEEType)
        {
            EEType* pEEType = ptrEEType.ToPointer();
            return pEEType->IsArray;
        }

        [RuntimeExport("RhIsString")]
        public static unsafe bool RhIsString(EETypePtr ptrEEType)
        {
            EEType* pEEType = ptrEEType.ToPointer();
            // String is currently the only non-array type with a non-zero component size.
            return (pEEType->ComponentSize == sizeof(char)) && !pEEType->IsArray;
        }

        [RuntimeExport("RhIsNullable")]
        public static unsafe bool RhIsNullable(EETypePtr ptrEEType)
        {
            EEType* pEEType = ptrEEType.ToPointer();
            return pEEType->IsNullable;
        }

        [RuntimeExport("RhGetNullableType")]
        public static unsafe EETypePtr RhGetNullableType(EETypePtr ptrEEType)
        {
            EEType* pEEType = ptrEEType.ToPointer();
            return new EETypePtr((IntPtr)pEEType->GetNullableType());
        }

        [RuntimeExport("RhHasReferenceFields")]
        public static unsafe bool RhHasReferenceFields(EETypePtr ptrEEType)
        {
            EEType* pEEType = ptrEEType.ToPointer();
            return pEEType->HasReferenceFields;
        }

        [RuntimeExport("RhGetCorElementType")]
        public static unsafe byte RhGetCorElementType(EETypePtr ptrEEType)
        {
            EEType* pEEType = ptrEEType.ToPointer();
            return (byte)pEEType->CorElementType;
        }

        public enum RhEETypeClassification
        {
            Regular,                // Object, String, Int32
            Array,                  // String[]
            Generic,                // List<Int32>
            GenericTypeDefinition,  // List<T>
            UnmanagedPointer,       // void*
        }

        [RuntimeExport("RhGetEETypeClassification")]
        public static unsafe RhEETypeClassification RhGetEETypeClassification(EETypePtr ptrEEType)
        {
            EEType* pEEType = ptrEEType.ToPointer();

            if (pEEType->IsArray)
                return RhEETypeClassification.Array;

            if (pEEType->IsGeneric)
                return RhEETypeClassification.Generic;

            if (pEEType->IsGenericTypeDefinition)
                return RhEETypeClassification.GenericTypeDefinition;

            if (pEEType->IsPointerTypeDefinition)
                return RhEETypeClassification.UnmanagedPointer;

            return RhEETypeClassification.Regular;
        }

        [RuntimeExport("RhGetEETypeHash")]
        public static unsafe uint RhGetEETypeHash(EETypePtr ptrEEType)
        {
            EEType* pEEType = ptrEEType.ToPointer();
            return pEEType->HashCode;
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
