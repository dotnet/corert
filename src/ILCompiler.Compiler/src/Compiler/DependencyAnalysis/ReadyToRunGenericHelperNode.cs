// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Collections.Generic;
using System.Diagnostics;

using Internal.Text;
using Internal.TypeSystem;

using ILCompiler.DependencyAnalysisFramework;

namespace ILCompiler.DependencyAnalysis
{
    public abstract partial class ReadyToRunGenericHelperNode : AssemblyStubNode, INodeWithRuntimeDeterminedDependencies
    {
        private ReadyToRunHelperId _id;
        private object _target;
        protected TypeSystemEntity _dictionaryOwner;
        protected GenericLookupResult _lookupSignature;

        public ReadyToRunGenericHelperNode(NodeFactory factory, ReadyToRunHelperId helperId, object target, TypeSystemEntity dictionaryOwner)
        {
            _id = helperId;
            _dictionaryOwner = dictionaryOwner;
            _target = target;

            _lookupSignature = GetLookupSignature(factory, helperId, target);
        }

        private static GenericLookupResult GetLookupSignature(NodeFactory factory, ReadyToRunHelperId id, object target)
        {
            switch (id)
            {
                case ReadyToRunHelperId.TypeHandle:
                    return factory.GenericLookup.Type((TypeDesc)target);
                case ReadyToRunHelperId.MethodHandle:
                    return factory.GenericLookup.MethodHandle((MethodDesc)target);
                case ReadyToRunHelperId.FieldHandle:
                    return factory.GenericLookup.FieldHandle((FieldDesc)target);
                case ReadyToRunHelperId.GetGCStaticBase:
                    return factory.GenericLookup.TypeGCStaticBase((TypeDesc)target);
                case ReadyToRunHelperId.GetNonGCStaticBase:
                    return factory.GenericLookup.TypeNonGCStaticBase((TypeDesc)target);
                case ReadyToRunHelperId.GetThreadStaticBase:
                    return factory.GenericLookup.TypeThreadStaticBaseIndex((TypeDesc)target);
                case ReadyToRunHelperId.MethodDictionary:
                    return factory.GenericLookup.MethodDictionary((MethodDesc)target);
                case ReadyToRunHelperId.VirtualCall:
                    return factory.GenericLookup.VirtualCall((MethodDesc)target);
                case ReadyToRunHelperId.ResolveVirtualFunction:
                    return factory.GenericLookup.VirtualMethodAddress((MethodDesc)target);
                case ReadyToRunHelperId.MethodEntry:
                    return factory.GenericLookup.MethodEntry((MethodDesc)target);
                case ReadyToRunHelperId.DelegateCtor:
                    return ((DelegateCreationInfo)target).GetLookupKind(factory);
                default:
                    throw new NotImplementedException();
            }
        }

        protected sealed override string GetName(NodeFactory factory) => this.GetMangledName(factory.NameMangler);
        public override bool IsShareable => true;

        protected sealed override void OnMarked(NodeFactory factory)
        {
            // When the helper call gets marked, ensure the generic layout for the associated dictionaries
            // includes the signature.
            factory.GenericDictionaryLayout(_dictionaryOwner).EnsureEntry(_lookupSignature);

            if ((_id == ReadyToRunHelperId.GetGCStaticBase || _id == ReadyToRunHelperId.GetThreadStaticBase) &&
                factory.TypeSystemContext.HasLazyStaticConstructor((TypeDesc)_target))
            {
                // If the type has a lazy static constructor, we also need the non-GC static base
                // because that's where the class constructor context is.
                factory.GenericDictionaryLayout(_dictionaryOwner).EnsureEntry(factory.GenericLookup.TypeNonGCStaticBase((TypeDesc)_target));
            }
        }

        public IEnumerable<DependencyListEntry> InstantiateDependencies(NodeFactory factory, Instantiation typeInstantiation, Instantiation methodInstantiation)
        {
            ArrayBuilder<DependencyListEntry> result = new ArrayBuilder<DependencyListEntry>();

            switch (_id)
            {
                case ReadyToRunHelperId.GetGCStaticBase:
                case ReadyToRunHelperId.GetThreadStaticBase:
                    {
                        // If the type has a lazy static constructor, we also need the non-GC static base
                        // because that's where the class constructor context is.
                        TypeDesc type = (TypeDesc)_target;

                        if (factory.TypeSystemContext.HasLazyStaticConstructor(type))
                        {
                            // TODO: Make _dictionaryOwner a RuntimeDetermined type/method and make a substitution with 
                            // typeInstantiation/methodInstantiation to get a concrete type. Then pass the generic dictionary
                            // node of the concrete type to the GetTarget call. Also change the signature of GetTarget to 
                            // take only the factory and dictionary as input.
                            result.Add(
                                new DependencyListEntry(
                                    factory.GenericLookup.TypeNonGCStaticBase(type).GetTarget(factory, typeInstantiation, methodInstantiation, null),
                                    "Dictionary dependency"));
                        }
                    }
                    break;

                case ReadyToRunHelperId.DelegateCtor:
                    {
                        DelegateCreationInfo createInfo = (DelegateCreationInfo)_target;
                        if (createInfo.NeedsVirtualMethodUseTracking)
                        {
                            MethodDesc instantiatedTargetMethod = createInfo.TargetMethod.GetNonRuntimeDeterminedMethodFromRuntimeDeterminedMethodViaSubstitution(typeInstantiation, methodInstantiation);
                            if (!factory.CompilationModuleGroup.ShouldProduceFullVTable(instantiatedTargetMethod.OwningType))
                            {
                                result.Add(
                                    new DependencyListEntry(
                                        factory.VirtualMethodUse(instantiatedTargetMethod),
                                        "Dictionary dependency"));
                            }

                            // TODO: https://github.com/dotnet/corert/issues/3224 
                            if (instantiatedTargetMethod.IsAbstract)
                            {
                                result.Add(new DependencyListEntry(factory.ReflectableMethod(instantiatedTargetMethod), "Abstract reflectable method"));
                            }
                        }
                    }
                    break;

                case ReadyToRunHelperId.ResolveVirtualFunction:
                    {
                        MethodDesc instantiatedTarget = ((MethodDesc)_target).GetNonRuntimeDeterminedMethodFromRuntimeDeterminedMethodViaSubstitution(typeInstantiation, methodInstantiation);
                        if (!factory.CompilationModuleGroup.ShouldProduceFullVTable(instantiatedTarget.OwningType))
                        {
                            result.Add(
                                new DependencyListEntry(
                                    factory.VirtualMethodUse(instantiatedTarget),
                                    "Dictionary dependency"));
                        }

                        // TODO: https://github.com/dotnet/corert/issues/3224 
                        if (instantiatedTarget.IsAbstract)
                        {
                            result.Add(new DependencyListEntry(factory.ReflectableMethod(instantiatedTarget), "Abstract reflectable method"));
                        }
                    }
                    break;
            }

            // All generic lookups depend on the thing they point to
            result.Add(new DependencyListEntry(
                        _lookupSignature.GetTarget(factory, typeInstantiation, methodInstantiation, null),
                        "Dictionary dependency"));

            return result.ToArray();
        }

        protected void AppendLookupSignatureMangledName(NameMangler nameMangler, Utf8StringBuilder sb)
        {
            if (_id != ReadyToRunHelperId.DelegateCtor)
            {
                _lookupSignature.AppendMangledName(nameMangler, sb);
            }
            else
            {
                ((DelegateCreationInfo)_target).AppendMangledName(nameMangler, sb);
            }
        }

        protected override DependencyList ComputeNonRelocationBasedDependencies(NodeFactory factory)
        {
            DependencyList dependencies = new DependencyList();
            foreach (DependencyNodeCore<NodeFactory> dependency in _lookupSignature.NonRelocDependenciesFromUsage(factory))
            {
                dependencies.Add(new DependencyListEntry(dependency, "GenericLookupResultDependency"));
            }

            // Root the template for the type while we're hitting its dictionary cells. In the future, we may
            // want to control this via type reflectability instead.
            if (_dictionaryOwner is MethodDesc)
            {
                dependencies.Add(factory.NativeLayout.TemplateMethodLayout((MethodDesc)_dictionaryOwner), "Type loader template");
            }
            else
            {
                dependencies.Add(factory.NativeLayout.TemplateTypeLayout((TypeDesc)_dictionaryOwner), "Type loader template");
            }

            return dependencies;
        }

        public override bool HasConditionalStaticDependencies => true;
        public override IEnumerable<CombinedDependencyListEntry> GetConditionalStaticDependencies(NodeFactory factory)
        {
            List<CombinedDependencyListEntry> conditionalDependencies = new List<CombinedDependencyListEntry>();
            NativeLayoutSavedVertexNode templateLayout;
            if (_dictionaryOwner is MethodDesc)
            {
                templateLayout = factory.NativeLayout.TemplateMethodLayout((MethodDesc)_dictionaryOwner);
                conditionalDependencies.Add(new CombinedDependencyListEntry(_lookupSignature.TemplateDictionaryNode(factory),
                                                                templateLayout,
                                                                "Type loader template"));
            }
            else
            {
                templateLayout = factory.NativeLayout.TemplateTypeLayout((TypeDesc)_dictionaryOwner);
                conditionalDependencies.Add(new CombinedDependencyListEntry(_lookupSignature.TemplateDictionaryNode(factory),
                                                                templateLayout,
                                                                "Type loader template"));
            }

            if (_id == ReadyToRunHelperId.GetGCStaticBase || _id == ReadyToRunHelperId.GetThreadStaticBase)
            {
                // If the type has a lazy static constructor, we also need the non-GC static base to be available as
                // a template dictionary node.
                TypeDesc type = (TypeDesc)_target;
                Debug.Assert(templateLayout != null);
                if (factory.TypeSystemContext.HasLazyStaticConstructor(type))
                {
                    GenericLookupResult nonGcRegionLookup = factory.GenericLookup.TypeNonGCStaticBase(type);
                    conditionalDependencies.Add(new CombinedDependencyListEntry(nonGcRegionLookup.TemplateDictionaryNode(factory),
                                                                templateLayout,
                                                                "Type loader template"));
                }
            }
            
            return conditionalDependencies;
        }
    }

    public partial class ReadyToRunGenericLookupFromDictionaryNode : ReadyToRunGenericHelperNode
    {
        public ReadyToRunGenericLookupFromDictionaryNode(NodeFactory factory, ReadyToRunHelperId helperId, object target, TypeSystemEntity dictionaryOwner)
            : base(factory, helperId, target, dictionaryOwner)
        {
        }

        public override void AppendMangledName(NameMangler nameMangler, Utf8StringBuilder sb)
        {
            Utf8String mangledContextName;
            if (_dictionaryOwner is MethodDesc)
                mangledContextName = nameMangler.GetMangledMethodName((MethodDesc)_dictionaryOwner);
            else
                mangledContextName = nameMangler.GetMangledTypeName((TypeDesc)_dictionaryOwner);

            sb.Append("__GenericLookupFromDict_").Append(mangledContextName).Append("_");
            AppendLookupSignatureMangledName(nameMangler, sb);
        }
    }

    public partial class ReadyToRunGenericLookupFromTypeNode : ReadyToRunGenericHelperNode
    {
        public ReadyToRunGenericLookupFromTypeNode(NodeFactory factory, ReadyToRunHelperId helperId, object target, TypeSystemEntity dictionaryOwner)
            : base(factory, helperId, target, dictionaryOwner)
        {
        }

        public override void AppendMangledName(NameMangler nameMangler, Utf8StringBuilder sb)
        {
            Utf8String mangledContextName;
            if (_dictionaryOwner is MethodDesc)
                mangledContextName = nameMangler.GetMangledMethodName((MethodDesc)_dictionaryOwner);
            else
                mangledContextName = nameMangler.GetMangledTypeName((TypeDesc)_dictionaryOwner);

            sb.Append("__GenericLookupFromType_").Append(mangledContextName).Append("_");
            AppendLookupSignatureMangledName(nameMangler, sb);
        }
    }
}
