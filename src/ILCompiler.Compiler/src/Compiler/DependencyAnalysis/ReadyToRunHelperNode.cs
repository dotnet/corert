// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Collections.Generic;
using ILCompiler.DependencyAnalysisFramework;
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
        ResolveVirtualFunction,

        TypeHandle,
        FieldHandle,
        MethodDictionary,
        MethodEntry,
    }

    public partial class ReadyToRunHelperNode : AssemblyStubNode
    {
        private ReadyToRunHelperId _id;
        private Object _target;

        public ReadyToRunHelperNode(NodeFactory factory, ReadyToRunHelperId id, Object target)
        {
            _id = id;
            _target = target;

            switch (id)
            {
                case ReadyToRunHelperId.NewHelper:
                case ReadyToRunHelperId.NewArr1:
                case ReadyToRunHelperId.IsInstanceOf:
                case ReadyToRunHelperId.CastClass:
                    {
                        // Make sure that if the EEType can't be generated, we throw the exception now.
                        // This way we can fail generating code for the method that references the EEType
                        // and (depending on the policy), we could avoid scraping the entire compilation.
                        TypeDesc type = (TypeDesc)target;
                        factory.NecessaryTypeSymbol(type);
                    }
                    break;
                case ReadyToRunHelperId.GetNonGCStaticBase:
                case ReadyToRunHelperId.GetGCStaticBase:
                case ReadyToRunHelperId.GetThreadStaticBase:
                    {
                        // Make sure we can compute static field layout now so we can fail early
                        DefType defType = (DefType)target;
                        defType.ComputeStaticFieldLayout(StaticLayoutKind.StaticRegionSizesAndFields);
                    }
                    break;
            }
        }

        protected override string GetName()
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
                    case ReadyToRunHelperId.ResolveVirtualFunction:
                        return "__ResolveVirtualFunction_" + NodeFactory.NameMangler.GetMangledMethodName((MethodDesc)_target);
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
    }
}
