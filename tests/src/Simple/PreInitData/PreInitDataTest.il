// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information. 

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
// Intrinsics
// =============================================================

.class private auto ansi beforefieldinit System.Runtime.InteropServices.McgIntrinsicsAttribute
       extends [mscorlib]System.Attribute
{
  .custom instance void [mscorlib]System.AttributeUsageAttribute::.ctor(valuetype [mscorlib]System.AttributeTargets) = ( 01 00 44 00 00 00 00 00 )                         // ..D.....
  .method public hidebysig specialname rtspecialname 
          instance void  .ctor() cil managed
  {
    // Code size       8 (0x8)
    .maxstack  8
    IL_0000:  ldarg.0
    IL_0001:  call       instance void [mscorlib]System.Attribute::.ctor()
    IL_0006:  nop
    IL_0007:  ret
  } // end of method McgIntrinsicsAttribute::.ctor

} // end of class System.Runtime.InteropServices.McgIntrinsicsAttribute

.class private abstract auto ansi sealed beforefieldinit System.Runtime.InteropServices.AddrofIntrinsics
       extends [mscorlib]System.Object
{
  .custom instance void System.Runtime.InteropServices.McgIntrinsicsAttribute::.ctor() = ( 01 00 00 00 ) 
  .method assembly hidebysig static native int 
          AddrOf<T>(!!T ftn) cil managed
  {
    // Code size       7 (0x7)
    .maxstack  8
    IL_0000:  nop
    IL_0001:  newobj     instance void [mscorlib]System.PlatformNotSupportedException::.ctor()
    IL_0006:  throw
  } // end of method AddrofIntrinsics::AddrOf

} // end of class System.Runtime.InteropServices.AddrofIntrinsics

// =============================================================
// Test code
// =============================================================

.class public auto ansi beforefieldinit PreInitDataTest
       extends [mscorlib]System.Object
{
.class auto ansi sealed nested public Func1Proc
         extends [mscorlib]System.MulticastDelegate
  {
    .method public hidebysig specialname rtspecialname 
            instance void  .ctor(object 'object',
                                 native int 'method') runtime managed
    {
    } // end of method Func1Proc::.ctor

    .method public hidebysig newslot virtual 
            instance void  Invoke(int32 a) runtime managed
    {
    } // end of method Func1Proc::Invoke

    .method public hidebysig newslot virtual 
            instance class [mscorlib]System.IAsyncResult 
            BeginInvoke(int32 a,
                        class [mscorlib]System.AsyncCallback callback,
                        object 'object') runtime managed
    {
    } // end of method Func1Proc::BeginInvoke

    .method public hidebysig newslot virtual 
            instance void  EndInvoke(class [mscorlib]System.IAsyncResult result) runtime managed
    {
    } // end of method Func1Proc::EndInvoke

  } // end of class Func1Proc

  .class auto ansi sealed nested public Func2Proc
         extends [mscorlib]System.MulticastDelegate
  {
    .method public hidebysig specialname rtspecialname 
            instance void  .ctor(object 'object',
                                 native int 'method') runtime managed
    {
    } // end of method Func2Proc::.ctor

    .method public hidebysig newslot virtual 
            instance void  Invoke(float32 a) runtime managed
    {
    } // end of method Func2Proc::Invoke

    .method public hidebysig newslot virtual 
            instance class [mscorlib]System.IAsyncResult 
            BeginInvoke(float32 a,
                        class [mscorlib]System.AsyncCallback callback,
                        object 'object') runtime managed
    {
    } // end of method Func2Proc::BeginInvoke

    .method public hidebysig newslot virtual 
            instance void  EndInvoke(class [mscorlib]System.IAsyncResult result) runtime managed
    {
    } // end of method Func2Proc::EndInvoke

  } // end of class Func2Proc 

  .field private static literal int32 Pass = int32(0x00000064)
  .field private static literal int32 Fail = int32(0xFFFFFFFF)
  .method public hidebysig static int32  Main(string[] args) cil managed
  {
    .entrypoint
    // Code size       151 (0x97)
    .maxstack  2
    .locals init (int32 V_0,
             bool V_1,
             bool V_2,
             bool V_3,
             bool V_4,
             bool V_5,
             int32 V_6)
    IL_0000:  nop
    IL_0001:  ldc.i4.s   100
    IL_0003:  stloc.0
    IL_0004:  call       bool PreInitDataTest::TestPreInitPrimitiveData()
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
    IL_001f:  call       bool PreInitDataTest::TestPreInitIntData()
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
    IL_003a:  call       bool PreInitDataTest::TestPreInitTypeData()
    IL_003f:  ldc.i4.0
    IL_0040:  ceq
    IL_0042:  stloc.3
    IL_0043:  ldloc.3
    IL_0044:  brfalse.s  IL_0055

    IL_0046:  nop
    IL_0047:  ldstr      "Failed"
    IL_004c:  call       void [mscorlib]System.Console::WriteLine(string)
    IL_0051:  nop
    IL_0052:  ldc.i4.m1
    IL_0053:  stloc.0
    IL_0054:  nop
    IL_0055:  call       bool PreInitDataTest::TestPreInitMethodData()
    IL_005a:  ldc.i4.0
    IL_005b:  ceq
    IL_005d:  stloc.s    V_4
    IL_005f:  ldloc.s    V_4
    IL_0061:  brfalse.s  IL_0072

    IL_0063:  nop
    IL_0064:  ldstr      "Failed"
    IL_0069:  call       void [mscorlib]System.Console::WriteLine(string)
    IL_006e:  nop
    IL_006f:  ldc.i4.m1
    IL_0070:  stloc.0
    IL_0071:  nop
    IL_0072:  call       bool PreInitDataTest::TestOtherStatics()
    IL_0077:  ldc.i4.0
    IL_0078:  ceq
    IL_007a:  stloc.s    V_5
    IL_007c:  ldloc.s    V_5
    IL_007e:  brfalse.s  IL_008f

    IL_0080:  nop
    IL_0081:  ldstr      "Failed"
    IL_0086:  call       void [mscorlib]System.Console::WriteLine(string)
    IL_008b:  nop
    IL_008c:  ldc.i4.m1
    IL_008d:  stloc.0
    IL_008e:  nop
    IL_008f:  ldloc.0
    IL_0090:  stloc.s    V_6
    IL_0092:  br.s       IL_0094

    IL_0094:  ldloc.s    V_6
    IL_0096:  ret
  } // end of method PreInitDataTest::Main

  .method private hidebysig static bool  TestPreInitPrimitiveData() cil managed
  {
    // Code size       41 (0x29)
    .maxstack  2
    .locals init (bool V_0,
             bool V_1)
    IL_0000:  nop
    IL_0001:  ldstr      "Testing preinitialized primitive data..."
    IL_0006:  call       void [mscorlib]System.Console::WriteLine(string)
    IL_000b:  nop
    IL_000c:  ldsfld     int32 PreInitData::PreInitializedInt32Field
    IL_0011:  ldc.i4     0x12345678
    IL_0016:  ceq
    IL_0018:  ldc.i4.0
    IL_0019:  ceq
    IL_001b:  stloc.0
    IL_001c:  ldloc.0
    IL_001d:  brfalse.s  IL_0023

    IL_001f:  ldc.i4.0
    IL_0020:  stloc.1
    IL_0021:  br.s       IL_0027

    IL_0023:  ldc.i4.1
    IL_0024:  stloc.1
    IL_0025:  br.s       IL_0027

    IL_0027:  ldloc.1
    IL_0028:  ret
  } // end of method PreInitDataTest::TestPreInitPrimitiveData  

  .method private hidebysig static bool  TestPreInitIntData() cil managed
  {
    // Code size       65 (0x41)
    .maxstack  3
    .locals init (int32 V_0,
             bool V_1,
             bool V_2,
             bool V_3)
    IL_0000:  nop
    IL_0001:  ldstr      "Testing preinitialized int array..."
    IL_0006:  call       void [mscorlib]System.Console::WriteLine(string)
    IL_000b:  nop
    IL_000c:  ldc.i4.0
    IL_000d:  stloc.0
    IL_000e:  br.s       IL_002d

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
    IL_002e:  ldsfld     int32[] PreInitData::PreInitializedIntField
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
  } // end of method PreInitDataTest::TestPreInitIntData

.method private hidebysig static bool  TestPreInitTypeData() cil managed
{
  // Code size       191 (0xbf)
  .maxstack  2
  .locals init (bool V_0,
           bool V_1,
           bool V_2,
           bool V_3,
           bool V_4)
  IL_0000:  nop
  IL_0001:  ldstr      "Testing preinitialized type array..."
  IL_0006:  call       void [mscorlib]System.Console::WriteLine(string)
  IL_000b:  nop
  IL_000c:  ldsfld     valuetype [mscorlib]System.RuntimeTypeHandle[] PreInitData::PreInitializedTypeField
  IL_0011:  ldc.i4.0
  IL_0012:  ldelema    [mscorlib]System.RuntimeTypeHandle
  IL_0017:  ldtoken    [mscorlib]System.Int32
  IL_001c:  call       class [mscorlib]System.Type [mscorlib]System.Type::GetTypeFromHandle(valuetype [mscorlib]System.RuntimeTypeHandle)
  IL_0021:  callvirt   instance valuetype [mscorlib]System.RuntimeTypeHandle [mscorlib]System.Type::get_TypeHandle()
  IL_0026:  call       instance bool [mscorlib]System.RuntimeTypeHandle::Equals(valuetype [mscorlib]System.RuntimeTypeHandle)
  IL_002b:  ldc.i4.0
  IL_002c:  ceq
  IL_002e:  stloc.0
  IL_002f:  ldloc.0
  IL_0030:  brfalse.s  IL_0039
  IL_0032:  ldc.i4.0
  IL_0033:  stloc.1
  IL_0034:  br         IL_00bd
  IL_0039:  ldsfld     valuetype [mscorlib]System.RuntimeTypeHandle[] PreInitData::PreInitializedTypeField
  IL_003e:  ldc.i4.1
  IL_003f:  ldelema    [mscorlib]System.RuntimeTypeHandle
  IL_0044:  ldtoken    [mscorlib]System.Int16
  IL_0049:  call       class [mscorlib]System.Type [mscorlib]System.Type::GetTypeFromHandle(valuetype [mscorlib]System.RuntimeTypeHandle)
  IL_004e:  callvirt   instance valuetype [mscorlib]System.RuntimeTypeHandle [mscorlib]System.Type::get_TypeHandle()
  IL_0053:  call       instance bool [mscorlib]System.RuntimeTypeHandle::Equals(valuetype [mscorlib]System.RuntimeTypeHandle)
  IL_0058:  ldc.i4.0
  IL_0059:  ceq
  IL_005b:  stloc.2
  IL_005c:  ldloc.2
  IL_005d:  brfalse.s  IL_0063
  IL_005f:  ldc.i4.0
  IL_0060:  stloc.1
  IL_0061:  br.s       IL_00bd
  IL_0063:  ldsfld     valuetype [mscorlib]System.RuntimeTypeHandle[] PreInitData::PreInitializedTypeField
  IL_0068:  ldc.i4.2
  IL_0069:  ldelema    [mscorlib]System.RuntimeTypeHandle
  IL_006e:  ldtoken    [mscorlib]System.Int64
  IL_0073:  call       class [mscorlib]System.Type [mscorlib]System.Type::GetTypeFromHandle(valuetype [mscorlib]System.RuntimeTypeHandle)
  IL_0078:  callvirt   instance valuetype [mscorlib]System.RuntimeTypeHandle [mscorlib]System.Type::get_TypeHandle()
  IL_007d:  call       instance bool [mscorlib]System.RuntimeTypeHandle::Equals(valuetype [mscorlib]System.RuntimeTypeHandle)
  IL_0082:  ldc.i4.0
  IL_0083:  ceq
  IL_0085:  stloc.3
  IL_0086:  ldloc.3
  IL_0087:  brfalse.s  IL_008d
  IL_0089:  ldc.i4.0
  IL_008a:  stloc.1
  IL_008b:  br.s       IL_00bd
  IL_008d:  ldsfld     valuetype [mscorlib]System.RuntimeTypeHandle[] PreInitData::PreInitializedTypeField
  IL_0092:  ldc.i4.3
  IL_0093:  ldelema    [mscorlib]System.RuntimeTypeHandle
  IL_0098:  ldtoken    [mscorlib]System.String
  IL_009d:  call       class [mscorlib]System.Type [mscorlib]System.Type::GetTypeFromHandle(valuetype [mscorlib]System.RuntimeTypeHandle)
  IL_00a2:  callvirt   instance valuetype [mscorlib]System.RuntimeTypeHandle [mscorlib]System.Type::get_TypeHandle()
  IL_00a7:  call       instance bool [mscorlib]System.RuntimeTypeHandle::Equals(valuetype [mscorlib]System.RuntimeTypeHandle)
  IL_00ac:  ldc.i4.0
  IL_00ad:  ceq
  IL_00af:  stloc.s    V_4
  IL_00b1:  ldloc.s    V_4
  IL_00b3:  brfalse.s  IL_00b9
  IL_00b5:  ldc.i4.0
  IL_00b6:  stloc.1
  IL_00b7:  br.s       IL_00bd
  IL_00b9:  ldc.i4.1
  IL_00ba:  stloc.1
  IL_00bb:  br.s       IL_00bd
  IL_00bd:  ldloc.1
  IL_00be:  ret
} // end of method PreInitDataTest::TestPreInitTypeData


  .method private hidebysig static bool  TestPreInitMethodData() cil managed
  {
    // Code size       92 (0x5c)
    .maxstack  3
    .locals init (bool V_0,
             bool V_1,
             bool V_2)
    IL_0000:  nop
    IL_0001:  ldstr      "Testing preinitialized method array..."
    IL_0006:  call       void [mscorlib]System.Console::WriteLine(string)
    IL_000b:  nop
    IL_000c:  ldsfld     native int[] PreInitData::PreInitializedMethodField
    IL_0011:  ldc.i4.0
    IL_0012:  ldelem.i
    IL_0013:  ldnull
    IL_0014:  ldftn      void NativeMethods::Func1(int32)
    IL_001a:  newobj     instance void PreInitDataTest/Func1Proc::.ctor(object,
                                                                        native int)
    IL_001f:  call       native int System.Runtime.InteropServices.AddrofIntrinsics::AddrOf<class PreInitDataTest/Func1Proc>(!!0)
    IL_0024:  call       bool [mscorlib]System.IntPtr::op_Inequality(native int,
                                                                     native int)
    IL_0029:  stloc.0
    IL_002a:  ldloc.0
    IL_002b:  brfalse.s  IL_0031

    IL_002d:  ldc.i4.0
    IL_002e:  stloc.1
    IL_002f:  br.s       IL_005a

    IL_0031:  ldsfld     native int[] PreInitData::PreInitializedMethodField
    IL_0036:  ldc.i4.1
    IL_0037:  ldelem.i
    IL_0038:  ldnull
    IL_0039:  ldftn      void NativeMethods::Func2(float32)
    IL_003f:  newobj     instance void PreInitDataTest/Func2Proc::.ctor(object,
                                                                        native int)
    IL_0044:  call       native int System.Runtime.InteropServices.AddrofIntrinsics::AddrOf<class PreInitDataTest/Func2Proc>(!!0)
    IL_0049:  call       bool [mscorlib]System.IntPtr::op_Inequality(native int,
                                                                     native int)
    IL_004e:  stloc.2
    IL_004f:  ldloc.2
    IL_0050:  brfalse.s  IL_0056

    IL_0052:  ldc.i4.0
    IL_0053:  stloc.1
    IL_0054:  br.s       IL_005a

    IL_0056:  ldc.i4.1
    IL_0057:  stloc.1
    IL_0058:  br.s       IL_005a

    IL_005a:  ldloc.1
    IL_005b:  ret
  } // end of method PreInitDataTest::TestPreInitMethodData
                                                           

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
                              
