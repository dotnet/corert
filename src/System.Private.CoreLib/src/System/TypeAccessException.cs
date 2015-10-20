// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace System
{
    // TypeAccessException derives from TypeLoadException rather than MemberAccessException because in
    // pre-v4 releases of the runtime TypeLoadException was used in lieu of a TypeAccessException.
    public class TypeAccessException : TypeLoadException
    {
        public TypeAccessException()
            : base(SR.Arg_TypeAccessException)
        {
            SetErrorCode(__HResults.COR_E_TYPEACCESS);
        }

        public TypeAccessException(string message)
            : base(message)
        {
            SetErrorCode(__HResults.COR_E_TYPEACCESS);
        }

        public TypeAccessException(string message, Exception inner)
            : base(message, inner)
        {
            SetErrorCode(__HResults.COR_E_TYPEACCESS);
        }
    }
}
