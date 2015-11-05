project(bootstrapper)

set(SOURCES
    ../main.cpp
    ../common.cpp
)

if(WIN32)
    list(APPEND SOURCES
        ../platform.windows.cpp)
endif()

add_library(bootstrapper STATIC ${SOURCES})

# Install the static bootstrapper library
install (TARGETS bootstrapper DESTINATION lib)
