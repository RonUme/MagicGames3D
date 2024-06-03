// (c) 2016-2023 Martin Cvengros. All rights reserved. Redistribution of source code without permission not allowed.
// uses FMOD by Firelight Technologies Pty Ltd

using System.Linq;
using UnityEngine;
using UnityEngine.Audio;

[ExecuteInEditMode()]
public class InputDeviceUnityMixerDemo : MonoBehaviour
{
    /// <summary>
    /// Mixer
    /// </summary>
    public AudioMixer audioMixer;

    void OnGUI()
    {
        GUILayout.Label("", AudioStreamSupport.UX.guiStyleLabelSmall); // statusbar on mobile overlay
        GUILayout.Label("", AudioStreamSupport.UX.guiStyleLabelSmall);
        // remove dependency on the rest of AudioStream completely to allow standalone usage for the plugin
        var versionString = "AudioStream © 2016-2023 Martin Cvengros, uses FMOD by Firelight Technologies Pty Ltd";
        GUILayout.Label(versionString, AudioStreamSupport.UX.guiStyleLabelMiddle);

        GUILayout.Label("This scene uses Windows and macOS (*) AudioStreamInputDevice audio mixer effect to record from specified system audio input and play it on a mixer group", AudioStreamSupport.UX.guiStyleLabelNormal);
        GUILayout.Label("(post effect) Volume of the input and Input Device ID effect parameters are exposed for scripting", AudioStreamSupport.UX.guiStyleLabelNormal);

        GUILayout.Label("(*) see documentation for details and current limitations\r\nit also means this scene won't work on other platforms currently", AudioStreamSupport.UX.guiStyleLabelNormal);

        if (Application.platform == RuntimePlatform.OSXEditor || Application.platform == RuntimePlatform.OSXPlayer)
        {
            GUILayout.Label(":!: AudioStreamInputDevice effect on macOS :!: please note that this efffect currently doesn't work properly on macOS (but is included in demo/mixer asset)");
        }

        if (this.audioMixer.GetFloat("Volume", out var volume))
        {
            using (new GUILayout.HorizontalScope())
            {
                GUILayout.Label("Input Device Volume: ");
                volume = GUILayout.HorizontalSlider(volume, -80, 0);
                GUILayout.Label(string.Format("{0:F2} dB", volume));
            }
            this.audioMixer.SetFloat("Volume", volume);
        }

        if (this.audioMixer.GetFloat("InputDeviceID", out var mixerInput))
        {
            var selectedInput = mixerInput + 1;
            using (new GUILayout.HorizontalScope())
            {
                GUILayout.Label("Input Device ID: ");
                selectedInput = GUILayout.SelectionGrid((int)selectedInput, Enumerable.Range(-1, 6).Select(s => s.ToString()).ToArray(), 6);
                GUILayout.Label("(-1 to turn off/stop recording)");
            }
            this.audioMixer.SetFloat("InputDeviceID", (int)selectedInput - 1);
        }
   }
}