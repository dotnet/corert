// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Collections.Generic;
using System.CommandLine;
using System.Diagnostics;
using System.IO;
using System.IO.Compression;
using System.Net.Http;
using System.Threading.Tasks;
using Newtonsoft.Json;

namespace CoreFX.TestUtils.TestFileSetup
{
    /// <summary>
    /// Defines the set of flags that represent exit codes
    /// </summary>
    [Flags]
    public enum ExitCode : int
    {
        Success = 0,
        HttpError = 1,
        IOError = 2,
        UnknownError = 10

    }

    /// <summary>
    /// This helper class is used to fetch CoreFX tests from a specified URL, unarchive them and create a flat directory structure
    /// through which to iterate.
    /// </summary>
    public static class TestFileSetup
    {
        private static HttpClient httpClient;
        private static bool cleanTestBuild = false;

        private static string outputDir;
        private static string testUrl;
        private static string testListPath;

        public static void Main(string[] args)
        {
            ExitCode exitCode = ExitCode.UnknownError;
            ArgumentSyntax argSyntax = ParseCommandLine(args);

            if (!Directory.Exists(outputDir))
            {
                try
                {
                    Directory.CreateDirectory(outputDir);
                }
                catch (IOException)
                {
                    exitCode = ExitCode.IOError;
                    Environment.Exit((int)exitCode);
                }
            }

            // parse args
            try
            {
                SetupTests(testUrl, outputDir, ReadTestNames(testListPath)).Wait();
                exitCode = ExitCode.Success;

            }
            catch (HttpRequestException)
            {
                exitCode = ExitCode.HttpError;
            }
            catch (IOException)
            {
                exitCode = ExitCode.IOError;
            }

            Environment.Exit((int)exitCode);
        }

        public static ArgumentSyntax ParseCommandLine(string[] args)
        {
            ArgumentSyntax argSyntax = ArgumentSyntax.Parse(args, syntax =>
            {
                syntax.DefineOption("out|outDir|outputDirectory", ref outputDir, "Directory where tests are downloaded");
                syntax.DefineOption("testUrl", ref testUrl, "URL, pointing to the list of tests");
                syntax.DefineOption("testListJsonPath", ref testListPath, "JSON-formatted list of test assembly names to download");
                syntax.DefineOption("clean", ref cleanTestBuild, "Remove all previously built test assemblies");
            });

            return argSyntax;
        }

        public static Dictionary<string, string> ReadTestNames(string testFilePath)
        {
            Debug.Assert(File.Exists(testFilePath));

            Dictionary<string, string> testNames = new Dictionary<string, string>();

            // We're being a passed a list of test assembly names, so anything that's not a string is invalid
            using (var sr = new StreamReader(testFilePath))
            using (var jsonReader = new JsonTextReader(sr))
            {
                while (jsonReader.Read())
                {
                    if (jsonReader.TokenType == JsonToken.String)
                    {
                        testNames.Add(jsonReader.Value.ToString(), string.Empty);
                    }
                }
            }

            return testNames;
        }

        public static async Task SetupTests(string jsonUrl, string destinationDirectory, Dictionary<string, string> testNames = null, bool runAllTests = false)
        {
            Debug.Assert(Directory.Exists(destinationDirectory));
            Debug.Assert(runAllTests || testNames != null);

            string tempDirPath = Path.Combine(destinationDirectory, "temp");
            if (!Directory.Exists(tempDirPath))
            {
                Directory.CreateDirectory(tempDirPath);
            }
            Dictionary<string, string> testPayloads = await GetTestUrls(jsonUrl, testNames, runAllTests);

            await GetTestArchives(testPayloads, tempDirPath);
            ExpandArchivesInDirectory(tempDirPath, destinationDirectory);

            Directory.Delete(tempDirPath);
        }
        public static async Task<Dictionary<string, string>> GetTestUrls(string jsonUrl, Dictionary<string, string> testNames = null, bool runAllTests = false)
        {
            if (httpClient is null)
            {
                httpClient = new HttpClient();
            }

            Debug.Assert(runAllTests || testNames != null);

            // Set up the json stream reader
            using (var responseStream = await httpClient.GetStreamAsync(jsonUrl))
            using (var streamReader = new StreamReader(responseStream))
            using (var jsonReader = new JsonTextReader(streamReader))
            {
                // Manual parsing - we only need to key-value pairs from each object and this avoids deserializing all of the work items into objects
                string markedTestName = string.Empty;
                string currentPropertyName = string.Empty;

                while (jsonReader.Read())
                {
                    if (jsonReader.Value != null)
                    {
                        switch (jsonReader.TokenType)
                        {
                            case JsonToken.PropertyName:
                                currentPropertyName = jsonReader.Value.ToString();
                                break;
                            case JsonToken.String:
                                if (currentPropertyName.Equals("WorkItemId"))
                                {
                                    string currentTestName = jsonReader.Value.ToString();

                                    if (runAllTests || testNames.ContainsKey(currentTestName))
                                    {
                                        markedTestName = currentTestName;
                                    }
                                }
                                else if (currentPropertyName.Equals("PayloadUri") && markedTestName != string.Empty)
                                {
                                    testNames[markedTestName] = jsonReader.Value.ToString();
                                    markedTestName = string.Empty;
                                }
                                break;
                        }
                    }
                }

            }
            return testNames;
        }

        public static async Task GetTestArchives(Dictionary<string, string> testPayloads, string downloadDir)
        {
            if (httpClient is null)
            {
                httpClient = new HttpClient();
            }

            foreach (string testName in testPayloads.Keys)
            {
                string payloadUri = testPayloads[testName];

                if (!Uri.IsWellFormedUriString(payloadUri, UriKind.Absolute))
                    continue;

                using (var response = await httpClient.GetStreamAsync(payloadUri))
                {
                    if (response.CanRead)
                    {
                        // Create the test setup directory if it doesn't exist
                        if (!Directory.Exists(downloadDir))
                        {
                            Directory.CreateDirectory(downloadDir);
                        }

                        // CoreFX test archives are output as .zip regardless of platform
                        string archivePath = Path.Combine(downloadDir, testName + ".zip");

                        // Copy to a temp folder 
                        using (FileStream file = new FileStream(archivePath, FileMode.Create))
                        {
                            await response.CopyToAsync(file);
                        }

                    }
                }
            }
        }

        public static void ExpandArchivesInDirectory(string archiveDirectory, string destinationDirectory, bool cleanup = true)
        {
            Debug.Assert(Directory.Exists(archiveDirectory));
            Debug.Assert(Directory.Exists(destinationDirectory));

            string[] archives = Directory.GetFiles(archiveDirectory, "*.zip", SearchOption.TopDirectoryOnly);

            foreach (string archivePath in archives)
            {
                string destinationDirName = Path.Combine(destinationDirectory, Path.GetFileNameWithoutExtension(archivePath));

                // If doing clean test build - delete existing artefacts
                if (Directory.Exists(destinationDirName) && cleanTestBuild)
                {
                    Directory.Delete(destinationDirName, true);
                }

                ZipFile.ExtractToDirectory(archivePath, destinationDirName);


                // Delete archives
                if (cleanup)
                {
                    File.Delete(archivePath);
                }
            }
        }


    }
}
