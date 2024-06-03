// (c) 2016-2023 Martin Cvengros. All rights reserved. Redistribution of source code without permission not allowed.

using AudioStreamSupport;
using NetMQ;
using NetMQ.Sockets;
using OscCore;
#if UNITY_WSA
using System.Threading.Tasks;
#else
using System.Threading;
# endif
using UnityEngine;

namespace AudioStream
{
    public class AudioStreamOscClient : AudioStreamNetworkClient
    {
        // ========================================================================================================================================
        #region Editor
        [Header("[OSC receiver setup]")]
        [Tooltip("Local connection port to receive incoming data on")]
        public int connectPort = AudioStreamOscSource.remotePortDefault;
        [ReadOnly]
        [Tooltip("(Local) Receiving IP - for informational purposes only")]
        public string localIP; // IPv4
        [Tooltip("OSC address")]
        public string oscAddress = AudioStreamOscSource.oscAddressDefault;
        #endregion
        // ========================================================================================================================================
        #region Non editor
#if UNITY_WSA
        Task
#else
        Thread
#endif
        clientThread;
        /// <summary>
        /// Not entirely true it means just that socket was created, not necessarily that it connected to host successfully
        /// TODO:
        /// </summary>
        public bool isConnected { get { return this.decoderRunning; } }
        OscServer oscServer;
        #endregion
        // ========================================================================================================================================
        #region Unity lifecycle
        protected override void Awake()
        {
            base.Awake();

            this.localIP = UWR.GetLocalIpAddress();
        }
        #endregion
        // ========================================================================================================================================
        #region Client
        bool clientLoopRunning = false;
        protected override bool Connect()
        {
            // networkQueue with max capacity
            this.networkQueue = new ThreadSafeQueue<byte[]>(100);
            try
            {
                // compute ~max. size/upper bound (when using PCM codec) of one sent packet for OSC writer buffer
                // == PCM frame size + payload size + OSC data (address + tags)
                var samples = AudioSettings.GetConfiguration().dspBufferSize;
                var channels = AudioStreamSupport.UnityAudio.ChannelsFromUnityDefaultSpeakerMode();
                var pcmWidth = samples * channels * sizeof(float);
                var payloadSize = System.Runtime.InteropServices.Marshal.SizeOf<PAYLOAD>(this.serverPayload);
                var addrSize = this.oscAddress.ToCharArray().Length;
                var reserve = 20; // tags should be 4 bytes, some reserve..

                var maxRSize = pcmWidth
                    + payloadSize
                    + addrSize
                    + reserve
                    ;
                // Debug.LogFormat(@"AB samples: {0}, channels: {1}, pcmWidth: {2}, payloadSize: {3}, addr l: {4}, reserve: {5}, max b size: {6}", samples, channels, pcmWidth, payloadSize, this.oscAddress.Length, reserve, maxRSize);

                this.oscServer = OscServer.GetOrCreate(this.connectPort, maxRSize);
                this.LOG(AudioStreamSupport.LogLevel.INFO, "Created OSC server on :{0}, rbuffer:{1} b", this.connectPort, maxRSize);
            }
            catch (System.Exception ex)
            {
                this.LOG(AudioStreamSupport.LogLevel.ERROR, "Can't receive on :{0}\r\n{1}\r\n{2} [{3}]", this.connectPort, ex.Message, ex.StackTrace, ex.InnerException != null ? ex.InnerException.Message : "");
                return false;
            }

            this.oscServer.TryAddMethod(this.oscAddress, this.ReadValues);

            // start client thread
            this.clientThread =
#if UNITY_WSA
                new Task(new System.Action(this.ClientLoop), TaskCreationOptions.LongRunning | TaskCreationOptions.RunContinuationsAsynchronously);
#else
                new Thread(new ThreadStart(this.ClientLoop));
            this.clientThread.Priority = System.Threading.ThreadPriority.Normal;
#endif
            this.clientLoopRunning = true;
            this.clientThread.Start();

            return true;
        }

        protected override void Disconnect()
        {
            if (this.clientThread != null)
            {
                this.clientLoopRunning = false;
#if !UNITY_WSA
                this.clientThread.Join();
#endif
                this.clientThread = null;
            }
        }
#if ENABLE_IL2CPP
        [Unity.IL2CPP.CompilerServices.Il2CppSetOption(Unity.IL2CPP.CompilerServices.Option.NullChecks, false)]
        [Unity.IL2CPP.CompilerServices.Il2CppSetOption(Unity.IL2CPP.CompilerServices.Option.ArrayBoundsChecks, false)]
        [Unity.IL2CPP.CompilerServices.Il2CppSetOption(Unity.IL2CPP.CompilerServices.Option.DivideByZeroChecks, false)]
#endif
        void ClientLoop()
        {
            // There are 10,000 ticks in a millisecond
            // timeout on socket read 10 ms
            System.TimeSpan msgTimeout = new System.TimeSpan(100000);

            // wrap OSC access to catch errors + cleanup
            try
            {
                this.LOG(LogLevel.INFO, "Receiving on {0}:{1}", this.localIP, this.connectPort);

                while (this.clientLoopRunning)
                {
                    this.oscServer.Update();
#if UNITY_WSA
                    this.clientThread.Wait(this.networkThreadSleep);
#else
                    Thread.Sleep(this.networkThreadSleep);
#endif
                }
            }
            catch (System.Exception ex)
            {
                this.LOG(AudioStreamSupport.LogLevel.ERROR, "{0} [{1}]", ex.Message, ex.InnerException != null ? ex.InnerException.Message : "");
            }
            finally
            {
                // shut down OSC
                this.oscServer.Dispose();
                this.oscServer = null;
            }
        }
        /// <summary>
        /// should be resized as needed when read into
        /// </summary>
        byte[] packet = new byte[0];
#if ENABLE_IL2CPP
        [Unity.IL2CPP.CompilerServices.Il2CppSetOption(Unity.IL2CPP.CompilerServices.Option.NullChecks, false)]
        [Unity.IL2CPP.CompilerServices.Il2CppSetOption(Unity.IL2CPP.CompilerServices.Option.ArrayBoundsChecks, false)]
        [Unity.IL2CPP.CompilerServices.Il2CppSetOption(Unity.IL2CPP.CompilerServices.Option.DivideByZeroChecks, false)]
#endif
        void ReadValues(OscMessageValues values)
        {
            values.ForEachElement((i, type) =>
            {
                if (type == TypeTag.Blob)
                {
                    var sz = values.ReadBlobElement(i, ref this.packet);

                    var barr = new byte[sz];
                    System.Array.Copy(packet, barr, barr.Length);
                    this.networkQueue.Enqueue(barr);
                }
            });
        }
        #endregion
    }
}