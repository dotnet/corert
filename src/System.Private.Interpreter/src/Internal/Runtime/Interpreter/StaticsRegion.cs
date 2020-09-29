// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using System.Diagnostics;
using System.Runtime.CompilerServices;
using Internal.Runtime.Augments;
using Internal.Runtime.TypeLoader;
using Internal.TypeSystem;
using Internal.TypeSystem.Ecma;

namespace Internal.Runtime.Interpreter
{
    internal class StaticsRegion
    {
        public readonly EcmaType OwningType;

        public readonly StaticClassConstructionContext StaticConstructorContext;

        public readonly byte[] NonGcStaticsBase;

        public readonly byte[] GcStaticsBase;

        public readonly byte[] ThreadStaticsBase;

        public StaticsRegion(EcmaType type, ComputedStaticFieldLayout layout)
        {
            OwningType = type;
            NonGcStaticsBase = new byte[layout.NonGcStatics.Size.AsInt];
            GcStaticsBase = new byte[layout.GcStatics.Size.AsInt];
            ThreadStaticsBase = new byte[layout.ThreadNonGcStatics.Size.AsInt + layout.ThreadGcStatics.Size.AsInt];

            if (RuntimeAugments.HasCctor(type.GetRuntimeTypeHandle()))
            {
                TypeLoaderEnvironment.TryGetMethodAddressFromMethodDesc(type.GetStaticConstructor(), out IntPtr methodAddress, out _, out TypeLoaderEnvironment.MethodAddressType foundAddressType);
                Debug.Assert(methodAddress != IntPtr.Zero && foundAddressType == TypeLoaderEnvironment.MethodAddressType.Exact);
                StaticConstructorContext = new StaticClassConstructionContext { cctorMethodAddress = methodAddress };
            }
        }
    }

    internal class StaticsRegionHashTable : LockFreeReaderHashtable<EcmaType, StaticsRegion>
    {
        protected override bool CompareKeyToValue(EcmaType key, StaticsRegion value)
        {
            return GetFullyQualifiedTypeName(key) == GetFullyQualifiedTypeName(value.OwningType);
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

        protected string GetFullyQualifiedTypeName(EcmaType type)
        {
            string module = type.Module.ToString() ?? string.Empty;
            return $"{module}!{type.GetFullName()}";
        }
    }
}
