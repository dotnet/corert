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

        // The following helpers are used for generic lookups only
        TypeHandle,
        MethodHandle,
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
                case ReadyToRunHelperId.VirtualCall:
                    {
                        // Make sure we aren't trying to callvirt Object.Finalize
                        MethodDesc method = (MethodDesc)target;
                        if (method.IsFinalizer)
                            throw new TypeSystemException.InvalidProgramException(ExceptionStringID.InvalidProgramCallVirtFinalize, method);
                    }
                    break;
            }
        }

        protected override string GetName(NodeFactory factory) => this.GetMangledName(factory.NameMangler);

        public ReadyToRunHelperId Id => _id;
        public Object Target =>  _target;

        public override void AppendMangledName(NameMangler nameMangler, Utf8StringBuilder sb)
        {
            switch (_id)
            {
                case ReadyToRunHelperId.NewHelper:
                    sb.Append("__NewHelper_").Append(nameMangler.GetMangledTypeName((TypeDesc)_target));
                    break;
                case ReadyToRunHelperId.NewArr1:
                    sb.Append("__NewArr1_").Append(nameMangler.GetMangledTypeName((TypeDesc)_target));
                    break;
                case ReadyToRunHelperId.VirtualCall:
                    sb.Append("__VirtualCall_").Append(nameMangler.GetMangledMethodName((MethodDesc)_target));
                    break;
                case ReadyToRunHelperId.IsInstanceOf:
                    sb.Append("__IsInstanceOf_").Append(nameMangler.GetMangledTypeName((TypeDesc)_target));
                    break;
                case ReadyToRunHelperId.CastClass:
                    sb.Append("__CastClass_").Append(nameMangler.GetMangledTypeName((TypeDesc)_target));
                    break;
                case ReadyToRunHelperId.GetNonGCStaticBase:
                    sb.Append("__GetNonGCStaticBase_").Append(nameMangler.GetMangledTypeName((TypeDesc)_target));
                    break;
                case ReadyToRunHelperId.GetGCStaticBase:
                    sb.Append("__GetGCStaticBase_").Append(nameMangler.GetMangledTypeName((TypeDesc)_target));
                    break;
                case ReadyToRunHelperId.GetThreadStaticBase:
                    sb.Append("__GetThreadStaticBase_").Append(nameMangler.GetMangledTypeName((TypeDesc)_target));
                    break;
                case ReadyToRunHelperId.DelegateCtor:
                    {
                        var createInfo = (DelegateCreationInfo)_target;
                        sb.Append("__DelegateCtor_");
                        if (createInfo.TargetNeedsVTableLookup)
                            sb.Append("FromVtbl_");
                        createInfo.Constructor.AppendMangledName(nameMangler, sb);
                        sb.Append("__");
                        sb.Append(nameMangler.GetMangledMethodName(createInfo.TargetMethod));
                        if (createInfo.Thunk != null)
                        {
                            sb.Append("__");
                            createInfo.Thunk.AppendMangledName(nameMangler, sb);
                        }
                    }
                    break;
                case ReadyToRunHelperId.ResolveVirtualFunction:
                    sb.Append("__ResolveVirtualFunction_");
                    sb.Append(nameMangler.GetMangledMethodName((MethodDesc)_target));
                    break;
                default:
                    throw new NotImplementedException();
            }
        }

        public override bool IsShareable => true;

        protected override DependencyList ComputeNonRelocationBasedDependencies(NodeFactory factory)
        {
            if (_id == ReadyToRunHelperId.VirtualCall || _id == ReadyToRunHelperId.ResolveVirtualFunction)
            {
                var targetMethod = (MethodDesc)_target;
#if !SUPPORT_JIT
                if (!factory.CompilationModuleGroup.ShouldProduceFullVTable(targetMethod.OwningType))
#endif
                {
                    DependencyList dependencyList = new DependencyList();
                    dependencyList.Add(factory.VirtualMethodUse((MethodDesc)_target), "ReadyToRun Virtual Method Call");
                    return dependencyList;
                }
            }
            else if (_id == ReadyToRunHelperId.DelegateCtor)
            {
                var info = (DelegateCreationInfo)_target;
                if (info.PerformsVirtualDispatch)
                {
#if !SUPPORT_JIT
                    if (!factory.CompilationModuleGroup.ShouldProduceFullVTable(info.TargetMethod.OwningType))
#endif
                    {
                        DependencyList dependencyList = new DependencyList();
                        dependencyList.Add(factory.VirtualMethodUse(info.TargetMethod), "ReadyToRun Delegate to virtual method");
                        return dependencyList;
                    }
                }
            }

            return null;
        }
    }
}
