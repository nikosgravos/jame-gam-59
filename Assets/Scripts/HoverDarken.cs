using System.Collections;
using System.Collections.Generic;
using TMPro;
using UnityEngine;

public class ClickHealth : MonoBehaviour
{
    public float timeToKill = 20f;   // total seconds of hovering needed, across the whole fight
    public GameObject currentChallenge;
    public GameObject nextChallenge;

    [Header("Decoys at half way")]
    public GameObject decoyPrefab;
    public Transform[] spawnPoints;

    [Header("The message hidden under the paint")]
    [Tooltip("Sits in the picture all along, unreadable, until it turns red at the end. Sprite or text, either works.")]
    public GameObject hiddenMessage;
    public Color messageColor = new Color(0.3764706f, 0.08235294f, 0.05882353f, 1f);   // 60150F
    public float messageFadeIn = 1.5f;
    [Tooltip("Seconds the message sits there, once it's fully faded in, before the next challenge takes over.")]
    public float messageHold = 3f;

    [Header("Flash when dark (so the player can glimpse the target)")]
    public bool flashWhenDark = true;
    [Range(0f, 1f)] public float darkThreshold = 0.25f;   // flashing starts once brightness drops below this
    public float flashInterval = 1.5f;                    // seconds between flashes
    public float flashDuration = 0.15f;                   // how long each flash lasts

    readonly Dictionary<SpriteRenderer, Color> originalColors = new Dictionary<SpriteRenderer, Color>();
    SpriteRenderer[] targetRenderers;
    float hoverTime;
    bool decoysSpawned;
    float brightness = 1f;
    bool flashing;
    float flashTimer;

    void Awake()
    {
        if (currentChallenge == null && transform.parent != null)
            currentChallenge = transform.parent.gameObject;   // default to the frame this object sits in

        targetRenderers = GetComponentsInChildren<SpriteRenderer>();

        if (GetComponent<Collider2D>() == null)
            Debug.LogError("ClickHealth: this object needs a Collider2D (e.g. PolygonCollider2D) to detect the mouse.", this);
    }

    void Update()
    {
        if (!flashWhenDark) return;

        if (brightness >= darkThreshold || hoverTime >= timeToKill)
        {
            if (flashing) { flashing = false; ApplyBrightness(); }
            flashTimer = 0f;
            return;
        }

        flashTimer += Time.deltaTime;
        if (!flashing && flashTimer >= flashInterval)
        {
            flashing = true;
            flashTimer = 0f;
            ApplyBrightness();
        }
        else if (flashing && flashTimer >= flashDuration)
        {
            flashing = false;
            flashTimer = 0f;
            ApplyBrightness();
        }
    }

    // Runs every frame the mouse is over the collider. Leaving just pauses the fade; nothing resets.
    void OnMouseOver()
    {
        if (hoverTime >= timeToKill) return;

        hoverTime = Mathf.Min(hoverTime + Time.deltaTime, timeToKill);

        if (!decoysSpawned && hoverTime >= timeToKill * 0.5f)
            SpawnDecoys();

        Darken(1f - hoverTime / timeToKill);

        if (hoverTime >= timeToKill) StartCoroutine(Win());
    }

    /// <summary>
    /// The picture is black. The message that was in it all along turns red, sits there a
    /// moment, and then the next challenge takes over.
    /// </summary>
    IEnumerator Win()
    {
        if (hiddenMessage != null)
        {
            SpriteRenderer[] sprites = hiddenMessage.GetComponentsInChildren<SpriteRenderer>();
            TMP_Text[] texts = hiddenMessage.GetComponentsInChildren<TMP_Text>();

            // Fades from whatever colour it was hiding as, so it works however it's kept unreadable.
            Color[] spriteFrom = new Color[sprites.Length];
            Color[] textFrom = new Color[texts.Length];
            for (int i = 0; i < sprites.Length; i++) spriteFrom[i] = sprites[i].color;
            for (int i = 0; i < texts.Length; i++) textFrom[i] = texts[i].color;

            for (float t = 0f; t < messageFadeIn; t += Time.deltaTime)
            {
                float k = t / messageFadeIn;
                for (int i = 0; i < sprites.Length; i++) sprites[i].color = Color.Lerp(spriteFrom[i], messageColor, k);
                for (int i = 0; i < texts.Length; i++) texts[i].color = Color.Lerp(textFrom[i], messageColor, k);
                yield return null;
            }

            foreach (SpriteRenderer sr in sprites) sr.color = messageColor;
            foreach (TMP_Text text in texts) text.color = messageColor;

            yield return new WaitForSeconds(messageHold);
        }

        if (nextChallenge == null)
            Debug.LogWarning("ClickHealth: Next Challenge isn't assigned, so nothing appears after the fade.", this);
        else
            nextChallenge.SetActive(true);

        currentChallenge.SetActive(false);
    }

    void SpawnDecoys()
    {
        decoysSpawned = true;
        foreach (Transform p in spawnPoints)
        {
            GameObject decoy = Instantiate(decoyPrefab, p.position, Quaternion.identity, currentChallenge.transform);
            DecoyBrighten brighten = decoy.GetComponent<DecoyBrighten>();
            if (brighten != null) brighten.target = this;
        }
    }

    // Called by decoys: takes back hover progress, which brightens the screen again.
    public void Restore(float seconds)
    {
        if (hoverTime >= timeToKill) return;

        hoverTime = Mathf.Max(hoverTime - seconds, 0f);
        Darken(1f - hoverTime / timeToKill);
    }

    // Tints every sprite in the challenge (the frame, the capsule, decoys) toward black. 1 = normal, 0 = black.
    void Darken(float newBrightness)
    {
        brightness = newBrightness;
        ApplyBrightness();
    }

    void ApplyBrightness()
    {
        foreach (SpriteRenderer sr in currentChallenge.GetComponentsInChildren<SpriteRenderer>())
        {
            if (!originalColors.TryGetValue(sr, out Color original))
                originalColors[sr] = original = sr.color;   // remembers the colour the first time it's seen, including new decoys

            // While flashing, the target itself shows at full brightness
            float b = flashing && System.Array.IndexOf(targetRenderers, sr) >= 0 ? 1f : brightness;
            Color c = original * b;
            c.a = original.a;
            sr.color = c;
        }
    }
}
