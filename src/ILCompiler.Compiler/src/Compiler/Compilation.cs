// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System.Collections.Generic;
using System.IO;

using ILCompiler.DependencyAnalysis;
using ILCompiler.DependencyAnalysisFramework;

using Internal.IL;
using Internal.TypeSystem;
using Internal.TypeSystem.Ecma;

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

            var rootingService = new RootingServiceProvider(dependencyGraph, nodeFactory);
            foreach (var rootProvider in compilationRoots)
                rootProvider.AddCompilationRoots(rootingService);
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

        void ICompilation.Compile(string outputFile)
        {
            // TODO: Hacky static fields

            NodeFactory.NameMangler = NameMangler;

            string systemModuleName = ((IAssemblyDesc)NodeFactory.TypeSystemContext.SystemModule).GetName().Name;

            // TODO: just something to get Runtime.Base compiled
            if (systemModuleName != "System.Private.CoreLib")
            {
                NodeFactory.CompilationUnitPrefix = systemModuleName.Replace(".", "_");
            }
            else
            {
                NodeFactory.CompilationUnitPrefix = NameMangler.SanitizeName(Path.GetFileNameWithoutExtension(outputFile));
            }

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
