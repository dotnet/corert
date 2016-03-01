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

        public abstract bool IsTypeInCompilationGroup(TypeDesc type);
        public abstract bool IsMethodInCompilationGroup(MethodDesc method);

        public virtual void AddCompilationRoots()
        {
            foreach (var inputFile in _typeSystemContext.InputFilePaths)
            {
                var module = _typeSystemContext.GetModuleFromPath(inputFile.Value);

                if (module.PEReader.PEHeaders.IsExe)
                    AddMainMethodCompilationRoot(module);

                AddCompilationRootsForRuntimeExports(module);
            }
        }

        public void AddWellKnownTypes()
        {
            var stringType = _typeSystemContext.GetWellKnownType(WellKnownType.String);

            if (IsTypeInCompilationGroup(stringType))
            {
                _rootProvider.AddCompilationRoot(stringType, "String type is always generated");
            }
        }

        protected void AddCompilationRootsForRuntimeExports(EcmaModule module)
        {
            foreach (var type in module.GetAllTypes())
            {
                foreach (var method in type.GetMethods())
                {
                    if (method.HasCustomAttribute("System.Runtime", "RuntimeExportAttribute"))
                    {
                        string exportName = ((EcmaMethod)method).GetAttributeStringValue("System.Runtime", "RuntimeExportAttribute");
                        _rootProvider.AddCompilationRoot(method, "Runtime export", exportName);
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
