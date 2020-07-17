// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using Internal.TypeSystem;
using Internal.TypeSystem.Ecma;

namespace ILCompiler
{
    public class RawMainMethodRootProvider : ICompilationRootProvider
    {
        private EcmaModule _module;

        public RawMainMethodRootProvider(EcmaModule module)
        {
            _module = module;
        }

        public void AddCompilationRoots(IRootingServiceProvider rootProvider)
        {
            MethodDesc mainMethod = _module.EntryPoint;
            if (mainMethod == null)
                throw new Exception("No managed entrypoint defined for executable module");

            rootProvider.AddCompilationRoot(mainMethod, "Managed Main Method");
        }
    }
}
