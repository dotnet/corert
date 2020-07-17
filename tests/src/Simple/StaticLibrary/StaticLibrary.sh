#!/usr/bin/env bash
$1/StaticLibraryTest
if [ $? == 100 ]; then
    echo pass
    exit 0
else
    echo fail
    exit 1
fi
