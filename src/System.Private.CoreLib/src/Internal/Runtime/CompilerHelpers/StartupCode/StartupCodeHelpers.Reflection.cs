// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;

using Assembly = System.Reflection.Assembly;

namespace Internal.Runtime.CompilerHelpers
{
    public partial class StartupCodeHelpers
    {
        // TODO: to support AssemblyLoadContext, we'll want to change this to accept a RuntimeTypeHandle
        // of the type that owns the entrypoint method, but this is it's own rat's nest. (Reflection analysis needs to
        // ensure the type is reflectable; the type might also be a <Module> type that is not reflectable with
        // our current policies, etc.)
        private static string s_entryAssemblyName;

        // The only reason why this is public is because the Project N IL2IL toolchain will remove this method
        // before it gets referenced otherwise. It's never okay to call this for anyone but the compiler.
        public static void InitializeEntryAssembly(string assemblyName)
        {
            s_entryAssemblyName = assemblyName;
        }

        internal static Assembly GetEntryAssembly()
        {
            if (s_entryAssemblyName != null)
            {
                // If the assembly wasn't reflection enabled, user will get a FileNotFoundException, but
                // that's probably as good as anything. Null would be an even more wrong answer.
                return Assembly.Load(s_entryAssemblyName);
            }
            return null;
        }
    }
}
