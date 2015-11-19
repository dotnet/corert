// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace System.Runtime.InteropServices
{
    [AttributeUsage(AttributeTargets.Field, Inherited = false)]
    [System.Runtime.InteropServices.ComVisible(true)]
    public sealed class FieldOffsetAttribute : Attribute
    {
        private int _val;
        public FieldOffsetAttribute(int offset)
        {
            _val = offset;
        }
        public int Value { get { return _val; } }
    }
}
