//===---- objwriter.cpp -----------------------------------------*- C++ -*-===//
//
// object writer
//
// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
//
//===----------------------------------------------------------------------===//
///
/// \file
/// \brief Implementation of object writer API for JIT/AOT
///
//===----------------------------------------------------------------------===//

#include "objwriter.h"
#include "debugInfo/dwarf/dwarfTypeBuilder.h"
#include "debugInfo/codeView/codeViewTypeBuilder.h"
#include "llvm/DebugInfo/CodeView/CodeView.h"
#include "llvm/DebugInfo/CodeView/Line.h"
#include "llvm/DebugInfo/CodeView/SymbolRecord.h"
#include "llvm/MC/MCAsmBackend.h"
#include "llvm/MC/MCAsmInfo.h"
#include "llvm/MC/MCContext.h"
#include "llvm/MC/MCDwarf.h"
#include "llvm/MC/MCInstPrinter.h"
#include "llvm/MC/MCInstrInfo.h"
#include "llvm/MC/MCParser/AsmLexer.h"
#include "llvm/MC/MCParser/MCTargetAsmParser.h"
#include "llvm/MC/MCRegisterInfo.h"
#include "llvm/MC/MCSectionCOFF.h"
#include "llvm/MC/MCSectionELF.h"
#include "llvm/MC/MCSectionMachO.h"
#include "llvm/MC/MCObjectStreamer.h"
#include "llvm/MC/MCSubtargetInfo.h"
#include "llvm/MC/MCTargetOptionsCommandFlags.h"
#include "llvm/MC/MCELFStreamer.h"
#include "llvm/BinaryFormat/COFF.h"
#include "llvm/Support/CommandLine.h"
#include "llvm/Support/Compression.h"
#include "llvm/BinaryFormat/ELF.h"
#include "llvm/Support/FileUtilities.h"
#include "llvm/Support/FormattedStream.h"
#include "llvm/Support/Host.h"
#include "llvm/Support/ManagedStatic.h"
#include "llvm/Support/MemoryBuffer.h"
#include "llvm/Support/PrettyStackTrace.h"
#include "llvm/Support/SourceMgr.h"
#include "llvm/Support/TargetRegistry.h"
#include "llvm/Support/TargetSelect.h"
#include "llvm/Support/ToolOutputFile.h"
#include "llvm/Support/Win64EH.h"
#include "llvm/Target/TargetMachine.h"
#include "..\..\..\lib\Target\AArch64\MCTargetDesc\AArch64MCExpr.h"

using namespace llvm;
using namespace llvm::codeview;

bool error(const Twine &Error) {
  errs() << Twine("error: ") + Error + "\n";
  return false;
}

void ObjectWriter::InitTripleName(const char* tripleName) {
  TripleName = tripleName != nullptr ? tripleName : sys::getDefaultTargetTriple();
}

Triple ObjectWriter::GetTriple() {
  Triple TheTriple(TripleName);

  if (TheTriple.getOS() == Triple::OSType::Darwin) {
    TheTriple = Triple(
        TheTriple.getArchName(), TheTriple.getVendorName(), "darwin",
        TheTriple
            .getEnvironmentName()); // it is workaround for llvm bug
                                    // https://bugs.llvm.org//show_bug.cgi?id=24927.
  }
  return TheTriple;
}

bool ObjectWriter::Init(llvm::StringRef ObjectFilePath, const char* tripleName) {
  llvm_shutdown_obj Y; // Call llvm_shutdown() on exit.

  // Initialize targets
  InitializeAllTargets();
  InitializeAllTargetMCs();
  InitializeAllAsmPrinters();
  
  TargetMOptions = InitMCTargetOptionsFromFlags();

  InitTripleName(tripleName);
  Triple TheTriple = GetTriple();

  // Get the target specific parser.
  std::string TargetError;
  const Target *TheTarget =
      TargetRegistry::lookupTarget(TripleName, TargetError);
  if (!TheTarget) {
    return error("Unable to create target for " + ObjectFilePath + ": " +
                 TargetError);
  }

  std::error_code EC;
  OS.reset(new raw_fd_ostream(ObjectFilePath, EC, sys::fs::F_None));
  if (EC)
    return error("Unable to create file for " + ObjectFilePath + ": " +
                 EC.message());

  RegisterInfo.reset(TheTarget->createMCRegInfo(TripleName));
  if (!RegisterInfo)
    return error("Unable to create target register info!");

  AsmInfo.reset(TheTarget->createMCAsmInfo(*RegisterInfo, TripleName));
  if (!AsmInfo)
    return error("Unable to create target asm info!");

  ObjFileInfo.reset(new MCObjectFileInfo);
  OutContext.reset(
      new MCContext(AsmInfo.get(), RegisterInfo.get(), ObjFileInfo.get()));
  ObjFileInfo->InitMCObjectFileInfo(TheTriple, false, CodeModel::Default,
                                    *OutContext);

  InstrInfo.reset(TheTarget->createMCInstrInfo());
  if (!InstrInfo)
    return error("no instr info info for target " + TripleName);

  std::string FeaturesStr;
  std::string MCPU;
  SubtargetInfo.reset(
      TheTarget->createMCSubtargetInfo(TripleName, MCPU, FeaturesStr));
  if (!SubtargetInfo)
    return error("no subtarget info for target " + TripleName);

  CodeEmitter =
      TheTarget->createMCCodeEmitter(*InstrInfo, *RegisterInfo, *OutContext);
  if (!CodeEmitter)
    return error("no code emitter for target " + TripleName);

  AsmBackend = TheTarget->createMCAsmBackend(*RegisterInfo, TripleName, MCPU,
                                             TargetMOptions);
  if (!AsmBackend)
    return error("no asm backend for target " + TripleName);

  Streamer = (MCObjectStreamer *)TheTarget->createMCObjectStreamer(
      TheTriple, *OutContext, *AsmBackend, *OS, CodeEmitter, *SubtargetInfo,
      RelaxAll,
      /*IncrementalLinkerCompatible*/ false,
      /*DWARFMustBeAtTheEnd*/ false);
  if (!Streamer)
    return error("no object streamer for target " + TripleName);
  Assembler = &Streamer->getAssembler();

  TMachine.reset(TheTarget->createTargetMachine(TripleName, MCPU, FeaturesStr,
                                                TargetOptions(), None));
  if (!TMachine)
    return error("no target machine for target " + TripleName);

  AssemblerPrinter.reset(TheTarget->createAsmPrinter(
      *TMachine, std::unique_ptr<MCStreamer>(Streamer)));
  if (!AssemblerPrinter)
    return error("no asm printer for target " + TripleName);

  FrameOpened = false;
  FuncId = 1;

  SetCodeSectionAttribute("text", CustomSectionAttributes_Executable, nullptr);

  if (ObjFileInfo->getObjectFileType() == ObjFileInfo->IsCOFF) {
    TypeBuilder.reset(new UserDefinedCodeViewTypesBuilder());
  } else {
    TypeBuilder.reset(new UserDefinedDwarfTypesBuilder());
  }

  TypeBuilder->SetStreamer(Streamer);
  unsigned TargetPointerSize = AssemblerPrinter->getPointerSize();
  TypeBuilder->SetTargetPointerSize(TargetPointerSize);

  if (ObjFileInfo->getObjectFileType() == ObjFileInfo->IsELF) {
    DwarfGenerator.reset(new DwarfGen());
    DwarfGenerator->SetTypeBuilder(static_cast<UserDefinedDwarfTypesBuilder*>(TypeBuilder.get()));
  }

  CFIsPerOffset.set_size(0);

  return true;
}

void ObjectWriter::Finish() { Streamer->Finish(); }

void ObjectWriter::SwitchSection(const char *SectionName,
                                 CustomSectionAttributes attributes,
                                 const char *ComdatName) {
  MCSection *Section = GetSection(SectionName, attributes, ComdatName);
  Streamer->SwitchSection(Section);
  if (Sections.count(Section) == 0) {
    Sections.insert(Section);
    if (ObjFileInfo->getObjectFileType() == ObjFileInfo->IsMachO) {
      assert(!Section->getBeginSymbol());
      // Output a DWARF linker-local symbol.
      // This symbol is used as a base for other symbols in a section.
      MCSymbol *SectionStartSym = OutContext->createTempSymbol();
      Streamer->EmitLabel(SectionStartSym);
      Section->setBeginSymbol(SectionStartSym);
    }
  }
}

MCSection *ObjectWriter::GetSection(const char *SectionName,
                                    CustomSectionAttributes attributes,
                                    const char *ComdatName) {
  MCSection *Section = nullptr;

  if (strcmp(SectionName, "text") == 0) {
    Section = ObjFileInfo->getTextSection();
  } else if (strcmp(SectionName, "data") == 0) {
    Section = ObjFileInfo->getDataSection();
  } else if (strcmp(SectionName, "rdata") == 0) {
    Section = ObjFileInfo->getReadOnlySection();
  } else if (strcmp(SectionName, "xdata") == 0) {
    Section = ObjFileInfo->getXDataSection();
  } else if (strcmp(SectionName, "bss") == 0) {
    if (ObjFileInfo->getObjectFileType() == ObjFileInfo->IsMachO) {
      Section = ObjFileInfo->getDataBSSSection();
    } else {
      Section = ObjFileInfo->getBSSSection();
    } 
  } else {
    Section = GetSpecificSection(SectionName, attributes, ComdatName);
  }
  assert(Section);
  return Section;
}

MCSection *ObjectWriter::GetSpecificSection(const char *SectionName,
                                            CustomSectionAttributes attributes,
                                            const char *ComdatName) {
  Triple TheTriple(TripleName);
  MCSection *Section = nullptr;
  SectionKind Kind = (attributes & CustomSectionAttributes_Executable)
                         ? SectionKind::getText()
                         : (attributes & CustomSectionAttributes_Writeable)
                               ? SectionKind::getData()
                               : SectionKind::getReadOnly();
  switch (TheTriple.getObjectFormat()) {
  case Triple::MachO: {
    unsigned typeAndAttributes = 0;
    if (attributes & CustomSectionAttributes_MachO_Init_Func_Pointers) {
      typeAndAttributes |= MachO::SectionType::S_MOD_INIT_FUNC_POINTERS;
    }
    Section = OutContext->getMachOSection(
        (attributes & CustomSectionAttributes_Executable) ? "__TEXT" : "__DATA",
        SectionName, typeAndAttributes, Kind);
    break;
  }
  case Triple::COFF: {
    unsigned Characteristics = COFF::IMAGE_SCN_MEM_READ;

    if (attributes & CustomSectionAttributes_Executable) {
      Characteristics |= COFF::IMAGE_SCN_CNT_CODE | COFF::IMAGE_SCN_MEM_EXECUTE;
    } else if (attributes & CustomSectionAttributes_Writeable) {
      Characteristics |=
          COFF::IMAGE_SCN_CNT_INITIALIZED_DATA | COFF::IMAGE_SCN_MEM_WRITE;
    } else {
      Characteristics |= COFF::IMAGE_SCN_CNT_INITIALIZED_DATA;
    }

    if (ComdatName != nullptr) {
      Section = OutContext->getCOFFSection(
          SectionName, Characteristics | COFF::IMAGE_SCN_LNK_COMDAT, Kind,
          ComdatName, COFF::COMDATType::IMAGE_COMDAT_SELECT_ANY);
    } else {
      Section = OutContext->getCOFFSection(SectionName, Characteristics, Kind);
    }
    break;
  }
  case Triple::ELF: {
    unsigned Flags = ELF::SHF_ALLOC;
    if (ComdatName != nullptr) {
      MCSymbolELF *GroupSym =
          cast<MCSymbolELF>(OutContext->getOrCreateSymbol(ComdatName));
      OutContext->createELFGroupSection(GroupSym);
      Flags |= ELF::SHF_GROUP;
    }
    if (attributes & CustomSectionAttributes_Executable) {
      Flags |= ELF::SHF_EXECINSTR;
    } else if (attributes & CustomSectionAttributes_Writeable) {
      Flags |= ELF::SHF_WRITE;
    }
    Section =
        OutContext->getELFSection(SectionName, ELF::SHT_PROGBITS, Flags, 0,
                                  ComdatName != nullptr ? ComdatName : "");
    break;
  }
  default:
    error("Unknown output format for target " + TripleName);
    break;
  }
  return Section;
}

void ObjectWriter::SetCodeSectionAttribute(const char *SectionName,
                                           CustomSectionAttributes attributes,
                                           const char *ComdatName) {
  MCSection *Section = GetSection(SectionName, attributes, ComdatName);

  assert(!Section->hasInstructions());
  Section->setHasInstructions(true);
  if (ObjFileInfo->getObjectFileType() != ObjFileInfo->IsCOFF) {
    OutContext->addGenDwarfSection(Section);
  }
}

void ObjectWriter::EmitAlignment(int ByteAlignment) {
  int64_t fillValue = 0;

  if (TMachine->getTargetTriple().getArch() == llvm::Triple::ArchType::x86 ||
      TMachine->getTargetTriple().getArch() == llvm::Triple::ArchType::x86_64) {
    fillValue = 0x90; // x86 nop
  }

  Streamer->EmitValueToAlignment(ByteAlignment, fillValue);
}

void ObjectWriter::EmitBlob(int BlobSize, const char *Blob) {
  if (Streamer->getCurrentSectionOnly()->getKind().isText()) {
    Streamer->EmitInstructionBytes(StringRef(Blob, BlobSize));
  } else {
    Streamer->EmitBytes(StringRef(Blob, BlobSize));
  }
}

void ObjectWriter::EmitIntValue(uint64_t Value, unsigned Size) {
  Streamer->EmitIntValue(Value, Size);
}

void ObjectWriter::EmitSymbolDef(const char *SymbolName, bool global) {
  MCSymbol *Sym = OutContext->getOrCreateSymbol(Twine(SymbolName));

  if (global) {
    Streamer->EmitSymbolAttribute(Sym, MCSA_Global);
  } else {
    Streamer->EmitSymbolAttribute(Sym, MCSA_Local);
  }

  Triple TheTriple = TMachine->getTargetTriple();

  // A Thumb2 function symbol should be marked with an appropriate ELF
  // attribute to make later computation of a relocation address value correct

  if (TheTriple.getObjectFormat() == Triple::ELF &&
      Streamer->getCurrentSectionOnly()->getKind().isText()) {
      switch (TheTriple.getArch()) {
        case Triple::thumb:
        case Triple::aarch64:
          Streamer->EmitSymbolAttribute(Sym, MCSA_ELF_TypeFunction);
          break;

        default:
          break;
    }
  }

  Streamer->EmitLabel(Sym);
}

const MCSymbolRefExpr *
ObjectWriter::GetSymbolRefExpr(const char *SymbolName,
                               MCSymbolRefExpr::VariantKind Kind) {
  // Create symbol reference
  MCSymbol *T = OutContext->getOrCreateSymbol(SymbolName);
  Assembler->registerSymbol(*T);
  return MCSymbolRefExpr::create(T, Kind, *OutContext);
}



unsigned ObjectWriter::GetDFSize() {
  return Streamer->getOrCreateDataFragment()->getContents().size();
}

bool ObjectWriter::EmitRelocDirective(const int Offset, StringRef Name, const MCExpr *Expr) {
  const MCExpr *OffsetExpr = MCConstantExpr::create(Offset, *OutContext);
  return Streamer->EmitRelocDirective(*OffsetExpr, Name, Expr, SMLoc());
}

const MCExpr *ObjectWriter::GenTargetExpr(const char *SymbolName,
                                          MCSymbolRefExpr::VariantKind Kind,
                                          int Delta, bool IsPCRel, int Size) {
  const MCExpr *TargetExpr = GetSymbolRefExpr(SymbolName, Kind);
  if (IsPCRel && Size != 0) {
    // If the fixup is pc-relative, we need to bias the value to be relative to
    // the start of the field, not the end of the field
    TargetExpr = MCBinaryExpr::createSub(
        TargetExpr, MCConstantExpr::create(Size, *OutContext), *OutContext);
  }
  if (Delta != 0) {
    TargetExpr = MCBinaryExpr::createAdd(
        TargetExpr, MCConstantExpr::create(Delta, *OutContext), *OutContext);
  }
  return TargetExpr;
}

int ObjectWriter::EmitSymbolRef(const char *SymbolName,
                                RelocType RelocationType, int Delta) {
  bool IsPCRel = false;
  int Size = 0;
  MCSymbolRefExpr::VariantKind Kind = MCSymbolRefExpr::VK_None;

  // Convert RelocationType to MCSymbolRefExpr
  switch (RelocationType) {
  case RelocType::IMAGE_REL_BASED_ABSOLUTE:
    assert(ObjFileInfo->getObjectFileType() == ObjFileInfo->IsCOFF);
    Kind = MCSymbolRefExpr::VK_COFF_IMGREL32;
    Size = 4;
    break;
  case RelocType::IMAGE_REL_BASED_HIGHLOW:
    Size = 4;
    break;
  case RelocType::IMAGE_REL_BASED_DIR64:
    Size = 8;
    break;
  case RelocType::IMAGE_REL_BASED_REL32: {
    Size = 4;
    IsPCRel = true;
    if (ObjFileInfo->getObjectFileType() == ObjFileInfo->IsELF) {
      // PLT is valid only for code symbols,
      // but there shouldn't be references to global data symbols
      Kind = MCSymbolRefExpr::VK_PLT;
    }
    break;
  }
  case RelocType::IMAGE_REL_BASED_RELPTR32:
    Size = 4;
    IsPCRel = true;
    Delta += 4; // size of C# (int) type is always 4 bytes
    break;
  case RelocType::IMAGE_REL_BASED_THUMB_MOV32: {
    const unsigned Offset = GetDFSize();
    const MCExpr *TargetExpr = GenTargetExpr(SymbolName, Kind, Delta);
    EmitRelocDirective(Offset, "R_ARM_THM_MOVW_ABS_NC", TargetExpr);
    EmitRelocDirective(Offset + 4, "R_ARM_THM_MOVT_ABS", TargetExpr);
    return 8;
  }
  case RelocType::IMAGE_REL_BASED_THUMB_BRANCH24: {
    const MCExpr *TargetExpr = GenTargetExpr(SymbolName, Kind, Delta);
    EmitRelocDirective(GetDFSize(), "R_ARM_THM_JUMP24", TargetExpr);
    return 4;
  }
  case RelocType::IMAGE_REL_BASED_ARM64_BRANCH26: {
    const MCExpr *TargetExpr = GenTargetExpr(SymbolName, Kind, Delta);
    EmitRelocDirective(GetDFSize(), "R_AARCH64_JUMP26", TargetExpr);
    return 4;
  }
  case RelocType::IMAGE_REL_BASED_ARM64_PAGEBASE_REL21: {
    const MCExpr *TargetExpr = GenTargetExpr(SymbolName, Kind, Delta);
    TargetExpr =
        AArch64MCExpr::create(TargetExpr, AArch64MCExpr::VK_CALL, *OutContext);
    EmitRelocDirective(GetDFSize(), "R_AARCH64_ADR_PREL_LO21", TargetExpr);
    return 4;
  }
  case RelocType::IMAGE_REL_BASED_ARM64_PAGEOFFSET_12A: {
    const MCExpr *TargetExpr = GenTargetExpr(SymbolName, Kind, Delta);
    TargetExpr =
        AArch64MCExpr::create(TargetExpr, AArch64MCExpr::VK_LO12, *OutContext);
    EmitRelocDirective(GetDFSize(), "R_AARCH64_ADD_ABS_LO12_NC", TargetExpr);
    return 4;
  }
  }

  const MCExpr *TargetExpr = GenTargetExpr(SymbolName, Kind, Delta, IsPCRel, Size);
  Streamer->EmitValueImpl(TargetExpr, Size, SMLoc(), IsPCRel);
  return Size;
}

void ObjectWriter::EmitWinFrameInfo(const char *FunctionName, int StartOffset,
                                    int EndOffset, const char *BlobSymbolName) {
  assert(ObjFileInfo->getObjectFileType() == ObjFileInfo->IsCOFF);

  // .pdata emission
  MCSection *Section = ObjFileInfo->getPDataSection();

  // If the function was emitted to a Comdat section, create an associative
  // section to place the frame info in. This is due to the Windows linker
  // requirement that a function and its unwind info come from the same
  // object file.
  MCSymbol *Fn = OutContext->getOrCreateSymbol(Twine(FunctionName));
  const MCSectionCOFF *FunctionSection = cast<MCSectionCOFF>(&Fn->getSection());
  if (FunctionSection->getCharacteristics() & COFF::IMAGE_SCN_LNK_COMDAT) {
    Section = OutContext->getAssociativeCOFFSection(
        cast<MCSectionCOFF>(Section), FunctionSection->getCOMDATSymbol());
  }

  Streamer->SwitchSection(Section);
  Streamer->EmitValueToAlignment(4);

  const MCExpr *BaseRefRel =
      GetSymbolRefExpr(FunctionName, MCSymbolRefExpr::VK_COFF_IMGREL32);

  // start Offset
  const MCExpr *StartOfs = MCConstantExpr::create(StartOffset, *OutContext);
  Streamer->EmitValue(
      MCBinaryExpr::createAdd(BaseRefRel, StartOfs, *OutContext), 4);

  // end Offset
  const MCExpr *EndOfs = MCConstantExpr::create(EndOffset, *OutContext);
  Streamer->EmitValue(MCBinaryExpr::createAdd(BaseRefRel, EndOfs, *OutContext),
                      4);

  // frame symbol reference
  Streamer->EmitValue(
      GetSymbolRefExpr(BlobSymbolName, MCSymbolRefExpr::VK_COFF_IMGREL32), 4);
}

void ObjectWriter::EmitCFIStart(int Offset) {
  assert(!FrameOpened && "frame should be closed before CFIStart");
  Streamer->EmitCFIStartProc(false);
  FrameOpened = true;
}

void ObjectWriter::EmitCFIEnd(int Offset) {
  assert(FrameOpened && "frame should be opened before CFIEnd");
  Streamer->EmitCFIEndProc();
  FrameOpened = false;
}

void ObjectWriter::EmitCFILsda(const char *LsdaBlobSymbolName) {
  assert(FrameOpened && "frame should be opened before CFILsda");

  // Create symbol reference
  MCSymbol *T = OutContext->getOrCreateSymbol(LsdaBlobSymbolName);
  Assembler->registerSymbol(*T);
  Streamer->EmitCFILsda(T, llvm::dwarf::Constants::DW_EH_PE_pcrel |
                               llvm::dwarf::Constants::DW_EH_PE_sdata4);
}

void ObjectWriter::EmitCFICode(int Offset, const char *Blob) {
  assert(FrameOpened && "frame should be opened before CFICode");

  const CFI_CODE *CfiCode = (const CFI_CODE *)Blob;
  switch (CfiCode->CfiOpCode) {
  case CFI_ADJUST_CFA_OFFSET:
    assert(CfiCode->DwarfReg == DWARF_REG_ILLEGAL &&
           "Unexpected Register Value for OpAdjustCfaOffset");
    Streamer->EmitCFIAdjustCfaOffset(CfiCode->Offset);
    break;
  case CFI_REL_OFFSET:
    Streamer->EmitCFIRelOffset(CfiCode->DwarfReg, CfiCode->Offset);
    break;
  case CFI_DEF_CFA_REGISTER:
    assert(CfiCode->Offset == 0 &&
           "Unexpected Offset Value for OpDefCfaRegister");
    Streamer->EmitCFIDefCfaRegister(CfiCode->DwarfReg);
    break;
  case CFI_DEF_CFA:
    assert(CfiCode->Offset != 0 &&
           "Unexpected Offset Value for OpDefCfa");
    Streamer->EmitCFIDefCfa(CfiCode->DwarfReg, CfiCode->Offset);
    break;
  default:
    assert(false && "Unrecognized CFI");
    break;
  }
}

void ObjectWriter::EmitLabelDiff(const MCSymbol *From, const MCSymbol *To,
                                 unsigned int Size) {
  MCSymbolRefExpr::VariantKind Variant = MCSymbolRefExpr::VK_None;
  const MCExpr *FromRef = MCSymbolRefExpr::create(From, Variant, *OutContext),
               *ToRef = MCSymbolRefExpr::create(To, Variant, *OutContext);
  const MCExpr *AddrDelta =
      MCBinaryExpr::create(MCBinaryExpr::Sub, ToRef, FromRef, *OutContext);
  Streamer->EmitValue(AddrDelta, Size);
}

void ObjectWriter::EmitSymRecord(int Size, SymbolRecordKind SymbolKind) {
  RecordPrefix Rec;
  Rec.RecordLen = ulittle16_t(Size + sizeof(ulittle16_t));
  Rec.RecordKind = ulittle16_t((uint16_t)SymbolKind);
  Streamer->EmitBytes(StringRef((char *)&Rec, sizeof(Rec)));
}

void ObjectWriter::EmitCOFFSecRel32Value(MCExpr const *Value) {
  MCDataFragment *DF = Streamer->getOrCreateDataFragment();
  MCFixup Fixup = MCFixup::create(DF->getContents().size(), Value, FK_SecRel_4);
  DF->getFixups().push_back(Fixup);
  DF->getContents().resize(DF->getContents().size() + 4, 0);
}

void ObjectWriter::EmitVarDefRange(const MCSymbol *Fn,
                                   const LocalVariableAddrRange &Range) {
  const MCSymbolRefExpr *BaseSym = MCSymbolRefExpr::create(Fn, *OutContext);
  const MCExpr *Offset = MCConstantExpr::create(Range.OffsetStart, *OutContext);
  const MCExpr *Expr = MCBinaryExpr::createAdd(BaseSym, Offset, *OutContext);
  EmitCOFFSecRel32Value(Expr);
  Streamer->EmitCOFFSectionIndex(Fn);
  Streamer->EmitIntValue(Range.Range, 2);
}

void ObjectWriter::EmitCVDebugVarInfo(const MCSymbol *Fn,
                                      const DebugVarInfo LocInfos[],
                                      int NumVarInfos) {
  for (int I = 0; I < NumVarInfos; I++) {
    // Emit an S_LOCAL record
    DebugVarInfo Var = LocInfos[I];
    TypeIndex Type = TypeIndex(Var.TypeIndex);
    LocalSymFlags Flags = LocalSymFlags::None;
    unsigned SizeofSym = sizeof(Type) + sizeof(Flags);
    unsigned NameLength = Var.Name.length() + 1;
    EmitSymRecord(SizeofSym + NameLength, SymbolRecordKind::LocalSym);
    if (Var.IsParam) {
      Flags |= LocalSymFlags::IsParameter;
    }
    Streamer->EmitBytes(StringRef((char *)&Type, sizeof(Type)));
    Streamer->EmitIntValue(static_cast<uint16_t>(Flags), sizeof(Flags));
    Streamer->EmitBytes(StringRef(Var.Name.c_str(), NameLength));

    for (const auto &Range : Var.Ranges) {
      // Emit a range record
      switch (Range.loc.vlType) {
      case ICorDebugInfo::VLT_REG:
      case ICorDebugInfo::VLT_REG_FP: {

        // Currently only support integer registers.
        // TODO: support xmm registers
        if (Range.loc.vlReg.vlrReg >=
            sizeof(cvRegMapAmd64) / sizeof(cvRegMapAmd64[0])) {
          break;
        }
        SymbolRecordKind SymbolKind = SymbolRecordKind::DefRangeRegisterSym;
        unsigned SizeofDefRangeRegisterSym = sizeof(DefRangeRegisterSym::Hdr) +
                                             sizeof(DefRangeRegisterSym::Range);
        EmitSymRecord(SizeofDefRangeRegisterSym, SymbolKind);

        DefRangeRegisterSym DefRangeRegisterSymbol(SymbolKind);
        DefRangeRegisterSymbol.Range.OffsetStart = Range.startOffset;
        DefRangeRegisterSymbol.Range.Range =
            Range.endOffset - Range.startOffset;
        DefRangeRegisterSymbol.Range.ISectStart = 0;
        DefRangeRegisterSymbol.Hdr.Register =
            cvRegMapAmd64[Range.loc.vlReg.vlrReg];
        unsigned Length = sizeof(DefRangeRegisterSymbol.Hdr);
        Streamer->EmitBytes(
            StringRef((char *)&DefRangeRegisterSymbol.Hdr, Length));
        EmitVarDefRange(Fn, DefRangeRegisterSymbol.Range);
        break;
      }

      case ICorDebugInfo::VLT_STK: {

        // TODO: support REGNUM_AMBIENT_SP
        if (Range.loc.vlStk.vlsBaseReg >=
            sizeof(cvRegMapAmd64) / sizeof(cvRegMapAmd64[0])) {
          break;
        }

        assert(Range.loc.vlStk.vlsBaseReg <
                   sizeof(cvRegMapAmd64) / sizeof(cvRegMapAmd64[0]) &&
               "Register number should be in the range of [REGNUM_RAX, "
               "REGNUM_R15].");

        SymbolRecordKind SymbolKind = SymbolRecordKind::DefRangeRegisterRelSym;
        unsigned SizeofDefRangeRegisterRelSym =
            sizeof(DefRangeRegisterRelSym::Hdr) +
            sizeof(DefRangeRegisterRelSym::Range);
        EmitSymRecord(SizeofDefRangeRegisterRelSym, SymbolKind);

        DefRangeRegisterRelSym DefRangeRegisterRelSymbol(SymbolKind);
        DefRangeRegisterRelSymbol.Range.OffsetStart = Range.startOffset;
        DefRangeRegisterRelSymbol.Range.Range =
            Range.endOffset - Range.startOffset;
        DefRangeRegisterRelSymbol.Range.ISectStart = 0;
        DefRangeRegisterRelSymbol.Hdr.Register =
            cvRegMapAmd64[Range.loc.vlStk.vlsBaseReg];
        DefRangeRegisterRelSymbol.Hdr.BasePointerOffset =
            Range.loc.vlStk.vlsOffset;

        unsigned Length = sizeof(DefRangeRegisterRelSymbol.Hdr);
        Streamer->EmitBytes(
            StringRef((char *)&DefRangeRegisterRelSymbol.Hdr, Length));
        EmitVarDefRange(Fn, DefRangeRegisterRelSymbol.Range);
        break;
      }

      case ICorDebugInfo::VLT_REG_BYREF:
      case ICorDebugInfo::VLT_STK_BYREF:
      case ICorDebugInfo::VLT_REG_REG:
      case ICorDebugInfo::VLT_REG_STK:
      case ICorDebugInfo::VLT_STK_REG:
      case ICorDebugInfo::VLT_STK2:
      case ICorDebugInfo::VLT_FPSTK:
      case ICorDebugInfo::VLT_FIXED_VA:
        // TODO: for optimized debugging
        break;

      default:
        assert(false && "Unknown varloc type!");
        break;
      }
    }
  }
}

void ObjectWriter::EmitCVDebugFunctionInfo(const char *FunctionName,
                                           int FunctionSize) {
  assert(ObjFileInfo->getObjectFileType() == ObjFileInfo->IsCOFF);

  // Mark the end of function.
  MCSymbol *FnEnd = OutContext->createTempSymbol();
  Streamer->EmitLabel(FnEnd);

  MCSection *Section = ObjFileInfo->getCOFFDebugSymbolsSection();
  Streamer->SwitchSection(Section);
  // Emit debug section magic before the first entry.
  if (FuncId == 1) {
    Streamer->EmitIntValue(COFF::DEBUG_SECTION_MAGIC, 4);
  }
  MCSymbol *Fn = OutContext->getOrCreateSymbol(Twine(FunctionName));

  // Emit a symbol subsection, required by VS2012+ to find function boundaries.
  MCSymbol *SymbolsBegin = OutContext->createTempSymbol(),
           *SymbolsEnd = OutContext->createTempSymbol();
  Streamer->EmitIntValue(unsigned(DebugSubsectionKind::Symbols), 4);
  EmitLabelDiff(SymbolsBegin, SymbolsEnd);
  Streamer->EmitLabel(SymbolsBegin);
  {
    ProcSym ProcSymbol(SymbolRecordKind::GlobalProcIdSym);
    ProcSymbol.CodeSize = FunctionSize;
    ProcSymbol.DbgEnd = FunctionSize;

    unsigned FunctionNameLength = strlen(FunctionName) + 1;
    unsigned HeaderSize =
        sizeof(ProcSymbol.Parent) + sizeof(ProcSymbol.End) +
        sizeof(ProcSymbol.Next) + sizeof(ProcSymbol.CodeSize) +
        sizeof(ProcSymbol.DbgStart) + sizeof(ProcSymbol.DbgEnd) +
        sizeof(ProcSymbol.FunctionType);
    unsigned SymbolSize = HeaderSize + 4 + 2 + 1 + FunctionNameLength;
    EmitSymRecord(SymbolSize, SymbolRecordKind::GlobalProcIdSym);

    Streamer->EmitBytes(StringRef((char *)&ProcSymbol.Parent, HeaderSize));
    // Emit relocation
    Streamer->EmitCOFFSecRel32(Fn, 0);
    Streamer->EmitCOFFSectionIndex(Fn);

    // Emit flags
    Streamer->EmitIntValue(0, 1);

    // Emit the function display name as a null-terminated string.

    Streamer->EmitBytes(StringRef(FunctionName, FunctionNameLength));

    // Emit local var info
    int NumVarInfos = DebugVarInfos.size();
    if (NumVarInfos > 0) {
      EmitCVDebugVarInfo(Fn, &DebugVarInfos[0], NumVarInfos);
      DebugVarInfos.clear();
    }

    // We're done with this function.
    EmitSymRecord(0, SymbolRecordKind::ProcEnd);
  }

  Streamer->EmitLabel(SymbolsEnd);

  // Every subsection must be aligned to a 4-byte boundary.
  Streamer->EmitValueToAlignment(4);

  // We have an assembler directive that takes care of the whole line table.
  // We also increase function id for the next function.
  Streamer->EmitCVLinetableDirective(FuncId++, Fn, FnEnd);
}

void ObjectWriter::EmitDwarfFunctionInfo(const char *FunctionName,
                                         int FunctionSize,
                                         unsigned MethodTypeIndex) {
  if (FuncId == 1) {
    DwarfGenerator->EmitCompileUnit();
  }

  DwarfGenerator->EmitSubprogramInfo(FunctionName, FunctionSize,
      MethodTypeIndex, DebugVarInfos, DebugEHClauseInfos);

  DebugVarInfos.clear();
  DebugEHClauseInfos.clear();

  FuncId++;
}

void ObjectWriter::EmitDebugFileInfo(int FileId, const char *FileName) {
  assert(FileId > 0 && "FileId should be greater than 0.");
  if (ObjFileInfo->getObjectFileType() == ObjFileInfo->IsCOFF) {
    Streamer->EmitCVFileDirective(FileId, FileName);
  } else {
    Streamer->EmitDwarfFileDirective(FileId, "", FileName);
  }
}

void ObjectWriter::EmitDebugFunctionInfo(const char *FunctionName,
                                         int FunctionSize,
                                         unsigned MethodTypeIndex) {
  if (ObjFileInfo->getObjectFileType() == ObjFileInfo->IsCOFF) {
    Streamer->EmitCVFuncIdDirective(FuncId);
    EmitCVDebugFunctionInfo(FunctionName, FunctionSize);
  } else {
    if (ObjFileInfo->getObjectFileType() == ObjFileInfo->IsELF) {
      MCSymbol *Sym = OutContext->getOrCreateSymbol(Twine(FunctionName));
      Streamer->EmitSymbolAttribute(Sym, MCSA_ELF_TypeFunction);
      Streamer->emitELFSize(Sym,
                            MCConstantExpr::create(FunctionSize, *OutContext));
      EmitDwarfFunctionInfo(FunctionName, FunctionSize, MethodTypeIndex);
    }
    // TODO: Should test it for Macho.
  }
}

void ObjectWriter::EmitDebugVar(char *Name, int TypeIndex, bool IsParm,
                                int RangeCount,
                                const ICorDebugInfo::NativeVarInfo *Ranges) {
  assert(RangeCount != 0);
  DebugVarInfo NewVar(Name, TypeIndex, IsParm);

  for (int I = 0; I < RangeCount; I++) {
    assert(Ranges[0].varNumber == Ranges[I].varNumber);
    NewVar.Ranges.push_back(Ranges[I]);
  }

  DebugVarInfos.push_back(NewVar);
}

void ObjectWriter::EmitDebugEHClause(unsigned TryOffset, unsigned TryLength,
                                unsigned HandlerOffset, unsigned HandlerLength) {
  if (ObjFileInfo->getObjectFileType() == ObjFileInfo->IsELF) {
    DebugEHClauseInfos.emplace_back(TryOffset, TryLength, HandlerOffset, HandlerLength);
  }
}

void ObjectWriter::EmitDebugLoc(int NativeOffset, int FileId, int LineNumber,
                                int ColNumber) {
  assert(FileId > 0 && "FileId should be greater than 0.");
  if (ObjFileInfo->getObjectFileType() == ObjFileInfo->IsCOFF) {
    Streamer->EmitCVFuncIdDirective(FuncId);
    Streamer->EmitCVLocDirective(FuncId, FileId, LineNumber, ColNumber, false,
                                 true, "", SMLoc());
  } else {
    Streamer->EmitDwarfLocDirective(FileId, LineNumber, ColNumber, 1, 0, 0, "");
  }
}

void ObjectWriter::EmitCVUserDefinedTypesSymbols() {
  const auto &UDTs = TypeBuilder->GetUDTs();
  if (UDTs.empty()) {
    return;
  }
  MCSection *Section = ObjFileInfo->getCOFFDebugSymbolsSection();
  Streamer->SwitchSection(Section);

  MCSymbol *SymbolsBegin = OutContext->createTempSymbol(),
           *SymbolsEnd = OutContext->createTempSymbol();
  Streamer->EmitIntValue(unsigned(DebugSubsectionKind::Symbols), 4);
  EmitLabelDiff(SymbolsBegin, SymbolsEnd);
  Streamer->EmitLabel(SymbolsBegin);

  for (const std::pair<std::string, uint32_t> &UDT : UDTs) {
    unsigned NameLength = UDT.first.length() + 1;
    unsigned RecordLength = 2 + 4 + NameLength;
    Streamer->EmitIntValue(RecordLength, 2);
    Streamer->EmitIntValue(unsigned(SymbolKind::S_UDT), 2);
    Streamer->EmitIntValue(UDT.second, 4);
    Streamer->EmitBytes(StringRef(UDT.first.c_str(), NameLength));
  }
  Streamer->EmitLabel(SymbolsEnd);
  Streamer->EmitValueToAlignment(4);
}

void ObjectWriter::EmitDebugModuleInfo() {
  if (ObjFileInfo->getObjectFileType() == ObjFileInfo->IsCOFF) {
    TypeBuilder->EmitTypeInformation(ObjFileInfo->getCOFFDebugTypesSection());
    EmitCVUserDefinedTypesSymbols();
  }

  // Ensure ending all sections.
  for (auto Section : Sections) {
    Streamer->endSection(Section);
  }

  if (ObjFileInfo->getObjectFileType() == ObjFileInfo->IsCOFF) {
    MCSection *Section = ObjFileInfo->getCOFFDebugSymbolsSection();
    Streamer->SwitchSection(Section);
    Streamer->EmitCVFileChecksumsDirective();
    Streamer->EmitCVStringTableDirective();
  } else if (ObjFileInfo->getObjectFileType() == ObjFileInfo->IsELF) {
    DwarfGenerator->EmitAbbrev();
    DwarfGenerator->EmitAranges();
    DwarfGenerator->Finish();
  } else {
    OutContext->setGenDwarfForAssembly(true);
  }
}

unsigned
ObjectWriter::GetEnumTypeIndex(const EnumTypeDescriptor &TypeDescriptor,
                               const EnumRecordTypeDescriptor *TypeRecords) {
  return TypeBuilder->GetEnumTypeIndex(TypeDescriptor, TypeRecords);
}

unsigned
ObjectWriter::GetClassTypeIndex(const ClassTypeDescriptor &ClassDescriptor) {
  unsigned res = TypeBuilder->GetClassTypeIndex(ClassDescriptor);
  return res;
}

unsigned ObjectWriter::GetCompleteClassTypeIndex(
    const ClassTypeDescriptor &ClassDescriptor,
    const ClassFieldsTypeDescriptior &ClassFieldsDescriptor,
    const DataFieldDescriptor *FieldsDescriptors,
    const StaticDataFieldDescriptor *StaticsDescriptors) {
  unsigned res = TypeBuilder->GetCompleteClassTypeIndex(ClassDescriptor,
      ClassFieldsDescriptor, FieldsDescriptors, StaticsDescriptors);
  return res;
}

unsigned
ObjectWriter::GetArrayTypeIndex(const ClassTypeDescriptor &ClassDescriptor,
                                const ArrayTypeDescriptor &ArrayDescriptor) {
  return TypeBuilder->GetArrayTypeIndex(ClassDescriptor, ArrayDescriptor);
}

unsigned 
ObjectWriter::GetPointerTypeIndex(const PointerTypeDescriptor& PointerDescriptor) {
    return TypeBuilder->GetPointerTypeIndex(PointerDescriptor);
}

unsigned 
ObjectWriter::GetMemberFunctionTypeIndex(const MemberFunctionTypeDescriptor& MemberDescriptor,
                                         uint32_t const *const ArgumentTypes) {
    return TypeBuilder->GetMemberFunctionTypeIndex(MemberDescriptor, ArgumentTypes);
}

unsigned 
ObjectWriter::GetMemberFunctionId(const MemberFunctionIdTypeDescriptor& MemberIdDescriptor) {
    return TypeBuilder->GetMemberFunctionId(MemberIdDescriptor);
}

unsigned
ObjectWriter::GetPrimitiveTypeIndex(int Type) {
  return TypeBuilder->GetPrimitiveTypeIndex(static_cast<PrimitiveTypeFlags>(Type));
}

void
ObjectWriter::EmitARMFnStart() {
  MCTargetStreamer &TS = *(Streamer->getTargetStreamer());
  ARMTargetStreamer &ATS = static_cast<ARMTargetStreamer &>(TS);

  ATS.emitFnStart();
}

void ObjectWriter::EmitARMFnEnd() {

  if (!CFIsPerOffset.empty())
  {
    EmitARMExIdxPerOffset();
  }

  MCTargetStreamer &TS = *(Streamer->getTargetStreamer());
  ARMTargetStreamer &ATS = static_cast<ARMTargetStreamer &>(TS);

  ATS.emitFnEnd();
}

void ObjectWriter::EmitARMExIdxLsda(const char *LsdaBlobSymbolName)
{
  MCTargetStreamer &TS = *(Streamer->getTargetStreamer());
  ARMTargetStreamer &ATS = static_cast<ARMTargetStreamer &>(TS);

  MCSymbol *T = OutContext->getOrCreateSymbol(LsdaBlobSymbolName);
  Assembler->registerSymbol(*T);

  ATS.emitLsda(T);
}

void ObjectWriter::EmitARMExIdxPerOffset()
{
  MCTargetStreamer &TS = *(Streamer->getTargetStreamer());
  ARMTargetStreamer &ATS = static_cast<ARMTargetStreamer &>(TS);
  const MCRegisterInfo *MRI = OutContext->getRegisterInfo();

  SmallVector<unsigned, 32> RegSet;
  bool IsVector = false;

  // LLVM reverses opcodes that are fed to ARMTargetStreamer, so we do the same,
  // but per code offset. Opcodes with different code offsets are already given in
  // the correct order.
  for (int i = CFIsPerOffset.size() - 1; i >= 0; --i)
  {
    unsigned char opCode = CFIsPerOffset[i].CfiOpCode;
    short Reg = CFIsPerOffset[i].DwarfReg;

    if (RegSet.empty() && opCode == CFI_REL_OFFSET)
    {
      IsVector = Reg >= 16;
    }
    else if (!RegSet.empty() && opCode != CFI_REL_OFFSET)
    {
      ATS.emitRegSave(RegSet, IsVector);
      RegSet.clear();
    }

    switch (opCode)
    {
    case CFI_REL_OFFSET:
      assert(IsVector == (Reg >= 16) && "Unexpected Register Type");
      RegSet.push_back(MRI->getLLVMRegNum(Reg, true));
      break;
    case CFI_ADJUST_CFA_OFFSET:
      assert(Reg == DWARF_REG_ILLEGAL &&
           "Unexpected Register Value for OpAdjustCfaOffset");
      ATS.emitPad(CFIsPerOffset[i].Offset);
      break;
    case CFI_DEF_CFA_REGISTER:
      ATS.emitMovSP(MRI->getLLVMRegNum(Reg, true));
      break;
    default:
      assert(false && "Unrecognized CFI");
      break;
    }
  }

  // if we have some registers left over, emit them
  if (!RegSet.empty())
  {
      ATS.emitRegSave(RegSet, IsVector);
  }

  CFIsPerOffset.clear();
}

void ObjectWriter::EmitARMExIdxCode(int Offset, const char *Blob)
{
  const CFI_CODE *CfiCode = (const CFI_CODE *)Blob;

  if (!CFIsPerOffset.empty() && CFIsPerOffset[0].CodeOffset != CfiCode->CodeOffset)
  {
    EmitARMExIdxPerOffset();
  }
  
  CFIsPerOffset.push_back(*CfiCode);
}
