// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using Internal.TypeSystem;
using Internal.TypeSystem.Ecma;

using AssemblyName = System.Reflection.AssemblyName;

namespace ILCompiler
{
    /// <summary>
    /// Root provider that roots all code and types in the non-framework assemblies.
    /// </summary>
    class ApplicationAssemblyRootProvider : ICompilationRootProvider
    {
        private readonly CompilerTypeSystemContext _context;

        public ApplicationAssemblyRootProvider(CompilerTypeSystemContext context)
        {
            _context = context;
        }

        public void AddCompilationRoots(IRootingServiceProvider rootProvider)
        {
            foreach (var inputFile in _context.ReferenceFilePaths.Keys)
            {
                ProcessAssembly(inputFile, rootProvider);
            }

            foreach (var inputFile in _context.InputFilePaths.Keys)
            {
                ProcessAssembly(inputFile, rootProvider);
            }
        }

        private void ProcessAssembly(string inputFile, IRootingServiceProvider rootProvider)
        {
            var assembly = (EcmaModule)_context.ResolveAssembly(new AssemblyName(inputFile), false);

            if (FrameworkStringResourceBlockingPolicy.IsFrameworkAssembly(assembly))
                return;

            rootProvider.RootModuleMetadata(assembly, "Application assembly root");

            foreach (TypeDesc type in assembly.GetAllTypes())
            {
                RdXmlRootProvider.RootType(rootProvider, type, "Application assembly root");
            }
        }
    }
}
