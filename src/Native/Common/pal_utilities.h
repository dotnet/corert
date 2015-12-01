// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

#pragma once

#include <assert.h>
#include <stddef.h>
#include <stdio.h>
#include <string.h>
#include <type_traits>

/**
* Cast a positive value typed as a signed integer to the
* appropriately sized unsigned integer type.
*
* We use this when we've already ensured that the value is positive,
* but we don't want to cast to a specific unsigned type as that could
* inadvertently defeat the compiler's narrowing conversion warnings
* (which we treat as error).
*/
template <typename T>
inline typename std::make_unsigned<T>::type UnsignedCast(T value)
{
    assert(value >= 0);
    return static_cast<typename std::make_unsigned<T>::type>(value);
}

