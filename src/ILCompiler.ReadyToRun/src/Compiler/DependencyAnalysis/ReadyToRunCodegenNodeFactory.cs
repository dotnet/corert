// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using ILCompiler.DependencyAnalysisFramework;
using Internal.TypeSystem;

namespace ILCompiler.DependencyAnalysis
{
    public sealed class ReadyToRunCodegenNodeFactory : NodeFactory
    {
        public ReadyToRunCodegenNodeFactory(CompilerTypeSystemContext context, CompilationModuleGroup compilationModuleGroup, MetadataManager metadataManager,
            InteropStubManager interopStubManager, NameMangler nameMangler, VTableSliceProvider vtableSliceProvider, DictionaryLayoutProvider dictionaryLayoutProvider)
            : base(context, 
                  compilationModuleGroup, 
                  metadataManager, 
                  interopStubManager, 
                  nameMangler, 
                  new LazyGenericsDisabledPolicy(), 
                  vtableSliceProvider, 
                  dictionaryLayoutProvider, 
                  new ImportedNodeProviderThrowing())
        {
            
        }

        public CoreCLRReadyToRunHeaderNode CoreCLRReadyToRunHeader;

        public CoreCLRReadyToRunRuntimeFunctionsTableNode CoreCLRReadyToRunRuntimeFunctionsTable;

        public CoreCLRReadyToRunEntryPointTableNode CoreCLRReadyToRunMethodEntryPointTable;

        public CoreCLRReadyToRunEntryPointTableNode CoreCLRReadyToRunInstanceEntryPointTable;

        public CoreCLRReadyToRunTypesTableNode CoreCLRReadyToRunTypesTable;

        public CoreCLRReadyToRunImportSectionsTableNode CoreCLRReadyToRunImportSectionsTable;

        protected override IMethodNode CreateMethodEntrypointNode(MethodDesc method)
        {
            return new MethodCodeNode(method);
        }

        protected override ISymbolNode CreateReadyToRunHelperNode(ReadyToRunHelperKey helperCall)
        {
            throw new NotImplementedException();
        }

        protected override IMethodNode CreateUnboxingStubNode(MethodDesc method)
        {
            throw new NotImplementedException();
        }

        public override void AttachToDependencyGraph(DependencyAnalyzerBase<NodeFactory> graph)
        {
            CoreCLRReadyToRunHeader = new CoreCLRReadyToRunHeaderNode(Target);
            graph.AddRoot(CoreCLRReadyToRunHeader, "ReadyToRunHeader is always generated");

            var compilerIdentifierNode = new CompilerIdentifierNode();
            CoreCLRReadyToRunHeader.Add(Internal.Runtime.ReadyToRunSectionType.CompilerIdentifier, compilerIdentifierNode, compilerIdentifierNode);

            CoreCLRReadyToRunRuntimeFunctionsTable = new CoreCLRReadyToRunRuntimeFunctionsTableNode(Target);
            graph.AddRoot(CoreCLRReadyToRunRuntimeFunctionsTable, "ReadyToRunRuntimeFunctionsTable is always generated");
            CoreCLRReadyToRunHeader.Add(
                Internal.Runtime.ReadyToRunSectionType.RuntimeFunctions,
                CoreCLRReadyToRunRuntimeFunctionsTable,
                CoreCLRReadyToRunRuntimeFunctionsTable);

            CoreCLRReadyToRunMethodEntryPointTable = new CoreCLRReadyToRunEntryPointTableNode(Target, instanceEntryPoints: false);
            graph.AddRoot(CoreCLRReadyToRunMethodEntryPointTable, "ReadyToRunMethodEntryPointTable is always generated");
            CoreCLRReadyToRunHeader.Add(
                Internal.Runtime.ReadyToRunSectionType.MethodDefEntryPoints,
                CoreCLRReadyToRunMethodEntryPointTable,
                CoreCLRReadyToRunMethodEntryPointTable);

            CoreCLRReadyToRunInstanceEntryPointTable = new CoreCLRReadyToRunEntryPointTableNode(Target, instanceEntryPoints: true);
            graph.AddRoot(CoreCLRReadyToRunInstanceEntryPointTable, "ReadyToRunInstanceEntryPointTable is always generated");
            CoreCLRReadyToRunHeader.Add(
                Internal.Runtime.ReadyToRunSectionType.InstanceMethodEntryPoints,
                CoreCLRReadyToRunInstanceEntryPointTable,
                CoreCLRReadyToRunInstanceEntryPointTable);

            CoreCLRReadyToRunTypesTable = new CoreCLRReadyToRunTypesTableNode(Target);
            graph.AddRoot(CoreCLRReadyToRunTypesTable, "ReadyToRunTypesTable is always generated");
            CoreCLRReadyToRunHeader.Add(
                Internal.Runtime.ReadyToRunSectionType.AvailableTypes,
                CoreCLRReadyToRunTypesTable,
                CoreCLRReadyToRunTypesTable);

            CoreCLRReadyToRunImportSectionsTable = new CoreCLRReadyToRunImportSectionsTableNode(Target);
            graph.AddRoot(CoreCLRReadyToRunImportSectionsTable, "ReadyToRunImportSectionsTable is always generated");
            CoreCLRReadyToRunHeader.Add(
                Internal.Runtime.ReadyToRunSectionType.ImportSections,
                CoreCLRReadyToRunImportSectionsTable,
                CoreCLRReadyToRunImportSectionsTable);
        }
    }
}
