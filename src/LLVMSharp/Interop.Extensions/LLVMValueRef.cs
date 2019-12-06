// Copyright (c) Microsoft and Contributors. All rights reserved. Licensed under the University of Illinois/NCSA Open Source License. See LICENSE.txt in the project root for license information.

using System;

namespace LLVMSharp.Interop
{
    public unsafe partial struct LLVMValueRef : IEquatable<LLVMValueRef>
    {
        public LLVMValueRef(IntPtr handle)
        {
            Handle = handle;
        }

        public IntPtr Handle;

        public static implicit operator LLVMValueRef(LLVMOpaqueValue* value)
        {
            return new LLVMValueRef((IntPtr)value);
        }

        public static implicit operator LLVMOpaqueValue*(LLVMValueRef value)
        {
            return (LLVMOpaqueValue*)value.Handle;
        }

        public uint Alignment
        {
            get => (Handle != IntPtr.Zero) ? LLVM.GetAlignment(this) : default;
            set => LLVM.SetAlignment(this, value);
        }

        public LLVMBasicBlockRef AsBasicBlock => (Handle != IntPtr.Zero) ? LLVM.ValueAsBasicBlock(this) : default;

        public LLVMBasicBlockRef[] BasicBlocks
        {
            get
            {
                if (Handle == IntPtr.Zero)
                {
                    return Array.Empty<LLVMBasicBlockRef>();
                }

                var BasicBlocks = new LLVMBasicBlockRef[BasicBlocksCount];

                fixed (LLVMBasicBlockRef* pBasicBlocks = BasicBlocks)
                {
                    LLVM.GetBasicBlocks(this, (LLVMOpaqueBasicBlock**)pBasicBlocks);
                }

                return BasicBlocks;
            }
        }

        public uint BasicBlocksCount => (Handle != IntPtr.Zero) ? LLVM.CountBasicBlocks(this) : default;

        public LLVMValueRef Condition
        {
            get => (Handle != IntPtr.Zero) ? LLVM.GetCondition(this) : default;
            set => LLVM.SetCondition(this, value);
        }

        public ulong ConstIntZExt => (Handle != IntPtr.Zero) ? LLVM.ConstIntGetZExtValue(this) : default;

        public long ConstIntSExt => (Handle != IntPtr.Zero) ? LLVM.ConstIntGetSExtValue(this) : default;

        public LLVMOpcode ConstOpcode => (Handle != IntPtr.Zero) ? LLVM.GetConstOpcode(this) : default;

        public LLVMDLLStorageClass DLLStorageClass
        {
            get => (Handle != IntPtr.Zero) ? LLVM.GetDLLStorageClass(this) : default;
            set => LLVM.SetDLLStorageClass(this, value);
        }

        public LLVMBasicBlockRef EntryBasicBlock => (Handle != IntPtr.Zero) ? LLVM.GetEntryBasicBlock(this) : default;

        public LLVMRealPredicate FCmpPredicate => (Handle != IntPtr.Zero) ? LLVM.GetFCmpPredicate(this) : default;

        public LLVMBasicBlockRef FirstBasicBlock => (Handle != IntPtr.Zero) ? LLVM.GetFirstBasicBlock(this) : default;

        public LLVMValueRef FirstParam => (Handle != IntPtr.Zero) ? LLVM.GetFirstParam(this) : default;

        public LLVMUseRef FirstUse => (Handle != IntPtr.Zero) ? LLVM.GetFirstUse(this) : default;

        public uint FunctionCallConv
        {
            get => (Handle != IntPtr.Zero) ? LLVM.GetFunctionCallConv(this) : default;
            set => LLVM.SetFunctionCallConv(this, value);
        }

        public string GC
        {
            get
            {
                if (Handle == IntPtr.Zero)
                {
                    return string.Empty;
                }

                var pName = LLVM.GetGC(this);

                if (pName is null)
                {
                    return string.Empty;
                }

                var span = new ReadOnlySpan<byte>(pName, int.MaxValue);
                return span.Slice(0, span.IndexOf((byte)'\0')).AsString();
            }

            set
            {
                using var marshaledName = new MarshaledString(value);
                LLVM.SetGC(this, marshaledName);
            }
        }

        public LLVMModuleRef GlobalParent => (Handle != IntPtr.Zero) ? LLVM.GetGlobalParent(this) : default;

        public bool HasMetadata => (Handle != IntPtr.Zero) ? LLVM.HasMetadata(this) != 0 : default;

        public bool HasUnnamedAddr
        {
            get => (Handle != IntPtr.Zero) ? LLVM.HasUnnamedAddr(this) != 0 : default;
            set => LLVM.SetUnnamedAddr(this, value ? 1 : 0);
        }

        public LLVMIntPredicate ICmpPredicate => (Handle != IntPtr.Zero) ? LLVM.GetICmpPredicate(this) : default;

        public uint IncomingCount => (Handle != IntPtr.Zero) ? LLVM.CountIncoming(this) : default;

        public LLVMValueRef Initializer
        {
            get => (Handle != IntPtr.Zero) ? LLVM.GetInitializer(this) : default;
            set => LLVM.SetInitializer(this, value);
        }

        public uint InstructionCallConv
        {
            get => (Handle != IntPtr.Zero) ? LLVM.GetInstructionCallConv(this) : default;
            set => LLVM.SetInstructionCallConv(this, value);
        }

        public LLVMValueRef InstructionClone => (Handle != IntPtr.Zero) ? LLVM.InstructionClone(this) : default;

        public LLVMOpcode InstructionOpcode => (Handle != IntPtr.Zero) ? LLVM.GetInstructionOpcode(this) : default;

        public LLVMBasicBlockRef InstructionParent => (Handle != IntPtr.Zero) ? LLVM.GetInstructionParent(this) : default;

        public uint IntrinsicID => (Handle != IntPtr.Zero) ? LLVM.GetIntrinsicID(this) : default;

        public LLVMValueRef IsAAddrSpaceCastInst => (Handle != IntPtr.Zero) ? LLVM.IsAAddrSpaceCastInst(this) : default;

        public LLVMValueRef IsAAllocaInst => (Handle != IntPtr.Zero) ? LLVM.IsAAllocaInst(this) : default;

        public LLVMValueRef IsAArgument => (Handle != IntPtr.Zero) ? LLVM.IsAArgument(this) : default;

        public LLVMValueRef IsABasicBlock => (Handle != IntPtr.Zero) ? LLVM.IsABasicBlock(this) : default;

        public LLVMValueRef IsABinaryOperator => (Handle != IntPtr.Zero) ? LLVM.IsABinaryOperator(this) : default;

        public LLVMValueRef IsABitCastInst => (Handle != IntPtr.Zero) ? LLVM.IsABitCastInst(this) : default;

        public LLVMValueRef IsABlockAddress => (Handle != IntPtr.Zero) ? LLVM.IsABlockAddress(this) : default;

        public LLVMValueRef IsABranchInst => (Handle != IntPtr.Zero) ? LLVM.IsABranchInst(this) : default;

        public LLVMValueRef IsACallInst => (Handle != IntPtr.Zero) ? LLVM.IsACallInst(this) : default;

        public LLVMValueRef IsACastInst => (Handle != IntPtr.Zero) ? LLVM.IsACastInst(this) : default;

        public LLVMValueRef IsACmpInst => (Handle != IntPtr.Zero) ? LLVM.IsACmpInst(this) : default;

        public LLVMValueRef IsAConstant => (Handle != IntPtr.Zero) ? LLVM.IsAConstant(this) : default;

        public LLVMValueRef IsAConstantAggregateZero => (Handle != IntPtr.Zero) ? LLVM.IsAConstantAggregateZero(this) : default;

        public LLVMValueRef IsAConstantArray => (Handle != IntPtr.Zero) ? LLVM.IsAConstantArray(this) : default;

        public LLVMValueRef IsAConstantDataArray => (Handle != IntPtr.Zero) ? LLVM.IsAConstantDataArray(this) : default;

        public LLVMValueRef IsAConstantDataSequential => (Handle != IntPtr.Zero) ? LLVM.IsAConstantDataSequential(this) : default;

        public LLVMValueRef IsAConstantDataVector => (Handle != IntPtr.Zero) ? LLVM.IsAConstantDataVector(this) : default;

        public LLVMValueRef IsAConstantExpr => (Handle != IntPtr.Zero) ? LLVM.IsAConstantExpr(this) : default;

        public LLVMValueRef IsAConstantFP => (Handle != IntPtr.Zero) ? LLVM.IsAConstantFP(this) : default;

        public LLVMValueRef IsAConstantInt => (Handle != IntPtr.Zero) ? LLVM.IsAConstantInt(this) : default;

        public LLVMValueRef IsAConstantPointerNull => (Handle != IntPtr.Zero) ? LLVM.IsAConstantPointerNull(this) : default;

        public LLVMValueRef IsAConstantStruct => (Handle != IntPtr.Zero) ? LLVM.IsAConstantStruct(this) : default;

        public LLVMValueRef IsAConstantVector => (Handle != IntPtr.Zero) ? LLVM.IsAConstantVector(this) : default;

        public LLVMValueRef IsADbgDeclareInst => (Handle != IntPtr.Zero) ? LLVM.IsADbgDeclareInst(this) : default;

        public LLVMValueRef IsADbgInfoIntrinsic => (Handle != IntPtr.Zero) ? LLVM.IsADbgInfoIntrinsic(this) : default;

        public LLVMValueRef IsAExtractElementInst => (Handle != IntPtr.Zero) ? LLVM.IsAExtractElementInst(this) : default;

        public LLVMValueRef IsAExtractValueInst => (Handle != IntPtr.Zero) ? LLVM.IsAExtractValueInst(this) : default;

        public LLVMValueRef IsAFCmpInst => (Handle != IntPtr.Zero) ?  LLVM.IsAFCmpInst(this) : default;

        public LLVMValueRef IsAFPExtInst => (Handle != IntPtr.Zero) ?  LLVM.IsAFPExtInst(this) : default;

        public LLVMValueRef IsAFPToSIInst => (Handle != IntPtr.Zero) ?  LLVM.IsAFPToSIInst(this) : default;

        public LLVMValueRef IsAFPToUIInst => (Handle != IntPtr.Zero) ?  LLVM.IsAFPToUIInst(this) : default;

        public LLVMValueRef IsAFPTruncInst => (Handle != IntPtr.Zero) ?  LLVM.IsAFPTruncInst(this) : default;

        public LLVMValueRef IsAFunction => (Handle != IntPtr.Zero) ?  LLVM.IsAFunction(this) : default;

        public LLVMValueRef IsAGetElementPtrInst => (Handle != IntPtr.Zero) ?  LLVM.IsAGetElementPtrInst(this) : default;

        public LLVMValueRef IsAGlobalAlias => (Handle != IntPtr.Zero) ?  LLVM.IsAGlobalAlias(this) : default;

        public LLVMValueRef IsAGlobalObject => (Handle != IntPtr.Zero) ?  LLVM.IsAGlobalObject(this) : default;

        public LLVMValueRef IsAGlobalValue => (Handle != IntPtr.Zero) ?  LLVM.IsAGlobalValue(this) : default;

        public LLVMValueRef IsAGlobalVariable => (Handle != IntPtr.Zero) ?  LLVM.IsAGlobalVariable(this) : default;

        public LLVMValueRef IsAICmpInst => (Handle != IntPtr.Zero) ?  LLVM.IsAICmpInst(this) : default;

        public LLVMValueRef IsAIndirectBrInst => (Handle != IntPtr.Zero) ?  LLVM.IsAIndirectBrInst(this) : default;

        public LLVMValueRef IsAInlineAsm => (Handle != IntPtr.Zero) ?  LLVM.IsAInlineAsm(this) : default;

        public LLVMValueRef IsAInsertElementInst => (Handle != IntPtr.Zero) ?  LLVM.IsAInsertElementInst(this) : default;

        public LLVMValueRef IsAInsertValueInst => (Handle != IntPtr.Zero) ?  LLVM.IsAInsertValueInst(this) : default;

        public LLVMValueRef IsAInstruction => (Handle != IntPtr.Zero) ?  LLVM.IsAInstruction(this) : default;

        public LLVMValueRef IsAIntrinsicInst => (Handle != IntPtr.Zero) ?  LLVM.IsAIntrinsicInst(this) : default;

        public LLVMValueRef IsAIntToPtrInst => (Handle != IntPtr.Zero) ?  LLVM.IsAIntToPtrInst(this) : default;

        public LLVMValueRef IsAInvokeInst => (Handle != IntPtr.Zero) ?  LLVM.IsAInvokeInst(this) : default;

        public LLVMValueRef IsALandingPadInst => (Handle != IntPtr.Zero) ?  LLVM.IsALandingPadInst(this) : default;

        public LLVMValueRef IsALoadInst => (Handle != IntPtr.Zero) ?  LLVM.IsALoadInst(this) : default;

        public LLVMValueRef IsAMDNode => (Handle != IntPtr.Zero) ?  LLVM.IsAMDNode(this) : default;

        public LLVMValueRef IsAMDString => (Handle != IntPtr.Zero) ?  LLVM.IsAMDString(this) : default;

        public LLVMValueRef IsAMemCpyInst => (Handle != IntPtr.Zero) ?  LLVM.IsAMemCpyInst(this) : default;

        public LLVMValueRef IsAMemIntrinsic => (Handle != IntPtr.Zero) ?  LLVM.IsAMemIntrinsic(this) : default;

        public LLVMValueRef IsAMemMoveInst => (Handle != IntPtr.Zero) ?  LLVM.IsAMemMoveInst(this) : default;

        public LLVMValueRef IsAMemSetInst => (Handle != IntPtr.Zero) ?  LLVM.IsAMemSetInst(this) : default;

        public LLVMValueRef IsAPHINode => (Handle != IntPtr.Zero) ?  LLVM.IsAPHINode(this) : default;

        public LLVMValueRef IsAPtrToIntInst => (Handle != IntPtr.Zero) ?  LLVM.IsAPtrToIntInst(this) : default;

        public LLVMValueRef IsAResumeInst => (Handle != IntPtr.Zero) ?  LLVM.IsAResumeInst(this) : default;

        public LLVMValueRef IsAReturnInst => (Handle != IntPtr.Zero) ?  LLVM.IsAReturnInst(this) : default;

        public LLVMValueRef IsASelectInst => (Handle != IntPtr.Zero) ?  LLVM.IsASelectInst(this) : default;

        public LLVMValueRef IsASExtInst => (Handle != IntPtr.Zero) ?  LLVM.IsASExtInst(this) : default;

        public LLVMValueRef IsAShuffleVectorInst => (Handle != IntPtr.Zero) ?  LLVM.IsAShuffleVectorInst(this) : default;

        public LLVMValueRef IsASIToFPInst => (Handle != IntPtr.Zero) ?  LLVM.IsASIToFPInst(this) : default;

        public LLVMValueRef IsAStoreInst => (Handle != IntPtr.Zero) ?  LLVM.IsAStoreInst(this) : default;

        public LLVMValueRef IsASwitchInst => (Handle != IntPtr.Zero) ?  LLVM.IsASwitchInst(this) : default;

        public LLVMValueRef IsATerminatorInst => (Handle != IntPtr.Zero) ?  LLVM.IsATerminatorInst(this) : default;

        public LLVMValueRef IsATruncInst => (Handle != IntPtr.Zero) ?  LLVM.IsATruncInst(this) : default;

        public LLVMValueRef IsAUIToFPInst => (Handle != IntPtr.Zero) ?  LLVM.IsAUIToFPInst(this) : default;

        public LLVMValueRef IsAUnaryInstruction => (Handle != IntPtr.Zero) ?  LLVM.IsAUnaryInstruction(this) : default;

        public LLVMValueRef IsAUndefValue => (Handle != IntPtr.Zero) ?  LLVM.IsAUndefValue(this) : default;

        public LLVMValueRef IsAUnreachableInst => (Handle != IntPtr.Zero) ?  LLVM.IsAUnreachableInst(this) : default;

        public LLVMValueRef IsAUser => (Handle != IntPtr.Zero) ?  LLVM.IsAUser(this) : default;

        public LLVMValueRef IsAVAArgInst => (Handle != IntPtr.Zero) ?  LLVM.IsAVAArgInst(this) : default;

        public LLVMValueRef IsAZExtInst => (Handle != IntPtr.Zero) ?  LLVM.IsAZExtInst(this) : default;

        public bool IsBasicBlock => (Handle != IntPtr.Zero) ?  LLVM.ValueIsBasicBlock(this) != 0 : default;

        public bool IsCleanup
        {
            get => (Handle != IntPtr.Zero) ? LLVM.IsCleanup(this) != 0 : default;
            set => LLVM.SetCleanup(this, value ? 1 : 0);
        }

        public bool IsConditional => (Handle != IntPtr.Zero) ? LLVM.IsConditional(this) != 0 : default;

        public bool IsConstant => (Handle != IntPtr.Zero) ? LLVM.IsConstant(this) != 0 : default;

        public bool IsConstantString => (Handle != IntPtr.Zero) ? LLVM.IsConstantString(this) != 0 : default;

        public bool IsDeclaration => (Handle != IntPtr.Zero) ? LLVM.IsDeclaration(this) != 0 : default;

        public bool IsExternallyInitialized
        {
            get => (Handle != IntPtr.Zero) ? LLVM.IsExternallyInitialized(this) != 0 : default;
            set => LLVM.SetExternallyInitialized(this, value ? 1 : 0);
        }

        public bool IsGlobalConstant
        {
            get => (Handle != IntPtr.Zero) ? LLVM.IsGlobalConstant(this) != 0 : default;
            set => LLVM.SetGlobalConstant(this, value ? 1 : 0);

        }

        public bool IsNull => (Handle != IntPtr.Zero) ? LLVM.IsNull(this) != 0 : default;

        public bool IsTailCall
        {
            get => (Handle != IntPtr.Zero) ? LLVM.IsTailCall(this) != 0 : default;
            set => LLVM.SetTailCall(this, IsTailCall ? 1 : 0);
        }

        public bool IsThreadLocal
        {
            get => (Handle != IntPtr.Zero) ? LLVM.IsThreadLocal(this) != 0 : default;
            set => LLVM.SetThreadLocal(this, value ? 1 : 0);
        }

        public bool IsUndef => (Handle != IntPtr.Zero) ? LLVM.IsUndef(this) != 0 : default;

        public LLVMBasicBlockRef LastBasicBlock => (Handle != IntPtr.Zero) ? LLVM.GetLastBasicBlock(this) : default;

        public LLVMValueRef LastParam => (Handle != IntPtr.Zero) ? LLVM.GetLastParam(this) : default;

        public LLVMLinkage Linkage
        {
            get => (Handle != IntPtr.Zero) ? LLVM.GetLinkage(this) : default;
            set => LLVM.SetLinkage(this, value);
        }

        public LLVMValueRef[] MDNodeOperands
        {
            get
            {
                if (Handle == IntPtr.Zero)
                {
                    return Array.Empty<LLVMValueRef>();
                }

                var Dest = new LLVMValueRef[MDNodeOperandsCount];

                fixed (LLVMValueRef* pDest = Dest)
                {
                    LLVM.GetMDNodeOperands(this, (LLVMOpaqueValue**)pDest);
                }

                return Dest;
            }
        }

        public uint MDNodeOperandsCount => (Handle != IntPtr.Zero) ? LLVM.GetMDNodeNumOperands(this) : default;

        public string Name
        {
            get
            {
                if (Handle == IntPtr.Zero)
                {
                    return string.Empty;
                }

                var pStr = LLVM.GetValueName(this);

                if (pStr is null)
                {
                    return string.Empty;
                }

                var span = new ReadOnlySpan<byte>(pStr, int.MaxValue);
                return span.Slice(0, span.IndexOf((byte)'\0')).AsString();
            }

            set
            {
                using var marshaledName = new MarshaledString(value);
                LLVM.SetValueName(this, marshaledName);
            }
        }

        public LLVMValueRef NextFunction => (Handle != IntPtr.Zero) ? LLVM.GetNextFunction(this) : default;

        public LLVMValueRef NextGlobal => (Handle != IntPtr.Zero) ? LLVM.GetNextGlobal(this) : default;

        public LLVMValueRef NextInstruction => (Handle != IntPtr.Zero) ? LLVM.GetNextInstruction(this) : default;

        public LLVMValueRef NextParam => (Handle != IntPtr.Zero) ? LLVM.GetNextParam(this) : default;

        public int OperandCount => (Handle != IntPtr.Zero) ? LLVM.GetNumOperands(this) : default;

        public LLVMValueRef[] Params
        {
            get
            {
                if (Handle == IntPtr.Zero)
                {
                    return Array.Empty<LLVMValueRef>();
                }

                var Params = new LLVMValueRef[ParamsCount];

                fixed (LLVMValueRef* pParams = Params)
                {
                    LLVM.GetParams(this, (LLVMOpaqueValue**)pParams);
                }

                return Params;
            }
        }

        public uint ParamsCount => (Handle != IntPtr.Zero) ? LLVM.CountParams(this) : default;

        public LLVMValueRef ParamParent => (Handle != IntPtr.Zero) ? LLVM.GetParamParent(this) : default;

        public LLVMValueRef PersonalityFn
        {
            get => (Handle != IntPtr.Zero) ? LLVM.GetPersonalityFn(this) : default;
            set => LLVM.SetPersonalityFn(this, value);
        }

        public LLVMValueRef PreviousGlobal => (Handle != IntPtr.Zero) ? LLVM.GetPreviousGlobal(this) : default;

        public LLVMValueRef PreviousInstruction => (Handle != IntPtr.Zero) ? LLVM.GetPreviousInstruction(this) : default;

        public LLVMValueRef PreviousParam => (Handle != IntPtr.Zero) ? LLVM.GetPreviousParam(this) : default;

        public LLVMValueRef PreviousFunction => (Handle != IntPtr.Zero) ? LLVM.GetPreviousFunction(this) : default;

        public string Section
        {
            get
            {
                if (Handle == IntPtr.Zero)
                {
                    return string.Empty;
                }

                var pSection = LLVM.GetSection(this);

                if (pSection is null)
                {
                    return string.Empty;
                }

                var span = new ReadOnlySpan<byte>(pSection, int.MaxValue);
                return span.Slice(0, span.IndexOf((byte)'\0')).AsString();
            }

            set
            {
                using var marshaledSection = new MarshaledString(value);
                LLVM.SetSection(this, marshaledSection);
            }
        }

        public uint SuccessorsCount => (Handle != IntPtr.Zero) ? LLVM.GetNumSuccessors(this) : default;

        public LLVMBasicBlockRef SwitchDefaultDest => (Handle != IntPtr.Zero) ? LLVM.GetSwitchDefaultDest(this) : default;

        public LLVMThreadLocalMode ThreadLocalMode
        {
            get => (Handle != IntPtr.Zero) ? LLVM.GetThreadLocalMode(this) : default;
            set => LLVM.SetThreadLocalMode(this, value);
        }

        public LLVMTypeRef TypeOf => (Handle != IntPtr.Zero) ? LLVM.TypeOf(this) : default;

        public LLVMVisibility Visibility
        {
            get => (Handle != IntPtr.Zero) ? LLVM.GetVisibility(this) : default;
            set => LLVM.SetVisibility(this, value);
        }

        public bool Volatile
        {
            get => (Handle != IntPtr.Zero) ? LLVM.GetVolatile(this) != 0 : default;
            set => LLVM.SetVolatile(this, value ? 1 : 0);
        }

        public static bool operator ==(LLVMValueRef left, LLVMValueRef right) => left.Handle == right.Handle;

        public static bool operator !=(LLVMValueRef left, LLVMValueRef right) => !(left == right);

        public static LLVMValueRef CreateConstAdd(LLVMValueRef LHSConstant, LLVMValueRef RHSConstant) => LLVM.ConstAdd(LHSConstant, RHSConstant);

        public static LLVMValueRef CreateConstAddrSpaceCast(LLVMValueRef ConstantVal, LLVMTypeRef ToType) => LLVM.ConstAddrSpaceCast(ConstantVal, ToType);

        public static LLVMValueRef CreateConstAllOnes(LLVMTypeRef Ty) => LLVM.ConstAllOnes(Ty);

        public static LLVMValueRef CreateConstAnd(LLVMValueRef LHSConstant, LLVMValueRef RHSConstant) => LLVM.ConstAnd(LHSConstant, RHSConstant);

        public static LLVMValueRef CreateConstArray(LLVMTypeRef ElementTy, LLVMValueRef[] ConstantVals)
        {
            fixed (LLVMValueRef* pConstantVals = ConstantVals)
            {
                return LLVM.ConstArray(ElementTy, (LLVMOpaqueValue**)pConstantVals, (uint)ConstantVals?.Length);
            }
        }

        public static LLVMValueRef CreateConstAShr(LLVMValueRef LHSConstant, LLVMValueRef RHSConstant) => LLVM.ConstAShr(LHSConstant, RHSConstant);

        public static LLVMValueRef CreateConstBitCast(LLVMValueRef ConstantVal, LLVMTypeRef ToType) => LLVM.ConstBitCast(ConstantVal, ToType);

        public static LLVMValueRef CreateConstExactSDiv(LLVMValueRef LHSConstant, LLVMValueRef RHSConstant) => LLVM.ConstExactSDiv(LHSConstant, RHSConstant);

        public static LLVMValueRef CreateConstExactUDiv(LLVMValueRef LHSConstant, LLVMValueRef RHSConstant) => LLVM.ConstExactUDiv(LHSConstant, RHSConstant);

        public static LLVMValueRef CreateConstExtractElement(LLVMValueRef VectorConstant, LLVMValueRef IndexConstant) => LLVM.ConstExtractElement(VectorConstant, IndexConstant);

        public static LLVMValueRef CreateConstExtractValue(LLVMValueRef AggConstant, uint[] IdxList)
        {
            fixed (uint* pIdxList = IdxList)
            {
                return LLVM.ConstExtractValue(AggConstant, pIdxList, (uint)IdxList?.Length);
            }
        }

        public static LLVMValueRef CreateConstFAdd(LLVMValueRef LHSConstant, LLVMValueRef RHSConstant) => LLVM.ConstFAdd(LHSConstant, RHSConstant);

        public static LLVMValueRef CreateConstFDiv(LLVMValueRef LHSConstant, LLVMValueRef RHSConstant) => LLVM.ConstFDiv(LHSConstant, RHSConstant);

        public static LLVMValueRef CreateConstFMul(LLVMValueRef LHSConstant, LLVMValueRef RHSConstant) => LLVM.ConstFMul(LHSConstant, RHSConstant);

        public static LLVMValueRef CreateConstFNeg(LLVMValueRef ConstantVal) => LLVM.ConstFNeg(ConstantVal);

        public static LLVMValueRef CreateConstFPCast(LLVMValueRef ConstantVal, LLVMTypeRef ToType) => LLVM.ConstFPCast(ConstantVal, ToType);

        public static LLVMValueRef CreateConstFPExt(LLVMValueRef ConstantVal, LLVMTypeRef ToType) => LLVM.ConstFPExt(ConstantVal, ToType);

        public static LLVMValueRef CreateConstFPToSI(LLVMValueRef ConstantVal, LLVMTypeRef ToType) => LLVM.ConstFPToSI(ConstantVal, ToType);

        public static LLVMValueRef CreateConstFPToUI(LLVMValueRef ConstantVal, LLVMTypeRef ToType) => LLVM.ConstFPToUI(ConstantVal, ToType);

        public static LLVMValueRef CreateConstFPTrunc(LLVMValueRef ConstantVal, LLVMTypeRef ToType) => LLVM.ConstFPTrunc(ConstantVal, ToType);

        public static LLVMValueRef CreateConstFRem(LLVMValueRef LHSConstant, LLVMValueRef RHSConstant) => LLVM.ConstFRem(LHSConstant, RHSConstant);

        public static LLVMValueRef CreateConstFSub(LLVMValueRef LHSConstant, LLVMValueRef RHSConstant) => LLVM.ConstFSub(LHSConstant, RHSConstant);

        public static LLVMValueRef CreateConstGEP(LLVMValueRef ConstantVal, LLVMValueRef[] ConstantIndices)
        {
            fixed (LLVMValueRef* pConstantIndices = ConstantIndices)
            {
                return LLVM.ConstGEP(ConstantVal, (LLVMOpaqueValue**)pConstantIndices, (uint)ConstantIndices?.Length);
            }
        }

        public static LLVMValueRef CreateConstInBoundsGEP(LLVMValueRef ConstantVal, LLVMValueRef[] ConstantIndices)
        {
            fixed (LLVMValueRef* pConstantIndices = ConstantIndices)
            {
                return LLVM.ConstInBoundsGEP(ConstantVal, (LLVMOpaqueValue**)pConstantIndices, (uint)ConstantIndices?.Length);
            }
        }

        public static LLVMValueRef CreateConstInlineAsm(LLVMTypeRef Ty, string AsmString, string Constraints, bool HasSideEffects, bool IsAlignStack)
        {
            using var marshaledAsmString = new MarshaledString(AsmString);
            using var marshaledConstraints = new MarshaledString(Constraints);
            return LLVM.ConstInlineAsm(Ty, marshaledAsmString, marshaledConstraints, HasSideEffects ? 1 : 0, IsAlignStack ? 1 : 0);
        }

        public static LLVMValueRef CreateConstInsertElement(LLVMValueRef VectorConstant, LLVMValueRef ElementValueConstant, LLVMValueRef IndexConstant) => LLVM.ConstInsertElement(VectorConstant, ElementValueConstant, IndexConstant);

        public static LLVMValueRef CreateConstInsertValue(LLVMValueRef AggConstant, LLVMValueRef ElementValueConstant, uint[] IdxList)
        {
            fixed (uint* pIdxList = IdxList)
            {
                return LLVM.ConstInsertValue(AggConstant, ElementValueConstant, pIdxList, (uint)IdxList?.Length);
            }
        }

        public static LLVMValueRef CreateConstInt(LLVMTypeRef IntTy, ulong N, bool SignExtend = false) => LLVM.ConstInt(IntTy, N, SignExtend ? 1 : 0);

        public static LLVMValueRef CreateConstIntCast(LLVMValueRef ConstantVal, LLVMTypeRef ToType, bool isSigned) => LLVM.ConstIntCast(ConstantVal, ToType, isSigned ? 1 : 0);

        public static LLVMValueRef CreateConstIntOfArbitraryPrecision(LLVMTypeRef IntTy, ulong[] Words)
        {
            fixed (ulong* pWords = Words)
            {
                return LLVM.ConstIntOfArbitraryPrecision(IntTy, (uint)Words?.Length, pWords);
            }
        }

        public static LLVMValueRef CreateConstIntOfString(LLVMTypeRef IntTy, string Text, byte Radix)
        {
            using var marshaledText = new MarshaledString(Text);
            return LLVM.ConstIntOfString(IntTy, marshaledText, Radix);
        }

        public static LLVMValueRef CreateConstIntOfStringAndSize(LLVMTypeRef IntTy, string Text, uint SLen, byte Radix)
        {
            using var marshaledText = new MarshaledString(Text);
            return LLVM.ConstIntOfStringAndSize(IntTy, marshaledText, SLen, Radix);
        }

        public static LLVMValueRef CreateConstIntToPtr(LLVMValueRef ConstantVal, LLVMTypeRef ToType) => LLVM.ConstIntToPtr(ConstantVal, ToType);

        public static LLVMValueRef CreateConstLShr(LLVMValueRef LHSConstant, LLVMValueRef RHSConstant) => LLVM.ConstLShr(LHSConstant, RHSConstant);

        public static LLVMValueRef CreateConstMul(LLVMValueRef LHSConstant, LLVMValueRef RHSConstant) => LLVM.ConstMul(LHSConstant, RHSConstant);

        public static LLVMValueRef CreateConstNamedStruct(LLVMTypeRef StructTy, LLVMValueRef[] ConstantVals)
        {
            fixed (LLVMValueRef* pConstantVals = ConstantVals)
            {
                return LLVM.ConstNamedStruct(StructTy, (LLVMOpaqueValue**)pConstantVals, (uint)ConstantVals?.Length);
            }
        }

        public static LLVMValueRef CreateConstNeg(LLVMValueRef ConstantVal) => LLVM.ConstNeg(ConstantVal);

        public static LLVMValueRef CreateConstNot(LLVMValueRef ConstantVal) => LLVM.ConstNot(ConstantVal);

        public static LLVMValueRef CreateConstNSWAdd(LLVMValueRef LHSConstant, LLVMValueRef RHSConstant) => LLVM.ConstNSWAdd(LHSConstant, RHSConstant);

        public static LLVMValueRef CreateConstNSWMul(LLVMValueRef LHSConstant, LLVMValueRef RHSConstant) => LLVM.ConstNSWMul(LHSConstant, RHSConstant);

        public static LLVMValueRef CreateConstNSWNeg(LLVMValueRef ConstantVal) => LLVM.ConstNSWNeg(ConstantVal);

        public static LLVMValueRef CreateConstNSWSub(LLVMValueRef LHSConstant, LLVMValueRef RHSConstant) => LLVM.ConstNSWSub(LHSConstant, RHSConstant);

        public static LLVMValueRef CreateConstNull(LLVMTypeRef Ty) => LLVM.ConstNull(Ty);

        public static LLVMValueRef CreateConstNUWAdd(LLVMValueRef LHSConstant, LLVMValueRef RHSConstant) => LLVM.ConstNUWAdd(LHSConstant, RHSConstant);

        public static LLVMValueRef CreateConstNUWMul(LLVMValueRef LHSConstant, LLVMValueRef RHSConstant) => LLVM.ConstNUWMul(LHSConstant, RHSConstant);

        public static LLVMValueRef CreateConstNUWNeg(LLVMValueRef ConstantVal) => LLVM.ConstNUWNeg(ConstantVal);

        public static LLVMValueRef CreateConstNUWSub(LLVMValueRef LHSConstant, LLVMValueRef RHSConstant) => LLVM.ConstNUWSub(LHSConstant, RHSConstant);

        public static LLVMValueRef CreateConstOr(LLVMValueRef LHSConstant, LLVMValueRef RHSConstant) => LLVM.ConstOr(LHSConstant, RHSConstant);

        public static LLVMValueRef CreateConstPointerCast(LLVMValueRef ConstantVal, LLVMTypeRef ToType) => LLVM.ConstPointerCast(ConstantVal, ToType);

        public static LLVMValueRef CreateConstPointerNull(LLVMTypeRef Ty) => LLVM.ConstPointerNull(Ty);

        public static LLVMValueRef CreateConstPtrToInt(LLVMValueRef ConstantVal, LLVMTypeRef ToType) => LLVM.ConstPtrToInt(ConstantVal, ToType);

        public static LLVMValueRef CreateConstReal(LLVMTypeRef RealTy, double N) => LLVM.ConstReal(RealTy, N);

        public static LLVMValueRef CreateConstRealOfString(LLVMTypeRef RealTy, string Text)
        {
            using var marshaledText = new MarshaledString(Text);
            return LLVM.ConstRealOfString(RealTy, marshaledText);
        }

        public static LLVMValueRef CreateConstRealOfStringAndSize(LLVMTypeRef RealTy, string Text, uint SLen)
        {
            using var marshaledText = new MarshaledString(Text);
            return LLVM.ConstRealOfStringAndSize(RealTy, marshaledText, SLen);
        }

        public static LLVMValueRef CreateConstSDiv(LLVMValueRef LHSConstant, LLVMValueRef RHSConstant) => LLVM.ConstSDiv(LHSConstant, RHSConstant);

        public static LLVMValueRef CreateConstSelect(LLVMValueRef ConstantCondition, LLVMValueRef ConstantIfTrue, LLVMValueRef ConstantIfFalse) => LLVM.ConstSelect(ConstantCondition, ConstantIfTrue, ConstantIfFalse);

        public static LLVMValueRef CreateConstSExt(LLVMValueRef ConstantVal, LLVMTypeRef ToType) => LLVM.ConstSExt(ConstantVal, ToType);

        public static LLVMValueRef CreateConstSExtOrBitCast(LLVMValueRef ConstantVal, LLVMTypeRef ToType) => LLVM.ConstSExtOrBitCast(ConstantVal, ToType);

        public static LLVMValueRef CreateConstShl(LLVMValueRef LHSConstant, LLVMValueRef RHSConstant) => LLVM.ConstShl(LHSConstant, RHSConstant);

        public static LLVMValueRef CreateConstShuffleVector(LLVMValueRef VectorAConstant, LLVMValueRef VectorBConstant, LLVMValueRef MaskConstant) => LLVM.ConstShuffleVector(VectorAConstant, VectorBConstant, MaskConstant);

        public static LLVMValueRef CreateConstSIToFP(LLVMValueRef ConstantVal, LLVMTypeRef ToType) => LLVM.ConstSIToFP(ConstantVal, ToType);

        public static LLVMValueRef CreateConstSRem(LLVMValueRef LHSConstant, LLVMValueRef RHSConstant) => LLVM.ConstSRem(LHSConstant, RHSConstant);

        public static LLVMValueRef CreateConstStruct(LLVMValueRef[] ConstantVals, bool Packed)
        {
            fixed (LLVMValueRef* pConstantVals = ConstantVals)
            {
                return LLVM.ConstStruct((LLVMOpaqueValue**)pConstantVals, (uint)ConstantVals?.Length, Packed ? 1 : 0);
            }
        }

        public static LLVMValueRef CreateConstSub(LLVMValueRef LHSConstant, LLVMValueRef RHSConstant) => LLVM.ConstSub(LHSConstant, RHSConstant);

        public static LLVMValueRef CreateConstTrunc(LLVMValueRef ConstantVal, LLVMTypeRef ToType) => LLVM.ConstTrunc(ConstantVal, ToType);

        public static LLVMValueRef CreateConstTruncOrBitCast(LLVMValueRef ConstantVal, LLVMTypeRef ToType) => LLVM.ConstTruncOrBitCast(ConstantVal, ToType);

        public static LLVMValueRef CreateConstUDiv(LLVMValueRef LHSConstant, LLVMValueRef RHSConstant) => LLVM.ConstUDiv(LHSConstant, RHSConstant);

        public static LLVMValueRef CreateConstUIToFP(LLVMValueRef ConstantVal, LLVMTypeRef ToType) => LLVM.ConstUIToFP(ConstantVal, ToType);

        public static LLVMValueRef CreateConstURem(LLVMValueRef LHSConstant, LLVMValueRef RHSConstant) => LLVM.ConstURem(LHSConstant, RHSConstant);

        public static LLVMValueRef CreateConstVector(LLVMValueRef[] ScalarConstantVars)
        {
            fixed (LLVMValueRef* pScalarConstantVars = ScalarConstantVars)
            {
                return LLVM.ConstVector((LLVMOpaqueValue**)pScalarConstantVars, (uint)ScalarConstantVars?.Length);
            }
        }

        public static LLVMValueRef CreateConstXor(LLVMValueRef LHSConstant, LLVMValueRef RHSConstant) => LLVM.ConstXor(LHSConstant, RHSConstant);

        public static LLVMValueRef CreateConstZExt(LLVMValueRef ConstantVal, LLVMTypeRef ToType) => LLVM.ConstZExt(ConstantVal, ToType);

        public static LLVMValueRef CreateConstZExtOrBitCast(LLVMValueRef ConstantVal, LLVMTypeRef ToType) => LLVM.ConstZExtOrBitCast(ConstantVal, ToType);

        public static LLVMValueRef CreateMDNode(LLVMValueRef[] Vals)
        {
            fixed (LLVMValueRef* pVals = Vals)
            {
                return LLVM.MDNode((LLVMOpaqueValue**)pVals, (uint)Vals?.Length);
            }
        }

        public void AddCase(LLVMValueRef OnVal, LLVMBasicBlockRef Dest) => LLVM.AddCase(this, OnVal, Dest);

        public void AddClause(LLVMValueRef ClauseVal) => LLVM.AddClause(this, ClauseVal);

        public void AddDestination(LLVMBasicBlockRef Dest) => LLVM.AddDestination(this, Dest);

        public void AddIncoming(LLVMValueRef[] IncomingValues, LLVMBasicBlockRef[] IncomingBlocks, uint Count)
        {
            fixed (LLVMValueRef* pIncomingValues = IncomingValues)
            fixed (LLVMBasicBlockRef* pIncomingBlocks = IncomingBlocks)
            {
                LLVM.AddIncoming(this, (LLVMOpaqueValue**)pIncomingValues, (LLVMOpaqueBasicBlock**)pIncomingBlocks, Count);
            }
        }

        public void AddTargetDependentFunctionAttr(string A, string V)
        {
            using var marshaledA = new MarshaledString(A);
            using var marshaledV = new MarshaledString(V);
            LLVM.AddTargetDependentFunctionAttr(this, marshaledA, marshaledV);
        }

        public LLVMBasicBlockRef AppendBasicBlock(string Name)
        {
            using var marshaledName = new MarshaledString(Name);
            return LLVM.AppendBasicBlock(this, marshaledName);
        }

        public void DeleteFunction() => LLVM.DeleteFunction(this);

        public void DeleteGlobal() => LLVM.DeleteGlobal(this);

        public void Dump() => LLVM.DumpValue(this);

        public override bool Equals(object obj) => obj is LLVMValueRef other && Equals(other);

        public bool Equals(LLVMValueRef other) => Handle == other.Handle;

        public string GetAsString(out UIntPtr Length)
        {
            fixed (UIntPtr* pLength = &Length)
            {
                var pStr = LLVM.GetAsString(this, pLength);

                if (pStr is null)
                {
                    return string.Empty;
                }

                var span = new ReadOnlySpan<byte>(pStr, (int)Length);
                return span.AsString();
            }
        }

        public LLVMAttributeRef[] GetAttributesAtIndex(LLVMAttributeIndex Idx)
        {
            var Attrs = new LLVMAttributeRef[GetAttributeCountAtIndex(Idx)];

            fixed (LLVMAttributeRef* pAttrs = Attrs)
            {
                LLVM.GetAttributesAtIndex(this, (uint)Idx, (LLVMOpaqueAttributeRef**)pAttrs);
            }

            return Attrs;
        }

        public uint GetAttributeCountAtIndex(LLVMAttributeIndex Idx) => LLVM.GetAttributeCountAtIndex(this, (uint)Idx);

        public LLVMValueRef GetBlockAddress(LLVMBasicBlockRef BB) => LLVM.BlockAddress(this, BB);

        public uint GetCallSiteAttributeCount(LLVMAttributeIndex Idx) => LLVM.GetCallSiteAttributeCount(this, (uint)Idx);

        public LLVMAttributeRef[] GetCallSiteAttributes(LLVMAttributeIndex Idx)
        {
            var Attrs = new LLVMAttributeRef[GetCallSiteAttributeCount(Idx)];

            fixed (LLVMAttributeRef* pAttrs = Attrs)
            {
                LLVM.GetCallSiteAttributes(this, (uint)Idx, (LLVMOpaqueAttributeRef**)pAttrs);
            }

            return Attrs;
        }

        public double GetConstRealDouble(out bool losesInfo)
        {
            int losesInfoOut;
            var result = LLVM.ConstRealGetDouble(this, &losesInfoOut);

            losesInfo = losesInfoOut != 0;
            return result;
        }

        public LLVMValueRef GetElementAsConstant(uint idx) => LLVM.GetElementAsConstant(this, idx);

        public override int GetHashCode() => Handle.GetHashCode();

        public LLVMBasicBlockRef GetIncomingBlock(uint Index) => LLVM.GetIncomingBlock(this, Index);

        public LLVMValueRef GetIncomingValue(uint Index) => LLVM.GetIncomingValue(this, Index);

        public string GetMDString(out uint Len)
        {
            fixed (uint* pLen = &Len)
            {
                var pMDStr = LLVM.GetMDString(this, pLen);

                if (pMDStr is null)
                {
                    return string.Empty;
                }

                var span = new ReadOnlySpan<byte>(pMDStr, (int)Len);
                return span.AsString();
            }
        }

        public LLVMValueRef GetMetadata(uint KindID) => LLVM.GetMetadata(this, KindID);

        public LLVMValueRef GetOperand(uint Index) => LLVM.GetOperand(this, Index);

        public LLVMUseRef GetOperandUse(uint Index) => LLVM.GetOperandUse(this, Index);

        public LLVMValueRef GetParam(uint Index) => LLVM.GetParam(this, Index);

        public LLVMBasicBlockRef GetSuccessor(uint i) => LLVM.GetSuccessor(this, i);

        public void InstructionEraseFromParent() => LLVM.InstructionEraseFromParent(this);

        public string PrintToString()
        {
            var pStr = LLVM.PrintValueToString(this);

            if (pStr is null)
            {
                return string.Empty;
            }
            var span = new ReadOnlySpan<byte>(pStr, int.MaxValue);

            var result = span.Slice(0, span.IndexOf((byte)'\0')).AsString();
            LLVM.DisposeMessage(pStr);
            return result;
        }

        public void ReplaceAllUsesWith(LLVMValueRef NewVal) => LLVM.ReplaceAllUsesWith(this, NewVal);

        public void SetInstrParamAlignment(uint index, uint align) => LLVM.SetInstrParamAlignment(this, index, align);

        public void SetMetadata(uint KindID, LLVMValueRef Node) => LLVM.SetMetadata(this, KindID, Node);

        public void SetOperand(uint Index, LLVMValueRef Val) => LLVM.SetOperand(this, Index, Val);

        public void SetParamAlignment(uint align) => LLVM.SetParamAlignment(this, align);

        public void SetSuccessor(uint i, LLVMBasicBlockRef block) => LLVM.SetSuccessor(this, i, block);

        public override string ToString() => (Handle != IntPtr.Zero) ? PrintToString() : string.Empty;

        public bool VerifyFunction(LLVMVerifierFailureAction Action) => LLVM.VerifyFunction(this, Action) == 0;

        public void ViewFunctionCFG() => LLVM.ViewFunctionCFG(this);

        public void ViewFunctionCFGOnly() => LLVM.ViewFunctionCFGOnly(this);
    }
}
