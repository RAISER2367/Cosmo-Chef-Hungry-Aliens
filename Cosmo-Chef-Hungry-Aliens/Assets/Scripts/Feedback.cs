using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Єдиний шар зворотного зв'язку для гравця. Нічого не треба ставити на сцену:
/// об'єкт [Feedback] створюється сам при першому виклику.
///
///   Feedback.Toast("текст")                 повідомлення зверху
///   Feedback.Good("текст") / Warn("текст")   зелене / червоне (+ звук помилки)
///   Feedback.Sfx("coin")                    звук (синтезується кодом, асети не потрібні)
///   Feedback.FloatText(pos, "+23", color)   текст, що злітає у світі
///   Feedback.SetPrompt / SetHand            підказка під прицілом і «що в руках»
///
/// Коли з'являться справжні звуки, достатньо підкласти AudioClip у словник overrides.
/// </summary>
public class Feedback : MonoBehaviour
{
    public static float MasterVolume = 0.6f;

    static Feedback _inst;
    static bool _quitting;

    static Feedback I
    {
        get
        {
            if (_quitting) return null;
            if (_inst == null)
            {
                var go = new GameObject("[Feedback]");
                DontDestroyOnLoad(go);
                _inst = go.AddComponent<Feedback>();
            }
            return _inst;
        }
    }

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    static void ResetStatics()
    {
        _inst = null;
        _quitting = false;
    }

    // ---------- Статичний API ----------

    public static void Toast(string msg, Color? color = null)
    {
        Feedback i = I;
        if (i != null) i.AddToast(msg, color ?? Color.white);
    }

    public static void Good(string msg) => Toast(msg, new Color(0.55f, 1f, 0.6f));

    public static void Warn(string msg)
    {
        Toast(msg, new Color(1f, 0.45f, 0.4f));
        Sfx("error");
    }

    public static void Sfx(string id, float volume = 1f, float pitch = 1f)
    {
        Feedback i = I;
        if (i != null) i.Play(id, volume, pitch);
    }

    public static void FloatText(Vector3 worldPos, string text, Color color, float size = 2.5f)
    {
        Feedback i = I;
        if (i != null) i.AddFloat(worldPos, text, color, size);
    }

    public static void SetPrompt(string text, bool actionable = true)
    {
        Feedback i = I;
        if (i == null) return;
        i._prompt = text ?? "";
        i._promptOk = actionable;
    }

    public static void SetHand(string text)
    {
        Feedback i = I;
        if (i != null) i._hand = text ?? "";
    }

    /// <summary>Підміна синтезованого звуку справжнім кліпом: Feedback.Overrides["coin"] = clip;</summary>
    public static readonly Dictionary<string, AudioClip> Overrides = new Dictionary<string, AudioClip>();

    // ---------- Внутрішній стан ----------

    class ToastItem
    {
        public TMP_Text text;
        public float age;
        public float life;
        public Color color;
    }

    class FloatItem
    {
        public GameObject go;
        public TextMeshPro text;
        public float age;
        public float life;
        public Vector3 start;
        public Color color;
    }

    TMP_FontAsset _font;
    Image _crossImg;
    RectTransform _crossRect;
    TMP_Text _promptText;
    TMP_Text _handText;
    RectTransform _toastRoot;

    readonly List<ToastItem> _toasts = new List<ToastItem>();
    readonly List<FloatItem> _floats = new List<FloatItem>();

    string _prompt = "";
    bool _promptOk = true;
    string _hand = "";
    string _shownPrompt = null;
    bool _shownOk;
    string _shownHand = null;

    string _lastToast;
    float _lastToastTime = -10f;

    AudioSource[] _sources;
    int _srcIdx;
    readonly Dictionary<string, AudioClip> _clips = new Dictionary<string, AudioClip>();

    void OnApplicationQuit() => _quitting = true;

    void Awake()
    {
        // Шрифт беремо з наявного TMP-тексту сцени: він уже підтримує кирилицю
        TMP_Text any = FindFirstObjectByType<TMP_Text>(FindObjectsInactive.Include);
        _font = any != null ? any.font : TMP_Settings.defaultFontAsset;

        BuildUI();

        _sources = new AudioSource[8];
        for (int i = 0; i < _sources.Length; i++)
        {
            var s = gameObject.AddComponent<AudioSource>();
            s.playOnAwake = false;
            s.spatialBlend = 0f;
            _sources[i] = s;
        }
    }

    // ---------- UI ----------

    void BuildUI()
    {
        var cgo = new GameObject("Canvas", typeof(RectTransform));
        cgo.transform.SetParent(transform, false);

        var canvas = cgo.AddComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = 500;

        var scaler = cgo.AddComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1920f, 1080f);
        scaler.matchWidthOrHeight = 0.5f;

        // Приціл
        var cross = new GameObject("Crosshair", typeof(RectTransform));
        cross.transform.SetParent(cgo.transform, false);
        _crossRect = cross.GetComponent<RectTransform>();
        _crossRect.anchorMin = _crossRect.anchorMax = new Vector2(0.5f, 0.5f);
        _crossRect.sizeDelta = new Vector2(6f, 6f);
        _crossImg = cross.AddComponent<Image>();
        _crossImg.raycastTarget = false;

        // Підказка під прицілом
        _promptText = MakeText("Prompt", cgo.transform, 30f, TextAlignmentOptions.Center);
        var pr = _promptText.rectTransform;
        pr.anchorMin = pr.anchorMax = new Vector2(0.5f, 0.5f);
        pr.pivot = new Vector2(0.5f, 1f);
        pr.anchoredPosition = new Vector2(0f, -40f);
        pr.sizeDelta = new Vector2(1000f, 50f);

        // Що в руках
        _handText = MakeText("Hand", cgo.transform, 28f, TextAlignmentOptions.Center);
        var hr = _handText.rectTransform;
        hr.anchorMin = hr.anchorMax = new Vector2(0.5f, 0f);
        hr.pivot = new Vector2(0.5f, 0f);
        hr.anchoredPosition = new Vector2(0f, 50f);
        hr.sizeDelta = new Vector2(1000f, 44f);

        // Стек повідомлень
        var root = new GameObject("Toasts", typeof(RectTransform));
        root.transform.SetParent(cgo.transform, false);
        _toastRoot = root.GetComponent<RectTransform>();
        _toastRoot.anchorMin = _toastRoot.anchorMax = new Vector2(0.5f, 1f);
        _toastRoot.pivot = new Vector2(0.5f, 1f);
        _toastRoot.anchoredPosition = new Vector2(0f, -90f);
        _toastRoot.sizeDelta = new Vector2(1000f, 10f);

        var layout = root.AddComponent<VerticalLayoutGroup>();
        layout.childAlignment = TextAnchor.UpperCenter;
        layout.childControlWidth = true;
        layout.childControlHeight = true;
        layout.childForceExpandWidth = true;
        layout.childForceExpandHeight = false;
        layout.spacing = 6f;

        var fitter = root.AddComponent<ContentSizeFitter>();
        fitter.verticalFit = ContentSizeFitter.FitMode.PreferredSize;
    }

    TextMeshProUGUI MakeText(string name, Transform parent, float size, TextAlignmentOptions align)
    {
        var go = new GameObject(name, typeof(RectTransform));
        go.transform.SetParent(parent, false);

        var t = go.AddComponent<TextMeshProUGUI>();
        if (_font != null) t.font = _font;
        t.fontSize = size;
        t.alignment = align;
        t.raycastTarget = false;
        t.color = Color.white;
        t.outlineWidth = 0.22f;
        t.outlineColor = new Color32(0, 0, 0, 255);
        return t;
    }

    // ---------- Toast / Float ----------

    void AddToast(string msg, Color color)
    {
        if (string.IsNullOrEmpty(msg)) return;
        if (msg == _lastToast && Time.unscaledTime - _lastToastTime < 0.7f) return;
        _lastToast = msg;
        _lastToastTime = Time.unscaledTime;

        TextMeshProUGUI t = MakeText("Toast", _toastRoot, 30f, TextAlignmentOptions.Center);
        t.text = msg;
        t.color = color;

        var le = t.gameObject.AddComponent<LayoutElement>();
        le.minHeight = 40f;
        le.preferredHeight = 40f;

        _toasts.Add(new ToastItem { text = t, life = 2.4f, color = color });

        while (_toasts.Count > 4)
        {
            Destroy(_toasts[0].text.gameObject);
            _toasts.RemoveAt(0);
        }
    }

    void AddFloat(Vector3 pos, string text, Color color, float size)
    {
        var go = new GameObject("FloatText");
        go.transform.position = pos;

        var t = go.AddComponent<TextMeshPro>();
        if (_font != null) t.font = _font;
        t.text = text;
        t.fontSize = size;
        t.alignment = TextAlignmentOptions.Center;
        t.color = color;
        t.rectTransform.sizeDelta = new Vector2(8f, 2f);

        _floats.Add(new FloatItem { go = go, text = t, life = 1.1f, start = pos, color = color });
    }

    // ---------- Update ----------

    void Update()
    {
        float dt = Time.unscaledDeltaTime;
        bool show = GameFlow.CanInteract;

        string prompt = show ? _prompt : "";
        string hand = show ? _hand : "";

        if (prompt != _shownPrompt || _promptOk != _shownOk)
        {
            _shownPrompt = prompt;
            _shownOk = _promptOk;
            _promptText.text = prompt.Length == 0 ? "" : (_promptOk ? "[E]  " + prompt : prompt);
            _promptText.color = _promptOk ? Color.white : new Color(1f, 0.6f, 0.55f);
        }

        if (hand != _shownHand)
        {
            _shownHand = hand;
            _handText.text = hand.Length == 0 ? "" : "У руках: " + hand;
        }

        _crossImg.enabled = show;
        bool has = show && prompt.Length > 0;
        _crossImg.color = has
            ? (_promptOk ? new Color(1f, 0.9f, 0.35f, 1f) : new Color(1f, 0.5f, 0.45f, 1f))
            : new Color(1f, 1f, 1f, 0.55f);
        float target = has ? 12f : 6f;
        float cur = Mathf.Lerp(_crossRect.sizeDelta.x, target, 20f * dt);
        _crossRect.sizeDelta = new Vector2(cur, cur);

        UpdateToasts(dt);
        UpdateFloats(dt);
    }

    void UpdateToasts(float dt)
    {
        for (int i = _toasts.Count - 1; i >= 0; i--)
        {
            ToastItem t = _toasts[i];
            t.age += dt;

            float fade = Mathf.Clamp01((t.life - t.age) / 0.4f);
            float pop = Mathf.Clamp01(t.age / 0.12f);

            t.text.color = new Color(t.color.r, t.color.g, t.color.b, fade);
            t.text.transform.localScale = Vector3.one * (0.8f + 0.2f * pop);

            if (t.age >= t.life)
            {
                Destroy(t.text.gameObject);
                _toasts.RemoveAt(i);
            }
        }
    }

    void UpdateFloats(float dt)
    {
        Camera cam = Camera.main;

        for (int i = _floats.Count - 1; i >= 0; i--)
        {
            FloatItem f = _floats[i];
            f.age += dt;

            float u = f.age / f.life;
            float fade = Mathf.Clamp01((1f - u) / 0.35f);
            float pop = Mathf.Clamp01(f.age / 0.1f);

            f.go.transform.position = f.start + Vector3.up * (f.age * 0.7f);
            f.go.transform.localScale = Vector3.one * (0.6f + 0.4f * pop);
            if (cam != null) f.go.transform.rotation = cam.transform.rotation;
            f.text.color = new Color(f.color.r, f.color.g, f.color.b, fade);

            if (f.age >= f.life)
            {
                Destroy(f.go);
                _floats.RemoveAt(i);
            }
        }
    }

    // ---------- Звук ----------

    void Play(string id, float vol, float pitch)
    {
        if (_sources == null) return;

        AudioClip clip;
        if (!Overrides.TryGetValue(id, out clip) || clip == null)
        {
            if (!_clips.TryGetValue(id, out clip))
            {
                clip = BuildClip(id);
                _clips[id] = clip;
            }
        }
        if (clip == null) return;

        AudioSource s = _sources[_srcIdx];
        _srcIdx = (_srcIdx + 1) % _sources.Length;

        s.clip = clip;
        s.volume = Mathf.Clamp01(vol * MasterVolume);
        s.pitch = pitch * Random.Range(0.97f, 1.03f);
        s.Play();
    }

    enum Wave { Sine, Tri, Square, Saw, Noise }

    struct Note
    {
        public float f0, f1, dur, gain, delay;
        public Wave wave;
    }

    static Note N(float f0, float f1, float dur, Wave w, float gain = 1f, float delay = 0f) =>
        new Note { f0 = f0, f1 = f1, dur = dur, wave = w, gain = gain, delay = delay };

    static readonly Dictionary<string, Note[]> Bank = new Dictionary<string, Note[]>
    {
        { "click",   new[] { N(900, 900, 0.04f, Wave.Sine, 0.6f) } },
        { "pick",    new[] { N(520, 760, 0.07f, Wave.Tri, 0.7f) } },
        { "drop",    new[] { N(320, 200, 0.08f, Wave.Tri, 0.7f) } },
        { "trash",   new[] { N(260, 80, 0.16f, Wave.Noise, 0.6f) } },
        { "cut",     new[] { N(170, 120, 0.05f, Wave.Square, 0.5f), N(1, 1, 0.04f, Wave.Noise, 0.5f) } },
        { "cutdone", new[] { N(523, 523, 0.06f, Wave.Sine), N(784, 784, 0.12f, Wave.Sine, 1f, 0.06f) } },
        { "ready",   new[] { N(880, 880, 0.08f, Wave.Sine), N(1320, 1320, 0.14f, Wave.Sine, 1f, 0.08f) } },
        { "warn",    new[] { N(440, 440, 0.08f, Wave.Square, 0.5f), N(440, 440, 0.08f, Wave.Square, 0.5f, 0.12f) } },
        { "burnt",   new[] { N(200, 70, 0.45f, Wave.Saw, 0.8f), N(1, 1, 0.3f, Wave.Noise, 0.4f) } },
        { "coin",    new[] { N(988, 988, 0.06f, Wave.Sine), N(1319, 1319, 0.18f, Wave.Sine, 1f, 0.06f) } },
        { "error",   new[] { N(180, 110, 0.2f, Wave.Square, 0.7f) } },
        { "bell",    new[] { N(660, 660, 0.14f, Wave.Sine, 0.8f), N(880, 880, 0.22f, Wave.Sine, 0.8f, 0.12f) } },
        { "happy",   new[] { N(523, 523, 0.08f, Wave.Sine), N(659, 659, 0.08f, Wave.Sine, 1f, 0.08f), N(784, 784, 0.18f, Wave.Sine, 1f, 0.16f) } },
        { "angry",   new[] { N(240, 110, 0.32f, Wave.Saw, 0.8f) } },
        { "lost",    new[] { N(392, 392, 0.12f, Wave.Sine, 0.8f), N(330, 330, 0.12f, Wave.Sine, 0.8f, 0.12f), N(262, 262, 0.24f, Wave.Sine, 0.8f, 0.24f) } },
        { "buy",     new[] { N(659, 659, 0.07f, Wave.Sine), N(988, 988, 0.16f, Wave.Sine, 1f, 0.07f) } },
        { "deliver", new[] { N(440, 440, 0.06f, Wave.Tri), N(660, 660, 0.06f, Wave.Tri, 1f, 0.07f), N(880, 880, 0.14f, Wave.Tri, 1f, 0.14f) } },
        { "step",    new[] { N(1, 1, 0.045f, Wave.Noise, 0.25f) } },
        { "start",   new[] { N(392, 392, 0.1f, Wave.Sine), N(523, 523, 0.1f, Wave.Sine, 1f, 0.1f), N(784, 784, 0.25f, Wave.Sine, 1f, 0.2f) } },
        { "end",     new[] { N(784, 784, 0.1f, Wave.Sine), N(659, 659, 0.1f, Wave.Sine, 1f, 0.1f), N(523, 523, 0.3f, Wave.Sine, 1f, 0.2f) } },
    };

    const int Rate = 44100;

    static AudioClip BuildClip(string id)
    {
        Note[] notes;
        if (!Bank.TryGetValue(id, out notes)) return null;

        float total = 0f;
        foreach (Note n in notes) total = Mathf.Max(total, n.delay + n.dur);

        int len = Mathf.CeilToInt(total * Rate) + 1;
        var data = new float[len];
        var rng = new System.Random(id.Length * 7919);

        foreach (Note n in notes)
        {
            int start = Mathf.RoundToInt(n.delay * Rate);
            int count = Mathf.RoundToInt(n.dur * Rate);
            double phase = 0.0;

            for (int i = 0; i < count && start + i < len; i++)
            {
                float u = (float)i / count;
                float t = (float)i / Rate;
                float f = Mathf.Lerp(n.f0, n.f1, u);

                phase += f / Rate;
                float ph = (float)(phase - System.Math.Floor(phase));

                float s;
                switch (n.wave)
                {
                    case Wave.Sine: s = Mathf.Sin(ph * Mathf.PI * 2f); break;
                    case Wave.Tri: s = 4f * Mathf.Abs(ph - 0.5f) - 1f; break;
                    case Wave.Square: s = ph < 0.5f ? 1f : -1f; break;
                    case Wave.Saw: s = 2f * ph - 1f; break;
                    default: s = (float)(rng.NextDouble() * 2.0 - 1.0); break;
                }

                float env = Mathf.Min(1f, t / 0.004f) * (1f - u) * (1f - u);
                data[start + i] += s * env * n.gain * 0.35f;
            }
        }

        AudioClip clip = AudioClip.Create("sfx_" + id, len, 1, Rate, false);
        clip.SetData(data, 0);
        return clip;
    }
}

/// <summary>Українські назви для підказок і повідомлень.</summary>
public static class Labels
{
    public static string Food(string food)
    {
        switch (food)
        {
            case "meat": return "м'ясо";
            case "cucumber": return "огірок";
            default: return food;
        }
    }

    public static string State(FoodState state, string food)
    {
        bool neuter = food == "meat";
        switch (state)
        {
            case FoodState.Raw: return neuter ? "сире" : "сирий";
            case FoodState.Cut: return neuter ? "нарізане" : "нарізаний";
            case FoodState.Ready: return neuter ? "готове" : "готовий";
            case FoodState.Burnt: return neuter ? "підгоріле" : "підгорілий";
            default: return state.ToString();
        }
    }

    public static string Item(FoodItem item)
    {
        if (item == null) return "";

        Dish dish = item as Dish;
        if (dish != null)
        {
            if (dish.parts.Count == 0) return "порожня тарілка";

            var names = new List<string>();
            foreach (FoodItem p in dish.parts) names.Add(Food(p.foodName) + " " + State(p.state, p.foodName));
            return "страва (" + string.Join(", ", names) + ")";
        }

        return Food(item.foodName) + " (" + State(item.state, item.foodName) + ")";
    }
}