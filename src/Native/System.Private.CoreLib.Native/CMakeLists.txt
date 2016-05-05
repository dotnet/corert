project(System.Private.CoreLib.Native)

set(NATIVE_SOURCES
    pal_environment.cpp
    pal_memory.cpp
    pal_io.cpp
    pal_cruntime.cpp
    pal_datetime.cpp
    pal_dynamicload.cpp
)

add_library(System.Private.CoreLib.Native
    STATIC
    ${NATIVE_SOURCES}
)

if (CMAKE_SYSTEM_NAME STREQUAL Linux)
    target_link_libraries(System.Private.CoreLib.Native rt)
endif ()

include(configure.cmake)

install (TARGETS System.Private.CoreLib.Native DESTINATION lib)
