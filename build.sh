#!/usr/bin/env bash

. ./cibuild/buildvars-setup.sh $*

# If setting variables failed, exit with the status code of the vars script
if [ $BUILDERRORLEVEL != 0 ]; then
    exit $BUILDERRORLEVEL
fi

. ./cibuild/build-native.sh $*

# If native build failed, exit with the status code of the native build
if [ $BUILDERRORLEVEL != 0 ]; then
    exit $BUILDERRORLEVEL
fi

. ./cibuild/build-managed.sh $*

# If managed build failed, exit with the status code of the managed build
if [ $BUILDERRORLEVEL != 0 ]; then
    exit $BUILDERRORLEVEL
fi

. ./cibuild/build-tests.sh $*

if [ $TESTERRORLEVEL != 0 ]; then
    exit $TESTERRORLEVEL
fi

echo "Product binaries are available at $__ProductBinDir"

exit $BUILDERRORLEVEL
