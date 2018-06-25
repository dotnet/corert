// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Reflection.Metadata;
using System.Reflection.Metadata.Ecma335;
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
        private string _inputFilePath;
        private string _objectFilePath;
        private IEnumerable<DependencyNode> _nodes;

        private int _headerSectionIndex;

#if DEBUG
        Dictionary<string, ISymbolNode> _previouslyWrittenNodeNames = new Dictionary<string, ISymbolNode>();
#endif

        public ReadyToRunObjectWriter(string inputFilePath, string objectFilePath, IEnumerable<DependencyNode> nodes, ReadyToRunCodegenNodeFactory factory)
        {
            _inputFilePath = inputFilePath;
            _objectFilePath = objectFilePath;
            _nodes = nodes;
            _nodeFactory = factory;
        }
        
        public void EmitPortableExecutable()
        {
            FileStream inputFileStream = File.OpenRead(_inputFilePath);
            bool succeeded = false;

            try
            {
                var peReader = new PEReader(inputFileStream);
                var peBuilder = new R2RPEBuilder(Machine.Amd64, peReader, new ValueTuple<string, SectionCharacteristics>[0]);
                var sectionBuilder = new SectionBuilder();

                _headerSectionIndex = sectionBuilder.AddSection(R2RPEBuilder.TextSectionName, SectionCharacteristics.ContainsCode | SectionCharacteristics.MemExecute | SectionCharacteristics.MemRead, 512);
                sectionBuilder.SetReadyToRunHeaderTable(_nodeFactory.CoreCLRReadyToRunHeader, _nodeFactory.CoreCLRReadyToRunHeader.GetData(_nodeFactory).Data.Length);

                foreach (var depNode in _nodes)
                {
                    if (depNode is MethodCodeNode methodNode)
                    {
                        int methodIndex = _nodeFactory.CoreCLRReadyToRunRuntimeFunctionsTable.Add(methodNode);
                        if (methodNode.Method is EcmaMethod ecmaMethod)
                        {
                            // Strip away the token type bits, keep just the low 24 bits RID
                            int rid = MetadataTokens.GetToken(ecmaMethod.Handle) & 0x00FFFFFF;
                            Debug.Assert(rid != 0);
                            
                            // TODO: how to synthesize method fixups blob?
                            byte[] fixups = null;
                            _nodeFactory.CoreCLRReadyToRunMethodEntryPointTable.Add(rid - 1, methodIndex, fixups, signature: null, methodHashCode: 0);
                        }

                        // TODO: method instance table
                    }
                    if (depNode is EETypeNode eeTypeNode &&
                        eeTypeNode.Type is EcmaType ecmaType)
                    {
                        int rid = MetadataTokens.GetToken(ecmaType.Handle) & 0x00FFFFFF;
                        Debug.Assert(rid != 0);
                        _nodeFactory.CoreCLRReadyToRunTypesTable.Add(rid, eeTypeNode);
                        
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

                    sectionBuilder.AddObjectData(nodeContents, _headerSectionIndex);
                }

                using (var peStream = File.Create(_objectFilePath))
                {
                    sectionBuilder.EmitR2R(Machine.Amd64, peReader, peStream);
                }

                succeeded = true;
            }
            finally
            {
                inputFileStream.Dispose();

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

        public static void EmitObject(string inputFilePath, string objectFilePath, IEnumerable<DependencyNode> nodes, ReadyToRunCodegenNodeFactory factory)
        {
            ReadyToRunObjectWriter objectWriter = new ReadyToRunObjectWriter(inputFilePath, objectFilePath, nodes, factory);
            objectWriter.EmitPortableExecutable();
        }
    }
}
