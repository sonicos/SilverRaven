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
using System.IO;
using System.Net;
using System.Text;
using System.Threading;
using Newtonsoft.Json;
using SilverRaven.Data;
using SilverRaven.Logging;
using SilverRaven.Utilities;

#if !(net40)
#endif

namespace SilverRaven
{
    /// <summary>
    /// The Raven Client, responsible for capturing exceptions and sending them to Sentry.
    /// </summary>
    public class RavenClient : IRavenClient
    {
        private readonly Dsn _currentDsn;
        private readonly IJsonPacketFactory _jsonPacketFactory;
        public static ManualResetEvent AllDone = new ManualResetEvent(false);


        /// <summary>
        /// Initializes a new instance of the <see cref="RavenClient" /> class.
        /// </summary>
        /// <param name="dsn">The Data Source Name in Sentry.</param>
        /// <param name="jsonPacketFactory">The optional factory that will be used to create the <see cref="JsonPacket" /> that will be sent to Sentry.</param>
        public RavenClient(string dsn, IJsonPacketFactory jsonPacketFactory = null)
            : this(new Dsn(dsn), jsonPacketFactory)
        {
        }


        /// <summary>
        /// Initializes a new instance of the <see cref="RavenClient" /> class.
        /// </summary>
        /// <param name="dsn">The Data Source Name in Sentry.</param>
        /// <param name="jsonPacketFactory">The optional factory that will be used to create the <see cref="JsonPacket" /> that will be sent to Sentry.</param>
        /// <exception cref="System.ArgumentNullException">dsn</exception>
        public RavenClient(Dsn dsn, IJsonPacketFactory jsonPacketFactory = null)
        {
            if (dsn == null)
                throw new ArgumentNullException("dsn");

            _currentDsn = dsn;
            _jsonPacketFactory = jsonPacketFactory ?? new JsonPacketFactory();

            Logger = "root";
            Timeout = TimeSpan.FromSeconds(5);
            
        }

        public string FileNameOverride { get; set; }
        /// <summary>
        /// Enable Gzip Compression?
        /// Defaults to <c>false</c>.
        /// </summary>
        public bool Compression { get; set; }

        /// <summary>
        /// Tags to be included on all exception captures
        /// Default is empty
        /// </summary>
        public Dictionary<string,string> DefaultTags { get; set; } 

        /// <summary>
        /// The Dsn currently being used to log exceptions.
        /// </summary>
        public Dsn CurrentDsn
        {
            get { return _currentDsn; }
        }

        /// <summary>
        /// Interface for providing a 'log scrubber' that removes 
        /// sensitive information from exceptions sent to sentry.
        /// </summary>
        public IScrubber LogScrubber { get; set; }

        /// <summary>
        /// The name of the logger. The default logger name is "root".
        /// </summary>
        public string Logger { get; set; }

        /// <summary>
        /// Gets or sets the timeout value in milliseconds for the HTTP communication with Sentry.
        /// </summary>
        /// <value>
        /// The number of milliseconds to wait before the request times out. The default is 5,000 milliseconds (5 seconds).
        /// </value>
        public TimeSpan Timeout { get; set; }


        /// <summary>
        /// Gets or sets the <see cref="Action"/> to execute if an error occurs when executing
        /// <see cref="CaptureException"/> or <see cref="CaptureMessage"/>.
        /// </summary>
        /// <value>
        /// The <see cref="Action"/> to execute if an error occurs when executing
        /// <see cref="CaptureException"/> or <see cref="CaptureMessage"/>.
        /// </value>
        public Action<Exception> ErrorOnCapture { get; set; }


        /// <summary>
        /// Captures the <see cref="Exception" />.
        /// </summary>
        /// <param name="exception">The <see cref="Exception" /> to capture.</param>
        /// <param name="message">The optional messge to capture. Default: <see cref="Exception.Message" />.</param>
        /// <param name="level">The <see cref="ErrorLevel" /> of the captured <paramref name="exception" />. Default: <see cref="ErrorLevel.Error" />.</param>
        /// <param name="tags">The tags to annotate the captured <paramref name="exception" /> with.</param>
        /// <param name="extra">The extra metadata to send with the captured <paramref name="exception" />.</param>
        /// <returns>
        /// The <see cref="JsonPacket.EventId" /> of the successfully captured <paramref name="exception" />, or <c>null</c> if it fails.
        /// </returns>
        public string CaptureException(Exception exception,
                                       SentryMessage message = null,
                                       ErrorLevel level = ErrorLevel.Error,
                                       IDictionary<string, string> tags = null,
                                       object extra = null)
        {
            try
            {
                Dictionary<string, string> outTags = null;
                if (DefaultTags != null)
                {
                    outTags = DefaultTags;
                }
                if (tags != null)
                {
                    if (outTags != null)
                    {
                        outTags = outTags.MergeLeft(tags);
                    }
                    else
                    {
                        outTags = (Dictionary<string,string>)tags;
                    }
                }

                JsonPacket packet = _jsonPacketFactory.Create(_currentDsn.ProjectId,
                                                                  exception,
                                                                  message,
                                                                  level,
                                                                  outTags,
                                                                  extra);
                return Send(packet, CurrentDsn);
            }
            catch (Exception sendException)
            {
                return HandleException(sendException);
            }
        }


        /// <summary>
        /// Captures the message.
        /// </summary>
        /// <param name="message">The message to capture.</param>
        /// <param name="level">The <see cref="ErrorLevel" /> of the captured <paramref name="message" />. Default <see cref="ErrorLevel.Info" />.</param>
        /// <param name="tags">The tags to annotate the captured <paramref name="message" /> with.</param>
        /// <param name="extra">The extra metadata to send with the captured <paramref name="message" />.</param>
        /// <returns>
        /// The <see cref="JsonPacket.EventId" /> of the successfully captured <paramref name="message" />, or <c>null</c> if it fails.
        /// </returns>
        public string CaptureMessage(SentryMessage message,
                                     ErrorLevel level = ErrorLevel.Info,
                                     Dictionary<string, string> tags = null,
                                     object extra = null)
        {
            try
            {
                JsonPacket packet = _jsonPacketFactory.Create(CurrentDsn.ProjectId, message, level, tags, extra);
                return Send(packet, CurrentDsn);
            }
            catch (Exception sendException)
            {
                return HandleException(sendException);
            }
        }


        private string HandleException(Exception exception)
        {
            try
            {
                if (ErrorOnCapture != null)
                {
                    ErrorOnCapture(exception);
                    return null;
                }

                //Console.ForegroundColor = ConsoleColor.Red;
                Console.Write("[ERROR] ");
                //Console.ForegroundColor = ConsoleColor.Gray;
                Console.WriteLine(exception);

                WebException webException = exception as WebException;
                if (webException == null || webException.Response == null)
                    return null;

                string messageBody;
                using (Stream stream = webException.Response.GetResponseStream())
                {
                    if (stream == null)
                        return null;

                    using (StreamReader sw = new StreamReader(stream))
                        messageBody = sw.ReadToEnd();
                }

                Console.WriteLine("[MESSAGE BODY] " + messageBody);
            }
            catch (Exception onErrorException)
            {
                Console.WriteLine(onErrorException);
            }

            return null;
        }


        /// <summary>
        /// Sends the specified packet to Sentry.
        /// </summary>
        /// <param name="packet">The packet to send.</param>
        /// <param name="dsn">The Data Source Name in Sentry.</param>
        /// <returns>
        /// The <see cref="JsonPacket.EventId"/> of the successfully captured JSON packet, or <c>null</c> if it fails.
        /// </returns>
        /// 
        /// 
        protected virtual string Send(JsonPacket packet, Dsn dsn)
        {
            AllDone = new ManualResetEvent(false);
            packet.Logger = Logger;

            var request = (HttpWebRequest) WebRequest.Create(dsn.SentryUri);


            request.Method = "POST";
            request.Accept = "application/json";
            request.Headers["X-Sentry-Auth"] = PacketBuilder.CreateAuthenticationHeader(dsn);
            //request.Headers.Add("X-Sentry-Auth", PacketBuilder.CreateAuthenticationHeader(dsn));
            //request.Headers["User-Agent"] = PacketBuilder.UserAgent;


            request.ContentType = "application/json; charset=utf-8";

            var data = packet.ToString(Formatting.None);

            if (LogScrubber != null)
                data = LogScrubber.Scrub(data);

            // Write the messagebody.
            var myRequestState = new RequestState
            {
                Request = request,
                RequestData = data
            };

            request.BeginGetRequestStream(GetRequestStreamCallback, myRequestState);
            AllDone.WaitOne(Timeout);

            if (!myRequestState.IsSuccess) return "";
            try
            {
                var response = JsonConvert.DeserializeObject<dynamic>(myRequestState.ResponseData);
                return response.id;
            }
            catch (Exception ex)
            {
                Console.WriteLine("An error occurred while trying to deserialize the response from the server: {0}", ex);
            }
            return "";
        }

        private static void GetRequestStreamCallback(IAsyncResult asynchronousResult)
        {
            var myRequestState = (RequestState)asynchronousResult.AsyncState;
            var request = myRequestState.Request;

            // End the operation
            var postStream = request.EndGetRequestStream(asynchronousResult);


            var postData = myRequestState.RequestData;

            // Convert the string into a byte array. 
            var byteArray = Encoding.UTF8.GetBytes(postData);

            // Write to the request stream.
            postStream.Write(byteArray, 0, postData.Length);
            postStream.Close();
            myRequestState.Request = request;
            // Start the asynchronous operation to get the response
            try
            {
                request.BeginGetResponse(GetResponseCallback, myRequestState);
            }
            catch (Exception ex)
            {
                Console.WriteLine("An error occurred while trying to start the request: {0}", ex);
            }
        }

        private static void GetResponseCallback(IAsyncResult asynchronousResult)
        {
            var myRequestState = (RequestState)asynchronousResult.AsyncState;
            var request = myRequestState.Request;

            // End the operation
            try
            {
                var response = (HttpWebResponse) request.EndGetResponse(asynchronousResult);
                myRequestState.Response = response;
                var streamResponse = response.GetResponseStream();
                myRequestState.StreamResponse = streamResponse;
                var streamRead = new StreamReader(streamResponse);
                var responseString = streamRead.ReadToEnd();
                myRequestState.ResponseData = responseString;
                // Close the stream object
                streamResponse.Close();
                streamRead.Close();

                // Release the HttpWebResponse
                response.Close();
                myRequestState.IsSuccess = true;
            }
            catch (Exception ex)
            {
                Console.WriteLine("An error occurred while retrieving the HTTP response: {0}", ex);
            }
            finally
            {
                AllDone.Set();
            }
            
        }

        #region Deprecated Methods

        /*
         *  These methods have been deprectaed in favour of the ones
         *  that have the same names as the other sentry clients, this
         *  is purely for the sake of consistency
         */


        /// <summary>
        /// Captures the event.
        /// </summary>
        /// <param name="e">The <see cref="Exception" /> to capture.</param>
        /// <returns></returns>
        [Obsolete("The more common CaptureException method should be used")]
        public string CaptureEvent(Exception e)
        {
            return CaptureException(e);
        }


        /// <summary>
        /// Captures the event.
        /// </summary>
        /// <param name="e">The <see cref="Exception" /> to capture.</param>
        /// <param name="tags">The tags to annotate the captured exception with.</param>
        /// <returns></returns>
        [Obsolete("The more common CaptureException method should be used")]
        public string CaptureEvent(Exception e, Dictionary<string, string> tags)
        {
            return CaptureException(e, tags: tags);
        }

        #endregion
    }
}