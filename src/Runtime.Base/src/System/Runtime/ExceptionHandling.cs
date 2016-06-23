// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System.Diagnostics;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;

using Internal.Runtime;

namespace System.Runtime
{
    public enum RhFailFastReason
    {
        Unknown = 0,
        InternalError = 1,                                   // "Runtime internal error"
        UnhandledException_ExceptionDispatchNotAllowed = 2,  // "Unhandled exception: no handler found before escaping a finally clause or other fail-fast scope."
        UnhandledException_CallerDidNotHandle = 3,           // "Unhandled exception: no handler found in calling method."
        ClassLibDidNotTranslateExceptionID = 4,              // "Unable to translate failure into a classlib-specific exception object."
        IllegalNativeCallableEntry = 5,                      // "Invalid Program: attempted to call a NativeCallable method from runtime-typesafe code."

        PN_UnhandledException = 6,                           // ProjectN: "unhandled exception"
        PN_UnhandledExceptionFromPInvoke = 7,                // ProjectN: "Unhandled exception: an unmanaged exception was thrown out of a managed-to-native transition."
        Max
    }

    // Keep this synchronized with the duplicate definition in DebugEventSource.cpp
    [Flags]
    internal enum ExceptionEventKind
    {
        Thrown = 1,
        CatchHandlerFound = 2,
        Unhandled = 4,
        FirstPassFrameEntered = 8
    }

    internal unsafe static class DebuggerNotify
    {
        // We cache the events a debugger is interested on the C# side to avoid p/invokes when the
        // debugger isn't attached.
        //
        // Ideally we would like the managed debugger to start toggling this directly so that
        // it stays perfectly up-to-date. However as a reasonable approximation we fetch
        // the value from native code at the beginning of each exception dispatch. If the debugger
        // attempts to enroll in more events mid-exception handling we aren't going to see it.
        private static ExceptionEventKind s_cachedEventMask;

        internal static void BeginFirstPass(Exception e, byte* faultingIP, UIntPtr faultingFrameSP)
        {
            s_cachedEventMask = InternalCalls.RhpGetRequestedExceptionEvents();

            if ((s_cachedEventMask & ExceptionEventKind.Thrown) == 0)
                return;

            InternalCalls.RhpSendExceptionEventToDebugger(ExceptionEventKind.Thrown, faultingIP, faultingFrameSP);
        }

        internal static void FirstPassFrameEntered(Exception e, byte* enteredFrameIP, UIntPtr enteredFrameSP)
        {
            s_cachedEventMask = InternalCalls.RhpGetRequestedExceptionEvents();

            if ((s_cachedEventMask & ExceptionEventKind.FirstPassFrameEntered) == 0)
                return;

            InternalCalls.RhpSendExceptionEventToDebugger(ExceptionEventKind.FirstPassFrameEntered, enteredFrameIP, enteredFrameSP);
        }

        internal static void EndFirstPass(Exception e, byte* handlerIP, UIntPtr handlingFrameSP)
        {
            if (handlerIP == null)
            {
                if ((s_cachedEventMask & ExceptionEventKind.Unhandled) == 0)
                    return;
                InternalCalls.RhpSendExceptionEventToDebugger(ExceptionEventKind.Unhandled, null, UIntPtr.Zero);
            }
            else
            {
                if ((s_cachedEventMask & ExceptionEventKind.CatchHandlerFound) == 0)
                    return;
                InternalCalls.RhpSendExceptionEventToDebugger(ExceptionEventKind.CatchHandlerFound, handlerIP, handlingFrameSP);
            }
        }

        internal static void BeginSecondPass()
        {
            //desktop debugging has an unwind begin event, however it appears that is unneeded for now, and possibly
            // will never be needed?
        }
    }

    internal unsafe static class EH
    {
        internal static UIntPtr MaxSP
        {
            get
            {
                return new UIntPtr(unchecked((void*)(ulong)-1L));
            }
        }

        private enum RhEHClauseKind
        {
            RH_EH_CLAUSE_TYPED = 0,
            RH_EH_CLAUSE_FAULT = 1,
            RH_EH_CLAUSE_FILTER = 2,
            RH_EH_CLAUSE_UNUSED = 3,
        }

        private struct RhEHClause
        {
            internal RhEHClauseKind _clauseKind;
            internal uint _tryStartOffset;
            internal uint _tryEndOffset;
            internal byte* _filterAddress;
            internal byte* _handlerAddress;
            internal void* _pTargetType;

            ///<summary>
            /// We expect the stackwalker to adjust return addresses to point at 'return address - 1' so that we 
            /// can use an interval here that is closed at the start and open at the end.  When a hardware fault
            /// occurs, the IP is pointing at the start of the instruction and will not be adjusted by the 
            /// stackwalker.  Therefore, it will naturally work with an interval that has a closed start and open
            /// end.
            ///</summary>
            public bool ContainsCodeOffset(uint codeOffset)
            {
                return ((codeOffset >= _tryStartOffset) &&
                        (codeOffset < _tryEndOffset));
            }
        }

        [StructLayout(LayoutKind.Explicit, Size = AsmOffsets.SIZEOF__EHEnum)]
        private struct EHEnum
        {
            [FieldOffset(0)]
            private IntPtr _dummy; // For alignment
        }

        // This is a fail-fast function used by the runtime as a last resort that will terminate the process with
        // as little effort as possible. No guarantee is made about the semantics of this fail-fast.
        internal static void FallbackFailFast(RhFailFastReason reason, Exception unhandledException)
        {
            InternalCalls.RhpFallbackFailFast();
        }

        // Constants used with RhpGetClasslibFunction, to indicate which classlib function
        // we are interested in. 
        // Note: make sure you change the def in EHHelpers.cpp if you change this!
        internal enum ClassLibFunctionId
        {
            GetRuntimeException = 0,
            FailFast = 1,
            // UnhandledExceptionHandler = 2, // unused
            AppendExceptionStackFrame = 3,
        }

        // Given an address pointing somewhere into a managed module, get the classlib-defined fail-fast 
        // function and invoke it.  Any failure to find and invoke the function, or if it returns, results in 
        // Rtm-define fail-fast behavior.
        internal unsafe static void FailFastViaClasslib(RhFailFastReason reason, Exception unhandledException,
                                                        IntPtr classlibAddress)
        {
            // Find the classlib function that will fail fast. This is a RuntimeExport function from the 
            // classlib module, and is therefore managed-callable.
            IntPtr pFailFastFunction = (IntPtr)InternalCalls.RhpGetClasslibFunction(classlibAddress,
                                                                           ClassLibFunctionId.FailFast);

            if (pFailFastFunction == IntPtr.Zero)
            {
                // The classlib didn't provide a function, so we fail our way...
                FallbackFailFast(reason, unhandledException);
            }

            try
            {
                // Invoke the classlib fail fast function.
                CalliIntrinsics.CallVoid(pFailFastFunction, reason, unhandledException, IntPtr.Zero, IntPtr.Zero);
            }
            catch
            {
                // disallow all exceptions leaking out of callbacks
            }

            // The classlib's function should never return and should not throw. If it does, then we fail our way...
            FallbackFailFast(reason, unhandledException);
        }

#if AMD64
        [StructLayout(LayoutKind.Explicit, Size = 0x4d0)]
#elif ARM
        [StructLayout(LayoutKind.Explicit, Size=0x1a0)]
#elif X86
        [StructLayout(LayoutKind.Explicit, Size=0x2cc)]
#else
        [StructLayout(LayoutKind.Explicit, Size = 0x10)] // this is small enough that it should trip an assert in RhpCopyContextFromExInfo
#endif
        private struct OSCONTEXT
        {
        }

#if ARM
        private const int c_IPAdjustForHardwareFault = 2;
#else
        private const int c_IPAdjustForHardwareFault = 1;
#endif

        internal unsafe static void* PointerAlign(void* ptr, int alignmentInBytes)
        {
            int alignMask = alignmentInBytes - 1;
#if BIT64
            return (void*)((((long)ptr) + alignMask) & ~alignMask);
#else
            return (void*)((((int)ptr) + alignMask) & ~alignMask);
#endif
        }


        [MethodImpl(MethodImplOptions.NoInlining)]
        internal unsafe static void UnhandledExceptionFailFastViaClasslib(
                                    RhFailFastReason reason, Exception unhandledException, IntPtr classlibAddress, ref ExInfo exInfo)
        {
            IntPtr pFailFastFunction = (IntPtr)InternalCalls.RhpGetClasslibFunction(classlibAddress,
                                                              ClassLibFunctionId.FailFast);

            if (pFailFastFunction == IntPtr.Zero)
            {
                FailFastViaClasslib(
                    reason,
                    unhandledException,
                    classlibAddress);
            }

            // 16-byte align the context.  This is overkill on x86 and ARM, but simplifies things slightly.
            const int contextAlignment = 16;
            byte* pbBuffer = stackalloc byte[sizeof(OSCONTEXT) + contextAlignment];
            void* pContext = PointerAlign(pbBuffer, contextAlignment);

            // We 'normalized' the faulting IP of hardware faults to behave like return addresses.  Undo this
            // normalization here so that we report the correct thing in the exception context record.
            if ((exInfo._kind & ExKind.KindMask) == ExKind.HardwareFault)
            {
                exInfo._pExContext->IP = (IntPtr)(((byte*)exInfo._pExContext->IP) - c_IPAdjustForHardwareFault);
            }

            InternalCalls.RhpCopyContextFromExInfo(pContext, sizeof(OSCONTEXT), exInfo._pExContext);

            try
            {
                CalliIntrinsics.CallVoid(pFailFastFunction, reason, unhandledException, exInfo._pExContext->IP, (IntPtr)pContext);
            }
            catch
            {
                // disallow all exceptions leaking out of callbacks
            }

            // The classlib's funciton should never return and should not throw. If it does, then we fail our way...
            FallbackFailFast(reason, unhandledException);
        }

        private enum RhEHFrameType
        {
            RH_EH_FIRST_FRAME = 1,
            RH_EH_FIRST_RETHROW_FRAME = 2,
        }

        internal unsafe static void AppendExceptionStackFrameViaClasslib(
            Exception exception, IntPtr IP, bool isFirstRethrowFrame, bool isFirstFrame)
        {
            IntPtr pAppendStackFrame = (IntPtr)InternalCalls.RhpGetClasslibFunction(IP,
                                                               ClassLibFunctionId.AppendExceptionStackFrame);
            int flags = (isFirstFrame ? (int)RhEHFrameType.RH_EH_FIRST_FRAME : 0) |
                        (isFirstRethrowFrame ? (int)RhEHFrameType.RH_EH_FIRST_RETHROW_FRAME : 0);

            if (pAppendStackFrame != IntPtr.Zero)
            {
                try
                {
                    CalliIntrinsics.CallVoid(pAppendStackFrame, exception, IP, flags);
                }
                catch
                {
                    // disallow all exceptions leaking out of callbacks
                }
            }
        }

        // Given an ExceptionID and an address pointing somewhere into a managed module, get
        // an exception object of a type that the module containing the given address will understand.
        // This finds the classlib-defined GetRuntimeException function and asks it for the exception object.
        internal static Exception GetClasslibException(ExceptionIDs id, IntPtr address)
        {
            unsafe
            {
                // Find the classlib function that will give us the exception object we want to throw. This
                // is a RuntimeExport function from the classlib module, and is therefore managed-callable.
                void* pGetRuntimeExceptionFunction =
                    InternalCalls.RhpGetClasslibFunction(address, ClassLibFunctionId.GetRuntimeException);

                // Return the exception object we get from the classlib.
                Exception e = null;
                try
                {
                    e = CalliIntrinsics.Call<Exception>((IntPtr)pGetRuntimeExceptionFunction, id);
                }
                catch
                {
                    // disallow all exceptions leaking out of callbacks
                }

                // If the helper fails to yield an object, then we fail-fast.
                if (e == null)
                {
                    FailFastViaClasslib(
                        RhFailFastReason.ClassLibDidNotTranslateExceptionID,
                        null,
                        address);
                }

                return e;
            }
        }

        // RhExceptionHandling_ functions are used to throw exceptions out of our asm helpers. We tail-call from 
        // the asm helpers to these functions, which performs the throw. The tail-call is important: it ensures that 
        // the stack is crawlable from within these functions.
        [RuntimeExport("RhExceptionHandling_ThrowClasslibOverflowException")]
        public static void ThrowClasslibOverflowException(IntPtr address)
        {
            // Throw the overflow exception defined by the classlib, using the return address of the asm helper
            // to find the correct classlib.

            throw GetClasslibException(ExceptionIDs.Overflow, address);
        }

        [RuntimeExport("RhExceptionHandling_ThrowClasslibDivideByZeroException")]
        public static void ThrowClasslibDivideByZeroException(IntPtr address)
        {
            // Throw the divide by zero exception defined by the classlib, using the return address of the asm helper
            // to find the correct classlib.

            throw GetClasslibException(ExceptionIDs.DivideByZero, address);
        }

        [RuntimeExport("RhExceptionHandling_FailedAllocation")]
        public static void FailedAllocation(EETypePtr pEEType, bool fIsOverflow)
        {
            ExceptionIDs exID = fIsOverflow ? ExceptionIDs.Overflow : ExceptionIDs.OutOfMemory;

            // Throw the out of memory exception defined by the classlib, using the input EEType* 
            // to find the correct classlib.

            throw pEEType.ToPointer()->GetClasslibException(exID);
        }

#if !INPLACE_RUNTIME
        private static OutOfMemoryException s_theOOMException = new OutOfMemoryException();

        // Rtm exports GetRuntimeException for the few cases where we have a helper that throws an exception
        // and may be called by either slr100 or other classlibs and that helper needs to throw an exception. 
        // There are only a few cases where this happens now (the fast allocation helpers), so we limit the 
        // exception types that Rtm will return.
        [RuntimeExport("GetRuntimeException")]
        public static Exception GetRuntimeException(ExceptionIDs id)
        {
            switch (id)
            {
                case ExceptionIDs.OutOfMemory:
                    // Throw a preallocated exception to avoid infinite recursion.
                    return s_theOOMException;

                case ExceptionIDs.Overflow:
                    return new OverflowException();

                default:
                    Debug.Assert(false, "unexpected ExceptionID");
                    FallbackFailFast(RhFailFastReason.InternalError, null);
                    return null;
            }
        }
#endif

        private enum HwExceptionCode : uint
        {
            STATUS_REDHAWK_NULL_REFERENCE = 0x00000000u,

            STATUS_DATATYPE_MISALIGNMENT = 0x80000002u,
            STATUS_ACCESS_VIOLATION = 0xC0000005u,
            STATUS_INTEGER_DIVIDE_BY_ZERO = 0xC0000094u,
        }

        [StructLayout(LayoutKind.Explicit, Size = AsmOffsets.SIZEOF__PAL_LIMITED_CONTEXT)]
        public struct PAL_LIMITED_CONTEXT
        {
            [FieldOffset(AsmOffsets.OFFSETOF__PAL_LIMITED_CONTEXT__IP)]
            internal IntPtr IP;
            // the rest of the struct is left unspecified.
        }

        internal struct StackRange
        {
        }

        // N.B. -- These values are burned into the throw helper assembly code and are also known the the 
        //         StackFrameIterator code.
        [Flags]
        internal enum ExKind : byte
        {
            None = 0,
            Throw = 1,
            HardwareFault = 2,
            // unused: 3
            KindMask = 3,

            RethrowFlag = 4,
            Rethrow = 5,        // RethrowFlag | Throw
            RethrowFault = 6,   // RethrowFlag | HardwareFault
        }

        [StackOnly]
        [StructLayout(LayoutKind.Explicit)]
        public struct ExInfo
        {
            internal void Init(Exception exceptionObj)
            {
                // _pPrevExInfo    -- set by asm helper
                // _pExContext     -- set by asm helper
                // _passNumber     -- set by asm helper
                // _kind           -- set by asm helper
                // _idxCurClause   -- set by asm helper
                // _frameIter      -- initialized explicitly during dispatch

                _exception = exceptionObj;
                _notifyDebuggerSP = UIntPtr.Zero;
            }

            internal void Init(Exception exceptionObj, ref ExInfo rethrownExInfo)
            {
                // _pPrevExInfo    -- set by asm helper
                // _pExContext     -- set by asm helper
                // _passNumber     -- set by asm helper
                // _idxCurClause   -- set by asm helper
                // _frameIter      -- initialized explicitly during dispatch

                _exception = exceptionObj;
                _kind = rethrownExInfo._kind | ExKind.RethrowFlag;
                _notifyDebuggerSP = UIntPtr.Zero;
            }

            internal Exception ThrownException
            {
                get
                {
                    return _exception;
                }
            }

            [FieldOffset(AsmOffsets.OFFSETOF__ExInfo__m_pPrevExInfo)]
            internal void* _pPrevExInfo;

            [FieldOffset(AsmOffsets.OFFSETOF__ExInfo__m_pExContext)]
            internal PAL_LIMITED_CONTEXT* _pExContext;

            [FieldOffset(AsmOffsets.OFFSETOF__ExInfo__m_exception)]
            private Exception _exception;    // actual object reference, specially reported by GcScanRootsWorker

            [FieldOffset(AsmOffsets.OFFSETOF__ExInfo__m_kind)]
            internal ExKind _kind;

            [FieldOffset(AsmOffsets.OFFSETOF__ExInfo__m_passNumber)]
            internal byte _passNumber;

            // BEWARE: This field is used by the stackwalker to know if the dispatch code has reached the 
            //         point at which a handler is called.  In other words, it serves as an "is a handler 
            //         active" state where '_idxCurClause == MaxTryRegionIdx' means 'no'. 
            [FieldOffset(AsmOffsets.OFFSETOF__ExInfo__m_idxCurClause)]
            internal uint _idxCurClause;

            [FieldOffset(AsmOffsets.OFFSETOF__ExInfo__m_frameIter)]
            internal StackFrameIterator _frameIter;

            [FieldOffset(AsmOffsets.OFFSETOF__ExInfo__m_notifyDebuggerSP)]
            volatile internal UIntPtr _notifyDebuggerSP;
        }

        //
        // Called by RhpThrowHwEx
        //
        [RuntimeExport("RhThrowHwEx")]
        public static void RhThrowHwEx(uint exceptionCode, ref ExInfo exInfo)
        {
            // trigger a GC (only if gcstress) to ensure we can stackwalk at this point
            GCStress.TriggerGC();

            InternalCalls.RhpValidateExInfoStack();

            IntPtr faultingCodeAddress = exInfo._pExContext->IP;

            ExceptionIDs exceptionId;
            switch (exceptionCode)
            {
                case (uint)HwExceptionCode.STATUS_REDHAWK_NULL_REFERENCE:
                    exceptionId = ExceptionIDs.NullReference;
                    break;

                case (uint)HwExceptionCode.STATUS_DATATYPE_MISALIGNMENT:
                    exceptionId = ExceptionIDs.DataMisaligned;
                    break;

                // N.B. -- AVs that have a read/write address lower than 64k are already transformed to 
                //         HwExceptionCode.REDHAWK_NULL_REFERENCE prior to calling this routine.
                case (uint)HwExceptionCode.STATUS_ACCESS_VIOLATION:
                    exceptionId = ExceptionIDs.AccessViolation;
                    break;

                case (uint)HwExceptionCode.STATUS_INTEGER_DIVIDE_BY_ZERO:
                    exceptionId = ExceptionIDs.DivideByZero;
                    break;

                default:
                    // We don't wrap SEH exceptions from foreign code like CLR does, so we believe that we
                    // know the complete set of HW faults generated by managed code and do not need to handle
                    // this case.
                    FailFastViaClasslib(RhFailFastReason.InternalError, null, faultingCodeAddress);
                    exceptionId = ExceptionIDs.NullReference;
                    break;
            }

            Exception exceptionToThrow = GetClasslibException(exceptionId, faultingCodeAddress);

            exInfo.Init(exceptionToThrow);
            DispatchEx(ref exInfo._frameIter, ref exInfo, MaxTryRegionIdx);
            FallbackFailFast(RhFailFastReason.InternalError, null);
        }

        private const uint MaxTryRegionIdx = 0xFFFFFFFFu;

        [RuntimeExport("RhThrowEx")]
        public static void RhThrowEx(Exception exceptionObj, ref ExInfo exInfo)
        {
            // trigger a GC (only if gcstress) to ensure we can stackwalk at this point
            GCStress.TriggerGC();

            InternalCalls.RhpValidateExInfoStack();

            // Transform attempted throws of null to a throw of NullReferenceException.
            if (exceptionObj == null)
            {
                IntPtr faultingCodeAddress = exInfo._pExContext->IP;
                exceptionObj = GetClasslibException(ExceptionIDs.NullReference, faultingCodeAddress);
            }

            exInfo.Init(exceptionObj);
            DispatchEx(ref exInfo._frameIter, ref exInfo, MaxTryRegionIdx);
            FallbackFailFast(RhFailFastReason.InternalError, null);
        }

        [RuntimeExport("RhRethrow")]
        public static void RhRethrow(ref ExInfo activeExInfo, ref ExInfo exInfo)
        {
            // trigger a GC (only if gcstress) to ensure we can stackwalk at this point
            GCStress.TriggerGC();

            InternalCalls.RhpValidateExInfoStack();

            // We need to copy the Exception object to this stack location because collided unwinds will cause
            // the original stack location to go dead.
            Exception rethrownException = activeExInfo.ThrownException;

            exInfo.Init(rethrownException, ref activeExInfo);
            DispatchEx(ref exInfo._frameIter, ref exInfo, activeExInfo._idxCurClause);
            FallbackFailFast(RhFailFastReason.InternalError, null);
        }

        private static void DispatchEx(ref StackFrameIterator frameIter, ref ExInfo exInfo, uint startIdx)
        {
            Debug.Assert(exInfo._passNumber == 1, "expected asm throw routine to set the pass");
            Exception exceptionObj = exInfo.ThrownException;

            // ------------------------------------------------
            //
            // First pass
            //
            // ------------------------------------------------
            UIntPtr handlingFrameSP = MaxSP;
            byte* pCatchHandler = null;
            uint catchingTryRegionIdx = MaxTryRegionIdx;

            bool isFirstRethrowFrame = (startIdx != MaxTryRegionIdx);
            bool isFirstFrame = true;

            byte* prevControlPC = null;
            UIntPtr prevFramePtr = UIntPtr.Zero;
            bool unwoundReversePInvoke = false;

            bool isValid = frameIter.Init(exInfo._pExContext);
            Debug.Assert(isValid, "RhThrowEx called with an unexpected context");
            DebuggerNotify.BeginFirstPass(exceptionObj, frameIter.ControlPC, frameIter.SP);
            for (; isValid; isValid = frameIter.Next(out startIdx, out unwoundReversePInvoke))
            {
                // For GC stackwalking, we'll happily walk across native code blocks, but for EH dispatch, we
                // disallow dispatching exceptions across native code.
                if (unwoundReversePInvoke)
                    break;

                prevControlPC = frameIter.ControlPC;

                DebugScanCallFrame(exInfo._passNumber, frameIter.ControlPC, frameIter.SP);

                // A debugger can subscribe to get callbacks at a specific frame of exception dispatch
                // exInfo._notifyDebuggerSP can be populated by the debugger from out of process
                // at any time.
                if (exInfo._notifyDebuggerSP == frameIter.SP)
                    DebuggerNotify.FirstPassFrameEntered(exceptionObj, frameIter.ControlPC, frameIter.SP);

                UpdateStackTrace(exceptionObj, ref exInfo, ref isFirstRethrowFrame, ref prevFramePtr, ref isFirstFrame);

                byte* pHandler;
                if (FindFirstPassHandler(exceptionObj, startIdx, ref frameIter,
                                         out catchingTryRegionIdx, out pHandler))
                {
                    handlingFrameSP = frameIter.SP;
                    pCatchHandler = pHandler;

                    DebugVerifyHandlingFrame(handlingFrameSP);
                    break;
                }
            }
            DebuggerNotify.EndFirstPass(exceptionObj, pCatchHandler, handlingFrameSP);

            if (pCatchHandler == null)
            {
                UnhandledExceptionFailFastViaClasslib(
                    RhFailFastReason.PN_UnhandledException,
                    exceptionObj,
                    (IntPtr)prevControlPC, // IP of the last frame that did not handle the exception
                    ref exInfo);
            }

            // We FailFast above if the exception goes unhandled.  Therefore, we cannot run the second pass
            // without a catch handler.
            Debug.Assert(pCatchHandler != null, "We should have a handler if we're starting the second pass");

            DebuggerNotify.BeginSecondPass();
            // ------------------------------------------------
            //
            // Second pass
            //
            // ------------------------------------------------

            // Due to the stackwalker logic, we cannot tolerate triggering a GC from the dispatch code once we
            // are in the 2nd pass.  This is because the stackwalker applies a particular unwind semantic to
            // 'collapse' funclets which gets confused when we walk out of the dispatch code and encounter the
            // 'main body' without first encountering the funclet.  The thunks used to invoke 2nd-pass 
            // funclets will always toggle this mode off before invoking them.
            InternalCalls.RhpSetThreadDoNotTriggerGC();

            exInfo._passNumber = 2;
            startIdx = MaxTryRegionIdx;
            isValid = frameIter.Init(exInfo._pExContext);
            for (; isValid && ((byte*)frameIter.SP <= (byte*)handlingFrameSP); isValid = frameIter.Next(out startIdx))
            {
                Debug.Assert(isValid, "second-pass EH unwind failed unexpectedly");
                DebugScanCallFrame(exInfo._passNumber, frameIter.ControlPC, frameIter.SP);

                if (frameIter.SP == handlingFrameSP)
                {
                    // invoke only a partial second-pass here...
                    InvokeSecondPass(ref exInfo, startIdx, catchingTryRegionIdx);
                    break;
                }

                InvokeSecondPass(ref exInfo, startIdx);
            }

            // ------------------------------------------------
            //
            // Call the handler and resume execution
            //
            // ------------------------------------------------
            exInfo._idxCurClause = catchingTryRegionIdx;
            InternalCalls.RhpCallCatchFunclet(
                exceptionObj, pCatchHandler, frameIter.RegisterSet, ref exInfo);
            // currently, RhpCallCatchFunclet will resume after the catch
            Debug.Assert(false, "unreachable");
            FallbackFailFast(RhFailFastReason.InternalError, null);
        }

        [System.Diagnostics.Conditional("DEBUG")]
        private static void DebugScanCallFrame(int passNumber, byte* ip, UIntPtr sp)
        {
            if (ip == null) { Debug.Assert(false, "false"); }
        }

        [System.Diagnostics.Conditional("DEBUG")]
        private static void DebugVerifyHandlingFrame(UIntPtr handlingFrameSP)
        {
            Debug.Assert(handlingFrameSP != MaxSP, "Handling frame must have an SP value");
            Debug.Assert(((UIntPtr*)handlingFrameSP) > &handlingFrameSP,
                "Handling frame must have a valid stack frame pointer");
        }

        private static void UpdateStackTrace(Exception exceptionObj, ref ExInfo exInfo, ref bool isFirstRethrowFrame, ref UIntPtr prevFramePtr, ref bool isFirstFrame)
        {
            // We use the fact that all funclet stack frames belonging to the same logical method activation 
            // will have the same FramePointer value.  Additionally, the stackwalker will return a sequence of
            // callbacks for all the funclet stack frames, one right after the other.  The classlib doesn't 
            // want to know about funclets, so we strip them out by only reporting the first frame of a 
            // sequence of funclets.  This is correct because the leafmost funclet is first in the sequence
            // and corresponds to the current 'IP state' of the method.
            UIntPtr curFramePtr = exInfo._frameIter.FramePointer;
            if ((prevFramePtr == UIntPtr.Zero) || (curFramePtr != prevFramePtr))
            {
                AppendExceptionStackFrameViaClasslib(exceptionObj, (IntPtr)exInfo._frameIter.ControlPC,
                                                     isFirstRethrowFrame, isFirstFrame);
            }
            prevFramePtr = curFramePtr;
            isFirstRethrowFrame = false;
            isFirstFrame = false;
        }

        private static bool FindFirstPassHandler(Exception exception, uint idxStart,
                                                 ref StackFrameIterator frameIter,
                                                 out uint tryRegionIdx, out byte* pHandler)
        {
            pHandler = null;
            tryRegionIdx = MaxTryRegionIdx;

            EHEnum ehEnum;
            byte* pbMethodStartAddress;
            if (!InternalCalls.RhpEHEnumInitFromStackFrameIterator(ref frameIter, &pbMethodStartAddress, &ehEnum))
                return false;

            byte* pbControlPC = frameIter.ControlPC;

            uint codeOffset = (uint)(pbControlPC - pbMethodStartAddress);

            uint lastTryStart = 0, lastTryEnd = 0;

            // Search the clauses for one that contains the current offset.
            RhEHClause ehClause;
            for (uint curIdx = 0; InternalCalls.RhpEHEnumNext(&ehEnum, &ehClause); curIdx++)
            {
                // 
                // Skip to the starting try region.  This is used by collided unwinds and rethrows to pickup where
                // the previous dispatch left off.
                //
                if (idxStart != MaxTryRegionIdx)
                {
                    if (curIdx <= idxStart)
                    {
                        lastTryStart = ehClause._tryStartOffset; lastTryEnd = ehClause._tryEndOffset;
                        continue;
                    }

                    // Now, we continue skipping while the try region is identical to the one that invoked the 
                    // previous dispatch.
                    if ((ehClause._tryStartOffset == lastTryStart) && (ehClause._tryEndOffset == lastTryEnd))
                        continue;
                }

                RhEHClauseKind clauseKind = ehClause._clauseKind;

                if (((clauseKind != RhEHClauseKind.RH_EH_CLAUSE_TYPED) &&
                     (clauseKind != RhEHClauseKind.RH_EH_CLAUSE_FILTER))
                    || !ehClause.ContainsCodeOffset(codeOffset))
                {
                    continue;
                }

                // Found a containing clause. Because of the order of the clauses, we know this is the
                // most containing.
                if (clauseKind == RhEHClauseKind.RH_EH_CLAUSE_TYPED)
                {
                    if (ShouldTypedClauseCatchThisException(exception, (EEType*)ehClause._pTargetType))
                    {
                        pHandler = ehClause._handlerAddress;
                        tryRegionIdx = curIdx;
                        return true;
                    }
                }
                else
                {
                    byte* pFilterFunclet = ehClause._filterAddress;
                    bool shouldInvokeHandler =
                        InternalCalls.RhpCallFilterFunclet(exception, pFilterFunclet, frameIter.RegisterSet);

                    if (shouldInvokeHandler)
                    {
                        pHandler = ehClause._handlerAddress;
                        tryRegionIdx = curIdx;
                        return true;
                    }
                }
            }

            return false;
        }

        static EEType* s_pLowLevelObjectType;

        private static bool ShouldTypedClauseCatchThisException(Exception exception, EEType* pClauseType)
        {
            if (TypeCast.IsInstanceOfClass(exception, pClauseType) != null)
                return true;

            if (s_pLowLevelObjectType == null)
            {
                s_pLowLevelObjectType = new System.Object().EEType;
            }

            // This allows the typical try { } catch { }--which expands to a typed catch of System.Object--to work on 
            // all objects when the clause is in the low level runtime code.  This special case is needed because 
            // objects from foreign type systems are sometimes throw back up at runtime code and this is the only way
            // to catch them outside of having a filter with no type check in it, which isn't currently possible to 
            // write in C#.  See https://github.com/dotnet/roslyn/issues/4388
            if (pClauseType->IsEquivalentTo(s_pLowLevelObjectType))
                return true;

            return false;
        }

        private static void InvokeSecondPass(ref ExInfo exInfo, uint idxStart)
        {
            InvokeSecondPass(ref exInfo, idxStart, MaxTryRegionIdx);
        }
        private static void InvokeSecondPass(ref ExInfo exInfo, uint idxStart, uint idxLimit)
        {
            EHEnum ehEnum;
            byte* pbMethodStartAddress;
            if (!InternalCalls.RhpEHEnumInitFromStackFrameIterator(ref exInfo._frameIter, &pbMethodStartAddress, &ehEnum))
                return;

            byte* pbControlPC = exInfo._frameIter.ControlPC;

            uint codeOffset = (uint)(pbControlPC - pbMethodStartAddress);

            uint lastTryStart = 0, lastTryEnd = 0;

            // Search the clauses for one that contains the current offset.
            RhEHClause ehClause;
            for (uint curIdx = 0; InternalCalls.RhpEHEnumNext(&ehEnum, &ehClause) && curIdx < idxLimit; curIdx++)
            {
                // 
                // Skip to the starting try region.  This is used by collided unwinds and rethrows to pickup where
                // the previous dispatch left off.
                //
                if (idxStart != MaxTryRegionIdx)
                {
                    if (curIdx <= idxStart)
                    {
                        lastTryStart = ehClause._tryStartOffset; lastTryEnd = ehClause._tryEndOffset;
                        continue;
                    }

                    // Now, we continue skipping while the try region is identical to the one that invoked the 
                    // previous dispatch.
                    if ((ehClause._tryStartOffset == lastTryStart) && (ehClause._tryEndOffset == lastTryEnd))
                        continue;
                }

                RhEHClauseKind clauseKind = ehClause._clauseKind;

                if ((clauseKind != RhEHClauseKind.RH_EH_CLAUSE_FAULT)
                    || !ehClause.ContainsCodeOffset(codeOffset))
                {
                    continue;
                }

                // Found a containing clause. Because of the order of the clauses, we know this is the
                // most containing.

                // N.B. -- We need to suppress GC "in-between" calls to finallys in this loop because we do
                // not have the correct next-execution point live on the stack and, therefore, may cause a GC
                // hole if we allow a GC between invocation of finally funclets (i.e. after one has returned
                // here to the dispatcher, but before the next one is invoked).  Once they are running, it's 
                // fine for them to trigger a GC, obviously.
                // 
                // As a result, RhpCallFinallyFunclet will set this state in the runtime upon return from the
                // funclet, and we need to reset it if/when we fall out of the loop and we know that the 
                // method will no longer get any more GC callbacks.

                byte* pFinallyHandler = ehClause._handlerAddress;
                exInfo._idxCurClause = curIdx;
                InternalCalls.RhpCallFinallyFunclet(pFinallyHandler, exInfo._frameIter.RegisterSet);
                exInfo._idxCurClause = MaxTryRegionIdx;
            }
        }

        [NativeCallable(EntryPoint = "RhpFailFastForPInvokeExceptionPreemp", CallingConvention = CallingConvention.Cdecl)]
        static public void RhpFailFastForPInvokeExceptionPreemp(IntPtr PInvokeCallsiteReturnAddr, void* pExceptionRecord, void* pContextRecord)
        {
            FailFastViaClasslib(RhFailFastReason.PN_UnhandledExceptionFromPInvoke, null, PInvokeCallsiteReturnAddr);
        }
        [RuntimeExport("RhpFailFastForPInvokeExceptionCoop")]
        static public void RhpFailFastForPInvokeExceptionCoop(IntPtr classlibBreadcrumb, void* pExceptionRecord, void* pContextRecord)
        {
            FailFastViaClasslib(RhFailFastReason.PN_UnhandledExceptionFromPInvoke, null, classlibBreadcrumb);
        }
    } // static class EH
}
