project(Runtime)

set(CMAKE_INCLUDE_CURRENT_DIR ON)

include_directories(../gc)
include_directories(../gc/env)

set(SOURCES
    allocheap.cpp
    AsmOffsets.cpp
#    AsmOffsetsVerify.cpp
    assert.cpp
    Crst.cpp
    dllmain.cpp
    eetype.cpp
    EHHelpers.cpp
    event.cpp
    FinalizerHelpers.cpp
    gcdump.cpp
    GCHelpers.cpp
    gcrhenv.cpp
    gcrhscan.cpp
    GcStressControl.cpp
    GenericInstance.cpp
    HandleTableHelpers.cpp
    MathHelpers.cpp
    MiscHelpers.cpp
    module.cpp
    ObjectLayout.cpp
    OptionalFieldsRuntime.cpp
    portable.cpp
    profheapwalkhelper.cpp
    Profiling.cpp
    RestrictedCallouts.cpp
    RHCodeMan.cpp
    RhConfig.cpp
    RuntimeInstance.cpp
    RWLock.cpp
    SectionMethodList.cpp
    StackFrameIterator.cpp
    startup.cpp
    stressLog.cpp
    SyncClean.cpp
    thread.cpp
    threadstore.cpp
    
    ../gc/gccommon.cpp
    ../gc/gceewks.cpp
    ../gc/gcwks.cpp
    ../gc/handletable.cpp
    ../gc/handletablecache.cpp
    ../gc/handletablecore.cpp
    ../gc/handletablescan.cpp
    ../gc/objecthandle.cpp
    ../gc/env/common.cpp    
)

if(WIN32)
  list(APPEND SOURCES
    DebugEventSource.cpp
    ../gc/env/gcenv.windows.cpp    
    PalRedhawkCommon.cpp
    PalRedhawkMinWin.cpp
  )
else()
  include_directories(unix)
  list(APPEND SOURCES
    ../gc/env/gcenv.unix.cpp    
  )
endif()

if(CLR_CMAKE_PLATFORM_ARCH_AMD64)
  add_definitions(-D_TARGET_AMD64_=1)
  add_definitions(-D_AMD64_)
  add_definitions(-DAMD64)
  add_definitions(-DTARGET_X64)
  add_definitions(-DTARGET_AMD64)
  add_definitions(-D_WIN64=1)
elseif(CLR_CMAKE_PLATFORM_ARCH_I386)
  add_definitions(-D_TARGET_X86_=1)
  add_definitions(-D_X86_)
  add_definitions(-DTARGET_X86)
  add_definitions(-D_WIN32=1)
elseif(CLR_CMAKE_PLATFORM_ARCH_ARM)
  add_definitions(-D_TARGET_ARM_=1)
  add_definitions(-D_ARM_)
  add_definitions(-DTARGET_ARM)
  add_definitions(-D_WIN32=1)
elseif(CLR_CMAKE_PLATFORM_ARCH_ARM64)
  add_definitions(-D_TARGET_ARM64_=1)
  add_definitions(-D_WIN64=1)
else()
  clr_unknown_arch()
endif()

add_definitions(-DAPP_LOCAL_RUNTIME)
add_definitions(-DFEATURE_BACKGROUND_GC)
add_definitions(-DFEATURE_BASICFREEZE)
add_definitions(-DFEATURE_CLR_EH)
add_definitions(-DFEATURE_COFF_OUTPUT)
add_definitions(-DFEATURE_CONSERVATIVE_GC)
add_definitions(-DFEATURE_CUSTOM_IMPORTS)
add_definitions(-DFEATURE_DECLSPEC_THREAD)
add_definitions(-DFEATURE_DYNAMIC_CODE)
add_compile_options($<$<CONFIG:Debug>:-DFEATURE_GC_STRESS>)
add_definitions(-DFEATURE_NUTC)
add_definitions(-DFEATURE_PROFILING)
add_definitions(-DFEATURE_REDHAWK)
add_definitions(-DFEATURE_SHARED_GENERICS)
add_definitions(-DMODERN_OS)
add_definitions(-DUSE_PORTABLE_HELPERS)
add_definitions(-DVERIFY_HEAP)

add_definitions(-DRTU_PORTABLE)
add_definitions(-D_LIB)

if(WIN32)
  # There is a problem with undefined symbol g_pConfig, windows don't care since it is in template method, but clang does 
  add_definitions(-DSTRESS_HEAP)
  add_compile_options(/GS)
  add_compile_options(/W1)
  add_compile_options(/Zc:wchar_t)
  add_compile_options($<$<CONFIG:Debug>:/ZI>)
  add_compile_options($<$<CONFIG:Debug>:/Gm>)
  add_compile_options($<$<CONFIG:Debug>:/Od>)
  add_compile_options(/Zc:inline)
  add_compile_options(/fp:precise)
  add_compile_options(/EHsc)
else()
  add_compile_options(-Wno-format)
  add_compile_options(-Wno-unused-variable)
  add_compile_options(-Wno-unused-private-field)
  add_compile_options(-Wno-tautological-undefined-compare)

  add_compile_options(-Wno-unknown-pragmas)
  add_compile_options(-Wno-ignored-attributes)
  add_compile_options(-Wno-unused-value)
  add_compile_options(-Wno-undefined-inline)
  add_compile_options(-Wno-unused-function)
  add_compile_options(-Wno-self-assign)
endif()

add_library(Runtime STATIC ${SOURCES})

# Install the static Runtime library
install (TARGETS Runtime DESTINATION lib)
