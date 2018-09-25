// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Collections.Generic;
using System.Reflection.Metadata;
using System.Reflection.Metadata.Ecma335;

using Internal.TypeSystem;
using Internal.TypeSystem.Ecma;

namespace ILCompiler
{
    internal class ReadyToRunMetadataFieldLayoutAlgorithm : MetadataFieldLayoutAlgorithm
    {
        /// <summary>
        /// Map from EcmaModule instances to field layouts within the individual modules.
        /// </summary>
        private ModuleFieldLayoutMap _moduleFieldLayoutMap;

        public ReadyToRunMetadataFieldLayoutAlgorithm(TargetDetails target)
        {
            _moduleFieldLayoutMap = new ModuleFieldLayoutMap(target);
        }

        public override ComputedStaticFieldLayout ComputeStaticFieldLayout(DefType defType, StaticLayoutKind layoutKind)
        {
            ComputedStaticFieldLayout layout = new ComputedStaticFieldLayout();
            if (defType is EcmaType ecmaType)
            {
                // ECMA types are the only ones that can have statics
                ModuleFieldLayout moduleFieldLayout = _moduleFieldLayoutMap.GetOrCreateValue(ecmaType.EcmaModule);
                layout.GcStatics = moduleFieldLayout.GcStatics;
                layout.NonGcStatics = moduleFieldLayout.NonGcStatics;
                layout.ThreadGcStatics = moduleFieldLayout.ThreadGcStatics;
                layout.ThreadNonGcStatics = moduleFieldLayout.ThreadNonGcStatics;
                moduleFieldLayout.TypeToFieldMap.TryGetValue(defType, out layout.Offsets);
            }
            return layout;
        }

        /// <summary>
        /// Map from modules to their static field layouts.
        /// </summary>
        private class ModuleFieldLayoutMap : LockFreeReaderHashtable<EcmaModule, ModuleFieldLayout>
        {
            /// <summary>
            /// <a href="https://github.com/dotnet/coreclr/blob/master/src/vm/class.h#L64">MAX_LOG2_PRIMITIVE_FIELD_SIZE</a>
            /// </summary>
            private const int MaxLog2PrimitiveFieldSize = 3;

            /// <summary>
            /// <a href="https://github.com/dotnet/coreclr/blob/master/src/vm/class.h#L66">MAX_PRIMITIVE_FIELD_SIZE</a>
            /// </summary>
            private const int MaxPrimitiveFieldSize = 1 << MaxLog2PrimitiveFieldSize;

            /// <summary>
            /// CoreCLR DomainLocalModule::OffsetOfDataBlob() / sizeof(void *)
            /// </summary>
            private const int DomainLocalModuleDataBlobOffsetAsIntPtrCount = 6;

            /// <summary>
            /// CoreCLR ThreadLocalModule::OffsetOfDataBlob() / sizeof(void *)
            /// </summary>
            private const int ThreadLocalModuleDataBlobOffsetAsIntPtrCount = 3;

            /// <summary>
            /// TargetDetails is used to query pointer size for the target architecture.
            /// </summary>
            private TargetDetails _target;

            public ModuleFieldLayoutMap(TargetDetails target)
            {
                _target = target;
            }

            protected override bool CompareKeyToValue(EcmaModule key, ModuleFieldLayout value)
            {
                return key == value.Module;
            }

            protected override bool CompareValueToValue(ModuleFieldLayout value1, ModuleFieldLayout value2)
            {
                return value1.Module == value2.Module;
            }

            protected override ModuleFieldLayout CreateValueFromKey(EcmaModule module)
            {
                int typeCountInModule = module.MetadataReader.GetTableRowCount(TableIndex.TypeDef);

                // 0 corresponds to "normal" statics, 1 to thread-local statics
                LayoutInt[] gcStatics = new LayoutInt[2]
                {
                    LayoutInt.Zero,
                    LayoutInt.Zero
                };
                LayoutInt[] nonGcStatics = new LayoutInt[2]
                {
                    new LayoutInt(DomainLocalModuleDataBlobOffsetAsIntPtrCount * _target.PointerSize + typeCountInModule),
                    new LayoutInt(ThreadLocalModuleDataBlobOffsetAsIntPtrCount * _target.PointerSize + typeCountInModule),
                };
                Dictionary<DefType, FieldAndOffset[]> typeToFieldMap = new Dictionary<DefType, FieldAndOffset[]>();

                for (int typeIndex = 1; typeIndex <= typeCountInModule; typeIndex++)
                {
                    List<FieldAndOffset> fieldsForType = null;
                    DefType defType = (DefType)module.GetObject(MetadataTokens.TypeDefinitionHandle(typeIndex));
                    if (defType.HasInstantiation)
                    {
                        // Generic types are exempt from the static field layout algorithm, see
                        // <a href="https://github.com/dotnet/coreclr/blob/master/src/vm/ceeload.cpp#L2049">this check</a>.
                        continue;
                    }
                    foreach (FieldDesc field in defType.GetFields())
                    {
                        if (field.IsStatic && !field.IsLiteral)
                        {
                            int index = (field.IsThreadStatic ? 1 : 0);
                            int alignment = 1;
                            int size = 0;
                            bool isGcField = false;

                            switch (field.FieldType.UnderlyingType.Category)
                            {
                                case TypeFlags.Byte:
                                case TypeFlags.SByte:
                                case TypeFlags.Boolean:
                                    size = 1;
                                    break;

                                case TypeFlags.Int16:
                                case TypeFlags.UInt16:
                                case TypeFlags.Char:
                                    alignment = 2;
                                    size = 2;
                                    break;

                                case TypeFlags.Int32:
                                case TypeFlags.UInt32:
                                case TypeFlags.Single:
                                    alignment = 4;
                                    size = 4;
                                    break;

                                case TypeFlags.FunctionPointer:
                                case TypeFlags.Pointer:
                                case TypeFlags.IntPtr:
                                case TypeFlags.UIntPtr:
                                    alignment = _target.PointerSize;
                                    size = _target.PointerSize;
                                    break;

                                case TypeFlags.Int64:
                                case TypeFlags.UInt64:
                                case TypeFlags.Double:
                                    alignment = 8;
                                    size = 8;
                                    break;

                                case TypeFlags.GenericParameter:
                                case TypeFlags.SzArray:
                                case TypeFlags.Array:
                                case TypeFlags.Class:
                                case TypeFlags.Interface:
                                    isGcField = true;
                                    alignment = _target.PointerSize;
                                    size = _target.PointerSize;
                                    break;

                                case TypeFlags.ValueType:
                                    isGcField = true;
                                    alignment = _target.PointerSize;
                                    size = _target.PointerSize;
                                    if (field.FieldType is EcmaType fieldEcmaType && fieldEcmaType.EcmaModule != module)
                                    {
                                        // Allocate pessimistic non-GC area for cross-module fields as that's what CoreCLR does
                                        // <a href="https://github.com/dotnet/coreclr/blob/master/src/vm/ceeload.cpp#L2124">here</a>
                                        nonGcStatics[index] = LayoutInt.AlignUp(nonGcStatics[index], new LayoutInt(MaxPrimitiveFieldSize)) + new LayoutInt(MaxPrimitiveFieldSize);
                                    }
                                    break;

                                default:
                                    throw new NotImplementedException(field.FieldType.Category.ToString());
                            }

                            LayoutInt[] layout = (isGcField ? gcStatics : nonGcStatics);
                            LayoutInt offset = LayoutInt.AlignUp(layout[index], new LayoutInt(alignment));
                            layout[index] = offset + new LayoutInt(size);
                            if (fieldsForType == null)
                            {
                                fieldsForType = new List<FieldAndOffset>();
                            }
                            fieldsForType.Add(new FieldAndOffset(field, offset));
                        }
                    }
                    if (fieldsForType != null)
                    {
                        typeToFieldMap.Add(defType, fieldsForType.ToArray());
                    }
                }

                LayoutInt blockAlignment = new LayoutInt(MaxPrimitiveFieldSize);

                return new ModuleFieldLayout(
                    module,
                    gcStatics: new StaticsBlock() { Size = gcStatics[0], LargestAlignment = blockAlignment },
                    nonGcStatics: new StaticsBlock() { Size = nonGcStatics[0], LargestAlignment = blockAlignment },
                    threadGcStatics: new StaticsBlock() { Size = gcStatics[1], LargestAlignment = blockAlignment },
                    threadNonGcStatics: new StaticsBlock() { Size = nonGcStatics[1], LargestAlignment = blockAlignment },
                    typeToFieldMap: typeToFieldMap);

                throw new NotImplementedException();
            }

            protected override int GetKeyHashCode(EcmaModule key)
            {
                return key.GetHashCode();
            }

            protected override int GetValueHashCode(ModuleFieldLayout value)
            {
                return value.Module.GetHashCode();
            }
        }

        /// <summary>
        /// Field layouts for a given EcmaModule.
        /// </summary>
        private class ModuleFieldLayout
        {
            public EcmaModule Module { get; }

            public StaticsBlock GcStatics { get; }

            public StaticsBlock NonGcStatics { get;  }

            public StaticsBlock ThreadGcStatics { get;  }

            public StaticsBlock ThreadNonGcStatics { get;  }

            public Dictionary<DefType, FieldAndOffset[]> TypeToFieldMap { get; }

            public ModuleFieldLayout(
                EcmaModule module, 
                StaticsBlock gcStatics, 
                StaticsBlock nonGcStatics, 
                StaticsBlock threadGcStatics, 
                StaticsBlock threadNonGcStatics,
                Dictionary<DefType, FieldAndOffset[]> typeToFieldMap)
            {
                Module = module;
                GcStatics = gcStatics;
                NonGcStatics = nonGcStatics;
                ThreadGcStatics = threadGcStatics;
                ThreadNonGcStatics = threadNonGcStatics;
                TypeToFieldMap = typeToFieldMap;
            }
        }
    }
}
