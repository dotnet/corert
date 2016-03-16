// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.
class ThreadStore;
typedef DPTR(ThreadStore) PTR_ThreadStore;
class Module;
typedef DPTR(Module) PTR_Module;
class ICodeManager;
struct GenericInstanceDesc;
typedef SPTR(GenericInstanceDesc) PTR_GenericInstanceDesc;
struct ModuleHeader;
enum GenericVarianceType : UInt8;
struct UnifiedGenericInstance;
class GenericTypeHashTable;
typedef DPTR(GenericTypeHashTable) PTR_GenericTypeHashTable;
struct StaticGcDesc;

class RuntimeInstance
{
    friend class AsmOffsets;
    friend struct DefaultSListTraits<RuntimeInstance>;
    friend class Thread;

    PTR_RuntimeInstance         m_pNext;
    PTR_ThreadStore             m_pThreadStore;
    HANDLE                      m_hPalInstance; // this is the HANDLE passed into DllMain
    SList<Module>               m_ModuleList;
    ReaderWriterLock            m_ModuleListLock;

#ifdef FEATURE_DYNAMIC_CODE
    struct CodeManagerEntry;
    typedef DPTR(CodeManagerEntry) PTR_CodeManagerEntry;

    struct CodeManagerEntry
    {
        PTR_CodeManagerEntry    m_pNext;
        PTR_VOID                m_pvStartRange;
        UInt32                  m_cbRange;
        ICodeManager *          m_pCodeManager;
    };

    typedef SList<CodeManagerEntry> CodeManagerList;
    CodeManagerList             m_CodeManagerList;
#endif

    // Indicates whether the runtime is in standalone exe mode where the only Redhawk module that will be
    // loaded into the process (besides the runtime's own module) is the exe itself. This flag will be 
    // correctly initialized once the exe module has loaded.
    bool                        m_fStandaloneExeMode;

    // If m_fStandaloneExeMode is set this contains a pointer to the exe module. Otherwise it's null.
    Module *                    m_pStandaloneExeModule;

#ifdef FEATURE_PROFILING
    // The thread writing the profile data is created lazily, whenever
    // a module with a profile section is registered.
    // To avoid starting the thread more than once, this flag indicates
    // whether the thread has been created already.
    bool                        m_fProfileThreadCreated;
#endif

    // List of generic instances that have GC references to report. This list is updated under the hash table
    // lock above and enumerated without lock during garbage collections (when updates cannot occur). This
    // list is only used in non-standalone exe mode, i.e. when we're unifying generic types. In standalone
    // mode we report the GenericInstanceDescs directly from the module itself.
    PTR_GenericInstanceDesc     m_genericInstReportList;

    ReaderWriterLock            m_GenericHashTableLock;

    // This is used (in standalone mode only) to build an on-demand hash tables of all generic instantiations
    PTR_GenericTypeHashTable            m_pGenericTypeHashTable;

    bool                        m_conservativeStackReportingEnabled;

    RuntimeInstance();

    SList<Module>* GetModuleList();

    bool BuildGenericTypeHashTable();

public:
    class ModuleIterator
    {
        ReaderWriterLock::ReadHolder    m_readHolder;
        PTR_Module                      m_pCurrentPosition;
    public:
        ModuleIterator();
        ~ModuleIterator();
        PTR_Module GetNext();
    };

    ~RuntimeInstance();
    ThreadStore *   GetThreadStore();
    HANDLE          GetPalInstance();

    bool RegisterModule(ModuleHeader *pModuleHeader);
    void UnregisterModule(Module *pModule);
    Module * FindModuleByAddress(PTR_VOID pvAddress);
    Module * FindModuleByCodeAddress(PTR_VOID ControlPC);
    Module * FindModuleByDataAddress(PTR_VOID Data);
    Module * FindModuleByReadOnlyDataAddress(PTR_VOID Data);
    Module * FindModuleByOsHandle(HANDLE hOsHandle);
    PTR_UInt8 FindMethodStartAddress(PTR_VOID ControlPC);
    bool EnableConservativeStackReporting();
    bool IsConservativeStackReportingEnabled() { return m_conservativeStackReportingEnabled; }

#ifdef FEATURE_DYNAMIC_CODE
    bool RegisterCodeManager(ICodeManager * pCodeManager, PTR_VOID pvStartRange, UInt32 cbRange);
    void UnregisterCodeManager(ICodeManager * pCodeManager);
#endif
    ICodeManager * FindCodeManagerByAddress(PTR_VOID ControlPC);

    // This will hold the module list lock over each callback. Make sure
    // the callback will not trigger any operation that needs to make use
    // of the module list.
    typedef void (* EnumerateModulesCallbackPFN)(Module *pModule, void *pvContext);
    void EnumerateModulesUnderLock(EnumerateModulesCallbackPFN pCallback, void *pvContext);

    static  RuntimeInstance *   Create(HANDLE hPalInstance);
    void Destroy();

    void EnumGenericStaticGCRefs(PTR_GenericInstanceDesc pInst, void * pfnCallback, void * pvCallbackData, Module *pModule);
    void EnumAllStaticGCRefs(void * pfnCallback, void * pvCallbackData);

    bool ShouldHijackCallsiteForGcStress(UIntNative CallsiteIP);
    bool ShouldHijackLoopForGcStress(UIntNative CallsiteIP);

    void EnableGcPollStress();
    void UnsychronizedResetHijackedLoops();

    // Given the EEType* for an instantiated generic type retrieve the GenericInstanceDesc associated with
    // that type. This is legal only for types that are guaranteed to have this metadata at runtime; generic
    // types which have variance over one or more of their type parameters and generic interfaces on array).
    GenericInstanceDesc * LookupGenericInstance(EEType * pEEType);

    // Given the EEType* for an instantiated generic type retrieve instantiation information (generic type
    // definition EEType, arity, type arguments and variance info for each type parameter). Has the same
    // limitations on usage as LookupGenericInstance above.
    EEType * GetGenericInstantiation(EEType *               pEEType,
                                     UInt32 *               pArity,
                                     EEType ***             ppInstantiation,
                                     GenericVarianceType ** ppVarianceInfo);

    bool SetGenericInstantiation(EEType *               pEEType,
                                 EEType *               pEETypeDef,
                                 UInt32                 arity,
                                 EEType **              pInstantiation);

    bool CreateGenericInstanceDesc(EEType *             pEEType,
                                   EEType *             pTemplateType,
                                   UInt32               arity,
                                   UInt32               nonGcStaticDataSize,
                                   UInt32               nonGCStaticDataOffset,
                                   UInt32               gcStaticDataSize,
                                   UInt32               threadStaticOffset,
                                   StaticGcDesc *       pGcStaticsDesc,
                                   StaticGcDesc *       pThreadStaticsDesc,
                                   UInt32*              pGenericVarianceFlags);

#ifdef FEATURE_PROFILING
    void InitProfiling(ModuleHeader *pModuleHeader);
    void WriteProfileInfo();
#endif // FEATURE_PROFILING

    bool IsInStandaloneExeMode()
    {
        return m_fStandaloneExeMode;
    }

    Module * GetStandaloneExeModule()
    {
        ASSERT(IsInStandaloneExeMode());
        return m_pStandaloneExeModule;
    }
};
typedef DPTR(RuntimeInstance) PTR_RuntimeInstance;


PTR_RuntimeInstance GetRuntimeInstance();


#define FOREACH_MODULE(p_module_name)                       \
{                                                           \
    RuntimeInstance::ModuleIterator __modules;              \
    Module * p_module_name;                                 \
    while ((p_module_name = __modules.GetNext()) != NULL)   \
    {                                                       \

#define END_FOREACH_MODULE  \
    }                       \
}                           \


