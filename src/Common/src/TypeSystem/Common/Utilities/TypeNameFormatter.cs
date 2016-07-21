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
                case TypeFlags.SignatureTypeVariable:
                    AppendName(sb, (SignatureTypeVariable)type);
                    return;
                case TypeFlags.SignatureMethodVariable:
                    AppendName(sb, (SignatureMethodVariable)type);
                    return;
                default:
                    Debug.Assert(type.IsDefType);
                    AppendName(sb, (DefType)type);
                    return;
            }
        }

        public void AppendName(StringBuilder sb, DefType type)
        {
            if (!type.IsTypeDefinition)
            {
                AppendNameForInstantiatedType(sb, type);
            }
            else
            {
                DefType containingType = type.ContainingType;
                if (containingType != null)
                    AppendNameForNestedType(sb, type, containingType);
                else
                    AppendNameForNamespaceType(sb, type);
            }
        }

        public abstract void AppendName(StringBuilder sb, ArrayType type);
        public abstract void AppendName(StringBuilder sb, ByRefType type);
        public abstract void AppendName(StringBuilder sb, PointerType type);
        public abstract void AppendName(StringBuilder sb, GenericParameterDesc type);
        public abstract void AppendName(StringBuilder sb, SignatureMethodVariable type);
        public abstract void AppendName(StringBuilder sb, SignatureTypeVariable type);

        protected abstract void AppendNameForNestedType(StringBuilder sb, DefType nestedType, DefType containingType);
        protected abstract void AppendNameForNamespaceType(StringBuilder sb, DefType type);
        protected abstract void AppendNameForInstantiatedType(StringBuilder sb, DefType type);

        #region Convenience methods

        public string FormatName(TypeDesc type)
        {
            StringBuilder sb = new StringBuilder();
            AppendName(sb, type);
            return sb.ToString();
        }

        public string FormatName(DefType type)
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
            AppendNameForInstantiatedType(sb, type);
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
