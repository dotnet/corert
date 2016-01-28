// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Collections.Generic;
using System.IO;

namespace Internal.CommandLine
{
    //
    // Simple command line parser
    //
    internal class CommandLineParser
    {
        private string[] _args;
        private int _current;

        private string _currentOption;

        public CommandLineParser(string[] args)
        {
            _args = args;
            _current = 0;
        }

        public string GetOption()
        {
            if (_current >= _args.Length)
                return null;

            string opt = _args[_current];
            _currentOption = opt;

            if (opt.StartsWith("-"))
            {
                opt = opt.Substring(1);
            }
            else
            if (Path.DirectorySeparatorChar != '/' && opt.StartsWith("/"))
            {
                // For convenience, allow command line options starting with slash on Windows
                opt = opt.Substring(1);
            }
            else
            {
                return "";
            }

            _current++;
            return opt;
        }

        public string GetCurrentOption()
        {
            return _currentOption;
        }

        public string GetStringValue()
        {
            if (_current >= _args.Length)
                throw new CommandLineException("Value expected for " + GetCurrentOption());

            return _args[_current++];
        }
        public void AppendExpandedPaths(Dictionary<string, string> dictionary, bool strict)
        {
            string pattern = GetStringValue();

            bool empty = true;

            string directoryName = Path.GetDirectoryName(pattern);
            string searchPattern = Path.GetFileName(pattern);

            if (directoryName == "")
                directoryName = ".";

            if (Directory.Exists(directoryName))
            {
                foreach (string fileName in Directory.EnumerateFiles(directoryName, searchPattern))
                {
                    string fullFileName = Path.GetFullPath(fileName);

                    string simpleName = Path.GetFileNameWithoutExtension(fileName);

                    if (dictionary.ContainsKey(simpleName))
                    {
                        if (strict)
                        {
                            throw new CommandLineException("Multiple input files matching same simple name " +
                                fullFileName + " " + dictionary[simpleName]);
                        }
                    }
                    else
                    {
                        dictionary.Add(simpleName, fullFileName);
                    }

                    empty = false;
                }
            }

            if (empty)
            {
                if (strict)
                {
                    throw new CommandLineException("No files matching " + pattern);
                }
                else
                {
                    Console.WriteLine("Warning: No files matching " + pattern);
                }
            }
        }
    }
}
