// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System.Collections.Generic;

using Internal.Text;
using Internal.TypeSystem;

using Debug = System.Diagnostics.Debug;
using GCStaticRegionConstants = Internal.Runtime.GCStaticRegionConstants;

namespace ILCompiler.DependencyAnalysis
{
    public class GCStaticsNode : ObjectNode, IExportableSymbolNode, ISortableSymbolNode, ISymbolNodeWithDebugInfo
    {
        private MetadataType _type;
        private List<PreInitFieldInfo> _preInitFieldInfos;

        public GCStaticsNode(MetadataType type)
        {
            Debug.Assert(!type.IsCanonicalSubtype(CanonicalFormKind.Specific));
            _type = type;
            _preInitFieldInfos = PreInitFieldInfo.GetPreInitFieldInfos(_type, hasGCStaticBase: true);
        }

        protected override string GetName(NodeFactory factory) => this.GetMangledName(factory.NameMangler);

        public void AppendMangledName(NameMangler nameMangler, Utf8StringBuilder sb)
        {
            sb.Append(nameMangler.NodeMangler.GCStatics(_type));
        }

        public int Offset => 0;
        public MetadataType Type => _type;

        public IDebugInfo DebugInfo => NullTypeIndexDebugInfo.Instance;

        public static string GetMangledName(TypeDesc type, NameMangler nameMangler)
        {
            return nameMangler.NodeMangler.GCStatics(type);
        }

        public virtual ExportForm GetExportForm(NodeFactory factory) => factory.CompilationModuleGroup.GetExportTypeForm(Type);

        private ISymbolNode GetGCStaticEETypeNode(NodeFactory factory)
        {
            GCPointerMap map = GCPointerMap.FromStaticLayout(_type);
            return factory.GCStaticEEType(map);
        }

        public GCStaticsPreInitDataNode NewPreInitDataNode()
        {
            Debug.Assert(_preInitFieldInfos != null);
            return new GCStaticsPreInitDataNode(_type, _preInitFieldInfos);
        }

        protected override DependencyList ComputeNonRelocationBasedDependencies(NodeFactory factory)
        {
            DependencyList dependencyList = new DependencyList();

            if (factory.TypeSystemContext.HasEagerStaticConstructor(_type))
            {
                dependencyList.Add(factory.EagerCctorIndirection(_type.GetStaticConstructor()), "Eager .cctor");
            }

            dependencyList.Add(factory.GCStaticsRegion, "GCStatics Region");
            if (factory.Target.Abi != TargetAbi.ProjectN)
            {
                dependencyList.Add(GetGCStaticEETypeNode(factory), "GCStatic EEType");
                if (_preInitFieldInfos != null)
                    dependencyList.Add(factory.GCStaticsPreInitDataNode(_type), "PreInitData node");
            }
            else
            {
                dependencyList.Add(((UtcNodeFactory)factory).TypeGCStaticDescSymbol(_type), "GC Desc");
            }

            dependencyList.Add(factory.GCStaticIndirection(_type), "GC statics indirection");
            EETypeNode.AddDependenciesForStaticsNode(factory, _type, ref dependencyList);

            return dependencyList;
        }

        public override bool StaticDependenciesAreComputed => true;

        public override ObjectNodeSection Section => ObjectNodeSection.DataSection;
        public override bool IsShareable => EETypeNode.IsTypeNodeShareable(_type);

        public override ObjectData GetData(NodeFactory factory, bool relocsOnly = false)
        {
            if (factory.Target.Abi == TargetAbi.CoreRT || factory.Target.Abi == TargetAbi.CppCodegen)
            {
                ObjectDataBuilder builder = new ObjectDataBuilder(factory, relocsOnly);

                builder.RequireInitialPointerAlignment();

                int delta = GCStaticRegionConstants.Uninitialized;

                // Set the flag that indicates next pointer following EEType is the preinit data
                if (_preInitFieldInfos != null)
                    delta |= GCStaticRegionConstants.HasPreInitializedData;
                
                builder.EmitPointerReloc(GetGCStaticEETypeNode(factory), delta);

                if (_preInitFieldInfos != null)
                    builder.EmitPointerReloc(factory.GCStaticsPreInitDataNode(_type));

                builder.AddSymbol(this);

                return builder.ToObjectData();
            }
            else 
            {
                if (_preInitFieldInfos == null)
                {
                    ObjectDataBuilder builder = new ObjectDataBuilder(factory, relocsOnly);

                    builder.RequireInitialPointerAlignment();

                    builder.EmitZeros(_type.GCStaticFieldSize.AsInt);

                    builder.AddSymbol(this);

                    return builder.ToObjectData();
                }
                else
                {
                    _preInitFieldInfos.Sort(PreInitFieldInfo.FieldDescCompare);
                    return GCStaticsPreInitDataNode.GetDataForPreInitDataField(this, _type, _preInitFieldInfos, 0, factory, relocsOnly);
                }
            }
        }

        public override int ClassCode => -522346696;

        public override int CompareToImpl(ISortableNode other, CompilerComparer comparer)
        {
            return comparer.Compare(_type, ((GCStaticsNode)other)._type);
        }
    }
}
