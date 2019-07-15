using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace ConsoleProxy
{
    class Header
    {
        public Header(string[] request)
        {
            Parse(request);
        }

        public string Hostname { get; private set; }
        public Int32 Port;
        public string RequestType;
        public byte[] Array;

        private void Parse(string[] request)
        {
            RequestType = request[0];

            if (RequestType == "CONNECT")
            {
                string requestUri = request[1];

                string[] seperator = { ":" };
                string[] uriSplit = requestUri.Split(seperator, StringSplitOptions.RemoveEmptyEntries);

                Hostname = uriSplit[0];
                if (uriSplit[1] == null)
                {
                    Port = 80;
                }
                else
                {
                    Port = Int32.Parse(uriSplit[1]);
                }
            }
            else
            {
                Hostname = request[1];
                Port = 80;
            }

            var builder = new StringBuilder();

            foreach (var _string in request)
            {
                builder.Append(_string).Append(' ');
            }
            builder.Append("\r\n");

            Array = Encoding.ASCII.GetBytes(builder.ToString());
        }
    }
}
