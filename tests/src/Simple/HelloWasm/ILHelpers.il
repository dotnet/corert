// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

.assembly extern mscorlib
{
  .publickeytoken = (B7 7A 5C 56 19 34 E0 89 )
  .ver 4:0:0:0
}

.assembly extern System.Private.CoreLib
{
  .publickeytoken = (B0 3F 5F 7F 11 D5 0A 3A )
  .ver 4:0:0:0
}

.assembly ILHelpers { }

.class public ILHelpers.ILHelpersTest
{
  // RVA static field
  .field public static int32 StaticInitedInt at D_00004000
  .data D_00004000 = bytearray (12 34 56 78)

  .method public static void EatArgs(string, object)
  {
    ret
  }

  .method public static void EatArg(object)
  {
    ret
  }
  //this is copied from the test case for https://github.com/dotnet/runtime/issues/9210
  //its been adapted to remove the requirement for Console.WriteLine because thats not supported in WASM
  //it can be removed once we have support for Console.WriteLine and can run the CoreCLR tests
  .method public static int32 InlineAssignByte()
  {
    .locals init (
      int8 local,
      object b
    )
    ldstr "InlineAssignByte"
    ldc.i4 300
    dup
    stloc.0
    box int32
    stloc.1
    ldloc.1
    call void ILHelpers.ILHelpersTest::EatArgs(string, object)
    ldstr "InlineAssignByte"
    ldloc.0
    box int32
    call void ILHelpers.ILHelpersTest::EatArgs(string, object)
    ldloc.1
    //after the unboxing and truncation on the way in ensure that we can subtract 200 and end up with the expected 100 return value
    unbox.any int32
    ldc.i4 200
    sub
    ret
  }
  .method public static int32 DupTest(int32&)
  {
    .locals init (
      int32 local,
      int32 local2
    )
    ldarg.0
    //push a bunch of entries onto the stack using dup, we will consume them through the rest of this method
    dup
    dup
    dup
    dup
    dup
    //first off lets ensure the passed parameter was pointing to an int of less than 10 (it should be 9)
    ldind.i4
    ldc.i4 10
    clt
    brtrue target
    //this is just trying to mess with the stack accross basic block boundries
    //it should actually leave the evaluation stack unchanged in the end
    pop
    dup
    //make sure we can deref one of our duplicate int refs and box it without any trouble
    target: ldind.i4
    box int32
    call void ILHelpers.ILHelpersTest::EatArg(object)
    //deref one of our duplicates and add 1 to it, this should not write back to the parameter it should only be stored in the local
    ldind.i4 
    ldc.i4 1
    add
    stloc.0
    //load one of our duplicates and add 200 to it, we're eventually going to return this value and test for it in the caller
    ldind.i4
    ldc.i4 200
    add
    //this should write back into the parameter
    stind.i4
    ldloc.0
    dup
    add
    dup
    stloc.1
    ldloc.0
    ceq
    //if the added value equals the source value we've got something writing to its original StackEntry instead of a dup
    brtrue failure
    ldind.i4
    ret
    failure:
    pop
    ldc.i4 0
    ret
  }

  .method public static bool BneSbyteExtend () cil managed 
  {
    // Method begins at RVA 0x2050
    // Code size 8 (0x8)
    .maxstack 8

    .locals init (
        [0] int8
    )

    ldc.i4.m1
    stloc.0
    ldloc.0
    ldc.i4.m1
    bne.un.s failure

    ldc.i4.1
    ret

    failure:
    ldc.i4.0
    ret
  }
}
