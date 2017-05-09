// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Collections.Generic;
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

        public NameMangler NameMangler => _nodeFactory.NameMangler;
        public NodeFactory NodeFactory => _nodeFactory;
        public CompilerTypeSystemContext TypeSystemContext => NodeFactory.TypeSystemContext;
        public Logger Logger => _logger;
        internal PInvokeILProvider PInvokeILProvider { get; }

        protected abstract bool GenerateDebugInfo { get; }

        private readonly TypeGetTypeMethodThunkCache _typeGetTypeMethodThunks;
        private readonly AssemblyGetExecutingAssemblyMethodThunkCache _assemblyGetExecutingAssemblyMethodThunks;
        private readonly MethodBaseGetCurrentMethodThunkCache _methodBaseGetCurrentMethodThunks;

        protected Compilation(
            DependencyAnalyzerBase<NodeFactory> dependencyGraph,
            NodeFactory nodeFactory,
            IEnumerable<ICompilationRootProvider> compilationRoots,
            Logger logger)
        {
            _dependencyGraph = dependencyGraph;
            _nodeFactory = nodeFactory;
            _logger = logger;

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
                return NodeFactory.PInvokeMethodFixup(metadata.Module, metadata.Name, metadata.DllImportSearchPath);
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
            if (!GenerateDebugInfo)
                return MethodDebugInformation.None;

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

        void ICompilation.Compile(string outputFile, ObjectDumper dumper)
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
        }

        void ICompilation.WriteDependencyLog(string fileName)
        {
            using (FileStream dgmlOutput = new FileStream(fileName, FileMode.Create))
            {
                DgmlWriter.WriteDependencyGraphToStream(dgmlOutput, _dependencyGraph, _nodeFactory);
                dgmlOutput.Flush();
            }
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

            public void RootStaticBasesForType(TypeDesc type, string reason)
            {
                Debug.Assert(!type.IsGenericDefinition);

                MetadataType metadataType = type as MetadataType;
                if (metadataType != null)
                {
                    if (metadataType.ThreadStaticFieldSize.AsInt > 0)
                    {
                        _graph.AddRoot(_factory.TypeThreadStaticIndex(metadataType), reason);
                    }

                    if (metadataType.GCStaticFieldSize.AsInt > 0)
                    {
                        _graph.AddRoot(_factory.TypeGCStaticsSymbol(metadataType), reason);
                    }

                    if (metadataType.NonGCStaticFieldSize.AsInt > 0)
                    {
                        _graph.AddRoot(_factory.TypeNonGCStaticsSymbol(metadataType), reason);
                    }
                }
            }

            public void RootVirtualMethodUse(MethodDesc method, string reason)
            {
                if (!_factory.CompilationModuleGroup.ShouldProduceFullVTable(method.OwningType))
                    _graph.AddRoot(_factory.VirtualMethodUse(method), reason);
            }
        }
    }

    // Interface under which Compilation is exposed externally.
    public interface ICompilation
    {
        void Compile(string outputFileName, ObjectDumper dumper);
        void WriteDependencyLog(string outputFileName);
    }
}
