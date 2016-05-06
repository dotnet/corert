// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Text;

using Internal.Runtime;
using Internal.TypeSystem;

using Debug = System.Diagnostics.Debug;

namespace ILCompiler.DependencyAnalysis
{
    /// <summary>
    /// Represents a subset of <see cref="EETypeNode"/> that is used to describe GC static field regions for
    /// types. It only fills out enough pieces of the EEType structure so that the GC can operate on it. Runtime should
    /// never see these.
    /// </summary>
    internal class GCStaticEETypeNode : ObjectNode, ISymbolNode
    {
        private GCPointerMap _gcMap;
        private TargetDetails _target;

        public GCStaticEETypeNode(TargetDetails target, GCPointerMap gcMap)
        {
            _gcMap = gcMap;
            _target = target;
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
                nameBuilder.Append("__GCStaticEEType_");
                nameBuilder.Append(_gcMap.ToString());

                return nameBuilder.ToString();
            }
        }

        int ISymbolNode.Offset
        {
            get
            {
                return ((_gcMap.NumSeries * 2) + 1) * _target.PointerSize;
            }
        }

        public override bool ShouldShareNodeAcrossModules(NodeFactory factory)
        {
            return true;
        }

        public override ObjectData GetData(NodeFactory factory, bool relocsOnly)
        {
            ObjectDataBuilder dataBuilder = new ObjectDataBuilder(factory);
            dataBuilder.Alignment = 16;
            dataBuilder.DefinedSymbols.Add(this);

            int totalSize = _gcMap.Size * _target.PointerSize;

            GCDescEncoder.EncodeStandardGCDesc(ref dataBuilder, _gcMap, totalSize, 0);

            Debug.Assert(dataBuilder.CountBytes == ((ISymbolNode)this).Offset);

            dataBuilder.EmitShort(0); // ComponentSize is always 0

            dataBuilder.EmitShort((short)EETypeFlags.HasPointersFlag);

            totalSize = Math.Max(totalSize, _target.PointerSize * 3); // minimum GC eetype size is 3 pointers
            dataBuilder.EmitInt(totalSize);

            // This is just so that EEType::Validate doesn't blow up at runtime
            dataBuilder.EmitPointerReloc(this); // Related type: itself

            return dataBuilder.ToObjectData();
        }
    }
}
