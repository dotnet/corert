// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

#if defined(_TARGET_X86_) || defined(_TARGET_AMD64_)

// These definitions are from our copy of libunwind. Including them directly here
// so that the REGDISPLAY code doesn't need to reference libunwind.
namespace LibunwindConstants
{
    // copied from Registers.hpp
    // Architecture independent register numbers
    enum {
      UNW_REG_IP = -1, // instruction pointer
      UNW_REG_SP = -2, // stack pointer
    };

    // copied from libunwind.h
    // 64-bit x86_64 registers
    enum {
      UNW_X86_64_RAX = 0,
      UNW_X86_64_RDX = 1,
      UNW_X86_64_RCX = 2,
      UNW_X86_64_RBX = 3,
      UNW_X86_64_RSI = 4,
      UNW_X86_64_RDI = 5,
      UNW_X86_64_RBP = 6,
      UNW_X86_64_RSP = 7,
      UNW_X86_64_R8  = 8,
      UNW_X86_64_R9  = 9,
      UNW_X86_64_R10 = 10,
      UNW_X86_64_R11 = 11,
      UNW_X86_64_R12 = 12,
      UNW_X86_64_R13 = 13,
      UNW_X86_64_R14 = 14,
      UNW_X86_64_R15 = 15
    };
}

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

    // Everything below was added to enable libunwind to work with REGDISPLAYs.

#if defined(_TARGET_AMD64_)

    inline uint64_t getRegister(int regNum) const
    {
        switch (regNum) 
        {
        case LibunwindConstants::UNW_REG_IP:
            return IP;
        case LibunwindConstants::UNW_REG_SP:
            return SP;
        case LibunwindConstants::UNW_X86_64_RAX:
            return *pRax;
        case LibunwindConstants::UNW_X86_64_RDX:
            return *pRdx;
        case LibunwindConstants::UNW_X86_64_RCX:
            return *pRcx;
        case LibunwindConstants::UNW_X86_64_RBX:
            return *pRbx;
        case LibunwindConstants::UNW_X86_64_RSI:
            return *pRsi;
        case LibunwindConstants::UNW_X86_64_RDI:
            return *pRdi;
        case LibunwindConstants::UNW_X86_64_RBP:
            return *pRbp;
        case LibunwindConstants::UNW_X86_64_RSP:
            return SP;
        case LibunwindConstants::UNW_X86_64_R8:
            return *pR8;
        case LibunwindConstants::UNW_X86_64_R9:
            return *pR9;
        case LibunwindConstants::UNW_X86_64_R10:
            return *pR10;
        case LibunwindConstants::UNW_X86_64_R11:
            return *pR11;
        case LibunwindConstants::UNW_X86_64_R12:
            return *pR12;
        case LibunwindConstants::UNW_X86_64_R13:
            return *pR13;
        case LibunwindConstants::UNW_X86_64_R14:
            return *pR14;
        case LibunwindConstants::UNW_X86_64_R15:
            return *pR15;
        }

        // Unsupported register requested
        abort();
    }

    inline void setRegister(int regNum, uint64_t value, uint64_t location)
    {
        switch (regNum)
        {
        case LibunwindConstants::UNW_REG_IP:
            IP = value;
            pIP = (PTR_PCODE)location;
            return;
        case LibunwindConstants::UNW_REG_SP:
            SP = value;
            return;
        case LibunwindConstants::UNW_X86_64_RAX:
            pRax = (PTR_UIntNative)location;
            return;
        case LibunwindConstants::UNW_X86_64_RDX:
            pRdx = (PTR_UIntNative)location;
            return;
        case LibunwindConstants::UNW_X86_64_RCX:
            pRcx =  (PTR_UIntNative)location;
            return;
        case LibunwindConstants::UNW_X86_64_RBX:
            pRbx =  (PTR_UIntNative)location;
            return;
        case LibunwindConstants::UNW_X86_64_RSI:
            pRsi =  (PTR_UIntNative)location;
            return;
        case LibunwindConstants::UNW_X86_64_RDI:
            pRdi =  (PTR_UIntNative)location;
            return;
        case LibunwindConstants::UNW_X86_64_RBP:
            pRbp =  (PTR_UIntNative)location;
            return;
        case LibunwindConstants::UNW_X86_64_RSP:
            SP = value;
            return;
        case LibunwindConstants::UNW_X86_64_R8:
            pR8 = (PTR_UIntNative)location;
            return;
        case LibunwindConstants::UNW_X86_64_R9:
            pR9 =  (PTR_UIntNative)location;
            return;
        case LibunwindConstants::UNW_X86_64_R10:
            pR10 =  (PTR_UIntNative)location;
            return;
        case LibunwindConstants::UNW_X86_64_R11:
            pR11 =  (PTR_UIntNative)location;
            return;
        case LibunwindConstants::UNW_X86_64_R12:
            pR12 =  (PTR_UIntNative)location;
            return;
        case LibunwindConstants::UNW_X86_64_R13:
            pR13 =  (PTR_UIntNative)location;
            return;
        case LibunwindConstants::UNW_X86_64_R14:
            pR14 =  (PTR_UIntNative)location;
            return;
        case LibunwindConstants::UNW_X86_64_R15:
            pR15 =  (PTR_UIntNative)location;
            return;
        }

        // Unsupported x86_64 register
        abort();
    }

    // N/A for x86_64
    inline bool validFloatRegister(int) { return false; }
    inline bool validVectorRegister(int) { return false; }

    inline static int  lastDwarfRegNum() { return 16; }

    inline bool validRegister(int regNum) const
    {
        if (regNum == LibunwindConstants::UNW_REG_IP)
            return true;
        if (regNum == LibunwindConstants::UNW_REG_SP)
            return true;
        if (regNum < 0)
            return false;
        if (regNum > 15)
            return false;
        return true;
    }

    // N/A for x86_64
    inline double getFloatRegister(int) const   { abort(); }
    inline   void setFloatRegister(int, double) { abort(); }
    inline double getVectorRegister(int) const  { abort(); }
    inline   void setVectorRegister(int, ...)   { abort(); }

    uint64_t  getSP() const { return SP; }
    void      setSP(uint64_t value, uint64_t location) { SP = value; }

    uint64_t  getIP() const { return IP; }

    void      setIP(uint64_t value, uint64_t location)
    {
        IP = value;
        pIP = (PTR_PCODE)location;
    }

    uint64_t  getRBP() const         { return *pRbp; }
    void      setRBP(uint64_t value, uint64_t location) { pRbp = (PTR_UIntNative)location; }
    uint64_t  getRBX() const         { return *pRbx; }
    void      setRBX(uint64_t value, uint64_t location) { pRbx = (PTR_UIntNative)location; }
    uint64_t  getR12() const         { return *pR12; }
    void      setR12(uint64_t value, uint64_t location) { pR12 = (PTR_UIntNative)location; }
    uint64_t  getR13() const         { return *pR13; }
    void      setR13(uint64_t value, uint64_t location) { pR13 = (PTR_UIntNative)location; }
    uint64_t  getR14() const         { return *pR14; }
    void      setR14(uint64_t value, uint64_t location) { pR14 = (PTR_UIntNative)location; }
    uint64_t  getR15() const         { return *pR15; }
    void      setR15(uint64_t value, uint64_t location) { pR15 = (PTR_UIntNative)location; }

#endif // _TARGET_AMD64_

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
#elif defined(_TARGET_WASM_)

struct REGDISPLAY
{
    // TODO: WebAssembly doesn't really have registers. What exactly do we need here?

    UIntNative   SP;
    PTR_PCODE    pIP;
    PCODE        IP;

    inline PCODE GetIP() { return NULL; }
    inline PTR_PCODE GetAddrOfIP() { return NULL; }
    inline UIntNative GetSP() { return 0; }
    inline UIntNative GetFP() { return 0; }

    inline void SetIP(PCODE IP) { }
    inline void SetAddrOfIP(PTR_PCODE pIP) { }
    inline void SetSP(UIntNative SP) { }
};
#endif // _X86_ || _AMD64_ || _ARM_ || _ARM64_ || _WASM_

typedef REGDISPLAY * PREGDISPLAY;
