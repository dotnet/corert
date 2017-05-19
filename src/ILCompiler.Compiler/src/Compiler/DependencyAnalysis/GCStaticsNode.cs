// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System.Collections.Generic;

using Internal.Text;
using Internal.TypeSystem;

using Debug = System.Diagnostics.Debug;

namespace ILCompiler.DependencyAnalysis
{
    public class GCStaticsNode : ObjectNode, IExportableSymbolNode
    {
        private MetadataType _type;
        private List<FieldDesc> _preinitFields;

        public GCStaticsNode(MetadataType type)
        {
            Debug.Assert(!type.IsCanonicalSubtype(CanonicalFormKind.Specific));
            _type = type;

            //
            // Do we have any pre initialized data field?
            //
            foreach (var field in _type.GetFields())
            {
                var preinitDataField = field.PreInitDataField;
                if (preinitDataField != null)
                {
                    if (_preinitFields == null)
                        _preinitFields = new List<FieldDesc>();
                    _preinitFields.Add(field);
                }
            }

            if (_preinitFields != null)
                _preinitFields.Sort(FieldDescCompare);
        }

        static int FieldDescCompare(FieldDesc fd1, FieldDesc fd2)
        {
            return fd1.Offset.AsInt - fd2.Offset.AsInt;
        }

        protected override string GetName(NodeFactory factory) => this.GetMangledName(factory.NameMangler);

        public void AppendMangledName(NameMangler nameMangler, Utf8StringBuilder sb)
        {
            sb.Append(nameMangler.NodeMangler.GCStatics(_type));
        }

        public int Offset => 0;
        public MetadataType Type => _type;

        public static string GetMangledName(TypeDesc type, NameMangler nameMangler)
        {
            return nameMangler.NodeMangler.GCStatics(type);
        }

        public virtual bool IsExported(NodeFactory factory) => factory.CompilationModuleGroup.ExportsType(Type);

        private ISymbolNode GetGCStaticEETypeNode(NodeFactory factory)
        {
            GCPointerMap map = GCPointerMap.FromStaticLayout(_type);
            return factory.GCStaticEEType(map);
        }

        public GCStaticsPreInitDataNode NewPreInitDataNode()
        {
            return new GCStaticsPreInitDataNode(_type, _preinitFields);
        }

        protected override DependencyList ComputeNonRelocationBasedDependencies(NodeFactory factory)
        {
            DependencyList dependencyList = new DependencyList();
            
            if (factory.TypeSystemContext.HasEagerStaticConstructor(_type))
            {
                dependencyList.Add(factory.EagerCctorIndirection(_type.GetStaticConstructor()), "Eager .cctor");
            }

            {
            }

            dependencyList.Add(factory.GCStaticsRegion, "GCStatics Region");
            if (factory.Target.Abi == TargetAbi.CoreRT)
            {
                dependencyList.Add(GetGCStaticEETypeNode(factory), "GCStatic EEType");
                if (_preinitFields != null)
                {
                    dependencyList.Add(factory.GCStaticsPreInitDataNode(_type), "PreInitData node");
                }
            }

            dependencyList.Add(factory.GCStaticIndirection(_type), "GC statics indirection");
            return dependencyList;
        }

        public override bool StaticDependenciesAreComputed => true;

        public override ObjectNodeSection Section => ObjectNodeSection.DataSection;
        public override bool IsShareable => EETypeNode.IsTypeNodeShareable(_type);

        public override ObjectData GetData(NodeFactory factory, bool relocsOnly = false)
        {
            ObjectDataBuilder builder = new ObjectDataBuilder(factory, relocsOnly);

            builder.RequireInitialPointerAlignment();

            if (factory.Target.Abi == TargetAbi.CoreRT)
            {
                builder.EmitPointerReloc(GetGCStaticEETypeNode(factory), 1);
            }
            else
            {
                builder.RequireInitialAlignment(_type.GCStaticFieldAlignment.AsInt);

                // @TODO - emit the frozen array node reloc
                builder.EmitZeros(_type.GCStaticFieldSize.AsInt);
            }

            builder.AddSymbol(this);

            return builder.ToObjectData();
        }
    }
}
