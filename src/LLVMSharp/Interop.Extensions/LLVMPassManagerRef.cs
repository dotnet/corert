// Copyright (c) Microsoft and Contributors. All rights reserved. Licensed under the University of Illinois/NCSA Open Source License. See LICENSE.txt in the project root for license information.

using System;

namespace LLVMSharp.Interop
{
    public unsafe partial struct LLVMPassManagerRef : IDisposable, IEquatable<LLVMPassManagerRef>
    {
        public LLVMPassManagerRef(IntPtr handle)
        {
            Handle = handle;
        }

        public IntPtr Handle;

        public static implicit operator LLVMPassManagerRef(LLVMOpaquePassManager* value)
        {
            return new LLVMPassManagerRef((IntPtr)value);
        }

        public static implicit operator LLVMOpaquePassManager*(LLVMPassManagerRef value)
        {
            return (LLVMOpaquePassManager*)value.Handle;
        }

        public static bool operator ==(LLVMPassManagerRef left, LLVMPassManagerRef right) => left.Handle == right.Handle;

        public static bool operator !=(LLVMPassManagerRef left, LLVMPassManagerRef right) => !(left == right);

        public static LLVMPassManagerRef Create() => LLVM.CreatePassManager();

        public void AddAggressiveDCEPass() => LLVM.AddAggressiveDCEPass(this);

        public void AddAlignmentFromAssumptionsPass() => LLVM.AddAlignmentFromAssumptionsPass(this);

        public void AddAlwaysInlinerPass() => LLVM.AddAlwaysInlinerPass(this);

        public void AddArgumentPromotionPass() => LLVM.AddArgumentPromotionPass(this);

        public void AddBasicAliasAnalysisPass() => LLVM.AddBasicAliasAnalysisPass(this);

        public void AddBitTrackingDCEPass() => LLVM.AddBitTrackingDCEPass(this);

        public void AddCalledValuePropagationPass() => LLVM.AddCalledValuePropagationPass(this);

        public void AddCFGSimplificationPass() => LLVM.AddCFGSimplificationPass(this);

        public void AddConstantMergePass() => LLVM.AddConstantMergePass(this);

        public void AddConstantPropagationPass() => LLVM.AddConstantPropagationPass(this);

        public void AddCorrelatedValuePropagationPass() => LLVM.AddCorrelatedValuePropagationPass(this);

        public void AddDeadArgEliminationPass() => LLVM.AddDeadArgEliminationPass(this);

        public void AddDeadStoreEliminationPass() => LLVM.AddDeadStoreEliminationPass(this);

        public void AddDemoteMemoryToRegisterPass() => LLVM.AddDemoteMemoryToRegisterPass(this);

        public void AddEarlyCSEMemSSAPass() => LLVM.AddEarlyCSEMemSSAPass(this);

        public void AddEarlyCSEPass() => LLVM.AddEarlyCSEPass(this);

        public void AddFunctionAttrsPass() => LLVM.AddFunctionAttrsPass(this);

        public void AddFunctionInliningPass() => LLVM.AddFunctionInliningPass(this);

        public void AddGlobalDCEPass() => LLVM.AddGlobalDCEPass(this);

        public void AddGlobalOptimizerPass() => LLVM.AddGlobalOptimizerPass(this);

        public void AddGVNPass() => LLVM.AddGVNPass(this);

        public void AddIndVarSimplifyPass() => LLVM.AddIndVarSimplifyPass(this);

        public void AddInstructionCombiningPass() => LLVM.AddInstructionCombiningPass(this);

        public void AddInternalizePass(uint AllButMain) => LLVM.AddInternalizePass(this, AllButMain);

        public void AddIPConstantPropagationPass() => LLVM.AddIPConstantPropagationPass(this);

        public void AddIPSCCPPass() => LLVM.AddIPSCCPPass(this);

        public void AddJumpThreadingPass() => LLVM.AddJumpThreadingPass(this);

        public void AddLICMPass() => LLVM.AddLICMPass(this);

        public void AddLoopDeletionPass() => LLVM.AddLoopDeletionPass(this);

        public void AddLoopIdiomPass() => LLVM.AddLoopIdiomPass(this);

        public void AddLoopRerollPass() => LLVM.AddLoopRerollPass(this);

        public void AddLoopRotatePass() => LLVM.AddLoopRotatePass(this);

        public void AddLoopUnrollPass() => LLVM.AddLoopUnrollPass(this);

        public void AddLoopUnswitchPass() => LLVM.AddLoopUnswitchPass(this);

        public void AddLoopVectorizePass() => LLVM.AddLoopVectorizePass(this);

        public void AddLowerExpectIntrinsicPass() => LLVM.AddLowerExpectIntrinsicPass(this);

        public void AddLowerSwitchPass() => LLVM.AddLowerSwitchPass(this);

        public void AddMemCpyOptPass() => LLVM.AddMemCpyOptPass(this);

        public void AddMergedLoadStoreMotionPass() => LLVM.AddMergedLoadStoreMotionPass(this);

        public void AddNewGVNPass() => LLVM.AddNewGVNPass(this);

        public void AddPartiallyInlineLibCallsPass() => LLVM.AddPartiallyInlineLibCallsPass(this);

        public void AddPromoteMemoryToRegisterPass() => LLVM.AddPromoteMemoryToRegisterPass(this);

        public void AddPruneEHPass() => LLVM.AddPruneEHPass(this);

        public void AddReassociatePass() => LLVM.AddReassociatePass(this);

        public void AddScalarizerPass() => LLVM.AddScalarizerPass(this);

        public void AddScalarReplAggregatesPass() => LLVM.AddScalarReplAggregatesPass(this);

        public void AddScalarReplAggregatesPassSSA() => LLVM.AddScalarReplAggregatesPassSSA(this);

        public void AddScalarReplAggregatesPassWithThreshold(int Threshold) => LLVM.AddScalarReplAggregatesPassWithThreshold(this, Threshold);

        public void AddSCCPPass() => LLVM.AddSCCPPass(this);

        public void AddScopedNoAliasAAPass() => LLVM.AddScopedNoAliasAAPass(this);

        public void AddSimplifyLibCallsPass() => LLVM.AddSimplifyLibCallsPass(this);

        public void AddSLPVectorizePass() => LLVM.AddSLPVectorizePass(this);

        public void AddStripDeadPrototypesPass() => LLVM.AddStripDeadPrototypesPass(this);

        public void AddStripSymbolsPass() => LLVM.AddStripSymbolsPass(this);

        public void AddTailCallEliminationPass() => LLVM.AddTailCallEliminationPass(this);

        public void AddTypeBasedAliasAnalysisPass() => LLVM.AddTypeBasedAliasAnalysisPass(this);

        public void AddVerifierPass() => LLVM.AddVerifierPass(this);

        public void Dispose()
        {
            if (Handle != IntPtr.Zero)
            {
                LLVM.DisposePassManager(this);
                Handle = IntPtr.Zero;
            }
        }

        public override bool Equals(object obj) => obj is LLVMPassManagerRef other && Equals(other);

        public bool Equals(LLVMPassManagerRef other) => Handle == other.Handle;

        public bool FinalizeFunctionPassManager() => LLVM.FinalizeFunctionPassManager(this) != 0;

        public override int GetHashCode() => Handle.GetHashCode();

        public bool InitializeFunctionPassManager() => LLVM.InitializeFunctionPassManager(this) != 0;

        public bool Run(LLVMModuleRef M) => LLVM.RunPassManager(this, M) != 0;

        public bool RunFunctionPassManager(LLVMValueRef F) => LLVM.RunFunctionPassManager(this, F) != 0;

    }
}
