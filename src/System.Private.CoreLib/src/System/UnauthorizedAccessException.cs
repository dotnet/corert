// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

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
