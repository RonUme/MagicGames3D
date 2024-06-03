// (c) 2016-2023 Martin Cvengros. All rights reserved. Redistribution of source code without permission not allowed.

using AudioStreamSupport;
using Concentus.Enums;
using Concentus.Structs;
using System;
#if UNITY_WSA
using System.Threading.Tasks;
#else
using System.Threading;
# endif
using UnityEngine;

namespace AudioStream
{
    public abstract partial class AudioStreamNetworkSource : AudioStreamNetwork
    {
        [Header("[Opus codec]")]
        [Range(6, 510)]
        [Tooltip("At very low bitrate codec switches to mono with further optimizations. Rates above 320 are not very practical.")]
        public int bitrate = 128;

        [Range(0, 10)]
        [Tooltip("Higher complexity provides e.g. better stereo resolution.")]
        public int complexity = 10;

        public enum OPUSAPPLICATIONTYPE
        {
            AUDIO
                , RESTRICTED_LOWDELAY
                , VOIP
        }
        [Tooltip("Encoder general aim:\r\nAUDIO - broadcast/high-fidelity application\r\nRESTRICTED_LOWDELAY - lowest-achievable latency, voice modes are not used.\r\nVOIP - VoIP/videoconference applications\r\n\r\nCannot be changed once the encoder has started.")]
        public OPUSAPPLICATIONTYPE opusApplicationType = OPUSAPPLICATIONTYPE.AUDIO;

        public enum RATE
        {
            CBR
                , VBR
                , Constrained_VBR
        }
        [Tooltip("Constatnt/Variable/Constrained variable bitrate")]
        public RATE rate = RATE.CBR;

        public enum OPUSFRAMESIZE
        {
            OPUSFRAMESIZE_120 = 120
                , OPUSFRAMESIZE_240 = 240
                , OPUSFRAMESIZE_480 = 480
                , OPUSFRAMESIZE_960 = 960
                , OPUSFRAMESIZE_1920 = 1920
                , OPUSFRAMESIZE_2880 = 2880
        }
        [Tooltip("The number of samples per channel in the input signal. This must be set such that\r\n\r\n1] is valid for Opus codec encoder\r\n2] be large enough to capture current OnAudioFilterRead buffer continously, and\r\n3] fit into MTU size as allowed by current network infrastructure.\r\n\r\nThe default (960) is usually OK for (Unity) Default DSP buffer size and common LAN routers with 1500 MTU size.")]
        public OPUSFRAMESIZE opusFrameSize = OPUSFRAMESIZE.OPUSFRAMESIZE_960;
        // ========================================================================================================================================
        #region Opus encoder
        OpusEncoder opusEncoder;
        void UpdateCodecBitrate(int _bitrate)
        {
            this.opusEncoder.Bitrate = (_bitrate * 1024);
        }

        void UpdateCodecComplexity(int _complexity)
        {
            this.opusEncoder.Complexity = _complexity;
        }

        void UpdateCodecVBRMode(RATE _rate)
        {
            this.rate = _rate;
            bool vbr = false, vbr_constrained = false;

            switch (this.rate)
            {
                case RATE.CBR:
                    vbr = vbr_constrained = false;
                    break;
                case RATE.VBR:
                    vbr = true;
                    vbr_constrained = false;
                    break;
                case RATE.Constrained_VBR:
                    vbr = vbr_constrained = true;
                    break;
            }

            this.opusEncoder.UseVBR = vbr;
            this.opusEncoder.UseConstrainedVBR = vbr_constrained;
        }

        bool StartEncoder_OPUS()
        {
            // only 1 or 2 channels permitted for the Opus encoder
            if (this.serverChannels != 1 && this.serverChannels != 2)
            {
                LOG(LogLevel.ERROR, "Unable to create Opus encoder - only MONO or STEREO system channels supported");
                return false;
            }

            // start the encoder
            OpusApplication opusApplication = OpusApplication.OPUS_APPLICATION_AUDIO;
            switch (this.opusApplicationType)
            {
                case OPUSAPPLICATIONTYPE.AUDIO:
                    opusApplication = OpusApplication.OPUS_APPLICATION_AUDIO;
                    break;
                case OPUSAPPLICATIONTYPE.RESTRICTED_LOWDELAY:
                    opusApplication = OpusApplication.OPUS_APPLICATION_RESTRICTED_LOWDELAY;
                    break;
                case OPUSAPPLICATIONTYPE.VOIP:
                    opusApplication = OpusApplication.OPUS_APPLICATION_VOIP;
                    break;
            }
            this.opusEncoder = new OpusEncoder(AudioStreamNetworkSource.opusSampleRate, AudioStreamNetworkSource.opusChannels, opusApplication);
            this.opusEncoder.EnableAnalysis = true;

            this.opusEncoder.UseInbandFEC = true;

            this.encodeThread =
#if UNITY_WSA
                new Task(new System.Action(this.EncodeLoop_OPUS), TaskCreationOptions.LongRunning | TaskCreationOptions.RunContinuationsAsynchronously);
#else
                new Thread(new ThreadStart(this.EncodeLoop_OPUS));
            this.encodeThread.Priority = this.encoderThreadPriority;
#endif
            this.encoderRunning = true;
            this.encodeThread.Start();

            LOG(LogLevel.INFO, "Created OPUS encoder {0} samplerate, {1} channels, {2}", AudioStreamNetworkSource.opusSampleRate, AudioStreamNetworkSource.opusChannels, opusApplication);
            
            return true;
        }
        #endregion
        // ========================================================================================================================================
        #region Encoder loop
        /// <summary>
        /// Samples to be encoded
        /// </summary>
        short[] samples2encode = null;
        /// <summary>
        /// encode buffer
        /// </summary>
        byte[] encodeBuffer = null;
#if ENABLE_IL2CPP
        [Unity.IL2CPP.CompilerServices.Il2CppSetOption(Unity.IL2CPP.CompilerServices.Option.NullChecks, false)]
        [Unity.IL2CPP.CompilerServices.Il2CppSetOption(Unity.IL2CPP.CompilerServices.Option.ArrayBoundsChecks, false)]
        [Unity.IL2CPP.CompilerServices.Il2CppSetOption(Unity.IL2CPP.CompilerServices.Option.DivideByZeroChecks, false)]
#endif
        void EncodeLoop_OPUS()
        {
            // prefix with the payload(config)
            var payload = this.PayloadToBytes(this.serverPayload);

            while (this.encoderRunning)
            {
                int fs = (int)this.opusFrameSize;
                var fsSamples = fs * AudioStreamNetworkSource.opusChannels;

                if (this.audioSamples.Available() >= fsSamples)
                {
                    // make enough room for decode buffer + 'one off error'/?
                    if (this.encodeBuffer == null || this.encodeBuffer.Length != fsSamples + 2)
                        this.encodeBuffer = new byte[fsSamples + 2];

                    float[] samples;
                    lock (this.audioSamplesLock)
                        samples = this.audioSamples.Read(fsSamples);

                    UnityAudio.FloatArrayToShortArray(samples, (uint)samples.Length, ref this.samples2encode);
                    
                    // don't allow thread to throw
                    int thisPacketSize = 0;
                    try
                    {
                        thisPacketSize = this.opusEncoder.Encode(this.samples2encode, 0, fs, this.encodeBuffer, 0, this.encodeBuffer.Length); // this throws OpusException on a failure, rather than returning a negative number
                    }
                    catch (System.Exception ex)
                    {
                        LOG(LogLevel.ERROR, "{0} / w bounds {1} {2} {3} ", ex.Message, this.opusFrameSize, this.audioSamples.Available(), this.encodeBuffer.Length);
                    }

                    if (thisPacketSize > 0)
                    {
                        // prefix with the payload(config)
                        var packet = new byte[payload.Length + thisPacketSize];
                        Array.Copy(payload, 0, packet, 0, payload.Length);

                        // add the rest
                        Array.Copy(this.encodeBuffer, 0, packet, payload.Length, thisPacketSize);

                        this.networkQueue.Enqueue(packet);
                    }
                }

                // don't tax CPU continuosly, but encode as fast as possible
#if UNITY_WSA
                this.encodeThread.Wait(1);
#else
                Thread.Sleep(1);
#endif
            }
        }
        #endregion
    }
}