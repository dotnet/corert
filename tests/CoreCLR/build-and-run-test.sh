#!/usr/bin/env bash

# This is the Unix equivalent of build-and-run-test.cmd
# It is invoked by each test's bash script. The reason it's called corerun is that
# the unix CoreCLR tests don't have a custom runner override environment variable.
# See issue https://github.com/dotnet/runtime/issues/7252

export TestExecutable=$2
export TestFileName=${TestExecutable%.exe}

echo "corerun. TestExecutable=${TestExecutable}. Test Filename: ${TestFileName}"
source "$CoreRT_TestRoot/coredump_handling.sh"

# Cleanup prvious test run artifacts
rm -r native 2>/dev/null

if [[ $CoreRT_EnableCoreDumps == 1 ]]; then
    set_up_core_dump_generation
fi

cp $CoreRT_TestRoot/CoreCLR/Test.csproj .

__msbuild_dir=${CoreRT_TestRoot}/../Tools
echo ${__msbuild_dir}/msbuild.sh /m /p:IlcPath=${CoreRT_ToolchainDir} /p:Configuration=${CoreRT_BuildType} Test.csproj
${__msbuild_dir}/msbuild.sh /m /p:IlcPath=${CoreRT_ToolchainDir} /p:Configuration=${CoreRT_BuildType} Test.csproj /v:d

if [[ $CoreRT_EnableCoreDumps == 1 ]]; then
    # Handle any core files generated when running the test IL through the toolchain.
    inspect_and_delete_core_files $CoreRT_ToolchainDir/corerun "$CoreRT_ToolchainDir"
fi

# Remove the test directory and executable from the arg list so it isn't passed to test execution
shift
shift

testExtRepo=$( dirname ${CoreRT_TestRoot} )/tests_downloaded/CoreCLR/
nativeArtifactRepo=${testExtRepo}native/
dirSuffix=$(dirname ${PWD#$testExtRepo})/
nativeDir=${nativeArtifactRepo}tests/src/${dirSuffix}

# In OSX we copy the native component to the directory where the exectuable resides.
# However, in Linux dlopen doesn't seem to look for current directory to resolve the dynamic library.
# So instead we point LD_LIBRARY_PATH to the directory where the native component is.
if [ -e ${nativeDir} ]; then
    if [ "${CoreRT_BuildOS}" == "OSX" ]; then
        echo "Copying native component from :"${nativeDir}
        cp ${nativeDir}*.dylib  native/ 2>/dev/null
    fi
    if [ "${CoreRT_BuildOS}" == "Linux" ]; then
        LD_LIBRARY_PATH=${LD_LIBRARY_PATH}:${nativeDir}
        export LD_LIBRARY_PATH
    fi
fi

if [[ ! -f native/${TestFileName} ]]; then
    echo "ERROR: Native binary not found. Unable to run test."
    exit -1
fi

pushd native/
./${TestFileName} "$@"
testScriptExitCode=$?
popd

if [[ $CoreRT_EnableCoreDumps == 1 ]]; then
    # Handle any core files generated when running the test.
    inspect_and_delete_core_files native/$TestFileName "$CoreRT_ToolchainDir"
fi

# Clean up test binary artifacts to save space
rm -r native 2>/dev/null

exit $testScriptExitCode

