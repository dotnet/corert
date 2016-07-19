// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.
//
// Implements System.Type
//
// ======================================================================================

using System;
using System.Threading;
using System.Runtime;
using System.Runtime.InteropServices;
using System.Runtime.CompilerServices;
using System.Collections.Generic;
using Internal.Runtime.Augments;
using Internal.Reflection.Core.NonPortable;

namespace System
{
    public abstract class Type
    {
        protected Type()
        {
        }

        public static readonly Object Missing = System.Reflection.Missing.Value;
        public static readonly Type[] EmptyTypes = Array.Empty<Type>();

        public abstract String AssemblyQualifiedName { get; }
        public abstract Type DeclaringType { get; }
        public abstract String FullName { get; }
        public abstract int GenericParameterPosition { get; }
        public abstract Type[] GenericTypeArguments { get; }

        public bool HasElementType
        {
            get
            {
                return IsArray || IsByRef || IsPointer;
            }
        }

        public virtual bool IsArray
        {
            get
            {
                throw NotImplemented.ByDesign;
            }
        }

        public virtual bool IsByRef
        {
            get
            {
                throw NotImplemented.ByDesign;
            }
        }

        public abstract bool IsConstructedGenericType { get; }
        public abstract bool IsGenericParameter { get; }

        public bool IsNested
        {
            get
            {
                return DeclaringType != null;
            }
        }

        public virtual bool IsPointer
        {
            get
            {
                throw NotImplemented.ByDesign;
            }
        }

        public abstract String Name { get; }
        public abstract String Namespace { get; }

        public virtual RuntimeTypeHandle TypeHandle
        {
            get { throw new NotSupportedException(); }
        }

        public abstract int GetArrayRank();
        public abstract Type GetElementType();
        public abstract Type GetGenericTypeDefinition();

        public static Type GetType(String typeName)
        {
            return RuntimeAugments.Callbacks.GetType(typeName, throwOnError: false, ignoreCase: false);
        }

        public static Type GetType(String typeName, bool throwOnError)
        {
            return RuntimeAugments.Callbacks.GetType(typeName, throwOnError, ignoreCase: false);
        }

        public static Type GetType(String typeName, bool throwOnError, bool ignoreCase)
        {
            return RuntimeAugments.Callbacks.GetType(typeName, throwOnError, ignoreCase);
        }

#if CORERT
        [Intrinsic]
#endif
        public static Type GetTypeFromHandle(RuntimeTypeHandle handle)
        {
            return ReflectionCoreNonPortable.GetTypeForRuntimeTypeHandle(handle);
        }

        public abstract Type MakeArrayType();
        public abstract Type MakeArrayType(int rank);
        public abstract Type MakeByRefType();
        public abstract Type MakeGenericType(params Type[] typeArguments);
        public abstract Type MakePointerType();

        public override String ToString()
        {
            // FxOverRh port note: Yes, this is actually what the desktop BCL does (including the lack of localization on "Type: ").
            // Of course, we override it in all of our runtime type implementations.
            return "Type: " + Name;
        }

        public override bool Equals(Object o)
        {
            if (o == null)
                return false;

            // Desktop calls an abstract UnderlyingSystemType which doesn't exist on System.Private.CoreLib.
            throw NotImplemented.ByDesign;
        }

        public bool Equals(Type o)
        {
            return Equals((Object)o);
        }

        public override int GetHashCode()
        {
            // Desktop calls an abstract UnderlyingSystemType which doesn't exist on System.Private.CoreLib.
            throw NotImplemented.ByDesign;
        }

        internal bool TryGetEEType(out EETypePtr eeType)
        {
            RuntimeTypeHandle typeHandle = RuntimeAugments.Callbacks.GetTypeHandleIfAvailable(this);
            if (typeHandle.IsNull)
            {
                eeType = default(EETypePtr);
                return false;
            }
            eeType = typeHandle.ToEETypePtr();
            return true;
        }
    }
}
