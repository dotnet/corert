// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;

using Internal.TypeSystem;
using Internal.TypeSystem.Ecma;
using Internal.IL.Stubs.StartupCode;

namespace ILCompiler
{
    /// <summary>
    /// Computes a compilation root based on the entrypoint of the assembly.
    /// </summary>
    public class MainMethodRootProvider : ICompilationRootProvider
    {
        /// <summary>
        /// Symbolic name under which the managed entrypoint is exported.
        /// </summary>
        public const string ManagedEntryPointMethodName = "__managed__Main";

        private EcmaModule _module;

        public MainMethodRootProvider(EcmaModule module)
        {
            _module = module;
        }

        public void AddCompilationRoots(IRootingServiceProvider rootProvider)
        {
            MethodDesc mainMethod = _module.EntryPoint;
            if (mainMethod == null)
                throw new Exception("No managed entrypoint defined for executable module");

            TypeDesc owningType = _module.GetGlobalModuleType();
            var startupCodeMain = new StartupCodeMainMethod(owningType, mainMethod);

            rootProvider.AddCompilationRoot(startupCodeMain, "Startup Code Main Method", ManagedEntryPointMethodName);
        }
    }
}
