// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Text;
using System.IO;
using System.Reflection;

using Internal.TypeSystem.Ecma;
using Internal.TypeSystem.TypesDebugInfo;

namespace ILCompiler
{
    public class MergedAssemblyRecord
    {
        public EcmaAssembly Assembly { get; }
        public string Name { get; }
        public uint AssemblyIndex { get; }
        public uint Timestamp { get; }
        public bool HasPDB { get; }
        public byte[] PublicKey { get; } 
        public byte[] VersionInfo { get; }
        public int VersionInfoLength
        {
            get
            {
                return BitConverter.ToUInt16(VersionInfo, 0);
            }
        }

        public MergedAssemblyRecord(EcmaAssembly assembly, string name, uint assemblyIndex, uint timestamp, bool hasPDB, byte[] publicKey, byte[] versionInfo)
        {
            Assembly = assembly;
            Name = name;
            AssemblyIndex = assemblyIndex;
            Timestamp = timestamp;
            HasPDB = hasPDB;
            PublicKey = publicKey;
            VersionInfo = versionInfo;

            if (versionInfo.Length < sizeof(ushort))
                throw new ArgumentException("versionInfo");

            int versionInfoLength = VersionInfoLength;
            if (versionInfoLength == 0)
            {
                VersionInfo = BitConverter.GetBytes((ushort)sizeof(ushort));
                Debug.Assert(VersionInfoLength == sizeof(ushort));
            }
            else
            {
                // Validate that a non-empty version info contains a VS_VERSION_INFO structure
                string vsVersionInfoString = "VS_VERSION_INFO";

                if (VersionInfoLength < (6 + vsVersionInfoString.Length * sizeof(char)))
                    throw new ArgumentException("versionInfo");

                string encodedString = Encoding.Unicode.GetString(versionInfo, 6, vsVersionInfoString.Length * sizeof(char));
                if (encodedString != vsVersionInfoString)
                    throw new ArgumentException("versionInfo");
            }

        }

        internal void Encode(DebugInfoBlob blob)
        {
            blob.WriteDWORD(Timestamp);
            blob.WriteDWORD(AssemblyIndex & 0x7FFFFFFF | (HasPDB ? 0x80000000 : 0));
            blob.WriteBuffer(VersionInfo, 0, VersionInfoLength);

            string nameWithPublicKey = Name;
            if (PublicKey != null && PublicKey.Length > 0)
            {
                nameWithPublicKey += ", PublicKey=";
                nameWithPublicKey += BitConverter.ToString(PublicKey).Replace("-", "");
            }
            blob.WriteString(nameWithPublicKey);
            blob.AlignToDWORD();
        }
    }

    public class MergedAssemblyRecords
    {
        public IReadOnlyCollection<MergedAssemblyRecord> MergedAssemblies { get; }
        public uint CorLibIndex { get; }
        public MergedAssemblyRecords(IReadOnlyCollection<MergedAssemblyRecord> mergedAssemblies, uint corLibIndex)
        {
            MergedAssemblies = mergedAssemblies;
            CorLibIndex = corLibIndex;
        }
    }
}
