// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Collections.Generic;
using System.Diagnostics;

using Internal.Runtime;
using Internal.Text;
using Internal.TypeSystem;
using Internal.IL;

namespace ILCompiler.DependencyAnalysis
{
    public class ConstructedEETypeNode : EETypeNode
    {
        public ConstructedEETypeNode(NodeFactory factory, TypeDesc type) : base(factory, type)
        {
            Debug.Assert(!type.IsCanonicalDefinitionType(CanonicalFormKind.Any));
            CheckCanGenerateConstructedEEType(factory, type);
        }

        protected override string GetName() => this.GetMangledName() + " constructed";

        public override bool ShouldSkipEmittingObjectNode(NodeFactory factory) => false;

        protected override bool EmitVirtualSlotsAndInterfaces => true;

        public override bool InterestingForDynamicDependencyAnalysis
        {
            get
            {
                return _type.IsDefType && _type.HasGenericVirtualMethod();
            }
        }

        protected virtual bool TrackInterfaceDispatchMapDepenendency => true;

        protected override DependencyList ComputeNonRelocationBasedDependencies(NodeFactory factory)
        {
            DependencyList dependencyList = base.ComputeNonRelocationBasedDependencies(factory);

            DefType closestDefType = _type.GetClosestDefType();

            if (_type.RuntimeInterfaces.Length > 0)
            {
                if (TrackInterfaceDispatchMapDepenendency)
                {
                    dependencyList.Add(factory.InterfaceDispatchMap(_type), "Interface dispatch map");
                }

                foreach (var implementedInterface in _type.RuntimeInterfaces)
                {
                    // If the type implements ICastable, the methods are implicitly necessary
                    if (implementedInterface == factory.ICastableInterface)
                    {
                        MethodDesc isInstDecl = implementedInterface.GetKnownMethod("IsInstanceOfInterface", null);
                        MethodDesc getImplTypeDecl = implementedInterface.GetKnownMethod("GetImplType", null);

                        MethodDesc isInstMethodImpl = _type.ResolveInterfaceMethodTarget(isInstDecl);
                        MethodDesc getImplTypeMethodImpl = _type.ResolveInterfaceMethodTarget(getImplTypeDecl);

                        if (isInstMethodImpl != null)
                            dependencyList.Add(factory.VirtualMethodUse(isInstMethodImpl), "ICastable IsInst");
                        if (getImplTypeMethodImpl != null)
                            dependencyList.Add(factory.VirtualMethodUse(getImplTypeMethodImpl), "ICastable GetImplType");
                    }

                    // If any of the implemented interfaces have variance, calls against compatible interface methods
                    // could result in interface methods of this type being used (e.g. IEnumberable<object>.GetEnumerator()
                    // can dispatch to an implementation of IEnumerable<string>.GetEnumerator()).
                    // For now, we will not try to optimize this and we will pretend all interface methods are necessary.
                    // NOTE: we need to also do this for generic interfaces on arrays because they have a weird casting rule
                    // that doesn't require the implemented interface to be variant to consider it castable.
                    if (implementedInterface.HasVariance || (_type.IsArray && implementedInterface.HasInstantiation))
                    {
                        foreach (var interfaceMethod in implementedInterface.GetAllMethods())
                        {
                            if (interfaceMethod.Signature.IsStatic)
                                continue;

                            // Generic virtual methods are tracked by an orthogonal mechanism.
                            if (interfaceMethod.HasInstantiation)
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

            // Generated type contains generic virtual methods that will get added to the GVM tables
            if (TypeGVMEntriesNode.TypeNeedsGVMTableEntries(_type))
            {
                dependencyList.Add(new DependencyListEntry(factory.TypeGVMEntries(_type), "Type with generic virtual methods"));
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
                // Generic virtual methods are tracked by an orthogonal mechanism.
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

                    // Generic virtual methods are tracked by an orthogonal mechanism.
                    if (interfaceMethod.HasInstantiation)
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

                    // Full EEtype of System.Canon should never be used.
                    if (type.IsCanonicalDefinitionType(CanonicalFormKind.Any))
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