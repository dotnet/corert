// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;

namespace System.Runtime.InteropServices
{
    /// <summary>
    /// MCG applies this attribute to CCW vtables with a parameter of their interface.
    /// This has the effect of causing the vtable to root the interface, which may otherwise
    /// be elligible for dependency reduction.
    /// </summary>
    /// <example>
    ///         [System.Runtime.InteropServices.McgRootsTypeAttribute(typeof(Windows.UI.Xaml.IDependencyObject))]
    ///         internal unsafe partial struct __vtable_Windows_UI_Xaml__IDependencyObject
    /// </example>
    [AttributeUsage(AttributeTargets.Struct, AllowMultiple = true, Inherited = false)]
    public sealed class McgRootsTypeAttribute : System.Attribute
    {
        public McgRootsTypeAttribute(Type rootedType) { }
    }
}
