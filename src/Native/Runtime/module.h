//
// Copyright (c) Microsoft Corporation.  All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.
//
#include "ICodeManager.h"

#include "SectionMethodList.h"

struct StaticGcDesc;
typedef SPTR(StaticGcDesc) PTR_StaticGcDesc;
struct IndirectionCell;
struct VSDInterfaceTargetInfo;
class DispatchMap;
struct BlobHeader;
struct GenericInstanceDesc;
typedef SPTR(struct GenericInstanceDesc) PTR_GenericInstanceDesc;
struct SimpleModuleHeader;

class Module
//#ifndef DACCESS_COMPILE
    // TODO: JIT support in DAC
    : public ICodeManager
//#endif
{
#ifdef DACCESS_COMPILE
    // The DAC does not support registration of dynamic code managers yet, but we need a space for the vtable used at runtime.
    // TODO: JIT support in DAC
    TADDR m_vptr;
#endif

    friend class AsmOffsets;
    friend struct DefaultSListTraits<Module>;
    friend class RuntimeInstance;
public:
    virtual ~Module();

    static Module *     Create(ModuleHeader *pModuleHeader);
    static Module *     Create(SimpleModuleHeader *pModuleHeader);

    void                Destroy();

    bool ContainsCodeAddress(PTR_VOID pvAddr);
    bool ContainsDataAddress(PTR_VOID pvAddr);
    bool ContainsReadOnlyDataAddress(PTR_VOID pvAddr);
    bool ContainsStubAddress(PTR_VOID pvAddr);

    static void EnumStaticGCRefsBlock(void * pfnCallback, void * pvCallbackData, PTR_StaticGcDesc pStaticGcInfo, PTR_UInt8 pbStaticData);
    void EnumStaticGCRefs(void * pfnCallback, void * pvCallbackData);

#ifdef FEATURE_VSD

    //
    // VSD support
    //
    IndirectionCell *           GetIndirectionCellArray();
    UInt32                      GetIndirectionCellArrayCount();
    VSDInterfaceTargetInfo *    GetInterfaceTargetInfoArray();

#endif // FEATURE_VSD

    // Get the classlib module that this module was compiled against.
    Module * GetClasslibModule();

    // Is this a classlib module?
    bool IsClasslibModule();

    // Get classlib-defined helpers for the exception system.
    void * GetClasslibRuntimeExceptionHelper();
    void * GetClasslibFailFastHelper();
    void * GetClasslibUnhandledExceptionHandlerHelper();
    void * GetClasslibAppendExceptionStackFrameHelper();

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

    // NULL out any GC references held by statics in this module. Note that this is unsafe unless we know that
    // no code is making (or can make) any reference to these statics. Generally this is only true when we are
    // about to unload the module.
    void ClearStaticRoots();

    void UnregisterFrozenSection();

    // Remove from the system any generic instantiations published by this module and not required by any
    // other module currently loaded.
    void UnregisterGenericInstances();

    PTR_UInt8 FindMethodStartAddress(PTR_VOID ControlPC);

    bool FindMethodInfo(PTR_VOID        ControlPC, 
                        MethodInfo *    pMethodInfoOut,
                        UInt32 *        pCodeOffset);

    bool IsFunclet(MethodInfo * pMethodInfo);

    PTR_VOID GetFramePointer(MethodInfo *   pMethodInfo, 
                             REGDISPLAY *   pRegisterSet);

    void EnumGcRefs(MethodInfo *    pMethodInfo, 
                    UInt32          codeOffset,
                    REGDISPLAY *    pRegisterSet,
                    GCEnumContext * hCallback);

    bool UnwindStackFrame(MethodInfo *    pMethodInfo,
                          UInt32          codeOffset,
                          REGDISPLAY *    pRegisterSet,
                          PTR_VOID *      ppPreviousTransitionFrame);

    bool GetReturnAddressHijackInfo(MethodInfo *     pMethodInfo,
                                    UInt32           codeOffset,
                                    REGDISPLAY *     pRegisterSet,
                                    PTR_PTR_VOID *   ppvRetAddrLocation,
                                    GCRefKind *      pRetValueKind);

    PTR_GenericInstanceDesc GetGidsWithGcRootsList();

    // BEWARE: care must be taken when using these Unsynchronized methods. Only one thread may call this at a time.
    void UnsynchronizedHijackMethodLoops(MethodInfo * pMethodInfo);
    void UnsynchronizedResetHijackedLoops();
    void UnsynchronizedHijackAllLoops();

    bool EHEnumInitFromReturnAddress(PTR_VOID ControlPC, PTR_VOID * pMethodStartAddressOut, EHEnumState * pEHEnumStateOut);

    bool EHEnumInit(MethodInfo * pMethodInfo, PTR_VOID * pMethodStartAddressOut, EHEnumState * pEHEnumStateOut);
    bool EHEnumNext(EHEnumState * pEHEnumState, EHClause * pEHClauseOut);

    void RemapHardwareFaultToGCSafePoint(MethodInfo * pMethodInfo, UInt32 * pCodeOffset);

    DispatchMap ** GetDispatchMapLookupTable();
    
    PTR_ModuleHeader GetModuleHeader();

    HANDLE GetOsModuleHandle();

    BlobHeader * GetReadOnlyBlobs(UInt32 * pcbBlobs);

    EEType * GetArrayBaseType();

    enum GenericInstanceDescKind
    {
        GenericInstances            = 1,
        GcRootGenericInstances      = 2,
        VariantGenericInstances     = 4,
        All = GenericInstances | GcRootGenericInstances | VariantGenericInstances
    };

    class GenericInstanceDescEnumerator
    {
        Module * m_pModule;

        GenericInstanceDesc * m_pCurrent;
        GenericInstanceDescKind m_gidEnumKind;
        UInt32 m_iCurrent;
        UInt32 m_nCount;

        Int32 m_iSection;

    public:
        GenericInstanceDescEnumerator(Module * pModule, GenericInstanceDescKind gidKind);
        GenericInstanceDesc * Next();
    };

    UInt32 GetGenericInstanceDescCount(GenericInstanceDescKind gidKind);

    bool IsFinalizerInitComplete() { return m_fFinalizerInitComplete; }
    void SetFinalizerInitComplete() { m_fFinalizerInitComplete = true; }

private:
    Module(ModuleHeader * pModuleHeader);
    bool RegisterGenericInstances();
#ifdef FEATURE_CUSTOM_IMPORTS
    static void DoCustomImports(ModuleHeader * pModuleHeader);
    PTR_UInt8 GetBaseAddress() { return (PTR_UInt8)(size_t)GetOsModuleHandle(); }
#endif // FEATURE_CUSTOM_IMPORTS

    static void UnsynchronizedHijackLoop(void ** ppvIndirectionCell, UInt32 cellIndex, 
                                         void * pvRedirStubsStart, UInt8 * pbDirtyBitmap);

    PTR_Module                  m_pNext;

    PTR_UInt8                   m_pbDeltaShortcutTable;   // 16-byte array of the most popular deltas

    PTR_ModuleHeader            m_pModuleHeader;
    SimpleModuleHeader *        m_pSimpleModuleHeader;
    void *                      m_pEHTypeTable;
    SectionMethodList           m_MethodList;
    GcSegmentHandle             m_FrozenSegment;
    HANDLE                      m_hOsModuleHandle;
    bool                        m_fFinalizerInitComplete;   // used only by classlib modules

    PTR_StaticGcDesc            m_pStaticsGCInfo;
    PTR_StaticGcDesc            m_pThreadStaticsGCInfo;
    PTR_UInt8                   m_pStaticsGCDataSection;
};

