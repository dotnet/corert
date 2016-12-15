// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;

namespace System {    
    [Serializable]
    [System.Runtime.InteropServices.ComVisible(true)]
    public class UnhandledExceptionEventArgs : EventArgs 
    {
        private Object _Exception;
        private bool _IsTerminating;

        public UnhandledExceptionEventArgs(Object exception, bool isTerminating) 
        {
            _Exception = exception;
            _IsTerminating = isTerminating;
        }

        public Object ExceptionObject 
        { 
            get { return _Exception; }
        }

        public bool IsTerminating 
        {
            get { return _IsTerminating; }
        }
    }
}