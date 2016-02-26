// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using Internal.TypeSystem;
using Internal.TypeSystem.Ecma;

namespace ILCompiler
{
    /// <summary>
    /// Provides a means to root types / methods at the compiler driver layer
    /// </summary>
    public interface ICompilationRootProvider
    {
        void AddMethodCompilationRoot(MethodDesc method, string reason, string exportName = null);
        void AddTypeCompilationRoot(TypeDesc type, string reason);
        void AddMainMethodCompilationRoot(EcmaModule module);
    }
}
