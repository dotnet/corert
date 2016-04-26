#!/usr/bin/env bash

usage()
{
    echo "Usage: $0 [OS] [arch] [flavor] [-extrepo] [-buildextrepo] [-mode] [-runtest]"
    echo "    -mode         : Compilation mode. Specify cpp/ryujit. Default: ryujit"
    echo "    -runtest      : Should just compile or run compiled binary? Specify: true/false. Default: true."
    echo "    -extrepo      : Path to external repo, currently supports: GitHub: dotnet/coreclr. Specify full path. If unspecified, runs corert tests"
    echo "    -buildextrepo : Should build at root level of external repo? Specify: true/false. Default: true"
    exit 1
}

runtest()
{
    echo "Running test $1 $2"
    __SourceFolder=$1
    __SourceFileName=$2
    __SourceFile=${__SourceFolder}/${__SourceFileName}
    ${__SourceFile}.sh $1/bin/${CoreRT_BuildType}/dnxcore50/${__BuildRid}/native $2
    return $?
}

restore()
{
    ${CoreRT_CliBinDir}/dotnet restore --quiet $1 --source "https://dotnet.myget.org/F/dotnet-core"
}

compiletest()
{
    echo "Compiling dir $1 with dotnet build $2"
    rm -rf $1/bin $1/obj
    ${CoreRT_CliBinDir}/dotnet build --runtime ${__BuildRid} --native -c ${CoreRT_BuildType} --ilcpath ${CoreRT_ToolchainDir} --appdepsdkpath ${CoreRT_AppDepSdkDir} $1 $2
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
CoreRT_CliBinDir=${CoreRT_TestRoot}/../Tools/dotnetcli
CoreRT_BuildArch=x64
CoreRT_BuildType=Debug
CoreRT_TestRun=true
CoreRT_TestCompileMode=ryujit
CoreRT_TestExtRepo=
CoreRT_BuildExtRepo=

while [ "$1" != "" ]; do
        lowerI="$(echo $1 | awk '{print tolower($0)}')"
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
            CoreRT_TestExtRepo=$1
            ;;
        -mode)
            shift
            CoreRT_TestCompileMode=$1
            ;;
        -runtest)
            shift
            CoreRT_TestRun=$1
            ;;
        -dotnetclipath) 
            shift
            CoreRT_CliBinDir=$1
            ;;
        *)
            ;;
    esac
    shift
done

source "$CoreRT_TestRoot/testenv.sh"

__BuildStr=${CoreRT_BuildOS}.${CoreRT_BuildArch}.${CoreRT_BuildType}
__CoreRTTestBinDir=${CoreRT_TestRoot}/../bin/tests
__LogDir=${CoreRT_TestRoot}/../bin/Logs/${__BuildStr}/tests
__build_os_lowcase=$(echo "${CoreRT_BuildOS}" | tr '[:upper:]' '[:lower:]')
if [ ${__build_os_lowcase} != "osx" ]; then
    __BuildRid=ubuntu.14.04-${CoreRT_BuildArch}
else
    __BuildRid=osx.10.10-${CoreRT_BuildArch}
fi

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
    if [ ! -e `dirname ${json}`/no_unix ]; then
        __restore=1
        run_test_dir ${json} ${__restore} "Jit"
        __restore=0
        if [ ! -e `dirname ${json}`/no_cpp ]; then
            run_test_dir ${json} ${__restore} "Cpp"
        fi
    fi
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

if [ ${__JitTotalTests} == 0 ]; then
    exit 1
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
