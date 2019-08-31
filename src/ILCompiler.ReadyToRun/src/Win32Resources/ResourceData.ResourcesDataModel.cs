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

namespace ILCompiler.Win32Resources
{
    public unsafe partial class ResourceData
    {
        private List<ResType_Ordinal> ResTypeHeadID = new List<ResType_Ordinal>();
        private List<ResType_Name> ResTypeHeadName = new List<ResType_Name>();

        class OrdinalName
        {
            public OrdinalName(ushort ordinal) { Ordinal = ordinal; }
            public readonly ushort Ordinal;
        }

        interface IUnderlyingName<T>
        {
            T Name { get; }
        }

        class ResName
        {
            public uint DataSize => (uint)DataEntry.Length;
            public byte[] DataEntry;
            public ushort NumberOfLanguages;
            public ushort LanguageId;
        }

        class ResName_Name : ResName, IUnderlyingName<string>
        {
            public ResName_Name(string name)
            {
                Name = name;
            }

            public string Name { get; }
        }

        class ResName_Ordinal : ResName, IUnderlyingName<ushort>
        {
            public ResName_Ordinal(ushort name)
            {
                Name = new OrdinalName(name);
            }

            public OrdinalName Name;
            ushort IUnderlyingName<ushort>.Name => Name.Ordinal;
        }

        class ResType
        {
            public List<ResName_Name> NameHeadName = new List<ResName_Name>();
            public List<ResName_Ordinal> NameHeadID = new List<ResName_Ordinal>();
        }

        class ResType_Ordinal : ResType, IUnderlyingName<ushort>
        {
            public ResType_Ordinal(ushort type)
            {
                Type = new OrdinalName(type);
            }

            public OrdinalName Type;
            ushort IUnderlyingName<ushort>.Name => Type.Ordinal;
        }

        class ResType_Name : ResType, IUnderlyingName<string>
        {
            public ResType_Name(string type)
            {
                Type = type;
            }

            public string Type { get; set; }
            string IUnderlyingName<string>.Name => Type;
        }
    }
}
