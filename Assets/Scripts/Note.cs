using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class Note : MonoBehaviour
{
    public enum State
    {
        spawning,
        moving,

    }

    [NonSerialized] public float startDuration = 3.42857142857f, moveDuration = 3.42857142857f;
    [NonSerialized] State state = State.spawning;
    double timeInstantiated;
    public float assignedTime;

    [NonSerialized] public Vector3 lanePosition, origin, startPos;
    float timer;

    public void HandleNoteHit()
    {
        float distance = Vector3.Distance(transform.position, origin) - 1;
        if (distance < 0.1f)
        {
            Debug.Log("PERFECT");
            ScoreManager.Instance.Hit(3);
        }
        else if (distance < 0.2f)
        {
            Debug.Log("Good");
            ScoreManager.Instance.Hit(1);
        }
        else
        {
            Debug.Log("Yikes");
            return;
        }
        ScoreManager.Instance.Hit(Mathf.RoundToInt(Mathf.Clamp((1/ distance) * 5, 0, 100)));
        RemoveNote();
    }

    void RemoveNote()
    {
        ArrowManager.Instance.RemoveArrow(this);
        Destroy(gameObject);

    }

    // Start is called before the first frame update
    void Start()
    {
        timer = 0;
        startPos = transform.position;
        ArrowManager.Instance.AddArrow(this);
    }

    // Update is called once per frame
    void Update()
    {
        switch (state)
        {
            case State.spawning:
                transform.position = Vector3.Lerp(startPos, lanePosition, timer);
                timer += Time.deltaTime / startDuration;

                if(timer >= 1)
                {
                    state = State.moving;
                    timer = 0;
                }
                break;
            case State.moving:
                transform.position = Vector3.Lerp(lanePosition, origin, timer);
                timer += Time.deltaTime / moveDuration;

                //Miss
                if (timer >= 1)
                {
                    RemoveNote();
                }
                break;
        }
    }
}
