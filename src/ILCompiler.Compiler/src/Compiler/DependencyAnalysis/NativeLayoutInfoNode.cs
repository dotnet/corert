// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Collections.Generic;

using Internal.Text;
using Internal.NativeFormat;
using Internal.TypeSystem;

namespace ILCompiler.DependencyAnalysis
{
    /// <summary>
    /// Native layout info blob.
    /// </summary>
    public sealed class NativeLayoutInfoNode : ObjectNode, ISymbolDefinitionNode
    {
        private ObjectAndOffsetSymbolNode _endSymbol;
        private ExternalReferencesTableNode _externalReferences;
        private ExternalReferencesTableNode _staticsReferences;

        private NativeWriter _writer;
        private byte[] _writerSavedBytes;

        private Section _signaturesSection;
        private Section _ldTokenInfoSection;
        private Section _templatesSection;

        private List<NativeLayoutVertexNode> _vertexNodesToWrite;

        public NativeLayoutInfoNode(ExternalReferencesTableNode externalReferences, ExternalReferencesTableNode staticsReferences)
        {
            _endSymbol = new ObjectAndOffsetSymbolNode(this, 0, "__nativelayoutinfo_End", true);
            _externalReferences = externalReferences;
            _staticsReferences = staticsReferences;

            _writer = new NativeWriter();
            _signaturesSection = _writer.NewSection();
            _ldTokenInfoSection = _writer.NewSection();
            _templatesSection = _writer.NewSection();

            _vertexNodesToWrite = new List<NativeLayoutVertexNode>();
        }

        public void AppendMangledName(NameMangler nameMangler, Utf8StringBuilder sb)
        {
            sb.Append(nameMangler.CompilationUnitPrefix).Append("__nativelayoutinfo");
        }
        public ISymbolNode EndSymbol => _endSymbol;
        public int Offset => 0;
        public override bool IsShareable => false;
        public override ObjectNodeSection Section => _externalReferences.Section;
        public override bool StaticDependenciesAreComputed => true;
        protected override string GetName(NodeFactory factory) => this.GetMangledName(factory.NameMangler);

        public Section LdTokenInfoSection => _ldTokenInfoSection;
        public Section SignaturesSection => _signaturesSection;
        public Section TemplatesSection => _templatesSection;
        public ExternalReferencesTableNode ExternalReferences => _externalReferences;
        public ExternalReferencesTableNode StaticsReferences => _staticsReferences;
        public NativeWriter Writer => _writer;

        public void AddVertexNodeToNativeLayout(NativeLayoutVertexNode vertexNode)
        {
            _vertexNodesToWrite.Add(vertexNode);
        }

        public void SaveNativeLayoutInfoWriter(NodeFactory factory)
        {
//            var d =
//                new Dictionary<MethodNameAndSigSignature, List<NativeLayoutMethodNameAndSignatureVertexNode>>();

            if (_writerSavedBytes != null)
                return;

            foreach (var vertexNode in _vertexNodesToWrite)
            {
                if (vertexNode is NativeLayoutMethodNameAndSignatureVertexNode)
                {
                    var m = (NativeLayoutMethodNameAndSignatureVertexNode)vertexNode;
                    var method = m.Method.ToString();
                    if ((uint)m.Method.GetHashCode() == 0x91B91B75)
                    {

                    }
                    if (method.Contains("[S.P.CompilerGenerated]Internal.CompilerGenerated.<Module>.InvokeRetOII<int32,__Canon,bool>(object,native int,ArgSetupState&,bool)"))
                    {
                    }
                }
                var v= vertexNode.WriteVertex(factory);
                if (vertexNode is NativeLayoutMethodNameAndSignatureVertexNode)
                {
                    var m = (NativeLayoutMethodNameAndSignatureVertexNode)vertexNode;
//                    if (v is MethodNameAndSigSignature)
//                    {
//                        if (d.ContainsKey((MethodNameAndSigSignature)v))
//                        {
//                            d[(MethodNameAndSigSignature)v].Add(m);
//                        }
//                        else d.Add((MethodNameAndSigSignature)v, new List<NativeLayoutMethodNameAndSignatureVertexNode> {m});
//                    }
                    var method = m.Method.ToString();
                    if (method.Contains("[S.P.CompilerGenerated]Internal.CompilerGenerated.<Module>.InvokeRetOII<int32,__Canon,bool>(object,native int,ArgSetupState&,bool)"))
                    {
                        _writer.OfInterest = v;
                    }
                }
                if (vertexNode is NativeLayoutTemplateMethodSignatureVertexNode)
                {
                    var m = (NativeLayoutTemplateMethodSignatureVertexNode)vertexNode;
                    if ((uint)m.MethodDesc.GetHashCode() == 0x91B91B75)
                    {

                    }
                }
            }

            _writerSavedBytes = _writer.Save();
//            foreach (var v in d.Keys)
//            {
//                if (v.VertexOffset == 25234)
//                {
//                    var m = d[v];
//                }
//            }
            if (_writerSavedBytes.Length == 36495)
            {
                var section = new byte[100];
                for (var i = 25236; i < 25336; i++)
                {
                    section[i - 25236] = _writerSavedBytes[i];
                }
            }

                // Zero out the native writer and vertex list so that we AV if someone tries to insert after we're done.
                _writer = null;
            _vertexNodesToWrite = null;
        }

        public override ObjectData GetData(NodeFactory factory, bool relocsOnly = false)
        {
            // Dependencies of the NativeLayoutInfo node are tracked by the callers that emit data into the native layout writer
            if (relocsOnly)
                return new ObjectData(Array.Empty<byte>(), Array.Empty<Relocation>(), 1, new ISymbolDefinitionNode[] { this });

            SaveNativeLayoutInfoWriter(factory);

            _endSymbol.SetSymbolOffset(_writerSavedBytes.Length);

            return new ObjectData(_writerSavedBytes, Array.Empty<Relocation>(), 1, new ISymbolDefinitionNode[] { this, _endSymbol });
        }

        protected internal override int Phase => (int)ObjectNodePhase.Ordered;
        public override int ClassCode => (int)ObjectNodeOrder.NativeLayoutInfoNode;
    }
}
