// (c) 2016-2023 Martin Cvengros. All rights reserved. Redistribution of source code without permission not allowed.

using NetMQ;
using NetMQ.Sockets;
using System.Linq;
#if UNITY_WSA
using System.Threading.Tasks;
#else
using System.Threading;
# endif
using UnityEngine;

namespace AudioStream
{
    /// <summary>
    /// Implements NetMQ transport for AudioStreamNetworkSource
    /// </summary>
    public class AudioStreamNetMQSource : AudioStreamNetworkSource
    {
        // ========================================================================================================================================
        #region Editor
        [Header("[Network source setup]")]
        /// <summary>
        /// Host IP as reported by .NET
        /// </summary>
        [Tooltip("Host IPv4 address as reported by .NET System.Net.Dns entry, determined at runtime")]
        [SerializeField]
        [AudioStreamSupport.ReadOnly]
        string _listenIP;
        public string listenIP { get { return this._listenIP; } protected set { this._listenIP = value; } }
        [Tooltip("Port to listen to for client connections on the above address")]
        public int listenPort = AudioStreamNetMQSource.listenPortDefault;
        [Tooltip("Maximum number of client connected simultaneously")]
        public int maxConnections = 10;
        #endregion

        // ========================================================================================================================================
        #region Non editor
        /// <summary>
        /// (default listen port)
        /// </summary>
        public const int listenPortDefault = 33000;
        #endregion
        // ========================================================================================================================================
        #region Source
        protected override bool Listen()
        {
            // start PublisherSocket loop on local IP

            if (System.Net.NetworkInformation.NetworkInterface.GetIsNetworkAvailable())
            {
                var host = System.Net.Dns.GetHostEntry(System.Net.Dns.GetHostName());
                this.listenIP = host.AddressList.FirstOrDefault(f => f.AddressFamily == System.Net.Sockets.AddressFamily.InterNetwork).ToString();

                this.LOG(AudioStreamSupport.LogLevel.DEBUG, "Host IP(v4): {0}", this.listenIP);
            }
            else
            {
                this.LOG(AudioStreamSupport.LogLevel.ERROR, "Network interface not available - will not run");
                return false;
            }

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
                this.networkThread.Wait(this.networkThreadSleep);
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
            AudioStreamNetMQSource.NetMQConfig_Use(this);

            // does not compute BufferPool.SetBufferManagerBufferPool(1024 * 1024, 1024);

            // There are 10,000 ticks in a millisecond
            // timeout on socket send 1 ms
            System.TimeSpan msgTimeout = new System.TimeSpan(10000);

            // wrap NetMQ access to catch errors + cleanup
            try
            {
                var binds = string.Format("@tcp://*:{0}", this.listenPort);
                using (var pubSocket = new PublisherSocket(binds))
                {
                    this.LOG(AudioStreamSupport.LogLevel.INFO, "Accepting connections on {0}:{1}", this.listenIP, this.listenPort);

                    var msg = new Msg();
                    while (this.networkThreadRunning)
                    {
                        // send output to all subscribers
                        byte[] packet = this.networkQueue.Dequeue();
                        if (packet != null)
                        {
                            msg.InitPool(packet.Length);
                            msg.Put(packet, 0, packet.Length);

                            if (pubSocket.TrySend(ref msg, msgTimeout, false))
                            {
                                this.LOG(AudioStreamSupport.LogLevel.DEBUG, "Sent packet: {0}", packet.Length);
                            }
                            else
                            {
                                this.LOG(AudioStreamSupport.LogLevel.WARNING, "Couldn't send packet: {0}", packet.Length);
                            }
                        }
#if UNITY_WSA
                        this.networkThread.Wait(this.networkThreadSleep);
#else
                        Thread.Sleep(this.networkThreadSleep);
#endif
                    }

                    if (msg.IsInitialised)
                        msg.Close();
                }
            }
            catch (System.Exception ex)
            {
                this.LOG(AudioStreamSupport.LogLevel.ERROR, "{0}\r\n{1} [{2}]", ex.Message, ex.StackTrace, ex.InnerException != null ? ex.InnerException.Message : "");
            }
            finally
            {
                AudioStreamNetMQSource.NetMQConfig_Release(this);
            }
        }
        #endregion

        // ========================================================================================================================================
        #region NetMQConfig
        static int netMQConfig_refc = 0;
        static void NetMQConfig_Use(AudioStreamNetMQSource instance)
        {
            AudioStreamNetMQSource.netMQConfig_refc++;
            instance.LOG(AudioStreamSupport.LogLevel.DEBUG, "NetMQConfig_Use [{0}]", AudioStreamNetMQSource.netMQConfig_refc);

            // hint on 1st usage
            if (AudioStreamNetMQSource.netMQConfig_refc == 1)
            {
                AsyncIO.ForceDotNet.Force();
                NetMQConfig.Cleanup();
                NetMQConfig.MaxSockets = instance.maxConnections;
                NetMQConfig.ThreadPoolSize = instance.maxConnections;

                instance.LOG(AudioStreamSupport.LogLevel.INFO, "AsyncIO.ForceDotNet.Force");
            }
        }
        static void NetMQConfig_Release(AudioStreamNetMQSource instance)
        {
            AudioStreamNetMQSource.netMQConfig_refc--;
            instance.LOG(AudioStreamSupport.LogLevel.DEBUG, "NetMQConfig_Release [{0}]", AudioStreamNetMQSource.netMQConfig_refc);

            // release on last usage
            if (AudioStreamNetMQSource.netMQConfig_refc <= 0)
            {
                AudioStreamNetMQSource.netMQConfig_refc = 0;

                NetMQConfig.Cleanup(false);
                instance.LOG(AudioStreamSupport.LogLevel.INFO, "NetMQConfig.Cleanup");
            }
        }
        #endregion
    }
}