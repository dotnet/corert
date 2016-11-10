// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System.Collections.Generic;

namespace ILCompiler
{
    /// <summary>
    /// Set of compilation roots that is a result of multiple <see cref="CompilationRootProvider"/> instances.
    /// </summary>
    public class CombinedCompilationRootProvider : CompilationRootProvider
    {
        private IEnumerable<CompilationRootProvider> _providers;

        private CombinedCompilationRootProvider(params CompilationRootProvider[] providers)
            : this((IEnumerable<CompilationRootProvider>)providers)
        {
        }

        public CombinedCompilationRootProvider(IEnumerable<CompilationRootProvider> providers)
        {
            _providers = providers;
        }

        internal override void AddCompilationRoots(IRootingServiceProvider rootProvider)
        {
            foreach (var provider in _providers)
                provider.AddCompilationRoots(rootProvider);
        }
    }
}
