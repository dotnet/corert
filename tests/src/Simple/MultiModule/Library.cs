// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Globalization;
using System.Reflection;
using System.Runtime.CompilerServices;

public class MultiModuleLibrary
{
    public static int ReturnValue = 50;
    public static string StaticString;
    [ThreadStatic]
    public static int ThreadStaticInt = 50;
    
}
