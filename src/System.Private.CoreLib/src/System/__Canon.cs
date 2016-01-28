// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

// 

// 

/*============================================================
**
**
**
** Object is the class used for generating shared generics
**
** 
===========================================================*/

using System.Runtime.InteropServices;

namespace System
{
    // CONTRACT with Compiler
    // The __Canon type is one of the primitives understood by the compilers. This type
    // must never be explicitly used by code.
    [CLSCompliant(false)]
    [System.Runtime.CompilerServices.DependencyReductionRoot]
    public class __Canon
    {
    }

    // CONTRACT with Compiler
    // The __UniversalCanon type is one of the primitives understood by the compilers. This type
    // must never be explicitly used by code.
    [CLSCompliant(false)]
    public struct __UniversalCanon
    {
    }

    // CONTRACT with Compiler
    // The __CanonAlike type is one of the primitives understood by the compilers. This type
    // must never be explicitly used by code.
    // TODO: Delete this type once normal __Canon templates are supported
    [CLSCompliant(false)]
    [System.Runtime.CompilerServices.DependencyReductionRoot]
    public class __CanonAlike
    {
    }

    // CONTRACT with Compiler
    // The __Boxed type is one of the primitives understood by the compilers. This type
    // must never be explicitly used by code. It is used to simulate the shape of boxed types
    [System.Runtime.CompilerServices.DependencyReductionRoot]
    [System.Runtime.InteropServices.StructLayout(LayoutKind.Explicit)]
    internal class __Boxed<T> where T : struct
    {
        [System.Runtime.CompilerServices.DependencyReductionRoot]
        private __Boxed()
        {
            BoxedValue = default(T);
        }

        [FieldOffset(0)]
        private T BoxedValue;
    }
}
