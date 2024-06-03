// (c) 2016-2023 Martin Cvengros. All rights reserved. Redistribution of source code without permission not allowed.

using AudioStreamSupport;
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
    public abstract partial class AudioStreamNetworkClient : AudioStreamNetwork
    {
        // ========================================================================================================================================
        #region Opus decoder
        OpusDecoder opusDecoder = null;
        /// <summary>
        /// Last detected frame size.
        /// </summary>
        public int opusPacket_frameSize
        {
            get;
            private set;
        }
        /// <summary>
        /// Decode Info
        /// </summary>
        public Concentus.Enums.OpusBandwidth opusPacket_Bandwidth
        {
            get;
            private set;
        }
        public Concentus.Enums.OpusMode opusPacket_Mode
        {
            get;
            private set;
        }
        public int opusPacket_Channels
        {
            get;
            private set;
        }
        public int opusPacket_NumFramesPerPacket
        {
            get;
            private set;
        }
        public int opusPacket_NumSamplesPerFrame
        {
            get;
            private set;
        }
        /// <summary>
        /// Starts decoding
        /// </summary>
        bool StartDecoder_OPUS()
        {
            this.opusDecoder = new OpusDecoder(AudioStreamNetworkSource.opusSampleRate, AudioStreamNetworkSource.opusChannels);

            this.decodeThread =
#if UNITY_WSA
                new Task(new System.Action(this.DecodeLoop_OPUS), TaskCreationOptions.LongRunning | TaskCreationOptions.RunContinuationsAsynchronously);
#else
                new Thread(new ThreadStart(this.DecodeLoop_OPUS));
            this.decodeThread.Priority = this.decoderThreadPriority;
#endif
            this.decoderRunning = true;
            this.decodeThread.Start();

            return true;
        }
        #endregion
        // ========================================================================================================================================
        #region Decoder loop
        float[] fArr;
        /// <summary>
        /// Continuosly enqueues (decoded) signal into audioQueue
        /// </summary>
#if ENABLE_IL2CPP
        [Unity.IL2CPP.CompilerServices.Il2CppSetOption(Unity.IL2CPP.CompilerServices.Option.NullChecks, false)]
        [Unity.IL2CPP.CompilerServices.Il2CppSetOption(Unity.IL2CPP.CompilerServices.Option.ArrayBoundsChecks, false)]
        [Unity.IL2CPP.CompilerServices.Il2CppSetOption(Unity.IL2CPP.CompilerServices.Option.DivideByZeroChecks, false)]
#endif
        void DecodeLoop_OPUS()
        {
            // skip payload
            var payloadSize = System.Runtime.InteropServices.Marshal.SizeOf(this.serverPayload);

            while (this.decoderRunning)
            {
                var networkPacket = this.networkQueue.Dequeue();

                if (networkPacket != null
                    && networkPacket.Length >= payloadSize
                    )
                {
                    // skip payload
                    var audioPacket = new byte[networkPacket.Length - payloadSize];
                    Array.Copy(networkPacket, payloadSize, audioPacket, 0, audioPacket.Length);

                    // Normal decoding
                    this.opusPacket_frameSize = OpusPacketInfo.GetNumSamples(audioPacket, 0, audioPacket.Length, AudioStreamNetworkSource.opusSampleRate);
                    this.opusPacket_Bandwidth = OpusPacketInfo.GetBandwidth(audioPacket, 0);
                    this.opusPacket_Mode = OpusPacketInfo.GetEncoderMode(audioPacket, 0);
                    this.opusPacket_Channels = OpusPacketInfo.GetNumEncodedChannels(audioPacket, 0);
                    this.opusPacket_NumFramesPerPacket = OpusPacketInfo.GetNumFrames(audioPacket, 0, audioPacket.Length);
                    this.opusPacket_NumSamplesPerFrame = OpusPacketInfo.GetNumSamplesPerFrame(audioPacket, 0, AudioStreamNetworkSource.opusSampleRate);

                    // keep 2 channels here - decoder can't cope with 1 channel only when e.g. decreased quality
                    short[] decodeBuffer = new short[this.opusPacket_frameSize * 2];

                    // opusFrameSize == thisFrameSize here 
                    int thisFrameSize = this.opusDecoder.Decode(audioPacket, 0, audioPacket.Length, decodeBuffer, 0, this.opusPacket_frameSize, false);

                    if (thisFrameSize > 0)
                    {
                        UnityAudio.ShortArrayToFloatArray(decodeBuffer, (uint)decodeBuffer.Length, ref this.fArr);

                        // resample if !using AudioClip
                        if (this.audioSourceIs2D)
                        {
                            // resample original source rate to output rate
                            var resampled = UnityAudio.ResampleFrame(this.fArr, this.serverPayload.samplerate, this.clientSamplerate);
                            lock (this.audioSamples)
                                this.audioSamples.Write(resampled);
                        }
                        else
                        {
                            lock (this.audioSamples)
                                this.audioSamples.Write(this.fArr);
                        }
                    }
                }
                //else
                //{
                //    // packet loss path not taken here since decoding loop runs usually much faster than audio loop

                //    this.opusFrameSize = 960;
                //    this.serverChannels = 2;

                //    float[] decodeBuffer = new float[this.opusFrameSize * this.serverChannels];

                //    // int thisFrameSize = 
                //    this.opusDecoder.Decode(null, 0, 0, decodeBuffer, 0, this.opusFrameSize, true);

                //    this.audioQueue.Enqueue(decodeBuffer);
                //}

                // don't tax CPU continuosly, but decode as fast as possible
                this.W84(1);
            }
        }
        /// <summary>
        /// 
        /// </summary>
        /// <param name="ms"></param>
        void W84(int ms)
        {
#if UNITY_WSA
            this.decodeThread.Wait(ms);
#else
            Thread.Sleep(ms);
#endif
        }
        #endregion
    }
}