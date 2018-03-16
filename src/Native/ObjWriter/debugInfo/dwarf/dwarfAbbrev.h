//===---- dwarfAbbrev.h -----------------------------------------*- C++ -*-===//
//
// dwarf abbreviations
//
// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.
//
//===----------------------------------------------------------------------===//

#pragma once

#include "llvm/BinaryFormat/Dwarf.h"
#include "llvm/MC/MCObjectStreamer.h"

using namespace llvm;

namespace DwarfAbbrev {

enum DwarfAbbrev : uint16_t
{
  CompileUnit = 0x1,
  BaseType,
  EnumerationType,
  Enumerator1,
  Enumerator2,
  Enumerator4,
  Enumerator8,
  TypeDef,
  Subprogram,
  SubprogramStatic,
  SubprogramSpec,
  SubprogramStaticSpec,
  Variable,
  VariableLoc,
  VariableStatic,
  FormalParameter,
  FormalParameterThis,
  FormalParameterLoc,
  FormalParameterThisLoc,
  FormalParameterSpec,
  FormalParameterThisSpec,
  ClassType,
  ClassTypeDecl,
  ClassMember,
  ClassMemberStatic,
  PointerType,
  ReferenceType,
  ArrayType,
  SubrangeType,
  ClassInheritance,
  LexicalBlock,
  TryBlock,
  CatchBlock
};

void Dump(MCObjectStreamer *Streamer, uint16_t DwarfVersion, unsigned TargetPointerSize);

}
