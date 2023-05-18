using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using Niantic.ARDK.Utilities.Input.Legacy;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.EnhancedTouch;

public class NoteInteraction : MonoBehaviour
{
    [SerializeField] LayerMask noteMask;
    Camera mainCam;


    void OnEnable()
    {
        TouchSimulation.Enable();
        EnhancedTouchSupport.Enable();
        UnityEngine.InputSystem.EnhancedTouch.Touch.onFingerDown += get_touch_details;
    }
    void get_touch_details(Finger fin)
    {
        HandleTouch(fin.screenPosition);
    }

    private void Start()
    {
        mainCam = Camera.main;
    }

    public void HandleTouch(Vector2 screenPos)
    {
        Ray ray = mainCam.ScreenPointToRay(screenPos);

        RaycastHit hit;
        if (!Physics.Raycast(ray, out hit, 10f, noteMask)) return;

        hit.transform.GetComponent<Note>().HandleNoteHit();
    }
}
