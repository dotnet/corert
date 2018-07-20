// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System.Collections.Generic;
using System.IO;
using System.Reflection.PortableExecutable;

using Internal.JitInterface;
using Internal.TypeSystem;

using ILCompiler.DependencyAnalysis;
using ILCompiler.DependencyAnalysis.ReadyToRun;
using ILCompiler.DependencyAnalysisFramework;

namespace ILCompiler
{
    public sealed class ReadyToRunCodegenCompilation : Compilation
    {
        private CorInfoImpl _corInfo;
        private JitConfigProvider _jitConfigProvider;
        string _inputFilePath;

        public new ReadyToRunCodegenNodeFactory NodeFactory { get; }
        internal ReadyToRunCodegenCompilation(
            DependencyAnalyzerBase<NodeFactory> dependencyGraph,
            ReadyToRunCodegenNodeFactory nodeFactory,
            IEnumerable<ICompilationRootProvider> roots,
            DebugInformationProvider debugInformationProvider,
            Logger logger,
            DevirtualizationManager devirtualizationManager,
            JitConfigProvider configProvider,
            string inputFilePath)
            : base(dependencyGraph, nodeFactory, roots, debugInformationProvider, devirtualizationManager, logger)
        {
            NodeFactory = nodeFactory;
            _jitConfigProvider = configProvider;
            _inputFilePath = inputFilePath;
        }

        private static IEnumerable<ICompilationRootProvider> GetCompilationRoots(IEnumerable<ICompilationRootProvider> existingRoots, NodeFactory factory)
        {
            // Todo: Eventually this should return an interesting set of roots, such as the ready-to-run header
            yield break;
        }

        protected override void CompileInternal(string outputFile, ObjectDumper dumper)
        {
            _corInfo = new CorInfoImpl(this, _jitConfigProvider);

            using (FileStream inputFile = File.OpenRead(_inputFilePath))
            {
                NodeFactory.PEReader = new PEReader(inputFile);

                _dependencyGraph.ComputeMarkedNodes();
                var nodes = _dependencyGraph.MarkedNodeList;

                NodeFactory.SetMarkingComplete();
                ReadyToRunObjectWriter.EmitObject(outputFile, nodes, NodeFactory);
            }
        }

        protected override void ComputeDependencyNodeDependencies(List<DependencyNodeCore<NodeFactory>> obj)
        {
            foreach (DependencyNodeCore<NodeFactory> dependency in obj)
            {
                var methodCodeNodeNeedingCode = dependency as MethodCodeNode;
                if (methodCodeNodeNeedingCode == null)
                {
                    // To compute dependencies of the shadow method that tracks dictionary
                    // dependencies we need to ensure there is code for the canonical method body.
                    var dependencyMethod = (ShadowConcreteMethodNode)dependency;
                    methodCodeNodeNeedingCode = (MethodCodeNode)dependencyMethod.CanonicalMethodNode;
                }

                // We might have already compiled this method.
                if (methodCodeNodeNeedingCode.StaticDependenciesAreComputed)
                    continue;

                MethodDesc method = methodCodeNodeNeedingCode.Method;

                if (Logger.IsVerbose)
                {
                    string methodName = method.ToString();
                    Logger.Writer.WriteLine("Compiling " + methodName);
                }

                try
                {
                    _corInfo.CompileMethod(methodCodeNodeNeedingCode);
                }
                catch (TypeSystemException ex)
                {
                    // If compilation fails, don't emit code for this method. It will be Jitted at runtime
                    Logger.Writer.WriteLine($"Warning: Method `{method}` was not compiled because: {ex.Message}");
                }
            }
        }
    }
}
