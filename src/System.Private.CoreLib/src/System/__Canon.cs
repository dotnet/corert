// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

// 

// 

/*============================================================
**
** Class:  System.__Canon
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
    // The __Boxed type is one of the primitives understood by the compilers. This type
    // must never be explicitly used by code. It is used to simulate the shape of boxed types
    [System.Runtime.CompilerServices.DependencyReductionRoot]
    [System.Runtime.InteropServices.StructLayout(LayoutKind.Explicit)]
    internal class __Boxed<T> where T : struct
    {
        [System.Runtime.CompilerServices.DependencyReductionRoot]
        __Boxed()
        {
            BoxedValue = default(T);
        }

        [FieldOffset(0)]
        T BoxedValue;
    }
}
