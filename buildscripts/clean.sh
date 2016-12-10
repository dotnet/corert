#!/usr/bin/env bash

echo "Removing all untracked files in the working tree"
git clean -xdf $__working_tree_root
exit $?
