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
using ILCompiler.DependencyAnalysis.ReadyToRun;
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
        private int _rdataSectionIndex;
        private int _dataSectionIndex;

#if DEBUG
        Dictionary<string, (ISymbolNode Node, int NodeIndex, int SymbolIndex)> _previouslyWrittenNodeNames = new Dictionary<string, (ISymbolNode Node, int NodeIndex, int SymbolIndex)>();
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

            FileStream mapFileStream = null;
            TextWriter mapFile = null;

            try
            {
                string mapFileName = Path.ChangeExtension(_objectFilePath, ".map");
                mapFileStream = new FileStream(mapFileName, FileMode.Create, FileAccess.Write);
                mapFile = new StreamWriter(mapFileStream);

                Stopwatch stopwatch = new Stopwatch();
                stopwatch.Start();
                mapFile.WriteLine($@"R2R object emission started: {DateTime.Now}");

                var peBuilder = new R2RPEBuilder(Machine.Amd64, _nodeFactory.PEReader, new ValueTuple<string, SectionCharacteristics>[0]);
                var sectionBuilder = new SectionBuilder();

                _textSectionIndex = sectionBuilder.AddSection(R2RPEBuilder.TextSectionName, SectionCharacteristics.ContainsCode | SectionCharacteristics.MemExecute | SectionCharacteristics.MemRead, 512);
                _rdataSectionIndex = sectionBuilder.AddSection(".rdata", SectionCharacteristics.ContainsInitializedData | SectionCharacteristics.MemRead, 512);
                _dataSectionIndex = sectionBuilder.AddSection(".data", SectionCharacteristics.ContainsInitializedData | SectionCharacteristics.MemWrite | SectionCharacteristics.MemRead, 512);

                foreach (var depNode in _nodes)
                {
                    if (depNode is EETypeNode eeTypeNode)
                    {
                        _nodeFactory.TypesTable.Add(eeTypeNode);
                    }
                }

                int nodeIndex = -1;
                foreach (var depNode in _nodes)
                {
                    ++nodeIndex;
                    ObjectNode node = depNode as ObjectNode;

                    if (node == null)
                        continue;

                    if (node.ShouldSkipEmittingObjectNode(_nodeFactory))
                        continue;

                    ObjectData nodeContents = node.GetData(_nodeFactory);
#if DEBUG
                    for (int symbolIndex = 0; symbolIndex < nodeContents.DefinedSymbols.Length; symbolIndex++)
                    {
                        ISymbolNode definedSymbol = nodeContents.DefinedSymbols[symbolIndex];
                        (ISymbolNode Node, int NodeIndex, int SymbolIndex) alreadyWrittenSymbol;
                        string symbolName = definedSymbol.GetMangledName(_nodeFactory.NameMangler);
                        if (_previouslyWrittenNodeNames.TryGetValue(symbolName, out alreadyWrittenSymbol))
                        {
                            Console.WriteLine($@"Duplicate symbol - 1st occurrence: [{alreadyWrittenSymbol.NodeIndex}:{alreadyWrittenSymbol.SymbolIndex}], {alreadyWrittenSymbol.Node.GetMangledName(_nodeFactory.NameMangler)}");
                            Console.WriteLine($@"Duplicate symbol - 2nd occurrence: [{nodeIndex}:{symbolIndex}], {definedSymbol.GetMangledName(_nodeFactory.NameMangler)}");
                            Debug.Fail("Duplicate node name emitted to file",
                            $"Symbol {definedSymbol.GetMangledName(_nodeFactory.NameMangler)} has already been written to the output object file {_objectFilePath} with symbol {alreadyWrittenSymbol}");
                        }
                        _previouslyWrittenNodeNames.Add(symbolName, (Node: definedSymbol, NodeIndex: nodeIndex, SymbolIndex: symbolIndex));
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

                    string name = null;

                    if (mapFile != null)
                    {
                        name = depNode.GetType().ToString();
                        int firstGeneric = name.IndexOf('[');
                        if (firstGeneric < 0)
                        {
                            firstGeneric = name.Length;
                        }
                        int lastDot = name.LastIndexOf('.', firstGeneric - 1, firstGeneric);
                        if (lastDot > 0)
                        {
                            name = name.Substring(lastDot + 1);
                        }
                    }
                    sectionBuilder.AddObjectData(nodeContents, targetSectionIndex, name, mapFile);
                }

                sectionBuilder.SetReadyToRunHeaderTable(_nodeFactory.Header, _nodeFactory.Header.GetData(_nodeFactory).Data.Length);

                using (var peStream = File.Create(_objectFilePath))
                {
                    sectionBuilder.EmitR2R(Machine.Amd64, _nodeFactory.PEReader, peStream);
                }

                mapFile.WriteLine($@"R2R object emission finished: {DateTime.Now}, {stopwatch.ElapsedMilliseconds} msecs");
                mapFile.Flush();
                mapFileStream.Flush();

                succeeded = true;
            }
            finally
            {
                if (mapFile != null)
                {
                    mapFile.Dispose();
                }
                if (mapFileStream != null)
                {
                    mapFileStream.Dispose();
                }
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
            Console.WriteLine($@"Emitting R2R PE file: {objectFilePath}");
            ReadyToRunObjectWriter objectWriter = new ReadyToRunObjectWriter(objectFilePath, nodes, factory);
            objectWriter.EmitPortableExecutable();
        }
    }
}
