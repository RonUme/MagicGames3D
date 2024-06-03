// (c) 2016-2023 Martin Cvengros. All rights reserved. Redistribution of source code without permission not allowed.

using AudioStreamSupport;
using System;
using System.Threading;
using UnityEngine;

namespace AudioStream
{
    public abstract partial class AudioStreamNetworkClient : AudioStreamNetwork
    {
        // ========================================================================================================================================
        #region PCM 'decoder'
        /// <summary>
        /// Starts decoding
        /// </summary>
        bool StartDecoder_PCM()
        {
            this.decodeThread =
#if UNITY_WSA
                new Task(new System.Action(this.DecodeLoop_PCM), TaskCreationOptions.LongRunning | TaskCreationOptions.RunContinuationsAsynchronously);
#else
                new Thread(new ThreadStart(this.DecodeLoop_PCM));
            this.decodeThread.Priority = this.decoderThreadPriority;
#endif
            this.decoderRunning = true;
            this.decodeThread.Start();

            return true;
        }
        #endregion
        // ========================================================================================================================================
        #region Decoder loop
        /// <summary>
        /// Continuosly enqueues (decoded) signal into audioQueue
        /// </summary>
#if ENABLE_IL2CPP
        [Unity.IL2CPP.CompilerServices.Il2CppSetOption(Unity.IL2CPP.CompilerServices.Option.NullChecks, false)]
        [Unity.IL2CPP.CompilerServices.Il2CppSetOption(Unity.IL2CPP.CompilerServices.Option.ArrayBoundsChecks, false)]
        [Unity.IL2CPP.CompilerServices.Il2CppSetOption(Unity.IL2CPP.CompilerServices.Option.DivideByZeroChecks, false)]
#endif
        void DecodeLoop_PCM()
        {
            // skip payload
            var payloadSize = System.Runtime.InteropServices.Marshal.SizeOf(this.serverPayload);
            byte bytes_per_sample = 2;

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

                    int thisFrameSize = audioPacket.Length;

                    if (thisFrameSize > 0)
                    {
                        // Unity audio is PCMFLOAT
                        UnityAudio.ByteArrayToFloatArray(audioPacket, (uint)audioPacket.Length, bytes_per_sample, Sound.SOUND_FORMAT.PCM16, ref this.fArr);

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
        #endregion
    }
}