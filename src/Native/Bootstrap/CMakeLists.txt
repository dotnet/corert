include_directories(../gc)
include_directories(../gc/env)
include_directories(../gc/sample)

if(CLR_CMAKE_PLATFORM_UNIX)
add_compile_options(-Wno-format)
add_compile_options(-Wno-unused-variable)
add_compile_options(-Wno-unused-private-field)
add_compile_options(-Wno-tautological-undefined-compare)
endif()

add_subdirectory(base)
add_subdirectory(cpp)
