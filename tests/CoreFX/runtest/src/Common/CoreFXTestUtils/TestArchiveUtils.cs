using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.IO.Compression;
using System.Net.Http;
using System.Threading.Tasks;
using Newtonsoft.Json;

namespace CoreFXTestUtils.TestArchiveUtils
{
    public static class TestArchiveUtils
    {
        static Dictionary<string,string> testNames = new Dictionary<string,string>(){ { "Common.Tests", "" }, { "System.Collections.Tests", "" }, { "asdasdasd", "" } };
        private static HttpClient httpClient;
        private static bool cleanTestBuild = false;

        public static void Main(string[] args)
        {
            string outputDir = string.Empty;
            string testUrl = string.Empty;
            string testListPath = string.Empty;

            Console.WriteLine("Passed " + args.Length + " arguments; which are");

            foreach (string arg in args)
            {
                Console.WriteLine(arg);
            }

            for (int i = 0; i < args.Length; i++)
            {
                switch (args[i])
                {
                    case "--outputDirectory":
                    case "--out":
                    case "--outputDir":
                        if(i + 1 < args.Length && !args[i+1].Substring(0,2).Equals("--"))
                        {
                            outputDir = args[i + 1];
                            i++;
                        }
                        break;
                    case "--testUrl":
                        if (i + 1 < args.Length && !args[i + 1].Substring(0, 2).Equals("--"))
                        {
                            testUrl = args[i + 1];
                            i++;
                        }
                        break;
                    case "--testListJsonPath":
                        if (i + 1 < args.Length && !args[i + 1].Substring(0, 2).Equals("--"))
                        {
                            testListPath = args[i + 1];
                            i++;
                        }
                        break;
                    case "--clean":
                        cleanTestBuild = true;
                        break;
                        
                }
            }

            if (!Directory.Exists(outputDir))
            {
                Directory.CreateDirectory(outputDir);
            }
            // parse args
            SetupTests(testNames, testUrl, outputDir).Wait();
        }

        public static Dictionary<string, string> ReadTestNames(string testFilePath)
        {
            Debug.Assert(File.Exists(testFilePath));

            Dictionary<string, string> testNames = new Dictionary<string, string>();
            // We assume we're being a passed a list of test assembly names, so ignore anything that's not a string
            using (var sr = new StreamReader(testFilePath))
            using (var jsonReader = new JsonTextReader(sr))
            {
                while (jsonReader.Read())
                {
                    if(jsonReader.TokenType == JsonToken.String)
                    {
                        testNames.Add(jsonReader.Value.ToString(), string.Empty);
                    }
                }
            }

            return testNames;
        }

        public static async Task SetupTests(Dictionary<string, string> testNames, string jsonUrl, string destinationDirectory)
        {
            Debug.Assert(Directory.Exists(destinationDirectory));

            string tempDirPath = Path.Combine(destinationDirectory, "temp");
            if (!Directory.Exists(tempDirPath))
            {
                Directory.CreateDirectory(tempDirPath);
            }

            Dictionary<string, string> testPayloads = await GetTestUrls(testNames, jsonUrl);
            await GetTestArchives(testPayloads, tempDirPath);
            ExpandArchivesInDirectory(tempDirPath, destinationDirectory);

            Directory.Delete(tempDirPath);
        }

        public static async Task<Dictionary<string, string>> GetTestUrls(Dictionary<string,string> testNames, string jsonUrl)
        {
            if(httpClient is null)
            {
                httpClient = new HttpClient();
            }

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
                    if(jsonReader.Value != null)
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
                                    // Test if we've added the key
                                    //// TODO add per test fetching
                                    if (testNames.ContainsKey(currentTestName))
                                    {
                                        markedTestName = currentTestName;
                                    }
                                }
                                else if(currentPropertyName.Equals("PayloadUri") && markedTestName != string.Empty)
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

            foreach(string testName in testPayloads.Keys)
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
