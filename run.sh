#!/usr/bin/env bash

__scriptpath=$(cd "$(dirname "$0")"; pwd -P)


__scriptpath=$(cd "$(dirname "$0")"; pwd -P)
__toolRuntime=$__scriptpath/Tools
__bootstrapUrl="https://raw.githubusercontent.com/dotnet/buildtools/master/bootstrap/bootstrap.sh?$(date +%s)"
__bootstrapDest=$__toolRuntime/bootstrap.sh
__cliVersion=`cat $__scriptpath/.cliversion`
repoRoot="$( cd -P "$( dirname "$SOURCE" )" && pwd )"

# args:
# remote_path - $1
# [out_path] - $2 - stdout if not provided
download() {
    local remote_path=$1
    local out_path=${2:-}

    local failed=false
    if [ -z "$out_path" ]; then
        curl --retry 10 -sSL --create-dirs $remote_path || failed=true
    else
        curl --retry 10 -sSL --create-dirs -o $out_path $remote_path || failed=true
    fi
    
    if [ "$failed" = true ]; then
        echo "run-build: Error: Download failed" >&2
        return 1
    fi
}

export DOTNET_SKIP_FIRST_TIME_EXPERIENCE=1
if [ ! -d $__toolRuntime ]; then
    mkdir -p $__toolRuntime
fi
if [ ! -f $__bootstrapDest ]; then
    download "$__bootstrapUrl" "$__bootstrapDest"
    chmod u+x $__bootstrapDest
fi

$__bootstrapDest --repositoryRoot $repoRoot

if [ $? != 0 ]; then
    echo "run: Error: Boot-strapping failed with exit code $?, see bootstrap.log for more information." >&2
    exit $?
fi

__toolRuntime=$__scriptpath/Tools
__dotnet=$__toolRuntime/dotnetcli/dotnet

$__dotnet $__toolRuntime/run.exe $*
exit $?
