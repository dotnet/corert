// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System.Collections.Generic;
using System.Diagnostics;
using ILCompiler.DependencyAnalysisFramework;
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

            if (_method.HasInstantiation)
            {
                if (factory.MetadataManager.ExactMethodInstantiations.AddExactMethodInstantiationEntry(factory, _method))
                {
                    // Ensure dependency nodes used by the signature are added to the graph
                    if (dependencies == null)
                        dependencies = new DependencyList();

                    dependencies.Add(new DependencyListEntry(factory.NecessaryTypeSymbol(_method.OwningType), "Exact method instantiation signature"));

                    foreach(var arg in _method.Instantiation)
                        dependencies.Add(new DependencyListEntry(factory.NecessaryTypeSymbol(arg), "Exact method instantiation signature"));

                    dependencies.Add(new DependencyListEntry(factory.NecessaryTypeSymbol(_method.Signature.ReturnType), "Exact method instantiation signature"));

                    for(int i = 0; i < _method.Signature.Length; i++)
                        dependencies.Add(new DependencyListEntry(factory.NecessaryTypeSymbol(_method.Signature[i]), "Exact method instantiation signature"));
                }
            }


            return dependencies;
        }

        public override bool HasDynamicDependencies
        {
            get
            {
                // Only generic virtual methods work have dynamic dependencies
                if (_method.HasInstantiation && _method.IsVirtual)
                {
                    if (_method.OwningType.IsCanonicalSubtype(CanonicalFormKind.Specific))
                        return false;

                    if (_method.OwningType.IsCanonicalSubtype(CanonicalFormKind.Universal) && 
                        _method.OwningType != _method.OwningType.ConvertToCanonForm(CanonicalFormKind.Universal))
                        return false;

                    if (_method.OwningType.IsRuntimeDeterminedSubtype)
                        return false;

                    if (_method.OwningType.ContainsGenericVariables)
                        return false;

                    // Check to see if the method instantiation has shared runtime components, or canon components. Those are not actually added to the various vtables, so 
                    // are not examined for dynamic dependencies
                    foreach(var type in _method.Instantiation)
                    {
                        if (type.IsRuntimeDeterminedSubtype || type.IsCanonicalSubtype(CanonicalFormKind.Specific) || type.IsGenericParameter)
                            return false;
                    }
                    return true;
                }
                return false;
            }
        }

        public override IEnumerable<CombinedDependencyListEntry> SearchDynamicDependencies(List<DependencyNodeCore<NodeFactory>> markedNodes, int firstNode, NodeFactory factory)
        {
            Debug.Assert(_method.IsVirtual && _method.HasInstantiation);

            List<CombinedDependencyListEntry> dynamicDependencies = new List<CombinedDependencyListEntry>();

            for (int i = firstNode; i < markedNodes.Count; i++)
            {
                DependencyNodeCore<NodeFactory> entry = markedNodes[i];
                EETypeNode entryAsEETypeNode = entry as EETypeNode;

                // TODO: Variance expansion

                if (entryAsEETypeNode == null)
                    continue;

                TypeDesc potentialOverrideType = entryAsEETypeNode.Type;
                if (!(potentialOverrideType is DefType))
                    continue;

                // Open generic types are not interesting.
                if (potentialOverrideType.ContainsGenericVariables)
                    continue;

                // If this is an interface gvm, look for types that implement the interface
                // and other instantantiations that have the same canonical form.
                // This ensure the various slot numbers remain equivalent across all types where there is an equivalence
                // relationship in the vtable.
                if (_method.OwningType.IsInterface)
                {
                    if (potentialOverrideType.IsInterface)
                        continue;

                    foreach (DefType interfaceImpl in potentialOverrideType.RuntimeInterfaces)
                    {
                        if (interfaceImpl.ConvertToCanonForm(CanonicalFormKind.Specific) == _method.OwningType.ConvertToCanonForm(CanonicalFormKind.Specific))
                        {
                            // Find if the type implements this method. (Note, do this comparision against the generic definition of the method, not the
                            // specific method instantiation that is "method"
                            MethodDesc genericDefinition =interfaceImpl.GetMethod(_method.Name, _method.GetTypicalMethodDefinition().Signature);
                            // TODO
                            //if (interfaceImpl.HasOverrideSlot(genericDefinition))
                            //{
                            //    CreateDependencyForMethodSlotAndInstantiation(potentialOverrideType, interfaceImpl.GetOverrideSlot(genericDefinition), method->Instantiation(), dynamicDependencies, _nodeFactory, NutcReductionNode::node_type::proposed_gvm_method_template);
                            //    CreateDependencyForMethodSlotDeclAndInstantiation(potentialOverrideType, interfaceImpl.GetOverrideSlot(genericDefinition), method->Instantiation(), dynamicDependencies, _nodeFactory, NutcReductionNode::node_type::proposed_gvm_method_template);
                            //}
                        }
                    }
                }
                else
                {
                    // TODO: Ensure GVM Canon Target

                    TypeDesc overrideTypeCanonCur = potentialOverrideType;
                    TypeDesc methodCanonContainingType = _method.OwningType;
                    while (overrideTypeCanonCur != null)
                    {
                        if (overrideTypeCanonCur.ConvertToCanonForm(CanonicalFormKind.Specific) == methodCanonContainingType.ConvertToCanonForm(CanonicalFormKind.Specific))
                        {
                            CreateDependencyForMethodSlotAndInstantiation((DefType)potentialOverrideType, dynamicDependencies, factory);
                            CreateDependencyForMethodSlotDeclAndInstantiation((DefType)potentialOverrideType, dynamicDependencies, factory);
                        }

                        overrideTypeCanonCur = overrideTypeCanonCur.BaseType;
                    }
                }
            }
            return dynamicDependencies;
        }

        private void CreateDependencyForMethodSlotAndInstantiation(DefType potentialOverrideType, List<CombinedDependencyListEntry> dynamicDependencies, NodeFactory factory)
        {
            Debug.Assert(!potentialOverrideType.ContainsGenericVariables);
            MethodDesc methodDefInDerivedType = potentialOverrideType.GetMethod(_method.Name, _method.GetTypicalMethodDefinition().Signature);
            if (methodDefInDerivedType == null)
                return;
            MethodDesc derivedMethodInstantiation = _method.Context.GetInstantiatedMethod(methodDefInDerivedType, _method.Instantiation);


            // Universal canonical instantiations should be entirely universal canon
            if (derivedMethodInstantiation.IsCanonicalMethod(CanonicalFormKind.Universal))
            {
                derivedMethodInstantiation = derivedMethodInstantiation.GetCanonMethodTarget(CanonicalFormKind.Universal);
            }

            if (true /* TODO: derivedMethodInstantiation.CheckConstraints()*/)
            {
                dynamicDependencies.Add(new CombinedDependencyListEntry(factory.MethodEntrypoint(derivedMethodInstantiation, false), null, "DerivedMethodInstantiation"));
            }
            else
            {
                // TODO
            }
        }

        private void CreateDependencyForMethodSlotDeclAndInstantiation(DefType potentialOverrideType, List<CombinedDependencyListEntry> dynamicDependencies, NodeFactory factory)
        {
            Debug.Assert(!potentialOverrideType.ContainsGenericVariables);
            MethodDesc slotDecl = MetadataVirtualMethodAlgorithm.FindSlotDefiningMethodForVirtualMethod(_method.GetTypicalMethodDefinition());
            MethodDesc declMethodInstantiation = _method.Context.GetInstantiatedMethod(slotDecl, _method.Instantiation);

            // Universal canonical instantiations should be entirely universal canon
            if (declMethodInstantiation.IsCanonicalMethod(CanonicalFormKind.Universal))
            {
                declMethodInstantiation = declMethodInstantiation.GetCanonMethodTarget(CanonicalFormKind.Universal);
            }

            if (true /* TODO: declMethodInstantiation.CheckConstraints()*/)
            {
                dynamicDependencies.Add(new CombinedDependencyListEntry(factory.MethodEntrypoint(declMethodInstantiation, false), null, "DeclMethodInstantiation"));
            }
            else
            {
                // TODO
            }
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
