// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using System.Collections.Generic;
using System.Runtime.CompilerServices;
using System.Threading;

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
        private readonly ConditionalWeakTable<Thread, CorInfoImpl> _corinfos = new ConditionalWeakTable<Thread, CorInfoImpl>();
        internal readonly RyuJitCompilationOptions _compilationOptions;
        private readonly ExternSymbolMappedField _hardwareIntrinsicFlags;
        private CountdownEvent _compilationCountdown;
        private readonly Dictionary<string, InstructionSet> _instructionSetMap;

        public InstructionSetSupport InstructionSetSupport { get; }

        internal RyuJitCompilation(
            DependencyAnalyzerBase<NodeFactory> dependencyGraph,
            NodeFactory nodeFactory,
            IEnumerable<ICompilationRootProvider> roots,
            ILProvider ilProvider,
            DebugInformationProvider debugInformationProvider,
            Logger logger,
            DevirtualizationManager devirtualizationManager,
            InstructionSetSupport instructionSetSupport,
            RyuJitCompilationOptions options)
            : base(dependencyGraph, nodeFactory, roots, ilProvider, debugInformationProvider, devirtualizationManager, logger)
        {
            _compilationOptions = options;
            _hardwareIntrinsicFlags = new ExternSymbolMappedField(nodeFactory.TypeSystemContext.GetWellKnownType(WellKnownType.Int32), "g_cpuFeatures");
            InstructionSetSupport = instructionSetSupport;

            _instructionSetMap = new Dictionary<string, InstructionSet>();
            foreach (var instructionSetInfo in InstructionSetFlags.ArchitectureToValidInstructionSets(TypeSystemContext.Target.Architecture))
            {
                if (!instructionSetInfo.Specifiable)
                    continue;

                _instructionSetMap.Add(instructionSetInfo.ManagedName, instructionSetInfo.InstructionSet);
            }
        }

        protected override void CompileInternal(string outputFile, ObjectDumper dumper)
        {
            _dependencyGraph.ComputeMarkedNodes();
            var nodes = _dependencyGraph.MarkedNodeList;

            NodeFactory.SetMarkingComplete();
            ObjectWriter.EmitObject(outputFile, nodes, NodeFactory, dumper);
        }

        protected override void ComputeDependencyNodeDependencies(List<DependencyNodeCore<NodeFactory>> obj)
        {
            // Determine the list of method we actually need to compile
            var methodsToCompile = new List<MethodCodeNode>();
            var canonicalMethodsToCompile = new HashSet<MethodDesc>();

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

                // We might have already queued this method for compilation
                MethodDesc method = methodCodeNodeNeedingCode.Method;
                if (method.IsCanonicalMethod(CanonicalFormKind.Any)
                    && !canonicalMethodsToCompile.Add(method))
                {
                    continue;
                }

                methodsToCompile.Add(methodCodeNodeNeedingCode);
            }

            if ((_compilationOptions & RyuJitCompilationOptions.SingleThreadedCompilation) != 0)
            {
                CompileSingleThreaded(methodsToCompile);
            }
            else
            {
                CompileMultiThreaded(methodsToCompile);
            }
        }
        private void CompileMultiThreaded(List<MethodCodeNode> methodsToCompile)
        {
            if (Logger.IsVerbose)
            {
                Logger.Writer.WriteLine($"Compiling {methodsToCompile.Count} methods...");
            }

            WaitCallback compileSingleMethodDelegate = m =>
            {
                CorInfoImpl corInfo = _corinfos.GetValue(Thread.CurrentThread, thread => new CorInfoImpl(this));
                CompileSingleMethod(corInfo, (MethodCodeNode)m);
            };

            using (_compilationCountdown = new CountdownEvent(methodsToCompile.Count))
            {

                foreach (MethodCodeNode methodCodeNodeNeedingCode in methodsToCompile)
                {
                    ThreadPool.QueueUserWorkItem(compileSingleMethodDelegate, methodCodeNodeNeedingCode);
                }

                _compilationCountdown.Wait();
                _compilationCountdown = null;
            }
        }


        private void CompileSingleThreaded(List<MethodCodeNode> methodsToCompile)
        {
            CorInfoImpl corInfo = _corinfos.GetValue(Thread.CurrentThread, thread => new CorInfoImpl(this));

            foreach (MethodCodeNode methodCodeNodeNeedingCode in methodsToCompile)
            {
                if (Logger.IsVerbose)
                {
                    Logger.Writer.WriteLine($"Compiling {methodCodeNodeNeedingCode.Method}...");
                }

                CompileSingleMethod(corInfo, methodCodeNodeNeedingCode);
            }
        }

        private void CompileSingleMethod(CorInfoImpl corInfo, MethodCodeNode methodCodeNodeNeedingCode)
        {
            MethodDesc method = methodCodeNodeNeedingCode.Method;

            try
            {
                corInfo.CompileMethod(methodCodeNodeNeedingCode);
            }
            catch (TypeSystemException ex)
            {
                // TODO: fail compilation if a switch was passed

                // Try to compile the method again, but with a throwing method body this time.
                MethodIL throwingIL = TypeSystemThrowingILEmitter.EmitIL(method, ex);
                corInfo.CompileMethod(methodCodeNodeNeedingCode, throwingIL);

                // TODO: Log as a warning. For now, just log to the logger; but this needs to
                // have an error code, be supressible, the method name/sig needs to be properly formatted, etc.
                // https://github.com/dotnet/corert/issues/72
                Logger.Writer.WriteLine($"Warning: Method `{method}` will always throw because: {ex.Message}");
            }
            finally
            {
                if (_compilationCountdown != null)
                    _compilationCountdown.Signal();
            }
        }

        public override MethodIL GetMethodIL(MethodDesc method)
        {
            TypeDesc owningType = method.OwningType;
            string intrinsicId = InstructionSetSupport.GetHardwareIntrinsicId(TypeSystemContext.Target.Architecture, owningType);
            if (!string.IsNullOrEmpty(intrinsicId)
                && HardwareIntrinsicHelpers.IsIsSupportedMethod(method))
            {
                InstructionSet instructionSet = _instructionSetMap[intrinsicId];

                // If this is an instruction set that is optimistically supported, but is not one of the
                // intrinsics that are known to be always available, emit IL that checks the support level
                // at runtime.
                if (!InstructionSetSupport.IsInstructionSetSupported(instructionSet)
                    && InstructionSetSupport.OptimisticFlags.HasInstructionSet(instructionSet))
                {
                    return HardwareIntrinsicHelpers.EmitIsSupportedIL(method, _hardwareIntrinsicFlags);
                }
            }

            return base.GetMethodIL(method);
        }

        public bool IsHardwareInstrinsicWithRuntimeDeterminedSupport(MethodDesc method)
        {
            string intrinsicId = InstructionSetSupport.GetHardwareIntrinsicId(TypeSystemContext.Target.Architecture, method.OwningType);
            if (!string.IsNullOrEmpty(intrinsicId))
            {
                InstructionSet instructionSet = _instructionSetMap[intrinsicId];
                return !InstructionSetSupport.IsInstructionSetSupported(instructionSet)
                    && InstructionSetSupport.OptimisticFlags.HasInstructionSet(instructionSet);
            }

            return false;
        }
    }

    [Flags]
    public enum RyuJitCompilationOptions
    {
        MethodBodyFolding = 0x1,
        SingleThreadedCompilation = 0x2,
    }
}
