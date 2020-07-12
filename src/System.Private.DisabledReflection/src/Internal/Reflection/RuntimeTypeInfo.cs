// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using System.Collections.Concurrent;
using System.Globalization;
using System.Reflection;

using Internal.Runtime.Augments;
using Internal.Reflection.Augments;
using Internal.Reflection.Core.NonPortable;
using System.Collections.Generic;

namespace Internal.Reflection
{
    internal sealed class RuntimeTypeInfo : TypeInfo, IRuntimeImplemented
    {
        private readonly RuntimeTypeHandle _typeHandle;

        public RuntimeTypeInfo(RuntimeTypeHandle typeHandle)
        {
            _typeHandle = typeHandle;
        }

        private bool DoNotThrowForNames => AppContext.TryGetSwitch("Switch.System.Reflection.Disabled.DoNotThrowForNames", out bool doNotThrow) && doNotThrow;

        private bool DoNotThrowForAssembly => AppContext.TryGetSwitch("Switch.System.Reflection.Disabled.DoNotThrowForAssembly", out bool doNotThrow) && doNotThrow;

        private bool DoNotThrowForAttributes => AppContext.TryGetSwitch("Switch.System.Reflection.Disabled.DoNotThrowForAttributes", out bool doNotThrow) && doNotThrow;

        public override RuntimeTypeHandle TypeHandle => _typeHandle;

        public override string Name => DoNotThrowForNames ? RuntimeAugments.GetLastResortString(_typeHandle) : throw new NotSupportedException(SR.Reflection_Disabled);

        public override string Namespace => DoNotThrowForNames ? "" : throw new NotSupportedException(SR.Reflection_Disabled);

        public override string FullName => Name;

        public override string AssemblyQualifiedName => throw new NotSupportedException(SR.Reflection_Disabled);

        public override Assembly Assembly => DoNotThrowForAssembly ? Assembly.GetExecutingAssembly() :  throw new NotSupportedException(SR.Reflection_Disabled);

        public override Module Module => throw new NotSupportedException(SR.Reflection_Disabled);

        public override Type UnderlyingSystemType => this;

        public override Guid GUID => throw new NotSupportedException(SR.Reflection_Disabled);

        public override Type BaseType
        {
            get
            {
                if (RuntimeAugments.TryGetBaseType(_typeHandle, out RuntimeTypeHandle baseTypeHandle))
                {
                    return GetRuntimeTypeInfo(baseTypeHandle);
                }

                return null;
            }
        }

        public override bool IsByRefLike => RuntimeAugments.IsByRefLike(_typeHandle);

        protected override bool IsValueTypeImpl()
        {
            return RuntimeAugments.IsValueType(_typeHandle);
        }

        protected override TypeCode GetTypeCodeImpl()
        {
            return ReflectionAugments.GetRuntimeTypeCode(this);
        }

        public override string ToString()
        {
            return RuntimeAugments.GetLastResortString(_typeHandle);
        }

        public override int GetHashCode()
        {
            return _typeHandle.GetHashCode();
        }

        public override ConstructorInfo[] GetConstructors(BindingFlags bindingAttr) => throw new NotSupportedException(SR.Reflection_Disabled);

        public override object[] GetCustomAttributes(bool inherit) => DoNotThrowForAttributes ? new Attribute[0] :  throw new NotSupportedException(SR.Reflection_Disabled);

        public override object[] GetCustomAttributes(Type attributeType, bool inherit) => DoNotThrowForAttributes ? new Attribute[0] : throw new NotSupportedException(SR.Reflection_Disabled);

        public override IList<CustomAttributeData> GetCustomAttributesData() => DoNotThrowForAttributes ? new List<CustomAttributeData>().AsReadOnly() : throw new NotSupportedException(SR.Reflection_Disabled);

        public override Type GetElementType()
        {
            if (RuntimeAugments.IsArrayType(_typeHandle) || RuntimeAugments.IsUnmanagedPointerType(_typeHandle) || RuntimeAugments.IsByRefType(_typeHandle))
            {
                return GetRuntimeTypeInfo(RuntimeAugments.GetRelatedParameterTypeHandle(_typeHandle));
            }

            return null;
        }

        public override EventInfo GetEvent(string name, BindingFlags bindingAttr) => throw new NotSupportedException(SR.Reflection_Disabled);

        public override EventInfo[] GetEvents(BindingFlags bindingAttr) => throw new NotSupportedException(SR.Reflection_Disabled);

        public override FieldInfo GetField(string name, BindingFlags bindingAttr) => throw new NotSupportedException(SR.Reflection_Disabled);

        public override FieldInfo[] GetFields(BindingFlags bindingAttr) => throw new NotSupportedException(SR.Reflection_Disabled);

        public override Type GetInterface(string name, bool ignoreCase) => throw new NotSupportedException(SR.Reflection_Disabled);

        public override Type[] GetInterfaces()
        {
            int count = RuntimeAugments.GetInterfaceCount(_typeHandle);
            if (count == 0)
                return Type.EmptyTypes;

            Type[] result = new Type[count];
            for (int i = 0; i < result.Length; i++)
            {
                result[i] = GetRuntimeTypeInfo(RuntimeAugments.GetInterface(_typeHandle, i));
            }

            return result;
        }

        public override MemberInfo[] GetMembers(BindingFlags bindingAttr) => throw new NotSupportedException(SR.Reflection_Disabled);

        public override MethodInfo[] GetMethods(BindingFlags bindingAttr) => throw new NotSupportedException(SR.Reflection_Disabled);

        public override Type GetNestedType(string name, BindingFlags bindingAttr) => throw new NotSupportedException(SR.Reflection_Disabled);

        public override Type[] GetNestedTypes(BindingFlags bindingAttr) => throw new NotSupportedException(SR.Reflection_Disabled);

        public override PropertyInfo[] GetProperties(BindingFlags bindingAttr) => throw new NotSupportedException(SR.Reflection_Disabled);

        public override object InvokeMember(string name, BindingFlags invokeAttr, Binder binder, object target, object[] args, ParameterModifier[] modifiers, CultureInfo culture, string[] namedParameters)
            => throw new NotSupportedException(SR.Reflection_Disabled);

        public override bool IsDefined(Type attributeType, bool inherit) => throw new NotSupportedException(SR.Reflection_Disabled);

        protected override TypeAttributes GetAttributeFlagsImpl() => throw new NotSupportedException(SR.Reflection_Disabled);

        protected override ConstructorInfo GetConstructorImpl(BindingFlags bindingAttr, Binder binder, CallingConventions callConvention, Type[] types, ParameterModifier[] modifiers)
            => throw new NotSupportedException(SR.Reflection_Disabled);

        protected override MethodInfo GetMethodImpl(string name, BindingFlags bindingAttr, Binder binder, CallingConventions callConvention, Type[] types, ParameterModifier[] modifiers)
            => throw new NotSupportedException(SR.Reflection_Disabled);

        protected override PropertyInfo GetPropertyImpl(string name, BindingFlags bindingAttr, Binder binder, Type returnType, Type[] types, ParameterModifier[] modifiers)
             => throw new NotSupportedException(SR.Reflection_Disabled);

        protected override bool HasElementTypeImpl()
        {
            return RuntimeAugments.IsArrayType(_typeHandle) || RuntimeAugments.IsUnmanagedPointerType(_typeHandle) || RuntimeAugments.IsByRefType(_typeHandle);
        }

        protected override bool IsArrayImpl()
        {
            return RuntimeAugments.IsArrayType(_typeHandle);
        }

        protected override bool IsByRefImpl()
        {
            return RuntimeAugments.IsByRefType(_typeHandle);
        }

        protected override bool IsCOMObjectImpl() => throw new NotSupportedException(SR.Reflection_Disabled);

        protected override bool IsPointerImpl()
        {
            return RuntimeAugments.IsUnmanagedPointerType(_typeHandle);
        }

        protected override bool IsPrimitiveImpl()
        {
            return RuntimeAugments.IsPrimitive(_typeHandle);
        }

        internal static RuntimeTypeInfo GetRuntimeTypeInfo(RuntimeTypeHandle typeHandle)
        {
            return RuntimeTypeTable.Table.GetOrAdd(new RuntimeTypeHandleKey(typeHandle));
        }

        public override Type MakeArrayType()
        {
            // We support enough of MakeArrayType to make enum operations work
            if (IsPrimitive)
            {
                if (this == typeof(sbyte))
                    return typeof(sbyte[]);
                else if (this == typeof(byte))
                    return typeof(byte[]);
                else if (this == typeof(short))
                    return typeof(short[]);
                else if (this == typeof(ushort))
                    return typeof(ushort[]);
                else if (this == typeof(int))
                    return typeof(int[]);
                else if (this == typeof(uint))
                    return typeof(uint[]);
                else if (this == typeof(long))
                    return typeof(long[]);
                else if (this == typeof(ulong))
                    return typeof(ulong[]);
            }

            return base.MakeArrayType();
        }

        private sealed class RuntimeTypeTable : ConcurrentUnifierW<RuntimeTypeHandleKey, RuntimeTypeInfo>
        {
            protected sealed override RuntimeTypeInfo Factory(RuntimeTypeHandleKey key)
            {
                return new RuntimeTypeInfo(key.TypeHandle);
            }

            public static readonly RuntimeTypeTable Table = new RuntimeTypeTable();
        }

        internal struct RuntimeTypeHandleKey : IEquatable<RuntimeTypeHandleKey>
        {
            public RuntimeTypeHandleKey(RuntimeTypeHandle typeHandle)
            {
                TypeHandle = typeHandle;
            }

            public RuntimeTypeHandle TypeHandle { get; }

            public override bool Equals(object obj)
            {
                if (!(obj is RuntimeTypeHandleKey other))
                    return false;
                return Equals(other);
            }

            public bool Equals(RuntimeTypeHandleKey other)
            {
                return TypeHandle.Equals(other.TypeHandle);
            }

            public override int GetHashCode()
            {
                return TypeHandle.GetHashCode();
            }
        }
    }
}
