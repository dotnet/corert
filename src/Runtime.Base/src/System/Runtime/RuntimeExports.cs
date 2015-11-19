// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

//
// This is where we group together all the runtime export calls.
//

using System.Diagnostics;
using System.Runtime.InteropServices;

namespace System.Runtime
{
    public static class RuntimeExports
    {
        //
        // internalcalls for System.Runtime.InteropServices.GCHandle.
        //

        // Allocate handle.
        [RuntimeExport("RhHandleAlloc")]
        public static IntPtr RhHandleAlloc(object value, GCHandleType type)
        {
            IntPtr h = InternalCalls.RhpHandleAlloc(value, type);

            if (h == IntPtr.Zero)
            {
                // Throw the out of memory exception defined by the classlib, using the return address of this method
                // to find the correct classlib.

                ExceptionIDs exID = ExceptionIDs.OutOfMemory;

                IntPtr returnAddr = BinderIntrinsics.GetReturnAddress();
                Exception e = EH.GetClasslibException(exID, returnAddr);
                throw e;
            }

            return h;
        }

        // Allocate dependent handle.
        [RuntimeExport("RhHandleAllocDependent")]
        public static IntPtr RhHandleAllocDependent(object primary, object secondary)
        {
            IntPtr h = InternalCalls.RhpHandleAllocDependent(primary, secondary);

            if (h == IntPtr.Zero)
            {
                // Throw the out of memory exception defined by the classlib, using the return address of this method
                // to find the correct classlib.

                ExceptionIDs exID = ExceptionIDs.OutOfMemory;

                IntPtr returnAddr = BinderIntrinsics.GetReturnAddress();
                Exception e = EH.GetClasslibException(exID, returnAddr);
                throw e;
            }

            return h;
        }

        // Allocate variable handle.
        [RuntimeExport("RhHandleAllocVariable")]
        public static IntPtr RhHandleAllocVariable(object value, uint type)
        {
            IntPtr h = InternalCalls.RhpHandleAllocVariable(value, type);

            if (h == IntPtr.Zero)
            {
                // Throw the out of memory exception defined by the classlib, using the return address of this method
                // to find the correct classlib.

                ExceptionIDs exID = ExceptionIDs.OutOfMemory;

                IntPtr returnAddr = BinderIntrinsics.GetReturnAddress();
                Exception e = EH.GetClasslibException(exID, returnAddr);
                throw e;
            }

            return h;
        }

        //
        // internal calls for allocation
        //
        [RuntimeExport("RhNewObject")]
        public unsafe static object RhNewObject(EETypePtr pEEType)
        {
            try
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
            catch (OutOfMemoryException)
            {
                // Throw the out of memory exception defined by the classlib, using the input EEType* 
                // to find the correct classlib.

                ExceptionIDs exID = ExceptionIDs.OutOfMemory;

                IntPtr addr = pEEType.ToPointer()->GetAssociatedModuleAddress();
                Exception e = EH.GetClasslibException(exID, addr);
                throw e;
            }
        }

        [RuntimeExport("RhNewArray")]
        public unsafe static object RhNewArray(EETypePtr pEEType, int length)
        {
            EEType* ptrEEType = (EEType*)pEEType.ToPointer();
            try
            {
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
            catch (OutOfMemoryException)
            {
                // Throw the out of memory exception defined by the classlib, using the input EEType* 
                // to find the correct classlib.

                ExceptionIDs exID = ExceptionIDs.OutOfMemory;

                IntPtr addr = pEEType.ToPointer()->GetAssociatedModuleAddress();
                Exception e = EH.GetClasslibException(exID, addr);
                throw e;
            }
            catch (OverflowException)
            {
                // Throw the overflow exception defined by the classlib, using the input EEType* 
                // to find the correct classlib.

                ExceptionIDs exID = ExceptionIDs.Overflow;

                IntPtr addr = pEEType.ToPointer()->GetAssociatedModuleAddress();
                Exception e = EH.GetClasslibException(exID, addr);
                throw e;
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
            if (result == null)
            {
                // Throw the out of memory exception defined by the classlib, using the input EEType* 
                // to find the correct classlib.

                ExceptionIDs exID = ExceptionIDs.OutOfMemory;

                IntPtr addr = pEEType.ToPointer()->GetAssociatedModuleAddress();
                Exception e = EH.GetClasslibException(exID, addr);
                throw e;
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
                        isValid = (o == null) || (o.EEType == ptrUnboxToEEType->GetNullableType());
                    else
                        isValid = (o != null && o.EEType->CorElementType == ptrUnboxToEEType->CorElementType && TypeCast.IsInstanceOfClass(o, ptrUnboxToEEType) != null);

                    if (!isValid)
                    {
                        // Throw the invalid cast exception defined by the classlib, using the input unbox EEType* 
                        // to find the correct classlib.

                        ExceptionIDs exID = o == null ? ExceptionIDs.NullReference : ExceptionIDs.InvalidCast;

                        IntPtr addr = ptrUnboxToEEType->GetAssociatedModuleAddress();
                        Exception e = EH.GetClasslibException(exID, addr);

                        BinderIntrinsics.TailCall_RhpThrowEx(e);
                    }
                    InternalCalls.RhUnbox(o, pData - 1, ptrUnboxToEEType);
                }
            }
            else
                data.o = o;
        }

        [RuntimeExport("RhArrayStoreCheckAny")]
        static public /*internal*/ unsafe void RhArrayStoreCheckAny(object array, ref Hack_o_p data)
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
        static public /*internal*/ unsafe bool RhBoxAndNullCheck(ref Hack_o_p data, EETypePtr pEEType)
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
                return RhNewObject(pEEType);
            else
                return new Wrapper();
        }

        [RuntimeExport("RhpReversePInvokeBadTransition")]
        public static void RhpReversePInvokeBadTransition()
        {
            IntPtr returnAddress = BinderIntrinsics.GetReturnAddress();
            if (returnAddress != IntPtr.Zero)
            {
                EH.FailFastViaClasslib(
                    RhFailFastReason.IllegalNativeCallableEntry,
                    null,
                    returnAddress);
            }
            else
            {
                // @HACKHACK: we need to force the method to have an EBP frame so that we can use the
                // GetReturnAddress() intrinsic above.  This seems to be the smallest way to do this.
                EH.FailFast(RhFailFastReason.InternalError, null);
                throw EH.GetClasslibException(ExceptionIDs.Arithmetic, returnAddress);
            }
        }

        [RuntimeExport("RhMemberwiseClone")]
        public static object RhMemberwiseClone(object src)
        {
            return src.MemberwiseClone();
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

        /// FUNCTION IS OBSOLETE AND NOT EXPECTED TO BE USED IN NEW CODE
        [RuntimeExport("RhSetNonArrayBaseType")]
        public static unsafe void RhSetNonArrayBaseType(EETypePtr ptrEEType, EETypePtr ptrBaseEEType)
        {
            EEType* pEEType = ptrEEType.ToPointer();
            EEType* pBaseEEType = ptrBaseEEType.ToPointer();
            pEEType->BaseType = pBaseEEType;
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
        [RuntimeExport("RhpCalculateStackTraceWorker")]
        public static unsafe int RhpCalculateStackTraceWorker(IntPtr[] outputBuffer)
        {
            int nFrames = 0;
            bool success = (outputBuffer != null);

            StackFrameIterator frameIter = new StackFrameIterator();
            bool isValid = frameIter.Init(null);
            for (; isValid; isValid = frameIter.Next())
            {
                if (outputBuffer != null)
                {
                    if (nFrames < outputBuffer.Length)
                        outputBuffer[nFrames] = new IntPtr(frameIter.ControlPC);
                    else
                        success = false;
                }
                nFrames++;
            }
            return success ? nFrames : -nFrames;
        }
    }
}
