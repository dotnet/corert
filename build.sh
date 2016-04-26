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

# Prepare the system for building

prepare_managed_build()
{
    # Run Init-Tools to restore BuildTools and ToolRuntime
    $__scriptpath/init-tools.sh

    # Tell nuget to always use repo-local nuget package cache. The "dotnet restore" invocations use the --packages
    # argument, but there are a few commands in publish and tests that do not.
    export NUGET_PACKAGES=$__packageroot

    echo "Using CLI tools version:"
    ls "$__dotnetclipath/sdk"
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

build_managed_corert()
{
    __buildproj=$__scriptpath/build.proj
    __buildlog=$__scriptpath/msbuild.$__BuildArch.log

    if [ -z "${ToolchainMilestone}" ]; then
        ToolchainMilestone=testing
    fi

    $__scriptpath/Tools/corerun $__scriptpath/Tools/MSBuild.exe "$__buildproj" /m /nologo /verbosity:minimal "/fileloggerparameters:Verbosity=normal;LogFile=$__buildlog" /t:Build /p:RepoPath=$__ProjectRoot /p:RepoLocalBuild="true" /p:RelativeProductBinDir=$__RelativeProductBinDir /p:CleanedTheBuild=$__CleanBuild /p:SkipTests=true /p:TestNugetRuntimeId=$__TestNugetRuntimeId /p:ToolNugetRuntimeId=$__ToolNugetRuntimeId /p:OSEnvironment=Unix /p:OSGroup=$__BuildOS /p:Configuration=$__BuildType /p:Platform=$__BuildArch /p:COMPUTERNAME=$(hostname) /p:USERNAME=$(id -un) /p:ToolchainMilestone=${ToolchainMilestone} $__UnprocessedBuildArgs
    BUILDERRORLEVEL=$?

    echo

    # Pull the build summary from the log file
    tail -n 4 "$__buildlog"
    echo Build Exit Code = $BUILDERRORLEVEL

    # Workaround for --appdepsdkpath command line switch being ignored. 
    # Copy the restored appdepsdk package to its default location.
    source "${__ProjectRoot}/tests/testenv.sh" $__BuildType $__BuildArch
    __DOTNET_TOOLS_VERSION=$(cat $__scriptpath/DotnetCLIVersion.txt)
    cp -r "${CoreRT_AppDepSdkDir}" "${__dotnetclipath}/sdk/${__DOTNET_TOOLS_VERSION}/appdepsdk"
}

build_native_corert()
{
    # All set to commence the build

    echo "Commencing build of corert native components for $__BuildOS.$__BuildArch.$__BuildType"
    cd "$__IntermediatesDir"

    # Regenerate the CMake solution
    echo "Invoking cmake with arguments: \"$__ProjectRoot\" $__BuildType"
    "$__ProjectRoot/src/Native/gen-buildsys-clang.sh" "$__ProjectRoot" $__ClangMajorVersion $__ClangMinorVersion $__BuildArch $__BuildType

    # Check that the makefiles were created.

    if [ ! -f "$__IntermediatesDir/Makefile" ]; then
        echo "Failed to generate native component build project!"
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
        exit 1
    fi

    echo "CoreRT native components successfully built."
}

__scriptpath=$(cd "$(dirname "$0")"; pwd -P)
__ProjectRoot=$__scriptpath
__packageroot=$__scriptpath/packages
__sourceroot=$__scriptpath/src
__rootbinpath="$__scriptpath/bin"
__TestNugetRuntimeId=ubuntu.14.04-x64
__buildmanaged=false
__buildnative=false
__dotnetclipath=$__scriptpath/Tools/dotnetcli

# Use uname to determine what the CPU is.
CPUName=$(uname -p)
# Some Linux platforms report unknown for platform, but the arch for machine.
if [ $CPUName == "unknown" ]; then
    CPUName=$(uname -m)
fi

case $CPUName in
    i686)
        __BuildArch=x86
        ;;

    x86_64)
        __BuildArch=x64
        ;;

    armv7l)
        echo "Unsupported CPU $CPUName detected, build might not succeed!"
        __BuildArch=arm
        ;;

    aarch64)
        echo "Unsupported CPU $CPUName detected, build might not succeed!"
        __BuildArch=arm64
        ;;

    *)
        echo "Unknown CPU $CPUName detected, configuring as if for x64"
        __BuildArch=x64
        ;;
esac

# Use uname to determine what the OS is.
OSName=$(uname -s)
case $OSName in
    Darwin)
        __BuildOS=OSX
        __ToolNugetRuntimeId=osx.10.10-x64
        __TestNugetRuntimeId=osx.10.10-x64
        ;;

    FreeBSD)
        __BuildOS=FreeBSD
        # TODO: Add proper FreeBSD target
        __ToolNugetRuntimeId=osx.10.10-x64
        __TestNugetRuntimeId=osx.10.10-x64
        ;;

    Linux)
        __BuildOS=Linux
        ;;

    NetBSD)
        __BuildOS=NetBSD
        # TODO: Add proper NetBSD target
        __ToolNugetRuntimeId=osx.10.10-x64
        __TestNugetRuntimeId=osx.10.10-x64
        ;;

    *)
        echo "Unsupported OS $OSName detected, configuring as if for Linux"
        __BuildOS=Linux
        ;;
esac
__BuildType=Debug

BUILDERRORLEVEL=0

# Set the various build properties here so that CMake and MSBuild can pick them up
__UnprocessedBuildArgs=
__CleanBuild=0
__VerboseBuild=0
__ClangMajorVersion=3
__ClangMinorVersion=5
__CrossBuild=0

while [ "$1" != "" ]; do
        lowerI="$(echo $1 | awk '{print tolower($0)}')"
        case $lowerI in
        -h|--help)
            usage
            exit 1
            ;;
        managed)
            __buildmanaged=true
            ;;
        native)
            __buildnative=true
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
        debug)
            __BuildType=Debug
            ;;
        release)
            __BuildType=Release
            ;;
        clean)
            __CleanBuild=1
            ;;
        verbose)
            __VerboseBuild=1
            ;;
        clang3.5)
            __ClangMajorVersion=3
            __ClangMinorVersion=5
            ;;
        clang3.6)
            __ClangMajorVersion=3
            __ClangMinorVersion=6
            ;;
        clang3.7)
            __ClangMajorVersion=3
            __ClangMinorVersion=7
            ;;
        cross)
            __CrossBuild=1
            ;;
        -dotnetclipath) 
            shift
            __dotnetclipath=$1
        ;;
        *)
          __UnprocessedBuildArgs="$__UnprocessedBuildArgs $1"
    esac
    shift
done

# If neither managed nor native are passed as arguments, default to building both

if [ "$__buildmanaged" = false -a "$__buildnative" = false ]; then
    __buildmanaged=true
    __buildnative=true
fi

# Set the remaining variables based upon the determined build configuration
__IntermediatesDir="$__rootbinpath/obj/Native/$__BuildOS.$__BuildArch.$__BuildType"
__ProductBinDir="$__rootbinpath/Product/$__BuildOS.$__BuildArch.$__BuildType"
__RelativeProductBinDir="bin/Product/$__BuildOS.$__BuildArch.$__BuildType"

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

if $__buildnative; then

    # Check prereqs.

    check_native_prereqs

    # Prepare the system

    prepare_native_build

    # Build the corert native components.

    build_native_corert

    # Build complete
fi

# If native build failed, exit with the status code of the managed build
if [ $BUILDERRORLEVEL != 0 ]; then
    exit $BUILDERRORLEVEL
fi

if $__buildmanaged; then

    # Prepare the system

    prepare_managed_build

    # Build the corert native components.

    build_managed_corert

    # Build complete
fi

# If managed build failed, exit with the status code of the managed build
if [ $BUILDERRORLEVEL != 0 ]; then
    exit $BUILDERRORLEVEL
fi

pushd ${__scriptpath}/tests
source ${__scriptpath}/tests/runtest.sh $__BuildOS $__BuildArch $__BuildType -dotnetclipath $__dotnetclipath
TESTERRORLEVEL=$?
popd

if [ $TESTERRORLEVEL != 0 ]; then
    exit $TESTERRORLEVEL
fi

echo "Product binaries are available at $__ProductBinDir"

exit $BUILDERRORLEVEL
