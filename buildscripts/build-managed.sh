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

    __buildarch="$__BuildArch"
    if [ "$__buildarch" = "armel" ]; then
        __buildarch=arm
        __ExtraMsBuildArgs="$__ExtraMsBuildArgs /p:BinDirPlatform=armel"
    fi

    $__ProjectRoot/Tools/msbuild.sh "$__buildproj" /m /nologo /verbosity:minimal "/fileloggerparameters:Verbosity=normal;LogFile=$__buildlog" /t:Build /p:RepoPath=$__ProjectRoot /p:RepoLocalBuild="true" /p:RelativeProductBinDir=$__RelativeProductBinDir /p:CleanedTheBuild=$__CleanBuild /p:NuPkgRid=$__NugetRuntimeId /p:TestNugetRuntimeId=$__NugetRuntimeId /p:OSGroup=$__BuildOS /p:Configuration=$__BuildType /p:Platform=$__buildarch /p:COMPUTERNAME=$(hostname) /p:USERNAME=$(id -un) /p:ToolchainMilestone=${ToolchainMilestone} $__UnprocessedBuildArgs $__ExtraMsBuildArgs
    export BUILDERRORLEVEL=$?

    echo

    # Pull the build summary from the log file
    tail -n 4 "$__buildlog"
    echo Build Exit Code = $BUILDERRORLEVEL
    if [ $BUILDERRORLEVEL != 0 ]; then
        exit $BUILDERRORLEVEL
    fi
}

# TODO It's a temporary decision because of there are no armel tizen nuget packages getting published today

get_official_cross_builds()
{
    if [ $__CrossBuild == 1 ]; then
        __corefxsite="https://ci.dot.net/job/dotnet_corefx/job/master/view/Official%20Builds/job/"
        __coreclrsite="https://ci.dot.net/job/dotnet_coreclr/job/master/view/Official%20Builds/job/"
        __corefxsource=
        __coreclrsource=
        __buildtype=
        if [ $__BuildType = "Debug" ]; then
            __buildtype="debug"
        else
            __buildtype="release"
        fi
        case $__BuildArch in
            arm)
            ;;
            arm64)
            ;;
            armel)
                ID=
                if [ -e $ROOTFS_DIR/etc/os-release ]; then
                    source $ROOTFS_DIR/etc/os-release
                fi
                if [ "$ID" = "tizen" ]; then
                   __corefxsource="tizen_armel_cross_${__buildtype}/lastSuccessfulBuild/artifact/bin/build.tar.gz"
                   __coreclrsource="armel_cross_${__buildtype}_tizen/lastSuccessfulBuild/artifact/bin/Product/Linux.armel.${__BuildType}/libSystem.Globalization.Native.a"
                fi
                ;;
        esac
        if [ -n "${__corefxsource}" ]; then
            wget "${__corefxsite}${__corefxsource}"
            export BUILDERRORLEVEL=$?
            if [ $BUILDERRORLEVEL != 0 ]; then
                exit $BUILDERRORLEVEL
            fi
            tar xvf ./build.tar.gz ./System.Native.a
            mv ./System.Native.a $__ProjectRoot/bin/Product/Linux.${__BuildArch}.${__BuildType}/packaging/publish1/framework
            rm -rf ./build.tar.gz
        fi
        if [ -n ${__coreclrsource} ]; then
            wget "${__coreclrsite}${__coreclrsource}"
            export BUILDERRORLEVEL=$?
            if [ $BUILDERRORLEVEL != 0 ]; then
                exit $BUILDERRORLEVEL
            fi
            mv ./libSystem.Globalization.Native.a $__ProjectRoot/bin/Product/Linux.${__BuildArch}.${__BuildType}/packaging/publish1/framework
        fi
     fi
}

if $__buildmanaged; then

    # Prepare the system

    prepare_managed_build

    # Build the corert native components.

    build_managed_corert

    # Get cross builds from official sites

    get_official_cross_builds

    # Build complete
fi
