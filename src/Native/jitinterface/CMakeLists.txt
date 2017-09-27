project(jitinterface)

set(NATIVE_SOURCES
    jithost.cpp
    jitinterface.cpp
    jitwrapper.cpp
    corinfoexception.cpp
)

if(WIN32 AND CLR_CMAKE_PLATFORM_ARCH_AMD64)
    set(NATIVE_SOURCES ${NATIVE_SOURCES}
        CodeHeap.cpp
        JITCodeManager.cpp
        ../Runtime/coreclr/GCInfoDecoder.cpp
    )

    add_definitions(-DGCINFODECODER_NO_EE)
    add_definitions(-DFEATURE_REDHAWK)
endif(WIN32 AND CLR_CMAKE_PLATFORM_ARCH_AMD64)

if(CMAKE_SYSTEM_NAME STREQUAL Emscripten)
    add_library(jitinterface
        STATIC
        ${NATIVE_SOURCES}
    )
else()
    add_library(jitinterface
        SHARED
        ${NATIVE_SOURCES}
    )
endif()

install (TARGETS jitinterface DESTINATION tools)
if(WIN32)
    target_link_libraries(jitinterface ntdll.lib) 
    install (FILES ${CMAKE_CURRENT_BINARY_DIR}/$<CONFIG>/jitinterface.pdb DESTINATION tools)
endif(WIN32)
