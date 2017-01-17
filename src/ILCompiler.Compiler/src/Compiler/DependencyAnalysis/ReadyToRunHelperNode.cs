// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;

using Internal.Text;
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
        ResolveGenericVirtualMethod,

        // The following helpers are used for generic lookups only
        TypeHandle,
        FieldHandle,
        MethodDictionary,
        MethodEntry
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
                    {
                        // Make sure that if the EEType can't be generated, we throw the exception now.
                        // This way we can fail generating code for the method that references the EEType
                        // and (depending on the policy), we could avoid scraping the entire compilation.
                        TypeDesc type = (TypeDesc)target;
                        factory.ConstructedTypeSymbol(type);
                    }
                    break;
                case ReadyToRunHelperId.IsInstanceOf:
                case ReadyToRunHelperId.CastClass:
                    {
                        // Make sure that if the EEType can't be generated, we throw the exception now.
                        // This way we can fail generating code for the method that references the EEType
                        // and (depending on the policy), we could avoid scraping the entire compilation.
                        TypeDesc type = (TypeDesc)target;
                        factory.NecessaryTypeSymbol(type);

                        Debug.Assert(!type.IsNullable, "Nullable needs to be unwrapped");
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

        protected override string GetName() => this.GetMangledName();

        public ReadyToRunHelperId Id => _id;
        public Object Target =>  _target;

        public override void AppendMangledName(NameMangler nameMangler, Utf8StringBuilder sb)
        {
            switch (_id)
            {
                case ReadyToRunHelperId.NewHelper:
                    sb.Append("__NewHelper_").Append(NodeFactory.NameMangler.GetMangledTypeName((TypeDesc)_target));
                    break;
                case ReadyToRunHelperId.NewArr1:
                    sb.Append("__NewArr1_").Append(NodeFactory.NameMangler.GetMangledTypeName((TypeDesc)_target));
                    break;
                case ReadyToRunHelperId.VirtualCall:
                    sb.Append("__VirtualCall_").Append(NodeFactory.NameMangler.GetMangledMethodName((MethodDesc)_target));
                    break;
                case ReadyToRunHelperId.IsInstanceOf:
                    sb.Append("__IsInstanceOf_").Append(NodeFactory.NameMangler.GetMangledTypeName((TypeDesc)_target));
                    break;
                case ReadyToRunHelperId.CastClass:
                    sb.Append("__CastClass_").Append(NodeFactory.NameMangler.GetMangledTypeName((TypeDesc)_target));
                    break;
                case ReadyToRunHelperId.GetNonGCStaticBase:
                    sb.Append("__GetNonGCStaticBase_").Append(NodeFactory.NameMangler.GetMangledTypeName((TypeDesc)_target));
                    break;
                case ReadyToRunHelperId.GetGCStaticBase:
                    sb.Append("__GetGCStaticBase_").Append(NodeFactory.NameMangler.GetMangledTypeName((TypeDesc)_target));
                    break;
                case ReadyToRunHelperId.GetThreadStaticBase:
                    sb.Append("__GetThreadStaticBase_").Append(NodeFactory.NameMangler.GetMangledTypeName((TypeDesc)_target));
                    break;
                case ReadyToRunHelperId.DelegateCtor:
                    {
                        var createInfo = (DelegateCreationInfo)_target;
                        sb.Append("__DelegateCtor_");
                        createInfo.Constructor.AppendMangledName(nameMangler, sb);
                        sb.Append("__");
                        createInfo.Target.AppendMangledName(nameMangler, sb);
                        if (createInfo.Thunk != null)
                        {
                            sb.Append("__");
                            createInfo.Thunk.AppendMangledName(nameMangler, sb);
                        }
                    }
                    break;
                case ReadyToRunHelperId.ResolveVirtualFunction:
                    sb.Append("__ResolveVirtualFunction_");
                    sb.Append(NodeFactory.NameMangler.GetMangledMethodName((MethodDesc)_target));
                    break;
                case ReadyToRunHelperId.ResolveGenericVirtualMethod:
                    sb.Append("__ResolveGenericVirtualMethod_");
                    sb.Append(NodeFactory.NameMangler.GetMangledMethodName((MethodDesc)_target));
                    break;
                default:
                    throw new NotImplementedException();
            }
        }

        public override bool IsShareable => true;

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
