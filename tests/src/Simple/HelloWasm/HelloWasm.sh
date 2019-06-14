#!/usr/bin/env bash
dir=$1
file=$2
cmp=`$dir/$file world | tr '\n' ';'`
if [[ $cmp = "Hello world;" ]]; then
    echo pass
    exit 0
else
    echo fail
    exit 1
fi
