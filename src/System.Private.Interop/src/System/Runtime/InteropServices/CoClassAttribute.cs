// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System;

namespace System.Runtime.InteropServices
{
    [AttributeUsage(AttributeTargets.Interface, Inherited = false)]
    public sealed class CoClassAttribute : Attribute
    {
        internal Type _CoClass;

        public CoClassAttribute(Type coClass)
        {
            _CoClass = coClass;
        }

        public Type CoClass { get { return _CoClass; } }
    }
}
