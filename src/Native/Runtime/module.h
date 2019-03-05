// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

#ifndef __module_h__
#define __module_h__

#include "ICodeManager.h"

#include "SectionMethodList.h"
#include "TypeManager.h"

struct StaticGcDesc;
typedef SPTR(StaticGcDesc) PTR_StaticGcDesc;
struct IndirectionCell;
struct VSDInterfaceTargetInfo;
class DispatchMap;
struct BlobHeader;

#ifdef PROJECTN

class Module : public ICodeManager
{
    friend class AsmOffsets;
    friend struct DefaultSListTraits<Module>;
    friend class RuntimeInstance;
public:
    virtual ~Module();

    static Module *     Create(ModuleHeader *pModuleHeader);

    void                Destroy();

    bool ContainsCodeAddress(PTR_VOID pvAddr);
    bool ContainsDataAddress(PTR_VOID pvAddr);
    bool ContainsReadOnlyDataAddress(PTR_VOID pvAddr);
    bool ContainsStubAddress(PTR_VOID pvAddr);

    static void EnumStaticGCRefsBlock(void * pfnCallback, void * pvCallbackData, PTR_StaticGcDesc pStaticGcInfo, PTR_UInt8 pbStaticData);
    void EnumStaticGCRefs(void * pfnCallback, void * pvCallbackData);

    // Get the classlib module that this module was compiled against.
    Module * GetClasslibModule();

    // Is this a classlib module?
    bool IsClasslibModule();

    // Get classlib-defined helpers for the exception system.
    void * GetClasslibFunction(ClasslibFunctionId functionId);

    // Get classlib-defined helper for running deferred static class constructors.
    void * GetClasslibCheckStaticClassConstruction();

    // Returns the classlib-defined helper for initializing the finalizer thread.  The contract is that it 
    // will be run before any object based on that classlib is finalized.
    void * GetClasslibInitializeFinalizerThread();

    // Returns a pointer to the unwind info blob for the module 
    PTR_UInt8 GetUnwindInfoBlob();
    PTR_UInt8 GetCallsiteStringBlob();
    PTR_UInt8 GetDeltaShortcutTable();

    // Returns true if this module is part of the OS module specified by hOsHandle.
    bool IsContainedBy(HANDLE hOsHandle);

    void UnregisterFrozenSection();

    PTR_UInt8 FindMethodStartAddress(PTR_VOID ControlPC);

    bool FindMethodInfo(PTR_VOID        ControlPC, 
                        MethodInfo *    pMethodInfoOut);

    bool IsFunclet(MethodInfo * pMethodInfo);

    PTR_VOID GetFramePointer(MethodInfo *   pMethodInfo, 
                             REGDISPLAY *   pRegisterSet);

    void EnumGcRefs(MethodInfo *    pMethodInfo, 
                    PTR_VOID        safePointAddress,
                    REGDISPLAY *    pRegisterSet,
                    GCEnumContext * hCallback);

    bool UnwindStackFrame(MethodInfo *    pMethodInfo,
                          REGDISPLAY *    pRegisterSet,
                          PTR_VOID *      ppPreviousTransitionFrame);

    UIntNative GetConservativeUpperBoundForOutgoingArgs(MethodInfo *   pMethodInfo,
                                                        REGDISPLAY *   pRegisterSet);

    bool GetReturnAddressHijackInfo(MethodInfo *     pMethodInfo,
                                    REGDISPLAY *     pRegisterSet,
                                    PTR_PTR_VOID *   ppvRetAddrLocation,
                                    GCRefKind *      pRetValueKind);

    // BEWARE: care must be taken when using these Unsynchronized methods. Only one thread may call this at a time.
    void UnsynchronizedHijackMethodLoops(MethodInfo * pMethodInfo);
    void UnsynchronizedResetHijackedLoops();
    void UnsynchronizedHijackAllLoops();

    bool EHEnumInit(MethodInfo * pMethodInfo, PTR_VOID * pMethodStartAddressOut, EHEnumState * pEHEnumStateOut);
    bool EHEnumNext(EHEnumState * pEHEnumState, EHClause * pEHClauseOut);

    PTR_VOID GetMethodStartAddress(MethodInfo * pMethodInfo);

    PTR_VOID RemapHardwareFaultToGCSafePoint(MethodInfo * pMethodInfo, PTR_VOID controlPC);

    DispatchMap ** GetDispatchMapLookupTable();
    
    PTR_ModuleHeader GetModuleHeader();

    PTR_VOID GetOsModuleHandle();

    BlobHeader * GetReadOnlyBlobs(UInt32 * pcbBlobs);

    EEType * GetArrayBaseType();

    bool IsFinalizerInitComplete() { return m_fFinalizerInitComplete; }
    void SetFinalizerInitComplete() { m_fFinalizerInitComplete = true; }

    void * RecoverLoopHijackTarget(UInt32 entryIndex, ModuleHeader * pModuleHeader);

    PTR_VOID GetAssociatedData(PTR_VOID ControlPC);

private:
    Module(ModuleHeader * pModuleHeader);
#ifdef FEATURE_CUSTOM_IMPORTS
    static void DoCustomImports(ModuleHeader * pModuleHeader);
    PTR_UInt8 GetBaseAddress() { return (PTR_UInt8)GetOsModuleHandle(); }
#endif // FEATURE_CUSTOM_IMPORTS


    static void UnsynchronizedHijackLoop(void ** ppvIndirectionCell, UInt32 cellIndex, 
                                         void * pvRedirStubsStart, UInt8 * pbDirtyBitmap);

    PTR_Module                  m_pNext;

    PTR_UInt8                   m_pbDeltaShortcutTable;   // 16-byte array of the most popular deltas

    PTR_ModuleHeader            m_pModuleHeader;
    void *                      m_pEHTypeTable;
    SectionMethodList           m_MethodList;
    GcSegmentHandle             m_FrozenSegment;
    HANDLE                      m_hOsModuleHandle;
    bool                        m_fFinalizerInitComplete;   // used only by classlib modules

    PTR_StaticGcDesc            m_pStaticsGCInfo;
    PTR_StaticGcDesc            m_pThreadStaticsGCInfo;
    PTR_UInt8                   m_pStaticsGCDataSection;

    ReaderWriterLock            m_loopHijackMapLock;
    MapSHash<UInt32, void*>     m_loopHijackIndexToTargetMap;
};

#else // PROJECTN

// Stubbed out implementation of "Module" code manager. The "Module" code managed is needed for MDIL binder
// generated binaries in ProjectN only.

class Module : public ICodeManager
{
    friend struct DefaultSListTraits<Module>;
    friend class RuntimeInstance;

public:
    static Module * Create(ModuleHeader *pModuleHeader) { return NULL; }
    void Destroy() { }

    bool ContainsCodeAddress(PTR_VOID pvAddr) { return false; }
    bool ContainsDataAddress(PTR_VOID pvAddr) { return false; }
    bool ContainsReadOnlyDataAddress(PTR_VOID pvAddr) { return false; }
    bool ContainsStubAddress(PTR_VOID pvAddr) { return false; }

    static void EnumStaticGCRefsBlock(void * pfnCallback, void * pvCallbackData, PTR_StaticGcDesc pStaticGcInfo, PTR_UInt8 pbStaticData) { }
    void EnumStaticGCRefs(void * pfnCallback, void * pvCallbackData) { }

    bool IsClasslibModule() { return false; }
    void * GetClasslibInitializeFinalizerThread() { return NULL; }

    bool IsContainedBy(HANDLE hOsHandle) { return false; }

    DispatchMap ** GetDispatchMapLookupTable() { return NULL; }

    PTR_ModuleHeader GetModuleHeader() { return NULL; }

    EEType * GetArrayBaseType() { return NULL; }

    bool IsFinalizerInitComplete() { return false; }
    void SetFinalizerInitComplete() { }

    void UnsynchronizedResetHijackedLoops() { }
    void UnsynchronizedHijackAllLoops() { }

    void * RecoverLoopHijackTarget(UInt32 entryIndex, ModuleHeader * pModuleHeader) { return NULL; }

private:
    PTR_Module m_pNext;
};

#endif // PROJECTN

#endif // __module_h__
