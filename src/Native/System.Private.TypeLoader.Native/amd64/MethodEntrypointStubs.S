// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

.intel_syntax noprefix

#if defined(__APPLE__)
#define C_FUNC(name) _##name
#else
#define C_FUNC(name) name
#endif

.macro LEAF_ENTRY Name, Section
        .global C_FUNC(\Name)
#if defined(__APPLE__)
        .text
#else
        .global C_FUNC(_\Name)
        .type \Name, %function
#endif
C_FUNC(\Name):
        .cfi_startproc
.endm

.macro LEAF_END Name, Section
#if !defined(__APPLE__)
        .size \Name, .-\Name
#endif
        .cfi_endproc
.endm

LEAF_ENTRY MethodEntrypointStubs_SetupPointers, _TEXT
    // TODO: implement
    ret
LEAF_END MethodEntrypointStubs_SetupPointers, _TEXT
