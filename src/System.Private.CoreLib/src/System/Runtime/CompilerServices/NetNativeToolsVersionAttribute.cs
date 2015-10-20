// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace System.Runtime.CompilerServices
{
    /// <summary>
    /// For assemblies which are produced by the .NET Native toolchain this attribute is used to store
    /// the version of the toolchain which produced such assembly.
    /// This is used for example for the IL TOC files in production builds.
    /// </summary>
    [AttributeUsage(AttributeTargets.Assembly, AllowMultiple = false, Inherited = false)]
    public class NetNativeToolsVersionAttribute : Attribute
    {
        public NetNativeToolsVersionAttribute(string netNativeToolsVersion) { /* nothing to do */ }
    }
}
