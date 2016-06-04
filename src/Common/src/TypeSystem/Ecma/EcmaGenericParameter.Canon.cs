// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using Debug = System.Diagnostics.Debug;

namespace Internal.TypeSystem.Ecma
{
    // Implements canonicalization of ECMA generic parameters 
    public partial class EcmaGenericParameter
    {
        public override bool IsCanonicalSubtype(CanonicalFormKind policy)
        {
            Debug.Assert(false, "IsCanonicalSubtype of an indefinite type");
            return false;
        }

        protected override TypeDesc ConvertToCanonFormImpl(CanonicalFormKind kind)
        {
            Debug.Assert(false, "ConvertToCanonFormImpl for an indefinite type");
            return this;
        }
    }
}