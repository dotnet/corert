// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.IO;
using System.Text;
using System.Diagnostics;
using System.Collections.Generic;

using Internal.Reflection.Augments;

namespace System.Reflection.Runtime.Assemblies
{
    //
    // This is a private assembly name abstraction that's more suitable for use as keys in our caches.
    //
    //  - Immutable, unlike the public AssemblyName
    //  - Has a useful Equals() override, unlike the public AssemblyName.
    //
    // We use this as our internal interchange type and only convert to and from the public AssemblyName class at public boundaries.
    //
    internal sealed class RuntimeAssemblyName : IEquatable<RuntimeAssemblyName>
    {
        public RuntimeAssemblyName(String name, Version version, String cultureName, AssemblyNameFlags flags, byte[] publicKeyOrToken)
        {
            Debug.Assert(name != null);
            this.Name = name;

            // Optional version.
            this.Version = version;

            // Optional culture name.
            this.CultureName = cultureName;

            // Optional flags (this is actually an OR of the classic flags and the ContentType.)
            this.Flags = flags;

            // Optional public key (if Flags.PublicKey == true) or public key token.
            this.PublicKeyOrToken = publicKeyOrToken;
        }

        // Simple name.
        public String Name { get; }

        // Optional version.
        public Version Version { get; }

        // Optional culture name.
        public String CultureName { get; }

        // Optional flags (this is actually an OR of the classic flags and the ContentType.)
        public AssemblyNameFlags Flags { get; }

        // Optional public key (if Flags.PublicKey == true) or public key token.
        public byte[] PublicKeyOrToken { get; }

        // Equality - this compares every bit of data in the RuntimeAssemblyName which is acceptable for use as keys in a cache
        // where semantic duplication is permissible. This method is *not* meant to define ref->def binding rules or 
        // assembly binding unification rules.
        public bool Equals(RuntimeAssemblyName other)
        {
            if (other == null)
                return false;
            if (!this.Name.Equals(other.Name))
                return false;
            if (this.Version == null)
            {
                if (other.Version != null)
                    return false;
            }
            else
            {
                if (!this.Version.Equals(other.Version))
                    return false;
            }
            if (!String.Equals(this.CultureName, other.CultureName))
                return false;
            if (this.Flags != other.Flags)
                return false;

            byte[] thisPK = this.PublicKeyOrToken;
            byte[] otherPK = other.PublicKeyOrToken;
            if (thisPK == null)
            {
                if (otherPK != null)
                    return false;
            }
            else if (otherPK == null)
            {
                return false;
            }
            else if (thisPK.Length != otherPK.Length)
            {
                return false;
            }
            else
            {
                for (int i = 0; i < thisPK.Length; i++)
                {
                    if (thisPK[i] != otherPK[i])
                        return false;
                }
            }

            return true;
        }

        public sealed override bool Equals(Object obj)
        {
            RuntimeAssemblyName other = obj as RuntimeAssemblyName;
            if (other == null)
                return false;
            return Equals(other);
        }

        public sealed override int GetHashCode()
        {
            return this.Name.GetHashCode();
        }

        //
        // Converts an RuntimeAssemblyName to a freshly allocated AssemblyName with no data aliasing to any other object.
        //
        public AssemblyName ToAssemblyName()
        {
            AssemblyName assemblyName = new AssemblyName();
            CopyToAssemblyName(assemblyName);
            return assemblyName;
        }

        //
        // Copies a RuntimeAssemblyName into a freshly allocated AssemblyName with no data aliasing to any other object.
        //
        public void CopyToAssemblyName(AssemblyName blank)
        {
            blank.Name = this.Name;
            if (this.Version != null)
                blank.Version = this.Version;
            if (this.CultureName != null)
                blank.CultureName = this.CultureName;

            // Our "Flags" contain both the classic flags and the ProcessorArchitecture + ContentType bits. The public AssemblyName has separate properties for
            // these. The setters for these properties quietly mask out any bits intended for the other one, so we needn't do that ourselves..
            blank.Flags = AssemblyNameHelpers.ExtractAssemblyNameFlags(this.Flags);
            blank.ContentType = AssemblyNameHelpers.ExtractAssemblyContentType(this.Flags);
            blank.ProcessorArchitecture = AssemblyNameHelpers.ExtractProcessorArchitecture(this.Flags);

            if (this.PublicKeyOrToken != null)
            {
                // We must not hand out our own copy of the PKT to AssemblyName as AssemblyName is amazingly trusting and gives untrusted callers 
                // full freedom to scribble on its PKT array. (As do we but we only have trusted callers!)
                byte[] pkCopy = new byte[this.PublicKeyOrToken.Length];
                ((ICollection<byte>)(this.PublicKeyOrToken)).CopyTo(pkCopy, 0);

                if (0 != (this.Flags & AssemblyNameFlags.PublicKey))
                    blank.SetPublicKey(pkCopy);
                else
                    blank.SetPublicKeyToken(pkCopy);
            }

            return;
        }

        public String FullName
        {
            get
            {
                return AssemblyNameHelpers.ComputeDisplayName(this);
            }
        }
    }
}

