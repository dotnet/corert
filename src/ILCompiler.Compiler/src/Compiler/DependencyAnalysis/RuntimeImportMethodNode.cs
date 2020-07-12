// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using Internal.TypeSystem;
using Internal.TypeSystem.Ecma;

namespace ILCompiler.DependencyAnalysis
{
    /// <summary>
    /// Represents a method that is imported from the runtime library.
    /// </summary>
    public class RuntimeImportMethodNode : ExternSymbolNode, IMethodNode, IExportableSymbolNode
    {
        private MethodDesc _method;

        public RuntimeImportMethodNode(MethodDesc method)
            : base(((EcmaMethod)method).GetRuntimeImportName())
        {
            _method = method;
        }

        public MethodDesc Method
        {
            get
            {
                return _method;
            }
        }

        public ExportForm GetExportForm(NodeFactory factory)
        {
            // Force non-fake exports for RuntimeImportMethods that have '*' as their module. ('*' means the method is 
            // REALLY a reference to the linked in native code)
            if (((EcmaMethod)_method).GetRuntimeImportDllName() == "*")
            {
                ExportForm exportForm = factory.CompilationModuleGroup.GetExportMethodForm(_method, false);
                if (exportForm == ExportForm.ByName)
                    return ExportForm.None; // Method symbols exported by name are naturally handled by the linker
                return exportForm;
            }

            return ExportForm.None; 
        }

        public override int ClassCode => -1173492615;

        public override int CompareToImpl(ISortableNode other, CompilerComparer comparer)
        {
            return comparer.Compare(_method, ((RuntimeImportMethodNode)other)._method);
        }
    }
}
