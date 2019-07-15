using System;
using System.Collections.Generic;
using System.Net;

namespace ConsoleProxy
{
    public class CacheData
    {
        public CacheKey Key { get; set; }
        public DateTime? ExpiresDT { get; set; }
        public DateTime StoredDT { get; set; }
        public Byte[] ResponseBytes { get; set; }
        public HttpStatusCode StatusCode { get; set; }
        public String StatusDescription { get; set; }
        public List<Tuple<String, String>> Headers { get; set; }
        public Boolean IsToBeRemoved { get; set; }
    }
}
