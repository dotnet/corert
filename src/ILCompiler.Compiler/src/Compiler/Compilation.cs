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

        private Dictionary<TypeDesc, RegisteredType> _registeredTypes = new Dictionary<TypeDesc, RegisteredType>();
        private Dictionary<MethodDesc, RegisteredMethod> _registeredMethods = new Dictionary<MethodDesc, RegisteredMethod>();
        private Dictionary<FieldDesc, RegisteredField> _registeredFields = new Dictionary<FieldDesc, RegisteredField>();
        private List<MethodDesc> _methodsThatNeedsCompilation = null;

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

        internal IEnumerable<RegisteredType> RegisteredTypes
        {
            get
            {
                return _registeredTypes.Values;
            }
        }

        internal RegisteredType GetRegisteredType(TypeDesc type)
        {
            RegisteredType existingRegistration;
            if (_registeredTypes.TryGetValue(type, out existingRegistration))
                return existingRegistration;

            RegisteredType registration = new RegisteredType() { Type = type };
            _registeredTypes.Add(type, registration);

            // Register all base types too
            var baseType = type.BaseType;
            if (baseType != null)
                GetRegisteredType(baseType);

            return registration;
        }

        internal RegisteredMethod GetRegisteredMethod(MethodDesc method)
        {
            RegisteredMethod existingRegistration;
            if (_registeredMethods.TryGetValue(method, out existingRegistration))
                return existingRegistration;

            RegisteredMethod registration = new RegisteredMethod() { Method = method };
            _registeredMethods.Add(method, registration);

            GetRegisteredType(method.OwningType);

            return registration;
        }

        internal RegisteredField GetRegisteredField(FieldDesc field)
        {
            RegisteredField existingRegistration;
            if (_registeredFields.TryGetValue(field, out existingRegistration))
                return existingRegistration;

            RegisteredField registration = new RegisteredField() { Field = field };
            _registeredFields.Add(field, registration);

            GetRegisteredType(field.OwningType);

            return registration;
        }

        private ILProvider _ilProvider = new ILProvider();

        public MethodIL GetMethodIL(MethodDesc method)
        {
            return _ilProvider.GetMethodIL(method);
        }

        private void CompileMethods()
        {
            var pendingMethods = _methodsThatNeedsCompilation;
            _methodsThatNeedsCompilation = null;

            foreach (MethodDesc method in pendingMethods)
            {
                _cppWriter.CompileMethod(method);
            }
        }

        private void ExpandVirtualMethods()
        {
            // Take a snapshot of _registeredTypes - new registered types can be added during the expansion
            foreach (var reg in _registeredTypes.Values.ToArray())
            {
                if (!reg.Constructed)
                    continue;

                TypeDesc declType = reg.Type;
                while (declType != null)
                {
                    var declReg = GetRegisteredType(declType);
                    if (declReg.VirtualSlots != null)
                    {
                        for (int i = 0; i < declReg.VirtualSlots.Count; i++)
                        {
                            MethodDesc declMethod = declReg.VirtualSlots[i];

                            AddMethod(VirtualFunctionResolution.FindVirtualFunctionTargetMethodOnObjectType(declMethod, reg.Type.GetClosestMetadataType()));
                        }
                    }

                    declType = declType.BaseType;
                }
            }
        }

        private CorInfoImpl _corInfo;

        public void CompileSingleFile(MethodDesc mainMethod)
        {
            if (_options.IsCppCodeGen)
            {
                _cppWriter = new CppCodeGen.CppWriter(this);
            }
            else
            {
                _corInfo = new CorInfoImpl(this);
            }

            _mainMethod = mainMethod;

            if (!_options.IsCppCodeGen)
            {
                NodeFactory.NameMangler = NameMangler;

                _nodeFactory = new NodeFactory(_typeSystemContext);

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

                _dependencyGraph.ComputeDependencyRoutine += ComputeDependencyNodeDependencies;
                var nodes = _dependencyGraph.MarkedNodeList;

                ObjectWriter.EmitObject(OutputPath, nodes, _nodeFactory);

                if (_options.DgmlLog != null)
                {
                    using (FileStream dgmlOutput = new FileStream(_options.DgmlLog, FileMode.Create))
                    {
                        DgmlWriter.WriteDependencyGraphToStream(dgmlOutput, _dependencyGraph);
                        dgmlOutput.Flush();
                    }
                }
            }
            else
            {
                AddWellKnownTypes();
                AddCompilationRoots();

                while (_methodsThatNeedsCompilation != null)
                {
                    CompileMethods();

                    ExpandVirtualMethods();
                }

                _cppWriter.OutputCode();
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
        }

        private void AddCompilationRoot(MethodDesc method, string reason, string exportName = null)
        {
            if (_dependencyGraph != null)
            {
                var methodEntryPoint = _nodeFactory.MethodEntrypoint(method);

                _dependencyGraph.AddRoot(methodEntryPoint, reason);

                if (exportName != null)
                    _nodeFactory.NodeAliases.Add(methodEntryPoint, exportName);
            }
            else
            {
                AddMethod(method);
            }
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

        // List of methods that are known to throw an exception during compilation.
        // On Windows it's fine to throw it because we have a catchall block.
        // On Linux, throwing a managed exception to native code will bring down the process.
        // https://github.com/dotnet/corert/issues/162
        private HashSet<TypeAndMethod> _skipJitList = new HashSet<TypeAndMethod>
        {
            new TypeAndMethod("System.SR", "GetResourceString"),
            new TypeAndMethod("System.Text.StringBuilder", "AppendFormatHelper"),
            new TypeAndMethod("System.Collections.Concurrent.ConcurrentUnifier`2", "GetOrAdd"),
            new TypeAndMethod("System.Globalization.NumberFormatInfo", "GetInstance"),
            new TypeAndMethod("System.Collections.Concurrent.ConcurrentUnifierW`2", "GetOrAdd"),
            new TypeAndMethod("System.Collections.Generic.LowLevelDictionary`2", "Find"),
            new TypeAndMethod("System.Collections.Generic.LowLevelDictionary`2", "GetBucket"),
            new TypeAndMethod("System.Collections.Generic.ArraySortHelper`1", "InternalBinarySearch"),
            new TypeAndMethod("System.RuntimeExceptionHelpers", "SerializeExceptionsForDump"),
            new TypeAndMethod("System.InvokeUtils", "CheckArgument"),
            new TypeAndMethod("System.Runtime.InteropServices.ExceptionHelpers", "GetMappingExceptionForHR"),
        };

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
                    if (Path.DirectorySeparatorChar != '\\' && _skipJitList.Contains(new TypeAndMethod(method.OwningType.Name, method.Name)))
                    {
                        throw new NotImplementedException("SkipJIT");
                    }

                    _corInfo.CompileMethod(methodCodeNodeNeedingCode);
                }
                catch (Exception e)
                {
                    Log.WriteLine("*** " + e.Message + " (" + method + ")");

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

        private void AddWellKnownTypes()
        {
            var stringType = TypeSystemContext.GetWellKnownType(WellKnownType.String);

            // TODO: We are rooting String[] so the bootstrap code can find the EEType for making the command-line args
            // string array.  Once we generate the startup code in managed code, we should remove this
            var arrayOfStringType = stringType.MakeArrayType();

            if (_dependencyGraph != null)
            {
                _dependencyGraph.AddRoot(_nodeFactory.ConstructedTypeSymbol(stringType), "String type is always generated");
                _dependencyGraph.AddRoot(_nodeFactory.ConstructedTypeSymbol(arrayOfStringType), "String[] type is always generated");
            }
            else
            {
                AddType(stringType);
                MarkAsConstructed(stringType);
                AddType(arrayOfStringType);
                MarkAsConstructed(arrayOfStringType);
            }
        }

        public void AddMethod(MethodDesc method)
        {
            RegisteredMethod reg = GetRegisteredMethod(method);
            if (reg.IncludedInCompilation)
                return;
            reg.IncludedInCompilation = true;

            RegisteredType regType = GetRegisteredType(method.OwningType);
            if (regType.Methods == null)
                regType.Methods = new List<RegisteredMethod>();
            regType.Methods.Add(reg);

            if (_methodsThatNeedsCompilation == null)
                _methodsThatNeedsCompilation = new List<MethodDesc>();
            _methodsThatNeedsCompilation.Add(method);

            if (_options.IsCppCodeGen)
            {
                // Precreate name to ensure that all types referenced by signatures are present
                GetRegisteredType(method.OwningType);
                var signature = method.Signature;
                GetRegisteredType(signature.ReturnType);
                for (int i = 0; i < signature.Length; i++)
                    GetRegisteredType(signature[i]);
            }
        }

        public void AddVirtualSlot(MethodDesc method)
        {
            RegisteredType reg = GetRegisteredType(method.OwningType);

            if (reg.VirtualSlots == null)
                reg.VirtualSlots = new List<MethodDesc>();

            for (int i = 0; i < reg.VirtualSlots.Count; i++)
            {
                if (reg.VirtualSlots[i] == method)
                    return;
            }

            reg.VirtualSlots.Add(method);
        }

        public void MarkAsConstructed(TypeDesc type)
        {
            GetRegisteredType(type).Constructed = true;
        }

        public void AddType(TypeDesc type)
        {
            RegisteredType reg = GetRegisteredType(type);
            if (reg.IncludedInCompilation)
                return;
            reg.IncludedInCompilation = true;

            TypeDesc baseType = type.BaseType;
            if (baseType != null)
                AddType(baseType);
            if (type.IsArray)
                AddType(((ArrayType)type).ElementType);
        }

        public void AddField(FieldDesc field)
        {
            RegisteredField reg = GetRegisteredField(field);
            if (reg.IncludedInCompilation)
                return;
            reg.IncludedInCompilation = true;

            if (_options.IsCppCodeGen)
            {
                // Precreate name to ensure that all types referenced by signatures are present
                GetRegisteredType(field.OwningType);
                GetRegisteredType(field.FieldType);
            }
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
