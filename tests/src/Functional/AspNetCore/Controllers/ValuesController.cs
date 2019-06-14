// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc;

namespace AspNetCore.Controllers
{
    public class ValuesController
    {
        public static readonly string ServerResponse = "Hello World!";

        [HttpGet("/")]
        public string Hello() => ServerResponse;

        // GET api/values
        [HttpGet("/api/values")]
        public IEnumerable<string> Get()
        {
            return new string[] { "value1", "value2" };
        }

        // GET api/values/5
        [HttpGet("/api/values/{id}")]
        public string Get(int id)
        {
            return "value is " + id;
        }
    }
}
