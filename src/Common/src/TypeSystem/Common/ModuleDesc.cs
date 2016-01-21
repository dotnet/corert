// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace Internal.TypeSystem
{
    public abstract partial class ModuleDesc
    {
        /// <summary>
        /// Gets the type system context the module belongs to.
        /// </summary>
        public TypeSystemContext Context
        {
            get;
            private set;
        }

        public ModuleDesc(TypeSystemContext context)
        {
            Context = context;
        }

        /// <summary>
        /// Gets a type in this module with the specified name.
        /// </summary>
        public abstract MetadataType GetType(string nameSpace, string name, bool throwIfNotFound = true);

        /// <summary>
        /// Gets the global &lt;Module&gt; type.
        /// </summary>
        public abstract TypeDesc GetGlobalModuleType();
    }
}
