// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using Debug = System.Diagnostics.Debug;

namespace Internal.TypeSystem
{
    partial struct GCPointerMap
    {
        public static GCPointerMap FromInstanceLayout(DefType type)
        {
            Debug.Assert(type.ContainsPointers);

            GCPointerMapBuilder builder = new GCPointerMapBuilder(type.InstanceByteCount, type.Context.Target.PointerSize);
            FromInstanceLayoutHelper(ref builder, type);

            return builder.ToGCMap();
        }

        private static void FromInstanceLayoutHelper(ref GCPointerMapBuilder builder, DefType type)
        {
            foreach (FieldDesc field in type.GetFields())
            {
                if (field.IsStatic)
                    continue;

                TypeDesc fieldType = field.FieldType;
                if (fieldType.IsObjRef)
                {
                    builder.MarkGCPointer(field.Offset);
                }
                else if (fieldType.IsValueType)
                {
                    var fieldDefType = (DefType)fieldType;
                    if (fieldDefType.ContainsPointers)
                    {
                        GCPointerMapBuilder innerBuilder =
                            builder.GetInnerBuilder(field.Offset, fieldDefType.InstanceByteCount);
                        FromInstanceLayoutHelper(ref innerBuilder, fieldDefType);
                    }
                }
            }
        }
    }
}