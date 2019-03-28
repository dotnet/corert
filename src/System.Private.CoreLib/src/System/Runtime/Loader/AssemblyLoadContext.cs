// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System.Reflection;
using Internal.Reflection.Augments;

// This type is just stubbed out to be harmonious with CoreCLR
namespace System.Runtime.Loader
{
    public class AssemblyLoadContext
    {
        protected AssemblyLoadContext()
        {
        }

        protected AssemblyLoadContext(bool isCollectible)
        {
            IsCollectible = isCollectible;
        }

        public AssemblyLoadContext(string name, bool isCollectible = false)
        {
            IsCollectible = isCollectible;
            Name = name;
        }

        public static Assembly[] GetLoadedAssemblies() => ReflectionAugments.ReflectionCoreCallbacks.GetLoadedAssemblies(); 

        // These events are never called
#pragma warning disable 0067
        public static event AssemblyLoadEventHandler AssemblyLoad;
        public static event ResolveEventHandler TypeResolve;
        public static event ResolveEventHandler ResourceResolve;
        public static event ResolveEventHandler AssemblyResolve;

        public event Func<AssemblyLoadContext, AssemblyName, Assembly> Resolving;
        public event Action<AssemblyLoadContext> Unloading;
#pragma warning restore 0067

        public static AssemblyLoadContext Default { get; } = new DefaultAssemblyLoadContext();

        public static AssemblyLoadContext GetLoadContext(Assembly assembly)
        {
            return Default;
        }

        public Assembly LoadFromAssemblyPath(string assemblyPath)
        {
            throw new PlatformNotSupportedException();
        }

        public bool IsCollectible { get; }

        public string Name { get; }

        public void SetProfileOptimizationRoot(string directoryPath) { }
        public void StartProfileOptimization(string profile) { }

        internal static void OnProcessExit()
        {
            Default.Unloading?.Invoke(Default);
        }

        internal unsafe Assembly InternalLoad(byte[] rawAssembly, byte[] rawSymbolStore)
        {
            return ReflectionAugments.ReflectionCoreCallbacks.Load(rawAssembly, rawSymbolStore);
        }
    }

    /// <summary>
    /// AssemblyLoadContext is not supported in .NET Native. This is
    /// just a dummy class to make applications compile.
    /// </summary>
    internal sealed class DefaultAssemblyLoadContext : AssemblyLoadContext
    {
        internal DefaultAssemblyLoadContext() : base("Default")
        {
        }
    }

    internal sealed class IndividualAssemblyLoadContext : AssemblyLoadContext
    {
        internal IndividualAssemblyLoadContext(string name) : base(name)
        {
        }
    }
}
