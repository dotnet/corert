// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.


/*============================================================
**
  Type:  Assembly
**
==============================================================*/

using global::System;
using global::System.IO;
using global::System.Collections.Generic;
using global::Internal.Reflection.Augments;

namespace System.Reflection
{
    public abstract class Assembly
    {
        protected Assembly()
        {
        }

        public virtual IEnumerable<CustomAttributeData> CustomAttributes
        {
            get
            {
                throw NotImplemented.ByDesign;
            }
        }

        public abstract IEnumerable<TypeInfo> DefinedTypes { get; }

        public virtual IEnumerable<Type> ExportedTypes
        {
            get
            {
                throw NotImplemented.ByDesign;
            }
        }

        public virtual String FullName
        {
            get
            {
                throw NotImplemented.ByDesign;
            }
        }

        public virtual bool IsDynamic
        {
            get
            {
                return false;
            }
        }

        public virtual Module ManifestModule
        {
            get
            {
                throw NotImplemented.ByDesign;
            }
        }

        public abstract IEnumerable<Module> Modules { get; }

        // Equals() and GetHashCode() implement reference equality for compatibility with desktop.
        // Unfortunately, this means that implementors who don't unify instances will be on the hook
        // to override these implementations to test for semantic equivalence.
        public override bool Equals(Object o)
        {
            return base.Equals(o);
        }

        public override int GetHashCode()
        {
            return base.GetHashCode();
        }

        public virtual ManifestResourceInfo GetManifestResourceInfo(String resourceName)
        {
            throw NotImplemented.ByDesign;
        }

        public virtual String[] GetManifestResourceNames()
        {
            throw NotImplemented.ByDesign;
        }

        public virtual Stream GetManifestResourceStream(String name)
        {
            throw NotImplemented.ByDesign;
        }

        public virtual AssemblyName GetName()
        {
            throw NotImplemented.ByDesign;
        }

        public virtual Type GetType(String name)
        {
            return GetType(name, throwOnError: false, ignoreCase: false);
        }

        public virtual Type GetType(String name, bool throwOnError, bool ignoreCase)
        {
            throw NotImplemented.ByDesign;
        }

        public static Assembly Load(AssemblyName assemblyRef)
        {
            return ReflectionAugments.ReflectionCoreCallbacks.Load(assemblyRef);
        }

        public override String ToString()
        {
            String displayName = FullName;
            if (displayName == null)
                return base.ToString();
            else
                return displayName;
        }
    }
}

