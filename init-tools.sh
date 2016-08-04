#!/usr/bin/env bash

__scriptpath=$(cd "$(dirname "$0")"; pwd -P)
__init_tools_log=$__scriptpath/init-tools.log
__PACKAGES_DIR=$__scriptpath/packages
__TOOLRUNTIME_DIR=$__scriptpath/Tools
__DOTNET_PATH=$__TOOLRUNTIME_DIR/dotnetcli
__DOTNET_CMD=$__DOTNET_PATH/dotnet
if [ -z "$__BUILDTOOLS_SOURCE" ]; then __BUILDTOOLS_SOURCE=https://dotnet.myget.org/F/dotnet-buildtools/api/v3/index.json; fi
__BUILD_TOOLS_PACKAGE_VERSION=$(cat $__scriptpath/BuildToolsVersion.txt)
__DOTNET_TOOLS_VERSION=$(cat $__scriptpath/DotnetCLIVersion.txt)
__BUILD_TOOLS_PATH=$__PACKAGES_DIR/Microsoft.DotNet.BuildTools/$__BUILD_TOOLS_PACKAGE_VERSION/lib
__PROJECT_JSON_PATH=$__TOOLRUNTIME_DIR/$__BUILD_TOOLS_PACKAGE_VERSION
__PROJECT_JSON_FILE=$__PROJECT_JSON_PATH/project.json
__PROJECT_JSON_CONTENTS="{ \"dependencies\": { \"Microsoft.DotNet.BuildTools\": \"$__BUILD_TOOLS_PACKAGE_VERSION\" }, \"frameworks\": { \"dnxcore50\": { } } }"
__INIT_TOOLS_DONE_MARKER=$__PROJECT_JSON_PATH/done

OSName=$(uname -s)
case $OSName in
    Darwin)
        OS=OSX
        __DOTNET_PKG=dotnet-dev-osx-x64
        ulimit -n 2048
        ;;

    Linux)
        OS=Linux
        source /etc/os-release
        if [ "$ID" == "centos" -o "$ID" == "rhel" ]; then
            __DOTNET_PKG=dotnet-dev-centos-x64
        elif [ "$ID" == "ubuntu" -o "$ID" == "debian" ]; then
            __DOTNET_PKG=dotnet-dev-ubuntu-x64
        else
            echo "Unsupported Linux distribution '$ID' detected. Downloading ubuntu-x64 tools."
            __DOTNET_PKG=dotnet-dev-ubuntu-x64
        fi
        ;;

    *)
        echo "Unsupported OS '$OSName' detected. Downloading ubuntu-x64 tools."
        OS=Linux
        __DOTNET_PKG=dotnet-dev-ubuntu-x64
        ;;
esac

if [ ! -e $__INIT_TOOLS_DONE_MARKER ]; then
    if [ -e $__TOOLRUNTIME_DIR ]; then rm -rf -- $__TOOLRUNTIME_DIR; fi
    echo "Running: $__scriptpath/init-tools.sh" > $__init_tools_log
    if [ ! -e $__DOTNET_PATH ]; then
        echo "Installing dotnet cli..."
        __DOTNET_LOCATION="https://dotnetcli.blob.core.windows.net/dotnet/beta/Binaries/${__DOTNET_TOOLS_VERSION}/${__DOTNET_PKG}.${__DOTNET_TOOLS_VERSION}.tar.gz"
        # curl has HTTPS CA trust-issues less often than wget, so lets try that first.
        echo "Installing '${__DOTNET_LOCATION}' to '$__DOTNET_PATH/dotnet.tar'" >> $__init_tools_log
        which curl > /dev/null 2> /dev/null
        if [ $? -ne 0 ]; then
            mkdir -p "$__DOTNET_PATH"
            wget -q -O $__DOTNET_PATH/dotnet.tar ${__DOTNET_LOCATION}
        else
            curl --retry 10 -sSL --create-dirs -o $__DOTNET_PATH/dotnet.tar ${__DOTNET_LOCATION}
        fi
        cd $__DOTNET_PATH
        tar -xf $__DOTNET_PATH/dotnet.tar
        if [ -n "$BUILDTOOLS_OVERRIDE_RUNTIME" ]; then
            find $__DOTNET_PATH -name *.ni.* | xargs rm 2>/dev/null
            cp -R $BUILDTOOLS_OVERRIDE_RUNTIME/* $__DOTNET_PATH/bin
            cp -R $BUILDTOOLS_OVERRIDE_RUNTIME/* $__DOTNET_PATH/bin/dnx
            cp -R $BUILDTOOLS_OVERRIDE_RUNTIME/* $__DOTNET_PATH/runtime/coreclr
        fi

        cd $__scriptpath
    fi

    if [ ! -d "$__PROJECT_JSON_PATH" ]; then mkdir "$__PROJECT_JSON_PATH"; fi
    echo $__PROJECT_JSON_CONTENTS > "$__PROJECT_JSON_FILE"

    if [ ! -e $__BUILD_TOOLS_PATH ]; then
        echo "Restoring BuildTools version $__BUILD_TOOLS_PACKAGE_VERSION..."
        echo "Running: $__DOTNET_CMD restore \"$__PROJECT_JSON_FILE\" --no-cache --packages $__PACKAGES_DIR --source $__BUILDTOOLS_SOURCE" >> $__init_tools_log
        $__DOTNET_CMD restore "$__PROJECT_JSON_FILE" --no-cache --packages $__PACKAGES_DIR --source $__BUILDTOOLS_SOURCE >> $__init_tools_log
        if [ ! -e "$__BUILD_TOOLS_PATH/init-tools.sh" ]; then echo "ERROR: Could not restore build tools correctly. See '$__init_tools_log' for more details."; fi
    fi

    echo "Initializing BuildTools..."
    echo "Running: $__BUILD_TOOLS_PATH/init-tools.sh $__scriptpath $__DOTNET_CMD $__TOOLRUNTIME_DIR" >> $__init_tools_log
    $__BUILD_TOOLS_PATH/init-tools.sh $__scriptpath $__DOTNET_CMD $__TOOLRUNTIME_DIR >> $__init_tools_log

    # Override Roslyn with newer version. Ideally, we would pick up the compiler update via buildtools update. But new buildtools 
    # require new CLI as well that we cannot pick up right now because of it is missing the native compilation driver.

    __ROSLYN_VERSION_OVERRIDE=2.0.0-beta3

    __ROSLYN_JSON_FILE=$__TOOLRUNTIME_DIR/roslyn.project.json
    __ROSLYN_JSON_CONTENTS="{ \"dependencies\": { \"Microsoft.Net.Compilers.netcore\": \"$__ROSLYN_VERSION_OVERRIDE\" }, \"frameworks\": { \"netcoreapp1.0\": { } } }"
    echo $__ROSLYN_JSON_CONTENTS > "$__ROSLYN_JSON_FILE"

    __ROSLYN_SOURCE=https://api.nuget.org/v3/index.json

    echo "Restoring Microsoft.Net.Compilers version $__ROSLYN_VERSION_OVERRIDE..."
    echo "Running: $__DOTNET_CMD restore \"$__ROSLYN_JSON_FILE\" --no-cache --packages $__PACKAGES_DIR --source $__ROSLYN_SOURCE" >> $__init_tools_log
    $__DOTNET_CMD restore "$__ROSLYN_JSON_FILE" --no-cache --packages $__PACKAGES_DIR --source $__ROSLYN_SOURCE >> $__init_tools_log
    if [ ! -e "$__PACKAGES_DIR/Microsoft.Net.Compilers.netcore/$__ROSLYN_VERSION_OVERRIDE/runtimes/any/native" ]; then echo "ERROR: Could not restore Microsoft.Net.Compilers correctly. See '$__init_tools_log' for more details."; fi

    cp "$__PACKAGES_DIR/Microsoft.Net.Compilers.netcore/$__ROSLYN_VERSION_OVERRIDE/runtimes/any/native/csc.exe" $__TOOLRUNTIME_DIR
    cp "$__PACKAGES_DIR/Microsoft.CodeAnalysis.Common/$__ROSLYN_VERSION_OVERRIDE/lib/netstandard1.3/Microsoft.CodeAnalysis.dll" $__TOOLRUNTIME_DIR
    cp "$__PACKAGES_DIR/Microsoft.CodeAnalysis.CSharp/$__ROSLYN_VERSION_OVERRIDE/lib/netstandard1.3/Microsoft.CodeAnalysis.CSharp.dll" $__TOOLRUNTIME_DIR
    cp "$__PACKAGES_DIR/Microsoft.CodeAnalysis.VisualBasic/$__ROSLYN_VERSION_OVERRIDE/lib/netstandard1.3/Microsoft.CodeAnalysis.VisualBasic.dll" $__TOOLRUNTIME_DIR
    cp "$__PACKAGES_DIR/System.Reflection.Metadata/1.3.0/lib/netstandard1.1/System.Reflection.Metadata.dll" $__TOOLRUNTIME_DIR
    cp "$__PACKAGES_DIR/System.Collections.Immutable/1.2.0/lib/netstandard1.0/System.Collections.Immutable.dll" $__TOOLRUNTIME_DIR

    touch $__INIT_TOOLS_DONE_MARKER
    echo "Done initializing tools."
else
    echo "Tools are already initialized"
fi
