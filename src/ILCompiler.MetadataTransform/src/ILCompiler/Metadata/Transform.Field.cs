// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System;
using System.Collections.Generic;

using Internal.Metadata.NativeFormat.Writer;

using Cts = Internal.TypeSystem;

using Debug = System.Diagnostics.Debug;
using FieldAttributes = System.Reflection.FieldAttributes;

namespace ILCompiler.Metadata
{
    public partial class Transform<TPolicy>
    {
        private EntityMap<Cts.FieldDesc, MetadataRecord> _fields =
            new EntityMap<Cts.FieldDesc, MetadataRecord>(EqualityComparer<Cts.FieldDesc>.Default);

        private Action<Cts.FieldDesc, Field> _initFieldDef;
        private Action<Cts.FieldDesc, MemberReference> _initFieldRef;

        private MetadataRecord HandleField(Cts.FieldDesc field)
        {
            MetadataRecord rec;

            if (_policy.GeneratesMetadata(field))
            {
                rec = HandleFieldDefinition(field);
            }
            else
            {
                rec = _fields.GetOrCreate(field, _initFieldRef ?? (_initFieldRef = InitializeFieldReference));
            }

            Debug.Assert(rec is Field || rec is MemberReference);

            return rec;
        }

        private Field HandleFieldDefinition(Cts.FieldDesc field)
        {
            Debug.Assert(field.GetTypicalFieldDefinition() == field);
            Debug.Assert(_policy.GeneratesMetadata(field));
            return (Field)_fields.GetOrCreate(field, _initFieldDef ?? (_initFieldDef = InitializeFieldDefinition));
        }

        private void InitializeFieldDefinition(Cts.FieldDesc entity, Field record)
        {
            record.Name = HandleString(entity.Name);
            record.Signature = new FieldSignature
            {
                Type = HandleType(entity.FieldType),
                // TODO: CustomModifiers
            };
            record.Flags = GetFieldAttributes(entity);

            // TODO: CustomAttributes
            // TODO: DefaultValue
            // TODO: Offset
        }

        private void InitializeFieldReference(Cts.FieldDesc entity, MemberReference record)
        {
            record.Name = HandleString(entity.Name);
            record.Parent = HandleType(entity.OwningType);
            record.Signature = new FieldSignature
            {
                Type = HandleType(entity.FieldType),
                // TODO: CustomModifiers
            };
        }

        private FieldAttributes GetFieldAttributes(Cts.FieldDesc field)
        {
            FieldAttributes result;

            var ecmaField = field as Cts.Ecma.EcmaField;
            if (ecmaField != null)
            {
                var fieldDefinition = ecmaField.MetadataReader.GetFieldDefinition(ecmaField.Handle);
                result = fieldDefinition.Attributes;
            }
            else
            {
                result = 0;

                if (field.IsStatic)
                    result |= FieldAttributes.Static;
                if (field.IsInitOnly)
                    result |= FieldAttributes.InitOnly;
                if (field.IsLiteral)
                    result |= FieldAttributes.Literal;
                if (field.HasRva)
                    result |= FieldAttributes.HasFieldRVA;

                // Not set: Visibility, NotSerialized, SpecialName, RTSpecialName, HasFieldMarshal, HasDefault
            }

            return result;
        }
    }
}
