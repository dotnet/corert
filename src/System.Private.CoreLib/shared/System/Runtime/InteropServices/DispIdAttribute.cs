// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

namespace System.Runtime.InteropServices
{
    [AttributeUsage(AttributeTargets.Method | AttributeTargets.Field | AttributeTargets.Property | AttributeTargets.Event, Inherited = false)]
    public sealed class DispIdAttribute : Attribute
    {
        public DispIdAttribute(int dispId)
        {
            Value = dispId;
        }

        public int Value { get; }
    }
}
