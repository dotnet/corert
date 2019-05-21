#!/usr/bin/env bash

${CoreRT_CoreCLRRuntimeDir}/corerun $1/$2.exe
if [ $? == 100 ]; then
    echo pass
    exit 0
else
    echo fail
    exit 1
fi
