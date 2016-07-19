// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Reflection;
using System.Diagnostics;
using System.Collections.Generic;
using System.Runtime.CompilerServices;

using Internal.Reflection.Extensibility;

namespace Internal.Reflection.Core.NonPortable
{
    //
    // TODO https://github.com/dotnet/corefx/issues/9805:
    //
    //   With Type and TypeInfo being merged back into the same object, RuntimeType is being phased out in favor of RuntimeTypeInfo.
    //   This phaseout will happen slowly since RuntimeType is a widely used exchange type.
    //
    internal abstract class RuntimeType : ExtensibleType, IEquatable<RuntimeType>, ICloneable, IReflectableType, IRuntimeImplementedType
    {
        protected RuntimeType()
            : base()
        {
        }

        public object Clone()
        {
            return this;
        }

        public abstract TypeInfo GetTypeInfo();

        public abstract override bool Equals(object obj);
        public abstract override int GetHashCode();

        public bool Equals(RuntimeType runtimeType)
        {
            return object.ReferenceEquals(this, runtimeType);
        }

        public abstract override string AssemblyQualifiedName { get; }
        public abstract override Type DeclaringType { get; }
        public abstract override string FullName { get; }
        public abstract override int GenericParameterPosition { get; }
        public abstract override Type[] GenericTypeArguments { get; }
        public abstract override bool IsArray { get; }
        public abstract override bool IsByRef { get; }
        public abstract override bool IsConstructedGenericType { get; }
        public abstract override bool IsGenericParameter { get; }
        public abstract override bool IsPointer { get; }
        public abstract override int GetArrayRank();
        public abstract override Type GetElementType();
        public abstract override Type GetGenericTypeDefinition();
        public abstract override Type MakeArrayType();
        public abstract override Type MakeArrayType(int rank);
        public abstract override Type MakeByRefType();
        public abstract override Type MakeGenericType(params Type[] instantiation);
        public abstract override Type MakePointerType();
        public abstract override string Name { get; }
        public abstract override string Namespace { get; }
        public abstract override RuntimeTypeHandle TypeHandle { get; }
        public abstract override string ToString();

        internal abstract string InternalFullNameOfAssembly { get; }
        internal abstract bool InternalTryGetTypeHandle(out RuntimeTypeHandle typeHandle);
        internal abstract bool InternalIsGenericTypeDefinition { get; }
        internal abstract RuntimeType InternalRuntimeElementType { get; }
        internal abstract RuntimeType[] InternalRuntimeGenericTypeArguments { get; }

        internal string InternalNameIfAvailable
        {
            get
            {
                RuntimeType ignore = null;
                return InternalGetNameIfAvailable(ref ignore);
            }
        }

        internal abstract string InternalGetNameIfAvailable(ref RuntimeType rootCauseForFailure);

        internal abstract bool InternalIsMultiDimArray { get; }
        internal abstract bool InternalIsOpen { get; }
    }
}


