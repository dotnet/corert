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

get_current_linux_rid() {
    # Construct RID for current distro

    rid=linux

    if [ -e /etc/os-release ]; then
        source /etc/os-release
        if [[ $ID == "alpine" ]]; then
            rid="linux-musl"
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
export __CrossBuild=0

__BuildArch=$__HostArch

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
            export __ExtraMsBuildArgs="$__ExtraMsBuildArgs /p:ObjWriterBuild=true"
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
        buildtests)
            export __BuildTests=1
            ;;
        x86|x64|arm|arm64|armel|wasm)
            __BuildArch=$lowerI
            ;;
        clang*)
            export __ClangMajorVersion=${lowerI:5:1}
            export __ClangMinorVersion=${lowerI:7:1}
            ;;
        *)
            export __UnprocessedBuildArgs="$__UnprocessedBuildArgs $1"
    esac
    shift
done

if [ -z "$__ClangMajorVersion" ] || [ -z "$__ClangMinorVersion" ]; then
    # Checking for any clang versions, if there is a symlink
    if [ -x "$(command -v clang)" ]; then
        export __ClangMajorVersion="$(echo | clang -dM -E - | grep __clang_major__ | cut -f3 -d ' ')"
        export __ClangMinorVersion="$(echo | clang -dM -E - | grep __clang_minor__ | cut -f3 -d ' ')"
    else
        export __ClangMajorVersion=3
        export __ClangMinorVersion=9
    fi
fi

if [ "${__HostOS}" != "OSX" ] && [ -z "$CppCompilerAndLinker" ]; then
    export CppCompilerAndLinker=clang-${__ClangMajorVersion}.${__ClangMinorVersion}
fi

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

export __LogsDir="$__rootbinpath/Logs"
export __TestBuildLog="$__LogsDir/tests_$__BuildOS.$__BuildArch.$__BuildType.log"

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
