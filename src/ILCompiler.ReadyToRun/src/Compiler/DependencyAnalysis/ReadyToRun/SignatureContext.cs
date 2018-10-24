// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Collections.Immutable;
using System.Diagnostics;
using System.Reflection.Metadata;
using System.Reflection.Metadata.Ecma335;

using Internal.JitInterface;
using Internal.TypeSystem;
using Internal.TypeSystem.Ecma;

namespace ILCompiler.DependencyAnalysis.ReadyToRun
{
    public class SignatureContext
    {
        /// <summary>
        /// Token resolver is used to translate typesystem objects into tokens relative to 
        /// input modules within the versioning bubble. We must not hardcode tokens relative
        /// to modules outside of version bubble as these can change arbitrarily.
        /// </summary>
        private readonly ModuleTokenResolver _resolver;

        /// <summary>
        /// Default context module for signatures. When encoding a type in a different
        /// assembly (within the same version bubble), we must use module override.
        /// </summary>
        private readonly EcmaModule _contextModule;

        public SignatureContext(ModuleTokenResolver resolver, EcmaModule contextModule)
        {
            _resolver = resolver;
            _contextModule = contextModule;
        }

        public ModuleToken GetModuleTokenForType(EcmaType type, bool throwIfNotFound = true)
        {
            return _resolver.GetModuleTokenForType(type, throwIfNotFound);
        }

        public ModuleToken GetModuleTokenForMethod(MethodDesc method, bool throwIfNotFound = true)
        {
            return _resolver.GetModuleTokenForMethod(method, throwIfNotFound);
        }

        public ModuleToken GetModuleTokenForField(FieldDesc field, bool throwIfNotFound = true)
        {
            return _resolver.GetModuleTokenForField(field, throwIfNotFound);
        }

        public int GetModuleIndex(EcmaModule targetModule)
        {
            if (targetModule == _contextModule)
            {
                return -1;
            }
            return _resolver.GetModuleIndex(targetModule);
        }

        public EcmaModule Module => _contextModule;
    }
}
