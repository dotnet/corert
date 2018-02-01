// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using Internal.TypeSystem;

namespace ILCompiler
{
    /// <summary>
    /// Compilation roots necessary to enable use of the jit intrinsic based Comparer<T>.Default and EqualityComparer<T>.Default implementations
    /// </summary>
    public class ComparerCompilationRootProvider : ICompilationRootProvider
    {
        TypeSystemContext _context;

        public ComparerCompilationRootProvider(TypeSystemContext context)
        {
            _context = context;
        }

        private Instantiation GetUniformInstantiation(int numArgs, TypeDesc uniformInstanitationType)
        {
            TypeDesc[] args = new TypeDesc[numArgs];
            for (int i = 0; i < numArgs; i++)
                args[i] = uniformInstanitationType;
            return new Instantiation(args);
        }

        private MethodDesc InstantiateMethodOverUniformType(MethodDesc method, TypeDesc uniformInstanitationType)
        {
            method = method.GetTypicalMethodDefinition();

            Instantiation typeUniformInstantiation = GetUniformInstantiation(method.OwningType.Instantiation.Length, uniformInstanitationType);

            DefType owningType;

            if (typeUniformInstantiation.Length == 0)
                owningType = (DefType)method.OwningType;
            else
                owningType = ((MetadataType)method.OwningType).MakeInstantiatedType(typeUniformInstantiation);

            Instantiation methodUniformInstantiation = GetUniformInstantiation(method.Instantiation.Length, uniformInstanitationType);

            MethodDesc uninstantiatedMethod = owningType.FindMethodOnTypeWithMatchingTypicalMethod(method);

            if (methodUniformInstantiation.Length == 0)
                return uninstantiatedMethod;
            else
                return uninstantiatedMethod.MakeInstantiatedMethod(methodUniformInstantiation);
        }

        private void AddUniformInstantiationForMethod(IRootingServiceProvider rootProvider, MethodDesc method, TypeDesc uniformInstanitationType)
        {
            MethodDesc instantiatedMethod = InstantiateMethodOverUniformType(method, uniformInstanitationType);
            rootProvider.AddCompilationRoot(instantiatedMethod, "Adding uniform instantiation");
        }

        private void AddCanonInstantiationsForMethod(IRootingServiceProvider rootProvider, MethodDesc method, bool normalCanonSupported)
        {
            if (_context.SupportsCanon && normalCanonSupported)
                AddUniformInstantiationForMethod(rootProvider, method, _context.CanonType);

            if (_context.SupportsUniversalCanon)
                AddUniformInstantiationForMethod(rootProvider, method, _context.UniversalCanonType);
        }

        private void AddCanonInstantiationsForMethod(IRootingServiceProvider rootProvider, MetadataType type, string methodName, bool normalCanonSupported)
        {
            MethodDesc method = type.GetMethod(methodName, null);
            AddCanonInstantiationsForMethod(rootProvider, method, normalCanonSupported);
        }

        public void AddCompilationRoots(IRootingServiceProvider rootProvider)
        {
            ModuleDesc systemModule = ((MetadataType)_context.GetWellKnownType(WellKnownType.Object)).Module;

            MetadataType equalityComparerType = systemModule.GetType("Internal.IntrinsicSupport", "EqualityComparerHelpers", false) as MetadataType;
            if (equalityComparerType != null)
            {
                AddCanonInstantiationsForMethod(rootProvider, equalityComparerType, "GetKnownGenericEquatableComparer", true);
                AddCanonInstantiationsForMethod(rootProvider, equalityComparerType, "GetKnownObjectEquatableComparer", true);
                AddCanonInstantiationsForMethod(rootProvider, equalityComparerType, "GetKnownNullableEquatableComparer", false);
                AddCanonInstantiationsForMethod(rootProvider, equalityComparerType, "GetKnownEnumEquatableComparer", false);
            }

            MetadataType comparerType = systemModule.GetType("Internal.IntrinsicSupport", "ComparerHelpers", false) as MetadataType;
            if (comparerType != null)
            {
                AddCanonInstantiationsForMethod(rootProvider, comparerType, "GetKnownGenericComparer", true);
                AddCanonInstantiationsForMethod(rootProvider, comparerType, "GetKnownObjectComparer", true);
                AddCanonInstantiationsForMethod(rootProvider, comparerType, "GetKnownNullableComparer", false);
            }
        }
    }
}
