using System.Collections;
using UnityEngine;
using UnityEngine.Events;

/// <summary>
/// For the second scene: image1 fades in, holds, then crossfades into image2.
/// Put this on any object and drag the two images in. Plays automatically.
/// Every duration can be edited in the Inspector.
/// </summary>
public class SecondSceneSequence : MonoBehaviour
{
    [Header("Images")]
    public SpriteRenderer image1;
    public SpriteRenderer image2;

    [Header("Timing (seconds)")]
    public float startDelay = 1f;          // wait before image1 starts fading in
    public float fadeIn = 2f;              // image1 fading in
    public float hold = 2f;                // image1 fully visible before the crossfade
    public float crossfade = 1.5f;         // image1 fades out while image2 fades in

    [Header("When it's done")]
    public UnityEvent onFinished;          // fires when image2 is fully visible

    void Start()
    {
        SetAlpha(image1, 0);
        SetAlpha(image2, 0);
        StartCoroutine(Play());
    }

    IEnumerator Play()
    {
        yield return new WaitForSeconds(startDelay);

        yield return Fade(image1, 0, 1, fadeIn);
        yield return new WaitForSeconds(hold);

        StartCoroutine(Fade(image1, 1, 0, crossfade));
        yield return Fade(image2, 0, 1, crossfade);

        onFinished.Invoke();
    }

    IEnumerator Fade(SpriteRenderer sr, float from, float to, float duration)
    {
        for (float t = 0; t < duration; t += Time.deltaTime)
        {
            SetAlpha(sr, Mathf.Lerp(from, to, t / duration));
            yield return null;
        }
        SetAlpha(sr, to);
    }

    static void SetAlpha(SpriteRenderer sr, float a)
    {
        if (sr == null) return;
        Color c = sr.color;
        c.a = a;
        sr.color = c;
    }
}
