using System.Collections;
using System.Collections.Generic;
using Unity.Mathematics;
using UnityEngine;

public class GameManager : MonoBehaviour
{
    [SerializeField] GameObject gameWorldReference;

    GameObject gameWorld;
    GameObject mainCam;
    Vector3 startPosition;

    // Start is called before the first frame update
    void Start()
    {
        mainCam = Camera.main.gameObject;
    }

    public void StartGame()
    {
        mainCam.transform.GetPositionAndRotation(out startPosition, out Quaternion rotation);
        startPosition -= Vector3.up;
        Vector3 rot = gameWorldReference.transform.rotation.eulerAngles;
        rot.y = rotation.eulerAngles.y;
        gameWorld = Instantiate(gameWorldReference, startPosition, Quaternion.Euler(rot));
    }

    public void Reset()
    {
        mainCam.transform.GetPositionAndRotation(out startPosition, out Quaternion rotation);
        startPosition -= Vector3.up;
        Vector3 rot = gameWorldReference.transform.rotation.eulerAngles;
        rot.y = rotation.eulerAngles.y;
        gameWorld.transform.SetPositionAndRotation(startPosition, Quaternion.Euler(rot));
    }
}