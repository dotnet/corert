// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System.Diagnostics.Contracts;

namespace System.Globalization
{
    internal partial class FormatProvider
    {
        internal sealed class CultureAwareComparer : StringComparer
#if FEATURE_RANDOMIZED_STRING_HASHING
        , IWellKnownStringEqualityComparer
#endif
        {
            private CompareInfo _compareInfo;
            private bool _ignoreCase;

            internal CultureAwareComparer(CultureInfo culture, bool ignoreCase)
            {
                _compareInfo = culture.CompareInfo;
                _ignoreCase = ignoreCase;
            }

            public override int Compare(string x, string y)
            {
                if (Object.ReferenceEquals(x, y)) return 0;
                if (x == null) return -1;
                if (y == null) return 1;
                return _compareInfo.Compare(x, 0, x.Length, y, 0, y.Length, _ignoreCase ? CompareOptions.IgnoreCase : CompareOptions.None);
            }

            public override bool Equals(string x, string y)
            {
                if (Object.ReferenceEquals(x, y)) return true;
                if (x == null || y == null) return false;

                return (_compareInfo.Compare(x, 0, x.Length, y, 0, y.Length, _ignoreCase ? CompareOptions.IgnoreCase : CompareOptions.None) == 0);
            }

            public override int GetHashCode(string obj)
            {
                if (obj == null)
                {
                    throw new ArgumentNullException(nameof(obj));
                }
                Contract.EndContractBlock();

                CompareOptions options = CompareOptions.None;

                if (_ignoreCase)
                {
                    options |= CompareOptions.IgnoreCase;
                }

                return _compareInfo.GetHashCodeOfString(obj, options);
            }

            // Equals method for the comparer itself. 
            public override bool Equals(Object obj)
            {
                CultureAwareComparer comparer = obj as CultureAwareComparer;
                if (comparer == null)
                {
                    return false;
                }
                return (_ignoreCase == comparer._ignoreCase) && (_compareInfo.Equals(comparer._compareInfo));
            }

            public override int GetHashCode()
            {
                int hashCode = _compareInfo.GetHashCode();
                return _ignoreCase ? (~hashCode) : hashCode;
            }
#if FEATURE_RANDOMIZED_STRING_HASHING
            IEqualityComparer IWellKnownStringEqualityComparer.GetRandomizedEqualityComparer()
            {
                return new CultureAwareRandomizedComparer(_compareInfo, _ignoreCase);
            }

            IEqualityComparer IWellKnownStringEqualityComparer.GetEqualityComparerForSerialization()
            {
                return this;
            }
#endif

        }
    }
}
