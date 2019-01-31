// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Reflection;
using System.Text;
using Internal.Reflection.Augments;

namespace System.Resources
{
    internal partial class ManifestBasedResourceGroveler
    {
        // Internal version of GetSatelliteAssembly that avoids throwing FileNotFoundException
        private static Assembly InternalGetSatelliteAssembly(Assembly mainAssembly,
                                                             CultureInfo culture,
                                                             Version version)
        {
            AssemblyName mainAssemblyAn = mainAssembly.GetName();
            AssemblyName an = new AssemblyName();

            an.CultureInfo = culture;
            an.Name = mainAssemblyAn.Name + ".resources";
            an.SetPublicKeyToken(mainAssemblyAn.GetPublicKeyToken());
            an.Flags = mainAssemblyAn.Flags;
            an.Version = version ?? mainAssemblyAn.Version;

            Assembly retAssembly = ReflectionAugments.ReflectionCoreCallbacks.Load(an, false);

            if (retAssembly == mainAssembly)
            {
                retAssembly = null;
            }

            return retAssembly;
        }

        internal static bool GetNeutralResourcesLanguageAttribute(Assembly assemblyHandle, ref string cultureName, out short fallbackLocation)
        {
            fallbackLocation = 0;

            foreach (CustomAttributeData attribute in assemblyHandle.CustomAttributes)
            {
                if (attribute.AttributeType.Equals(typeof(NeutralResourcesLanguageAttribute)))
                {
                    foreach (CustomAttributeTypedArgument cata in attribute.ConstructorArguments)
                    {
                        if (cata.ArgumentType.Equals(typeof(string)))
                        {
                            cultureName = (string)cata.Value;
                        }
                        else if (cata.ArgumentType.Equals(typeof(int)))
                        {
                            fallbackLocation = (short)cata.Value;
                        }
                    }
                    return true;
                }
            }

            return false;
        }
    }
}
