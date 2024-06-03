// (c) 2016-2023 Martin Cvengros. All rights reserved. Redistribution of source code without permission not allowed.

using AudioStreamSupport;
using System;
#if UNITY_WSA
using System.Threading.Tasks;
#else
using System.Threading;
# endif

namespace AudioStream
{
    public abstract partial class AudioStreamNetworkSource : AudioStreamNetwork
    {
        // ========================================================================================================================================
        #region PCM
        // PCM  rate/channels is server/Unity audio/AudioSource in payload
        #endregion

        bool StartEncoder_PCM()
        {
            // no restrictions on audio format, will transfer source (Unity AudioSource) audio as is
            this.encodeThread =
#if UNITY_WSA
                new Task(new System.Action(this.EncodeLoop_PCM), TaskCreationOptions.LongRunning | TaskCreationOptions.RunContinuationsAsynchronously);
#else
                new Thread(new ThreadStart(this.EncodeLoop_PCM));
            this.encodeThread.Priority = this.encoderThreadPriority;
#endif
            this.encoderRunning = true;
            this.encodeThread.Start();

            LOG(LogLevel.INFO, "Created PCM source {0} samplerate, {1} channels", this.serverSamplerate, this.serverChannels);

            return true;
        }
        // ========================================================================================================================================
        #region '' Encoder loop
#if ENABLE_IL2CPP
        [Unity.IL2CPP.CompilerServices.Il2CppSetOption(Unity.IL2CPP.CompilerServices.Option.NullChecks, false)]
        [Unity.IL2CPP.CompilerServices.Il2CppSetOption(Unity.IL2CPP.CompilerServices.Option.ArrayBoundsChecks, false)]
        [Unity.IL2CPP.CompilerServices.Il2CppSetOption(Unity.IL2CPP.CompilerServices.Option.DivideByZeroChecks, false)]
#endif
        void EncodeLoop_PCM()
        {
            // prefix with the payload(config)
            var payload = this.PayloadToBytes(this.serverPayload);

            while (this.encoderRunning)
            {
                var fsSamples = this.audioSamplesChSize
                    ;

                if (this.audioSamples.Available() >= fsSamples)
                {
                    float[] samples;
                    lock (this.audioSamplesLock)
                        samples = this.audioSamples.Read(fsSamples);

                    AudioStreamSupport.UnityAudio.FloatArrayToPCM16ByteArray(samples, (uint)samples.Length, ref this.encodeBuffer);

                    var thisPacketSize = this.encodeBuffer.Length;
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