using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using Rnd = UnityEngine.Random;

public class BombItScript : MonoBehaviour
{
    // General
    public KMBombModule Module;
    public KMBombInfo BombInfo;
    public KMAudio Audio;

    private int _moduleId;
    private static int _moduleIdCounter = 1;
    private bool _moduleSolved;

    public string Language;

    public KMSelectable PlaySel;
    private Coroutine _bombItSequence;
    private int _actionLength;
    private int _currentAction;
    private List<string> _requiredActions = new List<string>();
    private List<string> _inputActions = new List<string>();
    private static readonly string[] _actionNames = new string[] { "Press It!", "Tilt It!", "Flip It!", "Snip It!", "Slide It!" };
    private static readonly string[] _solveLines = new string[] { "POGGERS!", "You win, buddy!", "Sick solo, dude!", "High score!", "Tell your experts... if you have any!" };
    private static readonly string[] _strikeLines = new string[] { "YOWWWWW!", "Bummer.", "You blew it, dude.", "Do it the same, but, uh, better.", "Strikerooni, frienderini!" };

    private static readonly string[] _japaneseActionNames = new string[] { "押して！", "傾けて！", "切り替えて！", "切って！", "スライドして！" };
    
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
    private bool _solveItExpected;
    private bool _wireCanStrike = true;
    private Coroutine _wireStrikeDelay;

    private bool _japanese;

    public class BombItSettings
    {
        public bool UseJapaneseSounds;
    }

    private void Start()
    {
        _moduleId = _moduleIdCounter++;
        PlaySel.OnInteract += PlayPress;
        StatusLightSel.OnInteract += StatusLightPress;
        ButtonSel.OnInteract += ButtonPress;
        ButtonSel.OnInteractEnded += ButtonRelease;
        SwitchSel.OnInteract += SwitchFlip;
        WireSel.OnInteract += WirePress;
        SliderSel.OnInteract += SliderPress;
        SliderSel.OnInteractEnded += SliderRelease;

        if (Language != null)
        {
            if (Language == "Japanese")
                _japanese = true;
        }
        InitialLog();
        for (int i = 0; i < SliderRegionSels.Length; i++)
            SliderRegionSels[i].OnHighlight += SliderHighlight(i);
    }

    private void InitialLog()
    {
        if (_japanese)
            Debug.LogFormat("[Bomb It!{0} #{1}] 爆弾へようこそ！", _japanese ? " JA" : "", _moduleId);
        else
            Debug.LogFormat("[Bomb It!{0} #{0}] Welcome to Bomb It!", _japanese ? " JA" : "", _moduleId);
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
        _requiredActions = new List<string>();
        _inputActions = new List<string>();
        _actionLength = Rnd.Range(6, 10);
        _currentAction = 0;
        for (int i = 0; i < _actionLength; i++)
        {
            var action = _actionNames[Rnd.Range(0, _actionNames.Length)];
            if (action == "Snip It!" && canSnip)
            {
                canSnip = false;
                _requiredActions.Add(action);
                continue;
            }
            if (action == "Snip It!" && !canSnip)
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
            Debug.LogFormat("<Bomb It!{0} #{1}> Attempted to Solve It! When action wasn't expected. Strike.", _japanese ? " JA" : "", _moduleId);
            return false;
        }
        _inputActions.Add("Solve It!");
        if (!_solveItExpected)
        {
            Module.HandleStrike();
            PlayEndingVoiceLine(false);
            _sequencePlaying = false;
            if (_bombItSequence != null)
                StopCoroutine(_bombItSequence);
            Debug.LogFormat("<Bomb It!{0} #{1}> Attempted to Solve It! When {2} was expected. Strike.", _japanese ? " JA" : "", _moduleId, _requiredActions[_currentAction]);
            return false;
        }
        _actionSatisfied = true;
        _actionExpected = false;
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
            Debug.LogFormat("<Bomb It!{0} #{1}> Attempted to Press It! When action wasn't expected. Strike.", _japanese ? " JA" : "", _moduleId);
            return false;
        }
        _inputActions.Add("Press It!");
        if (_solveItExpected || _inputActions[_currentAction] != _requiredActions[_currentAction])
        {
            Module.HandleStrike();
            PlayEndingVoiceLine(false);
            _sequencePlaying = false;
            if (_bombItSequence != null)
                StopCoroutine(_bombItSequence);
            if (_solveItExpected)
                Debug.LogFormat("<Bomb It!{0} #{1}> Attempted to Press It! When Solve It! was expected. Strike.", _japanese ? " JA" : "", _moduleId);
            else
                Debug.LogFormat("<Bomb It!{0} #{1}> Attempted to Press It! When {2} was expected. Strike.", _japanese ? " JA" : "", _moduleId, _requiredActions[_currentAction]);
            return false;
        }
        _actionSatisfied = true;
        _actionExpected = false;
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
            Debug.LogFormat("<Bomb It!{0} #{1}> Attempted to Flip It! When action wasn't expected. Strike.", _japanese ? " JA" : "", _moduleId);
            return false;
        }
        _inputActions.Add("Flip It!");
        if (_solveItExpected || _inputActions[_currentAction] != _requiredActions[_currentAction])
        {
            Module.HandleStrike();
            PlayEndingVoiceLine(false);
            _sequencePlaying = false;
            if (_bombItSequence != null)
                StopCoroutine(_bombItSequence);
            if (_solveItExpected)
                Debug.LogFormat("<Bomb It!{0} #{1}> Attempted to Flip It! When Solve It! was expected. Strike.", _japanese ? " JA" : "", _moduleId);
            else
                Debug.LogFormat("<Bomb It!{0} #{1}> Attempted to Flip It! When {2} was expected. Strike.", _japanese ? " JA" : "", _moduleId, _requiredActions[_currentAction]);
            return false;
        }
        _actionSatisfied = true;
        _actionExpected = false;
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
            Debug.LogFormat("<Bomb It!{0} #{1}> Attempted to Snip It! When action wasn't expected. Strike.", _japanese ? " JA" : "", _moduleId);
            return false;
        }
        _inputActions.Add("Snip It!");
        if (_solveItExpected || _inputActions[_currentAction] != _requiredActions[_currentAction])
        {
            Module.HandleStrike();
            PlayEndingVoiceLine(false);
            _sequencePlaying = false;
            if (_bombItSequence != null)
                StopCoroutine(_bombItSequence);
            if (_solveItExpected)
                Debug.LogFormat("<Bomb It!{0} #{1}> Attempted to Snip It! When Solve It! was expected. Strike.", _japanese ? " JA" : "", _moduleId);
            else
                Debug.LogFormat("<Bomb It!{0} #{1}> Attempted to Snip It! When {2} was expected. Strike.", _japanese ? " JA" : "", _moduleId, _requiredActions[_currentAction]);
            return false;
        }
        Audio.PlaySoundAtTransform("Snip", transform);
        _actionSatisfied = true;
        _actionExpected = false;
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
                    Debug.LogFormat("<Bomb It!{0} #{1}> Attempted to Slide It! When action wasn't expected. Strike.", _japanese ? " JA" : "", _moduleId);
                    return;
                }
                _inputActions.Add("Slide It!");
                if (_solveItExpected || _inputActions[_currentAction] != _requiredActions[_currentAction])
                {
                    Module.HandleStrike();
                    PlayEndingVoiceLine(false);
                    _sequencePlaying = false;
                    if (_bombItSequence != null)
                        StopCoroutine(_bombItSequence);
                    if (_solveItExpected)
                        Debug.LogFormat("<Bomb It!{0} #{1}> Attempted to Slide It! When Solve It! was expected. Strike.", _japanese ? " JA" : "", _moduleId);
                    else
                        Debug.LogFormat("<Bomb It!{0} #{1}> Attempted to Slide It! When {2} was expected. Strike.", _japanese ? " JA" : "", _moduleId, _requiredActions[_currentAction]);
                    return;
                }
                Audio.PlaySoundAtTransform("Slide", transform);
                _actionSatisfied = true;
                _actionExpected = false;
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
        while (_currentAction != _actionLength)
        {
            PlayKick();
            PlayActionVoiceLine(_requiredActions[_currentAction]);
            Debug.LogFormat("[Bomb It!{0} #{1}] {2}", _japanese ? " JA" : "", _moduleId, GetAction(_requiredActions[_currentAction]));
            yield return new WaitForSeconds(0.3f);
            PlayHat();
            yield return new WaitForSeconds(0.3f);
            PlaySnare();
            yield return new WaitForSeconds(0.3f);
            PlayHat();
            yield return new WaitForSeconds(0.1f);
            _actionExpected = true;
            yield return new WaitForSeconds(0.2f);
            PlayKick();
            int delay = 10;
            if (TwitchPlaysActive)
                delay += 600;
            for (int i = 0; i < delay; i++)
            {
                if (_isTilted && _requiredActions[_currentAction] == "Tilt It!" && !_actionSatisfied)
                {
                    _actionSatisfied = true;
                    _inputActions.Add("Tilt It!");
                    Audio.PlaySoundAtTransform("Tilt", transform);
                }
                yield return new WaitForSeconds(0.02f);
            }
            yield return new WaitForSeconds(0.1f);
            _actionExpected = false;
            if (!_actionSatisfied)
            {
                if (_requiredActions[_currentAction] == "Snip It!")
                    _wireStrikeDelay = StartCoroutine(WireStrikeDelay());
                Module.HandleStrike();
                PlayEndingVoiceLine(false);
                _sequencePlaying = false;
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
        string str = "Solve It!";
        if (_japanese)
            str += " JA";
        Audio.PlaySoundAtTransform(str, transform);
        Debug.LogFormat("[Bomb It!{0} #{1}] {2}", _japanese ? " JA" : "", _moduleId, GetSolveAction());
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
        yield return new WaitForSeconds(0.2f);
        _actionExpected = false;
        if (!_actionSatisfied)
        {
            _solveItExpected = false;
            Module.HandleStrike();
            PlayEndingVoiceLine(false);
            _sequencePlaying = false;
            yield break;
        }
        yield break;
    }

    private string GetSolveAction()
    {
        if (_japanese)
            return "解除！";
        return "Solve It!";
    }

    private string GetAction(string action)
    {
        string a = action;
        if (_japanese)
            a = _japaneseActionNames[Array.IndexOf(_actionNames, action)];
        return a;
    }

    private void PlayActionVoiceLine(string soundName)
    {
        string str = soundName;
        if (_japanese)
            str += " JA";
        Audio.PlaySoundAtTransform(str, transform);
    }

    private void PlayEndingVoiceLine(bool solve)
    {
        int ix = Rnd.Range(0, 5);
        string str = (solve ? "Solve" : "Strike") + (ix + 1);
        if (_japanese)
            str += " JA";
        Audio.PlaySoundAtTransform(str, transform);
        Debug.LogFormat("[Bomb It!{0} #{1}] {2}", _japanese ? " JA" : "", _moduleId, solve ? _solveLines[ix] : _strikeLines[ix]);
    }

    private IEnumerator WireStrikeDelay()
    {
        _wireCanStrike = false;
        yield return new WaitForSeconds(0.5f);
        _wireCanStrike = true;
        yield break;
    }

    //twitch plays
    private bool TwitchPlaysActive;
    #pragma warning disable 414
    private readonly string TwitchHelpMessage = @"!{0} play [Presses the play button] | !{0} flip/slide/press/snip/tilt [Performs the specified action] | !{0} sl [Presses the status light] | On Twitch Plays there is an extra 12 seconds of leniency for performing actions";
    #pragma warning restore 414

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
        if (!_sequencePlaying)
            PlaySel.OnInteract();
        while (!_moduleSolved)
        {
            while (!_actionExpected || _actionSatisfied) yield return null;
            if (_solveItExpected)
                StatusLightSel.OnInteract();
            else
            {
                switch (_requiredActions[_currentAction])
                {
                    case "Press It!":
                        ButtonSel.OnInteract();
                        ButtonSel.OnInteractEnded();
                        break;
                    case "Flip It!":
                        SwitchSel.OnInteract();
                        break;
                    case "Snip It!":
                        WireSel.OnInteract();
                        break;
                    case "Slide It!":
                        SliderSel.OnInteract();
                        if (_currentSliderPos == 0)
                            SliderRegionSels[1].OnHighlight();
                        else
                            SliderRegionSels[0].OnHighlight();
                        SliderSel.OnInteractEnded();
                        break;
                    default:
                        Transform bomb;
                        if (Application.isEditor)
                            bomb = Module.transform.parent.parent;
                        else
                            bomb = Module.transform.parent;
                        float t = 0;
                        while (!_isTilted)
                        {
                            yield return null;
                            t += Time.deltaTime;
                            Vector3 angle = bomb.localEulerAngles;
                            angle.x += 2f;
                            bomb.localEulerAngles = angle;
                        }
                        while (_actionExpected) yield return null;
                        float t2 = 0f;
                        while (t2 < t)
                        {
                            yield return null;
                            t2 += Time.deltaTime;
                            Vector3 angle = bomb.localEulerAngles;
                            angle.x -= 2f;
                            bomb.localEulerAngles = angle;
                        }
                        break;
                }
            }
        }
    }
}
