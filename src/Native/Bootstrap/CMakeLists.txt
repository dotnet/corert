include_directories(../gc)
include_directories(../gc/env)
include_directories(../gc/sample)

if(CLR_CMAKE_PLATFORM_UNIX)
add_compile_options(-Wno-format)
add_compile_options(-Wno-unused-variable)
add_compile_options(-Wno-unused-private-field)
add_compile_options(-Wno-tautological-undefined-compare)
endif()

# We don't need base for WASM.
if(NOT CMAKE_SYSTEM_NAME STREQUAL Emscripten)
    add_subdirectory(base)
endif(NOT CMAKE_SYSTEM_NAME STREQUAL Emscripten)

add_subdirectory(cpp)
