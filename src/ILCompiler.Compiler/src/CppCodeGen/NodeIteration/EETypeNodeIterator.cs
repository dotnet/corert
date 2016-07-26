using ILCompiler.DependencyAnalysis;
using ILCompiler.DependencyAnalysisFramework;
using Internal.TypeSystem;
using System.Collections.Generic;

namespace ILCompiler.Compiler.CppCodeGen
{
    public class EETypeNodeIterator
    {
        private List<DependencyNode> _typeNodes;
        private Dictionary<TypeDesc, EETypeNode> _typeToNodeMap;

        public EETypeNodeIterator(IEnumerable<DependencyNode> nodes)
        {
            _typeNodes = new List<DependencyNode>();
            _typeToNodeMap = new Dictionary<TypeDesc, EETypeNode>();
            foreach (var node in nodes)
            {
                var typeNode = node as EETypeNode;
                if (typeNode != null)
                {
                    _typeToNodeMap[typeNode.Type] = typeNode;
                }
            }

            foreach (var node in _typeToNodeMap.Values)
            {
                AddNode(node);
            }
        }

        private void AddNode(EETypeNode node)
        {
            if (node != null && !_typeNodes.Contains(node))
            {
                EETypeNode baseTypeNode;
                if (node.Type.BaseType != null)
                {
                    _typeToNodeMap.TryGetValue(node.Type.BaseType, out baseTypeNode);
                    AddNode(baseTypeNode);
                }
                foreach (var field in node.Type.GetFields())
                {
                    EETypeNode fieldNode;
                    _typeToNodeMap.TryGetValue(field.FieldType, out fieldNode);
                    if (fieldNode != null && !_typeNodes.Contains(fieldNode))
                    {
                        if(fieldNode.Type.IsValueType)
                        AddNodeHierarchy(fieldNode);
                    }
                }
                if (!_typeNodes.Contains(node)) this._typeNodes.Add(node);
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
                    if (baseType != null && !_typeNodes.Contains(baseTypeNode))
                        _typeToNodeMap.TryGetValue(baseType, out baseTypeNode);
                    else
                        baseTypeNode = null;
                }
            }
            while (nodeStack.Count > 0)
            {
                var typeNode = nodeStack.Pop();
                if (!_typeNodes.Contains(typeNode))
                    _typeNodes.Add(typeNode);
            }
        }
        public List<DependencyNode> GetNodes()
        {
            return _typeNodes;
        }
    }
}