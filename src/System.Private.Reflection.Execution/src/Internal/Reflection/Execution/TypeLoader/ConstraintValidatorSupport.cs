// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Collections.Generic;
using System.Reflection;
using Internal.Reflection.Extensibility;
using Debug = global::System.Diagnostics.Debug;

namespace Internal.Reflection.Execution
{
    internal static partial class ConstraintValidator
    {
        //
        // We cannot do the constraint validation against real TypeInfo because of constraints need to be validated 
        // before the type is built.
        //
        // InstantiatedType allows us to use TypeInfo for constraint validation without creating a real TypeInfo.
        // It implements just enough methods for constraint validation to work, and performs type variable substitution 
        // as necesary.
        //

        private struct SigTypeContext
        {
            public readonly TypeInfo[] TypeInstantiation;
            public readonly TypeInfo[] MethodInstantiation;

            public SigTypeContext(TypeInfo[] typeInstantiation, TypeInfo[] methodInstantiation)
            {
                TypeInstantiation = typeInstantiation;
                MethodInstantiation = methodInstantiation;
            }
        }

        private class InstantiatedType : ExtensibleType, IReflectableType
        {
            private InstantiatedTypeInfo _typeInfo;

            public InstantiatedType(InstantiatedTypeInfo typeInfo)
            {
                _typeInfo = typeInfo;
            }

            TypeInfo IReflectableType.GetTypeInfo()
            {
                return _typeInfo;
            }

            //
            // These methods can't be overriden on TypeInfo directly
            //
            public override bool IsArray
            {
                get
                {
                    return _typeInfo.UnderlyingTypeInfo.IsArray;
                }
            }

            public override bool IsByRef
            {
                get
                {
                    return _typeInfo.UnderlyingTypeInfo.IsByRef;
                }
            }

            public override bool IsPointer
            {
                get
                {
                    return _typeInfo.UnderlyingTypeInfo.IsPointer;
                }
            }

            public override String AssemblyQualifiedName { get { Debug.Assert(false); throw NotImplemented.ByDesign; } }
            public override String FullName { get { Debug.Assert(false); throw NotImplemented.ByDesign; } }
            public override int GenericParameterPosition { get { Debug.Assert(false); throw NotImplemented.ByDesign; } }
            public override Type[] GenericTypeArguments { get { Debug.Assert(false); throw NotImplemented.ByDesign; } }
            public override bool IsConstructedGenericType { get { Debug.Assert(false); throw NotImplemented.ByDesign; } }
            public override bool IsGenericParameter { get { Debug.Assert(false); throw NotImplemented.ByDesign; } }
            public override int GetArrayRank() { Debug.Assert(false); throw NotImplemented.ByDesign; }
            public override Type GetElementType() { Debug.Assert(false); throw NotImplemented.ByDesign; }
            public override Type GetGenericTypeDefinition() { Debug.Assert(false); throw NotImplemented.ByDesign; }
            public override Type MakeArrayType() { Debug.Assert(false); throw NotImplemented.ByDesign; }
            public override Type MakeArrayType(int rank) { Debug.Assert(false); throw NotImplemented.ByDesign; }
            public override Type MakeByRefType() { Debug.Assert(false); throw NotImplemented.ByDesign; }
            public override Type MakeGenericType(params Type[] instantiation) { Debug.Assert(false); throw NotImplemented.ByDesign; }
            public override Type MakePointerType() { Debug.Assert(false); throw NotImplemented.ByDesign; }
            public override Type DeclaringType { get { Debug.Assert(false); throw NotImplemented.ByDesign; } }
            public override string Namespace { get { Debug.Assert(false); throw NotImplemented.ByDesign; } }
            public override String Name { get { Debug.Assert(false); throw NotImplemented.ByDesign; } }
        }

        private class InstantiatedTypeInfo : ExtensibleTypeInfo
        {
            private TypeInfo _underlyingTypeInfo;
            private SigTypeContext _context;

            private InstantiatedType _type;

            public InstantiatedTypeInfo(TypeInfo underlyingTypeInfo, SigTypeContext context)
            {
                _underlyingTypeInfo = underlyingTypeInfo;
                _context = context;
            }

            public TypeInfo UnderlyingTypeInfo
            {
                get
                {
                    return _underlyingTypeInfo;
                }
            }

            public override Type AsType()
            {
                if (_type == null)
                    _type = new InstantiatedType(this);
                return _type;
            }

            public override TypeAttributes Attributes
            {
                get
                {
                    return _underlyingTypeInfo.Attributes;
                }
            }

            public override IEnumerable<Type> ImplementedInterfaces
            {
                get
                {
                    foreach (var iface in _underlyingTypeInfo.ImplementedInterfaces)
                    {
                        yield return iface.GetTypeInfo().Instantiate(_context).AsType();
                    }
                }
            }

            public override bool IsValueType
            {
                get
                {
                    return _underlyingTypeInfo.IsValueType;
                }
            }

            public override bool IsGenericType
            {
                get
                {
                    return _underlyingTypeInfo.IsGenericType;
                }
            }

            public override Type GetGenericTypeDefinition()
            {
                return _underlyingTypeInfo.GetGenericTypeDefinition();
            }

            public override int GetArrayRank()
            {
                return _underlyingTypeInfo.GetArrayRank();
            }

            public override Type GetElementType()
            {
                return _underlyingTypeInfo.GetElementType().GetTypeInfo().Instantiate(_context).AsType();
            }

            public override Type[] GenericTypeArguments
            {
                get
                {
                    Type[] arguments = _underlyingTypeInfo.GenericTypeArguments;
                    for (int i = 0; i < arguments.Length; i++)
                    {
                        TypeInfo typeInfo = arguments[i].GetTypeInfo();
                        typeInfo = typeInfo.Instantiate(_context);
                        arguments[i] = typeInfo.AsType();
                    }
                    return arguments;
                }
            }

            public override Type BaseType
            {
                get
                {
                    return _underlyingTypeInfo.BaseType.GetTypeInfo().Instantiate(_context).AsType();
                }
            }

            public sealed override Assembly Assembly { get { Debug.Assert(false); throw NotImplemented.ByDesign; } }
            public sealed override String AssemblyQualifiedName { get { Debug.Assert(false); throw NotImplemented.ByDesign; } }
            public override bool ContainsGenericParameters { get { Debug.Assert(false); throw NotImplemented.ByDesign; } }
            public override MethodBase DeclaringMethod { get { Debug.Assert(false); throw NotImplemented.ByDesign; } }
            public override String FullName { get { Debug.Assert(false); throw NotImplemented.ByDesign; } }
            public override GenericParameterAttributes GenericParameterAttributes { get { Debug.Assert(false); throw NotImplemented.ByDesign; } }
            public override int GenericParameterPosition { get { Debug.Assert(false); throw NotImplemented.ByDesign; } }
            public override Guid GUID { get { Debug.Assert(false); throw NotImplemented.ByDesign; } }
            public override bool IsEnum { get { Debug.Assert(false); throw NotImplemented.ByDesign; } }
            public override bool IsGenericParameter { get { Debug.Assert(false); throw NotImplemented.ByDesign; } }
            public override bool IsGenericTypeDefinition { get { Debug.Assert(false); throw NotImplemented.ByDesign; } }
            public override bool IsSerializable { get { Debug.Assert(false); throw NotImplemented.ByDesign; } }
            public override String Namespace { get { Debug.Assert(false); throw NotImplemented.ByDesign; } }
            public override Type[] GetGenericParameterConstraints() { Debug.Assert(false); throw NotImplemented.ByDesign; }
            public sealed override Type MakeArrayType() { Debug.Assert(false); throw NotImplemented.ByDesign; }
            public sealed override Type MakeArrayType(int rank) { Debug.Assert(false); throw NotImplemented.ByDesign; }
            public sealed override Type MakeByRefType() { Debug.Assert(false); throw NotImplemented.ByDesign; }
            public sealed override Type MakeGenericType(params Type[] instantiation) { Debug.Assert(false); throw NotImplemented.ByDesign; }
            public sealed override Type MakePointerType() { Debug.Assert(false); throw NotImplemented.ByDesign; }
            public override Type DeclaringType { get { Debug.Assert(false); throw NotImplemented.ByDesign; } }
            public sealed override String Name { get { Debug.Assert(false); throw NotImplemented.ByDesign; } }
        }

        private static TypeInfo Instantiate(this TypeInfo type, SigTypeContext context)
        {
            if (type.IsGenericParameter)
            {
                int position = type.GenericParameterPosition;
                if (type.DeclaringMethod != null)
                {
                    return context.MethodInstantiation[position];
                }
                else
                {
                    Debug.Assert(type.DeclaringType != null);
                    return context.TypeInstantiation[position];
                }
            }

            if (type.ContainsGenericParameters)
            {
                //
                // Note we can come here for both generic and non-generic types. Consider this example:
                //
                // interface IFoo<T> { }
                // class Foo<U> : IFoo<U[]> { }
                //
                // var foo = typeof(Foo<>).GetTypeInfo();
                // var ifoo = foo.ImplementedInterfaces.First().GetTypeInfo();
                // var arg = ifoo.GetGenericArguments()[0].GetTypeInfo();
                //
                // arg.ContainsGenericParameters will be true, but arg.IsGenericType will be false.
                //
                return new InstantiatedTypeInfo(type, context);
            }

            return type;
        }

        private static bool IsInstantiatedTypeInfo(this TypeInfo type)
        {
            return type is InstantiatedTypeInfo;
        }

        //
        // Other helper methods to support constraint validation
        //

        private static bool IsNullable(this TypeInfo type)
        {
            return type.IsGenericType && CommonRuntimeTypes.Nullable.Equals(type.GetGenericTypeDefinition());
        }

        private static Type GetNullableType(this TypeInfo type)
        {
            Debug.Assert(type.IsNullable());

            Type[] arguments = type.GenericTypeArguments;
            Debug.Assert(arguments.Length == 1);

            return arguments[0];
        }

        private static bool IsSystemObject(this TypeInfo type)
        {
            return CommonRuntimeTypes.Object.Equals(type.AsType());
        }

        private static bool IsSystemValueType(this TypeInfo type)
        {
            return CommonRuntimeTypes.ValueType.Equals(type.AsType());
        }

        private static bool IsSystemArray(this TypeInfo type)
        {
            return CommonRuntimeTypes.Array.Equals(type.AsType());
        }

        private static bool IsSystemVoid(this TypeInfo type)
        {
            return CommonRuntimeTypes.Void.Equals(type.AsType());
        }

        private static bool HasExplicitOrImplicitPublicDefaultConstructor(this TypeInfo type)
        {
            // Strip InstantiatedTypeInfo - DeclaredConstructors is not implemented on InstantiatedTypeInfo
            if (type is InstantiatedTypeInfo)
                type = ((InstantiatedTypeInfo)type).UnderlyingTypeInfo;

            // valuetypes have public default ctors implicitly
            if (type.IsValueType)
                return true;

            foreach (var ctor in type.DeclaredConstructors)
            {
                if (!ctor.IsStatic && ctor.IsPublic && ctor.GetParameters().Length == 0)
                    return true;
            }
            return false;
        }

        private unsafe static int NormalizedPrimitiveTypeSizeForIntegerTypes(this TypeInfo type)
        {
            // Strip InstantiatedTypeInfo - IsEnum is not implemented on InstantiatedTypeInfo
            if (type is InstantiatedTypeInfo)
                type = ((InstantiatedTypeInfo)type).UnderlyingTypeInfo;

            Type normalizedType;

            if (type.IsEnum)
            {
                // TODO: Enum.GetUnderlyingType does not work for generic type definitions
                return NormalizedPrimitiveTypeSizeForIntegerTypes(Enum.GetUnderlyingType(type.AsType()).GetTypeInfo());
            }
            else
            if (type.IsPrimitive)
            {
                normalizedType = type.AsType();
            }
            else
            {
                return 0;
            }

            if (CommonRuntimeTypes.Byte.Equals(normalizedType) || CommonRuntimeTypes.SByte.Equals(normalizedType))
                return 1;

            if (CommonRuntimeTypes.UInt16.Equals(normalizedType) || CommonRuntimeTypes.Int16.Equals(normalizedType))
                return 2;

            if (CommonRuntimeTypes.UInt32.Equals(normalizedType) || CommonRuntimeTypes.Int32.Equals(normalizedType))
                return 4;

            if (CommonRuntimeTypes.UInt64.Equals(normalizedType) || CommonRuntimeTypes.Int64.Equals(normalizedType))
                return 8;

            if (CommonRuntimeTypes.UIntPtr.Equals(normalizedType) || CommonRuntimeTypes.IntPtr.Equals(normalizedType))
                return sizeof(IntPtr);

            return 0;
        }
    }
}
