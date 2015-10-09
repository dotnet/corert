// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System;
using System.Threading;

namespace System.Globalization
{
    [System.Runtime.InteropServices.ComVisible(true)]
    public class CultureNotFoundException : ArgumentException
    {
        private string _invalidCultureName; // unrecognized culture name

        public CultureNotFoundException()
            : base(DefaultMessage)
        {
        }

        public CultureNotFoundException(String message)
            : base(message)
        {
        }

        public CultureNotFoundException(String paramName, String message)
            : base(message, paramName)
        {
        }

        public CultureNotFoundException(String message, Exception innerException)
            : base(message, innerException)
        {
        }

        public CultureNotFoundException(String paramName, string invalidCultureName, String message)
            : base(message, paramName)
        {
            _invalidCultureName = invalidCultureName;
        }

        public CultureNotFoundException(String message, string invalidCultureName, Exception innerException)
            : base(message, innerException)
        {
            _invalidCultureName = invalidCultureName;
        }


        public virtual string InvalidCultureName
        {
            get { return _invalidCultureName; }
        }

        private static String DefaultMessage
        {
            get
            {
                return SR.Argument_CultureNotSupported;
            }
        }

        private String FormatedInvalidCultureId
        {
            get
            {
                return InvalidCultureName;
            }
        }

        public override String Message
        {
            get
            {
                String s = base.Message;
                if (
                    _invalidCultureName != null)
                {
                    String valueMessage = SR.Format(SR.Argument_CultureInvalidIdentifier, FormatedInvalidCultureId);
                    if (s == null)
                        return valueMessage;
                    return s + Environment.NewLine + valueMessage;
                }
                return s;
            }
        }
    }
}
