// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System.Globalization;
using System.Reflection;

namespace System.Resources
{
    public class ResourceManager
    {
        protected Assembly MainAssembly;

        public ResourceManager(Type resourceSource)
        {
            if (null == resourceSource)
            {
                throw new ArgumentNullException(nameof(resourceSource));
            }

            // TODO: NS2.0 ResourceManager
        }

        public ResourceManager(string baseName, Assembly assembly)
        {
            if (null == baseName)
            {
                throw new ArgumentNullException(nameof(baseName));
            }
            if (null == assembly)
            {
                throw new ArgumentNullException(nameof(assembly));
            }

            MainAssembly = assembly;

            // TODO: NS2.0 ResourceManager
        }

        public ResourceManager(string resourcesName)
        {
            // TODO: NS2.0 ResourceManager
        }

        public string GetString(string name)
        {
            return GetString(name, null);
        }

        // Looks up a resource value for a particular name.  Looks in the
        // specified CultureInfo, and if not found, all parent CultureInfos.
        // Returns null if the resource wasn't found.
        //
        public virtual string GetString(string name, CultureInfo culture)
        {
            if (null == name)
            {
                throw new ArgumentNullException(nameof(name));
            }

            // TODO: NS2.0 ResourceManager
            return name;
        }
    }
}
