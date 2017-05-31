// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System;
using System.Diagnostics;
using System.Reflection;
using System.Reflection.Metadata;
using System.Runtime.CompilerServices;

using Internal.TypeSystem;

namespace Internal.TypeSystem.Ecma
{
    public sealed class EcmaField : FieldDesc
    {
        [Flags]
        enum FieldFlags
        {
            BasicMetadataCache  = 0x01,
            Static              = 0x02,
            InitOnly            = 0x04,
        };

        EcmaType _type;
        FieldDefinitionHandle _handle;

        TypeDesc _fieldType;
        FieldFlags _fieldFlags;

        internal EcmaField(EcmaType type, FieldDefinitionHandle handle)
        {
            _type = type;
            _handle = handle;
        }

        public override TypeSystemContext Context
        {
            get
            {
                return _type.Module.Context;
            }
        }

        public override MetadataType OwningType
        {
            get
            {
                return _type;
            }
        }

        public EcmaModule Module
        {
            get
            {
                return _type.Module;
            }
        }

        public MetadataReader MetadataReader
        {
            get
            {
                return _type.MetadataReader;
            }
        }

        public FieldDefinitionHandle Handle
        {
            get
            {
                return _handle;
            }
        }
        
        public FieldDefinition FieldDefinition
        {
            get
            {
                return this.MetadataReader.GetFieldDefinition(_handle);
            }
        }

        void ComputeFieldType()
        {
            var metadataReader = this.Module.MetadataReader;
            BlobReader signatureReader = metadataReader.GetBlobReader(metadataReader.GetFieldDefinition(_handle).Signature);

            EcmaSignatureParser parser = new EcmaSignatureParser(this.Module, signatureReader);
            _fieldType = parser.ParseFieldSignature();
        }

        public override TypeDesc FieldType
        {
            get
            {
                if (_fieldType == null)
                    ComputeFieldType();
                return _fieldType;
            }
        }

        [MethodImpl(MethodImplOptions.NoInlining)]
        private FieldFlags InitializeFieldFlags(FieldFlags mask)
        {
            FieldFlags flags = 0;

            if ((mask & FieldFlags.BasicMetadataCache) != 0)
            {
                var fieldDefinition = this.MetadataReader.GetFieldDefinition(_handle);

                var fieldAttributes = fieldDefinition.Attributes;
                if ((fieldAttributes & FieldAttributes.Static) != 0)
                    flags |= FieldFlags.Static;

                if ((fieldAttributes & FieldAttributes.InitOnly) != 0)
                    flags |= FieldFlags.InitOnly;

                flags |= FieldFlags.BasicMetadataCache;
            }

            Debug.Assert((flags & mask) != 0);
            _fieldFlags |= flags;

            return flags & mask;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private FieldFlags GetFieldFlags(FieldFlags mask)
        {
            FieldFlags flags = _fieldFlags & mask;
            if (flags != 0)
                return flags;
            return InitializeFieldFlags(mask);
        }

        public override bool IsStatic
        {
            get
            {
                return (GetFieldFlags(FieldFlags.BasicMetadataCache | FieldFlags.Static) & FieldFlags.Static) != 0;
            }
        }

        public override bool IsInitOnly
        {
            get
            {
                return (GetFieldFlags(FieldFlags.BasicMetadataCache | FieldFlags.InitOnly) & FieldFlags.InitOnly) != 0;
            }
        }

        public FieldAttributes Attributes
        {
            get
            {
                var fieldDefinition = this.MetadataReader.GetFieldDefinition(_handle);
                return fieldDefinition.Attributes;
            }
        }

        public override string Name
        {
            get
            {
                var metadataReader = this.MetadataReader;
                var fieldDefinition = metadataReader.GetFieldDefinition(_handle);
                return metadataReader.GetString(fieldDefinition.Name);
            }
        }

        public override string ToString()
        {
            return _type.ToString() + "." + this.Name;
        }
    }
}
