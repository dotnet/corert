#!/usr/bin/env sh

if [ "$1" = "Linux" ]; then
    sudo apt update
    if [ "$?" != "0" ]; then
       exit 1;
    fi
    sudo apt list --installed
    sudo apt install libcurl4-openssl-dev
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

