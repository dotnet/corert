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

        public override bool IsEffectivelySealed(TypeDesc type)
        {
            return _compilationModuleGroup.ContainsType(type) && base.IsEffectivelySealed(type);
        }

        public override bool IsEffectivelySealed(MethodDesc method)
        {
            return _compilationModuleGroup.ContainsMethodBody(method, unboxingStub: false) && base.IsEffectivelySealed(method);
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
