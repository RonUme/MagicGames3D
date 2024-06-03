// (c) 2016-2023 Martin Cvengros. All rights reserved. Redistribution of source code without permission not allowed.

using AudioStreamSupport;
using System;
using System.Collections;
#if UNITY_WSA
using System.Threading.Tasks;
#else
using System.Threading;
# endif
using UnityEngine;

namespace AudioStream
{
    /// <summary>
    /// Base abstract class for network client
    /// Provides audio decoding and queuing, leaving newtork implementation for its descendant 
    /// </summary>
    [RequireComponent(typeof(AudioSource))]
    public abstract partial class AudioStreamNetworkClient : AudioStreamNetwork
    {
        // ========================================================================================================================================
        #region Editor
        [Header("[Setup]")]

        [Tooltip("Turn on/off logging to the Console. Errors are always printed.")]
        public LogLevel logLevel = LogLevel.ERROR;

        [Tooltip("Should be (almost) always ON, unless you want to do e.g. special configuration before running at runtime")]
        public bool autoStartClient = true;

        [Header("[Decoder]")]
        [Tooltip("You can increase the encoder thread priority if needed, but it's usually ok to leave it even below default Normal depending on how network and main thread perform")]
        public System.Threading.ThreadPriority decoderThreadPriority = System.Threading.ThreadPriority.Normal;
        /// <summary>
        /// Client/decoder running flag for UX/runtime
        /// </summary>
        public bool clientRunning { get; protected set ; }
        [Header("[Runtime]")]
        [SerializeField]
        [ReadOnly]
        [Tooltip("Determined from AudioSource spatialBlend when starting playback to use lower latency AudioClip if possible")]
        bool audioSourceIs2D = true;
        // TODO: not used just now for simplicity
        // events need Recv/Send notifications, errors on main thread
        //#region Unity events
        //[Header("[Events]")]
        //public EventWithStringParameter OnConnected;
        //public EventWithStringParameter OnDisonnected;
        //public EventWithStringStringParameter OnError;
        [Tooltip("Network receiver thread sleep (ms)")]
        [Range(1, 50)]
        /// <summary>
        /// ms
        /// </summary>
        public int networkThreadSleep = 5;
        //#endregion
        #endregion

        // ========================================================================================================================================
        #region Non editor
        /// <summary>
        /// received compressed audio packet queue
        /// </summary>
        protected ThreadSafeQueue<byte[]> networkQueue { get; set; }
        /// <summary>
        /// Set once first audio packet is processed
        /// </summary>
        public bool decoderRunning
        {
            get;
            private set;
        }
        public int networkQueueSize
        {
            get
            {
                if (this.networkQueue != null)
                    return this.networkQueue.Size();
                else
                    return 0;
            }
        }
#if UNITY_WSA
        Task
#else
        Thread
#endif
        decodeThread = null;
        #endregion
        // ========================================================================================================================================
        #region Unity lifecycle
        AudioSource @as;
        int clientSamplerate;
        int clientChannels;
        public int dspBufferSize { get; private set; }
        protected virtual void Awake()
        {
            this.gameObjectName = this.gameObject.name;

            this.@as = this.GetComponent<AudioSource>();

            var ac = AudioSettings.GetConfiguration();
            this.clientSamplerate = AudioSettings.outputSampleRate;
            this.clientChannels = UnityAudio.ChannelsFromUnityDefaultSpeakerMode();

            this.dspBufferSize = ac.dspBufferSize * clientChannels;

            // we want to play so I guess want the output to be heard, on iOS too:
            // Since 2017.1 there is a setting 'Force iOS Speakers when Recording' for this workaround needed in previous versions
#if !UNITY_2017_1_OR_NEWER
            if (Application.platform == RuntimePlatform.IPhonePlayer)
            {
                TimeLog.LOG(LogLevel.INFO, LogLevel.INFO, this.gameObject.name, null, "Setting playback output to speaker...");
                iOSSpeaker.RouteForPlayback();
            }
#endif
        }

        protected virtual void Start()
        {
            if (this.autoStartClient)
                this.StartClient();
        }

        protected virtual void Update()
        {
#if !UNITY_WSA
            if (this.decodeThread != null)
                this.decodeThread.Priority = this.decoderThreadPriority;
#endif
        }
        public bool capturedAudioFrame { get; private set; }
        public int capturedAudioSamples { get { return this.audioSamples.Available(); } }
        /// <summary>
        /// Will be set as clip's callback only if AudioSource is spatialized
        /// </summary>
        /// <param name="data"></param>
#if ENABLE_IL2CPP
        [Unity.IL2CPP.CompilerServices.Il2CppSetOption(Unity.IL2CPP.CompilerServices.Option.NullChecks, false)]
        [Unity.IL2CPP.CompilerServices.Il2CppSetOption(Unity.IL2CPP.CompilerServices.Option.ArrayBoundsChecks, false)]
        [Unity.IL2CPP.CompilerServices.Il2CppSetOption(Unity.IL2CPP.CompilerServices.Option.DivideByZeroChecks, false)]
#endif
        void PCMReaderCallback(float[] data)
        {
            var dlength = data.Length;
            Array.Clear(data, 0, dlength);

            if (!this.clientRunning)
                return;

            // should't be needed since CB is not installed for !spatialized source, just to be sure..
            if (this.audioSourceIs2D)
                return;

            if (this.audioSamples.Available() >= dlength)
            {
                float[] floats;
                lock (this.audioSamples)
                    floats = this.audioSamples.Read(dlength);

                Array.Copy(floats, data, floats.Length);
                this.capturedAudioFrame = true;
            }
            else
            {
                // not enough frames arrived
                this.capturedAudioFrame = false;
            }
        }

        protected virtual void OnDestroy()
        {
            this.StopClient();
        }

        #endregion
        // ========================================================================================================================================
        #region Client
        /// <summary>
        /// Decoded samples @ either original server rate, or manually resampled if needed
        /// for 2D vs AudioClip created with server rate
        /// </summary>
        BasicBufferFloat audioSamples = new BasicBufferFloat(100000);
        protected abstract bool Connect();
        protected abstract void Disconnect();
        public PAYLOAD serverPayload;
        public void StartClient()
        {
            if (this.clientRunning)
            {
                LOG(LogLevel.ERROR, "Client is already running");
                return;
            }

            this.clientRunning = true;

            // connect + client loop
            if (!this.Connect())
            {
                this.clientRunning = false;
                return;
            }

            //  wait for payload && start decoder
            StartCoroutine(this.StartClient_CR());
        }

        IEnumerator StartClient_CR()
        {
            this.serverPayload = default(PAYLOAD);
            var payloadSize = System.Runtime.InteropServices.Marshal.SizeOf(this.serverPayload);
            
            // some attempts
            var attempts = 0;
            while (this.serverPayload.samplerate == 0
                || this.serverPayload.channels == 0
                )
            {
                LOG(LogLevel.INFO, "Waiting for payload..");

                // get server config from packet payload
                var networkPacket = this.networkQueue.Dequeue();

                // at least the payload (config) has to be present
                if (networkPacket != null
                    && networkPacket.Length >= payloadSize
                    )
                {
                    var bytes = new byte[payloadSize];
                    Array.Copy(networkPacket, 0, bytes, 0, payloadSize);
                    this.serverPayload = this.BytesToPayload(bytes);

                    continue;
                }
                
                if (++attempts > 600)
                {
                    this.LOG(LogLevel.ERROR, "Can't connect");
                    this.StopClient();
                    yield break;
                }

                yield return null;
            }

            this.capturedAudioFrame = false;
            /*
             * setup AudioSource's source of samples 
             */
            var samples = this.serverPayload.samplerate * 5;
            this.audioSamples = new BasicBufferFloat(samples);

            // 2D => OnAudioFilterRead, !2D => PCMReaderCallback
            // ( this.@as.spatialize == [False] - only relevant w/ plugins (?) )
            // , 2D will have to be resampled from original rate 'manually', 3D will use AudioClip created w/ original rate
            this.audioSourceIs2D = this.@as.spatialBlend == 0;
            if (!this.audioSourceIs2D)
                this.@as.clip = AudioClip.Create(this.gameObject.name, samples, this.serverPayload.channels, this.serverPayload.samplerate, true, this.PCMReaderCallback);

            this.@as.loop = true;
            this.@as.Play();

            switch (this.serverPayload.codec)
            {
                case CODEC.OPUS:
                    this.clientRunning = this.StartDecoder_OPUS();
                    LOG(LogLevel.INFO, "Starting {0} decoder samplerate: {1}, channels: {2}, server rate: {3}, channels: {4}", this.serverPayload.codec, AudioStreamNetworkSource.opusSampleRate, AudioStreamNetworkSource.opusChannels, this.serverPayload.samplerate, this.serverPayload.channels);
                    break;

                case CODEC.PCM:
                    this.clientRunning = this.StartDecoder_PCM();
                    LOG(LogLevel.INFO, "Starting {0} decoder server rate: {1}, channels: {2}", this.serverPayload.codec, this.serverPayload.samplerate, this.serverPayload.channels);
                    break;
            }
        }

#if ENABLE_IL2CPP
        [Unity.IL2CPP.CompilerServices.Il2CppSetOption(Unity.IL2CPP.CompilerServices.Option.NullChecks, false)]
        [Unity.IL2CPP.CompilerServices.Il2CppSetOption(Unity.IL2CPP.CompilerServices.Option.ArrayBoundsChecks, false)]
        [Unity.IL2CPP.CompilerServices.Il2CppSetOption(Unity.IL2CPP.CompilerServices.Option.DivideByZeroChecks, false)]
#endif
        void OnAudioFilterRead(float[] data, int channels)
        {
            // don't process audio here if the AudioSource is spatialized
            if (!this.audioSourceIs2D)
                return;

            if (!this.clientRunning)
                return;

            var dlength = data.Length;

            if (this.audioSamples.Available() >= dlength)
            {
                float[] floats;
                lock (this.audioSamples)
                    floats = this.audioSamples.Read(dlength);

                Array.Copy(floats, data, floats.Length);
                this.capturedAudioFrame = true;
            }
            else
            {
                // not enough frames arrived
                this.capturedAudioFrame = false;
            }
        }

        public void StopClient()
        {
            StopAllCoroutines(); // StartDecoderCR

            this.Disconnect();

            if (this.@as != null)
                @as.Stop();

            this.capturedAudioFrame = false;

            if (this.decodeThread != null)
            {
                // If the thread that calls Abort holds a lock that the aborted thread requires, a deadlock can occur.
                this.decoderRunning = false;
#if !UNITY_WSA
                this.decodeThread.Join();
#endif
                this.decodeThread = null;
            }

            this.opusDecoder = null;
            this.clientRunning = false;
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