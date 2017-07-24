// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;

using Internal.TypeSystem;
using ILCompiler;

namespace Internal.IL
{
    // Implements an IL scanner that scans method bodies to be compiled by the code generation
    // backend before the actual compilation happens to gain insights into the code.
    partial class ILImporter
    {
        private readonly MethodDesc _method;
        private readonly WebAssemblyCodegenCompilation _compilation;

        private readonly byte[] _ilBytes;

        private class BasicBlock
        {
            // Common fields
            public BasicBlock Next;

            public int StartOffset;
            public int EndOffset;

            public bool TryStart;
            public bool FilterStart;
            public bool HandlerStart;
        }

        private class ExceptionRegion
        {
            public ILExceptionRegion ILRegion;
        }
        private ExceptionRegion[] _exceptionRegions;

        public ILImporter(WebAssemblyCodegenCompilation compilation, MethodDesc method, MethodIL methodIL)
        {
            _compilation = compilation;
            _method = method;
            _ilBytes = methodIL.GetILBytes();

            var ilExceptionRegions = methodIL.GetExceptionRegions();
            _exceptionRegions = new ExceptionRegion[ilExceptionRegions.Length];
            for (int i = 0; i < ilExceptionRegions.Length; i++)
            {
                _exceptionRegions[i] = new ExceptionRegion() { ILRegion = ilExceptionRegions[i] };
            }
        }

        public void Import()
        {
            FindBasicBlocks();
            ImportBasicBlocks();
        }

        private void MarkInstructionBoundary()
        {
        }

        private void StartImportingBasicBlock(BasicBlock basicBlock)
        {
        }

        private void EndImportingBasicBlock(BasicBlock basicBlock)
        {
        }

        private void StartImportingInstruction()
        {
        }

        private void EndImportingInstruction()
        {
        }

        private void ImportNop()
        {
        }

        private void ImportBreak()
        {
        }

        private void ImportLoadVar(int index, bool argument)
        {
        }

        private void ImportStoreVar(int index, bool argument)
        {
        }

        private void ImportAddressOfVar(int index, bool argument)
        {
        }

        private void ImportDup()
        {
        }

        private void ImportPop()
        {
        }

        private void ImportJmp(int token)
        {
        }

        private void ImportCasting(ILOpcode opcode, int token)
        {
        }

        private void ImportLoadNull()
        {
        }

        private void ImportReturn()
        {
        }

        private void ImportCall(ILOpcode opcode, int token)
        {
        }

        private void ImportCalli(int token)
        {
        }

        private void ImportLdFtn(int token, ILOpcode opCode)
        {
        }

        private void ImportLoadInt(long value, StackValueKind kind)
        {
        }

        private void ImportLoadFloat(double value)
        {
        }

        private void ImportBranch(ILOpcode opcode, BasicBlock target, BasicBlock fallthrough)
        {
        }

        private void ImportSwitchJump(int jmpBase, int[] jmpDelta, BasicBlock fallthrough)
        {
        }

        private void ImportLoadIndirect(int token)
        {
        }

        private void ImportLoadIndirect(TypeDesc type)
        {
        }

        private void ImportStoreIndirect(int token)
        {
        }

        private void ImportStoreIndirect(TypeDesc type)
        {
        }

        private void ImportBinaryOperation(ILOpcode opcode)
        {
        }

        private void ImportShiftOperation(ILOpcode opcode)
        {
        }

        private void ImportCompareOperation(ILOpcode opcode)
        {
        }

        private void ImportConvert(WellKnownType wellKnownType, bool checkOverflow, bool unsigned)
        {
        }

        private void ImportUnaryOperation(ILOpcode opCode)
        {
        }

        private void ImportCpOpj(int token)
        {
        }

        private void ImportUnbox(int token, ILOpcode opCode)
        {
        }

        private void ImportRefAnyVal(int token)
        {
        }

        private void ImportCkFinite()
        {
        }

        private void ImportMkRefAny(int token)
        {
        }

        private void ImportLdToken(int token)
        {
        }

        private void ImportLocalAlloc()
        {
        }

        private void ImportEndFilter()
        {
        }

        private void ImportCpBlk()
        {
        }

        private void ImportInitBlk()
        {
        }

        private void ImportRethrow()
        {
        }

        private void ImportSizeOf(int token)
        {
        }

        private void ImportRefAnyType()
        {
        }

        private void ImportArgList()
        {
        }

        private void ImportUnalignedPrefix(byte alignment)
        {
        }

        private void ImportVolatilePrefix()
        {
        }

        private void ImportTailPrefix()
        {
        }

        private void ImportConstrainedPrefix(int token)
        {
        }

        private void ImportNoPrefix(byte mask)
        {
        }

        private void ImportReadOnlyPrefix()
        {
        }

        private void ImportThrow()
        {
        }

        private void ImportLoadField(int token, bool isStatic)
        {
        }

        private void ImportAddressOfField(int token, bool isStatic)
        {
        }

        private void ImportStoreField(int token, bool isStatic)
        {
        }

        private void ImportLoadString(int token)
        {
        }

        private void ImportInitObj(int token)
        {
        }

        private void ImportBox(int token)
        {
        }

        private void ImportLeave(BasicBlock target)
        {
        }

        private void ImportNewArray(int token)
        {
        }

        private void ImportLoadElement(int token)
        {
        }

        private void ImportLoadElement(TypeDesc elementType)
        {
        }

        private void ImportStoreElement(int token)
        {
        }

        private void ImportStoreElement(TypeDesc elementType)
        {
        }

        private void ImportLoadLength()
        {
        }

        private void ImportAddressOfElement(int token)
        {
        }

        private void ImportEndFinally()
        {
        }

        private void ImportFallthrough(BasicBlock next)
        {
        }

        private TypeDesc GetWellKnownType(WellKnownType wellKnownType)
        {
            return _compilation.TypeSystemContext.GetWellKnownType(wellKnownType);
        }

        private void ReportInvalidBranchTarget(int targetOffset)
        {
            ThrowHelper.ThrowInvalidProgramException();
        }

        private void ReportFallthroughAtEndOfMethod()
        {
            ThrowHelper.ThrowInvalidProgramException();
        }

        private void ReportInvalidInstruction(ILOpcode opcode)
        {
            ThrowHelper.ThrowInvalidProgramException();
        }

    }
}