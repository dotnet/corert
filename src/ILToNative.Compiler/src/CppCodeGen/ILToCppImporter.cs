// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System;
using System.Text;
using System.Collections.Generic;
using System.Diagnostics;

using Internal.TypeSystem;
using Internal.IL.Stubs;

using ILToNative;
using ILToNative.CppCodeGen;

namespace Internal.IL
{
    public struct ILSequencePoint
    {
        public int Offset;
        public string Document;
        public int LineNumber;
        // TODO: The remaining info
    }

    public struct LocalVariable
    {
        public int Slot;
        public string Name;
        public bool CompilerGenerated;
    }

    partial class ILImporter
    {
        Compilation _compilation;
        CppWriter _writer;

        TypeSystemContext _typeSystemContext;

        MethodDesc _method;
        MethodSignature _methodSignature;

        TypeDesc _thisType;

        MethodIL _methodIL;
        byte[] _ilBytes;
        TypeDesc[] _locals;

        struct SequencePoint
        {
            public string Document;
            public int LineNumber;
        }
        SequencePoint[] _sequencePoints;
        Dictionary<int, LocalVariable> _localSlotToInfoMap;
        Dictionary<int, string> _parameterIndexToNameMap;

        class ExceptionRegion
        {
            public ILExceptionRegion ILRegion;
            public int ReturnLabels;
        };
        ExceptionRegion[] _exceptionRegions;

        class SpillSlot
        {
            public StackValueKind Kind;
            public TypeDesc Type;
            public String Name;
        };
        List<SpillSlot> _spillSlots;

        // TODO: Unify with verifier?
        [Flags]
        enum Prefix
        {
            ReadOnly = 0x01,
            Unaligned = 0x02,
            Volatile = 0x04,
            Tail = 0x08,
            Constrained = 0x10,
            No = 0x20,
        }
        Prefix _pendingPrefix;
        TypeDesc _constrained;

        StringBuilder _builder = new StringBuilder();

        class BasicBlock
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
            _writer = writer;

            _method = method;
            _methodSignature = method.Signature;

            _typeSystemContext = method.Context;

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

        public void SetLocalVariables(IEnumerable<LocalVariable> localVariables)
        {
            try
            {
                HashSet<string> names = new HashSet<string>();
                var localSlotToInfoMap = new Dictionary<int, LocalVariable>();
                foreach (var v in localVariables)
                {
                    LocalVariable modifiedLocal = v;
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

        struct Value
        {
            public Value(String name)
            {
                Name = name;
                Aux = null;
            }

            public String Name;
            public Object Aux;
        };

        struct StackValue
        {
            public StackValueKind Kind;
            public TypeDesc Type;
            public Value Value;
        }

        StackValueKind GetStackValueKind(TypeDesc type)
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

        void Push(StackValue value)
        {
            if (_stackTop >= _stack.Length)
                Array.Resize(ref _stack, 2 * _stackTop + 3);
            _stack[_stackTop++] = value;
        }

        void Push(StackValueKind kind, Value value, TypeDesc type = null)
        {
            Push(new StackValue() { Kind = kind, Type = type, Value = value });
        }

        StackValue Pop()
        {
            return _stack[--_stackTop];
        }

        public static bool Msvc = true;

        string GetStackValueKindCPPTypeName(StackValueKind kind, TypeDesc type = null)
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

        int _currentTemp = 1;
        string NewTempName()
        {
            return "_" + (_currentTemp++).ToString();
        }

        void PushTemp(StackValueKind kind, TypeDesc type = null)
        {
            string temp = NewTempName();

            Push(kind, new Value(temp), type);

            _builder.Append(GetStackValueKindCPPTypeName(kind, type));
            _builder.Append(" ");
            _builder.Append(temp);
            _builder.Append("=");
        }

        void AppendCastIfNecessary(TypeDesc destType, StackValueKind srcType)
        {
            if (destType.IsValueType)
                return;
            Append("(");
            Append(_writer.GetCppSignatureTypeName(destType));
            Append(")");
        }

        void AppendCastIfNecessary(StackValueKind dstType, TypeDesc srcType)
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

        Value NewSpillSlot(StackValueKind kind, TypeDesc type)
        {
            if (_spillSlots == null)
                _spillSlots = new List<SpillSlot>();

            SpillSlot spillSlot = new SpillSlot();
            spillSlot.Kind = kind;
            spillSlot.Type = type;
            spillSlot.Name = "_s" + _spillSlots.Count.ToString();

            _spillSlots.Add(spillSlot);

            return new Value() { Name = spillSlot.Name };
        }

        void Append(string s)
        {
            _builder.Append(s);
        }

        void Finish()
        {
            // _builder.AppendLine(";");
            _builder.Append("; ");
        }

        string GetVarName(int index, bool argument)
        {
            if (_localSlotToInfoMap != null && !argument && _localSlotToInfoMap.ContainsKey(index) && !_localSlotToInfoMap[index].CompilerGenerated)
            {
                return _localSlotToInfoMap[index].Name;
            }

            if (_parameterIndexToNameMap != null && argument && _parameterIndexToNameMap.ContainsKey(index))
            {
                return _parameterIndexToNameMap[index];
            }

            return (argument ? "_a" : "_l") + index.ToString();
        }

        TypeDesc GetVarType(int index, bool argument)
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
               return _locals[index];
            }
        }

        TypeDesc GetWellKnownType(WellKnownType wellKnownType)
        {
            return _typeSystemContext.GetWellKnownType(wellKnownType);
        }

        TypeDesc ResolveTypeToken(int token)
        {
            return (TypeDesc)_methodIL.GetObject(token);
        }

        void MarkInstructionBoundary()
        {
        }

        void StartImportingInstruction()
        {
            if (_sequencePoints == null)
                return;

            var sequencePoint = _sequencePoints[_currentOffset];
            if (sequencePoint.Document == null)
                return;

            _builder.AppendLine();

            Append("#line ");
            Append(sequencePoint.LineNumber.ToString());
            Append(" \"");
            Append(sequencePoint.Document.Replace("\\", "\\\\"));
            Append("\"");

            _builder.AppendLine();
        }

        void EndImportingInstruction()
        {
            if (_sequencePoints == null)
                _builder.AppendLine();
        }

        public string Compile()
        {
            FindBasicBlocks();

            ImportBasicBlocks();

            if (_sequencePoints != null && _sequencePoints[0].Document != null)
            {
                var sequencePoint = _sequencePoints[0];

                Append("#line ");
                Append(sequencePoint.LineNumber.ToString());
                Append(" \"");
                Append(sequencePoint.Document.Replace("\\", "\\\\"));
                Append("\"");

                _builder.AppendLine();
            }

            Append(_writer.GetCppMethodDeclaration(_method, true));

            _builder.Append("{");

            bool initLocals = _methodIL.GetInitLocals();
            for (int i = 0; i < _locals.Length; i++)
            {
                Append(_writer.GetCppSignatureTypeName(_locals[i]));
                Append(" ");
                Append(GetVarName(i, false));
                if (initLocals)
                {
                    TypeDesc localType = _locals[i];
                    if (localType.IsValueType && !localType.IsPrimitive && !localType.IsEnum)
                    {
                        Finish();
                        Append("memset(&");
                        Append(GetVarName(i, false));
                        Append(",0,sizeof(");
                        Append(_writer.GetCppSignatureTypeName(localType));
                        Append("))");
                    }
                    else
                    {
                        Append("=0");
                    }
                }
                Finish();
            }

            if (_spillSlots != null)
            {
                for (int i = 0; i < _spillSlots.Count; i++)
                {
                    SpillSlot spillSlot = _spillSlots[i];
                    Append(GetStackValueKindCPPTypeName(spillSlot.Kind, spillSlot.Type));
                    Append(" ");
                    Append(spillSlot.Name);
                    Finish();
                }
            }

            for (int i = 0; i < _exceptionRegions.Length; i++)
            {
                var r = _exceptionRegions[i];
                if (r.ReturnLabels != 0)
                {
                    _builder.Append("int __finallyReturn");
                    _builder.Append(i.ToString());
                    _builder.Append("=0;");
                }
            }

            for (int i = 0; i < _basicBlocks.Length; i++)
            {
                BasicBlock basicBlock = _basicBlocks[i];
                if (basicBlock != null)
                {
                    _builder.Append("_bb");
                    _builder.Append(i.ToString());
                    _builder.AppendLine(": {");
                    _builder.Append(basicBlock.Code);
                    // _builder.AppendLine("}");
                    _builder.Append("} ");
                }
            }

            for (int i = 0; i < _exceptionRegions.Length; i++)
            {
                var r = _exceptionRegions[i];
                if (r.ReturnLabels != 0)
                {
                    _builder.AppendLine("__endFinally" + i.ToString() + ": switch(__finallyReturn" + i.ToString() + ") {");
                    for (int j = 1; j <= r.ReturnLabels; j++)
                        _builder.AppendLine("case " + j.ToString() + ": goto __returnFromFinally" + i.ToString() + "_" + j.ToString() + ";");
                    _builder.AppendLine("default: " + (Msvc ? "__assume(0)" : "__builtin_unreachable()") + "; }");
                }
            }

            _builder.AppendLine("}");

            return _builder.ToString();
        }

        void StartImportingBasicBlock(BasicBlock basicBlock)
        {
        }

        void EndImportingBasicBlock(BasicBlock basicBlock)
        {
            basicBlock.Code = _builder.ToString();
            _builder.Clear();
        }

        void ImportNop()
        {
        }

        void ImportBreak()
        {
            throw new NotImplementedException("Opcode: break");
        }

        void ImportLoadVar(int index, bool argument)
        {
            string name = GetVarName(index, argument);
            string temp = NewTempName();

            TypeDesc type = GetVarType(index, argument);
            StackValueKind kind = GetStackValueKind(type);

            PushTemp(kind, type);
            AppendCastIfNecessary(kind, type);
            Append(name);
            Finish();
        }

        void ImportStoreVar(int index, bool argument)
        {
            var value = Pop();

            string name = GetVarName(index, argument);

            Append(name);
            Append("=");
            TypeDesc type = GetVarType(index, argument);
            AppendCastIfNecessary(type, value.Kind);
            Append(value.Value.Name);
            Finish();
        }

        void ImportAddressOfVar(int index, bool argument)
        {
            string name = GetVarName(index, argument);
            string temp = NewTempName();

            TypeDesc type = GetVarType(index, argument);
            type = type.MakeByRefType();

            PushTemp(StackValueKind.ByRef, type);
            AppendCastIfNecessary(StackValueKind.ByRef, type);
            Append("&");
            Append(name);
            Finish();
        }

        void ImportDup()
        {
            Push(_stack[_stackTop - 1]);
        }

        void ImportPop()
        {
            Pop();
        }

        void ImportJmp(int token)
        {
            throw new NotImplementedException("Opcode: jmp");
        }

        void ImportCasting(ILOpcode opcode, int token)
        {
            TypeDesc type = (TypeDesc)_methodIL.GetObject(token);

            if (type.IsInterface || type.IsArray)
                throw new NotImplementedException();

            var value = Pop();
            PushTemp(StackValueKind.ObjRef, type);

            Append(opcode == ILOpcode.isinst ? "__isinst_class" : "__castclass_class");
            Append("(");
            Append(value.Value.Name);
            Append(", ");
            Append(_writer.GetCppTypeName(type));
            Append("::__getMethodTable())");
            Finish();
        }

        void ImportIntrinsicCall(IntrinsicMethodKind intrinsicClassification)
        {
            switch (intrinsicClassification)
            {
                case IntrinsicMethodKind.RuntimeHelpersInitializeArray:
                    {
                        var fieldSlot = Pop();
                        var arraySlot = Pop();

                        var fieldDesc = (TypeSystem.Ecma.EcmaField)fieldSlot.Value.Aux;
                        int addr = fieldDesc.MetadataReader.GetFieldDefinition(fieldDesc.Handle).GetRelativeVirtualAddress();
                        var memBlock = fieldDesc.Module.PEReader.GetSectionData(addr).GetContent();

                        var fieldType = (TypeSystem.Ecma.EcmaType)fieldDesc.FieldType;
                        int size = fieldType.MetadataReader.GetTypeDefinition(fieldType.Handle).GetLayout().Size;
                        if (size == 0)
                            throw new NotImplementedException();

                        // TODO: Need to do more for arches with different endianness?
                        var preinitDataHolder = NewTempName();
                        Append("static const char ");
                        Append(preinitDataHolder);
                        Append("[] = { ");

                        for (int i = 0; i < size; i++)
                        {
                            if (i != 0)
                                Append(", ");
                            Append(String.Format("0x{0:X}", memBlock[i]));
                        }
                        Append(" }");
                        Finish();

                        Append("memcpy((char*)");
                        Append(arraySlot.Value.Name);
                        Append(" + ARRAY_BASE, ");
                        Append(preinitDataHolder);
                        Append(", ");
                        Append(size.ToString());
                        Append(")");

                        Finish();
                        break;
                    }
                default: throw new NotImplementedException();
            }
        }

        void ImportCall(ILOpcode opcode, int token)
        {
            bool callViaSlot = false;
            bool delegateInvoke = false;
            bool staticShuffleThunk = false;
            bool mdArrayCreate = false;

            MethodDesc method = (MethodDesc)_methodIL.GetObject(token);

            var intrinsicClassification = IntrinsicMethods.GetIntrinsicMethodClassification(method);
            if (intrinsicClassification != IntrinsicMethodKind.None)
            {
                ImportIntrinsicCall(intrinsicClassification);
                return;
            }

            if (opcode == ILOpcode.calli)
                throw new NotImplementedException();

            TypeDesc constrained = null;
            if (opcode != ILOpcode.newobj)
            {
                if ((_pendingPrefix & Prefix.Constrained) != 0 && opcode == ILOpcode.callvirt)
                {
                    _pendingPrefix &= ~Prefix.Constrained;
                    constrained = _constrained;

                    // TODO:
                    throw new NotImplementedException();
                }
            }

            TypeDesc retType = null;

            {
                TypeDesc owningType = method.OwningType;
                if (opcode == ILOpcode.newobj)
                    retType = owningType;

                if (opcode == ILOpcode.newobj)
                {
                    if (owningType.IsString)
                    {
                        // String constructors actually look like regular method calls
                        method = IntrinsicMethods.GetStringInitializer(method);
                        opcode = ILOpcode.call;
                    }
                    else if (owningType.IsArray)
                    {
                        mdArrayCreate = true;
                    }
                    else if (owningType.IsDelegate)
                    {
                        method = GetDelegateCtor(owningType, ref _stack[_stackTop - 1].Value, out staticShuffleThunk);
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

                    _compilation.AddVirtualSlot(method);

                    callViaSlot = true;
                }
            }

            if (!callViaSlot && !delegateInvoke && !mdArrayCreate)
                _compilation.AddMethod(method);

            if (opcode == ILOpcode.newobj)
                _compilation.MarkAsConstructed(retType);

            var methodSignature = method.Signature;

            if (retType == null)
                retType = methodSignature.ReturnType;

            string temp = null;
            StackValueKind retKind = StackValueKind.Unknown;

            if (!retType.IsVoid)
            {
                retKind = GetStackValueKind(retType);
                temp = NewTempName();

                Append(GetStackValueKindCPPTypeName(retKind, retType));
                Append(" ");
                Append(temp);
                if (retType.IsValueType && opcode == ILOpcode.newobj)
                {
                    Append(";");
                }
                else
                {
                    Append("=");

                    if (retType.IsPointer)
                    {
                        Append("(intptr_t)");
                    }
                }
            }

            if (opcode == ILOpcode.newobj && !mdArrayCreate)
            {
                if (!retType.IsValueType)
                {
                    _compilation.AddType(retType);

                    Append("__allocate_object(");
                    Append(_writer.GetCppTypeName(retType));
                    Append("::__getMethodTable())");
                    Finish();

                    if (staticShuffleThunk)
                    {
                        _stack[_stackTop - 2].Value.Name = temp;
                    }
                }
            }

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
                _compilation.AddType(method.OwningType);
                Append("RhNewMDArray");
            }
            else
            {
                Append(_writer.GetCppTypeName(method.OwningType));
                Append("::");
                Append(_writer.GetCppMethodName(method));
            }


            Append("(");
            int count = methodSignature.Length;
            bool hasThis = !methodSignature.IsStatic;
            if (hasThis)
                count++;
            if (mdArrayCreate)
            {
                Append(_writer.GetCppTypeName(method.OwningType));
                Append("::__getMethodTable(), ");
                Append(((ArrayType)method.OwningType).Rank.ToString());
                Append(", ");
                count--;
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
                count--;
                if (count > 0)
                    Append(", ");
            }
            for (int i = 0; i < count; i++)
            {
                var op = _stack[_stackTop - count + i];
                int argIndex = methodSignature.Length - (count - i);
                TypeDesc argType;
                if (argIndex == -1)
                {
                    argType = method.OwningType;
                    if (argType.IsValueType)
                        argType = argType.MakeByRefType();
                }
                else
                {
                    argType = methodSignature[argIndex];
                }
                AppendCastIfNecessary(argType, op.Kind);
                Append(op.Value.Name);
                if (i != count - 1)
                    Append(", ");
            }
            _stackTop -= count;
            Append(")");

            if (temp != null)
                Push(retKind, new Value(temp), retType);
            Finish();
        }

        MethodDesc GetDelegateCtor(TypeDesc delegateType, ref Value fnptrValue, out bool staticShuffleThunk)
        {
            MethodDesc target = (MethodDesc)fnptrValue.Aux;

            staticShuffleThunk = false;

            // TODO: Delegates to static methods
            if (target.Signature.IsStatic)
            {
                MethodDesc shuffleThunk = new DelegateShuffleThunk(target);

                StringBuilder sb = new StringBuilder();
                sb.Append("(intptr_t)&");
                sb.Append(_writer.GetCppTypeName(shuffleThunk.OwningType));
                sb.Append("::");
                sb.Append(_writer.GetCppMethodName(shuffleThunk));
                fnptrValue.Name = sb.ToString();

                _compilation.AddMethod(shuffleThunk);

                staticShuffleThunk = true;
            }

            // TODO: Delegates on valuetypes
            if (target.OwningType.IsValueType)
                throw new NotImplementedException();

            return delegateType.BaseType.BaseType.GetMethod("InitializeClosedInstance", null);
        }

        void ImportLdFtn(int token, ILOpcode opCode)
        {
            MethodDesc method = (MethodDesc)_methodIL.GetObject(token);

            if (opCode == ILOpcode.ldvirtftn)
            {
                if (method.IsVirtual)
                    throw new NotImplementedException();
            }

            _compilation.AddMethod(method);

            PushTemp(StackValueKind.NativeInt);
            Append("(intptr_t)&");
            Append(_writer.GetCppTypeName(method.OwningType));
            Append("::");
            Append(_writer.GetCppMethodName(method));

            _stack[_stackTop-1].Value.Aux = method;

            Finish();
        }

        void ImportLoadInt(long value, StackValueKind kind)
        {
            string val;
            if (kind == StackValueKind.Int64)
            {
                if (value == Int64.MinValue)
                    val = "(int64_t)(0x8000000000000000" + (Msvc ? "i64" : "LL") + ")";
                else
                    val = value.ToString() + (Msvc ? "i64" : "LL");
            }
            else
            {
                if (value == Int32.MinValue)
                    val = "(int32_t)(0x80000000)";
                else
                    val = ((int)value).ToString();
            }

            Push(kind, new Value(val));
        }

        void ImportLoadFloat(double value)
        {
            // TODO: Handle infinity, NaN, etc.
            string val = value.ToString();
            Push(StackValueKind.Float, new Value(val));
        }

        void ImportLoadNull()
        {
            Push(StackValueKind.ObjRef, new Value("0"));
        }

        void ImportReturn()
        {
            var returnType = _methodSignature.ReturnType;
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
            Finish();
        }

        void ImportFallthrough(BasicBlock next)
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
                Append(entryStack[i].Value.Name);
                Append("=");
                Append(_stack[i].Value.Name);
                Finish();
            }

            MarkBasicBlock(next);
        }

        void ImportSwitchJump(int jmpBase, int[] jmpDelta, BasicBlock fallthrough)
        {
            var op = Pop();

            Append("switch (");
            Append(op.Value.Name);
            Append(") { ");


            for (int i = 0; i < jmpDelta.Length; i++)
            {
                BasicBlock target = _basicBlocks[jmpBase + jmpDelta[i]];

                Append("case " + i + ": ");
                ImportFallthrough(target);
                Append("goto _bb");
                Append(target.StartOffset.ToString());
                Append("; break; ");
            }
            Append("}");
            Finish();

            if (fallthrough != null)
                ImportFallthrough(fallthrough);
        }

        void ImportBranch(ILOpcode opcode, BasicBlock target, BasicBlock fallthrough)
        {
            if (opcode != ILOpcode.br)
            {
                Append("if (");
                if (opcode == ILOpcode.brfalse || opcode == ILOpcode.brtrue)
                {
                    var op = Pop();
                    Append(op.Value.Name);
                    Append((opcode == ILOpcode.brtrue) ? "!=0" : "==0");
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
                    Append(op);
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

            Append("{ ");
            ImportFallthrough(target);
            Append("goto _bb");
            Append(target.StartOffset.ToString());
            Append("; }");
            Finish();

            if (fallthrough != null)
                ImportFallthrough(fallthrough);
        }

        void ImportBinaryOperation(ILOpcode opcode)
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
            Append(op);
            if (unsigned)
            {
                Append("(u");
                Append(GetStackValueKindCPPTypeName(kind));
                Append(")");
            }
            Append(op1.Value.Name);

            Finish();
        }

        void ImportShiftOperation(ILOpcode opcode)
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

            Append((opcode == ILOpcode.shl) ? "<<" : ">>");

            Append(shiftAmount.Value.Name);
            Finish();
        }

        void ImportCompareOperation(ILOpcode opcode)
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
            Append(op);
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
            Finish();
        }

        void ImportConvert(WellKnownType wellKnownType, bool checkOverflow, bool unsigned)
        {
            var op = Pop();

            TypeDesc type = GetWellKnownType(wellKnownType);

            PushTemp(GetStackValueKind(type));
            Append("(");
            Append(_writer.GetCppSignatureTypeName(type));
            Append(")");
            Append(op.Value.Name);
            Finish();
        }

        void ImportLoadField(int token, bool isStatic)
        {
            FieldDesc field = (FieldDesc)_methodIL.GetObject(token);

            _compilation.AddField(field);

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
            }

            Finish();
        }

        void ImportAddressOfField(int token, bool isStatic)
        {
            FieldDesc field = (FieldDesc)_methodIL.GetObject(token);

            _compilation.AddField(field);

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
            }

            Finish();
        }


        void ImportStoreField(int token, bool isStatic)
        {
            FieldDesc field = (FieldDesc)_methodIL.GetObject(token);

            _compilation.AddField(field);

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
            }
            Append("=");
            if (!fieldType.IsValueType)
            {
                Append("(");
                Append(_writer.GetCppSignatureTypeName(fieldType));
                Append(")");
            }
            Append(value.Value.Name);

            Finish();
        }

        void ImportLoadIndirect(int token)
        {
            ImportLoadIndirect(ResolveTypeToken(token));
        }

        void ImportLoadIndirect(TypeDesc type)
        {
            if (type == null)
                type = GetWellKnownType(WellKnownType.Object);

            var addr = Pop();

            PushTemp(GetStackValueKind(type), type);

            Append("*(");
            Append(_writer.GetCppSignatureTypeName(type));
            Append("*)");
            Append(addr.Value.Name);

            Finish();
        }

        void ImportStoreIndirect(int token)
        {
            ImportStoreIndirect(ResolveTypeToken(token));
        }

        void ImportStoreIndirect(TypeDesc type)
        {
            if (type == null)
                type = GetWellKnownType(WellKnownType.Object);

            var value = Pop();
            var addr = Pop();

            // TODO: Write barrier as necessary!!!

            Append("*(");
            Append(_writer.GetCppSignatureTypeName(type));
            Append("*)");
            Append(addr.Value.Name);
            Append("=");
            AppendCastIfNecessary(type, value.Kind);
            Append(value.Value.Name);

            Finish();
        }

        void ImportThrow()
        {
            var obj = Pop();

            Append("__throw_exception(");
            Append(obj.Value.Name);
            Append(")");
            Finish();
        }

        void ImportLoadString(int token)
        {
            string str = (string)_methodIL.GetObject(token);

            PushTemp(StackValueKind.ObjRef, GetWellKnownType(WellKnownType.String));

            StringBuilder escaped = new StringBuilder();
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
            Finish();
        }

        void ImportInitObj(int token)
        {
            TypeDesc type = (TypeDesc)_methodIL.GetObject(token);

            var addr = Pop();
            Append("memset((void*)");
            Append(addr.Value.Name);
            Append(",0,sizeof(");
            Append(_writer.GetCppSignatureTypeName(type));
            Append("))");
            Finish();
        }

        void ImportBox(int token)
        {
            TypeDesc type = (TypeDesc)_methodIL.GetObject(token);

            if (type.IsValueType)
            {
                if (type.IsNullable)
                    throw new NotImplementedException();

                var value = Pop();

                PushTemp(StackValueKind.ObjRef, type);

                _compilation.AddType(type);
                _compilation.MarkAsConstructed(type);

                Append("__allocate_object(");
                Append(_writer.GetCppTypeName(type));
                Append("::__getMethodTable())");
                Finish();

                string typeName = GetStackValueKindCPPTypeName(GetStackValueKind(type), type);

                // TODO: Write barrier as necessary
                Append("*(" + typeName + " *)((void **)" + _stack[_stackTop - 1].Value.Name + "+1) = " + value.Value.Name);
                Finish();
            }
        }

        static bool IsOffsetContained(int offset, int start, int length)
        {
            return start <= offset && offset < start + length;
        }

        static string AddReturnLabel(ExceptionRegion r)
        {
            r.ReturnLabels++;
            return r.ReturnLabels.ToString();
        }

        void ImportLeave(BasicBlock target)
        {
            // Empty the stack
            _stackTop = 0;

            // Close the scope and open a new one so that we don't put a goto label in the middle
            // of a scope.
            Append("} {");

            for (int i = 0; i < _exceptionRegions.Length; i++)
            {
                var r = _exceptionRegions[i];

                if (r.ILRegion.Kind == ILExceptionRegionKind.Finally &&
                    IsOffsetContained(_currentOffset - 1, r.ILRegion.TryOffset, r.ILRegion.TryLength) &&
                    !IsOffsetContained(target.StartOffset, r.ILRegion.TryOffset, r.ILRegion.TryLength))
                {
                    string returnLabel = AddReturnLabel(r);

                    Append("__finallyReturn");
                    Append(i.ToString());
                    Append("=");
                    Append(returnLabel);
                    Finish();

                    Append("goto _bb");
                    Append(r.ILRegion.HandlerOffset.ToString());
                    Finish();

                    Append("__returnFromFinally");
                    Append(i.ToString());
                    Append("_");
                    Append(returnLabel);
                    Append(":");
                    Finish();

                    MarkBasicBlock(_basicBlocks[r.ILRegion.HandlerOffset]);
                }
            }

            Append("goto _bb");
            Append(target.StartOffset.ToString());
            Finish();

            MarkBasicBlock(target);
        }

        int FindNearestFinally(int offset)
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

        void ImportEndFinally()
        {
            int finallyIndex = FindNearestFinally(_currentOffset - 1);

            Append("goto __endFinally");
            Append(finallyIndex.ToString());
            Finish();
        }

        void ImportNewArray(int token)
        {
            TypeDesc type = (TypeDesc)_methodIL.GetObject(token);
            TypeDesc arrayType = type.Context.GetArrayType(type);

            var numElements = Pop();

            PushTemp(StackValueKind.ObjRef, arrayType);

            _compilation.AddType(arrayType);
            _compilation.MarkAsConstructed(arrayType);

            Append("__allocate_array(");
            Append(numElements.Value.Name);
            Append(", ");
            Append(_writer.GetCppTypeName(arrayType));
            Append("::__getMethodTable()");
            Append(")");
            Finish();
        }

        void ImportLoadElement(int token)
        {
            ImportLoadElement(ResolveTypeToken(token));
        }

        void ImportLoadElement(TypeDesc elementType)
        {
            // ldelem_ref
            if (elementType == null)
                elementType = GetWellKnownType(WellKnownType.Object);

            var index = Pop();
            var arrayPtr = Pop();

            // Range check
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

            Finish();
        }

        void ImportStoreElement(int token)
        {
            ImportStoreElement(ResolveTypeToken(token));
        }

        void ImportStoreElement(TypeDesc elementType)
        {
            // stelem_ref
            if (elementType == null)
                elementType = GetWellKnownType(WellKnownType.Object);

            var value = Pop();
            var index = Pop();
            var arrayPtr = Pop();
            
            // Range check
            Append("__range_check(");
            Append(arrayPtr.Value.Name);
            Append(",");
            Append(index.Value.Name);
            Append(");");

            // TODO: Array covariance
            // TODO: Write barrier as necessary!!!

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

            Finish();
        }

        void ImportAddressOfElement(int token)
        {
            TypeDesc elementType = (TypeDesc)_methodIL.GetObject(token);
            var index = Pop();
            var arrayPtr = Pop();

            // Range check
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

            Finish();
        }

        void ImportLoadLength()
        {
            var arrayPtr = Pop();

            PushTemp(StackValueKind.NativeInt);

            Append("*((intptr_t *)");
            Append(arrayPtr.Value.Name);
            Append("+ 1)");

            Finish();
        }

        void ImportUnaryOperation(ILOpcode opCode)
        {
            var argument = Pop();

            PushTemp(argument.Kind, argument.Type);

            Append((opCode == ILOpcode.neg) ? "~" : "!");
            Append(argument.Value.Name);
            
            Finish();
        }

        void ImportCpOpj(int token)
        {
            throw new NotImplementedException();
        }

        void ImportUnbox(int token, ILOpcode opCode)
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

            Finish();
        }

        void ImportRefAnyVal(int token)
        {
            throw new NotImplementedException();
        }

        void ImportCkFinite()
        {
            throw new NotImplementedException();
        }

        void ImportMkRefAny(int token)
        {
            throw new NotImplementedException();
        }

        void ImportLdToken(int token)
        {
            var ldtokenValue = _methodIL.GetObject(token);
            WellKnownType ldtokenKind;
            if (ldtokenValue is TypeDesc)
                ldtokenKind = WellKnownType.RuntimeTypeHandle;
            else if (ldtokenValue is FieldDesc)
                ldtokenKind = WellKnownType.RuntimeFieldHandle;
            else if (ldtokenValue is MethodDesc)
                ldtokenKind = WellKnownType.RuntimeMethodHandle;
            else
                throw new InvalidOperationException();

            if (ldtokenKind != WellKnownType.RuntimeFieldHandle)
                throw new NotImplementedException();

            var value = new StackValue
            {
                Kind = StackValueKind.ValueType,
                Value = new Value
                {
                    Aux = ldtokenValue
                },
                Type = GetWellKnownType(ldtokenKind),
            };
            Push(value);
        }

        void ImportLocalAlloc()
        {
            StackValue count = Pop();

            // TODO: this is machine dependent and might not result in a HW stack overflow exception
            // TODO: might not have enough alignment guarantees for the allocated buffer

            var bufferName = NewTempName();
            Append("void* ");
            Append(bufferName);
            Append(" = alloca(");
            Append(count.Value.Name);
            Append(")");
            Finish();

            if (_methodIL.GetInitLocals())
            {
                Append("memset(");
                Append(bufferName);
                Append(", 0, ");
                Append(count.Value.Name);
                Append(")");
                Finish();
            }

            Push(StackValueKind.NativeInt, new Value(bufferName));
        }

        void ImportEndFilter()
        {
            throw new NotImplementedException();
        }

        void ImportCpBlk()
        {
            throw new NotImplementedException();
        }

        void ImportInitBlk()
        {
            throw new NotImplementedException();
        }

        void ImportRethrow()
        {
            throw new NotImplementedException();
        }

        void ImportSizeOf(int token)
        {
            var type = ResolveTypeToken(token);

            Push(StackValueKind.Int32, new Value("sizeof(" + _writer.GetCppTypeName(type) + ")"));
        }

        void ImportRefAnyType()
        {
            throw new NotImplementedException();
        }

        void ImportArgList()
        {
            throw new NotImplementedException();
        }

        void ImportUnalignedPrefix(byte alignment)
        {
            throw new NotImplementedException();
        }

        void ImportVolatilePrefix()
        {
            // TODO:
            // throw new NotImplementedException();
        }

        void ImportTailPrefix()
        {
            throw new NotImplementedException();
        }

        void ImportConstrainedPrefix(int token)
        {
            _pendingPrefix |= Prefix.Constrained;

            _constrained = ResolveTypeToken(token);
        }

        void ImportNoPrefix(byte mask)
        {
            throw new NotImplementedException();
        }

        void ImportReadOnlyPrefix()
        {
            throw new NotImplementedException();
        }

        void TriggerCctor(TypeDesc type)
        {
            // TODO: Before field init

            MethodDesc cctor = type.GetMethod(".cctor", null);
            if (cctor == null)
                return;

            // TODO: Thread safety

            string ctorHasRun = "__statics.__cctor_" + _writer.GetCppTypeName(type).Replace("::", "__");
            Append("if (!" + ctorHasRun + ") { " + ctorHasRun + " = true; ");
            Append(_writer.GetCppTypeName(cctor.OwningType));
            Append("::");
            Append(_writer.GetCppMethodName(cctor));
            Append("(); }");

            _compilation.AddMethod(cctor);
        }
    }
}
