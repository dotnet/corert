// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Collections;

namespace System.Collections.Generic
{
    internal sealed class ObjectEqualityComparer : IEqualityComparer
    {
        internal static readonly ObjectEqualityComparer Default = new ObjectEqualityComparer();

        private ObjectEqualityComparer()
        {
        }

        int IEqualityComparer.GetHashCode(object obj)
        {
            if (obj == null)
                return 0;
            else return obj.GetHashCode();
        }

        bool IEqualityComparer.Equals(object x, object y)
        {
            if (x == null)
                return y == null;

            if (y == null)
                return false;

            return x.Equals(y);
        }
    }
}
