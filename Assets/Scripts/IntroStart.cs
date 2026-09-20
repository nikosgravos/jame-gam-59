using UnityEngine;
using UnityEngine.InputSystem;

/// <summary>
/// Put this on any object in the intro scene. Shows the prompt object until the player
/// clicks (or presses any key), then hides it and starts the intro sequence.
/// </summary>
public class IntroStart : MonoBehaviour
{
    public GameObject prompt;          // e.g. a "Click to start" text object
    public IntroSequence sequence;
    public MusicPlayer music;          // starts looping when the player clicks
    public GameObject introText;      // the object with TextRenderer; leave it disabled in the scene

    bool started;

    void Update()
    {
        if (started) return;

        bool clicked = Mouse.current != null && Mouse.current.leftButton.wasPressedThisFrame;
        bool keyed = Keyboard.current != null && Keyboard.current.anyKey.wasPressedThisFrame;
        if (!clicked && !keyed) return;

        started = true;
        if (prompt != null) prompt.SetActive(false);
        if (music != null) music.Play();
        sequence.Begin();
        if (introText != null) introText.SetActive(true);   // TextRenderer plays as soon as it's enabled
    }
}
