// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Reflection.Metadata;
using System.Reflection.PortableExecutable;

using ILCompiler.DependencyAnalysis;
using ILCompiler.DependencyAnalysisFramework;
using ILCompiler.PEWriter;
using ObjectData = ILCompiler.DependencyAnalysis.ObjectNode.ObjectData;

using Internal.Metadata.NativeFormat;
using Internal.TypeSystem.Ecma;

namespace ILCompiler.DependencyAnalysis
{
    /// <summary>
    /// Object writer using R2RPEReader to directly emit Windows Portable Executable binaries
    /// </summary>
    internal class ReadyToRunObjectWriter
    {
        // Nodefactory for which ObjectWriter is instantiated for.
        private ReadyToRunCodegenNodeFactory _nodeFactory;
        private string _objectFilePath;
        private IEnumerable<DependencyNode> _nodes;

        private int _textSectionIndex;
        private int _dataSectionIndex;
        private int _rdataSectionIndex;

#if DEBUG
        Dictionary<string, ISymbolNode> _previouslyWrittenNodeNames = new Dictionary<string, ISymbolNode>();
#endif

        public ReadyToRunObjectWriter(string objectFilePath, IEnumerable<DependencyNode> nodes, ReadyToRunCodegenNodeFactory factory)
        {
            _objectFilePath = objectFilePath;
            _nodes = nodes;
            _nodeFactory = factory;
        }
        
        public void EmitPortableExecutable()
        {
            bool succeeded = false;

            try
            {
                var peBuilder = new R2RPEBuilder(Machine.Amd64, _nodeFactory.PEReader, new ValueTuple<string, SectionCharacteristics>[0]);
                var sectionBuilder = new SectionBuilder();

                _textSectionIndex = sectionBuilder.AddSection(R2RPEBuilder.TextSectionName, SectionCharacteristics.ContainsCode | SectionCharacteristics.MemExecute | SectionCharacteristics.MemRead, 512);
                _dataSectionIndex = sectionBuilder.AddSection(".data", SectionCharacteristics.ContainsInitializedData | SectionCharacteristics.MemWrite | SectionCharacteristics.MemRead, 512);
                _rdataSectionIndex = sectionBuilder.AddSection(".rdata", SectionCharacteristics.ContainsInitializedData | SectionCharacteristics.MemRead, 512);

                sectionBuilder.SetReadyToRunHeaderTable(_nodeFactory.Header, _nodeFactory.Header.GetData(_nodeFactory).Data.Length);

                foreach (var depNode in _nodes)
                {
                    if (depNode is MethodCodeNode methodNode)
                    {
                        int methodIndex = _nodeFactory.RuntimeFunctionsTable.Add(methodNode);
                        _nodeFactory.MethodEntryPointTable.Add(methodNode, methodIndex);
                    }

                    if (depNode is EETypeNode eeTypeNode)
                    {
                        _nodeFactory.TypesTable.Add(eeTypeNode);
                    }
                }

                foreach (var depNode in _nodes)
                {
                    ObjectNode node = depNode as ObjectNode;
                    if (node == null)
                        continue;

                    if (node.ShouldSkipEmittingObjectNode(_nodeFactory))
                        continue;

                    ObjectData nodeContents = node.GetData(_nodeFactory);

#if DEBUG
                    foreach (ISymbolNode definedSymbol in nodeContents.DefinedSymbols)
                    {
                        try
                        {
                            _previouslyWrittenNodeNames.Add(definedSymbol.GetMangledName(_nodeFactory.NameMangler), definedSymbol);
                        }
                        catch (ArgumentException)
                        {
                            ISymbolNode alreadyWrittenSymbol = _previouslyWrittenNodeNames[definedSymbol.GetMangledName(_nodeFactory.NameMangler)];
                            Debug.Fail("Duplicate node name emitted to file",
                            $"Symbol {definedSymbol.GetMangledName(_nodeFactory.NameMangler)} has already been written to the output object file {_objectFilePath} with symbol {alreadyWrittenSymbol}");
                        }
                    }
#endif

                    int targetSectionIndex;
                    switch (node.Section.Type)
                    {
                        case SectionType.Executable:
                            targetSectionIndex = _textSectionIndex;
                            break;

                        case SectionType.Writeable:
                            targetSectionIndex = _dataSectionIndex;
                            break;

                        case SectionType.ReadOnly:
                            targetSectionIndex = _rdataSectionIndex;
                            break;

                        default:
                            throw new NotImplementedException();
                    }

                    sectionBuilder.AddObjectData(nodeContents, targetSectionIndex);
                }

                using (var peStream = File.Create(_objectFilePath))
                {
                    sectionBuilder.EmitR2R(Machine.Amd64, _nodeFactory.PEReader, peStream);
                }

                succeeded = true;
            }
            finally
            {
                if (!succeeded)
                {
                    // If there was an exception while generating the OBJ file, make sure we don't leave the unfinished
                    // object file around.
                    try
                    {
                        File.Delete(_objectFilePath);
                    }
                    catch
                    {
                    }
                }
            }
        }

        public static void EmitObject(string objectFilePath, IEnumerable<DependencyNode> nodes, ReadyToRunCodegenNodeFactory factory)
        {
            ReadyToRunObjectWriter objectWriter = new ReadyToRunObjectWriter(objectFilePath, nodes, factory);
            objectWriter.EmitPortableExecutable();
        }
    }
}
