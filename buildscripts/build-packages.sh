#!/usr/bin/env bash

scriptRoot="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"

if [ "$BUILDVARS_DONE" != 1 ]; then
    . $scriptRoot/buildvars-setup.sh $*
fi

$__ProjectRoot/Tools/msbuild.sh "$__ProjectRoot/pkg/packages.proj" /m /nologo "/flp:v=diag;LogFile=build-packages.log" /p:RepoPath="$__ProjectRoot" /p:NuPkgRid=$__NugetRuntimeId /p:OSGroup=$__BuildOS /p:Configuration=$__BuildType /p:Platform=$__BuildArch /p:COMPUTERNAME=$(hostname) /p:USERNAME=$(id -un) $__UnprocessedBuildArgs $__ExtraMsBuildArgs
export BUILDERRORLEVEL=$?
exit $BUILDERRORLEVEL