// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Text;
using System.Reflection;
using System.Diagnostics;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Runtime.CompilerServices;
using System.Reflection.Runtime.TypeInfos;
using System.Reflection.Runtime.Assemblies;

using IRuntimeImplementedType = Internal.Reflection.Core.NonPortable.IRuntimeImplementedType;
using Internal.LowLevelLinq;
using Internal.Runtime.Augments;
using Internal.Reflection.Core.Execution;
using Internal.Reflection.Core.NonPortable;

namespace System.Reflection.Runtime.General
{
    internal static class Helpers
    {
        // This helper helps reduce the temptation to write "h == default(RuntimeTypeHandle)" which causes boxing.
        public static bool IsNull(this RuntimeTypeHandle h)
        {
            return h.Equals(default(RuntimeTypeHandle));
        }

        // Clones a Type[] array for the purpose of returning it from an api.
        public static Type[] CloneTypeArray(this Type[] types)
        {
            int count = types.Length;
            if (count == 0)
                return Array.Empty<Type>();  // Ok not to clone empty arrays - those are immutable.

            Type[] clonedTypes = new Type[count];
            for (int i = 0; i < count; i++)
            {
                clonedTypes[i] = types[i];
            }
            return clonedTypes;
        }

        public static bool IsRuntimeImplemented(this Type type)
        {
            return type is IRuntimeImplementedType;
        }

        public static RuntimeTypeInfo[] ToRuntimeTypeInfoArray(this Type[] types)
        {
            int count = types.Length;
            RuntimeTypeInfo[] typeInfos = new RuntimeTypeInfo[count];
            for (int i = 0; i < count; i++)
            {
                typeInfos[i] = types[i].CastToRuntimeTypeInfo();
            }
            return typeInfos;
        }

        public static string LastResortString(this RuntimeTypeHandle typeHandle)
        {
            return ReflectionCoreExecution.ExecutionEnvironment.GetLastResortString(typeHandle);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static RuntimeNamedTypeInfo CastToRuntimeNamedTypeInfo(this Type type)
        {
            Debug.Assert(type is RuntimeNamedTypeInfo);
            return (RuntimeNamedTypeInfo)type;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static RuntimeTypeInfo CastToRuntimeTypeInfo(this Type type)
        {
            Debug.Assert(type is RuntimeTypeInfo);
            return (RuntimeTypeInfo)type;
        }

        public static ReadOnlyCollection<T> ToReadOnlyCollection<T>(this IEnumerable<T> enumeration)
        {
            return new ReadOnlyCollection<T>(enumeration.ToArray());
        }

        public static MethodInfo FilterAccessor(this MethodInfo accessor, bool nonPublic)
        {
            if (nonPublic)
                return accessor;
            if (accessor.IsPublic)
                return accessor;
            return null;
        }

        public static object ToRawValue(this object defaultValueOrLiteral)
        {
            Enum e = defaultValueOrLiteral as Enum;
            if (e != null)
                return RuntimeAugments.GetEnumValue(e);
            return defaultValueOrLiteral;
        }

        public static Type GetTypeCore(this Assembly assembly, string name, bool ignoreCase)
        {
            RuntimeAssembly runtimeAssembly = assembly as RuntimeAssembly;
            if (runtimeAssembly != null)
            {
                // Not a recursion - this one goes to the actual instance method on RuntimeAssembly.
                return runtimeAssembly.GetTypeCore(name, ignoreCase: ignoreCase);
            }
            else
            {
                // This is a third-party Assembly object. We can emulate GetTypeCore() by calling the public GetType()
                // method. This is wasteful because it'll probably reparse a type string that we've already parsed
                // but it can't be helped.
                string escapedName = name.EscapeTypeNameIdentifier();
                return assembly.GetType(escapedName, throwOnError: false, ignoreCase: ignoreCase);
            }
        }

        public static TypeLoadException CreateTypeLoadException(string typeName, Assembly assemblyIfAny)
        {
            if (assemblyIfAny == null)
                throw new TypeLoadException(SR.Format(SR.TypeLoad_TypeNotFound, typeName));
            else
                throw Helpers.CreateTypeLoadException(typeName, assemblyIfAny.FullName);
        }

        public static TypeLoadException CreateTypeLoadException(string typeName, string assemblyName)
        {
            string message = SR.Format(SR.TypeLoad_TypeNotFoundInAssembly, typeName, assemblyName);
            return ReflectionCoreNonPortable.CreateTypeLoadException(message, typeName);
        }

        // Escape identifiers as described in "Specifying Fully Qualified Type Names" on msdn.
        // Current link is http://msdn.microsoft.com/en-us/library/yfsftwz6(v=vs.110).aspx
        public static string EscapeTypeNameIdentifier(this string identifier)
        {
            // Some characters in a type name need to be escaped
            if (identifier != null && identifier.IndexOfAny(s_charsToEscape) != -1)
            {
                StringBuilder sbEscapedName = new StringBuilder(identifier.Length);
                foreach (char c in identifier)
                {
                    if (c.NeedsEscapingInTypeName())
                        sbEscapedName.Append('\\');

                    sbEscapedName.Append(c);
                }
                identifier = sbEscapedName.ToString();
            }
            return identifier;
        }

        public static bool NeedsEscapingInTypeName(this char c)
        {
            return Array.IndexOf(s_charsToEscape, c) >= 0;
        }

        private static readonly char[] s_charsToEscape = new char[] { '\\', '[', ']', '+', '*', '&', ',' };
    }
}
