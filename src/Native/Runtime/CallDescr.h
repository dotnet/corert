// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

struct CallDescrData
{
    uint8_t* pSrc;
    int numStackSlots;
    int fpReturnSize;
    uint8_t* pArgumentRegisters;
    uint8_t* pFloatArgumentRegisters;
    void* pTarget;
    void* pReturnBuffer;
};
