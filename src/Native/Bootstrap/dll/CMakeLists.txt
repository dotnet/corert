project(bootstrapperdll)

add_definitions(-DCORERT_DLL)

set(SOURCES
    ../main.cpp
    ../common.cpp
)

add_library(bootstrapperdll STATIC ${SOURCES})

# Install the static bootstrapperdll library
install (TARGETS bootstrapperdll DESTINATION sdk)
if(WIN32)
    install (FILES ${CMAKE_CURRENT_BINARY_DIR}/bootstrapperdll.dir/$<CONFIG>/bootstrapperdll.pdb DESTINATION sdk)
endif()
