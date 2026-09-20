using System.Collections;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.InputSystem;

/// <summary>
/// A cryptogram, built at runtime from the sentence below. Every letter is swapped for a
/// different one at random, so the code is new every play: APPLE might come out as XOOIW.
///
/// Each letter gets a hangman blank. The coded letter sits under the line, the player's
/// answer goes above it. Click a coded letter to select it, type what you think it stands
/// for, and every copy of that coded letter fills in at once; backspace clears them all
/// again. Solve the sentence and the answer turns bold, "CLICK TO CONTINUE" appears, and
/// the next click hands over to nextChallenge (the same swap ClickHealth does).
///
/// Nothing needs building by hand: drop this on an empty object and it draws itself.
/// </summary>
public class CryptogramPuzzle : MonoBehaviour
{
    [Header("Puzzle")]
    [TextArea] public string sentence = "THE APPLE DOES NOT FALL FAR FROM THE TREE";
    [Tooltip("0 = a fresh code every play. Any other number always gives the same code, which is handy while testing.")]
    public int seed = 0;
    [Tooltip("Typing a letter that's already used elsewhere clears it from the old place first.")]
    public bool oneLetterOnlyOnce = true;
    [Tooltip("After typing, jump to the next coded letter that's still blank.")]
    public bool autoAdvance = true;

    [Header("Look")]
    public TMP_FontAsset font;
    [Tooltip("Height of a capital letter, in this object's local units.")]
    public float letterHeight = 0.2f;
    [Tooltip("Distance between the middle of one blank and the next.")]
    public float columnSpacing = 0.3f;
    [Tooltip("Distance between one row of blanks and the next.")]
    public float rowSpacing = 0.95f;
    public float gapAboveLine = 0.06f;
    public float gapBelowLine = 0.07f;
    [Tooltip("The puzzle shrinks to stay inside this box. Keep it inside the picture frame.")]
    public float maxWidth = 5.6f;
    public float maxHeight = 4.2f;

    [Header("The blank under each letter")]
    public float lineLength = 0.24f;
    public float lineThickness = 0.035f;
    [Tooltip("Dots in each blank. 1 draws a solid line instead.")]
    public int lineDots = 5;

    [Header("Colours")]
    public Color codeColor = new Color(0.72f, 0.64f, 0.48f, 1f);
    public Color answerColor = new Color(0.96f, 0.92f, 0.8f, 1f);
    public Color lineColor = new Color(0.62f, 0.55f, 0.42f, 1f);
    public Color selectedColor = new Color(1f, 0.8f, 0.3f, 1f);

    [Header("When it's solved")]
    [Tooltip("Seconds after the last letter goes in before everything but the letters to remember disappears.")]
    public float revealDelay = 2f;

    [Header("Letters to remember")]
    [Tooltip("Which letters are left on screen once it's solved, so the player carries them into the next " +
             "riddle. Count the letters of the sentence only — no spaces, no punctuation — starting at 1.")]
    public int[] highlightPositions = new int[] { 4, 25 };
    [Tooltip("The colour of those letters, and of the message underneath them.")]
    public Color highlightColor = new Color(0.3764706f, 0.08235294f, 0.05882353f, 1f);   // 60150F
    public bool highlightBold = true;
    [Tooltip("How much bigger those letters are drawn than the sentence was.")]
    public float highlightScale = 1.35f;

    [Header("Carrying on")]
    public string continueMessage = "CLICK TO CONTINUE";
    [Tooltip("Seconds the rest of the puzzle takes to fade away, while the message fades in over the same time.")]
    public float fadeDuration = 1.5f;
    [Tooltip("Turned off once the player clicks the message. Defaults to the top of this object's hierarchy.")]
    public GameObject currentChallenge;
    [Tooltip("Turned on once the player clicks the message.")]
    public GameObject nextChallenge;
    public UnityEvent onSolved;

    [Header("Audio")]
    public AudioClip typeSound;
    public AudioClip eraseSound;
    public AudioClip solvedSound;
    [Range(0f, 1f)] public float volume = 0.6f;

    class Slot
    {
        public char plain;                 // the real letter
        public char code;                  // what's shown under the line
        public bool isLetter;
        public int letterNumber;           // 1-based position among the sentence's letters
        public Vector2 pos;                // middle of the blank, in local space
        public TextMeshPro codeText;
        public TextMeshPro answerText;
        public SpriteRenderer line;
    }

    readonly List<Slot> slots = new List<Slot>();
    readonly char[] encode = new char[26];      // real letter -> coded letter
    readonly char[] answer = new char[26];      // coded letter -> what the player typed
    readonly List<char> typed = new List<char>();

    char selected;                              // '\0' = nothing selected
    bool solved, canContinue, finished;
    float slotFontSize;

    // Layout values after the auto-shrink, so hit testing matches what's drawn.
    float colW, rowH, aboveLine, belowLine;

    TextMeshPro continueText;
    AudioSource audioSource;
    Keyboard listeningTo;
    Texture2D lineTexture;
    Sprite lineSprite;
    Camera cam;

    void OnEnable() => Listen();

    void OnDisable()
    {
        if (listeningTo != null) listeningTo.onTextInput -= OnTextInput;
        listeningTo = null;
    }

    void OnDestroy()
    {
        if (lineSprite != null) Destroy(lineSprite);
        if (lineTexture != null) Destroy(lineTexture);
    }

    void Start()
    {
        cam = Camera.main;

        audioSource = GetComponent<AudioSource>();
        if (audioSource == null) audioSource = gameObject.AddComponent<AudioSource>();
        audioSource.playOnAwake = false;
        audioSource.volume = volume;

        if (font == null) font = TMP_Settings.defaultFontAsset;
        if (currentChallenge == null) currentChallenge = transform.root.gameObject;

        Build();
    }

    // ---------------------------------------------------------------- building

    void Build()
    {
        string text = Normalise(sentence);
        if (text.Length == 0)
        {
            Debug.LogWarning("CryptogramPuzzle: the sentence has no letters in it.", this);
            return;
        }

        MakeCode(text);

        string[] words = text.Split(' ');
        List<string> lines;

        // Shrink everything together until the whole puzzle fits in the box.
        float scale = 1f;
        float contentH;
        while (true)
        {
            int maxCols = Mathf.Max(1, Mathf.FloorToInt(maxWidth / (columnSpacing * scale)));
            lines = WrapBalanced(words, maxCols);

            aboveLine = (gapAboveLine + letterHeight) * scale;
            belowLine = (gapBelowLine + letterHeight) * scale;
            rowH = rowSpacing * scale;
            colW = columnSpacing * scale;

            float blockH = (lines.Count - 1) * rowH + aboveLine + belowLine;
            contentH = blockH + rowH * 0.75f;                     // room for the message underneath

            if (contentH <= maxHeight || scale < 0.25f) break;
            scale *= Mathf.Max(0.7f, maxHeight / contentH) * 0.97f;
        }

        float letterH = letterHeight * scale;
        float fontSize = FontSizeFor(letterH);
        float blockHeight = (lines.Count - 1) * rowH + aboveLine + belowLine;
        float topY = contentH * 0.5f;

        MakeLineSprite();
        slotFontSize = fontSize;
        int letterNumber = 0;

        for (int row = 0; row < lines.Count; row++)
        {
            string line = lines[row];
            float y = topY - aboveLine - row * rowH;
            float left = -(line.Length - 1) * 0.5f * colW;

            for (int col = 0; col < line.Length; col++)
            {
                char c = line[col];
                if (c == ' ') continue;

                Slot slot = new Slot
                {
                    plain = c,
                    isLetter = c >= 'A' && c <= 'Z',
                    pos = new Vector2(left + col * colW, y),
                };
                slot.code = slot.isLetter ? encode[c - 'A'] : c;

                slot.codeText = MakeText("Code", slot.code.ToString(), fontSize, codeColor,
                                         new Vector3(slot.pos.x, slot.pos.y - gapBelowLine * scale - letterH, 0f));

                if (slot.isLetter)
                {
                    slot.letterNumber = ++letterNumber;
                    slot.answerText = MakeText("Answer", "", fontSize, answerColor,
                                               new Vector3(slot.pos.x, slot.pos.y + gapAboveLine * scale, 0f));
                    slot.line = MakeLine(slot.pos, lineLength * scale, lineThickness * scale);
                }

                slots.Add(slot);
            }
        }

        continueText = MakeText("ContinueMessage", continueMessage, fontSize, highlightColor,
                                new Vector3(0f, topY - blockHeight - rowH * 0.5f, 0f));   // in the band under the last row
        continueText.rectTransform.sizeDelta = new Vector2(maxWidth * 2f, letterH * 6f);
        continueText.gameObject.SetActive(false);

        if (highlightPositions != null)
            foreach (int p in highlightPositions)
                if (p < 1 || p > letterNumber)
                    Debug.LogWarning("CryptogramPuzzle: highlight position " + p + " isn't a letter of this sentence, " +
                                     "which only has " + letterNumber + " of them. Count letters only, starting at 1.", this);

        Refresh();
    }

    /// <summary>Upper case, letters and simple punctuation only, single spaces.</summary>
    static string Normalise(string raw)
    {
        System.Text.StringBuilder sb = new System.Text.StringBuilder();
        bool pendingSpace = false;

        foreach (char c in raw.ToUpperInvariant())
        {
            if (char.IsWhiteSpace(c)) { pendingSpace = sb.Length > 0; continue; }
            if (!(c >= 'A' && c <= 'Z') && "',.!?;:-".IndexOf(c) < 0) continue;

            if (pendingSpace) { sb.Append(' '); pendingSpace = false; }
            sb.Append(c);
        }
        return sb.ToString();
    }

    /// <summary>Random one-to-one letter swap. No letter used in the sentence maps to itself.</summary>
    void MakeCode(string text)
    {
        System.Random rng = seed == 0 ? new System.Random() : new System.Random(seed);

        bool[] used = new bool[26];
        foreach (char c in text) if (c >= 'A' && c <= 'Z') used[c - 'A'] = true;

        int[] map = new int[26];
        for (int attempt = 0; attempt < 200; attempt++)
        {
            for (int i = 0; i < 26; i++) map[i] = i;
            for (int i = 25; i > 0; i--)
            {
                int j = rng.Next(i + 1);
                (map[i], map[j]) = (map[j], map[i]);
            }

            bool clean = true;
            for (int i = 0; i < 26 && clean; i++) if (used[i] && map[i] == i) clean = false;
            if (clean) break;
        }

        // Belt and braces: swapping a leftover letter with its neighbour can't create a new match.
        for (int i = 0; i < 26; i++)
            if (used[i] && map[i] == i)
            {
                int j = (i + 1) % 26;
                (map[i], map[j]) = (map[j], map[i]);
            }

        for (int i = 0; i < 26; i++) encode[i] = (char)('A' + map[i]);
    }

    /// <summary>Wraps into the fewest lines it can, then evens their lengths out.</summary>
    static List<string> WrapBalanced(string[] words, int maxColumns)
    {
        int longest = 1;
        foreach (string w in words) longest = Mathf.Max(longest, Mathf.Min(w.Length, maxColumns));

        int fewest = Wrap(words, maxColumns).Count;

        int lo = longest, hi = maxColumns;
        while (lo < hi)
        {
            int mid = (lo + hi) / 2;
            if (Wrap(words, mid).Count <= fewest) hi = mid; else lo = mid + 1;
        }
        return Wrap(words, lo);
    }

    static List<string> Wrap(string[] words, int width)
    {
        List<string> lines = new List<string>();
        string current = "";

        foreach (string w in words)
        {
            string word = w;
            while (word.Length > width)                       // a word too long for one line
            {
                if (current.Length > 0) { lines.Add(current); current = ""; }
                lines.Add(word.Substring(0, width));
                word = word.Substring(width);
            }

            if (word.Length == 0) continue;
            if (current.Length == 0) current = word;
            else if (current.Length + 1 + word.Length <= width) current += " " + word;
            else { lines.Add(current); current = word; }
        }

        if (current.Length > 0) lines.Add(current);
        return lines;
    }

    /// <summary>Font size that draws a capital letter exactly <paramref name="height"/> units tall.</summary>
    float FontSizeFor(float height)
    {
        const float probeSize = 100f;

        if (font != null && font.characterLookupTable.TryGetValue('M', out TMP_Character m) && m.glyph != null)
        {
            float capEm = m.glyph.metrics.horizontalBearingY / font.faceInfo.pointSize;
            float advanceEm = m.glyph.metrics.horizontalAdvance / font.faceInfo.pointSize;

            if (capEm > 0f && advanceEm > 0f)
            {
                // Measure how big an em actually draws, rather than assuming TextMeshPro's scale factor.
                TextMeshPro probe = MakeText("Probe", "MM", probeSize, Color.clear, Vector3.zero);
                probe.rectTransform.sizeDelta = new Vector2(10000f, 10000f);
                probe.ForceMeshUpdate();

                float emAtProbe = 0f;
                if (probe.textInfo != null && probe.textInfo.characterCount >= 2)
                {
                    float advance = probe.textInfo.characterInfo[1].origin - probe.textInfo.characterInfo[0].origin;
                    emAtProbe = advance / advanceEm;
                }
                Destroy(probe.gameObject);

                if (emAtProbe > 0.0001f) return probeSize * (height / capEm) / emAtProbe;
            }
        }

        return height * 12f;   // sane fallback if the font can't be measured
    }

    TextMeshPro MakeText(string name, string content, float size, Color color, Vector3 localPos)
    {
        GameObject go = new GameObject(name);
        TextMeshPro t = go.AddComponent<TextMeshPro>();
        go.transform.SetParent(transform, false);

        if (font != null) t.font = font;
        t.fontSize = size;
        t.color = color;
        t.alignment = TextAlignmentOptions.Baseline;      // centred across, sitting on the baseline
        t.text = content;
        t.sortingOrder = 4;
        t.rectTransform.sizeDelta = new Vector2(Mathf.Max(size, 1f), Mathf.Max(size, 1f));
        t.rectTransform.localPosition = localPos;
        return t;
    }

    void MakeLineSprite()
    {
        int dots = Mathf.Max(1, lineDots);
        int width = dots * 2 - 1;                         // dot, gap, dot, gap, ... dot

        lineTexture = new Texture2D(width, 1, TextureFormat.RGBA32, false)
        { filterMode = FilterMode.Point, wrapMode = TextureWrapMode.Clamp };
        for (int x = 0; x < width; x++)
            lineTexture.SetPixel(x, 0, x % 2 == 0 ? Color.white : Color.clear);
        lineTexture.Apply();

        lineSprite = Sprite.Create(lineTexture, new Rect(0, 0, width, 1), new Vector2(0.5f, 0.5f), 1f);
    }

    SpriteRenderer MakeLine(Vector2 pos, float length, float thickness)
    {
        GameObject go = new GameObject("Line");
        go.transform.SetParent(transform, false);
        go.transform.localPosition = new Vector3(pos.x, pos.y, 0f);
        go.transform.localScale = new Vector3(length / lineSprite.rect.width, thickness, 1f);

        SpriteRenderer sr = go.AddComponent<SpriteRenderer>();
        sr.sprite = lineSprite;
        sr.color = lineColor;
        sr.sortingOrder = 3;
        return sr;
    }

    // ----------------------------------------------------------------- playing

    void Update()
    {
        if (finished || slots.Count == 0) { typed.Clear(); return; }

        Listen();

        if (solved)
        {
            if (canContinue && Clicked()) Advance();   // Outro() runs the rest
            typed.Clear();
            return;
        }

        if (Clicked()) ClickAt(MouseWorldPosition());

        if (selected != '\0')
        {
            foreach (char c in typed) Type(c);

            Keyboard k = Keyboard.current;
            if (k != null && (k.backspaceKey.wasPressedThisFrame || k.deleteKey.wasPressedThisFrame)) Erase();
            if (k != null && k.tabKey.wasPressedThisFrame) Step(k.shiftKey.isPressed ? -1 : 1);
            if (k != null && k.rightArrowKey.wasPressedThisFrame) Step(1);
            if (k != null && k.leftArrowKey.wasPressedThisFrame) Step(-1);

            // The selected blanks breathe, so it's obvious which letter you're typing into.
            float pulse = 0.7f + 0.3f * Mathf.Sin(Time.time * 6f);
            Color pulsed = new Color(selectedColor.r * pulse, selectedColor.g * pulse, selectedColor.b * pulse, selectedColor.a);
            foreach (Slot s in slots)
                if (s.isLetter && s.code == selected && s.line != null)
                    s.line.color = pulsed;
        }

        typed.Clear();
    }

    void Listen()
    {
        Keyboard k = Keyboard.current;
        if (k == listeningTo) return;

        if (listeningTo != null) listeningTo.onTextInput -= OnTextInput;
        listeningTo = k;
        if (listeningTo != null) listeningTo.onTextInput += OnTextInput;
    }

    void OnTextInput(char c)
    {
        c = char.ToUpperInvariant(c);
        if (c >= 'A' && c <= 'Z') typed.Add(c);
    }

    static bool Clicked() => Mouse.current != null && Mouse.current.leftButton.wasPressedThisFrame;

    Vector3 MouseWorldPosition()
    {
        if (cam == null) cam = Camera.main;
        if (cam == null) return Vector3.zero;

        Vector2 screen = Mouse.current.position.ReadValue();
        float depth = Mathf.Abs(transform.position.z - cam.transform.position.z);
        return cam.ScreenToWorldPoint(new Vector3(screen.x, screen.y, depth));
    }

    void ClickAt(Vector3 world)
    {
        Vector3 local = transform.InverseTransformPoint(world);

        foreach (Slot s in slots)
        {
            if (!s.isLetter) continue;
            if (Mathf.Abs(local.x - s.pos.x) > colW * 0.5f) continue;
            if (local.y < s.pos.y - rowH * 0.5f || local.y > s.pos.y + rowH * 0.5f) continue;

            Select(s.code);
            return;
        }

        Select('\0');
    }

    void Select(char code)
    {
        if (selected == code) return;
        selected = code;
        Refresh();
    }

    void Type(char letter)
    {
        if (selected == '\0') return;

        if (oneLetterOnlyOnce)
            for (int i = 0; i < 26; i++)
                if (answer[i] == letter && (char)('A' + i) != selected) answer[i] = '\0';

        answer[selected - 'A'] = letter;
        Play(typeSound);

        if (CheckSolved()) return;
        if (autoAdvance) SelectNextBlank();
        Refresh();
    }

    void Erase()
    {
        if (selected == '\0' || answer[selected - 'A'] == '\0') return;

        answer[selected - 'A'] = '\0';
        Play(eraseSound != null ? eraseSound : typeSound);
        Refresh();
    }

    /// <summary>Moves the selection along the sentence, skipping repeats of the same coded letter.</summary>
    void Step(int direction)
    {
        List<char> order = CodeOrder();
        if (order.Count == 0) return;

        int at = order.IndexOf(selected);
        int next = at < 0 ? (direction > 0 ? 0 : order.Count - 1)
                          : ((at + direction) % order.Count + order.Count) % order.Count;
        Select(order[next]);
    }

    void SelectNextBlank()
    {
        List<char> order = CodeOrder();
        int at = Mathf.Max(0, order.IndexOf(selected));

        for (int i = 1; i <= order.Count; i++)
        {
            char code = order[(at + i) % order.Count];
            if (answer[code - 'A'] == '\0') { selected = code; return; }
        }
    }

    /// <summary>The coded letters in the order they first appear, so stepping reads left to right.</summary>
    List<char> CodeOrder()
    {
        List<char> order = new List<char>();
        foreach (Slot s in slots)
            if (s.isLetter && !order.Contains(s.code)) order.Add(s.code);
        return order;
    }

    bool CheckSolved()
    {
        foreach (Slot s in slots)
            if (s.isLetter && answer[s.code - 'A'] != s.plain) return false;

        solved = true;
        selected = '\0';
        Refresh();                          // the sentence just sits there until Outro() takes over
        if (onSolved != null) onSolved.Invoke();
        StartCoroutine(Outro());
        return true;
    }

    void Refresh()
    {
        foreach (Slot s in slots)
        {
            if (!s.isLetter) continue;

            bool isSelected = selected != '\0' && s.code == selected;
            char typedLetter = answer[s.code - 'A'];

            s.answerText.text = typedLetter == '\0' ? "" : typedLetter.ToString();
            s.answerText.color = isSelected ? selectedColor : answerColor;

            s.codeText.color = isSelected ? selectedColor : codeColor;
            s.line.color = isSelected ? selectedColor : lineColor;
        }
    }

    /// <summary>
    /// A beat on the finished sentence, then the puzzle fades away while the message fades up
    /// in its place. Only the letters the player carries into the next riddle are left behind.
    /// </summary>
    IEnumerator Outro()
    {
        yield return new WaitForSeconds(revealDelay);

        Play(solvedSound);

        List<TextMeshPro> leaving = new List<TextMeshPro>();
        List<SpriteRenderer> linesLeaving = new List<SpriteRenderer>();

        foreach (Slot s in slots)
        {
            leaving.Add(s.codeText);
            if (s.line != null) linesLeaving.Add(s.line);

            if (!s.isLetter) continue;

            if (!IsRemembered(s.letterNumber)) { leaving.Add(s.answerText); continue; }

            s.answerText.color = highlightColor;
            s.answerText.fontSize = slotFontSize * Mathf.Max(0.1f, highlightScale);
            if (highlightBold) s.answerText.fontStyle = FontStyles.Bold;
        }

        continueText.gameObject.SetActive(true);
        continueText.alpha = 0f;
        canContinue = true;                 // the message is on its way in, so a click counts

        for (float t = 0f; t < fadeDuration; t += Time.deltaTime)
        {
            float k = t / fadeDuration;
            foreach (TextMeshPro text in leaving) text.alpha = 1f - k;
            foreach (SpriteRenderer line in linesLeaving) SetAlpha(line, 1f - k);
            continueText.alpha = k;
            yield return null;
        }

        foreach (TextMeshPro text in leaving) text.gameObject.SetActive(false);
        foreach (SpriteRenderer line in linesLeaving) line.gameObject.SetActive(false);
        continueText.alpha = 1f;
    }

    static void SetAlpha(SpriteRenderer sr, float a)
    {
        Color c = sr.color;
        c.a = a;
        sr.color = c;
    }

    bool IsRemembered(int letterNumber)
    {
        if (highlightPositions == null) return false;
        foreach (int p in highlightPositions) if (p == letterNumber) return true;
        return false;
    }

    void Advance()
    {
        finished = true;

        if (nextChallenge != null) nextChallenge.SetActive(true);
        else Debug.LogWarning("CryptogramPuzzle: Next Challenge isn't assigned, so nothing happens after the puzzle.", this);

        if (currentChallenge != null) currentChallenge.SetActive(false);
    }

    void Play(AudioClip clip)
    {
        if (clip != null && audioSource != null) audioSource.PlayOneShot(clip, volume);
    }
}
