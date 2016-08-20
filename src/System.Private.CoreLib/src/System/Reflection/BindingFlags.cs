// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

namespace System.Reflection
{
    [Flags]
    public enum BindingFlags
    {
        CreateInstance = 512,
        DeclaredOnly = 2,
        Default = 0,
        FlattenHierarchy = 64,
        GetField = 1024,
        GetProperty = 4096,
        IgnoreCase = 1,
        Instance = 4,
        InvokeMethod = 256,
        NonPublic = 32,
        Public = 16,
        SetField = 2048,
        SetProperty = 8192,
        Static = 8,
    }
}