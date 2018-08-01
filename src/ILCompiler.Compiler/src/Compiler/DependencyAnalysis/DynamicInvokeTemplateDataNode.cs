// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Diagnostics;
using System.Collections.Generic;

using Internal.Text;
using Internal.TypeSystem;
using Internal.NativeFormat;

namespace ILCompiler.DependencyAnalysis
{
    /// <summary>
    /// Represents a map between method name / signature and CanonicalEntryPoint for the corresponding invoke stub.
    /// The first entry is the containing type of the invoke stubs.
    /// </summary>
    internal sealed class DynamicInvokeTemplateDataNode : ObjectNode, ISymbolDefinitionNode
    {
        private ObjectAndOffsetSymbolNode _endSymbol;
        private ExternalReferencesTableNode _externalReferences;
        private Dictionary<MethodDesc, int> _methodToTemplateIndex = new Dictionary<MethodDesc, int>();
        private TypeDesc _dynamicInvokeMethodContainerType;
#if DEBUG
        bool _dataEmitted;
#endif

        public DynamicInvokeTemplateDataNode(ExternalReferencesTableNode externalReferences)
        {
            _endSymbol = new ObjectAndOffsetSymbolNode(this, 0, "__dynamic_invoke_template_data_end", true);
            _externalReferences = externalReferences;
        }

        public void AppendMangledName(NameMangler nameMangler, Utf8StringBuilder sb)
        {
            sb.Append(nameMangler.CompilationUnitPrefix).Append("__dynamic_invoke_template_data");
        }

        public ISymbolNode EndSymbol => _endSymbol;
        public int Offset => 0;
        public override ObjectNodeSection Section => _externalReferences.Section;
        public override bool IsShareable => false;
        public override bool StaticDependenciesAreComputed => true;
        protected override string GetName(NodeFactory factory) => this.GetMangledName(factory.NameMangler);
        public override bool ShouldSkipEmittingObjectNode(NodeFactory factory) => _dynamicInvokeMethodContainerType == null;

        public int GetIdForMethod(MethodDesc dynamicInvokeMethod)
        {
            // We should only see canonical or non-shared methods here
            Debug.Assert(dynamicInvokeMethod.GetCanonMethodTarget(CanonicalFormKind.Specific) == dynamicInvokeMethod);
            Debug.Assert(!dynamicInvokeMethod.IsCanonicalMethod(CanonicalFormKind.Universal));

            int templateIndex;
            if (!_methodToTemplateIndex.TryGetValue(dynamicInvokeMethod, out templateIndex))
            {
#if DEBUG
                Debug.Assert(!_dataEmitted, "Cannot get new invoke stub Ids after data is emitted");
#endif
                TypeDesc dynamicInvokeMethodContainingType = dynamicInvokeMethod.OwningType;

                if (_dynamicInvokeMethodContainerType == null)
                {
                    _dynamicInvokeMethodContainerType = dynamicInvokeMethodContainingType;
                }
                Debug.Assert(dynamicInvokeMethodContainingType == _dynamicInvokeMethodContainerType);
                templateIndex = (2 * _methodToTemplateIndex.Count) + 1;
                // Add 1 to the index to account for the first blob entry being the containing EEType RVA
                _methodToTemplateIndex.Add(dynamicInvokeMethod, templateIndex);
            }

            return templateIndex;
        }
        
        public void AddDependenciesDueToInvokeTemplatePresence(ref DependencyList dependencies, NodeFactory factory, MethodDesc method)
        {
            // Add the invoke stub to the list so its index is available later on when ReflectionInvokeMapNode emits
            // the type loader dictionary information for the stub.
            GetIdForMethod(method);

            dependencies.Add(new DependencyListEntry(factory.MethodEntrypoint(method), "Dynamic invoke stub"));
            dependencies.Add(new DependencyListEntry(factory.NativeLayout.PlacedSignatureVertex(factory.NativeLayout.MethodNameAndSignatureVertex(method)), "Dynamic invoke stub"));
            dependencies.Add(new DependencyListEntry(factory.NecessaryTypeSymbol(method.OwningType), "Dynamic invoke stub containing type"));
        }

        public override ObjectData GetData(NodeFactory factory, bool relocsOnly = false)
        {
            if (relocsOnly)
                return new ObjectData(Array.Empty<byte>(), Array.Empty<Relocation>(), 1, new ISymbolDefinitionNode[] { this });

            Debug.Assert(_dynamicInvokeMethodContainerType != null);

            // Ensure the native layout blob has been saved
            factory.MetadataManager.NativeLayoutInfo.SaveNativeLayoutInfoWriter(factory);

            ObjectDataBuilder objData = new ObjectDataBuilder(factory, relocsOnly);
            objData.RequireInitialPointerAlignment();
            objData.AddSymbol(this);

            objData.EmitReloc(factory.NecessaryTypeSymbol(_dynamicInvokeMethodContainerType), RelocType.IMAGE_REL_BASED_RELPTR32);

            List<KeyValuePair<MethodDesc, int>> sortedList = new List<KeyValuePair<MethodDesc, int>>(_methodToTemplateIndex);
            sortedList.Sort((firstEntry, secondEntry) => firstEntry.Value.CompareTo(secondEntry.Value));

            for (int i = 0; i < sortedList.Count; i++)
            {
                Debug.Assert(sortedList[i].Value * 4 == objData.CountBytes);
                var nameAndSig = factory.NativeLayout.PlacedSignatureVertex(factory.NativeLayout.MethodNameAndSignatureVertex(sortedList[i].Key));

                objData.EmitInt(nameAndSig.SavedVertex.VertexOffset);
                objData.EmitReloc(factory.MethodEntrypoint(sortedList[i].Key), RelocType.IMAGE_REL_BASED_RELPTR32);
            }

            _endSymbol.SetSymbolOffset(objData.CountBytes);
            objData.AddSymbol(_endSymbol);

            // Prevent further adds now we're done writing
#if DEBUG
            if (!relocsOnly)
                _dataEmitted = true;
#endif

            return objData.ToObjectData();
        }

        protected internal override int Phase => (int)ObjectNodePhase.Ordered;
        public override int ClassCode => (int)ObjectNodeOrder.DynamicInvokeTemplateDataNode;
    }
}
