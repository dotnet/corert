// Copyright (c) Microsoft and Contributors. All rights reserved. Licensed under the University of Illinois/NCSA Open Source License. See LICENSE.txt in the project root for license information.

// Ported from https://github.com/llvm/llvm-project/tree/llvmorg-9.0.0/llvm/include/llvm-c
// Original source is Copyright (c) the LLVM Project and Contributors. Licensed under the Apache License v2.0 with LLVM Exceptions. See NOTICE.txt in the project root for license information.

namespace LLVMSharp.Interop
{
    public enum LLVMCallConv
    {
        LLVMCCallConv = 0,
        LLVMFastCallConv = 8,
        LLVMColdCallConv = 9,
        LLVMGHCCallConv = 10,
        LLVMHiPECallConv = 11,
        LLVMWebKitJSCallConv = 12,
        LLVMAnyRegCallConv = 13,
        LLVMPreserveMostCallConv = 14,
        LLVMPreserveAllCallConv = 15,
        LLVMSwiftCallConv = 16,
        LLVMCXXFASTTLSCallConv = 17,
        LLVMX86StdcallCallConv = 64,
        LLVMX86FastcallCallConv = 65,
        LLVMARMAPCSCallConv = 66,
        LLVMARMAAPCSCallConv = 67,
        LLVMARMAAPCSVFPCallConv = 68,
        LLVMMSP430INTRCallConv = 69,
        LLVMX86ThisCallCallConv = 70,
        LLVMPTXKernelCallConv = 71,
        LLVMPTXDeviceCallConv = 72,
        LLVMSPIRFUNCCallConv = 75,
        LLVMSPIRKERNELCallConv = 76,
        LLVMIntelOCLBICallConv = 77,
        LLVMX8664SysVCallConv = 78,
        LLVMWin64CallConv = 79,
        LLVMX86VectorCallCallConv = 80,
        LLVMHHVMCallConv = 81,
        LLVMHHVMCCallConv = 82,
        LLVMX86INTRCallConv = 83,
        LLVMAVRINTRCallConv = 84,
        LLVMAVRSIGNALCallConv = 85,
        LLVMAVRBUILTINCallConv = 86,
        LLVMAMDGPUVSCallConv = 87,
        LLVMAMDGPUGSCallConv = 88,
        LLVMAMDGPUPSCallConv = 89,
        LLVMAMDGPUCSCallConv = 90,
        LLVMAMDGPUKERNELCallConv = 91,
        LLVMX86RegCallCallConv = 92,
        LLVMAMDGPUHSCallConv = 93,
        LLVMMSP430BUILTINCallConv = 94,
        LLVMAMDGPULSCallConv = 95,
        LLVMAMDGPUESCallConv = 96,
    }
}
