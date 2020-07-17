// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

.assembly extern mscorlib
{
  .publickeytoken = (B7 7A 5C 56 19 34 E0 89 )
  .ver 4:0:0:0
}

.assembly CkFinite { }

.class public abstract sealed CkFinite.CkFiniteTest {

  .method public static bool CkFinite32(float32) {
    .maxstack 5
    try_start:
      ldarg 0
      ckfinite
      pop   //remove the value from the stack
      leave try_end
    try_end:
      ldc.i4 0x00000001
      ret
    handler_start:
      pop //remove the exception ref from the stack
      leave done
    handler_end:
    done:
      ldc.i4 0x00000000
      ret
    .try try_start to try_end catch [mscorlib]System.OverflowException handler handler_start to handler_end
  }

  .method public static bool CkFinite64(float64) {
    .maxstack 5
    try_start:
      ldarg 0
      ckfinite
      pop //remove the value from the stack
      leave try_end
    try_end:
      ldc.i4 0x00000001
      ret
    handler_start:
      pop //remove the exception ref from the stack
      leave done
    handler_end:
    done:
      ldc.i4 0x00000000
      ret
    .try try_start to try_end catch [mscorlib]System.OverflowException handler handler_start to handler_end
  }
}
