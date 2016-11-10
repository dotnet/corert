// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;

namespace ILCompiler
{
    /// <summary>
    /// Provides a set of seeds from which compilation will start.
    /// </summary>
    public abstract class CompilationRootProvider
    {
        /// <summary>
        /// Symbolic name under which the managed entrypoint is exported.
        /// </summary>
        public const string ManagedEntryPointMethodName = "__managed__Main";

        public static readonly CompilationRootProvider Empty = new EmptyCompilationRootProvider();

        internal abstract void AddCompilationRoots(IRootingServiceProvider rootProvider);

        private class EmptyCompilationRootProvider : CompilationRootProvider
        {
            internal override void AddCompilationRoots(IRootingServiceProvider rootProvider)
            {
            }
        }
    }
}
