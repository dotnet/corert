// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Runtime.InteropServices;

namespace NativeLibrary
{
    public class Class1
    {
        [NativeCallable(EntryPoint = "add", CallingConvention = CallingConvention.StdCall)]
        public static int Add(int a, int b)
        {
            return a + b;
        }
    }
}
