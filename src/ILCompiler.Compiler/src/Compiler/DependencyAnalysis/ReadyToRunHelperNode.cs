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
        NecessaryTypeHandle,
        MethodHandle,
        FieldHandle,
        MethodDictionary,
        MethodEntry,
        VirtualDispatchCell,
        DefaultConstructor,
    }

    public partial class ReadyToRunHelperNode : AssemblyStubNode, INodeWithDebugInfo
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
                            ThrowHelper.ThrowInvalidProgramException(ExceptionStringID.InvalidProgramCallVirtFinalize, method);
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
                    ((DelegateCreationInfo)_target).AppendMangledName(nameMangler, sb);
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

                DependencyList dependencyList = new DependencyList();

#if !SUPPORT_JIT
                factory.MetadataManager.GetDependenciesDueToVirtualMethodReflectability(ref dependencyList, factory, targetMethod);

                if (!factory.VTable(targetMethod.OwningType).HasFixedSlots)

                {
                    dependencyList.Add(factory.VirtualMethodUse((MethodDesc)_target), "ReadyToRun Virtual Method Call");
                }
#endif

                return dependencyList;
            }
            else if (_id == ReadyToRunHelperId.DelegateCtor)
            {
                var info = (DelegateCreationInfo)_target;
                if (info.NeedsVirtualMethodUseTracking)
                {
                    MethodDesc targetMethod = info.TargetMethod;

                    DependencyList dependencyList = new DependencyList();
#if !SUPPORT_JIT
                    factory.MetadataManager.GetDependenciesDueToVirtualMethodReflectability(ref dependencyList, factory, targetMethod);

                    if (!factory.VTable(info.TargetMethod.OwningType).HasFixedSlots)
                    {
                        dependencyList.Add(factory.VirtualMethodUse(info.TargetMethod), "ReadyToRun Delegate to virtual method");
                    }
#endif

                    return dependencyList;
                }
            }

            return null;
        }

        DebugLocInfo[] INodeWithDebugInfo.DebugLocInfos
        {
            get
            {
                if (_id == ReadyToRunHelperId.VirtualCall)
                {
                    // Generate debug information that lets debuggers step into the virtual calls.
                    // We generate a step into sequence point at the point where the helper jumps to
                    // the target of the virtual call.
                    TargetDetails target = ((MethodDesc)_target).Context.Target;
                    int debuggerStepInOffset = -1;
                    switch (target.Architecture)
                    {
                        case TargetArchitecture.X64:
                            debuggerStepInOffset = 3;
                            break;
                    }
                    if (debuggerStepInOffset != -1)
                    {
                        return new DebugLocInfo[]
                        {
                            new DebugLocInfo(0, String.Empty, WellKnownLineNumber.DebuggerStepThrough),
                            new DebugLocInfo(debuggerStepInOffset, String.Empty, WellKnownLineNumber.DebuggerStepIn)
                        };
                    }
                }

                return Array.Empty<DebugLocInfo>();
            }
        }

        DebugVarInfo[] INodeWithDebugInfo.DebugVarInfos
        {
            get
            {
                return Array.Empty<DebugVarInfo>();
            }
        }

#if !SUPPORT_JIT
        protected internal override int ClassCode => -911637948;

        protected internal override int CompareToImpl(SortableDependencyNode other, CompilerComparer comparer)
        {
            var compare = _id.CompareTo(((ReadyToRunHelperNode)other)._id);
            if (compare != 0)
                return compare;

            switch (_id)
            {
                case ReadyToRunHelperId.NewHelper:
                case ReadyToRunHelperId.NewArr1:
                case ReadyToRunHelperId.IsInstanceOf:
                case ReadyToRunHelperId.CastClass:
                case ReadyToRunHelperId.GetNonGCStaticBase:
                case ReadyToRunHelperId.GetGCStaticBase:
                case ReadyToRunHelperId.GetThreadStaticBase:
                    return comparer.Compare((TypeDesc)_target, (TypeDesc)((ReadyToRunHelperNode)other)._target);
                case ReadyToRunHelperId.VirtualCall:
                case ReadyToRunHelperId.ResolveVirtualFunction:
                    return comparer.Compare((MethodDesc)_target, (MethodDesc)((ReadyToRunHelperNode)other)._target);
                case ReadyToRunHelperId.DelegateCtor:
                    return ((DelegateCreationInfo)_target).CompareTo((DelegateCreationInfo)((ReadyToRunHelperNode)other)._target, comparer);
                default:
                    throw new NotImplementedException();
            }
            
        }
#endif
    }
}
