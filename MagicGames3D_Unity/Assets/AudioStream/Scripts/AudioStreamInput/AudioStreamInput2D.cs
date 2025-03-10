﻿// (c) 2016-2023 Martin Cvengros. All rights reserved. Redistribution of source code without permission not allowed.
// uses FMOD by Firelight Technologies Pty Ltd

using AudioStreamSupport;
using System;
using UnityEngine;

namespace AudioStream
{
    public class AudioStreamInput2D : AudioStreamInputBase
    {
        /// <summary>
        /// Default mix matrix
        /// </summary>
        float[] mixMatrix;
        /// <summary>
        /// User provided mix matrix
        /// </summary>
        float[] customMixMatrix = null;
        /// <summary>
        /// Provide custom mix matrix between inputs and outputs - first dimension (rows) for outputs, the second one (columns) for inputs
        /// Actual signal's respective channel value is multiplied by provided float value according to the mapping
        /// Example of possible mappings: https://en.wikipedia.org/wiki/Matrix_decoder
        /// Needs to be called before starting the recording
        /// </summary>
        /// <param name="_customMixMatrix"></param>
        public void SetCustomMixMatrix(float[,] _customMixMatrix)
        {
            if (_customMixMatrix.Rank != 2)
                throw new System.NotSupportedException("Custom mix matrix providing mapping between inputs and outputs must be of dimension 2");

            var rows = _customMixMatrix.GetLength(0);
            var columns = _customMixMatrix.GetLength(1);

            this.customMixMatrix = new float[rows * columns];
            for (var row = 0; row < rows; ++row)
                for (var column = 0; column < columns; ++column)
                    this.customMixMatrix[row * columns + column] = _customMixMatrix[row, column];
        }
        /// <summary>
        /// Returns mix matrix currently being used - valid once recording has started - NaNs otherwise
        /// First dimension (rows) for outputs, 2nd dimension (columns) for inputs
        /// </summary>
        /// <returns></returns>
        public float[,] GetMixMatrix()
        {
            var result = new float[this.outputChannels, this.recChannels];

            // init w/ non existing values
            if (this.mixMatrix == null)
                for (var row = 0; row < this.outputChannels; ++row)
                    for (var column = 0; column < this.recChannels; ++column)
                        result[row, column] = float.NaN;
            else
                for (var i = 0; i < this.mixMatrix.Length; ++i)
                    result[i / this.recChannels, i % this.recChannels] = this.mixMatrix[i];

            return result;
        }
        // ========================================================================================================================================
        #region Recording
        /// <summary>
        /// If AudioSource is available on this GameObject, simulate resampling to output sample rate by setting the pitch (since we have no AudioClip with PCM callback created)
        /// , or resample using simple resampler - in which case we also need to map channels based on potential user setting
        /// </summary>
        protected override void RecordingStarted()
        {
            var asource = this.GetComponent<AudioSource>();
            if (asource)
            {
                // if resampling is not needed just start the source

                if (this.resampleInput)
                {
                    if (this.useUnityToResampleAndMapChannels)
                    {
                        // resample the input by setting the pitch on the AudioSource based on input/output samplerate ratio
                        // this hack seems to be the only way of doing this without an AudioClip with given input rate wich has to have PCM callback (as in AudioStreamInput)
                        // this can lead to drifting over time if the rates are different enough though
                        var pitch_nominator = (float)(this.recRate * this.recChannels);
                        var pitch_denominator = (float)(this.outputSampleRate * this.outputChannels);
                        asource.pitch = pitch_nominator / pitch_denominator;

                        LOG(LogLevel.INFO, "'Resampling' pitch based on current settings: {0}, from: {1} / {2} [ (recRate * recChannels) {3} * {4} / (output samplerate * output channels) {5} * {6} ], speaker mode: {7}"
                            , asource.pitch
                            , pitch_nominator
                            , pitch_denominator
                            , this.recRate
                            , this.recChannels
                            , this.outputSampleRate
                            , this.outputChannels
                            , AudioSettings.speakerMode
                            );
                    }
                    else
                    {
                        // check if custom mix matrix is present
                        if (this.customMixMatrix != null)
                        {
                            if (this.customMixMatrix.GetLength(0) != this.recChannels)
                                throw new System.NotSupportedException(string.Format("Custom mix matrix's input channels don't match selected input with {0} channels", this.recChannels));

                            if (this.customMixMatrix.GetLength(1) != this.outputChannels)
                                throw new System.NotSupportedException(string.Format("Custom mix matrix's output channels don't match selected output with {0} channels", this.outputChannels));

                            this.mixMatrix = this.customMixMatrix;
                        }
                        else
                        {
                            // otherwise set default mix matrix for in -> out channels

                            // Unity speakermode as FMOD.SPEAKERMODE (from Unity effective channels)
                            var unitySpeakerMode = UnityAudio.UnitySpeakerModeFromChannels(this.outputChannels).ToFMODSpeakerMode();

                            // FMOD.SPEAKERMODE of input
                            System.Guid in_guid;
                            int in_systemrate;
                            FMOD.SPEAKERMODE in_speakermode;
                            int in_speakermodechannels;
                            FMOD.DRIVER_STATE in_state;
                            result = this.recording_system.system.getRecordDriverInfo(this.recordDeviceId, out in_guid, out in_systemrate, out in_speakermode, out in_speakermodechannels, out in_state);
                            ERRCHECK(result, "this.recording_system..getRecordDriverInfo");


                            // get default mix matrix
                            this.mixMatrix = new float[this.recChannels * this.outputChannels];

                            result = this.recording_system.system.getDefaultMixMatrix(in_speakermode, unitySpeakerMode, this.mixMatrix, this.recChannels);
                            ERRCHECK(result, "this.recording_system.system.getDefaultMixMatrix");
                        }

                        // log the matrix
                        var matrixAsString = string.Empty;
                        for (var row = 0; row < this.outputChannels; ++row)
                        {
                            for (var column = 0; column < this.recChannels; ++column)
                                matrixAsString += this.mixMatrix[row * this.recChannels + column] + "     ";

                            matrixAsString += Environment.NewLine;
                        }

                        LOG(LogLevel.INFO, "{0}Mix matrix (Inputs <-> Outputs mapping):\r\n{1}", this.customMixMatrix != null ? "Custom " : "Default ", matrixAsString);

                        asource.pitch = 1f; // !
                    }
                }
                else
                {
                    asource.pitch = 1f; // !
                }

                asource.Play();
            }
            else
            {
                if (this.GetComponent<AudioListener>() == null)
                    Debug.LogError("AudioStreamInput2D has no hard dependency on AudioSource and there is none attached, there is no AudioListener attached too. Please make sure that AudioSource is attached to this GameObject, or that this component is attached to GameObject with AudioListener");
            }
        }
        /// <summary>
        /// Nothing to do since data is retrieved via OnAudioFilterRead
        /// </summary>
        protected override void RecordingUpdate()
        {
        }
        /// <summary>
        /// mix of inputs based on custom mix matrix or when input != output channels
        /// </summary>
        float[] inputMix = null;
        /// <summary>
        /// Final OAFR signal
        /// </summary>
        BasicBufferFloat oafrOutputBuffer = new BasicBufferFloat(10000);
        /// <summary>
        /// Retrieve recording data, and provide them for output.
        /// - Data can be filtered here
        /// </summary>
        /// <param name="data"></param>
        /// <param name="channels"></param>
#if ENABLE_IL2CPP
        [Unity.IL2CPP.CompilerServices.Il2CppSetOption(Unity.IL2CPP.CompilerServices.Option.NullChecks, false)]
        [Unity.IL2CPP.CompilerServices.Il2CppSetOption(Unity.IL2CPP.CompilerServices.Option.ArrayBoundsChecks, false)]
        [Unity.IL2CPP.CompilerServices.Il2CppSetOption(Unity.IL2CPP.CompilerServices.Option.DivideByZeroChecks, false)]
#endif
        void OnAudioFilterRead(float[] data, int channels)
        {
            if (!this.isRecording)
                return;

            // keep record update loop running even if paused
            this.UpdateRecordBuffer();

            var dlength = data.Length;
            
            // always drain incoming buffer
            var inputSignal = this.GetAudioOutputBuffer((uint)dlength);

            // if paused don't process it
            if (this.isPaused)
                return;

            var inputSignal_length = inputSignal.Length;

            if (inputSignal_length > 0)
            {
                // if resampling is not needed just copy original signal out 
                if (!this.resampleInput)
                {
                    for (int i = 0; i < Mathf.Min(dlength, inputSignal_length); ++i) data[i] += inputSignal[i];
                    return;
                }

                if (this.useUnityToResampleAndMapChannels)
                {
                    // just fill the output buffer with input signal and leave everything to Unity
                    for (int i = 0; i < Mathf.Min(dlength, inputSignal_length); ++i) data[i] += inputSignal[i];
                }
                else
                {
                    // prepare mix for output first

                    // map input channels to output channels
                    // - iterate in input channels chunks and find out how to map them to output based on mix matrix
                    var mixLength = (inputSignal.Length / this.recChannels) * this.outputChannels;
                    if (this.inputMix == null
                        || this.inputMix.Length != mixLength
                        )
                        this.inputMix = new float[mixLength];

                    float[] inputSample = new float[this.recChannels];

                    for (var i = 0; i < inputSignal.Length; i += this.recChannels)
                    {
                        System.Array.Copy(inputSignal, i, inputSample, 0, this.recChannels);

                        var mix = new float[this.outputChannels];

                        // iterate over input channels, output channels and add input based on mix matrix
                        if (this.mixMatrix != null)
                            for (var in_ch = 0; in_ch < inputSample.Length; ++in_ch)
                                for (var out_ch = 0; out_ch < this.outputChannels; ++out_ch)
                                    mix[out_ch] += (inputSample[in_ch] * this.mixMatrix[out_ch * inputSample.Length + in_ch]);

                        System.Array.Copy(mix, 0, this.inputMix, (i / this.recChannels) * this.outputChannels, this.outputChannels);
                    }

                    // if input and output sample rates are the same, there's nothing to do
                    if (this.recRate != this.outputSampleRate)
                    {
                        // resample original rate to output rate
                        var resampled = UnityAudio.ResampleFrame(this.inputMix, this.recRate, this.outputSampleRate);
                        this.oafrOutputBuffer.Write(resampled);
                    }
                    else
                    {
                        this.oafrOutputBuffer.Write(this.inputMix);
                    }

                    // get the final result for output
                    // - retrieve what's possible in current frame
                    var length = Mathf.Min(dlength, this.oafrOutputBuffer.Available());

                    var outArr = this.oafrOutputBuffer.Read(length);
                    for (int i = 0; i < length; ++i) data[i] += outArr[i];

                    // if (dlength > length) LOG(LogLevel.WARNING, "!skipped frame: {0}", length);
                }
            }
        }

        protected override void RecordingStopped()
        {
            var asource = this.GetComponent<AudioSource>();
            if (asource)
                asource.Stop();

            this.customMixMatrix = null;
        }
        #endregion
    }
}