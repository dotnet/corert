// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

// 

// 

using System;

namespace Internal.Reflection
{
    // This enumeration is a contract with Dependency Reducer ReducerEngine.cs, MdTransform\Metadata.cs, and MCG
    [Flags]
    public enum MetadataTransformation
    {
        None = 0x0,
        OriginallyNotSealed = 0x1, // A method was originally unsealed, but a transform sealed it
        OriginallyVirtual = 0x2, // A method was originally virtual, but a transform devirtualized it
        OriginallySealed = 0x4, // A method was originally sealed, but a transform unsealed it
        OriginallyNewSlot = 0x8, // A method was originally NewSlot
        OriginallyAccessCheckedOnOverride = 0x10, // A method was originally AccessCheckedOnOverride (strict)

        OriginallyForeignObject = 0x20, // A class was originally marked as WindowsRuntime
        OriginallyComObject = 0x40
    }

    /// <summary>
    /// Indicates that a transform has changed metadata and has a flag for the state
    /// reflection should show
    /// </summary>
    [System.Runtime.CompilerServices.DependencyReductionRoot]
    [AttributeUsage(
        AttributeTargets.Method |
        AttributeTargets.Class |
        AttributeTargets.Enum |
        AttributeTargets.Interface |
        AttributeTargets.Struct |
        AttributeTargets.Delegate,
        Inherited = false)]
    public sealed class MetadataTransformedAttribute : Attribute
    {
        public MetadataTransformedAttribute(MetadataTransformation transformation)
        {
        }
    }
}
