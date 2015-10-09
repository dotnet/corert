// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.
//
// AbandonedMutexException
// Thrown when a wait completes because one or more mutexes was abandoned.
// AbandonedMutexs indicate serious error in user code or machine state.
////////////////////////////////////////////////////////////////////////////////

using System;
using System.Threading;
using System.Runtime.InteropServices;

namespace System.Threading
{
    [ComVisibleAttribute(false)]
    public class AbandonedMutexException : Exception
    {
        private int _mutexIndex = -1;
        private Mutex _mutex = null;

        public AbandonedMutexException()
            : base(SR.Threading_AbandonedMutexException)
        {
            SetErrorCode(__HResults.COR_E_ABANDONEDMUTEX);
        }

        public AbandonedMutexException(String message)
            : base(message)
        {
            SetErrorCode(__HResults.COR_E_ABANDONEDMUTEX);
        }

        public AbandonedMutexException(String message, Exception inner)
            : base(message, inner)
        {
            SetErrorCode(__HResults.COR_E_ABANDONEDMUTEX);
        }

        public AbandonedMutexException(int location, WaitHandle handle)
            : base(SR.Threading_AbandonedMutexException)
        {
            SetErrorCode(__HResults.COR_E_ABANDONEDMUTEX);
            SetupException(location, handle);
        }

        public AbandonedMutexException(String message, int location, WaitHandle handle)
            : base(message)
        {
            SetErrorCode(__HResults.COR_E_ABANDONEDMUTEX);
            SetupException(location, handle);
        }

        public AbandonedMutexException(String message, Exception inner, int location, WaitHandle handle)
            : base(message, inner)
        {
            SetErrorCode(__HResults.COR_E_ABANDONEDMUTEX);
            SetupException(location, handle);
        }

        private void SetupException(int location, WaitHandle handle)
        {
            _mutexIndex = location;
            if (handle != null)
                _mutex = handle as Mutex;
        }

        public Mutex Mutex
        {
            get
            {
                return _mutex;
            }
        }

        public int MutexIndex
        {
            get
            {
                return _mutexIndex;
            }
        }
    }
}

