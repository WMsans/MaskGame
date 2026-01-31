using UnityEngine;

public class SoundManager : MonoBehaviour
{

    public static SoundManager Instance;
    public AudioSource sculptSource;

    void Awake()
    {
        if (Instance == null) 
        {
            Instance = this;
        }
        else 
        {
            Destroy(gameObject);
        }
    }

    public void StartSculptSound()
    {
        sculptSource.loop = true;

        if (!sculptSource.isPlaying) 
        {
            sculptSource.Play();
        }
    }

    public void StopSculptSound()
    {
        sculptSource.loop = false;
    }
}