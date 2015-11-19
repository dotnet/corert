// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

/*============================================================
**
** Class:  OperationCanceledException
**
**
** Purpose: Exception for cancelled IO requests.
**
**
===========================================================*/

using System;
using System.Threading;

namespace System
{
    [System.Runtime.InteropServices.ComVisible(true)]
    public class OperationCanceledException : Exception
    {
        private CancellationToken _cancellationToken;

        public CancellationToken CancellationToken
        {
            get { return _cancellationToken; }
            private set { _cancellationToken = value; }
        }

        public OperationCanceledException()
            : base(SR.OperationCanceled)
        {
            HResult = __HResults.COR_E_OPERATIONCANCELED;
        }

        public OperationCanceledException(String message)
            : base(message)
        {
            HResult = __HResults.COR_E_OPERATIONCANCELED;
        }

        public OperationCanceledException(String message, Exception innerException)
            : base(message, innerException)
        {
            HResult = __HResults.COR_E_OPERATIONCANCELED;
        }


        public OperationCanceledException(CancellationToken token)
            : this()
        {
            CancellationToken = token;
        }

        public OperationCanceledException(String message, CancellationToken token)
            : this(message)
        {
            CancellationToken = token;
        }

        public OperationCanceledException(String message, Exception innerException, CancellationToken token)
            : this(message, innerException)
        {
            CancellationToken = token;
        }
    }
}
