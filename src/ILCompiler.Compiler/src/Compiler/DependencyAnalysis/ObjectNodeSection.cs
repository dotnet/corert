// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;

namespace ILCompiler.DependencyAnalysis
{
    public enum SectionType
    {
        ReadOnly,
        Writeable,
        Executable
    }

    /// <summary>
    /// Specifies the object file section a node will be placed in; ie "text" or "data"
    /// </summary>
    public class ObjectNodeSection
    {
        public string Name { get; private set; }
        public SectionType Type { get; private set; }
        public string ComdatName { get; private set; }

        private ObjectNodeSection(string name, SectionType type, string comdatName)
        {
            Name = name;
            Type = type;
            ComdatName = comdatName;
        }

        public ObjectNodeSection(string name, SectionType type) : this(name, type, null)
        { }

        /// <summary>
        /// Returns true if the section is a standard one (defined as text, data, or rdata currently)
        /// </summary>
        public bool IsStandardSection
        {
            get
            {
                return this == DataSection || this == ReadOnlyDataSection || this == TextSection || this == XDataSection;
            }
        }

        public ObjectNodeSection GetSharedSection(string key)
        {
            string standardSectionPrefix = "";
            if (IsStandardSection)
                standardSectionPrefix = ".";

            return new ObjectNodeSection(standardSectionPrefix + Name + "$" + key, Type, key);
        }

        public static readonly ObjectNodeSection XDataSection = new ObjectNodeSection("xdata", SectionType.ReadOnly);
        public static readonly ObjectNodeSection DataSection = new ObjectNodeSection("data", SectionType.Writeable);
        public static readonly ObjectNodeSection ReadOnlyDataSection = new ObjectNodeSection("rdata", SectionType.ReadOnly);
        public static readonly ObjectNodeSection TextSection = new ObjectNodeSection("text", SectionType.Executable);
    }
}
