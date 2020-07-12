// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Resources;
using System.Runtime.CompilerServices;
using System.Threading;

namespace System
{
    internal partial class SR
    {
        private static ResourceManager s_resourceManager;

        private static ResourceManager ResourceManager
        {
            get
            {
                if (SR.s_resourceManager == null)
                {
                    SR.s_resourceManager = new ResourceManager(SR.ResourceType);
                }
                return SR.s_resourceManager;
            }
        }

        // This method is used to decide if we need to append the exception message parameters to the message when calling SR.Format. 
        // by default it returns false.
        // Native code generators can replace the value this returns based on user input at the time of native code generation.
        // Marked as NoInlining because if this is used in an AoT compiled app that is not compiled into a single file, the user
        // could compile each module with a different setting for this. We want to make sure there's a consistent behavior
        // that doesn't depend on which native module this method got inlined into.
        [MethodImpl(MethodImplOptions.NoInlining)]
        private static bool UsingResourceKeys()
        {
            return false;
        }

        // Needed for debugger integration
        internal static string GetResourceString(string resourceKey)
        {
            return GetResourceString(resourceKey, string.Empty);
        }

        internal static string GetResourceString(string resourceKey, string defaultString)
        {
            if (UsingResourceKeys())
                return defaultString ?? resourceKey;

            string resourceString = null;
            try { resourceString = InternalGetResourceString(resourceKey); }
            catch (MissingManifestResourceException) { }

            if (defaultString != null && resourceKey.Equals(resourceString, StringComparison.Ordinal))
            {
                return defaultString;
            }

            return resourceString;
        }

        private static object _lock = new object();
        private static List<string> _currentlyLoading;
        private static int _infinitelyRecursingCount;

        private static string InternalGetResourceString(string key)
        {
            if (key == null || key.Length == 0)
            {
                Debug.Fail("SR::GetResourceString with null or empty key.  Bug in caller, or weird recursive loading problem?");
                return key;
            }

            // We have a somewhat common potential for infinite 
            // loops with mscorlib's ResourceManager.  If "potentially dangerous"
            // code throws an exception, we will get into an infinite loop
            // inside the ResourceManager and this "potentially dangerous" code.
            // Potentially dangerous code includes the IO package, CultureInfo,
            // parts of the loader, some parts of Reflection, Security (including 
            // custom user-written permissions that may parse an XML file at
            // class load time), assembly load event handlers, etc.  Essentially,
            // this is not a bounded set of code, and we need to fix the problem.
            // Fortunately, this is limited to mscorlib's error lookups and is NOT
            // a general problem for all user code using the ResourceManager.

            // The solution is to make sure only one thread at a time can call 
            // GetResourceString.  Also, since resource lookups can be 
            // reentrant, if the same thread comes into GetResourceString
            // twice looking for the exact same resource name before 
            // returning, we're going into an infinite loop and we should 
            // return a bogus string.  

            bool lockTaken = false;
            try
            {
                Monitor.Enter(_lock, ref lockTaken);

                // Are we recursively looking up the same resource?  Note - our backout code will set
                // the ResourceHelper's currentlyLoading stack to null if an exception occurs.
                if (_currentlyLoading != null && _currentlyLoading.Count > 0 && _currentlyLoading.LastIndexOf(key) != -1)
                {
                    // We can start infinitely recursing for one resource lookup,
                    // then during our failure reporting, start infinitely recursing again.
                    // avoid that.
                    if (_infinitelyRecursingCount > 0)
                    {
                        return key;
                    }
                    _infinitelyRecursingCount++;
                }
                if (_currentlyLoading == null)
                    _currentlyLoading = new List<string>();

                _currentlyLoading.Add(key); // Push

                string s = ResourceManager.GetString(key, null);
                _currentlyLoading.RemoveAt(_currentlyLoading.Count - 1); // Pop

                Debug.Assert(s != null, "Managed resource string lookup failed.  Was your resource name misspelled?  Did you rebuild mscorlib after adding a resource to resources.txt?  Debug this w/ cordbg and bug whoever owns the code that called SR.GetResourceString.  Resource name was: \"" + key + "\"");
                return s ?? key;
            }
            catch
            {
                if (lockTaken)
                {
                    // Backout code - throw away potentially corrupt state
                    _currentlyLoading = null;
                }
                throw;
            }
            finally
            {
                if (lockTaken)
                {
                    Monitor.Exit(_lock);
                }
            }
        }

        internal static string Format(IFormatProvider provider, string resourceFormat, params object[] args)
        {
            if (args != null)
            {
                if (UsingResourceKeys())
                {
                    return resourceFormat + ", " + string.Join(", ", args);
                }

                return string.Format(provider, resourceFormat, args);
            }

            return resourceFormat;
        }

        internal static string Format(string resourceFormat, params object[] args)
        {
            if (args != null)
            {
                if (UsingResourceKeys())
                {
                    return resourceFormat + ", " + string.Join(", ", args);
                }

                return string.Format(resourceFormat, args);
            }

            return resourceFormat;
        }

        internal static string Format(string resourceFormat, object p1)
        {
            if (UsingResourceKeys())
            {
                return string.Join(", ", resourceFormat, p1);
            }

            return string.Format(resourceFormat, p1);
        }

        internal static string Format(string resourceFormat, object p1, object p2)
        {
            if (UsingResourceKeys())
            {
                return string.Join(", ", resourceFormat, p1, p2);
            }

            return string.Format(resourceFormat, p1, p2);
        }

        internal static string Format(string resourceFormat, object p1, object p2, object p3)
        {
            if (UsingResourceKeys())
            {
                return string.Join(", ", resourceFormat, p1, p2, p3);
            }
            return string.Format(resourceFormat, p1, p2, p3);
        }
    }
}
