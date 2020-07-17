// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using System.Collections.Generic;
using System.Text;
using System.Runtime;
using System.Threading;
using Internal.Runtime.Augments;
using Internal.Runtime.CompilerServices;
using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Runtime.CompilerServices;

namespace Internal.Runtime.TypeLoader
{
    internal class ConstrainedCallSupport
    {
#if TARGET_ARM
        private delegate IntPtr ResolveCallOnReferenceTypeDel(IntPtr scratch, ref object thisPtr, IntPtr callDescIntPtr);
        private delegate IntPtr ResolveCallOnValueTypeDel(IntPtr scratch, IntPtr thisPtr, IntPtr callDescIntPtr);
#else
        private delegate IntPtr ResolveCallOnReferenceTypeDel(ref object thisPtr, IntPtr callDescIntPtr);
        private delegate IntPtr ResolveCallOnValueTypeDel(IntPtr thisPtr, IntPtr callDescIntPtr);
#endif

        [DllImport("*", ExactSpelling = true, EntryPoint = "ConstrainedCallSupport_GetStubs")]
        private extern static unsafe void ConstrainedCallSupport_GetStubs(out IntPtr constrainedCallSupport_DerefThisAndCall_CommonCallingStub, out IntPtr constrainedCallSupport_DirectConstrainedCall_CommonCallingStub);

        private static IntPtr s_constrainedCallSupport_DerefThisAndCall_CommonCallingStub;
        private static IntPtr s_constrainedCallSupport_DirectConstrainedCall_CommonCallingStub;

        private static object s_DerefThisAndCall_ThunkPoolHeap;
        private static object s_DirectConstrainedCall_ThunkPoolHeap;

        private static object s_DirectConstrainedCall_ThunkPoolHeapLock = new object();

        private static LowLevelDictionary<IntPtr, IntPtr> s_deferenceAndCallThunks = new LowLevelDictionary<IntPtr, IntPtr>();

        static ConstrainedCallSupport()
        {
            ConstrainedCallSupport_GetStubs(out s_constrainedCallSupport_DerefThisAndCall_CommonCallingStub,
                                            out s_constrainedCallSupport_DirectConstrainedCall_CommonCallingStub);
        }

        // There are multiple possible paths here.
        //
        //
        // - 1 If the ConstraintType is a reference type
        //   - ExactTarget in the CallDesc is never set.
        //   - 1.1 And the ConstrainedMethodType is an interface/class
        //     - 1.1.1 And the method does not have generic parameters
        //       - Perform virtual dispatch using runtime helper, cache the result in a hash table
        //     - 1.1.2 And the method does have generic parameters
        //       - Perform GVM dispatch, cache the result in a hash table
        //   - 1.2 Or the ConstraintType does not derive from or implement the ConstrainedMethodType
        //     - Throw error
        // - 2 If the ConstraintType is a value type
        //    Exact target will be set by the helper, but is not set initially.
        //   - 2.1 And the ConstrainedMethodType is an interface
        //     - 2.1.1 And the method does not have generic parameters
        //       - Resolve the target using the Redhawk interface dispatch api, and disassembly through the unboxing stub, if unboxing and instantiatiating, generate fat function pointer.
        //     - 2.1.2 And the method does have generic parameters
        //       - Resolve the target using the class library GVM dispatch, and disassembly through the unboxing stub, generate fat function pointer.
        //   - 2.2 Or the ConstrainedMethodType is object, and the slot is a method that is overriden on the valuetype
        //     - Resolve the target method by indexing into the vtable of the ConstraintType (OR by looking at the PreparedType, depending), disassembly through unboxing stub, if unboxing and instantiatiating, generate fat function pointer.
        //   - 2.3 Or the ConstrainedMethodType is object and the slot is a method that is not overriden on the valuetype
        //     - Generate a stub which tail calls a helper function which boxes the this pointer, and then does a virtual dispatch on the method
        //     - This function will need to take an extra argument or two that isn't enregistered on x86. Have fun with thread statics
        //   - 2.4 Or the ConstraintType does not derive from or implement the ConstrainedMethodType
        //     - Throw error

        public struct NonGenericConstrainedCallDesc
        {
            private IntPtr _exactTarget;
            private IntPtr _lookupFunc;
            private RuntimeTypeHandle _constraintType;
            private RuntimeTypeHandle _constrainedMethodType;
            private int _constrainedMethodSlot;

            // Consider in the future computing these values instead of hard coding
            internal const int s_ToStringSlot = 0;
            internal const int s_EqualsSlot = 1;
            internal const int s_GetHashCodeSlot = 2;
            private const int s_MaxObjectVTableSlot = 2;

            private static IntPtr s_resolveCallOnReferenceTypeFuncPtr;
            private static IntPtr s_resolveCallOnValueTypeFuncPtr;
            private static IntPtr s_resolveDirectConstrainedCallFuncPtr;
            private static IntPtr s_boxAndToStringFuncPtr;
            private static IntPtr s_boxAndGetHashCodeFuncPtr;
            private static IntPtr s_boxAndEqualsFuncPtr;

            private static LowLevelDictionary<RuntimeTypeHandle, LowLevelList<IntPtr>> s_nonGenericConstrainedCallDescs = new LowLevelDictionary<RuntimeTypeHandle, LowLevelList<IntPtr>>();
            private static LowLevelDictionary<RuntimeTypeHandle, LowLevelList<IntPtr>> s_nonGenericConstrainedCallDescsDirect = new LowLevelDictionary<RuntimeTypeHandle, LowLevelList<IntPtr>>();

            public static unsafe IntPtr GetDirectConstrainedCallPtr(RuntimeTypeHandle constraintType, RuntimeTypeHandle constrainedMethodType, int constrainedMethodSlot)
            {
                if (s_DirectConstrainedCall_ThunkPoolHeap == null)
                {
                    lock (s_DirectConstrainedCall_ThunkPoolHeapLock)
                    {
                        if (s_DirectConstrainedCall_ThunkPoolHeap == null)
                        {
                            s_DirectConstrainedCall_ThunkPoolHeap = RuntimeAugments.CreateThunksHeap(s_constrainedCallSupport_DirectConstrainedCall_CommonCallingStub);
                            Debug.Assert(s_DirectConstrainedCall_ThunkPoolHeap != null);
                        }
                    }
                }

                IntPtr thunk = RuntimeAugments.AllocateThunk(s_DirectConstrainedCall_ThunkPoolHeap);
                Debug.Assert(thunk != IntPtr.Zero);

                IntPtr constrainedCallDesc = Get(constraintType, constrainedMethodType, constrainedMethodSlot, true);
                RuntimeAugments.SetThunkData(s_DirectConstrainedCall_ThunkPoolHeap, thunk, constrainedCallDesc, s_resolveDirectConstrainedCallFuncPtr);

                return thunk;
            }

            public static unsafe IntPtr Get(RuntimeTypeHandle constraintType, RuntimeTypeHandle constrainedMethodType, int constrainedMethodSlot, bool directConstrainedCall = false)
            {
                LowLevelDictionary<RuntimeTypeHandle, LowLevelList<IntPtr>> nonGenericConstrainedCallDescsDirect = directConstrainedCall ? s_nonGenericConstrainedCallDescsDirect : s_nonGenericConstrainedCallDescs;

                lock (nonGenericConstrainedCallDescsDirect)
                {
                    // Get list of constrained call descs associated with a given type
                    LowLevelList<IntPtr> associatedCallDescs;
                    if (!nonGenericConstrainedCallDescsDirect.TryGetValue(constraintType, out associatedCallDescs))
                    {
                        associatedCallDescs = new LowLevelList<IntPtr>();
                        nonGenericConstrainedCallDescsDirect.Add(constraintType, associatedCallDescs);
                    }

                    // Perform linear scan of associated call descs to see if one matches
                    for (int i = 0; i < associatedCallDescs.Count; i++)
                    {
                        NonGenericConstrainedCallDesc* callDesc = (NonGenericConstrainedCallDesc*)associatedCallDescs[i];

                        Debug.Assert(constraintType.Equals(callDesc->_constraintType));

                        if (callDesc->_constrainedMethodSlot != constrainedMethodSlot)
                        {
                            continue;
                        }

                        if (!callDesc->_constrainedMethodType.Equals(constrainedMethodType))
                        {
                            continue;
                        }

                        // Found matching entry.
                        return associatedCallDescs[i];
                    }

                    // Did not find match, allocate a new one and add it to the lookup list
                    IntPtr newCallDescPtr = MemoryHelpers.AllocateMemory(sizeof(NonGenericConstrainedCallDesc));
                    NonGenericConstrainedCallDesc* newCallDesc = (NonGenericConstrainedCallDesc*)newCallDescPtr;
                    newCallDesc->_exactTarget = IntPtr.Zero;
                    if (directConstrainedCall)
                    {
                        newCallDesc->_lookupFunc = RuntimeAugments.GetUniversalTransitionThunk();
                    }
                    else
                    {
                        if (RuntimeAugments.IsValueType(constraintType))
                        {
                            newCallDesc->_lookupFunc = s_resolveCallOnValueTypeFuncPtr;
                        }
                        else
                        {
                            newCallDesc->_lookupFunc = s_resolveCallOnReferenceTypeFuncPtr;
                        }
                    }


                    newCallDesc->_constraintType = constraintType;
                    newCallDesc->_constrainedMethodSlot = constrainedMethodSlot;
                    newCallDesc->_constrainedMethodType = constrainedMethodType;

                    associatedCallDescs.Add(newCallDescPtr);

                    return newCallDescPtr;
                }
            }

            private delegate T BoxAndCallDel<T>(ref IntPtr thisPtr, IntPtr callDescIntPtr);
            private delegate T BoxAndCallDel2<T>(ref IntPtr thisPtr, IntPtr callDescIntPtr, object o);

            static NonGenericConstrainedCallDesc()
            {
                // TODO! File and fix bug where if the CctorHelper contents are in this function, we don't properly setup the cctor
                // This is a post checkin activity.
                CctorHelper();
            }

            private static void CctorHelper()
            {
                s_resolveCallOnReferenceTypeFuncPtr = Intrinsics.AddrOf((ResolveCallOnReferenceTypeDel)ResolveCallOnReferenceType);
                s_resolveCallOnValueTypeFuncPtr = Intrinsics.AddrOf((ResolveCallOnValueTypeDel)ResolveCallOnValueType);
                s_resolveDirectConstrainedCallFuncPtr = Intrinsics.AddrOf((Func<IntPtr, IntPtr, IntPtr>)ResolveDirectConstrainedCall);
                s_boxAndToStringFuncPtr = Intrinsics.AddrOf((BoxAndCallDel<string>)BoxAndToString);
                s_boxAndGetHashCodeFuncPtr = Intrinsics.AddrOf((BoxAndCallDel<int>)BoxAndGetHashCode);
                s_boxAndEqualsFuncPtr = Intrinsics.AddrOf((BoxAndCallDel2<bool>)BoxAndEquals);
            }

#if TARGET_ARM
            private static unsafe IntPtr ResolveCallOnReferenceType(IntPtr unused1, ref object thisPtr, IntPtr callDescIntPtr)
#else
            private static unsafe IntPtr ResolveCallOnReferenceType(ref object thisPtr, IntPtr callDescIntPtr)
#endif
            {
                return RuntimeAugments.RuntimeCacheLookup(thisPtr.GetType().TypeHandle.ToIntPtr(), callDescIntPtr,
                    (IntPtr context, IntPtr callDescPtr, object contextObject, ref IntPtr auxResult) =>
                    {
                        NonGenericConstrainedCallDesc* callDesc = (NonGenericConstrainedCallDesc*)callDescPtr;
                        IntPtr target = RuntimeAugments.ResolveDispatch(contextObject, callDesc->_constrainedMethodType, callDesc->_constrainedMethodSlot);
                        return GetThunkThatDereferencesThisPointerAndTailCallsTarget(target);
                    }, thisPtr, out _);
            }

            // Resolve a constrained call in case where the call is an MDIL constrained call directly through a function pointer located in the generic dictionary
            // This can only happen if there is a call from shared generic code to a structure which implements multiple of the same generic interface, and which instantiation
            // is decided by the exact type of the caller. For instance
            //
            // interface IFunc<T>
            // {
            //    void M();
            // }
            //
            // struct UnusualCase : IFunc<object>, IFunc<string>
            // {
            //    void IFunc<object>.M() { Console.WriteLine("In IFunc<object>");}
            //    void IFunc<string>.M() { Console.WriteLine("In IFunc<object>");}
            // }
            // class Caller<T,U> where T : IFunc<U>
            // {
            //    void Call(T c)
            //    {
            //        c.M();
            //    }
            // }
            //
            // If Caller is instantiated as Caller<UnusualCase,object>, or Caller<UnusualCase,string> we will generate code for Caller<UnusualCase,__Canon>.Call(UnusualCase)
            // However, that code will not be able to exactly specify the target of the call. It will need to use the generic dictionary.
            unsafe private static IntPtr ResolveDirectConstrainedCall(IntPtr callerTransitionBlockParam, IntPtr callDescIntPtr)
            {
                NonGenericConstrainedCallDesc* callDesc = (NonGenericConstrainedCallDesc*)callDescIntPtr;
                Debug.Assert(RuntimeAugments.IsInterface(callDesc->_constrainedMethodType));
                IntPtr targetOnTypeVtable = RuntimeAugments.ResolveDispatchOnType(callDesc->_constraintType, callDesc->_constrainedMethodType, callDesc->_constrainedMethodSlot);
                IntPtr exactTarget = RuntimeAugments.GetCodeTarget(targetOnTypeVtable);
                IntPtr underlyingTargetIfUnboxingAndInstantiatingStub;
                if (TypeLoaderEnvironment.TryGetTargetOfUnboxingAndInstantiatingStub(exactTarget, out underlyingTargetIfUnboxingAndInstantiatingStub))
                {
                    // If this is an unboxing and instantiating stub, get the underlying pointer. The caller of this function is required to have already setup the 
                    // instantiation argument
                    exactTarget = underlyingTargetIfUnboxingAndInstantiatingStub;
                }

                callDesc->_exactTarget = exactTarget;
                return exactTarget;
            }

#if TARGET_ARM
            private static unsafe IntPtr ResolveCallOnValueType(IntPtr unused1, IntPtr unused2, IntPtr callDescIntPtr)
#else
            private static unsafe IntPtr ResolveCallOnValueType(IntPtr unused, IntPtr callDescIntPtr)
#endif
            {
                NonGenericConstrainedCallDesc* callDesc = (NonGenericConstrainedCallDesc*)callDescIntPtr;
                IntPtr exactTarget = IntPtr.Zero;
                IntPtr targetOnTypeVtable = RuntimeAugments.ResolveDispatchOnType(callDesc->_constraintType, callDesc->_constrainedMethodType, callDesc->_constrainedMethodSlot);
                bool decodeUnboxing = true;

                if (!RuntimeAugments.IsInterface(callDesc->_constrainedMethodType))
                {
                    // Non-interface constrained call on a valuetype to a method that isn't GetHashCode/Equals/ToString?!?!
                    if (callDesc->_constrainedMethodSlot > s_MaxObjectVTableSlot)
                        throw new NotSupportedException();

                    RuntimeTypeHandle baseTypeHandle;
                    bool gotBaseType = RuntimeAugments.TryGetBaseType(callDesc->_constraintType, out baseTypeHandle);
                    Debug.Assert(gotBaseType);
                    if (targetOnTypeVtable == RuntimeAugments.ResolveDispatchOnType(baseTypeHandle, callDesc->_constrainedMethodType, callDesc->_constrainedMethodSlot))
                    {
                        // In this case, the valuetype does not override the base types implementation of ToString(), GetHashCode(), or Equals(object)
                        decodeUnboxing = false;
                    }
                }

                if (decodeUnboxing)
                {
                    exactTarget = TypeLoaderEnvironment.ConvertUnboxingFunctionPointerToUnderlyingNonUnboxingPointer(targetOnTypeVtable, callDesc->_constraintType);
                }
                else
                {
                    // Create a fat function pointer, where the instantiation argument is ConstraintType, and the target is BoxAndToString, BoxAndGetHashCode, or BoxAndEquals
                    IntPtr realTarget;

                    switch (callDesc->_constrainedMethodSlot)
                    {
                        case s_ToStringSlot:
                            realTarget = s_boxAndToStringFuncPtr;
                            break;
                        case s_GetHashCodeSlot:
                            realTarget = s_boxAndGetHashCodeFuncPtr;
                            break;
                        case s_EqualsSlot:
                            realTarget = s_boxAndEqualsFuncPtr;
                            break;
                        default:
                            throw new NotSupportedException();
                    }

                    exactTarget = FunctionPointerOps.GetGenericMethodFunctionPointer(realTarget, callDesc->_constraintType.ToIntPtr());
                }

                // Ensure that all threads will have their function pointers completely published before updating callDesc.
                // as the ExactTarget is read from callDesc by binder generated code without a barrier, we need a barrier here
                // to ensure that the new function pointer data is valid on all threads
                Interlocked.MemoryBarrier();

                // Its possible for multiple threads to race to set exact target. Check to see we always set the same value
                if (callDesc->_exactTarget != IntPtr.Zero)
                {
                    Debug.Assert(callDesc->_exactTarget == exactTarget);
                }

                callDesc->_exactTarget = exactTarget;
                return exactTarget;
            }

            private static unsafe string BoxAndToString(ref IntPtr data, IntPtr typeToBoxIntoPointer)
            {
                fixed (IntPtr* pData = &data)
                {
                    RuntimeTypeHandle typeToBoxInto = *(RuntimeTypeHandle*)&typeToBoxIntoPointer;

                    object boxedObject = RuntimeAugments.Box(typeToBoxInto, (IntPtr)pData);
                    return boxedObject.ToString();
                }
            }

            private static unsafe int BoxAndGetHashCode(ref IntPtr data, IntPtr typeToBoxIntoPointer)
            {
                fixed (IntPtr* pData = &data)
                {
                    RuntimeTypeHandle typeToBoxInto = *(RuntimeTypeHandle*)&typeToBoxIntoPointer;

                    object boxedObject = RuntimeAugments.Box(typeToBoxInto, (IntPtr)pData);
                    return boxedObject.GetHashCode();
                }
            }

            private static unsafe bool BoxAndEquals(ref IntPtr data, IntPtr typeToBoxIntoPointer, object obj)
            {
                fixed (IntPtr* pData = &data)
                {
                    RuntimeTypeHandle typeToBoxInto = *(RuntimeTypeHandle*)&typeToBoxIntoPointer;

                    object boxedObject = RuntimeAugments.Box(typeToBoxInto, (IntPtr)pData);
                    return boxedObject.Equals(obj);
                }
            }
        }

        public struct GenericConstrainedCallDesc
        {
            private IntPtr _exactTarget;
            private IntPtr _lookupFunc;
            private RuntimeTypeHandle _constraintType;
            private RuntimeMethodHandle _constrainedMethod;

            private static IntPtr s_resolveCallOnReferenceTypeFuncPtr;
            private static IntPtr s_resolveCallOnValueTypeFuncPtr;

            private static LowLevelDictionary<RuntimeTypeHandle, LowLevelList<IntPtr>> s_genericConstrainedCallDescs = new LowLevelDictionary<RuntimeTypeHandle, LowLevelList<IntPtr>>();

            public static unsafe IntPtr Get(RuntimeTypeHandle constraintType, RuntimeMethodHandle constrainedMethod)
            {
                lock (s_genericConstrainedCallDescs)
                {
                    // Get list of constrained call descs associated with a given type
                    LowLevelList<IntPtr> associatedCallDescs;
                    if (!s_genericConstrainedCallDescs.TryGetValue(constraintType, out associatedCallDescs))
                    {
                        associatedCallDescs = new LowLevelList<IntPtr>();
                        s_genericConstrainedCallDescs.Add(constraintType, associatedCallDescs);
                    }

                    // Perform linear scan of associated call descs to see if one matches
                    for (int i = 0; i < associatedCallDescs.Count; i++)
                    {
                        GenericConstrainedCallDesc* callDesc = (GenericConstrainedCallDesc*)associatedCallDescs[i];

                        Debug.Assert(constraintType.Equals(callDesc->_constraintType));

                        if (callDesc->_constrainedMethod != constrainedMethod)
                        {
                            continue;
                        }

                        // Found matching entry.
                        return associatedCallDescs[i];
                    }

                    // Did not find match, allocate a new one and add it to the lookup list
                    IntPtr newCallDescPtr = MemoryHelpers.AllocateMemory(sizeof(GenericConstrainedCallDesc));
                    GenericConstrainedCallDesc* newCallDesc = (GenericConstrainedCallDesc*)newCallDescPtr;
                    newCallDesc->_exactTarget = IntPtr.Zero;
                    if (RuntimeAugments.IsValueType(constraintType))
                    {
                        newCallDesc->_lookupFunc = s_resolveCallOnValueTypeFuncPtr;
                    }
                    else
                    {
                        newCallDesc->_lookupFunc = s_resolveCallOnReferenceTypeFuncPtr;
                    }

                    newCallDesc->_constraintType = constraintType;
                    newCallDesc->_constrainedMethod = constrainedMethod;

                    associatedCallDescs.Add(newCallDescPtr);

                    return newCallDescPtr;
                }
            }

            static GenericConstrainedCallDesc()
            {
                // TODO! File and fix bug where if the CctorHelper contents are in this function, we don't properly setup the cctor
                // This is a post checkin activity.
                CctorHelper();
            }

            private static void CctorHelper()
            {
                s_resolveCallOnReferenceTypeFuncPtr = Intrinsics.AddrOf((ResolveCallOnReferenceTypeDel)ResolveCallOnReferenceType);
                s_resolveCallOnValueTypeFuncPtr = Intrinsics.AddrOf((ResolveCallOnValueTypeDel)ResolveCallOnValueType);
            }

#if TARGET_ARM
            private static unsafe IntPtr ResolveCallOnReferenceType(IntPtr unused1, ref object thisPtr, IntPtr callDescIntPtr)
#else
            private static unsafe IntPtr ResolveCallOnReferenceType(ref object thisPtr, IntPtr callDescIntPtr)
#endif
            {
                return RuntimeAugments.RuntimeCacheLookup(thisPtr.GetType().TypeHandle.ToIntPtr(), callDescIntPtr,
                    (IntPtr context, IntPtr callDescPtr, object contextObject, ref IntPtr auxResult) =>
                    {
                        // Perform a normal GVM dispatch, then change the function pointer to dereference the this pointer.
                        GenericConstrainedCallDesc* callDesc = (GenericConstrainedCallDesc*)callDescPtr;
                        IntPtr target = RuntimeAugments.GVMLookupForSlot(contextObject.GetType().TypeHandle, callDesc->_constrainedMethod);

                        if (FunctionPointerOps.IsGenericMethodPointer(target))
                        {
                            GenericMethodDescriptor* genMethodDesc = FunctionPointerOps.ConvertToGenericDescriptor(target);
                            IntPtr actualCodeTarget = GetThunkThatDereferencesThisPointerAndTailCallsTarget(genMethodDesc->MethodFunctionPointer);

                            return FunctionPointerOps.GetGenericMethodFunctionPointer(actualCodeTarget, genMethodDesc->InstantiationArgument);
                        }
                        else
                        {
                            return GetThunkThatDereferencesThisPointerAndTailCallsTarget(target);
                        }
                    },
                    thisPtr, out _);
            }

#if TARGET_ARM
            private static unsafe IntPtr ResolveCallOnValueType(IntPtr unused1, IntPtr unused2, IntPtr callDescIntPtr)
#else
            private static unsafe IntPtr ResolveCallOnValueType(IntPtr unused, IntPtr callDescIntPtr)
#endif
            {
                GenericConstrainedCallDesc* callDesc = (GenericConstrainedCallDesc*)callDescIntPtr;
                IntPtr targetAsVirtualCall = RuntimeAugments.GVMLookupForSlot(callDesc->_constraintType, callDesc->_constrainedMethod);
                IntPtr exactTarget = IntPtr.Zero;

                if (FunctionPointerOps.IsGenericMethodPointer(targetAsVirtualCall))
                {
                    GenericMethodDescriptor* genMethodDesc = FunctionPointerOps.ConvertToGenericDescriptor(targetAsVirtualCall);
                    IntPtr actualCodeTarget = RuntimeAugments.GetCodeTarget(genMethodDesc->MethodFunctionPointer);
                    exactTarget = FunctionPointerOps.GetGenericMethodFunctionPointer(actualCodeTarget, genMethodDesc->InstantiationArgument);
                }
                else
                {
                    IntPtr actualCodeTarget = RuntimeAugments.GetCodeTarget(targetAsVirtualCall);
                    IntPtr callConverterThunk;

                    if (CallConverterThunk.TryGetNonUnboxingFunctionPointerFromUnboxingAndInstantiatingStub(actualCodeTarget, callDesc->_constraintType, out callConverterThunk))
                    {
                        actualCodeTarget = callConverterThunk;
                    }

                    exactTarget = actualCodeTarget;
                }

                // Ensure that all threads will have their function pointers completely published before updating callDesc.
                // as the ExactTarget is read from callDesc by binder generated code without a barrier, we need a barrier here
                // to ensure that the new function pointer data is valid on all threads
                Interlocked.MemoryBarrier();

                // Its possible for multiple threads to race to set exact target. Check to see we always set the same value
                if (callDesc->_exactTarget != IntPtr.Zero)
                {
                    Debug.Assert(callDesc->_exactTarget == exactTarget);
                }

                callDesc->_exactTarget = exactTarget;
                return exactTarget;
            }
        }

        private static IntPtr GetThunkThatDereferencesThisPointerAndTailCallsTarget(IntPtr target)
        {
            IntPtr result = IntPtr.Zero;
            lock (s_deferenceAndCallThunks)
            {
                if (!s_deferenceAndCallThunks.TryGetValue(target, out result))
                {
                    if (s_DerefThisAndCall_ThunkPoolHeap == null)
                    {
                        s_DerefThisAndCall_ThunkPoolHeap = RuntimeAugments.CreateThunksHeap(s_constrainedCallSupport_DerefThisAndCall_CommonCallingStub);
                        Debug.Assert(s_DerefThisAndCall_ThunkPoolHeap != null);
                    }

                    IntPtr thunk = RuntimeAugments.AllocateThunk(s_DerefThisAndCall_ThunkPoolHeap);
                    Debug.Assert(thunk != IntPtr.Zero);

                    RuntimeAugments.SetThunkData(s_DerefThisAndCall_ThunkPoolHeap, thunk, target, IntPtr.Zero);

                    result = thunk;
                    s_deferenceAndCallThunks.Add(target, result);
                }
            }

            return result;
        }
    }
}
