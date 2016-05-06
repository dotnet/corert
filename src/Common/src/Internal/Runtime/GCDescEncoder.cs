// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using Internal.TypeSystem;

using Debug = System.Diagnostics.Debug;

namespace Internal.Runtime
{
    /// <summary>
    /// Utility class for encoding GCDescs. GCDesc is a runtime structure used by the
    /// garbage collector that describes the GC layout of a memory region.
    /// </summary>
    public struct GCDescEncoder
    {
        /// <summary>
        /// Retrieves size of the GCDesc that describes the instance GC layout for the given type.
        /// </summary>
        public static int GetGCDescSize(TypeDesc type)
        {
            if (type.IsArray)
            {
                TypeDesc elementType = ((ArrayType)type).ElementType;
                if (elementType.IsGCPointer)
                {
                    // For efficiency this is special cased and encoded as one serie.
                    return 3 * type.Context.Target.PointerSize;
                }
                else if (elementType.IsDefType)
                {
                    var defType = (DefType)elementType;
                    if (defType.ContainsGCPointers)
                    {
                        int numSeries = GCPointerMap.FromInstanceLayout(defType).NumSeries;
                        Debug.Assert(numSeries > 0);
                        return (numSeries + 2) * type.Context.Target.PointerSize;
                    }
                }
            }
            else
            {
                var defType = (DefType)type;
                if (defType.ContainsGCPointers)
                {
                    int numSeries = GCPointerMap.FromInstanceLayout(defType).NumSeries;
                    Debug.Assert(numSeries > 0);
                    return (numSeries * 2 + 1) * type.Context.Target.PointerSize;
                }
            }

            return 0;
        }

        public static void EncodeGCDesc<T>(ref T builder, TypeDesc type)
            where T: struct, ITargetBinaryWriter
        {
            int initialBuilderPosition = builder.CountBytes;

            if (type.IsArray)
            {
                TypeDesc elementType = ((ArrayType)type).ElementType;

                // 2 means m_pEEType and _numComponents. Syncblock is sort of appended at the end of the object layout in this case.
                int baseSize = 2 * builder.TargetPointerSize;

                if (!type.IsSzArray)
                {
                    // Multi-dim arrays include upper and lower bounds for each rank
                    baseSize += 2 * type.Context.GetWellKnownType(WellKnownType.Int32).GetElementSize() * ((ArrayType)type).Rank;
                }

                if (elementType.IsGCPointer)
                {
                    // TODO: this optimization can be also applied to all element types that have all '1' GCPointerMap
                    //       get_GCDescSize needs to be updated appropriately when this optimization is enabled
                    EncodeStandardGCDesc(ref builder,
                        new GCPointerMap(new[] { 1 }, 1),
                        4 * builder.TargetPointerSize,
                        baseSize);
                }
                else if (elementType.IsDefType)
                {
                    var elementDefType = (DefType)elementType;
                    if (elementDefType.ContainsGCPointers)
                    {
                        EncodeArrayGCDesc(ref builder, GCPointerMap.FromInstanceLayout(elementDefType), baseSize);
                    }
                }
            }
            else
            {
                var defType = (DefType)type;
                if (defType.ContainsGCPointers)
                {
                    // Computing the layout for the boxed version if this is a value type.
                    int offs = defType.IsValueType ? builder.TargetPointerSize : 0;

                    // Include syncblock
                    int objectSize = defType.InstanceByteCount + offs + builder.TargetPointerSize;

                    EncodeStandardGCDesc(ref builder, GCPointerMap.FromInstanceLayout(defType), objectSize, offs);
                }
            }

            Debug.Assert(initialBuilderPosition + GetGCDescSize(type) == builder.CountBytes);
        }

        public static void EncodeStandardGCDesc<T>(ref T builder, GCPointerMap map, int size, int delta)
            where T: struct, ITargetBinaryWriter
        {
            Debug.Assert(size >= map.Size);

            int pointerSize = builder.TargetPointerSize;

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

        private static void EncodeArrayGCDesc<T>(ref T builder, GCPointerMap map, int size)
            where T : struct, ITargetBinaryWriter
        {
            int numSeries = 0;
            int leadingNonPointerCount = 0;

            int pointerSize = builder.TargetPointerSize;

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