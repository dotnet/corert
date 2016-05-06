// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Collections.Generic;
using System.Text;

using Internal.TypeSystem;
using Internal.Runtime;

using Debug = System.Diagnostics.Debug;

namespace ILCompiler.DependencyAnalysis
{
    internal class GCStaticEETypeNode : ObjectNode, ISymbolNode
    {
        private bool[] _gcDesc;
        private int _targetPointerSize;
        private TargetDetails _target;

        public GCStaticEETypeNode(bool[] gcDesc, NodeFactory factory)
        {
            _gcDesc = gcDesc;
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
                foreach (bool run in _gcDesc)
                {
                    nameBuilder.Append(run ? '1' : '0');
                    totalSize++;
                }
                nameBuilder.Append("_");
                nameBuilder.Append(totalSize.ToStringInvariant());


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
                int numSeries = 0;
                for (int i = 0; i < _gcDesc.Length; i++)
                {
                    if (_gcDesc[i])
                    {
                        numSeries++;
                        while (++i < _gcDesc.Length && _gcDesc[i]) ;
                    }
                }
                return numSeries;
            }
        }

        public override ObjectData GetData(NodeFactory factory, bool relocsOnly)
        {
            ObjectDataBuilder dataBuilder = new ObjectDataBuilder(factory);
            dataBuilder.Alignment = 16;
            dataBuilder.DefinedSymbols.Add(this);

            int pointerSize = factory.Target.PointerSize;

            int numSeries = 0;

            for (int cellIndex = _gcDesc.Length - 1; cellIndex >= 0; cellIndex--)
            {
                if (_gcDesc[cellIndex])
                {
                    numSeries++;

                    int seriesSize = pointerSize;

                    while (cellIndex > 0 && _gcDesc[cellIndex - 1])
                    {
                        seriesSize += pointerSize;
                        cellIndex--;
                    }

                    dataBuilder.EmitNaturalInt(seriesSize - _gcDesc.Length * pointerSize);
                    dataBuilder.EmitNaturalInt(cellIndex * pointerSize);
                }
            }

            Debug.Assert(numSeries > 0);
            dataBuilder.EmitNaturalInt(numSeries);

            Debug.Assert(((ISymbolNode)this).Offset == dataBuilder.CountBytes);

            dataBuilder.EmitShort(0); // ComponentSize is always 0

            dataBuilder.EmitShort((short)(EETypeFlags.HasPointersFlag | EETypeFlags.IsInterfaceFlag));

            int totalSize = Math.Max(_gcDesc.Length * pointerSize, _targetPointerSize * 3); // minimum GC eetype size is 3 pointers
            dataBuilder.EmitInt(totalSize);

            return dataBuilder.ToObjectData();
        }
    }
}
