// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Collections.Generic;
using System.IO;

using ILCompiler.DependencyAnalysis;
using ILCompiler.DependencyAnalysisFramework;

using Internal.IL;
using Internal.TypeSystem;
using Internal.TypeSystem.Ecma;

namespace ILCompiler
{
    public abstract class Compilation
    {
        protected DependencyAnalyzerBase<NodeFactory> _dependencyGraph;

        internal NameMangler NameMangler { get; }

        internal NodeFactory NodeFactory { get; }

        internal CompilerTypeSystemContext TypeSystemContext => NodeFactory.TypeSystemContext;

        internal Logger Logger { get; private set; } = Logger.Null;

        protected Compilation(NodeFactory nodeFactory, NameMangler nameMangler)
        {
            NameMangler = nameMangler;
            NodeFactory = nodeFactory;

        }

        private ILProvider _methodILCache = new ILProvider();

        internal MethodIL GetMethodIL(MethodDesc method)
        {
            // Flush the cache when it grows too big
            if (_methodILCache.Count > 1000)
                _methodILCache = new ILProvider();

            return _methodILCache.GetMethodIL(method);
        }

        public abstract Compilation UseBackendOptions(IEnumerable<string> options);

        public Compilation ConfigureDependencyGraph(Func<NodeFactory, DependencyAnalyzerBase<NodeFactory>> creator)
        {
            if (_dependencyGraph != null)
                throw new InvalidOperationException();

            _dependencyGraph = creator(NodeFactory);

            _dependencyGraph.ComputeDependencyRoutine += ComputeDependencyNodeDependencies;

            NodeFactory.AttachToDependencyGraph(_dependencyGraph);

            return this;
        }

        public Compilation UseLogger(Logger logger)
        {
            Logger = logger;
            return this;
        }

        protected abstract void ComputeDependencyNodeDependencies(List<DependencyNodeCore<NodeFactory>> obj);

        public void Compile(string outputFile)
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

        public void WriteDependencyLog(string fileName)
        {
            using (FileStream dgmlOutput = new FileStream(fileName, FileMode.Create))
            {
                DgmlWriter.WriteDependencyGraphToStream(dgmlOutput, _dependencyGraph);
                dgmlOutput.Flush();
            }
        }

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
    }
}
