//===---- objwriter.h --------------------------------*- C++ -*-===//
//
// object writer
//
// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license.
// See LICENSE file in the project root for full license information.
//
//===----------------------------------------------------------------------===//

#include "llvm/CodeGen/AsmPrinter.h"
#include "llvm/MC/MCCodeEmitter.h"
#include "llvm/MC/MCInstrInfo.h"
#include "llvm/MC/MCObjectFileInfo.h"
#include "llvm/Target/TargetOptions.h"
#include "llvm/DebugInfo/CodeView/TypeTableBuilder.h"

#include "cfi.h"
#include "jitDebugInfo.h"
#include <string>
#include <set>
#include "typeBuilder.h"

using namespace llvm;
using namespace llvm::codeview;

enum CustomSectionAttributes : int32_t {
  CustomSectionAttributes_ReadOnly = 0x0000,
  CustomSectionAttributes_Writeable = 0x0001,
  CustomSectionAttributes_Executable = 0x0002,
  CustomSectionAttributes_MachO_Init_Func_Pointers = 0x0100,
};

enum class RelocType {
  IMAGE_REL_BASED_ABSOLUTE = 0x00,
  IMAGE_REL_BASED_HIGHLOW = 0x03,
  IMAGE_REL_BASED_THUMB_MOV32 = 0x07,
  IMAGE_REL_BASED_DIR64 = 0x0A,
  IMAGE_REL_BASED_REL32 = 0x10,
  IMAGE_REL_BASED_THUMB_BRANCH24 = 0x13,
};

class ObjectWriter {
public:
  bool Init(StringRef FunctionName);
  void Finish();

  void SwitchSection(const char *SectionName,
                     CustomSectionAttributes attributes,
                     const char *ComdatName);
  void SetCodeSectionAttribute(const char *SectionName,
                               CustomSectionAttributes attributes,
                               const char *ComdatName);

  void EmitAlignment(int ByteAlignment);
  void EmitBlob(int BlobSize, const char *Blob);
  void EmitIntValue(uint64_t Value, unsigned Size);
  void EmitSymbolDef(const char *SymbolName);
  void EmitWinFrameInfo(const char *FunctionName, int StartOffset,
                        int EndOffset, const char *BlobSymbolName);
  int EmitSymbolRef(const char *SymbolName, RelocType RelocType, int Delta);

  void EmitDebugFileInfo(int FileId, const char *FileName);
  void EmitDebugFunctionInfo(const char *FunctionName, int FunctionSize);
  void EmitDebugVar(char *Name, int TypeIndex, bool IsParm, int RangeCount,
                    const ICorDebugInfo::NativeVarInfo *Ranges);
  void EmitDebugLoc(int NativeOffset, int FileId, int LineNumber,
                    int ColNumber);
  void EmitDebugModuleInfo();

  void EmitCFIStart(int Offset);
  void EmitCFIEnd(int Offset);
  void EmitCFILsda(const char *LsdaBlobSymbolName);
  void EmitCFICode(int Offset, const char *Blob);

  unsigned GetEnumTypeIndex(const EnumTypeDescriptor &TypeDescriptor,
                            const EnumRecordTypeDescriptor *TypeRecords);
  unsigned GetClassTypeIndex(const ClassTypeDescriptor &ClassDescriptor);
  unsigned GetCompleteClassTypeIndex(
      const ClassTypeDescriptor &ClassDescriptor,
      const ClassFieldsTypeDescriptior &ClassFieldsDescriptor,
      const DataFieldDescriptor *FieldsDescriptors);

  unsigned GetArrayTypeIndex(const ClassTypeDescriptor &ClassDescriptor,
                             const ArrayTypeDescriptor &ArrayDescriptor);

  unsigned GetPointerTypeIndex(const PointerTypeDescriptor& PointerDescriptor);

  unsigned GetMemberFunctionTypeIndex(const MemberFunctionTypeDescriptor& MemberDescriptor,
                                      uint32_t const *const ArgumentTypes);

  unsigned GetMemberFunctionId(const MemberFunctionIdTypeDescriptor& MemberIdDescriptor);

  void EmitARMFnStart();
  void EmitARMFnEnd();
  void EmitARMExIdxCode(int Offset, const char *Blob);
  void EmitARMExIdxLsda(const char *Blob);

private:
  void EmitLabelDiff(const MCSymbol *From, const MCSymbol *To,
                     unsigned int Size = 4);
  void EmitSymRecord(int Size, SymbolRecordKind SymbolKind);
  void EmitCOFFSecRel32Value(MCExpr const *Value);

  void EmitVarDefRange(const MCSymbol *Fn, const LocalVariableAddrRange &Range);
  void EmitCVDebugVarInfo(const MCSymbol *Fn, const DebugVarInfo LocInfos[],
                          int NumVarInfos);
  void EmitCVDebugFunctionInfo(const char *FunctionName, int FunctionSize);

  const MCSymbolRefExpr *GetSymbolRefExpr(
      const char *SymbolName,
      MCSymbolRefExpr::VariantKind Kind = MCSymbolRefExpr::VK_None);

  MCSection *GetSection(const char *SectionName,
                        CustomSectionAttributes attributes,
                        const char *ComdatName);

  MCSection *GetSpecificSection(const char *SectionName,
                                CustomSectionAttributes attributes,
                                const char *ComdatName);

  void EmitCVUserDefinedTypesSymbols();

  void InitTripleName();
  Triple GetTriple();
  unsigned GetDFSize();
  bool EmitRelocDirective(const int Offset, StringRef Name, const MCExpr *Expr);
  const MCExpr *GenTargetExpr(const char *SymbolName,
                              MCSymbolRefExpr::VariantKind Kind, int Delta,
                              bool IsPCRel = false, int Size = 0);


private:
  std::unique_ptr<MCRegisterInfo> RegisterInfo;
  std::unique_ptr<MCAsmInfo> AsmInfo;
  std::unique_ptr<MCObjectFileInfo> ObjFileInfo;
  std::unique_ptr<MCContext> OutContext;
  MCAsmBackend *AsmBackend; // Owned by MCStreamer
  std::unique_ptr<MCInstrInfo> InstrInfo;
  std::unique_ptr<MCSubtargetInfo> SubtargetInfo;
  MCCodeEmitter *CodeEmitter; // Owned by MCStreamer
  std::unique_ptr<TargetMachine> TMachine;
  std::unique_ptr<AsmPrinter> AssemblerPrinter;
  MCAssembler *Assembler; // Owned by MCStreamer

  std::unique_ptr<raw_fd_ostream> OS;
  MCTargetOptions TargetMOptions;
  bool FrameOpened;
  std::vector<DebugVarInfo> DebugVarInfos;

  std::set<MCSection *> Sections;
  int FuncId;

  UserDefinedTypesBuilder TypeBuilder;

  std::string TripleName;

  MCObjectStreamer *Streamer; // Owned by AsmPrinter
};

// When object writer is created/initialized successfully, it is returned.
// Or null object is returned. Client should check this.
extern "C" ObjectWriter *InitObjWriter(const char *ObjectFilePath) {
  ObjectWriter *OW = new ObjectWriter();
  if (OW->Init(ObjectFilePath)) {
    return OW;
  }
  delete OW;
  return nullptr;
}

extern "C" void FinishObjWriter(ObjectWriter *OW) {
  assert(OW && "ObjWriter is null");
  OW->Finish();
  delete OW;
}

extern "C" void SwitchSection(ObjectWriter *OW, const char *SectionName,
                              CustomSectionAttributes attributes,
                              const char *ComdatName) {
  assert(OW && "ObjWriter is null");
  OW->SwitchSection(SectionName, attributes, ComdatName);
}

extern "C" void SetCodeSectionAttribute(ObjectWriter *OW,
                                        const char *SectionName,
                                        CustomSectionAttributes attributes,
                                        const char *ComdatName) {
  assert(OW && "ObjWriter is null");
  OW->SetCodeSectionAttribute(SectionName, attributes, ComdatName);
}

extern "C" void EmitAlignment(ObjectWriter *OW, int ByteAlignment) {
  assert(OW && "ObjWriter is null");
  OW->EmitAlignment(ByteAlignment);
}

extern "C" void EmitBlob(ObjectWriter *OW, int BlobSize, const char *Blob) {
  assert(OW && "ObjWriter null");
  OW->EmitBlob(BlobSize, Blob);
}

extern "C" void EmitIntValue(ObjectWriter *OW, uint64_t Value, unsigned Size) {
  assert(OW && "ObjWriter is null");
  OW->EmitIntValue(Value, Size);
}

extern "C" void EmitSymbolDef(ObjectWriter *OW, const char *SymbolName) {
  assert(OW && "ObjWriter is null");
  OW->EmitSymbolDef(SymbolName);
}

extern "C" int EmitSymbolRef(ObjectWriter *OW, const char *SymbolName,
                             RelocType RelocType, int Delta) {
  assert(OW && "ObjWriter is null");
  return OW->EmitSymbolRef(SymbolName, RelocType, Delta);
}

extern "C" void EmitWinFrameInfo(ObjectWriter *OW, const char *FunctionName,
                                 int StartOffset, int EndOffset,
                                 const char *BlobSymbolName) {
  assert(OW && "ObjWriter is null");
  OW->EmitWinFrameInfo(FunctionName, StartOffset, EndOffset, BlobSymbolName);
}

extern "C" void EmitCFIStart(ObjectWriter *OW, int Offset) {
  assert(OW && "ObjWriter is null");
  OW->EmitCFIStart(Offset);
}

extern "C" void EmitCFIEnd(ObjectWriter *OW, int Offset) {
  assert(OW && "ObjWriter is null");
  OW->EmitCFIEnd(Offset);
}

extern "C" void EmitCFILsda(ObjectWriter *OW, const char *LsdaBlobSymbolName) {
  assert(OW && "ObjWriter is null");
  OW->EmitCFILsda(LsdaBlobSymbolName);
}

extern "C" void EmitCFICode(ObjectWriter *OW, int Offset, const char *Blob) {
  assert(OW && "ObjWriter is null");
  OW->EmitCFICode(Offset, Blob);
}

extern "C" void EmitDebugFileInfo(ObjectWriter *OW, int FileId,
                                  const char *FileName) {
  assert(OW && "ObjWriter is null");
  OW->EmitDebugFileInfo(FileId, FileName);
}

extern "C" void EmitDebugFunctionInfo(ObjectWriter *OW,
                                      const char *FunctionName,
                                      int FunctionSize) {
  assert(OW && "ObjWriter is null");
  OW->EmitDebugFunctionInfo(FunctionName, FunctionSize);
}

extern "C" void EmitDebugVar(ObjectWriter *OW, char *Name, int TypeIndex,
                             bool IsParam, int RangeCount,
                             ICorDebugInfo::NativeVarInfo *Ranges) {
  assert(OW && "ObjWriter is null");
  OW->EmitDebugVar(Name, TypeIndex, IsParam, RangeCount, Ranges);
}

extern "C" void EmitDebugLoc(ObjectWriter *OW, int NativeOffset, int FileId,
                             int LineNumber, int ColNumber) {
  assert(OW && "ObjWriter is null");
  OW->EmitDebugLoc(NativeOffset, FileId, LineNumber, ColNumber);
}

// This should be invoked at the end of module emission to finalize
// debug module info.
extern "C" void EmitDebugModuleInfo(ObjectWriter *OW) {
  assert(OW && "ObjWriter is null");
  OW->EmitDebugModuleInfo();
}

extern "C" unsigned GetEnumTypeIndex(ObjectWriter *OW,
                                     EnumTypeDescriptor TypeDescriptor,
                                     EnumRecordTypeDescriptor *TypeRecords) {
  assert(OW && "ObjWriter is null");
  return OW->GetEnumTypeIndex(TypeDescriptor, TypeRecords);
}

extern "C" unsigned GetClassTypeIndex(ObjectWriter *OW,
                                      ClassTypeDescriptor ClassDescriptor) {
  assert(OW && "ObjWriter is null");
  return OW->GetClassTypeIndex(ClassDescriptor);
}

extern "C" unsigned
GetCompleteClassTypeIndex(ObjectWriter *OW, ClassTypeDescriptor ClassDescriptor,
                          ClassFieldsTypeDescriptior ClassFieldsDescriptor,
                          DataFieldDescriptor *FieldsDescriptors) {
  assert(OW && "ObjWriter is null");
  return OW->GetCompleteClassTypeIndex(ClassDescriptor, ClassFieldsDescriptor,
                                       FieldsDescriptors);
}

extern "C" unsigned GetArrayTypeIndex(ObjectWriter *OW,
                                      ClassTypeDescriptor ClassDescriptor,
                                      ArrayTypeDescriptor ArrayDescriptor) {
  assert(OW && "ObjWriter is null");
  return OW->GetArrayTypeIndex(ClassDescriptor, ArrayDescriptor);
}

extern "C" unsigned GetPointerTypeIndex(ObjectWriter *OW,
    PointerTypeDescriptor PointerDescriptor) {
    assert(OW && "ObjWriter is null");
    return OW->GetPointerTypeIndex(PointerDescriptor);
}

extern "C" unsigned GetMemberFunctionTypeIndex(ObjectWriter *OW,
    MemberFunctionTypeDescriptor MemberDescriptor,
    uint32_t *ArgumentTypes) {
    assert(OW && "ObjWriter is null");
    return OW->GetMemberFunctionTypeIndex(MemberDescriptor, ArgumentTypes);
}

extern "C" unsigned GetMemberFunctionIdTypeIndex(ObjectWriter *OW,
    MemberFunctionIdTypeDescriptor MemberIdDescriptor) {
    assert(OW && "ObjWriter is null");
    return OW->GetMemberFunctionId(MemberIdDescriptor);
}

extern "C" void EmitARMFnStart(ObjectWriter *OW) {
    assert(OW && "ObjWriter is null");
    return OW->EmitARMFnStart();
}

extern "C" void EmitARMFnEnd(ObjectWriter *OW) {
  assert(OW && "ObjWriter is null");
  return OW->EmitARMFnEnd();
}

extern "C" void EmitARMExIdxLsda(ObjectWriter *OW, const char *Blob) {
  assert(OW && "ObjWriter is null");
  return OW->EmitARMExIdxLsda(Blob);
}

extern "C" void EmitARMExIdxCode(ObjectWriter *OW, int Offset, const char *Blob) {
  assert(OW && "ObjWriter is null");
  return OW->EmitARMExIdxCode(Offset, Blob);
}
