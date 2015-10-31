project(Runtime)

# Full version of the runtime is required by the JIT CodeGen.
# The low-level helpers can be implemented in assembly code.

include_directories(..)
include_directories(../../gc)
include_directories(../../gc/env)

# TODO Remove this definition for full runtime
add_definitions(-DUSE_PORTABLE_HELPERS)

add_library(Runtime STATIC ${COMMON_RUNTIME_SOURCES} ${FULL_RUNTIME_SOURCES} ${RUNTIME_SOURCES_ARCH_ASM})

if (WIN32)
    # Get the current list of definitions
    get_compile_definitions(DEFINITIONS)

    # Extract the definitions for the ASM code. Since there is a bug in the cmake that prevents us from
    # using the generator expressions, we split the definitions into lists based on the configuration.
    foreach(DEFINITION IN LISTS COMPILE_DEFINITIONS_LIST)
        if (${DEFINITION} MATCHES "^\\$<\\$<CONFIG:([^>]+)>:([^>]+)>$")
            # The entry contains generator expression, so insert the definition into a definitions list
            # corresponding to the config
           string(TOUPPER ${CMAKE_MATCH_1} CONFIG)
           set(ASM_DEFINITIONS_${CONFIG} ${ASM_DEFINITIONS_${CONFIG}} ${CMAKE_MATCH_2})
        else()
            list(APPEND ASM_DEFINITIONS ${DEFINITION})
        endif()
    endforeach()

    # Add defines for the ASM. Unfortunately setting it on the target is ignored for asm by the cmake, so we have
    # to set it on the sources.
    set_property(SOURCE ${RUNTIME_SOURCES_ARCH_ASM} PROPERTY COMPILE_DEFINITIONS ${ASM_DEFINITIONS})
    foreach(CONFIG IN LISTS CMAKE_CONFIGURATION_TYPES)
        string(TOUPPER ${CONFIG} CONFIG)
        set_property(SOURCE ${RUNTIME_SOURCES_ARCH_ASM} PROPERTY COMPILE_DEFINITIONS_${CONFIG} ${ASM_DEFINITIONS_${CONFIG}})
    endforeach()

    add_custom_command(
        # The AsmOffsets.inc will be built in the pre-build phase of the Runtime build
        TARGET Runtime PRE_BUILD
        COMMAND ${CMAKE_CXX_COMPILER} ${DEFINITIONS} /EP "${RUNTIME_DIR}/AsmOffsets.cpp" >"${CMAKE_CURRENT_BINARY_DIR}/AsmOffsets.inc"
    )
endif()

# Install the static Runtime library
install (TARGETS Runtime DESTINATION lib)
