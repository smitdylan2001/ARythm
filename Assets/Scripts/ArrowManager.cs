using Melanchall.DryWetMidi.MusicTheory;
using System.Collections;
using System.Collections.Generic;
using System.Drawing;
using UnityEngine;

public class ArrowManager : MonoBehaviour
{
    public static ArrowManager Instance { get; private set; }

    public GameObject ArrowPrefab;

    private List<RectTransform> arrows = new List<RectTransform>();
    private List<Note> notes = new List<Note>();
    private Dictionary<Note, RectTransform> noteDict = new Dictionary<Note, RectTransform>();
    private Camera mainCam;

    private void Awake()
    {
        Instance = this;
    }

    private void Start()
    {
        mainCam = Camera.main;
    }

    void Update()
    {
        UpdatePositions();
    }

    public void AddArrow(Note note)
    {
        RectTransform rt = Instantiate(ArrowPrefab, this.transform).GetComponent<RectTransform>();
        arrows.Add(rt);
        notes.Add(note);

        noteDict.Add(note, rt);

        UpdatePositions();
    }

    public void RemoveArrow(Note note)
    {
        notes.Remove(note);
        RectTransform rt = noteDict[note];
        arrows.Remove(rt);

        noteDict.Remove(note);

        Destroy(rt.gameObject);
    }

    private void UpdatePositions()
    {
        Vector2 camRect = new Vector2(mainCam.pixelWidth, mainCam.pixelHeight);
        Vector2 camCenter = camRect / 2;
        for (int i = 0; i < arrows.Count; i++)
        {
            Note note = notes[i];
            Vector3 dir2 = notes[i].transform.position - mainCam.transform.position;
            bool shouldHide = Vector3.Dot(mainCam.transform.forward, dir2.normalized) > 0.9f;
            RectTransform arrow = arrows[i];
            if (shouldHide)
            {
                arrow.gameObject.SetActive(false);
                continue;
            }

            arrow.gameObject.SetActive(true);
            Vector3 targetScreenPointScreen = mainCam.WorldToScreenPoint(note.transform.position);
            Vector2 targetScreenPoint = new Vector2(targetScreenPointScreen.x, targetScreenPointScreen.y);
            Vector2 dir = (targetScreenPoint - camCenter).normalized;

            float angle = Mathf.Atan2(dir.y, dir.x);

            arrows[i].position = camCenter + new Vector2(Mathf.Cos(angle) * camCenter.x * 0.9f, Mathf.Sin(angle) * camCenter.y * 0.9f);
            arrows[i].rotation = Quaternion.Euler( 0,0,angle * Mathf.Rad2Deg );
            //Vector3 viewport = mainCam.WorldToViewportPoint(note.transform.position);
            //bool inCameraFrustum = Is01(viewport.x) && Is01(viewport.y);
            //arrows[i].gameObject.SetActive(!inCameraFrustum);
            //if (inCameraFrustum) return;

            //Vector2 newPos = new Vector2(camRect.x * Mathf.Clamp01(viewport.x), camRect.y * Mathf.Clamp01(viewport.y));

        }
    }

    public bool PointInCameraView(Vector3 point)
    {
        Vector3 viewport = mainCam.WorldToViewportPoint(point);
        bool inCameraFrustum = Is01(viewport.x) && Is01(viewport.y);
        bool inFrontOfCamera = viewport.z > 0;

        return inCameraFrustum && inFrontOfCamera;
    }

    public bool Is01(float a)
    {
        return a > 0 && a < 1;
    }
}
