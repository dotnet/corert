// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System.Runtime.Serialization;

namespace System.Security
{
    [System.Runtime.InteropServices.ComVisible(true)]
    public class SecurityException : SystemException
    {
        public SecurityException()
            : base(SR.Arg_SecurityException)
        {
            SetErrorCode(System.__HResults.COR_E_SECURITY);
        }

        public SecurityException(String message)
            : base(message)
        {
            SetErrorCode(System.__HResults.COR_E_SECURITY);
        }

        public SecurityException(String message, Exception inner)
            : base(message, inner)
        {
            SetErrorCode(System.__HResults.COR_E_SECURITY);
        }

        public override String ToString()
        {
            return base.ToString();
        }

        protected SecurityException(SerializationInfo info, StreamingContext context) : base(info, context)
        {
            if (info == null)
                throw new ArgumentNullException(nameof(info));
        }

        // Stubs for surface area compatibility only
        public SecurityException(String message, Type type)
            : base(message)
        {
            SetErrorCode(System.__HResults.COR_E_SECURITY);
            PermissionType = type;
        }

        public SecurityException(string message, System.Type type, string state)
            : base(message)
        {
            SetErrorCode(System.__HResults.COR_E_SECURITY);
            PermissionType = type;
            PermissionState = state;
        }

        public object Demanded { get; set; }
        public object DenySetInstance { get; set; }
        public System.Reflection.AssemblyName FailedAssemblyInfo { get; set; }
        public string GrantedSet { get; set; }
        public System.Reflection.MethodInfo Method { get; set; }
        public string PermissionState { get; set; }
        public System.Type PermissionType { get; set; }
        public object PermitOnlySetInstance { get; set; }
        public string RefusedSet { get; set; }
        public string Url { get; set; }
    }
}
