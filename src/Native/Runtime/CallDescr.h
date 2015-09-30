//
// Copyright (c) Microsoft Corporation.  All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.
//
struct CallDescrData
{
    BYTE* pSrc;
    int numStackSlots;
    int fpReturnSize;
    BYTE* pArgumentRegisters;
    BYTE* pFloatArgumentRegisters;
    void* pTarget;
    void* pReturnBuffer;
};
