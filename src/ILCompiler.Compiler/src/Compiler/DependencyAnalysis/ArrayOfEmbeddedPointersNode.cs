// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Collections.Generic;

namespace ILCompiler.DependencyAnalysis
{
    /// <summary>
    /// Represents an array of pointers to symbols. <typeparamref name="TTarget"/> is the type
    /// of node each pointer within the vector points to.
    /// </summary>
    public sealed class ArrayOfEmbeddedPointersNode<TTarget> : ArrayOfEmbeddedDataNode<EmbeddedPointerIndirectionNode<TTarget>>
        where TTarget : ISymbolNode
    {
        private int _nextId;
        private string _startSymbolMangledName;

        public ArrayOfEmbeddedPointersNode(string startSymbolMangledName, string endSymbolMangledName, IComparer<TTarget> nodeSorter)
            : base(
                  startSymbolMangledName,
                  endSymbolMangledName,
                  nodeSorter != null ? new PointerIndirectionNodeComparer(nodeSorter) : null)
        {
            _startSymbolMangledName = startSymbolMangledName;
        }

        public EmbeddedObjectNode NewNode(TTarget target)
        {
            return new SimpleEmbeddedPointerIndirectionNode(this, target);
        }

        public EmbeddedObjectNode NewNodeWithSymbol(TTarget target)
        {
            int id = System.Threading.Interlocked.Increment(ref _nextId);
            return new EmbeddedPointerIndirectionWithSymbolNode(this, target, id);
        }

        private class PointerIndirectionNodeComparer : IComparer<EmbeddedPointerIndirectionNode<TTarget>>
        {
            private IComparer<TTarget> _innerComparer;

            public PointerIndirectionNodeComparer(IComparer<TTarget> innerComparer)
            {
                _innerComparer = innerComparer;
            }

            public int Compare(EmbeddedPointerIndirectionNode<TTarget> x, EmbeddedPointerIndirectionNode<TTarget> y)
            {
                return _innerComparer.Compare(x.Target, y.Target);
            }
        }

        private class SimpleEmbeddedPointerIndirectionNode : EmbeddedPointerIndirectionNode<TTarget>
        {
            protected ArrayOfEmbeddedPointersNode<TTarget> _parentNode;

            public SimpleEmbeddedPointerIndirectionNode(ArrayOfEmbeddedPointersNode<TTarget> futureParent, TTarget target)
                : base(target)
            {
                _parentNode = futureParent;
            }

            public override string GetName()
            {
                return "Embedded pointer to " + Target.MangledName;
            }

            protected override void OnMarked(NodeFactory context)
            {
                // We don't want the child in the parent collection unless it's necessary.
                // Only when this node gets marked, the parent node becomes the actual parent.
                _parentNode.AddEmbeddedObject(this);
            }

            public override IEnumerable<DependencyListEntry> GetStaticDependencies(NodeFactory context)
            {
                return new[]
                {
                    new DependencyListEntry(Target, "reloc"),
                    new DependencyListEntry(_parentNode, "Pointer region")
                };
            }
        }

        private class EmbeddedPointerIndirectionWithSymbolNode : SimpleEmbeddedPointerIndirectionNode, ISymbolNode
        {
            private int _id;

            public EmbeddedPointerIndirectionWithSymbolNode(ArrayOfEmbeddedPointersNode<TTarget> futureParent, TTarget target, int id)
                : base(futureParent, target)
            {
                _id = id;
            }

            public string MangledName
            {
                get
                {
                    return String.Concat(_parentNode._startSymbolMangledName, "_", _id.ToStringInvariant());
                }
            }
        }
    }
}
