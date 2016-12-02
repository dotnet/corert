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
using AssemblyName = System.Reflection.AssemblyName;

namespace ILCompiler
{
    public abstract class Compilation : ICompilation
    {
        protected readonly DependencyAnalyzerBase<NodeFactory> _dependencyGraph;
        protected readonly NameMangler _nameMangler;
        protected readonly NodeFactory _nodeFactory;
        protected readonly Logger _logger;

        internal NameMangler NameMangler => _nameMangler;
        internal NodeFactory NodeFactory => _nodeFactory;
        internal CompilerTypeSystemContext TypeSystemContext => NodeFactory.TypeSystemContext;
        internal Logger Logger => _logger;

        private readonly TypeGetTypeMethodThunkCache _typeGetTypeMethodThunks;

        protected Compilation(
            DependencyAnalyzerBase<NodeFactory> dependencyGraph,
            NodeFactory nodeFactory,
            IEnumerable<ICompilationRootProvider> compilationRoots,
            NameMangler nameMangler,
            Logger logger)
        {
            _dependencyGraph = dependencyGraph;
            _nodeFactory = nodeFactory;
            _nameMangler = nameMangler;
            _logger = logger;

            _dependencyGraph.ComputeDependencyRoutine += ComputeDependencyNodeDependencies;
            NodeFactory.AttachToDependencyGraph(_dependencyGraph);

            // TODO: hacky static field
            NodeFactory.NameMangler = nameMangler;

            var rootingService = new RootingServiceProvider(dependencyGraph, nodeFactory);
            foreach (var rootProvider in compilationRoots)
                rootProvider.AddCompilationRoots(rootingService);

            // TODO: use a better owning type for multi-file friendliness
            _typeGetTypeMethodThunks = new TypeGetTypeMethodThunkCache(TypeSystemContext.SystemModule.GetGlobalModuleType());
        }

        private ILProvider _methodILCache = new ILProvider();

        internal MethodIL GetMethodIL(MethodDesc method)
        {
            // Flush the cache when it grows too big
            if (_methodILCache.Count > 1000)
                _methodILCache = new ILProvider();

            return _methodILCache.GetMethodIL(method);
        }

        protected abstract void ComputeDependencyNodeDependencies(List<DependencyNodeCore<NodeFactory>> obj);

        protected abstract void CompileInternal(string outputFile);

        public DelegateCreationInfo GetDelegateCtor(TypeDesc delegateType, MethodDesc target)
        {
            return DelegateCreationInfo.Create(delegateType, target, NodeFactory);
        }

        /// <summary>
        /// Gets an object representing the static data for RVA mapped fields from the PE image.
        /// </summary>
        public ObjectNode GetFieldRvaData(FieldDesc field)
        {
            if (field.GetType() == typeof(Internal.IL.Stubs.PInvokeLazyFixupField))
            {
                var pInvokeFixup = (Internal.IL.Stubs.PInvokeLazyFixupField)field;
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

        void ICompilation.Compile(string outputFile)
        {
            // In multi-module builds, set the compilation unit prefix to prevent ambiguous symbols in linked object files
            _nameMangler.CompilationUnitPrefix = _nodeFactory.CompilationModuleGroup.IsSingleFileCompilation ? "" : NodeFactory.NameMangler.SanitizeName(Path.GetFileNameWithoutExtension(outputFile));
            CompileInternal(outputFile);
        }

        void ICompilation.WriteDependencyLog(string fileName)
        {
            using (FileStream dgmlOutput = new FileStream(fileName, FileMode.Create))
            {
                DgmlWriter.WriteDependencyGraphToStream(dgmlOutput, _dependencyGraph);
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
                var methodEntryPoint = _factory.MethodEntrypoint(method);

                _graph.AddRoot(methodEntryPoint, reason);

                if (exportName != null)
                    _factory.NodeAliases.Add(methodEntryPoint, exportName);
            }

            public void AddCompilationRoot(TypeDesc type, string reason)
            {
                if (type.IsGenericDefinition)
                    _graph.AddRoot(_factory.NecessaryTypeSymbol(type), reason);
                else
                    _graph.AddRoot(_factory.ConstructedTypeSymbol(type), reason);
            }
        }
    }

    // Interface under which Compilation is exposed externally.
    public interface ICompilation
    {
        void Compile(string outputFileName);
        void WriteDependencyLog(string outputFileName);
    }
}
