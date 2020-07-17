// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using System.Collections.Generic;
using System.Diagnostics;

using Internal.TypeSystem;
using ILCompiler.DependencyAnalysisFramework;

namespace ILCompiler.DependencyAnalysis
{
    /// <summary>
    /// This node is used for GVM dependency analysis and GVM tables building. Given an input
    /// type, this node can scan the type for a list of GVM table entries, and compute their dependencies.
    /// </summary>
    internal sealed class TypeGVMEntriesNode : DependencyNodeCore<NodeFactory>
    {
        internal class TypeGVMEntryInfo
        {
            public TypeGVMEntryInfo(MethodDesc callingMethod, MethodDesc implementationMethod, TypeDesc implementationType)
            {
                CallingMethod = callingMethod;
                ImplementationMethod = implementationMethod;
                ImplementationType = implementationType;
            }
            public MethodDesc CallingMethod { get; private set; }
            public MethodDesc ImplementationMethod { get; private set; }
            public TypeDesc ImplementationType { get; private set; }
        }
         
        private TypeDesc _associatedType;
        private DependencyList _staticDependencies;

        public TypeGVMEntriesNode(TypeDesc associatedType)
        {
            Debug.Assert(!associatedType.IsRuntimeDeterminedSubtype);
            Debug.Assert(TypeNeedsGVMTableEntries(associatedType));
            _associatedType = associatedType;
        }

        public override bool HasConditionalStaticDependencies => false;
        public override bool HasDynamicDependencies => false;
        public override bool InterestingForDynamicDependencyAnalysis => false;
        public override bool StaticDependenciesAreComputed => true;
        protected override string GetName(NodeFactory factory) => "__TypeGVMEntriesNode_" + factory.NameMangler.GetMangledTypeName(_associatedType);
        public override IEnumerable<CombinedDependencyListEntry> GetConditionalStaticDependencies(NodeFactory context)
        {
            return Array.Empty<CombinedDependencyListEntry>();
        }
        public override IEnumerable<CombinedDependencyListEntry> SearchDynamicDependencies(List<DependencyNodeCore<NodeFactory>> markedNodes, int firstNode, NodeFactory context)
        {
            return Array.Empty<CombinedDependencyListEntry>();
        }

        public override IEnumerable<DependencyListEntry> GetStaticDependencies(NodeFactory context)
        {
            if (_staticDependencies == null)
            {
                _staticDependencies = new DependencyList();

                foreach(var entry in ScanForGenericVirtualMethodEntries())
                    GenericVirtualMethodTableNode.GetGenericVirtualMethodImplementationDependencies(ref _staticDependencies, context, entry.CallingMethod, entry.ImplementationMethod);

                foreach (var entry in ScanForInterfaceGenericVirtualMethodEntries())
                    InterfaceGenericVirtualMethodTableNode.GetGenericVirtualMethodImplementationDependencies(ref _staticDependencies, context, entry.CallingMethod, entry.ImplementationType, entry.ImplementationMethod);
            }

            return _staticDependencies;
        }

        public static bool TypeNeedsGVMTableEntries(TypeDesc type)
        {
            // Only non-interface deftypes can have entries for their GVMs in the GVM hashtables.
            // Interface GVM entries are computed for types that implemenent the interface (not for the interface on its own)
            if(!type.IsDefType || type.IsInterface)
                return false;

            // Type declares GVMs
            if (type.HasGenericVirtualMethods())
                return true;

            //
            // Check if the type implements any interface with GVM methods, where the method implementations could be on 
            // base types.
            // Example:
            //      interface IFace {
            //          void IFaceGVMethod<U>();
            //      }
            //      class BaseClass {
            //          public virtual void IFaceGVMethod<U>() { ... }
            //      }
            //      public class DerivedClass : BaseClass, IFace { }
            //
            foreach (var iface in type.RuntimeInterfaces)
            {
                foreach (var method in iface.GetMethods())
                {
                    if (!method.HasInstantiation || method.Signature.IsStatic)
                        continue;

                    MethodDesc slotDecl = type.ResolveInterfaceMethodTarget(method);
                    if (slotDecl != null)
                        return true;
                }
            }

            return false;
        }

        public IEnumerable<TypeGVMEntryInfo> ScanForGenericVirtualMethodEntries()
        {
            foreach (MethodDesc decl in _associatedType.EnumAllVirtualSlots())
            {
                // Non-Generic virtual methods are tracked by an orthogonal mechanism.
                if (!decl.HasInstantiation)
                    continue;

                MethodDesc impl = _associatedType.FindVirtualFunctionTargetMethodOnObjectType(decl);

                if (impl.OwningType == _associatedType)
                    yield return new TypeGVMEntryInfo(decl, impl, null);
            }
        }

        public IEnumerable<TypeGVMEntryInfo> ScanForInterfaceGenericVirtualMethodEntries()
        {
            foreach (var iface in _associatedType.RuntimeInterfaces)
            {
                foreach (var method in iface.GetMethods())
                {
                    if (!method.HasInstantiation || method.Signature.IsStatic)
                        continue;

                    MethodDesc slotDecl = _associatedType.ResolveInterfaceMethodTarget(method);
                    if (slotDecl != null)
                        yield return new TypeGVMEntryInfo(method, slotDecl, _associatedType);
                }
            }
        }
    }
}
