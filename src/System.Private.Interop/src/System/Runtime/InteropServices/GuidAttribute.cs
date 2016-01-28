// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;

namespace System.Runtime.InteropServices
{
    [AttributeUsage(AttributeTargets.Assembly | AttributeTargets.Interface | AttributeTargets.Class | AttributeTargets.Enum | AttributeTargets.Struct | AttributeTargets.Delegate, Inherited = false)]
    public sealed class GuidAttribute : Attribute
    {
        internal String _val;
        public GuidAttribute(String guid)
        {
            _val = guid;
        }
        public String Value { get { return _val; } }
    }
}
