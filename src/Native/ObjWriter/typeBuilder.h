//===---- typeBuilder.h --------------------------------*- C++ -*-===//
//
// type builder is used to convert .Net types into CodeView descriptors.
//
// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license.
// See LICENSE file in the project root for full license information.
//
//===----------------------------------------------------------------------===//

#pragma once

#include "llvm/DebugInfo/CodeView/TypeTableBuilder.h"
#include "llvm/MC/MCObjectStreamer.h"

#include <string>
#include <vector>

using namespace llvm;
using namespace llvm::codeview;

typedef unsigned long long uint64;

#pragma pack(push, 8)

extern "C" struct EnumRecordTypeDescriptor {
  uint64 Value;
  char *Name;
};

extern "C" struct EnumTypeDescriptor {
  uint32_t ElementType;
  uint64 ElementCount;
  char *Name;
};

extern "C" struct ClassTypeDescriptor {
  int32_t IsStruct;
  char *Name;
  uint32_t BaseClassId;
};

extern "C" struct DataFieldDescriptor {
  uint32_t FieldTypeIndex;
  uint64 Offset;
  char *Name;
};

extern "C" struct ClassFieldsTypeDescriptior {
  uint64 Size;
  int32_t FieldsCount;
};

extern "C" struct ArrayTypeDescriptor {
  uint32_t Rank;
  uint32_t ElementType;
  uint32_t Size;
  int32_t IsMultiDimensional;
};

extern "C" struct PointerTypeDescriptor {
    uint32_t ElementType;
    int32_t IsReference;
    int32_t IsConst;
    int32_t Is64Bit;
};

extern "C" struct MemberFunctionTypeDescriptor {
    uint32_t ReturnType;
    uint32_t ContainingClass;
    uint32_t TypeIndexOfThisPointer;
    int32_t ThisAdjust;
    uint32_t CallingConvention;
    uint16_t NumberOfArguments;
};

extern "C" struct MemberFunctionIdTypeDescriptor {
    uint32_t MemberFunction;
    uint32_t ParentClass;
    char *Name;
};

class ArrayDimensionsDescriptor {
public:
  const char *GetLengthName(unsigned index);
  const char *GetBoundsName(unsigned index);

private:
  void Resize(unsigned NewSize);

  std::vector<std::string> Lengths;
  std::vector<std::string> Bounds;
};

#pragma pack(pop)
class UserDefinedTypesBuilder {
public:
  UserDefinedTypesBuilder();
  void SetStreamer(MCObjectStreamer *Streamer);
  void SetTargetPointerSize(unsigned TargetPointerSize);
  void EmitTypeInformation(MCSection *COFFDebugTypesSection);

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

  const std::vector<std::pair<std::string, codeview::TypeIndex>> &GetUDTs() {
    return UserDefinedTypes;
  }

private:
  void EmitCodeViewMagicVersion();
  ClassOptions GetCommonClassOptions();

  unsigned GetEnumFieldListType(uint64 Count,
                                const EnumRecordTypeDescriptor *TypeRecords);

  void AddBaseClass(FieldListRecordBuilder &FLBR, unsigned BaseClassId);
  void AddClassVTShape(FieldListRecordBuilder &FLBR);

  BumpPtrAllocator Allocator;
  TypeTableBuilder TypeTable;

  MCObjectStreamer *Streamer;
  unsigned TargetPointerSize;

  ArrayDimensionsDescriptor ArrayDimentions;
  TypeIndex ClassVTableTypeIndex;

  std::vector<std::pair<std::string, codeview::TypeIndex>> UserDefinedTypes;
};
