// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.Diagnostics;
using Internal.TypeSystem.TypesDebugInfo;
using ILCompiler.DependencyAnalysisFramework;

namespace ILCompiler.DependencyAnalysis
{
    // Part of Node factory that deals with nodes describing windows debug data
    partial class NodeFactory
    {
        /// <summary>
        /// Helper class that provides a level of grouping for all the windows debug data nodes
        /// </summary>
        public class WindowsDebugDataHelper
        {
            NodeFactory _nodeFactory;

            public WindowsDebugDataHelper(NodeFactory nodeFactory)
            {
                _nodeFactory = nodeFactory;
            }

            /// <summary>
            /// Initialize the WindowsDebugData emission pipeline.
            /// Cannot be called twice
            /// </summary>
            /// <param name="nonSectionBasedDebugInfoWriter">debug$T section generation interface. If null, use managed implementation that generates a object file section.</param>
            /// <param name="mergedAssemblyRecords">record of how assemblies are merged. If null, do not genearate extended debug information</param>
            /// <param name="graph">Graph to attach WindowsDebugData to</param>
            public void Init(ITypesDebugInfoWriter nonSectionBasedDebugInfoWriter, MergedAssemblyRecords mergedAssemblyRecords, DependencyAnalyzerBase<NodeFactory> graph)
            {
                Debug.Assert(_userDefinedTypeDescriptor == null); // Cannot be called twice
                Debug.Assert(graph != null);

                _debugNeedTypeIndicesStore = new WindowsDebugNeedTypeIndicesStoreNode();

                if (mergedAssemblyRecords != null)
                {
                    _debugPseudoAssemblySection = new WindowsDebugPseudoAssemblySection(_nodeFactory.TypeSystemContext);
                    _debugMergedAssembliesSection = new WindowsDebugMergedAssembliesSection(mergedAssemblyRecords);
                    _debugILImagesSection = new WindowsDebugILImagesSection(mergedAssemblyRecords);
                    _debugManagedNativeDictionaryInfoSection = new WindowsDebugManagedNativeDictionaryInfoSection();

                    _debugTypeSignatureMapSection = new WindowsDebugTypeSignatureMapSection(_userDefinedTypeDescriptor);
                    _debugMethodSignatureMapSection = new WindowsDebugMethodSignatureMapSection();
                    _debugVirtualMethodInfoSection = new WindowsDebugMethodInfoSection(mergedAssemblyRecords);
                }

                if (nonSectionBasedDebugInfoWriter != null)
                {
                    _userDefinedTypeDescriptor = new UserDefinedTypeDescriptor(nonSectionBasedDebugInfoWriter, _nodeFactory);
                }
                else
                {
                    _debugTypeRecordsSection = new WindowsDebugTypeRecordsSection(new DebugInfoWriter(), _nodeFactory);
                    _userDefinedTypeDescriptor = new UserDefinedTypeDescriptor(_debugTypeRecordsSection, _nodeFactory);
                }

                graph.AddRoot(_debugNeedTypeIndicesStore, "Debug Force All EETypes to have type indices");

                if (_debugManagedNativeDictionaryInfoSection != null)
                    graph.AddRoot(_debugManagedNativeDictionaryInfoSection, "Debug Method MDToken map");
                if (_debugMethodSignatureMapSection != null)
                    graph.AddRoot(_debugMethodSignatureMapSection, "Debug Method MDToken map");
                if (_debugTypeSignatureMapSection != null)
                    graph.AddRoot(_debugTypeSignatureMapSection, "Debug Type MDToken map");
                if (_debugILImagesSection != null)
                    graph.AddRoot(_debugILImagesSection, "Debug Merged ILImages");
                if (_debugMergedAssembliesSection != null)
                    graph.AddRoot(_debugMergedAssembliesSection, "Debug MergedAssemblyRecords");
                if (_debugPseudoAssemblySection != null)
                    graph.AddRoot(_debugPseudoAssemblySection, "Debug PseudoAssembly");
                if (_debugTypeRecordsSection != null)
                    graph.AddRoot(_debugTypeRecordsSection, "Debug Type Records");
                if (_debugVirtualMethodInfoSection != null)
                    graph.AddRoot(_debugVirtualMethodInfoSection, "Debug Virtual Method map");
            }

            private WindowsDebugILImagesSection _debugILImagesSection;
            private WindowsDebugTypeSignatureMapSection _debugTypeSignatureMapSection;
            private WindowsDebugTypeRecordsSection _debugTypeRecordsSection;
            private WindowsDebugManagedNativeDictionaryInfoSection _debugManagedNativeDictionaryInfoSection;
            private WindowsDebugMethodSignatureMapSection _debugMethodSignatureMapSection;
            private WindowsDebugMergedAssembliesSection _debugMergedAssembliesSection;
            private WindowsDebugPseudoAssemblySection _debugPseudoAssemblySection;
            private UserDefinedTypeDescriptor _userDefinedTypeDescriptor;
            private WindowsDebugNeedTypeIndicesStoreNode _debugNeedTypeIndicesStore;
            private WindowsDebugMethodInfoSection _debugVirtualMethodInfoSection;

            internal WindowsDebugILImagesSection DebugILImagesSection => _debugILImagesSection;
            internal WindowsDebugTypeSignatureMapSection DebugTypeSignatureMapSection => _debugTypeSignatureMapSection;
            internal WindowsDebugMethodSignatureMapSection DebugMethodSignatureMapSection => _debugMethodSignatureMapSection;
            internal WindowsDebugTypeRecordsSection DebugTypeRecordsSection => _debugTypeRecordsSection;
            internal WindowsDebugMergedAssembliesSection DebugMergedAssembliesSection => _debugMergedAssembliesSection;
            internal WindowsDebugPseudoAssemblySection DebugPseudoAssemblySection => _debugPseudoAssemblySection;
            internal WindowsDebugManagedNativeDictionaryInfoSection DebugManagedNativeDictionaryInfoSection => _debugManagedNativeDictionaryInfoSection;
            internal WindowsDebugNeedTypeIndicesStoreNode DebugNeedTypeIndicesStore => _debugNeedTypeIndicesStore;
            internal WindowsDebugMethodInfoSection DebugVirtualMethodInfoSection => _debugVirtualMethodInfoSection;

            public UserDefinedTypeDescriptor UserDefinedTypeDescriptor => _userDefinedTypeDescriptor;
        }

        public WindowsDebugDataHelper WindowsDebugData { get; private set; }
    }
}
