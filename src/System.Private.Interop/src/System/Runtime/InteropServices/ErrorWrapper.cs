// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

//

//

/*=============================================================================
**
** Class: ErrorWrapper.
**
**
** Purpose: Wrapper that is converted to a variant with VT_ERROR.
**
**
=============================================================================*/

using System;

namespace System.Runtime.InteropServices
{
    public sealed class ErrorWrapper
    {
        public ErrorWrapper(int errorCode)
        {
            m_ErrorCode = errorCode;
        }

        public ErrorWrapper(Object errorCode)
        {
            if (!(errorCode is int))
                throw new ArgumentException(SR.Arg_MustBeInt32, "errorCode");
            m_ErrorCode = (int)errorCode;
        }

        public ErrorWrapper(Exception e)
        {
            m_ErrorCode = Marshal.GetHRForException(e);
        }

        public int ErrorCode
        {
            get
            {
                return m_ErrorCode;
            }
        }

        private int m_ErrorCode;
    }
}
