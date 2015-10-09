// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System;
using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Runtime.CompilerServices;

namespace System.Runtime
{
    // CONTRACT with Runtime
    // This class lists all the static methods that the redhawk runtime exports to a class library
    // These are not expected to change much but are needed by the class library to implement its functionality
    //
    //      The contents of this file can be modified if needed by the class library
    //      E.g., the class and methods are marked internal assuming that only the base class library needs them
    //            but if a class library wants to factor differently (such as putting the GCHandle methods in an
    //            optional library, those methods can be moved to a different file/namespace/dll

    public static class RuntimeImports
    {
        private const string RuntimeLibrary = "[MRT]";
        //
        // calls to GC
        // These methods are needed to implement System.GC like functionality (optional)
        //

        // Force a garbage collection.
        [MethodImpl(MethodImplOptions.InternalCall)]
        [RuntimeImport(RuntimeLibrary, "RhCollect")]
        internal static extern void RhCollect(int generation, InternalGCCollectionMode mode);

        // Mark an object instance as already finalized.
        [MethodImpl(MethodImplOptions.InternalCall)]
        [RuntimeImport(RuntimeLibrary, "RhSuppressFinalize")]
        internal static extern void RhSuppressFinalize(Object obj);

        // Wait for all pending finalizers. This must be a p/invoke to avoid starving the GC.
        [DllImport(RuntimeLibrary, ExactSpelling = true)]
        private static extern void RhWaitForPendingFinalizers(int allowReentrantWait);

        // Temporary workaround to unblock shareable assembly bring-up - without shared interop,
        // we must prevent RhWaitForPendingFinalizers from using marshaling because it would
        // rewrite System.Private.CoreLib to reference the non-shareable interop assembly. With shared interop,
        // we will be able to remove this helper method and change the DllImport above 
        // to directly accept a boolean parameter.
        internal static void RhWaitForPendingFinalizers(bool allowReentrantWait)
        {
            RhWaitForPendingFinalizers(allowReentrantWait ? 1 : 0);
        }

        // Get maximum GC generation number.
        [MethodImplAttribute(MethodImplOptions.InternalCall)]
        [RuntimeImport(RuntimeLibrary, "RhGetMaxGcGeneration")]
        internal static extern int RhGetMaxGcGeneration();

        // Get count of collections so far.
        [MethodImplAttribute(MethodImplOptions.InternalCall)]
        [RuntimeImport(RuntimeLibrary, "RhGetGcCollectionCount")]
        internal static extern int RhGetGcCollectionCount(int generation, bool getSpecialGCCount);

        [MethodImplAttribute(MethodImplOptions.InternalCall)]
        [RuntimeImport(RuntimeLibrary, "RhGetGeneration")]
        internal static extern int RhGetGeneration(Object obj);

        [MethodImpl(MethodImplOptions.InternalCall)]
        [RuntimeImport(RuntimeLibrary, "RhReRegisterForFinalize")]
        internal static extern void RhReRegisterForFinalize(Object obj);

        [MethodImpl(MethodImplOptions.InternalCall)]
        [RuntimeImport(RuntimeLibrary, "RhGetGcLatencyMode")]
        internal static extern GCLatencyMode RhGetGcLatencyMode();

        [MethodImpl(MethodImplOptions.InternalCall)]
        [RuntimeImport(RuntimeLibrary, "RhSetGcLatencyMode")]
        internal static extern void RhSetGcLatencyMode(GCLatencyMode newLatencyMode);

        [MethodImpl(MethodImplOptions.InternalCall)]
        [RuntimeImport(RuntimeLibrary, "RhIsServerGc")]
        internal static extern bool RhIsServerGc();

        [MethodImpl(MethodImplOptions.InternalCall)]
        [RuntimeImport(RuntimeLibrary, "RhGetGcTotalMemory")]
        internal static extern long RhGetGcTotalMemory();

        [MethodImpl(MethodImplOptions.InternalCall)]
        [RuntimeImport(RuntimeLibrary, "RhGetLohCompactionMode")]
        internal static extern int RhGetLohCompactionMode();

        [MethodImpl(MethodImplOptions.InternalCall)]
        [RuntimeImport(RuntimeLibrary, "RhSetLohCompactionMode")]
        internal static extern void RhSetLohCompactionMode(int newLohCompactionMode);

        [MethodImpl(MethodImplOptions.InternalCall)]
        [RuntimeImport(RuntimeLibrary, "RhGetCurrentObjSize")]
        internal static extern long RhGetCurrentObjSize();

        [MethodImpl(MethodImplOptions.InternalCall)]
        [RuntimeImport(RuntimeLibrary, "RhGetGCNow")]
        internal static extern long RhGetGCNow();

        [MethodImpl(MethodImplOptions.InternalCall)]
        [RuntimeImport(RuntimeLibrary, "RhGetLastGCStartTime")]
        internal static extern long RhGetLastGCStartTime(int generation);

        [MethodImpl(MethodImplOptions.InternalCall)]
        [RuntimeImport(RuntimeLibrary, "RhGetLastGCDuration")]
        internal static extern long RhGetLastGCDuration(int generation);
        //
        // calls for GCHandle.
        // These methods are needed to implement GCHandle class like functionality (optional)
        //

        // Allocate handle.
        [MethodImpl(MethodImplOptions.InternalCall)]
        [RuntimeImport(RuntimeLibrary, "RhHandleAlloc")]
        internal static extern IntPtr RhHandleAlloc(Object value, GCHandleType type);

        // Allocate handle for dependent handle case where a secondary can be set at the same time.
        [MethodImpl(MethodImplOptions.InternalCall)]
        [RuntimeImport(RuntimeLibrary, "RhHandleAllocDependent")]
        internal static extern IntPtr RhHandleAllocDependent(Object primary, Object secondary);

        // Allocate variable handle with its initial type.
        [MethodImpl(MethodImplOptions.InternalCall)]
        [RuntimeImport(RuntimeLibrary, "RhHandleAllocVariable")]
        internal static extern IntPtr RhHandleAllocVariable(Object value, uint type);

        // Free handle.
        [MethodImpl(MethodImplOptions.InternalCall)]
        [RuntimeImport(RuntimeLibrary, "RhHandleFree")]
        internal static extern void RhHandleFree(IntPtr handle);

        // Get object reference from handle.
        [MethodImpl(MethodImplOptions.InternalCall)]
        [RuntimeImport(RuntimeLibrary, "RhHandleGet")]
        internal static extern Object RhHandleGet(IntPtr handle);

        // Get primary and secondary object references from dependent handle.
        [MethodImpl(MethodImplOptions.InternalCall)]
        [RuntimeImport(RuntimeLibrary, "RhHandleGetDependent")]
        internal static extern Object RhHandleGetDependent(IntPtr handle, out Object secondary);

        // Set object reference into handle.
        [MethodImpl(MethodImplOptions.InternalCall)]
        [RuntimeImport(RuntimeLibrary, "RhHandleSet")]
        internal static extern void RhHandleSet(IntPtr handle, Object value);

        // Set the secondary object reference into a dependent handle.
        [MethodImpl(MethodImplOptions.InternalCall)]
        [RuntimeImport(RuntimeLibrary, "RhHandleSetDependentSecondary")]
        internal static extern void RhHandleSetDependentSecondary(IntPtr handle, Object secondary);

        // Get the handle type associated with a variable handle.
        [MethodImpl(MethodImplOptions.InternalCall)]
        [RuntimeImport(RuntimeLibrary, "RhHandleGetVariableType")]
        internal static extern uint RhHandleGetVariableType(IntPtr handle);

        // Set the handle type associated with a variable handle.
        [MethodImpl(MethodImplOptions.InternalCall)]
        [RuntimeImport(RuntimeLibrary, "RhHandleSetVariableType")]
        internal static extern void RhHandleSetVariableType(IntPtr handle, uint type);

        // Conditionally and atomically set the handle type associated with a variable handle if the current
        // type is the one specified. Returns the previous handle type.
        [MethodImpl(MethodImplOptions.InternalCall)]
        [RuntimeImport(RuntimeLibrary, "RhHandleCompareExchangeVariableType")]
        internal static extern uint RhHandleCompareExchangeVariableType(IntPtr handle, uint oldType, uint newType);

        //
        // calls to runtime for type equality checks
        //

        [MethodImpl(MethodImplOptions.InternalCall)]
        [RuntimeImport(RuntimeLibrary, "RhTypeCast_AreTypesEquivalent")]
        internal static extern bool AreTypesEquivalent(EETypePtr pType1, EETypePtr pType2);

        [MethodImpl(MethodImplOptions.InternalCall)]
        [RuntimeImport(RuntimeLibrary, "RhTypeCast_AreTypesAssignable")]
        internal static extern bool AreTypesAssignable(EETypePtr pSourceType, EETypePtr pTargetType);

        [MethodImpl(MethodImplOptions.InternalCall)]
        [RuntimeImport(RuntimeLibrary, "RhGetEETypeHash")]
        internal static extern uint RhGetEETypeHash(EETypePtr pType);

        [MethodImpl(MethodImplOptions.InternalCall)]
        [RuntimeImport(RuntimeLibrary, "RhTypeCast_CheckArrayStore")]
        internal static extern void RhCheckArrayStore(Object array, Object obj);

        [MethodImpl(MethodImplOptions.InternalCall)]
        [RuntimeImport(RuntimeLibrary, "RhTypeCast_IsInstanceOf")]
        internal static extern object IsInstanceOf(object obj, EETypePtr pTargetType);

        [MethodImpl(MethodImplOptions.InternalCall)]
        [RuntimeImport(RuntimeLibrary, "RhTypeCast_IsInstanceOfClass")]
        internal static extern object IsInstanceOfClass(object obj, EETypePtr pTargetType);

        [MethodImpl(MethodImplOptions.InternalCall)]
        [RuntimeImport(RuntimeLibrary, "RhTypeCast_IsInstanceOfInterface")]
        internal static extern object IsInstanceOfInterface(object obj, EETypePtr pTargetType);

        //
        // calls to runtime for allocation
        // These calls are needed in types which cannot use "new" to allocate and need to do it manually
        //
        // calls to runtime for allocation
        //
        [MethodImpl(MethodImplOptions.InternalCall)]
        [RuntimeImport(RuntimeLibrary, "RhNewObject")]
        internal static extern object RhNewObject(EETypePtr pEEType);

        [MethodImpl(MethodImplOptions.InternalCall)]
        [RuntimeImport(RuntimeLibrary, "RhNewArray")]
        internal static extern Array RhNewArray(EETypePtr pEEType, int length);

        // @todo: Should we just have a proper export for this?
        [MethodImpl(MethodImplOptions.InternalCall)]
        [RuntimeImport(RuntimeLibrary, "RhNewArray")]
        internal static extern String RhNewArrayAsString(EETypePtr pEEType, int length);

        // Given the OS handle of a loaded Redhawk module, return true if the runtime no longer has any
        // references to resources in that module (i.e. the module can be safely unloaded with FreeLibrary, at
        // least as far as the runtime knows).
        [MethodImpl(MethodImplOptions.InternalCall)]
        [RuntimeImport(RuntimeLibrary, "RhCanUnloadModule")]
        internal static extern bool RhCanUnloadModule(IntPtr hOsModule);

        [MethodImpl(MethodImplOptions.InternalCall)]
        [RuntimeImport(RuntimeLibrary, "RhBox")]
        internal static unsafe extern object RhBox(EETypePtr pEEType, void* pData);

        [MethodImpl(MethodImplOptions.InternalCall)]
        [RuntimeImport(RuntimeLibrary, "RhUnbox")]
        internal static unsafe extern void RhUnbox(object obj, void* pData, EETypePtr pUnboxToEEType);

        [MethodImpl(MethodImplOptions.InternalCall)]
        [RuntimeImport(RuntimeLibrary, "RhMemberwiseClone")]
        internal static extern object RhMemberwiseClone(object obj);

        // Busy spin for the given number of iterations.
        [MethodImpl(MethodImplOptions.InternalCall)]
        [RuntimeImport(RuntimeLibrary, "RhSpinWait")]
        internal static extern void RhSpinWait(int iterations);

        // Yield the cpu to another thread ready to process, if one is available.
        [MethodImpl(MethodImplOptions.InternalCall)]
        [RuntimeImport(RuntimeLibrary, "RhYield")]
        internal static extern bool RhYield();

        // Wait for any object to be signalled, in a way that's compatible with the CLR's behavior in an STA.
        // ExactSpelling = 'true' to force MCG to resolve it to default
        [DllImport(RuntimeLibrary, ExactSpelling = true)]
        private static unsafe extern int RhCompatibleReentrantWaitAny(int alertable, int timeout, int count, IntPtr* handles);

        // Temporary workaround to unblock shareable assembly bring-up - without shared interop,
        // we must prevent RhCompatibleReentrantWaitAny from using marshaling because it would
        // rewrite System.Private.CoreLib to reference the non-shareable interop assembly. With shared interop,
        // we will be able to remove this helper method and change the DllImport above 
        // to directly accept a boolean parameter and use the SetLastError = true modifier.
        internal static unsafe int RhCompatibleReentrantWaitAny(bool alertable, int timeout, int count, IntPtr* handles)
        {
            return RhCompatibleReentrantWaitAny(alertable ? 1 : 0, timeout, count, handles);
        }

        //
        // EEType interrogation methods.
        //

        [MethodImplAttribute(MethodImplOptions.InternalCall)]
        [RuntimeImport(RuntimeLibrary, "RhGetEETypeClassification")]
        internal static extern RhEETypeClassification RhGetEETypeClassification(EETypePtr pEEType);

        [MethodImplAttribute(MethodImplOptions.InternalCall)]
        [RuntimeImport(RuntimeLibrary, "RhGetEETypeClassification")]
        internal static extern RhEETypeClassification RhGetEETypeClassification(IntPtr pEEType);

        [MethodImplAttribute(MethodImplOptions.InternalCall)]
        [RuntimeImport(RuntimeLibrary, "RhIsValueType")]
        internal static extern bool RhIsValueType(EETypePtr pEEType);

        [MethodImplAttribute(MethodImplOptions.InternalCall)]
        [RuntimeImport(RuntimeLibrary, "RhIsInterface")]
        internal static extern bool RhIsInterface(EETypePtr pEEType);

        [MethodImplAttribute(MethodImplOptions.InternalCall)]
        [RuntimeImport(RuntimeLibrary, "RhIsArray")]
        internal static extern bool RhIsArray(EETypePtr pEEType);

        [MethodImplAttribute(MethodImplOptions.InternalCall)]
        [RuntimeImport(RuntimeLibrary, "RhIsString")]
        internal static extern bool RhIsString(EETypePtr pEEType);

        [MethodImplAttribute(MethodImplOptions.InternalCall)]
        [RuntimeImport(RuntimeLibrary, "RhHasReferenceFields")]
        internal static extern bool RhHasReferenceFields(EETypePtr pEEType);

        [MethodImplAttribute(MethodImplOptions.InternalCall)]
        [RuntimeImport(RuntimeLibrary, "RhGetCorElementType")]
        internal static extern RhCorElementType RhGetCorElementType(EETypePtr pEEType);

        [MethodImplAttribute(MethodImplOptions.InternalCall)]
        [RuntimeImport(RuntimeLibrary, "RhGetValueTypeSize")]
        internal static extern uint RhGetValueTypeSize(EETypePtr pEEType);

        [MethodImplAttribute(MethodImplOptions.InternalCall)]
        [RuntimeImport(RuntimeLibrary, "RhIsNullable")]
        internal static extern bool RhIsNullable(EETypePtr pEEType);

        [MethodImplAttribute(MethodImplOptions.InternalCall)]
        [RuntimeImport(RuntimeLibrary, "RhGetNullableType")]
        internal static extern EETypePtr RhGetNullableType(EETypePtr pEEType);

        //
        // EEType Array Dissectors
        //

        [MethodImplAttribute(MethodImplOptions.InternalCall)]
        [RuntimeImport(RuntimeLibrary, "RhGetRelatedParameterType")]
        internal static extern EETypePtr RhGetRelatedParameterType(EETypePtr pEEType);

        [MethodImplAttribute(MethodImplOptions.InternalCall)]
        [RuntimeImport(RuntimeLibrary, "RhGetComponentSize")]
        internal static extern ushort RhGetComponentSize(EETypePtr pEEType);

        //
        // EEType Parent Hierarchy
        //

        [MethodImplAttribute(MethodImplOptions.InternalCall)]
        [RuntimeImport(RuntimeLibrary, "RhGetNonArrayBaseType")]
        internal static extern EETypePtr RhGetNonArrayBaseType(EETypePtr pEEType);


        // Note: This reports the transitive closure, not just the directly implemented interfaces.
        [MethodImplAttribute(MethodImplOptions.InternalCall)]
        [RuntimeImport(RuntimeLibrary, "RhGetNumInterfaces")]
        internal static extern uint RhGetNumInterfaces(EETypePtr pEEType);

        [MethodImplAttribute(MethodImplOptions.InternalCall)]
        [RuntimeImport(RuntimeLibrary, "RhGetInterface")]
        internal static extern EETypePtr RhGetInterface(EETypePtr pEEType, uint index);

        [MethodImplAttribute(MethodImplOptions.InternalCall)]
        [RuntimeImport(RuntimeLibrary, "RhGetGCDescSize")]
        internal static extern int RhGetGCDescSize(EETypePtr eeType);

        [MethodImplAttribute(MethodImplOptions.InternalCall)]
        [RuntimeImport(RuntimeLibrary, "RhCreateGenericInstanceDescForType2")]
        internal static unsafe extern bool RhCreateGenericInstanceDescForType2(EETypePtr pEEType, int arity, int nonGcStaticDataSize,
            int nonGCStaticDataOffset, int gcStaticDataSize, int threadStaticsOffset, void* pGcStaticsDesc, void* pThreadStaticsDesc, int* pGenericVarianceFlags);

        [MethodImplAttribute(MethodImplOptions.InternalCall)]
        [RuntimeImport(RuntimeLibrary, "RhAllocateMemory")]
        internal static unsafe extern IntPtr RhAllocateMemory(int sizeInBytes);

        [MethodImplAttribute(MethodImplOptions.InternalCall)]
        [RuntimeImport(RuntimeLibrary, "RhSetInterface")]
        internal static unsafe extern void RhSetInterface(EETypePtr pEEType, int index, EETypePtr pEETypeInterface);

        [MethodImplAttribute(MethodImplOptions.InternalCall)]
        [RuntimeImport(RuntimeLibrary, "RhSetGenericInstantiation")]
        internal static unsafe extern bool RhSetGenericInstantiation(EETypePtr pEEType, EETypePtr pEETypeDef, int arity, EETypePtr* pInstantiation);

        [MethodImplAttribute(MethodImplOptions.InternalCall)]
        [RuntimeImport(RuntimeLibrary, "RhNewInterfaceDispatchCell")]
        internal static unsafe extern IntPtr RhNewInterfaceDispatchCell(EETypePtr pEEType, int slotNumber);

        [MethodImplAttribute(MethodImplOptions.InternalCall)]
        [RuntimeImport(RuntimeLibrary, "RhResolveDispatch")]
        internal static extern IntPtr RhResolveDispatch(object pObject, EETypePtr pInterfaceType, ushort slot);

        [MethodImplAttribute(MethodImplOptions.InternalCall)]
        [RuntimeImport(RuntimeLibrary, "RhGetNonGcStaticFieldData")]
        internal static unsafe extern IntPtr RhGetNonGcStaticFieldData(EETypePtr pEEType);

        [MethodImplAttribute(MethodImplOptions.InternalCall)]
        [RuntimeImport(RuntimeLibrary, "RhGetGcStaticFieldData")]
        internal static unsafe extern IntPtr RhGetGcStaticFieldData(EETypePtr pEEType);

        [MethodImplAttribute(MethodImplOptions.InternalCall)]
        [RuntimeImport(RuntimeLibrary, "RhAllocateThunksFromTemplate")]
        internal static extern IntPtr RhAllocateThunksFromTemplate(IntPtr moduleHandle, int templateRva, int templateSize);

        [MethodImplAttribute(MethodImplOptions.InternalCall)]
        [RuntimeImport(RuntimeLibrary, "RhGetThreadLocalStorageForDynamicType")]
        internal static extern IntPtr RhGetThreadLocalStorageForDynamicType(int index, int tlsStorageSize, int numTlsCells);

        [MethodImplAttribute(MethodImplOptions.InternalCall)]
        [RuntimeImport(RuntimeLibrary, "RhIsDynamicType")]
        internal static unsafe extern bool RhIsDynamicType(EETypePtr pEEType);

        [MethodImplAttribute(MethodImplOptions.InternalCall)]
        [RuntimeImport(RuntimeLibrary, "RhHasCctor")]
        internal static unsafe extern bool RhHasCctor(EETypePtr pEEType);

        [MethodImplAttribute(MethodImplOptions.InternalCall)]
        [RuntimeImport(RuntimeLibrary, "RhResolveDispatchOnType")]
        internal static extern IntPtr RhResolveDispatchOnType(EETypePtr instanceType, EETypePtr interfaceType, ushort slot);

        // Keep in sync with RH\src\rtu\runtimeinstance.cpp
        internal enum RuntimeHelperKind
        {
            AllocateObject,
            IsInst,
            CastClass,
            AllocateArray,
            CheckArrayElementType,
        }

        [MethodImplAttribute(MethodImplOptions.InternalCall)]
        [RuntimeImport(RuntimeLibrary, "RhGetRuntimeHelperForType")]
        internal static unsafe extern IntPtr RhGetRuntimeHelperForType(EETypePtr pEEType, RuntimeHelperKind kind);

        [MethodImplAttribute(MethodImplOptions.InternalCall)]
        [RuntimeImport(RuntimeLibrary, "RhGetDispatchMapForType")]
        internal static unsafe extern IntPtr RhGetDispatchMapForType(EETypePtr pEEType);

        //
        // calls to runtime for process status
        //
        [MethodImplAttribute(MethodImplOptions.InternalCall)]
        [RuntimeImport(RuntimeLibrary, "RhEnableShutdownFinalization")]
        internal static extern void RhEnableShutdownFinalization(uint timeout);

        [MethodImplAttribute(MethodImplOptions.InternalCall)]
        [RuntimeImport(RuntimeLibrary, "RhHasShutdownStarted")]
        internal static extern bool RhHasShutdownStarted();


        internal enum GcRestrictedCalloutKind
        {
            StartCollection = 0, // Collection is about to begin
            EndCollection = 1, // Collection has completed
            AfterMarkPhase = 2, // All live objects are marked (not including ready for finalization objects),
                                // no handles have been cleared
        }

        //
        // Support for GC and HandleTable callouts.
        //
        [MethodImplAttribute(MethodImplOptions.InternalCall)]
        [RuntimeImport(RuntimeLibrary, "RhRegisterGcCallout")]
        internal static extern bool RhRegisterGcCallout(GcRestrictedCalloutKind eKind, IntPtr pCalloutMethod);

        [MethodImplAttribute(MethodImplOptions.InternalCall)]
        [RuntimeImport(RuntimeLibrary, "RhUnregisterGcCallout")]
        internal static extern void RhUnregisterGcCallout(GcRestrictedCalloutKind eKind, IntPtr pCalloutMethod);

        [MethodImplAttribute(MethodImplOptions.InternalCall)]
        [RuntimeImport(RuntimeLibrary, "RhRegisterRefCountedHandleCallback")]
        internal static extern bool RhRegisterRefCountedHandleCallback(IntPtr pCalloutMethod, EETypePtr pTypeFilter);

        [MethodImplAttribute(MethodImplOptions.InternalCall)]
        [RuntimeImport(RuntimeLibrary, "RhUnregisterRefCountedHandleCallback")]
        internal static extern void RhUnregisterRefCountedHandleCallback(IntPtr pCalloutMethod, EETypePtr pTypeFilter);

        [MethodImplAttribute(MethodImplOptions.InternalCall)]
        [RuntimeImport(RuntimeLibrary, "RhIsPromoted")]
        internal static extern bool RhIsPromoted(object obj);

        //
        // Blob support
        //
        [MethodImplAttribute(MethodImplOptions.InternalCall)]
        [RuntimeImport(RuntimeLibrary, "RhFindBlob")]
        internal static unsafe extern bool RhFindBlob(IntPtr hOsModule, uint blobId, byte** ppbBlob, uint* pcbBlob);

        [MethodImplAttribute(MethodImplOptions.InternalCall)]
        [RuntimeImport(RuntimeLibrary, "RhGetLoadedModules")]
        internal static extern uint RhGetLoadedModules(IntPtr[] resultArray);

        [MethodImplAttribute(MethodImplOptions.InternalCall)]
        [RuntimeImport(RuntimeLibrary, "RhGetModuleFromPointer")]
        internal static extern IntPtr RhGetModuleFromPointer(IntPtr pointerVal);

        [MethodImplAttribute(MethodImplOptions.InternalCall)]
        [RuntimeImport(RuntimeLibrary, "RhGetModuleFromEEType")]
        internal static extern IntPtr RhGetModuleFromEEType(IntPtr pEEType);

        [MethodImplAttribute(MethodImplOptions.InternalCall)]
        [RuntimeImport(RuntimeLibrary, "RhGetThreadStaticFieldAddress")]
        internal static unsafe extern byte* RhGetThreadStaticFieldAddress(EETypePtr pEEType, IntPtr fieldCookie);

        [MethodImplAttribute(MethodImplOptions.InternalCall)]
        [RuntimeImport(RuntimeLibrary, "RhGetCodeTarget")]
        internal static extern IntPtr RhGetCodeTarget(IntPtr pCode);
        //
        // EH helpers
        //
        [MethodImplAttribute(MethodImplOptions.InternalCall)]
        [RuntimeImport(RuntimeLibrary, "RhGetModuleFileName")]
        internal static extern unsafe int RhGetModuleFileName(IntPtr moduleHandle, out char* moduleName);

        [MethodImplAttribute(MethodImplOptions.InternalCall)]
        [RuntimeImport(RuntimeLibrary, "RhGetExceptionsForCurrentThread")]
        internal static extern unsafe bool RhGetExceptionsForCurrentThread(Exception[] outputArray, out int writtenCountOut);

        // returns the previous value.
        [MethodImplAttribute(MethodImplOptions.InternalCall)]
        [RuntimeImport(RuntimeLibrary, "RhSetErrorInfoBuffer")]
        internal static extern unsafe void* RhSetErrorInfoBuffer(void* pNewBuffer);

        //
        // StackTrace helper
        //

        [MethodImplAttribute(MethodImplOptions.InternalCall)]
        [RuntimeImport(RuntimeLibrary, "RhFindMethodStartAddress")]
        internal static extern unsafe IntPtr RhFindMethodStartAddress(IntPtr codeAddr);

        // Fetch a (managed) stack trace.  Fills in the given array with "return address IPs" for the current 
        // thread's (managed) stack (array index 0 will be the caller of this method).  The return value is 
        // the number of frames in the stack or a negative number (representing the required array size) if 
        // the passed-in buffer is too small.
        [MethodImplAttribute(MethodImplOptions.InternalCall)]
        [RuntimeImport(RuntimeLibrary, "RhGetCurrentThreadStackTrace")]
        internal static extern int RhGetCurrentThreadStackTrace(IntPtr[] outputBuffer);

        // Functions involved in thunks from managed to managed functions (Universal transition transitions 
        // from an arbitrary method call into a defined function, and CallDescrWorker goes the other way.
        [MethodImplAttribute(MethodImplOptions.InternalCall)]
        [RuntimeImport(RuntimeLibrary, "RhGetUniversalTransitionThunk")]
        internal static extern IntPtr RhGetUniversalTransitionThunk();

        [MethodImplAttribute(MethodImplOptions.InternalCall)]
        [RuntimeImport(RuntimeLibrary, "RhCallDescrWorker")]
        internal static extern void RhCallDescrWorker(IntPtr callDescr);

        // Moves memory from smem to dmem. Size must be a positive value.
        // This copy uses an intrinsic to be safe for copying arbitrary bits of
        // heap memory
        [MethodImplAttribute(MethodImplOptions.InternalCall)]
        [RuntimeImport(RuntimeLibrary, "RhBulkMoveWithWriteBarrier")]
        internal static extern unsafe void RhBulkMoveWithWriteBarrier(byte* dmem, byte* smem, int size);


        //
        // ETW helpers.
        //
        [MethodImplAttribute(MethodImplOptions.InternalCall)]
        [RuntimeImport(RuntimeLibrary, "RhpETWLogLiveCom")]
        internal extern static void RhpETWLogLiveCom(int eventType, IntPtr CCWHandle, IntPtr objectID, IntPtr typeRawValue, IntPtr IUnknown, IntPtr VTable, Int32 comRefCount, Int32 jupiterRefCount, Int32 flags);

        [MethodImplAttribute(MethodImplOptions.InternalCall)]
        [RuntimeImport(RuntimeLibrary, "RhpETWShouldWalkCom")]
        internal extern static bool RhpETWShouldWalkCom();

        [MethodImplAttribute(MethodImplOptions.InternalCall)]
        [RuntimeImport(RuntimeLibrary, "RhpEtwExceptionThrown")]
        internal extern static unsafe void RhpEtwExceptionThrown(char* exceptionTypeName, char* exceptionMessage, IntPtr faultingIP, long hresult);

        [Intrinsic]
        internal static extern double _copysign(double x, double y);

        [Intrinsic]
        internal static extern double floor(double x);

        [Intrinsic]
        internal static extern double fmod(double x, double y);

        [Intrinsic]
        internal static extern double pow(double x, double y);

        [Intrinsic]
        internal static extern double sqrt(double x);

        [Intrinsic]
        internal static extern double ceil(double x);

        [Intrinsic]
        internal static extern double cos(double x);

        [Intrinsic]
        internal static extern double sin(double x);

        [Intrinsic]
        internal static extern double tan(double x);

        [Intrinsic]
        internal static extern double cosh(double x);

        [Intrinsic]
        internal static extern double sinh(double x);

        [Intrinsic]
        internal static extern double tanh(double x);

        [Intrinsic]
        internal static extern double acos(double x);

        [Intrinsic]
        internal static extern double asin(double x);

        [Intrinsic]
        internal static extern double atan(double x);

        [Intrinsic]
        internal static extern double atan2(double x, double y);

        [Intrinsic]
        internal static extern double log(double x);

        [Intrinsic]
        internal static extern double log10(double x);

        [Intrinsic]
        internal static extern double exp(double x);

        [Intrinsic]
        internal static unsafe extern double modf(double x, double* intptr);

        // ExactSpelling = 'true' to force MCG to resolve it to default
        [DllImport(RuntimeImports.RuntimeLibrary, ExactSpelling = true)]
        internal static unsafe extern void _ecvt_s(byte* buffer, int sizeInBytes, double value, int count, int* dec, int* sign);

#if WIN64
        [DllImport(RuntimeImports.RuntimeLibrary, ExactSpelling = true)]
        internal static unsafe extern void memmove(byte* dmem, byte* smem, ulong size);
#else
        [DllImport(RuntimeImports.RuntimeLibrary, ExactSpelling = true)]
        internal static unsafe extern void memmove(byte* dmem, byte* smem, uint size);
#endif

        // Moves memory from smem to dmem. Size must be a positive value.
        [Intrinsic]
        internal static unsafe extern void memmove(byte* dmem, byte* smem, int size);

        [MethodImpl(MethodImplOptions.InternalCall)]
        [RuntimeImport(RuntimeLibrary, "RhpArrayCopy")]
        internal static extern bool TryArrayCopy(Array sourceArray, int sourceIndex, Array destinationArray, int destinationIndex, int length);

        [MethodImpl(MethodImplOptions.InternalCall)]
        [RuntimeImport(RuntimeLibrary, "RhpArrayClear")]
        internal static extern bool TryArrayClear(Array array, int index, int length);

        // Only the values defined below are valid. Any other value returned from RhGetCorElementType
        // indicates only that the type is not one of the primitives defined below and is otherwise undefined
        // and subject to change.
        internal enum RhCorElementType : byte
        {
            ELEMENT_TYPE_BOOLEAN = 0x2,
            ELEMENT_TYPE_CHAR = 0x3,
            ELEMENT_TYPE_I1 = 0x4,
            ELEMENT_TYPE_U1 = 0x5,
            ELEMENT_TYPE_I2 = 0x6,
            ELEMENT_TYPE_U2 = 0x7,
            ELEMENT_TYPE_I4 = 0x8,
            ELEMENT_TYPE_U4 = 0x9,
            ELEMENT_TYPE_I8 = 0xa,
            ELEMENT_TYPE_U8 = 0xb,
            ELEMENT_TYPE_R4 = 0xc,
            ELEMENT_TYPE_R8 = 0xd,
            ELEMENT_TYPE_I = 0x18,
            ELEMENT_TYPE_U = 0x19,
        }

        // Keep in sync with ProjectN\src\RH\src\rtm\System\Runtime\RuntimeExports.cs
        internal enum RhEETypeClassification
        {
            Regular,                // Object, String, Int32
            Array,                  // String[]
            Generic,                // List<Int32>
            GenericTypeDefinition,  // List<T>
            UnmanagedPointer,       // void*
        }

        internal static RhCorElementTypeInfo GetRhCorElementTypeInfo(RuntimeImports.RhCorElementType elementType)
        {
            return RhCorElementTypeInfo.GetRhCorElementTypeInfo(elementType);
        }

        internal struct RhCorElementTypeInfo
        {
            public RhCorElementTypeInfo(byte log2OfSize, ushort widenMask, bool isPrimitive = false)
            {
                _log2OfSize = log2OfSize;
                RhCorElementTypeInfoFlags flags = RhCorElementTypeInfoFlags.IsValid;
                if (isPrimitive)
                    flags |= RhCorElementTypeInfoFlags.IsPrimitive;
                _flags = flags;
                _widenMask = widenMask;
            }

            public bool IsPrimitive
            {
                get
                {
                    return 0 != (_flags & RhCorElementTypeInfoFlags.IsPrimitive);
                }
            }

            public bool IsFloat
            {
                get
                {
                    return 0 != (_flags & RhCorElementTypeInfoFlags.IsFloat);
                }
            }

            public byte Log2OfSize
            {
                get
                {
                    return _log2OfSize;
                }
            }

            //
            // This is a port of InvokeUtil::CanPrimitiveWiden() in the desktop runtime. This is used by various apis such as Array.SetValue()
            // and Delegate.DynamicInvoke() which allow value-preserving widenings from one primitive type to another.
            //
            public bool CanWidenTo(RuntimeImports.RhCorElementType targetElementType)
            {
                // Caller expected to ensure that both sides are primitive before calling us.
                Debug.Assert(this.IsPrimitive);
                Debug.Assert(GetRhCorElementTypeInfo(targetElementType).IsPrimitive);

                // Once we've asserted that the target is a primitive, we can also assert that it is >= ET_BOOLEAN.
                Debug.Assert(targetElementType >= RuntimeImports.RhCorElementType.ELEMENT_TYPE_BOOLEAN);
                byte targetElementTypeAsByte = (byte)targetElementType;
                ushort mask = (ushort)(1 << targetElementTypeAsByte);  // This is expected to overflow on larger ET_I and ET_U - this is ok and anticipated.
                if (0 != (_widenMask & mask))
                    return true;
                return false;
            }

            internal static RhCorElementTypeInfo GetRhCorElementTypeInfo(RuntimeImports.RhCorElementType elementType)
            {
                // The _lookupTable array only covers a subset of RhCorElementTypes, so we return a default 
                // info when someone asks for an elementType which does not have an entry in the table.
                if ((int)elementType > s_lookupTable.Length)
                    return default(RhCorElementTypeInfo);

                return s_lookupTable[(int)elementType];
            }


            private byte _log2OfSize;
            private RhCorElementTypeInfoFlags _flags;

            [Flags]
            private enum RhCorElementTypeInfoFlags : byte
            {
                IsValid = 0x01,       // Set for all valid CorElementTypeInfo's
                IsPrimitive = 0x02,   // Is it a primitive type (as defined by TypeInfo.IsPrimitive)
                IsFloat = 0x04,       // Is it a floating point type
            }

            private ushort _widenMask;


#if WIN64
            const byte log2PointerSize = 3;
#else
            private const byte log2PointerSize = 2;
#endif
            [PreInitialized] // The enclosing class (RuntimeImports) is depended upon (indirectly) by 
                             // __vtable_IUnknown, which is an eager-init class, so this type must not have a 
                             // lazy-init .cctor
            private static RhCorElementTypeInfo[] s_lookupTable = new RhCorElementTypeInfo[]
            {
                // index = 0x0
                new RhCorElementTypeInfo { _log2OfSize = 0, _widenMask = 0x0000, _flags = 0 },
                // index = 0x1
                new RhCorElementTypeInfo { _log2OfSize = 0, _widenMask = 0x0000, _flags = 0 },
                // index = 0x2 = ELEMENT_TYPE_BOOLEAN   (W = BOOL) 
                new RhCorElementTypeInfo { _log2OfSize = 0, _widenMask = 0x0004, _flags = RhCorElementTypeInfoFlags.IsValid|RhCorElementTypeInfoFlags.IsPrimitive },
                // index = 0x3 = ELEMENT_TYPE_CHAR      (W = U2, CHAR, I4, U4, I8, U8, R4, R8) (U2 == Char)
                new RhCorElementTypeInfo { _log2OfSize = 1, _widenMask = 0x3f88, _flags = RhCorElementTypeInfoFlags.IsValid|RhCorElementTypeInfoFlags.IsPrimitive },
                // index = 0x4 = ELEMENT_TYPE_I1        (W = I1, I2, I4, I8, R4, R8)
                new RhCorElementTypeInfo { _log2OfSize = 0, _widenMask = 0x3550, _flags = RhCorElementTypeInfoFlags.IsValid|RhCorElementTypeInfoFlags.IsPrimitive },
                // index = 0x5 = ELEMENT_TYPE_U1        (W = CHAR, U1, I2, U2, I4, U4, I8, U8, R4, R8)
                new RhCorElementTypeInfo { _log2OfSize = 0, _widenMask = 0x3FE8, _flags = RhCorElementTypeInfoFlags.IsValid|RhCorElementTypeInfoFlags.IsPrimitive },
                // index = 0x6 = ELEMENT_TYPE_I2        (W = I2, I4, I8, R4, R8)
                new RhCorElementTypeInfo { _log2OfSize = 1, _widenMask = 0x3540, _flags = RhCorElementTypeInfoFlags.IsValid|RhCorElementTypeInfoFlags.IsPrimitive },
                // index = 0x7 = ELEMENT_TYPE_U2        (W = U2, CHAR, I4, U4, I8, U8, R4, R8)
                new RhCorElementTypeInfo { _log2OfSize = 1, _widenMask = 0x3F88, _flags = RhCorElementTypeInfoFlags.IsValid|RhCorElementTypeInfoFlags.IsPrimitive },
                // index = 0x8 = ELEMENT_TYPE_I4        (W = I4, I8, R4, R8)
                new RhCorElementTypeInfo { _log2OfSize = 2, _widenMask = 0x3500, _flags = RhCorElementTypeInfoFlags.IsValid|RhCorElementTypeInfoFlags.IsPrimitive },
                // index = 0x9 = ELEMENT_TYPE_U4        (W = U4, I8, R4, R8)
                new RhCorElementTypeInfo { _log2OfSize = 2, _widenMask = 0x3E00, _flags = RhCorElementTypeInfoFlags.IsValid|RhCorElementTypeInfoFlags.IsPrimitive },
                // index = 0xa = ELEMENT_TYPE_I8        (W = I8, R4, R8)
                new RhCorElementTypeInfo { _log2OfSize = 3, _widenMask = 0x3400, _flags = RhCorElementTypeInfoFlags.IsValid|RhCorElementTypeInfoFlags.IsPrimitive },
                // index = 0xb = ELEMENT_TYPE_U8        (W = U8, R4, R8)
                new RhCorElementTypeInfo { _log2OfSize = 3, _widenMask = 0x3800, _flags = RhCorElementTypeInfoFlags.IsValid|RhCorElementTypeInfoFlags.IsPrimitive },
                // index = 0xc = ELEMENT_TYPE_R4        (W = R4, R8)
                new RhCorElementTypeInfo { _log2OfSize = 2, _widenMask = 0x3000, _flags = RhCorElementTypeInfoFlags.IsValid|RhCorElementTypeInfoFlags.IsPrimitive|RhCorElementTypeInfoFlags.IsFloat },
                // index = 0xd = ELEMENT_TYPE_R8        (W = R8)
                new RhCorElementTypeInfo { _log2OfSize = 3, _widenMask = 0x2000, _flags = RhCorElementTypeInfoFlags.IsValid|RhCorElementTypeInfoFlags.IsPrimitive|RhCorElementTypeInfoFlags.IsFloat },
                // index = 0xe
                new RhCorElementTypeInfo { _log2OfSize = 0, _widenMask = 0x0000, _flags = 0 },
                // index = 0xf
                new RhCorElementTypeInfo { _log2OfSize = 0, _widenMask = 0x0000, _flags = 0 },
                // index = 0x10
                new RhCorElementTypeInfo { _log2OfSize = 0, _widenMask = 0x0000, _flags = 0 },
                // index = 0x11
                new RhCorElementTypeInfo { _log2OfSize = 0, _widenMask = 0x0000, _flags = 0 },
                // index = 0x12
                new RhCorElementTypeInfo { _log2OfSize = 0, _widenMask = 0x0000, _flags = 0 },
                // index = 0x13
                new RhCorElementTypeInfo { _log2OfSize = 0, _widenMask = 0x0000, _flags = 0 },
                // index = 0x14
                new RhCorElementTypeInfo { _log2OfSize = 0, _widenMask = 0x0000, _flags = 0 },
                // index = 0x15
                new RhCorElementTypeInfo { _log2OfSize = 0, _widenMask = 0x0000, _flags = 0 },
                // index = 0x16
                new RhCorElementTypeInfo { _log2OfSize = 0, _widenMask = 0x0000, _flags = 0 },
                // index = 0x17
                new RhCorElementTypeInfo { _log2OfSize = 0, _widenMask = 0x0000, _flags = 0 },
                // index = 0x18 = ELEMENT_TYPE_I
                new RhCorElementTypeInfo { _log2OfSize = log2PointerSize, _widenMask = 0x0000, _flags = RhCorElementTypeInfoFlags.IsValid|RhCorElementTypeInfoFlags.IsPrimitive },
                // index = 0x19 = ELEMENT_TYPE_U
                new RhCorElementTypeInfo { _log2OfSize = log2PointerSize, _widenMask = 0x0000, _flags = RhCorElementTypeInfoFlags.IsValid|RhCorElementTypeInfoFlags.IsPrimitive },
            };
        }
    }
}

