// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using Internal.TypeSystem;
using Internal.TypeSystem.Ecma;

namespace ILCompiler
{
    public abstract class CompilationModuleGroup
    {
        protected CompilerTypeSystemContext _typeSystemContext;
        protected ICompilationRootProvider _rootProvider;

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
                    _rootProvider.AddMainMethodCompilationRoot(module);

                AddCompilationRootsForRuntimeExports(module);
            }
        }

        public void AddWellKnownTypes()
        {
            var stringType = _typeSystemContext.GetWellKnownType(WellKnownType.String);

            if (IsTypeInCompilationGroup(stringType))
            {
                _rootProvider.AddTypeCompilationRoot(stringType, "String type is always generated");
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
                        _rootProvider.AddMethodCompilationRoot(method, "Runtime export", exportName);
                    }
                }
            }
        }
    }
}
