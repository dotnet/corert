// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.Diagnostics;

using Internal.TypeSystem;

namespace ILCompiler
{
    public class SingleFileCompilationModuleGroup : CompilationModuleGroup
    {
        public override bool ContainsType(TypeDesc type)
        {
            return true;
        }

        public override bool ContainsTypeDictionary(TypeDesc type)
        {
            return true;
        }

        public override bool ContainsMethodBody(MethodDesc method, bool unboxingStub)
        {
            return true;
        }

        public override bool ContainsMethodDictionary(MethodDesc method)
        {
            Debug.Assert(method.GetCanonMethodTarget(CanonicalFormKind.Specific) != method);
            return ContainsMethodBody(method, false);
        }

        public override bool ImportsMethod(MethodDesc method, bool unboxingStub)
        {
            return false;
        }

        public override ExportForm GetExportTypeForm(TypeDesc type)
        {
            return ExportForm.None;
        }

        public override ExportForm GetExportTypeFormDictionary(TypeDesc type)
        {
            return ExportForm.None;
        }

        public override ExportForm GetExportMethodForm(MethodDesc method, bool unboxingStub)
        {
            return ExportForm.None;
        }

        public override ExportForm GetExportMethodDictionaryForm(MethodDesc method)
        {
            return ExportForm.None;
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

        public override bool PresenceOfEETypeImpliesAllMethodsOnType(TypeDesc type)
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
