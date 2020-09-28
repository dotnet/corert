// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using Internal.TypeSystem;
using Internal.TypeSystem.Ecma;

namespace Internal.Runtime.Interpreter
{
    internal class StaticsRegion
    {
        public DefType OwningType { get; private set; }

        public byte[] NonGcStaticsBase { get; private set; }

        public byte[] GcStaticsBase { get; private set; }

        public byte[] ThreadStaticsBase { get; private set; }

        public StaticsRegion(DefType type, ComputedStaticFieldLayout layout)
        {
            OwningType = type;
            NonGcStaticsBase = new byte[layout.NonGcStatics.Size.AsInt];
            GcStaticsBase = new byte[layout.GcStatics.Size.AsInt];
            ThreadStaticsBase = new byte[layout.ThreadNonGcStatics.Size.AsInt + layout.ThreadGcStatics.Size.AsInt];
        }
    }

    internal class StaticsRegionHashTable : LockFreeReaderHashtable<EcmaType, StaticsRegion>
    {
        protected override bool CompareKeyToValue(EcmaType key, StaticsRegion value)
        {
            return key == value.OwningType;
        }

        protected override bool CompareValueToValue(StaticsRegion value1, StaticsRegion value2)
        {
            return Object.ReferenceEquals(value1, value2);
        }

        protected override StaticsRegion CreateValueFromKey(EcmaType key)
        {
            var fieldLayoutAlgorithm = key.Context.GetLayoutAlgorithmForType(key);
            var layout = fieldLayoutAlgorithm.ComputeStaticFieldLayout(key, StaticLayoutKind.StaticRegionSizes);
            return new StaticsRegion(key, layout);
        }

        protected override int GetKeyHashCode(EcmaType key)
        {
            return key.GetHashCode();
        }

        protected override int GetValueHashCode(StaticsRegion value)
        {
            return value.GetHashCode();
        }
    }
}
