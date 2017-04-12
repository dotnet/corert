// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System.Globalization;
using System.Runtime.Serialization;
using System.Configuration.Assemblies;
using System.Reflection.Runtime.Assemblies;

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
                throw new ArgumentNullException(nameof(assemblyName));
            RuntimeAssemblyName runtimeAssemblyName = AssemblyNameParser.Parse(assemblyName);
            runtimeAssemblyName.CopyToAssemblyName(this);
        }

        // Constructs a new AssemblyName during deserialization. (Needs to public so we can whitelist in Reflection).
        public AssemblyName(SerializationInfo info, StreamingContext context)
        {
            //The graph is not valid until OnDeserialization() has been called.
            _siInfo = info;
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
                return AssemblyNameHelpers.ComputeDisplayName(this.ToRuntimeAssemblyName());
            }
        }

        public string Name { get; set; }
        public Version Version { get; set; }
        public string CodeBase { get; set; }
        public AssemblyHashAlgorithm HashAlgorithm { get; set; }
        public AssemblyVersionCompatibility VersionCompatibility { get; set; }
        public StrongNameKeyPair KeyPair { get; set; }

        public string EscapedCodeBase
        {
            get
            {
                if (CodeBase == null)
                    return null;
                else
                    return EscapeCodeBase(CodeBase);
            }
        }

        public byte[] GetPublicKey()
        {
            return _publicKey;
        }

        public byte[] GetPublicKeyToken()
        {
            if (_publicKeyToken == null)
                _publicKeyToken = AssemblyNameHelpers.ComputePublicKeyToken(_publicKey);
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

        public void GetObjectData(SerializationInfo info, StreamingContext context)
        {
            if (info == null)
                throw new ArgumentNullException(nameof(info));

            //Allocate the serialization info and serialize our static data.
            info.AddValue("_Name", Name);
            info.AddValue("_PublicKey", _publicKey, typeof(byte[]));
            info.AddValue("_PublicKeyToken", _publicKeyToken, typeof(byte[]));
            info.AddValue("_CultureInfo", (CultureInfo == null) ? -1 : CultureInfo.LCID);
            info.AddValue("_CodeBase", CodeBase);
            info.AddValue("_Version", Version);
            info.AddValue("_HashAlgorithm", HashAlgorithm, typeof(AssemblyHashAlgorithm));
            info.AddValue("_StrongNameKeyPair", KeyPair, typeof(StrongNameKeyPair));
            info.AddValue("_VersionCompatibility", VersionCompatibility, typeof(AssemblyVersionCompatibility));
            info.AddValue("_Flags", _flags, typeof(AssemblyNameFlags));

            // These are fields used (and set) internally by the full framework only. The fields are optional but the full framework
            // will catch an exception internally if they aren't there so to avoid that annoyance, we'll emit them using their default values.
            info.AddValue("_HashAlgorithmForControl", AssemblyHashAlgorithm.None, typeof(AssemblyHashAlgorithm));
            info.AddValue("_HashForControl", null, typeof(byte[]));
        }

        public void OnDeserialization(object sender)
        {
            // Deserialization has already been performed
            if (_siInfo == null)
                return;

            Name = _siInfo.GetString("_Name");
            _publicKey = (byte[])_siInfo.GetValue("_PublicKey", typeof(byte[]));
            _publicKeyToken = (byte[])_siInfo.GetValue("_PublicKeyToken", typeof(byte[]));
            int lcid = (int)_siInfo.GetInt32("_CultureInfo");
            if (lcid != -1)
                CultureInfo = new CultureInfo(lcid);

            CodeBase = _siInfo.GetString("_CodeBase");
            Version = (Version)_siInfo.GetValue("_Version", typeof(Version));
            HashAlgorithm = (AssemblyHashAlgorithm)_siInfo.GetValue("_HashAlgorithm", typeof(AssemblyHashAlgorithm));
            KeyPair = (StrongNameKeyPair)_siInfo.GetValue("_StrongNameKeyPair", typeof(StrongNameKeyPair));
            VersionCompatibility = (AssemblyVersionCompatibility)_siInfo.GetValue("_VersionCompatibility", typeof(AssemblyVersionCompatibility));
            _flags = (AssemblyNameFlags)_siInfo.GetValue("_Flags", typeof(AssemblyNameFlags));

            _siInfo = null;
        }

        public static AssemblyName GetAssemblyName(string assemblyFile) { throw new NotImplementedException(); } // TODO: https://github.com/dotnet/corert/issues/3253

        /// <summary>
        /// Compares the simple names disregarding Version, Culture and PKT. While this clearly does not
        /// match the intent of this api, this api has been broken this way since its debut and we cannot
        /// change its behavior now.
        /// </summary>
        public static bool ReferenceMatchesDefinition(AssemblyName reference, AssemblyName definition)
        {
            if (object.ReferenceEquals(reference, definition))
                return true;

            if (reference == null)
                throw new ArgumentNullException(nameof(reference));

            if (definition == null)
                throw new ArgumentNullException(nameof(definition));

            string refName = reference.Name ?? string.Empty;
            string defName = definition.Name ?? string.Empty;
            return refName.Equals(defName, StringComparison.OrdinalIgnoreCase);
        }

        internal static string EscapeCodeBase(string codebase) { throw new PlatformNotSupportedException(); }

        private AssemblyNameFlags _flags;
        private byte[] _publicKey;
        private byte[] _publicKeyToken;

        private SerializationInfo _siInfo;
    }
}

