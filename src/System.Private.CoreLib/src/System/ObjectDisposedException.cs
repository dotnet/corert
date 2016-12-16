// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Globalization;

namespace System
{
    /// <devdoc>
    ///    <para> The exception that is thrown when accessing an object that was
    ///       disposed.</para>
    /// </devdoc>
    [System.Runtime.InteropServices.ComVisible(true)]
    public class ObjectDisposedException : InvalidOperationException
    {
        private String _objectName;

        // This constructor should only be called by the EE (COMPlusThrow)
        private ObjectDisposedException() :
            this(null, SR.ObjectDisposed_Generic)
        {
        }

        public ObjectDisposedException(String objectName) :
            this(objectName, SR.ObjectDisposed_Generic)
        {
        }

        public ObjectDisposedException(String objectName, String message) : base(message)
        {
            SetErrorCode(__HResults.COR_E_OBJECTDISPOSED);
            _objectName = objectName;
        }

        public ObjectDisposedException(String message, Exception innerException)
            : base(message, innerException)
        {
            SetErrorCode(__HResults.COR_E_OBJECTDISPOSED);
        }

        /// <devdoc>
        ///    <para>Gets the text for the message for this exception.</para>
        /// </devdoc>
        public override String Message
        {
            get
            {
                String name = ObjectName;
                if (name == null || name.Length == 0)
                    return base.Message;

                String objectDisposed = SR.Format(SR.ObjectDisposed_ObjectName_Name, name);
                return base.Message + Environment.NewLine + objectDisposed;
            }
        }

        public String ObjectName
        {
            get
            {
                if ((_objectName == null)) // && !CompatibilitySwitches.IsAppEarlierThanWindowsPhone8)
                {
                    return String.Empty;
                }
                return _objectName;
            }
        }
    }
}
