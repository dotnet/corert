// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Reflection.Metadata.Ecma335;

using Internal.TypeSystem;
using Internal.TypeSystem.Ecma;
using Internal.IL.Stubs.StartupCode;

namespace ILCompiler
{
    public abstract class CompilationModuleGroup
    {
        protected CompilerTypeSystemContext _typeSystemContext;
        protected ICompilationRootProvider _rootProvider;

        public MethodDesc StartupCodeMain
        {
            get; private set;
        }

        protected CompilationModuleGroup(CompilerTypeSystemContext typeSystemContext, ICompilationRootProvider rootProvider)
        {
            _typeSystemContext = typeSystemContext;
            _rootProvider = rootProvider;
        }

        /// <summary>
        /// If true, "type" is in the set of input assemblies being compiled
        /// </summary>
        public abstract bool ContainsType(TypeDesc type);
        /// <summary>
        /// If true, "method" is in the set of input assemblies being compiled
        /// </summary>
        public abstract bool ContainsMethod(MethodDesc method);
        /// <summary>
        /// If true, it's possible for "type" to be generated in multiple modules independently and should be shared
        /// TODO: This API is in flux. Please do not add a dependency on it.
        /// </summary>
        public abstract bool ShouldShareAcrossModules(TypeDesc type);
        /// <summary>
        /// If true, it's possible for "method" to be generated in multiple modules independently and should be shared
        /// TODO: This API is in flux. Please do not add a dependency on it.
        /// </summary>
        public abstract bool ShouldShareAcrossModules(MethodDesc method);
        /// <summary>
        /// If true, all code is compiled into a single module
        /// </summary>
        public abstract bool IsSingleFileCompilation { get; }
        /// <summary>
        /// If true, the full type should be generated. This occurs in situations where the type is 
        /// shared between modules (generics, parameterized types), or the type lives in a different module
        /// and therefore needs a full VTable
        /// </summary>
        public abstract bool ShouldProduceFullType(TypeDesc type);

        public virtual void AddCompilationRoots()
        {
            foreach (var inputFile in _typeSystemContext.InputFilePaths)
            {
                var module = _typeSystemContext.GetModuleFromPath(inputFile.Value);

                if (module.PEReader.PEHeaders.IsExe)
                    AddMainMethodCompilationRoot(module);

                AddCompilationRootsForExports(module);
            }
        }

        public void AddWellKnownTypes()
        {
            var stringType = _typeSystemContext.GetWellKnownType(WellKnownType.String);

            if (ContainsType(stringType))
            {
                _rootProvider.AddCompilationRoot(stringType, "String type is always generated");
            }
        }

        protected void AddCompilationRootsForExports(EcmaModule module)
        {
            foreach (var type in module.GetAllTypes())
            {
                foreach (var method in type.GetMethods())
                {
                    EcmaMethod ecmaMethod = (EcmaMethod)method;

                    if (ecmaMethod.IsRuntimeExport)
                    {
                        string runtimeExportName = ecmaMethod.GetRuntimeExportName();
                        if (runtimeExportName != null)
                            _rootProvider.AddCompilationRoot(method, "Runtime export", runtimeExportName);
                    }

                    if (ecmaMethod.IsNativeCallable)
                    {
                        string nativeCallableExportName = ecmaMethod.GetNativeCallableExportName();
                        if (nativeCallableExportName != null)
                            _rootProvider.AddCompilationRoot(method, "Native callable", nativeCallableExportName);
                    }
                }
            }
        }

        private void AddMainMethodCompilationRoot(EcmaModule module)
        {
            if (StartupCodeMain != null)
                throw new Exception("Multiple entrypoint modules");

            int entryPointToken = module.PEReader.PEHeaders.CorHeader.EntryPointTokenOrRelativeVirtualAddress;
            MethodDesc mainMethod = module.GetMethod(MetadataTokens.EntityHandle(entryPointToken));

            var owningType = module.GetGlobalModuleType();
            StartupCodeMain = new StartupCodeMainMethod(owningType, mainMethod);

            _rootProvider.AddCompilationRoot(StartupCodeMain, "Startup Code Main Method", "__managed__Main");
        }
    }
}
