// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.


/*============================================================
**
  Type:  AssemblyName
**
==============================================================*/

using global::System;
using global::System.Globalization;
using global::Internal.Reflection.Augments;

namespace System.Reflection
{
    public sealed class AssemblyName
    {
        private String _Name;                  // Name
        private byte[] _PublicKey;
        private byte[] _PublicKeyToken;
        private String _CultureName;
        private Version _Version;
        private AssemblyNameFlags _Flags;


        public AssemblyName()
        {
            _Flags = AssemblyNameFlags.None;
        }


        public AssemblyName(String assemblyName)
        {
            if (assemblyName == null)
                throw new ArgumentNullException("assemblyName");
            ReflectionAugments.ReflectionCoreCallbacks.InitializeAssemblyName(this, assemblyName);
        }

        public ProcessorArchitecture ProcessorArchitecture
        {
            get
            {
                int x = (((int)_Flags) & 0x70) >> 4;
                if (x > 5)
                    x = 0;
                return (ProcessorArchitecture)x;
            }
            set
            {
                int x = ((int)value) & 0x07;
                if (x <= 5)
                {
                    _Flags = (AssemblyNameFlags)((int)_Flags & 0xFFFFFF0F);
                    _Flags |= (AssemblyNameFlags)(x << 4);
                }
            }
        }

        public AssemblyContentType ContentType
        {
            get
            {
                int x = (((int)_Flags) & 0x00000E00) >> 9;
                if (x > 1)
                    x = 0;
                return (AssemblyContentType)x;
            }
            set
            {
                int x = ((int)value) & 0x07;
                if (x <= 1)
                {
                    _Flags = (AssemblyNameFlags)((int)_Flags & 0xFFFFF1FF);
                    _Flags |= (AssemblyNameFlags)(x << 9);
                }
            }
        }


        public String CultureName
        {
            get
            {
                return _CultureName;
            }

            set
            {
                String newName = value;
                if (newName != null)
                {
                    // For desktop compat, we must validate and normalize the culture name.
                    newName = new CultureInfo(newName).Name;
                }
                _CultureName = newName;
            }
        }

        public AssemblyNameFlags Flags
        {
            get { return (AssemblyNameFlags)((uint)_Flags & 0xFFFFF10F); }
            set
            {
                _Flags &= unchecked((AssemblyNameFlags)0x00000EF0);
                _Flags |= (value & unchecked((AssemblyNameFlags)0xFFFFF10F));
            }
        }

        public String FullName
        {
            get
            {
                if (this.Name == null)
                    return String.Empty;
                return ReflectionAugments.ReflectionCoreCallbacks.ComputeAssemblyNameFullName(this);
            }
        }

        public String Name
        {
            get { return _Name; }
            set { _Name = value; }
        }

        public Version Version
        {
            get
            {
                return _Version;
            }
            set
            {
                _Version = value;
            }
        }
        public byte[] GetPublicKey()
        {
            return _PublicKey;
        }

        public byte[] GetPublicKeyToken()
        {
            if (_PublicKeyToken == null)
                _PublicKeyToken = ReflectionAugments.ReflectionCoreCallbacks.ComputePublicKeyToken(_PublicKey);
            return _PublicKeyToken;
        }

        public void SetPublicKey(byte[] publicKey)
        {
            _PublicKey = publicKey;

            if (publicKey == null)
                _Flags &= ~AssemblyNameFlags.PublicKey;
            else
                _Flags |= AssemblyNameFlags.PublicKey;
        }

        public void SetPublicKeyToken(byte[] publicKeyToken)
        {
            _PublicKeyToken = publicKeyToken;
        }

        public override String ToString()
        {
            String s = FullName;
            if (s == null)
                return base.ToString();
            else
                return s;
        }
    }
}

