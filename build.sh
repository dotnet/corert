#!/usr/bin/env bash

usage()
{
    echo "Usage: $0 [managed] [native] [BuildArch] [BuildType] [clean] [verbose] [clangx.y]"
    echo "managed - optional argument to build the managed code"
    echo "native - optional argument to build the native code"
    echo "The following arguments affect native builds only:"
    echo "BuildArch can be: x64, x86, arm, arm64"
    echo "BuildType can be: Debug, Release"
    echo "clean - optional argument to force a clean build."
    echo "verbose - optional argument to enable verbose build output."
    echo "clangx.y - optional argument to build using clang version x.y."

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

check_managed_prereqs()
{
    __monoversion=$(mono --version | grep "version 4.[1-9]")

    if [ $? -ne 0 ]; then
        # if built from tarball, mono only identifies itself as 4.0.1
        __monoversion=$(mono --version | egrep "version 4.0.[1-9]+(.[0-9]+)?")
        if [ $? -ne 0 ]; then
            echo "Mono 4.0.1.44 or later is required to build corert."
            exit 1
        else
            echo "WARNING: Mono 4.0.1.44 or later is required to build corert. Unable to assess if current version is supported."
        fi
    fi
}

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
    # Pull NuGet.exe down if we don't have it already
    if [ ! -e "$__nugetpath" ]; then
        which curl wget > /dev/null 2> /dev/null
        if [ $? -ne 0 -a $? -ne 1 ]; then
            echo "cURL or wget is required to build corert."
            exit 1
        fi
        echo "Restoring NuGet.exe..."

        # curl has HTTPS CA trust-issues less often than wget, so lets try that first.
        which curl > /dev/null 2> /dev/null
        if [ $? -ne 0 ]; then
           mkdir -p $__packageroot
           wget -q -O $__nugetpath https://api.nuget.org/downloads/nuget.exe
        else
           curl -sSL --create-dirs -o $__nugetpath https://api.nuget.org/downloads/nuget.exe
        fi

        if [ $? -ne 0 ]; then
            echo "Failed to restore NuGet.exe."
            exit 1
        fi
    fi

    # Grab the MSBuild package if we don't have it already
    if [ ! -e "$__msbuildpath" ]; then
        echo "Restoring MSBuild..."
        mono "$__nugetpath" install $__msbuildpackageid -Version $__msbuildpackageversion -source "https://www.myget.org/F/dotnet-buildtools/" -OutputDirectory "$__packageroot"
        if [ $? -ne 0 ]; then
            echo "Failed to restore MSBuild."
            exit 1
        fi
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

build_managed_corert()
{
    __buildproj=$__scriptpath/build.proj
    __buildlog=$__scriptpath/msbuild.log

    # TODO: Renable running tests
    mono $__msbuildpath "$__buildproj" /nologo /verbosity:minimal "/fileloggerparameters:Verbosity=normal;LogFile=$__buildlog" /t:Build /p:CleanedTheBuild=$__CleanBuild /p:SkipTests=true /p:TestNugetRuntimeId=$__TestNugetRuntimeId /p:ToolNugetRuntimeId=$__ToolNugetRuntimeId /p:OSEnvironment=Unix /p:OSGroup=$__BuildOS /p:Configuration=$__BuildType /p:Platform=$__BuildArch /p:UseRoslynCompiler=true /p:COMPUTERNAME=$(hostname) /p:USERNAME=$(id -un) "$@"
    BUILDERRORLEVEL=$?

    echo

    # Pull the build summary from the log file
    tail -n 4 "$__buildlog"
    echo Build Exit Code = $BUILDERRORLEVEL
}

build_native_corert()
{
    # All set to commence the build

    echo "Commencing build of corert native components for $__BuildOS.$__BuildArch.$__BuildType"
    cd "$__IntermediatesDir"

    # Regenerate the CMake solution
    echo "Invoking cmake with arguments: \"$__nativeroot\" $__CMakeArgs"
    "$__nativeroot/gen-buildsys-clang.sh" "$__nativeroot" $__ClangMajorVersion $__ClangMinorVersion $__CMakeArgs

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
__nativeroot=$__scriptpath/src/Native
__packageroot=$__scriptpath/packages
__sourceroot=$__scriptpath/src
__nugetpath=$__packageroot/NuGet.exe
__nugetconfig=$__sourceroot/NuGet.Config
__rootbinpath="$__scriptpath/bin"
__msbuildpackageid="Microsoft.Build.Mono.Debug"
__msbuildpackageversion="14.1.0.0-prerelease"
__msbuildpath=$__packageroot/$__msbuildpackageid.$__msbuildpackageversion/lib/MSBuild.exe
__ToolNugetRuntimeId=ubuntu.14.04-x64
__TestNugetRuntimeId=ubuntu.14.04-x64
__buildmanaged=true
__buildnative=true

# Workaround to enable nuget package restoration work successully on Mono
export TZ=UTC 
export MONO_THREADS_PER_CPU=2000

# Use uname to determine what the CPU is.
CPUName=$(uname -p)
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
    Linux)
        __BuildOS=Linux
        ;;

    Darwin)
        __BuildOS=OSX
        __ToolNugetRuntimeId=osx.10.10-x64
        __TestNugetRuntimeId=osx.10.10-x64
        ;;

    FreeBSD)
        __BuildOS=FreeBSD
        __ToolNugetRuntimeId=osx.10.10-x64
        __TestNugetRuntimeId=osx.10.10-x64
        ;;

    *)
        echo "Unsupported OS $OSName detected, configuring as if for Linux"
        __BuildOS=Linux
        ;;
esac
__BuildType=Debug
__CMakeArgs=DEBUG

case $__BuildOS in
    FreeBSD)
        __monoroot=/usr/local
        ;;
    OSX)
        __monoroot=/Library/Frameworks/Mono.framework/Versions/Current
        ;;
    *)
        __monoroot=/usr
        ;;
esac

__referenceassemblyroot=$__monoroot/lib/mono/xbuild-frameworks
BUILDERRORLEVEL=0

# Set the various build properties here so that CMake and MSBuild can pick them up
__UnprocessedBuildArgs=
__CleanBuild=0
__VerboseBuild=0
__ClangMajorVersion=3
__ClangMinorVersion=5

for i in "$@"
    do
        lowerI="$(echo $i | awk '{print tolower($0)}')"
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
            __CMakeArgs=RELEASE
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
        *)
          __UnprocessedBuildArgs="$__UnprocessedBuildArgs $i"
    esac
done

# If neither managed nor native are passed as arguments, default to building native only

if [ "$__buildmanaged" = false -a "$__buildnative" = false ]; then
    __buildmanaged=false
    __buildnative=true
fi

# Set the remaining variables based upon the determined build configuration
__IntermediatesDir="$__rootbinpath/obj/Native/$__BuildOS.$__BuildArch.$__BuildType"
__ProductBinDir="$__rootbinpath/Product/$__BuildOS.$__BuildArch.$__BuildType"

# Make the directories necessary for build if they don't exist

# Configure environment if we are doing a clean build.
if [ $__CleanBuild == 1 ]; then
    clean
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

    # Check prereqs.

    check_managed_prereqs

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

echo "Product binaries are available at $__ProductBinDir"

exit $BUILDERRORLEVEL
