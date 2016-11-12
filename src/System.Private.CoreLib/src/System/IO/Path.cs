// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

/*============================================================
**
** 
** 
**
**
** Purpose: A collection of path manipulation methods.
**
**
===========================================================*/

using System;
using Win32Native = Microsoft.Win32.Win32Native;
using System.Text;
using System.Runtime.InteropServices;
using System.Security;
#if FEATURE_LEGACYSURFACE
using System.Security.Cryptography;
#endif
using System.Runtime.CompilerServices;
using System.Globalization;
using System.Runtime.Versioning;
using System.Diagnostics.Contracts;

namespace System.IO {
    // Provides methods for processing directory strings in an ideally
    // cross-platform manner.  Most of the methods don't do a complete
    // full parsing (such as examining a UNC hostname), but they will
    // handle most string operations.  
    // 
    // File names cannot contain backslash (\), slash (/), colon (:),
    // asterick (*), question mark (?), quote ("), less than (<;), 
    // greater than (>;), or pipe (|).  The first three are used as directory
    // separators on various platforms.  Asterick and question mark are treated
    // as wild cards.  Less than, Greater than, and pipe all redirect input
    // or output from a program to a file or some combination thereof.  Quotes
    // are special.
    // 
    // We are guaranteeing that Path.SeparatorChar is the correct 
    // directory separator on all platforms, and we will support 
    // Path.AltSeparatorChar as well.  To write cross platform
    // code with minimal pain, you can use slash (/) as a directory separator in
    // your strings.
     // Class contains only static data, no need to serialize
    [ComVisible(true)]
    internal static class Path
    {
        // Platform specific directory separator character.  This is backslash
        // ('\') on Windows, slash ('/') on Unix, and colon (':') on Mac.
        // 
#if !PLATFORM_UNIX        
        public static readonly char DirectorySeparatorChar = '\\';
        internal const string DirectorySeparatorCharAsString = "\\";
#else
        public static readonly char DirectorySeparatorChar = '/';
        internal const string DirectorySeparatorCharAsString = "/";
#endif // !PLATFORM_UNIX
        
        // Platform specific alternate directory separator character.  
        // This is backslash ('\') on Unix, and slash ('/') on Windows 
        // and MacOS.
        // 
#if !PLATFORM_UNIX        
        public static readonly char AltDirectorySeparatorChar = '/';
#else
        public static readonly char AltDirectorySeparatorChar = '\\';
#endif // !PLATFORM_UNIX
    
        // Platform specific volume separator character.  This is colon (':')
        // on Windows and MacOS, and slash ('/') on Unix.  This is mostly
        // useful for parsing paths like "c:\windows" or "MacVolume:System Folder".  
        // 
#if !PLATFORM_UNIX
        public static readonly char VolumeSeparatorChar = ':';
#else
        public static readonly char VolumeSeparatorChar = '/';
#endif // !PLATFORM_UNIX        
        
        // Platform specific invalid list of characters in a path.
        // See the "Naming a File" MSDN conceptual docs for more details on
        // what is valid in a file name (which is slightly different from what
        // is legal in a path name).
        // Note: This list is duplicated in CheckInvalidPathChars
        [Obsolete("Please use GetInvalidPathChars or GetInvalidFileNameChars instead.")]
        public static readonly char[] InvalidPathChars = { '\"', '<', '>', '|', '\0', (Char)1, (Char)2, (Char)3, (Char)4, (Char)5, (Char)6, (Char)7, (Char)8, (Char)9, (Char)10, (Char)11, (Char)12, (Char)13, (Char)14, (Char)15, (Char)16, (Char)17, (Char)18, (Char)19, (Char)20, (Char)21, (Char)22, (Char)23, (Char)24, (Char)25, (Char)26, (Char)27, (Char)28, (Char)29, (Char)30, (Char)31 };

        // Trim trailing white spaces, tabs etc but don't be aggressive in removing everything that has UnicodeCategory of trailing space.
        // String.WhitespaceChars will trim aggressively than what the underlying FS does (for ex, NTFS, FAT).    
        internal static readonly char[] TrimEndChars = { (char) 0x9, (char) 0xA, (char) 0xB, (char) 0xC, (char) 0xD, (char) 0x20,   (char) 0x85, (char) 0xA0};
        
#if !PLATFORM_UNIX
        private static readonly char[] RealInvalidPathChars = { '\"', '<', '>', '|', '\0', (Char)1, (Char)2, (Char)3, (Char)4, (Char)5, (Char)6, (Char)7, (Char)8, (Char)9, (Char)10, (Char)11, (Char)12, (Char)13, (Char)14, (Char)15, (Char)16, (Char)17, (Char)18, (Char)19, (Char)20, (Char)21, (Char)22, (Char)23, (Char)24, (Char)25, (Char)26, (Char)27, (Char)28, (Char)29, (Char)30, (Char)31 };

        // This is used by HasIllegalCharacters
        private static readonly char[] InvalidPathCharsWithAdditionalChecks = { '\"', '<', '>', '|', '\0', (Char)1, (Char)2, (Char)3, (Char)4, (Char)5, (Char)6, (Char)7, (Char)8, (Char)9, (Char)10, (Char)11, (Char)12, (Char)13, (Char)14, (Char)15, (Char)16, (Char)17, (Char)18, (Char)19, (Char)20, (Char)21, (Char)22, (Char)23, (Char)24, (Char)25, (Char)26, (Char)27, (Char)28, (Char)29, (Char)30, (Char)31, '*', '?' };

        private static readonly char[] InvalidFileNameChars = { '\"', '<', '>', '|', '\0', (Char)1, (Char)2, (Char)3, (Char)4, (Char)5, (Char)6, (Char)7, (Char)8, (Char)9, (Char)10, (Char)11, (Char)12, (Char)13, (Char)14, (Char)15, (Char)16, (Char)17, (Char)18, (Char)19, (Char)20, (Char)21, (Char)22, (Char)23, (Char)24, (Char)25, (Char)26, (Char)27, (Char)28, (Char)29, (Char)30, (Char)31, ':', '*', '?', '\\', '/' };
#else
        private static readonly char[] RealInvalidPathChars = { '\0' };

        // This is used by HasIllegalCharacters
        private static readonly char[] InvalidPathCharsWithAdditionalChecks = { '\0', '*', '?' };

        private static readonly char[] InvalidFileNameChars = { '\0', '*', '?', '\\', '/' };
#endif // !PLATFORM_UNIX

#if !PLATFORM_UNIX
        public static readonly char PathSeparator = ';';
#else
        public static readonly char PathSeparator = ':';
#endif // !PLATFORM_UNIX


        // Make this public sometime.
        // The max total path is 260, and the max individual component length is 255. 
        // For example, D:\<256 char file name> isn't legal, even though it's under 260 chars.
        internal static readonly int MaxPath = 260;

        // Windows API definitions
        internal const int MAX_PATH = 260;  // From WinDef.h
        internal const int MAX_DIRECTORY_PATH = 248;   // cannot create directories greater than 248 characters
    
        // Changes the extension of a file path. The path parameter
        // specifies a file path, and the extension parameter
        // specifies a file extension (with a leading period, such as
        // ".exe" or ".cs").
        //
        // The function returns a file path with the same root, directory, and base
        // name parts as path, but with the file extension changed to
        // the specified extension. If path is null, the function
        // returns null. If path does not contain a file extension,
        // the new file extension is appended to the path. If extension
        // is null, any exsiting extension is removed from path.
        //
        public static String ChangeExtension(String path, String extension) {
            if (path != null) {
                CheckInvalidPathChars(path);
    
                String s = path;
                for (int i = path.Length; --i >= 0;) {
                    char ch = path[i];
                    if (ch == '.') {
                        s = path.Substring(0, i);
                        break;
                    }
                    if (ch == DirectorySeparatorChar || ch == AltDirectorySeparatorChar || ch == VolumeSeparatorChar) break;
                }
                if (extension != null && path.Length != 0) {
                    if (extension.Length == 0 || extension[0] != '.') {
                        s = s + ".";
                    }
                    s = s + extension;
                }
                return s;
            }
            return null;
        }


        // Gets the length of the root DirectoryInfo or whatever DirectoryInfo markers
        // are specified for the first part of the DirectoryInfo name.
        // 
        internal static int GetRootLength(String path) {
            CheckInvalidPathChars(path);
            
            int i = 0;
            int length = path.Length;

#if !PLATFORM_UNIX
            if (length >= 1 && (IsDirectorySeparator(path[0]))) {
                // handles UNC names and directories off current drive's root.
                i = 1;
                if (length >= 2 && (IsDirectorySeparator(path[1]))) {
                    i = 2;
                    int n = 2;
                    while (i < length && ((path[i] != DirectorySeparatorChar && path[i] != AltDirectorySeparatorChar) || --n > 0)) i++;
                }
            }
            else if (length >= 2 && path[1] == VolumeSeparatorChar) {
                // handles A:\foo.
                i = 2;
                if (length >= 3 && (IsDirectorySeparator(path[2]))) i++;
            }
            return i;
#else    
            if (length >= 1 && (IsDirectorySeparator(path[0]))) {
                i = 1;
            }
            return i;
#endif // !PLATFORM_UNIX
        }

        internal static bool IsDirectorySeparator(char c) {
            return (c==DirectorySeparatorChar || c == AltDirectorySeparatorChar);
        }


        public static char[] GetInvalidPathChars()
        {
            return (char[]) RealInvalidPathChars.Clone();
        }

        public static char[] GetInvalidFileNameChars()
        {
            return (char[]) InvalidFileNameChars.Clone();
        }

        // Returns the extension of the given path. The returned value includes the
        // period (".") character of the extension except when you have a terminal period when you get String.Empty, such as ".exe" or
        // ".cpp". The returned value is null if the given path is
        // null or if the given path does not include an extension.
        //
        [Pure]
        public static String GetExtension(String path) {
            if (path==null)
                return null;

            CheckInvalidPathChars(path);
            int length = path.Length;
            for (int i = length; --i >= 0;) {
                char ch = path[i];
                if (ch == '.')
                {
                    if (i != length - 1)
                        return path.Substring(i, length - i);
                    else
                        return String.Empty;
                }
                if (ch == DirectorySeparatorChar || ch == AltDirectorySeparatorChar || ch == VolumeSeparatorChar)
                    break;
            }
            return String.Empty;
        }

        internal const int MaxLongPath = 32000;

        private const string LongPathPrefix = @"\\?\";
        private const string UNCPathPrefix = @"\\";
        private const string UNCLongPathPrefixToInsert = @"?\UNC\";
        private const string UNCLongPathPrefix = @"\\?\UNC\";

        internal unsafe static bool HasLongPathPrefix(String path)
        {
            return path.StartsWith(LongPathPrefix, StringComparison.Ordinal);
        }

        internal unsafe static String AddLongPathPrefix(String path)
        {
            if (path.StartsWith(LongPathPrefix, StringComparison.Ordinal))
                return path;

            if (path.StartsWith(UNCPathPrefix, StringComparison.Ordinal))
                return path.Insert(2, UNCLongPathPrefixToInsert); // Given \\server\share in longpath becomes \\?\UNC\server\share  => UNCLongPathPrefix + path.SubString(2); => The actual command simply reduces the operation cost.

            return LongPathPrefix + path;
        }

        internal unsafe static String RemoveLongPathPrefix(String path)
        {
            if (!path.StartsWith(LongPathPrefix, StringComparison.Ordinal))
                return path;

            if (path.StartsWith(UNCLongPathPrefix, StringComparison.OrdinalIgnoreCase))
                return path.Remove(2, 6); // Given \\?\UNC\server\share we return \\server\share => @'\\' + path.SubString(UNCLongPathPrefix.Length) => The actual command simply reduces the operation cost.

            return path.Substring(4);
        }

        internal unsafe static StringBuilder RemoveLongPathPrefix(StringBuilder pathSB)
        {
            string path = pathSB.ToString();
            if (!path.StartsWith(LongPathPrefix, StringComparison.Ordinal))
                return pathSB;

            if (path.StartsWith(UNCLongPathPrefix, StringComparison.OrdinalIgnoreCase))
                return pathSB.Remove(2, 6);  // Given \\?\UNC\server\share we return \\server\share => @'\\' + path.SubString(UNCLongPathPrefix.Length) => The actual command simply reduces the operation cost.

            return pathSB.Remove(0, 4);
        }

        // Returns the name and extension parts of the given path. The resulting
        // string contains the characters of path that follow the last
        // backslash ("\"), slash ("/"), or colon (":") character in 
        // path. The resulting string is the entire path if path 
        // contains no backslash after removing trailing slashes, slash, or colon characters. The resulting 
        // string is null if path is null.
        //
        [Pure]
        public static String GetFileName(String path) {
          if (path != null) {
                CheckInvalidPathChars(path);
    
                int length = path.Length;
                for (int i = length; --i >= 0;) {
                    char ch = path[i];
                    if (ch == DirectorySeparatorChar || ch == AltDirectorySeparatorChar || ch == VolumeSeparatorChar)
                        return path.Substring(i + 1, length - i - 1);

                }
            }
            return path;
        }

        [Pure]
        public static String GetFileNameWithoutExtension(String path) {
            path = GetFileName(path);
            if (path != null)
            {
                int i;
                if ((i=path.LastIndexOf('.')) == -1)
                    return path; // No path extension found
                else
                    return path.Substring(0,i);
            }
            return null;
        }

        internal static bool IsRelative(string path)
        {
            Contract.Assert(path != null, "path can't be null");
#if !PLATFORM_UNIX
            if ((path.Length >= 3 && path[1] == VolumeSeparatorChar && path[2] == DirectorySeparatorChar && 
                   ((path[0] >= 'a' && path[0] <= 'z') || (path[0] >= 'A' && path[0] <= 'Z'))) ||
                  (path.Length >= 2 && path[0] == '\\' && path[1] == '\\'))
#else
            if(path.Length >= 1 && path[0] == VolumeSeparatorChar)
#endif // !PLATFORM_UNIX
                return false;
            else
                return true;
        
        }
    
        // Tests if the given path contains a root. A path is considered rooted
        // if it starts with a backslash ("\") or a drive letter and a colon (":").
        //
        [Pure]
        public static bool IsPathRooted(String path) {
            if (path != null) {
                CheckInvalidPathChars(path);
    
                int length = path.Length;
#if !PLATFORM_UNIX
                if ((length >= 1 && (path[0] == DirectorySeparatorChar || path[0] == AltDirectorySeparatorChar)) || (length >= 2 && path[1] == VolumeSeparatorChar))
                    return true;
#else
                if (length >= 1 && (path[0] == DirectorySeparatorChar || path[0] == AltDirectorySeparatorChar))
                    return true;
#endif
            }
            return false;
        }

        public static String Combine(String path1, String path2) {
            if (path1==null || path2==null)
                throw new ArgumentNullException((path1==null) ? "path1" : "path2");
            Contract.EndContractBlock();
            CheckInvalidPathChars(path1);
            CheckInvalidPathChars(path2);

            return CombineNoChecks(path1, path2);
        }

        public static String Combine(String path1, String path2, String path3) {
            if (path1 == null || path2 == null || path3 == null)
                throw new ArgumentNullException((path1 == null) ? "path1" : (path2 == null) ? "path2" : "path3");
            Contract.EndContractBlock();
            CheckInvalidPathChars(path1);
            CheckInvalidPathChars(path2);
            CheckInvalidPathChars(path3);

            return CombineNoChecks(CombineNoChecks(path1, path2), path3);
        }

        public static String Combine(String path1, String path2, String path3, String path4) {
            if (path1 == null || path2 == null || path3 == null || path4 == null)
                throw new ArgumentNullException((path1 == null) ? "path1" : (path2 == null) ? "path2" : (path3 == null) ? "path3" : "path4");
            Contract.EndContractBlock();
            CheckInvalidPathChars(path1);
            CheckInvalidPathChars(path2);
            CheckInvalidPathChars(path3);
            CheckInvalidPathChars(path4);

            return CombineNoChecks(CombineNoChecks(CombineNoChecks(path1, path2), path3), path4);
        }

        public static String Combine(params String[] paths) {
            if (paths == null) {
                throw new ArgumentNullException("paths");
            }
            Contract.EndContractBlock();

            int finalSize = 0;
            int firstComponent = 0;

            // We have two passes, the first calcuates how large a buffer to allocate and does some precondition
            // checks on the paths passed in.  The second actually does the combination.

            for (int i = 0; i < paths.Length; i++) {
                if (paths[i] == null) {
                    throw new ArgumentNullException("paths");
                }

                if (paths[i].Length  == 0) {
                    continue;
                }

                CheckInvalidPathChars(paths[i]);

                if (Path.IsPathRooted(paths[i])) {
                    firstComponent = i;
                    finalSize = paths[i].Length;
                } else {
                    finalSize += paths[i].Length;
                }

                char ch = paths[i][paths[i].Length - 1];
                if (ch != DirectorySeparatorChar && ch != AltDirectorySeparatorChar && ch != VolumeSeparatorChar) 
                    finalSize++;
                }

            StringBuilder finalPath = StringBuilderCache.Acquire(finalSize);

            for (int i = firstComponent; i < paths.Length; i++) {
                if (paths[i].Length == 0) {
                    continue;
                }

                if (finalPath.Length == 0) {
                    finalPath.Append(paths[i]);
                } else {
                    char ch = finalPath[finalPath.Length - 1];
                    if (ch != DirectorySeparatorChar && ch != AltDirectorySeparatorChar && ch != VolumeSeparatorChar) {
                        finalPath.Append(DirectorySeparatorChar);
                    }

                    finalPath.Append(paths[i]);
                }
            }

            return StringBuilderCache.GetStringAndRelease(finalPath);
        }

        private static String CombineNoChecks(String path1, String path2) {
            if (path2.Length == 0)
                return path1;

            if (path1.Length == 0)
                return path2;
                
            if (IsPathRooted(path2))
                return path2;

            char ch = path1[path1.Length - 1];
            if (ch != DirectorySeparatorChar && ch != AltDirectorySeparatorChar && ch != VolumeSeparatorChar) 
                return path1 + DirectorySeparatorCharAsString + path2;
            return path1 + path2;
        }

        private static readonly Char[] s_Base32Char   = {
                'a', 'b', 'c', 'd', 'e', 'f', 'g', 'h', 
                'i', 'j', 'k', 'l', 'm', 'n', 'o', 'p',
                'q', 'r', 's', 't', 'u', 'v', 'w', 'x', 
                'y', 'z', '0', '1', '2', '3', '4', '5'};

        internal static String ToBase32StringSuitableForDirName(byte[] buff)
        {
            // This routine is optimised to be used with buffs of length 20
            Contract.Assert(((buff.Length % 5) == 0), "Unexpected hash length");

            StringBuilder sb = StringBuilderCache.Acquire();
            byte b0, b1, b2, b3, b4;
            int  l, i;
    
            l = buff.Length;
            i = 0;

            // Create l chars using the last 5 bits of each byte.  
            // Consume 3 MSB bits 5 bytes at a time.

            do
            {
                b0 = (i < l) ? buff[i++] : (byte)0;
                b1 = (i < l) ? buff[i++] : (byte)0;
                b2 = (i < l) ? buff[i++] : (byte)0;
                b3 = (i < l) ? buff[i++] : (byte)0;
                b4 = (i < l) ? buff[i++] : (byte)0;

                // Consume the 5 Least significant bits of each byte
                sb.Append(s_Base32Char[b0 & 0x1F]);
                sb.Append(s_Base32Char[b1 & 0x1F]);
                sb.Append(s_Base32Char[b2 & 0x1F]);
                sb.Append(s_Base32Char[b3 & 0x1F]);
                sb.Append(s_Base32Char[b4 & 0x1F]);
    
                // Consume 3 MSB of b0, b1, MSB bits 6, 7 of b3, b4
                sb.Append(s_Base32Char[(
                        ((b0 & 0xE0) >> 5) | 
                        ((b3 & 0x60) >> 2))]);
    
                sb.Append(s_Base32Char[(
                        ((b1 & 0xE0) >> 5) | 
                        ((b4 & 0x60) >> 2))]);
    
                // Consume 3 MSB bits of b2, 1 MSB bit of b3, b4
                
                b2 >>= 5;
    
                Contract.Assert(((b2 & 0xF8) == 0), "Unexpected set bits");
    
                if ((b3 & 0x80) != 0)
                    b2 |= 0x08;
                if ((b4 & 0x80) != 0)
                    b2 |= 0x10;
    
                sb.Append(s_Base32Char[b2]);

            } while (i < l);

            return StringBuilderCache.GetStringAndRelease(sb);
        }

        internal static bool HasIllegalCharacters(String path, bool checkAdditional)
        {
            Contract.Requires(path != null);

            if (checkAdditional)
            {
                return path.IndexOfAny(InvalidPathCharsWithAdditionalChecks) >= 0;
            }

            return path.IndexOfAny(RealInvalidPathChars) >= 0;
        }

        internal static void CheckInvalidPathChars(String path, bool checkAdditional = false)
        {
            if (path == null)
                throw new ArgumentNullException("path");

            if (Path.HasIllegalCharacters(path, checkAdditional))
                throw new ArgumentException(SR.Argument_InvalidPathChars);
        }
        
        internal static String InternalCombine(String path1, String path2) {
            if (path1==null || path2==null)
                throw new ArgumentNullException((path1==null) ? "path1" : "path2");
            Contract.EndContractBlock();
            CheckInvalidPathChars(path1);
            CheckInvalidPathChars(path2);
            
            if (path2.Length == 0)
                throw new ArgumentException(SR.Argument_PathEmpty, "path2");
            if (IsPathRooted(path2))
                throw new ArgumentException(SR.Arg_Path2IsRooted, "path2");
            int i = path1.Length;
            if (i == 0) return path2;
            char ch = path1[i - 1];
            if (ch != DirectorySeparatorChar && ch != AltDirectorySeparatorChar && ch != VolumeSeparatorChar) 
                return path1 + DirectorySeparatorCharAsString + path2;
            return path1 + path2;
        }
            
    }
}
