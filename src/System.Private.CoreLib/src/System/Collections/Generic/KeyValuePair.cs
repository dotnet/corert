// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Text;

namespace System.Collections.Generic
{
    internal static partial class Toolbox
    {
        /// <summary>
        /// Used by KeyValuePair.ToString to reduce generic code
        /// </summary>
        internal static string PairToString(object key, object value)
        {
            StringBuilder s = StringBuilderCache.Acquire();
            s.Append('[');

            if (key != null)
            {
                s.Append(key);
            }

            s.Append(", ");

            if (value != null)
            {
                s.Append(value);
            }

            s.Append(']');

            return StringBuilderCache.GetStringAndRelease(s);
        }
    }


    // A KeyValuePair holds a key and a value from a dictionary.
    // It is used by the IEnumerable<T> implementation for both IDictionary<TKey, TValue>
    // and IReadOnlyDictionary<TKey, TValue>.
    public struct KeyValuePair<TKey, TValue>
    {
        private TKey key;       // DO NOT change the field name, it's required for compatibility with desktop .NET as it appears in serialization payload.
        private TValue value;   // DO NOT change the field name, it's required for compatibility with desktop .NET as it appears in serialization payload.

        public KeyValuePair(TKey key, TValue value)
        {
            this.key = key;
            this.value = value;
        }

        public TKey Key
        {
            get { return key; }
        }

        public TValue Value
        {
            get { return value; }
        }

        public override string ToString()
        {
            return Toolbox.PairToString(Key, Value);
        }
    }
}
