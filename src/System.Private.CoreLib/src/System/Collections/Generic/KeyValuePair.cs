// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

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
        private TKey _key;
        private TValue _value;

        public KeyValuePair(TKey key, TValue value)
        {
            _key = key;
            _value = value;
        }

        public TKey Key
        {
            get { return _key; }
        }

        public TValue Value
        {
            get { return _value; }
        }

        public override string ToString()
        {
            return Toolbox.PairToString(Key, Value);
        }
    }
}
