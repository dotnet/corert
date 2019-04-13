set(COMMON_RUNTIME_SOURCES
    allocheap.cpp
    rhassert.cpp
    CachedInterfaceDispatch.cpp
    Crst.cpp
    DebugEventSource.cpp
    DebugFuncEval.cpp
    DebuggerHook.cpp
    eetype.cpp
    EHHelpers.cpp
    event.cpp
    FinalizerHelpers.cpp
    GCHelpers.cpp
    gcheaputilities.cpp
    GCMemoryHelpers.cpp
    gcrhenv.cpp
    gcrhscan.cpp
    GcStressControl.cpp
    GenericUnification.cpp
    HandleTableHelpers.cpp
    MathHelpers.cpp
    MiscHelpers.cpp
    TypeManager.cpp
    ObjectLayout.cpp
    OptionalFieldsRuntime.cpp
    portable.cpp
    profheapwalkhelper.cpp
    RestrictedCallouts.cpp
    RhConfig.cpp
    RuntimeInstance.cpp
    RWLock.cpp
    SectionMethodList.cpp
    sha1.cpp
    StackFrameIterator.cpp
    startup.cpp
    stressLog.cpp
    strongname.cpp
    SyncClean.cpp
    thread.cpp
    threadstore.cpp
    UniversalTransitionHelpers.cpp

    # ProjectN only
    # gcdump.cpp
    # module.cpp
    # RHCodeMan.cpp

    ../gc/gccommon.cpp
    ../gc/gceewks.cpp
    ../gc/gcwks.cpp
    ../gc/gcscan.cpp
    ../gc/handletable.cpp
    ../gc/handletablecache.cpp
    ../gc/handletablecore.cpp
    ../gc/handletablescan.cpp
    ../gc/objecthandle.cpp
)

set(SERVER_GC_SOURCES
    ../gc/gceesvr.cpp
    ../gc/gcsvr.cpp
)

set(FULL_RUNTIME_SOURCES
    AsmOffsetsVerify.cpp
    ThunksMapping.cpp
)

set(RUNTIME_SOURCES_ARCH_ASM
)

set(PORTABLE_RUNTIME_SOURCES
)

include_directories(inc)

if(WIN32)

  include_directories(windows)

  list(APPEND COMMON_RUNTIME_SOURCES
    windows/PalRedhawkCommon.cpp
    windows/PalRedhawkMinWin.cpp
    eventtrace.cpp
    rheventtrace.cpp
  )

  list(APPEND FULL_RUNTIME_SOURCES
    windows/CoffNativeCodeManager.cpp
  )

  set(ASM_SUFFIX asm)

  if(CLR_CMAKE_PLATFORM_ARCH_AMD64)
    set(ARCH_SOURCES_DIR amd64)
  elseif(CLR_CMAKE_PLATFORM_ARCH_I386)
    set(ARCH_SOURCES_DIR i386)
  endif()

  list(APPEND RUNTIME_SOURCES_ARCH_ASM
    ${ARCH_SOURCES_DIR}/GC.${ASM_SUFFIX}
    ${ARCH_SOURCES_DIR}/GcProbe.${ASM_SUFFIX}
  )

else()

  include_directories(unix)
  if(NOT CLR_CMAKE_PLATFORM_ARCH_WASM)
    include_directories(../libunwind/include)
  else()
    include_directories($ENV{EMSCRIPTEN/system/lib/libcxxabi/include})
  endif()

  include_directories(../libunwind/include)
  include_directories(../libunwind)

  # Disable building _Unwind_XXX style APIs of libunwind, since we don't use them.
  add_definitions(-D_LIBUNWIND_DISABLE_ZERO_COST_APIS=1)

  # Compile unwinding only for the current compilation target architecture
  add_definitions(-D_LIBUNWIND_IS_NATIVE_ONLY)

  list(APPEND COMMON_RUNTIME_SOURCES
    unix/PalRedhawkUnix.cpp
  )

  list(APPEND FULL_RUNTIME_SOURCES
    unix/HardwareExceptions.cpp
    unix/UnixContext.cpp
    unix/UnwindHelpers.cpp
    unix/UnixNativeCodeManager.cpp
    ../libunwind/src/Unwind-EHABI.cpp
    ../libunwind/src/libunwind.cpp
  )

  if(CLR_CMAKE_PLATFORM_DARWIN)
    list(APPEND FULL_RUNTIME_SOURCES
      ../libunwind/src/Unwind_AppleExtras.cpp
    )
  endif()

  set(ASM_SUFFIX S)
  if(CLR_CMAKE_PLATFORM_ARCH_AMD64)
    set(ARCH_SOURCES_DIR amd64)
  elseif(CLR_CMAKE_PLATFORM_ARCH_ARM64)
    set(ARCH_SOURCES_DIR arm64)
  elseif(CLR_CMAKE_PLATFORM_ARCH_ARM)
    set(ARCH_SOURCES_DIR arm)
  elseif(CLR_CMAKE_PLATFORM_ARCH_I386)
    set(ARCH_SOURCES_DIR i386)
  elseif(CLR_CMAKE_PLATFORM_ARCH_WASM)
    set(ARCH_SOURCES_DIR wasm)
  endif()

  list(APPEND RUNTIME_SOURCES_ARCH_ASM
    ../libunwind/src/UnwindRegistersRestore.S
    ../libunwind/src/UnwindRegistersSave.S
  )
endif()

list(APPEND RUNTIME_SOURCES_ARCH_ASM
  ${ARCH_SOURCES_DIR}/AllocFast.${ASM_SUFFIX}
  ${ARCH_SOURCES_DIR}/CallDescrWorker.${ASM_SUFFIX}
  ${ARCH_SOURCES_DIR}/CallingConventionConverterHelpers.${ASM_SUFFIX}
  ${ARCH_SOURCES_DIR}/ExceptionHandling.${ASM_SUFFIX}
  ${ARCH_SOURCES_DIR}/Interlocked.${ASM_SUFFIX}
  ${ARCH_SOURCES_DIR}/PInvoke.${ASM_SUFFIX}
  ${ARCH_SOURCES_DIR}/InteropThunksHelpers.${ASM_SUFFIX}
  ${ARCH_SOURCES_DIR}/StubDispatch.${ASM_SUFFIX}
  ${ARCH_SOURCES_DIR}/UniversalTransition.${ASM_SUFFIX}
  ${ARCH_SOURCES_DIR}/WriteBarriers.${ASM_SUFFIX}
)

# Add architecture specific folder for looking up headers.
convert_to_absolute_path(ARCH_SOURCES_DIR ${ARCH_SOURCES_DIR})
include_directories(${ARCH_SOURCES_DIR})

add_definitions(-DFEATURE_BACKGROUND_GC)
add_definitions(-DFEATURE_BASICFREEZE)
add_definitions(-DFEATURE_CONSERVATIVE_GC)
add_definitions(-DFEATURE_CUSTOM_IMPORTS)
add_definitions(-DFEATURE_DYNAMIC_CODE)
add_compile_options($<$<CONFIG:Debug>:-DFEATURE_GC_STRESS>)
add_definitions(-DFEATURE_REDHAWK)
add_definitions(-DVERIFY_HEAP)
add_definitions(-DCORERT)
add_definitions(-DFEATURE_CACHED_INTERFACE_DISPATCH)
add_definitions(-D_LIB)
add_definitions(-DEETYPE_TYPE_MANAGER)
add_definitions(-DFEATURE_EMBEDDED_CONFIG)

if(WIN32)
  # There is a problem with undefined symbol g_pConfig, windows don't care since it is in template method, but clang does 
  add_definitions(-DSTRESS_HEAP)
  add_definitions(-DFEATURE_ETW)
  add_definitions(-DFEATURE_EVENT_TRACE)
  add_compile_options(/GS)
  add_compile_options(/W1)
  add_compile_options(/Zc:wchar_t)
  add_compile_options($<$<CONFIG:Debug>:/Zi>)
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
convert_to_absolute_path(SERVER_GC_SOURCES ${SERVER_GC_SOURCES})

convert_to_absolute_path(RUNTIME_SOURCES_ARCH_ASM ${RUNTIME_SOURCES_ARCH_ASM})

if(NOT CLR_CMAKE_PLATFORM_ARCH_WASM)
  add_subdirectory(Full)
endif()

add_subdirectory(Portable)
