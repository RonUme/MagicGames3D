// (c) 2016-2023 Martin Cvengros. All rights reserved. Redistribution of source code without permission not allowed.

using UnityEngine;

namespace AudioStream
{
    [ExecuteInEditMode()]
    public class AudioStreamOscClientDemo : MonoBehaviour
    {
        public AudioStreamOscClient audioStreamOscClient;

        System.Text.StringBuilder gauge = new System.Text.StringBuilder(10);
        Vector2 scrollPosition = Vector2.zero;

        void OnGUI()
        {
            AudioStreamDemoSupport.OnGUI_GUIHeader("");

            GUILayout.Label("<b>Network client (OscServer) will receive audio on specified port (and asset's configured  OSC address), press Connect to start receiving</b>", AudioStreamSupport.UX.guiStyleLabelNormal);
            GUILayout.Label("<b>This demo uses 2D AudioSource resulting in (much) better network latency</b>", AudioStreamSupport.UX.guiStyleLabelNormal);

            this.scrollPosition = GUILayout.BeginScrollView(this.scrollPosition, new GUIStyle());

            GUILayout.Label("==== Decoder", AudioStreamSupport.UX.guiStyleLabelNormal);

            GUILayout.BeginHorizontal();
            GUILayout.Label("Decoder thread priority: ", AudioStreamSupport.UX.guiStyleLabelNormal, GUILayout.MaxWidth(Screen.width / 4));
            this.audioStreamOscClient.decoderThreadPriority = (System.Threading.ThreadPriority)GUILayout.SelectionGrid((int)this.audioStreamOscClient.decoderThreadPriority, System.Enum.GetNames(typeof(System.Threading.ThreadPriority)), 5, AudioStreamSupport.UX.guiStyleButtonNormal, GUILayout.MaxWidth(Screen.width / 4 * 3));
            GUILayout.EndHorizontal();

            if (!this.audioStreamOscClient.isConnected)
            {
                GUILayout.Space(10);

                GUILayout.Label("==== Network");

                GUILayout.BeginHorizontal();
                GUILayout.Label("Local IP: ", AudioStreamSupport.UX.guiStyleLabelNormal, GUILayout.MaxWidth(Screen.width / 4));
                GUILayout.Label(this.audioStreamOscClient.localIP, AudioStreamSupport.UX.guiStyleLabelNormal, GUILayout.MaxWidth(Screen.width / 4));
                GUILayout.EndHorizontal();

                GUILayout.BeginHorizontal();
                GUILayout.Label("Receiver port: ", AudioStreamSupport.UX.guiStyleLabelNormal, GUILayout.MaxWidth(Screen.width / 4));
                this.audioStreamOscClient.connectPort = int.Parse(GUILayout.TextField(this.audioStreamOscClient.connectPort.ToString(), GUILayout.MaxWidth(Screen.width / 4)));
                GUILayout.EndHorizontal();

                if (GUILayout.Button("Connect", AudioStreamSupport.UX.guiStyleButtonNormal))
                    this.audioStreamOscClient.StartClient();
            }
            else
            {
                switch(this.audioStreamOscClient.serverPayload.codec)
                {
                    case AudioStreamNetwork.CODEC.OPUS:
                        GUILayout.Label("OPUS info:");
                        GUILayout.Label(string.Format("Codec sample rate: {0}, channels: {1}", AudioStreamNetworkSource.opusSampleRate, AudioStreamNetworkSource.opusChannels), AudioStreamSupport.UX.guiStyleLabelNormal);
                        GUILayout.Label(string.Format("Current frame size: {0}", this.audioStreamOscClient.opusPacket_frameSize), AudioStreamSupport.UX.guiStyleLabelNormal);
                        GUILayout.Label(string.Format("Bandwidth: {0}", this.audioStreamOscClient.opusPacket_Bandwidth), AudioStreamSupport.UX.guiStyleLabelNormal);
                        GUILayout.Label(string.Format("Mode: {0}", this.audioStreamOscClient.opusPacket_Mode), AudioStreamSupport.UX.guiStyleLabelNormal);
                        GUILayout.Label(string.Format("Channels: {0}", this.audioStreamOscClient.opusPacket_Channels), AudioStreamSupport.UX.guiStyleLabelNormal);
                        GUILayout.Label(string.Format("Frames per packet: {0}", this.audioStreamOscClient.opusPacket_NumFramesPerPacket), AudioStreamSupport.UX.guiStyleLabelNormal);
                        GUILayout.Label(string.Format("Samples per frame: {0}", this.audioStreamOscClient.opusPacket_NumSamplesPerFrame), AudioStreamSupport.UX.guiStyleLabelNormal);
                        break;
                    case AudioStreamNetwork.CODEC.PCM:
                        GUILayout.Label("PCM info:");
                        GUILayout.Label(string.Format("Codec sample rate: {0}, channels: {1}", this.audioStreamOscClient.serverPayload.samplerate, this.audioStreamOscClient.serverPayload.channels), AudioStreamSupport.UX.guiStyleLabelNormal);
                        break;
                }
                
                GUILayout.Space(10);
                GUILayout.Label("==== Audio source");
                var @as = this.audioStreamOscClient.GetComponent<AudioSource>();
                GUILayout.BeginHorizontal();
                GUILayout.Label("Volume: ", AudioStreamSupport.UX.guiStyleLabelNormal, GUILayout.MaxWidth(Screen.width / 4));
                @as.volume = GUILayout.HorizontalSlider(@as.volume, 0f, 1f, GUILayout.MaxWidth(Screen.width / 2));
                GUILayout.Label(Mathf.RoundToInt(@as.volume * 100f) + " %", AudioStreamSupport.UX.guiStyleLabelNormal, GUILayout.MaxWidth(Screen.width / 4));
                GUILayout.EndHorizontal();

                GUILayout.Space(10);

                GUILayout.Label("==== Network", AudioStreamSupport.UX.guiStyleLabelNormal);

                GUILayout.Label(string.Format("Receiving on: {0},{1}:{2}", AudioStreamNetwork.localhost, this.audioStreamOscClient.localIP, this.audioStreamOscClient.connectPort), AudioStreamSupport.UX.guiStyleLabelNormal);
                GUILayout.Label(string.Format("Server sample rate: {0}, channels: {1}", this.audioStreamOscClient.serverPayload.samplerate, this.audioStreamOscClient.serverPayload.channels), AudioStreamSupport.UX.guiStyleLabelNormal);
                GUILayout.Label(string.Format("Network queue size: {0}", this.audioStreamOscClient.networkQueueSize), AudioStreamSupport.UX.guiStyleLabelNormal);

                GUILayout.BeginHorizontal();
                GUILayout.Label("Client thread sleep: ", AudioStreamSupport.UX.guiStyleLabelNormal, GUILayout.MaxWidth(Screen.width / 4));
                this.audioStreamOscClient.networkThreadSleep = (int)GUILayout.HorizontalSlider(audioStreamOscClient.networkThreadSleep, 1, 20, GUILayout.MaxWidth(Screen.width / 2));
                GUILayout.Label(this.audioStreamOscClient.networkThreadSleep.ToString().PadLeft(2, '0') + " ms", AudioStreamSupport.UX.guiStyleLabelNormal, GUILayout.MaxWidth(Screen.width / 4));
                GUILayout.EndHorizontal();

                GUILayout.Space(10);

                GUILayout.Label("==== Status", AudioStreamSupport.UX.guiStyleLabelNormal);

                GUILayout.Label(string.Format("State = {0}"
                    , this.audioStreamOscClient.decoderRunning ? "Playing" : "Stopped"
                    )
                    , AudioStreamSupport.UX.guiStyleLabelNormal
                    );

                GUILayout.BeginHorizontal();

                GUILayout.Label(string.Format("Audio buffer size: {0} / available: {1}", this.audioStreamOscClient.dspBufferSize, this.audioStreamOscClient.capturedAudioSamples), AudioStreamSupport.UX.guiStyleLabelNormal, GUILayout.MaxWidth(Screen.width / 2));

                var r = Mathf.CeilToInt(((float)this.audioStreamOscClient.capturedAudioSamples / (float)this.audioStreamOscClient.dspBufferSize) * 10f);
                var c = Mathf.Min(r, 10);

                GUI.color = this.audioStreamOscClient.capturedAudioFrame ? Color.Lerp(Color.red, Color.green, c / 10f) : Color.red;

                this.gauge.Length = 0;
                for (int i = 0; i < c; ++i) this.gauge.Append("#");
                GUILayout.Label(this.gauge.ToString(), AudioStreamSupport.UX.guiStyleLabelNormal, GUILayout.MaxWidth(Screen.width / 2));

                GUILayout.EndHorizontal();

                GUI.color = Color.white;

                GUILayout.Space(20);
                
                if (GUILayout.Button("Disconnect", AudioStreamSupport.UX.guiStyleButtonNormal))
                    this.audioStreamOscClient.StopClient();
            }

            GUILayout.Space(40);

            GUILayout.EndScrollView();
        }
    }
}