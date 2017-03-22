// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Collections.Generic;
using ILCompiler.DependencyAnalysisFramework;
using Internal.Text;
using Internal.TypeSystem;

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

        protected override DependencyList ComputeNonRelocationBasedDependencies(NodeFactory factory)
        {
            DependencyList dependencies = new DependencyNodeCore<NodeFactory>.DependencyList();

            // When methods use generic lookup nodes, make sure they're also available as template dictionary nodes
            if (_dictionaryOwner is MethodDesc)
            {
                dependencies.Add(factory.NativeLayout.TemplateMethodLayout((MethodDesc)_dictionaryOwner), "Type loader template");
                dependencies.Add(_lookupSignature.TemplateDictionaryNode(factory), "Type loader template");
            }
            else
            {
                DefType actualTemplateType = GenericTypesTemplateMap.GetActualTemplateTypeForType(factory, (TypeDesc)_dictionaryOwner);
                dependencies.Add(factory.NativeLayout.TemplateTypeLayout(actualTemplateType), "Type loader template");
                dependencies.Add(_lookupSignature.TemplateDictionaryNode(factory), "Type loader template");
            }

            if (_id == ReadyToRunHelperId.GetGCStaticBase || _id == ReadyToRunHelperId.GetThreadStaticBase)
            {
                // If the type has a lazy static constructor, we also need the non-GC static base to be available as
                // a template dictionary node.
                TypeDesc type = (TypeDesc)_target;

                if (factory.TypeSystemContext.HasLazyStaticConstructor(type))
                {
                    GenericLookupResult nonGcRegionLookup = factory.GenericLookup.TypeNonGCStaticBase(type);
                    dependencies.Add(nonGcRegionLookup.TemplateDictionaryNode(factory), "Type loader template");
                }
            }

            return dependencies;
        }

        public IEnumerable<DependencyListEntry> InstantiateDependencies(NodeFactory factory, Instantiation typeInstantiation, Instantiation methodInstantiation)
        {
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
                            return new[] {
                                new DependencyListEntry(
                                    factory.GenericLookup.TypeNonGCStaticBase(type).GetTarget(factory, typeInstantiation, methodInstantiation, null),
                                    "Dictionary dependency"),
                                new DependencyListEntry(
                                    _lookupSignature.GetTarget(factory, typeInstantiation, methodInstantiation, null),
                                    "Dictionary dependency") };
                        }
                    }
                    break;
            }

            // All other generic lookups just depend on the thing they point to
            return new[] { new DependencyListEntry(
                        _lookupSignature.GetTarget(factory, typeInstantiation, methodInstantiation, null),
                        "Dictionary dependency") };
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
            _lookupSignature.AppendMangledName(nameMangler, sb);
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
            _lookupSignature.AppendMangledName(nameMangler, sb);
        }
    }
}
