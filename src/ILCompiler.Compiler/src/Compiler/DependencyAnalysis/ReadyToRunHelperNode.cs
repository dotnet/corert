// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;

using Internal.TypeSystem;

using Debug = System.Diagnostics.Debug;

namespace ILCompiler.DependencyAnalysis
{
    public enum ReadyToRunHelperId
    {
        Invalid,
        NewHelper,
        NewArr1,
        VirtualCall,
        IsInstanceOf,
        CastClass,
        GetNonGCStaticBase,
        GetGCStaticBase,
        GetThreadStaticBase,
        DelegateCtor,
        InterfaceDispatch,
        ResolveVirtualFunction,
        GenericLookupFromThis,
        GenericLookupFromDictionary,
    }

    public partial class ReadyToRunHelperNode : AssemblyStubNode
    {
        private ReadyToRunHelperId _id;
        private Object _target;

        public ReadyToRunHelperNode(ReadyToRunHelperId id, Object target)
        {
            _id = id;
            _target = target;
        }

        public override string GetName()
        {
            return ((ISymbolNode)this).MangledName;
        }

        public ReadyToRunHelperId Id
        {
            get
            {
                return _id;
            }
        }

        public Object Target
        {
            get
            {
                return _target;
            }
        }

        public override string MangledName
        {
            get
            {
                switch (_id)
                {
                    case ReadyToRunHelperId.NewHelper:
                        return "__NewHelper_" + NodeFactory.NameMangler.GetMangledTypeName((TypeDesc)_target);
                    case ReadyToRunHelperId.NewArr1:
                        return "__NewArr1_" + NodeFactory.NameMangler.GetMangledTypeName((TypeDesc)_target);
                    case ReadyToRunHelperId.VirtualCall:
                        return "__VirtualCall_" + NodeFactory.NameMangler.GetMangledMethodName((MethodDesc)_target);
                    case ReadyToRunHelperId.IsInstanceOf:
                        return "__IsInstanceOf_" + NodeFactory.NameMangler.GetMangledTypeName((TypeDesc)_target);
                    case ReadyToRunHelperId.CastClass:
                        return "__CastClass_" + NodeFactory.NameMangler.GetMangledTypeName((TypeDesc)_target);
                    case ReadyToRunHelperId.GetNonGCStaticBase:
                        return "__GetNonGCStaticBase_" + NodeFactory.NameMangler.GetMangledTypeName((TypeDesc)_target);
                    case ReadyToRunHelperId.GetGCStaticBase:
                        return "__GetGCStaticBase_" + NodeFactory.NameMangler.GetMangledTypeName((TypeDesc)_target);
                    case ReadyToRunHelperId.GetThreadStaticBase:
                        return "__GetThreadStaticBase_" + NodeFactory.NameMangler.GetMangledTypeName((TypeDesc)_target);
                    case ReadyToRunHelperId.DelegateCtor:
                        {
                            var createInfo = (DelegateCreationInfo)_target;
                            string mangledName = String.Concat("__DelegateCtor_",
                                createInfo.Constructor.MangledName, "__", createInfo.Target.MangledName);
                            if (createInfo.Thunk != null)
                                mangledName += String.Concat("__", createInfo.Thunk.MangledName);
                            return mangledName;
                        }
                    case ReadyToRunHelperId.InterfaceDispatch:
                        return "__InterfaceDispatch_" + NodeFactory.NameMangler.GetMangledMethodName((MethodDesc)_target);
                    case ReadyToRunHelperId.ResolveVirtualFunction:
                        return "__ResolveVirtualFunction_" + NodeFactory.NameMangler.GetMangledMethodName((MethodDesc)_target);
                    case ReadyToRunHelperId.GenericLookupFromThis:
                        {
                            var lookupInfo = (GenericLookupDescriptor)_target;

                            string mangledContextName;
                            if (lookupInfo.CanonicalOwner is MethodDesc)
                                mangledContextName = NodeFactory.NameMangler.GetMangledMethodName((MethodDesc)lookupInfo.CanonicalOwner);
                            else
                                mangledContextName = NodeFactory.NameMangler.GetMangledTypeName((TypeDesc)lookupInfo.CanonicalOwner);

                            return string.Concat("__GenericLookupFromThis_", mangledContextName, "_", lookupInfo.Signature.GetMangledName(NodeFactory.NameMangler));
                        }
                    case ReadyToRunHelperId.GenericLookupFromDictionary:
                        {
                            var lookupInfo = (GenericLookupDescriptor)_target;

                            string mangledContextName;
                            if (lookupInfo.CanonicalOwner is MethodDesc)
                                mangledContextName = NodeFactory.NameMangler.GetMangledMethodName((MethodDesc)lookupInfo.CanonicalOwner);
                            else
                                mangledContextName = NodeFactory.NameMangler.GetMangledTypeName((TypeDesc)lookupInfo.CanonicalOwner);

                            return string.Concat("__GenericLookupFromDict_", mangledContextName, "_", lookupInfo.Signature.GetMangledName(NodeFactory.NameMangler));
                        }
                    default:
                        throw new NotImplementedException();
                }
            }
        }

        public override bool ShouldShareNodeAcrossModules(NodeFactory factory)
        {
            return true;
        }

        protected override DependencyList ComputeNonRelocationBasedDependencies(NodeFactory factory)
        {
            if (_id == ReadyToRunHelperId.VirtualCall)
            {
                DependencyList dependencyList = new DependencyList();
                dependencyList.Add(factory.VirtualMethodUse((MethodDesc)_target), "ReadyToRun Virtual Method Call");
                dependencyList.Add(factory.VTable(((MethodDesc)_target).OwningType), "ReadyToRun Virtual Method Call Target VTable");
                return dependencyList;
            }
            else if (_id == ReadyToRunHelperId.InterfaceDispatch)
            {
                DependencyList dependencyList = new DependencyList();
                dependencyList.Add(factory.VirtualMethodUse((MethodDesc)_target), "ReadyToRun Interface Method Call");
                return dependencyList;
            }
            else if (_id == ReadyToRunHelperId.ResolveVirtualFunction)
            {
                DependencyList dependencyList = new DependencyList();
                dependencyList.Add(factory.VirtualMethodUse((MethodDesc)_target), "ReadyToRun Virtual Method Address Load");
                return dependencyList;
            }
            else
            {
                return null;
            }
        }

        protected override void OnMarked(NodeFactory factory)
        {
            switch (_id)
            {
                case ReadyToRunHelperId.GenericLookupFromThis:
                case ReadyToRunHelperId.GenericLookupFromDictionary:
                    {
                        // When the helper call gets marked, ensure the generic layout for the associated dictionaries
                        // includes the signature.
                        var lookupInfo = (GenericLookupDescriptor)_target;
                        factory.GenericDictionaryLayout(lookupInfo.CanonicalOwner).EnsureEntry(lookupInfo.Signature);
                    }
                    break;
            }
        }
    }

    public struct GenericLookupDescriptor : IEquatable<GenericLookupDescriptor>
    {
        public readonly TypeSystemEntity CanonicalOwner;

        public readonly DictionaryEntry Signature;

        public GenericLookupDescriptor(TypeSystemEntity canonicalOwner, DictionaryEntry signature)
        {
            // Owner should be a canonical type or canonical method
            Debug.Assert((
                canonicalOwner is TypeDesc &&
                    ((TypeDesc)canonicalOwner).IsCanonicalSubtype(CanonicalFormKind.Any))
                || (canonicalOwner is MethodDesc &&
                    ((MethodDesc)canonicalOwner).HasInstantiation && ((MethodDesc)canonicalOwner).IsSharedByGenericInstantiations));
            
            CanonicalOwner = canonicalOwner;
            Signature = signature;
        }

        public bool Equals(GenericLookupDescriptor other)
        {
            if (CanonicalOwner != other.CanonicalOwner)
                return false;

            if (!Signature.Equals(other.Signature))
                return false;

            return true;
        }

        public override bool Equals(object obj)
        {
            return obj is GenericLookupDescriptor && Equals((GenericLookupDescriptor)obj);
        }

        public override int GetHashCode()
        {
            int hash = 67;
            hash = hash * 31 + CanonicalOwner.GetHashCode();
            hash = hash * 31 + Signature.GetHashCode();
            return hash;
        }

        public override string ToString()
        {
            return String.Concat(
                "Lookup for ",
                CanonicalOwner.ToString(),
                ". Target: ",
                Signature.ToString()
                );
        }
    }
}
