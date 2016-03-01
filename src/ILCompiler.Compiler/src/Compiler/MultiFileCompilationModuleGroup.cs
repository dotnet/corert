// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System.Collections.Generic;
using Internal.TypeSystem;
using Internal.TypeSystem.Ecma;

namespace ILCompiler
{
    class MultiFileCompilationModuleGroup : CompilationModuleGroup
    {
        private HashSet<EcmaModule> _compilationModuleSet;

        public MultiFileCompilationModuleGroup(CompilerTypeSystemContext typeSystemContext, ICompilationRootProvider rootProvider) : base(typeSystemContext, rootProvider)
        { }

        public override bool IsTypeInCompilationGroup(TypeDesc type)
        {
            if (type.ContainsGenericVariables)
                return true;

            EcmaType ecmaType = type as EcmaType;

            if (ecmaType == null)
                return true;

            if (!IsModuleInCompilationGroup(ecmaType.EcmaModule))
            {
                return false;
            }

            return true;
        }

        public override bool IsMethodInCompilationGroup(MethodDesc method)
        {
            if (method.GetTypicalMethodDefinition().ContainsGenericVariables)
                return true;

            return IsTypeInCompilationGroup(method.OwningType);
        }

        public override void AddCompilationRoots()
        {
            base.AddCompilationRoots();

            bool buildingLibrary = true;
            foreach (var module in InputModules)
            {
                if (module.PEReader.PEHeaders.IsExe)
                {
                    buildingLibrary = false;
                    break;
                }
            }

            if (buildingLibrary)
            {
                foreach (var module in InputModules)
                {
                    AddCompilationRootsForMultifileLibrary(module);
                }
            }
        }

        private void AddCompilationRootsForMultifileLibrary(EcmaModule module)
        {
            foreach (TypeDesc type in module.GetAllTypes())
            {
                // Skip delegates (since their Invoke methods have no IL) and uninstantiated generic types
                if (type.IsDelegate || type.ContainsGenericVariables)
                    continue;

                EcmaType ecmaType = type as EcmaType;

                if (ecmaType.Attributes.HasFlag(System.Reflection.TypeAttributes.Public))
                {
                    foreach (EcmaMethod method in ecmaType.GetMethods())
                    {
                        // Skip methods with no IL and uninstantiated generic methods
                        if (method.IsIntrinsic || method.IsAbstract || method.ContainsGenericVariables)
                            continue;

                        if (method.ImplAttributes.HasFlag(System.Reflection.MethodImplAttributes.InternalCall))
                            continue;

                        _rootProvider.AddCompilationRoot(method, "Library module method");
                    }
                }
            }
        }

        private bool IsModuleInCompilationGroup(EcmaModule module)
        {
            return InputModules.Contains(module);
        }

        private HashSet<EcmaModule> InputModules
        {
            get
            {
                if (_compilationModuleSet == null)
                {
                    HashSet<EcmaModule> newCompilationModuleSet = new HashSet<EcmaModule>();

                    foreach (var path in _typeSystemContext.InputFilePaths)
                    {
                        newCompilationModuleSet.Add(_typeSystemContext.GetModuleFromPath(path.Value));
                    }

                    lock (this)
                    {
                        if (_compilationModuleSet == null)
                        {
                            _compilationModuleSet = newCompilationModuleSet;
                        }
                    }
                }

                return _compilationModuleSet;
            }
        }
    }
}
