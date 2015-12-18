// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text;

using Internal.TypeSystem;
using Internal.TypeSystem.Ecma;

using Internal.IL;
using Internal.IL.Stubs;

using Internal.JitInterface;
using ILCompiler.DependencyAnalysis;
using ILCompiler.DependencyAnalysisFramework;

namespace ILCompiler
{
    public struct CompilationOptions
    {
        public bool IsCppCodeGen;
        public bool NoLineNumbers;
        public string DgmlLog;
        public bool FullLog;
        public bool Verbose;
    }

    public partial class Compilation
    {
        private readonly CompilerTypeSystemContext _typeSystemContext;
        private readonly CompilationOptions _options;

        private NodeFactory _nodeFactory;
        private DependencyAnalyzerBase<NodeFactory> _dependencyGraph;

        private NameMangler _nameMangler = null;

        private ILCompiler.CppCodeGen.CppWriter _cppWriter = null;

        public Compilation(CompilerTypeSystemContext typeSystemContext, CompilationOptions options)
        {
            _typeSystemContext = typeSystemContext;
            _options = options;

            _nameMangler = new NameMangler(this);
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

        public string OutputPath
        {
            get;
            set;
        }

        public TextWriter Out
        {
            get;
            set;
        }

        private MethodDesc _mainMethod;

        internal MethodDesc MainMethod
        {
            get
            {
                return _mainMethod;
            }
        }

        internal bool IsCppCodeGen
        {
            get
            {
                return _options.IsCppCodeGen;
            }
        }

        internal CompilationOptions Options
        {
            get
            {
                return _options;
            }
        }

        private ILProvider _ilProvider = new ILProvider();

        public MethodIL GetMethodIL(MethodDesc method)
        {
            return _ilProvider.GetMethodIL(method);
        }

        private CorInfoImpl _corInfo;

        public void CompileSingleFile(MethodDesc mainMethod)
        {
            _mainMethod = mainMethod;

            NodeFactory.NameMangler = NameMangler;

            _nodeFactory = new NodeFactory(_typeSystemContext, _options.IsCppCodeGen);

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

            AddWellKnownTypes();
            AddCompilationRoots();

            if (_options.IsCppCodeGen)
            {
                _cppWriter = new CppCodeGen.CppWriter(this);

                _dependencyGraph.ComputeDependencyRoutine += CppCodeGenComputeDependencyNodeDependencies;

                var nodes = _dependencyGraph.MarkedNodeList;

                _cppWriter.OutputCode(nodes);
            }
            else
            {
                _corInfo = new CorInfoImpl(this);

                _dependencyGraph.ComputeDependencyRoutine += ComputeDependencyNodeDependencies;

                var nodes = _dependencyGraph.MarkedNodeList;

                ObjectWriter.EmitObject(OutputPath, nodes, _nodeFactory);
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

        private void AddCompilationRoots()
        {
            if (_mainMethod != null)
            {
                AddCompilationRoot(_mainMethod, "Main method", "__managed__Main");
            }

            foreach (var inputFile in _typeSystemContext.InputFilePaths)
            {
                var module = _typeSystemContext.GetModuleFromPath(inputFile.Value);
                AddCompilationRootsForRuntimeExports(module);
           }

            AddCompilationRootsForRuntimeExports((EcmaModule)_typeSystemContext.SystemModule);
        }

        private void AddCompilationRootsForRuntimeExports(EcmaModule module)
        {
            foreach (var type in module.GetAllTypes())
            {
                foreach (var method in type.GetMethods())
                {
                    if (method.HasCustomAttribute("System.Runtime", "RuntimeExportAttribute"))
                    {
                        string exportName = ((EcmaMethod)method).GetAttributeStringValue("System.Runtime", "RuntimeExportAttribute");
                        AddCompilationRoot(method, "Runtime export", exportName);
                    }
                }
            }
        }
 
        private void AddCompilationRoot(MethodDesc method, string reason, string exportName = null)
        {
            var methodEntryPoint = _nodeFactory.MethodEntrypoint(method);

            _dependencyGraph.AddRoot(methodEntryPoint, reason);

            if (exportName != null)
                _nodeFactory.NodeAliases.Add(methodEntryPoint, exportName);
        }

        private struct TypeAndMethod
        {
            public string TypeName;
            public string MethodName;
            public TypeAndMethod(string typeName, string methodName)
            {
                TypeName = typeName;
                MethodName = methodName;
            }
        }

        private void ComputeDependencyNodeDependencies(List<DependencyNodeCore<NodeFactory>> obj)
        {
            foreach (MethodCodeNode methodCodeNodeNeedingCode in obj)
            {
                MethodDesc method = methodCodeNodeNeedingCode.Method;
                string methodName = method.ToString();
                Log.WriteLine("Compiling " + methodName);

                var methodIL = _ilProvider.GetMethodIL(method);
                if (methodIL == null)
                    return;

                try
                {
                    _corInfo.CompileMethod(methodCodeNodeNeedingCode);
                }
                catch (Exception e)
                {
                    Log.WriteLine("*** " + method + ": " + e.Message);

                    // Call the __not_yet_implemented method
                    DependencyAnalysis.X64.X64Emitter emit = new DependencyAnalysis.X64.X64Emitter(_nodeFactory);
                    emit.Builder.RequireAlignment(_nodeFactory.Target.MinimumFunctionAlignment);
                    emit.Builder.DefinedSymbols.Add(methodCodeNodeNeedingCode);

                    emit.EmitLEAQ(emit.TargetRegister.Arg0, _nodeFactory.StringIndirection(method.ToString()));
                    DependencyAnalysis.X64.AddrMode loadFromArg0 =
                        new DependencyAnalysis.X64.AddrMode(emit.TargetRegister.Arg0, null, 0, 0, DependencyAnalysis.X64.AddrModeSize.Int64);
                    emit.EmitMOV(emit.TargetRegister.Arg0, ref loadFromArg0);
                    emit.EmitMOV(emit.TargetRegister.Arg0, ref loadFromArg0);

                    emit.EmitLEAQ(emit.TargetRegister.Arg1, _nodeFactory.StringIndirection(e.Message));
                    DependencyAnalysis.X64.AddrMode loadFromArg1 =
                        new DependencyAnalysis.X64.AddrMode(emit.TargetRegister.Arg1, null, 0, 0, DependencyAnalysis.X64.AddrModeSize.Int64);
                    emit.EmitMOV(emit.TargetRegister.Arg1, ref loadFromArg1);
                    emit.EmitMOV(emit.TargetRegister.Arg1, ref loadFromArg1);

                    emit.EmitJMP(_nodeFactory.ExternSymbol("__not_yet_implemented"));
                    methodCodeNodeNeedingCode.SetCode(emit.Builder.ToObjectData());
                    continue;
                }
            }
        }

        private void CppCodeGenComputeDependencyNodeDependencies(List<DependencyNodeCore<NodeFactory>> obj)
        {
            foreach (CppMethodCodeNode methodCodeNodeNeedingCode in obj)
            {
                _cppWriter.CompileMethod(methodCodeNodeNeedingCode);
            }
        }

        private void AddWellKnownTypes()
        {
            var stringType = TypeSystemContext.GetWellKnownType(WellKnownType.String);

            _dependencyGraph.AddRoot(_nodeFactory.ConstructedTypeSymbol(stringType), "String type is always generated");

            // TODO: We are rooting String[] so the bootstrap code can find the EEType for making the command-line args
            // string array.  Once we generate the startup code in managed code, we should remove this
            var arrayOfStringType = stringType.MakeArrayType();
            _dependencyGraph.AddRoot(_nodeFactory.ConstructedTypeSymbol(arrayOfStringType), "String[] type is always generated");
        }

        private Dictionary<MethodDesc, DelegateInfo> _delegateInfos = new Dictionary<MethodDesc, DelegateInfo>();
        public DelegateInfo GetDelegateCtor(MethodDesc target)
        {
            DelegateInfo info;

            if (!_delegateInfos.TryGetValue(target, out info))
            {
                _delegateInfos.Add(target, info = new DelegateInfo(this, target));
            }

            return info;
        }

        /// <summary>
        /// Gets an object representing the static data for RVA mapped fields from the PE image.
        /// </summary>
        public object GetFieldRvaData(FieldDesc field)
        {
            return _nodeFactory.ReadOnlyDataBlob(NameMangler.GetMangledFieldName(field),
                ((EcmaField)field).GetFieldRvaData(), _typeSystemContext.Target.PointerSize);
        }
    }
}
