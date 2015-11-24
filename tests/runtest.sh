#!/usr/bin/env bash

usage()
{
	echo "Usage: $0 [OS] [arch] [flavor] [-extrepo] [-buildextrepo] [-mode] [-runtest]"
	echo "    -mode         : Compilation mode. Specify cpp/ryujit. Default: ryujit"
	echo "    -runtest      : Should just compile or run compiled bianry? Specify: true/false. Default: true."
	echo "    -extrepo      : Path to external repo, currently supports: GitHub: dotnet/coreclr. Specify full path. If unspecified, runs corert tests"
	echo "    -buildextrepo : Should build at root level of external repo? Specify: true/false. Default: true"
	echo "    -nocache      : When restoring toolchain packages, obtain them from the feed not the cache."
	exit 1
}

runtest()
{
    echo "Running test $1 $2"
    __SourceFolder=$1
    __SourceFileName=$2
    __SourceFile=${__SourceFolder}/${__SourceFileName}
    ${__SourceFile}.sh $1 $2
    return $?
}

compiletest()
{
    echo "Compiling test $1 $2"
    __SourceFolder=$1
    __SourceFileName=$2
    __cli_dir=${__CoreRT_TestRoot}/../bin/tools/cli/bin
    ${__cli_dir}/dotnet restore ${__SourceFolder}
    ${__cli_dir}/dotnet compile --native --ilcpath ${__CoreRT_ToolchainDir} ${__SourceFolder} 
}

__CoreRT_TestRoot=$(cd "$(dirname "$0")"; pwd -P)
__CoreRT_BuildArch=x64
__CoreRT_BuildType=Debug
__CoreRT_TestRun=true
__CoreRT_TestCompileMode=ryujit
__CoreRT_TestExtRepo=
__CoreRT_BuildExtRepo=

# Use uname to determine what the OS is.
OSName=$(uname -s)
case $OSName in
    Linux)
        __CoreRT_BuildOS=Linux
        ;;

    Darwin)
        __CoreRT_BuildOS=OSX
        ;;

    FreeBSD)
        __CoreRT_BuildOS=FreeBSD
        ;;

    *)
        echo "Unsupported OS $OSName detected, configuring as if for Linux"
        __CoreRT_BuildOS=Linux
        ;;
esac

for i in "$@"
	do
		lowerI="$(echo $i | awk '{print tolower($0)}')"
		case $lowerI in
		-?|-h|--help)
			usage
			exit 1
			;;
		x86)
			__CoreRT_BuildArch=x86
			;;
		x64)
			__CoreRT_BuildArch=x64
			;;
		arm)
			__CoreRT_BuildArch=arm
			;;
		arm64)
			__CoreRT_BuildArch=arm64
			;;
		debug)
			__CoreRT_BuildType=Debug
			;;
		release)
			__CoreRT_BuildType=Release
			;;
		-extrepo)
            shift
			__CoreRT_TestExtRepo=$i
			;;
		-mode)
            shift
			__CoreRT_TestCompileMode=$i
			;;
		-runtest)
            shift
			__CoreRT_TestRun=$i
			;;
		-nocache)
			__CoreRT_NuGetOptions=-nocache
			;;
		*)
			;;
	esac
done

__BuildStr=${__CoreRT_BuildOS}.${__CoreRT_BuildArch}.${__CoreRT_BuildType}
__BinDir=${__CoreRT_TestRoot}/../bin/tests
__LogDir=${__CoreRT_TestRoot}/../bin/Logs/${__BuildStr}/tests
__NuPkgInstallDir=${__BinDir}/package
__BuiltNuPkgDir=${__CoreRT_TestRoot}/../bin/Product/${__BuildStr}/.nuget

__PackageRestoreCmd=$__CoreRT_TestRoot/restore.sh
source ${__PackageRestoreCmd} -nugetexedir ${__CoreRT_TestRoot}/../packages -installdir ${__NuPkgInstallDir} -nupkgdir ${__BuiltNuPkgDir} -nugetopt ${__CoreRT_NuGetOptions}

if [ ! -d ${__CoreRT_AppDepSdkDir} ]; then
    echo "AppDep SDK not installed at ${__CoreRT_AppDepSdkDir}"
    exit -1
fi

if [ ! -d ${__CoreRT_ProtoJitDir} ]; then
    echo "ProtoJIT SDK not installed at ${__CoreRT_ProtoJitDir}"
    exit -1
fi

if [ ! -d ${__CoreRT_ObjWriterDir} ]; then
    echo "ObjWriter SDK not installed at ${__CoreRT_ObjWriterDir}"
    exit -1
fi

if [ ! -d ${__CoreRT_ToolchainDir} ]; then
    echo "Toolchain not found in ${__CoreRT_ToolchainDir}"
    exit -1
fi

if [ -z ${__CoreRT_ToolchainPkg} ]; then
    echo "Run ${__PackageRestoreCmd} first"
    exit -1
fi

if [ -z ${__CoreRT_ToolchainVer} ]; then
    echo "Run ${__PackageRestoreCmd} first"
    exit -1
fi

__TotalTests=0
__PassedTests=0

shopt -s globstar
for json in src/**/*.json
do
    __dir_path=`dirname ${json}`
    __filename=`basename ${__dir_path}`
    compiletest ${__dir_path}
    runtest ${__dir_path} ${__filename}
done

if [ "$?" == 0 ]; then
    __PassedTests=$(($__PassedTests + 1))
fi
__TotalTests=$(($__TotalTests + 1))
echo "TOTAL: ${__TotalTests} PASSED: ${__PassedTests}"

exit 0



