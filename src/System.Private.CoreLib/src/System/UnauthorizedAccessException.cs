// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

/*============================================================
**
**
**
** Purpose: An exception for OS 'access denied' types of 
**          errors, including IO and limited security types 
**          of errors.
**
** 
===========================================================*/

namespace System
{
    // The UnauthorizedAccessException is thrown when access errors 
    // occur from IO or other OS methods.  
    [System.Runtime.InteropServices.ComVisible(true)]
    public class UnauthorizedAccessException : Exception
    {
        public UnauthorizedAccessException()
            : base(SR.Arg_UnauthorizedAccessException)
        {
            SetErrorCode(__HResults.COR_E_UNAUTHORIZEDACCESS);
        }

        public UnauthorizedAccessException(String message)
            : base(message)
        {
            SetErrorCode(__HResults.COR_E_UNAUTHORIZEDACCESS);
        }

        public UnauthorizedAccessException(String message, Exception inner)
            : base(message, inner)
        {
            SetErrorCode(__HResults.COR_E_UNAUTHORIZEDACCESS);
        }
    }
}
