// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Globalization;
using System.Reflection;
using System.Runtime.CompilerServices;

public class MultiModuleLibrary
{
    // Do not reference these three (3) statics in this assembly.
    // We're testing that statics in library code are rooted for use by consuming application code.
    public static int ReturnValue;
    public static string StaticString;
    [ThreadStatic]
    public static int ThreadStaticInt;
}
