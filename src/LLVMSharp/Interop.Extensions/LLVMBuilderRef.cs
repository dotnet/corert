// Copyright (c) Microsoft and Contributors. All rights reserved. Licensed under the University of Illinois/NCSA Open Source License. See LICENSE.txt in the project root for license information.

using System;

namespace LLVMSharp.Interop
{
    public unsafe partial struct LLVMBuilderRef : IDisposable, IEquatable<LLVMBuilderRef>
    {
        public LLVMBuilderRef(IntPtr handle)
        {
            Handle = handle;
        }

        public IntPtr Handle;

        public static implicit operator LLVMBuilderRef(LLVMOpaqueBuilder* Builder)
        {
            return new LLVMBuilderRef((IntPtr)Builder);
        }

        public static implicit operator LLVMOpaqueBuilder*(LLVMBuilderRef Builder)
        {
            return (LLVMOpaqueBuilder*)Builder.Handle;
        }

        public LLVMValueRef CurrentDebugLocation
        {
            get => (Handle != IntPtr.Zero) ? LLVM.GetCurrentDebugLocation(this) : default;
            set => LLVM.SetCurrentDebugLocation(this, value);
        }

        public LLVMBasicBlockRef InsertBlock => (Handle != IntPtr.Zero) ? LLVM.GetInsertBlock(this) : default;

        public static bool operator ==(LLVMBuilderRef left, LLVMBuilderRef right) => left.Equals(right);

        public static bool operator !=(LLVMBuilderRef left, LLVMBuilderRef right) => !(left == right);

        public LLVMValueRef BuildAdd(LLVMValueRef LHS, LLVMValueRef RHS, string Name = "")
        {
            using var marshaledName = new MarshaledString(Name);
            return LLVM.BuildAdd(this, LHS, RHS, marshaledName);
        }

        public LLVMValueRef BuildAddrSpaceCast(LLVMValueRef Val, LLVMTypeRef DestTy, string Name = "")
        {
            using var marshaledName = new MarshaledString(Name);
            return LLVM.BuildAddrSpaceCast(this, Val, DestTy, marshaledName);
        }

        public LLVMValueRef BuildAggregateRet(LLVMValueRef[] RetVals)
        {
            fixed (LLVMValueRef* pRetVals = RetVals)
            {
                return LLVM.BuildAggregateRet(this, (LLVMOpaqueValue**)pRetVals, (uint)RetVals?.Length);
            }
        }

        public LLVMValueRef BuildAlloca(LLVMTypeRef Ty, string Name = "")
        {
            using var marshaledName = new MarshaledString(Name);
            return LLVM.BuildAlloca(this, Ty, marshaledName);
        }

        public LLVMValueRef BuildAnd(LLVMValueRef LHS, LLVMValueRef RHS, string Name = "")
        {
            using var marshaledName = new MarshaledString(Name);
            return LLVM.BuildAnd(this, LHS, RHS, marshaledName);
        }

        public LLVMValueRef BuildArrayAlloca(LLVMTypeRef Ty, LLVMValueRef Val, string Name = "")
        {
            using var marshaledName = new MarshaledString(Name);
            return LLVM.BuildArrayAlloca(this, Ty, Val, marshaledName);
        }

        public LLVMValueRef BuildArrayMalloc(LLVMTypeRef Ty, LLVMValueRef Val, string Name = "")
        {
            using var marshaledName = new MarshaledString(Name);
            return LLVM.BuildArrayMalloc(this, Ty, Val, marshaledName);
        }

        public LLVMValueRef BuildAShr(LLVMValueRef LHS, LLVMValueRef RHS, string Name = "")
        {
            using var marshaledName = new MarshaledString(Name);
            return LLVM.BuildAShr(this, LHS, RHS, marshaledName);
        }

        public LLVMValueRef BuildAtomicRMW(LLVMAtomicRMWBinOp op, LLVMValueRef PTR, LLVMValueRef Val, LLVMAtomicOrdering ordering, bool singleThread) => LLVM.BuildAtomicRMW(this, op, PTR, Val, ordering, singleThread ? 1 : 0);

        public LLVMValueRef BuildBinOp(LLVMOpcode Op, LLVMValueRef LHS, LLVMValueRef RHS, string Name = "")
        {
            using var marshaledName = new MarshaledString(Name);
            return LLVM.BuildBinOp(this, Op, LHS, RHS, marshaledName);
        }

        public LLVMValueRef BuildBitCast(LLVMValueRef Val, LLVMTypeRef DestTy, string Name = "")
        {
            using var marshaledName = new MarshaledString(Name);
            return LLVM.BuildBitCast(this, Val, DestTy, marshaledName);
        }

        public LLVMValueRef BuildBr(LLVMBasicBlockRef Dest) => LLVM.BuildBr(this, Dest);

        public LLVMValueRef BuildCall(LLVMValueRef Fn, LLVMValueRef[] Args, string Name = "")
        {
            fixed (LLVMValueRef* pArgs = Args)
            {
                using var marshaledName = new MarshaledString(Name);
                return LLVM.BuildCall(this, Fn, (LLVMOpaqueValue**)pArgs, (uint)Args?.Length, marshaledName);
            }
        }

        public LLVMValueRef BuildCast(LLVMOpcode Op, LLVMValueRef Val, LLVMTypeRef DestTy, string Name = "")
        {
            using var marshaledName = new MarshaledString(Name);
            return LLVM.BuildCast(this, Op, Val, DestTy, marshaledName);
        }

        public LLVMValueRef BuildCondBr(LLVMValueRef If, LLVMBasicBlockRef Then, LLVMBasicBlockRef Else) => LLVM.BuildCondBr(this, If, Then, Else);

        public LLVMValueRef BuildExactSDiv(LLVMValueRef LHS, LLVMValueRef RHS, string Name = "")
        {
            using var marshaledName = new MarshaledString(Name);
            return LLVM.BuildExactSDiv(this, LHS, RHS, marshaledName);
        }

        public LLVMValueRef BuildExtractElement(LLVMValueRef VecVal, LLVMValueRef Index, string Name = "")
        {
            using var marshaledName = new MarshaledString(Name);
            return LLVM.BuildExtractElement(this, VecVal, Index, marshaledName);
        }

        public LLVMValueRef BuildExtractValue(LLVMValueRef AggVal, uint Index, string Name = "")
        {
            using var marshaledName = new MarshaledString(Name);
            return LLVM.BuildExtractValue(this, AggVal, Index, marshaledName);
        }

        public LLVMValueRef BuildFAdd(LLVMValueRef LHS, LLVMValueRef RHS, string Name = "")
        {
            using var marshaledName = new MarshaledString(Name);
            return LLVM.BuildFAdd(this, LHS, RHS, marshaledName);
        }

        public LLVMValueRef BuildFCmp(LLVMRealPredicate Op, LLVMValueRef LHS, LLVMValueRef RHS, string Name = "")
        {
            using var marshaledName = new MarshaledString(Name);
            return LLVM.BuildFCmp(this, Op, LHS, RHS, marshaledName);
        }

        public LLVMValueRef BuildFDiv(LLVMValueRef LHS, LLVMValueRef RHS, string Name = "")
        {
            using var marshaledName = new MarshaledString(Name);
            return LLVM.BuildFDiv(this, LHS, RHS, marshaledName);
        }

        public LLVMValueRef BuildFence(LLVMAtomicOrdering ordering, bool singleThread, string Name = "")
        {
            using var marshaledName = new MarshaledString(Name);
            return LLVM.BuildFence(this, ordering, singleThread ? 1 : 0, marshaledName);
        }

        public LLVMValueRef BuildFMul(LLVMValueRef LHS, LLVMValueRef RHS, string Name = "")
        {
            using var marshaledName = new MarshaledString(Name);
            return LLVM.BuildFMul(this, LHS, RHS, marshaledName);
        }

        public LLVMValueRef BuildFNeg(LLVMValueRef V, string Name = "")
        {
            using var marshaledName = new MarshaledString(Name);
            return LLVM.BuildFNeg(this, V, marshaledName);
        }

        public LLVMValueRef BuildFPCast(LLVMValueRef Val, LLVMTypeRef DestTy, string Name = "")
        {
            using var marshaledName = new MarshaledString(Name);
            return LLVM.BuildFPCast(this, Val, DestTy, marshaledName);
        }

        public LLVMValueRef BuildFPExt(LLVMValueRef Val, LLVMTypeRef DestTy, string Name = "")
        {
            using var marshaledName = new MarshaledString(Name);
            return LLVM.BuildFPExt(this, Val, DestTy, marshaledName);
        }

        public LLVMValueRef BuildFPToSI(LLVMValueRef Val, LLVMTypeRef DestTy, string Name = "")
        {
            using var marshaledName = new MarshaledString(Name);
            return LLVM.BuildFPToSI(this, Val, DestTy, marshaledName);
        }

        public LLVMValueRef BuildFPToUI(LLVMValueRef Val, LLVMTypeRef DestTy, string Name = "")
        {
            using var marshaledName = new MarshaledString(Name);
            return LLVM.BuildFPToUI(this, Val, DestTy, marshaledName);
        }

        public LLVMValueRef BuildFPTrunc(LLVMValueRef Val, LLVMTypeRef DestTy, string Name = "")
        {
            using var marshaledName = new MarshaledString(Name);
            return LLVM.BuildFPTrunc(this, Val, DestTy, marshaledName);
        }

        public LLVMValueRef BuildFree(LLVMValueRef PointerVal) => LLVM.BuildFree(this, PointerVal);

        public LLVMValueRef BuildFRem(LLVMValueRef LHS, LLVMValueRef RHS, string Name = "")
        {
            using var marshaledName = new MarshaledString(Name);
            return LLVM.BuildFRem(this, LHS, RHS, marshaledName);
        }

        public LLVMValueRef BuildFSub(LLVMValueRef LHS, LLVMValueRef RHS, string Name = "")
        {
            using var marshaledName = new MarshaledString(Name);
            return LLVM.BuildFSub(this, LHS, RHS, marshaledName);
        }

        public LLVMValueRef BuildGEP(LLVMValueRef Pointer, LLVMValueRef[] Indices, string Name = "")
        {
            fixed (LLVMValueRef* pIndices = Indices)
            {
                using var marshaledName = new MarshaledString(Name);
                return LLVM.BuildGEP(this, Pointer, (LLVMOpaqueValue**)pIndices, (uint)Indices?.Length, marshaledName);
            }
        }

        public LLVMValueRef BuildGlobalString(string Str, string Name = "")
        {
            using var marshaledStr = new MarshaledString(Str);
            using var marshaledName = new MarshaledString(Name);
            return LLVM.BuildGlobalString(this, marshaledStr, marshaledName);
        }

        public LLVMValueRef BuildGlobalStringPtr(string Str, string Name = "")
        {
            using var marshaledStr = new MarshaledString(Str);
            using var marshaledName = new MarshaledString(Name);
            return LLVM.BuildGlobalStringPtr(this, marshaledStr, marshaledName);
        }

        public LLVMValueRef BuildICmp(LLVMIntPredicate Op, LLVMValueRef LHS, LLVMValueRef RHS, string Name = "")
        {
            using var marshaledName = new MarshaledString(Name);
            return LLVM.BuildICmp(this, Op, LHS, RHS, marshaledName);
        }

        public LLVMValueRef BuildInBoundsGEP(LLVMValueRef Pointer, LLVMValueRef[] Indices, string Name = "")
        {
            fixed (LLVMValueRef* pIndices = Indices)
            {
                using var marshaledName = new MarshaledString(Name);
                return LLVM.BuildInBoundsGEP(this, Pointer, (LLVMOpaqueValue**)pIndices, (uint)Indices?.Length, marshaledName);
            }
        }

        public LLVMValueRef BuildIndirectBr(LLVMValueRef Addr, uint NumDests) => LLVM.BuildIndirectBr(this, Addr, NumDests);

        public LLVMValueRef BuildInsertElement(LLVMValueRef VecVal, LLVMValueRef EltVal, LLVMValueRef Index, string Name = "")
        {
            using var marshaledName = new MarshaledString(Name);
            return LLVM.BuildInsertElement(this, VecVal, EltVal, Index, marshaledName);
        }

        public LLVMValueRef BuildInsertValue(LLVMValueRef AggVal, LLVMValueRef EltVal, uint Index, string Name = "")
        {
            using var marshaledName = new MarshaledString(Name);
            return LLVM.BuildInsertValue(this, AggVal, EltVal, Index, marshaledName);
        }

        public LLVMValueRef BuildIntCast(LLVMValueRef Val, LLVMTypeRef DestTy, string Name = "")
        {
            using var marshaledName = new MarshaledString(Name);
            return LLVM.BuildIntCast(this, Val, DestTy, marshaledName);
        }

        public LLVMValueRef BuildIntToPtr(LLVMValueRef Val, LLVMTypeRef DestTy, string Name = "")
        {
            using var marshaledName = new MarshaledString(Name);
            return LLVM.BuildIntToPtr(this, Val, DestTy, marshaledName);
        }

        public LLVMValueRef BuildInvoke(LLVMValueRef Fn, LLVMValueRef[] Args, LLVMBasicBlockRef Then, LLVMBasicBlockRef Catch, string Name = "")
        {
            fixed (LLVMValueRef* pArgs = Args)
            {
                using var marshaledName = new MarshaledString(Name);
                return LLVM.BuildInvoke(this, Fn, (LLVMOpaqueValue**)pArgs, (uint)Args?.Length, Then, Catch, marshaledName);
            }
        }

        public LLVMValueRef BuildIsNotNull(LLVMValueRef Val, string Name = "")
        {
            using var marshaledName = new MarshaledString(Name);
            return LLVM.BuildIsNotNull(this, Val, marshaledName);
        }

        public LLVMValueRef BuildIsNull(LLVMValueRef Val, string Name = "")
        {
            using var marshaledName = new MarshaledString(Name);
            return LLVM.BuildIsNull(this, Val, marshaledName);
        }

        public LLVMValueRef BuildLandingPad(LLVMTypeRef Ty, LLVMValueRef PersFn, uint NumClauses, string Name = "")
        {
            using var marshaledName = new MarshaledString(Name);
            return LLVM.BuildLandingPad(this, Ty, PersFn, NumClauses, marshaledName);
        }

        public LLVMValueRef BuildLoad(LLVMValueRef PointerVal, string Name = "")
        {
            using var marshaledName = new MarshaledString(Name);
            return LLVM.BuildLoad(this, PointerVal, marshaledName);
        }

        public LLVMValueRef BuildLShr(LLVMValueRef LHS, LLVMValueRef RHS, string Name = "")
        {
            using var marshaledName = new MarshaledString(Name);
            return LLVM.BuildLShr(this, LHS, RHS, marshaledName);
        }

        public LLVMValueRef BuildMalloc(LLVMTypeRef Ty, string Name = "")
        {
            using var marshaledName = new MarshaledString(Name);
            return LLVM.BuildMalloc(this, Ty, marshaledName);
        }

        public LLVMValueRef BuildMul(LLVMValueRef LHS, LLVMValueRef RHS, string Name = "")
        {
            using var marshaledName = new MarshaledString(Name);
            return LLVM.BuildMul(this, LHS, RHS, marshaledName);
        }

        public LLVMValueRef BuildNeg(LLVMValueRef V, string Name = "")
        {
            using var marshaledName = new MarshaledString(Name);
            return LLVM.BuildNeg(this, V, marshaledName);
        }

        public LLVMValueRef BuildNot(LLVMValueRef V, string Name = "")
        {
            using var marshaledName = new MarshaledString(Name);
            return LLVM.BuildNot(this, V, marshaledName);
        }

        public LLVMValueRef BuildNSWAdd(LLVMValueRef LHS, LLVMValueRef RHS, string Name = "")
        {
            using var marshaledName = new MarshaledString(Name);
            return LLVM.BuildNSWAdd(this, LHS, RHS, marshaledName);
        }

        public LLVMValueRef BuildNSWMul(LLVMValueRef LHS, LLVMValueRef RHS, string Name = "")
        {
            using var marshaledName = new MarshaledString(Name);
            return LLVM.BuildNSWMul(this, LHS, RHS, marshaledName);
        }

        public LLVMValueRef BuildNSWNeg(LLVMValueRef V, string Name = "")
        {
            using var marshaledName = new MarshaledString(Name);
            return LLVM.BuildNSWNeg(this, V, marshaledName);
        }

        public LLVMValueRef BuildNSWSub(LLVMValueRef LHS, LLVMValueRef RHS, string Name = "")
        {
            using var marshaledName = new MarshaledString(Name);
            return LLVM.BuildNSWSub(this, LHS, RHS, marshaledName);
        }

        public LLVMValueRef BuildNUWAdd(LLVMValueRef LHS, LLVMValueRef RHS, string Name = "")
        {
            using var marshaledName = new MarshaledString(Name);
            return LLVM.BuildNUWAdd(this, LHS, RHS, marshaledName);
        }

        public LLVMValueRef BuildNUWMul(LLVMValueRef LHS, LLVMValueRef RHS, string Name = "")
        {
            using var marshaledName = new MarshaledString(Name);
            return LLVM.BuildNUWMul(this, LHS, RHS, marshaledName);
        }

        public LLVMValueRef BuildNUWNeg(LLVMValueRef V, string Name = "")
        {
            using var marshaledName = new MarshaledString(Name);
            return LLVM.BuildNUWNeg(this, V, marshaledName);
        }

        public LLVMValueRef BuildNUWSub(LLVMValueRef LHS, LLVMValueRef RHS, string Name = "")
        {
            using var marshaledName = new MarshaledString(Name);
            return LLVM.BuildNUWSub(this, LHS, RHS, marshaledName);
        }

        public LLVMValueRef BuildOr(LLVMValueRef LHS, LLVMValueRef RHS, string Name = "")
        {
            using var marshaledName = new MarshaledString(Name);
            return LLVM.BuildOr(this, LHS, RHS, marshaledName);
        }

        public LLVMValueRef BuildPhi(LLVMTypeRef Ty, string Name = "")
        {
            using var marshaledName = new MarshaledString(Name);
            return LLVM.BuildPhi(this, Ty, marshaledName);
        }

        public LLVMValueRef BuildPointerCast(LLVMValueRef Val, LLVMTypeRef DestTy, string Name = "")
        {
            using var marshaledName = new MarshaledString(Name);
            return LLVM.BuildPointerCast(this, Val, DestTy, marshaledName);
        }

        public LLVMValueRef BuildPtrDiff(LLVMValueRef LHS, LLVMValueRef RHS, string Name = "")
        {
            using var marshaledName = new MarshaledString(Name);
            return LLVM.BuildPtrDiff(this, LHS, RHS, marshaledName);
        }

        public LLVMValueRef BuildPtrToInt(LLVMValueRef Val, LLVMTypeRef DestTy, string Name = "")
        {
            using var marshaledName = new MarshaledString(Name);
            return LLVM.BuildPtrToInt(this, Val, DestTy, marshaledName);
        }

        public LLVMValueRef BuildResume(LLVMValueRef Exn) => LLVM.BuildResume(this, Exn);

        public LLVMValueRef BuildRet(LLVMValueRef V) => LLVM.BuildRet(this, V);

        public LLVMValueRef BuildRetVoid() => LLVM.BuildRetVoid(this);

        public LLVMValueRef BuildSDiv(LLVMValueRef LHS, LLVMValueRef RHS, string Name = "")
        {
            using var marshaledName = new MarshaledString(Name);
            return LLVM.BuildSDiv(this, LHS, RHS, marshaledName);
        }

        public LLVMValueRef BuildSelect(LLVMValueRef If, LLVMValueRef Then, LLVMValueRef Else, string Name = "")
        {
            using var marshaledName = new MarshaledString(Name);
            return LLVM.BuildSelect(this, If, Then, Else, marshaledName);
        }

        public LLVMValueRef BuildSExt(LLVMValueRef Val, LLVMTypeRef DestTy, string Name = "")
        {
            using var marshaledName = new MarshaledString(Name);
            return LLVM.BuildSExt(this, Val, DestTy, marshaledName);
        }

        public LLVMValueRef BuildSExtOrBitCast(LLVMValueRef Val, LLVMTypeRef DestTy, string Name = "")
        {
            using var marshaledName = new MarshaledString(Name);
            return LLVM.BuildSExtOrBitCast(this, Val, DestTy, marshaledName);
        }

        public LLVMValueRef BuildShl(LLVMValueRef LHS, LLVMValueRef RHS, string Name = "")
        {
            using var marshaledName = new MarshaledString(Name);
            return LLVM.BuildShl(this, LHS, RHS, marshaledName);
        }

        public LLVMValueRef BuildShuffleVector(LLVMValueRef V1, LLVMValueRef V2, LLVMValueRef Mask, string Name = "")
        {
            using var marshaledName = new MarshaledString(Name);
            return LLVM.BuildShuffleVector(this, V1, V2, Mask, marshaledName);
        }

        public LLVMValueRef BuildSIToFP(LLVMValueRef Val, LLVMTypeRef DestTy, string Name = "")
        {
            using var marshaledName = new MarshaledString(Name);
            return LLVM.BuildSIToFP(this, Val, DestTy, marshaledName);
        }

        public LLVMValueRef BuildSRem(LLVMValueRef LHS, LLVMValueRef RHS, string Name = "")
        {
            using var marshaledName = new MarshaledString(Name);
            return LLVM.BuildSRem(this, LHS, RHS, marshaledName);
        }

        public LLVMValueRef BuildStore(LLVMValueRef Val, LLVMValueRef Ptr) => LLVM.BuildStore(this, Val, Ptr);

        public LLVMValueRef BuildStructGEP(LLVMValueRef Pointer, uint Idx, string Name = "")
        {
            using var marshaledName = new MarshaledString(Name);
            return LLVM.BuildStructGEP(this, Pointer, Idx, marshaledName);
        }

        public LLVMValueRef BuildSub(LLVMValueRef LHS, LLVMValueRef RHS, string Name = "")
        {
            using var marshaledName = new MarshaledString(Name);
            return LLVM.BuildSub(this, LHS, RHS, marshaledName);
        }

        public LLVMValueRef BuildSwitch(LLVMValueRef V, LLVMBasicBlockRef Else, uint NumCases) => LLVM.BuildSwitch(this, V, Else, NumCases);

        public LLVMValueRef BuildTrunc(LLVMValueRef Val, LLVMTypeRef DestTy, string Name = "")
        {
            using var marshaledName = new MarshaledString(Name);
            return LLVM.BuildTrunc(this, Val, DestTy, marshaledName);
        }

        public LLVMValueRef BuildTruncOrBitCast(LLVMValueRef Val, LLVMTypeRef DestTy, string Name = "")
        {
            using var marshaledName = new MarshaledString(Name);
            return LLVM.BuildTruncOrBitCast(this, Val, DestTy, marshaledName);
        }

        public LLVMValueRef BuildUDiv(LLVMValueRef LHS, LLVMValueRef RHS, string Name = "")
        {
            using var marshaledName = new MarshaledString(Name);
            return LLVM.BuildUDiv(this, LHS, RHS, marshaledName);
        }

        public LLVMValueRef BuildUIToFP(LLVMValueRef Val, LLVMTypeRef DestTy, string Name = "")
        {
            using var marshaledName = new MarshaledString(Name);
            return LLVM.BuildUIToFP(this, Val, DestTy, marshaledName);
        }

        public LLVMValueRef BuildUnreachable() => LLVM.BuildUnreachable(this);

        public LLVMValueRef BuildURem(LLVMValueRef LHS, LLVMValueRef RHS, string Name = "")
        {
            using var marshaledName = new MarshaledString(Name);
            return LLVM.BuildURem(this, LHS, RHS, marshaledName);
        }

        public LLVMValueRef BuildVAArg(LLVMValueRef List, LLVMTypeRef Ty, string Name = "")
        {
            using var marshaledName = new MarshaledString(Name);
            return LLVM.BuildVAArg(this, List, Ty, marshaledName);
        }

        public LLVMValueRef BuildXor(LLVMValueRef LHS, LLVMValueRef RHS, string Name = "")
        {
            using var marshaledName = new MarshaledString(Name);
            return LLVM.BuildXor(this, LHS, RHS, marshaledName);
        }

        public LLVMValueRef BuildZExt(LLVMValueRef Val, LLVMTypeRef DestTy, string Name = "")
        {
            using var marshaledName = new MarshaledString(Name);
            return LLVM.BuildZExt(this, Val, DestTy, marshaledName);
        }

        public LLVMValueRef BuildZExtOrBitCast(LLVMValueRef Val, LLVMTypeRef DestTy, string Name = "")
        {
            using var marshaledName = new MarshaledString(Name);
            return LLVM.BuildZExtOrBitCast(this, Val, DestTy, marshaledName);
        }
        public void ClearInsertionPosition() => LLVM.ClearInsertionPosition(this);

        public void Dispose()
        {
            if (Handle != IntPtr.Zero)
            {
                LLVM.DisposeBuilder(this);
                Handle = IntPtr.Zero;
            }
        }

        public override bool Equals(object obj) => obj is LLVMBuilderRef other && Equals(other);

        public bool Equals(LLVMBuilderRef other) => Handle == other.Handle;

        public override int GetHashCode() => Handle.GetHashCode();

        public void Insert(LLVMValueRef Instr) => LLVM.InsertIntoBuilder(this, Instr);

        public void InsertWithName(LLVMValueRef Instr, string Name = "")
        {
            using var marshaledName = new MarshaledString(Name);
            LLVM.InsertIntoBuilderWithName(this, Instr, marshaledName);
        }

        public void Position(LLVMBasicBlockRef Block, LLVMValueRef Instr) => LLVM.PositionBuilder(this, Block, Instr);

        public void PositionAtEnd(LLVMBasicBlockRef Block) => LLVM.PositionBuilderAtEnd(this, Block);

        public void PositionBefore(LLVMValueRef Instr) => LLVM.PositionBuilderBefore(this, Instr);

        public void SetInstDebugLocation(LLVMValueRef Inst) => LLVM.SetInstDebugLocation(this, Inst);
    }
}
