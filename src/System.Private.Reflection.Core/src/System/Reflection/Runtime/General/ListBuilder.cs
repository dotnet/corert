// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

//
//
//
// Implements System.RuntimeType
//
// ======================================================================================


using System;
using System.Reflection;
using System.Runtime.ConstrainedExecution;
using System.Globalization;
using System.Threading;
using System.Diagnostics;
using System.Security.Permissions;
using System.Collections;
using System.Collections.Generic;
using System.Runtime;
using System.Runtime.Serialization;    
using System.Runtime.CompilerServices;
using System.Security;
using System.Text;
using System.Runtime.Remoting;
#if FEATURE_REMOTING
using System.Runtime.Remoting.Proxies;
using System.Runtime.Remoting.Messaging;
using System.Runtime.Remoting.Activation;
using System.Runtime.Remoting.Metadata;
#endif
using MdSigCallingConvention = System.Signature.MdSigCallingConvention;
using RuntimeTypeCache = System.RuntimeType.RuntimeTypeCache;
using System.Runtime.InteropServices;
using DebuggerStepThroughAttribute = System.Reflection.DebuggerStepThroughAttribute;
using MdToken = System.Reflection.MetadataToken;
using System.Runtime.Versioning;
using System.Diagnostics.Contracts;

namespace System 
{
    [Serializable]
    internal class RuntimeType : 
        System.Reflection.TypeInfo, ISerializable, ICloneable
    {
        // Helper to build lists of MemberInfos. Special cased to avoid allocations for lists of one element.
        private struct ListBuilder<T> where T : class
        {
            T[] _items;
            T _item;
            int _count;
            int _capacity;

            public ListBuilder(int capacity)
            {
                _items = null;
                _item = null;
                _count = 0;
                _capacity = capacity;
            }

            public T this[int index]
            {
                get
                {
                    Contract.Requires(index < Count);
                    return (_items != null) ? _items[index] : _item;
                }
            }

            public T[] ToArray()
            {
                if (_count == 0)
                    return EmptyArray<T>.Value;
                if (_count == 1)
                    return new T[1] { _item };

                Array.Resize(ref _items, _count);
                _capacity = _count;
                return _items;
            }

            public void CopyTo(Object[] array, int index)
            {
                if (_count == 0)
                    return;

                if (_count == 1)
                {
                    array[index] = _item;
                    return;
                }

                Array.Copy(_items, 0, array, index, _count);
            }

            public int Count
            {
                get
                {
                    return _count;
                }
            }

            public void Add(T item)
            {
                if (_count == 0)
                {
                    _item = item;
                }
                else                
                {
                    if (_count == 1)
                    {
                        if (_capacity < 2)
                            _capacity = 4;
                        _items = new T[_capacity];
                        _items[0] = _item;
                    }
                    else
                    if (_capacity == _count)
                    {
                        int newCapacity = 2 * _capacity;
                        Array.Resize(ref _items, newCapacity);
                        _capacity = newCapacity;
                    }

                    _items[_count] = item;
                }
                _count++;
            }
        }
    }
}
