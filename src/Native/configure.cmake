include(CheckFunctionExists)
include(CheckStructHasMember)
include(CheckCXXSourceCompiles)
include(CheckCXXSourceRuns)

#CMake does not include /usr/local/include into the include search path
#thus add it manually. This is required on FreeBSD.
include_directories(/usr/local/include)

if (CMAKE_SYSTEM_NAME STREQUAL Linux)
    set (CMAKE_REQUIRED_LIBRARIES rt)
endif ()

set (CMAKE_REQUIRED_LIBRARIES)

