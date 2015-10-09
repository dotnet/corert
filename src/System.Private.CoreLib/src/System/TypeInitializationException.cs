// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

/*=============================================================================
**
**
**
** Purpose: The exception class to wrap exceptions thrown by
**          a type's class initializer (.cctor).  This is sufficiently
**          distinct from a TypeLoadException, which means we couldn't
**          find the type.
**
**
=============================================================================*/

using System;
using System.Globalization;

namespace System
{
    [System.Runtime.InteropServices.ComVisible(true)]
    public sealed class TypeInitializationException : Exception
    {
        private String _typeName;

        // This exception is not creatable without specifying the
        //    inner exception.
        private TypeInitializationException()
            : base(SR.TypeInitialization_Default)
        {
            SetErrorCode(__HResults.COR_E_TYPEINITIALIZATION);
        }


        public TypeInitializationException(String fullTypeName, Exception innerException)
            : this(fullTypeName, SR.Format(SR.TypeInitialization_Type, fullTypeName), innerException)
        {
        }

        // This is called from within the runtime.  I believe this is necessary
        // for Interop only, though it's not particularly useful.
        internal TypeInitializationException(String message) : base(message)
        {
            SetErrorCode(__HResults.COR_E_TYPEINITIALIZATION);
        }

        internal TypeInitializationException(String fullTypeName, String message, Exception innerException)
            : base(message, innerException)
        {
            _typeName = fullTypeName;
            SetErrorCode(__HResults.COR_E_TYPEINITIALIZATION);
        }

        public String TypeName
        {
            get
            {
                if (_typeName == null)
                {
                    return String.Empty;
                }
                return _typeName;
            }
        }
    }
}
