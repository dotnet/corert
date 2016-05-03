// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;

using Internal.TypeSystem;

using Debug = System.Diagnostics.Debug;

namespace ILCompiler.DependencyAnalysis
{
    partial class EETypeNode
    {
        private int GCDescSize
        {
            get
            {
                if (!_constructed || _type.IsGenericDefinition)
                    return 0;

                if (_type.IsArray)
                {
                    TypeDesc elementType = ((ArrayType)_type).ElementType;
                    if (elementType.IsObjRef)
                    {
                        // For efficiency this is special cased and encoded as one serie.
                        return 3 * _type.Context.Target.PointerSize;
                    }
                    else if (elementType.IsDefType)
                    {
                        var defType = (DefType)elementType;
                        if (defType.ContainsPointers)
                        {
                            int numSeries = GCPointerMap.FromInstanceLayout(defType).NumSeries;
                            Debug.Assert(numSeries > 0);
                            return (numSeries + 2) * _type.Context.Target.PointerSize;
                        }
                    }
                }
                else
                {
                    var defType = (DefType)_type;
                    if (defType.ContainsPointers)
                    {
                        int numSeries = GCPointerMap.FromInstanceLayout(defType).NumSeries;
                        Debug.Assert(numSeries > 0);
                        return (numSeries * 2 + 1) * _type.Context.Target.PointerSize;
                    }
                }

                return 0;
            }
        }

        private void OutputGCDesc(ref ObjectDataBuilder builder)
        {
            if (!_constructed || _type.IsGenericDefinition)
            {
                Debug.Assert(GCDescSize == 0);
                return;
            }

            int initialBuilderPosition = builder.CountBytes;

            if (_type.IsArray)
            {
                TypeDesc elementType = ((ArrayType)_type).ElementType;

                // 2 means m_pEEType and _numComponents. Syncblock is sort of appended at the end of the object layout in this case.
                int baseSize = 2 * _type.Context.Target.PointerSize;

                if (!_type.IsSzArray)
                {
                    // Multi-dim arrays include upper and lower bounds for each rank
                    baseSize += 2 * _type.Context.GetWellKnownType(WellKnownType.Int32).GetElementSize() * ((ArrayType)_type).Rank;
                }

                if (elementType.IsObjRef)
                {
                    // TODO: this optimization can be also applied to all element types that have all '1' GCPointerMap
                    //       get_GCDescSize needs to be updated appropriately when this optimization is enabled
                    OutputStandardGCDesc(ref builder,
                        new GCPointerMap(new[] { 1 }, 1),
                        4 * _type.Context.Target.PointerSize,
                        baseSize);
                }
                else if (elementType.IsDefType)
                {
                    var elementDefType = (DefType)elementType;
                    if (elementDefType.ContainsPointers)
                    {
                        OutputArrayGCDesc(ref builder, GCPointerMap.FromInstanceLayout(elementDefType), baseSize);
                    }
                }
            }
            else
            {
                var defType = (DefType)_type;
                if (defType.ContainsPointers)
                {
                    // Computing the layout for the boxed version if this is a value type.
                    int offs = defType.IsValueType ? _type.Context.Target.PointerSize : 0;

                    // Include syncblock
                    int objectSize = defType.InstanceByteCount + offs + _type.Context.Target.PointerSize;

                    OutputStandardGCDesc(ref builder, GCPointerMap.FromInstanceLayout(defType), objectSize, offs);
                }
            }

            Debug.Assert(initialBuilderPosition + GCDescSize == builder.CountBytes);
        }

        private void OutputStandardGCDesc(ref ObjectDataBuilder builder, GCPointerMap map, int size, int delta)
        {
            Debug.Assert(size >= map.Size);

            int pointerSize = _type.Context.Target.PointerSize;

            int numSeries = 0;

            for (int cellIndex = map.Size - 1; cellIndex >= 0; cellIndex--)
            {
                if (map[cellIndex])
                {
                    numSeries++;

                    int seriesSize = pointerSize;

                    while (cellIndex > 0 && map[cellIndex - 1])
                    {
                        seriesSize += pointerSize;
                        cellIndex--;
                    }

                    builder.EmitNaturalInt(seriesSize - size);
                    builder.EmitNaturalInt(cellIndex * pointerSize + delta);
                }
            }

            Debug.Assert(numSeries > 0);
            builder.EmitNaturalInt(numSeries);
        }

        private void OutputArrayGCDesc(ref ObjectDataBuilder builder, GCPointerMap map, int size)
        {
            int numSeries = 0;
            int leadingNonPointerCount = 0;

            int pointerSize = _type.Context.Target.PointerSize;

            for (int cellIndex = 0; cellIndex < map.Size && !map[cellIndex]; cellIndex++)
            {
                leadingNonPointerCount++;
            }

            int nonPointerCount = leadingNonPointerCount;

            for (int cellIndex = map.Size - 1; cellIndex >= leadingNonPointerCount; cellIndex--)
            {
                if (map[cellIndex])
                {
                    numSeries++;

                    short pointerCount = 1;
                    while (cellIndex > leadingNonPointerCount && map[cellIndex - 1])
                    {
                        cellIndex--;
                        checked { pointerCount++; }
                    }

                    builder.EmitHalfNaturalInt(pointerCount);
                    builder.EmitHalfNaturalInt(checked((short)(nonPointerCount * pointerSize)));
                    nonPointerCount = 0;
                }
                else
                {
                    nonPointerCount++;
                }
            }

            Debug.Assert(numSeries > 0);
            builder.EmitNaturalInt(size + leadingNonPointerCount * pointerSize);
            builder.EmitNaturalInt(-numSeries);
        }
    }
}
