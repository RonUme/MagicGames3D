// (c) 2016-2023 Martin Cvengros. All rights reserved. Redistribution of source code without permission not allowed.

using OscCore;
using System.Collections.Generic;
#if UNITY_WSA
using System.Threading.Tasks;
#else
using System.Threading;
# endif
using UnityEngine;

namespace AudioStream
{
    /// <summary>
    /// 
    /// </summary>
    public class AudioStreamOscSource : AudioStreamNetworkSource
    {
        // ========================================================================================================================================
        #region Editor
        [Header("[OSC source setup]")]
        [Tooltip("Destination/broadcast IP\r\ndirect/known client IP works better latency wise\r\ndefault is local boradcast '255.255.255.255'")]
        public string remoteIP = "255.255.255.255";
        [Tooltip("List of destination/receiver ports to connect/send to")]
        public int[] remotePorts = new int[1] { AudioStreamOscSource.remotePortDefault };
        [Tooltip("OSC address")]
        public string oscAddress = AudioStreamOscSource.oscAddressDefault;
        #endregion
        // ========================================================================================================================================
        #region Non editor
        /// <summary>
        /// (default connect port)
        /// </summary>
        public const int remotePortDefault = 7000;
        public const string oscAddressDefault = "/as/audio";
        List<OscClient> oscClients = new List<OscClient>();
        #endregion
        // ========================================================================================================================================
        #region Source
        protected override bool Listen()
        {
            // compute ~max. size/upper bound (when using PCM codec) of one sent packet for OSC writer buffer
            // == PCM frame size + payload size + OSC data (address + tags)
            var samples = AudioSettings.GetConfiguration().dspBufferSize;
            var channels = AudioStreamSupport.UnityAudio.ChannelsFromUnityDefaultSpeakerMode();
            var pcmWidth = samples * channels * sizeof(float);
            var payloadSize = System.Runtime.InteropServices.Marshal.SizeOf<PAYLOAD>(this.serverPayload);
            var addrSize = this.oscAddress.ToCharArray().Length;
            var reserve = 20; // tags should be 4 bytes, some reserve..

            var maxWrSize = pcmWidth
                + payloadSize
                + addrSize
                + reserve
                ;
            // Debug.LogFormat(@"AB samples: {0}, channels: {1}, pcmWidth: {2}, payloadSize: {3}, addr l: {4}, reserve: {5}, max b size: {6}", samples, channels, pcmWidth, payloadSize, this.oscAddress.Length, reserve, maxWrSize);

            foreach (var remotePort in this.remotePorts)
            {
                try
                {
                    var client = new OscClient(this.remoteIP, remotePort, maxWrSize);
                    this.LOG(AudioStreamSupport.LogLevel.INFO, "Created OSC client {0}:{1}, wbuffer:{2} b", this.remoteIP, remotePort, maxWrSize);

                    this.oscClients.Add(client);
                }
                catch (System.Exception ex)
                {
                    this.LOG(AudioStreamSupport.LogLevel.ERROR, "Can't create OSC client for remote {0}:{1}\r\n{2}\r\n{3} [{4}]", this.remoteIP, remotePort, ex.Message, ex.StackTrace, ex.InnerException != null ? ex.InnerException.Message : "");
                }

                this.LOG(AudioStreamSupport.LogLevel.DEBUG, "New OSC client on remote {0}:{1}", this.remoteIP, remotePort);
            }

            if (this.oscClients.Count < 1)
                return false;

            // start listen thread
            this.networkThread =
#if UNITY_WSA
                new Task(new System.Action(this.SourceLoop), TaskCreationOptions.LongRunning | TaskCreationOptions.RunContinuationsAsynchronously);
#else
                new Thread(new ThreadStart(this.SourceLoop));
            this.networkThread.Priority = System.Threading.ThreadPriority.Normal;
#endif
            this.networkThreadRunning = true;
            this.networkThread.Start();

            return true;
        }
        protected override void StopListening()
        {
            if (this.networkThread != null)
            {
                this.networkThreadRunning = false;
#if UNITY_WSA
                this.networkThread.Wait(networkThreadSleep);
#else
                Thread.Sleep(this.networkThreadSleep);
                this.networkThread.Join();
#endif
                this.networkThread = null;
            }
        }

#if ENABLE_IL2CPP
        [Unity.IL2CPP.CompilerServices.Il2CppSetOption(Unity.IL2CPP.CompilerServices.Option.NullChecks, false)]
        [Unity.IL2CPP.CompilerServices.Il2CppSetOption(Unity.IL2CPP.CompilerServices.Option.ArrayBoundsChecks, false)]
        [Unity.IL2CPP.CompilerServices.Il2CppSetOption(Unity.IL2CPP.CompilerServices.Option.DivideByZeroChecks, false)]
#endif
        void SourceLoop()
        {
            // There are 10,000 ticks in a millisecond
            // timeout on socket send 1 ms
            System.TimeSpan msgTimeout = new System.TimeSpan(10000);

            var ports = "";
            foreach (var port in this.remotePorts)
                ports += string.Format(":{0}", port);

            this.LOG(AudioStreamSupport.LogLevel.INFO, "Sending to remote IP: {0}{1}", this.remoteIP, ports);
            
            // wrap OSC access to catch errors + cleanup
            try
            {
                while (this.networkThreadRunning)
                {
                    // send OCS message
                    byte[] packet = this.networkQueue.Dequeue();
                    if (packet != null)
                    {
                        foreach(var client in this.oscClients)
                            client.Send(this.oscAddress, packet, packet.Length);
                    }
#if UNITY_WSA
                    this.networkThread.Wait(this.networkThreadSleep);
#else
                    Thread.Sleep(this.networkThreadSleep);
#endif
                }
            }
            catch (System.Exception ex)
            {
                this.LOG(AudioStreamSupport.LogLevel.ERROR, "{0}\r\n{1} [{2}]", ex.Message, ex.StackTrace, ex.InnerException != null ? ex.InnerException.Message : "");
            }
            finally
            {
                // shut down OSC
                foreach(var client in this.oscClients)
                    client.Writer.Dispose();

                this.oscClients.Clear();
            }
        }
        #endregion
    }
}