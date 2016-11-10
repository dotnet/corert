// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

namespace ILCompiler
{
    /// <summary>
    /// Provides a set of seeds from which compilation will start.
    /// </summary>
    public abstract class CompilationRootProvider
    {
        internal abstract void AddCompilationRoots(IRootingServiceProvider rootProvider);
    }
}
