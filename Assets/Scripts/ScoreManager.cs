using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SocialPlatforms.Impl;

public class ScoreManager : MonoBehaviour
{
    public static ScoreManager Instance { get; private set; }
    public AudioSource hitSFX;
    public AudioSource missSFX;
    public TMPro.TMP_Text scoreText;
    public int score;

    private void Awake()
    {
        Instance = this;
        score = 0;
        scoreText.text = score.ToString();
    }

    public void Hit(int amount)
    {
        score += amount;
        score = Mathf.Clamp(score, 0, int.MaxValue);
        scoreText.text = score.ToString();
        //Update text;

        if(amount > 0) hitSFX.Play();
        else missSFX.Play();
    }
}
