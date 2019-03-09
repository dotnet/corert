// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Reflection;

class MyGenericUnusedClass<T> { }

internal class Program
{
    public static void UnusedMethod()
    {
    }

    public static void UnusedGenericMethod<T>()
    {
    }

    private static void Main(string[] args)
    {
        // Search through a forwarder
        {
            Type t = Type.GetType("System.Collections.Generic.List`1, System.Collections", throwOnError: false);
            if (t == null)
                throw new Exception("List");
        }

        // Search in mscorlib
        {
            Type t = Type.GetType("System.Runtime.CompilerServices.SuppressIldasmAttribute", throwOnError: false);
            if (t == null)
                throw new Exception("SuppressIldasmAttribute");
        }

        // Generics
        {
            Type t = Type.GetType("MyGenericUnusedClass`1", throwOnError: false);
            if (t == null)
                throw new Exception("MyGenericUnusedClass");

            // TODO: complete type metadata
            t.MakeGenericType(typeof(object));
        }

        // GetMethod on a non-generic type
        {
            MethodInfo mi = typeof(Program).GetMethod(nameof(UnusedMethod));
            if (mi == null)
                throw new Exception(nameof(UnusedMethod));
            mi.Invoke(null, Array.Empty<object>());
        }

        // GetMethod on a non-generic type
        {
            MethodInfo mi = Type.GetType(nameof(Program)).GetMethod(nameof(UnusedGenericMethod));
            if (mi == null)
                throw new Exception(nameof(UnusedGenericMethod));
            mi.MakeGenericMethod(typeof(object)).Invoke(null, Array.Empty<object>());
        }
    }
}
