using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using Rnd = UnityEngine.Random;

public class BombItScript : MonoBehaviour
{
    // General
    public KMBombModule Module;
    public KMBombInfo BombInfo;
    public KMAudio Audio;

    public string PublicVarLanguage;
    public KMSelectable PlaySel;
    public TextMesh BombItLabel;

    private int _moduleId;
    private bool _moduleSolved;


    private Coroutine _bombItSequence;
    private int _currentAction;
    private List<BombItAction> _requiredActions = new List<BombItAction>();
    private List<BombItAction> _inputActions = new List<BombItAction>();
    private static readonly string[] _solveLines = new string[] { "POGGERS!", "You win, buddy!", "Sick solo, dude!", "High score!", "Tell your experts... if you have any!" };
    private static readonly string[] _strikeLines = new string[] { "YOWWWWW!", "Bummer.", "You blew it, dude.", "Do it the same, but, uh, better.", "Strikerooni, frienderini!" };

    // Solve It!
    public KMSelectable StatusLightSel;
    // Tilt It!
    public GameObject Background;
    private bool _isTilted;
    // Press It!
    public KMSelectable ButtonSel;
    public GameObject ButtonCapObj;
    // Flip It!
    public KMSelectable SwitchSel;
    public GameObject SwitchObj;
    private bool _switchState;
    private bool _isSwitchAnimating;
    // Snip It!
    public KMSelectable WireSel;
    public GameObject[] WireObjs;
    private bool _isSnipped;
    // Slide It!
    public KMSelectable SliderSel;
    public KMSelectable[] SliderRegionSels;
    public GameObject SliderObj;
    private bool _holdingSlider;
    private int _currentSliderPos;

    private bool _sequencePlaying;
    private bool _actionExpected;
    private bool _actionSatisfied;
    private bool _actionExpectedAutosolve;
    private bool _solveItExpected;
    private bool _voicelinePlayed;
    private bool _wireCanStrike = true;
    private Coroutine _wireStrikeDelay;

    public enum BombItAction
    {
        PressIt,
        TiltIt,
        FlipIt,
        SnipIt,
        SlideIt,
        SolveIt
    }

    public class BombItLangSetup
    {
        public string Label;
        public string LetterCode;
        public string[] ActionNamesInLanguage;
        public string SolveItInLanguage;
        public string WelcomeMessage;

        public BombItLangSetup(string label, string letterCode, string[] anil, string solveIt, string welcome)
        {
            Label = label;
            ActionNamesInLanguage = anil;
            SolveItInLanguage = solveIt;
            WelcomeMessage = welcome;
            LetterCode = letterCode;
        }
    }

    private string[] ActionNames = new string[5];
    private string SolveItName;
    private string WelcomeMessage;
    private string ModuleName;
    private string[] EndingNames = new string[10] { "Solve1", "Solve2", "Solve3", "Solve4", "Solve5", "Strike1", "Strike2", "Strike3", "Strike4", "Strike5" };

    private readonly BombItLangSetup[] AllSetups = new BombItLangSetup[]
    {
        new BombItLangSetup(
            "Bomb It!",
            "",
            new string[] {
                "Press It!",
                "Tilt It!",
                "Flip It!",
                "Snip It!",
                "Slide It!"
            },
            "Solve It!",
            "Welcome to Bomb It!"
            ),
        new BombItLangSetup(
            "爆弾！",
            "JA",
            new string[] {
                "押して！",
                "傾けて！",
                "切り替えて！",
                "切って！",
                "スライドして！"
            },
            "解除！",
            "爆弾へようこそ！"
        )
    };

    private BombItLangSetup CurrentSetup;

    private static int[] _moduleIdCounters;

    private void Start()
    {
        if (PublicVarLanguage == "English")
            CurrentSetup = AllSetups[0];
        else if (PublicVarLanguage == "Japanese")
            CurrentSetup = AllSetups[1];
        else
            throw new InvalidOperationException("Invalid language");

        if (_moduleIdCounters == null)
        {
            _moduleIdCounters = new int[AllSetups.Length];
            for (int i = 0; i < _moduleIdCounters.Length; i++)
                _moduleIdCounters[i] = 1;
        }

        BombItLabel.text = CurrentSetup.Label;
        ModuleName = "Bomb It!" + (CurrentSetup.LetterCode.Length == 0 ? "" : " " + CurrentSetup.LetterCode);
        ActionNames = CurrentSetup.ActionNamesInLanguage.ToArray();
        SolveItName = CurrentSetup.SolveItInLanguage;
        WelcomeMessage = CurrentSetup.WelcomeMessage;
        EndingNames = EndingNames.Select(i => i.ToString() + CurrentSetup.LetterCode).ToArray();

        int ix = Array.IndexOf(AllSetups, CurrentSetup);
        _moduleId = _moduleIdCounters[ix]++;

        PlaySel.OnInteract += PlayPress;
        StatusLightSel.OnInteract += StatusLightPress;
        ButtonSel.OnInteract += ButtonPress;
        ButtonSel.OnInteractEnded += ButtonRelease;
        SwitchSel.OnInteract += SwitchFlip;
        WireSel.OnInteract += WirePress;
        SliderSel.OnInteract += SliderPress;
        SliderSel.OnInteractEnded += SliderRelease;

        Debug.LogFormat("<{0} #{1}> Loaded module in {2} language.", ModuleName, _moduleId, PublicVarLanguage);
        Debug.LogFormat("[{0} #{1}] {2}", ModuleName, _moduleId, WelcomeMessage);

        for (int i = 0; i < SliderRegionSels.Length; i++)
            SliderRegionSels[i].OnHighlight += SliderHighlight(i);

        Module.OnActivate += Activate;
    }

    private void Activate()
    {
        if (TwitchPlaysActive && !Application.isEditor)
        {
            GameObject tpAPIGameObject = GameObject.Find("TwitchPlays_Info");
            tpAPI = tpAPIGameObject.GetComponent<IDictionary<string, object>>();
        }
    }

    private void Update()
    {
        _isTilted = Vector3.Angle(Background.transform.up, Camera.main.transform.up) >= 45;
    }

    private bool PlayPress()
    {
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
        bool canSnip = false;
        if (!_isSnipped)
            canSnip = true;
        _requiredActions = new List<BombItAction>();
        _inputActions = new List<BombItAction>();
        var randActCount = Rnd.Range(6, 10);
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
        if (_moduleSolved || !_sequencePlaying)
            return false;
        if (!_actionExpected && _sequencePlaying)
        {
            Module.HandleStrike();
            PlayEndingVoiceLine(false);
            _sequencePlaying = false;
            if (_bombItSequence != null)
                StopCoroutine(_bombItSequence);
            if (_voicelinePlayed)
                Debug.LogFormat("<{0} #{1}> Attempted to Solve It! too early. Strike.", ModuleName, _moduleId);
            else
                Debug.LogFormat("<{0} #{1}> Attempted to Solve It! When action wasn't expected. Strike.", ModuleName, _moduleId);
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
            Debug.LogFormat("<{0} #{1}> Attempted to Solve It! When {2} was expected. Strike.", ModuleName, _moduleId, _requiredActions[_currentAction]);
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
        StartCoroutine(AnimateButton(0f, -0.05f));
        Audio.PlayGameSoundAtTransform(KMSoundOverride.SoundEffect.BigButtonPress, transform);
        if (_moduleSolved || !_sequencePlaying)
            return false;
        if (!_actionExpected && _sequencePlaying)
        {
            Module.HandleStrike();
            PlayEndingVoiceLine(false);
            _sequencePlaying = false;
            if (_bombItSequence != null)
                StopCoroutine(_bombItSequence);
            if (_voicelinePlayed)
                Debug.LogFormat("<{0} #{1}> Attempted to Press It! too early. Strike.", ModuleName, _moduleId);
            else
                Debug.LogFormat("<{0} #{1}> Attempted to Press It! When action wasn't expected. Strike.", ModuleName, _moduleId);
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
            Debug.LogFormat("<{0} #{1}> Attempted to Press It! When {2} was expected. Strike.", ModuleName, _moduleId, _requiredActions[_currentAction]);
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
        StartCoroutine(AnimateButton(-0.05f, 0f));
        Audio.PlayGameSoundAtTransform(KMSoundOverride.SoundEffect.BigButtonRelease, transform);
    }

    private IEnumerator AnimateButton(float a, float b)
    {
        var duration = 0.1f;
        var elapsed = 0f;
        while (elapsed < duration)
        {
            ButtonCapObj.transform.localPosition = new Vector3(0f, Easing.InOutQuad(elapsed, a, b, duration), 0f);
            yield return null;
            elapsed += Time.deltaTime;
        }
        ButtonCapObj.transform.localPosition = new Vector3(0f, b, 0f);
    }

    private bool SwitchFlip()
    {
        SwitchSel.AddInteractionPunch(0.25f);
        if (_isSwitchAnimating)
            return false;
        Audio.PlayGameSoundAtTransform(KMSoundOverride.SoundEffect.ButtonPress, transform);
        _switchState = !_switchState;
        StartCoroutine(AnimateSwitch());
        if (_moduleSolved || !_sequencePlaying)
            return false;
        if (!_actionExpected && _sequencePlaying)
        {
            Module.HandleStrike();
            PlayEndingVoiceLine(false);
            _sequencePlaying = false;
            if (_bombItSequence != null)
                StopCoroutine(_bombItSequence);
            if (_voicelinePlayed)
                Debug.LogFormat("<{0} #{1}> Attempted to Flip It! too early. Strike.", ModuleName, _moduleId);
            else
                Debug.LogFormat("<{0} #{1}> Attempted to Flip It! When action wasn't expected. Strike.", ModuleName, _moduleId);
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
                Debug.LogFormat("<{0} #{1}> Attempted to Flip It! When Solve It! was expected. Strike.", ModuleName, _moduleId);
            else
                Debug.LogFormat("<{0} #{1}> Attempted to Flip It! When {2} was expected. Strike.", ModuleName, _moduleId, _requiredActions[_currentAction]);
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

    private IEnumerator AnimateSwitch()
    {
        _isSwitchAnimating = true;
        var duration = 0.2f;
        var elapsed = 0f;
        while (elapsed < duration)
        {
            SwitchObj.transform.localEulerAngles = new Vector3(Easing.InOutQuad(elapsed, _switchState ? 60f : -60f, _switchState ? -60f : 60f, duration), 0f, 0f);
            yield return null;
            elapsed += Time.deltaTime;
        }
        SwitchObj.transform.localEulerAngles = new Vector3(_switchState ? -60f : 60f, 0f, 0f);
        _isSwitchAnimating = false;
    }

    private bool WirePress()
    {
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
                Debug.LogFormat("<{0} #{1}> Attempted to Snip It! too early. Strike.", ModuleName, _moduleId);
            else
                Debug.LogFormat("<{0} #{1}> Attempted to Snip It! When action wasn't expected. Strike.", ModuleName, _moduleId);
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
                Debug.LogFormat("<{0} #{1}> Attempted to Snip It! When Solve It! was expected. Strike.", ModuleName, _moduleId);
            else
                Debug.LogFormat("<{0} #{1}> Attempted to Snip It! When {2} was expected. Strike.", ModuleName, _moduleId, _requiredActions[_currentAction]);
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

    private Action SliderHighlight(int i)
    {
        return delegate ()
        {
            if (!_holdingSlider)
                return;
            if (_currentSliderPos != i)
            {
                _currentSliderPos = i;
                StartCoroutine(MoveSlider());
                if (_moduleSolved || !_sequencePlaying)
                    return;
                if (!_actionExpected && _sequencePlaying)
                {
                    Module.HandleStrike();
                    PlayEndingVoiceLine(false);
                    _sequencePlaying = false;
                    if (_bombItSequence != null)
                        StopCoroutine(_bombItSequence);
                    if (_voicelinePlayed)
                        Debug.LogFormat("<{0} #{1}> Attempted to Slide It! too early. Strike.", ModuleName, _moduleId);
                    else
                        Debug.LogFormat("<{0} #{1}> Attempted to Slide It! When action wasn't expected. Strike.", ModuleName, _moduleId);
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
                        Debug.LogFormat("<{0} #{1}> Attempted to Slide It! When Solve It! was expected. Strike.", ModuleName, _moduleId);
                    else
                        Debug.LogFormat("<{0} #{1}> Attempted to Slide It! When {2} was expected. Strike.", ModuleName, _moduleId, _requiredActions[_currentAction]);
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
        };
    }

    private IEnumerator MoveSlider()
    {
        Audio.PlayGameSoundAtTransform(KMSoundOverride.SoundEffect.ButtonPress, transform);
        var pos = SliderObj.transform.localPosition;
        var duration = 0.15f;
        var elapsed = 0f;
        while (elapsed < duration)
        {
            SliderObj.transform.localPosition = new Vector3(Easing.InOutQuad(elapsed, pos.x, _currentSliderPos == 0 ? -0.02f : 0.02f, duration), 0f, 0f);
            yield return null;
            elapsed += Time.deltaTime;
        }
        SliderObj.transform.localPosition = new Vector3(_currentSliderPos == 0 ? -0.02f : 0.02f, 0f, 0f);
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
            Audio.PlaySoundAtTransform(_requiredActions[_currentAction].ToString() + CurrentSetup.LetterCode, transform);
            Debug.LogFormat("[{0} #{1}] {2}", ModuleName, _moduleId, ActionNames[(int)_requiredActions[_currentAction]]);
            if (tpAPI != null && !Autosolved)
                tpAPI["ircConnectionSendMessage"] = "Module " + GetModuleCode() + " (" + ModuleName + ") says: " + ActionNames[(int)_requiredActions[_currentAction]];
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
                yield return new WaitForSeconds(0.02f);
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
        Audio.PlaySoundAtTransform("SolveIt" + CurrentSetup.LetterCode, transform);
        Debug.LogFormat("[{0} #{1}] {2}", ModuleName, _moduleId, SolveItName);
        if (tpAPI != null && !Autosolved)
            tpAPI["ircConnectionSendMessage"] = "Module " + GetModuleCode() + " (" + ModuleName + ") says: " + SolveItName;
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
        int ix = Rnd.Range(0, 5);
        if (solve)
        {
            Audio.PlaySoundAtTransform(EndingNames[ix], transform);
            Debug.LogFormat("[{0} #{1}] {2}", ModuleName, _moduleId, _solveLines[ix]);
        }
        else
        {
            Audio.PlaySoundAtTransform(EndingNames[ix + 5], transform);
            Debug.LogFormat("[{0} #{1}] {2}", ModuleName, _moduleId, _strikeLines[ix]);
        }
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
        command = command.ToLowerInvariant();
        switch (command)
        {
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

        return closest != null ? closest.Find("MultiDeckerUI").Find("IDText").GetComponent<UnityEngine.UI.Text>().text : null;
    }
}
