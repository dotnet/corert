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
          ValidSimpleAdd(int32 a,
              int32 b) cil managed
  {
     .maxstack  2 
     ldarg.1 
     ldarg.2 
     add 
     ret
  } // end of method Class1::Add

  .method public hidebysig instance void  InvalidSimpleAdd() cil managed
  {
    // Code size       16 (0x10)
    .maxstack  2
    .locals init ([0] int32 b,
             [1] string str,
             [2] int32 c)  
      ldc.i4.3
      stloc.0
      ldstr      "sfdsf"
      stloc.1
      ldloc.0
      ldloc.1
      add
      stloc.3
      ret
  } // end of method Program::Add

} // end of class ClassLibrary1.Class1
