// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

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
.method public static void EatArgs(string, object)
{
ret
}

.method public static void EatArg(object)
{
ret
}

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
    unbox.any int32
    ldc.i4 200
    sub
    ret
  }
 .method public static int32 DupTest(int32&)
 {
	ldarg.0
	dup
	dup
	dup
	ldind.i4
	box int32
	call void ILHelpers.ILHelpersTest::EatArg(object)
	ldind.i4 
	ldc.i4 200
	add
	stind.i4
	ldind.i4
	ret
 }
}