using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Text.RegularExpressions;
using UnityEngine;
using Rnd = UnityEngine.Random;

public class BombItScript : MonoBehaviour
{
    // General
    public KMBombModule Module;
    public KMBombInfo BombInfo;
    public KMAudio Audio;
    public KMSelectable PlaySel;
    public GameObject FlagObj;
    public Texture[] FlagTextures;
    public bool IsTranslatedModule;
    private bool IsForcedMission;

    private int _moduleId;
    private bool _moduleSolved;

    private Coroutine _bombItSequence;
    private int _currentAction;
    private List<BombItAction> _requiredActions = new List<BombItAction>();
    private List<BombItAction> _inputActions = new List<BombItAction>();

    // Solve it!
    public KMSelectable StatusLightSel;
    // Tilt it!
    public GameObject Background;
    private bool _isTilted;
    // Press it!
    public KMSelectable ButtonSel;
    public GameObject ButtonCapObj;
    private Coroutine _buttonAnimation;
    // Flip it!
    public KMSelectable SwitchSel;
    public GameObject SwitchObj;
    private bool _switchState;
    private Coroutine _switchAnimation;
    // Snip it!
    public KMSelectable WireSel;
    public GameObject[] WireObjs;
    private bool _isSnipped;
    // Slide it!
    public KMSelectable SliderSel;
    public KMSelectable[] SliderRegionSels;
    public GameObject SliderObj;
    private bool _holdingSlider;
    private int _currentSliderPos;
    private Coroutine _sliderAnimation;

    private bool _sequencePlaying;
    private bool _actionExpected;
    private bool _actionSatisfied;
    private bool _actionExpectedAutosolve;
    private bool _solveItExpected;
    private bool _voicelinePlayed;
    private bool _wireCanStrike = true;
    private Coroutine _wireStrikeDelay;

    private bool _isActivated;
    private string[] _actionNames = new string[5];
    private string _moduleName;
    private string _currentVoiceOver = "";

    public enum BombItAction
    {
        PressIt,
        FlipIt,
        SnipIt,
        SlideIt,
        TiltIt,
        SolveIt
    }

    public class BombItSettings
    {
        public string LanguageCode;
    }

    BombItSettings Settings = new BombItSettings();

    public class BombItLanguage
    {
        /// <summary>Name of the language in English.</summary>
        public string LanguageName;
        /// <summary>Uppercase language code used in filenames (e.g. <c>"JA"</c>).</summary>
        public string FileCode;
        /// <summary>Translations of the action commands.</summary>
        public string[] ActionNames;
        /// <summary>Translation of “Solve it!”.</summary>
        public string SolveIt;
        /// <summary>Translated solve messages (only used in logging).</summary>
        public string[] SolveLines = DefaultSolveLines;
        private static readonly string[] DefaultSolveLines = { "POGGERS!", "You win, buddy!", "Sick solo, dude!", "High score!", "Tell your experts... if you have any!" };
        /// <summary>Translated strike messages (only used in logging).</summary>
        public string[] StrikeLines = DefaultStrikeLines;
        private static readonly string[] DefaultStrikeLines = { "YOWWWWW!", "Bummer.", "You blew it, dude.", "Do it the same, but, uh, better.", "Strikerooni, frienderini!" };

        /// <summary>(optional) Names of multiple voice set for the same language.</summary>
        public string[] VoiceSets = null;
        /// <summary>(optional) Set to <c>false</c> to disable language. Default is <c>true</c>.</summary>
        public bool IsSupported = true;
        /// <summary>Specifies whether this language is English.</summary>
        public bool IsEnglish = false;
    }

    private static readonly Dictionary<string, string> _presetMissionIds = new Dictionary<string, string>
    {
        ["mod_Communitworion_Communitworion"] = "JA",
        ["mod_awesome7285_missions_Definitely Soloable 1"] = "JA",
        ["mod_eXishMissions_drogryan"] = "JA",
        ["mod_witeksmissionpack_Modules Witek can solo"] = "JA",
        ["mod_arleenmission_Monsplode Red & Blue"] = "JA",
        ["mod_witeksmissionpack_Witek's 47"] = "JA"
    };

#pragma warning disable 0414
    private static readonly Dictionary<string, object>[] TweaksEditorSettings = new Dictionary<string, object>[]
    {
        new Dictionary<string, object>
        {
            { "Filename", "BombItTranslated-settings.txt" },
            { "Name", "Bomb It! Translated Settings" },
            { "Listings", new List<Dictionary<string, object>> {
                new Dictionary<string, object>
                {
                    ["Key"] = "LanguageCode",
                    ["Text"] = "LanguageCode",
                    ["Type"] = "Dropdown",
                    ["DropdownItems"] = new List<object> { "BG", "EO", "JA", "PL", "Random"}
                },
            }}
        }
    };
#pragma warning restore 0414

    private static readonly Dictionary<string, BombItLanguage> Languages = new Dictionary<string, BombItLanguage>
    {
        ["EN"] = new BombItLanguage
        {
            // Bomb It!
            LanguageName = "English",
            IsEnglish = true,
            FileCode = "EN",
            ActionNames = new string[]
            {
                "Press it!",
                "Flip it!",
                "Snip it!",
                "Slide it!",
                "Tilt it!"
            },
            SolveIt = "Solve it!"
        },

        ["JA"] = new BombItLanguage
        {
            // 爆弾！
            LanguageName = "Japanese",
            FileCode = "JA",
            ActionNames = new string[]
            {
                "押して！",
                "切り替えて！",
                "切って！",
                "スライドして！",
                "傾けて！",
            },
            SolveIt = "解除！",
            SolveLines = new[] { "最高！", "OK！よく頑張ったね！", "ナイス～！", "ウェーイ！", "う～ん！いいねぇ！" },
            StrikeLines = new[] { "あ～あ。", "がっかり。", "オメー何やってんの？", "次はもっと頑張ろう。", "ハイ、やらかした～。" }
        },

        ["PL"] = new BombItLanguage
        {
            // Zbombarduj to!
            LanguageName = "Polish",
            FileCode = "PL",
            ActionNames = new string[]
            {
                "Wciśnij to!",
                "Przełącz to!",
                "Utnij to!",
                "Przesuń to!",
                "Przechyl to!",
            },
            SolveIt = "Rozbrój to!",
            SolveLines = new[] { "Zrób sobie pierogi po tej sesji.", "Świetnie!", "Wygrałeś!", "Napij się wody, zasłużyłeś na to.", "Dobra robota! Teraz skup się na bombie." },
            StrikeLines = new[] { "Będę szczery, ale mogłeś się postarać.", "Ach, kurde", "Szkoda.", "Może ci się uda następnym razem.", "Każda dusza ma swoją ciemność, a ty masz jej za dużo." }
        },

        ["EO"] = new BombItLanguage
        {
            // Bombu!
            LanguageName = "Esperanto",
            FileCode = "EO",
            ActionNames = new string[]
            {
                "Puŝu!",
                "Ŝaltu!",
                "Tranĉu!",
                "Ŝovu!",
                "Klinu!"
            },
            SolveIt = "Solvu!",
            SolveLines = new[] { "Gratulojn!", "Bone farita!", "Mojose!", "Fina venko!", "Reĝo de la maldanĝerigistoj!" },
            StrikeLines = new[] { "Aj aj aj!", "Domaĝe!", "Fiasko!", "Pliboniĝu aŭ kabeu!", "Ne krokodilu!" },
        },

        ["BG"] = new BombItLanguage
        {
            // Бомбардирай!
            LanguageName = "Bulgarian",
            FileCode = "BG",
            ActionNames = new string[]
            {
                "Натисни!",
                "Обърни!",
                "Отрежи!",
                "Плъзни!",
                "Наклони!"
            },
            SolveIt = "Обезвреди!"
        },

        ["DE"] = new BombItLanguage
        {
            // Bombardieren!
            LanguageName = "German",
            FileCode = "DE",
            ActionNames = new string[]
            {
                "Drücken!",
                "Umlegen!",
                "Schnippeln!",
                "Schieben!",
                "Kippen!"
            },
            SolveIt = "Lösen!",
            IsSupported = false
        },

        ["RU"] = new BombItLanguage
        {
            // Bomb It!
            LanguageName = "Russian",
            FileCode = "RU",
            ActionNames = new string[]
            {
                "Press it!",
                "Flip it!",
                "Snip it!",
                "Slide it!",
                "Tilt it!"
            },
            SolveIt = "Solve it!",
            VoiceSets = new string[] { "Rand", "Megum", "Termet" },
            IsSupported = false
        },

        ["ES"] = new BombItLanguage
        {
            // Bombealo!
            LanguageName = "Spanish",
            FileCode = "ES",
            ActionNames = new string[]
            {
                "¡Púlsalo!",
                "¡Vóltealo!",
                "¡Córtalo!",
                "¡Deslízalo!",
                "¡Inclínalo!"
            },
            SolveIt = "¡Desármalo!",
            IsSupported = false
        },

        ["TR"] = new BombItLanguage
        {
            // Bombala!
            LanguageName = "Turkish",
            FileCode = "TR",
            ActionNames = new string[]
            {
                "Bas onu!",
                "Çevir onu!",
                "Kes onu!",
                "Kaydır onu!",
                "Eğil onu!"
            },
            SolveIt = "Çöz onu!",
            IsSupported = false
        },

        ["NL"] = new BombItLanguage
        {
            // Bombarderen!
            LanguageName = "Dutch",
            FileCode = "NL",
            ActionNames = new string[]
            {
                "Indrukken!",
                "Omzetten!",
                "Knippen!",
                "Schuiven!",
                "Kantelen!"
            },
            SolveIt = "Oplossen!",
            IsSupported = false
        },

        ["SV"] = new BombItLanguage
        {
            // Bomba den!
            LanguageName = "Swedish",
            FileCode = "SV",
            ActionNames = new string[]
            {
                "Trycka på den!",
                "Vänd den!",
                "Klipp den!",
                "Glida den!",
                "Luta den!"
            },
            SolveIt = "Lös det!",
            IsSupported = false
        }
    };

    private BombItLanguage CurrentLanguage;

    private static int _moduleIdCounter = 1;
    private static int _moduleIdCounterTranslated = 1;

    private void Start()
    {
        _moduleId = IsTranslatedModule ? _moduleIdCounterTranslated++ : _moduleIdCounter++;
        if (IsTranslatedModule)
            FlagObj.SetActive(false);
        Module.OnActivate += Activate;

        PlaySel.OnInteract += PlayPress;
        StatusLightSel.OnInteract += StatusLightPress;
        ButtonSel.OnInteract += ButtonPress;
        ButtonSel.OnInteractEnded += ButtonRelease;
        SwitchSel.OnInteract += SwitchFlip;
        WireSel.OnInteract += WirePress;
        SliderSel.OnInteract += SliderPress;
        SliderSel.OnInteractEnded += SliderRelease;
        for (int i = 0; i < SliderRegionSels.Length; i++)
        {
            SliderRegionSels[i].OnHighlight += SliderHighlight(i);
            SliderRegionSels[i].OnInteract += UnityEditorOnlySliderPress(i);
        }
    }

    private BombItLanguage GetLanguage()
    {
        if (!IsTranslatedModule)
            return Languages["EN"];

        string langCode;
        BombItLanguage setup;

        // Force language if on preset mission.
        var missionID = GetMissionID();
        if (_presetMissionIds.TryGetValue(missionID, out langCode) && Languages.TryGetValue(langCode, out setup))
        {
            IsForcedMission = true;
            Debug.LogFormat("[Bomb It! Translated #{0}] Preset mission detected ({1}). Forcing {2} language.", _moduleId, missionID, setup.LanguageName);
            return setup;
        }

        // Set language based on mission description.
        var missionDesc = KTMissionGetter.Mission.Description;
        var rgx = missionDesc == null ? null : Regex.Match(missionDesc, @"^\[BombItTranslated\]\s+(.*?)\s*$", RegexOptions.Multiline);
        if (rgx != null && rgx.Success)
        {
            if (Languages.TryGetValue(rgx.Groups[1].Value, out setup) && !setup.IsEnglish)
            {
                Debug.LogFormat("[Bomb It! Translated #{0}] Language code “{1}” found in mission description. Using {2} language.", _moduleId, setup.FileCode, setup.LanguageName);
                return setup;
            }
            Debug.LogFormat("[Bomb It! Translated #{0}] Invalid language code “{1}” found in mission description. Referring to ModSettings.", _moduleId, rgx.Groups[1].Value);
        }

        // If on Twitch Plays, pick a language at random
        if (TwitchPlaysActive)
            return Languages.Values.Where(l => l.IsSupported && !l.IsEnglish).PickRandom();

        // Set language based on Mod settings.
        var modConfig = new ModConfig<BombItSettings>("BombItTranslated-settings");
        Settings = modConfig.Read();

        if (string.IsNullOrEmpty(Settings.LanguageCode))
            Settings.LanguageCode = "JA";
        modConfig.Write(Settings);

        if (Settings.LanguageCode == "Random")
            return Languages.Values.Where(i => i.IsSupported && !i.IsEnglish).PickRandom();

        if (Settings.LanguageCode != null && Languages.TryGetValue(Settings.LanguageCode, out setup))
            return setup;

        return Languages["JA"];
    }

    private string GetMissionID()
    {
        try
        {
            Component gameplayState = GameObject.Find("GameplayState(Clone)").GetComponent("GameplayState");
            Type type = gameplayState.GetType();
            FieldInfo fieldMission = type.GetField("MissionToLoad", BindingFlags.Public | BindingFlags.Static);
            return fieldMission.GetValue(gameplayState).ToString();
        }
        catch (NullReferenceException)
        {
            return "undefined";
        }
    }

    private void Activate()
    {
        if (TwitchPlaysActive && !Application.isEditor)
            tpAPI = GameObject.Find("TwitchPlays_Info").GetComponent<IDictionary<string, object>>();

        // Set up language
        CurrentLanguage = GetLanguage();

        if (IsTranslatedModule)
            FlagObj.GetComponent<MeshRenderer>().material.mainTexture = FlagTextures[Array.IndexOf(FlagTextures.Select(i => i.name).ToArray(), CurrentLanguage.FileCode)];
        // Set up UI elements
        _moduleName = IsTranslatedModule ? "Bomb It! Translated" : "Bomb It!";
        _actionNames = CurrentLanguage.ActionNames.ToArray();

        // Logging
        if (TwitchPlaysActive && IsTranslatedModule && !IsForcedMission)
            Debug.LogFormat("[{0} #{1}] Twitch Plays activated. Choosing random language...", _moduleName, _moduleId);
        Debug.LogFormat("[{0} #{1}] Loaded module in {2} language.", _moduleName, _moduleId, CurrentLanguage.LanguageName);

        if (IsTranslatedModule)
            FlagObj.SetActive(true);
        _isActivated = true;
    }

    private void Update()
    {
        _isTilted = Vector3.Angle(Background.transform.up, Camera.main.transform.up) >= 45;
    }

    private bool PlayPress()
    {
        if (!_isActivated)
            return false;
        if (_wireStrikeDelay != null)
            StopCoroutine(_wireStrikeDelay);
        PlaySel.AddInteractionPunch(0.4f);
        if (_sequencePlaying)
            return false;
        GenerateSequence();
        _solveItExpected = false;
        return false;
    }

    private void GenerateSequence()
    {
        if (CurrentLanguage.VoiceSets != null)
            _currentVoiceOver = CurrentLanguage.VoiceSets.PickRandom();
        bool canSnip = false;
        if (!_isSnipped)
            canSnip = true;
        _requiredActions = new List<BombItAction>();
        _inputActions = new List<BombItAction>();
        var randActCount = Rnd.Range(6, 11);
        _currentAction = 0;
        for (int i = 0; i < randActCount; i++)
        {
            var action = (BombItAction)Rnd.Range(0, Enum.GetValues(typeof(BombItAction)).Length - 1);
            if (action == BombItAction.SnipIt && canSnip)
            {
                canSnip = false;
                _requiredActions.Add(action);
                continue;
            }
            if (action == BombItAction.SnipIt && !canSnip)
            {
                i--;
                continue;
            }
            _requiredActions.Add(action);
        }
        _bombItSequence = StartCoroutine(BombItSequence());
    }

    private bool StatusLightPress()
    {
        StatusLightSel.AddInteractionPunch(0.4f);
        if (_moduleSolved || !_isActivated || !_sequencePlaying)
            return false;
        if (!_actionExpected && _sequencePlaying)
        {
            Module.HandleStrike();
            PlayEndingVoiceLine(false);
            _sequencePlaying = false;
            if (_bombItSequence != null)
                StopCoroutine(_bombItSequence);
            if (_voicelinePlayed)
                Debug.LogFormat("<{0} #{1}> Attempted to Solve it! too early. Strike.", _moduleName, _moduleId);
            else
                Debug.LogFormat("<{0} #{1}> Attempted to Solve it! When action wasn't expected. Strike.", _moduleName, _moduleId);
            _actionSatisfied = false;
            _actionExpected = false;
            _actionExpectedAutosolve = false;
            _voicelinePlayed = false;
            return false;
        }
        _inputActions.Add(BombItAction.SolveIt);
        if (!_solveItExpected)
        {
            Module.HandleStrike();
            PlayEndingVoiceLine(false);
            _sequencePlaying = false;
            if (_bombItSequence != null)
                StopCoroutine(_bombItSequence);
            Debug.LogFormat("<{0} #{1}> Attempted to Solve it! When {2} was expected. Strike.", _moduleName, _moduleId, _requiredActions[_currentAction]);
            _actionSatisfied = false;
            _actionExpected = false;
            _actionExpectedAutosolve = false;
            _voicelinePlayed = false;
            return false;
        }
        _actionSatisfied = true;
        _actionExpected = false;
        _actionExpectedAutosolve = false;
        Module.HandlePass();
        PlayEndingVoiceLine(true);
        _moduleSolved = true;
        return false;
    }

    private bool ButtonPress()
    {
        ButtonSel.AddInteractionPunch(0.5f);
        if (_buttonAnimation != null)
            StopCoroutine(_buttonAnimation);
        _buttonAnimation = StartCoroutine(AnimateButton(-0.05f));
        Audio.PlayGameSoundAtTransform(KMSoundOverride.SoundEffect.BigButtonPress, transform);
        if (_moduleSolved || !_isActivated || !_sequencePlaying)
            return false;
        if (!_actionExpected && _sequencePlaying)
        {
            Module.HandleStrike();
            PlayEndingVoiceLine(false);
            _sequencePlaying = false;
            if (_bombItSequence != null)
                StopCoroutine(_bombItSequence);
            if (_voicelinePlayed)
                Debug.LogFormat("<{0} #{1}> Attempted to Press it! too early. Strike.", _moduleName, _moduleId);
            else
                Debug.LogFormat("<{0} #{1}> Attempted to Press it! When action wasn't expected. Strike.", _moduleName, _moduleId);
            _actionSatisfied = false;
            _actionExpected = false;
            _actionExpectedAutosolve = false;
            _voicelinePlayed = false;
            return false;
        }
        _inputActions.Add(BombItAction.PressIt);
        if (_solveItExpected || _inputActions[_currentAction] != _requiredActions[_currentAction])
        {
            Module.HandleStrike();
            PlayEndingVoiceLine(false);
            _sequencePlaying = false;
            if (_bombItSequence != null)
                StopCoroutine(_bombItSequence);
            Debug.LogFormat("<{0} #{1}> Attempted to Press it! When {2} was expected. Strike.", _moduleName, _moduleId, _requiredActions[_currentAction]);
            _actionSatisfied = false;
            _actionExpected = false;
            _actionExpectedAutosolve = false;
            _voicelinePlayed = false;
            return false;
        }
        _actionSatisfied = true;
        _actionExpected = false;
        _actionExpectedAutosolve = false;
        Audio.PlaySoundAtTransform("Press", transform);
        return false;
    }

    private void ButtonRelease()
    {
        if (_buttonAnimation != null)
            StopCoroutine(_buttonAnimation);
        _buttonAnimation = StartCoroutine(AnimateButton(0f));
        Audio.PlayGameSoundAtTransform(KMSoundOverride.SoundEffect.BigButtonRelease, transform);
    }

    private IEnumerator AnimateButton(float goalPos)
    {
        var duration = 0.1f;
        var elapsed = 0f;
        var startPos = ButtonCapObj.transform.localPosition.y;
        while (elapsed < duration)
        {
            ButtonCapObj.transform.localPosition = new Vector3(0f, Easing.InOutQuad(elapsed, startPos, goalPos, duration), 0f);
            yield return null;
            elapsed += Time.deltaTime;
        }
        ButtonCapObj.transform.localPosition = new Vector3(0f, goalPos, 0f);
    }

    private bool SwitchFlip()
    {
        SwitchSel.AddInteractionPunch(0.25f);
        if (_switchAnimation != null)
            StopCoroutine(_switchAnimation);
        Audio.PlayGameSoundAtTransform(KMSoundOverride.SoundEffect.ButtonPress, transform);
        _switchState = !_switchState;
        _switchAnimation = StartCoroutine(AnimateSwitch(_switchState));
        if (_moduleSolved || !_isActivated || !_sequencePlaying)
            return false;
        if (!_actionExpected && _sequencePlaying)
        {
            Module.HandleStrike();
            PlayEndingVoiceLine(false);
            _sequencePlaying = false;
            if (_bombItSequence != null)
                StopCoroutine(_bombItSequence);
            if (_voicelinePlayed)
                Debug.LogFormat("<{0} #{1}> Attempted to Flip it! too early. Strike.", _moduleName, _moduleId);
            else
                Debug.LogFormat("<{0} #{1}> Attempted to Flip it! When action wasn't expected. Strike.", _moduleName, _moduleId);
            _actionSatisfied = false;
            _actionExpected = false;
            _actionExpectedAutosolve = false;
            _voicelinePlayed = false;
            return false;
        }
        _inputActions.Add(BombItAction.FlipIt);
        if (_solveItExpected || _inputActions[_currentAction] != _requiredActions[_currentAction])
        {
            Module.HandleStrike();
            PlayEndingVoiceLine(false);
            _sequencePlaying = false;
            if (_bombItSequence != null)
                StopCoroutine(_bombItSequence);
            if (_solveItExpected)
                Debug.LogFormat("<{0} #{1}> Attempted to Flip it! When Solve it! was expected. Strike.", _moduleName, _moduleId);
            else
                Debug.LogFormat("<{0} #{1}> Attempted to Flip it! When {2} was expected. Strike.", _moduleName, _moduleId, _requiredActions[_currentAction]);
            _actionSatisfied = false;
            _actionExpected = false;
            _actionExpectedAutosolve = false;
            _voicelinePlayed = false;
            return false;
        }
        _actionSatisfied = true;
        _actionExpected = false;
        _actionExpectedAutosolve = false;
        Audio.PlaySoundAtTransform("Flip", transform);
        return false;
    }

    private IEnumerator AnimateSwitch(bool state)
    {
        var duration = 0.2f;
        var elapsed = 0f;
        var startAngle = SwitchObj.transform.localEulerAngles.x;
        var goalAngle = state ? -60f : 60f;
        if (startAngle - goalAngle > 180f)
            goalAngle += 360f;
        if (goalAngle - startAngle > 180f)
            startAngle += 360f;
        while (elapsed < duration)
        {
            SwitchObj.transform.localEulerAngles = new Vector3(Easing.InOutQuad(elapsed, startAngle, goalAngle, duration), 0f, 0f);
            yield return null;
            elapsed += Time.deltaTime;
        }
        SwitchObj.transform.localEulerAngles = new Vector3(goalAngle, 0f, 0f);
    }

    private bool WirePress()
    {
        if (!_isActivated)
            return false;
        WireSel.AddInteractionPunch(0.2f);
        if (_isSnipped)
            return false;
        Audio.PlayGameSoundAtTransform(KMSoundOverride.SoundEffect.WireSnip, transform);
        _isSnipped = true;
        WireObjs[0].SetActive(false);
        WireObjs[1].SetActive(true);
        if (_moduleSolved)
            return false;
        if (!_actionExpected)
        {
            if (!_wireCanStrike)
                return false;
            Module.HandleStrike();
            PlayEndingVoiceLine(false);
            _sequencePlaying = false;
            if (_bombItSequence != null)
                StopCoroutine(_bombItSequence);
            if (_voicelinePlayed)
                Debug.LogFormat("<{0} #{1}> Attempted to Snip it! too early. Strike.", _moduleName, _moduleId);
            else
                Debug.LogFormat("<{0} #{1}> Attempted to Snip it! When action wasn't expected. Strike.", _moduleName, _moduleId);
            _actionSatisfied = false;
            _actionExpected = false;
            _actionExpectedAutosolve = false;
            _voicelinePlayed = false;
            return false;
        }
        _inputActions.Add(BombItAction.SnipIt);
        if (_solveItExpected || _inputActions[_currentAction] != _requiredActions[_currentAction])
        {
            Module.HandleStrike();
            PlayEndingVoiceLine(false);
            _sequencePlaying = false;
            if (_bombItSequence != null)
                StopCoroutine(_bombItSequence);
            if (_solveItExpected)
                Debug.LogFormat("<{0} #{1}> Attempted to Snip it! When Solve it! was expected. Strike.", _moduleName, _moduleId);
            else
                Debug.LogFormat("<{0} #{1}> Attempted to Snip it! When {2} was expected. Strike.", _moduleName, _moduleId, _requiredActions[_currentAction]);
            _actionSatisfied = false;
            _actionExpected = false;
            _actionExpectedAutosolve = false;
            _voicelinePlayed = false;
            return false;
        }
        Audio.PlaySoundAtTransform("Snip", transform);
        _actionSatisfied = true;
        _actionExpected = false;
        _actionExpectedAutosolve = false;
        return false;
    }

    private bool SliderPress()
    {
        _holdingSlider = true;
        return false;
    }

    private void SliderRelease()
    {
        _holdingSlider = false;
    }

    private KMSelectable.OnInteractHandler UnityEditorOnlySliderPress(int i)
    {
        return delegate ()
        {
            if (!Application.isEditor)
                return false;
            HandleSlider(i);
            return false;
        };
    }

    private Action SliderHighlight(int i)
    {
        return delegate ()
        {
            if (!_holdingSlider)
                return;
            HandleSlider(i);
        };
    }

    private void HandleSlider(int i)
    {
        if (_currentSliderPos != i)
        {
            _currentSliderPos = i;
            if (_sliderAnimation != null)
                StopCoroutine(_sliderAnimation);
            _sliderAnimation = StartCoroutine(MoveSlider(_currentSliderPos));
            if (_moduleSolved || !_isActivated || !_sequencePlaying)
                return;
            if (!_actionExpected && _sequencePlaying)
            {
                Module.HandleStrike();
                PlayEndingVoiceLine(false);
                _sequencePlaying = false;
                if (_bombItSequence != null)
                    StopCoroutine(_bombItSequence);
                if (_voicelinePlayed)
                    Debug.LogFormat("<{0} #{1}> Attempted to Slide it! too early. Strike.", _moduleName, _moduleId);
                else
                    Debug.LogFormat("<{0} #{1}> Attempted to Slide it! When action wasn't expected. Strike.", _moduleName, _moduleId);
                _actionSatisfied = false;
                _actionExpected = false;
                _actionExpectedAutosolve = false;
                _voicelinePlayed = false;
                return;
            }
            _inputActions.Add(BombItAction.SlideIt);
            if (_solveItExpected || _inputActions[_currentAction] != _requiredActions[_currentAction])
            {
                Module.HandleStrike();
                PlayEndingVoiceLine(false);
                _sequencePlaying = false;
                if (_bombItSequence != null)
                    StopCoroutine(_bombItSequence);
                if (_solveItExpected)
                    Debug.LogFormat("<{0} #{1}> Attempted to Slide it! When Solve it! was expected. Strike.", _moduleName, _moduleId);
                else
                    Debug.LogFormat("<{0} #{1}> Attempted to Slide it! When {2} was expected. Strike.", _moduleName, _moduleId, _requiredActions[_currentAction]);
                _actionSatisfied = false;
                _actionExpected = false;
                _actionExpectedAutosolve = false;
                _voicelinePlayed = false;
                return;
            }
            Audio.PlaySoundAtTransform("Slide", transform);
            _actionSatisfied = true;
            _actionExpected = false;
            _actionExpectedAutosolve = false;
            return;
        }
    }

    private IEnumerator MoveSlider(int goal)
    {
        Audio.PlayGameSoundAtTransform(KMSoundOverride.SoundEffect.ButtonPress, transform);
        var startPos = SliderObj.transform.localPosition.x;
        var goalPos = goal == 0 ? -0.02f : 0.02f;
        var duration = 0.15f;
        var elapsed = 0f;
        while (elapsed < duration)
        {
            SliderObj.transform.localPosition = new Vector3(Easing.InOutQuad(elapsed, startPos, goalPos, duration), 0f, 0f);
            yield return null;
            elapsed += Time.deltaTime;
        }
        SliderObj.transform.localPosition = new Vector3(goalPos, 0f, 0f);
    }

    private void PlayKick()
    {
        Audio.PlaySoundAtTransform("DrumKick", transform);
    }
    private void PlayHat()
    {
        Audio.PlaySoundAtTransform("DrumHat", transform);
    }
    private void PlaySnare()
    {
        Audio.PlaySoundAtTransform("DrumSnare", transform);
    }

    private IEnumerator BombItSequence()
    {
        _sequencePlaying = true;
        PlayKick();
        yield return new WaitForSeconds(0.3f);
        PlayHat();
        yield return new WaitForSeconds(0.3f);
        PlaySnare();
        yield return new WaitForSeconds(0.3f);
        PlayHat();
        yield return new WaitForSeconds(0.3f);
        PlayKick();
        yield return new WaitForSeconds(0.3f);
        PlayHat();
        yield return new WaitForSeconds(0.3f);
        PlaySnare();
        yield return new WaitForSeconds(0.3f);
        PlayHat();
        yield return new WaitForSeconds(0.3f);
        while (_currentAction != _requiredActions.Count)
        {
            PlayKick();
            _voicelinePlayed = true;
            Audio.PlaySoundAtTransform(_requiredActions[_currentAction] + CurrentLanguage.FileCode + _currentVoiceOver, transform);
            Debug.LogFormat("[{0} #{1}] {2}", _moduleName, _moduleId, _actionNames[(int)_requiredActions[_currentAction]]);
            if (tpAPI != null && !Autosolved)
                tpAPI["ircConnectionSendMessage"] = $"Module {GetModuleCode()} ({_moduleName}) says: {_actionNames[(int)_requiredActions[_currentAction]]}";
            yield return new WaitForSeconds(0.3f);
            PlayHat();
            yield return new WaitForSeconds(0.3f);
            PlaySnare();
            yield return new WaitForSeconds(0.3f);
            PlayHat();
            yield return new WaitForSeconds(0.1f);
            _voicelinePlayed = false;
            _actionExpected = true;
            yield return new WaitForSeconds(0.2f);
            _actionExpectedAutosolve = true;
            PlayKick();
            int delay = 10;
            if (TwitchPlaysActive && !Autosolved)
                delay += 400;
            for (int i = 0; i < delay; i++)
            {
                if (_isTilted && _requiredActions[_currentAction] == BombItAction.TiltIt && !_actionSatisfied)
                {
                    _actionSatisfied = true;
                    _inputActions.Add(BombItAction.TiltIt);
                    Audio.PlaySoundAtTransform("Tilt", transform);
                }
                yield return new WaitForSeconds(0.015f);
            }
            yield return new WaitForSeconds(0.1f);
            _actionExpected = false;
            _actionExpectedAutosolve = false;
            if (!_actionSatisfied)
            {
                if (_requiredActions[_currentAction] == BombItAction.SnipIt)
                    _wireStrikeDelay = StartCoroutine(WireStrikeDelay());
                Module.HandleStrike();
                PlayEndingVoiceLine(false);
                _sequencePlaying = false;
                _actionSatisfied = false;
                _actionExpected = false;
                _actionExpectedAutosolve = false;
                _voicelinePlayed = false;
                yield break;
            }
            _actionSatisfied = false;
            PlayHat();
            yield return new WaitForSeconds(0.3f);
            PlaySnare();
            yield return new WaitForSeconds(0.3f);
            PlayHat();
            yield return new WaitForSeconds(0.3f);
            _currentAction++;
        }
        _solveItExpected = true;
        Audio.PlaySoundAtTransform("SolveIt" + CurrentLanguage.FileCode, transform);
        Debug.LogFormat("[{0} #{1}] {2}", _moduleName, _moduleId, CurrentLanguage.SolveIt);
        if (tpAPI != null && !Autosolved)
            tpAPI["ircConnectionSendMessage"] = $"Module {GetModuleCode()} ({_moduleName}) says: {CurrentLanguage.SolveIt}";
        PlayKick();
        yield return new WaitForSeconds(0.3f);
        PlayHat();
        yield return new WaitForSeconds(0.3f);
        PlaySnare();
        _actionExpected = true;
        yield return new WaitForSeconds(0.3f);
        PlayHat();
        yield return new WaitForSeconds(0.3f);
        PlayKick();
        _actionExpectedAutosolve = true;
        if (TwitchPlaysActive && !Autosolved)
            yield return new WaitForSeconds(8.2f);
        else
            yield return new WaitForSeconds(0.2f);
        _actionExpected = false;
        _actionExpectedAutosolve = false;
        if (!_actionSatisfied)
        {
            _solveItExpected = false;
            Module.HandleStrike();
            PlayEndingVoiceLine(false);
            _sequencePlaying = false;
            _actionSatisfied = false;
            _actionExpected = false;
            _actionExpectedAutosolve = false;
            _voicelinePlayed = false;
            yield break;
        }
        yield break;
    }

    private void PlayEndingVoiceLine(bool solve)
    {
        var ix = Rnd.Range(0, 5);
        Audio.PlaySoundAtTransform((solve ? "Solve" : "Strike") + (ix + 1) + CurrentLanguage.FileCode, transform);
        Debug.LogFormat("[{0} #{1}] {2}", _moduleName, _moduleId, (solve ? CurrentLanguage.SolveLines : CurrentLanguage.StrikeLines)[ix]);
    }

    private IEnumerator WireStrikeDelay()
    {
        _wireCanStrike = false;
        yield return new WaitForSeconds(0.5f);
        _wireCanStrike = true;
        yield break;
    }

    //twitch plays
    private IDictionary<string, object> tpAPI;
#pragma warning disable 0649
    private bool TwitchPlaysActive;
#pragma warning restore 0649
    private bool Autosolved;
#pragma warning disable 0414
    private readonly string TwitchHelpMessage = @"!{0} play [Presses the play button] | !{0} flip/slide/press/snip/tilt [Performs the specified action] | !{0} sl [Presses the status light] | On Twitch Plays there is an extra 8 seconds of leniency for performing actions and all actions are outputted to chat";
#pragma warning restore 0414

    private IEnumerator ProcessTwitchCommand(string command)
    {
        switch (command.ToLowerInvariant())
        {
            case "language":
                yield return $"sendtochat The module is using the {CurrentLanguage.LanguageName} langauge.";
                break;
            case "play":
            case "start":
                if (_sequencePlaying)
                {
                    yield return "sendtochaterror The module has already been started!";
                    yield break;
                }
                yield return null;
                yield return "strike";
                PlaySel.OnInteract();
                break;
            case "flip":
                if (!_sequencePlaying)
                {
                    yield return "sendtochaterror The module must be started first!";
                    yield break;
                }
                yield return null;
                SwitchSel.OnInteract();
                break;
            case "slide":
                if (!_sequencePlaying)
                {
                    yield return "sendtochaterror The module must be started first!";
                    yield break;
                }
                yield return null;
                SliderSel.OnInteract();
                if (_currentSliderPos == 0)
                    SliderRegionSels[1].OnHighlight();
                else
                    SliderRegionSels[0].OnHighlight();
                SliderSel.OnInteractEnded();
                break;
            case "snip":
                if (!_sequencePlaying)
                {
                    yield return "sendtochaterror The module must be started first!";
                    yield break;
                }
                if (_isSnipped)
                {
                    yield return "sendtochaterror The wire has already been snipped!";
                    yield break;
                }
                yield return null;
                WireSel.OnInteract();
                break;
            case "press":
                if (!_sequencePlaying)
                {
                    yield return "sendtochaterror The module must be started first!";
                    yield break;
                }
                yield return null;
                ButtonSel.OnInteract();
                ButtonSel.OnInteractEnded();
                break;
            case "sl":
            case "status light":
                if (!_sequencePlaying)
                {
                    yield return "sendtochaterror The module must be started first!";
                    yield break;
                }
                yield return null;
                StatusLightSel.OnInteract();
                break;
        }
    }

    private IEnumerator TwitchHandleForcedSolve()
    {
        Autosolved = true;
        if (!_sequencePlaying)
            PlaySel.OnInteract();
        while (!_moduleSolved)
        {
            while (!_actionExpected || _actionSatisfied) yield return null;
            while (!_actionExpectedAutosolve && !_solveItExpected && _requiredActions[_currentAction] != BombItAction.TiltIt) yield return null;
            while (_solveItExpected && !_actionExpectedAutosolve) yield return null;
            if (_solveItExpected)
                StatusLightSel.OnInteract();
            else
            {
                switch (_requiredActions[_currentAction])
                {
                    case BombItAction.PressIt:
                        ButtonSel.OnInteract();
                        yield return new WaitForSeconds(0.1f);
                        ButtonSel.OnInteractEnded();
                        break;
                    case BombItAction.FlipIt:
                        SwitchSel.OnInteract();
                        break;
                    case BombItAction.SnipIt:
                        WireSel.OnInteract();
                        break;
                    case BombItAction.SlideIt:
                        SliderSel.OnInteract();
                        if (_currentSliderPos == 0)
                            SliderRegionSels[1].OnHighlight();
                        else
                            SliderRegionSels[0].OnHighlight();
                        SliderSel.OnInteractEnded();
                        break;
                    default:
                        Transform mod = Module.transform;
                        Vector3 origAngles = mod.localEulerAngles;
                        float t = 0;
                        while (!_isTilted)
                        {
                            yield return null;
                            t += Time.deltaTime;
                            Vector3 angle = mod.localEulerAngles;
                            angle.x += 2f;
                            mod.localEulerAngles = angle;
                        }
                        while (!_actionSatisfied) yield return null;
                        float t2 = 0f;
                        while (t2 < t)
                        {
                            yield return null;
                            t2 += Time.deltaTime;
                            Vector3 angle = mod.localEulerAngles;
                            angle.x -= 2f;
                            mod.localEulerAngles = angle;
                        }
                        mod.localEulerAngles = origAngles;
                        break;
                }
            }
        }
    }

    // Gets the Twitch Plays ID for the module
    private string GetModuleCode()
    {
        Transform closest = null;
        float closestDistance = float.MaxValue;
        foreach (Transform children in transform.parent)
        {
            var distance = (transform.position - children.position).magnitude;
            if (children.gameObject.name == "TwitchModule(Clone)" && (closest == null || distance < closestDistance))
            {
                closest = children;
                closestDistance = distance;
            }
        }

        return closest?.Find("MultiDeckerUI").Find("IDText").GetComponent<UnityEngine.UI.Text>().text;
    }
}