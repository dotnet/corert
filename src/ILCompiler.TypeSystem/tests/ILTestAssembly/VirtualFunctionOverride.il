// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

.class public auto ansi beforefieldinit VirtualFunctionOverride.ExplicitOverride
       extends [CoreTestAssembly]System.Object
{
  .method public virtual int32 MyGetHashCode()
  {
    .override [CoreTestAssembly]System.Object::GetHashCode

    ldc.i4 42
    ret
  }
}
