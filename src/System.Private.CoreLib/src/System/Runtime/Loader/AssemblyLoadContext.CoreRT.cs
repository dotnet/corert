// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System.Reflection;
using System.Runtime.InteropServices;

using Internal.Reflection.Augments;

// This type is just stubbed out to be harmonious with CoreCLR
namespace System.Runtime.Loader
{
    public partial class AssemblyLoadContext
    {
        internal static Assembly[] GetLoadedAssemblies() => ReflectionAugments.ReflectionCoreCallbacks.GetLoadedAssemblies();

        public Assembly LoadFromAssemblyName(AssemblyName assemblyName)
        {
            return Assembly.Load(assemblyName);
        }

        private static IntPtr InitializeAssemblyLoadContext(IntPtr ptrAssemblyLoadContext, bool fRepresentsTPALoadContext, bool isCollectible)
        {
            return IntPtr.Zero;
        }

        private static void PrepareForAssemblyLoadContextRelease(IntPtr ptrNativeAssemblyLoadContext, IntPtr ptrAssemblyLoadContextStrong)
        {
        }

        public static AssemblyLoadContext? GetLoadContext(Assembly assembly)
        {
            return Default;
        }

        public void SetProfileOptimizationRoot(string directoryPath)
        {
        }

        public void StartProfileOptimization(string profile)
        {
        }

        private Assembly InternalLoadFromPath(string? assemblyPath, string? nativeImagePath)
        {
            throw new PlatformNotSupportedException();
        }

        internal Assembly InternalLoad(byte[] arrAssembly, byte[] arrSymbols)
        {
            return ReflectionAugments.ReflectionCoreCallbacks.Load(arrAssembly, arrSymbols);
        }

        internal IntPtr GetResolvedUnmanagedDll(Assembly assembly, string unmanagedDllName)
        {
            IntPtr resolvedDll = IntPtr.Zero;

            Func<Assembly, string, IntPtr> dllResolveHandler = _resolvingUnmanagedDll;

            if (dllResolveHandler != null)
            {
                // Loop through the event subscribers and return the first non-null native library handle
                foreach (Func<Assembly, string, IntPtr>  handler in dllResolveHandler.GetInvocationList())
                {
                    resolvedDll = handler(assembly, unmanagedDllName);
                    if (resolvedDll != IntPtr.Zero)
                    {
                        return resolvedDll;
                    }
                }
            }

            return IntPtr.Zero;
        }

        private void ReferenceUnreferencedEvents()
        {
            // Dummy method to avoid CS0067 "Event is never used" warning.
            // These are defined in the shared partition and it's not worth the ifdeffing.
            _ = AssemblyLoad;
            _ = ResourceResolve;
            _ = _resolving;
            _ = TypeResolve;
            _ = AssemblyResolve;
        }
    }
}
