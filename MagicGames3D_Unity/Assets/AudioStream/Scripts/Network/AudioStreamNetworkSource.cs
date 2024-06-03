// (c) 2016-2023 Martin Cvengros. All rights reserved. Redistribution of source code without permission not allowed.

using AudioStreamSupport;
using System;
#if UNITY_WSA
using System.Threading.Tasks;
#else
using System.Threading;
# endif
using UnityEngine;

namespace AudioStream
{
    /// <summary>
    /// Base abstract class for network source
    /// Provides audio encoding and queuing, leaving newtork implementation for its descendant 
    /// </summary>
    public abstract partial class AudioStreamNetworkSource : AudioStreamNetwork
    {
        // ========================================================================================================================================
        #region Editor
        [Header("[Setup]")]

        [Tooltip("Turn on/off logging to the Console. Errors are always printed.")]
        public LogLevel logLevel = LogLevel.ERROR;

        [Tooltip("Monitoring volume, doesn't modify sent audio")]
        [Range(0f, 1f)]
        public float monitorVolume = 1f;

        [Tooltip("You can increase the encoder thread priority if needed, but it's usually ok to leave it even below default Normal depending on how network and main thread perform")]
        public System.Threading.ThreadPriority encoderThreadPriority = System.Threading.ThreadPriority.Normal;

        [Tooltip("Encoding method to use (PCM means no encoding)")]
        public CODEC codec = CODEC.OPUS;

        [Tooltip("Should be (almost) always ON, unless you want e.g. to do special configuration before running at runtime")]
        public bool autoStartSource = true;
        
        [Header("[Runtime]")]
        [Tooltip("Source/encoder running flag for UX/runtime")]
        [SerializeField]
        [AudioStreamSupport.ReadOnly]
        bool _sourceRunning = false;
        public bool sourceRunning { get { return this._sourceRunning; } protected set { this._sourceRunning = value; } }
        // TODO: not used just now for simplicity
        // events need Recv/Send notifications, errors on main thread
        //#region Unity events
        //[Header("[Events]")]
        //public EventWithStringStringParameter OnClientConnected;
        //public EventWithStringStringParameter OnClientDisconnected;
        //public EventWithStringStringParameter OnError;
        //#endregion
        [Tooltip("Network sender thread sleep (ms)")]
        [Range(1,50)]
        /// <summary>
        /// ms
        /// </summary>
        public int networkThreadSleep = 5;
        #endregion

        // ========================================================================================================================================
        #region Non editor
        /// <summary>
        /// Encoded packets, read by networking descendant with max capacity
        /// </summary>
        protected ThreadSafeQueue<byte[]> networkQueue = new ThreadSafeQueue<byte[]>(100);
        /// <summary>
        /// server payload
        /// </summary>
        public int serverSamplerate { get; protected set; }
        /// <summary>
        /// channels for OPUS constraint + server payload
        /// </summary>
        public int serverChannels { get; protected set; }
        /// <summary>
        /// payload - server/output device config + 'codec'
        /// </summary>
        protected PAYLOAD serverPayload;
        /// <summary>
        /// Set once first audio packet is processed
        /// </summary>
        public bool encoderRunning
        {
            get;
            private set;
        }
        public int networkQueueSize
        {
            get { return this.networkQueue.Size(); }
        }
        public int audioSamplesSize
        {
            get { return this.audioSamples.Available(); }
        }
#if UNITY_WSA
        Task
#else
        Thread
#endif
        encodeThread;
        protected
#if UNITY_WSA
        Task
#else
        Thread
#endif
        networkThread;
        protected bool networkThreadRunning = false;
        #endregion
        // ========================================================================================================================================
        #region Unity lifecycle
        protected virtual void Awake()
        {
            this.gameObjectName = this.gameObject.name;

            var ac = AudioSettings.GetConfiguration();
            this.serverSamplerate = ac.sampleRate;
            this.serverChannels = UnityAudio.ChannelsFromUnityDefaultSpeakerMode();
        }

        protected virtual void Start()
        {
            if (this.autoStartSource)
                this.StartSource();
        }

        protected virtual void Update()
        {
            if (this.opusEncoder != null)
            {
                this.UpdateCodecBitrate(this.bitrate);
                this.UpdateCodecComplexity(this.complexity);
                this.UpdateCodecVBRMode(this.rate);
            }

#if !UNITY_WSA
            if (this.encodeThread != null)
                this.encodeThread.Priority = this.encoderThreadPriority;
#endif
        }
#if ENABLE_IL2CPP
        [Unity.IL2CPP.CompilerServices.Il2CppSetOption(Unity.IL2CPP.CompilerServices.Option.NullChecks, false)]
        [Unity.IL2CPP.CompilerServices.Il2CppSetOption(Unity.IL2CPP.CompilerServices.Option.ArrayBoundsChecks, false)]
        [Unity.IL2CPP.CompilerServices.Il2CppSetOption(Unity.IL2CPP.CompilerServices.Option.DivideByZeroChecks, false)]
#endif
        void OnAudioFilterRead(float[] data, int channels)
        {
            this.audioSamplesChSize = data.Length;

            if (!this.sourceRunning)
                return;

            lock (this.audioSamplesLock)
                this.audioSamples.Write(data);

            for (var i = 0; i < this.audioSamplesChSize; ++i)
                data[i] *= this.monitorVolume;
        }

        protected virtual void OnDestroy()
        {
            this.StopSource();
        }
        #endregion
        // ========================================================================================================================================
        #region Network Source
        protected abstract bool Listen();
        protected abstract void StopListening();

        readonly object audioSamplesLock = new object();
        /// <summary>
        /// Samples to be encoded queue
        /// </summary>
        BasicBufferFloat audioSamples = new BasicBufferFloat(10000); // should be enough (tm) for audio to be picked up by decoder
        /// <summary>
        /// OAFR / PCM samples - audio packet size for PCM = 
        /// is == dspBufferSize * channels
        /// also approximation for UI
        /// </summary>
        public int audioSamplesChSize { get; protected set; }
        public void StartSource()
        {
            if (this.sourceRunning)
            {
                LOG(LogLevel.ERROR, "Source is already running");
                return;
            }

            this.sourceRunning = true;

            // setup server payload
            this.serverPayload = new PAYLOAD();
            this.serverPayload.codec = this.codec;
            this.serverPayload.samplerate = (UInt16)this.serverSamplerate;
            this.serverPayload.channels = (UInt16)this.serverChannels;

            // listen + source loop
            if (!this.Listen())
            {
                LOG(LogLevel.ERROR, "Can't start source");
                this.sourceRunning = false;
                return;
            }

            switch (this.codec)
            {
                case CODEC.OPUS:
                    this.sourceRunning = this.StartEncoder_OPUS();
                    break;

                case CODEC.PCM:
                    this.sourceRunning = this.StartEncoder_PCM();
                    break;
            }
        }
        public void StopSource()
        {
            this.StopListening();

            if (this.encodeThread != null)
            {
                // If the thread that calls Abort holds a lock that the aborted thread requires, a deadlock can occur.
                this.encoderRunning = false;
#if !UNITY_WSA
                this.encodeThread.Join();
#endif
                this.encodeThread = null;
            }

            this.sourceRunning = false;
        }
        #endregion
        // ========================================================================================================================================
        #region Support
        /// <summary>
        /// GO name to be accessible from all the threads if needed
        /// </summary>
        protected string gameObjectName = string.Empty;
        protected void LOG(LogLevel requestedLogLevel, string format, params object[] args)
        {
            Log.LOG(requestedLogLevel, this.logLevel, this.gameObjectName, format, args);
        }
        #endregion
    }
}