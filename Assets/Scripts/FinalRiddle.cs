using System.Collections;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.InputSystem;

/// <summary>
/// The last riddle, in two halves.
///
/// First a QWERTY keyboard: click a key to take one of that letter, click again to take a
/// second, again to put them back. Check the pick against the answer's letters — for
/// "JUDGMENT CAME" that's one each of C A G J D N U T and two each of M E.
///
/// Get that right and the keyboard gives way to those twelve letters as loose tiles, with a
/// row of blanks to drag them onto. Spell the answer and the scene fades to black and hands
/// over to the next one.
///
/// Nothing needs building by hand: drop this on an object and it draws itself. Sizes are in
/// world units whatever the object is scaled to.
/// </summary>
public class FinalRiddle : MonoBehaviour
{
    [Header("Riddle")]
    [Tooltip("Drives both halves: its letters are what must be picked, and their order is what must be spelled.")]
    public string answer = "JUDGMENT CAME";
    [Tooltip("Most of any one letter the player may take.")]
    public int maxPerLetter = 2;

    [Header("Font")]
    public TMP_FontAsset font;
    [Tooltip("Height of a capital letter, in world units.")]
    public float letterHeight = 0.26f;

    [Header("Keyboard")]
    public float keySize = 0.62f;
    public float keyGap = 0.1f;
    [Tooltip("Middle of the keyboard block.")]
    public Vector2 keyboardCentre = new Vector2(0f, -1.7f);

    [Header("Blanks and tiles")]
    [Tooltip("Height of the row of blanks the letters are dragged onto.")]
    public float slotRowY = 2.2f;
    public float slotSpacing = 0.62f;
    public float slotLineLength = 0.46f;
    public float lineThickness = 0.045f;
    public int lineDots = 7;
    [Tooltip("Extra room left where the answer has a space.")]
    public float wordGap = 0.45f;
    [Tooltip("Height of the row the loose tiles start on.")]
    public float trayRowY = 0.6f;
    public float traySpacing = 0.72f;

    [Header("Hint under the letters")]
    [Tooltip("Flavour text under the tray while the answer is being spelled out. Points at it without saying it.")]
    [TextArea(4, 8)]
    public string spellingHint =
        "EVE REACHED FOR THE TOMPLE. NEWTON SAT BENEATH IT.\n" +
        "THE GIRL IN THE GLASS COFFIN BIT INTO IT.\n" +
        "\n" +
        "NO COURT WAS CALLED. NO SENTENCE WAS READ ALOUD.\n" +
        "AND YET, EACH TIME, IT ARRIVED ALL THE SAME.";
    public float spellingHintY = -1.7f;
    [Tooltip("Size of the hint next to the letters, as a fraction of them.")]
    public float spellingHintScale = 0.62f;

    [Header("Check button")]
    public string buttonLabel = "CHECK";
    public float buttonY = -4f;
    public float buttonPadding = 0.22f;

    [Header("Reminder (optional hint, top left)")]
    public string hintHeading = "REMEMBER";
    [Tooltip("Shown under the heading while it's shut, so nobody opens it by accident.")]
    public string hintPrompt = "CLICK IF YOU FORGOT - THIS GIVES AWAY THE LETTERS";
    public string hintPromptOpen = "CLICK TO HIDE";
    [Tooltip("One row each, written as LABEL|ANSWER. The label is dimmed, the answer stands out.")]
    public string[] hintLines =
    {
        "PUZZLE 1|U M M",
        "PUZZLE 2|E N T E",
        "PUZZLE 3|3>C  1>A  4>G  10>J  4>D",
    };
    [Tooltip("How far in from the top-left corner of the view it sits.")]
    public float hintMargin = 0.55f;

    [Header("Messages")]
    public string wrongPick = "TRY AGAIN";
    public string wrongSpelling = "TRY AGAIN";
    public float messageTime = 2f;
    public float messageY = -4.7f;

    [Header("Colour")]
    [Tooltip("Every part of the riddle is tinted from this one colour.")]
    public Color uiColor = new Color(0.3764706f, 0.08235294f, 0.05882353f, 1f);   // 60150F

    [Header("Layering")]
    public int sortingOrder = 1;

    [Header("When it's spelled")]
    [Tooltip("Seconds the whole scene takes to fade down to black.")]
    public float fadeToBlack = 2.5f;
    [Tooltip("Faded out and turned off. Defaults to the top of this object's hierarchy.")]
    public GameObject currentChallenge;
    [Tooltip("Turned on once the fade finishes.")]
    public GameObject nextChallenge;
    public UnityEvent onSolved;

    [Header("Audio")]
    public AudioClip keySound;
    public AudioClip wrongSound;
    public AudioClip correctSound;
    [Range(0f, 1f)] public float volume = 0.6f;

    // Shades of the one colour, so the whole riddle moves together when it changes.
    Color Ink => uiColor;                        // letters and labels
    Color Bright => Scaled(uiColor, 2.2f);       // hovered, and the second of a doubled letter
    Color Faint => Alpha(uiColor, 0.55f);        // the blank lines
    Color Alarm => Scaled(uiColor, 2.6f);        // getting it wrong

    Color FillEmpty => Alpha(uiColor, 0.1f);     // a key nothing is taken from
    Color FillOne => Alpha(uiColor, 0.3f);       // one taken, and the loose tiles
    Color FillTwo => Alpha(uiColor, 0.48f);      // both taken

    static Color Scaled(Color c, float k) =>
        new Color(Mathf.Clamp01(c.r * k), Mathf.Clamp01(c.g * k), Mathf.Clamp01(c.b * k), c.a);

    static Color Alpha(Color c, float a) { c.a *= a; return c; }

    const string Row1 = "QWERTYUIOP";
    const string Row2 = "ASDFGHJKL";
    const string Row3 = "ZXCVBNM";

    class Key
    {
        public char letter;
        public int taken;
        public Vector2 pos;
        public SpriteRenderer box;
        public TextMeshPro label, count;
    }

    class Tile
    {
        public char letter;
        public Vector2 home;               // where it sits when it isn't in a blank
        public int slot = -1;              // which blank it's in, -1 for none
        public Transform root;
        public SpriteRenderer box;
        public TextMeshPro label;
    }

    class Slot
    {
        public Vector2 pos;
        public Tile tile;
        public SpriteRenderer line;
    }

    enum Phase { Picking, Spelling, Done }

    readonly List<Key> keys = new List<Key>();
    readonly List<Tile> tiles = new List<Tile>();
    readonly List<Slot> slots = new List<Slot>();

    Phase phase = Phase.Picking;
    Transform overlay, keyboard, spelling;
    TextMeshPro buttonText, message;
    SpriteRenderer buttonBox;
    Vector2 buttonSize;
    float fontSize, messageUntil;
    bool hoveringButton;

    Tile dragging;
    Vector2 dragGrab;

    Transform hint;
    SpriteRenderer hintPanel;
    TextMeshPro hintTitle, hintSub;
    readonly List<TextMeshPro> hintRows = new List<TextMeshPro>();
    Vector2 hintHitCentre, hintHitSize;
    bool hintOpen;

    AudioSource audioSource;
    Texture2D boxTexture, lineTexture;
    Sprite boxSprite, lineSprite;
    Camera cam;

    void OnDestroy()
    {
        if (boxSprite != null) Destroy(boxSprite);
        if (boxTexture != null) Destroy(boxTexture);
        if (lineSprite != null) Destroy(lineSprite);
        if (lineTexture != null) Destroy(lineTexture);
    }

    void Start()
    {
        cam = Camera.main;

        audioSource = GetComponent<AudioSource>();
        if (audioSource == null) audioSource = gameObject.AddComponent<AudioSource>();
        audioSource.playOnAwake = false;

        if (font == null) font = TMP_Settings.defaultFontAsset;
        if (currentChallenge == null) currentChallenge = transform.root.gameObject;

        Build();
    }

    // ---------------------------------------------------------------- building

    void Build()
    {
        if (Letters(answer).Count == 0)
        {
            Debug.LogWarning("FinalRiddle: the answer has no letters in it.", this);
            enabled = false;
            return;
        }

        float parentScale = transform.lossyScale.x;
        if (Mathf.Approximately(parentScale, 0f)) parentScale = 1f;

        GameObject overlayGo = new GameObject("Overlay");
        overlay = overlayGo.transform;
        overlay.SetParent(transform, false);
        overlay.localScale = Vector3.one / parentScale;   // inside here, 1 local unit == 1 world unit

        MakeBoxSprite();
        MakeLineSprite();
        fontSize = FontSizeFor(letterHeight);

        keyboard = new GameObject("Keyboard").transform;
        keyboard.SetParent(overlay, false);

        spelling = new GameObject("Spelling").transform;
        spelling.SetParent(overlay, false);
        spelling.gameObject.SetActive(false);

        BuildKeyboard();
        BuildButton();
        BuildHint();

        message = MakeText("Message", "", Alarm, new Vector2(0f, messageY), overlay);
        message.rectTransform.sizeDelta = new Vector2(20f, 5f);
        message.gameObject.SetActive(false);

        RefreshKeys();
    }

    void BuildKeyboard()
    {
        string[] rows = { Row1, Row2, Row3 };
        float step = keySize + keyGap;

        for (int r = 0; r < rows.Length; r++)
        {
            string row = rows[r];
            float y = keyboardCentre.y + (1 - r) * step;
            float left = keyboardCentre.x - (row.Length - 1) * 0.5f * step;

            for (int i = 0; i < row.Length; i++)
            {
                Key key = new Key { letter = row[i], pos = new Vector2(left + i * step, y) };

                key.box = MakeBox(key.pos, keySize, keySize, FillEmpty, keyboard);
                key.label = MakeText("Key", key.letter.ToString(), Ink,
                                     key.pos + new Vector2(0f, -letterHeight * 0.5f), keyboard);
                key.count = MakeText("Count", "", Bright,
                                     key.pos + new Vector2(keySize * 0.3f, keySize * 0.18f), keyboard);
                key.count.fontSize = fontSize * 0.6f;

                keys.Add(key);
            }
        }
    }

    void BuildButton()
    {
        buttonText = MakeText("ButtonLabel", buttonLabel, Ink,
                              new Vector2(0f, buttonY - letterHeight * 0.5f), overlay);
        buttonText.ForceMeshUpdate();

        buttonSize = new Vector2(buttonText.preferredWidth + buttonPadding * 2f,
                                 letterHeight + buttonPadding * 2f);

        buttonBox = MakeBox(new Vector2(0f, buttonY), buttonSize.x, buttonSize.y, FillEmpty, overlay);
        buttonBox.sortingOrder = sortingOrder;          // behind its own label
    }

    /// <summary>
    /// The reminder in the top corner. Shut it's just a heading and a warning; opened it lists
    /// what the earlier puzzles handed over. Nobody has to touch it.
    /// </summary>
    void BuildHint()
    {
        if (hintLines == null || hintLines.Length == 0) return;

        hint = new GameObject("Reminder").transform;
        hint.SetParent(overlay, false);

        float titleSize = letterHeight * 0.85f;
        float rowSize = letterHeight * 0.62f;
        float gap = letterHeight * 0.5f;

        Vector2 corner = TopLeft();
        float x = corner.x + hintMargin;
        float y = corner.y - hintMargin - titleSize;

        hintTitle = MakeText("Title", hintHeading, Ink, new Vector2(x, y), hint);
        SetUp(hintTitle, titleSize, TextAlignmentOptions.BaselineLeft);

        hintSub = MakeText("Prompt", hintPrompt, Faint, new Vector2(x, y - gap - rowSize * 0.5f), hint);
        SetUp(hintSub, rowSize * 0.8f, TextAlignmentOptions.BaselineLeft);

        // Labels first, so the answers can all start past the longest of them.
        float labelWidth = 0f;
        List<TextMeshPro> labels = new List<TextMeshPro>();
        foreach (string line in hintLines)
        {
            string label = line.Split('|')[0];
            TextMeshPro t = MakeText("Label", label, Faint, Vector2.zero, hint);
            SetUp(t, rowSize, TextAlignmentOptions.BaselineLeft);
            t.ForceMeshUpdate();
            labelWidth = Mathf.Max(labelWidth, t.preferredWidth);
            labels.Add(t);
        }

        float rowStep = rowSize + gap * 0.75f;
        float firstRow = y - gap * 2.6f - rowSize;   // clear of the line above, which stays on show
        float widest = 0f;

        for (int i = 0; i < hintLines.Length; i++)
        {
            string[] parts = hintLines[i].Split('|');
            float rowY = firstRow - i * rowStep;

            labels[i].rectTransform.localPosition = new Vector3(x, rowY, 0f);
            hintRows.Add(labels[i]);

            string value = parts.Length > 1 ? parts[1] : "";
            TextMeshPro t = MakeText("Answer", value, Ink,
                                     new Vector2(x + labelWidth + gap, rowY), hint);
            SetUp(t, rowSize, TextAlignmentOptions.BaselineLeft);
            t.ForceMeshUpdate();
            widest = Mathf.Max(widest, labelWidth + gap + t.preferredWidth);
            hintRows.Add(t);
        }

        hintTitle.ForceMeshUpdate();
        hintSub.ForceMeshUpdate();

        // The panel covers the heading and everything the rows will need, so it never resizes.
        float pad = gap * 0.8f;
        float width = Mathf.Max(widest, Mathf.Max(hintTitle.preferredWidth, hintSub.preferredWidth)) + pad * 2f;
        float top = corner.y - hintMargin + pad;
        float bottom = firstRow - (hintLines.Length - 1) * rowStep - pad;

        hintPanel = MakeBox(new Vector2(x - pad + width * 0.5f, (top + bottom) * 0.5f),
                            width, top - bottom, FillEmpty, hint);
        hintPanel.transform.SetAsFirstSibling();

        // Only the heading and its line of small print are clickable, so an open panel
        // doesn't swallow clicks meant for the keyboard underneath.
        float stripBottom = hintSub.rectTransform.localPosition.y - pad;
        hintHitCentre = new Vector2(x - pad + width * 0.5f, (top + stripBottom) * 0.5f);
        hintHitSize = new Vector2(width, top - stripBottom);

        ShowHint(false);
    }

    void SetUp(TextMeshPro t, float capHeight, TextAlignmentOptions align)
    {
        // Font size scales straight off the height, so this doesn't re-measure the font each time.
        t.fontSize = letterHeight > 0f ? fontSize * capHeight / letterHeight : fontSize;
        t.alignment = align;
        t.rectTransform.sizeDelta = new Vector2(30f, 10f);

        // Left-aligned text starts at the rect's left edge, so the pivot has to sit there too —
        // otherwise it draws half the rect's width away from where it's been positioned.
        t.rectTransform.pivot = new Vector2(0f, 0.5f);
    }

    void ShowHint(bool open)
    {
        hintOpen = open;
        hintSub.text = open ? hintPromptOpen : hintPrompt;
        foreach (TextMeshPro row in hintRows) row.gameObject.SetActive(open);
        if (hintPanel != null) hintPanel.gameObject.SetActive(open);
    }

    /// <summary>Top-left of what the camera can see, in this puzzle's own space.</summary>
    Vector2 TopLeft()
    {
        if (cam == null) cam = Camera.main;
        if (cam == null) return new Vector2(-8f, 4.5f);

        float depth = Mathf.Abs(transform.position.z - cam.transform.position.z);
        return overlay.InverseTransformPoint(cam.ViewportToWorldPoint(new Vector3(0f, 1f, depth)));
    }

    /// <summary>Second half: a blank per letter of the answer, and a loose tile for each one.</summary>
    void BuildSpelling()
    {
        List<char> wanted = Letters(answer);

        // Blanks, with a gap wherever the answer has a space.
        float width = (wanted.Count - 1) * slotSpacing + wordGap * Spaces(answer);
        float x = -width * 0.5f;

        foreach (char c in answer.ToUpperInvariant())
        {
            if (c == ' ') { x += wordGap; continue; }
            if (c < 'A' || c > 'Z') continue;

            Slot slot = new Slot { pos = new Vector2(x, slotRowY) };
            slot.line = MakeLine(slot.pos, slotLineLength, lineThickness, spelling);
            slots.Add(slot);
            x += slotSpacing;
        }

        // The letters the player picked, shuffled so the answer isn't just sitting there.
        List<char> loose = new List<char>(wanted);
        for (int i = loose.Count - 1; i > 0; i--)
        {
            int j = Random.Range(0, i + 1);
            (loose[i], loose[j]) = (loose[j], loose[i]);
        }

        float trayWidth = (loose.Count - 1) * traySpacing;
        for (int i = 0; i < loose.Count; i++)
        {
            Tile tile = new Tile
            {
                letter = loose[i],
                home = new Vector2(-trayWidth * 0.5f + i * traySpacing, trayRowY),
            };

            tile.root = new GameObject("Tile").transform;
            tile.root.SetParent(spelling, false);
            tile.root.localPosition = tile.home;

            tile.box = MakeBox(Vector2.zero, keySize, keySize, FillOne, tile.root);
            tile.label = MakeText("Letter", tile.letter.ToString(), Ink,
                                  new Vector2(0f, -letterHeight * 0.5f), tile.root);

            tiles.Add(tile);
        }

        if (spellingHint.Trim().Length > 0)
        {
            // Centred and multi-line, so it keeps MakeText's middle pivot rather than SetUp's left one.
            TextMeshPro hintText = MakeText("Hint", spellingHint, Ink, new Vector2(0f, spellingHintY), spelling);
            hintText.fontSize = fontSize * spellingHintScale;
            hintText.alignment = TextAlignmentOptions.Center;
            hintText.rectTransform.sizeDelta = new Vector2(16f, 6f);
        }

        spelling.gameObject.SetActive(true);
        keyboard.gameObject.SetActive(false);
        buttonBox.gameObject.SetActive(false);
        buttonText.gameObject.SetActive(false);
        if (hint != null) hint.gameObject.SetActive(false);   // it only ever helped with the picking
    }

    // ----------------------------------------------------------------- playing

    void Update()
    {
        if (phase == Phase.Done || overlay == null) return;

        if (message.gameObject.activeSelf && Time.time >= messageUntil)
            message.gameObject.SetActive(false);

        Vector2 point = MouseLocal();

        if (phase == Phase.Picking) UpdatePicking(point);
        else UpdateSpelling(point);
    }

    void UpdatePicking(Vector2 point)
    {
        bool overButton = Inside(point, new Vector2(0f, buttonY), buttonSize);
        if (overButton != hoveringButton)
        {
            hoveringButton = overButton;
            buttonText.color = hoveringButton ? Bright : Ink;
        }

        if (!Pressed()) return;

        if (hint != null && Inside(point, hintHitCentre, hintHitSize)) { ShowHint(!hintOpen); return; }

        if (overButton) { CheckPick(); return; }

        foreach (Key key in keys)
        {
            if (!Inside(point, key.pos, new Vector2(keySize, keySize))) continue;

            key.taken = (key.taken + 1) % (Mathf.Max(1, maxPerLetter) + 1);
            Play(keySound);
            RefreshKeys();
            return;
        }
    }

    void UpdateSpelling(Vector2 point)
    {
        if (Pressed())
        {
            for (int i = tiles.Count - 1; i >= 0; i--)
            {
                Tile tile = tiles[i];
                Vector2 at = tile.root.localPosition;
                if (!Inside(point, at, new Vector2(keySize, keySize))) continue;

                dragging = tile;
                dragGrab = at - point;
                tile.box.sortingOrder = sortingOrder + 6;
                tile.label.sortingOrder = sortingOrder + 7;
                break;
            }
            return;
        }

        if (dragging == null) return;

        if (Held())
        {
            dragging.root.localPosition = point + dragGrab;
            return;
        }

        Drop(dragging.root.localPosition);
    }

    void Drop(Vector2 at)
    {
        Tile tile = dragging;
        dragging = null;
        tile.box.sortingOrder = sortingOrder;
        tile.label.sortingOrder = sortingOrder + 1;

        int landed = -1;
        for (int i = 0; i < slots.Count; i++)
            if (Inside(at, slots[i].pos, new Vector2(slotSpacing, keySize * 1.4f))) { landed = i; break; }

        if (tile.slot >= 0) slots[tile.slot].tile = null;    // it's leaving wherever it was

        if (landed < 0)
        {
            tile.slot = -1;
            tile.root.localPosition = tile.home;
            return;
        }

        Tile sitting = slots[landed].tile;
        if (sitting != null)                                  // that blank was taken; send it home
        {
            sitting.slot = -1;
            sitting.root.localPosition = sitting.home;
        }

        tile.slot = landed;
        slots[landed].tile = tile;
        tile.root.localPosition = slots[landed].pos + new Vector2(0f, keySize * 0.55f);

        CheckSpelling();
    }

    void RefreshKeys()
    {
        foreach (Key key in keys)
        {
            key.box.color = key.taken == 0 ? FillEmpty : key.taken == 1 ? FillOne : FillTwo;
            key.count.text = key.taken == 0 ? "" : key.taken.ToString();
        }
    }

    void CheckPick()
    {
        Dictionary<char, int> wanted = Counts(Letters(answer));

        int picked = 0;
        foreach (Key key in keys)
        {
            picked += key.taken;
            wanted.TryGetValue(key.letter, out int need);
            if (key.taken != need) { Wrong(wrongPick); return; }
        }

        if (picked == 0) { Wrong(wrongPick); return; }

        phase = Phase.Spelling;
        Play(correctSound);
        message.gameObject.SetActive(false);
        BuildSpelling();
    }

    void CheckSpelling()
    {
        List<char> wanted = Letters(answer);

        for (int i = 0; i < slots.Count; i++)
            if (slots[i].tile == null) return;                // not full yet, nothing to judge

        for (int i = 0; i < slots.Count; i++)
            if (slots[i].tile.letter != wanted[i]) { Wrong(wrongSpelling); return; }

        phase = Phase.Done;
        dragging = null;
        message.gameObject.SetActive(false);
        Play(correctSound);
        if (onSolved != null) onSolved.Invoke();
        StartCoroutine(FadeOut());
    }

    void Wrong(string text)
    {
        message.text = text;
        message.gameObject.SetActive(true);
        messageUntil = Time.time + messageTime;
        Play(wrongSound);
    }

    /// <summary>Everything in the challenge sinks to black, then the next one takes over.</summary>
    IEnumerator FadeOut()
    {
        SpriteRenderer[] sprites = currentChallenge.GetComponentsInChildren<SpriteRenderer>();
        TMP_Text[] texts = currentChallenge.GetComponentsInChildren<TMP_Text>();

        Color[] spriteFrom = new Color[sprites.Length];
        Color[] textFrom = new Color[texts.Length];
        for (int i = 0; i < sprites.Length; i++) spriteFrom[i] = sprites[i].color;
        for (int i = 0; i < texts.Length; i++) textFrom[i] = texts[i].color;

        for (float t = 0f; t < fadeToBlack; t += Time.deltaTime)
        {
            float k = t / fadeToBlack;
            for (int i = 0; i < sprites.Length; i++) sprites[i].color = Darkened(spriteFrom[i], k);
            for (int i = 0; i < texts.Length; i++) texts[i].color = Darkened(textFrom[i], k);
            yield return null;
        }

        for (int i = 0; i < sprites.Length; i++) sprites[i].color = Darkened(spriteFrom[i], 1f);
        for (int i = 0; i < texts.Length; i++) texts[i].color = Darkened(textFrom[i], 1f);

        if (nextChallenge != null) nextChallenge.SetActive(true);
        else Debug.LogWarning("FinalRiddle: Next Challenge isn't assigned, so nothing follows the fade.", this);

        currentChallenge.SetActive(false);
    }

    /// <summary>Toward black, keeping whatever transparency the thing already had.</summary>
    static Color Darkened(Color from, float k)
    {
        Color c = from * (1f - k);
        c.a = from.a;
        return c;
    }

    // ------------------------------------------------------------------- bits

    static List<char> Letters(string text)
    {
        List<char> list = new List<char>();
        foreach (char c in text.ToUpperInvariant())
            if (c >= 'A' && c <= 'Z') list.Add(c);
        return list;
    }

    static int Spaces(string text)
    {
        int n = 0;
        foreach (char c in text.Trim()) if (c == ' ') n++;
        return n;
    }

    static Dictionary<char, int> Counts(List<char> letters)
    {
        Dictionary<char, int> counts = new Dictionary<char, int>();
        foreach (char c in letters)
        {
            counts.TryGetValue(c, out int n);
            counts[c] = n + 1;
        }
        return counts;
    }

    static bool Inside(Vector2 point, Vector2 centre, Vector2 size) =>
        Mathf.Abs(point.x - centre.x) <= size.x * 0.5f &&
        Mathf.Abs(point.y - centre.y) <= size.y * 0.5f;

    static bool Pressed() => Mouse.current != null && Mouse.current.leftButton.wasPressedThisFrame;
    static bool Held() => Mouse.current != null && Mouse.current.leftButton.isPressed;

    Vector2 MouseLocal()
    {
        if (cam == null) cam = Camera.main;
        if (cam == null || Mouse.current == null) return Vector2.one * 10000f;

        Vector2 screen = Mouse.current.position.ReadValue();
        float depth = Mathf.Abs(transform.position.z - cam.transform.position.z);
        Vector3 world = cam.ScreenToWorldPoint(new Vector3(screen.x, screen.y, depth));
        return overlay.InverseTransformPoint(world);
    }

    void Play(AudioClip clip)
    {
        if (clip != null && audioSource != null) audioSource.PlayOneShot(clip, volume);
    }

    // ------------------------------------------------------------- scaffolding

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
                GameObject probeGo = new GameObject("Probe");
                TextMeshPro probe = probeGo.AddComponent<TextMeshPro>();
                probe.rectTransform.SetParent(overlay, false);
                probe.font = font;
                probe.fontSize = probeSize;
                probe.color = Color.clear;
                probe.rectTransform.sizeDelta = new Vector2(10000f, 10000f);
                probe.text = "MM";
                probe.ForceMeshUpdate();

                float emAtProbe = 0f;
                if (probe.textInfo != null && probe.textInfo.characterCount >= 2)
                {
                    float advance = probe.textInfo.characterInfo[1].origin - probe.textInfo.characterInfo[0].origin;
                    emAtProbe = advance / advanceEm;
                }
                Destroy(probeGo);

                if (emAtProbe > 0.0001f) return probeSize * (height / capEm) / emAtProbe;
            }
        }

        return height * 12f;   // sane fallback if the font can't be measured
    }

    TextMeshPro MakeText(string name, string content, Color color, Vector2 pos, Transform parent)
    {
        GameObject go = new GameObject(name);
        TextMeshPro t = go.AddComponent<TextMeshPro>();
        go.transform.SetParent(parent, false);

        if (font != null) t.font = font;
        t.fontSize = fontSize > 0f ? fontSize : 2f;
        t.color = color;
        t.alignment = TextAlignmentOptions.Baseline;      // centred across, sitting on the baseline
        t.text = content;
        t.sortingOrder = sortingOrder + 1;
        t.rectTransform.sizeDelta = new Vector2(10f, 10f);
        t.rectTransform.localPosition = pos;
        return t;
    }

    void MakeBoxSprite()
    {
        boxTexture = new Texture2D(1, 1, TextureFormat.RGBA32, false)
        { filterMode = FilterMode.Point, wrapMode = TextureWrapMode.Clamp };
        boxTexture.SetPixel(0, 0, Color.white);
        boxTexture.Apply();

        boxSprite = Sprite.Create(boxTexture, new Rect(0, 0, 1, 1), new Vector2(0.5f, 0.5f), 1f);
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

    SpriteRenderer MakeBox(Vector2 pos, float width, float height, Color color, Transform parent)
    {
        SpriteRenderer sr = MakeSprite("Box", pos, boxSprite, parent);
        sr.transform.localScale = new Vector3(width, height, 1f);
        sr.color = color;
        return sr;
    }

    SpriteRenderer MakeLine(Vector2 pos, float length, float thickness, Transform parent)
    {
        SpriteRenderer sr = MakeSprite("Line", pos, lineSprite, parent);
        sr.transform.localScale = new Vector3(length / lineSprite.rect.width, thickness, 1f);
        sr.color = Faint;
        return sr;
    }

    SpriteRenderer MakeSprite(string name, Vector2 pos, Sprite sprite, Transform parent)
    {
        GameObject go = new GameObject(name);
        go.transform.SetParent(parent, false);
        go.transform.localPosition = pos;

        SpriteRenderer sr = go.AddComponent<SpriteRenderer>();
        sr.sprite = sprite;
        sr.sortingOrder = sortingOrder;
        return sr;
    }
}
