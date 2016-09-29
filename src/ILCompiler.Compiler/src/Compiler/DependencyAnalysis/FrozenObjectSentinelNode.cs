// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Collections.Generic;

namespace ILCompiler.DependencyAnalysis
{
    /// <summary>
    /// A sentinel node used to match the expected encoding of an array of frozen objects in the runtime.
    /// An extra pointer is expected by the GC at the end for correctness.
    /// </summary>
    class FrozenObjectSentinelNode : EmbeddedObjectNode
    {
        public override bool StaticDependenciesAreComputed
        {
            get
            {
                return true;
            }
        }

        public override void EncodeData(ref ObjectDataBuilder dataBuilder, NodeFactory factory, bool relocsOnly)
        {
            dataBuilder.EmitZeroPointer();
        }

        protected override string GetName()
        {
            return "FrozenObjectSentinelNode";
        }

        public override IEnumerable<DependencyListEntry> GetStaticDependencies(NodeFactory context)
        {
            return null;
        }
    }
}
