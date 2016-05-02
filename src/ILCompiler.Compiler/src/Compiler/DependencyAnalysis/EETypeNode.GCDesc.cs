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
                    else
                    {
                        if (elementType.IsPointer)
                        {
                            return 0;
                        }
                        else if (elementType.IsByRef)
                        {
                            throw new BadImageFormatException();
                        }
                        else
                        {
                            var defType = (DefType)elementType;
                            if (defType.ContainsPointers)
                            {
                                int numSeries = GCPointerMap.FromInstanceLayout(defType).NumSeries;
                                Debug.Assert(numSeries > 0);
                                return (numSeries + 2) * _type.Context.Target.PointerSize;
                            }
                            return 0;
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
                    return 0;
                }
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
                if (elementType.IsObjRef)
                {
                    OutputStandardGCDesc(ref builder,
                        new GCPointerMap(new[] { 1 << 2 }, 3),
                        4 * _type.Context.Target.PointerSize, 0);
                }
                else
                {
                    if (elementType.IsPointer)
                    {
                        // Nothing to output here. Unmanaged pointers don't point to the GC heap.
                    }
                    else if (elementType.IsByRef)
                    {
                        throw new BadImageFormatException();
                    }
                    else
                    {
                        var elementDefType = (DefType)elementType;
                        if (elementDefType.ContainsPointers)
                        {
                            OutputArrayGCDesc(ref builder, GCPointerMap.FromInstanceLayout(elementDefType));
                        }
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
                    int allocationSize = defType.InstanceByteCount + offs + _type.Context.Target.PointerSize;

                    OutputStandardGCDesc(ref builder, GCPointerMap.FromInstanceLayout(defType), allocationSize, offs);
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

        private void OutputArrayGCDesc(ref ObjectDataBuilder builder, GCPointerMap map)
        {
            int numSeries = 0;
            int leadingNonPointerCount = 0;

            int pointerSize = _type.Context.Target.PointerSize;

            for (int cellIndex = 0; cellIndex < map.Size; cellIndex++)
            {
                if (map[cellIndex])
                    break;
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
            builder.EmitNaturalInt((2 + leadingNonPointerCount) * pointerSize);
            builder.EmitNaturalInt(-numSeries);
        }
        
    }
}
