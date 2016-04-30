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
        private ISymbolNode _gcdesc;

        private ISymbolNode GCDesc
        {
            get
            {
                if (GCDescSize == 0)
                    return null;
                if (_gcdesc == null)
                {
                    System.Threading.Interlocked.CompareExchange(ref _gcdesc,
                        new ObjectAndOffsetSymbolNode(this, 0, "__GCDesc_" + NodeFactory.NameMangler.GetMangledTypeName(_type)), null);
                }
                return _gcdesc;
            }
        }

        private int GCDescSize
        {
            get
            {
                if (!_constructed || _type.IsGenericDefinition)
                    return 0;

                GCPointerMap map = GetGCPointerMap();
                if (!map.IsInitialized)
                    return 0;

                int numSeries = 0;
                for (int i = 0; i < map.Size; i++)
                {
                    if (map[i])
                    {
                        numSeries++;
                        while (++i < map.Size && map[i]) ;
                    }
                }

                if (_type.IsArray || _type.IsSzArray)
                {
                    return numSeries > 0 ? (numSeries + 2) * _type.Context.Target.PointerSize : 0;
                }
                else
                {
                    return numSeries > 0 ? (numSeries * 2 + 1) * _type.Context.Target.PointerSize : 0;
                }
            }
        }

        private GCPointerMap GetGCPointerMap()
        {
            switch (_type.Category)
            {
                case TypeFlags.SzArray:
                case TypeFlags.Array:
                    {
                        var elementType = ((ArrayType)_type).ElementType;
                        if (elementType.IsObjRef)
                        {
                            return new GCPointerMap(new int[] { 0x1 }, 1);
                        }
                        else if (elementType.IsPointer)
                        {
                            return default(GCPointerMap);
                        }
                        else if (elementType.IsByRef)
                        {
                            throw new BadImageFormatException();
                        }
                        else
                        {
                            Debug.Assert(elementType.IsValueType);
                            var elementDefType = (DefType)elementType;
                            if (!elementDefType.ContainsPointers)
                                return default(GCPointerMap);

                            return GCPointerMap.FromInstanceLayout(elementDefType);
                        }
                    }

                default:
                    {
                        Debug.Assert(_type.IsDefType);
                        var defType = (DefType)_type;
                        if (!defType.ContainsPointers)
                            return default(GCPointerMap);

                        return GCPointerMap.FromInstanceLayout(defType);
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

            GCPointerMap map = GetGCPointerMap();
            if (!map.IsInitialized)
            {
                Debug.Assert(GCDescSize == 0);
                return;
            }

            builder.DefinedSymbols.Add(GCDesc);

            int initialBuilderPosition = builder.CountBytes;

            switch (_type.Category)
            {
                case TypeFlags.Array:
                case TypeFlags.SzArray:
                    OutputArrayGCDesc(ref builder, map);
                    break;

                default:
                    Debug.Assert(_type.IsDefType);
                    var defType = (DefType)_type;

                    // Computing the layout for the boxed version if this is a value type.
                    int offs = defType.IsValueType ? _type.Context.Target.PointerSize : 0;

                    // Include syncblock
                    int allocationSize = defType.InstanceByteCount + offs + _type.Context.Target.PointerSize;

                    OutputStandardGCDesc(ref builder, map, allocationSize, offs);
                    break;
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
                    while (cellIndex > 0 && map[--cellIndex])
                    {
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
