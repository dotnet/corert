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

        Dictionary<TypeDesc, bool> _containsTypeLayoutCache = new Dictionary<TypeDesc, bool>();

        /// <summary>
        /// If true, the type is fully contained in the current compilation group.
        /// </summary>
        /// <returns></returns>
        public override bool ContainsTypeLayout(TypeDesc type)
        {
            bool containsTypeLayout;
            if (_containsTypeLayoutCache.TryGetValue(type, out containsTypeLayout))
            {
                return containsTypeLayout;
            }
            HashSet<TypeDesc> recursionGuard = new HashSet<TypeDesc>();
            return ContainsTypeLayout(type, recursionGuard);
        }

        private bool ContainsTypeLayout(TypeDesc type, HashSet<TypeDesc> recursionGuard)
        {
            if (!recursionGuard.Add(type))
            {
                // We've recursively found the same type - no reason to scan it again
                return true;
            }

            try
            {
                bool containsTypeLayout = ContainsTypeLayoutUncached(type, recursionGuard);
                _containsTypeLayoutCache[type] = containsTypeLayout;
                return containsTypeLayout;
            }
            finally
            {
                recursionGuard.Remove(type);
            }
        }

        private bool ContainsTypeLayoutUncached(TypeDesc type, HashSet<TypeDesc> recursionGuard)
        {
            if (type.IsValueType || 
                type.IsObject || 
                type.IsPrimitive || 
                type.IsEnum || 
                type.IsPointer ||
                type.IsFunctionPointer ||
                type.IsByRefLike ||
                type.IsCanonicalDefinitionType(CanonicalFormKind.Any))
            {
                return true;
            }
            DefType defType = type.GetClosestDefType();
            if (!ContainsType(defType.GetTypeDefinition()))
            {
                return false;
            }
            if (defType.BaseType != null && !ContainsTypeLayout(defType.BaseType, recursionGuard))
            {
                return false;
            }
            foreach (TypeDesc genericArg in defType.Instantiation)
            {
                if (!ContainsTypeLayout(genericArg, recursionGuard))
                {
                    return false;
                }
            }
            foreach (FieldDesc field in defType.GetFields())
            {
                if (!field.IsLiteral && 
                    !field.IsStatic && 
                    !field.HasRva && 
                    !ContainsTypeLayout(field.FieldType, recursionGuard))
                {
                    return false;
                }
            }

            return true;
        }
    }
}
