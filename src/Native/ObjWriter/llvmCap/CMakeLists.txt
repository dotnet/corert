cmake_minimum_required(VERSION 3.6)
project(ObjWriter)
include(ExternalProject)

set(LLVM_VERSION "5")
set(OBJWRITER_ROOT ${CMAKE_CURRENT_SOURCE_DIR}/../)
set(OBJWRITER_LLVM_POINT tools/ObjWriter)

if(NOT OBJWRITER_TARGET_ARCH)
  if(CLR_CMAKE_TARGET_ARCH MATCHES "^(x64|x86)$")
    if(NOT CROSS_BUILD)
      set(OBJWRITER_TARGET_ARCH "X86")
    else()
      # If we really want a cross version for x86/x64 -> arm32/arm64
      # I think in the opposite direction it's not necessary for anyone ;-)
      set(OBJWRITER_TARGET_ARCH "X86::ARM")
      set(USE_ARM_TARGET_TRIPLE 1)
    endif()
  elseif(CLR_CMAKE_TARGET_ARCH MATCHES "^(arm|armel)$")
    set(OBJWRITER_TARGET_ARCH "ARM")
    set(USE_ARM_TARGET_TRIPLE 1)
  elseif(CLR_CMAKE_TARGET_ARCH STREQUAL "arm64")
    set(OBJWRITER_TARGET_ARCH "AArch64")
  else()
    clr_unknown_arch()
  endif()
endif()

get_target_property(CORERT_NATIVE_COMPILE_OPTIONS Runtime COMPILE_OPTIONS)
get_target_property(CORERT_NATIVE_INCLUDE_DIRECTORIES Runtime INCLUDE_DIRECTORIES)

list(REMOVE_ITEM CORERT_NATIVE_COMPILE_OPTIONS "/Gz")
list(REMOVE_ITEM CORERT_NATIVE_COMPILE_OPTIONS "-Werror")

foreach(dir ${CORERT_NATIVE_INCLUDE_DIRECTORIES})
  set(CORERT_NATIVE_COMPILE_OPTIONS "${CORERT_NATIVE_COMPILE_OPTIONS} -I${dir}")
endforeach()

# For armel RootFS compatibility
if(CLR_CMAKE_TARGET_ARCH STREQUAL "armel")
  list(APPEND CORERT_NATIVE_COMPILE_OPTIONS "-Wno-gnu-include-next")
endif()

if(USE_ARM_TARGET_TRIPLE)
  list(APPEND LLVM_CMAKE_EXTRA_ARGS "-DLLVM_DEFAULT_TARGET_TRIPLE=thumbv7-linux-gnueabi")
endif()

# Make sure to remove debug flags from general build flags for LLVM
set(LLVM_COMPILE_OPTIONS "${CORERT_NATIVE_COMPILE_OPTIONS}")
list(REMOVE_ITEM LLVM_COMPILE_OPTIONS "-g")
list(REMOVE_ITEM LLVM_COMPILE_OPTIONS "-O0")
list(REMOVE_ITEM LLVM_COMPILE_OPTIONS "-Werror")

string(REPLACE ";" "\ " CORERT_NATIVE_COMPILE_OPTIONS "${CORERT_NATIVE_COMPILE_OPTIONS}")
string(REPLACE ";" "\ " LLVM_COMPILE_OPTIONS "${LLVM_COMPILE_OPTIONS}")

# If host and target are the same, we could use llvm-tblgen from LLVM itself.
# Otherwise we use host llvm-tblgen. It's universal way for cross-building.
if(NOT ${CMAKE_HOST_SYSTEM_PROCESSOR} STREQUAL ${CMAKE_SYSTEM_PROCESSOR})
  # Find llvm-tblgen tool
  if(NOT LLVM_TBLGEN_TOOL)
    execute_process (
      COMMAND bash -c "echo -n `which llvm-tblgen`"
      OUTPUT_VARIABLE LLVM_TBLGEN_TOOL
      )
    if(NOT LLVM_TBLGEN_TOOL)
      message(FATAL_ERROR "Can't find llvm-tblgen! You need to make sure that you have installed LLVM version $LLVM_VERSION")
    endif()
  endif()

  # Check compatibility
  execute_process (
    COMMAND bash -c "echo -n `${LLVM_TBLGEN_TOOL} --version | grep -i 'version ${LLVM_VERSION}'`"
    OUTPUT_VARIABLE IS_LLVM_TBLGEN_TOOL_COMPATIBLE
    )
  if(NOT IS_LLVM_TBLGEN_TOOL_COMPATIBLE)
    message(FATAL_ERROR "LLVM version incompatibility! You need to make sure that you have installed LLVM version ${LLVM_VERSION}")
  endif()

  list(APPEND LLVM_CMAKE_EXTRA_ARGS "-DLLVM_TABLEGEN=${LLVM_TBLGEN_TOOL}")
endif()

ExternalProject_Add(LLVM
  GIT_REPOSITORY https://github.com/llvm-mirror/llvm
  GIT_TAG release_${LLVM_VERSION}0
  GIT_SHALLOW 1
  GIT_PROGRESS 1
  PATCH_COMMAND ""
  UPDATE_COMMAND ""
  INSTALL_COMMAND ""
  USES_TERMINAL_DOWNLOAD 1
  USES_TERMINAL_CONFIGURE 1
  USES_TERMINAL_BUILD 1
  USES_TERMINAL_INSTALL 1
  DEPENDS Runtime PortableRuntime
  LIST_SEPARATOR ::
  CMAKE_ARGS -DHAVE_POSIX_SPAWN=0
             -DLLVM_BUILD_DOCS=0
             -DLLVM_BUILD_TESTS=0
             -DLLVM_ENABLE_DOXYGEN=0
             -DLLVM_ENABLE_PEDANTIC=0
             -DLLVM_ENABLE_PIC=1
             -DLLVM_ENABLE_WERROR=0
             -DLLVM_INCLUDE_DOCS=0
             -DLLVM_INCLUDE_EXAMPLES=0
             -DLLVM_INCLUDE_TESTS=0
             -DLLVM_OPTIMIZED_TABLEGEN=1
             -DCMAKE_BUILD_TYPE=Release
             -DLLVM_TARGET_ARCH=${OBJWRITER_TARGET_ARCH}
             -DLLVM_TARGETS_TO_BUILD=${OBJWRITER_TARGET_ARCH}
             -DCMAKE_C_COMPILER=${CMAKE_C_COMPILER}
             -DCMAKE_CXX_COMPILER=${CMAKE_CXX_COMPILER}
             -DCMAKE_C_FLAGS=${LLVM_COMPILE_OPTIONS}
             -DCMAKE_CXX_FLAGS=${LLVM_COMPILE_OPTIONS}
             -DCMAKE_EXE_LINKER_FLAGS=${CMAKE_EXE_LINKER_FLAGS}
             -DCMAKE_MODULE_LINKER_FLAGS=${CMAKE_MODULE_LINKER_FLAGS}
             -DCMAKE_SHARED_LINKER_FLAGS=${CMAKE_SHARED_LINKER_FLAGS}
             -DOBJWRITER_BUILD_TYPE=${CMAKE_BUILD_TYPE}
             -DOBJWRITER_C_FLAGS=${CORERT_NATIVE_COMPILE_OPTIONS}
             -DOBJWRITER_CXX_FLAGS=${CORERT_NATIVE_COMPILE_OPTIONS}
             ${LLVM_CMAKE_EXTRA_ARGS}
  )
ExternalProject_Get_Property(LLVM source_dir)
set(LLVM_SOURCE_DIR ${source_dir})
ExternalProject_Add_Step(LLVM PatchingLLVM
  COMMAND git checkout -- .
  COMMAND git apply ${OBJWRITER_ROOT}/llvm.patch COMMENT "Applying LLVM patch with ObjWriter fixes"
  WORKING_DIRECTORY ${LLVM_SOURCE_DIR}
  DEPENDEES patch
  USES_TERMINAL 1
  )

if(CMAKE_HOST_WIN32)
ExternalProject_Add_Step(LLVM ObjWriterInjection
  COMMAND ${CMAKE_COMMAND} -E copy_directory ${OBJWRITER_ROOT} ${OBJWRITER_LLVM_POINT} COMMENT "copy ${OBJWRITER_ROOT} -> ${OBJWRITER_LLVM_POINT}"
  WORKING_DIRECTORY ${LLVM_SOURCE_DIR}
  DEPENDEES patch
  USES_TERMINAL 1
  )
else()
ExternalProject_Add_Step(LLVM ObjWriterInjection
  COMMAND ${CMAKE_COMMAND} -E create_symlink ${OBJWRITER_ROOT} ${OBJWRITER_LLVM_POINT} COMMENT "mklink ${OBJWRITER_ROOT} -> ${OBJWRITER_LLVM_POINT}"
  WORKING_DIRECTORY ${LLVM_SOURCE_DIR}
  DEPENDEES patch
  USES_TERMINAL 1
  )
endif()

ExternalProject_Add_Step(LLVM forceconfigure
  COMMAND ${CMAKE_COMMAND} -E echo "Force configure of LLVM"
  DEPENDEES update
  DEPENDERS configure
  ALWAYS 1
  )
