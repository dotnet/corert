project(jitinterface)

set(NATIVE_SOURCES
    jithost.cpp
    jitinterface.cpp
    jitwrapper.cpp
    corinfoexception.cpp
)

set (JIT_SOURCES
    CodeHeap.cpp
    JITCodeManager.cpp
    ../Runtime/coreclr/GCInfoDecoder.cpp
)

if(WIN32 AND CLR_CMAKE_PLATFORM_ARCH_AMD64)

    add_definitions(-DGCINFODECODER_NO_EE)
    add_definitions(-DFEATURE_REDHAWK)
    add_definitions(-DFEATURE_SINGLE_MODULE_RUNTIME)

    add_library(System.Private.Jit.Native
        STATIC
        ${NATIVE_SOURCES}
        ${JIT_SOURCES}
    )
endif(WIN32 AND CLR_CMAKE_PLATFORM_ARCH_AMD64)

add_library(jitinterface
    SHARED
    ${NATIVE_SOURCES}
)

install (TARGETS jitinterface DESTINATION tools)
if(WIN32)
    target_link_libraries(jitinterface ntdll.lib) 
    install (FILES ${CMAKE_CURRENT_BINARY_DIR}/$<CONFIG>/jitinterface.pdb DESTINATION tools)

    if (CLR_CMAKE_PLATFORM_ARCH_AMD64)
        install (TARGETS System.Private.Jit.Native DESTINATION sdk)
    endif(CLR_CMAKE_PLATFORM_ARCH_AMD64)
endif(WIN32)
