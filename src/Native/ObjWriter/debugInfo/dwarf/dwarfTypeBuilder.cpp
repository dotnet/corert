//===---- dwarfTypeBuilder.cpp ----------------------------------*- C++ -*-===//
//
// dwarf type builder implementation
//
// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.
//
//===----------------------------------------------------------------------===//

#include "dwarfTypeBuilder.h"
#include "dwarfAbbrev.h"
#include "llvm/MC/MCContext.h"
#include "llvm/MC/MCAsmInfo.h"
#include "llvm/MC/MCObjectFileInfo.h"

#include <sstream>
#include <vector>

// DwarfInfo

void DwarfInfo::Dump(UserDefinedDwarfTypesBuilder *TypeBuilder, MCObjectStreamer *Streamer,
    MCSection *TypeSection, MCSection *StrSection) {
  if (IsDumped)
    return;

  IsDumped = true;

  MCContext &context = Streamer->getContext();

  InfoSymbol = context.createTempSymbol();
  InfoExpr = CreateOffsetExpr(context, TypeSection->getBeginSymbol(), InfoSymbol);

  DumpTypes(TypeBuilder, Streamer, TypeSection, StrSection);

  Streamer->SwitchSection(StrSection);
  StrSymbol = context.createTempSymbol();
  Streamer->EmitLabel(StrSymbol);
  DumpStrings(Streamer);

  Streamer->SwitchSection(TypeSection);
  Streamer->EmitLabel(InfoSymbol);
  DumpTypeInfo(Streamer, TypeBuilder);
}

void DwarfInfo::DumpTypes(UserDefinedDwarfTypesBuilder *TypeBuilder, MCObjectStreamer *Streamer,
      MCSection *TypeSection, MCSection *StrSection) {
  if (IsDumpedTypes)
    return;

  IsDumpedTypes = true;
}

void DwarfInfo::EmitSectionOffset(MCObjectStreamer *Streamer,
                                  MCSymbol *Symbol,
                                  unsigned Size,
                                  uint32_t Offset) {
  MCContext &context = Streamer->getContext();

  if (context.getAsmInfo()->doesDwarfUseRelocationsAcrossSections()) {
    if (Offset == 0) {
      Streamer->EmitSymbolValue(Symbol, Size);
    } else {
      const MCSymbolRefExpr *SymbolExpr = MCSymbolRefExpr::create(Symbol,
          MCSymbolRefExpr::VK_None, context);
      const MCExpr *OffsetExpr = MCConstantExpr::create(Offset, context);
      const MCExpr *Expr = MCBinaryExpr::createAdd(SymbolExpr, OffsetExpr, context);
      Streamer->EmitValue(Expr, Size);
    }
  } else {
    Streamer->EmitIntValue(Symbol->getOffset() + Offset, Size);
  }
}

const MCExpr *DwarfInfo::CreateOffsetExpr(MCContext &Context,
                                          MCSymbol *BeginSymbol,
                                          MCSymbol *Symbol) {
  MCSymbolRefExpr::VariantKind Variant = MCSymbolRefExpr::VK_None;
  const MCExpr *StartExpr =
    MCSymbolRefExpr::create(BeginSymbol, Variant, Context);
  const MCExpr *EndExpr =
    MCSymbolRefExpr::create(Symbol, Variant, Context);
  return MCBinaryExpr::createSub(EndExpr, StartExpr, Context);
}

void DwarfInfo::EmitOffset(MCObjectStreamer *Streamer,
                           const MCExpr *OffsetExpr,
                           unsigned Size) {
  MCContext &context = Streamer->getContext();

  if (!context.getAsmInfo()->hasAggressiveSymbolFolding()) {
    MCSymbol *Temp = context.createTempSymbol();
    Streamer->EmitAssignment(Temp, OffsetExpr);
    OffsetExpr = MCSymbolRefExpr::create(Temp, context);
  }

  Streamer->EmitValue(OffsetExpr, Size);
}

void DwarfInfo::EmitInfoOffset(MCObjectStreamer *Streamer, DwarfInfo *Info, unsigned Size) {
  uint64_t Offset = Info->InfoSymbol->getOffset();
  if (Offset != 0) {
    Streamer->EmitIntValue(Offset, Size);
  } else {
    EmitOffset(Streamer, Info->InfoExpr, Size);
  }
}

// DwarfPrimitiveTypeInfo

struct PrimitiveTypeDesc {
  const char *Name;
  int Encoding;
  unsigned ByteSize;
};

static PrimitiveTypeDesc GetPrimitiveTypeDesc(PrimitiveTypeFlags Type, unsigned TargetPointerSize) {
  switch (Type) {
    case PrimitiveTypeFlags::Void:    return {"void",           dwarf::DW_ATE_address,  0};
    case PrimitiveTypeFlags::Boolean: return {"bool",           dwarf::DW_ATE_boolean,  1};
    case PrimitiveTypeFlags::Char:    return {"char16_t",       dwarf::DW_ATE_UTF,      2};
    case PrimitiveTypeFlags::SByte:   return {"sbyte",          dwarf::DW_ATE_signed,   1};
    case PrimitiveTypeFlags::Byte:    return {"byte",           dwarf::DW_ATE_unsigned, 1};
    case PrimitiveTypeFlags::Int16:   return {"short",          dwarf::DW_ATE_signed,   2};
    case PrimitiveTypeFlags::UInt16:  return {"ushort",         dwarf::DW_ATE_unsigned, 2};
    case PrimitiveTypeFlags::Int32:   return {"int",            dwarf::DW_ATE_signed,   4};
    case PrimitiveTypeFlags::UInt32:  return {"uint",           dwarf::DW_ATE_unsigned, 4};
    case PrimitiveTypeFlags::Int64:   return {"long",           dwarf::DW_ATE_signed,   8};
    case PrimitiveTypeFlags::UInt64:  return {"ulong",          dwarf::DW_ATE_unsigned, 8};
    case PrimitiveTypeFlags::IntPtr:  return {"System.IntPtr",  dwarf::DW_ATE_signed,   TargetPointerSize};
    case PrimitiveTypeFlags::UIntPtr: return {"System.UIntPtr", dwarf::DW_ATE_unsigned, TargetPointerSize};
    case PrimitiveTypeFlags::Single:  return {"float",          dwarf::DW_ATE_float,    4};
    case PrimitiveTypeFlags::Double:  return {"double",         dwarf::DW_ATE_float,    8};
    default:
      assert(false && "Unexpected type");
      return {nullptr, 0, 0};
  }
}

void DwarfPrimitiveTypeInfo::DumpStrings(MCObjectStreamer *Streamer) {
  MCContext &context = Streamer->getContext();
  unsigned TargetPointerSize = context.getAsmInfo()->getCodePointerSize();

  PrimitiveTypeDesc TD = GetPrimitiveTypeDesc(Type, TargetPointerSize);
  if (TD.Name == nullptr)
    return;

  Streamer->EmitBytes(StringRef(TD.Name));
  Streamer->EmitIntValue(0, 1);
}

void DwarfPrimitiveTypeInfo::DumpTypeInfo(MCObjectStreamer *Streamer, UserDefinedDwarfTypesBuilder *TypeBuilder) {
  MCContext &context = Streamer->getContext();
  unsigned TargetPointerSize = context.getAsmInfo()->getCodePointerSize();

  PrimitiveTypeDesc TD = GetPrimitiveTypeDesc(Type, TargetPointerSize);
  if (TD.Name == nullptr)
    return;

  // Abbrev Number
  Streamer->EmitULEB128IntValue(DwarfAbbrev::BaseType);

  // DW_AT_name
  EmitSectionOffset(Streamer, StrSymbol, 4);

  // DW_AT_encoding
  Streamer->EmitIntValue(TD.Encoding, 1);

  // DW_AT_byte_size
  Streamer->EmitIntValue(TD.ByteSize, 1);
}

// DwarfEnumerator

void DwarfEnumerator::DumpStrings(MCObjectStreamer *Streamer) {
  Streamer->EmitBytes(Name);
  Streamer->EmitIntValue(0, 1);
}

void DwarfEnumerator::DumpTypeInfo(MCObjectStreamer *Streamer, UserDefinedDwarfTypesBuilder *TypeBuilder) {
  uint8_t Size = EnumTypeInfo->GetByteSize();

  // Abbrev Number
  switch (Size) {
    case 1:
      Streamer->EmitULEB128IntValue(DwarfAbbrev::Enumerator1);
      break;
    case 2:
      Streamer->EmitULEB128IntValue(DwarfAbbrev::Enumerator2);
      break;
    case 4:
      Streamer->EmitULEB128IntValue(DwarfAbbrev::Enumerator4);
      break;
    case 8:
      Streamer->EmitULEB128IntValue(DwarfAbbrev::Enumerator8);
      break;
    default:
      assert(false && "Unexpected byte size value");
  }

  // DW_AT_name
  EmitSectionOffset(Streamer, StrSymbol, 4);

  // DW_AT_const_value
  Streamer->EmitIntValue(Value, Size);
}

// DwarfEnumTypeInfo

DwarfEnumTypeInfo::DwarfEnumTypeInfo(const EnumTypeDescriptor &TypeDescriptor,
                                     const EnumRecordTypeDescriptor *TypeRecords) :
                                     Name(TypeDescriptor.Name),
                                     ElementType(TypeDescriptor.ElementType) {
  for (uint64 i = 0; i < TypeDescriptor.ElementCount; i++) {
    Records.emplace_back(TypeRecords[i], this);
  }
}

void DwarfEnumTypeInfo::DumpTypes(UserDefinedDwarfTypesBuilder *TypeBuilder, MCObjectStreamer *Streamer,
      MCSection *TypeSection, MCSection *StrSection) {
  if (IsDumpedTypes)
    return;

  DwarfInfo::DumpTypes(TypeBuilder, Streamer, TypeSection, StrSection);

  DwarfInfo *Info = TypeBuilder->GetTypeInfoByIndex(ElementType);
  assert(Info != nullptr);

  Info->Dump(TypeBuilder, Streamer, TypeSection, StrSection);
}

void DwarfEnumTypeInfo::Dump(UserDefinedDwarfTypesBuilder *TypeBuilder, MCObjectStreamer *Streamer,
      MCSection *TypeSection, MCSection *StrSection) {
  if (IsDumped)
    return;

  MCContext &context = Streamer->getContext();
  unsigned TargetPointerSize = context.getAsmInfo()->getCodePointerSize();

  DwarfPrimitiveTypeInfo *ElementTypeInfo = static_cast<DwarfPrimitiveTypeInfo*>(
      TypeBuilder->GetTypeInfoByIndex(ElementType));
  assert(ElementTypeInfo != nullptr);

  PrimitiveTypeDesc TD = GetPrimitiveTypeDesc(ElementTypeInfo->GetType(), TargetPointerSize);
  ByteSize = TD.ByteSize;

  DwarfInfo::Dump(TypeBuilder, Streamer, TypeSection, StrSection);

  for (auto &Enumerator : Records) {
    Enumerator.Dump(TypeBuilder, Streamer, TypeSection, StrSection);
  }

  // Terminate DIE
  Streamer->SwitchSection(TypeSection);
  Streamer->EmitIntValue(0, 1);
}

void DwarfEnumTypeInfo::DumpStrings(MCObjectStreamer *Streamer) {
  Streamer->EmitBytes(Name);
  Streamer->EmitIntValue(0, 1);
}

void DwarfEnumTypeInfo::DumpTypeInfo(MCObjectStreamer *Streamer, UserDefinedDwarfTypesBuilder *TypeBuilder) {
  // Abbrev Number
  Streamer->EmitULEB128IntValue(DwarfAbbrev::EnumerationType);

  // DW_AT_name
  EmitSectionOffset(Streamer, StrSymbol, 4);

  // DW_AT_type
  DwarfInfo *Info = TypeBuilder->GetTypeInfoByIndex(ElementType);
  assert(Info != nullptr);

  EmitInfoOffset(Streamer, Info, 4);

  // DW_AT_byte_size
  Streamer->EmitIntValue(ByteSize, 1);
}

// DwarfDataField

void DwarfDataField::DumpStrings(MCObjectStreamer *Streamer) {
  Streamer->EmitBytes(StringRef(Name));
  Streamer->EmitIntValue(0, 1);
}

void DwarfDataField::DumpTypes(UserDefinedDwarfTypesBuilder *TypeBuilder, MCObjectStreamer *Streamer,
    MCSection *TypeSection, MCSection *StrSection) {
  if (IsDumpedTypes)
    return;

  DwarfInfo::DumpTypes(TypeBuilder, Streamer, TypeSection, StrSection);

  DwarfInfo *MemberTypeInfo = TypeBuilder->GetTypeInfoByIndex(TypeIndex);
  assert(MemberTypeInfo != nullptr);

  MemberTypeInfo->Dump(TypeBuilder, Streamer, TypeSection, StrSection);
}

void DwarfDataField::DumpTypeInfo(MCObjectStreamer *Streamer, UserDefinedDwarfTypesBuilder *TypeBuilder) {
  // Abbrev Number
  Streamer->EmitULEB128IntValue(IsStatic ? DwarfAbbrev::ClassMemberStatic : DwarfAbbrev::ClassMember);

  // DW_AT_name
  EmitSectionOffset(Streamer, StrSymbol, 4);

  // DW_AT_type
  DwarfInfo *MemberTypeInfo = TypeBuilder->GetTypeInfoByIndex(TypeIndex);
  assert(MemberTypeInfo != nullptr);
  EmitInfoOffset(Streamer, MemberTypeInfo, 4);

  if (!IsStatic) {
    // DW_AT_data_member_location
    Streamer->EmitIntValue(Offset, 4);
  }
}

// DwarfClassTypeInfo

DwarfClassTypeInfo::DwarfClassTypeInfo(const ClassTypeDescriptor &ClassDescriptor,
                                       const ClassFieldsTypeDescriptior &ClassFieldsDescriptor,
                                       const DataFieldDescriptor *FieldsDescriptors) :
                                       Name(ClassDescriptor.Name),
                                       IsStruct(ClassDescriptor.IsStruct),
                                       BaseClassId(ClassDescriptor.BaseClassId),
                                       Size(ClassDescriptor.InstanceSize),
                                       IsForwardDecl(false) {
  for (int32_t i = 0; i < ClassFieldsDescriptor.FieldsCount; i++) {
    Fields.emplace_back(FieldsDescriptors[i]);
  }
}

void DwarfClassTypeInfo::DumpTypes(UserDefinedDwarfTypesBuilder *TypeBuilder, MCObjectStreamer *Streamer,
    MCSection *TypeSection, MCSection *StrSection) {
  if (IsDumpedTypes)
    return;

  DwarfInfo::DumpTypes(TypeBuilder, Streamer, TypeSection, StrSection);

  if (BaseClassId != 0) {
    DwarfInfo *BaseClassInfo = TypeBuilder->GetTypeInfoByIndex(BaseClassId);
    assert(BaseClassInfo != nullptr);

    BaseClassInfo->Dump(TypeBuilder, Streamer, TypeSection, StrSection);
  }

  for (auto &Field : Fields) {
    Field.DumpTypes(TypeBuilder, Streamer, TypeSection, StrSection);
  }

  for (auto *Function : MemberFunctions) {
    Function->DumpTypes(TypeBuilder, Streamer, TypeSection, StrSection);
  }
}

void DwarfClassTypeInfo::Dump(UserDefinedDwarfTypesBuilder *TypeBuilder, MCObjectStreamer *Streamer,
    MCSection *TypeSection, MCSection *StrSection) {
  if (IsDumped)
    return;

  DwarfInfo::Dump(TypeBuilder, Streamer, TypeSection, StrSection);

  if (IsForwardDecl)
    return;

  for (auto &Field : Fields) {
    Field.Dump(TypeBuilder, Streamer, TypeSection, StrSection);
  }

  for (auto *Function : MemberFunctions) {
    Function->Dump(TypeBuilder, Streamer, TypeSection, StrSection);
  }

  // Terminate DIE
  Streamer->SwitchSection(TypeSection);
  Streamer->EmitIntValue(0, 1);
}

void DwarfClassTypeInfo::DumpStrings(MCObjectStreamer *Streamer) {
  Streamer->EmitBytes(StringRef(Name));
  Streamer->EmitIntValue(0, 1);
}

void DwarfClassTypeInfo::DumpTypeInfo(MCObjectStreamer *Streamer, UserDefinedDwarfTypesBuilder *TypeBuilder) {
  // Abbrev Number
  Streamer->EmitULEB128IntValue(IsForwardDecl ? DwarfAbbrev::ClassTypeDecl : DwarfAbbrev::ClassType);

  // DW_AT_name
  EmitSectionOffset(Streamer, StrSymbol, 4);

  if (!IsForwardDecl) {
    // DW_AT_byte_size
    Streamer->EmitIntValue(Size, 4);
  }

  if (BaseClassId != 0) {
    DwarfInfo *BaseClassInfo = TypeBuilder->GetTypeInfoByIndex(BaseClassId);
    assert(BaseClassInfo != nullptr);

    // DW_TAG_inheritance DIE

    // Abbrev Number
    Streamer->EmitULEB128IntValue(DwarfAbbrev::ClassInheritance);

    // DW_AT_type
    EmitInfoOffset(Streamer, BaseClassInfo, 4);

    // DW_AT_data_member_location = 0
    Streamer->EmitIntValue(0, 1);
  }
}

// DwarfSimpleArrayTypeInfo

void DwarfSimpleArrayTypeInfo::DumpTypes(UserDefinedDwarfTypesBuilder *TypeBuilder, MCObjectStreamer *Streamer,
    MCSection *TypeSection, MCSection *StrSection) {
  if (IsDumpedTypes)
    return;

  DwarfInfo::DumpTypes(TypeBuilder, Streamer, TypeSection, StrSection);

  DwarfInfo *ElementInfo = TypeBuilder->GetTypeInfoByIndex(ElementType);
  assert(ElementInfo != nullptr);

  ElementInfo->Dump(TypeBuilder, Streamer, TypeSection, StrSection);
}

void DwarfSimpleArrayTypeInfo::DumpStrings(MCObjectStreamer *Streamer) {
  // nothing to dump
}

void DwarfSimpleArrayTypeInfo::DumpTypeInfo(MCObjectStreamer *Streamer, UserDefinedDwarfTypesBuilder *TypeBuilder) {
  // Abbrev Number
  Streamer->EmitULEB128IntValue(DwarfAbbrev::ArrayType);

  DwarfInfo *Info = TypeBuilder->GetTypeInfoByIndex(ElementType);
  assert(Info != nullptr);

  // DW_AT_type
  EmitInfoOffset(Streamer, Info, 4);

  // DW_TAG_subrange_type DIE

  // Abbrev Number
  Streamer->EmitULEB128IntValue(DwarfAbbrev::SubrangeType);

  // DW_AT_upper_bound
  Streamer->EmitULEB128IntValue(Size - 1);

  // Terminate DIE
  Streamer->EmitIntValue(0, 1);
}

// DwarfPointerTypeInfo

void DwarfPointerTypeInfo::DumpTypes(UserDefinedDwarfTypesBuilder *TypeBuilder, MCObjectStreamer *Streamer,
    MCSection *TypeSection, MCSection *StrSection) {
  if (IsDumpedTypes)
    return;

  DwarfInfo::DumpTypes(TypeBuilder, Streamer, TypeSection, StrSection);

  DwarfInfo *Info = TypeBuilder->GetTypeInfoByIndex(TypeDesc.ElementType);
  assert(Info != nullptr);

  Info->Dump(TypeBuilder, Streamer, TypeSection, StrSection);
}

void DwarfPointerTypeInfo::DumpStrings(MCObjectStreamer *Streamer) {
  // nothing to dump
}

void DwarfPointerTypeInfo::DumpTypeInfo(MCObjectStreamer *Streamer, UserDefinedDwarfTypesBuilder *TypeBuilder) {
  // Abbrev Number
  Streamer->EmitULEB128IntValue(TypeDesc.IsReference ? DwarfAbbrev::ReferenceType : DwarfAbbrev::PointerType);

  DwarfInfo *Info = TypeBuilder->GetTypeInfoByIndex(TypeDesc.ElementType);
  assert(Info != nullptr);

  // DW_AT_type
  EmitInfoOffset(Streamer, Info, 4);

  // DW_AT_byte_size
  Streamer->EmitIntValue(TypeDesc.Is64Bit ? 8 : 4, 1);
}

// DwarfMemberFunctionTypeInfo

DwarfMemberFunctionTypeInfo::DwarfMemberFunctionTypeInfo(
    const MemberFunctionTypeDescriptor& MemberDescriptor,
    uint32_t const *const ArgumentTypes,
    bool IsStaticMethod) :
    TypeDesc(MemberDescriptor),
    IsStaticMethod(IsStaticMethod) {
  for (uint16_t i = 0; i < MemberDescriptor.NumberOfArguments; i++) {
    this->ArgumentTypes.push_back(ArgumentTypes[i]);
  }
}

void DwarfMemberFunctionTypeInfo::DumpStrings(MCObjectStreamer *Streamer) {
  // nothing to dump
}

void DwarfMemberFunctionTypeInfo::DumpTypeInfo(MCObjectStreamer *Streamer, UserDefinedDwarfTypesBuilder *TypeBuilder) {
  // nothing to dump
}

// DwarfMemberFunctionIdTypeInfo

void DwarfMemberFunctionIdTypeInfo::DumpTypes(UserDefinedDwarfTypesBuilder *TypeBuilder, MCObjectStreamer *Streamer,
      MCSection *TypeSection, MCSection *StrSection) {
  if (IsDumpedTypes)
    return;

  DwarfInfo::DumpTypes(TypeBuilder, Streamer, TypeSection, StrSection);

  // Dump return type
  DwarfInfo *ReturnTypeInfo = TypeBuilder->GetTypeInfoByIndex(MemberFunctionTypeInfo->GetReturnTypeIndex());
  assert(ReturnTypeInfo != nullptr);

  ReturnTypeInfo->Dump(TypeBuilder, Streamer, TypeSection, StrSection);

  // Dump this pointer type
  if (!MemberFunctionTypeInfo->IsStatic()) {
    DwarfInfo *ThisPtrTypeInfo = TypeBuilder->GetTypeInfoByIndex(MemberFunctionTypeInfo->GetThisPtrTypeIndex());
    assert(ThisPtrTypeInfo != nullptr);

    ThisPtrTypeInfo->Dump(TypeBuilder, Streamer, TypeSection, StrSection);
  }

  // Dump argument types
  for (uint32_t ArgTypeIndex : MemberFunctionTypeInfo->GetArgTypes()) {
    DwarfInfo *ArgTypeInfo = TypeBuilder->GetTypeInfoByIndex(ArgTypeIndex);
    assert(ArgTypeInfo != nullptr);
    ArgTypeInfo->Dump(TypeBuilder, Streamer, TypeSection, StrSection);
  }
}

void DwarfMemberFunctionIdTypeInfo::DumpStrings(MCObjectStreamer *Streamer) {
  Streamer->EmitBytes(StringRef(Name));
  Streamer->EmitIntValue(0, 1);

  MCContext &context = Streamer->getContext();
  LinkageNameSymbol = context.createTempSymbol();
  Streamer->EmitLabel(LinkageNameSymbol);
  Streamer->EmitBytes(StringRef(LinkageName));
  Streamer->EmitIntValue(0, 1);
}

void DwarfMemberFunctionIdTypeInfo::DumpTypeInfo(MCObjectStreamer *Streamer, UserDefinedDwarfTypesBuilder *TypeBuilder) {
  // Abbrev Number
  bool IsStatic = MemberFunctionTypeInfo->IsStatic();

  Streamer->EmitULEB128IntValue(IsStatic ? DwarfAbbrev::SubprogramStaticSpec : DwarfAbbrev::SubprogramSpec);

  // DW_AT_name
  EmitSectionOffset(Streamer, StrSymbol, 4);

  // DW_AT_linkage_name
  EmitSectionOffset(Streamer, LinkageNameSymbol, 4);

  // DW_AT_decl_file
  Streamer->EmitIntValue(1, 1);

  // DW_AT_decl_line
  Streamer->EmitIntValue(1, 1);

  // DW_AT_type
  DwarfInfo *ReturnTypeInfo = TypeBuilder->GetTypeInfoByIndex(MemberFunctionTypeInfo->GetReturnTypeIndex());
  assert(ReturnTypeInfo != nullptr);

  EmitInfoOffset(Streamer, ReturnTypeInfo, 4);

  if (!IsStatic) {
    // DW_AT_object_pointer
    uint32_t Offset = Streamer->getOrCreateDataFragment()->getContents().size();

    Streamer->EmitIntValue(Offset + 4, 4);

    // This formal parameter DIE
    DwarfInfo *ThisTypeInfo = TypeBuilder->GetTypeInfoByIndex(MemberFunctionTypeInfo->GetThisPtrTypeIndex());
    assert(ThisTypeInfo != nullptr);

    // Abbrev Number
    Streamer->EmitULEB128IntValue(DwarfAbbrev::FormalParameterThisSpec);

    // DW_AT_type
    EmitInfoOffset(Streamer, ThisTypeInfo, 4);
  }

  for (uint32_t ArgTypeIndex : MemberFunctionTypeInfo->GetArgTypes()) {
    DwarfInfo *ArgTypeInfo = TypeBuilder->GetTypeInfoByIndex(ArgTypeIndex);
    assert(ArgTypeInfo != nullptr);

    // Formal parameter DIE

    // Abbrev Number
    Streamer->EmitULEB128IntValue(DwarfAbbrev::FormalParameterSpec);

    // DW_AT_type
    EmitInfoOffset(Streamer, ArgTypeInfo, 4);
  }

  // Ternimate DIE
  Streamer->EmitIntValue(0, 1);
}

// DwarfTypesBuilder

void UserDefinedDwarfTypesBuilder::EmitTypeInformation(
    MCSection *TypeSection,
    MCSection *StrSection) {
  for (auto &Info : DwarfTypes) {
    Info->Dump(this, Streamer, TypeSection, StrSection);
  }
}

unsigned UserDefinedDwarfTypesBuilder::GetEnumTypeIndex(
    const EnumTypeDescriptor &TypeDescriptor,
    const EnumRecordTypeDescriptor *TypeRecords) {
  unsigned TypeIndex = ArrayIndexToTypeIndex(DwarfTypes.size());
  UserDefinedTypes.push_back(std::make_pair(TypeDescriptor.Name, TypeIndex));
  DwarfTypes.push_back(make_unique<DwarfEnumTypeInfo>(TypeDescriptor, TypeRecords));
  return TypeIndex;
}

unsigned UserDefinedDwarfTypesBuilder::GetClassTypeIndex(
    const ClassTypeDescriptor &ClassDescriptor) {
  unsigned TypeIndex = ArrayIndexToTypeIndex(DwarfTypes.size());
  DwarfTypes.push_back(make_unique<DwarfClassTypeInfo>(ClassDescriptor));
  return TypeIndex;
}

unsigned UserDefinedDwarfTypesBuilder::GetCompleteClassTypeIndex(
    const ClassTypeDescriptor &ClassDescriptor,
    const ClassFieldsTypeDescriptior &ClassFieldsDescriptor,
    const DataFieldDescriptor *FieldsDescriptors) {
  unsigned TypeIndex = ArrayIndexToTypeIndex(DwarfTypes.size());
  UserDefinedTypes.push_back(std::make_pair(ClassDescriptor.Name, TypeIndex));
  DwarfTypes.push_back(make_unique<DwarfClassTypeInfo>(ClassDescriptor, ClassFieldsDescriptor, FieldsDescriptors));
  return TypeIndex;
}

unsigned UserDefinedDwarfTypesBuilder::GetArrayTypeIndex(
    const ClassTypeDescriptor &ClassDescriptor,
    const ArrayTypeDescriptor &ArrayDescriptor) {
  // Create corresponding class info
  ClassTypeDescriptor ArrayClassDescriptor = ClassDescriptor;

  std::vector<DataFieldDescriptor> FieldDescs;
  unsigned FieldOffset = TargetPointerSize;

  FieldDescs.push_back({GetPrimitiveTypeIndex(PrimitiveTypeFlags::Int32), FieldOffset, "m_NumComponents"});
  FieldOffset += TargetPointerSize;

  if (ArrayDescriptor.IsMultiDimensional == 1) {
    unsigned BoundsTypeIndex = GetSimpleArrayTypeIndex(GetPrimitiveTypeIndex(PrimitiveTypeFlags::Int32), ArrayDescriptor.Rank);
    FieldDescs.push_back({BoundsTypeIndex, FieldOffset, "m_Bounds"});
    FieldOffset += 2 * 4 * ArrayDescriptor.Rank;
  }

  unsigned DataTypeIndex = GetSimpleArrayTypeIndex(ArrayDescriptor.ElementType, 0);
  FieldDescs.push_back({DataTypeIndex, FieldOffset, "m_Data"});

  ClassFieldsTypeDescriptior FieldsTypeDesc =
      {TargetPointerSize, ArrayDescriptor.IsMultiDimensional ? 3 : 2};

  ArrayClassDescriptor.InstanceSize = FieldOffset;

  unsigned TypeIndex = ArrayIndexToTypeIndex(DwarfTypes.size());
  UserDefinedTypes.push_back(std::make_pair(ArrayClassDescriptor.Name, TypeIndex));
  DwarfTypes.push_back(make_unique<DwarfClassTypeInfo>(ArrayClassDescriptor, FieldsTypeDesc, FieldDescs.data()));

  return TypeIndex;
}

unsigned UserDefinedDwarfTypesBuilder::GetPointerTypeIndex(const PointerTypeDescriptor& PointerDescriptor)
{
  unsigned TypeIndex = ArrayIndexToTypeIndex(DwarfTypes.size());
  DwarfTypes.push_back(make_unique<DwarfPointerTypeInfo>(PointerDescriptor));
  return TypeIndex;
}

unsigned UserDefinedDwarfTypesBuilder::GetMemberFunctionTypeIndex(const MemberFunctionTypeDescriptor& MemberDescriptor,
    uint32_t const *const ArgumentTypes)
{
  unsigned TypeIndex = ArrayIndexToTypeIndex(DwarfTypes.size());
  bool IsStatic = MemberDescriptor.TypeIndexOfThisPointer == GetPrimitiveTypeIndex(PrimitiveTypeFlags::Void);
  DwarfTypes.push_back(make_unique<DwarfMemberFunctionTypeInfo>(MemberDescriptor, ArgumentTypes, IsStatic));
  return TypeIndex;
}

unsigned UserDefinedDwarfTypesBuilder::GetMemberFunctionId(const MemberFunctionIdTypeDescriptor& MemberIdDescriptor)
{
  unsigned TypeIndex = ArrayIndexToTypeIndex(DwarfTypes.size());

  DwarfMemberFunctionTypeInfo *MemberFunctionTypeInfo = static_cast<DwarfMemberFunctionTypeInfo*>(
      GetTypeInfoByIndex(MemberIdDescriptor.MemberFunction));
  assert(MemberFunctionTypeInfo != nullptr);

  DwarfMemberFunctionIdTypeInfo *MemberFunctionIdTypeInfo =
      new DwarfMemberFunctionIdTypeInfo(MemberIdDescriptor, MemberFunctionTypeInfo);

  DwarfTypes.push_back(std::unique_ptr<DwarfMemberFunctionIdTypeInfo>(MemberFunctionIdTypeInfo));

  DwarfClassTypeInfo *ParentClassInfo = static_cast<DwarfClassTypeInfo*>(
      GetTypeInfoByIndex(MemberIdDescriptor.ParentClass));
  assert(ParentClassInfo != nullptr);

  ParentClassInfo->AddMemberFunction(MemberFunctionIdTypeInfo);

  return TypeIndex;
}

unsigned UserDefinedDwarfTypesBuilder::GetPrimitiveTypeIndex(PrimitiveTypeFlags Type) {
  auto Iter = PrimitiveDwarfTypes.find(Type);
  if (Iter != PrimitiveDwarfTypes.end())
    return Iter->second;

  unsigned TypeIndex = ArrayIndexToTypeIndex(DwarfTypes.size());
  DwarfTypes.push_back(make_unique<DwarfPrimitiveTypeInfo>(Type));

  PrimitiveDwarfTypes.insert(std::make_pair(Type, TypeIndex));

  return TypeIndex;
}

unsigned UserDefinedDwarfTypesBuilder::GetSimpleArrayTypeIndex(unsigned ElemIndex, unsigned Size) {
  auto Iter = SimpleArrayDwarfTypes.find(ElemIndex);
  if (Iter != SimpleArrayDwarfTypes.end()) {
    auto CountMap = Iter->second;
    auto CountIter = CountMap.find(Size);
    if (CountIter != CountMap.end())
      return CountIter->second;
  }

  unsigned TypeIndex = ArrayIndexToTypeIndex(DwarfTypes.size());
  DwarfTypes.push_back(make_unique<DwarfSimpleArrayTypeInfo>(ElemIndex, Size));

  SimpleArrayDwarfTypes[ElemIndex][Size] = TypeIndex;

  return TypeIndex;
}