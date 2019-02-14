// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System.Threading;
using System.Reflection;
using System.Runtime.CompilerServices;

using Internal.Runtime.Augments;
using Internal.Reflection.Augments;
using Internal.Reflection.Core.NonPortable;

namespace System
{
    public abstract partial class Type : MemberInfo, IReflect
    {
        public bool IsInterface => (GetAttributeFlagsImpl() & TypeAttributes.ClassSemanticsMask) == TypeAttributes.Interface;

        [Intrinsic]
        public static Type GetTypeFromHandle(RuntimeTypeHandle handle) => RuntimeTypeUnifier.GetTypeForRuntimeTypeHandle(handle);

        [Intrinsic]
        public static Type GetType(string typeName) => GetType(typeName, throwOnError: false, ignoreCase: false);
        [Intrinsic]
        public static Type GetType(string typeName, bool throwOnError) => GetType(typeName, throwOnError: throwOnError, ignoreCase: false);
        [Intrinsic]
        public static Type GetType(string typeName, bool throwOnError, bool ignoreCase) => GetType(typeName, null, null, throwOnError: throwOnError, ignoreCase: ignoreCase);

        [Intrinsic]
        public static Type GetType(string typeName, Func<AssemblyName, Assembly> assemblyResolver, Func<Assembly, string, bool, Type> typeResolver) => GetType(typeName, assemblyResolver, typeResolver, throwOnError: false, ignoreCase: false);
        [Intrinsic]
        public static Type GetType(string typeName, Func<AssemblyName, Assembly> assemblyResolver, Func<Assembly, string, bool, Type> typeResolver, bool throwOnError) => GetType(typeName, assemblyResolver, typeResolver, throwOnError: throwOnError, ignoreCase: false);
        [Intrinsic]
        public static Type GetType(string typeName, Func<AssemblyName, Assembly> assemblyResolver, Func<Assembly, string, bool, Type> typeResolver, bool throwOnError, bool ignoreCase) => RuntimeAugments.Callbacks.GetType(typeName, assemblyResolver, typeResolver, throwOnError: throwOnError, ignoreCase: ignoreCase, defaultAssembly: null);

        public static Type GetTypeFromCLSID(Guid clsid, string server, bool throwOnError) => ReflectionAugments.ReflectionCoreCallbacks.GetTypeFromCLSID(clsid, server, throwOnError);

        public static Type GetTypeFromProgID(string progID, string server, bool throwOnError)
        {
            if (progID == null)
                throw new ArgumentNullException(nameof(progID));

            Guid clsid;
            Exception exception = GetCLSIDFromProgID(progID, out clsid);
            if (exception != null)
            {
                if (throwOnError)
                    throw exception;
                return null;
            }
            return Type.GetTypeFromCLSID(clsid, server, throwOnError);
        }

        [Intrinsic]
        public static bool operator ==(Type left, Type right)
        {
            if (object.ReferenceEquals(left, right))
                return true;

            if ((object)left == null || (object)right == null)
                return false;

            return left.Equals(right);
        }

        [Intrinsic]
        public static bool operator !=(Type left, Type right) => !(left == right);

        public bool IsRuntimeImplemented() => this is IRuntimeImplemented; // Not an api but needs to be public because of Reflection.Core/CoreLib divide.
    }
}

