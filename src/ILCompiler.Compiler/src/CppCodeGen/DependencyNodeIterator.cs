﻿// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.
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

        public DependencyNodeIterator(IEnumerable<DependencyNode> nodes)
        {
            _nodes = new List<DependencyNode>();
            _typeToNodeMap = new Dictionary<TypeDesc, EETypeNode>();
            _visited = new HashSet<DependencyNode>();
            foreach (var node in nodes)
            {
                if (node is EETypeNode)
                {
                    var typeNode = node as EETypeNode;
                    if (typeNode != null)
                    {
                        if (!_typeToNodeMap.ContainsKey(typeNode.Type))
                            _typeToNodeMap[typeNode.Type] = typeNode;
                        else if (typeNode.Constructed)
                            _typeToNodeMap[typeNode.Type] = typeNode;
                    }
                }
                // Assume ordering doesn't matter
                else if (node is CppMethodCodeNode) _nodes.Add(node);
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
                    _typeToNodeMap.TryGetValue(node.Type.BaseType, out baseTypeNode);
                    if (!node.Type.IsPrimitive)
                        AddTypeNode(baseTypeNode);
                    else if (!_nodes.Contains(baseTypeNode)) _nodes.Add(baseTypeNode);

                }
                foreach (var field in node.Type.GetFields())
                {
                    EETypeNode fieldNode;
                    _typeToNodeMap.TryGetValue(field.FieldType, out fieldNode);
                    if (fieldNode != null)
                    {
                        if (fieldNode.Type.IsValueType)
                        {
                            if (!fieldNode.Type.IsPrimitive)
                                AddTypeNode(fieldNode);
                            else if (!_nodes.Contains(fieldNode)) _nodes.Add(fieldNode);
                        }
                    }
                }
                if (!_nodes.Contains(node)) this._nodes.Add(node);
            }

        }
        private void AddNodeHierarchy(EETypeNode node)
        {
            Stack<EETypeNode> nodeStack = new Stack<EETypeNode>();
            EETypeNode baseTypeNode;
            nodeStack.Push(node);
            if (node.Type.BaseType != null)
            {
                _typeToNodeMap.TryGetValue(node.Type.BaseType, out baseTypeNode);

                while (baseTypeNode != null)
                {
                    nodeStack.Push(baseTypeNode);
                    var baseType = baseTypeNode.Type.BaseType;
                    if (baseType != null && !_nodes.Contains(baseTypeNode))
                        _typeToNodeMap.TryGetValue(baseType, out baseTypeNode);
                    else
                        baseTypeNode = null;
                }
            }
            while (nodeStack.Count > 0)
            {
                var typeNode = nodeStack.Pop();
                if (!_nodes.Contains(typeNode))
                    _nodes.Add(typeNode);
            }
        }
        public List<DependencyNode> GetNodes()
        {
            return _nodes;
        }
    }
}