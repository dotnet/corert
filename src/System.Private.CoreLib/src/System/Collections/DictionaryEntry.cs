// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace System.Collections
{
    // A DictionaryEntry holds a key and a value from a dictionary.
    // It is returned by IDictionaryEnumerator::GetEntry().
    [System.Runtime.InteropServices.ComVisible(true)]
    public struct DictionaryEntry
    {
        private Object _key;
        private Object _value;

        // Constructs a new DictionaryEnumerator by setting the Key
        // and Value fields appropriately.
        public DictionaryEntry(Object key, Object value)
        {
            _key = key;
            _value = value;
        }

        public Object Key
        {
            get
            {
                return _key;
            }

            set
            {
                _key = value;
            }
        }

        public Object Value
        {
            get
            {
                return _value;
            }

            set
            {
                _value = value;
            }
        }
    }
}
