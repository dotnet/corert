// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
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

        public UtcNodeFactory(CompilerTypeSystemContext context, CompilationModuleGroup compilationModuleGroup, string outputFile) 
            : base(context, compilationModuleGroup)
        {
            CreateHostedNodeCaches();
            CompilationUnitPrefix = Path.GetFileNameWithoutExtension(outputFile);
            ThreadStaticsIndex = new ThreadStaticsIndexNode(CompilationUnitPrefix);
        }

        private void CreateHostedNodeCaches()
        {
            _GCStaticDescs = new NodeCache<MetadataType, GCStaticDescNode>((MetadataType type) =>
            {
                return new GCStaticDescNode(type);
            });

            _threadStaticsOffset = new NodeCache<MetadataType, ThreadStaticsOffsetNode>((MetadataType type) =>
            {
                return new ThreadStaticsOffsetNode(type, this);
            });

            _threadStaticsIndex = new NodeCache<string, ThreadStaticsIndexNode>((string moduleName) =>
            {
                return new ThreadStaticsIndexNode(moduleName);
            });

            _hostedGenericDictionaryLayouts = new NodeCache<TypeSystemEntity, UtcDictionaryLayoutNode>((TypeSystemEntity methodOrType) =>
            {
                return new UtcDictionaryLayoutNode(methodOrType);
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
            graph.AddRoot(ThreadStaticsIndex, "Thread statics index is always generated");

            ReadyToRunHeader.Add(ReadyToRunSectionType.EagerCctor, EagerCctorTable, EagerCctorTable.StartSymbol, EagerCctorTable.EndSymbol);
            ReadyToRunHeader.Add(ReadyToRunSectionType.InterfaceDispatchTable, DispatchMapTable, DispatchMapTable.StartSymbol);
            ReadyToRunHeader.Add(ReadyToRunSectionType.FrozenObjectRegion, FrozenSegmentRegion, FrozenSegmentRegion.StartSymbol, FrozenSegmentRegion.EndSymbol);
            ReadyToRunHeader.Add(ReadyToRunSectionType.TypeManagerIndirection, TypeManagerIndirection, TypeManagerIndirection);
            ReadyToRunHeader.Add(ReadyToRunSectionType.GCStaticRegion, GCStaticsRegion, GCStaticsRegion.StartSymbol, GCStaticsRegion.EndSymbol);
            ReadyToRunHeader.Add(ReadyToRunSectionType.GCStaticDesc, GCStaticDescRegion, GCStaticDescRegion.StartSymbol, GCStaticDescRegion.EndSymbol);
            ReadyToRunHeader.Add(ReadyToRunSectionType.ThreadStaticRegion, ThreadStaticsRegion, ThreadStaticsRegion.StartSymbol, ThreadStaticsRegion.EndSymbol);
            ReadyToRunHeader.Add(ReadyToRunSectionType.ThreadStaticOffsetRegion, ThreadStaticsOffsetRegion, ThreadStaticsOffsetRegion.StartSymbol, ThreadStaticsOffsetRegion.EndSymbol);

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

        public GCStaticDescRegionNode GCStaticDescRegion = new GCStaticDescRegionNode(
            CompilationUnitPrefix + "__GCStaticDescStart", 
            CompilationUnitPrefix + "__GCStaticDescEnd");


        public ArrayOfEmbeddedDataNode ThreadStaticsOffsetRegion = new ArrayOfEmbeddedDataNode(
            CompilationUnitPrefix + "__ThreadStaticOffsetRegionStart",
            CompilationUnitPrefix + "__ThreadStaticOffsetRegionEnd",
            null);

        public ThreadStaticsIndexNode ThreadStaticsIndex;

        private NodeCache<MetadataType, GCStaticDescNode> _GCStaticDescs;

        public ISymbolNode TypeGCStaticDescSymbol(MetadataType type)
        {
            if (CompilationModuleGroup.ContainsType(type))
            {
                return _GCStaticDescs.GetOrAdd(type);
            }
            else
            {
                return ExternSymbol(GCStaticDescNode.GetMangledName(type));
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

        private NodeCache<string, ThreadStaticsIndexNode> _threadStaticsIndex;

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
    }
}
