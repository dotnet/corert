// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using System.Threading.Tasks;

using ILCompiler.DependencyAnalysis;
using ILCompiler.DependencyAnalysisFramework;
using Internal.Runtime;
using Internal.TypeSystem;
using Internal.TypeSystem.Ecma;

namespace ILCompiler
{
    public class UtcNodeFactory : NodeFactory
    {
        public static string CompilationUnitPrefix = "";
        public string targetPrefix;

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

        public UtcNodeFactory(CompilerTypeSystemContext context, CompilationModuleGroup compilationModuleGroup, IEnumerable<ModuleDesc> inputModules, string metadataFile, string outputFile) 
            : base(context, compilationModuleGroup, PickMetadataManager(context, compilationModuleGroup, inputModules, metadataFile))
        {
            CreateHostedNodeCaches();
            CompilationUnitPrefix = Path.GetFileNameWithoutExtension(outputFile);
            ThreadStaticsIndex = new ThreadStaticsIndexNode(CompilationUnitPrefix);
            ThreadStaticsStartOffset = new ThreadStaticsStartNode();
            targetPrefix = context.Target.Architecture == TargetArchitecture.X86 ? "_" : "";
            TLSDirectory = new ThreadStaticsDirectoryNode(targetPrefix);
            TlsStart = new ExternSymbolNode(targetPrefix + "_tls_start");
            TlsEnd = new ExternSymbolNode(targetPrefix + "_tls_end");
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

            _threadStaticsOffset = new NodeCache<MetadataType, ThreadStaticsOffsetNode>((MetadataType type) =>
            {
                return new ThreadStaticsOffsetNode(type, this);
            });

            _hostedGenericDictionaryLayouts = new NodeCache<TypeSystemEntity, UtcDictionaryLayoutNode>((TypeSystemEntity methodOrType) =>
            {
                return new UtcDictionaryLayoutNode(methodOrType);
            });

            _nonExternMethodSymbols = new NodeCache<MethodDesc, NonExternMethodSymbolNode>((MethodDesc method) =>
            {
                return new NonExternMethodSymbolNode(method);
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
            graph.AddRoot(ThreadStaticsRegion, "Thread Statics Region is always generated");
            graph.AddRoot(ThreadStaticsOffsetRegion, "Thread Statics Offset Region is always generated");
            graph.AddRoot(ThreadStaticGCDescRegion, "Thread Statics GC Desc Region is always generated");
            graph.AddRoot(ThreadStaticsIndex, "Thread statics index is always generated");
            graph.AddRoot(ThreadStaticsStartOffset, "Thread statics start offset is always generated");
            graph.AddRoot(TLSDirectory, "TLS Directory is always generated");
            
            ReadyToRunHeader.Add(ReadyToRunSectionType.EagerCctor, EagerCctorTable, EagerCctorTable.StartSymbol, EagerCctorTable.EndSymbol);
            ReadyToRunHeader.Add(ReadyToRunSectionType.InterfaceDispatchTable, DispatchMapTable, DispatchMapTable.StartSymbol);
            ReadyToRunHeader.Add(ReadyToRunSectionType.FrozenObjectRegion, FrozenSegmentRegion, FrozenSegmentRegion.StartSymbol, FrozenSegmentRegion.EndSymbol);
            ReadyToRunHeader.Add(ReadyToRunSectionType.TypeManagerIndirection, TypeManagerIndirection, TypeManagerIndirection);
            ReadyToRunHeader.Add(ReadyToRunSectionType.GCStaticRegion, GCStaticsRegion, GCStaticsRegion.StartSymbol, GCStaticsRegion.EndSymbol);
            ReadyToRunHeader.Add(ReadyToRunSectionType.GCStaticDesc, GCStaticDescRegion, GCStaticDescRegion.StartSymbol, GCStaticDescRegion.EndSymbol);
            ReadyToRunHeader.Add(ReadyToRunSectionType.ThreadStaticRegion, ThreadStaticsRegion, ThreadStaticsRegion.StartSymbol, ThreadStaticsRegion.EndSymbol);
            ReadyToRunHeader.Add(ReadyToRunSectionType.ThreadStaticOffsetRegion, ThreadStaticsOffsetRegion, ThreadStaticsOffsetRegion.StartSymbol, ThreadStaticsOffsetRegion.EndSymbol);
            ReadyToRunHeader.Add(ReadyToRunSectionType.ThreadStaticGCDescRegion, ThreadStaticGCDescRegion, ThreadStaticGCDescRegion.StartSymbol, ThreadStaticGCDescRegion.EndSymbol);
            ReadyToRunHeader.Add(ReadyToRunSectionType.ThreadStaticIndex, ThreadStaticsIndex, ThreadStaticsIndex);
            ReadyToRunHeader.Add(ReadyToRunSectionType.ThreadStaticStartOffset, ThreadStaticsStartOffset, ThreadStaticsStartOffset);

            MetadataManager.AddToReadyToRunHeader(ReadyToRunHeader);
            MetadataManager.AttachToDependencyGraph(graph);
        }

        protected override IMethodNode CreateMethodEntrypointNode(MethodDesc method)
        {
            if (method.HasCustomAttribute("System.Runtime", "RuntimeImportAttribute"))
            {
                return new RuntimeImportMethodNode(method);
            }

            return new ExternMethodSymbolNode(method);
        }

        protected override IMethodNode CreateUnboxingStubNode(MethodDesc method)
        {
            return new UnboxingStubNode(method);
        }

        protected override ISymbolNode CreateReadyToRunHelperNode(Tuple<ReadyToRunHelperId, object> helperCall)
        {
            return new ReadyToRunHelperNode(this, helperCall.Item1, helperCall.Item2);
        }

        protected override IMethodNode CreateShadowConcreteMethodNode(MethodDesc method)
        {
            // All methods are modeled as ExternMethodSymbolNode in ProjectX for now.
            return new ShadowConcreteMethodNode<ExternMethodSymbolNode>(method,
                (ExternMethodSymbolNode)MethodEntrypoint(method.GetCanonMethodTarget(CanonicalFormKind.Specific)));
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

        public ThreadStaticsStartNode ThreadStaticsStartOffset;

        public ThreadStaticsDirectoryNode TLSDirectory;

        // These two are defined in startup code to mark start and end of the entire Thread Local Storage area,
        // including the TLS data from different managed and native object files.
        public ExternSymbolNode TlsStart;
        public ExternSymbolNode TlsEnd;

        private NodeCache<MetadataType, GCStaticDescNode> _GCStaticDescs;

        public ISymbolNode TypeGCStaticDescSymbol(MetadataType type)
        {
            if (CompilationModuleGroup.ContainsType(type))
            {
                return _GCStaticDescs.GetOrAdd(type);
            }
            else
            {
                return ExternSymbol(GCStaticDescNode.GetMangledName(type, false));
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
                return ExternSymbol(GCStaticDescNode.GetMangledName(type, true));
            }
        }

        private NodeCache<MetadataType, ThreadStaticsOffsetNode> _threadStaticsOffset;

        public ISymbolNode TypeThreadStaticsOffsetSymbol(MetadataType type)
        {
            if (CompilationModuleGroup.ContainsType(type))
            {
                return _threadStaticsOffset.GetOrAdd(type);
            }
            else
            {
                return ExternSymbol(ThreadStaticsOffsetNode.GetMangledName(type));
            }
        }

        public ISymbolNode ThreadStaticsOffsetSymbol(MetadataType type)
        {
            if (CompilationModuleGroup.ContainsType(type))
            {
                return _threadStaticsOffset.GetOrAdd(type);
            }
            else
            {
                return ExternSymbol(ThreadStaticsOffsetNode.GetMangledName(type));
            }
        }

        private NodeCache<TypeSystemEntity, UtcDictionaryLayoutNode> _hostedGenericDictionaryLayouts;

        public override DictionaryLayoutNode GenericDictionaryLayout(TypeSystemEntity methodOrType)
        {
            return _hostedGenericDictionaryLayouts.GetOrAdd(methodOrType);
        }

        private NodeCache<MethodDesc, NonExternMethodSymbolNode> _nonExternMethodSymbols;

        public NonExternMethodSymbolNode NonExternMethodSymbol(MethodDesc method)
        {
            return _nonExternMethodSymbols.GetOrAdd(method);
        }
    }
}
