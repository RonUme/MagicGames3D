// (c) 2016-2023 Martin Cvengros. All rights reserved. Redistribution of source code without permission not allowed.
// uses FMOD by Firelight Technologies Pty Ltd

using System.Collections;
using System.Linq;
using UnityEngine;

namespace AudioStream
{
    [ExecuteInEditMode()]
    public class AudioStreamNetMQSourceDemo : MonoBehaviour
    {
        public AudioStreamNetMQSource audioStreamNetMQSource;

        public AudioSource audioSource;
        public AudioStreamInput2D audioStreamInput;
        FMOD_SystemW.INPUT_DEVICE defaultInput; // just for UI display 
        IEnumerator Start()
        {
            while (!this.audioStreamInput.ready)
                yield return null;

            this.defaultInput = FMOD_SystemW.AvailableInputs(this.audioStreamInput.logLevel, this.audioStreamInput.gameObject.name, this.audioStreamInput.OnError, true)[0];
        }

        int frameSizeEnumSelection = 3;

        System.Text.StringBuilder gauge = new System.Text.StringBuilder(10);
        Vector2 scrollPosition = Vector2.zero;

        void OnGUI()
        {
            AudioStreamDemoSupport.OnGUI_GUIHeader("");

            this.scrollPosition = GUILayout.BeginScrollView(this.scrollPosition, new GUIStyle());

            GUILayout.Label("<b>Connect to this network source based on Network address below from other running client instance.</b>", AudioStreamSupport.UX.guiStyleLabelNormal);
            GUILayout.Label("<b>Audio is being captured on main/scene AudioListener, so both AudioSource (netradio) and microphone (or any input) are sent and mixed by Unity</b>", AudioStreamSupport.UX.guiStyleLabelNormal);
            GUILayout.Label(string.Format("<b>(the source of the audio is netradio @: {0}, it plays independently)</b>", this.audioSource.GetComponent<AudioStream>().url), AudioStreamSupport.UX.guiStyleLabelNormal);
            GUILayout.Label("<b>Please restart Source after switching codec</b>", AudioStreamSupport.UX.guiStyleLabelNormal);
            GUILayout.Label("<b>You can adjust encoder properties while running</b>", AudioStreamSupport.UX.guiStyleLabelNormal);

            GUILayout.Space(10);
            GUILayout.Label("==== AudioStreamNetMQSource", AudioStreamSupport.UX.guiStyleLabelNormal);

            if (GUILayout.Button(this.audioStreamNetMQSource.sourceRunning ? "Stop source" : "Start source"))
                if (this.audioStreamNetMQSource.sourceRunning)
                    this.audioStreamNetMQSource.StopSource();
                else
                    this.audioStreamNetMQSource.StartSource();

            using (new GUILayout.HorizontalScope())
            {
                GUILayout.Label("Monitor volume (doesn't affect sent AudioSource's audio): ", AudioStreamSupport.UX.guiStyleLabelNormal, GUILayout.MaxWidth(Screen.width / 4));
                this.audioStreamNetMQSource.monitorVolume = GUILayout.HorizontalSlider(this.audioStreamNetMQSource.monitorVolume, 0f, 1f, GUILayout.MaxWidth(Screen.width / 2));
                GUILayout.Label(Mathf.RoundToInt(this.audioStreamNetMQSource.monitorVolume * 100f) + " %", AudioStreamSupport.UX.guiStyleLabelNormal, GUILayout.MaxWidth(Screen.width / 4));
            }

            GUILayout.Space(10);
            GUILayout.Label("==== Codec", AudioStreamSupport.UX.guiStyleLabelNormal);

            GUILayout.BeginHorizontal();
            GUILayout.Label("Codec: ", AudioStreamSupport.UX.guiStyleLabelNormal, GUILayout.MaxWidth(Screen.width / 4));
            this.audioStreamNetMQSource.codec = (AudioStreamNetworkSource.CODEC)GUILayout.SelectionGrid((int)this.audioStreamNetMQSource.codec, System.Enum.GetNames(typeof(AudioStreamNetworkSource.CODEC)), 2, AudioStreamSupport.UX.guiStyleButtonNormal, GUILayout.MaxWidth(Screen.width / 4 * 3));
            GUILayout.EndHorizontal();

            GUILayout.Space(10);
            GUILayout.Label("==== Encoder", AudioStreamSupport.UX.guiStyleLabelNormal);

            GUILayout.BeginHorizontal();
            GUILayout.Label("Encoder thread priority: ", AudioStreamSupport.UX.guiStyleLabelNormal, GUILayout.MaxWidth(Screen.width / 4));
            this.audioStreamNetMQSource.encoderThreadPriority = (System.Threading.ThreadPriority)GUILayout.SelectionGrid((int)this.audioStreamNetMQSource.encoderThreadPriority, System.Enum.GetNames(typeof(System.Threading.ThreadPriority)), 6, AudioStreamSupport.UX.guiStyleButtonNormal, GUILayout.MaxWidth(Screen.width / 4 * 3));
            GUILayout.EndHorizontal();

            switch (this.audioStreamNetMQSource.codec)
            {
                case AudioStreamNetworkSource.CODEC.OPUS:

                    GUILayout.BeginHorizontal();
                    GUILayout.Label("OPUS application type: ", AudioStreamSupport.UX.guiStyleLabelNormal, GUILayout.MaxWidth(Screen.width / 4));
                    GUILayout.Label(this.audioStreamNetMQSource.opusApplicationType.ToString());
                    GUILayout.EndHorizontal();

                    if (this.audioStreamNetMQSource.encoderRunning)
                    {
                        GUILayout.Label(string.Format("OPUS samplerate: {0} channels: {1}", AudioStreamNetworkSource.opusSampleRate, AudioStreamNetworkSource.opusChannels), AudioStreamSupport.UX.guiStyleLabelNormal);

                        GUILayout.BeginHorizontal();
                        GUILayout.Label("Bitrate: ", AudioStreamSupport.UX.guiStyleLabelNormal, GUILayout.MaxWidth(Screen.width / 4));
                        this.audioStreamNetMQSource.bitrate = (int)GUILayout.HorizontalSlider(this.audioStreamNetMQSource.bitrate, 6, 510, GUILayout.MaxWidth(Screen.width / 2));
                        GUILayout.Label(this.audioStreamNetMQSource.bitrate + " KBits/s", AudioStreamSupport.UX.guiStyleLabelNormal, GUILayout.MaxWidth(Screen.width / 4));
                        GUILayout.EndHorizontal();

                        GUILayout.BeginHorizontal();
                        GUILayout.Label("Complexity: ", AudioStreamSupport.UX.guiStyleLabelNormal, GUILayout.MaxWidth(Screen.width / 4));
                        this.audioStreamNetMQSource.complexity = (int)GUILayout.HorizontalSlider(this.audioStreamNetMQSource.complexity, 0, 10, GUILayout.MaxWidth(Screen.width / 2));
                        var cdesc = "(Medium)";
                        switch (this.audioStreamNetMQSource.complexity)
                        {
                            case 0:
                            case 1:
                            case 2:
                                cdesc = "(Low)";
                                break;
                            case 3:
                            case 4:
                            case 5:
                                cdesc = "(Medium)";
                                break;
                            case 6:
                            case 7:
                            case 8:
                                cdesc = "(High)";
                                break;
                            case 9:
                            case 10:
                                cdesc = "(Very High)";
                                break;
                        }
                        GUILayout.Label(this.audioStreamNetMQSource.complexity + " " + cdesc, AudioStreamSupport.UX.guiStyleLabelNormal, GUILayout.MaxWidth(Screen.width / 4));
                        GUILayout.EndHorizontal();

                        GUILayout.BeginHorizontal();
                        GUILayout.Label("Rate Control: ", AudioStreamSupport.UX.guiStyleLabelNormal, GUILayout.MaxWidth(Screen.width / 4));
                        this.audioStreamNetMQSource.rate = (AudioStreamNetworkSource.RATE)GUILayout.SelectionGrid((int)this.audioStreamNetMQSource.rate, System.Enum.GetNames(typeof(AudioStreamNetworkSource.RATE)), 3, AudioStreamSupport.UX.guiStyleButtonNormal, GUILayout.MaxWidth(Screen.width / 4 * 3));
                        GUILayout.EndHorizontal();

                        GUILayout.BeginHorizontal();
                        GUILayout.Label("Frame size: ", AudioStreamSupport.UX.guiStyleLabelNormal, GUILayout.MaxWidth(Screen.width / 4));
                        this.frameSizeEnumSelection = GUILayout.SelectionGrid(this.frameSizeEnumSelection, System.Enum.GetNames(typeof(AudioStreamNetworkSource.OPUSFRAMESIZE)).Select(s => s.Split('_')[1]).ToArray(), 6, AudioStreamSupport.UX.guiStyleButtonNormal, GUILayout.MaxWidth(Screen.width / 4 * 3));
                        GUILayout.EndHorizontal();
                        switch (this.frameSizeEnumSelection)
                        {
                            case 0:
                                this.audioStreamNetMQSource.opusFrameSize = AudioStreamNetworkSource.OPUSFRAMESIZE.OPUSFRAMESIZE_120;
                                break;
                            case 1:
                                this.audioStreamNetMQSource.opusFrameSize = AudioStreamNetworkSource.OPUSFRAMESIZE.OPUSFRAMESIZE_240;
                                break;
                            case 2:
                                this.audioStreamNetMQSource.opusFrameSize = AudioStreamNetworkSource.OPUSFRAMESIZE.OPUSFRAMESIZE_480;
                                break;
                            case 3:
                                this.audioStreamNetMQSource.opusFrameSize = AudioStreamNetworkSource.OPUSFRAMESIZE.OPUSFRAMESIZE_960;
                                break;
                            case 4:
                                this.audioStreamNetMQSource.opusFrameSize = AudioStreamNetworkSource.OPUSFRAMESIZE.OPUSFRAMESIZE_1920;
                                break;
                            case 5:
                                this.audioStreamNetMQSource.opusFrameSize = AudioStreamNetworkSource.OPUSFRAMESIZE.OPUSFRAMESIZE_2880;
                                break;
                        }
                    }
                    break;
                case AudioStreamNetworkSource.CODEC.PCM:
                    if (this.audioStreamNetMQSource.encoderRunning)
                    {
                        GUILayout.Label(string.Format("PCM (Unity) samplerate: {0} channels: {1}", this.audioStreamNetMQSource.serverSamplerate, this.audioStreamNetMQSource.serverChannels), AudioStreamSupport.UX.guiStyleLabelNormal);
                    }
                    break;
            }

            GUILayout.Space(10);
            GUILayout.Label("==== Audio source", AudioStreamSupport.UX.guiStyleLabelNormal);

            GUILayout.BeginHorizontal();
            GUILayout.Label("Volume: ", AudioStreamSupport.UX.guiStyleLabelNormal, GUILayout.MaxWidth(Screen.width / 4));
            this.audioSource.volume = GUILayout.HorizontalSlider(this.audioSource.volume, 0f, 1f, GUILayout.MaxWidth(Screen.width / 2));
            GUILayout.Label(Mathf.RoundToInt(this.audioSource.volume * 100f) + " %", AudioStreamSupport.UX.guiStyleLabelNormal, GUILayout.MaxWidth(Screen.width / 4));
            GUILayout.EndHorizontal();

            GUILayout.Space(10);
            GUILayout.Label("==== Optionally start default microphone (AudioStreamInput2D)", AudioStreamSupport.UX.guiStyleLabelNormal);
            using (new GUILayout.HorizontalScope())
            {
                GUILayout.Label(this.defaultInput.name, AudioStreamSupport.UX.guiStyleLabelNormal, GUILayout.MaxWidth(Screen.width / 2));
                if (GUILayout.Button(this.audioStreamInput.isRecording ? "Stop microphone" : "Start microphone", GUILayout.MaxWidth(Screen.width / 2)))
                    if (this.audioStreamInput.isRecording)
                        this.audioStreamInput.Stop();
                    else
                        this.audioStreamInput.Record();
            }

            GUILayout.Space(10);
            GUILayout.Label("==== Network", AudioStreamSupport.UX.guiStyleLabelNormal);

            if (this.audioStreamNetMQSource.listenIP != "0.0.0.0")
                GUILayout.Label(string.Format("Running at {0} : {1}", this.audioStreamNetMQSource.listenIP, this.audioStreamNetMQSource.listenPort), AudioStreamSupport.UX.guiStyleLabelNormal);
            else
                GUILayout.Label("No network seems to be available", AudioStreamSupport.UX.guiStyleLabelNormal);

            GUILayout.Label(string.Format("Network queue size: {0}", this.audioStreamNetMQSource.networkQueueSize), AudioStreamSupport.UX.guiStyleLabelNormal);

            GUILayout.BeginHorizontal();
            GUILayout.Label("Source thread sleep: ", AudioStreamSupport.UX.guiStyleLabelNormal, GUILayout.MaxWidth(Screen.width / 4));
            this.audioStreamNetMQSource.networkThreadSleep = (int)GUILayout.HorizontalSlider(this.audioStreamNetMQSource.networkThreadSleep, 1, 20, GUILayout.MaxWidth(Screen.width / 2));
            GUILayout.Label(this.audioStreamNetMQSource.networkThreadSleep.ToString().PadLeft(2, '0') + " ms", AudioStreamSupport.UX.guiStyleLabelNormal, GUILayout.MaxWidth(Screen.width / 4));
            GUILayout.EndHorizontal();

            GUILayout.Space(10);

            GUILayout.Label("==== Status", AudioStreamSupport.UX.guiStyleLabelNormal);

            GUILayout.Label(string.Format("State = {0}"
                , this.audioStreamNetMQSource.encoderRunning ? "Playing" : "Stopped"
                )
                , AudioStreamSupport.UX.guiStyleLabelNormal
                );

            GUILayout.BeginHorizontal();

            GUILayout.Label(string.Format("Audio buffer size: {0} / available: {1}", this.audioStreamNetMQSource.audioSamplesChSize, this.audioStreamNetMQSource.audioSamplesSize), AudioStreamSupport.UX.guiStyleLabelNormal, GUILayout.MaxWidth( Screen.width / 3 ));

            var r = Mathf.CeilToInt(((float)this.audioStreamNetMQSource.audioSamplesSize / (float)this.audioStreamNetMQSource.audioSamplesChSize) * 10f);
            var c = Mathf.Min(r, 10);

            GUI.color = Color.Lerp(Color.red, Color.green, c / 10f);

            this.gauge.Length = 0;
            for (int i = 0; i < c; ++i) this.gauge.Append("#");
            GUILayout.Label(this.gauge.ToString(), AudioStreamSupport.UX.guiStyleLabelNormal, GUILayout.MaxWidth(Screen.width / 2));

            GUILayout.EndHorizontal();

            GUILayout.Space(40);

            GUILayout.EndScrollView();
        }
    }
}