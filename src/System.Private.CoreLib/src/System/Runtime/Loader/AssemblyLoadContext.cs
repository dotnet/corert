// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.


using System;
using System.Reflection;
using System.Diagnostics.Contracts;
using System.IO;
using System.Runtime.Versioning;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using System.Security;
using System.Threading;

// This type is just stubbed out to be harmonious with CoreCLR
namespace System.Runtime.Loader
{
    public abstract class AssemblyLoadContext
    {
        public static Assembly[] GetLoadedAssemblies()
        {
            throw new NotImplementedException();
        }

        // These events are never called
        public static event AssemblyLoadEventHandler AssemblyLoad;
        public static event ResolveEventHandler TypeResolve;
        public static event ResolveEventHandler ResourceResolve;
        public static event ResolveEventHandler AssemblyResolve;
    }
}
