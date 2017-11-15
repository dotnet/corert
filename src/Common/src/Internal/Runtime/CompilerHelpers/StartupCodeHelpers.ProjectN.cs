// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Collections.Generic;
using System.Runtime;
using System.Runtime.InteropServices;

using Debug = Internal.Runtime.CompilerHelpers.StartupDebug;

namespace Internal.Runtime.CompilerHelpers
{
    [McgIntrinsics]
    public static partial class StartupCodeHelpers
    {
        /// <summary>
        /// Initial module array allocation used when adding modules dynamically.
        /// </summary>
        private const int InitialModuleCount = 8;
        
        /// <summary>
        /// Table of logical modules. Only the first _moduleCount elements of the array are in use.
        /// </summary>
        private static TypeManagerHandle[] _modules;

        /// <summary>
        /// Number of valid elements in the logical module table.
        /// </summary>
        private static int _moduleCount;

        /// <summary>
        /// Register a runtime module for subsequent retrieval using GetRegisteredModules().
        /// </summary>
        /// <param name="pointerWithinModule">Arbitrary pointer within the module to register</param>
        public static void RegisterModuleFromPointer(IntPtr pointerWithinModule)
        {
            IntPtr startOfModule = RuntimeImports.RhGetOSModuleFromPointer(pointerWithinModule);
            TypeManagerHandle newModuleHandle = new TypeManagerHandle(startOfModule);
            for (int moduleIndex = 0; moduleIndex < _moduleCount; moduleIndex++)
            {
                if (_modules[moduleIndex].Equals(newModuleHandle))
                {
                    // Module already registered
                    return;
                }
            }

            if (_modules == null || _moduleCount >= _modules.Length)
            {
                // Reallocate logical module array
                int newModuleLength = 2 * _moduleCount;
                if (newModuleLength < InitialModuleCount)
                {
                    newModuleLength = InitialModuleCount;
                }

                TypeManagerHandle[] newModules = new TypeManagerHandle[newModuleLength];
                for (int copyIndex = 0; copyIndex < _moduleCount; copyIndex++)
                {
                    newModules[copyIndex] = _modules[copyIndex];
                }
                _modules = newModules;
            }
            
            _modules[_moduleCount] = newModuleHandle;
            _moduleCount++;
        }

        /// <summary>
        /// Return the number of registered logical modules; optionally copy them into an array.
        /// </summary>
        /// <param name="outputModules">Array to copy logical modules to, null = only return logical module count</param>
        internal static int GetLoadedModules(TypeManagerHandle[] outputModules)
        {
            if (outputModules != null)
            {
                int copyLimit = (_moduleCount < outputModules.Length ? _moduleCount : outputModules.Length);
                for (int copyIndex = 0; copyIndex < copyLimit; copyIndex++)
                {
                    outputModules[copyIndex] = _modules[copyIndex];
                }
            }
            return _moduleCount;
        }
    }
}
