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

            // TODO: needs a closer look when we enable USG
            Debug.Assert(!type.IsCanonicalSubtype(CanonicalFormKind.Universal));
        }

        public override bool StaticDependenciesAreComputed => true;
        public override bool IsShareable => IsTypeNodeShareable(_type);
        public override bool HasConditionalStaticDependencies => false;
        protected override bool EmitVirtualSlotsAndInterfaces => true;
        public override bool ShouldSkipEmittingObjectNode(NodeFactory factory) => false;

        protected override DependencyList ComputeNonRelocationBasedDependencies(NodeFactory factory)
        {
            DependencyList dependencyList = base.ComputeNonRelocationBasedDependencies(factory);

            DefType closestDefType = _type.GetClosestDefType();

            if (_type.RuntimeInterfaces.Length > 0)
                dependencyList.Add(factory.InterfaceDispatchMap(_type), "Canonical interface dispatch map");

            dependencyList.Add(factory.VTable(_type), "VTable");

            // TODO: native layout dependencies (template type entries)

            // TODO: other dependencies needed by the dynamic type loader?

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
    }
}