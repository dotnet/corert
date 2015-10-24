//
// Copyright (c) Microsoft Corporation.  All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.
//

#if defined(TARGET_X86) || defined(TARGET_AMD64)

struct REGDISPLAY 
{
    PTR_UIntNative pRax;
    PTR_UIntNative pRcx;
    PTR_UIntNative pRdx;
    PTR_UIntNative pRbx;
    //           pEsp;
    PTR_UIntNative pRbp;
    PTR_UIntNative pRsi;
    PTR_UIntNative pRdi;
#ifdef TARGET_AMD64
    PTR_UIntNative pR8;
    PTR_UIntNative pR9;
    PTR_UIntNative pR10;
    PTR_UIntNative pR11;
    PTR_UIntNative pR12;
    PTR_UIntNative pR13;
    PTR_UIntNative pR14;
    PTR_UIntNative pR15;
#endif // TARGET_AMD64

    UIntNative   SP;
    PTR_PCODE    pIP;
    PCODE        IP;

#ifdef TARGET_AMD64
    Fp128          Xmm[16-6]; // preserved xmm6..xmm15 regs for EH stackwalk
                              // these need to be unwound during a stack walk
                              // for EH, but not adjusted, so we only need
                              // their values, not their addresses
#endif // TARGET_AMD64

    inline PCODE GetIP() { return IP; }
    inline PTR_PCODE GetAddrOfIP() { return pIP; }
    inline UIntNative GetSP() { return SP; }
    inline UIntNative GetFP() { return *pRbp; }
    inline UIntNative GetPP() { return *pRbx; }

    inline void SetIP(PCODE IP) { this->IP = IP; }
    inline void SetAddrOfIP(PTR_PCODE pIP) { this->pIP = pIP; }
    inline void SetSP(UIntNative SP) { this->SP = SP; }
};

#elif defined(TARGET_ARM)

struct REGDISPLAY 
{
    PTR_UIntNative pR0;
    PTR_UIntNative pR1;
    PTR_UIntNative pR2;
    PTR_UIntNative pR3;
    PTR_UIntNative pR4;
    PTR_UIntNative pR5;
    PTR_UIntNative pR6;
    PTR_UIntNative pR7;
    PTR_UIntNative pR8;
    PTR_UIntNative pR9;
    PTR_UIntNative pR10;
    PTR_UIntNative pR11;
    PTR_UIntNative pR12;
    PTR_UIntNative pLR;

    UIntNative   SP;
    PTR_PCODE    pIP;
    PCODE        IP;

    UInt64       D[16-8]; // preserved D registers D8..D15 (note that D16-D31 are not preserved according to the ABI spec)
                          // these need to be unwound during a stack walk
                          // for EH, but not adjusted, so we only need
                          // their values, not their addresses

    inline PCODE GetIP() { return IP; }
    inline PTR_PCODE GetAddrOfIP() { return pIP; }
    inline UIntNative GetSP() { return SP; }
    inline UIntNative GetFP() { return *pR7; }

    inline void SetIP(PCODE IP) { this->IP = IP; }
    inline void SetAddrOfIP(PTR_PCODE pIP) { this->pIP = pIP; }
    inline void SetSP(UIntNative SP) { this->SP = SP; }
};

#endif // _X86_ || _AMD64_

typedef REGDISPLAY * PREGDISPLAY;
