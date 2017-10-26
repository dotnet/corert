#!/usr/bin/env bash

# Use uname to determine what the CPU is.
export CPUName=$(uname -p)
# Some Linux platforms report unknown for platform, but the arch for machine.
if [ $CPUName == "unknown" ]; then
    export CPUName=$(uname -m)
fi

case $CPUName in
    i686)
        export __HostArch=x86
        ;;

    x86_64)
        export __HostArch=x64
        ;;

    armv7l)
        echo "Unsupported CPU $CPUName detected, build might not succeed!"
        export __HostArch=arm
        ;;

    aarch64)
        echo "Unsupported CPU $CPUName detected, build might not succeed!"
        export __HostArch=arm64
        ;;

    *)
        echo "Unknown CPU $CPUName detected, configuring as if for x64"
        export __HostArch=x64
        ;;
esac
