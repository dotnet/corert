#!/usr/bin/env bash

usage()
{
    echo "Usage: $0 [OS] [arch] [flavor] [-extrepo] [-buildextrepo] [-mode] [-runtest]"
    echo "    -mode         : Compilation mode. Specify cpp/ryujit. Default: ryujit"
    echo "    -runtest      : Should just compile or run compiled binary? Specify: true/false. Default: true."
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
    ${__SourceFile}.sh $1 $2 ${CoreRT_BuildType}
    return $?
}

restore()
{
    ${CoreRT_CliBinDir}/dotnet restore $1
}

compiletest()
{
    echo "Compiling dir $1 with dotnet compile $2"
    rm -rf $1/bin $1/obj
    ${CoreRT_CliBinDir}/dotnet compile --native -c ${CoreRT_BuildType} --ilcpath ${CoreRT_ToolchainDir} $1 $2
}

run_test_dir()
{
    local __test_dir=$1
    local __restore=$2
    local __mode=$3
    local __dir_path=`dirname ${__test_dir}`
    local __filename=`basename ${__dir_path}`
    if [ ${__restore} == 1 ]; then
      restore ${__dir_path}
    fi
    local __compile_args=""
    if [ "${__mode}" = "Cpp" ]; then
      __compile_args="--cpp"
    fi
    compiletest ${__dir_path} ${__compile_args}
    runtest ${__dir_path} ${__filename}
    local __exitcode=$?
    if [ ${__exitcode} == 0 ]; then
        local __pass_var=__${__mode}PassedTests
        eval ${__pass_var}=$((${__pass_var} + 1))
        echo "<test name=\"${__dir_path}\" type=\"${__filename}:${__mode}\" method=\"Main\" result=\"Pass\" />" >> ${__CoreRTTestBinDir}/testResults.tmp
    else
        echo "<test name=\"${__dir_path}\" type=\"${__filename}:${__mode}\" method=\"Main\" result=\"Fail\">" >> ${__CoreRTTestBinDir}/testResults.tmp
        echo "<failure exception-type=\"Exit code: ${__exitcode}\">" >> ${__CoreRTTestBinDir}/testResults.tmp
        echo     "<message>See ${__dir_path} /bin or /obj for logs </message>" >> ${__CoreRTTestBinDir}/testResults.tmp
        echo "</failure>" >> ${__CoreRTTestBinDir}/testResults.tmp
        echo "</test>" >> ${__CoreRTTestBinDir}/testResults.tmp
    fi
    local __total_var=__${__mode}TotalTests
    eval ${__total_var}=$((${__total_var} + 1))
    return $?
}

CoreRT_TestRoot=$(cd "$(dirname "$0")"; pwd -P)
CoreRT_CliBinDir=${CoreRT_TestRoot}/../bin/tools/cli/bin
CoreRT_BuildArch=x64
CoreRT_BuildType=Debug
CoreRT_TestRun=true
CoreRT_TestCompileMode=ryujit
CoreRT_TestExtRepo=
CoreRT_BuildExtRepo=

# Use uname to determine what the OS is.
OSName=$(uname -s)
case $OSName in
    Linux)
        CoreRT_BuildOS=Linux
        ;;

    Darwin)
        CoreRT_BuildOS=OSX
        ;;

    FreeBSD)
        CoreRT_BuildOS=FreeBSD
        ;;

    *)
        echo "Unsupported OS $OSName detected, configuring as if for Linux"
        CoreRT_BuildOS=Linux
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
            CoreRT_BuildArch=x86
            ;;
        x64)
            CoreRT_BuildArch=x64
            ;;
        arm)
            CoreRT_BuildArch=arm
            ;;
        arm64)
            CoreRT_BuildArch=arm64
            ;;
        debug)
            CoreRT_BuildType=Debug
            ;;
        release)
            CoreRT_BuildType=Release
            ;;
        -extrepo)
            shift
            CoreRT_TestExtRepo=$i
            ;;
        -mode)
            shift
            CoreRT_TestCompileMode=$i
            ;;
        -runtest)
            shift
            CoreRT_TestRun=$i
            ;;
        -nocache)
            CoreRT_NuGetOptions=-nocache
            ;;
        *)
            ;;
    esac
done

__BuildStr=${CoreRT_BuildOS}.${CoreRT_BuildArch}.${CoreRT_BuildType}
__CoreRTTestBinDir=${CoreRT_TestRoot}/../bin/tests
__LogDir=${CoreRT_TestRoot}/../bin/Logs/${__BuildStr}/tests
__NuPkgInstallDir=${__CoreRTTestBinDir}/package
__BuiltNuPkgDir=${CoreRT_TestRoot}/../bin/Product/${__BuildStr}/.nuget
__PackageRestoreCmd=$CoreRT_TestRoot/restore.sh
source ${__PackageRestoreCmd} -nugetexedir ${CoreRT_TestRoot}/../packages -installdir ${__NuPkgInstallDir} -nupkgdir ${__BuiltNuPkgDir} -nugetopt ${CoreRT_NuGetOptions}

if [ ! -d ${CoreRT_AppDepSdkDir} ]; then
    echo "AppDep SDK not installed at ${CoreRT_AppDepSdkDir}"
    exit -1
fi

if [ ! -d ${CoreRT_ToolchainDir} ]; then
    echo "Toolchain not found in ${CoreRT_ToolchainDir}"
    exit -1
fi

__CppTotalTests=0
__CppPassedTests=0
__JitTotalTests=0
__JitPassedTests=0

echo > ${__CoreRTTestBinDir}/testResults.tmp

__BuildOsLowcase=$(echo "${CoreRT_BuildOS}" | tr '[:upper:]' '[:lower:]')

for json in $(find src -iname 'project.json')
do
    __restore=1
    # Disable RyuJIT for OSX.
    if [ ${__BuildOsLowcase} != "osx" ]; then
        run_test_dir ${json} ${__restore} "Jit"
        __restore=0
    fi
    run_test_dir ${json} ${__restore} "Cpp"
done

__TotalTests=$((${__JitTotalTests} + ${__CppTotalTests}))
__PassedTests=$((${__JitPassedTests} + ${__CppPassedTests}))
__FailedTests=$((${__TotalTests} - ${__PassedTests}))

echo "<?xml version=\"1.0\" encoding=\"utf-8\"?>" > ${__CoreRTTestBinDir}/testResults.xml
echo "<assemblies>"  >> ${__CoreRTTestBinDir}/testResults.xml
echo "<assembly name=\"ILCompiler\" total=\"${__TotalTests}\" passed=\"${__PassedTests}\" failed=\"${__FailedTests}\" skipped=\"0\">"  >> ${__CoreRTTestBinDir}/testResults.xml
echo "<collection total=\"${__TotalTests}\" passed=\"${__PassedTests}\" failed=\"${__FailedTests}\" skipped=\"0\">"  >> ${__CoreRTTestBinDir}/testResults.xml
cat "${__CoreRTTestBinDir}/testResults.tmp" >> ${__CoreRTTestBinDir}/testResults.xml
echo "</collection>"  >> ${__CoreRTTestBinDir}/testResults.xml
echo "</assembly>"  >> ${__CoreRTTestBinDir}/testResults.xml
echo "</assemblies>"  >> ${__CoreRTTestBinDir}/testResults.xml


echo "JIT - TOTAL: ${__JitTotalTests} PASSED: ${__JitPassedTests}"
echo "CPP - TOTAL: ${__CppTotalTests} PASSED: ${__CppPassedTests}"

# Disable RyuJIT for OSX.
if [ ${__BuildOsLowcase} != "osx" ]; then
    if [ ${__JitTotalTests} == 0 ]; then
        exit 1
    fi
fi

if [ ${__CppTotalTests} == 0 ]; then
    exit 1
fi
if [ ${__JitTotalTests} -gt ${__JitPassedTests} ]; then
    exit 1
fi
if [ ${__CppTotalTests} -gt ${__CppPassedTests} ]; then
    exit 1
fi

exit 0


