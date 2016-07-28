using ILCompiler.DependencyAnalysis;
using ILCompiler.DependencyAnalysisFramework;
using Internal.TypeSystem;
using System.Collections.Generic;

namespace ILCompiler.Compiler.CppCodeGen
{
    public class DependencyNodeIterator
    {
        private List<DependencyNode> _nodes;
        private Dictionary<TypeDesc, EETypeNode> _typeToNodeMap;

        public DependencyNodeIterator(IEnumerable<DependencyNode> nodes)
        {
            _nodes = new List<DependencyNode>();
            _typeToNodeMap = new Dictionary<TypeDesc, EETypeNode>();
            foreach (var node in nodes)
            {
                if (node is EETypeNode)
                {
                    var typeNode = node as EETypeNode;
                    if (typeNode != null)
                    {
                        _typeToNodeMap[typeNode.Type] = typeNode;
                    }
                }
                // Assume ordering doesn't matter
                else if(node is CppMethodCodeNode) _nodes.Add(node);
            }

            foreach (var node in _typeToNodeMap.Values)
            {
                AddTypeNode(node);
            }
        }

        private void AddTypeNode(EETypeNode node)
        {
            if (node != null && !_nodes.Contains(node))
            {
                EETypeNode baseTypeNode;
                if (node.Type.BaseType != null)
                {
                    _typeToNodeMap.TryGetValue(node.Type.BaseType, out baseTypeNode);
                    AddTypeNode(baseTypeNode);
                }
                foreach (var field in node.Type.GetFields())
                {
                    EETypeNode fieldNode;
                    _typeToNodeMap.TryGetValue(field.FieldType, out fieldNode);
                    if (fieldNode != null && !_nodes.Contains(fieldNode))
                    {
                        if(fieldNode.Type.IsValueType)
                        AddNodeHierarchy(fieldNode);
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