//===---- objwriter.h ------------------------------------------*- C++ -*-===//
//
// object writer
//
// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.
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
#include "debugInfo/typeBuilder.h"
#include "debugInfo/dwarf/dwarfGen.h"

using namespace llvm;
using namespace llvm::codeview;

#ifdef _WIN32
#define DLL_EXPORT extern "C" __declspec(dllexport)
#else
#define DLL_EXPORT extern "C" __attribute((visibility("default")))
#endif

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
  IMAGE_REL_BASED_RELPTR32 = 0x7C,
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
  void EmitDebugFunctionInfo(const char *FunctionName, int FunctionSize, unsigned MethodTypeIndex);
  void EmitDebugVar(char *Name, int TypeIndex, bool IsParm, int RangeCount,
                    const ICorDebugInfo::NativeVarInfo *Ranges);
  void EmitDebugLoc(int NativeOffset, int FileId, int LineNumber,
                    int ColNumber);
  void EmitDebugEHClause(unsigned TryOffset, unsigned TryLength,
                         unsigned HandlerOffset, unsigned HandlerLength);
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

  unsigned GetPrimitiveTypeIndex(int Type);

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

  void EmitDwarfFunctionInfo(const char *FunctionName, int FunctionSize, unsigned MethodTypeIndex);

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
  std::unique_ptr<DwarfGen> DwarfGenerator;

  std::unique_ptr<raw_fd_ostream> OS;
  MCTargetOptions TargetMOptions;
  bool FrameOpened;
  std::vector<DebugVarInfo> DebugVarInfos;
  std::vector<DebugEHClauseInfo> DebugEHClauseInfos;

  std::set<MCSection *> Sections;
  int FuncId;

  std::unique_ptr<UserDefinedTypesBuilder> TypeBuilder;

  std::string TripleName;

  MCObjectStreamer *Streamer; // Owned by AsmPrinter
};

// When object writer is created/initialized successfully, it is returned.
// Or null object is returned. Client should check this.
DLL_EXPORT ObjectWriter *InitObjWriter(const char *ObjectFilePath) {
  ObjectWriter *OW = new ObjectWriter();
  if (OW->Init(ObjectFilePath)) {
    return OW;
  }
  delete OW;
  return nullptr;
}

DLL_EXPORT void FinishObjWriter(ObjectWriter *OW) {
  assert(OW && "ObjWriter is null");
  OW->Finish();
  delete OW;
}

DLL_EXPORT void SwitchSection(ObjectWriter *OW, const char *SectionName,
                              CustomSectionAttributes attributes,
                              const char *ComdatName) {
  assert(OW && "ObjWriter is null");
  OW->SwitchSection(SectionName, attributes, ComdatName);
}

DLL_EXPORT void SetCodeSectionAttribute(ObjectWriter *OW,
                                        const char *SectionName,
                                        CustomSectionAttributes attributes,
                                        const char *ComdatName) {
  assert(OW && "ObjWriter is null");
  OW->SetCodeSectionAttribute(SectionName, attributes, ComdatName);
}

DLL_EXPORT void EmitAlignment(ObjectWriter *OW, int ByteAlignment) {
  assert(OW && "ObjWriter is null");
  OW->EmitAlignment(ByteAlignment);
}

DLL_EXPORT void EmitBlob(ObjectWriter *OW, int BlobSize, const char *Blob) {
  assert(OW && "ObjWriter null");
  OW->EmitBlob(BlobSize, Blob);
}

DLL_EXPORT void EmitIntValue(ObjectWriter *OW, uint64_t Value, unsigned Size) {
  assert(OW && "ObjWriter is null");
  OW->EmitIntValue(Value, Size);
}

DLL_EXPORT void EmitSymbolDef(ObjectWriter *OW, const char *SymbolName) {
  assert(OW && "ObjWriter is null");
  OW->EmitSymbolDef(SymbolName);
}

DLL_EXPORT int EmitSymbolRef(ObjectWriter *OW, const char *SymbolName,
                             RelocType RelocType, int Delta) {
  assert(OW && "ObjWriter is null");
  return OW->EmitSymbolRef(SymbolName, RelocType, Delta);
}

DLL_EXPORT void EmitWinFrameInfo(ObjectWriter *OW, const char *FunctionName,
                                 int StartOffset, int EndOffset,
                                 const char *BlobSymbolName) {
  assert(OW && "ObjWriter is null");
  OW->EmitWinFrameInfo(FunctionName, StartOffset, EndOffset, BlobSymbolName);
}

DLL_EXPORT void EmitCFIStart(ObjectWriter *OW, int Offset) {
  assert(OW && "ObjWriter is null");
  OW->EmitCFIStart(Offset);
}

DLL_EXPORT void EmitCFIEnd(ObjectWriter *OW, int Offset) {
  assert(OW && "ObjWriter is null");
  OW->EmitCFIEnd(Offset);
}

DLL_EXPORT void EmitCFILsda(ObjectWriter *OW, const char *LsdaBlobSymbolName) {
  assert(OW && "ObjWriter is null");
  OW->EmitCFILsda(LsdaBlobSymbolName);
}

DLL_EXPORT void EmitCFICode(ObjectWriter *OW, int Offset, const char *Blob) {
  assert(OW && "ObjWriter is null");
  OW->EmitCFICode(Offset, Blob);
}

DLL_EXPORT void EmitDebugFileInfo(ObjectWriter *OW, int FileId,
                                  const char *FileName) {
  assert(OW && "ObjWriter is null");
  OW->EmitDebugFileInfo(FileId, FileName);
}

DLL_EXPORT void EmitDebugFunctionInfo(ObjectWriter *OW,
                                      const char *FunctionName,
                                      int FunctionSize,
                                      unsigned MethodTypeIndex) {
  assert(OW && "ObjWriter is null");
  OW->EmitDebugFunctionInfo(FunctionName, FunctionSize, MethodTypeIndex);
}

DLL_EXPORT void EmitDebugVar(ObjectWriter *OW, char *Name, int TypeIndex,
                             bool IsParam, int RangeCount,
                             ICorDebugInfo::NativeVarInfo *Ranges) {
  assert(OW && "ObjWriter is null");
  OW->EmitDebugVar(Name, TypeIndex, IsParam, RangeCount, Ranges);
}

DLL_EXPORT void EmitDebugEHClause(ObjectWriter *OW, unsigned TryOffset,
                                  unsigned TryLength, unsigned HandlerOffset,
                                  unsigned HandlerLength) {
  assert(OW && "ObjWriter is null");
  OW->EmitDebugEHClause(TryOffset, TryLength, HandlerOffset, HandlerLength);
}

DLL_EXPORT void EmitDebugLoc(ObjectWriter *OW, int NativeOffset, int FileId,
                             int LineNumber, int ColNumber) {
  assert(OW && "ObjWriter is null");
  OW->EmitDebugLoc(NativeOffset, FileId, LineNumber, ColNumber);
}

// This should be invoked at the end of module emission to finalize
// debug module info.
DLL_EXPORT void EmitDebugModuleInfo(ObjectWriter *OW) {
  assert(OW && "ObjWriter is null");
  OW->EmitDebugModuleInfo();
}

DLL_EXPORT unsigned GetEnumTypeIndex(ObjectWriter *OW,
                                     EnumTypeDescriptor TypeDescriptor,
                                     EnumRecordTypeDescriptor *TypeRecords) {
  assert(OW && "ObjWriter is null");
  return OW->GetEnumTypeIndex(TypeDescriptor, TypeRecords);
}

DLL_EXPORT unsigned GetClassTypeIndex(ObjectWriter *OW,
                                      ClassTypeDescriptor ClassDescriptor) {
  assert(OW && "ObjWriter is null");
  return OW->GetClassTypeIndex(ClassDescriptor);
}

DLL_EXPORT unsigned
GetCompleteClassTypeIndex(ObjectWriter *OW, ClassTypeDescriptor ClassDescriptor,
                          ClassFieldsTypeDescriptior ClassFieldsDescriptor,
                          DataFieldDescriptor *FieldsDescriptors) {
  assert(OW && "ObjWriter is null");
  return OW->GetCompleteClassTypeIndex(ClassDescriptor, ClassFieldsDescriptor,
                                       FieldsDescriptors);
}

DLL_EXPORT unsigned GetArrayTypeIndex(ObjectWriter *OW,
                                      ClassTypeDescriptor ClassDescriptor,
                                      ArrayTypeDescriptor ArrayDescriptor) {
  assert(OW && "ObjWriter is null");
  return OW->GetArrayTypeIndex(ClassDescriptor, ArrayDescriptor);
}

DLL_EXPORT unsigned GetPointerTypeIndex(ObjectWriter *OW,
    PointerTypeDescriptor PointerDescriptor) {
    assert(OW && "ObjWriter is null");
    return OW->GetPointerTypeIndex(PointerDescriptor);
}

DLL_EXPORT unsigned GetMemberFunctionTypeIndex(ObjectWriter *OW,
    MemberFunctionTypeDescriptor MemberDescriptor,
    uint32_t *ArgumentTypes) {
    assert(OW && "ObjWriter is null");
    return OW->GetMemberFunctionTypeIndex(MemberDescriptor, ArgumentTypes);
}

DLL_EXPORT unsigned GetMemberFunctionIdTypeIndex(ObjectWriter *OW,
    MemberFunctionIdTypeDescriptor MemberIdDescriptor) {
    assert(OW && "ObjWriter is null");
    return OW->GetMemberFunctionId(MemberIdDescriptor);
}

DLL_EXPORT unsigned GetPrimitiveTypeIndex(ObjectWriter *OW, int Type) {
    assert(OW && "ObjWriter is null");
    return OW->GetPrimitiveTypeIndex(Type);
}

DLL_EXPORT void EmitARMFnStart(ObjectWriter *OW) {
    assert(OW && "ObjWriter is null");
    return OW->EmitARMFnStart();
}

DLL_EXPORT void EmitARMFnEnd(ObjectWriter *OW) {
  assert(OW && "ObjWriter is null");
  return OW->EmitARMFnEnd();
}

DLL_EXPORT void EmitARMExIdxLsda(ObjectWriter *OW, const char *Blob) {
  assert(OW && "ObjWriter is null");
  return OW->EmitARMExIdxLsda(Blob);
}

DLL_EXPORT void EmitARMExIdxCode(ObjectWriter *OW, int Offset, const char *Blob) {
  assert(OW && "ObjWriter is null");
  return OW->EmitARMExIdxCode(Offset, Blob);
}
