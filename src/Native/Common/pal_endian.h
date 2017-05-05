// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

#pragma once

#include <inttypes.h>

inline uint16_t SWAP16(uint16_t x)
{
    return (x >> 8) | (x << 8);
}

inline uint32_t SWAP32(uint32_t x)
{
    return  (x >> 24) |
            ((x >> 8) & 0x0000FF00L) |
            ((x & 0x0000FF00L) << 8) |
            (x << 24);
}
