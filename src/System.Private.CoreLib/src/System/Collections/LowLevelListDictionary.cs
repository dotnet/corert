// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

/*============================================================
**
** Purpose: List for exceptions.
** 
===========================================================*/

using System.Diagnostics.Contracts;

namespace System.Collections
{
    ///    This is a simple implementation of IDictionary using a singly linked list. This
    ///    will be smaller and faster than a Hashtable if the number of elements is 10 or less.
    ///    This should not be used if performance is important for large numbers of elements.
    internal class LowLevelListDictionary : IDictionary
    {
        private DictionaryNode _head;
        private int _version;
        private int _count;
        private Object _syncRoot;

        public LowLevelListDictionary()
        {
        }

        public Object this[Object key]
        {
            get
            {
                if (key == null)
                {
                    throw new ArgumentNullException(nameof(key), SR.ArgumentNull_Key);
                }
                Contract.EndContractBlock();
                DictionaryNode node = _head;

                while (node != null)
                {
                    if (node.key.Equals(key))
                    {
                        return node.value;
                    }
                    node = node.next;
                }
                return null;
            }
            set
            {
                if (key == null)
                {
                    throw new ArgumentNullException(nameof(key), SR.ArgumentNull_Key);
                }
                Contract.EndContractBlock();

#if FEATURE_SERIALIZATION
                if (!key.GetType().IsSerializable)
                    throw new ArgumentException(Environment.GetResourceString("Argument_NotSerializable"), nameof(key));

                if ((value != null) && (!value.GetType().IsSerializable))
                    throw new ArgumentException(Environment.GetResourceString("Argument_NotSerializable"), nameof(value));
#endif

                _version++;
                DictionaryNode last = null;
                DictionaryNode node;
                for (node = _head; node != null; node = node.next)
                {
                    if (node.key.Equals(key))
                    {
                        break;
                    }
                    last = node;
                }
                if (node != null)
                {
                    // Found it
                    node.value = value;
                    return;
                }
                // Not found, so add a new one
                DictionaryNode newNode = new DictionaryNode();
                newNode.key = key;
                newNode.value = value;
                if (last != null)
                {
                    last.next = newNode;
                }
                else
                {
                    _head = newNode;
                }
                _count++;
            }
        }

        public int Count
        {
            get
            {
                return _count;
            }
        }

        public ICollection Keys
        {
            get
            {
                return new NodeKeyValueCollection(this, true);
            }
        }

        public bool IsReadOnly
        {
            get
            {
                return false;
            }
        }

        public bool IsFixedSize
        {
            get
            {
                return false;
            }
        }

        public bool IsSynchronized
        {
            get
            {
                return false;
            }
        }

        public Object SyncRoot
        {
            get
            {
                if (_syncRoot == null)
                {
                    System.Threading.Interlocked.CompareExchange<Object>(ref _syncRoot, new Object(), null);
                }
                return _syncRoot;
            }
        }

        public ICollection Values
        {
            get
            {
                return new NodeKeyValueCollection(this, false);
            }
        }

        public void Add(Object key, Object value)
        {
            if (key == null)
            {
                throw new ArgumentNullException(nameof(key), SR.ArgumentNull_Key);
            }
            Contract.EndContractBlock();

#if FEATURE_SERIALIZATION
            if (!key.GetType().IsSerializable)
                throw new ArgumentException(Environment.GetResourceString("Argument_NotSerializable"), nameof(key));

            if ((value != null) && (!value.GetType().IsSerializable))
                throw new ArgumentException(Environment.GetResourceString("Argument_NotSerializable"), nameof(value));
#endif

            _version++;
            DictionaryNode last = null;
            DictionaryNode node;
            for (node = _head; node != null; node = node.next)
            {
                if (node.key.Equals(key))
                {
                    throw new ArgumentException(SR.Format(SR.Argument_AddingDuplicate__, node.key, key));
                }
                last = node;
            }
            if (node != null)
            {
                // Found it
                node.value = value;
                return;
            }
            // Not found, so add a new one
            DictionaryNode newNode = new DictionaryNode();
            newNode.key = key;
            newNode.value = value;
            if (last != null)
            {
                last.next = newNode;
            }
            else
            {
                _head = newNode;
            }
            _count++;
        }

        public void Clear()
        {
            _count = 0;
            _head = null;
            _version++;
        }

        public bool Contains(Object key)
        {
            if (key == null)
            {
                throw new ArgumentNullException(nameof(key), SR.ArgumentNull_Key);
            }
            Contract.EndContractBlock();
            for (DictionaryNode node = _head; node != null; node = node.next)
            {
                if (node.key.Equals(key))
                {
                    return true;
                }
            }
            return false;
        }

        public void CopyTo(Array array, int index)
        {
            if (array == null)
                throw new ArgumentNullException(nameof(array));

            if (array.Rank != 1)
                throw new ArgumentException(SR.Arg_RankMultiDimNotSupported);

            if (index < 0)
                throw new ArgumentOutOfRangeException(nameof(index), SR.ArgumentOutOfRange_NeedNonNegNum);

            if (array.Length - index < this.Count)
                throw new ArgumentException(SR.ArgumentOutOfRange_Index, nameof(index));
            Contract.EndContractBlock();

            for (DictionaryNode node = _head; node != null; node = node.next)
            {
                array.SetValue(new DictionaryEntry(node.key, node.value), index);
                index++;
            }
        }

        public IDictionaryEnumerator GetEnumerator()
        {
            return new NodeEnumerator(this);
        }

        IEnumerator IEnumerable.GetEnumerator()
        {
            return new NodeEnumerator(this);
        }

        public void Remove(Object key)
        {
            if (key == null)
            {
                throw new ArgumentNullException(nameof(key), SR.ArgumentNull_Key);
            }
            Contract.EndContractBlock();
            _version++;
            DictionaryNode last = null;
            DictionaryNode node;
            for (node = _head; node != null; node = node.next)
            {
                if (node.key.Equals(key))
                {
                    break;
                }
                last = node;
            }
            if (node == null)
            {
                return;
            }
            if (node == _head)
            {
                _head = node.next;
            }
            else
            {
                last.next = node.next;
            }
            _count--;
        }

        private class NodeEnumerator : IDictionaryEnumerator
        {
            private LowLevelListDictionary _list;
            private DictionaryNode _current;
            private int _version;
            private bool _start;


            public NodeEnumerator(LowLevelListDictionary list)
            {
                _list = list;
                _version = list._version;
                _start = true;
                _current = null;
            }

            public Object Current
            {
                get
                {
                    return Entry;
                }
            }

            public DictionaryEntry Entry
            {
                get
                {
                    if (_current == null)
                    {
                        throw new InvalidOperationException(SR.InvalidOperation_EnumOpCantHappen);
                    }
                    return new DictionaryEntry(_current.key, _current.value);
                }
            }

            public Object Key
            {
                get
                {
                    if (_current == null)
                    {
                        throw new InvalidOperationException(SR.InvalidOperation_EnumOpCantHappen);
                    }
                    return _current.key;
                }
            }

            public Object Value
            {
                get
                {
                    if (_current == null)
                    {
                        throw new InvalidOperationException(SR.InvalidOperation_EnumOpCantHappen);
                    }
                    return _current.value;
                }
            }

            public bool MoveNext()
            {
                if (_version != _list._version)
                {
                    throw new InvalidOperationException(SR.InvalidOperation_EnumFailedVersion);
                }
                if (_start)
                {
                    _current = _list._head;
                    _start = false;
                }
                else
                {
                    if (_current != null)
                    {
                        _current = _current.next;
                    }
                }
                return (_current != null);
            }

            public void Reset()
            {
                if (_version != _list._version)
                {
                    throw new InvalidOperationException(SR.InvalidOperation_EnumFailedVersion);
                }
                _start = true;
                _current = null;
            }
        }


        private class NodeKeyValueCollection : ICollection
        {
            private LowLevelListDictionary _list;
            private bool _isKeys;

            public NodeKeyValueCollection(LowLevelListDictionary list, bool isKeys)
            {
                _list = list;
                _isKeys = isKeys;
            }

            void ICollection.CopyTo(Array array, int index)
            {
                if (array == null)
                    throw new ArgumentNullException(nameof(array));
                if (array.Rank != 1)
                    throw new ArgumentException(SR.Arg_RankMultiDimNotSupported);
                if (index < 0)
                    throw new ArgumentOutOfRangeException(nameof(index), SR.ArgumentOutOfRange_NeedNonNegNum);
                Contract.EndContractBlock();
                if (array.Length - index < _list.Count)
                    throw new ArgumentException(SR.ArgumentOutOfRange_Index, nameof(index));
                for (DictionaryNode node = _list._head; node != null; node = node.next)
                {
                    array.SetValue(_isKeys ? node.key : node.value, index);
                    index++;
                }
            }

            int ICollection.Count
            {
                get
                {
                    int count = 0;
                    for (DictionaryNode node = _list._head; node != null; node = node.next)
                    {
                        count++;
                    }
                    return count;
                }
            }

            bool ICollection.IsSynchronized
            {
                get
                {
                    return false;
                }
            }

            Object ICollection.SyncRoot
            {
                get
                {
                    return _list.SyncRoot;
                }
            }

            IEnumerator IEnumerable.GetEnumerator()
            {
                return new NodeKeyValueEnumerator(_list, _isKeys);
            }


            private class NodeKeyValueEnumerator : IEnumerator
            {
                private LowLevelListDictionary _list;
                private DictionaryNode _current;
                private int _version;
                private bool _isKeys;
                private bool _start;

                public NodeKeyValueEnumerator(LowLevelListDictionary list, bool isKeys)
                {
                    _list = list;
                    _isKeys = isKeys;
                    _version = list._version;
                    _start = true;
                    _current = null;
                }

                public Object Current
                {
                    get
                    {
                        if (_current == null)
                        {
                            throw new InvalidOperationException(SR.InvalidOperation_EnumOpCantHappen);
                        }
                        return _isKeys ? _current.key : _current.value;
                    }
                }

                public bool MoveNext()
                {
                    if (_version != _list._version)
                    {
                        throw new InvalidOperationException(SR.InvalidOperation_EnumFailedVersion);
                    }
                    if (_start)
                    {
                        _current = _list._head;
                        _start = false;
                    }
                    else
                    {
                        if (_current != null)
                        {
                            _current = _current.next;
                        }
                    }
                    return (_current != null);
                }

                public void Reset()
                {
                    if (_version != _list._version)
                    {
                        throw new InvalidOperationException(SR.InvalidOperation_EnumFailedVersion);
                    }
                    _start = true;
                    _current = null;
                }
            }
        }

        private class DictionaryNode
        {
            public Object key;
            public Object value;
            public DictionaryNode next;
        }
    }
}
