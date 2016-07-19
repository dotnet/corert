// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Reflection;
using System.Diagnostics;
using System.Reflection.Runtime.General;
using System.Reflection.Runtime.TypeInfos;
using Internal.Reflection.Core.NonPortable;


namespace System.Reflection.Runtime.Types
{
    //
    // TODO https://github.com/dotnet/corefx/issues/9805:
    //
    //  This is the only remaining non-abstract RuntimeType class. All RuntimeTypeInfo objects create one of these as their "AsType()" value.
    //  As its name implies, this type will go away entirely once Type's and TypeInfo's become the same instance object. 
    //
    internal sealed class RuntimeTypeTemporary : RuntimeType
    {
        public RuntimeTypeTemporary(RuntimeTypeInfo typeInfo)
            : base()
        {
            _typeInfo = typeInfo;
        }

        public sealed override TypeInfo GetTypeInfo() => _typeInfo;

        //
        // RuntimeTypeInfo's are unified using weak pointers so they use object.ReferenceEquals() for equality, and
        // a type-specific hash code (to maintain a stable hash code across weak destructions and rebirths.)
        //
        // RuntimeTypeTemporaries are created and destroyed at the same time as the associated RuntimeTypeInfo object
        // so they can use the same strategy: object.ReferenceEquals() for equality, and reuse the typeinfo's hash code.
        //
        public sealed override bool Equals(object obj)
        {
            if (object.ReferenceEquals(this, obj))
                return true;

            // TODO https://github.com/dotnet/corefx/issues/9805: This makes Equals() act as if Type and TypeInfo are already the same instance. This extra check will go away once they actually are the same instance.
            if (object.ReferenceEquals(this, _typeInfo))
                return true;

            return false;
        }

        public sealed override int GetHashCode() => _typeInfo.GetHashCode();

        public sealed override string AssemblyQualifiedName => _typeInfo.AssemblyQualifiedName;
        public sealed override Type DeclaringType => _typeInfo.DeclaringType;
        public sealed override string FullName => _typeInfo.FullName;
        public sealed override int GenericParameterPosition => _typeInfo.GenericParameterPosition;
        public sealed override Type[] GenericTypeArguments => _typeInfo.GenericTypeArguments;
        public sealed override string Namespace => _typeInfo.Namespace;

        //
        // TODO https://github.com/dotnet/corefx/issues/9805: 
        //   Hack: We cannot call _typeInfo.IsArray because TypeInfo.IsArray is non-virtual and hard-coded to call AsType().IsArray (thus, infinite recursion.)
        //   We need to add back Type.IsArrayImpl() and change IsArray to call that. But that's being done in parallel with this work so I don't want to make that change now.
        public sealed override bool IsArray => _typeInfo is RuntimeArrayTypeInfo;
        public sealed override bool IsByRef => _typeInfo is RuntimeByRefTypeInfo;
        public sealed override bool IsPointer => _typeInfo is RuntimePointerTypeInfo;

        public sealed override bool IsConstructedGenericType => _typeInfo.IsConstructedGenericType;
        public sealed override bool IsGenericParameter => _typeInfo.IsGenericParameter;

        public sealed override int GetArrayRank() => _typeInfo.GetArrayRank();
        public sealed override Type GetElementType() => _typeInfo.GetElementType();
        public sealed override Type GetGenericTypeDefinition() => _typeInfo.GetGenericTypeDefinition();

        public sealed override Type MakeArrayType() => _typeInfo.MakeArrayType();
        public sealed override Type MakeArrayType(int rank) => _typeInfo.MakeArrayType(rank);
        public sealed override Type MakeByRefType() => _typeInfo.MakeByRefType();
        public sealed override Type MakeGenericType(params Type[] instantiation) => _typeInfo.MakeGenericType(instantiation);
        public sealed override Type MakePointerType() => _typeInfo.MakePointerType();

        public sealed override string Name => _typeInfo.Name;
        public sealed override RuntimeTypeHandle TypeHandle => _typeInfo.TypeHandle;
        public sealed override string ToString() => _typeInfo.ToString();

        internal sealed override string InternalFullNameOfAssembly => _typeInfo.InternalFullNameOfAssembly;
        internal sealed override bool InternalTryGetTypeHandle(out RuntimeTypeHandle typeHandle)
        {
            typeHandle = _typeInfo.InternalTypeHandleIfAvailable;
            return !typeHandle.IsNull();
        }

        internal sealed override bool InternalIsGenericTypeDefinition => _typeInfo.IsGenericTypeDefinition;
        internal sealed override RuntimeType InternalRuntimeElementType => _typeInfo.InternalRuntimeElementType;
        internal sealed override RuntimeType[] InternalRuntimeGenericTypeArguments => _typeInfo.InternalRuntimeGenericTypeArguments;
        internal sealed override string InternalGetNameIfAvailable(ref Type rootCauseForFailure) => _typeInfo.InternalGetNameIfAvailable(ref rootCauseForFailure);

        internal sealed override bool InternalIsMultiDimArray
        {
            get
            {
                return _typeInfo.InternalIsMultiDimArray;
            }
        }

        internal sealed override bool InternalIsOpen => _typeInfo.ContainsGenericParameters;

        private readonly RuntimeTypeInfo _typeInfo;
    }
}
