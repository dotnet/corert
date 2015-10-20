// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System;

namespace System.IO
{
    [System.Runtime.InteropServices.ComVisible(true)]
    public class FileLoadException : IOException
    {
        private String _fileName;   // the name of the file we could not load.

        public FileLoadException()
            : base(SR.IO_FileLoad)
        {
            HResult = __HResults.COR_E_FILELOAD;
        }

        public FileLoadException(String message)
            : base(message)
        {
            HResult = __HResults.COR_E_FILELOAD;
        }

        public FileLoadException(String message, Exception inner)
            : base(message, inner)
        {
            HResult = __HResults.COR_E_FILELOAD;
        }

        public FileLoadException(String message, String fileName) : base(message)
        {
            HResult = __HResults.COR_E_FILELOAD;
            _fileName = fileName;
        }

        public FileLoadException(String message, String fileName, Exception inner)
            : base(message, inner)
        {
            HResult = __HResults.COR_E_FILELOAD;
            _fileName = fileName;
        }

        public String FileName
        {
            get { return _fileName; }
        }

        public override String Message
        {
            get
            {
                if (_message == null)
                    _message = SR.IO_FileLoad;

                return _message;
            }
        }

        public override String ToString()
        {
            String s = GetType().ToString() + ": " + Message;

            if (_fileName != null && _fileName.Length != 0)
                s += Environment.NewLine + SR.IO_FileName_Name + ": '" + _fileName + "'";

            if (InnerException != null)
                s = s + " ---> " + InnerException.ToString();

            if (StackTrace != null)
                s += Environment.NewLine + StackTrace;

            return s;
        }
    }
}
