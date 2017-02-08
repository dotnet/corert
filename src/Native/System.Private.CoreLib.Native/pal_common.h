// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

#pragma once

#include "config.h"

#include <assert.h>
#include <errno.h>
#include <stdint.h>

#include <new>

using std::nothrow;

////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
// Macros and helper functions

template<class T>
inline void UnusedInRelease(const T &t)
{
}
