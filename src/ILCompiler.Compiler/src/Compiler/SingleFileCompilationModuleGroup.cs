// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using Internal.TypeSystem;

namespace ILCompiler
{
    public class SingleFileCompilationModuleGroup : CompilationModuleGroup
    {
        public override bool ContainsType(TypeDesc type)
        {
            return true;
        }

        public override bool ContainsMethod(MethodDesc method)
        {
            return true;
        }

        public override bool IsSingleFileCompilation
        {
            get
            {
                return true;
            }
        }

        public override bool ShouldProduceFullType(TypeDesc type)
        {
            return false;
        }

        public override bool ShouldReferenceThroughImportTable(TypeDesc type)
        {
            return false;
        }
    }
}
