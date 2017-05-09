
//  Microsoft (R) .NET Framework IL Disassembler.  Version 4.6.1055.0
//  Copyright (c) Microsoft Corporation.  All rights reserved.



// Metadata version: v4.0.30319
.assembly extern System.Runtime
{
  .publickeytoken = (B0 3F 5F 7F 11 D5 0A 3A )                         // .?_....:
  .ver 4:1:0:0
}
.assembly extern System.Console
{
  .publickeytoken = (B0 3F 5F 7F 11 D5 0A 3A )                         // .?_....:
  .ver 4:0:0:0
}
.assembly repro
{
  .custom instance void [System.Runtime]System.Runtime.CompilerServices.CompilationRelaxationsAttribute::.ctor(int32) = ( 01 00 08 00 00 00 00 00 ) 
  .custom instance void [System.Runtime]System.Runtime.CompilerServices.RuntimeCompatibilityAttribute::.ctor() = ( 01 00 01 00 54 02 16 57 72 61 70 4E 6F 6E 45 78   // ....T..WrapNonEx
                                                                                                                   63 65 70 74 69 6F 6E 54 68 72 6F 77 73 01 )       // ceptionThrows.

  // --- The following custom attribute is added automatically, do not uncomment -------
  //  .custom instance void [System.Runtime]System.Diagnostics.DebuggableAttribute::.ctor(valuetype [System.Runtime]System.Diagnostics.DebuggableAttribute/DebuggingModes) = ( 01 00 07 01 00 00 00 00 ) 

  .custom instance void [System.Runtime]System.Reflection.AssemblyTitleAttribute::.ctor(string) = ( 01 00 05 72 65 70 72 6F 00 00 )                   // ...repro..
  .custom instance void [System.Runtime]System.Reflection.AssemblyDescriptionAttribute::.ctor(string) = ( 01 00 05 72 65 70 72 6F 00 00 )                   // ...repro..
  .custom instance void [System.Runtime]System.Reflection.AssemblyDefaultAliasAttribute::.ctor(string) = ( 01 00 05 72 65 70 72 6F 00 00 )                   // ...repro..
  .custom instance void [System.Runtime]System.Reflection.AssemblyCompanyAttribute::.ctor(string) = ( 01 00 15 4D 69 63 72 6F 73 6F 66 74 20 43 6F 72   // ...Microsoft Cor
                                                                                                      70 6F 72 61 74 69 6F 6E 00 00 )                   // poration..
  .custom instance void [System.Runtime]System.Reflection.AssemblyProductAttribute::.ctor(string) = ( 01 00 1A 4D 69 63 72 6F 73 6F 66 74 C2 AE 20 2E   // ...Microsoft.. .
                                                                                                      4E 45 54 20 46 72 61 6D 65 77 6F 72 6B 00 00 )    // NET Framework..
  .custom instance void [System.Runtime]System.Reflection.AssemblyCopyrightAttribute::.ctor(string) = ( 01 00 2F C2 A9 20 4D 69 63 72 6F 73 6F 66 74 20   // ../.. Microsoft 
                                                                                                        43 6F 72 70 6F 72 61 74 69 6F 6E 2E 20 20 41 6C   // Corporation.  Al
                                                                                                        6C 20 72 69 67 68 74 73 20 72 65 73 65 72 76 65   // l rights reserve
                                                                                                        64 2E 00 00 )                                     // d...
  .custom instance void [System.Runtime]System.Reflection.AssemblyFileVersionAttribute::.ctor(string) = ( 01 00 0B 31 2E 30 2E 32 35 33 30 33 2E 30 00 00 ) // ...1.0.25303.0..
  .custom instance void [System.Runtime]System.Reflection.AssemblyInformationalVersionAttribute::.ctor(string) = ( 01 00 59 31 2E 30 2E 32 35 33 30 33 2E 30 20 62   // ..Y1.0.25303.0 b
                                                                                                                   75 69 6C 74 20 62 79 3A 20 61 74 66 69 65 2D 4B   // uilt by: atfie-K
                                                                                                                   4F 44 4F 52 49 2E 20 43 6F 6D 6D 69 74 20 48 61   // ODORI. Commit Ha
                                                                                                                   73 68 3A 20 30 62 37 34 65 63 30 63 63 31 39 37   // sh: 0b74ec0cc197
                                                                                                                   63 31 36 34 35 63 61 65 31 35 64 30 39 65 30 37   // c1645cae15d09e07
                                                                                                                   32 61 36 39 65 32 64 39 61 39 37 62 00 00 )       // 2a69e2d9a97b..
  .custom instance void [System.Runtime]System.Reflection.AssemblyMetadataAttribute::.ctor(string,
                                                                                           string) = ( 01 00 00 00 00 00 ) 
  .publickey = (00 24 00 00 04 80 00 00 94 00 00 00 06 02 00 00   // .$..............
                00 24 00 00 52 53 41 31 00 04 00 00 01 00 01 00   // .$..RSA1........
                07 D1 FA 57 C4 AE D9 F0 A3 2E 84 AA 0F AE FD 0D   // ...W............
                E9 E8 FD 6A EC 8F 87 FB 03 76 6C 83 4C 99 92 1E   // ...j.....vl.L...
                B2 3B E7 9A D9 D5 DC C1 DD 9A D2 36 13 21 02 90   // .;.........6.!..
                0B 72 3C F9 80 95 7F C4 E1 77 10 8F C6 07 77 4F   // .r<......w....wO
                29 E8 32 0E 92 EA 05 EC E4 E8 21 C0 A5 EF E8 F1   // ).2.......!.....
                64 5C 4C 0C 93 C1 AB 99 28 5D 62 2C AA 65 2C 1D   // d\L.....(]b,.e,.
                FA D6 3D 74 5D 6F 2D E5 F1 7E 5E AF 0F C4 96 3D   // ..=t]o-..~^....=
                26 1C 8A 12 43 65 18 20 6D C0 93 34 4D 5A D2 93 ) // &...Ce. m..4MZ..
  .hash algorithm 0x00008004
  .ver 999:999:999:999
}
.module repro.exe
// MVID: {AC29AA8B-DC98-4381-A814-D7616433043A}
.imagebase 0x00400000
.file alignment 0x00000200
.stackreserve 0x00100000
.subsystem 0x0003       // WINDOWS_CUI
.corflags 0x00000009    //  ILONLY
// Image base: 0x01060000


// =============== CLASS MEMBERS DECLARATION ===================

.class private auto ansi beforefieldinit Details
       extends [System.Runtime]System.Object
{
  .class explicit ansi sealed nested private '__PreInitializedDataSize=16'
         extends [System.Private.CoreLib]System.ValueType
  {
    .pack 1
    .size 16
  } // end of class '__PreInitializedDataSize=116'

  .field private static valuetype 'Details'/'__PreInitializedDataSize=16' PreInitializedField_DataBlob at DataBlob_1
  .method public hidebysig specialname rtspecialname 
          instance void  .ctor() cil managed
  {
    // Code size       8 (0x8)
    .maxstack  8
    IL_0000:  ldarg.0
    IL_0001:  call       instance void [System.Runtime]System.Object::.ctor()
    IL_0006:  nop
    IL_0007:  ret
  } // end of method Details::.ctor

} // end of class Details

.data cil DataBlob_1 = bytearray (
    01 00 00 00
    02 00 00 00
    03 00 00 00
    04 00 00 00
)

.class private auto ansi beforefieldinit Program
       extends [System.Runtime]System.Object
{
  .field private static int32[] PreInitializedField
  .custom instance void System.Runtime.CompilerServices.PreInitializedAttribute::.ctor() = ( 01 00 00 00 ) 
  .custom instance void System.Runtime.CompilerServices.InitDataBlobAttribute::.ctor(class [System.Runtime]System.Type,
                                                                                     string) = ( 01 00 07 44 65 74 61 69 6C 73 1C 50 72 65 49 6E   // ...Details.PreIn
                                                                                                 69 74 69 61 6C 69 7A 65 64 46 69 65 6C 64 5F 44   // itializedField_D
                                                                                                 61 74 61 42 6C 6F 62 00 00 )                      // ataBlob..
  .method private hidebysig static void  Main(string[] args) cil managed
  {
    .entrypoint
    // Code size       39 (0x27)
    .maxstack  2
    .locals init ([0] int32 i,
             [1] bool V_1)
    IL_0000:  nop
    IL_0001:  ldc.i4.0
    IL_0002:  stloc.0
    IL_0003:  br.s       IL_0018

    IL_0005:  nop
    IL_0006:  ldsfld     int32[] Program::PreInitializedField
    IL_000b:  ldloc.0
    IL_000c:  ldelem.i4
    IL_000d:  call       void [System.Console]System.Console::WriteLine(int32)
    IL_0012:  nop
    IL_0013:  nop
    IL_0014:  ldloc.0
    IL_0015:  ldc.i4.1
    IL_0016:  add
    IL_0017:  stloc.0
    IL_0018:  ldloc.0
    IL_0019:  ldsfld     int32[] Program::PreInitializedField
    IL_001e:  ldlen
    IL_001f:  conv.i4
    IL_0020:  clt
    IL_0022:  stloc.1
    IL_0023:  ldloc.1
    IL_0024:  brtrue.s   IL_0005

    IL_0026:  ret
  } // end of method Program::Main

  .method public hidebysig specialname rtspecialname 
          instance void  .ctor() cil managed
  {
    // Code size       8 (0x8)
    .maxstack  8
    IL_0000:  ldarg.0
    IL_0001:  call       instance void [System.Runtime]System.Object::.ctor()
    IL_0006:  nop
    IL_0007:  ret
  } // end of method Program::.ctor

} // end of class Program

.class private auto ansi beforefieldinit System.Runtime.CompilerServices.PreInitializedAttribute
       extends [System.Runtime]System.Attribute
{
  .custom instance void [System.Runtime]System.AttributeUsageAttribute::.ctor(valuetype [System.Runtime]System.AttributeTargets) = ( 01 00 00 01 00 00 01 00 54 02 0D 41 6C 6C 6F 77   // ........T..Allow
                                                                                                                                     4D 75 6C 74 69 70 6C 65 00 )                      // Multiple.
  .method public hidebysig specialname rtspecialname 
          instance void  .ctor() cil managed
  {
    // Code size       8 (0x8)
    .maxstack  8
    IL_0000:  ldarg.0
    IL_0001:  call       instance void [System.Runtime]System.Attribute::.ctor()
    IL_0006:  nop
    IL_0007:  ret
  } // end of method PreInitializedAttribute::.ctor

} // end of class System.Runtime.CompilerServices.PreInitializedAttribute

.class private auto ansi beforefieldinit System.Runtime.CompilerServices.InitDataBlobAttribute
       extends [System.Runtime]System.Attribute
{
  .custom instance void [System.Runtime]System.AttributeUsageAttribute::.ctor(valuetype [System.Runtime]System.AttributeTargets) = ( 01 00 00 01 00 00 01 00 54 02 0D 41 6C 6C 6F 77   // ........T..Allow
                                                                                                                                     4D 75 6C 74 69 70 6C 65 00 )                      // Multiple.
  .method public hidebysig specialname rtspecialname 
          instance void  .ctor(class [System.Runtime]System.Type 'type',
                               string fieldName) cil managed
  {
    // Code size       9 (0x9)
    .maxstack  8
    IL_0000:  ldarg.0
    IL_0001:  call       instance void [System.Runtime]System.Attribute::.ctor()
    IL_0006:  nop
    IL_0007:  nop
    IL_0008:  ret
  } // end of method InitDataBlobAttribute::.ctor

} // end of class System.Runtime.CompilerServices.InitDataBlobAttribute

.class private auto ansi sealed '<PrivateImplementationDetails>'
       extends [System.Runtime]System.Object
{
  .custom instance void [System.Runtime]System.Runtime.CompilerServices.CompilerGeneratedAttribute::.ctor() = ( 01 00 00 00 ) 
  .class explicit ansi sealed nested private '__StaticArrayInitTypeSize=16'
         extends [System.Runtime]System.ValueType
  {
    .pack 1
    .size 16
  } // end of class '__StaticArrayInitTypeSize=16'

  .field static assembly initonly valuetype '<PrivateImplementationDetails>'/'__StaticArrayInitTypeSize=16' '1456763F890A84558F99AFA687C36B9037697848' at I_00002C4C
} // end of class '<PrivateImplementationDetails>'


// =============================================================

.data cil I_00002C4C = bytearray (
                 01 00 00 00 02 00 00 00 03 00 00 00 04 00 00 00) 
// *********** DISASSEMBLY COMPLETE ***********************
// WARNING: Created Win32 resource file repro.res
