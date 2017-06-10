// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information. 

//  Microsoft (R) .NET Framework IL Disassembler.  Version 4.6.1055.0
//  Copyright (c) Microsoft Corporation.  All rights reserved.

.assembly extern mscorlib
{
  .publickeytoken = (B7 7A 5C 56 19 34 E0 89 )                         // .z\V.4..
  .ver 4:0:0:0
}

.assembly extern System.Private.CoreLib
{
  .publickeytoken = (B0 3F 5F 7F 11 D5 0A 3A )                         // .?_....:
  .ver 4:0:0:0
}

.assembly PreInitData
{
  .hash algorithm 0x00008004
  .ver 0:0:0:0
}

.module PreInitData.exe
.imagebase 0x00400000
.file alignment 0x00000200
.stackreserve 0x00100000
.subsystem 0x0003       // WINDOWS_CUI
.corflags 0x00000001    //  ILONLY
// Image base: 0x024E0000

// =============================================================
// PreInitialized data
// =============================================================

.class private auto ansi beforefieldinit Details
       extends [mscorlib]System.Object
{
  .field private static valuetype '<PrivateImplementationDetails>'/'__StaticArrayInitTypeSize=16'  PreInitializedField_DataBlob at Data_IntArray
  .field private static valuetype '<PrivateImplementationDetails>'/'__StaticArrayInitTypeSize=32' PreInitializedTypeField_DataBlob at Data_TypeArray
  .custom instance void [System.Private.CoreLib]System.Runtime.CompilerServices.TypeHandleFixupAttribute::.ctor(int32,
                                                                                        class [mscorlib]System.Type) = ( 01 00 18 00 00 00 5A 53 79 73 74 65 6D 2E 53 74   // ......ZSystem.St
                                                                                                                         72 69 6E 67 2C 20 6D 73 63 6F 72 6C 69 62 2C 20   // ring, mscorlib, 
                                                                                                                         56 65 72 73 69 6F 6E 3D 34 2E 30 2E 30 2E 30 2C   // Version=4.0.0.0,
                                                                                                                         20 43 75 6C 74 75 72 65 3D 6E 65 75 74 72 61 6C   //  Culture=neutral
                                                                                                                         2C 20 50 75 62 6C 69 63 4B 65 79 54 6F 6B 65 6E   // , PublicKeyToken
                                                                                                                         3D 62 37 37 61 35 63 35 36 31 39 33 34 65 30 38   // =b77a5c561934e08
                                                                                                                         39 00 00 )                                        // 9..
  .custom instance void [System.Private.CoreLib]System.Runtime.CompilerServices.TypeHandleFixupAttribute::.ctor(int32,
                                                                                        class [mscorlib]System.Type) = ( 01 00 10 00 00 00 59 53 79 73 74 65 6D 2E 49 6E   // ......YSystem.In
                                                                                                                         74 36 34 2C 20 6D 73 63 6F 72 6C 69 62 2C 20 56   // t64, mscorlib, V
                                                                                                                         65 72 73 69 6F 6E 3D 34 2E 30 2E 30 2E 30 2C 20   // ersion=4.0.0.0, 
                                                                                                                         43 75 6C 74 75 72 65 3D 6E 65 75 74 72 61 6C 2C   // Culture=neutral,
                                                                                                                         20 50 75 62 6C 69 63 4B 65 79 54 6F 6B 65 6E 3D   //  PublicKeyToken=
                                                                                                                         62 37 37 61 35 63 35 36 31 39 33 34 65 30 38 39   // b77a5c561934e089
                                                                                                                         00 00 ) 
  .custom instance void [System.Private.CoreLib]System.Runtime.CompilerServices.TypeHandleFixupAttribute::.ctor(int32,
                                                                                        class [mscorlib]System.Type) = ( 01 00 08 00 00 00 59 53 79 73 74 65 6D 2E 49 6E   // ......YSystem.In
                                                                                                                         74 31 36 2C 20 6D 73 63 6F 72 6C 69 62 2C 20 56   // t16, mscorlib, V
                                                                                                                         65 72 73 69 6F 6E 3D 34 2E 30 2E 30 2E 30 2C 20   // ersion=4.0.0.0, 
                                                                                                                         43 75 6C 74 75 72 65 3D 6E 65 75 74 72 61 6C 2C   // Culture=neutral,
                                                                                                                         20 50 75 62 6C 69 63 4B 65 79 54 6F 6B 65 6E 3D   //  PublicKeyToken=
                                                                                                                         62 37 37 61 35 63 35 36 31 39 33 34 65 30 38 39   // b77a5c561934e089
                                                                                                                         00 00 ) 
  .custom instance void [System.Private.CoreLib]System.Runtime.CompilerServices.TypeHandleFixupAttribute::.ctor(int32,
                                                                                        class [mscorlib]System.Type) = ( 01 00 00 00 00 00 59 53 79 73 74 65 6D 2E 49 6E   // ......YSystem.In
                                                                                                                         74 33 32 2C 20 6D 73 63 6F 72 6C 69 62 2C 20 56   // t32, mscorlib, V
                                                                                                                         65 72 73 69 6F 6E 3D 34 2E 30 2E 30 2E 30 2C 20   // ersion=4.0.0.0, 
                                                                                                                         43 75 6C 74 75 72 65 3D 6E 65 75 74 72 61 6C 2C   // Culture=neutral,
                                                                                                                         20 50 75 62 6C 69 63 4B 65 79 54 6F 6B 65 6E 3D   //  PublicKeyToken=
                                                                                                                         62 37 37 61 35 63 35 36 31 39 33 34 65 30 38 39   // b77a5c561934e089
                                                                                                                         00 00 ) 
} // end of class Details

.class public auto ansi beforefieldinit PreInitData
{
  .field private static string StaticStringFieldBefore
  .field private static int32[] PreInitializedIntField
  .custom instance void [System.Private.CoreLib]System.Runtime.CompilerServices.PreInitializedAttribute::.ctor() = ( 01 00 00 00 ) 
  .custom instance void [System.Private.CoreLib]System.Runtime.CompilerServices.InitDataBlobAttribute::.ctor(class [mscorlib]System.Type,
                                                                                     string) = ( 01 00 07 44 65 74 61 69 6C 73 1C 50 72 65 49 6E   // ...Details.PreIn
                                                                                                 69 74 69 61 6C 69 7A 65 64 46 69 65 6C 64 5F 44   // itializedField_D
                                                                                                 61 74 61 42 6C 6F 62 00 00 )                      // ataBlob..
  .field private static class [System.Private.CoreLib]Internal.Runtime.CompilerServices.FixupRuntimeTypeHandle[] PreInitializedTypeField
  .custom instance void [System.Private.CoreLib]System.Runtime.CompilerServices.PreInitializedAttribute::.ctor() = ( 01 00 00 00 ) 
  .custom instance void [System.Private.CoreLib]System.Runtime.CompilerServices.InitDataBlobAttribute::.ctor(class [mscorlib]System.Type,
                                                                                     string) = ( 01 00 07 44 65 74 61 69 6C 73 20 50 72 65 49 6E   // ...Details PreIn
                                                                                                 69 74 69 61 6C 69 7A 65 64 54 79 70 65 46 69 65   // itializedTypeFie
                                                                                                 6C 64 5F 44 61 74 61 42 6C 6F 62 00 00 )          // ld_DataBlob..
  .field private static string StaticStringFieldAfter

  .method private hidebysig specialname rtspecialname static 
          void  .cctor() cil managed
  {
    // Code size       21 (0x15)
    .maxstack  8
    IL_0000:  ldstr      "BEFORE"
    IL_0005:  stsfld     string PreInitData::StaticStringFieldBefore
    IL_000a:  ldstr      "AFTER"
    IL_000f:  stsfld     string PreInitData::StaticStringFieldAfter
    IL_0014:  ret
  } // end of method PreInitData::.cctor
} // end of class PreInitData

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
// Data blob
// =============================================================

.data cil Data_IntArray = bytearray ( 01 00 00 00 02 00 00 00 03 00 00 00 04 00 00 00) 
.data cil Data_TypeArray = bytearray ( 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00 00)

// =============================================================
// Test code (disassembled)
// =============================================================

.class public auto ansi beforefieldinit PreInitDataTest
       extends [mscorlib]System.Object
{
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
    // Code size       271 (0x10f)
    .maxstack  3
    .locals init (int32 V_0,
             bool V_1,
             bool V_2,
             bool V_3,
             bool V_4,
             valuetype [mscorlib]System.RuntimeTypeHandle V_5,
             bool V_6,
             bool V_7,
             bool V_8)
    IL_0000:  nop
    IL_0001:  ldstr      "Testing preinitialized array..."
    IL_0006:  call       void [mscorlib]System.Console::WriteLine(string)
    IL_000b:  nop
    IL_000c:  ldc.i4.0
    IL_000d:  stloc.0
    IL_000e:  br.s       IL_0030

    IL_0010:  nop
    IL_0011:  ldsfld     int32[] PreInitData::PreInitializedIntField
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
    IL_0022:  brfalse.s  IL_002b

    IL_0024:  ldc.i4.0
    IL_0025:  stloc.2
    IL_0026:  br         IL_010d

    IL_002b:  nop
    IL_002c:  ldloc.0
    IL_002d:  ldc.i4.1
    IL_002e:  add
    IL_002f:  stloc.0
    IL_0030:  ldloc.0
    IL_0031:  ldsfld     int32[] PreInitData::PreInitializedIntField
    IL_0036:  ldlen
    IL_0037:  conv.i4
    IL_0038:  clt
    IL_003a:  stloc.3
    IL_003b:  ldloc.3
    IL_003c:  brtrue.s   IL_0010

    IL_003e:  ldsfld     valuetype [System.Private.CoreLib]Internal.Runtime.CompilerServices.FixupRuntimeTypeHandle[] PreInitData::PreInitializedTypeField
    IL_0043:  ldc.i4.0
    IL_0044:  ldelema    [System.Private.CoreLib]Internal.Runtime.CompilerServices.FixupRuntimeTypeHandle
    IL_0049:  call       instance valuetype [mscorlib]System.RuntimeTypeHandle [System.Private.CoreLib]Internal.Runtime.CompilerServices.FixupRuntimeTypeHandle::get_RuntimeTypeHandle()
    IL_004e:  stloc.s    V_5
    IL_0050:  ldloca.s   V_5
    IL_0052:  ldtoken    [mscorlib]System.Int32
    IL_0057:  call       class [mscorlib]System.Type [mscorlib]System.Type::GetTypeFromHandle(valuetype [mscorlib]System.RuntimeTypeHandle)
    IL_005c:  callvirt   instance valuetype [mscorlib]System.RuntimeTypeHandle [mscorlib]System.Type::get_TypeHandle()
    IL_0061:  call       instance bool [mscorlib]System.RuntimeTypeHandle::Equals(valuetype [mscorlib]System.RuntimeTypeHandle)
    IL_0066:  stloc.s    V_4
    IL_0068:  ldloc.s    V_4
    IL_006a:  brtrue.s  IL_0073

    IL_006c:  ldc.i4.0
    IL_006d:  stloc.2
    IL_006e:  br         IL_010d

    IL_0073:  ldsfld     valuetype [System.Private.CoreLib]Internal.Runtime.CompilerServices.FixupRuntimeTypeHandle[] PreInitData::PreInitializedTypeField
    IL_0078:  ldc.i4.1
    IL_0079:  ldelema    [System.Private.CoreLib]Internal.Runtime.CompilerServices.FixupRuntimeTypeHandle
    IL_007e:  call       instance valuetype [mscorlib]System.RuntimeTypeHandle [System.Private.CoreLib]Internal.Runtime.CompilerServices.FixupRuntimeTypeHandle::get_RuntimeTypeHandle()
    IL_0083:  stloc.s    V_5
    IL_0085:  ldloca.s   V_5
    IL_0087:  ldtoken    [mscorlib]System.Int16
    IL_008c:  call       class [mscorlib]System.Type [mscorlib]System.Type::GetTypeFromHandle(valuetype [mscorlib]System.RuntimeTypeHandle)
    IL_0091:  callvirt   instance valuetype [mscorlib]System.RuntimeTypeHandle [mscorlib]System.Type::get_TypeHandle()
    IL_0096:  call       instance bool [mscorlib]System.RuntimeTypeHandle::Equals(valuetype [mscorlib]System.RuntimeTypeHandle)
    IL_009b:  stloc.s    V_6
    IL_009d:  ldloc.s    V_6
    IL_009f:  brtrue.s  IL_00a5

    IL_00a1:  ldc.i4.0
    IL_00a2:  stloc.2
    IL_00a3:  br.s       IL_010d

    IL_00a5:  ldsfld     valuetype [System.Private.CoreLib]Internal.Runtime.CompilerServices.FixupRuntimeTypeHandle[] PreInitData::PreInitializedTypeField
    IL_00aa:  ldc.i4.2
    IL_00ab:  ldelema    [System.Private.CoreLib]Internal.Runtime.CompilerServices.FixupRuntimeTypeHandle
    IL_00b0:  call       instance valuetype [mscorlib]System.RuntimeTypeHandle [System.Private.CoreLib]Internal.Runtime.CompilerServices.FixupRuntimeTypeHandle::get_RuntimeTypeHandle()
    IL_00b5:  stloc.s    V_5
    IL_00b7:  ldloca.s   V_5
    IL_00b9:  ldtoken    [mscorlib]System.Int64
    IL_00be:  call       class [mscorlib]System.Type [mscorlib]System.Type::GetTypeFromHandle(valuetype [mscorlib]System.RuntimeTypeHandle)
    IL_00c3:  callvirt   instance valuetype [mscorlib]System.RuntimeTypeHandle [mscorlib]System.Type::get_TypeHandle()
    IL_00c8:  call       instance bool [mscorlib]System.RuntimeTypeHandle::Equals(valuetype [mscorlib]System.RuntimeTypeHandle)
    IL_00cd:  stloc.s    V_7
    IL_00cf:  ldloc.s    V_7
    IL_00d1:  brtrue.s  IL_00d7

    IL_00d3:  ldc.i4.0
    IL_00d4:  stloc.2
    IL_00d5:  br.s       IL_010d

    IL_00d7:  ldsfld     valuetype [System.Private.CoreLib]Internal.Runtime.CompilerServices.FixupRuntimeTypeHandle[] PreInitData::PreInitializedTypeField
    IL_00dc:  ldc.i4.3
    IL_00dd:  ldelema    [System.Private.CoreLib]Internal.Runtime.CompilerServices.FixupRuntimeTypeHandle
    IL_00e2:  call       instance valuetype [mscorlib]System.RuntimeTypeHandle [System.Private.CoreLib]Internal.Runtime.CompilerServices.FixupRuntimeTypeHandle::get_RuntimeTypeHandle()
    IL_00e7:  stloc.s    V_5
    IL_00e9:  ldloca.s   V_5
    IL_00eb:  ldtoken    [mscorlib]System.String
    IL_00f0:  call       class [mscorlib]System.Type [mscorlib]System.Type::GetTypeFromHandle(valuetype [mscorlib]System.RuntimeTypeHandle)
    IL_00f5:  callvirt   instance valuetype [mscorlib]System.RuntimeTypeHandle [mscorlib]System.Type::get_TypeHandle()
    IL_00fa:  call       instance bool [mscorlib]System.RuntimeTypeHandle::Equals(valuetype [mscorlib]System.RuntimeTypeHandle)
    IL_00ff:  stloc.s    V_8
    IL_0101:  ldloc.s    V_8
    IL_0103:  brtrue.s  IL_0109

    IL_0105:  ldc.i4.0
    IL_0106:  stloc.2
    IL_0107:  br.s       IL_010d

    IL_0109:  ldc.i4.1
    IL_010a:  stloc.2
    IL_010b:  br.s       IL_010d

    IL_010d:  ldloc.2
    IL_010e:  ret
  } // end of method PreInitDataTest::TestPreInitData

  .method private hidebysig static bool  TestOtherStatics() cil managed
  {
    // Code size       64 (0x40)
    .maxstack  2
    .locals init (bool V_0,
             bool V_1,
             bool V_2)
    IL_0000:  nop
    IL_0001:  ldstr      "Testing other statics work well with preinitialize"
    + "d data in the same type..."
    IL_0006:  call       void [mscorlib]System.Console::WriteLine(string)
    IL_000b:  nop
    IL_000c:  ldsfld     string PreInitData::StaticStringFieldBefore
    IL_0011:  ldstr      "BEFORE"
    IL_0016:  call       bool [mscorlib]System.String::op_Inequality(string,
                                                                     string)
    IL_001b:  stloc.0
    IL_001c:  ldloc.0
    IL_001d:  brfalse.s  IL_0023

    IL_001f:  ldc.i4.0
    IL_0020:  stloc.1
    IL_0021:  br.s       IL_003e

    IL_0023:  ldsfld     string PreInitData::StaticStringFieldAfter
    IL_0028:  ldstr      "AFTER"
    IL_002d:  call       bool [mscorlib]System.String::op_Inequality(string,
                                                                     string)
    IL_0032:  stloc.2
    IL_0033:  ldloc.2
    IL_0034:  brfalse.s  IL_003a

    IL_0036:  ldc.i4.0
    IL_0037:  stloc.1
    IL_0038:  br.s       IL_003e

    IL_003a:  ldc.i4.1
    IL_003b:  stloc.1
    IL_003c:  br.s       IL_003e

    IL_003e:  ldloc.1
    IL_003f:  ret
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

} // end of class PreInitDataTest

