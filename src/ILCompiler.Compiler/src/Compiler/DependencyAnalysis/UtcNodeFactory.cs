// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;

using ILCompiler.DependencyAnalysis;
using ILCompiler.DependencyAnalysisFramework;
using Internal.Runtime;
using Internal.TypeSystem;

namespace ILCompiler
{
    public class UtcNodeFactory : NodeFactory
    {
        public static string CompilationUnitPrefix = "";
        public string targetPrefix;
        private bool buildMRT;

        private static byte[] ReadBytesFromFile(string filename)
        {
            using (FileStream file = new FileStream(filename, FileMode.Open, FileAccess.Read, FileShare.Read))
            {
                int fileLen = checked((int)file.Length);
                int fileLenRemaining = fileLen;
                int curPos = 0;
                byte[] returnValue = new byte[fileLen];
                while (fileLenRemaining > 0)
                {
                    // Read may return anything from 0 to 10.
                    int n = file.Read(returnValue, curPos, fileLenRemaining);

                    // Unexpected end of file
                    if (n == 0)
                        throw new IOException();

                    curPos += n;
                    fileLenRemaining -= n;
                }

                return returnValue;
            }
        }

        private static ModuleDesc FindMetadataDescribingModuleInInputSet(IEnumerable<ModuleDesc> inputModules)
        {
            foreach (ModuleDesc module in inputModules)
            {
                if (PrecomputedMetadataManager.ModuleHasMetadataMappings(module))
                {
                    return module;
                }
            }

            return null;
        }

        private static MetadataManager PickMetadataManager(CompilerTypeSystemContext context, CompilationModuleGroup compilationModuleGroup, IEnumerable<ModuleDesc> inputModules, string metadataFile)
        {
            if (metadataFile == null)
            {
                return new EmptyMetadataManager(compilationModuleGroup, context);
            }
            else
            {
                return new PrecomputedMetadataManager(compilationModuleGroup, context, FindMetadataDescribingModuleInInputSet(inputModules), inputModules, ReadBytesFromFile(metadataFile));
            }
        }

        private static InteropStubManager NewEmptyInteropStubManager(CompilerTypeSystemContext context, CompilationModuleGroup compilationModuleGroup)
        {
            // On Project N, the compiler doesn't generate the interop code on the fly
            return new EmptyInteropStubManager(compilationModuleGroup, context, null);
        }

        public UtcNodeFactory(CompilerTypeSystemContext context, CompilationModuleGroup compilationModuleGroup, IEnumerable<ModuleDesc> inputModules, string metadataFile, string outputFile, UTCNameMangler nameMangler, bool buildMRT, DictionaryLayoutProvider dictionaryLayoutProvider) 
            : base(context, compilationModuleGroup, PickMetadataManager(context, compilationModuleGroup, inputModules, metadataFile), NewEmptyInteropStubManager(context, compilationModuleGroup), nameMangler, new AttributeDrivenLazyGenericsPolicy(), null, dictionaryLayoutProvider)
        {
            CreateHostedNodeCaches();
            CompilationUnitPrefix = nameMangler.CompilationUnitPrefix;
            ThreadStaticsIndex = new ThreadStaticsIndexNode(nameMangler.GetCurrentModuleTlsIndexPrefix());
            targetPrefix = context.Target.Architecture == TargetArchitecture.X86 ? "_" : "";
            TLSDirectory = new ThreadStaticsDirectoryNode(targetPrefix);
            TlsStart = new ExternSymbolNode(targetPrefix + "_tls_start");
            TlsEnd = new ExternSymbolNode(targetPrefix + "_tls_end");
            LoopHijackFlag = new LoopHijackFlagNode();
            this.buildMRT = buildMRT;
        }

        private void CreateHostedNodeCaches()
        {
            _GCStaticDescs = new NodeCache<MetadataType, GCStaticDescNode>((MetadataType type) =>
            {
                return new GCStaticDescNode(type, false);
            });

            _threadStaticGCStaticDescs = new NodeCache<MetadataType, GCStaticDescNode>((MetadataType type) =>
            {
                return new GCStaticDescNode(type, true);
            });

            _threadStaticsOffset = new NodeCache<MetadataType, ISymbolNode>((MetadataType type) =>
            {
                if (CompilationModuleGroup.ContainsType(type))
                {
                    return new ThreadStaticsOffsetNode(type, this);
                }
                else if (CompilationModuleGroup.ShouldReferenceThroughImportTable(type))
                {
                    return new ImportedThreadStaticsOffsetNode(type, this);
                }
                else
                {
                    return new ExternSymbolNode(ThreadStaticsOffsetNode.GetMangledName(NameMangler, type));
                }
            });

            _importedThreadStaticsIndices = new NodeCache<MetadataType, ImportedThreadStaticsIndexNode>((MetadataType type) =>
            {
                return new ImportedThreadStaticsIndexNode(this);
            });

            _nonExternMethodSymbols = new NodeCache<MethodKey, NonExternMethodSymbolNode>((MethodKey method) =>
            {
                return new NonExternMethodSymbolNode(this, method.Method, method.IsUnboxingStub);
            });

            _standaloneGCStaticDescs = new NodeCache<GCStaticDescNode, StandaloneGCStaticDescRegionNode>((GCStaticDescNode staticDesc) =>
            {
                return new StandaloneGCStaticDescRegionNode(staticDesc);
            });
        }

        public override void AttachToDependencyGraph(DependencyAnalyzerBase<NodeFactory> graph)
        {
            ReadyToRunHeader = new ReadyToRunHeaderNode(Target);

            graph.AddRoot(ReadyToRunHeader, "ReadyToRunHeader is always generated");
            graph.AddRoot(new ModulesSectionNode(Target), "ModulesSection is always generated");

            graph.AddRoot(EagerCctorTable, "EagerCctorTable is always generated");
            graph.AddRoot(DispatchMapTable, "DispatchMapTable is always generated");
            graph.AddRoot(FrozenSegmentRegion, "FrozenSegmentRegion is always generated");
            graph.AddRoot(TypeManagerIndirection, "ModuleManagerIndirection is always generated");
            graph.AddRoot(GCStaticsRegion, "GC StaticsRegion is always generated");
            graph.AddRoot(GCStaticDescRegion, "GC Static Desc is always generated");
            graph.AddRoot(ThreadStaticsOffsetRegion, "Thread Statics Offset Region is always generated");
            graph.AddRoot(ThreadStaticGCDescRegion, "Thread Statics GC Desc Region is always generated");
            if (Target.IsWindows)
            {
                // We need 2 delimiter symbols to bound the unboxing stubs region on Windows platforms (these symbols are
                // accessed using extern "C" variables in the bootstrapper)
                // On non-Windows platforms, the linker emits special symbols with special names at the begining/end of a section
                // so we do not need to emit them ourselves.
                graph.AddRoot(new WindowsUnboxingStubsRegionNode(false), "UnboxingStubsRegion delimiter for Windows platform");
                graph.AddRoot(new WindowsUnboxingStubsRegionNode(true), "UnboxingStubsRegion delimiter for Windows platform");
            }

            // The native part of the MRT library links against CRT which defines _tls_index and _tls_used.
            if (!buildMRT)
            {
                graph.AddRoot(ThreadStaticsIndex, "Thread statics index is always generated");
                graph.AddRoot(TLSDirectory, "TLS Directory is always generated");
            }

            ReadyToRunHeader.Add(ReadyToRunSectionType.EagerCctor, EagerCctorTable, EagerCctorTable.StartSymbol, EagerCctorTable.EndSymbol);
            ReadyToRunHeader.Add(ReadyToRunSectionType.InterfaceDispatchTable, DispatchMapTable, DispatchMapTable.StartSymbol);
            ReadyToRunHeader.Add(ReadyToRunSectionType.FrozenObjectRegion, FrozenSegmentRegion, FrozenSegmentRegion.StartSymbol, FrozenSegmentRegion.EndSymbol);
            ReadyToRunHeader.Add(ReadyToRunSectionType.TypeManagerIndirection, TypeManagerIndirection, TypeManagerIndirection);
            ReadyToRunHeader.Add(ReadyToRunSectionType.GCStaticRegion, GCStaticsRegion, GCStaticsRegion.StartSymbol, GCStaticsRegion.EndSymbol);
            ReadyToRunHeader.Add(ReadyToRunSectionType.GCStaticDesc, GCStaticDescRegion, GCStaticDescRegion.StartSymbol, GCStaticDescRegion.EndSymbol);
            ReadyToRunHeader.Add(ReadyToRunSectionType.ThreadStaticOffsetRegion, ThreadStaticsOffsetRegion, ThreadStaticsOffsetRegion.StartSymbol, ThreadStaticsOffsetRegion.EndSymbol);
            ReadyToRunHeader.Add(ReadyToRunSectionType.ThreadStaticGCDescRegion, ThreadStaticGCDescRegion, ThreadStaticGCDescRegion.StartSymbol, ThreadStaticGCDescRegion.EndSymbol);
            ReadyToRunHeader.Add(ReadyToRunSectionType.LoopHijackFlag, LoopHijackFlag, LoopHijackFlag);

            if (!buildMRT)
            {
                ReadyToRunHeader.Add(ReadyToRunSectionType.ThreadStaticIndex, ThreadStaticsIndex, ThreadStaticsIndex);
            }

            var commonFixupsTableNode = new ExternalReferencesTableNode("CommonFixupsTable", this);
            InteropStubManager.AddToReadyToRunHeader(ReadyToRunHeader, this, commonFixupsTableNode);
            MetadataManager.AddToReadyToRunHeader(ReadyToRunHeader, this, commonFixupsTableNode);
            MetadataManager.AttachToDependencyGraph(graph);
        }

        protected override IMethodNode CreateMethodEntrypointNode(MethodDesc method)
        {
            if (method.HasCustomAttribute("System.Runtime", "RuntimeImportAttribute"))
            {
                return new RuntimeImportMethodNode(method);
            }

            if (CompilationModuleGroup.ContainsMethodBody(method))
            {
                return NonExternMethodSymbol(method, false);
            }

            return new ExternMethodSymbolNode(this, method);
        }

        protected override IMethodNode CreateUnboxingStubNode(MethodDesc method)
        {
            if (method.IsCanonicalMethod(CanonicalFormKind.Any) && !method.HasInstantiation)
            {
                // Unboxing stubs to canonical instance methods need a special unboxing instantiating stub that unboxes
                // 'this' and also provides an instantiation argument (we do a calling convention conversion).
                // The unboxing instantiating stub is emitted by UTC.
                if (CompilationModuleGroup.ContainsMethodBody(method))
                {
                    return NonExternMethodSymbol(method, true);
                }

                return new ExternMethodSymbolNode(this, method, true);
            }
            else
            {
                // Otherwise we just unbox 'this' and don't touch anything else.
                return new UnboxingStubNode(method, Target);
            }
        }

        protected override ISymbolNode CreateReadyToRunHelperNode(ReadyToRunHelperKey helperCall)
        {
            return new ReadyToRunHelperNode(this, helperCall.HelperId, helperCall.Target);
        }

        public GCStaticDescRegionNode GCStaticDescRegion = new GCStaticDescRegionNode(
            CompilationUnitPrefix + "__GCStaticDescStart", 
            CompilationUnitPrefix + "__GCStaticDescEnd");

        public GCStaticDescRegionNode ThreadStaticGCDescRegion = new GCStaticDescRegionNode(
            CompilationUnitPrefix + "__ThreadStaticGCDescStart", 
            CompilationUnitPrefix + "__ThreadStaticGCDescEnd");

        public ArrayOfEmbeddedDataNode ThreadStaticsOffsetRegion = new ArrayOfEmbeddedDataNode(
            CompilationUnitPrefix + "__ThreadStaticOffsetRegionStart",
            CompilationUnitPrefix + "__ThreadStaticOffsetRegionEnd",
            null);

        public ThreadStaticsIndexNode ThreadStaticsIndex;

        public ThreadStaticsDirectoryNode TLSDirectory;

        // These two are defined in startup code to mark start and end of the entire Thread Local Storage area,
        // including the TLS data from different managed and native object files.
        public ExternSymbolNode TlsStart;
        public ExternSymbolNode TlsEnd;

        public LoopHijackFlagNode LoopHijackFlag;

        protected override ISymbolDefinitionNode CreateThreadStaticsNode(MetadataType type)
        {
            return new UtcThreadStaticsNode(type);
        }

        private NodeCache<MetadataType, GCStaticDescNode> _GCStaticDescs;

        public ISymbolNode TypeGCStaticDescSymbol(MetadataType type)
        {
            if (CompilationModuleGroup.ContainsType(type))
            {
                return _GCStaticDescs.GetOrAdd(type);
            }
            else
            {
                return ExternSymbol(GCStaticDescNode.GetMangledName(NameMangler, type, false));
            }
        }

        private NodeCache<MetadataType, GCStaticDescNode> _threadStaticGCStaticDescs;

        public ISymbolNode TypeThreadStaticGCDescNode(MetadataType type)
        {
            if (CompilationModuleGroup.ContainsType(type))
            {
                return _threadStaticGCStaticDescs.GetOrAdd(type);
            }
            else
            {
                return ExternSymbol(GCStaticDescNode.GetMangledName(NameMangler, type, true));
            }
        }

        private NodeCache<MetadataType, ISymbolNode> _threadStaticsOffset;

        public ISymbolNode TypeThreadStaticsOffsetSymbol(MetadataType type)
        {
            return _threadStaticsOffset.GetOrAdd(type);            
        }

        private NodeCache<MetadataType, ImportedThreadStaticsIndexNode> _importedThreadStaticsIndices;

        public ISymbolNode TypeThreadStaticsIndexSymbol(TypeDesc type)
        {
            if (CompilationModuleGroup.ContainsType(type))
            {
                return ThreadStaticsIndex;
            }
            else if (CompilationModuleGroup.ShouldReferenceThroughImportTable(type))
            {
                return _importedThreadStaticsIndices.GetOrAdd((MetadataType)type);
            }
            else
            {
                return ExternSymbol(ThreadStaticsIndexNode.GetMangledName((NameMangler as UTCNameMangler).GetImportedTlsIndexPrefix()));
            }
        }

        private NodeCache<MethodKey, NonExternMethodSymbolNode> _nonExternMethodSymbols;

        public NonExternMethodSymbolNode NonExternMethodSymbol(MethodDesc method, bool isUnboxingStub)
        {
            return _nonExternMethodSymbols.GetOrAdd(new MethodKey(method, isUnboxingStub));
        }

        private NodeCache<GCStaticDescNode, StandaloneGCStaticDescRegionNode> _standaloneGCStaticDescs;

        public StandaloneGCStaticDescRegionNode StandaloneGCStaticDescRegion(GCStaticDescNode staticDesc)
        {
            return _standaloneGCStaticDescs.GetOrAdd(staticDesc);
        }

        public class UtcDictionaryLayoutProvider : DictionaryLayoutProvider
        {
            public override DictionaryLayoutNode GetLayout(TypeSystemEntity methodOrType)
            {
                return new UtcDictionaryLayoutNode(methodOrType);
            }
        }

        public ISymbolNode LoopHijackFlagSymbol()
        {
            return LoopHijackFlag;
        }
    }
}
