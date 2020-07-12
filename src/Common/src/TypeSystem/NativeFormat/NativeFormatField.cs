// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.


using System;
using System.Diagnostics;
using System.Reflection;
using Internal.Metadata.NativeFormat;
using System.Runtime.CompilerServices;

using Internal.TypeSystem;

namespace Internal.TypeSystem.NativeFormat
{
    public sealed partial class NativeFormatField : FieldDesc, NativeFormatMetadataUnit.IHandleObject
    {
        private static class FieldFlags
        {
            public const int BasicMetadataCache = 0x0001;
            public const int Static = 0x0002;
            public const int InitOnly = 0x0004;
            public const int Literal = 0x0008;
            public const int HasRva = 0x0010;

            public const int AttributeMetadataCache = 0x0100;
            public const int ThreadStatic = 0x0200;
            public const int Intrinsic = 0x0400;
        }

        private NativeFormatType _type;
        private FieldHandle _handle;

        // Cached values
        private ThreadSafeFlags _fieldFlags;
        private TypeDesc _fieldType;
        private string _name;

        internal NativeFormatField(NativeFormatType type, FieldHandle handle)
        {
            _type = type;
            _handle = handle;

#if DEBUG
            // Initialize name eagerly in debug builds for convenience
            InitializeName();
#endif
        }

        Handle NativeFormatMetadataUnit.IHandleObject.Handle
        {
            get
            {
                return _handle;
            }
        }

        NativeFormatType NativeFormatMetadataUnit.IHandleObject.Container
        {
            get
            {
                return _type;
            }
        }

        public override TypeSystemContext Context
        {
            get
            {
                return _type.Module.Context;
            }
        }

        public override DefType OwningType
        {
            get
            {
                return _type;
            }
        }

        public NativeFormatModule NativeFormatModule
        {
            get
            {
                return _type.NativeFormatModule;
            }
        }

        public MetadataReader MetadataReader
        {
            get
            {
                return _type.MetadataReader;
            }
        }

        public NativeFormatMetadataUnit MetadataUnit
        {
            get
            {
                return _type.MetadataUnit;
            }
        }

        public FieldHandle Handle
        {
            get
            {
                return _handle;
            }
        }

        private TypeDesc InitializeFieldType()
        {
            var metadataReader = MetadataReader;

            NativeFormatSignatureParser parser = new NativeFormatSignatureParser(MetadataUnit, metadataReader.GetField(_handle).Signature, metadataReader);
            var fieldType = parser.ParseFieldSignature();
            return (_fieldType = fieldType);
        }

        public override TypeDesc FieldType
        {
            get
            {
                if (_fieldType == null)
                    return InitializeFieldType();
                return _fieldType;
            }
        }

        [MethodImpl(MethodImplOptions.NoInlining)]
        private int InitializeFieldFlags(int mask)
        {
            int flags = 0;

            if ((mask & FieldFlags.BasicMetadataCache) != 0)
            {
                var fieldAttributes = Attributes;

                if ((fieldAttributes & FieldAttributes.Static) != 0)
                    flags |= FieldFlags.Static;

                if ((fieldAttributes & FieldAttributes.InitOnly) != 0)
                    flags |= FieldFlags.InitOnly;

                if ((fieldAttributes & FieldAttributes.Literal) != 0)
                    flags |= FieldFlags.Literal;

                if ((fieldAttributes & FieldAttributes.HasFieldRVA) != 0)
                    flags |= FieldFlags.HasRva;

                flags |= FieldFlags.BasicMetadataCache;
            }

            // Fetching custom attribute based properties is more expensive, so keep that under
            // a separate cache that might not be accessed very frequently.
            if ((mask & FieldFlags.AttributeMetadataCache) != 0)
            {
                var metadataReader = this.MetadataReader;
                var fieldDefinition = metadataReader.GetField(_handle);

                foreach (var attributeHandle in fieldDefinition.CustomAttributes)
                {
                    ConstantStringValueHandle nameHandle;
                    string namespaceName;
                    if (!metadataReader.GetAttributeNamespaceAndName(attributeHandle, out namespaceName, out nameHandle))
                        continue;

                    if (nameHandle.StringEquals("ThreadStaticAttribute", metadataReader)
                        && namespaceName.Equals("System"))
                    {
                        flags |= FieldFlags.ThreadStatic;
                    }
                    else if (nameHandle.StringEquals("IntrinsicAttribute", metadataReader)
                        && namespaceName.Equals("System.Runtime.CompilerServices"))
                    {
                        flags |= FieldFlags.Intrinsic;
                    }
                }

                flags |= FieldFlags.AttributeMetadataCache;
            }

            Debug.Assert((flags & mask) != 0);

            _fieldFlags.AddFlags(flags);

            Debug.Assert((flags & mask) != 0);
            return flags & mask;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private int GetFieldFlags(int mask)
        {
            int flags = _fieldFlags.Value & mask;
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

        public override bool IsThreadStatic
        {
            get
            {
                return IsStatic &&
                    (GetFieldFlags(FieldFlags.AttributeMetadataCache | FieldFlags.ThreadStatic) & FieldFlags.ThreadStatic) != 0;
            }
        }

        public override bool IsInitOnly
        {
            get
            {
                return (GetFieldFlags(FieldFlags.BasicMetadataCache | FieldFlags.InitOnly) & FieldFlags.InitOnly) != 0;
            }
        }

        public override bool HasRva
        {
            get
            {
                return (GetFieldFlags(FieldFlags.BasicMetadataCache | FieldFlags.HasRva) & FieldFlags.HasRva) != 0;
            }
        }

        public override bool IsLiteral
        {
            get
            {
                return (GetFieldFlags(FieldFlags.BasicMetadataCache | FieldFlags.Literal) & FieldFlags.Literal) != 0;
            }
        }

        public FieldAttributes Attributes
        {
            get
            {
                return MetadataReader.GetField(_handle).Flags;
            }
        }

        private string InitializeName()
        {
            var metadataReader = MetadataReader;
            var name = metadataReader.GetString(metadataReader.GetField(_handle).Name);
            return (_name = name);
        }

        public override string Name
        {
            get
            {
                if (_name == null)
                    return InitializeName();
                return _name;
            }
        }

        public override bool HasCustomAttribute(string attributeNamespace, string attributeName)
        {
            return MetadataReader.HasCustomAttribute(MetadataReader.GetField(_handle).CustomAttributes,
                attributeNamespace, attributeName);
        }
    }
}
