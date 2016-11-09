// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

#if defined(_TARGET_X86_) || defined(_TARGET_AMD64_)

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
#ifdef _TARGET_AMD64_
    PTR_UIntNative pR8;
    PTR_UIntNative pR9;
    PTR_UIntNative pR10;
    PTR_UIntNative pR11;
    PTR_UIntNative pR12;
    PTR_UIntNative pR13;
    PTR_UIntNative pR14;
    PTR_UIntNative pR15;
#endif // _TARGET_AMD64_

    UIntNative   SP;
    PTR_PCODE    pIP;
    PCODE        IP;

#if defined(_TARGET_AMD64_) && !defined(UNIX_AMD64_ABI)
    Fp128          Xmm[16-6]; // preserved xmm6..xmm15 regs for EH stackwalk
                              // these need to be unwound during a stack walk
                              // for EH, but not adjusted, so we only need
                              // their values, not their addresses
#endif // _TARGET_AMD64_ && !UNIX_AMD64_ABI

    inline PCODE GetIP() { return IP; }
    inline PTR_PCODE GetAddrOfIP() { return pIP; }
    inline UIntNative GetSP() { return SP; }
    inline UIntNative GetFP() { return *pRbp; }
    inline UIntNative GetPP() { return *pRbx; }

    inline void SetIP(PCODE IP) { this->IP = IP; }
    inline void SetAddrOfIP(PTR_PCODE pIP) { this->pIP = pIP; }
    inline void SetSP(UIntNative SP) { this->SP = SP; }
};

#elif defined(_TARGET_ARM_)

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

#elif defined(_TARGET_ARM64_)

struct REGDISPLAY 
{
    PTR_UIntNative pX0;
    PTR_UIntNative pX1;
    PTR_UIntNative pX2;
    PTR_UIntNative pX3;
    PTR_UIntNative pX4;
    PTR_UIntNative pX5;
    PTR_UIntNative pX6;
    PTR_UIntNative pX7;
    PTR_UIntNative pX8;
    PTR_UIntNative pX9;
    PTR_UIntNative pX10;
    PTR_UIntNative pX11;
    PTR_UIntNative pX12;
    PTR_UIntNative pX13;
    PTR_UIntNative pX14;
    PTR_UIntNative pX15;
    PTR_UIntNative pX16;
    PTR_UIntNative pX17;
    PTR_UIntNative pX18;
    PTR_UIntNative pX19;
    PTR_UIntNative pX20;
    PTR_UIntNative pX21;
    PTR_UIntNative pX22;
    PTR_UIntNative pX23;
    PTR_UIntNative pX24;
    PTR_UIntNative pX25;
    PTR_UIntNative pX26;
    PTR_UIntNative pX27;
    PTR_UIntNative pX28;
    PTR_UIntNative pFP; // X29
    PTR_UIntNative pLR; // X30

    UIntNative   SP;
    PTR_PCODE    pIP;
    PCODE        IP;

    UInt64       D[16-8]; // Only the bottom 64-bit value of the V registers V8..V15 needs to be preserved
                          // (V0-V7 and V16-V31 are not preserved according to the ABI spec).
                          // These need to be unwound during a stack walk
                          // for EH, but not adjusted, so we only need
                          // their values, not their addresses

    inline PCODE GetIP() { return IP; }
    inline PTR_PCODE GetAddrOfIP() { return pIP; }
    inline UIntNative GetSP() { return SP; }
    inline UIntNative GetFP() { return *pFP; }

    inline void SetIP(PCODE IP) { this->IP = IP; }
    inline void SetAddrOfIP(PTR_PCODE pIP) { this->pIP = pIP; }
    inline void SetSP(UIntNative SP) { this->SP = SP; }
};

#endif // _X86_ || _AMD64_

typedef REGDISPLAY * PREGDISPLAY;
