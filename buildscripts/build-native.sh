#!/usr/bin/env bash

scriptRoot="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"

if [ "$BUILDVARS_DONE" != 1 ]; then
    . $scriptRoot/buildvars-setup.sh $*
fi

# Check the system to ensure the right pre-reqs are in place
check_native_prereqs()
{
    echo "Checking pre-requisites..."

    # resolve cmake version to use, prefer cmake3 if there is one
    if [ "$CMAKE" == "" ] ; then
        if ! CMAKE=$(command -v cmake3 || command -v cmake)
        then
            echo >&2 "Please install cmake before running this script";
            exit 1
        fi
        export CMAKE
    fi

    # Check for clang
    hash clang-$__ClangMajorVersion.$__ClangMinorVersion 2>/dev/null ||  hash clang$__ClangMajorVersion$__ClangMinorVersion 2>/dev/null ||  hash clang 2>/dev/null || { echo >&2 "Please install clang before running this script"; exit 1; }

    # Check for additional prereqs for wasm build
    if [ $__BuildArch == "wasm" ]; then
        hash emcmake 2>/dev/null || { echo >&2 "Please install Emscripten before running this script. See https://github.com/dotnet/corert/blob/master/Documentation/how-to-build-WebAssembly.md for more information."; exit 1; }
        if [ -z ${EMSDK+x} ]; then echo "EMSDK is not set. Ensure your have set up the Emscripten environment using \"source <emsdk_dir>/emsdk_env.sh\""; exit 1; fi
    fi
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

    if [ $__BuildArch == "wasm" ]; then
        emmake make install -j $NumProc $__UnprocessedBuildArgs
    else
        make install -j $NumProc $__UnprocessedBuildArgs
    fi
    EXITCODE=$?
    if [ $EXITCODE != 0 ]; then
        echo "Failed to build corert native components."
        popd
        exit $EXITCODE
    fi

    echo "CoreRT native components successfully built."
    popd
}

initHostDistroRid()
{
    if [ "$__HostOS" == "Linux" ]; then
        if [ ! -e /etc/os-release ]; then
            echo "WARNING: Can not determine runtime id for current distro."
            __HostDistroRid=""
        else
            source /etc/os-release
            __HostDistroRid="$ID.$VERSION_ID-$__HostArch"
        fi
    fi
}

initTargetDistroRid()
{
    if [ $__CrossBuild == 1 ]; then
        if [ "$__BuildOS" == "Linux" ]; then
            if [ ! -e $ROOTFS_DIR/etc/os-release ]; then
                echo "WARNING: Can not determine runtime id for current distro."
                export __DistroRid=""
            else
                source $ROOTFS_DIR/etc/os-release
                export __DistroRid="$ID.$VERSION_ID-$__BuildArch"
            fi
        fi
    else
        export __DistroRid="$__HostDistroRid"
    fi
}

build_host_native_corert()
{
    __SavedBuildArch=$__BuildArch
    __SavedIntermediatesDir=$__IntermediatesDir

    export __IntermediatesDir=$__IntermediatesHostDir
    export __BuildArch=$__HostArch
    export __CMakeBinDir="$__ProductHostBinDir"
    export CROSSCOMPILE=

    build_native_corert

    cp ${__ProductHostBinDir}/tools/jitinterface.so ${__ProductBinDir}

    export __BuildArch=$__SavedBuildArch
    export __IntermediatesDir=$__SavedIntermediatesDir
    export CROSSCOMPILE=1
}

if $__buildnative; then

    # init the host distro name
    initHostDistroRid

    # init the target distro name
    initTargetDistroRid

    # Check prereqs.

    check_native_prereqs

    # Prepare the system

    prepare_native_build

    # Build the corert native components.

    build_native_corert

    if [ $__CrossBuild = 1 ]; then
        build_host_native_corert
    fi

    # Build complete
fi

