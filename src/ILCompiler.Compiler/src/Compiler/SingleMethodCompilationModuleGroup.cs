// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using Internal.TypeSystem;

namespace ILCompiler
{
    /// <summary>
    /// A compilation group that only contains a single method. Useful for development purposes when investigating
    /// code generation issues.
    /// </summary>
    public class SingleMethodCompilationModuleGroup : CompilationModuleGroup
    {
        private MethodDesc _method;

        public SingleMethodCompilationModuleGroup(CompilerTypeSystemContext typeSystemContext, MethodDesc method)
            : base(typeSystemContext)
        {
            _method = method;
        }

        public override bool IsSingleFileCompilation
        {
            get
            {
                return false;
            }
        }

        public override bool ContainsMethod(MethodDesc method)
        {
            return method == _method;
        }

        public override bool ContainsType(TypeDesc type)
        {
            return false;
        }

        public override bool ShouldProduceFullType(TypeDesc type)
        {
            return false;
        }

        public override bool ShouldShareAcrossModules(MethodDesc method)
        {
            return true;
        }

        public override bool ShouldShareAcrossModules(TypeDesc type)
        {
            return true;
        }

        public override void AddCompilationRoots(IRootingServiceProvider rootProvider)
        {
            rootProvider.AddCompilationRoot(_method, "Single method mode");
        }
    }
}
