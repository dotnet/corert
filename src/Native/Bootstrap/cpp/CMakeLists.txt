project(bootstrappercpp)

add_definitions(-DCPPCODEGEN)

set(SOURCES
    ../main.cpp
    ../common.cpp
)

add_library(bootstrappercpp STATIC ${SOURCES})

# Install the static bootstrappercpp library
install (TARGETS bootstrappercpp DESTINATION sdk)
if(WIN32)
    install (FILES ${CMAKE_CURRENT_BINARY_DIR}/bootstrappercpp.dir/$<CONFIG>/bootstrappercpp.pdb DESTINATION sdk)
endif()

# Install the CppCodeGen support headers
install (FILES ../common.h DESTINATION inc)
install (FILES ../CppCodeGen.h DESTINATION inc)
