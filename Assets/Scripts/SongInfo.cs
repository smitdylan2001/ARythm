using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

[System.Serializable]
public struct NoteInfo
{
    public bool HasNote;
    public int ExtraDelayBeats;
    public int Lane;
    public Color Color;

    public float FallSpeed, MoveSpeed;
}

[CreateAssetMenu(fileName = "Song", menuName = "ScriptableObjects/SongInfo", order = 1)]
public class SongInfo : ScriptableObject
{
    public AudioClip Song;
    public string MidiPath;
    public float Bpm;
    public int toSkip;
    public float SecondsPerBeat { get { return 60/Bpm; } }
    public float SongDuration { get { return Song.length; } }

    public NoteInfo[] Notes;
}