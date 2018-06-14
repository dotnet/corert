// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Collections.Generic;
using System.IO;
using System.Reflection.Metadata;
using System.Reflection.PortableExecutable;

using ILCompiler.DependencyAnalysisFramework;
using ILCompiler.PEWriter;

namespace ILCompiler.DependencyAnalysis
{
    /// <summary>
    /// Object writer using R2RPEReader to directly emit Windows Portable Executable binaries
    /// </summary>
    internal class ReadyToRunObjectWriter
    {
        // Nodefactory for which ObjectWriter is instantiated for.
        private NodeFactory _nodeFactory;
        private string _inputFilePath;
        private string _objectFilePath;
        
        public ReadyToRunObjectWriter(string inputFilePath, string objectFilePath, NodeFactory factory, ReadyToRunCodegenCompilation compilation)
        {
            _nodeFactory = factory;
            _inputFilePath = inputFilePath;
            _objectFilePath = objectFilePath;
        }
        
        public void EmitPortableExecutable()
        {
            using (FileStream sr = File.OpenRead(_inputFilePath))
            {
                PEReader peReader = new PEReader(sr);
                R2RPEBuilder peBuilder = new R2RPEBuilder(peReader);
                
                var peBlob = new BlobBuilder();
                peBuilder.Serialize(peBlob);

                using (var peStream = File.Create(_objectFilePath))
                {
                    peBlob.WriteContentTo(peStream);
                }
            }
        }

        public static void EmitObject(string inputFilePath, string objectFilePath, IEnumerable<DependencyNode> nodes, NodeFactory factory, ReadyToRunCodegenCompilation compilation, IObjectDumper dumper)
        {
            ReadyToRunObjectWriter objectWriter = new ReadyToRunObjectWriter(inputFilePath, objectFilePath, factory, compilation);
            objectWriter.EmitPortableExecutable();
        }
    }
}
