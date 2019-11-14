#!/usr/bin/env bash

usage()
{
    echo "Usage: $0 [OS] [arch] [flavor] [-mode] [-runtest] [-coreclr <subset>]"
    echo "    -mode         : Compilation mode. Specify cpp/ryujit. Default: ryujit"
    echo "    -test         : Run a single test by folder name (ie, BasicThreading)"
    echo "    -runtest      : Should just compile or run compiled binary? Specify: true/false. Default: true."
    echo "    -corefx       : Download and run the CoreFX repo tests"
    echo "    -coreclr      : Download and run the CoreCLR repo tests"
    echo "    -multimodule  : Compile the framework as a .so and link tests against it (ryujit only)"
    echo "    -singlethread : Run tests on a single thread (avoid parallel execution)"
    echo "    -coredumps    : [For CI use] Enables core dump generation, and analyzes and possibly stores/uploads"
    echo "                      dumps collected during test run."
    echo ""
    echo "    --- CoreCLR Subset ---"
    echo "       top200     : Runs broad coverage / CI validation (~200 tests)."
    echo "       knowngood  : Runs tests known to pass on CoreRT (~6000 tests)."
    echo "       interop    : Runs only the interop tests (~43 tests)."
    echo "       all        : Runs all tests. There will be many failures (~7000 tests)."
    exit 1
}

runtest()
{
    echo "Running test $1 $2"
    __SourceFolder=$1
    __SourceFileName=$2
    __SourceFile=${__SourceFolder}/${__SourceFileName}
    ${__SourceFile}.sh $1/bin/${CoreRT_BuildType}/${CoreRT_BuildArch}/native $2
    return $?
}

run_test_dir()
{
    local __test_dir=$1
    local __mode=$2
    local __extra_cxxflags=$3
    local __extra_linkflags=$4
    local __dir_path=`dirname ${__test_dir}`
    local __filename=`basename ${__dir_path}`
    local __extra_args=""
    local __extra_flags=()
    if [ "${__mode}" = "Cpp" ]; then
        __extra_args="${__extra_args} /p:NativeCodeGen=cpp"
    fi
    if [ "${__mode}" = "Wasm" ]; then
        __extra_args="${__extra_args} /p:NativeCodeGen=wasm"
    fi
    if [ -n "${__extra_cxxflags}" ]; then
        __extra_cxxflags="/p:AdditionalCppCompilerFlags=\"${__extra_cxxflags}\""
        __extra_flags+=("${__extra_cxxflags}")
    fi
    if [ -n "${__extra_linkflags}" ]; then
        __extra_linkflags="/p:AdditionalLinkerFlags=\"${__extra_linkflags}\""
        __extra_flags+=("${__extra_linkflags}")
    fi
    if [ "${CoreRT_MultiFileConfiguration}" = "MultiModule" ]; then
        __extra_args="${__extra_args} /p:IlcMultiModule=true"
    fi

    rm -rf ${__dir_path}/bin/${CoreRT_BuildType} ${__dir_path}/obj/${CoreRT_BuildType}

    local __msbuild_dir=${CoreRT_TestRoot}/../Tools

    echo ${__msbuild_dir}/msbuild.sh /ds /m /p:IlcPath=${CoreRT_ToolchainDir} /p:Configuration=${CoreRT_BuildType} /p:Platform=${CoreRT_BuildArch} /p:OSGroup=${CoreRT_BuildOS} /p:RepoLocalBuild=true "/p:FrameworkLibPath=${CoreRT_TestRoot}/../bin/${CoreRT_BuildOS}.${CoreRT_BuildArch}.${CoreRT_BuildType}/lib" "/p:FrameworkObjPath=${CoreRT_TestRoot}/../bin/obj/${CoreRT_BuildOS}.${CoreRT_BuildArch}.${CoreRT_BuildType}/Framework" ${__extra_args} "${__extra_flags[@]}" ${__dir_path}/${__filename}.csproj
    ${__msbuild_dir}/msbuild.sh /ds /m /p:IlcPath=${CoreRT_ToolchainDir} /p:Configuration=${CoreRT_BuildType} /p:Platform=${CoreRT_BuildArch} /p:OSGroup=${CoreRT_BuildOS} /p:RepoLocalBuild=true "/p:FrameworkLibPath=${CoreRT_TestRoot}/../bin/${CoreRT_BuildOS}.${CoreRT_BuildArch}.${CoreRT_BuildType}/lib" "/p:FrameworkObjPath=${CoreRT_TestRoot}/../bin/obj/${CoreRT_BuildOS}.${CoreRT_BuildArch}.${CoreRT_BuildType}/Framework" ${__extra_args} "${__extra_flags[@]}" ${__dir_path}/${__filename}.csproj

    local __exitcode=$?

    if [ ${CoreRT_TestRun} == true ]; then
        runtest ${__dir_path} ${__filename}
        __exitcode=$?
    fi

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

download_and_unzip_coreclr_tests_artifacts()
{
    url=$1
    location=$2
    semaphore=$3
    if [ ! -e ${semaphore} ]; then
        if [ -d ${location} ]; then
            rm -r ${location}
        fi
        mkdir -p ${location}
    
        local_zip=${location}/tests.zip

        # Use curl if available, otherwise use wget
        if command -v curl > /dev/null; then
            curl --retry 10 --retry-delay 5 -sSL -o ${local_zip} ${url}
        else
            wget -q -O ${local_zip} ${url} --tries=10
        fi

        local __exitcode=$?
        if [ ${__exitcode} != 0 ]; then
            echo "Failed to download CoreCLR tests artifacts."
            exit ${__exitcode}
        fi

        unzip -q ${local_zip} -d ${location}
        __exitcode=$?
        # Exit code 1 indicates that a warning was encountered but unzipping completed successfully
        if [ ${__exitcode} != 0 ] && [ ${__exitcode} != 1 ]; then
            echo "Failed to unzip CoreCLR tests artifacts."
            exit ${__exitcode}
        fi

        echo "CoreCLR tests artifacts restored from ${url}" >> ${semaphore}
    fi
}

download_and_unzip_corefx_tests_artifacts()
{    
    url=$1
    test_list=$2
    #semaphore=${CoreRT_TestExtRepo_CoreFX}/init-tests.completed
    
    local __msbuild_dir=${CoreRT_CliBinDir}/..

    # Build and restore test helper projects
    ${CoreRT_CliBinDir}/dotnet restore "${CoreRT_TestFileHelperProjectPath}"
    local __exitcode=$?
    if [ ${__exitcode} != 0 ]; then
        exit ${__exitcode}
    fi

    ${CoreRT_CliBinDir}/dotnet restore "${CoreRT_XunitHelperProjectPath}"    
    __exitcode=$?
    if [ ${__exitcode} != 0 ]; then
        exit ${__exitcode}
    fi

    # Build the test helper projects
    ${CoreRT_CliBinDir}/dotnet build /m /ConsoleLoggerParameters:ForceNoAlign /p:IlcPath=${CoreRT_ToolchainDir} /p:Configuration=${CoreRT_BuildType} /p:Platform=${CoreRT_BuildArch} /p:OSGroup=${CoreRT_BuildOS} /p:RepoLocalBuild=true "/p:FrameworkLibPath=${CoreRT_TestRoot}/../bin/${CoreRT_BuildOS}.${CoreRT_BuildArch}.${CoreRT_BuildType}/lib" "/p:FrameworkObjPath=${CoreRT_TestRoot}/../bin/obj/${CoreRT_BuildOS}.${CoreRT_BuildArch}.${CoreRT_BuildType}/Framework" "/p:OutputPath=${CoreRT_TestingUtilitiesOutputDir}" "${CoreRT_TestFileHelperProjectPath}" 
    
    __exitcode=$?
    if [ ${__exitcode} != 0 ]; then
        exit ${__exitcode}
    fi

   ${CoreRT_CliBinDir}/dotnet build /m /ConsoleLoggerParameters:ForceNoAlign /p:IlcPath=${CoreRT_ToolchainDir} /p:Configuration=${CoreRT_BuildType} /p:Platform=${CoreRT_BuildArch} /p:OSGroup=${CoreRT_BuildOS} /p:RepoLocalBuild=true "/p:FrameworkLibPath=${CoreRT_TestRoot}/../bin/${CoreRT_BuildOS}.${CoreRT_BuildArch}.${CoreRT_BuildType}/lib" "/p:FrameworkObjPath=${CoreRT_TestRoot}/../bin/obj/${CoreRT_BuildOS}.${CoreRT_BuildArch}.${CoreRT_BuildType}/Framework" "/p:OutputPath=${CoreRT_TestingUtilitiesOutputDir}" "${CoreRT_XunitHelperProjectPath}" 
    __exitcode=$?
    if [ ${__exitcode} != 0 ]; then
        exit ${__exitcode}
    fi

    ${CoreRT_CliBinDir}/dotnet ${CoreRT_TestingUtilitiesOutputDir}/${CoreRT_TestFileHelperName}.dll --clean --outputDirectory ${CoreRT_TestExtRepo_CoreFX} --testListJsonPath ${test_list} --testUrl ${url}
    __exitcode=$?
    if [ ${__exitcode} != 0 ]; then
        exit ${__exitcode}
    fi
}


restore_coreclr_tests()
{
    CoreRT_Test_Download_Semaphore=${CoreRT_TestExtRepo}/init-tests.completed

    if [ -e ${CoreRT_Test_Download_Semaphore} ]; then
        echo "Tests are already initialized."
        return 0
    fi
    TESTS_REMOTE_URL=$(<${CoreRT_TestRoot}/CoreCLRTestsURL_${CoreRT_BuildOS}.txt)

    echo "Restoring tests (this may take a few minutes).."
    download_and_unzip_coreclr_tests_artifacts ${TESTS_REMOTE_URL}  ${CoreRT_TestExtRepo} ${CoreRT_Test_Download_Semaphore}
}

run_coreclr_tests()
{
    if [ -z ${CoreRT_TestExtRepo} ]; then
        CoreRT_TestExtRepo=$( dirname ${CoreRT_TestRoot} )/tests_downloaded/CoreCLR
    fi

    restore_coreclr_tests

    if [ ! -d ${CoreRT_TestExtRepo} ]; then
        echo "Error: ${CoreRT_TestExtRepo} does not exist."
        exit -1
    fi

    XunitTestBinBase=${CoreRT_TestExtRepo}
    CLRCustomTestLauncher=${CoreRT_TestRoot}/CoreCLR/build-and-run-test.sh

    pushd ${CoreRT_TestRoot}/CoreCLR

    export CoreRT_TestRoot
    export CoreRT_EnableCoreDumps
    export CLRCustomTestLauncher

    CoreRT_TestSelectionArg=
    if [ "$SelectedTests" = "top200" ]; then
        CoreRT_TestSelectionArg="-test_filter_path ${CoreRT_TestRoot}/Top200.CoreCLR.issues.targets"
    elif [ "$SelectedTests" = "interop" ]; then
        CoreRT_TestSelectionArg="-test_filter_path ${CoreRT_TestRoot}/Interop.CoreCLR.issues.targets"
    elif [ "$SelectedTests" = "knowngood" ]; then
        # Todo: Build the list of tests that pass
        CoreRT_TestSelectionArg=
    elif [ "$SelectedTests" = "all" ]; then
        CoreRT_TestSelectionArg=
    fi

    echo python runtest.py -test_native_bin_location ${CoreRT_TestExtRepo}/native/tests -test_location ${CoreRT_TestRoot}/CoreCLR -core_root ${CoreRT_TestExtRepo}/Tests/Core_Root -coreclr_repo_location ${CoreRT_TestRoot}/.. ${CoreRT_TestSelectionArg}    
    python runtest.py -test_native_bin_location ${CoreRT_TestExtRepo}/native/tests -test_location ${CoreRT_TestRoot}/CoreCLR -core_root ${CoreRT_TestExtRepo}/Tests/Core_Root -coreclr_repo_location ${CoreRT_TestRoot}/.. ${CoreRT_TestSelectionArg}
}

run_corefx_tests()
{
    CoreRT_TestExtRepo_CoreFX=${CoreRT_TestRoot}/../tests_downloaded/CoreFX
    CoreRT_TestingUtilitiesOutputDir=${CoreRT_TestExtRepo_CoreFX}/../CoreFXUtilities

    export CoreRT_TestRoot
    export CoreRT_EnableCoreDumps
    
    export CoreRT_TestExtRepo_CoreFX
    export CoreRT_TestingUtilitiesOutputDir
    export CoreRT_CliBinDir

    export CoreRT_SingleThreaded

    if [ ! -d "${CoreRT_TestExtRepo_CoreFX}" ]; then
        mkdir -p ${CoreRT_TestExtRepo_CoreFX}
    fi

    # Set paths to helpers
    CoreRT_TestFileHelperName=CoreFX.TestUtils.TestFileSetup
    CoreRT_TestFileHelperProjectPath="${CoreRT_TestRoot}/CoreFX/runtest/src/TestUtils/TestFileSetup/${CoreRT_TestFileHelperName}.csproj"

    CoreRT_XunitHelperName=CoreFX.TestUtils.XUnit
    CoreRT_XunitHelperProjectPath="${CoreRT_TestRoot}/CoreFX/runtest/src/TestUtils/XUnit/${CoreRT_XunitHelperName}.csproj"    
    
    TEST_LIST_JSON=${CoreRT_TestRoot}/TopN.CoreFX.Unix.issues.json
    case "$(uname -s)" in 
        # Check if we're running under Linux
        Linux)
            TESTS_REMOTE_URL=$(<${CoreRT_TestRoot}/CoreFXTestListURL_Linux.txt)
        ;;
    # Check if we're running under OSX
        Darwin)
            TESTS_REMOTE_URL=$(<${CoreRT_TestRoot}/CoreFXTestListURL_OSX.txt)
        ;;
    # Default to Linux if we don't recognize the OS
        *)
            TESTS_REMOTE_URL=$(<${CoreRT_TestRoot}/CoreFXTestListURL_Linux.txt)
        ;;
    esac


    download_and_unzip_corefx_tests_artifacts ${TESTS_REMOTE_URL} ${TEST_LIST_JSON}
    __exitcode=$?
    if [ ${__exitcode} != 0 ];
    then
        exit ${__exitcode}
    fi

    FXCustomTestLauncher=${CoreRT_TestRoot}/CoreFX/corerun
    XunitTestBinBase=${CoreRT_TestExtRepo_CoreFX}
    XunitLogDir=${CoreRT_TestRoot}/../bin/tests/CoreFX

    # Clean up existing logs
    if [ -d "${XunitLogDir}" ]; then
        rm -rf "${XunitLogDir}"
    fi
    mkdir ${XunitLogDir}

    pushd ${CoreRT_TestRoot}/CoreFX/runtest

    # TODO Add single test/target test support; add exclude tests argument
    ./runtest.sh --testRootDir=${XunitTestBinBase} --logdir=${XunitLogDir} --testLauncher=${FXCustomTestLauncher}
    __exitcode=$?
    if [ ${__exitcode} != 0 ];
    then 
        exit ${__exitcode}
    fi  

    echo ${CoreRT_CliBinDir}/dotnet ${CoreRT_TestingUtilitiesOutputDir}/${CoreRT_XunitHelperName}.dll --logDir ${XunitLogDir} --pattern "*.xml"

    ${CoreRT_CliBinDir}/dotnet ${CoreRT_TestingUtilitiesOutputDir}/${CoreRT_XunitHelperName}.dll --logDir ${XunitLogDir} --pattern "*.xml"
    
}

CoreRT_TestRoot="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
CoreRT_CliBinDir=${CoreRT_TestRoot}/../Tools/dotnetcli
CoreRT_BuildArch=x64
CoreRT_BuildType=Debug
CoreRT_TestRun=true
CoreRT_TestCompileMode=
CoreRT_CrossRootFS=
CoreRT_CrossCXXFlags=
CoreRT_CrossLinkerFlags=
CoreRT_CrossBuild=0
CoreRT_EnableCoreDumps=0
CoreRT_TestName=*
CoreRT_SingleThreaded=

while [ "$1" != "" ]; do
        lowerI="$(echo $1 | awk '{print tolower($0)}')"
        case $lowerI in
        -?|-h|--help)
            usage
            exit 1
            ;;
        wasm)
            CoreRT_BuildArch=wasm
            CoreRT_BuildOS=WebAssembly
            CoreRT_TestCompileMode=wasm
            CoreRT_TestRun=false
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
        armel)
            CoreRT_BuildArch=armel
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
        -mode)
            shift
            CoreRT_TestCompileMode=$1
            ;;
        -test)
            shift
            CoreRT_TestName=$1
            ;;
        -runtest)
            shift
            CoreRT_TestRun=$1
            ;;
        -dotnetclipath) 
            shift
            CoreRT_CliBinDir=$1
            ;;
        -cross)
            shift
            CoreRT_CrossBuild=$1
            ;;
        -coreclr)
            CoreRT_RunCoreCLRTests=true;
            shift
            # Convert the name of the selected test set to lower case
            SelectedTests="$(echo $1 | awk '{print tolower($0)}')"

            if [ -z ${SelectedTests} ]; then
                SelectedTests=top200
            elif [ "${SelectedTests}" != "all" ] && [ "${SelectedTests}" != "top200" ] && [ "${SelectedTests}" != "knowngood" ] && [ "${SelectedTests}" != "interop" ]; then
                echo "Error: Invalid CoreCLR test selection."
                exit -1
            fi
            ;;
        -corefx)
            CoreRT_RunCoreFXTests=true;
            ;;
        -multimodule)
            CoreRT_MultiFileConfiguration=MultiModule;
            ;;
        -singlethread)
            CoreRT_SingleThreaded=1;
            ;;
        -coredumps)
            CoreRT_EnableCoreDumps=1
            ;;
        *)
            ;;
    esac
    shift
done

CoreRT_ExtraCXXFlags=
CoreRT_ExtraLinkFlags=
if [ ${CoreRT_CrossBuild} != 0 ]; then
    CoreRT_TestRun=false
    CoreRT_CrossRootFS=${CoreRT_TestRoot}/../cross/rootfs/${CoreRT_BuildArch}
    # all values are brought from the appropriate toolchain.cmake's
    case $CoreRT_BuildArch in
         arm)
             CoreRT_CrossCXXFlags="-target armv7-linux-gnueabihf -mthumb -mfpu=vfpv3 --sysroot=${CoreRT_CrossRootFS}"
             CoreRT_CrossLinkerFlags="-target arm-linux-gnueabihf -B ${CoreRT_CrossRootFS}/usr/lib/gcc/arm-linux-gnueabihf `
                                     `-L${CorRT_CrossRootFS}/lib/arm-linux-gnueabihf --sysroot=${CoreRT_CrossRootFS}"
         ;;
         arm64)
             CoreRT_CrossCXXFlags="-target aarch64-linux-gnu --sysroot=${CoreRT_CrossRootFS}"
             CoreRT_CrossLinkerFlags="-target aarch64-linux-gnu -B ${CoreRT_CrossRootFS}/usr/lib/gcc/aarch64-linux-gnu `
                                     `-L${CoreRT_CrossRootFS}/lib/aarch64-linux-gnu --sysroot=${CoreRT_CrossRootFS}"
         ;;
         armel)
             CoreRT_CrossCXXFlags="-target armv7-linux-gnueabi -mthumb -mfpu=vfpv3  -mfloat-abi=softfp --sysroot=${CoreRT_CrossRootFS}"
             CoreRT_CrossLinkerFlags="-target arm-linux-gnueabi --sysroot=${CoreRT_CrossRootFS}"
             ID=
             if [ -e $ROOTFS_DIR/etc/os-release ]; then
                 source $ROOTFS_DIR/etc/os-release
             fi
             if [ "$ID" = "tizen" ]; then
                 TIZEN_TOOLCHAIN="armv7l-tizen-linux-gnueabi/6.2.1"
                 CoreRT_CrossCXXFlags="${CoreRT_CrossCXXFlags} -isystem ${CoreRT_CrossRootFS}/usr/lib/gcc/${TIZEN_TOOLCHAIN}/include/c++ `
                                      `-isystem ${CoreRT_CrossRootFS}//usr/lib/gcc/${TIZEN_TOOLCHAIN}/include/c++/armv7l-tizen-linux-gnueabi `
                                      `-isystem ${CoreRT_CrossRootFS}/armel/usr/include"
                 CoreRT_CrossLinkerFlags="${CoreRT_CrossLinkerFlags} -B${CoreRT_CrossRootFS}/usr/lib/gcc/${TIZEN_TOOLCHAIN} `
                                         `-L${CoreRT_CrossRootFS}/usr/lib/gcc/${TIZEN_TOOLCHAIN}"
             else
                 TOOLCHAIN="arm-linux-gnueabi"
                 CoreRT_CrossCXXFlags="${CoreRT_CrossCXXFlags} -isystem ${CoreRT_CrossRootFS}/usr/include/c++/4.9 `
                                      `-isystem ${CoreRT_CrossRootFS}/usr/include/arm-linux-gnueabi/c++/4.9 "
                 CoreRT_CrossLinkerFlags="${CoreRT_CrossLinkerFlags} -B${CoreRT_CrossRootFS}/usr/lib/gcc/${TOOLCHAIN}/4.9 `
                                         `-L${CoreRT_CrossRootFS}/usr/lib/gcc/${TOOLCHAIN}/4.9"
             fi
         ;;
    esac
    CoreRT_ExtraCXXFlags="$CoreRT_ExtraCXXFlags $CoreRT_CrossCXXFlags"
    CoreRT_ExtraLinkFlags="$CoreRT_ExtraLinkFlags $CoreRT_CrossLinkerFlags"
fi

source "$CoreRT_TestRoot/testenv.sh"

__BuildStr=${CoreRT_BuildOS}.${CoreRT_BuildArch}.${CoreRT_BuildType}
__CoreRTTestBinDir=${CoreRT_TestRoot}/../bin/tests
__LogDir=${CoreRT_TestRoot}/../bin/Logs/${__BuildStr}/tests
__build_os_lowcase=$(echo "${CoreRT_BuildOS}" | tr '[:upper:]' '[:lower:]')


if [ "$CoreRT_MultiFileConfiguration" = "MultiModule" ]; then
    CoreRT_TestCompileMode=ryujit
fi

if [ "$CoreRT_TestCompileMode" = "jit" ]; then
    CoreRT_TestCompileMode=ryujit
fi

if [ ! -d $__LogDir ]; then
    mkdir -p $__LogDir
fi

if [ ! -d ${CoreRT_ToolchainDir} ]; then
    echo "Toolchain not found in ${CoreRT_ToolchainDir}"
    exit -1
fi

if [ ${CoreRT_RunCoreCLRTests} ]; then
    run_coreclr_tests
    exit $?
fi

if [ ${CoreRT_RunCoreFXTests} ]; then
    run_corefx_tests 
    exit $?
fi

__CppTotalTests=0
__CppPassedTests=0
__JitTotalTests=0
__JitPassedTests=0
__WasmTotalTests=0
__WasmPassedTests=0

if [ ! -d ${__CoreRTTestBinDir} ]; then
    mkdir -p ${__CoreRTTestBinDir}
fi
echo > ${__CoreRTTestBinDir}/testResults.tmp

__BuildOsLowcase=$(echo "${CoreRT_BuildOS}" | tr '[:upper:]' '[:lower:]')
__TestSearchPath=${CoreRT_TestRoot}/src/Simple/${CoreRT_TestName}
for csproj in $(find ${__TestSearchPath} -name "*.csproj")
do
    if [ -e `dirname ${csproj}`/no_unix ]; then continue; fi
    if [ -e `dirname ${csproj}`/no_linux ] && [ "${CoreRT_HostOS}" != "OSX" ]; then continue; fi

    if [ "${CoreRT_TestCompileMode}" = "ryujit" ] || [ "${CoreRT_TestCompileMode}" = "" ]; then
        if [ ! -e `dirname ${csproj}`/no_ryujit ]; then
            run_test_dir ${csproj} "Jit"
        fi
    fi
    if [ "${CoreRT_TestCompileMode}" = "cpp" ] || [ "${CoreRT_TestCompileMode}" = "" ]; then
        if [ ! -e `dirname ${csproj}`/no_cpp ]; then
            run_test_dir ${csproj} "Cpp" "$CoreRT_ExtraCXXFlags" "$CoreRT_ExtraLinkFlags"
        fi
    fi
    if [ "${CoreRT_TestCompileMode}" = "wasm" ]; then
        if [ -e `dirname ${csproj}`/wasm ]; then
            run_test_dir ${csproj} "Wasm"
        fi
    fi
done

__TotalTests=$((${__JitTotalTests} + ${__CppTotalTests} + ${__WasmTotalTests}))
__PassedTests=$((${__JitPassedTests} + ${__CppPassedTests} + ${__WasmPassedTests}))
__FailedTests=$((${__TotalTests} - ${__PassedTests}))

if [ "$CoreRT_MultiFileConfiguration" = "MultiModule" ]; then
    __TestResultsLog=${__CoreRTTestBinDir}/${CoreRT_MultiFileConfiguration}/testResults.xml
    if [ ! -d ${__CoreRTTestBinDir}/${CoreRT_MultiFileConfiguration} ]; then
        mkdir -p ${__CoreRTTestBinDir}/${CoreRT_MultiFileConfiguration}
    fi
else
    __TestResultsLog=${__CoreRTTestBinDir}/testResults.xml
fi

echo "<assemblies>"  > ${__TestResultsLog}
echo "<assembly name=\"ILCompiler\" total=\"${__TotalTests}\" passed=\"${__PassedTests}\" failed=\"${__FailedTests}\" skipped=\"0\">"  >> ${__TestResultsLog}
echo "<collection total=\"${__TotalTests}\" passed=\"${__PassedTests}\" failed=\"${__FailedTests}\" skipped=\"0\">"  >> ${__TestResultsLog}
cat "${__CoreRTTestBinDir}/testResults.tmp" >> ${__TestResultsLog}
echo "</collection>"  >> ${__TestResultsLog}
echo "</assembly>"  >> ${__TestResultsLog}
echo "</assemblies>"  >> ${__TestResultsLog}


echo "JIT - TOTAL: ${__JitTotalTests} PASSED: ${__JitPassedTests}"
echo "CPP - TOTAL: ${__CppTotalTests} PASSED: ${__CppPassedTests}"
echo "WASM - TOTAL: ${__WasmTotalTests} PASSED: ${__WasmPassedTests}"

if [ ${__JitTotalTests} == 0 ] && [ "${CoreRT_TestCompileMode}" != "wasm" ]; then
    exit 1
fi
if [ ${__CppTotalTests} == 0 ] && [ "${CoreRT_TestCompileMode}" != "wasm" ]; then
    exit 1
fi
if [ ${__WasmTotalTests} == 0 ] && [ "${CoreRT_TestCompileMode}" == "wasm" ]; then
    exit 1
fi 
if [ ${__FailedTests} -gt 0 ]; then
    exit 1
fi
exit 0
