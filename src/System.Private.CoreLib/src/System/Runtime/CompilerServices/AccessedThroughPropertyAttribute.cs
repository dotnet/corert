// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

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

