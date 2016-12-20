function(_add_executable)
    add_executable(${ARGV})
endfunction()  

function(_add_library)
    add_library(${ARGV})
endfunction()

function(string_has_prefix full_string desired_prefix output_variable)
    string(LENGTH "${full_string}" _full_length)
    string(LENGTH "${desired_prefix}" _piece_length)
    if(_full_length LESS _piece_length)
        set(${output_variable} FALSE PARENT_SCOPE)
        return()
    endif()

    string(SUBSTRING "${full_string}" 0 ${_piece_length} _piece)
    if(_piece STREQUAL desired_prefix)
         set(${output_variable} TRUE PARENT_SCOPE)
    else()
         set(${output_variable} FALSE PARENT_SCOPE)
    endif()
endfunction()
