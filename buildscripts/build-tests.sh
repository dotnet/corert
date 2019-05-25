#!/usr/bin/env bash

scriptRoot="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"

if [ "$__SkipTests" == "true" ]; then
    exit 0
fi

if [ "$BUILDVARS_DONE" != 1 ]; then
    . $scriptRoot/buildvars-setup.sh $*
fi

# Restore CoreCLR for ReadyToRun testing
${__dotnetclipath}/dotnet msbuild ${__ProjectRoot}/tests/dirs.proj /nologo /t:Restore "/flp:v=normal;LogFile=$__TestBuildLog" /p:NuPkgRid=$__NugetRuntimeId /maxcpucount /p:OSGroup=$__BuildOS /p:Configuration=$__BuildType /p:Platform=$__BuildArch $__ExtraMsBuildArgs

echo Commencing build of test components for $__BuildOS.$__BuildArch.$__BuildType
echo


${__dotnetclipath}/dotnet msbuild ${__ProjectRoot}/tests/dirs.proj /nologo "/flp:v=normal;LogFile=$__TestBuildLog" /p:NuPkgRid=$__NugetRuntimeId /maxcpucount /p:OSGroup=$__BuildOS /p:Configuration=$__BuildType /p:Platform=$__BuildArch $__ExtraMsBuildArgs "/p:RepoPath=$__ProjectRoot"  "/p:RepoLocalBuild=true" /nodeReuse:false
export BUILDERRORLEVEL=$?
if [ $BUILDERRORLEVEL != 0 ]; then
    echo Test build failed with exit code $BUILDERRORLEVEL. Refer to $__TestBuildLog for details.
    exit $BUILDERRORLEVEL
fi

# No run. Only build >:|
if [ -n "$__BuildTests" ]; then
    exit 0
fi

pushd ${__ProjectRoot}/tests
source ${__ProjectRoot}/tests/runtest.sh $__BuildOS $__BuildArch $__BuildType -cross $__CrossBuild -dotnetclipath $__dotnetclipath
TESTERRORLEVEL=$?
popd
if [ $TESTERRORLEVEL != 0 ]; then
    exit $TESTERRORLEVEL
fi
