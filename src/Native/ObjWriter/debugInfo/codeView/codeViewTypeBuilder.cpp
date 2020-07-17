//===---- codeViewTypeBuilder.cpp -------------------------------*- C++ -*-===//
//
// type builder implementation using codeview::TypeTableBuilder
//
// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
//
//===----------------------------------------------------------------------===//

#include "codeViewTypeBuilder.h"
#include "llvm/BinaryFormat/COFF.h"
#include <sstream>
#include <vector>

UserDefinedCodeViewTypesBuilder::UserDefinedCodeViewTypesBuilder()
    : Allocator(), TypeTable(Allocator)
{
    // We pretend that the EEType pointer in System.Object is VTable shape.
    // We use the same "Vtable" for all types because the vtable shape debug
    // record is not expressive enough to capture our vtable shape (where the
    // vtable slots don't start at the beginning of the vtable).
    VFTableShapeRecord vfTableShape(TypeRecordKind::VFTableShape);
    ClassVTableTypeIndex = TypeTable.writeKnownType(vfTableShape);

    PointerRecord ptrToVfTableShape(ClassVTableTypeIndex,
        TargetPointerSize == 8 ? PointerKind::Near64 : PointerKind::Near32,
        PointerMode::LValueReference,
        PointerOptions::None,
        0);

    VFuncTabTypeIndex = TypeTable.writeKnownType(ptrToVfTableShape);
}

void UserDefinedCodeViewTypesBuilder::EmitCodeViewMagicVersion() {
  Streamer->EmitValueToAlignment(4);
  Streamer->AddComment("Debug section magic");
  Streamer->EmitIntValue(COFF::DEBUG_SECTION_MAGIC, 4);
}

ClassOptions UserDefinedCodeViewTypesBuilder::GetCommonClassOptions() {
  return ClassOptions();
}

void UserDefinedCodeViewTypesBuilder::EmitTypeInformation(
    MCSection *TypeSection,
    MCSection *StrSection) {

  if (TypeTable.empty())
    return;

  Streamer->SwitchSection(TypeSection);
  EmitCodeViewMagicVersion();

  TypeTable.ForEachRecord([&](TypeIndex FieldTypeIndex,
                              ArrayRef<uint8_t> Record) {
    StringRef S(reinterpret_cast<const char *>(Record.data()), Record.size());
    Streamer->EmitBinaryData(S);
  });
}

unsigned UserDefinedCodeViewTypesBuilder::GetEnumFieldListType(
    uint64 Count, const EnumRecordTypeDescriptor *TypeRecords) {
  FieldListRecordBuilder FLRB(TypeTable);
  FLRB.begin();
#ifndef NDEBUG
  uint64 MaxInt = (unsigned int)-1;
  assert(Count <= MaxInt && "There are too many fields inside enum");
#endif
  for (int i = 0; i < (int)Count; ++i) {
    EnumRecordTypeDescriptor record = TypeRecords[i];
    EnumeratorRecord ER(MemberAccess::Public, APSInt::getUnsigned(record.Value),
                        record.Name);
    FLRB.writeMemberType(ER);
  }
  TypeIndex Type = FLRB.end(true);
  return Type.getIndex();
}

unsigned UserDefinedCodeViewTypesBuilder::GetEnumTypeIndex(
    const EnumTypeDescriptor &TypeDescriptor,
    const EnumRecordTypeDescriptor *TypeRecords) {

  ClassOptions CO = GetCommonClassOptions();
  unsigned FieldListIndex =
      GetEnumFieldListType(TypeDescriptor.ElementCount, TypeRecords);
  TypeIndex FieldListIndexType = TypeIndex(FieldListIndex);
  TypeIndex ElementTypeIndex = TypeIndex(TypeDescriptor.ElementType);

  EnumRecord EnumRecord(TypeDescriptor.ElementCount, CO, FieldListIndexType,
                        TypeDescriptor.Name, StringRef(),
                        ElementTypeIndex);

  TypeIndex Type = TypeTable.writeKnownType(EnumRecord);
  UserDefinedTypes.push_back(std::make_pair(TypeDescriptor.Name, Type.getIndex()));
  return Type.getIndex();
}

unsigned UserDefinedCodeViewTypesBuilder::GetClassTypeIndex(
    const ClassTypeDescriptor &ClassDescriptor) {
  TypeRecordKind Kind =
      ClassDescriptor.IsStruct ? TypeRecordKind::Struct : TypeRecordKind::Class;
  ClassOptions CO = ClassOptions::ForwardReference | GetCommonClassOptions();

  ClassRecord CR(Kind, 0, CO, TypeIndex(), TypeIndex(), TypeIndex(), 0,
                 ClassDescriptor.Name, StringRef());
  TypeIndex FwdDeclTI = TypeTable.writeKnownType(CR);
  return FwdDeclTI.getIndex();
}

unsigned UserDefinedCodeViewTypesBuilder::GetCompleteClassTypeIndex(
    const ClassTypeDescriptor &ClassDescriptor,
    const ClassFieldsTypeDescriptior &ClassFieldsDescriptor,
    const DataFieldDescriptor *FieldsDescriptors,
    const StaticDataFieldDescriptor *StaticsDescriptors) {

  FieldListRecordBuilder FLBR(TypeTable);
  FLBR.begin();

  uint16_t memberCount = 0;
  if (ClassDescriptor.BaseClassId != 0) {
    memberCount++;
    AddBaseClass(FLBR, ClassDescriptor.BaseClassId);
  }
  else if (!ClassDescriptor.IsStruct) {
    memberCount++;
    AddClassVTShape(FLBR);
  }

  for (int i = 0; i < ClassFieldsDescriptor.FieldsCount; ++i) {
    DataFieldDescriptor desc = FieldsDescriptors[i];
    MemberAccess Access = MemberAccess::Public;
    TypeIndex MemberBaseType(desc.FieldTypeIndex);
    if (desc.Offset == 0xFFFFFFFF)
    {
      StaticDataMemberRecord SDMR(Access, MemberBaseType, desc.Name);
      FLBR.writeMemberType(SDMR);
    }
    else
    {
      int MemberOffsetInBytes = desc.Offset;
      DataMemberRecord DMR(Access, MemberBaseType, MemberOffsetInBytes,
          desc.Name);
      FLBR.writeMemberType(DMR);
    }
    memberCount++;
  }
  TypeIndex FieldListIndex = FLBR.end(true);
  TypeRecordKind Kind =
      ClassDescriptor.IsStruct ? TypeRecordKind::Struct : TypeRecordKind::Class;
  ClassOptions CO = GetCommonClassOptions();
  ClassRecord CR(Kind, memberCount, CO, FieldListIndex,
                 TypeIndex(), TypeIndex(), ClassFieldsDescriptor.Size,
                 ClassDescriptor.Name, StringRef());
  TypeIndex ClassIndex = TypeTable.writeKnownType(CR);

  UserDefinedTypes.push_back(std::make_pair(ClassDescriptor.Name, ClassIndex.getIndex()));

  return ClassIndex.getIndex();
}

unsigned UserDefinedCodeViewTypesBuilder::GetArrayTypeIndex(
    const ClassTypeDescriptor &ClassDescriptor,
    const ArrayTypeDescriptor &ArrayDescriptor) {
  FieldListRecordBuilder FLBR(TypeTable);
  FLBR.begin();

  unsigned Offset = 0;
  unsigned FieldsCount = 0;

  assert(ClassDescriptor.BaseClassId != 0);

  AddBaseClass(FLBR, ClassDescriptor.BaseClassId);
  FieldsCount++;
  Offset += TargetPointerSize;

  MemberAccess Access = MemberAccess::Public;
  TypeIndex IndexType = TypeIndex(SimpleTypeKind::Int32);
  DataMemberRecord CountDMR(Access, IndexType, Offset, "count");
  FLBR.writeMemberType(CountDMR);
  FieldsCount++;
  Offset += TargetPointerSize;

  if (ArrayDescriptor.IsMultiDimensional == 1) {
    for (unsigned i = 0; i < ArrayDescriptor.Rank; ++i) {
      DataMemberRecord LengthDMR(Access, TypeIndex(SimpleTypeKind::Int32),
                                 Offset, ArrayDimentions.GetLengthName(i));
      FLBR.writeMemberType(LengthDMR);
      FieldsCount++;
      Offset += 4;
    }

    for (unsigned i = 0; i < ArrayDescriptor.Rank; ++i) {
      DataMemberRecord BoundsDMR(Access, TypeIndex(SimpleTypeKind::Int32),
                                 Offset, ArrayDimentions.GetBoundsName(i));
      FLBR.writeMemberType(BoundsDMR);
      FieldsCount++;
      Offset += 4;
    }
  }

  TypeIndex ElementTypeIndex = TypeIndex(ArrayDescriptor.ElementType);
  ArrayRecord AR(ElementTypeIndex, IndexType, ArrayDescriptor.Size, "");
  TypeIndex ArrayIndex = TypeTable.writeKnownType(AR);
  DataMemberRecord ArrayDMR(Access, ArrayIndex, Offset, "values");
  FLBR.writeMemberType(ArrayDMR);
  FieldsCount++;

  TypeIndex FieldListIndex = FLBR.end(true);

  assert(ClassDescriptor.IsStruct == false);
  TypeRecordKind Kind = TypeRecordKind::Class;
  ClassOptions CO = GetCommonClassOptions();
  ClassRecord CR(Kind, FieldsCount, CO, FieldListIndex, TypeIndex(),
                 TypeIndex(), ArrayDescriptor.Size, ClassDescriptor.Name,
                 StringRef());
  TypeIndex ClassIndex = TypeTable.writeKnownType(CR);

  UserDefinedTypes.push_back(std::make_pair(ClassDescriptor.Name, ClassIndex.getIndex()));

  return ClassIndex.getIndex();
}

unsigned UserDefinedCodeViewTypesBuilder::GetPointerTypeIndex(const PointerTypeDescriptor& PointerDescriptor)
{
    uint32_t elementType = PointerDescriptor.ElementType;
    PointerKind pointerKind = PointerDescriptor.Is64Bit ? PointerKind::Near64 : PointerKind::Near32;
    PointerMode pointerMode = PointerDescriptor.IsReference ? PointerMode::LValueReference : PointerMode::Pointer;
    PointerOptions pointerOptions = PointerDescriptor.IsConst ? PointerOptions::Const : PointerOptions::None;

    PointerRecord PointerToClass(TypeIndex(elementType), pointerKind, pointerMode, pointerOptions, 0);
    TypeIndex PointerIndex = TypeTable.writeKnownType(PointerToClass);
    return PointerIndex.getIndex();
}

unsigned UserDefinedCodeViewTypesBuilder::GetMemberFunctionTypeIndex(const MemberFunctionTypeDescriptor& MemberDescriptor,
    uint32_t const *const ArgumentTypes)
{
    std::vector<TypeIndex> argumentTypes;
    argumentTypes.reserve(MemberDescriptor.NumberOfArguments);
    for (uint16_t iArgument = 0; iArgument < MemberDescriptor.NumberOfArguments; iArgument++)
    {
        argumentTypes.emplace_back(ArgumentTypes[iArgument]);
    }

    ArgListRecord ArgList(TypeRecordKind::ArgList, argumentTypes);
    TypeIndex ArgumentList = TypeTable.writeKnownType(ArgList);

    MemberFunctionRecord MemberFunction(TypeIndex(MemberDescriptor.ReturnType), 
                                        TypeIndex(MemberDescriptor.ContainingClass), 
                                        TypeIndex(MemberDescriptor.TypeIndexOfThisPointer), 
                                        CallingConvention(MemberDescriptor.CallingConvention), 
                                        FunctionOptions::None, MemberDescriptor.NumberOfArguments, 
                                        ArgumentList, 
                                        MemberDescriptor.ThisAdjust);

    TypeIndex MemberFunctionIndex = TypeTable.writeKnownType(MemberFunction);
    return MemberFunctionIndex.getIndex();
}

unsigned UserDefinedCodeViewTypesBuilder::GetMemberFunctionId(const MemberFunctionIdTypeDescriptor& MemberIdDescriptor)
{
    MemberFuncIdRecord MemberFuncId(TypeIndex(MemberIdDescriptor.MemberFunction), TypeIndex(MemberIdDescriptor.ParentClass), MemberIdDescriptor.Name);
    TypeIndex MemberFuncIdIndex = TypeTable.writeKnownType(MemberFuncId);
    return MemberFuncIdIndex.getIndex();
}

unsigned UserDefinedCodeViewTypesBuilder::GetPrimitiveTypeIndex(PrimitiveTypeFlags Type) {
  switch (Type) {
    case PrimitiveTypeFlags::Void:
      return TypeIndex::Void().getIndex();
    case PrimitiveTypeFlags::Boolean:
      return TypeIndex(SimpleTypeKind::Boolean8).getIndex();
    case PrimitiveTypeFlags::Char:
      return TypeIndex::WideCharacter().getIndex();
    case PrimitiveTypeFlags::SByte:
      return TypeIndex(SimpleTypeKind::SByte).getIndex();
    case PrimitiveTypeFlags::Byte:
      return TypeIndex(SimpleTypeKind::Byte).getIndex();
    case PrimitiveTypeFlags::Int16:
      return TypeIndex(SimpleTypeKind::Int16).getIndex();
    case PrimitiveTypeFlags::UInt16:
      return TypeIndex(SimpleTypeKind::UInt16).getIndex();
    case PrimitiveTypeFlags::Int32:
      return TypeIndex::Int32().getIndex();
    case PrimitiveTypeFlags::UInt32:
      return TypeIndex::UInt32().getIndex();
    case PrimitiveTypeFlags::Int64:
      return TypeIndex::Int64().getIndex();
    case PrimitiveTypeFlags::UInt64:
      return TypeIndex::UInt64().getIndex();
    case PrimitiveTypeFlags::Single:
      return TypeIndex::Float32().getIndex();
    case PrimitiveTypeFlags::Double:
      return TypeIndex::Float64().getIndex();
    case PrimitiveTypeFlags::IntPtr:
    case PrimitiveTypeFlags::UIntPtr:
      return TargetPointerSize == 4 ? TypeIndex::VoidPointer32().getIndex() :
        TypeIndex::VoidPointer64().getIndex();
    default:
      assert(false && "Unexpected type");
      return 0;
  }
}

void UserDefinedCodeViewTypesBuilder::AddBaseClass(FieldListRecordBuilder &FLBR,
                                           unsigned BaseClassId) {
  MemberAttributes def;
  TypeIndex BaseTypeIndex(BaseClassId);
  BaseClassRecord BCR(def, BaseTypeIndex, 0);
  FLBR.writeMemberType(BCR);
}

void UserDefinedCodeViewTypesBuilder::AddClassVTShape(FieldListRecordBuilder &FLBR) {
  VFPtrRecord VfPtr(VFuncTabTypeIndex);
  FLBR.writeMemberType(VfPtr);
}

const char *ArrayDimensionsDescriptor::GetLengthName(unsigned index) {
  if (Lengths.size() <= index) {
    Resize(index + 1);
  }
  return Lengths[index].c_str();
}

const char *ArrayDimensionsDescriptor::GetBoundsName(unsigned index) {
  if (Bounds.size() <= index) {
    Resize(index);
  }
  return Bounds[index].c_str();
}

void ArrayDimensionsDescriptor::Resize(unsigned NewSize) {
  unsigned OldSize = Lengths.size();
  assert(OldSize == Bounds.size());
  Lengths.resize(NewSize);
  Bounds.resize(NewSize);
  for (unsigned i = OldSize; i < NewSize; ++i) {
    std::stringstream ss;
    ss << "length" << i;
    ss >> Lengths[i];
    ss.clear();
    ss << "bounds" << i;
    ss >> Bounds[i];
  }
}
