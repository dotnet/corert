#!/usr/bin/env bash

scriptRoot="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"

if [ "$BUILDVARS_DONE" != 1 ]; then
    . $scriptRoot/buildvars-setup.sh $*
fi

# Check the system to ensure the right pre-reqs are in place
check_native_prereqs()
{
    echo "Checking pre-requisites..."

    # Check presence of CMake on the path
    hash cmake 2>/dev/null || { echo >&2 "Please install cmake before running this script"; exit 1; }

    # Check for clang
    hash clang-$__ClangMajorVersion.$__ClangMinorVersion 2>/dev/null ||  hash clang$__ClangMajorVersion$__ClangMinorVersion 2>/dev/null ||  hash clang 2>/dev/null || { echo >&2 "Please install clang before running this script"; exit 1; }
}

prepare_native_build()
{
    # Specify path to be set for CMAKE_INSTALL_PREFIX.
    # This is where all built CoreClr libraries will copied to.
    export __CMakeBinDir="$__ProductBinDir"

    # Configure environment if we are doing a verbose build
    if [ $__VerboseBuild == 1 ]; then
        export VERBOSE=1
    fi
}


build_native_corert()
{
    # All set to commence the build

    echo "Commencing build of corert native components for $__BuildOS.$__BuildArch.$__BuildType"
    pushd "$__IntermediatesDir"

    # Regenerate the CMake solution
    echo "Invoking cmake with arguments: \"$__ProjectRoot\" $__BuildType"
    "$__ProjectRoot/src/Native/gen-buildsys-clang.sh" "$__ProjectRoot" $__ClangMajorVersion $__ClangMinorVersion $__BuildArch $__BuildType

    # Check that the makefiles were created.

    if [ ! -f "$__IntermediatesDir/Makefile" ]; then
        echo "Failed to generate native component build project!"
        popd
        exit 1
    fi

    # Get the number of processors available to the scheduler
    # Other techniques such as `nproc` only get the number of
    # processors available to a single process.
    if [ `uname` = "FreeBSD" ]; then
        NumProc=`sysctl hw.ncpu | awk '{ print $2+1 }'`
    elif [ `uname` = "NetBSD" ]; then
        NumProc=$(($(getconf NPROCESSORS_ONLN)+1))
    else
        NumProc=$(($(getconf _NPROCESSORS_ONLN)+1))
    fi

    # Build

    echo "Executing make install -j $NumProc $__UnprocessedBuildArgs"

    make install -j $NumProc $__UnprocessedBuildArgs
    if [ $? != 0 ]; then
        echo "Failed to build corert native components."
        popd
        exit $?
    fi

    echo "CoreRT native components successfully built."
    popd
}


if $__buildnative; then

    # Check prereqs.

    check_native_prereqs

    # Prepare the system

    prepare_native_build

    # Build the corert native components.

    build_native_corert

    # Build complete
fi

