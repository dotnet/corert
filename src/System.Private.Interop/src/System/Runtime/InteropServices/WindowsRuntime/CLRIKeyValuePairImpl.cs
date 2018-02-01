// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System.Collections.Generic;
using Windows.Foundation.Collections;

namespace System.Runtime.InteropServices.WindowsRuntime
{
    [System.Runtime.CompilerServices.DependencyReductionRootAttribute]
    [McgInternalTypeAttribute]
    public sealed class CLRIKeyValuePairImpl<K, V> :
        global::System.Runtime.InteropServices.BoxedKeyValuePair,
        IKeyValuePair<K, V>, // Use IKeyValuePair<K, V> from S.P.Interop instead of Windows.winmd to loose the dependency between S.P.Interop and Windows.winmd
        __IUnboxInternal
    {
        private global::System.Collections.Generic.KeyValuePair<K, V> _pair;

        public CLRIKeyValuePairImpl(ref global::System.Collections.Generic.KeyValuePair<K, V> pair)
        {
            _pair = pair;
        }

        public K get_Key()
        {
            return _pair.Key;
        }

        [global::System.Runtime.InteropServices.McgAccessor(global::System.Runtime.InteropServices.McgAccessorKind.PropertyGet, "Value")]
        public V get_Value()
        {
            return _pair.Value;
        }

        public override object GetTarget()
        {
            return (object)_pair;
        }

        /// <summary>
        ///  Called by public object McgModule.Box(object obj, int boxingIndex) after allocating instance
        /// </summary>
        /// <param name="pair">KeyValuePair<K, V></param>
        /// <returns>IKeyValuePair<K,V> instance</returns>
        public override object Initialize(object pair)
        {
            _pair = (global::System.Collections.Generic.KeyValuePair<K, V>)pair;

            return this;
        }

        /// <summary>
        /// Get unboxed value
        /// This method is used by dynamic interop.
        /// The reason for adding this instance method instead of using static Unbox is to avoid reflection
        /// </summary>
        /// <param name="obj">native winrt object or our own boxed KeyValuePair<K,V></K></param>
        /// <returns>unboxed value as object</returns>
        public object get_Value(object obj)
        {
            return Unbox(obj);
        }

        /// <summary>
        /// This method is called from ComInterop.cs in the Unboxing code.
        /// </summary>
        /// <param name="wrapper">native winrt object or our own boxed KeyValuePair<K,V></param>
        /// <returns>unboxed value as object</returns>
        public static object Unbox(object wrapper)
        {
            CLRIKeyValuePairImpl<K, V> reference = wrapper as CLRIKeyValuePairImpl<K, V>;

            if (reference != null)
            {
                return reference._pair;
            }
            else
            {
                // We could just have the native IKeyValuePair in which case we simply return wrapper as IKeyValuePair.
                global::Windows.Foundation.Collections.IKeyValuePair<K, V> iPair = wrapper as global::Windows.Foundation.Collections.IKeyValuePair<K, V>;
                return new System.Collections.Generic.KeyValuePair<K, V>(iPair.get_Key(), iPair.get_Value());
            }
        }

        public override string ToString()
        {
            return _pair.ToString();
        }
    }

    [System.Runtime.CompilerServices.DependencyReductionRootAttribute]
    [McgInternalTypeAttribute]
    public sealed class CLRIKeyValuePairArrayImpl<K, V> : global::System.Runtime.InteropServices.BoxedKeyValuePair
    {
        private object _pairs;

        /// <summary>
        /// Called by public object McgModule.Box(object obj, int boxingIndex) after allocating instance
        /// </summary>
        /// <param name="pairs">KeyValuePair<K,V>[]</param>
        /// <returns>IReferenceArrayImpl<K,V> instance</K></returns>
        public override object Initialize(object pairs)
        {
            _pairs = pairs;

            global::System.Collections.Generic.KeyValuePair<K, V>[] unboxedPairArray = pairs as global::System.Collections.Generic.KeyValuePair<K, V>[];

            object[] boxedKeyValuePairs = new object[unboxedPairArray.Length];

            for (int i = 0; i < unboxedPairArray.Length; i++)
            {
                boxedKeyValuePairs[i] = new CLRIKeyValuePairImpl<K, V>(ref unboxedPairArray[i]);
            }

            // Lets create the IReferenceArrayImpl of the type.
            return new ReferenceArrayImpl<object>(boxedKeyValuePairs, PropertyType.InspectableArray);
        }

        public override object GetTarget()
        {
            return (object)_pairs;
        }
    }
}
