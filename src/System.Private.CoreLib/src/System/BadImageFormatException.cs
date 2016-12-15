// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

/*============================================================
**
**
**
** Purpose: Exception to an invalid dll or executable format.
**
** 
===========================================================*/

using System;
using System.Globalization;
using System.Runtime.Serialization;

namespace System
{
    public class BadImageFormatException : SystemException
    {
        private String _fileName;  // The name of the corrupt PE file.
        private String _fusionLog;  // fusion log (when applicable)

        public BadImageFormatException()
            : base(SR.Arg_BadImageFormatException)
        {
            SetErrorCode(__HResults.COR_E_BADIMAGEFORMAT);
        }

        public BadImageFormatException(String message)
            : base(message)
        {
            SetErrorCode(__HResults.COR_E_BADIMAGEFORMAT);
        }

        public BadImageFormatException(String message, Exception inner)
            : base(message, inner)
        {
            SetErrorCode(__HResults.COR_E_BADIMAGEFORMAT);
        }

        public BadImageFormatException(String message, String fileName) : base(message)
        {
            SetErrorCode(__HResults.COR_E_BADIMAGEFORMAT);
            _fileName = fileName;
        }

        public BadImageFormatException(String message, String fileName, Exception inner)
            : base(message, inner)
        {
            SetErrorCode(__HResults.COR_E_BADIMAGEFORMAT);
            _fileName = fileName;
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
            {
                //if ((_fileName == null) &&
                //    (HResult == System.__HResults.COR_E_EXCEPTION))
                _message = SR.Format(SR.BadImageFormatException_CouldNotLoadFileOrAssembly, _fileName);
                //else
                //TODO: Implement support to contain the correctly formatted message when using a filename
                //    _message = FileLoadException.FormatFileLoadExceptionMessage(_fileName, HResult);
            }
        }

        public String FileName
        {
            get { return _fileName; }
        }

        public override String ToString()
        {
            String s = GetType().ToString() + ": " + Message;

            if (_fileName != null && _fileName.Length != 0)
                s += Environment.NewLine + SR.Format(SR.IO_FileName_Name, _fileName);

            if (InnerException != null)
                s = s + " ---> " + InnerException.ToString();

            if (StackTrace != null)
                s += Environment.NewLine + StackTrace;

            return s;
        }

        protected BadImageFormatException(SerializationInfo info, StreamingContext context) 
            : base(info, context) 
        {
            // Base class constructor will check info != null.

            _fileName = info.GetString("BadImageFormat_FileName");
            try
            {
                _fusionLog = info.GetString("BadImageFormat_FusionLog");
            }
            catch 
            {
                _fusionLog = null;
            }
        }

        public String FusionLog 
        {
            get { return _fusionLog; }
        }
    }
}
