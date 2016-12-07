// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Reflection;

using Internal.Runtime.Augments;

namespace Internal.Runtime.CompilerHelpers
{
    /// <summary>
    /// Set of helpers used to implement callsite-specific reflection intrinsics.
    /// </summary>
    internal static class ReflectionHelpers
    {
        // This entry is used to implement Type.GetType()'s ability to detect the calling assembly and use it as
        // a default assembly name.
        public static Type GetType(string typeName, string callingAssemblyName, bool throwOnError, bool ignoreCase)
        {
            return ExtensibleGetType(typeName, callingAssemblyName, null, null, throwOnError: throwOnError, ignoreCase: ignoreCase);
        }

        // This entry is used to implement Type.GetType()'s ability to detect the calling assembly and use it as
        // a default assembly name.
        public static Type ExtensibleGetType(string typeName, string callingAssemblyName, Func<AssemblyName, Assembly> assemblyResolver, Func<Assembly, string, bool, Type> typeResolver, bool throwOnError, bool ignoreCase)
        {
            return RuntimeAugments.Callbacks.GetType(typeName, assemblyResolver, typeResolver, throwOnError, ignoreCase, callingAssemblyName);
        }
    }
}
