// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

namespace System.Collections.Generic
{
    // NonRandomizedStringEqualityComparer is the comparer used by default with the Dictionary<string,...> 
    // As the randomized string hashing is now turned on with no opt-out, we need to keep the performance not affected 
    // as much as possible in the main stream scenarios like Dictionary<string,>
    // We use NonRandomizedStringEqualityComparer as default comparer as it doesnt use the randomized string hashing which 
    // keep the performance not affected till we hit collision threshold and then we switch to the comparer which is using 
    // randomized string hashing.
    [Serializable]
    [System.Runtime.CompilerServices.TypeForwardedFrom("mscorlib, Version=4.0.0.0, Culture=neutral, PublicKeyToken=b77a5c561934e089")]
    public sealed class NonRandomizedStringEqualityComparer : EqualityComparer<string>
    {
        private static volatile IEqualityComparer<string> s_nonRandomizedComparer;

        internal static new IEqualityComparer<string> Default => s_nonRandomizedComparer ?? (s_nonRandomizedComparer = new NonRandomizedStringEqualityComparer());

        public sealed override bool Equals(string x, string y) => string.Equals(x, y);

        public sealed override int GetHashCode(string obj)
        {
            if (obj == null)
                return 0;
            return obj.GetLegacyNonRandomizedHashCode();
        }
    }
} 
