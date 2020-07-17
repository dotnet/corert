// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using System.Diagnostics;

using Internal.TypeSystem;
using ILCompiler.DependencyAnalysis;
using Internal.IL;
using Internal.IL.Stubs;

namespace ILCompiler
{
    /// <summary>
    /// Version of Compilation class used for JIT compilation. Should probably be merged with the Compilation class used in AOT compilation
    /// </summary>
    public class Compilation
    {
        public Compilation(TypeSystemContext context)
        {
            _typeSystemContext = context;
            _typeGetTypeMethodThunks = new TypeGetTypeMethodThunkCache(context.GetWellKnownType(WellKnownType.Object));
            _pInvokeILProvider = new PInvokeILProvider(new LazyPInvokePolicy(), null);
            _ilProvider = new CoreRTILProvider();
            _nodeFactory = new NodeFactory(context);
            _devirtualizationManager = new DevirtualizationManager();
        }

        private readonly NodeFactory _nodeFactory;
        private readonly TypeSystemContext _typeSystemContext;
        protected readonly Logger _logger = Logger.Null;
        private readonly TypeGetTypeMethodThunkCache _typeGetTypeMethodThunks;
        private ILProvider _ilProvider;
        private PInvokeILProvider _pInvokeILProvider;
        private readonly DevirtualizationManager _devirtualizationManager;

        internal Logger Logger => _logger;
        internal PInvokeILProvider PInvokeILProvider => _pInvokeILProvider;

        public TypeSystemContext TypeSystemContext { get { return _typeSystemContext; } }
        public NodeFactory NodeFactory { get { return _nodeFactory; } }

        public NameMangler NameMangler { get { return null; } }

        public virtual bool CanInline(MethodDesc caller, MethodDesc callee)
        {
            // No inlining limits by default
            return true;
        }

        public ISymbolNode GetFieldRvaData(FieldDesc field)
        {
            // Use the typical field definition in case this is an instantiated generic type
            field = field.GetTypicalFieldDefinition();
            throw new NotImplementedException();
        }

        internal MethodIL GetMethodIL(MethodDesc method)
        {
            return _ilProvider.GetMethodIL(method);
        }

        public bool HasLazyStaticConstructor(TypeDesc type) { return type.HasStaticConstructor; }

        public MethodDebugInformation GetDebugInfo(MethodIL methodIL)
        {
            // This method looks odd right now, but it's an extensibility point that lets us generate
            // fake debugging information for things that don't have physical symbols.
            return methodIL.GetDebugInfo();
        }

        /// <summary>
        /// Resolves a reference to an intrinsic method to a new method that takes it's place in the compilation.
        /// This is used for intrinsics where the intrinsic expansion depends on the callsite.
        /// </summary>
        /// <param name="intrinsicMethod">The intrinsic method called.</param>
        /// <param name="callsiteMethod">The callsite that calls the intrinsic.</param>
        /// <returns>The intrinsic implementation to be called for this specific callsite.</returns>
        public MethodDesc ExpandIntrinsicForCallsite(MethodDesc intrinsicMethod, MethodDesc callsiteMethod)
        {
            Debug.Assert(intrinsicMethod.IsIntrinsic);

            var intrinsicOwningType = intrinsicMethod.OwningType as MetadataType;
            if (intrinsicOwningType == null)
                return intrinsicMethod;

            if (intrinsicOwningType.Module != TypeSystemContext.SystemModule)
                return intrinsicMethod;

            if (intrinsicOwningType.Name == "Type" && intrinsicOwningType.Namespace == "System")
            {
                if (intrinsicMethod.Signature.IsStatic && intrinsicMethod.Name == "GetType")
                {
                    ModuleDesc callsiteModule = (callsiteMethod.OwningType as MetadataType)?.Module;
                    if (callsiteModule != null)
                    {
                        Debug.Assert(callsiteModule is IAssemblyDesc, "Multi-module assemblies");
                        return _typeGetTypeMethodThunks.GetHelper(intrinsicMethod, ((IAssemblyDesc)callsiteModule).GetName().FullName);
                    }
                }
            }

            return intrinsicMethod;
        }

        public bool HasFixedSlotVTable(TypeDesc type)
        {
            return true;
        }

        public bool IsEffectivelySealed(TypeDesc type)
        {
            return _devirtualizationManager.IsEffectivelySealed(type);
        }

        public bool IsEffectivelySealed(MethodDesc method)
        {
            return _devirtualizationManager.IsEffectivelySealed(method);
        }

        public MethodDesc ResolveVirtualMethod(MethodDesc declMethod, TypeDesc implType)
        {
            return _devirtualizationManager.ResolveVirtualMethod(declMethod, implType);
        }

        public bool NeedsRuntimeLookup(ReadyToRunHelperId lookupKind, object targetOfLookup)
        {
            // The current plan seem to be to copy paste from ILCompiler.Compilation, but that's not a sustainable plan
            throw new NotImplementedException();
        }

        public ReadyToRunHelperId GetLdTokenHelperForType(TypeDesc type)
        {
            return ReadyToRunHelperId.TypeHandle;
        }

        public ISymbolNode ComputeConstantLookup(ReadyToRunHelperId lookupKind, object targetOfLookup)
        {
            // The current plan seem to be to copy paste from ILCompiler.Compilation, but that's not a sustainable plan
            throw new NotImplementedException();
        }

        public GenericDictionaryLookup ComputeGenericLookup(MethodDesc contextMethod, ReadyToRunHelperId lookupKind, object targetOfLookup)
        {
            // The current plan seem to be to copy paste from ILCompiler.Compilation, but that's not a sustainable plan
            throw new NotImplementedException();
        }

        public DelegateCreationInfo GetDelegateCtor(TypeDesc delegateType, MethodDesc target, bool followVirtualDispatch)
        {
            return DelegateCreationInfo.Create(delegateType, target, NodeFactory, followVirtualDispatch);
        }

        public TypeDesc GetTypeOfRuntimeType()
        {
            // The current plan seem to be to copy paste from ILCompiler.Compilation, but that's not a sustainable plan
            throw new NotImplementedException();

        }

        public bool IsFatPointerCandidate(MethodDesc containingMethod, MethodSignature signature)
        {
            // The current plan seem to be to copy paste from ILCompiler.Compilation, but that's not a sustainable plan
            throw new NotImplementedException();
        }

        public sealed class LazyPInvokePolicy : PInvokeILEmitterConfiguration
        {
            public override bool GenerateDirectCall(string libraryName, string methodName)
            {
                return false;
            }
        }
    }
}
