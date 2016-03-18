// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

/*=============================================================================
**
**
**
** Purpose: The exception class for type loading failures.
**
**
=============================================================================*/

using System;
using System.Globalization;
using System.Runtime.InteropServices;
using System.Runtime.CompilerServices;
using System.Security;
using System.Diagnostics.Contracts;

namespace System
{
    public class TypeLoadException : Exception
    {
        public TypeLoadException()
            : base(SR.Arg_TypeLoadException)
        {
            SetErrorCode(__HResults.COR_E_TYPELOAD);
        }

        public TypeLoadException(String message)
            : base(message)
        {
            SetErrorCode(__HResults.COR_E_TYPELOAD);
        }

        public TypeLoadException(String message, Exception inner)
            : base(message, inner)
        {
            SetErrorCode(__HResults.COR_E_TYPELOAD);
        }

        internal TypeLoadException(String message, String typeName)
            : base(message)
        {
            SetErrorCode(__HResults.COR_E_TYPELOAD);
            _typeName = typeName;
        }

        public override String Message
        {
            get
            {
                SetMessageField();
                return _message;
            }
        }

        private void SetMessageField()
        {
            if (_message == null)
                _message = SR.Arg_TypeLoadException;
        }

        public String TypeName
        {
            get
            {
                if (_typeName == null)
                    return String.Empty;
                return _typeName;
            }
        }

        private String _typeName;
    }
}
