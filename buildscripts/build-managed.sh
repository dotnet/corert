#!/usr/bin/env bash

scriptRoot="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"

if [ "$BUILDVARS_DONE" != 1 ]; then
    . $scriptRoot/buildvars-setup.sh $*
fi

export __BuildArch

# Prepare the system for building

prepare_managed_build()
{
    # Run Init-Tools to restore BuildTools and ToolRuntime
    $__ProjectRoot/init-tools.sh

    echo "Using CLI tools version:"
    ls "$__dotnetclipath/sdk"
}

build_managed_corert()
{
    __buildproj=$__ProjectRoot/build.proj
    __buildlog=$__ProjectRoot/msbuild.$__BuildArch.log

    __buildarch="$__BuildArch"
    if [ "$__buildarch" = "armel" ]; then
        __buildarch=arm
        __ExtraMsBuildArgs="$__ExtraMsBuildArgs /p:BinDirPlatform=armel"
    fi

    $__dotnetclipath/dotnet msbuild "$__buildproj" /m /nologo /verbosity:minimal "/fileloggerparameters:Verbosity=normal;LogFile=$__buildlog" /t:Restore /p:RepoPath=$__ProjectRoot /p:RepoLocalBuild="true" /p:NuPkgRid=$__NugetRuntimeId /p:OSGroup=$__BuildOS /p:Configuration=$__BuildType /p:Platform=$__buildarch /p:COMPUTERNAME=$(hostname) /p:USERNAME=$(id -un) $__UnprocessedBuildArgs $__ExtraMsBuildArgs
    export BUILDERRORLEVEL=$?

    echo

    # Pull the build summary from the log file
    tail -n 4 "$__buildlog"
    echo Build Exit Code = $BUILDERRORLEVEL
    if [ $BUILDERRORLEVEL != 0 ]; then
        exit $BUILDERRORLEVEL
    fi

    # Buildtools tooling is not capable of publishing netcoreapp currently. Use helper projects to publish skeleton of
    # the standalone app that the build injects actual binaries into later.
    $__dotnetclipath/dotnet restore $__sourceroot/ILCompiler/netcoreapp/ilc.csproj -r $__NugetRuntimeId
    export BUILDERRORLEVEL=$?
    if [ $BUILDERRORLEVEL != 0 ]; then
        exit $BUILDERRORLEVEL
    fi
    $__dotnetclipath/dotnet publish $__sourceroot/ILCompiler/netcoreapp/ilc.csproj -r $__NugetRuntimeId -o $__ProductBinDir/tools
    export BUILDERRORLEVEL=$?
    if [ $BUILDERRORLEVEL != 0 ]; then
        exit $BUILDERRORLEVEL
    fi
    chmod +x $__ProductBinDir/tools/ilc

    $__ProjectRoot/Tools/msbuild.sh "$__buildproj" /m /nologo /verbosity:minimal "/fileloggerparameters:Verbosity=normal;LogFile=$__buildlog" /t:Build /p:RepoPath=$__ProjectRoot /p:RepoLocalBuild="true" /p:NuPkgRid=$__NugetRuntimeId /p:OSGroup=$__BuildOS /p:Configuration=$__BuildType /p:Platform=$__buildarch /p:COMPUTERNAME=$(hostname) /p:USERNAME=$(id -un) $__UnprocessedBuildArgs $__ExtraMsBuildArgs
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
        ID=
        case $__BuildArch in
            arm)
            ;;
            arm64)
            ;;
            armel)
                if [ -e $ROOTFS_DIR/etc/os-release ]; then
                    source $ROOTFS_DIR/etc/os-release
                fi
                ;;
        esac

        # only tizen case now
        if [ "$ID" != "tizen" ]; then
            return 0
        fi
        __tizenToolsRoot=${__ProjectRoot}/Tools/tizen
        __corefxsite="https://ci.dot.net/job/dotnet_corefx/job/master/job/"
        __coreclrsite="https://ci.dot.net/job/dotnet_coreclr/job/master/job/"
        __buildArchiveName="build.tar.gz"
        __systemNativeLibName="System.Native.a"
        __systemGlobNativeLibName="System.Globalization.Native.a"
        if [ $__BuildType = "Debug" ]; then
            __buildtype="debug"
        else
            __buildtype="release"
        fi
        __corefxsource="tizen_armel_cross_${__buildtype}/lastSuccessfulBuild/artifact/bin/${__buildArchiveName}"
        __coreclrsource="armel_cross_${__buildtype}_tizen/lastSuccessfulBuild/artifact/bin/Product/Linux.armel.${__BuildType}/${__systemGlobNativeLibName}"
        mkdir -p $__tizenToolsRoot

        (cd ${__tizenToolsRoot} && wget -t0 -N "${__corefxsite}${__corefxsource}")
        export BUILDERRORLEVEL=$?
        if [ $BUILDERRORLEVEL != 0 ]; then
            exit $BUILDERRORLEVEL
        fi
        tar xvf ${__tizenToolsRoot}/${__buildArchiveName} -C ${__tizenToolsRoot} ./${__systemNativeLibName}
        cp ${__tizenToolsRoot}/${__systemNativeLibName} $__ProjectRoot/bin/Linux.${__BuildArch}.${__BuildType}/framework

        (cd ${__tizenToolsRoot} && wget -t0 -N "${__coreclrsite}${__coreclrsource}")
        export BUILDERRORLEVEL=$?
        if [ $BUILDERRORLEVEL != 0 ]; then
            exit $BUILDERRORLEVEL
        fi
        cp ${__tizenToolsRoot}/${__systemGlobNativeLibName} $__ProjectRoot/bin/Linux.${__BuildArch}.${__BuildType}/framework
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
