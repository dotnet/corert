// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System.Diagnostics;

using Internal.TypeSystem;

namespace ILCompiler
{
    public class SingleFileCompilationModuleGroup : CompilationModuleGroup
    {
        public SingleFileCompilationModuleGroup(TypeSystemContext context)
            : base(context)
        {
        }

        public override bool ContainsType(TypeDesc type)
        {
            return true;
        }

        public override bool ContainsMethodBody(MethodDesc method)
        {
            return true;
        }

        public override bool ContainsMethodDictionary(MethodDesc method)
        {
            Debug.Assert(method.GetCanonMethodTarget(CanonicalFormKind.Specific) != method);
            return ContainsMethodBody(method);
        }

        public override bool ExportsType(TypeDesc type)
        {
            return false;
        }

        public override bool ExportsMethod(MethodDesc method)
        {
            return false;
        }

        public override bool ExportsMethodDictionary(MethodDesc method)
        {
            return false;
        }

        public override bool IsSingleFileCompilation
        {
            get
            {
                return true;
            }
        }

        public override bool ShouldProduceFullVTable(TypeDesc type)
        {
            return false;
        }

        public override bool ShouldPromoteToFullType(TypeDesc type)
        {
            return false;
        }

        public override bool ShouldReferenceThroughImportTable(TypeDesc type)
        {
            return false;
        }

        public override bool CanHaveReferenceThroughImportTable
        {
            get
            {
                return false;
            }
        } 
    }
}
