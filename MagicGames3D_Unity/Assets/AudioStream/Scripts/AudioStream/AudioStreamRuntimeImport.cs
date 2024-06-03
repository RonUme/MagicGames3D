// (c) 2016-2023 Martin Cvengros. All rights reserved. Redistribution of source code without permission not allowed.
// uses FMOD by Firelight Technologies Pty Ltd

using AudioStreamSupport;
using FMOD;
using System;
using System.IO;
using UnityEngine;
#if UNITY_WSA
using System.Threading.Tasks;
#else
using System.Threading;
#endif

namespace AudioStream
{
    /// <summary>
    /// non realtime stream decoding into optional cache + AudioClip creation
    /// resulting AudioClip is returned in an event callback once the download is finished/stopped.
    /// </summary>
    public class AudioStreamRuntimeImport : AudioStreamBase
    {
        // ========================================================================================================================================
        #region Editor
        [Header("[AudioStreamRuntimeImport]")]
        /// <summary>
        /// Checked from base before setting up the stream in order to skip streaming completely and retrieve previously saved audio from cache instead
        /// </summary>
        [Tooltip("If previously cached download exists for given url/file + uniqueCacheId, the download can be skipped and AudioClip can be created immediately from cached file instead if this is not enabled.\r\nOtherwise the stream is always started and any previously downloaded data is overwritten")]
        public bool overwriteCachedDownload = false;
        /// <summary>
        /// Advanced scripting usage - provide your own (unique) ID which will be used in conjuction with url as cache identifier
        /// Allows caching of multiple downloads from the same source/url (e.g. from the same web radio)
        /// </summary>
        public string cacheIdentifier = string.Empty;

        // TODO: turned off for now since it would need separate thread just for BinaryReader.ReadSingle() for every sinlge read from potentially large decoded file -
        // so probably not going to happen
        // [Tooltip("If true, the AudioClip creation will use *less* memory, but will be 5-10 times slower depending on the platform.\r\n\r\nThis might help if you experience memory related crashes esp. on mobiles when creating long/er clips.")]
        // BinaryReader.ReadBytes is ~ 5x -10x faster than BinaryReader.ReadSingle() loop
        // (small testing mp3 file, Unity 5.5.4, .net 3.5, win_64 ):
        // br.ReadBytes ms:
        // 5, 2, 10, 3, 2, 11, 10, 10, 10, 11
        // br.ReadSingle ms:
        // 72, 60, 60, 69, 60, 63, 59, 60, 68, 66
        // public bool slowClipCreation = false;

        /// <summary>
        /// User event with new AudioClip
        /// The passed clip is always created anew so it's user's resposibility to handle its memory/mamagement - see demo scene for example usage
        /// </summary>
        [Tooltip("User event containing new AudioClip after the download is complete.\r\nThe passed clip is always created anew so it's user's resposibility to handle its memory/mamagement - see demo scene for example usage")]
        public EventWithStringAudioClipParameter OnAudioClipCreated;
        [Tooltip("Samples of complete download (this is what's used to set AudioClip data)\r\nThese are in PCM FLOAT format, and channels and samplerate with currently used audio output")]
        public EventWithStringSamplesParameters OnSamplesCreated;
        [Tooltip("Decoded noncompressed PCM audio data")]
        /// <summary>
        /// Decoded bytes read from (file)stream
        /// </summary>
        [ReadOnly]
        public long decoded_bytes;
        [ReadOnly]
        public long decodingToAudioClipTimeInMs;
        #endregion
        // ========================================================================================================================================
        #region AudioStreamBase
        public override void SetOutput(int outputDriverId)
        {
            throw new System.NotImplementedException("This component doesn't support SetOutput.");
        }

        protected override void StreamChanged(float samplerate, int channels, SOUND_FORMAT sound_format)
        {
            LOG(LogLevel.INFO, "Stream samplerate change from {0}", this.streamSampleRate);

            this.StreamStopping();

            this.StreamStarting();

            LOG(LogLevel.INFO, "Stream samplerate changed to {0}", samplerate);
        }
        /// <summary>
        /// Measures time of just AudioClip creation, i.e. just time from cache to AudioClip
        /// (decoding + saving need not be necessarily always run)
        /// </summary>
        System.Diagnostics.Stopwatch sw;
        /// <summary>
        /// File cache for incoming decoder data
        /// </summary>
        FileStream fs = null;
        BinaryWriter bw = null;
        protected override void StreamStarting()
        {
            // (bps is not really meaningfull here since the system runs not in realtime)
            var output_Bps = this.fmodsystem.output_sampleRate * sizeof(float) * 5;
            // LOG(LogLevel.INFO, "Bps: {0} ; based on samplerate: {1}, channels: {2}, stream_bytes_per_sample: {3}", output_Bps, (int)this.streamSampleRate, this.streamChannels, this.streamBytesPerSample);

            // create decoder <-> PCM exchange
            this.decoderAudioQueue = new ThreadSafeListFloat(output_Bps);

            // add capture DSP to read decoded data
            result = this.channel.addDSP(FMOD.CHANNELCONTROL_DSP_INDEX.TAIL, this.captureDSP);
            ERRCHECK(result, "channel.addDSP");

            // create cache file and save stream properties

            // - save stream properties into its header/beginning for cached clip retrieval 
            var filepath = FileSystem.TempFilePath(this.url, this.cacheIdentifier, ".raw");
            LOG(LogLevel.INFO, "Creating cache file {0} with samplerate: {1}, channels: {2} ({3} bytes per sample)", filepath, this.fmodsystem.output_sampleRate, this.fmodsystem.output_channels, sizeof(float));

            this.fs = new FileStream(filepath, FileMode.Create, FileAccess.Write, FileShare.None);
            this.bw = new BinaryWriter(this.fs);

            this.bw.Write(this.fmodsystem.output_sampleRate);
            this.bw.Write(this.fmodsystem.output_channels);

            // start the dsp read
            this.StartDownload();
        }

        protected override void StreamStarving() { }

        protected override void StreamStopping()
        {
            // stop and retrieve the clip if dl was running prior
            this.StopDownloadAndCreateAudioClip(false);

            if (this.channel.hasHandle())
            {
                result = this.channel.removeDSP(this.captureDSP);
                // ERRCHECK(result, "channel.removeDSP", false); - will ERR_INVALID_HANDLE on finished channel -
            }
        }
        #endregion
        // ========================================================================================================================================
        #region DSP read
        /// <summary>
        /// (Encoded) file size - note: this means final decoded_bytes won't match this
        /// </summary>
        public long? file_size;
        /// <summary>
        /// Flag for the decoder thread
        /// </summary>
        bool decoderLoopRunning;
#if UNITY_WSA
        Task
#else
        System.Threading.Thread
#endif
        decoderThread;
        void StartDownload()
        {
            // reset download progress and try to determine the size of the file
            this.decoded_bytes = 0;
            this.file_size = this.mediaLength == INFINITE_LENGTH ? null : (long?)this.mediaLength;

            this.decoderThread =
#if UNITY_WSA
                new Task(new Action(this.DecoderLoop), TaskCreationOptions.LongRunning | TaskCreationOptions.RunContinuationsAsynchronously);
#else
                new System.Threading.Thread(new ThreadStart(this.DecoderLoop));
            this.decoderThread.Priority = System.Threading.ThreadPriority.Normal;
#endif
            this.decoderLoopRunning = true;
            this.decoderThread.Start();
        }
        /// <summary>
        /// Don't call from user code. Has to be public only to be reachable from ancestor
        /// </summary>
        /// <param name="forceRetrievalFromCache"></param>
        public void StopDownloadAndCreateAudioClip(bool forceRetrievalFromCache)
        {
            if (this.decoderThread != null)
            {
                // If the thread that calls Abort holds a lock that the aborted thread requires, a deadlock can occur.
                this.decoderLoopRunning = false;
#if !UNITY_WSA
                this.decoderThread.Join();
#endif
                this.decoderThread = null;
            }

            // Process cached data if called after download (cache file writer is still open), or when requesting cache retrieval directly (from startup)
            // ( will be also called from e.g. Stop (scene exit ..) in which case this gets skipped)
            if (this.fs != null || forceRetrievalFromCache)
            {
                this.sw = System.Diagnostics.Stopwatch.StartNew();

                // finish the file
                if (this.fs != null)
                {
                    this.bw.Close();
                    this.fs.Close();

                    this.bw = null;
                    this.fs = null;
                }

                // file saved - create new AudioClip

                // create clip from saved file
                // we'll use BinaryReader.ReadBytes
                // (not using BinaryReader.ReadSingle() loop based on user 'slowClipCreation' choice since it's not used)

                using (var fs = new FileStream(FileSystem.TempFilePath(this.url, this.cacheIdentifier, ".raw"), FileMode.Open, FileAccess.Read, FileShare.None))
                {
                    using (var br = new BinaryReader(fs))
                    {
                        // retrieve saved stream properties
                        var sr = br.ReadInt32();
                        var channels = br.ReadInt32();

                        var headerSize = sizeof(int) + sizeof(int);

                        // retrieve bytes audio data
                        var remainingBytes = (int)fs.Length - headerSize;
                        float[] samples = new float[remainingBytes / sizeof(float)];

                        /*
                        if (this.slowClipCreation)
                        {
                            // use slower method reading directly from the file into samples, which but skips creation of another in-memory buffer for conversion
                            for (var i = 0; i < samples.Length; ++i)
                                samples[i] = br.ReadSingle();
                        }
                        else
                        */
                        {
                            // read the whole file into memory first
                            var bytes = br.ReadBytes(remainingBytes);  // = File.ReadAllBytes(AudioStreamSupport.CachedFilePath(this.url));

                            // convert byte array to audio floats
                            // since it has has known format we can use BlockCopy instead of AudioStreamSupport.ByteArrayToFloatArray which is too slow for large clips
                            Buffer.BlockCopy(bytes, 0, samples, 0, bytes.Length);
                        }

                        // AudiClip.Create will throw on empty data
                        if (samples.Length > 0)
                        {
                            // samples event
                            if (this.OnSamplesCreated != null)
                                this.OnSamplesCreated.Invoke(this.gameObject.name, samples, channels, sr);
                            
                            // create the clip and set its samples
                            // samples are from FMOD mixer which runs @ output 0 samplerate - not this.streamSampleRate, so sound should be already resampled for output
                            // samplerate for output could be overriden in DevicesConfiguration - use SR what the system was created with

                            var audioClip = AudioClip.Create(this.url, samples.Length / channels, channels, sr, false);

                            if (audioClip.SetData(samples, 0))
                            {
                                LOG(LogLevel.INFO, "Created audio clip, samples: {0}, channels: {1}, samplerate: {2}, length: {3}", audioClip.samples, audioClip.channels, audioClip.frequency, audioClip.length);

                                if (this.OnAudioClipCreated != null)
                                    this.OnAudioClipCreated.Invoke(this.gameObject.name, audioClip);
                            }
                            else
                                LOG(LogLevel.ERROR, "Unable to set the clip data");
                        }
                    }
                }

                this.sw.Stop();
                this.decodingToAudioClipTimeInMs = sw.ElapsedMilliseconds;
                this.sw = null;

                LOG(LogLevel.INFO, "Setting up new AudioClip from downloaded data took: {0} ms", this.decodingToAudioClipTimeInMs);
            }
        }
        public int ntimeout { get; protected set; }
#if ENABLE_IL2CPP
        [Unity.IL2CPP.CompilerServices.Il2CppSetOption(Unity.IL2CPP.CompilerServices.Option.NullChecks, false)]
        [Unity.IL2CPP.CompilerServices.Il2CppSetOption(Unity.IL2CPP.CompilerServices.Option.ArrayBoundsChecks, false)]
        [Unity.IL2CPP.CompilerServices.Il2CppSetOption(Unity.IL2CPP.CompilerServices.Option.DivideByZeroChecks, false)]
#endif
        void DecoderLoop()
        {
            var readlength = int.MaxValue;

            this.ntimeout = 1;

            // decoded PCM
            float[] samples = null;

            // leave the loop running - if there's no data the starvation gets picked up in base
            while (this.decoderLoopRunning)
            {
                result = this.fmodsystem.Update();
                FMODHelpers.ERRCHECK(result, this.logLevel, this.gameObjectName, null, "decoder fmodsystem.Update");

                if (result == FMOD.RESULT.OK)
                {
                    // copy out all that's available
                    var paddedSignal = this.decoderAudioQueue.Read(readlength);

                    // for some (mp3?) files FMOD:
                    // 1) reports incorrect length e.g. 2x | this is the case even for their "play_sound" core native example
                    // 2) produces completely empty (all 0) buffer for 2nd half of the file (until playback/import gracefully finishes)
                    // as a workaround here:
                    // - detect buffer with all 0 in which case it won't be written to output
                    // after finisheg decoding/playing a file there also might be zeroed out portion in last decoded frame:
                    // - in that case only the filled part will be written
                    // both of the above are completely wrong

                    var lastNon0At = paddedSignal.Length;
                    bool all0 = true;
                    for (var i = paddedSignal.Length - 1; i > -1; --i)
                    {
                        all0 &= paddedSignal[i] == 0;
                        if (!all0)
                        {
                            lastNon0At = i;
                            break;
                        }
                    }

                    if (paddedSignal.Length - lastNon0At > 4096) // . TODO: make this a DSP callback block at least
                    {
                        samples = new float[lastNon0At + 1];
                        Array.Copy(paddedSignal, 0, samples, 0, lastNon0At + 1);
                    }
                    else
                    {
                        if (!all0)
                            samples = paddedSignal;
                        else
                        {
                            // this.LOG(LogLevel.WARNING, "Zero decoded frame not written");
                            samples = new float[0];
                        }
                    }

                    var length = samples.Length;
                    if (length > 0)
                    {
                        this.decoded_bytes += (samples.LongLength * sizeof(float));

                        // save decoded data to disk

                        // since BinaryWriter needs byte[] we have to convert the floats
                        var barr = new byte[samples.Length * sizeof(float)];
                        Buffer.BlockCopy(samples, 0, barr, 0, barr.Length);

                        this.bw.Write(barr);
                    }
                }
#if UNITY_WSA
                this.decoderThread.Wait(this.ntimeout);
#else
                System.Threading.Thread.Sleep(this.ntimeout);
#endif
            }
        }
        #endregion
    }
}