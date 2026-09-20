using System.Collections;
using TMPro;
using UnityEngine;
using UnityEngine.InputSystem;

/// <summary>
/// Goes on the button sprite. Clicking the button fades the book in and the button out;
/// clicking anywhere off the open book fades it back the other way. Clicks that land on the
/// book itself, or on anything written on it, leave it alone.
///
/// Works off the sprites' own bounds, so nothing needs a collider.
/// </summary>
public class BookToggle : MonoBehaviour
{
    [Tooltip("The book sprite. Leave it switched off in the scene so it starts closed.")]
    public GameObject book;
    [Tooltip("Seconds the book takes to fade in or out. The button fades the opposite way over the same time.")]
    public float fadeTime = 0.2f;

    [Header("Sound")]
    [Tooltip("Plays as the book opens.")]
    public AudioClip unravel;
    [Tooltip("Plays as the book closes.")]
    public AudioClip ravel;
    [Range(0f, 1f)] public float volume = 0.7f;

    [Header("While the book is open")]
    [Tooltip("Switched off so clicks on the book don't reach the puzzle behind it. Drag CodePuzzle in.")]
    public MonoBehaviour[] pauseWhileOpen;

    SpriteRenderer button, page;
    SpriteRenderer[] bookSprites;
    TMP_Text[] bookTexts;
    Color[] spriteBase, textBase;
    Color buttonBase;

    AudioSource audioSource;
    Camera cam;
    Coroutine fading;
    float bookAlpha;               // 0 = shut, 1 = open. The button runs the other way.
    bool open;

    void Awake()
    {
        button = GetComponent<SpriteRenderer>();
        if (button != null) buttonBase = button.color;

        cam = Camera.main;

        audioSource = GetComponent<AudioSource>();
        if (audioSource == null) audioSource = gameObject.AddComponent<AudioSource>();
        audioSource.playOnAwake = false;

        if (book != null)
        {
            page = book.GetComponent<SpriteRenderer>();

            // Inactive children count, since the book is usually left switched off in the scene.
            bookSprites = book.GetComponentsInChildren<SpriteRenderer>(true);
            bookTexts = book.GetComponentsInChildren<TMP_Text>(true);

            spriteBase = new Color[bookSprites.Length];
            textBase = new Color[bookTexts.Length];
            for (int i = 0; i < bookSprites.Length; i++) spriteBase[i] = bookSprites[i].color;
            for (int i = 0; i < bookTexts.Length; i++) textBase[i] = bookTexts[i].color;
        }

        if (page == null)
            Debug.LogWarning("BookToggle: the book needs a SpriteRenderer, otherwise there's no way to tell " +
                             "a click on it from a click off it.", this);

        open = book != null && book.activeSelf;   // match whatever state the book is left in
        bookAlpha = open ? 1f : 0f;
        Apply();
        Pause(open);
    }

    void Update()
    {
        if (book == null || page == null) return;
        if (Mouse.current == null || !Mouse.current.leftButton.wasPressedThisFrame) return;

        if (cam == null) { cam = Camera.main; if (cam == null) return; }

        Vector2 screen = Mouse.current.position.ReadValue();
        float depth = Mathf.Abs(transform.position.z - cam.transform.position.z);
        Vector3 point = cam.ScreenToWorldPoint(new Vector3(screen.x, screen.y, depth));

        if (!open)
        {
            if (Inside(button, point)) SetOpen(true);
        }
        else if (!Inside(page, point))
        {
            SetOpen(false);
        }
    }

    void SetOpen(bool wanted)
    {
        if (open == wanted) return;
        open = wanted;

        AudioClip clip = wanted ? unravel : ravel;
        if (clip != null) audioSource.PlayOneShot(clip, volume);

        if (fading != null) StopCoroutine(fading);
        fading = StartCoroutine(Fade());
    }

    /// <summary>Carries on from wherever the last fade got to, so a quick double-click doesn't jump.</summary>
    IEnumerator Fade()
    {
        if (open)
        {
            book.SetActive(true);
            Pause(true);
        }

        float from = bookAlpha;
        float to = open ? 1f : 0f;

        for (float t = 0f; t < fadeTime; t += Time.deltaTime)
        {
            bookAlpha = Mathf.Lerp(from, to, t / fadeTime);
            Apply();
            yield return null;
        }

        bookAlpha = to;
        Apply();

        if (!open)
        {
            book.SetActive(false);
            Pause(false);       // held back until the book is properly gone
        }

        fading = null;
    }

    /// <summary>The book at the current alpha, the button at the inverse of it.</summary>
    void Apply()
    {
        for (int i = 0; i < (bookSprites?.Length ?? 0); i++)
            bookSprites[i].color = WithAlpha(spriteBase[i], bookAlpha);

        for (int i = 0; i < (bookTexts?.Length ?? 0); i++)
            bookTexts[i].color = WithAlpha(textBase[i], bookAlpha);

        if (button != null) button.color = WithAlpha(buttonBase, 1f - bookAlpha);
    }

    static Color WithAlpha(Color c, float fade)
    {
        c.a *= fade;
        return c;
    }

    void Pause(bool paused)
    {
        if (pauseWhileOpen == null) return;

        foreach (MonoBehaviour behaviour in pauseWhileOpen)
            if (behaviour != null) behaviour.enabled = !paused;
    }

    static bool Inside(SpriteRenderer sr, Vector2 point)
    {
        if (sr == null) return false;

        Bounds b = sr.bounds;   // world space, so the z of the click doesn't come into it
        return point.x >= b.min.x && point.x <= b.max.x &&
               point.y >= b.min.y && point.y <= b.max.y;
    }
}
