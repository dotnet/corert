// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System.Collections.Generic;
using System.Diagnostics;

using ILCompiler.DependencyAnalysis;
using Internal.TypeSystem;
using Internal.TypeSystem.Ecma;

namespace ILCompiler
{
    public class ReadyToRunSingleAssemblyCompilationModuleGroup : CompilationModuleGroup
    {
        private HashSet<ModuleDesc> _compilationModuleSet;

        public ReadyToRunSingleAssemblyCompilationModuleGroup(TypeSystemContext context, IEnumerable<ModuleDesc> compilationModuleSet)
        {
            _compilationModuleSet = new HashSet<ModuleDesc>(compilationModuleSet);

            // The fake assembly that holds compiler generated types is part of the compilation.
            _compilationModuleSet.Add(context.GeneratedAssembly);
        }

        public sealed override bool ContainsType(TypeDesc type)
        {
            if (type is EcmaType ecmaType)
            {
                return IsModuleInCompilationGroup(ecmaType.EcmaModule);
            }
            if (type is InstantiatedType instantiatedType)
            {
                return ContainsType(instantiatedType.GetTypeDefinition());
            }
            return true;
        }

        public sealed override bool ContainsTypeDictionary(TypeDesc type)
        {
            return ContainsType(type);
        }

        public sealed override bool ContainsMethodBody(MethodDesc method, bool unboxingStub)
        {
            if (method is ArrayMethod)
            {
                // TODO-PERF: for now, we never emit native code for array methods as Crossgen ignores
                // them too. At some point we might be able to "exceed Crossgen CQ" by adding this support.
                return false;
            }

            return ContainsType(method.OwningType);
        }

        public sealed override bool ContainsMethodDictionary(MethodDesc method)
        {
            Debug.Assert(method.GetCanonMethodTarget(CanonicalFormKind.Specific) != method);
            return ContainsMethodBody(method, false);
        }

        public override bool ImportsMethod(MethodDesc method, bool unboxingStub)
        {
            return false;
        }

        public sealed override ExportForm GetExportTypeForm(TypeDesc type)
        {
            return ExportForm.None;
        }

        public sealed override ExportForm GetExportTypeFormDictionary(TypeDesc type)
        {
            return ExportForm.None;
        }

        public sealed override ExportForm GetExportMethodForm(MethodDesc method, bool unboxingStub)
        {
            return ExportForm.None;
        }

        public override ExportForm GetExportMethodDictionaryForm(MethodDesc method)
        {
            return ExportForm.None;
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

        public override bool CanHaveReferenceThroughImportTable
        {
            get
            {
                return false;
            }
        }

        public override bool ShouldProduceFullVTable(TypeDesc type)
        {
            return false;
        }

        public override bool ShouldPromoteToFullType(TypeDesc type)
        {
            return ShouldProduceFullVTable(type);
        }

        public override bool PresenceOfEETypeImpliesAllMethodsOnType(TypeDesc type)
        {
            return (type.HasInstantiation || type.IsArray) && ShouldProduceFullVTable(type) &&
                   type.ConvertToCanonForm(CanonicalFormKind.Specific).IsCanonicalSubtype(CanonicalFormKind.Any);
        }
    }
}
