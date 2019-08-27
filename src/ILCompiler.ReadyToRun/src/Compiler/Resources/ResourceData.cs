// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Collections;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Runtime.InteropServices;
using System.Text;


/// <summary>
/// Resource abstraction to allow examination
/// of a PE file that contains resources.
/// </summary>
public unsafe partial class ResourceData
{
    private byte[] _initialFileData;

    /// <summary>
    /// Initialize a ResourceData instance from a PE file
    /// </summary>
    /// <param name="peFileData"></param>
    public ResourceData(byte[] peFileData)
    {
        _initialFileData = (byte[])peFileData.Clone();
        ReadResourceData();
    }

    /// <summary>
    /// Find a resource in the resource data
    /// </summary>
    public byte[] FindResource(string name, string type, ushort language)
    {
        return FindResourceInternal(name, type, language);
    }

    /// <summary>
    /// Find a resource in the resource data
    /// </summary>
    public byte[] FindResource(ushort name, string type, ushort language)
    {
        return FindResourceInternal(name, type, language);
    }

    /// <summary>
    /// Find a resource in the resource data
    /// </summary>
    public byte[] FindResource(string name, ushort type, ushort language)
    {
        return FindResourceInternal(name, type, language);
    }

    /// <summary>
    /// Find a resource in the resource data
    /// </summary>
    public byte[] FindResource(ushort name, ushort type, ushort language)
    {
        return FindResourceInternal(name, type, language);
    }
}
