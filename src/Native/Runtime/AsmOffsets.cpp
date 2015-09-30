//
// Copyright (c) Microsoft Corporation.  All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.
//
#ifdef _ARM_

#define PLAT_ASM_OFFSET(offset, cls, member) OFFSETOF__##cls##__##member  equ  0x##offset
#define PLAT_ASM_SIZEOF(size,   cls        ) SIZEOF__##cls  equ  0x##size
#define PLAT_ASM_CONST(constant, expr)       expr  equ  0x##constant

#else

#define PLAT_ASM_OFFSET(offset, cls, member) OFFSETOF__##cls##__##member  equ  0##offset##h
#define PLAT_ASM_SIZEOF(size,   cls        ) SIZEOF__##cls  equ  0##size##h
#define PLAT_ASM_CONST(constant, expr)       expr  equ  0##constant##h

#endif

#include "AsmOffsets.h"
