using UnityEngine;

public class AudioManager : MonoBehaviour
{
    public static AudioManager Instance { get; private set; }
    public AudioSource footstepSource;
    public AudioSource sfxSource;
    public AudioClip explosionClip;
    public AudioClip fuseBombClip;
    public AudioClip pickItemClip;
    public AudioClip footstepClip;
   

    public bool isExplosionSoundEnabled = false;

    private void Awake()
    {
        if (Instance == null)
        {
            Instance = this;
            DontDestroyOnLoad(gameObject);
        }
        else
        {
            Destroy(gameObject);
        }
    }

    public void PlayExplosionSound()
    {
        sfxSource.PlayOneShot(explosionClip, 0.7f);
    }


    public void PlayFuseSound()
    {
        if (fuseBombClip != null)
            sfxSource.PlayOneShot(fuseBombClip, 0.5f);
    }

    public void PlayPickItemSound()
    {
        if (pickItemClip != null)
            sfxSource.PlayOneShot(pickItemClip);
    }

    public void PlayFootstep()
    {
        if (!footstepSource.isPlaying)
        {
            footstepSource.clip = footstepClip;
            footstepSource.loop = true;
            footstepSource.volume = 0.5f;
            footstepSource.Play();
        }
    }

    public void StopFootstep()
    {
        if (footstepSource.isPlaying)
        {
            footstepSource.Stop();
            footstepSource.loop = false;
        }
    }
}
