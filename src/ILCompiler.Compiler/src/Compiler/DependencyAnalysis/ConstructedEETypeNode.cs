// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Collections.Generic;
using System.Diagnostics;

using Internal.Runtime;
using Internal.Text;
using Internal.TypeSystem;

namespace ILCompiler.DependencyAnalysis
{
    internal class ConstructedEETypeNode : EETypeNode
    {
        public ConstructedEETypeNode(NodeFactory factory, TypeDesc type) : base(factory, type)
        {
            CheckCanGenerateConstructedEEType(factory, type);
        }

        protected override string GetName() => this.GetMangledName() + " constructed";

        public override bool ShouldSkipEmittingObjectNode(NodeFactory factory)
        {
            return false;
        }

        protected override DependencyList ComputeNonRelocationBasedDependencies(NodeFactory factory)
        {
            DefType closestDefType = _type.GetClosestDefType();

            DependencyList dependencyList = new DependencyList();
            if (_type.RuntimeInterfaces.Length > 0)
            {
                dependencyList.Add(factory.InterfaceDispatchMap(_type), "Interface dispatch map");

                // If any of the implemented interfaces have variance, calls against compatible interface methods
                // could result in interface methods of this type being used (e.g. IEnumberable<object>.GetEnumerator()
                // can dispatch to an implementation of IEnumerable<string>.GetEnumerator()).
                // For now, we will not try to optimize this and we will pretend all interface methods are necessary.
                
                foreach (var implementedInterface in _type.RuntimeInterfaces)
                {
                    if (implementedInterface.HasVariance)
                    {
                        foreach (var interfaceMethod in implementedInterface.GetAllMethods())
                        {
                            if (interfaceMethod.Signature.IsStatic)
                                continue;

                            MethodDesc implMethod = closestDefType.ResolveInterfaceMethodToVirtualMethodOnType(interfaceMethod);
                            if (implMethod != null)
                            {
                                dependencyList.Add(factory.VirtualMethodUse(interfaceMethod), "Variant interface method");
                                dependencyList.Add(factory.VirtualMethodUse(implMethod), "Variant interface method");
                            }
                        }
                    }
                }
            }

            if (_type.IsArray)
            {
                // Array EEType depends on System.Array's virtuals. Array EETypes don't point to
                // their base type (i.e. there's no reloc based dependency making this "just work").
                dependencyList.Add(factory.ConstructedTypeSymbol(_type.BaseType), "Array base type");
            }

            dependencyList.Add(factory.VTable(_type), "VTable");

            if (closestDefType.HasGenericDictionarySlot())
            {
                // Generic dictionary pointer is part of the vtable and as such it gets only laid out
                // at the final data emission phase. We need to report it as a non-relocation dependency.
                dependencyList.Add(factory.TypeGenericDictionary(closestDefType), "Type generic dictionary");
            }

            // Include the optional fields by default. We don't know if optional fields will be needed until
            // all of the interface usage has been stabilized. If we end up not needing it, the EEType node will not
            // generate any relocs to it, and the optional fields node will instruct the object writer to skip
            // emitting it.
            dependencyList.Add(_optionalFieldsNode, "Optional fields");

            // The fact that we generated an EEType means that someone can call RuntimeHelpers.RunClassConstructor.
            // We need to make sure this is possible - we need the class constructor context.
            if (factory.TypeSystemContext.HasLazyStaticConstructor(_type))
            {
                dependencyList.Add(factory.TypeNonGCStaticsSymbol((MetadataType)_type), "Class constructor");
            }

            return dependencyList;
        }

        public override bool HasConditionalStaticDependencies
        {
            get
            {
                // Since the vtable is dependency driven, generate conditional static dependencies for
                // all possible vtable entries
                foreach (var method in _type.GetClosestDefType().GetAllMethods())
                {
                    if (method.IsVirtual)
                        return true;
                }

                // If the type implements at least one interface, calls against that interface could result in this type's
                // implementation being used.
                if (_type.RuntimeInterfaces.Length > 0)
                    return true;

                return false;
            }
        }

        public override IEnumerable<CombinedDependencyListEntry> GetConditionalStaticDependencies(NodeFactory factory)
        {
            DefType defType = _type.GetClosestDefType();

            foreach (MethodDesc decl in defType.EnumAllVirtualSlots())
            {
                if (decl.HasInstantiation)
                    continue;

                MethodDesc impl = defType.FindVirtualFunctionTargetMethodOnObjectType(decl);
                if (impl.OwningType == defType && !impl.IsAbstract)
                {
                    MethodDesc canonImpl = impl.GetCanonMethodTarget(CanonicalFormKind.Specific);
                    yield return new CombinedDependencyListEntry(factory.MethodEntrypoint(canonImpl, _type.IsValueType), factory.VirtualMethodUse(decl), "Virtual method");
                }
            }

            Debug.Assert(
                _type == defType ||
                ((System.Collections.IStructuralEquatable)defType.RuntimeInterfaces).Equals(_type.RuntimeInterfaces,
                EqualityComparer<DefType>.Default));

            // Add conditional dependencies for interface methods the type implements. For example, if the type T implements
            // interface IFoo which has a method M1, add a dependency on T.M1 dependent on IFoo.M1 being called, since it's
            // possible for any IFoo object to actually be an instance of T.
            foreach (DefType interfaceType in defType.RuntimeInterfaces)
            {
                Debug.Assert(interfaceType.IsInterface);

                foreach (MethodDesc interfaceMethod in interfaceType.GetAllMethods())
                {
                    if (interfaceMethod.Signature.IsStatic)
                        continue;

                    MethodDesc implMethod = defType.ResolveInterfaceMethodToVirtualMethodOnType(interfaceMethod);
                    if (implMethod != null)
                    {
                        yield return new CombinedDependencyListEntry(factory.VirtualMethodUse(implMethod), factory.ReadyToRunHelper(ReadyToRunHelperId.VirtualCall, interfaceMethod), "Interface method");
                        yield return new CombinedDependencyListEntry(factory.VirtualMethodUse(implMethod), factory.ReadyToRunHelper(ReadyToRunHelperId.ResolveVirtualFunction, interfaceMethod), "Interface method address");
                    }
                }
            }
        }

        protected override ISymbolNode GetBaseTypeNode(NodeFactory factory)
        {
            return _type.BaseType != null ? factory.ConstructedTypeSymbol(_type.BaseType) : null;
        }

        protected override int GCDescSize => GCDescEncoder.GetGCDescSize(_type);

        protected override void OutputGCDesc(ref ObjectDataBuilder builder)
        {
            GCDescEncoder.EncodeGCDesc(ref builder, _type);
        }

        protected override void OutputVirtualSlots(NodeFactory factory, ref ObjectDataBuilder objData, TypeDesc implType, TypeDesc declType)
        {
            declType = declType.GetClosestDefType();

            var baseType = declType.BaseType;
            if (baseType != null)
                OutputVirtualSlots(factory, ref objData, implType, baseType);

            // The generic dictionary pointer occupies the first slot of each type vtable slice
            if (declType.HasGenericDictionarySlot())
            {
                objData.EmitPointerReloc(factory.TypeGenericDictionary(declType));
            }

            // Actual vtable slots follow
            IReadOnlyList<MethodDesc> virtualSlots = factory.VTable(declType).Slots;

            for (int i = 0; i < virtualSlots.Count; i++)
            {
                MethodDesc declMethod = virtualSlots[i];

                if (declMethod.HasInstantiation)
                {
                    // Generic virtual methods will "compile", but will fail to link. Check for it here.
                    throw new NotImplementedException("VTable for " + _type + " has generic virtual methods.");
                }

                MethodDesc implMethod = implType.GetClosestDefType().FindVirtualFunctionTargetMethodOnObjectType(declMethod);
                
                if (!implMethod.IsAbstract)
                {
                    MethodDesc canonImplMethod = implMethod.GetCanonMethodTarget(CanonicalFormKind.Specific);
                    objData.EmitPointerReloc(factory.MethodEntrypoint(canonImplMethod, implMethod.OwningType.IsValueType));
                }
                else
                {
                    objData.EmitZeroPointer();
                }
            }
        }

        protected override void OutputInterfaceMap(NodeFactory factory, ref ObjectDataBuilder objData)
        {
            foreach (var itf in _type.RuntimeInterfaces)
            {
                objData.EmitPointerReloc(factory.NecessaryTypeSymbol(itf));
            }
        }

        protected override void OutputVirtualSlotAndInterfaceCount(NodeFactory factory, ref ObjectDataBuilder objData)
        {
            int virtualSlotCount = 0;
            TypeDesc currentTypeSlice = _type.GetClosestDefType();

            while (currentTypeSlice != null)
            {
                if (currentTypeSlice.HasGenericDictionarySlot())
                    virtualSlotCount++;

                virtualSlotCount += factory.VTable(currentTypeSlice).Slots.Count;
                currentTypeSlice = currentTypeSlice.BaseType;
            }

            objData.EmitShort(checked((short)virtualSlotCount));
            objData.EmitShort(checked((short)_type.RuntimeInterfaces.Length));
        }

        public static bool CreationAllowed(TypeDesc type)
        {
            switch (type.Category)
            {
                case TypeFlags.Pointer:
                case TypeFlags.FunctionPointer:
                case TypeFlags.ByRef:
                    // Pointers and byrefs are not boxable
                    return false;
                case TypeFlags.Array:
                case TypeFlags.SzArray:
                    // TODO: any validation for arrays?
                    break;

                default:
                    // Generic definition EETypes can't be allocated
                    if (type.IsGenericDefinition)
                        return false;
                    break;
            }

            return true;
        }

        public static void CheckCanGenerateConstructedEEType(NodeFactory factory, TypeDesc type)
        {
            if (!CreationAllowed(type))
                throw new TypeSystemException.TypeLoadException(ExceptionStringID.ClassLoadGeneral, type);
        }
    }
}