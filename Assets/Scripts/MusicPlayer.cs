using UnityEngine;

/// <summary>
/// Put this on any object, assign the music clip. Call Play() to start (IntroStart does this on click).
/// The clip loops, fading in at the start of every pass and fading out at the end of it.
/// </summary>
[RequireComponent(typeof(AudioSource))]
public class MusicPlayer : MonoBehaviour
{
    public AudioClip music;
    [Range(0f, 1f)] public float volume = 0.5f;
    public float fadeTime = 3f;        // seconds of fade at the start and end of each loop

    AudioSource source;

    void Awake()
    {
        source = GetComponent<AudioSource>();
        source.clip = music;
        source.loop = true;
        source.playOnAwake = false;
        source.volume = 0f;
    }

    public void Play()
    {
        if (!source.isPlaying) source.Play();
    }

    void Update()
    {
        if (!source.isPlaying || music == null) return;

        float fadeIn = Mathf.Clamp01(source.time / fadeTime);
        float fadeOut = Mathf.Clamp01((music.length - source.time) / fadeTime);
        source.volume = volume * Mathf.Min(fadeIn, fadeOut);
    }
}
