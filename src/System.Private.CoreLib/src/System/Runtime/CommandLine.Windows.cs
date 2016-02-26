// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System.Runtime.InteropServices;

namespace System.Runtime
{
    // CONTRACT with Runtime
    // The binder expects a RuntimeExport'ed method with name "CreateCommandLine" in the class library
    //      Signature : public string[] fnname ();

    internal static class CommandLine
    {
        [RuntimeExport("CreateCommandLine")]
        public unsafe static string[] InternalCreateCommandLine()
        {
            char * pCmdLine = Interop.mincore.GetCommandLine();

            int nArgs = SegmentCommandLine(pCmdLine, null);

            string[] argArray = new string[nArgs];
            SegmentCommandLine(pCmdLine, argArray);
            return argArray;
        }


        //----------------------------------------------------------------------------------------------------
        // Splits a command line into argc/argv lists, using the VC7 parsing rules.  Adapted from CLR's
        // SegmentCommandLine implementation.
        //
        // This functions interface mimics the CommandLineToArgvW api.
        //
        private unsafe static int SegmentCommandLine(char * pCmdLine, string[] argArray)
        {
            int nArgs = 0;
            char c;
            bool inquote;

            // First, parse the program name (argv[0]). Argv[0] is parsed under special rules. Anything up to 
            // the first whitespace outside a quoted subtring is accepted. Backslashes are treated as normal 
            // characters.

            char* psrc = pCmdLine;

            inquote = false;
            do
            {
                if (*psrc == '"')
                {
                    inquote = !inquote;
                    c = *psrc++;
                    continue;
                }

                c = *psrc++;
            } while ((c != '\0' && (inquote || (c != ' ' && c != '\t'))));

            if (c == '\0')
            {
                psrc--;
            }

            inquote = false;

            // loop on each argument
            for (;;)
            {
                if (*psrc != '\0')
                {
                    while (*psrc == ' ' || *psrc == '\t')
                    {
                        ++psrc;
                    }
                }

                if (*psrc == '\0')
                    break;              // end of args

                // scan an argument

                char* psrcOrig = psrc;
                bool inquoteOrig = inquote;

                int argLen = ScanArgument(ref psrc, ref inquote, null);

                if (argArray != null)
                {
                    char[] arg = new char[argLen];
                    ScanArgument(ref psrcOrig, ref inquoteOrig, arg);
                    argArray[nArgs] = new string(arg);
                }

                nArgs++;
            }

            return nArgs;
        }

        private unsafe static int ScanArgument(ref char* psrc, ref bool inquote, char[] arg)
        {
            int charIdx = 0;
            // loop through scanning one argument
            for (;;)
            {
                bool copychar = true;
                // Rules: 2N backslashes + " ==> N backslashes and begin/end quote
                //      2N+1 backslashes + " ==> N backslashes + literal "
                //         N backslashes     ==> N backslashes
                int numslash = 0;
                while (*psrc == '\\')
                {
                    // count number of backslashes for use below
                    ++psrc;
                    ++numslash;
                }
                if (*psrc == '"')
                {
                    // if 2N backslashes before, start/end quote, otherwise copy literally
                    if (numslash % 2 == 0)
                    {
                        if (inquote && psrc[1] == '"')
                        {
                            psrc++;    // Double quote inside quoted string
                        }
                        else
                        {
                            // skip first quote char and copy second
                            copychar = false;       // don't copy quote
                            inquote = !inquote;
                        }
                    }
                    numslash /= 2;          // divide numslash by two
                }

                // copy slashes
                while (numslash-- > 0)
                {
                    if (arg != null)
                    {
                        arg[charIdx] = '\\';
                    }
                    charIdx++;
                }

                // if at end of arg, break loop
                if (*psrc == '\0' || (!inquote && (*psrc == ' ' || *psrc == '\t')))
                    break;

                // copy character into argument
                if (copychar)
                {
                    if (arg != null)
                    {
                        arg[charIdx] = *psrc;
                    }
                    charIdx++;
                }
                ++psrc;
            }

            return charIdx;
        }
    }
}
