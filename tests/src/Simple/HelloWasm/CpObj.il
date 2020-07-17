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

.assembly CpObj { }

.class public CpObj.CpObjTest
{
  .method public hidebysig static void CpObj(valuetype CpObj.TestValue& dest, valuetype CpObj.TestValue& src) cil managed
  {
    .maxstack 8
    ldarg.0
    ldarg.1
    cpobj CpObj.TestValue
    ret
  }
}

.class public value sealed CpObj.TestValue
{
  .field public int32 Field;
}
