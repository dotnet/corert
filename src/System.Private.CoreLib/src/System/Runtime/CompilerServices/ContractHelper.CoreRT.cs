// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System.Diagnostics.Contracts;
using DeveloperExperience = Internal.DeveloperExperience.DeveloperExperience;

namespace System.Runtime.CompilerServices
{
    public static partial class ContractHelper
    {
        /// <summary>
        /// Rewriter calls this method to get the default failure behavior.
        /// </summary>
        [System.Diagnostics.DebuggerNonUserCode]
        public static void TriggerFailure(ContractFailureKind kind, string displayMessage, string userMessage, string conditionText, Exception innerException)
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
    }
}
