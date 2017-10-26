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

        public static GenericLookupResult GetLookupSignature(NodeFactory factory, ReadyToRunHelperId id, object target)
        {
            // Necessary type handle is not something you can put in a dictionary - someone should have normalized to TypeHandle
            Debug.Assert(id != ReadyToRunHelperId.NecessaryTypeHandle);

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
                case ReadyToRunHelperId.VirtualDispatchCell:
                    return factory.GenericLookup.VirtualDispatchCell((MethodDesc)target);
                case ReadyToRunHelperId.MethodEntry:
                    return factory.GenericLookup.MethodEntry((MethodDesc)target);
                case ReadyToRunHelperId.DelegateCtor:
                    return ((DelegateCreationInfo)target).GetLookupKind(factory);
                case ReadyToRunHelperId.DefaultConstructor:
                    return factory.GenericLookup.DefaultCtorLookupResult((TypeDesc)target);
                default:
                    throw new NotImplementedException();
            }
        }

        protected sealed override string GetName(NodeFactory factory) => this.GetMangledName(factory.NameMangler);
        public override bool IsShareable => true;

        protected sealed override void OnMarked(NodeFactory factory)
        {
            DictionaryLayoutNode layout = factory.GenericDictionaryLayout(_dictionaryOwner);

            if (layout.HasUnfixedSlots)
            {
                // When the helper call gets marked, ensure the generic layout for the associated dictionaries
                // includes the signature.
                layout.EnsureEntry(_lookupSignature);

                if ((_id == ReadyToRunHelperId.GetGCStaticBase || _id == ReadyToRunHelperId.GetThreadStaticBase) &&
                    factory.TypeSystemContext.HasLazyStaticConstructor((TypeDesc)_target))
                {
                    // If the type has a lazy static constructor, we also need the non-GC static base
                    // because that's where the class constructor context is.
                    layout.EnsureEntry(factory.GenericLookup.TypeNonGCStaticBase((TypeDesc)_target));
                }
            }
        }

        public IEnumerable<DependencyListEntry> InstantiateDependencies(NodeFactory factory, Instantiation typeInstantiation, Instantiation methodInstantiation)
        {
            ArrayBuilder<DependencyListEntry> result = new ArrayBuilder<DependencyListEntry>();

            var lookupContext = new GenericLookupResultContext(_dictionaryOwner, typeInstantiation, methodInstantiation);

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
                            result.Add(
                                new DependencyListEntry(
                                    factory.GenericLookup.TypeNonGCStaticBase(type).GetTarget(factory, lookupContext),
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
                            if (!factory.VTable(instantiatedTargetMethod.OwningType).HasFixedSlots)
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
            }

            // All generic lookups depend on the thing they point to
            result.Add(new DependencyListEntry(
                        _lookupSignature.GetTarget(factory, lookupContext),
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

            dependencies.Add(factory.GenericDictionaryLayout(_dictionaryOwner), "Layout");

            foreach (DependencyNodeCore<NodeFactory> dependency in _lookupSignature.NonRelocDependenciesFromUsage(factory))
            {
                dependencies.Add(new DependencyListEntry(dependency, "GenericLookupResultDependency"));
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
