// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Collections.Generic;
using Internal.Text;

namespace ILCompiler.DependencyAnalysis.ReadyToRun
{
    public class MethodGCInfoNode : EmbeddedObjectNode, ISymbolDefinitionNode
    {
        private readonly MethodWithGCInfo _methodNode;

        private readonly MethodEHInfoNode _ehInfoNode;

        public MethodGCInfoNode(MethodWithGCInfo methodNode)
        {
            _methodNode = methodNode;
            _ehInfoNode = new MethodEHInfoNode(_methodNode);
        }

        public override bool StaticDependenciesAreComputed => true;

        int ISymbolDefinitionNode.Offset => OffsetFromBeginningOfArray;

        int ISymbolNode.Offset => 0;

        protected override int ClassCode => 892356612;

        public void AppendMangledName(NameMangler nameMangler, Utf8StringBuilder sb)
        {
            sb.Append("MethodGCInfoNode->");
            _methodNode.AppendMangledName(nameMangler, sb);
        }

        public override void EncodeData(ref ObjectDataBuilder dataBuilder, NodeFactory factory, bool relocsOnly)
        {
            if (relocsOnly)
            {
                return;
            }

            byte[] gcInfo = _methodNode.GCInfo;

            // Temporary hotfix - this stands for the AMD64 UNWIND_INFO I don't yet know where to get from
            dataBuilder.EmitLong(0);

            if (gcInfo != null)
            {
                dataBuilder.EmitBytes(gcInfo);
            }

            /* TODO: This is apparently incorrect, a different encoding is needed here
            ObjectNode.ObjectData ehInfo = _methodNode.EHInfo;
            ISymbolNode associatedDataNode = _methodNode.GetAssociatedDataNode(factory);

            foreach (FrameInfo frameInfo in _methodNode.FrameInfos)
            {
                FrameInfoFlags flags = frameInfo.Flags;
                flags |= (ehInfo != null ? FrameInfoFlags.HasEHInfo : 0);
                flags |= (associatedDataNode != null ? FrameInfoFlags.HasAssociatedData : 0);

                dataBuilder.EmitBytes(frameInfo.BlobData);
                dataBuilder.EmitByte((byte)flags);

                if (associatedDataNode != null)
                {
                    dataBuilder.EmitReloc(associatedDataNode, RelocType.IMAGE_REL_BASED_ADDR32NB);
                    associatedDataNode = null;
                }

                if (ehInfo != null)
                {
                    dataBuilder.EmitReloc(_ehInfoNode, RelocType.IMAGE_REL_BASED_ADDR32NB);
                    ehInfo = null;
                }

                if (gcInfo != null)
                {
                    dataBuilder.EmitBytes(gcInfo);
                    gcInfo = null;
                }

                // Align the record to 4 bytes
                int alignedOffset = (dataBuilder.CountBytes + 3) & -4;
                int paddingCount = alignedOffset - dataBuilder.CountBytes;
                dataBuilder.EmitZeros(paddingCount);
            }
            */
        }

        public override IEnumerable<DependencyListEntry> GetStaticDependencies(NodeFactory context)
        {
            return new DependencyListEntry[] { new DependencyListEntry(_ehInfoNode, "EH info for method") };
        }

        protected override string GetName(NodeFactory context)
        {
            Utf8StringBuilder sb = new Utf8StringBuilder();
            sb.Append("MethodGCInfo->");
            _methodNode.AppendMangledName(context.NameMangler, sb);
            return sb.ToString();
        }
    }
}
