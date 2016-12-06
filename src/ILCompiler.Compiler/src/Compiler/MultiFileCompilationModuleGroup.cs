﻿// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Collections.Generic;

using ILCompiler.DependencyAnalysis;
using Internal.TypeSystem;
using Internal.TypeSystem.Ecma;

namespace ILCompiler
{
    public abstract class MultiFileCompilationModuleGroup : CompilationModuleGroup
    {
        private HashSet<ModuleDesc> _compilationModuleSet;

        public MultiFileCompilationModuleGroup(TypeSystemContext context, IEnumerable<ModuleDesc> compilationModuleSet)
            : base(context)
        {
            _compilationModuleSet = new HashSet<ModuleDesc>(compilationModuleSet);

            // The fake assembly that holds compiler generated types is part of the compilation.
            _compilationModuleSet.Add(this.GeneratedAssembly);
        }

        public sealed override bool ContainsType(TypeDesc type)
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

        public sealed override bool ContainsMethod(MethodDesc method)
        {
            if (method.HasInstantiation)
                return true;

            return ContainsType(method.OwningType);
        }

        private bool IsModuleInCompilationGroup(EcmaModule module)
        {
            return _compilationModuleSet.Contains(module);
        }

        public sealed override bool IsSingleFileCompilation
        {
            get
            {
                return false;
            }
        }

        public sealed override bool ShouldReferenceThroughImportTable(TypeDesc type)
        {
            return false;
        }
    }

    public class MultiFileLibraryCompilationModuleGroup : MultiFileCompilationModuleGroup
    {
        public MultiFileLibraryCompilationModuleGroup(TypeSystemContext context, IEnumerable<ModuleDesc> compilationModuleSet)
            : base(context, compilationModuleSet)
        {
        }

        public override bool ShouldProduceFullType(TypeDesc type)
        {
            return true;
        }
    }

    public class MultiFileLeafCompilationModuleGroup : MultiFileCompilationModuleGroup
    {
        public MultiFileLeafCompilationModuleGroup(TypeSystemContext context, IEnumerable<ModuleDesc> compilationModuleSet)
            : base(context, compilationModuleSet)
        {
        }

        public override bool ShouldProduceFullType(TypeDesc type)
        {
            // Fully build all shareable types so they will be identical in each module
            if (EETypeNode.IsTypeNodeShareable(type))
                return true;

            // If referring to a type from another module, VTables, interface maps, etc should assume the
            // type is fully build.
            if (!ContainsType(type))
                return true;

            return false;
        }
    }
}
