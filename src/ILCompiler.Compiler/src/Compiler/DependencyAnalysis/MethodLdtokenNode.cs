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
    internal class MethodLdtokenNode : ObjectNode, IMethodNode
    {
        private MethodDesc _method;

        public MethodLdtokenNode(MethodDesc method)
        {
            _method = method;
        }

        public MethodDesc Method => _method;

        public void AppendMangledName(NameMangler nameMangler, Utf8StringBuilder sb)
        {
            sb.Append("__method_ldtoken_" + NodeFactory.NameMangler.GetMangledMethodName(_method));
        }

        public int Offset => 0;
        public override bool IsShareable => false;
        public override ObjectNodeSection Section => ObjectNodeSection.DataSection;
        public override bool StaticDependenciesAreComputed => true;
        protected override string GetName() => this.GetMangledName();


        public override bool HasDynamicDependencies
        {
            get
            {
                // Only generic virtual methods work have dynamic dependencies
                if (_method.HasInstantiation && _method.IsVirtual)
                {
                    if (_method.OwningType.IsCanonicalSubtype(CanonicalFormKind.Specific))
                        return false;

                    if (_method.OwningType.IsCanonicalSubtype(CanonicalFormKind.Universal) &&
                        _method.OwningType != _method.OwningType.ConvertToCanonForm(CanonicalFormKind.Universal))
                        return false;

                    if (_method.OwningType.IsRuntimeDeterminedSubtype)
                        return false;

                    if (_method.OwningType.ContainsGenericVariables)
                        return false;

                    // Check to see if the method instantiation has shared runtime components, or canon components. Those are not actually added to the various vtables, so 
                    // are not examined for dynamic dependencies
                    foreach (var type in _method.Instantiation)
                    {
                        if (type.IsRuntimeDeterminedSubtype || type.IsCanonicalSubtype(CanonicalFormKind.Specific) || type.IsGenericParameter)
                            return false;
                    }
                    return true;
                }
                return false;
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
                if (!(potentialOverrideType is DefType))
                    continue;

                if (_method.OwningType.HasSameTypeDefinition(potentialOverrideType) && potentialOverrideType.IsInterface && (potentialOverrideType != _method.OwningType))
                {
                    if (_method.OwningType.CanCastTo(potentialOverrideType))
                    {
                        // Variance expansion
                        MethodDesc matchingMethodOnRelatedVariantMethod = potentialOverrideType.GetMethod(_method.Name, _method.GetTypicalMethodDefinition().Signature);
                        matchingMethodOnRelatedVariantMethod = _method.Context.GetInstantiatedMethod(matchingMethodOnRelatedVariantMethod, _method.Instantiation);
                        dynamicDependencies.Add(new CombinedDependencyListEntry(factory.MethodLdtoken(matchingMethodOnRelatedVariantMethod), null, "GVM Variant Interface dependency"));
                    }
                }

                // Open generic types are not interesting.
                if (potentialOverrideType.ContainsGenericVariables)
                    continue;

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
                            MethodDesc slotDecl = potentialOverrideType.ResolveInterfaceMethodToVirtualMethodOnType(genericDefinition);
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

                            MethodDesc slotDecl = MetadataVirtualMethodAlgorithm.FindSlotDefiningMethodForVirtualMethod(_method.GetTypicalMethodDefinition());
                            CreateDependencyForMethodSlotAndInstantiation(slotDecl, dynamicDependencies, factory);
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

            MethodDesc derivedMethodInstantiation = _method.Context.GetInstantiatedMethod(methodDef, _method.Instantiation);

            // Universal canonical instantiations should be entirely universal canon
            if (derivedMethodInstantiation.IsCanonicalMethod(CanonicalFormKind.Universal))
            {
                derivedMethodInstantiation = derivedMethodInstantiation.GetCanonMethodTarget(CanonicalFormKind.Universal);
            }

            if (true /* TODO: derivedMethodInstantiation.CheckConstraints()*/)
            {
                bool getUnboxingStub = (derivedMethodInstantiation.OwningType.IsValueType || derivedMethodInstantiation.OwningType.IsEnum);
                dynamicDependencies.Add(new CombinedDependencyListEntry(factory.MethodEntrypoint(derivedMethodInstantiation, getUnboxingStub), null, "DerivedMethodInstantiation"));
            }
            else
            {
                // TODO
            }
        }

        public override ObjectData GetData(NodeFactory factory, bool relocsOnly)
        {
            // This node does not produce any data
            return new ObjectData(Array.Empty<byte>(), Array.Empty<Relocation>(), 1, Array.Empty<ISymbolNode>());
        }
    }
}
