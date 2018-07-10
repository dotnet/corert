// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using System.Runtime;
using System.Runtime.ExceptionServices;
using System.Runtime.InteropServices;
using System.Runtime.Versioning;
using System.Text;

namespace System
{
    public static partial class AppContext
    {
        [Flags]
        private enum SwitchValueState
        {
            HasFalseValue = 0x1,
            HasTrueValue = 0x2,
            HasLookedForOverride = 0x4,
            UnknownValue = 0x8 // Has no default and could not find an override
        }
        private static readonly Dictionary<string, SwitchValueState> s_switchMap = new Dictionary<string, SwitchValueState>();
        private static Dictionary<string, object> s_localStore = new Dictionary<string, object>();
        private static string s_defaultBaseDirectory;
        // AppDomain lives in CoreFX, but some of this class's events need to pass in AppDomains, so people registering those
        // events need to first pass in an AppDomain that we stash here to pass back in the events.
        private static object s_appDomain;

        static AppContext()
        {
            // populate the AppContext with the default set of values
            AppContextDefaultValues.PopulateDefaultValues();
        }

        public static void SetAppDomain(object appDomain)
        {
            s_appDomain = appDomain;
        }

        public static string TargetFrameworkName => Assembly.GetEntryAssembly()?.GetCustomAttribute<TargetFrameworkAttribute>()?.FrameworkName;

        public static string BaseDirectory
        {
            get
            {
                // The value of APP_CONTEXT_BASE_DIRECTORY key has to be a string and it is not allowed to be any other type. 
                // Otherwise the caller will get invalid cast exception
                return (string)GetData("APP_CONTEXT_BASE_DIRECTORY") ?? 
                    (s_defaultBaseDirectory ?? (s_defaultBaseDirectory = GetBaseDirectoryCore()));
            }
        }

        public static object GetData(string name)
        {
            if (name == null)
                throw new ArgumentNullException(nameof(name));

            object data;
            lock (((ICollection)s_localStore).SyncRoot)
            {
                s_localStore.TryGetValue(name, out data);
            }

            return data;
        }

        public static void SetData(string name, object data)
        {
            if (name == null)
                throw new ArgumentNullException(nameof(name));

            lock (((ICollection)s_localStore).SyncRoot)
            {
                s_localStore[name] = data;
            }
        }

        public static event UnhandledExceptionEventHandler UnhandledException;

        public static event System.EventHandler<FirstChanceExceptionEventArgs> FirstChanceException;

        public static event System.EventHandler ProcessExit;
        internal static event System.EventHandler Unloading;

        private static void OnUnhandledException(object sender, UnhandledExceptionEventArgs e)
        {
            var unhandledException = UnhandledException;
            if (unhandledException != null)
            {
                unhandledException(sender, e);
            }
        }

        internal static void OnFirstChanceException(object sender, FirstChanceExceptionEventArgs e)
        {
            var firstChanceException = FirstChanceException;
            if (firstChanceException != null)
            {
                firstChanceException(sender, e);
            }
        }

        [RuntimeExport("OnFirstChanceException")]
        internal static void OnFirstChanceException(object e)
        {
            OnFirstChanceException(s_appDomain, new FirstChanceExceptionEventArgs((Exception)e));
        }

        private static void OnProcessExit(object sender, EventArgs e)
        {
            var processExit = ProcessExit;
            if (processExit != null)
            {
                processExit(sender, e);
            }
        }

        private static void OnUnloading(object sender, EventArgs e)
        {
            var unloading = Unloading;
            if (unloading != null)
            {
                unloading(sender, e);
            }
        }

        #region Switch APIs
        /// <summary>
        /// Try to get the value of the switch.
        /// </summary>
        /// <param name="switchName">The name of the switch</param>
        /// <param name="isEnabled">A variable where to place the value of the switch</param>
        /// <returns>A return value of true represents that the switch was set and <paramref name="isEnabled"/> contains the value of the switch</returns>
        public static bool TryGetSwitch(string switchName, out bool isEnabled)
        {
            if (switchName == null)
                throw new ArgumentNullException(nameof(switchName));
            if (switchName.Length == 0)
                throw new ArgumentException(SR.Argument_EmptyName, nameof(switchName));

            // By default, the switch is not enabled.
            isEnabled = false;

            SwitchValueState switchValue;
            lock (s_switchMap)
            {
                if (s_switchMap.TryGetValue(switchName, out switchValue))
                {
                    // The value is in the dictionary. 
                    // There are 3 cases here:
                    // 1. The value of the switch is 'unknown'. This means that the switch name is not known to the system (either via defaults or checking overrides).
                    //    Example: This is the case when, during a servicing event, a switch is added to System.Xml which ships before mscorlib. The value of the switch
                    //             Will be unknown to mscorlib.dll and we want to prevent checking the overrides every time we check this switch
                    // 2. The switch has a valid value AND we have read the overrides for it
                    //    Example: TryGetSwitch is called for a switch set via SetSwitch
                    // 3. The switch has the default value and we need to check for overrides
                    //    Example: TryGetSwitch is called for the first time for a switch that has a default value 

                    // 1. The value is unknown
                    if (switchValue == SwitchValueState.UnknownValue)
                    {
                        isEnabled = false;
                        return false;
                    }

                    // We get the value of isEnabled from the value that we stored in the dictionary
                    isEnabled = (switchValue & SwitchValueState.HasTrueValue) == SwitchValueState.HasTrueValue;

                    // 2. The switch has a valid value AND we have checked for overrides
                    if ((switchValue & SwitchValueState.HasLookedForOverride) == SwitchValueState.HasLookedForOverride)
                    {
                        return true;
                    }

                    // Update the switch in the dictionary to mark it as 'checked for override'
                    s_switchMap[switchName] = (isEnabled ? SwitchValueState.HasTrueValue : SwitchValueState.HasFalseValue)
                                                | SwitchValueState.HasLookedForOverride;

                    return true;
                }
                else
                {
                    // The value is NOT in the dictionary
                    // In this case we need to see if we have an override defined for the value.
                    // There are 2 cases:
                    // 1. The value has an override specified. In this case we need to add the value to the dictionary 
                    //    and mark it as checked for overrides
                    //    Example: In a servicing event, System.Xml introduces a switch and an override is specified.
                    //             The value is not found in mscorlib (as System.Xml ships independent of mscorlib)
                    // 2. The value does not have an override specified
                    //    In this case, we want to capture the fact that we looked for a value and found nothing by adding 
                    //    an entry in the dictionary with the 'sentinel' value of 'SwitchValueState.UnknownValue'.
                    //    Example: This will prevent us from trying to find overrides for values that we don't have in the dictionary

                    // 1. The value has an override specified.
                    bool overrideValue;
                    if (AppContextDefaultValues.TryGetSwitchOverride(switchName, out overrideValue))
                    {
                        isEnabled = overrideValue;

                        // Update the switch in the dictionary to mark it as 'checked for override'
                        s_switchMap[switchName] = (isEnabled ? SwitchValueState.HasTrueValue : SwitchValueState.HasFalseValue)
                                                    | SwitchValueState.HasLookedForOverride;

                        return true;
                    }

                    // 2. The value does not have an override.
                    s_switchMap[switchName] = SwitchValueState.UnknownValue;
                }
            }
            return false; // we did not find a value for the switch
        }

        /// <summary>
        /// Assign a switch a value
        /// </summary>
        /// <param name="switchName">The name of the switch</param>
        /// <param name="isEnabled">The value to assign</param>
        public static void SetSwitch(string switchName, bool isEnabled)
        {
            if (switchName == null)
                throw new ArgumentNullException(nameof(switchName));
            if (switchName.Length == 0)
                throw new ArgumentException(SR.Argument_EmptyName, nameof(switchName));

            SwitchValueState switchValue = (isEnabled ? SwitchValueState.HasTrueValue : SwitchValueState.HasFalseValue)
                                            | SwitchValueState.HasLookedForOverride;

            lock (s_switchMap)
            {
                // Store the new value and the fact that we checked in the dictionary
                s_switchMap[switchName] = switchValue;
            }
        }

        /// <summary>
        /// This method is going to be called from the AppContextDefaultValues class when setting up the 
        /// default values for the switches. !!!! This method is called during the static constructor so it does not
        /// take a lock !!!! If you are planning to use this outside of that, please ensure proper locking.
        /// </summary>
        internal static void DefineSwitchDefault(string switchName, bool isEnabled)
        {
            s_switchMap[switchName] = isEnabled ? SwitchValueState.HasTrueValue : SwitchValueState.HasFalseValue;
        }
        #endregion
    }
}
