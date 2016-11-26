cmake_minimum_required(VERSION 2.8.12)
project(CoreRT)

# Include cmake functions
include(functions.cmake)

set(CMAKE_MACOSX_RPATH ON)
set(CMAKE_INSTALL_PREFIX $ENV{__CMakeBinDir})
set(CMAKE_INCLUDE_CURRENT_DIR ON)
if(NOT WIN32)
set(CMAKE_C_FLAGS "-std=c11")
set(CMAKE_CXX_FLAGS "-std=c++11")
endif()
set(CMAKE_SHARED_LIBRARY_PREFIX "")

function(clr_unknown_arch)
    if (WIN32)
        message(FATAL_ERROR "Only AMD64 and I386 are supported")
    else()
        message(FATAL_ERROR "Only AMD64, ARM64 and ARM are supported")
    endif()
endfunction()

if (CMAKE_SYSTEM_PROCESSOR STREQUAL x86_64 OR CMAKE_SYSTEM_PROCESSOR STREQUAL amd64)
    set(IS_64BIT_BUILD 1)
elseif (CMAKE_SYSTEM_PROCESSOR STREQUAL armv7l)
    add_definitions(-DBIT32=1)
    # Because we don't use CMAKE_C_COMPILER/CMAKE_CXX_COMPILER to use clang
    # we have to set the triple by adding a compiler argument
    add_compile_options(-target armv7-linux-gnueabihf)
    add_compile_options(-mthumb)
    add_compile_options(-mfpu=vfpv3)
elseif (CMAKE_SYSTEM_PROCESSOR STREQUAL aarch64)
    set(IS_64BIT_BUILD 1)
    # Because we don't use CMAKE_C_COMPILER/CMAKE_CXX_COMPILER to use clang
    # we have to set the triple by adding a compiler argument
    add_compile_options(-target aarch64-linux-gnu)
endif ()

if(CMAKE_SYSTEM_NAME STREQUAL Darwin)
  set(CLR_CMAKE_PLATFORM_UNIX 1)
  set(CLR_CMAKE_PLATFORM_UNIX_TARGET_AMD64 1)
  set(CLR_CMAKE_PLATFORM_DARWIN 1)
  if(CMAKE_VERSION VERSION_LESS "3.4.0")
    set(CMAKE_ASM_COMPILE_OBJECT "${CMAKE_C_COMPILER} <FLAGS> <DEFINES> -o <OBJECT> -c <SOURCE>")
  else()
    set(CMAKE_ASM_COMPILE_OBJECT "${CMAKE_C_COMPILER} <FLAGS> <DEFINES> <INCLUDES> -o <OBJECT> -c <SOURCE>")
  endif(CMAKE_VERSION VERSION_LESS "3.4.0")
endif(CMAKE_SYSTEM_NAME STREQUAL Darwin)

if(CMAKE_SYSTEM_NAME STREQUAL Linux)
    set(CLR_CMAKE_PLATFORM_UNIX 1)
    if(CMAKE_SYSTEM_PROCESSOR STREQUAL x86_64)
        set(CLR_CMAKE_PLATFORM_UNIX_TARGET_AMD64 1)
    elseif(CMAKE_SYSTEM_PROCESSOR STREQUAL armv7l)
        set(CLR_CMAKE_PLATFORM_UNIX_TARGET_ARM 1)
    elseif(CMAKE_SYSTEM_PROCESSOR STREQUAL aarch64)
        set(CLR_CMAKE_PLATFORM_UNIX_TARGET_ARM64 1)
    else()
        clr_unknown_arch()
    endif()
    set(CLR_CMAKE_PLATFORM_LINUX 1)
endif(CMAKE_SYSTEM_NAME STREQUAL Linux)

if(CMAKE_SYSTEM_NAME STREQUAL NetBSD)
    set(CLR_CMAKE_PLATFORM_UNIX 1)
    if(CMAKE_SYSTEM_PROCESSOR STREQUAL x86_64)
        set(CLR_CMAKE_PLATFORM_UNIX_TARGET_AMD64 1)
    elseif(CMAKE_SYSTEM_PROCESSOR STREQUAL armv7l)
        set(CLR_CMAKE_PLATFORM_UNIX_TARGET_ARM 1)
    elseif(CMAKE_SYSTEM_PROCESSOR STREQUAL aarch64)
        set(CLR_CMAKE_PLATFORM_UNIX_TARGET_ARM64 1)
    else()
        clr_unknown_arch()
    endif()
    set(CLR_CMAKE_PLATFORM_NETBSD 1)
endif(CMAKE_SYSTEM_NAME STREQUAL NetBSD)

if (CLR_CMAKE_PLATFORM_UNIX)
    include_directories(inc/unix)

    add_definitions(-DPLATFORM_UNIX=1)
    add_definitions(-DPAL_STDCPP_COMPAT)

    # All warnings that are not explicitly disabled are reported as errors
    add_compile_options(-Wall)
    add_compile_options(-Werror)
    add_compile_options(-Wno-invalid-offsetof)
    add_compile_options(-Wno-null-arithmetic)
    add_compile_options(-Wno-null-conversion)

    if (CLR_CMAKE_PLATFORM_UNIX_TARGET_AMD64)
        # Allow 16 byte compare-exchange
        add_compile_options(-mcx16)
        add_definitions(-DUNIX_AMD64_ABI)
    endif()

    # Disable strict warning on unused functions.
    add_compile_options(-Wno-unused-function)

    # The -fms-extensions enable the stuff like __if_exists, __declspec(uuid()), etc.
    add_compile_options(-fms-extensions)

    add_compile_options(-fPIC)
    add_compile_options(-fvisibility=hidden)

    if(CLR_CMAKE_PLATFORM_DARWIN)
        # We cannot enable "stack-protector-strong" on OS X due to a bug in clang compiler (current version 7.0.2)
        add_compile_options(-fstack-protector)
    else()
        add_compile_options(-fstack-protector-strong)
    endif(CLR_CMAKE_PLATFORM_DARWIN)

    if(CLR_CMAKE_PLATFORM_LINUX)
        set(CMAKE_ASM_FLAGS "${CMAKE_ASM_FLAGS} -Wa,--noexecstack")  
    endif(CLR_CMAKE_PLATFORM_LINUX)
endif(CLR_CMAKE_PLATFORM_UNIX)

if(CLR_CMAKE_PLATFORM_UNIX_TARGET_ARM)
    set(CLR_CMAKE_PLATFORM_ARCH_ARM 1)
elseif(CLR_CMAKE_PLATFORM_UNIX_TARGET_ARM64)
    set(CLR_CMAKE_PLATFORM_ARCH_ARM64 1)
elseif(CLR_CMAKE_PLATFORM_UNIX_TARGET_AMD64)
    set(CLR_CMAKE_PLATFORM_ARCH_AMD64 1)
elseif(WIN32)
    if (CLR_CMAKE_TARGET_ARCH STREQUAL x64)
        set(CLR_CMAKE_PLATFORM_ARCH_AMD64 1)
        set(IS_64BIT_BUILD 1)
    elseif(CLR_CMAKE_TARGET_ARCH STREQUAL x86)
        set(CLR_CMAKE_PLATFORM_ARCH_I386 1)
        set(IS_64BIT_BUILD 0)
    else()
        clr_unknown_arch()
    endif()
endif()

if(IS_64BIT_BUILD)
    add_definitions(-DBIT64=1)
endif(IS_64BIT_BUILD)

if(WIN32)
    enable_language(ASM_MASM)
else()
    enable_language(ASM)
endif(WIN32)

# Build a list of compiler definitions by putting -D in front of each define.
function(get_compile_definitions DefinitionName)
    # Get the current list of definitions
    get_directory_property(COMPILE_DEFINITIONS_LIST COMPILE_DEFINITIONS)

    foreach(DEFINITION IN LISTS COMPILE_DEFINITIONS_LIST)
        if (${DEFINITION} MATCHES "^\\$<\\$<CONFIG:([^>]+)>:([^>]+)>$")
            # The entries that contain generator expressions must have the -D inside of the 
            # expression. So we transform e.g. $<$<CONFIG:Debug>:_DEBUG> to $<$<CONFIG:Debug>:-D_DEBUG>
            set(DEFINITION "$<$<CONFIG:${CMAKE_MATCH_1}>:-D${CMAKE_MATCH_2}>")
        else()
            set(DEFINITION -D${DEFINITION})
        endif()
        list(APPEND DEFINITIONS ${DEFINITION})
    endforeach()
    set(${DefinitionName} ${DEFINITIONS} PARENT_SCOPE)
endfunction(get_compile_definitions)

if(CLR_CMAKE_PLATFORM_ARCH_AMD64)
  add_definitions(-D_TARGET_AMD64_=1)
  add_definitions(-D_AMD64_)
elseif(CLR_CMAKE_PLATFORM_ARCH_I386)
  add_definitions(-D_TARGET_X86_=1)
  add_definitions(-D_X86_)
elseif(CLR_CMAKE_PLATFORM_ARCH_ARM)
  add_definitions(-D_TARGET_ARM_=1)
  add_definitions(-D_ARM_)
elseif(CLR_CMAKE_PLATFORM_ARCH_ARM64)
  add_definitions(-D_TARGET_ARM64_=1)
  add_definitions(-D_ARM64_)
else()
  clr_unknown_arch()
endif()

# Set the passed in RetSources variable to the list of sources with added current source directory
# to form absolute paths. 
# The parameters after the RetSources are the input files. 
function(convert_to_absolute_path RetSources)
    set(Sources ${ARGN})
    foreach(Source IN LISTS Sources)
        list(APPEND AbsolutePathSources ${CMAKE_CURRENT_SOURCE_DIR}/${Source})
    endforeach()
    set(${RetSources} ${AbsolutePathSources} PARENT_SCOPE)
endfunction(convert_to_absolute_path)

if(WIN32)
    add_definitions(-DUNICODE=1)
    add_compile_options($<$<CONFIG:Debug>:-DDEBUG>)
    add_compile_options($<$<CONFIG:Debug>:/MTd>)
    add_compile_options($<$<CONFIG:Release>:/MT>)
    add_compile_options(/Zi) # enable debugging information
    set(CMAKE_SHARED_LINKER_FLAGS "${CMAKE_SHARED_LINKER_FLAGS} /DEBUG")
else(WIN32)
string(TOUPPER ${CMAKE_BUILD_TYPE} UPPERCASE_CMAKE_BUILD_TYPE)
if (UPPERCASE_CMAKE_BUILD_TYPE STREQUAL DEBUG)
    add_compile_options(-g -O0)
    add_definitions(-DDEBUG)
    add_definitions(-D_DEBUG)
elseif (UPPERCASE_CMAKE_BUILD_TYPE STREQUAL RELEASE)
    add_compile_options (-O3)
    add_definitions(-DNDEBUG)
else ()
    message(FATAL_ERROR "Unknown build type. Set CMAKE_BUILD_TYPE to DEBUG or RELEASE.")
endif ()
endif (WIN32)

include(configure.cmake)

if(WIN32)
  add_subdirectory(gc)
endif()
add_subdirectory(Runtime)
add_subdirectory(Bootstrap)
add_subdirectory(jitinterface)

# We don't need the PAL on Windows.
if(CLR_CMAKE_PLATFORM_UNIX)
  add_subdirectory(System.Private.CoreLib.Native)
endif()
