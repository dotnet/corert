// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;

using Debug = System.Diagnostics.Debug;

namespace Internal.TypeSystem
{
    /// <summary>
    /// Provides services to convert types to strings.
    /// </summary>
    public abstract class TypeNameFormatter
    {
        public string FormatName(TypeDesc type)
        {
            if (type.GetType() == typeof(SignatureTypeVariable))
                return FormatName((SignatureTypeVariable)type);

            if (type.GetType() == typeof(SignatureMethodVariable))
                return FormatName((SignatureMethodVariable)type);

            switch (type.Category)
            {
                case TypeFlags.Array:
                case TypeFlags.SzArray:
                    return FormatName((ArrayType)type);

                case TypeFlags.ByRef:
                    return FormatName((ByRefType)type);

                case TypeFlags.Pointer:
                    return FormatName((PointerType)type);

                case TypeFlags.GenericParameter:
                    return FormatName((GenericParameterDesc)type);

                default:
                    if (type.GetType() == typeof(InstantiatedType))
                        return FormatName((InstantiatedType)type);
                    else if (type is MetadataType)
                        return FormatName((MetadataType)type);
                    else
                    {
                        Debug.Assert(type is NoMetadata.NoMetadataType);
                        return FormatName((NoMetadata.NoMetadataType)type);
                    }
            }
        }

        public string FormatName(MetadataType type)
        {
            MetadataType containingType = type.ContainingType;
            if (containingType != null)
                return FormatNameForNestedType(containingType, type);
            return FormatNameForNamespaceType(type);
        }

        public virtual string FormatName(NoMetadata.NoMetadataType type)
        {
            // Name formatters that can deal with NoMetadata types need to override.
            throw new NotSupportedException();
        }

        public abstract string FormatName(ArrayType type);
        public abstract string FormatName(ByRefType type);
        public abstract string FormatName(PointerType type);
        public abstract string FormatName(InstantiatedType type);
        public abstract string FormatName(GenericParameterDesc type);
        public abstract string FormatName(SignatureMethodVariable type);
        public abstract string FormatName(SignatureTypeVariable type);

        protected abstract string FormatNameForNestedType(MetadataType containingType, MetadataType nestedType);
        protected abstract string FormatNameForNamespaceType(MetadataType type);
    }
}
