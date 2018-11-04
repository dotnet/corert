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
        private readonly ModuleTokenResolver _resolver;

        public SignatureContext(ModuleTokenResolver resolver)
        {
            _resolver = resolver;
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
    }
}
