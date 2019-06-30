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

        private Assembly InternalLoadFromPath(string? assemblyPath, string? nativeImagePath)
        {
            throw new PlatformNotSupportedException();
        }

        internal Assembly InternalLoad(byte[] arrAssembly, byte[] arrSymbols)
        {
            return ReflectionAugments.ReflectionCoreCallbacks.Load(arrAssembly, arrSymbols);
        }

        private static IntPtr InternalLoadUnmanagedDllFromPath(string unmanagedDllPath)
        {
            return InteropServices.NativeLibrary.Load(unmanagedDllPath);
        }

        private Assembly? GetFirstResolvedAssembly(AssemblyName assemblyName)
        {
            Assembly? resolvedAssembly = null;

            Func<AssemblyLoadContext, AssemblyName, Assembly> assemblyResolveHandler = _resolving;

            if (assemblyResolveHandler != null)
            {
                // Loop through the event subscribers and return the first non-null Assembly instance
                foreach (Func<AssemblyLoadContext, AssemblyName, Assembly> handler in assemblyResolveHandler.GetInvocationList())
                {
                    resolvedAssembly = handler(this, assemblyName);
                    if (resolvedAssembly != null)
                    {
                        return resolvedAssembly;
                    }
                }
            }

            return null;
        }

        private Assembly? ResolveUsingEvent(AssemblyName assemblyName)
        {
            string? simpleName = assemblyName.Name;

            // Invoke the AssemblyResolve event callbacks if wired up
            Assembly? assembly = GetFirstResolvedAssembly(assemblyName);
            return assembly;
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

        // This method is called by the VM.
        private static void OnAssemblyLoad(Assembly assembly)
        {
            AssemblyLoad?.Invoke(AppDomain.CurrentDomain, new AssemblyLoadEventArgs(assembly));
        }

        // This method is called by the VM.
        private static Assembly? OnResourceResolve(Assembly assembly, string resourceName)
        {
            return InvokeResolveEvent(ResourceResolve, assembly, resourceName);
        }

        // This method is called by the VM
        private static Assembly? OnTypeResolve(Assembly assembly, string typeName)
        {
            return InvokeResolveEvent(TypeResolve, assembly, typeName);
        }

        // This method is called by the VM.
        private static Assembly? OnAssemblyResolve(Assembly assembly, string assemblyFullName)
        {
            return InvokeResolveEvent(AssemblyResolve, assembly, assemblyFullName);
        }

        private static Assembly? InvokeResolveEvent(ResolveEventHandler eventHandler, Assembly assembly, string name)
        {
            if (eventHandler == null)
                return null;

            var args = new ResolveEventArgs(name, assembly);

            foreach (ResolveEventHandler handler in eventHandler.GetInvocationList())
            {
                Assembly? asm = handler(AppDomain.CurrentDomain, args);
                if (asm != null)
                    return asm;
            }

            return null;
        }
    }
}
