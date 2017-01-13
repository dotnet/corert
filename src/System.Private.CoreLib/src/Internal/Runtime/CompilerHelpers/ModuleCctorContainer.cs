// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Runtime;
using System.Runtime.CompilerServices;

using Debug = System.Diagnostics.Debug;

namespace Internal.Runtime.CompilerHelpers
{
    /// <summary>
    /// Container class to run specific class constructors in a defined order. Since we can't
    /// directly invoke class constructors in C#, they're renamed ILC_cctor (to sort of align
    /// with .NET Native).
    /// </summary>
    internal class ILT_ModuleCctorContainer
    {
        public static void ILT_cctor()
        {
            PreallocatedOutOfMemoryException.ILT_cctor();
            ClassConstructorRunner.ILT_cctor();
            TypeCast.CastCache.ILT_cctor();
            TypeLoaderExports.ILT_cctor();
        }
    }
}
