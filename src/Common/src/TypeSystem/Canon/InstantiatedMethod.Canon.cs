﻿// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using Debug = System.Diagnostics.Debug;

namespace Internal.TypeSystem
{
    // Implements generic method canonicalization
    partial class InstantiatedMethod
    {
        /// <summary>
        /// Stores a cached version of the canonicalized form of this method since
        /// calculating it is a recursive operation
        /// </summary>
        InstantiatedMethod _specificCanonCache = null;
        InstantiatedMethod _universalCanonCache = null;

        /// <summary>
        /// Returns the result of canonicalizing this method over the given kind of Canon
        /// </summary>
        public override MethodDesc GetCanonMethodTarget(CanonicalFormKind kind)
        {
            InstantiatedMethod canonicalMethodResult = GetCachedCanonValue(kind);
            if (canonicalMethodResult == null)
            {
                bool needsChange;
                Instantiation canonInstantiation = CanonUtilites.ConvertInstantiationToCanonForm(Instantiation, kind, out needsChange);
                if (needsChange)
                {
                    MethodDesc openMethodOnCanonicalizedType = _methodDef.GetCanonMethodTarget(kind);
                    canonicalMethodResult = Context.GetInstantiatedMethod(openMethodOnCanonicalizedType, canonInstantiation);
                }
                else
                {
                    canonicalMethodResult = this;
                }

                SetCachedCanonValue(kind, canonicalMethodResult);
            }
            
            return canonicalMethodResult;
        }

        InstantiatedMethod GetCachedCanonValue(CanonicalFormKind kind)
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

        void SetCachedCanonValue(CanonicalFormKind kind, InstantiatedMethod value)
        {
            switch(kind)
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
        /// True if either the containing type instantiation or any of this method's generic arguments
        /// are canonical
        /// </summary>
        public override bool IsCanonicalMethod(CanonicalFormKind policy)
        {
            if (OwningType.HasInstantiation && OwningType.IsCanonicalSubtype(policy))
            {
                return true;
            }

            foreach (TypeDesc type in Instantiation)
            {
                if (type.IsCanonicalSubtype(policy))
                {
                    return true;
                }
            }

            return false;
        }
    }
}