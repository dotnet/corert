// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Collections.Generic;

namespace ILCompiler.DependencyAnalysis
{
    /// <summary>
    /// An <see cref="EmbeddedObjectNode"/> whose sole value is a pointer to a different <see cref="ISymbolNode"/>.
    /// <typeparamref name="TTarget"/> represents the node type this pointer points to.
    /// </summary>
    public abstract class EmbeddedPointerIndirectionNode<TTarget> : EmbeddedObjectNode
        where TTarget : ISymbolNode
    {
        private TTarget _targetNode;

        /// <summary>
        /// Target symbol this node points to.
        /// </summary>
        public TTarget Target
        {
            get
            {
                return _targetNode;
            }
        }

        internal EmbeddedPointerIndirectionNode(TTarget target)
        {
            _targetNode = target;
        }

        public override bool StaticDependenciesAreComputed
        {
            get
            {
                return true;
            }
        }

        public override void EncodeData(ref ObjectDataBuilder dataBuilder, NodeFactory factory, bool relocsOnly)
        {
            dataBuilder.RequirePointerAlignment();
            dataBuilder.EmitPointerReloc(Target);
        }

        // At minimum, Target needs to be reported as a static dependency by inheritors.
        public abstract override IEnumerable<DependencyListEntry> GetStaticDependencies(NodeFactory factory);
    }
}
