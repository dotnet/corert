project(jitinterface)

set(NATIVE_SOURCES
    jithost.cpp
    jitinterface.cpp
    jitwrapper.cpp
    corinfoexception.cpp
)

add_library(jitinterface
    SHARED
    ${NATIVE_SOURCES}
)

install (TARGETS jitinterface DESTINATION .)
if(WIN32)
    install (FILES ${CMAKE_CURRENT_BINARY_DIR}/$<CONFIG>/jitinterface.pdb DESTINATION .)
endif(WIN32)
