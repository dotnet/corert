// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using Internal.TypeSystem;

namespace ILCompiler.DependencyAnalysis.ReadyToRun
{
    public class DevirtualizationManager : ILCompiler.DevirtualizationManager
    {
        private readonly CompilationModuleGroup _compilationModuleGroup;

        public DevirtualizationManager(CompilationModuleGroup compilationModuleGroup)
        {
            _compilationModuleGroup = compilationModuleGroup;
        }

        protected override MethodDesc ResolveVirtualMethod(MethodDesc declMethod, DefType implType)
        {
            if (_compilationModuleGroup.ContainsMethodBody(declMethod, unboxingStub: false) &&
                _compilationModuleGroup.ContainsType(implType))
            {
                return base.ResolveVirtualMethod(declMethod, implType);
            }

            // Cannot devirtualize across version bubble boundary
            return null;
        }
    }
}
