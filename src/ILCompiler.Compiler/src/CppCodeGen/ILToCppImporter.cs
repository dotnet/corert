// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Text;
using System.Collections.Generic;
using System.Diagnostics;

using Internal.TypeSystem;

using ILCompiler;
using ILCompiler.Compiler.CppCodeGen;
using ILCompiler.CppCodeGen;
using ILCompiler.SymbolReader;

using ILCompiler.DependencyAnalysis;

namespace Internal.IL
{
    internal partial class ILImporter
    {
        private Compilation _compilation;
        private NodeFactory _nodeFactory;
        private CppWriter _writer;

        private TypeSystemContext _typeSystemContext;

        private MethodDesc _method;
        private MethodSignature _methodSignature;

        private TypeDesc _thisType;

        private MethodIL _methodIL;
        private byte[] _ilBytes;
        private LocalVariableDefinition[] _locals;

        private struct SequencePoint
        {
            public string Document;
            public int LineNumber;
        }
        private SequencePoint[] _sequencePoints;
        private Dictionary<int, ILLocalVariable> _localSlotToInfoMap;
        private Dictionary<int, string> _parameterIndexToNameMap;

        private class ExceptionRegion
        {
            public ILExceptionRegion ILRegion;
            public int ReturnLabels;
        };
        private ExceptionRegion[] _exceptionRegions;

        private class SpillSlot
        {
            public StackValueKind Kind;
            public TypeDesc Type;
            public String Name;
        };
        private List<SpillSlot> _spillSlots;

        // TODO: Unify with verifier?
        [Flags]
        private enum Prefix
        {
            ReadOnly = 0x01,
            Unaligned = 0x02,
            Volatile = 0x04,
            Tail = 0x08,
            Constrained = 0x10,
            No = 0x20,
        }
        private Prefix _pendingPrefix;
        private TypeDesc _constrained;

        private CppGenerationBuffer _builder = new CppGenerationBuffer();
        private ArrayBuilder<object> _dependencies = new ArrayBuilder<object>();

        private class BasicBlock
        {
            // Common fields
            public BasicBlock Next;

            public int StartOffset;
            public int EndOffset;

            public StackValue[] EntryStack;

            public bool TryStart;
            public bool FilterStart;
            public bool HandlerStart;

            // Custom fields
            public string Code;
        };

        public ILImporter(Compilation compilation, CppWriter writer, MethodDesc method, MethodIL methodIL)
        {
            _compilation = compilation;
            _nodeFactory = _compilation.NodeFactory;

            _writer = writer;

            _method = method;
            _methodSignature = method.Signature;

            _typeSystemContext = method.Context;

            _msvc = (_typeSystemContext.Target.OperatingSystem == TargetOS.Windows);

            if (!_methodSignature.IsStatic)
                _thisType = method.OwningType;

            _methodIL = methodIL;

            _ilBytes = _methodIL.GetILBytes();
            _locals = _methodIL.GetLocals();

            var ilExceptionRegions = _methodIL.GetExceptionRegions();
            _exceptionRegions = new ExceptionRegion[ilExceptionRegions.Length];
            for (int i = 0; i < ilExceptionRegions.Length; i++)
            {
                _exceptionRegions[i] = new ExceptionRegion() { ILRegion = ilExceptionRegions[i] };
            }
        }

        public void SetSequencePoints(IEnumerable<ILSequencePoint> ilSequencePoints)
        {
            try
            {
                SequencePoint[] sequencePoints = new SequencePoint[_ilBytes.Length];
                foreach (var point in ilSequencePoints)
                {
                    sequencePoints[point.Offset] = new SequencePoint() { Document = point.Document, LineNumber = point.LineNumber };
                }
                _sequencePoints = sequencePoints;
            }
            catch
            {
            }
        }

        public void SetLocalVariables(IEnumerable<ILLocalVariable> localVariables)
        {
            try
            {
                HashSet<string> names = new HashSet<string>();
                var localSlotToInfoMap = new Dictionary<int, ILLocalVariable>();
                foreach (var v in localVariables)
                {
                    ILLocalVariable modifiedLocal = v;
                    modifiedLocal.Name = _compilation.NameMangler.SanitizeName(modifiedLocal.Name);
                    if (!names.Add(v.Name))
                    {
                        modifiedLocal.Name = string.Format("{0}_local{1}", v.Name, v.Slot);
                        names.Add(modifiedLocal.Name);
                    }

                    localSlotToInfoMap[v.Slot] = modifiedLocal;
                }
                _localSlotToInfoMap = localSlotToInfoMap;
            }
            catch
            {
                // oops, couldn't get local variables
            }
        }

        public void SetParameterNames(IEnumerable<string> parameters)
        {
            var parameterIndexToNameMap = new Dictionary<int, string>();
            int index = 0;
            foreach (var p in parameters)
            {
                parameterIndexToNameMap[index] = p;
                ++index;
            }

            _parameterIndexToNameMap = parameterIndexToNameMap;
        }

        private struct Value
        {
            public Value(String name)
            {
                Name = name;
                Aux = null;
            }

            public String Name;
            public Object Aux;
        };

        private struct StackValue
        {
            public StackValueKind Kind;
            public TypeDesc Type;
            public Value Value;
        }

        private StackValueKind GetStackValueKind(TypeDesc type)
        {
            switch (type.Category)
            {
                case TypeFlags.Boolean:
                case TypeFlags.Char:
                case TypeFlags.SByte:
                case TypeFlags.Byte:
                case TypeFlags.Int16:
                case TypeFlags.UInt16:
                case TypeFlags.Int32:
                case TypeFlags.UInt32:
                    return StackValueKind.Int32;
                case TypeFlags.Int64:
                case TypeFlags.UInt64:
                    return StackValueKind.Int64;
                case TypeFlags.Single:
                case TypeFlags.Double:
                    return StackValueKind.Float;
                case TypeFlags.IntPtr:
                case TypeFlags.UIntPtr:
                    return StackValueKind.NativeInt;
                case TypeFlags.ValueType:
                case TypeFlags.Nullable:
                    return StackValueKind.ValueType;
                case TypeFlags.Enum:
                    return GetStackValueKind(type.UnderlyingType);
                case TypeFlags.Class:
                case TypeFlags.Interface:
                case TypeFlags.Array:
                    return StackValueKind.ObjRef;
                case TypeFlags.ByRef:
                    return StackValueKind.ByRef;
                case TypeFlags.Pointer:
                    return StackValueKind.NativeInt;
                default:
                    return StackValueKind.Unknown;
            }
        }

        private void Push(StackValue value)
        {
            if (_stackTop >= _stack.Length)
                Array.Resize(ref _stack, 2 * _stackTop + 3);
            _stack[_stackTop++] = value;
        }

        private void Push(StackValueKind kind, Value value, TypeDesc type = null)
        {
            Push(new StackValue() { Kind = kind, Type = type, Value = value });
        }

        private StackValue Pop()
        {
            return _stack[--_stackTop];
        }

        private bool _msvc;

        private string GetStackValueKindCPPTypeName(StackValueKind kind, TypeDesc type = null)
        {
            switch (kind)
            {
                case StackValueKind.Int32: return "int32_t";
                case StackValueKind.Int64: return "int64_t";
                case StackValueKind.NativeInt: return "intptr_t";
                case StackValueKind.ObjRef: return "void*";
                case StackValueKind.Float: return "double";
                case StackValueKind.ByRef:
                case StackValueKind.ValueType: return _writer.GetCppSignatureTypeName(type);
                default: throw new NotSupportedException();
            }
        }

        private int _currentTemp = 1;
        private string NewTempName()
        {
            return "_" + (_currentTemp++).ToStringInvariant();
        }

        private void PushTemp(StackValueKind kind, TypeDesc type = null)
        {
            string temp = NewTempName();

            Push(kind, new Value(temp), type);

            // Start declaration on a new line
            AppendLine();
            Append(GetStackValueKindCPPTypeName(kind, type));
            Append(" ");
            Append(temp);
            Append(" = ");
        }

        private void AppendCastIfNecessary(TypeDesc destType, StackValueKind srcType)
        {
            if (destType.IsValueType)
                return;
            Append("(");
            Append(_writer.GetCppSignatureTypeName(destType));
            Append(")");
        }

        private void AppendCastIfNecessary(StackValueKind dstType, TypeDesc srcType)
        {
            if (dstType == StackValueKind.ByRef)
            {
                Append("(");
                Append(_writer.GetCppSignatureTypeName(srcType));
                Append(")");
            }
            else
            if (srcType.IsPointer)
            {
                Append("(intptr_t)");
            }
        }

        private Value NewSpillSlot(StackValueKind kind, TypeDesc type)
        {
            if (_spillSlots == null)
                _spillSlots = new List<SpillSlot>();

            SpillSlot spillSlot = new SpillSlot();
            spillSlot.Kind = kind;
            spillSlot.Type = type;
            spillSlot.Name = "_s" + _spillSlots.Count.ToStringInvariant();

            _spillSlots.Add(spillSlot);

            return new Value() { Name = spillSlot.Name };
        }

        /// <summary>
        /// If no sequence points are available, append an empty new line into 
        /// <see cref="_builder"/> and the required number of tabs. Otherwise does nothing.
        /// </summary>
        private void AppendLine()
        {
            if (_sequencePoints != null)
                return;
            _builder.AppendLine();
        }

        /// <summary>
        /// If no sequence points are available, append an empty new line into
        /// <see cref="_builder"/> without emitting any indentation. Otherwise does nothing.
        /// Useful to just skip a line.
        /// </summary>
        private void AppendEmptyLine()
        {
            if (_sequencePoints != null)
                return;
            _builder.AppendEmptyLine();
        }

        /// <summary>
        /// Append an empty new line into <see cref="_builder"/> without emitting any indentation.
        /// Useful to just skip a line.
        /// </summary>
        private void ForceAppendEmptyLine()
        {
            _builder.AppendEmptyLine();
        }

        /// <summary>
        /// Append string <param name="s"/> to <see cref="_builder"/>.
        /// </summary>
        /// <param name="s">String value to print.</param>
        private void Append(string s)
        {
            _builder.Append(s);
        }

        /// <summary>
        /// Append a semicolon to <see cref="_builder"/>.
        /// </summary>
        private void AppendSemicolon()
        {
            _builder.Append(";");
        }

        /// <summary>
        /// Increase level of indentation by one in <see cref="_builder"/>.
        /// </summary>
        public void Indent()
        {
            _builder.Indent();
        }

        /// <summary>
        /// Decrease level of indentation by one in <see cref="_builder"/>.
        /// </summary>
        private void Exdent()
        {
            _builder.Exdent();   
        }

        private string GetVarName(int index, bool argument)
        {
            if (_localSlotToInfoMap != null && !argument && _localSlotToInfoMap.ContainsKey(index) && !_localSlotToInfoMap[index].CompilerGenerated)
            {
                return _localSlotToInfoMap[index].Name;
            }

            if (_parameterIndexToNameMap != null && argument && _parameterIndexToNameMap.ContainsKey(index))
            {
                return _writer.SanitizeCppVarName(_parameterIndexToNameMap[index]);
            }

            return (argument ? "_a" : "_l") + index.ToStringInvariant();
        }

        private TypeDesc GetVarType(int index, bool argument)
        {
            if (argument)
            {
                if (_thisType != null)
                    index--;
                if (index == -1)
                {
                    if (_thisType.IsValueType)
                        return _thisType.MakeByRefType();
                    return _thisType;
                }
                else return _methodSignature[index];
            }
            else
            {
                return _locals[index].Type;
            }
        }

        private TypeDesc GetWellKnownType(WellKnownType wellKnownType)
        {
            return _typeSystemContext.GetWellKnownType(wellKnownType);
        }

        private TypeDesc ResolveTypeToken(int token)
        {
            return (TypeDesc)_methodIL.GetObject(token);
        }

        private void MarkInstructionBoundary()
        {
        }

        private void StartImportingInstruction()
        {
            if (_sequencePoints == null)
                return;

            var sequencePoint = _sequencePoints[_currentOffset];
            if (sequencePoint.Document == null)
                return;

            ForceAppendEmptyLine();
            Append("#line ");
            Append(sequencePoint.LineNumber.ToStringInvariant());
            Append(" \"");
            Append(sequencePoint.Document.Replace("\\", "\\\\"));
            Append("\"");
            ForceAppendEmptyLine();
        }

        private void EndImportingInstruction()
        {
            // Nothing to do, formatting is properly done.
        }

        public void Compile(CppMethodCodeNode methodCodeNodeNeedingCode)
        {
            FindBasicBlocks();

            ImportBasicBlocks();

            if (_sequencePoints != null && _sequencePoints[0].Document != null)
            {
                var sequencePoint = _sequencePoints[0];

                ForceAppendEmptyLine();
                Append("#line ");
                Append(sequencePoint.LineNumber.ToStringInvariant());
                Append(" \"");
                Append(sequencePoint.Document.Replace("\\", "\\\\"));
                Append("\"");
            }

            ForceAppendEmptyLine();
            Append(_writer.GetCppMethodDeclaration(_method, true));
            AppendLine();
            Append("{");
            Indent();

            bool initLocals = _methodIL.GetInitLocals();
            for (int i = 0; i < _locals.Length; i++)
            {
                AppendLine();
                Append(_writer.GetCppSignatureTypeName(_locals[i].Type));
                Append(" ");
                Append(GetVarName(i, false));
                if (initLocals)
                {
                    TypeDesc localType = _locals[i].Type;
                    if (localType.IsValueType && !localType.IsPrimitive && !localType.IsEnum)
                    {
                        AppendSemicolon();
                        AppendLine();
                        Append("memset(&");
                        Append(GetVarName(i, false));
                        Append(",0,sizeof(");
                        Append(_writer.GetCppSignatureTypeName(localType));
                        Append("))");
                    }
                    else
                    {
                        Append(" = 0");
                    }
                }
                AppendSemicolon();
            }

            if (_spillSlots != null)
            {
                for (int i = 0; i < _spillSlots.Count; i++)
                {
                    SpillSlot spillSlot = _spillSlots[i];
                    AppendLine();
                    Append(GetStackValueKindCPPTypeName(spillSlot.Kind, spillSlot.Type));
                    Append(" ");
                    Append(spillSlot.Name);
                    AppendSemicolon();
                }
            }

            for (int i = 0; i < _exceptionRegions.Length; i++)
            {
                var r = _exceptionRegions[i];
                if (r.ReturnLabels != 0)
                {
                    AppendLine();
                    Append("int __finallyReturn");
                    Append(i.ToStringInvariant());
                    Append(" = 0");
                    AppendSemicolon();
                }
            }

            // Temporary the indentation while printing blocks.
            // We want block to start on the first character of the line
            Exdent();
            for (int i = 0; i < _basicBlocks.Length; i++)
            {
                BasicBlock basicBlock = _basicBlocks[i];
                if (basicBlock != null)
                {
                    AppendEmptyLine();
                    AppendLine();
                    Append("_bb");
                    Append(i.ToStringInvariant());
                    Append(": {");
                    ForceAppendEmptyLine();
                    Append(basicBlock.Code);
                    AppendLine();
                    Append("}");
                }
            }

            for (int i = 0; i < _exceptionRegions.Length; i++)
            {
                var r = _exceptionRegions[i];
                if (r.ReturnLabels != 0)
                {
                    AppendEmptyLine();
                    AppendLine();
                    Append("__endFinally" + i.ToStringInvariant() + ":");
                    Indent();
                    AppendLine();
                    Append("switch(__finallyReturn" + i.ToStringInvariant() + ") {");
                    Indent();
                    for (int j = 1; j <= r.ReturnLabels; j++)
                    {
                        AppendLine();
                        Append("case " + j.ToStringInvariant() + ": goto __returnFromFinally" + i.ToStringInvariant() +
                                            "_" + j.ToStringInvariant() + ";");
                    }
                    AppendLine();
                    Append("default: " + (_msvc ? "__assume(0)" : "__builtin_unreachable()") + ";");
                    Exdent();
                    AppendLine();
                    Append("}");
                    Exdent();
                }
            }

            AppendEmptyLine();
            Append("}");

            methodCodeNodeNeedingCode.SetCode(_builder.ToString(), _dependencies.ToArray());
        }

        private void StartImportingBasicBlock(BasicBlock basicBlock)
        {
            Indent();
        }

        private void EndImportingBasicBlock(BasicBlock basicBlock)
        {
            Exdent();
            basicBlock.Code = _builder.ToString();
            _builder.Clear();
        }

        private void ImportNop()
        {
        }

        private void ImportBreak()
        {
            throw new NotImplementedException("Opcode: break");
        }

        private void ImportLoadVar(int index, bool argument)
        {
            string name = GetVarName(index, argument);
            string temp = NewTempName();

            TypeDesc type = GetVarType(index, argument);
            StackValueKind kind = GetStackValueKind(type);

            PushTemp(kind, type);
            AppendCastIfNecessary(kind, type);
            Append(name);
            AppendSemicolon();
        }

        private void ImportStoreVar(int index, bool argument)
        {
            var value = Pop();

            string name = GetVarName(index, argument);

            AppendLine();
            Append(name);
            Append(" = ");
            TypeDesc type = GetVarType(index, argument);
            AppendCastIfNecessary(type, value.Kind);
            Append(value.Value.Name);
            AppendSemicolon();
        }

        private void ImportAddressOfVar(int index, bool argument)
        {
            string name = GetVarName(index, argument);
            string temp = NewTempName();

            TypeDesc type = GetVarType(index, argument);
            type = type.MakeByRefType();

            PushTemp(StackValueKind.ByRef, type);
            AppendCastIfNecessary(StackValueKind.ByRef, type);
            Append("&");
            Append(name);
            AppendSemicolon();
        }

        private void ImportDup()
        {
            Push(_stack[_stackTop - 1]);
        }

        private void ImportPop()
        {
            Pop();
        }

        private void ImportJmp(int token)
        {
            throw new NotImplementedException("Opcode: jmp");
        }

        private void ImportCasting(ILOpcode opcode, int token)
        {
            TypeDesc type = (TypeDesc)_methodIL.GetObject(token);

            var value = Pop();
            PushTemp(StackValueKind.ObjRef, type);

            AddTypeReference(type, false);

            Append(opcode == ILOpcode.isinst ? "__isinst" : "__castclass");
            Append("(");
            Append(value.Value.Name);
            Append(", ");
            Append(_writer.GetCppTypeName(type));
            Append("::__getMethodTable())");
            AppendSemicolon();
        }

        private static bool IsTypeName(MethodDesc method, string typeNamespace, string typeName)
        {
            var metadataType = method.OwningType as MetadataType;
            if (metadataType == null)
                return false;
            return metadataType.Namespace == typeNamespace && metadataType.Name == typeName;
        }

        private bool ImportIntrinsicCall(MethodDesc method)
        {
            Debug.Assert(method.IsIntrinsic);

            switch (method.Name)
            {
                case "InitializeArray":
                    if (IsTypeName(method, "System.Runtime.CompilerServices", "RuntimeHelpers"))
                    {
                        var fieldSlot = Pop();
                        var arraySlot = Pop();

                        var fieldDesc = (TypeSystem.Ecma.EcmaField)fieldSlot.Value.Aux;
                        var memBlock = TypeSystem.Ecma.EcmaFieldExtensions.GetFieldRvaData(fieldDesc);

                        // TODO: Need to do more for arches with different endianness?
                        var preinitDataHolder = NewTempName();
                        AppendLine();
                        Append("static const unsigned char ");
                        Append(preinitDataHolder);
                        Append("[] = { ");

                        // Format arrays to have 16 entries per line or less.
                        if (memBlock.Length > 16)
                        {
                            Indent();
                            AppendLine();
                        }
                        for (int i = 0; i < memBlock.Length; i++)
                        {
                            if (i != 0)
                            {
                                Append(", ");
                                if ((i % 16) == 0)
                                    AppendLine();
                            }
                            Append("0x");
                            Append(memBlock[i].ToStringInvariant("x2"));
                        }
                        if (memBlock.Length > 16)
                        {
                            Exdent();
                            AppendLine();
                        }
                        Append("}");
                        AppendSemicolon();

                        AppendLine();
                        Append("memcpy((char*)");
                        Append(arraySlot.Value.Name);
                        Append(" + ARRAY_BASE, ");
                        Append(preinitDataHolder);
                        Append(", ");
                        Append(memBlock.Length.ToStringInvariant());
                        Append(")");

                        AppendSemicolon();
                        return true;
                    }
                    break;
                case "GetValueInternal":
                    if (IsTypeName(method, "System", "RuntimeTypeHandle"))
                    {
                        StackValue typeHandleSlot = Pop();
                        TypeDesc typeOfEEType = (TypeDesc)typeHandleSlot.Value.Aux;
                        Push(StackValueKind.NativeInt, new Value(
                            String.Concat("((intptr_t)", _writer.GetCppTypeName(typeOfEEType), "::__getMethodTable())")));
                        return true;
                    }
                    break;
                default:
                    break;
            }
            return false;
        }

        private void ImportCall(ILOpcode opcode, int token)
        {
            bool callViaSlot = false;
            bool delegateInvoke = false;
            DelegateInfo delegateInfo = null;
            bool mdArrayCreate = false;

            MethodDesc method = (MethodDesc)_methodIL.GetObject(token);

            if (method.IsIntrinsic)
            {
                if (ImportIntrinsicCall(method))
                    return;
            }

            TypeDesc constrained = null;
            if (opcode != ILOpcode.newobj)
            {
                if ((_pendingPrefix & Prefix.Constrained) != 0 && opcode == ILOpcode.callvirt)
                {
                    _pendingPrefix &= ~Prefix.Constrained;
                    constrained = _constrained;

                    bool forceUseRuntimeLookup;
                    MethodDesc directMethod = constrained.TryResolveConstraintMethodApprox(method.OwningType, method, out forceUseRuntimeLookup);
                    if (directMethod == null || forceUseRuntimeLookup)
                        throw new NotImplementedException();

                    method = directMethod;
                    opcode = ILOpcode.call;
                }
            }

            TypeDesc owningType = method.OwningType;

            TypeDesc retType = null;

            {
                if (opcode == ILOpcode.newobj)
                    retType = owningType;

                if (opcode == ILOpcode.newobj)
                {
                    if (owningType.IsString)
                    {
                        // String constructors actually look like regular method calls
                        method = method.GetStringInitializer();
                        opcode = ILOpcode.call;

                        // WORKAROUND: the static method expects an extra arg
                        // Annoyingly, it needs to be before all the others
                        if (_stackTop >= _stack.Length)
                            Array.Resize(ref _stack, 2 * _stackTop + 3);
                        for (int i = _stackTop - 1; i > _stackTop - method.Signature.Length; i--)
                            _stack[i + 1] = _stack[i];
                        _stackTop++;
                        _stack[_stackTop - method.Signature.Length] =
                            new StackValue { Kind = StackValueKind.ObjRef, Value = new Value("0") };
                    }
                    else if (owningType.IsArray)
                    {
                        mdArrayCreate = true;
                    }
                    else if (owningType.IsDelegate)
                    {
                        delegateInfo = _compilation.GetDelegateCtor((MethodDesc)_stack[_stackTop - 1].Value.Aux);
                        method = delegateInfo.Ctor;
                    }
                }
                else
                if (owningType.IsDelegate)
                {
                    if (method.Name == "Invoke")
                    {
                        opcode = ILOpcode.call;
                        delegateInvoke = true;
                    }
                }
            }

            if (opcode == ILOpcode.callvirt)
            {
                // TODO: Null checks

                if (method.IsVirtual)
                {
                    // TODO: Full resolution of virtual methods
                    if (!method.IsNewSlot)
                        throw new NotImplementedException();

                    // TODO: Interface calls
                    if (method.OwningType.IsInterface)
                        throw new NotImplementedException();

                    _dependencies.Add(_nodeFactory.VirtualMethodUse(new ResolvedVirtualMethod(method)));

                    callViaSlot = true;
                }
            }

            if (!callViaSlot && !delegateInvoke && !mdArrayCreate)
                AddMethodReference(method);

            if (opcode == ILOpcode.newobj)
                AddTypeReference(retType, true);

            var methodSignature = method.Signature;

            if (retType == null)
                retType = methodSignature.ReturnType;

            string temp = null;
            StackValueKind retKind = StackValueKind.Unknown;
            var needNewLine = false;

            if (!retType.IsVoid)
            {
                retKind = GetStackValueKind(retType);
                temp = NewTempName();

                AppendLine();
                Append(GetStackValueKindCPPTypeName(retKind, retType));
                Append(" ");
                Append(temp);
                if (retType.IsValueType && opcode == ILOpcode.newobj)
                {
                    Append(";");
                    needNewLine = true;
                }
                else
                {
                    Append(" = ");

                    if (retType.IsPointer)
                    {
                        Append("(intptr_t)");
                    }
                }
            }
            else
            {
                needNewLine = true;
            }

            if (opcode == ILOpcode.newobj && !mdArrayCreate)
            {
                if (!retType.IsValueType)
                {
                    // We do not reset needNewLine since we still need for the next statement.
                    if (needNewLine)
                        AppendLine();
                    Append("__allocate_object(");
                    Append(_writer.GetCppTypeName(retType));
                    Append("::__getMethodTable())");
                    AppendSemicolon();
                    needNewLine = true;

                    if (delegateInfo != null && delegateInfo.ShuffleThunk != null)
                    {
                        AddMethodReference(delegateInfo.ShuffleThunk);

                        _stack[_stackTop - 2].Value.Name = temp;

                        var sb = new CppGenerationBuffer();
                        AppendLine();
                        sb.Append("(intptr_t)&");
                        sb.Append(_writer.GetCppTypeName(delegateInfo.ShuffleThunk.OwningType));
                        sb.Append("::");
                        sb.Append(_writer.GetCppMethodName(delegateInfo.ShuffleThunk));

                        Push(StackValueKind.NativeInt, new Value(sb.ToString()), null);
                    }
                }
            }

            if (needNewLine)
                AppendLine();

            if (callViaSlot || delegateInvoke)
            {
                Append("(*");
                Append(_writer.GetCppTypeName(method.OwningType));
                Append("::");
                Append(delegateInvoke ? "__invoke__" : "__getslot__");
                Append(_writer.GetCppMethodName(method));
                Append("(");
                Append(_stack[_stackTop - (methodSignature.Length + 1)].Value.Name);
                Append("))");

                if (delegateInvoke)
                {
                    _stack[_stackTop - (methodSignature.Length + 1)].Value.Name =
                        "((" + _writer.GetCppSignatureTypeName(GetWellKnownType(WellKnownType.MulticastDelegate)) + ")" +
                            _stack[_stackTop - (methodSignature.Length + 1)].Value.Name + ")->m_firstParameter";
                }
            }
            else if (mdArrayCreate)
            {
                Append("RhNewMDArray");
            }
            else
            {
                Append(_writer.GetCppTypeName(method.OwningType));
                Append("::");
                Append(_writer.GetCppMethodName(method));
            }

            TypeDesc thisArgument = null;
            Append("(");
            if (mdArrayCreate)
            {
                Append(_writer.GetCppTypeName(method.OwningType));
                Append("::__getMethodTable(), ");
                Append(((ArrayType)method.OwningType).Rank.ToStringInvariant());
                Append(", ");
            }
            else if (opcode == ILOpcode.newobj)
            {
                Append("(");
                if (retType.IsValueType)
                {
                    Append(_writer.GetCppSignatureTypeName(retType.MakeByRefType()));
                    Append(")");
                    Append("&" + temp);
                }
                else
                {
                    Append(_writer.GetCppSignatureTypeName(retType));
                    Append(")");
                    Append(temp);
                }
                if (methodSignature.Length > 0)
                    Append(", ");
            }
            else
            {
                if (!methodSignature.IsStatic)
                {
                    thisArgument = owningType;
                    if (thisArgument.IsValueType)
                        thisArgument = thisArgument.MakeByRefType();
                }
            }
            PassCallArguments(methodSignature, thisArgument);
            Append(")");

            if (temp != null)
                Push(retKind, new Value(temp), retType);
            AppendSemicolon();
        }

        private void PassCallArguments(MethodSignature methodSignature, TypeDesc thisArgument)
        {
            int signatureLength = methodSignature.Length;
            int argumentsCount = (thisArgument != null) ? (signatureLength + 1) : signatureLength;
            for (int i = 0; i < argumentsCount; i++)
            {
                var op = _stack[_stackTop - argumentsCount + i];
                int argIndex = signatureLength - (argumentsCount - i);
                TypeDesc argType;
                if (argIndex == -1)
                {
                    argType = thisArgument;
                }
                else
                {
                    argType = methodSignature[argIndex];
                }
                AppendCastIfNecessary(argType, op.Kind);
                Append(op.Value.Name);
                if (i + 1 != argumentsCount)
                    Append(", ");
            }
            _stackTop -= argumentsCount;
        }

        private void ImportCalli(int token)
        {
            MethodSignature methodSignature = (MethodSignature)_methodIL.GetObject(token);

            TypeDesc thisArgument = null;
            if (!methodSignature.IsStatic)
            {
                thisArgument = GetWellKnownType(WellKnownType.Object);
                if (thisArgument.IsValueType)
                    thisArgument = thisArgument.MakeByRefType();
            }

            string typeDefName = "__calli__" + token.ToStringInvariant("x8");
            _writer.AppendSignatureTypeDef(_builder, typeDefName, methodSignature, thisArgument);

            TypeDesc retType = methodSignature.ReturnType;
            StackValueKind retKind = StackValueKind.Unknown;

            string temp = null;

            if (!retType.IsVoid)
            {
                retKind = GetStackValueKind(retType);
                temp = NewTempName();

                AppendLine();
                Append(GetStackValueKindCPPTypeName(retKind, retType));
                Append(" ");
                Append(temp);
                Append(" = ");

                if (retType.IsPointer)
                {
                    Append("(intptr_t)");
                }
            }
            else
            {
                AppendLine();
            }

            var fnPtrValue = Pop();
            Append("((");
            Append(typeDefName);
            Append(")");
            Append(fnPtrValue.Value.Name);
            Append(")(");
            PassCallArguments(methodSignature, thisArgument);
            Append(")");

            if (temp != null)
                Push(retKind, new Value(temp), retType);

            AppendSemicolon();
        }

        private void ImportLdFtn(int token, ILOpcode opCode)
        {
            MethodDesc method = (MethodDesc)_methodIL.GetObject(token);

            if (opCode == ILOpcode.ldvirtftn)
            {
                if (method.IsVirtual)
                    throw new NotImplementedException();
            }

            AddMethodReference(method);

            PushTemp(StackValueKind.NativeInt);
            Append("(intptr_t)&");
            Append(_writer.GetCppTypeName(method.OwningType));
            Append("::");
            Append(_writer.GetCppMethodName(method));

            _stack[_stackTop - 1].Value.Aux = method;

            AppendSemicolon();
        }

        private void ImportLoadInt(long value, StackValueKind kind)
        {
            string val;
            if (kind == StackValueKind.Int64)
            {
                if (value == Int64.MinValue)
                    val = "(int64_t)(0x8000000000000000" + (_msvc ? "i64" : "LL") + ")";
                else
                    val = value.ToStringInvariant() + (_msvc ? "i64" : "LL");
            }
            else
            {
                if (value == Int32.MinValue)
                    val = "(int32_t)(0x80000000)";
                else
                    val = ((int)value).ToStringInvariant();
            }

            Push(kind, new Value(val));
        }

        private void ImportLoadFloat(double value)
        {
            // TODO: Handle infinity, NaN, etc.
            if (Double.IsNaN(value) || Double.IsInfinity(value) || Double.IsPositiveInfinity(value) || Double.IsNegativeInfinity(value))
                throw new NotImplementedException();

            string val = value.ToStringInvariant();
            Push(StackValueKind.Float, new Value(val));
        }

        private void ImportLoadNull()
        {
            Push(StackValueKind.ObjRef, new Value("0"));
        }

        private void ImportReturn()
        {
            var returnType = _methodSignature.ReturnType;
            AppendLine();
            if (returnType.IsVoid)
            {
                Append("return");
            }
            else
            {
                var value = Pop();
                Append("return ");
                AppendCastIfNecessary(returnType, value.Kind);
                Append(value.Value.Name);
            }
            AppendSemicolon();
        }

        private void ImportFallthrough(BasicBlock next)
        {
            StackValue[] entryStack = next.EntryStack;

            if (entryStack != null)
            {
                if (entryStack.Length != _stackTop)
                    throw new InvalidProgramException();

                for (int i = 0; i < entryStack.Length; i++)
                {
                    // TODO: Do we need to allow conversions?
                    if (entryStack[i].Kind != _stack[i].Kind)
                        throw new InvalidProgramException();

                    if (entryStack[i].Kind == StackValueKind.ValueType)
                    {
                        if (entryStack[i].Type != _stack[i].Type)
                            throw new InvalidProgramException();
                    }
                }
            }
            else
            {
                entryStack = (_stackTop != 0) ? new StackValue[_stackTop] : s_emptyStack;

                for (int i = 0; i < entryStack.Length; i++)
                {
                    StackValue spilledValue = _stack[i];
                    spilledValue.Value = NewSpillSlot(spilledValue.Kind, spilledValue.Type);
                    entryStack[i] = spilledValue;
                }

                next.EntryStack = entryStack;
            }

            for (int i = 0; i < entryStack.Length; i++)
            {
                AppendLine();
                Append(entryStack[i].Value.Name);
                Append(" = ");
                Append(_stack[i].Value.Name);
                AppendSemicolon();
            }

            MarkBasicBlock(next);
        }

        private void ImportSwitchJump(int jmpBase, int[] jmpDelta, BasicBlock fallthrough)
        {
            var op = Pop();

            AppendLine();
            Append("switch (");
            Append(op.Value.Name);
            Append(") {");
            Indent();


            for (int i = 0; i < jmpDelta.Length; i++)
            {
                BasicBlock target = _basicBlocks[jmpBase + jmpDelta[i]];
                AppendLine();
                Append("case " + i + ": ");
                Indent();
                ImportFallthrough(target);
                AppendLine();
                Append("goto _bb");
                Append(target.StartOffset.ToStringInvariant());
                AppendSemicolon();
                AppendLine();
                Append("break; ");
                Exdent();
            }
            Exdent();
            AppendLine();
            Append("}");

            if (fallthrough != null)
                ImportFallthrough(fallthrough);
        }

        private void ImportBranch(ILOpcode opcode, BasicBlock target, BasicBlock fallthrough)
        {
            AppendLine();
            if (opcode != ILOpcode.br)
            {
                Append("if (");
                if (opcode == ILOpcode.brfalse || opcode == ILOpcode.brtrue)
                {
                    var op = Pop();
                    Append(op.Value.Name);
                    Append((opcode == ILOpcode.brtrue) ? " != 0" : " == 0");
                }
                else
                {
                    var op1 = Pop();
                    var op2 = Pop();

                    // StackValueKind is carefully ordered to make this work (assuming the IL is valid)
                    StackValueKind kind;

                    if (op1.Kind > op2.Kind)
                    {
                        kind = op1.Kind;
                    }
                    else
                    {
                        kind = op2.Kind;
                    }

                    string op = null;
                    bool unsigned = false;
                    bool inverted = false;
                    switch (opcode)
                    {
                        case ILOpcode.beq: op = "=="; break;
                        case ILOpcode.bge: op = ">="; break;
                        case ILOpcode.bgt: op = ">"; break;
                        case ILOpcode.ble: op = "<="; break;
                        case ILOpcode.blt: op = "<"; break;
                        case ILOpcode.bne_un: op = "!="; break;
                        case ILOpcode.bge_un:
                            if (kind == StackValueKind.Float)
                            {
                                op = "<"; inverted = true;
                            }
                            else
                            {
                                op = ">=";
                            }
                            if (kind == StackValueKind.Int32 || kind == StackValueKind.Int64)
                                unsigned = true;
                            break;
                        case ILOpcode.bgt_un:
                            if (kind == StackValueKind.Float)
                            {
                                op = "<="; inverted = true;
                            }
                            else
                            {
                                op = ">";
                            }
                            if (kind == StackValueKind.Int32 || kind == StackValueKind.Int64)
                                unsigned = true;
                            break;
                        case ILOpcode.ble_un:
                            if (kind == StackValueKind.Float)
                            {
                                op = ">"; inverted = true;
                            }
                            else
                            {
                                op = "<=";
                            }
                            if (kind == StackValueKind.Int32 || kind == StackValueKind.Int64)
                                unsigned = true;
                            break;
                        case ILOpcode.blt_un:
                            if (kind == StackValueKind.Float)
                            {
                                op = ">="; inverted = true;
                            }
                            else
                            {
                                op = "<";
                            }
                            if (kind == StackValueKind.Int32 || kind == StackValueKind.Int64)
                                unsigned = true;
                            break;
                    }

                    if (kind == StackValueKind.ByRef)
                        unsigned = false;

                    if (inverted)
                    {
                        Append("!(");
                    }
                    if (unsigned)
                    {
                        Append("(u");
                        Append(GetStackValueKindCPPTypeName(kind));
                        Append(")");
                    }
                    Append(op2.Value.Name);
                    Append(" ");
                    Append(op);
                    Append(" ");
                    if (unsigned)
                    {
                        Append("(u");
                        Append(GetStackValueKindCPPTypeName(kind));
                        Append(")");
                    }
                    Append(op1.Value.Name);
                    if (inverted)
                    {
                        Append(")");
                    }
                }
                Append(") ");
            }
            Append("{");
            Indent();
            ImportFallthrough(target);
            AppendLine();
            Append("goto _bb");
            Append(target.StartOffset.ToStringInvariant());
            AppendSemicolon();
            Exdent();
            AppendLine();
            Append("}");

            if (fallthrough != null)
                ImportFallthrough(fallthrough);
        }

        private void ImportBinaryOperation(ILOpcode opcode)
        {
            var op1 = Pop();
            var op2 = Pop();

            // StackValueKind is carefully ordered to make this work (assuming the IL is valid)
            StackValueKind kind;
            TypeDesc type;

            if (op1.Kind > op2.Kind)
            {
                kind = op1.Kind;
                type = op1.Type;
            }
            else
            {
                kind = op2.Kind;
                type = op2.Type;
            }

            // The one exception from the above rule
            if ((kind == StackValueKind.ByRef) &&
                    (opcode == ILOpcode.sub || opcode == ILOpcode.sub_ovf || opcode == ILOpcode.sub_ovf_un))
            {
                kind = StackValueKind.NativeInt;
                type = null;
            }

            PushTemp(kind, type);

            string op = null;
            bool unsigned = false;
            switch (opcode)
            {
                case ILOpcode.add: op = "+"; break;
                case ILOpcode.sub: op = "-"; break;
                case ILOpcode.mul: op = "*"; break;
                case ILOpcode.div: op = "/"; break;
                case ILOpcode.div_un: op = "/"; unsigned = true; break;
                case ILOpcode.rem: op = "%"; break;
                case ILOpcode.rem_un: op = "%"; unsigned = true; break;
                case ILOpcode.and: op = "&"; break;
                case ILOpcode.or: op = "|"; break;
                case ILOpcode.xor: op = "^"; break;

                // TODO: Overflow checks
                case ILOpcode.add_ovf: op = "+"; break;
                case ILOpcode.add_ovf_un: op = "+"; unsigned = true; break;
                case ILOpcode.sub_ovf: op = "-"; break;
                case ILOpcode.sub_ovf_un: op = "-"; unsigned = true; break;
                case ILOpcode.mul_ovf: op = "*"; break;
                case ILOpcode.mul_ovf_un: op = "*"; unsigned = true; break;

                default: Debug.Assert(false, "Unexpected opcode"); break;
            }

            if (unsigned)
            {
                Append("(u");
                Append(GetStackValueKindCPPTypeName(kind));
                Append(")");
            }
            Append(op2.Value.Name);
            Append(" ");
            Append(op);
            Append(" ");
            if (unsigned)
            {
                Append("(u");
                Append(GetStackValueKindCPPTypeName(kind));
                Append(")");
            }
            Append(op1.Value.Name);

            AppendSemicolon();
        }

        private void ImportShiftOperation(ILOpcode opcode)
        {
            var shiftAmount = Pop();
            var op = Pop();

            PushTemp(op.Kind, op.Type);

            if (opcode == ILOpcode.shr_un)
            {
                Append("(u");
                Append(GetStackValueKindCPPTypeName(op.Kind));
                Append(")");
            }
            Append(op.Value.Name);

            Append((opcode == ILOpcode.shl) ? " << " : " >> ");

            Append(shiftAmount.Value.Name);
            AppendSemicolon();
        }

        private void ImportCompareOperation(ILOpcode opcode)
        {
            var op1 = Pop();
            var op2 = Pop();

            // StackValueKind is carefully ordered to make this work (assuming the IL is valid)
            StackValueKind kind;

            if (op1.Kind > op2.Kind)
            {
                kind = op1.Kind;
            }
            else
            {
                kind = op2.Kind;
            }

            PushTemp(StackValueKind.Int32);

            string op = null;
            bool unsigned = false;
            bool inverted = false;
            switch (opcode)
            {
                case ILOpcode.ceq: op = "=="; break;
                case ILOpcode.cgt: op = ">"; break;
                case ILOpcode.clt: op = "<"; break;
                case ILOpcode.cgt_un:
                    if (kind == StackValueKind.Float)
                    {
                        op = "<="; inverted = true;
                    }
                    else
                    {
                        op = ">";
                        if (kind == StackValueKind.Int32 || kind == StackValueKind.Int64)
                            unsigned = true;
                    }
                    break;
                case ILOpcode.clt_un:
                    if (kind == StackValueKind.Float)
                    {
                        op = ">="; inverted = true;
                    }
                    else
                    {
                        op = "<";
                        if (kind == StackValueKind.Int32 || kind == StackValueKind.Int64)
                            unsigned = true;
                    }
                    break;
            }

            if (kind == StackValueKind.ByRef)
                unsigned = false;

            if (inverted)
            {
                Append("!(");
            }
            if (unsigned)
            {
                Append("(u");
                Append(GetStackValueKindCPPTypeName(kind));
                Append(")");
            }
            Append(op2.Value.Name);
            Append(" ");
            Append(op);
            Append(" ");
            if (unsigned)
            {
                Append("(u");
                Append(GetStackValueKindCPPTypeName(kind));
                Append(")");
            }
            Append(op1.Value.Name);
            if (inverted)
            {
                Append(")");
            }
            AppendSemicolon();
        }

        private void ImportConvert(WellKnownType wellKnownType, bool checkOverflow, bool unsigned)
        {
            var op = Pop();

            TypeDesc type = GetWellKnownType(wellKnownType);

            PushTemp(GetStackValueKind(type));
            Append("(");
            Append(_writer.GetCppSignatureTypeName(type));
            Append(")");
            Append(op.Value.Name);
            AppendSemicolon();
        }

        private void ImportLoadField(int token, bool isStatic)
        {
            FieldDesc field = (FieldDesc)_methodIL.GetObject(token);

            AddFieldReference(field);

            var thisPtr = isStatic ? new StackValue() : Pop();

            TypeDesc owningType = field.OwningType;
            TypeDesc fieldType = field.FieldType;

            // TODO: Is this valid combination?
            if (!isStatic && !owningType.IsValueType && thisPtr.Kind != StackValueKind.ObjRef)
                throw new InvalidProgramException();

            if (field.IsStatic)
                TriggerCctor(field.OwningType);

            StackValueKind kind = GetStackValueKind(fieldType);
            PushTemp(kind, fieldType);
            AppendCastIfNecessary(kind, fieldType);

            if (field.IsStatic)
            {
                if (!fieldType.IsValueType)
                    Append("__gcStatics.");
                else
                    Append("__statics.");
                Append(_writer.GetCppStaticFieldName(field));
            }
            else
            if (thisPtr.Kind == StackValueKind.ValueType)
            {
                Append(thisPtr.Value.Name);
                Append(".");
                Append(_writer.GetCppFieldName(field));
            }
            else
            {
                Append("((");
                Append(_writer.GetCppTypeName(owningType));
                Append("*)");
                Append(thisPtr.Value.Name);
                Append(")->");
                Append(_writer.GetCppFieldName(field));

                // TODO: Remove
                _writer.GetCppSignatureTypeName(owningType);
            }

            AppendSemicolon();
        }

        private void ImportAddressOfField(int token, bool isStatic)
        {
            FieldDesc field = (FieldDesc)_methodIL.GetObject(token);

            AddFieldReference(field);

            var thisPtr = isStatic ? new StackValue() : Pop();

            TypeDesc owningType = field.OwningType;
            TypeDesc fieldType = field.FieldType;

            // TODO: Is this valid combination?
            if (!isStatic && !owningType.IsValueType && thisPtr.Kind != StackValueKind.ObjRef)
                throw new InvalidProgramException();

            if (field.IsStatic)
                TriggerCctor(field.OwningType);

            TypeDesc addressType = fieldType.MakeByRefType();
            StackValueKind kind = GetStackValueKind(addressType);
            PushTemp(kind, addressType);
            AppendCastIfNecessary(kind, addressType);

            Append("&");

            if (field.IsStatic)
            {
                if (!fieldType.IsValueType)
                    Append("__gcStatics.");
                else
                    Append("__statics.");
                Append(_writer.GetCppStaticFieldName(field));
            }
            else
            if (thisPtr.Kind == StackValueKind.ValueType)
            {
                throw new NotImplementedException();
            }
            else
            {
                Append("((");
                Append(_writer.GetCppTypeName(owningType));
                Append("*)");
                Append(thisPtr.Value.Name);
                Append(")->");
                Append(_writer.GetCppFieldName(field));

                // TODO: Remove
                _writer.GetCppSignatureTypeName(owningType);
            }

            AppendSemicolon();
        }


        private void ImportStoreField(int token, bool isStatic)
        {
            FieldDesc field = (FieldDesc)_methodIL.GetObject(token);

            AddFieldReference(field);

            var value = Pop();
            var thisPtr = isStatic ? new StackValue() : Pop();

            TypeDesc owningType = field.OwningType;
            TypeDesc fieldType = field.FieldType;

            // TODO: Is this valid combination?
            if (!isStatic && !owningType.IsValueType && thisPtr.Kind != StackValueKind.ObjRef)
                throw new InvalidProgramException();

            if (field.IsStatic)
                TriggerCctor(field.OwningType);

            // TODO: Write barrier as necessary!!!

            AppendLine();
            if (field.IsStatic)
            {
                if (!fieldType.IsValueType)
                    Append("__gcStatics.");
                else
                    Append("__statics.");
                Append(_writer.GetCppStaticFieldName(field));
            }
            else if (thisPtr.Kind == StackValueKind.ValueType)
            {
                throw new NotImplementedException();
            }
            else
            {
                Append("((");
                Append(_writer.GetCppTypeName(owningType));
                Append("*)");
                Append(thisPtr.Value.Name);
                Append(")->");
                Append(_writer.GetCppFieldName(field));

                // TODO: Remove
                _writer.GetCppSignatureTypeName(owningType);
            }
            Append(" = ");
            if (!fieldType.IsValueType)
            {
                Append("(");
                Append(_writer.GetCppSignatureTypeName(fieldType));
                Append(")");
            }
            Append(value.Value.Name);
            AppendSemicolon();
        }

        private void ImportLoadIndirect(int token)
        {
            ImportLoadIndirect(ResolveTypeToken(token));
        }

        private void ImportLoadIndirect(TypeDesc type)
        {
            if (type == null)
                type = GetWellKnownType(WellKnownType.Object);

            var addr = Pop();

            PushTemp(GetStackValueKind(type), type);

            Append("*(");
            Append(_writer.GetCppSignatureTypeName(type));
            Append("*)");
            Append(addr.Value.Name);

            AppendSemicolon();
        }

        private void ImportStoreIndirect(int token)
        {
            ImportStoreIndirect(ResolveTypeToken(token));
        }

        private void ImportStoreIndirect(TypeDesc type)
        {
            if (type == null)
                type = GetWellKnownType(WellKnownType.Object);

            var value = Pop();
            var addr = Pop();

            // TODO: Write barrier as necessary!!!

            AppendLine();
            Append("*(");
            Append(_writer.GetCppSignatureTypeName(type));
            Append("*)");
            Append(addr.Value.Name);
            Append(" = ");
            AppendCastIfNecessary(type, value.Kind);
            Append(value.Value.Name);
            AppendSemicolon();
        }

        private void ImportThrow()
        {
            var obj = Pop();

            AppendLine();
            Append("__throw_exception(");
            Append(obj.Value.Name);
            Append(")");
            AppendSemicolon();
        }

        private void ImportLoadString(int token)
        {
            string str = (string)_methodIL.GetObject(token);

            PushTemp(StackValueKind.ObjRef, GetWellKnownType(WellKnownType.String));

            var escaped = new CppGenerationBuffer();
            foreach (char c in str)
            {
                switch (c)
                {
                    case '\\':
                        escaped.Append("\\\\");
                        break;
                    case '\r':
                        escaped.Append("\\r");
                        break;
                    case '\n':
                        escaped.Append("\\n");
                        break;
                    case '\t':
                        escaped.Append("\\t");
                        break;
                    default:
                        // TODO: handle all characters < 32
                        escaped.Append(c);
                        break;
                }
            }

            Append("__load_string_literal(\"");
            Append(escaped.ToString());
            Append("\")");
            AppendSemicolon();
        }

        private void ImportInitObj(int token)
        {
            TypeDesc type = (TypeDesc)_methodIL.GetObject(token);

            var addr = Pop();
            AppendLine();
            Append("memset((void*)");
            Append(addr.Value.Name);
            Append(",0,sizeof(");
            Append(_writer.GetCppSignatureTypeName(type));
            Append("))");
            AppendSemicolon();
        }

        private void ImportBox(int token)
        {
            TypeDesc type = (TypeDesc)_methodIL.GetObject(token);

            if (type.IsValueType)
            {
                if (type.IsNullable)
                    throw new NotImplementedException();

                var value = Pop();

                PushTemp(StackValueKind.ObjRef, type);

                AddTypeReference(type, true);

                Append("__allocate_object(");
                Append(_writer.GetCppTypeName(type));
                Append("::__getMethodTable())");
                AppendSemicolon();

                string typeName = GetStackValueKindCPPTypeName(GetStackValueKind(type), type);

                // TODO: Write barrier as necessary
                AppendLine();
                Append("*(" + typeName + " *)((void **)" + _stack[_stackTop - 1].Value.Name + "+1) = " + value.Value.Name);
                AppendSemicolon();
            }
        }

        private static bool IsOffsetContained(int offset, int start, int length)
        {
            return start <= offset && offset < start + length;
        }

        private static string AddReturnLabel(ExceptionRegion r)
        {
            r.ReturnLabels++;
            return r.ReturnLabels.ToStringInvariant();
        }

        private void ImportLeave(BasicBlock target)
        {
            // Empty the stack
            _stackTop = 0;

            // Close the scope and open a new one so that we don't put a goto label in the middle
            // of a scope.
            Exdent();
            AppendLine();
            Append("}");
            AppendLine();
            Append("{");
            Indent();

            for (int i = 0; i < _exceptionRegions.Length; i++)
            {
                var r = _exceptionRegions[i];

                if (r.ILRegion.Kind == ILExceptionRegionKind.Finally &&
                    IsOffsetContained(_currentOffset - 1, r.ILRegion.TryOffset, r.ILRegion.TryLength) &&
                    !IsOffsetContained(target.StartOffset, r.ILRegion.TryOffset, r.ILRegion.TryLength))
                {
                    string returnLabel = AddReturnLabel(r);

                    AppendLine();
                    Append("__finallyReturn");
                    Append(i.ToStringInvariant());
                    Append(" = ");
                    Append(returnLabel);
                    AppendSemicolon();

                    AppendLine();
                    Append("goto _bb");
                    Append(r.ILRegion.HandlerOffset.ToStringInvariant());
                    AppendSemicolon();

                    AppendEmptyLine();
                    Append("__returnFromFinally");
                    Append(i.ToStringInvariant());
                    Append("_");
                    Append(returnLabel);
                    Append(":");
                    AppendSemicolon();

                    MarkBasicBlock(_basicBlocks[r.ILRegion.HandlerOffset]);
                }
            }

            AppendLine();
            Append("goto _bb");
            Append(target.StartOffset.ToStringInvariant());
            AppendSemicolon();

            MarkBasicBlock(target);
        }

        private int FindNearestFinally(int offset)
        {
            int candidate = -1;
            for (int i = 0; i < _exceptionRegions.Length; i++)
            {
                var r = _exceptionRegions[i];

                if (r.ILRegion.Kind == ILExceptionRegionKind.Finally &&
                    IsOffsetContained(offset - 1, r.ILRegion.HandlerOffset, r.ILRegion.HandlerLength))
                {
                    if (candidate == -1 ||
                        _exceptionRegions[candidate].ILRegion.HandlerOffset < _exceptionRegions[i].ILRegion.HandlerOffset)
                    {
                        candidate = i;
                    }
                }
            }
            return candidate;
        }

        private void ImportEndFinally()
        {
            int finallyIndex = FindNearestFinally(_currentOffset - 1);

            AppendLine();
            Append("goto __endFinally");
            Append(finallyIndex.ToStringInvariant());
            AppendSemicolon();
        }

        private void ImportNewArray(int token)
        {
            TypeDesc type = (TypeDesc)_methodIL.GetObject(token);
            TypeDesc arrayType = type.Context.GetArrayType(type);

            var numElements = Pop();

            PushTemp(StackValueKind.ObjRef, arrayType);

            AddTypeReference(arrayType, true);

            Append("__allocate_array(");
            Append(numElements.Value.Name);
            Append(", ");
            Append(_writer.GetCppTypeName(arrayType));
            Append("::__getMethodTable()");
            Append(")");
            AppendSemicolon();
        }

        private void ImportLoadElement(int token)
        {
            ImportLoadElement(ResolveTypeToken(token));
        }

        private void ImportLoadElement(TypeDesc elementType)
        {
            // ldelem_ref
            if (elementType == null)
                elementType = GetWellKnownType(WellKnownType.Object);

            var index = Pop();
            var arrayPtr = Pop();

            // Range check
            AppendLine();
            Append("__range_check(");
            Append(arrayPtr.Value.Name);
            Append(",");
            Append(index.Value.Name);
            Append(");");

            PushTemp(GetStackValueKind(elementType), elementType);

            Append("*(");
            Append(_writer.GetCppSignatureTypeName(elementType));
            Append("*)((char *)");
            Append(arrayPtr.Value.Name);
            Append(" + ARRAY_BASE + sizeof(");
            Append(_writer.GetCppSignatureTypeName(elementType));
            Append(") * ");
            Append(index.Value.Name);
            Append(")");

            AppendSemicolon();
        }

        private void ImportStoreElement(int token)
        {
            ImportStoreElement(ResolveTypeToken(token));
        }

        private void ImportStoreElement(TypeDesc elementType)
        {
            // stelem_ref
            if (elementType == null)
                elementType = GetWellKnownType(WellKnownType.Object);

            var value = Pop();
            var index = Pop();
            var arrayPtr = Pop();

            // Range check
            AppendLine();
            Append("__range_check(");
            Append(arrayPtr.Value.Name);
            Append(",");
            Append(index.Value.Name);
            Append(");");

            // TODO: Array covariance
            // TODO: Write barrier as necessary!!!

            AppendLine();
            Append("*(");
            Append(_writer.GetCppSignatureTypeName(elementType));
            Append("*)((char *)");
            Append(arrayPtr.Value.Name);
            Append(" + ARRAY_BASE + sizeof(");
            Append(_writer.GetCppSignatureTypeName(elementType));
            Append(") * ");
            Append(index.Value.Name);
            Append(") = ");

            AppendCastIfNecessary(elementType, value.Kind);
            Append(value.Value.Name);

            AppendSemicolon();
        }

        private void ImportAddressOfElement(int token)
        {
            TypeDesc elementType = (TypeDesc)_methodIL.GetObject(token);
            var index = Pop();
            var arrayPtr = Pop();

            // Range check
            AppendLine();
            Append("__range_check(");
            Append(arrayPtr.Value.Name);
            Append(",");
            Append(index.Value.Name);
            Append(");");

            TypeDesc byRef = elementType.MakeByRefType();

            PushTemp(StackValueKind.ByRef, byRef);
            AppendCastIfNecessary(StackValueKind.ByRef, byRef);

            Append("(");
            Append(_writer.GetCppSignatureTypeName(elementType));
            Append("*)((char *)");
            Append(arrayPtr.Value.Name);
            Append(" + ARRAY_BASE + sizeof(");
            Append(_writer.GetCppSignatureTypeName(elementType));
            Append(") * ");
            Append(index.Value.Name);
            Append(")");

            AppendSemicolon();
        }

        private void ImportLoadLength()
        {
            var arrayPtr = Pop();

            PushTemp(StackValueKind.NativeInt);

            Append("*((intptr_t *)");
            Append(arrayPtr.Value.Name);
            Append("+ 1)");

            AppendSemicolon();
        }

        private void ImportUnaryOperation(ILOpcode opCode)
        {
            var argument = Pop();

            if (argument.Kind == StackValueKind.Float)
                throw new NotImplementedException();

            PushTemp(argument.Kind, argument.Type);

            Append((opCode == ILOpcode.neg) ? "~" : "!");
            Append(argument.Value.Name);

            AppendSemicolon();
        }

        private void ImportCpOpj(int token)
        {
            throw new NotImplementedException();
        }

        private void ImportUnbox(int token, ILOpcode opCode)
        {
            var type = ResolveTypeToken(token);

            var obj = Pop();

            if (opCode == ILOpcode.unbox)
            {
                PushTemp(StackValueKind.ByRef, type.MakeByRefType());
            }
            else
            {
                PushTemp(GetStackValueKind(type), type);
            }

            if (type.IsValueType)
            {
                // TODO: Unbox of nullable types
                if (type.IsNullable)
                    throw new NotImplementedException();

                if (opCode == ILOpcode.unbox_any)
                {
                    string typeName = GetStackValueKindCPPTypeName(GetStackValueKind(type), type);
                    Append("*(");
                    Append(typeName);
                    Append("*)");
                }

                Append("((void **)");
                Append(obj.Value.Name);
                Append("+1)");
            }
            else
            {
                // TODO: Cast
                Append(obj.Value.Name);
            }

            AppendSemicolon();
        }

        private void ImportRefAnyVal(int token)
        {
            throw new NotImplementedException();
        }

        private void ImportCkFinite()
        {
            throw new NotImplementedException();
        }

        private void ImportMkRefAny(int token)
        {
            throw new NotImplementedException();
        }

        private void ImportLdToken(int token)
        {
            var ldtokenValue = _methodIL.GetObject(token);
            WellKnownType ldtokenKind;
            string name;
            if (ldtokenValue is TypeDesc)
            {
                ldtokenKind = WellKnownType.RuntimeTypeHandle;
                AddTypeReference((TypeDesc)ldtokenValue, false);

                MethodDesc helper = _typeSystemContext.GetHelperEntryPoint("LdTokenHelpers", "GetRuntimeTypeHandle");
                AddMethodReference(helper);

                name = String.Concat(
                    _writer.GetCppTypeName(helper.OwningType),
                    "::",
                    _writer.GetCppMethodName(helper),
                    "((intptr_t)",
                    _writer.GetCppTypeName((TypeDesc)ldtokenValue),
                    "::__getMethodTable())");
            }
            else if (ldtokenValue is FieldDesc)
            {
                ldtokenKind = WellKnownType.RuntimeFieldHandle;
                name = null;
            }
            else if (ldtokenValue is MethodDesc)
            {
                throw new NotImplementedException();
            }
            else
                throw new InvalidOperationException();

            var value = new StackValue
            {
                Kind = StackValueKind.ValueType,
                Value = new Value
                {
                    Aux = ldtokenValue,
                    Name = name
                },
                Type = GetWellKnownType(ldtokenKind),
            };
            Push(value);
        }

        private void ImportLocalAlloc()
        {
            StackValue count = Pop();

            // TODO: this is machine dependent and might not result in a HW stack overflow exception
            // TODO: might not have enough alignment guarantees for the allocated buffer

            var bufferName = NewTempName();
            AppendLine();
            Append("void* ");
            Append(bufferName);
            Append(" = alloca(");
            Append(count.Value.Name);
            Append(")");
            AppendSemicolon();

            if (_methodIL.GetInitLocals())
            {
                AppendLine();
                Append("memset(");
                Append(bufferName);
                Append(", 0, ");
                Append(count.Value.Name);
                Append(")");
                AppendSemicolon();
            }

            Push(StackValueKind.NativeInt, new Value(bufferName));
        }

        private void ImportEndFilter()
        {
            throw new NotImplementedException();
        }

        private void ImportCpBlk()
        {
            throw new NotImplementedException();
        }

        private void ImportInitBlk()
        {
            throw new NotImplementedException();
        }

        private void ImportRethrow()
        {
            throw new NotImplementedException();
        }

        private void ImportSizeOf(int token)
        {
            var type = ResolveTypeToken(token);

            // TODO: Remove
            _writer.GetCppSignatureTypeName(type);

            Push(StackValueKind.Int32, new Value("sizeof(" + _writer.GetCppTypeName(type) + ")"));
        }

        private void ImportRefAnyType()
        {
            throw new NotImplementedException();
        }

        private void ImportArgList()
        {
            throw new NotImplementedException();
        }

        private void ImportUnalignedPrefix(byte alignment)
        {
            throw new NotImplementedException();
        }

        private void ImportVolatilePrefix()
        {
            // TODO:
            // throw new NotImplementedException();
        }

        private void ImportTailPrefix()
        {
            throw new NotImplementedException();
        }

        private void ImportConstrainedPrefix(int token)
        {
            _pendingPrefix |= Prefix.Constrained;

            _constrained = ResolveTypeToken(token);
        }

        private void ImportNoPrefix(byte mask)
        {
            throw new NotImplementedException();
        }

        private void ImportReadOnlyPrefix()
        {
            throw new NotImplementedException();
        }

        private void TriggerCctor(TypeDesc type)
        {
            // TODO: Before field init

            MethodDesc cctor = type.GetStaticConstructor();
            if (cctor == null)
                return;

            // TODO: Thread safety

            string ctorHasRun = "__statics.__cctor_" + _writer.GetCppTypeName(type).Replace("::", "__");
            AppendLine();
            Append("if (!" + ctorHasRun + ") {");
            Indent();
            AppendLine();
            Append(ctorHasRun + " = true;");
            AppendLine();
            Append(_writer.GetCppTypeName(cctor.OwningType));
            Append("::");
            Append(_writer.GetCppMethodName(cctor));
            Append("();");
            Exdent();
            AppendLine();
            Append("}");

            AddMethodReference(cctor);
        }

        private void AddTypeReference(TypeDesc type, bool constructed)
        {
            Object node;

            if (constructed)
                node = _nodeFactory.ConstructedTypeSymbol(type);
            else
                node = _nodeFactory.NecessaryTypeSymbol(type);

            _dependencies.Add(node);
        }

        private void AddMethodReference(MethodDesc method)
        {
            _dependencies.Add(_nodeFactory.MethodEntrypoint(method));
        }

        private void AddFieldReference(FieldDesc field)
        {
            if (field.IsStatic)
            {
                var owningType = (MetadataType)field.OwningType;

                Object node;
                if (field.IsThreadStatic)
                {
                    node = _nodeFactory.TypeThreadStaticsSymbol(owningType);
                }
                else
                {
                    if (field.HasGCStaticBase)
                        node = _nodeFactory.TypeGCStaticsSymbol(owningType);
                    else
                        node = _nodeFactory.TypeNonGCStaticsSymbol(owningType);
                }

                // TODO: Remove once the depedencies for static fields are tracked properly
                _writer.GetCppSignatureTypeName(owningType);

                _dependencies.Add(node);
            }
        }
    }
}
