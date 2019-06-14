// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;

using Assembly = System.Reflection.Assembly;

namespace Internal.Runtime.CompilerHelpers
{
    public partial class StartupCodeHelpers
    {
        // The CoreRT implementation is what we want to keep long term.
        // ProjectN doesn't have access to a convenient always-reflection-enabled type to use.
        // (We can't use the <Module> type because of IL2IL toolchain limitations.)

#if PROJECTN
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
#else
        private static RuntimeTypeHandle s_entryAssemblyType;

        internal static void InitializeEntryAssembly(RuntimeTypeHandle entryAssemblyType)
        {
            s_entryAssemblyType = entryAssemblyType;
        }

        internal static Assembly GetEntryAssembly()
        {
            return Type.GetTypeFromHandle(s_entryAssemblyType).Assembly;
        }
#endif
    }
}
