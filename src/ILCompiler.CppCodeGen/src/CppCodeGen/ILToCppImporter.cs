// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using System.Collections.Generic;
using System.Diagnostics;

using Internal.TypeSystem;

using ILCompiler;
using ILCompiler.Compiler.CppCodeGen;
using ILCompiler.CppCodeGen;

using ILCompiler.DependencyAnalysis;

namespace Internal.IL
{
    internal partial class ILImporter
    {
        /// <summary>
        /// Stack of values pushed onto the IL stack: locals, arguments, values, function pointer, ...
        /// </summary>
        private EvaluationStack<StackEntry> _stack = new EvaluationStack<StackEntry>(0);

        private Compilation _compilation;
        private NodeFactory _nodeFactory;
        private CppWriter _writer;

        private TypeSystemContext _typeSystemContext;

        private MethodDesc _method;
        private MethodSignature _methodSignature;

        private TypeDesc _thisType;

        private MethodIL _methodIL;
        private MethodIL _canonMethodIL;
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
            public enum ImportState : byte
            {
                Unmarked,
                IsPending
            }

            public BasicBlock Next;

            public int StartOffset;
            public ImportState State = ImportState.Unmarked;

            public EvaluationStack<StackEntry> EntryStack;

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

            if (!_methodSignature.IsStatic)
                _thisType = method.OwningType;

            _canonMethodIL = methodIL;

            // Get the runtime determined method IL so that this works right in shared code
            // and tokens in shared code resolve to runtime determined types.
            MethodIL uninstantiatiedMethodIL = methodIL.GetMethodILDefinition();
            if (methodIL != uninstantiatiedMethodIL)
            {
                MethodDesc sharedMethod = method.GetSharedRuntimeFormMethodTarget();
                _methodIL = new InstantiatedMethodIL(sharedMethod, uninstantiatiedMethodIL);
            }
            else
            {
                _methodIL = methodIL;
            }

            _ilBytes = methodIL.GetILBytes();
            _locals = methodIL.GetLocals();

            var ilExceptionRegions = methodIL.GetExceptionRegions();
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
                    string sanitizedName = _writer.SanitizeCppVarName(v.Name);
                    if (!names.Add(sanitizedName))
                    {
                        sanitizedName = string.Format("{0}_local{1}", sanitizedName, v.Slot);
                        names.Add(sanitizedName);
                    }

                    localSlotToInfoMap[v.Slot] = new ILLocalVariable(v.Slot, sanitizedName, v.CompilerGenerated);
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
                parameterIndexToNameMap[index] = _writer.SanitizeCppVarName(p);
                ++index;
            }

            _parameterIndexToNameMap = parameterIndexToNameMap;
        }

        private ISymbolNode GetGenericLookupHelper(ReadyToRunHelperId helperId, object helperArgument)
        {
            if (_method.RequiresInstMethodDescArg())
            {
                return _nodeFactory.ReadyToRunHelperFromDictionaryLookup(helperId, helperArgument, _method);
            }
            else
            {
                Debug.Assert(_method.RequiresInstMethodTableArg() || _method.AcquiresInstMethodTableFromThis());
                return _nodeFactory.ReadyToRunHelperFromTypeLookup(helperId, helperArgument, _method.OwningType);
            }
        }

        private string GetGenericContext()
        {
            Debug.Assert(_method.IsSharedByGenericInstantiations);

            if (_method.AcquiresInstMethodTableFromThis())
            {
                return String.Concat(
                    "*(void **)",
                    GetVarName(0, true));
            }
            else
            {
                return _writer.GetCppHiddenParam();
            }
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
                case TypeFlags.SzArray:
                    return StackValueKind.ObjRef;
                case TypeFlags.ByRef:
                    return StackValueKind.ByRef;
                case TypeFlags.Pointer:
                    return StackValueKind.NativeInt;
                default:
                    return StackValueKind.Unknown;
            }
        }

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
                case StackValueKind.ValueType:
                    return GetSignatureTypeNameAndAddReference(type);

                default: throw new NotSupportedException();
            }
        }

        private int _currentTemp = 1;
        private string NewTempName()
        {
            return "_" + (_currentTemp++).ToStringInvariant();
        }

        /// <summary>
        /// Push an expression named <paramref name="name"/> of kind <paramref name="kind"/>.
        /// </summary>
        /// <param name="kind">Kind of entry in stack</param>
        /// <param name="name">Variable to be pushed</param>
        /// <param name="type">Type if any of <paramref name="name"/></param>
        private void PushExpression(StackValueKind kind, string name, TypeDesc type = null)
        {
            Debug.Assert(kind != StackValueKind.Unknown, "Unknown stack kind");

            _stack.Push(new ExpressionEntry(kind, name, type));
        }

        /// <summary>
        /// Push a new temporary local of kind <paramref name="kind"/> and type <paramref name="type"/> and generate its declaration.
        /// </summary>
        /// <param name="kind">Kind of entry in stack</param>
        /// <param name="type">Type if any for new entry in stack</param>
        private void PushTemp(StackValueKind kind, TypeDesc type = null)
        {
            Debug.Assert(kind != StackValueKind.Unknown, "Unknown stack kind");

            PushTemp(new ExpressionEntry(kind, NewTempName(), type));
        }

        /// <summary>
        /// Push a new entry onto evaluation stack and declare a temporary local to hold its representation.
        /// </summary>
        /// <param name="entry">Entry to push onto evaluation stack.</param>
        private void PushTemp(StackEntry entry)
        {
            _stack.Push(entry);

            // Start declaration on a new line
            AppendLine();
            Append(GetStackValueKindCPPTypeName(entry.Kind, entry.Type));
            Append(" ");
            Append(entry);
            Append(" = ");
        }

        /// <summary>
        /// Generate a cast in case the stack type of source is not identical or compatible with destination type.
        /// </summary>
        /// <param name="destType">Type of destination</param>
        /// <param name="srcEntry">Source entry from stack</param>
        private void AppendCastIfNecessary(TypeDesc destType, StackEntry srcEntry)
        {
            ConstantEntry constant = srcEntry as ConstantEntry;
            if ((constant != null) && (constant.IsCastNecessary(destType)) || !destType.IsValueType || destType != srcEntry.Type)
            {
                if (srcEntry.Kind == StackValueKind.ValueType)
                {
                    Append("*(");
                    Append(GetSignatureTypeNameAndAddReference(destType));
                    Append("*");
                    Append(")&");
                }
                else
                {
                    Append("(");
                    Append(GetSignatureTypeNameAndAddReference(destType));
                    Append(")");
                }
            }
        }

        private void AppendCastIfNecessary(StackValueKind dstType, TypeDesc srcType)
        {
            if (dstType == StackValueKind.ByRef)
            {
                Append("(");
                Append(GetSignatureTypeNameAndAddReference(srcType));
                Append(")");
            }
            else
            if (srcType.IsPointer)
            {
                Append("(intptr_t)");
            }
        }

        private void AppendComparison(ILOpcode opcode, StackEntry op1, StackEntry op2)
        {
            // StackValueKind is carefully ordered to make this work (assuming the IL is valid)
            StackValueKind kind = (op1.Kind > op2.Kind) ? op1.Kind : op2.Kind;

            string op = null;
            bool unsigned = false;
            bool inverted = false;
            switch (opcode)
            {
                case ILOpcode.beq:
                case ILOpcode.ceq:
                    op = "==";
                    unsigned = op1.Kind == StackValueKind.ByRef ^ op2.Kind == StackValueKind.ByRef;
                    break;
                case ILOpcode.bge:
                    op = ">=";
                    break;
                case ILOpcode.bgt:
                case ILOpcode.cgt:
                    op = ">";
                    break;
                case ILOpcode.ble:
                    op = "<=";
                    break;
                case ILOpcode.blt:
                case ILOpcode.clt:
                    op = "<";
                    break;
                case ILOpcode.bne_un:
                    op = "!=";
                    unsigned = op1.Kind == StackValueKind.ByRef ^ op2.Kind == StackValueKind.ByRef;
                    break;
                case ILOpcode.bge_un:
                    if (kind == StackValueKind.Float)
                    {
                        op = "<";
                        inverted = true;
                    }
                    else
                    {
                        op = ">=";
                        unsigned = true;
                    }
                    break;
                case ILOpcode.bgt_un:
                case ILOpcode.cgt_un:
                    if (kind == StackValueKind.Float)
                    {
                        op = "<=";
                        inverted = true;
                    }
                    else
                    if (op1.Kind == StackValueKind.ObjRef && op1.Type == null)
                    {
                        // ECMA-335 III.1.5 Operand type table, P. 303:
                        // cgt.un is commonly used when comparing an ObjectRef with null (there is no "compare - not - equal" instruction)
                        // Turn into more natural compare not equal.
                        op = "!=";
                    }
                    else
                    {
                        op = ">";
                        unsigned = true;
                    }
                    break;
                case ILOpcode.ble_un:
                    if (kind == StackValueKind.Float)
                    {
                        op = ">";
                        inverted = true;
                    }
                    else
                    {
                        op = "<=";
                        unsigned = true;
                    }
                    break;
                case ILOpcode.blt_un:
                case ILOpcode.clt_un:
                    if (kind == StackValueKind.Float)
                    {
                        op = ">=";
                        inverted = true;
                    }
                    else
                    {
                        op = "<";
                        unsigned = true;
                    }
                    break;
                default:
                    Debug.Fail("Unexpected opcode");
                    break;
            }

            if (inverted)
            {
                Append("!(");
            }
            if (unsigned)
            {
                if (kind < StackValueKind.ByRef)
                {
                    Append("(u");
                    Append(GetStackValueKindCPPTypeName(kind));
                    Append(")");
                }
                else
                {
                    if (op2.Kind < StackValueKind.ByRef
                            || (op2.Kind == StackValueKind.ObjRef && op2.Type == null))
                        Append("(void*)");
                }
            }
            Append(op2);
            Append(" ");
            Append(op);
            Append(" ");
            if (unsigned)
            {
                if (kind < StackValueKind.ByRef)
                {
                    Append("(u");
                    Append(GetStackValueKindCPPTypeName(kind));
                    Append(")");
                }
                else
                {
                    if (op1.Kind < StackValueKind.ByRef
                            || (op1.Kind == StackValueKind.ObjRef && op1.Type == null))
                        Append("(void*)");
                }
            }
            Append(op1);
            if (inverted)
            {
                Append(")");
            }
        }

        private string GetSymbolNodeName(ISymbolNode node)
        {
            return node.GetMangledName(_nodeFactory.NameMangler).Replace("::", "_");
        }

        private void AppendMethodGenericDictionary(MethodDesc method)
        {
            ISymbolNode node = _nodeFactory.MethodGenericDictionary(method);
            _dependencies.Add(node);

            Append(GetSymbolNodeName(node));
        }

        private void AppendRuntimeMethodHandle(MethodDesc method)
        {
            ISymbolNode node = _nodeFactory.RuntimeMethodHandle(method);
            _dependencies.Add(node);

            Append(_writer.GetCppSymbolNodeName(_nodeFactory, node));
        }

        private void AppendFatFunctionPointer(MethodDesc method, bool isUnboxingStub = false)
        {
            ISymbolNode node = _nodeFactory.FatFunctionPointer(method, isUnboxingStub);
            _dependencies.Add(node);

            Append(_writer.GetCppSymbolNodeName(_nodeFactory, node));
        }

        private string GetGenericLookupHelperAndAddReference(ReadyToRunHelperId helperId, object helperArgument)
        {
            ISymbolNode node = GetGenericLookupHelper(helperId, helperArgument);
            _dependencies.Add(node);

            return _writer.GetCppReadyToRunGenericHelperNodeName(_nodeFactory, node as ReadyToRunGenericHelperNode);
        }

        private void AppendStaticFieldGenericLookupHelperAndAddReference(FieldDesc field)
        {
            Debug.Assert(field.IsStatic);

            ReadyToRunHelperId helperId;
            if (field.IsThreadStatic)
            {
                helperId = ReadyToRunHelperId.GetThreadStaticBase;
            }
            else if (field.HasGCStaticBase)
            {
                helperId = ReadyToRunHelperId.GetGCStaticBase;
            }
            else
            {
                helperId = ReadyToRunHelperId.GetNonGCStaticBase;
            }

            Append(GetGenericLookupHelperAndAddReference(helperId, field.OwningType));
        }

        private void AppendMethodAndAddReference(MethodDesc method, bool isUnboxingStub = false)
        {
            ISymbolNode node = _nodeFactory.MethodEntrypoint(method, isUnboxingStub);
            _dependencies.Add(node);

            Append(GetSymbolNodeName(node));
        }

        private StackEntry NewSpillSlot(StackEntry entry)
        {
            if (_spillSlots == null)
                _spillSlots = new List<SpillSlot>();

            SpillSlot spillSlot = new SpillSlot();
            spillSlot.Kind = entry.Kind;
            spillSlot.Type = entry.Type;
            spillSlot.Name = "_s" + _spillSlots.Count.ToStringInvariant();

            _spillSlots.Add(spillSlot);

            return new ExpressionEntry(entry.Kind, spillSlot.Name, entry.Type);
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
        /// Append string representation of <paramref name="value"/> to <see cref="_builder"/>.
        /// </summary>
        /// <param name="value">Value to print.</param>
        private void Append(StackEntry value)
        {
            value.Append(_builder);
        }

        /// <summary>
        /// Append a semicolon to <see cref="_builder"/>.
        /// </summary>
        private void AppendSemicolon()
        {
            _builder.Append(";");
        }

        /// <summary>
        /// Append the typedef of the method to assist in function pointer conversion
        /// </summary>
        /// <param name="method">Method typedef</param>
        private void AppendInterfaceCallTypeDef(MethodDesc method, string name)
        {
            _writer.AppendSignatureTypeDef(_builder, name, method.Signature, method.Signature.ReturnType);
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
                return _parameterIndexToNameMap[index];
            }

            return (argument ? "_a" : "_l") + index.ToStringInvariant();
        }

        private TypeDesc GetVarType(int index, bool argument)
        {
            TypeDesc type;

            if (argument)
            {
                if (_thisType != null)
                    index--;
                if (index == -1)
                {
                    if (_thisType.IsValueType)
                        type = _thisType.MakeByRefType();
                    else
                        type = _thisType;
                }
                else type = _methodSignature[index];
            }
            else
            {
                type = _locals[index].Type;
            }

            return _writer.ConvertToCanonFormIfNecessary(type, CanonicalFormKind.Specific);
        }

        private TypeDesc GetWellKnownType(WellKnownType wellKnownType)
        {
            return _typeSystemContext.GetWellKnownType(wellKnownType);
        }

        private TypeDesc ResolveTypeToken(int token)
        {
            return (TypeDesc)_canonMethodIL.GetObject(token);
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
            for (int i = 0; i < methodCodeNodeNeedingCode.Method.Signature.Length; i++)
            {
                var parameterType = methodCodeNodeNeedingCode.Method.Signature[i];
                AddTypeReference(_writer.ConvertToCanonFormIfNecessary(parameterType, CanonicalFormKind.Specific), false);
            }

            var returnType = methodCodeNodeNeedingCode.Method.Signature.ReturnType;
            if (!returnType.IsByRef)
            {
                AddTypeReference(_writer.ConvertToCanonFormIfNecessary(returnType, CanonicalFormKind.Specific), true);
            }
            var owningType = methodCodeNodeNeedingCode.Method.OwningType;

            AddTypeReference(owningType, true);

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
            _writer.AppendCppMethodDeclaration(_builder, _method, true);
            AppendLine();
            Append("{");
            Indent();


            if (_method.IsUnmanagedCallersOnly)
            {
                AppendLine();
                Append("ReversePInvokeFrame __frame");
                AppendSemicolon();
                AppendLine();
                Append("__reverse_pinvoke(&__frame)");
                AppendSemicolon();
            }

            bool initLocals = _methodIL.IsInitLocals;
            for (int i = 0; i < _locals.Length; i++)
            {
                TypeDesc localType = _writer.ConvertToCanonFormIfNecessary(_locals[i].Type, CanonicalFormKind.Specific);

                AppendLine();
                Append(GetSignatureTypeNameAndAddReference(localType));

                Append(" ");
                Append(GetVarName(i, false));
                if (initLocals)
                {
                    if (localType.IsValueType && !localType.IsPrimitive && !localType.IsEnum)
                    {
                        AppendSemicolon();
                        AppendLine();
                        Append("::memset(&");
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
                    Append("default: CORERT_UNREACHABLE;");
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
            _stack.Clear();

            EvaluationStack<StackEntry> entryStack = basicBlock.EntryStack;
            if (entryStack != null)
            {
                int n = entryStack.Length;
                for (int i = 0; i < n; i++)
                {
                    _stack.Push(entryStack[i].Duplicate());
                }
            }

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
            AppendLine();
            Append("__debug_break()");
            AppendSemicolon();
        }

        private void ImportLoadVar(int index, bool argument)
        {
            string name = GetVarName(index, argument);

            TypeDesc type = GetVarType(index, argument);
            StackValueKind kind = GetStackValueKind(type);

            PushTemp(kind, type);
            AppendCastIfNecessary(kind, type);
            Append(name);
            AppendSemicolon();
        }

        private void ImportStoreVar(int index, bool argument)
        {
            var value = _stack.Pop();

            string name = GetVarName(index, argument);

            AppendLine();
            Append(name);
            Append(" = ");
            TypeDesc type = GetVarType(index, argument);
            AppendCastIfNecessary(type, value);
            Append(value);
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
            _stack.Push(_stack.Peek().Duplicate());
        }

        private void ImportPop()
        {
            _stack.Pop();
        }

        private void ImportJmp(int token)
        {
            throw new NotImplementedException("Opcode: jmp");
        }

        private void ImportCasting(ILOpcode opcode, int token)
        {
            TypeDesc runtimeDeterminedType = (TypeDesc)_methodIL.GetObject(token);
            TypeDesc type = (TypeDesc)_canonMethodIL.GetObject(token);
            TypeDesc canonType = _writer.ConvertToCanonFormIfNecessary(type, CanonicalFormKind.Specific);

            var value = _stack.Pop();
            PushTemp(StackValueKind.ObjRef, canonType);

            AddTypeReference(canonType, false);

            Append(opcode == ILOpcode.isinst ? "__isinst" : "__castclass");
            Append("(");
            if (runtimeDeterminedType.IsRuntimeDeterminedSubtype)
            {
                Append("(MethodTable *)");
                Append(GetGenericLookupHelperAndAddReference(ReadyToRunHelperId.TypeHandle, runtimeDeterminedType));
                Append("(");
                Append(GetGenericContext());
                Append(")");
            }
            else
            {
                Append(_writer.GetCppTypeName(runtimeDeterminedType));
                Append("::__getMethodTable()");
            }
            Append(", ");
            Append(value);
            Append(")");
            AppendSemicolon();
        }

        private static bool IsTypeName(MethodDesc method, string typeNamespace, string typeName)
        {
            var metadataType = method.OwningType as MetadataType;
            if (metadataType == null)
                return false;
            return metadataType.Namespace == typeNamespace && metadataType.Name == typeName;
        }

        private bool ImportIntrinsicCall(MethodDesc method, MethodDesc runtimeDeterminedMethod)
        {
            Debug.Assert(method.IsIntrinsic);

            switch (method.Name)
            {
                case "InitializeArray":
                    if (IsTypeName(method, "System.Runtime.CompilerServices", "RuntimeHelpers"))
                    {
                        var fieldSlot = (LdTokenEntry<FieldDesc>)_stack.Pop();
                        var arraySlot = _stack.Pop();

                        var fieldDesc = fieldSlot.LdToken;
                        var dataBlob = ((ObjectNode)_compilation.GetFieldRvaData(fieldDesc)).GetData(_compilation.NodeFactory, false);
                        Debug.Assert(dataBlob.Relocs.Length == 0);
                        var memBlock = dataBlob.Data;

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
                        Append(arraySlot);
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
                        var typeHandleSlot = (LdTokenEntry<TypeDesc>)_stack.Pop();
                        TypeDesc typeOfEEType = typeHandleSlot.LdToken;

                        string expr;

                        if (typeOfEEType.IsRuntimeDeterminedSubtype)
                        {
                            expr = string.Concat(
                                "(intptr_t)",
                                GetGenericLookupHelperAndAddReference(ReadyToRunHelperId.TypeHandle, typeOfEEType),
                                "(", GetGenericContext(), ")");
                        }
                        else
                        {
                            expr = string.Concat("((intptr_t)", _writer.GetCppTypeName(typeOfEEType), "::__getMethodTable())");
                        }
                        PushExpression(StackValueKind.NativeInt, expr);
                        return true;
                    }
                    break;
                case ".ctor":
                    if (IsTypeName(method, "System", "ByReference`1"))
                    {
                        var value = _stack.Pop();
                        var byReferenceType = method.OwningType;

                        string tempName = NewTempName();

                        Append(GetStackValueKindCPPTypeName(StackValueKind.ValueType, byReferenceType));
                        Append(" ");
                        Append(tempName);
                        AppendSemicolon();

                        Append(tempName);
                        Append("._value = (intptr_t)");
                        Append(value);
                        AppendSemicolon();

                        PushExpression(StackValueKind.ValueType, tempName, byReferenceType);
                        return true;
                    }
                    break;
                case "get_Value":
                    if (IsTypeName(method, "System", "ByReference`1"))
                    {
                        var thisRef = _stack.Pop();
                        PushExpression(StackValueKind.ByRef,
                            String.Concat("(", GetSignatureTypeNameAndAddReference(method.Signature.ReturnType), ")", ((ExpressionEntry)thisRef).Name, "->_value"),
                            method.Signature.ReturnType);
                        return true;
                    }
                    break;
                case "DefaultConstructorOf":
                    if (IsTypeName(method, "System", "Activator") && method.Instantiation.Length == 1)
                    {
                        string expr;

                        if (runtimeDeterminedMethod.IsRuntimeDeterminedExactMethod)
                        {
                            expr = string.Concat(
                                "(intptr_t)",
                                GetGenericLookupHelperAndAddReference(ReadyToRunHelperId.DefaultConstructor, runtimeDeterminedMethod.Instantiation[0]),
                                "(", GetGenericContext(), ")");
                        }
                        else
                        {
                            IMethodNode methodNode = (IMethodNode)_compilation.ComputeConstantLookup(ReadyToRunHelperId.DefaultConstructor, method.Instantiation[0]);
                            _dependencies.Add(methodNode);

                            MethodDesc ctor =  methodNode.Method;

                            expr = string.Concat(
                                "(intptr_t)&",
                                _writer.GetCppTypeName(ctor.OwningType),
                                "::",
                                _writer.GetCppMethodName(ctor)
                            );
                        }

                        PushExpression(StackValueKind.NativeInt, expr);

                        return true;
                    }
                    break;
                default:
                    break;
            }
            return false;
        }

        private void ImportNewObjArray(TypeDesc owningType, MethodDesc runtimeMethod)
        {
            AppendLine();

            TypeDesc canonOwningType = _writer.ConvertToCanonFormIfNecessary(owningType, CanonicalFormKind.Specific);

            string dimensionsTemp = NewTempName();
            Append("int32_t " + dimensionsTemp + "[] = { ");
            int argumentsCount = runtimeMethod.Signature.Length;
            for (int i = 0; i < argumentsCount; i++)
            {
                Append("(int32_t)(");
                Append(_stack[_stack.Top - argumentsCount + i]);
                Append("),");
            }
            _stack.PopN(argumentsCount);

            Append("};");

            PushTemp(StackValueKind.ObjRef, canonOwningType);

            AddTypeReference(canonOwningType, true);

            MethodDesc helper = _typeSystemContext.GetHelperEntryPoint("ArrayHelpers", "NewObjArray");
            AddMethodReference(helper);

            Append(_writer.GetCppTypeName(helper.OwningType) + "::" + _writer.GetCppMethodName(helper));
            Append("((intptr_t)");

            if (!runtimeMethod.OwningType.IsRuntimeDeterminedSubtype)
            {
                Append(_writer.GetCppTypeName(runtimeMethod.OwningType));
                Append("::__getMethodTable(),");
            }
            else
            {
                Append("(MethodTable *)");
                Append(GetGenericLookupHelperAndAddReference(ReadyToRunHelperId.TypeHandle, runtimeMethod.OwningType));
                Append("(");
                Append(GetGenericContext());
                Append("),");
            }

            Append(argumentsCount.ToStringInvariant());
            Append(",");
            Append(dimensionsTemp);
            Append(")");
            AppendSemicolon();
        }

        private void ImportCall(ILOpcode opcode, int token)
        {
            bool callViaSlot = false;
            bool delegateInvoke = false;
            bool callViaInterfaceDispatch = false;
            bool callViaGVMDispatch = false;
            DelegateCreationInfo delegateInfo = null;

            var runtimeDeterminedMethod = (MethodDesc)_methodIL.GetObject(token);
            var method = (MethodDesc)_canonMethodIL.GetObject(token);

            if (method.IsIntrinsic)
            {
                if (ImportIntrinsicCall(method, runtimeDeterminedMethod))
                    return;

                method = _compilation.ExpandIntrinsicForCallsite(method, _method);
            }

            //this assumes that there will only ever be at most one RawPInvoke call in a given method
            if (method.IsRawPInvoke())
            {
                AppendLine();
                Append("PInvokeTransitionFrame __piframe");
                AppendSemicolon();
                AppendLine();
                Append("__pinvoke(&__piframe)");
                AppendSemicolon();
            }

            TypeDesc constrained = null;
            bool resolvedConstraint = false;
            if (opcode != ILOpcode.newobj)
            {
                if ((_pendingPrefix & Prefix.Constrained) != 0 && opcode == ILOpcode.callvirt)
                {
                    _pendingPrefix &= ~Prefix.Constrained;
                    constrained = _constrained;
                    if (constrained.IsRuntimeDeterminedSubtype)
                        constrained = constrained.ConvertToCanonForm(CanonicalFormKind.Specific);

                    bool forceUseRuntimeLookup;
                    var constrainedType = constrained.GetClosestDefType();
                    MethodDesc directMethod = constrainedType.TryResolveConstraintMethodApprox(method.OwningType, method, out forceUseRuntimeLookup);

                    if (forceUseRuntimeLookup)
                        throw new NotImplementedException();

                    if (directMethod != null)
                    {
                        method = directMethod;
                        opcode = ILOpcode.call;
                        resolvedConstraint = true;
                    }
                    //If constrainedType is a value type and constrainedType does not implement method (directMethod == null) then ptr is
                    //dereferenced, boxed, and passed as the 'this' pointer to the callvirt  method instruction.
                    else if (constrainedType.IsValueType)
                    {
                        int thisPosition = _stack.Top - (method.Signature.Length + 1);
                        _stack[thisPosition] = BoxValue(constrainedType, DereferenceThisPtr(method, constrainedType));
                    }
                    else
                    {
                        DereferenceThisPtr(method);
                    }
                }
            }

            TypeDesc owningType = method.OwningType;

            TypeDesc retType = null;
            TypeDesc runtimeDeterminedRetType = null;

            string delegateCtorHelper = null;

            {
                if (opcode == ILOpcode.newobj)
                {
                    retType = owningType;
                    runtimeDeterminedRetType = runtimeDeterminedMethod.OwningType;

                    if (owningType.IsString)
                    {
                        // String constructors actually look like regular method calls
                        IMethodNode node = _compilation.NodeFactory.StringAllocator(method);
                        _dependencies.Add(node);
                        method = node.Method;
                        opcode = ILOpcode.call;
                    }
                    else if (owningType.IsArray)
                    {
                        ImportNewObjArray(owningType, runtimeDeterminedMethod);
                        return;
                    }
                    else if (owningType.IsDelegate)
                    {
                        TypeDesc canonDelegateType = owningType.ConvertToCanonForm(CanonicalFormKind.Specific);
                        LdFtnTokenEntry ldFtnTokenEntry = (LdFtnTokenEntry)_stack.Peek();
                        delegateInfo = _compilation.GetDelegateCtor(canonDelegateType, ldFtnTokenEntry.LdToken, followVirtualDispatch: false);
                        method = delegateInfo.Constructor.Method;
                        MethodDesc delegateTargetMethod = delegateInfo.TargetMethod;

                        if (delegateInfo.NeedsRuntimeLookup && !ldFtnTokenEntry.IsVirtual)
                        {
                            delegateCtorHelper = GetGenericLookupHelperAndAddReference(ReadyToRunHelperId.DelegateCtor, delegateInfo);
                        }
                        else if (!ldFtnTokenEntry.IsVirtual && delegateTargetMethod.OwningType.IsValueType &&
                                 !delegateTargetMethod.Signature.IsStatic)
                        {
                            var sb = new CppGenerationBuffer();

                            MethodDesc canonDelegateTargetMethod = delegateTargetMethod.GetCanonMethodTarget(CanonicalFormKind.Specific);
                            ISymbolNode targetNode = delegateInfo.GetTargetNode(_nodeFactory);
                            _dependencies.Add(targetNode);

                            sb.Append("(");

                            if (delegateTargetMethod != canonDelegateTargetMethod)
                            {
                                sb.Append("((intptr_t)");
                                sb.Append(_writer.GetCppSymbolNodeName(_nodeFactory, targetNode));
                                sb.Append("()) + ");
                                sb.Append(_typeSystemContext.Target.FatFunctionPointerOffset.ToString());
                            }
                            else
                            {
                                sb.Append("(intptr_t)&");
                                sb.Append(_writer.GetCppTypeName(canonDelegateTargetMethod.OwningType));
                                sb.Append("::");
                                sb.Append(_writer.GetCppSymbolNodeName(_nodeFactory, targetNode));
                            }

                            sb.Append(")");

                            ldFtnTokenEntry.Name = sb.ToString();
                        }
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

            bool exactContextNeedsRuntimeLookup;
            if (method.HasInstantiation)
            {
                exactContextNeedsRuntimeLookup = method.IsSharedByGenericInstantiations;
            }
            else
            {
                exactContextNeedsRuntimeLookup = method.OwningType.IsCanonicalSubtype(CanonicalFormKind.Any);
            }

            if (opcode == ILOpcode.callvirt)
            {
                // TODO: Null checks

                if (method.IsVirtual && !method.IsFinal && !method.OwningType.IsSealed())
                {
                    // TODO: Full resolution of virtual methods
                    if (!method.IsNewSlot)
                        throw new NotImplementedException();

                    if (method.HasInstantiation)
                        callViaGVMDispatch = true;
                    else if (method.OwningType.IsInterface)
                        callViaInterfaceDispatch = true;
                    else
                        callViaSlot = true;

                    if (!callViaGVMDispatch && !_nodeFactory.VTable(method.OwningType).HasFixedSlots)
                        _dependencies.Add(_nodeFactory.VirtualMethodUse(method));
                    else if (callViaGVMDispatch)
                        _dependencies.Add(_nodeFactory.GVMDependencies(method));
                }
            }

            var canonMethod = method.GetCanonMethodTarget(CanonicalFormKind.Specific);

            if (!callViaSlot && !delegateInvoke && !callViaInterfaceDispatch && !callViaGVMDispatch)
                AddMethodReference(canonMethod);

            var canonMethodSignature = canonMethod.Signature;

            if (retType == null)
                retType = method.Signature.ReturnType;

            retType = _writer.ConvertToCanonFormIfNecessary(retType, CanonicalFormKind.Specific);

            if (opcode == ILOpcode.newobj)
                AddTypeReference(retType, true);

            string temp = null;
            StackValueKind retKind = StackValueKind.Unknown;
            var needNewLine = false;
            string gvmSlotVarName = null;

            if (callViaInterfaceDispatch)
            {
                ExpressionEntry v = (ExpressionEntry)_stack[_stack.Top - (canonMethodSignature.Length + 1)];

                string typeDefName = _writer.GetCppTypeName(canonMethod.OwningType) + "_" + _writer.GetCppMethodName(canonMethod);
                typeDefName = typeDefName.Replace("::", "_");
                _writer.AppendSignatureTypeDef(_builder, typeDefName, canonMethodSignature,
                    canonMethod.OwningType, canonMethod.RequiresInstMethodDescArg());

                string functionPtr = NewTempName();
                AppendEmptyLine();

                Append("void*");
                Append(functionPtr);
                Append(" = (void*) ");
                GetFunctionPointerForInterfaceMethod(runtimeDeterminedMethod, v, typeDefName);

                PushExpression(StackValueKind.ByRef, functionPtr);
            }
            else if (callViaGVMDispatch)
            {
                ExpressionEntry v = (ExpressionEntry)_stack[_stack.Top - (canonMethodSignature.Length + 1)];

                MethodDesc helper = _typeSystemContext.SystemModule.GetKnownType("System.Runtime", "TypeLoaderExports").GetKnownMethod("GVMLookupForSlot", null);
                AddMethodReference(helper);

                gvmSlotVarName = NewTempName();
                AppendEmptyLine();

                Append("intptr_t ");
                Append(gvmSlotVarName);
                Append(" = ");
                Append(_writer.GetCppTypeName(helper.OwningType) + "::" + _writer.GetCppMethodName(helper));
                Append("(");
                Append("(::System_Private_CoreLib::System::Object*)");
                Append(v.Name);
                Append(", ");

                if (exactContextNeedsRuntimeLookup)
                {
                    Append(GetGenericLookupHelperAndAddReference(ReadyToRunHelperId.MethodHandle, runtimeDeterminedMethod));
                    Append("(");
                    Append(GetGenericContext());
                    Append(")");
                }
                else
                {
                    AppendRuntimeMethodHandle(runtimeDeterminedMethod);
                    Append("()");
                }
                Append(");");

                string functionPtr = NewTempName();

                Append("intptr_t ");
                Append(functionPtr);
                AppendSemicolon();

                Append("if (");
                Append(gvmSlotVarName);
                Append(" & ");
                Append(_typeSystemContext.Target.FatFunctionPointerOffset.ToString());
                Append(") {");
                Append(functionPtr);
                Append(" = *(intptr_t*)(");
                Append(gvmSlotVarName);
                Append(" - ");
                Append(_typeSystemContext.Target.FatFunctionPointerOffset.ToString());
                Append(");} else {");
                Append(functionPtr);
                Append(" = ");
                Append(gvmSlotVarName);
                Append(";};");

                PushExpression(StackValueKind.ValueType, functionPtr);
            }

            string arrayAddressMethodHiddenArg = null;

            if (canonMethod.IsArrayAddressMethod())
            {
                arrayAddressMethodHiddenArg = NewTempName();

                Append("::System_Private_CoreLib::System::EETypePtr ");
                Append(arrayAddressMethodHiddenArg);
                Append(" = {(::System_Private_CoreLib::Internal::Runtime::EEType*)");

                TypeDesc type;

                if (!resolvedConstraint)
                    type = runtimeDeterminedMethod.OwningType;
                else
                    type = _constrained;

                if (exactContextNeedsRuntimeLookup)
                {
                    Append(GetGenericLookupHelperAndAddReference(ReadyToRunHelperId.TypeHandle, type));

                    Append("(");
                    Append(GetGenericContext());
                    Append(")");
                }
                else
                {
                    Append(_writer.GetCppTypeName(type));
                    Append("::__getMethodTable()");

                    AddTypeReference(type, true);
                }

                Append("};");
            }

            TypeDesc canonRetType = null;

            if (!retType.IsVoid)
            {
                if (opcode == ILOpcode.newobj)
                {
                    canonRetType = retType;
                }
                else
                {
                    canonRetType = _writer.ConvertToCanonFormIfNecessary(canonMethodSignature.ReturnType, CanonicalFormKind.Specific);
                }

                retKind = GetStackValueKind(canonRetType);
                temp = NewTempName();

                AppendLine();
                Append(GetStackValueKindCPPTypeName(retKind, canonRetType));
                Append(" ");
                Append(temp);
                if (canonRetType.IsValueType && opcode == ILOpcode.newobj || callViaGVMDispatch)
                {
                    AppendSemicolon();

                    if (!callViaGVMDispatch)
                        needNewLine = true;
                }
                else
                {
                    Append(" = ");

                    if (canonRetType.IsPointer)
                    {
                        Append("(intptr_t)");
                    }
                    else
                    {
                        AppendCastIfNecessary(retKind, canonRetType);
                    }
                }
            }
            else
            {
                needNewLine = true;
            }
            AddTypeReference(canonMethod.OwningType, true);

            if (opcode == ILOpcode.newobj)
            {
                if (!retType.IsValueType)
                {
                    // We do not reset needNewLine since we still need for the next statement.
                    if (needNewLine)
                        AppendLine();
                    Append("__allocate_object(");

                    if (runtimeDeterminedRetType.IsRuntimeDeterminedSubtype)
                    {
                        Append("(MethodTable *)");
                        Append(GetGenericLookupHelperAndAddReference(ReadyToRunHelperId.TypeHandle, runtimeDeterminedRetType));
                        Append("(");
                        Append(GetGenericContext());
                        Append("))");
                    }
                    else
                    {
                        Append(_writer.GetCppTypeName(runtimeDeterminedRetType));
                        Append("::__getMethodTable())");

                        AddTypeReference(runtimeDeterminedRetType, true);
                    }

                    AppendSemicolon();
                    needNewLine = true;

                    if (delegateInfo != null && delegateInfo.Thunk != null)
                    {
                        MethodDesc thunkMethod = delegateInfo.Thunk.Method;
                        AddMethodReference(thunkMethod);

                        var sb = new CppGenerationBuffer();
                        AppendLine();
                        sb.Append("(intptr_t)&");
                        sb.Append(_writer.GetCppTypeName(thunkMethod.OwningType));
                        sb.Append("::");
                        sb.Append(_writer.GetCppMethodName(thunkMethod));

                        PushExpression(StackValueKind.NativeInt, sb.ToString());
                    }
                }
            }

            if (needNewLine)
                AppendLine();

            if (callViaSlot || delegateInvoke)
            {
                ExpressionEntry v = (ExpressionEntry)_stack[_stack.Top - (canonMethodSignature.Length + 1)];

                Append("(*");
                Append(_writer.GetCppTypeName(canonMethod.OwningType));
                Append("::");
                Append(delegateInvoke ? "__invoke__" : "__getslot__");
                Append(_writer.GetCppMethodName(canonMethod));
                Append("(");
                Append(v);
                Append("))");

                if (delegateInvoke)
                {
                    v.Name =
                        "((" + _writer.GetCppSignatureTypeName(GetWellKnownType(WellKnownType.MulticastDelegate)) + ")" +
                            v.Name + ")->m_firstParameter";
                }
            }
            else if (callViaInterfaceDispatch)
            {
                Append("((");
                Append(_writer.GetCppTypeName(canonMethod.OwningType).Replace("::", "_"));
                Append("_");
                Append(_writer.GetCppMethodName(canonMethod));
                Append(")");
                ExpressionEntry v = (ExpressionEntry)_stack.Pop();
                Append(v);
                Append(")");
            }
            else if (delegateCtorHelper != null)
            {
                Append(delegateCtorHelper);
            }
            else if (!callViaGVMDispatch)
            {
                Append(_writer.GetCppTypeName(canonMethod.OwningType));
                Append("::");
                Append(_writer.GetCppMethodName(canonMethod));
            }

            TypeDesc thisArgument = null;
            if (opcode != ILOpcode.newobj && !canonMethodSignature.IsStatic)
            {
                thisArgument = canonMethod.OwningType;
                if (thisArgument.IsValueType)
                    thisArgument = thisArgument.MakeByRefType();
            }

            if (callViaGVMDispatch)
            {
                string typeDefName = _writer.GetCppTypeName(canonMethod.OwningType) + "_" + _writer.GetCppMethodName(canonMethod);
                typeDefName = typeDefName.Replace("::", "_");

                ExpressionEntry v = (ExpressionEntry)_stack.Pop();

                Append("if (");
                Append(gvmSlotVarName);
                Append(" & ");
                Append(_typeSystemContext.Target.FatFunctionPointerOffset.ToString());
                Append(") {");

                _writer.AppendSignatureTypeDef(_builder, typeDefName, canonMethodSignature,
                    canonMethod.OwningType, true);

                if (canonRetType != null && !canonRetType.IsValueType)
                {
                    Append(temp);
                    Append(" = ");

                    if (canonRetType.IsPointer)
                    {
                        Append("(intptr_t)");
                    }
                    else
                    {
                        AppendCastIfNecessary(GetStackValueKind(canonRetType), canonRetType);
                    }
                }

                Append("((");
                Append(_writer.GetCppTypeName(canonMethod.OwningType).Replace("::", "_"));
                Append("_");
                Append(_writer.GetCppMethodName(canonMethod));
                Append(")");
                Append(v);
                Append(")(");

                PassThisArgumentIfNeeded(canonMethodSignature, thisArgument);

                if (thisArgument != null)
                    Append(", ");

                Append("**(void***)(");
                Append(gvmSlotVarName);
                Append(" - ");
                Append(_typeSystemContext.Target.FatFunctionPointerOffset.ToString());
                Append(" + sizeof(void*))");

                if (canonMethodSignature.Length > 0)
                    Append(", ");

                PassCallArguments(canonMethodSignature, thisArgument, false);

                Append(");} else {");

                _writer.AppendSignatureTypeDef(_builder, typeDefName, canonMethodSignature,
                    canonMethod.OwningType, false);

                if (canonRetType != null && !canonRetType.IsValueType)
                {
                    Append(temp);
                    Append(" = ");

                    if (canonRetType.IsPointer)
                    {
                        Append("(intptr_t)");
                    }
                    else
                    {
                        AppendCastIfNecessary(GetStackValueKind(canonRetType), canonRetType);
                    }
                }

                Append("((");
                Append(_writer.GetCppTypeName(canonMethod.OwningType).Replace("::", "_"));
                Append("_");
                Append(_writer.GetCppMethodName(canonMethod));
                Append(")");
                Append(v);
                Append(")(");

                PassThisArgumentIfNeeded(canonMethodSignature, thisArgument);

                if (thisArgument != null && canonMethodSignature.Length > 0)
                    Append(", ");

                PassCallArguments(canonMethodSignature, thisArgument);

                Append(");};");
            }
            else
            {
                Append("(");

                if (opcode == ILOpcode.newobj)
                {
                    if (delegateCtorHelper != null)
                    {
                        Append(GetGenericContext());
                        Append(", ");
                    }

                    canonRetType = _writer.ConvertToCanonFormIfNecessary(canonMethod.OwningType, CanonicalFormKind.Specific);

                    Append("(");
                    if (canonRetType.IsValueType)
                    {
                        Append(_writer.GetCppSignatureTypeName(canonRetType.MakeByRefType()));
                        Append(")");
                        Append("&" + temp);
                    }
                    else
                    {
                        Append(_writer.GetCppSignatureTypeName(canonRetType));
                        Append(")");
                        Append(temp);
                    }
                    if (canonMethodSignature.Length > 0)
                        Append(", ");
                }

                PassThisArgumentIfNeeded(canonMethodSignature, thisArgument);

                if (thisArgument != null &&
                    (canonMethod.IsArrayAddressMethod() || canonMethod.RequiresInstArg() || canonMethodSignature.Length > 0))
                    Append(", ");

                if (canonMethod.IsArrayAddressMethod())
                {
                    Append(arrayAddressMethodHiddenArg);

                    if (canonMethodSignature.Length > 0)
                        Append(", ");
                }
                else if (canonMethod.RequiresInstArg())
                {
                    if (exactContextNeedsRuntimeLookup)
                    {
                        if (!resolvedConstraint)
                        {
                            if (canonMethod.RequiresInstMethodDescArg())
                            {
                                Append(GetGenericLookupHelperAndAddReference(ReadyToRunHelperId.MethodDictionary, runtimeDeterminedMethod));
                            }
                            else
                            {
                                Append(GetGenericLookupHelperAndAddReference(ReadyToRunHelperId.TypeHandle, runtimeDeterminedMethod.OwningType));
                            }

                            Append("(");
                            Append(GetGenericContext());
                            Append(")");
                        }
                        else
                        {
                            Debug.Assert(canonMethod.RequiresInstMethodTableArg());

                            if (canonMethod.RequiresInstMethodTableArg())
                            {
                                if (_constrained.IsRuntimeDeterminedSubtype)
                                {
                                    Append(GetGenericLookupHelperAndAddReference(ReadyToRunHelperId.TypeHandle, _constrained));

                                    Append("(");
                                    Append(GetGenericContext());
                                    Append(")");
                                }
                                else
                                {
                                    Append(_writer.GetCppTypeName(_constrained));
                                    Append("::__getMethodTable()");

                                    AddTypeReference(_constrained, true);
                                }
                            }
                        }
                    }
                    else
                    {
                        if (canonMethod.RequiresInstMethodDescArg())
                        {
                            Append("(char*)&");
                            AppendMethodGenericDictionary(method);
                            Append(" + sizeof(void*)");
                        }
                        else
                        {
                            Append(_writer.GetCppTypeName(method.OwningType));
                            Append("::__getMethodTable()");

                            AddTypeReference(method.OwningType, true);
                        }
                    }

                    if (canonMethodSignature.Length > 0)
                        Append(", ");
                }

                PassCallArguments(canonMethodSignature, thisArgument);
                Append(")");
            }

            if (temp != null)
            {
                Debug.Assert(retKind != StackValueKind.Unknown, "Valid return type");

                if (opcode != ILOpcode.newobj &&
                    retType != _writer.ConvertToCanonFormIfNecessary(canonMethodSignature.ReturnType, CanonicalFormKind.Specific))
                {
                    string retVar = temp;
                    retKind = GetStackValueKind(retType);
                    temp = NewTempName();

                    AppendSemicolon();
                    Append(GetStackValueKindCPPTypeName(retKind, retType));
                    Append(" ");
                    Append(temp);
                    Append(" = ");

                    if (retType.IsValueType && !retType.IsPrimitive)
                    {
                        Append("*(");
                        Append(_writer.GetCppSignatureTypeName(retType));
                        Append("*)&");
                    }
                    else
                    {
                        Append("(");
                        Append(_writer.GetCppSignatureTypeName(retType));
                        Append(")");
                    }
                    Append(retVar);
                }

                PushExpression(retKind, temp, retType);
            }
            AppendSemicolon();

            if (method.IsRawPInvoke())
            {
                AppendLine();
                Append("__pinvoke_return(&__piframe)");
                AppendSemicolon();
            }
        }

        private ExpressionEntry DereferenceThisPtr(MethodDesc method, TypeDesc type = null)
        {
            // Dereference "this"
            int thisPosition = _stack.Top - (method.Signature.Length + 1);
            string tempName = NewTempName();

            StackValueKind valueKind = StackValueKind.ObjRef;
            if (type != null && type.IsValueType)
                valueKind = StackValueKind.ValueType;

            Append(GetStackValueKindCPPTypeName(valueKind, type));
            Append(" ");
            Append(tempName);
            Append(" = *(");
            Append(GetStackValueKindCPPTypeName(valueKind, type));
            Append("*)");
            Append(_stack[thisPosition]);
            AppendSemicolon();
            var result = new ExpressionEntry(valueKind, tempName, type);
            _stack[thisPosition] = result;
            return result;
        }

        private void GetFunctionPointerForInterfaceMethod(MethodDesc method, ExpressionEntry v, string typeDefName)
        {
            Append("((");
            Append(typeDefName);
            // Call method to find implementation address
            Append(") System_Private_CoreLib::System::Runtime::DispatchResolve::FindInterfaceMethodImplementationTarget(");

            // Get EEType of current object (interface implementation)
            Append("::System_Private_CoreLib::System::Object::get_EEType((::System_Private_CoreLib::System::Object*)");
            Append(v.Name);
            Append(")");

            Append(", ");

            // Get EEType of interface
            Append("((::System_Private_CoreLib::Internal::Runtime::EEType *)(");

            if (method.OwningType.IsRuntimeDeterminedSubtype)
            {
                Append(GetGenericLookupHelperAndAddReference(ReadyToRunHelperId.TypeHandle, method.OwningType));
                Append("(");
                Append(GetGenericContext());
                Append(")))");
            }
            else
            {
                Append(_writer.GetCppTypeName(method.OwningType));
                Append("::__getMethodTable()))");
            }

            Append(", ");

            MethodDesc canonMethod = method.GetCanonMethodTarget(CanonicalFormKind.Specific);

            // Get slot of implementation
            Append("(uint16_t)");
            Append("(");
            Append(_writer.GetCppTypeName(canonMethod.OwningType));
            Append("::");
            Append("__getslot__");
            Append(_writer.GetCppMethodName(canonMethod));
            Append("(");
            Append(v.Name);
            Append("))");

            Append("));");
        }

        private void PassThisArgumentIfNeeded(MethodSignature methodSignature, TypeDesc thisArgument)
        {
            if (thisArgument == null)
                return;

            int signatureLength = methodSignature.Length;
            int argumentsCount = (thisArgument != null) ? (signatureLength + 1) : signatureLength;
            int thisIndex = _stack.Top - argumentsCount;

            var op = _stack[thisIndex];
            AppendCastIfNecessary(_writer.ConvertToCanonFormIfNecessary(thisArgument, CanonicalFormKind.Specific), op);
            Append(op);
        }

        private void PassCallArguments(MethodSignature methodSignature, TypeDesc thisArgument, bool clearStack = true)
        {
            int signatureLength = methodSignature.Length;
            int argumentsCount = (thisArgument != null) ? (signatureLength + 1) : signatureLength;
            int startingIndex = _stack.Top - signatureLength;

            for (int i = 0; i < signatureLength; i++)
            {
                var op = _stack[startingIndex + i];

                AppendCastIfNecessary(_writer.ConvertToCanonFormIfNecessary(methodSignature[i], CanonicalFormKind.Specific), op);
                Append(op);

                if (i != signatureLength - 1)
                    Append(", ");
            }

            if (clearStack)
                _stack.PopN(argumentsCount);
        }

        private void ImportCalli(int token)
        {
            MethodSignature methodSignature = (MethodSignature)_canonMethodIL.GetObject(token);

            TypeDesc thisArgument = null;
            if (!methodSignature.IsStatic)
            {
                thisArgument = GetWellKnownType(WellKnownType.Object);
                if (thisArgument.IsValueType)
                    thisArgument = thisArgument.MakeByRefType();
            }

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
                AppendSemicolon();
            }
            else
            {
                AppendLine();
            }

            var fnPtrValue = _stack.Pop();

            string fatPtr = NewTempName();

            Append("intptr_t ");
            Append(fatPtr);
            Append(" = ");
            Append(fnPtrValue);

            AppendSemicolon();

            Append("if (");
            Append(fatPtr);
            Append(" & ");
            Append(_typeSystemContext.Target.FatFunctionPointerOffset.ToString());
            Append(") {");
            Append(fnPtrValue);
            Append(" = *(intptr_t*)(");
            Append(fatPtr);
            Append(" - ");
            Append(_typeSystemContext.Target.FatFunctionPointerOffset.ToString());
            Append(")");

            AppendSemicolon();

            string typeDefName = "__calli__" + token.ToStringInvariant("x8");
            _writer.AppendSignatureTypeDef(_builder, typeDefName, methodSignature, thisArgument, true);

            if (!retType.IsVoid)
            {
                Append(temp);
                Append(" = ");

                if (retType.IsPointer)
                {
                    Append("(intptr_t)");
                }
            }

            Append("((");
            Append(typeDefName);
            Append(")");
            Append(fnPtrValue);
            Append(")(");

            PassThisArgumentIfNeeded(methodSignature, thisArgument);

            if (thisArgument != null)
                Append(", ");

            Append("**(void***)(");
            Append(fatPtr);
            Append(" - ");
            Append(_typeSystemContext.Target.FatFunctionPointerOffset.ToString());
            Append(" + sizeof(void*))");

            if (methodSignature.Length > 0)
                Append(", ");

            PassCallArguments(methodSignature, thisArgument, false);
            Append(")");

            AppendSemicolon();

            Append("} else {");

            _writer.AppendSignatureTypeDef(_builder, typeDefName, methodSignature, thisArgument, false);

            if (!retType.IsVoid)
            {
                Append(temp);
                Append(" = ");

                if (retType.IsPointer)
                {
                    Append("(intptr_t)");
                }
            }

            Append("((");
            Append(typeDefName);
            Append(")");
            Append(fnPtrValue);
            Append(")(");
            PassThisArgumentIfNeeded(methodSignature, thisArgument);

            if (thisArgument != null && methodSignature.Length > 0)
                Append(", ");

            PassCallArguments(methodSignature, thisArgument);
            Append(")");

            AppendSemicolon();
            Append("}");

            AppendSemicolon();

            if (temp != null)
            {
                Debug.Assert(retKind != StackValueKind.Unknown, "Valid return type");
                PushExpression(retKind, temp, retType);
            }
        }

        private void ImportLdFtn(int token, ILOpcode opCode)
        {
            MethodDesc runtimeDeterminedMethod = (MethodDesc)_methodIL.GetObject(token);
            MethodDesc method = ((MethodDesc)_canonMethodIL.GetObject(token));
            MethodDesc canonMethod = method.GetCanonMethodTarget(CanonicalFormKind.Specific);

            if (opCode == ILOpcode.ldvirtftn && canonMethod.IsVirtual && !canonMethod.HasInstantiation && canonMethod.OwningType.IsInterface)
            {
                AddVirtualMethodReference(canonMethod);
                var entry = new LdFtnTokenEntry(StackValueKind.NativeInt, NewTempName(), runtimeDeterminedMethod, true);
                ExpressionEntry v = (ExpressionEntry)_stack.Pop();
                string typeDefName = _writer.GetCppTypeName(canonMethod.OwningType) + "_" + _writer.GetCppMethodName(canonMethod);
                typeDefName = typeDefName.Replace("::", "_");
                _writer.AppendSignatureTypeDef(_builder, typeDefName, canonMethod.Signature, canonMethod.OwningType);

                AppendEmptyLine();

                PushTemp(entry);
                Append("(intptr_t) ");
                GetFunctionPointerForInterfaceMethod(runtimeDeterminedMethod, v, typeDefName);
            }
            else
            {
                bool isVirtual = opCode == ILOpcode.ldvirtftn && canonMethod.IsVirtual && !canonMethod.IsFinal && !canonMethod.OwningType.IsSealed();
                var entry = new LdFtnTokenEntry(StackValueKind.NativeInt, NewTempName(), runtimeDeterminedMethod, isVirtual);

                if (isVirtual)
                {
                    //ldvirtftn requires an object instance, we have to pop one off the stack
                    //then call the associated getslot method passing in the object instance to get the real function pointer
                    ExpressionEntry v = (ExpressionEntry)_stack.Pop();

                    PushTemp(entry);
                    Append("(intptr_t)");

                    if (!canonMethod.HasInstantiation)
                    {
                        Append(_writer.GetCppTypeName(canonMethod.OwningType));
                        Append("::__getslot__");
                        Append(_writer.GetCppMethodName(canonMethod));
                        Append("(");
                        Append(v.Name);
                        Append(")");
                    }
                    else
                    {
                        MethodDesc helper = _typeSystemContext.SystemModule.GetKnownType("System.Runtime", "TypeLoaderExports").GetKnownMethod("GVMLookupForSlot", null);
                        AddMethodReference(helper);

                        Append(_writer.GetCppTypeName(helper.OwningType) + "::" + _writer.GetCppMethodName(helper));
                        Append("(");
                        Append("(::System_Private_CoreLib::System::Object*)");
                        Append(v.Name);
                        Append(", ");

                        if (method.IsSharedByGenericInstantiations)
                        {
                            Append(GetGenericLookupHelperAndAddReference(ReadyToRunHelperId.MethodHandle, runtimeDeterminedMethod));
                            Append("(");
                            Append(GetGenericContext());
                            Append(")");
                        }
                        else
                        {
                            AppendRuntimeMethodHandle(runtimeDeterminedMethod);
                            Append("()");
                        }
                        Append(")");
                    }
                    AppendSemicolon();

                    if (!canonMethod.HasInstantiation && !_nodeFactory.VTable(canonMethod.OwningType).HasFixedSlots)
                        _dependencies.Add(_nodeFactory.VirtualMethodUse(canonMethod));
                    else if (canonMethod.HasInstantiation)
                        _dependencies.Add(_nodeFactory.GVMDependencies(canonMethod));
                }
                else
                {
                    // pop object reference off the stack
                    if (opCode == ILOpcode.ldvirtftn)
                        _stack.Pop();

                    bool exactContextNeedsRuntimeLookup;
                    if (method.HasInstantiation)
                    {
                        exactContextNeedsRuntimeLookup = method.IsSharedByGenericInstantiations;
                    }
                    else
                    {
                        exactContextNeedsRuntimeLookup = method.OwningType.IsCanonicalSubtype(CanonicalFormKind.Any);
                    }

                    PushTemp(entry);

                    if (canonMethod.IsSharedByGenericInstantiations && (canonMethod.HasInstantiation || canonMethod.Signature.IsStatic))
                    {
                        if (exactContextNeedsRuntimeLookup)
                        {
                            Append("((intptr_t)");
                            Append(GetGenericLookupHelperAndAddReference(ReadyToRunHelperId.MethodEntry, runtimeDeterminedMethod));
                            Append("(");
                            Append(GetGenericContext());
                            Append(")) + ");
                            Append(_typeSystemContext.Target.FatFunctionPointerOffset.ToString());
                        }
                        else
                        {
                            Append("((intptr_t)");
                            AppendFatFunctionPointer(runtimeDeterminedMethod);
                            Append("()) + ");
                            Append(_typeSystemContext.Target.FatFunctionPointerOffset.ToString());
                        }
                    }
                    else
                    {
                        Append("(intptr_t)&");
                        Append(_writer.GetCppTypeName(canonMethod.OwningType));
                        Append("::");
                        AppendMethodAndAddReference(canonMethod);
                    }

                    AppendSemicolon();
                }
            }
        }

        private void ImportLoadInt(long value, StackValueKind kind)
        {
            if (kind == StackValueKind.Int64)
            {
                _stack.Push(new Int64ConstantEntry(value));
            }
            else
            {
                Debug.Assert(value >= Int32.MinValue && value <= Int32.MaxValue, "Value too large for an Int32.");
                _stack.Push(new Int32ConstantEntry(checked((int)value)));
            }
        }

        private void ImportLoadFloat(double value)
        {
            _stack.Push(new FloatConstantEntry(value));
        }

        private void ImportLoadNull()
        {
            PushExpression(StackValueKind.ObjRef, "0");
        }

        private void ImportReturn()
        {
            if (_method.IsUnmanagedCallersOnly)
            {
                AppendLine();
                Append("__reverse_pinvoke_return(&__frame)");
                AppendSemicolon();
            }

            var returnType = _methodSignature.ReturnType;
            AppendLine();
            if (returnType.IsVoid)
            {
                Append("return");
            }
            else
            {
                var value = _stack.Pop();
                Append("return ");
                AppendCastIfNecessary(returnType, value);
                Append(value);
            }
            AppendSemicolon();
        }

        private void ImportFallthrough(BasicBlock next)
        {
            EvaluationStack<StackEntry> entryStack = next.EntryStack;

            if (entryStack != null)
            {
                if (entryStack.Length != _stack.Length)
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
                if (_stack.Length > 0)
                {
                    entryStack = new EvaluationStack<StackEntry>(_stack.Length);

                    for (int i = 0; i < _stack.Length; i++)
                    {
                        entryStack.Push(NewSpillSlot(_stack[i]));
                    }
                }
                next.EntryStack = entryStack;
            }

            if (entryStack != null)
            {
                for (int i = 0; i < entryStack.Length; i++)
                {
                    AppendLine();
                    Append(entryStack[i]);
                    Append(" = ");
                    Append(_stack[i]);
                    AppendSemicolon();
                }
            }

            MarkBasicBlock(next);
        }

        private void ImportSwitchJump(int jmpBase, int[] jmpDelta, BasicBlock fallthrough)
        {
            var op = _stack.Pop();

            AppendLine();
            Append("switch (");
            Append(op);
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
                    var op = _stack.Pop();
                    Append(op);
                    Append((opcode == ILOpcode.brtrue) ? " != 0" : " == 0");
                }
                else
                {
                    var op1 = _stack.Pop();
                    var op2 = _stack.Pop();

                    AppendComparison(opcode, op1, op2);
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
            var op1 = _stack.Pop();
            var op2 = _stack.Pop();

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

                default: Debug.Fail("Unexpected opcode"); break;
            }

            if (kind == StackValueKind.ByRef)
            {
                Append("(");
                Append(GetStackValueKindCPPTypeName(kind, type));
                Append(")(");
            }

            if (op2.Kind == StackValueKind.ByRef)
            {
                Append("(");
                Append(GetStackValueKindCPPTypeName(StackValueKind.NativeInt));
                Append(")");
            }
            else
            if (unsigned)
            {
                Append("(u");
                Append(GetStackValueKindCPPTypeName(kind));
                Append(")");
            }
            Append(op2);
            Append(" ");
            Append(op);
            Append(" ");
            if (op1.Kind == StackValueKind.ByRef)
            {
                Append("(");
                Append(GetStackValueKindCPPTypeName(StackValueKind.NativeInt));
                Append(")");
            }
            else
            if (unsigned)
            {
                Append("(u");
                Append(GetStackValueKindCPPTypeName(kind));
                Append(")");
            }
            Append(op1);

            if (kind == StackValueKind.ByRef)
            {
                Append(")");
            }

            AppendSemicolon();
        }

        private void ImportShiftOperation(ILOpcode opcode)
        {
            var shiftAmount = _stack.Pop();
            var op = _stack.Pop();

            PushTemp(op.Kind, op.Type);

            if (opcode == ILOpcode.shr_un)
            {
                Append("(u");
                Append(GetStackValueKindCPPTypeName(op.Kind));
                Append(")");
            }
            Append(op);

            Append((opcode == ILOpcode.shl) ? " << " : " >> ");

            Append(shiftAmount);
            AppendSemicolon();
        }

        private void ImportCompareOperation(ILOpcode opcode)
        {
            var op1 = _stack.Pop();
            var op2 = _stack.Pop();

            PushTemp(StackValueKind.Int32);

            AppendComparison(opcode, op1, op2);
            AppendSemicolon();
        }

        private void ImportConvert(WellKnownType wellKnownType, bool checkOverflow, bool unsigned)
        {
            var op = _stack.Pop();

            TypeDesc type = GetWellKnownType(wellKnownType);

            PushTemp(GetStackValueKind(type));
            Append("(");
            Append(GetSignatureTypeNameAndAddReference(type));
            Append(")");
            Append(op);
            AppendSemicolon();
        }

        private void ImportLoadField(int token, bool isStatic)
        {
            FieldDesc runtimeDeterminedField = (FieldDesc)_methodIL.GetObject(token);
            FieldDesc field = (FieldDesc)_canonMethodIL.GetObject(token);

            var thisPtr = isStatic ? InvalidEntry.Entry : _stack.Pop();

            TypeDesc runtimeDeterminedOwningType = runtimeDeterminedField.OwningType;

            TypeDesc owningType = _writer.ConvertToCanonFormIfNecessary(field.OwningType, CanonicalFormKind.Specific);
            TypeDesc fieldType = _writer.ConvertToCanonFormIfNecessary(field.FieldType, CanonicalFormKind.Specific);

            // TODO: Is this valid combination?
            if (!isStatic && !owningType.IsValueType && thisPtr.Kind != StackValueKind.ObjRef)
                throw new InvalidProgramException();

            if (!runtimeDeterminedOwningType.IsRuntimeDeterminedSubtype && field.IsStatic)
                TriggerCctor(runtimeDeterminedField.OwningType);

            StackValueKind kind = GetStackValueKind(fieldType);
            PushTemp(kind, fieldType);
            AppendCastIfNecessary(kind, fieldType);

            if (runtimeDeterminedOwningType.IsRuntimeDeterminedSubtype && field.IsStatic)
            {
                AddTypeReference(fieldType, false);

                if (fieldType.IsValueType)
                {
                    Append("*(");
                    Append(_writer.GetCppSignatureTypeName(fieldType));
                    Append("*)&(");
                }

                Append("(((");
                Append(_writer.GetCppStaticsTypeName(owningType, field.HasGCStaticBase, field.IsThreadStatic));
                Append("*)");
                AppendStaticFieldGenericLookupHelperAndAddReference(runtimeDeterminedField);
                Append("(");
                Append(GetGenericContext());
                Append("))->");
                Append(_writer.GetCppFieldName(field));
                Append(")");

                if (fieldType.IsValueType)
                    Append(")");
            }
            else
            {
                AddFieldReference(field);

                if (field.IsStatic)
                {
                    Append(_writer.GetCppStaticsName(owningType, field.HasGCStaticBase, field.IsThreadStatic, true));
                    Append(".");
                    Append(_writer.GetCppFieldName(field));
                }
                else
                if (thisPtr.Kind == StackValueKind.ValueType)
                {
                    Append(thisPtr);
                    Append(".");
                    Append(_writer.GetCppFieldName(field));
                }
                else
                {
                    Append("((");
                    Append(_writer.GetCppTypeName(owningType));
                    Append("*)");
                    Append(thisPtr);
                    Append(")->");
                    Append(_writer.GetCppFieldName(field));

                    GetSignatureTypeNameAndAddReference(owningType);
                }
            }

            AppendSemicolon();
        }

        private void ImportAddressOfField(int token, bool isStatic)
        {
            FieldDesc runtimeDeterminedField = (FieldDesc)_methodIL.GetObject(token);
            FieldDesc field = (FieldDesc)_canonMethodIL.GetObject(token);

            var thisPtr = isStatic ? InvalidEntry.Entry : _stack.Pop();

            TypeDesc runtimeDeterminedOwningType = runtimeDeterminedField.OwningType;

            TypeDesc owningType = _writer.ConvertToCanonFormIfNecessary(field.OwningType, CanonicalFormKind.Specific);
            TypeDesc fieldType = _writer.ConvertToCanonFormIfNecessary(field.FieldType, CanonicalFormKind.Specific);

            // TODO: Is this valid combination?
            if (!isStatic && !owningType.IsValueType && thisPtr.Kind != StackValueKind.ObjRef)
                throw new InvalidProgramException();

            if (!runtimeDeterminedOwningType.IsRuntimeDeterminedSubtype && field.IsStatic)
                TriggerCctor(runtimeDeterminedField.OwningType);

            TypeDesc addressType = fieldType.MakeByRefType();
            StackValueKind kind = GetStackValueKind(addressType);
            PushTemp(kind, addressType);

            if (runtimeDeterminedOwningType.IsRuntimeDeterminedSubtype && field.IsStatic)
            {
                AddTypeReference(fieldType, false);

                AppendCastIfNecessary(kind, addressType);

                Append("&");

                Append("(((");
                Append(_writer.GetCppStaticsTypeName(owningType, field.HasGCStaticBase, field.IsThreadStatic));
                Append("*)");
                AppendStaticFieldGenericLookupHelperAndAddReference(runtimeDeterminedField);
                Append("(");
                Append(GetGenericContext());
                Append("))->");
                Append(_writer.GetCppFieldName(field));
                Append(")");
            }
            else
            {
                AddFieldReference(field);

                AppendCastIfNecessary(kind, addressType);

                Append("&");

                if (field.IsStatic)
                {
                    Append(_writer.GetCppStaticsName(owningType, field.HasGCStaticBase, field.IsThreadStatic, true));
                    Append(".");
                    Append(_writer.GetCppFieldName(field));
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
                    Append(thisPtr);
                    Append(")->");
                    Append(_writer.GetCppFieldName(field));

                    GetSignatureTypeNameAndAddReference(owningType);
                }
            }

            AppendSemicolon();
        }


        private void ImportStoreField(int token, bool isStatic)
        {
            FieldDesc runtimeDeterminedField = (FieldDesc)_methodIL.GetObject(token);
            FieldDesc field = (FieldDesc)_canonMethodIL.GetObject(token);

            var value = _stack.Pop();
            var thisPtr = isStatic ? InvalidEntry.Entry : _stack.Pop();

            TypeDesc runtimeDeterminedOwningType = runtimeDeterminedField.OwningType;

            TypeDesc owningType = _writer.ConvertToCanonFormIfNecessary(field.OwningType, CanonicalFormKind.Specific);
            TypeDesc fieldType = _writer.ConvertToCanonFormIfNecessary(field.FieldType, CanonicalFormKind.Specific);

            // TODO: Is this valid combination?
            if (!isStatic && !owningType.IsValueType && thisPtr.Kind != StackValueKind.ObjRef)
                throw new InvalidProgramException();

            if (!runtimeDeterminedOwningType.IsRuntimeDeterminedSubtype && field.IsStatic)
                TriggerCctor(runtimeDeterminedField.OwningType);

            if (runtimeDeterminedOwningType.IsRuntimeDeterminedSubtype && field.IsStatic)
            {
                Append("*(");
                Append(_writer.GetCppSignatureTypeName(fieldType));
                Append("*)&(");

                Append("(((");
                Append(_writer.GetCppStaticsTypeName(owningType, field.HasGCStaticBase, field.IsThreadStatic));
                Append("*)");
                AppendStaticFieldGenericLookupHelperAndAddReference(runtimeDeterminedField);
                Append("(");
                Append(GetGenericContext());
                Append("))->");
                Append(_writer.GetCppFieldName(field));
                Append(")");

                Append(")");
            }
            else
            {
                AddFieldReference(field);

                // TODO: Write barrier as necessary!!!

                AppendLine();
                if (field.IsStatic)
                {
                    Append(_writer.GetCppStaticsName(owningType, field.HasGCStaticBase, field.IsThreadStatic, true));
                    Append(".");
                    Append(_writer.GetCppFieldName(field));
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
                    Append(thisPtr);
                    Append(")->");
                    Append(_writer.GetCppFieldName(field));

                    GetSignatureTypeNameAndAddReference(owningType);
                }
            }

            Append(" = ");
            if (!fieldType.IsValueType)
            {
                if (runtimeDeterminedOwningType.IsRuntimeDeterminedSubtype)
                {
                    fieldType = _typeSystemContext.GetFieldForInstantiatedType(field.GetTypicalFieldDefinition(), (InstantiatedType)owningType).FieldType;
                }

                Append("(");
                Append(_writer.GetCppSignatureTypeName(fieldType));
                Append(")");
            }
            Append(value);
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
            else
                AddTypeReference(type, false);
            var addr = _stack.Pop();

            PushTemp(GetStackValueKind(type), type);

            AppendCastIfNecessary(GetStackValueKind(type), type);
            Append("*(");
            Append(_writer.GetCppSignatureTypeName(type));
            Append("*)");
            Append(addr);

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
            else
                AddTypeReference(type, false);

            var value = _stack.Pop();
            var addr = _stack.Pop();

            // TODO: Write barrier as necessary!!!

            AppendLine();
            Append("*(");
            Append(_writer.GetCppSignatureTypeName(type));
            Append("*)");
            Append(addr);
            Append(" = ");
            AppendCastIfNecessary(type, value);
            Append(value);
            AppendSemicolon();
        }

        private void ImportThrow()
        {
            var obj = _stack.Pop();

            AppendLine();
            Append("__throw_exception(");
            Append(obj);
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
                    case '"':
                        escaped.Append("\\\"");
                        break;
                    default:
                        // TODO: Unicode string literals
                        if (c > 0x7F)
                        {
                            escaped.Append("?");
                            break;
                        }

                        if (c < 0x20)
                            escaped.Append("\\x" + ((int)c).ToStringInvariant("X2"));
                        else
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
            TypeDesc type = (TypeDesc)_canonMethodIL.GetObject(token);

            var addr = _stack.Pop();
            AppendLine();
            Append("::memset((void*)");
            Append(addr);
            Append(",0,sizeof(");
            Append(GetSignatureTypeNameAndAddReference(_writer.ConvertToCanonFormIfNecessary(type, CanonicalFormKind.Specific)));
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

                var value = _stack.Pop();
                _stack.Push(BoxValue(type, value));
            }
        }

        private ExpressionEntry BoxValue(TypeDesc type, StackEntry value)
        {
            string tempName = NewTempName();
            TypeDesc runtimeDeterminedType = type;

            if (type.IsRuntimeDeterminedSubtype)
                type = type.ConvertToCanonForm(CanonicalFormKind.Specific);

            AddTypeReference(type, true);
            Append(GetStackValueKindCPPTypeName(StackValueKind.ObjRef, type));
            Append(" ");
            Append(tempName);
            Append(" = __allocate_object(");

            if (runtimeDeterminedType.IsRuntimeDeterminedSubtype)
            {
                Append("(MethodTable *)");
                Append(GetGenericLookupHelperAndAddReference(ReadyToRunHelperId.TypeHandle, runtimeDeterminedType));
                Append("(");
                Append(GetGenericContext());
                Append("))");
            }
            else
            {
                Append(_writer.GetCppTypeName(type));
                Append("::__getMethodTable())");
            }

            AppendSemicolon();

            // TODO: Write barrier as necessary
            AppendLine();
            Append("*(" + _writer.GetCppSignatureTypeName(type) + " *)((void **)");
            Append(tempName);
            Append(" + 1) = ");
            Append(value);
            AppendSemicolon();
            return new ExpressionEntry(StackValueKind.ObjRef, tempName, type);
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
            _stack.Clear();

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
                    IsOffsetContained(offset, r.ILRegion.HandlerOffset, r.ILRegion.HandlerLength))
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
            _stack.Clear();

            int finallyIndex = FindNearestFinally(_currentOffset - 1);

            AppendLine();
            Append("goto __endFinally");
            Append(finallyIndex.ToStringInvariant());
            AppendSemicolon();
        }

        private void ImportNewArray(int token)
        {
            TypeDesc runtimeDeterminedType = (TypeDesc)_methodIL.GetObject(token);
            TypeDesc runtimeDeterminedArrayType = runtimeDeterminedType.MakeArrayType();
            TypeDesc type = (TypeDesc)_canonMethodIL.GetObject(token);
            TypeDesc arrayType = _writer.ConvertToCanonFormIfNecessary(type.MakeArrayType(), CanonicalFormKind.Specific);

            var numElements = _stack.Pop();

            PushTemp(StackValueKind.ObjRef, arrayType);

            Append("__allocate_array(");
            Append(numElements);
            Append(", ");

            AddTypeReference(arrayType, true);

            if (runtimeDeterminedType.IsRuntimeDeterminedSubtype)
            {
                Append("(MethodTable *)");
                Append(GetGenericLookupHelperAndAddReference(ReadyToRunHelperId.TypeHandle, runtimeDeterminedArrayType));
                Append("(");
                Append(GetGenericContext());
                Append(")");
            }
            else
            {
                Append(_writer.GetCppTypeName(runtimeDeterminedArrayType));
                Append("::__getMethodTable()");
            }

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
            else
                AddTypeReference(elementType, true);

            var index = _stack.Pop();
            var arrayPtr = _stack.Pop();

            // Range check
            AppendLine();
            Append("__range_check(");
            Append(arrayPtr);
            Append(",");
            Append(index);
            Append(");");

            PushTemp(GetStackValueKind(elementType), elementType);

            Append("*(");
            Append(_writer.GetCppSignatureTypeName(elementType));
            Append("*)((char *)");
            Append(arrayPtr);
            Append(" + ARRAY_BASE + sizeof(");
            Append(_writer.GetCppSignatureTypeName(elementType));
            Append(") * ");
            Append(index);
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
            else
                AddTypeReference(elementType, true);
            var value = _stack.Pop();
            var index = _stack.Pop();
            var arrayPtr = _stack.Pop();

            // Range check
            AppendLine();
            Append("__range_check(");
            Append(arrayPtr);
            Append(",");
            Append(index);
            Append(");");

            // TODO: Array covariance
            // TODO: Write barrier as necessary!!!

            AppendLine();
            Append("*(");
            Append(_writer.GetCppSignatureTypeName(elementType));
            Append("*)((char *)");
            Append(arrayPtr);
            Append(" + ARRAY_BASE + sizeof(");
            Append(_writer.GetCppSignatureTypeName(elementType));
            Append(") * ");
            Append(index);
            Append(") = ");

            AppendCastIfNecessary(elementType, value);
            Append(value);

            AppendSemicolon();
        }

        private void ImportAddressOfElement(int token)
        {
            TypeDesc elementType = (TypeDesc)_canonMethodIL.GetObject(token);
            var index = _stack.Pop();
            var arrayPtr = _stack.Pop();

            // TODO: type check, unless readonly prefix was applied

            // Range check
            AppendLine();
            Append("__range_check(");
            Append(arrayPtr);
            Append(",");
            Append(index);
            Append(");");

            TypeDesc byRef = elementType.MakeByRefType();

            PushTemp(StackValueKind.ByRef, byRef);
            AppendCastIfNecessary(StackValueKind.ByRef, byRef);

            Append("(");
            Append(_writer.GetCppSignatureTypeName(elementType));
            Append("*)((char *)");
            Append(arrayPtr);
            Append(" + ARRAY_BASE + sizeof(");
            Append(_writer.GetCppSignatureTypeName(elementType));
            Append(") * ");
            Append(index);
            Append(")");

            AppendSemicolon();
        }

        private void ImportLoadLength()
        {
            var arrayPtr = _stack.Pop();

            PushTemp(StackValueKind.NativeInt);

            Append("*((intptr_t *)");
            Append(arrayPtr);
            Append("+ 1)");

            AppendSemicolon();
        }

        private void ImportUnaryOperation(ILOpcode opCode)
        {
            var argument = _stack.Pop();

            PushTemp(argument.Kind, argument.Type);

            Debug.Assert((opCode == ILOpcode.neg) || (opCode == ILOpcode.not));
            Append((opCode == ILOpcode.neg) ? "-" : "~");
            Append(argument);

            AppendSemicolon();
        }

        private void ImportCpOpj(int token)
        {
            throw new NotImplementedException();
        }

        private void ImportUnbox(int token, ILOpcode opCode)
        {
            var type = ResolveTypeToken(token);

            var obj = _stack.Pop();

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
                    Append("(");
                    Append(typeName);
                    Append(")*");
                }

                Append("(");
                Append(_writer.GetCppSignatureTypeName(type));
                Append("*)");
                Append("((void **)");
                Append(obj);
                Append("+1)");
            }
            else
            {
                // TODO: Cast
                Append(obj);
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
            StackEntry value;
            if (ldtokenValue is TypeDesc)
            {
                ldtokenKind = WellKnownType.RuntimeTypeHandle;
                TypeDesc type = (TypeDesc)ldtokenValue;

                MethodDesc helper = _typeSystemContext.GetHelperEntryPoint("LdTokenHelpers", "GetRuntimeTypeHandle");
                AddMethodReference(helper);

                name = String.Concat(
                    _writer.GetCppTypeName(helper.OwningType),
                    "::",
                    _writer.GetCppMethodName(helper));

                if (type.IsRuntimeDeterminedSubtype)
                {
                    name = String.Concat(
                        name,
                        "((intptr_t)",
                        GetGenericLookupHelperAndAddReference(ReadyToRunHelperId.TypeHandle, type),
                        "(",
                        GetGenericContext(),
                        "))");
                }
                else
                {
                    AddTypeReference(type, true);

                    name = String.Concat(
                        name,
                        "((intptr_t)",
                        _compilation.NameMangler.GetMangledTypeName(type),
                        "::__getMethodTable())");
                }

                value = new LdTokenEntry<TypeDesc>(StackValueKind.ValueType, name, type, GetWellKnownType(ldtokenKind));
            }
            else if (ldtokenValue is FieldDesc)
            {
                ldtokenKind = WellKnownType.RuntimeFieldHandle;
                value = new LdTokenEntry<FieldDesc>(StackValueKind.ValueType, null, (FieldDesc)ldtokenValue, GetWellKnownType(ldtokenKind));
            }
            else if (ldtokenValue is MethodDesc)
            {
                throw new NotImplementedException();
            }
            else
                throw new InvalidOperationException();

            _stack.Push(value);
        }

        private void ImportLocalAlloc()
        {
            StackEntry count = _stack.Pop();

            // TODO: this is machine dependent and might not result in a HW stack overflow exception
            // TODO: might not have enough alignment guarantees for the allocated buffer

            var bufferName = NewTempName();
            AppendLine();
            Append("intptr_t ");
            Append(bufferName);
            Append(" = (intptr_t)alloca(");
            Append(count);
            Append(")");
            AppendSemicolon();

            if (_methodIL.IsInitLocals)
            {
                AppendLine();
                Append("::memset((void*)");
                Append(bufferName);
                Append(", 0, ");
                Append(count);
                Append(")");
                AppendSemicolon();
            }

            PushExpression(StackValueKind.NativeInt, bufferName);
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
            var size = _stack.Pop();
            var value = _stack.Pop();
            var addr = _stack.Pop();

            Append("::memset((void*)");
            Append(addr);
            Append(",");
            Append(value);
            Append(",");
            Append(size);
            Append(")");
            AppendSemicolon();
        }

        private void ImportRethrow()
        {
            throw new NotImplementedException();
        }

        private void ImportSizeOf(int token)
        {
            var type = ResolveTypeToken(token);

            GetSignatureTypeNameAndAddReference(type);

            PushExpression(StackValueKind.Int32, "(int32_t)sizeof(" + _writer.GetCppTypeName(type) + ")");
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
            // TODO:
            // throw new NotImplementedException();
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

            _constrained = (TypeDesc)_methodIL.GetObject(token);
        }

        private void ImportNoPrefix(byte mask)
        {
            throw new NotImplementedException();
        }

        private void ImportReadOnlyPrefix()
        {
            _pendingPrefix |= Prefix.ReadOnly;
        }

        private void TriggerCctor(TypeDesc type)
        {
            Debug.Assert(!type.IsRuntimeDeterminedSubtype);

            MethodDesc cctor = type.GetStaticConstructor();
            if (cctor == null)
                return;

            MethodDesc canonCctor = cctor.GetCanonMethodTarget(CanonicalFormKind.Specific);

            if (_nodeFactory.PreinitializationManager.HasEagerStaticConstructor(type))
            {
                _dependencies.Add(_nodeFactory.EagerCctorIndirection(canonCctor));
            }
            else if (_nodeFactory.PreinitializationManager.HasLazyStaticConstructor(type))
            {
                IMethodNode helperNode = (IMethodNode)_nodeFactory.HelperEntrypoint(HelperEntrypoint.EnsureClassConstructorRunAndReturnNonGCStaticBase);

                Append(_writer.GetCppTypeName(helperNode.Method.OwningType));
                Append("::");
                Append(_writer.GetCppMethodName(helperNode.Method));
                Append("((::System_Private_CoreLib::System::Runtime::CompilerServices::StaticClassConstructionContext*)&");
                Append(_writer.GetCppStaticsName(type));
                Append(", (intptr_t)&");
                Append(_writer.GetCppStaticsName(type));
                Append(");");

                AddMethodReference(canonCctor);
            }
        }

        private void AddTypeReference(TypeDesc type, bool constructed)
        {
            // CppImporter will rather arbitrarily try to generate types as constructed.
            // Stomp over the choice and only allow this if it remotely makes sense.
            constructed = constructed & ConstructedEETypeNode.CreationAllowed(type);

            AddTypeDependency(type, constructed);

            if (!type.IsGenericDefinition)
            {
                foreach (var field in type.GetFields())
                {
                    AddTypeDependency(_writer.ConvertToCanonFormIfNecessary(field.FieldType, CanonicalFormKind.Specific), false);
                }
            }
        }
        private void AddTypeDependency(TypeDesc type, bool constructed)
        {
            Debug.Assert(!type.IsRuntimeDeterminedSubtype);

            if (type.IsPrimitive ||
                type.IsCanonicalSubtype(CanonicalFormKind.Any) && (type.IsPointer || type.IsByRef))
            {
                return;
            }

            Object node;
            if (constructed)
                node = _nodeFactory.ConstructedTypeSymbol(type);
            else
                node = _nodeFactory.NecessaryTypeSymbol(type);
            if (_dependencies.Contains(node))
                return;
            _dependencies.Add(node);
        }

        private void AddMethodReference(MethodDesc method)
        {
            _dependencies.Add(_nodeFactory.MethodEntrypoint(method));
        }

        private void AddCanonicalReference(MethodDesc method)
        {
            _dependencies.Add(_nodeFactory.CanonicalEntrypoint(method));
        }

        private void AddVirtualMethodReference(MethodDesc method)
        {
            _dependencies.Add(_nodeFactory.VirtualMethodUse(method));
        }

        private void AddFieldReference(FieldDesc field)
        {
            var owningType = field.OwningType;

            Debug.Assert(!owningType.IsRuntimeDeterminedSubtype);

            if (field.IsStatic)
            {
                var metadataType = owningType as MetadataType;

                Object node;
                if (field.IsThreadStatic)
                {
                    node = _nodeFactory.TypeThreadStaticsSymbol(metadataType);
                }
                else
                {
                    if (field.HasGCStaticBase)
                        node = _nodeFactory.TypeGCStaticsSymbol(metadataType);
                    else
                        node = _nodeFactory.TypeNonGCStaticsSymbol(metadataType);
                }

                // TODO: Remove once the dependencies for static fields are tracked properly
                GetSignatureTypeNameAndAddReference(metadataType, true);
                _dependencies.Add(node);
            }

            var fieldType = field.FieldType;
            AddTypeReference(_writer.ConvertToCanonFormIfNecessary(fieldType, CanonicalFormKind.Specific), false);
        }

        private string GetSignatureTypeNameAndAddReference(TypeDesc type, bool constructed = true)
        {
            AddTypeReference(type, constructed);
            return _writer.GetCppSignatureTypeName(type);
        }

        private void ReportInvalidBranchTarget(int targetOffset)
        {
            ThrowHelper.ThrowInvalidProgramException();
        }

        private void ReportFallthroughAtEndOfMethod()
        {
            ThrowHelper.ThrowInvalidProgramException();
        }

        private void ReportMethodEndInsideInstruction()
        {
            ThrowHelper.ThrowInvalidProgramException();
        }

        private void ReportInvalidInstruction(ILOpcode opcode)
        {
            ThrowHelper.ThrowInvalidProgramException();
        }
    }
}
