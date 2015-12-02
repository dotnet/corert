//
// Copyright (c) Microsoft Corporation.  All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.
//

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
