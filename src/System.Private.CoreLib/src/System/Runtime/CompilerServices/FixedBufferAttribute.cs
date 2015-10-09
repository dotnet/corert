// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

/*============================================================
**
**
** Purpose: Used by a compiler for generating value types
**   in-place within other value types containing a certain
**   number of elements of the given (primitive) type.  Somewhat
**   similar to P/Invoke's ByValTStr attribute.
**   Used by C# with this syntax: "fixed int buffer[10];"
**
===========================================================*/

using System;

namespace System.Runtime.CompilerServices
{
    [AttributeUsage(AttributeTargets.Field, Inherited = false)]
    public sealed class FixedBufferAttribute : Attribute
    {
        private Type _elementType;
        private int _length;

        public FixedBufferAttribute(Type elementType, int length)
        {
            _elementType = elementType;
            _length = length;
        }

        public Type ElementType
        {
            get
            {
                return _elementType;
            }
        }

        public int Length
        {
            get
            {
                return _length;
            }
        }
    }
}
