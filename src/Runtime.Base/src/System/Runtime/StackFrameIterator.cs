// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

#define FEATURE_CLR_EH
using System.Runtime.InteropServices;

#if FEATURE_CLR_EH

namespace System.Runtime
{
    [StructLayout(LayoutKind.Explicit, Size = AsmOffsets.SIZEOF__REGDISPLAY)]
    internal unsafe struct REGDISPLAY
    {
        [FieldOffset(AsmOffsets.OFFSETOF__REGDISPLAY__SP)]
        internal UIntPtr SP;
    }

    [StructLayout(LayoutKind.Explicit, Size = AsmOffsets.SIZEOF__StackFrameIterator)]
    internal unsafe struct StackFrameIterator
    {
        [FieldOffset(AsmOffsets.OFFSETOF__StackFrameIterator__m_FramePointer)]
        private UIntPtr _framePointer;
        [FieldOffset(AsmOffsets.OFFSETOF__StackFrameIterator__m_ControlPC)]
        private IntPtr _controlPC;
        [FieldOffset(AsmOffsets.OFFSETOF__StackFrameIterator__m_RegDisplay)]
        private REGDISPLAY _regDisplay;

        internal byte* ControlPC { get { return (byte*)_controlPC; } }
        internal void* RegisterSet { get { fixed (void* pRegDisplay = &_regDisplay) { return pRegDisplay; } } }
        internal UIntPtr SP { get { return _regDisplay.SP; } }
        internal UIntPtr FramePointer { get { return _framePointer; } }

        internal bool Init(EH.PAL_LIMITED_CONTEXT* pStackwalkCtx)
        {
            return InternalCalls.RhpSfiInit(ref this, pStackwalkCtx);
        }

        internal bool Next()
        {
            uint uExCollideClauseIdx;
            bool fUnwoundReversePInvoke;
            return Next(out uExCollideClauseIdx, out fUnwoundReversePInvoke);
        }

        internal bool Next(out uint uExCollideClauseIdx)
        {
            bool fUnwoundReversePInvoke;
            return Next(out uExCollideClauseIdx, out fUnwoundReversePInvoke);
        }

        internal bool Next(out uint uExCollideClauseIdx, out bool fUnwoundReversePInvoke)
        {
            return InternalCalls.RhpSfiNext(ref this, out uExCollideClauseIdx, out fUnwoundReversePInvoke);
        }
    }
}

#endif // FEATURE_CLR_EH
