//===---- dwarfGen.cpp ------------------------------------------*- C++ -*-===//
//
// dwarf generator implementation
//
// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.
//
//===----------------------------------------------------------------------===//

#include "dwarfGen.h"
#include "dwarfAbbrev.h"

#include "llvm/MC/MCContext.h"
#include "llvm/MC/MCAsmInfo.h"
#include "llvm/MC/MCObjectFileInfo.h"
#include "llvm/Support/LEB128.h"

#ifdef FEATURE_LANGID_CS
#define DW_LANG_MICROSOFT_CSHARP 0x9e57
#endif

// Keep sync with ICorDebugInfo::RegNum (cordebuginfo.h)

enum class RegNumX86
{
  REGNUM_EAX,
  REGNUM_ECX,
  REGNUM_EDX,
  REGNUM_EBX,
  REGNUM_ESP,
  REGNUM_EBP,
  REGNUM_ESI,
  REGNUM_EDI,
  REGNUM_COUNT,
  REGNUM_FP = REGNUM_EBP,
  REGNUM_SP = REGNUM_ESP
};

enum class RegNumArm
{
  REGNUM_R0,
  REGNUM_R1,
  REGNUM_R2,
  REGNUM_R3,
  REGNUM_R4,
  REGNUM_R5,
  REGNUM_R6,
  REGNUM_R7,
  REGNUM_R8,
  REGNUM_R9,
  REGNUM_R10,
  REGNUM_R11,
  REGNUM_R12,
  REGNUM_SP,
  REGNUM_LR,
  REGNUM_PC,
  REGNUM_COUNT,
  REGNUM_FP = REGNUM_R7
};

enum class RegNumArm64
{
  REGNUM_X0,
  REGNUM_X1,
  REGNUM_X2,
  REGNUM_X3,
  REGNUM_X4,
  REGNUM_X5,
  REGNUM_X6,
  REGNUM_X7,
  REGNUM_X8,
  REGNUM_X9,
  REGNUM_X10,
  REGNUM_X11,
  REGNUM_X12,
  REGNUM_X13,
  REGNUM_X14,
  REGNUM_X15,
  REGNUM_X16,
  REGNUM_X17,
  REGNUM_X18,
  REGNUM_X19,
  REGNUM_X20,
  REGNUM_X21,
  REGNUM_X22,
  REGNUM_X23,
  REGNUM_X24,
  REGNUM_X25,
  REGNUM_X26,
  REGNUM_X27,
  REGNUM_X28,
  REGNUM_FP,
  REGNUM_LR,
  REGNUM_SP,
  REGNUM_PC,
  REGNUM_COUNT
};

enum class RegNumAmd64
{
  REGNUM_RAX,
  REGNUM_RCX,
  REGNUM_RDX,
  REGNUM_RBX,
  REGNUM_RSP,
  REGNUM_RBP,
  REGNUM_RSI,
  REGNUM_RDI,
  REGNUM_R8,
  REGNUM_R9,
  REGNUM_R10,
  REGNUM_R11,
  REGNUM_R12,
  REGNUM_R13,
  REGNUM_R14,
  REGNUM_R15,
  REGNUM_COUNT,
  REGNUM_SP = REGNUM_RSP,
  REGNUM_FP = REGNUM_RBP
};

// Helper routines from lib/MC/MCDwarf.cpp
static const MCExpr *forceExpAbs(MCStreamer &OS, const MCExpr* Expr) {
  MCContext &Context = OS.getContext();
  assert(!isa<MCSymbolRefExpr>(Expr));
  if (Context.getAsmInfo()->hasAggressiveSymbolFolding())
    return Expr;

  MCSymbol *ABS = Context.createTempSymbol();
  OS.EmitAssignment(ABS, Expr);
  return MCSymbolRefExpr::create(ABS, Context);
}

static void emitAbsValue(MCStreamer &OS, const MCExpr *Value, unsigned Size) {
  const MCExpr *ABS = forceExpAbs(OS, Value);
  OS.EmitValue(ABS, Size);
}

static const MCExpr *MakeStartMinusEndExpr(const MCStreamer &MCOS,
                                           const MCSymbol &Start,
                                           const MCSymbol &End,
                                           int IntVal) {
  MCSymbolRefExpr::VariantKind Variant = MCSymbolRefExpr::VK_None;
  const MCExpr *Res =
    MCSymbolRefExpr::create(&End, Variant, MCOS.getContext());
  const MCExpr *RHS =
    MCSymbolRefExpr::create(&Start, Variant, MCOS.getContext());
  const MCExpr *Res1 =
    MCBinaryExpr::create(MCBinaryExpr::Sub, Res, RHS, MCOS.getContext());
  const MCExpr *Res2 =
    MCConstantExpr::create(IntVal, MCOS.getContext());
  const MCExpr *Res3 =
    MCBinaryExpr::create(MCBinaryExpr::Sub, Res1, Res2, MCOS.getContext());
  return Res3;
}

static int GetDwarfRegNum(Triple::ArchType ArchType, int RegNum) {
  switch (ArchType) {
    case Triple::x86:
      switch (static_cast<RegNumX86>(RegNum)) {
        case RegNumX86::REGNUM_EAX: return 0;
        case RegNumX86::REGNUM_ECX: return 1;
        case RegNumX86::REGNUM_EDX: return 2;
        case RegNumX86::REGNUM_EBX: return 3;
        case RegNumX86::REGNUM_ESP: return 4;
        case RegNumX86::REGNUM_EBP: return 5;
        case RegNumX86::REGNUM_ESI: return 6;
        case RegNumX86::REGNUM_EDI: return 7;
        // fp registers
        default:
          return RegNum - static_cast<int>(RegNumX86::REGNUM_COUNT) + 32;
      }
    case Triple::arm:   // fall through
    case Triple::armeb: // fall through
    case Triple::thumb: // fall through
    case Triple::thumbeb:
      switch (static_cast<RegNumArm>(RegNum)) {
        case RegNumArm::REGNUM_R0:  return 0;
        case RegNumArm::REGNUM_R1:  return 1;
        case RegNumArm::REGNUM_R2:  return 2;
        case RegNumArm::REGNUM_R3:  return 3;
        case RegNumArm::REGNUM_R4:  return 4;
        case RegNumArm::REGNUM_R5:  return 5;
        case RegNumArm::REGNUM_R6:  return 6;
        case RegNumArm::REGNUM_R7:  return 7;
        case RegNumArm::REGNUM_R8:  return 8;
        case RegNumArm::REGNUM_R9:  return 9;
        case RegNumArm::REGNUM_R10: return 10;
        case RegNumArm::REGNUM_R11: return 11;
        case RegNumArm::REGNUM_R12: return 12;
        case RegNumArm::REGNUM_SP:  return 13;
        case RegNumArm::REGNUM_LR:  return 14;
        case RegNumArm::REGNUM_PC:  return 15;
        // fp registers
        default:
          return RegNum - static_cast<int>(RegNumArm::REGNUM_COUNT) + 64;
      }
    case Triple::aarch64: // fall through
    case Triple::aarch64_be:
      switch (static_cast<RegNumArm64>(RegNum)) {
        case RegNumArm64::REGNUM_X0:  return 0;
        case RegNumArm64::REGNUM_X1:  return 1;
        case RegNumArm64::REGNUM_X2:  return 2;
        case RegNumArm64::REGNUM_X3:  return 3;
        case RegNumArm64::REGNUM_X4:  return 4;
        case RegNumArm64::REGNUM_X5:  return 5;
        case RegNumArm64::REGNUM_X6:  return 6;
        case RegNumArm64::REGNUM_X7:  return 7;
        case RegNumArm64::REGNUM_X8:  return 8;
        case RegNumArm64::REGNUM_X9:  return 9;
        case RegNumArm64::REGNUM_X10: return 10;
        case RegNumArm64::REGNUM_X11: return 11;
        case RegNumArm64::REGNUM_X12: return 12;
        case RegNumArm64::REGNUM_X13: return 13;
        case RegNumArm64::REGNUM_X14: return 14;
        case RegNumArm64::REGNUM_X15: return 15;
        case RegNumArm64::REGNUM_X16: return 16;
        case RegNumArm64::REGNUM_X17: return 17;
        case RegNumArm64::REGNUM_X18: return 18;
        case RegNumArm64::REGNUM_X19: return 19;
        case RegNumArm64::REGNUM_X20: return 20;
        case RegNumArm64::REGNUM_X21: return 21;
        case RegNumArm64::REGNUM_X22: return 22;
        case RegNumArm64::REGNUM_X23: return 23;
        case RegNumArm64::REGNUM_X24: return 24;
        case RegNumArm64::REGNUM_X25: return 25;
        case RegNumArm64::REGNUM_X26: return 26;
        case RegNumArm64::REGNUM_X27: return 27;
        case RegNumArm64::REGNUM_X28: return 28;
        case RegNumArm64::REGNUM_FP:  return 29;
        case RegNumArm64::REGNUM_LR:  return 30;
        case RegNumArm64::REGNUM_SP:  return 31;
        case RegNumArm64::REGNUM_PC:  return 32;
        // fp registers
        default:
          return RegNum - static_cast<int>(RegNumArm64::REGNUM_COUNT) + 64;
      }
    case Triple::x86_64:
      switch (static_cast<RegNumAmd64>(RegNum)) {
        case RegNumAmd64::REGNUM_RAX: return 0;
        case RegNumAmd64::REGNUM_RDX: return 1;
        case RegNumAmd64::REGNUM_RCX: return 2;
        case RegNumAmd64::REGNUM_RBX: return 3;
        case RegNumAmd64::REGNUM_RSI: return 4;
        case RegNumAmd64::REGNUM_RDI: return 5;
        case RegNumAmd64::REGNUM_RBP: return 6;
        case RegNumAmd64::REGNUM_RSP: return 7;
        case RegNumAmd64::REGNUM_R8:  return 8;
        case RegNumAmd64::REGNUM_R9:  return 9;
        case RegNumAmd64::REGNUM_R10: return 10;
        case RegNumAmd64::REGNUM_R11: return 11;
        case RegNumAmd64::REGNUM_R12: return 12;
        case RegNumAmd64::REGNUM_R13: return 13;
        case RegNumAmd64::REGNUM_R14: return 14;
        case RegNumAmd64::REGNUM_R15: return 15;
        // fp registers
        default:
          return RegNum - static_cast<int>(RegNumAmd64::REGNUM_COUNT) + 17;
      }
    default:
      assert(false && "Unexpected architecture");
      return 0;
  }
}

static int GetDwarfFpRegNum(Triple::ArchType ArchType)
{
  switch (ArchType) {
    case Triple::x86:
      return GetDwarfRegNum(ArchType, static_cast<int>(RegNumX86::REGNUM_FP));
    case Triple::arm:   // fall through
    case Triple::armeb: // fall through
    case Triple::thumb: // fall through
    case Triple::thumbeb:
      return GetDwarfRegNum(ArchType, static_cast<int>(RegNumArm::REGNUM_FP));
    case Triple::aarch64: // fall through
    case Triple::aarch64_be:
      return GetDwarfRegNum(ArchType, static_cast<int>(RegNumArm64::REGNUM_FP));
    case Triple::x86_64:
      return GetDwarfRegNum(ArchType, static_cast<int>(RegNumAmd64::REGNUM_FP));
    default:
      assert(false && "Unexpected architecture");
      return 0;
  }
}

static void EmitVarLocation(MCObjectStreamer *Streamer,
                     const ICorDebugInfo::NativeVarInfo &VarInfo,
                     bool IsLocList = false) {
  MCContext &context = Streamer->getContext();
  unsigned TargetPointerSize = context.getAsmInfo()->getCodePointerSize();
  Triple::ArchType ArchType = context.getObjectFileInfo()->getTargetTriple().getArch();

  int DwarfRegNum;
  int DwarfRegNum2;
  int DwarfBaseRegNum;
  unsigned Len;

  bool IsByRef = false;
  bool IsStk2 = false;
  bool IsRegStk = false;

  switch (VarInfo.loc.vlType) {
    case ICorDebugInfo::VLT_REG_BYREF: // fall through
      IsByRef = true;
    case ICorDebugInfo::VLT_REG_FP:    // fall through
    case ICorDebugInfo::VLT_REG: {
      DwarfRegNum = GetDwarfRegNum(ArchType, VarInfo.loc.vlReg.vlrReg);
      if (IsByRef) {
        Len = 2;
        if (IsLocList) {
          Streamer->EmitIntValue(Len, 2);
        } else {
          Streamer->EmitULEB128IntValue(Len);
        }
        Streamer->EmitIntValue(DwarfRegNum + dwarf::DW_OP_breg0, 1);
        Streamer->EmitSLEB128IntValue(0);
      } else {
        Len = 1;
        if (IsLocList) {
          Streamer->EmitIntValue(Len, 2);
        } else {
          Streamer->EmitULEB128IntValue(Len);
        }
        Streamer->EmitIntValue(DwarfRegNum + dwarf::DW_OP_reg0, 1);
      }

      break;
    }
    case ICorDebugInfo::VLT_STK_BYREF: // fall through
      IsByRef = true;
    case ICorDebugInfo::VLT_STK2:
      IsStk2 = true;
    case ICorDebugInfo::VLT_STK: {
      DwarfBaseRegNum = GetDwarfRegNum(ArchType, IsStk2 ? VarInfo.loc.vlStk2.vls2BaseReg :
          VarInfo.loc.vlStk.vlsBaseReg);

      SmallString<128> Tmp;
      raw_svector_ostream OSE(Tmp);
      encodeSLEB128(IsStk2 ? VarInfo.loc.vlStk2.vls2Offset :
          VarInfo.loc.vlStk.vlsOffset, OSE);
      StringRef OffsetRepr = OSE.str();

      if (IsByRef) {
        Len = OffsetRepr.size() + 2;
        if (IsLocList) {
          Streamer->EmitIntValue(Len, 2);
        } else {
          Streamer->EmitULEB128IntValue(Len);
        }
        Streamer->EmitIntValue(DwarfBaseRegNum + dwarf::DW_OP_breg0, 1);
        Streamer->EmitBytes(OffsetRepr);
        Streamer->EmitIntValue(dwarf::DW_OP_deref, 1);
      } else {
        Len = OffsetRepr.size() + 1;
        if (IsLocList) {
          Streamer->EmitIntValue(Len, 2);
        } else {
          Streamer->EmitULEB128IntValue(Len);
        }
        Streamer->EmitIntValue(DwarfBaseRegNum + dwarf::DW_OP_breg0, 1);
        Streamer->EmitBytes(OffsetRepr);
      }

      break;
    }
    case ICorDebugInfo::VLT_REG_REG: {
      DwarfRegNum  = GetDwarfRegNum(ArchType, VarInfo.loc.vlRegReg.vlrrReg1);
      DwarfRegNum2 = GetDwarfRegNum(ArchType, VarInfo.loc.vlRegReg.vlrrReg2);

      Len = (1 /* DW_OP_reg */ + 1 /* DW_OP_piece */ + 1 /* Reg size */) * 2 + 1;
      if (IsLocList) {
        Streamer->EmitIntValue(Len, 2);
      } else {
        Streamer->EmitULEB128IntValue(Len + 1);
      }

      Streamer->EmitIntValue(DwarfRegNum2 + dwarf::DW_OP_reg0, 1);
      Streamer->EmitIntValue(dwarf::DW_OP_piece, 1);
      Streamer->EmitULEB128IntValue(TargetPointerSize);

      Streamer->EmitIntValue(DwarfRegNum + dwarf::DW_OP_reg0, 1);
      Streamer->EmitIntValue(dwarf::DW_OP_piece, 1);
      Streamer->EmitULEB128IntValue(TargetPointerSize);

      break;
    }
    case ICorDebugInfo::VLT_REG_STK: // fall through
      IsRegStk = true;
    case ICorDebugInfo::VLT_STK_REG: {
      DwarfRegNum = GetDwarfRegNum(ArchType, IsRegStk ? VarInfo.loc.vlRegStk.vlrsReg :
          VarInfo.loc.vlStkReg.vlsrReg);
      DwarfBaseRegNum = GetDwarfRegNum(ArchType, IsRegStk ? VarInfo.loc.vlRegStk.vlrsStk.vlrssBaseReg :
          VarInfo.loc.vlStkReg.vlsrStk.vlsrsBaseReg);

      SmallString<128> Tmp;
      raw_svector_ostream OSE(Tmp);
      encodeSLEB128(IsRegStk ? VarInfo.loc.vlRegStk.vlrsStk.vlrssOffset :
          VarInfo.loc.vlStkReg.vlsrStk.vlsrsOffset, OSE);
      StringRef OffsetRepr = OSE.str();

      Len = (1 /* DW_OP_reg */ + 1 /* DW_OP_piece */ + 1 /* Reg size */) +
          (1 /*DW_OP_breg */ + OffsetRepr.size() + 1 /* DW_OP_piece */ + 1 /* Reg size */) + 1;

      if (IsLocList) {
        Streamer->EmitIntValue(Len, 2);
      } else {
        Streamer->EmitULEB128IntValue(Len + 1);
      }

      if (IsRegStk) {
        Streamer->EmitIntValue(DwarfRegNum + dwarf::DW_OP_reg0, 1);
        Streamer->EmitIntValue(dwarf::DW_OP_piece, 1);
        Streamer->EmitULEB128IntValue(TargetPointerSize);

        Streamer->EmitIntValue(DwarfBaseRegNum + dwarf::DW_OP_breg0, 1);
        Streamer->EmitBytes(OffsetRepr);
        Streamer->EmitIntValue(dwarf::DW_OP_piece, 1);
        Streamer->EmitULEB128IntValue(TargetPointerSize);
      } else {
        Streamer->EmitIntValue(DwarfBaseRegNum + dwarf::DW_OP_breg0, 1);
        Streamer->EmitBytes(OffsetRepr);
        Streamer->EmitIntValue(dwarf::DW_OP_piece, 1);
        Streamer->EmitULEB128IntValue(TargetPointerSize);

        Streamer->EmitIntValue(DwarfRegNum + dwarf::DW_OP_reg0, 1);
        Streamer->EmitIntValue(dwarf::DW_OP_piece, 1);
        Streamer->EmitULEB128IntValue(TargetPointerSize);
      }

      break;
    }
    case ICorDebugInfo::VLT_FPSTK:
    case ICorDebugInfo::VLT_FIXED_VA:
      assert(false && "Unsupported varloc type!");
    default:
      assert(false && "Unknown varloc type!");
      if (IsLocList) {
        Streamer->EmitIntValue(0, 2);
      } else {
        Streamer->EmitULEB128IntValue(0);
      }
  }
}

// Lexical scope

class LexicalScope
{
public:
  LexicalScope(uint64_t Start, uint64_t End, bool IsFuncScope = false) :
               Start(Start),
               End(End),
               IsFuncScope(IsFuncScope) {}

  LexicalScope(VarInfo *Info) :
               Start(Info->GetStartOffset()),
               End(Info->GetEndOffset()),
               IsFuncScope(false) { Vars.push_back(Info); }

  bool IsContains(const VarInfo *Info) const {
    return Start <= Info->GetStartOffset() && End >= Info->GetEndOffset();
  }

  void AddVar(VarInfo *Info);

  void Dump(UserDefinedDwarfTypesBuilder *TypeBuilder, MCObjectStreamer *Streamer,
      MCSection *TypeSection, MCSection *StrSection, const MCExpr *SymExpr);

private:
  uint64_t Start;
  uint64_t End;
  bool IsFuncScope;
  std::vector<VarInfo*> Vars;
  std::vector<LexicalScope> InnerScopes;
};

void LexicalScope::AddVar(VarInfo *Info) {
  if (Info->IsParam() && IsFuncScope) {
    Vars.push_back(Info);
    return;
  }

  if (!IsContains(Info))
    return;

  uint64_t VarStart = Info->GetStartOffset();
  uint64_t VarEnd = Info->GetEndOffset();

  // Var belongs to inner scope
  if (VarStart != Start || VarEnd != End) {
    // Try to add variable to one the inner scopes
    for (auto &Scope : InnerScopes) {
      if (Scope.IsContains(Info)) {
        Scope.AddVar(Info);
        return;
      }
    }
    // We need to create new inner scope for this var
    InnerScopes.emplace_back(Info);
  } else {
    Vars.push_back(Info);
  }
}

void LexicalScope::Dump(UserDefinedDwarfTypesBuilder *TypeBuilder, MCObjectStreamer *Streamer,
      MCSection *TypeSection, MCSection *StrSection, const MCExpr *SymExpr) {
  Streamer->SwitchSection(TypeSection);

  if (!IsFuncScope)
  {
      // Dump lexical block DIE
      MCContext &context = Streamer->getContext();
      unsigned TargetPointerSize = context.getAsmInfo()->getCodePointerSize();

      // Abbrev Number
      Streamer->EmitULEB128IntValue(DwarfAbbrev::LexicalBlock);

      // DW_AT_low_pc
      const MCExpr *StartExpr = MCConstantExpr::create(Start, context);
      const MCExpr *LowPcExpr = MCBinaryExpr::create(MCBinaryExpr::Add, SymExpr,
          StartExpr, context);
      Streamer->EmitValue(LowPcExpr, TargetPointerSize);

      // DW_AT_high_pc
      Streamer->EmitIntValue(End - Start, TargetPointerSize);
  }

  for (auto *Var : Vars) {
    Var->Dump(TypeBuilder, Streamer, TypeSection, StrSection);
  }

  for (auto &Scope : InnerScopes) {
    Scope.Dump(TypeBuilder, Streamer, TypeSection, StrSection, SymExpr);
  }

  if (!IsFuncScope) {
    // Terminate block
    Streamer->EmitIntValue(0, 1);
  }
}

// VarInfo

VarInfo::VarInfo(const DebugVarInfo &Info, bool IsThis) :
                 DebugInfo(Info),
                 LocSymbol(nullptr),
                 IsThis(IsThis) {
  if (!Info.IsParam) {
    assert(!Info.Ranges.empty());
    StartOffset = Info.Ranges.front().startOffset;
    EndOffset = Info.Ranges.back().endOffset;
  } else {
    // Params belong to func scope
    StartOffset = 0xFFFFFFFF;
    EndOffset = 0xFFFFFFFF;
  }
}

void VarInfo::DumpLocsIfNeeded(MCObjectStreamer *Streamer,
                               MCSection *LocSection,
                               const MCExpr *SymExpr) {
  if (!IsDebugLocNeeded())
    return;

  Streamer->SwitchSection(LocSection);

  MCContext &context = Streamer->getContext();
  unsigned TargetPointerSize = context.getAsmInfo()->getCodePointerSize();

  LocSymbol = context.createTempSymbol();
  Streamer->EmitLabel(LocSymbol);

  for (const auto &NativeInfo : DebugInfo.Ranges) {
    const MCExpr *StartOffsetExpr = MCConstantExpr::create(NativeInfo.startOffset, context);
    const MCExpr *EndOffsetExpr = MCConstantExpr::create(NativeInfo.endOffset, context);

    // Begin address
    const MCExpr *BeginAddrExpr = MCBinaryExpr::create(MCBinaryExpr::Add, SymExpr,
        StartOffsetExpr, context);
    Streamer->EmitValue(BeginAddrExpr, TargetPointerSize);

    // End address
    const MCExpr *EndAddrExpr = MCBinaryExpr::create(MCBinaryExpr::Add, SymExpr,
        EndOffsetExpr, context);
    Streamer->EmitValue(EndAddrExpr, TargetPointerSize);

    // Expression
    EmitVarLocation(Streamer, NativeInfo, true);
  }

  // Terminate list entry
  Streamer->EmitIntValue(0, TargetPointerSize);
  Streamer->EmitIntValue(0, TargetPointerSize);
}

void VarInfo::DumpStrings(MCObjectStreamer *Streamer) {
  if (IsThis) {
    Streamer->EmitBytes(StringRef("this"));
  } else {
    Streamer->EmitBytes(StringRef(DebugInfo.Name));
  }
  Streamer->EmitIntValue(0, 1);
}

void VarInfo::DumpTypeInfo(MCObjectStreamer *Streamer, UserDefinedDwarfTypesBuilder *TypeBuilder) {
  bool IsDebugLocUsed = IsDebugLocNeeded();

  // Abbrev Number
  if (DebugInfo.IsParam) {
    if (IsThis) {
      Streamer->EmitULEB128IntValue(IsDebugLocUsed ? DwarfAbbrev::FormalParameterThisLoc :
          DwarfAbbrev::FormalParameterThis);
    } else {
      Streamer->EmitULEB128IntValue(IsDebugLocUsed ? DwarfAbbrev::FormalParameterLoc :
          DwarfAbbrev::FormalParameter);
    }
  } else {
    Streamer->EmitULEB128IntValue(IsDebugLocUsed ? DwarfAbbrev::VariableLoc :
          DwarfAbbrev::Variable);
  }

  // DW_AT_name
  EmitSectionOffset(Streamer, StrSymbol, 4);

  // DW_AT_decl_file
  Streamer->EmitIntValue(1, 1);

  // DW_AT_decl_line
  Streamer->EmitIntValue(1, 1);

  // DW_AT_type
  DwarfInfo *Info = TypeBuilder->GetTypeInfoByIndex(DebugInfo.TypeIndex);
  assert(Info != nullptr);

  EmitInfoOffset(Streamer, Info, 4);

  // DW_AT_location
  if (IsDebugLocUsed) {
    EmitSectionOffset(Streamer, LocSymbol, 4);
  } else {
    assert(DebugInfo.Ranges.size() == 1);
    EmitVarLocation(Streamer, DebugInfo.Ranges[0]);
  }
}

// SubprogramInfo

SubprogramInfo::SubprogramInfo(const char *Name,
                               int Size,
                               DwarfMemberFunctionIdTypeInfo *MethodTypeInfo,
                               const std::vector<DebugVarInfo> &DebugVarInfos,
                               const std::vector<DebugEHClauseInfo> &DebugEHClauseInfos) :
                               Name(Name),
                               Size(Size),
                               MethodTypeInfo(MethodTypeInfo),
                               DebugEHClauseInfos(DebugEHClauseInfos) {
  bool IsStatic = MethodTypeInfo->IsStatic();
  for (unsigned i = 0; i < DebugVarInfos.size(); i++) {
    VarInfos.emplace_back(DebugVarInfos[i], i == 0 && !IsStatic);
  }
}

void SubprogramInfo::Dump(UserDefinedDwarfTypesBuilder *TypeBuilder, MCObjectStreamer *Streamer,
    MCSection *TypeSection, MCSection *StrSection, MCSection *LocSection) {
  DumpDebugLoc(Streamer, LocSection);

  DwarfInfo::Dump(TypeBuilder, Streamer, TypeSection, StrSection);

  // Dump vars
  DumpVars(TypeBuilder, Streamer, TypeSection, StrSection);

  // Dump try-catch blocks
  Streamer->SwitchSection(TypeSection);
  DumpEHClauses(Streamer, TypeSection);

  // Terminate subprogram DIE
  Streamer->EmitIntValue(0, 1);
}

void SubprogramInfo::DumpTypeInfo(MCObjectStreamer *Streamer, UserDefinedDwarfTypesBuilder *TypeBuilder) {
  MCContext &context = Streamer->getContext();
  bool IsStatic = MethodTypeInfo->IsStatic();
  unsigned TargetPointerSize = context.getAsmInfo()->getCodePointerSize();
  Triple::ArchType ArchType = context.getObjectFileInfo()->getTargetTriple().getArch();

  // Subprogram DIE

  // Abbrev Number
  Streamer->EmitULEB128IntValue(IsStatic ? DwarfAbbrev::SubprogramStatic :
      DwarfAbbrev::Subprogram);

  // DW_AT_specification
  EmitInfoOffset(Streamer, MethodTypeInfo, 4);

  // DW_AT_low_pc
  MCSymbol *Sym = context.getOrCreateSymbol(Twine(Name));
  const MCExpr *SymExpr = MCSymbolRefExpr::create(Sym, MCSymbolRefExpr::VK_None, context);
  Streamer->EmitValue(SymExpr, TargetPointerSize);

  // DW_AT_high_pc
  Streamer->EmitIntValue(Size, TargetPointerSize);

  // DW_AT_frame_base
  Streamer->EmitULEB128IntValue(1);
  Streamer->EmitIntValue(GetDwarfFpRegNum(ArchType) + dwarf::DW_OP_reg0, 1);

  if (!IsStatic) {
    // DW_AT_object_pointer
    uint32_t Offset = Streamer->getOrCreateDataFragment()->getContents().size();

    Streamer->EmitIntValue(Offset + 4, 4);
  }
}

void SubprogramInfo::DumpDebugLoc(MCObjectStreamer *Streamer, MCSection *LocSection) {
  MCContext &context = Streamer->getContext();
  MCSymbol *Sym = context.getOrCreateSymbol(Twine(Name));
  const MCExpr *SymExpr = MCSymbolRefExpr::create(Sym, MCSymbolRefExpr::VK_None, context);

  for (auto &VarInfo : VarInfos) {
    VarInfo.DumpLocsIfNeeded(Streamer, LocSection, SymExpr);
  }
}

void SubprogramInfo::DumpVars(UserDefinedDwarfTypesBuilder *TypeBuilder, MCObjectStreamer *Streamer,
      MCSection *TypeSection, MCSection *StrSection) {
  MCContext &context = Streamer->getContext();
  MCSymbol *Sym = context.getOrCreateSymbol(Twine(Name));
  const MCExpr *SymExpr = MCSymbolRefExpr::create(Sym, MCSymbolRefExpr::VK_None, context);

  LexicalScope FuncScope(0, Size, true);

  for (unsigned i = 0; i < VarInfos.size(); i++) {
    FuncScope.AddVar(&VarInfos[i]);
  }

  FuncScope.Dump(TypeBuilder, Streamer, TypeSection, StrSection, SymExpr);
}

static void DumpEHClause(MCObjectStreamer *Streamer, MCSection *TypeSection, int Abbrev,
                         const MCExpr *SymExpr, unsigned Offset, unsigned Length) {
  MCContext &context = Streamer->getContext();
  unsigned TargetPointerSize = context.getAsmInfo()->getCodePointerSize();

  // Abbrev Number
  Streamer->EmitULEB128IntValue(Abbrev);

  // DW_AT_low_pc
  const MCExpr *OffsetExpr = MCConstantExpr::create(Offset, context);
  const MCExpr *AddrExpr = MCBinaryExpr::create(MCBinaryExpr::Add, SymExpr,
      OffsetExpr, context);

  Streamer->EmitValue(AddrExpr, TargetPointerSize);

  // DW_AT_high_pc
  Streamer->EmitIntValue(Length, TargetPointerSize);
}

void SubprogramInfo::DumpEHClauses(MCObjectStreamer *Streamer, MCSection *TypeSection) {
  MCContext &context = Streamer->getContext();

  MCSymbol *Sym = context.getOrCreateSymbol(Twine(Name));
  const MCExpr *SymExpr = MCSymbolRefExpr::create(Sym, MCSymbolRefExpr::VK_None, context);

  for (const auto &EHClause: DebugEHClauseInfos) {
    // Try block DIE
    DumpEHClause(Streamer, TypeSection, DwarfAbbrev::TryBlock,
        SymExpr, EHClause.TryOffset, EHClause.TryLength);

    // Catch block DIE
    DumpEHClause(Streamer, TypeSection, DwarfAbbrev::CatchBlock,
        SymExpr, EHClause.HandlerOffset, EHClause.HandlerLength);
  }
}

// DwarfGen

void DwarfGen::SetTypeBuilder(UserDefinedDwarfTypesBuilder *TypeBuilder) {
  assert(this->TypeBuilder == nullptr);
  assert(TypeBuilder != nullptr);
  this->TypeBuilder = TypeBuilder;
  this->Streamer = TypeBuilder->GetStreamer();
}

void DwarfGen::EmitCompileUnit() {
  MCContext &context = Streamer->getContext();

  MCSymbol *LineSectionSymbol = nullptr;
  MCSymbol *AbbrevSectionSymbol = nullptr;
  if (context.getAsmInfo()->doesDwarfUseRelocationsAcrossSections()) {
    LineSectionSymbol = Streamer->getDwarfLineTableSymbol(0);

    Streamer->SwitchSection(context.getObjectFileInfo()->getDwarfAbbrevSection());
    AbbrevSectionSymbol = context.createTempSymbol();
    Streamer->EmitLabel(AbbrevSectionSymbol);
  }

  MCSection *debugSection = context.getObjectFileInfo()->getDwarfInfoSection();
  Streamer->SwitchSection(debugSection);

  InfoStart = debugSection->getBeginSymbol();
  InfoEnd = context.createTempSymbol();

  // Length
  const MCExpr *Length = MakeStartMinusEndExpr(*Streamer, *InfoStart, *InfoEnd, 4);
  emitAbsValue(*Streamer, Length, 4);

  // Version
  Streamer->EmitIntValue(context.getDwarfVersion(), 2);

  // Unit type, Addr Size and Abbrev offset - DWARF >= 5
  // Abbrev offset, Addr Size               - DWARF <= 4
  unsigned addrSize = context.getAsmInfo()->getCodePointerSize();
  if (context.getDwarfVersion() >= 5) {
    Streamer->EmitIntValue(dwarf::DW_UT_compile, 1);
    Streamer->EmitIntValue(addrSize, 1);
  }

  // Abbrev Offset
  if (AbbrevSectionSymbol == nullptr) {
    Streamer->EmitIntValue(0, 4);
  } else {
    Streamer->EmitSymbolValue(AbbrevSectionSymbol, 4,
        context.getAsmInfo()->needsDwarfSectionOffsetDirective());
  }

  if (context.getDwarfVersion() <= 4)
    Streamer->EmitIntValue(addrSize, 1);

  // CompileUnit DIE

  // Abbrev Number
  Streamer->EmitULEB128IntValue(DwarfAbbrev::CompileUnit);

  // DW_AT_producer: CoreRT
  Streamer->EmitBytes(StringRef("CoreRT"));
  Streamer->EmitIntValue(0, 1);

  // DW_AT_language
#ifdef FEATURE_LANGID_CS
  Streamer->EmitIntValue(DW_LANG_MICROSOFT_CSHARP, 2);
#else
  Streamer->EmitIntValue(dwarf::DW_LANG_C89, 2);
#endif

  // DW_AT_stmt_list
  if (LineSectionSymbol == nullptr) {
    Streamer->EmitIntValue(0, 4);
  } else {
    Streamer->EmitSymbolValue(LineSectionSymbol, 4,
        context.getAsmInfo()->needsDwarfSectionOffsetDirective());
  }
}

void DwarfGen::EmitSubprogramInfo(const char *FunctionName,
                                  int FunctionSize,
                                  unsigned MethodTypeIndex,
                                  const std::vector<DebugVarInfo> &VarInfos,
                                  const std::vector<DebugEHClauseInfo> &DebugEHClauseInfos) {
  // return if CU isn't emitted
  if (InfoStart == nullptr)
    return;

  if (MethodTypeIndex == 0)
    return;

  DwarfMemberFunctionIdTypeInfo *MethodTypeInfo = static_cast<DwarfMemberFunctionIdTypeInfo*>(
      TypeBuilder->GetTypeInfoByIndex(MethodTypeIndex));
  assert(MethodTypeInfo != nullptr);

  MethodTypeInfo->SetLinkageName(FunctionName);

  Subprograms.emplace_back(FunctionName, FunctionSize, MethodTypeInfo, VarInfos, DebugEHClauseInfos);
}

void DwarfGen::EmitAbbrev() {
  // return if CU isn't emitted
  if (InfoStart == nullptr)
    return;

  MCContext &context = Streamer->getContext();

  DwarfAbbrev::Dump(Streamer, context.getDwarfVersion(),
      context.getAsmInfo()->getCodePointerSize());
}

void DwarfGen::EmitAranges() {
  // return if CU isn't emitted
  if (InfoStart == nullptr)
    return;

  MCContext &context = Streamer->getContext();
  Streamer->SwitchSection(context.getObjectFileInfo()->getDwarfARangesSection());

  auto &Sections = context.getGenDwarfSectionSyms();

  int Length = 4 + 2 + 4 + 1 + 1;
  int AddrSize = context.getAsmInfo()->getCodePointerSize();
  int Pad = 2 * AddrSize - (Length & (2 * AddrSize - 1));
  if (Pad == 2 * AddrSize)
    Pad = 0;
  Length += Pad;

  Length += 2 * AddrSize * Sections.size();
  Length += 2 * AddrSize;

  // Emit the header for this section.
  // The 4 byte length not including the 4 byte value for the length.
  Streamer->EmitIntValue(Length - 4, 4);

  // he 2 byte version, which is 2.
  Streamer->EmitIntValue(2, 2);

  // The 4 byte offset to the compile unit in the .debug_info from the start
  // of the .debug_info.
  Streamer->EmitSymbolValue(InfoStart, 4,
                            context.getAsmInfo()->needsDwarfSectionOffsetDirective());

  Streamer->EmitIntValue(AddrSize, 1);

  Streamer->EmitIntValue(0, 1);

  for(int i = 0; i < Pad; i++)
    Streamer->EmitIntValue(0, 1);

  for (MCSection *Sec : Sections) {
    const MCSymbol *StartSymbol = Sec->getBeginSymbol();
    MCSymbol *EndSymbol = Sec->getEndSymbol(context);
    assert(StartSymbol && "StartSymbol must not be NULL");
    assert(EndSymbol && "EndSymbol must not be NULL");

    const MCExpr *Addr = MCSymbolRefExpr::create(
      StartSymbol, MCSymbolRefExpr::VK_None, context);
    const MCExpr *Size = MakeStartMinusEndExpr(*Streamer,
      *StartSymbol, *EndSymbol, 0);
    Streamer->EmitValue(Addr, AddrSize);
    emitAbsValue(*Streamer, Size, AddrSize);
  }

  // Terminating zeros.
  Streamer->EmitIntValue(0, AddrSize);
  Streamer->EmitIntValue(0, AddrSize);
}

void DwarfGen::Finish() {
  // return if CU isn't emitted
  if (InfoStart == nullptr)
    return;

  MCContext &context = Streamer->getContext();

  // Dump type info

  MCSection *InfoSection = context.getObjectFileInfo()->getDwarfInfoSection();
  MCSection *StrSection = context.getObjectFileInfo()->getDwarfStrSection();
  MCSection *LocSection = context.getObjectFileInfo()->getDwarfLocSection();

  TypeBuilder->EmitTypeInformation(InfoSection, StrSection);

  // Dump subprograms

  for (auto &Subprogram : Subprograms) {
    Subprogram.Dump(TypeBuilder, Streamer, InfoSection, StrSection, LocSection);
  }

  // Add the NULL terminating the Compile Unit DIE's.
  Streamer->SwitchSection(context.getObjectFileInfo()->getDwarfInfoSection());

  Streamer->EmitIntValue(0, 1);

  Streamer->EmitLabel(InfoEnd);

  Streamer->SwitchSection(context.getObjectFileInfo()->getDwarfAbbrevSection());

  // Terminate the abbreviations for this compilation unit
  Streamer->EmitIntValue(0, 1);
}