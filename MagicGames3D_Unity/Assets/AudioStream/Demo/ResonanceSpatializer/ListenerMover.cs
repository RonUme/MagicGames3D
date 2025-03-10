﻿// (c) 2016-2023 Martin Cvengros. All rights reserved. Redistribution of source code without permission not allowed.

using UnityEngine;

public class ListenerMover : MonoBehaviour
{
    public float speed = .5f;

    Vector3 startPosition;

    void Start()
    {
        this.startPosition = this.transform.position;
    }

    void Update()
    {
        var up = Input.GetAxis("Fire3");    // left shift
        var down = -Input.GetAxis("Fire1"); // left ctrl

        var translation = new Vector3(Input.GetAxis("Horizontal"), up + down, Input.GetAxis("Vertical")) * this.speed;

        this.transform.Translate(translation);

        if (Input.GetKeyDown(KeyCode.R))
            this.transform.position = this.startPosition;
    }
}