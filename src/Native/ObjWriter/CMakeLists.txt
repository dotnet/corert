project(objwriter)

set(CMAKE_BUILD_TYPE "${OBJWRITER_BUILD_TYPE}")
set(CMAKE_C_FLAGS "${CMAKE_C_FLAGS} ${OBJWRITER_C_FLAGS}")
set(CMAKE_CXX_FLAGS "${CMAKE_CXX_FLAGS} ${OBJWRITER_CXX_FLAGS}")

if(WIN32)
set(CMAKE_CXX_FLAGS "${CMAKE_CXX_FLAGS} /EHsc")
endif()

if(NOT WIN32)
set(CMAKE_CXX_FLAGS "${CMAKE_CXX_FLAGS} -std=c++11 -fno-rtti")
endif()

message(STATUS "ObjWriter configuring with (${CMAKE_BUILD_TYPE}) build type and (${LLVM_DEFAULT_TARGET_TRIPLE}) default target triple")

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
   debugInfo/codeView/codeViewTypeBuilder.cpp
   debugInfo/codeView/codeViewTypeBuilder.h
   debugInfo/dwarf/dwarfTypeBuilder.cpp
   debugInfo/dwarf/dwarfTypeBuilder.h
   debugInfo/dwarf/dwarfGen.cpp
   debugInfo/dwarf/dwarfGen.h
   debugInfo/dwarf/dwarfAbbrev.cpp
   debugInfo/dwarf/dwarfAbbrev.h
   debugInfo/typeBuilder.h
   objwriter.h # Visual Studio generator doesn't include necessary header files into the project automatically
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
