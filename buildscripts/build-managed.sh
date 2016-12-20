#!/usr/bin/env bash

scriptRoot="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"

if [ "$BUILDVARS_DONE" != 1 ]; then
    . $scriptRoot/buildvars-setup.sh $*
fi

# Prepare the system for building

prepare_managed_build()
{
    # Run Init-Tools to restore BuildTools and ToolRuntime
    $__ProjectRoot/init-tools.sh

    # Tell nuget to always use repo-local nuget package cache. The "dotnet restore" invocations use the --packages
    # argument, but there are a few commands in publish and tests that do not.
    export NUGET_PACKAGES=$__packageroot

    echo "Using CLI tools version:"
    ls "$__dotnetclipath/sdk"
}


build_managed_corert()
{
    __buildproj=$__ProjectRoot/build.proj
    __buildlog=$__ProjectRoot/msbuild.$__BuildArch.log

    if [ -z "${ToolchainMilestone}" ]; then
        ToolchainMilestone=testing
    fi

    $__ProjectRoot/Tools/msbuild.sh "$__buildproj" /m /nologo /verbosity:minimal "/fileloggerparameters:Verbosity=normal;LogFile=$__buildlog" /t:Build /p:RepoPath=$__ProjectRoot /p:RepoLocalBuild="true" /p:RelativeProductBinDir=$__RelativeProductBinDir /p:CleanedTheBuild=$__CleanBuild /p:NuPkgRid=$__NugetRuntimeId /p:TestNugetRuntimeId=$__NugetRuntimeId /p:OSGroup=$__BuildOS /p:Configuration=$__BuildType /p:Platform=$__BuildArch /p:COMPUTERNAME=$(hostname) /p:USERNAME=$(id -un) /p:ToolchainMilestone=${ToolchainMilestone} $__UnprocessedBuildArgs $__ExtraMsBuildArgs
    export BUILDERRORLEVEL=$?

    echo

    # Pull the build summary from the log file
    tail -n 4 "$__buildlog"
    echo Build Exit Code = $BUILDERRORLEVEL
    if [ $BUILDERRORLEVEL != 0 ]; then
        exit $BUILDERRORLEVEL
    fi
}


if $__buildmanaged; then

    # Prepare the system

    prepare_managed_build

    # Build the corert native components.

    build_managed_corert

    # Build complete
fi