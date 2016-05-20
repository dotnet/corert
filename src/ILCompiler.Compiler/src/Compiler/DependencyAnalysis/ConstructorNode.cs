// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System.Collections.Generic;

using Internal.TypeSystem;
using ILCompiler.DependencyAnalysisFramework;

using Debug = System.Diagnostics.Debug;

namespace ILCompiler.DependencyAnalysis
{
    /// <summary>
    /// Represents a constructor. This node does not generate <see cref="ObjectNode.ObjectData"/>. Instead, this node
    /// maps to a regular entrypoint, but on top of that it triggers necessity of a constructed EEType of the
    /// type being constructed.
    /// </summary>
    public sealed class ConstructorNode : DependencyNodeCore<NodeFactory>, IMethodNode
    {
        private IMethodNode _constructorNode;

        public string MangledName
        {
            get
            {
                return _constructorNode.MangledName;
            }
        }

        public MethodDesc Method
        {
            get
            {
                return _constructorNode.Method;
            }
        }

        public int Offset
        {
            get
            {
                return _constructorNode.Offset;
            }
        }

        public override bool InterestingForDynamicDependencyAnalysis
        {
            get
            {
                return false;
            }
        }

        public override bool HasDynamicDependencies
        {
            get
            {
                return false;
            }
        }

        public override bool HasConditionalStaticDependencies
        {
            get
            {
                return false;
            }
        }

        public override bool StaticDependenciesAreComputed
        {
            get
            {
                return true;
            }
        }

        public ConstructorNode(IMethodNode constructorNode)
        {
            // Assert that this is either an actual constructor, or a static method that returns an instance
            // of its owning type (a "magic" constructor such as one of the String.Ctor methods).
            Debug.Assert(constructorNode.Method.IsConstructor ||
                (constructorNode.Method.Signature.IsStatic && constructorNode.Method.Signature.ReturnType == constructorNode.Method.OwningType));

            Debug.Assert(!(constructorNode is ConstructorNode));

            _constructorNode = constructorNode;
        }

        public override IEnumerable<DependencyListEntry> GetStaticDependencies(NodeFactory context)
        {
            yield return new DependencyListEntry(context.ConstructedTypeSymbol(_constructorNode.Method.OwningType), "Constructor call construction");
            yield return new DependencyListEntry(_constructorNode, "Constructor");
        }

        public override IEnumerable<CombinedDependencyListEntry> GetConditionalStaticDependencies(NodeFactory context)
        {
            return null;
        }

        public override IEnumerable<CombinedDependencyListEntry> SearchDynamicDependencies(List<DependencyNodeCore<NodeFactory>> markedNodes, int firstNode, NodeFactory context)
        {
            return null;
        }

        public override string GetName()
        {
            return MangledName + " constructor";
        }
    }
}
