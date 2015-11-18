#!/usr/bin/env bash

usage()
{
    echo "Usage: $0 [dbg^|rel] [x86^|amd64^|arm] [-?]"
    exit -8
}


source $(cd "$(dirname "$0")"; pwd -P)/testenv.sh "$@"

if [ -z ${CoreRT_BuildOS} ]; then
    export CoreRT_BuildOS=Linux
fi

if [ -z ${CoreRT_BuildArch} ]; then
    echo "Set CoreRT_BuildArch to x86/x64/arm/arm64"
    exit -1
fi

if [ -z ${CoreRT_BuildType} ]; then
    echo "Set CoreRT_BuildType to Debug or Release"
    exit -1
fi

__build_os_lowcase=$(echo "${CoreRT_BuildOS}" | tr '[:upper:]' '[:lower:]')
if [ ${__build_os_lowcase} != "osx" ]; then
    __BuildRid=ubuntu.14.04
else
    __BuildRid=osx.10.10
fi
export CoreRT_ToolchainPkg=toolchain.${__BuildRid}-${CoreRT_BuildArch}.Microsoft.DotNet.ILCompiler.Development
export CoreRT_ToolchainVer=1.0.2-prerelease-00001
export CoreRT_AppDepSdkPkg=toolchain.${__BuildRid}-${CoreRT_BuildArch}.Microsoft.DotNet.AppDep
export CoreRT_AppDepSdkVer=1.0.2-prerelease-00002

__ScriptDir=$(cd "$(dirname "$0")"; pwd -P)
__BuildStr=${CoreRT_BuildOS}.${CoreRT_BuildArch}.${CoreRT_BuildType}

while test $# -gt 0
    do
        lowerI="$(echo $1 | awk '{print tolower($0)}')"
        case $lowerI in
        -h|-help)
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
mono ${__NuGetExeDir}/NuGet.exe install -Source ${__NuGetFeedUrl} -OutputDir ${__NuPkgInstallDir} -Version ${CoreRT_AppDepSdkVer} ${CoreRT_AppDepSdkPkg} -prerelease ${__NuGetOptions} -nocache

__BuiltNuPkgPath=${__BuiltNuPkgDir}/${CoreRT_ToolchainPkg}.${CoreRT_ToolchainVer}.nupkg
echo Installing ILCompiler from ${__BuiltNuPkgPath} into ${__NuPkgInstallDir}

if [ ! -f ${__BuiltNuPkgPath} ]; then
    echo "Did not find a build ${__BuiltNuPkgPath}.  Did you run build.sh?"
    exit -1
fi

mono ${__NuGetExeDir}/NuGet.exe install -Source "${__BuiltNuPkgDir}" -OutputDir "${__NuPkgInstallDir}" ${CoreRT_ToolchainPkg} -Version ${CoreRT_ToolchainVer} -prerelease ${__NuGetOptions}
chmod +x ${__NuPkgInstallDir}/${CoreRT_ToolchainPkg}.${CoreRT_ToolchainVer}/corerun

export CoreRT_AppDepSdkDir=${__NuPkgInstallDir}/${CoreRT_AppDepSdkPkg}.${CoreRT_AppDepSdkVer}
export CoreRT_ToolchainDir=${__NuPkgInstallDir}/${CoreRT_ToolchainPkg}.${CoreRT_ToolchainVer}
export CoreRT_RyuJitDir=${CoreRT_ToolchainDir}
export CoreRT_ObjWriterDir=${CoreRT_ToolchainDir}
