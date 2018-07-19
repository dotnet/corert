// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Reflection.PortableExecutable;

using ILCompiler.DependencyAnalysisFramework;
using Internal.TypeSystem;

using ILCompiler.DependencyAnalysis.ReadyToRun;

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
            _signatureIndirectionNodes = new NodeCache<Signature, EmbeddedPointerIndirectionNode<Signature>>((Signature signature) =>
            {
                return new RvaEmbeddedPointerIndirectionNode<Signature>(signature);
            });
        }

        public PEReader PEReader;

        public HeaderNode Header;

        public RuntimeFunctionsTableNode RuntimeFunctionsTable;

        public MethodEntryPointTableNode MethodEntryPointTable;

        public InstanceEntryPointTableNode InstanceEntryPointTable;

        public TypesTableNode TypesTable;

        public ImportSectionsTableNode ImportSectionsTable;

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

        private NodeCache<Signature, EmbeddedPointerIndirectionNode<Signature>> _signatureIndirectionNodes;

        public EmbeddedPointerIndirectionNode<Signature> SignatureIndirection(Signature signature)
        {
            return _signatureIndirectionNodes.GetOrAdd(signature);
        }

        public override void AttachToDependencyGraph(DependencyAnalyzerBase<NodeFactory> graph)
        {
            Header = new HeaderNode(Target);
            graph.AddRoot(Header, "ReadyToRunHeader is always generated");

            var compilerIdentifierNode = new CompilerIdentifierNode(Target);
            Header.Add(Internal.Runtime.ReadyToRunSectionType.CompilerIdentifier, compilerIdentifierNode, compilerIdentifierNode);

            RuntimeFunctionsTable = new RuntimeFunctionsTableNode(Target);
            Header.Add(Internal.Runtime.ReadyToRunSectionType.RuntimeFunctions, RuntimeFunctionsTable, RuntimeFunctionsTable);

            MethodEntryPointTable = new MethodEntryPointTableNode(Target);
            Header.Add(Internal.Runtime.ReadyToRunSectionType.MethodDefEntryPoints, MethodEntryPointTable, MethodEntryPointTable);

            InstanceEntryPointTable = new InstanceEntryPointTableNode(Target);
            Header.Add(Internal.Runtime.ReadyToRunSectionType.InstanceMethodEntryPoints, InstanceEntryPointTable, InstanceEntryPointTable);

            TypesTable = new TypesTableNode(Target);
            Header.Add(Internal.Runtime.ReadyToRunSectionType.AvailableTypes, TypesTable, TypesTable);

            ImportSectionsTable = new ImportSectionsTableNode(Target);
            Header.Add(Internal.Runtime.ReadyToRunSectionType.ImportSections, ImportSectionsTable, ImportSectionsTable.StartSymbol);

            ImportSectionNode eagerImports = new ImportSectionNode(CorCompileImportType.CORCOMPILE_IMPORT_TYPE_UNKNOWN, CorCompileImportFlags.CORCOMPILE_IMPORT_FLAGS_EAGER, (byte)Target.PointerSize);
            ImportSectionsTable.AddEmbeddedObject(eagerImports);

            // All ready-to-run images have a module import helper which gets patched by the runtime on image load
            var moduleImport = new ModuleImport();
            eagerImports.AddImport(this, moduleImport);
            graph.AddRoot(moduleImport, "Module import is always generated");
        }
    }
}
