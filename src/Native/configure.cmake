include(CheckFunctionExists)
include(CheckStructHasMember)
include(CheckCXXSourceCompiles)
include(CheckCXXSourceRuns)
include(CheckCXXSymbolExists)

add_compile_options(-I${CMAKE_CURRENT_SOURCE_DIR}/Common)

if (NOT WIN32)
    include_directories(SYSTEM /usr/local/include)
endif ()

if (CMAKE_SYSTEM_NAME STREQUAL Linux)
    set (CMAKE_REQUIRED_LIBRARIES rt)
endif ()

set (CMAKE_REQUIRED_LIBRARIES)

