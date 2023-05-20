using System.Collections;
using System.Collections.Generic;
using Unity.Mathematics;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.SceneManagement;

public class GameManager : MonoBehaviour
{
    public static GameManager Instance { get; private set; }

    [SerializeField] GameObject gameWorldReference;
    [SerializeField ] GameObject EndScreen;
    [SerializeField] UnityEvent OnEndGame;
    GameObject gameWorld;
    GameObject mainCam;
    Vector3 startPosition;

    private void Awake()
    {
        Instance = this;
    }

    // Start is called before the first frame update
    void Start()
    {
        mainCam = Camera.main.gameObject;
    }

    public void StartGame()
    {
        mainCam.transform.GetPositionAndRotation(out startPosition, out Quaternion rotation);
        startPosition -= Vector3.up * 1.3f;
        Vector3 rot = gameWorldReference.transform.rotation.eulerAngles;
        rot.y = rotation.eulerAngles.y;
        gameWorld = Instantiate(gameWorldReference, startPosition, Quaternion.Euler(rot));
        gameWorld.transform.position += gameWorld.transform.forward * 0.7f;
    }

    public void Reset()
    {
        mainCam.transform.GetPositionAndRotation(out startPosition, out Quaternion rotation);
        startPosition -= Vector3.up * 1.3f;
        Vector3 rot = gameWorldReference.transform.rotation.eulerAngles;
        rot.y = rotation.eulerAngles.y;
        gameWorld.transform.SetPositionAndRotation(startPosition, Quaternion.Euler(rot));
        gameWorld.transform.position += gameWorld.transform.forward * 0.7f;
    }


    public void EndGame()
    {
        EndScreen.SetActive(true);
        OnEndGame.Invoke();
    }

    public void RestartGame()
    {
        SceneManager.LoadScene(SceneManager.GetActiveScene().buildIndex);
    }
}