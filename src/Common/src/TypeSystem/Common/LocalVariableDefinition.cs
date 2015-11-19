// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace Internal.TypeSystem
{
    public struct LocalVariableDefinition
    {
        /// <summary>
        /// Gets a value indicating whether the stored target should be pinned in the runtime
        /// heap and shouldn't be moved by the actions of the garbage collector.
        /// </summary>
        public readonly bool IsPinned;

        /// <summary>
        /// Gets the type of the local variable.
        /// </summary>
        public readonly TypeDesc Type;

        public LocalVariableDefinition(TypeDesc type, bool isPinned)
        {
            IsPinned = isPinned;
            Type = type;
        }

        public override string ToString()
        {
            return IsPinned ? "pinned " + Type.ToString() : Type.ToString();
        }
    }
}
