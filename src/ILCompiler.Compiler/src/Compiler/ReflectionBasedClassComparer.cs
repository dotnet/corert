// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;

using Internal.TypeSystem;

using TypeHashingAlgorithms = Internal.NativeFormat.TypeHashingAlgorithms;

namespace ILCompiler
{
    internal struct ReflectionBasedClassComparer : ITypeSystemClassComparer
    {
        private class BoxedInt
        {
            public Type Key { get; }
            public int Value { get; }
            public BoxedInt(Type key, int value) { Key = key; Value = value; }
        }

        private class ClassIDHashTable : LockFreeReaderHashtable<Type, BoxedInt>
        {
            private int ComputeHashCode(Type type)
            {
                string fullName = type.FullName;
                return TypeHashingAlgorithms.ComputeNameHashCode(fullName);
            }

            protected override bool CompareKeyToValue(Type key, BoxedInt value) => key == value.Key;
            protected override bool CompareValueToValue(BoxedInt v1, BoxedInt v2) => v1.Key == v2.Key;
            protected override int GetKeyHashCode(Type key) => ComputeHashCode(key);
            protected override int GetValueHashCode(BoxedInt value) => value.Value;
            protected override BoxedInt CreateValueFromKey(Type key)
            {
                return new BoxedInt(key, ComputeHashCode(key));
            }
        }

        private ClassIDHashTable _assignedIds;

        public ReflectionBasedClassComparer(int dummy = 0)
        {
            _assignedIds = new ClassIDHashTable();
        }

        public int CompareClasses(TypeDesc x, TypeDesc y)
        {
            int keyX = _assignedIds.GetOrCreateValue(x.GetType()).Value;
            int keyY = _assignedIds.GetOrCreateValue(y.GetType()).Value;
            return keyX - keyY;
        }
    }
}
