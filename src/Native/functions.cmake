function(_add_executable)
    add_executable(${ARGV})
endfunction()  

function(_add_library)
    add_library(${ARGV})
endfunction()

function(string_has_prefix full_string desired_prefix output_variable)
    string(LENGTH "${desired_prefix}" _length)
    string(SUBSTRING "${full_string}" 0 ${_length} _piece)

    if(_piece STREQUAL desired_prefix)
         set(${output_variable} TRUE PARENT_SCOPE)
    else()
         set(${output_variable} FALSE PARENT_SCOPE)
    endif()
endfunction()
