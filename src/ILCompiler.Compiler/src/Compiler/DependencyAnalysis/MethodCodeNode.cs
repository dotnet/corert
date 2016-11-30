// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System.Diagnostics;

using Internal.Text;
using Internal.TypeSystem;

namespace ILCompiler.DependencyAnalysis
{
    internal class MethodCodeNode : ObjectNode, IMethodNode, INodeWithCodeInfo, INodeWithDebugInfo
    {
        public static readonly ObjectNodeSection StartSection = new ObjectNodeSection(".managedcode$A", SectionType.Executable);
        public static readonly ObjectNodeSection WindowsContentSection = new ObjectNodeSection(".managedcode$I", SectionType.Executable);
        public static readonly ObjectNodeSection UnixContentSection = new ObjectNodeSection("__managedcode", SectionType.Executable);
        public static readonly ObjectNodeSection EndSection = new ObjectNodeSection(".managedcode$Z", SectionType.Executable);

        private MethodDesc _method;
        private ObjectData _methodCode;
        private FrameInfo[] _frameInfos;
        private byte[] _gcInfo;
        private ObjectData _ehInfo;
        private DebugLocInfo[] _debugLocInfos;
        private DebugVarInfo[] _debugVarInfos;

        public MethodCodeNode(MethodDesc method)
        {
            Debug.Assert(!method.IsAbstract);
            Debug.Assert(method.GetCanonMethodTarget(CanonicalFormKind.Specific) == method);
            _method = method;
        }

        public void SetCode(ObjectData data)
        {
            Debug.Assert(_methodCode == null);
            _methodCode = data;
        }

        public MethodDesc Method =>  _method;

        protected override string GetName() => this.GetMangledName();

        public override ObjectNodeSection Section
        {
            get
            {
                return _method.Context.Target.IsWindows ? WindowsContentSection : UnixContentSection;
            }
        }
        
        public override bool StaticDependenciesAreComputed => _methodCode != null;

        public void AppendMangledName(NameMangler nameMangler, Utf8StringBuilder sb)
        {
            sb.Append(NodeFactory.NameMangler.GetMangledMethodName(_method));
        }
        public int Offset => 0;
        public override bool IsShareable => _method is InstantiatedMethod || EETypeNode.IsTypeNodeShareable(_method.OwningType);

        protected override DependencyList ComputeNonRelocationBasedDependencies(NodeFactory factory)
        {
            DependencyList dependencies = null;

            TypeDesc owningType = _method.OwningType;
            if (factory.TypeSystemContext.HasEagerStaticConstructor(owningType))
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

            // Reflection invoke stub handling is here because in the current reflection model we reflection-enable
            // all methods that are compiled. Ideally the list of reflection enabled methods should be known before
            // we even start the compilation process (with the invocation stubs being compilation roots like any other).
            // The existing model has it's problems: e.g. the invocability of the method depends on inliner decisions.
            if (factory.MetadataManager.HasReflectionInvokeStub(_method)
                && !_method.IsCanonicalMethod(CanonicalFormKind.Any) /* Shared generics handled in the shadow concrete method node */)
            {
                if (dependencies == null)
                    dependencies = new DependencyList();

                MethodDesc invokeStub = factory.MetadataManager.GetReflectionInvokeStub(Method);
                MethodDesc canonInvokeStub = invokeStub.GetCanonMethodTarget(CanonicalFormKind.Specific);
                if (invokeStub != canonInvokeStub)
                    dependencies.Add(new DependencyListEntry(factory.FatFunctionPointer(invokeStub), "Reflection invoke"));
                else
                    dependencies.Add(new DependencyListEntry(factory.MethodEntrypoint(invokeStub), "Reflection invoke"));
            }

            return dependencies;
        }

        public override ObjectData GetData(NodeFactory factory, bool relocsOnly)
        {
            return _methodCode;
        }

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

        public DebugLocInfo[] DebugLocInfos => _debugLocInfos;
        public DebugVarInfo[] DebugVarInfos => _debugVarInfos;

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
    }
}
