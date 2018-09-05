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

        private readonly int _frameInfoIndex;

        public MethodGCInfoNode(MethodWithGCInfo methodNode, int frameInfoIndex)
        {
            _methodNode = methodNode;
            _frameInfoIndex = frameInfoIndex;
        }

        public override bool StaticDependenciesAreComputed => true;

        int ISymbolDefinitionNode.Offset => OffsetFromBeginningOfArray;

        int ISymbolNode.Offset => 0;

        public override int ClassCode => 892356612;

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
            byte[] unwindInfo = _methodNode.FrameInfos[_frameInfoIndex].BlobData;

            if (factory.Target.Architecture == Internal.TypeSystem.TargetArchitecture.X64)
            {
                // On Amd64, patch the first byte of the unwind info by setting the flags to EHANDLER | UHANDLER
                // as that's what CoreCLR does (zapcode.cpp, ZapUnwindData::Save).
                const byte UNW_FLAG_EHANDLER = 1;
                const byte UNW_FLAG_UHANDLER = 2;
                const byte FlagsShift = 3;

                unwindInfo[0] |= (byte)((UNW_FLAG_EHANDLER | UNW_FLAG_UHANDLER) << FlagsShift);
            }

            dataBuilder.EmitBytes(unwindInfo);

            // Personality routine RVA must be 4-aligned
            int align4Pad = -unwindInfo.Length & 3;
            dataBuilder.EmitZeros(align4Pad);

            bool isFilterFunclet = (_methodNode.FrameInfos[_frameInfoIndex].Flags & FrameInfoFlags.Filter) != 0;
            ReadyToRunCodegenNodeFactory r2rFactory = (ReadyToRunCodegenNodeFactory)factory;
            ISymbolNode personalityRoutine = (isFilterFunclet ? r2rFactory.FilterFuncletPersonalityRoutine : r2rFactory.PersonalityRoutine);
            dataBuilder.EmitReloc(personalityRoutine, RelocType.IMAGE_REL_BASED_ADDR32NB);

            if (_frameInfoIndex == 0 && _methodNode.GCInfo != null)
            {
                dataBuilder.EmitBytes(_methodNode.GCInfo);

                // Maintain 4-alignment for the next unwind / GC info block
                align4Pad = -_methodNode.GCInfo.Length & 3;
                dataBuilder.EmitZeros(align4Pad);
            }
        }

        public override IEnumerable<DependencyListEntry> GetStaticDependencies(NodeFactory context)
        {
            return Array.Empty<DependencyListEntry>();
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
