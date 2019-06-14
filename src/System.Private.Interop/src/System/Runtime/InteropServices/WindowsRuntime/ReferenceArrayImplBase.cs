// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.


namespace System.Runtime.InteropServices.WindowsRuntime
{
#pragma warning disable 618
    [System.Runtime.CompilerServices.DependencyReductionRootAttribute]
    [McgInternalTypeAttribute]
    public class ReferenceArrayImplBase :
     PropertyValueImpl,
     System.Collections.IList,
     System.Collections.Generic.IEnumerable<object>,
     System.Runtime.InteropServices.ICustomQueryInterface
    {
        // pinterface({faa585ea-6214-4217-afda-7f46de5869b3};cinterface(IInspectable))
        internal static System.Guid IID_IIterableOfObject =
            new System.Guid(153846939, 24753, 21182, 0xA4, 0x4A, 0x6F, 0xE8, 0xE9, 0x33, 0xCB, 0xE4);

        private System.Collections.IList _list;
        private System.Collections.Generic.IEnumerable<object> _enumerableOfObject;

        public override void Initialize(object val, int type)
        {
            m_data = val;
            m_type = (short)type;

            // This should not fail but I'm making a cast here anyway just in case 
            // we have a bug or there is a runtime failure
            _list = (System.Collections.IList)val;

            // Not every array implements IEnumerable<Object>
            _enumerableOfObject = val as System.Collections.Generic.IEnumerable<object>;
        }

        internal ReferenceArrayImplBase(object data, PropertyType type) : base(data, (int)type)
        {
            Initialize(data, (int)type);
        }

        //
        // Customize QI behavior:
        // If this array type doesn't implement IEnumerable<Object>, reject IIterable<Object>
        //
        System.Runtime.InteropServices.CustomQueryInterfaceResult ICustomQueryInterface.GetInterface(ref System.Guid iid, out System.IntPtr ppv)
        {
            ppv = default(System.IntPtr);

            if (System.Runtime.InteropServices.McgMarshal.GuidEquals(ref iid, ref IID_IIterableOfObject))
            {
                if (_enumerableOfObject == null)
                {
                    // This array type doesn't actually support IEnumerable<Object>
                    // Reject the QI
                    return System.Runtime.InteropServices.CustomQueryInterfaceResult.Failed;
                }
            }

            return System.Runtime.InteropServices.CustomQueryInterfaceResult.NotHandled;
        }

        //
        // IEnumerable methods. Used by data-binding in Jupiter when you try to data bind
        // against a managed array
        //
        System.Collections.IEnumerator System.Collections.IEnumerable.GetEnumerator()
        {
            return ((System.Collections.IEnumerable)_list).GetEnumerator();
        }

        //
        // IEnumerable<object> methods. We need this because System.Array implement this (implicitly) if it is castable to object[]
        //
        System.Collections.Generic.IEnumerator<object> System.Collections.Generic.IEnumerable<object>.GetEnumerator()
        {
            return _enumerableOfObject.GetEnumerator();
        }

        //
        // IList & ICollection methods. 
        // This enables two-way data binding and index access in Jupiter
        //
        object System.Collections.IList.this[int index]
        {
            get
            {
                return _list[index];
            }

            set
            {
                _list[index] = value;
            }
        }

        int System.Collections.IList.Add(object value)
        {
            return _list.Add(value);
        }

        bool System.Collections.IList.Contains(object value)
        {
            return _list.Contains(value);
        }

        void System.Collections.IList.Clear()
        {
            _list.Clear();
        }

        bool System.Collections.IList.IsReadOnly
        {
            get
            {
                return _list.IsReadOnly;
            }
        }

        bool System.Collections.IList.IsFixedSize
        {
            get
            {
                return _list.IsFixedSize;
            }
        }

        int System.Collections.IList.IndexOf(object value)
        {
            return _list.IndexOf(value);
        }

        void System.Collections.IList.Insert(int index, object value)
        {
            _list.Insert(index, value);
        }

        void System.Collections.IList.Remove(object value)
        {
            _list.Remove(value);
        }

        void System.Collections.IList.RemoveAt(int index)
        {
            _list.RemoveAt(index);
        }

        void System.Collections.ICollection.CopyTo(System.Array array, int index)
        {
            _list.CopyTo(array, index);
        }

        int System.Collections.ICollection.Count
        {
            get
            {
                return _list.Count;
            }
        }

        object System.Collections.ICollection.SyncRoot
        {
            get
            {
                return _list.SyncRoot;
            }
        }

        bool System.Collections.ICollection.IsSynchronized
        {
            get
            {
                return _list.IsSynchronized;
            }
        }
    }
#pragma warning restore 169
}
