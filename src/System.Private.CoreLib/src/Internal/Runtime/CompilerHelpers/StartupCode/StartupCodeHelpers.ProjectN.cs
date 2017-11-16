// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Runtime;

using Debug = Internal.Runtime.CompilerHelpers.StartupDebug;

namespace Internal.Runtime.CompilerHelpers
{
    public static partial class StartupCodeHelpers
    {
        /// <summary>
        /// Register a runtime module for subsequent retrieval using GetLoadedModules().
        /// </summary>
        /// <param name="pointerWithinModule">Arbitrary pointer within the module to register</param>
        public static void RegisterModuleFromPointer(IntPtr pointerWithinModule)
        {
            IntPtr startOfModule = RuntimeImports.RhGetOSModuleFromPointer(pointerWithinModule);
            TypeManagerHandle newModuleHandle = new TypeManagerHandle(startOfModule);
            for (int moduleIndex = 0; moduleIndex < s_moduleCount; moduleIndex++)
            {
                if (s_modules[moduleIndex].OsModuleBase == startOfModule)
                {
                    // Module already registered
                    return;
                }
            }

            AddModule(newModuleHandle);
        }
    }
}
