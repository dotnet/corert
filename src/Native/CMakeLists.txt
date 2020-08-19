cmake_minimum_required(VERSION 2.8.12)
project(CoreRT)

# Include cmake functions
include(functions.cmake)

if(CMAKE_SYSTEM_NAME STREQUAL Windows)
    # CMake version 3.8.0 or higher is required to compile targeting VS 2017
    cmake_minimum_required(VERSION 3.8.0)
endif()

if(CMAKE_SYSTEM_NAME STREQUAL Darwin)
    # CMake version 3.4.0 or higher has bug fixes required to compile targeting Darwin
    cmake_minimum_required(VERSION 3.4.0)
endif()

set(CMAKE_MACOSX_RPATH ON)
set(CMAKE_INSTALL_PREFIX $ENV{__CMakeBinDir})
set(CMAKE_INCLUDE_CURRENT_DIR ON)
if(NOT WIN32)
set(CMAKE_C_FLAGS "-std=c11")
set(CMAKE_CXX_FLAGS "-std=c++11")
endif()
set(CMAKE_SHARED_LIBRARY_PREFIX "")

function(clr_unknown_arch)
    message(FATAL_ERROR "Only AMD64, ARM64, ARM, ARMEL, I386 and WASM are supported")
endfunction()

if(CLR_CMAKE_TARGET_ARCH STREQUAL x64)
    set(CLR_CMAKE_PLATFORM_ARCH_AMD64 1)
    add_definitions(-DTARGET_AMD64=1)
    add_definitions(-DHOST_AMD64=1)
    add_definitions(-DHOST_64BIT=1)
elseif(CLR_CMAKE_TARGET_ARCH MATCHES "^(arm|armel)$")
    set(CLR_CMAKE_PLATFORM_ARCH_ARM 1)
    add_definitions(-DTARGET_ARM=1)
    add_definitions(-DHOST_ARM=1)
    add_definitions(-DFEATURE_64BIT_ALIGNMENT=1)
elseif(CLR_CMAKE_TARGET_ARCH STREQUAL arm64)
    set(CLR_CMAKE_PLATFORM_ARCH_ARM64 1)
    add_definitions(-DTARGET_ARM64=1)
    add_definitions(-DHOST_ARM64=1)
    add_definitions(-DHOST_64BIT=1)
elseif(CLR_CMAKE_TARGET_ARCH STREQUAL x86)
    set(CLR_CMAKE_PLATFORM_ARCH_I386 1)
    add_definitions(-DTARGET_X86=1)
    add_definitions(-DHOST_X86=1)
elseif(CLR_CMAKE_TARGET_ARCH STREQUAL wasm)
    set(CLR_CMAKE_PLATFORM_ARCH_WASM 1)
    add_definitions(-DTARGET_WASM=1)
    add_definitions(-DHOST_WASM=1)
    add_definitions(-DFEATURE_64BIT_ALIGNMENT=1)
else()
    clr_unknown_arch()
endif()

if(CMAKE_SYSTEM_NAME STREQUAL Darwin)
    set(CLR_CMAKE_PLATFORM_UNIX 1)
    set(CLR_CMAKE_PLATFORM_DARWIN 1)
    set(CMAKE_ASM_COMPILE_OBJECT "${CMAKE_C_COMPILER} <FLAGS> <DEFINES> <INCLUDES> -o <OBJECT> -c <SOURCE>")
endif(CMAKE_SYSTEM_NAME STREQUAL Darwin)

if(CMAKE_SYSTEM_NAME STREQUAL Linux)
    set(CLR_CMAKE_PLATFORM_UNIX 1)
    set(CLR_CMAKE_PLATFORM_LINUX 1)
endif(CMAKE_SYSTEM_NAME STREQUAL Linux)

if(CMAKE_SYSTEM_NAME STREQUAL FreeBSD)
    set(CLR_CMAKE_PLATFORM_UNIX 1)
    set(CLR_CMAKE_PLATFORM_FREEBSD 1)
endif(CMAKE_SYSTEM_NAME STREQUAL FreeBSD)

if(CMAKE_SYSTEM_NAME STREQUAL NetBSD)
    set(CLR_CMAKE_PLATFORM_UNIX 1)
    set(CLR_CMAKE_PLATFORM_NETBSD 1)
endif(CMAKE_SYSTEM_NAME STREQUAL NetBSD)

if(CMAKE_SYSTEM_NAME STREQUAL Emscripten)
    set(CLR_CMAKE_PLATFORM_UNIX 1)
    set(CLR_CMAKE_PLATFORM_WASM 1)
endif(CMAKE_SYSTEM_NAME STREQUAL Emscripten)

if (CLR_CMAKE_PLATFORM_UNIX)
    include_directories(inc/unix)

    add_definitions(-DHOST_UNIX=1)
    add_definitions(-DTARGET_UNIX=1)
    add_definitions(-DPAL_STDCPP_COMPAT)

    # All warnings that are not explicitly disabled are reported as errors
    add_compile_options(-Wall)
    add_compile_options(-Werror)
    add_compile_options(-Wno-invalid-offsetof)
    add_compile_options(-Wno-null-arithmetic)
    add_compile_options(-Wno-null-conversion)

    # Since 6 version, clang generates pragma-pack warnings, so disable it because we use pshpack[1..8].h/poppack.h
    add_compile_options(-Wno-pragmas)

    if(CLR_CMAKE_PLATFORM_ARCH_AMD64 OR CLR_CMAKE_PLATFORM_ARCH_I386)
        # Allow 16 byte compare-exchange
        add_compile_options(-mcx16)
    endif()

    if(CLR_CMAKE_PLATFORM_ARCH_ARM)
        add_compile_options(-march=armv7-a)
        if(TOOLCHAIN STREQUAL arm-linux-gnueabi)
            add_compile_options(-mfloat-abi=softfp)
        endif()
        add_compile_options(-mthumb)
        add_compile_options(-mfpu=vfpv3)
    endif()
    
    if(CLR_CMAKE_PLATFORM_ARCH_AMD64)
        add_definitions(-DUNIX_AMD64_ABI)
    elseif(CLR_CMAKE_PLATFORM_ARCH_I386)
        add_definitions(-DUNIX_X86_ABI)
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
    elseif(NOT CLR_CMAKE_PLATFORM_WASM)
        add_compile_options(-fstack-protector-strong)
    endif(CLR_CMAKE_PLATFORM_DARWIN)

    if(CLR_CMAKE_PLATFORM_LINUX)
        set(CMAKE_ASM_FLAGS "${CMAKE_ASM_FLAGS} -Wa,--noexecstack")
    endif(CLR_CMAKE_PLATFORM_LINUX)
endif(CLR_CMAKE_PLATFORM_UNIX)

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
    add_compile_options(/source-charset:utf-8) # Force MSVC to compile source as UTF-8.
    add_compile_options(/Zi) # enable debugging information
    set(CMAKE_SHARED_LINKER_FLAGS "${CMAKE_SHARED_LINKER_FLAGS} /DEBUG")

    if (CLR_CMAKE_PLATFORM_ARCH_I386)
      add_compile_options(/Gz)
    endif (CLR_CMAKE_PLATFORM_ARCH_I386)
else(WIN32)
string(TOUPPER ${CMAKE_BUILD_TYPE} UPPERCASE_CMAKE_BUILD_TYPE)
if (UPPERCASE_CMAKE_BUILD_TYPE STREQUAL DEBUG)
    add_compile_options(-g -O0)
    add_definitions(-DDEBUG)
    add_definitions(-D_DEBUG)
elseif (UPPERCASE_CMAKE_BUILD_TYPE STREQUAL RELEASE)
    add_compile_options (-g -O3)
    add_definitions(-DNDEBUG)
else ()
    message(FATAL_ERROR "Unknown build type. Set CMAKE_BUILD_TYPE to DEBUG or RELEASE.")
endif ()
if (CLR_CMAKE_PLATFORM_ARCH_I386)
    add_compile_options(-m32)
    link_libraries(-m32)
endif()
endif (WIN32)

include(configure.cmake)

if(WIN32)
    add_subdirectory(gc)
endif()
add_subdirectory(Runtime)
add_subdirectory(Bootstrap)

if(NOT CLR_CMAKE_PLATFORM_WASM)
    add_subdirectory(jitinterface)
endif(NOT CLR_CMAKE_PLATFORM_WASM)

# We don't need the PAL on Windows.
if(NOT WIN32)
    add_subdirectory(System.Private.CoreLib.Native)
endif(NOT WIN32)

if(NOT CLR_CMAKE_PLATFORM_WASM)
    add_subdirectory(System.Private.TypeLoader.Native)
endif(NOT CLR_CMAKE_PLATFORM_WASM)

if(OBJWRITER_BUILD)
    add_subdirectory(ObjWriter/llvmCap)
endif()
