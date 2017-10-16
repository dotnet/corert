// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

/*=============================================================================
**
**
**
** Purpose: The exception class for versioning problems with DLLS.
**
**
=============================================================================*/

using System.Runtime.Serialization;

namespace System
{
    public class MissingMemberException : MemberAccessException
    {
        public MissingMemberException()
            : base(SR.Arg_MissingMemberException)
        {
            HResult = HResults.COR_E_MISSINGMEMBER;
        }

        public MissingMemberException(String message)
            : base(message)
        {
            HResult = HResults.COR_E_MISSINGMEMBER;
        }

        public MissingMemberException(String message, Exception inner)
            : base(message, inner)
        {
            HResult = HResults.COR_E_MISSINGMEMBER;
        }

        public MissingMemberException(string className, string memberName)
        {
            ClassName = className;
            MemberName = memberName;
        }

        protected MissingMemberException(SerializationInfo info, StreamingContext context)
            : base(info, context)
        {
            throw new PlatformNotSupportedException();
        }

        public override void GetObjectData(SerializationInfo info, StreamingContext context)
        {
            base.GetObjectData(info, context);
        }

        public override string Message
        {
            get
            {
                return ClassName == null ? base.Message : SR.Format(SR.MissingMember_Name, ClassName + "." + MemberName + (Signature != null ? " " + FormatSignature(Signature) : string.Empty));
            }
        }

        internal static string FormatSignature(byte[] signature)
        {
            // This is not the correct implementation, however, it's probably not worth the time to port given that 
            //  (1) it's for a diagnostic
            //  (2) Signature is non-null when this exception is created from the native runtime. Which we don't do in .Net Native.
            //  (3) Only other time the signature is non-null is if this exception object is deserialized from a persisted blob from an older runtime.
            return string.Empty;
        }

        // If ClassName != null, GetMessage will construct on the fly using it
        // and the other variables. This allows customization of the
        // format depending on the language environment.
        protected string ClassName;
        protected string MemberName;
        protected byte[] Signature;
    }
}
