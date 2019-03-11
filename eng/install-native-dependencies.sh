#!/usr/bin/env sh

if [ "$1" = "Linux" ]; then
    apt update
    if [ "$?" != "0" ]; then
       exit 1;
    fi
    apt list --installed
    apt install libcurl4-openssl-dev
    apt install curl
    apt list --installed
    if [ "$?" != "0"]; then
        exit 1;
    fi
elif [ "$1" = "OSX" ]; then
    brew update
    if [ "$?" != "0" ]; then
        exit 1;
    fi
    brew install icu4c openssl
    if [ "$?" != "0" ]; then
        exit 1;
    fi
    brew link --force icu4c
    if [ "$?" != "0" ]; then
        exit 1;
    fi
else
    echo "Must pass \"Linux\" or \"OSX\" as first argument."
    exit 1
fi

