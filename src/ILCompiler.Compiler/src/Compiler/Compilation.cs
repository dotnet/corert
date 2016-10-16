// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Collections.Generic;
using System.IO;

using Internal.TypeSystem;
using Internal.TypeSystem.Ecma;

using Internal.IL;

using Internal.JitInterface;
using ILCompiler.DependencyAnalysis;
using ILCompiler.DependencyAnalysisFramework;

using Debug = System.Diagnostics.Debug;

namespace ILCompiler
{
    public class CompilationOptions
    {
        public string OutputFilePath;

        public bool IsCppCodeGen;
        public bool NoLineNumbers;
        public string DgmlLog;
        public bool FullLog;
        public bool Verbose;

        public IReadOnlyList<string> CodegenOptions = Array.Empty<string>();
    }

    public partial class Compilation
    {
        private readonly CompilerTypeSystemContext _typeSystemContext;
        private readonly CompilationOptions _options;

        private NodeFactory _nodeFactory;
        private DependencyAnalyzerBase<NodeFactory> _dependencyGraph;

        private NameMangler _nameMangler = null;

        private ILCompiler.CppCodeGen.CppWriter _cppWriter = null;
        private CompilationModuleGroup _compilationModuleGroup;

        public Compilation(CompilationOptions options, CompilerTypeSystemContext context, CompilationModuleGroup compilationGroup)
        {
            _options = options;

            _nameMangler = new NameMangler(options.IsCppCodeGen);

            _typeSystemContext = context;
            _compilationModuleGroup = compilationGroup;
        }

        public CompilerTypeSystemContext TypeSystemContext
        {
            get
            {
                return _typeSystemContext;
            }
        }

        public NameMangler NameMangler
        {
            get
            {
                return _nameMangler;
            }
        }

        public NodeFactory NodeFactory
        {
            get
            {
                return _nodeFactory;
            }
        }

        public TextWriter Log
        {
            get;
            set;
        }

        internal CompilationOptions Options
        {
            get
            {
                return _options;
            }
        }

        private ILProvider _methodILCache = new ILProvider();

        public MethodIL GetMethodIL(MethodDesc method)
        {
            // Flush the cache when it grows too big
            if (_methodILCache.Count > 1000)
                _methodILCache = new ILProvider();

            return _methodILCache.GetMethodIL(method);
        }

        private CorInfoImpl _corInfo;

        public void Compile()
        {
            NodeFactory.NameMangler = NameMangler;

            string systemModuleName = ((IAssemblyDesc)_typeSystemContext.SystemModule).GetName().Name;

            // TODO: just something to get Runtime.Base compiled
            if (systemModuleName != "System.Private.CoreLib")
            {
                NodeFactory.CompilationUnitPrefix = systemModuleName.Replace(".", "_");
            }
            else
            {
                NodeFactory.CompilationUnitPrefix = NameMangler.SanitizeName(Path.GetFileNameWithoutExtension(Options.OutputFilePath));
            }

            if (_options.IsCppCodeGen)
            {
                _nodeFactory = new CppCodegenNodeFactory(_typeSystemContext, _compilationModuleGroup);
            }
            else
            {
                _nodeFactory = new RyuJitNodeFactory(_typeSystemContext, _compilationModuleGroup);
            }

            // Choose which dependency graph implementation to use based on the amount of logging requested.
            if (_options.DgmlLog == null)
            {
                // No log uses the NoLogStrategy
                _dependencyGraph = new DependencyAnalyzer<NoLogStrategy<NodeFactory>, NodeFactory>(_nodeFactory, null);
            }
            else
            {
                if (_options.FullLog)
                {
                    // Full log uses the full log strategy
                    _dependencyGraph = new DependencyAnalyzer<FullGraphLogStrategy<NodeFactory>, NodeFactory>(_nodeFactory, null);
                }
                else
                {
                    // Otherwise, use the first mark strategy
                    _dependencyGraph = new DependencyAnalyzer<FirstMarkLogStrategy<NodeFactory>, NodeFactory>(_nodeFactory, null);
                }
            }

            _nodeFactory.AttachToDependencyGraph(_dependencyGraph);

            if (_options.IsCppCodeGen)
            {
                _cppWriter = new CppCodeGen.CppWriter(this);

                _dependencyGraph.ComputeDependencyRoutine += CppCodeGenComputeDependencyNodeDependencies;

                var nodes = _dependencyGraph.MarkedNodeList;

                _cppWriter.OutputCode(nodes, _compilationModuleGroup.StartupCodeMain, _nodeFactory);
            }
            else
            {
                _corInfo = new CorInfoImpl(this, new JitConfigProvider(_options.CodegenOptions));

                _dependencyGraph.ComputeDependencyRoutine += ComputeDependencyNodeDependencies;

                var nodes = _dependencyGraph.MarkedNodeList;

                ObjectWriter.EmitObject(_options.OutputFilePath, nodes, _nodeFactory);
            }

            if (_options.DgmlLog != null)
            {
                using (FileStream dgmlOutput = new FileStream(_options.DgmlLog, FileMode.Create))
                {
                    DgmlWriter.WriteDependencyGraphToStream(dgmlOutput, _dependencyGraph);
                    dgmlOutput.Flush();
                }
            }
        }

        private void ComputeDependencyNodeDependencies(List<DependencyNodeCore<NodeFactory>> obj)
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

                if (_options.Verbose)
                {
                    string methodName = method.ToString();
                    Log.WriteLine("Compiling " + methodName);
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
        /// Compiles the provided method code node while swapping it's body with a throwing stub.
        /// </summary>
        private bool TryCompileWithThrowingBody(MethodCodeNode methodNode, TypeSystemException exception)
        {
            MethodDesc helper;

            Type exceptionType = exception.GetType();
            if (exceptionType == typeof(TypeSystemException.TypeLoadException))
            {
                helper = _typeSystemContext.GetHelperEntryPoint("ThrowHelpers", "ThrowTypeLoadException");
            }
            else if (exceptionType == typeof(TypeSystemException.MissingFieldException))
            {
                helper = _typeSystemContext.GetHelperEntryPoint("ThrowHelpers", "ThrowMissingFieldException");
            }
            else if (exceptionType == typeof(TypeSystemException.MissingMethodException))
            {
                helper = _typeSystemContext.GetHelperEntryPoint("ThrowHelpers", "ThrowMissingMethodException");
            }
            else if (exceptionType == typeof(TypeSystemException.FileNotFoundException))
            {
                helper = _typeSystemContext.GetHelperEntryPoint("ThrowHelpers", "ThrowFileNotFoundException");
            }
            else if (exceptionType == typeof(TypeSystemException.InvalidProgramException))
            {
                helper = _typeSystemContext.GetHelperEntryPoint("ThrowHelpers", "ThrowInvalidProgramException");
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

        private void CppCodeGenComputeDependencyNodeDependencies(List<DependencyNodeCore<NodeFactory>> obj)
        {
            foreach (CppMethodCodeNode methodCodeNodeNeedingCode in obj)
            {
                _cppWriter.CompileMethod(methodCodeNodeNeedingCode);
            }
        }

        public DelegateCreationInfo GetDelegateCtor(TypeDesc delegateType, MethodDesc target)
        {
            return DelegateCreationInfo.Create(delegateType, target, _nodeFactory);
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
                return _nodeFactory.PInvokeMethodFixup(metadata.Module, metadata.Name);
            }
            else
            {
                return _nodeFactory.ReadOnlyDataBlob(NameMangler.GetMangledFieldName(field),
                    ((EcmaField)field).GetFieldRvaData(), _typeSystemContext.Target.PointerSize);
            }
        }

        public bool HasLazyStaticConstructor(TypeDesc type)
        {
            return _typeSystemContext.HasLazyStaticConstructor(type);
        }

        public MethodDebugInformation GetDebugInfo(MethodIL methodIL)
        {
            // This method looks odd right now, but it's an extensibility point that lets us generate
            // fake debugging information for things that don't have physical symbols.
            return methodIL.GetDebugInfo();
        }
    }
}
