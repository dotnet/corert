// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.Diagnostics;
using System.Runtime.InteropServices;

namespace System
{
    public partial class Exception
    {
        [DllImport("*")]
        internal static extern void RhpThrowEx(object exception);

        private static void DispatchExWasm(object exception)
        {
            AppendExceptionStackFrameWasm(exception, new StackTrace(1).ToString());
            //RhpThrowEx(exception); can't as not handling the transition unmanaged->managed in the landing pads.
        }

        private static void AppendExceptionStackFrameWasm(object exceptionObj, string stackTraceString)
        {
            // This method is called by the runtime's EH dispatch code and is not allowed to leak exceptions
            // back into the dispatcher.
            try
            {
                Exception ex = exceptionObj as Exception;
                if (ex == null)
                    Environment.FailFast("Exceptions must derive from the System.Exception class");

                if (!RuntimeExceptionHelpers.SafeToPerformRichExceptionSupport)
                    return;

                ex._stackTraceString = stackTraceString.Replace("__", ".").Replace("_", ".");
            }
            catch
            {
                // We may end up with a confusing stack trace or a confusing ETW trace log, but at least we
                // can continue to dispatch this exception.
            }
        }
    }
}
