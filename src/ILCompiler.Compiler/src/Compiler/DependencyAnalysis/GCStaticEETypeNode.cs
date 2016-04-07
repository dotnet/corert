// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Collections.Generic;
using System.Text;

using Internal.TypeSystem;

namespace ILCompiler.DependencyAnalysis
{
    internal class GCStaticEETypeNode : ObjectNode, ISymbolNode
    {
        private int[] _runLengths; // First is offset to first gc field, second is length of gc static run, third is length of non-gc data, etc
        private int _targetPointerSize;
        private TargetDetails _target;

        public GCStaticEETypeNode(bool[] gcDesc, NodeFactory factory)
        {
            List<int> runLengths = new List<int>();
            bool encodingGCPointers = false;
            int currentPointerCount = 0;
            foreach (bool pointerIsGC in gcDesc)
            {
                if (encodingGCPointers == pointerIsGC)
                {
                    currentPointerCount++;
                }
                else
                {
                    runLengths.Add(currentPointerCount * factory.Target.PointerSize);
                    encodingGCPointers = pointerIsGC;
                }
            }
            runLengths.Add(currentPointerCount);
            _runLengths = runLengths.ToArray();
            _targetPointerSize = factory.Target.PointerSize;
            _target = factory.Target;
        }

        public override string GetName()
        {
            return ((ISymbolNode)this).MangledName;
        }

        public override ObjectNodeSection Section
        {
            get
            {
                if (_target.IsWindows)
                    return ObjectNodeSection.ReadOnlyDataSection;
                else
                    return ObjectNodeSection.DataSection;
            }
        }

        public override bool StaticDependenciesAreComputed
        {
            get
            {
                return true;
            }
        }

        string ISymbolNode.MangledName
        {
            get
            {
                StringBuilder nameBuilder = new StringBuilder();
                nameBuilder.Append(NodeFactory.NameMangler.CompilationUnitPrefix + "__GCStaticEEType_");
                int totalSize = 0;
                foreach (int run in _runLengths)
                {
                    nameBuilder.Append(run.ToStringInvariant());
                    nameBuilder.Append("_");
                    totalSize += run;
                }
                nameBuilder.Append(totalSize.ToStringInvariant());
                nameBuilder.Append("_");

                return nameBuilder.ToString();
            }
        }

        int ISymbolNode.Offset
        {
            get
            {
                if (NumSeries > 0)
                {
                    return _targetPointerSize * ((NumSeries * 2) + 1);
                }
                else
                {
                    return 0;
                }
            }
        }

        private int NumSeries
        {
            get
            {
                return (_runLengths.Length - 1) / 2;
            }
        }

        public override ObjectData GetData(NodeFactory factory, bool relocsOnly)
        {
            ObjectDataBuilder dataBuilder = new ObjectDataBuilder(factory);
            dataBuilder.Alignment = 16;
            dataBuilder.DefinedSymbols.Add(this);

            bool hasPointers = NumSeries > 0;
            if (hasPointers)
            {
                for (int i = ((_runLengths.Length / 2) * 2) - 1; i >= 0; i--)
                {
                    if (_targetPointerSize == 4)
                    {
                        dataBuilder.EmitInt(_runLengths[i]);
                    }
                    else
                    {
                        dataBuilder.EmitLong(_runLengths[i]);
                    }
                }
                if (_targetPointerSize == 4)
                {
                    dataBuilder.EmitInt(NumSeries);
                }
                else
                {
                    dataBuilder.EmitLong(NumSeries);
                }
            }

            int totalSize = 0;
            foreach (int run in _runLengths)
            {
                totalSize += run * _targetPointerSize;
            }

            dataBuilder.EmitShort(0); // ComponentSize is always 0

            if (hasPointers)
                dataBuilder.EmitShort(0x20); // TypeFlags.HasPointers
            else
                dataBuilder.EmitShort(0x00);

            totalSize = Math.Max(totalSize, _targetPointerSize * 3); // minimum GC eetype size is 3 pointers
            dataBuilder.EmitInt(totalSize);

            return dataBuilder.ToObjectData();
        }
    }
}
