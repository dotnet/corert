using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Compression;
using System.Net.Http;
using System.Threading.Tasks;
using Newtonsoft.Json;

namespace CoreFXTestUtils
{
    public static class ArchiveTestUtils
    {
        static string testUrl = "https://dotnetbuilddrops.blob.core.windows.net/build-e0a70e19967c4fbfa9e6a9e7fed567cc/TestList.json?sv=2015-04-05&sr=c&sig=rwqOIDM585nrCg4ydSleaYi%2FN1%2BcFqEivySw06MiZRs%3D&st=2018-01-12T01%3A34%3A06.4065828Z&se=2018-02-11T01%3A34%3A06.4065828Z&sp=r";
        static Dictionary<string,string> testNames = new Dictionary<string,string>(){ { "Common.Tests", "" }, { "System.Collections.Tests", "" }, { "asdasdasd", "" } };
        private static HttpClient httpClient;

        static string baseOutputDir = @"C:\Users\anandono\source\repos\CoreFXTestsDownloaded\";

        public static void Main(string[] args)
        {
            if(args.Length < 1)
            {
                throw new ArgumentException("Please supply an output directory");
            }

            if (!Directory.Exists(baseOutputDir))
            {
                Directory.CreateDirectory(baseOutputDir);
            }
            
            // parse args
            SetupTests(testNames, testUrl, baseOutputDir);
        }

        public static void SetupTests(Dictionary<string, string> testNames, String jsonUrl, string destinationDirectory)
        {
            string tempDirPath = Path.Combine(destinationDirectory, "temp");
            if (!Directory.Exists(tempDirPath))
            {
                Directory.CreateDirectory(tempDirPath);
            }

            Dictionary<string, string> testPayloads = GetTestUrls(testNames, testUrl).Result;
            GetTestArchives(testPayloads, tempDirPath).Wait();
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
            using (var responseStream = httpClient.GetStreamAsync(jsonUrl).Result)
            using (var streamReader = new StreamReader(responseStream))
            using (var jsonReader = new JsonTextReader(streamReader))
            {
                // Manual parsing - we only need to key-value pairs from each object and this avoids deserializing all of the work items into objects
                string markedTestName = String.Empty;
                string currentPropertyName = String.Empty;

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
                                else if(currentPropertyName.Equals("PayloadUri") && markedTestName != String.Empty)
                                {
                                    testNames[markedTestName] = jsonReader.Value.ToString();
                                    markedTestName = String.Empty;
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
        public static void ExpandArchivesInDirectory(string archiveDirectory)
        {
            ExpandArchivesInDirectory(archiveDirectory, archiveDirectory);
        }

        public static void ExpandArchivesInDirectory(string archiveDirectory, string destinationDirectory, bool cleanup = true)
        {
            string[] archives = Directory.GetFiles(archiveDirectory, "*.zip", SearchOption.TopDirectoryOnly);

            foreach (string archivePath in archives)
            {
                string destinationDirName = Path.GetFileNameWithoutExtension(archivePath);
                ZipFile.ExtractToDirectory(archivePath, destinationDirectory + destinationDirName);

                // Delete archives
                if (cleanup)
                {
                    File.Delete(archivePath);
                }
            }
        }


    }
}
