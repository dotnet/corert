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
  .custom instance void [mscorlib]System.Runtime.CompilerServices.RuntimeCompatibilityAttribute::.ctor() = ( 01 00 01 00 54 02 16 57 72 61 70 4E 6F 6E 45 78   // ....T..WrapNonEx
                                                                                                             63 65 70 74 69 6F 6E 54 68 72 6F 77 73 01 )       // ceptionThrows.
  .custom instance void [mscorlib]System.Runtime.CompilerServices.CompilationRelaxationsAttribute::.ctor(int32) = ( 01 00 08 00 00 00 00 00 ) 
  .hash algorithm 0x00008004
  .ver 0:0:0:0
}
.module PreInitData.exe
// MVID: {9448CCAF-5D07-453D-8015-888B75F6925D}
.imagebase 0x00400000
.file alignment 0x00000200
.stackreserve 0x00100000
.subsystem 0x0003       // WINDOWS_CUI
.corflags 0x00000001    //  ILONLY
// Image base: 0x024E0000


// =============== CLASS MEMBERS DECLARATION ===================

.class private auto ansi beforefieldinit System.Runtime.CompilerServices.PreInitializedAttribute
       extends [mscorlib]System.Attribute
{
  .custom instance void [mscorlib]System.AttributeUsageAttribute::.ctor(valuetype [mscorlib]System.AttributeTargets) = ( 01 00 00 01 00 00 01 00 54 02 0D 41 6C 6C 6F 77   // ........T..Allow
                                                                                                                         4D 75 6C 74 69 70 6C 65 00 )                      // Multiple.
  .method public hidebysig specialname rtspecialname 
          instance void  .ctor() cil managed
  {
    // Code size       7 (0x7)
    .maxstack  8
    IL_0000:  ldarg.0
    IL_0001:  call       instance void [mscorlib]System.Attribute::.ctor()
    IL_0006:  ret
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
    // Code size       10 (0xa)
    .maxstack  8
    IL_0000:  ldarg.0
    IL_0001:  call       instance void [mscorlib]System.Attribute::.ctor()
    IL_0006:  nop
    IL_0007:  nop
    IL_0008:  nop
    IL_0009:  ret
  } // end of method InitDataBlobAttribute::.ctor

} // end of class System.Runtime.CompilerServices.InitDataBlobAttribute

.class private auto ansi beforefieldinit System.Runtime.CompilerServices.TypeHandleFixupAttribute
       extends [mscorlib]System.Attribute
{
  .custom instance void [mscorlib]System.AttributeUsageAttribute::.ctor(valuetype [mscorlib]System.AttributeTargets) = ( 01 00 00 01 00 00 01 00 54 02 0D 41 6C 6C 6F 77   // ........T..Allow
                                                                                                                         4D 75 6C 74 69 70 6C 65 01 )                      // Multiple.
  .method public hidebysig specialname rtspecialname 
          instance void  .ctor(int32 offset,
                               class [mscorlib]System.Type fixupType) cil managed
  {
    // Code size       10 (0xa)
    .maxstack  8
    IL_0000:  ldarg.0
    IL_0001:  call       instance void [mscorlib]System.Attribute::.ctor()
    IL_0006:  nop
    IL_0007:  nop
    IL_0008:  nop
    IL_0009:  ret
  } // end of method TypeHandleFixupAttribute::.ctor

} // end of class System.Runtime.CompilerServices.TypeHandleFixupAttribute

.class private auto ansi beforefieldinit System.Runtime.CompilerServices.MethodAddrFixupAttribute
       extends [mscorlib]System.Attribute
{
  .custom instance void [mscorlib]System.AttributeUsageAttribute::.ctor(valuetype [mscorlib]System.AttributeTargets) = ( 01 00 00 01 00 00 01 00 54 02 0D 41 6C 6C 6F 77   // ........T..Allow
                                                                                                                         4D 75 6C 74 69 70 6C 65 01 )                      // Multiple.
  .method public hidebysig specialname rtspecialname 
          instance void  .ctor(int32 offset,
                               class [mscorlib]System.Type fixupType,
                               string methodName) cil managed
  {
    // Code size       10 (0xa)
    .maxstack  8
    IL_0000:  ldarg.0
    IL_0001:  call       instance void [mscorlib]System.Attribute::.ctor()
    IL_0006:  nop
    IL_0007:  nop
    IL_0008:  nop
    IL_0009:  ret
  } // end of method MethodAddrFixupAttribute::.ctor

} // end of class System.Runtime.CompilerServices.MethodAddrFixupAttribute

.class private auto ansi beforefieldinit Details
       extends [mscorlib]System.Object
{
  .field private static valuetype '<PrivateImplementationDetails>'/'__StaticArrayInitTypeSize=16'  PreInitializedField_DataBlob at I_00002208
  .field private static valuetype '<PrivateImplementationDetails>'/'__StaticArrayInitTypeSize=32' PreInitializedTypeField_DataBlob at I_00002209
  .custom instance void System.Runtime.CompilerServices.TypeHandleFixupAttribute::.ctor(int32,
                                                                                        class [mscorlib]System.Type) = ( 01 00 18 00 00 00 5A 53 79 73 74 65 6D 2E 53 74   // ......ZSystem.St
                                                                                                                         72 69 6E 67 2C 20 6D 73 63 6F 72 6C 69 62 2C 20   // ring, mscorlib, 
                                                                                                                         56 65 72 73 69 6F 6E 3D 34 2E 30 2E 30 2E 30 2C   // Version=4.0.0.0,
                                                                                                                         20 43 75 6C 74 75 72 65 3D 6E 65 75 74 72 61 6C   //  Culture=neutral
                                                                                                                         2C 20 50 75 62 6C 69 63 4B 65 79 54 6F 6B 65 6E   // , PublicKeyToken
                                                                                                                         3D 62 37 37 61 35 63 35 36 31 39 33 34 65 30 38   // =b77a5c561934e08
                                                                                                                         39 00 00 )                                        // 9..
  .custom instance void System.Runtime.CompilerServices.TypeHandleFixupAttribute::.ctor(int32,
                                                                                        class [mscorlib]System.Type) = ( 01 00 10 00 00 00 59 53 79 73 74 65 6D 2E 49 6E   // ......YSystem.In
                                                                                                                         74 36 34 2C 20 6D 73 63 6F 72 6C 69 62 2C 20 56   // t64, mscorlib, V
                                                                                                                         65 72 73 69 6F 6E 3D 34 2E 30 2E 30 2E 30 2C 20   // ersion=4.0.0.0, 
                                                                                                                         43 75 6C 74 75 72 65 3D 6E 65 75 74 72 61 6C 2C   // Culture=neutral,
                                                                                                                         20 50 75 62 6C 69 63 4B 65 79 54 6F 6B 65 6E 3D   //  PublicKeyToken=
                                                                                                                         62 37 37 61 35 63 35 36 31 39 33 34 65 30 38 39   // b77a5c561934e089
                                                                                                                         00 00 ) 
  .custom instance void System.Runtime.CompilerServices.TypeHandleFixupAttribute::.ctor(int32,
                                                                                        class [mscorlib]System.Type) = ( 01 00 08 00 00 00 59 53 79 73 74 65 6D 2E 49 6E   // ......YSystem.In
                                                                                                                         74 31 36 2C 20 6D 73 63 6F 72 6C 69 62 2C 20 56   // t16, mscorlib, V
                                                                                                                         65 72 73 69 6F 6E 3D 34 2E 30 2E 30 2E 30 2C 20   // ersion=4.0.0.0, 
                                                                                                                         43 75 6C 74 75 72 65 3D 6E 65 75 74 72 61 6C 2C   // Culture=neutral,
                                                                                                                         20 50 75 62 6C 69 63 4B 65 79 54 6F 6B 65 6E 3D   //  PublicKeyToken=
                                                                                                                         62 37 37 61 35 63 35 36 31 39 33 34 65 30 38 39   // b77a5c561934e089
                                                                                                                         00 00 ) 
  .custom instance void System.Runtime.CompilerServices.TypeHandleFixupAttribute::.ctor(int32,
                                                                                        class [mscorlib]System.Type) = ( 01 00 00 00 00 00 59 53 79 73 74 65 6D 2E 49 6E   // ......YSystem.In
                                                                                                                         74 33 32 2C 20 6D 73 63 6F 72 6C 69 62 2C 20 56   // t32, mscorlib, V
                                                                                                                         65 72 73 69 6F 6E 3D 34 2E 30 2E 30 2E 30 2C 20   // ersion=4.0.0.0, 
                                                                                                                         43 75 6C 74 75 72 65 3D 6E 65 75 74 72 61 6C 2C   // Culture=neutral,
                                                                                                                         20 50 75 62 6C 69 63 4B 65 79 54 6F 6B 65 6E 3D   //  PublicKeyToken=
                                                                                                                         62 37 37 61 35 63 35 36 31 39 33 34 65 30 38 39   // b77a5c561934e089
                                                                                                                         00 00 ) 
  .method public hidebysig specialname rtspecialname 
          instance void  .ctor() cil managed
  {
    // Code size       7 (0x7)
    .maxstack  8
    IL_0000:  ldarg.0
    IL_0001:  call       instance void [mscorlib]System.Object::.ctor()
    IL_0006:  ret
  } // end of method Details::.ctor

} // end of class Details

.class public auto ansi beforefieldinit PreInitDataTest
       extends [mscorlib]System.Object
{
  .field private static literal int32 Pass = int32(0x00000064)
  .field private static literal int32 Fail = int32(0xFFFFFFFF)
  .field private static string StaticStringFieldBefore
  .field private static int32[] PreInitializedIntField
  .custom instance void System.Runtime.CompilerServices.PreInitializedAttribute::.ctor() = ( 01 00 00 00 ) 
  .custom instance void System.Runtime.CompilerServices.InitDataBlobAttribute::.ctor(class [mscorlib]System.Type,
                                                                                     string) = ( 01 00 07 44 65 74 61 69 6C 73 1C 50 72 65 49 6E   // ...Details.PreIn
                                                                                                 69 74 69 61 6C 69 7A 65 64 46 69 65 6C 64 5F 44   // itializedField_D
                                                                                                 61 74 61 42 6C 6F 62 00 00 )                      // ataBlob..
  .field private static class [mscorlib]System.RuntimeTypeHandle[] PreInitializedTypeField
  .custom instance void System.Runtime.CompilerServices.PreInitializedAttribute::.ctor() = ( 01 00 00 00 ) 
  .custom instance void System.Runtime.CompilerServices.InitDataBlobAttribute::.ctor(class [mscorlib]System.Type,
                                                                                     string) = ( 01 00 07 44 65 74 61 69 6C 73 20 50 72 65 49 6E   // ...Details PreIn
                                                                                                 69 74 69 61 6C 69 7A 65 64 54 79 70 65 46 69 65   // itializedTypeFie
                                                                                                 6C 64 5F 44 61 74 61 42 6C 6F 62 00 00 )          // ld_DataBlob..
  .field private static string StaticStringFieldAfter
  .method public hidebysig static int32  Main(string[] args) cil managed
  {
    .entrypoint
    // Code size       58 (0x3a)
    .maxstack  1
    .locals init (int32 V_0,
             int32 V_1,
             bool V_2)
    IL_0000:  nop
    IL_0001:  ldc.i4.s   100
    IL_0003:  stloc.0
    IL_0004:  call       bool PreInitDataTest::TestPreInitData()
    IL_0009:  stloc.2
    IL_000a:  ldloc.2
    IL_000b:  brtrue.s   IL_001c

    IL_000d:  nop
    IL_000e:  ldstr      "Failed"
    IL_0013:  call       void [mscorlib]System.Console::WriteLine(string)
    IL_0018:  nop
    IL_0019:  ldc.i4.m1
    IL_001a:  stloc.0
    IL_001b:  nop
    IL_001c:  call       bool PreInitDataTest::TestOtherStatics()
    IL_0021:  stloc.2
    IL_0022:  ldloc.2
    IL_0023:  brtrue.s   IL_0034

    IL_0025:  nop
    IL_0026:  ldstr      "Failed"
    IL_002b:  call       void [mscorlib]System.Console::WriteLine(string)
    IL_0030:  nop
    IL_0031:  ldc.i4.m1
    IL_0032:  stloc.0
    IL_0033:  nop
    IL_0034:  ldloc.0
    IL_0035:  stloc.1
    IL_0036:  br.s       IL_0038

    IL_0038:  ldloc.1
    IL_0039:  ret
  } // end of method PreInitDataTest::Main

  .method private hidebysig static bool  TestPreInitData() cil managed
  {
    // Code size       197 (0xc5)
    .maxstack  3
    .locals init (int32 V_0,
             bool V_1,
             bool V_2)
    IL_0000:  nop
    IL_0001:  ldstr      "Testing preinitialized array..."
    IL_0006:  call       void [mscorlib]System.Console::WriteLine(string)
    IL_000b:  nop
    IL_000c:  ldc.i4.0
    IL_000d:  stloc.0
    IL_000e:  br.s       IL_002d

    IL_0010:  nop
    IL_0011:  ldsfld     int32[] PreInitDataTest::PreInitializedIntField
    IL_0016:  ldloc.0
    IL_0017:  ldelem.i4
    IL_0018:  ldloc.0
    IL_0019:  ldc.i4.1
    IL_001a:  add
    IL_001b:  ceq
    IL_001d:  stloc.2
    IL_001e:  ldloc.2
    IL_001f:  brtrue.s   IL_0028

    IL_0021:  ldc.i4.0
    IL_0022:  stloc.1
    IL_0023:  br         IL_00c3

    IL_0028:  nop
    IL_0029:  ldloc.0
    IL_002a:  ldc.i4.1
    IL_002b:  add
    IL_002c:  stloc.0
    IL_002d:  ldloc.0
    IL_002e:  ldsfld     int32[] PreInitDataTest::PreInitializedIntField
    IL_0033:  ldlen
    IL_0034:  conv.i4
    IL_0035:  clt
    IL_0037:  stloc.2
    IL_0038:  ldloc.2
    IL_0039:  brtrue.s   IL_0010

    IL_003b:  ldsfld     valuetype [mscorlib]System.RuntimeTypeHandle[] PreInitDataTest::PreInitializedTypeField
    IL_0040:  ldc.i4.0
    IL_0041:  ldelem.ref
              ldind.i
    IL_0042:  ldtoken    [mscorlib]System.Int32
    IL_004c:  call       bool [mscorlib]System.RuntimeTypeHandle::op_Inequality(valuetype [mscorlib]System.RuntimeTypeHandle,
                                                                   valuetype [mscorlib]System.RuntimeTypeHandle)
    IL_0051:  ldc.i4.0
    IL_0052:  ceq
    IL_0054:  stloc.2
    IL_0055:  ldloc.2
    IL_0056:  brtrue.s   IL_005c

    IL_0058:  ldc.i4.0
    IL_0059:  stloc.1
    IL_005a:  br.s       IL_00c3

    IL_005c:  ldsfld     valuetype [mscorlib]System.RuntimeTypeHandle[] PreInitDataTest::PreInitializedTypeField
    IL_0061:  ldc.i4.1
    IL_0062:  ldelem.ref
    IL_0063:  ldtoken    [mscorlib]System.Int16
    IL_006d:  call       bool [mscorlib]System.RuntimeTypeHandle::op_Inequality(valuetype [mscorlib]System.RuntimeTypeHandle,
                                                                   valuetype [mscorlib]System.RuntimeTypeHandle)
    IL_0072:  ldc.i4.0
    IL_0073:  ceq
    IL_0075:  stloc.2
    IL_0076:  ldloc.2
    IL_0077:  brtrue.s   IL_007d

    IL_0079:  ldc.i4.0
    IL_007a:  stloc.1
    IL_007b:  br.s       IL_00c3

    IL_007d:  ldsfld     valuetype [mscorlib]System.RuntimeTypeHandle[] PreInitDataTest::PreInitializedTypeField
    IL_0082:  ldc.i4.2
    IL_0083:  ldelem.ref
    IL_0084:  ldtoken    [mscorlib]System.Int64
    IL_008e:  call       bool [mscorlib]System.RuntimeTypeHandle::op_Inequality(valuetype [mscorlib]System.RuntimeTypeHandle,
                                                                   valuetype [mscorlib]System.RuntimeTypeHandle)
    IL_0093:  ldc.i4.0
    IL_0094:  ceq
    IL_0096:  stloc.2
    IL_0097:  ldloc.2
    IL_0098:  brtrue.s   IL_009e

    IL_009a:  ldc.i4.0
    IL_009b:  stloc.1
    IL_009c:  br.s       IL_00c3

    IL_009e:  ldsfld     valuetype [mscorlib]System.RuntimeTypeHandle[] PreInitDataTest::PreInitializedTypeField
    IL_00a3:  ldc.i4.3
    IL_00a4:  ldelem.ref
    IL_00a5:  ldtoken    [mscorlib]System.String
    IL_00af:  call       bool [mscorlib]System.RuntimeTypeHandle::op_Inequality(valuetype [mscorlib]System.RuntimeTypeHandle,
                                                                   valuetype [mscorlib]System.RuntimeTypeHandle)
    IL_00b4:  ldc.i4.0
    IL_00b5:  ceq
    IL_00b7:  stloc.2
    IL_00b8:  ldloc.2
    IL_00b9:  brtrue.s   IL_00bf

    IL_00bb:  ldc.i4.0
    IL_00bc:  stloc.1
    IL_00bd:  br.s       IL_00c3

    IL_00bf:  ldc.i4.1
    IL_00c0:  stloc.1
    IL_00c1:  br.s       IL_00c3

    IL_00c3:  ldloc.1
    IL_00c4:  ret
  } // end of method PreInitDataTest::TestPreInitData

  .method private hidebysig static bool  TestOtherStatics() cil managed
  {
    // Code size       70 (0x46)
    .maxstack  2
    .locals init (bool V_0,
             bool V_1)
    IL_0000:  nop
    IL_0001:  ldstr      "Testing other statics work well with preinitialize"
    + "d data in the same type..."
    IL_0006:  call       void [mscorlib]System.Console::WriteLine(string)
    IL_000b:  nop
    IL_000c:  ldsfld     string PreInitDataTest::StaticStringFieldBefore
    IL_0011:  ldstr      "BEFORE"
    IL_0016:  call       bool [mscorlib]System.String::op_Inequality(string,
                                                                     string)
    IL_001b:  ldc.i4.0
    IL_001c:  ceq
    IL_001e:  stloc.1
    IL_001f:  ldloc.1
    IL_0020:  brtrue.s   IL_0026

    IL_0022:  ldc.i4.0
    IL_0023:  stloc.0
    IL_0024:  br.s       IL_0044

    IL_0026:  ldsfld     string PreInitDataTest::StaticStringFieldAfter
    IL_002b:  ldstr      "AFTER"
    IL_0030:  call       bool [mscorlib]System.String::op_Inequality(string,
                                                                     string)
    IL_0035:  ldc.i4.0
    IL_0036:  ceq
    IL_0038:  stloc.1
    IL_0039:  ldloc.1
    IL_003a:  brtrue.s   IL_0040

    IL_003c:  ldc.i4.0
    IL_003d:  stloc.0
    IL_003e:  br.s       IL_0044

    IL_0040:  ldc.i4.1
    IL_0041:  stloc.0
    IL_0042:  br.s       IL_0044

    IL_0044:  ldloc.0
    IL_0045:  ret
  } // end of method PreInitDataTest::TestOtherStatics

  .method public hidebysig specialname rtspecialname 
          instance void  .ctor() cil managed
  {
    // Code size       7 (0x7)
    .maxstack  8
    IL_0000:  ldarg.0
    IL_0001:  call       instance void [mscorlib]System.Object::.ctor()
    IL_0006:  ret
  } // end of method PreInitDataTest::.ctor

  .method private hidebysig specialname rtspecialname static 
          void  .cctor() cil managed
  {
    // Code size       43 (0x2b)
    .maxstack  8
    IL_0000:  ldstr      "BEFORE"
    IL_0005:  stsfld     string PreInitDataTest::StaticStringFieldBefore
    IL_0020:  ldstr      "AFTER"
    IL_0025:  stsfld     string PreInitDataTest::StaticStringFieldAfter
    IL_002a:  ret
  } // end of method PreInitDataTest::.cctor

} // end of class PreInitDataTest

.class private auto ansi '<PrivateImplementationDetails>'
       extends [mscorlib]System.Object
{
  .custom instance void [mscorlib]System.Runtime.CompilerServices.CompilerGeneratedAttribute::.ctor() = ( 01 00 00 00 ) 
  .class explicit ansi sealed nested private '__StaticArrayInitTypeSize=16'
         extends [mscorlib]System.ValueType
  {
    .pack 1
    .size 16
  } // end of class '__StaticArrayInitTypeSize=16'
  .class explicit ansi sealed nested private '__StaticArrayInitTypeSize=32'
         extends [mscorlib]System.ValueType
  {
    .pack 1
    .size 32
  } // end of class '__StaticArrayInitTypeSize=16'
} // end of class '<PrivateImplementationDetails>'


// =============================================================

.data cil I_00002208 = bytearray (
                 01 00 00 00 02 00 00 00 03 00 00 00 04 00 00 00) 
.data cil I_00002209 = bytearray (
                 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00)
// *********** DISASSEMBLY COMPLETE ***********************
// WARNING: Created Win32 resource file PreInitData.res
