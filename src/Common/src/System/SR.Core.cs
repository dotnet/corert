// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.
//
//   This is a standin for the SR class used throughout FX.
//

#if ENABLE_WINRT
using Internal.Runtime.Augments;
#endif // ENABLE_WINRT
using System.Runtime.CompilerServices;
using System.Text;

namespace System
{
    internal static partial class SR
    {
        //
        // System.Private.CoreLib and Reflection assemblies cannot depend on System.Resources so we'll call Windows Runtime ResourceManager 
        // directly using Internal.Runtime.Augments. 
        // For other assemblies, we use System.Resources.ResourceManager to do the resources lookup. it is important to 
        // not have such assemblies depend on internal contratcs as we can decide to make these assemblies portable.
        //

        private static Object s_resourceMap;
        private const string MoreInfoLink = @". For more information, visit http://go.microsoft.com/fwlink/?LinkId=623485";

        private static Object ResourceMap
        {
            get
            {
                if (SR.s_resourceMap == null)
                {
#if ENABLE_WINRT
                    SR.s_resourceMap = WinRTInterop.Callbacks.GetResourceMap(SR.s_resourcesName);
#else
                    SR.s_resourceMap = new object();
#endif // ENABLE_WINRT
                }
                return SR.s_resourceMap;
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

        // TODO: Resouce generation tool should be modified to call this version in release build
        [MethodImpl(MethodImplOptions.NoInlining)]
        internal static string GetResourceString(string resourceKey)
        {
#if ENABLE_WINRT
            return WinRTInterop.Callbacks.GetResourceString(ResourceMap, resourceKey, null);
#else
            return resourceKey;
#endif // ENABLE_WINRT
        }

        internal static string GetResourceString(string resourceKey, string defaultString)
        {
            string resourceString = GetResourceString(resourceKey);

            // if we are running on desktop, GetResourceString will just return resourceKey. so
            // in this case we'll return defaultString (if it is not null). 
            if (defaultString != null && resourceKey.Equals(resourceString))
            {
                return defaultString;
            }

            if (resourceString == null)
            {
                // It is not expected to have resourceString is null at this point.
                // this means our framework resources is missing while it is expected to be there. 
                // we have to throw on that or otherwise we’ll eventually get stack overflow exception.
                // we have to use hardcode the exception message here as we cannot lookup the resources for other keys.
                // We cannot throw MissingManifestResourceException as we cannot depend on the System.Resources here.

                throw new InvalidProgramException("Unable to find resource for the key " + resourceKey + ".");
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
