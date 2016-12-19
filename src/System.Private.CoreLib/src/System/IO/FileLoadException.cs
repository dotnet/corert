// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Runtime.Serialization;

namespace System.IO
{
    [Serializable]
    public class FileLoadException : IOException
    {
        private String _fileName;   // the name of the file we could not load.
        private String _fusionLog;  // fusion log (when applicable)

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

        protected FileLoadException(SerializationInfo info, StreamingContext context) : base(info, context)
        {
            // Base class constructor will check info != null.
            _fileName = info.GetString("FileLoad_FileName");

            try
            {
                _fusionLog = info.GetString("FileLoad_FusionLog");
            }
            catch
            {
                _fusionLog = null;
            }
        }

        public override void GetObjectData(SerializationInfo info, StreamingContext context)
        {
            base.GetObjectData(info, context);

            info.AddValue("FileLoad_FileName", _fileName, typeof(String));
            info.AddValue("FileLoad_FusionLog", _fusionLog, typeof(String));
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

        public String FusionLog
        {
            get { return _fusionLog; }
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
