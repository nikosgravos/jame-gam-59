using System.Collections;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.InputSystem;

/// <summary>
/// A row of hangman blanks the player types a number into each of, and a button that checks
/// them. Get it right and the whole scene fades down to black and hands over to the next
/// challenge; get it wrong and it just says so and lets you keep going.
///
/// Click a blank, type digits (a blank holds more than one, so 10 goes in fine), backspace
/// to rub them out. Tab and the arrows walk along the row, Enter presses the button.
///
/// Nothing needs building by hand: drop this on an empty object and it draws itself.
/// </summary>
public class CodePuzzle : MonoBehaviour
{
    [Header("Puzzle")]
    [Tooltip("The numbers the player has to enter, in order, separated by spaces.")]
    public string code = "3 1 7 10 4";
    [Tooltip("Most digits one blank will take.")]
    public int maxDigits = 2;

    [Header("Look")]
    public TMP_FontAsset font;
    [Tooltip("Height of a digit, in this object's local units.")]
    public float letterHeight = 0.24f;
    [Tooltip("Distance between the middle of one blank and the next.")]
    public float columnSpacing = 0.7f;
    public float gapAboveLine = 0.07f;
    public float lineLength = 0.5f;
    public float lineThickness = 0.04f;
    [Tooltip("Dots in each blank. 1 draws a solid line instead.")]
    public int lineDots = 7;

    [Header("Button")]
    public string buttonLabel = "CHECK";
    [Tooltip("How far below the blanks the button sits.")]
    public float buttonGap = 0.8f;
    public float buttonPadding = 0.22f;
    public float buttonBorder = 0.035f;

    [Header("Getting it wrong")]
    public string wrongMessage = "TRY AGAIN";
    [Tooltip("Seconds the message stays up.")]
    public float wrongMessageTime = 2f;

    [Header("Colours")]
    public Color numberColor = new Color(0.96f, 0.92f, 0.8f, 1f);
    public Color lineColor = new Color(0.62f, 0.55f, 0.42f, 1f);
    public Color selectedColor = new Color(1f, 0.8f, 0.3f, 1f);
    public Color buttonColor = new Color(0.72f, 0.64f, 0.48f, 1f);
    public Color wrongColor = new Color(0.76f, 0.24f, 0.18f, 1f);

    [Header("When it's right")]
    [Tooltip("Seconds the whole scene takes to fade down to black.")]
    public float fadeToBlack = 2.5f;
    [Tooltip("Faded out and turned off. Defaults to the top of this object's hierarchy.")]
    public GameObject currentChallenge;
    [Tooltip("Turned on once the fade finishes.")]
    public GameObject nextChallenge;
    public UnityEvent onSolved;

    [Header("Audio")]
    public AudioClip typeSound;
    public AudioClip wrongSound;
    public AudioClip correctSound;
    [Range(0f, 1f)] public float volume = 0.6f;

    class Slot
    {
        public string want;                // the number that belongs in this blank
        public string typed = "";
        public float x;
        public TextMeshPro text;
        public SpriteRenderer line;
    }

    readonly List<Slot> slots = new List<Slot>();
    readonly List<char> typedDigits = new List<char>();

    int selected = -1;                     // -1 = nothing selected
    bool finished;
    float colW, slotY, buttonY;
    Vector2 buttonSize;

    TextMeshPro buttonText, wrongText;
    readonly List<SpriteRenderer> buttonEdges = new List<SpriteRenderer>();
    float wrongUntil;
    bool hoveringButton;

    AudioSource audioSource;
    Keyboard listeningTo;
    Texture2D lineTexture, boxTexture;
    Sprite lineSprite, boxSprite;
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
        if (boxSprite != null) Destroy(boxSprite);
        if (boxTexture != null) Destroy(boxTexture);
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
        string[] wanted = code.Split(new[] { ' ', '\t', ',' }, System.StringSplitOptions.RemoveEmptyEntries);
        if (wanted.Length == 0)
        {
            Debug.LogWarning("CodePuzzle: there are no numbers in the code.", this);
            return;
        }

        float fontSize = FontSizeFor(letterHeight);
        colW = columnSpacing;

        // Centre the blanks, the button and the message as one block.
        buttonSize = new Vector2(0f, letterHeight + buttonPadding * 2f);
        float top = gapAboveLine + letterHeight;
        float bottom = -buttonGap - buttonSize.y * 0.5f - wrongMessageGap() - letterHeight;
        float shift = -(top + bottom) * 0.5f;

        slotY = shift;
        buttonY = shift - buttonGap;

        MakeLineSprite();
        MakeBoxSprite();

        float left = -(wanted.Length - 1) * 0.5f * colW;
        for (int i = 0; i < wanted.Length; i++)
        {
            Slot slot = new Slot { want = wanted[i], x = left + i * colW };

            slot.line = MakeLine(new Vector2(slot.x, slotY), lineLength, lineThickness);
            slot.text = MakeText("Number", "", fontSize, numberColor,
                                 new Vector3(slot.x, slotY + gapAboveLine, 0f));
            slots.Add(slot);
        }

        BuildButton(fontSize);

        wrongText = MakeText("WrongMessage", wrongMessage, fontSize, wrongColor,
                             new Vector3(0f, buttonY - buttonSize.y * 0.5f - wrongMessageGap(), 0f));
        wrongText.rectTransform.sizeDelta = new Vector2(Mathf.Max(fontSize, 20f), Mathf.Max(fontSize, 20f));
        wrongText.gameObject.SetActive(false);

        Refresh();
    }

    float wrongMessageGap() => letterHeight + gapAboveLine * 2f;

    void BuildButton(float fontSize)
    {
        buttonText = MakeText("ButtonLabel", buttonLabel, fontSize, buttonColor,
                              new Vector3(0f, buttonY - letterHeight * 0.5f, 0f));
        buttonText.rectTransform.sizeDelta = new Vector2(Mathf.Max(fontSize, 20f), Mathf.Max(fontSize, 20f));
        buttonText.ForceMeshUpdate();

        buttonSize = new Vector2(buttonText.preferredWidth + buttonPadding * 2f,
                                 letterHeight + buttonPadding * 2f);

        // Four thin bars make the frame round the label.
        float hw = buttonSize.x * 0.5f, hh = buttonSize.y * 0.5f;
        buttonEdges.Add(MakeBox(new Vector2(0f, buttonY + hh), buttonSize.x, buttonBorder));
        buttonEdges.Add(MakeBox(new Vector2(0f, buttonY - hh), buttonSize.x, buttonBorder));
        buttonEdges.Add(MakeBox(new Vector2(-hw, buttonY), buttonBorder, buttonSize.y));
        buttonEdges.Add(MakeBox(new Vector2(hw, buttonY), buttonBorder, buttonSize.y));
    }

    /// <summary>Font size that draws a digit exactly <paramref name="height"/> units tall.</summary>
    float FontSizeFor(float height)
    {
        const float probeSize = 100f;

        if (font != null && font.characterLookupTable.TryGetValue('8', out TMP_Character eight) && eight.glyph != null)
        {
            float capEm = eight.glyph.metrics.horizontalBearingY / font.faceInfo.pointSize;
            float advanceEm = eight.glyph.metrics.horizontalAdvance / font.faceInfo.pointSize;

            if (capEm > 0f && advanceEm > 0f)
            {
                // Measure how big an em actually draws, rather than assuming TextMeshPro's scale factor.
                TextMeshPro probe = MakeText("Probe", "88", probeSize, Color.clear, Vector3.zero);
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

    void MakeBoxSprite()
    {
        boxTexture = new Texture2D(1, 1, TextureFormat.RGBA32, false)
        { filterMode = FilterMode.Point, wrapMode = TextureWrapMode.Clamp };
        boxTexture.SetPixel(0, 0, Color.white);
        boxTexture.Apply();

        boxSprite = Sprite.Create(boxTexture, new Rect(0, 0, 1, 1), new Vector2(0.5f, 0.5f), 1f);
    }

    SpriteRenderer MakeLine(Vector2 pos, float length, float thickness)
    {
        SpriteRenderer sr = MakeSprite("Line", pos, lineSprite);
        sr.transform.localScale = new Vector3(length / lineSprite.rect.width, thickness, 1f);
        sr.color = lineColor;
        return sr;
    }

    SpriteRenderer MakeBox(Vector2 pos, float width, float height)
    {
        SpriteRenderer sr = MakeSprite("Edge", pos, boxSprite);
        sr.transform.localScale = new Vector3(width, height, 1f);
        sr.color = buttonColor;
        return sr;
    }

    SpriteRenderer MakeSprite(string name, Vector2 pos, Sprite sprite)
    {
        GameObject go = new GameObject(name);
        go.transform.SetParent(transform, false);
        go.transform.localPosition = new Vector3(pos.x, pos.y, 0f);

        SpriteRenderer sr = go.AddComponent<SpriteRenderer>();
        sr.sprite = sprite;
        sr.sortingOrder = 3;
        return sr;
    }

    // ----------------------------------------------------------------- playing

    void Update()
    {
        if (finished || slots.Count == 0) { typedDigits.Clear(); return; }

        Listen();

        Vector3 local = transform.InverseTransformPoint(MouseWorldPosition());
        bool overButton = Mathf.Abs(local.x) <= buttonSize.x * 0.5f &&
                          Mathf.Abs(local.y - buttonY) <= buttonSize.y * 0.5f;

        if (overButton != hoveringButton)
        {
            hoveringButton = overButton;
            PaintButton();
        }

        if (Clicked())
        {
            if (overButton) Check();
            else SelectAt(local);
        }
        if (finished) { typedDigits.Clear(); return; }   // the fade has started; leave it alone

        foreach (char c in typedDigits) TypeDigit(c);

        Keyboard k = Keyboard.current;
        if (k != null)
        {
            if (k.backspaceKey.wasPressedThisFrame || k.deleteKey.wasPressedThisFrame) Erase();
            if (k.tabKey.wasPressedThisFrame) Step(k.shiftKey.isPressed ? -1 : 1);
            if (k.rightArrowKey.wasPressedThisFrame) Step(1);
            if (k.leftArrowKey.wasPressedThisFrame) Step(-1);
            if (k.enterKey.wasPressedThisFrame || k.numpadEnterKey.wasPressedThisFrame) Check();   // last, so nothing repaints after it
        }

        if (wrongText.gameObject.activeSelf && Time.time >= wrongUntil)
            wrongText.gameObject.SetActive(false);

        typedDigits.Clear();
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
        if (c >= '0' && c <= '9') typedDigits.Add(c);
    }

    static bool Clicked() => Mouse.current != null && Mouse.current.leftButton.wasPressedThisFrame;

    Vector3 MouseWorldPosition()
    {
        if (cam == null) cam = Camera.main;
        if (cam == null || Mouse.current == null) return Vector3.one * 10000f;

        Vector2 screen = Mouse.current.position.ReadValue();
        float depth = Mathf.Abs(transform.position.z - cam.transform.position.z);
        return cam.ScreenToWorldPoint(new Vector3(screen.x, screen.y, depth));
    }

    void SelectAt(Vector3 local)
    {
        for (int i = 0; i < slots.Count; i++)
        {
            if (Mathf.Abs(local.x - slots[i].x) > colW * 0.5f) continue;
            if (Mathf.Abs(local.y - slotY) > (letterHeight + gapAboveLine) * 1.5f) continue;

            selected = i;
            Refresh();
            return;
        }

        selected = -1;
        Refresh();
    }

    void TypeDigit(char digit)
    {
        if (selected < 0) return;

        Slot slot = slots[selected];
        if (slot.typed.Length >= Mathf.Max(1, maxDigits)) return;

        slot.typed += digit;
        Play(typeSound);
        Refresh();
    }

    void Erase()
    {
        if (selected < 0) return;

        Slot slot = slots[selected];
        if (slot.typed.Length == 0) return;

        slot.typed = slot.typed.Substring(0, slot.typed.Length - 1);
        Play(typeSound);
        Refresh();
    }

    void Step(int direction)
    {
        if (slots.Count == 0) return;

        selected = selected < 0 ? (direction > 0 ? 0 : slots.Count - 1)
                                : ((selected + direction) % slots.Count + slots.Count) % slots.Count;
        Refresh();
    }

    void Check()
    {
        foreach (Slot slot in slots)
            if (slot.typed != slot.want) { Wrong(); return; }

        finished = true;
        selected = -1;
        Refresh();
        Play(correctSound);
        if (onSolved != null) onSolved.Invoke();
        StartCoroutine(FadeOut());
    }

    void Wrong()
    {
        wrongText.text = wrongMessage;
        wrongText.gameObject.SetActive(true);
        wrongUntil = Time.time + wrongMessageTime;
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
        else Debug.LogWarning("CodePuzzle: Next Challenge isn't assigned, so nothing follows the fade.", this);

        currentChallenge.SetActive(false);
    }

    /// <summary>Toward black, keeping whatever transparency the thing already had.</summary>
    static Color Darkened(Color from, float k)
    {
        Color c = from * (1f - k);
        c.a = from.a;
        return c;
    }

    void Refresh()
    {
        for (int i = 0; i < slots.Count; i++)
        {
            bool isSelected = i == selected;
            slots[i].text.text = slots[i].typed;
            slots[i].text.color = isSelected ? selectedColor : numberColor;
            slots[i].line.color = isSelected ? selectedColor : lineColor;
        }

        PaintButton();
    }

    void PaintButton()
    {
        if (buttonText == null) return;

        Color c = hoveringButton ? selectedColor : buttonColor;
        buttonText.color = c;
        foreach (SpriteRenderer edge in buttonEdges) edge.color = c;
    }

    void Play(AudioClip clip)
    {
        if (clip != null && audioSource != null) audioSource.PlayOneShot(clip, volume);
    }
}
