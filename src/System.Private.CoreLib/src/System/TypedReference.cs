// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System.Reflection;
using System.Diagnostics;
using System.Runtime;
using System.Runtime.InteropServices;
using System.Runtime.CompilerServices;

using Internal.Runtime.Augments;
using Internal.Reflection.Augments;

#if CORERT
namespace System
#else
// Add a fake TypedReference to keep Project X running with CoreRT's type system that needs this now.
namespace System
{
    [CLSCompliant(false)]
    [StructLayout(LayoutKind.Sequential)]
    public struct TypedReference
    {
    }
}

namespace System.Reflection  //@TODO: Intentionally placing TypedReference in the wrong namespace to work around NUTC's inability to handle ELEMENT_TYPE_TYPEDBYREF.
#endif
{
    [CLSCompliant(false)]
    [StructLayout(LayoutKind.Sequential)]
    public struct TypedReference
    {
        // Do not change the ordering of these fields. The JIT has a dependency on this layout.
#if CORERT
        private readonly ByReference<byte> _value;
#else
        private readonly ByReferenceOfByte _value;
#endif
        private readonly RuntimeTypeHandle _typeHandle;

        private TypedReference(object target, int offset, RuntimeTypeHandle typeHandle)
        {
#if CORERT
            _value = new ByReference<byte>(ref Unsafe.Add<byte>(ref target.GetRawData(), offset));
#else
            _value = new ByReferenceOfByte(target, offset);
#endif
            _typeHandle = typeHandle;
        }

        public static TypedReference MakeTypedReference(object target, FieldInfo[] flds)
        {
            Type type;
            int offset;
            ReflectionAugments.ReflectionCoreCallbacks.MakeTypedReference(target, flds, out type, out offset);
            return new TypedReference(target, offset, type.TypeHandle);
        }

        public static Type GetTargetType(TypedReference value) => Type.GetTypeFromHandle(value._typeHandle);

        public static RuntimeTypeHandle TargetTypeToken(TypedReference value)
        {
            if (value._typeHandle.IsNull)
                throw new NullReferenceException(); // For compatibility;
            return value._typeHandle;
        }

        internal static RuntimeTypeHandle RawTargetTypeToken(TypedReference value)
        {
            return value._typeHandle;
        }

        public static object ToObject(TypedReference value)
        {
            RuntimeTypeHandle typeHandle = value._typeHandle;
            if (typeHandle.IsNull)
                throw new ArgumentNullException(); // For compatibility.

            EETypePtr eeType = typeHandle.ToEETypePtr();
            if (eeType.IsValueType)
            {
                return RuntimeImports.RhBox(eeType, ref value.Value);
            }
            else
            {
                return Unsafe.As<byte, object>(ref value.Value);
            }
        }

        public static void SetTypedReference(TypedReference target, object value) { throw new NotSupportedException(); }

        public override bool Equals(object o) { throw new NotSupportedException(SR.NotSupported_NYI); }
        public override int GetHashCode() => _typeHandle.IsNull ? 0 : _typeHandle.GetHashCode();

        // Not an api - declared public because of CoreLib/Reflection.Core divide.
        public bool IsNull => _typeHandle.IsNull;

        internal ref byte Value
        {
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            get
            {
                return ref _value.Value;
            }
        }

        // @todo: ByReferenceOfByte is a workaround for the fact that ByReference<T> is broken on Project N right now.
        // Once that's fixed, delete this class and replace references to ByReferenceOfByte to ByReference<byte>.
        private struct ByReferenceOfByte
        {
            public ByReferenceOfByte(object target, int offset)
            {
                _target = target;
                _offset = offset;
            }

            public ref byte Value => ref Unsafe.Add<byte>(ref Unsafe.As<IntPtr, byte>(ref _target.m_pEEType), _offset);

            private readonly object _target;
            private readonly int _offset;
        }
    }
}

