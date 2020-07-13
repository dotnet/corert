// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.IO;
using System.Runtime.InteropServices;

using ILCompiler.DependencyAnalysis;
using ILCompiler.DependencyAnalysisFramework;

using Internal.IL;
using Internal.IL.Stubs;
using Internal.TypeSystem;
using Internal.TypeSystem.Ecma;

using Debug = System.Diagnostics.Debug;

namespace ILCompiler
{
    public abstract class Compilation : ICompilation
    {
        protected readonly DependencyAnalyzerBase<NodeFactory> _dependencyGraph;
        protected readonly NodeFactory _nodeFactory;
        protected readonly Logger _logger;
        private readonly DebugInformationProvider _debugInformationProvider;
        private readonly DevirtualizationManager _devirtualizationManager;

        public NameMangler NameMangler => _nodeFactory.NameMangler;
        public NodeFactory NodeFactory => _nodeFactory;
        public CompilerTypeSystemContext TypeSystemContext => NodeFactory.TypeSystemContext;
        public Logger Logger => _logger;
        public PInvokeILProvider PInvokeILProvider { get; }

        private readonly TypeGetTypeMethodThunkCache _typeGetTypeMethodThunks;
        private readonly AssemblyGetExecutingAssemblyMethodThunkCache _assemblyGetExecutingAssemblyMethodThunks;
        private readonly MethodBaseGetCurrentMethodThunkCache _methodBaseGetCurrentMethodThunks;

        protected Compilation(
            DependencyAnalyzerBase<NodeFactory> dependencyGraph,
            NodeFactory nodeFactory,
            IEnumerable<ICompilationRootProvider> compilationRoots,
            ILProvider ilProvider,
            DebugInformationProvider debugInformationProvider,
            DevirtualizationManager devirtualizationManager,
            Logger logger)
        {
            _dependencyGraph = dependencyGraph;
            _nodeFactory = nodeFactory;
            _logger = logger;
            _debugInformationProvider = debugInformationProvider;
            _devirtualizationManager = devirtualizationManager;

            _dependencyGraph.ComputeDependencyRoutine += ComputeDependencyNodeDependencies;
            NodeFactory.AttachToDependencyGraph(_dependencyGraph);

            var rootingService = new RootingServiceProvider(nodeFactory, _dependencyGraph.AddRoot);
            foreach (var rootProvider in compilationRoots)
                rootProvider.AddCompilationRoots(rootingService);

            MetadataType globalModuleGeneratedType = nodeFactory.TypeSystemContext.GeneratedAssembly.GetGlobalModuleType();
            _typeGetTypeMethodThunks = new TypeGetTypeMethodThunkCache(globalModuleGeneratedType);
            _assemblyGetExecutingAssemblyMethodThunks = new AssemblyGetExecutingAssemblyMethodThunkCache(globalModuleGeneratedType);
            _methodBaseGetCurrentMethodThunks = new MethodBaseGetCurrentMethodThunkCache();

            PInvokeILProvider = _nodeFactory.InteropStubManager.CreatePInvokeILProvider();
            if (PInvokeILProvider != null)
            {
                ilProvider = new CombinedILProvider(ilProvider, PInvokeILProvider);
            }

            _methodILCache = new ILCache(ilProvider);
        }

        private ILCache _methodILCache;

        public virtual MethodIL GetMethodIL(MethodDesc method)
        {
            // Flush the cache when it grows too big
            if (_methodILCache.Count > 1000)
                _methodILCache = new ILCache(_methodILCache.ILProvider);

            return _methodILCache.GetOrCreateValue(method).MethodIL;
        }

        protected abstract void ComputeDependencyNodeDependencies(List<DependencyNodeCore<NodeFactory>> obj);

        protected abstract void CompileInternal(string outputFile, ObjectDumper dumper);

        public bool CanInline(MethodDesc caller, MethodDesc callee)
        {
            return NodeFactory.CompilationModuleGroup.CanInline(caller, callee);
        }

        public DelegateCreationInfo GetDelegateCtor(TypeDesc delegateType, MethodDesc target, bool followVirtualDispatch)
        {
            // If we're creating a delegate to a virtual method that cannot be overriden, devirtualize.
            // This is not just an optimization - it's required for correctness in the presence of sealed
            // vtable slots.
            if (followVirtualDispatch && (target.IsFinal || target.OwningType.IsSealed()))
                followVirtualDispatch = false;

            return DelegateCreationInfo.Create(delegateType, target, NodeFactory, followVirtualDispatch);
        }

        /// <summary>
        /// Gets an object representing the static data for RVA mapped fields from the PE image.
        /// </summary>
        public virtual ISymbolNode GetFieldRvaData(FieldDesc field)
        {
            if (field.GetType() == typeof(PInvokeLazyFixupField))
            {
                var pInvokeFixup = (PInvokeLazyFixupField)field;
                PInvokeMetadata metadata = pInvokeFixup.PInvokeMetadata;
                ModuleDesc callingModule = ((MetadataType)pInvokeFixup.TargetMethod.OwningType).Module;
                DllImportSearchPath? dllImportSearchPath = default;
                if (callingModule.Assembly is EcmaAssembly asm)
                {
                    // We look for [assembly:DefaultDllImportSearchPaths(...)]
                    var attrHandle = asm.MetadataReader.GetCustomAttributeHandle(asm.AssemblyDefinition.GetCustomAttributes(),
                        "System.Runtime.InteropServices", "DefaultDllImportSearchPathsAttribute");
                    if (!attrHandle.IsNil)
                    {
                        var attr = asm.MetadataReader.GetCustomAttribute(attrHandle);
                        var decoded = attr.DecodeValue(new CustomAttributeTypeProvider(asm));
                        if (decoded.FixedArguments.Length == 1 &&
                            decoded.FixedArguments[0].Value is int searchPath)
                        {
                            dllImportSearchPath = (DllImportSearchPath)searchPath;
                        }
                    }
                }

                PInvokeModuleData moduleData = new PInvokeModuleData(metadata.Module, dllImportSearchPath, callingModule);
                return NodeFactory.PInvokeMethodFixup(moduleData, metadata.Name, metadata.Flags);
            }
            else if (field is ExternSymbolMappedField externField)
            {
                return NodeFactory.ExternSymbol(externField.SymbolName);
            }
            else
            {
                // Use the typical field definition in case this is an instantiated generic type
                field = field.GetTypicalFieldDefinition();
                return NodeFactory.ReadOnlyDataBlob(NameMangler.GetMangledFieldName(field),
                    ((EcmaField)field).GetFieldRvaData(), NodeFactory.Target.PointerSize);
            }
        }

        public bool HasLazyStaticConstructor(TypeDesc type)
        {
            return NodeFactory.PreinitializationManager.HasLazyStaticConstructor(type);
        }

        public MethodDebugInformation GetDebugInfo(MethodIL methodIL)
        {
            return _debugInformationProvider.GetDebugInfo(methodIL);
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
            else if (intrinsicOwningType.Name == "Assembly" && intrinsicOwningType.Namespace == "System.Reflection")
            {
                if (intrinsicMethod.Signature.IsStatic && intrinsicMethod.Name == "GetExecutingAssembly")
                {
                    ModuleDesc callsiteModule = (callsiteMethod.OwningType as MetadataType)?.Module;
                    if (callsiteModule != null)
                    {
                        Debug.Assert(callsiteModule is IAssemblyDesc, "Multi-module assemblies");
                        return _assemblyGetExecutingAssemblyMethodThunks.GetHelper((IAssemblyDesc)callsiteModule);
                    }
                }
            }
            else if (intrinsicOwningType.Name == "MethodBase" && intrinsicOwningType.Namespace == "System.Reflection")
            {
                if (intrinsicMethod.Signature.IsStatic && intrinsicMethod.Name == "GetCurrentMethod")
                {
                    return _methodBaseGetCurrentMethodThunks.GetHelper(callsiteMethod).InstantiateAsOpen();
                }
            }

            return intrinsicMethod;
        }

        public bool HasFixedSlotVTable(TypeDesc type)
        {
            return NodeFactory.VTable(type).HasFixedSlots;
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
            switch (lookupKind)
            {
                case ReadyToRunHelperId.TypeHandle:
                case ReadyToRunHelperId.NecessaryTypeHandle:
                case ReadyToRunHelperId.DefaultConstructor:
                case ReadyToRunHelperId.TypeHandleForCasting:
                case ReadyToRunHelperId.ObjectAllocator:
                    return ((TypeDesc)targetOfLookup).IsRuntimeDeterminedSubtype;

                case ReadyToRunHelperId.MethodDictionary:
                case ReadyToRunHelperId.MethodEntry:
                case ReadyToRunHelperId.VirtualDispatchCell:
                case ReadyToRunHelperId.MethodHandle:
                    return ((MethodDesc)targetOfLookup).IsRuntimeDeterminedExactMethod;

                case ReadyToRunHelperId.FieldHandle:
                    return ((FieldDesc)targetOfLookup).OwningType.IsRuntimeDeterminedSubtype;

                default:
                    throw new NotImplementedException();
            }
        }

        public ReadyToRunHelperId GetLdTokenHelperForType(TypeDesc type)
        {
            return _nodeFactory.MetadataManager.ShouldConsiderLdTokenReferenceAConstruction(type)
                ? ReadyToRunHelperId.TypeHandle
                : ReadyToRunHelperId.NecessaryTypeHandle;
        }

        public ISymbolNode ComputeConstantLookup(ReadyToRunHelperId lookupKind, object targetOfLookup)
        {
            switch (lookupKind)
            {
                case ReadyToRunHelperId.TypeHandle:
                    return NodeFactory.ConstructedTypeSymbol((TypeDesc)targetOfLookup);
                case ReadyToRunHelperId.NecessaryTypeHandle:
                    return NodeFactory.NecessaryTypeSymbol((TypeDesc)targetOfLookup);
                case ReadyToRunHelperId.TypeHandleForCasting:
                    {
                        var type = (TypeDesc)targetOfLookup;
                        if (type.IsNullable)
                            targetOfLookup = type.Instantiation[0];
                        return NodeFactory.NecessaryTypeSymbol((TypeDesc)targetOfLookup);
                    }
                case ReadyToRunHelperId.MethodDictionary:
                    return NodeFactory.MethodGenericDictionary((MethodDesc)targetOfLookup);
                case ReadyToRunHelperId.MethodEntry:
                    return NodeFactory.FatFunctionPointer((MethodDesc)targetOfLookup);
                case ReadyToRunHelperId.MethodHandle:
                    return NodeFactory.RuntimeMethodHandle((MethodDesc)targetOfLookup);
                case ReadyToRunHelperId.FieldHandle:
                    return NodeFactory.RuntimeFieldHandle((FieldDesc)targetOfLookup);
                case ReadyToRunHelperId.DefaultConstructor:
                    {
                        var type = (TypeDesc)targetOfLookup;   
                        MethodDesc ctor = type.GetDefaultConstructor();
                        if (ctor == null)
                        {
                            MetadataType activatorType = TypeSystemContext.SystemModule.GetKnownType("System", "Activator");
                            MetadataType classWithMissingCtor = activatorType.GetKnownNestedType("ClassWithMissingConstructor");
                            ctor = classWithMissingCtor.GetParameterlessConstructor();
                        }
                        return NodeFactory.CanonicalEntrypoint(ctor);
                    }
                case ReadyToRunHelperId.ObjectAllocator:
                    {
                        var type = (TypeDesc)targetOfLookup;
                        return NodeFactory.ExternSymbol(JitHelper.GetNewObjectHelperForType(type));
                    }

                default:
                    throw new NotImplementedException();
            }
        }

        public GenericDictionaryLookup ComputeGenericLookup(MethodDesc contextMethod, ReadyToRunHelperId lookupKind, object targetOfLookup)
        {
            GenericContextSource contextSource;

            if (contextMethod.RequiresInstMethodDescArg())
            {
                contextSource = GenericContextSource.MethodParameter;
            }
            else if (contextMethod.RequiresInstMethodTableArg())
            {
                contextSource = GenericContextSource.TypeParameter;
            }
            else
            {
                Debug.Assert(contextMethod.AcquiresInstMethodTableFromThis());
                contextSource = GenericContextSource.ThisObject;
            }

            //
            // Some helpers represent logical concepts that might not be something that can be looked up in a dictionary
            //

            // Downgrade type handle for casting to a normal type handle if possible
            if (lookupKind == ReadyToRunHelperId.TypeHandleForCasting)
            {
                var type = (TypeDesc)targetOfLookup;
                if (!type.IsRuntimeDeterminedType ||
                    (!((RuntimeDeterminedType)type).CanonicalType.IsCanonicalDefinitionType(CanonicalFormKind.Universal) &&
                    !((RuntimeDeterminedType)type).CanonicalType.IsNullable))
                {
                    if (type.IsNullable)
                    {
                        targetOfLookup = type.Instantiation[0];
                    }
                    lookupKind = ReadyToRunHelperId.NecessaryTypeHandle;
                }
            }

            // We don't have separate entries for necessary type handles to avoid possible duplication
            if (lookupKind == ReadyToRunHelperId.NecessaryTypeHandle)
            {
                lookupKind = ReadyToRunHelperId.TypeHandle;
            }

            // Can we do a fixed lookup? Start by checking if we can get to the dictionary.
            // Context source having a vtable with fixed slots is a prerequisite.
            if (contextSource == GenericContextSource.MethodParameter
                || HasFixedSlotVTable(contextMethod.OwningType))
            {
                DictionaryLayoutNode dictionaryLayout;
                if (contextSource == GenericContextSource.MethodParameter)
                    dictionaryLayout = _nodeFactory.GenericDictionaryLayout(contextMethod);
                else
                    dictionaryLayout = _nodeFactory.GenericDictionaryLayout(contextMethod.OwningType);

                // If the dictionary layout has fixed slots, we can compute the lookup now. Otherwise defer to helper.
                if (dictionaryLayout.HasFixedSlots)
                {
                    int pointerSize = _nodeFactory.Target.PointerSize;

                    GenericLookupResult lookup = ReadyToRunGenericHelperNode.GetLookupSignature(_nodeFactory, lookupKind, targetOfLookup);
                    int dictionarySlot = dictionaryLayout.GetSlotForFixedEntry(lookup);
                    if (dictionarySlot != -1)
                    {
                        int dictionaryOffset = dictionarySlot * pointerSize;

                        bool indirectLastOffset = lookup.LookupResultReferenceType(_nodeFactory) == GenericLookupResultReferenceType.Indirect;

                        if (contextSource == GenericContextSource.MethodParameter)
                        {
                            return GenericDictionaryLookup.CreateFixedLookup(contextSource, dictionaryOffset, indirectLastOffset: indirectLastOffset);
                        }
                        else
                        {
                            int vtableSlot = VirtualMethodSlotHelper.GetGenericDictionarySlot(_nodeFactory, contextMethod.OwningType);
                            int vtableOffset = EETypeNode.GetVTableOffset(pointerSize) + vtableSlot * pointerSize;
                            return GenericDictionaryLookup.CreateFixedLookup(contextSource, vtableOffset, dictionaryOffset, indirectLastOffset: indirectLastOffset);
                        }
                    }
                }
            }

            // Fixed lookup not possible - use helper.
            return GenericDictionaryLookup.CreateHelperLookup(contextSource, lookupKind, targetOfLookup);
        }

        /// <summary>
        /// Gets the type of System.Type descendant that implements runtime types.
        /// </summary>
        public virtual TypeDesc GetTypeOfRuntimeType()
        {
            ModuleDesc reflectionCoreModule = TypeSystemContext.GetModuleForSimpleName("System.Private.Reflection.Core", false);
            if (reflectionCoreModule != null)
            {
                return reflectionCoreModule.GetKnownType("System.Reflection.Runtime.TypeInfos", "RuntimeTypeInfo");
            }

            return null;
        }

        public bool IsFatPointerCandidate(MethodDesc containingMethod, MethodSignature signature)
        {
            // Unmanaged calls are never fat pointers
            if ((signature.Flags & MethodSignatureFlags.UnmanagedCallingConventionMask) != 0)
                return false;

            // Everything else except RawCalliHelpers could be a fat pointer
            var owningType = containingMethod.OwningType as MetadataType;
            return owningType?.Name != "RawCalliHelper";
        }

        CompilationResults ICompilation.Compile(string outputFile, ObjectDumper dumper)
        {
            if (dumper != null)
            {
                dumper.Begin();
            }

            CompileInternal(outputFile, dumper);

            if (dumper != null)
            {
                dumper.End();
            }

            return new CompilationResults(_dependencyGraph, _nodeFactory);
        }

        private sealed class ILCache : LockFreeReaderHashtable<MethodDesc, ILCache.MethodILData>
        {
            public ILProvider ILProvider { get; }

            public ILCache(ILProvider provider)
            {
                ILProvider = provider;
            }

            protected override int GetKeyHashCode(MethodDesc key)
            {
                return key.GetHashCode();
            }
            protected override int GetValueHashCode(MethodILData value)
            {
                return value.Method.GetHashCode();
            }
            protected override bool CompareKeyToValue(MethodDesc key, MethodILData value)
            {
                return Object.ReferenceEquals(key, value.Method);
            }
            protected override bool CompareValueToValue(MethodILData value1, MethodILData value2)
            {
                return Object.ReferenceEquals(value1.Method, value2.Method);
            }
            protected override MethodILData CreateValueFromKey(MethodDesc key)
            {
                return new MethodILData() { Method = key, MethodIL = ILProvider.GetMethodIL(key) };
            }

            internal class MethodILData
            {
                public MethodDesc Method;
                public MethodIL MethodIL;
            }
        }

        private sealed class CombinedILProvider : ILProvider
        {
            private readonly ILProvider _primaryILProvider;
            private readonly PInvokeILProvider _pinvokeProvider;

            public CombinedILProvider(ILProvider primaryILProvider, PInvokeILProvider pinvokeILProvider)
            {
                _primaryILProvider = primaryILProvider;
                _pinvokeProvider = pinvokeILProvider;
            }

            public override MethodIL GetMethodIL(MethodDesc method)
            {
                MethodIL result = _primaryILProvider.GetMethodIL(method);
                if (result == null && method.IsPInvoke)
                    result = _pinvokeProvider.GetMethodIL(method);

                return result;
            }
        }
    }

    // Interface under which Compilation is exposed externally.
    public interface ICompilation
    {
        CompilationResults Compile(string outputFileName, ObjectDumper dumper);
    }

    public class CompilationResults
    {
        private readonly DependencyAnalyzerBase<NodeFactory> _graph;
        protected readonly NodeFactory _factory;

        protected ImmutableArray<DependencyNodeCore<NodeFactory>> MarkedNodes
        {
            get
            {
                return _graph.MarkedNodeList;
            }
        }

        internal CompilationResults(DependencyAnalyzerBase<NodeFactory> graph, NodeFactory factory)
        {
            _graph = graph;
            _factory = factory;
        }

        public void WriteDependencyLog(string fileName)
        {
            using (FileStream dgmlOutput = new FileStream(fileName, FileMode.Create))
            {
                DgmlWriter.WriteDependencyGraphToStream(dgmlOutput, _graph, _factory);
                dgmlOutput.Flush();
            }
        }

        public IEnumerable<MethodDesc> CompiledMethodBodies
        {
            get
            {
                foreach (var node in MarkedNodes)
                {
                    if (node is IMethodBodyNode)
                        yield return ((IMethodBodyNode)node).Method;
                }
            }
        }

        public IEnumerable<TypeDesc> ConstructedEETypes
        {
            get
            {
                foreach (var node in MarkedNodes)
                {
                    if (node is ConstructedEETypeNode || node is CanonicalEETypeNode)
                    {
                        yield return ((IEETypeNode)node).Type;
                    }
                }
            }
        }
    }
}
