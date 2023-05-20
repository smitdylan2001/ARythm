using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.EnhancedTouch;

public class NoteInteraction : MonoBehaviour
{
    [SerializeField] LayerMask noteMask;
    [SerializeField] GameObject destroyVFX;
    Camera mainCam;
    RaycastHit hit;


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

        if (!Physics.Raycast(ray, out hit, 10f, noteMask)) return;

        hit.transform.GetComponent<Note>().HandleNoteHit();

        var go = Instantiate(destroyVFX, hit.transform);
        go.transform.SetLocalPositionAndRotation(Vector3.zero, Quaternion.Euler(Vector3.zero));
        go.transform.SetParent(null);
        go.transform.localScale = Vector3.one;
    }
}
