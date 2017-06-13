// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Diagnostics;
using System.Collections.Generic;

using ILCompiler.DependencyAnalysisFramework;
using Internal.Text;
using Internal.TypeSystem;

namespace ILCompiler.DependencyAnalysis
{
    /// <summary>
    /// This analysis node is used for computing GVM dependencies for the following cases:
    ///    1) Derived types where the GVM is overridden
    ///    2) Variant-interfaces GVMs
    /// This analysis node will ensure that the proper GVM instantiations are compiled on types.
    /// </summary>
    internal class GVMDependenciesNode : DependencyNodeCore<NodeFactory>
    {
        private MethodDesc _method;

        public MethodDesc Method => _method;

        public GVMDependenciesNode(MethodDesc method)
        {
            Debug.Assert(!method.IsRuntimeDeterminedExactMethod);
            Debug.Assert(method.IsVirtual && method.HasInstantiation);
            _method = method;
        }

        public override bool HasConditionalStaticDependencies => false;
        public override bool InterestingForDynamicDependencyAnalysis => false;
        public override bool StaticDependenciesAreComputed => true;
        protected override string GetName(NodeFactory factory) => "__GVMDependenciesNode_" + factory.NameMangler.GetMangledMethodName(_method);

        public override IEnumerable<DependencyListEntry> GetStaticDependencies(NodeFactory context)
        {
            // TODO: https://github.com/dotnet/corert/issues/3224
            if (_method.IsAbstract)
            {
                yield return new DependencyListEntry(context.ReflectableMethod(_method), "Abstract reflectable method");
            }
            else
            {
                // Universal canonical instantiations should be entirely universal canon
                if (_method.IsCanonicalMethod(CanonicalFormKind.Universal))
                    _method = _method.GetCanonMethodTarget(CanonicalFormKind.Universal);

                // TODO: verify for invalid instantiations, like List<void>?
                bool validInstantiation =
                    _method.IsSharedByGenericInstantiations ||       // Non-exact methods are always valid instantiations (always pass constraints check)
                    _method.CheckConstraints();                      // Verify that the instantiation does not violate constraints

                if (validInstantiation)
                {
                    MethodDesc canonMethodTarget = _method.GetCanonMethodTarget(CanonicalFormKind.Specific);

                    bool getUnboxingStub = (_method.OwningType.IsValueType || _method.OwningType.IsEnum);
                    yield return new DependencyListEntry(context.MethodEntrypoint(canonMethodTarget, getUnboxingStub), "GVM Dependency - Canon method");

                    if (canonMethodTarget != _method)
                    {
                        // Dependency includes the generic method dictionary of the instantiation, and all its dependencies. This is done by adding the 
                        // ShadowConcreteMethod to the list of dynamic dependencies. The generic dictionary will be reported as a dependency of the ShadowConcreteMethod
                        // TODO: detect large recursive generics and fallback to USG templates
                        Debug.Assert(!_method.IsCanonicalMethod(CanonicalFormKind.Any));
                        yield return new DependencyListEntry(context.ShadowConcreteMethod(_method), "GVM Dependency - Dictionary");
                    }
                }
                else
                {
                    // TODO: universal generics
                }
            }
        }

        public override IEnumerable<CombinedDependencyListEntry> GetConditionalStaticDependencies(NodeFactory context)
        {
            return Array.Empty<CombinedDependencyListEntry>();
        }

        public override bool HasDynamicDependencies
        {
            get
            {
                if (_method.IsCanonicalMethod(CanonicalFormKind.Specific))
                    return false;

                if (_method.OwningType.IsCanonicalSubtype(CanonicalFormKind.Universal) &&
                    _method.OwningType != _method.OwningType.ConvertToCanonForm(CanonicalFormKind.Universal))
                    return false;

                return true;
            }
        }

        public override IEnumerable<CombinedDependencyListEntry> SearchDynamicDependencies(List<DependencyNodeCore<NodeFactory>> markedNodes, int firstNode, NodeFactory factory)
        {
            Debug.Assert(_method.IsVirtual && _method.HasInstantiation);

            List<CombinedDependencyListEntry> dynamicDependencies = new List<CombinedDependencyListEntry>();

            // Disable dependence tracking for ProjectN
            if (factory.Target.Abi == TargetAbi.ProjectN)
            {
                return dynamicDependencies;
            }

            for (int i = firstNode; i < markedNodes.Count; i++)
            {
                DependencyNodeCore<NodeFactory> entry = markedNodes[i];
                EETypeNode entryAsEETypeNode = entry as EETypeNode;

                if (entryAsEETypeNode == null)
                    continue;

                TypeDesc potentialOverrideType = entryAsEETypeNode.Type;
                if (!potentialOverrideType.IsDefType)
                    continue;

                Debug.Assert(!potentialOverrideType.IsRuntimeDeterminedSubtype);

                if (potentialOverrideType.IsInterface)
                {
                    if (_method.OwningType.HasSameTypeDefinition(potentialOverrideType) && (potentialOverrideType != _method.OwningType))
                    {
                        if (_method.OwningType.CanCastTo(potentialOverrideType))
                        {
                            // Variance expansion
                            MethodDesc matchingMethodOnRelatedVariantMethod = potentialOverrideType.GetMethod(_method.Name, _method.GetTypicalMethodDefinition().Signature);
                            matchingMethodOnRelatedVariantMethod = _method.Context.GetInstantiatedMethod(matchingMethodOnRelatedVariantMethod, _method.Instantiation);
                            dynamicDependencies.Add(new CombinedDependencyListEntry(factory.GVMDependencies(matchingMethodOnRelatedVariantMethod), null, "GVM Variant Interface dependency"));
                        }
                    }

                    continue;
                }

                // If this is an interface gvm, look for types that implement the interface
                // and other instantantiations that have the same canonical form.
                // This ensure the various slot numbers remain equivalent across all types where there is an equivalence
                // relationship in the vtable.
                if (_method.OwningType.IsInterface)
                {
                    foreach (DefType interfaceImpl in potentialOverrideType.RuntimeInterfaces)
                    {
                        if (interfaceImpl == _method.OwningType)
                        {
                            MethodDesc slotDecl = potentialOverrideType.ResolveInterfaceMethodTarget(_method.GetMethodDefinition());
                            if (slotDecl != null)
                                CreateDependencyForMethodSlotAndInstantiation(slotDecl, dynamicDependencies, factory);
                        }
                    }
                }
                else
                {
                    // Quickly check if the potential overriding type is at all related to the GVM's owning type (there is no need
                    // to do any processing for a type that is not at all related to the GVM's owning type).
                    TypeDesc overrideTypeCur = potentialOverrideType;
                    {
                        do
                        {
                            if (overrideTypeCur == _method.OwningType)
                                break;

                            overrideTypeCur = overrideTypeCur.BaseType;
                        }
                        while (overrideTypeCur != null);

                        if (overrideTypeCur == null)
                            continue;
                    }

                    overrideTypeCur = potentialOverrideType;
                    while (overrideTypeCur != null)
                    {
                        if (overrideTypeCur == _method.OwningType)
                        {
                            // The GVMDependencyNode already declares the entrypoint/dictionary dependencies of the current method 
                            // as static dependencies, therefore we can break the loop as soon we hit the current method's owning type
                            // while we're traversing the hierarchy of the potential derived types.
                            break;
                        }

                        MethodDesc instantiatedTargetMethod = overrideTypeCur.FindVirtualFunctionTargetMethodOnObjectType(_method);
                        if (instantiatedTargetMethod != null)
                            dynamicDependencies.Add(new CombinedDependencyListEntry(factory.GVMDependencies(instantiatedTargetMethod), null, "DerivedMethodInstantiation"));

                        overrideTypeCur = overrideTypeCur.BaseType;
                    }
                }
            }
            return dynamicDependencies;
        }

        public static int repeat = 0;
        public static int total = 0;
        HashSet<MethodDesc> done = new HashSet<MethodDesc>();

        private void CreateDependencyForMethodSlotAndInstantiation(MethodDesc methodDef, List<CombinedDependencyListEntry> dynamicDependencies, NodeFactory factory)
        {
            Debug.Assert(methodDef != null);
            Debug.Assert(!methodDef.Signature.IsStatic);

            if (methodDef.IsAbstract)
                return;

            total++;
            if (!done.Add(methodDef))
            {
                //Debugger.Break();
                repeat++;
                return;
            }

            MethodDesc derivedMethodInstantiation = _method.Context.GetInstantiatedMethod(methodDef, _method.Instantiation);
            dynamicDependencies.Add(new CombinedDependencyListEntry(factory.GVMDependencies(derivedMethodInstantiation), null, "DerivedMethodInstantiation"));
        }
    }
}
