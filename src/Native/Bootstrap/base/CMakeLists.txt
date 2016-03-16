project(bootstrapper)

set(SOURCES
    ../main.cpp
    ../common.cpp
)

if(CLR_CMAKE_PLATFORM_UNIX)
    list(APPEND SOURCES
        ../platform.unix.cpp)
endif()

add_library(bootstrapper STATIC ${SOURCES})

# Install the static bootstrapper library
install (TARGETS bootstrapper DESTINATION lib)
if(WIN32)
    install (FILES ${CMAKE_CURRENT_BINARY_DIR}/bootstrapper.dir/$<CONFIG>/bootstrapper.pdb DESTINATION lib)
endif()
