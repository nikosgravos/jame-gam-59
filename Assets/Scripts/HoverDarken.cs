using System.Collections.Generic;
using UnityEngine;

public class ClickHealth : MonoBehaviour
{
    public float timeToKill = 20f;   // total seconds of hovering needed, across the whole fight
    public GameObject currentChallenge;
    public GameObject nextChallenge;

    [Header("Decoys at half way")]
    public GameObject decoyPrefab;
    public Transform[] spawnPoints;

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

        if (hoverTime >= timeToKill)
        {
            if (nextChallenge == null)
                Debug.LogWarning("ClickHealth: Next Challenge isn't assigned, so nothing appears after the fade.", this);
            else
                nextChallenge.SetActive(true);

            currentChallenge.SetActive(false);
        }
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
