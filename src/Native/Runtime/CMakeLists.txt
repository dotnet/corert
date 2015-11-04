set(COMMON_RUNTIME_SOURCES
    allocheap.cpp
    assert.cpp
    Crst.cpp
    DebugEventSource.cpp
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
)

set(FULL_RUNTIME_SOURCES
    # AsmOffsetsVerify.cpp
)

set(PORTABLE_RUNTIME_SOURCES
)

if(WIN32)

  include_directories(windows)

  list(APPEND COMMON_RUNTIME_SOURCES
    windows/PalRedhawkCommon.cpp
    windows/PalRedhawkMinWin.cpp
  )

  if(CLR_CMAKE_PLATFORM_ARCH_AMD64)
    set(ARCH_SOURCES_DIR amd64)
    set(ASM_SUFFIX asm)
  endif()

else()

  include_directories(unix)

  list(APPEND COMMON_RUNTIME_SOURCES
    ../gc/env/gcenv.unix.cpp    
    unix/PalRedhawkUnix.cpp
  )

  if(CLR_CMAKE_PLATFORM_ARCH_AMD64)
    set(ARCH_SOURCES_DIR amd64)
    set(ASM_SUFFIX S)
  endif()

endif()

set(RUNTIME_SOURCES_ARCH_ASM
  ${ARCH_SOURCES_DIR}/WriteBarriers.${ASM_SUFFIX}
)

add_definitions(-DAPP_LOCAL_RUNTIME)
add_definitions(-DFEATURE_BACKGROUND_GC)
add_definitions(-DFEATURE_BASICFREEZE)
add_definitions(-DFEATURE_CONSERVATIVE_GC)
add_definitions(-DFEATURE_CUSTOM_IMPORTS)
add_definitions(-DFEATURE_DYNAMIC_CODE)
add_compile_options($<$<CONFIG:Debug>:-DFEATURE_GC_STRESS>)
add_definitions(-DFEATURE_PROFILING)
add_definitions(-DFEATURE_REDHAWK)
add_definitions(-DVERIFY_HEAP)
add_definitions(-DCORERT)

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
  add_definitions(-DNO_UI_ASSERT)

  add_compile_options(-Wno-format)
  add_compile_options(-Wno-ignored-attributes)
  add_compile_options(-Wno-self-assign)
  add_compile_options(-Wno-tautological-undefined-compare)
  add_compile_options(-Wno-undefined-inline)
  add_compile_options(-Wno-unknown-pragmas)
  add_compile_options(-Wno-unused-function)
  add_compile_options(-Wno-unused-private-field)
  add_compile_options(-Wno-unused-value)
  add_compile_options(-Wno-unused-variable)

  add_compile_options(-Qunused-arguments)

  include(unix/configure.cmake)
  include_directories(${CMAKE_CURRENT_BINARY_DIR})
endif()

set(RUNTIME_DIR ${CMAKE_CURRENT_SOURCE_DIR})

convert_to_absolute_path(COMMON_RUNTIME_SOURCES ${COMMON_RUNTIME_SOURCES})

convert_to_absolute_path(FULL_RUNTIME_SOURCES ${FULL_RUNTIME_SOURCES})
convert_to_absolute_path(PORTABLE_RUNTIME_SOURCES ${PORTABLE_RUNTIME_SOURCES})

convert_to_absolute_path(RUNTIME_SOURCES_ARCH_ASM ${RUNTIME_SOURCES_ARCH_ASM})

add_subdirectory(Full)
add_subdirectory(Portable)
