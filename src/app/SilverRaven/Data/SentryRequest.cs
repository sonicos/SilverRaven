#region License

// Copyright (c) 2014 The Sentry Team and individual contributors.
// All rights reserved.
// 
// Redistribution and use in source and binary forms, with or without modification, are permitted
// provided that the following conditions are met:
// 
//     1. Redistributions of source code must retain the above copyright notice, this list of
//        conditions and the following disclaimer.
// 
//     2. Redistributions in binary form must reproduce the above copyright notice, this list of
//        conditions and the following disclaimer in the documentation and/or other materials
//        provided with the distribution.
// 
//     3. Neither the name of the Sentry nor the names of its contributors may be used to
//        endorse or promote products derived from this software without specific prior written
//        permission.
// 
// THIS SOFTWARE IS PROVIDED BY THE COPYRIGHT HOLDERS AND CONTRIBUTORS "AS IS" AND ANY EXPRESS OR
// IMPLIED WARRANTIES, INCLUDING, BUT NOT LIMITED TO, THE IMPLIED WARRANTIES OF MERCHANTABILITY AND
// FITNESS FOR A PARTICULAR PURPOSE ARE DISCLAIMED. IN NO EVENT SHALL THE COPYRIGHT HOLDER OR
// CONTRIBUTORS BE LIABLE FOR ANY DIRECT, INDIRECT, INCIDENTAL, SPECIAL, EXEMPLARY, OR CONSEQUENTIAL
// DAMAGES (INCLUDING, BUT NOT LIMITED TO, PROCUREMENT OF SUBSTITUTE GOODS OR SERVICES; LOSS OF USE,
// DATA, OR PROFITS; OR BUSINESS INTERRUPTION) HOWEVER CAUSED AND ON ANY THEORY OF LIABILITY,
// WHETHER IN CONTRACT, STRICT LIABILITY, OR TORT (INCLUDING NEGLIGENCE OR OTHERWISE) ARISING IN
// ANY WAY OUT OF THE USE OF THIS SOFTWARE, EVEN IF ADVISED OF THE POSSIBILITY OF SUCH DAMAGE.

#endregion

using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Reflection;
using System.Security.Principal;
using Newtonsoft.Json;

namespace SilverRaven.Data
{
    /// <summary>
    /// The Request information is stored in the Http interface. Two arguments are required: url and method.
    /// </summary>
    public class SentryRequest
    {
        private static PropertyInfo _currentHttpContextProperty;
        private readonly dynamic _httpContext;


        internal SentryRequest(dynamic httpContext)
        {
            if (httpContext == null)
                throw new ArgumentNullException("httpContext");

            _httpContext = httpContext;
            Url = _httpContext.Request.Url.ToString();
            Method = _httpContext.Request.HttpMethod;
            Environment = Convert(x => x.Request.ServerVariables);
            Headers = Convert(x => x.Request.Headers);
            Cookies = Convert(x => x.Request.Cookies);
            Data = Convert(x => x.Request.Form);
            QueryString = _httpContext.Request.QueryString.ToString();
        }


        /// <summary>
        /// Gets or sets the cookies.
        /// </summary>
        /// <value>
        /// The cookies.
        /// </value>
        [JsonProperty(PropertyName = "cookies", NullValueHandling = NullValueHandling.Ignore)]
        public IDictionary<string, string> Cookies { get; set; }

        /// <summary>
        /// The data variable should only contain the request body (not the query string). It can either be a dictionary (for standard HTTP requests) or a raw request body.
        /// </summary>
        /// <value>
        /// The data.
        /// </value>
        [JsonProperty(PropertyName = "data", NullValueHandling = NullValueHandling.Ignore)]
        public object Data { get; set; }

        /// <summary>
        /// The env variable is a compounded dictionary of HTTP headers as well as environment information passed from the webserver.
        /// Sentry will explicitly look for REMOTE_ADDR in env for things which require an IP address.
        /// </summary>
        /// <value>
        /// The environment.
        /// </value>
        [JsonProperty(PropertyName = "env", NullValueHandling = NullValueHandling.Ignore)]
        public IDictionary<string, string> Environment { get; set; }

        /// <summary>
        /// Gets or sets the headers.
        /// </summary>
        /// <value>
        /// The headers.
        /// </value>
        [JsonProperty(PropertyName = "headers", NullValueHandling = NullValueHandling.Ignore)]
        public IDictionary<string, string> Headers { get; set; }

        /// <summary>
        /// Gets or sets the method of the HTTP request.
        /// </summary>
        /// <value>
        /// The method of the HTTP request.
        /// </value>
        [JsonProperty(PropertyName = "method", NullValueHandling = NullValueHandling.Ignore)]
        public string Method { get; set; }

        /// <summary>
        /// Gets or sets the query string.
        /// </summary>
        /// <value>
        /// The query string.
        /// </value>
        [JsonProperty(PropertyName = "query_string", NullValueHandling = NullValueHandling.Ignore)]
        public string QueryString { get; set; }

        /// <summary>
        /// Gets or sets the URL of the HTTP request.
        /// </summary>
        /// <value>
        /// The URL of the HTTP request.
        /// </value>
        [JsonProperty(PropertyName = "url", NullValueHandling = NullValueHandling.Ignore)]
        public string Url { get; set; }


        /// <summary>
        /// Gets the request.
        /// </summary>
        /// <returns>
        /// If an HTTP contest is available, an instance of <see cref="SentryRequest"/>, otherwise <c>null</c>.
        /// </returns>
        public static SentryRequest GetRequest()
        {
            try
            {
                var httpContext = GetHttpContext();
                return httpContext != null ? new SentryRequest(httpContext) : null;
            }
            catch (Exception exception)
            {
                Console.WriteLine(exception);
            }

            return null;
        }


        /// <summary>
        /// Gets the user.
        /// </summary>
        /// <returns>
        /// If an HTTP context is available, an instance of <see cref="SentryUser"/>, otherwise <c>null</c>.
        /// </returns>
        public SentryUser GetUser()
        {
            return new SentryUser(GetPrincipal())
            {
                IpAddress = GetIpAddress()
            };
        }


        private IDictionary<string, string> Convert(Func<dynamic, WebHeaderCollection> collectionGetter)
        {
            IDictionary<string, string> dictionary = new Dictionary<string, string>();
           

            try
            {
                var collection = collectionGetter.Invoke(_httpContext);
                var keys = Enumerable.ToArray(collection.AllKeys);

                foreach (object key in keys)
                {
                    if (key == null)
                        continue;

                    var stringKey = key as string ?? key.ToString();

                    // NOTE: Ignore these keys as they just add duplicate information. [asbjornu]
                    if (stringKey.StartsWith("ALL_") || stringKey.StartsWith("HTTP_"))
                        continue;

                    var value = collection[stringKey];
                    var stringValue = value as string;

                    if (stringValue != null)
                    {
                        // Most dictionary values will be strings and go through this path.
                        dictionary.Add(stringKey, stringValue);
                    }
                    else
                    {
                        // HttpCookieCollection is an ugly, evil beast that needs to be treated with a sledgehammer.

                        try
                        {
                            // For whatever stupid reason, HttpCookie.ToString() doesn't return its Value, so we need to dive into the .Value property like this.
                            dictionary.Add(stringKey, value.Value);
                        }
                        catch (Exception exception)
                        {
                            dictionary.Add(stringKey, exception.ToString());
                        }
                    }
                }
            }
            catch (Exception exception)
            {
                Console.WriteLine(exception);
            }

            return dictionary;
        }


        private static PropertyInfo GetHttpContextCurrentProperty()
        {
            var systemWeb = AppDomain.CurrentDomain
                                     .GetAssemblies()
                                     .FirstOrDefault(assembly => assembly.FullName.StartsWith("System.Web,"));

            if (systemWeb == null)
                return null;

            var httpContextType = systemWeb.GetExportedTypes()
                                           .FirstOrDefault(type => type.Name == "HttpContext");

            if (httpContextType == null)
                return null;

            return httpContextType.GetProperty("Current", BindingFlags.Static | BindingFlags.Public);
        }


        private static dynamic GetHttpContext()
        {
            try
            {
                if (_currentHttpContextProperty == null)
                    _currentHttpContextProperty = GetHttpContextCurrentProperty();

                if (_currentHttpContextProperty != null)
                    return _currentHttpContextProperty.GetValue(null, null);
            }
            catch (Exception exception)
            {
                Console.WriteLine("An error occurred while retrieving the HTTP context: {0}", exception);
            }

            return null;
        }


        private dynamic GetIpAddress()
        {
            try
            {
                return _httpContext.Request.UserHostAddress;
            }
            catch (Exception exception)
            {
                Console.WriteLine(exception);
            }

            return null;
        }


        private IPrincipal GetPrincipal()
        {
            try
            {
                return _httpContext.User as IPrincipal;
            }
            catch (Exception exception)
            {
                Console.WriteLine(exception);
            }

            return null;
        }
    }
}