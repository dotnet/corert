// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System.Reflection;
using Internal.Reflection.Augments;

// This type is just stubbed out to be harmonious with CoreCLR
namespace System.Runtime.Loader
{
    public abstract class AssemblyLoadContext
    {
        public static Assembly[] GetLoadedAssemblies() => ReflectionAugments.ReflectionCoreCallbacks.GetLoadedAssemblies(); 

        // These events are never called
        public static event AssemblyLoadEventHandler AssemblyLoad;
        public static event ResolveEventHandler TypeResolve;
        public static event ResolveEventHandler ResourceResolve;
        public static event ResolveEventHandler AssemblyResolve;
    }
}
