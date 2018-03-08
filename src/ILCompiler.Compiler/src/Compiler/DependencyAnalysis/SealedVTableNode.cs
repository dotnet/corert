// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.


using System;
using System.Collections.Generic;
using System.Diagnostics;

using Internal.Text;
using Internal.TypeSystem;

namespace ILCompiler.DependencyAnalysis
{
    public class SealedVTableNode : ObjectNode, ISymbolDefinitionNode
    {
        private readonly TypeDesc _type;
        private List<MethodDesc> _sealedVTableEntries;

        public SealedVTableNode(TypeDesc type)
        {
            // Multidimensional arrays should not get a sealed vtable or a dispatch map. Runtime should use the 
            // sealed vtable and dispatch map of the System.Array basetype instead.
            // Pointer arrays also follow the same path
            Debug.Assert(!type.IsArrayTypeWithoutGenericInterfaces());
            Debug.Assert(!type.IsRuntimeDeterminedSubtype);

            _type = type;
        }

        protected override string GetName(NodeFactory factory) => this.GetMangledName(factory.NameMangler);

        public override ObjectNodeSection Section => _type.Context.Target.IsWindows ? ObjectNodeSection.FoldableReadOnlyDataSection : ObjectNodeSection.DataSection;

        public virtual void AppendMangledName(NameMangler nameMangler, Utf8StringBuilder sb)
        {
            sb.Append(nameMangler.CompilationUnitPrefix + "__SealedVTable_" + nameMangler.NodeMangler.EEType(_type));
        }

        int ISymbolNode.Offset => 0;
        int ISymbolDefinitionNode.Offset => 0;
        public override bool IsShareable => EETypeNode.IsTypeNodeShareable(_type);
        public override bool StaticDependenciesAreComputed => true;

        /// <summary>
        /// Returns the number of sealed vtable slots on the type. This API should only be called after successfully 
        /// building the sealed vtable slots.
        /// </summary>
        public int NumSealedVTableEntries
        {
            get
            {
                if (_sealedVTableEntries == null)
                    throw new NotSupportedException();

                return _sealedVTableEntries.Count;
            }
        }

        /// <summary>
        /// Returns the slot of a method in the sealed vtable, or -1 if not found. This API should only be called after 
        /// successfully building the sealed vtable slots.
        /// </summary>
        public int ComputeSealedVTableSlot(MethodDesc method)
        {
            if (_sealedVTableEntries == null)
                throw new NotSupportedException();

            for (int i = 0; i < _sealedVTableEntries.Count; i++)
            {
                if (_sealedVTableEntries[i] == method)
                    return i;
            }

            return -1;
        }

        public bool BuildSealedVTableSlots(NodeFactory factory, bool relocsOnly)
        {
            // Sealed vtable already built
            if (_sealedVTableEntries != null)
                return true;

            TypeDesc declType = _type.GetClosestDefType();

            // It's only okay to touch the actual list of slots if we're in the final emission phase
            // or the vtable is not built lazily.
            if (relocsOnly && !factory.VTable(declType).HasFixedSlots)
                return false;

            _sealedVTableEntries = new List<MethodDesc>();

            // Cpp codegen does not support sealed vtables
            if (factory.IsCppCodegenTemporaryWorkaround)
                return true;

            IReadOnlyList<MethodDesc> virtualSlots = factory.VTable(declType).Slots;

            for (int i = 0; i < virtualSlots.Count; i++)
            {
                MethodDesc implMethod = _type.GetClosestDefType().FindVirtualFunctionTargetMethodOnObjectType(virtualSlots[i]);

                if (implMethod.CanMethodBeInSealedVTable())
                    _sealedVTableEntries.Add(implMethod);
            }

            for (int interfaceIndex = 0; interfaceIndex < _type.RuntimeInterfaces.Length; interfaceIndex++)
            {
                var interfaceType = _type.RuntimeInterfaces[interfaceIndex];

                virtualSlots = factory.VTable(interfaceType).Slots;

                for (int interfaceMethodSlot = 0; interfaceMethodSlot < virtualSlots.Count; interfaceMethodSlot++)
                {
                    MethodDesc declMethod = virtualSlots[interfaceMethodSlot];
                    var implMethod = _type.GetClosestDefType().ResolveInterfaceMethodToVirtualMethodOnType(declMethod);

                    // Interface methods first implemented by a base type in the hierarchy will return null for the implMethod (runtime interface
                    // dispatch will walk the inheritance chain).
                    if (implMethod != null && implMethod.CanMethodBeInSealedVTable() && implMethod.OwningType != _type)
                        _sealedVTableEntries.Add(implMethod);
                }
            }

            return true;
        }

        public override ObjectData GetData(NodeFactory factory, bool relocsOnly)
        {
            ObjectDataBuilder objData = new ObjectDataBuilder(factory, relocsOnly);
            objData.RequireInitialAlignment(4);
            objData.AddSymbol(this);

            if (BuildSealedVTableSlots(factory, relocsOnly))
            {
                for (int i = 0; i < _sealedVTableEntries.Count; i++)
                {
                    MethodDesc canonImplMethod = _sealedVTableEntries[i].GetCanonMethodTarget(CanonicalFormKind.Specific);
                    objData.EmitReloc(factory.MethodEntrypoint(canonImplMethod, _sealedVTableEntries[i].OwningType.IsValueType), RelocType.IMAGE_REL_BASED_RELPTR32);
                }
            }

            return objData.ToObjectData();
        }

        protected internal override int ClassCode => 1632890252;
        protected internal override int CompareToImpl(SortableDependencyNode other, CompilerComparer comparer)
        {
            return comparer.Compare(_type, ((SealedVTableNode)other)._type);
        }
    }
}
