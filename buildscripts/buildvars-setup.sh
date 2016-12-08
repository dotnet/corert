#!/usr/bin/env bash

usage()
{
    echo "Usage: $0 [managed] [native] [BuildArch] [BuildType] [clean] [cross] [verbose] [clangx.y]"
    echo "managed - optional argument to build the managed code"
    echo "native - optional argument to build the native code"
    echo "The following arguments affect native builds only:"
    echo "BuildArch can be: x64, x86, arm, arm64"
    echo "BuildType can be: Debug, Release"
    echo "clean - optional argument to force a clean build."
    echo "verbose - optional argument to enable verbose build output."
    echo "clangx.y - optional argument to build using clang version x.y."
    echo "cross - optional argument to signify cross compilation,"
    echo "      - will use ROOTFS_DIR environment variable if set."

    exit 1
}

setup_dirs()
{
    echo Setting up directories for build

    mkdir -p "$__ProductBinDir"
    mkdir -p "$__IntermediatesDir"
}

# Performs "clean build" type actions (deleting and remaking directories)

clean()
{
    echo "Cleaning previous output for the selected configuration"
    rm -rf "$__ProductBinDir"
    rm -rf "$__IntermediatesDir"
}


# Check the system to ensure the right pre-reqs are in place

check_native_prereqs()
{
    echo "Checking pre-requisites..."

    # Check presence of CMake on the path
    hash cmake 2>/dev/null || { echo >&2 "Please install cmake before running this script"; exit 1; }

    # Check for clang
    hash clang-$__ClangMajorVersion.$__ClangMinorVersion 2>/dev/null ||  hash clang$__ClangMajorVersion$__ClangMinorVersion 2>/dev/null ||  hash clang 2>/dev/null || { echo >&2 "Please install clang before running this script"; exit 1; }
}


get_current_linux_distro() {
    # Detect Distro
    if [ "$(cat /etc/*-release | grep -cim1 ubuntu)" -eq 1 ]; then
        if [ "$(cat /etc/*-release | grep -cim1 16.04)" -eq 1 ]; then
            echo "ubuntu.16.04"
            return 0
        fi

        echo "ubuntu.14.04"
        return 0
    fi

    # Cannot determine Linux distribution, assuming Ubuntu 14.04.
    echo "ubuntu.14.04"
    return 0
}


export __scriptpath="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
export __ProjectRoot=$__scriptpath/..
export __packageroot=$__ProjectRoot/packages
export __sourceroot=$__ProjectRoot/src
export __rootbinpath="$__ProjectRoot/bin"
export __buildmanaged=false
export __buildnative=false
export __dotnetclipath=$__ProjectRoot/Tools/dotnetcli

# Use uname to determine what the CPU is.
export CPUName=$(uname -p)
# Some Linux platforms report unknown for platform, but the arch for machine.
if [ $CPUName == "unknown" ]; then
    export CPUName=$(uname -m)
fi

case $CPUName in
    i686)
        export __BuildArch=x86
        ;;

    x86_64)
        export __BuildArch=x64
        ;;

    armv7l)
        echo "Unsupported CPU $CPUName detected, build might not succeed!"
        export __BuildArch=arm
        ;;

    aarch64)
        echo "Unsupported CPU $CPUName detected, build might not succeed!"
        export __BuildArch=arm64
        ;;

    *)
        echo "Unknown CPU $CPUName detected, configuring as if for x64"
        export __BuildArch=x64
        ;;
esac

# Use uname to determine what the OS is.
export OSName=$(uname -s)
case $OSName in
    Darwin)
        export __BuildOS=OSX
        export __NugetRuntimeId=osx.10.10-x64
        ulimit -n 2048
        ;;

    FreeBSD)
        export __BuildOS=FreeBSD
        # TODO: Add proper FreeBSD target
        export __NugetRuntimeId=ubuntu.14.04-x64
        ;;

    Linux)
        export __BuildOS=Linux
        export __NugetRuntimeId=$(get_current_linux_distro)-x64
        ;;

    NetBSD)
        export __BuildOS=NetBSD
        # TODO: Add proper NetBSD target
        export __NugetRuntimeId=ubuntu.14.04-x64
        ;;

    *)
        echo "Unsupported OS $OSName detected, configuring as if for Linux"
        export __BuildOS=Linux
        export __NugetRuntimeId=ubuntu.14.04-x64
        ;;
esac
export __BuildType=Debug

export BUILDERRORLEVEL=0

# Set the various build properties here so that CMake and MSBuild can pick them up
export __UnprocessedBuildArgs=
export __CleanBuild=0
export __VerboseBuild=0
export __ClangMajorVersion=3
export __ClangMinorVersion=5
export __CrossBuild=0


while [ "$1" != "" ]; do
        lowerI="$(echo $1 | awk '{print tolower($0)}')"
        case $lowerI in
        -h|--help)
            usage
            exit 1
            ;;
        managed)
            export __buildmanaged=true
            ;;
        native)
            export __buildnative=true
            ;;
        x86)
            export __BuildArch=x86
            ;;
        x64)
            export __BuildArch=x64
            ;;
        arm)
            export __BuildArch=arm
            ;;
        arm64)
            export __BuildArch=arm64
            ;;
        debug)
            export __BuildType=Debug
            ;;
        release)
            export __BuildType=Release
            ;;
        clean)
            export __CleanBuild=1
            ;;
        verbose)
            export __VerboseBuild=1
            ;;
        clang3.5)
            export __ClangMajorVersion=3
            export __ClangMinorVersion=5
            ;;
        clang3.6)
            export __ClangMajorVersion=3
            export __ClangMinorVersion=6
            ;;
        clang3.7)
            export __ClangMajorVersion=3
            export __ClangMinorVersion=7
            ;;
        cross)
            export __CrossBuild=1
            ;;
        -dotnetclipath)
            shift
            export __dotnetclipath=$1
            ;;
        -officialbuildid)
            shift
            export __ExtraMsBuildArgs="$__ExtraMsBuildArgs /p:OfficialBuildId=$1"
            ;;
        *)
          export __UnprocessedBuildArgs="$__UnprocessedBuildArgs $1"
    esac
    shift
done


# If neither managed nor native are passed as arguments, default to building both

if [ "$__buildmanaged" = false -a "$__buildnative" = false ]; then
    export __buildmanaged=true
    export __buildnative=true
fi

# Set the remaining variables based upon the determined build configuration
export __IntermediatesDir="$__rootbinpath/obj/Native/$__BuildOS.$__BuildArch.$__BuildType"
export __ProductBinDir="$__rootbinpath/Product/$__BuildOS.$__BuildArch.$__BuildType"
export __RelativeProductBinDir="bin/Product/$__BuildOS.$__BuildArch.$__BuildType"

# CI_SPECIFIC - On CI machines, $HOME may not be set. In such a case, create a subfolder and set the variable to set.
# This is needed by CLI to function.
if [ -z "$HOME" ]; then
    if [ ! -d "$__ProjectRoot/temp_home" ]; then
        mkdir "$__ProjectRoot/temp_home"
    fi
    export HOME=$__ProjectRoot/temp_home
    echo "HOME not defined; setting it to $HOME"
fi


# Configure environment if we are doing a clean build.
if [ $__CleanBuild == 1 ]; then
    clean
fi


# Configure environment if we are doing a cross compile.
if [ $__CrossBuild == 1 ]; then
    export CROSSCOMPILE=1
    if ! [[ -n "$ROOTFS_DIR" ]]; then
        export ROOTFS_DIR="$__ProjectRoot/cross/rootfs/$__BuildArch"
    fi
fi

setup_dirs

export BUILDERRORLEVEL=0
export BUILDVARS_DONE=1