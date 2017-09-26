#!/usr/bin/env bash

# Enable Server GC for this test
export RH_UseServerGC=1
$1/$2
if [ $? == 100 ]; then
    echo pass
    exit 0
else
    echo fail
    exit 1
fi
