// (c) 2016-2023 Martin Cvengros. All rights reserved. Redistribution of source code without permission not allowed.

using AudioStreamSupport;
using NetMQ;
using NetMQ.Sockets;
#if UNITY_WSA
using System.Threading.Tasks;
#else
using System.Threading;
# endif
using UnityEngine;

namespace AudioStream
{
    /// <summary>
    /// Implements NetMQ transport for AudioStreamNetworkClient
    /// </summary>
    public class AudioStreamNetMQClient : AudioStreamNetworkClient
    {
        // ========================================================================================================================================
        #region Editor
        [Header("[Network client setup]")]
        [Tooltip("IP address of the AudioStreamNetworkSource to connect to")]
        public string serverIP = "0.0.0.0";
        [Tooltip("Port to connect to")]
        public int serverTransferPort = AudioStreamNetMQSource.listenPortDefault;
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
        #endregion

        // ========================================================================================================================================
        #region Client
        bool clientLoopRunning = false;
        protected override bool Connect()
        {
            // networkQueue with max capacity
            this.networkQueue = new ThreadSafeQueue<byte[]>(100);

            // NetMQ for 3.5 runtime has problems connecting to // and non reachable IPs resulting in deadlock
            // TODO: verify with version for 4.x once 3.5. is no longer being used 
            if (string.IsNullOrWhiteSpace(this.serverIP)
                || this.serverIP == "0.0.0.0")
            {
                this.LOG(LogLevel.ERROR, "Won't be connecting to: {0}", this.serverIP);
                return false;
            }

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
            AudioStreamNetMQClient.NetMQConfig_Use(this);

            // does not compute BufferPool.SetBufferManagerBufferPool(1024 * 1024, 1024);

            // There are 10,000 ticks in a millisecond
            // timeout on socket read 10 ms
            System.TimeSpan msgTimeout = new System.TimeSpan(100000);

            // wrap NetMQ access to catch errors + cleanup
            try
            {
                this.LOG(LogLevel.INFO, "Connecting to {0}:{1}", this.serverIP, this.serverTransferPort);

                using (var subSocket = new SubscriberSocket(string.Format(">tcp://{0}:{1}", this.serverIP, this.serverTransferPort)))
                {
                    // subscribe to 'any' topic
                    subSocket.SubscribeToAnyTopic();

                    var msg = new Msg();
                    while (this.clientLoopRunning)
                    {
                        // try to pull a packet
                        msg.InitEmpty();

                        if (subSocket.TryReceive(ref msg, msgTimeout))
                        {
                            var packet = msg.Data;
                            if (packet != null)
                            {
                                this.LOG(AudioStreamSupport.LogLevel.DEBUG, "Recv packet: {0}", packet.Length);

                                var barr = new byte[msg.Size];
                                System.Array.Copy(packet, barr, barr.Length);

                                this.networkQueue.Enqueue(barr);
                            }
                            else
                            {
                                this.LOG(LogLevel.WARNING, "Received malformed packet {0}", msg);
                            }
                        }
                        else
                        {
                            // spamsalot
                            // Debug.LogFormat("Got nothing");
                        }
#if UNITY_WSA
                        this.clientThread.Wait(this.networkThreadSleep);
#else
                        Thread.Sleep(this.networkThreadSleep);
#endif
                    }

                    if (msg.IsInitialised)
                        msg.Close();

                    subSocket.Disconnect(string.Format("tcp://{0}:{1}", this.serverIP, this.serverTransferPort));
                }
            }
            catch (System.Exception ex)
            {
                this.LOG(AudioStreamSupport.LogLevel.ERROR, "{0} [{1}]", ex.Message, ex.InnerException != null ? ex.InnerException.Message : "");
            }
            finally
            {
                AudioStreamNetMQClient.NetMQConfig_Release(this);
            }
        }

        #endregion

        // ========================================================================================================================================
        #region NetMQConfig
        static int netMQConfig_refc = 0;
        static void NetMQConfig_Use(AudioStreamNetMQClient instance)
        {
            AudioStreamNetMQClient.netMQConfig_refc++;
            instance.LOG(AudioStreamSupport.LogLevel.DEBUG, "NetMQConfig_Use [{0}]", AudioStreamNetMQClient.netMQConfig_refc);

            // hint on 1st usage
            if (AudioStreamNetMQClient.netMQConfig_refc == 1)
            {
                AsyncIO.ForceDotNet.Force();
                NetMQConfig.Cleanup();

                instance.LOG(AudioStreamSupport.LogLevel.INFO, "AsyncIO.ForceDotNet.Force");
            }
        }
        static void NetMQConfig_Release(AudioStreamNetMQClient instance)
        {
            AudioStreamNetMQClient.netMQConfig_refc--;
            instance.LOG(AudioStreamSupport.LogLevel.DEBUG, "NetMQConfig_Release [{0}]", AudioStreamNetMQClient.netMQConfig_refc);

            // release on last usage
            if (AudioStreamNetMQClient.netMQConfig_refc <= 0)
            {
                AudioStreamNetMQClient.netMQConfig_refc = 0;

                NetMQConfig.Cleanup(false);
                instance.LOG(AudioStreamSupport.LogLevel.INFO, "NetMQConfig.Cleanup");
            }
        }
        #endregion
    }
}