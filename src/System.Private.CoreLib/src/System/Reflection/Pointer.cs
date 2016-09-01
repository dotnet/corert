// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System.Runtime.Serialization;
using System.Runtime.CompilerServices;
using Internal.Reflection.Core.NonPortable;

namespace System.Reflection
{
    public sealed unsafe class Pointer : ISerializable
    {
        public Pointer()
        {
        }

        private Pointer(void* ptr, Type ptrType)
        {
            _ptr = ptr;
            _ptrType = ptrType;
        }

        [CLSCompliant(false)]
        public static object Box(void* ptr, Type type)
        {
            if (type == null)
                throw new ArgumentNullException(nameof(type));
            if (!type.IsPointer)
                throw new ArgumentException(SR.Arg_MustBePointer, nameof(ptr));
            if (!type.IsRuntimeImplemented())
                throw new ArgumentException(SR.Arg_MustBePointer, nameof(ptr));

            return new Pointer(ptr, type);
        }

        [CLSCompliant(false)]
        public static void* Unbox(object ptr)
        {
            if (!(ptr is Pointer))
                throw new ArgumentException(SR.Arg_MustBePointer, nameof(ptr));
            return ((Pointer)ptr)._ptr;
        }

        void ISerializable.GetObjectData(SerializationInfo info, StreamingContext context) { throw new NotImplementedException(); }

        private readonly void* _ptr;
        private readonly Type _ptrType;
    }
}
