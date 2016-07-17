// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Text;

using Debug = System.Diagnostics.Debug;

namespace Internal.TypeSystem
{
    /// <summary>
    /// Provides services to convert types to strings.
    /// </summary>
    public abstract class TypeNameFormatter
    {
        public void AppendName(StringBuilder sb, TypeDesc type)
        {
            if (type.GetType() == typeof(SignatureTypeVariable))
            {
                AppendName(sb, (SignatureTypeVariable)type);
                return;
            }

            if (type.GetType() == typeof(SignatureMethodVariable))
            {
                AppendName(sb, (SignatureMethodVariable)type);
                return;
            }

            switch (type.Category)
            {
                case TypeFlags.Array:
                case TypeFlags.SzArray:
                    AppendName(sb, (ArrayType)type);
                    return;
                case TypeFlags.ByRef:
                    AppendName(sb, (ByRefType)type);
                    return;
                case TypeFlags.Pointer:
                    AppendName(sb, (PointerType)type);
                    return;
                case TypeFlags.GenericParameter:
                    AppendName(sb, (GenericParameterDesc)type);
                    return;
                default:
                    if (type.GetType() == typeof(InstantiatedType))
                        AppendName(sb, (InstantiatedType)type);
                    else if (type is MetadataType)
                        AppendName(sb, (MetadataType)type);
                    else
                    {
                        Debug.Assert(type is NoMetadata.NoMetadataType);
                        AppendName(sb, (NoMetadata.NoMetadataType)type);
                    }
                    return;
            }
        }

        public void AppendName(StringBuilder sb, MetadataType type)
        {
            MetadataType containingType = type.ContainingType;
            if (containingType != null)
                AppendNameForNestedType(sb, type, containingType);
            else
                AppendNameForNamespaceType(sb, type);
        }

        public virtual void AppendName(StringBuilder sb, NoMetadata.NoMetadataType type)
        {
            // Name formatters that can deal with NoMetadata types need to override.
            throw new NotSupportedException();
        }

        public abstract void AppendName(StringBuilder sb, ArrayType type);
        public abstract void AppendName(StringBuilder sb, ByRefType type);
        public abstract void AppendName(StringBuilder sb, PointerType type);
        public abstract void AppendName(StringBuilder sb, InstantiatedType type);
        public abstract void AppendName(StringBuilder sb, GenericParameterDesc type);
        public abstract void AppendName(StringBuilder sb, SignatureMethodVariable type);
        public abstract void AppendName(StringBuilder sb, SignatureTypeVariable type);

        protected abstract void AppendNameForNestedType(StringBuilder sb, MetadataType nestedType, MetadataType containingType);
        protected abstract void AppendNameForNamespaceType(StringBuilder sb, MetadataType type);

        #region Convenience methods

        public string FormatName(TypeDesc type)
        {
            StringBuilder sb = new StringBuilder();
            AppendName(sb, type);
            return sb.ToString();
        }

        public string FormatName(MetadataType type)
        {
            StringBuilder sb = new StringBuilder();
            AppendName(sb, type);
            return sb.ToString();
        }

        public string FormatName(NoMetadata.NoMetadataType type)
        {
            StringBuilder sb = new StringBuilder();
            AppendName(sb, type);
            return sb.ToString();
        }

        public string FormatName(ArrayType type)
        {
            StringBuilder sb = new StringBuilder();
            AppendName(sb, type);
            return sb.ToString();
        }

        public string FormatName(ByRefType type)
        {
            StringBuilder sb = new StringBuilder();
            AppendName(sb, type);
            return sb.ToString();
        }

        public string FormatName(PointerType type)
        {
            StringBuilder sb = new StringBuilder();
            AppendName(sb, type);
            return sb.ToString();
        }

        public string FormatName(InstantiatedType type)
        {
            StringBuilder sb = new StringBuilder();
            AppendName(sb, type);
            return sb.ToString();
        }

        public string FormatName(GenericParameterDesc type)
        {
            StringBuilder sb = new StringBuilder();
            AppendName(sb, type);
            return sb.ToString();
        }

        public string FormatName(SignatureMethodVariable type)
        {
            StringBuilder sb = new StringBuilder();
            AppendName(sb, type);
            return sb.ToString();
        }

        public string FormatName(SignatureTypeVariable type)
        {
            StringBuilder sb = new StringBuilder();
            AppendName(sb, type);
            return sb.ToString();
        }

        #endregion
    }
}
