﻿// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using Internal.Runtime.Interpreter;
using Internal.TypeSystem;

namespace Internal.IL
{
    partial class ILImporter
    {
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

            public bool TryStart;
            public bool FilterStart;
            public bool HandlerStart;
        }

        private class ExceptionRegion
        {
            public ILExceptionRegion ILRegion;
        }

        private readonly byte[] _ilBytes;
        private readonly MethodDesc _method;
        private readonly MethodIL _methodIL;
        private readonly ILInterpreter _interpreter;
        private ExceptionRegion[] _exceptionRegions;

        public ILImporter(ILInterpreter interpreter, MethodDesc method, MethodIL methodIL)
        {
            _ilBytes = methodIL.GetILBytes();
            _method = method;
            _methodIL = methodIL;
            _interpreter = interpreter;

            var ilExceptionRegions = methodIL.GetExceptionRegions();
            _exceptionRegions = new ExceptionRegion[methodIL.GetExceptionRegions().Length];
            for (int i = 0; i < ilExceptionRegions.Length; i++)
            {
                _exceptionRegions[i] = new ExceptionRegion() { ILRegion = ilExceptionRegions[i] };
            }
        }

        public void Interpret()
        {
            FindBasicBlocks();
            ImportBasicBlocks();
        }

        private void MarkInstructionBoundary() { }

        private void StartImportingInstruction() { }

        private void EndImportingInstruction() { }

        private void StartImportingBasicBlock(BasicBlock basicBlock) { }

        private void EndImportingBasicBlock(BasicBlock basicBlock) { }

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

        private TypeDesc ResolveTypeToken(int token)
        {
            return (TypeDesc)_methodIL.GetObject(token);
        }

        private TypeDesc GetWellKnownType(WellKnownType wellKnownType)
        {
            return _interpreter.TypeSystemContext.GetWellKnownType(wellKnownType);
        }

        private void ImportNop()
        {
            // Do nothing!
        }

        private void ImportBreak()
        {
            throw new NotImplementedException();
        }

        private void ImportLoadVar(int index, bool argument)
        {
            throw new NotImplementedException();
        }

        private void ImportStoreVar(int index, bool argument)
        {
            throw new NotImplementedException();
        }

        private void ImportAddressOfVar(int index, bool argument)
        {
            throw new NotImplementedException();
        }

        private void ImportDup()
        {
            throw new NotImplementedException();
        }

        private void ImportPop()
        {
            throw new NotImplementedException();
        }

        private void ImportCalli(int token)
        {
            throw new NotImplementedException();
        }

        private void ImportLoadNull()
        {
            _interpreter.EvaluationStack.Push(new ObjectRefStackItem(null));
        }

        private void ImportReturn()
        {
            bool hasReturnValue = _interpreter.EvaluationStack.TryPop(out StackItem stackItem);
            if (!hasReturnValue)
                return;

            switch (stackItem.Kind)
            {
                case StackValueKind.Int32:
                    _interpreter.SetReturnValue(((Int32StackItem)stackItem).Value);
                    break;
                case StackValueKind.Int64:
                    _interpreter.SetReturnValue(((Int64StackItem)stackItem).Value);
                    break;
                case StackValueKind.Unknown:
                case StackValueKind.NativeInt:
                case StackValueKind.Float:
                    if (stackItem.Type == WellKnownType.Single)
                        _interpreter.SetReturnValue(((FloatStackItem)stackItem).Value);
                    else if (stackItem.Type == WellKnownType.Double)
                        _interpreter.SetReturnValue(((DoubleStackItem)stackItem).Value);
                    break;
                case StackValueKind.ByRef:
                case StackValueKind.ObjRef:
                    _interpreter.SetReturnValue(((ObjectRefStackItem)stackItem).Value);
                    break;
                case StackValueKind.ValueType:
                default:
                    break;
            }
        }

        private void ImportLoadInt(long value, StackValueKind kind)
        {
            if (kind == StackValueKind.Int32)
                _interpreter.EvaluationStack.Push(new Int32StackItem((int)value));
            else if (kind == StackValueKind.Int64)
                _interpreter.EvaluationStack.Push(new Int64StackItem(value));
        }

        private void ImportLoadFloat(float value)
        {
            _interpreter.EvaluationStack.Push(new FloatStackItem(value));
        }

        private void ImportLoadFloat(double value)
        {
            _interpreter.EvaluationStack.Push(new DoubleStackItem(value));
        }

        private void ImportShiftOperation(ILOpcode opcode)
        {
            throw new NotImplementedException();
        }

        private void ImportCompareOperation(ILOpcode opcode)
        {
            throw new NotImplementedException();
        }

        private void ImportConvert(WellKnownType wellKnownType, bool checkOverflow, bool unsigned)
        {
            throw new NotImplementedException();
        }

        private void ImportUnaryOperation(ILOpcode opCode)
        {
            throw new NotImplementedException();
        }

        private void ImportCpOpj(int token)
        {
            throw new NotImplementedException();
        }

        private void ImportCkFinite()
        {
            throw new NotImplementedException();
        }

        private void ImportLocalAlloc()
        {
            throw new NotImplementedException();
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
            throw new NotImplementedException();
        }

        private void ImportUnalignedPrefix(byte alignment)
        {
            throw new NotImplementedException();
        }

        private void ImportVolatilePrefix()
        {
            throw new NotImplementedException();
        }

        private void ImportTailPrefix()
        {
            throw new NotImplementedException();
        }

        private void ImportNoPrefix(byte mask)
        {
            throw new NotImplementedException();
        }

        private void ImportThrow()
        {
            throw new NotImplementedException();
        }

        private void ImportInitObj(int token)
        {
            throw new NotImplementedException();
        }

        private void ImportLoadLength()
        {
            throw new NotImplementedException();
        }

        private void ImportEndFinally()
        {
            throw new NotImplementedException();
        }

        private void ImportFallthrough(BasicBlock nextBasicBlock)
        {
            throw new NotImplementedException();
        }

        private void ImportReadOnlyPrefix()
        {
            throw new NotImplementedException();
        }

        private void ImportRefAnyType()
        {
            throw new NotImplementedException();
        }

        private void ImportConstrainedPrefix(int v)
        {
            throw new NotImplementedException();
        }

        private void ImportLdFtn(int v, ILOpcode opCode)
        {
            throw new NotImplementedException();
        }

        private void ImportArgList()
        {
            throw new NotImplementedException();
        }

        private void ImportLeave(BasicBlock basicBlock)
        {
            throw new NotImplementedException();
        }

        private void ImportLdToken(int v)
        {
            throw new NotImplementedException();
        }

        private void ImportMkRefAny(int v)
        {
            throw new NotImplementedException();
        }

        private void ImportRefAnyVal(int v)
        {
            throw new NotImplementedException();
        }

        private void ImportAddressOfElement(int v)
        {
            throw new NotImplementedException();
        }

        private void ImportNewArray(int v)
        {
            throw new NotImplementedException();
        }

        private void ImportBox(int v)
        {
            throw new NotImplementedException();
        }

        private void ImportStoreField(int v1, bool v2)
        {
            throw new NotImplementedException();
        }

        private void ImportAddressOfField(int v1, bool v2)
        {
            throw new NotImplementedException();
        }

        private void ImportLoadField(int v1, bool v2)
        {
            throw new NotImplementedException();
        }

        private void ImportUnbox(int v, ILOpcode opCode)
        {
            throw new NotImplementedException();
        }

        private void ImportCasting(ILOpcode opCode, int v)
        {
            throw new NotImplementedException();
        }

        private void ImportLoadString(int token)
        {
            string str = (string)_methodIL.GetObject(token);
            _interpreter.EvaluationStack.Push(new ObjectRefStackItem(str));
        }

        private void ImportBinaryOperation(ILOpcode opCode)
        {
            throw new NotImplementedException();
        }

        private void ImportSwitchJump(int jmpBase, int[] jmpDelta, BasicBlock basicBlock)
        {
            throw new NotImplementedException();
        }

        private void ImportBranch(ILOpcode iLOpcode, BasicBlock basicBlock1, BasicBlock basicBlock2)
        {
            throw new NotImplementedException();
        }

        private void ImportCall(ILOpcode opCode, int v)
        {
            throw new NotImplementedException();
        }

        private void ImportJmp(int v)
        {
            throw new NotImplementedException();
        }

        private void ImportLoadIndirect(int token)
        {
            ImportLoadIndirect(ResolveTypeToken(token));
        }

        private void ImportLoadIndirect(TypeDesc type)
        {
            throw new NotImplementedException();
        }

        private void ImportStoreIndirect(int token)
        {
            ImportStoreIndirect(ResolveTypeToken(token));
        }

        private void ImportStoreIndirect(TypeDesc type)
        {
            throw new NotImplementedException();
        }

        private void ImportLoadElement(int token)
        {
            ImportLoadElement(ResolveTypeToken(token));
        }

        private void ImportLoadElement(TypeDesc elementType)
        {
            throw new NotImplementedException();
        }

        private void ImportStoreElement(int token)
        {
            ImportStoreElement(ResolveTypeToken(token));
        }

        private void ImportStoreElement(TypeDesc elementType)
        {
            throw new NotImplementedException();
        }
    }
}
