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

namespace SilverRaven
{
    /// <summary>
    /// The Data Source Name of a given project in Sentry.
    /// </summary>
    public class Dsn
    {
        private readonly string _path;
        private readonly int _port;
        private readonly string _privateKey;
        private readonly string _projectId;
        private readonly string _publicKey;
        private readonly Uri _sentryUri;
        private readonly Uri _uri;


        /// <summary>
        /// Initializes a new instance of the <see cref="Dsn"/> class.
        /// </summary>
        /// <param name="dsn">The Data Source Name.</param>
        public Dsn(string dsn)
        {
            if (string.IsNullOrWhiteSpace(dsn))
                throw new ArgumentNullException("dsn");

            try
            {
                _uri = new Uri(dsn);
                _privateKey = GetPrivateKey(_uri);
                _publicKey = GetPublicKey(_uri);
                _port = _uri.Port;
                _projectId = GetProjectId(_uri);
                _path = GetPath(_uri);
                var sentryUriString = $"{_uri.Scheme}://{_uri.DnsSafeHost}:{Port}{Path}/api/{ProjectId}/store/";
                _sentryUri = new Uri(sentryUriString);
            }
            catch (Exception exception)
            {
                
                throw new ArgumentException("Invalid DSN",  exception);
            }
        }


        /// <summary>
        /// Sentry path.
        /// </summary>
        public string Path
        {
            get { return _path; }
        }

        /// <summary>
        /// The sentry server port.
        /// </summary>
        public int Port
        {
            get { return _port; }
        }

        /// <summary>
        /// Project private key.
        /// </summary>
        public string PrivateKey
        {
            get { return _privateKey; }
        }

        /// <summary>
        /// Project identification.
        /// </summary>
        public string ProjectId
        {
            get { return _projectId; }
        }

        /// <summary>
        /// Project public key.
        /// </summary>
        public string PublicKey
        {
            get { return _publicKey; }
        }

        /// <summary>
        /// Sentry Uri for sending reports.
        /// </summary>
        public Uri SentryUri
        {
            get { return _sentryUri; }
        }

        /// <summary>
        /// Absolute Dsn Uri
        /// </summary>
        public Uri Uri
        {
            get { return _uri; }
        }


        /// <summary>
        /// Returns a <see cref="System.String" /> that represents this instance.
        /// </summary>
        /// <returns>
        /// A <see cref="System.String" /> that represents this instance.
        /// </returns>
        public override string ToString()
        {
            return _uri.ToString();
        }


        /// <summary>
        /// Get a path from a Dsn uri
        /// </summary>
        /// <param name="uri"></param>
        /// <returns></returns>
        private static string GetPath(Uri uri)
        {
            int lastSlash = uri.AbsolutePath.LastIndexOf("/", StringComparison.Ordinal);
            return uri.AbsolutePath.Substring(0, lastSlash);
        }


        /// <summary>
        /// Get a private key from a Dsn uri.
        /// </summary>
        /// <param name="uri"></param>
        /// <returns></returns>
        private static string GetPrivateKey(Uri uri)
        {
            return uri.UserInfo.Split(':')[1];
        }


        /// <summary>
        /// Get a project ID from a Dsn uri.
        /// </summary>
        /// <param name="uri"></param>
        /// <returns></returns>
        private static string GetProjectId(Uri uri)
        {
            int lastSlash = uri.AbsoluteUri.LastIndexOf("/", StringComparison.Ordinal);
            return uri.AbsoluteUri.Substring(lastSlash + 1);
        }


        /// <summary>
        /// Get a public key from a Dsn uri.
        /// </summary>
        /// <param name="uri"></param>
        /// <returns></returns>
        private static string GetPublicKey(Uri uri)
        {
            return uri.UserInfo.Split(':')[0];
        }
    }
}