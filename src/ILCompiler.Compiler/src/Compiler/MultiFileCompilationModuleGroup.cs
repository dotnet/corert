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

        public MultiFileCompilationModuleGroup(IEnumerable<EcmaModule> compilationModuleSet)
        {
            _compilationModuleSet = new HashSet<EcmaModule>(compilationModuleSet);
        }

        public override bool ContainsType(TypeDesc type)
        {
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
            if (method.HasInstantiation)
                return true;

            return ContainsType(method.OwningType);
        }

        private bool BuildingLibrary
        {
            get
            {
                foreach (var module in _compilationModuleSet)
                {
                    if (module.PEReader.PEHeaders.IsExe)
                    {
                        return false;
                    }
                }
                return true;
            }
        }

        private bool IsModuleInCompilationGroup(EcmaModule module)
        {
            return _compilationModuleSet.Contains(module);
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
            if (EETypeNode.IsTypeNodeShareable(type))
                return true;

            // If referring to a type from another module, VTables, interface maps, etc should assume the
            // type is fully build.
            if (!ContainsType(type))
                return true;

            return false;
        }

        public override bool ShouldReferenceThroughImportTable(TypeDesc type)
        {
            return false;
        }
    }
}
