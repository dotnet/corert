// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;

using Internal.Text;
using Internal.TypeSystem;

using ILCompiler;
using ILCompiler.DependencyAnalysis;

using Debug = System.Diagnostics.Debug;

namespace Internal.Runtime.JitSupport
{
    public class JitMethodCodeNode : ObjectNode, IMethodCodeNode
    {
        public JitMethodCodeNode(MethodDesc method)
        {
            _method = method;
        }

        private MethodDesc _method;
        private ObjectData _methodCode;
        private FrameInfo[] _frameInfos;
        private byte[] _gcInfo;
        private ObjectData _ehInfo;

        public void SetCode(byte[] data, Relocation[] relocs, DebugLocInfo[] debugLocInfos, DebugVarInfo[] debugVarInfos)
        {
            Debug.Assert(_methodCode == null);
            _methodCode = new MethodCode(data, relocs, _method.Context.Target.MinimumFunctionAlignment, new ISymbolDefinitionNode[] { this },
                debugLocInfos, debugVarInfos);
        }

        public MethodDesc Method => _method;

        public FrameInfo[] FrameInfos => _frameInfos;
        public byte[] GCInfo => _gcInfo;
        public ObjectData EHInfo => _ehInfo;

        public void InitializeFrameInfos(FrameInfo[] frameInfos)
        {
            Debug.Assert(_frameInfos == null);
            _frameInfos = frameInfos;
        }

        public void InitializeGCInfo(byte[] gcInfo)
        {
            Debug.Assert(_gcInfo == null);
            _gcInfo = gcInfo;
        }

        public void InitializeEHInfo(ObjectData ehInfo)
        {
            Debug.Assert(_ehInfo == null);
            _ehInfo = ehInfo;
        }

        protected override string GetName(NodeFactory factory)
        {
            throw new PlatformNotSupportedException();
        }

        public override ObjectNodeSection Section
        {
            get
            {
                throw new PlatformNotSupportedException();
            }
        }

        public override bool StaticDependenciesAreComputed => _methodCode != null;

        public void AppendMangledName(NameMangler nameMangler, Utf8StringBuilder sb)
        {
            throw new PlatformNotSupportedException();
        }
        public int Offset => 0;
        public override bool IsShareable => false;

        public override ObjectData GetData(NodeFactory factory, bool relocsOnly = false)
        {
            return _methodCode;
        }
    }
}
