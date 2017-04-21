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
            // Reflection invoke stub handling is here because in the current reflection model we reflection-enable
            // all methods that are compiled. Ideally the list of reflection enabled methods should be known before
            // we even start the compilation process (with the invocation stubs being compilation roots like any other).
            // The existing model has it's problems: e.g. the invocability of the method depends on inliner decisions.
            if (context.MetadataManager.IsReflectionInvokable(_method) && _method.IsAbstract)
            {
                DependencyList dependencies = new DependencyList();

                if (context.MetadataManager.HasReflectionInvokeStubForInvokableMethod(_method) && !_method.IsCanonicalMethod(CanonicalFormKind.Any))
                {
                    MethodDesc canonInvokeStub = context.MetadataManager.GetCanonicalReflectionInvokeStub(_method);
                    if (canonInvokeStub.IsSharedByGenericInstantiations)
                    {
                        dependencies.Add(new DependencyListEntry(context.MetadataManager.DynamicInvokeTemplateData, "Reflection invoke template data"));
                        context.MetadataManager.DynamicInvokeTemplateData.AddDependenciesDueToInvokeTemplatePresence(ref dependencies, context, canonInvokeStub);
                    }
                    else
                        dependencies.Add(new DependencyListEntry(context.MethodEntrypoint(canonInvokeStub), "Reflection invoke"));
                }

                dependencies.AddRange(ReflectionVirtualInvokeMapNode.GetVirtualInvokeMapDependencies(context, _method));

                return dependencies;
            }

            return Array.Empty<DependencyListEntry>();
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
                if (!(potentialOverrideType is DefType))
                    continue;

                Debug.Assert(!potentialOverrideType.IsRuntimeDeterminedSubtype);

                if (_method.OwningType.HasSameTypeDefinition(potentialOverrideType) && potentialOverrideType.IsInterface && (potentialOverrideType != _method.OwningType))
                {
                    if (_method.OwningType.CanCastTo(potentialOverrideType))
                    {
                        // Variance expansion
                        MethodDesc matchingMethodOnRelatedVariantMethod = potentialOverrideType.GetMethod(_method.Name, _method.GetTypicalMethodDefinition().Signature);
                        matchingMethodOnRelatedVariantMethod = _method.Context.GetInstantiatedMethod(matchingMethodOnRelatedVariantMethod, _method.Instantiation);
                        dynamicDependencies.Add(new CombinedDependencyListEntry(factory.GVMDependencies(matchingMethodOnRelatedVariantMethod), null, "GVM Variant Interface dependency"));
                    }
                }

                // If this is an interface gvm, look for types that implement the interface
                // and other instantantiations that have the same canonical form.
                // This ensure the various slot numbers remain equivalent across all types where there is an equivalence
                // relationship in the vtable.
                if (_method.OwningType.IsInterface)
                {
                    if (potentialOverrideType.IsInterface)
                        continue;

                    foreach (DefType interfaceImpl in potentialOverrideType.RuntimeInterfaces)
                    {
                        if (interfaceImpl.ConvertToCanonForm(CanonicalFormKind.Specific) == _method.OwningType.ConvertToCanonForm(CanonicalFormKind.Specific))
                        {
                            // Find if the type implements this method. (Note, do this comparision against the generic definition of the method, not the
                            // specific method instantiation that is "method"
                            MethodDesc genericDefinition = interfaceImpl.GetMethod(_method.Name, _method.GetTypicalMethodDefinition().Signature);
                            MethodDesc slotDecl = potentialOverrideType.ResolveInterfaceMethodTarget(genericDefinition);
                            if (slotDecl != null)
                                CreateDependencyForMethodSlotAndInstantiation(slotDecl, dynamicDependencies, factory);
                        }
                    }
                }
                else
                {
                    // TODO: Ensure GVM Canon Target

                    TypeDesc overrideTypeCanonCur = potentialOverrideType;
                    TypeDesc methodCanonContainingType = _method.OwningType;
                    while (overrideTypeCanonCur != null)
                    {
                        if (overrideTypeCanonCur.ConvertToCanonForm(CanonicalFormKind.Specific) == methodCanonContainingType.ConvertToCanonForm(CanonicalFormKind.Specific))
                        {
                            MethodDesc methodDefInDerivedType = potentialOverrideType.GetMethod(_method.Name, _method.GetTypicalMethodDefinition().Signature);
                            if(methodDefInDerivedType != null)
                                CreateDependencyForMethodSlotAndInstantiation(methodDefInDerivedType, dynamicDependencies, factory);

                            MethodDesc slotDecl = MetadataVirtualMethodAlgorithm.FindSlotDefiningMethodForVirtualMethod(_method);
                            if (slotDecl != null)
                                CreateDependencyForMethodSlotAndInstantiation(slotDecl.GetMethodDefinition(), dynamicDependencies, factory);
                        }

                        overrideTypeCanonCur = overrideTypeCanonCur.BaseType;
                    }
                }
            }
            return dynamicDependencies;
        }

        private void CreateDependencyForMethodSlotAndInstantiation(MethodDesc methodDef, List<CombinedDependencyListEntry> dynamicDependencies, NodeFactory factory)
        {
            Debug.Assert(methodDef != null);
            Debug.Assert(!methodDef.Signature.IsStatic);

            if (methodDef.IsAbstract)
                return;

            MethodDesc derivedMethodInstantiation = _method.Context.GetInstantiatedMethod(methodDef, _method.Instantiation);

            // Universal canonical instantiations should be entirely universal canon
            if (derivedMethodInstantiation.IsCanonicalMethod(CanonicalFormKind.Universal))
            {
                derivedMethodInstantiation = derivedMethodInstantiation.GetCanonMethodTarget(CanonicalFormKind.Universal);
            }

            // TODO: verify for invalid instantiations, like List<void>?
            bool validInstantiation = 
                derivedMethodInstantiation.IsSharedByGenericInstantiations ||       // Non-exact methods are always valid instantiations (always pass constraints check)
                derivedMethodInstantiation.CheckConstraints();                      // Verify that the instantiation does not violate constraints

            if (validInstantiation)
            {
                MethodDesc canonMethodTarget = derivedMethodInstantiation.GetCanonMethodTarget(CanonicalFormKind.Specific);

                bool getUnboxingStub = (derivedMethodInstantiation.OwningType.IsValueType || derivedMethodInstantiation.OwningType.IsEnum);
                dynamicDependencies.Add(new CombinedDependencyListEntry(factory.MethodEntrypoint(canonMethodTarget, getUnboxingStub), null, "DerivedMethodInstantiation"));

                if (canonMethodTarget != derivedMethodInstantiation)
                {
                    // Dependency includes the generic method dictionary of the instantiation, and all its dependencies. This is done by adding the 
                    // ShadowConcreteMethod to the list of dynamic dependencies. The generic dictionary will be reported as a dependency of the ShadowConcreteMethod
                    // TODO: detect large recursive generics and fallback to USG templates
                    Debug.Assert(!derivedMethodInstantiation.IsCanonicalMethod(CanonicalFormKind.Any));
                    dynamicDependencies.Add(new CombinedDependencyListEntry(factory.ShadowConcreteMethod(derivedMethodInstantiation), null, "DerivedMethodInstantiation dictionary"));
                }
            }
            else
            {
                // TODO: universal generics
            }
        }
    }
}
