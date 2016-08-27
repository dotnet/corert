// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.


/*============================================================
**
  Type:  AssemblyName
**
==============================================================*/

using System.Globalization;
using System.Runtime.Serialization;
using System.Configuration.Assemblies;

using Internal.Reflection.Augments;

namespace System.Reflection
{
    public sealed class AssemblyName : ICloneable, IDeserializationCallback, ISerializable
    {
        public AssemblyName()
        {
            HashAlgorithm = AssemblyHashAlgorithm.None;
            VersionCompatibility = AssemblyVersionCompatibility.SameMachine;
            _flags = AssemblyNameFlags.None;
        }

        public AssemblyName(string assemblyName)
        {
            if (assemblyName == null)
                throw new ArgumentNullException("assemblyName");
            ReflectionAugments.ReflectionCoreCallbacks.InitializeAssemblyName(this, assemblyName);
        }

        public object Clone()
        {
            AssemblyName n = new AssemblyName();
            n.Name = Name;
            n._publicKey = (byte[])_publicKey?.Clone();
            n._publicKeyToken = (byte[])_publicKeyToken?.Clone();
            n.CultureInfo = CultureInfo;
            n.Version = (Version)Version?.Clone();
            n._flags = _flags;
            n.CodeBase = CodeBase;
            n.HashAlgorithm = HashAlgorithm;
            n.VersionCompatibility = VersionCompatibility;
            return n;
        }

        public ProcessorArchitecture ProcessorArchitecture
        {
            get
            {
                int x = (((int)_flags) & 0x70) >> 4;
                if (x > 5)
                    x = 0;
                return (ProcessorArchitecture)x;
            }
            set
            {
                int x = ((int)value) & 0x07;
                if (x <= 5)
                {
                    _flags = (AssemblyNameFlags)((int)_flags & 0xFFFFFF0F);
                    _flags |= (AssemblyNameFlags)(x << 4);
                }
            }
        }

        public AssemblyContentType ContentType
        {
            get
            {
                int x = (((int)_flags) & 0x00000E00) >> 9;
                if (x > 1)
                    x = 0;
                return (AssemblyContentType)x;
            }
            set
            {
                int x = ((int)value) & 0x07;
                if (x <= 1)
                {
                    _flags = (AssemblyNameFlags)((int)_flags & 0xFFFFF1FF);
                    _flags |= (AssemblyNameFlags)(x << 9);
                }
            }
        }

        public string CultureName
        {
            get
            {
                return CultureInfo?.Name;
            }
            set
            {
                CultureInfo = (value == null) ? null : new CultureInfo(value);
            }
        }

        public CultureInfo CultureInfo { get; set; }

        public AssemblyNameFlags Flags
        {
            get { return (AssemblyNameFlags)((uint)_flags & 0xFFFFF10F); }
            set
            {
                _flags &= unchecked((AssemblyNameFlags)0x00000EF0);
                _flags |= (value & unchecked((AssemblyNameFlags)0xFFFFF10F));
            }
        }

        public string FullName
        {
            get
            {
                if (this.Name == null)
                    return string.Empty;
                return ReflectionAugments.ReflectionCoreCallbacks.ComputeAssemblyNameFullName(this);
            }
        }

        public string Name { get; set; }
        public Version Version { get; set; }
        public string CodeBase { get; set; }
        public AssemblyHashAlgorithm HashAlgorithm { get; set; }
        public AssemblyVersionCompatibility VersionCompatibility { get; set; }

        public byte[] GetPublicKey()
        {
            return _publicKey;
        }

        public byte[] GetPublicKeyToken()
        {
            if (_publicKeyToken == null)
                _publicKeyToken = ReflectionAugments.ReflectionCoreCallbacks.ComputePublicKeyToken(_publicKey);
            return _publicKeyToken;
        }

        public void SetPublicKey(byte[] publicKey)
        {
            _publicKey = publicKey;

            if (publicKey == null)
                _flags &= ~AssemblyNameFlags.PublicKey;
            else
                _flags |= AssemblyNameFlags.PublicKey;
        }

        public void SetPublicKeyToken(byte[] publicKeyToken)
        {
            _publicKeyToken = publicKeyToken;
        }

        public override string ToString()
        {
            string s = FullName;
            if (s == null)
                return base.ToString();
            else
                return s;
        }

        public void GetObjectData(SerializationInfo info, StreamingContext context) { throw new NotImplementedException(); }
        public void OnDeserialization(object sender) { throw new NotImplementedException(); }

        private AssemblyNameFlags _flags;
        private byte[] _publicKey;
        private byte[] _publicKeyToken;
    }
}

