project(PortableRuntime)

# Portable version of the runtime is designed to be used with CppCodeGen only.
# It should be written in pure C/C++, with no assembly code.

include_directories(..)
include_directories(../../gc)
include_directories(../../gc/env)

add_definitions(-DUSE_PORTABLE_HELPERS)

add_library(PortableRuntime STATIC ${COMMON_RUNTIME_SOURCES} ${PORTABLE_RUNTIME_SOURCES})

# Install the static Runtime library
install (TARGETS PortableRuntime DESTINATION sdk)
if(WIN32)
    install (FILES ${CMAKE_CURRENT_BINARY_DIR}/PortableRuntime.dir/$<CONFIG>/PortableRuntime.pdb DESTINATION sdk)
endif()
