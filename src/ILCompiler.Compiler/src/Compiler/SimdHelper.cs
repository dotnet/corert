// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System.Collections.Generic;

using Internal.TypeSystem;

using AssemblyName = System.Reflection.AssemblyName;

namespace ILCompiler
{
    /// <summary>
    /// Helper type that deals with System.Numerics.Vectors intrinsics.
    /// </summary>
    public struct SimdHelper
    {
        private ModuleDesc[] _simdModulesCached;

        public bool IsInSimdModule(TypeDesc type)
        {
            if (type is MetadataType)
            {
                if (_simdModulesCached == null)
                {
                    InitializeSimdModules(type);
                }

                ModuleDesc typeModule = ((MetadataType)type).Module;
                foreach (ModuleDesc simdModule in _simdModulesCached)
                    if (typeModule == simdModule)
                        return true;
            }

            return false;
        }

        private void InitializeSimdModules(TypeDesc type)
        {
            TypeSystemContext context = type.Context;

            ArrayBuilder<ModuleDesc> simdModules = new ArrayBuilder<ModuleDesc>();

            ModuleDesc module = context.ResolveAssembly(new AssemblyName("System.Numerics"), false);
            if (module != null)
                simdModules.Add(module);

            module = context.ResolveAssembly(new AssemblyName("System.Numerics.Vectors"), false);
            if (module != null)
                simdModules.Add(module);

            _simdModulesCached = simdModules.ToArray();
        }

        public bool IsVectorOfT(TypeDesc type)
        {
            return IsInSimdModule(type)
                && ((MetadataType)type).Name == "Vector`1"
                && ((MetadataType)type).Namespace == "System.Numerics";
        }
    }
}
