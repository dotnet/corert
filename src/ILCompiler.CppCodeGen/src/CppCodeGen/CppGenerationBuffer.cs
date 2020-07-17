// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.Text;

namespace ILCompiler.Compiler.CppCodeGen
{
    /// <summary>
    /// Similar to a StringBuilder but handles proper formatting of C/C++ code by supporting indentation.
    /// 
    /// Principle to remember: new lines have to be requested when the output needs to be on a new line.
    /// When a new line is printed via <see cref="AppendLine"/> indentation is performed.
    /// 
    /// Use <see cref="Indent"/> and <see cref="Exdent"/> to increase/decrease the level of indentation.
    /// </summary>
    public class CppGenerationBuffer
    {
        /// <summary>
        /// Initialize new instance
        /// </summary>
        public CppGenerationBuffer()
        {
            _builder = new StringBuilder();
        }

        /// <summary>
        /// Level of indentation used so far.
        /// </summary>
        private int _indent;

        /// <summary>
        /// Builder where all additions are done.
        /// </summary>
        private StringBuilder _builder;

        /// <summary>
        /// Increase level of indentation by one.
        /// </summary>
        public void Indent()
        {
            _indent++;
        }

        /// <summary>
        /// Decrease level of indentation by one.
        /// </summary>
        public void Exdent()
        {
            _indent--;
        }

        /// <summary>
        /// Append string <param name="s"/> to content.
        /// </summary>
        /// <param name="s">String value to print.</param>
        public void Append(string s)
        {
            _builder.Append(s);
        }

        /// <summary>
        /// Append integer <param name="i"/> in decimal format to content.
        /// </summary>
        /// <param name="i">Integer value to print.</param>
        public void Append(int i)
        {
            _builder.Append(i);
        }

        /// <summary>
        /// Append character <param name="c"/> to content.
        /// </summary>
        /// <param name="c">Character value to print.</param>
        public void Append(char c)
        {
            _builder.Append(c);
        }

        /// <summary>
        /// Append an empty new line without emitting any indentation.
        /// Useful to just skip a line.
        /// </summary>
        public void AppendEmptyLine()
        {
            _builder.AppendLine();
        }

        /// <summary>
        /// Append an empty new line and the required number of tabs.
        /// </summary>
        public void AppendLine()
        {
            _builder.AppendLine();
            for (int i = 0; i < _indent; i++)
                _builder.Append('\t');
        }

        /// <summary>
        /// Clear current content.
        /// </summary>
        public void Clear()
        {
            _builder.Clear();
        }

        /// <summary>
        /// Export current content as a string.
        /// </summary>
        /// <returns>String representation of current content.</returns>
        public override string ToString()
        {
            return _builder.ToString();
        }
    }
}
