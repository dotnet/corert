// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Threading;
using System.Threading.Tasks;

/// <summary>
/// A set of helper to manipulate paths into a canonicalized form to ensure user-provided paths
/// match those in the ETW log.
/// </summary>
static class PathExtensions
{
    /// <summary>
    /// Millisecond timeout for file / directory deletion.
    /// </summary>
    const int DeletionTimeoutMilliseconds = 10000;

    /// <summary>
    /// Back-off for repeated checks for directory deletion. According to my local experience [trylek],
    /// when the directory is opened in the file explorer, the propagation typically takes 2 seconds.
    /// </summary>
    const int DirectoryDeletionBackoffMilliseconds = 500;

    internal static string ToAbsolutePath(this string argValue) => Path.GetFullPath(argValue);

    internal static string ToAbsoluteDirectoryPath(this string argValue) => argValue.ToAbsolutePath().StripTrailingDirectorySeparators();
        
    internal static string StripTrailingDirectorySeparators(this string str)
    {
        if (String.IsNullOrWhiteSpace(str))
        {
            return str;
        }

        while (str.Length > 0 && str[str.Length - 1] == Path.DirectorySeparatorChar)
        {
            str = str.Remove(str.Length - 1);
        }

        return str;
    }

    internal static void RecreateDirectory(this string path)
    {
        if (Directory.Exists(path))
        {
            Stopwatch stopwatch = new Stopwatch();
            stopwatch.Start();
            Task<bool> deleteSubtreeTask = path.DeleteSubtree();
            deleteSubtreeTask.Wait();
            if (deleteSubtreeTask.Result)
            {
                Console.WriteLine("Deleted {0} in {1} msecs", path, stopwatch.ElapsedMilliseconds);
            }
            else
            {
                throw new Exception($"Error: Could not delete output folder {path}");
            }
        }

        Directory.CreateDirectory(path);
    }

    internal static bool IsParentOf(this DirectoryInfo outputPath, DirectoryInfo inputPath)
    {
        DirectoryInfo parentInfo = inputPath.Parent;
        while (parentInfo != null)
        {
            if (parentInfo == outputPath)
                return true;

            parentInfo = parentInfo.Parent;
        }

        return false;
    }

    /// <summary>
    /// Asynchronous task for subtree deletion.
    /// </summary>
    /// <param name="path">Directory to delete</param>
    /// <returns>Task returning true on success, false on failure</returns>
    public static async Task<bool> DeleteSubtree(this string path)
    {
        bool succeeded = true;

        try
        {
            if (!Directory.Exists(path))
            {
                // Non-existent folders are harmless w.r.t. deletion
                Console.WriteLine("Skipping non-existent folder: '{0}'", path);
                return succeeded;
            }
            Console.WriteLine("Deleting '{0}'", path);
            var tasks = new List<Task<bool>>();
            foreach (string subfolder in Directory.EnumerateDirectories(path))
            {
                tasks.Add(Task<bool>.Run(() => subfolder.DeleteSubtree()));
            }
            string[] files = Directory.GetFiles(path);
            foreach (string file in files)
            {
                tasks.Add(Task<bool>.Run(() => file.DeleteFile()));
            }

            await Task<bool>.WhenAll(tasks);

            foreach (var task in tasks)
            {
                if (!task.Result)
                    succeeded = false;
            }
        }
        catch (Exception ex)
        {
            Console.Error.WriteLine("Error deleting '{0}': {1}", path, ex.Message);
            succeeded = false;
        }

        if (succeeded)
        {
            Stopwatch folderDeletion = new Stopwatch();
            folderDeletion.Start();
            while (Directory.Exists(path))
            {
                try
                {
                    Directory.Delete(path, recursive: false);
                }
                catch (DirectoryNotFoundException)
                {
                    // Directory not found is OK (the directory might have been deleted during the back-off delay).
                }
                catch (Exception)
                {
                    Console.WriteLine("Folder deletion failure, maybe transient ({0} msecs): '{1}'", folderDeletion.ElapsedMilliseconds, path);
                }

                if (!Directory.Exists(path))
                {
                    break;
                }

                if (folderDeletion.ElapsedMilliseconds > DeletionTimeoutMilliseconds)
                {
                    Console.Error.WriteLine("Timed out trying to delete directory '{0}'", path);
                    succeeded = false;
                    break;
                }

                Thread.Sleep(DirectoryDeletionBackoffMilliseconds);
            }
        }

        return succeeded;
    }

    private static bool DeleteFile(this string file)
    {
        try
        {
            File.Delete(file);
            return true;
        }
        catch (Exception ex)
        {
            Console.Error.WriteLine($"{file}: {ex.Message}");
            return false;
        }
    }
}
