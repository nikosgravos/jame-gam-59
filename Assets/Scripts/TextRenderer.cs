using System.Collections;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.Events;

/// <summary>
/// Put this on the TextMeshPro object that holds the intro text.
/// Plays automatically: each line types out letter by letter, pauses, then the next line starts.
/// </summary>
[RequireComponent(typeof(TMP_Text))]
public class TextRenderer : MonoBehaviour
{
    [Header("Timing (seconds)")]
    public float startDelay = 1f;          // wait before the first line
    public float secondsPerLetter = 0.05f;
    public float pauseAfterLine = 1.2f;    // wait between one line finishing and the next starting
    public float endDelay = 1.5f;          // wait after the last line before the frame appears
    [Header("Audio")]
    public AudioClip typingSound;          // plays from the start of each line, stops when the line ends
    [Range(0f, 1f)] public float typingVolume = 0.6f;

    [Header("When it's done")]
    public GameObject frame;               // activated at the end, which starts the first challenge
    public UnityEvent onFinished;          // optional extra actions

    TMP_Text text;
    AudioSource audioSource;
    readonly List<int> starts = new List<int>();   // index of each line's first character
    readonly List<int> ends = new List<int>();     // index just past each line's last character

    void OnEnable()
    {
        text = GetComponent<TMP_Text>();

        audioSource = GetComponent<AudioSource>();
        if (audioSource == null) audioSource = gameObject.AddComponent<AudioSource>();
        audioSource.playOnAwake = false;
        audioSource.loop = true;         // keeps going if the clip is shorter than the line
        audioSource.clip = typingSound;
        audioSource.volume = typingVolume;

        // Read the lines straight from the text box. Blank lines are skipped.
        string full = text.text.Replace("\r\n", "\n");
        text.text = full;
        text.maxVisibleCharacters = 0;   // the full text stays laid out, so nothing shifts while it reveals

        starts.Clear();
        ends.Clear();
        int pos = 0;
        foreach (string line in full.Split('\n'))
        {
            if (line.Trim().Length > 0)
            {
                starts.Add(pos);
                ends.Add(pos + line.Length);
            }
            pos += line.Length + 1;   // +1 for the newline
        }

        StartCoroutine(Play());
    }

    IEnumerator Play()
    {
        yield return new WaitForSeconds(startDelay);

        for (int line = 0; line < ends.Count; line++)
        {
            text.maxVisibleCharacters = starts[line];
            if (typingSound != null) audioSource.Play();   // restarts from the beginning each line

            for (int i = starts[line] + 1; i <= ends[line]; i++)
            {
                text.maxVisibleCharacters = i;
                yield return new WaitForSeconds(secondsPerLetter);
            }

            audioSource.Stop();
            if (line < ends.Count - 1) yield return new WaitForSeconds(pauseAfterLine);
        }

        yield return new WaitForSeconds(endDelay);

        onFinished.Invoke();
        if (frame != null) frame.SetActive(true);
        gameObject.SetActive(false);   // hide the intro text
    }
}
