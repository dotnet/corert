// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Reflection;
using System.Runtime;
using System.Runtime.CompilerServices;
using System.Threading;
using Debug = System.Diagnostics.Debug;

namespace Internal.Runtime.CompilerHelpers
{
    /// <summary>
    /// Container class to run specific class constructors in a defined order. Since we can't
    /// directly invoke class constructors in C#, they're renamed Initialize.
    /// </summary>
    internal class LibraryInitializer
    {
        public static void InitializeLibrary()
        {
            ManagedThreadId.PrintLine("InitLib");
            var x = "InitLib".GetHashCode();
            ManagedThreadId.PrintLine("InitLib called GetHashCode");
            var ran = new RuntimeAssemblyName("something", new Version(1, 1), "en-GB", AssemblyNameFlags.None, null);
            x = ran.GetHashCode();
            ManagedThreadId.PrintLine("InitLib called  RuntimeAssemblyName GetHashCode");

            PreallocatedOutOfMemoryException.Initialize();
            x = "InitLib2".GetHashCode();
            ManagedThreadId.PrintLine("InitLib called GetHashCode2");
            ClassConstructorRunner.Initialize();
            TypeLoaderExports.Initialize();
        }
    }
}
