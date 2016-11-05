// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System.Collections.Generic;

using ILCompiler.DependencyAnalysis;
using Internal.TypeSystem;
using Internal.TypeSystem.Ecma;

namespace ILCompiler
{
    public class MultiFileCompilationModuleGroup : CompilationModuleGroup
    {
        private HashSet<EcmaModule> _compilationModuleSet;

        public MultiFileCompilationModuleGroup(CompilerTypeSystemContext typeSystemContext) : base(typeSystemContext)
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

        public override void AddCompilationRoots(IRootingServiceProvider rootProvider)
        {
            base.AddCompilationRoots(rootProvider);

            if (BuildingLibrary)
            {
                foreach (var module in InputModules)
                {
                    AddCompilationRootsForMultifileLibrary(module, rootProvider);
                }
            }
        }

        private void AddCompilationRootsForMultifileLibrary(EcmaModule module, IRootingServiceProvider rootProvider)
        {
            foreach (TypeDesc type in module.GetAllTypes())
            {
                // Skip delegates (since their Invoke methods have no IL) and uninstantiated generic types
                if (type.IsDelegate || type.ContainsGenericVariables)
                    continue;

                try
                {
                    rootProvider.AddCompilationRoot(type, "Library module type");
                }
                catch (TypeSystemException)
                {
                    // TODO: fail compilation if a switch was passed

                    // Swallow type load exceptions while rooting
                    continue;

                    // TODO: Log as a warning
                }

                RootMethods(type, "Library module method", rootProvider);
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
            if (type.IsParameterizedType || type.IsFunctionPointer || type is InstantiatedType)
            {
                return true;
            }

            return false;
        }

        private void RootMethods(TypeDesc type, string reason, IRootingServiceProvider rootProvider)
        {
            foreach (MethodDesc method in type.GetMethods())
            {
                // Skip methods with no IL and uninstantiated generic methods
                if (method.IsIntrinsic || method.IsAbstract || method.ContainsGenericVariables)
                    continue;

                if (method.IsInternalCall)
                    continue;

                try
                {
                    CheckCanGenerateMethod(method);
                    rootProvider.AddCompilationRoot(method, reason);
                }
                catch (TypeSystemException)
                {
                    // TODO: fail compilation if a switch was passed

                    // Individual methods can fail to load types referenced in their signatures.
                    // Skip them in library mode since they're not going to be callable.
                    continue;

                    // TODO: Log as a warning
                }
            }
        }

        public override bool ShouldReferenceThroughImportTable(TypeDesc type)
        {
            return false;
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

        /// <summary>
        /// Validates that it will be possible to generate '<paramref name="method"/>' based on the types 
        /// in its signature. Unresolvable types in a method's signature prevent RyuJIT from generating
        /// even a stubbed out throwing implementation.
        /// </summary>
        private static void CheckCanGenerateMethod(MethodDesc method)
        {
            MethodSignature signature = method.Signature;

            CheckTypeCanBeUsedInSignature(signature.ReturnType);

            for (int i = 0; i < signature.Length; i++)
            {
                CheckTypeCanBeUsedInSignature(signature[i]);
            }
        }

        private static void CheckTypeCanBeUsedInSignature(TypeDesc type)
        {
            MetadataType defType = type as MetadataType;

            if (defType != null)
            {
                defType.ComputeTypeContainsGCPointers();
            }
        }
    }
}
