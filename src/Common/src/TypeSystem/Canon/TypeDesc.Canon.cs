// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using Debug = System.Diagnostics.Debug;

namespace Internal.TypeSystem
{
    // Implements canonicalization for types
    partial class TypeDesc
    {
        /// <summary>
        /// Stores a cached version of the canonicalized form of this type since
        /// calculating it is a recursive operation
        /// </summary>
        TypeDesc _specificCanonCache = null;
        TypeDesc _universalCanonCache = null;
        TypeDesc GetCachedCanonValue(CanonicalFormKind kind)
        {
            switch (kind)
            {
                case CanonicalFormKind.Specific:
                    return _specificCanonCache;

                case CanonicalFormKind.Universal:
                    return _universalCanonCache;

                default:
                    Debug.Fail("Invalid CanonicalFormKind: " + kind);
                    return null;
            }
        }

        void SetCachedCanonValue(CanonicalFormKind kind, TypeDesc value)
        {
            switch (kind)
            {
                case CanonicalFormKind.Specific:
                    Debug.Assert(_specificCanonCache == null || _specificCanonCache == value);
                    _specificCanonCache = value;
                    break;

                case CanonicalFormKind.Universal:
                    Debug.Assert(_universalCanonCache == null || _universalCanonCache == value);
                    _universalCanonCache = value;
                    break;

                default:
                    Debug.Fail("Invalid CanonicalFormKind: " + kind);
                    break;
            }
        }

        /// <summary>
        /// Returns the canonical form of this type
        /// </summary>
        public TypeDesc ConvertToCanonForm(CanonicalFormKind kind)
        {
            TypeDesc canonForm = GetCachedCanonValue(kind);
            if (canonForm == null)
            {
                canonForm = ConvertToCanonFormImpl(kind);
                SetCachedCanonValue(kind, canonForm);
            }

            return canonForm;
        }

        /// <summary>
        /// Derived types that override this should convert their generic parameters to canonical ones
        /// </summary>
        protected abstract TypeDesc ConvertToCanonFormImpl(CanonicalFormKind kind);

        /// <summary>
        /// Returns true if this type matches the discovery policy or if it's parameterized over one that does
        /// </summary>
        public abstract bool IsCanonicalSubtype(CanonicalFormKind policy);
    }
}
