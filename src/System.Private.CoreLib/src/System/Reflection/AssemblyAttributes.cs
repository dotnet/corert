// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Diagnostics.Contracts;
using System.Configuration.Assemblies;

namespace System.Reflection
{
    [AttributeUsage(AttributeTargets.Assembly, Inherited = false)]
    public unsafe sealed class AssemblyAlgorithmIdAttribute : Attribute
    {
        public AssemblyAlgorithmIdAttribute(AssemblyHashAlgorithm algorithmId)
        {
            AlgorithmId = (uint)algorithmId;
        }

        [CLSCompliant(false)]
        public AssemblyAlgorithmIdAttribute(uint algorithmId)
        {
            AlgorithmId = algorithmId;
        }

        [CLSCompliant(false)]
        public uint AlgorithmId { get; }
    }

    [AttributeUsage(AttributeTargets.Assembly, Inherited = false)]
    [System.Runtime.InteropServices.ComVisible(true)]
    public sealed class AssemblyCopyrightAttribute : Attribute
    {
        private String _copyright;

        public AssemblyCopyrightAttribute(String copyright)
        {
            _copyright = copyright;
        }

        public String Copyright
        {
            get { return _copyright; }
        }
    }

    [AttributeUsage(AttributeTargets.Assembly, Inherited = false)]
    [System.Runtime.InteropServices.ComVisible(true)]
    public sealed class AssemblyTrademarkAttribute : Attribute
    {
        private String _trademark;

        public AssemblyTrademarkAttribute(String trademark)
        {
            _trademark = trademark;
        }

        public String Trademark
        {
            get { return _trademark; }
        }
    }


    [AttributeUsage(AttributeTargets.Assembly, Inherited = false)]
    [System.Runtime.InteropServices.ComVisible(true)]
    public sealed class AssemblyProductAttribute : Attribute
    {
        private String _product;

        public AssemblyProductAttribute(String product)
        {
            _product = product;
        }

        public String Product
        {
            get { return _product; }
        }
    }

    [AttributeUsage(AttributeTargets.Assembly, Inherited = false)]
    [System.Runtime.InteropServices.ComVisible(true)]
    public sealed class AssemblyCompanyAttribute : Attribute
    {
        private String _company;

        public AssemblyCompanyAttribute(String company)
        {
            _company = company;
        }

        public String Company
        {
            get { return _company; }
        }
    }

    [AttributeUsage(AttributeTargets.Assembly, Inherited = false)]
    [System.Runtime.InteropServices.ComVisible(true)]
    public sealed class AssemblyDescriptionAttribute : Attribute
    {
        private String _description;

        public AssemblyDescriptionAttribute(String description)
        {
            _description = description;
        }

        public String Description
        {
            get { return _description; }
        }
    }

    [AttributeUsage(AttributeTargets.Assembly, Inherited = false)]
    [System.Runtime.InteropServices.ComVisible(true)]
    public sealed class AssemblyTitleAttribute : Attribute
    {
        private String _title;

        public AssemblyTitleAttribute(String title)
        {
            _title = title;
        }

        public String Title
        {
            get { return _title; }
        }
    }

    [AttributeUsage(AttributeTargets.Assembly, Inherited = false)]
    [System.Runtime.InteropServices.ComVisible(true)]
    public sealed class AssemblyConfigurationAttribute : Attribute
    {
        private String _configuration;

        public AssemblyConfigurationAttribute(String configuration)
        {
            _configuration = configuration;
        }

        public String Configuration
        {
            get { return _configuration; }
        }
    }

    [AttributeUsage(AttributeTargets.Assembly, Inherited = false)]
    [System.Runtime.InteropServices.ComVisible(true)]
    public sealed class AssemblyDefaultAliasAttribute : Attribute
    {
        private String _defaultAlias;

        public AssemblyDefaultAliasAttribute(String defaultAlias)
        {
            _defaultAlias = defaultAlias;
        }

        public String DefaultAlias
        {
            get { return _defaultAlias; }
        }
    }

    [AttributeUsage(AttributeTargets.Assembly, Inherited = false)]
    [System.Runtime.InteropServices.ComVisible(true)]
    public sealed class AssemblyInformationalVersionAttribute : Attribute
    {
        private String _informationalVersion;

        public AssemblyInformationalVersionAttribute(String informationalVersion)
        {
            _informationalVersion = informationalVersion;
        }

        public String InformationalVersion
        {
            get { return _informationalVersion; }
        }
    }

    [AttributeUsage(AttributeTargets.Assembly, Inherited = false)]
    [System.Runtime.InteropServices.ComVisible(true)]
    public sealed class AssemblyFileVersionAttribute : Attribute
    {
        private String _version;

        public AssemblyFileVersionAttribute(String version)
        {
            if (version == null)
                throw new ArgumentNullException("version");
            Contract.EndContractBlock();
            _version = version;
        }

        public String Version
        {
            get { return _version; }
        }
    }

    [AttributeUsage(AttributeTargets.Assembly, Inherited = false)]
    [System.Runtime.InteropServices.ComVisible(true)]
    public unsafe sealed class AssemblyCultureAttribute : Attribute
    {
        private String _culture;

        public AssemblyCultureAttribute(String culture)
        {
            _culture = culture;
        }

        public String Culture
        {
            get { return _culture; }
        }
    }

    [AttributeUsage(AttributeTargets.Assembly, Inherited = false)]
    [System.Runtime.InteropServices.ComVisible(true)]
    public unsafe sealed class AssemblyVersionAttribute : Attribute
    {
        private String _version;

        public AssemblyVersionAttribute(String version)
        {
            _version = version;
        }

        public String Version
        {
            get { return _version; }
        }
    }

    [AttributeUsage(AttributeTargets.Assembly, Inherited = false)]
    [System.Runtime.InteropServices.ComVisible(true)]
    public sealed class AssemblyKeyFileAttribute : Attribute
    {
        private String _keyFile;

        public AssemblyKeyFileAttribute(String keyFile)
        {
            _keyFile = keyFile;
        }

        public String KeyFile
        {
            get { return _keyFile; }
        }
    }

    [AttributeUsage(AttributeTargets.Assembly, Inherited = false)]
    [System.Runtime.InteropServices.ComVisible(true)]
    public sealed class AssemblyDelaySignAttribute : Attribute
    {
        private bool _delaySign;

        public AssemblyDelaySignAttribute(bool delaySign)
        {
            _delaySign = delaySign;
        }

        public bool DelaySign
        {
            get
            { return _delaySign; }
        }
    }

    [AttributeUsage(AttributeTargets.Assembly, Inherited = false)]
    [System.Runtime.InteropServices.ComVisible(true)]
    public unsafe sealed class AssemblyFlagsAttribute : Attribute
    {
        private AssemblyNameFlags _flags;

        public AssemblyFlagsAttribute(AssemblyNameFlags assemblyFlags)
        {
            _flags = assemblyFlags;
        }

        public int AssemblyFlags
        {
            get { return (int)_flags; }
        }

        [Obsolete("This constructor has been deprecated. Please use AssemblyFlagsAttribute(AssemblyNameFlags) instead. http://go.microsoft.com/fwlink/?linkid=14202")]
        public AssemblyFlagsAttribute(int assemblyFlags)
        {
            _flags = (AssemblyNameFlags)assemblyFlags;
        }

        [Obsolete("This constructor has been deprecated. Please use AssemblyFlagsAttribute(AssemblyNameFlags) instead. http://go.microsoft.com/fwlink/?linkid=14202")]
        [CLSCompliant(false)]
        public AssemblyFlagsAttribute(uint flags)
        {
            _flags = (AssemblyNameFlags)flags;
        }

        [Obsolete("This property has been deprecated. Please use AssemblyFlags instead. http://go.microsoft.com/fwlink/?linkid=14202")]
        [CLSCompliant(false)]
        public uint Flags
        {
            get { return (uint)_flags; }
        }
    }

    [AttributeUsage(AttributeTargets.Assembly, AllowMultiple = true, Inherited = false)]
    public sealed class AssemblyMetadataAttribute : Attribute
    {
        private String _key;
        private String _value;

        public AssemblyMetadataAttribute(string key, string value)
        {
            _key = key;
            _value = value;
        }

        public string Key
        {
            get { return _key; }
        }

        public string Value
        {
            get { return _value; }
        }
    }

    [AttributeUsage(AttributeTargets.Assembly, Inherited = false, AllowMultiple = false)]
    public sealed class AssemblySignatureKeyAttribute : Attribute
    {
        private String _publicKey;
        private String _countersignature;

        public AssemblySignatureKeyAttribute(String publicKey, String countersignature)
        {
            _publicKey = publicKey;
            _countersignature = countersignature;
        }

        public String PublicKey
        {
            get { return _publicKey; }
        }

        public String Countersignature
        {
            get { return _countersignature; }
        }
    }
    [AttributeUsage(AttributeTargets.Assembly, Inherited = false)]
    [System.Runtime.InteropServices.ComVisible(true)]
    public sealed class AssemblyKeyNameAttribute : Attribute
    {
        private String _keyName;

        public AssemblyKeyNameAttribute(String keyName)
        {
            _keyName = keyName;
        }

        public String KeyName
        {
            get { return _keyName; }
        }
    }
}

