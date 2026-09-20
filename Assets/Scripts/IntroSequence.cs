using System.Collections;
using UnityEngine;
using UnityEngine.Events;

/// <summary>
/// Put this on any object in the intro scene and drag the four images into the slots.
/// Waits until Begin() is called (IntroStart does this on click). Every duration below can be edited in the Inspector.
/// Sequence: Scene1 fades in -> crossfades to Scene2 -> Scene2 disappears, Tomato appears
///           -> Apple flickers in and out over Tomato -> everything disappears.
/// </summary>
public class IntroSequence : MonoBehaviour
{
    [Header("Images")]
    public SpriteRenderer scene1;
    public SpriteRenderer scene2;
    public SpriteRenderer tomato;
    public SpriteRenderer apple;

    [Header("Timing (seconds)")]
    public float startDelay = 2f;          // wait before Scene1 starts fading in
    public float scene1FadeIn = 1f;
    public float scene1Hold = 2f;          // Scene1 fully visible before it fades into Scene2
    public float crossfade = 1f;           // Scene1 -> Scene2
    public float scene2Hold = 2f;          // Scene2 visible before it disappears
    public float tomatoHold = 3f;          // Tomato alone before the flicker starts

    public float tomatoFadeIn = 1f;        // Scene2 fades out while Tomato fades in over this time

    [Header("Flicker")]
    public float flickerDuration = 6f;     // how long the whole flicker lasts
    public float maxPause = 1.2f;          // longest pause between bursts (at the start)
    public int maxBurstSize = 6;           // most flashes in one burst (near the end)
    public float slowFlash = 0.12f;        // length of one flash at the start
    public float fastFlash = 0.03f;        // length of one flash at the end (rapid)
    public float appleHold = 2f;           // Apple stays on screen after the flicker
    public float appleFadeOut = 2f;        // then the Apple fades out and nothing is left

    [Header("When it's done")]
    public UnityEvent onFinished;          // optional extra actions

    void Start()
    {
        SetAlpha(scene1, 0);
        SetAlpha(scene2, 0);
        SetAlpha(tomato, 0);
        SetAlpha(apple, 0);
    }

    public void Begin()
    {
        StartCoroutine(Play());
    }

    IEnumerator Play()
    {
        yield return new WaitForSeconds(startDelay);

        yield return Fade(scene1, 0, 1, scene1FadeIn);
        yield return new WaitForSeconds(scene1Hold);

        // Crossfade Scene1 -> Scene2
        StartCoroutine(Fade(scene1, 1, 0, crossfade));
        yield return Fade(scene2, 0, 1, crossfade);
        yield return new WaitForSeconds(scene2Hold);

        // Scene2 disappears, Tomato fades in
        StartCoroutine(Fade(scene2, 1, 0, tomatoFadeIn));
        yield return Fade(tomato, 0, 1, tomatoFadeIn);
        yield return new WaitForSeconds(tomatoHold);

        // Horror-movie flicker: Apple flashes over Tomato in random bursts.
        // Early on: a lone flick, a long pause. Later: bigger bursts, shorter pauses. At the end: nonstop and rapid.
        float startTime = Time.time;
        while (Time.time - startTime < flickerDuration)
        {
            float progress = (Time.time - startTime) / flickerDuration;
            int burst = Random.Range(1, 2 + Mathf.RoundToInt(progress * (maxBurstSize - 1)));

            for (int i = 0; i < burst && Time.time - startTime < flickerDuration; i++)
            {
                progress = (Time.time - startTime) / flickerDuration;
                float flash = Mathf.Lerp(slowFlash, fastFlash, progress) * Random.Range(0.6f, 1.4f);

                SetAlpha(apple, 1);
                SetAlpha(tomato, 0);
                yield return new WaitForSeconds(flash);

                SetAlpha(apple, 0);
                SetAlpha(tomato, 1);
                yield return new WaitForSeconds(flash * Random.Range(0.5f, 1.5f));
            }

            progress = (Time.time - startTime) / flickerDuration;
            float pause = Mathf.Lerp(maxPause, 0f, Mathf.Clamp01(progress * 1.25f)) * Random.Range(0.5f, 1.5f);
            yield return new WaitForSeconds(pause);
        }

        // The flicker ends on the Apple, which holds for a moment
        SetAlpha(apple, 1);
        SetAlpha(tomato, 0);
        yield return new WaitForSeconds(appleHold);

        // Apple fades out, leaving nothing on screen
        yield return Fade(apple, 1, 0, appleFadeOut);
        SetAlpha(apple, 0);
        SetAlpha(tomato, 0);
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
