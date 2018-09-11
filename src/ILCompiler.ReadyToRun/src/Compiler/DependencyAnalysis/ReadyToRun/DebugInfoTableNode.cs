// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.IO;

using Internal.JitInterface;
using Internal.NativeFormat;
using Internal.Text;
using Internal.TypeSystem;

namespace ILCompiler.DependencyAnalysis.ReadyToRun
{
    public class DebugInfoTableNode : HeaderTableNode
    {
        public DebugInfoTableNode(TargetDetails target) : base(target) { }

        public override int ClassCode => 1000735112;

        public override void AppendMangledName(NameMangler nameMangler, Utf8StringBuilder sb)
        {
            sb.Append(nameMangler.CompilationUnitPrefix);
            sb.Append("__ReadyToRunDebugInfoTable");
        }

        public override ObjectData GetData(NodeFactory factory, bool relocsOnly = false)
        {
            // This node does not trigger generation of other nodes.
            if (relocsOnly)
                return new ObjectData(Array.Empty<byte>(), Array.Empty<Relocation>(), 1, new ISymbolDefinitionNode[] { this });

            NativeWriter writer = new NativeWriter();
            Section section = writer.NewSection();
            VertexArray vertexArray = new VertexArray(section);
            section.Place(vertexArray);

            foreach (MethodDesc method in ((ReadyToRunTableManager)factory.MetadataManager).GetCompiledMethods())
            {
                MethodWithGCInfo methodCodeNode = factory.MethodEntrypoint(method) as MethodWithGCInfo;
                if (methodCodeNode == null)
                {
                    methodCodeNode = ((ExternalMethodImport)factory.MethodEntrypoint(method))?.MethodCodeNode;
                    if (methodCodeNode == null)
                        continue;
                }

                MemoryStream methodDebugBlob = new MemoryStream();
                
                byte[] bounds = CreateBoundsBlobForMethod(methodCodeNode);
                byte[] vars = CreateVarBlobForMethod(methodCodeNode);

                NibbleWriter nibbleWriter = new NibbleWriter();
                nibbleWriter.WriteUInt((uint)(bounds?.Length ?? 0));
                nibbleWriter.WriteUInt((uint)(vars?.Length ?? 0));

                byte[] header = nibbleWriter.ToArray();
                methodDebugBlob.Write(header, 0, header.Length);

                if (bounds?.Length > 0)
                {
                    methodDebugBlob.Write(bounds, 0, bounds.Length);
                }

                if (vars?.Length > 0)
                {
                    methodDebugBlob.Write(vars, 0, vars.Length);
                }

                BlobVertex debugBlob = new BlobVertex(methodDebugBlob.ToArray());

                vertexArray.Set(((ReadyToRunCodegenNodeFactory)factory).RuntimeFunctionsTable.GetIndex(methodCodeNode), new DebugInfoVertex(debugBlob));
            }

            vertexArray.ExpandLayout();

            MemoryStream writerContent = new MemoryStream();
            writer.Save(writerContent);

            return new ObjectData(
                data: writerContent.ToArray(),
                relocs: null,
                alignment: 8,
                definedSymbols: new ISymbolDefinitionNode[] { this });
        }

        private byte[] CreateBoundsBlobForMethod(MethodWithGCInfo method)
        {
            if (method.DebugLocInfos == null || method.DebugLocInfos.Length == 0)
                return null;

            NibbleWriter writer = new NibbleWriter();
            writer.WriteUInt((uint)method.DebugLocInfos.Length);

            uint previousNativeOffset = 0;
            foreach (var locInfo in method.DebugLocInfos)
            {
                NativeLocInfo offsetMapping = locInfo.OffsetMapping;
                writer.WriteUInt(offsetMapping.nativeOffset - previousNativeOffset);
                writer.WriteUInt(offsetMapping.ilOffset + 3); // Count of items in Internal.JitInterface.MappingTypes to adjust the IL offset by
                writer.WriteUInt(offsetMapping.source);

                previousNativeOffset = offsetMapping.nativeOffset;
            }

            return writer.ToArray();
        }

        private byte[] CreateVarBlobForMethod(MethodWithGCInfo method)
        {
            if (method.DebugVarInfos == null || method.DebugVarInfos.Length == 0)
                return null;

            NibbleWriter writer = new NibbleWriter();
            writer.WriteUInt((uint)method.DebugVarInfos[0].Ranges.Count);

            foreach (var nativeVarInfo in method.DebugVarInfos[0].Ranges)
            {
                writer.WriteUInt(nativeVarInfo.startOffset);
                writer.WriteUInt(nativeVarInfo.endOffset - nativeVarInfo.startOffset);
                writer.WriteUInt((uint)(nativeVarInfo.varNumber - (int)ILNum.MAX_ILNUM));

                VarLocType varLocType = (VarLocType)(nativeVarInfo.varLoc.A.ToInt64() & 0xFFFFFFFF);

                writer.WriteUInt((uint)varLocType);

                switch (varLocType)
                {
                    case VarLocType.VLT_REG:
                    case VarLocType.VLT_REG_FP:
                    case VarLocType.VLT_REG_BYREF:
                        writer.WriteUInt((uint)nativeVarInfo.varLoc.B);
                        break;
                    case VarLocType.VLT_STK:
                    case VarLocType.VLT_STK_BYREF:
                        writer.WriteUInt((uint)nativeVarInfo.varLoc.B);
                        writer.WriteInt(nativeVarInfo.varLoc.C);
                        break;
                    case VarLocType.VLT_REG_REG:
                        writer.WriteUInt((uint)nativeVarInfo.varLoc.B);
                        writer.WriteUInt((uint)nativeVarInfo.varLoc.C);
                        break;
                    case VarLocType.VLT_REG_STK:
                        writer.WriteUInt((uint)nativeVarInfo.varLoc.B);
                        writer.WriteUInt((uint)nativeVarInfo.varLoc.C);
                        writer.WriteInt(nativeVarInfo.varLoc.D);
                        break;
                    case VarLocType.VLT_STK_REG:
                        writer.WriteInt(nativeVarInfo.varLoc.B);
                        writer.WriteUInt((uint)nativeVarInfo.varLoc.C);
                        writer.WriteUInt((uint)nativeVarInfo.varLoc.D);
                        break;
                    case VarLocType.VLT_STK2:
                        writer.WriteUInt((uint)nativeVarInfo.varLoc.B);
                        writer.WriteInt(nativeVarInfo.varLoc.C);
                        break;
                    case VarLocType.VLT_FPSTK:
                        writer.WriteUInt((uint)nativeVarInfo.varLoc.B);
                        break;
                    case VarLocType.VLT_FIXED_VA:
                        writer.WriteUInt((uint)nativeVarInfo.varLoc.B);
                        break;
                    default:
                        throw new BadImageFormatException("Unexpected var loc type");
                }
            }

            return writer.ToArray();
        }

        enum VarLocType : uint
        {
            VLT_REG,        // variable is in a register
            VLT_REG_BYREF,  // address of the variable is in a register
            VLT_REG_FP,     // variable is in an fp register
            VLT_STK,        // variable is on the stack (memory addressed relative to the frame-pointer)
            VLT_STK_BYREF,  // address of the variable is on the stack (memory addressed relative to the frame-pointer)
            VLT_REG_REG,    // variable lives in two registers
            VLT_REG_STK,    // variable lives partly in a register and partly on the stack
            VLT_STK_REG,    // reverse of VLT_REG_STK
            VLT_STK2,       // variable lives in two slots on the stack
            VLT_FPSTK,      // variable lives on the floating-point stack
            VLT_FIXED_VA,   // variable is a fixed argument in a varargs function (relative to VARARGS_HANDLE)

            VLT_COUNT,
            VLT_INVALID
        };
    }
}
