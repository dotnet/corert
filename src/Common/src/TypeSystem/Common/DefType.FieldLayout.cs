// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System;

namespace Internal.TypeSystem
{
    // This is the api surface necessary to query the field layout of a type

    public abstract partial class DefType : TypeDesc
    {
        /// <summary>
        /// Does a type transitively have any fields which are GC object pointers
        /// </summary>
        public abstract bool ContainsPointers
        {
            get;
        }

        /// <summary>
        /// The number of bytes required to hold a field of this type
        /// </summary>
        public abstract int InstanceFieldSize
        {
            get;
        }

        /// <summary>
        /// What is the alignment requirement of the fields of this type
        /// </summary>
        public abstract int InstanceFieldAlignment
        {
            get;
        }

        /// <summary>
        /// The number of bytes required when allocating this type on this GC heap
        /// </summary>
        public abstract int InstanceByteCount
        {
            get;
        }

        /// <summary>
        /// How many bytes must be allocated to represent the non GC visible static fields of this type.
        /// </summary>
        public abstract int NonGCStaticFieldSize
        {
            get;
        }

        /// <summary>
        /// What is the alignment required for allocating the non GC visible static fields of this type.
        /// </summary>
        public abstract int NonGCStaticFieldAlignment
        {
            get;
        }

        /// <summary>
        /// How many bytes must be allocated to represent the GC visible static fields of this type.
        /// </summary>
        public abstract int GCStaticFieldSize
        {
            get;
        }

        /// <summary>
        /// What is the alignment required for allocating the GC visible static fields of this type.
        /// </summary>
        public abstract int GCStaticFieldAlignment
        {
            get;
        }

        /// <summary>
        /// How many bytes must be allocated to represent the (potentially GC visible) thread static
        /// fields of this type.
        /// </summary>
        public abstract int ThreadStaticFieldSize
        {
            get;
        }

        /// <summary>
        /// What is the alignment required for allocating the (potentially GC visible) thread static
        /// fields of this type.
        /// </summary>
        public abstract int ThreadStaticFieldAlignment
        {
            get;
        }
    }

}