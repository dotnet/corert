#!/usr/bin/env bash

usage()
{
    echo "Usage: $0 [dbg^|rel] [x86^|amd64^|arm] [-?]"
    exit -8
}


source $(cd "$(dirname "$0")"; pwd -P)/testenv.sh

if [ -z ${__CoreRT_BuildOS} ]; then
    __CoreRT_BuildOS=Linux
fi

if [ -z ${__CoreRT_BuildArch} ]; then
    echo "Set __CoreRT_BuildArch to x86/x64/arm/arm64"
    exit -1
fi

if [ -z ${__CoreRT_BuildType} ]; then
    echo "Set __CoreRT_BuildType to Debug or Release"
    exit -1
fi

__CoreRT_ToolchainPkg=toolchain.ubuntu.14.04-${__CoreRT_BuildArch}.Microsoft.DotNet.ILCompiler.Development
__CoreRT_ToolchainVer=1.0.2-prerelease-00001
__CoreRT_AppDepSdkPkg=toolchain.ubuntu.14.04-${__CoreRT_BuildArch}.Microsoft.DotNet.AppDep
__CoreRT_AppDepSdkVer=1.0.0-prerelease
__CoreRT_ProtoJitPkg=toolchain.ubuntu.14.04-${__CoreRT_BuildArch}.Microsoft.DotNet.RyuJit
__CoreRT_ProtoJitVer=1.0.0-prerelease
__CoreRT_ObjWriterPkg=toolchain.ubuntu.14.04-${__CoreRT_BuildArch}.Microsoft.DotNet.ObjectWriter
__CoreRT_ObjWriterVer=1.0.2-prerelease

__ScriptDir=$(cd "$(dirname "$0")"; pwd -P)
__BuildStr=${__CoreRT_BuildOS}.${__CoreRT_BuildArch}.${__CoreRT_BuildType}

while test $# -gt 0
    do
        lowerI="$(echo $1 | awk '{print tolower($0)}')"
        case $lowerI in
        -?|-h|-help)
            usage
            exit 1
            ;;
        -nugetexedir)
            shift
            __NuGetExeDir=$1
            ;;
        -nupkgdir)
            shift
            __BuiltNuPkgDir=$1
            ;;
        -installdir)
            shift
            __NuPkgInstallDir=$1
            ;;
        -nugetopt)
            shift
            __NuGetOptions=$1
            ;;
        *)
            ;;
        esac
    shift
done

__TempFolder=`mktemp -d`
__NuPkgUnpackDir=${__TempFolder}

if [ ! -f ${__NuGetExeDir}/NuGet.exe ] ; then
    echo "No NuGet.exe found at ${__NuGetExeDir}.  Specify -nugetexedir option"
    exit -1
fi

if [ -z ${__NuPkgInstallDir} ] ; then
    echo "Specify -installdir option"
    exit -1
fi

if [ ! -d ${__BuiltNuPkgDir} ] ; then
    echo "Specify -nupkgdir to point to the build toolchain path"
    echo ${__BuiltNuPkgDir}
    exit -1
fi

echo "Cleaning up ${__NuPkgInstallDir}"
rm -rf ${__NuPkgInstallDir}
mkdir -p ${__NuPkgInstallDir}
if [ ! -d ${__NuPkgInstallDir} ]; then
    echo "Could not make install dir"
    exit -1
fi

__NuGetFeedUrl="https://www.myget.org/F/dotnet/auth/3e4f1dbe-f43a-45a8-b029-3ad4d25605ac/api/v2"

echo Installing CoreRT external dependencies
mono ${__NuGetExeDir}/NuGet.exe install -Source ${__NuGetFeedUrl} -OutputDir ${__NuPkgInstallDir} -Version ${__CoreRT_AppDepSdkVer} ${__CoreRT_AppDepSdkPkg} -prerelease ${__NuGetOptions} -nocache

echo Installing ProtoJit from NuGet
mono ${__NuGetExeDir}/NuGet.exe install -Source ${__NuGetFeedUrl} -OutputDir ${__NuPkgInstallDir} -Version ${__CoreRT_ProtoJitVer} ${__CoreRT_ProtoJitPkg} -prerelease ${__NuGetOptions}

echo Installing ObjectWriter from NuGet
mono ${__NuGetExeDir}/NuGet.exe install -Source ${__NuGetFeedUrl} -OutputDir ${__NuPkgInstallDir} -Version ${__CoreRT_ObjWriterVer} ${__CoreRT_ObjWriterPkg} -prerelease ${__NuGetOptions}

echo Installing ILCompiler from ${__BuiltNuPkgPath} into ${__NuPkgInstallDir}
__BuiltNuPkgPath=${__BuiltNuPkgDir}/${__CoreRT_ToolchainPkg}.${__CoreRT_ToolchainVer}.nupkg

if [ ! -f ${__BuiltNuPkgPath} ]; then
    echo "Did not find a build ${__BuiltNuPkgPath}.  Did you run build.sh?"
    exit -1
fi

if [ ! -d ${__NuPkgUnpackDir} ]; then
    echo "Could not make install dir"
    exit -1
fi

cp ${__BuiltNuPkgPath} ${__NuPkgUnpackDir}
echo "<packages><package id=\"${__CoreRT_ToolchainPkg}\" version=\"${__CoreRT_ToolchainVer}\"/></packages>" > ${__NuPkgUnpackDir}/packages.config
mono ${__NuGetExeDir}/NuGet.exe install "${__NuPkgUnpackDir}/packages.config" -Source "${__NuPkgUnpackDir}" -OutputDir "${__NuPkgInstallDir}" -prerelease ${__NuGetOptions}
rm -rf ${__NuPkgUnpackDir}
chmod +x ${__NuPkgInstallDir}/${__CoreRT_ToolchainPkg}.${__CoreRT_ToolchainVer}/corerun

export __CoreRT_AppDepSdkDir=${__NuPkgInstallDir}/${__CoreRT_AppDepSdkPkg}.${__CoreRT_AppDepSdkVer}
export __CoreRT_ProtoJitDir=${__NuPkgInstallDir}/${__CoreRT_ProtoJitPkg}.${__CoreRT_ProtoJitVer}/runtimes/ubuntu.14.04-${__CoreRT_BuildArch}/native/
export __CoreRT_ObjWriterDir=${__NuPkgInstallDir}/${__CoreRT_ObjWriterPkg}.${__CoreRT_ObjWriterVer}/runtimes/ubuntu.14.04-${__CoreRT_BuildArch}/native/
export __CoreRT_ToolchainDir=${__NuPkgInstallDir}/${__CoreRT_ToolchainPkg}.${__CoreRT_ToolchainVer}
