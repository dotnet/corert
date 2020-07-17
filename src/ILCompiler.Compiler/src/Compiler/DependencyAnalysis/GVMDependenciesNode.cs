// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

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
    public class GVMDependenciesNode : DependencyNodeCore<NodeFactory>
    {
        private const int UniversalCanonGVMDepthHeuristic_NonCanonDepth = 2;
        private const int UniversalCanonGVMDepthHeuristic_CanonDepth = 2;
        private readonly MethodDesc _method;

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
            DependencyList dependencies = null;

            context.MetadataManager.GetDependenciesDueToVirtualMethodReflectability(ref dependencies, context, _method);
            
            if (!_method.IsAbstract)
            {
                MethodDesc instantiatedMethod = _method;

                // Universal canonical instantiations should be entirely universal canon
                if (instantiatedMethod.IsCanonicalMethod(CanonicalFormKind.Universal))
                    instantiatedMethod = instantiatedMethod.GetCanonMethodTarget(CanonicalFormKind.Universal);

                bool validInstantiation =
                    instantiatedMethod.IsSharedByGenericInstantiations || (      // Non-exact methods are always valid instantiations (always pass constraints check)
                        instantiatedMethod.Instantiation.CheckValidInstantiationArguments() &&
                        instantiatedMethod.OwningType.Instantiation.CheckValidInstantiationArguments() &&
                        instantiatedMethod.CheckConstraints());

                if (validInstantiation)
                {
                    MethodDesc canonMethodTarget = instantiatedMethod.GetCanonMethodTarget(CanonicalFormKind.Specific);

                    if (context.TypeSystemContext.SupportsUniversalCanon && canonMethodTarget.IsGenericDepthGreaterThan(UniversalCanonGVMDepthHeuristic_CanonDepth))
                    {
                        // fall back to using the universal generic variant of the generic method
                        return dependencies;
                    }

                    bool getUnboxingStub = instantiatedMethod.OwningType.IsValueType;
                    dependencies = dependencies ?? new DependencyList();
                    dependencies.Add(context.MethodEntrypoint(canonMethodTarget, getUnboxingStub), "GVM Dependency - Canon method");

                    if (canonMethodTarget != instantiatedMethod)
                    {
                        // Dependency includes the generic method dictionary of the instantiation, and all its dependencies.
                        Debug.Assert(!instantiatedMethod.IsCanonicalMethod(CanonicalFormKind.Any));

                        if (context.TypeSystemContext.SupportsUniversalCanon && instantiatedMethod.IsGenericDepthGreaterThan(UniversalCanonGVMDepthHeuristic_NonCanonDepth))
                        {
                            // fall back to using the universal generic variant of the generic method
                            return dependencies;
                        }

                        dependencies.Add(context.MethodGenericDictionary(instantiatedMethod), "GVM Dependency - Dictionary");
                        dependencies.Add(context.NativeLayout.TemplateMethodEntry(canonMethodTarget), "GVM Dependency - Template entry");
                        dependencies.Add(context.NativeLayout.TemplateMethodLayout(canonMethodTarget), "GVM Dependency - Template");
                    }
                }
            }

            return dependencies;
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
                        if (potentialOverrideType.CanCastTo(_method.OwningType))
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
                            {
                                MethodDesc implementingMethodInstantiation = _method.Context.GetInstantiatedMethod(slotDecl, _method.Instantiation);
                                dynamicDependencies.Add(new CombinedDependencyListEntry(factory.GVMDependencies(implementingMethodInstantiation), null, "ImplementingMethodInstantiation"));
                            }
                        }
                    }
                }
                else
                {
                    // Quickly check if the potential overriding type is at all related to the GVM's owning type (there is no need
                    // to do any processing for a type that is not at all related to the GVM's owning type. Resolving virtuals is expensive).
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
    }
}
