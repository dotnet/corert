if(WIN32)
  set(ASM_SUFFIX asm)

  if(CLR_CMAKE_PLATFORM_ARCH_AMD64)
    set(ARCH_SOURCES_DIR amd64)
  elseif(CLR_CMAKE_PLATFORM_ARCH_I386)
    set(ARCH_SOURCES_DIR i386)
  endif()

else()

  set(ASM_SUFFIX S)

  if(CLR_CMAKE_PLATFORM_ARCH_AMD64)
    set(ARCH_SOURCES_DIR amd64)
  elseif(CLR_CMAKE_PLATFORM_ARCH_ARM64)
    set(ARCH_SOURCES_DIR arm64)
  elseif(CLR_CMAKE_PLATFORM_ARCH_ARM)
    set(ARCH_SOURCES_DIR arm)
  elseif(CLR_CMAKE_PLATFORM_ARCH_I386)
    set(ARCH_SOURCES_DIR i386)
  elseif(CLR_CMAKE_PLATFORM_ARCH_WASM)
    set(ARCH_SOURCES_DIR wasm)
  endif()

endif()

set(RUNTIME_SOURCES_ARCH_ASM
  ${ARCH_SOURCES_DIR}/ConstrainedCallSupportHelpers.${ASM_SUFFIX}
  ${ARCH_SOURCES_DIR}/MethodEntrypointStubs.${ASM_SUFFIX}
  ${ARCH_SOURCES_DIR}/VTableResolver.${ASM_SUFFIX}
)

convert_to_absolute_path(RUNTIME_SOURCES_ARCH_ASM ${RUNTIME_SOURCES_ARCH_ASM})

add_library(System.Private.TypeLoader.Native
    STATIC
    ${RUNTIME_SOURCES_ARCH_ASM}
)

if (CMAKE_SYSTEM_NAME STREQUAL Linux)
    target_link_libraries(System.Private.TypeLoader.Native rt)
endif ()

install (TARGETS System.Private.TypeLoader.Native DESTINATION sdk)
