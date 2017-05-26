// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Reflection;
using System.Collections.Generic;
using System.Runtime.CompilerServices;

internal class Program
{
    public struct Struct
    {
        public int X;

        [MethodImpl(MethodImplOptions.NoInlining)]
        public void SetX(int x)
        {
            X = x;
        }
    }

    public delegate void StructSetter(ref Struct s, int value);

    private static void Main(string[] args)
    {
        if (String.Empty.Length > 0)
        {
            Struct st = default(Struct);
            st.SetX(123);
        }

        var mi = typeof(Struct).GetTypeInfo().GetMethod("SetX");
        Console.WriteLine(mi);

        Struct s = default(Struct);
        var f = (StructSetter)mi.CreateDelegate(typeof(StructSetter));
        f(ref s, 1234);
        Console.WriteLine(s.X);

        if (String.Empty.Length > 0)
        {
            // Make sure we generate metadata for the Invoke method
            StructSetter s2 = f.Invoke;
            s2(ref s, 555);
        }
    }
}
