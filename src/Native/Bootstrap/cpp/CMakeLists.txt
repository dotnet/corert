project(bootstrappercpp)

add_definitions(-DCPPCODEGEN)

set(SOURCES
    ../main.cpp
    ../common.cpp
)

add_library(bootstrappercpp STATIC ${SOURCES})

# Install the static bootstrappercpp library
install (TARGETS bootstrappercpp DESTINATION lib)
if(WIN32)
    install (FILES ${CMAKE_CURRENT_BINARY_DIR}/bootstrappercpp.dir/$<CONFIG>/bootstrappercpp.pdb DESTINATION lib)
endif()
