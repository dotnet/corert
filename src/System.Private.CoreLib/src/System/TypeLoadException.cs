// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

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
            [System.Security.SecuritySafeCritical]  // auto-generated
            get
            {
                SetMessageField();
                return _message;
            }
        }

        [System.Security.SecurityCritical]  // auto-generated
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
        //// This is called from inside the EE. 
        //[System.Security.SecurityCritical]  // auto-generated
        //private TypeLoadException(String className,
        //                          String assemblyName,
        //                          String messageArg,
        //                          int    resourceId)
        //: base(null)
        //{
        //    SetErrorCode(__HResults.COR_E_TYPELOAD);
        //    ClassName  = className;
        //    AssemblyName = assemblyName;
        //    MessageArg = messageArg;
        //    ResourceId = resourceId;

        //    // Set the _message field eagerly; debuggers look at this field to 
        //    // display error info. They don't call the Message property.
        //    SetMessageField();   
        //}

        //[System.Security.SecurityCritical]  // auto-generated
        //[DllImport(JitHelpers.QCall, CharSet = CharSet.Unicode)]
        //[SuppressUnmanagedCodeSecurity]
        //private static extern void GetTypeLoadExceptionMessage(int resourceId, StringHandleOnStack retString);

        //// If ClassName != null, GetMessage will construct on the fly using it
        //// and ResourceId (mscorrc.dll). This allows customization of the
        //// class name format depending on the language environment.
        //private String  ClassName;
        //private String  AssemblyName;
        //private String  MessageArg;
        //internal int    ResourceId;
    }
}
