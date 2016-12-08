#!/usr/bin/env bash

. ./buildscripts/buildvars-setup.sh $*

# If setting variables failed, exit with the status code of the vars script
if [ $BUILDERRORLEVEL != 0 ]; then
    exit $BUILDERRORLEVEL
fi

. ./buildscripts/build-native.sh $*

# If native build failed, exit with the status code of the native build
if [ $BUILDERRORLEVEL != 0 ]; then
    exit $BUILDERRORLEVEL
fi

. ./buildscripts/build-managed.sh $*

# If managed build failed, exit with the status code of the managed build
if [ $BUILDERRORLEVEL != 0 ]; then
    exit $BUILDERRORLEVEL
fi

. ./buildscripts/build-tests.sh $*

if [ $TESTERRORLEVEL != 0 ]; then
    exit $TESTERRORLEVEL
fi

echo "Product binaries are available at $__ProductBinDir"

exit $BUILDERRORLEVEL
