// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Collections.Generic;
using System.Diagnostics;

using Internal.TypeSystem;
using Internal.Runtime;

namespace ILCompiler.DependencyAnalysis
{
    /// <summary>
    /// Canonical type instantiations are emitted, not because they are used directly by the user code, but because
    /// they are used by the dynamic type loader when dynamically instantiating types at runtime.
    /// The data that we emit on canonical type instantiations should just be the minimum that is needed by the template 
    /// type loader. 
    /// Similarly, the dependencies that we track for canonicl type instantiations are minimal, and are just the ones used
    /// by the dynamic type loader
    /// </summary>
    internal sealed class CanonicalEETypeNode : EETypeNode
    {
        public CanonicalEETypeNode(NodeFactory factory, TypeDesc type) : base(factory, type)
        {
            Debug.Assert(!type.IsCanonicalDefinitionType(CanonicalFormKind.Any));
            Debug.Assert(type.IsCanonicalSubtype(CanonicalFormKind.Any));
            Debug.Assert(type == type.ConvertToCanonForm(CanonicalFormKind.Specific));
            Debug.Assert(!type.IsMdArray);
            Debug.Assert(!type.IsByRefLike);
        }

        public override bool StaticDependenciesAreComputed => true;
        public override bool IsShareable => IsTypeNodeShareable(_type);
        protected override bool EmitVirtualSlotsAndInterfaces => true;
        public override bool ShouldSkipEmittingObjectNode(NodeFactory factory) => false;

        protected override DependencyList ComputeNonRelocationBasedDependencies(NodeFactory factory)
        {
            DependencyList dependencyList = base.ComputeNonRelocationBasedDependencies(factory);

            // Ensure that we track the necessary type symbol if we are working with a constructed type symbol.
            // The emitter will ensure we don't emit both, but this allows us assert that we only generate
            // relocs to nodes we emit.
            dependencyList.Add(factory.NecessaryTypeSymbol(_type), "Necessary type symbol related to CanonicalEETypeNode");

            DefType closestDefType = _type.GetClosestDefType();

            if (_type.RuntimeInterfaces.Length > 0)
                dependencyList.Add(factory.InterfaceDispatchMap(_type), "Canonical interface dispatch map");

            dependencyList.Add(factory.VTable(closestDefType), "VTable");

            if (_type.IsCanonicalSubtype(CanonicalFormKind.Universal))
                dependencyList.Add(factory.NativeLayout.TemplateTypeLayout(_type), "Universal generic types always have template layout");

            // Track generic virtual methods that will get added to the GVM tables
            if (TypeGVMEntriesNode.TypeNeedsGVMTableEntries(_type))
                dependencyList.Add(new DependencyListEntry(factory.TypeGVMEntries(_type), "Type with generic virtual methods"));

            return dependencyList;
        }

        protected override ISymbolNode GetBaseTypeNode(NodeFactory factory)
        {
            return _type.BaseType != null ? factory.NecessaryTypeSymbol(GetFullCanonicalTypeForCanonicalType(_type.BaseType)) : null;
        }

        protected override int GCDescSize
        {
            get
            {
                // No GCDescs for universal canonical types
                if (_type.IsCanonicalSubtype(CanonicalFormKind.Universal))
                    return 0;

                Debug.Assert(_type.IsCanonicalSubtype(CanonicalFormKind.Specific));
                return GCDescEncoder.GetGCDescSize(_type);
            }
        }

        protected override void OutputGCDesc(ref ObjectDataBuilder builder)
        {
            // No GCDescs for universal canonical types
            if (_type.IsCanonicalSubtype(CanonicalFormKind.Universal))
                return;

            Debug.Assert(_type.IsCanonicalSubtype(CanonicalFormKind.Specific));
            GCDescEncoder.EncodeGCDesc(ref builder, _type);
        }

        protected override void OutputInterfaceMap(NodeFactory factory, ref ObjectDataBuilder objData)
        {
            foreach (var itf in _type.RuntimeInterfaces)
            {
                // Interface omitted for canonical instantiations (constructed at runtime for dynamic types from the native layout info)
                objData.EmitZeroPointer();
            }
        }

        protected override void OutputBaseSize(ref ObjectDataBuilder objData)
        {
            bool emitMinimumObjectSize = false;

            if (_type.IsCanonicalSubtype(CanonicalFormKind.Universal) && _type.IsDefType)
            {
                LayoutInt instanceByteCount = ((DefType)_type).InstanceByteCount;

                if (instanceByteCount.IsIndeterminate)
                {
                    // For USG types, they may be of indeterminate size, and the size of the type may be meaningless. 
                    // In that case emit a fixed constant.
                    emitMinimumObjectSize = true;
                }
            }

            if (emitMinimumObjectSize)
                objData.EmitInt(MinimumObjectSize);
            else
                base.OutputBaseSize(ref objData);
        }

        protected override void ComputeValueTypeFieldPadding()
        {
            DefType defType = _type as DefType;

            // Types of indeterminate sizes don't have computed ValueTypeFieldPadding
            if (defType != null && defType.InstanceByteCount.IsIndeterminate)
            {
                Debug.Assert(_type.IsCanonicalSubtype(CanonicalFormKind.Universal));
                return;
            }

            base.ComputeValueTypeFieldPadding();
        }
    }
}
