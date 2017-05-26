// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

.assembly extern CoreTestAssembly
{
}

.assembly BasicArithmeticTests
{
}

// Mark this as core library so that ILASM doesn't take the code paths that
// 'fix' all the references to e.g. System.Object to be mscorlib.
.mscorlib


.class public auto ansi beforefieldinit BasicArithmeticTestsType
       extends [CoreTestAssembly]System.Object
{
  .method public hidebysig instance int32 
          SimpleAdd(int32 a,
              int32 b) cil managed
  {
    // Code size       9 (0x9)
    .maxstack  2
    .locals init (int32 V_0)
    IL_0000:  nop
    IL_0001:  ldarg.1
    IL_0002:  ldarg.2
    IL_0003:  add
    IL_0004:  stloc.0
    IL_0005:  br.s       IL_0007

    IL_0007:  ldloc.0
    IL_0008:  ret
  } // end of method Class1::Add
} // end of class ClassLibrary1.Class1