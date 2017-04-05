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

        protected override string GetName(NodeFactory factory) => this.GetMangledName(factory.NameMangler) + " constructed";

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

            // Ensure that we track the necessary type symbol if we are working with a constructed type symbol.
            // The emitter will ensure we don't emit both, but this allows us assert that we only generate
            // relocs to nodes we emit.
            dependencyList.Add(factory.NecessaryTypeSymbol(_type), "NecessaryType for constructed type");

            DefType closestDefType = _type.GetClosestDefType();

            if (TrackInterfaceDispatchMapDepenendency && _type.RuntimeInterfaces.Length > 0)
            {
                dependencyList.Add(factory.InterfaceDispatchMap(_type), "Interface dispatch map");
            }

            if (_type.RuntimeInterfaces.Length > 0 && !factory.CompilationModuleGroup.ShouldProduceFullVTable(_type))
            {
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
                    bool allInterfaceMethodsAreImplicitlyUsed = false;
                    if (implementedInterface.HasVariance)
                    {
                        TypeDesc interfaceDefinition = implementedInterface.GetTypeDefinition();
                        for (int i = 0; i < interfaceDefinition.Instantiation.Length; i++)
                        {
                            if (((GenericParameterDesc)interfaceDefinition.Instantiation[i]).Variance != 0 &&
                                !implementedInterface.Instantiation[i].IsValueType)
                            {
                                allInterfaceMethodsAreImplicitlyUsed = true;
                                break;
                            }
                        }
                    }
                    if (!allInterfaceMethodsAreImplicitlyUsed && 
                        (_type.IsArray || _type.GetTypeDefinition() == factory.ArrayOfTEnumeratorType) &&
                        implementedInterface.HasInstantiation)
                    {
                        // NOTE: we need to also do this for generic interfaces on arrays because they have a weird casting rule
                        // that doesn't require the implemented interface to be variant to consider it castable.
                        // For value types, we only need this when the array is castable by size (int[] and ICollection<uint>),
                        // or it's a reference type (Derived[] and ICollection<Base>).
                        TypeDesc elementType = _type.IsArray ? ((ArrayType)_type).ElementType : _type.Instantiation[0];
                        allInterfaceMethodsAreImplicitlyUsed =
                            CastingHelper.IsArrayElementTypeCastableBySize(elementType) ||
                            (elementType.IsDefType && !elementType.IsValueType);
                    }

                    if (allInterfaceMethodsAreImplicitlyUsed)
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
                // Add a dependency on the template for this type, if the canonical type should be generated into this binary.
                DefType templateType = GenericTypesTemplateMap.GetActualTemplateTypeForType(factory, _type.ConvertToCanonForm(CanonicalFormKind.Specific));

                if (templateType.IsCanonicalSubtype(CanonicalFormKind.Any) && !factory.NecessaryTypeSymbol(templateType).RepresentsIndirectionCell)
                    dependencyList.Add(factory.NativeLayout.TemplateTypeLayout(templateType), "Template Type Layout");
            }

            // Generated type contains generic virtual methods that will get added to the GVM tables
            if (TypeGVMEntriesNode.TypeNeedsGVMTableEntries(_type))
            {
                dependencyList.Add(new DependencyListEntry(factory.TypeGVMEntries(_type), "Type with generic virtual methods"));
            }

            if (factory.TypeSystemContext.HasLazyStaticConstructor(_type))
            {
                // The fact that we generated an EEType means that someone can call RuntimeHelpers.RunClassConstructor.
                // We need to make sure this is possible.
                dependencyList.Add(new DependencyListEntry(factory.TypeNonGCStaticsSymbol((MetadataType)_type), "Class constructor"));
            }

            // Dependencies of the StaticsInfoHashTable and the ReflectionFieldAccessMap
            if (_type is MetadataType)
            {
                MetadataType metadataType = (MetadataType)_type;

                // NOTE: The StaticsInfoHashtable entries need to reference the gc and non-gc static nodes through an indirection cell.
                // The StaticsInfoHashtable entries only exist for static fields on generic types.

                if (metadataType.GCStaticFieldSize.AsInt > 0)
                {
                    ISymbolNode gcStatics = factory.TypeGCStaticsSymbol(metadataType);
                    dependencyList.Add(_type.HasInstantiation ? factory.Indirection(gcStatics) : gcStatics, "GC statics indirection for StaticsInfoHashtable");
                }
                if (metadataType.NonGCStaticFieldSize.AsInt > 0)
                {
                    ISymbolNode nonGCStatic = factory.TypeNonGCStaticsSymbol(metadataType);
                    if (_type.HasInstantiation)
                    {
                        // The entry in the StaticsInfoHashtable points at the begining of the static fields data, so we need to add
                        // the cctor context offset to the indirection cell.

                        int cctorOffset = 0;
                        if (factory.TypeSystemContext.HasLazyStaticConstructor(metadataType))
                            cctorOffset += NonGCStaticsNode.GetClassConstructorContextStorageSize(factory.TypeSystemContext.Target, metadataType);

                        nonGCStatic = factory.Indirection(nonGCStatic, cctorOffset);
                    }
                    dependencyList.Add(nonGCStatic, "Non-GC statics indirection for StaticsInfoHashtable");
                }

                // TODO: TLS dependencies
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

            // If we're producing a full vtable, none of the dependencies are conditional.
            if (!factory.CompilationModuleGroup.ShouldProduceFullVTable(defType))
            {
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
                            yield return new CombinedDependencyListEntry(factory.VirtualMethodUse(implMethod), factory.VirtualMethodUse(interfaceMethod), "Interface method");
                        }
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

                    // Byref-like types have interior pointers and cannot be heap allocated.
                    if (type.IsValueType && ((DefType)type).IsByRefLike)
                        return false;

                    // The global "<Module>" type can never be allocated.
                    if (((MetadataType)type).IsModuleType)
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