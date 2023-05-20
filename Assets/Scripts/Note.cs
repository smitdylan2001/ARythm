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
        dying
    }

    [NonSerialized] public float startDuration = 3.42857142857f, moveDuration = 3.42857142857f;
    [NonSerialized] State state = State.spawning;
    double timeInstantiated;
    public float assignedTime;

    [NonSerialized] public Vector3 lanePosition, origin, startPos, hitPos;
    float timer;

    public void HandleNoteHit()
    {
        float distance = Vector3.Distance(transform.position, origin) - 1;
        if (distance < 0.1f)
        {
            Debug.Log("PERFECT");
            ScoreManager.Instance.Hit(Mathf.RoundToInt(Mathf.Clamp((1/ distance) * 5, 0, 100)));
        }
        else if (distance < 0.2f)
        {
            Debug.Log("Good");
            ScoreManager.Instance.Hit(Mathf.RoundToInt(Mathf.Clamp((1/ distance) * 5, 0, 100)));
        }
        else
        {
            ScoreManager.Instance.Hit(Mathf.RoundToInt(-distance*10));
        }
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

        var rot = transform.rotation.eulerAngles;
        transform.LookAt(origin);
        transform.rotation = Quaternion.Euler(new Vector3(rot.x, transform.rotation.eulerAngles.y, rot.z));
        hitPos = (lanePosition - origin).normalized;
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
                transform.position = Vector3.LerpUnclamped(lanePosition, origin, timer);
                timer += Time.deltaTime / moveDuration;

                //Miss
                if (Vector3.Distance(origin, transform.position) < 0.1f)
                {
                    ScoreManager.Instance.Hit(-5);
                    RemoveNote();
                }
                break;
            
        }
    }
}
