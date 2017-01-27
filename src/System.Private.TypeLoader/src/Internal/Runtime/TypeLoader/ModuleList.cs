// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.


using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Runtime;
using System.Text;
using System.Threading;
using Internal.Runtime.Augments;
using Internal.Metadata.NativeFormat;
using Internal.Reflection.Execution;

namespace Internal.Runtime.TypeLoader
{
    public enum ModuleType
    {
        Eager,
        ReadyToRun,
        Ecma
    }

    /// <summary>
    /// This class represents basic information about a native binary module including its
    /// metadata.
    /// </summary>
    public unsafe class ModuleInfo
    {
        /// <summary>
        /// Module handle is equal to its starting virtual address in memory (i.e. it points
        /// at the executable PE header).
        /// </summary>
        public IntPtr Handle { get; private set; }

        /// <summary>
        /// A reference to the dynamic module is part of the EEType for dynamically allocated types.
        /// </summary>
        internal DynamicModule* DynamicModulePtr { get; private set; }

        /// <summary>
        /// What sort of module is this? (Eager, ReadyToRun)?
        /// </summary>
        internal ModuleType ModuleType { get; private set; }

        /// <summary>
        /// Initialize module info and construct per-module metadata reader.
        /// </summary>
        /// <param name="moduleHandle">Handle (address) of module to initialize</param>
        internal ModuleInfo(IntPtr moduleHandle, ModuleType moduleType)
        {
            Handle = moduleHandle;
            ModuleType = moduleType;

            DynamicModule* dynamicModulePtr = (DynamicModule*)MemoryHelpers.AllocateMemory(sizeof(DynamicModule));
            dynamicModulePtr->CbSize = DynamicModule.DynamicModuleSize;
            Debug.Assert(sizeof(DynamicModule) >= dynamicModulePtr->CbSize);

            if ((moduleType == ModuleType.ReadyToRun) || (moduleType == ModuleType.Ecma))
            {
                // Dynamic type load modules utilize dynamic type resolution
                dynamicModulePtr->DynamicTypeSlotDispatchResolve = Intrinsics.AddrOf(
                    (Func<IntPtr, IntPtr, ushort, IntPtr>)ResolveTypeSlotDispatch);
            }
            else
            {
                Debug.Assert(moduleType == ModuleType.Eager);
                // Pre-generated modules do not
                dynamicModulePtr->DynamicTypeSlotDispatchResolve = IntPtr.Zero;
            }

            dynamicModulePtr->GetRuntimeException = Intrinsics.AddrOf(
                (Func<ExceptionIDs, Exception>)RuntimeExceptionHelpers.GetRuntimeException);

            DynamicModulePtr = dynamicModulePtr;
        }

        internal unsafe static IntPtr ResolveTypeSlotDispatch(IntPtr targetTypeAsIntPtr, IntPtr interfaceTypeAsIntPtr, ushort slot)
        {
            IntPtr methodAddress;
            if (!TypeLoaderEnvironment.Instance.TryResolveTypeSlotDispatch(targetTypeAsIntPtr, interfaceTypeAsIntPtr, slot, out methodAddress))
            {
                throw new BadImageFormatException();
            }
            return methodAddress;
        }        
    }

    public class NativeFormatModuleInfo : ModuleInfo
    {
        /// <summary>
        /// Initialize module info and construct per-module metadata reader.
        /// </summary>
        /// <param name="moduleHandle">Handle (address) of module to initialize</param>
        internal NativeFormatModuleInfo(IntPtr moduleHandle, ModuleType moduleType, IntPtr pBlob, int cbBlob) : base (moduleHandle, moduleType)
        {
            MetadataReader = new MetadataReader((IntPtr)pBlob, (int)cbBlob);
        }
        
        /// <summary>
        /// Module metadata reader for NativeFormat metadata
        /// </summary>
        public MetadataReader MetadataReader { get; private set; }
    }

    /// <summary>
    /// This class represents a linear module list and a dictionary mapping module handles
    /// to its indices. When a new module is registered, a new instance of this class gets
    /// constructed and atomically updates the _loadedModuleMap so that at any point in time
    /// all threads see the map as consistent.
    /// </summary>
    internal sealed class ModuleMap
    {
        /// <summary>
        /// Array of loaded binary modules.
        /// </summary>
        public readonly ModuleInfo[] Modules;

        /// <summary>
        /// Map of module handles to indices within the Modules array.
        /// </summary>
        public readonly LowLevelDictionary<IntPtr, int> HandleToModuleIndex;

        internal ModuleMap(ModuleInfo[] modules)
        {
            Modules = modules;
            HandleToModuleIndex = new LowLevelDictionary<IntPtr, int>();
            for (int moduleIndex = 0; moduleIndex < Modules.Length; moduleIndex++)
            {
                // Ecma modules don't go in the reverse lookup hash because they share a module index with the system module
                if (Modules[moduleIndex].ModuleType != ModuleType.Ecma)
                    HandleToModuleIndex.Add(Modules[moduleIndex].Handle, moduleIndex);
            }
        }
    }

    /// <summary>
    /// Helper class that can construct an enumerator for the module info map, possibly adjusting
    /// the module order so that a given explicitly specified module goes first - this is used
    /// as optimization in cases where a certain module is most likely to contain some metadata.
    /// </summary>
    public struct ModuleInfoEnumerable
    {
        /// <summary>
        /// Module map to enumerate
        /// </summary>
        private readonly ModuleMap _moduleMap;

        /// <summary>
        /// Module handle that should be enumerated first, default(IntPtr) when not used.
        /// </summary>
        private readonly IntPtr _preferredModuleHandle;

        /// <summary>
        /// Store module map and preferred module to pass to the enumerator upon construction.
        /// </summary>
        /// <param name="moduleMap">Module map to enumerate</param>
        /// <param name="preferredModuleHandle">Optional module handle to enumerate first</param>
        internal ModuleInfoEnumerable(ModuleMap moduleMap, IntPtr preferredModuleHandle)
        {
            _moduleMap = moduleMap;
            _preferredModuleHandle = preferredModuleHandle;
        }

        /// <summary>
        /// Construct the actual module info enumerator.
        /// </summary>
        public ModuleInfoEnumerator GetEnumerator()
        {
            return new ModuleInfoEnumerator(_moduleMap, _preferredModuleHandle);
        }
    }

    /// <summary>
    /// This enumerator iterates the module map, possibly adjusting the order to make a given
    /// module go first in the enumeration.
    /// </summary>
    public struct ModuleInfoEnumerator
    {
        /// <summary>
        /// Array of modules to enumerate.
        /// </summary>
        private readonly ModuleInfo[] _modules;

        /// <summary>
        /// Preferred module index in the array, -1 when none (in such case the array is enumerated
        /// in its natural order).
        /// </summary>
        private int _preferredIndex;

        /// <summary>
        /// Enumeration step index initially set to -1 (so that the first MoveNext increments it to 0).
        /// </summary>
        private int _iterationIndex;

        /// <summary>
        /// Current _modules element that should be returned by Current (updated in MoveNext).
        /// </summary>
        private ModuleInfo _currentModule;

        /// <summary>
        /// Initialize the module enumerator state machine and locate the preferred module index.
        /// </summary>
        /// <param name="moduleMap">Module map to enumerate</param>
        /// <param name="preferredModuleHandle">Optional module handle to enumerate first</param>
        internal ModuleInfoEnumerator(ModuleMap moduleMap, IntPtr preferredModuleHandle)
        {
            _modules = moduleMap.Modules;
            _preferredIndex = -1;
            _iterationIndex = -1;
            _currentModule = null;

            if (preferredModuleHandle != default(IntPtr) &&
                !moduleMap.HandleToModuleIndex.TryGetValue(preferredModuleHandle, out _preferredIndex))
            {
                Environment.FailFast("Invalid module requested in enumeration: " + preferredModuleHandle.LowLevelToString());
            }
        }

        /// <summary>
        /// Move the enumerator state machine to the next element in the module map.
        /// </summary>
        /// <returns>true when [another] module is available, false when the enumeration is finished</returns>
        public bool MoveNext()
        {
            if (_iterationIndex + 1 >= _modules.Length)
            {
                _currentModule = null;
                return false;
            }

            _iterationIndex++;
            int moduleIndex = _iterationIndex;
            if (moduleIndex <= _preferredIndex)
            {
                // Transform the index so that the _preferredIndex is returned in first iteration
                moduleIndex = (moduleIndex == 0 ? _preferredIndex : moduleIndex - 1);
            }

            _currentModule = _modules[moduleIndex];

            return true;
        }

        /// <summary>
        /// Look up the "current" module corresponding to the previous call to MoveNext.
        /// </summary>
        public ModuleInfo Current
        {
            get
            {
                if (_currentModule == null)
                {
                    Environment.FailFast("Current module queried in wrong enumerator state");
                }
                return _currentModule;
            }
        }
    }

    /// <summary>
    /// Helper class that can construct an enumerator for the module handle map, possibly adjusting
    /// the module order so that a given explicitly specified module goes first - this is used
    /// as optimization in cases where a certain module is most likely to contain some metadata.
    /// </summary>
    public struct ModuleHandleEnumerable
    {
        /// <summary>
        /// Module map to enumerate
        /// </summary>
        private readonly ModuleMap _moduleMap;

        /// <summary>
        /// Module handle that should be enumerated first, default(IntPtr) when not used.
        /// </summary>
        private readonly IntPtr _preferredModuleHandle;

        /// <summary>
        /// Store module map and preferred module to pass to the enumerator upon construction.
        /// </summary>
        /// <param name="moduleMap">Module map to enumerate</param>
        /// <param name="preferredModuleHandle">Optional module handle to enumerate first</param>
        internal ModuleHandleEnumerable(ModuleMap moduleMap, IntPtr preferredModuleHandle)
        {
            _moduleMap = moduleMap;
            _preferredModuleHandle = preferredModuleHandle;
        }

        /// <summary>
        /// Create the actual module handle enumerator.
        /// </summary>
        public ModuleHandleEnumerator GetEnumerator()
        {
            return new ModuleHandleEnumerator(_moduleMap, _preferredModuleHandle);
        }
    }

    /// <summary>
    /// Enumerator for module handles, optionally overriding module order with a given preferred
    /// module to be enumerated first.
    /// </summary>
    public struct ModuleHandleEnumerator
    {
        /// <summary>
        /// The underlying ModuleInfoEnumerator handles enumeration internals
        /// </summary>
        private ModuleInfoEnumerator _moduleInfoEnumerator;

        /// <summary>
        /// Construct the underlying module info enumerator used to iterate the module map
        /// </summary>
        /// <param name="moduleMap">Module map to enumerate</param>
        /// <param name="preferredModuleHandle">Optional module handle to enumerate first</param>
        internal ModuleHandleEnumerator(ModuleMap moduleMap, IntPtr preferredModuleHandle)
        {
            _moduleInfoEnumerator = new ModuleInfoEnumerator(moduleMap, preferredModuleHandle);
        }

        /// <summary>
        /// Move to next element in the module map. Return true when an element is available,
        /// false when the enumeration is finished.
        /// </summary>
        public bool MoveNext()
        {
            bool result;
            do
            {
                result = _moduleInfoEnumerator.MoveNext();
                // Ecma module shouldn't be reported as they should not be enumerated by ModuleHandle (as its always the System module)
                if (!result || (_moduleInfoEnumerator.Current.ModuleType != ModuleType.Ecma))
                {
                    break;
                }
            } while(true);

            return result;
        }

        /// <summary>
        /// Return current module handle.
        /// </summary>
        public IntPtr Current
        {
            get { return _moduleInfoEnumerator.Current.Handle; }
        }
    }

    /// <summary>
    /// Helper class that can construct an enumerator for module metadata readers, possibly adjusting
    /// the module order so that a given explicitly specified module goes first - this is used
    /// as optimization in cases where a certain module is most likely to contain some metadata.
    /// </summary>
    public struct MetadataReaderEnumerable
    {
        /// <summary>
        /// Module map to enumerate
        /// </summary>
        private readonly ModuleMap _moduleMap;

        /// <summary>
        /// Module handle that should be enumerated first, default(IntPtr) when not used.
        /// </summary>
        private readonly IntPtr _preferredModuleHandle;

        /// <summary>
        /// Store module map and preferred module to pass to the enumerator upon construction.
        /// </summary>
        /// <param name="moduleMap">Module map to enumerate</param>
        /// <param name="preferredModuleHandle">Optional module handle to enumerate first</param>
        internal MetadataReaderEnumerable(ModuleMap moduleMap, IntPtr preferredModuleHandle)
        {
            _moduleMap = moduleMap;
            _preferredModuleHandle = preferredModuleHandle;
        }

        /// <summary>
        /// Create the actual module handle enumerator.
        /// </summary>
        public MetadataReaderEnumerator GetEnumerator()
        {
            return new MetadataReaderEnumerator(_moduleMap, _preferredModuleHandle);
        }
    }

    /// <summary>
    /// Enumerator for metadata readers, optionally overriding module order with a given preferred
    /// module to be enumerated first.
    /// </summary>
    public struct MetadataReaderEnumerator
    {
        /// <summary>
        /// The underlying ModuleInfoEnumerator handles enumeration internals
        /// </summary>
        private ModuleInfoEnumerator _moduleInfoEnumerator;

        /// <summary>
        /// Construct the underlying module info enumerator used to iterate the module map
        /// </summary>
        /// <param name="moduleMap">Module map to enumerate</param>
        /// <param name="preferredModuleHandle">Optional module handle to enumerate first</param>
        internal MetadataReaderEnumerator(ModuleMap moduleMap, IntPtr preferredModuleHandle)
        {
            _moduleInfoEnumerator = new ModuleInfoEnumerator(moduleMap, preferredModuleHandle);
        }

        /// <summary>
        /// Move to next element in the module map. Return true when an element is available,
        /// false when the enumeration is finished.
        /// </summary>
        public bool MoveNext()
        {
            while (_moduleInfoEnumerator.MoveNext())
            {
                NativeFormatModuleInfo moduleInfo = _moduleInfoEnumerator.Current as NativeFormatModuleInfo;
                if (moduleInfo != null && moduleInfo.MetadataReader != null)
                {
                    return true;
                }
            }
            return false;
        }

        /// <summary>
        /// Return current metadata reader.
        /// </summary>
        public MetadataReader Current
        {
            get { return ((NativeFormatModuleInfo)_moduleInfoEnumerator.Current).MetadataReader; }
        }
    }

    /// <summary>
    /// Utilities for manipulating module list and metadata readers.
    /// </summary>
    public sealed class ModuleList
    {
        /// <summary>
        /// Map of module addresses to module info. Every time a new module is loaded,
        /// the reference gets atomically updated to a newly copied instance of the dictionary
        /// to that consumers of this dictionary can look at the reference and  enumerate / process it without locking, fear that the contents of the dictionary change
        /// under its hands.
        /// </summary>
        private volatile ModuleMap _loadedModuleMap;

        internal ModuleMap GetLoadedModuleMapInternal() { return _loadedModuleMap; }

        /// <summary>
        /// List of callbacks to execute when a module gets registered.
        /// </summary>
        private Action<ModuleInfo> _moduleRegistrationCallbacks;

        /// <summary>
        /// Lock used for serializing module registrations.
        /// </summary>
        private Lock _moduleRegistrationLock;

        /// <summary>
        /// Base Module (module that contains System.Object)
        /// </summary>
        private ModuleInfo _systemModule;

        /// <summary>
        /// Register initially (eagerly) loaded modules.
        /// </summary>
        internal ModuleList()
        {
            _loadedModuleMap = new ModuleMap(new ModuleInfo[0]);
            _moduleRegistrationCallbacks = default(Action<ModuleInfo>);
            _moduleRegistrationLock = new Lock();

            RegisterNewModules(ModuleType.Eager);

            IntPtr systemObjectModule = RuntimeAugments.GetModuleFromTypeHandle(RuntimeAugments.RuntimeTypeHandleOf<object>());
            foreach (ModuleInfo m in _loadedModuleMap.Modules)
            {
                if (m.Handle == systemObjectModule)
                {
                    _systemModule = m;
                    break;
                }
            }
        }

        /// <summary>
        /// Module list is a process-wide singleton that physically lives in the TypeLoaderEnvironment instance.
        /// </summary>
        public static ModuleList Instance
        {
            get { return TypeLoaderEnvironment.Instance.ModuleList; }
        }

        /// <summary>
        /// Register a new callback that gets called whenever a new module gets registered.
        /// The module registration happens under a global lock so that the module registration
        /// callbacks are never called concurrently.
        /// </summary>
        /// <param name="moduleRegistrationCallback">Method to call whenever a new module is registered</param>
        public static void AddModuleRegistrationCallback(Action<ModuleInfo> newModuleRegistrationCallback)
        {
            // Accumulate callbacks to be notified upon module registration
            Instance._moduleRegistrationCallbacks += newModuleRegistrationCallback;

            // Invoke the new callback for all modules that have already been registered
            foreach (ModuleInfo moduleInfo in EnumerateModules())
            {
                newModuleRegistrationCallback(moduleInfo);
            }
        }

        /// <summary>
        /// Register all modules which were added (Registered) to the runtime and are not already registered with the TypeLoader.
        /// </summary>
        /// <param name="moduleType">Type to assign to all new modules.</param>
        public void RegisterNewModules(ModuleType moduleType)
        {
            // prevent multiple threads from registering modules concurrently
            using (LockHolder.Hold(_moduleRegistrationLock))
            {
                // Fetch modules that have already been registered with the runtime
                int loadedModuleCount = RuntimeAugments.GetLoadedModules(null);
                IntPtr[] loadedModuleHandles = new IntPtr[loadedModuleCount];
                int loadedModuleCountUpdated = RuntimeAugments.GetLoadedModules(loadedModuleHandles);
                Debug.Assert(loadedModuleCount == loadedModuleCountUpdated);

                LowLevelList<IntPtr> newModuleHandles = new LowLevelList<IntPtr>(loadedModuleHandles.Length);
                foreach (IntPtr moduleHandle in loadedModuleHandles)
                {
                    // Skip already registered modules.
                    int oldModuleIndex;
                    if (_loadedModuleMap.HandleToModuleIndex.TryGetValue(moduleHandle, out oldModuleIndex))
                    {
                        continue;
                    }

                    newModuleHandles.Add(moduleHandle);
                }

                // Copy existing modules to new dictionary
                int oldModuleCount = _loadedModuleMap.Modules.Length;
                ModuleInfo[] updatedModules = new ModuleInfo[oldModuleCount + newModuleHandles.Count];
                if (oldModuleCount > 0)
                {
                    Array.Copy(_loadedModuleMap.Modules, 0, updatedModules, 0, oldModuleCount);
                }

                for (int newModuleIndex = 0; newModuleIndex < newModuleHandles.Count; newModuleIndex++)
                {
                    ModuleInfo newModuleInfo;

                    unsafe
                    {
                        byte* pBlob;
                        uint cbBlob;

                        if (RuntimeAugments.FindBlob(newModuleHandles[newModuleIndex], (int)ReflectionMapBlob.EmbeddedMetadata, new IntPtr(&pBlob), new IntPtr(&cbBlob)))
                        {
                            newModuleInfo = new NativeFormatModuleInfo(newModuleHandles[newModuleIndex], moduleType, (IntPtr)pBlob, (int)cbBlob);
                        }
                        else
                        {
                            newModuleInfo = new ModuleInfo(newModuleHandles[newModuleIndex], moduleType);
                        }
                    }

                    updatedModules[oldModuleCount + newModuleIndex] = newModuleInfo;

                    if (_moduleRegistrationCallbacks != null)
                    {
                        _moduleRegistrationCallbacks(newModuleInfo);
                    }
                }

                // Atomically update the module map
                _loadedModuleMap = new ModuleMap(updatedModules);
            }
        }

        public void RegisterModule(ModuleInfo newModuleInfo)
        {
            // prevent multiple threads from registering modules concurrently
            using (LockHolder.Hold(_moduleRegistrationLock))
            {
                // Copy existing modules to new dictionary
                int oldModuleCount = _loadedModuleMap.Modules.Length;
                ModuleInfo[] updatedModules = new ModuleInfo[oldModuleCount + 1];
                if (oldModuleCount > 0)
                {
                    Array.Copy(_loadedModuleMap.Modules, 0, updatedModules, 0, oldModuleCount);
                }
                updatedModules[oldModuleCount] = newModuleInfo;
                if (_moduleRegistrationCallbacks != null)
                {
                    _moduleRegistrationCallbacks(newModuleInfo);
                }

                // Atomically update the module map
                _loadedModuleMap = new ModuleMap(updatedModules);
            }
        }

        /// <summary>
        /// Locate module info for a given module. Fail if not found or before the module registry
        /// gets initialized.
        /// </summary>
        /// <param name="moduleHandle">Handle of module to look up</param>
        public ModuleInfo GetModuleInfoByHandle(IntPtr moduleHandle)
        {
            ModuleMap moduleMap = _loadedModuleMap;
            return moduleMap.Modules[moduleMap.HandleToModuleIndex[moduleHandle]];
        }

        /// <summary>
        /// Try to Locate module info for a given module. Returns false when not found.
        /// gets initialized.
        /// </summary>
        /// <param name="moduleHandle">Handle of module to look up</param>
        public bool TryGetModuleInfoByHandle(IntPtr moduleHandle, out ModuleInfo moduleInfo)
        {
            ModuleMap moduleMap = _loadedModuleMap;
            int moduleIndex;
            if (moduleMap.HandleToModuleIndex.TryGetValue(moduleHandle, out moduleIndex))
            {
                moduleInfo = moduleMap.Modules[moduleIndex];
                return true;
            }
            moduleInfo = null;
            return false;
        }

        /// <summary>
        /// Given module handle, locate the metadata reader. Return null when not found.
        /// </summary>
        /// <param name="moduleHandle">Handle of module to look up</param>
        /// <returns>Reader for the embedded metadata blob in the module, null when not found</returns>
        public MetadataReader GetMetadataReaderForModule(IntPtr moduleHandle)
        {
            ModuleMap moduleMap = _loadedModuleMap;
            int moduleIndex;
            if (moduleMap.HandleToModuleIndex.TryGetValue(moduleHandle, out moduleIndex))
            {
                NativeFormatModuleInfo moduleInfo = moduleMap.Modules[moduleIndex] as NativeFormatModuleInfo;
                return moduleInfo.MetadataReader;
            }
            return null;
        }

        /// <summary>
        /// Given dynamic module handle, locate the moduleinfo
        /// </summary>
        /// <param name="moduleHandle">Handle of module to look up</param>
        /// <returns>fails if not found</returns>
        public unsafe ModuleInfo GetModuleInfoForDynamicModule(IntPtr dynamicModuleHandle)
        {
            foreach (ModuleInfo moduleInfo in _loadedModuleMap.Modules)
            {
                if (new IntPtr(moduleInfo.DynamicModulePtr) == dynamicModuleHandle)
                    return moduleInfo;
            }

            // We should never have a dynamic module that is not associated with a module (where does it come from?!)
            Debug.Assert(false);
            return null;
        }        


        /// <summary>
        /// Locate the containing module for a given metadata reader. Assert when not found.
        /// </summary>
        /// <param name="reader">Metadata reader to look up</param>
        /// <returns>Module handle of the module containing the given reader</returns>
        public ModuleInfo GetModuleInfoForMetadataReader(MetadataReader reader)
        {
            foreach (ModuleInfo moduleInfo in _loadedModuleMap.Modules)
            {
                NativeFormatModuleInfo nativeFormatModuleInfo = moduleInfo as NativeFormatModuleInfo;
                if (nativeFormatModuleInfo != null && nativeFormatModuleInfo.MetadataReader == reader)
                {
                    return moduleInfo;
                }
            }

            // We should never have a reader that is not associated with a module (where does it come from?!)
            Debug.Assert(false);
            return null;
        }

        /// <summary>
        /// Locate the containing module for a given metadata reader. Assert when not found.
        /// </summary>
        /// <param name="reader">Metadata reader to look up</param>
        /// <returns>Module handle of the module containing the given reader</returns>
        public IntPtr GetModuleForMetadataReader(MetadataReader reader)
        {
            foreach (ModuleInfo moduleInfo in _loadedModuleMap.Modules)
            {
                NativeFormatModuleInfo nativeFormatModuleInfo = moduleInfo as NativeFormatModuleInfo;
                if (nativeFormatModuleInfo != null && nativeFormatModuleInfo.MetadataReader == reader)
                {
                    return moduleInfo.Handle;
                }
            }

            // We should never have a reader that is not associated with a module (where does it come from?!)
            Debug.Assert(false);
            return IntPtr.Zero;
        }

        /// <summary>
        /// Base Module (module that contains System.Object)
        /// </summary>
        public ModuleInfo SystemModule
        {
            get
            {
                return _systemModule;
            }
        }

        /// <summary>
        /// Enumerate modules. Optionally specify a module that should be enumerated first
        /// - this is used as an optimization in cases when a certain binary module is more probable
        /// to contain a certain information.
        /// </summary>
        /// <param name="preferredModule">Optional handle to the module which should be enumerated first</param>
        public static ModuleInfoEnumerable EnumerateModules(IntPtr preferredModule = default(IntPtr))
        {
            return new ModuleInfoEnumerable(Instance._loadedModuleMap, preferredModule);
        }

        /// <summary>
        /// Enumerate metadata readers. Optionally specify a module that should be enumerated first
        /// - this is used as an optimization in cases when a certain binary module is more probable
        /// to contain a certain information.
        /// </summary>
        /// <param name="preferredModule">Optional handle to the module which should be enumerated first</param>
        public static MetadataReaderEnumerable EnumerateMetadataReaders(IntPtr preferredModule = default(IntPtr))
        {
            return new MetadataReaderEnumerable(Instance._loadedModuleMap, preferredModule);
        }

        /// <summary>
        /// Enumerate module handles (simplified version for code that only needs the module addresses).
        /// </summary>
        public static ModuleHandleEnumerable Enumerate(IntPtr preferredModule = default(IntPtr))
        {
            return new ModuleHandleEnumerable(Instance._loadedModuleMap, preferredModule);
        }
    }

    public static partial class RuntimeSignatureHelper
    {
        public static ModuleInfo GetModuleInfo(this Internal.Runtime.CompilerServices.RuntimeSignature methodSignature)
        {
            if (methodSignature.IsNativeLayoutSignature)
            {
                return ModuleList.Instance.GetModuleInfoByHandle(methodSignature.ModuleHandle);
            }
            else
            {
                ModuleInfo moduleInfo;
                if (!ModuleList.Instance.TryGetModuleInfoByHandle(methodSignature.ModuleHandle, out moduleInfo))
                {
                    moduleInfo = ModuleList.Instance.GetModuleInfoForDynamicModule(methodSignature.ModuleHandle);
                }
                return moduleInfo;
            }
        }
    }
}
