﻿// (c) 2016-2023 Martin Cvengros. All rights reserved. Redistribution of source code without permission not allowed.

using AudioStream;
using System.Collections;
using UnityEngine;
/// <summary>
/// 
/// </summary>
[ExecuteInEditMode()]
public class AudioSourceChannelsSeparationDemo : MonoBehaviour
{
    public AudioSourceChannelsSeparation audioSourceChannelsSeparation;
    public AudioListener listener;

    IEnumerator Start()
    {
        // w8 until channels are created and spread them evenly on arc in front of listener/camera
        while (this.audioSourceChannelsSeparation.audioSourceChannels == null
            || this.audioSourceChannelsSeparation.audioSourceChannels.Length < 1)
            yield return null;

        // from right to left
        var a = 0f;
        var radius = 70f;
        var step = Mathf.PI / (float)(this.audioSourceChannelsSeparation.audioSourceChannels.Length - 1);

        for (var i = 0; i < this.audioSourceChannelsSeparation.audioSourceChannels.Length; ++i)
        {
            var asc = this.audioSourceChannelsSeparation.audioSourceChannels[i];
            asc.transform.position = new Vector3(Mathf.Cos(a) * radius
                , 0
                , Mathf.Sin(a) * radius
                );
            // enlarge graphics a little
            asc.transform.localScale *= 5;
            a += step;
        }
    }

    void OnGUI()
    {
        AudioStreamDemoSupport.OnGUI_GUIHeader("");

        GUILayout.Label("This scene plays Unity imported 6 channels AudioClip and instantiates single channel AudioSource prefab *per current Unity output channel* in the scene with audio from it\r\n" +
            "- original AudioClip is played and processed by Unity as needed for current output before splitting -");
        GUILayout.Label("Note: Normally Unity plays multichannle files automatically - this scene just demostrates access to output channels using Unity audio scripting");

        GUILayout.Space(5);
        GUILayout.Label(">> W/S/A/D/Arrows to move || Left Shift/Ctrl to move up/down || Mouse to look || 'R' to reset listener position <<");
    }
}