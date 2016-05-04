// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using Debug = System.Diagnostics.Debug;

namespace Internal.TypeSystem
{
    partial struct GCPointerMap
    {
        /// <summary>
        /// Computes the GC pointer map for the instance fields of <paramref name="type"/>.
        /// </summary>
        public static GCPointerMap FromInstanceLayout(DefType type)
        {
            Debug.Assert(type.ContainsGCPointers);

            GCPointerMapBuilder builder = new GCPointerMapBuilder(type.InstanceByteCount, type.Context.Target.PointerSize);
            FromInstanceLayoutHelper(ref builder, type);

            return builder.ToGCMap();
        }

        /// <summary>
        /// Computes the GC pointer map of the GC static region of the type.
        /// </summary>
        public static GCPointerMap FromStaticLayout(DefType type)
        {
            GCPointerMapBuilder builder = new GCPointerMapBuilder(type.GCStaticFieldSize, type.Context.Target.PointerSize);

            foreach (FieldDesc field in type.GetFields())
            {
                if (!field.IsStatic || field.HasRva || field.IsLiteral
                    || !field.HasGCStaticBase || field.IsThreadStatic)
                    continue;

                TypeDesc fieldType = field.FieldType;
                if (fieldType.IsGCPointer)
                {
                    builder.MarkGCPointer(field.Offset);
                }
                else
                {
                    Debug.Assert(fieldType.IsValueType);
                    var fieldDefType = (DefType)fieldType;
                    Debug.Assert(fieldDefType.ContainsGCPointers);

                    GCPointerMapBuilder innerBuilder =
                        builder.GetInnerBuilder(field.Offset, fieldDefType.InstanceByteCount);
                    FromInstanceLayoutHelper(ref innerBuilder, fieldDefType);
                }
            }

            return builder.ToGCMap();
        }

        /// <summary>
        /// Computes the GC pointer map of the thread static region of the type.
        /// </summary>
        public static GCPointerMap FromThreadStaticLayout(DefType type)
        {
            GCPointerMapBuilder builder = new GCPointerMapBuilder(type.ThreadStaticFieldSize, type.Context.Target.PointerSize);

            foreach (FieldDesc field in type.GetFields())
            {
                if (!field.IsStatic || field.HasRva || field.IsLiteral || !field.IsThreadStatic)
                    continue;

                TypeDesc fieldType = field.FieldType;
                if (fieldType.IsGCPointer)
                {
                    builder.MarkGCPointer(field.Offset);
                }
                else if (fieldType.IsValueType)
                {
                    var fieldDefType = (DefType)fieldType;
                    if (fieldDefType.ContainsGCPointers)
                    {
                        GCPointerMapBuilder innerBuilder =
                            builder.GetInnerBuilder(field.Offset, fieldDefType.InstanceByteCount);
                        FromInstanceLayoutHelper(ref innerBuilder, fieldDefType);
                    }
                }
            }

            return builder.ToGCMap();
        }

        private static void FromInstanceLayoutHelper(ref GCPointerMapBuilder builder, DefType type)
        {
            if (!type.IsValueType && type.HasBaseType)
            {
                DefType baseType = type.BaseType;
                GCPointerMapBuilder baseLayoutBuilder = builder.GetInnerBuilder(0, baseType.InstanceByteCount);
                FromInstanceLayoutHelper(ref baseLayoutBuilder, baseType);
            }

            foreach (FieldDesc field in type.GetFields())
            {
                if (field.IsStatic)
                    continue;

                TypeDesc fieldType = field.FieldType;
                if (fieldType.IsGCPointer)
                {
                    builder.MarkGCPointer(field.Offset);
                }
                else if (fieldType.IsValueType)
                {
                    var fieldDefType = (DefType)fieldType;
                    if (fieldDefType.ContainsGCPointers)
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