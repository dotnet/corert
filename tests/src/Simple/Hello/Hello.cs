// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;

// Name of namespace matches the name of the assembly on purpose to
// ensure that we can handle this (mostly an issue for C++ code generation).
namespace Hello
{
    internal class Program
    {
        private static void Main(string[] args)
        {
            Console.WriteLine("Hello world");
        }
    }
}
