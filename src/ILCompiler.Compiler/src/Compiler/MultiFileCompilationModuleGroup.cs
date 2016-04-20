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

        public override bool ContainsType(TypeDesc type)
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

        public override bool ContainsMethod(MethodDesc method)
        {
            if (method.GetTypicalMethodDefinition().ContainsGenericVariables)
                return true;

            return ContainsType(method.OwningType);
        }

        private bool BuildingLibrary
        {
            get
            {
                foreach (var module in InputModules)
                {
                    if (module.PEReader.PEHeaders.IsExe)
                    {
                        return false;
                    }
                }
                return true;
            }
        }

        public override void AddCompilationRoots()
        {
            base.AddCompilationRoots();
            
            if (BuildingLibrary)
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

                _rootProvider.AddCompilationRoot(type, "Library module type");
                RootMethods(type, "Library module method");
            }
        }

        private bool IsModuleInCompilationGroup(EcmaModule module)
        {
            return InputModules.Contains(module);
        }

        public override bool IsSingleFileCompilation
        {
            get
            {
                return false;
            }
        }
        
        public override bool ShouldProduceFullType(TypeDesc type)
        {
            // TODO: Remove this once we have delgate constructor transform added and GetMethods() tells us about
            //       the virtuals we add on to delegate types.
            if (type.IsDelegate)
                return false;

            // Fully build all types when building a library
            if (BuildingLibrary)
                return true;

            // Fully build all shareable types so they will be identical in each module
            if (ShouldShareAcrossModules(type))
                return true;

            // If referring to a type from another module, VTables, interface maps, etc should assume the
            // type is fully build.
            if (!ContainsType(type))
                return true;
            
            return false;
        }

        public override bool ShouldShareAcrossModules(MethodDesc method)
        {
            if (method is InstantiatedMethod)
                return true;

            return ShouldShareAcrossModules(method.OwningType);
        }

        public override bool ShouldShareAcrossModules(TypeDesc type)
        {
            if (type is ParameterizedType || type is InstantiatedType)
            {
                return true;
            }

            return false;
        }

        private void RootMethods(TypeDesc type, string reason)
        {
            foreach (MethodDesc method in type.GetMethods())
            {
                // Skip methods with no IL and uninstantiated generic methods
                if (method.IsIntrinsic || method.IsAbstract || method.ContainsGenericVariables)
                    continue;
                
                if (method.IsInternalCall)
                    continue;

                _rootProvider.AddCompilationRoot(method, reason);
            }
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
