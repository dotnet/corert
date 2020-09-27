// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using System.Runtime.InteropServices;
using Internal.TypeSystem;
using Internal.TypeSystem.Ecma;

namespace Internal.Runtime.Interpreter
{
    internal enum StaticsKind { NonGcStatics, GcStatics, ThreadNonGcStatics, ThreadGcStatics }

    internal class StaticsRegion
    {
        public DefType OwningType { get; private set; }

        public IntPtr Base { get; private set; }

        public StaticsRegion(DefType type, int size)
        {
            OwningType = type;
            Base = Marshal.AllocHGlobal(size);
        }
    }

    internal class StaticsRegionHashTable : LockFreeReaderHashtable<EcmaType, StaticsRegion>
    {
        private readonly StaticsKind _staticsKind;

        public StaticsRegionHashTable(StaticsKind staticsKind)
        {
            _staticsKind = staticsKind;
        }

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
            return _staticsKind switch
            {
                StaticsKind.GcStatics => new StaticsRegion(key, layout.GcStatics.Size.AsInt),
                StaticsKind.ThreadGcStatics => new StaticsRegion(key, layout.ThreadGcStatics.Size.AsInt),
                StaticsKind.ThreadNonGcStatics => new StaticsRegion(key, layout.ThreadNonGcStatics.Size.AsInt),
                _ => new StaticsRegion(key, layout.NonGcStatics.Size.AsInt),
            };
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
