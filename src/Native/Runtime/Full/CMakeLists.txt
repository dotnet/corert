project(Runtime)

# Full version of the runtime is required by the JIT CodeGen.
# The low-level helpers can be implemented in assembly code.

include_directories(..)
include_directories(../../gc)
include_directories(../../gc/env)

add_library(Runtime STATIC ${COMMON_RUNTIME_SOURCES} ${SOURCES})

# Install the static Runtime library
install (TARGETS Runtime DESTINATION lib)
