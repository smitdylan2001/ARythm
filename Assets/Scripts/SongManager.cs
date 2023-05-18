using Melanchall.DryWetMidi.Core;
using Melanchall.DryWetMidi.Interaction;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using UnityEngine;
using UnityEngine.Networking;

public class SongManager : MonoBehaviour
{
    public static SongManager Instance { get; private set; }
    public static MidiFile midiFile;
    public int InputDelayMs = 0;
    public float SongDelaySec = 0;
    private string fileLocation;
    public float marginOfError;
    public Lane[] lanes;
    public float noteTime;
    [SerializeField] GameObject Note;
    [SerializeField] AudioSource mainAudioSource;

    Transform[] spawnPoints;
    SongInfo songInfo;
    float timer = 0;

    private void Awake()
    {
        Instance = this;
    }

    private void Start()
    {
        if(!mainAudioSource) mainAudioSource = FindAnyObjectByType<AudioSource>();

        
    }
    private IEnumerator ReadFromWebsite()
    {
        using (UnityWebRequest www = UnityWebRequest.Get(Application.streamingAssetsPath + "/" + fileLocation))
        {
            yield return www.SendWebRequest();

            if (www.result == UnityWebRequest.Result.ConnectionError || www.result == UnityWebRequest.Result.ProtocolError || www.result == UnityWebRequest.Result.DataProcessingError)
            {
                Debug.LogError(www.error);
            }
            else
            {
                byte[] results = www.downloadHandler.data;
                using (var stream = new MemoryStream(results))
                {
                    midiFile = MidiFile.Read(stream);
                    GetDataFromMidi();
                }
            }
        }
    }

    private void ReadFromFile()
    {
        midiFile = MidiFile.Read(Application.streamingAssetsPath + "/" + fileLocation);
        GetDataFromMidi();
    }

    public void GetDataFromMidi()
    {
        var notes = midiFile.GetNotes();
        var array = new Melanchall.DryWetMidi.Interaction.Note[notes.Count];
        notes.CopyTo(array, 0);

        foreach (var lane in lanes) lane.SetTimeStamps(array);

        //Invoke(nameof(StartSong), SongDelaySec);
    }
    public void StartSong(SongInfo song)
    {
        songInfo = song;
        timer = 0;

        StartCoroutine(PlaySong());
    }

    public static double GetAudioSourceTime()
    {
        return (double)Instance.mainAudioSource.timeSamples / Instance.mainAudioSource.clip.frequency;
    }

    private IEnumerator PlaySong()
    {
        fileLocation = songInfo.MidiPath;
        lanes = FindObjectsOfType<Lane>();
        if (Application.platform == RuntimePlatform.Android || Application.streamingAssetsPath.StartsWith("http://") || Application.streamingAssetsPath.StartsWith("https://"))
        {
            yield return ReadFromWebsite();
        }
        else
        {
            ReadFromFile();
        }

        mainAudioSource.clip = songInfo.Song;
        mainAudioSource.Play();

        int note = 0;
        float delay = songInfo.SecondsPerBeat;


        //while (timer < songInfo.SongDuration)
        //{
        //    if (note < songInfo.Notes.Length)
        //    {
        //        NoteInfo thisNote = songInfo.Notes[note];

        //        if (thisNote.HasNote)
        //        {
        //            Vector3 lanePos = spawnPoints[thisNote.Lane].position;
        //            Vector3 spawnPos = lanePos + new Vector3(0, 3, 0);
        //            var noteObject = Instantiate(Note, spawnPos, Quaternion.identity).GetComponent<Note>();
        //            noteObject.startDuration = 1;
        //            noteObject.moveDuration = 1;
        //            noteObject.lanePosition = lanePos;
        //            noteObject.origin = Vector3.zero;
        //        }

        //        float timeIncrease = delay + (delay * thisNote.ExtraDelayBeats);
        //        timer += timeIncrease;
        //        yield return new WaitForSecondsRealtime(timeIncrease);

        //        note++;
        //    }
        //    else
        //    {
        //        timer += Time.deltaTime;
        //        yield return null;
        //    }
        //}

        //end song
    }
}
