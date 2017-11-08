project(objwriter)

set(CMAKE_CXX_FLAGS "${CMAKE_CXX_FLAGS} -std=c++11 -fno-rtti")
include_directories(${LLVM_INCLUDE_DIRS})
include_directories(.)
add_definitions(${LLVM_DEFINITIONS})

if (WIN32)
  # Create .def file containing a list of exports preceeded by
  # 'EXPORTS'.  The file "objwriter.exports" already contains the list, so we
  # massage it into the correct format here to create "objwriter.exports.def".
  set(OBJWRITER_EXPORTS_DEF ${CMAKE_CURRENT_BINARY_DIR}/objwriter.exports.def)
  set(OBJWRITER_EXPORTS_DEF_TEMP ${OBJWRITER_EXPORTS_DEF}.txt)
  file(READ "objwriter.exports" exports_list)
  file(WRITE ${OBJWRITER_EXPORTS_DEF_TEMP} "LIBRARY OBJWRITER\n")
  file(APPEND ${OBJWRITER_EXPORTS_DEF_TEMP} "EXPORTS\n")
  file(APPEND ${OBJWRITER_EXPORTS_DEF_TEMP} ${exports_list})
  # Copy the file only if it has changed.
  execute_process(COMMAND ${CMAKE_COMMAND} -E copy_if_different
    ${OBJWRITER_EXPORTS_DEF_TEMP} ${OBJWRITER_EXPORTS_DEF})
endif()

# Now build our tools
add_library(objwriter
   SHARED
   objwriter.cpp
   typeBuilder.cpp
   objwriter.h # Visual Studio generator doesn't include necessary header files into the project automatically
   typeBuilder.h
   ${OBJWRITER_EXPORTS_DEF}
)

# Find the libraries that correspond to the LLVM components
# that we wish to use
llvm_map_components_to_libnames(llvm_libs
  ${LLVM_TARGETS_TO_BUILD}
)

# Link against LLVM libraries
target_link_libraries(objwriter
${llvm_libs})
