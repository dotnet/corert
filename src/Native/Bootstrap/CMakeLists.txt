project(reproNative)

include_directories(../gc)
include_directories(../gc/env)
include_directories(../gc/sample)

set(SOURCES
    main.cpp
    common.cpp
)

if(WIN32)
    list(APPEND SOURCES
        platform.windows.cpp)
endif()

if(CLR_CMAKE_PLATFORM_UNIX)
add_compile_options(-Wno-format)
add_compile_options(-Wno-unused-variable)
add_compile_options(-Wno-unused-private-field)
add_compile_options(-Wno-tautological-undefined-compare)
endif()

if(CLR_CMAKE_PLATFORM_ARCH_AMD64)
  add_definitions(-D_TARGET_AMD64_=1)
  add_definitions(-D_WIN64=1)
elseif(CLR_CMAKE_PLATFORM_ARCH_I386)
  add_definitions(-D_TARGET_X86_=1)
  add_definitions(-D_WIN32=1)
elseif(CLR_CMAKE_PLATFORM_ARCH_ARM)
  add_definitions(-D_TARGET_ARM_=1)
  add_definitions(-D_WIN32=1)
elseif(CLR_CMAKE_PLATFORM_ARCH_ARM64)
  add_definitions(-D_TARGET_ARM64_=1)
  add_definitions(-D_WIN64=1)
else()
  clr_unknown_arch()
endif()

add_library(reproNative STATIC ${SOURCES})

# Install the static reproNative library
install (TARGETS reproNative DESTINATION lib)
