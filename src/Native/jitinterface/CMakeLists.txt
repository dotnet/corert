project(jitinterface)

set(NATIVE_SOURCES
    jithost.cpp
    jitinterface.cpp
    jitwrapper.cpp
    corinfoexception.cpp
)

if(WIN32)
    set(NATIVE_SOURCES ${NATIVE_SOURCES}
        CodeHeap.cpp
        JITCodeManager.cpp
        ../Runtime/coreclr/GCInfoDecoder.cpp
    )

    add_definitions(-DGCINFODECODER_NO_EE)
    add_definitions(-DFEATURE_REDHAWK)
endif(WIN32)

add_library(jitinterface
    SHARED
    ${NATIVE_SOURCES}
)

install (TARGETS jitinterface DESTINATION .)
if(WIN32)
    target_link_libraries(jitinterface ntdll.lib) 
    install (FILES ${CMAKE_CURRENT_BINARY_DIR}/$<CONFIG>/jitinterface.pdb DESTINATION .)
endif(WIN32)
