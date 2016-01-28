// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

/*=============================================================================
**
**
**
** Purpose: Exception class for dereferencing a null reference.
**
**
=============================================================================*/

namespace System
{
    // NullReferenceException is required by Bartok due to an internal implementation detail, but Redhawk 
    // does not promote AVs to NullReferenceExceptions, so it won't be catchable unless someone explicitly 
    // has a 'throw new NullReferenceException()'

    [System.Runtime.InteropServices.ComVisible(true)]
    public class NullReferenceException : Exception
    {
        public NullReferenceException()
            : base(SR.Arg_NullReferenceException)
        {
            SetErrorCode(__HResults.COR_E_NULLREFERENCE);
        }

        public NullReferenceException(String message)
            : base(message)
        {
            SetErrorCode(__HResults.COR_E_NULLREFERENCE);
        }

        public NullReferenceException(String message, Exception innerException)
            : base(message, innerException)
        {
            SetErrorCode(__HResults.COR_E_NULLREFERENCE);
        }
    }
}
