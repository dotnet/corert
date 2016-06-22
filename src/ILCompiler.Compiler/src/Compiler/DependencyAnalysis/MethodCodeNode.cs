// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Diagnostics;
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
        private ObjectData _ehInfo;
        private DebugLocInfo[] _debugLocInfos;
        private DebugVarInfo[] _debugVarInfos;

        public MethodCodeNode(MethodDesc method)
        {
            Debug.Assert(!method.IsAbstract);
            _method = method;
        }

        public void SetCode(ObjectData data)
        {
            Debug.Assert(_methodCode == null);
            _methodCode = data;
        }

        public MethodDesc Method
        {
            get
            {
                return _method;
            }
        }
        public override string GetName()
        {
            return ((ISymbolNode)this).MangledName;
        }

        public override ObjectNodeSection Section
        {
            get
            {
                return _method.Context.Target.IsWindows ? WindowsContentSection : UnixContentSection;
            }
        }

        public override bool ShouldShareNodeAcrossModules(NodeFactory factory)
        {
            return factory.CompilationModuleGroup.ShouldShareAcrossModules(_method);
        }

        public override bool StaticDependenciesAreComputed
        {
            get
            {
                return _methodCode != null;
            }
        }

        string ISymbolNode.MangledName
        {
            get
            {
                return NodeFactory.NameMangler.GetMangledMethodName(_method);
            }
        }

        int ISymbolNode.Offset
        {
            get
            {
                return 0;
            }
        }

        protected override DependencyList ComputeNonRelocationBasedDependencies(NodeFactory factory)
        {
            DependencyList dependencies = null;

            TypeDesc owningType = _method.OwningType;
            if (factory.TypeInitializationManager.HasEagerStaticConstructor(owningType))
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

            return dependencies;
        }

        public override ObjectData GetData(NodeFactory factory, bool relocsOnly)
        {
            return _methodCode;
        }

        public FrameInfo[] FrameInfos
        {
            get
            {
                return _frameInfos;
            }
        }

        public ObjectData EHInfo
        {
            get
            {
                return _ehInfo;
            }
        }

        public void InitializeFrameInfos(FrameInfo[] frameInfos)
        {
            Debug.Assert(_frameInfos == null);
            _frameInfos = frameInfos;
        }

        public void InitializeEHInfo(ObjectData ehInfo)
        {
            Debug.Assert(_ehInfo == null);
            _ehInfo = ehInfo;
        }

        public DebugLocInfo[] DebugLocInfos
        {
            get
            {
                return _debugLocInfos;
            }
        }

        public DebugVarInfo[] DebugVarInfos
        {
            get
            {
                return _debugVarInfos;
            }
        }

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
