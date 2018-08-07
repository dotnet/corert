// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System.Globalization;
using System.Runtime.Serialization;
using System.Configuration.Assemblies;

namespace System.Reflection
{
    public sealed class AssemblyName : ICloneable, IDeserializationCallback, ISerializable
    {
        private string _name;
        private byte[] _publicKey;
        private byte[] _publicKeyToken;
        private CultureInfo _cultureInfo;
        private string _codeBase;
        private Version _version;

        private StrongNameKeyPair _strongNameKeyPair;
        private AssemblyHashAlgorithm _hashAlgorithm;

        private AssemblyVersionCompatibility _versionCompatibility;
        private AssemblyNameFlags _flags;

        public AssemblyName()
        {
            _hashAlgorithm = AssemblyHashAlgorithm.None;
            _versionCompatibility = AssemblyVersionCompatibility.SameMachine;
            _flags = AssemblyNameFlags.None;
        }

        public AssemblyName(string assemblyName)
            : this()
        {
            if (assemblyName == null)
                throw new ArgumentNullException(nameof(assemblyName));
            RuntimeAssemblyName runtimeAssemblyName = AssemblyNameParser.Parse(assemblyName);
            runtimeAssemblyName.CopyToAssemblyName(this);
        }

        // Set and get the name of the assembly. If this is a weak Name
        // then it optionally contains a site. For strong assembly names, 
        // the name partitions up the strong name's namespace
        public string Name
        {
            get { return _name; }
            set { _name = value; }
        }

        public Version Version
        {
            get { return _version; }
            set { _version = value; }
        }

        // Locales, internally the LCID is used for the match.
        public CultureInfo CultureInfo
        {
            get { return _cultureInfo; }
            set { _cultureInfo = value; }
        }

        public string CultureName
        {
            get
            {
                return (_cultureInfo == null) ? null : _cultureInfo.Name;
            }
            set
            {
                _cultureInfo = (value == null) ? null : new CultureInfo(value);
            }
        }

        public string CodeBase
        {
            get { return _codeBase; }
            set { _codeBase = value; }
        }

        public string EscapedCodeBase
        {
            get
            {
                if (_codeBase == null)
                    return null;
                else
                    return EscapeCodeBase(_codeBase);
            }
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

        // Make a copy of this assembly name.
        public object Clone()
        {
            AssemblyName name = new AssemblyName();
            name._name = _name;
            name._publicKey = (byte[])_publicKey?.Clone();
            name._publicKeyToken = (byte[])_publicKeyToken?.Clone();
            name._cultureInfo = _cultureInfo;
            name._version = (Version)_version?.Clone();
            name._flags = _flags;
            name._codeBase = _codeBase;
            name._hashAlgorithm = _hashAlgorithm;
            name._versionCompatibility = _versionCompatibility;
            return name;
        }

		public static AssemblyName GetAssemblyName(string assemblyFile)
        {
            throw new PlatformNotSupportedException(SR.Arg_PlatformNotSupported_AssemblyName_GetAssemblyName);
        }

        // The public key that is used to verify an assemblies
        // inclusion into the namespace. If the public key associated
        // with the namespace cannot verify the assembly the assembly
        // will fail to load.
        public byte[] GetPublicKey()
        {
            return _publicKey;
        }

        public void SetPublicKey(byte[] publicKey)
        {
            _publicKey = publicKey;

            if (publicKey == null)
                _flags &= ~AssemblyNameFlags.PublicKey;
            else
                _flags |= AssemblyNameFlags.PublicKey;
        }

        // The compressed version of the public key formed from a truncated hash.
        // Will throw a SecurityException if _PublicKey is invalid
        public byte[] GetPublicKeyToken()
        {
            if (_publicKeyToken == null)
                _publicKeyToken = AssemblyNameHelpers.ComputePublicKeyToken(_publicKey);
            return _publicKeyToken;
        }

        public void SetPublicKeyToken(byte[] publicKeyToken)
        {
            _publicKeyToken = publicKeyToken;
        }

        // Flags modifying the name. So far the only flag is PublicKey, which
        // indicates that a full public key and not the compressed version is
        // present.
        // Processor Architecture flags are set only through ProcessorArchitecture
        // property and can't be set or retrieved directly
        // Content Type flags are set only through ContentType property and can't be
        // set or retrieved directly
        public AssemblyNameFlags Flags
        {
            get { return (AssemblyNameFlags)((uint)_flags & 0xFFFFF10F); }
            set
            {
                _flags &= unchecked((AssemblyNameFlags)0x00000EF0);
                _flags |= (value & unchecked((AssemblyNameFlags)0xFFFFF10F));
            }
        }

        public AssemblyHashAlgorithm HashAlgorithm
        {
            get { return _hashAlgorithm; }
            set { _hashAlgorithm = value; }
        }

        public AssemblyVersionCompatibility VersionCompatibility
        {
            get { return _versionCompatibility; }
            set { _versionCompatibility = value; }
        }

        public StrongNameKeyPair KeyPair
        {
            get { return _strongNameKeyPair; }
            set { _strongNameKeyPair = value; }
        }

        public string FullName
        {
            get
            {
                if (this.Name == null)
                    return string.Empty;
                // Do not call GetPublicKeyToken() here - that latches the result into AssemblyName which isn't a side effect we want.
                byte[] pkt = _publicKeyToken ?? AssemblyNameHelpers.ComputePublicKeyToken(_publicKey);
                return AssemblyNameFormatter.ComputeDisplayName(Name, Version, CultureName, pkt, Flags, ContentType);
            }
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
            throw new PlatformNotSupportedException();
        }

        public void OnDeserialization(object sender)
        {
            throw new PlatformNotSupportedException();
        }

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
    }
}

