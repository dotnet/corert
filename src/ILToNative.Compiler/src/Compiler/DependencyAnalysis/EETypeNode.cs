// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using ILToNative.DependencyAnalysisFramework;
using Internal.TypeSystem;

namespace ILToNative.DependencyAnalysis
{
    class EETypeNode : ObjectNode, ISymbolNode
    {
        TypeDesc _type;
        bool _constructed;

        public EETypeNode(TypeDesc type, bool constructed)
        {
            _type = type;
            _constructed = constructed;
        }

        public override string GetName()
        {
            if (_constructed)
            {
                return ((ISymbolNode)this).MangledName + " constructed";
            }
            else
            {
                return ((ISymbolNode)this).MangledName;
            }
        }

        public override bool ShouldSkipEmittingObjectNode(NodeFactory factory)
        {
            if (!_constructed)
            {
                // If there is a constructed version of this node in the graph, emit that instead
                if (((DependencyNode)factory.ConstructedTypeSymbol(_type)).Marked)
                {
                    return true;
                }
            }

            return false;
        }

        public TypeDesc Type
        {
            get { return _type; }
        }

        public bool Constructed
        {
            get { return _constructed; }
        }

        public override string Section
        {
            get
            {
                return "data";
            }
        }

        public override bool StaticDependenciesAreComputed
        {
            get
            {
                return true;
            }
        }

        int ISymbolNode.Offset
        {
            get
            {
                return 0;
            }
        }

        string ISymbolNode.MangledName
        {
            get
            {
                return "__EEType_" + NodeFactory.NameMangler.GetMangledTypeName(_type);
            }
        }

        public override ObjectData GetData(NodeFactory factory, bool relocsOnly)
        {
            ObjectDataBuilder objData = new ObjectDataBuilder(factory);
            objData.Alignment = 16;
            objData.DefinedSymbols.Add(this);
            if (_type.IsArray)
            {
                objData.EmitShort((short)((ArrayType)_type).ElementType.GetElementSize()); // m_ComponentSize
                objData.EmitShort(0x4);                           // m_flags: IsArray(0x4)
            }
            else if (_type.IsString)
            {
                objData.EmitShort(2); // m_ComponentSize
                objData.EmitShort(0); // m_flags: 0
            }
            else
            {
                objData.EmitShort(0); // m_ComponentSize
                objData.EmitShort(0); // m_flags: 0
            }

            int pointerSize = _type.Context.Target.PointerSize;
            int minimumObjectSize = pointerSize * 3;
            int objectSize;
            if (_type is MetadataType)
            {
                objectSize = pointerSize +
                    ((MetadataType)_type).InstanceByteCount; // +pointerSize for SyncBlock
            }
            else if (_type is ArrayType)
            {
                objectSize = 3 * pointerSize; // SyncBlock + EETypePtr + Length
                int rank = ((ArrayType)_type).Rank;
                if (rank > 1)
                    objectSize +=
                        2 * _type.Context.GetWellKnownType(WellKnownType.Int32).GetElementSize() * rank;
            }
            else
                throw new NotImplementedException();

            objectSize = AlignmentHelper.AlignUp(objectSize, pointerSize);
            objectSize = Math.Max(minimumObjectSize, objectSize);
            objData.EmitInt(objectSize);

            if (Type.BaseType != null)
            {
                if (_constructed)
                {
                    objData.EmitPointerReloc(factory.ConstructedTypeSymbol(Type.BaseType));
                }
                else
                {
                    objData.EmitPointerReloc(factory.NecessaryTypeSymbol(Type.BaseType));
                }
            }
            else
            {
                objData.EmitZeroPointer();
            }

            if (_constructed)
            {
                OutputVirtualSlots(ref objData, _type, _type, factory);
            }

            return objData.ToObjectData();
        }

        public override bool HasConditionalStaticDependencies
        {
            get
            {
                // non constructed types don't have vtables
                if (!_constructed)
                    return false;

                // Since the vtable is dependency driven, generate conditional static dependencies for
                // all possible vtable entries
                foreach (MethodDesc method in _type.GetMethods())
                {
                    if (method.IsVirtual)
                        return true;
                }

                return false;
            }
        }

        public override IEnumerable<CombinedDependencyListEntry> GetConditionalStaticDependencies(NodeFactory factory)
        {
            if (_type is MetadataType)
            {
                foreach (MethodDesc decl in VirtualFunctionResolution.EnumAllVirtualSlots((MetadataType)_type))
                {
                    MethodDesc impl = VirtualFunctionResolution.FindVirtualFunctionTargetMethodOnObjectType(decl, (MetadataType)_type);
                    if (impl.OwningType == _type)
                    {
                        yield return new DependencyNodeCore<NodeFactory>.CombinedDependencyListEntry(factory.MethodEntrypoint(impl), factory.VirtualMethodUse(decl), "Virtual method");
                    }
                }
            }
        }

        private void OutputVirtualSlots(ref ObjectDataBuilder objData, TypeDesc implType, TypeDesc declType, NodeFactory context)
        {
            var baseType = declType.BaseType;
            if (baseType != null)
                OutputVirtualSlots(ref objData, implType, baseType, context);

            List<MethodDesc> virtualSlots;
            context.VirtualSlots.TryGetValue(declType, out virtualSlots);

            if (virtualSlots != null)
            {
                for (int i = 0; i < virtualSlots.Count; i++)
                {
                    MethodDesc declMethod = virtualSlots[i];

                    MethodDesc implMethod = VirtualFunctionResolution.FindVirtualFunctionTargetMethodOnObjectType(declMethod, implType.GetClosestDefType());

                    objData.EmitPointerReloc(context.MethodEntrypoint(implMethod));
                }
            }
        }
    }
}
