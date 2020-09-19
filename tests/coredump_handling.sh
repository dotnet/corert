#
# This script does nothing on its own. It contains functions related to core
# dump handling and is intended to be sourced from other scripts.
#

function set_up_core_dump_generation {
    # We will only enable dump generation here if we're on Mac or Linux
    if [[ ! ( "$(uname -s)" == "Darwin" || "$(uname -s)" == "Linux" ) ]]; then
        return
    fi

    # We won't enable dump generation on OS X/macOS if the machine hasn't been
    # configured with the kern.corefile pattern we expect.
    if [[ ( "$(uname -s)" == "Darwin" && "$(sysctl -n kern.corefile)" != "core.%P" ) ]]; then
        echo "WARNING: Core dump generation not being enabled due to unexpected kern.corefile value."
        return
    fi

    # Allow dump generation
    ulimit -c unlimited

    if [ "$(uname -s)" == "Linux" ]; then
        if [ -e /proc/self/coredump_filter ]; then
            # Include memory in private and shared file-backed mappings in the dump.
            # This ensures that we can see disassembly from our shared libraries when
            # inspecting the contents of the dump. See 'man core' for details.
            echo -n 0x3F > /proc/self/coredump_filter
        fi
    fi
}

function print_info_from_core_file {
    local core_file_name=$1
    local executable_name=$2

    if ! [ -e $executable_name ]; then
        echo "Unable to find executable $executable_name"
        return
    elif ! [ -e $core_file_name ]; then
        echo "Unable to find core file $core_file_name"
        return
    fi

    # Use LLDB to inspect the core dump on Mac, and GDB everywhere else.
    if [[ "$(uname -s)" == "Darwin" ]]; then
        hash lldb 2>/dev/null || { echo >&2 "LLDB was not found. Unable to print core file."; return; }

        echo "Printing info from core file $core_file_name"
        lldb -c $core_file_name -b -o 'bt'
    else
        # Use GDB to print the backtrace from the core file.
        hash gdb 2>/dev/null || { echo >&2 "GDB was not found. Unable to print core file."; return; }

        echo "Printing info from core file $core_file_name"
        gdb --batch -ex "thread apply all bt full" -ex "quit" $executable_name $core_file_name
    fi
}

function upload_core_file_to_dumpling {
    local core_file_name=$1
    local paths_to_add=$2

    local dumpling_script="dumpling.py"
    local dumpling_file="local_dumplings.txt"

    # dumpling requires that the file exist before appending.
    touch ./$dumpling_file

    if [ ! -x $dumpling_script ]; then
        echo "Dumpling script not found. Dump cannot be uploaded."
        return
    fi

    echo "Uploading $core_file_name to dumpling service."

    # Ensure the script has Unix line endings
    perl -pi -e 's/\r\n|\n|\r/\n/g' "$dumpling_script"

    # The output from this will include a unique ID for this dump.
    ./$dumpling_script "upload" "--dumppath" "$core_file_name" "--incpaths" $paths_to_add "--properties" "Project=CoreRT" "--squelch" | tee -a $dumpling_file
}

function preserve_core_file {
    local core_file_name=$1
    local paths_to_associated_files=$2
    local storage_location="/tmp/coredumps_corert"

    # Create the directory (this shouldn't fail even if it already exists).
    mkdir -p $storage_location

    # Only preserve the dump if the directory is empty. Otherwise, do nothing.
    # This is a way to prevent us from storing/uploading too many dumps.
    if [ ! "$(ls -A $storage_location)" ]; then
        echo "Copying core file $core_file_name to $storage_location"
        cp $core_file_name $storage_location

        upload_core_file_to_dumpling $core_file_name $paths_to_associated_files
    fi
}

function inspect_and_delete_core_files {
    # This function prints some basic information from core files in the current
    # directory and deletes them immediately. Based on the state of the system, it may
    # also upload a core file to the dumpling service.
    # (see preserve_core_file).
    
    # Depending on distro/configuration, the core files may either be named "core"
    # or "core.<PID>" by default. We will read /proc/sys/kernel/core_uses_pid to 
    # determine which one it is.
    # On OS X/macOS, we checked the kern.corefile value before enabling core dump
    # generation, so we know it always includes the PID.
    local core_name_uses_pid=0
    if [[ (( -e /proc/sys/kernel/core_uses_pid ) && ( "1" == $(cat /proc/sys/kernel/core_uses_pid) )) 
          || ( "$(uname -s)" == "Darwin" ) ]]; then
        core_name_uses_pid=1
    fi

    local executable_name=$1
    local associated_files=$2

    if [ $core_name_uses_pid == "1" ]; then
        # We don't know what the PID of the process was, so let's look at all core
        # files whose name matches core.NUMBER
        for f in core.*; do
            if [[ $f =~ core.[0-9]+ ]]; then
                print_info_from_core_file "$f" "$executable_name"
                preserve_core_file "$f" "$associated_files"
                rm "$f"
            fi
        done
    elif [ -f core ]; then
        print_info_from_core_file "core" $CORE_ROOT/"corerun"
        preserve_core_file "core"
        rm "core"
    fi
}
