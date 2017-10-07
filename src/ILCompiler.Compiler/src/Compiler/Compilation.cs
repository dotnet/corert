// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.IO;

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

        public NameMangler NameMangler => _nodeFactory.NameMangler;
        public NodeFactory NodeFactory => _nodeFactory;
        public CompilerTypeSystemContext TypeSystemContext => NodeFactory.TypeSystemContext;
        public Logger Logger => _logger;
        internal PInvokeILProvider PInvokeILProvider { get; }

        private readonly TypeGetTypeMethodThunkCache _typeGetTypeMethodThunks;
        private readonly AssemblyGetExecutingAssemblyMethodThunkCache _assemblyGetExecutingAssemblyMethodThunks;
        private readonly MethodBaseGetCurrentMethodThunkCache _methodBaseGetCurrentMethodThunks;

        protected Compilation(
            DependencyAnalyzerBase<NodeFactory> dependencyGraph,
            NodeFactory nodeFactory,
            IEnumerable<ICompilationRootProvider> compilationRoots,
            DebugInformationProvider debugInformationProvider,
            Logger logger)
        {
            _dependencyGraph = dependencyGraph;
            _nodeFactory = nodeFactory;
            _logger = logger;
            _debugInformationProvider = debugInformationProvider;

            _dependencyGraph.ComputeDependencyRoutine += ComputeDependencyNodeDependencies;
            NodeFactory.AttachToDependencyGraph(_dependencyGraph);

            var rootingService = new RootingServiceProvider(dependencyGraph, nodeFactory);
            foreach (var rootProvider in compilationRoots)
                rootProvider.AddCompilationRoots(rootingService);

            MetadataType globalModuleGeneratedType = nodeFactory.CompilationModuleGroup.GeneratedAssembly.GetGlobalModuleType();
            _typeGetTypeMethodThunks = new TypeGetTypeMethodThunkCache(globalModuleGeneratedType);
            _assemblyGetExecutingAssemblyMethodThunks = new AssemblyGetExecutingAssemblyMethodThunkCache(globalModuleGeneratedType);
            _methodBaseGetCurrentMethodThunks = new MethodBaseGetCurrentMethodThunkCache();

            bool? forceLazyPInvokeResolution = null;
            // TODO: Workaround lazy PInvoke resolution not working with CppCodeGen yet
            // https://github.com/dotnet/corert/issues/2454
            // https://github.com/dotnet/corert/issues/2149
            if (nodeFactory.IsCppCodegenTemporaryWorkaround) forceLazyPInvokeResolution = false;
            PInvokeILProvider = new PInvokeILProvider(new PInvokeILEmitterConfiguration(forceLazyPInvokeResolution), nodeFactory.InteropStubManager.InteropStateManager);

            _methodILCache = new ILProvider(PInvokeILProvider);
        }

        private ILProvider _methodILCache;
        
        public MethodIL GetMethodIL(MethodDesc method)
        {
            // Flush the cache when it grows too big
            if (_methodILCache.Count > 1000)
                _methodILCache = new ILProvider(PInvokeILProvider);

            return _methodILCache.GetMethodIL(method);
        }

        protected abstract void ComputeDependencyNodeDependencies(List<DependencyNodeCore<NodeFactory>> obj);

        protected abstract void CompileInternal(string outputFile, ObjectDumper dumper);

        public DelegateCreationInfo GetDelegateCtor(TypeDesc delegateType, MethodDesc target, bool followVirtualDispatch)
        {
            return DelegateCreationInfo.Create(delegateType, target, NodeFactory, followVirtualDispatch);
        }

        /// <summary>
        /// Gets an object representing the static data for RVA mapped fields from the PE image.
        /// </summary>
        public ObjectNode GetFieldRvaData(FieldDesc field)
        {
            if (field.GetType() == typeof(PInvokeLazyFixupField))
            {
                var pInvokeFixup = (PInvokeLazyFixupField)field;
                PInvokeMetadata metadata = pInvokeFixup.PInvokeMetadata;
                return NodeFactory.PInvokeMethodFixup(metadata.Module, metadata.Name);
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
            return TypeSystemContext.HasLazyStaticConstructor(type);
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

        public bool NeedsRuntimeLookup(ReadyToRunHelperId lookupKind, object targetOfLookup)
        {
            switch (lookupKind)
            {
                case ReadyToRunHelperId.TypeHandle:
                case ReadyToRunHelperId.NecessaryTypeHandle:
                case ReadyToRunHelperId.DefaultConstructor:
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

        public ISymbolNode ComputeConstantLookup(ReadyToRunHelperId lookupKind, object targetOfLookup)
        {
            switch (lookupKind)
            {
                case ReadyToRunHelperId.TypeHandle:
                    return NodeFactory.ConstructedTypeSymbol((TypeDesc)targetOfLookup);
                case ReadyToRunHelperId.NecessaryTypeHandle:
                    return NodeFactory.NecessaryTypeSymbol((TypeDesc)targetOfLookup);
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

                        if (contextSource == GenericContextSource.MethodParameter)
                        {
                            return GenericDictionaryLookup.CreateFixedLookup(contextSource, dictionaryOffset);
                        }
                        else
                        {
                            int vtableSlot = VirtualMethodSlotHelper.GetGenericDictionarySlot(_nodeFactory, contextMethod.OwningType);
                            int vtableOffset = EETypeNode.GetVTableOffset(pointerSize) + vtableSlot * pointerSize;
                            return GenericDictionaryLookup.CreateFixedLookup(contextSource, vtableOffset, dictionaryOffset);
                        }
                    }
                }
            }

            // Fixed lookup not possible - use helper.
            return GenericDictionaryLookup.CreateHelperLookup(contextSource);
        }

        CompilationResults ICompilation.Compile(string outputFile, ObjectDumper dumper)
        {
            if (dumper != null)
            {
                dumper.Begin();
            }

            // In multi-module builds, set the compilation unit prefix to prevent ambiguous symbols in linked object files
            NameMangler.CompilationUnitPrefix = _nodeFactory.CompilationModuleGroup.IsSingleFileCompilation ? "" : Path.GetFileNameWithoutExtension(outputFile);
            CompileInternal(outputFile, dumper);

            if (dumper != null)
            {
                dumper.End();
            }

            return new CompilationResults(_dependencyGraph, _nodeFactory);
        }

        private class RootingServiceProvider : IRootingServiceProvider
        {
            private DependencyAnalyzerBase<NodeFactory> _graph;
            private NodeFactory _factory;

            public RootingServiceProvider(DependencyAnalyzerBase<NodeFactory> graph, NodeFactory factory)
            {
                _graph = graph;
                _factory = factory;
            }

            public void AddCompilationRoot(MethodDesc method, string reason, string exportName = null)
            {
                IMethodNode methodEntryPoint = _factory.CanonicalEntrypoint(method);
                _graph.AddRoot(methodEntryPoint, reason);

                if (exportName != null)
                    _factory.NodeAliases.Add(methodEntryPoint, exportName);
            }

            public void AddCompilationRoot(TypeDesc type, string reason)
            {
                if (!ConstructedEETypeNode.CreationAllowed(type))
                {
                    _graph.AddRoot(_factory.NecessaryTypeSymbol(type), reason);
                }
                else
                {
                    _graph.AddRoot(_factory.ConstructedTypeSymbol(type), reason);
                }
            }

            public void RootThreadStaticBaseForType(TypeDesc type, string reason)
            {
                Debug.Assert(!type.IsGenericDefinition);

                MetadataType metadataType = type as MetadataType;
                if (metadataType != null && metadataType.ThreadStaticFieldSize.AsInt > 0)
                {
                    _graph.AddRoot(_factory.TypeThreadStaticIndex(metadataType), reason);

                    // Also explicitly root the non-gc base if we have a lazy cctor
                    if(_factory.TypeSystemContext.HasLazyStaticConstructor(type))
                        _graph.AddRoot(_factory.TypeNonGCStaticsSymbol(metadataType), reason);
                }
            }

            public void RootGCStaticBaseForType(TypeDesc type, string reason)
            {
                Debug.Assert(!type.IsGenericDefinition);

                MetadataType metadataType = type as MetadataType;
                if (metadataType != null && metadataType.GCStaticFieldSize.AsInt > 0)
                {
                    _graph.AddRoot(_factory.TypeGCStaticsSymbol(metadataType), reason);

                    // Also explicitly root the non-gc base if we have a lazy cctor
                    if (_factory.TypeSystemContext.HasLazyStaticConstructor(type))
                        _graph.AddRoot(_factory.TypeNonGCStaticsSymbol(metadataType), reason);
                }
            }

            public void RootNonGCStaticBaseForType(TypeDesc type, string reason)
            {
                Debug.Assert(!type.IsGenericDefinition);

                MetadataType metadataType = type as MetadataType;
                if (metadataType != null && (metadataType.NonGCStaticFieldSize.AsInt > 0 || _factory.TypeSystemContext.HasLazyStaticConstructor(type)))
                {
                    _graph.AddRoot(_factory.TypeNonGCStaticsSymbol(metadataType), reason);
                }
            }

            public void RootVirtualMethodForReflection(MethodDesc method, string reason)
            {
                Debug.Assert(method.IsVirtual);

                if (!_factory.VTable(method.OwningType).HasFixedSlots)
                    _graph.AddRoot(_factory.VirtualMethodUse(method), reason);

                if (method.IsAbstract)
                {
                    _graph.AddRoot(_factory.ReflectableMethod(method), reason);
                }
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
        private readonly NodeFactory _factory;

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
