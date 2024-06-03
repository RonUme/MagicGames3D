// (c) 2016-2023 Martin Cvengros. All rights reserved. Redistribution of source code without permission not allowed.

using System;
using System.Collections;
using System.Net;
using System.Net.Sockets;

namespace AudioStreamSupport
{
    /// <summary>
    /// (missing) UWR features
    /// </summary>
    public static class UWR
    {
        /// <summary>
        /// :(almost literally): https://stackoverflow.com/a/28424940/503221 : 
        /// </summary>
        /// <param name="fromUrl"></param>
        /// <param name="OnResolved"></param>
        /// <returns></returns>
        public static IEnumerator TryToResolveUrl_CR(string fromUrl, Action<string, string> OnResolved)
        {
            int maxRedirCount = 8;  // prevent infinite loops
            var newUrl = fromUrl;

            do
            {
                HttpWebRequest req = null;
                HttpWebResponse resp = null;

                try
                {
                    req = (HttpWebRequest)HttpWebRequest.Create(fromUrl);
                    req.Method = "HEAD";
                    req.AllowAutoRedirect = false;
                    resp = (HttpWebResponse)req.GetResponse();
                }
                catch (WebException ex)
                {
                    var err = ex.Message + (ex.InnerException != null ? " - " + ex.InnerException.Message : string.Empty);
                    // Return the last known good URL
                    OnResolved(newUrl, err);
                    yield break;
                }
                catch (Exception ex)
                {
                    var err = ex.Message + (ex.InnerException != null ? " - " + ex.InnerException.Message : string.Empty);
                    OnResolved(string.Empty, err);
                    yield break;
                }
                finally
                {
                    if (resp != null)
                        resp.Close();
                }

                switch (resp.StatusCode)
                {
                    case HttpStatusCode.OK:
                        OnResolved(newUrl, string.Empty);
                        yield break;

                    case HttpStatusCode.Redirect:
                    case HttpStatusCode.MovedPermanently:
                    case HttpStatusCode.RedirectKeepVerb:
                    case HttpStatusCode.RedirectMethod:
                        newUrl = resp.Headers["Location"];
                        if (newUrl == null)
                        {
                            OnResolved(fromUrl, string.Empty);
                            yield break;
                        }

                        if (newUrl.IndexOf("://", System.StringComparison.Ordinal) == -1)
                        {
                            // Doesn't have a URL Schema, meaning it's a relative or absolute URL
                            Uri u = new Uri(new Uri(fromUrl), newUrl);
                            newUrl = u.ToString();
                        }
                        break;

                    default:
                        OnResolved(newUrl, string.Empty);
                        yield break;
                }

                fromUrl = newUrl;

                yield return null;

            } while (maxRedirCount-- > 0);

            OnResolved(newUrl, string.Empty);
        }
        /// <summary>
        /// 
        /// </summary>
        /// <param name="fromUrl"></param>
        /// <returns></returns>
        public static (string, string) TryToResolveUrl(string fromUrl)
        {
            int maxRedirCount = 8;  // prevent infinite loops
            var newUrl = fromUrl;

            do
            {
                HttpWebRequest req = null;
                HttpWebResponse resp = null;

                try
                {
                    req = (HttpWebRequest)HttpWebRequest.Create(fromUrl);
                    req.Method = "HEAD";
                    req.AllowAutoRedirect = false;
                    resp = (HttpWebResponse)req.GetResponse();
                }
                catch (WebException ex)
                {
                    var err = ex.Message + (ex.InnerException != null ? " - " + ex.InnerException.Message : string.Empty);
                    // Return the last known good URL
                    return (newUrl, err);
                }
                catch (Exception ex)
                {
                    var err = ex.Message + (ex.InnerException != null ? " - " + ex.InnerException.Message : string.Empty);
                    return (string.Empty, err);
                }
                finally
                {
                    if (resp != null)
                        resp.Close();
                }

                switch (resp.StatusCode)
                {
                    case HttpStatusCode.OK:
                        return (newUrl, string.Empty);

                    case HttpStatusCode.Redirect:
                    case HttpStatusCode.MovedPermanently:
                    case HttpStatusCode.RedirectKeepVerb:
                    case HttpStatusCode.RedirectMethod:
                        newUrl = resp.Headers["Location"];
                        if (newUrl == null)
                        {
                            return (fromUrl, string.Empty);
                        }

                        if (newUrl.IndexOf("://", System.StringComparison.Ordinal) == -1)
                        {
                            // Doesn't have a URL Schema, meaning it's a relative or absolute URL
                            Uri u = new Uri(new Uri(fromUrl), newUrl);
                            newUrl = u.ToString();
                        }
                        break;

                    default:
                        return (newUrl, string.Empty);
                }

                fromUrl = newUrl;

            } while (maxRedirCount-- > 0);

            return (newUrl, string.Empty);
        }
        /// <summary>
        /// Local IP(v4) of the machine
        /// </summary>
        /// <returns></returns>
        public static string GetLocalIpAddress()
        {
            string localIP = "unknown";
            var host = Dns.GetHostEntry(Dns.GetHostName());
            foreach (IPAddress ip in host.AddressList)
            {
                if (ip.AddressFamily == AddressFamily.InterNetwork)
                {
                    localIP = ip.ToString();
                    break;
                }
            }
            return localIP;
        }
    }
}