// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

#define DEBUG // The behavior of this contract library should be consistent regardless of build type.

using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;
using System.Diagnostics.Contracts;
using System.Reflection;
using DeveloperExperience = Internal.DeveloperExperience.DeveloperExperience;

#if FEATURE_RELIABILITY_CONTRACTS
using System.Runtime.ConstrainedExecution;
#endif
#if FEATURE_UNTRUSTED_CALLERS
using System.Security;
using System.Security.Permissions;
#endif


namespace System.Diagnostics.Contracts
{
    public static partial class Contract
    {
        #region Private Methods

        [ThreadStatic]
        private static bool t_assertingMustUseRewriter;

        /// <summary>
        /// This method is used internally to trigger a failure indicating to the "programmer" that he is using the interface incorrectly.
        /// It is NEVER used to indicate failure of actual contracts at runtime.
        /// </summary>
#if FEATURE_UNTRUSTED_CALLERS
#endif
        static partial void AssertMustUseRewriter(ContractFailureKind kind, string contractKind)
        {
            //TODO: Implement CodeContract failure mechanics including enabling CCIRewrite

            if (t_assertingMustUseRewriter)
            {
                System.Diagnostics.Debug.Fail("Asserting that we must use the rewriter went reentrant. Didn't rewrite this System.Private.CoreLib?");
                return;
            }
            t_assertingMustUseRewriter = true;

            //// For better diagnostics, report which assembly is at fault.  Walk up stack and
            //// find the first non-mscorlib assembly.
            //Assembly thisAssembly = typeof(Contract).Assembly;  // In case we refactor mscorlib, use Contract class instead of Object.
            //StackTrace stack = new StackTrace();
            //Assembly probablyNotRewritten = null;
            //for (int i = 0; i < stack.FrameCount; i++)
            //{
            //    Assembly caller = stack.GetFrame(i).GetMethod().DeclaringType.Assembly;
            //    if (caller != thisAssembly)
            //    {
            //        probablyNotRewritten = caller;
            //        break;
            //    }
            //}

            //if (probablyNotRewritten == null)
            //    probablyNotRewritten = thisAssembly;
            //String simpleName = probablyNotRewritten.GetName().Name;
            string simpleName = "System.Private.CoreLib";
            System.Runtime.CompilerServices.ContractHelper.TriggerFailure(kind, SR.Format(SR.MustUseCCRewrite, contractKind, simpleName), null, null, null);

            t_assertingMustUseRewriter = false;
        }

        #endregion Private Methods

        #region Failure Behavior

        /// <summary>
        /// Without contract rewriting, failing Assert/Assumes end up calling this method.
        /// Code going through the contract rewriter never calls this method. Instead, the rewriter produced failures call
        /// System.Runtime.CompilerServices.ContractHelper.RaiseContractFailedEvent, followed by 
        /// System.Runtime.CompilerServices.ContractHelper.TriggerFailure.
        /// </summary>
        [System.Diagnostics.DebuggerNonUserCode]
#if FEATURE_RELIABILITY_CONTRACTS
        [ReliabilityContract(Consistency.WillNotCorruptState, Cer.MayFail)]
#endif
        static partial void ReportFailure(ContractFailureKind failureKind, string userMessage, string conditionText, Exception innerException)
        {
            if (failureKind < ContractFailureKind.Precondition || failureKind > ContractFailureKind.Assume)
                throw new ArgumentException(SR.Format(SR.Arg_EnumIllegalVal, failureKind), nameof(failureKind));
            Contract.EndContractBlock();

            // displayMessage == null means: yes we handled it. Otherwise it is the localized failure message
            string displayMessage = System.Runtime.CompilerServices.ContractHelper.RaiseContractFailedEvent(failureKind, userMessage, conditionText, innerException);

            if (displayMessage == null) return;

            System.Runtime.CompilerServices.ContractHelper.TriggerFailure(failureKind, displayMessage, userMessage, conditionText, innerException);
        }

#if !FEATURE_CORECLR
        /// <summary>
        /// Allows a managed application environment such as an interactive interpreter (IronPython)
        /// to be notified of contract failures and 
        /// potentially "handle" them, either by throwing a particular exception type, etc.  If any of the
        /// event handlers sets the Cancel flag in the ContractFailedEventArgs, then the Contract class will
        /// not pop up an assert dialog box or trigger escalation policy.  Hooking this event requires 
        /// full trust, because it will inform you of bugs in the appdomain and because the event handler
        /// could allow you to continue execution.
        /// </summary>
        public static event EventHandler<ContractFailedEventArgs> ContractFailed
        {
#if FEATURE_UNTRUSTED_CALLERS
#if FEATURE_LINK_DEMAND
            [SecurityPermission(SecurityAction.LinkDemand, Unrestricted = true)]
#endif
#endif
            add
            {
                System.Runtime.CompilerServices.ContractHelper.InternalContractFailed += value;
            }
#if FEATURE_UNTRUSTED_CALLERS
#if FEATURE_LINK_DEMAND
            [SecurityPermission(SecurityAction.LinkDemand, Unrestricted = true)]
#endif
#endif
            remove
            {
                System.Runtime.CompilerServices.ContractHelper.InternalContractFailed -= value;
            }
        }
#endif // !FEATURE_CORECLR

        #endregion FailureBehavior
    }

#if !FEATURE_CORECLR  // Not usable on Silverlight by end users due to security, and full trust users have not yet expressed an interest.
    public sealed class ContractFailedEventArgs : EventArgs
    {
        private ContractFailureKind _failureKind;
        private string _message;
        private string _condition;
        private Exception _originalException;
        private bool _handled;
        private bool _unwind;

        internal Exception thrownDuringHandler;

#if FEATURE_RELIABILITY_CONTRACTS
        [ReliabilityContract(Consistency.WillNotCorruptState, Cer.MayFail)]
#endif
        public ContractFailedEventArgs(ContractFailureKind failureKind, string message, string condition, Exception originalException)
        {
            Contract.Requires(originalException == null || failureKind == ContractFailureKind.PostconditionOnException);
            _failureKind = failureKind;
            _message = message;
            _condition = condition;
            _originalException = originalException;
        }

        public string Message { get { return _message; } }
        public string Condition { get { return _condition; } }
        public ContractFailureKind FailureKind { get { return _failureKind; } }
        public Exception OriginalException { get { return _originalException; } }

        // Whether the event handler "handles" this contract failure, or to fail via escalation policy.
        public bool Handled
        {
            get { return _handled; }
        }

#if FEATURE_UNTRUSTED_CALLERS
#if FEATURE_LINK_DEMAND
        [SecurityPermission(SecurityAction.LinkDemand, Unrestricted = true)]
#endif
#endif
        public void SetHandled()
        {
            _handled = true;
        }

        public bool Unwind
        {
            get { return _unwind; }
        }

#if FEATURE_UNTRUSTED_CALLERS
#if FEATURE_LINK_DEMAND
        [SecurityPermission(SecurityAction.LinkDemand, Unrestricted = true)]
#endif
#endif
        public void SetUnwind()
        {
            _unwind = true;
        }
    }
#endif // !FEATURE_CORECLR

    [Serializable]
    [System.Runtime.CompilerServices.TypeForwardedFrom("mscorlib, Version=4.0.0.0, Culture=neutral, PublicKeyToken=b77a5c561934e089")]
    // Needs to be public for type forwarding serialization support.
    public sealed class ContractException : Exception
    {
        private readonly ContractFailureKind _kind;
        private readonly string _userMessage;
        private readonly string _condition;

        [System.Diagnostics.CodeAnalysis.SuppressMessage("Microsoft.Performance", "CA1811:AvoidUncalledPrivateCode")]
        public ContractFailureKind Kind { get { return _kind; } }
        [System.Diagnostics.CodeAnalysis.SuppressMessage("Microsoft.Performance", "CA1811:AvoidUncalledPrivateCode")]
        public string Failure { get { return this.Message; } }
        [System.Diagnostics.CodeAnalysis.SuppressMessage("Microsoft.Performance", "CA1811:AvoidUncalledPrivateCode")]
        public string UserMessage { get { return _userMessage; } }
        [System.Diagnostics.CodeAnalysis.SuppressMessage("Microsoft.Performance", "CA1811:AvoidUncalledPrivateCode")]
        public string Condition { get { return _condition; } }

        // Called by COM Interop, if we see COR_E_CODECONTRACTFAILED as an HRESULT.
        private ContractException()
        {
            HResult = System.Runtime.CompilerServices.ContractHelper.COR_E_CODECONTRACTFAILED;
        }

        public ContractException(ContractFailureKind kind, string failure, string userMessage, string condition, Exception innerException)
            : base(failure, innerException)
        {
            HResult = System.Runtime.CompilerServices.ContractHelper.COR_E_CODECONTRACTFAILED;
            _kind = kind;
            _userMessage = userMessage;
            _condition = condition;
        }

        private ContractException(System.Runtime.Serialization.SerializationInfo info, System.Runtime.Serialization.StreamingContext context)
            : base(info, context)
        {
            _kind = (ContractFailureKind)info.GetInt32("Kind");
            _userMessage = info.GetString("UserMessage");
            _condition = info.GetString("Condition");
        }

        public override void GetObjectData(System.Runtime.Serialization.SerializationInfo info, System.Runtime.Serialization.StreamingContext context)
        {
            base.GetObjectData(info, context);
            info.AddValue("Kind", _kind);
            info.AddValue("UserMessage", _userMessage);
            info.AddValue("Condition", _condition);
        }
    }
}


namespace System.Runtime.CompilerServices
{
    public static partial class ContractHelper
    {
        #region Private fields

#if !FEATURE_CORECLR
        private static volatile EventHandler<ContractFailedEventArgs> s_contractFailedEvent;
        private static readonly object s_lockObject = new object();
#endif // !FEATURE_CORECLR
        internal const int COR_E_CODECONTRACTFAILED = unchecked((int)0x80131542);

        #endregion

#if !FEATURE_CORECLR
        /// <summary>
        /// Allows a managed application environment such as an interactive interpreter (IronPython) or a
        /// web browser host (Jolt hosting Silverlight in IE) to be notified of contract failures and 
        /// potentially "handle" them, either by throwing a particular exception type, etc.  If any of the
        /// event handlers sets the Cancel flag in the ContractFailedEventArgs, then the Contract class will
        /// not pop up an assert dialog box or trigger escalation policy.  Hooking this event requires 
        /// full trust.
        /// </summary>
        internal static event EventHandler<ContractFailedEventArgs> InternalContractFailed
        {
#if FEATURE_UNTRUSTED_CALLERS
#endif
            add
            {
                // Eagerly prepare each event handler _marked with a reliability contract_, to 
                // attempt to reduce out of memory exceptions while reporting contract violations.
                // This only works if the new handler obeys the constraints placed on 
                // constrained execution regions.  Eagerly preparing non-reliable event handlers
                // would be a perf hit and wouldn't significantly improve reliability.
                // UE: Please mention reliable event handlers should also be marked with the 
                // PrePrepareMethodAttribute to avoid CER eager preparation work when ngen'ed.
                //#if !FEATURE_CORECLR
                //                System.Runtime.CompilerServices.RuntimeHelpers.PrepareContractedDelegate(value);
                //#endif
                lock (s_lockObject)
                {
                    s_contractFailedEvent += value;
                }
            }
#if FEATURE_UNTRUSTED_CALLERS
#endif
            remove
            {
                lock (s_lockObject)
                {
                    s_contractFailedEvent -= value;
                }
            }
        }
#endif // !FEATURE_CORECLR

        /// <summary>
        /// Rewriter will call this method on a contract failure to allow listeners to be notified.
        /// The method should not perform any failure (assert/throw) itself.
        /// This method has 3 functions:
        /// 1. Call any contract hooks (such as listeners to Contract failed events)
        /// 2. Determine if the listeneres deem the failure as handled (then resultFailureMessage should be set to null)
        /// 3. Produce a localized resultFailureMessage used in advertising the failure subsequently.
        /// </summary>
        /// <param name="resultFailureMessage">Should really be out (or the return value), but partial methods are not flexible enough.
        /// On exit: null if the event was handled and should not trigger a failure.
        ///          Otherwise, returns the localized failure message</param>
        [SuppressMessage("Microsoft.Design", "CA1030:UseEventsWhereAppropriate")]
        [SuppressMessage("Microsoft.Design", "CA1031:DoNotCatchGeneralExceptionTypes")]
        [System.Diagnostics.DebuggerNonUserCode]
#if FEATURE_RELIABILITY_CONTRACTS
#endif
        static partial void RaiseContractFailedEventImplementation(ContractFailureKind failureKind, string userMessage, string conditionText, Exception innerException, ref string resultFailureMessage)
        {
            if (failureKind < ContractFailureKind.Precondition || failureKind > ContractFailureKind.Assume)
                throw new ArgumentException(SR.Format(SR.Arg_EnumIllegalVal, failureKind), nameof(failureKind));
            Contract.EndContractBlock();

            string returnValue;
            string displayMessage = "contract failed.";  // Incomplete, but in case of OOM during resource lookup...
#if !FEATURE_CORECLR
            ContractFailedEventArgs eventArgs = null;  // In case of OOM.
#endif // !FEATURE_CORECLR
#if FEATURE_RELIABILITY_CONTRACTS
            System.Runtime.CompilerServices.RuntimeHelpers.PrepareConstrainedRegions();
#endif
            try
            {
                displayMessage = GetDisplayMessage(failureKind, userMessage, conditionText);
#if !FEATURE_CORECLR
                if (s_contractFailedEvent != null)
                {
                    eventArgs = new ContractFailedEventArgs(failureKind, displayMessage, conditionText, innerException);
                    foreach (EventHandler<ContractFailedEventArgs> handler in s_contractFailedEvent.GetInvocationList())
                    {
                        try
                        {
                            handler(null, eventArgs);
                        }
                        catch (Exception e)
                        {
                            eventArgs.thrownDuringHandler = e;
                            eventArgs.SetUnwind();
                        }
                    }
                    if (eventArgs.Unwind)
                    {
                        //if (Environment.IsCLRHosted)
                        //    TriggerCodeContractEscalationPolicy(failureKind, displayMessage, conditionText, innerException);

                        // unwind
                        if (innerException == null) { innerException = eventArgs.thrownDuringHandler; }
                        throw new ContractException(failureKind, displayMessage, userMessage, conditionText, innerException);
                    }
                }
#endif // !FEATURE_CORECLR
            }
            finally
            {
#if !FEATURE_CORECLR
                if (eventArgs != null && eventArgs.Handled)
                {
                    returnValue = null; // handled
                }
                else
#endif // !FEATURE_CORECLR
                {
                    returnValue = displayMessage;
                }
            }
            resultFailureMessage = returnValue;
        }

        /// <summary>
        /// Rewriter calls this method to get the default failure behavior.
        /// </summary>
        [SuppressMessage("Microsoft.Usage", "CA1801:ReviewUnusedParameters", MessageId = "conditionText")]
        [SuppressMessage("Microsoft.Usage", "CA1801:ReviewUnusedParameters", MessageId = "userMessage")]
        [SuppressMessage("Microsoft.Usage", "CA1801:ReviewUnusedParameters", MessageId = "kind")]
        [SuppressMessage("Microsoft.Usage", "CA1801:ReviewUnusedParameters", MessageId = "innerException")]
        [System.Diagnostics.DebuggerNonUserCode]
#if FEATURE_UNTRUSTED_CALLERS && !FEATURE_CORECLR
#endif
        static partial void TriggerFailureImplementation(ContractFailureKind kind, string displayMessage, string userMessage, string conditionText, Exception innerException)
        {
            // If we're here, our intent is to pop up a dialog box (if we can).  For developers 
            // interacting live with a debugger, this is a good experience.  For Silverlight 
            // hosted in Internet Explorer, the assert window is great.  If we cannot
            // pop up a dialog box, throw an exception (consider a library compiled with 
            // "Assert On Failure" but used in a process that can't pop up asserts, like an 
            // NT Service).  For the CLR hosted by server apps like SQL or Exchange, we should 
            // trigger escalation policy.  
            //#if !FEATURE_CORECLR
            //            if (Environment.IsCLRHosted)
            //            {
            //                TriggerCodeContractEscalationPolicy(kind, displayMessage, conditionText, innerException);
            //                // Hosts like SQL may choose to abort the thread, so we will not get here in all cases.
            //                // But if the host's chosen action was to throw an exception, we should throw an exception
            //                // here (which is easier to do in managed code with the right parameters).  
            //                throw new ContractException(kind, displayMessage, userMessage, conditionText, innerException);
            //            }
            //#endif // !FEATURE_CORECLR

            //TODO: Implement CodeContract failure mechanics including enabling CCIRewrite

            string stackTrace = null; //@todo: Any reasonable way to get a stack trace here?
            bool userSelectedIgnore = DeveloperExperience.Default.OnContractFailure(stackTrace, kind, displayMessage, userMessage, conditionText, innerException);
            if (userSelectedIgnore)
                return;

            //if (!Environment.UserInteractive) {
            throw new ContractException(kind, displayMessage, userMessage, conditionText, innerException);
            //}
            //// May need to rethink Assert.Fail w/ TaskDialogIndirect as a model.  Window title.  Main instruction.  Content.  Expanded info.
            //// Optional info like string for collapsed text vs. expanded text.
            //String windowTitle = SR.Format(GetResourceNameForFailure(kind));
            //const int numStackFramesToSkip = 2;  // To make stack traces easier to read

            //System.Diagnostics.Debug.Assert(conditionText, displayMessage, windowTitle, COR_E_CODECONTRACTFAILED, StackTrace.TraceFormat.Normal, numStackFramesToSkip);
            // If we got here, the user selected Ignore.  Continue.
        }

        private static string GetFailureMessage(ContractFailureKind failureKind, string conditionText)
        {
            bool hasConditionText = !string.IsNullOrEmpty(conditionText);
            switch (failureKind)
            {
                case ContractFailureKind.Assert:
                    return hasConditionText ? SR.Format(SR.AssertionFailed_Cnd, conditionText) : SR.AssertionFailed;

                case ContractFailureKind.Assume:
                    return hasConditionText ? SR.Format(SR.AssumptionFailed_Cnd, conditionText) : SR.AssumptionFailed;

                case ContractFailureKind.Precondition:
                    return hasConditionText ? SR.Format(SR.PreconditionFailed_Cnd, conditionText) : SR.PreconditionFailed;

                case ContractFailureKind.Postcondition:
                    return hasConditionText ? SR.Format(SR.PostconditionFailed_Cnd, conditionText) : SR.PostconditionFailed;

                case ContractFailureKind.Invariant:
                    return hasConditionText ? SR.Format(SR.InvariantFailed_Cnd, conditionText) : SR.InvariantFailed;

                case ContractFailureKind.PostconditionOnException:
                    return hasConditionText ? SR.Format(SR.PostconditionOnExceptionFailed_Cnd, conditionText) : SR.PostconditionOnExceptionFailed;

                default:
                    Contract.Assume(false, "Unreachable code");
                    return SR.AssumptionFailed;
            }
        }

#if FEATURE_RELIABILITY_CONTRACTS
        [ReliabilityContract(Consistency.WillNotCorruptState, Cer.MayFail)]
#endif
        private static string GetDisplayMessage(ContractFailureKind failureKind, string userMessage, string conditionText)
        {
            // Well-formatted English messages will take one of four forms.  A sentence ending in
            // either a period or a colon, the condition string, then the message tacked 
            // on to the end with two spaces in front.
            // Note that both the conditionText and userMessage may be null.  Also, 
            // on Silverlight we may not be able to look up a friendly string for the
            // error message.  Let's leverage Silverlight's default error message there.
            string failureMessage = GetFailureMessage(failureKind, conditionText);
            // Now add in the user message, if present.
            if (!string.IsNullOrEmpty(userMessage))
            {
                return failureMessage + "  " + userMessage;
            }
            else
            {
                return failureMessage;
            }
        }
        //#if !FEATURE_CORECLR
        //        // Will trigger escalation policy, if hosted and the host requested us to do something (such as 
        //        // abort the thread or exit the process).  Starting in Dev11, for hosted apps the default behavior 
        //        // is to throw an exception.  
        //        // Implementation notes:
        //        // We implement our default behavior of throwing an exception by simply returning from our native 
        //        // method inside the runtime and falling through to throw an exception.
        //        // We must call through this method before calling the method on the Environment class
        //        // because our security team does not yet support SecuritySafeCritical on P/Invoke methods.
        //        // Note this can be called in the context of throwing another exception (EnsuresOnThrow).
        //#if FEATURE_UNTRUSTED_CALLERS
        //#endif 
        //#if FEATURE_RELIABILITY_CONTRACTS
        //        [ReliabilityContract(Consistency.WillNotCorruptState, Cer.Success)]
        //#endif
        //        [DebuggerNonUserCode]
        //        private static void TriggerCodeContractEscalationPolicy(ContractFailureKind failureKind, String message, String conditionText, Exception innerException)
        //        {
        //            String exceptionAsString = null;
        //            if (innerException != null)
        //                exceptionAsString = innerException.ToString();
        //            Environment.TriggerCodeContractFailure(failureKind, message, conditionText, exceptionAsString);
        //        }
        //#endif // !FEATURE_CORECLR
    }
}  // namespace System.Runtime.CompilerServices

