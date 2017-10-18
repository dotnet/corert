// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System.Runtime.Serialization;

namespace System
{
    [Serializable]
    [System.Runtime.CompilerServices.TypeForwardedFrom("mscorlib, Version=4.0.0.0, Culture=neutral, PublicKeyToken=b77a5c561934e089")]
    public partial class TypeLoadException : SystemException
    {
        public TypeLoadException()
            : base(SR.Arg_TypeLoadException)
        {
            HResult = HResults.COR_E_TYPELOAD;
        }

        public TypeLoadException(string message)
            : base(message)
        {
            HResult = HResults.COR_E_TYPELOAD;
        }

        public TypeLoadException(string message, Exception inner)
            : base(message, inner)
        {
            HResult = HResults.COR_E_TYPELOAD;
        }

        protected TypeLoadException(SerializationInfo info, StreamingContext context)
            : base(info, context)
        {
            // Ignoring serialization input
        }

        public override string Message
        {
            get
            {
                if (_message == null)
                    _message = SR.Arg_TypeLoadException;
                return _message;
            }
        }

        public string TypeName
        {
            get
            {
                if (_typeName == null)
                    return string.Empty;
                return _typeName;
            }
        }

        public override void GetObjectData(SerializationInfo info, StreamingContext context)
        {
            base.GetObjectData(info, context);
            info.AddValue("TypeLoadClassName", null, typeof(string));
            info.AddValue("TypeLoadAssemblyName", null, typeof(string));
            info.AddValue("TypeLoadMessageArg", null, typeof(string));
            info.AddValue("TypeLoadResourceID", 0, typeof(int));
        }

        private readonly string _typeName;
    }
}
