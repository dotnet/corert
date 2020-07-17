// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using ILCompiler.DependencyAnalysis;
using ILCompiler.DependencyAnalysisFramework;
using Internal.TypeSystem;
using System.Collections.Generic;

namespace ILCompiler.Compiler.CppCodeGen
{
    public class DependencyNodeIterator
    {
        /// <summary>
        /// Iteration through dependency nodes to ensure C++ codegen build order
        /// </summary>
        private List<DependencyNode> _nodes;
        private HashSet<DependencyNode> _visited;
        private Dictionary<TypeDesc, EETypeNode> _typeToNodeMap;
        private NodeFactory _factory;

        public DependencyNodeIterator(IEnumerable<DependencyNode> nodes, NodeFactory factory)
        {
            _nodes = new List<DependencyNode>();
            _typeToNodeMap = new Dictionary<TypeDesc, EETypeNode>();
            _visited = new HashSet<DependencyNode>();
            _factory = factory;
            foreach (var node in nodes)
            {
                if (node is EETypeNode)
                {
                    var typeNode = node as EETypeNode;
                    if (!_typeToNodeMap.ContainsKey(typeNode.Type))
                        _typeToNodeMap[typeNode.Type] = typeNode;
                    else if (typeNode is ConstructedEETypeNode)
                        _typeToNodeMap[typeNode.Type] = typeNode;
                }
                // Assume ordering doesn't matter
                else _nodes.Add(node);
            }

            foreach (var node in _typeToNodeMap.Values)
            {
                AddTypeNode(node);
            }
        }

        private void AddTypeNode(EETypeNode node)
        {
            if (node != null && !_nodes.Contains(node) && !_visited.Contains(node))
            {
                _visited.Add(node);
                EETypeNode baseTypeNode;
                if (node.Type.BaseType != null)
                {
                    TypeDesc baseType = node.Type.BaseType;
                    if (!node.Type.IsGenericDefinition && baseType.IsCanonicalSubtype(CanonicalFormKind.Any))
                        baseType = baseType.ConvertToCanonForm(CanonicalFormKind.Specific);

                    _typeToNodeMap.TryGetValue(baseType, out baseTypeNode);
                    if (!node.Type.IsPrimitive)
                        AddTypeNode(baseTypeNode);
                    else if (!_nodes.Contains(baseTypeNode)) _nodes.Add(baseTypeNode);
                }
                if (!node.Type.IsGenericDefinition)
                {
                    foreach (var field in node.Type.GetFields())
                    {
                        EETypeNode fieldNode;
                        _typeToNodeMap.TryGetValue(field.FieldType, out fieldNode);
                        if (fieldNode == null)
                        {
                            if (field.FieldType.IsValueType)
                                AddTypeNode((EETypeNode)_factory.NecessaryTypeSymbol(field.FieldType));
                        }
                        else
                        {
                            if (fieldNode.Type.IsValueType)
                            {
                                if (!fieldNode.Type.IsPrimitive)
                                    AddTypeNode(fieldNode);
                                else if (!_nodes.Contains(fieldNode))
                                    _nodes.Add(fieldNode);
                            }
                        }
                    }
                }
                if (!_nodes.Contains(node)) _nodes.Add(node);
            }
        }
        public List<DependencyNode> GetNodes()
        {
            return _nodes;
        }
    }
}
