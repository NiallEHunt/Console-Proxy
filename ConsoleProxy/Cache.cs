using System;
using System.Collections;
using System.Collections.Generic;
using System.Net;
using System.Threading;

namespace ConsoleProxy
{
    class Cache
    {
        private static Hashtable cacheTable = new Hashtable();
        private static readonly Object cacheLock = new object();
        private static readonly Object statsLock = new object();
        private static Int32 hits;

        // Checks the hashtable for the given CacheKey.
        // If it is present it then checks to see if it has expired. 
        // Returns null if not found or expired.
        public static CacheData GetData(HttpWebRequest request)
        {
            CacheKey key = new CacheKey(request.RequestUri.AbsoluteUri, request.UserAgent);
            if (cacheTable[key] != null)
            { 
                CacheData data = (CacheData)cacheTable[key];
                if (data.IsToBeRemoved || (data.ExpiresDT.HasValue && data.ExpiresDT < DateTime.Now))
                {
                    // Marks the entry to be removed for the cache maintenance thread to remove
                    data.IsToBeRemoved = true;
                    return null;
                }
                // Locks so that other threads can not edit the hits counter at the same time
                Monitor.Enter(statsLock);
                hits++;
                Monitor.Exit(statsLock);
                return data;
            }
            return null;
        }

        // Returns a CacheData object to represent the given request and response
        public static CacheData AddDataEntry(HttpWebRequest request, HttpWebResponse response, List<Tuple<String, String>> headers, DateTime? expires)
        {
            CacheData newEntry = new CacheData
            {
                ExpiresDT = expires,
                StoredDT = DateTime.Now,
                Headers = headers,
                Key = new CacheKey(request.RequestUri.AbsoluteUri, request.UserAgent),
                StatusCode = response.StatusCode,
                StatusDescription = response.StatusDescription
            };
            if (response.ContentLength > 0)
                newEntry.ResponseBytes = new Byte[response.ContentLength];
            return newEntry;
        }

        // Adds the given CacheEntry to the HashTable
        public static void AddData(CacheData entry)
        { 
            // Locks so that only this thread can edit the HashTable at this time to avoid incorrect stored info
            Monitor.Enter(cacheLock);
            if (!cacheTable.Contains(entry.Key))
                cacheTable.Add(entry.Key, entry);
            Monitor.Exit(cacheLock);
        }

        // Checks the headers to see if the response can be cached or not
        // Also updates the DataTime that the response will expire
        public static Boolean CanCache(WebHeaderCollection headers, ref DateTime? expires)
        {
            foreach (String headerKey in headers.AllKeys)
            {
                String value = headers[headerKey].ToLower();
                switch (headerKey.ToLower())
                {
                    case "cache-control":
                        if (value.Contains("private") || value.Contains("no-cache"))
                            return false;
                        else if (value.Contains("public") || value.Contains("no-store"))
                            return true;

                        if (value.Contains("max-age"))
                        {
                            if (int.TryParse(value, out int seconds))
                            {
                                if (seconds == 0)
                                    return false;

                                DateTime maxAgeDT = DateTime.Now.AddSeconds(seconds);
                                if (!expires.HasValue || expires.Value < maxAgeDT)
                                    expires = maxAgeDT;
                            }
                        }
                        break;
                    case "pragma":
                        if (value == "no-cache")
                            return false;
                        break;
                    case "expires":
                        DateTime expiresDT;
                        if (DateTime.TryParse(value, out expiresDT))
                        {
                            if (!expires.HasValue || expires.Value < expiresDT)
                                expires = expiresDT;
                        }
                        break;
                }
            }
            return true;
        }

        // Runs every 30 seconds. Removes cache entries that have expired
        public static void CacheMaintenance()
        {
            try
            {
                while (true)
                {
                    Thread.Sleep(30000);
                    List<CacheKey> keysToRemove = new List<CacheKey>();
                    foreach (CacheKey key in cacheTable.Keys)
                    {
                        CacheData entry = (CacheData)cacheTable[key];
                        if (entry.IsToBeRemoved || entry.ExpiresDT < DateTime.Now)
                            keysToRemove.Add(key);
                    }

                    foreach (CacheKey key in keysToRemove)
                        cacheTable.Remove(key);

                    Console.WriteLine(String.Format("Cache maintenance complete.  Number of items stored={0} Number of cache hits={1} ", cacheTable.Count, hits));
                }
            }
            catch (ThreadAbortException) { }
        }
    }
}
