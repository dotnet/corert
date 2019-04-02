// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Collections.Generic;

using ILCompiler.DependencyAnalysis;
using ILCompiler.DependencyAnalysisFramework;

using Internal.IL;
using Internal.IL.Stubs;
using Internal.TypeSystem;
using Internal.JitInterface;

namespace ILCompiler
{
    public sealed class RyuJitCompilation : Compilation
    {
        private CorInfoImpl _corInfo;
        private JitConfigProvider _jitConfigProvider;
        internal readonly RyuJitCompilationOptions _compilationOptions;

        internal RyuJitCompilation(
            DependencyAnalyzerBase<NodeFactory> dependencyGraph,
            NodeFactory nodeFactory,
            IEnumerable<ICompilationRootProvider> roots,
            ILProvider ilProvider,
            DebugInformationProvider debugInformationProvider,
            PInvokeILEmitterConfiguration pinvokePolicy,
            Logger logger,
            DevirtualizationManager devirtualizationManager,
            JitConfigProvider configProvider,
            RyuJitCompilationOptions options)
            : base(dependencyGraph, nodeFactory, roots, ilProvider, debugInformationProvider, devirtualizationManager, pinvokePolicy, logger)
        {
            _jitConfigProvider = configProvider;
            _compilationOptions = options;
        }

        protected override void CompileInternal(string outputFile, ObjectDumper dumper)
        {
            _corInfo = new CorInfoImpl(this, _jitConfigProvider);

            _dependencyGraph.ComputeMarkedNodes();
            var nodes = _dependencyGraph.MarkedNodeList;

            NodeFactory.SetMarkingComplete();
            ObjectWriter.EmitObject(outputFile, nodes, NodeFactory, dumper);
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
                    // TODO: fail compilation if a switch was passed

                    // Try to compile the method again, but with a throwing method body this time.
                    MethodIL throwingIL = TypeSystemThrowingILEmitter.EmitIL(method, ex);
                    _corInfo.CompileMethod(methodCodeNodeNeedingCode, throwingIL);

                    // TODO: Log as a warning. For now, just log to the logger; but this needs to
                    // have an error code, be supressible, the method name/sig needs to be properly formatted, etc.
                    // https://github.com/dotnet/corert/issues/72
                    Logger.Writer.WriteLine($"Warning: Method `{method}` will always throw because: {ex.Message}");
                }
            }
        }
    }

    [Flags]
    public enum RyuJitCompilationOptions
    {
        MethodBodyFolding = 0x1,
    }
}
