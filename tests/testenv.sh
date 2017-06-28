#!/usr/bin/env bash

usage()
{
	echo "$0 [arch: x86/x64/arm/arm64] [flavor: debug/release]"
	exit 1
}

for i in "$@"
	do
		lowerI="$(echo $i | awk '{print tolower($0)}')"
		case $lowerI in
		-?|-h|-help)
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
		armel)
			CoreRT_BuildArch=armel
			;;
		arm64)
			CoreRT_BuildArch=arm64
			;;
		dbg)
			CoreRT_BuildType=Debug
			;;
		debug)
			CoreRT_BuildType=Debug
			;;
		rel)
			CoreRT_BuildType=Release
			;;
		release)
			CoreRT_BuildType=Release
			;;
		*)
			;;
	esac
done

if [ -z ${CoreRT_BuildArch} ]; then
    echo "Set CoreRT_BuildArch to x86/x64/arm/arm64"
    exit -1
fi

if [ -z ${CoreRT_BuildType} ]; then
    echo "Set CoreRT_BuildType to Debug or Release"
    exit -1
fi

# Use uname to determine what the OS is.
OSName=$(uname -s)
case $OSName in
    Darwin)
        CoreRT_BuildOS=OSX
        ;;

    FreeBSD)
        CoreRT_BuildOS=FreeBSD
        ;;

    Linux)
        CoreRT_BuildOS=Linux
        ;;

    NetBSD)
        CoreRT_BuildOS=NetBSD
        ;;

    *)
        echo "Unsupported OS $OSName detected, configuring as if for Linux"
        CoreRT_BuildOS=Linux
        ;;
esac

export CoreRT_BuildArch
export CoreRT_BuildType
export CoreRT_BuildOS

__ScriptDir=$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)

export CoreRT_ToolchainDir=${__ScriptDir}/../bin/${CoreRT_BuildOS}.${CoreRT_BuildArch}.${CoreRT_BuildType}
