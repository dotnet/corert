// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.


/*============================================================
**
  Type:  ReflectionTypeLoadException
**
==============================================================*/

using System.Runtime.Serialization;

namespace System.Reflection
{
    [Serializable]
    public sealed class ReflectionTypeLoadException : SystemException, ISerializable
    {
        private Type[] _classes;
        private Exception[] _exceptions;

        public ReflectionTypeLoadException(Type[] classes, Exception[] exceptions) : base(null)
        {
            _classes = classes;
            _exceptions = exceptions;
            HResult = __HResults.COR_E_REFLECTIONTYPELOAD;
        }

        public ReflectionTypeLoadException(Type[] classes, Exception[] exceptions, String message) : base(message)
        {
            _classes = classes;
            _exceptions = exceptions;
            HResult = __HResults.COR_E_REFLECTIONTYPELOAD;
        }

        internal ReflectionTypeLoadException(SerializationInfo info, StreamingContext context) : base(info, context)
        {
            _classes = (Type[])(info.GetValue("Types", typeof(Type[])));
            _exceptions = (Exception[])(info.GetValue("Exceptions", typeof(Exception[])));
        }

        public override void GetObjectData(SerializationInfo info, StreamingContext context)
        {
            base.GetObjectData(info, context);
            info.AddValue("Types", _classes, typeof(Type[]));
            info.AddValue("Exceptions", _exceptions, typeof(Exception[]));
        }

        public Type[] Types
        {
            get { return _classes; }
        }

        public Exception[] LoaderExceptions
        {
            get { return _exceptions; }
        }
    }
}
