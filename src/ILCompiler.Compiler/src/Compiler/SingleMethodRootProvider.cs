// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using Internal.TypeSystem;

namespace ILCompiler
{
    /// <summary>
    /// Compilation root that is a single method.
    /// </summary>
    public class SingleMethodRootProvider : CompilationRootProvider
    {
        private MethodDesc _method;

        public SingleMethodRootProvider(MethodDesc method)
        {
            _method = method;
        }

        internal override void AddCompilationRoots(IRootingServiceProvider rootProvider)
        {
            rootProvider.AddCompilationRoot(_method, "Single method root");
        }
    }
}
