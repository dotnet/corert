// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.Diagnostics;
using System.Text;
using Internal.Text;
using Internal.TypeSystem;

namespace ILCompiler.DependencyAnalysis
{
    [DebuggerTypeProxy(typeof(MethodCodeNodeDebugView))]
    public class MethodCodeNode : ObjectNode, IMethodBodyNode, INodeWithCodeInfo, INodeWithDebugInfo, ISymbolDefinitionNode, ISpecialUnboxThunkNode
    {
        private MethodDesc _method;
        private ObjectData _methodCode;
        private FrameInfo[] _frameInfos;
        private byte[] _gcInfo;
        private ObjectData _ehInfo;
        private DebugLocInfo[] _debugLocInfos;
        private DebugVarInfo[] _debugVarInfos;
        private DebugEHClauseInfo[] _debugEHClauseInfos;
        private bool _isFoldable;

        public MethodCodeNode(MethodDesc method)
        {
            Debug.Assert(!method.IsAbstract);
            Debug.Assert(method.GetCanonMethodTarget(CanonicalFormKind.Specific) == method);
            _method = method;
        }

        public void SetCode(ObjectData data, bool isFoldable)
        {
            Debug.Assert(_methodCode == null);
            _methodCode = data;
            _isFoldable = isFoldable;
        }

        public MethodDesc Method =>  _method;

        protected override string GetName(NodeFactory factory) => this.GetMangledName(factory.NameMangler);

        public override ObjectNodeSection Section
        {
            get
            {
                return _method.Context.Target.IsWindows ?
                    (_isFoldable ? ObjectNodeSection.FoldableManagedCodeWindowsContentSection : ObjectNodeSection.ManagedCodeWindowsContentSection) :
                    (_isFoldable ? ObjectNodeSection.FoldableManagedCodeUnixContentSection : ObjectNodeSection.ManagedCodeUnixContentSection);
            }
        }
        
        public override bool StaticDependenciesAreComputed => _methodCode != null;

        public virtual void AppendMangledName(NameMangler nameMangler, Utf8StringBuilder sb)
        {
            sb.Append(nameMangler.GetMangledMethodName(_method));
        }
        public int Offset => 0;
        public override bool IsShareable => _method is InstantiatedMethod || EETypeNode.IsTypeNodeShareable(_method.OwningType);

        protected override DependencyList ComputeNonRelocationBasedDependencies(NodeFactory factory)
        {
            DependencyList dependencies = null;

            TypeDesc owningType = _method.OwningType;
            if (factory.PreinitializationManager.HasEagerStaticConstructor(owningType))
            {
                if (dependencies == null)
                    dependencies = new DependencyList();
                dependencies.Add(factory.EagerCctorIndirection(owningType.GetStaticConstructor()), "Eager .cctor");
            }

            if (_ehInfo != null && _ehInfo.Relocs != null)
            {
                if (dependencies == null)
                    dependencies = new DependencyList();

                foreach (Relocation reloc in _ehInfo.Relocs)
                {
                    dependencies.Add(reloc.Target, "reloc");
                }
            }

            if (MethodAssociatedDataNode.MethodHasAssociatedData(factory, this))
            {
                dependencies = dependencies ?? new DependencyList();
                dependencies.Add(new DependencyListEntry(factory.MethodAssociatedData(this), "Method associated data"));
            }

            CodeBasedDependencyAlgorithm.AddDependenciesDueToMethodCodePresence(ref dependencies, factory, _method);

            return dependencies;
        }

        public override ObjectData GetData(NodeFactory factory, bool relocsOnly)
        {
            return _methodCode;
        }

        public bool IsSpecialUnboxingThunk => ((CompilerTypeSystemContext)Method.Context).IsSpecialUnboxingThunk(_method);

        public ISymbolNode GetUnboxingThunkTarget(NodeFactory factory)
        {
            Debug.Assert(IsSpecialUnboxingThunk);

            MethodDesc nonUnboxingMethod = ((CompilerTypeSystemContext)Method.Context).GetTargetOfSpecialUnboxingThunk(_method);
            return factory.MethodEntrypoint(nonUnboxingMethod, false);
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

        public override int ClassCode => 788492407;

        public override int CompareToImpl(ISortableNode other, CompilerComparer comparer)
        {
            return comparer.Compare(_method, ((MethodCodeNode)other)._method);
        }

        public override string ToString()
        {
            return _method.ToString();
        }

        internal class MethodCodeNodeDebugView
        {
            private readonly MethodCodeNode _node;

            public MethodCodeNodeDebugView(MethodCodeNode node)
            {
                _node = node;
            }

            public MethodDesc Method => _node.Method;

            public string Disassembly
            {
                get
                {
                    var sb = new StringBuilder();
                    sb.Append("// ");
                    sb.AppendLine(_node.Method.ToString());
                    if (_node.StaticDependenciesAreComputed)
                    {
                        var d = Disassembler.Disassemble(
                            _node.Method.Context.Target.Architecture,
                            _node._methodCode.Data,
                            _node._methodCode.Relocs);
                        sb.Append(d);
                    }
                    else
                    {
                        sb.Append("// Not compiled yet.");
                    }

                    return sb.ToString();
                }
            }
        }
    }
}
