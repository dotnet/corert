// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using Internal.Runtime.CompilerServices;

namespace System.Resources
{
    [RelocatedType("System.Resources.ResourceManager")]
    [AttributeUsage(AttributeTargets.Assembly, AllowMultiple = false)]
    public sealed class NeutralResourcesLanguageAttribute : Attribute
    {
        private readonly string _culture;
        private UltimateResourceFallbackLocation _fallbackLoc;

        public NeutralResourcesLanguageAttribute(string cultureName)
        {
            if (cultureName == null)
            {
                throw new ArgumentNullException(nameof(cultureName));
            }

            _culture = cultureName;
            _fallbackLoc = UltimateResourceFallbackLocation.MainAssembly;
        }

        public NeutralResourcesLanguageAttribute(String cultureName, UltimateResourceFallbackLocation location)
        {
            if (cultureName == null)
            {
                throw new ArgumentNullException(nameof(cultureName));
            }
            if (!Enum.IsDefined(typeof(UltimateResourceFallbackLocation), location))
            {
                throw new ArgumentException(SR.Format(SR.Arg_InvalidNeutralResourcesLanguage_FallbackLoc, location));
            }

            _culture = cultureName;
            _fallbackLoc = location;
        }

        public String CultureName
        {
            get { return _culture; }
        }

        public UltimateResourceFallbackLocation Location
        {
            get { return _fallbackLoc; }
        }
    }
}
