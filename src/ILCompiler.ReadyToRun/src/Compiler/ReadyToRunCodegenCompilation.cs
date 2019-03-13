// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System.Collections.Generic;
using System.IO;
using System.Reflection.PortableExecutable;

using Internal.IL;
using Internal.JitInterface;
using Internal.TypeSystem;
using Internal.TypeSystem.Ecma;

using ILCompiler.DependencyAnalysis;
using ILCompiler.DependencyAnalysis.ReadyToRun;
using ILCompiler.DependencyAnalysisFramework;

namespace ILCompiler
{
    public sealed class ReadyToRunCodegenCompilation : Compilation
    {
        /// <summary>
        /// Map from method modules to the appropriate CorInfoImpl instantiations
        /// used to propagate the module back to managed code as context for
        /// reference token resolution.
        /// </summary>
        private readonly Dictionary<EcmaModule, CorInfoImpl> _corInfo;

        /// <summary>
        /// JIT configuration provider.
        /// </summary>
        private readonly JitConfigProvider _jitConfigProvider;

        /// <summary>
        /// Name of the compilation input MSIL file.
        /// </summary>
        private readonly string _inputFilePath;

        public new ReadyToRunCodegenNodeFactory NodeFactory { get; }

        public ReadyToRunSymbolNodeFactory SymbolNodeFactory { get; }

        internal ReadyToRunCodegenCompilation(
            DependencyAnalyzerBase<NodeFactory> dependencyGraph,
            ReadyToRunCodegenNodeFactory nodeFactory,
            IEnumerable<ICompilationRootProvider> roots,
            ILProvider ilProvider,
            DebugInformationProvider debugInformationProvider,
            PInvokeILEmitterConfiguration pInvokePolicy,
            Logger logger,
            DevirtualizationManager devirtualizationManager,
            JitConfigProvider configProvider,
            string inputFilePath)
            : base(dependencyGraph, nodeFactory, roots, ilProvider, debugInformationProvider, devirtualizationManager, pInvokePolicy, logger)
        {
            NodeFactory = nodeFactory;
            SymbolNodeFactory = new ReadyToRunSymbolNodeFactory(nodeFactory);
            _corInfo = new Dictionary<EcmaModule, CorInfoImpl>();
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
            using (FileStream inputFile = File.OpenRead(_inputFilePath))
            {
                PEReader inputPeReader = new PEReader(inputFile);

                _dependencyGraph.ComputeMarkedNodes();
                var nodes = _dependencyGraph.MarkedNodeList;

                NodeFactory.SetMarkingComplete();
                ReadyToRunObjectWriter.EmitObject(inputPeReader, outputFile, nodes, NodeFactory);
            }
        }

        internal bool IsInheritanceChainLayoutFixedInCurrentVersionBubble(TypeDesc type)
        {
            // TODO: implement
            return true;
        }

        public override TypeDesc GetTypeOfRuntimeType()
        {
            return TypeSystemContext.SystemModule.GetKnownType("System", "RuntimeType");
        }

        protected override void ComputeDependencyNodeDependencies(List<DependencyNodeCore<NodeFactory>> obj)
        {
            foreach (DependencyNodeCore<NodeFactory> dependency in obj)
            {
                var methodCodeNodeNeedingCode = dependency as MethodWithGCInfo;
                if (methodCodeNodeNeedingCode == null)
                {
                    // To compute dependencies of the shadow method that tracks dictionary
                    // dependencies we need to ensure there is code for the canonical method body.
                    var dependencyMethod = (ShadowConcreteMethodNode)dependency;
                    methodCodeNodeNeedingCode = (MethodWithGCInfo)dependencyMethod.CanonicalMethodNode;
                }

                // We might have already compiled this method.
                if (methodCodeNodeNeedingCode.StaticDependenciesAreComputed)
                    continue;

                MethodDesc method = methodCodeNodeNeedingCode.Method;
                if (!NodeFactory.CompilationModuleGroup.ContainsMethodBody(method, unboxingStub: false))
                {
                    // Don't drill into methods defined outside of this version bubble
                    continue;
                }

                if (Logger.IsVerbose)
                {
                    string methodName = method.ToString();
                    Logger.Writer.WriteLine("Compiling " + methodName);
                }

                try
                {
                    EcmaModule module = ((EcmaMethod)method.GetTypicalMethodDefinition()).Module;

                    CorInfoImpl perModuleCorInfo;
                    if (!_corInfo.TryGetValue(module, out perModuleCorInfo))
                    {
                        perModuleCorInfo = new CorInfoImpl(this, module, _jitConfigProvider);
                        _corInfo.Add(module, perModuleCorInfo);
                    }

                    perModuleCorInfo.CompileMethod(methodCodeNodeNeedingCode);
                }
                catch (TypeSystemException ex)
                {
                    // If compilation fails, don't emit code for this method. It will be Jitted at runtime
                    Logger.Writer.WriteLine($"Warning: Method `{method}` was not compiled because: {ex.Message}");
                }
                catch (RequiresRuntimeJitException ex)
                {
                    Logger.Writer.WriteLine($"Info: Method `{method}` was not compiled because `{ex.Message}` requires runtime JIT");
                }
            }
        }

        public override bool CanInline(MethodDesc callerMethod, MethodDesc calleeMethod)
        {
            // Allow inlining if the target method is within the same version bubble
            return NodeFactory.CompilationModuleGroup.ContainsMethodBody(calleeMethod, unboxingStub: false) ||
                calleeMethod.HasCustomAttribute("System.Runtime.Versioning", "NonVersionableAttribute");
        }

        public override ObjectNode GetFieldRvaData(FieldDesc field) => SymbolNodeFactory.GetRvaFieldNode(field);
    }
}
