// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace System.Runtime.CompilerServices
{
    [AttributeUsage(AttributeTargets.Field)]
    [System.Runtime.InteropServices.ComVisible(true)]
    public sealed class AccessedThroughPropertyAttribute : Attribute
    {
        private readonly string _propertyName;

        public AccessedThroughPropertyAttribute(string propertyName)
        {
            _propertyName = propertyName;
        }

        public string PropertyName
        {
            get
            {
                return _propertyName;
            }
        }
    }
}

