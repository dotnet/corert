// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.AspNetCore;
using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;

using AspNetCore.Controllers;

namespace AspNetCore
{
    //
    // This functional test is the dotnet CLI WebApi template with an HttpClient
    // that makes a request to the started server and verifies it returns the 
    // expected string to the client.
    //
    public class Program
    {
        static readonly int RequestTimeOut = 20;

        public static int Main(string[] args)
        {
            int returnCode = 1;

            var cancellationToken = new CancellationTokenSource().Token;

            Console.WriteLine("Starting web host");
            IWebHost webHost = BuildWebHost(args);
            webHost.RunAsync(cancellationToken);

            try
            {
                var requestTask = TestWebRequest();
                requestTask.Wait(new TimeSpan(0, 0, RequestTimeOut));
                returnCode = requestTask.Result;
            }
            catch (Exception e)
            {
                // If the server didn't start properly
                Console.WriteLine("Web request failed");
                Console.WriteLine(e.InnerException.ToString());
                return 1;
            }

            Console.WriteLine("Shutting down web host");
            webHost.StopAsync(new TimeSpan(0, 0, RequestTimeOut));
            
            return returnCode;
        }

        private async static Task<int> TestWebRequest()
        {
            HttpClient client = new HttpClient();
            Uri requestUri = new Uri("http://localhost:5000");
            Console.WriteLine($"Requesting {requestUri.ToString()}");
            string response = await client.GetStringAsync(requestUri);
            
            if (response == ValuesController.ServerResponse)
            {
                Console.WriteLine($"Success - Server responded with {ValuesController.ServerResponse}");
                return 0;
            }
            else
            {
                Console.WriteLine($"Failed - Server responded with {ValuesController.ServerResponse}");
                return 1;
            }
        }

        public static IWebHost BuildWebHost(string[] args) =>
            WebHost.CreateDefaultBuilder(args)
                .UseStartup<Startup>()
                .Build();
    }
}
