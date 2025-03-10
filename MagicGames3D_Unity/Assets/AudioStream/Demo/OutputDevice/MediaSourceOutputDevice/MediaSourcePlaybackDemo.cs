﻿// (c) 2016-2023 Martin Cvengros. All rights reserved. Redistribution of source code without permission not allowed.
// uses FMOD by Firelight Technologies Pty Ltd

using AudioStream;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

[ExecuteInEditMode()]
public class MediaSourcePlaybackDemo : MonoBehaviour
{
    /// <summary>
    /// Available audio outputs reported by FMOD
    /// </summary>
    List<FMOD_SystemW.OUTPUT_DEVICE> availableOutputs = new List<FMOD_SystemW.OUTPUT_DEVICE>();
    /// <summary>
    /// FMOD output component to play audio on selected device + its channels
    /// </summary>
    public MediaSourceOutputDevice mediaSourceOutputDevice;
    /// <summary>
    /// demo sample assets stored in 'StreamingAssets/AudioStream'
    /// </summary>
    public string media_StreamingAssetsFilename = "24ch_polywav_16bit_48k.wav";
    /// <summary>
    /// FMOD channel (sounds playing) reference of the sound
    /// </summary>
    FMOD.Channel channel;
    /// <summary>
    /// has to be independent from user sound/channel to be manipulable when channel is not playing
    /// </summary>
    float channel_volume = 0.5f;

    #region UI events

    Dictionary<string, string> playbackStatesFromEvents = new Dictionary<string, string>();
    Dictionary<string, string> notificationStatesFromEvents = new Dictionary<string, string>();

    public void OnPlaybackStarted(string goName)
    {
        this.playbackStatesFromEvents[goName] = "Playback started";
    }
    public void OnPlaybackPaused(string goName)
    {
        this.playbackStatesFromEvents[goName] = "Playback paused";
    }

    public void OnPlaybackStopped(string goName)
    {
        this.playbackStatesFromEvents[goName] = "Playback stopped";
    }

    public void OnPlaybackError(string goName, string msg)
    {
        this.playbackStatesFromEvents[goName] = msg;
    }

    public void OnNotificationError(string goName, string msg)
    {
        this.notificationStatesFromEvents[goName] = msg;
    }

    public void OnNotificationDevicesChanged(string goName)
    {
        this.notificationStatesFromEvents[goName] = "Devices changed";

        // update available outputs device list
        this.availableOutputs = FMOD_SystemW.AvailableOutputs(this.mediaSourceOutputDevice.logLevel, this.mediaSourceOutputDevice.gameObject.name, this.mediaSourceOutputDevice.OnError);

        //string msg = "Available outputs:" + System.Environment.NewLine;
        //for (int i = 0; i < this.availableOutputs.Count; ++i)
        //    msg += this.availableOutputs[i].id + " : " + this.availableOutputs[i].name + System.Environment.NewLine;
        //Debug.Log(msg);

        /*
         * do any custom reaction based on outputs change here
         */

        // for demo we select correct displayed list item of playing output
        // since ASOD components update their output driver id automatically after devices change, just sync list with the id
        this.selectedOutput = this.mediaSourceOutputDevice.RuntimeOutputDriverID;
    }

    #endregion
    /// <summary>
    /// User selected audio output driver id
    /// </summary>
    int selectedOutput = 0; // 0 is system default
    int previousSelectedOutput = -1; // trigger device change at start

    IEnumerator Start()
    {
        while (!this.mediaSourceOutputDevice.ready)
            yield return null;

        // get demo assets paths
        yield return AudioStreamDemoSupport.GetFilenameFromStreamingAssets(this.media_StreamingAssetsFilename, (path) => this.media_StreamingAssetsFilename = path);

        // create sound in paused state [on device 0, it'll be recreated on device switch]
        this.mediaSourceOutputDevice.StartUserSound(this.media_StreamingAssetsFilename, this.channel_volume, true, false, null, 0, 0, out this.channel);

        // check for available outputs once ready, i.e. FMOD is started up
        if (Application.isPlaying)
        {
            string msg = "Available outputs:" + System.Environment.NewLine;

            this.availableOutputs = FMOD_SystemW.AvailableOutputs(this.mediaSourceOutputDevice.logLevel, this.mediaSourceOutputDevice.gameObject.name, this.mediaSourceOutputDevice.OnError);

            for (int i = 0; i < this.availableOutputs.Count; ++i)
                msg += this.availableOutputs[i].id + " : " + this.availableOutputs[i].name + System.Environment.NewLine;

            Debug.Log(msg);
        }
    }

    void OnDestroy()
    {
        this.mediaSourceOutputDevice.ReleaseUserSound(this.channel);
    }

    Vector2 scrollPosition1 = Vector2.zero, scrollPosition2 = Vector2.zero;
    /// <summary>
    /// trigger UI change without user clicking on 1st screen showing
    /// </summary>
    bool guiStart = true;
    void OnGUI()
    {
        AudioStreamDemoSupport.OnGUI_GUIHeader(this.mediaSourceOutputDevice.fmodVersion);

        GUILayout.Label("This scene will play testing 24 channel audio file on selected output\r\n" +
            "Output channels selection is not changed so audio file channels are mapped (and down/mixed) to outputs automatically by FMOD\r\n" +
            "This component doesn't use Unity audio", AudioStreamSupport.UX.guiStyleLabelNormal);

        GUILayout.Label("Select output device and press Play (default output device is preselected).", AudioStreamSupport.UX.guiStyleLabelNormal);

        // selection of available audio outputs at runtime
        // list can be long w/ special devices with many ports so wrap it in scroll view
        this.scrollPosition1 = GUILayout.BeginScrollView(this.scrollPosition1, new GUIStyle());

        GUILayout.Label("Available output devices:", AudioStreamSupport.UX.guiStyleLabelNormal);

        if (this.availableOutputs.Count < 1)
            GUILayout.Label("File loading...");

        this.selectedOutput = GUILayout.SelectionGrid(this.selectedOutput, this.availableOutputs.Select((output, index) => string.Format("[Output #{0}]: {1}", index, output.name)).ToArray()
            , 1, AudioStreamSupport.UX.guiStyleButtonNormal);

        GUILayout.Label(string.Format("-- user requested {0}, running on {1}", this.mediaSourceOutputDevice.outputDevice.name, this.mediaSourceOutputDevice.RuntimeOutputDriverID), AudioStreamSupport.UX.guiStyleLabelNormal);

        if (this.selectedOutput != this.previousSelectedOutput
            && this.availableOutputs.Count > 0)
        {
            if ((Application.isPlaying
                // Indicate correct device in the list, but don't call output update if it was not due user changing / clicking it
                && Event.current.type == EventType.Used
                )
                || this.guiStart
                )
            {
                this.guiStart = false;

                // switch output :
                // : the system's sounds need to be recreated as well

                this.mediaSourceOutputDevice.ReleaseUserSound(this.channel);

                this.mediaSourceOutputDevice.SetOutput(this.selectedOutput);

                this.mediaSourceOutputDevice.StartUserSound(this.media_StreamingAssetsFilename, this.channel_volume, true, false, null, 0, 0, out this.channel);
            }

            this.previousSelectedOutput = this.selectedOutput;
        }

        GUILayout.EndScrollView();

        GUI.color = Color.yellow;

        foreach (var p in this.playbackStatesFromEvents)
            GUILayout.Label(p.Key + " : " + p.Value, AudioStreamSupport.UX.guiStyleLabelNormal);

        foreach (var p in this.notificationStatesFromEvents)
            GUILayout.Label(p.Key + " : " + p.Value, AudioStreamSupport.UX.guiStyleLabelNormal);

        GUI.color = Color.white;

        this.scrollPosition2 = GUILayout.BeginScrollView(this.scrollPosition2, new GUIStyle());

        FMOD.RESULT lastError;
        string lastErrorString;

        lastErrorString = this.mediaSourceOutputDevice.GetLastError(out lastError);

        GUILayout.Label(this.mediaSourceOutputDevice.GetType() + "   ========================================", AudioStreamSupport.UX.guiStyleLabelSmall);
        GUILayout.Label(string.Format("State = {0}"
            , lastError + " " + lastErrorString
            )
            , AudioStreamSupport.UX.guiStyleLabelNormal);

        GUILayout.Label(string.Format("Output device latency average: {0} ms", this.mediaSourceOutputDevice.latencyAverage), AudioStreamSupport.UX.guiStyleLabelNormal);

        GUILayout.Space(10);

        // Display output channels of currently selected output for each media separately once everything is available
        if (this.availableOutputs.Count > this.selectedOutput)
        {
            using (new GUILayout.HorizontalScope())
            {
                // media
                using (new GUILayout.VerticalScope())
                {
                    // display info about media assets used

                    GUILayout.Label("24 ch audio clip: " + this.media_StreamingAssetsFilename, AudioStreamSupport.UX.guiStyleLabelNormal);
                    GUILayout.Label("used with kind permission from Jon Olive of MagicBeans Physical Audio Ltd.", AudioStreamSupport.UX.guiStyleLabelNormal);

                    GUILayout.Label("Gain of the output mix: ");

                    GUILayout.BeginHorizontal();

                    this.channel_volume = (float)System.Math.Round(
                        GUILayout.HorizontalSlider(this.channel_volume, 0f, 1.2f, GUILayout.MaxWidth(Screen.width / 2))
                        , 2
                        );
                    GUILayout.Label(Mathf.Round(this.channel_volume * 100f) + " %", AudioStreamSupport.UX.guiStyleLabelNormal);

                    if (this.channel_volume != this.mediaSourceOutputDevice.GetVolume(this.channel))
                        this.mediaSourceOutputDevice.SetVolume(this.channel, this.channel_volume);

                    GUILayout.EndHorizontal();

                    GUILayout.BeginHorizontal();

                    if (GUILayout.Button(this.mediaSourceOutputDevice.IsSoundPlaying(this.channel) ? "Stop" : "Play", AudioStreamSupport.UX.guiStyleButtonNormal)
                        && this.mediaSourceOutputDevice.ready
                        )
                        if (this.mediaSourceOutputDevice.IsSoundPlaying(this.channel))
                        {
                            this.mediaSourceOutputDevice.StopUserSound(this.channel);
                        }
                        else
                        {
                            FMOD.Channel newChannel;
                            this.mediaSourceOutputDevice.PlayUserSound(this.channel, this.channel_volume, true, null, 0, 0, out newChannel);
                            this.channel = newChannel;
                        }

                    GUILayout.EndHorizontal();
                }
            }
        }

        GUILayout.Space(40);

        GUILayout.EndScrollView();
    }
}