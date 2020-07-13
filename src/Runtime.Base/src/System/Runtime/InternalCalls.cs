// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

//
// This is where we group together all the internal calls.
//

using System.Runtime.InteropServices;
using System.Runtime.CompilerServices;

using Internal.Runtime;

#if TARGET_64BIT
using nuint = System.UInt64;
#else
using nuint = System.UInt32;
#endif

namespace System.Runtime
{
    internal enum DispatchCellType
    {
        InterfaceAndSlot = 0x0,
        MetadataToken = 0x1,
        VTableOffset = 0x2,
    }

    internal struct DispatchCellInfo
    {
        public DispatchCellType CellType;
        public EETypePtr InterfaceType;
        public ushort InterfaceSlot;
        public byte HasCache;
        public uint MetadataToken;
        public uint VTableOffset;
    }

    // Constants used with RhpGetClasslibFunction, to indicate which classlib function
    // we are interested in. 
    // Note: make sure you change the def in ICodeManager.h if you change this!
    internal enum ClassLibFunctionId
    {
        GetRuntimeException = 0,
        FailFast = 1,
        // UnhandledExceptionHandler = 2, // unused
        AppendExceptionStackFrame = 3,
        CheckStaticClassConstruction = 4,
        GetSystemArrayEEType = 5,
        OnFirstChance = 6,
        DebugFuncEvalHelper = 7,
        DebugFuncEvalAbortHelper = 8,
    }

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

        [RuntimeExport("RhStartNoGCRegion")]
        internal static int RhStartNoGCRegion(long totalSize, bool hasLohSize, long lohSize, bool disallowFullBlockingGC)
        {
            return RhpStartNoGCRegion(totalSize, hasLohSize, lohSize, disallowFullBlockingGC);
        }

        [RuntimeExport("RhEndNoGCRegion")]
        internal static int RhEndNoGCRegion()
        {
            return RhpEndNoGCRegion();
        }

        //
        // internalcalls for System.Runtime.__Finalizer.
        //

        // Fetch next object which needs finalization or return null if we've reached the end of the list.
        [RuntimeImport(Redhawk.BaseName, "RhpGetNextFinalizableObject")]
        [MethodImpl(MethodImplOptions.InternalCall)]
        [ManuallyManaged(GcPollPolicy.Never)]
        internal static extern object RhpGetNextFinalizableObject();

        //
        // internalcalls for System.Runtime.InteropServices.GCHandle.
        //

        // Allocate handle.
        [RuntimeImport(Redhawk.BaseName, "RhpHandleAlloc")]
        [MethodImpl(MethodImplOptions.InternalCall)]
        [ManuallyManaged(GcPollPolicy.Never)]
        internal static extern IntPtr RhpHandleAlloc(object value, GCHandleType type);

        // Allocate dependent handle.
        [RuntimeImport(Redhawk.BaseName, "RhpHandleAllocDependent")]
        [MethodImpl(MethodImplOptions.InternalCall)]
        [ManuallyManaged(GcPollPolicy.Never)]
        internal static extern IntPtr RhpHandleAllocDependent(object primary, object secondary);

        // Allocate variable handle.
        [RuntimeImport(Redhawk.BaseName, "RhpHandleAllocVariable")]
        [MethodImpl(MethodImplOptions.InternalCall)]
        [ManuallyManaged(GcPollPolicy.Never)]
        internal static extern IntPtr RhpHandleAllocVariable(object value, uint type);

        [RuntimeImport(Redhawk.BaseName, "RhHandleGet")]
        [MethodImpl(MethodImplOptions.InternalCall)]
        [ManuallyManaged(GcPollPolicy.Never)]
        internal static extern object RhHandleGet(IntPtr handle);

        [RuntimeImport(Redhawk.BaseName, "RhHandleSet")]
        [MethodImpl(MethodImplOptions.InternalCall)]
        [ManuallyManaged(GcPollPolicy.Never)]
        internal static extern IntPtr RhHandleSet(IntPtr handle, object value);

        //
        // internal calls for allocation
        //
        [RuntimeImport(Redhawk.BaseName, "RhpNewFast")]
        [MethodImpl(MethodImplOptions.InternalCall)]
        [ManuallyManaged(GcPollPolicy.Sometimes)]
        internal extern static unsafe object RhpNewFast(EEType* pEEType);  // BEWARE: not for finalizable objects!

        [RuntimeImport(Redhawk.BaseName, "RhpNewFinalizable")]
        [MethodImpl(MethodImplOptions.InternalCall)]
        [ManuallyManaged(GcPollPolicy.Sometimes)]
        internal extern static unsafe object RhpNewFinalizable(EEType* pEEType);

        [RuntimeImport(Redhawk.BaseName, "RhpNewArray")]
        [MethodImpl(MethodImplOptions.InternalCall)]
        [ManuallyManaged(GcPollPolicy.Sometimes)]
        internal extern static unsafe object RhpNewArray(EEType* pEEType, int length);

#if FEATURE_64BIT_ALIGNMENT
        [RuntimeImport(Redhawk.BaseName, "RhpNewFastAlign8")]
        [MethodImpl(MethodImplOptions.InternalCall)]
        [ManuallyManaged(GcPollPolicy.Sometimes)]
        internal extern static unsafe object RhpNewFastAlign8(EEType * pEEType);  // BEWARE: not for finalizable objects!

        [RuntimeImport(Redhawk.BaseName, "RhpNewFinalizableAlign8")]
        [MethodImpl(MethodImplOptions.InternalCall)]
        [ManuallyManaged(GcPollPolicy.Sometimes)]
        internal extern static unsafe object RhpNewFinalizableAlign8(EEType* pEEType);

        [RuntimeImport(Redhawk.BaseName, "RhpNewArrayAlign8")]
        [MethodImpl(MethodImplOptions.InternalCall)]
        [ManuallyManaged(GcPollPolicy.Sometimes)]
        internal extern static unsafe object RhpNewArrayAlign8(EEType* pEEType, int length);

        [RuntimeImport(Redhawk.BaseName, "RhpNewFastMisalign")]
        [MethodImpl(MethodImplOptions.InternalCall)]
        [ManuallyManaged(GcPollPolicy.Sometimes)]
        internal extern static unsafe object RhpNewFastMisalign(EEType * pEEType);
#endif // FEATURE_64BIT_ALIGNMENT

        [RuntimeImport(Redhawk.BaseName, "RhpCopyObjectContents")]
        [MethodImpl(MethodImplOptions.InternalCall)]
        [ManuallyManaged(GcPollPolicy.Never)]
        internal extern static unsafe void RhpCopyObjectContents(object objDest, object objSrc);

        [RuntimeImport(Redhawk.BaseName, "RhpCompareObjectContents")]
        [MethodImpl(MethodImplOptions.InternalCall)]
        [ManuallyManaged(GcPollPolicy.Never)]
        internal extern static bool RhpCompareObjectContentsAndPadding(object obj1, object obj2);

        [RuntimeImport(Redhawk.BaseName, "RhpAssignRef")]
        [MethodImpl(MethodImplOptions.InternalCall)]
        [ManuallyManaged(GcPollPolicy.Never)]
        internal extern static unsafe void RhpAssignRef(ref object address, object obj);

        [MethodImplAttribute(MethodImplOptions.InternalCall)]
        [RuntimeImport(Redhawk.BaseName, "RhpInitMultibyte")]
        internal static extern unsafe ref byte RhpInitMultibyte(ref byte dmem, int c, nuint size);

        [MethodImplAttribute(MethodImplOptions.InternalCall)]
        [RuntimeImport(Redhawk.BaseName, "memmove")]
        internal static extern unsafe void* memmove(byte* dmem, byte* smem, nuint size);

        [MethodImplAttribute(MethodImplOptions.InternalCall)]
        [RuntimeImport(Redhawk.BaseName, "RhBulkMoveWithWriteBarrier")]
        internal static extern unsafe void RhBulkMoveWithWriteBarrier(ref byte dmem, ref byte smem, nuint size);

#if FEATURE_GC_STRESS
        //
        // internal calls for GC stress
        //
        [RuntimeImport(Redhawk.BaseName, "RhpInitializeGcStress")]
        [MethodImpl(MethodImplOptions.InternalCall)]
        [ManuallyManaged(GcPollPolicy.Never)]
        internal extern static unsafe void RhpInitializeGcStress();
#endif // FEATURE_GC_STRESS

        [RuntimeImport(Redhawk.BaseName, "RhpEHEnumInitFromStackFrameIterator")]
        [MethodImpl(MethodImplOptions.InternalCall)]
        [ManuallyManaged(GcPollPolicy.Never)]
        internal extern static unsafe bool RhpEHEnumInitFromStackFrameIterator(ref StackFrameIterator pFrameIter, byte** pMethodStartAddress, void* pEHEnum);

        [RuntimeImport(Redhawk.BaseName, "RhpEHEnumNext")]
        [MethodImpl(MethodImplOptions.InternalCall)]
        [ManuallyManaged(GcPollPolicy.Never)]
        internal extern static unsafe bool RhpEHEnumNext(void* pEHEnum, void* pEHClause);

        [RuntimeImport(Redhawk.BaseName, "RhpGetSealedVirtualSlot")]
        [MethodImpl(MethodImplOptions.InternalCall)]
        [ManuallyManaged(GcPollPolicy.Never)]
        internal extern static unsafe IntPtr RhpGetSealedVirtualSlot(EEType* pEEType, ushort slot);

        [RuntimeImport(Redhawk.BaseName, "RhpGetDispatchCellInfo")]
        [MethodImpl(MethodImplOptions.InternalCall)]
        [ManuallyManaged(GcPollPolicy.Never)]
        internal extern static unsafe void RhpGetDispatchCellInfo(IntPtr pCell, out DispatchCellInfo newCellInfo);

        [RuntimeImport(Redhawk.BaseName, "RhpSearchDispatchCellCache")]
        [MethodImpl(MethodImplOptions.InternalCall)]
        [ManuallyManaged(GcPollPolicy.Never)]
        internal extern static unsafe IntPtr RhpSearchDispatchCellCache(IntPtr pCell, EEType* pInstanceType);

        [RuntimeImport(Redhawk.BaseName, "RhpUpdateDispatchCellCache")]
        [MethodImpl(MethodImplOptions.InternalCall)]
        [ManuallyManaged(GcPollPolicy.Never)]
        internal extern static unsafe IntPtr RhpUpdateDispatchCellCache(IntPtr pCell, IntPtr pTargetCode, EEType* pInstanceType, ref DispatchCellInfo newCellInfo);

        [RuntimeImport(Redhawk.BaseName, "RhpGetClasslibFunctionFromCodeAddress")]
        [MethodImpl(MethodImplOptions.InternalCall)]
        [ManuallyManaged(GcPollPolicy.Never)]
        internal extern static unsafe void* RhpGetClasslibFunctionFromCodeAddress(IntPtr address, ClassLibFunctionId id);

        [RuntimeImport(Redhawk.BaseName, "RhpGetClasslibFunctionFromEEType")]
        [MethodImpl(MethodImplOptions.InternalCall)]
        [ManuallyManaged(GcPollPolicy.Never)]
        internal extern static unsafe void* RhpGetClasslibFunctionFromEEType(IntPtr pEEType, ClassLibFunctionId id);

        //
        // StackFrameIterator
        //

        [RuntimeImport(Redhawk.BaseName, "RhpSfiInit")]
        [MethodImpl(MethodImplOptions.InternalCall)]
        [ManuallyManaged(GcPollPolicy.Never)]
        internal static extern unsafe bool RhpSfiInit(ref StackFrameIterator pThis, void* pStackwalkCtx, bool instructionFault);

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
        internal extern static unsafe uint RhpGetEETypeRareFlags(EEType* pEEType);

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

        [RuntimeImport(Redhawk.BaseName, "RhpGetNumThunkBlocksPerMapping")]
        [MethodImpl(MethodImplOptions.InternalCall)]
        [ManuallyManaged(GcPollPolicy.Never)]
        internal extern static int RhpGetNumThunkBlocksPerMapping();

        [RuntimeImport(Redhawk.BaseName, "RhpGetNumThunksPerBlock")]
        [MethodImpl(MethodImplOptions.InternalCall)]
        [ManuallyManaged(GcPollPolicy.Never)]
        internal extern static int RhpGetNumThunksPerBlock();

        [RuntimeImport(Redhawk.BaseName, "RhpGetThunkSize")]
        [MethodImpl(MethodImplOptions.InternalCall)]
        [ManuallyManaged(GcPollPolicy.Never)]
        internal extern static int RhpGetThunkSize();

        [RuntimeImport(Redhawk.BaseName, "RhpGetThunkDataBlockAddress")]
        [MethodImpl(MethodImplOptions.InternalCall)]
        [ManuallyManaged(GcPollPolicy.Never)]
        internal extern static IntPtr RhpGetThunkDataBlockAddress(IntPtr thunkStubAddress);

        [RuntimeImport(Redhawk.BaseName, "RhpGetThunkStubsBlockAddress")]
        [MethodImpl(MethodImplOptions.InternalCall)]
        [ManuallyManaged(GcPollPolicy.Never)]
        internal extern static IntPtr RhpGetThunkStubsBlockAddress(IntPtr thunkDataAddress);

        [RuntimeImport(Redhawk.BaseName, "RhpGetThunkBlockSize")]
        [MethodImpl(MethodImplOptions.InternalCall)]
        [ManuallyManaged(GcPollPolicy.Never)]
        internal extern static int RhpGetThunkBlockSize();

        [RuntimeImport(Redhawk.BaseName, "RhpGetThreadAbortException")]
        [MethodImpl(MethodImplOptions.InternalCall)]
        internal extern static Exception RhpGetThreadAbortException();

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
        internal static extern uint RhpWaitForFinalizerRequest();

        // Indicate that the current round of finalizations is complete.
        [DllImport(Redhawk.BaseName, CallingConvention = CallingConvention.Cdecl)]
        internal static extern void RhpSignalFinalizationComplete();

        [DllImport(Redhawk.BaseName, CallingConvention = CallingConvention.Cdecl)]
        internal static extern void RhpAcquireCastCacheLock();

        [DllImport(Redhawk.BaseName, CallingConvention = CallingConvention.Cdecl)]
        internal static extern void RhpReleaseCastCacheLock();

        [DllImport(Redhawk.BaseName, CallingConvention = CallingConvention.Cdecl)]
        internal extern static long PalGetTickCount64();

        [DllImport(Redhawk.BaseName, CallingConvention = CallingConvention.Cdecl)]
        internal static extern void RhpAcquireThunkPoolLock();

        [DllImport(Redhawk.BaseName, CallingConvention = CallingConvention.Cdecl)]
        internal static extern void RhpReleaseThunkPoolLock();

        [DllImport(Redhawk.BaseName, CallingConvention = CallingConvention.Cdecl)]
        internal static extern IntPtr RhAllocateThunksMapping();

        // Enters a no GC region, possibly doing a blocking GC if there is not enough
        // memory available to satisfy the caller's request.
        [DllImport(Redhawk.BaseName, CallingConvention = CallingConvention.Cdecl)]
        internal static extern int RhpStartNoGCRegion(long totalSize, bool hasLohSize, long lohSize, bool disallowFullBlockingGC);

        // Exits a no GC region, possibly doing a GC to clean up the garbage that
        // the caller allocated.
        [DllImport(Redhawk.BaseName, CallingConvention = CallingConvention.Cdecl)]
        internal static extern int RhpEndNoGCRegion();
    }
}
