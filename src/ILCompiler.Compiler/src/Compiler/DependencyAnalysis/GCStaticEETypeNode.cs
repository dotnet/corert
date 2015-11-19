﻿// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace ILCompiler.DependencyAnalysis
{
    class GCStaticEETypeNode : ObjectNode, ISymbolNode
    {
        int[] _runLengths; // First is offset to first gc field, second is length of gc static run, third is length of non-gc data, etc
        int _targetPointerSize;

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
        }

        public override string GetName()
        {
            return ((ISymbolNode)this).MangledName;
        }

        public override string Section
        {
            get
            {
                return "data";
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
                nameBuilder.Append("__GCStaticEEType_");
                int totalSize = 0;
                foreach (int run in _runLengths)
                {
                    nameBuilder.Append(run.ToString(CultureInfo.InvariantCulture));
                    nameBuilder.Append("_");
                    totalSize += run;
                }
                nameBuilder.Append(totalSize.ToString(CultureInfo.InvariantCulture));
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
