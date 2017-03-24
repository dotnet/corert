// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Collections.Generic;
using Debug = System.Diagnostics.Debug;

namespace Internal.TypeSystem.Interop
{
    public class NativeStructType : MetadataType
    {
        // The managed struct that this type will imitate
        public MetadataType ManagedStructType
        {
            get;
        }

        public override ModuleDesc Module
        {
            get;
        }

        public override string Name
        {
            get
            {
                return "__NativeType__" + ManagedStructType.Name;
            }
        }

        public override string Namespace
        {
            get
            {
                return "Internal.CompilerGenerated";
            }
        }

        public override Instantiation Instantiation
        {
            get
            {
                return ManagedStructType.Instantiation;
            }
        }

        public override PInvokeStringFormat PInvokeStringFormat
        {
            get
            {
                return ManagedStructType.PInvokeStringFormat;
            }
        }

        public override bool IsExplicitLayout
        {
            get
            {
                return ManagedStructType.IsExplicitLayout;
            }
        }

        public override bool IsSequentialLayout
        {
            get
            {
                return ManagedStructType.IsSequentialLayout;
            }
        }

        public override bool IsBeforeFieldInit
        {
            get
            {
                return ManagedStructType.IsBeforeFieldInit;
            }
        }

        public override MetadataType MetadataBaseType
        {
            get
            {
                return (MetadataType)Context.GetWellKnownType(WellKnownType.ValueType);
            }
        }

        public override bool IsSealed
        {
            get
            {
                return true;
            }
        }

        public override bool IsAbstract
        {
            get
            {
                return false;
            }
        }

        public override DefType ContainingType
        {
            get
            {
                return null;
            }
        }

        public override DefType[] ExplicitlyImplementedInterfaces
        {
            get
            {
                return Array.Empty<DefType>();
            }
        }

        public override TypeSystemContext Context
        {
            get
            {
                return ManagedStructType.Context;
            }
        }

        private NativeStructField[] _fields;
        private InteropStateManager _interopStateManager;


        public NativeStructType(ModuleDesc owningModule, MetadataType managedStructType, InteropStateManager interopStateManager)
        {
            Debug.Assert(managedStructType.IsTypeDefinition);
            Debug.Assert(managedStructType.IsValueType);
            Debug.Assert(!managedStructType.IsGenericDefinition);

            Module = owningModule;
            ManagedStructType = managedStructType;
            _interopStateManager = interopStateManager;
            CalculateFields();
        }

        private void CalculateFields()
        {
            bool isSequential = ManagedStructType.IsSequentialLayout;
            MarshalAsDescriptor[] marshalAsDescriptors = ManagedStructType.GetFieldMarshalAsDescriptors();
            bool isAnsi = ((MetadataType)ManagedStructType).PInvokeStringFormat == PInvokeStringFormat.AnsiClass;

            int numFields = 0;
            foreach (FieldDesc field in ManagedStructType.GetFields())
            {
                if (field.IsStatic)
                {
                    continue;
                }
                numFields++;
            }

            _fields = new NativeStructField[numFields];

            int index = 0;
            foreach (FieldDesc field in ManagedStructType.GetFields())
            {
                if (field.IsStatic)
                {
                    continue;
                }

                var managedType = field.FieldType;

                TypeDesc nativeType;
                try
                {
                    nativeType = MarshalHelpers.GetNativeStructFieldType(managedType, marshalAsDescriptors[index], _interopStateManager, isAnsi);
                }
                catch (NotSupportedException)
                {
                    // if marshalling is not supported for this type the generates stubs will emit appropriate
                    // error message. We just set native type to be same as managedtype
                    nativeType = managedType;
                }

                _fields[index++] = new NativeStructField(nativeType, field, this, field.Name);
            }
        }

        public override ClassLayoutMetadata GetClassLayout()
        {
            ClassLayoutMetadata layout = ManagedStructType.GetClassLayout();

            ClassLayoutMetadata result;
            result.PackingSize = layout.PackingSize;
            result.Size = layout.Size;

            if (IsExplicitLayout)
            {
                result.Offsets = new FieldAndOffset[layout.Offsets.Length];

                Debug.Assert(layout.Offsets.Length <= _fields.Length);

                int layoutIndex = 0;
                for (int index = 0; index < _fields.Length; index++)
                {
                    if (_fields[index].Name == layout.Offsets[layoutIndex].Field.Name)
                    {
                        result.Offsets[layoutIndex] = new FieldAndOffset(_fields[index], layout.Offsets[layoutIndex].Offset);
                        layoutIndex++;
                    }
                }

                Debug.Assert(layoutIndex == layout.Offsets.Length);
            }
            else
            {
                result.Offsets = null;
            }

            return result;
        }

        public override bool HasCustomAttribute(string attributeNamespace, string attributeName)
        {
            return false;
        }

        public override IEnumerable<MetadataType> GetNestedTypes()
        {
            return Array.Empty<MetadataType>();
        }

        public override MetadataType GetNestedType(string name)
        {
            return null;
        }

        protected override MethodImplRecord[] ComputeVirtualMethodImplsForType()
        {
            return Array.Empty<MethodImplRecord>();
        }

        public override MethodImplRecord[] FindMethodsImplWithMatchingDeclName(string name)
        {
            return Array.Empty<MethodImplRecord>();
        }

        public override int GetHashCode()
        {
            var hashCodeBuilder = new Internal.NativeFormat.TypeHashingAlgorithms.HashCodeBuilder(Namespace);

            if (Namespace.Length > 0)
            {
                hashCodeBuilder.Append(".");
            }

            hashCodeBuilder.Append(Name);
            return hashCodeBuilder.ToHashCode();
        }

        protected override TypeFlags ComputeTypeFlags(TypeFlags mask)
        {
            TypeFlags flags = 0;

            if ((mask & TypeFlags.HasGenericVarianceComputed) != 0)
            {
                flags |= TypeFlags.HasGenericVarianceComputed;
            }

            if ((mask & TypeFlags.CategoryMask) != 0)
            {
                flags |= TypeFlags.ValueType;
            }

            return flags;
        }

        public override IEnumerable<FieldDesc> GetFields()
        {
            return _fields;
        }

        /// <summary>
        /// Synthetic field on <see cref="NativeStructType"/>.
        /// </summary>
        private class NativeStructField : FieldDesc
        {
            private FieldDesc _managedField;
            private TypeDesc _fieldType;
            private NativeStructType _owningType;

            private string _name;

            public override TypeSystemContext Context
            {
                get
                {
                    return _owningType.Context;
                }
            }

            public override TypeDesc FieldType
            {
                get
                {
                    return _fieldType;
                }
            }

            public override bool HasRva
            {
                get
                {
                    return false;
                }
            }


            public override bool IsInitOnly
            {
                get
                {
                    return _managedField.IsInitOnly;
                }
            }

            public override bool IsLiteral
            {
                get
                {
                    return false;
                }
            }

            public override bool IsStatic
            {
                get
                {
                    return false;
                }
            }

            public override bool IsThreadStatic
            {
                get
                {
                    return false;
                }
            }

            public override DefType OwningType
            {
                get
                {
                    return _owningType;
                }
            }

            public override bool HasCustomAttribute(string attributeNamespace, string attributeName)
            {
                return false;
            }

            public override string Name
            {
                get
                {
                    return _name;
                }
            }

            public NativeStructField(TypeDesc nativeType, FieldDesc managedField, NativeStructType owningType, string name)
            {
                _fieldType = nativeType;
                _managedField = managedField;
                _owningType = owningType;
                _name = name;
            }
        }
    }
}