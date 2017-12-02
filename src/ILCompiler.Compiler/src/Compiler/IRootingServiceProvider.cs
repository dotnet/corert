﻿// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using Internal.TypeSystem;

namespace ILCompiler
{
    /// <summary>
    /// Provides a means to root types / methods at the compiler driver layer
    /// </summary>
    public interface IRootingServiceProvider
    {
        void AddCompilationRoot(MethodDesc method, string reason, string exportName = null);
        void AddCompilationRoot(TypeDesc type, string reason);
        void RootThreadStaticBaseForType(TypeDesc type, string reason);
        void RootGCStaticBaseForType(TypeDesc type, string reason);
        void RootNonGCStaticBaseForType(TypeDesc type, string reason);
        void RootVirtualMethodForReflection(MethodDesc method, string reason);
        void RootModuleMetadata(ModuleDesc module, string reason);
    }
}
