// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;

using Internal.TypeSystem;

using Debug = System.Diagnostics.Debug;

namespace ILCompiler.DependencyAnalysis
{
    public struct GenericLookupDescriptor : IEquatable<GenericLookupDescriptor>
    {
        public readonly TypeSystemEntity CanonicalOwner;

        public readonly DictionaryEntry Signature;

        public GenericLookupDescriptor(TypeSystemEntity canonicalOwner, DictionaryEntry signature)
        {
            // Owner should be a canonical type or canonical method
            Debug.Assert((
                canonicalOwner is TypeDesc &&
                    ((TypeDesc)canonicalOwner).IsCanonicalSubtype(CanonicalFormKind.Any))
                || (canonicalOwner is MethodDesc &&
                    ((MethodDesc)canonicalOwner).HasInstantiation && ((MethodDesc)canonicalOwner).IsSharedByGenericInstantiations));

            CanonicalOwner = canonicalOwner;
            Signature = signature;
        }

        public bool Equals(GenericLookupDescriptor other)
        {
            if (CanonicalOwner != other.CanonicalOwner)
                return false;

            if (!Signature.Equals(other.Signature))
                return false;

            return true;
        }

        public override bool Equals(object obj)
        {
            return obj is GenericLookupDescriptor && Equals((GenericLookupDescriptor)obj);
        }

        public override int GetHashCode()
        {
            int hash = 67;
            hash = hash * 31 + CanonicalOwner.GetHashCode();
            hash = hash * 31 + Signature.GetHashCode();
            return hash;
        }

        public override string ToString()
        {
            return String.Concat(
                "Lookup for ",
                CanonicalOwner.ToString(),
                ". Target: ",
                Signature.ToString()
                );
        }
    }
}
