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
			export CoreRT_BuildArch=x86
			;;
		x64)
			export CoreRT_BuildArch=x64
			;;
		arm)
			export CoreRT_BuildArch=arm
			;;
		arm64)
			export CoreRT_BuildArch=arm64
			;;
		dbg)
			export CoreRT_BuildType=Debug
			;;
		debug)
			export CoreRT_BuildType=Debug
			;;
		rel)
			export CoreRT_BuildType=Release
			;;
		release)
			export CoreRT_BuildType=Release
			;;
		*)
			;;
	esac
done
