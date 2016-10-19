// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Collections.Generic;

using ILCompiler.DependencyAnalysis;
using ILCompiler.DependencyAnalysisFramework;

using Internal.IL;
using Internal.TypeSystem;
using Internal.JitInterface;

using Debug = System.Diagnostics.Debug;

namespace ILCompiler
{
    public sealed class RyuJitCompilation : Compilation
    {
        private CorInfoImpl _corInfo;
        private JitConfigProvider _jitConfigProvider;

        internal RyuJitCompilation(
            DependencyAnalyzerBase<NodeFactory> dependencyGraph,
            NodeFactory nodeFactory,
            Logger logger,
            JitConfigProvider configProvider)
            : base(dependencyGraph, nodeFactory, new NameMangler(false), logger)
        {
            _jitConfigProvider = configProvider;
        }

        protected override void CompileInternal(string outputFile)
        {
            _corInfo = new CorInfoImpl(this, _jitConfigProvider);

            var nodes = _dependencyGraph.MarkedNodeList;

            ObjectWriter.EmitObject(outputFile, nodes, NodeFactory);
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
                    var dependencyMethod = (ShadowConcreteMethodNode<MethodCodeNode>)dependency;
                    methodCodeNodeNeedingCode = dependencyMethod.CanonicalMethodNode;
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
                    if (!TryCompileWithThrowingBody(methodCodeNodeNeedingCode, ex))
                        throw;
                    // TODO: Log as a warning
                }
            }
        }

        /// <summary>
        /// Compiles the provided method code node while swapping its body with a throwing stub.
        /// </summary>
        private bool TryCompileWithThrowingBody(MethodCodeNode methodNode, TypeSystemException exception)
        {
            MethodDesc helper;

            Type exceptionType = exception.GetType();
            if (exceptionType == typeof(TypeSystemException.TypeLoadException))
            {
                helper = TypeSystemContext.GetHelperEntryPoint("ThrowHelpers", "ThrowTypeLoadException");
            }
            else if (exceptionType == typeof(TypeSystemException.MissingFieldException))
            {
                helper = TypeSystemContext.GetHelperEntryPoint("ThrowHelpers", "ThrowMissingFieldException");
            }
            else if (exceptionType == typeof(TypeSystemException.MissingMethodException))
            {
                helper = TypeSystemContext.GetHelperEntryPoint("ThrowHelpers", "ThrowMissingMethodException");
            }
            else if (exceptionType == typeof(TypeSystemException.FileNotFoundException))
            {
                helper = TypeSystemContext.GetHelperEntryPoint("ThrowHelpers", "ThrowFileNotFoundException");
            }
            else if (exceptionType == typeof(TypeSystemException.InvalidProgramException))
            {
                helper = TypeSystemContext.GetHelperEntryPoint("ThrowHelpers", "ThrowInvalidProgramException");
            }
            else
            {
                return false;
            }

            Debug.Assert(helper.Signature.Length == exception.Arguments.Count + 1);

            var emitter = new Internal.IL.Stubs.ILEmitter();
            var codeStream = emitter.NewCodeStream();

            var infinityLabel = emitter.NewCodeLabel();
            codeStream.EmitLabel(infinityLabel);

            codeStream.EmitLdc((int)exception.StringID);

            foreach (var arg in exception.Arguments)
            {
                codeStream.Emit(ILOpcode.ldstr, emitter.NewToken(arg));
            }

            codeStream.Emit(ILOpcode.call, emitter.NewToken(helper));

            // The call will never return, but we still need to emit something. Emit a jump so that
            // we don't have to bother balancing the stack if the method returns something.
            codeStream.Emit(ILOpcode.br, infinityLabel);

            _corInfo.CompileMethod(methodNode, emitter.Link(methodNode.Method));

            return true;
        }
    }
}
