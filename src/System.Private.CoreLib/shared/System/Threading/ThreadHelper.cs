// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.Runtime.ConstrainedExecution;
using System.Security.Principal;

namespace System.Threading
{
    internal class ThreadHelper
    {
        private Delegate _start;
        internal CultureInfo _startCulture;
        internal CultureInfo _startUICulture;
        private object _startArg = null;
        private ExecutionContext _executionContext = null;

        internal ThreadHelper(Delegate start)
        {
            _start = start; 
        }

        internal void SetExecutionContextHelper(ExecutionContext ec)
        {
            _executionContext = ec;
        }

        internal static ContextCallback _ccb = new ContextCallback(ThreadStart_Context);

        private static void ThreadStart_Context(object state)
        {
            ThreadHelper t = (ThreadHelper)state;
            if (t._start is ThreadStart)
            {
                ((ThreadStart)t._start)();
            }
            else
            {
                ((ParameterizedThreadStart)t._start)(t._startArg);
            }
        }

        // call back helper
        internal void ThreadStart(object obj)
        {
            _startArg = obj;

            if (_startCulture != null)
            {
                CultureInfo.CurrentCulture = _startCulture;
                _startCulture = null;
            }

            if (_startUICulture != null)
            {
                CultureInfo.CurrentUICulture = _startUICulture;
                _startUICulture = null;
            }

            ExecutionContext context = _executionContext;
            if (context != null)
            {
                ExecutionContext.RunInternal(context, _ccb, (object)this);
            }
            else
            {
                ((ParameterizedThreadStart)_start)(obj);
            }
        }

        // call back helper
        internal void ThreadStart()
        {
            ExecutionContext context = _executionContext;
            if (context != null)
            {
                ExecutionContext.RunInternal(context, _ccb, (object)this);
            }
            else
            {
                ((ThreadStart)_start)();
            }
        }
    }
}
