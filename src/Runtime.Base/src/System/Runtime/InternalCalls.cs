// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

//
// This is where we group together all the internal calls.
//

using System.Runtime.InteropServices;
using System.Runtime.CompilerServices;

using Internal.Runtime;

namespace System.Runtime
{
    internal static class InternalCalls
    {
        //
        // internalcalls for System.GC.
        //

        // Force a garbage collection.
        [RuntimeExport("RhCollect")]
        internal static void RhCollect(int generation, InternalGCCollectionMode mode)
        {
            RhpCollect(generation, mode);
        }

        [DllImport(Redhawk.BaseName, CallingConvention = CallingConvention.Cdecl)]
        private static extern void RhpCollect(int generation, InternalGCCollectionMode mode);

        [RuntimeExport("RhGetGcTotalMemory")]
        internal static long RhGetGcTotalMemory()
        {
            return RhpGetGcTotalMemory();
        }

        [DllImport(Redhawk.BaseName, CallingConvention = CallingConvention.Cdecl)]
        private static extern long RhpGetGcTotalMemory();

        //
        // internalcalls for System.Runtime.__Finalizer.
        //

        // Fetch next object which needs finalization or return null if we've reached the end of the list.
        [RuntimeImport(Redhawk.BaseName, "RhpGetNextFinalizableObject")]
        [MethodImpl(MethodImplOptions.InternalCall)]
        [ManuallyManaged(GcPollPolicy.Never)]
        internal static extern Object RhpGetNextFinalizableObject();

        //
        // internalcalls for System.Runtime.InteropServices.GCHandle.
        //

        // Allocate handle.
        [RuntimeImport(Redhawk.BaseName, "RhpHandleAlloc")]
        [MethodImpl(MethodImplOptions.InternalCall)]
        [ManuallyManaged(GcPollPolicy.Never)]
        internal static extern IntPtr RhpHandleAlloc(Object value, GCHandleType type);

        // Allocate dependent handle.
        [RuntimeImport(Redhawk.BaseName, "RhpHandleAllocDependent")]
        [MethodImpl(MethodImplOptions.InternalCall)]
        [ManuallyManaged(GcPollPolicy.Never)]
        internal static extern IntPtr RhpHandleAllocDependent(Object primary, Object secondary);

        // Allocate variable handle.
        [RuntimeImport(Redhawk.BaseName, "RhpHandleAllocVariable")]
        [MethodImpl(MethodImplOptions.InternalCall)]
        [ManuallyManaged(GcPollPolicy.Never)]
        internal static extern IntPtr RhpHandleAllocVariable(Object value, uint type);

        [RuntimeImport(Redhawk.BaseName, "RhHandleGet")]
        [MethodImpl(MethodImplOptions.InternalCall)]
        [ManuallyManaged(GcPollPolicy.Never)]
        internal static extern Object RhHandleGet(IntPtr handle);

        [RuntimeImport(Redhawk.BaseName, "RhHandleSet")]
        [MethodImpl(MethodImplOptions.InternalCall)]
        [ManuallyManaged(GcPollPolicy.Never)]
        internal static extern IntPtr RhHandleSet(IntPtr handle, Object value);

        //
        // internal calls for allocation
        //
        [RuntimeImport(Redhawk.BaseName, "RhpNewFast")]
        [MethodImpl(MethodImplOptions.InternalCall)]
        [ManuallyManaged(GcPollPolicy.Sometimes)]
        internal unsafe extern static object RhpNewFast(EEType* pEEType);  // BEWARE: not for finalizable objects!

        [RuntimeImport(Redhawk.BaseName, "RhpNewFinalizable")]
        [MethodImpl(MethodImplOptions.InternalCall)]
        [ManuallyManaged(GcPollPolicy.Sometimes)]
        internal unsafe extern static object RhpNewFinalizable(EEType* pEEType);

        [RuntimeImport(Redhawk.BaseName, "RhpNewArray")]
        [MethodImpl(MethodImplOptions.InternalCall)]
        [ManuallyManaged(GcPollPolicy.Sometimes)]
        internal unsafe extern static object RhpNewArray(EEType* pEEType, int length);

#if FEATURE_64BIT_ALIGNMENT
        [RuntimeImport(Redhawk.BaseName, "RhpNewFastAlign8")]
        [MethodImpl(MethodImplOptions.InternalCall)]
        [ManuallyManaged(GcPollPolicy.Sometimes)]
        internal unsafe extern static object RhpNewFastAlign8(EEType * pEEType);  // BEWARE: not for finalizable objects!

        [RuntimeImport(Redhawk.BaseName, "RhpNewFinalizableAlign8")]
        [MethodImpl(MethodImplOptions.InternalCall)]
        [ManuallyManaged(GcPollPolicy.Sometimes)]
        internal unsafe extern static object RhpNewFinalizableAlign8(EEType* pEEType);

        [RuntimeImport(Redhawk.BaseName, "RhpNewArrayAlign8")]
        [MethodImpl(MethodImplOptions.InternalCall)]
        [ManuallyManaged(GcPollPolicy.Sometimes)]
        internal unsafe extern static object RhpNewArrayAlign8(EEType* pEEType, int length);

        [RuntimeImport(Redhawk.BaseName, "RhpNewFastMisalign")]
        [MethodImpl(MethodImplOptions.InternalCall)]
        [ManuallyManaged(GcPollPolicy.Sometimes)]
        internal unsafe extern static object RhpNewFastMisalign(EEType * pEEType);
#endif // FEATURE_64BIT_ALIGNMENT

        [RuntimeImport(Redhawk.BaseName, "RhpBox")]
        [MethodImpl(MethodImplOptions.InternalCall)]
        [ManuallyManaged(GcPollPolicy.Sometimes)]
        internal unsafe extern static void RhpBox(object obj, ref byte data);

        [RuntimeImport(Redhawk.BaseName, "RhUnbox")]
        [MethodImpl(MethodImplOptions.InternalCall)]
        [ManuallyManaged(GcPollPolicy.Sometimes)]
        internal unsafe extern static void RhUnbox(object obj, ref byte data, EEType* pUnboxToEEType);

        [RuntimeImport(Redhawk.BaseName, "RhpCopyObjectContents")]
        [MethodImpl(MethodImplOptions.InternalCall)]
        [ManuallyManaged(GcPollPolicy.Never)]
        internal unsafe extern static void RhpCopyObjectContents(object objDest, object objSrc);

#if FEATURE_GC_STRESS
        //
        // internal calls for GC stress
        //
        [RuntimeImport(Redhawk.BaseName, "RhpInitializeGcStress")]
        [MethodImpl(MethodImplOptions.InternalCall)]
        [ManuallyManaged(GcPollPolicy.Never)]
        internal unsafe extern static void RhpInitializeGcStress();
#endif // FEATURE_GC_STRESS

        [RuntimeImport(Redhawk.BaseName, "RhpEHEnumInitFromStackFrameIterator")]
        [MethodImpl(MethodImplOptions.InternalCall)]
        [ManuallyManaged(GcPollPolicy.Never)]
        internal unsafe extern static bool RhpEHEnumInitFromStackFrameIterator(ref StackFrameIterator pFrameIter, byte** pMethodStartAddress, void* pEHEnum);

        [RuntimeImport(Redhawk.BaseName, "RhpEHEnumNext")]
        [MethodImpl(MethodImplOptions.InternalCall)]
        [ManuallyManaged(GcPollPolicy.Never)]
        internal unsafe extern static bool RhpEHEnumNext(void* pEHEnum, void* pEHClause);

        [RuntimeImport(Redhawk.BaseName, "RhpGetArrayBaseType")]
        [MethodImpl(MethodImplOptions.InternalCall)]
        [ManuallyManaged(GcPollPolicy.Never)]
        internal unsafe extern static EEType* RhpGetArrayBaseType(EEType* pEEType);

        [RuntimeImport(Redhawk.BaseName, "RhpHasDispatchMap")]
        [MethodImpl(MethodImplOptions.InternalCall)]
        [ManuallyManaged(GcPollPolicy.Never)]
        internal unsafe extern static bool RhpHasDispatchMap(EEType* pEETypen);

        [RuntimeImport(Redhawk.BaseName, "RhpGetDispatchMap")]
        [MethodImpl(MethodImplOptions.InternalCall)]
        [ManuallyManaged(GcPollPolicy.Never)]
        internal unsafe extern static DispatchResolve.DispatchMap* RhpGetDispatchMap(EEType* pEEType);

        [RuntimeImport(Redhawk.BaseName, "RhpGetSealedVirtualSlot")]
        [MethodImpl(MethodImplOptions.InternalCall)]
        [ManuallyManaged(GcPollPolicy.Never)]
        internal unsafe extern static IntPtr RhpGetSealedVirtualSlot(EEType* pEEType, ushort slot);

        [RuntimeImport(Redhawk.BaseName, "RhpGetDispatchCellInfo")]
        [MethodImpl(MethodImplOptions.InternalCall)]
        [ManuallyManaged(GcPollPolicy.Never)]
        internal unsafe extern static void RhpGetDispatchCellInfo(IntPtr pCell, EEType** pInterfaceType, ushort* slot);

        [RuntimeImport(Redhawk.BaseName, "RhpSearchDispatchCellCache")]
        [MethodImpl(MethodImplOptions.InternalCall)]
        [ManuallyManaged(GcPollPolicy.Never)]
        internal unsafe extern static IntPtr RhpSearchDispatchCellCache(IntPtr pCell, EEType* pInstanceType);

        [RuntimeImport(Redhawk.BaseName, "RhpUpdateDispatchCellCache")]
        [MethodImpl(MethodImplOptions.InternalCall)]
        [ManuallyManaged(GcPollPolicy.Never)]
        internal unsafe extern static IntPtr RhpUpdateDispatchCellCache(IntPtr pCell, IntPtr pTargetCode, EEType* pInstanceType);

        [RuntimeImport(Redhawk.BaseName, "RhpGetClasslibFunction")]
        [MethodImpl(MethodImplOptions.InternalCall)]
        [ManuallyManaged(GcPollPolicy.Never)]
        internal unsafe extern static void* RhpGetClasslibFunction(IntPtr address, EH.ClassLibFunctionId id);

        //
        // StackFrameIterator
        //

        [RuntimeImport(Redhawk.BaseName, "RhpSfiInit")]
        [MethodImpl(MethodImplOptions.InternalCall)]
        [ManuallyManaged(GcPollPolicy.Never)]
        internal static extern unsafe bool RhpSfiInit(ref StackFrameIterator pThis, void* pStackwalkCtx);

        [RuntimeImport(Redhawk.BaseName, "RhpSfiNext")]
        [MethodImpl(MethodImplOptions.InternalCall)]
        [ManuallyManaged(GcPollPolicy.Never)]
        internal static extern bool RhpSfiNext(ref StackFrameIterator pThis, out uint uExCollideClauseIdx, out bool fUnwoundReversePInvoke);

        //
        // DebugEventSource
        //

        [RuntimeImport(Redhawk.BaseName, "RhpGetRequestedExceptionEvents")]
        [MethodImpl(MethodImplOptions.InternalCall)]
        [ManuallyManaged(GcPollPolicy.Never)]
        internal static extern ExceptionEventKind RhpGetRequestedExceptionEvents();

        [DllImport(Redhawk.BaseName)]
        internal static extern unsafe void RhpSendExceptionEventToDebugger(ExceptionEventKind eventKind, byte* ip, UIntPtr sp);

        //
        // Miscellaneous helpers.
        //

        // Get the rarely used (optional) flags of an EEType. If they're not present 0 will be returned.
        [RuntimeImport(Redhawk.BaseName, "RhpGetEETypeRareFlags")]
        [MethodImpl(MethodImplOptions.InternalCall)]
        [ManuallyManaged(GcPollPolicy.Never)]
        internal extern static unsafe UInt32 RhpGetEETypeRareFlags(EEType* pEEType);

        // Retrieve the offset of the value embedded in a Nullable<T>.
        [RuntimeImport(Redhawk.BaseName, "RhpGetNullableEETypeValueOffset")]
        [MethodImpl(MethodImplOptions.InternalCall)]
        [ManuallyManaged(GcPollPolicy.Never)]
        internal extern static unsafe byte RhpGetNullableEETypeValueOffset(EEType* pEEType);

        // Retrieve the target type T in a Nullable<T>.
        [RuntimeImport(Redhawk.BaseName, "RhpGetNullableEEType")]
        [MethodImpl(MethodImplOptions.InternalCall)]
        [ManuallyManaged(GcPollPolicy.Never)]
        internal extern static unsafe EEType* RhpGetNullableEEType(EEType* pEEType);

        // For an ICastable type return a pointer to code that implements ICastable.IsInstanceOfInterface.
        [RuntimeImport(Redhawk.BaseName, "RhpGetICastableIsInstanceOfInterfaceMethod")]
        [MethodImpl(MethodImplOptions.InternalCall)]
        [ManuallyManaged(GcPollPolicy.Never)]
        internal extern static unsafe IntPtr RhpGetICastableIsInstanceOfInterfaceMethod(EEType* pEEType);

        // For an ICastable type return a pointer to code that implements ICastable.GetImplType.
        [RuntimeImport(Redhawk.BaseName, "RhpGetICastableGetImplTypeMethod")]
        [MethodImpl(MethodImplOptions.InternalCall)]
        [ManuallyManaged(GcPollPolicy.Never)]
        internal extern static unsafe IntPtr RhpGetICastableGetImplTypeMethod(EEType* pEEType);

        [RuntimeImport(Redhawk.BaseName, "RhpGetNextFinalizerInitCallback")]
        [MethodImpl(MethodImplOptions.InternalCall)]
        [ManuallyManaged(GcPollPolicy.Never)]
        internal extern static unsafe IntPtr RhpGetNextFinalizerInitCallback();

        [RuntimeImport(Redhawk.BaseName, "RhpCallCatchFunclet")]
        [MethodImpl(MethodImplOptions.InternalCall)]
        [ManuallyManaged(GcPollPolicy.Never)]
        internal extern static unsafe IntPtr RhpCallCatchFunclet(
            object exceptionObj, byte* pHandlerIP, void* pvRegDisplay, ref EH.ExInfo exInfo);

        [RuntimeImport(Redhawk.BaseName, "RhpCallFinallyFunclet")]
        [MethodImpl(MethodImplOptions.InternalCall)]
        [ManuallyManaged(GcPollPolicy.Never)]
        internal extern static unsafe void RhpCallFinallyFunclet(byte* pHandlerIP, void* pvRegDisplay);

        [RuntimeImport(Redhawk.BaseName, "RhpCallFilterFunclet")]
        [MethodImpl(MethodImplOptions.InternalCall)]
        [ManuallyManaged(GcPollPolicy.Never)]
        internal extern static unsafe bool RhpCallFilterFunclet(
            object exceptionObj, byte* pFilterIP, void* pvRegDisplay);

        [RuntimeImport(Redhawk.BaseName, "RhpFallbackFailFast")]
        [MethodImpl(MethodImplOptions.InternalCall)]
        [ManuallyManaged(GcPollPolicy.Never)]
        internal extern static unsafe void RhpFallbackFailFast();

        [RuntimeImport(Redhawk.BaseName, "RhpSetThreadDoNotTriggerGC")]
        [MethodImpl(MethodImplOptions.InternalCall)]
        [ManuallyManaged(GcPollPolicy.Never)]
        internal extern static void RhpSetThreadDoNotTriggerGC();

        [System.Diagnostics.Conditional("DEBUG")]
        [RuntimeImport(Redhawk.BaseName, "RhpValidateExInfoStack")]
        [MethodImpl(MethodImplOptions.InternalCall)]
        [ManuallyManaged(GcPollPolicy.Never)]
        internal extern static void RhpValidateExInfoStack();

        [RuntimeImport(Redhawk.BaseName, "RhpCopyContextFromExInfo")]
        [MethodImpl(MethodImplOptions.InternalCall)]
        [ManuallyManaged(GcPollPolicy.Never)]
        internal extern static unsafe void RhpCopyContextFromExInfo(void* pOSContext, int cbOSContext, EH.PAL_LIMITED_CONTEXT* pPalContext);


        //------------------------------------------------------------------------------------------------------------
        // PInvoke-based internal calls
        //
        // These either do not need to be called in cooperative mode or, in some cases, MUST be called in preemptive
        // mode.  Note that they must use the Cdecl calling convention due to a limitation in our .obj file linking
        // support.
        //------------------------------------------------------------------------------------------------------------

        // Block the current thread until at least one object needs to be finalized (returns true) or
        // memory is low (returns false and the finalizer thread should initiate a garbage collection).
        [DllImport(Redhawk.BaseName, CallingConvention = CallingConvention.Cdecl)]
        internal static extern UInt32 RhpWaitForFinalizerRequest();

        // Indicate that the current round of finalizations is complete.
        [DllImport(Redhawk.BaseName, CallingConvention = CallingConvention.Cdecl)]
        internal static extern void RhpSignalFinalizationComplete();

        [DllImport(Redhawk.BaseName, CallingConvention = CallingConvention.Cdecl)]
        internal static extern void RhpAcquireCastCacheLock();

        [DllImport(Redhawk.BaseName, CallingConvention = CallingConvention.Cdecl)]
        internal static extern void RhpReleaseCastCacheLock();

        [DllImport(Redhawk.BaseName, CallingConvention = CallingConvention.Cdecl)]
        internal extern static long PalGetTickCount64();
    }
}
