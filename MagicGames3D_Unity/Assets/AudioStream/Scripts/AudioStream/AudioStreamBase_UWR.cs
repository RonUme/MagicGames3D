// (c) 2016-2023 Martin Cvengros. All rights reserved. Redistribution of source code without permission not allowed.
// uses FMOD by Firelight Technologies Pty Ltd

using AudioStreamSupport;
using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Networking;

namespace AudioStream
{
    /// <summary>
    /// UWR + download handler/s for network and local filesystem access
    /// </summary>
    public abstract partial class AudioStreamBase : MonoBehaviour
    {
        // ========================================================================================================================================
        #region UnityWebRequest
        #region Custom certificate handler
#if UNITY_2018_1_OR_NEWER
        /// <summary>
        /// https://docs.unity3d.com/ScriptReference/Networking.CertificateHandler.html
        /// Note: Custom certificate validation is currently only implemented for the following platforms - Android, iOS, tvOS and desktop platforms.
        /// </summary>
        class NoCheckPolicyCertificateHandler : CertificateHandler
        {
            protected override bool ValidateCertificate(byte[] certificateData)
            {
                // Allow all certificates to pass..
                return true;

                /*
                 * optional key check:
                 */

                // var certificate = new System.Security.Cryptography.X509Certificates.X509Certificate2(certificateData);
                // var pubk = certificate.GetPublicKeyString();
                // Debug.LogFormat("Certificate public key: {0}", pubk);

                // if (pk.ToLowerInvariant().Equals(PUBLIC_KEY.ToLower())) ..
                // ;
            }
        }
#endif
        #endregion
        #region Custom byte stream download handler
        /// <summary>
        /// Custom download handler which writes to injected FMOD filesystem
        /// </summary>
        class ByteStreamDownloadHandler : DownloadHandlerScript
        {
            /// <summary>
            /// Content-Lenght returned in response header should be the lenght of the body/content, but it's not always the case, see ReceiveContentLengthHeader 
            /// </summary>
            public uint contentLength = INFINITE_LENGTH;
            public uint downloaded;
            public bool downloadComplete = false;
            /// <summary>
            /// injected the whole class mainly for logging
            /// </summary>
            AudioStreamBase audioStream;
            /// <summary>
            /// Pre-allocated scripted download handler - should eliminate memory allocations
            /// </summary>
            /// <param name="downloadHandlerBuffer"></param>
            /// <param name="audioStreamWithFileSystem"></param>
            public ByteStreamDownloadHandler(byte[] downloadHandlerBuffer, AudioStreamBase audioStreamWithFileSystem)
                : base(downloadHandlerBuffer)
            {
                this.contentLength = INFINITE_LENGTH;
                this.downloaded = 0;
                this.downloadComplete = false;

                this.audioStream = audioStreamWithFileSystem;
            }
            /// <summary>
            /// Required by DownloadHandler base class.
            /// Not used - the data is being written directly into injected audio buffer
            /// </summary>
            /// <returns></returns>
            protected override byte[] GetData()
            {
                return null;
            }
            /// <summary>
            /// Called once per frame when data has been received from the network.
            /// </summary>
            /// <param name="data"></param>
            /// <param name="dataLength"></param>
            /// <returns></returns>
            protected override bool ReceiveData(byte[] data, int dataLength)
            {
                if (data == null || data.Length < 1 || dataLength < 1)
                {
                    this.downloadComplete = true;
                    return false;
                }

                // take just given length
                var newData = new byte[dataLength];
                Array.Copy(data, 0, newData, 0, dataLength);

                this.audioStream.LOG(LogLevel.DEBUG, "ReceiveData/Writing: {0}", dataLength);

                // write incoming buffer
                this.audioStream.mediaBuffer.Write(newData);
                this.downloaded += (uint)dataLength;

                return true;
            }
            string lastLogMessage; // log spam prevention
            /// <summary>
            /// Called when all data has been received from the server and delivered via ReceiveData.
            /// </summary>
            protected override void CompleteContent()
            {
                this.downloadComplete = true;
            }
            /// <summary>
            /// Called when a Content-Length header is received from the server.
            /// ... can be sent more than once but it looks like whatever came last is good enough question mark
            /// </summary>
            /// <param name="_contentLength"></param>
#if UNITY_2019_1_OR_NEWER
            protected override void ReceiveContentLengthHeader(ulong _contentLength)
            {
                base.ReceiveContentLengthHeader(_contentLength);

                this.audioStream.LOG(LogLevel.DEBUG, "Received Content length: {0}", _contentLength);

                // Content-Lenght should be body/media lenght, but some servers return (probably misconfigured) value - such as http://stream.antenne.de:80/antenne -> _contentLength == 60 bytes
                // since this is values used for stream/file lenght FMOD - correctly - immediately stops - detect this for 'low' values and don't use it

                if (_contentLength < 1024)
                {
                    var msg = string.Format("Will ignore received Content length: {0} / modify handler here if needed", _contentLength);
                    if (this.lastLogMessage != msg)
                    {
                        this.lastLogMessage = msg;
                        this.audioStream.LOG(LogLevel.WARNING, msg);
                    }
                }
                else
                    this.contentLength = (uint)_contentLength;
            }
#else
            protected override void ReceiveContentLength(int _contentLength)
            {
                base.ReceiveContentLength(_contentLength);

                this.audioStream.LOG(LogLevel.DEBUG, "Received Content length: {0}", _contentLength);

                // Content-Lenght should be body/media lenght, but some servers return (probably misconfigured) value - such as http://stream.antenne.de:80/antenne -> _contentLength == 61 bytes
                // since this is values used for stream/file lenght FMOD - correctly - immediately stops - detect this for 'low' values and don't use it

                if (_contentLength < 1024)
                {
                    var msg = string.Format("Will ignore received Content length: {0} / modify handler here if needed", _contentLength);
                    if (this.lastLogMessage != msg)
                    {
                        this.lastLogMessage = msg;
                        this.audioStream.LOG(LogLevel.WARNING, msg);
                    }
                }
                else
                    this.contentLength = (uint)_contentLength;
            }
#endif
        }
        #endregion
        #region custom request headers
        /// <summary>
        /// Custom request headers
        /// </summary>
        readonly Dictionary<string, string> webRequestCustomHeaders = new Dictionary<string, string>();
        public void SetCustomRequestHeader(string key, string value)
        {
            if (this.webRequestCustomHeaders.ContainsKey(key))
                this.webRequestCustomHeaders[key] = value;
            else
                this.webRequestCustomHeaders.Add(key, value);
        }
        public void ClearCustomRequestHeaders()
        {
            this.webRequestCustomHeaders.Clear();
        }
        #endregion
        /*
         * network/file streams
         */
        ByteStreamDownloadHandler downloadHandler = null;
        UnityWebRequest webRequest = null;
        #endregion
    }
}
