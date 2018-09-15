// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using Microsoft.Win32.SafeHandles;
using System;
using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;
using System.IO;
using System.Security.AccessControl;
using System.Text;

namespace Microsoft.Win32
{
    /// <summary>Registry encapsulation. To get an instance of a RegistryKey use the Registry class's static members then call OpenSubKey.</summary>
#if REGISTRY_ASSEMBLY
    public
#else
    internal
#endif
    sealed partial class RegistryKey : IDisposable
    {
        public static readonly IntPtr HKEY_CLASSES_ROOT = new IntPtr(unchecked((int)0x80000000));
        public static readonly IntPtr HKEY_CURRENT_USER = new IntPtr(unchecked((int)0x80000001));
        public static readonly IntPtr HKEY_LOCAL_MACHINE = new IntPtr(unchecked((int)0x80000002));
        public static readonly IntPtr HKEY_USERS = new IntPtr(unchecked((int)0x80000003));
        public static readonly IntPtr HKEY_PERFORMANCE_DATA = new IntPtr(unchecked((int)0x80000004));
        public static readonly IntPtr HKEY_CURRENT_CONFIG = new IntPtr(unchecked((int)0x80000005));

        /// <summary>Names of keys.  This array must be in the same order as the HKEY values listed above.</summary>
        private static readonly string[] s_hkeyNames = new string[]
        {
            "HKEY_CLASSES_ROOT",
            "HKEY_CURRENT_USER",
            "HKEY_LOCAL_MACHINE",
            "HKEY_USERS",
            "HKEY_PERFORMANCE_DATA",
            "HKEY_CURRENT_CONFIG"
        };

        // MSDN defines the following limits for registry key names & values:
        // Key Name: 255 characters
        // Value name:  16,383 Unicode characters
        // Value: either 1 MB or current available memory, depending on registry format.
        private const int MaxKeyLength = 255;
        private const int MaxValueLength = 16383;

        private volatile SafeRegistryHandle _hkey;
        private volatile string _keyName;
        private volatile bool _remoteKey;
        private volatile StateFlags _state;
        private volatile RegistryView _regView = RegistryView.Default;

        /// <summary>
        /// Creates a RegistryKey. This key is bound to hkey, if writable is <b>false</b> then no write operations will be allowed.
        /// </summary>
        private RegistryKey(SafeRegistryHandle hkey, bool writable, RegistryView view) :
            this(hkey, writable, false, false, false, view)
        {
        }

        /// <summary>
        /// Creates a RegistryKey.
        /// This key is bound to hkey, if writable is <b>false</b> then no write operations
        /// will be allowed. If systemkey is set then the hkey won't be released
        /// when the object is GC'ed.
        /// The remoteKey flag when set to true indicates that we are dealing with registry entries
        /// on a remote machine and requires the program making these calls to have full trust.
        /// </summary>
        private RegistryKey(SafeRegistryHandle hkey, bool writable, bool systemkey, bool remoteKey, bool isPerfData, RegistryView view)
        {
            ValidateKeyView(view);

            _hkey = hkey;
            _keyName = "";
            _remoteKey = remoteKey;
            _regView = view;

            if (systemkey)
            {
                _state |= StateFlags.SystemKey;
            }
            if (writable)
            {
                _state |= StateFlags.WriteAccess;
            }
            if (isPerfData)
            {
                _state |= StateFlags.PerfData;
            }
        }

        public void Flush()
        {
            FlushCore();
        }

        public void Dispose()
        {
            if (_hkey != null)
            {
                if (!IsSystemKey())
                {
                    try
                    {
                        _hkey.Dispose();
                    }
                    catch (IOException)
                    {
                        // we don't really care if the handle is invalid at this point
                    }
                    finally
                    {
                        _hkey = null;
                    }
                }
                else if (IsPerfDataKey())
                {
                    ClosePerfDataKey();
                }
            }
        }

        /// <summary>Creates a new subkey, or opens an existing one.</summary>
        /// <param name="subkey">Name or path to subkey to create or open.</param>
        /// <returns>The subkey, or <b>null</b> if the operation failed.</returns>
        [SuppressMessage("Microsoft.Concurrency", "CA8001", Justification = "Reviewed for thread safety")]
        public RegistryKey CreateSubKey(string subkey)
        {
            return CreateSubKey(subkey, IsWritable());
        }

        public RegistryKey CreateSubKey(string subkey, bool writable)
        {
            return CreateSubKeyInternal(subkey, writable, RegistryOptions.None);
        }

        public RegistryKey CreateSubKey(string subkey, bool writable, RegistryOptions options)
        {
            return CreateSubKeyInternal(subkey, writable, options);
        }

        private RegistryKey CreateSubKeyInternal(string subkey, bool writable, RegistryOptions registryOptions)
        {
            ValidateKeyOptions(registryOptions);
            ValidateKeyName(subkey);
            EnsureWriteable();
            subkey = FixupName(subkey); // Fixup multiple slashes to a single slash

            // only keys opened under read mode is not writable
            if (!_remoteKey)
            {
                RegistryKey key = InternalOpenSubKey(subkey, writable);
                if (key != null)
                {
                    // Key already exits
                    return key;
                }
            }

            return CreateSubKeyInternalCore(subkey, writable, registryOptions);
        }

        public void DeleteValue(string name, bool throwOnMissingValue)
        {
            EnsureWriteable();
            DeleteValueCore(name, throwOnMissingValue);
        }

        public static RegistryKey OpenBaseKey(RegistryHive hKey)
        {
            return OpenBaseKey(hKey, RegistryView.Default);
        }
        
        public static RegistryKey OpenBaseKey(RegistryHive hKey, RegistryView view)
        {
            ValidateKeyView(view);
            return OpenBaseKeyCore(hKey, view);
        }

        /// <summary>
        /// Retrieves a subkey. If readonly is <b>true</b>, then the subkey is opened with
        /// read-only access.
        /// </summary>
        /// <returns>the Subkey requested, or <b>null</b> if the operation failed.</returns>
        public RegistryKey OpenSubKey(string name, bool writable) =>
            OpenSubKey(name, GetRegistryKeyRights(writable));

        public RegistryKey OpenSubKey(string name, RegistryRights rights)
        {
            ValidateKeyName(name);
            EnsureNotDisposed();
            name = FixupName(name); // Fixup multiple slashes to a single slash
            return InternalOpenSubKeyCore(name, rights, throwOnPermissionFailure: true);
        }

        /// <summary>
        /// This required no security checks. This is to get around the Deleting SubKeys which only require
        /// write permission. They call OpenSubKey which required read. Now instead call this function w/o security checks
        /// </summary>
        private RegistryKey InternalOpenSubKey(string name, bool writable)
        {
            ValidateKeyName(name);
            EnsureNotDisposed();
            return InternalOpenSubKeyCore(name, GetRegistryKeyRights(writable), throwOnPermissionFailure: false);
        }

        /// <summary>Returns a subkey with read only permissions.</summary>
        /// <param name="name">Name or path of subkey to open.</param>
        /// <returns>The Subkey requested, or <b>null</b> if the operation failed.</returns>
        public RegistryKey OpenSubKey(string name)
        {
            return OpenSubKey(name, false);
        }

        /// <summary>Retrieves the count of subkeys.</summary>
        /// <returns>A count of subkeys.</returns>
        public int SubKeyCount
        {
            get { return InternalSubKeyCount(); }
        }

        public RegistryView View
        {
            get
            {
                EnsureNotDisposed();
                return _regView;
            }
        }

        public SafeRegistryHandle Handle
        {
            get
            {
                EnsureNotDisposed();
                return IsSystemKey() ? SystemKeyHandle : _hkey;
            }
        }

        public static RegistryKey FromHandle(SafeRegistryHandle handle)
        {
            return FromHandle(handle, RegistryView.Default);
        }

        public static RegistryKey FromHandle(SafeRegistryHandle handle, RegistryView view)
        {
            if (handle == null) throw new ArgumentNullException(nameof(handle));
            ValidateKeyView(view);

            return new RegistryKey(handle, writable: true, view: view);
        }

        private int InternalSubKeyCount()
        {
            EnsureNotDisposed();
            return InternalSubKeyCountCore();
        }

        /// <summary>Retrieves an array of strings containing all the subkey names.</summary>
        /// <returns>All subkey names.</returns>
        public string[] GetSubKeyNames()
        {
            return InternalGetSubKeyNames();
        }

        private string[] InternalGetSubKeyNames()
        {
            int subkeys = InternalSubKeyCount();
            return subkeys > 0 ?
                InternalGetSubKeyNamesCore(subkeys) :
                Array.Empty<string>();
        }

        /// <summary>Retrieves the count of values.</summary>
        /// <returns>A count of values.</returns>
        public int ValueCount
        {
            get
            {
                EnsureNotDisposed();
                return InternalValueCountCore();
            }
        }

        /// <summary>Retrieves an array of strings containing all the value names.</summary>
        /// <returns>All value names.</returns>
        public string[] GetValueNames()
        {
            int values = ValueCount;
            return values > 0 ?
                GetValueNamesCore(values) :
                Array.Empty<string>();
        }

        /// <summary>Retrieves the specified value. <b>null</b> is returned if the value doesn't exist</summary>
        /// <remarks>
        /// Note that <var>name</var> can be null or "", at which point the
        /// unnamed or default value of this Registry key is returned, if any.
        /// </remarks>
        /// <param name="name">Name of value to retrieve.</param>
        /// <returns>The data associated with the value.</returns>
        public object GetValue(string name)
        {
            return InternalGetValue(name, null, false, true);
        }

        /// <summary>Retrieves the specified value. <i>defaultValue</i> is returned if the value doesn't exist.</summary>
        /// <remarks>
        /// Note that <var>name</var> can be null or "", at which point the
        /// unnamed or default value of this Registry key is returned, if any.
        /// The default values for RegistryKeys are OS-dependent.  NT doesn't
        /// have them by default, but they can exist and be of any type.  On
        /// Win95, the default value is always an empty key of type REG_SZ.
        /// Win98 supports default values of any type, but defaults to REG_SZ.
        /// </remarks>
        /// <param name="name">Name of value to retrieve.</param>
        /// <param name="defaultValue">Value to return if <i>name</i> doesn't exist.</param>
        /// <returns>The data associated with the value.</returns>
        public object GetValue(string name, object defaultValue)
        {
            return InternalGetValue(name, defaultValue, false, true);
        }

        public object GetValue(string name, object defaultValue, RegistryValueOptions options)
        {
            if (options < RegistryValueOptions.None || options > RegistryValueOptions.DoNotExpandEnvironmentNames)
            {
                throw new ArgumentException(SR.Format(SR.Arg_EnumIllegalVal, (int)options), nameof(options));
            }
            bool doNotExpand = (options == RegistryValueOptions.DoNotExpandEnvironmentNames);
            return InternalGetValue(name, defaultValue, doNotExpand, checkSecurity: true);
        }

        public object InternalGetValue(string name, object defaultValue, bool doNotExpand, bool checkSecurity)
        {
            if (checkSecurity)
            {
                EnsureNotDisposed();
            }

            // Name can be null!  It's the most common use of RegQueryValueEx
            return InternalGetValueCore(name, defaultValue, doNotExpand);
        }

        public RegistryValueKind GetValueKind(string name)
        {
            EnsureNotDisposed();
            return GetValueKindCore(name);
        }

        public string Name
        {
            get
            {
                EnsureNotDisposed();
                return _keyName;
            }
        }

        //The actual api is SetValue(string name, object value, RegistryValueKind valueKind) but we only need to set Strings
        // so this is a cut-down version that supports on that.
        internal void SetValue(string name, string value)
        {
            if (value == null)
                throw new ArgumentNullException(nameof(value));

            if (name != null && name.Length > MaxValueLength)
                throw new ArgumentException(SR.Arg_RegValStrLenBug, nameof(name));

            EnsureWriteable();
            SetValueCore(name, value);
        }

        /// <summary>Retrieves a string representation of this key.</summary>
        /// <returns>A string representing the key.</returns>
        public override string ToString()
        {
            EnsureNotDisposed();
            return _keyName;
        }

        private static string FixupName(string name)
        {
            Debug.Assert(name != null, "[FixupName]name!=null");
            if (name.IndexOf('\\') == -1)
            {
                return name;
            }

            StringBuilder sb = new StringBuilder(name);
            FixupPath(sb);
            int temp = sb.Length - 1;
            if (temp >= 0 && sb[temp] == '\\') // Remove trailing slash
            {
                sb.Length = temp;
            }

            return sb.ToString();
        }

        private static void FixupPath(StringBuilder path)
        {
            Debug.Assert(path != null);

            int length = path.Length;
            bool fixup = false;
            char markerChar = (char)0xFFFF;

            int i = 1;
            while (i < length - 1)
            {
                if (path[i] == '\\')
                {
                    i++;
                    while (i < length && path[i] == '\\')
                    {
                        path[i] = markerChar;
                        i++;
                        fixup = true;
                    }
                }
                i++;
            }

            if (fixup)
            {
                i = 0;
                int j = 0;
                while (i < length)
                {
                    if (path[i] == markerChar)
                    {
                        i++;
                        continue;
                    }
                    path[j] = path[i];
                    i++;
                    j++;
                }
                path.Length += j - i;
            }
        }

        private void EnsureNotDisposed()
        {
        }

        private void EnsureWriteable()
        {
        }

        private static void ValidateKeyName(string name)
        {
        }

        private static void ValidateKeyOptions(RegistryOptions options)
        {
        }

        private static void ValidateKeyView(RegistryView view)
        {
        }

        private static RegistryRights GetRegistryKeyRights(bool isWritable)
        {
            return isWritable ?
                RegistryRights.ReadKey | RegistryRights.WriteKey :
                RegistryRights.ReadKey;
        }

        /// <summary>Retrieves the current state of the dirty property.</summary>
        /// <remarks>A key is marked as dirty if any operation has occurred that modifies the contents of the key.</remarks>
        /// <returns><b>true</b> if the key has been modified.</returns>
        private bool IsDirty() => (_state & StateFlags.Dirty) != 0;

        private bool IsSystemKey() => (_state & StateFlags.SystemKey) != 0;

        private bool IsWritable() => (_state & StateFlags.WriteAccess) != 0;

        private bool IsPerfDataKey() => (_state & StateFlags.PerfData) != 0;

        private void SetDirty() => _state |= StateFlags.Dirty;

        [Flags]
        private enum StateFlags
        {
            /// <summary>Dirty indicates that we have munged data that should be potentially written to disk.</summary>
            Dirty = 0x0001,
            /// <summary>SystemKey indicates that this is a "SYSTEMKEY" and shouldn't be "opened" or "closed".</summary>
            SystemKey = 0x0002,
            /// <summary>Access</summary>
            WriteAccess = 0x0004,
            /// <summary>Indicates if this key is for HKEY_PERFORMANCE_DATA</summary>
            PerfData = 0x0008
        }

        /**
         * Closes this key, flushes it to disk if the contents have been modified.
         */
        public void Close()
        {
            Dispose(true);
        }

        private void Dispose(bool disposing)
        {
            if (_hkey != null)
            {
                if (!IsSystemKey())
                {
                    try
                    {
                        _hkey.Dispose();
                    }
                    catch (IOException)
                    {
                        // we don't really care if the handle is invalid at this point
                    }
                    finally
                    {
                        _hkey = null;
                    }
                }
                else if (disposing && IsPerfDataKey())
                {
                    // System keys should never be closed.  However, we want to call RegCloseKey
                    // on HKEY_PERFORMANCE_DATA when called from PerformanceCounter.CloseSharedResources
                    // (i.e. when disposing is true) so that we release the PERFLIB cache and cause it
                    // to be refreshed (by re-reading the registry) when accessed subsequently. 
                    // This is the only way we can see the just installed perf counter.  
                    // NOTE: since HKEY_PERFORMANCE_DATA is process wide, there is inherent race condition in closing
                    // the key asynchronously. While Vista is smart enough to rebuild the PERFLIB resources
                    // in this situation the down level OSes are not. We have a small window between  
                    // the dispose below and usage elsewhere (other threads). This is By Design. 
                    // This is less of an issue when OS > NT5 (i.e Vista & higher), we can close the perfkey  
                    // (to release & refresh PERFLIB resources) and the OS will rebuild PERFLIB as necessary. 
                    Interop.Advapi32.RegCloseKey(RegistryKey.HKEY_PERFORMANCE_DATA);
                }
            }
        }

        internal static RegistryKey GetBaseKey(IntPtr hKey)
        {
            return GetBaseKey(hKey, RegistryView.Default);
        }

        internal static RegistryKey GetBaseKey(IntPtr hKey, RegistryView view)
        {
            int index = ((int)hKey) & 0x0FFFFFFF;

            bool isPerf = hKey == HKEY_PERFORMANCE_DATA;
            // only mark the SafeHandle as ownsHandle if the key is HKEY_PERFORMANCE_DATA.
            SafeRegistryHandle srh = new SafeRegistryHandle(hKey, isPerf);

            RegistryKey key = new RegistryKey(srh, true, true, false, isPerf, view);
            key._keyName = s_hkeyNames[index];
            return key;
        }

        // This dummy method is added to have the same implemenatation of Registry class. 
        // Its not being used anywhere. 
        public void SetValue(string name, object value, RegistryValueKind valueKind) { }
    }
}
