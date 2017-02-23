// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Collections.Generic;
using System.Diagnostics;

using Internal.TypeSystem;

namespace ILCompiler.DependencyAnalysis
{
    internal sealed class CanonicalDefinitionEETypeNode : EETypeNode
    {
        public CanonicalDefinitionEETypeNode(NodeFactory factory, TypeDesc type) : base(factory, type)
        {
            Debug.Assert(type.IsCanonicalDefinitionType(CanonicalFormKind.Any));
        }

        public override bool ShouldSkipEmittingObjectNode(NodeFactory factory) => false;
        public override bool StaticDependenciesAreComputed => true;
        public override bool IsShareable => true;
        protected override DependencyList ComputeNonRelocationBasedDependencies(NodeFactory factory) => null;
        protected override int GCDescSize => 0;

        protected internal override void ComputeOptionalEETypeFields(NodeFactory factory, bool relocsOnly)
        {
            // TODO: handle the __UniversalCanon case (valuetype padding optional field...)
            Debug.Assert(_type.IsCanonicalDefinitionType(CanonicalFormKind.Specific));
        }

        protected override void OutputBaseSize(ref ObjectDataBuilder objData)
        {
            // Canonical definition types will have their base size set to the minimum
            objData.EmitInt(MinimumObjectSize);
        }
    }
}
