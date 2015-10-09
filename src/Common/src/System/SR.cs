// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System.Resources;
using System.Runtime.CompilerServices;
using System.Text;

namespace System
{
    internal static partial class SR
    {
        private static ResourceManager s_resourceManager;
        private const string MoreInfoLink = @". For more information, visit http://go.microsoft.com/fwlink/?LinkId=623485";

        private static ResourceManager ResourceManager
        {
            get
            {
                if (SR.s_resourceManager == null)
                {
                    // In ProjectN, the following constructor ResourceManager(Type) is going to be replaced by the private constructor ResourceManager(String)
                    // we'll pass s_resourcesName to this constructor
                    SR.s_resourceManager = new ResourceManager(SR.ResourceType);
                }
                return SR.s_resourceManager;
            }
        }

        // This method is used to decide if we need to append the exception message parameters to the message when calling SR.Format. 
        // by default it returns false. We overwrite the implementation of this method to return true through IL transformer 
        // when compiling ProjectN app as retail build. 
        [MethodImpl(MethodImplOptions.NoInlining)]
        private static bool UsingResourceKeys()
        {
            return false;
        }

        internal static string GetResourceString(string resourceKey, string defaultString)
        {
            string resourceString = null;
            try { resourceString = ResourceManager.GetString(resourceKey); } catch (MissingManifestResourceException) { }

            // if we are running on desktop, ResourceManager.GetString will just return resourceKey. so
            // in this case we'll return defaultString (if it is not null) 
            if (defaultString != null && resourceKey.Equals(resourceString, StringComparison.Ordinal))
            {
                return defaultString;
            }

            if (resourceString == null)
            {
                // we are missing the framework library resources which is not expected.
                // we have to use hardcode the exception message here as we cannot lookup the resources for other keys.
                throw new MissingManifestResourceException("Unable to find resource for the key " + resourceKey + ".");
            }

            return resourceString;
        }

        internal static string Format(string resourceFormat, params object[] args)
        {
            if (args != null)
            {
                if (UsingResourceKeys())
                {
                    return resourceFormat + String.Join(", ", args) + MoreInfoLink;
                }

                return String.Format(resourceFormat, args);
            }

            return resourceFormat;
        }

        [MethodImpl(MethodImplOptions.NoInlining)]
        internal static string Format(string resourceFormat, object p1)
        {
            if (UsingResourceKeys())
            {
                return String.Join(", ", resourceFormat, p1) + MoreInfoLink;
            }

            return String.Format(resourceFormat, p1);
        }

        [MethodImpl(MethodImplOptions.NoInlining)]
        internal static string Format(string resourceFormat, object p1, object p2)
        {
            if (UsingResourceKeys())
            {
                return String.Join(", ", resourceFormat, p1, p2) + MoreInfoLink;
            }

            return String.Format(resourceFormat, p1, p2);
        }

        [MethodImpl(MethodImplOptions.NoInlining)]
        internal static string Format(string resourceFormat, object p1, object p2, object p3)
        {
            if (UsingResourceKeys())
            {
                return String.Join(", ", resourceFormat, p1, p2, p3) + MoreInfoLink;
            }
            return String.Format(resourceFormat, p1, p2, p3);
        }
    }
}
