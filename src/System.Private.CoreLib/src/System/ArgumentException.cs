// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

/*=============================================================================
**
**
**
** Purpose: Exception class for invalid arguments to a method.
**
**
=============================================================================*/

using System;
using System.Globalization;
using System.Runtime.Serialization;

namespace System
{
    // The ArgumentException is thrown when an argument does not meet 
    // the contract of the method.  Ideally it should give a meaningful error
    // message describing what was wrong and which parameter is incorrect.
    // 
    [System.Runtime.InteropServices.ComVisible(true)]
    public class ArgumentException : SystemException
    {
        private String _paramName;

        // Creates a new ArgumentException with its message 
        // string set to the empty string. 
        public ArgumentException()
            : base(SR.Arg_ArgumentException)
        {
            SetErrorCode(__HResults.COR_E_ARGUMENT);
        }

        // Creates a new ArgumentException with its message 
        // string set to message. 
        // 
        public ArgumentException(String message)
            : base(message)
        {
            SetErrorCode(__HResults.COR_E_ARGUMENT);
        }

        public ArgumentException(String message, Exception innerException)
            : base(message, innerException)
        {
            SetErrorCode(__HResults.COR_E_ARGUMENT);
        }

        public ArgumentException(String message, String paramName, Exception innerException)
            : base(message, innerException)
        {
            _paramName = paramName;
            SetErrorCode(__HResults.COR_E_ARGUMENT);
        }

        public ArgumentException(String message, String paramName)

            : base(message)
        {
            _paramName = paramName;
            SetErrorCode(__HResults.COR_E_ARGUMENT);
        }

        protected ArgumentException(SerializationInfo info, StreamingContext context)
            : base(info, context) 
        {
            _paramName = info.GetString("ParamName");
        }

        public override String Message
        {
            get
            {
                String s = base.Message;
                if (!String.IsNullOrEmpty(_paramName))
                {
                    String resourceString = SR.Format(SR.Arg_ParamName_Name, _paramName);
                    return s + Environment.NewLine + resourceString;
                }
                else
                    return s;
            }
        }

        public virtual String ParamName
        {
            get { return _paramName; }
        }
    }
}
