
using System.IO;
using System.Net;

namespace SilverRaven
{
    internal class RequestState
    {
        // This class stores the State of the request.
        
        public string RequestData = "";
        public string ResponseData = "";
        public HttpWebRequest Request;
        public HttpWebResponse Response;
        public Stream StreamResponse;
        public bool IsSuccess = false;

        internal RequestState()
        {
            Response = null;
            Request = null;
            StreamResponse = null;
        }
    }
}
