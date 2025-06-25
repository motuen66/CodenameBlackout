using UnityEngine;

public class AudioManager : MonoBehaviour
{
    public static AudioManager Instance { get; private set; }
    public AudioSource footstepSource;
    public AudioSource sfxSource;
    public AudioSource musicSource;
    public AudioClip explosionClip;
    public AudioClip fuseBombClip;
    public AudioClip pickItemClip;
    public AudioClip footstepClip;
    public AudioClip welcomeClip;
    public AudioClip backgroundClip;
    public AudioClip loseClip;
    public AudioClip winClip;

    private void Awake()
    {
        if (Instance == null)
        {
            Instance = this;
            DontDestroyOnLoad(transform.root.gameObject); 
        }
        else
        {
            Destroy(gameObject);
        }
    }

    public void PlayLoseSound()
    {
        Debug.Log("PlayLoseSound called");

        if (loseClip == null)
        {
            Debug.LogError("Lose clip is not assigned!");
            return;
        }

        if (musicSource == null)
        {
            Debug.LogError("Music source is null!");
            return;
        }

        musicSource.Stop(); // Dừng nhạc nền
        sfxSource.Stop(); // Dừng âm thanh hiệu ứng
        musicSource.clip = loseClip;
        musicSource.loop = false;
        musicSource.volume = 1f;
        musicSource.Play();
    }
    public void PlayWinSound()
    {
        Debug.Log("PlayWinSound called");

        if (loseClip == null)
        {
            Debug.LogError("Win clip is not assigned!");
            return;
        }

        if (musicSource == null)
        {
            Debug.LogError("Music source is null!");
            return;
        }

        musicSource.Stop(); // Dừng nhạc nền
        sfxSource.Stop(); // Dừng âm thanh hiệu ứng
        musicSource.clip = loseClip;
        musicSource.clip = winClip;
        musicSource.loop = false;
        musicSource.volume = 1f;
        musicSource.Play();
    }

    public void PlayExplosionSound()
    {
        sfxSource.PlayOneShot(explosionClip, 0.7f);
        Debug.Log("Playing explosion sound");
        
    }
    public void PlayWelcomeSound()
    {
        if (musicSource == null || welcomeClip == null) return;

        musicSource.Stop();
        musicSource.clip = welcomeClip;
        musicSource.loop = false;
        musicSource.Play();
    }

    public void PlayBackgroundMusic()
    {
        if (backgroundClip != null)
        {
            musicSource.clip = backgroundClip;
            musicSource.loop = true;
            musicSource.volume = 0.3f;
            musicSource.Play();
        }
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
            footstepSource.volume = 1f;
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
