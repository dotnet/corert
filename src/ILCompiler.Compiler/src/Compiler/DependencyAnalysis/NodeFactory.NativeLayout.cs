// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using Internal.TypeSystem;

namespace ILCompiler.DependencyAnalysis
{
    /// Part of Node factory that deals with nodes describing native layout information
    partial class NodeFactory
    {
        /// <summary>
        /// Helper class that provides a level of grouping for all the native layout lookups
        /// </summary>
        public class NativeLayoutHelper
        {
            NodeFactory _factory;

            public NativeLayoutHelper(NodeFactory factory)
            {
                _factory = factory;
                CreateNodeCaches();
            }

            private void CreateNodeCaches()
            {
                _typeSignatures = new NodeCache<TypeDesc, NativeLayoutTypeSignatureVertexNode>(type =>
                {
                     return NativeLayoutTypeSignatureVertexNode.NewTypeSignatureVertexNode(_factory, type);
                });

                _methodSignatures = new NodeCache<MethodDesc, NativeLayoutMethodSignatureVertexNode>(method =>
                {
                    return new NativeLayoutMethodSignatureVertexNode(_factory, method);
                });

                _methodNameAndSignatures = new NodeCache<MethodDesc, NativeLayoutMethodNameAndSignatureVertexNode>(method =>
                {
                    return new NativeLayoutMethodNameAndSignatureVertexNode(_factory, method);
                });

                _placedSignatures = new NodeCache<NativeLayoutVertexNode, NativeLayoutPlacedSignatureVertexNode>(vertexNode =>
                {
                    return new NativeLayoutPlacedSignatureVertexNode(vertexNode);
                });

                _methodLdTokenSignatures = new NodeCache<MethodDesc, NativeLayoutMethodLdTokenVertexNode>(method =>
                {
                    return new NativeLayoutMethodLdTokenVertexNode(_factory, method);
                });

                _fieldLdTokenSignatures = new NodeCache<FieldDesc, NativeLayoutFieldLdTokenVertexNode>(field =>
                {
                    return new NativeLayoutFieldLdTokenVertexNode(_factory, field);
                });

                _nativeLayoutSignatureNodes = new NodeCache<NativeLayoutSavedVertexNode, NativeLayoutSignatureNode>(signature =>
                {
                    return new NativeLayoutSignatureNode(signature);
                });

                _templateMethodEntries = new NodeCache<MethodDesc, NativeLayoutTemplateMethodSignatureVertexNode>(method =>
                {
                    return new NativeLayoutTemplateMethodSignatureVertexNode(_factory, method);
                });

                _templateMethodLayouts = new NodeCache<MethodDesc, NativeLayoutTemplateMethodLayoutVertexNode>(method =>
                {
                    return new NativeLayoutTemplateMethodLayoutVertexNode(_factory, method);
                });
            }

            private NodeCache<TypeDesc, NativeLayoutTypeSignatureVertexNode> _typeSignatures;
            internal NativeLayoutTypeSignatureVertexNode TypeSignatureVertex(TypeDesc type)
            {
                return _typeSignatures.GetOrAdd(type);
            }

            private NodeCache<MethodDesc, NativeLayoutMethodSignatureVertexNode> _methodSignatures;
            internal NativeLayoutMethodSignatureVertexNode MethodSignatureVertex(MethodDesc method)
            {
                return _methodSignatures.GetOrAdd(method);
            }

            private NodeCache<MethodDesc, NativeLayoutMethodNameAndSignatureVertexNode> _methodNameAndSignatures;
            internal NativeLayoutMethodNameAndSignatureVertexNode MethodNameAndSignatureVertex(MethodDesc method)
            {
                return _methodNameAndSignatures.GetOrAdd(method);
            }

            private NodeCache<NativeLayoutVertexNode, NativeLayoutPlacedSignatureVertexNode> _placedSignatures;
            internal NativeLayoutPlacedSignatureVertexNode PlacedSignatureVertex(NativeLayoutVertexNode vertexNode)
            {
                return _placedSignatures.GetOrAdd(vertexNode);
            }

            private NodeCache<MethodDesc, NativeLayoutMethodLdTokenVertexNode> _methodLdTokenSignatures;
            internal NativeLayoutMethodLdTokenVertexNode MethodLdTokenVertex(MethodDesc method)
            {
                return _methodLdTokenSignatures.GetOrAdd(method);
            }

            private NodeCache<FieldDesc, NativeLayoutFieldLdTokenVertexNode> _fieldLdTokenSignatures;
            internal NativeLayoutFieldLdTokenVertexNode FieldLdTokenVertex(FieldDesc field)
            {
                return _fieldLdTokenSignatures.GetOrAdd(field);
            }

            private NodeCache<NativeLayoutSavedVertexNode, NativeLayoutSignatureNode> _nativeLayoutSignatureNodes;
            internal NativeLayoutSignatureNode NativeLayoutSignature(NativeLayoutSavedVertexNode signature)
            {
                return _nativeLayoutSignatureNodes.GetOrAdd(signature);
            }

            private NodeCache<MethodDesc, NativeLayoutTemplateMethodSignatureVertexNode> _templateMethodEntries;
            internal NativeLayoutTemplateMethodSignatureVertexNode TemplateMethodEntry(MethodDesc method)
            {
                return _templateMethodEntries.GetOrAdd(method);
            }

            private NodeCache<MethodDesc, NativeLayoutTemplateMethodLayoutVertexNode> _templateMethodLayouts;
            internal NativeLayoutTemplateMethodLayoutVertexNode TemplateMethodLayout(MethodDesc method)
            {
                return _templateMethodLayouts.GetOrAdd(method);
            }
        }

        public NativeLayoutHelper NativeLayout;
    }
}