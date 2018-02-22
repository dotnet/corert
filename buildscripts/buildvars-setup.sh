#!/usr/bin/env bash

usage()
{
    echo "Usage: $0 [managed] [native] [BuildArch] [BuildType] [clean] [cross] [verbose] [objwriter] [clangx.y]"
    echo "managed - optional argument to build the managed code"
    echo "native - optional argument to build the native code"
    echo "The following arguments affect native builds only:"
    echo "BuildArch can be: x64, x86, arm, arm64, armel, wasm"
    echo "BuildType can be: Debug, Release"
    echo "clean - optional argument to force a clean build."
    echo "verbose - optional argument to enable verbose build output."
    echo "objwriter - optional argument to enable build ObjWriter library"
    echo "clangx.y - optional argument to build using clang version x.y."
    echo "cross - optional argument to signify cross compilation,"
    echo "      - will use ROOTFS_DIR environment variable if set."
    echo "skiptests - optional argument to skip running tests after building."
    exit 1
}

setup_dirs()
{
    echo Setting up directories for build

    mkdir -p "$__ProductBinDir"
    mkdir -p "$__IntermediatesDir"
    if [ $__CrossBuild = 1 ]; then
        mkdir -p "$__ProductHostBinDir"
        mkdir -p "$__IntermediatesHostDir"
    fi
}

# Performs "clean build" type actions (deleting and remaking directories)

clean()
{
    echo "Cleaning previous output for the selected configuration"
    rm -rf "$__ProductBinDir"
    rm -rf "$__IntermediatesDir"
    if [ $__CrossBuild = 1 ]; then
        rm -rf "$__ProductHostBinDir"
        rm -rf "$__IntermediatesHostDir"
    fi
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

get_current_linux_rid() {
    # Construct RID for current distro

    rid=linux

    if [ -e /etc/os-release ]; then
        source /etc/os-release
        if [[ $ID == "alpine" ]]; then
            # remove the last version digit
            VERSION_ID=${VERSION_ID%.*}
            rid=alpine.$VERSION_ID
        fi

    elif [ -e /etc/redhat-release ]; then
          redhatRelease=$(</etc/redhat-release)
          if [[ $redhatRelease == "CentOS release 6."* || $redhatRelease == "Red Hat Enterprise Linux Server release 6."* ]]; then
              rid=rhel.6
          fi
    fi

    echo $rid
}

# Disable telemetry, first time experience, and global sdk look for the CLI
export DOTNET_CLI_TELEMETRY_OPTOUT=1
export DOTNET_SKIP_FIRST_TIME_EXPERIENCE=1
export DOTNET_MULTILEVEL_LOOKUP=0

export __scriptpath="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
export __ProjectRoot=$__scriptpath/..
export __sourceroot=$__ProjectRoot/src
export __rootbinpath="$__ProjectRoot/bin"
export __buildmanaged=false
export __buildnative=false
export __dotnetclipath=$__ProjectRoot/Tools/dotnetcli

# Initialize variables that depend on the compilation host
. $__scriptpath/hostvars-setup.sh

export __BuildType=Debug

export BUILDERRORLEVEL=0

# Set the various build properties here so that CMake and MSBuild can pick them up
export __UnprocessedBuildArgs=
export __CleanBuild=0
export __VerboseBuild=0
export __ObjWriterBuild=0
export __ClangMajorVersion=3
export __ClangMinorVersion=9
export __CrossBuild=0

__BuildArch=$__HostArch

# Checking for any clang versions, if there is a symlink
if [ -x "$(command -v clang)" ]; then
    __ClangMajorVersion="$(echo | clang -dM -E - | grep __clang_major__ | cut -f3 -d ' ')"
    __ClangMinorVersion="$(echo | clang -dM -E - | grep __clang_minor__ | cut -f3 -d ' ')"
fi

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
            __BuildArch=x86
            ;;
        x64)
            __BuildArch=x64
            ;;
        arm)
            __BuildArch=arm
            ;;
        arm64)
            __BuildArch=arm64
            ;;
        armel)
            __BuildArch=armel
            ;;
        wasm)
            __BuildArch=wasm
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
        objwriter)
            export __ObjWriterBuild=1
            ;;
        clang3.6)
            export __ClangMajorVersion=3
            export __ClangMinorVersion=6
            ;;
        clang3.7)
            export __ClangMajorVersion=3
            export __ClangMinorVersion=7
            ;;
        clang3.8)
            export __ClangMajorVersion=3
            export __ClangMinorVersion=8
            ;;
        clang3.9)
            export __ClangMajorVersion=3
            export __ClangMinorVersion=9
            ;;
        clang4.0)
            export __ClangMajorVersion=4
            export __ClangMinorVersion=0
            ;;
        clang5.0)
            export __ClangMajorVersion=5
            export __ClangMinorVersion=0
            ;;
        clang6.0)
            export __ClangMajorVersion=6
            export __ClangMinorVersion=0
            ;;
        clang7.0)
            export __ClangMajorVersion=7
            export __ClangMinorVersion=0
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
        skiptests)
            export __SkipTests=true
            ;;
        *)
          export __UnprocessedBuildArgs="$__UnprocessedBuildArgs $1"
    esac
    shift
done

export $__BuildArch

# Use uname to determine what the OS is.
export OSName=$(uname -s)
case $OSName in
    Darwin)
        export __HostOS=OSX
        export __NugetRuntimeId=osx-x64
        ulimit -n 2048
        ;;

    FreeBSD)
        export __HostOS=FreeBSD
        # TODO: Add proper FreeBSD target
        export __NugetRuntimeId=linux-x64
        ;;

    Linux)
        export __HostOS=Linux
        export __NugetRuntimeId=$(get_current_linux_rid)-$__HostArch
        ;;

    NetBSD)
        export __HostOS=NetBSD
        # TODO: Add proper NetBSD target
        export __NugetRuntimeId=linux-x64
        ;;

    *)
        echo "Unsupported OS $OSName detected, configuring as if for Linux"
        export __HostOS=Linux
        export __NugetRuntimeId=linux-x64
        ;;
esac

# For msbuild
if [ $__HostOS != "OSX" ]; then
    export CppCompilerAndLinker=clang-${__ClangMajorVersion}.${__ClangMinorVersion}
fi

export __BuildOS="$__HostOS"

# Overwrite __BuildOS with WebAssembly if wasm is target build arch, but keep the __NugetRuntimeId to match the Host OS
if [ $__BuildArch == "wasm" ]; then
    export __BuildOS=WebAssembly
fi

# If neither managed nor native are passed as arguments, default to building both

if [ "$__buildmanaged" = false -a "$__buildnative" = false ]; then
    export __buildmanaged=true
    export __buildnative=true
fi

# Set the remaining variables based upon the determined build configuration
export __IntermediatesDir="$__rootbinpath/obj/Native/$__BuildOS.$__BuildArch.$__BuildType"
if [ $__CrossBuild = 1 ]; then
    export __IntermediatesHostDir="$__rootbinpath/obj/Native/$__BuildOS.$__HostArch.$__BuildType"
fi
export __ProductBinDir="$__rootbinpath/$__BuildOS.$__BuildArch.$__BuildType"
if [ $__CrossBuild = 1 ]; then
    export __ProductHostBinDir="$__rootbinpath/$__BuildOS.$__HostArch.$__BuildType"
fi

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
