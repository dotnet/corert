// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.    

//  Microsoft (R) .NET Framework IL Disassembler.  Version 4.6.1055.0
//  Copyright (c) Microsoft Corporation.  All rights reserved.

// Metadata version: v4.0.30319
.assembly extern mscorlib
{
  .publickeytoken = (B7 7A 5C 56 19 34 E0 89 )                         // .z\V.4..
  .ver 4:0:0:0
}
.assembly PreInitData
{
  .custom instance void [mscorlib]System.Runtime.CompilerServices.CompilationRelaxationsAttribute::.ctor(int32) = ( 01 00 08 00 00 00 00 00 ) 
  .custom instance void [mscorlib]System.Runtime.CompilerServices.RuntimeCompatibilityAttribute::.ctor() = ( 01 00 01 00 54 02 16 57 72 61 70 4E 6F 6E 45 78   // ....T..WrapNonEx
                                                                                                             63 65 70 74 69 6F 6E 54 68 72 6F 77 73 01 )       // ceptionThrows.

  // --- The following custom attribute is added automatically, do not uncomment -------
  //  .custom instance void [mscorlib]System.Diagnostics.DebuggableAttribute::.ctor(valuetype [mscorlib]System.Diagnostics.DebuggableAttribute/DebuggingModes) = ( 01 00 07 01 00 00 00 00 ) 

  .hash algorithm 0x00008004
  .ver 0:0:0:0
}
.module PreInitData.exe
// MVID: {916169DC-051D-4529-A01D-E629C1FB5BFB}
.imagebase 0x00400000
.file alignment 0x00000200
.stackreserve 0x00100000
.subsystem 0x0003       // WINDOWS_CUI
.corflags 0x00000001    //  ILONLY
// Image base: 0x001E0000


// =============== CLASS MEMBERS DECLARATION ===================

.class private auto ansi beforefieldinit Details
       extends [mscorlib]System.Object
{
  .class explicit ansi sealed nested private '__PreInitData__Size=16'
         extends [mscorlib]System.ValueType
  {
    .pack 1
    .size 16
  } // end of class '__PreInitData__Size=16'   
  
  .field private static initonly valuetype Details/'__PreInitData__Size=16' PreInitializedField_DataBlob at I_00002A28
  .method public hidebysig specialname rtspecialname 
          instance void  .ctor() cil managed
  {
    // Code size       8 (0x8)
    .maxstack  8
    IL_0000:  ldarg.0
    IL_0001:  call       instance void [mscorlib]System.Object::.ctor()
    IL_0006:  nop
    IL_0007:  ret
  } // end of method Details::.ctor
} // end of class Details

.class public auto ansi beforefieldinit PreInitDataTest
       extends [mscorlib]System.Object
{
  .field private static int32[] StaticIntArrayField
  .field private static int32[] PreInitializedField
  .custom instance void System.Runtime.CompilerServices.PreInitializedAttribute::.ctor() = ( 01 00 00 00 ) 
  .custom instance void System.Runtime.CompilerServices.InitDataBlobAttribute::.ctor(class [mscorlib]System.Type,
                                                                                     string) = ( 01 00 07 44 65 74 61 69 6C 73 1C 50 72 65 49 6E   // ...Details.PreIn
                                                                                                 69 74 69 61 6C 69 7A 65 64 46 69 65 6C 64 5F 44   // itializedField_D
                                                                                                 61 74 61 42 6C 6F 62 00 00 )                      // ataBlob..
  .field private static string StaticStringField
  .field private static literal int32 Pass = int32(0x00000064)
  .field private static literal int32 Fail = int32(0xFFFFFFFF)
  .method public hidebysig static int32  Main(string[] args) cil managed
  {
    .entrypoint
    // Code size       64 (0x40)
    .maxstack  2
    .locals init (int32 V_0,
             bool V_1,
             bool V_2,
             int32 V_3)
    IL_0000:  nop
    IL_0001:  ldc.i4.s   100
    IL_0003:  stloc.0
    IL_0004:  call       bool PreInitDataTest::TestPreInitData()
    IL_0009:  ldc.i4.0
    IL_000a:  ceq
    IL_000c:  stloc.1
    IL_000d:  ldloc.1
    IL_000e:  brfalse.s  IL_001f

    IL_0010:  nop
    IL_0011:  ldstr      "Failed"
    IL_0016:  call       void [mscorlib]System.Console::WriteLine(string)
    IL_001b:  nop
    IL_001c:  ldc.i4.m1
    IL_001d:  stloc.0
    IL_001e:  nop
    IL_001f:  call       bool PreInitDataTest::TestOtherStatics()
    IL_0024:  ldc.i4.0
    IL_0025:  ceq
    IL_0027:  stloc.2
    IL_0028:  ldloc.2
    IL_0029:  brfalse.s  IL_003a

    IL_002b:  nop
    IL_002c:  ldstr      "Failed"
    IL_0031:  call       void [mscorlib]System.Console::WriteLine(string)
    IL_0036:  nop
    IL_0037:  ldc.i4.m1
    IL_0038:  stloc.0
    IL_0039:  nop
    IL_003a:  ldloc.0
    IL_003b:  stloc.3
    IL_003c:  br.s       IL_003e

    IL_003e:  ldloc.3
    IL_003f:  ret
  } // end of method PreInitDataTest::Main

  .method private hidebysig static bool  TestPreInitData() cil managed
  {
    // Code size       65 (0x41)
    .maxstack  3
    .locals init (int32 V_0,
             bool V_1,
             bool V_2,
             bool V_3)
    IL_0000:  nop
    IL_0001:  ldstr      "Testing preinitialized array..."
    IL_0006:  call       void [mscorlib]System.Console::WriteLine(string)
    IL_000b:  nop
    IL_000c:  ldc.i4.0
    IL_000d:  stloc.0
    IL_000e:  br.s       IL_002d

    IL_0010:  nop
    IL_0011:  ldsfld     int32[] PreInitDataTest::PreInitializedField
    IL_0016:  ldloc.0
    IL_0017:  ldelem.i4
    IL_0018:  ldloc.0
    IL_0019:  ldc.i4.1
    IL_001a:  add
    IL_001b:  ceq
    IL_001d:  ldc.i4.0
    IL_001e:  ceq
    IL_0020:  stloc.1
    IL_0021:  ldloc.1
    IL_0022:  brfalse.s  IL_0028

    IL_0024:  ldc.i4.0
    IL_0025:  stloc.2
    IL_0026:  br.s       IL_003f

    IL_0028:  nop
    IL_0029:  ldloc.0
    IL_002a:  ldc.i4.1
    IL_002b:  add
    IL_002c:  stloc.0
    IL_002d:  ldloc.0
    IL_002e:  ldsfld     int32[] PreInitDataTest::PreInitializedField
    IL_0033:  ldlen
    IL_0034:  conv.i4
    IL_0035:  clt
    IL_0037:  stloc.3
    IL_0038:  ldloc.3
    IL_0039:  brtrue.s   IL_0010

    IL_003b:  ldc.i4.1
    IL_003c:  stloc.2
    IL_003d:  br.s       IL_003f

    IL_003f:  ldloc.2
    IL_0040:  ret
  } // end of method PreInitDataTest::TestPreInitData

  .method private hidebysig static bool  TestOtherStatics() cil managed
  {
    // Code size       90 (0x5a)
    .maxstack  3
    .locals init (int32 V_0,
             bool V_1,
             bool V_2,
             bool V_3,
             bool V_4)
    IL_0000:  nop
    IL_0001:  ldstr      "Testing other statics work well with preinitialize"
    + "d data in the same type..."
    IL_0006:  call       void [mscorlib]System.Console::WriteLine(string)
    IL_000b:  nop
    IL_000c:  ldc.i4.0
    IL_000d:  stloc.0
    IL_000e:  br.s       IL_002d

    IL_0010:  nop
    IL_0011:  ldsfld     int32[] PreInitDataTest::StaticIntArrayField
    IL_0016:  ldloc.0
    IL_0017:  ldelem.i4
    IL_0018:  ldloc.0
    IL_0019:  ldc.i4.5
    IL_001a:  add
    IL_001b:  ceq
    IL_001d:  ldc.i4.0
    IL_001e:  ceq
    IL_0020:  stloc.1
    IL_0021:  ldloc.1
    IL_0022:  brfalse.s  IL_0028

    IL_0024:  ldc.i4.0
    IL_0025:  stloc.2
    IL_0026:  br.s       IL_0058

    IL_0028:  nop
    IL_0029:  ldloc.0
    IL_002a:  ldc.i4.1
    IL_002b:  add
    IL_002c:  stloc.0
    IL_002d:  ldloc.0
    IL_002e:  ldsfld     int32[] PreInitDataTest::StaticIntArrayField
    IL_0033:  ldlen
    IL_0034:  conv.i4
    IL_0035:  clt
    IL_0037:  stloc.3
    IL_0038:  ldloc.3
    IL_0039:  brtrue.s   IL_0010

    IL_003b:  ldsfld     string PreInitDataTest::StaticStringField
    IL_0040:  ldstr      "ABCDE"
    IL_0045:  call       bool [mscorlib]System.String::op_Inequality(string,
                                                                     string)
    IL_004a:  stloc.s    V_4
    IL_004c:  ldloc.s    V_4
    IL_004e:  brfalse.s  IL_0054

    IL_0050:  ldc.i4.0
    IL_0051:  stloc.2
    IL_0052:  br.s       IL_0058

    IL_0054:  ldc.i4.1
    IL_0055:  stloc.2
    IL_0056:  br.s       IL_0058

    IL_0058:  ldloc.2
    IL_0059:  ret
  } // end of method PreInitDataTest::TestOtherStatics

  .method public hidebysig specialname rtspecialname 
          instance void  .ctor() cil managed
  {
    // Code size       8 (0x8)
    .maxstack  8
    IL_0000:  ldarg.0
    IL_0001:  call       instance void [mscorlib]System.Object::.ctor()
    IL_0006:  nop
    IL_0007:  ret
  } // end of method PreInitDataTest::.ctor

  .method private hidebysig specialname rtspecialname static 
          void  .cctor() cil managed
  {
    // Code size       55 (0x37)
    .maxstack  8
    IL_0000:  ldc.i4.4
    IL_0001:  newarr     [mscorlib]System.Int32
    IL_0006:  dup
    IL_0007:  ldtoken    field valuetype '<PrivateImplementationDetails>'/'__StaticArrayInitTypeSize=16' '<PrivateImplementationDetails>'::AB434F2ED820934995191E4C1A3C24AB41FF2E2C
    IL_000c:  call       void [mscorlib]System.Runtime.CompilerServices.RuntimeHelpers::InitializeArray(class [mscorlib]System.Array,
                                                                                                        valuetype [mscorlib]System.RuntimeFieldHandle)
    IL_0011:  stsfld     int32[] PreInitDataTest::StaticIntArrayField
/*
    IL_0016:  ldc.i4.4
    IL_0017:  newarr     [mscorlib]System.Int32
    IL_001c:  dup
    IL_001d:  ldtoken    field valuetype '<PrivateImplementationDetails>'/'__StaticArrayInitTypeSize=16' '<PrivateImplementationDetails>'::'1456763F890A84558F99AFA687C36B9037697848'
    IL_0022:  call       void [mscorlib]System.Runtime.CompilerServices.RuntimeHelpers::InitializeArray(class [mscorlib]System.Array,
                                                                                                        valuetype [mscorlib]System.RuntimeFieldHandle)
    IL_0027:  stsfld     int32[] PreInitDataTest::PreInitializedField
*/
    IL_002c:  ldstr      "ABCDE"
    IL_0031:  stsfld     string PreInitDataTest::StaticStringField
    IL_0036:  ret
  } // end of method PreInitDataTest::.cctor

} // end of class PreInitDataTest

.class private auto ansi beforefieldinit System.Runtime.CompilerServices.PreInitializedAttribute
       extends [mscorlib]System.Attribute
{
  .custom instance void [mscorlib]System.AttributeUsageAttribute::.ctor(valuetype [mscorlib]System.AttributeTargets) = ( 01 00 00 01 00 00 01 00 54 02 0D 41 6C 6C 6F 77   // ........T..Allow
                                                                                                                         4D 75 6C 74 69 70 6C 65 00 )                      // Multiple.
  .method public hidebysig specialname rtspecialname 
          instance void  .ctor() cil managed
  {
    // Code size       8 (0x8)
    .maxstack  8
    IL_0000:  ldarg.0
    IL_0001:  call       instance void [mscorlib]System.Attribute::.ctor()
    IL_0006:  nop
    IL_0007:  ret
  } // end of method PreInitializedAttribute::.ctor

} // end of class System.Runtime.CompilerServices.PreInitializedAttribute

.class private auto ansi beforefieldinit System.Runtime.CompilerServices.InitDataBlobAttribute
       extends [mscorlib]System.Attribute
{
  .custom instance void [mscorlib]System.AttributeUsageAttribute::.ctor(valuetype [mscorlib]System.AttributeTargets) = ( 01 00 00 01 00 00 01 00 54 02 0D 41 6C 6C 6F 77   // ........T..Allow
                                                                                                                         4D 75 6C 74 69 70 6C 65 00 )                      // Multiple.
  .method public hidebysig specialname rtspecialname 
          instance void  .ctor(class [mscorlib]System.Type 'type',
                               string fieldName) cil managed
  {
    // Code size       9 (0x9)
    .maxstack  8
    IL_0000:  ldarg.0
    IL_0001:  call       instance void [mscorlib]System.Attribute::.ctor()
    IL_0006:  nop
    IL_0007:  nop
    IL_0008:  ret
  } // end of method InitDataBlobAttribute::.ctor

} // end of class System.Runtime.CompilerServices.InitDataBlobAttribute

.class private auto ansi sealed '<PrivateImplementationDetails>'
       extends [mscorlib]System.Object
{
  .custom instance void [mscorlib]System.Runtime.CompilerServices.CompilerGeneratedAttribute::.ctor() = ( 01 00 00 00 ) 
  .class explicit ansi sealed nested private '__StaticArrayInitTypeSize=16'
         extends [mscorlib]System.ValueType
  {
    .pack 1
    .size 16
  } // end of class '__StaticArrayInitTypeSize=16'

  .field static assembly initonly valuetype '<PrivateImplementationDetails>'/'__StaticArrayInitTypeSize=16' AB434F2ED820934995191E4C1A3C24AB41FF2E2C at I_00002A38
} // end of class '<PrivateImplementationDetails>'


// =============================================================

.data cil I_00002A28 = bytearray (
                 01 00 00 00 02 00 00 00 03 00 00 00 04 00 00 00) 
.data cil I_00002A38 = bytearray (
                 05 00 00 00 06 00 00 00 07 00 00 00 08 00 00 00) 
// *********** DISASSEMBLY COMPLETE ***********************
// WARNING: Created Win32 resource file PreInitData.res
