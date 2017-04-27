#!/usr/bin/env bash

usage()
{
    echo "Usage: $0 [BuildArch]"
    echo "BuildArch can be: arm, arm64"

    exit 1
}

__CrossDir=$( cd "$( dirname "${BASH_SOURCE[0]}" )" && pwd )
__InitialDir=$PWD
__BuildArch=arm
__UbuntuArch=armhf
__UbuntuRepo="http://ports.ubuntu.com/"
__UbuntuPackages="build-essential lldb-3.6-dev libunwind8-dev gettext symlinks liblttng-ust-dev libicu-dev"
__LLDB_Package="lldb-3.6-dev"
__MachineTriple=arm-linux-gnueabihf
__UnprocessedBuildArgs=
__SkipUnmount=0

for i in "$@"
    do
        lowerI="$(echo $i | awk '{print tolower($0)}')"
        case $lowerI in
        -?|-h|--help)
        usage
        exit 1
        ;;
        arm)
        __BuildArch=arm
        __UbuntuArch=armhf
        __UbuntuRepo="http://ports.ubuntu.com/"
        __UbuntuPackages="build-essential lldb-3.6-dev libunwind8-dev gettext symlinks liblttng-ust-dev libicu-dev"
        __MachineTriple=arm-linux-gnueabihf
        ;;
        arm64)
        __BuildArch=arm64
        __UbuntuArch=arm64
        __UbuntuRepo="http://ports.ubuntu.com/"
        __UbuntuPackages="build-essential libunwind8-dev gettext symlinks liblttng-ust-dev libicu-dev"
        __MachineTriple=aarch64-linux-gnu
        ;;
        armel)
            __BuildArch=armel
            __UbuntuArch=armel
            __UbuntuRepo="http://ftp.debian.org/debian/"
            __LinuxCodeName=jessie
            ;;
        lldb3.6)
            __LLDB_Package="lldb-3.6-dev"
            ;;
        lldb3.8)
            __LLDB_Package="lldb-3.8-dev"
            ;;
        vivid)
            if [ "$__LinuxCodeName" != "jessie" ]; then
                __LinuxCodeName=vivid
            fi
            ;;
        wily)
            if [ "$__LinuxCodeName" != "jessie" ]; then
                __LinuxCodeName=wily
            fi
            ;;
        xenial)
            if [ "$__LinuxCodeName" != "jessie" ]; then
                __LinuxCodeName=xenial
            fi
            ;;
        jessie)
            __LinuxCodeName=jessie
            __UbuntuRepo="http://ftp.debian.org/debian/"
            ;;
        tizen)
            if [ "$__BuildArch" != "armel" ]; then
                echo "Tizen is available only for armel."
                usage;
                exit 1;
            fi
            __LinuxCodeName=
            __UbuntuRepo=
            __Tizen=tizen
            ;;
        --skipunmount)
            __SkipUnmount=1
            ;;
        *)
        __UnprocessedBuildArgs="$__UnprocessedBuildArgs $i"
    esac
done

if [ "$__BuildArch" == "armel" ]; then
    __LLDB_Package="lldb-3.5-dev"
fi

__RootfsDir="$__CrossDir/rootfs/$__BuildArch"
__UbuntuPackages="$__UbuntuPackagesBase $__LLDB_Package"


if [[ -n "$ROOTFS_DIR" ]]; then
    __RootfsDir=$ROOTFS_DIR
fi

if [ -d "$__RootfsDir" ]; then
    if [ $__SkipUnmount == 0 ]; then
        umount $__RootfsDir/*
    fi
    rm -rf $__RootfsDir
fi

if [ "$__Tizen" == "tizen" ]; then
    ROOTFS_DIR=$__RootfsDir $__CrossDir/$__BuildArch/tizen-build-rootfs.sh
else
    qemu-debootstrap --arch $__UbuntuArch trusty $__RootfsDir $__UbuntuRepo
    cp $__CrossDir/$__BuildArch/sources.list $__RootfsDir/etc/apt/sources.list
    chroot $__RootfsDir apt-get update
    chroot $__RootfsDir apt-get -y install $__UbuntuPackages
    chroot $__RootfsDir symlinks -cr /usr

    if [ $__SkipUnmount == 0 ]; then
        umount $__RootfsDir/*
    fi
fi
