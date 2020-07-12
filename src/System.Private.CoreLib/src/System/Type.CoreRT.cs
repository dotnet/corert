// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.Threading;
using System.Reflection;
using System.Runtime.CompilerServices;

using Internal.Runtime.Augments;
using Internal.Runtime.CompilerServices;
using Internal.Reflection.Augments;
using Internal.Reflection.Core.NonPortable;
using System.Runtime.InteropServices;

namespace System
{
    public abstract partial class Type : MemberInfo, IReflect
    {
        public bool IsInterface => (GetAttributeFlagsImpl() & TypeAttributes.ClassSemanticsMask) == TypeAttributes.Interface;

        [Intrinsic]
        public static Type GetTypeFromHandle(RuntimeTypeHandle handle) => handle.IsNull ? null : GetTypeFromEETypePtr(handle.ToEETypePtr());

        internal static Type GetTypeFromEETypePtr(EETypePtr eeType)
        {
            // If we support the writable data section on EETypes, the runtime type associated with the EEType
            // is cached there. If writable data is not supported, we need to do a lookup in the runtime type
            // unifier's hash table.
            if (Internal.Runtime.EEType.SupportsWritableData)
            {
                ref GCHandle handle = ref eeType.GetWritableData<GCHandle>();
                if (handle.IsAllocated)
                {
                    return Unsafe.As<Type>(handle.Target);
                }
                else
                {
                    return GetTypeFromEETypePtrSlow(eeType, ref handle);
                }
            }
            else
            {
                return RuntimeTypeUnifier.GetRuntimeTypeForEEType(eeType);
            }
        }

        [MethodImpl(MethodImplOptions.NoInlining)]
        private static Type GetTypeFromEETypePtrSlow(EETypePtr eeType, ref GCHandle handle)
        {
            // Note: this is bypassing the "fast" unifier cache (based on a simple IntPtr
            // identity of EEType pointers). There is another unifier behind that cache
            // that ensures this code is race-free.
            Type result = RuntimeTypeUnifier.GetRuntimeTypeBypassCache(eeType);
            GCHandle tempHandle = GCHandle.Alloc(result);

            // We don't want to leak a handle if there's a race
            if (Interlocked.CompareExchange(ref Unsafe.As<GCHandle, IntPtr>(ref handle), (IntPtr)tempHandle, default) != default)
            {
                tempHandle.Free();
            }

            return result;
        }

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

            // CLR-compat: runtime types are never equal to non-runtime types
            // If `left` is a non-runtime type with a weird Equals implementation
            // this is where operator `==` would differ from `Equals` call.
            if (left.IsRuntimeImplemented() || right.IsRuntimeImplemented())
                return false;

            return left.Equals(right);
        }

        [Intrinsic]
        public static bool operator !=(Type left, Type right) => !(left == right);

        public bool IsRuntimeImplemented() => this is IRuntimeImplemented; // Not an api but needs to be public because of Reflection.Core/CoreLib divide.
    }
}

