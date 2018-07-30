// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Diagnostics;

using Internal.JitInterface;
using Internal.Text;
using Internal.TypeSystem;

namespace ILCompiler.DependencyAnalysis.ReadyToRun
{
    public class MethodWithGCInfo : ObjectNode, IMethodCodeNode, IMethodBodyNode
    {
        public readonly MethodGCInfoNode GCInfoNode;

        private readonly MethodDesc _method;
        private readonly ModuleToken _token;

        private ObjectData _methodCode;
        private FrameInfo[] _frameInfos;
        private byte[] _gcInfo;
        private ObjectData _ehInfo;
        private DebugLocInfo[] _debugLocInfos;
        private DebugVarInfo[] _debugVarInfos;
        private DebugEHClauseInfo[] _debugEHClauseInfos;

        public MethodWithGCInfo(MethodDesc methodDesc, ModuleToken token)
        {
            GCInfoNode = new MethodGCInfoNode(this);
            _method = methodDesc;
            _token = token;
        }

        public void SetCode(ObjectData data)
        {
            Debug.Assert(_methodCode == null);
            _methodCode = data;
        }

        public MethodDesc Method => _method;

        public override ObjectData GetData(NodeFactory factory, bool relocsOnly)
        {
            ObjectData methodCode = _methodCode;
            if (relocsOnly)
            {
                int extraRelocs = 1; // GCInfoNode

                Relocation[] relocs = new Relocation[methodCode.Relocs.Length + extraRelocs];
                Array.Copy(methodCode.Relocs, relocs, methodCode.Relocs.Length);
                int extraRelocIndex = methodCode.Relocs.Length;
                relocs[extraRelocIndex++] = new Relocation(RelocType.IMAGE_REL_BASED_ADDR32NB, 0, GCInfoNode);
                Debug.Assert(extraRelocIndex == relocs.Length);
                methodCode = new ObjectData(methodCode.Data, relocs, methodCode.Alignment, methodCode.DefinedSymbols); 
            }
            return methodCode;
        }

        public override bool StaticDependenciesAreComputed => _methodCode != null;

        public virtual void AppendMangledName(NameMangler nameMangler, Utf8StringBuilder sb)
        {
            sb.Append(nameMangler.GetMangledMethodName(_method));
        }

        protected override string GetName(NodeFactory factory)
        {
            Utf8StringBuilder sb = new Utf8StringBuilder();
            AppendMangledName(factory.NameMangler, sb);
            return sb.ToString();
        }

        private const int ClassCodeValue = 315213488;

        int ISortableSymbolNode.ClassCode => ClassCodeValue;

        protected override int ClassCode => ClassCodeValue;

        public override ObjectNodeSection Section
        {
            get
            {
                return _method.Context.Target.IsWindows ? ObjectNodeSection.ManagedCodeWindowsContentSection : ObjectNodeSection.ManagedCodeUnixContentSection;
            }
        }

        public FrameInfo[] FrameInfos => _frameInfos;
        public byte[] GCInfo => _gcInfo;
        public ObjectData EHInfo => _ehInfo;

        public ISymbolNode GetAssociatedDataNode(NodeFactory factory)
        {
            if (MethodAssociatedDataNode.MethodHasAssociatedData(factory, this))
                return factory.MethodAssociatedData(this);

            return null;
        }

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

        public DebugLocInfo[] DebugLocInfos => _debugLocInfos;
        public DebugVarInfo[] DebugVarInfos => _debugVarInfos;
        public DebugEHClauseInfo[] DebugEHClauseInfos => _debugEHClauseInfos;

        public void InitializeDebugLocInfos(DebugLocInfo[] debugLocInfos)
        {
            Debug.Assert(_debugLocInfos == null);
            _debugLocInfos = debugLocInfos;
        }

        public void InitializeDebugVarInfos(DebugVarInfo[] debugVarInfos)
        {
            Debug.Assert(_debugVarInfos == null);
            _debugVarInfos = debugVarInfos;
        }

        public void InitializeDebugEHClauseInfos(DebugEHClauseInfo[] debugEHClauseInfos)
        {
            Debug.Assert(_debugEHClauseInfos == null);
            _debugEHClauseInfos = debugEHClauseInfos;
        }

        public int CompareToImpl(ISortableSymbolNode other, CompilerComparer comparer)
        {
            MethodWithGCInfo otherMethod = (MethodWithGCInfo)other;
            return _token.CompareTo(otherMethod._token);
        }

        public int Offset => 0;
        public override bool IsShareable => _method is InstantiatedMethod || EETypeNode.IsTypeNodeShareable(_method.OwningType);
    }
}
