// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using System.Diagnostics;
using System.Runtime.InteropServices;
using System.IO;
using System.Text;

using Internal.Text;

namespace ILCompiler.DependencyAnalysis
{
    internal class WindowsDebugILImagesSection : ObjectNode, ISymbolDefinitionNode
    {
        private MergedAssemblyRecords _mergedAssemblies;

        public WindowsDebugILImagesSection(MergedAssemblyRecords mergedAssemblies)
        {
            _mergedAssemblies = mergedAssemblies;
        }

        private ObjectNodeSection _section = new ObjectNodeSection(".ilimges", SectionType.ReadOnly);
        public override ObjectNodeSection Section => _section;

        public override bool IsShareable => false;

        public override bool StaticDependenciesAreComputed => true;

        public int Offset => 0;

        public override int ClassCode => 2051656903;

        public void AppendMangledName(NameMangler nameMangler, Utf8StringBuilder sb)
        {
            sb.Append(GetName(null));
        }

        public override ObjectData GetData(NodeFactory factory, bool relocsOnly = false)
        {
            // This node does not trigger generation of other nodes.
            if (relocsOnly)
                return new ObjectData(Array.Empty<byte>(), Array.Empty<Relocation>(), 1, new ISymbolDefinitionNode[] { this });

            MemoryStream memoryStream = new MemoryStream();
            BinaryWriter writer = new BinaryWriter(memoryStream, Encoding.Unicode, true);
            writer.Write(1); // magic format version number
            writer.Write(_mergedAssemblies.MergedAssemblies.Count); // number of il images that will follow

            int totalSizeOfActualMergedAssemblies = 0;
            checked
            {
                const int ILIMAGES_HEADER_ELEMENTS = 2;
                const int ILIMAGES_PERMODULE_ELEMENTS = 3;
                int endOfILAssemblyListHeader = ((_mergedAssemblies.MergedAssemblies.Count * ILIMAGES_PERMODULE_ELEMENTS) + ILIMAGES_HEADER_ELEMENTS) * sizeof(int);

                int offsetOfNextAssembly = endOfILAssemblyListHeader;

                foreach (MergedAssemblyRecord mergedAssembly in _mergedAssemblies.MergedAssemblies)
                {
                    int assemblyFileLen = mergedAssembly.Assembly.PEReader.GetEntireImage().Length;
                    writer.Write(mergedAssembly.AssemblyIndex);
                    writer.Write(offsetOfNextAssembly);
                    writer.Write(assemblyFileLen);
                    offsetOfNextAssembly += assemblyFileLen;
                    totalSizeOfActualMergedAssemblies += assemblyFileLen;
                }

                writer.Flush();
                writer.Dispose();

                byte[] mergedAssemblyHeader = memoryStream.ToArray();
                Debug.Assert(mergedAssemblyHeader.Length == endOfILAssemblyListHeader);

                byte[] mergedAssemblyBlob = new byte[mergedAssemblyHeader.Length + totalSizeOfActualMergedAssemblies];
                Array.Copy(mergedAssemblyHeader, mergedAssemblyBlob, mergedAssemblyHeader.Length);

                offsetOfNextAssembly = endOfILAssemblyListHeader;
                foreach (MergedAssemblyRecord mergedAssembly in _mergedAssemblies.MergedAssemblies)
                {
                    var memoryBlock = mergedAssembly.Assembly.PEReader.GetEntireImage();
                    int assemblyFileLen = memoryBlock.Length;
                    unsafe
                    {
                        Marshal.Copy(new IntPtr(memoryBlock.Pointer), mergedAssemblyBlob, offsetOfNextAssembly, assemblyFileLen);
                    }
                    offsetOfNextAssembly += assemblyFileLen;
                }

                return new ObjectData(mergedAssemblyBlob, Array.Empty<Relocation>(), 1, new ISymbolDefinitionNode[] { this });
            }
        }

        protected override string GetName(NodeFactory context)
        {
            return "___DebugILImagesSection";
        }
    }
}
