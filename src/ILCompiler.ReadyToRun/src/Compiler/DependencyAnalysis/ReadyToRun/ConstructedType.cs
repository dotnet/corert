// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Collections.Generic;
using System.Diagnostics;

using Internal.Runtime;
using Internal.TypeSystem;
using Internal.IL;
using Internal.Text;

using ILCompiler.DependencyAnalysisFramework;

namespace ILCompiler.DependencyAnalysis.ReadyToRun
{
    public class ConstructedType : DependencyNodeCore<NodeFactory>, IEETypeNode
    {
        TypeDesc _type;

        public ConstructedType(NodeFactory factory, TypeDesc type)
        {
            _type = type;
            Debug.Assert(!type.IsCanonicalDefinitionType(CanonicalFormKind.Any));
            CheckCanGenerateConstructedEEType(factory, type);
        }

        void ISymbolNode.AppendMangledName(NameMangler nameMangler, Utf8StringBuilder sb)
        {
            sb.Append("ConstructedType: ");
            sb.Append(_type.ToString());
        }

        protected override string GetName(NodeFactory factory) => this.GetMangledName(factory.NameMangler) + " constructed";

        public int Offset => 0;

        public bool RepresentsIndirectionCell => false;

        public override bool StaticDependenciesAreComputed => true;

        public TypeDesc Type => _type;

        public override bool InterestingForDynamicDependencyAnalysis
        {
            get
            {
                if (_type.IsInterface)
                    return _type.HasGenericVirtualMethods();

                if (_type.IsDefType)
                {
                    // First, check if this type has any GVM that overrides a GVM on a parent type. If that's the case, this makes
                    // the current type interesting for GVM analysis (i.e. instantiate its overriding GVMs for existing GVMDependenciesNodes
                    // of the instantiated GVM on the parent types).
                    foreach (var method in _type.GetAllMethods())
                    {
                        if (method.HasInstantiation && method.IsVirtual)
                        {
                            MethodDesc slotDecl = MetadataVirtualMethodAlgorithm.FindSlotDefiningMethodForVirtualMethod(method);
                            if (slotDecl != method)
                                return true;
                        }
                    }

                    // Second, check if this type has any GVMs that implement any GVM on any of the implemented interfaces. This would
                    // make the current type interesting for dynamic dependency analysis to that we can instantiate its GVMs.
                    foreach (DefType interfaceImpl in _type.RuntimeInterfaces)
                    {
                        foreach (var method in interfaceImpl.GetAllMethods())
                        {
                            if (method.HasInstantiation && method.IsVirtual)
                            {
                                // We found a GVM on one of the implemented interfaces. Find if the type implements this method. 
                                // (Note, do this comparision against the generic definition of the method, not the specific method instantiation
                                MethodDesc genericDefinition = method.GetMethodDefinition();
                                MethodDesc slotDecl = _type.ResolveInterfaceMethodTarget(genericDefinition);
                                if (slotDecl != null)
                                    return true;
                            }
                        }
                    }
                }
                return false;
            }
        }

        public override IEnumerable<DependencyListEntry> GetStaticDependencies(NodeFactory factory)
        {
            DependencyList dependencyList = new DependencyList();

            dependencyList.Add(new DependencyListEntry(factory.NecessaryTypeSymbol(_type), "Necessary type"));
            if (_type.BaseType != null)
            {
                dependencyList.Add(new DependencyListEntry(factory.NecessaryTypeSymbol(_type.BaseType), "Base type"));
            }
            if (_type.RuntimeInterfaces != null)
            {
                foreach (DefType definedInterface in _type.RuntimeInterfaces)
                {
                    dependencyList.Add(new DependencyListEntry(factory.NecessaryTypeSymbol(definedInterface), "Defined interface"));
                }
            }
            foreach (TypeDesc typeArg in _type.Instantiation)
            {
                dependencyList.Add(new DependencyListEntry(factory.NecessaryTypeSymbol(typeArg), "Instantiation argument"));
            }

            return (IEnumerable<DependencyListEntry>) dependencyList;
        }

        public static bool CreationAllowed(TypeDesc type)
        {
            switch (type.Category)
            {
                case TypeFlags.Pointer:
                case TypeFlags.FunctionPointer:
                case TypeFlags.ByRef:
                    // Pointers and byrefs are not boxable
                    return false;
                case TypeFlags.Array:
                case TypeFlags.SzArray:
                    // TODO: any validation for arrays?
                    break;

                default:
                    // Generic definition EETypes can't be allocated
                    if (type.IsGenericDefinition)
                        return false;

                    // Full EEType of System.Canon should never be used.
                    if (type.IsCanonicalDefinitionType(CanonicalFormKind.Any))
                        return false;

                    // The global "<Module>" type can never be allocated.
                    if (((MetadataType)type).IsModuleType)
                        return false;

                    break;
            }

            return true;
        }

        public static void CheckCanGenerateConstructedEEType(NodeFactory factory, TypeDesc type)
        {
            if (!CreationAllowed(type))
                ThrowHelper.ThrowTypeLoadException(ExceptionStringID.ClassLoadGeneral, type);
        }

        public override bool HasDynamicDependencies => false;
        public override bool HasConditionalStaticDependencies => false;
        public override IEnumerable<CombinedDependencyListEntry> GetConditionalStaticDependencies(NodeFactory context) => null;
        public override IEnumerable<CombinedDependencyListEntry> SearchDynamicDependencies(List<DependencyNodeCore<NodeFactory>> markedNodes, int firstNode, NodeFactory context) => null;
        int ISortableNode.CompareToImpl(ISortableNode other, CompilerComparer comparer) => throw new NotImplementedException();

        int ISortableNode.ClassCode => 381945542;
    }
}
